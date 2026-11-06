using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CampusCommons.GrandLibrary;
using CampusCommons.LibraryCharacter;
using CampusCommons.StudyImmersive;
public static class CampusEscapeCorrection {
 const string ScenePath="Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity", Backup="Assets/LibraryCharacter/Backups/GrandLibrary_before_Korean_20261106.unity";
 [InitializeOnLoadMethod] static void Init(){if(File.Exists("Library/campus-correction.request"))EditorApplication.delayCall+=Run;}
 static void Check(bool value,string error){if(!value)throw new Exception(error);}
 static Transform[] All(Scene s)=>s.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>(true)).ToArray();
 static string Key(Transform t){var parts=new List<string>();while(t){int ordinal=0;if(t.parent){for(int i=0;i<t.GetSiblingIndex();i++)if(t.parent.GetChild(i).name==t.name)ordinal++;}parts.Add(t.name+"["+ordinal+"]");t=t.parent;}parts.Reverse();return string.Join("/",parts);}
 static void Run(){
  if(!File.Exists("Library/campus-correction.request"))return;File.Delete("Library/campus-correction.request");Scene baseline=default;
  try{
   var scene=SceneManager.GetActiveScene();Check(!EditorApplication.isPlayingOrWillChangePlaymode&&scene.path==ScenePath,"Open the actual scene in Edit mode");
   string before=AssetDatabase.GenerateUniqueAssetPath("Assets/LibraryCharacter/Backups/GrandLibrary_before_escape_correction.unity");Check(EditorSceneManager.SaveScene(scene,before,true),"Current scene backup failed");
   baseline=EditorSceneManager.OpenScene(Backup,OpenSceneMode.Additive);var old=All(baseline).ToDictionary(Key);var cur=All(scene);var escape=cur.Single(t=>t.name=="Maps").Find("Escape Room");Check(escape,"Escape room missing");
   int restored=0;foreach(var t in cur.Where(t=>!t.IsChildOf(escape))){
    if(!old.TryGetValue(Key(t),out var source))continue;var text=t.GetComponent<TextMesh>();var original=source.GetComponent<TextMesh>();
    if(text&&original){Undo.RecordObject(text,"Restore original room text");text.text=original.text;text.font=original.font;var r=text.GetComponent<Renderer>();var oldr=original.GetComponent<Renderer>();if(r&&oldr){Undo.RecordObject(r,"Restore original room text material");r.sharedMaterials=oldr.sharedMaterials;}restored++;}
    if(t.name=="Mafia Wall Sign"||t.name=="SIGN_Quiz_QUIZ  1  VS  1"||t.name=="SIGN_Quiz_READY  /  THINK  /  ANSWER"){var r=t.GetComponent<Renderer>();var oldr=source.GetComponent<Renderer>();Check(r&&oldr,"Original room sign missing");Undo.RecordObject(r,"Restore original room sign");r.enabled=oldr.enabled;}
    var atlas=t.GetComponent<WorldTextFontAtlas>();var oldAtlas=source.GetComponent<WorldTextFontAtlas>();if(atlas&&oldAtlas){Undo.RecordObject(atlas,"Restore original lobby font");atlas.font=oldAtlas.font;atlas.depthMaterial=oldAtlas.depthMaterial;}
   }
   foreach(string n in new[]{"Mafia Room Sign","Quiz Room Sign"}){var generated=cur.SingleOrDefault(t=>t && t.name==n);if(generated)Undo.DestroyObjectImmediate(generated.gameObject);}
   var welcome=cur.Single(t=>t && t.name=="University Welcome");Undo.SetTransformParent(welcome,escape,"Move welcome sign to escape room");Undo.RecordObject(welcome,"Position escape welcome sign");welcome.SetPositionAndRotation(new Vector3(475f,2.13f,42.43f),Quaternion.identity);welcome.localScale=Vector3.one*.43f;
   foreach(var child in welcome.Cast<Transform>().Where(t=>t.name.EndsWith("Mount")).ToArray())Undo.DestroyObjectImmediate(child.gameObject);
   var atlasK=escape.GetComponent<WorldTextFontAtlas>();if(!atlasK)atlasK=Undo.AddComponent<WorldTextFontAtlas>(escape.gameObject);atlasK.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/StudyImmersive/Resources/KIT/NotoSansKR.ttf");atlasK.depthMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/StudyImmersive/Art/KIT/KIT Korean Text.mat");Check(atlasK.font&&atlasK.depthMaterial,"Korean font assets missing");atlasK.depthMaterial.mainTexture=atlasK.font.material.mainTexture;
   var doors=escape.GetComponentsInChildren<Door>(true).Where(d=>d.name.StartsWith("Classroom Door ")&&!d.secondHinge).OrderBy(d=>d.name).ToArray();Check(doors.Length==14,"Classroom count changed");
   int plaques=0;foreach(var d in doors){var plates=d.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Room Number").ToArray();Check(plates.Length==2,"Missing plaque on "+d.name);foreach(var plate in plates){Check(plate.IsChildOf(d.hinge),"Plaque does not follow hinge: "+d.name);var beforePos=plate.localPosition;var beforeRot=plate.localRotation;var closed=plate.position;var rot=d.hinge.localRotation;d.hinge.localRotation=Quaternion.Euler(0,70,0);Check((plate.position-closed).magnitude>.01f,"Plaque did not move: "+d.name);Check(plate.localPosition==beforePos&&plate.localRotation==beforeRot,"Plaque local transform changed");d.hinge.localRotation=rot;plaques++;}}
   foreach(char c in "금오공과대학교에 오신 것을 환영합니다디지털관 호인공지능 중심 대학 선정 경축출구0123456789")if(!char.IsWhiteSpace(c))Check(atlasK.font.HasCharacter(c),"Missing font character: "+c);
   Check(!All(scene).Any(t=>t.name=="University Welcome"&&!t.IsChildOf(escape)),"Welcome still outside escape room");
   EditorSceneManager.CloseScene(baseline,true);baseline=default;EditorSceneManager.SetActiveScene(scene);EditorSceneManager.MarkSceneDirty(scene);Check(EditorSceneManager.SaveScene(scene),"Scene save failed");AssetDatabase.SaveAssets();
   Render();File.WriteAllText("Tools/campus-correction-result.txt","PASS\nbackup="+before+"\nrestoredTexts="+restored+"\nclassrooms="+doors.Length+"\nplaquesMoved="+plaques+"\nfontGlyphs=PASS\n");Debug.Log("CAMPUS_ESCAPE_CORRECTION_PASS rooms="+doors.Length+" plaques="+plaques);
  }catch(Exception e){if(baseline.IsValid()&&baseline.isLoaded)EditorSceneManager.CloseScene(baseline,true);File.WriteAllText("Tools/campus-correction-result.txt","FAIL "+e);Debug.LogException(e);}
 }
 static void Render(){Directory.CreateDirectory("Design/KoreanCampus");var o=new GameObject("Escape welcome preview"){hideFlags=HideFlags.HideAndDontSave};var c=o.AddComponent<Camera>();c.transform.position=new Vector3(475,1.7f,39.7f);c.transform.LookAt(new Vector3(475,2.13f,42.43f));c.fieldOfView=65;c.nearClipPlane=.04f;c.farClipPlane=30;var rt=new RenderTexture(1200,750,24);c.targetTexture=rt;c.Render();RenderTexture.active=rt;var img=new Texture2D(1200,750,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,1200,750),0,0);img.Apply();File.WriteAllBytes("Design/KoreanCampus/escape-welcome.png",img.EncodeToPNG());c.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(img);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(o);}
}

