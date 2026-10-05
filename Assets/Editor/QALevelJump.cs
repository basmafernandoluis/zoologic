using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zoologic;

namespace Zoologic.EditorTools
{
    /// <summary>
    /// Sauts de niveaux pour les tests manuels (éditeur uniquement).
    /// N'apparaît jamais dans le build : aucun impact prod.
    /// </summary>
    public static class QALevelJump
    {
        // Même clé que LevelProgressManager.HighestUnlockedKey (privée) :
        // débloquer ne crée aucun nouveau système de persistance.
        private const string HighestUnlockedKey = "highest_unlocked";

        private const string TestGridPath = "Assets/Scenes/TestGrid.unity";

        [MenuItem("Tools/Zoo Logic/QA/Debloquer niveaux (jusqu'a 500)")]
        public static void DebloquerNiveaux()
        {
            PlayerPrefs.SetInt(HighestUnlockedKey, 500);
            PlayerPrefs.Save();
            Debug.Log("[QA] Niveaux débloqués jusqu'à 500 (PlayerPrefs persistants ; reset via les settings du jeu).");
        }

        [MenuItem("Tools/Zoo Logic/QA/Charger niveau 81 (8x8)")]
        public static void ChargerNiveau81() => ChargerNiveau(81);

        [MenuItem("Tools/Zoo Logic/QA/Charger niveau 100 (8x8)")]
        public static void ChargerNiveau100() => ChargerNiveau(100);

        private static void ChargerNiveau(int niveau)
        {
            PuzzleGameController.IsDailyPuzzle = false;
            PuzzleGameController.SelectedLevel = niveau;
            EditorSceneManager.OpenScene(TestGridPath);
            Debug.Log("[QA] Niveau " + niveau + " chargé dans TestGrid (daily désactivé). Lancez Play pour tester.");
        }
    }
}

