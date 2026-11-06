using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CampusCommons.GrandLibrary.Editor
{
    [Serializable] public class SchoolMaterialTable { public SchoolMaterialEntry[] entries; }
    [Serializable] public class SchoolMaterialEntry
    {
        public int index;
        public string sourceMaterial,texture;
        public float[] color;
        public bool solid;
    }
    public static class UserSchoolEnvironment
    {
        const string Root="Assets/GrandLibrary/UserSchool";
        public static readonly Vector3 Origin=new Vector3(500,0,0);
        public static Vector3 ReturnPortal { get; private set; }
        public static Vector3 Arrival { get; private set; }

        public static void Build(Transform parent,Material portalMaterial,Material brassMaterial)
        {
            Directory.CreateDirectory(Root+"/Materials"); AssetDatabase.Refresh();
            var text=AssetDatabase.LoadAssetAtPath<TextAsset>(Root+"/SchoolMaterials.json");
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/BaldiSchool.fbx");
            if (!text || !model) throw new Exception("The converted user school model is missing.");
            var table=JsonUtility.FromJson<SchoolMaterialTable>(text.text);
            var materials=new Dictionary<int,Material>();
            foreach(var item in table.entries)
            {
                string path=Root+"/Materials/School_"+item.index.ToString("D3")+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                bool textured=!string.IsNullOrEmpty(item.texture);
                Shader shader=Shader.Find(textured?"Unlit/Texture":"Standard");
                if (!material) { material=new Material(shader); AssetDatabase.CreateAsset(material,path); }
                material.shader=shader;
                if (textured)
                {
                    string texPath=Root+"/Textures/"+item.texture;
                    var importer=AssetImporter.GetAtPath(texPath) as TextureImporter;
                    if (importer && importer.filterMode!=FilterMode.Point)
                    { importer.filterMode=FilterMode.Point; importer.SaveAndReimport(); }
                    material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                    if (!material.mainTexture) throw new Exception("Missing school texture: "+item.texture);
                }
                else
                {
                    material.color=item.color!=null && item.color.Length>=3 ? new Color(item.color[0],item.color[1],item.color[2]) : Color.gray;
                    material.SetFloat("_Glossiness",.08f);
                }
                material.name="School_"+item.index.ToString("D3")+"_"+item.sourceMaterial;
                EditorUtility.SetDirty(material); materials.Add(item.index,material);
            }
            var school=(GameObject)PrefabUtility.InstantiatePrefab(model);
            school.name="User supplied Baldi school"; school.transform.SetParent(parent,false); school.transform.position=Origin;
            int count=0,solid=0;
            foreach(var renderer in school.GetComponentsInChildren<MeshRenderer>())
            {
                string[] parts=renderer.name.Split('_');
                if (parts.Length<3 || !int.TryParse(parts[1],out int index) || !materials.ContainsKey(index))
                    throw new Exception("Unexpected school renderer name: "+renderer.name);
                var slots=renderer.sharedMaterials;
                if (slots.Length==0) slots=new Material[1];
                for(int i=0;i<slots.Length;i++) slots[i]=materials[index];
                renderer.sharedMaterials=slots; count++;
                if (parts[2].StartsWith("SOLID"))
                {
                    var filter=renderer.GetComponent<MeshFilter>();
                    if (filter && filter.sharedMesh)
                    { var collider=renderer.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh=filter.sharedMesh; solid++; }
                }
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,StaticEditorFlags.BatchingStatic);
            }
            if (count<40 || solid<10) throw new Exception("The school mesh import is incomplete.");
            Physics.SyncTransforms();
            ReturnPortal=FindPortalLocation(); Arrival=ReturnPortal+Vector3.forward*1.9f+Vector3.up*.08f;
            CreateReturnPortal(parent,ReturnPortal,portalMaterial,brassMaterial);
            Debug.Log("USER_SCHOOL_OK renderers="+count+" colliders="+solid+" portal="+ReturnPortal+" arrival="+Arrival);
        }
        static Vector3 FindPortalLocation()
        {
            // Search the school itself for a carpeted, collision-free rectangle
            // close to a classroom; arrival sits outside the return trigger.
            Vector3 best=Vector3.zero; float bestScore=float.PositiveInfinity;
            for(float x=464;x<=536;x+=1.0f) for(float z=-49;z<=49;z+=1.0f)
            {
                if (!GroundAt(x,z,out float y,out string material)) continue;
                if (!material.Contains("Carpet") && !material.Contains("TileFloor") && !material.Contains("Ph_Floor")) continue;
                float score=(x-488)*(x-488)+(z-33)*(z-33)+(material.Contains("Carpet")?0:140);
                if (score>=bestScore) continue;
                bool clear=true;
                foreach(float dx in new[] {-.65f,0,.65f})
                {
                    foreach(float dz in new[] {0f,.65f,1.3f,1.9f,2.45f})
                    {
                        float px=x+dx,pz=z+dz;
                        if (!GroundAt(px,pz,out float gy,out _) || Mathf.Abs(gy-y)>.15f ||
                            Physics.CheckCapsule(new Vector3(px,y+.36f,pz),new Vector3(px,y+1.48f,pz),.28f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                        { clear=false; break; }
                    }
                    if (!clear) break;
                }
                if (clear) { best=new Vector3(x,y,z); bestScore=score; }
            }
            if (float.IsPositiveInfinity(bestScore)) throw new Exception("No safe classroom area was found for the school portal.");
            return best;
        }
        static bool GroundAt(float x,float z,out float floor,out string material)
        {
            floor=0; material="";
            if (!Physics.Raycast(new Vector3(x,1.8f,z),Vector3.down,out RaycastHit hit,2.8f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)) return false;
            if (Mathf.Abs(hit.point.y)>.3f) return false;
            var renderer=hit.collider.GetComponent<Renderer>();
            if (!renderer || !renderer.sharedMaterial) return false;
            floor=hit.point.y; material=renderer.sharedMaterial.name;
            return true;
        }
        static void CreateReturnPortal(Transform parent,Vector3 p,Material surface,Material brass)
        {
            var gate=new GameObject("School return portal visual"); gate.transform.SetParent(parent,false); gate.transform.position=p;
            var glow=GameObject.CreatePrimitive(PrimitiveType.Sphere); glow.name="Portal surface"; glow.transform.SetParent(gate.transform,false);
            glow.transform.localPosition=new Vector3(0,1.12f,0); glow.transform.localScale=new Vector3(1.34f,2.02f,.04f);
            UnityEngine.Object.DestroyImmediate(glow.GetComponent<Collider>()); glow.GetComponent<Renderer>().sharedMaterial=surface;
            for(int i=0;i<32;i++)
            {
                float a=i*Mathf.PI*2/32,b=(i+1)*Mathf.PI*2/32;
                var from=new Vector3(Mathf.Sin(a)*.72f,1.12f+Mathf.Cos(a)*1.05f,0);
                var to=new Vector3(Mathf.Sin(b)*.72f,1.12f+Mathf.Cos(b)*1.05f,0);
                var segment=GameObject.CreatePrimitive(PrimitiveType.Cylinder); segment.name="Brass portal frame";
                segment.transform.SetParent(gate.transform,false); segment.transform.localPosition=(from+to)*.5f;
                segment.transform.localRotation=Quaternion.FromToRotation(Vector3.up,to-from);
                segment.transform.localScale=new Vector3(.075f,(to-from).magnitude*.5f,.075f);
                UnityEngine.Object.DestroyImmediate(segment.GetComponent<Collider>()); segment.GetComponent<Renderer>().sharedMaterial=brass;
            }
            var label=new GameObject("LOUNGE sign"); label.transform.SetParent(gate.transform,false);
            label.transform.localPosition=new Vector3(0,1.92f,.06f); label.transform.localRotation=Quaternion.Euler(0,180,0);
            var text=label.AddComponent<TextMesh>(); text.text="LOUNGE"; text.anchor=TextAnchor.MiddleCenter;
            text.alignment=TextAlignment.Center; text.fontSize=64; text.characterSize=.035f; text.color=Color.white;
            var font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.font=font; label.GetComponent<MeshRenderer>().sharedMaterial=font.material;
        }
    }
}
