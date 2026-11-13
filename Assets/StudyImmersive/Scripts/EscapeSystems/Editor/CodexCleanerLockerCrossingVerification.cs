using System;
using System.Reflection;
using CampusCommons.GrandLibrary;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class CodexCleanerLockerCrossingVerification {
 const string Key="CodexCleanerLockerCrossingVerification";const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;static EscapeGame game;static EscapeCorridorCleaner robot;static CharacterController player;static int stage;static float start;static bool captured;
 static object Get(string n)=>typeof(EscapeCorridorCleaner).GetField(n,F).GetValue(robot);static void Set(string n,object v)=>typeof(EscapeCorridorCleaner).GetField(n,F).SetValue(robot,v);static void Check(bool b,string s){if(!b)throw new Exception(s);}static void Place(CharacterController c,Vector3 p){c.enabled=false;c.transform.position=p;c.enabled=true;}
 public static void Run(){EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod]static void Resume(){EditorApplication.delayCall+=()=>{if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;};}static void Finish(int n){Time.timeScale=1;SessionState.SetBool(Key,false);EditorApplication.Exit(n);}
 static void Setup(bool push){robot.Release();Place(robot.GetComponent<CharacterController>(),new Vector3(527.5f,.08f,4));Place(player,push?new Vector3(527.5f,.08f,5.05f):new Vector3(500,.08f,-25));var nav=Get("navigation");nav.GetType().GetProperty("Target").SetValue(nav,new Vector3(527.5f,.08f,27));Set("direction",Vector3.forward);Set("facingDirection",Vector3.forward);Set("turning",false);Set("navigationReady",true);Set("catchCooldown",0f);Set("activeSeconds",8f);Set("speed",2.42f);Set("accelerationTime",0f);Physics.SyncTransforms();captured=false;start=Time.time;}
 static void Tick(){if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<2)return;try{
 if(stage==0){game=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();var p=UnityEngine.Object.FindAnyObjectByType<Player>();Check(game.BeginSelection(p)&&game.ChooseMode(EscapeGame.Mode.Easy),"start failed");robot=UnityEngine.Object.FindAnyObjectByType<EscapeCorridorCleaner>();player=p.GetComponent<CharacterController>();typeof(EscapeWorld).GetField("barrierCountdown",F).SetValue(game.World,9999f);typeof(EscapeWorld).GetField("professorCooldown",F).SetValue(game.World,9999f);if(game.monster)game.monster.gameObject.SetActive(false);Time.timeScale=3;Setup(false);stage=1;return;}
 captured |= robot.IsPushing;
 if(robot.transform.position.z>24f){if(stage==1){Debug.Log("LOCKER_CORRIDOR_SOLO_PASS pos="+robot.transform.position);Setup(true);stage=2;return;}Check(captured,"player was not caught");Debug.Log("LOCKER_CORRIDOR_PUSH_PASS pos="+robot.transform.position+" player="+player.transform.position);game.ExitToLobby();Finish(0);return;}
 Check(Time.time-start<40,"locker passage blocked stage="+stage+" position="+robot.transform.position+" direction="+Get("direction"));
 }catch(Exception e){Debug.LogException(e);Finish(1);}}
}
