using System;
using UnityEngine;

namespace Zoologic
{
    public static class Easing
    {
        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;

            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        public static float EaseOutCubic(float t)
            => 1f - Mathf.Pow(1f - t, 3f);

        public static float EaseOutQuad(float t)
            => 1f - (1f - t) * (1f - t);

        public static float EaseInQuad(float t)
            => t * t;

        public static float EaseOutElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;

            const float c4 = (2f * Mathf.PI) / 3f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
        }

        public static float EaseInOutQuad(float t)
            => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) * 0.5f;
    }

    /// <summary>
    /// Helpers de "punch" visuel réutilisables : échelle en surtension et fade-out.
    /// Rapide et léger (aucun package). Les coroutines vivent sur un MonoBehaviour passé
    /// (généralement le composant appelant) pour rester liées à la scène.
    /// </summary>
    public static class Punch
    {
        /// <summary>
        /// Lance un punch d'échelle (scale 1 → overshoot → 1) sur <paramref name="rt"/>.
        /// Retourne la coroutine pour pouvoir l'interrompre éventuellement.
        /// </summary>
        public static Coroutine Scale(MonoBehaviour runner, RectTransform rt,
            float targetScale = 1.25f, float duration = 0.3f, bool elastic = false)
        {
            if (runner == null || rt == null)
                return null;
            return runner.StartCoroutine(ScaleRoutine(rt, targetScale, duration, elastic));
        }

        private static System.Collections.IEnumerator ScaleRoutine(RectTransform rt,
            float targetScale, float duration, bool elastic)
        {
            Vector3 baseScale = rt.localScale;
            float elapsed = 0f;

            // Montée.
            while (elapsed < duration * 0.5f)
            {
                float t = Mathf.Clamp01(elapsed / (duration * 0.5f));
                float s = Mathf.Lerp(baseScale.x, targetScale, Easing.EaseOutQuad(t));
                rt.localScale = new Vector3(s, s, s);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // Retour.
            elapsed = 0f;
            while (elapsed < duration * 0.5f)
            {
                float t = Mathf.Clamp01(elapsed / (duration * 0.5f));
                float s = elastic
                    ? Mathf.Lerp(targetScale, baseScale.x, Easing.EaseOutElastic(t))
                    : Mathf.Lerp(targetScale, baseScale.x, Easing.EaseOutBack(t));
                rt.localScale = new Vector3(s, s, s);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            rt.localScale = baseScale;
        }

        /// <summary>Fade l'alpha d'une Image entre sa valeur initiale et 0 (puis revient).</summary>
        public static Coroutine FlashAlpha(MonoBehaviour runner, UnityEngine.UI.Image image,
            float minAlpha = 0.4f, float duration = 0.25f)
        {
            if (runner == null || image == null)
                return null;
            return runner.StartCoroutine(FlashAlphaRoutine(image, minAlpha, duration));
        }

        private static System.Collections.IEnumerator FlashAlphaRoutine(UnityEngine.UI.Image image,
            float minAlpha, float duration)
        {
            Color baseColor = image.color;
            Color dimmed = new Color(baseColor.r, baseColor.g, baseColor.b, minAlpha);
            float half = duration * 0.5f;
            float elapsed = 0f;

            while (elapsed < half)
            {
                float t = Mathf.Clamp01(elapsed / half);
                image.color = Color.Lerp(baseColor, dimmed, Easing.EaseInQuad(t));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < half)
            {
                float t = Mathf.Clamp01(elapsed / half);
                image.color = Color.Lerp(dimmed, baseColor, Easing.EaseOutQuad(t));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            image.color = baseColor;
        }
    }

    /// <summary>
    /// Petit tremblement d'écran léger : déplace une RectTransform (le plateau) de
    /// façon aléatoire amortie. Indépendant de Punch (position vs échelle).
    /// </summary>
    public static class ScreenShake
    {
        /// <summary>
        /// Lance un shake sur <paramref name="rt"/> (souvent le conteneur du plateau).
        /// L'amplitude est en unités de canvas (px de référence 1080x1920).
        /// </summary>
        public static Coroutine Shake(MonoBehaviour runner, RectTransform rt,
            float amplitude = 20f, float duration = 0.3f)
        {
            if (runner == null || rt == null)
                return null;
            return runner.StartCoroutine(ShakeRoutine(rt, amplitude, duration));
        }

        private static System.Collections.IEnumerator ShakeRoutine(RectTransform rt,
            float amplitude, float duration)
        {
            Vector3 basePos = rt.localPosition;
            float elapsed = 0f;
            float seed = UnityEngine.Random.value * 100f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                float decay = 1f - t;
                float offsetX = Mathf.PerlinNoise(seed, elapsed * 30f) - 0.5f;
                float offsetY = Mathf.PerlinNoise(seed + 100f, elapsed * 30f) - 0.5f;
                float mag = amplitude * decay * 2f;

                rt.localPosition = basePos + new Vector3(offsetX * mag, offsetY * mag, 0f);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            rt.localPosition = basePos;
        }
    }

    public static class Haptics
    {
        private const string HapticsEnabledKey = "haptics_enabled";

        private static bool _isEnabled = PlayerPrefs.GetInt(HapticsEnabledKey, 1) == 1;

        public static bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                PlayerPrefs.SetInt(HapticsEnabledKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static void VibrateLight() => Vibrate(50);

        public static void VibrateStrong() => Vibrate(350);

        private static void Vibrate(int milliseconds)
        {
            if (!_isEnabled || !Application.isMobilePlatform)
                return;

            try
            {
                if (Application.platform == RuntimePlatform.Android && milliseconds > 0)
                    VibrateAndroid(milliseconds);
                else
                    Handheld.Vibrate();
            }
            catch (Exception)
            {
            }
        }

        private static void VibrateAndroid(int milliseconds)
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
            {
                vibrator?.Call("vibrate", (long)milliseconds);
            }
        }
    }
}
