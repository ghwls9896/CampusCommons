using UnityEngine;

namespace CampusCommons.LibraryCharacter
{
    /// <summary>Non-colliding study items on the existing long tables in the school hall.</summary>
    public static class SchoolHallDecorations
    {
        struct Table { public float x,z;public bool alongZ;public Table(float px,float pz,bool zAxis=false){x=px;z=pz;alongZ=zAxis;} }
        static readonly Table[] tables={
            new Table(498.5f,-23.25f),new Table(506.25f,-23.25f),new Table(513.25f,-23.25f),
            new Table(508.5f,-28.5f,true),new Table(513.5f,-28.5f,true),new Table(513.5f,-35f,true),
            new Table(500.75f,-35f,true),new Table(505.75f,-32f),new Table(521.25f,-35f),
            new Table(519f,-26.25f),new Table(518.75f,-23.25f),new Table(518f,-21.5f)
        };
        static Mesh cube,cylinder;
        static Material[] covers;
        static Material pages,ceramic,coffee,pencil,laptop,screen;
        static Mesh PrimitiveMesh(PrimitiveType shape)
        {
            var item=GameObject.CreatePrimitive(shape);var mesh=item.GetComponent<MeshFilter>().sharedMesh;
            if(Application.isPlaying)Object.Destroy(item);else Object.DestroyImmediate(item);return mesh;
        }
        static Material ColorMaterial(string name,Color color)
        {return new Material(Shader.Find("Unlit/Color")){name=name,color=color};}
        static void Part(Transform parent,string name,Mesh mesh,Vector3 position,Vector3 scale,Material material)
        {
            var part=new GameObject(name);part.transform.SetParent(parent,false);part.transform.localPosition=position;part.transform.localScale=scale;
            part.AddComponent<MeshFilter>().sharedMesh=mesh;part.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
        public static void Create(Transform mapRoot = null)
        {
            if(GameObject.Find("School Hall Study Items"))return;
            if(!cube)cube=PrimitiveMesh(PrimitiveType.Cube);if(!cylinder)cylinder=PrimitiveMesh(PrimitiveType.Cylinder);
            if(covers==null || covers.Length==0 || !covers[0])
            {
                covers=new[]{ColorMaterial("Book cover blue",new Color(.15f,.32f,.48f)),ColorMaterial("Book cover green",new Color(.24f,.39f,.30f)),
                    ColorMaterial("Book cover ochre",new Color(.70f,.43f,.17f)),ColorMaterial("Book cover red",new Color(.56f,.23f,.20f))};
                pages=ColorMaterial("Paper",new Color(.96f,.94f,.85f));ceramic=ColorMaterial("Mug",new Color(.92f,.94f,.91f));
                coffee=ColorMaterial("Drink",new Color(.25f,.14f,.08f));pencil=ColorMaterial("Pencil",new Color(.94f,.65f,.16f));
                laptop=ColorMaterial("Laptop case",new Color(.16f,.20f,.23f));screen=ColorMaterial("Laptop screen",new Color(.48f,.68f,.75f));
            }
            var root=new GameObject("School Hall Study Items");
            if(mapRoot)root.transform.SetParent(mapRoot,true);
            for(int i=0;i<tables.Length;i++)for(int station=0;station<3;station++)
            {
                var t=tables[i];Vector3 along=t.alongZ?Vector3.forward:Vector3.right;
                var group=new GameObject("Study items "+(i+1)+"-"+(station+1));
                var table=GameObject.Find("School Hall Table "+(i+1).ToString("00"));
                if(table)
                {
                    // The tabletop is now an editable group: keep study props attached when it moves.
                    group.transform.SetParent(table.transform,false);
                    group.transform.localPosition=Vector3.up*.788f+along*((station-1)*1.5f);
                    group.transform.localRotation=Quaternion.Euler(0,t.alongZ?0:90,0);
                }
                else
                {
                    group.transform.SetParent(root.transform,false);
                    group.transform.position=new Vector3(t.x,.788f,t.z)+along*((station-1)*1.5f);
                    group.transform.rotation=Quaternion.Euler(0,t.alongZ?0:90,0);
                }
                var parent=group.transform;
                if((i+station)%4==0)
                {
                    Part(parent,"Laptop keyboard",cube,new Vector3(-.035f,.01f,0),new Vector3(.27f,.02f,.19f),laptop);
                    Part(parent,"Laptop screen frame",cube,new Vector3(-.035f,.108f,-.09f),new Vector3(.27f,.19f,.014f),laptop);
                    Part(parent,"Laptop display",cube,new Vector3(-.035f,.108f,-.081f),new Vector3(.24f,.16f,.003f),screen);
                }
                else
                {
                    Part(parent,"Notebook cover",cube,new Vector3(-.035f,.012f,0),new Vector3(.27f,.024f,.19f),covers[(i+station)%covers.Length]);
                    Part(parent,"Notebook pages",cube,new Vector3(-.035f,.028f,0),new Vector3(.245f,.012f,.165f),pages);
                    Part(parent,"Pencil",cube,new Vector3(-.035f,.038f,.095f),new Vector3(.18f,.009f,.009f),pencil);
                }
                if((i+station)%2==0)
                {
                    Part(parent,"Mug",cylinder,new Vector3(.16f,.065f,0),new Vector3(.095f,.065f,.095f),ceramic);
                    Part(parent,"Drink",cylinder,new Vector3(.16f,.131f,0),new Vector3(.075f,.001f,.075f),coffee);
                    Part(parent,"Mug handle",cube,new Vector3(.218f,.077f,0),new Vector3(.035f,.035f,.018f),ceramic);
                }
                else
                    Part(parent,"Extra book",cube,new Vector3(.16f,.018f,0),new Vector3(.16f,.036f,.18f),covers[(i+station+1)%covers.Length]);
            }
        }
    }
}
