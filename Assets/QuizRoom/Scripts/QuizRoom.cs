using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CampusCommons.Account;
using CampusCommons.GrandLibrary;
using UnityEngine;
using UnityEngine.UI;

namespace CampusCommons.Quiz
{
    [DefaultExecutionOrder(200)]
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
        GUIStyle chairHint;
        bool boardDirty = true, framed;
        Camera boardCamera;
        Vector3 previousCameraPosition;
        Quaternion previousCameraRotation;
        Button submitButton;
        Button[] choices;
        Text clock, saveStatus;
        readonly System.Collections.Generic.List<Selectable> actions = new System.Collections.Generic.List<Selectable>();
        string draft = "";
        QuizTheme theme;
        bool useRange;
        CancellationTokenSource picker;
        string startPage = "1", endPage = "1", directory;
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
            UpdateVisit();
            if (!InRoom || !player.isActiveAndEnabled)
            {
                RemoveBoard();
                return;
            }
            if (theme == null)
            {
                var room = chairs != null && chairs.Length > 0 && chairs[0] ? chairs[0].transform : null;
                while (room && room.name != "Quiz Room") room = room.parent;
                var surface = room ? room.Find("Architecture/Quiz Screen Structure") : null;
                boardCamera = Camera.main;
                if (!surface || !surface.TryGetComponent<Renderer>(out var screen) || !boardCamera) return;
                theme = new QuizTheme(screen, boardCamera, EndVisit);
                boardDirty = true;
            }
            if (BlocksMovement)
            {
                if (!framed)
                {
                    previousCameraPosition = boardCamera.transform.position;
                    previousCameraRotation = boardCamera.transform.rotation;
                }
                theme.Frame(boardCamera);
                framed = true;
            }
            theme.SetInteraction(BlocksMovement && !player.IsExitConfirmOpen && !AccountGate.LobbyOverlayOwnsInput);
            RefreshBoard();
        }

        void UpdateVisit()
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
            boardDirty = true;
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
                long accountExpiry = admission.account_expires_at;
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
                        if (accountExpiry > 0 && accountExpiry <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 45)
                            accountExpiry = await session.RenewQuizSeatAsync(slot, admission.ticket, ct);
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
                boardDirty = true;
                notice = "";
            }
            // Polls update the countdown in place so a live Korean IME composition
            // and a scrolled review survive even when the server polls twice a second.
            if (state == null || state.message != latest.message || state.can_upload != latest.can_upload ||
                state.DifficultyLabel != latest.DifficultyLabel || state.Self.ready != latest.Self.ready ||
                state.players.Length != latest.players.Length) boardDirty = true;
            if (newRound || latest.phase == "waiting" || latest.phase == "playing")
                ClearPdf();
            state = latest;
            if (picker != null && (newRound || !latest.can_upload || (latest.Self.ready && latest.phase != "error")))
                CancelPicker();
            receivedAt = Time.realtimeSinceStartupAsDouble;
        }

        void ClearPdf()
        {
            documentBytes = null;
            document.Reset();
            useRange = false;
            startPage = endPage = "1";
        }

        void RemoveBoard()
        {
            theme?.Dispose();
            theme = null;
            boardCamera = null;
        }

        void OnDisable() { EndVisit(); RemoveBoard(); chairs = null; }
        void OnDestroy() { RemoveBoard(); }

        void EndVisit()
        {
            CancelPicker();
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
            if (framed)
            {
                if (player && player.isActiveAndEnabled) player.RefreshView();
                else if (boardCamera) boardCamera.transform.SetPositionAndRotation(previousCameraPosition, previousCameraRotation);
            }
            framed = false;
            boardDirty = true;
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
            boardDirty = true;
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

        bool CanChoosePdf => visit != null && !acting && state?.Self != null &&
            ((state.phase == "upload" && !state.Self.ready) || (state.phase == "error" && state.can_upload));

        void CancelPicker()
        {
            var pending = picker;
            picker = null;
            pending?.Cancel();
        }

        async void ChoosePdf()
        {
            if (picker != null || !CanChoosePdf) return;
            var current = CancellationTokenSource.CreateLinkedTokenSource(visit.Token);
            picker = current;
            var round = state.round_id;
            try
            {
                var chosen = await QuizPdfPicker.OpenAsync(directory, current.Token);
                if (picker == current && !current.IsCancellationRequested && CanChoosePdf &&
                    state.round_id == round && !string.IsNullOrEmpty(chosen))
                    SelectPdf(chosen);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                if (picker == current && !current.IsCancellationRequested)
                {
                    notice = "PDF 선택 창을 열지 못했습니다. 다시 시도해 주세요.";
                    Debug.LogWarning("Quiz PDF picker: " + e.GetType().Name);
                }
            }
            finally
            {
                if (picker == current) picker = null;
                current.Dispose();
            }
        }

        void OnGUI()
        {
            if (!InRoom || !player.isActiveAndEnabled || BlocksMovement || player.IsExitConfirmOpen ||
                AccountGate.LobbyOverlayOwnsInput || chairs == null) return;
            // Match Player.Update's actual E interaction origin and distance.
            var origin = player.transform.position + Vector3.up * .6f;
            if (!Array.Exists(chairs, chair => chair && !chair.occupant &&
                (chair.transform.position - origin).sqrMagnitude < 1.65f * 1.65f)) return;
            if (chairHint == null)
            {
                chairHint = new GUIStyle(GUI.skin.box) {
                    font = Resources.Load<Font>("KIT/NotoSansKR"), fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            chairHint.fontSize = Mathf.Max(12, Mathf.RoundToInt(19 * scale));
            GUI.Box(new Rect(Screen.width * .5f - 66 * scale, Screen.height * .79f, 132 * scale, 42 * scale),
                "E   앉기", chairHint);
        }

        void RefreshBoard()
        {
            if (boardDirty)
            {
                boardDirty = false;
                actions.Clear();
                choices = null;
                submitButton = null;
                clock = saveStatus = null;
                theme.BeginPage("2인 PDF 퀴즈", BlocksMovement);
                if (!BlocksMovement) DrawRules();
                else if (state == null) theme.Line("좌석 연결 중…", 30, true);
                else DrawMatch();
            }

            theme.Status(string.IsNullOrEmpty(notice) ? connectionNotice :
                string.IsNullOrEmpty(connectionNotice) ? notice : notice + "   ·   " + connectionNotice);
            double remaining = state == null ? 0 : Math.Max(0, state.remaining_ms / 1000d -
                (Time.realtimeSinceStartupAsDouble - receivedAt));
            foreach (var action in actions)
                action.interactable = !acting && (state?.phase != "playing" || remaining > 0);
            if (clock) clock.text = "남은 시간  " + Math.Ceiling(remaining) + "초";
            if (choices != null)
                for (int i = 0; i < choices.Length; i++)
                    choices[i].image.color = i == selected ? QuizTheme.Selected : QuizTheme.Control;
            if (submitButton) submitButton.interactable = !acting && remaining > 0 &&
                (state.question.level == 1 ? selected >= 0 : !string.IsNullOrWhiteSpace(draft));
            if (saveStatus) saveStatus.text = HasUnsavedAnswer || saving ? "답 저장 중…" :
                "시간이 끝나면 마지막으로 저장한 답을 채점합니다.";
        }

        void DrawRules()
        {
            theme.Line("두 사람이 한 자리씩 앉아 각자 공부한 PDF를 선택하세요.", 48, true).alignment = TextAnchor.MiddleCenter;
            theme.Line("객관식 10초   ·   단답형 15초   ·   서술형 20초", 61, true, true).alignment = TextAnchor.MiddleCenter;
            theme.Line("각 단계는 10점입니다. 두 사람의 총점, 정답률, 풀이 시간을 비교하세요.", 40).alignment = TextAnchor.MiddleCenter;
        }

        void DrawMatch()
        {
            string players = string.Join("      ", Array.ConvertAll(state.players, p =>
                (p.slot == state.self_slot ? "나  " : "상대  ") + p.nickname + "  ·  Lv." + p.level));
            theme.Line(players, 19, false, true);
            switch (state.phase)
            {
                case "waiting":
                    theme.Line("상대 참가자를 기다리는 중  ·  1 / 2", 30, true);
                    theme.Line("다른 계정이 나머지 좌석에 앉으면 각자의 PDF를 선택할 수 있습니다.");
                    theme.Line("객관식 10초   ·   단답형 15초   ·   서술형 20초", 23, false, true);
                    break;
                case "upload":
                    DrawUpload();
                    break;
                case "generating":
                    theme.Line("두 사람의 문제를 만들고 있습니다…", 30, true);
                    theme.Line(state.DifficultyLabel);
                    break;
                case "playing":
                    DrawQuestion();
                    break;
                case "grading":
                    theme.Line("최종 결과 대기 중", 30, true);
                    theme.Line("두 사람의 풀이와 채점이 끝나면 결과를 확인할 수 있습니다.");
                    break;
                case "results":
                    DrawResults();
                    break;
                case "error":
                    theme.Line(state.message);
                    actions.Add(theme.ActionButton("다시 시도", () => _ = Command("retry?round_id=" + Uri.EscapeDataString(state.round_id))));
                    if (state.can_upload) DrawUpload();
                    break;
                default:
                    theme.Line("서버에서 대전 상태를 확인하고 있습니다.");
                    break;
            }
        }

        void DrawUpload()
        {
            if (state.Self.ready && state.phase != "error")
            {
                theme.Line("내 PDF 제출 완료", 32, true);
                theme.Line("상대가 PDF를 제출하면 두 사람의 문제를 준비합니다.");
                return;
            }
            theme.Line("공부한 PDF 선택", 32, true);
            theme.Line(state.DifficultyLabel + "  ·  각자 자기 PDF로 3문제를 풉니다.", 21);
            actions.Add(theme.ActionButton(documentBytes == null ? "PDF 선택하기" : "다른 PDF 선택하기", ChoosePdf));
            if (documentBytes == null) return;
            theme.Line(document.Name + "  ·  " + document.SizeLabel);
            var row = theme.Row();
            actions.Add(theme.ActionButton(useRange ? "전체 페이지 사용" : "페이지 범위 지정", () => {
                useRange = !useRange;
                boardDirty = true;
            }, row));
            if (useRange)
            {
                actions.Add(theme.Field("시작 페이지", startPage, 8, value => startPage = value, row: row, numeric: true));
                actions.Add(theme.Field("끝 페이지", endPage, 8, value => endPage = value, row: row, numeric: true));
            }
            actions.Add(theme.ActionButton("이 PDF로 참가", Upload, primary: true));
        }

        void DrawQuestion()
        {
            var q = state.question;
            if (q == null || q.level < 1 || q.level > 3 || string.IsNullOrWhiteSpace(q.prompt))
            {
                theme.Line("상대의 풀이가 끝나기를 기다리는 중…", 28, true);
                return;
            }
            var names = new[] { "객관식", "단답형", "서술형" };
            theme.Line(q.level + "단계  ·  " + names[q.level - 1] + "   /   " + state.DifficultyLabel, 22, true, true);
            clock = theme.Line("", 23, true);
            theme.Line(q.prompt, 27, true);
            if (q.level == 1 && q.options != null)
            {
                choices = new Button[q.options.Length];
                RectTransform row = null;
                for (int i = 0; i < choices.Length; i++)
                {
                    if (i % 2 == 0) row = theme.Row();
                    int choice = i;
                    choices[i] = theme.ActionButton((i + 1) + ".  " + q.options[i], () => selected = choice, row);
                    actions.Add(choices[i]);
                }
            }
            else actions.Add(theme.Field("답을 입력하세요", draft, q.level == 2 ? 300 : 1000,
                value => draft = value, multiline: q.level == 3));
            submitButton = theme.ActionButton("답 제출", Submit, primary: true);
            saveStatus = theme.Line("", 18, false, true);
        }

        void DrawResults()
        {
            theme.Line("대전 결과", 32, true);
            theme.Line("정답률은 세 문제 중 만점을 받은 비율입니다. 총점에는 부분점수를 포함합니다.", 20);
            if (state.results == null) return;
            foreach (var result in state.results)
            {
                theme.Line((result.slot == state.self_slot ? "나  " : "상대  ") + result.nickname +
                    "   ·   " + result.score + " / 30점", 25, true, true);
                theme.Line("정답률  " + result.accuracy.ToString("0.#") + "% (" + result.correct +
                    "/3)     풀이 시간  " + (result.elapsed_ms / 1000d).ToString("0.0") + "초", 22);
                if (result.stages == null) continue;
                foreach (var stage in result.stages)
                    theme.Line(stage.level + "단계    " + stage.score + " / 10점    " +
                        (stage.elapsed_ms / 1000d).ToString("0.0") + "초", 18);
            }
            if (state.review == null || state.review.Length == 0) return;
            theme.Line("내 PDF 풀이 복습", 28, true);
            foreach (var item in state.review)
            {
                theme.Line(item.level + "단계    ·    PDF " + item.source_page + "페이지    ·    " +
                    item.score + "/10점", 22, true, true);
                theme.Line(item.prompt, 24, true);
                theme.Line("내 답    " + (string.IsNullOrEmpty(item.answer) ? "미응답" : item.answer));
                theme.Line("모범 답    " + item.model_answer);
                theme.Line(item.explanation, 20);
                if (!string.IsNullOrEmpty(item.feedback)) theme.Line(item.feedback, 20);
            }
        }
    }
}
