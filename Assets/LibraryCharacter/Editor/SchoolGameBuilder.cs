using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CampusCommons.LibraryCharacter;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CampusCommons.LibraryCharacter.Editor
{
    public static class SchoolGameBuilder
    {
        const string Root="Assets/LibraryCharacter";
        const string Generated=Root+"/Generated";
        const string DoorsName="School Doors";
        static Material Material(string name,Color color,string shader="Standard")
        {
            string path=Generated+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,path);}
            m.color=color;if(m.HasProperty("_Glossiness"))m.SetFloat("_Glossiness",.15f);EditorUtility.SetDirty(m);return m;
        }
        static GameObject Cube(Transform parent,string name,Vector3 position,Vector3 scale,Material mat,bool collider=false)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(parent,false);
            obj.transform.localPosition=position;obj.transform.localScale=scale;obj.GetComponent<Renderer>().sharedMaterial=mat;
            if(!collider)UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());return obj;
        }
        static GameObject Cylinder(Transform parent,string name,Vector3 position,Vector3 scale,Material mat)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cylinder);obj.name=name;obj.transform.SetParent(parent,false);
            obj.transform.localPosition=position;obj.transform.localScale=scale;obj.GetComponent<Renderer>().sharedMaterial=mat;
            UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());return obj;
        }
        static void Text(Transform parent,string value,Vector3 position,Quaternion rotation,float size,Color color)
        {
            var obj=new GameObject("Sign");obj.transform.SetParent(parent,false);obj.transform.localPosition=position;obj.transform.localRotation=rotation;
            var t=obj.AddComponent<TextMesh>();t.text=value;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.fontSize=72;t.characterSize=size;t.color=color;
        }
        static GameObject BuildSheet(Material blue,Material white,Material stand)
        {
            var root=new GameObject("Question Sheet");root.AddComponent<Sheet>();
            var hit=root.AddComponent<BoxCollider>();hit.center=new Vector3(0,1.08f,0);hit.size=new Vector3(.72f,1.05f,.35f);hit.isTrigger=true;
            Cylinder(root.transform,"Base",new Vector3(0,.035f,0),new Vector3(.3f,.035f,.3f),stand);
            Cylinder(root.transform,"Stand",new Vector3(0,.4f,0),new Vector3(.035f,.36f,.035f),stand);
            Cube(root.transform,"Cover",new Vector3(0,1.05f,0),new Vector3(.58f,.7f,.065f),blue);
            foreach(float side in new[]{-1f,1f})
            {
                Cube(root.transform,"Paper",new Vector3(0,1.05f,side*.038f),new Vector3(.48f,.60f,.012f),white);
                var rot=side<0?Quaternion.identity:Quaternion.Euler(0,180,0);
                Text(root.transform,"?",new Vector3(0,1.1f,side*.047f),rot,.006f,new Color(.09f,.15f,.32f));
                for(int i=0;i<2;i++)Cube(root.transform,"Text Line",new Vector3(0,.87f-i*.065f,side*.046f),new Vector3(.28f,.014f,.006f),stand);
            }
            var saved=PrefabUtility.SaveAsPrefabAsset(root,Generated+"/Sheet.prefab");UnityEngine.Object.DestroyImmediate(root);return saved;
        }
        static Door CreateDoor(Transform parent,Bounds bounds,bool exit,Material blue,Material face,Material frame)
        {
            bool horizontal=bounds.size.x>bounds.size.z;float width=horizontal?bounds.size.x:bounds.size.z,height=bounds.size.y;
            Vector3 tangent=horizontal?Vector3.right:Vector3.forward;float yaw=horizontal?0:-90;
            var root=new GameObject(exit?"Exit Door":"Classroom Door");root.transform.SetParent(parent,false);
            root.transform.SetPositionAndRotation(bounds.center-tangent*(width*.5f)-Vector3.up*(height*.5f),Quaternion.Euler(0,yaw,0));
            var door=root.AddComponent<Door>();door.exitDoor=exit;var pivot=new GameObject("Hinge").transform;pivot.SetParent(root.transform,false);door.hinge=pivot;
            Cube(pivot,"Door Panel",new Vector3(width*.5f,height*.5f,0),new Vector3(width-.035f,height-.035f,.10f),blue,true);
            foreach(float side in new[]{-1f,1f})
            {
                var q=GameObject.CreatePrimitive(PrimitiveType.Quad);q.name="Door Texture";q.transform.SetParent(pivot,false);
                q.transform.localPosition=new Vector3(width*.5f,height*.5f,side*.053f);
                q.transform.localRotation=side<0?Quaternion.identity:Quaternion.Euler(0,180,0);
                q.transform.localScale=new Vector3(width-.045f,height-.045f,1);q.GetComponent<Renderer>().sharedMaterial=face;
                UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
            }
            Cube(root.transform,"Left Door Frame",new Vector3(-.025f,height*.5f,0),new Vector3(.05f,height,.16f),frame);
            Cube(root.transform,"Right Door Frame",new Vector3(width+.025f,height*.5f,0),new Vector3(.05f,height,.16f),frame);
            Cube(root.transform,"Top Door Frame",new Vector3(width*.5f,height+.025f,0),new Vector3(width+.1f,.05f,.16f),frame);
            if(exit)
            {
                Text(root.transform,"EXIT",new Vector3(width*.5f,height+.18f,-.01f),Quaternion.identity,.0045f,new Color(1,.86f,.3f));
                Text(root.transform,"EXIT",new Vector3(width*.5f,height+.18f,.01f),Quaternion.Euler(0,180,0),.0045f,new Color(1,.86f,.3f));
            }
            return door;
        }
        static GameObject BuildDoors(SchoolGrid map,Vector3 spawn,Transform school,Material blue,Material face,Material frame)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/School/DoorMarkers.fbx");if(!source)throw new Exception("Original school door markers are missing");
            var markers=(GameObject)PrefabUtility.InstantiatePrefab(source);markers.transform.SetPositionAndRotation(school.position,school.rotation);markers.transform.localScale=school.lossyScale;
            var rs=markers.GetComponentsInChildren<MeshRenderer>().OrderBy(r=>r.name).ToArray();if(rs.Length!=23)throw new Exception("Expected 23 original doorways; got "+rs.Length);
            int exit=-1;float best=float.NegativeInfinity;var path=new List<Vector3>();
            for(int i=0;i<rs.Length;i++)
            {
                Vector3 center=rs[i].bounds.center;
                if(!map.FindPath(spawn,center,path))continue;
                // Prefer a distant original doorway toward the school perimeter.
                float edge=Mathf.Max(Mathf.Abs(center.x-500),Mathf.Abs(center.z));
                float score=path.Count*.5f+edge*.35f;
                if(score>best){best=score;exit=i;}
            }
            if(exit<0)throw new Exception("Could not locate a reachable exit door");
            var root=new GameObject(DoorsName);
            for(int i=0;i<rs.Length;i++)
            {
                var door=CreateDoor(root.transform,rs[i].bounds,i==exit,blue,face,frame);
                if(i!=exit)door.gameObject.name="Classroom Door "+(i+1).ToString("00");
                else map.exitLocation=rs[i].bounds.center;
            }
            UnityEngine.Object.DestroyImmediate(markers);EditorUtility.SetDirty(map);
            var saved=PrefabUtility.SaveAsPrefabAsset(root,Generated+"/SchoolDoors.prefab");UnityEngine.Object.DestroyImmediate(root);
            Debug.Log("SCHOOL_DOORS_OK count="+rs.Length+" exit="+map.exitLocation.ToString("F2")+" distance="+best.ToString("F1"));return saved;
        }
        public static SchoolGrid BuildAssets()
        {
            Directory.CreateDirectory(Generated);AssetDatabase.Refresh();var flow=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();
            var school=GameObject.Find("User supplied Baldi school");if(!flow || !school)throw new Exception("Existing school or escape flow missing");
            var map=SchoolGridBuilder.Build(flow.schoolArrival);
            var blue=Material("SchoolDoorBlue",new Color(.04f,.07f,.48f));var frame=Material("SchoolDoorFrame",new Color(.28f,.30f,.31f));
            var face=Material("SchoolDoorFace",Color.white,"Unlit/Texture");
            string texturePath=Root+"/School/SchoolDoor.png";var importer=AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if(!importer)throw new Exception("Source school door texture missing");if(importer.filterMode!=FilterMode.Point){importer.filterMode=FilterMode.Point;importer.SaveAndReimport();}
            face.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);face.mainTextureScale=new Vector2(.80f,.88f);face.mainTextureOffset=new Vector2(.10f,0);
            var sheetBlue=Material("SheetBlue",new Color(.08f,.22f,.65f));var paper=Material("SheetPaper",new Color(.97f,.96f,.88f));var stand=Material("SheetStand",new Color(.26f,.29f,.32f));
            BuildSheet(sheetBlue,paper,stand);BuildDoors(map,flow.schoolArrival,school.transform,blue,face,frame);AssetDatabase.SaveAssets();return map;
        }
        public static void Apply(SchoolGrid map)
        {
            var scene=SceneManager.GetActiveScene();var flow=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();
            var monster=UnityEngine.Object.FindAnyObjectByType<Monster>();if(!scene.IsValid() || !flow || !monster || !map)throw new Exception("Game scene, monster, or navigation missing");
            var doorPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Generated+"/SchoolDoors.prefab");
            var sheetPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Generated+"/Sheet.prefab");
            if(!doorPrefab || !sheetPrefab)throw new Exception("Prepared doors or sheets are missing");
            if(GameObject.Find(DoorsName))throw new Exception("School doors already exist");
            var doors=(GameObject)PrefabUtility.InstantiatePrefab(doorPrefab,scene);doors.name=DoorsName;Undo.RegisterCreatedObjectUndo(doors,"Add school doors");
            var all=doors.GetComponentsInChildren<Door>();foreach(var door in all)door.game=flow;
            var exit=all.SingleOrDefault(d=>d.exitDoor);if(!exit)throw new Exception("Exactly one exit door is required");
            Undo.RecordObject(flow,"Set escape game");Undo.RecordObject(monster,"Set monster pathfinding");
            flow.schoolGrid=map;flow.notebookPositions=map.sheetLocations;flow.notebookPrefab=sheetPrefab;flow.monster=monster;flow.exitDoor=exit;monster.map=map;
            EditorUtility.SetDirty(flow);EditorUtility.SetDirty(monster);Debug.Log("SCHOOL_GAME_APPLIED doors="+all.Length+" sheets="+map.sheetLocations.Length);
        }
    }
}
