using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Zoologic
{
    /// <summary>
    /// Confettis procéduraux (aucun package, aucun prefab). Génère sur demande une
    /// pluie de petites particules rectangulaires multicolores qui tombent sur tout
    /// le canvas puis disparaissent, pour célébrer la victoire.
    ///
    /// Légèreté : un seul objet avec une texture 1x1 blanche (teintée par Image.color)
    /// et des enfants rectangulaires repositionnés à la main chaque frame.
    /// </summary>
    public static class ConfettiHelper
    {
        private const int DefaultCount = 60;

        /// <summary>
        /// Lance une pluie de confettis sur tout le canvas donné.
        /// <paramref name="runner"/> sert d'hôte aux coroutines (souvent le contrôleur).
        /// </summary>
        public static void Burst(MonoBehaviour runner, Canvas canvas, int count = DefaultCount)
        {
            if (runner == null || canvas == null)
                return;
            runner.StartCoroutine(BurstRoutine(canvas, count));
        }

        private static IEnumerator BurstRoutine(Canvas canvas, int count)
        {
            var root = new GameObject("Confetti", typeof(RectTransform), typeof(CanvasRenderer));
            root.transform.SetParent(canvas.transform, false);

            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var pivot = canvas.GetComponent<RectTransform>();
            float width = pivot.rect.width;
            float height = pivot.rect.height;

            var parts = new ParticleData[count];
            var image = CreateParticleTexture();

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("P", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(root.transform, false);
                var rt = (RectTransform)go.transform;
                float size = Random.Range(10f, 22f);
                rt.sizeDelta = new Vector2(size, size * Random.Range(0.4f, 0.9f));

                var img = go.GetComponent<Image>();
                img.sprite = image;
                img.color = GetRandomColor();
                img.raycastTarget = false;

                parts[i] = new ParticleData(rt, Random.Range(0f, 1f) * width, Random.Range(0f, 0.25f) * height,
                    Random.Range(160f, 380f), Random.Range(-30f, 30f), Random.Range(120f, 280f) * (Random.Range(0f, 1f) > 0.5f ? 1f : -1f),
                    Random.Range(0.02f, 0.05f), Random.Range(-6f, 6f));
            }

            float elapsed = 0f;
            const float duration = 3f;

            while (elapsed < duration)
            {
                for (int i = 0; i < parts.Length; i++)
                {
                    var p = parts[i];
                    p.Update(Time.unscaledDeltaTime, width, height, elapsed / duration);
                }
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            Object.Destroy(root);
        }

        private static Sprite _pixel;

        private static Sprite CreateParticleTexture()
        {
            if (_pixel != null)
                return _pixel;

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _pixel = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            return _pixel;
        }

        private static Color GetRandomColor()
        {
            Color[] palette =
            {
                new Color(1f, 0.55f, 0.55f), // rouge clair
                new Color(1f, 0.78f, 0.35f), // orange
                new Color(1f, 0.95f, 0.40f), // jaune
                new Color(0.50f, 0.85f, 0.50f), // vert
                new Color(0.45f, 0.75f, 0.95f), // bleu
                new Color(0.75f, 0.55f, 0.95f), // violet
                new Color(1f, 0.60f, 0.85f), // rose
            };
            return palette[Random.Range(0, palette.Length)];
        }

        private sealed class ParticleData
        {
            public RectTransform Rt;
            public float X;
            public float Y;
            public float FallSpeed;
            public float Drift;
            public float RotSpeed;
            public float Alpha;
            public float AlphaFade;
            private float _angle;
            private Image _img;

            public ParticleData(RectTransform rt, float x, float y, float fallSpeed, float drift,
                float rotSpeed, float alphaFade, float startAngle)
            {
                Rt = rt;
                X = x;
                Y = y;
                FallSpeed = fallSpeed;
                Drift = drift;
                RotSpeed = rotSpeed;
                Alpha = 1f;
                AlphaFade = alphaFade;
                _angle = startAngle;
                _img = rt.GetComponent<Image>();
                _img.color = ApplyAlpha(_img.color, Alpha);
            }

            public void Update(float dt, float width, float height, float globalT)
            {
                X += Drift * dt;
                Y -= FallSpeed * dt;
                _angle += RotSpeed * dt;

                if (Y < -50f)
                {
                    Y = height + 50f;
                    X = Random.Range(0f, width);
                }

                Rt.anchoredPosition = new Vector2(X, Y);
                Rt.localRotation = Quaternion.Euler(0f, 0f, _angle);

                // Fade lent vers la fin pour une disparition douce.
                Alpha = Mathf.Max(0f, Alpha - AlphaFade * dt);
                if (Alpha > 0f)
                    _img.color = ApplyAlpha(_img.color, Alpha);
            }

            private static Color ApplyAlpha(Color c, float a)
            {
                c.a = Mathf.Clamp01(a);
                return c;
            }
        }
    }
}
