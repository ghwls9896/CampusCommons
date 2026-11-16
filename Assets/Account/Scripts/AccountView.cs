using System;
using System.Threading.Tasks;
using CampusCommons.GrandLibrary;
using CampusCommons.Webcam;
using UnityEngine;
namespace CampusCommons.Account
{
 [DefaultExecutionOrder(-100)]
 public sealed class AccountView : MonoBehaviour
 {
  enum LobbyPanel { None, Menu, My }

  AccountGate gate;
  AccountSession observed;
  AccountGate.Screen last;
  string email = "", password = "", confirm = "", code = "", nicknameEdit = "";
  Vector2 scroll;
  GUIStyle heading, label, field, button, notice, choice, panel;
  GUIStyle nameplateName, nameplateLevel, myButton;
  Player localPlayer;
  LobbyPanel lobbyPanel;
  bool editingNickname;

  bool IsInLobby => localPlayer && localPlayer.isActiveAndEnabled && localPlayer.currentSpace == Portal.Space.Library;
  internal bool LobbyOverlayOpen => lobbyPanel != LobbyPanel.None && gate.Ready && IsInLobby;

  void Awake() { gate = GetComponent<AccountGate>(); }
  void Styles()
  {
   if(heading!=null)return;
   var font=Resources.Load<Font>("KIT/NotoSansKR");
   heading=new GUIStyle(GUI.skin.label){font=font,fontSize=28,fontStyle=FontStyle.Bold,wordWrap=true};
   label=new GUIStyle(GUI.skin.label){font=font,fontSize=17,wordWrap=true};notice=new GUIStyle(label){normal={textColor=new Color(.95f,.79f,.46f)}};
   field=new GUIStyle(GUI.skin.textField){font=font,fontSize=18,fixedHeight=36,padding=new RectOffset(10,10,7,7)};
   button=new GUIStyle(GUI.skin.button){font=font,fontSize=17,fixedHeight=38};
   choice=new GUIStyle(button){fontSize=26,fontStyle=FontStyle.Bold,fixedHeight=96};
   panel=new GUIStyle(GUI.skin.box){padding=new RectOffset(24,24,24,24)};
   nameplateName = new GUIStyle
   {
    font = font, fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
    clipping = TextClipping.Overflow, normal = { textColor = Color.white }
   };
   nameplateLevel = new GUIStyle(nameplateName) { fontSize = 15 };
   myButton = new GUIStyle(button) { fontStyle = FontStyle.Bold, fontSize = 17, fixedHeight = 0 };
  }

  void Update()
  {
   if (gate.Session != observed)
   {
    if (observed != null) observed.Changed -= ClearWhenSignedOut;
    observed = gate.Session;
    if (observed != null) observed.Changed += ClearWhenSignedOut;
   }
   if (last != gate.Page)
   {
    password = confirm = code = "";
    last = gate.Page;
   }

   if (gate.Ready && !localPlayer) localPlayer = FindAnyObjectByType<Player>();
   if (!gate.Ready || !IsInLobby) CloseLobbyPanel();
   if (UnityEngine.Input.GetKeyDown(KeyCode.Escape)) HandleLobbyEscape();
   else if (gate.Ready && UnityEngine.Input.GetKeyDown(KeyCode.F9))
   {
    if (IsInLobby) ToggleMy();
    else if (StudyRecordService.Instance) StudyRecordService.Instance.ToggleHistory();
   }
   if (LobbyOverlayOpen) ShowPointer();
  }

  bool PlayerOverlayOpen()
  {
   if (!localPlayer) return false;
   return (localPlayer.TryGetComponent(out LibraryInventoryUI inventory) && inventory.OwnsInput) ||
       (localPlayer.TryGetComponent(out LibraryAvatarCustomizerUI avatar) && avatar.IsOpen);
  }

  bool HandleLobbyEscape()
  {
   if (!gate.Ready || !IsInLobby) return false;
   if (LobbyOverlayOpen)
   {
    CloseLobbyPanel();
    return true;
   }

   // Let the open inventory, character settings, or records consume Escape first.
   if (PlayerOverlayOpen() || localPlayer.IsExitConfirmOpen) return false;
   var records = StudyRecordService.Instance;
   if (records && records.CloseHistory()) return true;

   lobbyPanel = LobbyPanel.Menu;
   ShowPointer();
   return true;
  }

  bool ToggleMy()
  {
   if (!gate.Ready || !IsInLobby || lobbyPanel == LobbyPanel.Menu || PlayerOverlayOpen()) return false;
   if (lobbyPanel == LobbyPanel.My)
   {
    CloseLobbyPanel();
    return true;
   }

   lobbyPanel = LobbyPanel.My;
   var records = StudyRecordService.Instance;
   if (records)
   {
    records.CloseHistory();
    records.RefreshHistory();
   }
   ShowPointer();
   return true;
  }

  void CloseLobbyPanel()
  {
   lobbyPanel = LobbyPanel.None;
   editingNickname = false;
   nicknameEdit = "";
  }

  static void ShowPointer()
  {
   Cursor.lockState = CursorLockMode.None;
   Cursor.visible = true;
  }

  void LogoutFromLobbyMenu()
  {
   if (!LobbyOverlayOpen || lobbyPanel != LobbyPanel.Menu || gate.Busy) return;
   CloseLobbyPanel();
   Run(gate.LogoutAsync);
  }

