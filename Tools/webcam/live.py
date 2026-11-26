"""Manual webcam test UI: browser -> loopback -> local VLM. No frame storage."""
import argparse
import html
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import os
from pathlib import Path
import secrets
import threading
from dataclasses import asdict

from qev_backend import QEVBackend
from desktop_capture import desktop_provider
from screen_study import ScreenStudy, combine

ROOT = Path(__file__).resolve().parent


def configure(server, backend, model_label, *, unity=False, desktop=None):
    server.backend = backend
    server.origin = f'http://127.0.0.1:{server.server_port}'
    server.token = os.environ.get('QEV_SESSION_TOKEN') or secrets.token_urlsafe(32)
    server.model_label = model_label
    server.busy = threading.Lock()
    server.model_lock = threading.Lock()
    server.unity = unity

    def classify_screen(image):
        # Skip optional screen inference if a webcam request already has the model.
        if not server.model_lock.acquire(blocking=False):
            return 'unknown'
        try:
            return backend.classify_screen(image)
        finally:
            server.model_lock.release()

    server.screen = ScreenStudy(desktop, classify_screen) if desktop else None


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass  # Do not log frames or model observations.

    def send(self, status, body, content_type='application/json; charset=utf-8'):
        if isinstance(body, dict):
            body = json.dumps(body, ensure_ascii=False).encode()
        self.send_response(status)
        self.send_header('Content-Type', content_type)
        self.send_header('Content-Length', str(len(body)))
        self.send_header('Cache-Control', 'no-store')
        self.send_header('X-Content-Type-Options', 'nosniff')
        self.send_header('Referrer-Policy', 'no-referrer')
        self.send_header('Permissions-Policy', 'camera=(self), microphone=()')
        self.send_header('Content-Security-Policy',
                         "default-src 'self'; script-src 'self'; style-src 'self'; "
                         "connect-src 'self'; img-src 'self' blob:; media-src 'self' blob:; "
                         "object-src 'none'; frame-ancestors 'none'; base-uri 'none'")
        self.end_headers()
        try:
            self.wfile.write(body)
        except (BrokenPipeError, ConnectionResetError):
            pass

    def fail(self, status, message):
        self.send(status, {'ok': False, 'error': message})

    def allowed_host(self):
        return self.headers.get('Host') == self.server.origin.removeprefix('http://')

    def do_GET(self):
        if not self.allowed_host():
            return self.fail(403, '허용되지 않은 주소입니다.')
        if self.path == '/' and not self.server.unity:
            page = (ROOT / 'index.html').read_text(encoding='utf-8')
            page = page.replace('__SESSION_TOKEN__', self.server.token)
            page = page.replace('__MODEL_LABEL__', html.escape(self.server.model_label))
            return self.send(200, page.encode(), 'text/html; charset=utf-8')
        if self.path in ('/app.js', '/style.css') and not self.server.unity:
            mime = 'text/javascript' if self.path.endswith('.js') else 'text/css'
            return self.send(200, (ROOT / self.path[1:]).read_bytes(), mime + '; charset=utf-8')
        if self.path == '/api/config':
            return self.send(200, {'interval_ms': 1000, 'mode': getattr(self.server.backend, 'mode', 'fast'),
                                   'device': self.server.backend.device,
                                   'combined_study': self.server.unity,
                                   'window_capture': self.server.screen is not None,
                                   'model': self.server.model_label})
        if self.path == '/favicon.ico':
            return self.send(204, b'')
        self.fail(404, '찾을 수 없습니다.')

    def do_POST(self):
        if (not self.allowed_host() or self.headers.get('Origin') != self.server.origin or
                not secrets.compare_digest(self.headers.get('X-Session-Token', ''), self.server.token)):
            return self.fail(403, '이 테스트 화면에서 시작해 주세요.')
        if self.server.unity and self.path.startswith('/api/study/'):
            return self.window_command()
        expected = '/api/study-frame' if self.server.unity else '/api/infer'
        if self.path != expected:
            return self.fail(404, '찾을 수 없습니다.')
        if self.headers.get('Content-Type') != 'image/jpeg' or self.headers.get('Transfer-Encoding'):
            return self.fail(400, 'JPEG 사진만 지원합니다.')
        try:
            length = int(self.headers.get('Content-Length', '0'))
            if not 0 < length <= 2 * 1024 * 1024:
                return self.fail(413, '사진 크기는 2 MiB 이하여야 합니다.')
            self.connection.settimeout(10)
            data = self.rfile.read(length)
            if len(data) != length or not data.startswith(b'\xff\xd8\xff'):
                raise ValueError('Incomplete image')
        except (OSError, ValueError):
            return self.fail(400, '사진을 읽지 못했습니다.')
        if not self.server.busy.acquire(blocking=False):
            return self.fail(429, '이전 사진을 분석 중입니다. 잠시 후 다시 시도합니다.')
        try:
            if self.server.unity:
                screen = self.server.screen
                window = screen.active_at_capture() if screen else None
                with self.server.model_lock:
                    camera = self.server.backend.classify_pose(data)
                result = {'ok': True, 'mode': camera['mode']}
                if camera.get('observed', True):
                    scores = camera['probabilities']
                    evidence = (screen.observe(window) if screen and camera['state'] in ('paper', 'screen')
                                and scores['paper'] <= .5 else 'unknown')
                    result.update(combine(scores, evidence))
                else:
                    result.update(observed=False, screen='unknown', reason='camera_unreadable')
            else:
                result = self.server.backend.classify(data)
        except Exception as error:
            # Exception types are sufficient for debugging; never log a frame.
            print(f'QEV inference failed: {type(error).__name__}', flush=True)
            return self.fail(502, '모델 분석 중 오류가 발생했습니다. 실행 터미널을 확인해 주세요.')
        finally:
            self.server.busy.release()
        self.send(200, result)

    def window_command(self):
        screen = self.server.screen
        if screen is None:
            return self.fail(404, '이 기기에서 공부 창 확인을 준비해 주세요.')
        command = self.path.removeprefix('/api/study/')
        try:
            if command == 'windows':
                permitted = screen.desktop.permitted()
                windows = [asdict(window) for window in screen.windows()] if permitted else []
                return self.send(200, {'ok': True, 'permitted': permitted, 'windows': windows})
            if command == 'permission':
                return self.send(200, {'ok': True, 'permitted': screen.desktop.request_permission()})
            if command == 'clear':
                screen.clear()
                return self.send(200, {'ok': True})
            if command == 'select':
                length = int(self.headers.get('Content-Length', '0'))
                if not 0 < length <= 512:
                    return self.fail(400, '창 선택 정보가 올바르지 않습니다.')
                self.connection.settimeout(2)
                data = json.loads(self.rfile.read(length))
                if (not isinstance(data, dict) or not isinstance(data.get('id'), str) or
                        not data['id'].isdigit() or type(data.get('pid')) is not int):
                    return self.fail(400, '창 선택 정보가 올바르지 않습니다.')
                if not screen.desktop.permitted() or not screen.select(data['id'], data['pid']):
                    return self.fail(409, '창이 닫혔거나 선택할 수 없습니다. 목록을 새로 고쳐 주세요.')
                return self.send(200, {'ok': True})
        except (OSError, ValueError):
            return self.fail(400, '공부 창 정보를 확인하지 못했습니다.')
        self.fail(404, '찾을 수 없습니다.')


