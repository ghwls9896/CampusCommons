
using System;
using System.Collections.Generic;
using CampusCommons.Account;
using CampusCommons.GrandLibrary;
using UnityEngine;
using UnityEngine.Rendering;

namespace CampusCommons.TeamProgression
{
    [DefaultExecutionOrder(-28000)]
    public sealed class LevelCosmeticVisuals : MonoBehaviour
    {
        public enum AuraPalette { Blue, Purple, Green }
        public enum CapePalette { Blue, Red, White }

        public static LevelCosmeticVisuals Instance { get; private set; }

        [Header("Cosmetic colors")]
        [SerializeField] AuraPalette auraPalette = AuraPalette.Blue;
        [SerializeField] CapePalette capePalette = CapePalette.Blue;

        [Header("Cape mascot")]
        [SerializeField] Texture2D capeMascotTexture;

        GameObject capeMascotRoot;
        Material capeMascotMaterial;
        Mesh capeMascotMesh;

#if UNITY_EDITOR
        [Header("Editor-only preview")]
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

        readonly List<LineRenderer> auraMotes =
            new List<LineRenderer>();
        readonly List<LineRenderer> specialMotes =
            new List<LineRenderer>();
        readonly List<LineRenderer> auraLines =
            new List<LineRenderer>();

        float nextRefresh;
        int displayedLevel = -1;
        AuraPalette appliedAura;
        CapePalette appliedCape;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic()
        {
            Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Instance == null)
                new GameObject("Campus level cosmetics")
                    .AddComponent<LevelCosmeticVisuals>();
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

            bool entered = gate != null &&
                           gate.Ready &&
                           unlocks != null &&
                           unlocks.IsReady;

            if (!entered)
            {
                ShowLevel(0);
                return;
            }

            Player player = FindAnyObjectByType<Player>();

            Transform model = player != null
                ? player.transform.Find("Student character")
                : null;

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
            if (previewInEditor)
                level = Mathf.Clamp(previewLevel, 1, 15);
#endif

            ApplyPalette();
            ShowLevel(level);
        }

        void LateUpdate()
        {
            if (auraRoot != null &&
                auraRoot.activeSelf &&
                auraSpin != null)
            {
                auraSpin.localRotation =
                    Quaternion.Euler(0f, Time.time * 14f, 0f);
            }

            if (specialRoot != null &&
                specialRoot.activeSelf &&
                magicSpin != null)
            {
                magicSpin.localRotation =
                    Quaternion.Euler(0f, -Time.time * 18f, 0f);
            }

            if (capeRoot != null &&
                capeRoot.activeSelf &&
                capePivot != null)
            {
                capePivot.localRotation =
                    Quaternion.Euler(
                        Mathf.Sin(Time.time * 1.8f) * 2f,
                        0f, 0f);
            }

            if (auraRoot != null && auraRoot.activeSelf)
                AnimateMotes(
                    auraMotes, Time.time, 0.57f, 1.0f);

            if (specialRoot != null && specialRoot.activeSelf)
                AnimateMotes(
                    specialMotes, Time.time, 0.85f, 1.5f);
        }

        void BuildVisuals(Transform model)
        {
            visualRoot = Child(model, "CC Level Cosmetics");

            lineMaterial = MakeMaterial(
                "CC cosmetic glow lines", Color.white);

            capeMaterial = MakeMaterial(
                "CC cosmetic cape", CapeColor(), true);

            BuildAura();
            BuildCape();
            BuildSpecial();
            ApplyPalette();
            ShowLevel(0);
        }

        // ========================================
        // LV.5 BLUE AURA
        // ========================================

