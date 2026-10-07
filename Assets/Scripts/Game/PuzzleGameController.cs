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
namespace Zoologic{
    /// <summary>    /// Contrôleur principal du jeu jouable :    ///  - génère un niveau via <see cref="LevelGenerator"/> au démarrage ;
    ///  - gère les taps (placer / retirer un pion) et les appuis longs (marquer un "X") ;
    ///  - détecte la victoire via <see cref="RuleValidator.IsSolved"/> ;
    ///  - gère les vies (<see cref="LivesManager"/>), le score et le panneau de défaite
    ///    /// Le câblage est entièrement automatique (aucune configuration dans l'Inspector) :    /// ce composant crée lui-même le canvas, l'EventSystem, le HUD et le texte de    /// victoire s'ils n'existent pas déjà.    /// </summary>
    public sealed class PuzzleGameController : MonoBehaviour
    {
        [SerializeField] private int _numeroNiveau = 1;

        public static int SelectedLevel = 1;
        public static bool IsDailyPuzzle = false;

        private PuzzleGrid _grid;
        private bool[,] _xMarks;
        private GridView _gridView;
        private bool _victoryShown;
        private bool _victoryAdDecided; // anti double-déclenchement pub/victoire (animation-end + Continuer)
        private Coroutine _victoryAnimation;

        private GameHUD _hud;
        private LivesManager _livesManager;
        private bool _partieTerminee;
        // Guidage intégré au niveau 1 (remplace l'ancienne scène Tutorial).
        private const string GuidedKey = "guided_level1_done";
        private bool _guideActive;
        private readonly HashSet<(int row, int col)> _guideCells = new HashSet<(int row, int col)>();
        private bool _guideAllowTray = true;
        private bool _guideAllowPawnDrag = true;
        private UIHandPointer _guideHand;
        private GameObject _guideBubble;
        private TextMeshProUGUI _guideBubbleText;
        private GameObject _guideSpotRoot;
        private Image _guideSpotFrame;
        // Phase 2 : niveau 1 assisté jusqu'à la victoire (zéro défaite possible).
        private bool _guidePhase2;
        private bool _guidePhase2Placed;
        private float _guideIdleTime;
        private System.Collections.Generic.List<(int row, int col)> _guideSolution;
        public static bool GuidedDone()        {
            return PlayerPrefs.GetInt(GuidedKey, 0) == 1;
        }
        public static void ResetGuided()        {
            PlayerPrefs.DeleteKey(GuidedKey);
            PlayerPrefs.Save();
        }
        private static void MarkGuidedDone()        {
            PlayerPrefs.SetInt(GuidedKey, 1);
            PlayerPrefs.Save();
            AnalyticsManager.LogTutorialComplete();
        }
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
        // Spec modale victoire : indices utilisés pour les étoiles (tolérance familles).
        private int _hintsUsedThisLevel;
        // Boutons victoire : verrouillés jusqu'à T+1550ms, pulse ensuite (90% CTR Suivant).
        private Button _victoryNextBtn;
        private Button _victoryRewardBtn;
        private GameObject _victoryRewardRow;
        private Image _victoryRewardBg;
        private TextMeshProUGUI _victoryRewardText;
        private Coroutine _victoryPulseRoutine;
        private CanvasGroup _victoryOverlayGroup;

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

        // Inventaire du niveau : l'animal glissé EST l'animal posé.
        private System.Collections.Generic.List<AnimalIconSet.MoodSet> _levelSets;

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
            _hintsUsedThisLevel = 0;
            _moveCount = 0;
            _totalPenaliteCumul = 0;

            SFXManager.Instance.ResumeMusic();

            Canvas canvas = null;
            try            {
                canvas = EnsureCanvas();
                EnsureEventSystem();
                CreateBackground(canvas);
            }
            catch (System.Exception e)            {
                Debug.LogError("[Zoologic] Start: échec de l'initialisation de l'environnement UI.\n" + e);
                return;
            }
            try            {
                _hud = gameObject.AddComponent<GameHUD>();
                _hud.Build(canvas, _numeroNiveau);
                _hud.OnReessayer = ReinitialiserNiveau;
                _hud.OnIndiceDemande = DemanderIndice;
                _hud.OnGommeDemande = UtiliserGomme;
                _hud.OnPubViesDemande = HandlePubVies;
                _hud.OnViePayanteDemande = HandleAchatVie;
            }
            catch (System.Exception e)            {
                Debug.LogError("[Zoologic] Start: échec de la construction du HUD.\n" + e);
            }
            try            {
                _grid = GenerateLevel();
                _xMarks = new bool[_grid.Size, _grid.Size];
            }
            catch (System.Exception e)            {
                Debug.LogError("[Zoologic] Start: échec de la génération du niveau.\n" + e);
                return;
            }
            try            {
                _gridView.OnCellTapped = HandleCellTapped;
                _gridView.Build(_grid, (RectTransform)canvas.transform);

                if (_gridView.BoardContainer != null)
                    _gridView.BoardContainer.anchoredPosition =
                        new Vector2(0f, _hud.BoardYOffset);

                _hud.SetProgression(_grid.Pions.Count, _grid.Size);
                ReorderCanvasHierarchy(canvas);
                SetupDragAndDrop(canvas);
                AnalyticsManager.LogLevelStart(IsDailyPuzzle ? 0 : _numeroNiveau, _grid.Size, IsDailyPuzzle);
            }
            catch (System.Exception e)            {
                Debug.LogError("[Zoologic] Start: échec de la construction de la grille.\n" + e);
            }
            try            {
                _hud.CreerPanneauDefaite(canvas, _numeroNiveau, IsDailyPuzzle);
            }
            catch (System.Exception e)            {
                Debug.LogError("[Zoologic] Start: échec de la création du panneau de défaite.\n" + e);
            }
            try            {
                CreateVictoryPanel(canvas);
                if (_victoryRoot != null)
                    _victoryRoot.SetActive(false);
            }
            catch (System.Exception e)            {
                Debug.LogError("[Zoologic] Start: échec de la création du panneau de victoire.\n" + e);
            }
            try            {
                _livesManager = new LivesManager();
                _livesManager.OnPartiePerdue = GererPartiePerdue;
                _hud.SetVies(_livesManager.Vies);
            }
            catch (System.Exception e)            {
                Debug.LogError("[Zoologic] Start: échec de l'initialisation du gestionnaire de vies.\n" + e);
            }
            try            {
                if (_numeroNiveau == 1 && !IsDailyPuzzle && !GuidedDone())                    StartCoroutine(GuidedLevel1Routine(canvas));
            }
            catch (System.Exception e)            {
                Debug.LogError("[Zoologic] Start: échec du guidage niveau 1.\n" + e);
            }

