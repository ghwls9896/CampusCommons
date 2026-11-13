using System;
using System.Reflection;
using CampusCommons.GrandLibrary;
using CampusCommons.LibraryCharacter;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class CodexCleanerRealGateNavigationVerification {
 const string Key="CodexCleanerRealGateNavigationVerification";const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance; static EscapeGame game; static EscapeCorridorCleaner robot; static Door gate; static float start; static int stage;
 static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);static void Check(bool x,string s){if(!x)throw new Exception(s);}static void Place(CharacterController c,Vector3 p){c.enabled=false;c.transform.position=p;c.enabled=true;}
 public static void Run(){EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod]static void Resume(){EditorApplication.delayCall+=()=>{if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;};}static void Finish(int n){Time.timeScale=1;SessionState.SetBool(Key,false);EditorApplication.Exit(n);}
 static void Tick(){if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<2)return;try{
 if(stage==0){game=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();var player=UnityEngine.Object.FindAnyObjectByType<Player>();Check(game.BeginSelection(player)&&game.ChooseMode(EscapeGame.Mode.Easy),"start failed");robot=UnityEngine.Object.FindAnyObjectByType<EscapeCorridorCleaner>();Set(game.World,"barrierCountdown",9999f);Set(game.World,"professorCooldown",9999f);if(game.monster)game.monster.gameObject.SetActive(false);Place(player.GetComponent<CharacterController>(),new Vector3(500,.08f,0));Place(robot.GetComponent<CharacterController>(),new Vector3(527.5f,.08f,41f));
 var nav=Get(robot,"navigation");var v=(int[])Get(nav,"visits");for(int i=0;i<v.Length;i++)v[i]=1;int destination=game.schoolGrid.NearestCell(new Vector3(500,0,41));v[destination]=0;Check((bool)nav.GetType().GetMethod("Plan").Invoke(nav,new object[]{robot.transform.position}),"no path");Set(robot,"navigationReady",true);robot.GetType().GetMethod("SetNavigationDirection",F).Invoke(robot,null);Set(robot,"facingDirection",(Vector3)Get(robot,"direction"));Set(robot,"turning",false);Set(robot,"activeSeconds",8f);
 foreach(var door in (Door[])Get(robot,"doors"))if(door&&door.name=="Classroom Door 17")gate=door;Check(gate&&!gate.IsOpen,"gate missing or not closed");Physics.SyncTransforms();Time.timeScale=3;start=Time.time;stage=1;return;}
 if(robot.transform.position.x<509.5f){Check(gate.IsFullyOpen,"passed without opening gate");Debug.Log("GRID_REAL_GATE_PASS gate="+gate.name+" robot="+robot.transform.position);game.ExitToLobby();Finish(0);}else Check(Time.time-start<70,"gate traversal failed pos="+robot.transform.position+" gateOpen="+gate.IsOpen+" target="+Get(Get(robot,"navigation"),"<Target>k__BackingField"));
 }catch(Exception e){Debug.LogException(e);Finish(1);}}
}
