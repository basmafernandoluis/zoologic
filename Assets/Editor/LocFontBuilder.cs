using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

namespace Zoologic.EditorTools
{
    public static class LocFontBuilder
    {
        private struct LocFontJob
        {
            public string Code;
            public string Ttf;
            public int Sampling;
            public LocFontJob(string code, string ttf, int sampling) { Code = code; Ttf = ttf; Sampling = sampling; }
        }

        // Sources (OFL) : google/fonts
        //  ar: ofl/almarai/Almarai-Regular.ttf
        //  hi: ofl/mukta/Mukta-Regular.ttf
        //  zh: ofl/zcoolkuaile/ZCOOLKuaiLe-Regular.ttf
        //  ja: ofl/mplusrounded1c/MPLUSRounded1c-Regular.ttf
        [MenuItem("Tools/Zoo Logic/Build Loc Fonts (ar/hi/zh/ja)")]
        public static void BuildLocFontsMenu() => BuildLocFonts();

        public static void BuildLocFonts()
        {
            var jobs = new[]
            {
                new LocFontJob("ar-SA", "Assets/Editor/LocFontSrc/ar.ttf", 48),
                new LocFontJob("hi-IN", "Assets/Editor/LocFontSrc/hi.ttf", 48),
                new LocFontJob("zh-CN", "Assets/Editor/LocFontSrc/zh.ttf", 48),
                new LocFontJob("ja-JP", "Assets/Editor/LocFontSrc/ja.ttf", 48),
            };
            Directory.CreateDirectory("Assets/Resources/Fonts");
            foreach (var job in jobs)
            {
                try { BuildOne(job); }
                catch (System.Exception e) { Debug.LogError($"[LocFont] {job.Code} FAILED: {e}"); }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LocFont] Done.");
        }

        private static void BuildOne(LocFontJob job)
        {
            var srcFont = AssetDatabase.LoadAssetAtPath<Font>(job.Ttf);
            if (srcFont == null) { Debug.LogError($"[LocFont] {job.Code}: TTF introuvable {job.Ttf}"); return; }

            string charset = CollectCharset(job.Code);
            Debug.Log($"[LocFont] {job.Code}: {charset.Length} glyphes uniques.");

            int padding = 5;
            int atlas = PickAtlas(job.Code, charset.Length, job.Sampling, padding);
            var fa = TMP_FontAsset.CreateFontAsset(
                srcFont, job.Sampling, padding,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                atlas, atlas, AtlasPopulationMode.Dynamic, false);
            if (fa == null) { Debug.LogError($"[LocFont] {job.Code}: CreateFontAsset a retourné null"); return; }

            if (!fa.TryAddCharacters(charset, out string missing, true))
                Debug.LogWarning($"[LocFont] {job.Code}: TryAddCharacters partiel, manquants={missing?.Length ?? 0}");
            if (!string.IsNullOrEmpty(missing))
                Debug.LogWarning($"[LocFont] {job.Code}: glyphes manquants: {missing}");

            fa.atlasPopulationMode = AtlasPopulationMode.Static;

            fa.name = $"Loc_{job.Code}";
            string assetPath = $"Assets/Resources/Fonts/Loc_{job.Code}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) AssetDatabase.DeleteAsset(assetPath);

            AssetDatabase.CreateAsset(fa, assetPath);
            if (fa.atlasTextures != null && fa.atlasTextures.Length > 0 && fa.atlasTextures[0] != null)
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            if (fa.material != null)
                AssetDatabase.AddObjectToAsset(fa.material, fa);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            Debug.Log($"[LocFont] {job.Code}: OK -> {assetPath} (atlas {atlas}, {fa.characterTable?.Count ?? 0} caracteres)");
        }

        private static int PickAtlas(string code, int glyphCount, int sampling, int padding)
        {
            if (code == "zh-CN" || code == "ja-JP") return 4096;
            if (code == "ar-SA" || code == "hi-IN") return 2048;
            int cell = sampling + padding * 2;
            foreach (int size in new[] { 1024, 2048, 4096 })
            {
                int perRow = (size - 8) / cell;
                if ((long)perRow * perRow >= (long)(glyphCount * 1.3)) return size;
            }
            return 4096;
        }

        private static int PickAtlas(int glyphCount, int sampling, int padding)
        {
            int cell = sampling + padding * 2;
            foreach (int size in new[] { 1024, 2048, 4096 })
            {
                int perRow = (size - 8) / cell;
                if ((long)perRow * perRow >= (long)(glyphCount * 1.3)) return size;
            }
            return 4096;
        }

        private static string CollectCharset(string code)
        {
            var set = new HashSet<uint>();
            for (uint c = 32; c < 127; c++) set.Add(c);
            foreach (char c in "•♥—–…’«»「」？！：；×→") set.Add(c);
            foreach (char c in "FrançaisEnglishPortuguêsРусскийالعربية中文日本語हिन्दी") set.Add(c);
            string dir = "Assets/Resources/Localization";
            string[] files;
            try { files = Directory.GetFiles(dir, "*.json"); }
            catch { files = new[] { $"Assets/Resources/Localization/{code}.json" }; }
            foreach (var path in files)
            {
                if (!File.Exists(path)) continue;
                try
                {
                    string json = File.ReadAllText(path);
                    var table = JsonUtility.FromJson<LocTable>("{\"entries\":" + json + "}");
                    if (table?.entries != null)
                        foreach (var e in table.entries)
                        {
                            if (e?.v == null) continue;
                            foreach (char c in e.v) set.Add(c);
                        }
                }
                catch (System.Exception e) { Debug.LogWarning($"[LocFont] parse {path}: {e.Message}"); }
            }
            var chars = new char[set.Count];
            int i = 0;
            foreach (uint c in set) chars[i++] = (char)c;
            return new string(chars);
        }

        [System.Serializable] private class LocTable { public LocEntry[] entries; }
        [System.Serializable] private class LocEntry { public string k; public string v; }
    }
}
