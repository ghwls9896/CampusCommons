"""Real loopback HTTP tests; no camera or external network is used."""
from contextlib import contextmanager
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
from pathlib import Path
import sys
import threading
import unittest
from urllib.error import HTTPError
from urllib.request import Request, urlopen

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import probe
import live


@contextmanager
def running(handler):
    server = ThreadingHTTPServer(('127.0.0.1', 0), handler)
    thread = threading.Thread(target=server.serve_forever, kwargs={'poll_interval': .01}, daemon=True)
    thread.start()
    try:
        yield server, f'http://127.0.0.1:{server.server_port}'
    finally:
        server.shutdown()
        server.server_close()
        thread.join()


class ModelReply(BaseHTTPRequestHandler):
    def do_POST(self):
        self.rfile.read(int(self.headers['Content-Length']))
        self.send_response(self.server.code)
        if self.server.code == 302:
            self.send_header('Location', '/must-not-follow')
        data = self.server.body
        self.send_header('Content-Length', str(len(data) + self.server.extra_length))
        self.end_headers()
        self.wfile.write(data)

    def log_message(self, *args):
        pass


class LiveTests(unittest.TestCase):
    def test_model_protocol_errors_remain_unknown(self):
        for code, body, extra in ((500, b'failed', 0), (302, b'', 0),
                                  (200, b'not json', 0), (200, b'{}', 20)):
            with self.subTest(code=code, body=body), running(ModelReply) as (server, endpoint):
                server.code, server.body, server.extra_length = code, body, extra
                result = probe.infer(endpoint, {}, timeout=2)
                self.assertFalse(result['ok'])
                self.assertEqual(result['state'], 'unknown')

    def test_truncated_generation_cannot_count_as_study(self):
        with running(ModelReply) as (server, endpoint):
            server.code, server.extra_length = 200, 0
            server.body = json.dumps({'choices': [{'finish_reason': 'length', 'message': {
                'content': '{"state":"reading_writing","reason":"paper"}'}}]}).encode()
            result = probe.infer(endpoint, {})
            self.assertTrue(result['ok'])
            self.assertFalse(result['valid'])
            self.assertEqual(result['state'], 'unknown')

    def test_web_page_requires_exact_host_and_does_not_start_camera(self):
        with running(live.Handler) as (server, endpoint):
            live.configure(server, InMemoryBackend(), 'Test model')
            with urlopen(endpoint) as response:
                page = response.read().decode()
                self.assertIn('카메라 시작', page)
                self.assertIn(server.token, page)
                self.assertIn("connect-src 'self'", response.headers['Content-Security-Policy'])
            with self.assertRaises(HTTPError) as error:
                urlopen(Request(endpoint, headers={'Host': 'other.example'}))
            self.assertEqual(error.exception.code, 403)
            error.exception.close()

    def test_cross_origin_and_missing_token_rejected_before_inference(self):
        with running(live.Handler) as (server, endpoint):
            backend = InMemoryBackend()
            live.configure(server, backend, 'Test model')
            for origin, token in (('https://example.com', server.token), (endpoint, '')):
                request = Request(endpoint + '/api/infer', data=b'\xff\xd8\xfftest', headers={
                    'Origin': origin, 'X-Session-Token': token, 'Content-Type': 'image/jpeg'})
                with self.assertRaises(HTTPError) as error:
                    urlopen(request)
                self.assertEqual(error.exception.code, 403)
                error.exception.close()
            self.assertEqual(backend.frames, [])

    def test_invalid_image_rejected_and_unavailable_model_shown_as_error(self):
        with running(live.Handler) as (server, endpoint):
            live.configure(server, UnavailableBackend(), 'Test model')
            for body, expected in ((b'bad', 400), (b'\xff\xd8\xfftest', 502)):
                request = Request(endpoint + '/api/infer', data=body, headers={
                    'Origin': endpoint, 'X-Session-Token': server.token, 'Content-Type': 'image/jpeg'})
                with self.assertRaises(HTTPError) as error:
                    urlopen(request)
                self.assertEqual(error.exception.code, expected)
                self.assertNotIn('state', json.loads(error.exception.read()))
                error.exception.close()

    def test_frame_is_passed_in_memory_and_config_has_one_second_accurate_mode(self):
        with running(live.Handler) as (server, endpoint):
            backend = InMemoryBackend()
            live.configure(server, backend, 'Test model')
            with urlopen(endpoint + '/api/config') as response:
                config = json.load(response)
            self.assertEqual(config['interval_ms'], 1000)
            self.assertEqual(config['mode'], 'accurate')
            data = b'\xff\xd8\xfftest'
            request = Request(endpoint + '/api/infer', data=data, headers={
                'Origin': endpoint, 'X-Session-Token': server.token, 'Content-Type': 'image/jpeg'})
            with urlopen(request) as response:
                result = json.load(response)
            self.assertEqual(result['state'], 'studying')
            self.assertEqual(backend.frames, [data])

    def test_busy_model_rejects_another_request_instead_of_queueing(self):
        with running(live.Handler) as (server, endpoint):
            backend = InMemoryBackend()
            live.configure(server, backend, 'Test model')
            server.busy.acquire()
            try:
                request = Request(endpoint + '/api/infer', data=b'\xff\xd8\xfftest', headers={
                    'Origin': endpoint, 'X-Session-Token': server.token, 'Content-Type': 'image/jpeg'})
                with self.assertRaises(HTTPError) as error:
                    urlopen(request)
                self.assertEqual(error.exception.code, 429)
                error.exception.close()
                self.assertEqual(backend.frames, [])
            finally:
                server.busy.release()


class InMemoryBackend:
    device = 'test'

    def __init__(self):
        self.frames = []

    def classify(self, data):
        self.frames.append(data)
        return {'ok': True, 'state': 'studying', 'probabilities': {
            'studying': .7, 'phone': .1, 'other': .1, 'away': .1},
            'confidence': .7, 'seconds': .01, 'mode': 'accurate', 'device': self.device}


class UnavailableBackend:
    device = 'test'

    def classify(self, data):
        raise RuntimeError('Unavailable model')


if __name__ == '__main__':
    unittest.main()
