using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace CampusCommons.LibraryCharacter.Editor {
public static class CafeFinalPreview {
 [InitializeOnLoadMethod] static void Init(){EditorApplication.delayCall+=Run;}
 static void Run(){if(!File.Exists("Tools/cafe-final-preview.request"))return;File.Delete("Tools/cafe-final-preview.request");try{if(EditorApplication.isPlaying||SceneManager.GetActiveScene().path!="Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity")throw new Exception("Wrong scene");Directory.CreateDirectory("Design/CafeUpdate");Render("final-atrium.png",new Vector3(-2,5,-13),new Vector3(0,10,2),70);Render("final-cafe.png",new Vector3(0,2.7f,5.8f),new Vector3(0,1.8f,10.8f),65);Render("final-doors.png",new Vector3(6,3,-3),new Vector3(14,2,0),70);File.WriteAllText("Tools/cafe-final-preview-result.txt","PASS\n");}catch(Exception e){File.WriteAllText("Tools/cafe-final-preview-result.txt",e.ToString());Debug.LogException(e);}}
 static void Render(string name,Vector3 pos,Vector3 target,float fov){var go=new GameObject("Final preview camera"){hideFlags=HideFlags.HideAndDontSave};var cam=go.AddComponent<Camera>();cam.transform.position=pos;cam.transform.LookAt(target);cam.fieldOfView=fov;cam.nearClipPlane=.08f;cam.farClipPlane=150;cam.clearFlags=CameraClearFlags.Skybox;var rt=new RenderTexture(1280,800,24){antiAliasing=2};cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var img=new Texture2D(1280,800,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,1280,800),0,0);img.Apply();File.WriteAllBytes("Design/CafeUpdate/"+name,img.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(img);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
}}
 
