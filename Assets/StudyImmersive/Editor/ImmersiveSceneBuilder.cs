using System;
using System.Collections.Generic;
using System.IO;
using CampusCommons.GrandLibrary;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using CampusCommons.StudyImmersive;

namespace CampusCommons.StudyImmersive.Editor
{
    public static class ImmersiveSceneBuilder
    {
        public const string SourceScene="Assets/GrandLibrary/Scenes/GrandLibrary.unity";
        public const string ScenePath="Assets/StudyImmersive/Scenes/GrandLibrary_Immersive.unity";
        const string Root="Assets/StudyImmersive";
        static readonly Dictionary<string,Material> MaterialCache=new Dictionary<string,Material>();

        [MenuItem("Campus/Create immersive study scene")]
        public static void MenuCreate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Regenerate immersive scene?","The generated scene will be replaced. Save custom edits under another name first.","Regenerate","Cancel")) return;
            Build();
        }
        public static void Build()
        {
            Directory.CreateDirectory(Root+"/Scenes");
            Directory.CreateDirectory(Root+"/Materials");
            Directory.CreateDirectory(Root+"/Prefabs");
            Directory.CreateDirectory("Design/Immersive");
            AssetDatabase.Refresh(); MaterialCache.Clear();
            PrepareMaterials();
            var scene=EditorSceneManager.OpenScene(SourceScene,OpenSceneMode.Single);
            if (!scene.IsValid()) throw new Exception("Could not open the saved source scene.");
            var additions=new GameObject("IMMERSIVE - realistic plants and study props");
            UpgradeSkybox();
            UpgradeChairsAndStudy();
            ImproveShelfBooks();
            ReplacePlaceholderPlants(additions.transform);
            HideOldTreeBranches();
            DressTables(additions.transform);
            ReplaceSomeSofas();
            var notebookPrefab=CreateNotebookPrefab();
            var entry=GameObject.Find("ESCAPE ROOM");
            var returnTrigger=GameObject.Find("ESCAPE ROOM TO LOUNGE");
            var returnVisual=GameObject.Find("School return portal visual");
            if (!entry || !returnTrigger || !returnVisual) throw new Exception("The existing escape portals were not found.");
            var portal=entry.GetComponent<Portal>();
            if (!portal) throw new Exception("Escape entry portal component missing.");
            Physics.SyncTransforms();
            var noteLocations=FindAccessibleNotebookLocations(portal.arrivalPosition);
            var flow=entry.GetComponent<EscapeGame>(); if (!flow) flow=entry.AddComponent<EscapeGame>();
            flow.schoolArrival=portal.arrivalPosition; flow.schoolYaw=portal.arrivalYaw;
            flow.schoolReturnTrigger=returnTrigger; flow.schoolReturnVisual=returnVisual;
            flow.notebookPrefab=notebookPrefab; flow.notebookPositions=noteLocations;
            // Unity delivers trigger callbacks to disabled MonoBehaviours. Remove
            // the old portal from this new scene so it cannot skip the mode menu.
            UnityEngine.Object.DestroyImmediate(portal);
            returnTrigger.SetActive(false); returnVisual.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene,ScenePath)) throw new Exception("Unable to save the new immersive scene.");
            AssetDatabase.SaveAssets();
            Debug.Log("IMMERSIVE_SCENE_OK: owned skybox, raised chairs, extra study tables, detailed plants/books, four escape modes, "+noteLocations.Length+" reachable notebook positions.");
        }
        static string TexturePath(string id,string stem)
        {
            string dir=Root+"/Realism/"+id+"/";
            foreach(string extension in new[] {".png",".jpg"})
            {
                string path=dir+stem.ToLowerInvariant()+extension;
                if (File.Exists(path)) return path;
            }
            throw new Exception("Missing "+id+"/"+stem);
        }
        static Texture2D Texture(string id,string stem,bool normal=false)
        {
            string path=TexturePath(id,stem);
            if (normal)
            {
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                if (!importer) throw new Exception("No texture importer at "+path);
                if (importer.textureType!=TextureImporterType.NormalMap)
                { importer.textureType=TextureImporterType.NormalMap; importer.SaveAndReimport(); }
            }
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!texture) throw new Exception("Texture not imported: "+path);
            return texture;
        }
        static Material MakeMaterial(string key,string id,string diffuse,string normal,bool cutout=false)
        {
            string path=Root+"/Materials/"+key+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader=Shader.Find(cutout?"StudyImmersive/RealFoliage":"Standard");
            if (!shader) throw new Exception("Required shader missing: "+key);
            if (!mat) { mat=new Material(shader); AssetDatabase.CreateAsset(mat,path); }
            mat.shader=shader; mat.color=Color.white;
            mat.mainTexture=Texture(id,diffuse);
            mat.SetTexture("_BumpMap",Texture(id,normal,true));
            mat.SetFloat("_BumpScale",.6f); mat.SetFloat("_Glossiness",.2f);
            if (cutout) mat.SetFloat("_Cutoff",.38f); else mat.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(mat); MaterialCache[key]=mat; return mat;
        }
        static void PrepareMaterials()
        {
            MakeMaterial("plant_pot","potted_plant_02","pot_diff","pot_nor_gl");
            MakeMaterial("plant_leaves","potted_plant_02","cutout","leaves_nor_gl",true);
            MakeMaterial("succulent","potted_plant_04","diffuse","nor_gl");
            MakeMaterial("calathea","calathea_orbifolia_01","cutout","nor_gl",true);
            MakeMaterial("book_cover","book_encyclopedia_set_01","cover_diff","cover_nor_gl");
            MakeMaterial("book_paper","book_encyclopedia_set_01","paper_diff","paper_nor_gl");
            MakeMaterial("notebook","binder_notebook","diffuse","nor_gl");
            MakeMaterial("sofa","sofa_03","diffuse","nor_gl");
        }
        static void UpgradeSkybox()
        {
            var sky=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Skybox/sky-1.mat");
            if (!sky || !sky.shader || !sky.HasProperty("_Tex") || !sky.GetTexture("_Tex"))
            {
                var cube=AssetDatabase.LoadAssetAtPath<Cubemap>(Root+"/Skybox/sky-1.png");
                if (!cube) throw new Exception("The owned skybox cubemap could not be imported.");
                string path=Root+"/Materials/OwnedSkybox.mat";
                sky=AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!sky) { sky=new Material(Shader.Find("Skybox/Cubemap")); AssetDatabase.CreateAsset(sky,path); }
                sky.SetTexture("_Tex",cube); sky.SetFloat("_Exposure",1); EditorUtility.SetDirty(sky);
            }
            RenderSettings.skybox=sky;
            DynamicGI.UpdateEnvironment();
            Debug.Log("OWNED_SKYBOX_SELECTED "+AssetDatabase.GetAssetPath(sky));
        }
        static Bounds GetBounds(GameObject model)
        {
            var renderers=model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length==0) throw new Exception("No mesh renderers in "+model.name);
            var bounds=renderers[0].bounds;
            for(int i=1;i<renderers.Length;i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
        static Material MaterialFor(string id,string originalName)
        {
            string name=(originalName??"").ToLowerInvariant();
            if (id=="potted_plant_02") return MaterialCache[(name.Contains("leaf") || name.Contains("leav"))?"plant_leaves":"plant_pot"];
            if (id=="potted_plant_04") return MaterialCache["succulent"];
            if (id=="calathea_orbifolia_01") return MaterialCache["calathea"];
            if (id=="book_encyclopedia_set_01") return MaterialCache[name.Contains("paper")?"book_paper":"book_cover"];
            if (id=="binder_notebook") return MaterialCache["notebook"];
            return MaterialCache["sofa"];
        }
        static GameObject Place(Transform parent,string id,string name,Vector3 point,Vector3 targetSize,float yaw,bool collider)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Realism/"+id+"/"+id+".fbx");
            if (!source) throw new Exception("Missing imported model: "+id);
            var host=new GameObject(name); host.transform.SetParent(parent,true);
            host.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(source); model.transform.SetParent(host.transform,false);
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var materials=renderer.sharedMaterials;
                if (materials.Length==0) materials=new Material[1];
                for(int i=0;i<materials.Length;i++) materials[i]=MaterialFor(id,materials[i]?materials[i].name:renderer.name);
                renderer.sharedMaterials=materials;
            }
            Bounds before=GetBounds(model);
            if (before.size.x<.001f || before.size.y<.001f || before.size.z<.001f) throw new Exception("Invalid model bounds: "+id);
            model.transform.localScale=Vector3.Scale(model.transform.localScale,new Vector3(targetSize.x/before.size.x,targetSize.y/before.size.y,targetSize.z/before.size.z));
            Bounds after=GetBounds(model); model.transform.localPosition+=new Vector3(-after.center.x,-after.min.y,-after.center.z);
            host.transform.position=point; host.transform.rotation=Quaternion.Euler(0,yaw,0);
            if (collider)
            {
                var box=host.AddComponent<BoxCollider>(); box.center=new Vector3(0,targetSize.y*.5f,0); box.size=targetSize;
            }
            return host;
        }
        static void UpgradeChairsAndStudy()
        {
            var study=GameObject.Find("Study tables and chairs");
            if (!study) throw new Exception("Study furniture is missing from the source scene");
            // The source FBX's rotated axis made the visible table top higher
            // than its collider. Rescale the visual in the host's axes, leaving
            // the intended 0.75m/0.8m collider and physical clearance intact.
            foreach(var box in UnityEngine.Object.FindObjectsByType<BoxCollider>())
            {
                if (box.name!="Study table" && box.name!="Quiz desk") continue;
                var before=GetBounds(box.gameObject);
                if (before.size.x<.01f || before.size.y<.01f || before.size.z<.01f) throw new Exception("Invalid table bounds");
                var existing=new List<Transform>(); foreach(Transform child in box.transform) existing.Add(child);
                var wrapper=new GameObject("Corrected table proportions").transform; wrapper.SetParent(box.transform,false);
                foreach(var child in existing) child.SetParent(wrapper,true);
                var goal=box.size;
                wrapper.localScale=new Vector3(goal.x/before.size.x,goal.y/before.size.y,goal.z/before.size.z);
                var after=GetBounds(box.gameObject); wrapper.position+=Vector3.up*(before.min.y-after.min.y);
            }
            var originals=new List<GameObject>();
            foreach(Transform child in study.transform)
                if (child.name=="Study table" || child.name=="Study chair" || child.name=="Study chair seat" || child.name=="Study desk lamp") originals.Add(child.gameObject);
            foreach(var original in originals)
            {
                var copy=UnityEngine.Object.Instantiate(original,study.transform);
                copy.name=original.name; copy.transform.position+=new Vector3(0,0,-6.15f);
            }
            foreach(var box in UnityEngine.Object.FindObjectsByType<BoxCollider>())
            {
                if (box.name!="Study chair" && box.name!="Quiz chair" && box.name!="Mafia chair") continue;
                if (box.transform.childCount==0) continue;
                var model=box.transform.GetChild(0); var before=GetBounds(model.gameObject);
                model.localScale=Vector3.Scale(model.localScale,new Vector3(1,1.12f,1));
                var after=GetBounds(model.gameObject); model.position+=Vector3.up*(before.min.y-after.min.y);
                var size=box.size; size.y*=1.12f; box.size=size; var center=box.center; center.y=size.y*.5f; box.center=center;
            }
            foreach(var seat in UnityEngine.Object.FindObjectsByType<Seat>())
            {
                if (seat.name!="Study chair seat" && seat.name!="Quiz player seat" && seat.name!="Mafia player seat") continue;
                var p=seat.transform.position; p.y=.50f; seat.transform.position=p;
            }
        }
        static void ImproveShelfBooks()
        {
            var source=MaterialCache["book_cover"];
            foreach(var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>())
            {
                if (!renderer.name.StartsWith("LIB_Library__book")) continue;
                var old=renderer.sharedMaterial; if (!old) continue;
                string path=Root+"/Materials/Shelf_"+old.name+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!mat) { mat=new Material(old); AssetDatabase.CreateAsset(mat,path); }
                mat.shader=Shader.Find("StudyImmersive/LibraryBooks");
                mat.color=Color.Lerp(old.color,Color.white,.12f);
                mat.mainTexture=source.mainTexture; mat.SetTexture("_BumpMap",source.GetTexture("_BumpMap"));
                mat.SetFloat("_BumpScale",.45f); mat.SetFloat("_Glossiness",.19f); mat.EnableKeyword("_NORMALMAP");
                EditorUtility.SetDirty(mat); renderer.sharedMaterial=mat;
            }
        }
        static void ReplacePlaceholderPlants(Transform parent)
        {
            foreach(var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>())
            {
                string n=renderer.name;
                if (!(n.StartsWith("LIB_Library__") || n.StartsWith("LIB_Mafia__") || n.StartsWith("LIB_Quiz__"))) continue;
                if (n.Contains("__plant__") || n.Contains("__leaf_light__") || n.Contains("__pot__")) renderer.gameObject.SetActive(false);
            }
            var plants=new GameObject("Detailed potted plants"); plants.transform.SetParent(parent,false);
            foreach(var p in new[] {new Vector3(-14,0,-7),new Vector3(12,0,-13),new Vector3(-15,0,5),new Vector3(14,0,11),new Vector3(-3,0,-13),new Vector3(4,0,-13),new Vector3(-12,0,11),new Vector3(12,0,11)})
                Place(plants.transform,"potted_plant_02","Indoor plant",p,new Vector3(1.35f,1.65f,1.3f),p.x*13,true);
            foreach(var p in new[] {new Vector3(93,0,7.1f),new Vector3(107,0,7.1f),new Vector3(194,0,6.8f),new Vector3(206,0,6.8f)})
                Place(plants.transform,"potted_plant_02","Room plant",p,new Vector3(.85f,1.1f,.8f),p.x*7,true);
        }
        static void HideOldTreeBranches()
        {
            var source=GameObject.Find("LIB_Library__wood_dark__DETAIL");
            if (!source) return;
            var renderer=source.GetComponent<MeshRenderer>(); if (!renderer || !renderer.sharedMaterial) return;
            string path=Root+"/Materials/PrunedTimber.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material=new Material(renderer.sharedMaterial); AssetDatabase.CreateAsset(material,path); }
            material.shader=Shader.Find("StudyImmersive/PrunedTimber");
            if (!material.shader) throw new Exception("Pruned timber shader missing");
            EditorUtility.SetDirty(material); renderer.sharedMaterial=material;
        }
        static void DressTables(Transform parent)
        {
            var props=new GameObject("Books and desk plants"); props.transform.SetParent(parent,false);
            foreach(float z in new[] {-.3f,-6.45f}) foreach(float x in new[] {-3.4f,3.4f})
            {
                Place(props.transform,"book_encyclopedia_set_01","Detailed book set",new Vector3(x+.72f,.76f,z+.04f),new Vector3(.60f,.24f,.19f),0,false);
                Place(props.transform,"binder_notebook","Study notebook",new Vector3(x-.48f,.755f,z+.1f),new Vector3(.42f,.035f,.2f),8,false);
                Place(props.transform,"potted_plant_04","Desk succulent",new Vector3(x-1f,.76f,z+.08f),new Vector3(.18f,.24f,.18f),0,false);
            }
            foreach(float x in new[] {197.2f,202.8f})
                Place(props.transform,"binder_notebook","Quiz notebook",new Vector3(x-.55f,.81f,1.75f),new Vector3(.4f,.035f,.18f),0,false);
            foreach(float x in new[] {98.3f,101.7f})
                Place(props.transform,"binder_notebook","Mafia notes",new Vector3(x,.91f,1),new Vector3(.42f,.04f,.2f),x<100?16:-16,false);
            foreach(float x in new[] {-13.7f,13.7f}) foreach(float y in new[] {.48f,1.05f,1.62f})
                Place(props.transform,"book_encyclopedia_set_01","Bookcase volumes",new Vector3(x,y,-10.25f),new Vector3(.62f,.24f,.18f),0,false);
        }
        static void ReplaceSomeSofas()
        {
            int index=0;
            foreach(var host in UnityEngine.Object.FindObjectsByType<Transform>())
            {
                if (host.name!="Leather sofa") continue;
                if (++index%2!=0) continue;
                var children=new List<GameObject>(); foreach(Transform child in host) children.Add(child.gameObject);
                foreach(var child in children) child.SetActive(false);
                var model=Place(host,"sofa_03","Detailed brown sofa",host.position,new Vector3(3,.95f,1.05f),host.eulerAngles.y,false);
                model.transform.SetParent(host,true);
                var box=host.GetComponent<BoxCollider>(); if (box) { var s=box.size; s.y=.95f; box.size=s; var c=box.center; c.y=.475f; box.center=c; }
            }
        }
        static GameObject CreateNotebookPrefab()
        {
            var root=new GameObject("Escape notebook pedestal");
            Place(root.transform,"binder_notebook","Notebook",new Vector3(0,.86f,0),new Vector3(.4f,.04f,.21f),0,false);
            var stem=GameObject.CreatePrimitive(PrimitiveType.Cylinder); stem.name="Stand"; stem.transform.SetParent(root.transform,false);
            stem.transform.localPosition=new Vector3(0,.42f,0); stem.transform.localScale=new Vector3(.05f,.42f,.05f);
            UnityEngine.Object.DestroyImmediate(stem.GetComponent<Collider>());
            var basePart=GameObject.CreatePrimitive(PrimitiveType.Cylinder); basePart.name="Base"; basePart.transform.SetParent(root.transform,false);
            basePart.transform.localPosition=new Vector3(0,.035f,0); basePart.transform.localScale=new Vector3(.28f,.035f,.28f);
            UnityEngine.Object.DestroyImmediate(basePart.GetComponent<Collider>());
            string path=Root+"/Prefabs/EscapeNotebook.prefab";
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,path); UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }
        static bool Walkable(Vector2Int p)
        {
            float x=p.x*.5f,z=p.y*.5f;
            if (x<460 || x>540 || z< -54 || z>54) return false;
            if (!Physics.Raycast(new Vector3(x,1.8f,z),Vector3.down,out RaycastHit hit,2.8f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) || Mathf.Abs(hit.point.y)>.3f) return false;
            return !Physics.CheckCapsule(new Vector3(x,hit.point.y+.36f,z),new Vector3(x,hit.point.y+1.48f,z),.29f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
        }
        static Vector3[] FindAccessibleNotebookLocations(Vector3 spawn)
        {
            var start=new Vector2Int(Mathf.RoundToInt(spawn.x*2),Mathf.RoundToInt(spawn.z*2));
            var queue=new Queue<Vector2Int>(); var distance=new Dictionary<Vector2Int,int>(); var blocked=new HashSet<Vector2Int>();
            var directions=new[] {Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down};
            queue.Enqueue(start); distance[start]=0;
            while(queue.Count>0 && distance.Count<18000)
            {
                var cell=queue.Dequeue();
                foreach(var d in directions)
                {
                    var next=cell+d;
                    if (distance.ContainsKey(next) || blocked.Contains(next)) continue;
                    if (Walkable(next)) { distance[next]=distance[cell]+1; queue.Enqueue(next); }
                    else blocked.Add(next);
                }
            }
            if (distance.Count<100) throw new Exception("The school entry has insufficient accessible space.");
            var result=new List<Vector3>();
            for(int i=0;i<7;i++)
            {
                float desired=3.5f+i*3.5f, best=float.PositiveInfinity; Vector3 chosen=Vector3.zero;
                foreach(var pair in distance)
                {
                    float path=pair.Value*.5f;
                    if (path<2.5f) continue;
                    var point=new Vector3(pair.Key.x*.5f,0,pair.Key.y*.5f);
                    bool tooClose=false; foreach(var existing in result) if (Vector3.Distance(existing,point)<2.5f) { tooClose=true; break; }
                    if (tooClose) continue;
                    float score=Mathf.Abs(path-desired)+Mathf.Abs(point.x-spawn.x)*.005f;
                    if (score<best) { best=score; chosen=point; }
                }
                if (float.IsPositiveInfinity(best)) throw new Exception("Not enough reachable positions for escape notebooks.");
                result.Add(chosen);
            }
            Debug.Log("ESCAPE_NOTE_LOCATIONS_OK reachable="+distance.Count+" locations="+string.Join("; ",result));
            return result.ToArray();
        }
    }
}
