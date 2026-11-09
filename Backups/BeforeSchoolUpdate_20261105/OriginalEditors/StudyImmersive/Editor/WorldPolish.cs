using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CampusCommons.GrandLibrary;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CampusCommons.StudyImmersive.Editor
{
    public static class WorldPolish
    {
        const string Root="Assets/StudyImmersive";
        public const string OutScene=Root+"/Scenes/GrandLibrary_Living.unity";
        static readonly Dictionary<string,Material> M=new Dictionary<string,Material>();
        static readonly string Extra=Root+"/RealismExtra/";
        static readonly string Owned=Root+"/OwnedTerrain/";
        [MenuItem("Campus/Open living library and set as startup")]
        public static void OpenAsMainScene()
        {
            if(!AssetDatabase.LoadAssetAtPath<SceneAsset>(OutScene)) {EditorUtility.DisplayDialog("Living library","The improved scene is not imported yet.","OK");return;}
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scenes=new List<EditorBuildSettingsScene>{new EditorBuildSettingsScene(OutScene,true)};
            scenes.AddRange(EditorBuildSettings.scenes.Where(s=>s.path!=OutScene));
            EditorBuildSettings.scenes=scenes.ToArray();
            EditorSceneManager.OpenScene(OutScene,OpenSceneMode.Single);
        }
        [MenuItem("Campus/Testing/Visit escape portal in Play mode")]
        public static void VisitEscapePortalForTesting()
        {
            if(!EditorApplication.isPlaying) {EditorUtility.DisplayDialog("Testing","Enter Play mode first.","OK");return;}
            var player=UnityEngine.Object.FindFirstObjectByType<LibraryPlayer>();
            if(!player || player.currentSpace!=LibraryPortal.Space.Library) return;
            var body=player.GetComponent<CharacterController>();if(!body) return;
            body.enabled=false;player.transform.position=new Vector3(13.8f,.08f,5.5f);body.enabled=true;Physics.SyncTransforms();
        }
        public static void Build()
        {
            AssetDatabase.Refresh(); M.Clear();
            Directory.CreateDirectory(Root+"/LivingMaterials");
            Directory.CreateDirectory(Root+"/Geometry");
            MaterialFrom("tree trunk",Extra+"tree_small_02/diffuse.jpg",Extra+"tree_small_02/nor_gl.jpg");
            MaterialFrom("tree branches",Extra+"tree_small_02/branch_diff.jpg",Extra+"tree_small_02/branch_nor_gl.jpg");
            MaterialFrom("tree leaves",Extra+"tree_small_02/leaves_cutout.png",Extra+"tree_small_02/leaves_nor_gl.jpg",true);
            MaterialFrom("owned grass A",Owned+"Details/Textures/Grass_A_BaseColor.tif",Owned+"Details/Textures/Grass_A_Normal.tif",true);
            MaterialFrom("owned grass C",Owned+"Details/Textures/Grass_C_BaseColor.tif",Owned+"Details/Textures/Grass_C_Normal.tif",true);
            MaterialFrom("owned fern",Owned+"Details/Textures/Fern_BaseColor.tif",Owned+"Details/Textures/Fern_Normal.tif",true);
            MaterialFrom("table wood","Assets/GrandLibrary/Realism/wood_floor/wood_floor_diff.jpg","Assets/GrandLibrary/Realism/wood_floor/wood_floor_normal.jpg");
            MaterialFrom("rug weave",Extra+"poly_wool_herringbone/diffuse.jpg",Extra+"poly_wool_herringbone/nor_gl.jpg");
            SolidMaterial("planter",new Color(.24f,.18f,.13f),.12f,.18f);
            SolidMaterial("soil",new Color(.12f,.09f,.06f),0,.05f);
            SolidMaterial("trim",new Color(.53f,.36f,.15f),.65f,.35f);
            var scene=EditorSceneManager.OpenScene(ImmersiveSceneBuilder.ScenePath,OpenSceneMode.Single);
            var root=new GameObject("LIVING LIBRARY - trees, owned package foliage and furniture");
            UpgradeMafia();
            AddStudyTables();
            ImproveRugs();
            AddRoomDetails(root.transform);
            AddGreenery(root.transform);
            var sky=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Skybox/sky-1.mat");
            if(!sky || !sky.HasProperty("_Tex") || !sky.GetTexture("_Tex")) throw new Exception("Owned skybox material is missing its cubemap");
            RenderSettings.skybox=sky; RenderSettings.ambientMode=AmbientMode.Skybox;
            DynamicGI.UpdateEnvironment();
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene,OutScene)) throw new Exception("Could not save living library scene");
            AssetDatabase.SaveAssets();
            Debug.Log("WORLD_POLISH_BUILD_OK "+OutScene);
        }
        static Material MaterialFrom(string key,string colorPath,string normalPath,bool foliage=false)
        {
            var color=AssetDatabase.LoadAssetAtPath<Texture2D>(colorPath);
            var importer=AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if(!color || !importer) throw new Exception("Missing textures for "+key);
            if(importer.textureType!=TextureImporterType.NormalMap) { importer.textureType=TextureImporterType.NormalMap;importer.SaveAndReimport(); }
            var normal=AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            var shader=Shader.Find(foliage?"StudyImmersive/RealFoliage":"Standard");
            if(!shader || !normal) throw new Exception("Missing material support for "+key);
            string path=Root+"/LivingMaterials/"+key.Replace(' ','_')+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material) { material=new Material(shader);AssetDatabase.CreateAsset(material,path); }
            material.shader=shader;material.color=Color.white;material.mainTexture=color;
            material.SetTexture("_BumpMap",normal); material.SetFloat("_BumpScale",.6f);
            material.SetFloat("_Glossiness",foliage?.12f:.22f);
            if(foliage) material.SetFloat("_Cutoff",.28f); else material.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material); M[key]=material;return material;
        }
        static Material SolidMaterial(string key,Color color,float metallic,float smooth)
        {
            string path=Root+"/LivingMaterials/"+key+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat) {mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,path);}
            mat.color=color;mat.SetFloat("_Metallic",metallic);mat.SetFloat("_Glossiness",smooth);
            EditorUtility.SetDirty(mat);M[key]=mat;return mat;
        }
        static void UpgradeMafia()
        {
            var top=GameObject.Find("LIB_Mafia__wood__SOLID");
            var baseObj=GameObject.Find("LIB_Mafia__wood_dark__SOLID");
            var gold=GameObject.Find("LIB_Mafia__gold__DETAIL");
            if(!top || !baseObj || !gold) throw new Exception("Mafia table geometry was not found");
            float lowering=.145f;
            top.transform.position+=Vector3.down*lowering;
            top.GetComponent<Renderer>().sharedMaterial=M["table wood"];
            gold.transform.position+=Vector3.down*lowering;
            var filter=baseObj.GetComponent<MeshFilter>(); var collider=baseObj.GetComponent<MeshCollider>();
            if(!filter || !filter.sharedMesh || !collider) throw new Exception("Mafia table base collision is missing");
            var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh); var verts=mesh.vertices; int changed=0;
            for(int i=0;i<verts.Length;i++)
            {
                var p=baseObj.transform.TransformPoint(verts[i]);
                float dx=p.x-100,dz=p.z-1;
                if(dx*dx+dz*dz<1.05f*1.05f && p.y>=-.01f && p.y<.84f)
                {p.y*=.675f/.82f;verts[i]=baseObj.transform.InverseTransformPoint(p);changed++;}
            }
            if(changed<10) throw new Exception("Mafia table base vertices were not found");
            mesh.vertices=verts;mesh.RecalculateBounds();mesh.name="Mafia resized table base";
            string path=Root+"/Geometry/MafiaTableBase.asset"; var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing) {EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}
            else AssetDatabase.CreateAsset(mesh,path);
            filter.sharedMesh=mesh;collider.sharedMesh=null;collider.sharedMesh=mesh;
            foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if(t.name=="Mafia chair" || t.name=="Mafia player seat")
                {
                    var d=t.position-new Vector3(100,0,1);t.position=new Vector3(100+d.x*(3.55f/3.9f),t.position.y,1+d.z*(3.55f/3.9f));
                }
                else if(t.name=="Mafia notes") t.position+=Vector3.down*lowering;
            }
        }
        static void AddStudyTables()
        {
            var study=GameObject.Find("Study tables and chairs");if(!study) throw new Exception("Study area missing");
            var copyList=new List<Transform>();
            foreach(Transform t in study.transform)
                if((t.name=="Study table" || t.name=="Study chair" || t.name=="Study chair seat" || t.name=="Study desk lamp") && t.position.z>-2 && t.position.z<1.5f) copyList.Add(t);
            foreach(var t in copyList) {var clone=UnityEngine.Object.Instantiate(t.gameObject,study.transform);clone.name=t.name;clone.transform.position+=Vector3.forward*6.15f;}
            var props=GameObject.Find("Books and desk plants");if(!props) throw new Exception("Study props missing");
            copyList.Clear();
            foreach(Transform t in props.transform)
                if((t.name=="Study notebook" || t.name=="Detailed book set" || t.name=="Desk succulent") && Mathf.Abs(t.position.x)<8 && t.position.z>-2 && t.position.z<1.5f) copyList.Add(t);
            foreach(var t in copyList) {var clone=UnityEngine.Object.Instantiate(t.gameObject,props.transform);clone.name=t.name;clone.transform.position+=Vector3.forward*6.15f;}
        }
        static void ImproveRugs()
        {
            foreach(var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if(r.name=="LIB_Library__rug__DETAIL" || r.name=="LIB_Mafia__rug__DETAIL") r.sharedMaterial=M["rug weave"];
        }
        static Bounds ModelBounds(GameObject go)
        {
            var rs=go.GetComponentsInChildren<Renderer>(true);if(rs.Length==0) throw new Exception("No geometry in "+go.name);
            var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;
        }
        static GameObject Model(Transform parent,string path,string name,Vector3 pos,Vector3 size,float yaw,Func<string,int,Material> choose,bool collider=false)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!prefab) throw new Exception("Missing model: "+path);
            var host=new GameObject(name);host.transform.SetParent(parent,false);
            var wrapper=new GameObject("Scaled geometry").transform;wrapper.SetParent(host.transform,false);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab);model.transform.SetParent(wrapper,false);
            foreach(var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var slots=r.sharedMaterials;for(int i=0;i<slots.Length;i++)slots[i]=choose(slots[i]?slots[i].name:r.name,i);
                r.sharedMaterials=slots;
            }
            var b=ModelBounds(model);if(b.size.x<.001f || b.size.y<.001f || b.size.z<.001f)throw new Exception("Degenerate bounds: "+path);
            wrapper.localScale=new Vector3(size.x/b.size.x,size.y/b.size.y,size.z/b.size.z);
            b=ModelBounds(model);wrapper.position+=new Vector3(-b.center.x,-b.min.y,-b.center.z);
            host.transform.SetPositionAndRotation(pos,Quaternion.Euler(0,yaw,0));
            if(collider){var c=host.AddComponent<BoxCollider>();c.center=new Vector3(0,size.y*.5f,0);c.size=size;}
            return host;
        }
        static Material Existing(string path) {var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m)throw new Exception("Missing material "+path);return m;}
        static void AddRoomDetails(Transform parent)
        {
            var group=new GameObject("Room shelves, books and lamps");group.transform.SetParent(parent,false);
            var shelf=Existing("Assets/GrandLibrary/Realism/Materials/wooden_bookshelf_worn.mat");
            var lamp=Existing("Assets/GrandLibrary/Realism/Materials/desk_lamp_arm_01.mat");
            var cover=Existing(Root+"/Materials/book_cover.mat");var paper=Existing(Root+"/Materials/book_paper.mat");
            foreach(var p in new[]{new Vector3(92.3f,0,-.5f),new Vector3(107.7f,0,-.5f),new Vector3(193.7f,0,-1),new Vector3(206.3f,0,-1)})
            {
                Model(group.transform,"Assets/GrandLibrary/Realism/wooden_bookshelf_worn/wooden_bookshelf_worn.fbx","Room detailed bookcase",p,new Vector3(1.2f,2.25f,.5f),0,(n,i)=>shelf,true);
                foreach(float h in new[]{.45f,1.0f,1.55f})
                    Model(group.transform,Root+"/Realism/book_encyclopedia_set_01/book_encyclopedia_set_01.fbx","Room book volumes",p+new Vector3(0,h,-.2f),new Vector3(.58f,.22f,.15f),0,(n,i)=>n.ToLowerInvariant().Contains("paper")?paper:cover);
            }
            foreach(float x in new[]{197.2f,202.8f})
            {
                Model(group.transform,"Assets/GrandLibrary/Realism/desk_lamp_arm_01/desk_lamp_arm_01.fbx","Quiz reading lamp",new Vector3(x+.62f,.81f,1.82f),new Vector3(.28f,.47f,.28f),x<200?15:-15,(n,i)=>lamp);
                Model(group.transform,Root+"/Realism/book_encyclopedia_set_01/book_encyclopedia_set_01.fbx","Quiz books",new Vector3(x-.5f,.81f,1.98f),new Vector3(.36f,.17f,.14f),0,(n,i)=>n.ToLowerInvariant().Contains("paper")?paper:cover);
            }
            foreach(float x in new[]{98.8f,101.2f})
                Model(group.transform,"Assets/GrandLibrary/Realism/desk_lamp_arm_01/desk_lamp_arm_01.fbx","Mafia reading lamp",new Vector3(x,.755f,1),new Vector3(.28f,.48f,.28f),x<100?30:-30,(n,i)=>lamp);
        }
        static GameObject Cylinder(Transform parent,string name,Vector3 pos,Vector3 scale,Material material,bool collider=false)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=name;g.transform.SetParent(parent,false);g.transform.position=pos;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=material;
            if(!collider)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;
        }
        static void Detail(Transform parent,string meshName,string material,Vector3 pos,float width,float height,float yaw)
        {
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Owned+"Details/Models/"+meshName+".asset");if(!mesh)throw new Exception("Missing owned vegetation mesh "+meshName);
            var b=mesh.bounds;if(b.size.x<.001f || b.size.y<.001f || b.size.z<.001f)throw new Exception("Bad vegetation bounds "+meshName);
            var host=new GameObject("Owned "+meshName);host.transform.SetParent(parent,false);host.transform.SetPositionAndRotation(pos,Quaternion.Euler(0,yaw,0));
            var g=new GameObject("Foliage mesh");g.transform.SetParent(host.transform,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=M[material];
            var s=new Vector3(width/b.size.x,height/b.size.y,width/b.size.z);g.transform.localScale=s;
            g.transform.localPosition=new Vector3(-b.center.x*s.x,-b.min.y*s.y,-b.center.z*s.z);
        }
        static void Planter(Transform parent,Vector3 pos,float diameter,bool tree,int seed)
        {
            var host=new GameObject(tree?"Indoor tree planter":"Fern and grass planter");host.transform.SetParent(parent,false);
            float h=tree?.42f:.25f;
            Cylinder(host.transform,"Ceramic planter",pos+Vector3.up*(h*.5f),new Vector3(diameter,h*.5f,diameter),M["planter"],true);
            Cylinder(host.transform,"Soil",pos+Vector3.up*(h+.012f),new Vector3(diameter*.9f,.018f,diameter*.9f),M["soil"]);
            Cylinder(host.transform,"Brass rim",pos+Vector3.up*(h-.025f),new Vector3(diameter*1.02f,.018f,diameter*1.02f),M["trim"]);
            float y=pos.y+h+.03f;
            if(tree)
                Model(host.transform,Extra+"tree_small_02/tree_small_optimized.fbx","Detailed indoor tree",new Vector3(pos.x,y,pos.z),new Vector3(3.3f,4.25f,3.3f),seed*29,(name,i)=>{string n=name.ToLowerInvariant();return M[n.Contains("branch")?"tree branches":n.Contains("leav")?"tree leaves":n.Contains("trunk")?"tree trunk":i==0?"tree branches":i==1?"tree leaves":"tree trunk"];});
            int count=tree?7:3;
            for(int i=0;i<count;i++)
            {
                float a=(i*137.5f+seed*27)*Mathf.Deg2Rad, r=diameter*(tree?.28f:.2f);
                Vector3 at=new Vector3(pos.x+Mathf.Sin(a)*r,y,pos.z+Mathf.Cos(a)*r);
                bool fern=i%3==0;string mesh=fern?(i%2==0?"Fern_A":"Fern_B"):(i%2==0?"Grass_A":"Grass_C");
                Detail(host.transform,mesh,fern?"owned fern":i%2==0?"owned grass A":"owned grass C",at,tree?.48f:.28f,tree?.47f:.36f,i*53+seed*17);
            }
        }
        static void AddGreenery(Transform parent)
        {
            var plants=new GameObject("Real trees and owned Unity grass and ferns");plants.transform.SetParent(parent,false);
            // The source scene batches the old planters' soil into separate
            // renderers. Hide those as well, otherwise their disks float inside
            // the new, lower planters after the old pots have been removed.
            foreach(var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if((r.name.StartsWith("LIB_Library__") || r.name.StartsWith("LIB_Mafia__") || r.name.StartsWith("LIB_Quiz__")) && r.name.Contains("__soil__")) r.gameObject.SetActive(false);
            var ground=new[]{new Vector3(-14,0,-7),new Vector3(12,0,-13),new Vector3(-15,0,5),new Vector3(14,0,11)};
            foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if(t && t.name=="Indoor plant" && ground.Any(p=>Vector3.Distance(t.position,p)<.1f)) UnityEngine.Object.DestroyImmediate(t.gameObject);
            for(int i=0;i<ground.Length;i++)Planter(plants.transform,ground[i],1.5f,true,i);
            foreach(float level in new[]{4.8f,9.6f})
                for(int i=0;i<8;i++) {float a=(i*45+23)*Mathf.Deg2Rad;Planter(plants.transform,new Vector3(Mathf.Sin(a)*16.4f,level,Mathf.Cos(a)*16.4f),.62f,false,i+(int)level);}
            foreach(var p in new[]{new Vector3(93,0,7.1f),new Vector3(107,0,7.1f),new Vector3(194,0,6.8f),new Vector3(206,0,6.8f)})
                Planter(plants.transform,p+new Vector3(0,0,-1.1f),.65f,false,Mathf.RoundToInt(p.x));
        }
        public static void BuildAndRender()
        {
            Build();
            Preview("living-lounge.png",new Vector3(-3,2.7f,-10),new Vector3(4,3.5f,1),72);
            Preview("living-tree.png",new Vector3(-10,1.9f,-9.5f),new Vector3(-14,2.2f,-7),60);
            Preview("living-grass.png",new Vector3(9.7f,1.3f,-11.4f),new Vector3(12,.48f,-13),55);
            Preview("living-study.png",new Vector3(-6,1.65f,-3.8f),new Vector3(-3.4f,.85f,-.3f),58);
            Preview("living-mafia.png",new Vector3(100,2.1f,-6.3f),new Vector3(100,1.6f,1),68);
            Preview("living-quiz.png",new Vector3(200,2.1f,-6.2f),new Vector3(200,2,3),64);
            Preview("living-sky.png",new Vector3(-6,2,-5),new Vector3(0,14,0),72);
            Debug.Log("LIVING_PREVIEWS_OK");
        }
        static void Preview(string name,Vector3 position,Vector3 look,float fov)
        {
            Directory.CreateDirectory("Design/Immersive");
            var g=new GameObject("Living preview camera");var c=g.AddComponent<Camera>();
            c.transform.position=position;c.transform.LookAt(look);c.fieldOfView=fov;c.nearClipPlane=.08f;c.farClipPlane=120;c.clearFlags=CameraClearFlags.Skybox;
            var rt=new RenderTexture(1440,900,24){antiAliasing=4};c.targetTexture=rt;c.Render();RenderTexture.active=rt;
            var image=new Texture2D(1440,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();
            File.WriteAllBytes("Design/Immersive/"+name,image.EncodeToPNG());c.targetTexture=null;RenderTexture.active=null;
            UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(g);
        }
        public static void Inspect()
        {
            AssetDatabase.Refresh();
            foreach(string id in new[]{"tree_small_02","round_wooden_table_02","dining_table","coffee_table_round_01"})
            {
                var go=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/RealismExtra/"+id+"/"+id+".fbx");
                if(!go) { Debug.LogError("MODEL_MISSING "+id); continue; }
                foreach(var renderer in go.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var filter=renderer.GetComponent<MeshFilter>();
                    Debug.Log("MODEL_ITEM "+id+" name="+renderer.name+" verts="+(filter&&filter.sharedMesh?filter.sharedMesh.vertexCount:0)+" mats="+string.Join(",",renderer.sharedMaterials.Select(m=>m?m.name:"<null>")));
                }
            }
            foreach(string name in new[]{"Grass_A","Grass_B","Grass_C","Fern_A","Fern_B","Fern_C"})
            {
                string path=Root+"/OwnedTerrain/Details/Models/"+name+".asset";
                var all=AssetDatabase.LoadAllAssetsAtPath(path);
                Debug.Log("OWNED_DETAIL "+name+" "+string.Join(",",all.Select(a=>a.GetType().Name+":"+a.name)));
            }
            foreach(string name in new[]{"Grass_A","Grass_C","Fern"})
            {
                var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/OwnedTerrain/Details/Textures/"+name+"_BaseColor.tif");
                Debug.Log("OWNED_TEXTURE "+name+" "+(tex?tex.width+"x"+tex.height+" "+tex.format:"MISSING"));
            }
            var scene=EditorSceneManager.OpenScene(ImmersiveSceneBuilder.ScenePath);
            Physics.SyncTransforms();
            foreach(var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if(renderer.name.StartsWith("LIB_Mafia") && (renderer.name.Contains("wood") || renderer.name.Contains("gold")))
                {
                    var f=renderer.GetComponent<MeshFilter>(); var c=renderer.GetComponent<MeshCollider>();
                    Debug.Log("MAFIA_MESH "+renderer.name+" "+renderer.bounds+" vertices="+(f&&f.sharedMesh?f.sharedMesh.vertexCount:0)+" collider="+(bool)c);
                }
            foreach(var hit in Physics.RaycastAll(new Vector3(100,4,1),Vector3.down,5).OrderBy(h=>h.distance))
                Debug.Log("MAFIA_RAY "+hit.collider.name+" y="+hit.point.y);
            foreach(string n in new[]{"Mafia chair","Mafia player seat","Mafia notes","Quiz chair","Study table","Study chair"})
            {
                var go=GameObject.Find(n); if(go) Debug.Log("FURNITURE "+n+" pos="+go.transform.position);
            }
            Debug.Log("WORLD_POLISH_INSPECT_OK");
        }
    }
}
