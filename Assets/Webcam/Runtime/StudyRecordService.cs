using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CampusCommons.Account;
using UnityEngine;

namespace CampusCommons.Webcam
{
    [Serializable]
    public class StudySyncRequest
    {
        public string action = "sync", session_id;
        public long started_ms, ended_ms;
        public StudySegment[] segments;
    }

    [Serializable]
    public class StudySummaryRequest { public string action = "summary"; }

    [Serializable]
    public class StudyHistory
    {
        public string session_id;
        public long started_ms, ended_ms, study_ms, phone_ms, other_ms, away_ms, drowsy_ms;
    }

    [Serializable]
    public class StudyReply
    {
        public string user_id, session_id;
        public long day_ms, today_ms;
        public int ack_sequence;
        public StudyHistory[] sessions;
    }

    public sealed class StudyRecordService : MonoBehaviour
    {
        public static StudyRecordService Instance { get; private set; }
        public StudyTracker Tracker { get; private set; }
        public string SyncStatus { get; private set; } = "기록 준비 중";
        public bool Available => store != null && !storageFailed;
        public long SessionMilliseconds { get; private set; }
        public long TodayMilliseconds { get; private set; }

        StudyHistory[] history = Array.Empty<StudyHistory>();
        StudyStore store;
        StudyRecord active;
        string owner;
        double timeAnchor, nextSave, nextSync;
        long utcAnchor, summaryDay = -1, displayedDay = -1;
        bool syncing, storageFailed, summaryRequested, retryRejected;
        bool showHistory;
        Vector2 scroll;
        GUIStyle text, button;
        Font font;

        static long UtcNow => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (!Instance) new GameObject("Study records").AddComponent<StudyRecordService>();
        }

        static string StoragePath()
        {
#if UNITY_EDITOR
            if (Application.isBatchMode)
                return Path.Combine(Path.GetTempPath(), "CampusCommons-study-test", Guid.NewGuid().ToString(), "study-records.xml");
#endif
            return Path.Combine(Application.persistentDataPath, "study-records.xml");
        }

