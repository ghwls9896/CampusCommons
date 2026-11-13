using System;using System.Reflection;using CampusCommons.Webcam;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;
public static class StudyBackgroundVerification {
 const string Key="StudyBackgroundVerification";
 public static void Run(){SessionState.SetBool(Key,true);EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod]static void Resume(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
 static void Tick(){if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<1)return;int code=0;
  try{var webcam=UnityEngine.Object.FindAnyObjectByType<LobbyStudyWebcam>();if(!webcam)throw new Exception("Bootstrap absent");
   var session=(StudySession)typeof(LobbyStudyWebcam).GetField("session",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(webcam);session.Begin(true,true);
   webcam.SendMessage("OnApplicationFocus",true);webcam.SendMessage("OnApplicationFocus",false);if(!webcam.IsRunning)throw new Exception("Background focus must keep active measurement");
   if(!Application.runInBackground)throw new Exception("Unity must tick while backgrounded");
   webcam.SendMessage("OnApplicationPause",true);if(webcam.IsRunning)throw new Exception("Actual OS pause must stop measurement");
   Debug.Log("STUDY_BACKGROUND_PASS: focus preserved, actual pause stopped; no camera/model");
  }catch(Exception e){Debug.LogException(e);code=1;}
  SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(code);
 }
}
