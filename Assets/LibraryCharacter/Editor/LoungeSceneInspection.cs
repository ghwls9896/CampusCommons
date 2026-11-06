using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CampusCommons.LibraryCharacter.Editor
{
    public static class LoungeSceneInspection
    {
        const string Request = "Tools/lounge-inspect.request";
        [InitializeOnLoadMethod] static void Start() { EditorApplication.delayCall += RunIfRequested; }
        static void RunIfRequested()
        {
            if (!File.Exists(Request)) return;
            File.Delete(Request);
            try { Inspect(); } catch (Exception ex) { File.WriteAllText("Tools/lounge-inspect-error.txt",ex.ToString()); Debug.LogException(ex); }
        }
        [MenuItem("Campus/Testing/Inspect lounge layout")]
        public static void Inspect()
        {
            var scene=SceneManager.GetActiveScene();
            var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            var sb=new StringBuilder();sb.AppendLine("Scene "+scene.path+" dirty="+scene.isDirty+" playing="+EditorApplication.isPlaying);
            foreach(var t in all)
            {
                string n=t.name.ToLowerInvariant();
                bool wanted=n.Contains("planter") || n.Contains("bookcase") || n.Contains("bookshelf") || n.Contains("study table") || n.Contains("study chair") || n.Contains("quiz desk") || n.Contains("quiz chair") || n.Contains("quiz seat") || n.Contains("mafia chair") || n.Contains("mafia seat") || n.Contains("sofa") || n.Contains("coffee table") || n.Contains("lounge chair") || n.Contains("lighting") || n.Contains("skylight") || n.Contains("lounge light") || n.Contains("room ceiling light") || n.Contains("metal detail") || n.Contains("floor solid") || n.Contains("stair");
                if(!wanted) continue;
                var rs=t.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled).ToArray();
                var b=rs.Length>0?rs[0].bounds:new Bounds(t.position,Vector3.zero);foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);
                var colliders=t.GetComponents<Collider>();var light=t.GetComponent<Light>();
                sb.AppendLine(t.name+" | parent="+(t.parent?t.parent.name:"ROOT")+" | active="+t.gameObject.activeInHierarchy+" | pos="+t.position.ToString("F2")+" | rot="+t.eulerAngles.ToString("F1")+" | scale="+t.lossyScale.ToString("F2")+" | bounds="+b.center.ToString("F2")+" / "+b.size.ToString("F2")+" | col="+string.Join(",",colliders.Select(c=>c.GetType().Name+"/"+c.enabled))+ (light?" | light="+light.type+" "+light.intensity+" range="+light.range: ""));
            }
            File.WriteAllText("Tools/lounge-inspection.txt",sb.ToString());
            Directory.CreateDirectory("Design/CurrentLayout");
            Render("lounge.png",new Vector3(-1,5,-12),new Vector3(2,4,2),70);
            Render("stairs.png",new Vector3(-5,5,-8),new Vector3(-11,5,2),72);
            Render("mafia.png",new Vector3(100,2.5f,-6.3f),new Vector3(100,1.6f,1),68);
            Render("quiz.png",new Vector3(200,2.3f,-6.2f),new Vector3(200,2,3),64);
            Debug.Log("LOUNGE_INSPECTION_OK");
        }
        static void Render(string file,Vector3 p,Vector3 target,float fov)
        {
            var go=new GameObject("Inspection camera"){hideFlags=HideFlags.HideAndDontSave};
            var cam=go.AddComponent<Camera>();cam.transform.position=p;cam.transform.LookAt(target);cam.fieldOfView=fov;cam.nearClipPlane=.08f;cam.farClipPlane=150;cam.clearFlags=CameraClearFlags.Skybox;
            var rt=new RenderTexture(1280,800,24){antiAliasing=2};cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
            var img=new Texture2D(1280,800,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,1280,800),0,0);img.Apply();File.WriteAllBytes("Design/CurrentLayout/"+file,img.EncodeToPNG());
            cam.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(img);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
