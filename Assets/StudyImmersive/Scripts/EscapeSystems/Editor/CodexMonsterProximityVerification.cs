using System;
using System.Reflection;
using CampusCommons.GrandLibrary;
using CampusCommons.LibraryCharacter;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class CodexMonsterProximityVerification {
 const string Key="CodexMonsterProximityVerification"; const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Instance;
 static EscapeGame game; static Monster monster; static Player player; static MonsterProximityEffect fx; static float start; static int stage;
 static void Check(bool x,string m){if(!x)throw new Exception(m);} static float Level()=> (float)typeof(MonsterProximityEffect).GetField("intensity",F).GetValue(fx);
 static void Place(float distance){var body=monster.GetComponent<CharacterController>();body.enabled=false;monster.transform.position=player.transform.position+Vector3.right*distance;body.enabled=true;Physics.SyncTransforms();}
 public static void Run(){EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod] static void Resume(){EditorApplication.delayCall+=()=>{if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;};}
 static void Tick(){if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<2)return;try{
 if(stage==0){game=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();player=UnityEngine.Object.FindAnyObjectByType<Player>();Check(game.BeginSelection(player)&&game.ChooseMode(Environment.GetEnvironmentVariable("CAMPUS_PROXIMITY_MODE")=="dark"?EscapeGame.Mode.Hard:EscapeGame.Mode.Easy),"start");monster=UnityEngine.Object.FindAnyObjectByType<Monster>();monster.SpeedUp();monster.enabled=false;fx=UnityEngine.Object.FindAnyObjectByType<MonsterProximityEffect>();Check(fx,"effect missing");Place(3);start=Time.time;stage=1;return;}
 if(stage==1 && Time.time-start>1.5f){Check(Level()>.8f,"near effect too low "+Level());var a=fx.GetComponent<AudioSource>();Check(a && a.clip && a.spatialBlend==0 && a.pitch>1.2f && a.volume>.4f,"heartbeat settings");Check((Texture2D)typeof(MonsterProximityEffect).GetField("grain",F).GetValue(fx),"grain missing");Debug.Log("PROXIMITY_NEAR_PASS intensity="+Level()+" pitch="+a.pitch);Place(35);start=Time.time;stage=2;return;}
 if(stage==2 && Time.time-start>2.7f){Check(Level()<.05f,"far effect did not fade "+Level());Debug.Log("PROXIMITY_FAR_FADE_PASS intensity="+Level());var camera=Camera.main;game.ExitToLobby();Check(!fx.GetComponent<AudioSource>().isPlaying,"heartbeat still playing");Check(camera && player.enabled,"camera/lobby restore failed");Debug.Log("PROXIMITY_EXIT_PASS");SessionState.SetBool(Key,false);EditorApplication.Exit(0);}
 }catch(Exception e){Debug.LogException(e);SessionState.SetBool(Key,false);EditorApplication.Exit(1);}}
}
