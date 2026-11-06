using System;
using System.IO;
using System.Linq;
using System.Text;
using CampusCommons.LibraryCharacter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace CampusCommons.LibraryCharacter.Editor {
 public static class KoreanSignAudit {
  const string Request="Library/korean-sign-audit.request";
  [InitializeOnLoadMethod]static void Init(){if(File.Exists(Request))EditorApplication.delayCall+=Snapshot;}
  static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
  static void Snapshot(){
   if(!File.Exists(Request))return;File.Delete(Request);
   try{
    var scene=SceneManager.GetActiveScene();
    if(EditorApplication.isPlayingOrWillChangePlaymode||scene.path!="Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity")throw new Exception("Open GrandLibrary_Living and stop Play.");
    string backup=AssetDatabase.GenerateUniqueAssetPath("Assets/LibraryCharacter/Backups/GrandLibrary_before_korean_signs.unity");
    if(!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Scene snapshot failed");
    Inspect("Tools/korean-sign-audit.txt","backup="+backup+" dirty="+scene.isDirty);
   }catch(Exception e){File.WriteAllText("Tools/korean-sign-audit.txt","FAIL "+e);Debug.LogException(e);}
  }
  static void Inspect(string file,string info){
   var scene=SceneManager.GetActiveScene();var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();var s=new StringBuilder(info+" objects="+all.Length+"\n");
   foreach(var t in all){
    var text=t.GetComponent<TextMesh>();var r=t.GetComponent<Renderer>();
    if(text){s.AppendLine("TEXT "+PathOf(t)+" active="+t.gameObject.activeInHierarchy+" value="+text.text.Replace("\n"," / ")+" pos="+t.position.ToString("F3")+" rot="+t.eulerAngles.ToString("F1")+" fontSize="+text.fontSize+" size="+text.characterSize+" bounds="+(r?r.bounds.size.ToString("F2"):"none"));continue;}
    if(t.name.StartsWith("SIGN_")||t.name=="Mafia Wall Sign")s.AppendLine("MESH_SIGN "+PathOf(t)+" active="+t.gameObject.activeInHierarchy+" render="+(r?r.enabled:false)+" bounds="+(r?r.bounds.center.ToString("F2")+"/"+r.bounds.size.ToString("F2"):"none"));
    var door=t.GetComponent<Door>();if(door){s.AppendLine("DOOR "+PathOf(t)+" pos="+t.position.ToString("F2")+" rot="+t.eulerAngles.ToString("F1")+" second="+(bool)door.secondHinge);
      foreach(var c in t.GetComponentsInChildren<Transform>(true).Where(x=>x.name=="Door Texture"))s.AppendLine(" FACE "+PathOf(c)+" pos="+c.position.ToString("F2")+" rot="+c.eulerAngles.ToString("F1")+" scale="+c.lossyScale.ToString("F2"));}
   }
   File.WriteAllText(file,s.ToString());
  }
  public static void RunBatch(){EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");Inspect("Tools/korean-sign-audit.txt","stage");}
 }
}