        void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            try
            {
                store = StudyStore.Load(StoragePath());
                // On restart, recover only the time observed before the last durable save.
                foreach (var record in store.Data.sessions)
                    if (record.ended_ms == 0) record.ended_ms = LastObservedEnd(record);
                SyncStatus = "로그인 후 기록을 불러옵니다.";
                Persist();
            }
            catch (Exception)
            {
                storageFailed = true;
                SyncStatus = "기록 파일을 열 수 없습니다. 다른 게임 창과 저장 공간을 확인해 주세요.";
            }
        }

        static long LastObservedEnd(StudyRecord record) =>
            record.segments.Count == 0 ? record.started_ms : record.segments[record.segments.Count - 1].end_ms;

        public bool Begin()
        {
            var gate = AccountGate.Instance;
            if (!Available || !gate || !gate.Ready || !gate.Session.IsAuthenticated) return false;

            Finish();
            utcAnchor = UtcNow;
            timeAnchor = Time.realtimeSinceStartupAsDouble;
            active = store.Begin(gate.Session.UserId, utcAnchor);
            Tracker = new StudyTracker();
            SessionMilliseconds = 0;
            nextSave = timeAnchor + 5;
            Persist();
            return Available;
        }

        public void Observe(double capturedAt, double[] scores)
        {
            if (active == null) return;
            var gate = AccountGate.Instance;
            if (!gate || !gate.Ready || gate.Session.UserId != active.user_id)
            {
                Finish();
                return;
            }

            Tracker.Observe(capturedAt, scores);
            Tracker.DrainIntervals(interval => store.Append(active,
                utcAnchor + (long)Math.Round((interval.Start - timeAnchor) * 1000),
                utcAnchor + (long)Math.Round((interval.End - timeAnchor) * 1000),
                interval.State));
            SessionMilliseconds = (long)Math.Round(Tracker.StudySeconds * 1000);
            RefreshToday();
        }

        public void Finish()
        {
            if (active == null) return;
            Tracker.Break();
            active.ended_ms = LastObservedEnd(active);
            active = null;
            Persist();
            nextSync = 0;
        }

        void Persist()
        {
            if (!Available) return;
            try { store.Save(); }
            catch (Exception)
            {
                storageFailed = true;
                SyncStatus = "기록 저장에 실패했습니다. 저장 공간을 확인해 주세요.";
            }
        }

        void RefreshToday()
        {
            long now = UtcNow;
            displayedDay = StudyTime.DayStart(now);
            TodayMilliseconds = owner != null && store != null ? store.Today(owner, now) : 0;
        }

        void Update()
        {
            var gate = AccountGate.Instance;
            string current = gate && gate.Ready && gate.Session.IsAuthenticated ? gate.Session.UserId : null;
            if (current != owner)
            {
                Finish();
                owner = current;
                summaryDay = -1;
                history = Array.Empty<StudyHistory>();
                SessionMilliseconds = 0;
                showHistory = retryRejected = false;
                RequestSummary();
                RefreshToday();
            }

            if (storageFailed)
            {
                var webcam = FindAnyObjectByType<LobbyStudyWebcam>();
                if (webcam && webcam.IsRunning) webcam.StopStudy();
                return;
            }

            double now = Time.realtimeSinceStartupAsDouble;
            if (active != null && now >= nextSave)
            {
                Persist();
                nextSave = now + 5;
            }
            if (owner == null || !Available) return;
            if (displayedDay != StudyTime.DayStart(UtcNow)) RefreshToday();
            if (syncing || now < nextSync) return;

            nextSync = now + 30;
            if (summaryRequested || summaryDay != displayedDay || store.Data.sessions.Any(record =>
                    record.user_id == owner && record.HasPending && string.IsNullOrEmpty(record.sync_error)))
                _ = Sync(gate.Session, owner);
        }

        void RequestSummary()
        {
            summaryRequested = true;
            nextSync = 0;
        }

        public void RetrySync()
        {
            retryRejected = true;
            RequestSummary();
        }

        bool IsCurrent(AccountSession account, string user)
        {
            var gate = AccountGate.Instance;
            return this && gate && gate.Ready && gate.Session == account &&
                account.IsAuthenticated && account.UserId == user && owner == user;
        }

        async Task Sync(AccountSession account, string user)
        {
            syncing = true;
            bool requested = summaryRequested || summaryDay != displayedDay;
            bool tryRejected = retryRejected;
            summaryRequested = retryRejected = false;
            try
            {
                SyncStatus = "기록 동기화 중";
                bool receivedSummary = false;
                // Upload pending intervals before reading totals, including retries after a lost reply.
                foreach (var record in store.Data.sessions.Where(record => record.user_id == user &&
                    record.HasPending && (tryRejected || string.IsNullOrEmpty(record.sync_error))).ToArray())
                {
                    var batch = store.Batch(record);
                    Persist();
                    if (!Available) return;
                    int expected = batch.Length == 0 ? record.acked : batch[batch.Length - 1].sequence;
                    bool finalBatch = expected == record.segments.Count - 1;
                    long end = finalBatch ? record.ended_ms : 0;
                    StudyReply reply;
                    try
                    {
                        reply = await account.StudyCallAsync<StudyReply>(user, new StudySyncRequest
                        {
                            session_id = record.session_id, started_ms = record.started_ms,
                            ended_ms = end, segments = batch
                        });
                    }
                    catch (AccountException error) when (error.Code == "record_conflict" ||
                        error.Code == "invalid_record" || error.Code == "forbidden")
                    {
                        if (!IsCurrent(account, user)) return;
                        record.sync_error = error.Code;
                        Persist();
                        RefreshToday();
                        if (!Available) return;
                        continue;
                    }

                    if (!IsCurrent(account, user)) return;
                    if (reply == null || reply.user_id != user || reply.session_id != record.session_id ||
                        reply.ack_sequence != expected)
                        throw new InvalidDataException("Invalid study acknowledgement");

                    store.Acknowledge(record, expected, reply.day_ms, reply.today_ms);
                    record.sync_error = null;
                    record.end_acked = end > 0;
                    if (record.end_acked) store.Data.sessions.Remove(record);
                    ApplySummary(reply);
                    receivedSummary = true;
                    if (!Available) return;
                    if (!finalBatch)
                    {
                        nextSync = 0;
                        SyncStatus = "남은 기록 동기화 중";
                        return;
                    }
                }

                if (requested && !receivedSummary)
                {
                    var reply = await account.StudyCallAsync<StudyReply>(user, new StudySummaryRequest());
                    if (!IsCurrent(account, user)) return;
                    if (reply == null || reply.user_id != user) throw new InvalidDataException("Invalid study summary");
                    store.SetTotal(user, reply.day_ms, reply.today_ms);
                    ApplySummary(reply);
                    if (!Available) return;
                }

                if (store.Data.sessions.Any(record => record.user_id == user && !string.IsNullOrEmpty(record.sync_error)))
                    SyncStatus = "일부 기록을 동기화할 수 없어 이 기기에 보관했습니다.";
                else
                    SyncStatus = "기록 저장됨";
            }
            catch (Exception)
            {
                if (IsCurrent(account, user))
                {
                    summaryRequested |= requested;
                    SyncStatus = "기기에 보관 중 · 연결되면 재전송";
                }
            }
            finally { syncing = false; }
        }

        void ApplySummary(StudyReply reply)
        {
            summaryDay = reply.day_ms;
            history = reply.sessions ?? Array.Empty<StudyHistory>();
            Persist();
            RefreshToday();
        }

        void ToggleHistory()
        {
            showHistory = !showHistory;
            if (showHistory) RequestSummary();
        }

        void LateUpdate()
        {
            if (owner == null) return;
            if (Input.GetKeyDown(KeyCode.F9)) ToggleHistory();
            if (showHistory && Application.isFocused)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        void OnGUI()
        {
            if (owner == null) return;
            if (text == null)
            {
                font = Font.CreateDynamicFontFromOSFont(new[] { "Apple SD Gothic Neo", "Malgun Gothic", "Arial" }, 16);
                text = new GUIStyle(GUI.skin.label) { font = font, fontSize = 16, wordWrap = true };
                button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 16 };
            }

            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            var header = new Rect(Mathf.Max(8, Screen.width - 320), 12, 304, 64);
            GUI.Box(header, GUIContent.none);
            GUI.Label(new Rect(header.x + 10, 16, 284, 26), "오늘 공부  " + StudyTime.Clock(TodayMilliseconds), text);
            if (GUI.Button(new Rect(header.x + 10, 43, 284, 26), "공부 기록 (F9)", button)) ToggleHistory();
            if (showHistory) DrawHistory();
            GUI.matrix = old;
        }

        void DrawHistory()
        {
            float width = Mathf.Min(600, Screen.width - 24);
            float height = Mathf.Min(500, Screen.height - 100);
            var rect = new Rect((Screen.width - width) / 2, 80, width, height);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 14, rect.y + 12, width - 28, height - 24));
            GUILayout.Label("내 공부 기록 · 한국시간 기준", text);
            GUILayout.Label(SyncStatus, text);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("동기화", button)) RetrySync();
            if (GUILayout.Button("닫기 (F9)", button)) showHistory = false;
            GUILayout.EndHorizontal();
            scroll = GUILayout.BeginScrollView(scroll);
            if (history.Length == 0) GUILayout.Label("동기화된 기록이 없습니다.", text);
            foreach (var row in history)
            {
                var date = DateTimeOffset.FromUnixTimeMilliseconds(row.started_ms).ToOffset(TimeSpan.FromHours(9));
                GUILayout.Label(date.ToString("MM/dd HH:mm") + "  공부 " + StudyTime.Clock(row.study_ms), text);
                GUILayout.Label("휴대폰 " + StudyTime.Clock(row.phone_ms) + " · 자리비움 " + StudyTime.Clock(row.away_ms) +
                    "\n자는중 " + StudyTime.Clock(row.drowsy_ms) + " · 다른활동 " + StudyTime.Clock(row.other_ms), text);
                GUILayout.Space(12);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void OnApplicationQuit() { Finish(); Persist(); }

        void OnDestroy()
        {
            Finish();
            store?.Dispose();
            if (font) Destroy(font);
            if (Instance == this) Instance = null;
        }
    }
}
