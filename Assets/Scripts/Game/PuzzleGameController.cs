using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zoodoku.Core;

namespace Zoodoku
{
    /// <summary>
    /// Contrôleur principal du jeu jouable :
    ///  - génère un niveau via <see cref="LevelGenerator"/> au démarrage ;
    ///  - gère les taps (placer / retirer un pion) et les appuis longs (marquer un "X") ;
    ///  - signale les conflits en rouge via <see cref="GridView.FlashConflict"/> ;
    ///  - détecte la victoire via <see cref="RuleValidator.IsSolved"/> ;
    ///  - gère les vies (<see cref="LivesManager"/>), le score et le panneau de défaite
    ///    via <see cref="GameHUD"/>.
    ///
    /// Le câblage est entièrement automatique (aucune configuration dans l'Inspector) :
    /// ce composant crée lui-même le canvas, l'EventSystem, le HUD et le texte de
    /// victoire s'ils n'existent pas déjà.
    /// </summary>
    public sealed class PuzzleGameController : MonoBehaviour
    {
        [SerializeField] private int _numeroNiveau = 1;

        /// <summary>
        /// Numéro de niveau à charger. À définir AVANT de charger la scène de jeu.
        /// Par défaut 1 (mode test sans passer par la LevelMap).
        /// </summary>
        public static int SelectedLevel = 1;

        private PuzzleGrid _grid;
        private bool[,] _xMarks;
        private GridView _gridView;
        private bool _victoryShown;
        private Coroutine _victoryAnimation;

        private GameHUD _hud;
        private LivesManager _livesManager;
        private bool _partieTerminee;

        // Stars de victoire
        private readonly Image[] _victoryStars = new Image[3];
        private readonly GameObject[] _victoryStarRoots = new GameObject[3];

        // Victory panel elements
        private GameObject _victoryRoot;
        private GameObject _victoryPanel;
        private TextMeshProUGUI _victoryText;
        private Outline _victoryOutline;
        private Vector2 _victoryTextBasePosition;

        // Paramètres de l'animation de victoire.
        private const float VictoryAnimationDuration = 0.9f;
        private const float VictoryTextSlide = 30f;

        // Score : 100 de départ, -15 par conflit.
        private const int ScoreDepart = 100;
        private const int ScorePenaliteConflit = 15;

        // Compteur d'erreurs pour le calcul des étoiles.
        private int _conflictsThisLevel;

        // Fond d'écran : léger dégradé vertical (crème en haut → bleu doux en bas).
        private static readonly Color BackgroundTopColor = new Color(0.97f, 0.96f, 0.93f);
        private static readonly Color BackgroundBottomColor = new Color(0.84f, 0.91f, 0.97f);
        private static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.55f);

        private void Awake()
        {
            _gridView = GetComponent<GridView>();
            if (_gridView == null)
                _gridView = gameObject.AddComponent<GridView>();
        }

        private void Start()
        {
            _numeroNiveau = SelectedLevel;
            _conflictsThisLevel = 0;

            Canvas canvas = null;

            try
            {
                canvas = EnsureCanvas();
                EnsureEventSystem();
                CreateBackground(canvas);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Zoodoku] Start: échec de l'initialisation de l'environnement UI.\n" + e);
                return;
            }

