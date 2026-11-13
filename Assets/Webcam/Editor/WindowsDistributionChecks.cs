using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using CampusCommons.Webcam;
public static class WindowsDistributionChecks
{
 const string Key="WindowsDistributionChecks"; static int stage; static bool failed; static Font shared; static double deadline; static MonoBehaviour[] targets;
 public static void Run(){SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod] static void Resume(){EditorApplication.delayCall+=()=>{if(!SessionState.GetBool(Key,false))return;stage=0;failed=false;deadline=EditorApplication.timeSinceStartup+60;EditorApplication.update+=Tick;};}
 static void Log(string message,string trace,LogType kind){if(kind==LogType.Error||kind==LogType.Exception)failed=true;}
 static void Tick(){
  if(EditorApplication.timeSinceStartup>deadline){Finish(false);return;}
  if(!EditorApplication.isPlaying)return;
  if(stage==0){
   failed=false;Application.logMessageReceived+=Log;
   shared=Resources.Load<Font>("KIT/NotoSansKR");if(!shared){Finish(false);return;}
   var boot=StudyRuntimeBootstrap.Instance;
   var lobby=UnityEngine.Object.FindAnyObjectByType<LobbyStudyWebcam>();
   if(!lobby)lobby=new GameObject("Font ownership test").AddComponent<LobbyStudyWebcam>();
   targets=new MonoBehaviour[]{boot,lobby,StudyRecordService.Instance};
   foreach(var target in targets){
    target.GetType().GetField("font",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,shared);
    UnityEngine.Object.Destroy(target.gameObject);
   }
   stage=1;return;
  }
  foreach(var target in targets)if(target)return;
  Finish(!failed&&shared&&Resources.Load<Font>("KIT/NotoSansKR")==shared);
 }
 static void Finish(bool passed){SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;Debug.Log(passed?"SHARED_FONT_LIFETIME_PASS":"SHARED_FONT_LIFETIME_FAIL");EditorApplication.Exit(passed?0:1);}
}
