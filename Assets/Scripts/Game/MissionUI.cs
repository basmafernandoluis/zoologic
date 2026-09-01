using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zoologic
{
    public static class MissionUI
    {
        private static GameObject _root;
        private static TMP_FontAsset _fontTitle;
        private static TMP_FontAsset _fontBody;

        public static void Show(Canvas canvas)
        {
            if (_root != null) Object.Destroy(_root);
            _fontTitle = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Bold SDF");
            _fontBody = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Regular SDF");

            _root = new GameObject("MissionsRoot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _root.transform.SetParent(canvas.transform, false);
            var rr = _root.GetComponent<RectTransform>();
            rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one;
            rr.offsetMin = Vector2.zero; rr.offsetMax = Vector2.zero;
            var ri = _root.GetComponent<Image>();
            ri.color = new Color(0.24f, 0.16f, 0.10f, 0.62f);
            ri.raycastTarget = true;

            var card = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            card.transform.SetParent(_root.transform, false);
            var cr = card.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0.5f, 0.5f); cr.anchorMax = new Vector2(0.5f, 0.5f);
            cr.pivot = new Vector2(0.5f, 0.5f);
            cr.sizeDelta = new Vector2(860f, 740f);
            var ci = card.GetComponent<Image>();
            ci.sprite = CreateRounded(256, 0.18f);
            ci.type = Image.Type.Simple;
            ci.color = new Color(1f, 0.98f, 0.94f, 1f);
            var cardShadow = card.AddComponent<Shadow>();
            cardShadow.effectColor = new Color(0.18f, 0.11f, 0.06f, 0.30f);
            cardShadow.effectDistance = new Vector2(0f, -10f);

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(28, 28, 28, 28);
            vlg.spacing = 14f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var titleGO = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titleGO.transform.SetParent(card.transform, false);
            var title = titleGO.GetComponent<TextMeshProUGUI>();
            title.font = _fontTitle; title.text = "Missions"; title.fontSize = 38; title.fontStyle = FontStyles.Bold;
            title.color = new Color(0.29f, 0.18f, 0.10f); title.alignment = TextAlignmentOptions.Center;
            var tle = titleGO.AddComponent<LayoutElement>(); tle.preferredHeight = 48f;

            var list = MissionManager.GetMissions();
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                var row = new GameObject($"Row{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                row.transform.SetParent(card.transform, false);
                var rowRect = row.GetComponent<RectTransform>();
                var rowLE = row.AddComponent<LayoutElement>(); rowLE.preferredHeight = 128f;
                var rowImg = row.GetComponent<Image>();
                rowImg.sprite = CreateRounded(128, 0.22f);
                rowImg.type = Image.Type.Simple;
                rowImg.color = m.IsCompleted && !m.claimed ? new Color(1f, 0.91f, 0.62f) : new Color(1f, 0.96f, 0.88f);
                if (m.IsCompleted && !m.claimed)
                {
                    var ol = row.AddComponent<Outline>();
                    ol.effectColor = new Color(0.95f, 0.70f, 0.20f);
                    ol.effectDistance = new Vector2(2f, -2f);
                }

                var hlg = row.AddComponent<HorizontalLayoutGroup>();
                hlg.padding = new RectOffset(18, 18, 12, 12);
                hlg.spacing = 12f;
                hlg.childAlignment = TextAnchor.MiddleLeft;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
                hlg.childControlHeight = true;

                var left = new GameObject("Left", typeof(RectTransform));
                left.transform.SetParent(row.transform, false);
                var leftLE = left.AddComponent<LayoutElement>(); leftLE.flexibleWidth = 1f;
                var leftVLG = left.AddComponent<VerticalLayoutGroup>();
                leftVLG.spacing = 6f;
                leftVLG.childForceExpandWidth = true;

                var labelGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                labelGO.transform.SetParent(left.transform, false);
                var label = labelGO.GetComponent<TextMeshProUGUI>();
                label.font = _fontTitle; label.text = m.Label; label.fontSize = 22; label.fontStyle = FontStyles.Bold;
                label.color = new Color(0.29f, 0.18f, 0.10f); label.alignment = TextAlignmentOptions.MidlineLeft;
                var labLE = labelGO.AddComponent<LayoutElement>(); labLE.preferredHeight = 26f;

                var barBG = new GameObject("BarBG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                barBG.transform.SetParent(left.transform, false);
                var barBGRect = barBG.GetComponent<RectTransform>();
                var barBGLE = barBG.AddComponent<LayoutElement>(); barBGLE.preferredHeight = 18f;
                var barBGImg = barBG.GetComponent<Image>();
                barBGImg.sprite = CreateRounded(64, 0.35f);
                barBGImg.type = Image.Type.Simple;
                barBGImg.color = new Color(0.87f, 0.80f, 0.71f);
                var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                fillGO.transform.SetParent(barBG.transform, false);
                var fillRect = fillGO.GetComponent<RectTransform>();
                fillRect.anchorMin = new Vector2(0f, 0f); fillRect.anchorMax = new Vector2(0f, 1f);
                fillRect.pivot = new Vector2(0f, 0.5f);
                fillRect.offsetMin = new Vector2(2f, 2f); fillRect.offsetMax = new Vector2(-2f, -2f);
                float frac = Mathf.Clamp01((float)m.progress / Mathf.Max(1, m.target));
                fillRect.anchorMax = new Vector2(frac, 1f);
                var fillImg = fillGO.GetComponent<Image>();
                fillImg.sprite = CreateRounded(64, 0.35f);
                fillImg.type = Image.Type.Simple;
                fillImg.color = m.IsCompleted ? new Color(0.22f, 0.65f, 0.30f) : new Color(0.95f, 0.63f, 0.16f);

                var progGO = new GameObject("Prog", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                progGO.transform.SetParent(left.transform, false);
                var prog = progGO.GetComponent<TextMeshProUGUI>();
                prog.font = _fontBody; prog.text = $"{m.progress}/{m.target}"; prog.fontSize = 18;
                prog.color = new Color(0.50f, 0.42f, 0.35f); prog.alignment = TextAlignmentOptions.MidlineLeft;
                var progLE = progGO.AddComponent<LayoutElement>(); progLE.preferredHeight = 18f;

                int idx = i;
                var btnGO = new GameObject("Btn", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                btnGO.transform.SetParent(row.transform, false);
                var btnRect = btnGO.GetComponent<RectTransform>();
                btnRect.sizeDelta = new Vector2(160f, 52f);
                var btnImg = btnGO.GetComponent<Image>();
                btnImg.sprite = KenneyUI.Button(canClaim ? "Green" : "Grey") ?? CreateRounded(128, 0.35f);
                btnImg.type = Image.Type.Simple;
                bool canClaim = m.IsCompleted && !m.claimed;
                btnImg.color = canClaim ? new Color(0.22f, 0.65f, 0.30f) : new Color(0.85f, 0.79f, 0.71f);
                var btnShadow = btnGO.AddComponent<Shadow>();
                btnShadow.effectColor = new Color(0.20f, 0.12f, 0.07f, 0.20f);
                btnShadow.effectDistance = new Vector2(0f, -3f);
                var btn = btnGO.GetComponent<Button>();
                btn.targetGraphic = btnImg;
                btn.interactable = canClaim;
                var btxtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                btxtGO.transform.SetParent(btnGO.transform, false);
                var btxtRect = btxtGO.GetComponent<RectTransform>();
                btxtRect.anchorMin = Vector2.zero; btxtRect.anchorMax = Vector2.one;
                btxtRect.offsetMin = Vector2.zero; btxtRect.offsetMax = Vector2.zero;
                var btxt = btxtGO.GetComponent<TextMeshProUGUI>();
                btxt.font = _fontTitle;
                btxt.text = canClaim ? $"+{m.reward}" : (m.claimed ? "Fait" : $"+{m.reward}");
                btxt.fontSize = 18; btxt.fontStyle = FontStyles.Bold;
                btxt.color = canClaim ? Color.white : new Color(0.60f, 0.48f, 0.35f);
                btxt.alignment = TextAlignmentOptions.Center;
                var ble = btnGO.AddComponent<LayoutElement>(); ble.preferredWidth = 110f; ble.preferredHeight = 52f;
                if (canClaim)
                {
                    btn.onClick.AddListener(() =>
                    {
                        if (MissionManager.TryClaim(idx))
                        {
                            SFXManager.Instance.PlayUnlock();
                            Object.Destroy(_root); _root = null;
                            var c = Object.FindFirstObjectByType<Canvas>();
                            if (c != null) Show(c);
                            if (c != null)
                            {
                                var t = new GameObject("Toast", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                                t.transform.SetParent(c.transform, false);
                                var tr = t.GetComponent<RectTransform>();
                                tr.anchorMin = new Vector2(0.5f, 0.5f); tr.anchorMax = new Vector2(0.5f, 0.5f);
                                tr.sizeDelta = new Vector2(460f, 56f); tr.anchoredPosition = new Vector2(0f, -260f);
                                var ti = t.GetComponent<Image>(); ti.sprite = CreateRounded(128, 0.35f); ti.color = new Color(0f, 0f, 0f, 0.82f);
                                var ttGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                                ttGO.transform.SetParent(t.transform, false);
                                var ttr = ttGO.GetComponent<RectTransform>(); ttr.anchorMin = Vector2.zero; ttr.anchorMax = Vector2.one;
                                ttr.offsetMin = Vector2.zero; ttr.offsetMax = Vector2.zero;
                                var tt = ttGO.GetComponent<TextMeshProUGUI>(); tt.font = _fontTitle; tt.text = $"+{m.reward} pièces !"; tt.fontSize = 24; tt.color = Color.white; tt.alignment = TextAlignmentOptions.Center;
                                Object.Destroy(t, 1.4f);
                            }
                        }
                    });
                }
            }

            var closeBtn = new GameObject("Close", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            closeBtn.transform.SetParent(card.transform, false);
            var closeRect = closeBtn.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f); closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f); closeRect.sizeDelta = new Vector2(44f, 44f);
            closeRect.anchoredPosition = new Vector2(-10f, -10f);
            var closeLayout = closeBtn.AddComponent<LayoutElement>();
            closeLayout.ignoreLayout = true;
            var closeImg = closeBtn.GetComponent<Image>();
            closeImg.sprite = KenneyUI.Cross() ?? CreateRounded(64, 0.5f);
            closeImg.color = new Color(0f, 0f, 0f, 0.08f);
            var closeBtnComp = closeBtn.GetComponent<Button>();
            closeBtnComp.targetGraphic = closeImg;
            closeBtnComp.onClick.AddListener(() => { Object.Destroy(_root); _root = null; });
            var closeTxt = new GameObject("X", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            closeTxt.transform.SetParent(closeBtn.transform, false);
            var closeTxtRect = closeTxt.GetComponent<RectTransform>();
            closeTxtRect.anchorMin = Vector2.zero; closeTxtRect.anchorMax = Vector2.one;
            closeTxtRect.offsetMin = Vector2.zero; closeTxtRect.offsetMax = Vector2.zero;
            var closeT = closeTxt.GetComponent<TextMeshProUGUI>();
            closeT.font = _fontTitle; closeT.text = "×"; closeT.fontSize = 32;
            closeT.color = new Color(0.40f, 0.40f, 0.42f); closeT.alignment = TextAlignmentOptions.Center;
            _root.AddComponent<ClickOutsideCloser>().Init(_root);
        }

        private class ClickOutsideCloser : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler
        {
            private GameObject _root;
            public void Init(GameObject root) => _root = root;
            public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
            {
                if (eventData.pointerCurrentRaycast.gameObject == _root)
                    Destroy(_root);
            }
        }

        private static Sprite CreateRounded(int res, float ratio)
        {
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
            float half = (res - 1) * 0.5f; float rad = res * ratio; float inner = half - rad;
            for (int y = 0; y < res; y++) for (int x = 0; x < res; x++)
                {
                    float px = x - half; float py = y - half;
                    float qx = Mathf.Clamp(px, -inner, inner); float qy = Mathf.Clamp(py, -inner, inner);
                    float dx = px - qx; float dy = py - qy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(rad + 0.5f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f));
        }
    }
}
