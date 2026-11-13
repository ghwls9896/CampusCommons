using System;
using System.Reflection;
using CampusCommons.GrandLibrary;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class CodexCleanerCorridorCatchVerification {
 const string Key="CodexCleanerCorridorCatchVerification";const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;static EscapeGame game;static EscapeCorridorCleaner robot;static CharacterController player;static int stage;static float start;
 static object Get(string n)=>typeof(EscapeCorridorCleaner).GetField(n,F).GetValue(robot);static void Set(string n,object v)=>typeof(EscapeCorridorCleaner).GetField(n,F).SetValue(robot,v);static object Call(string n,params object[] v)=>typeof(EscapeCorridorCleaner).GetMethod(n,F).Invoke(robot,v);static void Check(bool b,string s){if(!b)throw new Exception(s);}static void Place(CharacterController b,Vector3 p){b.enabled=false;b.transform.position=p;b.enabled=true;}
 public static void Run(){EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod]static void Resume(){EditorApplication.delayCall+=()=>{if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;};}static void Finish(int n){Time.timeScale=1;SessionState.SetBool(Key,false);EditorApplication.Exit(n);}
 static void Setup(int side){robot.Release();var rb=robot.GetComponent<CharacterController>();Place(rb,new Vector3(527.5f,.08f,-27));Place(player,new Vector3(527.5f+side*1.15f,.08f,-25.95f));var nav=Get("navigation");nav.GetType().GetProperty("Target").SetValue(nav,new Vector3(527.5f,.08f,-20));Set("direction",Vector3.forward);Set("facingDirection",Vector3.forward);Set("turning",false);Set("navigationReady",true);Set("catchCooldown",0f);Set("speed",2.2f);Set("activeSeconds",0f);Physics.SyncTransforms();Check((bool)Call("CanCatch",player,0f),"real corridor edge blocked side="+side);start=Time.time;}
 static void Tick(){if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<2)return;try{
 if(stage==0){game=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();var p=UnityEngine.Object.FindAnyObjectByType<Player>();Check(game.BeginSelection(p)&&game.ChooseMode(EscapeGame.Mode.Easy),"start failed");robot=UnityEngine.Object.FindAnyObjectByType<EscapeCorridorCleaner>();player=p.GetComponent<CharacterController>();typeof(EscapeWorld).GetField("barrierCountdown",F).SetValue(game.World,9999f);typeof(EscapeWorld).GetField("professorCooldown",F).SetValue(game.World,9999f);if(game.monster)game.monster.gameObject.SetActive(false);
 var rb=robot.GetComponent<CharacterController>();Place(rb,new Vector3(1000,.08f,1000));Set("direction",Vector3.forward);var probe=new GameObject("catch probe").AddComponent<CharacterController>();probe.height=1.8f;probe.radius=.3f;probe.center=Vector3.up*.9f;Place(probe,new Vector3(1001.8f,.08f,1000.8f));Physics.SyncTransforms();Check((bool)Call("CanCatch",probe,0f),"right edge not covered");Place(probe,new Vector3(998.2f,.08f,1000.8f));Physics.SyncTransforms();Check((bool)Call("CanCatch",probe,0f),"left edge not covered");Place(probe,new Vector3(1001.8f,.08f,1000.8f));var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(1000.9f,1f,1000.4f);wall.transform.localScale=new Vector3(.12f,2,1.5f);Physics.SyncTransforms();Check(!(bool)Call("CanCatch",probe,0f),"caught through wall");UnityEngine.Object.DestroyImmediate(wall);UnityEngine.Object.DestroyImmediate(probe.gameObject);Debug.Log("CLEANER_WIDTH_GEOMETRY_PASS both sides and wall protection");Time.timeScale=2;Setup(-1);stage=1;return;}
 if(stage==1&&robot.IsPushing){Debug.Log("CLEANER_REAL_EDGE_CATCH_PASS left");Setup(1);stage=2;return;}
 if(stage==2&&robot.IsPushing){Debug.Log("CLEANER_REAL_EDGE_CATCH_PASS right");game.ExitToLobby();Finish(0);return;}
 Check(Time.time-start<5,"failed to capture edge side stage="+stage+" pos="+robot.transform.position);
 }catch(Exception e){Debug.LogException(e);Finish(1);}}
}
