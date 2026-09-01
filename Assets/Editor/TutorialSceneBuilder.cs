using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zoologic.Core;

namespace Zoologic.EditorTools
{
    /// <summary>
    /// Outil d'édition qui crée la scène "Tutorial" : caméra, canvas UI, EventSystem,
    /// et un GameRoot portant GridView + TutorialManager.
    ///
    /// Tout le câblage est fait ici : il suffit d'appuyer sur Play. Ce script peut
    /// s'exécuter manuellement via le menu Tools > Zoologic, ou en mode batch :
    ///
    ///   "E:\UnityEditors\6000.3.21f1\Editor\Unity.exe" -batchmode -nographics -quit \
    ///       -projectPath "E:\projet new\Zoodoku" \
    ///       -executeMethod Zoologic.EditorTools.TutorialSceneBuilder.CreateTutorialScene
    /// </summary>
    public static class TutorialSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Tutorial.unity";

        // Grille fixe du tutoriel (identique à celle de TutorialManager).
        private static readonly int[,] TutorialRegions =
        {
            { 0, 0, 1, 1 },
            { 0, 0, 1, 1 },
            { 2, 2, 3, 3 },
            { 2, 2, 3, 3 },
        };

        // Solution complète de la grille fixe (utilisée par le smoke test).
        private static readonly (int row, int col)[] Solution =
        {
            (0, 1), (1, 3), (2, 0), (3, 2),
        };

