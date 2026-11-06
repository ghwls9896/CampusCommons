using System;
using System.IO;
using System.Linq;
using System.Text;
using CampusCommons.GrandLibrary;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CampusCommons.LibraryCharacter.Editor
{
    public static class CafeSceneInspection
    {
        [InitializeOnLoadMethod] static void Init() { EditorApplication.delayCall += Run; }
        static void Run()
        {
            const string request="Tools/cafe-inspect.request";if(!File.Exists(request))return;File.Delete(request);
            try { Inspect(); } catch(Exception e) { File.WriteAllText("Tools/cafe-inspection-error.txt",e.ToString());Debug.LogException(e); }
        }
        static void Inspect()
        {
            var scene=SceneManager.GetActiveScene();var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();var sb=new StringBuilder();
            sb.AppendLine("SCENE "+scene.path+" dirty="+scene.isDirty+" playing="+EditorApplication.isPlaying);
            foreach(var t in all)
            {
                var f=t.GetComponent<MeshFilter>();var text=t.GetComponent<TextMesh>();var portal=t.GetComponent<Portal>();var escape=t.GetComponent<EscapeGame>();
                bool selected=(f && f.sharedMesh && (f.sharedMesh.name.Contains("portal_") || f.sharedMesh.name.Contains("LIB_Library__brass") || f.sharedMesh.name.Contains("LIB_Library__wood_dark") || f.sharedMesh.name.Contains("LIB_Library__stone") || f.sharedMesh.name.Contains("LIB_Mafia__portal") || f.sharedMesh.name.Contains("LIB_Quiz__portal") || f.sharedMesh.name.Contains("LIB_Mafia__brass") || f.sharedMesh.name.Contains("LIB_Quiz__brass"))) || text || portal || escape || t.name.Contains("chandelier");
                if(!selected)continue;
                var r=t.GetComponent<Renderer>();var c=t.GetComponent<Collider>();
                sb.AppendLine("OBJ "+t.name+" parent="+(t.parent?t.parent.name:"root")+" active="+t.gameObject.activeInHierarchy+" pos="+t.position.ToString("F3")+" rot="+t.eulerAngles.ToString("F2")+" mesh="+(f&&f.sharedMesh?f.sharedMesh.name:"-")+" asset="+(f&&f.sharedMesh?AssetDatabase.GetAssetPath(f.sharedMesh):"-")+" mat="+(r?string.Join(",",r.sharedMaterials.Select(m=>m?m.name+"/"+m.shader.name:"null")):"-")+" col="+(c?c.GetType().Name+" trigger="+c.isTrigger:"-")+(text?" TEXT="+text.text:"")+(portal?" DEST="+portal.destination+" arrival="+portal.arrivalPosition:"")+(escape?" ESCAPE":""));
            }
            string root="Assets/UserModels_CafeUpdate/";
            foreach(string p in new[]{"door-wooden-door-metal/source/door wooden.fbx","canarian-cafe-coffee-grinder/source/grinder-for export.fbx","grey-carpet/source/extracted/model.fbx","round-carpet/source/extracted/round-carpet.obj","victorian-chandelier/source/lamparaTecho.fbx","victorian-chandelier/source/victorian_chandelier_optimized.fbx"})
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(root+p);sb.AppendLine("ASSET "+p+" found="+(bool)prefab);if(!prefab)continue;
                var obj=UnityEngine.Object.Instantiate(prefab);obj.hideFlags=HideFlags.HideAndDontSave;obj.transform.position=Vector3.zero;obj.transform.rotation=Quaternion.identity;
                var rs=obj.GetComponentsInChildren<Renderer>();if(rs.Length>0){var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);sb.AppendLine("  overall bounds center="+b.center.ToString("F4")+" size="+b.size.ToString("F4")+" renderers="+rs.Length);}
                foreach(var r in rs.Take(8)){var mesh=r.GetComponent<MeshFilter>();sb.AppendLine("  "+r.name+" bounds="+r.bounds.center.ToString("F3")+"/"+r.bounds.size.ToString("F3")+" mesh="+(mesh&&mesh.sharedMesh?mesh.sharedMesh.name:"-")+" mats="+string.Join(",",r.sharedMaterials.Select(m=>m?m.name:"null")));}
                UnityEngine.Object.DestroyImmediate(obj);
            }
            File.WriteAllText("Tools/cafe-scene-inspection.txt",sb.ToString());Debug.Log("CAFE_SCENE_INSPECTION_OK");
        }
    }
}
