import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

MODULE = Path(__file__).resolve().parents[1] / 'probe.py'
spec = importlib.util.spec_from_file_location('webcam_probe', MODULE)
probe = importlib.util.module_from_spec(spec)
spec.loader.exec_module(probe)


class ProbeTests(unittest.TestCase):
    def test_only_observable_activity_labels_are_accepted(self):
        for state in ('reading_writing', 'phone', 'away', 'unknown'):
            result = probe.validate_result(json.dumps({'state': state, 'reason': 'Visible evidence.'}))
            self.assertTrue(result['valid'])
            self.assertEqual(result['state'], state)

    def test_invalid_response_never_earns_study_or_away(self):
        for value in ('', 'reading_writing', '{"state":"reading_writing"}',
                      'null', '[]', '{"state":"studying","reason":"sure"}',
                      '{"state":"phone","reason":" "}',
                      '{"state":"phone","reason":5}',
                      '{"state":"away","reason":"empty","extra":true}',
                      '```json\n{"state":"away","reason":"empty"}\n```'):
            with self.subTest(value=value):
                self.assertEqual(probe.validate_result(value)['state'], 'unknown')
                self.assertFalse(probe.validate_result(value)['valid'])

    def test_local_endpoint_rejects_remote_and_ambiguous_destinations(self):
        for value in ('https://example.com', 'http://0.0.0.0:8080',
                      'http://127.0.0.1.evil.example:8080', 'http://localhost:8080',
                      'http://user:pass@127.0.0.1:8080', 'http://127.0.0.1:8080?x=1',
                      'http://127.0.0.1:8080/other', 'http://127.0.0.1:99999'):
            with self.subTest(value=value), self.assertRaises(ValueError):
                probe.local_endpoint(value)
        self.assertEqual(probe.local_endpoint('http://127.0.0.1:8080/'), 'http://127.0.0.1:8080')
        self.assertEqual(probe.local_endpoint('http://[::1]:8080'), 'http://[::1]:8080')

    def test_http_failures_are_counted_not_mixed_with_latency(self):
        summary = probe.summarize([
            {'ok': True, 'seconds': 1., 'valid': True},
            {'ok': False, 'seconds': 60., 'valid': False},
            {'ok': True, 'seconds': 3., 'valid': False},
        ])
        self.assertEqual(summary['requests'], 3)
        self.assertEqual(summary['request_failures'], 1)
        self.assertEqual(summary['invalid_outputs'], 1)
        self.assertEqual(summary['median_seconds'], 2.)
        self.assertEqual(summary['p95_seconds'], 3.)
        self.assertIsNone(probe.summarize([])['median_seconds'])

    def test_image_bytes_are_embedded_and_no_remote_image_is_requested(self):
        with tempfile.TemporaryDirectory() as directory:
            p = Path(directory) / 'sample.jpg'
            p.write_bytes(b'\xff\xd8\xffexample')
            payload = probe.make_request(p)
            image = payload['messages'][0]['content'][0]['image_url']['url']
            self.assertTrue(image.startswith('data:image/jpeg;base64,'))
            self.assertEqual(payload['temperature'], 0)
            self.assertFalse(payload['stream'])
            p.write_bytes(b'not an image')
            with self.assertRaises(ValueError):
                probe.make_request(p)

    def test_oversized_image_rejected_before_read(self):
        with tempfile.TemporaryDirectory() as directory:
            p = Path(directory) / 'huge.png'
            with p.open('wb') as file:
                file.truncate(6 * 1024 * 1024)
            with self.assertRaises(ValueError):
                probe.make_request(p)


if __name__ == '__main__':
    unittest.main()
