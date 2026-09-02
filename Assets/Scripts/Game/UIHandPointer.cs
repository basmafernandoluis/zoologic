using UnityEngine;
using UnityEngine.UI;

namespace Zoologic
{
    public sealed class UIHandPointer : MonoBehaviour
    {
        private RectTransform _rect;
        private Image _image;
        private Canvas _canvas;
        private RectTransform _target;
        private Coroutine _bobRoutine;
        private Vector2 _baseOffset = new Vector2(28f, -42f);

        private static Sprite _handSprite;

        public static UIHandPointer Create(Canvas canvas)
        {
            var go = new GameObject("HandPointer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(canvas.transform, false);
            var p = go.AddComponent<UIHandPointer>();
            p._canvas = canvas;
            p._rect = (RectTransform)go.transform;
            p._rect.sizeDelta = new Vector2(96f, 96f);
            p._rect.pivot = new Vector2(0f, 1f);
            p._image = go.GetComponent<Image>();
            p._image.sprite = GetHandSprite();
            p._image.raycastTarget = false;
            p._image.preserveAspect = true;
            go.SetActive(false);
            return p;
        }

        public void PointTo(RectTransform target)
        {
            if (target == null) return;
            _target = target;
            gameObject.SetActive(true);
            UpdatePosition();
            if (_bobRoutine != null) StopCoroutine(_bobRoutine);
            _bobRoutine = StartCoroutine(BobRoutine());
        }

        public void Hide()
        {
            if (_bobRoutine != null) { StopCoroutine(_bobRoutine); _bobRoutine = null; }
            gameObject.SetActive(false);
            _target = null;
        }

        public void PlayTap()
        {
            if (!gameObject.activeSelf) return;
            StopCoroutine(TapRoutine());
            StartCoroutine(TapRoutine());
        }

        private void UpdatePosition()
        {
            if (_target == null || _canvas == null) return;
            Vector3 world = _target.TransformPoint(_target.rect.center);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(_canvas.worldCamera, world);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_canvas.transform, screen, _canvas.worldCamera, out local);
            _rect.anchoredPosition = local + _baseOffset;
        }

        private System.Collections.IEnumerator BobRoutine()
        {
            Vector2 basePos = _rect.anchoredPosition;
            while (true)
            {
                if (_target != null) UpdatePosition();
                basePos = _rect.anchoredPosition - _baseOffset + new Vector2(0f, 0f);
                float t = Mathf.Sin(Time.unscaledTime * 3.2f) * 8f;
                _rect.anchoredPosition = basePos + _baseOffset + new Vector2(0f, t);
                yield return null;
            }
        }

        private System.Collections.IEnumerator TapRoutine()
        {
            float d = 0.14f; float e = 0f;
            Vector3 baseScale = Vector3.one;
            while (e < d)
            {
                e += Time.unscaledDeltaTime;
                float s = 1f - Mathf.Sin(e / d * Mathf.PI) * 0.22f;
                transform.localScale = baseScale * s;
                yield return null;
            }
            transform.localScale = baseScale;
        }

        private static Sprite GetHandSprite()
        {
            if (_handSprite != null) return _handSprite;
            _handSprite = Resources.Load<Sprite>("UI/hand");
            if (_handSprite != null) return _handSprite;
            _handSprite = CreateHandSprite();
            return _handSprite;
        }

        private static Sprite CreateHandSprite()
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
            for (int y = 0; y < 14; y++) for (int x = s/2 - 6; x < s/2 + 6; x++) tex.SetPixel(x, y + 58, new Color(1f, 0.85f, 0.45f, 1f));
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
        }
    }
}
