using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace CampusCommons.GrandLibrary.Editor
{
    public static class GrandLibrarySceneBuilder
    {
        public const string ScenePath="Assets/GrandLibrary/Scenes/GrandLibrary.unity";
        const string ModelPath="Assets/GrandLibrary/Models/GrandLibrary.fbx";
        const string MaterialsPath="Assets/GrandLibrary/Materials";
        static readonly Dictionary<string,string> Colors=new Dictionary<string,string>
        {
            {"walnut","#755239"},{"wood","#A67550"},{"wood_light","#D1A36D"},{"wood_dark","#594130"},{"floor","#FFFFFF"},
            {"brass","#B99259"},{"bronze","#6D5136"},{"ivory","#D8C8A8"},{"stone","#A39178"},
            {"leather","#654B3A"},{"leather_light","#9A7555"},{"cushion","#B7A188"},{"rug","#8D7860"},
            {"plant","#36573A"},{"leaf_light","#627C4A"},{"soil","#3D3022"},{"pot","#73624C"},
            {"glass","#9DAFA2"},{"black","#171B1B"},{"ink","#263137"},{"chalk","#E8E0C7"},
            {"school_floor","#B2A17F"},{"school_wall","#D6CBB1"},{"greenboard","#274A3C"},
            {"blue","#425C74"},{"red","#8A3E32"},{"gold","#CEAE73"},{"violet","#765780"},
            {"lamp","#FFE1A1"},{"lightline","#FFD08A"},
            {"book01","#56372C"},{"book02","#70553E"},{"book03","#705B43"},{"book04","#314639"},
            {"book05","#353C44"},{"book06","#9A7A4D"},{"book07","#7C4230"},{"book08","#796F5A"},
            {"book09","#59505B"},{"book10","#A69270"},
            {"portal_mafia","#B45DF3"},{"portal_quiz","#5FD1E0"},{"portal_escape","#EE9A50"}
        };
        [MenuItem("Campus/Create grand library scene")]
        public static void CreateFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("Regenerate Grand Library?",
                "This replaces GrandLibrary.unity with the generated version. Save edits under another name first.","Regenerate","Cancel")) return;
            Generate();
        }
        public static void Generate()
        {
            Directory.CreateDirectory("Assets/GrandLibrary/Scenes");
            Directory.CreateDirectory(MaterialsPath);
            Directory.CreateDirectory("Design/GrandLibrary");
            AssetDatabase.Refresh();
            RealisticFurnishing.Prepare();
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (!model) throw new Exception("GrandLibrary.fbx is missing. Run Tools/build_grand_library.py first.");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=(GameObject)PrefabUtility.InstantiatePrefab(model);
            root.name="Grand Library and Game Rooms - 3D model";
            var allMaterials=CreateMaterials();
            int solids=0, renders=0;
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                renders++;
                string[] parts=renderer.name.Split(new[] {"__"},StringSplitOptions.None);
                string material=parts.Length>=3?parts[1]:"chalk";
                renderer.sharedMaterial=allMaterials.ContainsKey(material)?allMaterials[material]:allMaterials["chalk"];
                if (parts.Length>=3 && parts[2].StartsWith("SOLID"))
                {
                    var filter=renderer.GetComponent<MeshFilter>();
                    if (filter && filter.sharedMesh)
                    {
                        var collider=renderer.gameObject.AddComponent<MeshCollider>();
                        collider.sharedMesh=filter.sharedMesh; solids++;
                    }
                }
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccluderStatic);
            }
            if (solids<10 || renders<40) throw new Exception("Unexpected FBX contents: "+solids+" solid batches and "+renders+" renderers.");

            var content=new GameObject("Realistic interiors and user school");
            var seats=new GameObject("SEATS - E to sit");
            RealisticFurnishing.Build(content.transform,Seat);
            UserSchoolEnvironment.Build(content.transform,allMaterials["portal_quiz"],allMaterials["brass"]);

            var triggers=new GameObject("PORTALS - walk through to travel");
            CreatePortal(triggers.transform,"MAFIA",new Vector3(14,0,-5.5f),Portal.Space.Mafia,new Vector3(100,.08f,-5.1f),0,90);
            CreatePortal(triggers.transform,"QUIZ 1 VS 1",new Vector3(14,0,0),Portal.Space.Quiz,new Vector3(200,.08f,-5.1f),0,90);
            CreatePortal(triggers.transform,"ESCAPE ROOM",new Vector3(14,0,5.5f),Portal.Space.Escape,UserSchoolEnvironment.Arrival,0,90);
            PortalLabel(triggers.transform,new Vector3(13.84f,4.45f,-5.5f),"MAFIA");
            PortalLabel(triggers.transform,new Vector3(13.84f,4.45f,0),"QUIZ  1 VS 1");
            PortalLabel(triggers.transform,new Vector3(13.84f,4.45f,5.5f),"ESCAPE ROOM");
            CreatePortal(triggers.transform,"MAFIA TO LOUNGE",new Vector3(100,0,-8.1f),Portal.Space.Library,new Vector3(11.5f,.08f,-5.5f),270);
            CreatePortal(triggers.transform,"QUIZ TO LOUNGE",new Vector3(200,0,-8.1f),Portal.Space.Library,new Vector3(11.5f,.08f,0),270);
            CreatePortal(triggers.transform,"ESCAPE ROOM TO LOUNGE",UserSchoolEnvironment.ReturnPortal,Portal.Space.Library,new Vector3(11.5f,.08f,5.5f),270,0,1.45f,2.2f);
            new GameObject("Player startup").AddComponent<GameStart>();
            Lighting();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene,ScenePath)) throw new Exception("Failed to save Grand Library scene.");
            var buildScenes=new List<EditorBuildSettingsScene>{new EditorBuildSettingsScene(ScenePath,true)};
            foreach(var item in EditorBuildSettings.scenes) if (item.path!=ScenePath) buildScenes.Add(item);
            EditorBuildSettings.scenes=buildScenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("GRAND_LIBRARY_SCENE_OK: "+renders+" renderers, "+solids+" collision meshes, six portal triggers.");
        }
        static Dictionary<string,Material> CreateMaterials()
        {
            var result=new Dictionary<string,Material>();
            var woodTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GrandLibrary/Textures/walnut_grain.png");
            var floorTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GrandLibrary/Textures/floor_planks.png");
            if (!woodTexture || !floorTexture) throw new Exception("Generated wood textures are missing.");
            foreach(var item in Colors)
            {
                bool portal=item.Key.StartsWith("portal_");
                Shader shader=Shader.Find(portal?"GrandLibrary/Portal":"Standard");
                if (!shader) throw new Exception("Missing shader for "+item.Key);
                string path=MaterialsPath+"/"+item.Key+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material) { material=new Material(shader); AssetDatabase.CreateAsset(material,path); }
                material.shader=shader; ColorUtility.TryParseHtmlString(item.Value,out Color color); material.color=color;
                if (!portal)
                {
                    material.SetFloat("_Glossiness",item.Key=="brass"?.45f:.18f);
                    material.SetFloat("_Metallic",item.Key=="brass"?.55f:0);
                    if (item.Key=="wood" || item.Key=="wood_light" || item.Key=="wood_dark" || item.Key=="walnut" || item.Key=="floor")
                    {
                        material.mainTexture=RealisticFurnishing.FloorDiffuse;
                        material.SetTexture("_BumpMap",RealisticFurnishing.FloorNormal);
                        material.SetFloat("_BumpScale",.45f); material.EnableKeyword("_NORMALMAP");
                    }
                    if (item.Key=="lamp" || item.Key=="lightline")
                    {
                        material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor",color*.45f);
                        material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    }
                }
                EditorUtility.SetDirty(material); result.Add(item.Key,material);
            }
            return result;
        }
        static void CreatePortal(Transform parent,string name,Vector3 p,Portal.Space destination,Vector3 arrival,float yaw,float portalYaw=0,float width=2.35f,float height=4.0f)
        {
            var go=new GameObject(name); go.transform.SetParent(parent,false); go.transform.position=p;
            go.transform.rotation=Quaternion.Euler(0,portalYaw,0);
            var collider=go.AddComponent<BoxCollider>(); collider.isTrigger=true;
            collider.center=new Vector3(0,height*.5f,0); collider.size=new Vector3(width,height,.85f);
            var portal=go.AddComponent<Portal>(); portal.destination=destination; portal.arrivalPosition=arrival; portal.arrivalYaw=yaw; portal.portalLabel=name;
        }
        static void PortalLabel(Transform parent,Vector3 position,string label)
        {
            var go=new GameObject(label+" sign"); go.transform.SetParent(parent,false);
            go.transform.position=position; go.transform.rotation=Quaternion.Euler(0,90,0);
            var text=go.AddComponent<TextMesh>(); text.text=label; text.anchor=TextAnchor.MiddleCenter;
            text.alignment=TextAlignment.Center; text.fontSize=64; text.characterSize=.045f; text.color=new Color(1,.91f,.77f);
            var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.font=font; go.GetComponent<MeshRenderer>().sharedMaterial=font.material;
        }
        static void Seat(Transform parent,Vector3 p,float yaw,string name)
        {
            var go=new GameObject(name); go.transform.SetParent(parent,false);
            go.transform.position=p; go.transform.rotation=Quaternion.Euler(0,yaw,0); go.AddComponent<Seat>();
        }
        static void Point(Transform parent, string name,Vector3 p,float intensity,float range)
        {
            var go=new GameObject(name); go.transform.SetParent(parent,false); go.transform.position=p;
            var light=go.AddComponent<Light>(); light.type=LightType.Point; light.color=new Color(1,.70f,.38f);
            light.intensity=intensity; light.range=range; light.shadows=LightShadows.None;
        }
        static void Lighting()
        {
            var parent=new GameObject("Warm architectural lighting").transform;
            var sun=new GameObject("Skylight"); sun.transform.SetParent(parent,false); sun.transform.rotation=Quaternion.Euler(52,-30,0);
            var directional=sun.AddComponent<Light>(); directional.type=LightType.Directional;
            directional.color=new Color(1,.85f,.7f); directional.intensity=.63f; directional.shadows=LightShadows.Soft;
            foreach(float x in new[] {-10f,0f,10f})
                foreach(float z in new[] {-6f,7f}) Point(parent,"Lounge warm pendant",new Vector3(x,8,z),1.35f,20);
            foreach(float x in new[] {100f,200f,300f}) Point(parent,"Room ceiling light",new Vector3(x,4,0),1.9f,20);
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.43f,.42f,.40f);
            RenderSettings.ambientEquatorColor=new Color(.31f,.28f,.24f);
            RenderSettings.ambientGroundColor=new Color(.17f,.14f,.12f);
            var skyPath=MaterialsPath+"/LibrarySky.mat";
            var sky=AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (!sky)
            {
                sky=new Material(Shader.Find("Skybox/Procedural"));
                sky.SetFloat("_AtmosphereThickness",.9f); sky.SetFloat("_Exposure",1.05f);
                sky.SetColor("_SkyTint",new Color(.57f,.65f,.68f));
                AssetDatabase.CreateAsset(sky,skyPath);
            }
            RenderSettings.skybox=sky; RenderSettings.fog=false;
            QualitySettings.pixelLightCount=8; QualitySettings.shadowDistance=65;
            QualitySettings.shadows=ShadowQuality.All; QualitySettings.shadowResolution=ShadowResolution.High;
        }
    }
}
