using System;
using System.Collections;
using System.IO;
using CampusCommons.GrandLibrary;
using UnityEngine;
using UnityEngine.Networking;

namespace CampusCommons.Quiz
{
    // Desktop demo: one PDF, one generation request, three locally graded questions.
    [RequireComponent(typeof(Player))]
    public class QuizRoom : MonoBehaviour
    {
        const int MaxBytes = 10 * 1024 * 1024;
        readonly QuizSession session = new QuizSession();
        Player player;
        UnityWebRequest request;
        bool entered, working, settingsOpen = true, browsing;
        string server = "", token = "", path = "", directory = "", message = "";
        string[] folders = new string[0], files = new string[0];
        int selected = -1;
        Vector2 scroll, fileScroll;
        GUIStyle heading, body, button, field, small;
        public bool BlocksMovement => enabled && player && player.currentSpace == Portal.Space.Quiz;

        void Awake()
        {
            player = GetComponent<Player>();
            server = PlayerPrefs.GetString("Quiz.Server", "");
            // The shared demo code is kept only in memory, never the Gemini API key.
            directory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrEmpty(directory)) directory = Application.persistentDataPath;
        }
        void Update()
        {
            if (BlocksMovement && !entered)
            {
                entered = true;
                if (QuizConnection.IsValidUrl(server, out _)) StartCoroutine(Warmup());
            }
            else if (!BlocksMovement && entered) ClearRoom();
        }
        void OnDisable() { ClearRoom(); }
        void ClearRoom()
        {
            StopAllCoroutines();
            if (request != null) { request.Abort(); request.Dispose(); request = null; }
            entered = working = browsing = false;
            session.Reset(); path = message = ""; selected = -1; scroll = Vector2.zero;
        }
        void CancelRequest()
        {
            StopAllCoroutines();
            if (request != null) { request.Abort(); request.Dispose(); request = null; }
            working = false;
            message = "요청을 취소했습니다. 이미 시작된 서버 생성은 한도를 사용할 수 있습니다.";
        }
        IEnumerator Warmup()
        {
            working = true; message = "서버에 연결 중… 무료 서버가 잠들어 있으면 약 1분이 걸릴 수 있습니다.";
            request = UnityWebRequest.Get(server.TrimEnd('/') + "/healthz");
            request.timeout = 90;
            yield return request.SendWebRequest();
            message = request.result == UnityWebRequest.Result.Success ? "연결되었습니다. PDF를 선택해 주세요." : "연결하지 못했습니다. 서버 주소와 배포 상태를 확인한 뒤 다시 연결해 주세요.";
            request.Dispose(); request = null; working = false;
        }
        void Generate()
        {
            if (!QuizConnection.IsValidUrl(server, out message)) { settingsOpen = true; return; }
            if (string.IsNullOrWhiteSpace(token)) { message = "관리자에게 받은 참가 코드를 입력해 주세요."; settingsOpen = true; return; }
            if (token.Length > 128 || Array.Exists(token.ToCharArray(), c => c < 33 || c > 126))
            { message = "참가 코드는 공백 없는 영문·숫자·기호 128자 이내로 입력해 주세요."; return; }
            byte[] pdf;
            try
            {
                if (!string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase)) throw new IOException("PDF 파일을 선택해 주세요.");
                using (var stream = File.OpenRead(path))
                {
                    if (stream.Length == 0 || stream.Length > MaxBytes) throw new IOException("PDF 파일 크기는 0보다 크고 10MB 이하여야 합니다.");
                    pdf = new byte[(int)stream.Length];
                    int read = 0;
                    while (read < pdf.Length)
                    {
                        int count = stream.Read(pdf, read, pdf.Length - read);
                        if (count == 0) throw new IOException("파일을 읽지 못했습니다. 다시 선택해 주세요.");
                        read += count;
                    }
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
            { message = "파일을 열 수 없습니다. PDF 경로·권한·크기(10MB 이하)를 확인해 주세요."; return; }
            PlayerPrefs.SetString("Quiz.Server", server); PlayerPrefs.Save();
            StartCoroutine(CreateQuiz(pdf));
        }
        IEnumerator CreateQuiz(byte[] pdf)
        {
            working = true; settingsOpen = browsing = false;
            message = "PDF를 읽고 레벨별 문제를 만들고 있어요. 최대 3분 정도 기다려 주세요.";
            request = new UnityWebRequest(server.TrimEnd('/') + "/v1/quizzes", "POST");
            request.uploadHandler = new UploadHandlerRaw(pdf);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/pdf");
            request.SetRequestHeader("X-Quiz-Token", token);
            request.timeout = 210;
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var data = JsonUtility.FromJson<QuizSet>(request.downloadHandler.text);
                    if (data == null || !data.IsValid(out _)) message = "문제 응답이 올바르지 않습니다. 다시 생성해 주세요.";
                    else { session.Begin(data); selected = -1; scroll = Vector2.zero; message = ""; }
                }
                catch (ArgumentException) { message = "문제 응답을 읽지 못했습니다. 다시 생성해 주세요."; }
            }
            else
            {
                message = "서버에 연결하지 못했거나 시간이 초과되었습니다. 잠시 후 다시 시도해 주세요.";
                try
                {
                    var error = JsonUtility.FromJson<QuizErrorEnvelope>(request.downloadHandler.text);
                    if (error != null && error.error != null && !string.IsNullOrWhiteSpace(error.error.message)) message = error.error.message;
                }
                catch (ArgumentException) { }
            }
            request.Dispose(); request = null; working = false;
        }
        void Browse(string location)
        {
            try
            {
                var dir = new DirectoryInfo(location);
                var dirs = dir.GetDirectories(); var entries = dir.GetFiles();
                Array.Sort(dirs, (a,b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name,b.Name));
                Array.Sort(entries, (a,b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name,b.Name));
                folders = Array.ConvertAll(dirs, d => d.FullName);
                files = Array.ConvertAll(Array.FindAll(entries, f => f.Extension.Equals(".pdf",StringComparison.OrdinalIgnoreCase)), f => f.FullName);
                directory = dir.FullName; browsing = true; fileScroll = Vector2.zero;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
            { message = "이 폴더에 접근할 수 없습니다. 다른 폴더나 PDF 전체 경로를 입력해 주세요."; }
        }
        void Styles()
        {
            if (body != null) return;
            var font = Resources.Load<Font>("KIT/NotoSansKR");
            body = new GUIStyle(GUI.skin.label) { font = font, fontSize = 19, wordWrap = true, richText = false };
            heading = new GUIStyle(body) { fontSize = 28, fontStyle = FontStyle.Bold };
            small = new GUIStyle(body) { fontSize = 15 };
            button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 18, wordWrap = true, richText = false, padding = new RectOffset(12,12,10,10) };
            field = new GUIStyle(GUI.skin.textField) { font = font, fontSize = 18 };
        }
        bool Button(string label) => GUILayout.Button(label, button, GUILayout.MinHeight(42));
        void OnGUI()
        {
            if (!BlocksMovement || player.IsExitConfirmOpen) return;
            Styles(); var matrix = GUI.matrix; var color = GUI.color;
            float scale = Mathf.Min(Screen.width / 1100f, Screen.height / 760f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width-1100*scale)/2,(Screen.height-760*scale)/2,0), Quaternion.identity, Vector3.one*scale);
            GUI.color = new Color(.055f,.09f,.12f,.98f); GUI.DrawTexture(new Rect(20,20,1060,720),Texture2D.whiteTexture); GUI.color = color;
            GUILayout.BeginArea(new Rect(45,35,1010,690));
            GUILayout.BeginHorizontal(); GUILayout.Label("PDF 퀴즈룸",heading);
            if (GUILayout.Button("나가기 · ESC",button,GUILayout.Width(170))) player.RequestRoomExit();
            GUILayout.EndHorizontal();
            GUILayout.Label("혼자 학습하기  ·  레벨 1 기초 → 레벨 2 적용 → 레벨 3 추론",small);
            scroll = GUILayout.BeginScrollView(scroll);
            if (session.Data == null) DrawSetup();
            else if (session.Finished) DrawResults();
            else DrawQuestion();
            GUILayout.Space(12);
            GUILayout.Label("AI가 만든 문제와 해설은 틀릴 수 있어요. 표시된 PDF 페이지와 함께 확인해 주세요.",small);
            GUILayout.EndScrollView(); GUILayout.EndArea(); GUI.matrix = matrix; GUI.color = color;
        }
        void DrawSetup()
        {
            GUI.enabled = !working;
            if (Button(settingsOpen ? "연결 설정 접기" : "연결 설정")) settingsOpen = !settingsOpen;
            if (settingsOpen)
            {
                GUILayout.Label("서버 주소 (관리자가 배포한 HTTPS 주소)",small);
                server = GUILayout.TextField(server,256,field).Trim();
                GUILayout.Label("참가 코드 (Gemini API 키를 입력하지 마세요)",small);
                token = GUILayout.PasswordField(token,'●',128,field);
                if (Button("서버 연결 확인"))
                {
                    if (QuizConnection.IsValidUrl(server,out message))
                    { PlayerPrefs.SetString("Quiz.Server",server); PlayerPrefs.Save(); StartCoroutine(Warmup()); }
                }
            }
            GUILayout.Space(12); GUILayout.Label("공부한 PDF 선택 · 최대 10MB / 80페이지",body);
            path = GUILayout.TextField(path,2048,field);
            if (Button("폴더에서 PDF 찾기")) Browse(directory);
            if (browsing) DrawBrowser();
            GUILayout.Label("문제 생성을 누르면 선택한 PDF가 서버와 Google Gemini에 전송됩니다. 공유 가능한 학습 자료를 사용해 주세요.",small);
            if (Button("레벨 1 · 2 · 3 문제 만들기")) Generate();
            GUI.enabled = true;
            if (!string.IsNullOrEmpty(message)) GUILayout.Label(message,body);
            if (working && Button("요청 취소")) CancelRequest();
        }
        void DrawBrowser()
        {
            GUILayout.BeginHorizontal();
            if (Button("내 문서")) Browse(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            if (Button("홈")) Browse(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            if (Button("상위 폴더")) { var parent = Directory.GetParent(directory); if (parent != null) Browse(parent.FullName); }
            if (Button("닫기")) browsing = false;
            GUILayout.EndHorizontal();
            GUILayout.Label(directory,small);
            fileScroll = GUILayout.BeginScrollView(fileScroll,GUILayout.Height(190));
            // Cap visible entries; a full path can always be pasted into the input above.
            for (int i=0;i<Math.Min(folders.Length,300);i++) if (Button("[폴더] " + Path.GetFileName(folders[i]))) { Browse(folders[i]); break; }
            for (int i=0;i<Math.Min(files.Length,300);i++) if (Button(Path.GetFileName(files[i]))) { path=files[i]; browsing=false; break; }
            if (files.Length==0) GUILayout.Label("이 폴더에는 PDF가 없습니다.",small);
            GUILayout.EndScrollView();
        }
        void DrawQuestion()
        {
            var q = session.Current;
            GUILayout.Space(16); GUILayout.Label("레벨 " + q.level + " / 3",heading);
            GUILayout.Label(q.prompt,body); GUILayout.Space(10);
            for (int i=0;i<4;i++)
            {
                GUI.enabled = !session.Submitted;
                if (Button((selected==i?"● ":"○ ")+(i+1)+". "+q.options[i])) selected=i;
            }
            GUI.enabled = !session.Submitted && selected>=0;
            if (Button("답 제출")) session.Submit(selected);
            GUI.enabled = true;
            if (session.Submitted)
            {
                GUILayout.Label(selected==q.correct_index?"정답입니다!":"아쉬워요. 해설을 확인해 보세요.",heading);
                Feedback(q);
                if (Button(q.level==3?"최종 결과 보기":"다음 레벨")) { session.Next(); selected=-1; scroll=Vector2.zero; }
            }
        }
        void Feedback(QuizQuestion q)
        {
            GUILayout.Label("정답: "+(q.correct_index+1)+". "+q.options[q.correct_index],body);
            GUILayout.Label("해설: "+q.explanation,body);
            GUILayout.Label("PDF "+q.source_page+"페이지 · “"+q.source_quote+"”",small);
        }
        void DrawResults()
        {
            GUILayout.Label("학습 완료 · "+session.Score+" / 3 정답",heading);
            for(int i=0;i<3;i++)
            {
                var q=session.Data.questions[i]; GUILayout.Space(14);
                GUILayout.Label("레벨 "+q.level+" · "+q.prompt,body);
                GUILayout.Label("내 답: "+(session.Answers[i]+1)+". "+q.options[session.Answers[i]],small);
                Feedback(q);
            }
            if(Button("새 PDF로 다시 학습하기")) { session.Reset(); path=message=""; selected=-1; scroll=Vector2.zero; }
        }
    }
}
