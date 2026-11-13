using System;
using System.Collections.Generic;
using System.Reflection;
using CampusCommons.GrandLibrary;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class CodexNpcSpeedAndProfessorSoundVerification {
 const string Key="CodexNpcSpeedAndProfessorSoundVerification";const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 static object Get(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);static void Set(object o,string n,object v)=>o.GetType().GetField(n,F).SetValue(o,v);static void Check(bool b,string m){if(!b)throw new Exception(m);}
 public static void Run(){EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod]static void Init(){EditorApplication.delayCall+=()=>{if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;};}
 static void Tick(){if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<2)return;SessionState.SetBool(Key,false);try{
 var game=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();var player=UnityEngine.Object.FindAnyObjectByType<Player>();Check(game.BeginSelection(player)&&game.ChooseMode(EscapeGame.Mode.Easy),"game start failed");var world=game.World;world.enabled=false;var professor=Get(world,"professor");var worker=Get(world,"worker");var pa=(AudioSource)Get(professor,"audio");var wa=(AudioSource)Get(worker,"audio");Check(Mathf.Approximately(pa.volume,.8f)&&Mathf.Approximately(wa.volume,.5f),"wrong sound volumes");
 var ambient=typeof(EscapeWorld).GetMethod("UpdatePatrolAmbient",F);foreach(var npc in new[]{professor,worker}){var audio=(AudioSource)Get(npc,"audio");for(int i=0;i<20;i++){pa.Stop();wa.Stop();Set(npc,"ambientWait",0f);ambient.Invoke(world,new object[]{npc,.1f});float n=(float)Get(npc,"ambientWait");Check(npc==professor?n>=2.5f&&n<=4f:n>=4.5f&&n<=7.5f,"incorrect interval "+n);}}
 Set(world,"doors",Array.Empty<CampusCommons.LibraryCharacter.Door>());Set(world,"professorCooldown",100f);int k=0;foreach(var npc in new[]{professor,worker}){var body=(CharacterController)Get(npc,"body");body.enabled=false;body.transform.position=new Vector3(1000+k*4,.08f,1000);body.enabled=true;var route=(List<Vector3>)Get(npc,"route");route.Clear();route.Add(new Vector3(1000+k*4,0,1004));Set(npc,"next",0);Set(npc,"replan",10f);Set(npc,"hasDestination",true);k++;}Physics.SyncTransforms();var pp=((CharacterController)Get(professor,"body")).transform.position;var wp=((CharacterController)Get(worker,"body")).transform.position;
 typeof(EscapeWorld).GetMethod("UpdatePatrols",F).Invoke(world,new object[]{.1f});float pd=((CharacterController)Get(professor,"body")).transform.position.z-pp.z;float wd=((CharacterController)Get(worker,"body")).transform.position.z-wp.z;Check(Mathf.Abs(pd-.485f)<.02f&&Mathf.Abs(wd-.445f)<.02f,"unexpected speeds "+pd+","+wd);Debug.Log("NPC_TUNING_PASS professor="+pd*10+" worker="+wd*10+" professor volume 0.8 and 2.5-4s interval");game.ExitToLobby();EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
