using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Zoologic.EditorTools
{
    public static class BuildAPK
    {
        private const string ApkPath = "Builds/ZooLogic_v0.2.apk";
        private const string AabPath = "Builds/ZooLogic_v0.2.aab";

        // https://developer.android.com/studio/publish/app-signing
        private const string KeystorePath = "Assets/play store/memorymatrix.keystore";
        private const string KeystorePass = "123456";
        private const string KeyAlias = "memorymatrix";
        private const string KeyAliasPass = "123456";

        private const string IconPath = "Assets/myicon.jpg";
        private const string SplashPath = "Assets/Resources/UI/splash_android.png";

        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/LevelMap.unity",
            "Assets/Scenes/TestGrid.unity"
        };

        [MenuItem("Tools/Zoo Logic/Apply App Icon")]
        public static void ApplyAppIcon()
        {
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (tex == null)
            {
                Debug.LogError("[Icon] Texture non trouv\u00e9e : " + IconPath);
                return;
            }

#pragma warning disable CS0618 // SetIconsForTargetGroup conserv\u00e9 : comportement legacy + adaptive
            PlayerSettings.SetIconsForTargetGroup(
                BuildTargetGroup.Android,
                new[] { tex, tex, tex });
#pragma warning restore CS0618
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Icon] Ic\u00f4ne appliqu\u00e9e \u00e0 Android (legacy + adaptive).");
        }

        [MenuItem("Tools/Zoo Logic/Build Android APK (Test Ads)")]
        public static void BuildAndroid()
        {
            SetAdMobTestDefines(true);
            PrepareAndroidBuild();
            LogResult(BuildPipeline.BuildPlayer(ScenePaths, ApkPath, BuildTarget.Android, BuildOptions.None), "APK-TEST");
        }

        [MenuItem("Tools/Zoo Logic/Build Android AAB (Prod Ads)")]
        public static void BuildAndroidAAB()
        {
            SetAdMobTestDefines(false);
            PrepareAndroidBuild();
            EditorUserBuildSettings.buildAppBundle = true;
            LogResult(BuildPipeline.BuildPlayer(ScenePaths, AabPath, BuildTarget.Android, BuildOptions.None), "AAB-PROD");
            EditorUserBuildSettings.buildAppBundle = false;
        }

        private static void SetAdMobTestDefines(bool test)
        {
            var target = UnityEditor.Build.NamedBuildTarget.Android;
            string defines = PlayerSettings.GetScriptingDefineSymbols(target);
            var list = new System.Collections.Generic.HashSet<string>(defines.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries));
            if (test) list.Add("ADMOB_TEST");
            else list.Remove("ADMOB_TEST");
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", list));
            Debug.Log($"[AdMob] Defines ADMOB_TEST={(test ? "ON (test IDs)" : "OFF (prod IDs: ca-app-pub-7435856398879419)")}");}


        private static void PrepareAndroidBuild()
        {
            Debug.Log("=== ZOO LOGIC ANDROID BUILD PREP ===");

            PlayerSettings.companyName = "AppWizards";
            PlayerSettings.productName = "Zoo Logic";
            PlayerSettings.bundleVersion = "0.2";
            PlayerSettings.Android.bundleVersionCode = 2;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.appwizards.zoologic");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel35;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            ApplyAppIcon();
            ApplyAndroidSplash();

            // Signature release/upload (obligatoire : Play rejette les AAB sign�s avec la cl� debug).
            if (!File.Exists(KeystorePath))
            {
                Debug.LogError("[Sign] Keystore introuvable : " + KeystorePath);
                EditorUtility.DisplayDialog("Zoo Logic", "Keystore introuvable :\n" + KeystorePath, "OK");
            }
            else
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = KeystorePath;
                PlayerSettings.Android.keystorePass = KeystorePass;
                PlayerSettings.Android.keyaliasName = KeyAlias;
                PlayerSettings.Android.keyaliasPass = KeyAliasPass;
                Debug.Log("[Sign] Cl\u00e9 de signature : " + KeyAlias + " (" + KeystorePath + ")");
            }

            SerializedObject settings = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            SerializedProperty inputHandler = settings.FindProperty("activeInputHandler");
            if (inputHandler != null && inputHandler.intValue != 1)
            {
                inputHandler.intValue = 1;
                settings.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[Build] Set Active Input Handling to Input System Package (New) - Android single handler for InputSystemUIInputModule");
            }

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePaths[0], true),
                new EditorBuildSettingsScene(ScenePaths[1], true),
                new EditorBuildSettingsScene(ScenePaths[2], true)
            };

            Debug.Log("[Build] Backend: " + PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android));
        }

        private static void ApplyAndroidSplash()
        {
            Sprite splash = AssetDatabase.LoadAssetAtPath<Sprite>(SplashPath);
            if (splash == null)
            {
                Debug.LogWarning("[Splash] Sprite non trouv\u00e9 : " + SplashPath);
                return;
            }
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.background = splash;
            PlayerSettings.SplashScreen.backgroundPortrait = splash;
            PlayerSettings.SplashScreen.backgroundColor = new Color(246f / 255f, 196f / 255f, 106f / 255f, 1f);
            Debug.Log("[Splash] Splash Android appliqu\u00e9 : " + SplashPath);
        }

        private static void LogResult(BuildReport report, string kind)
        {
            BuildSummary summary = report.summary;

            Debug.Log("=== BUILD " + kind + " RESULTS ===");
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
                long bytes = File.Exists(summary.outputPath) ? new FileInfo(summary.outputPath).Length : (long)summary.totalSize;
                Debug.Log("=== BUILD " + kind + " SUCCEEDED ===");
                Debug.Log(kind + ": " + summary.outputPath + " (" + (bytes / (1024.0 * 1024.0)).ToString("F2") + " MB)");
            }
            else
            {
                Debug.LogError("=== BUILD " + kind + " FAILED ===");
            }
        }
    }
}
