using System;using System.Threading;using System.Threading.Tasks;using CampusCommons.Account;
class AccountStudyTests{
 class Reply{public string user_id;}
 class Transport:IAccountTransport{
  public TaskCompletionSource<Reply> held=new TaskCompletionSource<Reply>();public int writes;
  public Task<T> SendAsync<T>(string method,string path,object body,string bearer,CancellationToken ct)where T:class{
   if(typeof(T)==typeof(AuthResponse))return Task.FromResult((T)(object)new AuthResponse{user=new AuthUser{id="a",email_confirmed_at="yes"},access_token="token",refresh_token="refresh",expires_in=3600});
   writes++;return (Task<T>)(object)held.Task;
  }
 }
 static void Main(){Run().GetAwaiter().GetResult();}
 static async Task Run(){var t=new Transport();var s=new AccountSession(t);await s.SignInAsync("a@example.test","pass");bool failed=false;try{await s.StudyCallAsync<Reply>("b",new object());}catch(AccountException){failed=true;}if(!failed||t.writes!=0)throw new Exception("foreign outbox sent");
  var task=s.StudyCallAsync<Reply>("a",new object());s.Clear();t.held.SetResult(new Reply{user_id="a"});failed=false;try{await task;}catch(OperationCanceledException){failed=true;}if(!failed)throw new Exception("late logout response accepted");Console.WriteLine("STUDY_ACCOUNT_PASS");}
}
