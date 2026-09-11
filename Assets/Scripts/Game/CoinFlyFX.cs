using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Zoologic
{
    /// <summary>
    /// Envol de pièces partagé : N pièces partent d'un point (bouton, pilule)
    /// et volent jusqu'à l'icône pièces du HUD (ou haut d'écran en repli),
    /// avec punch de la cible + son. Visuel uniquement, sans logique.
    /// </summary>
    public static class CoinFlyFX
    {
        public static void Play(Canvas canvas, Transform from, int count = 8, Action onDone = null)
        {
            var runner = SFXManager.Instance;
            if (runner == null || canvas == null || from == null)
            {
                onDone?.Invoke();
                return;
            }
            runner.StartCoroutine(FlyRoutine(canvas, from.position, count, onDone));
        }

        public static void Play(Canvas canvas, Vector3 fromWorld, int count = 8, Action onDone = null)
        {
            var runner = SFXManager.Instance;
            if (runner == null || canvas == null)
            {
                onDone?.Invoke();
                return;
            }
            runner.StartCoroutine(FlyRoutine(canvas, fromWorld, count, onDone));
        }

        private static IEnumerator FlyRoutine(Canvas canvas, Vector3 startWorld, int count, Action onDone)
        {
            GameObject targetGO = GameObject.Find("CoinNombre")
                ?? GameObject.Find("EconomyPill")
                ?? GameObject.Find("CoinIcone");
            Vector3 targetWorld = targetGO != null ? targetGO.transform.position
                : new Vector3(Screen.width * 0.5f, Screen.height - 120f, 0f);

            var flyRoot = new GameObject("CoinFlyFX", typeof(RectTransform));
            flyRoot.transform.SetParent(canvas.transform, false);
            var flyRect = (RectTransform)flyRoot.transform;
            flyRect.anchorMin = Vector2.zero; flyRect.anchorMax = Vector2.one;
            flyRect.offsetMin = Vector2.zero; flyRect.offsetMax = Vector2.zero;
            flyRoot.transform.SetAsLastSibling();

            Sprite coinSprite = Resources.Load<Sprite>("UI/coin");
            const float dur = 0.8f;
            const float stagger = 0.06f;
            var runner = SFXManager.Instance;
            for (int i = 0; i < Mathf.Max(1, count); i++)
            {
                var go = new GameObject($"FlyCoin{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(flyRoot.transform, false);
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = new Vector2(40f, 40f);
                rt.position = startWorld;
                var img = go.GetComponent<Image>();
                img.sprite = coinSprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
                go.SetActive(false);
                runner.StartCoroutine(FlyOne(rt, startWorld, targetWorld, i * stagger, dur));
                try { SFXManager.Instance.PlayUnlock(); } catch { }
            }

            yield return new WaitForSecondsRealtime(stagger * (Mathf.Max(1, count) - 1) + dur + 0.1f);
            if (flyRoot != null) UnityEngine.Object.Destroy(flyRoot);
            try { SFXManager.Instance.PlaySuccess(); } catch { }
            Haptics.VibrateLight();
            if (targetGO != null && runner != null)
            {
                try { Punch.Scale(runner, (RectTransform)targetGO.transform, 1.22f, 0.28f); } catch { }
                var hud = UnityEngine.Object.FindFirstObjectByType<GameHUD>();
                if (hud != null)
                {
                    try { hud.RefreshCoins(); } catch { }
                }
            }
            try { onDone?.Invoke(); } catch (Exception e) { Debug.LogError("[CoinFlyFX] onDone: " + e); }
        }

        private static IEnumerator FlyOne(RectTransform rt, Vector3 from, Vector3 to, float delay, float dur)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            if (rt == null) yield break;
            rt.gameObject.SetActive(true);
            rt.position = from;
            float el = 0f;
            Vector3 ctrl = (from + to) * 0.5f + new Vector3(60f, 220f, 0f);
            while (el < dur)
            {
                float t = Mathf.Clamp01(el / dur);
                float e = Easing.EaseInOutQuad(t);
                Vector3 a = Vector3.Lerp(from, ctrl, e);
                Vector3 b = Vector3.Lerp(ctrl, to, e);
                rt.position = Vector3.Lerp(a, b, e);
                float s = Mathf.Lerp(1.2f, 0.6f, e);
                rt.localScale = new Vector3(s, s, s);
                rt.Rotate(0f, 0f, 540f * Time.unscaledDeltaTime);
                el += Time.unscaledDeltaTime;
                yield return null;
            }
            if (rt != null) rt.gameObject.SetActive(false);
        }
    }
}
