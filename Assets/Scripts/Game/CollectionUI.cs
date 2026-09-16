using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Zoologic.Localization;

namespace Zoologic
{
    /// <summary>
    /// Boutique Collection : 4 skins de pions (Bois gratuit, Flat 200, Doré 350,
    /// Nuit 500). Sélection persistée, appliquée au prochain niveau.
    /// </summary>
    public static class CollectionUI
    {
        private static GameObject _panelRoot;
        private static TMP_FontAsset _fontTitle;
        private static TMP_FontAsset _fontBody;
        private static TextMeshProUGUI _coinsText;
        private static Transform _rowsRoot;

        public static bool IsOpen => _panelRoot != null;

        public static void Show(Canvas canvas)
        {
            if (canvas == null) return;
            if (_panelRoot != null) Object.Destroy(_panelRoot);
            _fontTitle = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Bold SDF");
            _fontBody = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Regular SDF");

            _panelRoot = new GameObject("CollectionRoot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _panelRoot.transform.SetParent(canvas.transform, false);
            var rootRect = _panelRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            var rootImg = _panelRoot.GetComponent<Image>();
            rootImg.color = new Color(0.12f, 0.08f, 0.05f, 0.55f);
            rootImg.raycastTarget = true;
            _panelRoot.transform.SetAsLastSibling();

            var card = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            card.transform.SetParent(_panelRoot.transform, false);
            var cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(880f, 1060f);
            cardRect.anchoredPosition = Vector2.zero;
            var cardImg = card.GetComponent<Image>();
            cardImg.sprite = B1UI.Bubble ?? JellyUI.ButtonGrey;
            cardImg.type = Image.Type.Sliced;
            cardImg.color = new Color(1f, 0.98f, 0.94f, 1f);
            var cardShadow = card.AddComponent<Shadow>();
            cardShadow.effectColor = new Color(0.18f, 0.11f, 0.06f, 0.30f);
            cardShadow.effectDistance = new Vector2(0f, -10f);

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(32, 32, 28, 28);
            vlg.spacing = 14f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;

            // Titre + cagnotte.
            var headerGO = new GameObject("Header", typeof(RectTransform));
            headerGO.transform.SetParent(card.transform, false);
            var headerLE = headerGO.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 128f;
            headerLE.flexibleWidth = 1f;
            var headerVLG = headerGO.AddComponent<VerticalLayoutGroup>();
            headerVLG.spacing = 6f;
            headerVLG.childAlignment = TextAnchor.UpperCenter;
            headerVLG.childForceExpandWidth = false;
            headerVLG.childForceExpandHeight = false;
            headerVLG.childControlWidth = false;
            headerVLG.childControlHeight = false;

            var titleGO = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titleGO.transform.SetParent(headerGO.transform, false);
            var title = titleGO.GetComponent<TextMeshProUGUI>();
            title.font = _fontTitle;
            title.text = LocalizationManager.Get("shop.title");
            LocalizationManager.ApplyTo(title);
            title.fontSize = 44;
            title.fontStyle = FontStyles.Bold;
            title.color = new Color(0.29f, 0.18f, 0.10f);
            title.alignment = TextAlignmentOptions.Center;
            var titleLE = titleGO.AddComponent<LayoutElement>();
            titleLE.preferredWidth = 760f;
            titleLE.preferredHeight = 56f;
            titleLE.flexibleWidth = 0f;

            var pillGO = new GameObject("Coins", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            pillGO.transform.SetParent(headerGO.transform, false);
            var pillImg = pillGO.GetComponent<Image>();
            pillImg.sprite = JellyUI.SmallYellow ?? B1UI.Bubble;
            pillImg.type = Image.Type.Sliced;
            var pillLE = pillGO.AddComponent<LayoutElement>();
            pillLE.preferredWidth = 190f;
            pillLE.preferredHeight = 56f;
            pillLE.flexibleWidth = 0f;
            var pillHLG = pillGO.AddComponent<HorizontalLayoutGroup>();
            pillHLG.padding = new RectOffset(10, 10, 6, 6);
            pillHLG.spacing = 8f;
            pillHLG.childAlignment = TextAnchor.MiddleCenter;
            var coinIconGO = new GameObject("Coin", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            coinIconGO.transform.SetParent(pillGO.transform, false);
            var coinIcon = coinIconGO.GetComponent<Image>();
            coinIcon.sprite = Resources.Load<Sprite>("UI/coin");
            coinIcon.preserveAspect = true;
            coinIcon.raycastTarget = false;
            var coinLE = coinIconGO.AddComponent<LayoutElement>();
            coinLE.preferredWidth = 36f;
            coinLE.preferredHeight = 36f;
            var coinTxtGO = new GameObject("Count", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            coinTxtGO.transform.SetParent(pillGO.transform, false);
            _coinsText = coinTxtGO.GetComponent<TextMeshProUGUI>();
            _coinsText.font = _fontTitle;
            _coinsText.fontSize = 30;
            _coinsText.fontStyle = FontStyles.Bold;
            _coinsText.color = new Color(0.29f, 0.18f, 0.10f);
            _coinsText.alignment = TextAlignmentOptions.MidlineLeft;
            _coinsText.raycastTarget = false;
            var coinTxtLE = coinTxtGO.AddComponent<LayoutElement>();
            coinTxtLE.flexibleWidth = 1f;
            RefreshCoins();

            // Lignes skins.
            var rowsGO = new GameObject("Rows", typeof(RectTransform));
            rowsGO.transform.SetParent(card.transform, false);
            var rowsLE = rowsGO.AddComponent<LayoutElement>();
            rowsLE.flexibleWidth = 1f;
            rowsLE.flexibleHeight = 1f;
            var rowsVLG = rowsGO.AddComponent<VerticalLayoutGroup>();
            rowsVLG.spacing = 12f;
            rowsVLG.childAlignment = TextAnchor.UpperCenter;
            rowsVLG.childForceExpandWidth = true;
            rowsVLG.childForceExpandHeight = false;
            rowsVLG.childControlHeight = false;
            _rowsRoot = rowsGO.transform;
            RebuildRows();

            // Fermer.
            var closeGO = new GameObject("Close", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            closeGO.transform.SetParent(card.transform, false);
            var closeRect = closeGO.GetComponent<RectTransform>();
            var closeLE = closeGO.AddComponent<LayoutElement>();
            closeLE.preferredHeight = 76f;
            closeLE.flexibleWidth = 1f;
            var closeImg = closeGO.GetComponent<Image>();
            closeImg.sprite = JellyUI.ButtonGrey;
            closeImg.type = Image.Type.Sliced;
            var closeBtn = closeGO.GetComponent<Button>();
            JellyUI.ApplyJellyButton(closeBtn, closeImg, JellyUI.ButtonGrey, JellyUI.ButtonGrey, JellyUI.ButtonRed, JellyUI.ButtonGrey);
            closeBtn.onClick.AddListener(() => { SFXManager.Instance.PlayMenuClose(); Close(); });
            var closeTxtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            closeTxtGO.transform.SetParent(closeGO.transform, false);
            var closeTxtRect = closeTxtGO.GetComponent<RectTransform>();
            closeTxtRect.anchorMin = Vector2.zero;
            closeTxtRect.anchorMax = Vector2.one;
            closeTxtRect.offsetMin = Vector2.zero;
            closeTxtRect.offsetMax = Vector2.zero;
            var closeTxt = closeTxtGO.GetComponent<TextMeshProUGUI>();
            closeTxt.font = _fontTitle;
            closeTxt.text = "×";
            closeTxt.fontSize = 44;
            closeTxt.fontStyle = FontStyles.Bold;
            closeTxt.color = Color.white;
            closeTxt.alignment = TextAlignmentOptions.Center;
            closeTxt.raycastTarget = false;
        }

        public static void Close()
        {
            if (_panelRoot != null)
            {
                Object.Destroy(_panelRoot);
                _panelRoot = null;
            }
            _rowsRoot = null;
            _coinsText = null;
        }

        private static void RefreshCoins()
        {
            if (_coinsText != null)
                _coinsText.text = CurrencyManager.GetCoins().ToString();
        }

        private static void RebuildRows()
        {
            if (_rowsRoot == null) return;
            for (int i = _rowsRoot.childCount - 1; i >= 0; i--)
                Object.Destroy(_rowsRoot.GetChild(i).gameObject);

            for (int skin = 0; skin < SkinManager.Count; skin++)
                BuildRow(_rowsRoot, skin);
        }

        private static void BuildRow(Transform parent, int skin)
        {
            bool owned = SkinManager.IsOwned(skin);
            bool selected = SkinManager.Selected == skin;
            int cost = SkinManager.CostOf(skin);

            var rowGO = new GameObject("Skin_" + skin, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rowGO.transform.SetParent(parent, false);
            var rowImg = rowGO.GetComponent<Image>();
            rowImg.sprite = B1UI.Bubble ?? JellyUI.ButtonGrey;
            rowImg.type = Image.Type.Sliced;
            rowImg.color = selected ? new Color(1f, 0.93f, 0.70f, 1f) : Color.white;
            rowImg.raycastTarget = false;
            var rowLE = rowGO.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 150f;
            rowLE.flexibleWidth = 1f;
            var rowHLG = rowGO.AddComponent<HorizontalLayoutGroup>();
            rowHLG.padding = new RectOffset(16, 16, 14, 14);
            rowHLG.spacing = 12f;
            rowHLG.childAlignment = TextAnchor.MiddleLeft;
            rowHLG.childForceExpandWidth = false;

            // Aperçu : 3 animaux du skin.
            Sprite[] previews = SkinManager.GetPreviewSprites(skin);
            Color tint = SkinManager.TintOf(skin);
            for (int i = 0; i < 3; i++)
            {
                var pvGO = new GameObject("Pv" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                pvGO.transform.SetParent(rowGO.transform, false);
                var pvImg = pvGO.GetComponent<Image>();
                pvImg.sprite = i < previews.Length ? previews[i] : null;
                pvImg.preserveAspect = true;
                pvImg.color = tint;
                pvImg.raycastTarget = false;
                var pvLE = pvGO.AddComponent<LayoutElement>();
                pvLE.preferredWidth = 76f;
                pvLE.preferredHeight = 76f;
                pvLE.flexibleWidth = 0f;
                pvLE.flexibleHeight = 0f;
            }

            // Nom.
            var nameGO = new GameObject("Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            nameGO.transform.SetParent(rowGO.transform, false);
            var nameTxt = nameGO.GetComponent<TextMeshProUGUI>();
            nameTxt.font = _fontTitle;
            nameTxt.text = LocalizationManager.Get("skin." + SkinKey(skin));
            LocalizationManager.ApplyTo(nameTxt);
            nameTxt.fontSize = 30;
            nameTxt.fontStyle = FontStyles.Bold;
            nameTxt.color = new Color(0.29f, 0.18f, 0.10f);
            nameTxt.alignment = TextAlignmentOptions.MidlineLeft;
            nameTxt.raycastTarget = false;
            var nameLE = nameGO.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1f;

            // Bouton d'action.
            string label;
            Sprite btnNormal;
            bool enabled;
            if (selected) { label = LocalizationManager.Get("shop.selected"); btnNormal = JellyUI.ButtonGrey; enabled = false; }
            else if (owned) { label = LocalizationManager.Get("shop.select"); btnNormal = JellyUI.ButtonGreen; enabled = true; }
            else
            {
                label = LocalizationManager.Get("shop.buy", cost);
                bool afford = CurrencyManager.HasCoins(cost);
                btnNormal = afford ? JellyUI.ButtonYellow : JellyUI.ButtonGrey;
                enabled = afford;
            }
            var btnGO = new GameObject("Action", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(rowGO.transform, false);
            var btnImg = btnGO.GetComponent<Image>();
            btnImg.sprite = btnNormal ?? JellyUI.ButtonGrey;
            btnImg.type = Image.Type.Sliced;
            var btnLE = btnGO.AddComponent<LayoutElement>();
            btnLE.preferredWidth = 230f;
            btnLE.preferredHeight = 72f;
            btnLE.flexibleWidth = 0f;
            var btn = btnGO.GetComponent<Button>();
            JellyUI.ApplyJellyButton(btn, btnImg, btnNormal, JellyUI.ButtonYellow, JellyUI.ButtonRed, JellyUI.ButtonGrey);
            btn.interactable = enabled;
            var btnTxtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            btnTxtGO.transform.SetParent(btnGO.transform, false);
            var btnTxtRect = btnTxtGO.GetComponent<RectTransform>();
            btnTxtRect.anchorMin = Vector2.zero;
            btnTxtRect.anchorMax = Vector2.one;
            btnTxtRect.offsetMin = new Vector2(8f, 4f);
            btnTxtRect.offsetMax = new Vector2(-8f, -4f);
            var btnTxt = btnTxtGO.GetComponent<TextMeshProUGUI>();
            btnTxt.font = _fontTitle;
            btnTxt.text = label;
            btnTxt.fontSize = 26;
            btnTxt.fontStyle = FontStyles.Bold;
            btnTxt.color = enabled ? Color.white : new Color(1f, 1f, 1f, 0.6f);
            btnTxt.alignment = TextAlignmentOptions.Center;
            btnTxt.raycastTarget = false;
            btnTxt.enableAutoSizing = true;
            btnTxt.fontSizeMin = 18;
            btnTxt.fontSizeMax = 26;
            int captured = skin;
            btn.onClick.AddListener(() => OnRowAction(captured));
        }

        private static string SkinKey(int skin)
        {
            switch (skin)
            {
                case SkinManager.Flat: return "flat";
                case SkinManager.Gold: return "gold";
                case SkinManager.Night: return "night";
                default: return "wood";
            }
        }

        private static void OnRowAction(int skin)
        {
            if (SkinManager.Selected == skin) return;
            if (SkinManager.IsOwned(skin))
            {
                SkinManager.Select(skin);
                SFXManager.Instance.PlayUnlock();
                RebuildRows();
                return;
            }
            int cost = SkinManager.CostOf(skin);
            if (!CurrencyManager.SpendCoins(cost))
            {
                SFXManager.Instance.PlayDialogueBlip();
                return;
            }
            SkinManager.Own(skin);
            SkinManager.Select(skin);
            SFXManager.Instance.PlayUnlock();
            RefreshCoins();
            RebuildRows();
        }
    }
}
