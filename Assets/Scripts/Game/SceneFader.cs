using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Zoologic
{
    /// <summary>
    /// Fondu plein écran réutilisable (fade to black puis fade in), sans package.
    /// Un overlay noir placé au-dessus du canvas, dont l'alpha est animé.
    /// </summary>
    public static class SceneFader
    {
        /// <summary>
        /// Fondu de sortie (fond → noir) puis appelle <paramref name="onDone"/>.
        /// Utilisé avant de charger une scène.
        /// </summary>
        public static void FadeOut(MonoBehaviour runner, Canvas canvas, float duration, System.Action onDone)
        {
            runner.StartCoroutine(FadeOutRoutine(canvas, duration, onDone));
        }

        /// <summary>Fondu d'entrée (noir → transparent), pour révéler une scène.</summary>
        public static void FadeIn(MonoBehaviour runner, Canvas canvas, float duration)
        {
            runner.StartCoroutine(FadeInRoutine(canvas, duration));
        }

        private static IEnumerator FadeOutRoutine(Canvas canvas, float duration, System.Action onDone)
        {
            Image overlay = CreateOverlay(canvas);
            overlay.raycastTarget = true; // bloque les clics pendant la transition

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                SetAlpha(overlay, Easing.EaseInQuad(t));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            SetAlpha(overlay, 1f);

            onDone?.Invoke();
        }

        private static IEnumerator FadeInRoutine(Canvas canvas, float duration)
        {
            Image overlay = CreateOverlay(canvas);
            overlay.raycastTarget = false;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                SetAlpha(overlay, 1f - Easing.EaseOutQuad(t));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            SetAlpha(overlay, 0f);
            Object.Destroy(overlay.gameObject);
        }

        private static Image CreateOverlay(Canvas canvas)
        {
            var go = new GameObject("SceneFade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(canvas.transform, false);
            go.transform.SetAsLastSibling();

            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = false;
            return img;
        }

        private static void SetAlpha(Image img, float a)
        {
            Color c = img.color;
            c.a = Mathf.Clamp01(a);
            img.color = c;
        }
    }
}
