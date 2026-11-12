using System;
using System.Threading.Tasks;
using UnityEngine;
namespace CampusCommons.Account
{
 public sealed class AccountView : MonoBehaviour
 {
  AccountGate gate;string email="",password="",confirm="",code="",nickname="";Vector2 scroll;AccountSession observed;AccountProfile observedProfile;
  GUIStyle heading,label,field,button,notice,choice,panel;AccountGate.Screen last;
  void Awake(){gate=GetComponent<AccountGate>();}
  void Styles()
  {
   if(heading!=null)return;
   var font=Font.CreateDynamicFontFromOSFont(new[]{"Apple SD Gothic Neo","Malgun Gothic","Noto Sans CJK KR","Arial"},18);
   heading=new GUIStyle(GUI.skin.label){font=font,fontSize=28,fontStyle=FontStyle.Bold,wordWrap=true};
   label=new GUIStyle(GUI.skin.label){font=font,fontSize=17,wordWrap=true};notice=new GUIStyle(label){normal={textColor=new Color(.95f,.79f,.46f)}};
   field=new GUIStyle(GUI.skin.textField){font=font,fontSize=18,fixedHeight=36,padding=new RectOffset(10,10,7,7)};
   button=new GUIStyle(GUI.skin.button){font=font,fontSize=17,fixedHeight=38};
   choice=new GUIStyle(button){fontSize=26,fontStyle=FontStyle.Bold,fixedHeight=96};
   panel=new GUIStyle(GUI.skin.box){padding=new RectOffset(24,24,24,24)};
  }
  void Update()
  {
   if(gate.Session!=observed){if(observed!=null)observed.Changed-=ClearWhenSignedOut;observed=gate.Session;if(observed!=null)observed.Changed+=ClearWhenSignedOut;}
   if(last!=gate.Page){password=confirm=code="";if(gate.Page==AccountGate.Screen.Nickname)nickname=gate.Session.Profile?.nickname??"";last=gate.Page;}
   // The profile arrives after the screen transition during real HTTP requests.
   if(gate.Session.Profile!=observedProfile){observedProfile=gate.Session.Profile;if(gate.Page==AccountGate.Screen.Nickname)nickname=observedProfile?.nickname??"";}
  }
  void ClearWhenSignedOut(){if(!gate.Session.IsAuthenticated&&!gate.Session.RecoveryPending){password=confirm=code=nickname="";}}
  void OnDestroy(){if(observed!=null)observed.Changed-=ClearWhenSignedOut;}
  string Input(string title,string value,bool secret=false){GUILayout.Label(title,label);return secret?GUILayout.PasswordField(value,'●',128,field):GUILayout.TextField(value,254,field);}
  bool Button(string title)=>GUILayout.Button(title,button);
  void Run(Func<Task> action){gate.Perform(action);}
  void PasswordAction(Func<string,Task> action)
  {
   string p=password;bool matches=password==confirm;
   Run(()=>{if(!matches)throw new AccountException(400,"password_mismatch");return action(p);});
  }
  void OnGUI()
  {
   Styles();GUI.depth=-10000;
   if(gate.Ready){DrawHud();return;}
   var old=GUI.color;GUI.color=new Color(.055f,.075f,.085f,.97f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=old;
   float width=Mathf.Min(520,Screen.width-28),height=Mathf.Min(gate.Page==AccountGate.Screen.Start?520:720,Screen.height-28);
   GUILayout.BeginArea(new Rect((Screen.width-width)/2,(Screen.height-height)/2,width,height),panel);
   scroll=GUILayout.BeginScrollView(scroll);GUILayout.Label("Campus Commons",heading);GUILayout.Space(12);
   if(!gate.Configured){GUILayout.Label("Supabase 연결 설정이 필요합니다.",heading);GUILayout.Label("프로젝트의 docs/account/SETUP.md에 따라 설정한 뒤 Campus → 계정 → Supabase 연결 설정에서 공개 연결값을 저장해 주세요.",label);}
   GUI.enabled=!gate.Busy&&gate.Configured;
   switch(gate.Page)
   {
    case AccountGate.Screen.Start:
     GUILayout.Label("함께 배우는 캠퍼스에 오신 것을 환영합니다.",label);GUILayout.Space(28);
     if(GUILayout.Button("로그인",choice))gate.Page=AccountGate.Screen.Login;
     GUILayout.Space(16);
     if(GUILayout.Button("회원가입",choice))gate.Page=AccountGate.Screen.Signup;
     GUILayout.Space(40);
     if(Button("비밀번호 찾기"))gate.Page=AccountGate.Screen.RecoveryEmail;break;
    case AccountGate.Screen.Login:
     GUILayout.Label("로그인",heading);email=Input("이메일",email);password=Input("비밀번호",password,true);
     if(Button("로그인")){var e=email;var p=password;Run(()=>gate.LoginAsync(e,p));}
     if(Button("회원가입"))gate.Page=AccountGate.Screen.Signup;
     if(Button("이메일 확인 코드 입력"))gate.Page=AccountGate.Screen.SignupCode;
     if(Button("비밀번호 찾기"))gate.Page=AccountGate.Screen.RecoveryEmail;Back();break;
    case AccountGate.Screen.Signup:
     GUILayout.Label("회원가입",heading);email=Input("이메일",email);password=Input("비밀번호 · 12~128자",password,true);confirm=Input("비밀번호 확인",confirm,true);
     if(Button("가입하고 확인 코드 받기"))PasswordAction(p=>gate.SignupAsync(email,p));Back();break;
    case AccountGate.Screen.SignupCode:
     GUILayout.Label("이메일 확인",heading);email=Input("이메일",email);code=Input("메일로 받은 확인 코드",code);
     if(Button("확인하고 계속")){var e=email;var c=code;Run(()=>gate.VerifySignupAsync(e,c));}
     if(Button("확인 코드 재전송"))Run(()=>gate.Session.ResendSignupAsync(email));Back();break;
    case AccountGate.Screen.RecoveryEmail:
     GUILayout.Label("비밀번호 찾기",heading);email=Input("가입한 이메일",email);if(Button("재설정 코드 받기"))Run(()=>gate.RecoverAsync(email));Back();break;
    case AccountGate.Screen.RecoveryCode:
     GUILayout.Label("재설정 코드 확인",heading);email=Input("이메일",email);code=Input("메일로 받은 재설정 코드",code);
     if(Button("코드 확인")){var e=email;var c=code;Run(()=>gate.VerifyRecoveryAsync(e,c));}
     if(Button("재설정 코드 재전송"))Run(()=>gate.RecoverAsync(email));Back();break;
    case AccountGate.Screen.NewPassword:
     GUILayout.Label("새 비밀번호",heading);password=Input("새 비밀번호 · 12~128자",password,true);confirm=Input("새 비밀번호 확인",confirm,true);
     if(Button("비밀번호 변경"))PasswordAction(p=>gate.ResetPasswordAsync(p));Back();break;
    case AccountGate.Screen.Nickname:
     GUILayout.Label("입장 닉네임",heading);nickname=Input("닉네임 · 2~12자",nickname);
     if(gate.Session.Profile!=null){var p=gate.Session.Profile;GUILayout.Label("Lv. "+p.level+" / 15",label);}
     if(Button("로비 입장")){var n=nickname;Run(()=>gate.EnterAsync(n));}
     if(Button("내 정보 다시 불러오기"))Run(async()=>{await gate.Session.ReadProfileAsync();nickname=gate.Session.Profile.nickname;});
     if(Button("다른 계정으로 로그인"))Run(()=>gate.LogoutAsync());break;
   }
   GUI.enabled=true;
   if(gate.Busy)GUILayout.Label("처리 중입니다…",label);
   if(!string.IsNullOrEmpty(gate.Message))GUILayout.Label(gate.Message,notice);
   GUILayout.EndScrollView();GUILayout.EndArea();
  }
  void Back(){if(Button("시작 화면으로")){gate.Session.Clear();gate.Page=AccountGate.Screen.Start;}}
  void DrawHud()
  {
   var p=gate.Session.Profile;if(p==null)return;
   GUILayout.BeginArea(new Rect(Mathf.Max(8,Screen.width-310),12,298,140),GUI.skin.box);
   GUILayout.Label(p.nickname+" · Lv. "+p.level+" / 15"+(p.level==15?" · MAX":""),label);
   GUI.enabled=!gate.Busy;if(Button("로그아웃"))Run(()=>gate.LogoutAsync());GUI.enabled=true;
   if(!string.IsNullOrEmpty(gate.Message))GUILayout.Label(gate.Message,notice);GUILayout.EndArea();
  }
 }
}
