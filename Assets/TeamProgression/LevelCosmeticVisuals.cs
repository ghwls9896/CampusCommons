using System;
using System.Collections.Generic;
using CampusCommons.Account;
using CampusCommons.GrandLibrary;
using UnityEngine;
using UnityEngine.Rendering;

namespace CampusCommons.TeamProgression
{
    /// <summary>
    /// Local visual-only rewards. No XP/level mutation and no Sunbox dependency.
    /// Attaches to the already-created "Student character" under the local Player.
    /// </summary>
    [DefaultExecutionOrder(-28000)]
    public sealed class LevelCosmeticVisuals : MonoBehaviour
    {
        public enum AuraPalette { Blue, Purple, Green }
        public enum CapePalette { Blue, Red, White }

        public static LevelCosmeticVisuals Instance { get; private set; }

        [Header("Cosmetic colors (local appearance only)")]
        [SerializeField] AuraPalette auraPalette = AuraPalette.Blue;
        [SerializeField] CapePalette capePalette = CapePalette.Blue;

#if UNITY_EDITOR
        [Header("Editor-only preview (no XP or server changes)")]
        [Tooltip("Only works in the Unity Editor after a valid account has entered the world.")]
        public bool previewInEditor;
        [Range(1, 15)] public int previewLevel = 15;
#endif

        Transform attachedModel;
        GameObject visualRoot;
        GameObject auraRoot;
        GameObject capeRoot;
        GameObject specialRoot;
        Transform auraSpin;
        Transform magicSpin;
        Transform capePivot;
        Material lineMaterial;
        Material capeMaterial;
        Mesh capeMesh;
        ParticleSystem auraParticles;
        readonly List<LineRenderer> auraLines = new List<LineRenderer>();
        float nextRefresh;
        int displayedLevel = -1;
        AuraPalette appliedAura;
        CapePalette appliedCape;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() { Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Instance == null)
                new GameObject("Campus level cosmetics").AddComponent<LevelCosmeticVisuals>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.3f;

            var unlocks = AccountLevelUnlocks.Instance;
            var gate = AccountGate.Instance;
            bool entered = gate != null && gate.Ready &&
                           unlocks != null && unlocks.IsReady;

            if (!entered)
            {
                ShowLevel(0);
                return;
            }

            Player player = FindAnyObjectByType<Player>();
            Transform model = player != null
                ? player.transform.Find("Student character") : null;

            if (model == null)
            {
                ForgetModel();
                return;
            }

            if (attachedModel != model || visualRoot == null)
            {
                ForgetModel();
                attachedModel = model;
                BuildVisuals(model);
                displayedLevel = -1;
            }

            int level = unlocks.CurrentLevel;
#if UNITY_EDITOR
            // A visual-only developer preview. Never changes account data or unlock flags.
            if (previewInEditor)
                level = Mathf.Clamp(previewLevel, 1, 15);
#endif
            ApplyPalette();
            ShowLevel(level);
        }

        void LateUpdate()
        {
            if (auraRoot != null && auraRoot.activeSelf && auraSpin != null)
                auraSpin.localRotation = Quaternion.Euler(0f, Time.time * 14f, 0f);

            if (specialRoot != null && specialRoot.activeSelf && magicSpin != null)
                magicSpin.localRotation = Quaternion.Euler(0f, -Time.time * 18f, 0f);

            if (capeRoot != null && capeRoot.activeSelf && capePivot != null)
                capePivot.localRotation =
                    Quaternion.Euler(Mathf.Sin(Time.time * 1.8f) * 2f, 0f, 0f);
        }

        void BuildVisuals(Transform model)
        {
            visualRoot = Child(model, "CC Level Cosmetics");
            lineMaterial = MakeMaterial("CC cosmetic glow lines", Color.white);
            capeMaterial = MakeMaterial("CC cosmetic cape", CapeColor(), true);
            BuildAura();
            BuildCape();
            BuildSpecial();
            ApplyPalette();
            ShowLevel(0);
        }

        void BuildAura()
        {
            auraRoot = Child(visualRoot.transform, "Lv5 - Aurora");
            auraSpin = Child(auraRoot.transform, "Rotating magic circles").transform;
            Ring(auraSpin, "Outer blue ring", 0.72f, 0.075f, 0.026f,
                 AuraColor(), auraLines);
            Ring(auraSpin, "Inner blue ring", 0.55f, 0.085f, 0.014f,
                 AuraColor(), auraLines);
            for (int i = 0; i < 8; ++i)
            {
                float a = i * Mathf.PI * 0.25f;
                Vector3 from = new Vector3(Mathf.Cos(a) * 0.64f, 0.08f,
                                           Mathf.Sin(a) * 0.64f);
                Vector3 to = new Vector3(Mathf.Cos(a) * 0.71f, 0.08f,
                                         Mathf.Sin(a) * 0.71f);
                auraLines.Add(Line(auraSpin, "Rune notch", new[] { from, to },
                                   AuraColor(), 0.018f));
            }

            auraParticles = Particles(auraRoot.transform, "Rising blue dust",
                                      AuraColor(), 22f, 0.60f, 0.045f);
            auraRoot.SetActive(false);
        }

