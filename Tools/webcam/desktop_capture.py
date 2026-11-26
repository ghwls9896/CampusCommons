"""Read the active desktop window and capture only a selected window."""
from base64 import b64decode
from io import BytesIO
import json
import os
from pathlib import Path
import subprocess
import sys
from threading import Event

from screen_study import Window

MAC_HELPER = Path(__file__).with_name('study-window')


def desktop_provider():
    if sys.platform == 'darwin' and MAC_HELPER.is_file():
        return MacDesktop()
    if sys.platform == 'win32':
        try:
            from windows_capture import WindowsCapture
            return WindowsDesktop(WindowsCapture)
        except (ImportError, OSError):
            return None
    return None


class MacDesktop:
    def call(self, *args, timeout=3):
        try:
            result = subprocess.run([str(MAC_HELPER), *args], capture_output=True,
                                    timeout=timeout, check=True,
                                    env={**os.environ, 'CAMPUS_WINDOW_PARENT': str(os.getpid())})
            return json.loads(result.stdout)
        except (OSError, ValueError, subprocess.SubprocessError):
            return {}

    def permitted(self):
        return self.call('permission').get('permitted', False)

    def request_permission(self):
        return self.call('request', timeout=15).get('permitted', False)

    def windows(self):
        return [Window(**item) for item in self.call('list').get('windows', [])]

    def active(self):
        item = self.call('active').get('window')
        return Window(**item) if item else None

    def capture(self, window):
        payload = self.call('capture', window.id, str(window.pid), timeout=5)
        return b64decode(payload.get('image', ''), validate=True), payload.get('words', '')


class WindowsDesktop:
    def __init__(self, capture_type):
        import ctypes
        from ctypes import wintypes

        self.ctypes = ctypes
        self.types = wintypes
        self.user = ctypes.WinDLL('user32', use_last_error=True)
        self.user.GetForegroundWindow.restype = wintypes.HWND
        self.user.GetWindowThreadProcessId.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.DWORD)]
        self.user.GetWindowTextW.argtypes = [wintypes.HWND, wintypes.LPWSTR, ctypes.c_int]
        self.user.IsWindowVisible.argtypes = [wintypes.HWND]
        self.user.IsIconic.argtypes = [wintypes.HWND]
        self.enumerator = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
        self.user.EnumWindows.argtypes = [self.enumerator, wintypes.LPARAM]
        self.capture_type = capture_type

    def permitted(self):
        return True

    def request_permission(self):
        return True

    def window(self, hwnd):
        import psutil

        if not hwnd or not self.user.IsWindowVisible(hwnd) or self.user.IsIconic(hwnd):
            return None
        pid = self.types.DWORD()
        self.user.GetWindowThreadProcessId(hwnd, self.ctypes.byref(pid))
        title = self.ctypes.create_unicode_buffer(512)
        if not self.user.GetWindowTextW(hwnd, title, len(title)):
            return None
        try:
            app = psutil.Process(pid.value).name()
        except (psutil.NoSuchProcess, psutil.AccessDenied):
            return None
        return Window(str(hwnd), pid.value, app, app.removesuffix('.exe'), title.value)

    def active(self):
        return self.window(self.user.GetForegroundWindow())

    def windows(self):
        results = []

        @self.enumerator
        def append(hwnd, _):
            window = self.window(hwnd)
            if window:
                results.append(window)
            return len(results) < 50

        self.user.EnumWindows(append, 0)
        return results

    def capture(self, window):
        from PIL import Image

        if self.active() != window:
            return b'', ''
        capture = self.capture_type(window_hwnd=int(window.id), cursor_capture=False)
        done = Event()
        frames = []

        @capture.event
        def on_frame_arrived(frame, control):
            try:
                pixels = frame.frame_buffer
                # Copy only the pixels needed for the local screen classifier.
                step = max(1, (max(frame.width, frame.height) + 1023) // 1024)
                with Image.fromarray(pixels[::step, ::step, :3][:, :, ::-1]) as image:
                    with BytesIO() as stream:
                        image.save(stream, format='JPEG', quality=85)
                        frames.append(stream.getvalue())
            finally:
                control.stop()
                done.set()

        @capture.event
        def on_closed():
            done.set()

        control = capture.start_free_threaded()
        if not done.wait(2):
            control.stop()
        control.wait()
        return (frames[0], '') if frames else (b'', '')
