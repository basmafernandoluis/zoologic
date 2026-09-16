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
            try { WireStaticFallbacks(); } catch { }
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
            if (tmp.font == null) return;
            tmp.isRightToLeftText = IsRTL && ContainsRtl(tmp.text);
            try { WireStaticFallbacks(); } catch { }
            if (_originals.TryGetValue(tmp.GetInstanceID(), out var orig) && orig != null)
            {
                if (tmp.font != orig && orig.name.Contains("Fredoka"))
                {
                    tmp.font = orig;
                }
                _originals.Remove(tmp.GetInstanceID());
            }
            var cur = GetComplexFont(Current);
            if (cur != null && tmp.font != cur) AddFallback(tmp.font, cur);
            EnsureFallback(tmp.font);
            if (cur != null) EnsureFallback(cur);
            // Filet système (Noto/Roboto…) : couvre les glyphes absents des
            // atlas préfabriqués, y compris les futures clés. No-op éditeur.
            try { AttachSystemFallback(tmp.font, Current); } catch { }
        }

        private static TMP_FontAsset[] _locCache;
        private static void WireStaticFallbacks()
        {
            try
            {
                if (_locCache == null)
                {
                    _locCache = new[]
                    {
                        Resources.Load<TMP_FontAsset>("Fonts/Loc_ar-SA"),
                        Resources.Load<TMP_FontAsset>("Fonts/Loc_zh-CN"),
                        Resources.Load<TMP_FontAsset>("Fonts/Loc_ja-JP"),
                        Resources.Load<TMP_FontAsset>("Fonts/Loc_hi-IN"),
                    };
                }
                var latin = new[]
                {
                    Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Bold SDF"),
                    Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Regular SDF"),
                };
                var all = new System.Collections.Generic.List<TMP_FontAsset>();
                foreach (var l in latin) if (l != null) all.Add(l);
                foreach (var loc in _locCache) if (loc != null) all.Add(loc);
                foreach (var a in all)
                {
                    foreach (var b in all)
                    {
                        if (a == b) continue;
                        AddFallback(a, b);
                    }
                    EnsureFallback(a);
                }
            }
            catch { }
        }

        private static string LangOf(TMP_FontAsset f)
        {
            if (f == null) return null;
            if (f.name.Contains("ar-SA")) return "ar-SA";
            if (f.name.Contains("zh-CN")) return "zh-CN";
            if (f.name.Contains("ja-JP")) return "ja-JP";
            if (f.name.Contains("hi-IN")) return "hi-IN";
            return null;
        }

        private static bool ContainsRtl(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
            {
                if ((c >= '\u0590' && c <= '\u08FF')
                    || (c >= '\uFB1D' && c <= '\uFDFF')
                    || (c >= '\uFE70' && c <= '\uFEFC'))
                    return true;
            }
            return false;
        }

        private static void AddFallback(TMP_FontAsset font, TMP_FontAsset fallback)
        {
            if (font == null || fallback == null || font == fallback) return;
            try
            {
                if (font.fallbackFontAssetTable == null)
                    font.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
                var list = font.fallbackFontAssetTable;
                if (!list.Contains(fallback)) list.Add(fallback);
            }
            catch { }
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

        private static readonly Dictionary<string, TMP_FontAsset> _sysFallbackCache = new Dictionary<string, TMP_FontAsset>(StringComparer.Ordinal);
        private static readonly HashSet<string> _sysAttempted = new HashSet<string>(StringComparer.Ordinal);

        private static void AttachSystemFallback(TMP_FontAsset target, string code)
        {
            if (target == null || string.IsNullOrEmpty(code)) return;
            if (Application.isEditor) return;
            if (Application.platform != RuntimePlatform.Android && Application.platform != RuntimePlatform.IPhonePlayer) return;
            try
            {
                if (_sysFallbackCache.TryGetValue(code, out var sys) && sys != null)
                {
                    AddFallback(target, sys);
                    return;
                }
                if (_sysAttempted.Contains(code)) return;
                _sysAttempted.Add(code);
                var created = CreateSystemFont(code);
                if (created != null)
                {
                    _sysFallbackCache[code] = created;
                    AddFallback(target, created);
                }
            }
            catch { }
        }

        private static TMP_FontAsset CreateSystemFont(string code)
        {
            if (Application.isEditor) return null;
            try
            {
                var candidates = CandidatesFor(code);
                int probe = ProbeFor(code);
                foreach (var family in candidates)
                {
                    Font sysFont = null;
                    try { sysFont = Font.CreateDynamicFontFromOSFont(family, 64); }
                    catch { continue; }
                    if (sysFont == null) continue;
                    bool covers;
                    try { covers = sysFont.HasCharacter((char)probe); }
                    catch { covers = false; }
                    if (!covers) continue;
                    try
                    {
                        var tmpFont = TMP_FontAsset.CreateFontAsset(sysFont, 90, 5,
                            UnityEngine.TextCore.LowLevel.GlyphRenderMode.SMOOTH, 1024, 1024,
                            AtlasPopulationMode.Dynamic, true);
                        if (tmpFont == null) continue;
                        tmpFont.name = $"Sys_{code}";
                        EnsureFallback(tmpFont);
                        return tmpFont;
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }

        private static TMP_FontAsset GetComplexFont(string code)
        {
            if (_complexCache.TryGetValue(code, out var cached) && cached != null) return cached;
            try
            {
                var prebuilt = Resources.Load<TMP_FontAsset>($"Fonts/Loc_{code}");
                if (prebuilt != null)
                {
                    _complexCache[code] = prebuilt;
                    return prebuilt;
                }
            }
            catch { }
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
                if (font.fallbackFontAssetTable == null)
                    font.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
                var list = font.fallbackFontAssetTable;
                if (!list.Contains(_liberationFallback)) list.Add(_liberationFallback);
            }
            catch { }
        }

        [Serializable] private class LocTable { public LocEntry[] entries; }
        [Serializable] private class LocEntry { public string k; public string v; }
    }
}
