using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CampusCommons.GrandLibrary.Editor
{
    // Original Poly Haven CC0 models; source URLs and checksums are kept in sources.json.
    public static class RealisticFurnishing
    {
        const string Root="Assets/GrandLibrary/Realism";
        static readonly string[] Ids={"sofa_02","mid_century_lounge_chair","dining_chair_02","round_wooden_table_01","wooden_table_02","wooden_bookshelf_worn","modern_ceiling_lamp_01","desk_lamp_arm_01"};
        static readonly Dictionary<string,Material> Materials=new Dictionary<string,Material>();
        public static Texture2D FloorDiffuse { get; private set; }
        public static Texture2D FloorNormal { get; private set; }

        public static void Prepare()
        {
            Directory.CreateDirectory(Root+"/Materials");
            AssetDatabase.Refresh(); Materials.Clear();
            foreach(string id in Ids)
            {
                var diffuse=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/"+id+"/"+id+"_diff.jpg");
                var normal=ImportNormal(Root+"/"+id+"/"+id+"_normal.jpg");
                if (!diffuse || !normal || !AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/"+id+"/"+id+".fbx"))
                    throw new Exception("Missing realistic model or texture: "+id);
                string path=Root+"/Materials/"+id+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material) { material=new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(material,path); }
                material.color=Color.white; material.mainTexture=diffuse;
                material.SetTexture("_BumpMap",normal); material.EnableKeyword("_NORMALMAP");
                material.SetFloat("_BumpScale",.65f); material.SetFloat("_Glossiness",.28f);
                EditorUtility.SetDirty(material); Materials[id]=material;
            }
            FloorDiffuse=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/wood_floor/wood_floor_diff.jpg");
            FloorNormal=ImportNormal(Root+"/wood_floor/wood_floor_normal.jpg");
            if (!FloorDiffuse || !FloorNormal) throw new Exception("Real wood floor textures are missing");
        }
        static Texture2D ImportNormal(string path)
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) return null;
            if (importer.textureType!=TextureImporterType.NormalMap)
            { importer.textureType=TextureImporterType.NormalMap; importer.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static Bounds BoundsOf(GameObject model)
        {
            var renderers=model.GetComponentsInChildren<Renderer>();
            if (renderers.Length==0) throw new Exception("No renderers in "+model.name);
            var bounds=renderers[0].bounds;
            for(int i=1;i<renderers.Length;i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
        static GameObject Place(Transform parent,string id,string name,Vector3 position,Vector3 size,float yaw,bool solid)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/"+id+"/"+id+".fbx");
            if (!asset) throw new Exception("Missing model: "+id);
            var host=new GameObject(name); host.transform.SetParent(parent,false);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(asset);
            model.transform.SetParent(host.transform,false);
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var slots=renderer.sharedMaterials;
                if (slots.Length==0) slots=new Material[1];
                for(int i=0;i<slots.Length;i++) slots[i]=Materials[id];
                renderer.sharedMaterials=slots;
            }
            var original=BoundsOf(model);
            if (original.size.x<.001f || original.size.y<.001f || original.size.z<.001f)
                throw new Exception("Degenerate bounds on "+id);
            model.transform.localScale=Vector3.Scale(model.transform.localScale,new Vector3(size.x/original.size.x,size.y/original.size.y,size.z/original.size.z));
            var adjusted=BoundsOf(model);
            model.transform.localPosition+=new Vector3(-adjusted.center.x,-adjusted.min.y,-adjusted.center.z);
            host.transform.position=position; host.transform.rotation=Quaternion.Euler(0,yaw,0);
            if (solid)
            {
                var collider=host.AddComponent<BoxCollider>(); collider.center=new Vector3(0,size.y*.5f,0); collider.size=size;
            }
            return host;
        }
        public static void Build(Transform parent,Action<Transform,Vector3,float,string> seat)
        {
            var social=new GameObject("Realistic lounge furniture"); social.transform.SetParent(parent,false);
            foreach(float x in new[] {-9f,9f}) foreach(float z in new[] {-5f,3f})
            {
                Place(social.transform,"sofa_02","Leather sofa",new Vector3(x,0,z-1.75f),new Vector3(3,.9f,1.05f),0,true);
                Place(social.transform,"round_wooden_table_01","Low round table",new Vector3(x,0,z),new Vector3(1.5f,.55f,1.5f),0,true);
                foreach(float side in new[] {-1f,1f})
                {
                    float sx=x+side*1.65f, yaw=side<0?125:235;
                    Place(social.transform,"mid_century_lounge_chair","Lounge armchair",new Vector3(sx,0,z+.75f),new Vector3(.95f,1.05f,1f),yaw,true);
                    seat(social.transform,new Vector3(sx,.61f,z+.75f),yaw,"Lounge armchair seat");
                }
                foreach(float dx in new[] {-.8f,0f,.8f}) seat(social.transform,new Vector3(x+dx,.62f,z-1.75f),0,"Sofa seat");
            }
            var study=new GameObject("Study tables and chairs"); study.transform.SetParent(parent,false);
            foreach(float x in new[] {-3.4f,3.4f})
            {
                Place(study.transform,"wooden_table_02","Study table",new Vector3(x,0,-.3f),new Vector3(2.55f,.75f,1.05f),0,true);
                foreach(float dx in new[] {-.72f,.72f}) foreach(float side in new[] {-1f,1f})
                {
                    float z=-.3f+side*.93f, yaw=side<0?0:180;
                    Place(study.transform,"dining_chair_02","Study chair",new Vector3(x+dx,0,z),new Vector3(.56f,.98f,.62f),yaw,true);
                    seat(study.transform,new Vector3(x+dx,.61f,z),yaw,"Study chair seat");
                }
                Place(study.transform,"desk_lamp_arm_01","Study desk lamp",new Vector3(x,.76f,-.3f),new Vector3(.4f,.65f,.4f),0,false);
            }
            foreach(float x in new[] {-13.7f,13.7f})
                Place(social.transform,"wooden_bookshelf_worn","Detailed bookcase",new Vector3(x,0,-10.5f),new Vector3(1.5f,2.45f,.65f),0,true);
            foreach(float x in new[] {-10f,10f}) foreach(float z in new[] {-6f,7f})
                Place(social.transform,"modern_ceiling_lamp_01","Pendant lamp",new Vector3(x,10.6f,z),new Vector3(.65f,1.3f,.65f),0,false);

            var mafia=new GameObject("Mafia furniture"); mafia.transform.SetParent(parent,false);
            for(int i=0;i<10;i++)
            {
                float a=i*Mathf.PI*2/10, x=100+Mathf.Sin(a)*3.9f, z=1+Mathf.Cos(a)*3.9f, yaw=180+a*Mathf.Rad2Deg;
                Place(mafia.transform,"dining_chair_02","Mafia chair",new Vector3(x,0,z),new Vector3(.58f,.98f,.64f),yaw,true);
                seat(mafia.transform,new Vector3(x,.61f,z),yaw,"Mafia player seat");
            }
            Place(mafia.transform,"modern_ceiling_lamp_01","Mafia pendant",new Vector3(100,3.55f,1),new Vector3(.8f,1.05f,.8f),0,false);
            var quiz=new GameObject("Two player quiz furniture"); quiz.transform.SetParent(parent,false);
            foreach(float x in new[] {197.2f,202.8f})
            {
                Place(quiz.transform,"wooden_table_02","Quiz desk",new Vector3(x,0,1.8f),new Vector3(2,.8f,.9f),0,true);
                Place(quiz.transform,"dining_chair_02","Quiz chair",new Vector3(x,0,.65f),new Vector3(.58f,.98f,.64f),0,true);
                seat(quiz.transform,new Vector3(x,.61f,.65f),0,"Quiz player seat");
            }
            Place(quiz.transform,"modern_ceiling_lamp_01","Quiz pendant",new Vector3(200,3.55f,0),new Vector3(.75f,1.05f,.75f),0,false);
        }
    }
}
