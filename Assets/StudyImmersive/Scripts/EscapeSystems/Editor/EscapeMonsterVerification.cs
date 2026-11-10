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
public static class EscapeMonsterVerification
{
 const string Pending="EscapeMonsterVerificationPending";static int stage;static float start;static Vector3 original;static EscapeGame game;static Player player;static Monster monster;
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
 public static void Run(){EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod]static void Resume(){EditorApplication.delayCall+=()=>{if(SessionState.GetBool(Pending,false))EditorApplication.update+=Tick;};}
 static void Finish(int code){EditorApplication.update-=Tick;SessionState.SetBool(Pending,false);EditorApplication.Exit(code);}
 static void Tick(){if(!EditorApplication.isPlaying)return;try{
 if(stage==0){if(Time.timeSinceLevelLoad<1.5f)return;game=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();player=UnityEngine.Object.FindAnyObjectByType<Player>();monster=UnityEngine.Object.FindAnyObjectByType<Monster>();Check(game.BeginSelection(player)&&game.ChooseMode(EscapeGame.Mode.Easy),"Could not start");original=monster.transform.position;var ahead=original-player.transform.position;ahead.y=0;var forward=Camera.main.transform.forward;forward.y=0;Check(ahead.magnitude>=2.3f && ahead.magnitude<=5.5f && Vector3.Dot(ahead.normalized,forward.normalized)>.9f,"Monster did not spawn in front of player: "+original);start=Time.time;stage=1;}
 else if(stage==1&&Time.time-start>1.5f){Check(!monster.IsAwakened && Vector3.Distance(original,monster.transform.position)<.05f && monster.picture.gameObject.activeSelf,"Monster not visible or moved before wrong answer");var sheet=UnityEngine.Object.FindObjectsByType<Sheet>().First();Check(game.OpenSheet(sheet.gameObject),"Could not open sheet");Check(game.SubmitAnswer((game.CurrentCorrectAnswer+1)%4),"Could not submit wrong answer");Check(monster.IsAwakened && monster.picture.gameObject.activeSelf && monster.State==Monster.Behaviour.Chase,"First error did not reveal and begin pursuit");Check(((Vector3)typeof(Monster).GetField("goal",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(monster)-player.transform.position).sqrMagnitude<1f,"First pursuit has no player target");Check(game.ContinueQuestion(),"Question continue failed");for(int i=0;i<2;i++){Check(game.SubmitAnswer(game.CurrentCorrectAnswer)&&game.ContinueQuestion(),"Could not finish sheet");}start=Time.time;stage=2;}
 else if(stage==2&&Time.time-start>4f){float moved=Vector3.Distance(original,monster.transform.position);Check(moved>.15f || game.Caught,"Awakened monster did not move");Debug.Log("CC15_MONSTER_PASS waited before wrong answer, targeted player and moved after answer; distance="+moved);Finish(0);}
 }catch(Exception e){Debug.LogException(e);Finish(1);}}
}
