using System.Collections.Generic;
using CampusCommons.Account;
using CampusCommons.GrandLibrary;
using UnityEngine;
using UnityEngine.Rendering;

namespace CampusCommons.TeamProgression
{
    /// <summary>
    /// Visual-only incremental cosmetics for Lv.5-15.
    /// No XP calculations, rewards, account writes, or gameplay dependencies.
    /// Lv.1-4 have no effects. No nametag / no XP UI.
    /// Lv.6 unlocks two runic wrist cuffs that follow the animated hands.
    /// Lv.9 unlocks persistent floating crystal companions through Lv.15.
    /// Designed to coexist with LevelCosmeticVisuals (Lv.5/10/15 rewards).
    /// </summary>
    // Follow wrist bones after the Animator and avatar sit-pose updates.
    [DefaultExecutionOrder(20000)]
    public sealed class CharacterCosmeticEvolution : MonoBehaviour
    {
        static CharacterCosmeticEvolution instance;

        [Header("Appearance tuning")]
        [SerializeField, Range(0.5f, 1.5f)] float effectScale = 1f;
        [SerializeField, Range(0.2f, 1.5f)] float effectBrightness = 0.85f;
        [SerializeField] Vector3 crystalGroupOffset = new Vector3(0f, 0f, 0f);

        [Header("Lv.6 runic wrist cuffs")]
        [SerializeField, Range(0.07f, 0.15f)] float wristRadius = 0.095f;
        [SerializeField, Range(0f, 0.14f)] float wristInset = 0.055f;

        readonly List<Transform> crystalPrecursors = new List<Transform>();
        readonly List<Vector3> crystalBasePositions = new List<Vector3>();
        readonly List<Transform> cyanSparks = new List<Transform>();
        readonly List<Transform> goldSparks = new List<Transform>();

        Player player;
        Transform model;
        GameObject root;
        GameObject wristCuffs;
        Transform leftWristAnchor, rightWristAnchor;
        Transform leftWristRunes, rightWristRunes;
        Transform leftHand, rightHand, leftForearm, rightForearm;
        float nextWristScan;
        bool wristWarningShown;
        Mesh wristGoldMesh, wristCyanMesh;
        GameObject cyanDust;
        GameObject crystalHarbingers;
        GameObject orbit;
        GameObject goldDust;
        GameObject wingPreview;
        GameObject finalCrown;
        GameObject capeEmblemGlow;
        Transform capeAnchor;
        TrailRenderer trailLeft;
        TrailRenderer trailRight;
        Material lineMaterial;
        Material cyanCrystalMaterial;
        Material goldCrystalMaterial;
        Mesh crystalMesh;
        int shownLevel = -1;
        float nextScan;
        Vector3 previousPosition;
        bool havePreviousPosition;

