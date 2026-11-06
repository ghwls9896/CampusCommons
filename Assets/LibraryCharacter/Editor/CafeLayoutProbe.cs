using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace CampusCommons.LibraryCharacter.Editor {
public static class CafeLayoutProbe {
 [InitializeOnLoadMethod] static void Init(){ EditorApplication.delayCall += Run; }
 static void Run(){ if(!File.Exists("Tools/cafe-layout-probe.request"))return; File.Delete("Tools/cafe-layout-probe.request"); try{Inspect();}catch(Exception e){File.WriteAllText("Tools/cafe-layout-probe-error.txt",e.ToString());Debug.LogException(e);} }
 static void Inspect(){
  var scene=SceneManager.GetActiveScene();var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray(); var sb=new StringBuilder(); sb.AppendLine(scene.path+" dirty="+scene.isDirty);
  foreach(var f in all.Select(t=>t.GetComponent<MeshFilter>()).Where(f=>f&&f.sharedMesh&&f.gameObject.activeInHierarchy)){
   string n=f.sharedMesh.name; if(!n.StartsWith("LIB_Library__")) continue; var v=f.sharedMesh.vertices; int cnt=0;var b=new Bounds();
   foreach(var i in f.sharedMesh.triangles){var p=f.transform.TransformPoint(v[i]); if(p.x>12.2f&&p.x<15.5f&&p.y<5.1f&&new[]{-5.5f,0f,5.5f}.Any(z=>Mathf.Abs(p.z-z)<2.3f)){if(cnt==0)b=new Bounds(p,Vector3.zero);else b.Encapsulate(p);cnt++;}}
   if(cnt>0)sb.AppendLine("PORTAL_GEOMETRY "+n+" vertices="+cnt+" bounds="+b.center.ToString("F2")+" / "+b.size.ToString("F2"));
  }
  foreach(var r in all.Select(t=>t.GetComponent<Renderer>()).Where(r=>r&&r.enabled&&r.gameObject.activeInHierarchy)){
   var b=r.bounds; if(b.size.x>10||b.size.z>10||b.size.y>7)continue;var p=b.center; if(p.y<3.5f&&p.x>-13&&p.x<7&&p.z>8&&p.z<17)sb.AppendLine("CAFE_AREA "+r.name+" parent="+(r.transform.parent?r.transform.parent.name:"-")+" bounds="+p.ToString("F2")+" / "+b.size.ToString("F2"));
  }
  File.WriteAllText("Tools/cafe-layout-probe.txt",sb.ToString());Directory.CreateDirectory("Design/CafeUpdate");
  Render("before-top.png",new Vector3(0,30,0),new Vector3(0,0,0),true,23); Render("before-doors.png",new Vector3(6,3,-3),new Vector3(14,2,0),false,70); Render("before-cafe.png",new Vector3(0,4,5),new Vector3(-4,1.5f,14),false,70);
 }
 static void Render(string name,Vector3 pos,Vector3 target,bool ortho,float size){var go=new GameObject("Temporary inspection camera"){hideFlags=HideFlags.HideAndDontSave};var cam=go.AddComponent<Camera>();cam.transform.position=pos;cam.transform.LookAt(target,ortho?Vector3.forward:Vector3.up);cam.orthographic=ortho;if(ortho)cam.orthographicSize=size;else cam.fieldOfView=size;cam.nearClipPlane=.08f;cam.farClipPlane=120;var rt=new RenderTexture(1280,800,24){antiAliasing=2};cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var img=new Texture2D(1280,800,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,1280,800),0,0);img.Apply();File.WriteAllBytes("Design/CafeUpdate/"+name,img.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(img);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
}}
 
