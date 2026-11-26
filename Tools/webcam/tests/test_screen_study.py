from io import BytesIO
from pathlib import Path
import sys
import threading
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from screen_study import Window, ScreenStudy, combine, screen_hint


def scores(paper=0, screen=0, phone=0, other=0, away=0, drowsy=0):
    return dict(paper=paper, screen=screen, phone=phone, other=other, away=away, drowsy=drowsy)


class FakeDesktop:
    def __init__(self, window):
        self.window = window
        self.captured = []

    def windows(self):
        return [self.window]

    def active(self):
        return self.window

    def capture(self, window):
        self.captured.append(window)
        return b'fixture screenshot', ''


class ScreenStudyTests(unittest.TestCase):
    def test_flat_camera_frame_has_no_study_evidence(self):
        try:
            from PIL import Image
        except ImportError:
            self.skipTest('Pillow is in the QEV runtime')
        from qev_backend import QEVBackend

        backend = QEVBackend.__new__(QEVBackend)
        backend.mode, backend.device = 'fast', 'fixture'
        with Image.new('RGB', (256, 144), 'white') as image, BytesIO() as input_bytes:
            image.save(input_bytes, format='JPEG')
            result = backend.classify_pose(input_bytes.getvalue())
        self.assertFalse(result['observed'])
        self.assertNotIn('probabilities', result)

    def test_screen_requires_study_context_and_uncertain_stops_observation(self):
        pose = scores(paper=.08, screen=.82, other=.10)
        self.assertEqual(combine(pose, 'study')['state'], 'studying')
        self.assertEqual(combine(pose, 'other')['state'], 'other')
        self.assertFalse(combine(pose, 'unknown')['observed'])

    def test_paper_and_nonstudy_are_not_overridden_by_the_screen(self):
        self.assertEqual(combine(scores(paper=.88, screen=.12), 'other')['state'], 'studying')
        for state in ('phone', 'away', 'drowsy', 'other'):
            pose = scores(paper=.29, screen=.30, **{state: .41})
            self.assertEqual(combine(pose, 'study')['state'], state)

    def test_youtube_is_not_a_study_allowlist(self):
        self.assertEqual(screen_hint(Window('1', 3, 'com.google.Chrome', 'Chrome', 'YouTube')), 'unknown')
        self.assertEqual(screen_hint(Window('1', 3, 'com.google.Chrome', 'Chrome', '교재.pdf - Chrome')), 'study')
        self.assertEqual(screen_hint(Window('1', 3, 'com.discord.Discord', 'Discord', '과제.pdf')), 'other')

    def test_same_window_tab_change_discards_old_decision(self):
        pdf = Window('1', 3, 'com.google.Chrome', 'Chrome', '교재.pdf - Chrome')
        desktop = FakeDesktop(pdf)
        with ScreenStudy(desktop, lambda _: 'other') as screen:
            self.assertFalse(screen.select('9', 3))
            self.assertEqual(screen.observe(), 'unknown')
            self.assertTrue(screen.select('1', 3))
            self.assertEqual(screen.observe(), 'study')
            desktop.window = Window('1', 3, 'com.google.Chrome', 'Chrome', '새 탭 - YouTube')
            self.assertEqual(screen.observe(), 'unknown')
            desktop.window = Window('2', 3, 'com.google.Chrome', 'Chrome', '다른 창.pdf')
            self.assertEqual(screen.observe(), 'unknown')
            screen.clear()
            self.assertEqual(screen.observe(), 'unknown')
        self.assertTrue(all(window.id == '1' for window in desktop.captured))

    def test_late_capture_cannot_restore_study_after_tab_switch(self):
        desktop = FakeDesktop(Window('1', 3, 'chrome.exe', 'Chrome', 'Video A'))
        started = threading.Event()
        finish = threading.Event()

        def classify(_):
            started.set()
            finish.wait(2)
            return 'study'

        with ScreenStudy(desktop, classify) as screen:
            self.assertTrue(screen.select('1', 3))
            self.assertEqual(screen.observe(), 'unknown')
            self.assertTrue(started.wait(2))
            desktop.window = Window('1', 3, 'chrome.exe', 'Chrome', 'Video B')
            self.assertEqual(screen.observe(), 'unknown')
            finish.set()
            screen.pending.result(2)
            self.assertEqual(screen.observe(), 'unknown')


if __name__ == '__main__':
    unittest.main()
