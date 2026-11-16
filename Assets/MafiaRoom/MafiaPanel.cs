using System;
using System.Collections;
using System.Text;
using CampusCommons.Account;
using CampusCommons.GrandLibrary;
using CampusCommons.Quiz;
using UnityEngine;
using UnityEngine.Networking;

namespace CampusCommons.Mafia
{
    public sealed class MafiaPanel : MonoBehaviour
    {
        const string Rooms = "/v1/mafia/rooms";

        [Serializable] sealed class JoinBody { public string nickname; }
        [Serializable] sealed class Admission { public string room_id, member_token; }
        [Serializable] sealed class ErrorBody { public string detail; }
        [Serializable] sealed class Turn { public int revision; public string kind, target, text; }
        [Serializable] sealed class Line { public int sequence; public string speaker, text, source; }
        [Serializable] sealed class RoomView
        {
            public string room_id, phase, result, revealed_professor;
            public int round, revision;
            public bool is_host, pending, can_speak;
            public string[] members, alive;
            public Line[] messages;
        }

        Player player;
        Player heldPlayer;
        QuizDeployment config;
        UnityWebRequest active;
        RoomView view;
        bool inRoom, panelOpen;
        float nextPoll;
        string room = "", member = "", nickname = "", code = "", compose = "", status = "", selected = "";
        Vector2 scroll;
        GUIStyle heading, label, button, input;
        int lastSequence;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var instance = new GameObject("Campus mafia room");
            DontDestroyOnLoad(instance);
            instance.AddComponent<MafiaPanel>();
        }

        void Awake()
        {
            var asset = Resources.Load<TextAsset>("QuizConnection");
            if (!asset) return;
            try
            {
                var candidate = JsonUtility.FromJson<QuizDeployment>(asset.text);
                if (candidate != null && candidate.IsValid()) config = candidate;
            }
            catch (ArgumentException) { }
        }

        void Update()
        {
            if (!player) player = FindAnyObjectByType<Player>();
            var gate = AccountGate.Instance;
            bool inside = player && player.currentSpace == Portal.Space.Mafia && gate && gate.Ready;
            if (inside != inRoom)
            {
                inRoom = inside;
                if (inside)
                {
                    nickname = gate.Session.Profile?.nickname ?? "";
                    ShowPanel();
                }
                else
                {
                    ClosePanel();
                    LeaveRoom();
                }
            }
            if (!inside) return;
            if (panelOpen && Input.GetKeyDown(KeyCode.Escape)) ClosePanel();
            if (!panelOpen && !player.IsExitConfirmOpen && Input.GetKeyDown(KeyCode.M)) ShowPanel();
            if (room.Length > 0 && active == null && Time.unscaledTime >= nextPoll)
                StartCoroutine(Send("GET", RoomPath(), null, ApplyView, true));
        }

