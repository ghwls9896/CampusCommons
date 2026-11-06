using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CampusCommons.LibraryCharacter.Editor
{
    /// <summary>Rebuilds the decorative upper blue strip from the school wall surfaces.</summary>
    public static class SchoolUpperTrimBuilder
    {
        const string AssetPath="Assets/LibraryCharacter/Resources/SchoolUpperTrim.asset";
        static readonly HashSet<string> WallMaterials=new HashSet<string>{"Campus Wall","Campus Plain Wall","Campus Notice","Campus Window"};
        static readonly List<Vector3> vertices=new List<Vector3>();
        static readonly List<Vector2> uv=new List<Vector2>();
        static readonly List<int> triangles=new List<int>();
        static List<Vector3> Clip(List<Vector3> input,float plane,bool above)
        {
            var output=new List<Vector3>();if(input.Count==0)return output;
            Vector3 prior=input[input.Count-1];bool priorInside=above?prior.y>=plane:prior.y<=plane;
            foreach(Vector3 current in input)
            {
                bool inside=above?current.y>=plane:current.y<=plane;
                if(inside!=priorInside)
                {
                    float t=(plane-prior.y)/(current.y-prior.y);
                    output.Add(Vector3.Lerp(prior,current,t));
                }
                if(inside)output.Add(current);
                prior=current;priorInside=inside;
            }
            return output;
        }
        static void AddClipped(Vector3 a,Vector3 b,Vector3 c,float low,float high)
        {
            Vector3 normal=Vector3.Cross(b-a,c-a).normalized;
            if(Mathf.Abs(normal.y)>.05f || Mathf.Max(Mathf.Abs(normal.x),Mathf.Abs(normal.z))<.95f)return;
            var poly=Clip(Clip(new List<Vector3>{a,b,c},low,true),high,false);
            if(poly.Count<3)return;
            int first=vertices.Count;
            foreach(var p in poly)
            {
                vertices.Add(p+normal*.004f);uv.Add(new Vector2(.5f,Mathf.Clamp01((p.y-low)/(high-low))));
            }
            for(int i=1;i+1<poly.Count;i++){triangles.Add(first);triangles.Add(first+i);triangles.Add(first+i+1);}
        }
        [MenuItem("Campus/Rebuild school upper blue trim")]
        public static void Build()
        {
            vertices.Clear();uv.Clear();triangles.Clear();int wallMeshes=0,doors=0;
            foreach(var filter in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
            {
                var mesh=filter.sharedMesh;var renderer=filter.GetComponent<MeshRenderer>();
                if(!mesh || !renderer || !renderer.enabled || !mesh.name.Contains("SCHOOL_"))continue;
                var mats=renderer.sharedMaterials;var world=mesh.vertices.Select(filter.transform.TransformPoint).ToArray();bool used=false;
                for(int sub=0;sub<mesh.subMeshCount && sub<mats.Length;sub++)
                {
                    if(!mats[sub] || !WallMaterials.Contains(mats[sub].name))continue;
                    var indices=mesh.GetTriangles(sub);used=true;
                    for(int j=0;j<indices.Length;j+=3)
                    {
                        var a=world[indices[j]];var b=world[indices[j+1]];var c=world[indices[j+2]];
                        float bottom=Mathf.Min(a.y,Mathf.Min(b.y,c.y)),top=Mathf.Max(a.y,Mathf.Max(b.y,c.y));
                        for(int level=1;level<=5;level++)
                        {
                            float high=level*2.5f,low=high-.28f;
                            if(top>=low && bottom<=high)AddClipped(a,b,c,low,high);
                        }
                    }
                }
                if(used)wallMeshes++;
            }
            // A doorway can have no wall polygon at all above its moving panel.
            // Add the same non-colliding strip across that opening on both sides.
            foreach(var door in UnityEngine.Object.FindObjectsByType<Door>(FindObjectsInactive.Include))
            {
                var left=door.hinge?door.hinge.GetComponentInChildren<Collider>():null;if(!left)continue;
                var b=left.bounds;if(door.secondHinge){var right=door.secondHinge.GetComponentInChildren<Collider>();if(right)b.Encapsulate(right.bounds);}
                if(b.max.y<2.2f || b.max.y>2.8f)continue;
                Vector3 tangent=door.transform.right;Vector3 normal=door.transform.forward;
                float width=Mathf.Abs(tangent.x)*b.size.x+Mathf.Abs(tangent.z)*b.size.z;
                if(width<.5f || width>5f)continue;
                Vector3 center=b.center;center.y=2.5f;
                foreach(float sign in new[]{-1f,1f})
                {
                    Vector3 p=center+normal*sign*.065f,lower=Vector3.down*.28f;
                    Vector3 a=p-tangent*width*.5f,bp=p+tangent*width*.5f,c=bp+lower,d=a+lower;
                    if(sign<0){AddClipped(a,bp,c,2.22f,2.5f);AddClipped(a,c,d,2.22f,2.5f);}
                    else{AddClipped(c,bp,a,2.22f,2.5f);AddClipped(d,c,a,2.22f,2.5f);}
                }
                doors++;
            }
            if(vertices.Count==0)throw new Exception("No eligible school wall surfaces were found");
            var generated=new Mesh{name="School Upper Blue Trim",indexFormat=IndexFormat.UInt32};
            generated.SetVertices(vertices);generated.SetUVs(0,uv);generated.SetTriangles(triangles,0);generated.RecalculateBounds();
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(AssetPath);
            if(old){EditorUtility.CopySerialized(generated,old);UnityEngine.Object.DestroyImmediate(generated);EditorUtility.SetDirty(old);}
            else AssetDatabase.CreateAsset(generated,AssetPath);
            AssetDatabase.SaveAssets();Debug.Log("SCHOOL_UPPER_TRIM_OK walls="+wallMeshes+" doorways="+doors+" triangles="+triangles.Count/3);
        }
        public static void RunBatch()
        {
            EditorSceneManager.OpenScene("Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity");Build();EditorApplication.Exit(0);
        }
    }
}
