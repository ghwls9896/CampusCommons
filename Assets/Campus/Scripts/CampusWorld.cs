using System;
using System.Collections.Generic;
using UnityEngine;

namespace CampusCommons
{
    [Serializable] public class CampusData
    {
        public MaterialSpec[] materials;
        public ShapeSpec[] shapes;
        public SeatSpec[] seats;
        public LabelSpec[] labels;
    }
    [Serializable] public class MaterialSpec { public string name, hex; }
    [Serializable] public class ShapeSpec
    {
        public string name, kind, material, group;
        public float[] position, scale;
        public float yaw;
        public bool solid;
    }
    [Serializable] public class SeatSpec { public string name, group; public float[] position; public float yaw; }
    [Serializable] public class LabelSpec { public string name, text, material, group; public float[] position; public float size, yaw; }

    public static class CampusWorld
    {
        public const string RootName = "Campus Commons";
        public static Vector3 V(float[] a) => new Vector3(a[0], a[1], a[2]);
        public static GameObject Build()
        {
            var source = Resources.Load<TextAsset>("campus");
            if (!source) throw new InvalidOperationException("Resources/campus.json is missing.");
            var data = JsonUtility.FromJson<CampusData>(source.text);
            var root = new GameObject(RootName);
            var groups = new Dictionary<string, Transform>();
            foreach (string name in new[] { "Lounge", "Quiz", "Mafia" })
            {
                var group = new GameObject(name).transform;
                group.SetParent(root.transform, false);
                groups.Add(name, group);
            }
            var materials = new Dictionary<string, Material>();
            foreach (var spec in data.materials)
            {
                ColorUtility.TryParseHtmlString(spec.hex, out Color color);
                var mat = new Material(Shader.Find("Standard")) { name = spec.name, color = color };
                mat.SetFloat("_Glossiness", .16f);
                materials.Add(spec.name, mat);
            }
            foreach (var spec in data.shapes)
            {
                PrimitiveType type = spec.kind == "Cylinder" ? PrimitiveType.Cylinder :
                    spec.kind == "Sphere" ? PrimitiveType.Sphere : PrimitiveType.Cube;
                var go = GameObject.CreatePrimitive(type);
                go.name = spec.name;
                go.transform.SetParent(groups[spec.group], false);
                go.transform.position = V(spec.position);
                go.transform.rotation = Quaternion.Euler(0, spec.yaw, 0);
                Vector3 scale = V(spec.scale);
                // Unity's primitive cylinder is 2 units tall; the data stores full height.
                if (type == PrimitiveType.Cylinder) scale.y *= .5f;
                go.transform.localScale = scale;
                go.GetComponent<Renderer>().sharedMaterial = materials[spec.material];
                if (!spec.solid) Remove(go.GetComponent<Collider>());
                go.isStatic = true;
            }
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            foreach (var spec in data.labels)
            {
                var go = new GameObject(spec.name);
                go.transform.SetParent(groups[spec.group], false);
                go.transform.position = V(spec.position);
                go.transform.rotation = Quaternion.Euler(0, spec.yaw, 0);
                var label = go.AddComponent<TextMesh>();
                label.text = spec.text; label.font = font; label.fontSize = 64;
                label.characterSize = spec.size / 6f;
                label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
                label.color = materials[spec.material].color;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            foreach (var spec in data.seats)
            {
                var go = new GameObject(spec.name + " [E to sit]");
                go.transform.SetParent(groups[spec.group], false);
                go.transform.position = V(spec.position);
                go.transform.rotation = Quaternion.Euler(0, spec.yaw, 0);
                go.AddComponent<CampusSeat>().room = spec.group;
            }
            var sun = new GameObject("Afternoon sunlight"); sun.transform.SetParent(root.transform);
            sun.transform.rotation = Quaternion.Euler(48, -32, 0);
            var light = sun.AddComponent<Light>(); light.type = LightType.Directional;
            light.intensity = .8f; light.color = new Color(1, .96f, .9f); light.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.4f, .44f, .46f);
            RenderSettings.ambientEquatorColor = new Color(.26f, .28f, .25f);
            RenderSettings.ambientGroundColor = new Color(.16f, .18f, .16f);
            RenderSettings.fog = false;
            return root;
        }
        static void Remove(UnityEngine.Object obj)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
