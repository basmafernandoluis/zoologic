using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zoologic.EditorTools
{
    /// <summary>
    /// Crée la scène "Splash" (écran de lancement, index 0 du build) :
    /// caméra, canvas UI, EventSystem et une racine portant SplashController.
    /// Menu : Tools / Zoologic / Créer la scène Splash.
    /// </summary>
    public static class SplashSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Splash.unity";

        [MenuItem("Tools/Zoologic/Créer la scène Splash")]
        public static void CreateSplashScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGameObject = new GameObject("Main Camera");
            cameraGameObject.tag = "MainCamera";
            var camera = cameraGameObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.96f, 0.68f, 0.25f, 1f); // orange du key-art
            cameraGameObject.AddComponent<AudioListener>();

            var canvasGameObject = new GameObject(
                "UICanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGameObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGameObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            var eventSystemGameObject = new GameObject("EventSystem", typeof(EventSystem));
            eventSystemGameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            var splashRoot = new GameObject("SplashRoot");
            splashRoot.AddComponent<SplashController>();

            EnsureFolder("Assets/Scenes");
            SauvegarderSceneResiliente(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            ReorderBuildSettingsFirst();

            Debug.Log("[Zoologic] Scène Splash créée : " + ScenePath);
            EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>
        /// Sauvegarde en tolérant le bruit connu et non-fatal : Unity tente de
        /// sauvegarder les prefabs PlaceholderAds d'AdMob (dossier immuable
        /// Packages/) marqués dirty à l'import — cf. BuildAPK.IsBenignPackageNoise.
        /// La scène elle-même est bien écrite malgré ces erreurs : on le vérifie.
        /// </summary>
        private static void SauvegarderSceneResiliente(UnityEngine.SceneManagement.Scene scene, string path)
        {
            bool saved = false;
            try
            {
                saved = EditorSceneManager.SaveScene(scene, path);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Zoologic] Splash : erreur pendant SaveScene (bruit AdMob probable, vérification en cours) : " + e.Message);
            }
            if (!saved && !System.IO.File.Exists(path))
                throw new System.Exception("[Zoologic] Splash : échec réel de sauvegarde, fichier absent : " + path);
            Debug.Log("[Zoologic] Splash : scène sauvegardée (les erreurs 'immutable folder / PlaceholderAds' sont bénignes et connues).");
        }

        /// <summary>Le splash démarre le jeu : garanti en index 0.</summary>
        private static void ReorderBuildSettingsFirst()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.Sort((a, b) => GetSceneOrder(a.path).CompareTo(GetSceneOrder(b.path)));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static int GetSceneOrder(string path)
        {
            if (path.Contains("Splash")) return 0;
            if (path.Contains("MainMenu")) return 1;
            if (path.Contains("LevelMap")) return 2;
            if (path.Contains("TestGrid")) return 3;
            return 99;
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(scene => scene.path == path))
                return;

            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string parent = System.IO.Path.GetDirectoryName(folderPath).Replace('\\', '/');
            string name = System.IO.Path.GetFileName(folderPath);

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
