using System;
using System.Threading;
using System.Threading.Tasks;
using CampusCommons.GrandLibrary;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace CampusCommons.Account.Editor
{
 // Real Unity lifecycle with a deterministic HTTP fixture; not a live Supabase test.
 public static class AccountFlowTests
 {
  sealed class Fixture : IAccountTransport
  {
   public bool RefreshFails;
   public async Task<T> SendAsync<T>(string method,string path,object body,string token,CancellationToken ct) where T:class
   {
    if(path.Contains("refresh_token")&&RefreshFails)throw new AccountException(503,"connection_failed");
    if(typeof(T)==typeof(AccountProfile)&&body is ReadRequest)await Task.Delay(250);
    object value=null;
    if(typeof(T)==typeof(AuthResponse))value=new AuthResponse{access_token="fixture",refresh_token="fixture",expires_at=4000000000,user=new AuthUser{id="fixture-user",email_confirmed_at="date"}};
    if(typeof(T)==typeof(AccountProfile))value=new AccountProfile{user_id="fixture-user",nickname=(body as ProfileRequest)?.nickname??"이전닉네임",total_xp=150,level=2,next_level_total_xp=300};
    return (T)value;
   }
  }
  static int step;static double deadline;static Task task;static Fixture fixture;static AccountGate gate;static Player player;static bool runtimeError;static float enteredAt,profileReadyAt;
  public static void Run()
  {EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");SessionState.SetBool("AccountFlowTest",true);EditorApplication.EnterPlaymode();}
  [InitializeOnLoadMethod] static void Resume()
  {EditorApplication.delayCall+=()=>{if(!SessionState.GetBool("AccountFlowTest",false))return;step=0;profileReadyAt=-1;deadline=EditorApplication.timeSinceStartup+90;runtimeError=false;Application.logMessageReceived+=Log;EditorApplication.update+=Tick;};}
  static void Log(string m,string stack,LogType type){if(stack.Contains("UnityEditor.Search.SearchDatabase"))return;if(type==LogType.Exception||type==LogType.Error)runtimeError=true;}
  static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
  static void Tick()
  {
   try
   {
    Check(EditorApplication.timeSinceStartup<deadline,"Account flow timed out");if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<2)return;
    if(step==0){gate=UnityEngine.Object.FindAnyObjectByType<AccountGate>();player=UnityEngine.Object.FindAnyObjectByType<Player>();Check(gate&&player,"bootstrap missing");Check(!player.enabled&&!player.GetComponent<CharacterController>().enabled,"pre-auth player input enabled");fixture=new Fixture();gate.Configure(fixture);task=gate.LoginAsync("a@example.test","password12345");step=1;return;}
    if(task!=null&&!task.IsCompleted)return;if(task!=null&&task.IsFaulted)throw task.Exception;
    if(step==1){if(profileReadyAt<0){profileReadyAt=Time.time;return;}if(Time.time-profileReadyAt<.3f)return;Check((string)typeof(AccountView).GetField("nickname",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(gate.GetComponent<AccountView>())=="이전닉네임","async profile did not prefill previous nickname");Check(!gate.Ready&&gate.Session.Profile.total_xp==150,"must choose nickname before entry");task=gate.EnterAsync("새닉네임");enteredAt=Time.time;step=2;return;}
    if(step==2){if(Time.time-enteredAt<1)return;Check(player.transform.Find("Student character")&&Camera.main,"player did not start after login");Check(gate.Ready&&player.enabled&&player.GetComponent<CharacterController>().enabled,"entry failed");Check(gate.Session.Profile.nickname=="새닉네임"&&gate.Session.Profile.total_xp==150,"nickname reset progress");fixture.RefreshFails=true;typeof(AccountSession).GetField("expires",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(gate.Session,1L);enteredAt=Time.time;step=3;return;}
    if(step==3){if(Time.time-enteredAt<.5f)return;Check(!gate.Ready&&!player.enabled,"expired token failed to block world");task=gate.LogoutAsync();step=4;return;}
    if(step==4){Check(!gate.Ready&&!player.enabled&&gate.Session.Profile==null,"logout retained world/data");Check(Cursor.visible&&Cursor.lockState==CursorLockMode.None,"blocked cursor");Check(!runtimeError,"runtime errors during account flow");Debug.Log("ACCOUNT_FLOW_PASS");Finish(0);}
   }catch(Exception e){Debug.LogException(e);Finish(1);}
  }
  static void Finish(int code){SessionState.SetBool("AccountFlowTest",false);Application.logMessageReceived-=Log;EditorApplication.update-=Tick;EditorApplication.Exit(code);}
 }
}
