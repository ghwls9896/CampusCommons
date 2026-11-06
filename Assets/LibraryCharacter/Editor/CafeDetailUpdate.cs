using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CampusCommons.LibraryCharacter.Editor
{
    // Adds replaceable cafe props to the existing lobby, while preserving its doors and interactions.
    public static class CafeDetailUpdate
    {
        const string ScenePath = "Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity";
        const string Root = "Assets/UserModels_CafeUpdate/PolyHavenCC0/";
        const string Request = "Tools/cafe-detail-update.request";
        const string Result = "Tools/cafe-detail-update-result.txt";
        static readonly string[] Required = {
            "Coffee/CoffeeBrewer.fbx", "Coffee/CoffeeBrewer_diff.png",
            "Tea/TeaSet.fbx", "Tea/TeaSet_diff.jpg", "Hat/FishermanHat.fbx",
            "Hat/FishermanHat_diff.jpg", "Chalkboard/Chalkboard.fbx",
            "Chalkboard/Chalkboard_frame_diff.png", "Chalkboard/Chalkboard_board_diff.png"
        };

        [InitializeOnLoadMethod] static void Init()
        {
            if (File.Exists(Request)) EditorApplication.update += RunRequested;
        }
        static void RunRequested()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (Required.Any(p => !AssetDatabase.LoadMainAssetAtPath(Root + p))) return;
            EditorApplication.update -= RunRequested;
            File.Delete(Request);
            try { Apply(); }
            catch (Exception ex) { File.WriteAllText(Result, "FAIL: " + ex); Debug.LogException(ex); }
        }
        static Material Material(string name, string texture, float metallic, float smoothness)
        {
            string path = Root + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, path); }
            var image = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + texture);
            if (!image) throw new Exception("Missing texture: " + texture);
            mat.mainTexture = image; mat.color = Color.white;
            mat.SetFloat("_Metallic", metallic); mat.SetFloat("_Glossiness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }
        static Bounds BoundsOf(Renderer[] renderers)
        {
            if (renderers.Length == 0) throw new Exception("No visible model geometry");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
        static GameObject Model(Transform parent, string file, string name, string meshName,
            Material material, Vector3 floorPosition, float width, float angle = 0, bool turnUpright = false)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Root + file);
            if (!asset) throw new Exception("Missing model: " + file);
            var obj = UnityEngine.Object.Instantiate(asset);
            obj.name = name; obj.transform.SetParent(parent, false);
            obj.transform.rotation = Quaternion.Euler(0, angle, 0) * Quaternion.Euler(turnUpright ? -90 : 0, 0, 0);
            var all = obj.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in all)
            {
                bool active = string.IsNullOrEmpty(meshName) || renderer.name.IndexOf(meshName, StringComparison.OrdinalIgnoreCase) >= 0;
                renderer.enabled = active;
                if (active && material) renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
            }
            var selected = all.Where(r => r.enabled).ToArray();
            var bounds = BoundsOf(selected);
            float originalWidth = Mathf.Max(bounds.size.x, bounds.size.z);
            if (originalWidth < .0001f) throw new Exception("Invalid model width: " + name);
            obj.transform.localScale *= width / originalWidth;
            bounds = BoundsOf(selected);
            obj.transform.position += floorPosition + Vector3.up * (bounds.size.y * .5f) - bounds.center;
            return obj;
        }
        static void Text(Transform parent, string label, string content, Vector3 position, float size, Vector3 rotation)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/UserModels_CafeUpdate/SceneMaterials/World text depth.mat");
            if (!mat) throw new Exception("World text material missing");
            var obj = new GameObject(label); obj.transform.SetParent(parent, false); obj.transform.position = position; obj.transform.rotation = Quaternion.Euler(rotation);
            var text = obj.AddComponent<TextMesh>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
            text.fontSize = 64; text.characterSize = size; text.color = new Color(.95f, .89f, .75f);
            obj.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }
        static void HatStand(Transform parent, Vector3 at)
        {
            var brass = AssetDatabase.LoadAssetAtPath<Material>("Assets/UserModels_CafeUpdate/SceneMaterials/Aged brass.mat");
            var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/UserModels_CafeUpdate/SceneMaterials/Walnut.mat");
            foreach (var part in new[]{ ("Hat stand", at + Vector3.up * .18f, new Vector3(.025f,.18f,.025f), brass), ("Hat stand base", at + Vector3.up * .01f, new Vector3(.16f,.01f,.16f), wood), ("Hat display pad", at + Vector3.up * .365f, new Vector3(.16f,.01f,.16f), wood) })
            {
                var obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder); obj.name = part.Item1; obj.transform.SetParent(parent, false);
                obj.transform.position = part.Item2; obj.transform.localScale = part.Item3; obj.GetComponent<Renderer>().sharedMaterial = part.Item4;
                UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
            }
        }
        [MenuItem("Campus/Add detailed cafe props")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != ScenePath) throw new Exception("Open the living scene and stop Play first.");
            var cafe = GameObject.Find("Cafe Interior") ?? GameObject.Find("Cafe and future shop");
            if (!cafe) throw new Exception("The existing cafe was not found.");
            if (GameObject.Find("Cafe detailed props")) throw new Exception("Cafe details were already added.");
            foreach (string path in Required) if (!AssetDatabase.LoadMainAssetAtPath(Root + path)) throw new Exception("Asset not ready: " + path);
            string backup = AssetDatabase.GenerateUniqueAssetPath("Assets/LibraryCharacter/Backups/GrandLibrary_before_cafe_details.unity");
            if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new Exception("Could not back up the open scene.");

            var coffee = Material("Coffee brewer material", "Coffee/CoffeeBrewer_diff.png", .18f, .45f);
            var tea = Material("Porcelain tea set material", "Tea/TeaSet_diff.jpg", .05f, .48f);
            var hat = Material("Costume hat material", "Hat/FishermanHat_diff.jpg", 0f, .2f);
            var board = Material("Chalkboard slate material", "Chalkboard/Chalkboard_board_diff.png", 0f, .15f);
            var frame = Material("Chalkboard wood material", "Chalkboard/Chalkboard_frame_diff.png", 0f, .24f);

            var group = new GameObject("Cafe detailed props"); group.transform.SetParent(cafe.transform, false);
            Undo.RegisterCreatedObjectUndo(group, "Add cafe props");
            var parent = group.transform;
            string[] oldMachine = { "Espresso machine housing", "Espresso machine top", "Espresso machine front", "Espresso drip tray", "Coffee group", "Portafilter handle", "Steam wand", "Porcelain cup" };
            foreach (var transform in cafe.GetComponentsInChildren<Transform>(true))
                if (oldMachine.Contains(transform.name)) transform.gameObject.SetActive(false);

            Model(parent, "Coffee/CoffeeBrewer.fbx", "Coffee brewer and glass carafes", null, coffee, new Vector3(-.55f, 1.095f, 10.87f), .78f, 180, true);
            for (int i = 0; i < 3; i++)
            {
                float x = .42f + i * .25f;
                Model(parent, "Tea/TeaSet.fbx", "Cup saucer " + (i + 1), "saucer_circular_03", tea, new Vector3(x, 1.095f, 10.79f), .165f);
                Model(parent, "Tea/TeaSet.fbx", "Coffee cup " + (i + 1), "cup_small_01", tea, new Vector3(x, 1.107f, 10.79f), .145f, i * 18f);
            }
            Model(parent, "Tea/TeaSet.fbx", "Cafe sugar bowl", "sugar_cup_01", tea, new Vector3(1.25f, 1.095f, 10.85f), .16f);
            Model(parent, "Hat/FishermanHat.fbx", "Costume display hat 1", null, hat, new Vector3(3.82f, 1.275f, 10.72f), .36f, -18, true);
            Model(parent, "Hat/FishermanHat.fbx", "Costume display hat 2", null, hat, new Vector3(4.30f, 1.275f, 10.72f), .36f, 20, true);
            HatStand(parent, new Vector3(3.82f, .895f, 10.72f)); HatStand(parent, new Vector3(4.30f, .895f, 10.72f));
            var chalk = Model(parent, "Chalkboard/Chalkboard.fbx", "Cafe standing menu", null, null, new Vector3(-3.75f, .01f, 9.7f), .82f, -8f, true);
            foreach (var renderer in chalk.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m && m.name.IndexOf("board", StringComparison.OrdinalIgnoreCase) >= 0 && m.name.IndexOf("frame", StringComparison.OrdinalIgnoreCase) < 0 ? board : frame).ToArray();
            var chalkBounds = BoundsOf(chalk.GetComponentsInChildren<Renderer>());
            var collision = new GameObject("Menu collision"); collision.transform.SetParent(parent, false); collision.transform.position = chalkBounds.center;
            var box = collision.AddComponent<BoxCollider>(); box.size = chalkBounds.size;
            Vector3 front = Quaternion.Euler(0, -8, 0) * Vector3.back;
            Text(parent, "Cafe chalk menu lettering", "COFFEE\nTEA", chalkBounds.center + Vector3.up * .05f + front * (chalkBounds.extents.z * .7f + .05f), .016f, new Vector3(10, -8, 0));

            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Could not save the updated scene.");
            RenderPreview();
            File.WriteAllText(Result, "PASS: imported coffee brewer, porcelain cups and saucers, hats and chalkboard; backup=" + backup + "\n");
            Debug.Log("CAFE_DETAIL_UPDATE_OK " + backup);
        }
        static void RenderPreview()
        {
            Directory.CreateDirectory("Design/CafeUpdate");
            var obj = new GameObject("Temporary cafe inspection camera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = obj.AddComponent<Camera>(); camera.transform.position = new Vector3(0, 2.7f, 5.8f);
            camera.transform.LookAt(new Vector3(0, 1.8f, 10.8f)); camera.fieldOfView = 65; camera.nearClipPlane = .08f; camera.farClipPlane = 150;
            var rt = new RenderTexture(960, 600, 24) { antiAliasing = 2 }; camera.targetTexture = rt; camera.Render();
            RenderTexture.active = rt; var image = new Texture2D(960, 600, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 960, 600), 0, 0); image.Apply();
            File.WriteAllBytes("Design/CafeUpdate/final-cafe-details.png", image.EncodeToPNG());
            camera.targetTexture = null; RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}

