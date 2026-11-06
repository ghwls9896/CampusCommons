using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CampusCommons.LibraryCharacter.Editor
{
    public static class SchoolTableAudit
    {
        [InitializeOnLoadMethod] static void Init() { if(File.Exists("Tools/school-table-audit.request")) EditorApplication.update += Run; }
        struct Table { public float x,z; public bool alongZ; public Table(float x,float z,bool alongZ=false){this.x=x;this.z=z;this.alongZ=alongZ;} }
        static readonly Table[] Tables={new Table(498.5f,-23.25f),new Table(506.25f,-23.25f),new Table(513.25f,-23.25f),new Table(508.5f,-28.5f,true),new Table(513.5f,-28.5f,true),new Table(513.5f,-35f,true),new Table(500.75f,-35f,true),new Table(505.75f,-32f),new Table(521.25f,-35f),new Table(519f,-26.25f),new Table(518.75f,-23.25f),new Table(518f,-21.5f)};
        static void Run()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;EditorApplication.update-=Run;File.Delete("Tools/school-table-audit.request");
            try{Inspect();}catch(Exception e){File.WriteAllText("Tools/school-table-audit.txt","FAIL "+e);Debug.LogException(e);}
        }
        static void Inspect()
        {
            var scene=SceneManager.GetActiveScene();var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();var sb=new StringBuilder();
            foreach(string name in new[]{"Study Furniture","Lounge Furniture","Mafia Furniture","Quiz Furniture","Cafe and future shop","Cafe and doorway update"})
            {
                var p=all.FirstOrDefault(t=>t.name==name);if(!p)continue;sb.AppendLine("PARENT "+name);
                foreach(Transform t in p)sb.AppendLine(" CHILD "+t.name+" active="+t.gameObject.activeInHierarchy+" pos="+t.position.ToString("F2")+" childcount="+t.childCount);
            }
            var rug=all.Select(t=>t.GetComponent<MeshFilter>()).FirstOrDefault(f=>f&&f.name=="Lobby Rug Detail");
            if(rug){var v=rug.sharedMesh.vertices;var tris=rug.sharedMesh.triangles;sb.AppendLine("LOUNGE_RUG mesh="+rug.sharedMesh.name+" transform="+rug.transform.position.ToString("F3")+" renderer="+rug.GetComponent<Renderer>().enabled);
                for(int q=0;q<4;q++){int count=0;Bounds b=new Bounds();for(int i=0;i<tris.Length;i+=3){var a=rug.transform.TransformPoint(v[tris[i]]);var c=rug.transform.TransformPoint(v[tris[i+1]]);var d=rug.transform.TransformPoint(v[tris[i+2]]);var mid=(a+c+d)/3;if((mid.x<0?0:1)+(mid.z<0?0:2)!=q)continue;if(count++==0)b=new Bounds(a,Vector3.zero);b.Encapsulate(a);b.Encapsulate(c);b.Encapsulate(d);}sb.AppendLine(" RUG_QUARTER "+q+" triangles="+count+" bounds="+b.center.ToString("F3")+"/"+b.size.ToString("F3"));}}
            var fdesk=all.Select(t=>t.GetComponent<MeshFilter>()).First(f=>f&&f.sharedMesh&&f.sharedMesh.name=="SCHOOL_017_SOLID");
            var verts=fdesk.sharedMesh.vertices;var indices=fdesk.sharedMesh.triangles;
            for(int ti=0;ti<Tables.Length;ti++){
                var t=Tables[ti];int count=0;Bounds b=new Bounds();var heights=new SortedSet<string>();
                for(int i=0;i<indices.Length;i+=3){var a=fdesk.transform.TransformPoint(verts[indices[i]]);var c=fdesk.transform.TransformPoint(verts[indices[i+1]]);var d=fdesk.transform.TransformPoint(verts[indices[i+2]]);var mid=(a+c+d)/3;float dx=mid.x-t.x,dz=mid.z-t.z;float along=t.alongZ?dz:dx,across=t.alongZ?dx:dz;
                    if(Mathf.Abs(along)>2.51f||Mathf.Abs(across)>.64f||mid.y<.40f||mid.y>.81f)continue;if(count++==0)b=new Bounds(a,Vector3.zero);b.Encapsulate(a);b.Encapsulate(c);b.Encapsulate(d);heights.Add(mid.y.ToString("F2"));}
                sb.AppendLine("TABLE "+(ti+1)+" center="+t.x+","+t.z+" alongZ="+t.alongZ+" triangles="+count+" bounds="+b.center.ToString("F2")+"/"+b.size.ToString("F2")+" heights="+string.Join(",",heights));
                foreach(float across in new[]{-.58f,-.45f,-.2f,0f,.2f,.45f,.58f}){
                    Vector3 p=new Vector3(t.x,1.4f,t.z)+(t.alongZ?Vector3.right:Vector3.forward)*across;var hits=Physics.RaycastAll(p,Vector3.down,2f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).OrderByDescending(h=>h.point.y).Take(3).ToArray();
                    sb.AppendLine(" RAY across="+across+" "+string.Join(";",hits.Select(h=>h.collider.name+"@"+h.point.y.ToString("F3"))));}
            }
            File.WriteAllText("Tools/school-table-audit.txt",sb.ToString());
        }
    }
}


