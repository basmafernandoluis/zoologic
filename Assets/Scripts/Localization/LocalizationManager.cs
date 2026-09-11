using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Zoologic.Localization
{
    public static class LocalizationManager
    {
        public const string DefaultLang = "en-US";
        public static readonly string[] Supported = {
            "fr-FR", "en-US", "pt-BR", "ru-RU",
            "ar-SA", "zh-CN", "ja-JP", "hi-IN"
        };

        private const string PrefsKey = "app_lang";
        private static Dictionary<string, string> _strings = new Dictionary<string, string>(StringComparer.Ordinal);
        private static string _current;
        private static TMP_FontAsset _liberationFallback;

        public static event Action OnLanguageChanged;
        public static string Current => _current ?? DefaultLang;
        public static bool IsRTL => Current == "ar-SA";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInit()
        {
            try
            {
                var host = new GameObject("LocalizationApplier");
                host.AddComponent<LocalizationApplier>();
            }
            catch { }
            string saved = PlayerPrefs.GetString(PrefsKey, "");
            string sys = Application.systemLanguage switch
            {
                SystemLanguage.French => "fr-FR",
                SystemLanguage.Portuguese => "pt-BR",
                SystemLanguage.Russian => "ru-RU",
                SystemLanguage.Arabic => "ar-SA",
                SystemLanguage.ChineseSimplified or SystemLanguage.Chinese => "zh-CN",
                SystemLanguage.Japanese => "ja-JP",
                SystemLanguage.Hindi => "hi-IN",
                _ => ""
            };
            SetLanguage(Normalize(saved) ?? Normalize(sys) ?? DefaultLang, silent: true);
        }

        public static string Normalize(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            foreach (var s in Supported)
                if (string.Equals(s, code, StringComparison.OrdinalIgnoreCase)) return s;
            string lower = code.ToLowerInvariant();
            if (lower.StartsWith("fr")) return "fr-FR";
            if (lower.StartsWith("pt")) return "pt-BR";
            if (lower.StartsWith("ru")) return "ru-RU";
            if (lower.StartsWith("ar")) return "ar-SA";
            if (lower.StartsWith("zh")) return "zh-CN";
            if (lower.StartsWith("ja")) return "ja-JP";
            if (lower.StartsWith("hi")) return "hi-IN";
            if (lower.StartsWith("en")) return "en-US";
            return null;
        }

        public static void SetLanguage(string code, bool silent = false)
        {
            code = Normalize(code) ?? DefaultLang;
            _current = code;
            PlayerPrefs.SetString(PrefsKey, code);
            PlayerPrefs.Save();
            LoadTable(code);
            if (code != DefaultLang) MergeFallback();
            if (NeedsComplexFont(code))
            {
                try { GetComplexFont(code); } catch { }
            }
            if (!silent) OnLanguageChanged?.Invoke();
        }

        private static void LoadTable(string code)
        {
            _strings.Clear();
            var asset = Resources.Load<TextAsset>($"Localization/{code}");
            if (asset == null) { Debug.LogWarning($"[Loc] missing table Localization/{code}"); return; }
            try
            {
                var dict = JsonUtility.FromJson<LocTable>("{\"entries\":" + asset.text + "}");
                if (dict?.entries != null)
                    foreach (var e in dict.entries)
                        if (!string.IsNullOrEmpty(e.k)) _strings[e.k] = e.v ?? "";
            }
            catch (Exception e) { Debug.LogError($"[Loc] parse {code}: {e.Message}"); }
        }

        private static void MergeFallback()
        {
            var asset = Resources.Load<TextAsset>($"Localization/{DefaultLang}");
            if (asset == null) return;
            try
            {
                var dict = JsonUtility.FromJson<LocTable>("{\"entries\":" + asset.text + "}");
                if (dict?.entries != null)
                    foreach (var e in dict.entries)
                        if (!string.IsNullOrEmpty(e.k) && !_strings.ContainsKey(e.k)) _strings[e.k] = e.v ?? "";
            }
            catch { }
        }

        public static string Get(string key)
        {
            if (key == null) return "";
            if (_strings.Count == 0) LoadTable(Current);
            return _strings.TryGetValue(key, out var v) ? v : $"[{key}]";
        }

        public static string Get(string key, params object[] args)
        {
            string fmt = Get(key);
            try { return args == null || args.Length == 0 ? fmt : string.Format(fmt, args); }
            catch { return fmt; }
        }

        public static void ApplyTo(TMP_Text tmp)
        {
            if (tmp == null) return;
            tmp.isRightToLeftText = IsRTL;
            if (tmp.font == null) return;
            if (NeedsComplexFont(Current))
            {
                var complex = GetComplexFont(Current);
                if (complex != null)
                {
                    if (_originals.Count > 2000) _originals.Clear();
                    if (IsLatinFont(tmp.font) && !_originals.ContainsKey(tmp.GetInstanceID()))
                        _originals[tmp.GetInstanceID()] = tmp.font;
                    tmp.font = complex;
                    return;
                }
                Debug.LogWarning($"[Loc] no system font for {Current}: import Noto SDF to Resources/Fonts/ (see docs).");
            }
            else if (_originals.TryGetValue(tmp.GetInstanceID(), out var orig) && orig != null)
            {
                tmp.font = orig;
                _originals.Remove(tmp.GetInstanceID());
            }
            EnsureFallback(tmp.font);
        }

        private static readonly Dictionary<int, TMP_FontAsset> _originals = new Dictionary<int, TMP_FontAsset>();
        private static readonly Dictionary<string, TMP_FontAsset> _complexCache = new Dictionary<string, TMP_FontAsset>(StringComparer.Ordinal);

        public static bool NeedsComplexFont(string code) =>
            code == "ar-SA" || code == "zh-CN" || code == "ja-JP" || code == "hi-IN";

        private static bool IsLatinFont(TMP_FontAsset f) =>
            f != null && (f.name.Contains("Fredoka") || f.name.Contains("Kenney") || f.name.Contains("Liberation"));

        private static string[] CandidatesFor(string code)
        {
            bool android = Application.platform == RuntimePlatform.Android;
            bool apple = Application.platform == RuntimePlatform.IPhonePlayer
                || Application.platform == RuntimePlatform.OSXPlayer
                || Application.platform == RuntimePlatform.OSXEditor;
            return code switch
            {
                "ar-SA" => android
                    ? new[] { "Noto Naskh Arabic", "Noto Sans Arabic", "Roboto" }
                    : apple
                        ? new[] { "Geeza Pro", "Noto Sans Arabic", "Arial" }
                        : new[] { "Noto Naskh Arabic", "Noto Sans Arabic", "Tahoma", "Arial" },
                "zh-CN" => android
                    ? new[] { "Noto Sans CJK", "Noto Sans SC", "Droid Sans Fallback", "Roboto" }
                    : apple
                        ? new[] { "PingFang SC", "Hiragino Sans GB", "Arial Unicode MS" }
                        : new[] { "Noto Sans CJK SC", "Noto Sans SC", "Microsoft YaHei", "SimSun", "Arial Unicode MS" },
                "ja-JP" => android
                    ? new[] { "Noto Sans JP", "Noto Sans CJK", "Droid Sans Fallback", "Roboto" }
                    : apple
                        ? new[] { "Hiragino Sans", "Hiragino Kaku Gothic ProN", "Arial Unicode MS" }
                        : new[] { "Noto Sans JP", "Yu Gothic", "Meiryo", "Arial Unicode MS" },
                "hi-IN" => android
                    ? new[] { "Noto Sans Devanagari", "Roboto" }
                    : apple
                        ? new[] { "Kohinoor Devanagari", "Arial" }
                        : new[] { "Noto Sans Devanagari", "Nirmala UI", "Mangal", "Arial" },
                _ => Array.Empty<string>(),
            };
        }

        private static int ProbeFor(string code) => code switch
        {
            "ar-SA" => 0x0627,
            "zh-CN" => 0x4E2D,
            "ja-JP" => 0x3042,
            "hi-IN" => 0x0905,
            _ => 0x41,
        };

        private static TMP_FontAsset GetComplexFont(string code)
        {
            if (_complexCache.TryGetValue(code, out var cached) && cached != null) return cached;
            try
            {
                var prebuilt = Resources.Load<TMP_FontAsset>($"Fonts/Loc_{code}");
                if (prebuilt != null)
                {
                    _complexCache[code] = prebuilt;
                    Debug.Log($"[Loc] prebuilt font for {code}");
                    return prebuilt;
                }
            }
            catch { }
            try
            {
                var candidates = CandidatesFor(code);
                var installed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    foreach (var n in Font.GetOSInstalledFontNames()) installed.Add(n);
                }
                catch { }
                int probe = ProbeFor(code);
                for (int pass = 0; pass < 2 && candidates.Length > 0; pass++)
                {
                    foreach (var family in candidates)
                    {
                        if (pass == 0 && installed.Count > 0 && !installed.Contains(family)) continue;
                        Font sysFont = null;
                        try { sysFont = Font.CreateDynamicFontFromOSFont(family, 64); }
                        catch { continue; }
                        if (sysFont == null) continue;
                        bool covers;
                        try { covers = sysFont.HasCharacter((char)probe); }
                        catch { covers = false; }
                        if (!covers) { try { UnityEngine.Object.Destroy(sysFont); } catch { } continue; }
                        try
                        {
                            var tmpFont = TMP_FontAsset.CreateFontAsset(sysFont, 90, 5,
                                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SMOOTH, 1024, 1024,
                                AtlasPopulationMode.Dynamic, true);
                            if (tmpFont == null) continue;
                            tmpFont.name = $"Loc_{code}";
                            EnsureFallback(tmpFont);
                            try
                            {
                                if (tmpFont.fallbackFontAssetTable == null)
                                    tmpFont.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
                                if (_liberationFallback != null && !tmpFont.fallbackFontAssetTable.Contains(_liberationFallback))
                                    tmpFont.fallbackFontAssetTable.Add(_liberationFallback);
                            }
                            catch { }
                            _complexCache[code] = tmpFont;
                            Debug.Log($"[Loc] complex font for {code}: {family}");
                            return tmpFont;
                        }
                        catch { }
                    }
                }
                Debug.LogWarning($"[Loc] no OS font covers {code}; squares expected. Import Noto SDF to Resources/Fonts/.");
            }
            catch (Exception e) { Debug.LogWarning($"[Loc] complex font failed ({code}): {e.Message}"); }
            return null;
        }

        public static void ApplyFontsToScene()
        {
            try
            {
                foreach (var tmp in Resources.FindObjectsOfTypeAll<TMP_Text>())
                {
                    if (tmp == null) continue;
                    try
                    {
                        if (!tmp.gameObject.scene.IsValid()) continue;
                    }
                    catch { continue; }
                    try { ApplyTo(tmp); } catch { }
                }
            }
            catch (Exception e) { Debug.LogWarning($"[Loc] ApplyFontsToScene: {e.Message}"); }
        }

        private sealed class LocalizationApplier : MonoBehaviour
        {
            private void Awake() { DontDestroyOnLoad(gameObject); }
            private void OnEnable()
            {
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnScene;
                OnLanguageChanged += ApplySoon;
            }
            private void OnDisable()
            {
                UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnScene;
                OnLanguageChanged -= ApplySoon;
            }
            private void OnScene(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => ApplySoon();
            private void ApplySoon()
            {
                StopAllCoroutines();
                StartCoroutine(Delayed());
            }
            private System.Collections.IEnumerator Delayed()
            {
                yield return null;
                yield return null;
                ApplyFontsToScene();
                yield return new WaitForSecondsRealtime(0.5f);
                ApplyFontsToScene();
            }
        }

        public static TextAlignmentOptions Mirror(TextAlignmentOptions a) => IsRTL switch
        {
            true => a switch
            {
                TextAlignmentOptions.Left => TextAlignmentOptions.Right,
                TextAlignmentOptions.Right => TextAlignmentOptions.Left,
                TextAlignmentOptions.MidlineLeft => TextAlignmentOptions.MidlineRight,
                TextAlignmentOptions.MidlineRight => TextAlignmentOptions.MidlineLeft,
                TextAlignmentOptions.TopLeft => TextAlignmentOptions.TopRight,
                TextAlignmentOptions.TopRight => TextAlignmentOptions.TopLeft,
                TextAlignmentOptions.BottomLeft => TextAlignmentOptions.BottomRight,
                TextAlignmentOptions.BottomRight => TextAlignmentOptions.BottomLeft,
                _ => a,
            },
            _ => a,
        };

        private static void EnsureFallback(TMP_FontAsset font)
        {
            if (font == null) return;
            try
            {
                if (_liberationFallback == null)
                    _liberationFallback = Resources.Load<TMP_FontAsset>("Fonts/LiberationSans SDF")
                        ?? TMP_Settings.defaultFontAsset;
                if (_liberationFallback == null || font == _liberationFallback) return;
                var list = font.fallbackFontAssetTable;
                if (list == null) return;
                if (!list.Contains(_liberationFallback)) list.Add(_liberationFallback);
            }
            catch { }
        }

        [Serializable] private class LocTable { public LocEntry[] entries; }
        [Serializable] private class LocEntry { public string k; public string v; }
    }
}
