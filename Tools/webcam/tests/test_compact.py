"""Guard against silently accepting a truncated or wrong model as accurate."""
from pathlib import Path
from types import SimpleNamespace as NS
import sys
import unittest
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from qev_backend import QEVBackend

class CompactContractTests(unittest.TestCase):
    def model(self, depth=16):
        return NS(serving_mode='accurate', early_exit_layer=None,
                  backbone={'bundled': True, 'depth': 16, 'vocabulary': 32768},
                  encoder=NS(model=NS(language_model=NS(layers=[None]*depth,
                      embed_tokens=NS(num_embeddings=32768))),
                      processor=NS(image_processor=NS(size={'longest_edge':1638400}))))

    def test_compact_accepts_its_complete_stack_but_rejects_wrong_depth(self):
        try:
            backend = QEVBackend(self.model(), 'cpu', variant='compact')
        except (ValueError, TypeError) as error:
            self.fail(f'Complete compact model must load: {error}')
        self.assertIn('0.5B', backend.model_label)
        with self.assertRaises(ValueError):
            QEVBackend(self.model(15), 'cpu', variant='compact')

    def test_original_mode_does_not_accept_compact_as_full_original(self):
        with self.assertRaises(ValueError):
            QEVBackend(self.model(), 'cpu')


# Real bundle and public images are opt-in; never opens a camera.
import os
@unittest.skipUnless(os.environ.get('QEV_COMPACT_TEST') == '1', 'opt-in compact real-model test')
class CompactRuntimeTests(unittest.TestCase):
    def test_in_memory_predictions_match_native_public_image_path(self):
        import json
        import hashlib
        import torch
        from qev import DecisionRequest
        from qev_backend import build_request
        backend = QEVBackend.load(os.environ['QEV_MODEL_DIR'], os.environ['QEV_CACHE_DIR'], 'mps', 'compact')
        root = Path(os.environ['QEV_SMOKE_FIXTURES'])
        rows = []
        for name, expected in [('reading','studying'), ('phone','phone'), ('other','other'), ('away','away'), ('drowsy','drowsy')]:
            data = (root / f'{name}.jpg').read_bytes()
            result = backend.classify(data)
            request = build_request()
            request['state']['images'][0]['path'] = f'{name}.jpg'
            native = backend.model.predict(DecisionRequest.model_validate(request), image_root=root)['answers']['activity']
            self.assertEqual(result['probabilities'], native['probabilities'])
            rows.append({'fixture':name, 'expected':expected, 'sha256':hashlib.sha256(data).hexdigest(), **result})
        torch.mps.synchronize()
        report = {'model':'ken-jo/QEV-0.5B', 'revision':'0d99aa34b354a81a4b963cb3059b75466174fd54',
                  'native_parity':True, 'samples':rows,
                  'driver_allocated_bytes':torch.mps.driver_allocated_memory(),
                  'limitations':'Five public sanity examples, not webcam accuracy. No camera accessed.'}
        if os.environ.get('QEV_COMPACT_REPORT'):
            Path(os.environ['QEV_COMPACT_REPORT']).write_text(json.dumps(report, indent=2)+'\n')
        print(json.dumps(report))

if __name__ == '__main__':
    unittest.main()
