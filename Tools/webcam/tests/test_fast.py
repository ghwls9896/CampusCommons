"""Fast mode must use its trained exit head and declare the actual serving mode."""
import os
import sys
import unittest
from pathlib import Path
from types import SimpleNamespace as NS
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
from qev_backend import QEVBackend

class FastContractTests(unittest.TestCase):
    def test_fast_requires_a_real_exit_head(self):
        model=NS(serving_mode='fast',early_exit_layer=16,exit_head=object(),
                 encoder=NS(model=NS(language_model=NS(layers=[None]*16)),
                 processor=NS(image_processor=NS(size={'longest_edge':262144}))))
        try:
            backend=QEVBackend(model,'cpu',mode='fast')
        except (TypeError,ValueError) as error:
            self.fail(f'Native fast mode must load: {error}')
        self.assertEqual(backend.mode,'fast')
        model.exit_head=None
        with self.assertRaises(ValueError):
            QEVBackend(model,'cpu',mode='fast')

@unittest.skipUnless(os.environ.get('QEV_FAST_TEST')=='1','opt-in native fast parity')
class FastRuntimeTests(unittest.TestCase):
    def test_memory_input_matches_native_fast_head(self):
        import json
        from qev import DecisionRequest
        from qev_backend import build_request
        backend=QEVBackend.load(os.environ['QEV_MODEL_DIR'],os.environ['QEV_CACHE_DIR'],'mps',mode='fast')
        root=Path(os.environ['QEV_SMOKE_FIXTURES'])
        rows=[]
        for name, expected in [('reading','studying'),('phone','phone'),('other','other'),('away','away'),('drowsy','drowsy')]:
            result=backend.classify((root/f'{name}.jpg').read_bytes())
            request=build_request();request['state']['images'][0]['path']=f'{name}.jpg'
            native=backend.model.predict(DecisionRequest.model_validate(request),image_root=root)['answers']['activity']
            self.assertEqual(result['mode'],'fast')
            self.assertEqual(result['probabilities'],native['probabilities'])
            rows.append({'fixture':name,'expected':expected,**result})
        if os.environ.get('QEV_FAST_REPORT'):
            Path(os.environ['QEV_FAST_REPORT']).write_text(json.dumps({'native_parity':True,'samples':rows},indent=2)+'\n')
