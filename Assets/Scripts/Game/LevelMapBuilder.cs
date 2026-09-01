using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Zoologic.Core;

namespace Zoologic
{
    public class LevelMapBuilder : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterCallback()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "LevelMap") return;
            var go = new GameObject("LevelMapBuilder");
            go.AddComponent<LevelMapBuilder>();
        }

        // ------------------------------------------------------------------
        // Constantes de layout (référence 1080×1920).
        // ------------------------------------------------------------------

        private const int TotalLevels = 100;
        private const int Columns = 4;
        private const float HeaderHeight = 130f;
        private const float ContentPad = 30f;
        private const float CellGap = 20f;
        private const float SeparatorHeight = 68f;
        private const float SeparatorMargin = 12f;
        private const float SimulatedTopNotch = 70f;

        // ------------------------------------------------------------------
        // Palette pastel chaude cohérente avec l'écran de jeu.
        // ------------------------------------------------------------------

        private static readonly Color HeaderBg = new Color(1f, 1f, 1f, 0.97f);
        private static readonly Color HeaderSepColor = new Color(0f, 0f, 0f, 0.10f);
        private static readonly Color TitleColor = new Color(0.15f, 0.13f, 0.10f);
        private static readonly Color BubbleWhite = new Color(1.00f, 0.98f, 0.96f, 1f);
        private static readonly Color BubbleLocked = new Color(0.96f, 0.94f, 0.90f, 1f);
        private static readonly Color BubbleBorderLight = new Color(0.92f, 0.89f, 0.86f, 1f);
        private static readonly Color NumberColor = new Color(0.22f, 0.19f, 0.16f, 1f);
        private static readonly Color NumberLockedColor = new Color(0.42f, 0.38f, 0.34f, 1f);
        private static readonly Color GoldStar = new Color(1f, 0.82f, 0.18f, 1f);
        private static readonly Color EmptyStar = new Color(0.92f, 0.88f, 0.83f, 1f);
        private static readonly Color LockedStar = new Color(0.80f, 0.74f, 0.66f, 1f);
        private static readonly Color LockColor = new Color(0.62f, 0.54f, 0.46f, 1f);
        private static readonly Color SeparatorBg = new Color(0.93f, 0.68f, 0.35f, 1f);
        private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.18f);
        private static readonly Color CurrentLevelBorder = new Color(0.95f, 0.55f, 0.15f, 1f);
        private static readonly Color CurrentLevelGlow = new Color(0.95f, 0.55f, 0.15f, 0.30f);

        // ------------------------------------------------------------------
        // Champs.
        // ------------------------------------------------------------------

        private ScrollRect _scrollRect;
        private RectTransform _content;
        private readonly List<LevelBubble> _bubbles = new List<LevelBubble>();
        private int _loadedCount;
        private bool _loading;
        private float _cellSize;
        private int _lastGridSize;
        private TMP_FontAsset _fontTitle;
        private TMP_FontAsset _fontBody;
        private int _currentLevel;
        private float _topInset;
        private float _headerTotal;
        private TMP_Text _livesCountText;
        private TMP_Text _livesTimerText;
        private float _livesTimerAccum;

        private struct LevelBubble
        {
            public int Level;
            public GameObject Root;
            public Image BubbleImage;
            public TMP_Text NumberText;
            public List<Image> StarImages;
            public GameObject GlowBorder;
        }

        // ------------------------------------------------------------------
        // Lifecycle.
        // ------------------------------------------------------------------

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
        }

        private void Start()
        {
            _fontTitle = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Bold SDF");
            _fontBody = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Regular SDF");

            _cellSize = (1080f - 2f * ContentPad - (Columns - 1) * CellGap) / Columns;

            _currentLevel = FindCurrentLevel();
            _topInset = CalcTopInset();

            PuzzleGameController.IsDailyPuzzle = false;
            BuildScene();
            CreerCarteDefiDuJour();
            LoadBubbles(40);
            StartCoroutine(ScrollToCurrentLevel());
            if (DailyRewardManager.CanClaimToday())
                StartCoroutine(ShowDailyDelayed());
        }

        // ------------------------------------------------------------------
        // Encoche haute en unités de canvas (réf 1080x1920). Sur mobile réel on
        // lit la safe area ; sinon (éditeur/desktop) on applique une encoche simulée.
        // ------------------------------------------------------------------

        private static float CalcTopInset()
        {
            float canvasRefHeight = 1920f;
            Rect safe = Screen.safeArea;
            float insetPx = Screen.height - safe.yMax;
            if (insetPx <= 1f)
                return SimulatedTopNotch;
            return insetPx * (canvasRefHeight / Mathf.Max(Screen.height, 1));
        }

        private int FindCurrentLevel()
        {
            int highest = LevelProgressManager.GetHighestUnlockedLevel();
            for (int lvl = 1; lvl <= highest; lvl++)
            {
                if (LevelProgressManager.GetStars(lvl) == 0)
                    return lvl;
            }
            return highest;
        }

        private void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame ?? false)
            {
                if (SettingsPanel.HandleBackButton()) return;
                SFXManager.Instance.PlayMenuClose();
                SceneManager.LoadScene("MainMenu");
            }

            _livesTimerAccum += Time.unscaledDeltaTime;
            if (_livesTimerAccum >= 1f)
            {
                _livesTimerAccum = 0f;
                if (_livesCountText != null)
                {
                    int lives = LivesManager.GetStoredLives();
                    _livesCountText.text = lives.ToString();
                    if (_livesTimerText != null)
                    {
                        if (lives >= LivesManager.MaxVies) _livesTimerText.text = "";
                        else
                        {
                            int secs = LivesManager.GetSecondsUntilNextLife();
                            _livesTimerText.text = $"{secs / 60:00}:{secs % 60:00}";
                        }
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // Construction de la scène.
        // ------------------------------------------------------------------

        private void BuildScene()
        {
            var canvasGO = new GameObject("Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            if (EventSystem.current == null)
            {
                canvasGO.AddComponent<EventSystem>();
                canvasGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            if (FindFirstObjectByType<Camera>() == null)
            {
                var camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
                var cam = camGO.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = BackgroundHelper.BgBottom;
                camGO.AddComponent<AudioListener>();
            }

            BuildBackground(canvasGO.transform);
            BuildHeader(canvasGO.transform);
            _scrollRect = BuildScrollArea(canvasGO.transform);
            _content = _scrollRect.content;
        }

        // ------------------------------------------------------------------
        // Fond : dégradé vertical pastel identique à l'écran de jeu.
        // ------------------------------------------------------------------

        private void BuildBackground(Transform parent)
        {
            BackgroundHelper.ApplyBackground(parent);
        }

        // ------------------------------------------------------------------
        // Header : barre blanche avec bouton retour (gauche) et titre centré.
        // ------------------------------------------------------------------

        private void BuildHeader(Transform parent)
        {
            _headerTotal = HeaderHeight + _topInset;

            var header = new GameObject("Header");
            header.transform.SetParent(parent, false);
            var rect = header.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, _headerTotal);
            rect.anchoredPosition = Vector2.zero;

            var img = header.AddComponent<Image>();
            img.color = HeaderBg;
            img.raycastTarget = false;

            var giftGO = new GameObject("DailyGift");
            giftGO.transform.SetParent(header.transform, false);
            var giftRect = giftGO.AddComponent<RectTransform>();
            giftRect.anchorMin = new Vector2(0f, 0.5f);
            giftRect.anchorMax = new Vector2(0f, 0.5f);
            giftRect.pivot = new Vector2(0f, 0.5f);
            giftRect.sizeDelta = new Vector2(138f, 52f);
            giftRect.anchoredPosition = new Vector2(22f, 0f);
            var giftImg = giftGO.AddComponent<Image>();
            giftImg.sprite = KenneyUI.Button(DailyRewardManager.CanClaimToday() ? "Yellow" : "Grey") ?? CreerSpriteArrondi(128, 0.5f);
            giftImg.type = Image.Type.Simple;
            giftImg.color = Color.white;
            var giftBtn = giftGO.AddComponent<Button>();
            giftBtn.targetGraphic = giftImg;
            giftBtn.onClick.AddListener(() =>
            {
                SFXManager.Instance.PlayMenuOpen();
                var c = FindFirstObjectByType<Canvas>();
                if (c != null) DailyRewardUI.Show(c);
            });
            var giftTxtGO = new GameObject("Text");
            giftTxtGO.transform.SetParent(giftGO.transform, false);
            var giftTxtRect = giftTxtGO.AddComponent<RectTransform>();
            giftTxtRect.anchorMin = Vector2.zero;
            giftTxtRect.anchorMax = Vector2.one;
            giftTxtRect.offsetMin = Vector2.zero;
            giftTxtRect.offsetMax = Vector2.zero;
            var giftTxt = giftTxtGO.AddComponent<TextMeshProUGUI>();
            giftTxt.font = _fontTitle;
            giftTxt.text = "Cadeau";
            giftTxt.fontSize = 20;
            giftTxt.fontStyle = FontStyles.Bold;
            giftTxt.color = TitleColor;
            giftTxt.alignment = TextAlignmentOptions.Center;

            var missionsGO = new GameObject("MissionsBtn");
            missionsGO.transform.SetParent(header.transform, false);
            var missionsRect = missionsGO.AddComponent<RectTransform>();
            missionsRect.anchorMin = new Vector2(0f, 0.5f);
            missionsRect.anchorMax = new Vector2(0f, 0.5f);
            missionsRect.pivot = new Vector2(0f, 0.5f);
            missionsRect.sizeDelta = new Vector2(130f, 52f);
            missionsRect.anchoredPosition = new Vector2(170f, 0f);
            var missionsImg = missionsGO.AddComponent<Image>();
            missionsImg.sprite = KenneyUI.Button("Blue") ?? CreerSpriteArrondi(128, 0.5f);
            missionsImg.type = Image.Type.Simple;
            missionsImg.color = Color.white;
            var missionsBtn = missionsGO.AddComponent<Button>();
            missionsBtn.targetGraphic = missionsImg;
            missionsBtn.onClick.AddListener(() => { SFXManager.Instance.PlayMenuOpen(); var c = FindFirstObjectByType<Canvas>(); if (c != null) MissionUI.Show(c); });
            var missionsTxtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            missionsTxtGO.transform.SetParent(missionsGO.transform, false);
            var missionsTxtRect = missionsTxtGO.GetComponent<RectTransform>();
            missionsTxtRect.anchorMin = Vector2.zero; missionsTxtRect.anchorMax = Vector2.one;
            missionsTxtRect.offsetMin = Vector2.zero; missionsTxtRect.offsetMax = Vector2.zero;
            var missionsTxt = missionsTxtGO.GetComponent<TextMeshProUGUI>();
            missionsTxt.font = _fontTitle; missionsTxt.text = "Missions"; missionsTxt.fontSize = 18; missionsTxt.fontStyle = FontStyles.Bold; missionsTxt.color = Color.white; missionsTxt.alignment = TextAlignmentOptions.Center;
            int doneM = MissionManager.GetCompletedCount();
            if (doneM > 0)
            {
                var badgeGO = new GameObject("Badge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                badgeGO.transform.SetParent(missionsGO.transform, false);
                var badgeRect = badgeGO.GetComponent<RectTransform>();
                badgeRect.anchorMin = new Vector2(1f, 1f); badgeRect.anchorMax = new Vector2(1f, 1f);
                badgeRect.pivot = new Vector2(0.5f, 0.5f);
                badgeRect.sizeDelta = new Vector2(22f, 22f);
                badgeRect.anchoredPosition = new Vector2(8f, 8f);
                var badgeImg = badgeGO.GetComponent<Image>();
                badgeImg.sprite = CreerSpriteArrondi(64, 0.5f);
                badgeImg.color = new Color(0.92f, 0.36f, 0.42f);
                var badgeTxtGO2 = new GameObject("Count", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                badgeTxtGO2.transform.SetParent(badgeGO.transform, false);
                var btr2 = badgeTxtGO2.GetComponent<RectTransform>();
                btr2.anchorMin = Vector2.zero; btr2.anchorMax = Vector2.one;
                btr2.offsetMin = Vector2.zero; btr2.offsetMax = Vector2.zero;
                var btxt2 = badgeTxtGO2.GetComponent<TextMeshProUGUI>();
                btxt2.font = _fontTitle; btxt2.text = doneM.ToString(); btxt2.fontSize = 14; btxt2.fontStyle = FontStyles.Bold; btxt2.color = Color.white; btxt2.alignment = TextAlignmentOptions.Center;
            }

            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(header.transform, false);
            var titleRect = titleGO.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(24f, 0f);
            titleRect.offsetMax = new Vector2(-140f, 0f);

            var titleText = titleGO.AddComponent<TextMeshProUGUI>();
            titleText.font = _fontTitle;
            titleText.text = "Niveaux";
            titleText.fontSize = 44;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = TitleColor;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.raycastTarget = false;

            CreerPiluleVies(header.transform);
        }

        private void CreerBoutonRetour(Transform parent)
        {
            var btnGO = new GameObject("BtnRetour");
            btnGO.transform.SetParent(parent, false);
            var btnRect = btnGO.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0f, 0.5f);
            btnRect.anchorMax = new Vector2(0f, 0.5f);
            btnRect.pivot = new Vector2(0f, 0.5f);
            btnRect.sizeDelta = new Vector2(64f, 64f);
            btnRect.anchoredPosition = new Vector2(22f, 0f);

            // Ombre derrière la flèche
            var arrowShadow = new GameObject("Shadow");
            arrowShadow.transform.SetParent(btnGO.transform, false);
            var asRect = arrowShadow.AddComponent<RectTransform>();
            asRect.anchorMin = Vector2.zero;
            asRect.anchorMax = Vector2.one;
            asRect.offsetMin = new Vector2(2f, -3f);
            asRect.offsetMax = new Vector2(2f, -3f);
            var asImg = arrowShadow.AddComponent<Image>();
            asImg.sprite = CreerFlecheRetourSprite();
            asImg.color = new Color(0f, 0f, 0f, 0.25f);
            asImg.raycastTarget = false;
            arrowShadow.transform.SetAsFirstSibling();

            var btnImg = btnGO.AddComponent<Image>();
            btnImg.sprite = CreerFlecheRetourSprite();
            btnImg.color = TitleColor;
            btnImg.raycastTarget = true;

            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            btn.colors = colors;

            btn.onClick.AddListener(() =>
            {
                SFXManager.Instance.PlayMenuClose();
                SceneManager.LoadScene("MainMenu");
            });
        }

        // ------------------------------------------------------------------
        // Pilule de ressources : cœurs (vies) alignés à droite du header.
        // ------------------------------------------------------------------

        private void CreerPiluleVies(Transform parent)
        {
            var pill = new GameObject("HeartsPill");
            pill.transform.SetParent(parent, false);
            var pillRect = pill.AddComponent<RectTransform>();
            pillRect.anchorMin = new Vector2(1f, 0.5f);
            pillRect.anchorMax = new Vector2(1f, 0.5f);
            pillRect.pivot = new Vector2(1f, 0.5f);
            pillRect.sizeDelta = new Vector2(118f, 52f);
            pillRect.anchoredPosition = new Vector2(-22f, 0f);

            var pillImg = pill.AddComponent<Image>();
            pillImg.sprite = KenneyUI.FlatButton("Grey") ?? CreerSpriteArrondi(128, 0.5f);
            pillImg.type = Image.Type.Simple;
            pillImg.color = Color.white;
            pillImg.raycastTarget = false;

            Sprite heart = Resources.Load<Sprite>("UI/heart");
            var heartObj = new GameObject("Heart");
            heartObj.transform.SetParent(pill.transform, false);
            var heartRect = heartObj.AddComponent<RectTransform>();
            heartRect.anchorMin = new Vector2(0f, 0.5f);
            heartRect.anchorMax = new Vector2(0f, 0.5f);
            heartRect.pivot = new Vector2(0.5f, 0.5f);
            heartRect.sizeDelta = new Vector2(30f, 30f);
            heartRect.anchoredPosition = new Vector2(26f, 0f);
            var heartImg = heartObj.AddComponent<Image>();
            heartImg.sprite = heart;
            heartImg.preserveAspect = true;
            heartImg.color = GoldStar;
            heartImg.raycastTarget = false;

            var txtObj = new GameObject("Count");
            txtObj.transform.SetParent(pill.transform, false);
            var txtRect = txtObj.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = new Vector2(44f, 0f);
            txtRect.offsetMax = Vector2.zero;
            var txt = txtObj.AddComponent<TextMeshProUGUI>();
            txt.font = _fontTitle;
            txt.text = LivesManager.GetStoredLives().ToString();
            txt.fontSize = 30;
            txt.fontStyle = FontStyles.Bold;
            txt.color = NumberColor;
            txt.alignment = TextAlignmentOptions.MidlineRight;
            txt.raycastTarget = false;
            _livesCountText = txt;

            var timerObj = new GameObject("Timer");
            timerObj.transform.SetParent(pill.transform, false);
            var timerRect = timerObj.AddComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0.5f, 0f);
            timerRect.anchorMax = new Vector2(0.5f, 0f);
            timerRect.pivot = new Vector2(0.5f, 1f);
            timerRect.sizeDelta = new Vector2(120f, 18f);
            timerRect.anchoredPosition = new Vector2(0f, -2f);
            _livesTimerText = timerObj.AddComponent<TextMeshProUGUI>();
            _livesTimerText.font = _fontBody;
            _livesTimerText.fontSize = 14;
            _livesTimerText.color = new Color(0.60f, 0.48f, 0.35f);
            _livesTimerText.alignment = TextAlignmentOptions.Center;
            _livesTimerText.raycastTarget = false;
            _livesTimerText.text = "";
        }

        // ------------------------------------------------------------------
        // Zone de scroll : Viewport → Content (VLG) → ScrollRect.
        // ------------------------------------------------------------------

        private ScrollRect BuildScrollArea(Transform parent)
        {
            var scrollGO = new GameObject("ScrollArea");
            scrollGO.transform.SetParent(parent, false);
            var scrollRectRT = scrollGO.AddComponent<RectTransform>();
            scrollRectRT.anchorMin = Vector2.zero;
            scrollRectRT.anchorMax = Vector2.one;
            scrollRectRT.offsetMin = Vector2.zero;
            scrollRectRT.offsetMax = new Vector2(0f, -_headerTotal);

            var viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(scrollGO.transform, false);
            var viewportRect = viewportGO.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            var viewportImg = viewportGO.AddComponent<Image>();
            viewportImg.color = new Color(0f, 0f, 0f, 0.01f);
            viewportImg.raycastTarget = true;

            var mask = viewportGO.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);
            var contentRect = contentGO.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = SeparatorMargin;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset((int)ContentPad, (int)ContentPad, 15, 15);

            var fitter = contentGO.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = scrollGO.AddComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.1f;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.135f;
            scrollRect.scrollSensitivity = 35f;

            CreerFadesScroll(parent);

            return scrollRect;
        }

        // Fondu haut/bas au-dessus de la zone de scroll : évite la coupe brutale
        // des tuiles qui passent sous le frame de l'écran.
        private void CreerFadesScroll(Transform parent)
        {
            float fadeH = 90f;

            var top = new GameObject("FadeTop");
            top.transform.SetParent(parent, false);
            var topRect = top.AddComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0f, 1f);
            topRect.anchorMax = new Vector2(1f, 1f);
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.sizeDelta = new Vector2(0f, fadeH);
            topRect.anchoredPosition = new Vector2(0f, -_headerTotal);
            var topImg = top.AddComponent<Image>();
            topImg.sprite = CreerSpriteFonduVertical(true);
            topImg.type = Image.Type.Simple;
            topImg.color = Color.white;
            topImg.raycastTarget = false;

            var bottom = new GameObject("FadeBottom");
            bottom.transform.SetParent(parent, false);
            var bottomRect = bottom.AddComponent<RectTransform>();
            bottomRect.anchorMin = new Vector2(0f, 0f);
            bottomRect.anchorMax = new Vector2(1f, 0f);
            bottomRect.pivot = new Vector2(0.5f, 0f);
            bottomRect.sizeDelta = new Vector2(0f, fadeH);
            bottomRect.anchoredPosition = Vector2.zero;
            var bottomImg = bottom.AddComponent<Image>();
            bottomImg.sprite = CreerSpriteFonduVertical(false);
            bottomImg.type = Image.Type.Simple;
            bottomImg.color = Color.white;
            bottomImg.raycastTarget = false;
        }

        // ------------------------------------------------------------------
        // Chargement progressif des bulles.
        // ------------------------------------------------------------------

        private void LoadBubbles(int count)
        {
            if (_loading || _loadedCount >= TotalLevels) return;
            _loading = true;

            int fromLevel = _loadedCount + 1;
            int toLevel = Mathf.Min(_loadedCount + count, TotalLevels);

            var currentRow = new List<int>();

            for (int level = fromLevel; level <= toLevel; level++)
            {
                int gridSize = LevelConfig.GetGridSize(level);

                if (gridSize != _lastGridSize)
                {
                    if (currentRow.Count > 0)
                    {
                        CreerLigneBulles(currentRow);
                        currentRow.Clear();
                    }

                    CreerBandeauSeparateur(gridSize);
                    _lastGridSize = gridSize;
                }

                currentRow.Add(level);

                if (currentRow.Count == Columns)
                {
                    CreerLigneBulles(currentRow);
                    currentRow.Clear();
                }
            }

            if (currentRow.Count > 0)
            {
                CreerLigneBulles(currentRow);
                currentRow.Clear();
            }

            _loadedCount = toLevel;
            _loading = false;

            if (_loadedCount < TotalLevels)
                StartCoroutine(CheckLoadMore());
        }

        // ------------------------------------------------------------------
        // Bandeau séparateur horizontal (badge pilule pleine largeur).
        // ------------------------------------------------------------------

        private void CreerBandeauSeparateur(int gridSize)
        {
            var go = new GameObject("Separator_" + gridSize);
            go.transform.SetParent(_content, false);

            // Ombre portée frère (décalée vers le bas) pour la profondeur.
            var shadowGO = new GameObject("Shadow");
            shadowGO.transform.SetParent(go.transform, false);
            var shadowRect = shadowGO.AddComponent<RectTransform>();
            shadowRect.anchorMin = new Vector2(0f, 0f);
            shadowRect.anchorMax = new Vector2(1f, 1f);
            shadowRect.pivot = new Vector2(0.5f, 0.5f);
            shadowRect.offsetMin = new Vector2(12f, -8f);
            shadowRect.offsetMax = new Vector2(-12f, 4f);
            var shadowImg = shadowGO.AddComponent<Image>();
            shadowImg.sprite = CreerSpriteArrondi(128, 0.35f);
            shadowImg.color = new Color(0f, 0f, 0f, 0.28f);
            shadowImg.raycastTarget = false;

            // Fond principal : dégradé vertical (haut clair → bas soutenu).
            var img = go.AddComponent<Image>();
            img.sprite = CreerSpriteGradientArrondi(128, 0.35f, SeparatorBg, Lighten(SeparatorBg, 0.30f));
            img.raycastTarget = false;

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = SeparatorHeight;
            le.flexibleWidth = 1f;

            // Reflet haut discret (liseré lumineux subtil, pas "brillant saturé").
            var sheenGO = new GameObject("Sheen", typeof(RectTransform));
            sheenGO.transform.SetParent(go.transform, false);
            var sheenRect = sheenGO.GetComponent<RectTransform>();
            sheenRect.anchorMin = new Vector2(0.02f, 0.62f);
            sheenRect.anchorMax = new Vector2(0.98f, 0.98f);
            sheenRect.offsetMin = Vector2.zero;
            sheenRect.offsetMax = Vector2.zero;
            var sheenImg = sheenGO.AddComponent<Image>();
            sheenImg.sprite = CreerSpriteArrondi(64, 0.5f);
            sheenImg.color = new Color(1f, 1f, 1f, 0.07f);
            sheenImg.raycastTarget = false;

            // Pastille blanche pleinement visible à gauche, avec l'étoile centrée
            // dedans (aucune troncature, contrairement à l'ancien accent nu).
            var badgeGO = new GameObject("Badge", typeof(RectTransform));
            badgeGO.transform.SetParent(go.transform, false);
            var badgeRect = badgeGO.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0f, 0.5f);
            badgeRect.anchorMax = new Vector2(0f, 0.5f);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            badgeRect.sizeDelta = new Vector2(44f, 44f);
            badgeRect.anchoredPosition = new Vector2(30f, 0f);
            var badgeImg = badgeGO.AddComponent<Image>();
            badgeImg.sprite = CreerSpriteArrondi(64, 0.5f);
            badgeImg.color = new Color(1f, 1f, 1f, 0.95f);
            badgeImg.raycastTarget = false;

            var dotGO = new GameObject("Accent", typeof(RectTransform));
            dotGO.transform.SetParent(badgeGO.transform, false);
            var dotRect = dotGO.GetComponent<RectTransform>();
            dotRect.anchorMin = Vector2.zero;
            dotRect.anchorMax = Vector2.one;
            dotRect.offsetMin = Vector2.zero;
            dotRect.offsetMax = Vector2.zero;
            var dotImg = dotGO.AddComponent<Image>();
            dotImg.sprite = GetStarSprite();
            dotImg.color = SeparatorBg;
            dotImg.raycastTarget = false;

            var txtGO = new GameObject("Label", typeof(RectTransform));
            txtGO.transform.SetParent(go.transform, false);
            var txtRect = txtGO.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = new Vector2(70f, 0f);
            txtRect.offsetMax = Vector2.zero;

            var txt = txtGO.AddComponent<TextMeshProUGUI>();
            txt.font = _fontTitle;
            txt.text = "Grilles " + gridSize + "\u00D7" + gridSize;
            txt.fontSize = 34;
            txt.fontStyle = FontStyles.Bold;
            txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.Center;
            txt.textWrappingMode = TextWrappingModes.NoWrap;
            txt.raycastTarget = false;
            txt.outlineWidth = 0.35f;
            txt.outlineColor = new Color(0f, 0f, 0f, 0.30f);
        }

        private void CreerCarteDefiDuJour()
        {
            bool done = DailyPuzzleManager.IsCompletedToday();
            var go = new GameObject("DailyCard");
            go.transform.SetParent(_content, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 110f;
            le.flexibleWidth = 1f;

            var bg = go.AddComponent<Image>();
            bg.sprite = CreerSpriteGradientArrondi(128, 0.28f, new Color(0.96f, 0.78f, 0.22f), new Color(1f, 0.92f, 0.45f));
            bg.raycastTarget = false;

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(22, 22, 12, 12);
            hlg.spacing = 12f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;

            var leftGO = new GameObject("Left", typeof(RectTransform));
            leftGO.transform.SetParent(go.transform, false);
            var leftLE = leftGO.AddComponent<LayoutElement>();
            leftLE.flexibleWidth = 1f;
            var leftVLG = leftGO.AddComponent<VerticalLayoutGroup>();
            leftVLG.spacing = 4f;
            leftVLG.childAlignment = TextAnchor.MiddleLeft;
            leftVLG.childForceExpandWidth = true;

            var titleGO = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titleGO.transform.SetParent(leftGO.transform, false);
            var title = titleGO.GetComponent<TextMeshProUGUI>();
            title.font = _fontTitle;
            title.text = done ? "Défi du jour — Terminé" : "Défi du jour";
            title.fontSize = 26;
            title.fontStyle = FontStyles.Bold;
            title.color = done ? new Color(0.40f, 0.38f, 0.34f) : new Color(0.29f, 0.18f, 0.10f);
            title.alignment = TextAlignmentOptions.MidlineLeft;
            var titleLE2 = titleGO.AddComponent<LayoutElement>();
            titleLE2.preferredHeight = 30f;

            var subGO = new GameObject("Sub", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            subGO.transform.SetParent(leftGO.transform, false);
            var sub = subGO.GetComponent<TextMeshProUGUI>();
            sub.font = _fontBody;
            sub.text = done ? "Reviens demain" : $"Grille {DailyPuzzleManager.GetTodaySize()}×{DailyPuzzleManager.GetTodaySize()}  •  +{DailyPuzzleManager.RewardCoins} pièces";
            sub.fontSize = 20;
            sub.color = new Color(0.50f, 0.42f, 0.35f);
            sub.alignment = TextAlignmentOptions.MidlineLeft;
            var subLE = subGO.AddComponent<LayoutElement>();
            subLE.preferredHeight = 24f;

            var btnGO = new GameObject("BtnDaily", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(go.transform, false);
            var btnRect = btnGO.GetComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(160f, 52f);
            var btnImg = btnGO.GetComponent<Image>();
            btnImg.sprite = KenneyUI.Button(done ? "Grey" : "Green") ?? CreerSpriteArrondi(128, 0.35f);
            btnImg.type = Image.Type.Simple;
            btnImg.color = done ? new Color(0.75f, 0.75f, 0.78f) : new Color(0.22f, 0.65f, 0.30f);
            var btn = btnGO.GetComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.interactable = !done;
            var btnLE = btnGO.AddComponent<LayoutElement>();
            btnLE.preferredWidth = 160f;
            btnLE.preferredHeight = 52f;
            var btnTxtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            btnTxtGO.transform.SetParent(btnGO.transform, false);
            var btnTxtRect = btnTxtGO.GetComponent<RectTransform>();
            btnTxtRect.anchorMin = Vector2.zero;
            btnTxtRect.anchorMax = Vector2.one;
            btnTxtRect.offsetMin = Vector2.zero;
            btnTxtRect.offsetMax = Vector2.zero;
            var btnTxt = btnTxtGO.GetComponent<TextMeshProUGUI>();
            btnTxt.font = _fontTitle;
            btnTxt.text = done ? "Fait" : "Jouer";
            btnTxt.fontSize = 22;
            btnTxt.fontStyle = FontStyles.Bold;
            btnTxt.color = Color.white;
            btnTxt.alignment = TextAlignmentOptions.Center;
            if (!done)
            {
                btn.onClick.AddListener(() =>
                {
                    if (LivesManager.GetStoredLives() <= 0)
                    {
                        SFXManager.Instance.PlayMenuClose();
                        ShowLivesPopup();
                        return;
                    }
                    SFXManager.Instance.PlayMenuOpen();
                    PuzzleGameController.IsDailyPuzzle = true;
                    SceneManager.LoadScene("TestGrid");
                });
            }
        }

        private static Color Lighten(Color c, float amount)
        {
            return new Color(
                Mathf.Clamp01(c.r + amount),
                Mathf.Clamp01(c.g + amount),
                Mathf.Clamp01(c.b + amount),
                c.a);
        }

        private static Sprite CreerSpriteGradientArrondi(int resolution, float coinRatio,
            Color bottom, Color top)
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
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius + 0.5f - dist);
                    if (alpha <= 0f) { texture.SetPixel(x, y, new Color(0f, 0f, 0f, 0f)); continue; }

                    float t = (float)y / (resolution - 1);
                    Color c = Color.Lerp(bottom, top, t);
                    c.a = alpha;
                    texture.SetPixel(x, y, c);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, resolution, resolution),
                new Vector2(0.5f, 0.5f));
        }


        /// <summary>
        /// Sprite de fondu vertical. fadeFromTop = true : opaque avec BgTop en
        /// haut du sprite, transparent en bas. false : opaque avec BgBottom en
        /// bas, transparent en haut. Ainsi le fondu se noie dans le fond.
        /// </summary>
        private static Sprite CreerSpriteFonduVertical(bool fadeFromTop)
        {
            const int h = 64;
            var tex = new Texture2D(1, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Color edge = fadeFromTop ? BackgroundHelper.BgTop : BackgroundHelper.BgBottom;

            for (int y = 0; y < h; y++)
            {
                float t = (float)y / (h - 1); // 0 = bas du sprite, 1 = haut
                Color c;
                if (fadeFromTop)
                    c = new Color(edge.r, edge.g, edge.b, t);      // opaque (haut) -> transparent (bas)
                else
                    c = new Color(edge.r, edge.g, edge.b, 1f - t);  // transparent (haut) -> opaque (bas)
                tex.SetPixel(0, y, c);
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f));
        }

        // ------------------------------------------------------------------
        // Ligne de 4 bulles.
        // ------------------------------------------------------------------

        private void CreerLigneBulles(List<int> levels)
        {
            var rowGO = new GameObject("Row_" + levels[0]);
            rowGO.transform.SetParent(_content, false);

            var rowLayout = rowGO.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = _cellSize;
            rowLayout.flexibleWidth = 1f;

            var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = CellGap;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            for (int i = 0; i < levels.Count; i++)
                CreerBulleNiveau(levels[i], rowGO.transform);
        }

        // ------------------------------------------------------------------
        // Bulle de niveau (case arrondie + numéro + étoiles + cadenas).
        // ------------------------------------------------------------------

        private void CreerBulleNiveau(int level, Transform parent)
        {
            bool unlocked = level <= LevelProgressManager.GetHighestUnlockedLevel();
            int stars = LevelProgressManager.GetStars(level);
            bool isCurrent = unlocked && level == _currentLevel;

            var bubbleGO = new GameObject("Bubble_" + level);
            bubbleGO.transform.SetParent(parent, false);

            var bubbleRect = bubbleGO.AddComponent<RectTransform>();
            bubbleRect.anchorMin = new Vector2(0.5f, 0.5f);
            bubbleRect.anchorMax = new Vector2(0.5f, 0.5f);
            bubbleRect.pivot = new Vector2(0.5f, 0.5f);

            var bubbleLayout = bubbleGO.AddComponent<LayoutElement>();
            bubbleLayout.preferredWidth = _cellSize;
            bubbleLayout.preferredHeight = _cellSize;

            CreerOmbreArrondie(bubbleGO.transform);
            CreerFondArrondi(bubbleGO);

            var bubbleImg = bubbleGO.GetComponent<Image>();
            bubbleImg.color = unlocked ? BubbleWhite : BubbleLocked;
            bubbleImg.raycastTarget = unlocked;

            if (unlocked)
            {
                var btn = bubbleGO.AddComponent<Button>();
                btn.targetGraphic = bubbleImg;
                btn.transition = Selectable.Transition.ColorTint;
                var colors = btn.colors;
                colors.normalColor = isCurrent ? CurrentLevelGlow : BubbleWhite;
                colors.highlightedColor = new Color(0.90f, 0.93f, 1f);
                colors.pressedColor = new Color(0.82f, 0.86f, 0.95f);
                btn.colors = colors;

                int capturedLevel = level;
                btn.onClick.AddListener(() =>
                {
                    if (LivesManager.GetStoredLives() <= 0)
                    {
                        SFXManager.Instance.PlayMenuClose();
                        ShowLivesPopup();
                        return;
                    }
                    SFXManager.Instance.PlayMenuOpen();
                    PuzzleGameController.SelectedLevel = capturedLevel;
                    SceneManager.LoadScene("TestGrid");
                });

                var handler = bubbleGO.AddComponent<BubblePressHandler>();
                if (isCurrent)
                    handler.EnablePulse();

                if (isCurrent)
                    CreerGlowBorder(bubbleGO.transform);
            }

            CreerTexteNiveau(bubbleGO.transform, level, unlocked);
            CreerEtoiles(bubbleGO.transform, stars, unlocked);

            if (!unlocked)
                CreerCadenas(bubbleGO.transform);

            _bubbles.Add(new LevelBubble
            {
                Level = level,
                Root = bubbleGO,
                BubbleImage = bubbleImg,
                NumberText = null,
                StarImages = new List<Image>(),
                GlowBorder = null
            });
        }

        private void CreerFondArrondi(GameObject go)
        {
            var existing = go.GetComponent<Image>();
            if (existing != null) return;

            var img = go.AddComponent<Image>();
            img.sprite = GetRoundedRectSprite();
            img.type = Image.Type.Simple;
        }

        private void CreerOmbreArrondie(Transform parent)
        {
            var shadowGO = new GameObject("Shadow");
            shadowGO.transform.SetParent(parent, false);
            var rect = shadowGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(_cellSize - 6f, _cellSize - 6f);
            rect.anchoredPosition = new Vector2(3f, -5f);

            var img = shadowGO.AddComponent<Image>();
            img.sprite = GetRoundedRectSprite();
            img.color = ShadowColor;
            img.raycastTarget = false;
        }

        private void CreerTexteNiveau(Transform parent, int level, bool unlocked)
        {
            var txtGO = new GameObject("Num");
            txtGO.transform.SetParent(parent, false);
            var rect = txtGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.1f, 0.45f);
            rect.anchorMax = new Vector2(0.9f, 0.85f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var txt = txtGO.AddComponent<TextMeshProUGUI>();
            txt.font = _fontTitle;
            txt.text = level.ToString();
            txt.fontSize = 48;
            txt.fontStyle = FontStyles.Bold;
            txt.color = unlocked ? NumberColor : NumberLockedColor;
            txt.alignment = TextAlignmentOptions.Center;
            txt.enableAutoSizing = true;
            txt.fontSizeMin = 20f;
            txt.fontSizeMax = 56f;
            txt.raycastTarget = false;
        }

        private void CreerEtoiles(Transform parent, int starCount, bool unlocked)
        {
            if (!unlocked) return;

            var starsGO = new GameObject("Stars");
            starsGO.transform.SetParent(parent, false);
            var rect = starsGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.1f, 0.08f);
            rect.anchorMax = new Vector2(0.9f, 0.40f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var hlg = starsGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 4f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            Sprite starSprite = GetStarSprite();

            for (int i = 0; i < 3; i++)
            {
                var starGO = new GameObject("Star_" + i);
                starGO.transform.SetParent(starsGO.transform, false);
                var starImg = starGO.AddComponent<Image>();
                starImg.sprite = starSprite;
                starImg.preserveAspect = true;
                starImg.color = i < starCount ? GoldStar : EmptyStar;
                starImg.raycastTarget = false;

                var starLE = starGO.AddComponent<LayoutElement>();
                starLE.preferredWidth = 38f;
                starLE.preferredHeight = 38f;
            }
        }

        private void CreerCadenas(Transform parent)
        {
            var lockGO = new GameObject("Lock");
            lockGO.transform.SetParent(parent, false);
            var rect = lockGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(40f, 40f);
            rect.anchoredPosition = Vector2.zero;

            var lockImg = lockGO.AddComponent<Image>();
            lockImg.sprite = CreerCadenasSprite();
            lockImg.color = LockColor;
            lockImg.preserveAspect = true;
            lockImg.raycastTarget = false;
        }

        private GameObject CreerGlowBorder(Transform parent)
        {
            var glowGO = new GameObject("Glow");
            glowGO.transform.SetParent(parent, false);
            var rect = glowGO.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-6f, -6f);
            rect.offsetMax = new Vector2(6f, 6f);

            var img = glowGO.AddComponent<Image>();
            img.sprite = CreerSpriteArrondi(128, 0.28f);
            img.color = CurrentLevelBorder;
            img.raycastTarget = false;

            glowGO.transform.SetAsFirstSibling();
            return glowGO;
        }

        // ------------------------------------------------------------------
        // Scroll infini et auto-scroll au niveau courant.
        // ------------------------------------------------------------------

        private IEnumerator CheckLoadMore()
        {
            while (_loadedCount < TotalLevels)
            {
                if (_scrollRect.verticalNormalizedPosition < 0.15f)
                    LoadBubbles(20);
                yield return new WaitForSeconds(0.25f);
            }
        }

        private IEnumerator ScrollToCurrentLevel()
        {
            yield return new WaitForSeconds(0.3f);

            int target = _currentLevel;
            if (target <= 1)
            {
                _scrollRect.verticalNormalizedPosition = 1f;
                yield break;
            }

            float rowHeight = _cellSize + SeparatorMargin;
            float separatorAlloc = SeparatorHeight + SeparatorMargin;
            int separatorsBefore = 0;
            int lastSeen = 0;
            for (int lvl = 1; lvl < target; lvl++)
            {
                int gs = LevelConfig.GetGridSize(lvl);
                if (gs != lastSeen)
                {
                    separatorsBefore++;
                    lastSeen = gs;
                }
            }

            int rowIndex = (target - 1) / Columns;
            float targetY = rowIndex * rowHeight + separatorsBefore * separatorAlloc;

            float viewportH = ((RectTransform)_scrollRect.viewport).rect.height;
            float contentH = _content.rect.height;

            if (contentH <= viewportH)
            {
                _scrollRect.verticalNormalizedPosition = 0.5f;
                yield break;
            }

            float normalized = Mathf.Clamp01(targetY / (contentH - viewportH));
            _scrollRect.verticalNormalizedPosition = Mathf.Clamp01(1f - normalized);
        }

        private IEnumerator ShowDailyDelayed()
        {
            yield return new WaitForSeconds(0.8f);
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null && DailyRewardManager.CanClaimToday())
                DailyRewardUI.Show(canvas);
        }

        private void ShowLivesPopup()
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            var root = new GameObject("LivesPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(canvas.transform, false);
            var rRect = root.GetComponent<RectTransform>();
            rRect.anchorMin = Vector2.zero;
            rRect.anchorMax = Vector2.one;
            rRect.offsetMin = Vector2.zero;
            rRect.offsetMax = Vector2.zero;
            var rImg = root.GetComponent<Image>();
            rImg.color = new Color(0.24f, 0.16f, 0.10f, 0.62f);
            rImg.raycastTarget = true;
            var card = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            card.transform.SetParent(root.transform, false);
            var cRect = card.GetComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0.5f, 0.5f);
            cRect.anchorMax = new Vector2(0.5f, 0.5f);
            cRect.pivot = new Vector2(0.5f, 0.5f);
            cRect.sizeDelta = new Vector2(560f, 320f);
            cRect.anchoredPosition = Vector2.zero;
            var cImg = card.GetComponent<Image>();
            cImg.sprite = CreerSpriteArrondi(128, 0.22f);
            cImg.type = Image.Type.Simple;
            cImg.color = new Color(1f, 0.98f, 0.94f, 1f);
            var cardShadow = card.AddComponent<Shadow>();
            cardShadow.effectColor = new Color(0.18f, 0.11f, 0.06f, 0.30f);
            cardShadow.effectDistance = new Vector2(0f, -8f);
            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(24, 24, 24, 24);
            vlg.spacing = 16f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth = true;
            var titleGO = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titleGO.transform.SetParent(card.transform, false);
            var title = titleGO.GetComponent<TextMeshProUGUI>();
            title.font = _fontTitle;
            title.text = "Plus de vies !";
            title.fontSize = 32;
            title.fontStyle = FontStyles.Bold;
            title.color = new Color(0.29f, 0.18f, 0.10f);
            title.alignment = TextAlignmentOptions.Center;
            var titleLE = titleGO.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 40f;
            var timerGO = new GameObject("Timer", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            timerGO.transform.SetParent(card.transform, false);
            var timer = timerGO.GetComponent<TextMeshProUGUI>();
            timer.font = _fontBody;
            int secs = LivesManager.GetSecondsUntilNextLife();
            timer.text = secs > 0 ? $"Prochaine vie dans {secs / 60:00}:{secs % 60:00}" : "Vies pleines !";
            timer.fontSize = 20;
            timer.color = new Color(0.60f, 0.48f, 0.35f);
            timer.alignment = TextAlignmentOptions.Center;
            var timerLE = timerGO.AddComponent<LayoutElement>();
            timerLE.preferredHeight = 24f;
            var btnRow = new GameObject("BtnRow", typeof(RectTransform));
            btnRow.transform.SetParent(card.transform, false);
            var rowLE = btnRow.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 60f;
            var hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            var pubBtn = CreateLivesButton(btnRow.transform, "Pub (+3 ♥)", new Color(0.22f, 0.65f, 0.30f), () =>
            {
                var admob2 = AdMobManager.Instance;
                System.Action grant2 = () =>
                {
                    var lm = new LivesManager();
                    lm.AjouterVies(3);
                    SFXManager.Instance.PlayUnlock();
                    Destroy(root);
                    if (_livesCountText != null) _livesCountText.text = LivesManager.GetStoredLives().ToString();
                };
                if (admob2 != null) admob2.ShowRewarded(grant2);
                else grant2();
            });
            var closeBtn = CreateLivesButton(btnRow.transform, "Fermer", new Color(0.75f, 0.75f, 0.78f), () => Destroy(root));
            root.AddComponent<PopupCloser>().Init(root);
        }

        private Button CreateLivesButton(Transform parent, string label, Color bg, System.Action onClick)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(220f, 52f);
            var img = go.GetComponent<Image>();
            img.sprite = KenneyUI.Button(bg.g > bg.r ? "Green" : "Grey") ?? CreerSpriteArrondi(128, 0.35f);
            img.type = Image.Type.Simple;
            img.color = bg;
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.20f, 0.12f, 0.07f, 0.22f);
            shadow.effectDistance = new Vector2(0f, -3f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick?.Invoke());
            var txtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            txtGO.transform.SetParent(go.transform, false);
            var txtRect = txtGO.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;
            var txt = txtGO.GetComponent<TextMeshProUGUI>();
            txt.font = _fontTitle;
            txt.text = label;
            txt.fontSize = 20;
            txt.fontStyle = FontStyles.Bold;
            txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.Center;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 220f;
            le.preferredHeight = 52f;
            return btn;
        }

        private class PopupCloser : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler
        {
            private GameObject _root;
            public void Init(GameObject root) => _root = root;
            public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
            {
                if (eventData.pointerCurrentRaycast.gameObject == _root)
                    Destroy(_root);
            }
        }

        // ------------------------------------------------------------------
        // Sprites procéduraux.
        // ------------------------------------------------------------------

        private static Sprite _roundedRectSprite;

        private static Sprite GetRoundedRectSprite()
        {
            if (_roundedRectSprite == null)
                _roundedRectSprite = CreerSpriteArrondi(256, 0.22f);
            return _roundedRectSprite;
        }

        private static Sprite GetStarSprite()
        {
            Sprite s = Resources.Load<Sprite>("UI/star");
            if (s != null) return s;
            return CreerEtoileSprite();
        }

        private static Sprite CreerSpriteArrondi(int resolution, float coinRatio)
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
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius + 0.5f - dist);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, resolution, resolution),
                new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreerEtoileSprite()
        {
            int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            float c = s / 2f;
            float rOut = s * 0.45f;
            float rIn = rOut * 0.4f;

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = x - c + 0.5f;
                    float dy = y - c + 0.5f;
                    float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 90f;
                    if (angle < 0) angle += 360f;

                    float radA = angle * Mathf.Deg2Rad;
                    int seg = ((int)(angle / 72f)) % 2;
                    float rEdge = seg == 0 ? rOut : rIn;

                    float edgeX = c + rEdge * Mathf.Sin(radA);
                    float edgeY = c - rEdge * Mathf.Cos(radA);
                    float distToEdge = Mathf.Sqrt((x - edgeX) * (x - edgeX) + (y - edgeY) * (y - edgeY));

                    if (distToEdge < 1.8f)
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, 1f));
                    else
                        tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreerCadenasSprite()
        {
            int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                    tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));

            float cx = s / 2f;
            float cy = s * 0.55f;
            float radOuter = s * 0.25f;
            float radInner = s * 0.17f;

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;

                    bool inOuter = dist <= radOuter && dist >= radInner;
                    bool inArc = angle >= -180f && angle <= 0f;

                    if (inOuter && inArc)
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, 1f));
                }
            }

            float bodyTop = cy + 2f;
            float bodyBottom = s * 0.12f;
            float bodyLeft = cx - radInner;
            float bodyRight = cx + radInner;
            float cornerR = 4f;

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    if (y >= bodyBottom && y <= bodyTop && x >= bodyLeft && x <= bodyRight)
                    {
                        bool inCorner = false;
                        float dBL = Mathf.Sqrt((x - bodyLeft) * (x - bodyLeft) + (y - bodyBottom) * (y - bodyBottom));
                        float dBR = Mathf.Sqrt((x - bodyRight) * (x - bodyRight) + (y - bodyBottom) * (y - bodyBottom));
                        if (dBL < cornerR || dBR < cornerR) inCorner = true;

                        if (!inCorner)
                            tex.SetPixel(x, y, new Color(1f, 1f, 1f, 1f));
                    }
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreerFlecheRetourSprite()
        {
            int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);

            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                    tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));

            float thickness = 5f;
            float headSize = 18f;
            float cx = s * 0.55f;
            float cy = s * 0.5f;

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dy = Mathf.Abs(y - cy);
                    if (dy <= thickness && x >= cx - 26f && x <= cx + 6f)
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, 1f));

                    float dHead = Mathf.Sqrt((x - (cx - 22f)) * (x - (cx - 22f)) + (y - cy) * (y - cy));
                    if (dHead <= headSize && x <= cx - 18f)
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, 1f));

                    float da = Mathf.Abs(y - (cy - headSize * 0.6f));
                    float db = Mathf.Abs(y - (cy + headSize * 0.6f));
                    if ((da <= thickness || db <= thickness) && x >= cx - 40f && x <= cx - 22f)
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, 1f));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        }

        // ------------------------------------------------------------------
        // BubblePressHandler — feedback tactile scale 92% pendant l'appui.
        // ------------------------------------------------------------------

        private class BubblePressHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
        {
            private Vector3 _baseScale;
            private bool _pressed;
            private bool _pulseEnabled;
            private float _pulseElapsed;
            private const float PulseDuration = 1.2f;
            private const float PulseMax = 1.06f;
            private const float PressScale = 0.92f;

            private void Awake()
            {
                _baseScale = transform.localScale;
            }

            public void EnablePulse()
            {
                _pulseEnabled = true;
            }

            private void Update()
            {
                if (_pressed || !_pulseEnabled) return;
                _pulseElapsed += Time.deltaTime;
                float t = Mathf.PingPong(_pulseElapsed / PulseDuration, 1f);
                float smooth = Mathf.SmoothStep(0f, 1f, t);
                transform.localScale = Vector3.Lerp(_baseScale, _baseScale * PulseMax, smooth);
            }

            public void OnPointerDown(PointerEventData eventData)
            {
                _pressed = true;
                transform.localScale = _baseScale * PressScale;
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                if (!_pressed) return;
                _pressed = false;
                _pulseElapsed = 0f;
                transform.localScale = _baseScale;
            }
        }

        // ------------------------------------------------------------------
        // DragScrollHandler.
        // ------------------------------------------------------------------

        private class DragScrollHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
        {
            private ScrollRect _scroll;

            private void Awake()
            {
                _scroll = GetComponentInParent<ScrollRect>();
            }

            public void OnBeginDrag(PointerEventData eventData)
            {
                if (_scroll != null) _scroll.OnBeginDrag(eventData);
            }

            public void OnDrag(PointerEventData eventData)
            {
                if (_scroll != null) _scroll.OnDrag(eventData);
            }

            public void OnEndDrag(PointerEventData eventData)
            {
                if (_scroll != null) _scroll.OnEndDrag(eventData);
            }

            public void OnScroll(PointerEventData eventData)
            {
                if (_scroll != null) _scroll.OnScroll(eventData);
            }
        }
    }
}
