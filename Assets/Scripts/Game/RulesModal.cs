using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zoologic.Localization;

namespace Zoologic
{
    /// <summary>
    /// Modal "?" DesignDoctor Défaut #2 : remplace les 3 pills de règles permanentes.
    /// - Bouton "?" 48dp mini (132px ref) dans le header.
    /// - Ouverture pop 0.25s EaseOutBack + fade (sans DOTween : utilise Easing maison).
    /// - WCAG AA : texte #4E342E (8.1:1), secondaire #795548 (4.9:1), fond #FFF8EC.
    /// - Canvas dédié overrideSorting=10 pour limiter SendWillRenderCanvases du board.
    ///
    /// Usage :
    ///   _rulesModal = RulesModal.Build(canvas, fontTitle, fontBody);
    ///   helpButton.onClick.AddListener(() => _rulesModal.Toggle());
    ///   // Tutoriel N1 : _rulesModal.ShowRuleOnly(0); // "1 par couleur" seule
    /// </summary>
    public sealed class RulesModal : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _panelRect;
        [SerializeField] private GameObject _root;

        private Coroutine _anim;
        private bool _visible;
        private TMP_FontAsset _fontTitle;
        private TMP_FontAsset _fontBody;
        private GameObject[] _ruleCards = new GameObject[3];

        private static readonly Color TitleBrown = new Color(0.30f, 0.20f, 0.18f, 1f); // #4E342E
        private static readonly Color SecondaryBrown = new Color(0.47f, 0.33f, 0.28f, 1f); // #795548
        private static readonly Color CardBg = new Color(1f, 0.98f, 0.96f, 1f);
        private static readonly Color AccentOrange = new Color(0.95f, 0.65f, 0.20f, 1f);
        private static readonly Color AccentBlue = new Color(0.30f, 0.55f, 0.90f, 1f);
        private static readonly Color AccentPink = new Color(0.92f, 0.30f, 0.55f, 1f);

        public bool IsVisible => _visible;

