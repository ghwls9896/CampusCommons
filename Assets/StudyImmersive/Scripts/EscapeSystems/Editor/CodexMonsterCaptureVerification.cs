using System;
using System.Reflection;
using CampusCommons.GrandLibrary;
using CampusCommons.LibraryCharacter;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class CodexMonsterCaptureVerification {
 const string Key="CodexMonsterCaptureVerification";const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
 static void Check(bool b,string s){if(!b)throw new Exception(s);}static void Place(CharacterController c,Vector3 p){c.enabled=false;c.transform.position=p;c.enabled=true;}
 public static void Run(){EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod]static void Resume(){EditorApplication.delayCall+=()=>{if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;};}
 static void Tick(){if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<2)return;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;try{
 var g=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();var p=UnityEngine.Object.FindAnyObjectByType<Player>();Check(g.BeginSelection(p)&&g.ChooseMode(EscapeGame.Mode.Easy),"start failed");var m=UnityEngine.Object.FindAnyObjectByType<Monster>();m.SpeedUp();Check(Mathf.Abs(m.CurrentMoveSpeed-3.157f)<.01f,"monster speed was not restored");typeof(Monster).GetField("wakeRemaining",F).SetValue(m,0f);m.enabled=false;
 var test=typeof(Monster).GetMethod("CanCaptureBetween",F);Func<Vector3,Vector3,Vector3,Vector3,bool> hit=(a,b,c,d)=>(bool)test.Invoke(m,new object[]{a,b,c,d});
 var a=new Vector3(1000,0,1000);var f=Vector3.forward;var right=Vector3.right;
 Check(hit(a,a+f,a+f*1.8f,a+f*2.8f),"same direction catch missed");
 Check(hit(a,a+f,a+right*1.8f+f*2f,a+right*1.8f-f),"opposite side pass missed");
 Check(hit(a,a+f*4,a+f*4,a),"between-frame pass missed");
 Check(!hit(a,a,a+right*2.1f,a+right*2.1f),"too far captured");
 Check(!hit(a,a+right*12,a+right*6,a+right*6),"teleport captured along route");
 Check(!hit(a,a,a+right+Vector3.up*3,a+right+Vector3.up*3),"different floor captured");
 var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=a+right*.9f+Vector3.up;wall.transform.localScale=new Vector3(.2f,3f,5f);Physics.SyncTransforms();
 Check(!hit(a,a,a+right*1.8f,a+right*1.8f),"caught through wall");UnityEngine.Object.DestroyImmediate(wall);
 Debug.Log("MONSTER_CAPTURE_GEOMETRY_PASS same_direction side_pass swept wall distance teleport floor");
 Place(m.GetComponent<CharacterController>(),a);Place(p.GetComponent<CharacterController>(),a+right*1.8f);Physics.SyncTransforms();typeof(Monster).GetMethod("Update",F).Invoke(m,null);
 Check(g.IsEnded,"actual capture did not end round");Debug.Log("MONSTER_CAPTURE_ROUND_PASS");g.ExitToLobby();EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
