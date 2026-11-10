"""Opt-in pinned Mac/MPS regression: same public decisions within a GPU budget.

QEV_MEMORY_TEST=1; QEV_MODEL_DIR, QEV_CACHE_DIR and QEV_SMOKE_FIXTURES required.
Uses only public fixtures whose SHA256 matches the committed M4 baseline.
"""
import hashlib
import json
import os
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from qev_backend import QEVBackend


@unittest.skipUnless(os.environ.get('QEV_MEMORY_TEST') == '1', 'opt-in pinned Mac/MPS memory test')
class MPSMemoryTests(unittest.TestCase):
    def test_native_scores_preserved_with_driver_memory_below_three_gib(self):
        import torch
        self.assertTrue(torch.backends.mps.is_available())
        model = QEVBackend.load(os.environ['QEV_MODEL_DIR'], os.environ['QEV_CACHE_DIR'], 'mps')
        root = Path(os.environ['QEV_SMOKE_FIXTURES'])
        baseline_path = Path(__file__).resolve().parents[3] / 'docs/webcam/results/qev-m4-five-class-resources.json'
        baseline = json.loads(baseline_path.read_text())['samples']
        for row in baseline:
            data = (root / (row['fixture'] + '.jpg')).read_bytes()
            self.assertEqual(hashlib.sha256(data).hexdigest(), row['sha256'])
            result = model.classify(data)
            self.assertEqual(result['state'], row['expected'])
            self.assertEqual(result['probabilities'], row['probabilities'])
        torch.mps.synchronize()
        # Includes Metal/MPSGraph allocations and allocator caches, not only tensors.
        self.assertLess(torch.mps.driver_allocated_memory(), 3 * 1024 ** 3)


if __name__ == '__main__':
    unittest.main()
