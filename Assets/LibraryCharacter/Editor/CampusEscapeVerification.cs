using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CampusCommons.GrandLibrary;
using CampusCommons.LibraryCharacter;
using CampusCommons.StudyImmersive;
public static class CampusEscapeVerification {
 const string Pending="CampusEscapeVerificationPending";static int stage;static float start;static Player player;static EscapeGame game;static Door door;static Transform plaque;static Vector3 plaqueClosed;static string error;
 [InitializeOnLoadMethod] static void Init(){EditorApplication.delayCall+=Resume;}
 static void Check(bool v,string m){if(!v)throw new Exception(m);}
 static void Resume(){
  if(File.Exists("Library/campus-verify.request")){File.Delete("Library/campus-verify.request");try{var scene=SceneManager.GetActiveScene();Check(!EditorApplication.isPlayingOrWillChangePlaymode&&scene.path=="Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity","Wrong scene");var welcome=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="University Welcome");Undo.RecordObject(welcome,"Reposition welcome sign");welcome.position=new Vector3(473.5f,2.13f,42.43f);EditorSceneManager.MarkSceneDirty(scene);Check(EditorSceneManager.SaveScene(scene),"Could not save");SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();}catch(Exception e){File.WriteAllText("Tools/campus-play-verification.txt","FAIL "+e);Debug.LogException(e);}return;}
  if(SessionState.GetBool(Pending,false)&&EditorApplication.isPlayingOrWillChangePlaymode){stage=0;error=null;Application.logMessageReceived+=Log;EditorApplication.update+=Tick;}
 }
 static void Log(string m,string stack,LogType type){if((type==LogType.Error||type==LogType.Exception)&&!(stack??"").Contains("UnityEditor.Search.SearchDatabase"))error=m;}
 static void Tick(){if(!EditorApplication.isPlaying)return;try{
  if(stage==0){if(Time.timeSinceLevelLoad<1.2f)return;player=UnityEngine.Object.FindAnyObjectByType<Player>();game=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();Check(player&&game&&Camera.main,"Player/game/camera missing");bool was=player.IsFirstPerson;player.ToggleView();Check(player.IsFirstPerson!=was,"Camera did not toggle");player.ToggleView();Check(player.IsFirstPerson==was,"Camera did not return");Check(game.BeginSelection(player),"Escape selection failed");if(game.monster)game.monster.graceSeconds=100;Check(game.ChooseMode(EscapeGame.Mode.Easy),"Escape start failed");Check(game.ActiveNotebookCount==3&&game.Target==3,"Question sheets failed to spawn");Check(game.IsFirstPerson&&game.ElevatorCount==3,"Escape camera/elevators missing");door=UnityEngine.Object.FindObjectsByType<Door>().First(x=>!x.secondHinge&&x.GetComponentsInChildren<Transform>().Any(t=>t.name=="Room Number"));plaque=door.GetComponentsInChildren<Transform>().First(t=>t.name=="Room Number");plaqueClosed=plaque.position;door.Interact();Check(door.IsOpen,"Door did not open");start=Time.time;stage=1;}
  else if(stage==1&&Time.time-start>.6f){Check(door.IsFullyOpen,"Door did not rotate open");Check(Vector3.Distance(plaque.position,plaqueClosed)>.01f&&plaque.IsChildOf(door.hinge),"Plaque did not follow moving door");var sheets=UnityEngine.Object.FindObjectsByType<Sheet>();Check(sheets.Length==3,"Sheets missing");foreach(var sheet in sheets){Check(game.OpenSheet(sheet.gameObject),"Could not open sheet");for(int i=0;i<3;i++){Check(game.CurrentCorrectAnswer>=0,"Question missing");Check(game.SubmitAnswer(game.CurrentCorrectAnswer),"Answer failed");Check(game.ContinueQuestion(),"Continue failed");}}Check(game.Found==3&&game.ReadyToExit,"Question collection failed");UnityEngine.Object.FindAnyObjectByType<ElevatorExit>().Interact();Check(game.Completed,"Elevator exit failed");game.ExitToLobby();Check(player.enabled&&player.currentSpace==Portal.Space.Library,"Return to lobby failed");Check(error==null,"Runtime error: "+error);File.WriteAllText("Tools/campus-play-verification.txt","PASS\nCamera toggle and first person; three sheets and nine answers; hinged door and attached plaque; elevator completion and return.\n");Finish();}
 }catch(Exception e){File.WriteAllText("Tools/campus-play-verification.txt","FAIL "+e);Debug.LogException(e);Finish();}}
 static void Finish(){Application.logMessageReceived-=Log;EditorApplication.update-=Tick;SessionState.SetBool(Pending,false);EditorApplication.ExitPlaymode();}
}