def start_parent_watchdog():
    """Exit even if Unity crashes or reloads without managed cleanup."""
    parent = os.environ.get('QEV_PARENT_PID')
    if not parent:
        return
    import psutil
    import time
    try:
        owner = psutil.Process(int(parent))
        created = owner.create_time()
    except (ValueError, psutil.Error):
        raise RuntimeError('Parent process is unavailable')
    def watch():
        while True:
            time.sleep(2)
            try:
                if not owner.is_running() or owner.create_time() != created:
                    os._exit(0)
            except psutil.Error:
                os._exit(0)
    threading.Thread(target=watch, daemon=True).start()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--model-dir', type=Path, required=True)
    parser.add_argument('--cache-dir', type=Path, required=True)
    parser.add_argument('--device', choices=('auto', 'mps', 'cuda', 'cpu'), default='auto')
    parser.add_argument('--port', type=int, default=8770)
    parser.add_argument('--mode', choices=('fast',), default='fast')
    parser.add_argument('--unity-study', action='store_true')
    args = parser.parse_args()
    os.environ['HF_HUB_OFFLINE'] = '1'
    os.environ['TRANSFORMERS_OFFLINE'] = '1'
    os.environ['HF_HUB_DISABLE_TELEMETRY'] = '1'
    start_parent_watchdog()
    print(f'Loading QEV-0.8B {args.mode} (local files only)…', flush=True)
    backend = QEVBackend.load(args.model_dir, args.cache_dir, args.device, args.mode)
    server = ThreadingHTTPServer(('127.0.0.1', args.port), Handler)
    mode_label = '속도 모드' if args.mode == 'fast' else '정확도 우선'
    desktop = desktop_provider() if args.unity_study else None
    configure(server, backend, f'{backend.model_label} · {mode_label} · 1초 간격',
              unity=args.unity_study, desktop=desktop)
    print(f'Model ready: {backend.mode} / {backend.device}', flush=True)
    print(f'Webcam test: {server.origin} (camera starts only after your click)', flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        if server.screen:
            server.screen.clear()
        server.server_close()


if __name__ == '__main__':
    main()
