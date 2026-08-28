using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zoologic
{
    /// <summary>
    /// Interface permanente (HUD) affichée pendant le jeu :
    ///  - en-tête (bouton retour, numéro de niveau, bouton réglages) ;
    ///  - barre de règles (3 cartes horizontales) ;
    ///  - cœurs / vies (3 images heart.png) ;
    ///  - score (en haut à gauche) ;
    ///  - compteur d'indices (en bas, icône potion.png) ;
    ///  - panneau de défaite (fond sombre + « Réessayer »).
    ///
    /// Tout est construit procéduralement (aucun prefab). Les sprites
    /// (formes arrondies) sont générés par texture à la demande.
    /// </summary>
    public sealed class GameHUD : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Constantes de layout (référence 1080x1920).
        // ------------------------------------------------------------------

        private const float HeaderPadding = 22f;
        private const float RuleBarHeight = 92f;

        // Encoche simulée (px réf 1080x1920) utilisée quand la safe area réelle
        // est nulle (éditeur, desktop) afin de prévisualiser l'espacement.
        private const float SimulatedTopNotch = 70f;

        // ------------------------------------------------------------------
        // Couleurs.
        // ------------------------------------------------------------------

        private static readonly Color HeaderBgColor = new Color(1f, 1f, 1f, 0.97f);
        private static readonly Color RuleCardBg = new Color(0.99f, 0.97f, 0.93f, 1f);
        private static readonly Color PillColor = new Color(0.62f, 0.80f, 0.96f, 1f);
        private static readonly Color PillTextColor = new Color(0.10f, 0.30f, 0.52f, 1f);
        private static readonly Color HeartFullColor = new Color(0.93f, 0.22f, 0.33f, 1f);
        private static readonly Color HeartEmptyColor = new Color(0.80f, 0.80f, 0.82f, 0.4f);
        private static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color RetryButtonColor = new Color(0.15f, 0.50f, 0.92f, 1f);
        private static readonly Color HintCountColor = new Color(0.35f, 0.40f, 0.50f, 1f);
        private static readonly Color IconTextColor = new Color(0.42f, 0.47f, 0.52f, 1f);
        private static readonly Color CardShadowColor = new Color(0f, 0f, 0f, 0.16f);

        // Cartes de règles : une teinte pastel distincte par règle (fini, plus "greybox").
        private static readonly Color RuleCardBgColor1 = new Color(1.00f, 0.97f, 0.91f, 1f);
        private static readonly Color RuleCardBgColor2 = new Color(0.93f, 0.97f, 1.00f, 1f);
        private static readonly Color RuleCardBgColor3 = new Color(1.00f, 0.93f, 0.97f, 1f);
        private static readonly Color RuleAccentColor1 = new Color(0.95f, 0.65f, 0.20f, 1f);
        private static readonly Color RuleAccentColor2 = new Color(0.30f, 0.55f, 0.90f, 1f);
        private static readonly Color RuleAccentColor3 = new Color(0.92f, 0.30f, 0.55f, 1f);

        // Tuile de bouton d'en-tête (retour / réglages) : pastel cohérente entre les deux.
        private static readonly Color HeaderTileBg = new Color(0.91f, 0.94f, 0.97f, 1f);
        private static readonly Color HeaderTileIcon = new Color(0.25f, 0.35f, 0.45f, 1f);
        private static readonly Color HeartPillBg = new Color(1f, 0.94f, 0.95f, 1f);

        private static readonly Color ScorePillBg = new Color(0.98f, 0.85f, 0.24f, 1f);
        private static readonly Color ScorePillTextColor = new Color(0.42f, 0.28f, 0.02f, 1f);
        private static readonly Color HintPillBg = new Color(0.78f, 0.89f, 1f, 1f);
        private static readonly Color HintPillTextColor = new Color(0.10f, 0.35f, 0.78f, 1f);
        private static readonly Color CoinPillTextColor = new Color(0.75f, 0.55f, 0.05f, 1f);
        private static readonly Color CoinInsufficientColor = new Color(0.85f, 0.30f, 0.30f, 1f);
        private static readonly Color ScoreLabelColor = new Color(0.50f, 0.52f, 0.56f, 1f);
        private static readonly Color ScoreValueColor = new Color(0.13f, 0.13f, 0.15f, 1f);
        private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.28f);

        // ------------------------------------------------------------------
        // Champs privés.
        // ------------------------------------------------------------------

        private TMP_FontAsset _fontTitle;
        private TMP_FontAsset _fontBody;

        // Score
        private TextMeshProUGUI _scoreValueText;
        private int _score;
        private Image _scorePillBg;
        private RectTransform _scorePillRect;
        private Color _scorePillBgColor;
        private Coroutine _scorePunchRoutine;
        private static readonly Color ScorePunchBgColor = new Color(0.95f, 0.55f, 0.45f, 1f);

        // Cœurs (images, pas de texte)
        private readonly Image[] _heartImages = new Image[LivesManager.ViesDepart];
        private readonly GameObject[] _heartRoots = new GameObject[LivesManager.ViesDepart];
        private Coroutine[] _heartAnimRoutines = new Coroutine[LivesManager.ViesDepart];

        // Panneau défaite
        private GameObject _defaitePanel;
        private GameObject _gameOverRoot;
        private GameObject _overlay;
        private Image _defeatOwl;

        // Indice
        private TextMeshProUGUI _indiceCountText;
        private int _indiceCount;
        private Image _indiceIconImage;
        private Coroutine _indiceBounceRoutine;

        // Pièces : solde affiché + indicateur d'achat d'indice.
        private TextMeshProUGUI _coinsValueText;
        private Image _coinsIconImage;
        private Image _indiceCoinIconImage;
        private Sprite _coinSprite;
        private Coroutine _toastRoutine;

        // Interactions bloquées
        private bool _interactionsBloquees;

        // Indice button components for graying out
        private Button _indiceButton;
        private Image _indiceButtonBg;
        private static readonly Color IndiceDisabledColor = new Color(0.75f, 0.75f, 0.78f, 0.5f);

        // Distance (px réf) entre le haut de l'écran et le bas du header.
        private float _headerBottom;

        /// <summary>Score actuel affiché.</summary>
        public int Score => _score;

        /// <summary>Nombre d'indices restants.</summary>
        public int IndiceCount => _indiceCount;

        /// <summary>
        /// Encoche haute en unités de canvas (réf 1080x1920). Sur mobile réel on lit
        /// la safe area ; sinon (éditeur/desktop) on applique une encoche simulée.
        /// </summary>
        public float TopInset
        {
            get
            {
                float canvasRefHeight = 1920f;
                Rect safe = Screen.safeArea;
                float safeTop = safe.yMax;
                float screenHeight = Mathf.Max(Screen.height, 1);
                float insetPx = screenHeight - safeTop;
                // La safe area n'est égale à l'écran que si pas d'encoche.
                bool hasNotch = insetPx > 1f;
                if (hasNotch)
                    return insetPx * (canvasRefHeight / screenHeight);
                return SimulatedTopNotch;
            }
        }

        /// <summary>Board offset Y pour centrer la grille dans l'espace HUD en haut et le bas de l'écran.</summary>
        public float BoardYOffset
        {
            get
            {
                // Zone haute : header (encoche comprise) + barre de règles.
                float topOccupied = _headerBottom + RuleBarHeight;
                // Zone basse : marge réservée en bas de l'écran (zone de confort des doigts).
                float bottomReserved = 80f;
                // Référence : on travaille dans l'espace du canvas (hauteur 1920 en compte moyen).
                float canvasHeight = 1920f;
                float availableCenter = (topOccupied + (canvasHeight - bottomReserved)) * 0.5f;
                // Décalage par rapport au centre du canvas (960 = moitié de la hauteur de référence).
                return -(availableCenter - canvasHeight * 0.5f);
            }
        }

        /// <summary>
        /// Construit le HUD complet sous le canvas donné.
        /// </summary>
        /// <param name="canvas">Canvas parent (ScreenSpaceOverlay).</param>
        /// <param name="numeroNiveau">Numéro du niveau à afficher.</param>
        public void Build(Canvas canvas, int numeroNiveau)
        {
            _fontTitle = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Bold SDF");
            _fontBody = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Regular SDF");
            _score = 100;
            _indiceCount = 3;

            BuildHeader(canvas, numeroNiveau);
            BuildBarreRegle(canvas);
        }

        // ------------------------------------------------------------------
        // 1) EN-TÊTE : ← retour | pilule « Niveau X » | ⚙ réglages
        // ------------------------------------------------------------------

        private void BuildHeader(Canvas canvas, int numeroNiveau)
        {
            float inset = TopInset;

            // Distances (px réf) depuis le haut de l'écran : row1 = pilule niveau,
            // row2 = stats. Les deux sont placées sous l'encoche (inset).
            float dRow1 = inset + 28f;
            float dRow2 = inset + 108f;
            // Hauteur du header = bas du contenu (row2 + 28 pilule + marge).
            float H = dRow2 + 42f;
            _headerBottom = H;

            // --- Header background (white bar) ---
            var header = CreerObjetUI("Header", canvas.transform);
            var rect = header.GetComponent<RectTransform>();
            CreerRemplissage(rect, H, ancreHaut: true);

            var image = header.AddComponent<Image>();
            image.color = HeaderBgColor;
            image.raycastTarget = true;

            // Ombre douce sous le header
            var headerOmbre = CreerObjetUI("HeaderOmbre", header.transform);
            var hoRect = headerOmbre.GetComponent<RectTransform>();
            hoRect.anchorMin = new Vector2(0f, 0f);
            hoRect.anchorMax = new Vector2(1f, 0f);
            hoRect.pivot = new Vector2(0.5f, 1f);
            hoRect.sizeDelta = new Vector2(0f, 12f);
            hoRect.anchoredPosition = new Vector2(0f, -2f);
            var hoImg = headerOmbre.AddComponent<Image>();
            hoImg.color = new Color(0f, 0f, 0f, 0.14f);
            hoImg.raycastTarget = false;
            hoRect.SetAsFirstSibling();

            // Séparateur fin en bas
            var separateur = CreerObjetUI("Separateur", header.transform);
            var sepRect = separateur.GetComponent<RectTransform>();
            sepRect.anchorMin = new Vector2(0f, 0f);
            sepRect.anchorMax = new Vector2(1f, 0f);
            sepRect.pivot = new Vector2(0.5f, 1f);
            sepRect.sizeDelta = new Vector2(0f, 2f);
            sepRect.anchoredPosition = Vector2.zero;
            var sepImg = separateur.AddComponent<Image>();
            sepImg.color = new Color(0f, 0f, 0f, 0.10f);
            sepImg.raycastTarget = false;

            // Row 1: ← back | Niveau X pill | ⚙ settings
            float row1Y = H * 0.5f - dRow1;

            var btnRetour = CreerBoutonTuileImage(header.transform, GetBackSprite(), 34f,
                new Vector2(HeaderPadding, row1Y), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Color.white);
            btnRetour.onClick.AddListener(() =>
            {
                SFXManager.Instance.PlayMenuClose();
                SFXManager.Instance.ResumeMusic();
                UnityEngine.SceneManagement.SceneManager.LoadScene("LevelMap");
            });

            CreerPiluleNiveau(header.transform, numeroNiveau, row1Y);

            var btnReglages = CreerBoutonTuileImage(header.transform, GetSettingsSprite(), 34f,
                new Vector2(-HeaderPadding, row1Y), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            btnReglages.onClick.AddListener(() =>
            {
                SFXManager.Instance.PlayMenuOpen();
                SettingsPanel.Open();
            });

            // Row 2: [⭐ 100] [♥♥♥] [🧪×3] — stats row
            float row2Y = H * 0.5f - dRow2;
            BuildStatsRow(header.transform, row2Y);
        }

        // ------------------------------------------------------------------
        // STATS ROW : score pill, hearts, hint pill
        // ------------------------------------------------------------------

        private void BuildStatsRow(Transform header, float y)
        {
            float pillH = 56f;

            // --- Score pill (left) ---
            float scoreW = 150f;
            float scoreX = HeaderPadding;

            var scorePill = CreerObjetUI("ScorePill", header);
            var spRect = scorePill.GetComponent<RectTransform>();
            spRect.anchorMin = new Vector2(0f, 0.5f);
            spRect.anchorMax = new Vector2(0f, 0.5f);
            spRect.pivot = new Vector2(0f, 0.5f);
            spRect.sizeDelta = new Vector2(scoreW, pillH);
            spRect.anchoredPosition = new Vector2(scoreX, y);
            AjouterOmbre(spRect, header, 3f, -5f);

            _scorePillBg = scorePill.AddComponent<Image>();
            _scorePillBg.sprite = GetPiluleSprite();
            _scorePillBg.type = Image.Type.Simple;
            _scorePillBg.color = ScorePillBg;
            _scorePillBg.raycastTarget = false;
            _scorePillRect = spRect;
            _scorePillBgColor = ScorePillBg;

            var scoreTxt = CreerObjetUI("ScoreText", scorePill.transform);
            var stRect = scoreTxt.GetComponent<RectTransform>();
            stRect.anchorMin = Vector2.zero;
            stRect.anchorMax = Vector2.one;
            stRect.offsetMin = Vector2.zero;
            stRect.offsetMax = Vector2.zero;

            _scoreValueText = scoreTxt.AddComponent<TextMeshProUGUI>();
            _scoreValueText.font = _fontTitle;
            _scoreValueText.text = _score.ToString();
            _scoreValueText.fontSize = 34;
            _scoreValueText.alignment = TextAlignmentOptions.Center;
            _scoreValueText.color = ScorePillTextColor;
            _scoreValueText.fontStyle = FontStyles.Bold;
            _scoreValueText.raycastTarget = false;

            // --- Hearts pill (center) ---
            Sprite heartSprite = Resources.Load<Sprite>("UI/heart");
            float heartSize = 40f;
            float heartSpacing = 6f;
            float totalHeartsW = LivesManager.ViesDepart * heartSize + (LivesManager.ViesDepart - 1) * heartSpacing;
            float headerWidth = header is RectTransform hrt && hrt.rect.width > 0f ? hrt.rect.width : 1080f;
            float pillPadX = 22f;
            float heartsPillW = totalHeartsW + pillPadX * 2f;
            float heartsPillH = 56f;

            // Conteneur pilule cohérent avec score (gauche) et indice (droite).
            var heartsPill = CreerObjetUI("HeartsPill", header);
            var hpPillRect = heartsPill.GetComponent<RectTransform>();
            hpPillRect.anchorMin = new Vector2(0f, 0.5f);
            hpPillRect.anchorMax = new Vector2(0f, 0.5f);
            hpPillRect.pivot = new Vector2(0f, 0.5f);
            hpPillRect.sizeDelta = new Vector2(heartsPillW, heartsPillH);
            hpPillRect.anchoredPosition = new Vector2((headerWidth - heartsPillW) * 0.5f, y);
            AjouterOmbre(hpPillRect, header, 3f, -5f);

            var heartsPillImg = heartsPill.AddComponent<Image>();
            heartsPillImg.sprite = GetPiluleSprite();
            heartsPillImg.type = Image.Type.Simple;
            heartsPillImg.color = HeartPillBg;
            heartsPillImg.raycastTarget = false;

            for (int i = 0; i < LivesManager.ViesDepart; i++)
            {
                var heartObj = CreerObjetUI($"Coeur{i}", heartsPill.transform);
                var heartRect = heartObj.GetComponent<RectTransform>();
                heartRect.anchorMin = new Vector2(0f, 0.5f);
                heartRect.anchorMax = new Vector2(0f, 0.5f);
                heartRect.pivot = new Vector2(0.5f, 0.5f);
                heartRect.sizeDelta = new Vector2(heartSize, heartSize);
                heartRect.anchoredPosition = new Vector2(
                    pillPadX + heartSize * 0.5f + i * (heartSize + heartSpacing),
                    0f);

                _heartImages[i] = heartObj.AddComponent<Image>();
                _heartImages[i].sprite = heartSprite;
                _heartImages[i].type = Image.Type.Simple;
                _heartImages[i].preserveAspect = true;
                _heartImages[i].color = HeartFullColor;
                _heartImages[i].raycastTarget = false;
                _heartRoots[i] = heartObj;
            }

            // --- Coin pill (entre les cœurs et la pilule indice) ---
            // Constantes de la pilule indice, déclarées ici pour positionner la pilule pièces.
            float hintW = 132f;
            float hintX = -HeaderPadding - hintW;

            _coinSprite = Resources.Load<Sprite>("UI/coin");
            float coinW = 124f;
            float coinPillX = hintX - coinW - 16f;

            var coinPill = CreerObjetUI("CoinPill", header);
            var cpRect = coinPill.GetComponent<RectTransform>();
            cpRect.anchorMin = new Vector2(1f, 0.5f);
            cpRect.anchorMax = new Vector2(1f, 0.5f);
            cpRect.pivot = new Vector2(1f, 0.5f);
            cpRect.sizeDelta = new Vector2(coinW, pillH);
            cpRect.anchoredPosition = new Vector2(coinPillX, y);
            AjouterOmbre(cpRect, header, 3f, -5f);

            var coinPillImg = coinPill.AddComponent<Image>();
            coinPillImg.sprite = GetPiluleSprite();
            coinPillImg.type = Image.Type.Simple;
            coinPillImg.color = HintPillBg;
            coinPillImg.raycastTarget = false;

            if (_coinSprite != null)
            {
                var coinIconObj = CreerObjetUI("CoinIcone", coinPill.transform);
                var coinIconRect = coinIconObj.GetComponent<RectTransform>();
                coinIconRect.anchorMin = new Vector2(0f, 0.5f);
                coinIconRect.anchorMax = new Vector2(0f, 0.5f);
                coinIconRect.pivot = new Vector2(0.5f, 0.5f);
                coinIconRect.sizeDelta = new Vector2(32f, 32f);
                coinIconRect.anchoredPosition = new Vector2(26f, 0f);

                _coinsIconImage = coinIconObj.AddComponent<Image>();
                _coinsIconImage.sprite = _coinSprite;
                _coinsIconImage.type = Image.Type.Simple;
                _coinsIconImage.preserveAspect = true;
                _coinsIconImage.color = CoinPillTextColor;
                _coinsIconImage.raycastTarget = false;
            }

            var coinCountObj = CreerObjetUI("CoinNombre", coinPill.transform);
            var coinCountRect = coinCountObj.GetComponent<RectTransform>();
            coinCountRect.anchorMin = new Vector2(0f, 0f);
            coinCountRect.anchorMax = new Vector2(1f, 1f);
            coinCountRect.offsetMin = new Vector2(50f, 0f);
            coinCountRect.offsetMax = new Vector2(-10f, 0f);

            _coinsValueText = coinCountObj.AddComponent<TextMeshProUGUI>();
            _coinsValueText.font = _fontTitle;
            _coinsValueText.text = CurrencyManager.GetCoins().ToString();
            _coinsValueText.fontSize = 30;
            _coinsValueText.alignment = TextAlignmentOptions.MidlineRight;
            _coinsValueText.color = CoinPillTextColor;
            _coinsValueText.fontStyle = FontStyles.Bold;
            _coinsValueText.raycastTarget = false;

            // --- Hint pill (right) ---
            var hintPill = CreerObjetUI("HintPill", header);
            var hpRect = hintPill.GetComponent<RectTransform>();
            hpRect.anchorMin = new Vector2(1f, 0.5f);
            hpRect.anchorMax = new Vector2(1f, 0.5f);
            hpRect.pivot = new Vector2(1f, 0.5f);
            hpRect.sizeDelta = new Vector2(hintW, pillH);
            hpRect.anchoredPosition = new Vector2(hintX, y);
            AjouterOmbre(hpRect, header, 3f, -5f);

            _indiceButtonBg = hintPill.AddComponent<Image>();
            _indiceButtonBg.sprite = GetPiluleSprite();
            _indiceButtonBg.type = Image.Type.Simple;
            _indiceButtonBg.color = HintPillBg;
            _indiceButtonBg.raycastTarget = true;

            _indiceButton = hintPill.AddComponent<Button>();
            _indiceButton.targetGraphic = _indiceButtonBg;
            _indiceButton.onClick.AddListener(() => OnIndiceDemande?.Invoke());

            // Potion icon
            Sprite potionSprite = Resources.Load<Sprite>("UI/potion");
            var iconObj = CreerObjetUI("IndiceIcone", hintPill.transform);
            var iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(34f, 34f);
            iconRect.anchoredPosition = new Vector2(28f, 0f);

            _indiceIconImage = iconObj.AddComponent<Image>();
            _indiceIconImage.sprite = potionSprite;
            _indiceIconImage.type = Image.Type.Simple;
            _indiceIconImage.preserveAspect = true;
            _indiceIconImage.color = HintPillTextColor;
            _indiceIconImage.raycastTarget = false;

            // Count text
            var countObj = CreerObjetUI("IndiceNombre", hintPill.transform);
            var countRect = countObj.GetComponent<RectTransform>();
            countRect.anchorMin = new Vector2(0f, 0f);
            countRect.anchorMax = new Vector2(1f, 1f);
            countRect.offsetMin = new Vector2(52f, 0f);
            countRect.offsetMax = new Vector2(-12f, 0f);

            _indiceCountText = countObj.AddComponent<TextMeshProUGUI>();
            _indiceCountText.font = _fontTitle;
            _indiceCountText.text = _indiceCount.ToString();
            _indiceCountText.fontSize = 30;
            _indiceCountText.alignment = TextAlignmentOptions.MidlineRight;
            _indiceCountText.color = HintPillTextColor;
            _indiceCountText.fontStyle = FontStyles.Bold;
            _indiceCountText.raycastTarget = false;

            // Piécette d'achat : visible quand les indices gratuits sont épuisés
            // (l'indice s'achète alors avec des pièces).
            if (_coinSprite != null)
            {
                var coinObj = CreerObjetUI("IndiceAchatIcone", hintPill.transform);
                var coinRect = coinObj.GetComponent<RectTransform>();
                coinRect.anchorMin = new Vector2(1f, 0.5f);
                coinRect.anchorMax = new Vector2(1f, 0.5f);
                coinRect.pivot = new Vector2(0.5f, 0.5f);
                coinRect.sizeDelta = new Vector2(22f, 22f);
                coinRect.anchoredPosition = new Vector2(-30f, 0f);

                _indiceCoinIconImage = coinObj.AddComponent<Image>();
                _indiceCoinIconImage.sprite = _coinSprite;
                _indiceCoinIconImage.type = Image.Type.Simple;
                _indiceCoinIconImage.preserveAspect = true;
                _indiceCoinIconImage.color = CoinPillTextColor;
                _indiceCoinIconImage.raycastTarget = false;
                _indiceCoinIconImage.gameObject.SetActive(false);
            }

            UpdateIndiceButtonState();
            StartIndiceBounce();
        }

        private void CreerPiluleNiveau(Transform parent, int numero, float y)
        {
            var pilule = CreerObjetUI("NiveauPilule", parent);
            var piluleRect = pilule.GetComponent<RectTransform>();
            piluleRect.anchorMin = new Vector2(0.5f, 0.5f);
            piluleRect.anchorMax = new Vector2(0.5f, 0.5f);
            piluleRect.pivot = new Vector2(0.5f, 0.5f);
            piluleRect.sizeDelta = new Vector2(240f, 54f);
            piluleRect.anchoredPosition = new Vector2(0f, y);
            AjouterOmbre(piluleRect, parent, 3f, -5f);

            var piluleImg = pilule.AddComponent<Image>();
            piluleImg.sprite = GetPiluleSprite();
            piluleImg.type = Image.Type.Simple;
            piluleImg.color = PillColor;
            piluleImg.raycastTarget = false;

            var texte = CreerObjetUI("NiveauTexte", pilule.transform);
            var textRect = texte.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = texte.AddComponent<TextMeshProUGUI>();
            text.font = _fontTitle;
            text.text = $"Niveau {numero}";
            text.fontSize = 34;
            text.alignment = TextAlignmentOptions.Center;
            text.color = PillTextColor;
            text.fontStyle = FontStyles.Bold;
            text.raycastTarget = false;
        }

        // ------------------------------------------------------------------
        // 2) BARRE DE RÈGLES : 3 cartes horizontales
        // ------------------------------------------------------------------

        private void BuildBarreRegle(Canvas canvas)
        {
            var container = CreerObjetUI("RegleBar", canvas.transform);
            var rect = container.GetComponent<RectTransform>();

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, RuleBarHeight);
            rect.anchoredPosition = new Vector2(0f, -_headerBottom);

            CreerCarteRegle(container.transform, "\u25CB", "1 par couleur", 0);
            CreerCarteRegle(container.transform, "\u25A1", "1 par ligne\net colonne", 1);
            CreerCarteRegleIcone(container.transform, GetDiagonalArrowSprite(), "Ne peut pas\nse toucher", 2);
        }

        private void CreerCarteRegle(Transform parent, string icone, string label, int index)
        {
            float cardWidth = CalcCardWidth(parent);
            float cardHeight = RuleBarHeight - 20f;
            float spacing = 20f;
            float totalWidth = 3f * cardWidth + 2f * spacing;
            float startX = -totalWidth * 0.5f + cardWidth * 0.5f;
            float x = startX + index * (cardWidth + spacing);

            var carte = CreerObjetUI($"Carte{index}", parent);
            var carteRect = carte.GetComponent<RectTransform>();
            carteRect.anchorMin = new Vector2(0.5f, 0.5f);
            carteRect.anchorMax = new Vector2(0.5f, 0.5f);
            carteRect.pivot = new Vector2(0.5f, 0.5f);
            carteRect.sizeDelta = new Vector2(cardWidth, cardHeight);
            carteRect.anchoredPosition = new Vector2(x, 0f);

            var carteImg = carte.AddComponent<Image>();
            carteImg.sprite = GetCarteSprite();
            carteImg.type = Image.Type.Simple;
            carteImg.color = GetRuleCardBg(index);
            carteImg.raycastTarget = false;

            // Liseré d'accent en haut de carte (finition, distingue chaque règle).
            var accent = CreerObjetUI("Accent", carte.transform);
            var accentRect = accent.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0f, 1f);
            accentRect.anchorMax = new Vector2(1f, 1f);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.sizeDelta = new Vector2(-20f, 8f);
            accentRect.anchoredPosition = new Vector2(0f, -8f);
            var accentImg = accent.AddComponent<Image>();
            accentImg.sprite = GetPiluleSprite();
            accentImg.type = Image.Type.Simple;
            accentImg.color = GetRuleAccent(index);
            accentImg.raycastTarget = false;

            // Ombre sous la carte
            var ombre = CreerObjetUI("Ombre", carte.transform);
            var ombreRect = ombre.GetComponent<RectTransform>();
            ombreRect.anchorMin = Vector2.zero;
            ombreRect.anchorMax = Vector2.one;
            ombreRect.offsetMin = new Vector2(4f, -5f);
            ombreRect.offsetMax = new Vector2(4f, -5f);
            var ombreImg = ombre.AddComponent<Image>();
            ombreImg.color = CardShadowColor;
            ombreImg.raycastTarget = false;
            ombre.transform.SetAsFirstSibling();

            // Icône
            var iconObj = CreerObjetUI("Icone", carte.transform);
            var iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0f);
            iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(56f, 0f);
            iconRect.anchoredPosition = new Vector2(46f, 0f);

            var iconText = iconObj.AddComponent<TextMeshProUGUI>();
            iconText.font = _fontBody;
            iconText.text = icone;
            iconText.fontSize = 38;
            iconText.alignment = TextAlignmentOptions.Center;
            iconText.color = GetRuleAccent(index);
            iconText.fontStyle = FontStyles.Bold;
            iconText.raycastTarget = false;

            // Label
            var labelObj = CreerObjetUI("Label", carte.transform);
            var labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(88f, 10f);
            labelRect.offsetMax = new Vector2(-10f, -10f);

            var labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.font = _fontBody;
            labelText.text = label;
            labelText.fontSize = 26;
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            labelText.color = ScoreValueColor;
            labelText.lineSpacing = 0f;
            labelText.raycastTarget = false;
        }

        // ------------------------------------------------------------------
        // 4) CŒURS : animations.
        // ------------------------------------------------------------------

        public void SetScore(int score)
        {
            if (score == _score)
                return;
            bool decreased = score < _score;
            _score = score;
            if (_scoreValueText != null)
                _scoreValueText.text = _score.ToString();

            if (decreased)
                PunchScore();
        }

        /// <summary>Petit punch rouge sur la pilule de score quand le score diminue.</summary>
        private void PunchScore()
        {
            if (_scorePillBg == null || !Application.isPlaying)
                return;

            if (_scorePunchRoutine != null)
                StopCoroutine(_scorePunchRoutine);
            _scorePunchRoutine = StartCoroutine(ScorePunchRoutine());
        }

        private IEnumerator ScorePunchRoutine()
        {
            Color original = _scorePillBgColor;
            _scorePillBgColor = ScorePunchBgColor;
            if (_scorePillBg != null)
                _scorePillBg.color = ScorePunchBgColor;

            float duration = 0.35f;
            float elapsed = 0f;
            RectTransform pill = _scorePillRect;

            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float scaleFactor = t < 0.5f
                    ? Mathf.Lerp(1f, 1.18f, Easing.EaseOutQuad(t * 2f))
                    : Mathf.Lerp(1.18f, 1f, Easing.EaseOutBack((t - 0.5f) * 2f));

                if (pill != null)
                    pill.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);

                _scorePillBg.color = Color.Lerp(ScorePunchBgColor, original, Easing.EaseOutQuad(t));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (pill != null)
                pill.localScale = Vector3.one;
            _scorePillBg.color = original;
            _scorePillBgColor = original;
            _scorePunchRoutine = null;
        }

        public void SetVies(int vies)
        {
            for (int i = 0; i < _heartImages.Length; i++)
            {
                if (_heartImages[i] == null)
                    continue;

                if (i < vies)
                {
                    // Cœur vivant : blanc (couleur d'origine du sprite).
                    StopHeartAnim(i);
                    _heartImages[i].color = HeartFullColor;
                    _heartRoots[i].transform.localScale = Vector3.one;
                }
                else
                {
                    // Cœur perdu : animation de disparition (scale down + fade out).
                    if (_heartAnimRoutines[i] == null && Application.isPlaying
                        && _heartRoots[i].activeSelf)
                    {
                        _heartAnimRoutines[i] = StartCoroutine(HeartDeathRoutine(i));
                    }
                }
            }
        }

        private IEnumerator HeartDeathRoutine(int index)
        {
            float duration = 0.3f;
            float elapsed = 0f;
            Vector3 startScale = _heartRoots[index].transform.localScale;
            Color startColor = _heartImages[index].color;

            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float ease = 1f - (1f - t) * (1f - t); // easeOutQuad
                _heartRoots[index].transform.localScale = Vector3.Lerp(startScale, Vector3.zero, ease);
                _heartImages[index].color = Color.Lerp(startColor, HeartEmptyColor, ease);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _heartRoots[index].transform.localScale = Vector3.one;
            _heartImages[index].color = HeartEmptyColor;
            _heartAnimRoutines[index] = null;
        }

        private void StopHeartAnim(int index)
        {
            if (_heartAnimRoutines[index] != null)
            {
                StopCoroutine(_heartAnimRoutines[index]);
                _heartAnimRoutines[index] = null;
            }
        }

        /// <summary>
        /// Décrémente le compteur d'indices de 1. Retourne true si un indice était disponible.
        /// </summary>
        public bool DecrementIndice()
        {
            if (_indiceCount <= 0)
                return false;

            _indiceCount--;
            if (_indiceCountText != null)
                _indiceCountText.text = _indiceCount.ToString();

            UpdateIndiceButtonState();
            return true;
        }

        private void StartIndiceBounce()
        {
            if (!Application.isPlaying || _indiceIconImage == null)
                return;
            _indiceBounceRoutine = StartCoroutine(IndiceBounceRoutine());
        }

        private IEnumerator IndiceBounceRoutine()
        {
            RectTransform rt = _indiceIconImage.rectTransform;
            Vector2 basePos = rt.anchoredPosition;
            float amplitude = 3f;
            float speed = 2.5f;

            while (true)
            {
                float y = basePos.y + Mathf.Sin(Time.unscaledTime * speed) * amplitude;
                rt.anchoredPosition = new Vector2(basePos.x, y);
                yield return null;
            }
        }

        private void StopIndiceBounce()
        {
            if (_indiceBounceRoutine != null)
            {
                StopCoroutine(_indiceBounceRoutine);
                _indiceBounceRoutine = null;
            }
            if (_indiceIconImage != null)
            {
                RectTransform rt = _indiceIconImage.rectTransform;
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, 6f);
            }
        }

        /// <summary>
        /// Met à jour l'état visuel du bouton indice. Avec indices gratuits restants
        /// il libelle le nombre restant ; à 0 il passe en « achat » : libellé du coût
        /// en pièces + piécette d'achat. Le bouton reste cliquable tant que les
        /// interactions ne sont pas bloquées.
        /// </summary>
        private void UpdateIndiceButtonState()
        {
            bool purchaseMode = _indiceCount <= 0;
            bool enabled = !_interactionsBloquees;

            if (_indiceButton != null)
                _indiceButton.interactable = enabled;

            if (_indiceIconImage != null)
                _indiceIconImage.color = enabled ? HintPillTextColor : IndiceDisabledColor;

            if (_indiceCountText != null)
            {
                _indiceCountText.text = _indiceCount.ToString();
            }

            if (_indiceCoinIconImage != null)
                _indiceCoinIconImage.gameObject.SetActive(purchaseMode && enabled);

            if (enabled)
                StartIndiceBounce();
            else
                StopIndiceBounce();
        }

        /// <summary>
        /// Rafraîchit l'affichage du solde de pièces (appelé après gain ou dépense).
        /// </summary>
        public void RefreshCoins()
        {
            if (_coinsValueText != null)
                _coinsValueText.text = CurrencyManager.GetCoins().ToString();
        }

        /// <summary>
        /// Court retour visuel quand le joueur n'a pas assez de pièces pour acheter un indice.
        /// La pilule indice rougit brièvement et un petit toast affiche le manque.
        /// </summary>
        public void NotifierPiècesInsuffisantes(int cout)
        {
            if (_indiceButtonBg != null)
            {
                Color original = _indiceButtonBg.color;
                _indiceButtonBg.color = CoinInsufficientColor;
                StartCoroutine(RestoreIndiceBgRoutine(original));
            }

            if (_coinsIconImage != null)
                Punch.FlashAlpha(this, _coinsIconImage, 0.3f, 0.4f);

            ShowCoinToast($"Pas assez de pièces ({cout})");
            Haptics.VibrateLight();
        }

        private IEnumerator RestoreIndiceBgRoutine(Color original)
        {
            float duration = 0.45f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                if (_indiceButtonBg != null)
                    _indiceButtonBg.color = Color.Lerp(CoinInsufficientColor, original, Easing.EaseOutCubic(t));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            if (_indiceButtonBg != null)
                _indiceButtonBg.color = original;
        }

        /// <summary>
        /// Affiche un toast temporaire centré (fond sombre arrondi + texte), puis le retire.
        /// </summary>
        private void ShowCoinToast(string message)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null)
                return;

            GameObject existing = GameObject.Find("CoinToast");
            if (existing != null)
                Destroy(existing);

            var toastObj = CreerObjetUI("CoinToast", canvas.transform);
            var toastRect = toastObj.GetComponent<RectTransform>();
            toastRect.anchorMin = new Vector2(0.5f, 0.5f);
            toastRect.anchorMax = new Vector2(0.5f, 0.5f);
            toastRect.pivot = new Vector2(0.5f, 0.5f);
            toastRect.sizeDelta = new Vector2(560f, 72f);
            toastRect.anchoredPosition = new Vector2(0f, -240f);

            var toastImg = toastObj.AddComponent<Image>();
            toastImg.sprite = GetPiluleSprite();
            toastImg.type = Image.Type.Simple;
            toastImg.color = new Color(0f, 0f, 0f, 0.82f);

            var toastTextObj = CreerObjetUI("Text", toastObj.transform);
            var toastTextRect = toastTextObj.GetComponent<RectTransform>();
            toastTextRect.anchorMin = Vector2.zero;
            toastTextRect.anchorMax = Vector2.one;
            toastTextRect.offsetMin = Vector2.zero;
            toastTextRect.offsetMax = Vector2.zero;

            var toastText = toastTextObj.AddComponent<TextMeshProUGUI>();
            toastText.font = _fontBody;
            toastText.text = message;
            toastText.fontSize = 26;
            toastText.alignment = TextAlignmentOptions.Center;
            toastText.color = Color.white;
            toastText.raycastTarget = false;

            if (_toastRoutine != null)
                StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(CoinToastRoutine(toastObj));
        }

        private IEnumerator CoinToastRoutine(GameObject toastObj)
        {
            float duration = 1.4f;
            float elapsed = 0f;
            float fadeIn = 0.15f;

            CanvasGroup group = toastObj.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                group.alpha = t < fadeIn ? Easing.EaseOutQuad(t / fadeIn) : 1f;
                if (t > 0.7f)
                    group.alpha = 1f - Easing.EaseInQuad((t - 0.7f) / 0.3f);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (toastObj != null)
                Destroy(toastObj);
            _toastRoutine = null;
        }

        // ------------------------------------------------------------------
        // 6) PANNEAU DE DÉFAITE (superposé, caché par défaut)
        // ------------------------------------------------------------------

        private void BuildDefaitePanel(Canvas canvas)
        {
            _gameOverRoot = CreerObjetUI("GameOverRoot", canvas.transform);
            var rootRect = _gameOverRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // Fond semi-transparent
            _overlay = CreerObjetUI("Overlay", _gameOverRoot.transform);
            var overlayRect = _overlay.GetComponent<RectTransform>();
            CreerRemplissage(overlayRect, 0f, ancreHaut: false);
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            var overlayImg = _overlay.AddComponent<Image>();
            overlayImg.color = OverlayColor;
            overlayImg.raycastTarget = true;

            // Panneau central (agrandi pour le hibou)
            _defaitePanel = CreerObjetUI("DefaitePanel", _gameOverRoot.transform);
            var panelRect = _defaitePanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(700f, 480f);
            panelRect.anchoredPosition = Vector2.zero;

            var panelImg = _defaitePanel.AddComponent<Image>();
            panelImg.sprite = GetCarteSprite();
            panelImg.type = Image.Type.Simple;
            panelImg.color = Color.white;

            // Hibou mascotte — inclinaison triste à -12°
            Sprite owlSprite = Resources.Load<Sprite>("Art/Animals/owl");
            if (owlSprite != null)
            {
                var owlObj = CreerObjetUI("DefeatOwl", _defaitePanel.transform);
                var owlRect = owlObj.GetComponent<RectTransform>();
                owlRect.anchorMin = new Vector2(0.5f, 0.82f);
                owlRect.anchorMax = new Vector2(0.5f, 0.82f);
                owlRect.pivot = new Vector2(0.5f, 0.5f);
                owlRect.sizeDelta = new Vector2(110f, 110f);
                owlRect.anchoredPosition = Vector2.zero;
                owlRect.localRotation = Quaternion.Euler(0f, 0f, -12f);

                _defeatOwl = owlObj.AddComponent<Image>();
                _defeatOwl.sprite = owlSprite;
                _defeatOwl.preserveAspect = true;
                _defeatOwl.color = new Color(1f, 1f, 1f, 0f);
                _defeatOwl.raycastTarget = false;
            }

            // Titre « Niveau échoué » (décalé vers le bas)
            var titreObj = CreerObjetUI("Titre", _defaitePanel.transform);
            var titreRect = titreObj.GetComponent<RectTransform>();
            titreRect.anchorMin = new Vector2(0f, 0.58f);
            titreRect.anchorMax = new Vector2(1f, 0.78f);
            titreRect.offsetMin = Vector2.zero;
            titreRect.offsetMax = Vector2.zero;

            var titreText = titreObj.AddComponent<TextMeshProUGUI>();
            titreText.font = _fontTitle;
            titreText.text = "Niveau échoué";
            titreText.fontSize = 42;
            titreText.alignment = TextAlignmentOptions.Center;
            titreText.color = new Color(0.15f, 0.15f, 0.18f, 1f);
            titreText.fontStyle = FontStyles.Bold;
            titreText.raycastTarget = false;

            // Sous-titre « Plus de vies ! »
            var sousObj = CreerObjetUI("SousTitre", _defaitePanel.transform);
            var sousRect = sousObj.GetComponent<RectTransform>();
            sousRect.anchorMin = new Vector2(0f, 0.42f);
            sousRect.anchorMax = new Vector2(1f, 0.58f);
            sousRect.offsetMin = Vector2.zero;
            sousRect.offsetMax = Vector2.zero;

            var sousText = sousObj.AddComponent<TextMeshProUGUI>();
            sousText.font = _fontBody;
            sousText.text = "Plus de vies !";
            sousText.fontSize = 28;
            sousText.alignment = TextAlignmentOptions.Center;
            sousText.color = ScoreLabelColor;
            sousText.raycastTarget = false;

            // Bouton « Réessayer »
            var btnObj = CreerObjetUI("BtnReessayer", _defaitePanel.transform);
            var btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0.08f);
            btnRect.anchorMax = new Vector2(0.5f, 0.08f);
            btnRect.pivot = new Vector2(0.5f, 0.5f);
            btnRect.sizeDelta = new Vector2(320f, 70f);
            btnRect.anchoredPosition = Vector2.zero;

            var btnImg = btnObj.AddComponent<Image>();
            btnImg.sprite = GetCarteSprite();
            btnImg.type = Image.Type.Simple;
            btnImg.color = RetryButtonColor;

            var btnComp = btnObj.AddComponent<Button>();
            btnComp.targetGraphic = btnImg;
            btnComp.onClick.AddListener(() => OnReessayer?.Invoke());

            var btnTextObj = CreerObjetUI("Texte", btnObj.transform);
            var btnTextRect = btnTextObj.GetComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;

            var btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnText.font = _fontTitle;
            btnText.text = "Réessayer";
            btnText.fontSize = 30;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.color = Color.white;
            btnText.fontStyle = FontStyles.Bold;
            btnText.raycastTarget = false;

            // Le root reste actif ; ce sont overlay et panel qui basculent.
            _overlay.SetActive(false);
            _defaitePanel.SetActive(false);
        }

        /// <summary>Événement invoqué quand le bouton « Réessayer » est pressé.</summary>
        public Action OnReessayer;

        /// <summary>Événement invoqué quand le bouton indice est pressé.</summary>
        public Action OnIndiceDemande;

        /// <summary>
        /// Construit le panneau de défaite. À appeler APRÈS que la grille a été
        /// construite, pour qu'il soit le dernier enfant du canvas (rendu au premier plan).
        /// </summary>
        public void CreerPanneauDefaite(Canvas canvas)
        {
            BuildDefaitePanel(canvas);
        }

        public void AfficherDefaite()
        {
            if (_overlay != null)
                _overlay.SetActive(true);
            if (_defaitePanel != null)
                _defaitePanel.SetActive(true);

            if (_defeatOwl != null)
                StartCoroutine(DefeatOwlFadeInRoutine());
        }

        private IEnumerator DefeatOwlFadeInRoutine()
        {
            Transform owlT = _defeatOwl.transform;
            float startY = owlT.localPosition.y + 20f;
            float endY = owlT.localPosition.y;
            float duration = 0.6f;
            float elapsed = 0f;

            owlT.localPosition = new Vector3(owlT.localPosition.x, startY, owlT.localPosition.z);
            _defeatOwl.color = new Color(1f, 1f, 1f, 0f);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Easing.EaseOutCubic(t);
                _defeatOwl.color = new Color(1f, 1f, 1f, eased);
                owlT.localPosition = Vector3.Lerp(
                    new Vector3(owlT.localPosition.x, startY, owlT.localPosition.z),
                    new Vector3(owlT.localPosition.x, endY, owlT.localPosition.z),
                    eased);
                yield return null;
            }

            _defeatOwl.color = Color.white;
            owlT.localPosition = new Vector3(owlT.localPosition.x, endY, owlT.localPosition.z);
        }

        public void CacherDefaite()
        {
            if (_overlay != null)
                _overlay.SetActive(false);
            if (_defaitePanel != null)
                _defaitePanel.SetActive(false);

            if (_defeatOwl != null)
            {
                _defeatOwl.color = new Color(1f, 1f, 1f, 0f);
                _defeatOwl.transform.localPosition = Vector3.zero;
            }
        }

        // ------------------------------------------------------------------
        // Blocage des interactions.
        // ------------------------------------------------------------------

        public void BloquerInteractions(bool bloquer)
        {
            _interactionsBloquees = bloquer;
            UpdateIndiceButtonState();
        }

        public bool InteractionsBloquees => _interactionsBloquees;

        // ------------------------------------------------------------------
        // Réinitialisation complète du HUD (appelé par le contrôleur).
        // ------------------------------------------------------------------

        public void Reinitialiser(int score, int vies, int indices)
        {
            _score = score;
            if (_scoreValueText != null)
                _scoreValueText.text = _score.ToString();

            SetVies(vies);

            _indiceCount = indices;
            if (_indiceCountText != null)
                _indiceCountText.text = _indiceCount.ToString();

            CacherDefaite();
            BloquerInteractions(false);
            RefreshCoins();
            UpdateIndiceButtonState();
        }

        // ------------------------------------------------------------------
        // Utilitaires UI : création d'objets, sprites procéduraux.
        // ------------------------------------------------------------------

        private static GameObject CreerObjetUI(string nom, Transform parent)
        {
            var go = new GameObject(nom, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>
        /// Ajoute une ombre portée douce sous un élément (pilule...). L'ombre est
        /// insérée comme frère juste derrière l'élément, calquée sur sa position.
        /// </summary>
        private void AjouterOmbre(RectTransform target, Transform holder, float offX, float offY)
        {
            var ombre = CreerObjetUI("Ombre", holder);
            var ombreRect = ombre.GetComponent<RectTransform>();
            ombreRect.anchorMin = target.anchorMin;
            ombreRect.anchorMax = target.anchorMax;
            ombreRect.pivot = target.pivot;
            ombreRect.sizeDelta = target.sizeDelta;
            ombreRect.anchoredPosition = target.anchoredPosition + new Vector2(offX, offY);
            var ombreImg = ombre.AddComponent<Image>();
            ombreImg.sprite = GetPiluleSprite();
            ombreImg.type = Image.Type.Simple;
            ombreImg.color = ShadowColor;
            ombreImg.raycastTarget = false;
            ombreImg.rectTransform.SetAsLastSibling();
            if (target.parent != null)
                ombreImg.rectTransform.SetSiblingIndex(target.GetSiblingIndex());
        }

        private static void CreerRemplissage(RectTransform rect, float hauteur, bool ancreHaut)
        {            if (ancreHaut)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(0f, hauteur);
                rect.anchoredPosition = Vector2.zero;
            }
            else
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
        }

        private Button CreerBouton(Transform parent, string label, float fontSize,
            Vector2 anchoredPos, Vector2 anchor, Vector2 pivot)
        {
            var btnObj = CreerObjetUI($"Btn{label}", parent);
            var btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = anchor;
            btnRect.anchorMax = anchor;
            btnRect.pivot = pivot;
            btnRect.sizeDelta = new Vector2(70f, 70f);
            btnRect.anchoredPosition = anchoredPos;

            var btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0f, 0f, 0f, 0f); // invisible, juste cliquable
            btnImg.raycastTarget = true;

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImg;

            var textObj = CreerObjetUI("Texte", btnObj.transform);
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.font = _fontTitle;
            text.text = label;
            text.fontSize = (int)fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = ScoreValueColor;
            text.raycastTarget = false;

            return btn;
        }

        /// <summary>
        /// Comme CreerBouton mais avec une icône Image au lieu d'un texte.
        /// Utilisé pour le bouton réglages (engrenage) dont le caractère Unicode
        /// n'est pas rendu correctement par la police.
        /// </summary>
        private Button CreerBoutonImage(Transform parent, Sprite iconSprite, float iconSize,
            Vector2 anchoredPos, Vector2 anchor, Vector2 pivot)
        {
            var btnObj = CreerObjetUI("BtnImage", parent);
            var btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = anchor;
            btnRect.anchorMax = anchor;
            btnRect.pivot = pivot;
            btnRect.sizeDelta = new Vector2(70f, 70f);
            btnRect.anchoredPosition = anchoredPos;

            var btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0f, 0f, 0f, 0f);
            btnImg.raycastTarget = true;

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImg;

            var iconObj = CreerObjetUI("Icone", btnObj.transform);
            var iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

            var iconImage = iconObj.AddComponent<Image>();
            iconImage.sprite = iconSprite;
            iconImage.type = Image.Type.Simple;
            iconImage.preserveAspect = true;
            iconImage.color = Color.white;
            iconImage.raycastTarget = false;

            return btn;
        }

        /// <summary>
        /// Bouton d'en-tête carré/arrondi avec fond (tuile pastel) + icône image.
        /// Symétrie visuelle avec le retour (contrairement à une icône flottante nue).
        /// </summary>
        private Button CreerBoutonTuileImage(Transform parent, Sprite iconSprite, float iconSize,
            Vector2 anchoredPos, Vector2 anchor, Vector2 pivot, Color? tint = null)
        {
            var btnObj = CreerObjetUI("BtnTuile", parent);
            var btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = anchor;
            btnRect.anchorMax = anchor;
            btnRect.pivot = pivot;
            btnRect.sizeDelta = new Vector2(64f, 64f);
            btnRect.anchoredPosition = anchoredPos;

            var btnImg = btnObj.AddComponent<Image>();
            btnImg.sprite = GetPiluleSprite();
            btnImg.type = Image.Type.Simple;
            btnImg.color = HeaderTileBg;
            btnImg.raycastTarget = true;
            AjouterOmbre(btnRect, parent, 2f, -3f);

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            var colors = btn.colors;
            colors.pressedColor = new Color(0.80f, 0.86f, 0.92f, 1f);
            btn.colors = colors;

            var iconObj = CreerObjetUI("Icone", btnObj.transform);
            var iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

            var iconImage = iconObj.AddComponent<Image>();
            iconImage.sprite = iconSprite;
            iconImage.type = Image.Type.Simple;
            iconImage.preserveAspect = true;
            iconImage.color = tint ?? HeaderTileIcon;
            iconImage.raycastTarget = false;

            return btn;
        }

        /// <summary>
        /// Variante texte du bouton tuile (utilisée pour la flèche de retour ←).
        /// Même tuile pastel que CreerBoutonTuileImage pour garder la symétrie.
        /// </summary>
        private Button CreerBoutonTuileTexte(Transform parent, string glyph, float fontSize,
            Vector2 anchoredPos, Vector2 anchor, Vector2 pivot)
        {
            var btnObj = CreerObjetUI("BtnTuile", parent);
            var btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = anchor;
            btnRect.anchorMax = anchor;
            btnRect.pivot = pivot;
            btnRect.sizeDelta = new Vector2(64f, 64f);
            btnRect.anchoredPosition = anchoredPos;

            var btnImg = btnObj.AddComponent<Image>();
            btnImg.sprite = GetPiluleSprite();
            btnImg.type = Image.Type.Simple;
            btnImg.color = HeaderTileBg;
            btnImg.raycastTarget = true;
            AjouterOmbre(btnRect, parent, 2f, -3f);

            var btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            var colors = btn.colors;
            colors.pressedColor = new Color(0.80f, 0.86f, 0.92f, 1f);
            btn.colors = colors;

            var textObj = CreerObjetUI("Texte", btnObj.transform);
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObj.AddComponent<TextMeshProUGUI>();
            text.font = _fontTitle;
            text.text = glyph;
            text.fontSize = (int)fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = HeaderTileIcon;
            text.raycastTarget = false;

            return btn;
        }

        /// <summary>
        /// Variante de CreerCarteRegle avec un sprite procédural au lieu d'un
        /// caractère texte pour l'icône. Utilisé pour la carte "diagonale" dont
        /// le caractère ↘ n'est pas rendu correctement par la police.
        /// </summary>
        /// <summary>Largeur d'une carte de règle, proportionnelle à la largeur du conteneur (robuste aux ratios).</summary>
        private static float CalcCardWidth(Transform parent)
        {
            if (parent is RectTransform prt && prt.rect.width > 0f)
            {
                // 3 cartes + 2 espaces de 20px, avec une marge de 20px de chaque côté.
                float usable = prt.rect.width - 2f * 20f - 2f * 20f;
                return Mathf.Clamp(usable / 3f, 220f, 360f);
            }
            return 320f;
        }

        private void CreerCarteRegleIcone(Transform parent, Sprite iconSprite, string label, int index)
        {
            float cardWidth = CalcCardWidth(parent);
            float cardHeight = RuleBarHeight - 20f;
            float spacing = 20f;
            float totalWidth = 3f * cardWidth + 2f * spacing;
            float startX = -totalWidth * 0.5f + cardWidth * 0.5f;
            float x = startX + index * (cardWidth + spacing);

            var carte = CreerObjetUI($"Carte{index}", parent);
            var carteRect = carte.GetComponent<RectTransform>();
            carteRect.anchorMin = new Vector2(0.5f, 0.5f);
            carteRect.anchorMax = new Vector2(0.5f, 0.5f);
            carteRect.pivot = new Vector2(0.5f, 0.5f);
            carteRect.sizeDelta = new Vector2(cardWidth, cardHeight);
            carteRect.anchoredPosition = new Vector2(x, 0f);

            var carteImg = carte.AddComponent<Image>();
            carteImg.sprite = GetCarteSprite();
            carteImg.type = Image.Type.Simple;
            carteImg.color = GetRuleCardBg(index);
            carteImg.raycastTarget = false;

            // Liseré d'accent en haut de carte (finition, distingue chaque règle).
            var accent = CreerObjetUI("Accent", carte.transform);
            var accentRect = accent.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0f, 1f);
            accentRect.anchorMax = new Vector2(1f, 1f);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.sizeDelta = new Vector2(-20f, 8f);
            accentRect.anchoredPosition = new Vector2(0f, -8f);
            var accentImg = accent.AddComponent<Image>();
            accentImg.sprite = GetPiluleSprite();
            accentImg.type = Image.Type.Simple;
            accentImg.color = GetRuleAccent(index);
            accentImg.raycastTarget = false;

            // Ombre sous la carte
            var ombre = CreerObjetUI("Ombre", carte.transform);
            var ombreRect = ombre.GetComponent<RectTransform>();
            ombreRect.anchorMin = Vector2.zero;
            ombreRect.anchorMax = Vector2.one;
            ombreRect.offsetMin = new Vector2(4f, -5f);
            ombreRect.offsetMax = new Vector2(4f, -5f);
            var ombreImg = ombre.AddComponent<Image>();
            ombreImg.color = CardShadowColor;
            ombreImg.raycastTarget = false;
            ombre.transform.SetAsFirstSibling();

            // Icône sprite (au lieu de texte)
            var iconObj = CreerObjetUI("Icone", carte.transform);
            var iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0f);
            iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(56f, 0f);
            iconRect.anchoredPosition = new Vector2(46f, 0f);

            var iconImage = iconObj.AddComponent<Image>();
            iconImage.sprite = iconSprite;
            iconImage.type = Image.Type.Simple;
            iconImage.preserveAspect = true;
            iconImage.color = GetRuleAccent(index);
            iconImage.raycastTarget = false;

            // Label
            var labelObj = CreerObjetUI("Label", carte.transform);
            var labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(88f, 10f);
            labelRect.offsetMax = new Vector2(-10f, -10f);

            var labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.font = _fontBody;
            labelText.text = label;
            labelText.fontSize = 26;
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            labelText.color = ScoreValueColor;
            labelText.lineSpacing = 0f;
            labelText.raycastTarget = false;
        }

        // ------------------------------------------------------------------
        // Sprites procéduraux (coins arrondis).
        // ------------------------------------------------------------------

        private static Sprite _piluleSprite;
        private static Sprite _carteSprite;
        private static Sprite _settingsSprite;
        private static Sprite _backSprite;
        private static Sprite _diagonalNoTouchSprite;

        private static Sprite GetPiluleSprite()
        {
            if (_piluleSprite == null)
                _piluleSprite = CreerSpriteRectangleArrondi(256, 0.35f);
            return _piluleSprite;
        }

        private static Sprite GetCarteSprite()
        {
            if (_carteSprite == null)
                _carteSprite = CreerSpriteRectangleArrondi(256, 0.15f);
            return _carteSprite;
        }

        private static Sprite GetSettingsSprite()
        {
            if (_settingsSprite == null)
                _settingsSprite = Resources.Load<Sprite>("UI/settings");
            return _settingsSprite;
        }

        private static Sprite GetBackSprite()
        {
            if (_backSprite == null)
                _backSprite = Resources.Load<Sprite>("UI/retour");
            return _backSprite;
        }

        private static Sprite GetDiagonalArrowSprite()
        {
            if (_diagonalNoTouchSprite == null)
                _diagonalNoTouchSprite = CreerSpriteDiagonaleInterdite(128);
            return _diagonalNoTouchSprite;
        }

        private static Color GetRuleCardBg(int index)
        {
            switch (index)
            {
                case 0: return RuleCardBgColor1;
                case 1: return RuleCardBgColor2;
                default: return RuleCardBgColor3;
            }
        }

        private static Color GetRuleAccent(int index)
        {
            switch (index)
            {
                case 0: return RuleAccentColor1;
                case 1: return RuleAccentColor2;
                default: return RuleAccentColor3;
            }
        }

        private static Sprite CreerSpriteRectangleArrondi(int resolution, float coinRatio)
        {
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float half = (resolution - 1) * 0.5f;
            float radius = resolution * coinRatio;
            float inner = half - radius;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float px = x - half;
                    float py = y - half;

                    float qx = Mathf.Clamp(px, -inner, inner);
                    float qy = Mathf.Clamp(py, -inner, inner);
                    float dx = px - qx;
                    float dy = py - qy;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    float alpha = Mathf.Clamp01(radius + 0.5f - distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, resolution, resolution),
                new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// Engrenage simplifié : cercle central + 8 dents rectangulaires
        /// réparties autour, pour représenter un bouton "réglages".
        /// </summary>
        private static Sprite CreerSpriteEngrenage(int resolution)
        {
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float center = (resolution - 1) * 0.5f;
            float outerRadius = resolution * 0.46f;
            float hubRadius = resolution * 0.20f;
            float toothWidth = resolution * 0.12f;
            float toothLength = resolution * 0.18f;
            int toothCount = 8;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);

                    bool inside = false;

                    // Cercle central (moyeu).
                    if (dist <= hubRadius)
                        inside = true;

                    // 8 dents rectangulaires.
                    for (int i = 0; i < toothCount; i++)
                    {
                        float toothAngle = (2f * Mathf.PI * i) / toothCount;
                        float angleDiff = Mathf.Abs(angle - toothAngle);
                        if (angleDiff > Mathf.PI)
                            angleDiff = 2f * Mathf.PI - angleDiff;

                        float halfWidth = Mathf.Atan2(toothWidth * 0.5f, outerRadius);
                        if (angleDiff <= halfWidth && dist >= hubRadius && dist <= outerRadius + toothLength)
                            inside = true;
                    }

                    float alpha = inside ? 1f : 0f;
                    // Adoucir le bord.
                    if (!inside)
                    {
                        float edgeDist = dist - (outerRadius + toothLength);
                        if (edgeDist > -1.5f && edgeDist < 0f)
                            alpha = Mathf.Clamp01(edgeDist + 1.5f);
                    }

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, resolution, resolution),
                new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// Flèche diagonale vers le bas-droite : ligne diagonale + pointe.
        /// Pour la règle "ne peut pas se toucher" (diagonale adjacente).
        /// </summary>
        /// <summary>
        /// Pictogramme "ne peut pas se toucher en diagonale" : mini-grille 2x2
        /// (4 cases) dont la paire diagonale est reliée puis barrée par un trait
        /// de prohibition. Beaucoup plus lisible qu'une simple flèche diagonale.
        /// </summary>
        private static Sprite CreerSpriteDiagonaleInterdite(int resolution)
        {
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float n = resolution - 1f;
            float gap = resolution * 0.06f;       // espace entre cases
            float cell = (n / 2f) - gap * 0.5f;   // taille d'une case
            float stroke = resolution * 0.06f;    // épaisseur des traits

            // Centres des 4 cases (coin supérieur gauche de chaque case).
            float[] cellX = new float[] { gap, gap + cell + gap, gap, gap + cell + gap };
            float[] cellY = new float[] { gap, gap, gap + cell + gap, gap + cell + gap };

            // Paire diagonale à souligner : haut-gauche (0) et bas-droite (3).
            float d0x = gap + cell * 0.5f;
            float d0y = gap + cell * 0.5f;
            float d1x = gap + cell + gap + cell * 0.5f;
            float d1y = d1x;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    bool inside = false;

                    // 1) Contours des 4 cases.
                    for (int c = 0; c < 4; c++)
                    {
                        float cx = cellX[c];
                        float cy = cellY[c];
                        bool nearLeft = Mathf.Abs(x - cx) <= stroke && y >= cy && y <= cy + cell;
                        bool nearRight = Mathf.Abs(x - (cx + cell)) <= stroke && y >= cy && y <= cy + cell;
                        bool nearBottom = Mathf.Abs(y - cy) <= stroke && x >= cx && x <= cx + cell;
                        bool nearTop = Mathf.Abs(y - (cy + cell)) <= stroke && x >= cx && x <= cx + cell;
                        if (nearLeft || nearRight || nearBottom || nearTop)
                            inside = true;
                    }

                    // 2) Paire diagonale reliée (haut-gauche -> bas-droite).
                    float dx = d1x - d0x;
                    float dy = d1y - d0y;
                    float len = Mathf.Sqrt(dx * dx + dy * dy);
                    float nx = -dy / len;
                    float ny = dx / len;
                    float px = x - d0x;
                    float py = y - d0y;
                    float proj = (px * dx + py * dy) / (len * len);
                    float dist = Mathf.Abs(px * nx + py * ny);
                    if (proj >= -0.1f && proj <= 1.1f && dist <= stroke * 0.9f)
                        inside = true;

                    // 3) Trait de prohibition (anti-diagonal).
                    float px2 = x - d0x;
                    float py2 = y - d0y;
                    float proj2 = (px2 * dy + py2 * dx) / (len * len);
                    float dist2 = Mathf.Abs(px2 * nx + py2 * ny);
                    if (proj2 >= 0.25f && proj2 <= 0.85f && dist2 <= stroke * 0.9f)
                        inside = true;

                    float alpha = inside ? 1f : 0f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, resolution, resolution),
                new Vector2(0.5f, 0.5f));
        }
    }
}
