using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace CampusCommons.LibraryCharacter.Editor {
public static class CafeRemovePlatform {
 [InitializeOnLoadMethod] static void Init(){EditorApplication.delayCall+=Run;}
 static void Run(){if(!File.Exists("Tools/cafe-remove-platform.request"))return;File.Delete("Tools/cafe-remove-platform.request");try{Apply();}catch(Exception e){File.WriteAllText("Tools/cafe-remove-platform-result.txt",e.ToString());Debug.LogException(e);}}
 static void Apply(){var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity"||EditorApplication.isPlaying)throw new Exception("Wrong scene");var f=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshFilter>(true)).First(x=>x.sharedMesh&&x.sharedMesh.name=="DoorUpdate_LIB_Library__stone__SOLID");f.GetComponent<Renderer>().enabled=false;var c=f.GetComponent<Collider>();if(c)c.enabled=false;EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);File.WriteAllText("Tools/cafe-remove-platform-result.txt","PASS\n");}
}}
