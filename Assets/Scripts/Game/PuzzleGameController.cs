using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
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
        private GameObject _victoryPerfectBadge;
        private bool _victoryPerfect;
        private GameObject _victoryGlow;
        private Coroutine _victoryGlowRoutine;
        private static Sprite _radialGlowSprite;
        private TextMeshProUGUI _victoryText;
        private TextMeshProUGUI _victoryLevelText;
        private TextMeshProUGUI _victoryCoinText;
        private GameObject _victoryCoinPill;
        private Outline _victoryOutline;
        private Vector2 _victoryTextBasePosition;
        private Image _victoryOwl;
        private int _victoryStarsEarned = 3;
        private int _victoryCoinReward;

        // Paramètres de l'animation de victoire.
        private const float VictoryAnimationDuration = 0.9f;
        private const float VictoryTextSlide = 30f;

        // Score : 100 de départ, -15 par conflit. La pénalité cumulée ne diminue jamais.
        private const int ScoreDepart = 100;
        private const int ScorePenaliteConflit = 15;
        private int _totalPenaliteCumul;

        // Économie : récompense en pièces à la victoire (resserrée pour
        // garder les power-ups significatifs : ~1 victoire = 1 indice).
        private const int CoinBaseReward = 10;
        private const int CoinStarBonus = 5;

        // Bonus du niveau parfait (0 conflit + coups == taille) : ~1 gomme offerte.
        private const int PerfectBonus = 10;

        // Économie : coût d'un indice acheté lorsque les indices gratuits sont épuisés.
        public const int IndiceCout = 20;

        // Économie : coût du power-up « gomme » (retire tous les pions en conflit).
        public const int GommeCout = 30;

        // Économie : achat dépannage d'1 vie dans la modale d'échec. Volontairement
        // cher (~4-5 victoires) pour que la pub rewarded (+3 vies gratuites)
        // reste le meilleur choix (AdMob garde son importance).
        public const int ViePayanteCout = 100;

        // Recharge (s) après usage de la gomme avant de pouvoir la racheter.
        private const float GommeRecharge = 1.2f;

        // Drag & drop (barre d'animaux + déplacement / retrait des pions).
        private BoardDragController _drag;

        // Compteur d'erreurs pour le calcul des étoiles.
        private int _conflictsThisLevel;

        // Compteur de coups (poses + déplacements) pour l'objectif parfait.
        private int _moveCount;

        // Fond d'écran : dégradé vertical chaud (crème → pêche pâle).
        private static readonly Color OverlayColor = new Color(0f, 0f, 0f, 0.55f);

        private void Awake()
        {
            _gridView = GetComponent<GridView>();
            if (_gridView == null)
                _gridView = gameObject.AddComponent<GridView>();
            EnsureEventSystem();
        }

        private void PreloadGameplaySprites()
        {
            Resources.Load<Sprite>("Sprites/bak_0");
            Resources.Load<Sprite>("Sprites/bak_2");
            Resources.Load<Sprite>("Sprites/bak_3");
            Resources.Load<Sprite>("Sprites/bak_4");
            Resources.Load<Sprite>("Sprites/b_11");
            Resources.Load<Sprite>("Sprites/b_13");
            Resources.Load<Sprite>("Sprites/b_9");
            Resources.Load<Sprite>("Sprites/b_8");
            Resources.Load<Sprite>("Sprites/b_38");
            Resources.Load<Sprite>("Sprites/b_44");
        }

        private void Start()
        {
            PreloadGameplaySprites();
            _numeroNiveau = SelectedLevel;
            _conflictsThisLevel = 0;
            _moveCount = 0;
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
                _hud.OnViePayanteDemande = HandleAchatVie;
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
                ReorderCanvasHierarchy(canvas);
                SetupDragAndDrop(canvas);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Zoologic] Start: échec de la construction de la grille.\n" + e);
            }

            try
            {
                _hud.CreerPanneauDefaite(canvas, _numeroNiveau, IsDailyPuzzle);
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
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Zoologic] Start: échec de l'initialisation du gestionnaire de vies.\n" + e);
            }

            SceneFader.FadeIn(this, canvas, 0.35f);
        }

        private void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame ?? false)
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
            msgTxt.text = Zoologic.Localization.LocalizationManager.Get("hud.quit_level");
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

            var btnOui = CreerBoutonSimple(btnRow.transform, Zoologic.Localization.LocalizationManager.Get("menu.yes"), dangerRed, fontBody, 26f);
            btnOui.onClick.AddListener(() =>
            {
                SFXManager.Instance.PlayMenuClose();
                IsDailyPuzzle = false;
                UnityEngine.SceneManagement.SceneManager.LoadScene("LevelMap");
            });

            var btnAnnuler = CreerBoutonSimple(btnRow.transform, Zoologic.Localization.LocalizationManager.Get("menu.no"), accentBlue, fontBody, 26f);
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
        // Interactions — tap = X brouillon, drag = poser / déplacer / retirer.
        // ------------------------------------------------------------------

        /// <summary>
        /// Tap simple : toggule un X sur la case (note brouillon).
        /// Aucun conflit/score/vie n'est vérifié.
        /// </summary>
        private void HandleCellTapped(int row, int col)
        {
            if (_partieTerminee || _victoryShown)
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
        /// Pose un pion (drop depuis la barre). Conflits, score et victoire ici.
        /// </summary>
        private void PlacePionAt(int row, int col, bool countMission = true)
        {
            if (_partieTerminee || _victoryShown || _grid.HasPion(row, col))
                return;

            _grid.PlacePion(row, col);
            _xMarks[row, col] = false;
            _gridView.SetPion(row, col, true);
            _gridView.SetX(row, col, false);

            _moveCount++;
            _hud.SetProgression(_grid.Pions.Count, _grid.Size);
            _hud.SetMoves(_moveCount);
            _hud.UpdateTrayCount(_grid.Size - _grid.Pions.Count);
            if (countMission)
                MissionManager.AddProgress(MissionType.PlaceAnimals, 1);
            VerifierConflitPlacement(row, col);
            RefreshConflicts();
            UpdateVictoryVisibility();
        }

        /// <summary>
        /// Retire un pion (drop hors plateau).
        /// </summary>
        private void RemovePionAt(int row, int col)
        {
            if (_partieTerminee || _victoryShown || !_grid.HasPion(row, col))
                return;

            _grid.RemovePion(row, col);
            _gridView.SetPion(row, col, false);

            SFXManager.Instance.PlayClickedOut();

            _hud.SetProgression(_grid.Pions.Count, _grid.Size);
            _hud.UpdateTrayCount(_grid.Size - _grid.Pions.Count);
            ReevaluerConflits();
            RefreshConflicts();
            UpdateVictoryVisibility();
        }

        /// <summary>
        /// Déplace un pion (drag pion → autre case libre). Une seule évaluation.
        /// </summary>
        private void MovePionAt(int fromRow, int fromCol, int toRow, int toCol)
        {
            if (_partieTerminee || _victoryShown)
                return;
            if (!_grid.HasPion(fromRow, fromCol) || _grid.HasPion(toRow, toCol))
                return;

            _grid.RemovePion(fromRow, fromCol);
            _gridView.SetPion(fromRow, fromCol, false);
            PlacePionAt(toRow, toCol, countMission: false);
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
                _hud.NotifierPiecesInsuffisantes(IndiceCout);
                return;
            }

            bool success = _gridView.RequestHint();

            if (!success)
                return;

            _hud.NotifierIndiceAffiche();

            if (!purchaseMode)
            {
                _hud.DecrementIndice();
                SFXManager.Instance.PlayUnlock();
            }
            else
            {
                if (!CurrencyManager.SpendCoins(IndiceCout))
                {
                    _hud.NotifierPiecesInsuffisantes(IndiceCout);
                    return;
                }
                SFXManager.Instance.PlayUnlock();
                _hud.RefreshCoins();
                _hud.RefreshIndiceDisplay();
            }
            MissionManager.AddProgress(MissionType.UseHints, 1);
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
                _hud.NotifierPiecesInsuffisantes(GommeCout);
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
            _hud.UpdateTrayCount(_grid.Size - _grid.Pions.Count);
            MissionManager.AddProgress(MissionType.UseEraser, 1);
            _gridView.ShakeBoard(20f, 0.3f);
            _hud.BloquerPowerUpTemporairement(GommeRecharge);
            RefreshConflicts();
            UpdateVictoryVisibility();
        }

        private void HandlePubVies()
        {
            if (_livesManager == null || _hud == null) return;
            var admob = AdMobManager.Instance;
            System.Action grant = () =>
            {
                _livesManager.AjouterVies(LivesManager.MaxVies);
                _hud.SetVies(_livesManager.Vies);
                _hud.CacherDefaite();
                _partieTerminee = false;
                SFXManager.Instance.ResumeMusic();
                _hud.BloquerInteractions(false);
                SFXManager.Instance.PlayUnlock();
            };
            // Families: no reward without a real ad view.
            if (admob != null && admob.IsRewardedReady()) admob.ShowRewarded(grant, () => _hud.NotifierPubIndisponible());
            else _hud.NotifierPubIndisponible();
        }

        /// <summary>
        /// Achat dépannage : 1 vie contre des pièces (modale d'échec). La modale
        /// reste ouverte : Réessayer se réactive et le joueur peut retenter.
        /// </summary>
        private void HandleAchatVie()
        {
            if (_livesManager == null || _hud == null) return;
            if (_livesManager.Vies >= LivesManager.MaxVies) return;
            if (!CurrencyManager.SpendCoins(ViePayanteCout))
            {
                _hud.NotifierPiecesInsuffisantes(ViePayanteCout);
                return;
            }
            _livesManager.AjouterVies(1);
            _hud.SetVies(_livesManager.Vies);
            _hud.RefreshCoins();
            _hud.RefreshDefeatState();
            SFXManager.Instance.PlayUnlock();
            Haptics.VibrateLight();
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

            // Explication : nomme la règle violée et ne flash que la paire
            // fautive (nouveau pion + adversaires), pas tout le plateau.
            _hud.NotifierConflit(conflits[0]);
            _gridView.FlashConflict(row, col);
            foreach (var (r, c) in RuleValidator.GetConflictingCells(_grid, row, col))
                _gridView.FlashConflict(r, c);
            Haptics.VibrateLight();

            int nouveauScore = Mathf.Max(0, ScoreDepart - _totalPenaliteCumul * ScorePenaliteConflit);
            _hud.SetScore(nouveauScore);
        }

        private void ReevaluerConflits()
        {
            int nouveauScore = Mathf.Max(0, ScoreDepart - _totalPenaliteCumul * ScorePenaliteConflit);
            _hud.SetScore(nouveauScore);
        }

        /// <summary>
        /// Recalcule les anneaux de conflit persistants après une mutation.
        /// </summary>
        private void RefreshConflicts()
        {
            if (_grid == null || _gridView == null)
                return;
            var set = new HashSet<(int row, int col)>();
            var pions = new List<(int row, int col)>(_grid.Pions);
            for (int i = 0; i < pions.Count; i++)
            {
                for (int j = i + 1; j < pions.Count; j++)
                {
                    if (SontEnConflit(pions[i], pions[j]))
                    {
                        set.Add(pions[i]);
                        set.Add(pions[j]);
                    }
                }
            }
            _gridView.RefreshConflictMarks(set);
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

        /// <summary>
        /// Câble le drag &amp; drop : barre d'animaux, déplacement et retrait des pions.
        /// </summary>
        private void SetupDragAndDrop(Canvas canvas)
        {
            if (_drag == null)
                _drag = BoardDragController.Create(canvas, _gridView);
            else
                _drag.SetGridView(_gridView);

            _drag.CanPlaceAt = (r, c) => !_grid.HasPion(r, c);
            _drag.OnTrayDropOnCell = (r, c) =>
            {
                if (_partieTerminee || _victoryShown) { _drag.Cancel(); return; }
                PlacePionAt(r, c);
            };
            _drag.OnTrayDropInvalid = (r, c) =>
            {
                _gridView.ShakeBoard(12f, 0.2f);
                SFXManager.Instance.PlayDialogueBlip();
            };
            _drag.OnPawnMove = (fr, fc, tr, tc) =>
            {
                if (_partieTerminee || _victoryShown) { _drag.Cancel(); return; }
                MovePionAt(fr, fc, tr, tc);
            };
            _drag.OnPawnDropInvalid = (r, c) =>
            {
                _gridView.FlashConflict(r, c);
                Haptics.VibrateLight();
            };
            _drag.OnPawnDropOutside = (r, c) =>
            {
                if (_partieTerminee || _victoryShown) { _drag.Cancel(); return; }
                RemovePionAt(r, c);
            };
            _gridView.OnPawnDragStart = (r, c, e) =>
            {
                if (_partieTerminee || _victoryShown)
                    return;
                _drag.BeginPawnDrag(r, c, _gridView.GetPawnSprite(r, c), e.pointerId);
                _drag.UpdateDrag(e.pointerId, e.position);
            };
            _gridView.OnPawnDrag = (r, c, e) =>
            {
                if (_drag == null || !_drag.IsDragging)
                    return;
                _drag.UpdateDrag(e.pointerId, e.position);
            };
            _gridView.OnPawnDragEnd = (r, c, e) =>
            {
                if (_drag == null || !_drag.IsDragging)
                    return;
                _drag.EndDrag(e.pointerId, e.position);
            };
            if (_hud != null)
                _hud.RebuildAnimalTray(_gridView.GetZoneAnimalSprites(), _drag);
        }

        private void GererPartiePerdue()
        {
            if (_drag != null)
                _drag.Cancel();
            _partieTerminee = true;
            SFXManager.Instance.PauseMusic();
            _hud.BloquerInteractions(true);
            _hud.AfficherDefaite();
        }

        private void ReinitialiserNiveau()
        {
            if (LivesManager.GetStoredLives() <= 0)
            {
                _hud.NotifierViesEpuisees();
                return;
            }

            _partieTerminee = false;
            SFXManager.Instance.ResumeMusic();
            _conflictsThisLevel = 0;
            _moveCount = 0;
            _totalPenaliteCumul = 0;
            if (_drag != null)
                _drag.Cancel();
            _hud.CacherDefaite();
            HideVictory();

            int livesForRetry = LivesManager.GetStoredLives();
            _hud.Reinitialiser(ScoreDepart, livesForRetry, _hud.IndiceCount);
            _hud.SetProgression(0, _grid.Size);
            _hud.SetMoves(0);
            _hud.UpdateTrayCount(_grid.Size);
            RefreshConflicts();

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
            _victoryPerfect = false;

            if (IsDailyPuzzle)
            {
                _victoryStarsEarned = 3;
                _victoryCoinReward = DailyPuzzleManager.RewardCoins;
                if (!DailyPuzzleManager.IsCompletedToday())
                {
                    DailyPuzzleManager.MarkCompletedToday();
                    int total = DailyPuzzleManager.RewardCoins + DailyPuzzleManager.GetStreakBonus();
                    CurrencyManager.AddCoins(total);
                    _hud.RefreshCoins();
                    _victoryCoinReward = total;
                }
            }
            else
            {
                int stars = _conflictsThisLevel == 0 ? 3
                    : _conflictsThisLevel <= 2 ? 2
                    : 1;

                LevelProgressManager.SetStars(_numeroNiveau, stars);
                LevelProgressManager.UnlockNextLevel(_numeroNiveau);
                MissionManager.AddProgress(MissionType.CompleteLevels, 1);
                MissionManager.AddProgress(MissionType.EarnStars, stars);

                int coinReward = CoinBaseReward + stars * CoinStarBonus;
                // Niveau parfait : 0 conflit + autant de coups que de cases.
                _victoryPerfect = _conflictsThisLevel == 0 && _moveCount == _grid.Size;
                if (_victoryPerfect)
                    coinReward += PerfectBonus;
                CurrencyManager.AddCoins(coinReward);
                _hud.RefreshCoins();
                _victoryStarsEarned = stars;
                _victoryCoinReward = coinReward;
            }

            if (_victoryLevelText != null)
                _victoryLevelText.text = IsDailyPuzzle ? Zoologic.Localization.LocalizationManager.Get("victory.daily_badge") : Zoologic.Localization.LocalizationManager.Get("victory.level_badge", _numeroNiveau);
            if (_victoryText != null)
                _victoryText.text = IsDailyPuzzle ? Zoologic.Localization.LocalizationManager.Get("victory.daily_title") : Zoologic.Localization.LocalizationManager.Get("victory.title");

            if (_drag != null)
                _drag.Cancel();

            if (_victoryPerfectBadge != null)
                _victoryPerfectBadge.SetActive(_victoryPerfect);

            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
                ConfettiHelper.Burst(this, canvas, _victoryPerfect ? 140 : 70);
            if (_victoryPerfect)
                Haptics.VibrateStrong();

            if (_victoryAnimation != null)
                StopCoroutine(_victoryAnimation);
            _victoryAnimation = StartCoroutine(VictoryAnimationRoutine());
            if (_victoryGlowRoutine != null)
                StopCoroutine(_victoryGlowRoutine);
            _victoryGlowRoutine = StartCoroutine(VictoryGlowRoutine());
        }

        /// <summary>Halo doré pulsé tant que la victoire est affichée.</summary>
        private IEnumerator VictoryGlowRoutine()
        {
            while (true)
            {
                if (_victoryGlow == null)
                    yield break;
                float t = (Mathf.Sin(Time.unscaledTime * 2.2f) + 1f) * 0.5f;
                var img = _victoryGlow.GetComponent<Image>();
                if (img != null)
                    img.color = new Color(1f, 0.85f, 0.35f, Mathf.Lerp(0.28f, 0.45f, t));
                float rot = _victoryGlow.transform.localEulerAngles.z + Time.unscaledDeltaTime * 8f;
                _victoryGlow.transform.localRotation = Quaternion.Euler(0f, 0f, rot);
                float s = 1f + Mathf.Sin(Time.unscaledTime * 2.2f) * 0.03f;
                _victoryGlow.transform.localScale = new Vector3(s, s, s);
                yield return null;
            }
        }

        /// <summary>Dégradé radial blanc (teinté doré à l'usage) pour le halo.</summary>
        private static Sprite GetRadialGlowSprite()
        {
            if (_radialGlowSprite != null)
                return _radialGlowSprite;
            const int res = 256;
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            float center = (res - 1) * 0.5f;
            float radius = res * 0.5f;
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float dx = (x - center) / radius;
                    float dy = (y - center) / radius;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = (1f - d) * (1f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            _radialGlowSprite = Sprite.Create(tex, new Rect(0f, 0f, res, res), new Vector2(0.5f, 0.5f));
            return _radialGlowSprite;
        }

        private void HideVictory()
        {
            if (_victoryAnimation != null)
            {
                StopCoroutine(_victoryAnimation);
                _victoryAnimation = null;
            }
            if (_victoryGlowRoutine != null)
            {
                StopCoroutine(_victoryGlowRoutine);
                _victoryGlowRoutine = null;
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

            // Cacher les stars (reset état grisé).
            for (int i = 0; i < 3; i++)
            {
                if (_victoryStarRoots[i] != null)
                    _victoryStarRoots[i].SetActive(false);
                if (_victoryStars[i] != null)
                {
                    _victoryStars[i].sprite = GridView.StarGrey;
                    _victoryStars[i].color = Color.white;
                }
            }
            if (_victoryCoinText != null)
                _victoryCoinText.text = "+0";
            if (_victoryPerfectBadge != null)
            {
                _victoryPerfectBadge.transform.localScale = Vector3.one;
                _victoryPerfectBadge.SetActive(false);
            }
            _victoryPerfect = false;
            if (_victoryPanel != null)
                _victoryPanel.transform.localScale = Vector3.one;

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
            Canvas canvasFx = FindFirstObjectByType<Canvas>();
            if (canvasFx != null)
                ConfettiHelper.Burst(this, canvasFx, _victoryPerfect ? 200 : 120);

            if (_victoryRoot != null)
                _victoryRoot.SetActive(true);
            Zoologic.Localization.LocalizationManager.ApplyFontsToScene();
            if (_victoryPanel != null)
            {
                _victoryPanel.transform.localScale = Vector3.zero;
                float pd = 0f;
                while (pd < 0.35f)
                {
                    float pt = Mathf.Clamp01(pd / 0.35f);
                    float ps = Easing.EaseOutBack(pt);
                    _victoryPanel.transform.localScale = new Vector3(ps, ps, ps);
                    pd += Time.unscaledDeltaTime;
                    yield return null;
                }
                _victoryPanel.transform.localScale = Vector3.one;
            }
            if (_victoryCoinText != null)
                _victoryCoinText.text = "+0";

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

            // Étoiles : les gagnées pop en or avec son, les autres restent grisées.
            for (int i = 0; i < 3; i++)
            {
                if (_victoryStars[i] == null || _victoryStarRoots[i] == null)
                    continue;
                if (i < _victoryStarsEarned)
                {
                    SFXManager.Instance.PlayUnlock();
                    Haptics.VibrateLight();
                    yield return StartCoroutine(StarPopRoutine(i));
                }
                else
                {
                    _victoryStarRoots[i].SetActive(true);
                    _victoryStarRoots[i].transform.localScale = Vector3.one * 0.85f;
                    _victoryStars[i].sprite = GridView.StarGrey;
                    _victoryStars[i].color = Color.white;
                }
                yield return new WaitForSecondsRealtime(0.15f);
            }

            // Badge parfait : pop doré après les étoiles.
            if (_victoryPerfect && _victoryPerfectBadge != null)
            {
                _victoryPerfectBadge.transform.localScale = Vector3.zero;
                SFXManager.Instance.PlayUnlock();
                Haptics.VibrateLight();
                float bd = 0f;
                while (bd < 0.3f)
                {
                    float bt = Mathf.Clamp01(bd / 0.3f);
                    float bs = Easing.EaseOutBack(bt);
                    _victoryPerfectBadge.transform.localScale = new Vector3(bs, bs, bs);
                    bd += Time.unscaledDeltaTime;
                    yield return null;
                }
                _victoryPerfectBadge.transform.localScale = Vector3.one;
                yield return new WaitForSecondsRealtime(0.1f);
            }

            // Compteur de pièces animé + punch de la pilule.
            if (_victoryCoinText != null)
            {
                SFXManager.Instance.PlayConfirm();
                float cd = 0f;
                const float countDur = 0.7f;
                while (cd < countDur)
                {
                    float ct = Mathf.Clamp01(cd / countDur);
                    int v = Mathf.RoundToInt(Mathf.Lerp(0f, _victoryCoinReward, Easing.EaseOutCubic(ct)));
                    _victoryCoinText.text = $"+{v}";
                    cd += Time.unscaledDeltaTime;
                    yield return null;
                }
                _victoryCoinText.text = $"+{_victoryCoinReward}";
                if (_victoryCoinPill != null)
                    Punch.Scale(this, _victoryCoinPill.GetComponent<RectTransform>(), 1.15f, 0.25f);
                Haptics.VibrateLight();
            }

            // Envol des pièces gagnées vers l'icône pièces du HUD.
            yield return StartCoroutine(VictoryCoinFlyRoutine());

            // Hibou : rebond EaseOutBack puis oscillation joyeuse ±8°
            if (_victoryOwl != null)
                yield return StartCoroutine(OwlVictoryRoutine());

            _victoryAnimation = null;

            // Interstitiel APRES la sequence (modale + pieces visibles),
            // jamais avant ni pendant. Incremente a chaque victoire.
            if (_victoryShown && !IsDailyPuzzle)
            {
                yield return new WaitForSecondsRealtime(0.8f);
                if (_victoryShown && !IsDailyPuzzle)
                    AdMobManager.Instance?.ShowInterstitialIfNeeded();
            }
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

            Sprite gold = Resources.Load<Sprite>("UI/star");
            if (gold != null)
                img.sprite = gold;
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

        private IEnumerator VictoryCoinFlyRoutine()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            Transform from = _victoryCoinPill != null ? _victoryCoinPill.transform : _victoryPanel.transform;
            if (canvas == null || from == null) yield break;
            GameObject targetGO = GameObject.Find("CoinNombre") ?? GameObject.Find("EconomyPill") ?? GameObject.Find("CoinIcone");
            Vector3 targetWorld = targetGO != null ? targetGO.transform.position
                : new Vector3(Screen.width * 0.5f, Screen.height - 120f, 0f);
            Vector3 startWorld = from.position;

            var flyRoot = new GameObject("VictoryCoinFly", typeof(RectTransform));
            flyRoot.transform.SetParent(canvas.transform, false);
            var flyRect = (RectTransform)flyRoot.transform;
            flyRect.anchorMin = Vector2.zero; flyRect.anchorMax = Vector2.one;
            flyRect.offsetMin = Vector2.zero; flyRect.offsetMax = Vector2.zero;
            flyRoot.transform.SetAsLastSibling();

            Sprite coinSprite = Resources.Load<Sprite>("UI/coin");
            const int n = 8;
            const float dur = 0.8f;
            const float stagger = 0.06f;
            var coins = new System.Collections.Generic.List<RectTransform>();
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject($"VCoin{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(flyRoot.transform, false);
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = new Vector2(40f, 40f);
                rt.position = startWorld;
                var im = go.GetComponent<Image>();
                im.sprite = coinSprite;
                im.preserveAspect = true;
                im.raycastTarget = false;
                go.SetActive(false);
                coins.Add(rt);
                StartCoroutine(VictoryFlyOne(rt, startWorld, targetWorld, i * stagger, dur));
                SFXManager.Instance.PlayUnlock();
            }

            yield return new WaitForSecondsRealtime(stagger * (n - 1) + dur + 0.1f);
            if (flyRoot != null) Destroy(flyRoot);
            SFXManager.Instance.PlaySuccess();
            Haptics.VibrateLight();
            if (targetGO != null)
                Punch.Scale(this, (RectTransform)targetGO.transform, 1.22f, 0.28f);
            if (_hud != null) _hud.RefreshCoins();
        }

        private IEnumerator VictoryFlyOne(RectTransform rt, Vector3 from, Vector3 to, float delay, float dur)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            if (rt == null) yield break;
            rt.gameObject.SetActive(true);
            rt.position = from;
            float el = 0f;
            Vector3 ctrl = (from + to) * 0.5f + new Vector3(60f, 220f, 0f);
            while (el < dur)
            {
                float t = Mathf.Clamp01(el / dur);
                float e = Easing.EaseInOutQuad(t);
                Vector3 a = Vector3.Lerp(from, ctrl, e);
                Vector3 b = Vector3.Lerp(ctrl, to, e);
                rt.position = Vector3.Lerp(a, b, e);
                float s = Mathf.Lerp(1.2f, 0.6f, e);
                rt.localScale = new Vector3(s, s, s);
                rt.Rotate(0f, 0f, 540f * Time.unscaledDeltaTime);
                el += Time.unscaledDeltaTime;
                yield return null;
            }
            if (rt != null) rt.gameObject.SetActive(false);
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
            foreach (var m in UnityEngine.Object.FindObjectsByType<StandaloneInputModule>(FindObjectsSortMode.None)) DestroyImmediate(m);
            if (EventSystem.current != null)
            {
                var cur = EventSystem.current;
                if (cur.GetComponent<InputSystemUIInputModule>() == null)
                    cur.gameObject.AddComponent<InputSystemUIInputModule>();
                return;
            }

            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<InputSystemUIInputModule>();
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
            rootImg.color = new Color(0.08f, 0.05f, 0.03f, 0.72f);
            rootImg.raycastTarget = true;

            // 2) Panneau centré blanc arrondi avec relief cartoon
            _victoryPanel = new GameObject("VictoryPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _victoryPanel.transform.SetParent(_victoryRoot.transform, false);
            var panelRect = _victoryPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(640f, 640f);
            panelRect.anchoredPosition = Vector2.zero;

            var panelImg = _victoryPanel.GetComponent<Image>();
            if (B1UI.Bubble != null)
            {
                panelImg.sprite = B1UI.Bubble;
                panelImg.type = Image.Type.Sliced;
                panelImg.pixelsPerUnitMultiplier = 1f;
            }
            panelImg.color = new Color(1f, 0.985f, 0.95f, 1f);
            panelImg.raycastTarget = false;
            // Halo doré festif derrière le panneau : la victoire rayonne,
            // le plateau en arrière-plan s'efface.
            _victoryGlow = new GameObject("VictoryGlow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _victoryGlow.transform.SetParent(_victoryRoot.transform, false);
            var glowRect = _victoryGlow.GetComponent<RectTransform>();
            glowRect.anchorMin = new Vector2(0.5f, 0.5f);
            glowRect.anchorMax = new Vector2(0.5f, 0.5f);
            glowRect.pivot = new Vector2(0.5f, 0.5f);
            glowRect.sizeDelta = new Vector2(1000f, 1000f);
            glowRect.anchoredPosition = Vector2.zero;
            var glowImg = _victoryGlow.GetComponent<Image>();
            glowImg.sprite = GetRadialGlowSprite();
            glowImg.type = Image.Type.Simple;
            glowImg.preserveAspect = true;
            glowImg.color = new Color(1f, 0.85f, 0.35f, 0.35f);
            glowImg.raycastTarget = false;
            _victoryGlow.transform.SetAsFirstSibling();
            _victoryPanel.transform.SetAsLastSibling();
            var panelShadow = _victoryPanel.AddComponent<Shadow>();
            panelShadow.effectColor = new Color(0.18f, 0.11f, 0.06f, 0.35f);
            panelShadow.effectDistance = new Vector2(0f, -12f);
            var panelOutline = _victoryPanel.AddComponent<Outline>();
            panelOutline.effectColor = new Color(1f, 1f, 1f, 0.9f);
            panelOutline.effectDistance = new Vector2(3f, -3f);

            // Badge niveau en haut du panneau
            var lvlGO = new GameObject("VictoryLevel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            lvlGO.transform.SetParent(_victoryPanel.transform, false);
            var lvlRect = lvlGO.GetComponent<RectTransform>();
            lvlRect.anchorMin = new Vector2(0.5f, 1f);
            lvlRect.anchorMax = new Vector2(0.5f, 1f);
            lvlRect.pivot = new Vector2(0.5f, 1f);
            lvlRect.sizeDelta = new Vector2(500f, 44f);
            lvlRect.anchoredPosition = new Vector2(0f, -14f);
            _victoryLevelText = lvlGO.GetComponent<TextMeshProUGUI>();
            _victoryLevelText.font = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Bold SDF");
            _victoryLevelText.text = IsDailyPuzzle ? Zoologic.Localization.LocalizationManager.Get("victory.daily_badge") : Zoologic.Localization.LocalizationManager.Get("victory.level_badge", _numeroNiveau);
            Zoologic.Localization.LocalizationManager.ApplyTo(_victoryLevelText);
            _victoryLevelText.fontSize = 30;
            _victoryLevelText.fontStyle = FontStyles.Bold;
            _victoryLevelText.alignment = TextAlignmentOptions.Center;
            _victoryLevelText.color = new Color(0.95f, 0.55f, 0.15f, 1f);
            _victoryLevelText.outlineWidth = 0.12f;
            _victoryLevelText.outlineColor = new Color(0.35f, 0.18f, 0.05f, 0.35f);
            _victoryLevelText.raycastTarget = false;

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
            _victoryText.text = IsDailyPuzzle ? Zoologic.Localization.LocalizationManager.Get("victory.daily_title") : Zoologic.Localization.LocalizationManager.Get("victory.title");
            Zoologic.Localization.LocalizationManager.ApplyTo(_victoryText);
            _victoryText.fontSize = 52;
            _victoryText.fontStyle = FontStyles.Bold;
            _victoryText.alignment = TextAlignmentOptions.Center;
            _victoryText.color = new Color(0.18f, 0.12f, 0.08f, 1f);
            _victoryText.raycastTarget = false;
            var vSh = textGO.AddComponent<Shadow>();
            vSh.effectColor = new Color(1f, 0.92f, 0.75f, 0.55f);
            vSh.effectDistance = new Vector2(0f, -3f);

            _victoryTextBasePosition = textRect.anchoredPosition;

            // 5) Étoiles dans le panneau, sous le texte (taille augmentée)
            Sprite starSprite = Resources.Load<Sprite>("UI/star");
            float starSize = 84f;
            float starSpacing = 18f;
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
                starRect.anchoredPosition = new Vector2(xOffset, -50f);

                Image img = starObj.GetComponent<Image>();
                if (img == null) img = starObj.AddComponent<Image>();
                img.sprite = GridView.StarGrey;
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
                img.color = Color.white;
                img.raycastTarget = false;

                _victoryStars[i] = img;
                _victoryStarRoots[i] = starObj;
                starObj.SetActive(true);
            }

            // 5bis) Badge "PARFAIT" doré entre étoiles et pilule (caché par défaut).
            _victoryPerfectBadge = new GameObject("PerfectBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _victoryPerfectBadge.transform.SetParent(_victoryPanel.transform, false);
            var perfectRect = _victoryPerfectBadge.GetComponent<RectTransform>();
            perfectRect.anchorMin = new Vector2(0.5f, 0.5f);
            perfectRect.anchorMax = new Vector2(0.5f, 0.5f);
            perfectRect.pivot = new Vector2(0.5f, 0.5f);
            perfectRect.sizeDelta = new Vector2(420f, 46f);
            perfectRect.anchoredPosition = new Vector2(0f, -100f);
            var perfectBg = _victoryPerfectBadge.GetComponent<Image>();
            var perfectSprite = JellyUI.ButtonYellow;
            perfectBg.sprite = perfectSprite;
            perfectBg.type = Image.Type.Sliced;
            perfectBg.pixelsPerUnitMultiplier = 1f;
            var perfectContentGO = new GameObject("Content", typeof(RectTransform), typeof(CanvasRenderer));
            perfectContentGO.transform.SetParent(_victoryPerfectBadge.transform, false);
            var perfectContentRect = perfectContentGO.GetComponent<RectTransform>();
            perfectContentRect.anchorMin = Vector2.zero;
            perfectContentRect.anchorMax = Vector2.one;
            perfectContentRect.offsetMin = new Vector2(12f, 4f);
            perfectContentRect.offsetMax = new Vector2(-12f, -4f);
            var perfectHLG = perfectContentGO.AddComponent<HorizontalLayoutGroup>();
            perfectHLG.spacing = 8f;
            perfectHLG.childAlignment = TextAnchor.MiddleCenter;
            perfectHLG.childForceExpandWidth = false;
            perfectHLG.childControlWidth = false;
            var perfectStarGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            perfectStarGO.transform.SetParent(perfectContentGO.transform, false);
            var perfectStarLE = perfectStarGO.AddComponent<LayoutElement>();
            perfectStarLE.preferredWidth = 30f;
            perfectStarLE.preferredHeight = 30f;
            var perfectStarImg = perfectStarGO.GetComponent<Image>();
            perfectStarImg.sprite = Resources.Load<Sprite>("UI/star");
            perfectStarImg.preserveAspect = true;
            perfectStarImg.raycastTarget = false;
            var perfectTxtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            perfectTxtGO.transform.SetParent(perfectContentGO.transform, false);
            var perfectTxtLE = perfectTxtGO.AddComponent<LayoutElement>();
            perfectTxtLE.flexibleWidth = 1f;
            var perfectTxt = perfectTxtGO.GetComponent<TextMeshProUGUI>();
            perfectTxt.font = tmpFont;
            perfectTxt.text = Zoologic.Localization.LocalizationManager.Get("victory.perfect");
            Zoologic.Localization.LocalizationManager.ApplyTo(perfectTxt);
            perfectTxt.fontSize = 28;
            perfectTxt.fontStyle = FontStyles.Bold;
            perfectTxt.color = new Color(0.45f, 0.22f, 0.03f, 1f);
            perfectTxt.alignment = TextAlignmentOptions.Center;
            perfectTxt.raycastTarget = false;
            _victoryPerfectBadge.SetActive(false);

            // 5ter) Pilule récompense pièces sous les étoiles (décalée pour le badge).
            _victoryCoinPill = new GameObject("CoinPill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _victoryCoinPill.transform.SetParent(_victoryPanel.transform, false);
            var coinRect = _victoryCoinPill.GetComponent<RectTransform>();
            coinRect.anchorMin = new Vector2(0.5f, 0.5f);
            coinRect.anchorMax = new Vector2(0.5f, 0.5f);
            coinRect.pivot = new Vector2(0.5f, 0.5f);
            coinRect.sizeDelta = new Vector2(300f, 64f);
            coinRect.anchoredPosition = new Vector2(0f, -170f);
            var coinBg = _victoryCoinPill.GetComponent<Image>();
            coinBg.color = new Color(1f, 0.96f, 0.86f, 1f);
            coinBg.raycastTarget = false;
            var coinOutline = _victoryCoinPill.AddComponent<Outline>();
            coinOutline.effectColor = new Color(0.95f, 0.70f, 0.20f, 1f);
            coinOutline.effectDistance = new Vector2(2f, -2f);

            var coinIconGO = new GameObject("CoinIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            coinIconGO.transform.SetParent(_victoryCoinPill.transform, false);
            var coinIconRect = coinIconGO.GetComponent<RectTransform>();
            coinIconRect.anchorMin = new Vector2(0f, 0.5f);
            coinIconRect.anchorMax = new Vector2(0f, 0.5f);
            coinIconRect.pivot = new Vector2(0.5f, 0.5f);
            coinIconRect.sizeDelta = new Vector2(44f, 44f);
            coinIconRect.anchoredPosition = new Vector2(36f, 0f);
            var coinIcon = coinIconGO.GetComponent<Image>();
            coinIcon.sprite = Resources.Load<Sprite>("UI/coin");
            coinIcon.preserveAspect = true;
            coinIcon.raycastTarget = false;

            var coinTxtGO = new GameObject("CoinText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            coinTxtGO.transform.SetParent(_victoryCoinPill.transform, false);
            var coinTxtRect = coinTxtGO.GetComponent<RectTransform>();
            coinTxtRect.anchorMin = Vector2.zero;
            coinTxtRect.anchorMax = Vector2.one;
            coinTxtRect.offsetMin = new Vector2(70f, 0f);
            coinTxtRect.offsetMax = new Vector2(-10f, 0f);
            _victoryCoinText = coinTxtGO.GetComponent<TextMeshProUGUI>();
            _victoryCoinText.font = tmpFont;
            _victoryCoinText.text = "+0";
            _victoryCoinText.fontSize = 34;
            _victoryCoinText.fontStyle = FontStyles.Bold;
            _victoryCoinText.alignment = TextAlignmentOptions.MidlineLeft;
            _victoryCoinText.color = new Color(0.55f, 0.32f, 0.08f, 1f);
            _victoryCoinText.raycastTarget = false;

            // 6) Boutons jeu : Continuer Jelly vert + icône, Menu en lien texte discret
            // (même langage que la modale d'échec, fini les rectangles plats).
            var btnGO = new GameObject("BtnContinuer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            btnGO.transform.SetParent(_victoryPanel.transform, false);
            var btnRect2 = btnGO.GetComponent<RectTransform>();
            btnRect2.anchorMin = new Vector2(0.5f, 0.05f);
            btnRect2.anchorMax = new Vector2(0.5f, 0.05f);
            btnRect2.pivot = new Vector2(0.5f, 0.5f);
            btnRect2.sizeDelta = new Vector2(400f, 78f);
            btnRect2.anchoredPosition = new Vector2(-95f, 10f);

            var btnImg = btnGO.GetComponent<Image>();
            var contNormal = JellyUI.ButtonGreen;
            var contHover = JellyUI.ButtonYellow ?? contNormal;
            var contPressed = JellyUI.ButtonRed ?? contNormal;
            var contDisabled = JellyUI.ButtonGrey ?? contNormal;
            btnImg.sprite = contNormal;
            btnImg.type = Image.Type.Sliced;
            btnImg.pixelsPerUnitMultiplier = 1f;

            var btnComp = btnGO.AddComponent<Button>();
            JellyUI.ApplyJellyButton(btnComp, btnImg, contNormal, contHover, contPressed, contDisabled);
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

            var btnContentGO = new GameObject("Content", typeof(RectTransform), typeof(CanvasRenderer));
            btnContentGO.transform.SetParent(btnGO.transform, false);
            var btnContentRect = btnContentGO.GetComponent<RectTransform>();
            btnContentRect.anchorMin = Vector2.zero;
            btnContentRect.anchorMax = Vector2.one;
            btnContentRect.offsetMin = new Vector2(14f, 6f);
            btnContentRect.offsetMax = new Vector2(-14f, -6f);
            var btnHLG = btnContentGO.AddComponent<HorizontalLayoutGroup>();
            btnHLG.spacing = 10f;
            btnHLG.childAlignment = TextAnchor.MiddleCenter;
            btnHLG.childForceExpandWidth = false;
            btnHLG.childControlWidth = false;
            var btnIconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            btnIconGO.transform.SetParent(btnContentGO.transform, false);
            var btnIconLE = btnIconGO.AddComponent<LayoutElement>();
            btnIconLE.preferredWidth = 42f;
            btnIconLE.preferredHeight = 42f;
            var btnIconImg = btnIconGO.GetComponent<Image>();
            btnIconImg.sprite = Resources.Load<Sprite>("UI/play_button");
            btnIconImg.preserveAspect = true;
            btnIconImg.raycastTarget = false;

            var btnTxtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            btnTxtGO.transform.SetParent(btnContentGO.transform, false);
            var btnTxtLE = btnTxtGO.AddComponent<LayoutElement>();
            btnTxtLE.flexibleWidth = 1f;

            var btnTxt = btnTxtGO.GetComponent<TextMeshProUGUI>();
            btnTxt.font = tmpFont;
            btnTxt.text = Zoologic.Localization.LocalizationManager.Get("victory.continue");
            btnTxt.fontSize = 32;
            btnTxt.fontStyle = FontStyles.Bold;
            btnTxt.color = Color.white;
            btnTxt.alignment = TextAlignmentOptions.Center;
            btnTxt.raycastTarget = false;
            btnTxt.enableAutoSizing = true;
            btnTxt.fontSizeMin = 22;
            btnTxt.fontSizeMax = 32;
            var btnShadow = btnTxtGO.AddComponent<Shadow>();
            btnShadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
            btnShadow.effectDistance = new Vector2(0f, -2f);

            var menuGO = new GameObject("BtnMenu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            menuGO.transform.SetParent(_victoryPanel.transform, false);
            var menuRect = menuGO.GetComponent<RectTransform>();
            menuRect.anchorMin = new Vector2(0.5f, 0.05f);
            menuRect.anchorMax = new Vector2(0.5f, 0.05f);
            menuRect.pivot = new Vector2(0.5f, 0.5f);
            menuRect.sizeDelta = new Vector2(150f, 78f);
            menuRect.anchoredPosition = new Vector2(195f, 10f);
            var menuImg = menuGO.GetComponent<Image>();
            var menuNormal = JellyUI.SmallGrey;
            var menuHover = JellyUI.SmallYellow ?? menuNormal;
            var menuPressed = JellyUI.SmallRed ?? menuNormal;
            menuImg.sprite = menuNormal;
            menuImg.type = Image.Type.Sliced;
            menuImg.pixelsPerUnitMultiplier = 1f;
            var menuBtn = menuGO.AddComponent<Button>();
            JellyUI.ApplyJellyButton(menuBtn, menuImg, menuNormal, menuHover, menuPressed, menuNormal);
            menuBtn.onClick.AddListener(() =>
            {
                SFXManager.Instance.PlayMenuClose();
                Canvas c = FindFirstObjectByType<Canvas>();
                IsDailyPuzzle = false;
                SceneFader.FadeOut(this, c, 0.3f,
                    () => UnityEngine.SceneManagement.SceneManager.LoadScene("LevelMap"));
            });
            var menuContentGO = new GameObject("Content", typeof(RectTransform), typeof(CanvasRenderer));
            menuContentGO.transform.SetParent(menuGO.transform, false);
            var menuContentRect = menuContentGO.GetComponent<RectTransform>();
            menuContentRect.anchorMin = Vector2.zero;
            menuContentRect.anchorMax = Vector2.one;
            menuContentRect.offsetMin = new Vector2(10f, 6f);
            menuContentRect.offsetMax = new Vector2(-10f, -6f);
            var menuHLG = menuContentGO.AddComponent<HorizontalLayoutGroup>();
            menuHLG.spacing = 8f;
            menuHLG.childAlignment = TextAnchor.MiddleCenter;
            menuHLG.childForceExpandWidth = false;
            menuHLG.childControlWidth = false;
            var menuIconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            menuIconGO.transform.SetParent(menuContentGO.transform, false);
            var menuIconLE = menuIconGO.AddComponent<LayoutElement>();
            menuIconLE.preferredWidth = 34f;
            menuIconLE.preferredHeight = 34f;
            var menuIconImg = menuIconGO.GetComponent<Image>();
            menuIconImg.sprite = Resources.Load<Sprite>("UI/Icons/home_pixi") ?? Resources.Load<Sprite>("UI/Icons/back");
            menuIconImg.preserveAspect = true;
            menuIconImg.color = Color.white;
            menuIconImg.raycastTarget = false;
            var menuTxtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            menuTxtGO.transform.SetParent(menuContentGO.transform, false);
            var menuTxtLE = menuTxtGO.AddComponent<LayoutElement>();
            menuTxtLE.flexibleWidth = 1f;
            var menuTxt = menuTxtGO.GetComponent<TextMeshProUGUI>();
            menuTxt.font = tmpFont;
            menuTxt.text = Zoologic.Localization.LocalizationManager.Get("victory.menu");
            menuTxt.fontSize = 22;
            menuTxt.fontStyle = FontStyles.Bold;
            menuTxt.color = Color.white;
            menuTxt.alignment = TextAlignmentOptions.Center;
            menuTxt.raycastTarget = false;
            menuTxt.enableAutoSizing = true;
            menuTxt.fontSizeMin = 16;
            menuTxt.fontSizeMax = 22;
            menuTxt.outlineWidth = 0.18f;
            menuTxt.outlineColor = new Color(0f, 0f, 0f, 0.40f);
            var menuTxtShadow = menuTxtGO.AddComponent<Shadow>();
            menuTxtShadow.effectColor = new Color(0f, 0f, 0f, 0.30f);
            menuTxtShadow.effectDistance = new Vector2(0f, -2f);
        }

        private void DisableParasiteText(Canvas canvas)
        {
            foreach (var t in canvas.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
                if (t.text == "Place les animaux") t.gameObject.SetActive(false);
            foreach (var t in canvas.GetComponentsInChildren<Text>(true))
                if (t.text == "Place les animaux") t.gameObject.SetActive(false);
        }

        private void ReorderCanvasHierarchy(Canvas canvas)
        {
            Transform ct = canvas.transform;
            Transform dock = ct.Find("DockBois") ?? ct.Find("FooterWave") ?? ct.Find("DockContainer") ?? ct.Find("BottomPanel");
            Transform grid = _gridView != null ? _gridView.BoardContainer?.transform : null;
            Transform reset = ct.Find("ResetButton") ?? ct.Find("GommeBouton");
            if (dock != null)
            {
                var rt = dock as RectTransform;
                if (rt != null)
                {
                    float bInset = _hud != null ? _hud.BottomInset : 18f;
                    float y = Mathf.Max(bInset, 48f);
                    rt.anchorMin = new Vector2(0.5f, 0f);
                    rt.anchorMax = new Vector2(0.5f, 0f);
                    rt.pivot = new Vector2(0.5f, 0f);
                    rt.sizeDelta = new Vector2(0f, 70f);
                    rt.anchoredPosition = new Vector2(0f, y);
                    var img = dock.GetComponent<Image>();
                    if (img != null) DestroyImmediate(img);
                    var lbl = dock.GetComponentInChildren<TextMeshProUGUI>();
                    if (lbl == null)
                    {
                        var t = dock.Find("FooterLabel");
                        if (t != null) lbl = t.GetComponent<TextMeshProUGUI>();
                    }
                }
            }
            if (reset != null)
            {
                var rt = reset as RectTransform;
                if (rt != null)
                {
                    float bInset = _hud != null ? _hud.BottomInset : 18f;
                    float y = Mathf.Max(bInset, 48f);
                    rt.anchorMin = new Vector2(1f, 0f);
                    rt.anchorMax = new Vector2(1f, 0f);
                    rt.pivot = new Vector2(1f, 0f);
                    rt.sizeDelta = new Vector2(96f, 96f);
                    rt.anchoredPosition = new Vector2(-48f, y + 24f);
                    var img = reset.GetComponent<Image>();
                    if (img != null)
                    {
                        var s = Resources.LoadAll<Sprite>("Sprites").FirstOrDefault(x => x.name == "b_11") ?? Resources.Load<Sprite>("Sprites/b_11");
                        if (s != null) { img.sprite = s; img.color = Color.white; }
                        else { img.sprite = null; img.color = new Color(0.92f, 0.36f, 0.42f, 1f); }
                        img.type = Image.Type.Simple;
                        img.preserveAspect = true;
                    }
                    var sh = reset.GetComponent<Shadow>();
                    if (sh == null) sh = reset.gameObject.AddComponent<Shadow>();
                    sh.effectColor = new Color(0f, 0f, 0f, 0.25f);
                    sh.effectDistance = new Vector2(0f, -4f);
                }
            }
            if (grid != null && dock != null && reset != null)
            {
                dock.SetAsLastSibling();
                reset.SetAsLastSibling();
            }
            else if (grid != null && dock != null)
            {
                dock.SetAsLastSibling();
            }
            else if (reset != null) reset.SetAsLastSibling();
        }
    }
}
