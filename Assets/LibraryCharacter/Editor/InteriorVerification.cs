using System;
using System.IO;
using System.Linq;
using System.Text;
using CampusCommons.GrandLibrary;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CampusCommons.LibraryCharacter.Editor
{
    public static class InteriorVerification
    {
        [InitializeOnLoadMethod] static void Init(){if(File.Exists("Tools/interior-verification.request"))EditorApplication.update+=Run;}
        static void Run()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;EditorApplication.update-=Run;File.Delete("Tools/interior-verification.request");
            try{Inspect();}catch(Exception e){File.WriteAllText("Tools/interior-verification.txt","FAIL: "+e);Debug.LogException(e);}
        }
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        static Transform One(string n)
        {var a=SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Where(t=>t.name==n).ToArray();Check(a.Length==1,"Missing or duplicate "+n);return a[0];}
        static Bounds BoundsOf(Transform t)
        {var rs=t.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();Check(rs.Length>0,"No visual on "+t.name);var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;}
        static void CheckMovement(Transform original)
        {
            var clone=UnityEngine.Object.Instantiate(original.gameObject);clone.hideFlags=HideFlags.HideAndDontSave;
            var children=clone.GetComponentsInChildren<Transform>(true);Check(children.Length>2,"Empty group "+original.name);
            var before=children.Select(t=>t.position).ToArray();Vector3 shift=new Vector3(.91f,.17f,-.44f);clone.transform.position+=shift;
            for(int i=0;i<children.Length;i++)Check(Vector3.Distance(children[i].position,before[i]+shift)<.002f,"Child did not move with "+original.name);
            UnityEngine.Object.DestroyImmediate(clone);
        }
        static void Inspect()
        {
            var scene=SceneManager.GetActiveScene();Check(!EditorApplication.isPlaying&&scene.path=="Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity","Wrong scene");
            var sb=new StringBuilder();
            for(int i=1;i<=4;i++){
                string n=i.ToString("00");var set=One("Lounge Set "+n);var rug=One("Lounge Rug "+n);var table=One("Coffee Table "+n);
                Check(rug.parent==set&&table.parent==set,"Lounge group incomplete "+n);var a=BoundsOf(rug).center;var b=BoundsOf(table).center;a.y=b.y=0;
                Check(Vector3.Distance(a,b)<.1f,"Lounge furniture does not follow rug "+n);CheckMovement(set);sb.AppendLine("LOUNGE "+n+" rug="+a.ToString("F2")+" table="+b.ToString("F2"));
            }
            for(int i=1;i<=6;i++){
                string n=i.ToString("00");var group=One("Study Set "+n);var table=One("Study Table "+n);Check(table.parent==group,"Study group incomplete "+n);
                for(int j=(i-1)*4+1;j<=i*4;j++){var chair=One("Study Chair "+j.ToString("00"));var seat=One("Study Chair Seat "+j.ToString("00"));Check(chair.parent==group&&seat.parent==chair,"Seat not attached to chair");}
                CheckMovement(group);
            }
            Check(One("Study Rug 03").gameObject.activeInHierarchy&&!One("Round rug beneath Study Table 03").gameObject.activeInHierarchy,"Study rug 03 was not replaced");
            var tables=One("School Hall Tables");Check(tables.childCount==12,"Expected 12 school tables");
            foreach(Transform t in tables){var posts=t.GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="Floor-to-table post").ToArray();Check(posts.Length==6&&posts.All(p=>Mathf.Abs(p.bounds.min.y)<.02f&&p.bounds.max.y>.70f),"Table posts are not grounded: "+t.name);Check(t.GetComponentsInChildren<MeshCollider>().Length==1,"Table collision missing");}
            CheckMovement(tables.GetChild(0));
            var signage=One("World Sign Font").GetComponent<WorldTextFontAtlas>();Check(signage&&signage.font&&signage.depthMaterial&&signage.depthMaterial.shader.name=="Campus/WorldTextDepth","Depth-tested font setup missing");
            Check(signage.depthMaterial.mainTexture==signage.font.material.mainTexture,"Font atlas does not match the material");
            var labels=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TextMesh>(true)).Where(t=>t.gameObject.activeInHierarchy&&t.text.All(c=>c<128)).ToArray();
            Check(labels.Length>=8&&labels.All(t=>t.font==signage.font&&t.GetComponent<MeshRenderer>().sharedMaterial==signage.depthMaterial),"Some signs lack the new font");
            Check(One("Cafe Interior").parent==One("Cafe Shop"),"Cafe is not grouped");CheckMovement(One("Cafe Shop"));
            int missing=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            Check(missing==0,"Scene has missing scripts: "+missing);sb.AppendLine("PASS: six study sets, four lounge sets, 12 grounded tables, cafe group, modern depth-tested font, no missing scripts.");
            File.WriteAllText("Tools/interior-verification.txt",sb.ToString());Directory.CreateDirectory("Design/LayoutUpdate");
            Render("hall-final.png",new Vector3(507,3.4f,-18.8f),new Vector3(508,1,-30),false,65);
            Render("hall-floor-final.png",new Vector3(497,1.55f,-20.8f),new Vector3(506,.65f,-29),false,68);
            Render("lounge-final.png",new Vector3(-8.3f,2.5f,-11.8f),new Vector3(-8.1f,.9f,-5),false,68);
            Render("signs-final.png",new Vector3(7,2.8f,-3),new Vector3(14,3.0f,0),false,65);
        }
        static void Render(string path,Vector3 p,Vector3 target,bool ortho,float field)
        {
            var go=new GameObject("Verification camera"){hideFlags=HideFlags.HideAndDontSave};var cam=go.AddComponent<Camera>();cam.transform.position=p;cam.transform.LookAt(target);cam.fieldOfView=field;cam.nearClipPlane=.05f;cam.farClipPlane=150;
            var rt=new RenderTexture(960,600,24){antiAliasing=2};cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var image=new Texture2D(960,600,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,960,600),0,0);image.Apply();File.WriteAllBytes("Design/LayoutUpdate/"+path,image.EncodeToPNG());
            cam.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);
        }
    }
}


