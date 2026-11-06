using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CampusCommons.LibraryCharacter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CampusCommons.LibraryCharacter.Editor
{
    public static class LoungePolish
    {
        const string Root="Assets/LibraryCharacter";
        const string Generated=Root+"/Generated";
        const string Books=Generated+"/ShelfBooks.prefab";
        static readonly Color[] Colors={
            new Color(.87f,.68f,.58f), new Color(.74f,.44f,.43f), new Color(.60f,.70f,.56f),
            new Color(.58f,.65f,.77f), new Color(.90f,.79f,.55f), new Color(.70f,.62f,.57f)
        };

        static Material[] Materials()
        {
            var paper=AssetDatabase.LoadAssetAtPath<Material>(Generated+"/BookPaper.mat");
            var original=AssetDatabase.LoadAssetAtPath<Material>(Generated+"/BookCover_0.mat");
            if(!paper || !original) throw new Exception("Original book materials are missing");
            var result=new Material[Colors.Length+1];
            for(int i=0;i<Colors.Length;i++)
            {
                string path=Generated+"/MixedBookCover_"+i+".mat";
                var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!mat){mat=new Material(original);AssetDatabase.CreateAsset(mat,path);}
                mat.color=Colors[i];mat.enableInstancing=true;EditorUtility.SetDirty(mat);result[i]=mat;
            }
            paper.enableInstancing=true;EditorUtility.SetDirty(paper);result[Colors.Length]=paper;return result;
        }
        struct BookPart {public Mesh mesh;public Matrix4x4 matrix;public Bounds bounds;public int cover;public int paper;}
        static BookPart[] ReadBooks()
        {
            string path=Root+"/Books/book_individual.fbx";
            var importer=AssetImporter.GetAtPath(path) as ModelImporter;
            if(!importer) throw new Exception("Individual scanned book model is missing");
            if(!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset);
            instance.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);instance.transform.localScale=Vector3.one;
            var result=new List<BookPart>();
            foreach(var r in instance.GetComponentsInChildren<MeshRenderer>())
            {
                var f=r.GetComponent<MeshFilter>();if(!f || !f.sharedMesh)continue;
                var matrix=f.transform.localToWorldMatrix;var points=f.sharedMesh.vertices;
                if(points.Length==0)continue;var bounds=new Bounds(matrix.MultiplyPoint3x4(points[0]),Vector3.zero);
                foreach(var v in points)bounds.Encapsulate(matrix.MultiplyPoint3x4(v));
                int cover=Array.FindIndex(r.sharedMaterials,m=>m && m.name.ToLowerInvariant().Contains("cover"));
                int paper=Array.FindIndex(r.sharedMaterials,m=>m && m.name.ToLowerInvariant().Contains("paper"));
                if(cover<0 || paper<0) throw new Exception("Book materials missing on "+r.name);
                result.Add(new BookPart{mesh=f.sharedMesh,matrix=matrix,bounds=bounds,cover=cover,paper=paper});
            }
            UnityEngine.Object.DestroyImmediate(instance);
            if(result.Count!=20)throw new Exception("Expected 20 individual books, got "+result.Count);
            return result.OrderBy(b=>b.bounds.center.x).ToArray();
        }
        static Mesh MakeRow(BookPart[] books,int variation)
        {
            var random=new System.Random(4129+variation*613);var transforms=new Matrix4x4[books.Length];
            var color=new int[books.Length];float cursor=0, height=books.Max(b=>b.bounds.size.y),depth=books.Max(b=>b.bounds.size.z);
            for(int i=0;i<books.Length;i++)
            {
                var b=books[i];float sx=.68f+(float)random.NextDouble()*.67f;
                float sy=.73f+(float)random.NextDouble()*.36f, sz=.90f+(float)random.NextDouble()*.20f;
                transforms[i]=Matrix4x4.Translate(new Vector3(cursor-b.bounds.min.x*sx,-b.bounds.min.y*sy,-b.bounds.center.z*sz))
                    *Matrix4x4.Scale(new Vector3(sx,sy,sz))*b.matrix;
                color[i]=(i+variation)%Colors.Length;
                cursor+=b.bounds.size.x*sx+.00045f;
            }
            for(int i=color.Length-1;i>0;i--){int j=random.Next(i+1);(color[i],color[j])=(color[j],color[i]);}
            var normalize=Matrix4x4.Scale(new Vector3(1f/cursor,1f/height,1f/depth));
            var submeshes=new List<Mesh>();
            for(int group=0;group<=Colors.Length;group++)
            {
                var items=new List<CombineInstance>();
                for(int i=0;i<books.Length;i++)
                {
                    if(group==Colors.Length)items.Add(new CombineInstance{mesh=books[i].mesh,subMeshIndex=books[i].paper,transform=normalize*transforms[i]});
                    else if(color[i]==group)items.Add(new CombineInstance{mesh=books[i].mesh,subMeshIndex=books[i].cover,transform=normalize*transforms[i]});
                }
                if(items.Count==0)throw new Exception("Book color variation is empty");
                var mesh=new Mesh();mesh.indexFormat=IndexFormat.UInt32;mesh.CombineMeshes(items.ToArray(),true,true,false);submeshes.Add(mesh);
            }
            var merged=new Mesh{name="Mixed books row "+(variation+1),indexFormat=IndexFormat.UInt32};
            merged.CombineMeshes(submeshes.Select(m=>new CombineInstance{mesh=m,transform=Matrix4x4.identity}).ToArray(),false,false,false);
            merged.RecalculateBounds();foreach(var m in submeshes)UnityEngine.Object.DestroyImmediate(m);
            string path=Generated+"/MixedBookRow_"+variation+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old){EditorUtility.CopySerialized(merged,old);UnityEngine.Object.DestroyImmediate(merged);return old;}
            AssetDatabase.CreateAsset(merged,path);return merged;
        }
        static void RebuildBookPrefab()
        {
            var materials=Materials();var originals=ReadBooks();var meshes=new Mesh[8];
            for(int i=0;i<meshes.Length;i++)meshes[i]=MakeRow(originals,i);
            var root=PrefabUtility.LoadPrefabContents(Books);int count=0;
            try
            {
                foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var match=Regex.Match(r.name,@"^(\d+)층 (\d+)구역 (\d+)단 (왼쪽|오른쪽)$");
                    if(!match.Success)match=Regex.Match(r.name,@"^Floor (\d+) Bay (\d+) Shelf (\d+) (Left|Right)$");
                    if(!match.Success)continue;
                    int floor=int.Parse(match.Groups[1].Value),bay=int.Parse(match.Groups[2].Value),shelf=int.Parse(match.Groups[3].Value);
                    bool right=match.Groups[4].Value=="오른쪽" || match.Groups[4].Value=="Right";int seed=floor*127+bay*31+shelf*11+(right?5:0);
                    r.name=$"Floor {floor} Bay {bay:00} Shelf {shelf} {(right?"Right":"Left")}";
                    r.GetComponent<MeshFilter>().sharedMesh=meshes[Math.Abs(seed)%meshes.Length];r.sharedMaterials=materials;
                    r.transform.localScale=new Vector3(1.04f,.49f+(Math.Abs(seed)%5)*.022f,.19f);
                    r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;count++;
                }
                if(count!=960)throw new Exception("Expected 960 original book rows; got "+count);
                root.name="Library Books";PrefabUtility.SaveAsPrefabAsset(root,Books);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            Debug.Log("POLISH_BOOKS_OK rows="+count+" colors="+Colors.Length+" variants="+meshes.Length);
        }
        static readonly Dictionary<string,string> Names=new Dictionary<string,string>{
            {"공간 구조","Building"},{"가구와 학교","Interiors and School"},{"좌석","Seats"},{"포탈","Portals"},{"플레이어 생성","Player Spawn"},
            {"조명","Lighting"},{"식물과 책 소품","Plants and Props"},{"나무와 추가 가구","Trees and Furniture"},{"책장 책","Library Books"},
            {"새 몬스터","Bird Monster"},{"학교 문","School Doors"},{"라운지 가구","Lounge Furniture"},{"공부 가구","Study Furniture"},
            {"마피아 가구","Mafia Furniture"},{"퀴즈 가구","Quiz Furniture"},{"탈출 학교","Escape School"},
            {"기존 귀환 포탈 (숨김)","Old Return Portal (Hidden)"},{"기존 귀환 지점 (숨김)","Old Return Point (Hidden)"},
            {"마피아 포탈","Mafia Portal"},{"퀴즈 포탈","Quiz Portal"},{"탈출 포탈","Escape Portal"},{"마피아 귀환 포탈","Mafia Return Portal"},
            {"퀴즈 귀환 포탈","Quiz Return Portal"},{"마피아 안내판","Mafia Sign"},{"퀴즈 안내판","Quiz Sign"},{"탈출 안내판","Escape Sign"},
            {"소파","Sofa"},{"소파 테이블","Coffee Table"},{"라운지 의자","Lounge Chair"},{"라운지 의자 좌석","Lounge Chair Seat"},{"소파 좌석","Sofa Seat"},
            {"공부 책상","Study Table"},{"공부 의자","Study Chair"},{"공부 의자 좌석","Study Chair Seat"},{"책상 조명","Desk Lamp"},
            {"마피아 의자","Mafia Chair"},{"마피아 좌석","Mafia Seat"},{"퀴즈 책상","Quiz Desk"},{"퀴즈 의자","Quiz Chair"},{"퀴즈 좌석","Quiz Seat"},
            {"책장","Bookcase"},{"방 책장","Room Bookcase"},{"방 책","Room Books"},{"퀴즈 책","Quiz Books"},{"책상 책","Desk Books"},
            {"공부 노트","Study Notebook"},{"퀴즈 노트","Quiz Notebook"},{"마피아 노트","Mafia Notes"},{"책상 화분","Desk Plant"},
            {"화분","Planter"},{"실내 식물","Indoor Plant"},{"방 식물","Room Plant"},{"책상 소품","Desk Props"},{"책장 소품","Bookcase Props"},
            {"갈색 소파","Brown Sofa"},{"방 소품","Room Props"},{"나무와 풀","Trees and Grass"},{"나무 화단","Tree Planter"},
            {"풀 화분","Grass Planter"},{"실내 나무","Indoor Tree"},{"흙","Soil"},{"화분 테두리","Planter Rim"},
            {"퀴즈 조명","Quiz Lamp"},{"마피아 조명","Mafia Lamp"},{"퀴즈 천장등","Quiz Ceiling Light"},{"마피아 천장등","Mafia Ceiling Light"},
            {"라운지 빛","Lounge Light"},{"햇빛","Skylight"},{"방 천장 빛","Room Ceiling Light"},{"모델","Model"},{"책상 모델","Table Model"},
            {"새 2D 이미지","Bird Sprite"},{"교실문","Classroom Door"},{"탈출문","Exit Door"},{"경첩","Hinge"},{"문판","Door Panel"},
            {"문 무늬","Door Texture"},{"왼쪽 문틀","Left Door Frame"},{"오른쪽 문틀","Right Door Frame"},{"위 문틀","Top Door Frame"},
            {"표시","Sign"},{"문제지","Question Sheet"},{"받침","Base"},{"지지대","Stand"},{"표지","Cover"},{"종이","Paper"},{"문장선","Text Line"},
            {"풀 A","Grass A"},{"풀 C","Grass C"},{"양치식물 A","Fern A"},{"양치식물 B","Fern B"}
        };
        internal static string English(string name)
        {
            if(Names.TryGetValue(name,out string full))return full;
            var suffix=Regex.Match(name,@"^(.*?)( \d+)$");
            if(suffix.Success && Names.TryGetValue(suffix.Groups[1].Value,out string part))return part+suffix.Groups[2].Value;
            var book=Regex.Match(name,@"^(\d+)층 (\d+)구역 (\d+)단 (왼쪽|오른쪽)$");
            if(book.Success)return $"Floor {book.Groups[1].Value} Bay {book.Groups[2].Value} Shelf {book.Groups[3].Value} {(book.Groups[4].Value=="왼쪽"?"Left":"Right")}";
            string result=name;
            foreach(var pair in new[]{("어두운 목재","Dark Wood"),("밝은 목재","Light Wood"),("호두나무","Walnut"),("탈출 포탈","Escape Portal"),("마피아 포탈","Mafia Portal"),("퀴즈 포탈","Quiz Portal"),
                ("로비","Lobby"),("마피아","Mafia"),("퀴즈","Quiz"),("학교","School"),("바닥","Floor"),("목재","Wood"),("금속","Metal"),("청동","Bronze"),
                ("러그","Rug"),("돌","Stone"),("조명","Lamp"),("빛 선","Light Strip"),("화면","Screen"),("밝은 벽","Light Wall"),("검정","Black"),("파랑","Blue"),("금색","Gold"),("구조","Structure"),("장식","Detail"),("소품","Prop")})
                result=result.Replace(pair.Item1,pair.Item2);
            return result;
        }
        static int Translate(Transform root)
        {
            int changed=0;
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                string name=English(t.name);if(name==t.name)continue;
                Undo.RecordObject(t.gameObject,"Use English object names");t.name=name;changed++;
            }
            return changed;
        }
        static void TranslatePrefabs()
        {
            foreach(string name in new[]{"SchoolDoors.prefab","Sheet.prefab","ScholarBird.prefab"})
            {
                string path=Generated+"/"+name;var root=PrefabUtility.LoadPrefabContents(path);
                try{Translate(root.transform);PrefabUtility.SaveAsPrefabAsset(root,path);}
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
        }
        static void RestoreLamps()
        {
            var meshes=AssetDatabase.LoadAllAssetsAtPath("Assets/GrandLibrary/Models/GrandLibrary.fbx").OfType<Mesh>().ToArray();
            // The old procedural glowing globes overlap the realistic fixtures.
            // Restore their ceiling rods, then attach the original realistic pendants to them.
            foreach(var entry in new[]{("LobbyBrass.asset","LIB_Library__brass__DETAIL")})
            {
                var original=meshes.SingleOrDefault(m=>m.name==entry.Item2);
                var filters=UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include).Where(f=>f.sharedMesh && AssetDatabase.GetAssetPath(f.sharedMesh)==Generated+"/"+entry.Item1).ToArray();
                if(!original || filters.Length!=1)throw new Exception("Original ceiling lamp geometry not found: "+entry.Item2);
                Undo.RecordObject(filters[0],"Restore ceiling lamps");filters[0].sharedMesh=original;
            }
            var lights=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t=>t.name=="Pendant lamp").ToArray();
            if(lights.Length!=4)throw new Exception("Expected four original pendant lamps, got "+lights.Length);
            foreach(var light in lights)
            {
                Undo.RecordObject(light.gameObject,"Restore ceiling lamps");Undo.RecordObject(light,"Attach ceiling lamp");
                var p=light.position;light.position=new Vector3(p.x+Mathf.Sign(p.x),p.y,p.z+1);light.gameObject.SetActive(true);
            }
            Debug.Log("POLISH_LAMPS_RESTORED count="+lights.Length);
        }
        static Bounds BoundsOf(Transform root)
        {
            var renderers=root.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
            if(renderers.Length==0)return new Bounds(root.position,Vector3.zero);
            var bounds=renderers[0].bounds;foreach(var r in renderers.Skip(1))bounds.Encapsulate(r.bounds);return bounds;
        }
        static void Decorate()
        {
            if(GameObject.Find("Lounge Decorations"))throw new Exception("Lounge decorations are already present");
            var parent=new GameObject("Lounge Decorations");Undo.RegisterCreatedObjectUndo(parent,"Add lounge decorations");
            var all=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            var source=all.FirstOrDefault(t=>t.name.StartsWith("실내 식물 "));
            if(!source)throw new Exception("Existing realistic indoor plant is missing");
            var positions=new[]{new Vector3(-8.9f,0,-13.8f),new Vector3(8.9f,0,-13.8f),new Vector3(-9.0f,0,13.8f),new Vector3(9.0f,0,13.8f)};
            for(int i=0;i<positions.Length;i++)
            {
                var copy=UnityEngine.Object.Instantiate(source.gameObject,parent.transform);copy.name="Decorative Plant "+(i+1).ToString("00");
                copy.transform.position=positions[i];copy.transform.rotation=Quaternion.Euler(0,65*i,0);copy.transform.localScale=Vector3.one*(i%2==0?.9f:1.08f);
            }
            var rug=AssetDatabase.LoadAssetAtPath<Material>("Assets/StudyImmersive/LivingMaterials/rug_weave.mat");if(!rug)throw new Exception("Woven rug material is missing");
            var tables=all.Where(t=>Regex.IsMatch(t.name,@"^공부 책상 \d+$")).OrderBy(t=>t.name).ToArray();
            if(tables.Length!=6)throw new Exception("Expected six study tables, got "+tables.Length);
            for(int i=0;i<tables.Length;i++)
            {
                var bounds=BoundsOf(tables[i]);var carpet=GameObject.CreatePrimitive(PrimitiveType.Cylinder);carpet.name="Study Rug "+(i+1).ToString("00");
                carpet.transform.SetParent(parent.transform,false);carpet.transform.position=new Vector3(bounds.center.x,.012f,bounds.center.z);
                carpet.transform.localScale=new Vector3(3.4f,.004f,2.55f);carpet.GetComponent<Renderer>().sharedMaterial=rug;
                UnityEngine.Object.DestroyImmediate(carpet.GetComponent<Collider>());
            }
            var succulent=all.FirstOrDefault(t=>Regex.IsMatch(t.name,@"^책상 화분 \d+$"));
            var coffee=all.Where(t=>Regex.IsMatch(t.name,@"^소파 테이블 \d+$")).OrderBy(t=>t.name).ToArray();
            if(!succulent || coffee.Length!=4)throw new Exception("Lounge table decorations are missing");
            for(int i=0;i<coffee.Length;i++)
            {
                var bounds=BoundsOf(coffee[i]);var copy=UnityEngine.Object.Instantiate(succulent.gameObject,parent.transform);
                copy.name="Coffee Table Plant "+(i+1).ToString("00");copy.transform.position=new Vector3(bounds.center.x,bounds.max.y+.005f,bounds.center.z);
                copy.transform.rotation=Quaternion.Euler(0,90*i,0);copy.transform.localScale=Vector3.one*.9f;
            }
            Debug.Log("POLISH_DECOR_OK plants="+positions.Length+" rugs="+tables.Length+" tableplants="+coffee.Length);
        }
        [MenuItem("Campus/Polish lounge and English names")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new Exception("Stop Play mode before polishing the lounge");
            var scene=SceneManager.GetActiveScene();
            if(!scene.IsValid() || !scene.isLoaded || !UnityEngine.Object.FindAnyObjectByType<Monster>() ||
                (scene.path!="Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity" && scene.path!=Root+"/Preview/LiveScenePolishInput.unity"))
                throw new Exception("Open the updated GrandLibrary_Living scene first");
            if(GameObject.Find("Lounge Decorations"))throw new Exception("The lounge update is already applied");
            if(!AssetDatabase.IsValidFolder(Root+"/Backups"))AssetDatabase.CreateFolder(Root,"Backups");
            string backup=AssetDatabase.GenerateUniqueAssetPath(Root+"/Backups/GrandLibrary_before_lounge_polish.unity");
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Could not save scene backup");
            RestoreLamps();Decorate();RebuildBookPrefab();TranslatePrefabs();
            int count=0;foreach(var root in scene.GetRootGameObjects())count+=Translate(root.transform);
            var remaining=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Where(t=>Regex.IsMatch(t.name,"[가-힣]")).Select(t=>t.name).Distinct().ToArray();
            foreach(var name in remaining)Debug.LogWarning("POLISH_UNTRANSLATED "+name);
            EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Could not save updated scene");
            AssetDatabase.SaveAssets();Debug.Log("LOUNGE_POLISH_SAVED renamed="+count+" untranslated="+remaining.Length+" backup="+backup);
        }
        public static void RunBatch()
        {
            EditorSceneManager.OpenScene(Root+"/Preview/LiveScenePolishInput.unity");Apply();
        }
    }
}
