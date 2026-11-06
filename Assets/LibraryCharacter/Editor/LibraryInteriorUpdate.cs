using System;
using System.IO;
using System.Linq;
using CampusCommons.GrandLibrary;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CampusCommons.LibraryCharacter.Editor
{
    public static class LibraryInteriorUpdate
    {
        const string ScenePath = "Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity";
        const string Request = "Tools/library-interior-update.request";
        const string Materials = "Assets/StudyImmersive/LivingMaterials/";

        [InitializeOnLoadMethod] static void Initialize() { EditorApplication.delayCall += ApplyIfRequested; }
        static void ApplyIfRequested()
        {
            if (!File.Exists(Request)) return;
            File.Delete(Request);
            try { Apply(); File.WriteAllText("Tools/library-interior-result.txt", "PASS\n"); }
            catch (Exception e) { File.WriteAllText("Tools/library-interior-result.txt", e.ToString()); Debug.LogException(e); }
        }
        static Transform[] All() => SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
        static Transform One(string name) { var t = All().FirstOrDefault(x => x.name == name); if (!t) throw new Exception("Missing " + name); return t; }
        static Material MaterialAsset(string name, Color color, float metallic, float smooth, bool glow = false)
        {
            string path = Materials + name + ".mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, path); }
            mat.color = color; mat.SetFloat("_Metallic", metallic); mat.SetFloat("_Glossiness", smooth);
            if (glow) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", color * .8f); }
            EditorUtility.SetDirty(mat); return mat;
        }
        static void MoveShelf(string name, string bookPrefix, Vector3 destination, float yaw)
        {
            var shelf = One(name); Vector3 old = shelf.position;
            var books = All().Where(t => t.name.StartsWith(bookPrefix, StringComparison.Ordinal) &&
                Mathf.Abs(t.position.x - old.x) < .25f && Mathf.Abs(t.position.z - old.z) < .5f).ToArray();
            if (books.Length != 3) throw new Exception(name + " expected 3 aligned book props, found " + books.Length);
            Quaternion turn = Quaternion.Euler(0, yaw, 0) * Quaternion.Inverse(shelf.rotation);
            foreach (var b in books) { Undo.RecordObject(b, "Move shelf books"); b.position = destination + turn * (b.position - old); b.rotation = turn * b.rotation; }
            Undo.RecordObject(shelf, "Move bookshelf"); shelf.SetPositionAndRotation(destination, Quaternion.Euler(0, yaw, 0));
        }
        static void MovePlanters()
        {
            var pots = All().Where(t => t.name.StartsWith("Grass Planter ")).ToArray();
            if (pots.Length != 20) throw new Exception("Expected 20 grass planters, found " + pots.Length);
            foreach (var t in pots)
            {
                var pot = t.Find("Planter"); if (!pot) throw new Exception(t.name + " has no pot");
                Vector3 p = pot.position, destination = p;
                if (p.y > 4)
                {
                    Vector2 radial = new Vector2(p.x, p.z).normalized * 17.8f;
                    destination.x = radial.x; destination.z = radial.y;
                }
                else if (p.x > 90 && p.x < 110) { destination.x = p.x < 100 ? 91.6f : 108.4f; destination.z = 6.2f; }
                else if (p.x > 190 && p.x < 210) { destination.x = p.x < 200 ? 193.4f : 206.6f; destination.z = -3.5f; }
                Undo.RecordObject(t, "Move planter"); t.position += destination - p;
            }
        }
        static void RefineStudyFurniture()
        {
            foreach (var t in All().Where(t => t.parent && t.parent.name == "Study Furniture" && t.name.StartsWith("Study Table ")))
            { Undo.RecordObject(t, "Refine table proportions"); t.localScale = new Vector3(.94f, 1f, 1.05f); }
            foreach (var t in All().Where(t => t.parent && t.parent.name == "Study Furniture" && t.name.StartsWith("Study Chair ") && !t.name.Contains("Seat")))
            { Undo.RecordObject(t, "Refine chair proportions"); t.localScale = new Vector3(.96f, 1f, .96f); }
        }
        static void RefineFloor(Transform parent)
        {
            var floor = All().Select(t => t.GetComponent<MeshFilter>()).FirstOrDefault(f => f && f.sharedMesh && f.sharedMesh.name == "LIB_Library__floor__SOLID");
            if (!floor || !floor.GetComponent<Renderer>()) throw new Exception("Lobby floor mesh missing");
            var old = floor.GetComponent<Renderer>().sharedMaterial; string path = Materials + "lobby_refined_floor.mat";
            var refined = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!refined) { refined = new Material(old); AssetDatabase.CreateAsset(refined, path); }
            refined.color = new Color(.85f, .80f, .75f); refined.SetFloat("_Glossiness", .14f); EditorUtility.SetDirty(refined);
            Undo.RecordObject(floor.GetComponent<Renderer>(), "Refine lobby floor"); floor.GetComponent<Renderer>().sharedMaterial = refined;
            var rugMat = MaterialAsset("quiz_rug_navy", new Color(.12f,.18f,.21f), 0f, .12f);
            foreach (float x in new[] {197.2f, 202.8f})
            {
                var rug = GameObject.CreatePrimitive(PrimitiveType.Cylinder); rug.name = "Quiz seating rug"; rug.transform.SetParent(parent, false);
                rug.transform.position = new Vector3(x, .012f, 1.1f); rug.transform.localScale = new Vector3(3.1f, .006f, 3.0f);
                rug.GetComponent<Renderer>().sharedMaterial = rugMat; UnityEngine.Object.DestroyImmediate(rug.GetComponent<Collider>());
            }
        }
        static Vector3 At(float radius, float degrees, float y)
        { float a = degrees * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(a) * radius, y, Mathf.Cos(a) * radius); }
        static void StairColliders(Transform parent)
        {
            var group = new GameObject("Stair handrail collisions"); group.transform.SetParent(parent, false);
            const float rise = 4.8f / 27f;
            for (int flight = 0; flight < 2; flight++) for (int i = 0; i < 27; i++)
            {
                float start = -150f + 210f * flight + 210f * i / 27f;
                float end = start + 210f / 27f;
                float top = flight * 4.8f + (i + 1) * rise;
                Vector3 a = At(11.83f, start, top), b = At(11.83f, end, top);
                var segment = new GameObject("Stair rail barrier " + (flight * 27 + i + 1).ToString("00")); segment.transform.SetParent(group.transform, false);
                segment.transform.position = (a + b) * .5f + Vector3.up * .44f;
                segment.transform.rotation = Quaternion.LookRotation(b - a, Vector3.up);
                var box = segment.AddComponent<BoxCollider>(); box.size = new Vector3(.11f, 1.18f, Vector3.Distance(a,b) + .04f);
            }
        }
        static void Rod(Transform parent, Vector3 a, Vector3 b, float radius, Material mat, string name)
        {
            var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder); rod.name = name; rod.transform.SetParent(parent, false);
            rod.transform.position = (a + b) * .5f; rod.transform.up = (b - a).normalized;
            rod.transform.localScale = new Vector3(radius * 2f, Vector3.Distance(a, b) * .5f, radius * 2f);
            rod.GetComponent<Renderer>().sharedMaterial = mat; UnityEngine.Object.DestroyImmediate(rod.GetComponent<Collider>());
        }
        static void Chandelier(Transform parent, Material metal, Material bulb)
        {
            var root = new GameObject("Grand double-ring chandelier"); root.transform.SetParent(parent, false);
            const int segments = 32; const float upper = 12.65f, lower = 11.85f;
            for (int i = 0; i < segments; i++)
            {
                float a = 360f * i / segments, b = 360f * (i + 1) / segments;
                Rod(root.transform, At(2.65f,a,upper), At(2.65f,b,upper), .055f, metal, "Upper brass ring");
                Rod(root.transform, At(3.4f,a,lower), At(3.4f,b,lower), .065f, metal, "Lower brass ring");
                if (i % 4 == 0) Rod(root.transform, At(2.65f,a,upper), At(3.4f,a,lower), .035f, metal, "Ring support");
                if (i % 8 == 0) Rod(root.transform, At(2.65f,a,upper), At(2.65f,a,15.0f), .028f, metal, "Suspension");
                if (i % 2 == 0)
                {
                    var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere); lamp.name = "Warm glass lamp"; lamp.transform.SetParent(root.transform, false);
                    lamp.transform.position = At(3.4f,a,lower+.12f); lamp.transform.localScale = new Vector3(.26f,.32f,.26f);
                    lamp.GetComponent<Renderer>().sharedMaterial = bulb; UnityEngine.Object.DestroyImmediate(lamp.GetComponent<Collider>());
                }
            }
            var lightObject = new GameObject("Chandelier warm light"); lightObject.transform.SetParent(root.transform, false); lightObject.transform.position = new Vector3(0, 10.7f, 0);
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Point; light.color = new Color(1f,.79f,.56f); light.intensity = 1.0f; light.range = 18f; light.shadows = LightShadows.None;
        }
        [MenuItem("Campus/Polish rooms, railings and clock lighting")]
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != ScenePath || scene.isDirty) throw new Exception("Open the saved living library scene and stop Play before applying the update.");
            if (GameObject.Find("Library interior update")) throw new Exception("The update has already been applied.");
            foreach (string n in new[] {"Bookcase 01", "Bookcase 02", "Room Bookcase 01", "Room Bookcase 02", "Room Bookcase 03", "Room Bookcase 04", "Skylight"}) One(n);
            if (!AssetDatabase.IsValidFolder("Assets/LibraryCharacter/Backups")) AssetDatabase.CreateFolder("Assets/LibraryCharacter", "Backups");
            string backup = AssetDatabase.GenerateUniqueAssetPath("Assets/LibraryCharacter/Backups/GrandLibrary_before_interior_update.unity");
            if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new Exception("Could not save the scene backup.");
            int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Update library interiors");
            try
            {
                var root = new GameObject("Library interior update"); Undo.RegisterCreatedObjectUndo(root, "Update library interiors");
                MoveShelf("Bookcase 01", "Bookcase Props ", new Vector3(-8.0f,0,-16.1f), -153.6f);
                MoveShelf("Bookcase 02", "Bookcase Props ", new Vector3(8.0f,0,-16.1f), 153.6f);
                MoveShelf("Room Bookcase 01", "Room Books ", new Vector3(91.05f,0,.5f), -90f);
                MoveShelf("Room Bookcase 02", "Room Books ", new Vector3(108.95f,0,.5f), 90f);
                MoveShelf("Room Bookcase 03", "Room Books ", new Vector3(193.05f,0,.5f), -90f);
                MoveShelf("Room Bookcase 04", "Room Books ", new Vector3(206.95f,0,.5f), 90f);
                MovePlanters(); RefineStudyFurniture(); RefineFloor(root.transform); StairColliders(root.transform);
                Chandelier(root.transform, MaterialAsset("chandelier_brass", new Color(.55f,.37f,.16f), .75f,.42f), MaterialAsset("chandelier_warm_glass", new Color(1f,.78f,.45f), 0,.28f,true));
                var clock = root.AddComponent<LocalTimeLighting>(); clock.sun = One("Skylight").GetComponent<Light>();
                clock.interiorLights = All().Select(t => t.GetComponent<Light>()).Where(l => l && l.type == LightType.Point && l.transform.position.x < 250f).ToArray();
                EditorSceneManager.MarkSceneDirty(scene); if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Could not save the updated scene.");
                AssetDatabase.SaveAssets(); Debug.Log("LIBRARY_INTERIOR_UPDATE_OK backup=" + backup);
            }
            catch { Undo.RevertAllDownToGroup(undo); throw; }
        }
    }
}
