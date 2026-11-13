using System;
using System.Threading;
using System.Threading.Tasks;
namespace CampusCommons.Account
{
 // Tokens only live in this object; never persisted or logged.
 public sealed class AccountSession : IDisposable
 {
  readonly IAccountTransport transport;
  CancellationTokenSource lifetime=new CancellationTokenSource();
  int generation;string access,refresh;long expires;
  Task refreshTask;
  public AuthUser User {get;private set;}
  public string UserId=>User?.id;
  public AccountProfile Profile {get;private set;}
  public bool RecoveryPending {get;private set;}
  public bool HasSession=>User!=null&&!string.IsNullOrEmpty(access);
  public bool TokenExpired=>HasSession&&expires<=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
  public bool IsAuthenticated=>HasSession&&!RecoveryPending&&!TokenExpired;
  public event Action Changed;
  public AccountSession(IAccountTransport transport){this.transport=transport;}
  public void Clear()
  {
   generation++;lifetime.Cancel();lifetime.Dispose();lifetime=new CancellationTokenSource();
   access=refresh=null;expires=0;User=null;Profile=null;RecoveryPending=false;refreshTask=null;Changed?.Invoke();
  }
  void Current(int expected,CancellationToken ct){ct.ThrowIfCancellationRequested();if(generation!=expected)throw new OperationCanceledException();}
  void Accept(AuthResponse response,bool recovery=false)
  {
   if(response?.user==null||response.user.is_anonymous||string.IsNullOrEmpty(response.user.email_confirmed_at)||string.IsNullOrEmpty(response.access_token)||string.IsNullOrEmpty(response.refresh_token))throw new AccountException(401,"email_not_confirmed");
   User=response.user;access=response.access_token;refresh=response.refresh_token;
   expires=response.expires_at>0?response.expires_at:DateTimeOffset.UtcNow.ToUnixTimeSeconds()+Math.Max(1,response.expires_in);
   RecoveryPending=recovery;Changed?.Invoke();
  }
  async Task<AuthResponse> Authenticate(string path,object body,bool recovery,CancellationToken ct)
  {
   Clear();int g=generation;
   using(var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,lifetime.Token))
   {var result=await transport.SendAsync<AuthResponse>("POST",path,body,null,linked.Token);Current(g,linked.Token);Accept(result,recovery);return result;}
  }
  public Task<AuthResponse> SignInAsync(string email,string password,CancellationToken ct=default(CancellationToken))
  {
   if(string.IsNullOrEmpty(password))throw new AccountException(400,"invalid_credentials");
   return Authenticate("/auth/v1/token?grant_type=password",new Credentials{email=AccountRules.Email(email),password=password},false,ct);
  }
  public async Task SignUpAsync(string email,string password,CancellationToken ct=default(CancellationToken))
  {
   AccountRules.Password(password);email=AccountRules.Email(email);Clear();int g=generation;
   using(var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,lifetime.Token))
   {await transport.SendAsync<AuthResponse>("POST","/auth/v1/signup",new Credentials{email=email,password=password},null,linked.Token);Current(g,linked.Token);}
   // Even a project configured for auto-confirm requires a separate sign-in here.
  }
  public Task<AuthResponse> VerifySignupCodeAsync(string email,string code,CancellationToken ct=default(CancellationToken))
  {return Authenticate("/auth/v1/verify",new CodeRequest{email=AccountRules.Email(email),token=AccountRules.Code(code),type="email"},false,ct);}
  public Task<AuthResponse> VerifyRecoveryCodeAsync(string email,string code,CancellationToken ct=default(CancellationToken))
  {return Authenticate("/auth/v1/verify",new CodeRequest{email=AccountRules.Email(email),token=AccountRules.Code(code),type="recovery"},true,ct);}
  public async Task ResendSignupAsync(string email,CancellationToken ct=default(CancellationToken))
  {await transport.SendAsync<EmptyResponse>("POST","/auth/v1/resend",new EmailRequest{email=AccountRules.Email(email),type="signup"},null,ct);}
  public async Task BeginPasswordRecoveryAsync(string email,CancellationToken ct=default(CancellationToken))
  {email=AccountRules.Email(email);Clear();await transport.SendAsync<EmptyResponse>("POST","/auth/v1/recover",new EmailRequest{email=email},null,ct);}
  public async Task UpdatePasswordAsync(string password,CancellationToken ct=default(CancellationToken))
  {
   AccountRules.Password(password);if(!RecoveryPending||string.IsNullOrEmpty(access))throw new AccountException(401,"recovery_required");
   int g=generation;await EnsureFreshAsync(ct);Current(g,ct);
   await transport.SendAsync<AuthUser>("PUT","/auth/v1/user",new PasswordRequest{password=password},access,ct);Current(g,ct);await SignOutAsync(ct);
  }
  public Task EnsureFreshAsync(CancellationToken ct=default(CancellationToken))
  {
   ct.ThrowIfCancellationRequested();if(string.IsNullOrEmpty(access))throw new AccountException(401,"not_signed_in");
   if(expires>DateTimeOffset.UtcNow.ToUnixTimeSeconds()+60)return Task.CompletedTask;
   if(refreshTask!=null&&!refreshTask.IsCompleted)return refreshTask;
   return refreshTask=RefreshCoreAsync();
  }
  async Task RefreshCoreAsync()
  {
   int g=generation;var ct=lifetime.Token;bool recovery=RecoveryPending;
   try{var response=await transport.SendAsync<AuthResponse>("POST","/auth/v1/token?grant_type=refresh_token",new RefreshRequest{refresh_token=refresh},null,ct);Current(g,ct);
    if(response?.user?.id!=UserId)throw new AccountException(401,"invalid_token");Accept(response,recovery);}
   catch(AccountException e){if(g==generation&&(e.Status==400||e.Status==401||e.Status==403))Clear();throw;}
  }
  async Task<AccountProfile> ProfileCall(object body,CancellationToken ct)
  {
   if(!HasSession||RecoveryPending)throw new AccountException(401,"not_signed_in");int g=generation;
   using(var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,lifetime.Token))
   {
    await EnsureFreshAsync(linked.Token);Current(g,linked.Token);if(!IsAuthenticated)throw new AccountException(401,"not_signed_in");
    AccountProfile result;
    try{result=await transport.SendAsync<AccountProfile>("POST","/functions/v1/account",body,access,linked.Token);}
    catch(AccountException e){if(g==generation&&(e.Status==401||e.Status==403))Clear();throw;}
    Current(g,linked.Token);
    if(result==null||result.user_id!=UserId||result.level<1||result.level>15)throw new AccountException(502,"invalid_profile");
    Profile=result;Changed?.Invoke();return result;
   }
  }
  public async Task<T> StudyCallAsync<T>(string expectedUser,object body,CancellationToken ct=default(CancellationToken)) where T:class
  {
   if(!IsAuthenticated||UserId!=expectedUser)throw new AccountException(401,"not_signed_in");
   int g=generation;
   using(var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,lifetime.Token))
   {
    await EnsureFreshAsync(linked.Token);Current(g,linked.Token);
    if(!IsAuthenticated||UserId!=expectedUser)throw new AccountException(401,"not_signed_in");
    var result=await transport.SendAsync<T>("POST","/functions/v1/study",body,access,linked.Token);
    Current(g,linked.Token);return result;
   }
  }
  public Task<AccountProfile> ReadProfileAsync(CancellationToken ct=default(CancellationToken))=>ProfileCall(new ReadRequest(),ct);
  public Task<AccountProfile> EnterAsync(string nickname,CancellationToken ct=default(CancellationToken))=>ProfileCall(new ProfileRequest{action="enter",nickname=AccountRules.Nickname(nickname)},ct);
  public async Task SignOutAsync(CancellationToken ct=default(CancellationToken))
  {
   string previous=access;Clear();if(string.IsNullOrEmpty(previous))return;
   try{await transport.SendAsync<EmptyResponse>("POST","/auth/v1/logout?scope=local",null,previous,ct);}catch(AccountException){/* Local signout is immediate even when the remote service is unavailable. */}
  }
  public void Dispose(){Clear();lifetime.Dispose();(transport as IDisposable)?.Dispose();}
 }
}
