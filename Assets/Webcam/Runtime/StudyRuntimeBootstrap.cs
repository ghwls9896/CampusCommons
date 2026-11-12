using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace CampusCommons.Webcam
{
    // Preparing files never blocks account/quiz screens or the Unity main thread.
    public sealed class StudyRuntimeBootstrap : MonoBehaviour
    {
        public static StudyRuntimeBootstrap Instance { get; private set; }
        public static string Root { get; private set; }
        public static bool Packaged { get; private set; }
        public static string PythonPath { get; private set; }
        public static bool Ready => !string.IsNullOrEmpty(Root);
        public bool Preparing => job != null && !job.IsCompleted;
        public float Progress => progress;
        public string Status => error ?? status;
        public bool Failed => error != null;
        volatile float progress;
        volatile string status = "공부 인식 준비 중";
        string error, packageDirectory, installBase, platform;
        CancellationTokenSource cancel;
        Task<string> job;
        RuntimePackageManifest manifest;
        Font font; GUIStyle text;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() { Instance = null; Root = PythonPath = null; Packaged = false; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install() { if (!Instance) new GameObject("Study runtime preparation").AddComponent<StudyRuntimeBootstrap>(); }
        public static string PlatformId()
        {
            if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
                return Environment.Is64BitProcess ? "windows-x64" : "unsupported";
            if (Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor)
                return SystemInfo.processorType.IndexOf("Apple", StringComparison.OrdinalIgnoreCase) >= 0 ? "macos-arm64" : "unsupported";
            return "unsupported";
        }
        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
            platform = PlatformId(); packageDirectory = Path.Combine(Application.streamingAssetsPath, "StudyRuntime");
            installBase = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CampusCommons", "StudyRuntime");
#if UNITY_EDITOR
            // Existing development setup only; release builds never consult these paths/overrides.
            string dev = Environment.GetEnvironmentVariable("CAMPUS_QEV_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".campuscommons", "qev");
            string python = Path.Combine(dev, Environment.OSVersion.Platform == PlatformID.Win32NT ? ".venv/Scripts/python.exe" : ".venv/bin/python");
            if (Environment.GetEnvironmentVariable("CAMPUS_RUNTIME_TEST_PACKAGE") != "1" && File.Exists(python) && File.Exists(Path.Combine(dev, "app/live.py")))
            { Root = dev; PythonPath = python; progress = 1; status = "공부 인식 준비 완료"; return; }
#endif
            Begin(false);
        }
        public void Retry()
        {
            if (Preparing) return;
            var study = UnityEngine.Object.FindAnyObjectByType<LobbyStudyWebcam>();
            if (study) study.StopStudy();
            Root = PythonPath = null; Begin(true);
        }
        void Begin(bool force)
        {
            error = null; progress = 0; status = "공부 인식 준비 중";
            cancel?.Dispose(); cancel = new CancellationTokenSource();
            try
            {
                manifest = JsonUtility.FromJson<RuntimePackageManifest>(File.ReadAllText(Path.Combine(packageDirectory, "manifest.json")));
                RuntimePackageInstaller.Validate(manifest, platform);
                string archive = Path.Combine(packageDirectory, manifest.archive);
                var token = cancel.Token;
                job = Task.Run(() => RuntimePackageInstaller.Install(manifest, archive, installBase, platform,
                    (value, message) => { progress = value; status = message; }, token, force), token);
            }
            catch (Exception e) { Fail(e); }
        }
        void Update()
        {
            if (job == null || !job.IsCompleted) return;
            var complete = job; job = null;
            if (complete.IsCanceled) { error = "공부 인식 준비가 중단됐습니다. 다시 시도해 주세요."; return; }
            if (complete.IsFaulted) { Fail(complete.Exception.GetBaseException()); return; }
            Packaged = true; Root = complete.Result; PythonPath = Path.Combine(Root, manifest.python);
            progress = 1; status = "공부 인식 준비 완료";
            Debug.Log("STUDY_RUNTIME_READY platform=" + platform + " packaged=true");
        }
        void Fail(Exception e)
        {
            // Do not expose local paths or implementation details to players.
            error = e is UnauthorizedAccessException ? "공부 인식 파일을 저장할 수 없습니다. 저장 권한을 확인해 주세요." :
                e is InvalidDataException ? e.Message : "공부 인식 준비를 완료하지 못했습니다. 저장 공간을 확인한 뒤 다시 시도해 주세요.";
            Debug.LogWarning("Study runtime preparation failed: " + e.GetType().Name);
        }
        void OnApplicationQuit() { cancel?.Cancel(); }
        void OnDestroy()
        {
            cancel?.Cancel(); if (font) Destroy(font);
            if (Instance == this) Instance = null;
        }
        void OnGUI()
        {
            if (Ready) return;
            if (text == null)
            {
                font = Font.CreateDynamicFontFromOSFont(new[] { "Apple SD Gothic Neo", "Malgun Gothic", "Arial" }, 16);
                text = new GUIStyle(GUI.skin.label) { font = font, fontSize = 16, wordWrap = true };
            }
            var matrix = GUI.matrix; GUI.matrix = Matrix4x4.identity;
            float width = Mathf.Min(560, Screen.width - 24);
            var rect = new Rect((Screen.width - width) / 2, 12, width, Failed ? 120 : 104);
            GUI.Box(rect, GUIContent.none); GUILayout.BeginArea(new Rect(rect.x + 12, rect.y + 10, width - 24, rect.height - 16));
            GUILayout.Label(Status + (Preparing ? "  " + Mathf.RoundToInt(Progress * 100) + "%" : ""), text);
            GUILayout.Label("처음 한 번만 준비합니다. 로그인과 퀴즈는 먼저 이용할 수 있어요.", text);
            if (Failed && GUILayout.Button("다시 준비하기")) Retry();
            GUILayout.EndArea(); GUI.matrix = matrix;
        }
    }
}
