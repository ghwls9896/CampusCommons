using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CampusCommons.LibraryCharacter.Editor
{
    public static class LayoutAudit
    {
        const string Request = "Tools/layout-audit.request";
        [InitializeOnLoadMethod] static void Init() { if (File.Exists(Request)) EditorApplication.update += Run; }
        static void Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= Run; File.Delete(Request);
            try { Inspect(); } catch (Exception e) { File.WriteAllText("Tools/layout-audit-result.txt", "FAIL " + e); Debug.LogException(e); }
        }
        static string BoundsDescription(Transform t)
        {
            var renderers = t.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            if (renderers.Length == 0) return "none";
            var b = renderers[0].bounds; foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds);
            return b.center.ToString("F2") + "/" + b.size.ToString("F2");
        }
        static void Inspect()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != "Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity") throw new Exception("Open the living scene and stop Play first");
            bool dirty = scene.isDirty;
            string backup = AssetDatabase.GenerateUniqueAssetPath("Assets/LibraryCharacter/Backups/GrandLibrary_before_manual_layout_work.unity");
            if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new Exception("Could not back up the current scene");
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            var sb = new StringBuilder(); sb.AppendLine("SCENE dirty-before-backup=" + dirty + " dirty-now=" + scene.isDirty + " backup=" + backup);
            foreach (var t in all)
            {
                string n = t.name.ToLowerInvariant();
                bool wanted = n == "study furniture" || n == "lounge furniture" || n == "lounge decorations" || n == "cafe and future shop" ||
                    n.StartsWith("study table ") || n.StartsWith("study chair ") || n.StartsWith("study rug ") || n.StartsWith("round rug beneath") ||
                    n.StartsWith("desk lamp ") || n.StartsWith("desk plant ") || n.StartsWith("table plant ") || n.StartsWith("coffee table ") || n.StartsWith("lounge chair ") || n.StartsWith("sofa ") || n == "grey cafe rug";
                if (!wanted) continue;
                sb.AppendLine("LAYOUT " + t.name + " | parent=" + (t.parent ? t.parent.name : "ROOT") + " | active=" + t.gameObject.activeInHierarchy +
                    " | pos=" + t.position.ToString("F3") + " | rot=" + t.eulerAngles.ToString("F1") + " | scale=" + t.lossyScale.ToString("F2") + " | bounds=" + BoundsDescription(t) + " | children=" + t.childCount);
            }
            foreach (var text in all.Select(t => t.GetComponent<TextMesh>()).Where(t => t && t.gameObject.activeInHierarchy))
                sb.AppendLine("TEXT " + text.name + " | value=" + text.text.Replace("\n", " / ") + " | font=" + (text.font ? text.font.name : "null") + " | pos=" + text.transform.position.ToString("F2"));
            foreach (var filter in all.Select(t => t.GetComponent<MeshFilter>()).Where(f => f && f.sharedMesh && f.sharedMesh.name.Contains("SCHOOL_")))
            {
                var mesh = filter.sharedMesh; var r = filter.GetComponent<Renderer>(); if (!r || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                var vertices = mesh.vertices; var triangles = mesh.triangles; int count=0; Bounds b=new Bounds();
                for (int i=0;i<triangles.Length;i+=3)
                {
                    Vector3 p=(filter.transform.TransformPoint(vertices[triangles[i]])+filter.transform.TransformPoint(vertices[triangles[i+1]])+filter.transform.TransformPoint(vertices[triangles[i+2]]))/3f;
                    if (p.x<494 || p.x>525 || p.z<-39 || p.z>-19 || p.y<.15f || p.y>1.5f) continue;
                    if (count++==0) b=new Bounds(p,Vector3.zero); else b.Encapsulate(p);
                }
                if (count>0) sb.AppendLine("HALL_MESH " + filter.name + " | mesh=" + mesh.name + " | mat=" + string.Join(",",r.sharedMaterials.Select(m=>m?m.name:"null")) + " | triangles=" + count + " | bounds=" + b.center.ToString("F2") + "/" + b.size.ToString("F2"));
            }
            File.WriteAllText("Tools/layout-audit-result.txt", sb.ToString());
            Directory.CreateDirectory("Design/LayoutUpdate");
            Render("lobby-top.png",new Vector3(0,30,0),new Vector3(0,0,0),true,22);
            Render("hall.png",new Vector3(507,3.4f,-18.8f),new Vector3(508,1,-30),false,65);
            Render("hall-top.png",new Vector3(508,18,-29),new Vector3(508,0,-29),true,16);
        }
        static void Render(string file, Vector3 p, Vector3 target, bool ortho, float field)
        {
            var go=new GameObject("Layout inspection camera"){hideFlags=HideFlags.HideAndDontSave}; var cam=go.AddComponent<Camera>();
            cam.transform.position=p; cam.transform.LookAt(target,ortho?Vector3.forward:Vector3.up); cam.orthographic=ortho;
            if(ortho) cam.orthographicSize=field; else cam.fieldOfView=field; cam.nearClipPlane=.08f;cam.farClipPlane=150;
            var rt=new RenderTexture(960,600,24){antiAliasing=2};cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
            var image=new Texture2D(960,600,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,600),0,0);image.Apply();
            File.WriteAllBytes("Design/LayoutUpdate/"+file,image.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=null;
            UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);
        }
    }
}

