using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zoologic
{
    public static class DailyRewardUI
    {
        private static GameObject _panelRoot;
        private static TMP_FontAsset _fontTitle;
        private static TMP_FontAsset _fontBody;

        public static void TryShowAutoPopup()
        {
            if (!DailyRewardManager.CanClaimToday()) return;
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            Show(canvas);
        }

        public static void Show(Canvas canvas)
        {
            if (_panelRoot != null) Object.Destroy(_panelRoot);
            _fontTitle = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Bold SDF");
            _fontBody = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Regular SDF");

            _panelRoot = new GameObject("DailyRewardRoot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _panelRoot.transform.SetParent(canvas.transform, false);
            var rootRect = _panelRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            var rootImg = _panelRoot.GetComponent<Image>();
            rootImg.color = new Color(0.24f, 0.16f, 0.10f, 0.62f);
            rootImg.raycastTarget = true;

            var card = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            card.transform.SetParent(_panelRoot.transform, false);
            var cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(860f, 780f);
            cardRect.anchoredPosition = Vector2.zero;
            var cardImg = card.GetComponent<Image>();
            cardImg.sprite = CreateRoundedSprite(256, 0.18f);
            cardImg.type = Image.Type.Simple;
            cardImg.color = new Color(1f, 0.98f, 0.94f, 1f);
            var cardShadow = card.AddComponent<Shadow>();
            cardShadow.effectColor = new Color(0.18f, 0.11f, 0.06f, 0.30f);
            cardShadow.effectDistance = new Vector2(0f, -10f);

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(32, 32, 32, 32);
            vlg.spacing = 18f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;

            var titleGO = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titleGO.transform.SetParent(card.transform, false);
            var title = titleGO.GetComponent<TextMeshProUGUI>();
            title.font = _fontTitle;
            title.text = "Cadeau du jour";
            title.fontSize = 42;
            title.fontStyle = FontStyles.Bold;
            title.color = new Color(0.29f, 0.18f, 0.10f);
            title.alignment = TextAlignmentOptions.Center;
            var titleLE = titleGO.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 60f;

            var streak = DailyRewardManager.GetStreak();
            int canClaimDay = DailyRewardManager.CanClaimToday() ? Mathf.Min(streak + 1, 7) : Mathf.Min(streak, 7);
            if (streak == 0 && DailyRewardManager.CanClaimToday()) canClaimDay = 1;

            var subGO = new GameObject("Sub", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            subGO.transform.SetParent(card.transform, false);
            var sub = subGO.GetComponent<TextMeshProUGUI>();
            sub.font = _fontBody;
            sub.text = $"Jour {canClaimDay}/7  -  Série {streak} jours";
            sub.fontSize = 24;
            sub.color = new Color(0.50f, 0.42f, 0.35f);
            sub.alignment = TextAlignmentOptions.Center;
            var subLE = subGO.AddComponent<LayoutElement>();
            subLE.preferredHeight = 36f;

            var gridGO = new GameObject("Grid", typeof(RectTransform));
            gridGO.transform.SetParent(card.transform, false);
            var gridRect = gridGO.GetComponent<RectTransform>();
            var gridLE = gridGO.AddComponent<LayoutElement>();
            gridLE.preferredHeight = 280f;
            var hlg = gridGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.padding = new RectOffset(0, 0, 10, 10);

            for (int day = 1; day <= 7; day++)
            {
                bool isClaimed = day <= streak && !DailyRewardManager.CanClaimToday() ? day <= streak : day < canClaimDay;
                if (DailyRewardManager.CanClaimToday() && day < canClaimDay) isClaimed = true;
                if (!DailyRewardManager.CanClaimToday() && day <= streak) isClaimed = true;
                bool isToday = day == canClaimDay && DailyRewardManager.CanClaimToday();
                CreateDayCell(gridGO.transform, day, isClaimed, isToday);
            }

            bool canClaim = DailyRewardManager.CanClaimToday();
            int reward = DailyRewardManager.GetTodayReward();

            var btnRow = new GameObject("BtnRow", typeof(RectTransform));
            btnRow.transform.SetParent(card.transform, false);
            var btnRowRect = btnRow.GetComponent<RectTransform>();
            var btnRowLE = btnRow.AddComponent<LayoutElement>();
            btnRowLE.preferredHeight = 90f;
            var bh = btnRow.AddComponent<HorizontalLayoutGroup>();
            bh.spacing = 18f;
            bh.childAlignment = TextAnchor.MiddleCenter;
            bh.childForceExpandWidth = false;

            var claimBtn = CreateButton(btnRow.transform, canClaim ? $"Réclamer {reward}" : "Déjà réclamé", new Color(0.22f, 0.65f, 0.30f), canClaim);
            if (canClaim)
            {
                claimBtn.onClick.AddListener(() =>
                {
                    int r = DailyRewardManager.Claim();
                    SFXManager.Instance.PlayUnlock();
                    Object.Destroy(_panelRoot);
                    _panelRoot = null;
                    var c = Object.FindFirstObjectByType<Canvas>();
                    if (c != null) ShowCoinToast(c, $"+{r} pièces !");
                });
            }

            var x2Btn = CreateButton(btnRow.transform, "x2 Pub", new Color(0.22f, 0.50f, 0.85f), canClaim);
            if (canClaim)
            {
                x2Btn.onClick.AddListener(() =>
                {
                    var admob = AdMobManager.Instance;
                    if (admob != null) admob.ShowRewarded(() =>
                    {
                        int r = DailyRewardManager.Claim();
                        if (r > 0) CurrencyManager.AddCoins(r);
                        SFXManager.Instance.PlayUnlock();
                        Object.Destroy(_panelRoot);
                        _panelRoot = null;
                        var c = Object.FindFirstObjectByType<Canvas>();
                        if (c != null) ShowCoinToast(c, $"+{r * 2} pièces (x2) !");
                    });
                    else
                    {
                        int r = DailyRewardManager.Claim();
                        if (r > 0) CurrencyManager.AddCoins(r);
                        SFXManager.Instance.PlayUnlock();
                        Object.Destroy(_panelRoot);
                        _panelRoot = null;
                        var c = Object.FindFirstObjectByType<Canvas>();
                        if (c != null) ShowCoinToast(c, $"+{r * 2} pièces (x2) !");
                    }
                });
            }

            var closeBtn = new GameObject("Close", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            closeBtn.transform.SetParent(card.transform, false);
            var closeRect = closeBtn.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.sizeDelta = new Vector2(48f, 48f);
            closeRect.anchoredPosition = new Vector2(-12f, -12f);
            var closeLayout = closeBtn.AddComponent<LayoutElement>();
            closeLayout.ignoreLayout = true;
            var closeImg = closeBtn.GetComponent<Image>();
            closeImg.sprite = CreateRoundedSprite(64, 0.5f);
            closeImg.color = new Color(0f, 0f, 0f, 0.08f);
            var closeBtnComp = closeBtn.GetComponent<Button>();
            closeBtnComp.targetGraphic = closeImg;
            closeBtnComp.onClick.AddListener(() => { Object.Destroy(_panelRoot); _panelRoot = null; });

            var closeTxt = new GameObject("X", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            closeTxt.transform.SetParent(closeBtn.transform, false);
            var closeTxtRect = closeTxt.GetComponent<RectTransform>();
            closeTxtRect.anchorMin = Vector2.zero;
            closeTxtRect.anchorMax = Vector2.one;
            closeTxtRect.offsetMin = Vector2.zero;
            closeTxtRect.offsetMax = Vector2.zero;
            var closeT = closeTxt.GetComponent<TextMeshProUGUI>();
            closeT.font = _fontTitle;
            closeT.text = "×";
            closeT.fontSize = 36;
            closeT.color = new Color(0.40f, 0.40f, 0.42f);
            closeT.alignment = TextAlignmentOptions.Center;
        }

        private static void CreateDayCell(Transform parent, int day, bool claimed, bool today)
        {
            var cell = new GameObject($"Day{day}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cell.transform.SetParent(parent, false);
            var rect = cell.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 0f);
            var le = cell.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.preferredHeight = 240f;
            var img = cell.GetComponent<Image>();
            img.sprite = CreateRoundedSprite(128, 0.22f);
            img.type = Image.Type.Simple;
            if (today) img.color = new Color(1f, 0.96f, 0.78f);
            else if (claimed) img.color = new Color(0.88f, 0.94f, 0.88f);
            else img.color = new Color(1f, 0.96f, 0.88f);
            if (today)
            {
                var outline = cell.AddComponent<Outline>();
                outline.effectColor = new Color(0.95f, 0.70f, 0.20f);
                outline.effectDistance = new Vector2(3f, -3f);
            }

            var vlg = cell.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(6, 6, 12, 12);
            vlg.spacing = 6f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth = true;

            var dayTxtGO = new GameObject("Day", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            dayTxtGO.transform.SetParent(cell.transform, false);
            var dayTxt = dayTxtGO.GetComponent<TextMeshProUGUI>();
            dayTxt.font = _fontBody;
            dayTxt.text = $"J{day}";
            dayTxt.fontSize = 18;
            dayTxt.color = new Color(0.60f, 0.48f, 0.35f);
            dayTxt.alignment = TextAlignmentOptions.Center;
            var dayLE = dayTxtGO.AddComponent<LayoutElement>();
            dayLE.preferredHeight = 22f;

            var coinGO = new GameObject("Coin", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            coinGO.transform.SetParent(cell.transform, false);
            var coinRect = coinGO.GetComponent<RectTransform>();
            coinRect.sizeDelta = new Vector2(36f, 36f);
            var coinImg = coinGO.GetComponent<Image>();
            coinImg.sprite = Resources.Load<Sprite>("UI/coin");
            coinImg.preserveAspect = true;
            var coinLE = coinGO.AddComponent<LayoutElement>();
            coinLE.preferredHeight = 40f;

            var amtGO = new GameObject("Amt", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            amtGO.transform.SetParent(cell.transform, false);
            var amt = amtGO.GetComponent<TextMeshProUGUI>();
            amt.font = _fontTitle;
            amt.text = DailyRewardManager.GetRewardForDay(day).ToString();
            amt.fontSize = 22;
            amt.fontStyle = FontStyles.Bold;
            amt.color = new Color(0.22f, 0.19f, 0.16f);
            amt.alignment = TextAlignmentOptions.Center;
            var amtLE = amtGO.AddComponent<LayoutElement>();
            amtLE.preferredHeight = 24f;

            var statusGO = new GameObject("Status", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            statusGO.transform.SetParent(cell.transform, false);
            var status = statusGO.GetComponent<TextMeshProUGUI>();
            status.font = _fontBody;
            status.text = claimed ? "Fait" : (today ? "Aujourd'hui" : "");
            status.fontSize = 20;
            status.color = claimed ? new Color(0.22f, 0.65f, 0.30f) : new Color(0.95f, 0.70f, 0.20f);
            status.alignment = TextAlignmentOptions.Center;
            var sLE = statusGO.AddComponent<LayoutElement>();
            sLE.preferredHeight = 22f;
        }

        private static Button CreateButton(Transform parent, string label, Color bg, bool enabled)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(240f, 62f);
            var img = go.GetComponent<Image>();
            img.sprite = CreateRoundedSprite(128, 0.35f);
            img.color = enabled ? bg : new Color(0.78f, 0.74f, 0.69f);
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.20f, 0.12f, 0.07f, 0.22f);
            shadow.effectDistance = new Vector2(0f, -4f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.interactable = enabled;
            var txtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            txtGO.transform.SetParent(go.transform, false);
            var txtRect = txtGO.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;
            var txt = txtGO.GetComponent<TextMeshProUGUI>();
            txt.font = _fontTitle;
            txt.text = label;
            txt.fontSize = 24;
            txt.fontStyle = FontStyles.Bold;
            txt.color = enabled ? Color.white : new Color(0.45f, 0.38f, 0.32f);
            txt.alignment = TextAlignmentOptions.Center;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 240f;
            le.preferredHeight = 62f;
            return btn;
        }

        private static void ShowCoinToast(Canvas canvas, string msg)
        {
            var toast = new GameObject("DailyToast", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            toast.transform.SetParent(canvas.transform, false);
            var rect = toast.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(520f, 64f);
            rect.anchoredPosition = new Vector2(0f, -260f);
            var img = toast.GetComponent<Image>();
            img.sprite = CreateRoundedSprite(128, 0.35f);
            img.color = new Color(0f, 0f, 0f, 0.82f);
            var txtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            txtGO.transform.SetParent(toast.transform, false);
            var txtRect = txtGO.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;
            var txt = txtGO.GetComponent<TextMeshProUGUI>();
            txt.font = _fontTitle;
            txt.text = msg;
            txt.fontSize = 26;
            txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.Center;
            Object.Destroy(toast, 1.6f);
        }

        private static Sprite CreateRoundedSprite(int res, float ratio)
        {
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            float half = (res - 1) * 0.5f;
            float rad = res * ratio;
            float inner = half - rad;
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