        void BuildAura()
        {
            auraRoot = Child(
                visualRoot.transform, "Lv5 - Aurora");

            auraSpin = Child(
                auraRoot.transform,
                "Rotating magic circles").transform;

            Ring(
                auraSpin, "Outer blue ring",
                0.72f, 0.075f, 0.026f,
                AuraColor(), auraLines);

            Ring(
                auraSpin, "Inner blue ring",
                0.55f, 0.085f, 0.014f,
                AuraColor(), auraLines);

            for (int i = 0; i < 8; ++i)
            {
                float a = i * Mathf.PI * 0.25f;

                Vector3 from = new Vector3(
                    Mathf.Cos(a) * 0.64f,
                    0.08f,
                    Mathf.Sin(a) * 0.64f);

                Vector3 to = new Vector3(
                    Mathf.Cos(a) * 0.71f,
                    0.08f,
                    Mathf.Sin(a) * 0.71f);

                auraLines.Add(
                    Line(
                        auraSpin,
                        "Rune notch",
                        new[] { from, to },
                        AuraColor(),
                        0.018f));
            }

            BuildMotes(
                auraRoot.transform,
                "Rising blue dust",
                AuraColor(),
                22,
                auraMotes,
                auraLines);

            auraRoot.SetActive(false);
        }

        // ========================================
        // LV.10 CAPE
        // ========================================

        void BuildCape()
        {
            capeRoot = Child(
                visualRoot.transform, "Lv10 - Royal cape");

            capePivot = Child(
                capeRoot.transform,
                "Cape shoulder anchor").transform;

            // ¸ÁÅä¸¦ ¾î±ú¿¡ °¡±îÀÌ ¹èÄ¡
            capePivot.localPosition =
                new Vector3(0f, 1.4f, -0.15f);

            var cloth = Child(capePivot, "Cloth mesh");

            var filter = cloth.AddComponent<MeshFilter>();
            var renderer = cloth.AddComponent<MeshRenderer>();

            capeMesh = CreateCapeMesh();

            filter.sharedMesh = capeMesh;
            renderer.sharedMaterial = capeMaterial;
            renderer.shadowCastingMode =
                ShadowCastingMode.Off;

            // ¸ÁÅä Áß¾Ó¿¡ ¸¶½ºÄÚÆ® »ý¼º
            BuildCapeMascot();

            Color gold =
                new Color(1f, 0.72f, 0.25f, 0.95f);

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3[] edge = new Vector3[9];

                for (int j = 0; j < edge.Length; ++j)
                {
                    float v = j / 8f;

                    edge[j] = CapeVertex(
                        side == -1 ? 0f : 1f, v);
                }

                Line(
                    capePivot,
                    "Golden side embroidery",
                    edge,
                    gold,
                    0.018f);
            }

            var hem = new Vector3[13];

            for (int j = 0; j < hem.Length; ++j)
                hem[j] = CapeVertex(j / 12f, 1f);

            Line(
                capePivot,
                "Golden lower hem",
                hem,
                gold,
                0.026f);

