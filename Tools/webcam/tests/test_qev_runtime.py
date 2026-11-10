"""Opt-in real-model smoke test with public fixtures, never the user's camera.

QEV_SMOKE_FIXTURES must contain reading.jpg, phone.jpg, other.jpg and away.jpg.
Start live.py first. These four examples are regression checks, not an accuracy set.
"""
import json
import os
from pathlib import Path
import re
import sys
import unittest
from urllib.request import Request, urlopen

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from probe import local_endpoint


@unittest.skipUnless(os.environ.get('QEV_SMOKE_FIXTURES'), 'opt-in public-image/model test')
class RealQEVTests(unittest.TestCase):
    def test_four_public_activity_examples(self):
        endpoint = local_endpoint(os.environ.get('QEV_SMOKE_URL', 'http://127.0.0.1:8770'))
        with urlopen(endpoint, timeout=5) as response:
            page = response.read().decode()
        token = re.search('name="session-token" content="([^"]+)"', page).group(1)
        root = Path(os.environ['QEV_SMOKE_FIXTURES'])
        for name, expected in [('reading', 'studying'), ('phone', 'phone'),
                               ('other', 'other'), ('away', 'away')]:
            with self.subTest(image=name):
                request = Request(endpoint + '/api/infer', data=(root / f'{name}.jpg').read_bytes(),
                    headers={'Origin': endpoint, 'X-Session-Token': token, 'Content-Type': 'image/jpeg'})
                with urlopen(request, timeout=60) as response:
                    result = json.load(response)
                self.assertEqual(result['mode'], 'accurate')
                self.assertEqual(result['state'], expected)


if __name__ == '__main__':
    unittest.main()
