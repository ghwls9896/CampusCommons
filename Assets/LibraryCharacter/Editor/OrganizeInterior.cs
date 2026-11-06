using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CampusCommons.GrandLibrary;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CampusCommons.LibraryCharacter.Editor
{
    /// <summary>Groups related furniture, aligns it to the current rugs and repairs the school hall tables.</summary>
    public static class OrganizeInterior
    {
        const string ScenePath = "Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity";
        const string Generated = "Assets/LibraryCharacter/Generated/InteriorGroups";
        const string Request = "Tools/organize-interior.request";
        static Transform[] All() => SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        static Transform One(string name)
        {
            var hits=All().Where(t=>t.name==name).ToArray();
            if(hits.Length!=1)throw new Exception("Expected one object named "+name+", found "+hits.Length);
            return hits[0];
        }
        static Transform Group(string name, Transform parent, Vector3 position)
        {
            var obj=new GameObject(name);obj.transform.SetParent(parent,false);obj.transform.position=position;return obj.transform;
        }
        static void Parent(Transform child,Transform parent) { child.SetParent(parent,true); }
        static Bounds BoundsOf(Transform transform)
        {
            var rs=transform.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
            if(rs.Length==0)throw new Exception("No visible mesh under "+transform.name);
            var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;
        }
        static Vector3 Horizontal(Vector3 p) { p.y=0;return p; }
        static string N(int n) => n.ToString("00");
        [InitializeOnLoadMethod] static void Init(){if(File.Exists(Request))EditorApplication.update+=Run;}
        static void Run()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(!AssetDatabase.LoadAssetAtPath<Font>("Assets/StudyImmersive/Resources/Inter-SemiBold.ttf"))return;
            EditorApplication.update-=Run;File.Delete(Request);
            try{Apply();}catch(Exception e){File.WriteAllText("Tools/organize-interior-result.txt","FAIL: "+e);Debug.LogException(e);}
        }
        static Mesh SaveMesh(Mesh mesh,string name)
        {
            string path=AssetDatabase.GenerateUniqueAssetPath(Generated+"/"+name+".asset");mesh.name=name;AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static Mesh Slice(Mesh source,List<int> triangles,Transform sourceTransform,Transform target,string name)
        {
            var vertices=source.vertices;var normals=source.normals;var uvs=source.uv;var colors=source.colors;
            var mapping=new Dictionary<int,int>();var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();var color=new List<Color>();var ix=new List<int>();
            Matrix4x4 matrix=target.worldToLocalMatrix*sourceTransform.localToWorldMatrix;Matrix4x4 normalMatrix=matrix.inverse.transpose;
            Func<int,int> add=(old)=>{
                if(mapping.TryGetValue(old,out int found))return found;
                int index=v.Count;mapping.Add(old,index);v.Add(matrix.MultiplyPoint3x4(vertices[old]));
                if(normals.Length==vertices.Length)n.Add(normalMatrix.MultiplyVector(normals[old]).normalized);
                if(uvs.Length==vertices.Length)uv.Add(uvs[old]);if(colors.Length==vertices.Length)color.Add(colors[old]);return index;
            };
            bool flip=matrix.determinant<0;
            for(int i=0;i<triangles.Count;i+=3){int a=add(triangles[i]),b=add(triangles[i+1]),c=add(triangles[i+2]);ix.Add(a);ix.Add(flip?c:b);ix.Add(flip?b:c);}
            var mesh=new Mesh { indexFormat=v.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16 };mesh.SetVertices(v);mesh.SetTriangles(ix,0);
            if(n.Count==v.Count)mesh.SetNormals(n);else mesh.RecalculateNormals();if(uv.Count==v.Count)mesh.SetUVs(0,uv);if(color.Count==v.Count)mesh.SetColors(color);
            mesh.RecalculateBounds();return SaveMesh(mesh,name);
        }
        static void GroupStudies()
        {
            var study=One("Study Furniture");
            for(int i=1;i<=6;i++)
            {
                var table=One("Study Table "+N(i));var round=One("Round rug beneath Study Table "+N(i));var old=One("Study Rug "+N(i));
                Vector3 center=BoundsOf(round).center;Vector3 delta=Horizontal(center-table.position);
                var lamp=One("Desk Lamp "+N(i));var plant=One("Desk Plant "+N(i));var books=One("Desk Books "+N(i));
                var chairs=Enumerable.Range((i-1)*4+1,4).Select(j=>One("Study Chair "+N(j))).ToArray();
                var seats=Enumerable.Range((i-1)*4+1,4).Select(j=>One("Study Chair Seat "+N(j))).ToArray();
                table.position+=delta;lamp.position+=delta;plant.position+=delta;books.position+=delta;
                for(int j=0;j<4;j++){chairs[j].position+=delta;seats[j].position+=delta;Parent(seats[j],chairs[j]);}
                Parent(lamp,table);Parent(plant,table);Parent(books,table);
                if(i==3){old.position=new Vector3(center.x,old.position.y,center.z);old.gameObject.SetActive(true);round.gameObject.SetActive(false);}
                var set=Group("Study Set "+N(i),study,new Vector3(center.x,0,center.z));
                Parent(table,set);foreach(var chair in chairs)Parent(chair,set);Parent(round,set);Parent(old,set);
            }
        }
        static void GroupLounges(StringBuilder report)
        {
            var furniture=One("Lounge Furniture");var source=One("Lobby Rug Detail");var filter=source.GetComponent<MeshFilter>();var renderer=source.GetComponent<MeshRenderer>();
            if(!filter||!filter.sharedMesh||filter.sharedMesh.subMeshCount!=1||!renderer||!renderer.enabled)throw new Exception("Original lounge rugs are not ready");
            var mesh=filter.sharedMesh;var vertices=mesh.vertices;var triangles=mesh.triangles;
            var chunks=Enumerable.Range(0,4).Select(_=>new List<int>()).ToArray();var bounds=new Bounds[4];var have=new bool[4];
            for(int i=0;i<triangles.Length;i+=3)
            {
                Vector3 a=source.TransformPoint(vertices[triangles[i]]),b=source.TransformPoint(vertices[triangles[i+1]]),c=source.TransformPoint(vertices[triangles[i+2]]),mid=(a+b+c)/3;
                int q=(mid.x<0?0:1)+(mid.z<0?0:2);chunks[q].Add(triangles[i]);chunks[q].Add(triangles[i+1]);chunks[q].Add(triangles[i+2]);
                if(!have[q]){bounds[q]=new Bounds(a,Vector3.zero);have[q]=true;}bounds[q].Encapsulate(a);bounds[q].Encapsulate(b);bounds[q].Encapsulate(c);
            }
            if(chunks.Any(c=>c.Count<60)||bounds.Any(b=>b.size.x<3||b.size.z<3))throw new Exception("Could not separate the four lounge rugs safely");
            int[] quadrants={0,2,1,3};
            for(int i=1;i<=4;i++)
            {
                int q=quadrants[i-1];Vector3 center=bounds[q].center;var set=Group("Lounge Set "+N(i),furniture,new Vector3(center.x,0,center.z));
                var rug=Group("Lounge Rug "+N(i),set,set.position);var f=rug.gameObject.AddComponent<MeshFilter>();f.sharedMesh=Slice(mesh,chunks[q],source,rug,"Lounge Rug "+N(i));
                var r=rug.gameObject.AddComponent<MeshRenderer>();r.sharedMaterials=renderer.sharedMaterials;r.shadowCastingMode=renderer.shadowCastingMode;r.receiveShadows=renderer.receiveShadows;
                var sofa=One("Sofa "+N(i));var table=One("Coffee Table "+N(i));var plant=One("Coffee Table Plant "+N(i));
                var chairs=new[]{One("Lounge Chair "+N(i*2-1)),One("Lounge Chair "+N(i*2))};
                Vector3 shift=Horizontal(center-BoundsOf(table).center);
                Vector3 sofaOffset=Horizontal(BoundsOf(sofa).center-sofa.position);
                for(int j=1;j<=3;j++){var seat=One("Sofa Seat "+N((i-1)*3+j));seat.position+=sofaOffset;Parent(seat,sofa);}
                for(int j=0;j<2;j++)
                {
                    var seat=One("Lounge Chair Seat "+N((i-1)*2+j+1));seat.position+=Horizontal(BoundsOf(chairs[j]).center-chairs[j].position);Parent(seat,chairs[j]);
                }
                plant.position+=Horizontal(BoundsOf(table).center-BoundsOf(plant).center);Parent(plant,table);
                foreach(var item in new[]{sofa,table,chairs[0],chairs[1]}){item.position+=shift;Parent(item,set);}
                report.AppendLine("Lounge Set "+N(i)+" rug="+center.ToString("F2")+" furniture shift="+shift.ToString("F2"));
            }
            renderer.enabled=false;
        }
        static void GroupShelves()
        {
            foreach(var entry in new[]{("Bookcase 01","Bookcase Props "),("Bookcase 02","Bookcase Props "),("Room Bookcase 01","Room Books "),("Room Bookcase 02","Room Books "),("Room Bookcase 03","Room Books "),("Room Bookcase 04","Room Books ")})
            {
                var shelf=One(entry.Item1);var nearby=All().Where(t=>t.name.StartsWith(entry.Item2,StringComparison.Ordinal)&&Mathf.Abs(t.position.x-shelf.position.x)<.25f&&Mathf.Abs(t.position.z-shelf.position.z)<.5f).ToArray();
                if(nearby.Length!=3)throw new Exception("Unexpected bookshelf contents for "+entry.Item1+": "+nearby.Length);
                foreach(var book in nearby)Parent(book,shelf);
            }
        }
        static void GroupRooms()
        {
            var mafia=One("Mafia Furniture");for(int i=1;i<=10;i++)Parent(One("Mafia Seat "+N(i)),One("Mafia Chair "+N(i)));
            var quiz=One("Quiz Furniture");var rugs=All().Where(t=>t.name=="Quiz seating rug").ToArray();if(rugs.Length!=2)throw new Exception("Expected two quiz rugs");
            for(int i=1;i<=2;i++)
            {
                var desk=One("Quiz Desk "+N(i));var chair=One("Quiz Chair "+N(i));var seat=One("Quiz Seat "+N(i));
                var rug=rugs.OrderBy(t=>Horizontal(t.position-desk.position).sqrMagnitude).First();
                var group=Group("Quiz Station "+N(i),quiz,Horizontal(desk.position));Parent(seat,chair);Parent(desk,group);Parent(chair,group);Parent(rug,group);
            }
        }
        static void GroupCafe()
        {
            var cafe=One("Cafe and future shop");var doorway=cafe.parent;var wrapper=Group("Cafe and Shop",doorway,new Vector3(0,0,10.85f));Parent(cafe,wrapper);Parent(One("Grey cafe rug"),wrapper);
            var service=Group("Service Counter",cafe,new Vector3(0,0,10.85f));var shop=Group("Costume Display",cafe,new Vector3(4.05f,0,10.65f));
            var sign=Group("Cafe Sign",cafe,new Vector3(0,2.95f,11.15f));var menu=Group("Standing Menu",cafe,new Vector3(-3.75f,0,9.7f));
            var details=One("Cafe detailed props");var children=cafe.Cast<Transform>().Where(t=>t!=service&&t!=shop&&t!=sign&&t!=menu&&t!=details).ToArray();
            foreach(var child in children)
            {
                string n=child.name;
                if(n.StartsWith("Costume display")||n.StartsWith("Display ")||n.StartsWith("Shop "))Parent(child,shop);
                else if(n.StartsWith("Cafe header")||n=="Cafe title"||n.StartsWith("Sign suspension")||n=="Cafe warm light")Parent(child,sign);
                else Parent(child,service);
            }
            var hats=new[]{Group("Hat Display 01",shop,new Vector3(3.82f,.895f,10.72f)),Group("Hat Display 02",shop,new Vector3(4.30f,.895f,10.72f))};
            var settings=Enumerable.Range(1,3).Select(i=>Group("Coffee Setting "+N(i),service,new Vector3(.42f+(i-1)*.25f,1.095f,10.79f))).ToArray();
            foreach(var child in details.Cast<Transform>().ToArray())
            {
                string n=child.name;
                if(n.StartsWith("Costume display hat")||n.StartsWith("Hat stand")||n=="Hat display pad")Parent(child,hats[child.position.x<4.05f?0:1]);
                else if(n=="Cafe standing menu"||n=="Menu collision"||n=="Cafe chalk menu lettering")Parent(child,menu);
                else if(n.StartsWith("Cup saucer ")||n.StartsWith("Coffee cup "))
                {int number=int.Parse(n.Substring(n.LastIndexOf(' ')+1));Parent(child,settings[number-1]);}
                else Parent(child,service);
            }
            if(details.childCount==0&&details.GetComponents<Component>().Length==1)UnityEngine.Object.DestroyImmediate(details.gameObject);
            var main=One("Cafe and doorway update");var chandelier=Group("Chandelier",main,new Vector3(0,12.8f,0));
            foreach(string n in new[]{"Chandelier suspension","Victorian chandelier","Victorian chandelier warm light"})Parent(One(n),chandelier);
        }
        struct HallTable { public float x,z;public bool alongZ;public HallTable(float x,float z,bool zAxis=false){this.x=x;this.z=z;alongZ=zAxis;} }
        static readonly HallTable[] Tables={new HallTable(498.5f,-23.25f),new HallTable(506.25f,-23.25f),new HallTable(513.25f,-23.25f),new HallTable(508.5f,-28.5f,true),new HallTable(513.5f,-28.5f,true),new HallTable(513.5f,-35f,true),new HallTable(500.75f,-35f,true),new HallTable(505.75f,-32f),new HallTable(521.25f,-35f),new HallTable(519f,-26.25f),new HallTable(518.75f,-23.25f),new HallTable(518f,-21.5f)};
        static void Beam(Transform parent,string name,Vector3 local,Vector3 size,Material mat)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=local;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;
        }
        static void BuildHallTables(StringBuilder report)
        {
            var source=All().Select(t=>t.GetComponent<MeshFilter>()).FirstOrDefault(f=>f&&f.sharedMesh&&f.sharedMesh.name=="SCHOOL_017_SOLID");
            if(!source||!source.GetComponent<MeshCollider>()||source.sharedMesh.subMeshCount!=1)throw new Exception("School table source mesh not found");
            var original=source.sharedMesh;var verts=original.vertices;var triangles=original.triangles;
            var parts=Tables.Select(_=>new List<int>()).ToArray();var rest=new List<int>();
            for(int i=0;i<triangles.Length;i+=3)
            {
                Vector3 mid=(source.transform.TransformPoint(verts[triangles[i]])+source.transform.TransformPoint(verts[triangles[i+1]])+source.transform.TransformPoint(verts[triangles[i+2]]))/3;
                int match=-1;
                for(int j=0;j<Tables.Length;j++)
                {
                    var t=Tables[j];float dx=mid.x-t.x,dz=mid.z-t.z;float along=t.alongZ?dz:dx,across=t.alongZ?dx:dz;
                    if(Mathf.Abs(along)>2.51f||Mathf.Abs(across)>.64f||mid.y<.40f||mid.y>.81f)continue;
                    if(match>=0)throw new Exception("Ambiguous school table triangle");match=j;
                }
                var dest=match>=0?parts[match]:rest;dest.Add(triangles[i]);dest.Add(triangles[i+1]);dest.Add(triangles[i+2]);
            }
            if(parts.Any(p=>p.Count!=108))throw new Exception("Unexpected school table geometry: "+string.Join(",",parts.Select(p=>(p.Count/3).ToString())));
            var metalPath=Generated+"/School table steel.mat";var metal=AssetDatabase.LoadAssetAtPath<Material>(metalPath);
            if(!metal){metal=new Material(Shader.Find("Standard")){name="School table steel",color=new Color(.25f,.31f,.35f)};metal.SetFloat("_Metallic",.62f);metal.SetFloat("_Glossiness",.35f);AssetDatabase.CreateAsset(metal,metalPath);}
            var root=Group("School Hall Tables",null,Vector3.zero);var sourceRenderer=source.GetComponent<MeshRenderer>();var oldCollider=source.GetComponent<MeshCollider>();
            for(int i=0;i<Tables.Length;i++)
            {
                var table=Tables[i];var group=Group("School Hall Table "+N(i+1),root,new Vector3(table.x,0,table.z));
                var surface=Group("Tabletop and Benches",group,group.position);var f=surface.gameObject.AddComponent<MeshFilter>();f.sharedMesh=Slice(original,parts[i],source.transform,surface,"School Hall Table "+N(i+1));
                var mr=surface.gameObject.AddComponent<MeshRenderer>();mr.sharedMaterials=sourceRenderer.sharedMaterials;mr.shadowCastingMode=sourceRenderer.shadowCastingMode;mr.receiveShadows=sourceRenderer.receiveShadows;
                var mc=surface.gameObject.AddComponent<MeshCollider>();mc.sharedMesh=f.sharedMesh;mc.sharedMaterial=oldCollider.sharedMaterial;
                for(int frame=0;frame<3;frame++)
                {
                    float along=(frame-1)*2f;var frameRoot=Group("Support Frame "+(frame+1),group,group.position);
                    foreach(float side in new[]{-.22f,.22f})
                    {
                        Vector3 at=table.alongZ?new Vector3(side,.355f,along):new Vector3(along,.355f,side);
                        Beam(frameRoot,"Floor-to-table post",at,new Vector3(.055f,.71f,.055f),metal);
                    }
                    Vector3 cross=table.alongZ?new Vector3(0,.425f,along):new Vector3(along,.425f,0);
                    Vector3 size=table.alongZ?new Vector3(1.20f,.05f,.055f):new Vector3(.055f,.05f,1.20f);
                    Beam(frameRoot,"Bench support",cross,size,metal);
                }
            }
            var remainder=UnityEngine.Object.Instantiate(original);remainder.SetTriangles(rest,0);remainder.RecalculateBounds();SaveMesh(remainder,"School desks without hall tables");
            source.sharedMesh=remainder;oldCollider.sharedMesh=null;oldCollider.sharedMesh=remainder;
            report.AppendLine("School hall tables="+parts.Length+", separate table mesh and 3 grounded frames each");
        }
        static void Typography()
        {
            var font=AssetDatabase.LoadAssetAtPath<Font>("Assets/StudyImmersive/Resources/Inter-SemiBold.ttf");var shader=Shader.Find("Campus/WorldTextDepth");
            if(!font||!shader)throw new Exception("Modern font or depth-tested shader missing");
            var labels=All().Select(t=>t.GetComponent<TextMesh>()).Where(t=>t&&t.text.All(c=>c<128)).ToArray();
            foreach(var label in labels)font.RequestCharactersInTexture(label.text,label.fontSize,label.fontStyle);
            string path=Generated+"/Modern world text.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}mat.shader=shader;mat.mainTexture=font.material.mainTexture;mat.color=Color.white;EditorUtility.SetDirty(mat);
            foreach(var label in labels){label.font=font;var renderer=label.GetComponent<MeshRenderer>();if(renderer)renderer.sharedMaterial=mat;}
            var controller=Group("World Sign Font",null,Vector3.zero).gameObject.AddComponent<WorldTextFontAtlas>();controller.font=font;controller.depthMaterial=mat;
        }
        [MenuItem("Campus/Organize furniture and school tables")]
        public static void Apply()
        {
            var scene=SceneManager.GetActiveScene();if(EditorApplication.isPlaying||scene.path!=ScenePath)throw new Exception("Open the living scene and stop Play first");
            if(All().Any(t=>t.name=="Study Set 01"||t.name=="School Hall Tables"))throw new Exception("This organization has already been applied");
            string backup=AssetDatabase.GenerateUniqueAssetPath("Assets/LibraryCharacter/Backups/GrandLibrary_before_organizing.unity");
            if(!EditorSceneManager.SaveScene(scene,backup,true))throw new Exception("Could not back up the live scene");
            if(!AssetDatabase.IsValidFolder(Generated))AssetDatabase.CreateFolder("Assets/LibraryCharacter/Generated","InteriorGroups");
            int seatCount=UnityEngine.Object.FindObjectsByType<Seat>(FindObjectsInactive.Include).Length;var report=new StringBuilder();
            GroupStudies();GroupLounges(report);GroupShelves();GroupRooms();GroupCafe();BuildHallTables(report);Typography();Physics.SyncTransforms();
            if(UnityEngine.Object.FindObjectsByType<Seat>(FindObjectsInactive.Include).Length!=seatCount)throw new Exception("A seat was lost during grouping");
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();if(!EditorSceneManager.SaveScene(scene))throw new Exception("Could not save scene");
            report.AppendLine("Seats preserved="+seatCount+"; backup="+backup);File.WriteAllText("Tools/organize-interior-result.txt","PASS\n"+report);
            Debug.Log("ORGANIZE_INTERIOR_OK backup="+backup);
        }
    }
}

