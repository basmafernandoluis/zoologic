using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Zoologic.EditorTools
{
    public static class BuildAPK
    {
        private const string ApkPath = "Builds/ZooLogic_v0.7.apk";
        private const string AabPath = "Builds/ZooLogic_v0.7.aab";

        // https://developer.android.com/studio/publish/app-signing
        private const string KeystorePath = "Assets/play store/memorymatrix.keystore";
        private const string KeystorePass = "123456";
        private const string KeyAlias = "memorymatrix";
        private const string KeyAliasPass = "123456";

        private const string IconPath = "Assets/myicon.png";
        private const string IconFallbackPath = "Assets/myicon.jpg";
        private const string SplashPath = "Assets/Resources/UI/splash_android.png";

        private static readonly string[] ScenePaths =
        {
            "Assets/Scenes/Splash.unity",
            "Assets/Scenes/MainMenu.unity",
            "Assets/Scenes/LevelMap.unity",
            "Assets/Scenes/TestGrid.unity"
        };


        [MenuItem("Tools/Zoo Logic/Apply App Icon")]
        public static void ApplyAppIcon()
        {
            EnsureIconImportSettings();
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath)
                ?? AssetDatabase.LoadAssetAtPath<Texture2D>(IconFallbackPath);
            if (tex == null)
            {
                Debug.LogError("[Icon] Texture non trouvée : " + IconPath + " ni " + IconFallbackPath);
                return;
            }

#pragma warning disable CS0618 // SetIconsForTargetGroup conserv\u00e9 : comportement legacy + adaptive
            PlayerSettings.SetIconsForTargetGroup(
                BuildTargetGroup.Android,
                new[] { tex, tex, tex });
#pragma warning restore CS0618
            // SaveAssets() global interdit : il reecrit les prefabs immuables
            // d'AdMob (PlaceholderAds). On ne persiste que les PlayerSettings.
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset"))
            {
                if (o != null && EditorUtility.IsDirty(o))
                    AssetDatabase.SaveAssetIfDirty(o);
            }
            AssetDatabase.Refresh();
            Debug.Log("[Icon] Ic\u00f4ne appliqu\u00e9e \u00e0 Android (legacy + adaptive).");
        }

        /// <summary>
        /// L'icône Android doit être non compressée (sinon le build prévient que
        /// la qualité sera dégradée). Appliqué sur notre asset mutable uniquement.
        /// Vide aussi l'override de plateforme Android (qui forçait ETC2).
        /// </summary>
        private static void EnsureIconImportSettings()
        {
            var importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("[Icon] Importer introuvable : " + IconPath);
                return;
            }
            bool dirty = false;
            if (importer.textureType != TextureImporterType.Default) { importer.textureType = TextureImporterType.Default; dirty = true; }
            if (!importer.sRGBTexture) { importer.sRGBTexture = true; dirty = true; }
            if (!importer.mipmapEnabled) { importer.mipmapEnabled = true; dirty = true; }
            if (importer.maxTextureSize < 1024) { importer.maxTextureSize = 1024; dirty = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
            if (importer.GetPlatformTextureSettings("Android", out int maxSize, out TextureImporterFormat format, out int quality, out bool overridden) && overridden)
            {
                importer.ClearPlatformTextureSettings("Android");
                dirty = true;
            }
            Debug.Log($"[Icon] Import settings: type={importer.textureType} compression={importer.textureCompression} maxSize={importer.maxTextureSize}");
            if (dirty)
            {
                importer.SaveAndReimport();
                Debug.Log("[Icon] Import myicon.png forcé en non-compressé (RGBA32).");
            }
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
            PlayerSettings.bundleVersion = "0.7";
            PlayerSettings.Android.bundleVersionCode = 7;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.appwizards.zoologic");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel36;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            try { PlayerSettings.Android.resizeableActivity = true; Debug.Log("[Build] resizableWindow=true for large screens"); } catch { }
            try
            {
                var so = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
                var p = so.FindProperty("AndroidResolverSettings");
                // no-op, ensure manifest will have resizeableActivity true via resizableWindow
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            catch { }
            PlayerSettings.Android.preferredInstallLocation = AndroidPreferredInstallLocation.Auto;

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
                new EditorBuildSettingsScene(ScenePaths[2], true),
                new EditorBuildSettingsScene(ScenePaths[3], true)
            };
            // Pas de SaveAssets() global : cf. ApplyAppIcon (prefabs AdMob immuables).
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")
                .Concat(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/EditorBuildSettings.asset")))
            {
                if (o != null && EditorUtility.IsDirty(o))
                    AssetDatabase.SaveAssetIfDirty(o);
            }

            try
            {
                var t = System.Type.GetType("UnityEditor.Build.Profile.BuildProfile, UnityEditor.Build.Profile");
                if (t != null)
                {
                    var m = t.GetMethod("GetActiveBuildProfile", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
                    var p = m?.Invoke(null, null);
                    if (p != null)
                    {
                        var prop = t.GetProperty("scenes");
                        if (prop != null)
                        {
                            var arr = System.Array.CreateInstance(prop.PropertyType.GetElementType(), ScenePaths.Length);
                            for (int i = 0; i < ScenePaths.Length; i++)
                            {
                                var sceneInfoType = prop.PropertyType.GetElementType();
                                var ctor = sceneInfoType.GetConstructor(new[] { typeof(string), typeof(bool) });
                                object info = ctor != null ? ctor.Invoke(new object[] { ScenePaths[i], true }) : System.Activator.CreateInstance(sceneInfoType);
                                if (ctor == null)
                                {
                                    var pathProp = sceneInfoType.GetProperty("path");
                                    var enabledProp = sceneInfoType.GetProperty("enabled");
                                    pathProp?.SetValue(info, ScenePaths[i]);
                                    enabledProp?.SetValue(info, true);
                                }
                                arr.SetValue(info, i);
                            }
                            prop.SetValue(p, arr);
                            Debug.Log("[Build] Active BuildProfile scenes synchronisés (" + ScenePaths.Length + ")");
                        }
                    }
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[Build] BuildProfile sync ignoré: " + e.Message); }

            Debug.Log("[Build] Backend: " + PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android));
        }

        private static void ApplyAndroidSplash()
        {
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            try { PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark; } catch {}
            Debug.Log("[Splash] Splash désactivé - Made by Unity retiré, démarrage direct MainMenu");
        }

        /// <summary>
        /// Bruit connu et non-fatal : Unity tente de sauvegarder les prefabs
        /// PlaceholderAds du package AdMob (dossier immuable) marqués dirty à
        /// l'import. Le build continue et réussit (vérifié : APK produit).
        /// On le déclasse pour ne pas masquer les vraies erreurs.
        /// </summary>
        private static bool IsBenignPackageNoise(string content)
        {
            return !string.IsNullOrEmpty(content)
                && content.Contains("immutable folder")
                && content.Contains("PlaceholderAds");
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
                    if (msg.type == LogType.Error && IsBenignPackageNoise(msg.content)) Debug.Log("[BUILD-INFO] (bénin, build non bloqué) " + msg.content);
                    else if (msg.type == LogType.Error) Debug.LogError("[BUILD-ERR] " + msg.content);
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
