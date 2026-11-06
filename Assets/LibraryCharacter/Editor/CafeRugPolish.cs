using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace CampusCommons.LibraryCharacter.Editor {
public static class CafeRugPolish {
 [InitializeOnLoadMethod] static void Init(){EditorApplication.delayCall+=Run;}
 static void Run(){if(!File.Exists("Tools/cafe-rug-polish.request"))return;File.Delete("Tools/cafe-rug-polish.request");try{var s=SceneManager.GetActiveScene();if(s.path!="Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity"||EditorApplication.isPlaying)throw new Exception("Wrong scene");var rug=GameObject.Find("Grey cafe rug");if(!rug)throw new Exception("Cafe rug missing");rug.transform.rotation=Quaternion.Euler(-90,0,0);var rs=rug.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);rug.transform.position+=new Vector3(0,.025f,8.8f)-b.center;EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);File.WriteAllText("Tools/cafe-rug-polish-result.txt","PASS\n");}catch(Exception e){File.WriteAllText("Tools/cafe-rug-polish-result.txt",e.ToString());Debug.LogException(e);}}
}}
