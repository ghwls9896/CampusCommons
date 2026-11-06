using UnityEngine;

namespace CampusCommons.LibraryCharacter
{
    /// <summary>Shows the school wall-top trim generated from the existing walls.</summary>
    public static class SchoolUpperTrim
    {
        public static void Create(Transform mapRoot = null)
        {
            if(GameObject.Find("School Upper Blue Trim"))return;
            var mesh=Resources.Load<Mesh>("SchoolUpperTrim");
            var texture=Resources.Load<Texture2D>("SchoolUpperTrimGradient");
            if(!mesh || !texture)return;
            var root=new GameObject("School Upper Blue Trim");
            if(mapRoot)root.transform.SetParent(mapRoot,true);
            root.AddComponent<MeshFilter>().sharedMesh=mesh;
            var material=new Material(Shader.Find("Unlit/Texture")){name="School Upper Blue Trim (runtime)",mainTexture=texture};
            root.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
    }
}
