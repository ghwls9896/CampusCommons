using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CampusCommons.GrandLibrary;
using CampusCommons.LibraryCharacter;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class EscapeWorldPatrolVerification
{
 const string Pending="CC15PatrolTestPending";static int stage;static float start;static EscapeGame game;static EscapeWorld world;static Vector3 profStart,workerStart;static GameObject professor,worker;
 static void Check(bool ok,string m){if(!ok)throw new Exception(m);}
 public static void Run(){EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod]static void Resume(){EditorApplication.delayCall+=()=>{if(SessionState.GetBool(Pending,false))EditorApplication.update+=Tick;};}
 static void Finish(int n){Time.timeScale=1;EditorApplication.update-=Tick;SessionState.SetBool(Pending,false);EditorApplication.Exit(n);}
 static void Tick(){if(!EditorApplication.isPlaying)return;try{
 if(stage==0){if(Time.timeSinceLevelLoad<1.5f)return;game=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();var player=UnityEngine.Object.FindAnyObjectByType<Player>();Check(game.BeginSelection(player)&&game.ChooseMode(EscapeGame.Mode.Easy),"Could not begin");world=game.World;professor=GameObject.Find("Professor");worker=GameObject.Find("Maintenance Worker");Check(professor&&worker,"NPC missing");Check(professor.GetComponentInChildren<SpriteRenderer>()?.sprite && worker.GetComponentInChildren<SpriteRenderer>()?.sprite,"NPC sprite missing");Check(world.ItemTexture(EscapeGame.InventoryItem.Soda)&&world.ItemTexture(EscapeGame.InventoryItem.Padlock)&&world.ItemTexture(EscapeGame.InventoryItem.ExcuseNote),"Item texture missing");profStart=professor.transform.position;workerStart=worker.transform.position;start=Time.time;stage=1;}
 else if(stage==1 && Time.time-start>5f){float p=Vector3.Distance(profStart,professor.transform.position),w=Vector3.Distance(workerStart,worker.transform.position);Debug.Log("CC15_NPC_MOVEMENT professor="+p+" worker="+w);Check(p>.15f&&w>.15f,"Patrol actor did not move");typeof(EscapeWorld).GetField("barrierCountdown",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(world,0f);start=Time.time;Time.timeScale=3f;stage=2;}
 else if(stage==2){if(world.ActiveBarrierCount>0){var path=new List<Vector3>();var player=UnityEngine.Object.FindAnyObjectByType<Player>();foreach(var t in game.PendingSheetPositions)Check(game.schoolGrid.FindPath(player.transform.position,t,path,(a,b)=>!world.IsEdgeBlocked(a,b)),"Barrier blocks pending sheet");bool exit=false;foreach(var t in game.ExitPositions)if(game.schoolGrid.FindPath(player.transform.position,t,path,(a,b)=>!world.IsEdgeBlocked(a,b))){exit=true;break;}Check(exit,"Barrier blocks all exits");Debug.Log("CC15_PATROL_PASS both NPCs move; worker timed barrier preserves routes");Finish(0);}else if(Time.time-start>90f)throw new Exception("Worker did not find a safe timed barrier in 90 game seconds");}
 }catch(Exception e){Debug.LogException(e);Finish(1);}}
}
