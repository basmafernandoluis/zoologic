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
            rootImg.color = new Color(0.24f, 0.16f, 0.10f, 0.75f);
            rootImg.raycastTarget = true;
            _panelRoot.transform.SetAsLastSibling();

            var card = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            card.transform.SetParent(_panelRoot.transform, false);
            var cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(820f, 0f);
            cardRect.anchoredPosition = Vector2.zero;
            var cardImg = card.GetComponent<Image>();
            cardImg.color = new Color(1f, 0.98f, 0.94f, 1f);
            var csf = card.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(40, 40, 40, 40);
            vlg.spacing = 20f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;

            AddText(card.transform, LocalizationManager.Get("age.title"), fontTitle, 40, FontStyles.Bold, new Color(0.29f, 0.18f, 0.10f), 64f);
            AddText(card.transform, LocalizationManager.Get("age.subtitle"), fontBody, 24, FontStyles.Normal, new Color(0.50f, 0.42f, 0.35f), 80f);

            var underBtn = CreateChoiceButton(card.transform, LocalizationManager.Get("age.under5"), new Color(0.22f, 0.50f, 0.85f), fontTitle);
            underBtn.onClick.AddListener(() => Choose(true, onDone));
            var overBtn = CreateChoiceButton(card.transform, LocalizationManager.Get("age.over5"), new Color(0.22f, 0.65f, 0.30f), fontTitle);
            overBtn.onClick.AddListener(() => Choose(false, onDone));
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
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.flexibleWidth = 1f;
            LocalizationManager.ApplyTo(tmp);
        }

        private static Button CreateChoiceButton(Transform parent, string label, Color bg, TMP_FontAsset font)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = bg;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var txtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            txtGO.transform.SetParent(go.transform, false);
            var txtRect = txtGO.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = new Vector2(16f, 8f);
            txtRect.offsetMax = new Vector2(-16f, -8f);
            var txt = txtGO.GetComponent<TextMeshProUGUI>();
            txt.font = font;
            txt.text = label;
            txt.fontSize = 28;
            txt.fontStyle = FontStyles.Bold;
            txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.Center;
            txt.raycastTarget = false;
            LocalizationManager.ApplyTo(txt);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 84f;
            le.flexibleWidth = 1f;
            return btn;
        }
    }
}
