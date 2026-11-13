using System;
using System.Linq;
using CampusCommons.GrandLibrary;
using System.Threading;
using System.Threading.Tasks;
using CampusCommons.Account;
using CampusCommons.Webcam;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class LocalIntegrationAudit
{
 const string Key="LocalIntegrationAudit";
 static int liveStage; static Seat liveSeat; static double liveAt; static bool begun; static double deadline;
 sealed class Fixture:IAccountTransport {
  public Task<T> SendAsync<T>(string method,string path,object body,string token,CancellationToken ct) where T:class {
   object value=null;
   if(typeof(T)==typeof(AuthResponse))value=new AuthResponse{access_token="fixture",refresh_token="fixture",expires_at=4000000000,user=new AuthUser{id="fixture-user",email_confirmed_at="date"}};
   if(typeof(T)==typeof(AccountProfile))value=new AccountProfile{user_id="fixture-user",nickname=(body as ProfileRequest)?.nickname??"통합검사",level=1};
   return Task.FromResult((T)value);
  }
 }
 public static void OpenForUser(){SessionState.EraseString(Key);EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");EditorApplication.EnterPlaymode();}
 public static void Live(){SessionState.SetString(Key,"live");EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");EditorApplication.EnterPlaymode();}
 public static void Quiz(){SessionState.SetString(Key,"quiz");CampusCommons.Quiz.Editor.QuizRoomTest.Run();}
 public static void Webcam(){SessionState.SetString(Key,"webcam");WebcamLobbyVerification.Run();}
 public static void World(){SessionState.SetString(Key,"world");EscapeWorldIntegrationVerification.Run();}
 public static void Locked(){SessionState.SetString(Key,"locked");EscapeNpcLockedRouteVerification.Run();}
 public static void Open(){SessionState.SetString(Key,"open");EscapeOpenDoorRouteVerification.Run();}
 public static void Lobby(){SessionState.SetString(Key,"lobby");EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");CampusCommons.LibraryCharacter.Editor.CafeLobbyValidation.Begin();}
 [InitializeOnLoadMethod]static void Resume(){EditorApplication.delayCall+=()=>{if(SessionState.GetString(Key,"")=="")return;begun=false;deadline=EditorApplication.timeSinceStartup+100;EditorApplication.update+=Tick;};}
 static void LiveTick(LobbyStudyWebcam study){
  try{
   if(!AccountGate.Instance.Ready || !study || Time.timeSinceLevelLoad<2)return;
   var player=UnityEngine.Object.FindAnyObjectByType<Player>();
   if(liveStage==0){
    liveSeat=UnityEngine.Object.FindObjectsByType<Seat>().First(x=>!x.occupant && Mathf.Abs(x.transform.position.x)<30);
    var body=player.GetComponent<CharacterController>();body.enabled=false;player.transform.position=liveSeat.transform.position-Vector3.up*.6f;body.enabled=true;
    if(!player.TrySit(liveSeat))throw new Exception("LIVE_SEAT_FAILED");liveStage=1;liveAt=EditorApplication.timeSinceStartup;return;
   }
   if(liveStage==1){
    if(study.IsRunning)throw new Exception("Inference auto-started on seating");
    if(study.PreviewReady){Debug.Log("LIVE_PREVIEW_READY_NO_INFERENCE");study.StartStudy();liveStage=2;liveAt=EditorApplication.timeSinceStartup;}
    else if(EditorApplication.timeSinceStartup-liveAt>40)throw new Exception("LIVE_CAMERA_NOT_READY: "+study.Status);
    return;
   }
   if(liveStage==2){
    var labels=new[]{"공부중","자는중","자리비움","휴대폰 사용중","다른활동중"};
    if(labels.Contains(study.Status)){
     var local=(LocalStudyProcess)typeof(LobbyStudyWebcam).GetField("local",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(study);
     study.EndStudy();if(study.IsRunning||study.PreviewReady||liveSeat.occupant||local.Alive)throw new Exception("LIVE_EXIT_CLEANUP_FAILED");
     Debug.Log("LIVE_WEBCAM_QEV_PASS: preview, manual inference, valid five-class response, camera/process shutdown and standing; no frames saved");SessionState.EraseString(Key);EditorApplication.Exit(0);
    }else if(!study.IsRunning)throw new Exception("LIVE_INFERENCE_STOPPED: "+study.Status);
    else if(EditorApplication.timeSinceStartup-liveAt>80)throw new Exception("LIVE_INFERENCE_TIMEOUT");
   }
  }catch(Exception e){if(study)study.EndStudy();Debug.LogException(e);SessionState.EraseString(Key);EditorApplication.Exit(1);}
 }
 static async void Tick(){
  if(EditorApplication.timeSinceStartup>deadline){Debug.LogError("INTEGRATION_TIMEOUT");SessionState.EraseString(Key);EditorApplication.Exit(1);return;}
  if(!EditorApplication.isPlaying){
   if(begun && SessionState.GetString(Key,"")=="lobby"){
    string r=System.IO.File.ReadAllText("Tools/cafe-lobby-validation.txt");Debug.Log(r);SessionState.EraseString(Key);EditorApplication.Exit(r.StartsWith("PASS:")?0:1);
   }return;
  }
  var study=UnityEngine.Object.FindAnyObjectByType<LobbyStudyWebcam>();if(study)study.SendMessage("OnApplicationFocus",SessionState.GetString(Key,"")=="live");
  if(begun && SessionState.GetString(Key,"")=="live"){LiveTick(study);return;}
  if(begun)return;begun=true;
  try{var gate=AccountGate.Instance;gate.Configure(new Fixture());await gate.LoginAsync("audit@example.test","fixture-password");await gate.EnterAsync("통합검사");Debug.Log("INTEGRATION_FIXTURE_AUTH_READY");}
  catch(Exception e){Debug.LogException(e);SessionState.EraseString(Key);EditorApplication.Exit(1);}
 }
}
