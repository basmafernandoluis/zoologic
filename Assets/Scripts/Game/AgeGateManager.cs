using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Zoologic.Localization;

namespace Zoologic
{
    public static class AgeGateManager
    {
        private const string PrefsKey = "zoologic_age_band";
        private const int Unknown = 0;
        private const int Under5 = 1;
        private const int Over5 = 2;

        private static GameObject _panelRoot;

        public static bool HasChosen => PlayerPrefs.GetInt(PrefsKey, Unknown) != Unknown;
        public static bool IsUnder5 => PlayerPrefs.GetInt(PrefsKey, Unknown) == Under5;
        public static bool AreAdsAllowed => HasChosen && !IsUnder5;

        public static void ResetChoice()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                return;
            }
            if (EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
                EventSystem.current.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        public static void ShowIfNeeded(Canvas canvas, Action<bool> onDone)
        {
            if (HasChosen) { onDone?.Invoke(IsUnder5); return; }
            Show(canvas, onDone);
        }

        public static void Show(Canvas canvas, Action<bool> onDone)
        {
            if (canvas == null) { onDone?.Invoke(IsUnder5); return; }
            if (_panelRoot != null) UnityEngine.Object.Destroy(_panelRoot);
            EnsureEventSystem();

            var fontTitle = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Bold SDF");
            var fontBody = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Regular SDF");

            _panelRoot = new GameObject("AgeGateRoot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _panelRoot.transform.SetParent(canvas.transform, false);
            var rootRect = _panelRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            var rootImg = _panelRoot.GetComponent<Image>();
            rootImg.sprite = Resources.Load<Sprite>("UI/Background_PawPattern");
            rootImg.type = rootImg.sprite != null ? Image.Type.Tiled : Image.Type.Simple;
            rootImg.color = new Color(0.55f, 0.38f, 0.22f, 1f);
            rootImg.raycastTarget = true;
            _panelRoot.transform.SetAsLastSibling();

            // Halo chaud derrière la carte.
            var haloGO = new GameObject("Halo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            haloGO.transform.SetParent(_panelRoot.transform, false);
            var haloRect = haloGO.GetComponent<RectTransform>();
            haloRect.anchorMin = new Vector2(0.5f, 0.5f);
            haloRect.anchorMax = new Vector2(0.5f, 0.5f);
            haloRect.pivot = new Vector2(0.5f, 0.5f);
            haloRect.sizeDelta = new Vector2(1000f, 1000f);
            haloRect.anchoredPosition = Vector2.zero;
            var haloImg = haloGO.GetComponent<Image>();
            haloImg.sprite = CreateRadialSprite();
            haloImg.type = Image.Type.Simple;
            haloImg.color = new Color(1f, 0.85f, 0.40f, 0.30f);
            haloImg.raycastTarget = false;
            haloGO.AddComponent<HaloPulse>();

            var card = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            card.transform.SetParent(_panelRoot.transform, false);
            var cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(860f, 800f);
            cardRect.anchoredPosition = Vector2.zero;
            var cardImg = card.GetComponent<Image>();
            cardImg.sprite = Resources.Load<Sprite>("UI/Carte parchemin");
            cardImg.type = Image.Type.Simple;
            cardImg.preserveAspect = true;
            cardImg.color = Color.white;
            cardImg.raycastTarget = true;
            var cardShadow = card.AddComponent<Shadow>();
            cardShadow.effectColor = new Color(0.18f, 0.11f, 0.06f, 0.35f);
            cardShadow.effectDistance = new Vector2(0f, -10f);
            card.AddComponent<CardPop>();

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(70, 70, 60, 50);
            vlg.spacing = 18f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;

            AddText(card.transform, LocalizationManager.Get("age.title"), fontTitle, 48, FontStyles.Bold, new Color(0.29f, 0.18f, 0.10f), 72f);
            AddText(card.transform, LocalizationManager.Get("age.subtitle"), fontBody, 23, FontStyles.Normal, new Color(0.50f, 0.42f, 0.35f), 88f);

            // Deux lignes : grande 6+ en premier, petite 5- en dessous.
            // 5 ans reste visible et fonctionnel (conformité Families).
            var rowGO = new GameObject("Choices", typeof(RectTransform));
            rowGO.transform.SetParent(card.transform, false);
            var rowVLG = rowGO.AddComponent<VerticalLayoutGroup>();
            rowVLG.spacing = 14f;
            rowVLG.childAlignment = TextAnchor.MiddleCenter;
            rowVLG.childForceExpandWidth = false;
            rowVLG.childForceExpandHeight = false;
            rowVLG.childControlWidth = true;
            rowVLG.childControlHeight = true;
            var rowLE = rowGO.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 300f;
            rowLE.flexibleWidth = 1f;

            var overBtn = CreateChoiceButton(rowGO.transform, LocalizationManager.Get("age.over5"), JellyUI.ButtonGreen, GetMaskot(1), fontTitle, true, 700f, 170f);
            overBtn.onClick.AddListener(() => Choose(false, onDone));
            var underBtn = CreateChoiceButton(rowGO.transform, LocalizationManager.Get("age.under5"), JellyUI.ButtonGrey, GetMaskot(0), fontTitle, false, 540f, 110f);
            underBtn.onClick.AddListener(() => Choose(true, onDone));
        }

        /// <summary>Dégradé radial blanc (teinté or à l'usage) pour le halo.</summary>
        private static Sprite _radialSprite;
        private static Sprite CreateRadialSprite()
        {
            if (_radialSprite != null) return _radialSprite;
            const int res = 128;
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            float center = (res - 1) * 0.5f;
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = (1f - d) * (1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            _radialSprite = Sprite.Create(tex, new Rect(0f, 0f, res, res), new Vector2(0.5f, 0.5f));
            return _radialSprite;
        }

        private class HaloPulse : MonoBehaviour
        {
            private void Update()
            {
                if (this == null) return;
                float t = (Mathf.Sin(Time.unscaledTime * 2f) + 1f) * 0.5f;
                transform.localScale = Vector3.one * (1f + t * 0.04f);
                var img = GetComponent<Image>();
                if (img != null)
                    img.color = new Color(1f, 0.85f, 0.40f, Mathf.Lerp(0.22f, 0.34f, t));
            }
        }

        private class CardPop : MonoBehaviour
        {
            private void OnEnable()
            {
                StartCoroutine(PopRoutine());
            }
            private System.Collections.IEnumerator PopRoutine()
            {
                transform.localScale = Vector3.zero;
                yield return null;
                float d = 0.32f;
                float e = 0f;
                while (e < d)
                {
                    e += Time.unscaledDeltaTime;
                    float s = Easing.EaseOutBack(Mathf.Clamp01(e / d));
                    transform.localScale = new Vector3(s, s, s);
                    yield return null;
                }
                transform.localScale = Vector3.one;
                Destroy(this);
            }
        }

        private static void Choose(bool under5, Action<bool> onDone)
        {
            PlayerPrefs.SetInt(PrefsKey, under5 ? Under5 : Over5);
            PlayerPrefs.Save();
            if (_panelRoot != null) { UnityEngine.Object.Destroy(_panelRoot); _panelRoot = null; }
            try { AdMobManager.Instance?.OnAgeBandChosen(under5); } catch { }
            try { onDone?.Invoke(under5); } catch (Exception e) { Debug.LogError("[AgeGate] onDone exception: " + e); }
        }

        private static void AddText(Transform parent, string text, TMP_FontAsset font, int size, FontStyles style, Color color, float height)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = size - 8f;
            tmp.fontSizeMax = size;
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.flexibleWidth = 1f;
            LocalizationManager.ApplyTo(tmp);
        }

        private static Sprite _maskot0;
        private static Sprite _maskot1;
        private static bool _maskotsLoaded;

        /// <summary>Mascottes gate (maskot_0 poussin, maskot_1 ours).</summary>
        private static Sprite GetMaskot(int index)
        {
            if (!_maskotsLoaded)
            {
                _maskotsLoaded = true;
                try
                {
                    var all = Resources.LoadAll<Sprite>("UI/maskot");
                    if (all != null)
                    {
                        foreach (var s in all)
                        {
                            if (s == null) continue;
                            if (s.name == "maskot_0") _maskot0 = s;
                            else if (s.name == "maskot_1") _maskot1 = s;
                        }
                    }
                }
                catch { }
            }
            return index == 0 ? _maskot0 : _maskot1;
        }

        private static Button CreateChoiceButton(Transform parent, string label, Sprite jelly, Sprite icon, TMP_FontAsset font, bool featured = false, float width = 0f, float height = 150f)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = jelly;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
            var btn = go.GetComponent<Button>();
            JellyUI.ApplyJellyButton(btn, img,
                jelly, JellyUI.ButtonYellow, JellyUI.ButtonRed, JellyUI.ButtonGrey);
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0.20f, 0.12f, 0.07f, 0.30f);
            sh.effectDistance = new Vector2(0f, -5f);
            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(go.transform, false);
            var contentRect = contentGO.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = new Vector2(12f, 8f);
            contentRect.offsetMax = new Vector2(-12f, -8f);
            var hlg = contentGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childControlWidth = false;
            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGO.transform.SetParent(contentGO.transform, false);
            var iconImg = iconGO.GetComponent<Image>();
            iconImg.sprite = icon;
            iconImg.type = Image.Type.Simple;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            var iconLE = iconGO.AddComponent<LayoutElement>();
            iconLE.preferredWidth = featured ? 76f : 56f;
            iconLE.preferredHeight = featured ? 76f : 56f;
            var txtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            txtGO.transform.SetParent(contentGO.transform, false);
            var txt = txtGO.GetComponent<TextMeshProUGUI>();
            txt.font = font;
            txt.text = label;
            txt.fontSize = featured ? 30 : 25;
            txt.fontStyle = FontStyles.Bold;
            txt.color = featured ? Color.white : new Color(1f, 1f, 1f, 0.85f);
            txt.alignment = TextAlignmentOptions.Center;
            txt.raycastTarget = false;
            txt.enableAutoSizing = true;
            txt.fontSizeMin = 18;
            txt.fontSizeMax = featured ? 30 : 25;
            txt.outlineWidth = 0.12f;
            txt.outlineColor = new Color(0f, 0f, 0f, 0.30f);
            var txtLE = txtGO.AddComponent<LayoutElement>();
            txtLE.flexibleWidth = 1f;
            LocalizationManager.ApplyTo(txt);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            if (width > 0f)
            {
                le.preferredWidth = width;
                le.flexibleWidth = 0f;
            }
            else
            {
                le.flexibleWidth = featured ? 1.25f : 1f;
            }
            if (featured)
            {
                var glow = go.AddComponent<Outline>();
                glow.effectColor = new Color(1f, 0.85f, 0.30f, 0.9f);
                glow.effectDistance = new Vector2(3f, -3f);
                go.AddComponent<FeaturedPulse>();
            }
            else
            {
                img.color = new Color(0.92f, 0.92f, 0.94f, 1f);
            }
            return btn;
        }

        /// <summary>Pulse doux du bouton mis en avant.</summary>
        private class FeaturedPulse : MonoBehaviour
        {
            private void Update()
            {
                if (this == null) return;
                float s = 1f + Mathf.Sin(Time.unscaledTime * 2.6f) * 0.025f;
                transform.localScale = new Vector3(s, s, s);
            }
        }
    }
}
