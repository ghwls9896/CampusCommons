using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace CampusCommons.Account
{
 [Serializable] public sealed class AuthUser { public string id,email,email_confirmed_at; public bool is_anonymous; }
 [Serializable] public sealed class AuthResponse { public string access_token,refresh_token; public long expires_at,expires_in; public AuthUser user; }
 [Serializable] public sealed class AccountProfile { public string user_id,nickname; public int level; }
 [Serializable] public sealed class Credentials {public string email,password;}
 [Serializable] public sealed class EmailRequest {public string email,type;}
 [Serializable] public sealed class CodeRequest {public string email,token,type;}
 [Serializable] public sealed class RefreshRequest {public string refresh_token;}
 [Serializable] public sealed class PasswordRequest {public string password;}
 [Serializable] public sealed class ProfileRequest {public string action,nickname;}
 [Serializable] public sealed class ReadRequest {public string action="read";}
 [Serializable] public sealed class EmptyResponse { }
 public sealed class AccountException : Exception
 {
  public readonly int Status;public readonly string Code;
  public AccountException(int status,string code):base("계정 요청을 처리하지 못했습니다."){Status=status;Code=code;}
 }
 public interface IAccountTransport
 {
  Task<T> SendAsync<T>(string method,string path,object body,string bearer,CancellationToken ct) where T:class;
 }
 public static class AccountRules
 {
  public static string Email(string value)
  {
   value=(value??"").Trim();
   if(value.Length>254||value.IndexOf('@')<1||value.EndsWith("@")||value.IndexOfAny(new[]{' ','\r','\n','\t'})>=0)throw new AccountException(400,"invalid_email");return value;
  }
  public static void Password(string value)
  {if(value==null||value.Length<12||value.Length>128)throw new AccountException(400,"weak_password");}
  public static string Nickname(string value)
  {
   string name;try{name=(value??"").Normalize(NormalizationForm.FormC).Trim();}catch(ArgumentException){throw new AccountException(400,"invalid_nickname");}
   int length=0;
   for(int i=0;i<name.Length;i++)
   {
    var c=CharUnicodeInfo.GetUnicodeCategory(name,i);
    if(c==UnicodeCategory.Control||c==UnicodeCategory.Format||c==UnicodeCategory.LineSeparator||c==UnicodeCategory.ParagraphSeparator||name[i]=='<'||name[i]=='>')throw new AccountException(400,"invalid_nickname");
    if(char.IsHighSurrogate(name[i])&&i+1<name.Length&&char.IsLowSurrogate(name[i+1]))i++;
    else if(char.IsSurrogate(name[i]))throw new AccountException(400,"invalid_nickname");length++;
   }
   if(length<2||length>12)throw new AccountException(400,"invalid_nickname");return name;
  }
  public static string Code(string value)
  {value=(value??"").Trim();if(value.Length!=6)throw new AccountException(400,"invalid_code");foreach(char c in value)if(c<'0'||c>'9')throw new AccountException(400,"invalid_code");return value;}
 }
}
