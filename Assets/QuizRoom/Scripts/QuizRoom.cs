using System;
using System.Collections;
using System.IO;
using CampusCommons.GrandLibrary;
using UnityEngine;
using UnityEngine.Networking;

namespace CampusCommons.Quiz
{
    [RequireComponent(typeof(Player))]
    public class QuizRoom : MonoBehaviour
    {
        enum WorkPhase { None, Connecting, Inspecting, Generating, Grading }
        readonly QuizSession session = new QuizSession();
        readonly QuizDocument document = new QuizDocument();
        Player player;
        UnityWebRequest request;
        bool entered, working, browsing, configured, gradingAttempted, focusAnswer;
        string typedAnswer="";
        string server="", token="", path="", directory="", message="";
        string[] folders=new string[0], files=new string[0];
        byte[] documentBytes;
        int selected=-1, browserPage;
        float startedAt;
        WorkPhase phase;
        Vector2 scroll,fileScroll;
        QuizTheme theme;
        public bool BlocksMovement => enabled && player && player.currentSpace==Portal.Space.Quiz;

        void Awake()
        {
            player=GetComponent<Player>();
            var config=Resources.Load<TextAsset>("QuizConnection");
            if(config) try {
                var deployment=JsonUtility.FromJson<QuizDeployment>(config.text);
                configured=deployment!=null && deployment.IsValid();
                if(configured) {server=deployment.server_url;token=deployment.access_token;}
            } catch(ArgumentException) {configured=false;}
            directory=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if(string.IsNullOrEmpty(directory)) directory=Application.persistentDataPath;
        }
        void Update()
        {
            if(BlocksMovement && !entered) {
                entered=true;
                if(configured && QuizConnection.IsValidUrl(server,out _)) StartCoroutine(Warmup());
                else message="퀴즈 서비스를 준비 중입니다. 잠시 후 다시 이용해 주세요.";
            } else if(!BlocksMovement && entered) ClearRoom();
            if(BlocksMovement && !player.IsExitConfirmOpen && session.Current!=null && session.Tick(Time.realtimeSinceStartupAsDouble,selected,typedAnswer)) ResetQuestionInput();
            if(BlocksMovement && session.NeedsGrading && !gradingAttempted && !working) StartCoroutine(GradeAnswers());
        }
        void OnDisable() {ClearRoom();}
        void OnDestroy() {if(theme!=null) theme.Dispose();}
        void StopRequest()
        {
            StopAllCoroutines();
            if(request!=null) {request.Abort();request.Dispose();request=null;}
            working=false;phase=WorkPhase.None;
        }
        void ClearRoom()
        {
            StopRequest();entered=browsing=false;
            session.Reset();document.Reset();documentBytes=null;path=message="";gradingAttempted=false;ResetQuestionInput();
        }
        void CancelRequest()
        {
            bool grading=phase==WorkPhase.Grading;
            bool connecting=phase==WorkPhase.Connecting;
            bool inspecting=phase==WorkPhase.Inspecting;
            StopRequest();
            if(inspecting) document.Fail("파일 확인을 취소했습니다. PDF를 다시 선택해 주세요.");
            message=grading?"채점을 취소했습니다. 결과 화면에서 다시 채점할 수 있습니다.":inspecting?"":connecting?"연결 준비를 취소했습니다. PDF를 선택해 다시 연결할 수 있습니다.":"생성을 취소했습니다. 다시 시작할 수 있습니다.";
        }
        void BeginWork(WorkPhase next) {phase=next;working=true;startedAt=Time.realtimeSinceStartup;message="";}
        void EndWork() {if(request!=null) {request.Dispose();request=null;}working=false;phase=WorkPhase.None;}
        IEnumerator Warmup()
        {
            BeginWork(WorkPhase.Connecting);
            request=UnityWebRequest.Get(server.TrimEnd('/')+"/healthz");request.timeout=90;
            yield return request.SendWebRequest();
            if(request.result!=UnityWebRequest.Result.Success) message="연결이 지연되고 있습니다. PDF를 선택해 다시 시도해 주세요.";
            EndWork();
        }
        void SelectPdf(string chosen)
        {
            StopRequest();documentBytes=null;message="";path=chosen;document.Select(chosen,0);directory=Path.GetDirectoryName(chosen);browsing=false;
            try {
                if(!Path.GetExtension(path).Equals(".pdf",StringComparison.OrdinalIgnoreCase)) throw new IOException();
                using(var stream=File.OpenRead(path)) {
                    document.Select(path,stream.Length);
                    if(!document.CanInspect) return;
                    documentBytes=new byte[(int)stream.Length];
                    int read=0;
                    while(read<documentBytes.Length) {
                        int count=stream.Read(documentBytes,read,documentBytes.Length-read);
                        if(count==0) throw new IOException();read+=count;
                    }
                }
            } catch(Exception e) when(e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException) {
                document.Fail("PDF를 열지 못했습니다. 파일과 접근 권한을 확인해 주세요.");documentBytes=null;return;
            }
            if(!configured) {document.Fail("퀴즈 서비스에 연결할 수 없습니다. 잠시 후 다시 선택해 주세요.");return;}
            StartCoroutine(InspectPdf());
        }
        UnityWebRequest PdfRequest(string endpoint)
        {
            var next=new UnityWebRequest(server.TrimEnd('/')+endpoint,"POST");
            next.uploadHandler=new UploadHandlerRaw(documentBytes);next.downloadHandler=new DownloadHandlerBuffer();
            next.SetRequestHeader("Content-Type","application/pdf");next.SetRequestHeader("X-Quiz-Token",token);
            next.timeout=210;return next;
        }
        IEnumerator InspectPdf()
        {
            BeginWork(WorkPhase.Inspecting);request=PdfRequest("/v1/pdfs/inspect");
            yield return request.SendWebRequest();
            if(request.result==UnityWebRequest.Result.Success) {
                try {document.ApplyInspection(JsonUtility.FromJson<QuizPdfInspection>(request.downloadHandler.text));}
                catch(ArgumentException) {document.Fail("파일 정보를 읽지 못했습니다. PDF를 다시 선택해 주세요.");}
            } else document.Fail(RequestError("파일 정보를 확인하지 못했습니다. PDF를 다시 선택해 주세요."));
            EndWork();
        }
        void Generate()
        {
            if(!configured || working || !document.CanGenerate || documentBytes==null) return;
            StartCoroutine(CreateQuiz());
        }
        IEnumerator CreateQuiz()
        {
            BeginWork(WorkPhase.Generating);browsing=false;request=PdfRequest("/v1/quizzes?start_page="+document.StartPage+"&end_page="+document.EndPage);
            yield return request.SendWebRequest();
            if(request.result==UnityWebRequest.Result.Success) {
                try {
                    var data=JsonUtility.FromJson<QuizSet>(request.downloadHandler.text);
                    if(data==null || !data.IsValid(out _) || data.page_range==null || data.page_range.start_page!=document.StartPage || data.page_range.end_page!=document.EndPage || data.page_range.total_pages!=document.Pages) message="문제 응답이 올바르지 않습니다. 다시 생성해 주세요.";
                    else {session.Begin(data,Time.realtimeSinceStartupAsDouble);gradingAttempted=false;ResetQuestionInput();}
                } catch(ArgumentException) {message="문제 응답을 읽지 못했습니다. 다시 생성해 주세요.";}
            } else message=RequestError("서버에 연결하지 못했거나 시간이 초과되었습니다. 다시 시도해 주세요.");
            EndWork();
        }
        void ResetQuestionInput() {selected=-1;typedAnswer="";focusAnswer=false;scroll=Vector2.zero;}
        IEnumerator GradeAnswers()
        {
            gradingAttempted=true;BeginWork(WorkPhase.Grading);
            var payload=System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(session.GradingRequest()));
            request=new UnityWebRequest(server.TrimEnd('/')+"/v1/quizzes/grade","POST");
            request.uploadHandler=new UploadHandlerRaw(payload);request.downloadHandler=new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type","application/json");request.SetRequestHeader("X-Quiz-Token",token);request.timeout=60;
            yield return request.SendWebRequest();
            if(request.result==UnityWebRequest.Result.Success) {
                try {if(!session.ApplyGrades(JsonUtility.FromJson<QuizGradeSet>(request.downloadHandler.text))) message="채점 결과를 확인하지 못했습니다. 다시 채점해 주세요.";}
                catch(ArgumentException) {message="채점 결과를 읽지 못했습니다. 다시 채점해 주세요.";}
            } else message=RequestError("채점에 연결하지 못했습니다. 다시 채점해 주세요.");
            EndWork();
        }
        string RequestError(string fallback)
        {
            try {
                var error=JsonUtility.FromJson<QuizErrorEnvelope>(request.downloadHandler.text);
                if(error?.error!=null && !string.IsNullOrWhiteSpace(error.error.message))
                    return error.error.code=="unauthorized" || error.error.code=="server_configuration"
                        ?"퀴즈 서비스에 연결할 수 없습니다. 잠시 후 다시 이용해 주세요.":QuizText.Display(error.error.message);
            } catch(ArgumentException) { }
            return fallback;
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
                message="이 폴더에 접근할 수 없습니다. 홈이나 다른 드라이브를 선택해 주세요.";
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
        void OnGUI()
        {
            if(!BlocksMovement || player.IsExitConfirmOpen) return;
            if(theme==null) theme=new QuizTheme();
            var matrix=GUI.matrix;var color=GUI.color;var skin=GUI.skin;bool enabledBefore=GUI.enabled;GUI.skin=theme.Skin;
            float scale=Mathf.Min(Screen.width/1180f,Screen.height/820f);
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1180*scale)/2,(Screen.height-820*scale)/2,0),Quaternion.identity,Vector3.one*scale);
            GUI.color=new Color(0,0,0,.22f);GUI.DrawTexture(new Rect(-4000,-4000,9000,9000),Texture2D.whiteTexture);GUI.color=Color.white;
            GUI.Box(new Rect(30,47,1120,740),GUIContent.none,theme.Shadow);
            GUI.Box(new Rect(30,40,1120,740),GUIContent.none,theme.Panel);
            GUILayout.BeginArea(new Rect(70,68,1040,60));
            GUILayout.BeginHorizontal();GUILayout.BeginVertical();
            Label("PDF 퀴즈룸",theme.Title);GUILayout.Space(4);Label("나의 자료로 완성하는 짧고 깊은 복습",theme.Small);
            GUILayout.EndVertical();GUILayout.FlexibleSpace();
            if(Button("나가기  ·  ESC",theme.Quiet,GUILayout.Width(175),GUILayout.Height(44))) player.RequestRoomExit();
            GUILayout.EndHorizontal();GUILayout.EndArea();
            GUI.color=new Color(.91f,.93f,.95f);GUI.DrawTexture(new Rect(70,144,1040,1),Texture2D.whiteTexture);GUI.color=Color.white;
            GUILayout.BeginArea(new Rect(70,165,1040,575));
            DrawSteps();GUILayout.Space(28);
            scroll=GUILayout.BeginScrollView(scroll,GUIStyle.none,GUI.skin.verticalScrollbar);
            if(session.Data==null) DrawSetup();else if(session.Finished) DrawResults();else DrawQuestion();
            GUILayout.Space(8);GUILayout.EndScrollView();GUILayout.EndArea();
            GUI.enabled=enabledBefore;GUI.color=color;GUI.matrix=matrix;GUI.skin=skin;
        }
        void DrawSteps()
        {
            int active=session.Data==null?0:session.Finished?2:1;
            GUILayout.BeginHorizontal();
            var labels=new[]{"01   자료 선택","02   문제 풀기","03   복습 완료"};
            for(int i=0;i<3;i++) {
                Label(labels[i],i==active?theme.ActiveStep:theme.Step,GUILayout.Width(190));
                if(i<2) Label("—",theme.Caption,GUILayout.Width(40));
            }
            GUILayout.EndHorizontal();
        }
        void DrawSetup()
        {
            GUILayout.BeginHorizontal();GUILayout.BeginVertical(GUILayout.Width(690));
            bool hasFile=document.Path.Length>0;
            Label(hasFile?"출제할 범위를 골라주세요.":"읽은 내용을, 내 지식으로.",hasFile?theme.Title:theme.Heading);GUILayout.Space(8);
            Label(hasFile?"PDF 파일의 첫 장을 1페이지로 셉니다.":"학습한 PDF로 세 단계의 짧은 퀴즈에 도전하세요.",theme.Small);GUILayout.Space(18);
            GUILayout.BeginVertical(theme.Card);
            GUILayout.BeginHorizontal();Label("학습 자료",theme.Caption);GUILayout.FlexibleSpace();Label("PDF",theme.Caption);GUILayout.EndHorizontal();
            GUILayout.Space(12);
            if(!hasFile) {
                Label("어떤 자료로 복습할까요?",theme.FileName);GUILayout.Space(8);
                Label("PDF 한 개 · 최대 10 MB · 원하는 페이지 범위 선택",theme.Small);GUILayout.Space(18);
            } else {
                Label(document.Name,theme.Body);GUILayout.Space(14);
                GUILayout.BeginHorizontal();
                Label(document.SizeLabel+" / 10 MB",document.Bytes>QuizDocument.MaxBytes?theme.Warning:theme.Small,GUILayout.Width(280));
                Label(document.Pages>0?"전체 "+document.Pages+"페이지":phase==WorkPhase.Inspecting?"페이지 확인 중…":"페이지 확인 전",theme.Small);
                GUILayout.EndHorizontal();
                if(document.Inspected) {
                    GUILayout.Space(18);
                    if(!working) {
                        Label("문제를 만들 페이지 범위",theme.Caption);GUILayout.Space(8);
                        GUILayout.BeginHorizontal();
                        string first=GUILayout.TextField(document.StartText,10,theme.Input,GUILayout.Width(116),GUILayout.Height(46));
                        Label("부터",theme.Small,GUILayout.Width(40));
                        string last=GUILayout.TextField(document.EndText,10,theme.Input,GUILayout.Width(116),GUILayout.Height(46));
                        Label("페이지까지",theme.Small,GUILayout.Width(95));
                        document.SetRange(first,last);
                        if(Button("전체 선택",theme.Quiet,GUILayout.Height(46))) document.SetRange("1",document.Pages.ToString());
                        GUILayout.EndHorizontal();
                    }
                    if(document.SelectedPages>0) {GUILayout.Space(8);Label(document.StartPage+"~"+document.EndPage+"페이지 · "+document.SelectedPages+"페이지 선택",theme.Small);}
                }
                GUILayout.Space(16);
            }
            if(!working || phase==WorkPhase.Connecting) {
                if(Button(hasFile?"다른 PDF 선택하기":"PDF 선택하기",theme.Secondary,GUILayout.Height(44))) ChoosePdf();
            }
            GUILayout.EndVertical();
            if(document.Warning.Length>0) {GUILayout.Space(12);Notice(document.Warning);}
            if(working) {GUILayout.Space(16);DrawActivity();}
            else {
                if(message.Length>0) {GUILayout.Space(12);Notice(message);}
                GUILayout.Space(22);GUI.enabled=configured && document.CanGenerate;
                if(Button("문제 만들기  →",theme.Primary,GUILayout.Height(52))) Generate();GUI.enabled=true;
            }
            if(browsing) {GUILayout.Space(16);DrawBrowser();}
            GUILayout.EndVertical();GUILayout.Space(36);
            GUILayout.BeginVertical(theme.Card,GUILayout.Width(280));
            Label("나만의 복습 루틴",theme.Caption);GUILayout.Space(24);
            Guide("1","객관식 · 10초","보기에서 알맞은 답을 선택해요.");GUILayout.Space(22);
            Guide("2","단답형 · 15초","핵심 답을 짧게 입력해요.");GUILayout.Space(22);
            Guide("3","서술형 · 20초","생각을 한두 문장으로 적어요.");
            GUILayout.EndVertical();GUILayout.EndHorizontal();
        }
        void Guide(string number,string title,string detail)
        {
            Label(number+"   "+title,theme.Body);GUILayout.Space(6);Label(detail,theme.Small);
        }
        void Notice(string text)
        {
            GUILayout.BeginVertical(theme.ErrorCard);Label(text,theme.Warning);GUILayout.EndVertical();
        }
        void DrawActivity()
        {
            GUILayout.BeginVertical(theme.Card);
            GUILayout.BeginHorizontal();var spinner=GUILayoutUtility.GetRect(48,48,GUILayout.Width(48));
            theme.Spinner(spinner,Time.realtimeSinceStartup);GUILayout.Space(14);GUILayout.BeginVertical();
            bool uploading=phase==WorkPhase.Generating && request!=null && request.uploadProgress>=0 && request.uploadProgress<1;
            string title=phase==WorkPhase.Connecting?"학습을 준비하고 있어요":phase==WorkPhase.Inspecting?"파일 정보를 확인하고 있어요":phase==WorkPhase.Grading?"답의 의미를 비교해 채점하고 있어요":uploading?"학습 자료를 업로드하고 있어요":"문제를 만들고 있어요";
            Label(title,theme.Body);GUILayout.Space(5);
            int seconds=(int)(Time.realtimeSinceStartup-startedAt);
            string detail=phase==WorkPhase.Connecting?"첫 연결은 약 1분이 걸릴 수 있어요.":phase==WorkPhase.Inspecting?"페이지 수와 파일 상태를 검사합니다.":phase==WorkPhase.Grading?"단답형 · 서술형 답안을 확인합니다.":"선택한 범위에서 세 단계의 문제를 구성합니다.";
            Label((seconds/60).ToString("00")+":"+(seconds%60).ToString("00")+" 경과  ·  "+detail,theme.Small);
            GUILayout.EndVertical();GUILayout.EndHorizontal();GUILayout.Space(16);
            var track=GUILayoutUtility.GetRect(10,4,GUILayout.ExpandWidth(true));
            GUI.color=new Color(.86f,.89f,.92f);GUI.DrawTexture(track,Texture2D.whiteTexture);GUI.color=QuizTheme.Navy;
            if(uploading) GUI.DrawTexture(new Rect(track.x,track.y,track.width*Mathf.Clamp01(request.uploadProgress),4),Texture2D.whiteTexture);
            else {float width=track.width*.23f;float x=(track.width-width)*(.5f+.5f*Mathf.Sin(Time.realtimeSinceStartup*2));GUI.DrawTexture(new Rect(track.x+x,track.y,width,4),Texture2D.whiteTexture);}
            GUI.color=Color.white;GUILayout.Space(14);
            if(Button("요청 취소",theme.Quiet,GUILayout.Height(36))) CancelRequest();
            GUILayout.EndVertical();
        }
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
        void DrawQuestion()
        {
            var q=session.Current;if(q==null) return;
            double remaining=session.Remaining(Time.realtimeSinceStartupAsDouble);
            GUILayout.BeginHorizontal();GUILayout.BeginVertical();
            Label(q.level+"단계 · "+new[]{"객관식","단답형","서술형"}[q.level-1],theme.Caption);GUILayout.Space(7);
            Label("제한 "+session.TimeLimit+"초 · 10점 만점",theme.Small);GUILayout.EndVertical();
            GUILayout.FlexibleSpace();var clock=GUILayoutUtility.GetRect(82,80,GUILayout.Width(82),GUILayout.Height(80));
            theme.Countdown(clock,remaining,session.TimeLimit);GUILayout.EndHorizontal();
            GUILayout.Space(8);var track=GUILayoutUtility.GetRect(10,5,GUILayout.ExpandWidth(true));
            GUI.color=new Color(.90f,.92f,.94f);GUI.DrawTexture(track,Texture2D.whiteTexture);
            GUI.color=remaining<=3?new Color(.77f,.25f,.20f):QuizTheme.Navy;
            GUI.DrawTexture(new Rect(track.x,track.y,track.width*(float)(remaining/session.TimeLimit),5),Texture2D.whiteTexture);GUI.color=Color.white;
            GUILayout.Space(24);Label(q.prompt,theme.Title);GUILayout.Space(22);
            if(q.level==1) {
                for(int i=0;i<4;i++) {
                    if(Button((i+1).ToString("00")+"     "+q.options[i],selected==i?theme.SelectedChoice:theme.Choice,GUILayout.MinHeight(54))) selected=i;
                    GUILayout.Space(9);
                }
                GUILayout.Space(12);GUI.enabled=selected>=0;
                if(Button("제출하고 다음으로  →",theme.Primary,GUILayout.Height(50)) && session.SubmitChoice(selected,Time.realtimeSinceStartupAsDouble)) ResetQuestionInput();
                GUI.enabled=true;
            } else {
                Label(q.level==2?"핵심 용어나 짧은 문장으로 답해 주세요.":"생각을 한두 문장으로 설명해 주세요.",theme.Small);GUILayout.Space(12);
                string name="quiz-answer-"+q.level;GUI.SetNextControlName(name);
                typedAnswer=q.level==2?GUILayout.TextField(typedAnswer,300,theme.Input,GUILayout.Height(58)):
                    GUILayout.TextArea(typedAnswer,1000,theme.Input,GUILayout.Height(132));
                if(!focusAnswer) {GUI.FocusControl(name);focusAnswer=true;}
                GUILayout.Space(12);GUI.enabled=!string.IsNullOrWhiteSpace(typedAnswer);
                if(Button(q.level==3?"제출하고 결과 보기  →":"제출하고 다음으로  →",theme.Primary,GUILayout.Height(50)) && session.SubmitText(typedAnswer,q.level,Time.realtimeSinceStartupAsDouble)) ResetQuestionInput();
                GUI.enabled=true;
            }
            GUILayout.Space(14);Label("시간이 끝나면 현재 답이 자동 제출됩니다. 정답과 해설은 마지막에 확인해요.",theme.Small);
        }
        void DrawResults()
        {
            Label("복습을 마쳤어요.",theme.Heading);GUILayout.Space(8);
            Label(session.NeedsGrading?(working?"단답형 · 서술형 채점 중":"단답형 · 서술형 채점 대기"):"총 "+session.Score+" / 30점",theme.Positive);GUILayout.Space(6);
            Label("총 풀이 시간 "+session.TotalElapsed.ToString("0.0")+"초",theme.Small);GUILayout.Space(16);
            if(working && phase==WorkPhase.Grading) {DrawActivity();GUILayout.Space(16);}
            else if(session.NeedsGrading) {
                if(message.Length>0) {Notice(message);GUILayout.Space(10);}
                if(Button("AI 채점 다시하기",theme.Secondary,GUILayout.Height(44))) StartCoroutine(GradeAnswers());GUILayout.Space(16);
            }
            for(int i=0;i<3;i++) {
                var q=session.Data.questions[i];GUILayout.BeginVertical(theme.Card);
                string verdict=session.Points[i].HasValue?session.Points[i]+" / 10점":"채점 대기";
                Label((i+1)+"단계 · "+new[]{"객관식","단답형","서술형"}[i]+" · "+verdict+(session.TimedOut[i]?" · 시간 종료":""),theme.Caption);GUILayout.Space(10);
                Label("내 답  "+(session.Responses[i].Length==0?"미응답":session.Responses[i]),theme.Small);GUILayout.Space(8);
                Label("정답  "+q.model_answer,theme.Body);GUILayout.Space(8);
                Label(q.explanation,theme.Small);GUILayout.Space(8);
                Label("풀이 "+session.ElapsedSeconds[i].ToString("0.0")+"초 / 제한 "+new[]{10,15,20}[i]+"초  ·  PDF "+q.source_page+"페이지",theme.Caption);
                if(i>0 && session.CriterionScores[i]!=null) {
                    var breakdown=new string[q.grading_criteria.Length];
                    for(int j=0;j<breakdown.Length;j++) breakdown[j]=q.grading_criteria[j].description+" "+session.CriterionScores[i][j]+"/"+q.grading_criteria[j].max_points+"점";
                    GUILayout.Space(7);Label(string.Join(" · ",breakdown),theme.Small);
                }
                if(session.GradingFeedback[i].Length>0 && session.Responses[i].Length>0) {GUILayout.Space(6);Label(session.GradingFeedback[i],theme.Small);}
                GUILayout.EndVertical();GUILayout.Space(12);
            }
            if(Button("새 PDF로 다시 학습하기",theme.Primary,GUILayout.Height(50))) {
                StopRequest();session.Reset();document.Reset();documentBytes=null;path=message="";gradingAttempted=false;ResetQuestionInput();
            }
        }
    }
}
