using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CampusCommons.Account;
using CampusCommons.GrandLibrary;
using UnityEngine;

namespace CampusCommons.Quiz
{
    [RequireComponent(typeof(Player))]
    public sealed class QuizRoom : MonoBehaviour
    {
        Player player;
        Seat[] chairs;
        Seat occupied;
        CancellationTokenSource visit;
        QuizDuelState state;
        string ticket, accountId;
        bool acting;
        int actionVersion;
        double receivedAt;
        string notice = "", connectionNotice = "";
        int selected = -1, savedChoice = -1;
        string savedText = "";
        bool saving;
        double nextSave;
        GUIStyle chairLabel;
        string draft = "";
        QuizTheme theme;
        Vector2 scroll, fileScroll;
        bool browsing, useRange;
        string startPage = "1", endPage = "1", directory;
        string[] folders = Array.Empty<string>(), files = Array.Empty<string>();
        int browserPage;
        readonly QuizDocument document = new QuizDocument();
        byte[] documentBytes;

        bool InRoom => player && player.currentSpace == Portal.Space.Quiz;
        public bool BlocksMovement => enabled && InRoom && occupied && occupied.occupant == player;
        public QuizDuelState Match => state;

        void Awake()
        {
            player = GetComponent<Player>();
            directory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrEmpty(directory)) directory = Application.persistentDataPath;
        }

