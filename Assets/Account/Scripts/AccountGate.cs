using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace CampusCommons.Account
{
 [DefaultExecutionOrder(-32000)]
 public sealed class AccountGate : MonoBehaviour
 {
  public enum Screen { Login,Signup,SignupCode,RecoveryEmail,RecoveryCode,NewPassword,Nickname,World }
  public static AccountGate Instance {get;private set;}
  public AccountSession Session {get;private set;}
  public Screen Page {get;set;}=Screen.Login;
  public bool Ready {get;private set;}
  public bool Busy {get;private set;}
  public string Message {get;private set;}="";
  public bool Configured {get;private set;}
  public AccountWorldGate World {get;private set;}
  float refreshAt;int operation;
  public event Action<AccountProfile> EntryReady;
  public event Action EntryRevoked;
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatic(){Instance=null;}
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Install()
  {
   if(Instance)return;new GameObject("Campus account").AddComponent<AccountGate>();
  }
  void Awake()
  {
   if(Instance&&Instance!=this){Destroy(gameObject);return;}Instance=this;DontDestroyOnLoad(gameObject);
   World=gameObject.AddComponent<AccountWorldGate>();World.Gate=this;
   var config=Resources.Load<TextAsset>("AccountConnection");AccountConnection connection=null;try{if(config)connection=JsonUtility.FromJson<AccountConnection>(config.text);}catch{}
   connection=connection??new AccountConnection();Configured=connection.IsValid();SetSession(new UnityAccountTransport(this,connection));
   gameObject.AddComponent<AccountView>();SceneManager.sceneLoaded+=SceneLoaded;
  }
  void SceneLoaded(Scene scene,LoadSceneMode mode){Revoke();Page=Session.IsAuthenticated?Screen.Nickname:Screen.Login;World.Capture();}
  void SetSession(IAccountTransport transport)
  {
   if(Session!=null){Session.Changed-=SessionChanged;Session.Dispose();}Session=new AccountSession(transport);Session.Changed+=SessionChanged;
  }
  // Dependency injection for editor fixtures and future platform transports, not an auth bypass.
  public void Configure(IAccountTransport transport){Revoke();SetSession(transport);Configured=true;Page=Screen.Login;}
  void SessionChanged(){if(!Session.IsAuthenticated&&Ready){Revoke();Page=Screen.Login;}}
  public void Revoke(){bool wasReady=Ready;Ready=false;if(World)World.Block();if(wasReady)EntryRevoked?.Invoke();}
  public async Task LoginAsync(string email,string password){Revoke();await Session.SignInAsync(email,password);Page=Screen.Nickname;await Session.ReadProfileAsync();}
  public async Task SignupAsync(string email,string password){Revoke();await Session.SignUpAsync(email,password);Page=Screen.SignupCode;Message="메일의 확인 코드를 입력해 주세요.";}
  public async Task VerifySignupAsync(string email,string code){await Session.VerifySignupCodeAsync(email,code);Page=Screen.Nickname;await Session.ReadProfileAsync();}
  public async Task RecoverAsync(string email){Revoke();await Session.BeginPasswordRecoveryAsync(email);Page=Screen.RecoveryCode;Message="등록된 이메일이라면 재설정 코드가 전송됩니다.";}
  public async Task VerifyRecoveryAsync(string email,string code){await Session.VerifyRecoveryCodeAsync(email,code);Page=Screen.NewPassword;}
  public async Task ResetPasswordAsync(string password){await Session.UpdatePasswordAsync(password);Page=Screen.Login;Message="비밀번호를 변경했습니다. 다시 로그인해 주세요.";}
  public async Task EnterAsync(string nickname)
  {
   var profile=await Session.EnterAsync(nickname);Ready=true;Page=Screen.World;World.Release();EntryReady?.Invoke(profile);
  }
  public async Task LogoutAsync(){operation++;Busy=false;Revoke();Page=Screen.Login;Message="로그아웃했습니다.";await Session.SignOutAsync();}
  public async void Perform(Func<Task> action)
  {
   if(Busy)return;int id=++operation;Busy=true;Message="";
   try{await action();}
   catch(OperationCanceledException){}
   catch(AccountException e){if(id==operation){Message=Describe(e);if(!Session.IsAuthenticated&&!Session.RecoveryPending&&Page==Screen.Nickname)Page=Screen.Login;}}
   catch(Exception){if(id==operation)Message="요청을 완료하지 못했습니다. 연결 상태를 확인하고 다시 시도해 주세요.";}
   finally{if(id==operation)Busy=false;}
  }
  async void Update()
  {
   if(Ready&&Session.TokenExpired){Revoke();Page=Screen.Nickname;Message="로그인 유효 시간이 지났습니다. 연결을 확인하고 내 정보를 다시 불러오거나 다시 로그인해 주세요.";}
   if(!Ready||Busy||Time.unscaledTime<refreshAt)return;refreshAt=Time.unscaledTime+20;
   try{await Session.EnsureFreshAsync();}catch(AccountException e){Message=Describe(e);if(!Session.IsAuthenticated){Revoke();Page=Screen.Login;}}catch(OperationCanceledException){}
   catch{Message="서버 연결을 확인해 주세요.";}
  }
  public static string Describe(AccountException e)
  {
   switch(e.Code)
   {
    case "configuration_required":return "Supabase 연결 설정이 필요합니다. 설정 안내를 확인해 주세요.";
    case "invalid_email":return "올바른 이메일 주소를 입력해 주세요.";
    case "password_mismatch":return "비밀번호 확인 값이 일치하지 않습니다.";
    case "weak_password":return "비밀번호는 12~128자로 입력해 주세요.";
    case "invalid_nickname":return "닉네임은 2~12자로 입력해 주세요. 줄바꿈·태그·제어문자는 사용할 수 없습니다.";
    case "invalid_code":case "otp_expired":return "확인 코드가 올바르지 않거나 만료됐습니다. 코드를 다시 확인하거나 재전송해 주세요.";
    case "email_not_confirmed":return "이메일 확인 후 로그인해 주세요. 확인 코드 재전송을 이용할 수 있습니다.";
    case "invalid_credentials":return "이메일 또는 비밀번호가 맞지 않습니다.";
    case "over_email_send_rate_limit":case "over_request_rate_limit":return "요청이 너무 많습니다. 잠시 후 다시 시도해 주세요.";
    case "invalid_profile":return "사용자 정보를 확인하지 못했습니다. 다시 불러와 주세요.";
   }
   if(e.Status==429)return "요청 한도에 도달했습니다. 잠시 후 다시 시도해 주세요.";
   if(e.Status==401||e.Status==403)return "로그인 상태를 확인하지 못했습니다. 다시 로그인해 주세요.";
   return "서버에 연결하지 못했습니다. 연결 상태와 Supabase 설정을 확인해 주세요.";
  }
  void OnDestroy(){SceneManager.sceneLoaded-=SceneLoaded;if(Session!=null){Session.Changed-=SessionChanged;Session.Dispose();}if(Instance==this)Instance=null;}
 }
}
