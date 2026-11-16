using System.Collections.Generic;
using UnityEngine;

namespace CampusCommons.StudyImmersive
{
    // A wall-mounted cabinet; the interaction itself remains owned by EscapeGame.
    public sealed class WallBreakerVisual : MonoBehaviour
    {
        readonly List<Material> materials = new List<Material>();
        Transform cover;
        public bool IsOpen { get; private set; }
        public void Open() { if (cover) cover.localRotation = Quaternion.Euler(0f, -105f, 0f); IsOpen = true; }
        public void Close() { if (cover) cover.localRotation = Quaternion.identity; IsOpen = false; }

        public static GameObject Create(Transform school)
        {
            var root = new GameObject("강당 벽면 차단기");
            root.transform.SetPositionAndRotation(school.TransformPoint(new Vector3(0f, 1.35f, -20.11f)),
                Quaternion.LookRotation(school.TransformDirection(Vector3.back), Vector3.up));
            root.transform.SetParent(school, true);
            var cabinet = root.AddComponent<WallBreakerVisual>();
            Material shell = cabinet.MakeMaterial(new Color(.37f, .41f, .43f));
            Material dark = cabinet.MakeMaterial(new Color(.10f, .12f, .13f));
            Material pale = cabinet.MakeMaterial(new Color(.67f, .70f, .67f));
            Material yellow = cabinet.MakeMaterial(new Color(.96f, .72f, .10f));
            Material red = cabinet.MakeMaterial(new Color(.77f, .10f, .07f));
            cabinet.Box("Cabinet", new Vector3(0, 0, 0), new Vector3(.78f, .98f, .20f), shell);

            cabinet.Box("Switch recess", new Vector3(0, .06f, .126f), new Vector3(.46f, .30f, .016f), dark);
            for (int i = -1; i <= 1; i++)
            {
                cabinet.Box("Breaker switch", new Vector3(i * .14f, .065f, .145f), new Vector3(.09f, .21f, .035f), shell);
                cabinet.Box("Switch toggle", new Vector3(i * .14f, .09f, .174f), new Vector3(.05f, .07f, .035f), i == 0 ? red : pale);
            }
            cabinet.cover = new GameObject("Cabinet door hinge").transform;
            cabinet.cover.SetParent(root.transform, false);
            cabinet.cover.localPosition = new Vector3(-.34f, 0, .20f);
            cabinet.Box("Door panel", new Vector3(.34f, 0, 0), new Vector3(.68f, .88f, .035f), pale, cabinet.cover);
            cabinet.Box("Warning label", new Vector3(.34f, -.25f, .025f), new Vector3(.28f, .20f, .02f), yellow, cabinet.cover);
            cabinet.Box("Warning mark", new Vector3(.34f, -.23f, .04f), new Vector3(.025f, .10f, .014f), dark, cabinet.cover);
            cabinet.Box("Warning dot", new Vector3(.34f, -.30f, .04f), new Vector3(.025f, .025f, .014f), dark, cabinet.cover);
            cabinet.Box("Door handle", new Vector3(.625f, 0, .04f), new Vector3(.025f, .16f, .04f), dark, cabinet.cover);
            return root;
        }

        Material MakeMaterial(Color color)
        {
            var material = new Material(Shader.Find("Standard"));
            material.color = color; materials.Add(material); return material;
        }
        void Box(string name, Vector3 position, Vector3 scale, Material material, Transform parent = null)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name; item.transform.SetParent(parent ? parent : transform, false);
            item.transform.localPosition = position; item.transform.localScale = scale;
            item.GetComponent<Collider>().enabled = false;
            item.GetComponent<Renderer>().sharedMaterial = material;
        }
        void OnDestroy() { foreach (var material in materials) if (material) Destroy(material); }
    }
}
