using System;
using System.Collections;
using CampusCommons.Account;
using CampusCommons.GrandLibrary;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace CampusCommons.Webcam
{
    public sealed class LobbyStudyWebcam : MonoBehaviour
    {
        public static LobbyStudyWebcam Instance { get; private set; }

        readonly StudySession session=new StudySession();
        Seat[] seats=Array.Empty<Seat>(); Player player; Seat activeSeat;
        LocalStudyProcess local; WebCamTexture cameraTexture; UnityWebRequest request;
        StudyWindowPicker studyWindow;
        bool previewAttempted, openingPreview; bool focused=true; string message="", failure; float scanAt;
        GUIStyle timerText, noticeText, button;
        public bool IsAvailable {get;private set;}
        public bool IsRunning=>session.Running;
        public bool PreviewReady=>cameraTexture && cameraTexture.isPlaying && cameraTexture.width>16;
        public string Status=>failure ?? (message.Length>0?message:session.Label);
        internal bool ShowsSeatHud => IsAvailable && !AccountGate.LobbyOverlayOwnsInput;
        bool CanStartFromSeat => IsAvailable && focused && !IsRunning && !openingPreview && StudyRuntimeBootstrap.Ready;

        internal string StatusFor(Player character) =>
            IsAvailable && IsRunning && character && character == player ? Status : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetInstance() { Instance = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(FindAnyObjectByType<LobbyStudyWebcam>())return;
            var go=new GameObject("Local study webcam");DontDestroyOnLoad(go);go.AddComponent<LobbyStudyWebcam>();
        }
        void Awake()
        {
            Instance = this;
            Application.runInBackground=true;focused=Application.isFocused;SceneManager.sceneLoaded+=SceneLoaded;Scan();
        }
        void SceneLoaded(Scene scene,LoadSceneMode mode){StopStudy();Scan();}
        void Scan(){player=FindAnyObjectByType<Player>();seats=FindObjectsByType<Seat>(FindObjectsSortMode.None);}
        void Update()
        {
            if(Time.unscaledTime>=scanAt){scanAt=Time.unscaledTime+1;Scan();}
            Seat seated=null;
            if(player && player.isActiveAndEnabled && player.currentSpace==Portal.Space.Library)
                foreach(var seat in seats)if(seat && seat.occupant==player){seated=seat;break;}
            if(seated!=activeSeat)
            {
                // Account revocation disables Player before this scan. Release its old seat too.
                if(activeSeat && activeSeat.occupant==player && !seated)ReleaseSeat();
                StopStudy();activeSeat=seated;previewAttempted=false;
            }
            IsAvailable=seated;
            if(IsAvailable && focused && !previewAttempted)OpenPreview();
            if(IsAvailable && focused && !AccountGate.LobbyOverlayOwnsInput && Input.GetKeyDown(KeyCode.F8))ToggleStudy();
        }
        void LateUpdate()
        {
            if(IsAvailable && focused){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        }
        void OpenPreview(bool startAfterPreview = false)
        {
            previewAttempted = openingPreview = true;
            failure = null;
            StartCoroutine(PreparePreview(startAfterPreview));
        }
        public void ToggleStudy(){if(IsRunning)EndStudy();else StartFromSeat();}
        void StartFromSeat()
        {
            if (!CanStartFromSeat) return;
            if (PreviewReady) StartStudy();
            else OpenPreview(startAfterPreview: true);
        }
        public void StartStudy()
        {
            if(!StudyRuntimeBootstrap.Ready){failure="공부 인식 준비가 끝나면 시작할 수 있습니다.";return;}
            if(!PreviewReady || session.Running || !IsAvailable || !focused)return;
            var gate = AccountGate.Instance;
            if (!gate || !gate.Ready || !gate.Session.IsAuthenticated) { failure="공부를 시작하려면 로그인해 주세요."; return; }
            var records = StudyRecordService.Instance;
            if (!records || !records.Begin()) { failure=records ? records.SyncStatus : "공부 기록을 준비하고 있습니다. 다시 시도해 주세요."; return; }
            if(!session.Begin(IsAvailable,focused)){StudyRecordService.Instance.Finish();return;}
            failure = null;
            message="모델 준비 중";StartCoroutine(Run(session.Epoch));
        }
        void ReleaseSeat()
        {
            if(!player || !activeSeat || activeSeat.occupant!=player)return;
            bool blocked=!player.isActiveAndEnabled;
            player.Stand();
            if(blocked){var body=player.GetComponent<CharacterController>();if(body)body.enabled=false;}
        }
        public void EndStudy()
        {
            StopStudy();ReleaseSeat();activeSeat=null;IsAvailable=false;previewAttempted=false;
        }
        public void StopStudy()
        {
            session.Stop();StudyRecordService.Instance?.Finish();StopAllCoroutines();
            studyWindow?.Dispose();
            studyWindow = null;
            openingPreview = false;
            if(request!=null){request.Abort();request.Dispose();request=null;}
            if(cameraTexture){cameraTexture.Stop();Destroy(cameraTexture);cameraTexture=null;}
            local?.Dispose();local=null;message="";failure=null;
        }
        void Fail(string value){StopStudy();previewAttempted=true;failure=value;}
        IEnumerator PreparePreview(bool startAfterPreview)
        {
            message="카메라 권한 확인 중";
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            if(!Application.HasUserAuthorization(UserAuthorization.WebCam)){Fail("카메라 권한이 필요합니다. 시스템 설정에서 허용해 주세요.");yield break;}
            if(WebCamTexture.devices.Length==0){Fail("사용할 수 있는 카메라가 없습니다.");yield break;}
            try{cameraTexture=new WebCamTexture(WebCamTexture.devices[0].name,1280,720,15);cameraTexture.Play();}
            catch(Exception){Fail("카메라를 켤 수 없습니다. 다른 앱의 카메라 사용을 확인해 주세요.");yield break;}
            float deadline=Time.realtimeSinceStartup+15;message="카메라 준비 중";
            while(cameraTexture.width<=16 || !cameraTexture.didUpdateThisFrame){
                if(Time.realtimeSinceStartup>deadline){Fail("카메라 영상을 받지 못했습니다.");yield break;}
                yield return null;
            }
            openingPreview = false;
            message = "";
            if (startAfterPreview) StartStudy();
        }
        IEnumerator Run(int epoch)
        {
            try{local=new LocalStudyProcess();local.Start();}
            catch(Exception e){Fail(e is System.IO.FileNotFoundException?e.Message:"로컬 모델을 실행하지 못했습니다. 설치 상태를 확인해 주세요.");yield break;}
            float deadline=Time.realtimeSinceStartup+90;
            bool ready=false;
            while(session.Running && Time.realtimeSinceStartup<deadline)
            {
                if(!local.Alive){Fail("모델 실행이 종료되었습니다. 설치 상태를 확인해 주세요.");yield break;}
                request=UnityWebRequest.Get(local.Origin+"/api/config");request.timeout=2;
                yield return request.SendWebRequest();
                if(request.result==UnityWebRequest.Result.Success){
                    try
                    {
                        var config = JsonUtility.FromJson<Config>(request.downloadHandler.text);
                        ready = config.mode == "fast" && config.interval_ms == 1000;
                        if (ready && !config.combined_study)
                        {
                            request.Dispose();
                            request = null;
                            Fail("공부 인식 파일을 새 버전으로 준비해 주세요.");
                            yield break;
                        }
                        if (ready && config.window_capture) studyWindow = new StudyWindowPicker(this, local);
                    }
                    catch(Exception){ready=false;}
                }
                request.Dispose();request=null;
                if(ready)break;
                yield return new WaitForSecondsRealtime(.3f);
            }
            if(!ready){Fail("모델 준비 시간이 초과됐습니다. 다시 시작해 주세요.");yield break;}
            session.FrameSeen(Time.realtimeSinceStartup);
            message="분석 중";
            while(session.Running && session.Epoch==epoch)
            {
                if(!cameraTexture.isPlaying){Fail("카메라 연결이 끊겼습니다. 다시 시작해 주세요.");yield break;}
                if(!cameraTexture.didUpdateThisFrame){
                    if(session.CameraStalled(Time.realtimeSinceStartup)){Fail("카메라 영상을 받지 못했습니다. 다시 시작해 주세요.");yield break;}
                    yield return null;continue;
                }
                session.FrameSeen(Time.realtimeSinceStartup);
                double captured=Time.realtimeSinceStartupAsDouble;float began=Time.realtimeSinceStartup;byte[] bytes;
                try{bytes=WebcamFrameEncoder.Encode(cameraTexture);}
                catch(Exception){Fail("카메라 프레임을 읽지 못했습니다.");yield break;}
                request=new UnityWebRequest(local.Origin+"/api/study-frame","POST");
                request.uploadHandler=new UploadHandlerRaw(bytes);request.downloadHandler=new DownloadHandlerBuffer();request.timeout=30;
                request.SetRequestHeader("Content-Type","image/jpeg");request.SetRequestHeader("Origin",local.Origin);
                request.SetRequestHeader("X-Session-Token",local.Token);
                yield return request.SendWebRequest();
                string state=null;Decision decision=null;
                if(request.result==UnityWebRequest.Result.Success){
                    try{state=DecisionState(request.downloadHandler.text);decision=JsonUtility.FromJson<Decision>(request.downloadHandler.text);}catch(Exception){state=null;}
                }
                request.Dispose();request=null;
                var records = StudyRecordService.Instance;
                if (decision != null && decision.ok && decision.mode == "fast" &&
                    !decision.observed && decision.screen == "unknown" && decision.probabilities == null)
                {
                    records.BreakObservation();
                    message = decision.reason == "camera_unreadable" ? "카메라 영상 확인 필요" :
                        studyWindow == null ? "공부 창 확인 불가" : "화면 확인 필요";
                }
                else
                {
                    if (state == null || decision == null || !decision.observed)
                    { Fail("분석 오류입니다. 다시 시작해 주세요."); yield break; }
                    var scores = decision.probabilities;
                    records.Observe(captured, new double[] { scores.studying, scores.phone, scores.other, scores.away, scores.drowsy });
                    string stable = records.Tracker?.Label;
                    if (stable != null) { if (!session.Apply(epoch, stable)) yield break; message = ""; }
                    else message = "분석 중";
                }
                float wait=1-(Time.realtimeSinceStartup-began);
                if(wait>0)yield return new WaitForSecondsRealtime(wait);
            }
        }
        public static string DecisionState(string json)
        {
            var r=JsonUtility.FromJson<Decision>(json);
            if(r==null||!r.ok||r.mode!="fast"||r.probabilities==null||StudySession.LabelFor(r.state)==null)return null;
            var p=r.probabilities;float[] values={p.studying,p.phone,p.other,p.away,p.drowsy};
            float sum=0,max=0;foreach(float v in values){if(float.IsNaN(v)||float.IsInfinity(v)||v<0||v>1)return null;sum+=v;max=Mathf.Max(max,v);}
            int index=Array.IndexOf(new[]{"studying","phone","other","away","drowsy"},r.state);
            if(Mathf.Abs(sum-1)>.001f||values[index]<max)return null;
            return r.state;
        }
        [Serializable] class Config{public string mode;public int interval_ms;public bool combined_study,window_capture;}
        [Serializable] class Scores{public float studying,phone,other,away,drowsy;}
        [Serializable] class Decision{public bool ok,observed;public string state,mode,screen,reason;public Scores probabilities;}
        void OnApplicationFocus(bool value){focused=value;}
        void OnApplicationPause(bool value){if(value)StopStudy();else previewAttempted=false;}
        void OnDisable(){EndStudy();}
        void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            StopStudy();
            if (Instance == this) Instance = null;
        }
        void OnApplicationQuit(){StopStudy();}
        void OnGUI()
        {
            if (!ShowsSeatHud) return;
            if (button == null)
            {
                var font = Resources.Load<Font>("KIT/NotoSansKR");
                timerText = new GUIStyle(GUI.skin.label)
                {
                    font = font, fontSize = 17, fontStyle = FontStyle.Bold, wordWrap = true,
                    alignment = TextAnchor.MiddleCenter, padding = new RectOffset(10, 10, 4, 4)
                };
                noticeText = new GUIStyle(timerText) { fontSize = 15, fontStyle = FontStyle.Normal };
                button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 17 };
            }

            var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            DrawStudyTimers();
            DrawSeatControls();
            studyWindow?.Draw(button, noticeText);
            GUI.matrix = matrix;
        }

        void DrawStudyTimers()
        {
            var records = StudyRecordService.Instance;
            long elapsed = IsRunning ? records?.SessionMilliseconds ?? 0 : 0;
            string current = "이번 공부  " + StudyTime.Clock(elapsed);
            string today = "오늘 누적  " + StudyTime.Clock(records?.TodayMilliseconds ?? 0);
            var content = new GUIContent(current + "     " + today);
            float space = Screen.width - 24;
            if (timerText.CalcSize(content).x > space) content.text = current + "\n" + today;
            float width = Mathf.Min(Mathf.Ceil(timerText.CalcSize(content).x) + 10, space);
            float height = Mathf.Max(38, Mathf.Ceil(timerText.CalcHeight(content, width)) + 4);
            var rect = new Rect((Screen.width - width) / 2, 12, width, height);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(rect, content, timerText);

            string notice = failure ?? (player && player.IsFirstPerson ? StatusFor(player) : null);
            if (notice == null) return;
            content.text = notice;
            width = Mathf.Min(460, space);
            height = Mathf.Max(28, Mathf.Ceil(noticeText.CalcHeight(content, width)));
            var note = new Rect((Screen.width - width) / 2, rect.yMax + 6, width, height);
            GUI.Box(note, GUIContent.none);
            GUI.Label(note, content, noticeText);
        }

        void DrawSeatControls()
        {
            const float margin = 12, gap = 8, buttonHeight = 36;
            float width = Mathf.Min(288, Screen.width - margin * 2);
            float height = Mathf.Min(width * 9 / 16, Screen.height * .35f);
            float x = Screen.width - width - margin;
            float buttonY = Screen.height - margin - buttonHeight;
            var preview = new Rect(x, buttonY - gap - height, width, height);
            GUI.DrawTexture(preview, Texture2D.blackTexture);
            if (PreviewReady) DrawPreview(preview);

            float buttonWidth = (width - gap) / 2;
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && CanStartFromSeat;
            if (GUI.Button(new Rect(x, buttonY, buttonWidth, buttonHeight), "시작", button)) StartFromSeat();
            GUI.enabled = enabled;
            if (GUI.Button(new Rect(x + buttonWidth + gap, buttonY, buttonWidth, buttonHeight), "일어나기", button))
                EndStudy();
        }

        void DrawPreview(Rect bounds)
        {
            GUI.BeginGroup(bounds);
            var matrix = GUI.matrix;
            var rect = new Rect(0, 0, bounds.width, bounds.height);
            int angle = cameraTexture.videoRotationAngle;
            GUIUtility.RotateAroundPivot(-angle, rect.center);
            if (cameraTexture.videoVerticallyMirrored)
                GUIUtility.ScaleAroundPivot(new Vector2(1, -1), rect.center);
            if (angle % 180 != 0)
            {
                Vector2 center = rect.center;
                rect.size = new Vector2(bounds.height, bounds.width);
                rect.center = center;
            }
            GUI.DrawTexture(rect, cameraTexture, ScaleMode.ScaleToFit);
            GUI.matrix = matrix;
            GUI.EndGroup();
        }
    }
}
