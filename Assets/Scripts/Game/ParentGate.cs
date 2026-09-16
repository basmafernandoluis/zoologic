using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Zoologic.Localization;

namespace Zoologic
{
    public static class ParentGate
    {
        private const float HoldSeconds = 2f;
        private static GameObject _panelRoot;

        public static void Show(Canvas canvas, Action onSuccess, Action onCancel = null)
        {
            if (canvas == null) { onCancel?.Invoke(); return; }
            if (_panelRoot != null) UnityEngine.Object.Destroy(_panelRoot);
            AgeGateManager.EnsureEventSystem();

            var fontTitle = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Bold SDF");
            var fontBody = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Regular SDF");

            _panelRoot = new GameObject("ParentGateRoot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
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
            cardRect.sizeDelta = new Vector2(760f, 0f);
            cardRect.anchoredPosition = Vector2.zero;
            card.GetComponent<Image>().color = new Color(1f, 0.98f, 0.94f, 1f);
            var csf = card.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(40, 40, 40, 40);
            vlg.spacing = 20f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;

            AddText(card.transform, LocalizationManager.Get("parentgate.title"), fontTitle, 36, FontStyles.Bold, new Color(0.29f, 0.18f, 0.10f));
            AddText(card.transform, LocalizationManager.Get("parentgate.hold"), fontBody, 24, FontStyles.Normal, new Color(0.50f, 0.42f, 0.35f));

            var holdGO = new GameObject("HoldButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            holdGO.transform.SetParent(card.transform, false);
            var holdImg = holdGO.GetComponent<Image>();
            holdImg.color = new Color(0.22f, 0.50f, 0.85f, 0.25f);
            holdImg.type = Image.Type.Filled;
            holdImg.fillMethod = Image.FillMethod.Horizontal;
            holdImg.fillOrigin = 0;
            holdImg.fillAmount = 0f;
            var holdBtn = holdGO.GetComponent<Button>();
            holdBtn.targetGraphic = holdImg;
            var holdLE = holdGO.AddComponent<LayoutElement>();
            holdLE.preferredHeight = 84f;
            holdLE.flexibleWidth = 1f;

            var holdTxtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            holdTxtGO.transform.SetParent(holdGO.transform, false);
            var holdTxtRect = holdTxtGO.GetComponent<RectTransform>();
            holdTxtRect.anchorMin = Vector2.zero;
            holdTxtRect.anchorMax = Vector2.one;
            holdTxtRect.offsetMin = Vector2.zero;
            holdTxtRect.offsetMax = Vector2.zero;
            var holdTxt = holdTxtGO.GetComponent<TextMeshProUGUI>();
            holdTxt.font = fontTitle;
            holdTxt.text = LocalizationManager.Get("parentgate.hold_button");
            holdTxt.fontSize = 26;
            holdTxt.fontStyle = FontStyles.Bold;
            holdTxt.color = new Color(0.20f, 0.13f, 0.08f);
            holdTxt.alignment = TextAlignmentOptions.Center;
            holdTxt.raycastTarget = false;
            LocalizationManager.ApplyTo(holdTxt);

            var holder = holdGO.AddComponent<HoldBehaviour>();
            holder.Init(holdImg, () =>
            {
                Close();
                try { onSuccess?.Invoke(); } catch (Exception e) { Debug.LogError("[ParentGate] onSuccess exception: " + e); }
            });

            var cancelGO = new GameObject("CancelButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            cancelGO.transform.SetParent(card.transform, false);
            var cancelImg = cancelGO.GetComponent<Image>();
            cancelImg.color = new Color(0.78f, 0.74f, 0.69f);
            var cancelBtn = cancelGO.GetComponent<Button>();
            cancelBtn.targetGraphic = cancelImg;
            var cancelLE = cancelGO.AddComponent<LayoutElement>();
            cancelLE.preferredHeight = 64f;
            cancelLE.flexibleWidth = 1f;
            var cancelTxtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            cancelTxtGO.transform.SetParent(cancelGO.transform, false);
            var cancelTxtRect = cancelTxtGO.GetComponent<RectTransform>();
            cancelTxtRect.anchorMin = Vector2.zero;
            cancelTxtRect.anchorMax = Vector2.one;
            cancelTxtRect.offsetMin = Vector2.zero;
            cancelTxtRect.offsetMax = Vector2.zero;
            var cancelTxt = cancelTxtGO.GetComponent<TextMeshProUGUI>();
            cancelTxt.font = fontTitle;
            cancelTxt.text = LocalizationManager.Get("parentgate.cancel");
            cancelTxt.fontSize = 24;
            cancelTxt.fontStyle = FontStyles.Bold;
            cancelTxt.color = Color.white;
            cancelTxt.alignment = TextAlignmentOptions.Center;
            cancelTxt.raycastTarget = false;
            LocalizationManager.ApplyTo(cancelTxt);
            cancelBtn.onClick.AddListener(() =>
            {
                Close();
                try { onCancel?.Invoke(); } catch { }
            });
            LocalizationManager.ApplyFontsToScene();
        }

        private static void Close()
        {
            if (_panelRoot != null) { UnityEngine.Object.Destroy(_panelRoot); _panelRoot = null; }
        }

        private static void AddText(Transform parent, string text, TMP_FontAsset font, int size, FontStyles style, Color color)
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
            le.preferredHeight = 70f;
            le.flexibleWidth = 1f;
            LocalizationManager.ApplyTo(tmp);
        }

        private sealed class HoldBehaviour : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
        {
            private Image _fill;
            private Action _onComplete;
            private bool _holding;
            private float _t;

            public void Init(Image fill, Action onComplete)
            {
                _fill = fill;
                _onComplete = onComplete;
            }

            public void OnPointerDown(PointerEventData eventData) { _holding = true; _t = 0f; }
            public void OnPointerUp(PointerEventData eventData) { ResetHold(); }
            public void OnPointerExit(PointerEventData eventData) { ResetHold(); }

            private void ResetHold()
            {
                _holding = false;
                _t = 0f;
                if (_fill != null) _fill.fillAmount = 0f;
            }

            private void Update()
            {
                if (!_holding) return;
                _t += Time.unscaledDeltaTime;
                if (_fill != null) _fill.fillAmount = Mathf.Clamp01(_t / HoldSeconds);
                if (_t >= HoldSeconds)
                {
                    _holding = false;
                    try { _onComplete?.Invoke(); } catch (Exception e) { Debug.LogError("[ParentGate] complete exception: " + e); }
                }
            }
        }
    }
}
