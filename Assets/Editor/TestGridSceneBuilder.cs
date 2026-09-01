using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zoologic.EditorTools
{
    /// <summary>
    /// Outil d'édition qui crée la scène de test "TestGrid" : caméra, canvas UI,
    /// EventSystem, et un GameRoot portant GridView + PuzzleGameController.
    ///
    /// Tout le câblage est fait ici : il suffit d'appuyer sur Play. Ce script peut
    /// s'exécuter manuellement via le menu Tools > Zoologic, ou en mode batch :
    ///
    ///   "E:\UnityEditors\6000.3.21f1\Editor\Unity.exe" -batchmode -nographics -quit \
    ///       -projectPath "E:\projet new\Zoodoku" \
    ///       -executeMethod Zoologic.EditorTools.TestGridSceneBuilder.CreateTestScene
    /// </summary>
    public static class TestGridSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/TestGrid.unity";

        /// <summary>Crée (ou recrée) la scène Assets/Scenes/TestGrid.unity.</summary>
        [MenuItem("Tools/Zoologic/Créer la scène de test")]
        public static void CreateTestScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Caméra.
            var cameraGameObject = new GameObject("Main Camera");
            cameraGameObject.tag = "MainCamera";
            var camera = cameraGameObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.95f, 0.70f, 0.55f, 1f); // assorti au bas du dégradé chaud
            cameraGameObject.AddComponent<AudioListener>();

            // Canvas principal (Screen Space Overlay).
            var canvasGameObject = new GameObject(
                "UICanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGameObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGameObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            // EventSystem : gère les taps / appuis longs sur les cases.
            var eventSystemGameObject = new GameObject("EventSystem", typeof(EventSystem));
            eventSystemGameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            // Racine du jeu : le contrôleur s'auto-câble en Awake/Start.
            var gameRoot = new GameObject("GameRoot");
            gameRoot.AddComponent<GridView>();
            gameRoot.AddComponent<PuzzleGameController>();

            EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            Debug.Log("[Zoologic] Scène de test créée : " + ScenePath);

            // Vérification que le pipeline runtime se construit sans exception.
            SmokeTest();

            // Referme la scène de test pour qu'elle soit rouverte au prochain lancement.
            EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>
        /// Test de fumée : génère une grille 5x5 avec LevelGenerator, construit la
        /// grille visuelle via GridView.Build et exerce les visuels de cases, le tout
        /// en mode édition. Utile pour valider le code runtime sans lancer Play.
        /// </summary>
        public static void SmokeTest()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var canvasGameObject = new GameObject(
                "UICanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGameObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var host = new GameObject("Host");
            var gridView = host.AddComponent<GridView>();

            var grid = new Zoologic.Core.LevelGenerator().GenerateLevel(5, 1);
            if (grid == null)
                throw new System.Exception("[Zoologic] SmokeTest : GenerateLevel(5, 1) a retourné null.");

            gridView.Build(grid, (RectTransform)canvasGameObject.transform);

            if (gridView.Size != 5)
                throw new System.Exception("[Zoologic] SmokeTest : taille erronée (" + gridView.Size + ").");

            var board = (RectTransform)canvasGameObject.transform.Find("Board");
            if (board == null)
                throw new System.Exception("[Zoologic] SmokeTest : Board introuvable.");

            // Le Board contient aussi les ombres : on compte uniquement les cases "Cell".
            int cellCount = 0;
            foreach (Transform child in board)
            {
                if (child.name == "Cell")
                    cellCount++;
            }
            if (cellCount != 25)
                throw new System.Exception(
                    "[Zoologic] SmokeTest : cases manquantes (" + cellCount + ").");

            gridView.SetPion(0, 0, true);
            gridView.SetPion(0, 0, false);
            gridView.SetX(2, 3, true);
            gridView.SetX(2, 3, false);
            gridView.FlashConflict(1, 1);

            // Les 30 icônes d'animaux doivent être chargées depuis Resources.
            int iconCount = AnimalIconSet.LoadAll().Length;
            if (iconCount < 5)
                throw new System.Exception(
                    "[Zoologic] SmokeTest : icônes d'animaux manquantes (" + iconCount + "/30).");
            if (AnimalIconSet.GetShuffled().Length != iconCount)
                throw new System.Exception("[Zoologic] SmokeTest : mélange d'icônes invalide.");

            Debug.Log("[Zoologic] SMOKE TEST OK : grille 5x5, 25 cases, visuels exercés, " + iconCount + " icônes.");
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
