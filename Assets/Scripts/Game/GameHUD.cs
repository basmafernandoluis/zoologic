using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zoodoku
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

        private const float HeaderHeight = 120f;
        private const float HeaderPadding = 20f;
        private const float RuleBarHeight = 100f;
        private const float ScoreAreaHeight = 85f;
        private const float HintAreaHeight = 95f;

        // ------------------------------------------------------------------
        // Couleurs.
        // ------------------------------------------------------------------

        private static readonly Color HeaderBgColor = new Color(1f, 1f, 1f, 0.92f);
        private static readonly Color RuleCardBg = new Color(0.95f, 0.95f, 0.97f, 1f);
        private static readonly Color PillColor = new Color(0.26f, 0.55f, 0.88f, 1f);
        private static readonly Color HeartFullColor = Color.white;
        private static readonly Color HeartEmptyColor = new Color(0.45f, 0.45f, 0.50f, 0.45f);
        private static readonly Color ScoreLabelColor = new Color(0.50f, 0.52f, 0.56f, 1f);
        private static readonly Color ScoreValueColor = new Color(0.13f, 0.13f, 0.15f, 1f);
        private static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color RetryButtonColor = new Color(0.26f, 0.55f, 0.88f, 1f);
        private static readonly Color HintCountColor = new Color(0.35f, 0.40f, 0.50f, 1f);
        private static readonly Color IconTextColor = new Color(0.45f, 0.50f, 0.55f, 1f);
        private static readonly Color CardShadowColor = new Color(0f, 0f, 0f, 0.06f);

        // ------------------------------------------------------------------
        // Champs privés.
        // ------------------------------------------------------------------

        private TMP_FontAsset _fontTitle;
        private TMP_FontAsset _fontBody;

        // Score
        private TextMeshProUGUI _scoreValueText;
        private int _score;

        // Cœurs (images, pas de texte)
        private readonly Image[] _heartImages = new Image[LivesManager.ViesDepart];
        private readonly GameObject[] _heartRoots = new GameObject[LivesManager.ViesDepart];
        private Coroutine[] _heartAnimRoutines = new Coroutine[LivesManager.ViesDepart];

        // Panneau défaite
        private GameObject _defaitePanel;
        private GameObject _gameOverRoot;
        private GameObject _overlay;

        // Indice
        private TextMeshProUGUI _indiceCountText;
        private int _indiceCount;
        private Image _indiceIconImage;
        private Coroutine _indiceBounceRoutine;

        // Interactions bloquées
        private bool _interactionsBloquees;

        // Indice button components for graying out
        private Button _indiceButton;
        private Image _indiceButtonBg;
        private static readonly Color IndiceDisabledColor = new Color(0.75f, 0.75f, 0.78f, 0.5f);

        /// <summary>Score actuel affiché.</summary>
        public int Score => _score;

        /// <summary>Nombre d'indices restants.</summary>
        public int IndiceCount => _indiceCount;

        /// <summary>Board offset Y pour centrer la grille dans l'espace disponible entre le HUD et la zone d'indices.</summary>
        public float BoardYOffset
        {
            get
            {
                // HUD bottom edge (header + rules + score area)
                float hudBottom = HeaderHeight + RuleBarHeight + ScoreAreaHeight;
                // Screen top of available area
                float screenTop = 1920f;
                // Hint area bottom edge
                float hintBottom = screenTop - HintAreaHeight;
                // Center of available space
                float availableCenter = (hudBottom + hintBottom) * 0.5f;
                // Offset from canvas center (960 = screen height / 2)
                return -(availableCenter - 960f);
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
            BuildBarreInfo(canvas);
            BuildCompteurIndice(canvas);
        }

        // ------------------------------------------------------------------
        // 1) EN-TÊTE : ← retour | pilule « Niveau X » | ⚙ réglages
        // ------------------------------------------------------------------

        private void BuildHeader(Canvas canvas, int numeroNiveau)
        {
            var header = CreerObjetUI("Header", canvas.transform);
            var rect = header.GetComponent<RectTransform>();
            CreerRemplissage(rect, HeaderHeight, ancreHaut: true);

            var image = header.AddComponent<Image>();
            image.color = HeaderBgColor;
            image.raycastTarget = true;

            // Séparateur fin en bas
            var separateur = CreerObjetUI("Separateur", header.transform);
            var sepRect = separateur.GetComponent<RectTransform>();
            sepRect.anchorMin = new Vector2(0f, 0f);
            sepRect.anchorMax = new Vector2(1f, 0f);
            sepRect.pivot = new Vector2(0.5f, 1f);
            sepRect.sizeDelta = new Vector2(0f, 2f);
            sepRect.anchoredPosition = Vector2.zero;
            var sepImg = separateur.AddComponent<Image>();
            sepImg.color = new Color(0f, 0f, 0f, 0.08f);
            sepImg.raycastTarget = false;

            // Bouton retour (flèche ←)
            var btnRetour = CreerBouton(header.transform, "\u2190", 40f, new Vector2(HeaderPadding, 0f),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f));
            btnRetour.onClick.AddListener(() =>
            {
                SFXManager.Instance.PlayMenuClose();
                UnityEngine.SceneManagement.SceneManager.LoadScene("LevelMap");
            });

            // Pilule « Niveau X »
            CreerPiluleNiveau(header.transform, numeroNiveau);

            // Bouton réglages (roue engrenage — sprite procédural)
            var btnReglages = CreerBoutonImage(header.transform, GetSettingsSprite(), 36f,
                new Vector2(-HeaderPadding, 0f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f));
            btnReglages.onClick.AddListener(() =>
            {
                SFXManager.Instance.PlayMenuOpen();
                SettingsPanel.Open();
            });
        }

        private void CreerPiluleNiveau(Transform parent, int numero)
        {
            var pilule = CreerObjetUI("NiveauPilule", parent);
            var piluleRect = pilule.GetComponent<RectTransform>();
            piluleRect.anchorMin = new Vector2(0.5f, 0.5f);
            piluleRect.anchorMax = new Vector2(0.5f, 0.5f);
            piluleRect.pivot = new Vector2(0.5f, 0.5f);
            piluleRect.sizeDelta = new Vector2(200f, 50f);
            piluleRect.anchoredPosition = Vector2.zero;

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
            text.fontSize = 28;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
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

            // Sous l'en-tête
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, RuleBarHeight);
            rect.anchoredPosition = new Vector2(0f, -HeaderHeight);

            CreerCarteRegle(container.transform, "\u25CB", "1 par couleur", 0);
            CreerCarteRegle(container.transform, "\u25A1", "1 par ligne\net colonne", 1);
            CreerCarteRegleIcone(container.transform, GetDiagonalArrowSprite(), "Ne peut pas\nse toucher", 2);
        }

        private void CreerCarteRegle(Transform parent, string icone, string label, int index)
        {
            float cardWidth = 320f;
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
            carteImg.color = RuleCardBg;
            carteImg.raycastTarget = false;

            // Ombre sous la carte
            var ombre = CreerObjetUI("Ombre", carte.transform);
            var ombreRect = ombre.GetComponent<RectTransform>();
            ombreRect.anchorMin = Vector2.zero;
            ombreRect.anchorMax = Vector2.one;
            ombreRect.offsetMin = new Vector2(2f, -3f);
            ombreRect.offsetMax = new Vector2(2f, -3f);
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
            iconRect.sizeDelta = new Vector2(50f, 0f);
            iconRect.anchoredPosition = new Vector2(38f, 0f);

            var iconText = iconObj.AddComponent<TextMeshProUGUI>();
            iconText.font = _fontBody;
            iconText.text = icone;
            iconText.fontSize = 32;
            iconText.alignment = TextAlignmentOptions.Center;
            iconText.color = IconTextColor;
            iconText.raycastTarget = false;

            // Label
            var labelObj = CreerObjetUI("Label", carte.transform);
            var labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(72f, 8f);
            labelRect.offsetMax = new Vector2(-8f, -8f);

            var labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.font = _fontBody;
            labelText.text = label;
            labelText.fontSize = 22;
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            labelText.color = ScoreValueColor;
            labelText.lineSpacing = 0f;
            labelText.raycastTarget = false;
        }

        // ------------------------------------------------------------------
        // 3) SCORE + CŒURS : éléments directs sur le canvas.
        //    Score à gauche, cœurs à droite, même ligne.
        // ------------------------------------------------------------------

        private void BuildBarreInfo(Canvas canvas)
        {
            float yOffset = HeaderHeight + RuleBarHeight + 10f;

            // --- Score : label au-dessus, valeur en dessous, ancré en haut-gauche ---
            var scoreLabel = CreerObjetUI("ScoreLabel", canvas.transform);
            var slRect = scoreLabel.GetComponent<RectTransform>();
            slRect.anchorMin = new Vector2(0f, 1f);
            slRect.anchorMax = new Vector2(0f, 1f);
            slRect.pivot = new Vector2(0f, 1f);
            slRect.sizeDelta = new Vector2(200f, 30f);
            slRect.anchoredPosition = new Vector2(35f, -yOffset);

            var slText = scoreLabel.AddComponent<TextMeshProUGUI>();
            slText.font = _fontBody;
            slText.text = "Score";
            slText.fontSize = 22;
            slText.alignment = TextAlignmentOptions.TopLeft;
            slText.color = ScoreLabelColor;
            slText.raycastTarget = false;

            var scoreValue = CreerObjetUI("ScoreValue", canvas.transform);
            var svRect = scoreValue.GetComponent<RectTransform>();
            svRect.anchorMin = new Vector2(0f, 1f);
            svRect.anchorMax = new Vector2(0f, 1f);
            svRect.pivot = new Vector2(0f, 1f);
            svRect.sizeDelta = new Vector2(200f, 55f);
            svRect.anchoredPosition = new Vector2(35f, -(yOffset + 30f));

            _scoreValueText = scoreValue.AddComponent<TextMeshProUGUI>();
            _scoreValueText.font = _fontTitle;
            _scoreValueText.text = _score.ToString();
            _scoreValueText.fontSize = 44;
            _scoreValueText.alignment = TextAlignmentOptions.MidlineLeft;
            _scoreValueText.color = ScoreValueColor;
            _scoreValueText.fontStyle = FontStyles.Bold;
            _scoreValueText.raycastTarget = false;

            // --- Cœurs : 3 images heart.png ancrées en haut-droite, espacées ---
            Sprite heartSprite = Resources.Load<Sprite>("UI/heart");
            float heartSize = 56f;
            float heartSpacing = 10f;
            float totalHeartsW = LivesManager.ViesDepart * heartSize + (LivesManager.ViesDepart - 1) * heartSpacing;
            float heartsStartX = -35f - totalHeartsW;

            for (int i = 0; i < LivesManager.ViesDepart; i++)
            {
                var heartObj = CreerObjetUI($"Coeur{i}", canvas.transform);
                var heartRect = heartObj.GetComponent<RectTransform>();
                heartRect.anchorMin = new Vector2(1f, 1f);
                heartRect.anchorMax = new Vector2(1f, 1f);
                heartRect.pivot = new Vector2(0f, 1f);
                heartRect.sizeDelta = new Vector2(heartSize, heartSize);
                heartRect.anchoredPosition = new Vector2(
                    heartsStartX + i * (heartSize + heartSpacing),
                    -yOffset);

                _heartImages[i] = heartObj.AddComponent<Image>();
                _heartImages[i].sprite = heartSprite;
                _heartImages[i].type = Image.Type.Simple;
                _heartImages[i].preserveAspect = true;
                _heartImages[i].color = HeartFullColor;
                _heartImages[i].raycastTarget = false;
                _heartRoots[i] = heartObj;
            }
        }

        public void SetScore(int score)
        {
            _score = score;
            if (_scoreValueText != null)
                _scoreValueText.text = _score.ToString();
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

        // ------------------------------------------------------------------
        // 5) COMPTEUR D'INDICES (en bas de l'écran)
        // ------------------------------------------------------------------

        private void BuildCompteurIndice(Canvas canvas)
        {
            var container = CreerObjetUI("IndiceContainer", canvas.transform);
            var rect = container.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(140f, 80f);
            rect.anchoredPosition = new Vector2(0f, 15f);

            _indiceButtonBg = container.AddComponent<Image>();
            _indiceButtonBg.sprite = GetPiluleSprite();
            _indiceButtonBg.type = Image.Type.Simple;
            _indiceButtonBg.color = new Color(1f, 1f, 1f, 0.85f);
            _indiceButtonBg.raycastTarget = true;

            _indiceButton = container.AddComponent<Button>();
            _indiceButton.targetGraphic = _indiceButtonBg;
            _indiceButton.onClick.AddListener(() => OnIndiceDemande?.Invoke());

            // Icône potion.png
            Sprite potionSprite = Resources.Load<Sprite>("UI/potion");
            var iconObj = CreerObjetUI("IndiceIcone", container.transform);
            var iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(44f, 44f);
            iconRect.anchoredPosition = new Vector2(-20f, 6f);

            _indiceIconImage = iconObj.AddComponent<Image>();
            _indiceIconImage.sprite = potionSprite;
            _indiceIconImage.type = Image.Type.Simple;
            _indiceIconImage.preserveAspect = true;
            _indiceIconImage.color = Color.white;
            _indiceIconImage.raycastTarget = false;

            // Nombre
            var countObj = CreerObjetUI("IndiceNombre", container.transform);
            var countRect = countObj.GetComponent<RectTransform>();
            countRect.anchorMin = new Vector2(0.5f, 0.5f);
            countRect.anchorMax = new Vector2(0.5f, 0.5f);
            countRect.pivot = new Vector2(0.5f, 0.5f);
            countRect.sizeDelta = new Vector2(40f, 40f);
            countRect.anchoredPosition = new Vector2(18f, 0f);

            _indiceCountText = countObj.AddComponent<TextMeshProUGUI>();
            _indiceCountText.font = _fontTitle;
            _indiceCountText.text = _indiceCount.ToString();
            _indiceCountText.fontSize = 30;
            _indiceCountText.alignment = TextAlignmentOptions.Center;
            _indiceCountText.color = HintCountColor;
            _indiceCountText.fontStyle = FontStyles.Bold;
            _indiceCountText.raycastTarget = false;

            UpdateIndiceButtonState();
            StartIndiceBounce();
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
        /// Met à jour l'état visuel du bouton indice : grisé si compteur à 0.
        /// </summary>
        private void UpdateIndiceButtonState()
        {
            bool enabled = _indiceCount > 0 && !_interactionsBloquees;

            if (_indiceButton != null)
                _indiceButton.interactable = enabled;

            if (_indiceIconImage != null)
                _indiceIconImage.color = enabled ? Color.white : IndiceDisabledColor;

            if (enabled)
                StartIndiceBounce();
            else
                StopIndiceBounce();
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

            // Panneau central
            _defaitePanel = CreerObjetUI("DefaitePanel", _gameOverRoot.transform);
            var panelRect = _defaitePanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(700f, 400f);
            panelRect.anchoredPosition = Vector2.zero;

            var panelImg = _defaitePanel.AddComponent<Image>();
            panelImg.sprite = GetCarteSprite();
            panelImg.type = Image.Type.Simple;
            panelImg.color = Color.white;

            // Titre « Niveau échoué »
            var titreObj = CreerObjetUI("Titre", _defaitePanel.transform);
            var titreRect = titreObj.GetComponent<RectTransform>();
            titreRect.anchorMin = new Vector2(0f, 0.65f);
            titreRect.anchorMax = new Vector2(1f, 0.90f);
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
            sousRect.anchorMin = new Vector2(0f, 0.48f);
            sousRect.anchorMax = new Vector2(1f, 0.65f);
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
            btnRect.anchorMin = new Vector2(0.5f, 0.10f);
            btnRect.anchorMax = new Vector2(0.5f, 0.10f);
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
        }

        public void CacherDefaite()
        {
            if (_overlay != null)
                _overlay.SetActive(false);
            if (_defaitePanel != null)
                _defaitePanel.SetActive(false);
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

        private static void CreerRemplissage(RectTransform rect, float hauteur, bool ancreHaut)
        {
            if (ancreHaut)
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
            iconImage.color = ScoreValueColor;
            iconImage.raycastTarget = false;

            return btn;
        }

        /// <summary>
        /// Variante de CreerCarteRegle avec un sprite procédural au lieu d'un
        /// caractère texte pour l'icône. Utilisé pour la carte "diagonale" dont
        /// le caractère ↘ n'est pas rendu correctement par la police.
        /// </summary>
        private void CreerCarteRegleIcone(Transform parent, Sprite iconSprite, string label, int index)
        {
            float cardWidth = 320f;
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
            carteImg.color = RuleCardBg;
            carteImg.raycastTarget = false;

            // Ombre sous la carte
            var ombre = CreerObjetUI("Ombre", carte.transform);
            var ombreRect = ombre.GetComponent<RectTransform>();
            ombreRect.anchorMin = Vector2.zero;
            ombreRect.anchorMax = Vector2.one;
            ombreRect.offsetMin = new Vector2(2f, -3f);
            ombreRect.offsetMax = new Vector2(2f, -3f);
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
            iconRect.sizeDelta = new Vector2(50f, 0f);
            iconRect.anchoredPosition = new Vector2(38f, 0f);

            var iconImage = iconObj.AddComponent<Image>();
            iconImage.sprite = iconSprite;
            iconImage.type = Image.Type.Simple;
            iconImage.preserveAspect = true;
            iconImage.color = IconTextColor;
            iconImage.raycastTarget = false;

            // Label
            var labelObj = CreerObjetUI("Label", carte.transform);
            var labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(72f, 8f);
            labelRect.offsetMax = new Vector2(-8f, -8f);

            var labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.font = _fontBody;
            labelText.text = label;
            labelText.fontSize = 22;
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
        private static Sprite _diagonalArrowSprite;

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
                _settingsSprite = CreerSpriteEngrenage(128);
            return _settingsSprite;
        }

        private static Sprite GetDiagonalArrowSprite()
        {
            if (_diagonalArrowSprite == null)
                _diagonalArrowSprite = CreerSpriteFlecheDiagonale(128);
            return _diagonalArrowSprite;
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
        private static Sprite CreerSpriteFlecheDiagonale(int resolution)
        {
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float center = (resolution - 1) * 0.5f;
            float margin = resolution * 0.18f;
            float lineThickness = resolution * 0.08f;
            float headSize = resolution * 0.22f;

            // Point de départ (haut-gauche) et point d'arrivée (bas-droite).
            float x0 = margin;
            float y0 = margin;
            float x1 = resolution - 1f - margin;
            float y1 = resolution - 1f - margin;
            float dx = x1 - x0;
            float dy = y1 - y0;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            float nx = -dy / length; // normale
            float ny = dx / length;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float px = x - x0;
                    float py = y - y0;
                    float proj = (px * dx + py * dy) / (length * length); // projection sur la ligne
                    float dist = Mathf.Abs(px * nx + py * ny);            // distance à la ligne

                    bool inside = false;

                    // Ligne principale (de 15 % à 85 % de la longueur).
                    if (proj >= 0.15f && proj <= 0.85f && dist <= lineThickness)
                        inside = true;

                    // Pointe de flèche (triangle à l'extrémité bas-droite).
                    if (proj > 0.70f)
                    {
                        float tipDist = Mathf.Sqrt(
                            (x - x1) * (x - x1) + (y - y1) * (y - y1));
                        if (tipDist <= headSize && proj >= 0.60f)
                            inside = true;
                    }

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
