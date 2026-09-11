#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Zoologic.EditorTools
{
    /// <summary>
    /// Génère les polices SDF pour les langues à script complexe (ar/zh/ja/hi)
    /// à partir des polices OS. Exécuter via Tools > Zoologic > Build Localization Fonts.
    /// Les assets sont sauvés dans Assets/Resources/Fonts/Loc_{code}.asset
    /// et chargés au runtime par LocalizationManager (aucune dépendance OS sur device).
    /// </summary>
    public static class LocFontBuilder
    {
        private static readonly (string code, string[] families, char probe)[] Targets =
        {
            ("ar-SA", new[] { "Noto Naskh Arabic", "Noto Sans Arabic", "Tahoma", "Arial", "Geeza Pro" }, 'ا'),
            ("zh-CN", new[] { "Noto Sans CJK SC", "Noto Sans SC", "Microsoft YaHei", "SimSun", "PingFang SC" }, '中'),
            ("ja-JP", new[] { "Noto Sans JP", "Yu Gothic", "Meiryo", "Hiragino Sans" }, 'あ'),
            ("hi-IN", new[] { "Noto Sans Devanagari", "Nirmala UI", "Mangal", "Kohinoor Devanagari", "Arial" }, 'अ'),
        };

        [MenuItem("Tools/Zoologic/Build Localization Fonts")]
        public static void BuildAll()
        {
            int ok = 0;
            foreach (var (code, families, probe) in Targets)
                if (BuildOne(code, families, probe)) ok++;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Loc] fonts built: {ok}/{Targets.Length}. Relancez Play pour tester.");
        }

        [System.Serializable] private class LocTable { public LocEntry[] entries; }
        [System.Serializable] private class LocEntry { public string k; public string v; }

        private static void Prepopulate(TMP_FontAsset asset, string code)
        {
            try
            {
                var chars = new System.Collections.Generic.HashSet<uint>();
                for (char c = (char)32; c < (char)127; c++) chars.Add(c);
                foreach (var lang in new[] { code, "en-US" })
                {
                    var ta = Resources.Load<TextAsset>($"Localization/{lang}");
                    if (ta == null) continue;
                    var dict = JsonUtility.FromJson<LocTable>("{\"entries\":" + ta.text + "}");
                    if (dict?.entries == null) continue;
                    foreach (var e in dict.entries)
                    {
                        if (string.IsNullOrEmpty(e.v)) continue;
                        foreach (char c in e.v) chars.Add(c);
                    }
                }
                var arr = new uint[chars.Count];
                int i = 0;
                foreach (var c in chars) arr[i++] = c;
                if (asset.TryAddCharacters(arr, out uint[] missing))
                    Debug.Log($"[Loc] {code}: {arr.Length - missing.Length}/{arr.Length} glyphs baked" +
                        (missing.Length > 0 ? $", missing {missing.Length}" : ""));
                else
                    Debug.LogWarning($"[Loc] {code}: TryAddCharacters failed");
            }
            catch (System.Exception e) { Debug.LogWarning($"[Loc] prepopulate {code}: {e.Message}"); }
        }

        private static bool BuildOne(string code, string[] families, char probe)
        {
            foreach (var family in families)
            {
                Font src;
                try { src = Font.CreateDynamicFontFromOSFont(family, 64); }
                catch { continue; }
                if (src == null) continue;
                bool covers;
                try { covers = src.HasCharacter(probe); }
                catch { covers = false; }
                if (!covers) continue;
                try
                {
                    var asset = TMP_FontAsset.CreateFontAsset(src, 90, 5,
                        UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDF32, 1024, 1024,
                        AtlasPopulationMode.Dynamic, true);
                    if (asset == null) continue;
                    asset.name = $"Loc_{code}";
                    Prepopulate(asset, code);
                    string path = $"Assets/Resources/Fonts/Loc_{code}.asset";
                    var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                    if (existing != null)
                    {
                        EditorUtility.CopySerialized(asset, existing);
                        Object.DestroyImmediate(asset);
                        asset = existing;
                    }
                    else
                    {
                        AssetDatabase.CreateAsset(asset, path);
                    }
                    Debug.Log($"[Loc] font {code} <- {family} : {path}");
                    return true;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[Loc] build {code} ({family}): {e.Message}");
                }
            }
            Debug.LogError($"[Loc] no OS font found for {code} (tried: {string.Join(", ", families)})");
            return false;
        }
    }
}
#endif
