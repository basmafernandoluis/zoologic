using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zoologic.Core;

namespace Zoologic
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

        public static int SelectedLevel = 1;
        public static bool IsDailyPuzzle = false;

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
        private Image _victoryOwl;

        // Paramètres de l'animation de victoire.
        private const float VictoryAnimationDuration = 0.9f;
        private const float VictoryTextSlide = 30f;

        // Score : 100 de départ, -15 par conflit. La pénalité cumulée ne diminue jamais.
        private const int ScoreDepart = 100;
        private const int ScorePenaliteConflit = 15;
        private int _totalPenaliteCumul;

        // Économie : récompense en pièces à la victoire.
        private const int CoinBaseReward = 40;
        private const int CoinStarBonus = 10;

        // Économie : coût d'un indice acheté lorsque les indices gratuits sont épuisés.
        public const int IndiceCout = 20;

        // Économie : coût du power-up « gomme » (retire tous les pions en conflit).
        public const int GommeCout = 30;

        // Recharge (s) après usage de la gomme avant de pouvoir la racheter.
        private const float GommeRecharge = 1.2f;

        // Double-tap detection
        private const float DoubleTapWindow = 0.3f;
        private float _lastTapTime;
        private int _lastTapRow = -1;
        private int _lastTapCol = -1;
        private Coroutine _pendingSingleTapRoutine;

        // Compteur d'erreurs pour le calcul des étoiles.
        private int _conflictsThisLevel;

        // Fond d'écran : dégradé vertical chaud (crème → pêche pâle).
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
            _totalPenaliteCumul = 0;

            SFXManager.Instance.ResumeMusic();

            Canvas canvas = null;

            try
            {
                canvas = EnsureCanvas();
                EnsureEventSystem();
                CreateBackground(canvas);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Zoologic] Start: échec de l'initialisation de l'environnement UI.\n" + e);
                return;
            }

            try
            {
                _hud = gameObject.AddComponent<GameHUD>();
                _hud.Build(canvas, _numeroNiveau);
                _hud.OnReessayer = ReinitialiserNiveau;
                _hud.OnIndiceDemande = DemanderIndice;
                _hud.OnGommeDemande = UtiliserGomme;
                _hud.OnPubViesDemande = HandlePubVies;
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Zoologic] Start: échec de la construction du HUD.\n" + e);
            }

            try
            {
                _grid = GenerateLevel();
                _xMarks = new bool[_grid.Size, _grid.Size];
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Zoologic] Start: échec de la génération du niveau.\n" + e);
                return;
            }

            try
            {
                _gridView.OnCellTapped = HandleCellTapped;
                _gridView.Build(_grid, (RectTransform)canvas.transform);

                if (_gridView.BoardContainer != null)
                    _gridView.BoardContainer.anchoredPosition =
                        new Vector2(0f, _hud.BoardYOffset);

                _hud.SetProgression(_grid.Pions.Count, _grid.Size);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Zoologic] Start: échec de la construction de la grille.\n" + e);
            }

            try
            {
                _hud.CreerPanneauDefaite(canvas);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Zoologic] Start: échec de la création du panneau de défaite.\n" + e);
            }

            try
            {
                CreateVictoryPanel(canvas);
                if (_victoryRoot != null)
                    _victoryRoot.SetActive(false);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Zoologic] Start: échec de la création du panneau de victoire.\n" + e);
            }

            try
            {
                _livesManager = new LivesManager();
                _livesManager.OnPartiePerdue = GererPartiePerdue;
                _hud.SetVies(_livesManager.Vies);
                if (_livesManager.Vies <= 0)
                    GererPartiePerdue();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Zoologic] Start: échec de l'initialisation du gestionnaire de vies.\n" + e);
            }

            SceneFader.FadeIn(this, canvas, 0.35f);
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
                IsDailyPuzzle = false;
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
            if (IsDailyPuzzle)
            {
                var dailyGen = new LevelGenerator(seed: DailyPuzzleManager.GetTodaySeed());
                int dailySize = DailyPuzzleManager.GetTodaySize();
                int dailyDiff = 3;
                try
                {
                    PuzzleGrid g = dailyGen.GenerateLevel(dailySize, dailyDiff);
                    if (g != null) return g;
                }
                catch (Exception) { }
                return dailyGen.GenerateUniqueGrid(dailySize);
            }

            var generator = new LevelGenerator(seed: _numeroNiveau);
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
        // Interactions — détection double-tap.
        // ------------------------------------------------------------------

        private void HandleCellTapped(int row, int col)
        {
            if (_partieTerminee)
                return;

            float now = Time.unscaledTime;
            bool isDoubleTap = _lastTapRow == row && _lastTapCol == col
                && (now - _lastTapTime) < DoubleTapWindow;

            if (isDoubleTap)
            {
                // Double-tap : annule le tap simple en attente et exécute l'action pion.
                if (_pendingSingleTapRoutine != null)
                {
                    StopCoroutine(_pendingSingleTapRoutine);
                    _pendingSingleTapRoutine = null;
                }

                PerformDoubleTapAction(row, col);
                _lastTapRow = -1;
                _lastTapCol = -1;
            }
            else
            {
                // Premier tap : lance un délai avant d'exécuter l'action simple (X).
                _lastTapTime = now;
                _lastTapRow = row;
                _lastTapCol = col;

                if (_pendingSingleTapRoutine != null)
                    StopCoroutine(_pendingSingleTapRoutine);
                _pendingSingleTapRoutine = StartCoroutine(DelayedSingleTap(row, col));
            }
        }

        private IEnumerator DelayedSingleTap(int row, int col)
        {
            yield return new WaitForSecondsRealtime(DoubleTapWindow);
            _pendingSingleTapRoutine = null;
            PerformSingleTapAction(row, col);
        }

        /// <summary>
        /// Tap simple : toggule un X sur la case (note brouillon).
        /// Aucun conflit/score/vie n'est vérifié.
        /// </summary>
        private void PerformSingleTapAction(int row, int col)
        {
            if (_partieTerminee)
                return;

            // Si la case contient un pion, l'animal "réagit" d'un petit rebond.
            if (_grid.HasPion(row, col))
            {
                _gridView.PlayHi(row, col);
                return;
            }

            bool hasX = _xMarks[row, col];
            _xMarks[row, col] = !hasX;
            _gridView.SetX(row, col, !hasX);

            SFXManager.Instance.PlayDialogueBlip();
        }

        /// <summary>
        /// Double-tap : place ou retire un pion.
        /// Vérifie les conflits, le score et les victoires uniquement ici.
        /// </summary>
        private void PerformDoubleTapAction(int row, int col)
        {
            if (_partieTerminee)
                return;

            if (_grid.HasPion(row, col))
            {
                _grid.RemovePion(row, col);
                _gridView.SetPion(row, col, false);

                SFXManager.Instance.PlayClickedOut();

                _hud.SetProgression(_grid.Pions.Count, _grid.Size);
                ReevaluerConflits();
                UpdateVictoryVisibility();
                return;
            }

            _grid.PlacePion(row, col);
            _xMarks[row, col] = false;
            _gridView.SetPion(row, col, true);
            _gridView.SetX(row, col, false);

            _hud.SetProgression(_grid.Pions.Count, _grid.Size);
            VerifierConflitPlacement(row, col);
            FlashAllConflicts();
            UpdateVictoryVisibility();
        }

        private void DemanderIndice()
        {
            if (_partieTerminee)
                return;

            // Mode achat : lorsque les indices gratuits sont épuisés, l'indice coûte
            // des pièces. On valide la solvabilité avant d'afficher le guide.
            bool purchaseMode = _hud.IndiceCount <= 0;
            if (purchaseMode && !CurrencyManager.HasCoins(IndiceCout))
            {
                _hud.NotifierPiècesInsuffisantes(IndiceCout);
                return;
            }

            bool success = _gridView.RequestHint();

            if (!success)
                return;

            if (!purchaseMode)
            {
                _hud.DecrementIndice();
                SFXManager.Instance.PlayUnlock();
            }
            else
            {
                CurrencyManager.SpendCoins(IndiceCout);
                SFXManager.Instance.PlayUnlock();
                _hud.RefreshCoins();
            }
        }

        /// <summary>
        /// Utilise le power-up « gomme » : paye son coût, puis retire tous les pions
        /// actuellement en conflit (les pions valides sont conservés).
        /// </summary>
        private void UtiliserGomme()
        {
            if (_partieTerminee)
                return;

            if (!CurrencyManager.HasCoins(GommeCout))
            {
                _hud.NotifierPiècesInsuffisantes(GommeCout);
                return;
            }

            var pions = new List<(int row, int col)>(_grid.Pions);
            var enConflit = new HashSet<(int row, int col)>();

            for (int i = 0; i < pions.Count; i++)
            {
                for (int j = i + 1; j < pions.Count; j++)
                {
                    if (SontEnConflit(pions[i], pions[j]))
                    {
                        enConflit.Add(pions[i]);
                        enConflit.Add(pions[j]);
                    }
                }
            }

            // Rien à retirer : on ne facture pas l'utilisation superflue.
            if (enConflit.Count == 0)
            {
                _hud.NotifierAucuneCible();
                return;
            }

            CurrencyManager.SpendCoins(GommeCout);
            _hud.RefreshCoins();
            SFXManager.Instance.PlayClickedOut();

            foreach ((int row, int col) in enConflit)
            {
                _grid.RemovePion(row, col);
                _gridView.SetPion(row, col, false);
            }

            _hud.SetProgression(_grid.Pions.Count, _grid.Size);
            _gridView.ShakeBoard(20f, 0.3f);
            _hud.BloquerPowerUpTemporairement(GommeRecharge);
            UpdateVictoryVisibility();
        }

        private void HandlePubVies()
        {
            if (_livesManager == null || _hud == null) return;
            _livesManager.AjouterVies(LivesManager.MaxVies);
            _hud.SetVies(_livesManager.Vies);
            _hud.CacherDefaite();
            _partieTerminee = false;
            SFXManager.Instance.ResumeMusic();
            _hud.BloquerInteractions(false);
            SFXManager.Instance.PlayUnlock();
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
                _gridView.PunchBoard();
                return;
            }

            SFXManager.Instance.PlayFailure();

            _conflictsThisLevel++;
            _totalPenaliteCumul++;
            _livesManager.PerdreVie();
            _hud.SetVies(_livesManager.Vies);

            _gridView.ShakeBoard(26f, 0.35f);

            int nouveauScore = Mathf.Max(0, ScoreDepart - _totalPenaliteCumul * ScorePenaliteConflit);
            _hud.SetScore(nouveauScore);
        }

        private void ReevaluerConflits()
        {
            int nouveauScore = Mathf.Max(0, ScoreDepart - _totalPenaliteCumul * ScorePenaliteConflit);
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
            SFXManager.Instance.PauseMusic();
            _hud.BloquerInteractions(true);
            _hud.AfficherDefaite();
        }

        private void ReinitialiserNiveau()
        {
            _partieTerminee = false;
            SFXManager.Instance.ResumeMusic();
            _conflictsThisLevel = 0;
            _totalPenaliteCumul = 0;
            _hud.CacherDefaite();
            HideVictory();

            _livesManager.Reinitialiser();
            _hud.Reinitialiser(ScoreDepart, LivesManager.ViesDepart, _hud.IndiceCount);
            _hud.SetProgression(0, _grid.Size);

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
            SFXManager.Instance.PauseMusic();
            SFXManager.Instance.PlaySuccess();

            if (IsDailyPuzzle)
            {
                if (!DailyPuzzleManager.IsCompletedToday())
                {
                    CurrencyManager.AddCoins(DailyPuzzleManager.RewardCoins);
                    DailyPuzzleManager.MarkCompletedToday();
                    _hud.RefreshCoins();
                }
            }
            else
            {
                int stars = _conflictsThisLevel == 0 ? 3
                    : _conflictsThisLevel <= 2 ? 2
                    : 1;

                LevelProgressManager.SetStars(_numeroNiveau, stars);
                LevelProgressManager.UnlockNextLevel(_numeroNiveau);

                int coinReward = CoinBaseReward + stars * CoinStarBonus;
                CurrencyManager.AddCoins(coinReward);
                _hud.RefreshCoins();
            }

            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
                ConfettiHelper.Burst(this, canvas, 70);

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

            // Réinitialiser le hibou
            if (_victoryOwl != null)
            {
                _victoryOwl.transform.localScale = Vector3.zero;
                _victoryOwl.transform.localRotation = Quaternion.identity;
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

            // Hibou : rebond EaseOutBack puis oscillation joyeuse ±8°
            if (_victoryOwl != null)
                yield return StartCoroutine(OwlVictoryRoutine());

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
        // Animation du hibou de victoire : rebond + oscillation joyeuse.
        // ------------------------------------------------------------------

        private IEnumerator OwlVictoryRoutine()
        {
            Transform owlT = _victoryOwl.transform;
            owlT.localScale = Vector3.zero;

            // Rebond EaseOutBack : 0 → 1
            float bounceDur = 0.35f;
            float elapsed = 0f;
            while (elapsed < bounceDur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / bounceDur);
                float s = Easing.EaseOutBack(t);
                owlT.localScale = new Vector3(s, s, s);
                yield return null;
            }
            owlT.localScale = Vector3.one;

            // Oscillation ±8°, 3 allers-retours amortis
            float wobbleAmp = 8f;
            float wobbleDur = 0.4f;
            for (int swing = 0; swing < 3; swing++)
            {
                float dir = (swing % 2 == 0) ? 1f : -1f;
                float target = wobbleAmp * dir / (1 + swing * 0.5f);
                float start = owlT.localEulerAngles.z;
                if (start > 180f) start -= 360f;

                elapsed = 0f;
                while (elapsed < wobbleDur)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / wobbleDur);
                    float angle = Mathf.Lerp(start, target, Easing.EaseOutCubic(t));
                    owlT.localRotation = Quaternion.Euler(0f, 0f, angle);
                    yield return null;
                }
            }

            owlT.localRotation = Quaternion.identity;
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
            BackgroundHelper.ApplyBackground(canvas.transform);
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

            // 2) Panneau centré blanc (agrandi pour accueillir le hibou)
            _victoryPanel = new GameObject("VictoryPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _victoryPanel.transform.SetParent(_victoryRoot.transform, false);
            var panelRect = _victoryPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(600f, 480f);
            panelRect.anchoredPosition = Vector2.zero;

            var panelImg = _victoryPanel.GetComponent<Image>();
            panelImg.color = new Color(1f, 1f, 1f, 0.95f);
            panelImg.raycastTarget = false;

            // 3) Hibou mascotte — au-dessus du texte
            Sprite owlSprite = Resources.Load<Sprite>("Art/Animals/owl");
            if (owlSprite != null)
            {
                var owlGO = new GameObject("VictoryOwl", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                owlGO.transform.SetParent(_victoryPanel.transform, false);
                var owlRect = owlGO.GetComponent<RectTransform>();
                owlRect.anchorMin = new Vector2(0.5f, 0.82f);
                owlRect.anchorMax = new Vector2(0.5f, 0.82f);
                owlRect.pivot = new Vector2(0.5f, 0.5f);
                owlRect.sizeDelta = new Vector2(120f, 120f);
                owlRect.anchoredPosition = Vector2.zero;

                _victoryOwl = owlGO.GetComponent<Image>();
                _victoryOwl.sprite = owlSprite;
                _victoryOwl.preserveAspect = true;
                _victoryOwl.raycastTarget = false;
            }

            // 4) Texte "Niveau terminé !" dans le panneau (décalé vers le bas pour laisser place au hibou)
            var tmpFont = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Bold SDF");

            var textGO = new GameObject("VictoryText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(_victoryPanel.transform, false);
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0.50f);
            textRect.anchorMax = new Vector2(1f, 0.75f);
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

            // 5) Étoiles dans le panneau, sous le texte
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
                starRect.anchoredPosition = new Vector2(xOffset, -60f);

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

            // 6) Bouton "Continuer" dans le panneau
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
                Canvas c = FindFirstObjectByType<Canvas>();
                if (IsDailyPuzzle)
                {
                    IsDailyPuzzle = false;
                    SceneFader.FadeOut(this, c, 0.3f,
                        () => UnityEngine.SceneManagement.SceneManager.LoadScene("LevelMap"));
                }
                else
                {
                    SelectedLevel = _numeroNiveau + 1;
                    SceneFader.FadeOut(this, c, 0.3f,
                        () => UnityEngine.SceneManagement.SceneManager.LoadScene("TestGrid"));
                }
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
