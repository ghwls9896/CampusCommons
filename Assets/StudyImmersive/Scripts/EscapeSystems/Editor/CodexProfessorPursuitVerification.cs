using System;
using System.Reflection;
using CampusCommons.GrandLibrary;
using CampusCommons.LibraryCharacter;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class CodexProfessorPursuitVerification {
 const string Key="CodexProfessorPursuitVerification"; const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance;
 static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o); static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);
 static void Check(bool x,string m){if(!x)throw new Exception(m);}
 static void Place(CharacterController c,Vector3 p){c.enabled=false;c.transform.position=p;c.enabled=true;}
 public static void Run(){EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod] static void Resume(){EditorApplication.delayCall+=()=>{if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;};}
 static void Tick(){if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<2)return;SessionState.SetBool(Key,false);try{
 var game=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();var player=UnityEngine.Object.FindAnyObjectByType<Player>();Check(game.BeginSelection(player)&&game.ChooseMode(EscapeGame.Mode.Easy),"start");var world=game.World;world.enabled=false;var npc=Get(world,"professor");var prof=(CharacterController)Get(npc,"body");var body=player.GetComponent<CharacterController>();
 Place(prof,new Vector3(527.5f,.08f,-38));Place(body,new Vector3(527.5f,.08f,-30));Physics.SyncTransforms();
 var see=typeof(EscapeWorld).GetMethod("CanSeePlayer",F);Check((bool)see.Invoke(world,new object[]{12f}),"professor could not see player in clear corridor");
 var update=typeof(EscapeWorld).GetMethod("UpdatePatrols",F);float initial=Vector3.Distance(prof.transform.position,body.transform.position);
 for(int i=0;i<50 && !world.BlocksMovement;i++){body.Move(Vector3.forward*(7.02f*.05f)+Vector3.down*.1f);Physics.SyncTransforms();update.Invoke(world,new object[]{.05f});}
 float after=Vector3.Distance(prof.transform.position,body.transform.position);Check(after<initial-1f,"professor failed to close gap while player ran: before="+initial+" after="+after);Debug.Log("PROFESSOR_CHASE_RUN_PASS before="+initial+" after="+after);
 Check((bool)Get(npc,"pursuing") || world.BlocksMovement,"professor not pursuing");
 if(!world.BlocksMovement){var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.position=(prof.transform.position+body.transform.position)*.5f+Vector3.up;blocker.transform.localScale=new Vector3(3,3,.5f);Physics.SyncTransforms();Check(!(bool)see.Invoke(world,new object[]{12f}),"blocker did not break sight");update.Invoke(world,new object[]{.1f});Check((bool)Get(npc,"pursuing"),"professor forgot player immediately after losing sight");Debug.Log("PROFESSOR_CHASE_MEMORY_PASS");UnityEngine.Object.Destroy(blocker);}
 game.ExitToLobby();EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
