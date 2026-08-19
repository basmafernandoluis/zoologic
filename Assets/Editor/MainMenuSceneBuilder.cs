using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Zoodoku.EditorTools
{
    public static class MainMenuSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";

        [MenuItem("Tools/Zoodoku/Créer la scène MainMenu")]
        public static void CreateMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            ReorderBuildSettings();

            Debug.Log("[Zoodoku] Scène MainMenu créée : " + ScenePath);
            EditorSceneManager.OpenScene(ScenePath);
        }

        private static void ReorderBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            scenes.Sort((a, b) =>
            {
                int orderA = GetSceneOrder(a.path);
                int orderB = GetSceneOrder(b.path);
                return orderA.CompareTo(orderB);
            });

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static int GetSceneOrder(string path)
        {
            if (path.Contains("MainMenu")) return 0;
            if (path.Contains("LevelMap")) return 1;
            if (path.Contains("TestGrid")) return 2;
            if (path.Contains("Tutorial")) return 3;
            return 99;
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path))
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