        static readonly Color Cyan = new Color(0.31f, 0.83f, 1f, 0.92f);
        static readonly Color PaleCyan = new Color(0.51f, 0.92f, 1f, 0.65f);
        static readonly Color Gold = new Color(1f, 0.77f, 0.34f, 0.9f);
        static readonly Color SoftGold = new Color(1f, 0.88f, 0.59f, 0.68f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() { instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (instance == null)
                new GameObject("Campus cosmetic evolution")
                    .AddComponent<CharacterCosmeticEvolution>();
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 0.25f;

            AccountGate gate = AccountGate.Instance;
            AccountLevelUnlocks unlocks = AccountLevelUnlocks.Instance;
            if (gate == null || !gate.Ready || gate.Session == null ||
                !gate.Session.IsAuthenticated || unlocks == null ||
                !unlocks.IsReady || unlocks.CurrentUserId != gate.Session.UserId)
            {
                ClearAll();
                return;
            }

            Player currentPlayer = FindAnyObjectByType<Player>();
            Transform currentModel = currentPlayer != null
                ? currentPlayer.transform.Find("Student character") : null;
            if (currentModel == null)
            {
                ClearAll();
                return;
            }

            if (currentModel != model || player != currentPlayer || root == null)
            {
                ClearAll();
                player = currentPlayer;
                model = currentModel;
                BuildAll();
            }

            int level = Mathf.Clamp(unlocks.CurrentLevel, 1, 15);
#if UNITY_EDITOR
            // One preview checkbox controls BOTH existing cosmetics and new layers.
            LevelCosmeticVisuals original = LevelCosmeticVisuals.Instance;
            if (original != null && original.previewInEditor)
                level = Mathf.Clamp(original.previewLevel, 1, 15);
#endif
            TryBindCapeEmblem();
            if (shownLevel != level)
                ApplyLevel(level);
            if (level >= 6)
                RefreshWristBindings();
        }

        void LateUpdate()
        {
            if (root == null || player == null || model == null) return;

            bool visible = !player.IsFirstPerson;
            if (root.activeSelf != visible) root.SetActive(visible);

            Vector3 position = player.transform.position;
            float speed = 0f;
            if (havePreviousPosition && Time.deltaTime > 0.0001f)
                speed = Vector3.Distance(position, previousPosition) / Time.deltaTime;
            previousPosition = position;
            havePreviousPosition = true;

            bool trailOn = visible && shownLevel >= 8 && speed > 0.35f && speed < 9f;
            if (trailLeft != null) trailLeft.emitting = trailOn;
            if (trailRight != null) trailRight.emitting = trailOn;
            if (!visible) return;

            float t = Time.time;
            if (wristCuffs != null && wristCuffs.activeInHierarchy)
            {
                FollowWrist(leftWristAnchor, leftHand, leftForearm);
                FollowWrist(rightWristAnchor, rightHand, rightForearm);

                if (leftWristRunes != null)
                    leftWristRunes.localRotation = Quaternion.Euler(0f, t * 40f, 0f);
                if (rightWristRunes != null)
                    rightWristRunes.localRotation = Quaternion.Euler(0f, -t * 40f, 0f);
            }
            if (crystalHarbingers != null && crystalHarbingers.activeInHierarchy)
            {
                for (int i = 0; i < crystalPrecursors.Count; i++)
                {
                    Transform crystal = crystalPrecursors[i];
                    if (crystal == null) continue;
                    crystal.localPosition = crystalBasePositions[i] +
                        Vector3.up * (Mathf.Sin(t * 2.0f + i * 1.35f) * 0.045f);
                    crystal.localRotation = Quaternion.Euler(9f, t * (30f + 5f * i), 12f);
                }
            }
            if (orbit != null)
                orbit.transform.localRotation = Quaternion.Euler(0f, t * 24f, 0f);
            if (finalCrown != null)
                finalCrown.transform.localRotation = Quaternion.Euler(0f, -t * 16f, 0f);

            AnimateSparks(cyanSparks, t, 0.54f, 1.75f, 0.28f);
            AnimateSparks(goldSparks, t * 0.72f, 0.65f, 1.9f, 0.63f);
        }

        void BuildAll()
        {
            root = Child(model, "CC Cosmetic Evolution");
            root.transform.localScale = Vector3.one * effectScale;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogError("[CampusCommons] No shader available for cosmetic evolution.");
                return;
            }
            lineMaterial = new Material(shader) { name = "CC evolution shared line material" };
            lineMaterial.color = Color.white;

            crystalMesh = BuildCrystalMesh();
            cyanCrystalMaterial = MakeCrystalMaterial("CC cyan gemstone", Cyan);
            goldCrystalMaterial = MakeCrystalMaterial("CC gold gemstone", Gold);
            BuildWristCuffs();
            cyanDust = BuildSparkGroup("Lv7 - Cyan stardust", 14, Cyan, cyanSparks);
            BuildTrails();
            BuildCrystalHarbingers();
            BuildOrbit();
            goldDust = BuildSparkGroup("Lv13 - Golden stardust", 12, Gold, goldSparks);
            BuildWingPreview();
            BuildFinalCrown();
            shownLevel = -1;
        }

