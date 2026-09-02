using UnityEngine;
using UnityEngine.UI;

namespace Zoologic
{
    public sealed class UIHandPointer : MonoBehaviour
    {
        [SerializeField] private Image _handImage;
        [SerializeField] private Sprite _handSprite;

        private RectTransform _rect;
        private Canvas _canvas;
        private RectTransform _target;
        private Vector2 _offset = new Vector2(20f, -40f);
        private Coroutine _bobRoutine;
        private Coroutine _tapRoutine;
        private Coroutine _showRoutine;

        public static UIHandPointer Create(Canvas canvas)
        {
            var go = new GameObject("HandPointer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(canvas.transform, false);
            var comp = go.AddComponent<UIHandPointer>();
            comp._canvas = canvas;
            comp._rect = (RectTransform)go.transform;
            comp._rect.sizeDelta = new Vector2(110f, 110f);
            comp._rect.pivot = new Vector2(0.15f, 0.85f);
            comp._handImage = go.GetComponent<Image>();
            comp._handImage.sprite = comp.LoadHandSprite();
            comp._handImage.raycastTarget = false;
            comp._handImage.preserveAspect = true;
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.28f);
            sh.effectDistance = new Vector2(3f, -3f);
            go.SetActive(false);
            comp._rect.localScale = Vector3.zero;
            return comp;
        }

        private Sprite LoadHandSprite()
        {
            if (_handSprite != null) return _handSprite;
            var s = Resources.Load<Sprite>("UI/hand");
            if (s != null) return s;
            s = Resources.Load<Sprite>("play store/tap");
            if (s != null) return s;
            var tex = Resources.Load<Texture2D>("play store/tap");
            if (tex != null) return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return CreateFallbackSprite();
        }

        public void PointTo(RectTransform targetCell)
        {
            PointTo(targetCell, _offset);
        }

        public void PointTo(RectTransform targetCell, Vector2 offset)
        {
            if (targetCell == null) return;
            _target = targetCell;
            _offset = offset;
            transform.SetAsLastSibling();
            Show();
            UpdatePosition();
            if (_bobRoutine != null) StopCoroutine(_bobRoutine);
            _bobRoutine = StartCoroutine(BobRoutine());
        }

        public void Show()
        {
            if (gameObject.activeSelf && _rect.localScale == Vector3.one) return;
            gameObject.SetActive(true);
            _handImage.raycastTarget = false;
            if (_showRoutine != null) StopCoroutine(_showRoutine);
            _showRoutine = StartCoroutine(ShowRoutine());
        }

        public void Hide()
        {
            if (!gameObject.activeSelf) { _target = null; return; }
            if (_bobRoutine != null) { StopCoroutine(_bobRoutine); _bobRoutine = null; }
            if (_showRoutine != null) StopCoroutine(_showRoutine);
            _showRoutine = StartCoroutine(HideRoutine());
        }

        public void PlayTap()
        {
            if (!gameObject.activeSelf) return;
            if (_tapRoutine != null) StopCoroutine(_tapRoutine);
            _tapRoutine = StartCoroutine(TapRoutine());
        }

        private System.Collections.IEnumerator ShowRoutine()
        {
            float d = 0.15f; float e = 0f;
            _rect.localScale = Vector3.zero;
            while (e < d)
            {
                e += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(e / d);
                float s = Easing.EaseOutBack(t);
                _rect.localScale = new Vector3(s, s, s);
                yield return null;
            }
            _rect.localScale = Vector3.one;
        }

        private System.Collections.IEnumerator HideRoutine()
        {
            float d = 0.15f; float e = 0f;
            Vector3 start = _rect.localScale;
            while (e < d)
            {
                e += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(e / d);
                float s = Mathf.Lerp(1f, 0f, Easing.EaseInQuad(t));
                _rect.localScale = new Vector3(s, s, s);
                yield return null;
            }
            _rect.localScale = Vector3.zero;
            gameObject.SetActive(false);
            _target = null;
        }

        private System.Collections.IEnumerator BobRoutine()
        {
            while (true)
            {
                if (_target != null)
                {
                    Vector2 local = GetTargetAnchored();
                    float bob = Mathf.Sin(Time.time * 3f) * 12f;
                    _rect.anchoredPosition = local + _offset + new Vector2(0f, bob);
                }
                yield return null;
            }
        }

        private System.Collections.IEnumerator TapRoutine()
        {
            float d = 0.18f; float e = 0f;
            Vector3 baseScale = Vector3.one;
            while (e < d)
            {
                e += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(e / d);
                float s;
                if (t < 0.5f) s = Mathf.Lerp(1f, 0.82f, t * 2f);
                else s = Mathf.Lerp(0.82f, 1f, (t - 0.5f) * 2f);
                transform.localScale = baseScale * s;
                yield return null;
            }
            transform.localScale = baseScale;
        }

        private void UpdatePosition()
        {
            if (_target == null || _canvas == null) return;
            Vector2 local = GetTargetAnchored();
            _rect.anchoredPosition = local + _offset;
        }

        private Vector2 GetTargetAnchored()
        {
            if (_target == null || _canvas == null) return Vector2.zero;
            Vector3 world = _target.TransformPoint(_target.rect.center);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(_canvas.worldCamera, world);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_canvas.transform, screen, _canvas.worldCamera, out local);
            return local;
        }

        private static Sprite CreateFallbackSprite()
        {
            int s = 96;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++) tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
            Vector2 c = new Vector2(s * 0.5f, s * 0.55f);
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                Vector2 p = new Vector2(x, y);
                float d = (p - c).magnitude;
                if (d < 28f) tex.SetPixel(x, y, new Color(1f, 0.85f, 0.45f, 1f));
                if (d < 22f) tex.SetPixel(x, y, new Color(1f, 0.93f, 0.70f, 1f));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
        }
    }
}
