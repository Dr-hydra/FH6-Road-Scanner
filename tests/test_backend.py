import io
import json
import unittest

from fh6_scanner.backend import (
    BackendServer,
    ProtocolWriter,
    configure_utf8_streams,
    parse_scan_params,
    serve,
)


class BackendParameterTests(unittest.TestCase):
    def test_legacy_string_values_are_accepted(self):
        params = parse_scan_params(
            {
                "scan_region": ["10", "20", "300", "400"],
                "detect_region": ["30", "40", "50", "60"],
                "step_x": "20",
                "step_y": "25",
                "move_delay": "0.006",
                "diff_threshold": "14",
                "start_delay": "5",
                "stop_on_hit": "false",
            }
        )

        self.assertEqual(params.scan_region, (10, 20, 300, 400))
        self.assertEqual(params.detect_region, (30, 40, 50, 60))
        self.assertEqual(params.step_x, 20)
        self.assertFalse(params.stop_on_hit)

    def test_invalid_region_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "扫描区域"):
            parse_scan_params(
                {
                    "scan_region": [0, 0, 0, 10],
                    "detect_region": [0, 0, 10, 10],
                    "step_x": 20,
                    "step_y": 20,
                    "move_delay": 0,
                    "diff_threshold": 14,
                    "start_delay": 0,
                }
            )


class ProtocolTests(unittest.TestCase):
    def test_stream_configuration_uses_utf8(self):
        class ReconfigurableStream:
            def __init__(self):
                self.options = None

            def reconfigure(self, **kwargs):
                self.options = kwargs

        import fh6_scanner.backend as backend

        original = backend.sys
        fake = type("FakeSys", (), {})()
        fake.stdin = ReconfigurableStream()
        fake.stdout = ReconfigurableStream()
        fake.stderr = ReconfigurableStream()
        backend.sys = fake
        try:
            configure_utf8_streams()
        finally:
            backend.sys = original

        for stream in (fake.stdin, fake.stdout, fake.stderr):
            self.assertEqual(stream.options, {"encoding": "utf-8", "errors": "replace"})

    def test_ping_reports_ready(self):
        output = io.StringIO()
        server = BackendServer(ProtocolWriter(output))
        server.handle(
            {
                "type": "command",
                "id": "ping-1",
                "command": "ping",
                "args": {},
            }
        )

        message = json.loads(output.getvalue())
        self.assertTrue(message["ok"])
        self.assertEqual(message["result"], {"ready": True})

    def test_unknown_command_returns_structured_error(self):
        output = io.StringIO()
        server = BackendServer(ProtocolWriter(output))
        server.handle(
            {
                "type": "command",
                "id": "request-1",
                "command": "does_not_exist",
                "args": {},
            }
        )

        message = json.loads(output.getvalue())
        self.assertEqual(message["type"], "response")
        self.assertEqual(message["id"], "request-1")
        self.assertFalse(message["ok"])
        self.assertIn("未知命令", message["error"])

    def test_shutdown_ends_serve_loop(self):
        requests = "\n".join(
            [
                json.dumps(
                    {
                        "type": "command",
                        "id": "shutdown-1",
                        "command": "shutdown",
                        "args": {},
                    }
                ),
                json.dumps(
                    {
                        "type": "command",
                        "id": "ignored",
                        "command": "does_not_exist",
                        "args": {},
                    }
                ),
            ]
        )
        output = io.StringIO()
        serve(io.StringIO(requests), ProtocolWriter(output))

        messages = [json.loads(line) for line in output.getvalue().splitlines()]
        self.assertEqual(len(messages), 1)
        self.assertTrue(messages[0]["ok"])
        self.assertEqual(messages[0]["result"], {"shutdown": True})

    def test_utf8_bom_is_accepted(self):
        request = {
            "type": "command",
            "id": "bom-1",
            "command": "shutdown",
            "args": {},
        }
        output = io.StringIO()
        serve(io.StringIO("\ufeff" + json.dumps(request)), ProtocolWriter(output))

        message = json.loads(output.getvalue())
        self.assertTrue(message["ok"])
        self.assertEqual(message["id"], "bom-1")


if __name__ == "__main__":
    unittest.main()
