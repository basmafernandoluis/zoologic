using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Zoologic.EditorTools
{
    /// <summary>
    /// Crée la scène "LevelMap" : scène vide, tout est construit procéduralement
    /// par LevelMapBuilder au RuntimeInitializeOnLoadMethod.
    /// </summary>
    public static class LevelMapSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/LevelMap.unity";

        [MenuItem("Tools/Zoologic/Créer la scène LevelMap")]
        public static void CreateLevelMapScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            Debug.Log("[Zoologic] Scène LevelMap créée : " + ScenePath);

            EditorSceneManager.OpenScene(ScenePath);
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
