using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampusCommons.Editor
{
    // Execute with -batchmode -executeMethod CampusCommons.Editor.CampusValidation.Run
    // (without -quit, because the validator exits after its asynchronous Play Mode checks).
    public static class CampusValidation
    {
        static int frames;
        static bool failed;
        static bool editorSearchError;
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Campus/Scenes/CampusCommons.unity");
            SessionState.SetBool("CampusValidationPending",true);
            EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        static void Resume()
        {
            EditorApplication.delayCall += () =>
            {
                if (!SessionState.GetBool("CampusValidationPending",false)) return;
                frames=0; failed=false; editorSearchError=false;
                Application.logMessageReceived += Observe;
                EditorApplication.update += Tick;
            };
        }
        static void Observe(string condition,string stack,LogType type)
        {
            // The installed editor's Search startup can fail in headless mode before
            // creating its user index. Record that editor-only issue separately.
            if (stack.Contains("UnityEditor.Search.SearchDatabase")) { editorSearchError=true; return; }
            if (type==LogType.Exception || type==LogType.Error) failed=true;
        }
        static void Check(bool ok,string message)
        {
            if (!ok) throw new Exception(message);
        }
        static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
            ++frames;
            // Editor updates can run many times before a single game frame.
            if (Time.frameCount<40 || Time.timeSinceLevelLoad<2f) return;
            EditorApplication.update -= Tick;
            try
            {
                Check(!failed,"Runtime error before checks");
                var player=UnityEngine.Object.FindFirstObjectByType<CampusPlayer>();
                Check(player,"Player spawned");
                Check(Camera.main && Camera.main.isActiveAndEnabled,"Player camera active");
                var cc=player.GetComponent<CharacterController>();
                Check(cc.isGrounded,"Player is grounded at "+player.transform.position+" after "+Time.timeSinceLevelLoad+" seconds");
                float start=player.transform.position.z;
                for (int i=0;i<60;i++) cc.Move(new Vector3(0,-.02f,.06f));
                Check(player.transform.position.z>start+3,"Character moves through the lounge");
                var seats=UnityEngine.Object.FindObjectsByType<CampusSeat>(FindObjectsSortMode.None);
                Check(seats.Length==43,"43 seat anchors created");
                CampusSeat chosen=Array.Find(seats,s=>s.name.StartsWith("Quiz seat 01"));
                Check(chosen,"Quiz seat exists");
                cc.enabled=false;
                Vector3 stand=new Vector3(chosen.transform.position.x+1,.03f,chosen.transform.position.z);
                player.transform.position=stand; cc.enabled=true;
                Check(player.TrySit(chosen),"Nearby chair can be occupied");
                Check(chosen.occupant==player && !cc.enabled,"Seated occupancy and collision state");
                player.Stand();
                Check(!chosen.occupant && cc.enabled && Vector3.Distance(player.transform.position,stand)<.01f,"Standing restores the free chair and position");
                // Both doorways must permit an actual capsule sweep through the shared wall.
                foreach (float x in new[] {-9f,9f})
                {
                    cc.enabled=false; player.transform.position=new Vector3(x,.05f,12.3f); cc.enabled=true;
                    for (int i=0;i<60;i++) cc.Move(new Vector3(0,-.01f,.065f));
                    Check(player.transform.position.z>15.8f,"Doorway passable at x="+x);
                }
                Directory.CreateDirectory("Design");
                File.WriteAllText("Design/unity-validation.txt","PASS\nUnity "+Application.unityVersion+"\nPlay Mode: player spawn, grounded capsule movement, 43 seats, sit/stand occupancy, both doorways.\nInput gestures and remote multiplayer were not tested.\nEditor Search startup exception observed separately: "+editorSearchError+"\n");
                Debug.Log("CAMPUS_PLAYMODE_VALIDATION_PASS");
                Finish(0);
            }
            catch(Exception ex) { Debug.LogException(ex); Finish(1); }
        }
        static void Finish(int code)
        {
            SessionState.SetBool("CampusValidationPending",false);
            Application.logMessageReceived -= Observe;
            EditorApplication.Exit(code);
        }
        public static void RenderPreview()
        {
            EditorSceneManager.OpenScene("Assets/Campus/Scenes/CampusCommons.unity");
            var cameraGo=new GameObject("Preview camera");
            var camera=cameraGo.AddComponent<Camera>();
            camera.orthographic=true; camera.orthographicSize=33;
            camera.transform.position=new Vector3(48,57,-56);
            camera.transform.LookAt(new Vector3(0,0,7));
            camera.backgroundColor=new Color(.16f,.21f,.2f); camera.clearFlags=CameraClearFlags.SolidColor;
            camera.farClipPlane=200;
            QualitySettings.shadowDistance=150;
            QualitySettings.shadows=ShadowQuality.All;
            QualitySettings.shadowResolution=ShadowResolution.High;
            var rt=new RenderTexture(1600,1300,24) { antiAliasing=4 }; camera.targetTexture=rt; camera.Render();
            RenderTexture.active=rt;
            var texture=new Texture2D(1600,1300,TextureFormat.RGB24,false);
            texture.ReadPixels(new Rect(0,0,1600,1300),0,0); texture.Apply();
            File.WriteAllBytes("Design/Unity-overview.png",texture.EncodeToPNG());
            camera.targetTexture=null; RenderTexture.active=null;
            UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(cameraGo);
            Debug.Log("CAMPUS_UNITY_RENDER_PASS");
        }
        public static void GenerateAndRender()
        {
            CampusSceneBuilder.Generate();
            RenderPreview();
        }
    }
}
