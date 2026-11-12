"""Manual webcam test UI: browser -> loopback -> local VLM. No frame storage."""
import argparse
import html
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import os
from pathlib import Path
import secrets
import threading

from qev_backend import QEVBackend

ROOT = Path(__file__).resolve().parent


def configure(server, backend, model_label):
    server.backend = backend
    server.origin = f'http://127.0.0.1:{server.server_port}'
    server.token = secrets.token_urlsafe(32)
    server.model_label = model_label
    server.busy = threading.Lock()


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
        if self.path == '/':
            page = (ROOT / 'index.html').read_text(encoding='utf-8')
            page = page.replace('__SESSION_TOKEN__', self.server.token)
            page = page.replace('__MODEL_LABEL__', html.escape(self.server.model_label))
            return self.send(200, page.encode(), 'text/html; charset=utf-8')
        if self.path in ('/app.js', '/style.css'):
            mime = 'text/javascript' if self.path.endswith('.js') else 'text/css'
            return self.send(200, (ROOT / self.path[1:]).read_bytes(), mime + '; charset=utf-8')
        if self.path == '/api/config':
            return self.send(200, {'interval_ms': 1000, 'mode': getattr(self.server.backend, 'mode', 'accurate'),
                                   'device': self.server.backend.device,
                                   'model': self.server.model_label})
        if self.path == '/favicon.ico':
            return self.send(204, b'')
        self.fail(404, '찾을 수 없습니다.')

    def do_POST(self):
        if (not self.allowed_host() or self.headers.get('Origin') != self.server.origin or
                not secrets.compare_digest(self.headers.get('X-Session-Token', ''), self.server.token)):
            return self.fail(403, '이 테스트 화면에서 시작해 주세요.')
        if self.path != '/api/infer':
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
            result = self.server.backend.classify(data)
        except Exception as error:
            # Exception types are sufficient for debugging; never log a frame.
            print(f'QEV inference failed: {type(error).__name__}', flush=True)
            return self.fail(502, '모델 분석 중 오류가 발생했습니다. 실행 터미널을 확인해 주세요.')
        finally:
            self.server.busy.release()
        self.send(200, result)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--model-dir', type=Path, required=True)
    parser.add_argument('--cache-dir', type=Path, required=True)
    parser.add_argument('--device', choices=('auto', 'mps', 'cuda', 'cpu'), default='auto')
    parser.add_argument('--port', type=int, default=8770)
    parser.add_argument('--variant', choices=('original', 'compact'), default='original')
    parser.add_argument('--mode', choices=('accurate', 'fast'), default='accurate')
    args = parser.parse_args()
    if args.variant == 'compact' and args.mode != 'accurate':
        parser.error('Compact supports accurate mode only')
    os.environ['HF_HUB_OFFLINE'] = '1'
    os.environ['TRANSFORMERS_OFFLINE'] = '1'
    os.environ['HF_HUB_DISABLE_TELEMETRY'] = '1'
    print(f'Loading QEV {args.variant} {args.mode} (local files only)…', flush=True)
    backend = QEVBackend.load(args.model_dir, args.cache_dir, args.device, args.variant, args.mode)
    server = ThreadingHTTPServer(('127.0.0.1', args.port), Handler)
    mode_label = '속도 모드' if args.mode == 'fast' else '정확도 우선'
    configure(server, backend, f'{backend.model_label} · {mode_label} · 1초 간격')
    print(f'Model ready: {backend.mode} / {backend.device}', flush=True)
    print(f'Webcam test: {server.origin} (camera starts only after your click)', flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == '__main__':
    main()
