using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CampusCommons.GrandLibrary;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CampusCommons.LibraryCharacter.Editor
{
    public static class CafeLobbyBuilder
    {
        const string ScenePath = "Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity";
        const string Models = "Assets/UserModels_CafeUpdate/";
        const string Mats = Models + "SceneMaterials/";
        const string Generated = Models + "SceneGenerated/";
        [InitializeOnLoadMethod] static void Init() { EditorApplication.delayCall += Run; }
        static void Run()
        {
            const string request = "Tools/cafe-lobby-build.request";
            if (!File.Exists(request)) return;
            File.Delete(request);
            try { Apply(); File.WriteAllText("Tools/cafe-lobby-build-result.txt", "PASS\n"); }
            catch (Exception ex) { File.WriteAllText("Tools/cafe-lobby-build-result.txt", ex.ToString()); Debug.LogException(ex); }
        }
        static Transform[] All() => SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        static Transform One(string n) { var t = All().FirstOrDefault(x => x.name == n); if (!t) throw new Exception("Missing scene object: " + n); return t; }
        static GameObject Asset(string path) { var a = AssetDatabase.LoadAssetAtPath<GameObject>(Models + path); if (!a) throw new Exception("Missing model: " + path); return a; }
        static Material Mat(string name, Color c, float metallic=0, float gloss=.2f, string texture=null)
        {
            string path=Mats+name+".mat"; var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m=new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m,path); }
            m.color=c; m.SetFloat("_Metallic",metallic);m.SetFloat("_Glossiness",gloss);
            if (texture!=null) { var t=AssetDatabase.LoadAssetAtPath<Texture2D>(Models+texture); if(!t)throw new Exception("Missing texture: "+texture);m.mainTexture=t; }
            EditorUtility.SetDirty(m); return m;
        }
        static Material TransparentGlass()
        {
            var m=Mat("Dome glass",new Color(.57f,.74f,.82f,.11f),.05f,.82f);m.SetFloat("_Mode",3);m.SetInt("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.SrcAlpha);m.SetInt("_DstBlend",(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);m.SetInt("_ZWrite",0);m.DisableKeyword("_ALPHATEST_ON");m.EnableKeyword("_ALPHABLEND_ON");m.DisableKeyword("_ALPHAPREMULTIPLY_ON");m.renderQueue=3000;return m;
        }
        static Bounds BoundsOf(GameObject obj)
        {
            var rs=obj.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled).ToArray();if(rs.Length==0)throw new Exception("No renderers on "+obj.name);
            var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;
        }
        static GameObject Model(string path,Transform parent,string name,Vector3 euler,Vector3 scale,Vector3 center,Material material)
        {
            var go=UnityEngine.Object.Instantiate(Asset(path));go.name=name;go.transform.SetParent(parent,false);go.transform.localRotation=Quaternion.Euler(euler);go.transform.localScale=scale;
            foreach(var r in go.GetComponentsInChildren<Renderer>(true)){var materials=r.sharedMaterials;for(int i=0;i<materials.Length;i++)materials[i]=material;r.sharedMaterials=materials;}
            Bounds b=BoundsOf(go);go.transform.position+=center-b.center;return go;
        }
        static GameObject Box(Transform p,string name,Vector3 position,Vector3 scale,Material mat,bool collider=false)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(p,false);g.transform.position=position;g.transform.rotation=Quaternion.identity;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=mat;if(!collider)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;
        }
        static void Rod(Transform p,string name,Vector3 a,Vector3 b,float radius,Material mat)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=name;g.transform.SetParent(p,false);g.transform.position=(a+b)*.5f;g.transform.up=(b-a).normalized;g.transform.localScale=new Vector3(radius*2,Vector3.Distance(a,b)*.5f,radius*2);g.GetComponent<Renderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
        }
        static Vector3 Radial(float r,float a,float y) { float t=a*Mathf.Deg2Rad; return new Vector3(Mathf.Sin(t)*r,y,Mathf.Cos(t)*r); }
        static void Dome(Transform root,Material bronze)
        {
            var g=new GameObject("Glazed atrium and roof ribs");g.transform.SetParent(root,false);var p=g.transform;
            float[] rr={17.6f,13.2f,8.8f,4.4f,0f};float[] yy={15.0f,17.1f,18.75f,19.70f,20.05f};const int sectors=20;
            for(int i=0;i<sectors;i++){
                float angle=360f*i/sectors;
                for(int j=0;j<rr.Length-1;j++)Rod(p,"Radial roof rib",Radial(rr[j],angle,yy[j]),Radial(rr[j+1],angle,yy[j+1]),.07f,bronze);
                foreach(int ring in new[]{1,2,3})Rod(p,"Circular roof purlin",Radial(rr[ring],angle,yy[ring]),Radial(rr[ring],angle+360f/sectors,yy[ring]),.045f,bronze);
            }
            var vertices=new List<Vector3>();var tris=new List<int>();
            for(int i=0;i<sectors;i++)for(int j=0;j<rr.Length-1;j++){
                float a=360f*i/sectors,b=360f*(i+1)/sectors;int k=vertices.Count;vertices.Add(Radial(rr[j],a,yy[j]));vertices.Add(Radial(rr[j],b,yy[j]));vertices.Add(Radial(rr[j+1],a,yy[j+1]));vertices.Add(Radial(rr[j+1],b,yy[j+1]));
                tris.AddRange(new[]{k,k+1,k+2,k+2,k+1,k+3,k+2,k+1,k,k+3,k+1,k+2});
            }
            var mesh=new Mesh{name="Atrium glazing"};mesh.SetVertices(vertices);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();string path=Generated+"Atrium glazing.asset";AssetDatabase.CreateAsset(mesh,path);
            var glazing=new GameObject("Glass dome");glazing.transform.SetParent(p,false);glazing.AddComponent<MeshFilter>().sharedMesh=mesh;glazing.AddComponent<MeshRenderer>().sharedMaterial=TransparentGlass();
        }
        static Light Point(Transform p,string name,Vector3 pos,float intensity,float range)
        {
            var g=new GameObject(name);g.transform.SetParent(p,false);g.transform.position=pos;var l=g.AddComponent<Light>();l.type=LightType.Point;l.color=new Color(1f,.77f,.49f);l.intensity=intensity;l.range=range;l.shadows=LightShadows.None;return l;
        }
        static void TrimMesh(string source, Func<Vector3,bool> remove)
        {
            var f=All().Select(t=>t.GetComponent<MeshFilter>()).FirstOrDefault(x=>x&&x.sharedMesh&&x.sharedMesh.name==source);if(!f)throw new Exception("Missing mesh "+source);
            var mesh=UnityEngine.Object.Instantiate(f.sharedMesh);var v=mesh.vertices;int removed=0;
            for(int sub=0;sub<mesh.subMeshCount;sub++){
                var input=mesh.GetTriangles(sub);var keep=new List<int>(input.Length);
                for(int i=0;i<input.Length;i+=3){var c=(f.transform.TransformPoint(v[input[i]])+f.transform.TransformPoint(v[input[i+1]])+f.transform.TransformPoint(v[input[i+2]]))/3f;if(remove(c)){removed++;continue;}keep.Add(input[i]);keep.Add(input[i+1]);keep.Add(input[i+2]);}
                mesh.SetTriangles(keep,sub,true);
            }
            if(removed==0)throw new Exception("No old portal geometry removed from "+source);
            mesh.name="DoorUpdate_"+source;mesh.RecalculateBounds();string path=Generated+mesh.name+".asset";AssetDatabase.CreateAsset(mesh,path);f.sharedMesh=mesh;var col=f.GetComponent<MeshCollider>();if(col){col.sharedMesh=null;col.sharedMesh=mesh;}
        }
        static bool NearOldPortal(Vector3 p,float xmin,float xmax,float ymax,float halfWidth)
        {return p.x>xmin&&p.x<xmax&&p.y<ymax&&new[]{-5.5f,0f,5.5f}.Any(z=>Mathf.Abs(p.z-z)<halfWidth);}
        static Material WorldTextMaterial()
        {
            var shader=Shader.Find("Campus/WorldTextDepth");if(!shader)throw new Exception("Depth text shader unavailable");var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var m=AssetDatabase.LoadAssetAtPath<Material>(Mats+"World text depth.mat");if(!m){m=new Material(shader);AssetDatabase.CreateAsset(m,Mats+"World text depth.mat");}m.shader=shader;m.mainTexture=font.material.mainTexture;m.color=Color.white;EditorUtility.SetDirty(m);return m;
        }
        static void FixWorldText(Material material)
        {
            foreach(var text in All().Select(t=>t.GetComponent<TextMesh>()).Where(t=>t)){
                var renderer=text.GetComponent<MeshRenderer>();if(renderer)renderer.sharedMaterial=material;
            }
        }
        static void Sign(Transform root,string name,string value,Vector3 pos,Vector3 rotation,float size,Material textMat)
        {
            var o=new GameObject(name);o.transform.SetParent(root,false);o.transform.position=pos;o.transform.rotation=Quaternion.Euler(rotation);var t=o.AddComponent<TextMesh>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.text=value;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.fontSize=64;t.characterSize=size;t.color=new Color(1f,.91f,.76f);o.GetComponent<MeshRenderer>().sharedMaterial=textMat;
        }
        static void Doors(Transform root,Material textMat,Material wood,Material gold,Material frameMat,Material doorMat)
        {
            foreach(var f in All().Select(t=>t.GetComponent<MeshFilter>()).Where(f=>f&&f.sharedMesh&&f.sharedMesh.name.StartsWith("LIB_Library__portal_")))f.GetComponent<Renderer>().enabled=false;
            TrimMesh("LIB_Library__brass__DETAIL",p=>NearOldPortal(p,12.7f,15.1f,4.15f,1.8f));
            TrimMesh("LIB_Library__stone__SOLID",p=>NearOldPortal(p,12.1f,15.8f,.3f,2.25f));
            TrimMesh("LIB_Library__wood_dark__DETAIL",p=>NearOldPortal(p,13.5f,14.5f,4.8f,1.85f));
            var groups=new[]{("Mafia Portal","Mafia Sign","MAFIA",-5.5f),("Quiz Portal","Quiz Sign","QUIZ",0f),("Escape Portal","Escape Sign","ESCAPE",5.5f)};
            foreach(var item in groups){
                var endpoint=One(item.Item1);var endpointCollider=endpoint.GetComponent<BoxCollider>();if(endpointCollider)endpointCollider.enabled=false;
                var oldSign=One(item.Item2);oldSign.gameObject.SetActive(false);
                var entrance=new GameObject(item.Item3+" entrance");entrance.transform.SetParent(root,false);entrance.transform.position=new Vector3(14.0f,0,item.Item4);entrance.transform.rotation=Quaternion.Euler(0,90,0);var p=entrance.transform;
                Box(p,"Recessed walnut doorway",new Vector3(14.38f,1.58f,item.Item4),new Vector3(.22f,3.16f,2.55f),wood);
                Box(p,"Doorway left trim",new Vector3(14.14f,1.58f,item.Item4-1.14f),new Vector3(.30f,3.16f,.10f),gold);
                Box(p,"Doorway right trim",new Vector3(14.14f,1.58f,item.Item4+1.14f),new Vector3(.30f,3.16f,.10f),gold);
                Box(p,"Doorway lintel",new Vector3(14.14f,3.17f,item.Item4),new Vector3(.30f,.12f,2.38f),gold);
                Box(p,"Door name plaque",new Vector3(14.02f,3.53f,item.Item4),new Vector3(.18f,.40f,2.0f),wood);
                Sign(p,item.Item3+" door label",item.Item3,new Vector3(13.91f,3.53f,item.Item4),new Vector3(0,90,0),.04f,textMat);
                var model=Model("door-wooden-door-metal/source/door wooden.fbx",p,"Imported wooden door",Vector3.zero,Vector3.one*1.17f,new Vector3(14.04f,1.44f,item.Item4),doorMat);
                // Model's frame is taller than its panel; keep the frame still and hinge the panel.
                var rs=model.GetComponentsInChildren<Renderer>().OrderByDescending(r=>r.bounds.size.y).ToArray();if(rs.Length!=2)throw new Exception("Door expected two renderers");
                rs[0].sharedMaterial=frameMat;rs[1].sharedMaterial=doorMat;
                var hinge=new GameObject("Door hinge");hinge.transform.SetParent(model.transform,false);hinge.transform.localPosition=new Vector3(-.60f,0,0);rs[1].transform.SetParent(hinge.transform,true);
                var travel=entrance.AddComponent<LobbyDoor>();travel.hinge=hinge.transform;travel.destinationLabel=item.Item3;travel.room=endpoint.GetComponent<Portal>();travel.escape=endpoint.GetComponent<EscapeGame>();if(!travel.room&&!travel.escape)throw new Exception("Destination missing: "+item.Item3);
            }
        }
        static void Rugs(Transform root,Material round,Material grey)
        {
            foreach(var old in All().Where(t=>t.name.StartsWith("Study Rug ")))old.gameObject.SetActive(false);
            foreach(var t in All().Where(t=>t.parent&&t.parent.name=="Study Furniture"&&t.name.StartsWith("Study Table "))){
                Model("round-carpet/source/extracted/round-carpet.obj",root,"Round rug beneath "+t.name,Vector3.zero,Vector3.one*.041f,new Vector3(t.position.x,.025f,t.position.z),round);
            }
            Model("grey-carpet/source/extracted/model.fbx",root,"Grey cafe rug",new Vector3(90,0,0),new Vector3(2.3f,2.3f,.12f),new Vector3(0,.025f,8.8f),grey);
        }
        static void Cafe(Transform root,Material wood,Material gold,Material ivory,Material steel,Material dark,Material glass,Material grinderMat,Material textMat)
        {
            var g=new GameObject("Cafe and future shop");g.transform.SetParent(root,false);var p=g.transform;
            Box(p,"Walnut service counter",new Vector3(0,.50f,10.85f),new Vector3(5.8f,1.0f,1.00f),wood,true);
            Box(p,"Counter stone worktop",new Vector3(0,1.04f,10.85f),new Vector3(6.05f,.10f,1.14f),ivory);
            Box(p,"Counter lower brass line",new Vector3(0,.12f,10.33f),new Vector3(5.65f,.035f,.03f),gold);
            for(int i=-2;i<=2;i++)Box(p,"Counter inset panel",new Vector3(i*1.10f,.54f,10.337f),new Vector3(.91f,.64f,.02f),dark);
            Box(p,"Cafe header",new Vector3(0,2.95f,11.15f),new Vector3(3.6f,.56f,.15f),wood);
            Box(p,"Cafe header gold top",new Vector3(0,3.23f,11.15f),new Vector3(3.65f,.035f,.19f),gold);
            Sign(p,"Cafe title","ATRIUM CAFE",new Vector3(0,2.96f,11.06f),Vector3.zero,.047f,textMat);
            Rod(p,"Sign suspension left",new Vector3(-1.45f,3.23f,11.15f),new Vector3(-1.45f,4.05f,11.15f),.025f,gold);
            Rod(p,"Sign suspension right",new Vector3(1.45f,3.23f,11.15f),new Vector3(1.45f,4.05f,11.15f),.025f,gold);
            Model("canarian-cafe-coffee-grinder/source/grinder-for export.fbx",p,"Imported coffee grinder",Vector3.zero,Vector3.one*.17f,new Vector3(-1.80f,1.42f,10.87f),grinderMat);
            // Countertop espresso machine, with simple physically scaled details.
            Box(p,"Espresso machine housing",new Vector3(-.55f,1.42f,10.87f),new Vector3(1.38f,.68f,.55f),steel);
            Box(p,"Espresso machine top",new Vector3(-.55f,1.78f,10.87f),new Vector3(1.43f,.055f,.59f),dark);
            Box(p,"Espresso machine front",new Vector3(-.55f,1.43f,10.57f),new Vector3(1.24f,.44f,.045f),dark);
            Box(p,"Espresso drip tray",new Vector3(-.55f,1.105f,10.52f),new Vector3(1.22f,.045f,.33f),steel);
            for(int i=-1;i<=1;i+=2){float x=-.55f+i*.32f;Rod(p,"Coffee group",new Vector3(x,1.49f,10.52f),new Vector3(x,1.34f,10.52f),.07f,gold);Rod(p,"Portafilter handle",new Vector3(x,1.34f,10.50f),new Vector3(x,1.34f,10.33f),.032f,dark);}
            Rod(p,"Steam wand",new Vector3(.12f,1.48f,10.57f),new Vector3(.20f,1.20f,10.49f),.025f,steel);
            for(int i=0;i<4;i++){var cup=GameObject.CreatePrimitive(PrimitiveType.Cylinder);cup.name="Porcelain cup";cup.transform.SetParent(p,false);cup.transform.position=new Vector3(.65f+i*.22f,1.14f,10.85f);cup.transform.localScale=new Vector3(.16f,.09f,.16f);cup.GetComponent<Renderer>().sharedMaterial=ivory;UnityEngine.Object.DestroyImmediate(cup.GetComponent<Collider>());}
            Box(p,"Menu board",new Vector3(1.85f,1.55f,10.85f),new Vector3(.75f,.83f,.10f),wood);
            Box(p,"Menu slate",new Vector3(1.85f,1.55f,10.785f),new Vector3(.64f,.71f,.025f),dark);
            Sign(p,"Menu heading","COFFEE",new Vector3(1.85f,1.68f,10.76f),Vector3.zero,.018f,textMat);
            Sign(p,"Menu detail","CAFE",new Vector3(1.85f,1.46f,10.76f),Vector3.zero,.015f,textMat);
            Box(p,"Costume display base",new Vector3(4.05f,.42f,10.65f),new Vector3(1.35f,.84f,.72f),wood,true);
            Box(p,"Costume display top",new Vector3(4.05f,.86f,10.65f),new Vector3(1.42f,.06f,.80f),ivory);
            Rod(p,"Display rail",new Vector3(3.60f,2.03f,10.68f),new Vector3(4.5f,2.03f,10.68f),.026f,gold);
            Rod(p,"Display post left",new Vector3(3.6f,.90f,10.68f),new Vector3(3.6f,2.03f,10.68f),.026f,gold);
            Rod(p,"Display post right",new Vector3(4.5f,.90f,10.68f),new Vector3(4.5f,2.03f,10.68f),.026f,gold);
            Box(p,"Shop plaque",new Vector3(4.05f,1.21f,10.24f),new Vector3(1.18f,.30f,.08f),dark);
            Sign(p,"Shop label","STYLE SHOP",new Vector3(4.05f,1.21f,10.19f),Vector3.zero,.020f,textMat);
            var staff=new GameObject("Staff interaction anchor - connect NPC here");staff.transform.SetParent(p,false);staff.transform.position=new Vector3(.4f,0,11.70f);
            var shop=new GameObject("Shop interaction anchor - connect catalogue here");shop.transform.SetParent(p,false);shop.transform.position=new Vector3(4.05f,0,9.6f);
            Point(p,"Cafe warm light",new Vector3(0,3.2f,10.5f),.65f,8f);
        }
        [MenuItem("Campus/Build cafe and lobby doors")]
        public static void Apply()
        {
            var scene=SceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!=ScenePath)throw new Exception("Open GrandLibrary_Living and stop Play first");
            if(All().Any(t=>t.name=="Cafe and doorway update"||t.name=="Cafe Shop"))throw new Exception("Cafe update already exists");
            foreach(string path in new[]{"door-wooden-door-metal/source/door wooden.fbx","canarian-cafe-coffee-grinder/source/grinder-for export.fbx","grey-carpet/source/extracted/model.fbx","round-carpet/source/extracted/round-carpet.obj","victorian-chandelier/source/victorian_chandelier_optimized.fbx"})Asset(path);
            One("Grand double-ring chandelier");foreach(var n in new[]{"Mafia Portal","Quiz Portal","Escape Portal","Mafia Sign","Quiz Sign","Escape Sign"})One(n);
            string backup=AssetDatabase.GenerateUniqueAssetPath("Assets/LibraryCharacter/Backups/GrandLibrary_before_cafe_doors.unity");if(!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Could not save a backup of the open scene");
            if(!AssetDatabase.IsValidFolder(Mats.TrimEnd('/')))AssetDatabase.CreateFolder(Models.TrimEnd('/'),"SceneMaterials");
            if(!AssetDatabase.IsValidFolder(Generated.TrimEnd('/')))AssetDatabase.CreateFolder(Models.TrimEnd('/'),"SceneGenerated");
            var root=new GameObject("Cafe and doorway update");Undo.RegisterCreatedObjectUndo(root,"Create cafe and doors");
            var wood=Mat("Walnut",new Color(.20f,.105f,.065f),.05f,.32f);var dark=Mat("Dark inset",new Color(.065f,.05f,.04f),.05f,.22f);var gold=Mat("Aged brass",new Color(.56f,.38f,.16f),.75f,.43f);var ivory=Mat("Warm stone",new Color(.73f,.69f,.61f),.05f,.35f);var steel=Mat("Coffee steel",new Color(.53f,.56f,.55f),.8f,.58f);
            var chandelier=Mat("Victorian chandelier",Color.white,.48f,.40f,"victorian-chandelier/textures/lampara3D_albedo_alpha.png");var door=Mat("Imported door",Color.white,.10f,.28f,"door-wooden-door-metal/textures/door_BaseColor.png");var frame=Mat("Imported door frame",Color.white,.4f,.34f,"door-wooden-door-metal/textures/frame_rBaseColor.png");var round=Mat("Imported round carpet",Color.white,0,.10f,"round-carpet/textures/round-carpet-texture.jpeg");var grey=Mat("Imported grey carpet",Color.white,0,.08f,"grey-carpet/textures/tex_u1_v1_diffuse.jpeg");var grinder=Mat("Imported grinder",Color.white,.38f,.35f,"canarian-cafe-coffee-grinder/textures/Grinder-color.png");var text=WorldTextMaterial();
            var old=One("Grand double-ring chandelier");old.gameObject.SetActive(false);
            Dome(root.transform,gold);Rod(root.transform,"Chandelier suspension",new Vector3(0,14.5f,0),new Vector3(0,20f,0),.04f,gold);
            Model("victorian-chandelier/source/victorian_chandelier_optimized.fbx",root.transform,"Victorian chandelier",Vector3.zero,Vector3.one*.72f,new Vector3(0,12.8f,0),chandelier);
            var chandelierLight=Point(root.transform,"Victorian chandelier warm light",new Vector3(0,10.8f,0),1f,18f);
            Doors(root.transform,text,wood,gold,frame,door);Rugs(root.transform,round,grey);Cafe(root.transform,wood,gold,ivory,steel,dark,null,grinder,text);FixWorldText(text);
            var clock=UnityEngine.Object.FindAnyObjectByType<LocalTimeLighting>();if(clock)clock.interiorLights=clock.interiorLights.Where(l=>l&&l.gameObject.activeInHierarchy).Concat(root.GetComponentsInChildren<Light>()).Distinct().ToArray();
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();if(!EditorSceneManager.SaveScene(scene))throw new Exception("Could not save updated scene");
            Debug.Log("CAFE_LOBBY_BUILD_OK backup="+backup);
        }
    }
}
