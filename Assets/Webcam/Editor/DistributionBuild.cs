using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using CampusCommons.Account;
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
        public static void ValidateAccountConnection(string account)
        {
            if (string.IsNullOrWhiteSpace(account) || JsonUtility.FromJson<AccountConnection>(account)?.IsValid() != true)
                throw new BuildFailedException("Valid AccountConnection.json is required in Assets/Account/Resources.");
        }
        static void ValidateQuizResources(IEnumerable<string> assetPaths)
        {
            foreach (string path in assetPaths)
            {
                if (path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    string.Equals(Path.GetFileName(path), "QuizConnection.json", StringComparison.OrdinalIgnoreCase))
                    throw new BuildFailedException("Move the legacy QuizConnection.json outside Unity Resources before building: " + path);
            }
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
            ValidateQuizResources(AssetDatabase.GetAllAssetPaths());
            ValidateAccountConnection(Resources.Load<TextAsset>("AccountConnection")?.text);
            try
            {
                var manifest = JsonUtility.FromJson<RuntimePackageManifest>(File.ReadAllText(Path.Combine(Package, "manifest.json")));
                RuntimePackageInstaller.Validate(manifest, platform);
                if (manifest.studyProtocol != 1)
                    throw new InvalidDataException("Combined study runtime package required.");
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
                if (target == BuildTarget.StandaloneOSX) FinalizeMacBundle(path);
                Debug.Log("DESKTOP_DISTRIBUTION_BUILD_PASS platform=" + target + " bytes=" + report.summary.totalSize);
            }
            finally { PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, previous); PlayerSettings.iOS.cameraUsageDescription = previousCamera; MacMinimum(previousMinimum); }
        }
        static void FinalizeMacBundle(string app)
        {
            string bundle = "\"" + Path.GetFullPath(app) + "\"";
            RunBundleTool("/usr/bin/plutil", "-lint \"" + Path.GetFullPath(Path.Combine(app, "Contents/Info.plist")) + "\"");
            // The camera description is written after Unity's initial ad-hoc signature.
            RunBundleTool("/usr/bin/codesign", "--force --sign - " + bundle);
            RunBundleTool("/usr/bin/codesign", "--verify --deep --strict " + bundle);
        }
        static void RunBundleTool(string executable, string arguments)
        {
            var info = new System.Diagnostics.ProcessStartInfo(executable, arguments)
                { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var process = System.Diagnostics.Process.Start(info))
            {
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new BuildFailedException(executable + " failed. " + error.GetAwaiter().GetResult() + output.GetAwaiter().GetResult());
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
            ValidateAccountConnection(account);
            int rejected = 0;
            foreach (var invalid in new[] { "", "{}", account.Replace("https://", "http://"), account.Replace("sb_publishable_fixture_only", "invalid") })
                try { ValidateAccountConnection(invalid); } catch (BuildFailedException) { rejected++; }
            if (rejected != 4) throw new Exception("Incomplete client configuration accepted");
            ValidateQuizResources(new[] { "Assets/QuizRoom/Resources/QuizConnection.example.json",
                "Assets/Account/Resources/AccountConnection.json" });
            foreach (string legacy in new[] { "Assets/QuizRoom/Resources/QuizConnection.json",
                "Assets/Other/Resources/Legacy/quizconnection.JSON" })
                try { ValidateQuizResources(new[] { legacy }); } catch (BuildFailedException) { rejected++; }
            if (rejected != 6) throw new Exception("Legacy quiz token resource accepted");
            string previousMinimum = MacMinimum("15.0");
            try { if (MacMinimum(null) != "15.0") throw new Exception("Minimum macOS setting not applied"); }
            finally { MacMinimum(previousMinimum); }
            Debug.Log("DISTRIBUTION_CONFIG_GATE_PASS 6 checks");
        }
    }
}
