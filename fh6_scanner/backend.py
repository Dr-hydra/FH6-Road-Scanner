import json
import os
import sys
import threading
from typing import Any

import cv2

from .constants import OUT_DIR, TEMPLATE_PATH
from .scanner import ScanParams, run_scan
from .screen_utils import ScreenGrabber, diff_score


def configure_utf8_streams():
    for stream_name in ("stdin", "stdout", "stderr"):
        stream = getattr(sys, stream_name, None)
        reconfigure = getattr(stream, "reconfigure", None)
        if reconfigure is not None:
            reconfigure(encoding="utf-8", errors="replace")


def _number(value: Any, name: str, cast, minimum=None):
    try:
        result = cast(value)
    except (TypeError, ValueError) as exc:
        raise ValueError(f"{name} 必须是数字。") from exc
    if minimum is not None and result < minimum:
        raise ValueError(f"{name} 不能小于 {minimum}。")
    return result


def _region(value: Any, name: str):
    if not isinstance(value, (list, tuple)) or len(value) != 4:
        raise ValueError(f"{name} 必须包含 x、y、宽度和高度。")
    x = _number(value[0], f"{name} X", int, 0)
    y = _number(value[1], f"{name} Y", int, 0)
    width = _number(value[2], f"{name}宽度", int, 1)
    height = _number(value[3], f"{name}高度", int, 1)
    return x, y, width, height


def _boolean(value: Any, default: bool):
    if value is None:
        return default
    if isinstance(value, bool):
        return value
    if isinstance(value, str):
        normalized = value.strip().lower()
        if normalized in {"true", "1", "yes", "on"}:
            return True
        if normalized in {"false", "0", "no", "off"}:
            return False
    return bool(value)


def parse_scan_params(args: dict[str, Any]) -> ScanParams:
    if not isinstance(args, dict):
        raise ValueError("扫描参数格式错误。")
    return ScanParams(
        scan_region=_region(args.get("scan_region"), "扫描区域"),
        detect_region=_region(args.get("detect_region"), "检测区域"),
        step_x=_number(args.get("step_x"), "横向步长", int, 1),
        step_y=_number(args.get("step_y"), "纵向步长", int, 1),
        move_delay=_number(args.get("move_delay"), "移动延迟", float, 0),
        diff_threshold=_number(args.get("diff_threshold"), "差异阈值", float, 0),
        start_delay=_number(args.get("start_delay"), "开始延迟", float, 0),
        stop_on_hit=_boolean(args.get("stop_on_hit"), True),
    )


class ProtocolWriter:
    def __init__(self, stream=None):
        self.stream = stream or sys.stdout
        self.lock = threading.Lock()

    def send(self, payload: dict[str, Any]):
        line = json.dumps(payload, ensure_ascii=False, separators=(",", ":"))
        with self.lock:
            self.stream.write(line + "\n")
            self.stream.flush()

    def response(self, request_id: str, result=None, error: str | None = None):
        payload = {
            "type": "response",
            "id": request_id,
            "ok": error is None,
        }
        if error is None:
            payload["result"] = result or {}
        else:
            payload["error"] = error
        self.send(payload)

    def event(self, name: str, **payload):
        self.send({"type": "event", "event": name, **payload})


