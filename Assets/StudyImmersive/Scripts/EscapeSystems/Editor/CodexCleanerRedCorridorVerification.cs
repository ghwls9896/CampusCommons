using System;
using System.Reflection;
using CampusCommons.GrandLibrary;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class CodexCleanerRedCorridorVerification {
 const string Key="CodexCleanerRedCorridorVerification";const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
 public static void Run(){EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod]static void Resume(){EditorApplication.delayCall+=()=>{if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;};}
 static void Tick(){if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<2)return;SessionState.SetBool(Key,false);try{
 var game=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();var p=UnityEngine.Object.FindAnyObjectByType<Player>();if(!game.BeginSelection(p)||!game.ChooseMode(EscapeGame.Mode.Easy))throw new Exception("start failed");var robot=UnityEngine.Object.FindAnyObjectByType<EscapeCorridorCleaner>();var nav=typeof(EscapeCorridorCleaner).GetField("navigation",F).GetValue(robot);var t=nav.GetType();var on=t.GetMethod("OnCorridor",BindingFlags.NonPublic|BindingFlags.Static);var seen=(int[])t.GetField("seen",F).GetValue(nav);int stamp=(int)t.GetField("stamp",F).GetValue(nav);var map=game.schoolGrid;
 var points=new[]{new Vector3(472,0,-41),new Vector3(472,0,41),new Vector3(492.5f,0,-41),new Vector3(492.5f,0,-18.5f),new Vector3(492.5f,0,8.5f),new Vector3(485,0,41),new Vector3(500,0,41),new Vector3(500,0,8.5f),new Vector3(527.5f,0,41),new Vector3(527.5f,0,-46)};
 foreach(var point in points){int id=map.NearestCell(point);if(id<0||seen[id]!=stamp)throw new Exception("red corridor unreachable "+point+" nearest="+(id<0?"none":map.Position(id).ToString()));}
 int reachable=0;bool[] allowed=new bool[seen.Length];for(int i=0;i<seen.Length;i++)if(seen[i]==stamp){if(!(bool)on.Invoke(null,new object[]{map.Position(i)}))throw new Exception("room in route: "+map.Position(i));allowed[i]=true;reachable++;}
 var visits=(int[])t.GetField("visits",F).GetValue(nav);var arrive=t.GetMethod("Arrive");var target=t.GetProperty("Target");int steps=0,missing=reachable;
 while(steps<30000&&missing>0){Vector3 pos=(Vector3)target.GetValue(nav);if(!(bool)on.Invoke(null,new object[]{pos}))throw new Exception("target outside corridor "+pos);if(!(bool)arrive.Invoke(nav,new object[]{pos}))throw new Exception("no next path");steps++;if(steps%25==0){missing=0;for(int i=0;i<visits.Length;i++)if(allowed[i]&&visits[i]==0)missing++;}}
 if(missing>0)throw new Exception("unvisited="+missing+" of "+reachable);Debug.Log("RED_CORRIDOR_COVERAGE_PASS cells="+reachable+" segments="+steps+" all branches visited; no room cells");game.ExitToLobby();EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
