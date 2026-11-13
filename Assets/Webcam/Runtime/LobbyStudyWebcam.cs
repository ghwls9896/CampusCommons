using System;
using System.Collections;
using CampusCommons.GrandLibrary;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace CampusCommons.Webcam
{
    public sealed class LobbyStudyWebcam : MonoBehaviour
    {
        readonly StudySession session=new StudySession();
        Seat[] seats=Array.Empty<Seat>(); Player player; Seat activeSeat;
        LocalStudyProcess local; WebCamTexture cameraTexture; UnityWebRequest request;
        bool previewAttempted; bool focused=true; string message=""; float scanAt;
        GUIStyle text,button; Font font;
        public bool IsAvailable {get;private set;}
        public bool IsRunning=>session.Running;
        public bool PreviewReady=>cameraTexture && cameraTexture.isPlaying && cameraTexture.width>16;
        public string Status=>message.Length>0?message:session.Label;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if(FindAnyObjectByType<LobbyStudyWebcam>())return;
            var go=new GameObject("Local study webcam");DontDestroyOnLoad(go);go.AddComponent<LobbyStudyWebcam>();
        }
        void Awake(){focused=Application.isFocused;SceneManager.sceneLoaded+=SceneLoaded;Scan();}
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
            if(IsAvailable && focused && Input.GetKeyDown(KeyCode.F8))ToggleStudy();
        }
        void LateUpdate()
        {
            if(IsAvailable && focused){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        }
        void OpenPreview()
        {
            previewAttempted=true;StartCoroutine(PreparePreview());
        }
        public void ToggleStudy(){if(IsRunning)EndStudy();else StartStudy();}
        public void StartStudy()
        {
            if(!StudyRuntimeBootstrap.Ready){message="공부 인식 준비가 끝나면 시작할 수 있습니다.";return;}
            if(!PreviewReady || !session.Begin(IsAvailable,focused))return;
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
            session.Stop();StopAllCoroutines();
            if(request!=null){request.Abort();request.Dispose();request=null;}
            if(cameraTexture){cameraTexture.Stop();Destroy(cameraTexture);cameraTexture=null;}
            local?.Dispose();local=null;message="";
        }
        void Fail(string value){StopStudy();previewAttempted=true;message=value;}
        IEnumerator PreparePreview()
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
            message="시작을 누르면 공부 상태를 분석합니다.";
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
                    try{var config=JsonUtility.FromJson<Config>(request.downloadHandler.text);ready=config.mode=="fast" && config.interval_ms==1000;}
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
                float began=Time.realtimeSinceStartup;byte[] bytes;
                try{bytes=WebcamFrameEncoder.Encode(cameraTexture);}
                catch(Exception){Fail("카메라 프레임을 읽지 못했습니다.");yield break;}
                request=new UnityWebRequest(local.Origin+"/api/infer","POST");
                request.uploadHandler=new UploadHandlerRaw(bytes);request.downloadHandler=new DownloadHandlerBuffer();request.timeout=30;
                request.SetRequestHeader("Content-Type","image/jpeg");request.SetRequestHeader("Origin",local.Origin);
                request.SetRequestHeader("X-Session-Token",local.Token);
                yield return request.SendWebRequest();
                string state=null;
                if(request.result==UnityWebRequest.Result.Success){
                    try{state=DecisionState(request.downloadHandler.text);}catch(Exception){state=null;}
                }
                request.Dispose();request=null;
                if(state==null){Fail("분석 오류입니다. 다시 시작해 주세요.");yield break;}
                if(!session.Apply(epoch,state))yield break;
                message="";
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
        [Serializable] class Config{public string mode;public int interval_ms;}
        [Serializable] class Scores{public float studying,phone,other,away,drowsy;}
        [Serializable] class Decision{public bool ok;public string state,mode;public Scores probabilities;}
        void OnApplicationFocus(bool value){if(focused==value)return;focused=value;if(!value)StopStudy();else previewAttempted=false;}
        void OnApplicationPause(bool value){if(value)StopStudy();else previewAttempted=false;}
        void OnDisable(){EndStudy();}
        void OnDestroy(){SceneManager.sceneLoaded-=SceneLoaded;StopStudy();}
        void OnApplicationQuit(){StopStudy();}
        void OnGUI()
        {
            if(!IsAvailable)return;
            if(text==null){
                font=Resources.Load<Font>("KIT/NotoSansKR");
                text=new GUIStyle(GUI.skin.label){font=font,fontSize=18,wordWrap=true};
                button=new GUIStyle(GUI.skin.button){font=font,fontSize=18};
            }
            var old=GUI.matrix;GUI.matrix=Matrix4x4.identity;
            var rect=new Rect(Mathf.Max(8,Screen.width-320),Mathf.Max(80,Screen.height-435),304,415);
            GUI.Box(rect,GUIContent.none);GUILayout.BeginArea(new Rect(rect.x+14,rect.y+12,276,390));
            GUILayout.Label("공부 인식",text);GUILayout.Space(8);
            var preview=GUILayoutUtility.GetRect(276,146);
            if(PreviewReady)
            {
                var matrix=GUI.matrix;
                GUIUtility.RotateAroundPivot(-cameraTexture.videoRotationAngle,preview.center);
                if(cameraTexture.videoVerticallyMirrored)GUIUtility.ScaleAroundPivot(new Vector2(1,-1),preview.center);
                GUI.DrawTexture(preview,cameraTexture,ScaleMode.ScaleToFit);
                GUI.matrix=matrix;
            }
            else GUI.Label(preview,"카메라 미리보기",text);
            GUILayout.Label(Status,text,GUILayout.Height(60));
            GUILayout.BeginHorizontal();
            if(!session.Running)
            {
                GUI.enabled=PreviewReady && StudyRuntimeBootstrap.Ready;
                if(GUILayout.Button("시작 (F8)",button,GUILayout.Height(38)))StartStudy();
                GUI.enabled=true;
                if(!PreviewReady && GUILayout.Button("재시도",button,GUILayout.Height(38))){StopStudy();OpenPreview();}
            }
            if(GUILayout.Button("종료 · 일어나기",button,GUILayout.Height(38)))EndStudy();
            GUILayout.EndHorizontal();
            if(StudyRuntimeBootstrap.Instance && !StudyRuntimeBootstrap.Instance.Preparing && !session.Running && GUILayout.Button("공부 인식 다시 준비",button)) StudyRuntimeBootstrap.Instance.Retry();
            GUILayout.Space(10);GUILayout.Label("영상 저장 없음 · 이 PC에서만 처리\n종료하면 카메라를 끄고 일어납니다.",text);
            GUILayout.EndArea();GUI.matrix=old;
        }
    }
}
