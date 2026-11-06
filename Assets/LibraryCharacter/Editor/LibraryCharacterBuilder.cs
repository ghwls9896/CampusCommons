using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CampusCommons.GrandLibrary;
using CampusCommons.LibraryCharacter;
using CampusCommons.StudyImmersive;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CampusCommons.LibraryCharacter.Editor
{
    public static class LibraryCharacterBuilder
    {
        const string Root="Assets/LibraryCharacter";
        const string Generated=Root+"/Generated";
        const string OriginalScene="Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity";
        public const string PreviewScene=Root+"/Preview/GrandLibrary_DetailedPreview.unity";
        const string ShelfName="Detailed 3D library books";
        const string BirdName="Scholar bird - camera facing 2D";

        static Texture2D Texture(string path,bool normal=false)
        {
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(!importer)throw new Exception("Missing texture "+path);
            bool changed=false;
            if(normal && importer.textureType!=TextureImporterType.NormalMap){importer.textureType=TextureImporterType.NormalMap;changed=true;}
            if(importer.maxTextureSize!=4096){importer.maxTextureSize=4096;changed=true;}
            if(importer.textureCompression!=TextureImporterCompression.CompressedHQ){importer.textureCompression=TextureImporterCompression.CompressedHQ;changed=true;}
            if(importer.anisoLevel!=4){importer.anisoLevel=4;changed=true;}
            if(changed)importer.SaveAndReimport();
            var t=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(!t)throw new Exception("Texture import failed: "+path);return t;
        }
        static Material BookMaterial(string name,string colorPath,string normalPath,Color tint)
        {
            var diffuse=Texture(colorPath);var normal=Texture(normalPath,true);string path=Generated+"/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(mat,path);}
            mat.color=tint;mat.mainTexture=diffuse;mat.SetTexture("_BumpMap",normal);mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_BumpScale",.55f);mat.SetFloat("_Glossiness",.18f);mat.enableInstancing=true;EditorUtility.SetDirty(mat);return mat;
        }
        static Sprite PrepareSprite()
        {
            string path=Root+"/Sprites/ScholarBird_Billboard.png";
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(!importer)throw new Exception("Bird sprite image is missing");
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.spritePixelsPerUnit=100;
            // Image is 1881 px tall, with the visible feet at y=1801 from the top.
            var spriteSettings=new TextureImporterSettings();importer.ReadTextureSettings(spriteSettings);
            spriteSettings.spriteAlignment=(int)SpriteAlignment.Custom;spriteSettings.spritePivot=new Vector2(.5f,80f/1881f);
            importer.SetTextureSettings(spriteSettings);
            importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.filterMode=FilterMode.Bilinear;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.SaveAndReimport();
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(!sprite)throw new Exception("Bird sprite failed to import");return sprite;
        }
        static Mesh BuildCanonicalBookMesh(out bool[] paperSlots)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Books/book_set_optimized.fbx");
            if(!prefab)throw new Exception("Optimized book model missing");
            var temp=(GameObject)PrefabUtility.InstantiatePrefab(prefab);temp.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);temp.transform.localScale=Vector3.one;
            var renderers=temp.GetComponentsInChildren<MeshRenderer>();
            if(renderers.Length!=1)throw new Exception("Expected one optimized book mesh; got "+renderers.Length);
            var r=renderers[0];var filter=r.GetComponent<MeshFilter>();if(!filter || !filter.sharedMesh)throw new Exception("Book mesh filter missing");
            var source=filter.sharedMesh;var mats=r.sharedMaterials;
            if(source.subMeshCount!=mats.Length || mats.Length<2)throw new Exception("Book materials and submeshes do not match");
            paperSlots=mats.Select(m=>m && m.name.ToLowerInvariant().Contains("paper")).ToArray();
            if(!paperSlots.Any(x=>x) || paperSlots.All(x=>x))throw new Exception("Book cover/paper materials not identified");
            // The scanned set is Z-up in the FBX. Rotate its actual geometry,
            // rather than stretching its depth into height, before normalizing.
            var aligned=Matrix4x4.Rotate(Quaternion.Euler(-90,0,0))*filter.transform.localToWorldMatrix;
            var points=source.vertices;var first=aligned.MultiplyPoint3x4(points[0]);var b=new Bounds(first,Vector3.zero);
            for(int i=1;i<points.Length;i++)b.Encapsulate(aligned.MultiplyPoint3x4(points[i]));
            var offset=Matrix4x4.Translate(new Vector3(-b.center.x,-b.min.y,-b.center.z));
            var parts=new CombineInstance[source.subMeshCount];
            for(int i=0;i<parts.Length;i++)parts[i]=new CombineInstance{mesh=source,subMeshIndex=i,transform=offset*aligned};
            var mesh=new Mesh{name="Detailed book row - shared instanced geometry",indexFormat=IndexFormat.UInt32};
            mesh.CombineMeshes(parts,false,true,false);mesh.RecalculateBounds();
            UnityEngine.Object.DestroyImmediate(temp);
            string path=Generated+"/DetailedBookRow.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing){EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);mesh=existing;}
            else AssetDatabase.CreateAsset(mesh,path);
            return mesh;
        }
        public static void BuildAssets()
        {
            Directory.CreateDirectory(Generated);Directory.CreateDirectory(Root+"/Preview");AssetDatabase.Refresh();
            var coverPath=Root+"/Books/Textures/cover_diff.jpg";var coverNormal=Root+"/Books/Textures/cover_nor_gl.jpg";
            var palette=new[]{Color.white,new Color(1,.76f,.70f),new Color(.72f,.86f,.72f),new Color(.72f,.79f,.96f),new Color(.94f,.87f,.68f)};
            var covers=new Material[palette.Length];for(int i=0;i<covers.Length;i++)covers[i]=BookMaterial("BookCover_"+i,coverPath,coverNormal,palette[i]);
            var paper=BookMaterial("BookPaper",Root+"/Books/Textures/paper_diff.jpg",Root+"/Books/Textures/paper_nor_gl.jpg",Color.white);
            var mesh=BuildCanonicalBookMesh(out bool[] isPaper);var size=mesh.bounds.size;
            if(size.x<.001f || size.y<.001f || size.z<.001f)throw new Exception("Invalid book row size");
            var parent=new GameObject(ShelfName);int count=0;
            for(int level=0;level<3;level++)for(int bay=0;bay<40;bay++)for(int shelf=0;shelf<4;shelf++)for(int pair=0;pair<2;pair++)
            {
                float a=bay*Mathf.PI*2/40, offset=pair==0?-.60f:.60f;
                float x=Mathf.Sin(a)*18.76f+Mathf.Cos(a)*offset;
                float z=Mathf.Cos(a)*18.76f-Mathf.Sin(a)*offset;
                float h=.44f+((bay*7+shelf*3+pair*5+level)%5)*.035f;
                var row=new GameObject($"Floor {level+1} Bay {bay+1:00} Shelf {shelf+1} {(pair==0?"Left":"Right")}");
                row.transform.SetParent(parent.transform,false);row.transform.position=new Vector3(x,level*4.8f+.205f+shelf*.76f,z);
                // The scanned set's spines point along local +Z, toward the
                // atrium after the 180-degree turn; otherwise white page edges face out.
                row.transform.rotation=Quaternion.Euler(0,a*Mathf.Rad2Deg+180f,0);
                row.transform.localScale=new Vector3(1.04f/size.x,h/size.y,.19f/size.z);
                row.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=row.AddComponent<MeshRenderer>();
                var slots=new Material[isPaper.Length];for(int i=0;i<slots.Length;i++)slots[i]=isPaper[i]?paper:covers[(bay+shelf*2+pair+level)%covers.Length];
                renderer.sharedMaterials=slots;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;count++;
            }
            PrefabUtility.SaveAsPrefabAsset(parent,Generated+"/ShelfBooks.prefab");UnityEngine.Object.DestroyImmediate(parent);
            var sprite=PrepareSprite();var bird=new GameObject(BirdName);bird.layer=2;
            var body=bird.AddComponent<CharacterController>();body.height=1.8f;body.center=new Vector3(0,.9f,0);body.radius=.28f;body.stepOffset=.25f;
            var image=new GameObject("Bird Sprite");image.transform.SetParent(bird.transform,false);image.layer=2;
            image.AddComponent<SpriteRenderer>().sprite=sprite;image.AddComponent<FaceCamera>();
            float scale=2.0f/(1621f/100f);image.transform.localScale=Vector3.one*scale;
            bird.AddComponent<Monster>().picture=image.transform;
            PrefabUtility.SaveAsPrefabAsset(bird,Generated+"/ScholarBird.prefab");UnityEngine.Object.DestroyImmediate(bird);
            AssetDatabase.SaveAssets();Debug.Log("LIBRARY_CHARACTER_ASSETS_OK rows="+count+" mesh_vertices="+mesh.vertexCount);
        }
        static Vector3 FindBirdPosition(EscapeGame flow)
        {
            Vector3 start=flow.schoolArrival;Quaternion q=Quaternion.Euler(0,flow.schoolYaw,0);Physics.SyncTransforms();
            foreach(float distance in new[]{3.5f,3f,4f,2.5f,4.5f})foreach(float side in new[]{0f,-.65f,.65f,-1.1f,1.1f})
            {
                Vector3 p=start+q*new Vector3(side,0,distance);
                if(!Physics.Raycast(p+Vector3.up*2.5f,Vector3.down,out RaycastHit floor,4,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))continue;
                p.y=floor.point.y;
                if(Mathf.Abs(p.y-start.y)>.4f || Physics.CheckCapsule(p+Vector3.up*.35f,p+Vector3.up*1.5f,.26f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))continue;
                Vector3 eye=start+Vector3.up*1.55f;Vector3 look=p+Vector3.up*1.2f;
                if(Physics.Linecast(eye,look,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))continue;
                return p;
            }
            throw new Exception("Could not find a clear position for the bird near the school entrance");
        }
        static void ApplyToCurrentScene()
        {
            var scene=SceneManager.GetActiveScene();if(!scene.IsValid() || !scene.isLoaded)throw new Exception("No scene is open");
            if(GameObject.Find(ShelfName) || GameObject.Find(BirdName))throw new Exception("The new books or character are already in this scene");
            var flow=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();if(!flow)throw new Exception("This scene has no escape room entrance");
            var all=UnityEngine.Object.FindObjectsByType<MeshRenderer>();
            var old=all.Where(r=>r.name.StartsWith("LIB_Library__book") || r.name=="LIB_Library__gold__DETAIL").ToArray();
            if(old.Length<5)throw new Exception("Original library books could not be identified");
            var bookPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Generated+"/ShelfBooks.prefab");
            var birdPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Generated+"/ScholarBird.prefab");
            if(!bookPrefab || !birdPrefab)throw new Exception("Prepared book or bird prefab is missing");
            Vector3 birdPosition=FindBirdPosition(flow);
            foreach(var r in old){Undo.RecordObject(r.gameObject,"Replace old books");r.gameObject.SetActive(false);}
            var books=(GameObject)PrefabUtility.InstantiatePrefab(bookPrefab,scene);books.name=ShelfName;Undo.RegisterCreatedObjectUndo(books,"Add detailed books");
            var bird=(GameObject)PrefabUtility.InstantiatePrefab(birdPrefab,scene);bird.name=BirdName;bird.transform.position=birdPosition;
            var helper=new GameObject("Temporary bird view");helper.transform.position=flow.schoolArrival+Vector3.up*1.62f;
            bird.GetComponentInChildren<FaceCamera>().Face(helper.transform);UnityEngine.Object.DestroyImmediate(helper);
            Undo.RegisterCreatedObjectUndo(bird,"Add scholar bird");EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("LIBRARY_CHARACTER_APPLIED old_book_renderers="+old.Length+" bird="+birdPosition);
        }
        [MenuItem("Campus/Apply school update")]
        public static void ApplyInOpenEditor()
        {
            if(EditorApplication.isPlaying){EditorUtility.DisplayDialog("Apply character and books","Stop Play mode before applying the changes.","OK");return;}
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=OriginalScene){EditorUtility.DisplayDialog("Apply character and books","Open GrandLibrary_Living first, then try again.","OK");return;}
            if(GameObject.Find("Library Books") || GameObject.Find("책장 책") || UnityEngine.Object.FindAnyObjectByType<Monster>())
            {EditorUtility.DisplayDialog("School update","This scene already contains the updated books and character.","OK");return;}
            if(!AssetDatabase.IsValidFolder(Root+"/Backups"))AssetDatabase.CreateFolder(Root,"Backups");
            string backup=AssetDatabase.GenerateUniqueAssetPath(Root+"/Backups/GrandLibrary_before_books_and_bird.unity");
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Could not back up the open scene");
            ApplyToCurrentScene();
            SchoolGameBuilder.Apply(AssetDatabase.LoadAssetAtPath<SchoolGrid>(Generated+"/SchoolGrid.asset"));
            SceneOrganizer.Apply();
            if(!EditorSceneManager.SaveScene(scene))throw new Exception("Could not save the improved active scene");
            AssetDatabase.SaveAssets();
            Debug.Log("LIBRARY_CHARACTER_ACTIVE_SCENE_SAVED backup="+backup);
        }
        [MenuItem("Campus/Testing/Visit first sheet in Play mode")]
        public static void VisitFirstSheet()
        {
            if(!EditorApplication.isPlaying)return;
            var game=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();var player=UnityEngine.Object.FindAnyObjectByType<Player>();
            var sheet=UnityEngine.Object.FindAnyObjectByType<Sheet>();
            if(!game || !game.IsRunning || !player || !sheet || !game.schoolGrid)return;
            var path=new List<Vector3>();if(!game.schoolGrid.FindPath(sheet.transform.position,game.schoolArrival,path) || path.Count<2)return;
            var body=player.GetComponent<CharacterController>();if(!body)return;
            body.enabled=false;player.transform.position=path[Mathf.Min(2,path.Count-1)]+Vector3.up*.08f;body.enabled=true;Physics.SyncTransforms();
            game.AimAt(sheet.GetComponent<Collider>().bounds.center);
        }
        public static void BuildPreview()
        {
            BuildAssets();EditorSceneManager.OpenScene(OriginalScene,OpenSceneMode.Single);
            var map=SchoolGameBuilder.BuildAssets();SceneOrganizer.Prepare();ApplyToCurrentScene();SchoolGameBuilder.Apply(map);SceneOrganizer.Apply();
            if(!EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),PreviewScene))throw new Exception("Could not save preview scene");
            Render("books-close.png",new Vector3(-.8f,1.65f,16.2f),new Vector3(0,1.45f,18.76f),56);
            Render("books-lounge.png",new Vector3(-3,2.7f,-10),new Vector3(4,3.5f,1),72);
            var flow=UnityEngine.Object.FindAnyObjectByType<EscapeGame>();var bird=UnityEngine.Object.FindAnyObjectByType<Monster>().gameObject;
            Vector3 eye=flow.schoolArrival+Vector3.up*1.62f;Render("bird-school.png",eye,bird.transform.position+Vector3.up*1.0f,68);
            Render("school-door.png",new Vector3(485.3f,1.6f,33.1f),new Vector3(483.75f,1.25f,33.125f),65);
            Debug.Log("LIBRARY_CHARACTER_PREVIEW_OK");
        }
        static void Render(string name,Vector3 pos,Vector3 look,float fov)
        {
            Directory.CreateDirectory("Design/CharacterAndBooks");var go=new GameObject("Preview camera");var c=go.AddComponent<Camera>();
            c.transform.SetPositionAndRotation(pos,Quaternion.LookRotation(look-pos));c.fieldOfView=fov;c.nearClipPlane=.08f;c.farClipPlane=100;c.clearFlags=CameraClearFlags.Skybox;
            var rt=new RenderTexture(1440,900,24){antiAliasing=4};c.targetTexture=rt;c.Render();RenderTexture.active=rt;
            var image=new Texture2D(1440,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();
            File.WriteAllBytes("Design/CharacterAndBooks/"+name,image.EncodeToPNG());c.targetTexture=null;RenderTexture.active=null;
            UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);
        }
        public static void InspectBook()
        {
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Generated+"/DetailedBookRow.asset");
            var cover=AssetDatabase.LoadAssetAtPath<Material>(Generated+"/BookCover_0.mat");
            var paper=AssetDatabase.LoadAssetAtPath<Material>(Generated+"/BookPaper.mat");
            Debug.Log("BOOK_INSPECT_BOUNDS center="+mesh.bounds.center.ToString("F7")+" size="+mesh.bounds.size.ToString("F7")+" submeshes="+mesh.subMeshCount);
            var vertices=mesh.vertices;
            for(int s=0;s<mesh.subMeshCount;s++)
            {
                var tris=mesh.GetTriangles(s);float sumNeg=0,sumPos=0,areaNeg=0,areaPos=0;
                for(int i=0;i<tris.Length;i+=3)
                {
                    var a=vertices[tris[i]];var b=vertices[tris[i+1]];var c=vertices[tris[i+2]];
                    var normal=Vector3.Cross(b-a,c-a);float area=normal.magnitude*.5f;
                    if(normal.sqrMagnitude<1e-30f)continue;normal.Normalize();
                    if(Mathf.Abs(normal.z)>.75f){if((a.z+b.z+c.z)<0){areaNeg+=area;sumNeg++;}else{areaPos+=area;sumPos++;}}
                }
                Debug.Log("BOOK_INSPECT_SUBMESH "+s+" -Z area="+areaNeg+" faces="+sumNeg+" +Z area="+areaPos+" faces="+sumPos);
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.white;
            var row=new GameObject("Isolated book row");row.AddComponent<MeshFilter>().sharedMesh=mesh;row.AddComponent<MeshRenderer>().sharedMaterials=new[]{cover,paper};
            var size=mesh.bounds.size;row.transform.localScale=new Vector3(1.5f/size.x,.55f/size.y,.25f/size.z);
            var light=new GameObject("Light").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(45,-25,0);light.intensity=1;
            Render("book-negative-z.png",new Vector3(0,.45f,-2.3f),new Vector3(0,.30f,0),50);
            Render("book-positive-z.png",new Vector3(0,.45f,2.3f),new Vector3(0,.30f,0),50);
            Render("book-top.png",new Vector3(0,2.3f,0),new Vector3(0,.1f,0),50);
            Debug.Log("BOOK_INSPECT_OK");
        }
    }
}
