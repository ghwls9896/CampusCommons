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

    def test_request_includes_drowsiness_as_the_fifth_activity_choice(self):
        request = self.backend().build_request()
        self.assertEqual(set(request['questions']), {'activity'})
        question = request['questions']['activity']
        self.assertEqual(question['type'], 'choice')
        self.assertEqual(set(question['criteria']), {'studying', 'phone', 'other', 'away', 'drowsy'})

    def test_scores_are_checked_before_an_activity_is_accepted(self):
        backend = self.backend()
        answer = {'choice': 'phone', 'probabilities': {
            'studying': .1, 'phone': .7, 'other': .1, 'away': .05, 'drowsy': .05}}
        self.assertEqual(backend.validate_answer(answer)['state'], 'phone')
        for change in ({'choice': 'unknown'}, {'choice': 'away'},
                       {'probabilities': {'phone': 1.}},
                       {'probabilities': {'studying': float('nan'), 'phone': .7, 'other': .1, 'away': .1, 'drowsy': .1}},
                       {'probabilities': {'studying': -.1, 'phone': .8, 'other': .1, 'away': .1, 'drowsy': .1}}):
            with self.subTest(change=change), self.assertRaises(ValueError):
                backend.validate_answer({**answer, **change})

    def test_drowsy_result_is_accepted_without_a_sixth_unknown_category(self):
        result = self.backend().validate_answer({'choice': 'other', 'abstained': True, 'probabilities': {
            'studying': .19, 'phone': .19, 'other': .24, 'away': .19, 'drowsy': .19}})
        self.assertEqual(result['state'], 'other')
        result = self.backend().validate_answer({'choice': 'drowsy', 'probabilities': {
            'studying': .1, 'phone': .05, 'other': .1, 'away': .05, 'drowsy': .7}})
        self.assertEqual(result['state'], 'drowsy')
        with self.assertRaises(ValueError):
            self.backend().validate_answer({'choice': 'studying', 'probabilities': {
                'studying': .7, 'phone': .1, 'other': .1, 'away': .1}})


if __name__ == '__main__':
    unittest.main()