        // Lv.6: actual 3D torus jewelry plus rotating rune markers. The cuffs
        // live inside our own root but follow the animated wrist bones in world
        // space; we never modify the original avatar, prefab, or rig.
        void BuildWristCuffs()
        {
            wristCuffs = Child(root.transform, "Lv6 - Runic wrist cuffs");
            wristGoldMesh = BuildTorusMesh(wristRadius, 0.010f);
            wristCyanMesh = BuildTorusMesh(wristRadius * 1.08f, 0.0055f);

            BuildOneWrist("Left", out leftWristAnchor, out leftWristRunes);
            BuildOneWrist("Right", out rightWristAnchor, out rightWristRunes);
            wristCuffs.SetActive(false);
        }

        void BuildOneWrist(string side, out Transform anchor, out Transform runes)
        {
            anchor = Child(wristCuffs.transform, side + " wrist bone follower").transform;

            AddBandMesh(anchor, "Gold 3D cuff", wristGoldMesh,
                goldCrystalMaterial, -0.018f);
            AddBandMesh(anchor, "Cyan 3D cuff", wristCyanMesh,
                cyanCrystalMaterial, 0.018f);

            runes = Child(anchor, "Rotating wrist runes").transform;
            RingXZ(runes, "Fine cyan rune circle", wristRadius * 1.19f,
                0f, 0.007f, PaleCyan);

            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI * 2f / 6f;
                Vector3 outward = new Vector3(Mathf.Cos(angle), 0f,
                    Mathf.Sin(angle));
                MakeLine(runes, "Rune engraving " + i, new[]
                {
                    outward * (wristRadius * 1.11f) + Vector3.up * -0.021f,
                    outward * (wristRadius * 1.28f) + Vector3.up * 0.000f,
                    outward * (wristRadius * 1.11f) + Vector3.up * 0.021f
                }, i % 2 == 0 ? Cyan : SoftGold, 0.009f);
            }
        }