        /// <summary>Crée (ou recrée) la scène Assets/Scenes/Tutorial.unity.</summary>
        [MenuItem("Tools/Zoologic/Créer la scène tutoriel")]
        public static void CreateTutorialScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Caméra.
            var cameraGameObject = new GameObject("Main Camera");
            cameraGameObject.tag = "MainCamera";
            var camera = cameraGameObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.84f, 0.91f, 0.97f, 1f); // assorti au bas du dégradé (fallback)
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

            // EventSystem : gère les taps sur les cases.
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

            // Racine du jeu : le tutoriel s'auto-câble en Awake/Start.
            var gameRoot = new GameObject("GameRoot");
            gameRoot.AddComponent<GridView>();
            gameRoot.AddComponent<TutorialManager>();

            EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            Debug.Log("[Zoologic] Scène tutoriel créée : " + ScenePath);

            // Vérification du pipeline runtime (grille fixe + règles + visuels).
            SmokeTest();

            // Referme la scène pour qu'elle soit rouverte au prochain lancement.
            EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>
        /// Test de fumée du tutoriel : vérifie que la grille fixe 4x4 se construit
        /// visuellement, que les règles typées (ConflictType) fonctionnent, et que
        /// l'étape 4 n'admet qu'une seule case valide (déduction unique).
        /// </summary>
        public static void SmokeTest()
        {
            // 1) Construction visuelle de la grille fixe.
            var canvasGameObject = new GameObject(
                "UICanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGameObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var host = new GameObject("Host");
            var gridView = host.AddComponent<GridView>();

            var grid = new PuzzleGrid(TutorialRegions);
            gridView.Build(grid, (RectTransform)canvasGameObject.transform);

            if (gridView.Size != 4)
                throw new System.Exception("[Zoologic] TutorialSmokeTest : taille erronée (" + gridView.Size + ").");

            var board = (RectTransform)canvasGameObject.transform.Find("Board");
            if (board == null)
                throw new System.Exception("[Zoologic] TutorialSmokeTest : Board introuvable.");

            int cellCount = 0;
            foreach (Transform child in board)
            {
                if (child.name == "Cell")
                    cellCount++;
            }
            if (cellCount != 16)
                throw new System.Exception(
                    "[Zoologic] TutorialSmokeTest : cases manquantes (" + cellCount + ").");

            // 2) Règles typées : GetConflicts distingue les quatre types.
            var grilleRegles = new PuzzleGrid(TutorialRegions);
            grilleRegles.PlacePion(0, 0);

            AssertContains(RuleValidator.GetConflicts(grilleRegles, 0, 0), new ConflictType[0],
                "GetConflicts doit ignorer le pion de la case testée (0,0).");
            AssertContains(RuleValidator.GetConflicts(grilleRegles, 0, 1), new[] { ConflictType.Zone, ConflictType.Row },
                "GetConflicts(0,1) doit signaler zone ET ligne.");
            AssertContains(RuleValidator.GetConflicts(grilleRegles, 1, 0), new[] { ConflictType.Zone, ConflictType.Column },
                "GetConflicts(1,0) doit signaler zone ET colonne.");
            AssertContains(RuleValidator.GetConflicts(grilleRegles, 0, 3), new[] { ConflictType.Row },
                "GetConflicts(0,3) doit signaler uniquement la ligne.");
            AssertContains(RuleValidator.GetConflicts(grilleRegles, 3, 0), new[] { ConflictType.Column },
                "GetConflicts(3,0) doit signaler uniquement la colonne.");
            AssertContains(RuleValidator.GetConflicts(grilleRegles, 1, 1), new[] { ConflictType.Zone, ConflictType.Diagonal },
                "GetConflicts(1,1) doit signaler zone ET diagonale.");

            // 3) Règles booléennes : IsValidPlacement reste cohérent après le refactor.
            if (RuleValidator.IsValidPlacement(grilleRegles, 0, 0))
                throw new System.Exception("[Zoologic] TutorialSmokeTest : case occupée considérée valide.");
            if (RuleValidator.IsValidPlacement(grilleRegles, 0, 3))
                throw new System.Exception("[Zoologic] TutorialSmokeTest : même ligne considérée valide.");
            if (RuleValidator.IsValidPlacement(grilleRegles, 3, 0))
                throw new System.Exception("[Zoologic] TutorialSmokeTest : même colonne considérée valide.");
            if (!RuleValidator.IsValidPlacement(grilleRegles, 3, 3))
                throw new System.Exception("[Zoologic] TutorialSmokeTest : case libre valide refusée.");

            // 4) La grille fixe est résolvable (solution connue, sans conflit).
            var grilleResolue = new PuzzleGrid(TutorialRegions);
            foreach ((int row, int col) in Solution)
            {
                grilleResolue.PlacePion(row, col);
                if (RuleValidator.GetConflicts(grilleResolue, row, col).Count > 0)
                    throw new System.Exception(
                        "[Zoologic] TutorialSmokeTest : la solution " + row + "," + col + " contient un conflit.");
            }
            if (!RuleValidator.IsSolved(grilleResolue))
                throw new System.Exception("[Zoologic] TutorialSmokeTest : la solution connue n'est pas résolue.");

            // 5) Déduction unique de l'étape 4 : avec les 3 pions pré-posés, une seule
            //    case vide reste valide, et c'est la case attendue (2,0).
            var grilleEtape4 = new PuzzleGrid(TutorialRegions);
            grilleEtape4.PlacePion(0, 1);
            grilleEtape4.PlacePion(1, 3);
            grilleEtape4.PlacePion(3, 2);

            var valides = new List<(int row, int col)>();
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    if (!grilleEtape4.HasPion(row, col) && RuleValidator.IsValidPlacement(grilleEtape4, row, col))
                        valides.Add((row, col));
                }
            }
            if (valides.Count != 1 || valides[0] != (2, 0))
                throw new System.Exception(
                    "[Zoologic] TutorialSmokeTest : déduction non unique, valides = [" +
                    string.Join(", ", valides) + "] (attendu : (2, 0)).");

            Debug.Log("[Zoologic] TUTORIAL SMOKE TEST OK : grille 4x4, 16 cases, règles typées, solution unique en (2,0).");
        }

        /// <summary>
        /// Vérifie que la liste de conflits contient exactement les types attendus.
        /// </summary>
        private static void AssertContains(List<ConflictType> actual, ConflictType[] expected, string message)
        {
            if (actual.Count != expected.Length)
                throw new System.Exception("[Zoologic] TutorialSmokeTest : " + message +
                    " (obtenu " + string.Join(",", actual) + ").");

            foreach (ConflictType type in expected)
            {
                if (!actual.Contains(type))
                    throw new System.Exception("[Zoologic] TutorialSmokeTest : " + message +
                        " (type manquant : " + type + ").");
            }
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