            SceneFader.FadeIn(this, canvas, 0.35f);
        }
        private void Update()        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame ?? false)
            {
                if (SettingsPanel.HandleBackButton()) return;
                ShowQuitConfirmation();
            }
            if (_guidePhase2 && !_victoryShown && !_partieTerminee)            {
                _guideIdleTime += Time.unscaledDeltaTime;
                if (_guideIdleTime >= 10f)                {
                    _guideIdleTime = 0f;
                    SuggestGuideCell();
                }
            }
            else            {
                _guideIdleTime = 0f;
            }
        }
        /// <summary>Phase 2 : pulse discret sur une case solution vide après 10s.</summary>
        private void SuggestGuideCell()
        {
            try            {
                if (_guideSolution == null || _grid == null || _gridView == null)                    return;
                foreach (var (row, col) in _guideSolution)                {
                    if (!_grid.HasPion(row, col))                    {
                        Canvas canvas = FindFirstObjectByType<Canvas>();
                        if (canvas == null) return;
                        StartCoroutine(GuideIdlePulseRoutine(canvas, _gridView.GetCellRect(row, col)));
                        return;
                    }
                }
            }
            catch {
            }
        }
        /// <summary>
        /// Chien de garde anti old-input, persistant sur toutes les scènes :
        /// détruit tout StandaloneInputModule résiduel (scène périmée, plugin)
        /// et garantit le module Input System sur l'EventSystem courant.
        /// Source de l'InvalidOperationException sous "Input System (New)".
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void StartInputWatchdog()
        {
            EnsureInputWatchdog();
        }

        private static void EnsureInputWatchdog()
        {
            var go = new GameObject("InputWatchdog");
            go.AddComponent<InputWatchdog>();
            DontDestroyOnLoad(go);
        }
        private sealed class InputWatchdog : MonoBehaviour        {
            private int _frames;
            private void OnEnable()            {
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnScene;
            }
            private void OnDisable()            {
                UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnScene;
            }
            private void OnScene(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)            {
                SweepNow();
            }
            private void Update()            {
                if ((++_frames & 31) != 0)                    return;
                SweepNow();
            }
            private static void SweepNow()            {
                try                {
                    foreach (var m in UnityEngine.Object.FindObjectsByType<StandaloneInputModule>(FindObjectsSortMode.None))                    {
                        Debug.LogWarning("[Zoologic] StandaloneInputModule résiduel détruit : " + m.gameObject.name);
                        DestroyImmediate(m);
                    }
                    var current = EventSystem.current;
                    if (current != null && current.GetComponent<InputSystemUIInputModule>() == null)                        current.gameObject.AddComponent<InputSystemUIInputModule>();
                }
                catch {
                }
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
        // G├®n├®ration du niveau.
        // ------------------------------------------------------------------

        private PuzzleGrid GenerateLevel()
        {
            if (IsDailyPuzzle)
                return GenererNiveauResilient(DailyPuzzleManager.GetTodaySeed(), DailyPuzzleManager.GetTodaySize(), 3);

            int size = Core.LevelConfig.GetGridSize(_numeroNiveau);
            int difficulty = Core.LevelConfig.GetTargetDifficulty(_numeroNiveau);
            return GenererNiveauResilient(_numeroNiveau, size, difficulty);
        }

        /// <summary>
        /// Génération résiliente, utilisée par le jeu et le QA Gate.
        /// Étage 1 : pipeline historique exact (seed de base, paramètres par défaut)
        /// → les niveaux qui passaient gardent leur grille au bit près.
        /// Étage 2 (échec étage 1 uniquement) : seeds dérivés + paramètres secours
        /// (7x7 et 8x8) sous borne temps → sauve les niveaux morts.
        /// Ne retourne jamais null : dernier recours = grille brute (jouable,
        /// unicité non garantie) avec erreur loggée.
        /// </summary>
        public static PuzzleGrid GenererNiveauResilient(int seedBase, int size, int targetDifficulty)
        {
            // Étage 1 : historique exact.
            try
            {
                var legacy = new LevelGenerator(seed: seedBase);
                PuzzleGrid g = null;
                try {
                    g = legacy.GenerateLevel(size, targetDifficulty);
                }
                catch (Exception) {
                }
                if (g == null)                    g = legacy.GenerateUniqueGrid(size);
                if (g != null)                    return g;
            }
            catch (Exception) {
            }
            // Étage 2a : 8x8 → banque offline validée (instantané, unicité garantie).
            if (size >= 8)
            {
                PuzzleGrid bank = PickBank8x8(seedBase, targetDifficulty);
                if (bank != null)                {
                    Debug.LogWarning("[Zoologic] GenererNiveauResilient: secours banque 8x8 utilisé (seed=" + seedBase + ").");
                    return bank;
                }
                Debug.LogError("[Zoologic] GenererNiveauResilient: banque 8x8 indisponible (seed=" + seedBase + ") - repli seeds derives.");
            }
            // Étage 2b : seeds dérivés, paramètres secours sur grandes grilles.
            var sw = System.Diagnostics.Stopwatch.StartNew();
            const long budgetMs = 2000;
            PuzzleGrid meilleure = null;
            int meilleurEcart = int.MaxValue;
            int essai = 0;
            while (sw.ElapsedMilliseconds < budgetMs && essai < 40)            {
                int seed = unchecked(seedBase * 100003 + essai * 7919 + 17);
                var gen = size >= 7                    ? new LevelGenerator(seed: seed, tailleMaxFacteur: 1.6, voisinage4Dir: true, serpentFacteur: 1.3, maxTentativesUnicite: 150)                    : new LevelGenerator(seed: seed);
                PuzzleGrid candidate = null;
                try {
                    candidate = gen.GenerateUniqueGrid(size);
                }
                catch (Exception) {
                    essai++;
                    continue;
                }
                int ecart = Math.Abs(DifficultyScorer.ScoreDifficulty(candidate) - targetDifficulty);
                if (ecart < meilleurEcart)                {
                    meilleurEcart = ecart;
                    meilleure = candidate;
                }
                if (ecart == 0)                    return candidate;
                essai++;
            }
            if (meilleure != null)            {
                Debug.LogWarning("[Zoologic] GenererNiveauResilient: secours seed dérivé utilisé (seed=" + seedBase + ", " + size + "x" + size + ").");
                return meilleure;
            }
            Debug.LogError("[Zoologic] GenererNiveauResilient: échec total (seed=" + seedBase + ", " + size + "x" + size + ") — grille brute non-unique en dernier recours.");
            return new LevelGenerator(seed: unchecked(seedBase + 999999)).GenerateRawGrid(size);
        }
        private static System.Collections.Generic.List<(int score, int[,] zones)> _bank8x8;
        /// <summary>        /// Choisit une grille 8x8 dans la banque offline validée        /// (Assets/Resources/Bank8x8.txt) : meilleur score vs difficulté cible,        /// ex-aequo départagés par le seed (déterministe par niveau).        /// Retourne null si la banque est absente ou illisible.
        /// </summary>
        private static PuzzleGrid PickBank8x8(int seedBase, int targetDifficulty)
        {
            if (_bank8x8 == null)                _bank8x8 = ChargerBanque8x8();
            if (_bank8x8 == null || _bank8x8.Count == 0)                return null;
            int meilleurEcart = int.MaxValue;
            foreach (var e in _bank8x8)                meilleurEcart = Math.Min(meilleurEcart, Math.Abs(e.score - targetDifficulty));
            var candidats = new System.Collections.Generic.List<int>();
            for (int i = 0; i < _bank8x8.Count; i++)                if (Math.Abs(_bank8x8[i].score - targetDifficulty) == meilleurEcart)                    candidats.Add(i);
            int choix = candidats[Math.Abs(seedBase % candidats.Count)];
            return new PuzzleGrid(_bank8x8[choix].zones);
        }
        private static System.Collections.Generic.List<(int score, int[,] zones)> ChargerBanque8x8()        {
            var resultat = new System.Collections.Generic.List<(int score, int[,] zones)>();
            try            {
                var asset = Resources.Load<TextAsset>("Bank8x8");
                if (asset == null || string.IsNullOrEmpty(asset.text))                {
                    Debug.LogError("[Zoologic] Banque 8x8 introuvable (Resources/Bank8x8).");
                    return resultat;
                }
                foreach (string brut in asset.text.Split(new[] {
                    '\n' }
                , StringSplitOptions.RemoveEmptyEntries))                {
                    string ligne = brut.Trim();
                    if (ligne.Length == 0 || ligne[0] == '#')                        continue;
                    if (ligne.Length != 66 || ligne[1] != ':' || ligne[0] < '1' || ligne[0] > '3')                        continue;
                    var zones = new int[8, 8];
                    bool ok = true;
                    for (int i = 0; i < 64; i++)                    {
                        char c = ligne[2 + i];
                        if (c < '0' || c > '7') {
                            ok = false;
                            break;
                        }
                        zones[i / 8, i % 8] = c - '0';
                    }
                    if (ok)                        resultat.Add((ligne[0] - '0', zones));
                }
            }
            catch (Exception e)            {
                Debug.LogError("[Zoologic] Lecture banque 8x8 impossible : " + e.GetType().Name + " - " + e.Message);
            }
            return resultat;
        }
        // Interactions — tap = X brouillon, drag = poser / déplacer / retirer.
        /// <summary>
        /// Aucun conflit/score/vie n'est vérifié.        /// </summary>
        private void HandleCellTapped(int row, int col)
        {
            if (_partieTerminee || _victoryShown)
                return;
            if (_guideActive && !_guideCells.Contains((row, col)))                return;
            if (_guidePhase2) NoteGuideGesture();
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
        /// <summary>        /// Pose un pion (drop depuis la barre) : consomme SON jeton, l'animal        /// glissé EST l'animal posé. Conflits, score et victoire ici.
        /// </summary>
        private void PlacePionAt(int row, int col, AnimalIconSet.MoodSet set, bool countMission = true)
        {
            if (_partieTerminee || _victoryShown || _grid.HasPion(row, col))
                return;
            if (!set.IsComplete)                return;
            if (_hud != null && !_hud.ConsumeTraySet(set))                return;
            if (_guideActive || _guidePhase2)            {
                // Niveau 1 : aucun coup erroné (ni vie perdue) possible.
                if (_grid.Pions.Count >= _grid.Size)
                {
                    if (_hud != null) _hud.ReturnTraySet(set);
                    return;
                }
                var pre = RuleValidator.GetConflicts(_grid, row, col);
                if (pre.Count > 0)                {
                    if (_hud != null) _hud.ReturnTraySet(set);
                    _gridView.ShakeBoard(12f, 0.35f);
                    _hud.NotifierConflit(pre[0]);
                    _gridView.FlashConflict(row, col);
                    foreach (var (r, c) in RuleValidator.GetConflictingCells(_grid, row, col))                        _gridView.FlashConflict(r, c);
                    return;
                }
            }
            _grid.PlacePion(row, col);
            _xMarks[row, col] = false;
            _gridView.SetCellMoodSet(row, col, set);
            _gridView.SetPion(row, col, true);
            _gridView.SetX(row, col, false);
            // Arrivée heureuse (swap seul, la chute joue) + triste différé si conflit.
            _gridView.SetPawnMood(row, col, AnimalIconSet.PawnMood.Happy, animate: false);
            _placeTimes[(row, col)] = Time.unscaledTime;
            StartCoroutine(DelayedSadCheckRoutine(row, col));
            _moveCount++;
            _hud.SetProgression(_grid.Pions.Count, _grid.Size);
            _hud.SetMoves(_moveCount);
            if (_guidePhase2 && !_guidePhase2Placed)            {
                // 1er placement phase 2 : la guidance persistante a rempli son rôle.
                _guidePhase2Placed = true;
                HideGuideBubble();
                HideGuideSpotlight();
            }
            if (countMission)
                MissionManager.AddProgress(MissionType.PlaceAnimals, 1);
            VerifierConflitPlacement(row, col);
            RefreshConflicts();
            UpdateVictoryVisibility();
        }
        /// <summary>        /// Retire un pion (drop hors plateau) : rend SON jeton à la barre.        /// </summary>
        private void RemovePionAt(int row, int col)
        {
            if (_partieTerminee || _victoryShown || !_grid.HasPion(row, col))
                return;
            AnimalIconSet.MoodSet set = _gridView.GetCellMoodSet(row, col);
            _grid.RemovePion(row, col);
            _gridView.SetPion(row, col, false);
            if (_hud != null)                _hud.ReturnTraySet(set);
            _hud.SetProgression(_grid.Pions.Count, _grid.Size);
            ReevaluerConflits();
            RefreshConflicts();
            UpdateVictoryVisibility();
        }
        /// <summary>        /// Déplace un pion (drag pion → autre case libre) : l'animal voyage        /// avec lui. Une seule évaluation.        /// </summary>
        private void MovePionAt(int fromRow, int fromCol, int toRow, int toCol)
        {
            if (_partieTerminee || _victoryShown)
                return;
            if (!_grid.HasPion(fromRow, fromCol) || _grid.HasPion(toRow, toCol))
                return;
            AnimalIconSet.MoodSet set = _gridView.GetCellMoodSet(fromRow, fromCol);
            if (!set.IsComplete)                return;
            if (_guideActive || _guidePhase2)            {
                // Move guidé : conflits ignorés depuis l'origine (modèle intact).
                bool conflit = false;
                foreach (var p in _grid.Pions)                {
                    if (p.row == fromRow && p.col == fromCol) continue;
                    if (SontEnConflit((toRow, toCol), p)) {
                        conflit = true;
                        break;
                    }
                }
                if (conflit)                {
                    _gridView.ShakeBoard(12f, 0.35f);
                    _gridView.FlashConflict(toRow, toCol);
                    Haptics.VibrateLight();
                    return;
                }
            }
            _grid.RemovePion(fromRow, fromCol);
            _gridView.SetPion(fromRow, fromCol, false);
            _grid.PlacePion(toRow, toCol);
            _xMarks[toRow, toCol] = false;
            _gridView.SetCellMoodSet(toRow, toCol, set);
            _gridView.SetPion(toRow, toCol, true);
            _gridView.SetX(toRow, toCol, false);
            _gridView.SetPawnMood(toRow, toCol, AnimalIconSet.PawnMood.Happy, animate: false);
            _placeTimes[(toRow, toCol)] = Time.unscaledTime;
            StartCoroutine(DelayedSadCheckRoutine(toRow, toCol));
            _moveCount++;
            _hud.SetProgression(_grid.Pions.Count, _grid.Size);
            _hud.SetMoves(_moveCount);
            VerifierConflitPlacement(toRow, toCol);
            RefreshConflicts();
            UpdateVictoryVisibility();
        }
        private void DemanderIndice()        {
            if (_partieTerminee)                return;
            // Moins de 5 ans : indices illimités (pas de pub, pas de pression).
            if (!AdMobManager.AreAdsAllowed())
            {
                MontrerIndice();
                return;
            }
            // Stock épuisé : une pub rewarded recharge +1 puis montre l'indice.
            if (HintStockManager.Get() <= 0)
            {
                var admob = AdMobManager.Instance;
                System.Action grant = () =>            {
                    HintStockManager.Add(1);
                    _hud.SetIndiceStock(HintStockManager.Get());
                    AnalyticsManager.LogAdReward("hints");
                    MontrerIndice();
                }
                ;
                if (admob != null && admob.IsRewardedReady())                    admob.ShowRewarded(grant, () => _hud.NotifierPubIndisponible());
                else                    _hud.NotifierPubIndisponible();
                return;
            }
            MontrerIndice();
        }
        /// <summary>        /// Affiche l'indice et consomme 1 du stock global (sauf moins de 5 ans).        /// Plus d'achat en pièces.        /// </summary>
        private void MontrerIndice()
        {
            bool success = _gridView.RequestHint();

            if (!success)            {
                _hud.NotifierIndiceIndisponible();
                return;
            }
            _hintsUsedThisLevel++; // spec étoiles : tolérance familles sur les indices
            _hud.NotifierIndiceAffiche();
            AnalyticsManager.LogHintUsed(IsDailyPuzzle ? 0 : _numeroNiveau);
            if (AdMobManager.AreAdsAllowed())            {
                HintStockManager.Consume();
                _hud.SetIndiceStock(HintStockManager.Get());
            }
            MissionManager.AddProgress(MissionType.UseHints, 1);
        }
        /// <summary>        /// Utilise le power-up « gomme » (gratuit) : retire tous les pions        /// actuellement en conflit (les pions valides sont conservés).        /// </summary>
        private void UtiliserGomme()
        {
            if (_partieTerminee)                return;
            var pions = new List<(int row, int col)>(_grid.Pions);
            var enConflit = new HashSet<(int row, int col)>();
            for (int i = 0; i < pions.Count; i++)            {
                for (int j = i + 1; j < pions.Count; j++)                {
                    if (SontEnConflit(pions[i], pions[j]))                    {
                        enConflit.Add(pions[i]);
                        enConflit.Add(pions[j]);
                    }
                }
            }
            if (enConflit.Count == 0)            {
                _hud.NotifierAucuneCible();
                return;
            }
            SFXManager.Instance.PlayClickedOut();
            foreach ((int row, int col) in enConflit)            {
                AnimalIconSet.MoodSet set = _gridView.GetCellMoodSet(row, col);
                _grid.RemovePion(row, col);
                _gridView.SetPion(row, col, false);
                if (_hud != null)                    _hud.ReturnTraySet(set);
            }
            _hud.SetProgression(_grid.Pions.Count, _grid.Size);
            MissionManager.AddProgress(MissionType.UseEraser, 1);
            _gridView.ShakeBoard(20f, 0.3f);
            _hud.BloquerPowerUpTemporairement(GommeRecharge);
            RefreshConflicts();
            UpdateVictoryVisibility();
        }
        private void HandlePubVies()        {
            if (_livesManager == null || _hud == null) return;
            var admob = AdMobManager.Instance;
                System.Action grant = () =>            {
                    _livesManager.AjouterVies(LivesManager.MaxVies);
                    AnalyticsManager.LogAdReward("lives");
                    _hud.SetVies(_livesManager.Vies);
                _hud.CacherDefaite();
                _partieTerminee = false;
                SFXManager.Instance.ResumeMusic();
                _hud.BloquerInteractions(false);
            }
            ;
            // Families: no reward without a real ad view.
            if (admob != null && admob.IsRewardedReady()) admob.ShowRewarded(grant, () => _hud.NotifierPubIndisponible());
            else _hud.NotifierPubIndisponible();
        }
        /// <summary>        /// Achat dépannage : 1 vie contre des pièces (modale d'échec). La modale        /// reste ouverte : Réessayer se réactive et le joueur peut retenter.        /// </summary>
        private void HandleAchatVie()
        {
            if (_livesManager == null || _hud == null) return;
            if (_livesManager.Vies >= LivesManager.MaxVies) return;
            if (!CurrencyManager.SpendCoins(ViePayanteCout))            {
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
            if (conflits.Count == 0)            {
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
            // Explication : badge conflit seul (toast "Même ligne !" + shake + flash).
            // Pas de modal ici : règles déjà expliquées au N1 première install,
            // modal uniquement sur demande via bouton "?" (sinon spam à chaque erreur).
            _hud.NotifierConflit(conflits[0]);
            _gridView.FlashConflict(row, col);
            foreach (var (r, c) in RuleValidator.GetConflictingCells(_grid, row, col))                _gridView.FlashConflict(r, c);
            Haptics.VibrateLight();
            int nouveauScore = Mathf.Max(0, ScoreDepart - _totalPenaliteCumul * ScorePenaliteConflit);
            _hud.SetScore(nouveauScore);
        }
        private void ReevaluerConflits()        {
            int nouveauScore = Mathf.Max(0, ScoreDepart - _totalPenaliteCumul * ScorePenaliteConflit);
            _hud.SetScore(nouveauScore);
        }
        private readonly System.Collections.Generic.Dictionary<(int row, int col), float> _placeTimes =            new System.Collections.Generic.Dictionary<(int row, int col), float>();
        /// <summary>        /// Triste différé 0.35s (la chute se lit d'abord) avec revalidation :        /// pion encore là ET toujours en conflit, sinon annulé (drag rapide).        /// </summary>
        private System.Collections.IEnumerator DelayedSadCheckRoutine(int row, int col)
        {
            yield return new WaitForSecondsRealtime(0.35f);
            if (_partieTerminee || _grid == null || _gridView == null)                yield break;
            if (!_grid.HasPion(row, col))                yield break;
            var set = GetPionsEnConflit();
            if (set.Contains((row, col)))                _gridView.SetPawnMood(row, col, AnimalIconSet.PawnMood.Sad);
        }
        /// <summary>        /// Recalcule les anneaux de conflit persistants après une mutation.        /// Ignore les pions posés il y a moins de 0.4s (le différé les gère).        /// Les survivants résolus redeviennent heureux (micro-pop).        /// </summary>
        private void RefreshConflicts()
        {
            if (_grid == null || _gridView == null)                return;
            var set = GetPionsEnConflit();
            _gridView.RefreshConflictMarks(set);
            float now = Time.unscaledTime;
            foreach (var (row, col) in _grid.Pions)            {
                if (_placeTimes.TryGetValue((row, col), out float placedAt) && now - placedAt < 0.4f)                    continue;
                _gridView.SetPawnMood(row, col,                    set.Contains((row, col)) ? AnimalIconSet.PawnMood.Sad : AnimalIconSet.PawnMood.Happy);
            }
        }
        private HashSet<(int row, int col)> GetPionsEnConflit()        {
            var set = new HashSet<(int row, int col)>();
            if (_grid == null)                return set;
            var pions = new List<(int row, int col)>(_grid.Pions);
            for (int i = 0; i < pions.Count; i++)            {
                for (int j = i + 1; j < pions.Count; j++)                {
                    if (SontEnConflit(pions[i], pions[j]))                    {
                        set.Add(pions[i]);
                        set.Add(pions[j]);
                    }
                }
            }
            return set;
        }
        private int CompterPionsEnConflit(List<(int row, int col)> pions)        {
            var conflits = new HashSet<(int row, int col)>();
            for (int i = 0; i < pions.Count; i++)            {
                for (int j = i + 1; j < pions.Count; j++)                {
                    if (SontEnConflit(pions[i], pions[j]))                    {
                        conflits.Add(pions[i]);
                        conflits.Add(pions[j]);
                    }
                }
            }
            return conflits.Count;
        }
        private void FlashAllConflicts()        {
            var pions = new List<(int row, int col)>(_grid.Pions);
            var conflits = new HashSet<(int row, int col)>();
            for (int i = 0; i < pions.Count; i++)            {
                for (int j = i + 1; j < pions.Count; j++)                {
                    if (SontEnConflit(pions[i], pions[j]))                    {
                        conflits.Add(pions[i]);
                        conflits.Add(pions[j]);
                    }
                }
            }
            foreach ((int row, int col) in conflits)                _gridView.FlashConflict(row, col);
            if (conflits.Count > 0)                Haptics.VibrateLight();
        }
        private bool SontEnConflit((int row, int col) a, (int row, int col) b)        {
            return a.row == b.row                || a.col == b.col                || _grid.GetRegionId(a.row, a.col) == _grid.GetRegionId(b.row, b.col)                || (Mathf.Abs(a.row - b.row) == 1 && Mathf.Abs(a.col - b.col) == 1);
        }
        // ------------------------------------------------------------------        // Défaite / Réinitialisation.        // ------------------------------------------------------------------        /// <summary>        /// Câble le drag &amp;
        /// drop : barre d'animaux, déplacement et retrait des pions.
        /// </summary>
        private void SetupDragAndDrop(Canvas canvas)
        {
            if (_drag == null)                _drag = BoardDragController.Create(canvas, _gridView);
            else                _drag.SetGridView(_gridView);
            _drag.CanPlaceAt = (r, c) => !_grid.HasPion(r, c);
            _drag.OnTrayDropOnCell = (r, c, set) =>            {
                if (_partieTerminee || _victoryShown) {
                    _drag.Cancel();
                    return;
                }
                if (_guideActive && (!_guideAllowTray || !_guideCells.Contains((r, c)))) return;
                if (_guidePhase2)                {
                    NoteGuideGesture();
                    if (!GuideValidateDrop(r, c, -1, -1, false)) return;
                }
                PlacePionAt(r, c, set);
            }
            ;
            _drag.OnTrayDropInvalid = (r, c) =>            {
                if (_guideActive && !_guideAllowTray) return;
                _gridView.ShakeBoard(12f, 0.2f);
                SFXManager.Instance.PlayDialogueBlip();
            }
            ;
            _drag.OnPawnMove = (fr, fc, tr, tc) =>            {
                if (_partieTerminee || _victoryShown) {
                    _drag.Cancel();
                    return;
                }
                if (_guideActive) return;
                if (_guidePhase2)                {
                    NoteGuideGesture();
                    if (!GuideValidateDrop(tr, tc, fr, fc, true)) return;
                }
                MovePionAt(fr, fc, tr, tc);
            }
            ;
            _drag.OnPawnDropInvalid = (r, c) =>            {
                _gridView.FlashConflict(r, c);
                Haptics.VibrateLight();
            }
            ;
            _drag.OnPawnDropOutside = (r, c) =>            {
                if (_partieTerminee || _victoryShown) {
                    _drag.Cancel();
                    return;
                }
                if (_guideActive && (!_guideAllowPawnDrag || !_guideCells.Contains((r, c)))) return;
                if (_guidePhase2) NoteGuideGesture();
                RemovePionAt(r, c);
            }
            ;
            _gridView.OnPawnDragStart = (r, c, e) =>            {
                if (_partieTerminee || _victoryShown)                    return;
                if (_guideActive && (!_guideAllowPawnDrag || !_guideCells.Contains((r, c))))                    return;
                if (_guidePhase2) NoteGuideGesture();
                AnimalIconSet.MoodSet set = _gridView.GetCellMoodSet(r, c);
                if (!set.IsComplete)                    return;
                _drag.BeginPawnDrag(r, c, set, e.pointerId);
                _drag.UpdateDrag(e.pointerId, e.position);
            }
            ;
            _gridView.OnPawnDrag = (r, c, e) =>            {
                if (_drag == null || !_drag.IsDragging)                    return;
                _drag.UpdateDrag(e.pointerId, e.position);
            }
            ;
            _gridView.OnPawnDragEnd = (r, c, e) =>            {
                if (_drag == null || !_drag.IsDragging)                    return;
                _drag.EndDrag(e.pointerId, e.position);
            }
            ;
            _levelSets = SkinManager.GetLevelMoodSets(_grid.Size);
            if (_hud != null)                _hud.RebuildAnimalTray(_levelSets, _drag);
        }
        private void GererPartiePerdue()        {
            AnalyticsManager.LogLevelEnd(IsDailyPuzzle ? 0 : _numeroNiveau, false);
            if (_drag != null)                _drag.Cancel();
            _partieTerminee = true;
            SFXManager.Instance.PauseMusic();
            _hud.BloquerInteractions(true);
            _hud.AfficherDefaite();
        }
        private void ReinitialiserNiveau()        {
            if (LivesManager.GetStoredLives() <= 0)            {
                _hud.NotifierViesEpuisees();
                return;
            }
            _partieTerminee = false;
            SFXManager.Instance.ResumeMusic();
            _conflictsThisLevel = 0;
            _hintsUsedThisLevel = 0;
            _moveCount = 0;
            _placeTimes.Clear();
            _totalPenaliteCumul = 0;
            if (_drag != null)                _drag.Cancel();
            _hud.CacherDefaite();
            HideVictory();
            int livesForRetry = LivesManager.GetStoredLives();
            _hud.Reinitialiser(ScoreDepart, livesForRetry, _hud.IndiceCount);
            _hud.SetProgression(0, _grid.Size);
            _hud.SetMoves(0);
            _hud.ResetTray();
            RefreshConflicts();
            _grid.Clear();
            for (int row = 0; row < _grid.Size; row++)            {
                for (int col = 0; col < _grid.Size; col++)                {
                    _xMarks[row, col] = false;
                    _gridView.SetPion(row, col, false);
                    _gridView.SetX(row, col, false);
                }
            }
            _victoryShown = false;
        }
        // ------------------------------------------------------------------        // Guidage intégré au niveau 1 (3 gestes forcés sur la vraie grille).        // Règles/score/vies inchangés : la démo n'utilise que des placements        // directs (sans validation) et se termine par ReinitialiserNiveau.        // ------------------------------------------------------------------
        private IEnumerator GuidedLevel1Routine(Canvas canvas)
        {
            AnalyticsManager.LogTutorialBegin();
            _guideActive = true;
            _guideCells.Clear();
            if (_levelSets == null || _levelSets.Count == 0)            {
                EndGuide(canvas);
                yield break;
            }
            var solver = new PuzzleSolver();
            List<List<(int row, int col)>> solutions = null;
            try {
                solutions = solver.FindAllSolutions(_grid, 1);
            }
            catch {
                solutions = null;
            }
            if (solutions == null || solutions.Count == 0 || solutions[0].Count == 0)            {
                EndGuide(canvas);
                yield break;
            }
            var solution = new HashSet<(int row, int col)>(solutions[0]);
            _guideSolution = solutions[0];
            _guideHand = UIHandPointer.Create(canvas);
            // Étape 1 : glisser un jeton vers une case solution.
            var target = solutions[0][0];
            _guideCells.Clear();
            _guideCells.Add(target);
            _guideAllowTray = true;
            _guideAllowPawnDrag = false;
            ShowGuideBubble(Zoologic.Localization.LocalizationManager.Get("tutorial.double_tap"));
            ShowGuideSpotlight(canvas, _gridView.GetCellRect(target.row, target.col));
            _guideHand.PlayDragFromTo(TrayChipRect(), _gridView.GetCellRect(target.row, target.col));
            yield return new WaitUntil(() => !_guideActive || _grid.HasPion(target.row, target.col));
            if (!_guideActive) yield break;
            _guideHand.Hide();
            HideGuideBubble();
            HideGuideSpotlight();
            yield return new WaitForSecondsRealtime(0.4f);
            // Étape 2 : tap X sur une case hors solution.
            (int row, int col) xCell = (-1, -1);
            for (int r = 0; r < _grid.Size && xCell.row < 0; r++)            {
                for (int c = 0; c < _grid.Size; c++)                {
                    if (!solution.Contains((r, c)) && !_grid.HasPion(r, c))                    {
                        xCell = (r, c);
                        break;
                    }
                }
            }
            if (xCell.row >= 0)            {
                _guideCells.Clear();
                _guideCells.Add(xCell);
                _guideAllowTray = false;
                _guideAllowPawnDrag = false;
                ShowGuideBubble(Zoologic.Localization.LocalizationManager.Get("tutorial.tap_x"));
                ShowGuideSpotlight(canvas, _gridView.GetCellRect(xCell.row, xCell.col));
                _guideHand.PointTo(_gridView.GetCellRect(xCell.row, xCell.col));
                yield return new WaitUntil(() => !_guideActive || _xMarks[xCell.row, xCell.col] || _grid.HasPion(xCell.row, xCell.col));
                if (!_guideActive) yield break;
                _guideHand.PlayTap();
                _guideHand.Hide();
                HideGuideBubble();
                HideGuideSpotlight();
                yield return new WaitForSecondsRealtime(0.4f);
            }
            // Étape 3 : démo conflit (auto, jamais sur une case X) puis retrait.
            (int row, int col) demo = (-1, -1);
            for (int r = 0; r < _grid.Size && demo.row < 0; r++)            {
                for (int c = 0; c < _grid.Size; c++)                {
                    if (!solution.Contains((r, c)) && !_grid.HasPion(r, c)                        && !_xMarks[r, c]                        && RuleValidator.GetConflicts(_grid, r, c).Count > 0)                    {
                        demo = (r, c);
                        break;
                    }
                }
            }
            if (demo.row >= 0 && _levelSets != null && _levelSets.Count > 0)            {
                // Beat "regarde" : spotlight + pause avant l'auto-pose.
                ShowGuideBubble(Zoologic.Localization.LocalizationManager.Get("guided.watch"));
                ShowGuideSpotlight(canvas, _gridView.GetCellRect(demo.row, demo.col));
                yield return new WaitForSecondsRealtime(1.0f);
                if (!_guideActive) yield break;
                var conflicts = RuleValidator.GetConflicts(_grid, demo.row, demo.col);
                if (_hud != null) _hud.ConsumeTraySet(_levelSets[0]);
                _grid.PlacePion(demo.row, demo.col);
                _gridView.SetCellMoodSet(demo.row, demo.col, _levelSets[0]);
                _gridView.SetPion(demo.row, demo.col, true);
                _gridView.SetPawnMood(demo.row, demo.col, AnimalIconSet.PawnMood.Sad);
                _gridView.FlashConflict(demo.row, demo.col);
                foreach (var (r, c) in RuleValidator.GetConflictingCells(_grid, demo.row, demo.col))                    _gridView.FlashConflict(r, c);
                if (conflicts.Count > 0)                    _hud.NotifierConflit(conflicts[0]);
                ShowGuideBubble(Zoologic.Localization.LocalizationManager.Get("tutorial.tap_removes"));
                _guideCells.Clear();
                _guideCells.Add(demo);
                _guideAllowTray = false;
                _guideAllowPawnDrag = true;
                _guideHand.PlayDragToLocal(                    _gridView.GetCellRect(demo.row, demo.col),                    GuideOffBoardLocal(canvas, _gridView.GetCellRect(demo.row, demo.col)));
                yield return new WaitUntil(() => !_guideActive || !_grid.HasPion(demo.row, demo.col));
                if (!_guideActive) yield break;
                _guideHand.Hide();
                HideGuideBubble();
                HideGuideSpotlight();
            }
            // Phase 2 : on garde tout (pions, croix), protection jusqu'à victoire.
            // Guidance persistante (bulle + spot) jusqu'au 1er placement.
            _guideActive = false;
            _guideCells.Clear();
            _guideAllowTray = true;
            _guideAllowPawnDrag = true;
            _guidePhase2 = true;
            _guidePhase2Placed = false;
            _guideIdleTime = 0f;
            ShowGuideBubble(Zoologic.Localization.LocalizationManager.Get("guided.your_turn"));
            ShowGuideSpotlight(canvas, FirstEmptySolutionCell());
        }
        /// <summary>        /// Phase 2 : valide un drop avant pose (zéro vie perdue possible).        /// Retourne false si le drop est refusé (avec feedback).
        /// </summary>
        private bool GuideValidateDrop(int row, int col, int skipRow, int skipCol, bool hasSkip)
        {
            var types = RuleValidator.GetConflicts(_grid, row, col);
            if (types.Count == 0)                return true;
            if (hasSkip)            {
                var cells = RuleValidator.GetConflictingCells(_grid, row, col);
                bool onlySkipped = true;
                foreach (var cell in cells)                {
                    if (cell.row != skipRow || cell.col != skipCol)                    {
                        onlySkipped = false;
                        break;
                    }
                }
                if (onlySkipped)                    return true;
            }
            _gridView.ShakeBoard(12f, 0.35f);
            _hud.NotifierConflit(types[0]);
            _gridView.FlashConflict(row, col);
            foreach (var (r, c) in RuleValidator.GetConflictingCells(_grid, row, col))                _gridView.FlashConflict(r, c);
            Haptics.VibrateLight();
            return false;
        }
        /// <summary>Un geste vient d'avoir lieu : reset le timer d'aide + cache le pulse (phase 2 uniquement).</summary>
        private void NoteGuideGesture()
        {
            if (!_guidePhase2)                return;
            _guideIdleTime = 0f;
            HideGuideSpotlight();
        }
        private System.Collections.IEnumerator GuideIdlePulseRoutine(Canvas canvas, RectTransform cellRect)        {
            ShowGuideSpotlight(canvas, cellRect);
            yield return new WaitForSecondsRealtime(2.5f);
            HideGuideSpotlight();
        }
        /// <summary>Première case solution vide (spotlight phase 2).</summary>
        private RectTransform FirstEmptySolutionCell()
        {
            try            {
                if (_guideSolution != null && _grid != null && _gridView != null)                {
                    foreach (var (row, col) in _guideSolution)                    {
                        if (!_grid.HasPion(row, col))                            return _gridView.GetCellRect(row, col);
                    }
                }
            }
            catch {
            }
            return null;
        }
        private void EndGuide(Canvas canvas)        {
            _guideActive = false;
            _guideCells.Clear();
            _guideAllowTray = true;
            _guideAllowPawnDrag = true;
            HideGuideBubble();
            HideGuideSpotlight();
            if (_guideHand != null)            {
                _guideHand.Hide();
                try {
                    Destroy(_guideHand.gameObject);
                }
                catch {
                }
                _guideHand = null;
            }
        }
        private RectTransform TrayChipRect()        {
            try            {
                if (_hud != null && _hud.AnimalTray != null)                    return _hud.AnimalTray.GetChipRect(0);
            }
            catch {
            }
            return null;
        }
        private Vector2 GuideOffBoardLocal(Canvas canvas, RectTransform cellRect)        {
            try            {
                var board = _gridView != null ? _gridView.BoardContainer : null;
                Vector3 world = cellRect.TransformPoint(cellRect.rect.center);
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(canvas != null ? canvas.worldCamera : null, world);
                Vector2 cellLocal;
                RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, screen, canvas != null ? canvas.worldCamera : null, out cellLocal);
                float bottom = cellLocal.y - 380f;
                if (board != null)                {
                    Vector3[] corners = new Vector3[4];
                    board.GetWorldCorners(corners);
                    Vector2 cornerScreen = RectTransformUtility.WorldToScreenPoint(canvas != null ? canvas.worldCamera : null, corners[0]);
                    Vector2 cornerLocal;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, cornerScreen, canvas != null ? canvas.worldCamera : null, out cornerLocal))                        bottom = cornerLocal.y - 140f;
                }
                return new Vector2(cellLocal.x, bottom);
            }
            catch {
                return new Vector2(0f, -500f);
            }
        }
        private void ShowGuideBubble(string text)        {
            try            {
                Canvas canvas = FindFirstObjectByType<Canvas>();
                if (canvas == null) return;
                if (_guideBubble == null)                {
                    _guideBubble = new GameObject("GuideBubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    _guideBubble.transform.SetParent(canvas.transform, false);
                    var rt = (RectTransform)_guideBubble.transform;
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(860f, 120f);
                    rt.anchoredPosition = new Vector2(0f, 620f);
                    var img = _guideBubble.GetComponent<Image>();
                    img.sprite = B1UI.Bubble;
                    img.type = Image.Type.Sliced;
                    img.color = new Color(1f, 0.985f, 0.95f, 1f);
                    img.raycastTarget = false;
                    var sh = _guideBubble.AddComponent<Shadow>();
                    sh.effectColor = new Color(0.25f, 0.15f, 0.08f, 0.30f);
                    sh.effectDistance = new Vector2(0f, -8f);
                    var txtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                    txtGO.transform.SetParent(_guideBubble.transform, false);
                    var txtRect = (RectTransform)txtGO.transform;
                    txtRect.anchorMin = Vector2.zero;
                    txtRect.anchorMax = Vector2.one;
                    txtRect.offsetMin = new Vector2(150f, 14f);
                    txtRect.offsetMax = new Vector2(-24f, -14f);
                    _guideBubbleText = txtGO.GetComponent<TextMeshProUGUI>();
                    _guideBubbleText.font = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Bold SDF");
                    _guideBubbleText.fontSize = 34;
                    _guideBubbleText.fontStyle = FontStyles.Bold;
                    _guideBubbleText.color = new Color(0.29f, 0.18f, 0.10f, 1f);
                    _guideBubbleText.alignment = TextAlignmentOptions.MidlineLeft;
                    _guideBubbleText.textWrappingMode = TextWrappingModes.Normal;
                    _guideBubbleText.raycastTarget = false;
                    var owlGO = new GameObject("Mascot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    owlGO.transform.SetParent(_guideBubble.transform, false);
                    var owlRect = (RectTransform)owlGO.transform;
                    owlRect.anchorMin = new Vector2(0f, 0.5f);
                    owlRect.anchorMax = new Vector2(0f, 0.5f);
                    owlRect.pivot = new Vector2(0.5f, 0.5f);
                    owlRect.sizeDelta = new Vector2(100f, 100f);
                    owlRect.anchoredPosition = new Vector2(70f, 0f);
                    var owlImg = owlGO.GetComponent<Image>();
                    owlImg.sprite = Resources.Load<Sprite>("Art/Animals/owl");
                    owlImg.preserveAspect = true;
                    owlImg.raycastTarget = false;
                    Zoologic.Localization.LocalizationManager.ApplyTo(_guideBubbleText);
                }
                _guideBubbleText.text = text;
                _guideBubble.SetActive(true);
                _guideBubble.transform.SetAsLastSibling();
                if (_guideHand != null)                    _guideHand.transform.SetAsLastSibling();
            }
            catch (System.Exception e)            {
                Debug.LogWarning("[Zoologic] GuideBubble: " + e.Message);
            }
        }
        private void HideGuideBubble()        {
            if (_guideBubble != null)                _guideBubble.SetActive(false);
        }
        /// <summary>        /// Spotlight 100% espace plateau (aucune conversion écran) : 4 panneaux        /// sombres enfants du plateau autour du trou + cadre doré sur la case.        /// Panneaux non-bloquants (le tactile reste géré par le gating).        /// </summary>
        private void ShowGuideSpotlight(Canvas canvas, RectTransform cellRect)
        {
            try            {
                if (cellRect == null || _gridView == null)                    return;
                var board = _gridView.BoardContainer;
                if (board == null)                    return;
                // Coordonnées layout finales (stables même pendant les pops d'entrée).
                Vector2 bSize = board.sizeDelta;
                Vector2 cPos = cellRect.anchoredPosition;
                Vector2 cSize = cellRect.sizeDelta + new Vector2(20f, 20f);
                float bx = bSize.x * 0.5f;
                float by = bSize.y * 0.5f;
                float cx0 = cPos.x - cSize.x * 0.5f;
                float cx1 = cPos.x + cSize.x * 0.5f;
                float cy0 = cPos.y - cSize.y * 0.5f;
                float cy1 = cPos.y + cSize.y * 0.5f;
                if (_guideSpotRoot == null)                {
                    _guideSpotRoot = new GameObject("GuideSpot", typeof(RectTransform));
                    _guideSpotRoot.transform.SetParent(board, false);
                    var rootRect = (RectTransform)_guideSpotRoot.transform;
                    rootRect.anchorMin = new Vector2(0.5f, 0.5f);
                    rootRect.anchorMax = new Vector2(0.5f, 0.5f);
                    rootRect.pivot = new Vector2(0.5f, 0.5f);
                    rootRect.sizeDelta = Vector2.zero;
                    rootRect.anchoredPosition = Vector2.zero;
                    for (int i = 0; i < 4; i++)                    {
                        var p = new GameObject("Dim" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                        p.transform.SetParent(_guideSpotRoot.transform, false);
                        var img = p.GetComponent<Image>();
                        img.sprite = GridView.SharedRoundedRect;
                        img.type = Image.Type.Simple;
                        img.color = new Color(0.10f, 0.07f, 0.05f, 0.45f);
                        img.raycastTarget = false;
                    }
                    var fGO = new GameObject("Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    fGO.transform.SetParent(_guideSpotRoot.transform, false);
                    _guideSpotFrame = fGO.GetComponent<Image>();
                    _guideSpotFrame.sprite = GridView.SharedRing;
                    _guideSpotFrame.type = Image.Type.Simple;
                    _guideSpotFrame.color = new Color(1f, 0.82f, 0.18f, 1f);
                    _guideSpotFrame.raycastTarget = false;
                }
                if (_guideSpotRoot.transform.parent != board)                    _guideSpotRoot.transform.SetParent(board, false);
                // Panneaux : haut / bas / gauche / droite autour du trou.
                SetGuideDim(0, new Vector2(-bx, cy1), new Vector2(bx, by));
                SetGuideDim(1, new Vector2(-bx, -by), new Vector2(bx, cy0));
                SetGuideDim(2, new Vector2(-bx, cy0), new Vector2(cx0, cy1));
                SetGuideDim(3, new Vector2(cx1, cy0), new Vector2(bx, cy1));
                var frameRect = (RectTransform)_guideSpotFrame.transform;
                frameRect.anchorMin = new Vector2(0.5f, 0.5f);
                frameRect.anchorMax = new Vector2(0.5f, 0.5f);
                frameRect.pivot = new Vector2(0.5f, 0.5f);
                frameRect.sizeDelta = cSize + new Vector2(16f, 16f);
                frameRect.anchoredPosition = cPos;
                _guideSpotRoot.SetActive(true);
                _guideSpotRoot.transform.SetAsLastSibling();
            }
            catch (System.Exception e)            {
                Debug.LogWarning("[Zoologic] GuideSpot: " + e.Message);
            }
        }
        private void SetGuideDim(int index, Vector2 min, Vector2 max)        {
            if (_guideSpotRoot == null)                return;
            var t = _guideSpotRoot.transform.Find("Dim" + index) as RectTransform;
            if (t == null)                return;
            t.anchorMin = new Vector2(0.5f, 0.5f);
            t.anchorMax = new Vector2(0.5f, 0.5f);
            t.pivot = new Vector2(0.5f, 0.5f);
            t.sizeDelta = new Vector2(Mathf.Max(0f, max.x - min.x), Mathf.Max(0f, max.y - min.y));
            t.anchoredPosition = (min + max) * 0.5f;
        }
        private void HideGuideSpotlight()        {
            if (_guideSpotRoot != null)                _guideSpotRoot.SetActive(false);
        }
        // ------------------------------------------------------------------
        // Victoire.
        // ------------------------------------------------------------------
        private void UpdateVictoryVisibility()
        {
            bool solved = RuleValidator.IsSolved(_grid);
            if (solved == _victoryShown)                return;
            _victoryShown = solved;
            if (solved)            {
                if (_guidePhase2 && _numeroNiveau == 1)                {
                    MarkGuidedDone();
                    _guidePhase2 = false;
                    HideGuideBubble();
                    HideGuideSpotlight();
                    if (_guideHand != null)                    {
                        _guideHand.Hide();
                        try {
                            Destroy(_guideHand.gameObject);
                        }
                        catch {
                        }
                        _guideHand = null;
                    }
                }
                PlayVictory();
            }
            else                HideVictory();
        }
        private void PlayVictory()        {
            AnalyticsManager.LogLevelEnd(IsDailyPuzzle ? 0 : _numeroNiveau, true);
            SFXManager.Instance.PauseMusic();
            SFXManager.Instance.PlaySuccess();
            _victoryPerfect = false;
            _victoryAdDecided = false;
            if (IsDailyPuzzle)            {
                _victoryStarsEarned = 3;
                _victoryCoinReward = DailyPuzzleManager.RewardCoins;
                if (!DailyPuzzleManager.IsCompletedToday())                {
                    DailyPuzzleManager.MarkCompletedToday();
                    int total = DailyPuzzleManager.RewardCoins + DailyPuzzleManager.GetStreakBonus();
                    CurrencyManager.AddCoins(total);
                    _hud.RefreshCoins();
                    _victoryCoinReward = total;
                }
            }
            else            {
                // Spec étoiles : 1 toujours, 2 si quasi-parfait (tolérance familles),
                // 3 = PARFAIT (0 conflit + 0 indice + coups == taille). Grandes
                // grilles 7x7/8x8 : tolérance 2 erreurs. Éco : 15 / 20 / 35 pièces.
                int errTol = _grid != null && _grid.Size >= 7 ? 2 : 1;
                bool noHint = _hintsUsedThisLevel == 0;
                bool perfect = _conflictsThisLevel == 0 && noHint && _moveCount == _grid.Size;
                int stars = perfect ? 3
                    : (_conflictsThisLevel <= errTol && _hintsUsedThisLevel <= 1) ? 2
                    : 1;
                LevelProgressManager.SetStars(_numeroNiveau, stars);
                LevelProgressManager.UnlockNextLevel(_numeroNiveau);
                MissionManager.AddProgress(MissionType.CompleteLevels, 1);
                MissionManager.AddProgress(MissionType.EarnStars, stars);
                int coinReward = CoinBaseReward + stars * CoinStarBonus;
                _victoryPerfect = perfect;
                if (_victoryPerfect)                    coinReward += PerfectBonus;
                CurrencyManager.AddCoins(coinReward);
                _hud.RefreshCoins();
                _victoryStarsEarned = stars;
                _victoryCoinReward = coinReward;
            }
            SetupVictoryRewardRow();
            SetVictoryButtonsEnabled(false);
            if (_victoryLevelText != null)                _victoryLevelText.text = IsDailyPuzzle ? Zoologic.Localization.LocalizationManager.Get("victory.daily_badge") : Zoologic.Localization.LocalizationManager.Get("victory.level_badge", _numeroNiveau);
            if (_victoryText != null)                _victoryText.text = IsDailyPuzzle ? Zoologic.Localization.LocalizationManager.Get("victory.daily_title") : Zoologic.Localization.LocalizationManager.Get("victory.title");
            if (_drag != null)                _drag.Cancel();
            if (_victoryPerfectBadge != null)                _victoryPerfectBadge.SetActive(_victoryPerfect);
            // Célébration : tous les pions heureux.
            if (_grid != null && _gridView != null)
            {
                foreach (var (row, col) in _grid.Pions)                    _gridView.SetPawnMood(row, col, AnimalIconSet.PawnMood.Happy, animate: false);
            }
            // Spec : pas de pluie ici (elle part à T+1150 dans la séquence + bursts
            // par étoile). Un seul déclencheur confettis = VictoryAnimationRoutine.
            if (_victoryPerfect)                Haptics.VibrateStrong();
            if (_victoryAnimation != null)                StopCoroutine(_victoryAnimation);
            _victoryAnimation = StartCoroutine(VictoryAnimationRoutine());
            if (_victoryGlowRoutine != null)                StopCoroutine(_victoryGlowRoutine);
            _victoryGlowRoutine = StartCoroutine(VictoryGlowRoutine());
        }
        /// <summary>
        /// Spec rewarded : secondaire bleu sans pulse, caché si -5 ans (zéro pub).
        /// Vies non pleines → +3 vies, sinon +1 indice (jamais de récompense inutile).
        /// </summary>
        private void SetupVictoryRewardRow()
        {
            bool adsAllowed = false;
            try { adsAllowed = AdMobManager.AreAdsAllowed(); } catch { adsAllowed = false; }
            bool livesMissing = false;
            bool hintMissing = false;
            try { livesMissing = LivesManager.GetStoredLives() < LivesManager.MaxVies; } catch { livesMissing = false; }
            try { hintMissing = HintStockManager.Get() < HintStockManager.MaxStock; } catch { hintMissing = false; }
            // Stock plein des deux côtés : rien à gagner → on cache la ligne
            // (jamais de pub regardée pour rien, plafond indices = 3).
            bool useful = adsAllowed && (livesMissing || hintMissing);
            if (_victoryRewardRow != null)
                _victoryRewardRow.SetActive(useful);
            if (!useful || _victoryRewardText == null)
                return;
            _victoryRewardText.text = Zoologic.Localization.LocalizationManager.Get(
                livesMissing ? "victory.reward_lives" : "victory.reward_hint");
        }

        /// <summary>Verrouille/déverrouille Suivant + rewarded (séquence T+1550ms).</summary>
        private void SetVictoryButtonsEnabled(bool enabled)
        {
            if (_victoryNextBtn != null)
                _victoryNextBtn.interactable = enabled;
            if (_victoryRewardBtn != null)
                _victoryRewardBtn.interactable = enabled && (_victoryRewardRow == null || _victoryRewardRow.activeSelf);
            if (_victoryRewardBg != null)
                _victoryRewardBg.color = enabled
                    ? new Color(0.89f, 0.94f, 1.00f, 1f)
                    : new Color(0.75f, 0.75f, 0.78f, 0.5f);
        }

        /// <summary>Récompense rewarded victoire : pub puis +3 vies ou +1 indice.</summary>
        private void GrantVictoryReward()
        {
            var admob = AdMobManager.Instance;
            if (admob == null || !admob.IsRewardedReady())
            {
                if (_hud != null) _hud.NotifierPubIndisponible();
                return;
            }
            // Garde anti-gaspillage (plafond stock 3) : pas de pub si rien à gagner.
            bool livesMissing = false;
            bool hintMissing = false;
            try { livesMissing = LivesManager.GetStoredLives() < LivesManager.MaxVies; } catch { }
            try { hintMissing = HintStockManager.Get() < HintStockManager.MaxStock; } catch { }
            if (!livesMissing && !hintMissing)
            {
                SetupVictoryRewardRow();
                return;
            }
            admob.ShowRewarded(() =>
            {
                bool gaveLife = false;
                try
                {
                    if (_livesManager != null && LivesManager.GetStoredLives() < LivesManager.MaxVies)
                    {
                        _livesManager.AjouterVies(LivesManager.MaxVies);
                        if (_hud != null) _hud.SetVies(_livesManager.Vies);
                        gaveLife = true;
                    }
                }
                catch { gaveLife = false; }
                if (!gaveLife)
                {
                    HintStockManager.Add(1);
                    if (_hud != null) _hud.SetIndiceStock(HintStockManager.Get());
                }
                AnalyticsManager.LogAdReward("victory");
                SFXManager.Instance.PlayUnlock();
                if (_victoryRewardRow != null)
                    Punch.Scale(this, (RectTransform)_victoryRewardRow.transform, 1.08f, 0.25f);
                SetupVictoryRewardRow();
            }, () => { if (_hud != null) _hud.NotifierPubIndisponible(); });
        }

        /// <summary>Pulse d'appel du CTA vedette (1.0 ↔ 1.04, 1200ms, infini).</summary>
        private IEnumerator VictoryPulseRoutine(RectTransform target)
        {
            while (true)
            {
                if (target == null) yield break;
                float t = (Mathf.Sin(Time.unscaledTime * (2f * Mathf.PI / 1.2f)) + 1f) * 0.5f;
                float s = Mathf.Lerp(1f, 1.04f, Easing.EaseInOutQuad(t));
                target.localScale = new Vector3(s, s, s);
                yield return null;
            }
        }

        /// <summary>Halo doré pulsé tant que la victoire est affichée.</summary>
        private IEnumerator VictoryGlowRoutine()
        {
            while (true)            {
                if (_victoryGlow == null)                    yield break;
                float t = (Mathf.Sin(Time.unscaledTime * 2.2f) + 1f) * 0.5f;
                var img = _victoryGlow.GetComponent<Image>();
                if (img != null)                    img.color = new Color(1f, 0.85f, 0.35f, Mathf.Lerp(0.28f, 0.45f, t));
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
            if (_radialGlowSprite != null)                return _radialGlowSprite;
            const int res = 256;
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            float center = (res - 1) * 0.5f;
            float radius = res * 0.5f;
            for (int y = 0; y < res; y++)            {
                for (int x = 0; x < res; x++)                {
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
        private void HideVictory()        {
            if (_victoryAnimation != null)            {
                StopCoroutine(_victoryAnimation);
                _victoryAnimation = null;
            }
            if (_victoryPulseRoutine != null)            {
                StopCoroutine(_victoryPulseRoutine);
                _victoryPulseRoutine = null;
            }
            if (_victoryGlowRoutine != null)            {
                StopCoroutine(_victoryGlowRoutine);
                _victoryGlowRoutine = null;
            }
            // Reset CTA pour la prochaine victoire (pulse, lock, overlay).
            if (_victoryNextBtn != null)
            {
                _victoryNextBtn.interactable = false;
                _victoryNextBtn.transform.localScale = Vector3.one;
            }
            if (_victoryRewardBtn != null)
                _victoryRewardBtn.interactable = false;
            if (_victoryOverlayGroup != null)
                _victoryOverlayGroup.alpha = 1f;
            if (_victoryRoot != null)                _victoryRoot.SetActive(false);
            if (_victoryText != null)            {
                _victoryText.rectTransform.anchoredPosition = _victoryTextBasePosition;
                _victoryText.color = VictoryTextColor;
            }
            if (_victoryOutline != null)                _victoryOutline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            // Cacher les stars (reset etat grise).
            for (int i = 0; i < 3; i++)
            {
                if (_victoryStarRoots[i] != null)                    _victoryStarRoots[i].SetActive(false);                if (_victoryStars[i] != null)                {
                    _victoryStars[i].sprite = GridView.StarGrey;                    _victoryStars[i].color = Color.white;                }
            }
            if (_victoryCoinText != null)                _victoryCoinText.text = "+0";            if (_victoryPerfectBadge != null)            {
                _victoryPerfectBadge.transform.localScale = Vector3.one;                _victoryPerfectBadge.SetActive(false);            }
            _victoryPerfect = false;            if (_victoryPanel != null)                _victoryPanel.transform.localScale = Vector3.one;
            // Réinitialiser le hibou
            if (_victoryOwl != null)
            {
                _victoryOwl.transform.localScale = Vector3.zero;                _victoryOwl.transform.localRotation = Quaternion.identity;            }
            _gridView.ResetVictoryZoom();        }
        // Spec : titre #4E342E (8.1:1 AAA sur crème).
        private static readonly Color VictoryTextColor = new Color(0.30f, 0.20f, 0.18f, 1f);        private IEnumerator VictoryAnimationRoutine()
        {
            // Spec 4 étapes, T0 = apparition. CTA verrouillés jusqu'à T+1550ms.
            float t0 = Time.unscaledTime;
            SetVictoryButtonsEnabled(false);
            if (_victoryPulseRoutine != null) { StopCoroutine(_victoryPulseRoutine); _victoryPulseRoutine = null; }
            _gridView.PlayVictoryZoom();
            Haptics.VibrateStrong();
            Canvas canvasFx = FindFirstObjectByType<Canvas>();
            // (1) Overlay fade 250ms + carte pop 0.7→1.0 380ms (delay 80ms, OutBack 1.7).
            if (_victoryOverlayGroup != null) { _victoryOverlayGroup.alpha = 0f; StartCoroutine(FadeCanvasGroup(_victoryOverlayGroup, 1f, 0.25f)); }
            if (_victoryRoot != null) _victoryRoot.SetActive(true);
            Zoologic.Localization.LocalizationManager.ApplyFontsToScene();
            if (_victoryPanel != null) StartCoroutine(CardPopRoutine(_victoryPanel.transform, 0.08f));
            if (_victoryCoinText != null) _victoryCoinText.text = "+0";
            StartCoroutine(VictoryTitleRoutine());
            // Hibou en parallèle (ne bloque plus la séquence).
            if (_victoryOwl != null) StartCoroutine(OwlVictoryRoutine());
            // (2) Étoiles cascade 550/850/1150ms : pop or 320ms + burst 12pts,
            // les autres restent grisées. (3) Pluie principale + badge parfait à la 3e.
            float[] starSlots = { 0.55f, 0.85f, 1.15f };
            for (int i = 0; i < 3; i++)
            {
                float waitSlot = starSlots[i] - (Time.unscaledTime - t0);
                if (waitSlot > 0f) yield return new WaitForSecondsRealtime(waitSlot);
                if (_victoryStars[i] == null || _victoryStarRoots[i] == null) continue;
                if (i < _victoryStarsEarned)
                {
                    Haptics.VibrateLight();
                    SFXManager.Instance.PlayUnlock();
                    StartCoroutine(StarPopRoutine(i));
                    if (canvasFx != null) ConfettiHelper.Burst(this, canvasFx, 12);
                }
                else
                {
                    _victoryStarRoots[i].SetActive(true);
                    _victoryStarRoots[i].transform.localScale = Vector3.one * 0.85f;
                    _victoryStars[i].sprite = GridView.StarGrey;
                    _victoryStars[i].color = Color.white;
                }
                if (i == 2)
                {
                    if (canvasFx != null) ConfettiHelper.Burst(this, canvasFx, _victoryPerfect ? 150 : 80);
                    if (_victoryPerfect)
                    {
                        if (canvasFx != null) ConfettiHelper.Burst(this, canvasFx, 40);
                        SFXManager.Instance.PlaySuccess();
                        if (_victoryPerfectBadge != null) StartCoroutine(PerfectPopRoutine());
                    }
                }
            }
            // (4) Activation CTA à T+1550ms + punch + pulse infini (seul bouton animé).
            {
                float waitEnable = 1.55f - (Time.unscaledTime - t0);
                if (waitEnable > 0f) yield return new WaitForSecondsRealtime(waitEnable);
            }
            SetVictoryButtonsEnabled(true);
            if (_victoryNextBtn != null)
            {
                Punch.Scale(this, (RectTransform)_victoryNextBtn.transform, 1.12f, 0.25f);
                if (_victoryPulseRoutine != null) StopCoroutine(_victoryPulseRoutine);
                _victoryPulseRoutine = StartCoroutine(VictoryPulseRoutine((RectTransform)_victoryNextBtn.transform));
            }
            // Compteur de pièces animé + punch de la pilule (pendant CTA actif).
            if (_victoryCoinText != null)
            {
                SFXManager.Instance.PlayConfirm();
                float cd = 0f;
                const float countDur = 0.6f;
                while (cd < countDur)                {
                    float ct = Mathf.Clamp01(cd / countDur);
                    int v = Mathf.RoundToInt(Mathf.Lerp(0f, _victoryCoinReward, Easing.EaseOutCubic(ct)));
                    _victoryCoinText.text = $"+{v}";
                    cd += Time.unscaledDeltaTime;
                    yield return null;
                }
                _victoryCoinText.text = $"+{_victoryCoinReward}";
                if (_victoryCoinPill != null)                    Punch.Scale(this, _victoryCoinPill.GetComponent<RectTransform>(), 1.15f, 0.25f);
                Haptics.VibrateLight();
            }
            // Envol des pièces gagnées vers l'icône pièces du HUD (hibou déjà joué
            // en parallèle au T0). Le joueur peut cliquer : gardes anti double-trigger.
            yield return StartCoroutine(VictoryCoinFlyRoutine());
            _victoryAnimation = null;
            // Interstitiel APRES la sequence (modale + pieces visibles),
            // jamais avant ni pendant (VictoryCoinFlyRoutine + 0.8s, jamais DailyPuzzle).
            // MODE B : déclencheur primaire (joueurs patients). Fallback au clic
            // Continuer si l'animation est interrompue (clic rapide) — voir _victoryAdDecided.
            if (_victoryShown && !IsDailyPuzzle && !_victoryAdDecided)
            {
                yield return new WaitForSecondsRealtime(0.8f);
                if (_victoryShown && !IsDailyPuzzle && !_victoryAdDecided)
                {
                    _victoryAdDecided = true;
                    AdMobManager.Instance?.ShowInterstitialIfNeeded(_numeroNiveau);
                }
            }
        }
        // Stars de victoire.
        private void CreateVictoryStars(Canvas canvas)        {
            Sprite starSprite = Resources.Load<Sprite>("UI/star");
            float starSize = 64f;
            float starSpacing = 16f;
            float totalW = 3f * starSize + 2f * starSpacing;
            for (int i = 0; i < 3; i++)            {
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
        /// <summary>Spec : overlay fade + carte pop + titre + badge = helpers séquence.</summary>
        private IEnumerator FadeCanvasGroup(CanvasGroup group, float target, float duration)
        {
            if (group == null) yield break;
            float start = group.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                group.alpha = Mathf.Lerp(start, target, Easing.EaseOutQuad(t));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            group.alpha = target;
        }

        private IEnumerator CardPopRoutine(Transform card, float delay)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            if (card == null) yield break;
            // Impact : punch léger + micro-shake de la carte.
            card.localScale = new Vector3(0.7f, 0.7f, 1f);
            const float duration = 0.38f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float s = 0.7f + 0.3f * Easing.EaseOutBack(t);
                card.localScale = new Vector3(s, s, 1f);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            card.localScale = Vector3.one;
            Punch.Scale(this, (RectTransform)card, 1.06f, 0.18f);
            Haptics.VibrateLight();
        }

        private IEnumerator VictoryTitleRoutine()
        {
            if (_victoryText == null) yield break;
            yield return new WaitForSecondsRealtime(0.15f);
            _victoryText.rectTransform.anchoredPosition =
                _victoryTextBasePosition - new Vector2(0f, VictoryTextSlide);
            _victoryText.color = new Color(VictoryTextColor.r, VictoryTextColor.g, VictoryTextColor.b, 0f);
            if (_victoryOutline != null) _victoryOutline.effectColor = new Color(0f, 0f, 0f, 0f);
            const float duration = 0.30f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = Easing.EaseOutBack(t);
                _victoryText.rectTransform.anchoredPosition =
                    _victoryTextBasePosition - new Vector2(0f, VictoryTextSlide * (1f - eased));
                _victoryText.color = new Color(VictoryTextColor.r, VictoryTextColor.g, VictoryTextColor.b, Mathf.Clamp01(eased));
                if (_victoryOutline != null) _victoryOutline.effectColor = new Color(0f, 0f, 0f, 0.8f * Mathf.Clamp01(eased));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            _victoryText.rectTransform.anchoredPosition = _victoryTextBasePosition;
            _victoryText.color = VictoryTextColor;
            if (_victoryOutline != null) _victoryOutline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        }

        private IEnumerator PerfectPopRoutine()
        {
            if (_victoryPerfectBadge == null) yield break;
            _victoryPerfectBadge.transform.localScale = Vector3.zero;
            Haptics.VibrateLight();
            const float duration = 0.30f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float s = Easing.EaseOutBack(t);
                _victoryPerfectBadge.transform.localScale = new Vector3(s, s, s);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            _victoryPerfectBadge.transform.localScale = Vector3.one;
        }

        private IEnumerator StarPopRoutine(int index)        {
            GameObject root = _victoryStarRoots[index];
            Image img = _victoryStars[index];
            Sprite gold = Resources.Load<Sprite>("UI/star");
            if (gold != null)                img.sprite = gold;
            root.SetActive(true);
            // Spec : 0 → 1.2 → 1.0 en 320ms (overshoot marqué, dopamine).
            img.color = new Color(1f, 1f, 1f, 0f);
            float elapsed = 0f;
            const float upDur = 0.18f;
            while (elapsed < upDur)
            {
                float t = Mathf.Clamp01(elapsed / upDur);
                float s = Mathf.Lerp(0f, 1.2f, Easing.EaseOutQuad(t));
                root.transform.localScale = new Vector3(s, s, s);
                img.color = new Color(1f, 1f, 1f, t);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            elapsed = 0f;
            const float downDur = 0.14f;
            while (elapsed < downDur)
            {
                float t = Mathf.Clamp01(elapsed / downDur);
                float s = Mathf.Lerp(1.2f, 1f, Easing.EaseOutBack(t));
                root.transform.localScale = new Vector3(s, s, s);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            root.transform.localScale = Vector3.one;
            img.color = Color.white;
        }
        private IEnumerator VictoryCoinFlyRoutine()        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            Transform from = _victoryCoinPill != null ? _victoryCoinPill.transform : _victoryPanel.transform;
            if (canvas == null || from == null) yield break;
            GameObject targetGO = GameObject.Find("CoinNombre") ?? GameObject.Find("EconomyPill") ?? GameObject.Find("CoinIcone");
            Vector3 targetWorld = targetGO != null ? targetGO.transform.position                : new Vector3(Screen.width * 0.5f, Screen.height - 120f, 0f);
            Vector3 startWorld = from.position;
            var flyRoot = new GameObject("VictoryCoinFly", typeof(RectTransform));
            flyRoot.transform.SetParent(canvas.transform, false);
            var flyRect = (RectTransform)flyRoot.transform;
            flyRect.anchorMin = Vector2.zero;
            flyRect.anchorMax = Vector2.one;
            flyRect.offsetMin = Vector2.zero;
            flyRect.offsetMax = Vector2.zero;
            flyRoot.transform.SetAsLastSibling();
            Sprite coinSprite = Resources.Load<Sprite>("UI/coin");
            const int n = 8;
            const float dur = 0.8f;
            const float stagger = 0.06f;
            var coins = new System.Collections.Generic.List<RectTransform>();
            for (int i = 0; i < n; i++)            {
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
            if (targetGO != null)                Punch.Scale(this, (RectTransform)targetGO.transform, 1.22f, 0.28f);
            if (_hud != null) _hud.RefreshCoins();
        }
        private IEnumerator VictoryFlyOne(RectTransform rt, Vector3 from, Vector3 to, float delay, float dur)        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            if (rt == null) yield break;
            rt.gameObject.SetActive(true);
            rt.position = from;
            float el = 0f;
            Vector3 ctrl = (from + to) * 0.5f + new Vector3(60f, 220f, 0f);
            while (el < dur)            {
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
        // Animation du hibou de victoire : rebond + oscillation joyeuse.
        private IEnumerator OwlVictoryRoutine()        {
            Transform owlT = _victoryOwl.transform;
            owlT.localScale = Vector3.zero;
            // Rebond EaseOutBack : 0 → 1
            float bounceDur = 0.35f;
            float elapsed = 0f;
            while (elapsed < bounceDur)            {
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
            for (int swing = 0; swing < 3; swing++)            {
                float dir = (swing % 2 == 0) ? 1f : -1f;
                float target = wobbleAmp * dir / (1 + swing * 0.5f);
                float start = owlT.localEulerAngles.z;
                if (start > 180f) start -= 360f;
                elapsed = 0f;
                while (elapsed < wobbleDur)                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / wobbleDur);
                    float angle = Mathf.Lerp(start, target, Easing.EaseOutCubic(t));
                    owlT.localRotation = Quaternion.Euler(0f, 0f, angle);
                    yield return null;
                }
            }
            owlT.localRotation = Quaternion.identity;
        }
        // Création de l'environnement UI (fallback si absent de la scène).
        private static Canvas EnsureCanvas()        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)                return canvas;
            var canvasGameObject = new GameObject(                "UICanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGameObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGameObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }
        private static void EnsureEventSystem()        {
            foreach (var m in UnityEngine.Object.FindObjectsByType<StandaloneInputModule>(FindObjectsSortMode.None)) DestroyImmediate(m);
            if (EventSystem.current != null)            {
                var cur = EventSystem.current;
                if (cur.GetComponent<InputSystemUIInputModule>() == null)                    cur.gameObject.AddComponent<InputSystemUIInputModule>();
                return;
            }
            var es = new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<InputSystemUIInputModule>();
        }
        private void CreateBackground(Canvas canvas)        {
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
            // Spec : dim #3E2723 à 65%, bloquant dès T0. Fade via CanvasGroup (séquence).
            rootImg.color = new Color(0.24f, 0.15f, 0.11f, 0.65f);
            rootImg.raycastTarget = true;
            _victoryOverlayGroup = _victoryRoot.GetComponent<CanvasGroup>();
            if (_victoryOverlayGroup == null)
                _victoryOverlayGroup = _victoryRoot.AddComponent<CanvasGroup>();
            // 2) Panneau centré blanc arrondi avec relief cartoon
            _victoryPanel = new GameObject("VictoryPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _victoryPanel.transform.SetParent(_victoryRoot.transform, false);
            var panelRect = _victoryPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            // Spec : carte 680x1020 (hiérarchie verticale pieces → rewarded →
            // Suivant → menu). Tient iPhone SE (1136px utiles).
            panelRect.sizeDelta = new Vector2(680f, 1020f);
            panelRect.anchoredPosition = Vector2.zero;
            var panelImg = _victoryPanel.GetComponent<Image>();
            if (B1UI.Bubble != null)            {
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
            if (owlSprite != null)            {
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
            // 5) Étoiles dans le panneau, sous le texte. Spec : 3x 110px, gap 26.
            Sprite starSprite = Resources.Load<Sprite>("UI/star");
            float starSize = 110f;
            float starSpacing = 26f;
            float totalW = 3f * starSize + 2f * starSpacing;
            for (int i = 0; i < 3; i++)            {
                var starObj = new GameObject($"Star{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                starObj.transform.SetParent(_victoryPanel.transform, false);
                var starRect = (RectTransform)starObj.transform;
                starRect.anchorMin = new Vector2(0.5f, 0.5f);
                starRect.anchorMax = new Vector2(0.5f, 0.5f);
                starRect.pivot = new Vector2(0.5f, 0.5f);
                starRect.sizeDelta = new Vector2(starSize, starSize);
                float xOffset = -totalW * 0.5f + starSize * 0.5f + i * (starSize + starSpacing);
                starRect.anchoredPosition = new Vector2(xOffset, 40f);
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
            // Spec : badge PARFAIT 480x60 doré entre étoiles et pilule.
            perfectRect.sizeDelta = new Vector2(480f, 60f);
            perfectRect.anchoredPosition = new Vector2(0f, -55f);
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
            perfectTxt.fontSize = 32;
            perfectTxt.fontStyle = FontStyles.Bold;
            perfectTxt.color = new Color(0.45f, 0.22f, 0.03f, 1f);
            perfectTxt.alignment = TextAlignmentOptions.Center;
            perfectTxt.raycastTarget = false;
            // 5ter) Pilule récompense pièces sous les étoiles (décalée pour le badge).
            _victoryCoinPill = new GameObject("CoinPill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _victoryCoinPill.transform.SetParent(_victoryPanel.transform, false);
            var coinRect = _victoryCoinPill.GetComponent<RectTransform>();
            coinRect.anchorMin = new Vector2(0.5f, 0.5f);
            coinRect.anchorMax = new Vector2(0.5f, 0.5f);
            coinRect.pivot = new Vector2(0.5f, 0.5f);
            // Spec : pilule pièces 440x92 (fond #FFF3C4, contour or).
            coinRect.sizeDelta = new Vector2(440f, 92f);
            coinRect.anchoredPosition = new Vector2(0f, -140f);
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
            coinIconRect.sizeDelta = new Vector2(56f, 56f);
            coinIconRect.anchoredPosition = new Vector2(44f, 0f);
            var coinIcon = coinIconGO.GetComponent<Image>();
            coinIcon.sprite = Resources.Load<Sprite>("UI/coin");
            coinIcon.preserveAspect = true;
            coinIcon.raycastTarget = false;
            var coinTxtGO = new GameObject("CoinText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            coinTxtGO.transform.SetParent(_victoryCoinPill.transform, false);
            var coinTxtRect = coinTxtGO.GetComponent<RectTransform>();
            coinTxtRect.anchorMin = Vector2.zero;
            coinTxtRect.anchorMax = Vector2.one;
            coinTxtRect.offsetMin = new Vector2(92f, 0f);
            coinTxtRect.offsetMax = new Vector2(-10f, 0f);
            _victoryCoinText = coinTxtGO.GetComponent<TextMeshProUGUI>();
            _victoryCoinText.font = tmpFont;
            _victoryCoinText.text = "+0";
            _victoryCoinText.fontSize = 40;
            _victoryCoinText.fontStyle = FontStyles.Bold;
            _victoryCoinText.alignment = TextAlignmentOptions.MidlineLeft;
            _victoryCoinText.color = new Color(0.55f, 0.32f, 0.08f, 1f);
            _victoryCoinText.raycastTarget = false;
            // 5quater) Ligne rewarded secondaire bleue (cachée si -5 ans, zéro pub).
            // Spec : 560x84, fond #E3F0FF, contour #4D8CE5, texte #1D4ED8, SANS pulse.
            _victoryRewardRow = new GameObject("RewardRow", typeof(RectTransform));
            _victoryRewardRow.transform.SetParent(_victoryPanel.transform, false);
            var rewRect = _victoryRewardRow.GetComponent<RectTransform>();
            rewRect.anchorMin = new Vector2(0.5f, 0.5f);
            rewRect.anchorMax = new Vector2(0.5f, 0.5f);
            rewRect.pivot = new Vector2(0.5f, 0.5f);
            rewRect.sizeDelta = new Vector2(560f, 84f);
            rewRect.anchoredPosition = new Vector2(0f, -235f);
            var rewBorderGO = new GameObject("Border", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rewBorderGO.transform.SetParent(_victoryRewardRow.transform, false);
            var rewBorderRect = (RectTransform)rewBorderGO.transform;
            rewBorderRect.anchorMin = Vector2.zero;
            rewBorderRect.anchorMax = Vector2.one;
            rewBorderRect.offsetMin = Vector2.zero;
            rewBorderRect.offsetMax = Vector2.zero;
            var rewBorderImg = rewBorderGO.GetComponent<Image>();
            rewBorderImg.sprite = GridView.SharedRoundedRect;
            rewBorderImg.type = Image.Type.Sliced;
            rewBorderImg.color = new Color(0.30f, 0.55f, 0.90f, 1f);
            rewBorderImg.raycastTarget = false;
            var rewBgGO = new GameObject("Bg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rewBgGO.transform.SetParent(_victoryRewardRow.transform, false);
            var rewBgRect = (RectTransform)rewBgGO.transform;
            rewBgRect.anchorMin = Vector2.zero;
            rewBgRect.anchorMax = Vector2.one;
            rewBgRect.offsetMin = new Vector2(4f, 4f);
            rewBgRect.offsetMax = new Vector2(-4f, -4f);
            _victoryRewardBg = rewBgGO.GetComponent<Image>();
            _victoryRewardBg.sprite = GridView.SharedRoundedRect;
            _victoryRewardBg.type = Image.Type.Sliced;
            _victoryRewardBg.color = new Color(0.89f, 0.94f, 1.00f, 1f);
            _victoryRewardBg.raycastTarget = true;
            _victoryRewardBtn = _victoryRewardRow.AddComponent<Button>();
            _victoryRewardBtn.targetGraphic = _victoryRewardBg;
            _victoryRewardBtn.onClick.AddListener(() => GrantVictoryReward());
            var rewContentGO = new GameObject("Content", typeof(RectTransform));
            rewContentGO.transform.SetParent(_victoryRewardRow.transform, false);
            var rewContentRect = (RectTransform)rewContentGO.transform;
            rewContentRect.anchorMin = Vector2.zero;
            rewContentRect.anchorMax = Vector2.one;
            rewContentRect.offsetMin = new Vector2(20f, 8f);
            rewContentRect.offsetMax = new Vector2(-20f, -8f);
            var rewHLG = rewContentGO.AddComponent<HorizontalLayoutGroup>();
            rewHLG.spacing = 12f;
            rewHLG.childAlignment = TextAnchor.MiddleCenter;
            rewHLG.childForceExpandWidth = false;
            rewHLG.childControlWidth = false;
            var rewIconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rewIconGO.transform.SetParent(rewContentGO.transform, false);
            var rewIconLE = rewIconGO.AddComponent<LayoutElement>();
            rewIconLE.preferredWidth = 52f;
            rewIconLE.preferredHeight = 52f;
            var rewIconImg = rewIconGO.GetComponent<Image>();
            rewIconImg.sprite = Resources.Load<Sprite>("UI/play_button");
            rewIconImg.preserveAspect = true;
            rewIconImg.color = new Color(0.11f, 0.31f, 0.85f, 1f);
            rewIconImg.raycastTarget = false;
            var rewTxtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            rewTxtGO.transform.SetParent(rewContentGO.transform, false);
            var rewTxtLE = rewTxtGO.AddComponent<LayoutElement>();
            rewTxtLE.flexibleWidth = 1f;
            _victoryRewardText = rewTxtGO.GetComponent<TextMeshProUGUI>();
            _victoryRewardText.font = tmpFont;
            _victoryRewardText.text = Zoologic.Localization.LocalizationManager.Get("victory.reward_hint");
            _victoryRewardText.fontSize = 32;
            _victoryRewardText.fontStyle = FontStyles.Bold;
            _victoryRewardText.color = new Color(0.11f, 0.31f, 0.85f, 1f);
            _victoryRewardText.alignment = TextAlignmentOptions.Center;
            _victoryRewardText.raycastTarget = false;
            _victoryRewardText.enableAutoSizing = true;
            _victoryRewardText.fontSizeMin = 22;
            _victoryRewardText.fontSizeMax = 32;
            // 6) CTA vedette : Niveau Suivant orange pleine largeur + pulse (90% CTR).
            // Spec : 560x104, fond #F2A633, texte #3E342E 4.7:1, seul bouton animé.
            var btnGO = new GameObject("BtnContinuer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            btnGO.transform.SetParent(_victoryPanel.transform, false);
            var btnRect2 = btnGO.GetComponent<RectTransform>();
            btnRect2.anchorMin = new Vector2(0.5f, 0.05f);
            btnRect2.anchorMax = new Vector2(0.5f, 0.05f);
            btnRect2.pivot = new Vector2(0.5f, 0.5f);
            btnRect2.sizeDelta = new Vector2(560f, 104f);
            btnRect2.anchoredPosition = new Vector2(0f, 110f);

            var btnImg = btnGO.GetComponent<Image>();
            var contNormal = JellyUI.ButtonYellow ?? JellyUI.ButtonGreen;
            var contHover = JellyUI.ButtonYellow ?? contNormal;
            var contPressed = JellyUI.ButtonRed ?? contNormal;
            var contDisabled = JellyUI.ButtonGrey ?? contNormal;
            btnImg.sprite = contNormal;
            btnImg.type = Image.Type.Sliced;
            btnImg.pixelsPerUnitMultiplier = 1f;

            var btnComp = btnGO.AddComponent<Button>();
            JellyUI.ApplyJellyButton(btnComp, btnImg, contNormal, contHover, contPressed, contDisabled);
            _victoryNextBtn = btnComp;
            bool navigated = false; // garde anti double-clic (pub + navigation)
            System.Action navigate = () =>
            {
                if (navigated) return;
                navigated = true;
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
            };
            btnComp.onClick.AddListener(() =>
            {
                // MODE B fallback : si l'animation a été interrompue (clic rapide),
                // la décision pub n'a pas eu lieu — flush ici, toujours post-victoire,
                // jamais pendant gameplay. Navigation reprise à la fermeture de l'ad.
                if (_victoryShown && !IsDailyPuzzle && !_victoryAdDecided
                    && AdMobManager.Instance != null
                    && AdMobManager.Instance.ShowInterstitialIfNeeded(_numeroNiveau, navigate))
                {
                    _victoryAdDecided = true;
                    return;
                }
                navigate();
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
            btnTxt.text = Zoologic.Localization.LocalizationManager.Get("victory.next");
            btnTxt.fontSize = 40;
            btnTxt.fontStyle = FontStyles.Bold;
            btnTxt.color = new Color(0.24f, 0.15f, 0.11f, 1f);
            btnTxt.alignment = TextAlignmentOptions.Center;
            btnTxt.raycastTarget = false;
            btnTxt.enableAutoSizing = true;
            btnTxt.fontSizeMin = 28;
            btnTxt.fontSizeMax = 40;
            var btnShadow = btnTxtGO.AddComponent<Shadow>();
            btnShadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
            btnShadow.effectDistance = new Vector2(0f, -2f);

            // Spec : Menu = lien texte discret (hitbox 420x56 ≥ 48dp, jamais un 2e CTA).
            var menuGO = new GameObject("BtnMenu", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            menuGO.transform.SetParent(_victoryPanel.transform, false);
            var menuRect = menuGO.GetComponent<RectTransform>();
            menuRect.anchorMin = new Vector2(0.5f, 0.05f);
            menuRect.anchorMax = new Vector2(0.5f, 0.05f);
            menuRect.pivot = new Vector2(0.5f, 0.5f);
            menuRect.sizeDelta = new Vector2(420f, 56f);
            menuRect.anchoredPosition = new Vector2(0f, 28f);
            var menuImg = menuGO.GetComponent<Image>();
            menuImg.color = new Color(0f, 0f, 0f, 0f);
            menuImg.raycastTarget = true;
            var menuBtn = menuGO.AddComponent<Button>();
            menuBtn.targetGraphic = menuImg;
            menuBtn.transition = Selectable.Transition.ColorTint;
            var menuColors = menuBtn.colors;
            menuColors.normalColor = Color.white;
            menuColors.pressedColor = new Color(0.85f, 0.80f, 0.72f, 1f);
            menuBtn.colors = menuColors;
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
            // Spec : texte souligné #795548 5.2:1 AA sur crème, pas d'icône (lien, pas CTA).
            var menuTxtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            menuTxtGO.transform.SetParent(menuContentGO.transform, false);
            var menuTxtRect = menuTxtGO.GetComponent<RectTransform>();
            menuTxtRect.anchorMin = Vector2.zero;
            menuTxtRect.anchorMax = Vector2.one;
            menuTxtRect.offsetMin = Vector2.zero;
            menuTxtRect.offsetMax = Vector2.zero;
            var menuTxt = menuTxtGO.GetComponent<TextMeshProUGUI>();
            menuTxt.font = tmpFont;
            menuTxt.text = $"<u>{Zoologic.Localization.LocalizationManager.Get("victory.menu")}</u>";
            menuTxt.fontSize = 28;
            menuTxt.fontStyle = FontStyles.Normal;
            menuTxt.color = new Color(0.47f, 0.33f, 0.28f, 1f);
            menuTxt.alignment = TextAlignmentOptions.Center;
            menuTxt.raycastTarget = false;
            menuTxt.enableAutoSizing = true;
            menuTxt.fontSizeMin = 20;
            menuTxt.fontSizeMax = 28;
            // Spec anti dark-pattern : X 132x132 visible en haut-droite de la carte.
            var closeGO = new GameObject("BtnClose", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            closeGO.transform.SetParent(_victoryPanel.transform, false);
            var closeRect = closeGO.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.sizeDelta = new Vector2(132f, 132f);
            closeRect.anchoredPosition = new Vector2(-24f, -24f);
            var closeImg = closeGO.GetComponent<Image>();
            closeImg.sprite = GridView.SharedRoundedRect;
            closeImg.type = Image.Type.Simple;
            closeImg.color = new Color(0.84f, 0.28f, 0.26f, 1f);
            closeImg.raycastTarget = true;
            var closeBtn = closeGO.AddComponent<Button>();
            closeBtn.targetGraphic = closeImg;
            closeBtn.onClick.AddListener(() =>
            {
                SFXManager.Instance.PlayMenuClose();
                Canvas c2 = FindFirstObjectByType<Canvas>();
                IsDailyPuzzle = false;
                SceneFader.FadeOut(this, c2, 0.3f,
                    () => UnityEngine.SceneManagement.SceneManager.LoadScene("LevelMap"));
            });
            var closeTxtGO = new GameObject("X", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            closeTxtGO.transform.SetParent(closeGO.transform, false);
            var closeTxtRect = closeTxtGO.GetComponent<RectTransform>();
            closeTxtRect.anchorMin = Vector2.zero;
            closeTxtRect.anchorMax = Vector2.one;
            closeTxtRect.offsetMin = Vector2.zero;
            closeTxtRect.offsetMax = new Vector2(0f, 6f);
            var closeTxt = closeTxtGO.GetComponent<TextMeshProUGUI>();
            closeTxt.font = tmpFont;
            closeTxt.text = "×";
            closeTxt.fontSize = 72;
            closeTxt.fontStyle = FontStyles.Bold;
            closeTxt.color = Color.white;
            closeTxt.alignment = TextAlignmentOptions.Center;
            closeTxt.raycastTarget = false;
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
                        var ft = dock.Find("FooterLabel");
                        if (ft != null) lbl = ft.GetComponent<TextMeshProUGUI>();
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
                    rt.sizeDelta = new Vector2(108f, 108f);
                    rt.anchoredPosition = new Vector2(-48f, y + 24f);
                    var img = reset.GetComponent<Image>();
                    if (img != null)
                    {
                        var s = Resources.LoadAll<Sprite>("Sprites/hi").FirstOrDefault(x => x.name == "hi_1") ?? Resources.LoadAll<Sprite>("Sprites").FirstOrDefault(x => x.name == "b_11") ?? Resources.Load<Sprite>("Sprites/b_11");
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
            Transform hint = ct.Find("IndiceBouton");
            if (grid != null && dock != null && reset != null)
            {
                dock.SetAsLastSibling();
                reset.SetAsLastSibling();
                if (hint != null) hint.SetAsLastSibling();
            }
            else if (grid != null && dock != null)
            {
                dock.SetAsLastSibling();
                if (hint != null) hint.SetAsLastSibling();
            }
            else if (reset != null) reset.SetAsLastSibling();
        }
    }
}