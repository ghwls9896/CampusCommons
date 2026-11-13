using System;
using System.Reflection;
using CampusCommons.GrandLibrary;
using CampusCommons.StudyImmersive;
using CampusCommons.LibraryCharacter;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class CodexMonsterSoundProbe {
 const string K="CodexMonsterSoundProbe";
 public static void Run(){EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");SessionState.SetBool(K,true);EditorApplication.EnterPlaymode();}
 [InitializeOnLoadMethod] static void Resume(){EditorApplication.delayCall+=()=>{if(SessionState.GetBool(K,false))EditorApplication.update+=Tick;};}
 static void Tick(){if(!EditorApplication.isPlaying||Time.timeSinceLevelLoad<2)return;SessionState.SetBool(K,false);try{
 var g=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();var p=UnityEngine.Object.FindAnyObjectByType<Player>();if(!g.BeginSelection(p)||!g.ChooseMode(EscapeGame.Mode.Easy))throw new Exception("start failed");
 var m=UnityEngine.Object.FindAnyObjectByType<Monster>();var listeners=UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);var cam=Camera.main;
 if (!cam || listeners.Length!=1 || !listeners[0].isActiveAndEnabled || listeners[0].gameObject!=cam.gameObject) throw new Exception("listener must be on player camera");
 Debug.Log("SOUND_PROBE_CAMERA "+(cam?cam.name:"none")+" listenerCount="+listeners.Length);foreach(var x in listeners)Debug.Log("SOUND_PROBE_LISTENER "+x.name+" active="+x.isActiveAndEnabled+" camera="+(cam&&x.gameObject==cam.gameObject));
 foreach(var a in m.GetComponents<AudioSource>()) { if(a.spatialBlend!=1f) throw new Exception("monster source must be 3D"); Debug.Log("SOUND_PROBE_MONSTER spatial="+a.spatialBlend+" min="+a.minDistance+" max="+a.maxDistance+" rolloff="+a.rolloffMode); }
 if(!m.footstepClip || m.footstepClip.channels!=1 || !m.alertClip || m.alertClip.channels!=1) throw new Exception("monster audio must be mono");
 Debug.Log("SOUND_PROBE_CLIPS foot="+(m.footstepClip?m.footstepClip.channels:0)+" alert="+(m.alertClip?m.alertClip.channels:0));
 g.ExitToLobby();EditorApplication.Exit(0);
 }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