            try
            {
                _hud = gameObject.AddComponent<GameHUD>();
                _hud.Build(canvas, _numeroNiveau);
                _hud.OnReessayer = ReinitialiserNiveau;
                _hud.OnIndiceDemande = DemanderIndice;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Zoodoku] Start: échec de la construction du HUD.\n" + e);
            }

            try
            {
                _grid = GenerateLevel();
                _xMarks = new bool[_grid.Size, _grid.Size];
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Zoodoku] Start: échec de la génération du niveau.\n" + e);
                return;
            }

            try
            {
                _gridView.OnCellTapped = HandleCellTapped;
                _gridView.OnCellLongPressed = HandleCellLongPressed;
                _gridView.Build(_grid, (RectTransform)canvas.transform);

                if (_gridView.BoardContainer != null)
                    _gridView.BoardContainer.anchoredPosition =
                        new Vector2(0f, _hud.BoardYOffset);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Zoodoku] Start: échec de la construction de la grille.\n" + e);
            }

            try
            {
                _hud.CreerPanneauDefaite(canvas);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Zoodoku] Start: échec de la création du panneau de défaite.\n" + e);
            }

            try
            {
                CreateVictoryPanel(canvas);
                if (_victoryRoot != null)
                    _victoryRoot.SetActive(false);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Zoodoku] Start: échec de la création du panneau de victoire.\n" + e);
            }

            try
            {
                _livesManager = new LivesManager();
                _livesManager.OnPartiePerdue = GererPartiePerdue;
                _hud.SetVies(_livesManager.Vies);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Zoodoku] Start: échec de l'initialisation du gestionnaire de vies.\n" + e);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (SettingsPanel.HandleBackButton()) return;
                ShowQuitConfirmation();
            }
        }

        private void ShowQuitConfirmation()
        {
            if (_partieTerminee) return;

            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            var confirmGO = new GameObject("QuitDialog");
            confirmGO.transform.SetParent(canvas.transform, false);
            var confirmRect = confirmGO.AddComponent<RectTransform>();
            confirmRect.anchorMin = Vector2.zero;
            confirmRect.anchorMax = Vector2.one;
            confirmRect.offsetMin = Vector2.zero;
            confirmRect.offsetMax = Vector2.zero;

            var confirmBg = confirmGO.AddComponent<Image>();
            confirmBg.color = OverlayColor;
            confirmBg.raycastTarget = true;

            var panel = new GameObject("Panel");
            panel.transform.SetParent(confirmGO.transform, false);
            var cpRect = panel.AddComponent<RectTransform>();
            cpRect.anchorMin = new Vector2(0.5f, 0.5f);
            cpRect.anchorMax = new Vector2(0.5f, 0.5f);
            cpRect.pivot = new Vector2(0.5f, 0.5f);
            cpRect.sizeDelta = new Vector2(700f, 300f);
            cpRect.anchoredPosition = Vector2.zero;
            var cpImg = panel.AddComponent<Image>();
            cpImg.color = new Color(0.18f, 0.14f, 0.26f);
            cpImg.raycastTarget = true;

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 24f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(40, 40, 30, 30);

            var msgGO = new GameObject("Message");
            msgGO.transform.SetParent(panel.transform, false);
            var msgTxt = msgGO.AddComponent<TextMeshProUGUI>();
            msgTxt.font = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Regular SDF");
            msgTxt.text = "Quitter le niveau ?";
            msgTxt.fontSize = 30;
            msgTxt.color = Color.white;
            msgTxt.alignment = TextAlignmentOptions.Center;
            msgTxt.raycastTarget = false;
            var msgLE = msgGO.AddComponent<LayoutElement>();
            msgLE.preferredHeight = 60f;

            var btnRow = new GameObject("ButtonRow");
            btnRow.transform.SetParent(panel.transform, false);
            btnRow.AddComponent<RectTransform>();
            var btnHLG = btnRow.AddComponent<HorizontalLayoutGroup>();
            btnHLG.spacing = 30f;
            btnHLG.childAlignment = TextAnchor.MiddleCenter;
            btnHLG.childForceExpandWidth = false;
            btnHLG.childForceExpandHeight = false;
            btnRow.AddComponent<LayoutElement>().preferredHeight = 60f;

            var fontBody = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Regular SDF");
            Color dangerRed = new Color(0.85f, 0.30f, 0.30f);
            Color accentBlue = new Color(0.26f, 0.55f, 0.88f);

            var btnOui = CreerBoutonSimple(btnRow.transform, "Oui", dangerRed, fontBody, 26f);
            btnOui.onClick.AddListener(() =>
            {
                SFXManager.Instance.PlayMenuClose();
                UnityEngine.SceneManagement.SceneManager.LoadScene("LevelMap");
            });

            var btnAnnuler = CreerBoutonSimple(btnRow.transform, "Annuler", accentBlue, fontBody, 26f);
            btnAnnuler.onClick.AddListener(() =>
            {
                SFXManager.Instance.PlayMenuClose();
                Destroy(confirmGO);
            });
        }

        private static Button CreerBoutonSimple(Transform parent, string label, Color bgColor, TMP_FontAsset font, float fontSize)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(250f, 55f);

            var img = go.AddComponent<Image>();
            img.color = bgColor;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor = bgColor;
            colors.highlightedColor = bgColor * 1.15f;
            colors.pressedColor = bgColor * 0.8f;
            btn.colors = colors;

            var txtGO = new GameObject("Text");
            txtGO.transform.SetParent(go.transform, false);
            var txtRect = txtGO.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;
            var txt = txtGO.AddComponent<TextMeshProUGUI>();
            txt.font = font;
            txt.text = label;
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyles.Bold;
            txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.Center;
            txt.raycastTarget = false;

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 250f;
            le.preferredHeight = 55f;

            return btn;
        }

        // ------------------------------------------------------------------
        // Génération du niveau.
        // ------------------------------------------------------------------

        private PuzzleGrid GenerateLevel()
        {
            var generator = new LevelGenerator();
            int size = Core.LevelConfig.GetGridSize(_numeroNiveau);
            int difficulty = Core.LevelConfig.GetTargetDifficulty(_numeroNiveau);

            try
            {
                PuzzleGrid grid = generator.GenerateLevel(size, difficulty);
                if (grid != null)
                    return grid;
            }
            catch (Exception)
            {
            }

            return generator.GenerateUniqueGrid(size);
        }

        // ------------------------------------------------------------------
        // Interactions.
        // ------------------------------------------------------------------

        private void HandleCellTapped(int row, int col)
        {
            if (_partieTerminee)
                return;

            if (_grid.HasPion(row, col))
            {
                _grid.RemovePion(row, col);
                _gridView.SetPion(row, col, false);

                SFXManager.Instance.PlayClickedOut();

                ReevaluerConflits();
                UpdateVictoryVisibility();
                return;
            }

            _grid.PlacePion(row, col);
            _xMarks[row, col] = false;
            _gridView.SetPion(row, col, true);
            _gridView.SetX(row, col, false);

            VerifierConflitPlacement(row, col);
            FlashAllConflicts();
            UpdateVictoryVisibility();
        }

        private void HandleCellLongPressed(int row, int col)
        {
            if (_partieTerminee)
                return;

            if (_grid.HasPion(row, col))
                return;

            bool hasX = _xMarks[row, col];
            _xMarks[row, col] = !hasX;
            _gridView.SetX(row, col, !hasX);

            SFXManager.Instance.PlayDialogueBlip();
        }

        private void DemanderIndice()
        {
            if (_partieTerminee)
                return;

            if (!_hud.DecrementIndice())
                return;

            SFXManager.Instance.PlayUnlock();
            _gridView.RequestHint();
        }

        // ------------------------------------------------------------------
        // Règles : conflits, score, victoire.
        // ------------------------------------------------------------------

        private void VerifierConflitPlacement(int row, int col)
        {
            List<ConflictType> conflits = RuleValidator.GetConflicts(_grid, row, col);

            if (conflits.Count == 0)
            {
                SFXManager.Instance.PlayConfirm();
                return;
            }

            SFXManager.Instance.PlayFailure();

            _conflictsThisLevel++;
            _livesManager.PerdreVie();
            _hud.SetVies(_livesManager.Vies);

            int nouveauScore = Mathf.Max(0, _hud.Score - ScorePenaliteConflit);
            _hud.SetScore(nouveauScore);
        }

        private void ReevaluerConflits()
        {
            var pions = new List<(int row, int col)>(_grid.Pions);
            int nbConflits = CompterPionsEnConflit(pions);

            int penalite = nbConflits * ScorePenaliteConflit;
            int nouveauScore = Mathf.Max(0, ScoreDepart - penalite);
            _hud.SetScore(nouveauScore);
        }

        private int CompterPionsEnConflit(List<(int row, int col)> pions)
        {
            var conflits = new HashSet<(int row, int col)>();

            for (int i = 0; i < pions.Count; i++)
            {
                for (int j = i + 1; j < pions.Count; j++)
                {
                    if (SontEnConflit(pions[i], pions[j]))
                    {
                        conflits.Add(pions[i]);
                        conflits.Add(pions[j]);
                    }
                }
            }

            return conflits.Count;
        }

        private void FlashAllConflicts()
        {
            var pions = new List<(int row, int col)>(_grid.Pions);
            var conflits = new HashSet<(int row, int col)>();

            for (int i = 0; i < pions.Count; i++)
            {
                for (int j = i + 1; j < pions.Count; j++)
                {
                    if (SontEnConflit(pions[i], pions[j]))
                    {
                        conflits.Add(pions[i]);
                        conflits.Add(pions[j]);
                    }
                }
            }

            foreach ((int row, int col) in conflits)
                _gridView.FlashConflict(row, col);

            if (conflits.Count > 0)
                Haptics.VibrateLight();
        }

        private bool SontEnConflit((int row, int col) a, (int row, int col) b)
        {
            return a.row == b.row
                || a.col == b.col
                || _grid.GetRegionId(a.row, a.col) == _grid.GetRegionId(b.row, b.col)
                || (Mathf.Abs(a.row - b.row) == 1 && Mathf.Abs(a.col - b.col) == 1);
        }

        // ------------------------------------------------------------------
        // Défaite / Réinitialisation.
        // ------------------------------------------------------------------

        private void GererPartiePerdue()
        {
            _partieTerminee = true;
            _hud.BloquerInteractions(true);
            _hud.AfficherDefaite();
        }

        private void ReinitialiserNiveau()
        {
            _partieTerminee = false;
            _conflictsThisLevel = 0;
            _hud.CacherDefaite();
            HideVictory();

            _livesManager.Reinitialiser();
            _hud.Reinitialiser(ScoreDepart, LivesManager.ViesDepart, _hud.IndiceCount);

            _grid.Clear();
            for (int row = 0; row < _grid.Size; row++)
            {
                for (int col = 0; col < _grid.Size; col++)
                {
                    _xMarks[row, col] = false;
                    _gridView.SetPion(row, col, false);
                    _gridView.SetX(row, col, false);
                }
            }

            _victoryShown = false;
        }

        // ------------------------------------------------------------------
        // Victoire.
        // ------------------------------------------------------------------

        private void UpdateVictoryVisibility()
        {
            bool solved = RuleValidator.IsSolved(_grid);
            if (solved == _victoryShown)
                return;

            _victoryShown = solved;
            if (solved)
                PlayVictory();
            else
                HideVictory();
        }

        private void PlayVictory()
        {
            SFXManager.Instance.PlaySuccess();

            int stars = _conflictsThisLevel == 0 ? 3
                : _conflictsThisLevel <= 2 ? 2
                : 1;

            LevelProgressManager.SetStars(_numeroNiveau, stars);
            LevelProgressManager.UnlockNextLevel(_numeroNiveau);

            if (_victoryAnimation != null)
                StopCoroutine(_victoryAnimation);
            _victoryAnimation = StartCoroutine(VictoryAnimationRoutine());
        }

        private void HideVictory()
        {
            if (_victoryAnimation != null)
            {
                StopCoroutine(_victoryAnimation);
                _victoryAnimation = null;
            }

            if (_victoryRoot != null)
                _victoryRoot.SetActive(false);

            if (_victoryText != null)
            {
                _victoryText.rectTransform.anchoredPosition = _victoryTextBasePosition;
                _victoryText.color = VictoryTextColor;
            }

            if (_victoryOutline != null)
                _victoryOutline.effectColor = new Color(0f, 0f, 0f, 0.8f);

            // Cacher les stars.
            for (int i = 0; i < 3; i++)
            {
                if (_victoryStarRoots[i] != null)
                    _victoryStarRoots[i].SetActive(false);
            }

            _gridView.ResetVictoryZoom();
        }

        // Couleur du texte de victoire (contraste sur fond blanc).
        private static readonly Color VictoryTextColor = new Color(0.15f, 0.15f, 0.18f, 1f);

        private IEnumerator VictoryAnimationRoutine()
        {
            _gridView.PlayVictoryZoom();
            Haptics.VibrateStrong();

            if (_victoryRoot != null)
                _victoryRoot.SetActive(true);

            // État initial : texte invisible, décalé vers le bas.
            _victoryText.rectTransform.anchoredPosition =
                _victoryTextBasePosition - new Vector2(0f, VictoryTextSlide);
            _victoryText.color = new Color(VictoryTextColor.r, VictoryTextColor.g, VictoryTextColor.b, 0f);
            if (_victoryOutline != null)
                _victoryOutline.effectColor = new Color(0f, 0f, 0f, 0f);

            // Fade-in + remontée de 30 pixels, avec décélération douce.
            float elapsed = 0f;

            while (elapsed < VictoryAnimationDuration)
            {
                float t = Mathf.Clamp01(elapsed / VictoryAnimationDuration);
                float eased = Easing.EaseOutCubic(t);

                _victoryText.rectTransform.anchoredPosition =
                    _victoryTextBasePosition - new Vector2(0f, VictoryTextSlide * (1f - eased));
                _victoryText.color = new Color(VictoryTextColor.r, VictoryTextColor.g, VictoryTextColor.b, eased);
                if (_victoryOutline != null)
                    _victoryOutline.effectColor = new Color(0f, 0f, 0f, 0.8f * eased);

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _victoryText.rectTransform.anchoredPosition = _victoryTextBasePosition;
            _victoryText.color = VictoryTextColor;
            if (_victoryOutline != null)
                _victoryOutline.effectColor = new Color(0f, 0f, 0f, 0.8f);

            // Apparition séquentielle des 3 étoiles (pop l'une après l'autre).
            for (int i = 0; i < 3; i++)
            {
                if (_victoryStars[i] == null || _victoryStarRoots[i] == null)
                    continue;
                yield return StartCoroutine(StarPopRoutine(i));
                yield return new WaitForSecondsRealtime(0.1f);
            }

            _victoryAnimation = null;
        }

        // ------------------------------------------------------------------
        // Stars de victoire.
        // ------------------------------------------------------------------

        private void CreateVictoryStars(Canvas canvas)
        {
            Sprite starSprite = Resources.Load<Sprite>("UI/star");
            float starSize = 64f;
            float starSpacing = 16f;
            float totalW = 3f * starSize + 2f * starSpacing;

            for (int i = 0; i < 3; i++)
            {
                var starObj = new GameObject($"Star{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                starObj.transform.SetParent(canvas.transform, false);

                var rect = (RectTransform)starObj.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(starSize, starSize);

                float xOffset = -totalW * 0.5f + starSize * 0.5f + i * (starSize + starSpacing);
                rect.anchoredPosition = _victoryTextBasePosition + new Vector2(xOffset, 55f);

                Image img = starObj.GetComponent<Image>();
                if (img == null) img = starObj.AddComponent<Image>();
                img.sprite = starSprite;
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
                img.color = Color.white;
                img.raycastTarget = false;

                _victoryStars[i] = img;
                _victoryStarRoots[i] = starObj;
                starObj.SetActive(false);
            }
        }

        private IEnumerator StarPopRoutine(int index)
        {
            GameObject root = _victoryStarRoots[index];
            Image img = _victoryStars[index];

            root.SetActive(true);
            root.transform.localScale = Vector3.zero;
            img.color = new Color(1f, 1f, 1f, 0f);

            float duration = 0.25f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = Easing.EaseOutBack(t);
                root.transform.localScale = new Vector3(scale, scale, scale);
                img.color = new Color(1f, 1f, 1f, t);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            root.transform.localScale = Vector3.one;
            img.color = Color.white;
        }

        // ------------------------------------------------------------------
        // Création de l'environnement UI (fallback si absent de la scène).
        // ------------------------------------------------------------------

        private static Canvas EnsureCanvas()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
                return canvas;

            var canvasGameObject = new GameObject(
                "UICanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            canvas = canvasGameObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGameObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private void CreateBackground(Canvas canvas)
        {
            var gameObject = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(canvas.transform, false);

            var rect = (RectTransform)gameObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = gameObject.GetComponent<Image>();
            image.sprite = CreateVerticalGradientSprite(BackgroundTopColor, BackgroundBottomColor);
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
        }

        private static Sprite CreateVerticalGradientSprite(Color top, Color bottom)
        {
            const int height = 64;
            var texture = new Texture2D(1, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < height; y++)
            {
                float t = y / (float)(height - 1);
                texture.SetPixel(0, y, Color.Lerp(bottom, top, t));
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 1f, height), new Vector2(0.5f, 0.5f));
        }

        // ------------------------------------------------------------------
        // Création du panneau de victoire (overlay plein écran + panneau centré).
        // ------------------------------------------------------------------

        private void CreateVictoryPanel(Canvas canvas)
        {
            // 1) Root : overlay plein écran (fond sombre semi-transparent)
            _victoryRoot = new GameObject("VictoryRoot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _victoryRoot.transform.SetParent(canvas.transform, false);
            var rootRect = _victoryRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var rootImg = _victoryRoot.GetComponent<Image>();
            rootImg.color = new Color(0f, 0f, 0f, 0.55f);
            rootImg.raycastTarget = true;

            // 2) Panneau centré blanc
            _victoryPanel = new GameObject("VictoryPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _victoryPanel.transform.SetParent(_victoryRoot.transform, false);
            var panelRect = _victoryPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(600f, 350f);
            panelRect.anchoredPosition = Vector2.zero;

            var panelImg = _victoryPanel.GetComponent<Image>();
            panelImg.color = new Color(1f, 1f, 1f, 0.95f);
            panelImg.raycastTarget = false;

            // 3) Texte "Niveau terminé !" dans le panneau
            var tmpFont = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Bold SDF");

            var textGO = new GameObject("VictoryText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(_victoryPanel.transform, false);
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0.55f);
            textRect.anchorMax = new Vector2(1f, 0.90f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            _victoryText = textGO.GetComponent<TextMeshProUGUI>();
            _victoryText.font = tmpFont;
            _victoryText.text = "Niveau terminé !";
            _victoryText.fontSize = 42;
            _victoryText.alignment = TextAlignmentOptions.Center;
            _victoryText.color = new Color(0.15f, 0.15f, 0.18f, 1f);
            _victoryText.raycastTarget = false;

            _victoryTextBasePosition = textRect.anchoredPosition;

            // 4) Étoiles dans le panneau, sous le texte
            Sprite starSprite = Resources.Load<Sprite>("UI/star");
            float starSize = 64f;
            float starSpacing = 16f;
            float totalW = 3f * starSize + 2f * starSpacing;

            for (int i = 0; i < 3; i++)
            {
                var starObj = new GameObject($"Star{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                starObj.transform.SetParent(_victoryPanel.transform, false);

                var starRect = (RectTransform)starObj.transform;
                starRect.anchorMin = new Vector2(0.5f, 0.5f);
                starRect.anchorMax = new Vector2(0.5f, 0.5f);
                starRect.pivot = new Vector2(0.5f, 0.5f);
                starRect.sizeDelta = new Vector2(starSize, starSize);

                float xOffset = -totalW * 0.5f + starSize * 0.5f + i * (starSize + starSpacing);
                starRect.anchoredPosition = new Vector2(xOffset, -30f);

                Image img = starObj.GetComponent<Image>();
                if (img == null) img = starObj.AddComponent<Image>();
                img.sprite = starSprite;
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
                img.color = Color.white;
                img.raycastTarget = false;

                _victoryStars[i] = img;
                _victoryStarRoots[i] = starObj;
                starObj.SetActive(false);
            }

            // 5) Bouton "Continuer" dans le panneau
            var btnGO = new GameObject("BtnContinuer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            btnGO.transform.SetParent(_victoryPanel.transform, false);
            var btnRect2 = btnGO.GetComponent<RectTransform>();
            btnRect2.anchorMin = new Vector2(0.5f, 0.05f);
            btnRect2.anchorMax = new Vector2(0.5f, 0.05f);
            btnRect2.pivot = new Vector2(0.5f, 0.5f);
            btnRect2.sizeDelta = new Vector2(300f, 65f);
            btnRect2.anchoredPosition = Vector2.zero;

            var btnImg = btnGO.GetComponent<Image>();
            btnImg.color = new Color(0.26f, 0.55f, 0.88f, 1f);

            var btnComp = btnGO.AddComponent<Button>();
            btnComp.targetGraphic = btnImg;
            btnComp.onClick.AddListener(() =>
            {
                SFXManager.Instance.PlayMenuClose();
                SelectedLevel = _numeroNiveau + 1;
                UnityEngine.SceneManagement.SceneManager.LoadScene("TestGrid");
            });

            var btnTxtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            btnTxtGO.transform.SetParent(btnGO.transform, false);
            var btnTxtRect = btnTxtGO.GetComponent<RectTransform>();
            btnTxtRect.anchorMin = Vector2.zero;
            btnTxtRect.anchorMax = Vector2.one;
            btnTxtRect.offsetMin = Vector2.zero;
            btnTxtRect.offsetMax = Vector2.zero;

            var btnTxt = btnTxtGO.GetComponent<TextMeshProUGUI>();
            btnTxt.font = tmpFont;
            btnTxt.text = "Continuer";
            btnTxt.fontSize = 28;
            btnTxt.fontStyle = FontStyles.Bold;
            btnTxt.color = Color.white;
            btnTxt.alignment = TextAlignmentOptions.Center;
            btnTxt.raycastTarget = false;
        }
    }
}
