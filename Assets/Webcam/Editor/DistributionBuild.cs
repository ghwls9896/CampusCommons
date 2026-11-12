using System;
using System.IO;
using System.Threading;
using CampusCommons.Account;
using CampusCommons.Quiz;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CampusCommons.Webcam.Editor
{
    public sealed class DistributionBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        const string Package = "Assets/StreamingAssets/StudyRuntime";
        public static void ValidateConnections(string account, string quiz)
        {
            if (string.IsNullOrWhiteSpace(account) || JsonUtility.FromJson<AccountConnection>(account)?.IsValid() != true)
                throw new BuildFailedException("Valid AccountConnection.json is required in Assets/Account/Resources.");
            if (string.IsNullOrWhiteSpace(quiz) || JsonUtility.FromJson<QuizDeployment>(quiz)?.IsValid() != true)
                throw new BuildFailedException("Valid private QuizConnection.json is required in Assets/QuizRoom/Resources. Do not commit its token.");
        }
        public void OnPreprocessBuild(BuildReport report)
        {
            string platform = report.summary.platform == BuildTarget.StandaloneOSX ? "macos-arm64" :
                report.summary.platform == BuildTarget.StandaloneWindows64 ? "windows-x64" : null;
            if (platform == null) throw new BuildFailedException("This distribution supports macOS ARM64 and Windows x64.");
#if UNITY_EDITOR_OSX
            if (report.summary.platform == BuildTarget.StandaloneOSX && UnityEditor.OSXStandalone.UserBuildSettings.architecture != OSArchitecture.ARM64)
                throw new BuildFailedException("The macOS runtime requires ARM64. Use CampusCommons > Distribution > Build macOS.");
#endif
            ValidateConnections(Resources.Load<TextAsset>("AccountConnection")?.text, Resources.Load<TextAsset>("QuizConnection")?.text);
            try
            {
                var manifest = JsonUtility.FromJson<RuntimePackageManifest>(File.ReadAllText(Path.Combine(Package, "manifest.json")));
                RuntimePackageInstaller.Validate(manifest, platform);
                if (platform == "macos-arm64" && manifest.minimumOS != "15.0") throw new InvalidDataException("macOS runtime minimum must be 15.0.");
                string archive = Path.Combine(Package, manifest.archive);
                if (new FileInfo(archive).Length != manifest.archiveBytes || RuntimePackageInstaller.HashFile(archive, CancellationToken.None) != manifest.sha256)
                    throw new InvalidDataException("Runtime package checksum mismatch.");
            }
            catch (Exception e) { throw new BuildFailedException("Prepare the matching runtime with Tools/distribution/prepare_runtime.py before building. " + e.GetType().Name); }
        }
        [MenuItem("CampusCommons/Distribution/Build macOS (Apple Silicon)")]
        public static void Mac()
        {
#if UNITY_EDITOR_OSX
            var previous = UnityEditor.OSXStandalone.UserBuildSettings.architecture;
            try { UnityEditor.OSXStandalone.UserBuildSettings.architecture = OSArchitecture.ARM64; Build(BuildTarget.StandaloneOSX, "Builds/CampusCommons-macos-arm64/CampusCommons.app"); }
            finally { UnityEditor.OSXStandalone.UserBuildSettings.architecture = previous; }
#else
            throw new BuildFailedException("Build the macOS distribution on an Apple Silicon Mac.");
#endif
        }
        [MenuItem("CampusCommons/Distribution/Build Windows (x64)")]
        public static void Windows() => Build(BuildTarget.StandaloneWindows64, "Builds/CampusCommons-windows-x64/CampusCommons.exe");
        static void Build(BuildTarget target, string path)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
                throw new BuildFailedException("Install the matching Standalone Build Support module in Unity Hub.");
            var previous = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            string previousCamera = PlayerSettings.iOS.cameraUsageDescription;
            string previousMinimum = MacMinimum(null);
            try
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
                PlayerSettings.iOS.cameraUsageDescription = "좌석에 앉으면 미리보기를 켜고, 시작을 누르면 이 기기에서만 공부 상태를 판별합니다. 영상은 저장하거나 외부로 전송하지 않습니다.";
                if (target == BuildTarget.StandaloneOSX) MacMinimum("15.0");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { "Assets/StudyImmersive/Scenes/GrandLibrary_Living.unity" },
                    locationPathName = path, target = target, options = BuildOptions.None });
                if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Distribution build failed: " + report.summary.result);
                if (target == BuildTarget.StandaloneOSX) VerifyMacPlist(path);
                Debug.Log("DESKTOP_DISTRIBUTION_BUILD_PASS platform=" + target + " bytes=" + report.summary.totalSize);
            }
            finally { PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, previous); PlayerSettings.iOS.cameraUsageDescription = previousCamera; MacMinimum(previousMinimum); }
        }
        static void VerifyMacPlist(string app)
        {
            var info = new System.Diagnostics.ProcessStartInfo("/usr/bin/plutil", "-lint \"" + Path.GetFullPath(Path.Combine(app, "Contents/Info.plist")) + "\"")
                { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var process = System.Diagnostics.Process.Start(info))
            {
                process.StandardOutput.ReadToEnd(); process.StandardError.ReadToEnd(); process.WaitForExit();
                if (process.ExitCode != 0) throw new BuildFailedException("macOS rejected the generated Info.plist.");
            }
        }
        // Unity serializes this macOS setting but does not expose a public typed setter.
        static string MacMinimum(string value)
        {
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var property = settings.FindProperty("macOSTargetOSVersion");
            if (property == null) throw new BuildFailedException("macOS minimum version setting unavailable.");
            string current = property.stringValue;
            if (value != null) { property.stringValue = value; settings.ApplyModifiedPropertiesWithoutUndo(); }
            return current;
        }
        public static void CheckConfigurationGate()
        {
            const string account = "{\"url\":\"https://example.supabase.co\",\"publishableKey\":\"sb_publishable_fixture_only\"}";
            const string quiz = "{\"server_url\":\"https://example.onrender.com\",\"access_token\":\"fixture\"}";
            ValidateConnections(account, quiz);
            int rejected = 0;
            foreach (var pair in new[] { new[] { "", quiz }, new[] { account, "" }, new[] { "{}", quiz }, new[] { account, "{}" } })
                try { ValidateConnections(pair[0], pair[1]); } catch (BuildFailedException) { rejected++; }
            if (rejected != 4) throw new Exception("Incomplete client configuration accepted");
            string previousMinimum = MacMinimum("15.0");
            try { if (MacMinimum(null) != "15.0") throw new Exception("Minimum macOS setting not applied"); }
            finally { MacMinimum(previousMinimum); }
            Debug.Log("DISTRIBUTION_CONFIG_GATE_PASS 6 checks");
        }
    }
}