        public static RulesModal Build(Canvas parent, TMP_FontAsset fontTitle, TMP_FontAsset fontBody)
        {
            var go = new GameObject("RulesModal", typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            var modal = go.AddComponent<RulesModal>();
            modal._fontTitle = fontTitle;
            modal._fontBody = fontBody;
            modal.BuildHierarchy(parent);
            go.SetActive(false);
            return modal;
        }

        private void BuildHierarchy(Canvas parent)
        {
            var rect = (RectTransform)transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _root = gameObject;

            // Canvas dédié popups : isole les rebuilds (board statique non rebatché).
            var sortCanvas = gameObject.AddComponent<Canvas>();
            sortCanvas.overrideSorting = true;
            sortCanvas.sortingOrder = 10;
            gameObject.AddComponent<GraphicRaycaster>();

            // Backdrop (clic = fermer), raycast actif mais 1 seul draw call.
            var bgGO = new GameObject("Backdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGO.transform.SetParent(transform, false);
            var bgRect = (RectTransform)bgGO.transform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = bgGO.GetComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.45f);
            bgImg.raycastTarget = true;
            var bgBtn = bgGO.AddComponent<Button>();
            bgBtn.targetGraphic = bgImg;
            bgBtn.transition = Selectable.Transition.None;
            bgBtn.onClick.AddListener(Hide);

            // Panel carte crème radius 16dp + padding 16dp + ombre.
            var panelGO = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelGO.transform.SetParent(transform, false);
            _panelRect = (RectTransform)panelGO.transform;
            _panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            _panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            _panelRect.pivot = new Vector2(0.5f, 0.5f);
            _panelRect.sizeDelta = new Vector2(760f, 560f);
            _panelRect.anchoredPosition = Vector2.zero;
            var panelImg = panelGO.GetComponent<Image>();
            panelImg.sprite = GetRoundedSprite();
            panelImg.type = Image.Type.Sliced;
            panelImg.color = CardBg;
            panelImg.raycastTarget = false;
            var sh = panelGO.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.28f);
            sh.effectDistance = new Vector2(0f, -8f);

            var vlg = panelGO.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(32, 32, 28, 28);
            vlg.spacing = 18f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Titre 20sp Bold #4E342E
            _ruleCards = new GameObject[3];
            var titleGO = new GameObject("Title", typeof(RectTransform));
            titleGO.transform.SetParent(panelGO.transform, false);
            var titleLE = titleGO.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 56f;
            titleLE.flexibleWidth = 1f;
            var titleTxt = titleGO.AddComponent<TextMeshProUGUI>();
            titleTxt.font = _fontTitle;
            titleTxt.text = ResolveLoc("hud.how_to_play", "Comment jouer ?");
            titleTxt.fontSize = 40;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.alignment = TextAlignmentOptions.Center;
            titleTxt.color = TitleBrown;
            titleTxt.raycastTarget = false;

            string[] keys = { "hud.rule_color", "hud.rule_rowcol", "hud.rule_no_touch" };
            string[] fallback = { "1 par couleur", "1 par ligne et colonne", "Ne peut pas se toucher" };
            Color[] accents = { AccentOrange, AccentBlue, AccentPink };
            string[] glyphs = { "●", "▢", "⤫" };

            for (int i = 0; i < 3; i++)
            {
                string label = ResolveLoc(keys[i], fallback[i]);
                _ruleCards[i] = CreateRuleRow(panelGO.transform, glyphs[i], label, accents[i]);
            }

            // Bouton Fermer 48dp min (132px ref x 96px), 1 seul selectable.
            var closeGO = new GameObject("BtnFermer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            closeGO.transform.SetParent(panelGO.transform, false);
            var closeLE = closeGO.AddComponent<LayoutElement>();
            closeLE.preferredHeight = 96f;
            closeLE.flexibleWidth = 1f;
            var closeImg = closeGO.GetComponent<Image>();
            closeImg.sprite = GetRoundedSprite();
            closeImg.type = Image.Type.Sliced;
            closeImg.color = TitleBrown;
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(Hide);

            var closeTxtGO = new GameObject("Text", typeof(RectTransform));
            closeTxtGO.transform.SetParent(closeGO.transform, false);
            var closeTxtRect = (RectTransform)closeTxtGO.transform;
            closeTxtRect.anchorMin = Vector2.zero;
            closeTxtRect.anchorMax = Vector2.one;
            closeTxtRect.offsetMin = Vector2.zero;
            closeTxtRect.offsetMax = Vector2.zero;
            var closeTxt = closeTxtGO.AddComponent<TextMeshProUGUI>();
            closeTxt.font = _fontTitle;
            closeTxt.text = ResolveLoc("menu.ok", "Compris !");
            closeTxt.fontSize = 32;
            closeTxt.fontStyle = FontStyles.Bold;
            closeTxt.alignment = TextAlignmentOptions.Center;
            closeTxt.color = Color.white;
            closeTxt.raycastTarget = false;

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.interactable = false;
            _group.blocksRaycasts = false;
        }

        private GameObject CreateRuleRow(Transform parent, string glyph, string label, Color accent)
        {
            var rowGO = new GameObject("Rule", typeof(RectTransform));
            rowGO.transform.SetParent(parent, false);
            var le = rowGO.AddComponent<LayoutElement>();
            le.preferredHeight = 104f;
            le.flexibleWidth = 1f;

            var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.padding = new RectOffset(8, 8, 8, 8);

            // Pastille 56px + glyphe blanc (contraste AAA sur accent).
            var badgeGO = new GameObject("Badge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            badgeGO.transform.SetParent(rowGO.transform, false);
            var badgeLE = badgeGO.AddComponent<LayoutElement>();
            badgeLE.preferredWidth = 76f;
            badgeLE.preferredHeight = 76f;
            var badgeImg = badgeGO.GetComponent<Image>();
            badgeImg.sprite = GetRoundedSprite();
            badgeImg.color = accent;
            badgeImg.raycastTarget = false;

            var glyphGO = new GameObject("Glyph", typeof(RectTransform));
            glyphGO.transform.SetParent(badgeGO.transform, false);
            var glyphRect = (RectTransform)glyphGO.transform;
            glyphRect.anchorMin = Vector2.zero;
            glyphRect.anchorMax = Vector2.one;
            glyphRect.offsetMin = Vector2.zero;
            glyphRect.offsetMax = new Vector2(0f, 4f);
            var glyphTxt = glyphGO.AddComponent<TextMeshProUGUI>();
            glyphTxt.font = _fontTitle;
            glyphTxt.text = glyph;
            glyphTxt.fontSize = 38;
            glyphTxt.fontStyle = FontStyles.Bold;
            glyphTxt.alignment = TextAlignmentOptions.Center;
            glyphTxt.color = Color.white;
            glyphTxt.raycastTarget = false;

            var labelGO = new GameObject("Label", typeof(RectTransform));
            labelGO.transform.SetParent(rowGO.transform, false);
            var labelLE = labelGO.AddComponent<LayoutElement>();
            labelLE.flexibleWidth = 1f;
            labelLE.preferredHeight = 88f;
            var labelTxt = labelGO.AddComponent<TextMeshProUGUI>();
            labelTxt.font = _fontTitle;
            labelTxt.text = label;
            labelTxt.fontSize = 30;
            labelTxt.fontSizeMin = 24;
            labelTxt.fontSizeMax = 32;
            labelTxt.enableAutoSizing = true;
            labelTxt.fontStyle = FontStyles.Bold;
            labelTxt.alignment = TextAlignmentOptions.MidlineLeft;
            labelTxt.color = TitleBrown; // 8.1:1 sur crème
            labelTxt.textWrappingMode = TextWrappingModes.Normal;
            labelTxt.overflowMode = TextOverflowModes.Truncate;
            labelTxt.raycastTarget = false;
            LocalizationManager.ApplyTo(labelTxt);
            return rowGO;
        }

        public void Toggle()
        {
            if (_visible) Hide();
            else Show();
        }

        public void Show()
        {
            if (_anim != null) StopCoroutine(_anim);
            gameObject.SetActive(true);
            _visible = true;
            _anim = StartCoroutine(ShowRoutine());
        }

        public void Hide()
        {
            if (!gameObject.activeSelf) return;
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(HideRoutine());
            _visible = false;
        }

        /// <summary>Tutoriel N1 : ne montre qu'une règle (0=couleur), masque les 2 autres.</summary>
        public void ShowRuleOnly(int index)
        {
            for (int i = 0; i < _ruleCards.Length; i++)
                if (_ruleCards[i] != null)
                    _ruleCards[i].SetActive(i == index);
            Show();
        }

        public void ShowAllRules()
        {
            for (int i = 0; i < _ruleCards.Length; i++)
                if (_ruleCards[i] != null)
                    _ruleCards[i].SetActive(true);
        }

        private IEnumerator ShowRoutine()
        {
            _group.interactable = true;
            _group.blocksRaycasts = true;
            float dur = 0.25f;
            float t = 0f;
            _panelRect.localScale = new Vector3(0.8f, 0.8f, 1f);
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                float s = Easing.EaseOutBack(k); // = DOTween OutBack sans dépendance
                _panelRect.localScale = new Vector3(s, s, 1f);
                _group.alpha = Easing.EaseOutQuad(k);
                yield return null;
            }
            _panelRect.localScale = Vector3.one;
            _group.alpha = 1f;
            _anim = null;
        }

        private IEnumerator HideRoutine()
        {
            _group.interactable = false;
            _group.blocksRaycasts = false;
            float dur = 0.18f;
            float t = 0f;
            float startAlpha = _group.alpha;
            Vector3 startScale = _panelRect.localScale;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                _group.alpha = Mathf.Lerp(startAlpha, 0f, k);
                _panelRect.localScale = Vector3.Lerp(startScale, new Vector3(0.85f, 0.85f, 1f), k);
                yield return null;
            }
            _group.alpha = 0f;
            gameObject.SetActive(false);
            _anim = null;
        }

