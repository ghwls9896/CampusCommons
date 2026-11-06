using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CampusCommons.GrandLibrary;
using CampusCommons.LibraryCharacter;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CampusCommons.LibraryCharacter.Editor
{
    public static class CampusRefinement
    {
        const string Root="Assets/LibraryCharacter";
        const string Generated=Root+"/Generated";
        const string Theme=Root+"/School/CampusTheme";
        static Material Textured(string name,string texturePath)
        {
            var importer=AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if(!importer)throw new Exception("Texture is missing: "+texturePath);
            if(importer.filterMode!=FilterMode.Bilinear){importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();}
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);if(!texture)throw new Exception("Texture import failed: "+texturePath);
            string path=Theme+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find("Unlit/Texture"));AssetDatabase.CreateAsset(material,path);}
            material.shader=Shader.Find("Unlit/Texture");material.mainTexture=texture;material.mainTextureScale=Vector2.one;material.mainTextureOffset=Vector2.zero;
            EditorUtility.SetDirty(material);return material;
        }
        static Material Solid(string name,Color color)
        {
            string path=Theme+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(Shader.Find("Unlit/Color"));AssetDatabase.CreateAsset(mat,path);}
            mat.shader=Shader.Find("Unlit/Color");mat.color=color;EditorUtility.SetDirty(mat);return mat;
        }
        static void FitShelves()
        {
            string path=Generated+"/ShelfBooks.prefab";var root=PrefabUtility.LoadPrefabContents(path);int count=0;
            try
            {
                foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var m=Regex.Match(r.name,@"^Floor (\d+) Bay (\d+) Shelf (\d+) (Left|Right)$");if(!m.Success)continue;
                    int floor=int.Parse(m.Groups[1].Value)-1,bay=int.Parse(m.Groups[2].Value)-1,shelf=int.Parse(m.Groups[3].Value)-1;
                    bool right=m.Groups[4].Value=="Right";float a=bay*Mathf.PI*2/40;
                    const float width=1.03f,depth=.105f,radius=18.825f;
                    // Each combined mesh runs from x=0 to x=1. Its local X points
                    // opposite the shelf tangent after turning the spines inward.
                    float center=right?.68f:-.68f,anchor=center+width*.5f;
                    r.transform.localPosition=new Vector3(Mathf.Sin(a)*radius+Mathf.Cos(a)*anchor,floor*4.8f+.204f+shelf*.76f,Mathf.Cos(a)*radius-Mathf.Sin(a)*anchor);
                    r.transform.localScale=new Vector3(width,Mathf.Min(r.transform.localScale.y,.578f),depth);count++;
                }
                if(count!=960)throw new Exception("Expected 960 shelf rows; found "+count);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            Debug.Log("CAMPUS_BOOKS_FIT rows="+count);
        }
        static void FillShelfGaps()
        {
            if(GameObject.Find("Shelf Gap Fillers"))throw new Exception("Shelf gap fillers already exist");
            var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/GrandLibrary/Materials/wood_dark.mat");
            if(!material)throw new Exception("Shelf wood material is missing");
            var sample=GameObject.CreatePrimitive(PrimitiveType.Cube);var cube=sample.GetComponent<MeshFilter>().sharedMesh;UnityEngine.Object.DestroyImmediate(sample);
            var root=new GameObject("Shelf Gap Fillers");Undo.RegisterCreatedObjectUndo(root,"Close gaps between shelves");
            for(int level=0;level<3;level++)
            {
                var items=new CombineInstance[40];
                for(int i=0;i<40;i++)
                {
                    float a=(i+.5f)*Mathf.PI*2/40;var pos=new Vector3(Mathf.Sin(a)*19.1f,level*4.8f+2.05f,Mathf.Cos(a)*19.1f);
                    items[i]=new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(pos,Quaternion.Euler(0,a*Mathf.Rad2Deg,0),new Vector3(.39f,4f,.42f))};
                }
                var mesh=new Mesh{name="Shelf filler floor "+(level+1)};mesh.CombineMeshes(items,true,true,false);
                string path=Generated+"/ShelfGapFillers_"+level+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
                var obj=new GameObject("Floor "+(level+1));obj.transform.SetParent(root.transform,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=material;
            }
        }
        static MeshFilter FindMesh(string meshName)
        {
            var filters=UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include).Where(f=>f.sharedMesh && f.sharedMesh.name==meshName).ToArray();
            if(filters.Length!=1)throw new Exception("Expected one scene mesh named "+meshName+", found "+filters.Length);return filters[0];
        }
        static void TrimReturnPieces(string sourceName,float centerX,bool signOnly)
        {
            var filter=FindMesh(sourceName);var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);var vertices=mesh.vertices;int removed=0;
            for(int sub=0;sub<mesh.subMeshCount;sub++)
            {
                var original=mesh.GetTriangles(sub);var keep=new List<int>(original.Length);
                for(int i=0;i<original.Length;i+=3)
                {
                    Vector3 c=(filter.transform.TransformPoint(vertices[original[i]])+filter.transform.TransformPoint(vertices[original[i+1]])+filter.transform.TransformPoint(vertices[original[i+2]]))/3f;
                    bool inPortal=Mathf.Abs(c.x-centerX)<1.85f && Mathf.Abs(c.z+8.1f)<.4f && c.y<4.8f && c.y>(signOnly?4.18f:.05f);
                    if(inPortal){removed++;continue;}keep.Add(original[i]);keep.Add(original[i+1]);keep.Add(original[i+2]);
                }
                mesh.SetTriangles(keep,sub,true);
            }
            if(removed==0)throw new Exception("No return portal geometry found in "+sourceName);
            mesh.RecalculateBounds();string path=Generated+"/NoReturn_"+sourceName+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,path);
            Undo.RecordObject(filter,"Remove room return portal");filter.sharedMesh=mesh;
        }
        static void DisableRenderer(string meshName,bool collider=false)
        {
            var filter=FindMesh(meshName);var renderer=filter.GetComponent<Renderer>();if(renderer){Undo.RecordObject(renderer,"Remove room portal");renderer.enabled=false;}
            if(collider){var c=filter.GetComponent<Collider>();if(c){Undo.RecordObject(c,"Remove portal platform collider");c.enabled=false;}}
        }
        static void RemoveReturnPortals()
        {
            foreach(var portal in UnityEngine.Object.FindObjectsByType<Portal>(FindObjectsInactive.Include))
                if(portal.destination==Portal.Space.Library){Undo.RecordObject(portal.gameObject,"Remove return trigger");portal.gameObject.SetActive(false);}
            foreach(string area in new[]{"Mafia","Quiz"})
            {
                DisableRenderer("LIB_"+area+"__portal_quiz__DETAIL");
                DisableRenderer("LIB_"+area+"__stone__SOLID",true);
                DisableRenderer("SIGN_"+area+"_LOUNGE");
            }
            DisableRenderer("LIB_Quiz__brass__DETAIL");DisableRenderer("LIB_Quiz__wood_dark__DETAIL");
            TrimReturnPieces("LIB_Mafia__brass__DETAIL",100,false);
            TrimReturnPieces("LIB_Mafia__wood_dark__DETAIL",100,true);
            Debug.Log("CAMPUS_RETURN_PORTALS_REMOVED");
        }
        static void Assign(Renderer r,Material material)
        {
            Undo.RecordObject(r,"Update school material");var mats=r.sharedMaterials;
            if(mats.Length==0)mats=new Material[1];for(int i=0;i<mats.Length;i++)mats[i]=material;r.sharedMaterials=mats;
        }
        static void SplitSurface(MeshFilter filter,Material wall,Material floor,Material ceiling)
        {
            var original=filter.sharedMesh;var mesh=UnityEngine.Object.Instantiate(original);var vertices=mesh.vertices;
            var groups=new[]{new List<int>(),new List<int>(),new List<int>()};
            for(int sub=0;sub<original.subMeshCount;sub++)
            {
                var tris=original.GetTriangles(sub);
                for(int i=0;i<tris.Length;i+=3)
                {
                    var a=filter.transform.TransformPoint(vertices[tris[i]]);var b=filter.transform.TransformPoint(vertices[tris[i+1]]);var c=filter.transform.TransformPoint(vertices[tris[i+2]]);
                    var n=Vector3.Cross(b-a,c-a).normalized;float y=(a.y+b.y+c.y)/3f;
                    int group=Mathf.Abs(n.y)>.75f?(y<1.3f?1:2):0;groups[group].Add(tris[i]);groups[group].Add(tris[i+1]);groups[group].Add(tris[i+2]);
                }
            }
            mesh.subMeshCount=3;for(int i=0;i<3;i++)mesh.SetTriangles(groups[i],i,false);mesh.RecalculateBounds();
            string path=Generated+"/CampusSurface_"+original.name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
            Undo.RecordObject(filter,"Correct wall and floor surfaces");filter.sharedMesh=mesh;
            var renderer=filter.GetComponent<Renderer>();Undo.RecordObject(renderer,"Assign wall and floor materials");renderer.sharedMaterials=new[]{wall,floor,ceiling};
        }
        static void ThemeSchool()
        {
            var wall=Textured("Campus Wall",Theme+"/CampusWall.png");var plain=Textured("Campus Plain Wall",Theme+"/CampusWallPlain.png");
            var floor=Textured("Campus Floor",Theme+"/CampusFloor.png");var ceiling=Textured("Campus Ceiling",Theme+"/CampusCeiling.png");
            var window=Textured("Campus Window",Theme+"/CampusWindow.png");var notice=Textured("Campus Notice",Theme+"/CampusNotice.png");
            var locker=Textured("Campus Locker",Theme+"/CampusLocker.png");var lift=Textured("Campus Lift",Theme+"/CampusLift.png");
            var wood=Textured("Campus Desk Wood","Assets/GrandLibrary/Realism/wood_floor/wood_floor_diff.jpg");
            var blue=Solid("Campus Blue",new Color(.19f,.46f,.65f));
            var floorIds=new HashSet<int>{0,4,8,13,19,38,43,48};
            var ceilingIds=new HashSet<int>{5,9,12,37};
            var posterIds=new HashSet<int>{3,6,7,10,11,25,26,27,28,29,33,34,36,40,41,42,46,47,49,55,56,57,58,60,61,64,65,66};
            var noticeIds=new HashSet<int>{44,45,62};var wallIds=new HashSet<int>{14,20,21,51,52,63};int changed=0,split=0;
            foreach(var filter in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
            {
                if(!filter.sharedMesh)continue;var match=Regex.Match(filter.sharedMesh.name,@"^SCHOOL_(\d+)_(SOLID|DETAIL)$");if(!match.Success)continue;
                int id=int.Parse(match.Groups[1].Value);var renderer=filter.GetComponent<Renderer>();if(!renderer)continue;
                if(id==1 || id==2 || id==54){Undo.RecordObject(renderer,"Remove old vending machine art");renderer.enabled=false;changed++;continue;}
                if(id==18 || id==37 || id==39){SplitSurface(filter,plain,floor,ceiling);split++;continue;}
                Material selected=null;
                if(floorIds.Contains(id))selected=floor;else if(ceilingIds.Contains(id))selected=ceiling;
                else if(posterIds.Contains(id))selected=wall;else if(noticeIds.Contains(id))selected=notice;else if(wallIds.Contains(id))selected=wall;
                else if(id==15 || id==53)selected=window;else if(id==30 || id==31)selected=locker;
                else if(id==32 || id==35)selected=blue;else if(id==22)selected=lift;else if(id==17)selected=wood;else if(id==50)selected=plain;
                if(selected){Assign(renderer,selected);changed++;}
            }
            var point=new Vector3(504.5f,0,0);Physics.SyncTransforms();
            if(!Physics.Raycast(point+Vector3.up*1.6f,Vector3.down,2f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
            {
                var patch=GameObject.CreatePrimitive(PrimitiveType.Cube);patch.name="School Floor Seam Repair";
                patch.transform.position=point+Vector3.down*.025f;patch.transform.localScale=new Vector3(.5f,.05f,.5f);patch.GetComponent<Renderer>().sharedMaterial=floor;
                Undo.RegisterCreatedObjectUndo(patch,"Repair school floor seam");
            }
            var doorFace=Textured("Campus Door",Theme+"/CampusDoor.png");var doorPanel=Solid("Campus Door Paint",new Color(.14f,.36f,.55f));
            var doorFrame=Solid("Campus Door Frame",new Color(.35f,.43f,.47f));string doorPath=Generated+"/SchoolDoors.prefab";
            var doors=PrefabUtility.LoadPrefabContents(doorPath);int doorParts=0;
            try
            {
                foreach(var r in doors.GetComponentsInChildren<Renderer>(true))
                {
                    if(r.name=="Door Texture"){r.sharedMaterial=doorFace;doorParts++;}
                    else if(r.name=="Door Panel")r.sharedMaterial=doorPanel;
                    else if(r.name.EndsWith("Door Frame"))r.sharedMaterial=doorFrame;
                }
                if(doorParts!=46)throw new Exception("Expected 46 door faces, found "+doorParts);
                PrefabUtility.SaveAsPrefabAsset(doors,doorPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(doors);}
            Debug.Log("CAMPUS_SCHOOL_THEME changed="+changed+" split="+split+" doorFaces="+doorParts);
        }
        [MenuItem("Campus/Fix rooms and campus theme")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new Exception("Stop Play mode first");var scene=SceneManager.GetActiveScene();
            if(!scene.IsValid() || !scene.isLoaded || (scene.path!="Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity" && scene.path!=Root+"/Preview/CampusRefreshInput.unity"))
                throw new Exception("Open GrandLibrary_Living first");
            if(GameObject.Find("Shelf Gap Fillers"))throw new Exception("The campus room update is already applied");
            string backup=AssetDatabase.GenerateUniqueAssetPath(Root+"/Backups/GrandLibrary_before_campus_refinement.unity");
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Could not back up the open scene");
            FitShelves();FillShelfGaps();RemoveReturnPortals();ThemeSchool();
            EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Could not save scene");
            AssetDatabase.SaveAssets();Debug.Log("CAMPUS_REFINEMENT_SAVED backup="+backup);
        }
        public static void RunBatch()
        {
            EditorSceneManager.OpenScene(Root+"/Preview/CampusRefreshInput.unity");Apply();
        }
    }
}
