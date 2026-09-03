using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zoologic
{
    /// <summary>
    /// Interface permanente (HUD) affich�e pendant le jeu :
    ///  - en-t�te (bouton retour, num�ro de niveau, bouton r�glages) ;
    ///  - barre de r�gles (3 cartes horizontales) ;
    ///  - c�urs / vies (3 images heart.png) ;
    ///  - score (en haut � gauche) ;
    ///  - compteur d'indices (en bas, ic�ne potion.png) ;
    ///  - panneau de d�faite (fond sombre + � R�essayer �).
    ///
    /// Tout est construit proc�duralement (aucun prefab). Les sprites
    /// (formes arrondies) sont g�n�r�s par texture � la demande.
    /// </summary>
    public sealed class GameHUD : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Constantes de layout (r�f�rence 1080x1920).
        // ------------------------------------------------------------------

        private const float HeaderPadding = 22f;
        private const float RuleBarHeight = 92f;

        // Encoche simul�e (px r�f 1080x1920) utilis�e quand la safe area r�elle
        // est nulle (�diteur, desktop) afin de pr�visualiser l'espacement.
        private const float SimulatedTopNotch = 70f;

        // ------------------------------------------------------------------
        // Couleurs.
        // ------------------------------------------------------------------

        private static readonly Color HeaderBgColor = new Color(1f, 1f, 1f, 0f);
        private static readonly Color TitleBrown = new Color(0.29f, 0.18f, 0.10f, 1f);
        private static readonly Color RuleCardBg = new Color(0.99f, 0.97f, 0.93f, 1f);
        private static readonly Color PillColor = new Color(0.62f, 0.80f, 0.96f, 1f);
        private static readonly Color PillTextColor = new Color(0.10f, 0.30f, 0.52f, 1f);
        private static readonly Color HeartFullColor = new Color(0.93f, 0.22f, 0.33f, 1f);
        private static readonly Color HeartEmptyColor = new Color(0.80f, 0.80f, 0.82f, 0.55f);
        private static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color RetryButtonColor = new Color(0.15f, 0.50f, 0.92f, 1f);
        private static readonly Color HintCountColor = new Color(0.35f, 0.40f, 0.50f, 1f);
        private static readonly Color IconTextColor = new Color(0.42f, 0.47f, 0.52f, 1f);
        private static readonly Color CardShadowColor = new Color(0f, 0f, 0f, 0.16f);

        // Cartes de r�gles : une teinte pastel distincte par r�gle (fini, plus "greybox").
        private static readonly Color RuleCardBgColor1 = new Color(1.00f, 0.97f, 0.91f, 1f);
        private static readonly Color RuleCardBgColor2 = new Color(0.93f, 0.97f, 1.00f, 1f);
        private static readonly Color RuleCardBgColor3 = new Color(1.00f, 0.93f, 0.97f, 1f);
        private static readonly Color RuleAccentColor1 = new Color(0.95f, 0.65f, 0.20f, 1f);
        private static readonly Color RuleAccentColor2 = new Color(0.30f, 0.55f, 0.90f, 1f);
        private static readonly Color RuleAccentColor3 = new Color(0.92f, 0.30f, 0.55f, 1f);

        private static readonly Color HeaderTileBg = new Color(1f, 0.98f, 0.96f, 1f);
        private static readonly Color HeaderTileIcon = new Color(0.29f, 0.18f, 0.10f, 1f);
        private static readonly Color HeartPillBg = new Color(1f, 0.98f, 0.96f, 1f);
        private static readonly Color BubbleBorderLight = new Color(0.92f, 0.89f, 0.86f, 1f);
        private static readonly Color ScorePillBg = new Color(1f, 0.98f, 0.96f, 1f);
        private static readonly Color ScorePillTextColor = new Color(0.22f, 0.19f, 0.16f, 1f);
        private static readonly Color HintPillBg = new Color(1f, 0.98f, 0.96f, 1f);
        private static readonly Color HintPillTextColor = new Color(0.22f, 0.19f, 0.16f, 1f);
        private static readonly Color CoinPillTextColor = new Color(0.22f, 0.19f, 0.16f, 1f);
        private static readonly Color CoinInsufficientColor = new Color(0.85f, 0.30f, 0.30f, 1f);
        private static readonly Color GumBgColor = new Color(0.92f, 0.36f, 0.42f, 1f);
        private static readonly Color ScoreLabelColor = new Color(0.50f, 0.52f, 0.56f, 1f);
        private static readonly Color ScoreValueColor = new Color(0.13f, 0.13f, 0.15f, 1f);
        private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.28f);

        // ------------------------------------------------------------------
        // Champs priv�s.
        // ------------------------------------------------------------------

        private TMP_FontAsset _fontTitle;
        private TMP_FontAsset _fontBody;

        // Score (interne, non affich� en pill jeu � gard� pour logique)
        private TextMeshProUGUI _scoreValueText;
        private int _score;
        private Image _scorePillBg;
        private RectTransform _scorePillRect;
        private Color _scorePillBgColor;
        private Coroutine _scorePunchRoutine;
        private static readonly Color ScorePunchBgColor = new Color(0.95f, 0.55f, 0.45f, 1f);

        // Progression jeu : ?? X/Y chats/animaux plac�s
        private Image _progressionIconImage;
        private TextMeshProUGUI _progressionText;
        private int _progressionTotal = 5;

        // C�urs (images, pas de texte)
        private readonly Image[] _heartImages = new Image[LivesManager.ViesDepart];
        private readonly GameObject[] _heartRoots = new GameObject[LivesManager.ViesDepart];
        private Coroutine[] _heartAnimRoutines = new Coroutine[LivesManager.ViesDepart];
        private TextMeshProUGUI _livesTimerText;
        private Coroutine _livesTimerRoutine;

        // Panneau d�faite
        private GameObject _defaitePanel;
        private GameObject _gameOverRoot;
        private GameObject _overlay;
        private Image _defeatOwl;

        // Indice
        private TextMeshProUGUI _indiceCountText;
        private int _indiceCount;
        private Image _indiceIconImage;
        private Coroutine _indiceBounceRoutine;

        // Pi�ces : solde affich� + indicateur d'achat d'indice.
        private TextMeshProUGUI _coinsValueText;
        private Image _coinsIconImage;
        private Image _indiceCoinIconImage;
        private Sprite _coinSprite;
        private Coroutine _toastRoutine;

        // Power-up � gomme � : bouton flottant en bas d'�cran.
        private Button _gommeButton;
        private Image _gommeButtonBg;
        private Coroutine _gommeRechargeRoutine;

        // Interactions bloqu�es
        private bool _interactionsBloquees;

        // Indice button components for graying out
        private Button _indiceButton;
        private Image _indiceButtonBg;
        private static readonly Color IndiceDisabledColor = new Color(0.75f, 0.75f, 0.78f, 0.5f);

        // Distance (px r�f) entre le haut de l'�cran et le bas du header.
        private float _headerBottom;

        /// <summary>Score actuel affich�.</summary>
        public int Score => _score;

        /// <summary>Nombre d'indices restants.</summary>
        public int IndiceCount => _indiceCount;

        /// <summary>
        /// Encoche haute en unit�s de canvas (r�f 1080x1920). Sur mobile r�el on lit
        /// la safe area ; sinon (�diteur/desktop) on applique une encoche simul�e.
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
                // La safe area n'est �gale � l'�cran que si pas d'encoche.
                bool hasNotch = insetPx > 1f;
                if (hasNotch)
                    return insetPx * (canvasRefHeight / screenHeight);
                return SimulatedTopNotch;
            }
        }

        /// <summary>Board offset Y pour centrer la grille dans l'espace HUD en haut et le bas de l'�cran.</summary>
        public float BoardYOffset
        {
            get
            {
                // Zone haute : header (encoche comprise) + barre de r�gles.
                float topOccupied = _headerBottom + RuleBarHeight;
                float bottomReserved = 35f;
                // R�f�rence : on travaille dans l'espace du canvas (hauteur 1920 en compte moyen).
                float canvasHeight = 1920f;
                float availableCenter = (topOccupied + (canvasHeight - bottomReserved)) * 0.5f;
                // D�calage par rapport au centre du canvas (960 = moiti� de la hauteur de r�f�rence).
                return -(availableCenter - canvasHeight * 0.5f);
            }
        }

        /// <summary>
        /// Construit le HUD complet sous le canvas donn�.
        /// </summary>
        /// <param name="canvas">Canvas parent (ScreenSpaceOverlay).</param>
        /// <param name="numeroNiveau">Num�ro du niveau � afficher.</param>
        public void Build(Canvas canvas, int numeroNiveau)
        {
            _fontTitle = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Bold SDF");
            _fontBody = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Regular SDF");
            _score = 100;
            _indiceCount = 3;

            BuildHeader(canvas, numeroNiveau);
            BuildBarreRegle(canvas);
            BuildFooterWave(canvas);
            BuildGommeBouton(canvas);
        }

        // ------------------------------------------------------------------
        // 1) EN-T�TE : ? retour | pilule � Niveau X � | ? r�glages
        // ------------------------------------------------------------------

        private void BuildHeader(Canvas canvas, int numeroNiveau)
        {
            float inset = TopInset;

            // Distances (px r�f) depuis le haut de l'�cran : row1 = pilule niveau,
            // row2 = stats. Les deux sont plac�es sous l'encoche (inset).
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
            image.raycastTarget = false;

            // Row 1: ? back | Niveau X pill | ? settings
            float row1Y = H * 0.5f - dRow1;

            var btnRetour = CreerBoutonTuileImage(header.transform, GetBackSprite(), 34f,
                new Vector2(HeaderPadding, row1Y), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Color.white);
            btnRetour.onClick.AddListener(() =>
            {
                SFXManager.Instance.PlayMenuClose();
                SFXManager.Instance.ResumeMusic();
                PuzzleGameController.IsDailyPuzzle = false;
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

            // Row 2: [? 100] [???] [??�3] � stats row
            float row2Y = H * 0.5f - dRow2;
            BuildStatsRow(header.transform, row2Y);
        }

        // ------------------------------------------------------------------
        // STATS ROW : score pill, hearts, hint pill
        // ------------------------------------------------------------------

        private void BuildStatsRow(Transform header, float y)
        {
            float pillH = 56f;

            // --- Progression pill (left) : ?? 1/5 � remplace le score technique par un feedback jeu ---
            float progW = 150f;
            float progX = HeaderPadding;

            var progPill = CreerObjetUI("ProgressionPill", header);
            var progRect = progPill.GetComponent<RectTransform>();
            progRect.anchorMin = new Vector2(0f, 0.5f);
            progRect.anchorMax = new Vector2(0f, 0.5f);
            progRect.pivot = new Vector2(0f, 0.5f);
            progRect.sizeDelta = new Vector2(progW, 52f);
            progRect.anchoredPosition = new Vector2(progX, y);
            AjouterOmbre(progRect, header, 3f, -5f);

            _scorePillBg = progPill.AddComponent<Image>();
            _scorePillBg.sprite = GetPiluleSprite();
            _scorePillBg.type = Image.Type.Simple;
            _scorePillBg.color = new Color(1f, 0.98f, 0.96f, 1f);
            _scorePillBg.raycastTarget = false;
            _scorePillRect = progRect;
            _scorePillBgColor = _scorePillBg.color;

            var progIconGO = CreerObjetUI("ProgIcon", progPill.transform);
            var progIconRect = progIconGO.GetComponent<RectTransform>();
            progIconRect.anchorMin = new Vector2(0f, 0.5f);
            progIconRect.anchorMax = new Vector2(0f, 0.5f);
            progIconRect.pivot = new Vector2(0.5f, 0.5f);
            progIconRect.sizeDelta = new Vector2(34f, 34f);
            progIconRect.anchoredPosition = new Vector2(22f, 0f);
            _progressionIconImage = progIconGO.AddComponent<Image>();
            _progressionIconImage.sprite = Resources.Load<Sprite>("Art/Animals/cat") ?? Resources.Load<Sprite>("Art/Animals/bear");
            if (_progressionIconImage.sprite == null)
            {
                var all = AnimalIconSet.LoadAll();
                if (all != null && all.Length > 0) _progressionIconImage.sprite = all[0];
            }
            _progressionIconImage.type = Image.Type.Simple;
            _progressionIconImage.preserveAspect = true;
            _progressionIconImage.raycastTarget = false;

            var progTxtGO = CreerObjetUI("ProgText", progPill.transform);
            var progTxtRect = progTxtGO.GetComponent<RectTransform>();
            progTxtRect.anchorMin = new Vector2(0f, 0f);
            progTxtRect.anchorMax = new Vector2(1f, 1f);
            progTxtRect.offsetMin = new Vector2(58f, 0f);
            progTxtRect.offsetMax = new Vector2(-10f, 0f);
            _progressionText = progTxtGO.AddComponent<TextMeshProUGUI>();
            _progressionText.font = _fontTitle;
            _progressionText.text = $"<color=#22C55E>0</color><color=#4A2C12>/{Mathf.Max(_progressionTotal, 5)}</color>";
            _progressionText.fontSize = 30;
            _progressionText.alignment = TextAlignmentOptions.MidlineLeft;
            _progressionText.fontStyle = FontStyles.Bold;
            _progressionText.raycastTarget = false;

            // --- Hearts pill (center) ---
            Sprite heartSprite = Resources.Load<Sprite>("UI/heart");
            float heartSize = 40f;
            float heartSpacing = 6f;
            float totalHeartsW = LivesManager.ViesDepart * heartSize + (LivesManager.ViesDepart - 1) * heartSpacing;
            float headerWidth = header is RectTransform hrt && hrt.rect.width > 0f ? hrt.rect.width : 1080f;
            float pillPadX = 22f;
            float heartsPillW = totalHeartsW + pillPadX * 2f;
            float heartsPillH = 56f;

            // Conteneur pilule coh�rent avec score (gauche) et indice (droite).
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

            // --- Pilule �conomie combin�e (pi�ces | indice) : une seule pilule � droite ---
            _coinSprite = Resources.Load<Sprite>("UI/coin");
            float economyW = 202f;
            float economyX = -HeaderPadding;

            var economyPill = CreerObjetUI("EconomyPill", header);
            var epRect = economyPill.GetComponent<RectTransform>();
            epRect.anchorMin = new Vector2(1f, 0.5f);
            epRect.anchorMax = new Vector2(1f, 0.5f);
            epRect.pivot = new Vector2(1f, 0.5f);
            epRect.sizeDelta = new Vector2(economyW, pillH);
            epRect.anchoredPosition = new Vector2(economyX, y);
            AjouterOmbre(epRect, header, 3f, -5f);

            _indiceButtonBg = economyPill.AddComponent<Image>();
            _indiceButtonBg.sprite = GetPiluleSprite();
            _indiceButtonBg.type = Image.Type.Simple;
            _indiceButtonBg.color = HintPillBg;
            _indiceButtonBg.raycastTarget = true;

            _indiceButton = economyPill.AddComponent<Button>();
            _indiceButton.targetGraphic = _indiceButtonBg;
            _indiceButton.onClick.AddListener(() => OnIndiceDemande?.Invoke());

            if (_coinSprite != null)
            {
                var coinIconObj = CreerObjetUI("CoinIcone", economyPill.transform);
                var coinIconRect = coinIconObj.GetComponent<RectTransform>();
                coinIconRect.anchorMin = new Vector2(0f, 0.5f);
                coinIconRect.anchorMax = new Vector2(0f, 0.5f);
                coinIconRect.pivot = new Vector2(0.5f, 0.5f);
                coinIconRect.sizeDelta = new Vector2(32f, 32f);
                coinIconRect.anchoredPosition = new Vector2(22f, 0f);

                _coinsIconImage = coinIconObj.AddComponent<Image>();
                _coinsIconImage.sprite = _coinSprite;
                _coinsIconImage.type = Image.Type.Simple;
                _coinsIconImage.preserveAspect = true;
                _coinsIconImage.color = CoinPillTextColor;
                _coinsIconImage.raycastTarget = false;
            }

            var coinCountObj = CreerObjetUI("CoinNombre", economyPill.transform);
            var coinCountRect = coinCountObj.GetComponent<RectTransform>();
            coinCountRect.anchorMin = new Vector2(0f, 0.5f);
            coinCountRect.anchorMax = new Vector2(0f, 0.5f);
            coinCountRect.pivot = new Vector2(0f, 0.5f);
            coinCountRect.sizeDelta = new Vector2(52f, pillH);
            coinCountRect.anchoredPosition = new Vector2(52f, 0f);

            _coinsValueText = coinCountObj.AddComponent<TextMeshProUGUI>();
            _coinsValueText.font = _fontTitle;
            _coinsValueText.text = CurrencyManager.GetCoins().ToString();
            _coinsValueText.fontSize = 28;
            _coinsValueText.alignment = TextAlignmentOptions.MidlineLeft;
            _coinsValueText.color = CoinPillTextColor;
            _coinsValueText.fontStyle = FontStyles.Bold;
            _coinsValueText.raycastTarget = false;

            var divider = CreerObjetUI("Divider", economyPill.transform);
            var divRect = divider.GetComponent<RectTransform>();
            divRect.anchorMin = new Vector2(0.5f, 0.15f);
            divRect.anchorMax = new Vector2(0.5f, 0.85f);
            divRect.pivot = new Vector2(0.5f, 0.5f);
            divRect.sizeDelta = new Vector2(2f, 0f);
            divRect.anchoredPosition = new Vector2(1f, 0f);
            var divImg = divider.AddComponent<Image>();
            divImg.color = new Color(0f, 0f, 0f, 0.12f);
            divImg.raycastTarget = false;

            Sprite potionSprite = Resources.Load<Sprite>("UI/potion");
            var hintIconObj = CreerObjetUI("IndiceIcone", economyPill.transform);
            var hintIconRect = hintIconObj.GetComponent<RectTransform>();
            hintIconRect.anchorMin = new Vector2(0f, 0.5f);
            hintIconRect.anchorMax = new Vector2(0f, 0.5f);
            hintIconRect.pivot = new Vector2(0.5f, 0.5f);
            hintIconRect.sizeDelta = new Vector2(32f, 32f);
            hintIconRect.anchoredPosition = new Vector2(118f, 0f);

            _indiceIconImage = hintIconObj.AddComponent<Image>();
            _indiceIconImage.sprite = potionSprite;
            _indiceIconImage.type = Image.Type.Simple;
            _indiceIconImage.preserveAspect = true;
            _indiceIconImage.color = HintPillTextColor;
            _indiceIconImage.raycastTarget = false;

            var hintCountObj = CreerObjetUI("IndiceNombre", economyPill.transform);
            var hintCountRect = hintCountObj.GetComponent<RectTransform>();
            hintCountRect.anchorMin = new Vector2(0f, 0.5f);
            hintCountRect.anchorMax = new Vector2(0f, 0.5f);
            hintCountRect.pivot = new Vector2(0f, 0.5f);
            hintCountRect.sizeDelta = new Vector2(36f, pillH);
            hintCountRect.anchoredPosition = new Vector2(148f, 0f);

            _indiceCountText = hintCountObj.AddComponent<TextMeshProUGUI>();
            _indiceCountText.font = _fontTitle;
            _indiceCountText.text = _indiceCount.ToString();
            _indiceCountText.fontSize = 28;
            _indiceCountText.alignment = TextAlignmentOptions.MidlineLeft;
            _indiceCountText.color = HintPillTextColor;
            _indiceCountText.fontStyle = FontStyles.Bold;
            _indiceCountText.raycastTarget = false;

            if (_coinSprite != null)
            {
                var coinObj = CreerObjetUI("IndiceAchatIcone", economyPill.transform);
                var coinRect = coinObj.GetComponent<RectTransform>();
                coinRect.anchorMin = new Vector2(0f, 0.5f);
                coinRect.anchorMax = new Vector2(0f, 0.5f);
                coinRect.pivot = new Vector2(0.5f, 0.5f);
                coinRect.sizeDelta = new Vector2(18f, 18f);
                coinRect.anchoredPosition = new Vector2(170f, 0f);

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
            var texte = CreerObjetUI("NiveauTitre", parent);
            var textRect = texte.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(400f, 54f);
            textRect.anchoredPosition = new Vector2(0f, y);

            var text = texte.AddComponent<TextMeshProUGUI>();
            text.font = _fontTitle;
            text.text = PuzzleGameController.IsDailyPuzzle ? "D�fi du jour" : $"Niveau {numero}";
            text.fontSize = 48;
            text.alignment = TextAlignmentOptions.Center;
            text.color = TitleBrown;
            text.fontStyle = FontStyles.Bold;
            text.raycastTarget = false;
            text.outlineWidth = 0.34f;
            text.outlineColor = new Color(1f, 1f, 1f, 0.85f);
        }

        // ------------------------------------------------------------------
        // 2) BARRE DE R�GLES : 3 cartes horizontales
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

            var barBg = CreerObjetUI("BarBg", container.transform);
            var barBgRect = barBg.GetComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0f, 0.5f);
            barBgRect.anchorMax = new Vector2(1f, 0.5f);
            barBgRect.pivot = new Vector2(0.5f, 0.5f);
            barBgRect.sizeDelta = new Vector2(-24f, RuleBarHeight - 8f);
            barBgRect.anchoredPosition = Vector2.zero;
            var barBgImg = barBg.AddComponent<Image>();
            barBgImg.sprite = GetCarteSprite();
            barBgImg.type = Image.Type.Simple;
            barBgImg.color = new Color(1f, 1f, 1f, 0.98f);
            barBgImg.raycastTarget = false;
            barBg.transform.SetAsFirstSibling();
            var barShadow = CreerObjetUI("BarShadow", barBg.transform);
            var barShRect = barShadow.GetComponent<RectTransform>();
            barShRect.anchorMin = Vector2.zero;
            barShRect.anchorMax = Vector2.one;
            barShRect.offsetMin = new Vector2(4f, -6f);
            barShRect.offsetMax = new Vector2(4f, -6f);
            var barShImg = barShadow.AddComponent<Image>();
            barShImg.sprite = GetCarteSprite();
            barShImg.color = new Color(0f, 0f, 0f, 0.10f);
            barShImg.raycastTarget = false;
            barShadow.transform.SetAsFirstSibling();
            var barIgnore = barBg.AddComponent<LayoutElement>();
            barIgnore.ignoreLayout = true;

            var hlg = container.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(8, 8, 8, 8);
            hlg.spacing = 6f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            CreerCarteRegle(container.transform, "\u25CB", "1 par couleur", 0);
            CreerCarteRegle(container.transform, "\u25A1", "1 par ligne\net colonne", 1);
            CreerCarteRegleIcone(container.transform, GetDiagonalArrowSprite(), "Ne peut pas\nse toucher", 2);
        }

        private void CreerCarteRegle(Transform parent, string icone, string label, int index)
        {
            var carte = CreerObjetUI($"Carte{index}", parent);
            var carteLE = carte.AddComponent<LayoutElement>();
            carteLE.flexibleWidth = 1f;
            carteLE.preferredWidth = 220f;
            carteLE.minWidth = 150f;
            var carteRect = carte.GetComponent<RectTransform>();
            carteRect.sizeDelta = new Vector2(0f, RuleBarHeight - 20f);

            var carteImg = carte.AddComponent<Image>();
            carteImg.sprite = GetCarteSprite();
            carteImg.type = Image.Type.Simple;
            carteImg.color = GetRuleCardBg(index);
            carteImg.raycastTarget = false;

            // Liser� d'accent en haut de carte (finition, distingue chaque r�gle).
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

            // Ic�ne
            var iconObj = CreerObjetUI("Icone", carte.transform);
            var iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0f);
            iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(56f, 0f);
            iconRect.anchoredPosition = new Vector2(46f, 0f);

            var iconText = iconObj.AddComponent<TextMeshProUGUI>();
            iconText.font = _fontTitle;
            iconText.text = icone;
            iconText.fontSize = 42;
            iconText.alignment = TextAlignmentOptions.Center;
            iconText.color = GetRuleAccent(index);
            iconText.fontStyle = FontStyles.Bold;
            iconText.raycastTarget = false;

            var labelObj = CreerObjetUI("Label", carte.transform);
            var labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(88f, 10f);
            labelRect.offsetMax = new Vector2(-10f, -10f);

            var labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.font = _fontTitle;
            labelText.text = label;
            labelText.fontSize = 28;
            labelText.fontSizeMin = 18;
            labelText.fontSizeMax = 28;
            labelText.enableAutoSizing = true;
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            labelText.color = ScoreValueColor;
            labelText.fontStyle = FontStyles.Bold;
            labelText.lineSpacing = 2f;
            labelText.textWrappingMode = TextWrappingModes.Normal;
            labelText.overflowMode = TextOverflowModes.Ellipsis;
            labelText.raycastTarget = false;

            var cardLE = carte.AddComponent<LayoutElement>();
            cardLE.flexibleWidth = 1f;
            cardLE.preferredHeight = cardHeight;
            cardLE.minWidth = 180f;
        }

        // ------------------------------------------------------------------
        // 3bis) POWER-UP � GOMME � : bouton flottant, coin bas-droit.
        // ------------------------------------------------------------------

        private float BottomInset
        {
            get
            {
                Rect safe = Screen.safeArea;
                float insetPx = safe.yMin;
                if (insetPx <= 1f) return 18f;
                return insetPx * (1920f / Mathf.Max(Screen.height, 1));
            }
        }

        private void BuildGommeBouton(Canvas canvas)
        {
            float bottomMargin = 26f + BottomInset + 148f;
            float rightMargin = 16f;
            float size = 76f;

            var btnObj = CreerObjetUI("GommeBouton", canvas.transform);
            var btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(1f, 0f);
            btnRect.anchorMax = new Vector2(1f, 0f);
            btnRect.pivot = new Vector2(1f, 0f);
            btnRect.sizeDelta = new Vector2(size, size);
            btnRect.anchoredPosition = new Vector2(-rightMargin, bottomMargin);

            _gommeButtonBg = btnObj.AddComponent<Image>();
            _gommeButtonBg.sprite = KenneyUI.Button("Red") ?? GetPiluleSprite();
            _gommeButtonBg.type = Image.Type.Simple;
            _gommeButtonBg.color = GumBgColor;
            _gommeButtonBg.raycastTarget = true;

            AjouterOmbre(btnRect, canvas.transform, 3f, -5f);

            _gommeButton = btnObj.AddComponent<Button>();
            _gommeButton.targetGraphic = _gommeButtonBg;
            var colors = _gommeButton.colors;
            colors.pressedColor = new Color(0.80f, 0.62f, 0.66f, 1f);
            _gommeButton.colors = colors;
            _gommeButton.onClick.AddListener(() => OnGommeDemande?.Invoke());

            // Ic�ne gemme rouge (gomme corrective).
            Sprite gemSprite = Resources.Load<Sprite>("UI/gemRed");
            if (gemSprite != null)
            {
                var iconObj = CreerObjetUI("Icone", btnObj.transform);
                var iconRect = iconObj.GetComponent<RectTransform>();
                iconRect.anchorMin = Vector2.zero;
                iconRect.anchorMax = Vector2.one;
                iconRect.offsetMin = Vector2.zero;
                iconRect.offsetMax = Vector2.zero;
                var iconImg = iconObj.AddComponent<Image>();
                iconImg.sprite = gemSprite;
                iconImg.type = Image.Type.Simple;
                iconImg.preserveAspect = true;
                iconImg.color = Color.white;
                iconImg.raycastTarget = false;
            }

            var badgeGO = CreerObjetUI("BadgeCout", btnObj.transform);
            var badgeRect = badgeGO.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(1f, 1f);
            badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            badgeRect.sizeDelta = new Vector2(56f, 30f);
            badgeRect.anchoredPosition = new Vector2(8f, 10f);
            var badgeImg = badgeGO.AddComponent<Image>();
            badgeImg.sprite = GetPiluleSprite();
            badgeImg.type = Image.Type.Simple;
            badgeImg.color = new Color(1f, 1f, 1f, 0.97f);
            badgeImg.raycastTarget = false;
            var badgeShadow = CreerObjetUI("Shadow", badgeGO.transform);
            var bsRect2 = badgeShadow.GetComponent<RectTransform>();
            bsRect2.anchorMin = Vector2.zero;
            bsRect2.anchorMax = Vector2.one;
            bsRect2.offsetMin = new Vector2(2f, -3f);
            bsRect2.offsetMax = new Vector2(2f, -3f);
            var bsImg2 = badgeShadow.AddComponent<Image>();
            bsImg2.sprite = GetPiluleSprite();
            bsImg2.color = new Color(0f, 0f, 0f, 0.18f);
            bsImg2.raycastTarget = false;
            badgeShadow.transform.SetAsFirstSibling();

            var badgeTxtGO = CreerObjetUI("Text", badgeGO.transform);
            var badgeTxtRect = badgeTxtGO.GetComponent<RectTransform>();
            badgeTxtRect.anchorMin = Vector2.zero;
            badgeTxtRect.anchorMax = Vector2.one;
            badgeTxtRect.offsetMin = Vector2.zero;
            badgeTxtRect.offsetMax = Vector2.zero;
            var badgeTxt = badgeTxtGO.AddComponent<TextMeshProUGUI>();
            badgeTxt.font = _fontTitle;
            badgeTxt.text = PuzzleGameController.GommeCout.ToString();
            badgeTxt.fontSize = 20;
            badgeTxt.alignment = TextAlignmentOptions.Center;
            badgeTxt.color = GumBgColor;
            badgeTxt.fontStyle = FontStyles.Bold;
            badgeTxt.raycastTarget = false;
        }

        private void BuildFooterWave(Canvas canvas)
        {
            var footer = CreerObjetUI("FooterWave", canvas.transform);
            var rect = footer.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(0f, 148f);
            rect.anchoredPosition = Vector2.zero;
            var img = footer.AddComponent<Image>();
            img.sprite = GetCarteSprite();
            img.type = Image.Type.Simple;
            img.color = new Color(0.63f, 0.85f, 0.94f, 1f);
            img.raycastTarget = false;

            // CatSilhouette supprim� - re-ancrage �vite ic�ne flottante au bord du banner (task 3)

            var labelGO = CreerObjetUI("FooterLabel", footer.transform);
            var labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            labelRect.anchorMax = new Vector2(1f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = new Vector2(0f, 60f);
            labelRect.anchoredPosition = new Vector2(0f, -6f);
            var label = labelGO.AddComponent<TextMeshProUGUI>();
            label.font = _fontTitle;
            label.text = "Place les animaux";
            label.fontSize = 38;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.fontStyle = FontStyles.Bold;
            label.outlineWidth = 0.28f;
            label.outlineColor = new Color(0.20f, 0.55f, 0.80f, 1f);
            label.raycastTarget = false;
        }

        // ------------------------------------------------------------------
        // 4) C�URS : animations.
        // ------------------------------------------------------------------

        public void SetScore(int score)
        {
            _score = score;
        }

        public void SetProgression(int placed, int total)
        {
            _progressionTotal = Mathf.Max(1, total);
            if (_progressionText != null)
                _progressionText.text = $"<color=#22C55E>{placed}</color><color=#4A2C12>/{_progressionTotal}</color>";
            if (_progressionIconImage != null && placed > 0)
                Punch.Scale(this, _progressionIconImage.rectTransform, 1.18f, 0.22f);
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
                    // C�ur vivant : blanc (couleur d'origine du sprite).
                    StopHeartAnim(i);
                    _heartImages[i].color = HeartFullColor;
                    _heartRoots[i].transform.localScale = Vector3.one;
                }
                else
                {
                    // C�ur perdu : animation de disparition (scale down + fade out).
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
        /// D�cr�mente le compteur d'indices de 1. Retourne true si un indice �tait disponible.
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
        /// Met � jour l'�tat visuel du bouton indice. Avec indices gratuits restants
        /// il libelle le nombre restant ; � 0 il passe en � achat � : libell� du co�t
        /// en pi�ces + pi�cette d'achat. Le bouton reste cliquable tant que les
        /// interactions ne sont pas bloqu�es.
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
        /// Rafra�chit l'affichage du solde de pi�ces (appel� apr�s gain ou d�pense).
        /// </summary>
        public void RefreshCoins()
        {
            if (_coinsValueText != null)
                _coinsValueText.text = CurrencyManager.GetCoins().ToString();
        }

        /// <summary>
        /// Court retour visuel quand le joueur n'a pas assez de pi�ces pour acheter un indice.
        /// La pilule indice rougit bri�vement et un petit toast affiche le manque.
        /// </summary>
        public void NotifierPiecesInsuffisantes(int cout)
        {
            if (_indiceButtonBg != null)
            {
                Color original = _indiceButtonBg.color;
                _indiceButtonBg.color = CoinInsufficientColor;
                StartCoroutine(RestoreIndiceBgRoutine(original));
            }

            if (_coinsIconImage != null)
                Punch.FlashAlpha(this, _coinsIconImage, 0.3f, 0.4f);

            ShowCoinToast($"Pas assez de pi�ces ({cout})");
            Haptics.VibrateLight();
        }

        /// <summary>
        /// �v�nement : la gomme a �t� demand�e alors qu'aucun pion n'est en conflit
        /// (pas de cible). Simple retour informatif, sans co�t.
        /// </summary>
        public void NotifierAucuneCible()
        {
            ShowCoinToast("Aucun conflit � retirer");
            Haptics.VibrateLight();
        }

        public void NotifierViesEpuisees()
        {
            ShowCoinToast("Plus de vies ! Patiente ou regarde une pub");
            Haptics.VibrateLight();
        }

        /// <summary>
        /// D�sactive bri�vement le bouton gomme apr�s usage (�vite les doubles-clics
        /// qui paieraient deux fois), puis le r�arme.
        /// </summary>
        public void BloquerPowerUpTemporairement(float duree)
        {
            if (_gommeButton == null)
                return;

            _gommeButton.interactable = false;
            if (_gommeButtonBg != null)
                _gommeButtonBg.color = IndiceDisabledColor;

            if (_gommeRechargeRoutine != null)
                StopCoroutine(_gommeRechargeRoutine);
            _gommeRechargeRoutine = StartCoroutine(RechargeGommeRoutine(duree));
        }

        private IEnumerator RechargeGommeRoutine(float duree)
        {
            yield return new WaitForSecondsRealtime(duree);

            if (_gommeButton != null && !_interactionsBloquees)
                _gommeButton.interactable = true;
            if (_gommeButtonBg != null)
                _gommeButtonBg.color = GumBgColor;

            _gommeRechargeRoutine = null;
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
        /// Affiche un toast temporaire centr� (fond sombre arrondi + texte), puis le retire.
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
        // 6) PANNEAU DE D�FAITE (superpos�, cach� par d�faut)
        // ------------------------------------------------------------------

        private void BuildDefaitePanel(Canvas canvas)
        {
            _gameOverRoot = CreerObjetUI("GameOverRoot", canvas.transform);
            var rootRect = _gameOverRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            _overlay = CreerObjetUI("Overlay", _gameOverRoot.transform);
            var overlayRect = _overlay.GetComponent<RectTransform>();
            CreerRemplissage(overlayRect, 0f, ancreHaut: false);
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            var overlayImg = _overlay.AddComponent<Image>();
            overlayImg.color = new Color(0.15f, 0.12f, 0.10f, 0.48f);
            overlayImg.raycastTarget = true;

            _defaitePanel = CreerObjetUI("DefaitePanel", _gameOverRoot.transform);
            var panelRect = _defaitePanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(700f, 620f);
            panelRect.anchoredPosition = Vector2.zero;

            var panelImg = _defaitePanel.AddComponent<Image>();
            panelImg.sprite = GetCarteSprite();
            panelImg.type = Image.Type.Simple;
            panelImg.color = new Color(1f, 0.98f, 0.96f, 1f);
            var panelShadow = _defaitePanel.AddComponent<Shadow>();
            panelShadow.effectColor = new Color(0.18f, 0.11f, 0.06f, 0.32f);
            panelShadow.effectDistance = new Vector2(0f, -10f);

            var vlg = _defaitePanel.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 18f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(28, 28, 28, 28);

            var fitter = _defaitePanel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Hibou mascotte � layout-driven, -12�
            Sprite owlSprite = Resources.Load<Sprite>("Art/Animals/owl");
            if (owlSprite != null)
            {
                var owlObj = CreerObjetUI("DefeatOwl", _defaitePanel.transform);
                var owlLE = owlObj.AddComponent<LayoutElement>();
                owlLE.preferredHeight = 110f;
                owlLE.flexibleWidth = 1f;
                var owlRect = owlObj.GetComponent<RectTransform>();
                owlRect.sizeDelta = new Vector2(110f, 110f);
                owlRect.localRotation = Quaternion.Euler(0f, 0f, -12f);

                _defeatOwl = owlObj.AddComponent<Image>();
                _defeatOwl.sprite = owlSprite;
                _defeatOwl.preserveAspect = true;
                _defeatOwl.color = new Color(1f, 1f, 1f, 0f);
                _defeatOwl.raycastTarget = false;
            }

            // Titre � Niveau �chou� � - jelly style, layout-driven
            var titreObj = CreerObjetUI("Titre", _defaitePanel.transform);
            var titreLE = titreObj.AddComponent<LayoutElement>();
            titreLE.preferredHeight = 62f;
            titreLE.flexibleWidth = 1f;
            var titreText = titreObj.AddComponent<TextMeshProUGUI>();
            titreText.font = _fontTitle;
            titreText.text = "Niveau �chou�";
            titreText.fontSize = 54;
            titreText.alignment = TextAlignmentOptions.Center;
            titreText.color = new Color(0.18f, 0.12f, 0.08f, 1f);
            titreText.fontStyle = FontStyles.Bold;
            titreText.raycastTarget = false;
            titreText.enableAutoSizing = false;
            var titreSh = titreObj.AddComponent<Shadow>();
            titreSh.effectColor = new Color(1f, 0.92f, 0.75f, 0.55f);
            titreSh.effectDistance = new Vector2(0f, -3f);

            // Sous-titre � Plus de vies ! �
            var sousObj = CreerObjetUI("SousTitre", _defaitePanel.transform);
            var sousLE = sousObj.AddComponent<LayoutElement>();
            sousLE.preferredHeight = 42f;
            sousLE.flexibleWidth = 1f;
            var sousText = sousObj.AddComponent<TextMeshProUGUI>();
            sousText.font = _fontTitle;
            sousText.text = "Plus de vies !";
            sousText.fontSize = 32;
            sousText.fontStyle = FontStyles.Bold;
            sousText.alignment = TextAlignmentOptions.Center;
            sousText.color = new Color(0.62f, 0.32f, 0.22f, 1f);
            sousText.raycastTarget = false;

            var timerGO = CreerObjetUI("TimerVies", _defaitePanel.transform);
            var timerLE = timerGO.AddComponent<LayoutElement>();
            timerLE.preferredHeight = 36f;
            timerLE.flexibleWidth = 1f;
            _livesTimerText = timerGO.AddComponent<TextMeshProUGUI>();
            _livesTimerText.font = _fontTitle;
            _livesTimerText.text = "";
            _livesTimerText.fontSize = 26;
            _livesTimerText.fontStyle = FontStyles.Bold;
            _livesTimerText.alignment = TextAlignmentOptions.Center;
            _livesTimerText.color = new Color(0.24f, 0.15f, 0.14f, 1f);
            _livesTimerText.raycastTarget = false;
            _livesTimerText.enableAutoSizing = false;

            var pubGO = CreerObjetUI("BtnPubVies", _defaitePanel.transform);
            var pubLE = pubGO.AddComponent<LayoutElement>();
            pubLE.preferredHeight = 62f;
            pubLE.flexibleWidth = 1f;
            var pubImg = pubGO.AddComponent<Image>();
            var pubNormal = JellyUI.ButtonYellow ?? GetCarteSprite();
            var pubHover = JellyUI.ButtonYellow ?? pubNormal;
            var pubPressed = JellyUI.ButtonRed ?? pubNormal;
            var pubDisabled = JellyUI.ButtonGrey ?? pubNormal;
            pubImg.sprite = pubNormal;
            pubImg.type = Image.Type.Sliced;
            pubImg.pixelsPerUnitMultiplier = 1f;
            var pubBtn = pubGO.AddComponent<Button>();
            JellyUI.ApplyJellyButton(pubBtn, pubImg, pubNormal, pubHover, pubPressed, pubDisabled);
            pubBtn.onClick.AddListener(() => OnPubViesDemande?.Invoke());
            var pubTxtGO = CreerObjetUI("Text", pubGO.transform);
            var pubTxtRect = pubTxtGO.GetComponent<RectTransform>();
            pubTxtRect.anchorMin = Vector2.zero;
            pubTxtRect.anchorMax = Vector2.one;
            pubTxtRect.offsetMin = new Vector2(12f, 6f);
            pubTxtRect.offsetMax = new Vector2(-12f, -6f);
            var pubTxt = pubTxtGO.AddComponent<TextMeshProUGUI>();
            pubTxt.font = _fontTitle;
            pubTxt.text = "Regarder une pub (+3 ?)";
            pubTxt.fontSize = 22;
            pubTxt.alignment = TextAlignmentOptions.Center;
            pubTxt.color = new Color(0.20f, 0.12f, 0.06f, 1f);
            pubTxt.fontStyle = FontStyles.Bold;
            pubTxt.raycastTarget = false;
            pubTxt.enableAutoSizing = true;
            pubTxt.fontSizeMin = 16;
            pubTxt.fontSizeMax = 22;

            var btnObj = CreerObjetUI("BtnReessayer", _defaitePanel.transform);
            var btnLE = btnObj.AddComponent<LayoutElement>();
            btnLE.preferredHeight = 72f;
            btnLE.flexibleWidth = 1f;
            var btnImg = btnObj.AddComponent<Image>();
            var btnNormal = JellyUI.ButtonGreen ?? GetCarteSprite();
            var btnHover = JellyUI.ButtonYellow ?? btnNormal;
            var btnPressed = JellyUI.ButtonRed ?? btnNormal;
            var btnDisabled = JellyUI.ButtonGrey ?? btnNormal;
            btnImg.sprite = btnNormal;
            btnImg.type = Image.Type.Sliced;
            btnImg.pixelsPerUnitMultiplier = 1f;
            var btnComp = btnObj.AddComponent<Button>();
            JellyUI.ApplyJellyButton(btnComp, btnImg, btnNormal, btnHover, btnPressed, btnDisabled);
            btnComp.onClick.AddListener(() => OnReessayer?.Invoke());

            var btnTextObj = CreerObjetUI("Texte", btnObj.transform);
            var btnTextRect = btnTextObj.GetComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = new Vector2(12f, 6f);
            btnTextRect.offsetMax = new Vector2(-12f, -6f);

            var btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnText.font = _fontTitle;
            btnText.text = "R�essayer";
            btnText.fontSize = 30;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.color = Color.white;
            btnText.fontStyle = FontStyles.Bold;
            btnText.raycastTarget = false;
            var btnShadow = btnTextObj.AddComponent<Shadow>();
            btnShadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
            btnShadow.effectDistance = new Vector2(0f, -2f);

            var menuGO = CreerObjetUI("BtnMenu", _defaitePanel.transform);
            var menuLE = menuGO.AddComponent<LayoutElement>();
            menuLE.preferredHeight = 58f;
            menuLE.flexibleWidth = 1f;
            var menuImg = menuGO.AddComponent<Image>();
            menuImg.sprite = JellyUI.SmallGrey ?? GetPiluleSprite();
            menuImg.type = Image.Type.Sliced;
            menuImg.pixelsPerUnitMultiplier = 1f;
            menuImg.color = Color.white;
            menuImg.raycastTarget = true;
            var menuBtn = menuGO.AddComponent<Button>();
            var homeIcon = Resources.Load<Sprite>("UI/Icons/home_pixi") ?? Resources.Load<Sprite>("UI/Icons/back");
            if (homeIcon == null) homeIcon = JellyUI.SmallGrey;
            JellyUI.ApplyJellyButton(menuBtn, menuImg, JellyUI.SmallGrey ?? GetPiluleSprite(), JellyUI.SmallYellow ?? GetPiluleSprite(), JellyUI.SmallRed ?? GetPiluleSprite(), JellyUI.SmallGrey ?? GetPiluleSprite());
            var menuContentGO = CreerObjetUI("Content", menuGO.transform);
            var menuContentRect = menuContentGO.GetComponent<RectTransform>();
            menuContentRect.anchorMin = Vector2.zero;
            menuContentRect.anchorMax = Vector2.one;
            menuContentRect.offsetMin = new Vector2(14f, 6f);
            menuContentRect.offsetMax = new Vector2(-14f, -6f);
            var menuHLG = menuContentGO.AddComponent<HorizontalLayoutGroup>();
            menuHLG.spacing = 10f;
            menuHLG.childAlignment = TextAnchor.MiddleCenter;
            menuHLG.childForceExpandWidth = false;
            menuHLG.childControlWidth = false;
            var menuIconGO = CreerObjetUI("Icon", menuContentGO.transform);
            var menuIconRect = menuIconGO.GetComponent<RectTransform>();
            menuIconRect.sizeDelta = new Vector2(26f, 26f);
            var menuIconLE = menuIconGO.AddComponent<LayoutElement>();
            menuIconLE.preferredWidth = 26f;
            menuIconLE.preferredHeight = 26f;
            var menuIconImg = menuIconGO.AddComponent<Image>();
            menuIconImg.sprite = homeIcon;
            menuIconImg.preserveAspect = true;
            menuIconImg.color = new Color(0.22f, 0.15f, 0.10f, 1f);
            menuIconImg.raycastTarget = false;
            var menuTxtGO = CreerObjetUI("Text", menuContentGO.transform);
            var menuTxt = menuTxtGO.AddComponent<TextMeshProUGUI>();
            menuTxt.font = _fontTitle;
            menuTxt.text = "Retour menu";
            menuTxt.fontSize = 20;
            menuTxt.alignment = TextAlignmentOptions.MidlineLeft;
            menuTxt.color = new Color(0.22f, 0.15f, 0.10f, 1f);
            menuTxt.fontStyle = FontStyles.Bold;
            menuTxt.raycastTarget = false;
            menuTxt.enableAutoSizing = false;
            menuBtn.onClick.AddListener(() =>
            {
                SFXManager.Instance.PlayMenuClose();
                PuzzleGameController.IsDailyPuzzle = false;
                UnityEngine.SceneManagement.SceneManager.LoadScene("LevelMap");
            });
            // Le root reste actif ; ce sont overlay et panel qui basculent.
            _overlay.SetActive(false);
            _defaitePanel.SetActive(false);
        }

        /// <summary>�v�nement invoqu� quand le bouton � R�essayer � est press�.</summary>
        public Action OnReessayer;

        /// <summary>�v�nement invoqu� quand le bouton indice est press�.</summary>
        public Action OnIndiceDemande;

        /// <summary>�v�nement invoqu� quand le bouton gomme est press�.</summary>
        public Action OnGommeDemande;

        public Action OnPubViesDemande;

        /// <summary>
        /// Construit le panneau de d�faite. � appeler APR�S que la grille a �t�
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

            if (_livesTimerRoutine != null) StopCoroutine(_livesTimerRoutine);
            _livesTimerRoutine = StartCoroutine(LivesTimerRoutine());
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

        private IEnumerator LivesTimerRoutine()
        {
            while (true)
            {
                int secs = LivesManager.GetSecondsUntilNextLife();
                if (_livesTimerText != null)
                {
                    if (secs <= 0) _livesTimerText.text = "Vies pleines !";
                    else _livesTimerText.text = $"Prochaine vie dans {secs / 60:00}:{secs % 60:00}";
                }
                if (LivesManager.GetStoredLives() >= LivesManager.MaxVies) yield break;
                yield return new WaitForSecondsRealtime(1f);
            }
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

            if (_livesTimerRoutine != null)
            {
                StopCoroutine(_livesTimerRoutine);
                _livesTimerRoutine = null;
            }
        }

        // ------------------------------------------------------------------
        // Blocage des interactions.
        // ------------------------------------------------------------------

        public void BloquerInteractions(bool bloquer)
        {
            _interactionsBloquees = bloquer;
            UpdateIndiceButtonState();

            if (_gommeButton != null)
                _gommeButton.interactable = !bloquer;
            if (_gommeButtonBg != null && !bloquer)
                _gommeButtonBg.color = GumBgColor;
        }

        public bool InteractionsBloquees => _interactionsBloquees;

        // ------------------------------------------------------------------
        // R�initialisation compl�te du HUD (appel� par le contr�leur).
        // ------------------------------------------------------------------

        public void Reinitialiser(int score, int vies, int indices)
        {
            _score = score;

            SetVies(vies);

            _indiceCount = indices;
            if (_indiceCountText != null)
                _indiceCountText.text = _indiceCount.ToString();

            SetProgression(0, _progressionTotal);

            CacherDefaite();
            BloquerInteractions(false);
            RefreshCoins();
            UpdateIndiceButtonState();
        }

        // ------------------------------------------------------------------
        // Utilitaires UI : cr�ation d'objets, sprites proc�duraux.
        // ------------------------------------------------------------------

        private static GameObject CreerObjetUI(string nom, Transform parent)
        {
            var go = new GameObject(nom, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>
        /// Ajoute une ombre port�e douce sous un �l�ment (pilule...). L'ombre est
        /// ins�r�e comme fr�re juste derri�re l'�l�ment, calqu�e sur sa position.
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
        /// Comme CreerBouton mais avec une ic�ne Image au lieu d'un texte.
        /// Utilis� pour le bouton r�glages (engrenage) dont le caract�re Unicode
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
        /// Bouton d'en-t�te carr�/arrondi avec fond (tuile pastel) + ic�ne image.
        /// Sym�trie visuelle avec le retour (contrairement � une ic�ne flottante nue).
        /// </summary>
        private Button CreerBoutonTuileImage(Transform parent, Sprite iconSprite, float iconSize,
            Vector2 anchoredPos, Vector2 anchor, Vector2 pivot, Color? tint = null)
        {
            var btnObj = CreerObjetUI("BtnTuile", parent);
            var btnRect = btnObj.GetComponent<RectTransform>();
            btnRect.anchorMin = anchor;
            btnRect.anchorMax = anchor;
            btnRect.pivot = pivot;
            btnRect.sizeDelta = new Vector2(68f, 68f);
            btnRect.anchoredPosition = anchoredPos;

            var btnImg = btnObj.AddComponent<Image>();
            var jellyNormal = JellyUI.SmallGreen ?? GetPiluleSprite();
            var jellyHover = JellyUI.SmallYellow ?? jellyNormal;
            var jellyPressed = JellyUI.SmallRed ?? jellyNormal;
            var jellyDisabled = JellyUI.SmallGrey ?? jellyNormal;
            btnImg.sprite = jellyNormal;
            btnImg.type = Image.Type.Sliced;
            btnImg.pixelsPerUnitMultiplier = 1f;
            btnImg.raycastTarget = true;

            var btn = btnObj.AddComponent<Button>();
            JellyUI.ApplyJellyButton(btn, btnImg, jellyNormal, jellyHover, jellyPressed, jellyDisabled);

            var iconObj = CreerObjetUI("Icone", btnObj.transform);
            var iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            iconRect.anchoredPosition = new Vector2(0f, 3f);

            var iconImage = iconObj.AddComponent<Image>();
            iconImage.sprite = iconSprite;
            iconImage.type = Image.Type.Simple;
            iconImage.preserveAspect = true;
            iconImage.color = tint ?? Color.white;
            iconImage.raycastTarget = false;

            return btn;
        }

        /// <summary>
        /// Variante texte du bouton tuile (utilis�e pour la fl�che de retour ?).
        /// M�me tuile pastel que CreerBoutonTuileImage pour garder la sym�trie.
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
        /// Variante de CreerCarteRegle avec un sprite proc�dural au lieu d'un
        /// caract�re texte pour l'ic�ne. Utilis� pour la carte "diagonale" dont
        /// le caract�re ? n'est pas rendu correctement par la police.
        /// </summary>
        /// <summary>Largeur d'une carte de r�gle, proportionnelle � la largeur du conteneur (robuste aux ratios).</summary>
        private static float CalcCardWidth(Transform parent)
        {
            if (parent is RectTransform prt && prt.rect.width > 0f)
            {
                // 3 cartes + 2 espaces de 20px, avec une marge de 20px de chaque c�t�.
                float usable = prt.rect.width - 2f * 20f - 2f * 20f;
                return Mathf.Clamp(usable / 3f, 220f, 360f);
            }
            return 320f;
        }

        private void CreerCarteRegleIcone(Transform parent, Sprite iconSprite, string label, int index)
        {
            float cardHeight = RuleBarHeight - 20f;
            var carte = CreerObjetUI($"Carte{index}", parent);
            var carteLE = carte.AddComponent<LayoutElement>();
            carteLE.flexibleWidth = 1f;
            carteLE.preferredWidth = 220f;
            carteLE.minWidth = 150f;
            var carteRect = carte.GetComponent<RectTransform>();
            carteRect.sizeDelta = new Vector2(0f, cardHeight);

            var carteImg = carte.AddComponent<Image>();
            carteImg.sprite = GetCarteSprite();
            carteImg.type = Image.Type.Simple;
            carteImg.color = GetRuleCardBg(index);
            carteImg.raycastTarget = false;

            // Liser� d'accent en haut de carte (finition, distingue chaque r�gle).
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

            // Ic�ne sprite (au lieu de texte)
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

            var labelObj = CreerObjetUI("Label", carte.transform);
            var labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(88f, 10f);
            labelRect.offsetMax = new Vector2(-10f, -10f);

            var labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.font = _fontTitle;
            labelText.text = label;
            labelText.fontSize = 28;
            labelText.fontSizeMin = 18;
            labelText.fontSizeMax = 28;
            labelText.enableAutoSizing = true;
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            labelText.color = ScoreValueColor;
            labelText.fontStyle = FontStyles.Bold;
            labelText.lineSpacing = 2f;
            labelText.textWrappingMode = TextWrappingModes.Normal;
            labelText.overflowMode = TextOverflowModes.Ellipsis;
            labelText.raycastTarget = false;

            var cardLE = carte.AddComponent<LayoutElement>();
            cardLE.flexibleWidth = 1f;
            cardLE.preferredHeight = cardHeight;
            cardLE.minWidth = 180f;
        }

        // ------------------------------------------------------------------
        // Sprites proc�duraux (coins arrondis).
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
        /// Engrenage simplifi� : cercle central + 8 dents rectangulaires
        /// r�parties autour, pour repr�senter un bouton "r�glages".
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
        /// Fl�che diagonale vers le bas-droite : ligne diagonale + pointe.
        /// Pour la r�gle "ne peut pas se toucher" (diagonale adjacente).
        /// </summary>
        /// <summary>
        /// Pictogramme "ne peut pas se toucher en diagonale" : mini-grille 2x2
        /// (4 cases) dont la paire diagonale est reli�e puis barr�e par un trait
        /// de prohibition. Beaucoup plus lisible qu'une simple fl�che diagonale.
        /// </summary>
        private static Sprite CreerSpriteDiagonaleInterdite(int resolution)
        {
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float n = resolution - 1f;
            float gap = resolution * 0.06f;       // espace entre cases
            float cell = (n / 2f) - gap * 0.5f;   // taille d'une case
            float stroke = resolution * 0.06f;    // �paisseur des traits

            // Centres des 4 cases (coin sup�rieur gauche de chaque case).
            float[] cellX = new float[] { gap, gap + cell + gap, gap, gap + cell + gap };
            float[] cellY = new float[] { gap, gap, gap + cell + gap, gap + cell + gap };

            // Paire diagonale � souligner : haut-gauche (0) et bas-droite (3).
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

                    // 2) Paire diagonale reli�e (haut-gauche -> bas-droite).
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
