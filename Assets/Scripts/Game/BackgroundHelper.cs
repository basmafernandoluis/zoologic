using UnityEngine;
using UnityEngine.UI;

namespace Zoologic
{
    /// <summary>
    /// Utilitaire partagé pour le fond d'écran sur les 3 scènes principales.
    /// Palette chaude cohérente + texture pattern_81 en overlay tiling.
    /// </summary>
    public static class BackgroundHelper
    {
        // ------------------------------------------------------------------
        // Palette chaude partagée ( MainMenu / LevelMap / GridView )
        // ------------------------------------------------------------------

        public static readonly Color BgTop    = new Color(1.00f, 0.98f, 0.94f); // #FFFBF0 beige clair cozy
        public static readonly Color BgBottom = new Color(1.00f, 0.92f, 0.82f); // #FFEACC pêche très claire

        // Overlay pattern : teinte légèrement plus soutenue que le dégradé, alpha 7%
        private static readonly Color PatternTint = new Color(0.93f, 0.82f, 0.73f, 0.07f);

        // ------------------------------------------------------------------
        // Gradient
        // ------------------------------------------------------------------

        public static Sprite CreateGradientSprite(Color top, Color bottom)
        {
            const int height = 64;
            var tex = new Texture2D(1, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < height; y++)
                tex.SetPixel(0, y, Color.Lerp(bottom, top, y / (float)(height - 1)));

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, height), new Vector2(0.5f, 0.5f));
        }

        // ------------------------------------------------------------------
        // Tiling pattern (pattern_81.png, Wrap Mode Repeat dans l'import)
        // ------------------------------------------------------------------

        private static Sprite _patternSprite;

        public static Sprite GetPatternSprite()
        {
            if (_patternSprite == null)
                _patternSprite = Resources.Load<Sprite>("Art/Patterns/pattern_81");
            return _patternSprite;
        }

        // ------------------------------------------------------------------
        // Construction complète du fond : gradient + overlay pattern tiling.
        // Appelle cette méthode depuis chaque scène après avoir créé le canvas.
        // ------------------------------------------------------------------

        public static void ApplyBackground(Transform canvasTransform)
        {
            // 1) Dégradé
            var gradGO = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gradGO.transform.SetParent(canvasTransform, false);
            var gradRect = gradGO.GetComponent<RectTransform>();
            gradRect.anchorMin = Vector2.zero;
            gradRect.anchorMax = Vector2.one;
            gradRect.offsetMin = Vector2.zero;
            gradRect.offsetMax = Vector2.zero;
            var gradImg = gradGO.GetComponent<Image>();
            gradImg.sprite = CreateGradientSprite(BgTop, BgBottom);
            gradImg.raycastTarget = false;

            // 2) Overlay pattern tiling
            Sprite pattern = GetPatternSprite();
            if (pattern == null) return;

            var patGO = new GameObject("PatternOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            patGO.transform.SetParent(canvasTransform, false);
            var patRect = patGO.GetComponent<RectTransform>();
            patRect.anchorMin = Vector2.zero;
            patRect.anchorMax = Vector2.one;
            patRect.offsetMin = Vector2.zero;
            patRect.offsetMax = Vector2.zero;
            var patImg = patGO.GetComponent<Image>();
            patImg.sprite = pattern;
            patImg.type = Image.Type.Tiled;
            patImg.pixelsPerUnitMultiplier = 0.45f;
            patImg.color = PatternTint;
            patImg.raycastTarget = false;
        }
    }
}