class BackendServer:
    def __init__(self, writer=None):
        self.writer = writer or ProtocolWriter()
        self.stop_event = threading.Event()
        self.scan_thread: threading.Thread | None = None
        self.shutdown_requested = False

    @property
    def is_scanning(self):
        return self.scan_thread is not None and self.scan_thread.is_alive()

    def handle(self, request: dict[str, Any]):
        request_id = str(request.get("id", ""))
        try:
            if request.get("type") != "command":
                raise ValueError("消息类型必须为 command。")
            command = request.get("command")
            args = request.get("args") or {}

            if command == "ping":
                result = {"ready": True}
            elif command == "capture_template":
                result = self.capture_template(args)
            elif command == "test_diff":
                result = self.test_diff(args)
            elif command == "start_scan":
                result = self.start_scan(args)
            elif command == "stop_scan":
                result = self.stop_scan()
            elif command == "shutdown":
                result = self.shutdown()
            else:
                raise ValueError(f"未知命令：{command}")
            self.writer.response(request_id, result=result)
        except Exception as exc:
            self.writer.response(request_id, error=str(exc))

    def capture_template(self, args):
        self._ensure_idle()
        region = _region(args.get("detect_region"), "检测区域")
        image = ScreenGrabber().grab_region(region)
        if not cv2.imwrite(TEMPLATE_PATH, image):
            raise OSError(f"无法写入模板文件：{TEMPLATE_PATH}")
        path = os.path.abspath(TEMPLATE_PATH)
        self.writer.event("log", text=f"已截取模板：{path}")
        return {"path": path}

    def test_diff(self, args):
        self._ensure_idle()
        if not os.path.exists(TEMPLATE_PATH):
            raise FileNotFoundError("缺少模板，请先截取或更新模板。")
        template = cv2.imread(TEMPLATE_PATH)
        if template is None:
            raise ValueError("模板读取失败，请重新截取模板。")
        region = _region(args.get("detect_region"), "检测区域")
        current = ScreenGrabber().grab_region(region)
        score = diff_score(current, template)
        self.writer.event("log", text=f"当前差异分数：{score:.2f}")
        return {"score": score}

    def start_scan(self, args):
        self._ensure_idle()
        if not os.path.exists(TEMPLATE_PATH):
            raise FileNotFoundError("缺少模板，请先截取或更新模板。")
        params = parse_scan_params(args)
        self.stop_event.clear()
        self.scan_thread = threading.Thread(
            target=self._scan_worker,
            args=(params,),
            name="fh6-scan-worker",
            daemon=True,
        )
        self.scan_thread.start()
        self.writer.event("scan_state", state="running")
        return {"started": True}

    def stop_scan(self):
        if self.is_scanning:
            self.stop_event.set()
            self.writer.event("scan_state", state="stopping")
            self.writer.event("log", text="正在请求停止扫描……")
        return {"stopping": self.is_scanning}

    def shutdown(self):
        self.stop_event.set()
        self.shutdown_requested = True
        return {"shutdown": True}

    def _ensure_idle(self):
        if self.is_scanning:
            raise RuntimeError("扫描正在运行，请先停止扫描。")

    def _should_stop(self):
        return self.stop_event.is_set()

    def _scan_worker(self, params: ScanParams):
        result = run_scan(
            params=params,
            stop_checker=self._should_stop,
            log=lambda text: self.writer.event("log", text=text),
            status=lambda text: self.writer.event("status", text=text),
            progress=lambda value: self.writer.event("progress", value=value),
            hit=lambda x, y, score, full_path, crop_path: self.writer.event(
                "hit",
                x=x,
                y=y,
                score=score,
                full_path=os.path.abspath(full_path),
                crop_path=os.path.abspath(crop_path),
            ),
        )
        state = {
            "completed": "completed",
            "hit": "completed",
            "error": "error",
            "stopped": "stopped",
        }.get(result, "stopped")
        self.writer.event("scan_state", state=state)


def serve(input_stream=None, writer=None):
    input_stream = input_stream or sys.stdin
    server = BackendServer(writer=writer)
    for raw_line in input_stream:
        raw_line = raw_line.lstrip("\ufeff")
        if not raw_line.strip():
            continue
        try:
            request = json.loads(raw_line)
        except json.JSONDecodeError as exc:
            server.writer.response("", error=f"无效 JSON：{exc.msg}")
            continue
        server.handle(request)
        if server.shutdown_requested:
            break


def main():
    configure_utf8_streams()
    os.makedirs(OUT_DIR, exist_ok=True)
    serve()


if __name__ == "__main__":
    main()
