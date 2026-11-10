import importlib.util
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))


class QEVContractTests(unittest.TestCase):
    def backend(self):
        self.assertIsNotNone(importlib.util.find_spec('qev_backend'), 'QEV backend is missing')
        import qev_backend
        return qev_backend

    def test_request_uses_exactly_four_activity_choices(self):
        request = self.backend().build_request()
        self.assertEqual(set(request['questions']), {'activity'})
        question = request['questions']['activity']
        self.assertEqual(question['type'], 'choice')
        self.assertEqual(set(question['criteria']), {'studying', 'phone', 'other', 'away'})

    def test_scores_are_checked_before_an_activity_is_accepted(self):
        backend = self.backend()
        answer = {'choice': 'phone', 'probabilities': {
            'studying': .1, 'phone': .7, 'other': .15, 'away': .05}}
        self.assertEqual(backend.validate_answer(answer)['state'], 'phone')
        for change in ({'choice': 'unknown'}, {'choice': 'away'},
                       {'probabilities': {'phone': 1.}},
                       {'probabilities': {'studying': float('nan'), 'phone': .7, 'other': .2, 'away': .1}},
                       {'probabilities': {'studying': -.1, 'phone': .9, 'other': .1, 'away': .1}}):
            with self.subTest(change=change), self.assertRaises(ValueError):
                backend.validate_answer({**answer, **change})

    def test_low_confidence_keeps_four_categories_as_requested(self):
        result = self.backend().validate_answer({'choice': 'other', 'abstained': True, 'probabilities': {
            'studying': .24, 'phone': .24, 'other': .28, 'away': .24}})
        self.assertEqual(result['state'], 'other')


if __name__ == '__main__':
    unittest.main()
