using System;
using System.IO;
using CampusCommons.StudyImmersive;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampusCommons.StudyImmersive.Editor
{
    /// <summary>Run with Unity -batchmode -executeMethod CampusCommons.StudyImmersive.Editor.ConstructionBarrierVerification.Run.</summary>
    public static class ConstructionBarrierVerification
    {
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var barrier = ConstructionBarrier.Create(Vector3.zero, Quaternion.identity);
            var collider = barrier.GetComponent<BoxCollider>();
            Check(collider && Mathf.Approximately(collider.size.x, 2f), "Incorrect collider width");
            Check(Mathf.Approximately(collider.size.y, ConstructionBarrier.Height), "Incorrect collider height");
            var mesh = barrier.GetComponentInChildren<MeshFilter>().sharedMesh;
            var stripe = barrier.transform.Find("Black diagonal stripes - both sides").GetComponent<MeshFilter>().sharedMesh;
            Check(mesh && stripe && stripe.vertexCount > 6 && stripe.triangles.Length > 6, "Missing visible geometry");
            Check(stripe.bounds.min.x >= -1f && stripe.bounds.max.x <= 1f, "Stripes escape the panel");
            Physics.SyncTransforms();
            foreach (float x in new[] {-.95f, 0f, .95f})
            {
                Check(Physics.Raycast(new Vector3(x, 1f, -2f), Vector3.forward, out RaycastHit front, 4f) && front.collider == collider, "Front passage not blocked at " + x);
                Check(Physics.Raycast(new Vector3(x, 1f, 2f), Vector3.back, out RaycastHit back, 4f) && back.collider == collider, "Back passage not blocked at " + x);
            }
            Check(!Physics.Raycast(new Vector3(1.1f, 1f, -2f), Vector3.forward, 4f), "Collider extends beyond specified width");
            RenderPreview();
            UnityEngine.Object.DestroyImmediate(barrier.gameObject); Physics.SyncTransforms();
            Check(!Physics.Raycast(new Vector3(0, 1f, -2f), Vector3.forward, 4f), "Removed barrier still blocks");
            var shortBarrier = ConstructionBarrier.Create(Vector3.zero, Quaternion.identity, .1f);
            Check(Mathf.Approximately(shortBarrier.Width, 1f), "Minimum width not clamped");
            UnityEngine.Object.DestroyImmediate(shortBarrier.gameObject);
            var longBarrier = ConstructionBarrier.Create(Vector3.zero, Quaternion.identity, 20f);
            Check(Mathf.Approximately(longBarrier.Width, 12f), "Maximum width not clamped");
            UnityEngine.Object.DestroyImmediate(longBarrier.gameObject);
            Debug.Log("CONSTRUCTION_BARRIER_PASS geometry, adjustable width, both-way collision, removal");
        }
        static void RenderPreview()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.Log("CONSTRUCTION_BARRIER_PREVIEW_SKIPPED: no graphics device"); return;
            }
            var lightObject = new GameObject("Preview Light"); var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.4f; lightObject.transform.rotation = Quaternion.Euler(38f, -35f, 0f);
            var cameraObject = new GameObject("Preview Camera"); var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .14f, .16f);
            cameraObject.transform.position = new Vector3(4.4f, 2.8f, -7.2f);
            cameraObject.transform.LookAt(new Vector3(0f, 1f, 0f)); camera.fieldOfView = 36f;
            var target = new RenderTexture(960, 600, 24); camera.targetTexture = target; camera.Render();
            var before = RenderTexture.active; RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply();
            string path = Path.Combine(Path.GetTempPath(), "campus-cc13-barrier-preview.png");
            File.WriteAllBytes(path, image.EncodeToPNG()); Debug.Log("CONSTRUCTION_BARRIER_PREVIEW " + path);
            RenderTexture.active = before; camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(image); target.Release(); UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(lightObject);
        }
    }
}