        void ShowPanel()
        {
            panelOpen = true;
            if (player && player.enabled)
            {
                heldPlayer = player;
                player.enabled = false;
            }
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        void ClosePanel()
        {
            panelOpen = false;
            if (heldPlayer && AccountGate.Instance && AccountGate.Instance.Ready) heldPlayer.enabled = true;
            heldPlayer = null;
        }

        string RoomPath() => Rooms + "/" + room;

        UnityWebRequest MakeRequest(string method, string path, object body)
        {
            var request = new UnityWebRequest(config.server_url.TrimEnd('/') + path, method);
            request.redirectLimit = 0;
            request.timeout = room.Length == 0 ? 80 : 12;
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("X-Quiz-Token", config.access_token);
            if (member.Length > 0) request.SetRequestHeader("X-Mafia-Member", member);
            if (body != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));
                request.SetRequestHeader("Content-Type", "application/json");
            }
            return request;
        }

        IEnumerator Send(string method, string path, object body, Action<string> success, bool quiet = false)
        {
            active = MakeRequest(method, path, body);
            yield return active.SendWebRequest();
            var content = active.downloadHandler.text;
            var responseCode = active.responseCode;
            bool succeeded = active.result == UnityWebRequest.Result.Success;
            active.Dispose();
            active = null;
            nextPoll = Time.unscaledTime + (succeeded ? 1.25f : 3f);
            if (succeeded)
            {
                if (!quiet || status == "연결이 지연되고 있습니다.") status = "";
                try { success(content); }
                catch (ArgumentException) { status = "서버 응답을 읽을 수 없습니다."; }
            }
            else if (quiet && (responseCode == 403 || responseCode == 404))
            {
                ClearSession();
                status = "방 연결이 종료되었습니다. 다시 참가해 주세요.";
            }
            else if (!quiet) status = ErrorMessage(content);
            else status = "연결이 지연되고 있습니다.";
        }

        static string ErrorMessage(string body)
        {
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    var error = JsonUtility.FromJson<ErrorBody>(body);
                    if (!string.IsNullOrEmpty(error?.detail)) return error.detail;
                }
                catch (ArgumentException) { }
            }
            return "서버에 연결할 수 없습니다. 잠시 후 다시 시도해 주세요.";
        }

        void Admit(bool create)
        {
            var path = create ? Rooms : Rooms + "/" + Uri.EscapeDataString(code.Trim()) + "/join";
            status = "서버에 연결 중입니다.";
            StartCoroutine(Send("POST", path, new JoinBody { nickname = nickname }, content =>
            {
                var admission = JsonUtility.FromJson<Admission>(content);
                if (admission == null || string.IsNullOrEmpty(admission.member_token) || string.IsNullOrEmpty(admission.room_id))
                {
                    status = "방 참가 정보를 확인할 수 없습니다.";
                    return;
                }
                room = admission.room_id;
                member = admission.member_token;
                nextPoll = 0;
            }));
        }

        void ApplyView(string content)
        {
            var snapshot = JsonUtility.FromJson<RoomView>(content);
            if (snapshot?.messages == null || snapshot.alive == null || snapshot.members == null)
                throw new ArgumentException("Invalid room state");
            view = snapshot;
            if (Array.IndexOf(view.alive, selected) < 0) selected = "";
            if (view.messages.Length == 0 || view.messages[view.messages.Length - 1].sequence == lastSequence) return;
            lastSequence = view.messages[view.messages.Length - 1].sequence;
            scroll.y = float.MaxValue;
        }

        void Act(string kind)
        {
            string content = kind == "question" || kind == "chat" ? compose.Trim() : "";
            var turn = new Turn { revision = view.revision, kind = kind, target = selected, text = content };
            StartCoroutine(Send("POST", RoomPath() + "/actions", turn, response =>
            {
                ApplyView(response);
                if (content.Length > 0 && compose.Trim() == content) compose = "";
            }));
        }

        void LeaveRoom()
        {
            StopAllCoroutines();
            if (active != null)
            {
                active.Abort();
                active.Dispose();
                active = null;
            }
            if (member.Length > 0) StartCoroutine(NotifyLeave(MakeRequest("POST", RoomPath() + "/leave", null)));
            ClearSession();
            status = "";
        }

        void ClearSession()
        {
            room = member = compose = selected = "";
            lastSequence = 0;
            view = null;
            scroll = Vector2.zero;
        }

        static IEnumerator NotifyLeave(UnityWebRequest request)
        {
            using (request) yield return request.SendWebRequest();
        }

        void OnDestroy()
        {
            ClosePanel();
            if (active != null) { active.Abort(); active.Dispose(); }
        }

        void OnGUI()
        {
            if (!inRoom || !player || player.IsExitConfirmOpen) return;
            PrepareStyles();
            if (!panelOpen)
            {
                if (GUI.Button(new Rect(16, Screen.height - 57, 190, 40), "교수를 찾아라  [M]", button)) ShowPanel();
                return;
            }

            float width = Mathf.Min(Screen.width - 28, 780);
            float height = Mathf.Min(Screen.height - 28, 655);
            var bounds = new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height);
            int depth = GUI.depth;
            GUI.depth = -5;
            var color = GUI.color;
            GUI.color = new Color(.085f, .105f, .15f, .97f);
            GUI.DrawTexture(bounds, Texture2D.whiteTexture);
            GUI.color = color;
            GUILayout.BeginArea(new Rect(bounds.x + 20, bounds.y + 16, width - 40, height - 32));
            GUILayout.BeginHorizontal();
            GUILayout.Label("교수를 찾아라", heading);
            if (GUILayout.Button("접기", button, GUILayout.Width(74))) ClosePanel();
            GUILayout.EndHorizontal();
            if (status.Length > 0) GUILayout.Label(status, label);
            if (config == null) GUILayout.Label("마피아룸 연결 설정이 필요합니다.", label);
            else if (room.Length == 0) DrawJoin();
            else DrawRoom();
            GUILayout.EndArea();
            GUI.depth = depth;
        }

        void PrepareStyles()
        {
            if (heading != null) return;
            var font = Resources.Load<Font>("KIT/NotoSansKR") ?? GUI.skin.font;
            heading = new GUIStyle(GUI.skin.label) { font = font, fontSize = 25, fontStyle = FontStyle.Bold };
            label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 15, wordWrap = true, richText = false };
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 15 };
            input = new GUIStyle(GUI.skin.textField) { font = font, fontSize = 16 };
        }

        void DrawJoin()
        {
            GUILayout.Label("표시 이름", label);
            nickname = GUILayout.TextField(nickname, 20, input, GUILayout.Height(32));
            if (Button("방 만들기", !string.IsNullOrWhiteSpace(nickname))) Admit(true);
            GUILayout.Label("공유 받은 방 코드", label);
            code = GUILayout.TextField(code, 10, input, GUILayout.Height(32));
            if (Button("방 참가", code.Trim().Length == 10 && !string.IsNullOrWhiteSpace(nickname))) Admit(false);
        }

        void DrawRoom()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("방 " + room, label);
            if (GUILayout.Button("코드 복사", button)) GUIUtility.systemCopyBuffer = room;
            if (GUILayout.Button("방 나가기", button)) { GUILayout.EndHorizontal(); LeaveRoom(); return; }
            GUILayout.EndHorizontal();
            if (view == null) { GUILayout.Label("참가자와 방 상태 불러오는 중", label); return; }
            GUILayout.Label("참가자  " + string.Join(" · ", view.members), label);
            string phase = view.phase == "night" ? "밤 · 보호할 학생 선택" : view.phase == "day" ? "낮 · AI 대화와 추리" : "최종 결과";
            GUILayout.Label(view.round + "라운드  |  " + phase, label);
            scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
            foreach (var line in view.messages)
                GUILayout.Label(line.speaker + "  " + line.text, label);
            GUILayout.EndScrollView();
            if (view.pending) GUILayout.Label("AI 대사를 생성하고 있습니다.", label);
            if (view.phase == "finished") GUILayout.Label(view.result + "  교수는 " + view.revealed_professor + "입니다.", label);
            else DrawActions();
        }

        void DrawActions()
        {
            bool canAct = !view.pending;
            if (view.phase == "day" || view.is_host)
            {
                GUILayout.BeginHorizontal();
                foreach (var target in view.alive)
                    if (GUILayout.Button(target == selected ? "[" + target + "]" : target, button)) selected = target;
                GUILayout.EndHorizontal();
            }
            if (view.phase == "night")
            {
                if (view.is_host && Button("선택한 학생 보호", canAct && selected.Length > 0)) Act("protect");
                else if (!view.is_host) GUILayout.Label("진행자가 보호할 학생을 선택 중입니다.", label);
            }
            else if (view.is_host)
            {
                GUILayout.BeginHorizontal();
                if (Button("다음 AI 발언", canAct && view.can_speak)) Act("speak");
                if (Button("다음 밤", canAct && view.round < 3)) Act("next_night");
                if (Button("교수 지목", canAct && selected.Length > 0)) Act("guess");
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            compose = GUILayout.TextField(compose, 160, input, GUILayout.Height(30));
            bool hasText = !string.IsNullOrWhiteSpace(compose);
            if (view.phase == "day" && Button("AI에게 질문", canAct && selected.Length > 0 && hasText)) Act("question");
            if (Button("참가자 채팅", hasText)) Act("chat");
            GUILayout.EndHorizontal();
        }

        bool Button(string text, bool available)
        {
            bool previous = GUI.enabled;
            GUI.enabled = previous && active == null && available;
            bool pressed = GUILayout.Button(text, button, GUILayout.Height(30));
            GUI.enabled = previous;
            return pressed;
        }
    }
}
