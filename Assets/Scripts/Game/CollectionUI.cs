using System.Collections;
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
        private static int _tab; // 0 = skins, 1 = mascottes
        private static readonly System.Collections.Generic.List<Button> _tabButtons =
            new System.Collections.Generic.List<Button>();

        public static bool IsOpen => _panelRoot != null;

        public static void Show(Canvas canvas)
        {
            if (canvas == null) return;
            if (_panelRoot != null) Object.Destroy(_panelRoot);
            _tab = 0;
            _tabButtons.Clear();
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

            // Onglets Skins / Mascottes.
            var tabsGO = new GameObject("Tabs", typeof(RectTransform));
            tabsGO.transform.SetParent(card.transform, false);
            var tabsLE = tabsGO.AddComponent<LayoutElement>();
            tabsLE.preferredHeight = 104f;
            tabsLE.flexibleWidth = 1f;
            var tabsHLG = tabsGO.AddComponent<HorizontalLayoutGroup>();
            tabsHLG.spacing = 16f;
            tabsHLG.childAlignment = TextAnchor.MiddleCenter;
            tabsHLG.childForceExpandWidth = false;
            BuildTabButton(tabsGO.transform, "collection.tab_skins", 0);
            BuildTabButton(tabsGO.transform, "collection.tab_mascots", 1);

            // Lignes skins / grille mascottes.
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

        private static void BuildTabButton(Transform parent, string locKey, int tabIndex)
        {
            bool active = _tab == tabIndex;
            var btnGO = new GameObject("Tab" + tabIndex, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(parent, false);
            var btnImg = btnGO.GetComponent<Image>();
            btnImg.sprite = active ? JellyUI.ButtonGreen : JellyUI.ButtonGrey;
            btnImg.type = Image.Type.Sliced;
            var btnLE = btnGO.AddComponent<LayoutElement>();
            btnLE.preferredWidth = 300f;
            btnLE.preferredHeight = 96f;
            btnLE.flexibleWidth = 0f;
            var btn = btnGO.GetComponent<Button>();
            JellyUI.ApplyJellyButton(btn, btnImg, btnImg.sprite, JellyUI.ButtonYellow, JellyUI.ButtonRed, JellyUI.ButtonGrey);
            _tabButtons.Add(btn);
            var txtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            txtGO.transform.SetParent(btnGO.transform, false);
            var txtRect = txtGO.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = new Vector2(8f, 4f);
            txtRect.offsetMax = new Vector2(-8f, -4f);
            var txt = txtGO.GetComponent<TextMeshProUGUI>();
            txt.font = _fontTitle;
            txt.text = LocalizationManager.Get(locKey);
            txt.fontSize = 30;
            txt.fontStyle = FontStyles.Bold;
            txt.color = active ? Color.white : new Color(1f, 1f, 1f, 0.75f);
            txt.alignment = TextAlignmentOptions.Center;
            txt.raycastTarget = false;
            txt.enableAutoSizing = true;
            txt.fontSizeMin = 20;
            txt.fontSizeMax = 30;
            btn.onClick.AddListener(() =>
            {
                if (_tab == tabIndex) return;
                _tab = tabIndex;
                SFXManager.Instance.PlayMenuOpen();
                RebuildTabs();
                RebuildRows();
            });
        }

        private static void RebuildTabs()
        {
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                if (_tabButtons[i] == null) continue;
                bool active = _tab == i;
                var img = _tabButtons[i].GetComponent<Image>();
                if (img != null)
                {
                    img.sprite = active ? JellyUI.ButtonGreen : JellyUI.ButtonGrey;
                    img.color = Color.white;
                }
                var txt = _tabButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null)
                    txt.color = active ? Color.white : new Color(1f, 1f, 1f, 0.75f);
            }
        }

        private static void RebuildRows()
        {
            if (_rowsRoot == null) return;
            for (int i = _rowsRoot.childCount - 1; i >= 0; i--)
                Object.Destroy(_rowsRoot.GetChild(i).gameObject);

            if (_tab == 1)
            {
                BuildMascotGrid(_rowsRoot);
                return;
            }
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

        // ------------------------------------------------------------------
        // Onglet Mascottes : 8 mx__ en grille 4x2 (raretés C/R/E/L).
        // ------------------------------------------------------------------

        private static Color MascotBg(string r) => r switch
        {
            "C" => new Color(0.910f, 0.957f, 0.910f, 1f),
            "R" => new Color(0.890f, 0.941f, 1.000f, 1f),
            "E" => new Color(0.953f, 0.910f, 1.000f, 1f),
            _ => new Color(1.000f, 0.953f, 0.839f, 1f),
        };

        private static string MascotRarityKey(string r) => "mascot.rarity." + r switch
        {
            "C" => "common",
            "R" => "rare",
            "E" => "epic",
            _ => "legendary",
        };

        // Grille 5x2 : les 10 mascottes visibles SANS scroll (fini la rangée
        // cachée). Cellule = face 120 + rareté, tap = popup détail (achat).
        // Plus de "???" : silhouette légère + nom de rareté même verrouillé.
        private static void BuildMascotGrid(Transform parent)
        {
            int count = StarChestManager.MascotCount;
            const int perRow = 5;
            for (int row = 0; row * perRow < count; row++)
            {
                var rowGO = new GameObject("MRow" + row, typeof(RectTransform));
                rowGO.transform.SetParent(parent, false);
                var rowLE = rowGO.AddComponent<LayoutElement>();
                rowLE.preferredHeight = 210f;
                rowLE.flexibleWidth = 1f;
                var rowHLG = rowGO.AddComponent<HorizontalLayoutGroup>();
                rowHLG.spacing = 12f;
                rowHLG.childAlignment = TextAnchor.MiddleCenter;
                rowHLG.childForceExpandWidth = false;
                for (int col = 0; col < perRow; col++)
                {
                    int index = row * perRow + col;
                    if (index >= count) break;
                    BuildMascotCell(rowGO.transform, index);
                }
            }
        }

        // Cellule 150x200 : face 120 + rareté, TOUTE la cellule cliquable
        // (150px ≥ 96 lint). Le prix vit dans le popup détail, fini le fouillis.
        private static void BuildMascotCell(Transform parent, int index)
        {
            string rarity = StarChestManager.RarityOf(index);
            bool owned = StarChestManager.OwnsMascot(index);
            Sprite face = StarChestManager.MascotFace(index);

            var cellGO = new GameObject("Mascot" + index, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            cellGO.transform.SetParent(parent, false);
            var cellImg = cellGO.GetComponent<Image>();
            cellImg.sprite = B1UI.Bubble ?? JellyUI.ButtonGrey;
            cellImg.type = Image.Type.Sliced;
            cellImg.color = owned ? MascotBg(rarity) : new Color(0.93f, 0.91f, 0.88f, 1f);
            var cellLE = cellGO.AddComponent<LayoutElement>();
            cellLE.preferredWidth = 150f;
            cellLE.preferredHeight = 200f;
            cellLE.flexibleWidth = 0f;
            cellLE.flexibleHeight = 0f;
            var cellShadow = cellGO.AddComponent<Shadow>();
            cellShadow.effectColor = new Color(0.18f, 0.11f, 0.06f, 0.22f);
            cellShadow.effectDistance = new Vector2(0f, -5f);
            var cellBtn = cellGO.GetComponent<Button>();
            cellBtn.targetGraphic = cellImg;
            int tapped = index;
            cellBtn.onClick.AddListener(() => ShowMascotPopup(tapped));

            var faceGO = new GameObject("Face", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            faceGO.transform.SetParent(cellGO.transform, false);
            var faceRect = faceGO.GetComponent<RectTransform>();
            faceRect.anchorMin = new Vector2(0.5f, 1f);
            faceRect.anchorMax = new Vector2(0.5f, 1f);
            faceRect.pivot = new Vector2(0.5f, 1f);
            faceRect.sizeDelta = new Vector2(120f, 120f);
            faceRect.anchoredPosition = new Vector2(0f, -14f);
            var faceImg = faceGO.GetComponent<Image>();
            if (face != null)
            {
                faceImg.sprite = face;
                faceImg.preserveAspect = true;
                faceImg.color = owned ? Color.white : new Color(0.10f, 0.09f, 0.11f, 0.45f);
            }
            else
            {
                faceImg.color = new Color(0f, 0f, 0f, 0f);
            }
            faceImg.raycastTarget = false;
            if (face == null)
            {
                var qGO = new GameObject("Q", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                qGO.transform.SetParent(cellGO.transform, false);
                var qRect = qGO.GetComponent<RectTransform>();
                qRect.anchorMin = new Vector2(0.5f, 1f);
                qRect.anchorMax = new Vector2(0.5f, 1f);
                qRect.pivot = new Vector2(0.5f, 1f);
                qRect.sizeDelta = new Vector2(120f, 120f);
                qRect.anchoredPosition = new Vector2(0f, -14f);
                var qTxt = qGO.GetComponent<TextMeshProUGUI>();
                qTxt.font = _fontTitle;
                qTxt.text = "?";
                qTxt.fontSize = 84;
                qTxt.fontStyle = FontStyles.Bold;
                qTxt.color = new Color(0.55f, 0.50f, 0.44f, 1f);
                qTxt.alignment = TextAlignmentOptions.Center;
                qTxt.raycastTarget = false;
            }

            var nameGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            nameGO.transform.SetParent(cellGO.transform, false);
            var nameRect = nameGO.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 0f);
            nameRect.pivot = new Vector2(0.5f, 0f);
            nameRect.sizeDelta = new Vector2(0f, 44f);
            nameRect.anchoredPosition = new Vector2(0f, 10f);
            var nameTxt = nameGO.GetComponent<TextMeshProUGUI>();
            nameTxt.font = _fontTitle;
            nameTxt.text = LocalizationManager.Get(MascotRarityKey(rarity));
            nameTxt.fontSize = 26;
            nameTxt.fontStyle = FontStyles.Bold;
            nameTxt.color = owned ? RarityInk(rarity) : new Color(0.55f, 0.50f, 0.44f, 1f);
            nameTxt.alignment = TextAlignmentOptions.Center;
            nameTxt.raycastTarget = false;
            nameTxt.enableAutoSizing = true;
            nameTxt.fontSizeMin = 18;
            nameTxt.fontSizeMax = 26;
        }

        /// <summary>Popup détail : grande face + rareté + prix/achat + fermer.</summary>
        private static void ShowMascotPopup(int index)
        {
            string rarity = StarChestManager.RarityOf(index);
            bool owned = StarChestManager.OwnsMascot(index);
            int price = StarChestManager.ShopPrice(rarity);
            Sprite face = StarChestManager.MascotFace(index);
            SFXManager.Instance.PlayMenuOpen();

            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            var root = new GameObject("MascotPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(canvas.transform, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            var rootImg = root.GetComponent<Image>();
            rootImg.color = new Color(0.12f, 0.08f, 0.05f, 0.55f);
            rootImg.raycastTarget = true;
            root.transform.SetAsLastSibling();

            var card = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            card.transform.SetParent(root.transform, false);
            var cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(560f, 640f);
            cardRect.anchoredPosition = Vector2.zero;
            var cardImg = card.GetComponent<Image>();
            cardImg.sprite = B1UI.Bubble ?? JellyUI.ButtonGrey;
            cardImg.type = Image.Type.Sliced;
            cardImg.color = owned ? MascotBg(rarity) : new Color(1f, 0.98f, 0.94f, 1f);
            cardImg.raycastTarget = false;
            var cardShadow = card.AddComponent<Shadow>();
            cardShadow.effectColor = new Color(0.18f, 0.11f, 0.06f, 0.32f);
            cardShadow.effectDistance = new Vector2(0f, -10f);

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(32, 32, 28, 28);
            vlg.spacing = 12f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var faceGO = new GameObject("Face", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            faceGO.transform.SetParent(card.transform, false);
            var faceImg = faceGO.GetComponent<Image>();
            faceImg.sprite = face;
            faceImg.preserveAspect = true;
            faceImg.color = owned ? Color.white : new Color(0.10f, 0.09f, 0.11f, 0.45f);
            faceImg.raycastTarget = false;
            var faceLE = faceGO.AddComponent<LayoutElement>();
            faceLE.preferredWidth = 220f;
            faceLE.preferredHeight = 220f;
            faceLE.flexibleWidth = 0f;

            var nameGO = new GameObject("Rarity", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            nameGO.transform.SetParent(card.transform, false);
            var nameTxt = nameGO.GetComponent<TextMeshProUGUI>();
            nameTxt.font = _fontTitle;
            nameTxt.text = LocalizationManager.Get(MascotRarityKey(rarity));
            nameTxt.fontSize = 36;
            nameTxt.fontStyle = FontStyles.Bold;
            nameTxt.color = RarityInk(rarity);
            nameTxt.alignment = TextAlignmentOptions.Center;
            nameTxt.raycastTarget = false;
            var nameLE = nameGO.AddComponent<LayoutElement>();
            nameLE.preferredHeight = 48f;
            nameLE.flexibleWidth = 1f;

            var hintGO = new GameObject("Hint", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            hintGO.transform.SetParent(card.transform, false);
            var hintTxt = hintGO.GetComponent<TextMeshProUGUI>();
            hintTxt.font = _fontTitle;
            hintTxt.text = !owned && price < 0
                ? LocalizationManager.Get("chest.title")
                : "";
            hintTxt.fontSize = 24;
            hintTxt.fontStyle = FontStyles.Bold;
            hintTxt.color = new Color(0.55f, 0.50f, 0.44f, 1f);
            hintTxt.alignment = TextAlignmentOptions.Center;
            hintTxt.raycastTarget = false;
            var hintLE = hintGO.AddComponent<LayoutElement>();
            hintLE.preferredHeight = string.IsNullOrEmpty(hintTxt.text) ? 0f : 34f;
            hintLE.flexibleWidth = 1f;

            if (!owned && price > 0)
            {
                bool afford = CurrencyManager.HasCoins(price);
                var buyGO = new GameObject("Buy", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                buyGO.transform.SetParent(card.transform, false);
                var buyImg = buyGO.GetComponent<Image>();
                Sprite buyNormal = afford ? JellyUI.ButtonYellow : JellyUI.ButtonGrey;
                buyImg.sprite = buyNormal ?? JellyUI.ButtonGrey;
                buyImg.type = Image.Type.Sliced;
                var buyLE = buyGO.AddComponent<LayoutElement>();
                buyLE.preferredHeight = 96f;
                buyLE.flexibleWidth = 1f;
                var buyBtn = buyGO.GetComponent<Button>();
                JellyUI.ApplyJellyButton(buyBtn, buyImg, buyNormal, JellyUI.ButtonYellow, JellyUI.ButtonRed, JellyUI.ButtonGrey);
                buyBtn.interactable = afford;
                var buyTxtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                buyTxtGO.transform.SetParent(buyGO.transform, false);
                var buyTxtRect = buyTxtGO.GetComponent<RectTransform>();
                buyTxtRect.anchorMin = Vector2.zero;
                buyTxtRect.anchorMax = Vector2.one;
                buyTxtRect.offsetMin = new Vector2(8f, 4f);
                buyTxtRect.offsetMax = new Vector2(-8f, -4f);
                var buyTxt = buyTxtGO.GetComponent<TextMeshProUGUI>();
                buyTxt.font = _fontTitle;
                buyTxt.text = LocalizationManager.Get("shop.buy", price);
                buyTxt.fontSize = 30;
                buyTxt.fontStyle = FontStyles.Bold;
                buyTxt.color = afford ? Color.white : new Color(1f, 1f, 1f, 0.6f);
                buyTxt.alignment = TextAlignmentOptions.Center;
                buyTxt.raycastTarget = false;
                buyBtn.onClick.AddListener(() =>
                {
                    OnMascotBuy(index, price);
                    Object.Destroy(root);
                    RebuildRows();
                });
            }

            var closeGO = new GameObject("Close", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            closeGO.transform.SetParent(card.transform, false);
            var closeImg = closeGO.GetComponent<Image>();
            closeImg.sprite = JellyUI.ButtonGrey;
            closeImg.type = Image.Type.Sliced;
            var closeLE = closeGO.AddComponent<LayoutElement>();
            closeLE.preferredHeight = 76f;
            closeLE.flexibleWidth = 1f;
            var closeBtn = closeGO.GetComponent<Button>();
            JellyUI.ApplyJellyButton(closeBtn, closeImg, JellyUI.ButtonGrey, JellyUI.ButtonGrey, JellyUI.ButtonRed, JellyUI.ButtonGrey);
            closeBtn.onClick.AddListener(() => { SFXManager.Instance.PlayMenuClose(); Object.Destroy(root); });
            var closeTxtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            closeTxtGO.transform.SetParent(closeGO.transform, false);
            var closeTxtRect = closeTxtGO.GetComponent<RectTransform>();
            closeTxtRect.anchorMin = Vector2.zero;
            closeTxtRect.anchorMax = Vector2.one;
            closeTxtRect.offsetMin = Vector2.zero;
            closeTxtRect.offsetMax = Vector2.zero;
            var closeTxt = closeTxtGO.GetComponent<TextMeshProUGUI>();
            closeTxt.font = _fontTitle;
            closeTxt.text = LocalizationManager.Get("menu.ok");
            closeTxt.fontSize = 30;
            closeTxt.fontStyle = FontStyles.Bold;
            closeTxt.color = Color.white;
            closeTxt.alignment = TextAlignmentOptions.Center;
            closeTxt.raycastTarget = false;
        }

        private static Color RarityInk(string r) => r switch
        {
            "C" => new Color(0.184f, 0.239f, 0.180f, 1f),
            "R" => new Color(0.141f, 0.204f, 0.302f, 1f),
            "E" => new Color(0.227f, 0.165f, 0.353f, 1f),
            _ => new Color(0.290f, 0.204f, 0.063f, 1f),
        };

        private static void OnMascotBuy(int index, int price)
        {
            if (StarChestManager.OwnsMascot(index)) return;
            if (!CurrencyManager.SpendCoins(price))
            {
                SFXManager.Instance.PlayDialogueBlip();
                return;
            }
            StarChestManager.SetOwned(index);
            try { AnalyticsManager.LogMascotUnlocked("mx" + index, StarChestManager.RarityOf(index)); } catch { }
            SFXManager.Instance.PlayUnlock();
            RefreshCoins();
            RebuildRows();
        }
    }
}
