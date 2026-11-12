using System;
using System.Linq;
using CampusCommons.GrandLibrary;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class EscapeSpawnVerification
{
 const string Pending="EscapeSpawnVerificationPending";static int stage;static float start;static EscapeGame game;static Player player;static CharacterController body;
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 public static void Run(){EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod]static void Resume(){EditorApplication.delayCall+=()=>{if(SessionState.GetBool(Pending,false))EditorApplication.update+=Tick;};}
 static void Finish(int code){EditorApplication.update-=Tick;SessionState.SetBool(Pending,false);EditorApplication.Exit(code);}
 static void Tick(){if(!EditorApplication.isPlaying)return;try{
  if(stage==0){if(Time.timeSinceLevelLoad<1.5f)return;game=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();player=UnityEngine.Object.FindAnyObjectByType<Player>();body=player.GetComponent<CharacterController>();var door=UnityEngine.Object.FindObjectsByType<LobbyDoor>().First(d=>d.escape==game);body.enabled=false;player.transform.position=door.transform.position;body.enabled=true;Physics.SyncTransforms();Check(door.TryEnter(player),"Lobby door failed");start=Time.time;stage=1;}
  else if(stage==1 && Time.time-start>.8f){var wanted=new Vector3(500,.08f,46);Check(game.IsSelecting && Vector3.Distance(player.transform.position,wanted)<.1f,"Mode selection did not spawn in 101 alcove");Check(Vector3.Dot(Camera.main.transform.forward,Vector3.back)>.95f,"Spawn camera does not face the corridor");Check(game.ChooseMode(EscapeGame.Mode.Easy),"Mode start failed");Check(Vector3.Distance(player.transform.position,wanted)<.1f,"Game start did not preserve spawn");Physics.SyncTransforms();float before=player.transform.position.z;body.Move(Vector3.back*2f);Check(player.transform.position.z<before-1.8f,"Spawn cannot move into corridor");game.ExitToLobby();Check(player.enabled && player.currentSpace==Portal.Space.Library,"Lobby return failed");Debug.Log("CC15_SPAWN_PLAY_PASS 101 alcove, south-facing camera, clear movement, lobby return");Finish(0);}
 }catch(Exception ex){Debug.LogException(ex);Finish(1);}}
}
