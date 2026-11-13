using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CampusCommons.Account;
public static class SessionTests
{
 static int checks;
 static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
 sealed class Fake : IAccountTransport, IDisposable
 {
  public bool Disposed; public void Dispose(){Disposed=true;}
  public readonly List<string> Paths=new List<string>();
  public Func<string,object,Task<object>> Reply;
  public async Task<T> SendAsync<T>(string method,string path,object body,string token,CancellationToken ct) where T:class
  { Paths.Add(method+" "+path);return (T)await Reply(path,body); }
 }
 static AuthResponse Auth(string id="a",long expiry=4000000000) {return new AuthResponse{access_token="access-"+id,refresh_token="refresh-"+id,expires_at=expiry,user=new AuthUser{id=id,email="a@example.test",email_confirmed_at="date"}};}
 static AccountProfile Profile(string id="a",int level=1){return new AccountProfile{user_id=id,nickname="닉네임",level=level};}
 public static int Main(){Run().GetAwaiter().GetResult();Console.WriteLine("ACCOUNT_SESSION_PASS "+checks+" checks");return 0;}
 static async Task Run()
 {
  var f=new Fake(); f.Reply=(p,b)=>Task.FromResult<object>(Auth());var s=new AccountSession(f);
  Check(!s.IsAuthenticated,"starts signed out");await s.SignInAsync(" a@example.test ","password12345");Check(s.IsAuthenticated && s.UserId=="a","sign in");
  f.Reply=(p,b)=>Task.FromResult<object>(Profile());await s.ReadProfileAsync();Check(s.Profile.level==1,"load persistent profile");
  await s.EnterAsync("새닉네임");Check(s.Profile.level==1,"enter preserves level");
  f.Reply=(p,b)=>Task.FromResult<object>(null);await s.SignOutAsync();Check(!s.IsAuthenticated && s.Profile==null,"logout clears local state");
  var pending=new TaskCompletionSource<object>();f.Reply=(p,b)=>pending.Task;
  var old=s.SignInAsync("a@example.test","password12345");s.Clear();
  f.Reply=(p,b)=>Task.FromResult<object>(Auth("b"));await s.SignInAsync("b@example.test","password12345");pending.SetResult(Auth());
  try{await old;Check(false,"old login must cancel");}catch(OperationCanceledException){Check(s.UserId=="b","late A cannot replace B");}
  var stale=new TaskCompletionSource<object>();f.Reply=(p,b)=>stale.Task;var read=s.ReadProfileAsync();s.Clear();stale.SetResult(Profile("b"));
  try{await read;Check(false,"stale profile must cancel");}catch(OperationCanceledException){Check(s.Profile==null,"late profile discarded");}
  f.Reply=(p,b)=>Task.FromResult<object>(new AuthResponse{user=new AuthUser{id="a"}});await s.SignUpAsync("a@example.test","password12345");Check(!s.IsAuthenticated,"unconfirmed signup never enters");
  f.Reply=(p,b)=>Task.FromResult<object>(Auth());await s.VerifySignupCodeAsync("a@example.test","123456");Check(s.IsAuthenticated,"signup OTP creates session");
  await s.VerifyRecoveryCodeAsync("a@example.test","123456");Check(!s.IsAuthenticated && s.RecoveryPending,"recovery restricted until password update");
  f.Reply=(p,b)=>Task.FromResult<object>(p.Contains("logout")?null:new AuthUser{id="a",email_confirmed_at="date"});await s.UpdatePasswordAsync("newpassword123");Check(!s.RecoveryPending && !s.IsAuthenticated,"password reset requires fresh login");
  f.Reply=(p,b)=>Task.FromResult<object>(Auth("a",1));await s.SignInAsync("a@example.test","password12345");
  var refresh=new TaskCompletionSource<object>();var refreshCount=0;f.Reply=(p,b)=>{refreshCount++;return refresh.Task;};
  var r1=s.EnsureFreshAsync();var r2=s.EnsureFreshAsync();refresh.SetResult(Auth());await Task.WhenAll(r1,r2);Check(refreshCount==1,"single refresh during concurrent callers");
  f.Reply=(p,b)=>Task.FromResult<object>(Profile("other"));try{await s.ReadProfileAsync();Check(false,"mismatched profile");}catch(AccountException){Check(s.Profile==null,"mismatched response rejected");}
  f.Reply=(p,b)=>Task.FromResult<object>(Auth("a",1));await s.SignInAsync("a@example.test","password12345");
  f.Reply=(p,b)=>{throw new AccountException(401,"invalid_token");};try{await s.EnsureFreshAsync();}catch(AccountException){}Check(!s.IsAuthenticated,"terminal refresh error clears auth");
  f.Reply=(p,b)=>Task.FromResult<object>(Auth("a",DateTimeOffset.UtcNow.ToUnixTimeSeconds()+30));await s.SignInAsync("a@example.test","password12345");
  f.Reply=(p,b)=>{throw new AccountException(503,"connection_failed");};try{await s.EnsureFreshAsync();}catch(AccountException){}Check(s.IsAuthenticated,"transient failure before expiry retains valid auth");
  f.Reply=(p,b)=>Task.FromResult<object>(Auth("a",1));await s.SignInAsync("a@example.test","password12345");
  f.Reply=(p,b)=>{throw new AccountException(503,"connection_failed");};try{await s.EnsureFreshAsync();}catch(AccountException){}Check(!s.IsAuthenticated,"expired offline token cannot authorize world");
  foreach(int level in new[]{1,5,10,15}) {
   f.Reply=(p,b)=>Task.FromResult<object>(Auth());await s.SignInAsync("a@example.test","password12345");
   f.Reply=(p,b)=>Task.FromResult<object>(Profile("a",level));await s.ReadProfileAsync();await s.EnterAsync("변경닉네임");
   Check(s.Profile.level==level,"level-only response and nickname preserve level "+level);
  }
  foreach(int level in new[]{0,-1,16,100}) {
   f.Reply=(p,b)=>Task.FromResult<object>(Auth());await s.SignInAsync("a@example.test","password12345");
   f.Reply=(p,b)=>Task.FromResult<object>(Profile("a",level));
   try{await s.ReadProfileAsync();Check(false,"invalid level accepted");}catch(AccountException e){Check(e.Code=="invalid_profile"&&s.Profile==null,"invalid level rejected");}
  }
  f.Reply=(p,b)=>Task.FromResult<object>(Auth("b"));await s.SignInAsync("b@example.test","password12345");
  f.Reply=(p,b)=>Task.FromResult<object>(Profile("b",1));await s.ReadProfileAsync();Check(s.Profile.user_id=="b"&&s.Profile.level==1,"B reads only B level");
  f.Reply=(p,b)=>Task.FromResult<object>(Auth());await s.SignInAsync("a@example.test","password12345");
  f.Reply=(p,b)=>Task.FromResult<object>(Profile("a",15));await s.ReadProfileAsync();Check(s.Profile.level==15,"A level restored after re-login");
  var unconfirmed=Auth();unconfirmed.user.email_confirmed_at=null;f.Reply=(p,b)=>Task.FromResult<object>(unconfirmed);
  try{await s.SignInAsync("a@example.test","password12345");Check(false,"unconfirmed login accepted");}catch(AccountException){Check(!s.HasSession,"unconfirmed login blocked");}
  foreach(var code in new[]{"12345","1234567","1234567890","12a456"}){
   int count=f.Paths.Count;try{await s.VerifySignupCodeAsync("a@example.test",code);Check(false,"invalid code accepted");}catch(AccountException){Check(f.Paths.Count==count,"invalid code never sent");}
  }
  f.Reply=(p,b)=>{throw new AccountException(403,"otp_expired");};
  try{await s.VerifySignupCodeAsync("a@example.test","123456");Check(false,"expired signup OTP accepted");}catch(AccountException){Check(!s.HasSession,"failed signup OTP blocked");}
  try{await s.VerifyRecoveryCodeAsync("a@example.test","123456");Check(false,"expired recovery OTP accepted");}catch(AccountException){Check(!s.HasSession,"failed recovery OTP blocked");}
  f.Reply=(p,b)=>Task.FromResult<object>(Auth());await s.VerifyRecoveryCodeAsync("a@example.test","123456");int before=f.Paths.Count;
  try{await s.EnterAsync("닉네임");Check(false,"recovery entered world");}catch(AccountException){Check(f.Paths.Count==before,"recovery cannot call account API");}
  f.Reply=(p,b)=>{throw new AccountException(503,"connection_failed");};
  try{await s.UpdatePasswordAsync("newpassword123");Check(false,"failed password update succeeded");}catch(AccountException){Check(s.RecoveryPending&&!s.IsAuthenticated,"failed reset remains restricted");}
  s.Dispose();Check(f.Disposed,"session disposal owns transport cleanup");
  Check(AccountRules.Nickname("  가나다  ")=="가나다","nickname NFC trim");
  foreach(var name in new[]{"a","<b>닉</b>","닉\n네임","닉\u202E네임",new string('가',13)}){try{AccountRules.Nickname(name);Check(false,"invalid nickname");}catch(AccountException){Check(true,"nickname rejected");}}
 }
}
