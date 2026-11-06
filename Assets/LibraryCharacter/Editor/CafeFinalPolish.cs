using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace CampusCommons.LibraryCharacter.Editor {
public static class CafeFinalPolish {
 [InitializeOnLoadMethod] static void Init(){EditorApplication.delayCall+=Run;}
 static void Run(){if(!File.Exists("Tools/cafe-final-polish.request"))return;File.Delete("Tools/cafe-final-polish.request");try{Apply();}catch(Exception e){File.WriteAllText("Tools/cafe-final-polish-result.txt",e.ToString());Debug.LogException(e);}}
 static void Apply(){
  var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity"||EditorApplication.isPlaying)throw new Exception("Wrong scene or play mode");
  var glass=AssetDatabase.LoadAssetAtPath<Material>("Assets/UserModels_CafeUpdate/SceneMaterials/Dome glass.mat");var shader=Shader.Find("Campus/DomeGlass");if(!glass||!shader)throw new Exception("Glass shader not ready");glass.shader=shader;glass.color=new Color(.70f,.80f,.86f,.075f);glass.renderQueue=3000;EditorUtility.SetDirty(glass);
  var glazing=GameObject.Find("Glass dome");var filter=glazing.GetComponent<MeshFilter>();var mesh=filter.sharedMesh;var old=mesh.triangles;if(old.Length%12!=0)throw new Exception("Unexpected glazing topology");var tris=new System.Collections.Generic.List<int>();for(int i=0;i<old.Length;i+=12)for(int j=0;j<6;j++)tris.Add(old[i+j]);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();EditorUtility.SetDirty(mesh);glazing.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
  var doors=UnityEngine.Object.FindObjectsByType<CampusCommons.GrandLibrary.LobbyDoor>();if(doors.Length!=3)throw new Exception("Expected three lobby doors");foreach(var door in doors){var f=door.hinge.GetComponentInChildren<MeshFilter>();if(!f||!f.sharedMesh)throw new Exception("Door panel mesh missing");var c=f.gameObject.GetComponent<BoxCollider>();if(!c)c=f.gameObject.AddComponent<BoxCollider>();c.center=f.sharedMesh.bounds.center;c.size=f.sharedMesh.bounds.size;var backing=door.transform.Find("Recessed walnut doorway");if(backing&&!backing.GetComponent<Collider>())backing.gameObject.AddComponent<BoxCollider>();}
  var fstone=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshFilter>(true)).First(x=>x.sharedMesh&&x.sharedMesh.name=="DoorUpdate_LIB_Library__stone__SOLID");var original=AssetDatabase.LoadAllAssetsAtPath("Assets/GrandLibrary/Models/GrandLibrary.fbx").OfType<Mesh>().First(x=>x.name=="LIB_Library__stone__SOLID");var b=new Bounds();int n=0;foreach(var v in original.vertices){var p=fstone.transform.TransformPoint(v);if(n++==0)b=new Bounds(p,Vector3.zero);else b.Encapsulate(p);}
  var sb=new StringBuilder();sb.AppendLine("original stone world bounds "+b.center.ToString("F2")+" / "+b.size.ToString("F2")+" vertices="+n+" triangles="+original.triangles.Length/3);var vert=fstone.sharedMesh.vertices;var t=fstone.sharedMesh.triangles;int remaining=0;var rb=new Bounds();for(int i=0;i<t.Length;i+=3){var c=(fstone.transform.TransformPoint(vert[t[i]])+fstone.transform.TransformPoint(vert[t[i+1]])+fstone.transform.TransformPoint(vert[t[i+2]]))/3f;if(c.x>10&&c.x<17&&c.y<.4f&&new[]{-5.5f,0f,5.5f}.Any(z=>Mathf.Abs(c.z-z)<3f)){if(remaining++==0)rb=new Bounds(c,Vector3.zero);else rb.Encapsulate(c);}}
  sb.AppendLine("remaining platform triangles="+remaining+" centroids="+rb.center.ToString("F2")+" / "+rb.size.ToString("F2"));
  EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);File.WriteAllText("Tools/cafe-final-polish-result.txt",sb.ToString());Debug.Log("CAFE_FINAL_POLISH_OK");
 }
}}
