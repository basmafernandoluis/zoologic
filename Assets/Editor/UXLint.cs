using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Zoologic.EditorTools
{
    /// <summary>
    /// Lint UX/UI : boutons trop petits + contrastes insuffisants.
    /// IMPORTANT : toute l'UI est procédurale (construite au runtime), donc ce
    /// lint ne voit quelque chose qu'en PlayMode, scène ouverte (TestGrid,
    /// LevelMap, MainMenu...). En EditMode il prévient et ne fait rien.
    /// Menu : Tools/Zoo Logic/QA/Run UX Lint (PlayMode).
    ///
    /// Règles (unités = px ref 1080x1920, car 1 unité canvas = 1 px ref) :
    /// - Tout Button actif : min(width,height) >= 64px (~23dp, plancher WCAG 24dp).
    ///   En dessous = FAIL. Les boutons < 132px (48dp standard) sont listés en
    ///   info pour revue.
    /// - Tout TMP fonctionnel (alpha >= 0.7, size >= 20, pas *Ghost*) :
    ///   contraste vs fond opaque composite >= 4.5 (WCAG AA). En dessous = FAIL.
    /// - Classe décorative (*Ghost* dans le nom, ex NumGhost) : FAIL si < 2.0.
    /// - Textes alpha < 0.35 ou sans fond opaque trouvable : ignorés (noté).
    /// Les FAIL partent en LogError (visibles CI/batch), le reste en info.
    /// </summary>
    public static class UXLint
    {
        // FAIL sous 64px (~23dp, minimum WCAG 24dp). Entre 64 et 132 : info revue.
        // Les rangées shop existantes (72px) passent ; les vrais risques
        // fat-finger (< 64px : anciens slots 36px, cadenas 40px) sont bloqués.
        private const float MinButtonSide = 64f;
        private const float StandardButtonSide = 132f;
        private const float MinContrast = 4.5f;
        private const float MinContrastDecor = 2.0f;

        private static readonly Color AssumedBaseBg = new Color(1f, 0.973f, 0.925f, 1f); // #FFF8EC

        [MenuItem("Tools/Zoo Logic/QA/Run UX Lint (PlayMode scene)")]
        public static void RunFromMenu()
        {
            int fails = Run();
            if (fails == 0)
                Debug.Log("[UXLint] PASS : aucun défaut (boutons + contrastes).");
            else
                Debug.LogError($"[UXLint] FAIL : {fails} défaut(s). Voir erreurs ci-dessus.");
        }

        /// <summary>Entrée batch : retourne le nombre de FAIL.</summary>
        public static int RunBatch() => Run();

        public static int Run()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[UXLint] Lancer le PlayMode sur la scène à auditer (UI procédurale invisible en EditMode).");
                return 0;
            }

            int fails = 0;
            int buttonsOk = 0;
            var smallButtons = new List<string>();
            fails += LintButtons(ref buttonsOk, smallButtons);
            fails += LintTexts();
            Debug.Log($"[UXLint] Boutons OK: {buttonsOk}, sous-standard (<132px) : {smallButtons.Count}"
                + (smallButtons.Count > 0 ? " [" + string.Join(", ", smallButtons) + "]" : ""));
            return fails;
        }

        // ------------------------------------------------------------------

        private static int LintButtons(ref int okCount, List<string> small)
        {
            int fails = 0;
            foreach (var btn in Object.FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (btn == null || !btn.gameObject.activeInHierarchy || !btn.enabled)
                    continue;
                var rt = btn.transform as RectTransform;
                if (rt == null)
                    continue;
                float w = Mathf.Abs(rt.rect.width);
                float h = Mathf.Abs(rt.rect.height);
                float min = Mathf.Min(w, h);
                string path = FullPath(btn.transform);
                if (min < MinButtonSide)
                {
                    fails++;
                    Debug.LogError($"[UXLint][FAIL] Bouton trop petit ({w:F0}x{h:F0}px < 64) : {path}", btn.gameObject);
                }
                else
                {
                    okCount++;
                    if (min < StandardButtonSide)
                        small.Add($"{btn.name}({min:F0}px)");
                }
            }
            return fails;
        }

        private static int LintTexts()
        {
            int fails = 0;
            foreach (var tmp in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None))
            {
                if (tmp == null || !tmp.gameObject.activeInHierarchy)
                    continue;
                // Couleur effective : les tags riches <color=#...> priment sur
                // tmp.color (souvent blanc, la vraie couleur est dans le tag).
                Color fg = EffectiveTextColor(tmp);
                float alpha = fg.a;
                if (alpha < 0.35f)
                    continue; // fades / fantômes : ignoré
                if (tmp.fontSize < 20f)
                    continue;
                bool decor = tmp.name.ToLowerInvariant().Contains("ghost")
                    || tmp.transform.parent != null && tmp.transform.parent.name.ToLowerInvariant().Contains("ghost");
                if (alpha < 0.7f && !decor)
                    continue;
                Color bg;
                if (!TryFindOpaqueBg(tmp.transform, out bg))
                    continue; // pas de fond déterminable : noté nulle part, ignoré
                float ratio = Contrast(fg, bg);
                // WCAG AA : 4.5 texte normal, 3.0 grand texte (>=24px ou >=19px gras).
                bool isBold = (tmp.fontStyle & FontStyles.Bold) != 0;
                float threshold = (tmp.fontSize >= 24f || (isBold && tmp.fontSize >= 19f)) ? 3.0f : MinContrast;
                string path = FullPath(tmp.transform);
                if (decor)
                {
                    if (ratio < MinContrastDecor)
                    {
                        fails++;
                        Debug.LogError($"[UXLint][FAIL] Décor illisible ({ratio:F2}:1 < 2.0) : {path}", tmp.gameObject);
                    }
                    continue;
                }
                if (ratio < threshold)
                {
                    fails++;
                    Debug.LogError($"[UXLint][FAIL] Contraste insuffisant ({ratio:F2}:1 < {threshold:F1}) '{Truncate(StripTags(tmp.text), 40)}' ({ToHex(fg)} sur fond {ToHex(bg)}) : {path}", tmp.gameObject);
                }
            }
            return fails;
        }

        private static readonly Regex ColorTagRegex =
            new Regex(@"<color\s*=\s*(#[0-9a-fA-F]{6,8})", RegexOptions.Compiled);

        /// <summary>Couleur réellement rendue : dernier tag &lt;color&gt; × couleur vertex.</summary>
        private static Color EffectiveTextColor(TMP_Text tmp)
        {
            Color c = tmp.color;
            var matches = ColorTagRegex.Matches(tmp.text ?? "");
            if (matches.Count > 0 && ColorUtility.TryParseHtmlString(matches[matches.Count - 1].Groups[1].Value, out Color tag))
                c = new Color(tag.r * c.r, tag.g * c.g, tag.b * c.b, tag.a * c.a);
            return c;
        }

        private static string StripTags(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return Regex.Replace(s, @"<[^>]+>", "");
        }

        private static readonly Dictionary<Sprite, Color> _spriteAvgCache = new Dictionary<Sprite, Color>();

        /// <summary>Couleur rendue d'un Image : moyenne du sprite × teinte.</summary>
        private static Color EffectiveImageColor(Image img)
        {
            Color tint = img.color;
            Sprite s = img.sprite;
            if (s == null || s.texture == null)
                return tint;
            if (!_spriteAvgCache.TryGetValue(s, out Color avg))
            {
                avg = Color.white;
                try
                {
                    avg = AverageSpriteColor(s);
                    _spriteAvgCache[s] = avg;
                }
                catch { /* texture illisible : repli teinte seule */ }
            }
            return new Color(avg.r * tint.r, avg.g * tint.g, avg.b * tint.b, avg.a * tint.a);
        }

        /// <summary>Moyenne alpha-pondérée des texels opaques du rect sprite
        /// (supporte les atlas). Alpha retourné = couverture moyenne.
        /// Les textures d'atlas sont non-lisibles en PlayMode (Read/Write off) :
        /// on passe par un blit RenderTexture (marche toujours).</summary>
        private static Color AverageSpriteColor(Sprite s)
        {
            var tex = s.texture;
            Rect r = s.rect;
            int x0 = Mathf.Clamp((int)r.x, 0, tex.width - 1);
            int y0 = Mathf.Clamp((int)r.y, 0, tex.height - 1);
            int x1 = Mathf.Clamp((int)(r.x + r.width), 0, tex.width);
            int y1 = Mathf.Clamp((int)(r.y + r.height), 0, tex.height);
            Texture2D src = tex;
            Texture2D owned = null;
            try
            {
                // Test de lisibilité : lève si Read/Write off.
                src.GetPixel(Mathf.Clamp(x0, 0, src.width - 1), Mathf.Clamp(y0, 0, src.height - 1));
            }
            catch
            {
                src = null;
            }
            if (src == null)
            {
                // Chemin blit : cop éléments via GPU puis lecture (coordonnées
                // bottom-up identiques au rect sprite, pas de flip).
                RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(tex, rt);
                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                owned = new Texture2D(x1 - x0, y1 - y0, TextureFormat.RGBA32, false);
                owned.ReadPixels(new Rect(x0, y0, x1 - x0, y1 - y0), 0, 0);
                owned.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                x0 = 0; y0 = 0; x1 = owned.width; y1 = owned.height;
                src = owned;
            }
            try
            {
                double R = 0, G = 0, B = 0, A = 0;
                int total = 0;
                int step = Mathf.Max(1, (int)(((x1 - x0) * (y1 - y0)) / 4096));
                for (int y = y0; y < y1; y += step)
                    for (int x = x0; x < x1; x += step)
                    {
                        total++;
                        Color p = src.GetPixel(x, y);
                        if (p.a <= 0.05f) continue;
                        R += p.r * p.a; G += p.g * p.a; B += p.b * p.a; A += p.a;
                    }
                if (total == 0 || A <= 0.0) return new Color(1f, 1f, 1f, 0f);
                float coverage = (float)(A / total);
                return new Color((float)(R / A), (float)(G / A), (float)(B / A), coverage);
            }
            finally
            {
                if (owned != null) Object.Destroy(owned);
            }
        }

        private static bool TryFindOpaqueBg(Transform from, out Color bg)
        {
            // Composite du socle crème + tous les Image ancêtres (externe -> interne).
            float r = AssumedBaseBg.r, g = AssumedBaseBg.g, b = AssumedBaseBg.b;
            bool found = false;
            var chain = new List<Image>();
            Transform node = from.parent;
            while (node != null)
            {
                var img = node.GetComponent<Image>();
                if (img != null && img.enabled)
                    chain.Add(img);
                node = node.parent;
            }
            for (int i = chain.Count - 1; i >= 0; i--)
            {
                Color c = EffectiveImageColor(chain[i]);
                if (c.a <= 0f)
                    continue;
                found = true;
                r = r * (1f - c.a) + c.r * c.a;
                g = g * (1f - c.a) + c.g * c.a;
                b = b * (1f - c.a) + c.b * c.a;
            }
            bg = new Color(r, g, b, 1f);
            return found;
        }

        private static float Contrast(Color fg, Color bg)
        {
            float l1 = Luminance(fg);
            float l2 = Luminance(bg);
            if (l1 < l2) { float t = l1; l1 = l2; l2 = t; }
            return (l1 + 0.05f) / (l2 + 0.05f);
        }

        private static float Luminance(Color c)
        {
            float R = Mathf.GammaToLinearSpace(Mathf.Clamp01(c.r));
            float G = Mathf.GammaToLinearSpace(Mathf.Clamp01(c.g));
            float B = Mathf.GammaToLinearSpace(Mathf.Clamp01(c.b));
            return 0.2126f * R + 0.7152f * G + 0.0722f * B;
        }

        private static string FullPath(Transform t)
        {
            var parts = new List<string>();
            while (t != null) { parts.Add(t.name); t = t.parent; }
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static string Truncate(string s, int n)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("\n", " ");
            return s.Length <= n ? s : s.Substring(0, n) + "…";
        }

        private static string ToHex(Color c)
        {
            return "#" + ((int)(c.r * 255)).ToString("X2") + ((int)(c.g * 255)).ToString("X2") + ((int)(c.b * 255)).ToString("X2");
        }
    }
}
