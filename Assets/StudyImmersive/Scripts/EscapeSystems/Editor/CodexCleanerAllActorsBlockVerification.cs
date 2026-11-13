using System;
using System.Reflection;
using CampusCommons.GrandLibrary;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class CodexCleanerAllActorsBlockVerification {
 const string Key="CodexCleanerAllActorsBlockVerification"; const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
 static EscapeGame game; static EscapeCorridorCleaner robot; static CharacterController pb,prof,monster; static int stage; static float start; static Vector3 releasePos; static float maxZ=-999f;
 static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o); static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
 static object Call(object o,string n,params object[] v)=>o.GetType().GetMethod(n,F).Invoke(o,v); static void Check(bool x,string s){if(!x)throw new Exception(s);} static void Place(CharacterController c,Vector3 p){c.enabled=false;c.transform.position=p;c.enabled=true;}
 public static void Run(){EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod] static void Resume(){EditorApplication.delayCall+=()=>{if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;};}
 static void Finish(int n){Time.timeScale=1;SessionState.SetBool(Key,false);EditorApplication.Exit(n);}
 static void Tick(){if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<2)return;try{
 if(stage==0){game=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();var player=UnityEngine.Object.FindAnyObjectByType<Player>();Check(game.BeginSelection(player)&&game.ChooseMode(EscapeGame.Mode.Easy),"start failed");robot=UnityEngine.Object.FindAnyObjectByType<EscapeCorridorCleaner>();pb=player.GetComponent<CharacterController>();prof=GameObject.Find("Professor").GetComponent<CharacterController>();Set(game.World,"barrierCountdown",9999f);Set(game.World,"professorCooldown",9999f);game.monster.SpeedUp();Set(game.monster,"wakeRemaining",100f);monster=game.monster.GetComponent<CharacterController>();
 Place(robot.GetComponent<CharacterController>(),new Vector3(527.5f,.08f,-27f));Set(robot,"direction",Vector3.forward);Set(robot,"facingDirection",Vector3.forward);Set(robot,"turning",false);Set(robot,"activeSeconds",0f);Set(robot,"speed",2.2f);
 var nav=Get(robot,"navigation");nav.GetType().GetProperty("Target").SetValue(nav,new Vector3(527.5f,.08f,-20f));Set(robot,"navigationReady",true);
 Place(pb,new Vector3(527.5f,.08f,-25.95f));Place(prof,new Vector3(527.5f,.08f,-25.95f));Place(monster,new Vector3(527.5f,.08f,-25.95f));var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="actor blocking wall";wall.transform.position=new Vector3(527.5f,1f,-24.5f);wall.transform.localScale=new Vector3(3,2,.2f);Physics.SyncTransforms();Call(robot,"CaptureNpcs",.1f);Check(robot.IsCarrying(prof)&&robot.IsCarrying(monster),"NPCs not captured");start=Time.time;Time.timeScale=2;stage=1;return;}
 if(stage==1){if(robot.IsPushing && robot.IsCarrying(prof) && robot.IsCarrying(monster)){stage=2;Debug.Log("ALL_ACTORS_BLOCK_CAPTURE_PASS");}else Check(Time.time-start<8,"no capture");}
 else if(stage==2){if(!robot.IsPushing&&!robot.IsCarrying(prof)&&!robot.IsCarrying(monster)){releasePos=robot.transform.position;stage=3;start=Time.time;Debug.Log("ALL_ACTORS_BLOCK_RELEASE_PASS at="+releasePos+" player="+pb.transform.position); }else Check(Time.time-start<10,"no release before wall");}
 else if(stage==3){maxZ=Mathf.Max(maxZ,robot.transform.position.z);if(robot.transform.position.z > -25.24f){Check(robot.transform.position.z < -24.8f && pb.transform.position.z< -24.5f && prof.transform.position.z< -24.5f && monster.transform.position.z< -24.5f,"actor crossed wall");Debug.Log("ALL_ACTORS_BLOCK_ROBOT_CONTINUES_PASS before="+releasePos+" after="+robot.transform.position);game.ExitToLobby();Finish(0);}else Check(Time.time-start<7,"robot still held back at "+robot.transform.position+" release="+releasePos+" maxZ="+maxZ+" ignore="+Physics.GetIgnoreCollision(robot.GetComponent<CharacterController>(),pb));}
 }catch(Exception e){Debug.LogException(e);Finish(1);}}
}