  void ClearWhenSignedOut()
  {
   if (!gate.Session.IsAuthenticated && !gate.Session.RecoveryPending)
   {
    password = confirm = code = "";
    CloseLobbyPanel();
   }
  }
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
   Styles();
   int previousDepth = GUI.depth;
   GUI.depth = -10000;
   try
   {
    if (gate.Ready)
    {
     if (LobbyOverlayOpen)
     {
      if (lobbyPanel == LobbyPanel.My) DrawMy();
      else DrawLobbyMenu();
     }
     else if (!PlayerOverlayOpen())
     {
      DrawNameplate();
      if (IsInLobby) DrawMyButton();
     }
    }
    else DrawSignIn();
   }
   finally { GUI.depth = previousDepth; }
  }

  void DrawSignIn()
  {
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
    case AccountGate.Screen.LoadingProfile:
     GUILayout.Label("계정 연결", heading);
     if (gate.Busy) GUILayout.Label("내 정보를 불러오고 있습니다.", label);
     else
     {
      if (Button("다시 연결")) Run(gate.LoadProfileAndEnterAsync);
      if (Button("다른 계정으로 로그인")) Run(gate.LogoutAsync);
     }
     break;
   }
   GUI.enabled=true;
   if(gate.Busy)GUILayout.Label("처리 중입니다…",label);
   if(!string.IsNullOrEmpty(gate.Message))GUILayout.Label(gate.Message,notice);
   GUILayout.EndScrollView();GUILayout.EndArea();
  }
  void Back(){if(Button("시작 화면으로")){gate.Session.Clear();gate.Page=AccountGate.Screen.Start;}}
  void DrawMyButton()
  {
   if (GUI.Button(new Rect(Screen.width - 82, 12, 70, 34), "MY", myButton)) ToggleMy();
  }

  void DrawNameplate()
  {
   if (Event.current.type != EventType.Repaint) return;
   var profile = gate.Session.Profile;
   if (profile == null || !localPlayer || !localPlayer.TryGetNameplateScreenPoint(out Vector2 point)) return;

   Color tint = GUI.color;
   Color contentTint = GUI.contentColor;
   GUI.color = GUI.contentColor = Color.white;
   float bottom = DrawNameplateLine(profile.nickname, nameplateName, point.x, point.y);
   bottom = DrawNameplateLine($"Lv.{profile.level}", nameplateLevel, point.x, bottom);
   var study = LobbyStudyWebcam.Instance;
   string status = study ? study.StatusFor(localPlayer) : null;
   if (status != null) DrawNameplateLine(status, nameplateLevel, point.x, bottom);
   GUI.color = tint;
   GUI.contentColor = contentTint;
  }

  static float DrawNameplateLine(string value, GUIStyle style, float center, float bottom)
  {
   var content = new GUIContent(value);
   var size = style.CalcSize(content);
   float width = Mathf.Ceil(size.x) + 8;
   float height = Mathf.Ceil(Mathf.Max(size.y, style.lineHeight)) + 4;
   float top = bottom - height;
   style.Draw(new Rect(center - width / 2, top, width, height), content, false, false, false, false);
   return top;
  }

  void DimWorld()
  {
   var color = GUI.color;
   GUI.color = new Color(0.035f, 0.045f, 0.055f, 0.6f);
   GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
   GUI.color = color;
  }

  void DrawMy()
  {
   var profile = gate.Session.Profile;
   if (profile == null) return;
   DimWorld();
   float width = Mathf.Min(560, Screen.width - 32);
   float height = Mathf.Min(540, Screen.height - 32);
   GUILayout.BeginArea(new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height), panel);
   GUILayout.BeginHorizontal();
   GUILayout.Label("MY", heading);
   GUILayout.FlexibleSpace();
   if (GUILayout.Button("닫기", button, GUILayout.Width(76))) CloseLobbyPanel();
   GUILayout.EndHorizontal();

   GUILayout.Space(10);
   GUILayout.Label($"Lv.{profile.level}/15", label);
   GUILayout.BeginHorizontal();
   GUILayout.Label("닉네임  " + profile.nickname, label);
   if (!editingNickname && GUILayout.Button("닉네임 수정", button, GUILayout.Width(128)))
   {
    nicknameEdit = profile.nickname;
    editingNickname = true;
   }
   GUILayout.EndHorizontal();

   if (editingNickname) DrawNicknameEditor();
   if (!string.IsNullOrEmpty(gate.Message)) GUILayout.Label(gate.Message, notice);

   GUILayout.Space(14);
   var records = StudyRecordService.Instance;
   if (records) records.DrawHistoryContents();
   else GUILayout.Label("공부 기록을 불러오는 중입니다.", label);
   GUILayout.EndArea();
  }

  void DrawNicknameEditor()
  {
   bool enabled = GUI.enabled;
   GUI.enabled = enabled && !gate.Busy;
   nicknameEdit = Input("새 닉네임 · 2~12자", nicknameEdit);
   GUILayout.BeginHorizontal();
   if (Button("저장"))
   {
    string nickname = nicknameEdit;
    Run(async () =>
    {
     await gate.ChangeNicknameAsync(nickname);
     editingNickname = false;
    });
   }
   if (Button("취소")) editingNickname = false;
   GUILayout.EndHorizontal();
   GUI.enabled = enabled;
  }

  void DrawLobbyMenu()
  {
   DimWorld();

   bool hasNotice = !string.IsNullOrEmpty(gate.Message);
   float width = Mathf.Min(348, Screen.width - 32);
   float height = hasNotice ? 246 : 196;
   GUILayout.BeginArea(new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height), panel);
   GUILayout.Label("로비 메뉴", heading);
   if (hasNotice) GUILayout.Label(gate.Message, notice);
   GUILayout.Space(12);
   if (Button("계속하기")) CloseLobbyPanel();
   bool enabled = GUI.enabled;
   GUI.enabled = enabled && !gate.Busy;
   if (Button("로그아웃")) LogoutFromLobbyMenu();
   GUI.enabled = enabled;
   GUILayout.EndArea();
  }
 }
}