        void BuildCape()
        {
            capeRoot = Child(visualRoot.transform, "Lv10 - Royal cape");
            capePivot = Child(capeRoot.transform, "Cape shoulder anchor").transform;
            capePivot.localPosition = new Vector3(0f, 1.40f, -0.24f);
            var cloth = Child(capePivot, "Cloth mesh");
            var filter = cloth.AddComponent<MeshFilter>();
            var renderer = cloth.AddComponent<MeshRenderer>();
            capeMesh = CreateCapeMesh();
            filter.sharedMesh = capeMesh;
            renderer.sharedMaterial = capeMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            Color gold = new Color(1f, 0.72f, 0.25f, 0.95f);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3[] edge = new Vector3[9];
                for (int j = 0; j < edge.Length; ++j)
                {
                    float v = j / 8f;
                    edge[j] = CapeVertex(side == -1 ? 0f : 1f, v);
                }
                Line(capePivot, "Golden side embroidery", edge, gold, 0.018f);
            }
            var hem = new Vector3[13];
            for (int j = 0; j < hem.Length; ++j)
                hem[j] = CapeVertex(j / 12f, 1f);
            Line(capePivot, "Golden lower hem", hem, gold, 0.026f);
            capeRoot.SetActive(false);
        }

        static Vector3 CapeVertex(float u, float v)
        {
            float width = Mathf.Lerp(0.58f, 1.13f, v);
            float x = (u - 0.5f) * width;
            float y = -1.18f * v;
            float z = -0.025f - v * v * 0.23f +
                      0.035f * Mathf.Abs(u - 0.5f);
            return new Vector3(x, y, z);
        }

        static Mesh CreateCapeMesh()
        {
            const int columns = 9, rows = 9;
            int count = columns * rows;
            var vertices = new Vector3[count * 2];
            var uv = new Vector2[count * 2];
            var triangles = new int[(columns - 1) * (rows - 1) * 12];
            for (int y = 0; y < rows; ++y)
                for (int x = 0; x < columns; ++x)
                {
                    int index = y * columns + x;
                    float u = x / (float)(columns - 1);
                    float v = y / (float)(rows - 1);
                    vertices[index] = vertices[index + count] = CapeVertex(u, v);
                    uv[index] = uv[index + count] = new Vector2(u, v);
                }

            int t = 0;
            for (int y = 0; y < rows - 1; ++y)
                for (int x = 0; x < columns - 1; ++x)
                {
                    int a = y * columns + x;
                    int b = a + 1;
                    int c = a + columns;
                    int d = c + 1;
                    triangles[t++] = a; triangles[t++] = b; triangles[t++] = c;
                    triangles[t++] = b; triangles[t++] = d; triangles[t++] = c;
                    triangles[t++] = a + count; triangles[t++] = c + count;
                    triangles[t++] = b + count;
                    triangles[t++] = b + count; triangles[t++] = c + count;
                    triangles[t++] = d + count;
                }

            var mesh = new Mesh { name = "CC generated cape" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        void BuildSpecial()
        {
            specialRoot = Child(visualRoot.transform, "Lv15 - Golden ascension");
            magicSpin = Child(specialRoot.transform, "Golden magic circle").transform;
            Color gold = new Color(1f, 0.73f, 0.22f, 1f);
            Ring(magicSpin, "Golden outer ring", 0.91f, 0.085f, 0.035f,
                 gold, null);
            Ring(magicSpin, "Golden inner ring", 0.69f, 0.095f, 0.020f,
                 gold, null);

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3[] mainFeather =
                {
                    new Vector3(side * 0.22f, 1.50f, -0.28f),
                    new Vector3(side * 0.54f, 1.81f, -0.30f),
                    new Vector3(side * 1.03f, 2.13f, -0.30f),
                    new Vector3(side * 0.95f, 1.80f, -0.30f),
                    new Vector3(side * 0.76f, 1.57f, -0.30f),
                    new Vector3(side * 0.33f, 1.36f, -0.30f)
                };
                Line(specialRoot.transform, "Golden wing outline",
                     mainFeather, gold, 0.060f);
                for (int i = 0; i < 3; ++i)
                {
                    float offset = i * 0.16f;
                    Line(specialRoot.transform, "Wing feather",
                        new[]
                        {
                            new Vector3(side * 0.30f, 1.49f - offset * 0.25f, -0.32f),
                            new Vector3(side * (0.65f + offset), 1.75f - offset, -0.32f),
                            new Vector3(side * (0.86f + offset), 1.87f - offset, -0.32f)
                        }, gold, 0.026f);
                }
            }
            var sparkles = Particles(specialRoot.transform, "Golden sparkles",
                                     gold, 32f, 0.90f, 0.055f);
            sparkles.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            specialRoot.SetActive(false);
        }

        void ShowLevel(int level)
        {
            if (level == displayedLevel) return;
            displayedLevel = level;
            if (auraRoot != null) auraRoot.SetActive(level >= 5);
            if (capeRoot != null) capeRoot.SetActive(level >= 10);
            if (specialRoot != null) specialRoot.SetActive(level >= 15);
            // No account identifier and no assertion that preview level is saved.
            Debug.Log("[CampusCommons] Local cosmetic visual level: " + level);
        }

        void ApplyPalette()
        {
            if (capeMaterial != null && (appliedCape != capePalette || displayedLevel < 0))
            {
                capeMaterial.color = CapeColor();
                appliedCape = capePalette;
            }
            if (auraParticles != null && (appliedAura != auraPalette || displayedLevel < 0))
            {
                Color tint = AuraColor();
                foreach (var line in auraLines)
                {
                    if (line == null) continue;
                    line.startColor = tint;
                    line.endColor = tint;
                }
                var main = auraParticles.main;
                main.startColor = tint;
                appliedAura = auraPalette;
            }
        }

        Color AuraColor()
        {
            switch (auraPalette)
            {
                case AuraPalette.Purple: return new Color(0.69f, 0.30f, 1f, 0.9f);
                case AuraPalette.Green: return new Color(0.27f, 1f, 0.48f, 0.9f);
                default: return new Color(0.20f, 0.70f, 1f, 0.9f);
            }
        }

        Color CapeColor()
        {
            switch (capePalette)
            {
                case CapePalette.Red: return new Color(0.54f, 0.11f, 0.19f);
                case CapePalette.White: return new Color(0.88f, 0.88f, 0.92f);
                default: return new Color(0.08f, 0.21f, 0.46f);
            }
        }

        static GameObject Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.layer = 2; // Same non-physics layer as Player's avatar visuals.
            go.transform.SetParent(parent, false);
            return go;
        }

        static Material MakeMaterial(string name, Color color, bool cloth = false)
        {
            // The cape needs an ordinary 3D mesh shader; the glowing lines and
            // particles use a transparent sprite shader when available.
            Shader shader = cloth ? Shader.Find("Standard")
                                  : Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader) { name = name, color = color };
            return material;
        }

        LineRenderer Line(Transform parent, string name, Vector3[] points,
                          Color tint, float width)
        {
            var go = Child(parent, name);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = false;
            line.positionCount = points.Length;
            line.widthMultiplier = width;
            line.numCornerVertices = 3;
            line.numCapVertices = 3;
            line.sharedMaterial = lineMaterial;
            line.startColor = tint;
            line.endColor = tint;
            line.shadowCastingMode = ShadowCastingMode.Off;
            for (int i = 0; i < points.Length; ++i)
                line.SetPosition(i, points[i]);
            return line;
        }

        void Ring(Transform parent, string name, float radius, float y,
                  float width, Color tint, List<LineRenderer> register)
        {
            var points = new Vector3[97];
            for (int i = 0; i < points.Length; ++i)
            {
                float a = i * Mathf.PI * 2f / (points.Length - 1);
                points[i] = new Vector3(Mathf.Cos(a) * radius, y,
                                        Mathf.Sin(a) * radius);
            }
            var line = Line(parent, name, points, tint, width);
            if (register != null) register.Add(line);
        }

        ParticleSystem Particles(Transform parent, string name, Color tint,
                                 float perSecond, float radius, float size)
        {
            var go = Child(parent, name);
            var system = go.AddComponent<ParticleSystem>();
            var main = system.main;
            main.duration = 2f;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 1.5f;
            main.startSpeed = 0.35f;
            main.startSize = size;
            main.startColor = tint;
            main.maxParticles = 100;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = system.emission;
            emission.rateOverTime = perSecond;
            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = lineMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return system;
        }

        void ForgetModel()
        {
            if (visualRoot != null) Destroy(visualRoot);
            if (capeMesh != null) Destroy(capeMesh);
            if (capeMaterial != null) Destroy(capeMaterial);
            if (lineMaterial != null) Destroy(lineMaterial);
            visualRoot = auraRoot = capeRoot = specialRoot = null;
            auraSpin = magicSpin = capePivot = null;
            auraParticles = null;
            capeMaterial = lineMaterial = null;
            capeMesh = null;
            attachedModel = null;
            auraLines.Clear();
            displayedLevel = -1;
        }

        void OnDestroy()
        {
            ForgetModel();
            if (Instance == this) Instance = null;
        }
    }
}
