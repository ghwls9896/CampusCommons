"""Combine camera and permitted foreground-window evidence in memory."""
from concurrent.futures import ThreadPoolExecutor
from dataclasses import dataclass
import re
from threading import Lock
import time

REFRESH_SECONDS = 3
MAX_AGE_SECONDS = 4.5
CURRENT_WINDOW = object()


@dataclass(frozen=True)
class Window:
    id: str
    pid: int
    app: str
    name: str
    title: str

    @property
    def identity(self):
        return self.id, self.pid, self.app

    @property
    def context(self):
        return self.identity, self.title


def combine(scores, screen):
    paper, computer = scores['paper'], scores['screen']
    state = max(scores, key=scores.get)
    engaged = state in ('paper', 'screen')
    if state == 'screen' and paper <= .5 and screen == 'unknown':
        return {'observed': False, 'screen': screen}
    study = paper
    if engaged and screen == 'study':
        study += computer
    result = {'studying': study, **{name: scores[name] for name in ('phone', 'other', 'away', 'drowsy')}}
    if engaged or state == 'other':
        result['other'] += paper + computer - study
    else:
        # Preserve visible phone use, absence and dozing when one leads camera evidence.
        result[state] += computer
    return {'observed': True, 'screen': screen, 'state': max(result, key=result.get), 'probabilities': result}


BLOCKED_APPS = frozenset({
    'discord.exe', 'com.discord.discord', 'kakaotalk.exe', 'com.kakao.kakaotalkmac',
    'telegram.exe', 'ru.keepcoder.telegram', 'com.apple.messages', 'slack.exe', 'com.tinyspeck.slackmacgap',
    'steam.exe', 'com.valvesoftware.steam', 'minecraft.exe', 'robloxplayerbeta.exe',
    'leagueclient.exe', 'valorant-win64-shipping.exe', 'overwatch.exe',
    'campuscommons.exe', 'com.campus-studio.campus-commons', 'unity.exe', 'com.unity3d.unityeditor5.x',
})
SENSITIVE_TITLE = re.compile(r'\b(password|sign[ -]?in|log[ -]?in|checkout)\b|비밀번호|로그인|결제', re.I)
PDF_TITLE = re.compile(r'\.pdf(?:\b|$)', re.I)
STUDY_TITLE = re.compile(r'\b(lecture|course|syllabus|homework|worksheet)\b|강의|강의자료|학습자료|과제', re.I)
MEDIA_TITLE = re.compile(r'youtube|유튜브|netflix|twitch|치지직|쇼츠|shorts', re.I)
ENTERTAINMENT_TEXT = re.compile(r'\b(gameplay|walkthrough|prank|reaction|gaming|game over|arcade game)\b|게임플레이|먹방|예능|쇼츠|공략', re.I)
CHAT_TITLE = re.compile(r'\b(discord|messenger|telegram|whatsapp)\b|카카오톡|채팅방', re.I)
LESSON_TEXT = re.compile(r'\b(lecture|lesson|syllabus|chapter|exercise|course|homework)\b|강의|수업|학습|교재|문제풀이|예제', re.I)


def screen_hint(window):
    if window.app.casefold() in BLOCKED_APPS or CHAT_TITLE.search(window.title):
        return 'other'
    if SENSITIVE_TITLE.search(window.title):
        return 'unknown'
    if MEDIA_TITLE.search(window.title):
        return 'other' if ENTERTAINMENT_TEXT.search(window.title) else 'unknown'
    if PDF_TITLE.search(window.title) or STUDY_TITLE.search(window.title):
        return 'study'
    return 'unknown'


class ScreenStudy:
    def __init__(self, desktop, classify_image):
        self.desktop = desktop
        self.classify_image = classify_image
        self.lock = Lock()
        self.worker = ThreadPoolExecutor(max_workers=1, thread_name_prefix='study-window')
        self.selected = None
        self.current = None
        self.revision = 0
        self.sample = None
        self.pending = None
        self.next_capture = 0

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.clear()
        self.worker.shutdown()

    def windows(self):
        return [window for window in self.desktop.windows()
                if window.app.casefold() not in BLOCKED_APPS and not SENSITIVE_TITLE.search(window.title)][:30]

    def select(self, window_id, pid):
        chosen = next((window for window in self.windows() if (window.id, window.pid) == (window_id, pid)), None)
        if chosen is None:
            return False
        with self.lock:
            self.selected = chosen.identity
            self.reset_context(None)
        return True

    def clear(self):
        with self.lock:
            self.selected = None
            self.reset_context(None)

    def reset_context(self, context):
        self.revision += 1
        self.current = context
        self.sample = None
        self.next_capture = 0

    def active_at_capture(self):
        with self.lock:
            if self.selected is None:
                return None
            window = self.desktop.active()
            key = window.context if window else None
            if key != self.current:
                self.reset_context(key)
            return window

    def observe(self, window=CURRENT_WINDOW):
        if window is CURRENT_WINDOW:
            window = self.active_at_capture()
        with self.lock:
            if window is None:
                return 'unknown'
            if self.selected is None or self.current != window.context:
                return 'unknown'
            hint = screen_hint(window)
            if hint == 'other':
                return hint
            if window.identity != self.selected or SENSITIVE_TITLE.search(window.title):
                return 'unknown'
            if hint == 'study':
                return hint

            now = time.monotonic()
            if self.pending and self.pending.done():
                revision, sampled, result = self.pending.result()
                self.pending = None
                if revision == self.revision:
                    self.sample = sampled, result
            if self.pending is None and now >= self.next_capture:
                self.next_capture = now + REFRESH_SECONDS
                self.pending = self.worker.submit(self.capture, window, self.revision, now)
            if self.sample and now - self.sample[0] <= MAX_AGE_SECONDS:
                return self.sample[1]
            return 'unknown'

    def capture(self, window, revision, sampled):
        try:
            # Avoid a queued capture after selection was changed or cleared.
            with self.lock:
                if revision != self.revision or self.selected != window.identity:
                    return revision, sampled, 'unknown'
            if self.desktop.active() != window:
                return revision, sampled, 'unknown'
            image, words = self.desktop.capture(window)
            if self.desktop.active() != window or SENSITIVE_TITLE.search(words):
                return revision, sampled, 'unknown'
            if ENTERTAINMENT_TEXT.search(words):
                return revision, sampled, 'other'
            # A YouTube tab needs specific evidence from the visible page.
            if len(LESSON_TEXT.findall(words)) >= 2:
                return revision, sampled, 'study'
            return revision, sampled, self.classify_image(image) if image else 'unknown'
        except Exception:
            # Errors from OS capture, OCR, or model execution provide no study evidence.
            return revision, sampled, 'unknown'
