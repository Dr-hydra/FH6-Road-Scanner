import ctypes

try:
    import winsound
except ImportError:
    winsound = None


# Windows DPI 修正
try:
    ctypes.windll.user32.SetProcessDPIAware()
except Exception:
    pass


def get_screen_size():
    return (
        ctypes.windll.user32.GetSystemMetrics(0),
        ctypes.windll.user32.GetSystemMetrics(1),
    )


def is_key_down(vk_code):
    """
    检测全局按键状态。
    即使游戏窗口在前台，也能检测到。
    """
    try:
        return bool(ctypes.windll.user32.GetAsyncKeyState(vk_code) & 0x8000)
    except Exception:
        return False


def move_mouse(x, y):
    """使用 Win32 API 立即移动鼠标。"""
    ctypes.windll.user32.SetCursorPos(int(x), int(y))


def beep():
    if winsound:
        winsound.Beep(1200, 160)
        winsound.Beep(900, 160)
        winsound.Beep(1200, 160)
    else:
        print("\a")
