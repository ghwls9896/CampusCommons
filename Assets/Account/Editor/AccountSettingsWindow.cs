using System.IO;
using UnityEditor;
using UnityEngine;
namespace CampusCommons.Account.Editor
{
 public sealed class AccountSettingsWindow : EditorWindow
 {
  string url="",key="",message="";
  [MenuItem("Campus/계정/Supabase 연결 설정")]
  public static void Open(){GetWindow<AccountSettingsWindow>("Supabase 연결").Show();}
  void OnEnable()
  {
   var file=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Account/Resources/AccountConnection.json");
   if(file){var config=JsonUtility.FromJson<AccountConnection>(file.text);url=config.url;key=config.publishableKey;}
  }
  void OnGUI()
  {
   EditorGUILayout.LabelField("공개 클라이언트 연결값",EditorStyles.boldLabel);
   EditorGUILayout.HelpBox("프로젝트 URL과 publishable key만 입력하세요. secret/service_role/DB 비밀번호는 입력하면 안 됩니다. 저장한 값은 게임 빌드에 포함됩니다.",MessageType.Info);
   url=EditorGUILayout.TextField("Project URL",url);key=EditorGUILayout.TextField("Publishable key",key);
   if(GUILayout.Button("연결값 저장"))
   {
    var config=new AccountConnection{url=url.Trim().TrimEnd('/'),publishableKey=key.Trim()};
    if(!config.IsValid())message="HTTPS 프로젝트 URL과 공개 publishable/anon key를 확인하세요.";
    else{File.WriteAllText("Assets/Account/Resources/AccountConnection.json",JsonUtility.ToJson(config,true)+"\n");AssetDatabase.Refresh();message="저장했습니다. Play를 다시 시작해 로그인하세요.";}
   }
   if(!string.IsNullOrEmpty(message))EditorGUILayout.HelpBox(message,MessageType.Info);
   if(GUILayout.Button("설정 안내 열기"))Application.OpenURL("file://"+Path.GetFullPath("docs/account/SETUP.md"));
  }
 }
}
