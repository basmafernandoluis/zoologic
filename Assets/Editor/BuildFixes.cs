using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Zoologic.EditorTools
{
    class BuildFixes : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            FixIOSPluginForAndroid();
        }

        private static void FixIOSPluginForAndroid()
        {
            var pluginPath = "Packages/com.google.ads.mobile/Plugins/iOS/unity-plugin-library.xcframework";
            var importer = AssetImporter.GetAtPath(pluginPath) as PluginImporter;
            if (importer == null) return;
            bool changed = false;
            if (importer.GetCompatibleWithPlatform(BuildTarget.Android))
            {
                importer.SetCompatibleWithPlatform(BuildTarget.Android, false);
                changed = true;
            }
            if (importer.GetCompatibleWithPlatform(BuildTarget.iOS) == false)
            {
                importer.SetCompatibleWithPlatform(BuildTarget.iOS, true);
                changed = true;
            }
            if (changed)
            {
                importer.SaveAndReimport();
                Debug.Log("[BuildFixes] iOS xcframework excluded from Android");
            }
        }
    }
}