        void AddBandMesh(Transform parent, string title, Mesh mesh,
            Material material, float y)
        {
            GameObject band = Child(parent, title);
            band.transform.localPosition = new Vector3(0f, y, 0f);
            band.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = band.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static Mesh BuildTorusMesh(float radius, float tubeRadius)
        {
            const int around = 48;
            const int across = 8;
            Vector3[] vertices = new Vector3[(around + 1) * (across + 1)];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[around * across * 6];

            for (int a = 0; a <= around; a++)
            {
                float angle = a * Mathf.PI * 2f / around;
                float ca = Mathf.Cos(angle), sa = Mathf.Sin(angle);
                for (int b = 0; b <= across; b++)
                {
                    float tube = b * Mathf.PI * 2f / across;
                    float r = radius + tubeRadius * Mathf.Cos(tube);
                    int index = a * (across + 1) + b;
                    vertices[index] = new Vector3(ca * r,
                        tubeRadius * Mathf.Sin(tube), sa * r);
                    uv[index] = new Vector2(a / (float)around,
                        b / (float)across);
                }
            }

            int t = 0;
            for (int a = 0; a < around; a++)
                for (int b = 0; b < across; b++)
                {
                    int p = a * (across + 1) + b;
                    int q = p + across + 1;
                    triangles[t++] = p;
                    triangles[t++] = p + 1;
                    triangles[t++] = q;
                    triangles[t++] = q;
                    triangles[t++] = p + 1;
                    triangles[t++] = q + 1;
                }

            Mesh mesh = new Mesh { name = "CC runic wrist cuff torus" };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        void RefreshWristBindings()
        {
            if (model == null || Time.unscaledTime < nextWristScan) return;
            nextWristScan = Time.unscaledTime + 0.6f;

            // Re-evaluate active rig after a gender/preset switch.
            Transform newLeft = null, newRight = null;
            Transform newLeftForearm = null, newRightForearm = null;

            Animator[] animators = model.GetComponentsInChildren<Animator>(true);
            foreach (Animator animator in animators)
            {
                if (animator == null || !animator.gameObject.activeInHierarchy ||
                    !animator.isHuman) continue;
                Transform lh = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                Transform rh = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (lh == null || rh == null) continue;
                newLeft = lh;
                newRight = rh;
                newLeftForearm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                newRightForearm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                break;
            }

            // Sunbox and some imported skeletons use Hand_L / Hand_R,
            // LowerArm_L / LowerArm_R instead of a Humanoid avatar mapping.
            if (newLeft == null || newRight == null)
            {
                Transform[] bones = model.GetComponentsInChildren<Transform>(true);
                newLeft = FindNamedActiveBone(bones, "lefthand", "handl", "lhand", "leftwrist", "wristl");
                newRight = FindNamedActiveBone(bones, "righthand", "handr", "rhand", "rightwrist", "wristr");
                newLeftForearm = FindNamedActiveBone(bones, "leftlowerarm", "lowerarml", "lforearm", "leftforearm", "forearml");
                newRightForearm = FindNamedActiveBone(bones, "rightlowerarm", "lowerarmr", "rforearm", "rightforearm", "forearmr");
            }

            leftHand = newLeft;
            rightHand = newRight;
            leftForearm = newLeftForearm;
            rightForearm = newRightForearm;

            if (!wristWarningShown && (leftHand == null || rightHand == null))
            {
                wristWarningShown = true;
                Debug.LogWarning("[CampusCommons] Lv.6 cuffs: hand bones not found. " +
                    "Wrist effects stay hidden until an active avatar rig is available.");
            }
        }

        static Transform FindNamedActiveBone(Transform[] bones, params string[] names)
        {
            foreach (Transform bone in bones)
            {
                if (bone == null || !bone.gameObject.activeInHierarchy) continue;
                string name = bone.name.ToLowerInvariant()
                    .Replace("_", "").Replace("-", "")
                    .Replace(" ", "").Replace(":", "").Replace(".", "");
                foreach (string expected in names)
                    if (name == expected || name.EndsWith(expected))
                        return bone;
            }
            return null;
        }

        void FollowWrist(Transform anchor, Transform hand, Transform forearm)
        {
            if (anchor == null) return;
            bool found = hand != null && hand.gameObject.activeInHierarchy;
            if (anchor.gameObject.activeSelf != found)
                anchor.gameObject.SetActive(found);
            if (!found) return;

            Vector3 alongArm = forearm != null && forearm.gameObject.activeInHierarchy
                ? forearm.position - hand.position
                : (hand.parent != null ? hand.parent.position - hand.position : hand.up);
            if (alongArm.sqrMagnitude < 0.0001f)
                alongArm = hand.up;
            alongArm.Normalize();

            // A small inset toward the elbow puts the rings on the wrist,
            // rather than around the fingers. Both wrists follow animations.
            anchor.SetPositionAndRotation(
                hand.position + alongArm * wristInset,
                Quaternion.FromToRotation(Vector3.up, alongArm));
        }

        // Shared gem mesh: closed, light-weight octahedron (8 faces).
        static Mesh BuildCrystalMesh()
        {
            Mesh mesh = new Mesh { name = "CC crystal octahedron" };
            mesh.vertices = new[]
            {
                new Vector3( 0f,  1f,  0f), // 0 top
                new Vector3( 1f,  0f,  0f), // 1 right
                new Vector3( 0f,  0f,  1f), // 2 front
                new Vector3(-1f,  0f,  0f), // 3 left
                new Vector3( 0f,  0f, -1f), // 4 back
                new Vector3( 0f, -1f,  0f)  // 5 bottom
            };
            mesh.triangles = new[]
            {
                0, 2, 1, 0, 3, 2, 0, 1, 4, 0, 4, 3,
                5, 1, 2, 5, 2, 3, 5, 4, 1, 5, 3, 4
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        Material MakeCrystalMaterial(string title, Color tint)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return lineMaterial;

            Material material = new Material(shader) { name = title };
            Color finalTint = tint * effectBrightness;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", finalTint);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", finalTint);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", finalTint * 0.55f);
            }
            return material;
        }

        GameObject MakeCrystal(Transform parent, string title,
            Material material, Vector3 localPosition, Vector3 localScale)
        {
            GameObject gem = Child(parent, title);
            gem.transform.localPosition = localPosition;
            gem.transform.localScale = localScale;
            MeshFilter filter = gem.AddComponent<MeshFilter>();
            filter.sharedMesh = crystalMesh;
            MeshRenderer renderer = gem.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return gem;
        }

        GameObject BuildSparkGroup(string groupName, int count, Color tint,
            List<Transform> sprites)
        {
            GameObject group = Child(root.transform, groupName);
            for (int i = 0; i < count; ++i)
            {
                LineRenderer sparkle = MakeLine(group.transform, "Light mote " + i,
                    new[] { Vector3.zero, new Vector3(0f, 0.035f + (i % 3) * 0.012f, 0f) },
                    tint, 0.014f + (i % 4) * 0.003f);
                sprites.Add(sparkle.transform);
            }
            return group;
        }

        void BuildTrails()
        {
            trailLeft = MakeTrail(-0.18f);
            trailRight = MakeTrail(0.18f);
        }

        TrailRenderer MakeTrail(float x)
        {
            GameObject marker = Child(root.transform, x < 0 ? "Lv8 - Left movement trail" : "Lv8 - Right movement trail");
            marker.transform.localPosition = new Vector3(x, 0.42f, -0.14f);
            TrailRenderer trail = marker.AddComponent<TrailRenderer>();
            trail.sharedMaterial = lineMaterial;
            trail.time = 0.34f;
            trail.startWidth = 0.063f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.06f;
            trail.numCornerVertices = 3;
            trail.numCapVertices = 3;
            trail.startColor = Cyan;
            trail.endColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0f);
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.emitting = false;
            return trail;
        }

