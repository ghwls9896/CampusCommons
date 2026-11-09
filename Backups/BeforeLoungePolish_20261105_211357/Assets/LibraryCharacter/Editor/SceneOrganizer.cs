using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CampusCommons.LibraryCharacter.Editor
{
    public static class SceneOrganizer
    {
        const string Generated="Assets/LibraryCharacter/Generated/";
        static readonly Vector2[] OldHangingLights={new Vector2(-11,-5),new Vector2(11,-5),new Vector2(-11,8),new Vector2(11,8)};
        static bool FloatingGlobe(Vector3 p)=>p.y>9;
        static bool HangingRod(Vector3 p)
        {
            if(p.y<10.9f || p.y>14.2f)return false;
            foreach(var c in OldHangingLights)if(Mathf.Abs(p.x-c.x)<.13f && Mathf.Abs(p.z-c.y)<.13f)return true;
            return false;
        }
        static void PrepareTrimmed(string objectName,string assetName,Func<Vector3,bool> remove)
        {
            var source=GameObject.Find(objectName);if(!source)throw new Exception("Lamp mesh not found: "+objectName);
            var filter=source.GetComponent<MeshFilter>();if(!filter || !filter.sharedMesh)throw new Exception("Lamp geometry missing");
            var copy=UnityEngine.Object.Instantiate(filter.sharedMesh);var vertices=copy.vertices;int removed=0;
            for(int part=0;part<copy.subMeshCount;part++)
            {
                var triangles=copy.GetTriangles(part);var keep=new List<int>(triangles.Length);
                for(int i=0;i<triangles.Length;i+=3)
                {
                    var center=(source.transform.TransformPoint(vertices[triangles[i]])+source.transform.TransformPoint(vertices[triangles[i+1]])+source.transform.TransformPoint(vertices[triangles[i+2]]))/3;
                    if(remove(center)){removed++;continue;}keep.Add(triangles[i]);keep.Add(triangles[i+1]);keep.Add(triangles[i+2]);
                }
                copy.SetTriangles(keep,part,true);
            }
            if(removed==0)throw new Exception("No floating lamp triangles found in "+objectName);
            copy.RecalculateBounds();copy.name=assetName;
            string path=Generated+assetName+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old){EditorUtility.CopySerialized(copy,old);UnityEngine.Object.DestroyImmediate(copy);}else AssetDatabase.CreateAsset(copy,path);
            Debug.Log("LOBBY_LAMP_TRIM_OK "+objectName+" triangles="+removed);
        }
        public static void Prepare()
        {
            PrepareTrimmed("LIB_Library__lamp__DETAIL","LobbyLamps",FloatingGlobe);
            PrepareTrimmed("LIB_Library__brass__DETAIL","LobbyBrass",HangingRod);AssetDatabase.SaveAssets();
        }
        static void UseMesh(string objectName,string assetName)
        {
            var obj=GameObject.Find(objectName);var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Generated+assetName+".asset");
            if(!obj || !mesh)throw new Exception("Prepared lamp cleanup missing: "+objectName);
            var filter=obj.GetComponent<MeshFilter>();Undo.RecordObject(filter,"Remove floating lamps");filter.sharedMesh=mesh;
        }
        static readonly Dictionary<string,string> Names=new Dictionary<string,string>{
            {"Grand Library and Game Rooms - 3D model","공간 구조"},{"Realistic interiors and user school","가구와 학교"},
            {"SEATS - E to sit","좌석"},{"PORTALS - walk through to travel","포탈"},{"Player startup","플레이어 생성"},
            {"Warm architectural lighting","조명"},{"IMMERSIVE - realistic plants and study props","식물과 책 소품"},
            {"LIVING LIBRARY - trees, owned package foliage and furniture","나무와 추가 가구"},
            {"Detailed 3D library books","책장 책"},{"Scholar bird - camera facing 2D","새 몬스터"},
            {"Realistic lounge furniture","라운지 가구"},{"Study tables and chairs","공부 가구"},{"Mafia furniture","마피아 가구"},
            {"Two player quiz furniture","퀴즈 가구"},{"User supplied Baldi school","탈출 학교"},
            {"School return portal visual","기존 귀환 포탈 (숨김)"},{"ESCAPE ROOM TO LOUNGE","기존 귀환 지점 (숨김)"},
            {"MAFIA","마피아 포탈"},{"QUIZ 1 VS 1","퀴즈 포탈"},{"ESCAPE ROOM","탈출 포탈"},
            {"MAFIA TO LOUNGE","마피아 귀환 포탈"},{"QUIZ TO LOUNGE","퀴즈 귀환 포탈"},
            {"MAFIA sign","마피아 안내판"},{"QUIZ  1 VS 1 sign","퀴즈 안내판"},{"ESCAPE ROOM sign","탈출 안내판"},
            {"Leather sofa","소파"},{"Low round table","소파 테이블"},{"Lounge armchair","라운지 의자"},
            {"Lounge armchair seat","라운지 의자 좌석"},{"Sofa seat","소파 좌석"},
            {"Study table","공부 책상"},{"Study chair","공부 의자"},{"Study chair seat","공부 의자 좌석"},
            {"Study desk lamp","책상 조명"},{"Mafia chair","마피아 의자"},{"Mafia player seat","마피아 좌석"},
            {"Quiz desk","퀴즈 책상"},{"Quiz chair","퀴즈 의자"},{"Quiz player seat","퀴즈 좌석"},
            {"Detailed bookcase","책장"},{"Room detailed bookcase","방 책장"},{"Room book volumes","방 책"},
            {"Quiz books","퀴즈 책"},{"Detailed book set","책상 책"},{"Study notebook","공부 노트"},
            {"Quiz notebook","퀴즈 노트"},{"Mafia notes","마피아 노트"},{"Desk succulent","책상 화분"},
            {"Detailed potted plants","화분"},{"Indoor plant","실내 식물"},{"Room plant","방 식물"},
            {"Books and desk plants","책상 소품"},{"Bookcase volumes","책장 소품"},{"Detailed brown sofa","갈색 소파"},
            {"Room shelves, books and lamps","방 소품"},{"Real trees and owned Unity grass and ferns","나무와 풀"},
            {"Indoor tree planter","나무 화단"},{"Fern and grass planter","풀 화분"},{"Detailed indoor tree","실내 나무"},
            {"Ceramic planter","화분"},{"Soil","흙"},{"Brass rim","화분 테두리"},
            {"Quiz reading lamp","퀴즈 조명"},{"Mafia reading lamp","마피아 조명"},{"Quiz pendant","퀴즈 천장등"},
            {"Mafia pendant","마피아 천장등"},{"Lounge warm pendant","라운지 빛"},{"Skylight","햇빛"},
            {"Room ceiling light","방 천장 빛"},{"Scaled geometry","모델"},{"Corrected table proportions","책상 모델"}
        };
        static string GeometryName(string name)
        {
            if(name.StartsWith("LIB_"))
            {
                string[] p=name.Split(new[]{"__"},StringSplitOptions.None);if(p.Length<3)return name;
                string area=p[0].Contains("Library")?"로비":p[0].Contains("Mafia")?"마피아":"퀴즈";
                var materials=new Dictionary<string,string>{{"floor","바닥"},{"wood","목재"},{"wood_dark","어두운 목재"},{"wood_light","밝은 목재"},{"brass","금속"},{"bronze","청동"},{"walnut","호두나무"},{"rug","러그"},{"stone","돌"},{"lamp","조명"},{"lightline","빛 선"},{"ink","화면"},{"ivory","밝은 벽"},{"black","검정"},{"blue","파랑"},{"gold","금색"},{"portal_escape","탈출 포탈"},{"portal_mafia","마피아 포탈"},{"portal_quiz","퀴즈 포탈"}};
                string type=materials.TryGetValue(p[1],out string label)?label:p[1];return area+" "+type+(p[2]=="SOLID"?" 구조":" 장식");
            }
            if(name.StartsWith("SCHOOL_"))
            {
                string[] p=name.Split('_');return "학교 "+(p.Length>2 && p[2].StartsWith("SOLID")?"구조 ":"소품 ")+(p.Length>1?p[1]:"");
            }
            if(name.StartsWith("Owned Grass_"))return "풀 "+name.Substring("Owned Grass_".Length);
            if(name.StartsWith("Owned Fern_"))return "양치식물 "+name.Substring("Owned Fern_".Length);
            return name;
        }
        public static void Apply()
        {
            UseMesh("LIB_Library__lamp__DETAIL","LobbyLamps");UseMesh("LIB_Library__brass__DETAIL","LobbyBrass");
            var all=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            foreach(var t in all)if(t && t.name=="Pendant lamp"){Undo.RecordObject(t.gameObject,"Remove floating lamp");t.gameObject.SetActive(false);}
            // Old placeholder book and plant meshes are disabled in this scene;
            // remove those redundant scene instances after the new models are in place.
            var unused=all.Where(t=>t && t.name.StartsWith("LIB_") && !t.gameObject.activeSelf).Select(t=>t.gameObject).ToArray();
            foreach(var obj in unused)if(obj)Undo.DestroyObjectImmediate(obj);
            all=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            int renamed=0;
            foreach(var t in all)
            {
                if(!t)continue;string before=t.name;
                string after=Names.TryGetValue(before,out string replacement)?replacement:GeometryName(before);
                if(after!=before){Undo.RecordObject(t.gameObject,"Organize scene names");t.name=after;renamed++;}
            }
            var organizedNames=new HashSet<string>(Names.Values);organizedNames.Add("문 무늬");
            foreach(var parent in all)
            {
                if(!parent)continue;
                var children=new List<Transform>();foreach(Transform child in parent)children.Add(child);
                foreach(var group in children.Where(c=>organizedNames.Contains(c.name)).GroupBy(c=>c.name).Where(g=>g.Count()>1))
                {
                    int number=0;foreach(var child in group){Undo.RecordObject(child.gameObject,"Number scene objects");child.name=group.Key+" "+(++number).ToString("00");renamed++;}
                }
            }
            Debug.Log("SCENE_ORGANIZED renamed="+renamed+" removed_old_placeholders="+unused.Length);
        }
    }
}
