using System;
using System.Reflection;
using CampusCommons.GrandLibrary;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class CodexCleanerSpeedTurnTuningVerification {
 const string Key="CodexCleanerSpeedTurnTuningVerification";const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;static EscapeGame game;static EscapeCorridorCleaner cleaner;static float start;static Vector3 startPosition;
 static object Get(string n)=>typeof(EscapeCorridorCleaner).GetField(n,F).GetValue(cleaner);static void Set(string n,object v)=>typeof(EscapeCorridorCleaner).GetField(n,F).SetValue(cleaner,v);static void Call(string n,params object[] v)=>typeof(EscapeCorridorCleaner).GetMethod(n,F).Invoke(cleaner,v);static void Check(bool ok,string s){if(!ok)throw new Exception(s);}
 public static void Run(){EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod]static void Resume(){EditorApplication.delayCall+=()=>{if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;};}static void Finish(int n){Time.timeScale=1;SessionState.SetBool(Key,false);EditorApplication.Exit(n);}
 static void Tick(){if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<2)return;try{
 if(!cleaner){game=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();var p=UnityEngine.Object.FindAnyObjectByType<Player>();Check(game.BeginSelection(p)&&game.ChooseMode(EscapeGame.Mode.Easy),"start failed");cleaner=UnityEngine.Object.FindAnyObjectByType<EscapeCorridorCleaner>();typeof(EscapeWorld).GetField("barrierCountdown",F).SetValue(game.World,9999f);typeof(EscapeWorld).GetField("professorCooldown",F).SetValue(game.World,9999f);if(game.monster)game.monster.gameObject.SetActive(false);
 Check(Mathf.Abs((float)Get("speed")-2.42f)<.001f,"starting speed wrong");Set("activeSeconds",8f);Call("UpdateAcceleration",2.5f);Check(Mathf.Abs((float)Get("speed")-28.6f)<.001f,"top speed wrong");Call("ResetAcceleration");Check(Mathf.Abs((float)Get("speed")-2.42f)<.001f,"reset speed wrong");Set("facingDirection",Vector3.forward);Set("direction",Vector3.right);Set("turning",true);startPosition=cleaner.transform.position;Time.timeScale=2;start=Time.time;return;}
 if((bool)Get("turning")){Check(Vector3.Distance(startPosition,cleaner.transform.position)<.1f,"moved during turn");Check(Time.time-start<7,"rotation took too long");}else{float elapsed=Time.time-start;Check(elapsed>4.7f&&elapsed<5.6f,"unexpected 90 degree turn duration "+elapsed);Check(Vector3.Angle((Vector3)Get("facingDirection"),Vector3.right)<.2f,"incorrect facing");Debug.Log("CLEANER_TUNING_PASS initial=2.42 max=28.6 turn90="+elapsed);game.ExitToLobby();Finish(0);}
 }catch(Exception e){Debug.LogException(e);Finish(1);}}
}