        // Lv.9: Three floating crystals behind the shoulders, not old cape pins.
        // Remains unlocked from Lv.9 through Lv.15, including with the cape.
        void BuildCrystalHarbingers()
        {
            crystalHarbingers = Child(root.transform, "Lv9 - Floating crystal trio");
            crystalHarbingers.transform.localPosition = crystalGroupOffset;

            Vector3[] positions =
            {
                new Vector3(-0.38f, 1.34f, -0.44f),
                new Vector3( 0.00f, 1.65f, -0.51f),
                new Vector3( 0.38f, 1.34f, -0.44f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject holder = Child(crystalHarbingers.transform,
                    "Floating shard anchor " + i);
                holder.transform.localPosition = positions[i];
                float size = i == 1 ? 1.15f : 0.88f;
                MakeCrystal(holder.transform,
                    "Faceted 3D shard", i == 1 ? goldCrystalMaterial : cyanCrystalMaterial,
                    Vector3.zero, new Vector3(0.105f, 0.19f, 0.087f) * size);

                MakeDiamond(holder.transform, "Fine crystal outline",
                    new Vector3(0f, 0f, 0f),
                    0.12f * size, 0.2f * size,
                    i == 1 ? SoftGold : PaleCyan, 0.012f);
                crystalPrecursors.Add(holder.transform);
                crystalBasePositions.Add(positions[i]);
            }
            crystalHarbingers.SetActive(false);
        }

        void BuildOrbit()
        {
            orbit = Child(root.transform, "Lv12 - Orbiting sigils");
            orbit.transform.localPosition = new Vector3(0f, 1.00f, 0f);
            RingXZ(orbit.transform, "Floating orbit", 0.63f, 0f, 0.008f, SoftGold);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6f;
                Vector3 pos = new Vector3(Mathf.Cos(a) * 0.63f,
                    (i % 2 == 0 ? 0.02f : -0.02f), Mathf.Sin(a) * 0.63f);
                MakeDiamond(orbit.transform, "Orbit glyph", pos, 0.043f, 0.075f,
                    SoftGold, 0.012f);
            }
        }

        void BuildWingPreview()
        {
            wingPreview = Child(root.transform, "Lv14 - Wing awakening preview");
            for (int side = -1; side <= 1; side += 2)
            {
                MakeLine(wingPreview.transform, "Small wing silhouette", new[]
                {
                    new Vector3(side * 0.24f, 1.50f, -0.27f),
                    new Vector3(side * 0.48f, 1.68f, -0.31f),
                    new Vector3(side * 0.79f, 1.84f, -0.36f),
                    new Vector3(side * 0.61f, 1.55f, -0.33f),
                    new Vector3(side * 0.32f, 1.36f, -0.27f)
                }, SoftGold, 0.026f);
                for (int i = 0; i < 3; i++)
                {
                    float a = i * 0.11f;
                    MakeLine(wingPreview.transform, "Feather preview", new[]
                    {
                        new Vector3(side * 0.30f, 1.48f - a, -0.29f),
                        new Vector3(side * (0.54f + a), 1.64f - a, -0.33f),
                        new Vector3(side * (0.69f + a), 1.67f - a, -0.35f)
                    }, SoftGold, 0.012f);
                }
            }
        }