        void LateUpdate()
        {
            if (!InRoom)
            {
                if (chairs != null) { EndVisit(); chairs = null; notice = ""; }
                return;
            }
            if (chairs == null)
            {
                chairs = Array.FindAll(FindObjectsByType<Seat>(FindObjectsSortMode.None),
                    seat => seat.gameObject.scene == player.gameObject.scene &&
                        (seat.name == "Quiz Seat 01" || seat.name == "Quiz Seat 02" || seat.name == "Quiz player seat"));
                Array.Sort(chairs, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
            }
            var gate = AccountGate.Instance;
            if (visit != null && (!occupied || occupied.occupant != player ||
                !gate || !gate.Ready || gate.Session.UserId != accountId))
                EndVisit();
            if (visit != null)
            {
                if (!saving && !acting && HasUnsavedAnswer && Time.realtimeSinceStartupAsDouble >= nextSave)
                    _ = SaveDraft();
                return;
            }
            if (!gate || !gate.Ready || chairs.Length != 2) return;

            int slot = Array.FindIndex(chairs, chair => chair && chair.occupant == player);
            if (slot < 0) return;
            occupied = chairs[slot];
            accountId = gate.Session.UserId;
            visit = new CancellationTokenSource();
            notice = connectionNotice = "";
            if (gate.LobbyEnabled) _ = JoinAndPoll(gate.Session, slot, visit.Token);
            else connectionNotice = "온라인 퀴즈 연결이 꺼져 있습니다.";
        }

        async Task JoinAndPoll(AccountSession session, int slot, CancellationToken ct)
        {
            try
            {
                var admission = await session.JoinQuizSeatAsync(slot, ct);
                if (ct.IsCancellationRequested)
                {
                    if (!string.IsNullOrEmpty(admission?.ticket)) _ = ReleaseTicket(admission.ticket);
                    return;
                }
                if (string.IsNullOrEmpty(admission?.ticket) || admission.state?.players == null ||
                    admission.state.self_slot != slot || admission.state.Self == null)
                    throw new QuizDuelFailure(502, "좌석 정보를 확인하지 못했습니다.");
                ticket = admission.ticket;
                Apply(admission.state);
                while (true)
                {
                    await Task.Delay(500, ct);
                    int before = actionVersion;
                    try
                    {
                        var latest = await QuizDuelApi.Request(ticket, HttpMethod.Get, "state", null, null, ct);
                        ct.ThrowIfCancellationRequested();
                        connectionNotice = "";
                        // Keep the seat lease alive during uploads. A concurrent poll
                        // may have read the state before a newer accepted command.
                        if (!acting && before == actionVersion) Apply(latest);
                    }
                    catch (QuizDuelFailure e) when (e.Status == 401) { throw; }
                    catch (Exception) when (!ct.IsCancellationRequested)
                    {
                        connectionNotice = "연결을 다시 확인하고 있습니다…";
                        await Task.Delay(1000, ct);
                    }
                }
            }
            catch (Exception e)
            {
                if (ct.IsCancellationRequested) return;
                EndVisit();
                if (e is AccountException account && account.Code == "seat_taken")
                    notice = "다른 참가자가 이미 앉아 있습니다. 옆자리에 앉아 주세요.";
                else if (e is AccountException duplicate && duplicate.Code == "already_seated")
                    notice = "이 계정은 다른 좌석을 사용 중입니다.";
                else if (e is QuizDuelFailure failure && failure.Status == 401)
                    notice = "좌석 사용 시간이 끝났습니다. 다시 앉아 주세요.";
                else notice = "좌석에 연결하지 못했습니다. 잠시 후 다시 앉아 주세요.";
            }
        }

        void Apply(QuizDuelState latest)
        {
            bool newRound = state == null || latest.round_id != state.round_id;
            if (newRound || latest.phase != state.phase || latest.question?.level != state.question?.level)
            {
                savedChoice = selected = -1; savedText = draft = "";
                scroll = Vector2.zero;
                notice = "";
            }
            if (newRound || latest.phase == "waiting" || latest.phase == "playing")
                ClearPdf();
            state = latest;
            receivedAt = Time.realtimeSinceStartupAsDouble;
        }

        void ClearPdf()
        {
            documentBytes = null;
            document.Reset();
            browsing = useRange = false;
            startPage = endPage = "1";
        }

        void OnDisable() { EndVisit(); chairs = null; }
        void OnDestroy() { theme?.Dispose(); }

        void EndVisit()
        {
            var leaving = ticket;
            ticket = accountId = null;
            visit?.Cancel();
            visit?.Dispose();
            visit = null;
            if (occupied && player && occupied.occupant == player)
            {
                player.Stand();
                // AccountWorldGate disables the controller before it disables this component.
                if (!player.enabled && player.TryGetComponent<CharacterController>(out var body)) body.enabled = false;
            }
            occupied = null;
            state = null;
            acting = saving = false;
            savedChoice = -1; savedText = ""; nextSave = 0;
            actionVersion++;
            notice = connectionNotice = draft = "";
            selected = -1;
            ClearPdf();
            if (!string.IsNullOrEmpty(leaving)) _ = ReleaseTicket(leaving);
        }

        static async Task ReleaseTicket(string leaving)
        {
            try { await QuizDuelApi.Request(leaving, HttpMethod.Delete, "seat", null, null, CancellationToken.None); }
            catch (Exception) { /* Server expires the seat if the client goes offline. */ }
        }

        async Task Command(string path, byte[] payload = null, string contentType = null)
        {
            if (acting || visit == null || string.IsNullOrEmpty(ticket)) return;
            var ct = visit.Token;
            acting = true; actionVersion++; notice = "";
            try
            {
                var latest = await QuizDuelApi.Request(ticket, HttpMethod.Post, path, payload, contentType, ct);
                ct.ThrowIfCancellationRequested();
                Apply(latest);
            }
            catch (Exception e)
            {
                if (ct.IsCancellationRequested) return;
                if (e is QuizDuelFailure expired && expired.Status == 401)
                {
                    EndVisit();
                    notice = "좌석 사용 시간이 끝났습니다. 다시 앉아 주세요.";
                }
                else notice = e is QuizDuelFailure failure ? failure.Message :
                    "요청을 확인하지 못했습니다. 연결을 확인하고 다시 시도해 주세요.";
            }
            finally { if (!ct.IsCancellationRequested) { acting = false; actionVersion++; } }
        }

        void SelectPdf(string chosen)
        {
            if (acting || state == null) return;
            browsing = false;
            notice = "";
            ClearPdf();
            try
            {
                if (!Path.GetExtension(chosen).Equals(".pdf", StringComparison.OrdinalIgnoreCase)) throw new IOException();
                using (var stream = File.OpenRead(chosen))
                {
                    document.Select(chosen, stream.Length);
                    if (!document.CanInspect) { notice = document.Warning; return; }
                    documentBytes = new byte[(int)stream.Length];
                    int read = 0;
                    while (read < documentBytes.Length)
                    {
                        int count = stream.Read(documentBytes, read, documentBytes.Length - read);
                        if (count == 0) throw new IOException();
                        read += count;
                    }
                }
                directory = Path.GetDirectoryName(chosen);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
            {
                ClearPdf();
                notice = "PDF를 열지 못했습니다. 파일과 접근 권한을 확인해 주세요.";
            }
        }

        void Upload()
        {
            if (state == null || documentBytes == null || acting) return;
            string path = "pdf?round_id=" + Uri.EscapeDataString(state.round_id);
            if (useRange)
            {
                if (!int.TryParse(startPage, out int start) || !int.TryParse(endPage, out int end) ||
                    start < 1 || end < start || end - start >= QuizDocument.MaxPages)
                {
                    notice = "1페이지부터 최대 1000페이지 이내로 시작과 끝을 지정해 주세요.";
                    return;
                }
                path += "&start_page=" + start + "&end_page=" + end;
            }
            _ = Command(path, documentBytes, "application/pdf");
        }

        bool HasUnsavedAnswer => state?.phase == "playing" && state.question != null &&
            (state.question.level == 1 ? selected != savedChoice : draft != savedText);

        QuizDuelAnswer CurrentAnswer() => new QuizDuelAnswer {
            round_id = state.round_id, level = state.question.level,
            choice = state.question.level == 1 ? selected : -1, text = state.question.level == 1 ? "" : draft
        };

        async Task SaveDraft()
        {
            var answer = CurrentAnswer();
            var ct = visit.Token;
            saving = true;
            nextSave = Time.realtimeSinceStartupAsDouble + .35;
            try
            {
                await QuizDuelApi.Request(ticket, HttpMethod.Post, "draft",
                    Encoding.UTF8.GetBytes(JsonUtility.ToJson(answer)), "application/json", ct);
                if (!ct.IsCancellationRequested && state?.round_id == answer.round_id &&
                    state.phase == "playing" && state.question.level == answer.level)
                {
                    savedChoice = answer.choice;
                    savedText = answer.text;
                }
            }
            catch (Exception) { if (!ct.IsCancellationRequested) nextSave = Time.realtimeSinceStartupAsDouble + 1; }
            finally { if (!ct.IsCancellationRequested) saving = false; }
        }

        void Submit()
        {
            if (state?.phase != "playing" || state.question == null || acting) return;
            _ = Command("answer", Encoding.UTF8.GetBytes(JsonUtility.ToJson(CurrentAnswer())), "application/json");
        }

        void Browse(string location)
        {
            browsing=true;browserPage=0;fileScroll=Vector2.zero;folders=files=new string[0];
            try {
                var dir=new DirectoryInfo(location);var dirs=dir.GetDirectories();var entries=dir.GetFiles();
                Array.Sort(dirs,(a,b)=>StringComparer.OrdinalIgnoreCase.Compare(a.Name,b.Name));
                Array.Sort(entries,(a,b)=>StringComparer.OrdinalIgnoreCase.Compare(a.Name,b.Name));
                folders=Array.ConvertAll(dirs,d=>d.FullName);
                files=Array.ConvertAll(Array.FindAll(entries,f=>f.Extension.Equals(".pdf",StringComparison.OrdinalIgnoreCase)),f=>f.FullName);
                directory=dir.FullName;
            } catch(Exception e) when(e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException) {
                notice="이 폴더에 접근할 수 없습니다. 홈이나 다른 드라이브를 선택해 주세요.";
            }
        }
        void ChoosePdf()
        {
#if UNITY_EDITOR
            string chosen=UnityEditor.EditorUtility.OpenFilePanel("공부한 PDF 선택",directory,"pdf");
            if(!string.IsNullOrEmpty(chosen)) SelectPdf(chosen);
#else
            Browse(directory);
#endif
        }
        void Label(string text,GUIStyle style,params GUILayoutOption[] options) => GUILayout.Label(QuizText.Display(text),style,options);
        bool Button(string text,GUIStyle style,params GUILayoutOption[] options) => GUILayout.Button(QuizText.Display(text),style,options);
        void DrawBrowser()
        {
            GUILayout.BeginVertical(theme.Card);GUILayout.BeginHorizontal();
            if(Button("내 문서",theme.Quiet)) Browse(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            if(Button("홈",theme.Quiet)) Browse(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            if(Button("상위",theme.Quiet)) {var parent=Directory.GetParent(directory);if(parent!=null) Browse(parent.FullName);}
            if(Button("닫기",theme.Quiet)) browsing=false;GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();foreach(string drive in Directory.GetLogicalDrives()) if(Button(drive,theme.Quiet)) {Browse(drive);break;}GUILayout.EndHorizontal();
            Label(directory,theme.Small);GUILayout.Space(8);
            const int pageSize=100;int pageCount=Math.Max(1,(folders.Length+files.Length+pageSize-1)/pageSize);browserPage=Math.Min(browserPage,pageCount-1);
            GUILayout.BeginHorizontal();if(browserPage>0 && Button("이전 목록",theme.Quiet)) {browserPage--;fileScroll=Vector2.zero;}
            Label((browserPage+1)+" / "+pageCount,theme.Small);if(browserPage+1<pageCount && Button("다음 목록",theme.Quiet)) {browserPage++;fileScroll=Vector2.zero;}GUILayout.EndHorizontal();
            fileScroll=GUILayout.BeginScrollView(fileScroll,GUILayout.Height(190));
            for(int i=browserPage*pageSize;i<Math.Min(folders.Length+files.Length,(browserPage+1)*pageSize);i++) {
                if(i<folders.Length) {if(Button("[폴더] "+Path.GetFileName(folders[i]),theme.Quiet)) {Browse(folders[i]);break;}}
                else if(Button(Path.GetFileName(files[i-folders.Length]),theme.Quiet)) {SelectPdf(files[i-folders.Length]);break;}
            }
            if(files.Length==0) Label("이 폴더에는 PDF가 없습니다.",theme.Small);
            GUILayout.EndScrollView();GUILayout.EndVertical();
        }
        void OnGUI()
        {
            if (!InRoom || !player.isActiveAndEnabled || player.IsExitConfirmOpen) return;
            if (theme == null) theme = new QuizTheme();
            if (!BlocksMovement) DrawChairNumbers();
            var previousMatrix = GUI.matrix;
            var previousSkin = GUI.skin;
            var previousColor = GUI.color;
            bool previousEnabled = GUI.enabled;
            float scale = Mathf.Min(Screen.width / 980f, Screen.height / 680f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 980 * scale) / 2,
                (Screen.height - 680 * scale) / 2), Quaternion.identity, Vector3.one * scale);
            GUI.skin = theme.Skin;
            if (!BlocksMovement)
            {
                GUILayout.BeginArea(new Rect(180, 550, 620, 126), theme.Panel);
                Label(string.IsNullOrEmpty(notice) ? "퀴즈 좌석으로 이동해 E를 눌러 앉으세요." : notice, theme.Body);
                GUILayout.Space(5); Label("두 사람이 앉으면 각자의 PDF로 3단계 퀴즈를 시작합니다.", theme.Small);
                GUILayout.EndArea();
            }
            else
            {
                GUI.Box(new Rect(20, 20, 940, 640), GUIContent.none, theme.Panel);
                GUILayout.BeginArea(new Rect(54, 42, 872, 590));
                GUILayout.BeginHorizontal(); Label("2인 PDF 퀴즈", theme.Title); GUILayout.FlexibleSpace();
                if (Button("자리에서 일어나기", theme.Secondary)) EndVisit();
                GUILayout.EndHorizontal(); GUILayout.Space(14);
                scroll = GUILayout.BeginScrollView(scroll, GUIStyle.none, GUI.skin.verticalScrollbar);
                if (state == null) Label(string.IsNullOrEmpty(connectionNotice) ? "좌석 연결 중…" : connectionNotice, theme.Body);
                else DrawMatch();
                if (notice.Length > 0) { GUILayout.Space(8); Label(notice, theme.Warning); }
                if (state != null && connectionNotice.Length > 0) Label(connectionNotice, theme.Warning);
                GUILayout.EndScrollView(); GUILayout.EndArea();
            }
            GUI.enabled = previousEnabled; GUI.color = previousColor; GUI.skin = previousSkin; GUI.matrix = previousMatrix;
        }

        void DrawChairNumbers()
        {
            var camera = Camera.main;
            if (!camera || chairs == null || chairs.Length != 2) return;
            if (chairLabel == null)
            {
                chairLabel = new GUIStyle(GUI.skin.box) { font = theme.Body.font, fontSize = 14,
                    fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }
            for (int i = 0; i < chairs.Length; i++)
            {
                if (!chairs[i]) continue;
                var point = camera.WorldToScreenPoint(chairs[i].transform.position + Vector3.up * .68f);
                if (point.z > camera.nearClipPlane)
                    GUI.Box(new Rect(point.x - 34, Screen.height - point.y - 13, 68, 26), "좌석 " + (i + 1), chairLabel);
            }
        }

        void DrawMatch()
        {
            GUILayout.BeginHorizontal();
            foreach (var p in state.players)
                Label((p.slot == state.self_slot ? "나  " : "상대  ") + p.nickname + "  ·  Lv." + p.level,
                    theme.Body, GUILayout.Width(400));
            GUILayout.EndHorizontal(); GUILayout.Space(18);

            switch (state.phase)
            {
                case "waiting":
                    Label("상대 참가자를 기다리는 중  ·  1 / 2", theme.Heading);
                    Label("다른 계정이 나머지 좌석에 앉으면 PDF를 선택할 수 있습니다.", theme.Small);
                    break;
                case "upload":
                    DrawUpload();
                    break;
                case "generating":
                    Label("두 사람의 문제를 만들고 있습니다…", theme.Heading);
                    Label("공통 난이도 Lv." + state.target_level + "  ·  객관식, 단답형, 서술형", theme.Body);
                    break;
                case "playing":
                    DrawQuestion();
                    break;
                case "grading":
                    Label("최종 결과 대기 중", theme.Heading);
                    Label("두 사람의 풀이와 채점이 끝나면 시간과 정답률을 비교합니다.", theme.Body);
                    break;
                case "results":
                    DrawResults();
                    break;
                case "error":
                    Label(state.message, theme.Warning);
                    GUI.enabled = !acting;
                    if (Button("다시 시도", theme.Primary)) _ = Command("retry?round_id=" + Uri.EscapeDataString(state.round_id));
                    GUI.enabled = true;
                    if (state.can_upload) DrawUpload();
                    break;
                default:
                    Label("서버에서 대전 상태를 확인하고 있습니다.", theme.Body);
                    break;
            }
        }

        void DrawUpload()
        {
            var self = state.Self;
            if (self.ready && state.phase != "error")
            {
                Label("내 PDF 제출 완료", theme.Heading);
                Label("상대가 PDF를 제출하면 AI가 두 사람의 문제를 준비합니다.", theme.Body);
                return;
            }
            Label("내 PDF 선택", theme.Heading);
            Label("공통 난이도 Lv." + state.target_level + "  ·  각자 자기 PDF로 3문제를 풉니다.", theme.Small);
            GUILayout.Space(12);
            GUI.enabled = !acting;
            if (Button(documentBytes == null ? "PDF 선택하기" : "다른 PDF 선택하기", theme.Secondary)) ChoosePdf();
            if (browsing) DrawBrowser();
            if (documentBytes != null)
            {
                GUILayout.Space(12);
                Label(document.Name + "  ·  " + document.SizeLabel, theme.Body);
                useRange = GUILayout.Toggle(useRange, " 페이지 범위 지정");
                if (useRange)
                {
                    GUILayout.BeginHorizontal();
                    startPage = GUILayout.TextField(startPage, 8, theme.Input, GUILayout.Width(120), GUILayout.Height(48));
                    Label("부터", theme.Small, GUILayout.Width(45));
                    endPage = GUILayout.TextField(endPage, 8, theme.Input, GUILayout.Width(120), GUILayout.Height(48));
                    Label("페이지까지", theme.Small); GUILayout.EndHorizontal();
                }
                GUILayout.Space(8);
                if (Button(acting ? "업로드 중…" : "이 PDF로 참가", theme.Primary)) Upload();
            }
            GUI.enabled = true;
        }

        void DrawQuestion()
        {
            var q = state.question;
            if (q == null || q.level < 1 || q.level > 3 || string.IsNullOrWhiteSpace(q.prompt)) return;
            int limit = 5 * (q.level + 1);
            double remaining = Math.Max(0, state.remaining_ms / 1000d - (Time.realtimeSinceStartupAsDouble - receivedAt));
            GUILayout.BeginHorizontal(); GUILayout.BeginVertical();
            Label(q.level + "단계  ·  " + new[]{"객관식", "단답형", "서술형"}[q.level - 1], theme.Title);
            Label("제한 " + limit + "초  ·  10점 만점", theme.Small);
            GUILayout.EndVertical(); GUILayout.FlexibleSpace();
            var clock = GUILayoutUtility.GetRect(82, 80, GUILayout.Width(82), GUILayout.Height(80));
            theme.Countdown(clock, remaining, limit); GUILayout.EndHorizontal();
            Label(q.prompt, theme.Body); GUILayout.Space(12);
            GUI.enabled = !acting && remaining > 0;
            if (q.level == 1 && q.options != null)
            {
                for (int i = 0; i < q.options.Length; i++)
                {
                    if (Button((i + 1) + "   " + q.options[i], selected == i ? theme.SelectedChoice : theme.Choice)) selected = i;
                    GUILayout.Space(5);
                }
            }
            else if (q.level == 2) draft = GUILayout.TextField(draft, 300, theme.Input, GUILayout.Height(58));
            else draft = GUILayout.TextArea(draft, 1000, theme.Input, GUILayout.Height(108));
            GUILayout.Space(8);
            GUI.enabled = GUI.enabled && (q.level == 1 ? selected >= 0 : !string.IsNullOrWhiteSpace(draft));
            if (Button(acting ? "제출 중…" : "답 제출", theme.Primary)) Submit();
            GUI.enabled = true;
            Label(HasUnsavedAnswer || saving ? "답 저장 중…" : "시간이 끝나면 서버에 저장된 마지막 답으로 채점합니다.", theme.Small);
        }

        void DrawResults()
        {
            Label("대전 결과", theme.Heading);
            Label("정답률은 세 문제 중 만점을 받은 문제의 비율입니다. 부분점수는 총점에 반영합니다.", theme.Small);
            if (state.results == null) return;
            GUILayout.Space(12);
            foreach (var r in state.results)
            {
                GUILayout.BeginVertical(theme.Card);
                Label((r.slot == state.self_slot ? "나  " : "상대  ") + r.nickname, theme.Title);
                Label("총점 " + r.score + " / 30점   ·   정답률 " + r.accuracy.ToString("0.#") + "% (" +
                    r.correct + "/3)   ·   풀이 시간 " + (r.elapsed_ms / 1000d).ToString("0.0") + "초", theme.Body);
                if (r.stages != null)
                    foreach (var stage in r.stages)
                        Label(stage.level + "단계    " + stage.score + " / 10점    " +
                            (stage.elapsed_ms / 1000d).ToString("0.0") + "초", theme.Small);
                GUILayout.EndVertical(); GUILayout.Space(8);
            }
            if (state.review == null || state.review.Length == 0) return;
            Label("내 PDF 풀이 복습", theme.Title); GUILayout.Space(8);
            foreach (var item in state.review)
            {
                GUILayout.BeginVertical(theme.Card);
                Label(item.level + "단계  ·  PDF " + item.source_page + "페이지  ·  " + item.score + "/10점", theme.Caption);
                Label(item.prompt, theme.Body);
                Label("내 답   " + (string.IsNullOrEmpty(item.answer) ? "미응답" : item.answer), theme.Small);
                Label("모범 답   " + item.model_answer, theme.Body);
                Label(item.explanation, theme.Small);
                if (!string.IsNullOrEmpty(item.feedback)) Label(item.feedback, theme.Small);
                GUILayout.EndVertical(); GUILayout.Space(8);
            }
        }
    }
}