            capeRoot.SetActive(false);
        }

        // ========================================
        // CAPE MASCOT
        // ========================================

        void BuildCapeMascot()
        {
            Texture2D texture = capeMascotTexture != null
                ? capeMascotTexture
                : Resources.Load<Texture2D>(
                    "TeamProgression/CampusMascot");

            if (texture == null || capePivot == null)
            {
                Debug.LogWarning(
                    "[CampusCommons] Cape mascot image not found. " +
                    "Place CampusMascot.png in " +
                    "Assets/Resources/TeamProgression/");

                return;
            }

            // ¸¶½ºÄÚÆ® ¿øº» ºñÀ² À¯Áö
            float aspect =
                texture.width /
                (float)Mathf.Max(1, texture.height);

            float logoHeight = 0.65f;
            float logoWidth =
                Mathf.Min(0.95f, logoHeight * aspect);

            // ¸ÁÅä Áß¾Ó ³ôÀÌ
            float centerY = -0.59f;

            const int cols = 8;
            const int rows = 8;

            var vertices =
                new Vector3[(cols + 1) * (rows + 1)];

            var uv = new Vector2[vertices.Length];

            var triangles =
                new int[cols * rows * 6];

            for (int y = 0; y <= rows; y++)
            {
                float vImg = y / (float)rows;

                float localY =
                    centerY +
                    (0.5f - vImg) * logoHeight;

                float capeV =
                    Mathf.Clamp01(-localY / 1.18f);

                float capeWidth =
                    Mathf.Lerp(0.58f, 1.13f, capeV);

                for (int x = 0; x <= cols; x++)
                {
                    float uImg = x / (float)cols;

                    float localX =
                        (uImg - 0.5f) * logoWidth;

                    float capeU = Mathf.Clamp01(
                        0.5f + localX / capeWidth);

                    Vector3 pos =
                        CapeVertex(capeU, capeV);

                    // ¸ÁÅä ¹Ù±ùÂÊ¿¡ »ìÂ¦ ¶ç¿ö °ãÄ§ ¹æÁö
                    pos.z -= 0.012f;

                    int i = y * (cols + 1) + x;

                    vertices[i] = pos;

                    uv[i] = new Vector2(
                        uImg, 1f - vImg);
                }
            }

            int t = 0;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int a = y * (cols + 1) + x;
                    int b = a + 1;
                    int c = a + cols + 1;
                    int d = c + 1;

                    triangles[t++] = a;
                    triangles[t++] = b;
                    triangles[t++] = c;

                    triangles[t++] = b;
                    triangles[t++] = d;
                    triangles[t++] = c;
                }
            }

            capeMascotMesh = new Mesh
            {
                name = "CC curved cape mascot mesh"
            };

            capeMascotMesh.vertices = vertices;
            capeMascotMesh.uv = uv;
            capeMascotMesh.triangles = triangles;

            capeMascotMesh.RecalculateNormals();
            capeMascotMesh.RecalculateBounds();

            capeMascotRoot = Child(
                capePivot, "Cape mascot emblem");

            var filter =
                capeMascotRoot.AddComponent<MeshFilter>();

            filter.sharedMesh = capeMascotMesh;

            var renderer =
                capeMascotRoot.AddComponent<MeshRenderer>();

            Shader shader =
                Shader.Find("Unlit/Transparent");

            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            if (shader == null)
                shader = Shader.Find("Unlit/Texture");

            if (shader == null)
            {
                Debug.LogWarning(
                    "[CampusCommons] Transparent shader not found.");

                capeMascotRoot.SetActive(false);
                return;
            }

            capeMascotMaterial =
                new Material(shader)
                {
                    name = "CC cape mascot decal"
                };

            capeMascotMaterial.mainTexture = texture;

            renderer.sharedMaterial = capeMascotMaterial;
            renderer.shadowCastingMode =
                ShadowCastingMode.Off;
        }

        // ========================================
        // CAPE MESH
        // ========================================

        static Vector3 CapeVertex(float u, float v)
        {
            float width =
                Mathf.Lerp(0.58f, 1.13f, v);

            float x = (u - 0.5f) * width;
            float y = -1.18f * v;

            float z =
                -0.025f -
                v * v * 0.23f +
                0.035f * Mathf.Abs(u - 0.5f);

            return new Vector3(x, y, z);
        }

        static Mesh CreateCapeMesh()
        {
            const int columns = 9;
            const int rows = 9;

            int count = columns * rows;

            var vertices = new Vector3[count * 2];
            var uv = new Vector2[count * 2];

            var triangles =
                new int[(columns - 1) * (rows - 1) * 12];

            for (int y = 0; y < rows; ++y)
            {
                for (int x = 0; x < columns; ++x)
                {
                    int index = y * columns + x;

                    float u =
                        x / (float)(columns - 1);

                    float v =
                        y / (float)(rows - 1);

                    vertices[index] =
                        vertices[index + count] =
                            CapeVertex(u, v);

                    uv[index] =
                        uv[index + count] =
                            new Vector2(u, v);
                }
            }

            int t = 0;

            for (int y = 0; y < rows - 1; ++y)
            {
                for (int x = 0; x < columns - 1; ++x)
                {
                    int a = y * columns + x;
                    int b = a + 1;
                    int c = a + columns;
                    int d = c + 1;

                    triangles[t++] = a;
                    triangles[t++] = b;
                    triangles[t++] = c;

                    triangles[t++] = b;
                    triangles[t++] = d;
                    triangles[t++] = c;

                    triangles[t++] = a + count;
                    triangles[t++] = c + count;
                    triangles[t++] = b + count;

                    triangles[t++] = b + count;
                    triangles[t++] = c + count;
                    triangles[t++] = d + count;
                }
            }

            var mesh = new Mesh
            {
                name = "CC generated cape"
            };

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        // ========================================
        // LV.15 GOLDEN EFFECT
        // ========================================

        void BuildSpecial()
        {
            specialRoot = Child(
                visualRoot.transform,
                "Lv15 - Golden ascension");

            magicSpin = Child(
                specialRoot.transform,
                "Golden magic circle").transform;

            Color gold =
                new Color(1f, 0.73f, 0.22f, 1f);

            Ring(
                magicSpin,
                "Golden outer ring",
                0.91f,
                0.085f,
                0.035f,
                gold,
                null);

            Ring(
                magicSpin,
                "Golden inner ring",
                0.69f,
                0.095f,
                0.020f,
                gold,
                null);

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3[] mainFeather =
                {
                    new Vector3(
                        side * 0.22f, 1.50f, -0.28f),

                    new Vector3(
                        side * 0.54f, 1.81f, -0.30f),

                    new Vector3(
                        side * 1.03f, 2.13f, -0.30f),

                    new Vector3(
                        side * 0.95f, 1.80f, -0.30f),

                    new Vector3(
                        side * 0.76f, 1.57f, -0.30f),

                    new Vector3(
                        side * 0.33f, 1.36f, -0.30f)
                };

                Line(
                    specialRoot.transform,
                    "Golden wing outline",
                    mainFeather,
                    gold,
                    0.060f);

                for (int i = 0; i < 3; ++i)
                {
                    float offset = i * 0.16f;

                    Line(
                        specialRoot.transform,
                        "Wing feather",
                        new[]
                        {
                            new Vector3(
                                side * 0.30f,
                                1.49f - offset * 0.25f,
                                -0.32f),

                            new Vector3(
                                side * (0.65f + offset),
                                1.75f - offset,
                                -0.32f),

                            new Vector3(
                                side * (0.86f + offset),
                                1.87f - offset,
                                -0.32f)
                        },
                        gold,
                        0.026f);
                }
            }

            BuildMotes(
                specialRoot.transform,
                "Golden sparkles",
                gold,
                32,
                specialMotes,
                null);

            specialRoot.SetActive(false);
        }

        // ========================================
        // LEVEL DISPLAY
        // ========================================

        void ShowLevel(int level)
        {
            if (level == displayedLevel) return;

            displayedLevel = level;

            if (auraRoot != null)
                auraRoot.SetActive(level >= 5);

            if (capeRoot != null)
                capeRoot.SetActive(level >= 10);

            if (specialRoot != null)
                specialRoot.SetActive(level >= 15);

            Debug.Log(
                "[CampusCommons] Local cosmetic visual level: "
                + level);
        }

        void ApplyPalette()
        {
            if (capeMaterial != null &&
                (appliedCape != capePalette ||
                 displayedLevel < 0))
            {
                capeMaterial.color = CapeColor();
                appliedCape = capePalette;
            }

            if (auraMotes.Count > 0 &&
                (appliedAura != auraPalette ||
                 displayedLevel < 0))
            {
                Color tint = AuraColor();

                foreach (var line in auraLines)
                {
                    if (line == null) continue;

                    line.startColor = tint;
                    line.endColor = tint;
                }

                appliedAura = auraPalette;
            }
        }

        Color AuraColor()
        {
            switch (auraPalette)
            {
                case AuraPalette.Purple:
                    return new Color(
                        0.69f, 0.30f, 1f, 0.9f);

                case AuraPalette.Green:
                    return new Color(
                        0.27f, 1f, 0.48f, 0.9f);

                default:
                    return new Color(
                        0.20f, 0.70f, 1f, 0.9f);
            }
        }

        Color CapeColor()
        {
            switch (capePalette)
            {
                case CapePalette.Red:
                    return new Color(
                        0.54f, 0.11f, 0.19f);

                case CapePalette.White:
                    return new Color(
                        0.88f, 0.88f, 0.92f);

                default:
                    return new Color(
                        0.08f, 0.21f, 0.46f);
            }
        }

        // ========================================
        // UTILITY
        // ========================================

        static GameObject Child(
            Transform parent, string name)
        {
            var go = new GameObject(name);

            go.layer = 2;
            go.transform.SetParent(parent, false);

            return go;
        }

        static Material MakeMaterial(
            string name, Color color, bool cloth = false)
        {
            Shader shader = cloth
                ? Shader.Find("Standard")
                : Shader.Find("Sprites/Default");

            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            if (shader == null)
                shader = Shader.Find("Standard");

            var material = new Material(shader)
            {
                name = name,
                color = color
            };

            return material;
        }

        LineRenderer Line(
            Transform parent,
            string name,
            Vector3[] points,
            Color tint,
            float width)
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

            line.shadowCastingMode =
                ShadowCastingMode.Off;

            for (int i = 0; i < points.Length; ++i)
                line.SetPosition(i, points[i]);

            return line;
        }

        void Ring(
            Transform parent,
            string name,
            float radius,
            float y,
            float width,
            Color tint,
            List<LineRenderer> register)
        {
            var points = new Vector3[97];

            for (int i = 0; i < points.Length; ++i)
            {
                float a =
                    i * Mathf.PI * 2f /
                    (points.Length - 1);

                points[i] = new Vector3(
                    Mathf.Cos(a) * radius,
                    y,
                    Mathf.Sin(a) * radius);
            }

            var line = Line(
                parent, name, points, tint, width);

            if (register != null)
                register.Add(line);
        }

        void BuildMotes(
            Transform root,
            string name,
            Color tint,
            int count,
            List<LineRenderer> motes,
            List<LineRenderer> recolor)
        {
            var cluster = Child(root, name);

            for (int i = 0; i < count; ++i)
            {
                var line = Line(
                    cluster.transform,
                    "Luminous mote " + i,
                    new[]
                    {
                        Vector3.zero,
                        new Vector3(0f, 0.065f, 0f)
                    },
                    tint,
                    0.025f);

                motes.Add(line);

                if (recolor != null)
                    recolor.Add(line);
            }
        }

        static void AnimateMotes(
            List<LineRenderer> motes,
            float time,
            float radius,
            float height)
        {
            for (int i = 0; i < motes.Count; ++i)
            {
                var line = motes[i];

                if (line == null) continue;

                float fraction =
                    (i + 0.5f) / motes.Count;

                float phase =
                    time * (0.42f + (i % 4) * 0.04f) +
                    fraction * 1.618f;

                float angle =
                    fraction * Mathf.PI * 2f * 4f +
                    time * 0.5f;

                float distance =
                    radius *
                    (0.28f + 0.7f *
                    Mathf.Abs(
                        Mathf.Sin(fraction * 14f)));

                line.transform.localPosition =
                    new Vector3(
                        Mathf.Cos(angle) * distance,
                        Mathf.Repeat(phase, height),
                        Mathf.Sin(angle) * distance);
            }
        }

        void ForgetModel()
        {
            if (visualRoot != null)
                Destroy(visualRoot);

            if (capeMesh != null)
                Destroy(capeMesh);

            if (capeMascotMesh != null)
                Destroy(capeMascotMesh);

            if (capeMascotMaterial != null)
                Destroy(capeMascotMaterial);

            if (capeMaterial != null)
                Destroy(capeMaterial);

            if (lineMaterial != null)
                Destroy(lineMaterial);

            visualRoot = null;
            auraRoot = null;
            capeRoot = null;
            specialRoot = null;

            auraSpin = null;
            magicSpin = null;
            capePivot = null;

            auraMotes.Clear();
            specialMotes.Clear();
            auraLines.Clear();

            capeMaterial = null;
            lineMaterial = null;
            capeMesh = null;

            capeMascotMesh = null;
            capeMascotMaterial = null;
            capeMascotRoot = null;

            attachedModel = null;
            displayedLevel = -1;
        }

        void OnDestroy()
        {
            ForgetModel();

            if (Instance == this)
                Instance = null;
        }
    }
}
