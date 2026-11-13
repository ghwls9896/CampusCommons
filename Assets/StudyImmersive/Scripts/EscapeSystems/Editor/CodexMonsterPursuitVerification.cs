using System;
using System.Reflection;
using CampusCommons.GrandLibrary;
using CampusCommons.LibraryCharacter;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class CodexMonsterPursuitVerification {
 const string Key="CodexMonsterPursuitVerification";
 public static void Run(){EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod]static void Resume(){EditorApplication.delayCall+=()=>{if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;};}
 static void Tick(){if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<2)return;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;try{var game=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();var player=UnityEngine.Object.FindAnyObjectByType<Player>();if(!game.BeginSelection(player)||!game.ChooseMode(EscapeGame.Mode.Easy))throw new Exception("start");var go=new GameObject("Monster pursuit verification");go.AddComponent<CodexMonsterPursuitDriver>().Setup(game,player);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
public sealed class CodexMonsterPursuitDriver:MonoBehaviour {
 const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;EscapeGame game;Monster monster;CharacterController body;float elapsed,initial;
 void Place(CharacterController c,Vector3 p){c.enabled=false;c.transform.position=p;c.enabled=true;}
 public void Setup(EscapeGame owner,Player p){try{game=owner;game.World.enabled=false;var cleaner=(EscapeCorridorCleaner)typeof(EscapeWorld).GetField("cleaner",F).GetValue(game.World);if(cleaner)cleaner.gameObject.SetActive(false);monster=UnityEngine.Object.FindAnyObjectByType<Monster>();body=p.GetComponent<CharacterController>();monster.SpeedUp();typeof(Monster).GetField("wakeRemaining",F).SetValue(monster,0f);Place(monster.GetComponent<CharacterController>(),new Vector3(527.5f,.08f,-38));Place(body,new Vector3(527.5f,.08f,-30));Physics.SyncTransforms();initial=Vector3.Distance(monster.transform.position,body.transform.position);Time.timeScale=1;}catch(Exception e){Fail(e);}}
 void Update(){try{if(!game||!monster)return;float dt=Time.deltaTime;elapsed+=dt;if(!game.IsEnded)body.Move(Vector3.forward*(2f*dt)+Vector3.down*(2f*dt));float gap=Vector3.Distance(monster.transform.position,body.transform.position);if(game.IsEnded||elapsed>3f){Debug.Log("CHASE_DIAG elapsed="+elapsed+" monster="+monster.transform.position+" player="+body.transform.position+" state="+monster.State);if(gap>=initial)throw new Exception("monster failed to close gap initial="+initial+" gap="+gap);Debug.Log("MONSTER_CHASE_RUN_PASS initial="+initial+" gap="+gap);Time.timeScale=1;game.ExitToLobby();EditorApplication.Exit(0);}}catch(Exception e){Fail(e);}}
 void Fail(Exception e){Debug.LogException(e);Time.timeScale=1;EditorApplication.Exit(1);}
}