        /// <summary>
        /// Get() retourne "[key]" si clé absente : IsNullOrEmpty ne suffit pas.
        /// Détecte "[...]" et retombe sur le français en dur (jamais de clé brute).
        /// </summary>
        private static string ResolveLoc(string key, string fallbackFr)
        {
            string v = LocalizationManager.Get(key);
            if (string.IsNullOrEmpty(v)) return fallbackFr;
            v = v.Trim();
            if (v.Length >= 2 && v[0] == '[' && v[v.Length - 1] == ']')
                return fallbackFr;
            return v;
        }

        private static Sprite _rounded;
        private static Sprite GetRoundedSprite()
        {
            if (_rounded != null) return _rounded;
            const int res = 128;
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            float half = (res - 1) * 0.5f;
            float radius = res * 0.22f;
            float inner = half - radius;
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                {
                    float px = x - half, py = y - half;
                    float qx = Mathf.Clamp(px, -inner, inner);
                    float qy = Mathf.Clamp(py, -inner, inner);
                    float d = Mathf.Sqrt((px - qx) * (px - qx) + (py - qy) * (py - qy));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius + 0.5f - d)));
                }
            tex.Apply();
            _rounded = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f));
            _rounded.name = "RulesModalRounded";
            return _rounded;
        }
    }
}
