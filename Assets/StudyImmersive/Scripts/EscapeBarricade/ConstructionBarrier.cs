using System.Collections.Generic;
using UnityEngine;

namespace CampusCommons.StudyImmersive
{
    /// <summary>
    /// Creates a solid, temporary construction barrier. The future maintenance
    /// NPC controls where and when it is placed; this class does not choose routes.
    /// Position is the middle of the barrier at floor height; local X is its width.
    /// </summary>
    public sealed class ConstructionBarrier : MonoBehaviour
    {
        // The entrance corridor measures about two metres between the walls.
        public const float DefaultWidth = 2f;
        public const float Height = 2.1f;
        const float Thickness = .16f;
        readonly List<Material> materials = new List<Material>();
        Mesh stripes;
        public float Width { get; private set; }

        /// <param name="groundCenter">Center of the barrier at floor level.</param>
        /// <param name="rotation">The direction of local X is across the hallway.</param>
        /// <param name="width">Full width in meters, clamped to 1-12 meters.</param>
        /// <param name="lifetimeSeconds">Positive values remove the barrier automatically; zero leaves it until destroyed.</param>
        public static ConstructionBarrier Create(Vector3 groundCenter, Quaternion rotation, float width = DefaultWidth, float lifetimeSeconds = 0f)
        {
            var root = new GameObject("Construction Barrier");
            root.transform.SetPositionAndRotation(groundCenter, rotation);
            var barrier = root.AddComponent<ConstructionBarrier>();
            barrier.Build(Mathf.Clamp(width, 1f, 12f));
            if (lifetimeSeconds > 0f && Application.isPlaying) Destroy(root, lifetimeSeconds);
            return barrier;
        }

        void Build(float width)
        {
            Width = width;
            var wall = gameObject.AddComponent<BoxCollider>();
            wall.center = new Vector3(0, Height * .5f, 0);
            wall.size = new Vector3(width, Height, Thickness);

            Shader shader = Shader.Find("Standard");
            if (!shader) shader = Shader.Find("Sprites/Default");
            var yellow = NewMaterial(shader, new Color(.95f, .68f, .06f));
            var black = NewMaterial(shader, new Color(.045f, .045f, .04f));
            AddCube("Yellow solid panel", new Vector3(0, Height * .5f, 0), new Vector3(width, Height, Thickness), yellow);
            AddCube("Left black frame", new Vector3(-width * .5f + .05f, Height * .5f, 0), new Vector3(.10f, Height, .22f), black);
            AddCube("Right black frame", new Vector3(width * .5f - .05f, Height * .5f, 0), new Vector3(.10f, Height, .22f), black);
            AddCube("Top black frame", new Vector3(0, Height - .045f, 0), new Vector3(width, .09f, .22f), black);
            AddCube("Bottom black frame", new Vector3(0, .045f, 0), new Vector3(width, .09f, .22f), black);
            AddCube("Left base", new Vector3(-width * .5f + .22f, .04f, 0), new Vector3(.36f, .08f, .75f), black);
            AddCube("Right base", new Vector3(width * .5f - .22f, .04f, 0), new Vector3(.36f, .08f, .75f), black);

            stripes = BuildStripes(width);
            stripes.name = "Diagonal hazard stripes";
            stripes.hideFlags = HideFlags.DontSave;
            var stripedFace = new GameObject("Black diagonal stripes - both sides");
            stripedFace.transform.SetParent(transform, false);
            stripedFace.AddComponent<MeshFilter>().sharedMesh = stripes;
            stripedFace.AddComponent<MeshRenderer>().sharedMaterial = black;
        }

        Material NewMaterial(Shader shader, Color color)
        {
            var material = new Material(shader) { color = color, hideFlags = HideFlags.DontSave };
            materials.Add(material); return material;
        }
        void AddCube(string objectName, Vector3 center, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = objectName; part.transform.SetParent(transform, false);
            part.transform.localPosition = center; part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = material;
            var extraCollider = part.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(extraCollider); else DestroyImmediate(extraCollider);
        }

        // Clip each diagonal stripe to the yellow panel instead of letting it
        // protrude beyond the edges. Draw both faces with outward-facing triangles.
        static Mesh BuildStripes(float width)
        {
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            float xMin = -width * .5f + .10f, xMax = width * .5f - .10f;
            const float yMin = .10f, yMax = Height - .10f, slope = .78f, period = .90f, bandWidth = .34f;
            int first = Mathf.FloorToInt((xMin + slope * yMin) / period) - 1;
            int last = Mathf.CeilToInt((xMax + slope * yMax) / period) + 1;
            for (int band = first; band <= last; band++)
            {
                float lower = band * period, upper = lower + bandWidth;
                var polygon = new List<Vector2> { new Vector2(xMin, yMin), new Vector2(xMax, yMin), new Vector2(xMax, yMax), new Vector2(xMin, yMax) };
                polygon = Clip(polygon, lower, slope, true); polygon = Clip(polygon, upper, slope, false);
                if (polygon.Count < 3) continue;
                AddFace(polygon, -Thickness * .5f - .002f, true, vertices, triangles);
                AddFace(polygon, Thickness * .5f + .002f, false, vertices, triangles);
            }
            var mesh = new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static List<Vector2> Clip(List<Vector2> polygon, float threshold, float slope, bool keepAbove)
        {
            var output = new List<Vector2>(); if (polygon.Count == 0) return output;
            Vector2 previous = polygon[polygon.Count - 1]; float previousValue = previous.x + slope * previous.y - threshold;
            foreach (var current in polygon)
            {
                float currentValue = current.x + slope * current.y - threshold;
                bool inside = keepAbove ? currentValue >= 0f : currentValue <= 0f;
                bool wasInside = keepAbove ? previousValue >= 0f : previousValue <= 0f;
                if (inside != wasInside)
                    output.Add(Vector2.Lerp(previous, current, previousValue / (previousValue - currentValue)));
                if (inside) output.Add(current);
                previous = current; previousValue = currentValue;
            }
            return output;
        }
        static void AddFace(List<Vector2> polygon, float z, bool reverse, List<Vector3> vertices, List<int> triangles)
        {
            int start = vertices.Count;
            foreach (var point in polygon) vertices.Add(new Vector3(point.x, point.y, z));
            for (int i = 1; i < polygon.Count - 1; i++)
            {
                triangles.Add(start); triangles.Add(start + (reverse ? i + 1 : i)); triangles.Add(start + (reverse ? i : i + 1));
            }
        }
        void OnDestroy()
        {
            if (stripes) { if (Application.isPlaying) Destroy(stripes); else DestroyImmediate(stripes); }
            foreach (var material in materials)
                if (material) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
        }
    }
}
