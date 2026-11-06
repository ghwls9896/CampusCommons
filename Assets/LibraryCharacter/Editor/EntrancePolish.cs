using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CampusCommons.LibraryCharacter;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CampusCommons.LibraryCharacter.Editor
{
    public static class EntrancePolish
    {
        const string Root="Assets/LibraryCharacter";
        const string Generated=Root+"/Generated";
        static readonly Vector3 Start=new Vector3(475,.08f,41);
        static readonly Vector3 CreatureStart=new Vector3(483,0,41);
        static Material Metal(string name,Color color,float metallic,float smoothness)
        {
            string path=Generated+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,path);}
            mat.color=color;mat.SetFloat("_Metallic",metallic);mat.SetFloat("_Glossiness",smoothness);mat.enableInstancing=true;EditorUtility.SetDirty(mat);return mat;
        }
        static GameObject Cube(Transform parent,string name,Vector3 position,Vector3 scale,Material material,bool collider=false)
        {
            var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.SetParent(parent,false);
            o.transform.localPosition=position;o.transform.localScale=scale;o.GetComponent<Renderer>().sharedMaterial=material;
            if(!collider)UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>());return o;
        }
        static void BuildLeaf(Transform hinge,bool right,float leafWidth,float height,Material metal,Material trim,Material glass)
        {
            float center=(right?-1:1)*leafWidth*.5f;
            Cube(hinge,right?"Right Gate Leaf":"Left Gate Leaf",new Vector3(center,height*.5f,0),new Vector3(leafWidth-.012f,height-.04f,.10f),metal,true);
            if(!right)Cube(hinge,"Closing Stile",new Vector3(leafWidth-.006f,height*.5f,0),new Vector3(.018f,height-.04f,.12f),trim);
            foreach(float side in new[]{-1f,1f})
            {
                // A framed glazed inset and a raised push bar are actual 3D parts.
                Cube(hinge,"Window Frame",new Vector3(center,height*.72f,side*.058f),new Vector3(leafWidth*.36f,height*.34f,.018f),trim);
                Cube(hinge,"Window Glass",new Vector3(center,height*.72f,side*.070f),new Vector3(leafWidth*.31f,height*.31f,.012f),glass);
                Cube(hinge,"Push Bar",new Vector3(center,height*.43f,side*.093f),new Vector3(leafWidth*.68f,.055f,.06f),trim);
                Cube(hinge,"Lower Plate",new Vector3(center,height*.12f,side*.058f),new Vector3(leafWidth*.82f,.09f,.018f),trim);
            }
        }
        static void UpgradeWideDoors()
        {
            var silver=Metal("Gate Silver",new Color(.92f,.92f,.92f),.12f,.48f);
            silver.EnableKeyword("_EMISSION");silver.SetColor("_EmissionColor",new Color(.22f,.22f,.22f));EditorUtility.SetDirty(silver);
            var trim=Metal("Gate Dark Metal",new Color(.30f,.35f,.38f),.65f,.48f);
            var glass=Metal("Gate Frosted Glass",new Color(.42f,.63f,.72f),.08f,.5f);
            string path=Generated+"/SchoolDoors.prefab";var root=PrefabUtility.LoadPrefabContents(path);int count=0;
            try
            {
                foreach(var door in root.GetComponentsInChildren<Door>(true))
                {
                    var original=door.hinge?door.hinge.GetComponentInChildren<BoxCollider>():null;
                    if(!original || original.transform.localScale.x<1.8f)continue;
                    float width=original.transform.localScale.x+.035f,height=original.transform.localScale.y+.035f;
                    var oldChildren=door.hinge.Cast<Transform>().Select(t=>t.gameObject).ToArray();
                    foreach(var child in oldChildren)UnityEngine.Object.DestroyImmediate(child);
                    var other=new GameObject("Right Gate Hinge").transform;other.SetParent(door.transform,false);other.localPosition=new Vector3(width,0,0);door.secondHinge=other;
                    door.hinge.name="Left Gate Hinge";BuildLeaf(door.hinge,false,width*.5f,height,silver,trim,glass);BuildLeaf(other,true,width*.5f,height,silver,trim,glass);
                    foreach(var r in door.GetComponentsInChildren<Renderer>(true))if(r.name.EndsWith("Door Frame"))r.sharedMaterial=trim;
                    count++;
                }
                if(count!=8)throw new Exception("Expected eight wide school doors, got "+count);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            Debug.Log("ENTRANCE_SILVER_GATES count="+count);
        }
        static void RecessBookcaseBacks()
        {
            var filters=UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include).Where(f=>f.sharedMesh && f.sharedMesh.name=="LIB_Library__wood_dark__SOLID").ToArray();
            if(filters.Length!=1)throw new Exception("Library bookcase backing mesh is missing");
            var filter=filters[0];var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);var vertices=mesh.vertices;int changed=0;
            var levels=new[]{.05f,4.05f,4.85f,8.85f,9.65f,13.65f};
            for(int i=0;i<vertices.Length;i++)
            {
                Vector3 p=filter.transform.TransformPoint(vertices[i]);float a=Mathf.Round(Mathf.Atan2(p.x,p.z)/(2*Mathf.PI/40))*(2*Mathf.PI/40);
                float radial=p.x*Mathf.Sin(a)+p.z*Mathf.Cos(a)-19.1f,tangent=p.x*Mathf.Cos(a)-p.z*Mathf.Sin(a);
                bool level=levels.Any(y=>Mathf.Abs(p.y-y)<.025f);
                if(!level || Mathf.Abs(tangent)>=1.40f || Mathf.Abs(radial+.21f)>=.025f)continue;
                // Move only the inner face of each thick solid backing board back,
                // retaining the rear face. This creates genuine usable shelf depth.
                p+=new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*.29f;
                vertices[i]=filter.transform.InverseTransformPoint(p);changed++;
            }
            if(changed<1000)throw new Exception("Unexpected bookcase backing geometry: "+changed);
            mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.name="Recessed Bookcase Backing";
            string path=Generated+"/RecessedBookcaseBack.asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,path);
            Undo.RecordObject(filter,"Recess bookcase backs");filter.sharedMesh=mesh;
            var collider=filter.GetComponent<MeshCollider>();if(collider){Undo.RecordObject(collider,"Match bookcase collision");collider.sharedMesh=mesh;}
            Debug.Log("ENTRANCE_BOOKCASE_BACK vertices="+changed);
        }
        static void FitFullDepthBooks()
        {
            string path=Generated+"/ShelfBooks.prefab";var root=PrefabUtility.LoadPrefabContents(path);int count=0;
            try
            {
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var m=Regex.Match(renderer.name,@"^Floor (\d+) Bay (\d+) Shelf (\d+) (Left|Right)$");if(!m.Success)continue;
                    int floor=int.Parse(m.Groups[1].Value)-1,bay=int.Parse(m.Groups[2].Value)-1,shelf=int.Parse(m.Groups[3].Value)-1;
                    float a=bay*Mathf.PI*2/40,center=m.Groups[4].Value=="Right"?.68f:-.68f;
                    const float width=1.03f,depth=.28f,radius=18.98f;float anchor=center+width*.5f;
                    renderer.transform.localPosition=new Vector3(Mathf.Sin(a)*radius+Mathf.Cos(a)*anchor,floor*4.8f+.204f+shelf*.76f,Mathf.Cos(a)*radius-Mathf.Sin(a)*anchor);
                    renderer.transform.localScale=new Vector3(width,Mathf.Min(renderer.transform.localScale.y,.578f),depth);count++;
                }
                if(count!=960)throw new Exception("Expected 960 book rows, found "+count);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            Debug.Log("ENTRANCE_BOOKS_DEPTH rows="+count+" depth=.28");
        }
        static void MoveStartToHallway()
        {
            var game=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();var monster=UnityEngine.Object.FindAnyObjectByType<Monster>();
            if(!game || !monster || !game.schoolGrid)throw new Exception("School game or navigation is missing");
            var old=game.schoolGrid;var route=new List<Vector3>();Physics.SyncTransforms();
            foreach(var p in new[]{Start,CreatureStart})
            {
                if(!Physics.Raycast(p+Vector3.up*1.8f,Vector3.down,out RaycastHit hit,2.5f,~(1<<2),QueryTriggerInteraction.Ignore) || Mathf.Abs(hit.point.y)>.15f ||
                   Physics.CheckCapsule(new Vector3(p.x,.36f,p.z),new Vector3(p.x,1.48f,p.z),.29f,~(1<<2),QueryTriggerInteraction.Ignore))
                    throw new Exception("Hallway position is blocked: "+p);
            }
            if(!old.FindPath(Start,CreatureStart,route) || route.Count<10)throw new Exception("Monster cannot reach the hallway start");
            if(Physics.Linecast(Start+Vector3.up*1.54f,CreatureStart+Vector3.up,~(1<<2),QueryTriggerInteraction.Ignore))throw new Exception("Monster is not visible down the hallway");
            if(!old.FindPath(Start,old.exitLocation,route))throw new Exception("Hallway start cannot reach the exit");
            var distances=new List<(Vector3,int)>();
            foreach(var p in old.sheetLocations)
            {
                if(!old.FindPath(Start,p,route))throw new Exception("A problem sheet is not reachable from the hallway");
                distances.Add((p,route.Count));
            }
            var sorted=distances.OrderBy(x=>x.Item2).Select(x=>x.Item1).ToArray();
            if(sorted.Length!=20)throw new Exception("Expected 20 school problem sheet positions");
            var copy=UnityEngine.Object.Instantiate(old);copy.name="Hallway School Grid";copy.sheetLocations=sorted;
            string path=Generated+"/HallwaySchoolGrid.asset";var existing=AssetDatabase.LoadAssetAtPath<SchoolGrid>(path);
            if(existing){EditorUtility.CopySerialized(copy,existing);UnityEngine.Object.DestroyImmediate(copy);copy=existing;}else AssetDatabase.CreateAsset(copy,path);
            Undo.RecordObject(game,"Move school arrival to hallway");Undo.RecordObject(monster.transform,"Move monster to hallway");Undo.RecordObject(monster,"Update school navigation");
            game.schoolArrival=Start;game.schoolYaw=90;game.schoolGrid=copy;game.notebookPositions=copy.sheetLocations;
            monster.transform.position=CreatureStart;monster.map=copy;
            var face=monster.GetComponentInChildren<FaceCamera>(true);
            if(face)
            {
                var helper=new GameObject("Temporary hallway camera");helper.transform.position=Start+Vector3.up*1.62f;
                Undo.RecordObject(face.transform,"Face the hallway start");face.Face(helper.transform);UnityEngine.Object.DestroyImmediate(helper);
            }
            EditorUtility.SetDirty(game);EditorUtility.SetDirty(monster);
            Debug.Log("ENTRANCE_HALLWAY_START position="+Start+" monster="+CreatureStart+" nearestSheetSteps="+distances.Min(x=>x.Item2));
        }
        [MenuItem("Campus/Improve gates books and school start")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new Exception("Stop Play mode before applying the changes");var scene=SceneManager.GetActiveScene();
            if(!scene.IsValid() || !scene.isLoaded || (scene.path!="Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity" && scene.path!=Root+"/Preview/EntranceRefreshInput.unity"))
                throw new Exception("Open the living library scene first");
            if(AssetDatabase.LoadAssetAtPath<Mesh>(Generated+"/RecessedBookcaseBack.asset") &&
               UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include).Any(f=>f.sharedMesh && f.sharedMesh.name=="Recessed Bookcase Backing"))
                throw new Exception("The school entrance update has already been applied");
            string backup=AssetDatabase.GenerateUniqueAssetPath(Root+"/Backups/GrandLibrary_before_entrance_polish.unity");
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Could not save the scene backup");
            RecessBookcaseBacks();FitFullDepthBooks();UpgradeWideDoors();MoveStartToHallway();
            EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Could not save the updated scene");
            AssetDatabase.SaveAssets();Debug.Log("ENTRANCE_POLISH_SAVED backup="+backup);
        }
        public static void RunBatch(){EditorSceneManager.OpenScene(Root+"/Preview/EntranceRefreshInput.unity");Apply();}
    }
}
