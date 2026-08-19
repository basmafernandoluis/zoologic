using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Zoodoku.EditorTools
{
    public static class BuildAPK
    {
        private const string ApkPath = "Builds/ZoodokuTest_v0.2.apk";

        [MenuItem("Tools/Zoodoku/Build Android APK")]
        public static void BuildAndroid()
        {
            Debug.Log("=== ZOODOKU ANDROID BUILD START ===");

            Debug.Log("[Build] Active: " + EditorUserBuildSettings.activeBuildTarget);
            Debug.Log("[Build] Arch: " + PlayerSettings.Android.targetArchitectures);
            Debug.Log("[Build] Backend: " + PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android));

            PlayerSettings.companyName = "IndieDev";
            PlayerSettings.productName = "Zoodoku Test";
            PlayerSettings.bundleVersion = "0.2";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.debutant.zoodokutest");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel35;

            SerializedObject settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            SerializedProperty inputHandler = settings.FindProperty("activeInputHandler");
            if (inputHandler != null && inputHandler.intValue != 0)
            {
                inputHandler.intValue = 0;
                settings.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[Build] Set Active Input Handling to Old (Input Manager)");
            }

            Debug.Log("[Build] Arch after set: " + PlayerSettings.Android.targetArchitectures);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/LevelMap.unity", true),
                new EditorBuildSettingsScene("Assets/Scenes/TestGrid.unity", true)
            };

            string full = Path.GetFullPath(ApkPath);
            string dir = Path.GetDirectoryName(full);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            if (File.Exists(full)) File.Delete(full);

            Debug.Log("[Build] Starting build...");
            BuildReport report = BuildPipeline.BuildPlayer(
                new[] { "Assets/Scenes/MainMenu.unity", "Assets/Scenes/LevelMap.unity", "Assets/Scenes/TestGrid.unity" },
                ApkPath, BuildTarget.Android, BuildOptions.None);
            BuildSummary summary = report.summary;

            Debug.Log("=== BUILD RESULTS ===");
            Debug.Log("Result   : " + summary.result);
            Debug.Log("Output   : " + summary.outputPath);
            Debug.Log("Size     : " + (summary.totalSize / (1024.0 * 1024.0)).ToString("F2") + " MB");
            Debug.Log("Time     : " + summary.totalTime.TotalSeconds.ToString("F1") + "s");
            Debug.Log("Warnings : " + summary.totalWarnings);
            Debug.Log("Errors   : " + summary.totalErrors);

            foreach (BuildStep step in report.steps)
                foreach (BuildStepMessage msg in step.messages)
                    if (msg.type == LogType.Error) Debug.LogError("[BUILD-ERR] " + msg.content);
                    else if (msg.type == LogType.Warning) Debug.LogWarning("[BUILD-WARN] " + msg.content);

            if (summary.result == BuildResult.Succeeded)
            {
                long bytes = File.Exists(full) ? new FileInfo(full).Length : (long)summary.totalSize;
                Debug.Log("=== BUILD SUCCEEDED ===");
                Debug.Log("APK: " + full + " (" + (bytes / (1024.0 * 1024.0)).ToString("F2") + " MB)");
            }
            else
            {
                Debug.LogError("=== BUILD FAILED ===");
            }
        }
    }
}