        void BuildFinalCrown()
        {
            finalCrown = Child(root.transform, "Lv15 - Ascension crown");
            RingXZ(finalCrown.transform, "Golden floating crown", 0.49f, 2.01f, 0.017f, Gold);
            RingXZ(finalCrown.transform, "Inner crown", 0.36f, 2.04f, 0.009f, SoftGold);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                MakeLine(finalCrown.transform, "Crown ray", new[]
                {
                    new Vector3(Mathf.Cos(a) * 0.46f, 2.01f, Mathf.Sin(a) * 0.46f),
                    new Vector3(Mathf.Cos(a) * 0.49f, 2.10f + (i % 2) * 0.055f,
                        Mathf.Sin(a) * 0.49f)
                }, Gold, 0.016f);
            }
        }

        void TryBindCapeEmblem()
        {
            if (model == null) return;
            Transform currentAnchor = model.Find(
                "CC Level Cosmetics/Lv10 - Royal cape/Cape shoulder anchor");
            if (currentAnchor == null) return;
            if (capeAnchor == currentAnchor && capeEmblemGlow != null) return;
            if (capeEmblemGlow != null) Destroy(capeEmblemGlow);
            capeAnchor = currentAnchor;
            capeEmblemGlow = Child(capeAnchor, "Lv11 - Illuminated mascot border");
            capeEmblemGlow.transform.localPosition = new Vector3(0f, -0.59f, -0.127f);
            RingXY(capeEmblemGlow.transform, "Emblem halo", 0.43f, 0.41f, 0f, 0.012f, SoftGold);
            for (int i = 0; i < 4; ++i)
            {
                float a = i * Mathf.PI * 0.5f;
                Vector3 center = new Vector3(Mathf.Sin(a) * 0.43f,
                    Mathf.Cos(a) * 0.41f, -0.006f);
                MakeDiamond(capeEmblemGlow.transform, "Emblem star", center,
                    0.037f, 0.060f, Gold, 0.013f);
            }
            capeEmblemGlow.SetActive(shownLevel >= 11);
        }

        void ApplyLevel(int level)
        {
            shownLevel = level;
            // No new cosmetic effects for Lv.1-4.
            // Original LevelCosmeticVisuals shows the Lv.5 circle.
            Toggle(wristCuffs, level >= 6);
            Toggle(cyanDust, level >= 7);
            Toggle(crystalHarbingers, level >= 9);
            Toggle(orbit, level >= 12);
            Toggle(goldDust, level >= 13);
            Toggle(wingPreview, level == 14);
            Toggle(finalCrown, level >= 15);
            Toggle(capeEmblemGlow, level >= 11);

            Color trailColor = level >= 13 ? Gold : Cyan;
            if (trailLeft != null) SetTrailColor(trailLeft, trailColor);
            if (trailRight != null) SetTrailColor(trailRight, trailColor);
        }

        static void SetTrailColor(TrailRenderer trail, Color tint)
        {
            trail.startColor = tint;
            trail.endColor = new Color(tint.r, tint.g, tint.b, 0f);
        }

        static void Tint(LineRenderer line, Color color)
        {
            if (line == null) return;
            line.startColor = color;
            line.endColor = color;
        }

        static void Toggle(GameObject target, bool on)
        {
            if (target != null && target.activeSelf != on)
                target.SetActive(on);
        }

        static void AnimateSparks(List<Transform> sparks, float time,
            float radius, float height, float yOffset)
        {
            for (int i = 0; i < sparks.Count; i++)
            {
                Transform spark = sparks[i];
                if (spark == null) continue;
                float fraction = (i + 0.5f) / sparks.Count;
                float angle = fraction * Mathf.PI * 2f * 3f + time * 0.32f;
                float r = radius * (0.50f + 0.46f * Mathf.Sin(fraction * 12.7f) *
                    Mathf.Sin(fraction * 12.7f));
                spark.localPosition = new Vector3(Mathf.Cos(angle) * r,
                    yOffset + Mathf.Repeat(time * (0.4f + (i % 3) * 0.06f) + fraction * height, height),
                    Mathf.Sin(angle) * r);
            }
        }

        LineRenderer RingXZ(Transform parent, string title, float radius,
            float y, float width, Color tint)
        {
            Vector3[] points = new Vector3[73];
            for (int i = 0; i < points.Length; i++)
            {
                float a = (i / 72f) * Mathf.PI * 2f;
                points[i] = new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);
            }
            return MakeLine(parent, title, points, tint, width);
        }

        LineRenderer RingXY(Transform parent, string title, float radiusX,
            float radiusY, float z, float width, Color tint)
        {
            Vector3[] points = new Vector3[49];
            for (int i = 0; i < points.Length; i++)
            {
                float a = (i / 48f) * Mathf.PI * 2f;
                points[i] = new Vector3(Mathf.Cos(a) * radiusX,
                    Mathf.Sin(a) * radiusY, z);
            }
            return MakeLine(parent, title, points, tint, width);
        }

        void MakeDiamond(Transform parent, string title, Vector3 center,
            float width, float height, Color color, float thickness)
        {
            MakeLine(parent, title, new[]
            {
                center + new Vector3(0f, height, 0f),
                center + new Vector3(width, 0f, 0f),
                center + new Vector3(0f, -height, 0f),
                center + new Vector3(-width, 0f, 0f),
                center + new Vector3(0f, height, 0f)
            }, color, thickness);
        }

        LineRenderer MakeLine(Transform parent, string title,
            Vector3[] positions, Color color, float width)
        {
            GameObject obj = Child(parent, title);
            LineRenderer lr = obj.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.sharedMaterial = lineMaterial;
            lr.widthMultiplier = width;
            lr.positionCount = positions.Length;
            lr.numCornerVertices = 3;
            lr.numCapVertices = 3;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.startColor = color;
            lr.endColor = color;
            for (int i = 0; i < positions.Length; i++)
                lr.SetPosition(i, positions[i]);
            return lr;
        }

        static GameObject Child(Transform parent, string title)
        {
            GameObject obj = new GameObject(title);
            obj.layer = 2;
            obj.transform.SetParent(parent, false);
            return obj;
        }

        void ClearAll()
        {
            // Cape emblem belongs to the original cape anchor, not our root.
            if (capeEmblemGlow != null) Destroy(capeEmblemGlow);
            if (root != null) Destroy(root);
            if (crystalMesh != null) Destroy(crystalMesh);
            if (wristGoldMesh != null) Destroy(wristGoldMesh);
            if (wristCyanMesh != null) Destroy(wristCyanMesh);
            if (cyanCrystalMaterial != null && cyanCrystalMaterial != lineMaterial)
                Destroy(cyanCrystalMaterial);
            if (goldCrystalMaterial != null && goldCrystalMaterial != lineMaterial)
                Destroy(goldCrystalMaterial);
            if (lineMaterial != null) Destroy(lineMaterial);
            root = null;
            lineMaterial = null;
            crystalMesh = null;
            wristGoldMesh = wristCyanMesh = null;
            cyanCrystalMaterial = null;
            goldCrystalMaterial = null;
            player = null;
            model = null;
            wristCuffs = null;
            leftWristAnchor = rightWristAnchor = null;
            leftWristRunes = rightWristRunes = null;
            leftHand = rightHand = leftForearm = rightForearm = null;
            nextWristScan = 0f;
            wristWarningShown = false;
            cyanDust = crystalHarbingers = orbit = goldDust =
                wingPreview = finalCrown = capeEmblemGlow = null;
            capeAnchor = null;
            trailLeft = trailRight = null;
            crystalPrecursors.Clear();
            crystalBasePositions.Clear();
            cyanSparks.Clear();
            goldSparks.Clear();
            shownLevel = -1;
            havePreviousPosition = false;
        }

        void OnDestroy()
        {
            ClearAll();
            if (instance == this) instance = null;
        }
    }
}
