using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
namespace CampusCommons.Account
{
 [Serializable] public sealed class AccountConnection
 {
  public string url="",publishableKey="";
  public bool IsValid()
  {
   Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||uri.Scheme!="https"||string.IsNullOrEmpty(uri.Host)||uri.AbsolutePath!="/"||uri.UserInfo!=""||uri.Query!=""||uri.Fragment!="")return false;
   if(publishableKey.StartsWith("sb_publishable_",StringComparison.Ordinal))return publishableKey.Length>20;
   // Legacy anon keys are supported. Reject privileged keys at the configuration boundary.
   try{var parts=publishableKey.Split('.');if(parts.Length!=3)return false;var body=parts[1].Replace('-','+').Replace('_','/');body=body.PadRight((body.Length+3)/4*4,'=');return JsonUtility.FromJson<KeyRole>(Encoding.UTF8.GetString(Convert.FromBase64String(body)))?.role=="anon";}catch{return false;}
  }
  [Serializable] sealed class KeyRole {public string role;}
 }
 public sealed class UnityAccountTransport : IAccountTransport, IDisposable
 {
  readonly List<Action> pending=new List<Action>();bool disposed;
  readonly MonoBehaviour runner;readonly AccountConnection connection;
  public UnityAccountTransport(MonoBehaviour runner,AccountConnection connection){this.runner=runner;this.connection=connection;}
  public Task<T> SendAsync<T>(string method,string path,object body,string bearer,CancellationToken ct) where T:class
  {
   var completion=new TaskCompletionSource<T>();
   if(disposed||ct.IsCancellationRequested){completion.SetCanceled();return completion.Task;}
   if(!connection.IsValid()){completion.SetException(new AccountException(503,"configuration_required"));return completion.Task;}
   UnityWebRequest req=null;
   try
   {
    req=new UnityWebRequest(connection.url.TrimEnd('/')+path,method);req.downloadHandler=new DownloadHandlerBuffer();req.timeout=25;
    if(body!=null){req.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(body)));req.SetRequestHeader("Content-Type","application/json");}
    req.SetRequestHeader("apikey",connection.publishableKey);if(!string.IsNullOrEmpty(bearer))req.SetRequestHeader("Authorization","Bearer "+bearer);
    var active=req;Action abort=()=>{active.Abort();completion.TrySetCanceled();active.Dispose();};
    var op=req.SendWebRequest();pending.Add(abort);runner.StartCoroutine(Complete(req,op,ct,completion,abort));
   }
   catch{req?.Dispose();completion.SetException(new AccountException(503,"connection_failed"));}
   return completion.Task;
  }
  IEnumerator Complete<T>(UnityWebRequest req,UnityWebRequestAsyncOperation op,CancellationToken ct,TaskCompletionSource<T> completion,Action abort) where T:class
  {
   try
   {
   using(req)
   {
    while(!completion.Task.IsCompleted&&!op.isDone)
    {
     if(ct.IsCancellationRequested){req.Abort();completion.TrySetCanceled();yield break;}
     if(req.downloadedBytes>1024*1024){req.Abort();completion.TrySetException(new AccountException(502,"invalid_response"));yield break;}
     yield return null;
    }
    if(completion.Task.IsCompleted)yield break;
    if(ct.IsCancellationRequested){completion.TrySetCanceled();yield break;}
    if(req.result!=UnityWebRequest.Result.Success)
    {
     string code="connection_failed";try{var error=JsonUtility.FromJson<AuthError>(req.downloadHandler.text);if(!string.IsNullOrEmpty(error?.error_code))code=error.error_code;else{var envelope=JsonUtility.FromJson<ErrorEnvelope>(req.downloadHandler.text);if(!string.IsNullOrEmpty(envelope?.error?.code))code=envelope.error.code;}}catch{}
     completion.TrySetException(new AccountException((int)req.responseCode,code));yield break;
    }
    try{var text=req.downloadHandler.text;completion.TrySetResult(string.IsNullOrWhiteSpace(text)?Activator.CreateInstance<T>():JsonUtility.FromJson<T>(text));}
    catch{completion.TrySetException(new AccountException(502,"invalid_response"));}
   }
   }finally{pending.Remove(abort);}
  }
  public void Dispose(){if(disposed)return;disposed=true;foreach(var abort in pending.ToArray())abort();pending.Clear();}
  [Serializable] sealed class AuthError {public string error_code;}
  [Serializable] sealed class ErrorEnvelope {public ErrorBody error;}
  [Serializable] sealed class ErrorBody {public string code;}
 }
}
