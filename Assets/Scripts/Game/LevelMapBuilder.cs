using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        // Spec refonte : 3 colonnes x 300px, marges 50, gap 40, header 180.
        private const int Columns = 3;
        private const float HeaderHeight = 180f;
        private const float ContentPad = 50f;
        private const float CellGap = 40f;
        private const float SeparatorHeight = 110f;
        private const float SeparatorMargin = 40f;
        private const float SimulatedTopNotch = 70f;
        private const float FixedDailyHeight = 240f;
        // Jauge Star Chests : fixe sous le défi (titre + 3 coffres 112px).
        private const float GaugeHeight = 176f;

        // ------------------------------------------------------------------
        // Palette Pastel Pop chaude - dynamique et douce.
        // ------------------------------------------------------------------

        private static readonly Color PastelPopBg = new Color(0.992f, 0.973f, 0.949f, 1f);
        private static readonly Color HeaderBg = new Color(1f, 1f, 1f, 0.97f);
        private static readonly Color HeaderSepColor = new Color(0f, 0f, 0f, 0.10f);
        private static readonly Color TitleColor = new Color(0.306f, 0.204f, 0.180f, 1f);
        private static readonly Color BubbleWhite = new Color(1.00f, 0.98f, 0.96f, 1f);
        // Spec refonte (WCAG sur fond crème #FFF8EC).
        private static readonly Color SuccessBg = new Color(0.875f, 0.949f, 0.780f, 1f);
        private static readonly Color SuccessEdge = new Color(0.678f, 0.835f, 0.506f, 1f);
        private static readonly Color CurrentNumber = new Color(0.365f, 0.251f, 0.216f, 1f);
        private static readonly Color CurrentEdge = new Color(0.902f, 0.494f, 0.133f, 1f);
        private static readonly Color LockedEdge = new Color(0.851f, 0.796f, 0.690f, 1f);
        private static readonly Color StarOutline = new Color(0.553f, 0.353f, 0.000f, 1f);
        private static readonly Color BubbleLocked = new Color(0.953f, 0.918f, 0.847f, 1f);
        private static readonly Color BubbleBorderLight = new Color(0.92f, 0.89f, 0.86f, 1f);
        private static readonly Color NumberColor = new Color(0.306f, 0.204f, 0.180f, 1f);
        private static readonly Color NumberLockedColor = new Color(0.620f, 0.550f, 0.440f, 1f);
        private static readonly Color GoldStar = new Color(1f, 0.757f, 0.027f, 1f);
        private static readonly Color EmptyStar = new Color(0.92f, 0.88f, 0.83f, 1f);
        private static readonly Color LockedStar = new Color(0.80f, 0.74f, 0.66f, 1f);
        private static readonly Color LockColor = new Color(0.553f, 0.518f, 0.443f, 1f);
        private static readonly Color SeparatorBg = new Color(0.290f, 0.486f, 0.349f, 1f);
        private static readonly Color SeparatorBgLight = new Color(0.329f, 0.541f, 0.392f, 1f);
        private static readonly Color SeparatorBevelLight = new Color(1f, 1f, 1f, 0.55f);
        private static readonly Color SeparatorBevelDark = new Color(0.45f, 0.60f, 0.50f, 0.25f);
        private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.18f);
        private static readonly Color CurrentLevelGradientTop = new Color(1f, 0.718f, 0.302f, 1f);
        private static readonly Color CurrentLevelGradientBottom = new Color(1f, 0.851f, 0.651f, 1f);
        private static readonly Color CurrentLevelBorder = new Color(1f, 0.65f, 0.22f, 1f);
        private static readonly Color CurrentLevelGlow = new Color(1f, 0.58f, 0.20f, 0.45f);

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
            StarChestManager.CheckAndGrant();
            BuildScene();
            Zoologic.Localization.LocalizationManager.ApplyFontsToScene();
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
                // Pub plein écran : ne rien consommer, laisser le SDK gérer BACK/X.
                if (AdMobManager.IsFullscreenAdShowing()) return;
                if (DailyRewardUI.IsOpen) { DailyRewardUI.Close(); return; }
                if (MissionUI.IsOpen) { MissionUI.Close(); return; }
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

            // P0.2 : EventSystem unique via garde partagée (jamais hébergé ici).
            UiInputGuard.EnsureSingleEventSystem();

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
            CreerDailyFixe(canvasGO.transform);
            CreerJaugeCoffres(canvasGO.transform);
            _scrollRect = BuildScrollArea(canvasGO.transform);
            _content = _scrollRect.content;
        }

        // ------------------------------------------------------------------
        // Fond : dégradé vertical pastel identique à l'écran de jeu.
        // ------------------------------------------------------------------

        private void BuildBackground(Transform parent)
        {
            var bgGO = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGO.transform.SetParent(parent, false);
            var bgRect = bgGO.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = bgGO.GetComponent<Image>();
            bgImg.color = PastelPopBg;
            bgImg.raycastTarget = false;
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

            CreerBoutonRetour(header.transform);

            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(header.transform, false);
            var titleRect = titleGO.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            float contentShift = _topInset * 0.5f;
            titleRect.offsetMin = new Vector2(200f, -contentShift);
            titleRect.offsetMax = new Vector2(-300f, -contentShift);

            var titleText = titleGO.AddComponent<TextMeshProUGUI>();
            titleText.font = _fontTitle;
            titleText.text = Zoologic.Localization.LocalizationManager.Get("levelmap.title");
            Zoologic.Localization.LocalizationManager.ApplyTo(titleText);
            titleText.fontSize = 72;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = TitleColor;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 56;
            titleText.fontSizeMax = 72;
            titleText.raycastTarget = false;

            CreerPiluleVies(header.transform);
        }

        private void CreerBoutonRetour(Transform parent)
        {
            // Spec D7 : bouton rond clay (fini le violet), flèche procédurale teintée.
            var btnGO = new GameObject("BtnRetour");
            btnGO.transform.SetParent(parent, false);
            var btnRect = btnGO.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0f, 0.5f);
            btnRect.anchorMax = new Vector2(0f, 0.5f);
            btnRect.pivot = new Vector2(0f, 0.5f);
            btnRect.sizeDelta = new Vector2(132f, 132f);
            btnRect.anchoredPosition = new Vector2(48f, -_topInset * 0.5f);

            // Sprite d'origine b_13 (tuile violette, flèche →) : restauré tel quel,
            // miroir horizontal pour pointer en arrière, visuel 96px centré dans
            // une zone tactile 132px (fond invisible).
            var tileGO = new GameObject("Tile");
            tileGO.transform.SetParent(btnGO.transform, false);
            var tileRect = tileGO.AddComponent<RectTransform>();
            tileRect.anchorMin = new Vector2(0.5f, 0.5f);
            tileRect.anchorMax = new Vector2(0.5f, 0.5f);
            tileRect.pivot = new Vector2(0.5f, 0.5f);
            tileRect.sizeDelta = new Vector2(96f, 96f);
            tileRect.anchoredPosition = Vector2.zero;
            tileRect.localScale = new Vector3(-1f, 1f, 1f);
            var tileImg = tileGO.AddComponent<Image>();
            // P0.1 : b_13 prioritaire, chevron procédural (vérifié visuellement)
            // en secours — plus jamais de bouton invisible si l'atlas change.
            Sprite backTile = Resources.LoadAll<Sprite>("Sprites").FirstOrDefault(s => s.name == "b_13") ?? Resources.Load<Sprite>("Sprites/b_13");
            if (backTile != null)
            {
                tileImg.sprite = backTile;
                tileImg.color = Color.white;
            }
            else
            {
                tileImg.sprite = CreerFlecheRetourSprite();
                tileImg.color = new Color(0.365f, 0.251f, 0.216f, 1f);
                tileRect.localScale = Vector3.one;
            }
            tileImg.type = Image.Type.Simple;
            tileImg.preserveAspect = true;
            tileImg.raycastTarget = false;
            // Zone tactile = tout le bouton 132px (fond invisible).
            var hitImg = btnGO.AddComponent<Image>();
            hitImg.color = new Color(0f, 0f, 0f, 0f);
            hitImg.raycastTarget = true;

            var btn = btnGO.AddComponent<Button>();
            btn.targetGraphic = hitImg;
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
            pillRect.sizeDelta = new Vector2(240f, 96f);
            pillRect.anchoredPosition = new Vector2(-48f, -_topInset * 0.5f);

            var pillImg = pill.AddComponent<Image>();
            pillImg.sprite = KenneyUI.FlatButton("Grey") ?? CreerSpriteArrondi(128, 0.5f);
            pillImg.type = Image.Type.Simple;
            pillImg.color = Color.white;
            pillImg.raycastTarget = false;
            var pillShadow = pill.AddComponent<Shadow>();
            pillShadow.effectColor = new Color(0f, 0f, 0f, 0.08f);
            pillShadow.effectDistance = new Vector2(0f, -3f);

            Sprite heart = Resources.Load<Sprite>("UI/heart");
            var heartObj = new GameObject("Heart");
            heartObj.transform.SetParent(pill.transform, false);
            var heartRect = heartObj.AddComponent<RectTransform>();
            heartRect.anchorMin = new Vector2(0f, 0.5f);
            heartRect.anchorMax = new Vector2(0f, 0.5f);
            heartRect.pivot = new Vector2(0.5f, 0.5f);
            heartRect.sizeDelta = new Vector2(56f, 56f);
            heartRect.anchoredPosition = new Vector2(40f, 1.5f);
            var heartImg = heartObj.AddComponent<Image>();
            heartImg.sprite = heart;
            heartImg.preserveAspect = true;
            heartImg.color = GoldStar;
            heartImg.raycastTarget = false;
            var heartJuice = heartObj.AddComponent<HeartJuice>();

            var txtObj = new GameObject("Count");
            txtObj.transform.SetParent(pill.transform, false);
            var txtRect = txtObj.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = new Vector2(84f, 0f);
            txtRect.offsetMax = new Vector2(-16f, 0f);
            var txt = txtObj.AddComponent<TextMeshProUGUI>();
            txt.font = _fontTitle;
            txt.text = LivesManager.GetStoredLives().ToString();
            txt.fontSize = 44;
            txt.fontStyle = FontStyles.Bold;
            txt.color = NumberColor;
            txt.alignment = TextAlignmentOptions.MidlineRight;
            txt.raycastTarget = false;
            var txtShadow = txtObj.AddComponent<Shadow>();
            txtShadow.effectColor = new Color(0.15f, 0.12f, 0.10f, 0.12f);
            txtShadow.effectDistance = new Vector2(0f, -1.5f);
            _livesCountText = txt;

            var timerObj = new GameObject("Timer");
            timerObj.transform.SetParent(pill.transform, false);
            var timerRect = timerObj.AddComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0.5f, 0f);
            timerRect.anchorMax = new Vector2(0.5f, 0f);
            timerRect.pivot = new Vector2(0.5f, 1f);
            timerRect.sizeDelta = new Vector2(140f, 20f);
            timerRect.anchoredPosition = new Vector2(0f, -2f);
            _livesTimerText = timerObj.AddComponent<TextMeshProUGUI>();
            _livesTimerText.font = _fontBody;
            _livesTimerText.fontSize = 18;
            _livesTimerText.color = new Color(0.60f, 0.48f, 0.35f);
            _livesTimerText.alignment = TextAlignmentOptions.Center;
            _livesTimerText.raycastTarget = false;
            _livesTimerText.text = "";
        }

        private class HeartJuice : MonoBehaviour
        {
            void Update()
            {
                float s = 1f + Mathf.Sin(Time.unscaledTime * 2.2f) * 0.06f;
                transform.localScale = new Vector3(s, s, s);
            }
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
            scrollRectRT.offsetMax = new Vector2(0f, -(_headerTotal + FixedDailyHeight + GaugeHeight + 16f));

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
            topRect.anchoredPosition = new Vector2(0f, -(_headerTotal + FixedDailyHeight + GaugeHeight));
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

            // Fond principal : bleu pastel doux avec relief biseauté 3D.
            var img = go.AddComponent<Image>();
            img.sprite = CreerSpriteGradientArrondi(128, 0.35f, SeparatorBg, SeparatorBgLight);
            img.raycastTarget = false;

            // Biseau 3D : liseré clair en haut, ombre douce en bas.
            var bevelLightGO = new GameObject("BevelLight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bevelLightGO.transform.SetParent(go.transform, false);
            var bevelLightRect = bevelLightGO.GetComponent<RectTransform>();
            bevelLightRect.anchorMin = new Vector2(0f, 0.88f);
            bevelLightRect.anchorMax = new Vector2(1f, 1f);
            bevelLightRect.offsetMin = new Vector2(12f, 0f);
            bevelLightRect.offsetMax = new Vector2(-12f, 0f);
            var bevelLightImg = bevelLightGO.GetComponent<Image>();
            bevelLightImg.sprite = CreerSpriteArrondi(64, 0.35f);
            bevelLightImg.color = SeparatorBevelLight;
            bevelLightImg.raycastTarget = false;

            var bevelDarkGO = new GameObject("BevelDark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bevelDarkGO.transform.SetParent(go.transform, false);
            var bevelDarkRect = bevelDarkGO.GetComponent<RectTransform>();
            bevelDarkRect.anchorMin = new Vector2(0f, 0f);
            bevelDarkRect.anchorMax = new Vector2(1f, 0.12f);
            bevelDarkRect.offsetMin = new Vector2(12f, 0f);
            bevelDarkRect.offsetMax = new Vector2(-12f, 0f);
            var bevelDarkImg = bevelDarkGO.GetComponent<Image>();
            bevelDarkImg.sprite = CreerSpriteArrondi(64, 0.35f);
            bevelDarkImg.color = SeparatorBevelDark;
            bevelDarkImg.raycastTarget = false;

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

            var badgeGO = new GameObject("Badge", typeof(RectTransform));
            badgeGO.transform.SetParent(go.transform, false);
            var badgeRect = badgeGO.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0f, 0.5f);
            badgeRect.anchorMax = new Vector2(0f, 0.5f);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            badgeRect.sizeDelta = new Vector2(28f, 28f);
            badgeRect.anchoredPosition = new Vector2(24f, 0f);
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
            txt.text = Zoologic.Localization.LocalizationManager.Get("levelmap.grids", gridSize);
            Zoologic.Localization.LocalizationManager.ApplyTo(txt);
            txt.fontSize = 44;
            txt.fontStyle = FontStyles.Bold;
            txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.Center;
            txt.enableAutoSizing = true;
            txt.fontSizeMin = 32;
            txt.fontSizeMax = 44;
            txt.textWrappingMode = TextWrappingModes.NoWrap;
            txt.raycastTarget = false;
            txt.outlineWidth = 0.35f;
            txt.outlineColor = new Color(0f, 0f, 0f, 0.30f);
        }

        private void CreerDailyFixe(Transform canvas)
        {
            var fixedGO = new GameObject("FixedDaily");
            fixedGO.transform.SetParent(canvas, false);
            var rt = fixedGO.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, FixedDailyHeight);
            rt.anchoredPosition = new Vector2(0f, -_headerTotal);
            var pad = fixedGO.AddComponent<HorizontalLayoutGroup>();
            pad.padding = new RectOffset((int)ContentPad, (int)ContentPad, 8, 8);
            pad.childControlWidth = true;
            pad.childControlHeight = true;
            pad.childForceExpandWidth = true;
            pad.childForceExpandHeight = true;
            CreerCarteDefiDuJour(fixedGO.transform);
        }

        private void CreerCarteDefiDuJour(Transform overrideParent = null)
        {
            bool done = DailyPuzzleManager.IsCompletedToday();
            var go = new GameObject("DailyCard");
            go.transform.SetParent(overrideParent != null ? overrideParent : _content, false);
            var le = go.AddComponent<LayoutElement>();
            if (overrideParent != null) { le.flexibleHeight = 1f; le.flexibleWidth = 1f; }
            else { le.preferredHeight = 150f; le.flexibleWidth = 1f; }

            var bg = go.AddComponent<Image>();
            bg.sprite = CreerSpriteGradientArrondi(128, 0.30f,
                done ? new Color(0.82f, 0.79f, 0.74f) : new Color(1f, 0.60f, 0.10f),
                done ? new Color(0.90f, 0.88f, 0.84f) : new Color(1f, 0.80f, 0.28f));
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = false;
            var cardOl = go.AddComponent<Outline>();
            cardOl.effectColor = done ? new Color(1f, 1f, 1f, 0.5f) : new Color(1f, 1f, 1f, 0.85f);
            cardOl.effectDistance = new Vector2(2.5f, -2.5f);
            var cardSh = go.AddComponent<Shadow>();
            cardSh.effectColor = done ? new Color(0f, 0f, 0f, 0.12f) : new Color(0.55f, 0.28f, 0.05f, 0.35f);
            cardSh.effectDistance = new Vector2(0f, -6f);

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(32, 32, 28, 28);
            hlg.spacing = 20f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            // RTL arabe : miroir de lecture (étoile ↔ bouton).
            try { hlg.reverseArrangement = Zoologic.Localization.LocalizationManager.IsRTL; } catch { }

            // Icône trophée dans pastille blanche
            var iconGO = new GameObject("Trophy", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGO.transform.SetParent(go.transform, false);
            var iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(104f, 104f);
            var iconBg = iconGO.GetComponent<Image>();
            iconBg.sprite = CreerSpriteArrondi(64, 0.5f);
            iconBg.color = done ? new Color(1f, 1f, 1f, 0.6f) : Color.white;
            var iconLE = iconGO.AddComponent<LayoutElement>();
            iconLE.preferredWidth = 104f; iconLE.preferredHeight = 104f;
            iconLE.flexibleWidth = 0f; iconLE.flexibleHeight = 0f;
            var starGO = new GameObject("Star", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            starGO.transform.SetParent(iconGO.transform, false);
            var starRect = starGO.GetComponent<RectTransform>();
            starRect.anchorMin = Vector2.zero; starRect.anchorMax = Vector2.one;
            starRect.offsetMin = new Vector2(12f, 12f); starRect.offsetMax = new Vector2(-12f, -12f);
            var starImg = starGO.GetComponent<Image>();
            starImg.sprite = done
                ? (KenneyUI.Checkmark() ?? Resources.Load<Sprite>("UI/star"))
                : (Resources.Load<Sprite>("UI/star") ?? KenneyUI.Checkmark());
            starImg.preserveAspect = true;
            starImg.color = done ? new Color(0.55f, 0.52f, 0.48f) : Color.white;
            starImg.raycastTarget = false;

            var leftGO = new GameObject("Left", typeof(RectTransform));
            leftGO.transform.SetParent(go.transform, false);
            var leftLE = leftGO.AddComponent<LayoutElement>();
            leftLE.flexibleWidth = 1f;
            var leftVLG = leftGO.AddComponent<VerticalLayoutGroup>();
            leftVLG.spacing = 4f;
            leftVLG.childAlignment = TextAnchor.MiddleLeft;
            leftVLG.childForceExpandWidth = true;

            // Ligne titre : [Titre flexible][Pastille série] — fini les 3 lignes empilées.
            int puzzleStreak = DailyPuzzleManager.GetPuzzleStreak();
            bool streakAlive = DailyPuzzleManager.IsStreakAlive();
            string streakText = "";
            if (done && puzzleStreak > 1)
                streakText = Zoologic.Localization.LocalizationManager.Get("daily.puzzle_streak", puzzleStreak);
            else if (!done && streakAlive && puzzleStreak > 0)
                streakText = Zoologic.Localization.LocalizationManager.Get("daily.puzzle_streak_bonus", puzzleStreak + 1, DailyPuzzleManager.GetUpcomingBonus());
            var titleRowGO = new GameObject("TitleRow", typeof(RectTransform));
            titleRowGO.transform.SetParent(leftGO.transform, false);
            var titleRowHLG = titleRowGO.AddComponent<HorizontalLayoutGroup>();
            titleRowHLG.spacing = 12f;
            titleRowHLG.childAlignment = TextAnchor.MiddleLeft;
            titleRowHLG.childForceExpandWidth = false;
            titleRowHLG.childForceExpandHeight = false;
            titleRowHLG.childControlWidth = true;
            try { titleRowHLG.reverseArrangement = Zoologic.Localization.LocalizationManager.IsRTL; } catch { }
            var titleRowLE = titleRowGO.AddComponent<LayoutElement>();
            titleRowLE.preferredHeight = 52f;
            titleRowLE.flexibleWidth = 1f;

            var titleGO = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titleGO.transform.SetParent(titleRowGO.transform, false);
            var title = titleGO.GetComponent<TextMeshProUGUI>();
            title.font = _fontTitle;
            title.text = done ? Zoologic.Localization.LocalizationManager.Get("daily.daily_done") : Zoologic.Localization.LocalizationManager.Get("daily.challenge");
            Zoologic.Localization.LocalizationManager.ApplyTo(title);
            title.fontSize = 40;
            title.fontStyle = FontStyles.Bold;
            title.color = done ? new Color(0.45f, 0.42f, 0.38f) : Color.white;
            title.outlineWidth = done ? 0f : 0.18f;
            title.outlineColor = new Color(0.45f, 0.20f, 0.02f, 0.55f);
            title.alignment = TextAlignmentOptions.MidlineLeft;
            title.enableAutoSizing = true;
            title.fontSizeMin = 30;
            title.fontSizeMax = 40;
            try { title.isRightToLeftText = Zoologic.Localization.LocalizationManager.IsRTL; } catch { }
            title.raycastTarget = false;
            var titleLE2 = titleGO.AddComponent<LayoutElement>();
            titleLE2.flexibleWidth = 1f;
            titleLE2.preferredHeight = 52f;

            // Pastille série (ex-ligne dédiée) : compacte, à droite du titre.
            var streakPillGO = new GameObject("StreakPill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            streakPillGO.transform.SetParent(titleRowGO.transform, false);
            var streakPillImg = streakPillGO.GetComponent<Image>();
            streakPillImg.sprite = CreerSpriteArrondi(64, 0.5f);
            streakPillImg.color = new Color(1f, 1f, 1f, 0.92f);
            streakPillImg.raycastTarget = false;
            var streakPillLE = streakPillGO.AddComponent<LayoutElement>();
            streakPillLE.preferredWidth = 150f;
            streakPillLE.preferredHeight = 44f;
            streakPillLE.flexibleWidth = 0f;
            var streakPillTxtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            streakPillTxtGO.transform.SetParent(streakPillGO.transform, false);
            var streakPillTxtRect = streakPillTxtGO.GetComponent<RectTransform>();
            streakPillTxtRect.anchorMin = Vector2.zero;
            streakPillTxtRect.anchorMax = Vector2.one;
            streakPillTxtRect.offsetMin = new Vector2(12f, 0f);
            streakPillTxtRect.offsetMax = new Vector2(-12f, 0f);
            var streakPillTxt = streakPillTxtGO.GetComponent<TextMeshProUGUI>();
            streakPillTxt.font = _fontTitle;
            streakPillTxt.text = streakText;
            streakPillTxt.fontSize = 24;
            streakPillTxt.fontStyle = FontStyles.Bold;
            streakPillTxt.color = new Color(0.75f, 0.38f, 0.08f, 1f);
            streakPillTxt.alignment = TextAlignmentOptions.Center;
            streakPillTxt.enableAutoSizing = true;
            streakPillTxt.fontSizeMin = 18;
            streakPillTxt.fontSizeMax = 24;
            streakPillTxt.raycastTarget = false;
            streakPillGO.SetActive(!string.IsNullOrEmpty(streakText));

            var subGO = new GameObject("Sub", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            subGO.transform.SetParent(leftGO.transform, false);
            var sub = subGO.GetComponent<TextMeshProUGUI>();
            sub.font = _fontTitle;
            sub.text = done ? Zoologic.Localization.LocalizationManager.Get("daily.reward_tomorrow") : Zoologic.Localization.LocalizationManager.Get("daily.blurb", DailyPuzzleManager.GetTodaySize());
            Zoologic.Localization.LocalizationManager.ApplyTo(sub);
            sub.fontSize = 26;
            sub.fontStyle = FontStyles.Bold;
            sub.color = done ? new Color(0.55f, 0.52f, 0.48f) : new Color(0.45f, 0.22f, 0.03f);
            sub.alignment = TextAlignmentOptions.MidlineLeft;
            sub.enableAutoSizing = true;
            sub.fontSizeMin = 20;
            sub.fontSizeMax = 26;
            sub.textWrappingMode = TextWrappingModes.Normal;
            try { sub.isRightToLeftText = Zoologic.Localization.LocalizationManager.IsRTL; } catch { }
            sub.raycastTarget = false;
            var subLE = subGO.AddComponent<LayoutElement>();
            subLE.preferredHeight = 44f;

            // Pilule récompense : pièce + montant, explicite d'un coup d'œil.
            var rewardGO = new GameObject("RewardPill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rewardGO.transform.SetParent(go.transform, false);
            var rewardImg = rewardGO.GetComponent<Image>();
            rewardImg.sprite = CreerSpriteArrondi(64, 0.5f);
            rewardImg.color = new Color(1f, 1f, 1f, 0.92f);
            rewardImg.raycastTarget = false;
            var rewardLE = rewardGO.AddComponent<LayoutElement>();
            rewardLE.preferredWidth = 170f;
            rewardLE.preferredHeight = 88f;
            rewardLE.flexibleWidth = 0f;
            rewardLE.flexibleHeight = 0f;
            var rewardHLG = rewardGO.AddComponent<HorizontalLayoutGroup>();
            rewardHLG.padding = new RectOffset(12, 12, 6, 6);
            rewardHLG.spacing = 8f;
            rewardHLG.childAlignment = TextAnchor.MiddleCenter;
            rewardHLG.childForceExpandWidth = false;
            var rewardCoinGO = new GameObject("Coin", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rewardCoinGO.transform.SetParent(rewardGO.transform, false);
            var rewardCoinImg = rewardCoinGO.GetComponent<Image>();
            rewardCoinImg.sprite = Resources.Load<Sprite>("UI/coin");
            rewardCoinImg.preserveAspect = true;
            rewardCoinImg.raycastTarget = false;
            var rewardCoinLE = rewardCoinGO.AddComponent<LayoutElement>();
            rewardCoinLE.preferredWidth = 40f;
            rewardCoinLE.preferredHeight = 40f;
            var rewardTxtGO = new GameObject("Amount", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            rewardTxtGO.transform.SetParent(rewardGO.transform, false);
            var rewardTxt = rewardTxtGO.GetComponent<TextMeshProUGUI>();
            rewardTxt.font = _fontTitle;
            rewardTxt.text = "+" + (DailyPuzzleManager.IsCompletedToday()
                ? DailyPuzzleManager.RewardCoins
                : DailyPuzzleManager.RewardCoins + DailyPuzzleManager.GetUpcomingBonus());
            Zoologic.Localization.LocalizationManager.ApplyTo(rewardTxt);
            rewardTxt.fontSize = 32;
            rewardTxt.fontStyle = FontStyles.Bold;
            rewardTxt.color = new Color(0.361f, 0.251f, 0.216f, 1f);
            rewardTxt.alignment = TextAlignmentOptions.MidlineLeft;
            rewardTxt.raycastTarget = false;
            var rewardTxtLE = rewardTxtGO.AddComponent<LayoutElement>();
            rewardTxtLE.flexibleWidth = 1f;

            var btnGO = new GameObject("BtnDaily", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(go.transform, false);
            var btnRect = btnGO.GetComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(300f, 132f);
            var btnImg = btnGO.GetComponent<Image>();
            btnImg.sprite = KenneyUI.Button(done ? "Grey" : "Green") ?? CreerSpriteArrondi(128, 0.4f);
            btnImg.type = Image.Type.Sliced;
            // Spec D5 : vert profond #2E7D32 (5.1:1 AA), 300x132 ≥ 48dp.
            btnImg.color = done ? new Color(0.72f, 0.72f, 0.75f) : new Color(0.180f, 0.490f, 0.196f, 1f);
            var btnOl = btnGO.AddComponent<Outline>();
            btnOl.effectColor = new Color(1f, 1f, 1f, 0.7f);
            btnOl.effectDistance = new Vector2(2f, -2f);
            var btnSh = btnGO.AddComponent<Shadow>();
            btnSh.effectColor = new Color(0.30f, 0.15f, 0.02f, 0.35f);
            btnSh.effectDistance = new Vector2(0f, -4f);
            var btn = btnGO.GetComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.interactable = !done;
            var btnLE = btnGO.AddComponent<LayoutElement>();
            btnLE.preferredWidth = 300f;
            btnLE.preferredHeight = 132f;
            var btnTxtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            btnTxtGO.transform.SetParent(btnGO.transform, false);
            var btnTxtRect = btnTxtGO.GetComponent<RectTransform>();
            btnTxtRect.anchorMin = Vector2.zero;
            btnTxtRect.anchorMax = Vector2.one;
            btnTxtRect.offsetMin = Vector2.zero;
            btnTxtRect.offsetMax = Vector2.zero;
            var btnTxt = btnTxtGO.GetComponent<TextMeshProUGUI>();
            btnTxt.font = _fontTitle;
            btnTxt.text = done ? Zoologic.Localization.LocalizationManager.Get("daily.done") : Zoologic.Localization.LocalizationManager.Get("daily.play");
            btnTxt.enableAutoSizing = true;
            btnTxt.fontSizeMin = 28;
            btnTxt.fontSizeMax = 40;
            btnTxt.fontSize = 40;
            btnTxt.fontStyle = FontStyles.Bold;
            btnTxt.color = Color.white;
            btnTxt.outlineWidth = 0.12f;
            btnTxt.outlineColor = new Color(0f, 0f, 0f, 0.30f);
            btnTxt.alignment = TextAlignmentOptions.Center;
            btnTxt.raycastTarget = false;
            if (!done) go.AddComponent<DailyPulse>();
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

        // ------------------------------------------------------------------
        // Jauge Star Chests : fixe sous le défi + popup de claim.
        // ------------------------------------------------------------------

        private void CreerJaugeCoffres(Transform canvas)
        {
            var old = canvas.Find("ChestGauge");
            if (old != null) Destroy(old.gameObject);

            int world = StarChestManager.WorldOf(_currentLevel);
            int stars = StarChestManager.StarsOfWorld(world);

            var barGO = new GameObject("ChestGauge");
            barGO.transform.SetParent(canvas, false);
            var barRect = barGO.AddComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.sizeDelta = new Vector2(0f, GaugeHeight);
            barRect.anchoredPosition = new Vector2(0f, -(_headerTotal + FixedDailyHeight));

            var hlg = barGO.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(50, 50, 16, 16);
            hlg.spacing = 16f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // Bloc gauche : titre + progression vers le prochain palier.
            var leftGO = new GameObject("ChestInfo");
            leftGO.transform.SetParent(barGO.transform, false);
            var leftLE = leftGO.AddComponent<LayoutElement>();
            leftLE.flexibleWidth = 1f;
            var leftVLG = leftGO.AddComponent<VerticalLayoutGroup>();
            leftVLG.spacing = 6f;
            leftVLG.childAlignment = TextAnchor.MiddleLeft;
            leftVLG.childForceExpandWidth = true;

            var titleGO = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titleGO.transform.SetParent(leftGO.transform, false);
            var titleTxt = titleGO.GetComponent<TextMeshProUGUI>();
            titleTxt.font = _fontTitle;
            titleTxt.text = Zoologic.Localization.LocalizationManager.Get("chest.title");
            Zoologic.Localization.LocalizationManager.ApplyTo(titleTxt);
            titleTxt.fontSize = 30;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.color = TitleColor;
            titleTxt.alignment = TextAlignmentOptions.MidlineLeft;
            titleTxt.enableAutoSizing = true;
            titleTxt.fontSizeMin = 22;
            titleTxt.fontSizeMax = 30;
            titleTxt.raycastTarget = false;
            var titleLE = titleGO.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 38f;

            int target = -1;
            for (int p = 0; p < 3; p++)
            {
                if (!StarChestManager.IsClaimed(world, p)) { target = StarChestManager.Thresholds[p]; break; }
            }

            var progGO = new GameObject("Progress", typeof(RectTransform));
            progGO.transform.SetParent(leftGO.transform, false);
            var progHLG = progGO.AddComponent<HorizontalLayoutGroup>();
            progHLG.spacing = 10f;
            progHLG.childAlignment = TextAnchor.MiddleLeft;
            progHLG.childForceExpandWidth = false;
            var progLE = progGO.AddComponent<LayoutElement>();
            progLE.preferredHeight = 44f;
            var progTxtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            progTxtGO.transform.SetParent(progGO.transform, false);
            var progTxt = progTxtGO.GetComponent<TextMeshProUGUI>();
            progTxt.font = _fontTitle;
            progTxt.text = target < 0 ? "MAX" : stars + "/" + target;
            progTxt.fontSize = 32;
            progTxt.fontStyle = FontStyles.Bold;
            progTxt.color = TitleColor;
            progTxt.alignment = TextAlignmentOptions.MidlineLeft;
            progTxt.raycastTarget = false;
            var progTxtLE = progTxtGO.AddComponent<LayoutElement>();
            progTxtLE.preferredWidth = 170f;
            progTxtLE.preferredHeight = 44f;
            var progStarGO = new GameObject("Star", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            progStarGO.transform.SetParent(progGO.transform, false);
            var progStarImg = progStarGO.GetComponent<Image>();
            progStarImg.sprite = GetStarSprite();
            progStarImg.preserveAspect = true;
            progStarImg.color = GoldStar;
            progStarImg.raycastTarget = false;
            var progStarLE = progStarGO.AddComponent<LayoutElement>();
            progStarLE.preferredWidth = 40f;
            progStarLE.preferredHeight = 40f;

            // Barre de progression vers le prochain palier (remplissage animé).
            float nextTarget = target < 0 ? 1f : target;
            float fillInit = target < 0 ? 1f : Mathf.Clamp01((float)stars / nextTarget);
            var barBgGO = new GameObject("ChestProgressBg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            barBgGO.transform.SetParent(leftGO.transform, false);
            var barBgImg = barBgGO.GetComponent<Image>();
            barBgImg.sprite = GetRoundedRectSprite();
            barBgImg.type = Image.Type.Sliced;
            barBgImg.color = new Color(0.918f, 0.875f, 0.796f, 1f);
            barBgImg.raycastTarget = false;
            var barBgLE = barBgGO.AddComponent<LayoutElement>();
            barBgLE.preferredHeight = 24f;
            barBgLE.flexibleWidth = 1f;
            var barFillGO = new GameObject("ChestProgressFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            barFillGO.transform.SetParent(barBgGO.transform, false);
            var barFillRect = barFillGO.GetComponent<RectTransform>();
            barFillRect.anchorMin = new Vector2(0f, 0f);
            barFillRect.anchorMax = new Vector2(0f, 1f);
            barFillRect.pivot = new Vector2(0f, 0.5f);
            barFillRect.offsetMin = new Vector2(4f, 4f);
            barFillRect.offsetMax = new Vector2(0f, -4f);
            var barFillImg = barFillGO.GetComponent<Image>();
            barFillImg.sprite = GetRoundedRectSprite();
            barFillImg.type = Image.Type.Simple;
            barFillImg.color = new Color(0.494f, 0.839f, 0.627f, 1f);
            barFillImg.raycastTarget = false;
            StartCoroutine(ChestFillRoutine(barFillRect, barBgGO.GetComponent<RectTransform>(), fillInit));

            int firstUnclaimed = -1;
            for (int p = 0; p < 3; p++)
            {
                if (!StarChestManager.IsClaimed(world, p)) { firstUnclaimed = p; break; }
            }
            for (int p = 0; p < 3; p++)
                CreerBoutonCoffre(barGO.transform, world, p, stars, p == firstUnclaimed);
        }

        /// <summary>Remplissage animé 600ms EaseOutCubic (dopamine de progression).</summary>
        private IEnumerator ChestFillRoutine(RectTransform fill, RectTransform bg, float targetFill)
        {
            yield return null;
            if (fill == null || bg == null) yield break;
            float fullW = bg.rect.width - 8f;
            float elapsed = 0f;
            const float duration = 0.6f;
            while (elapsed < duration)
            {
                if (fill == null) yield break;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                fill.sizeDelta = new Vector2(Mathf.Max(0f, fullW * targetFill * eased), fill.sizeDelta.y);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            if (fill != null)
                fill.sizeDelta = new Vector2(Mathf.Max(0f, fullW * targetFill), fill.sizeDelta.y);
        }

        private void RefreshChestGauge()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            CreerJaugeCoffres(canvas.transform);
        }

        private void CreerBoutonCoffre(Transform parent, int world, int palier, int stars, bool isNext)
        {
            bool claimed = StarChestManager.IsClaimed(world, palier);
            bool ready = !claimed && stars >= StarChestManager.Thresholds[palier];
            // Prochain palier verrouillé : vivant quand même (incitation), tap = aperçu.
            bool tease = !claimed && !ready && isNext;
            Color tier = palier == 0 ? new Color(0.494f, 0.839f, 0.627f, 1f)
                : palier == 1 ? new Color(0.302f, 0.549f, 0.898f, 1f)
                : new Color(1f, 0.788f, 0.235f, 1f);

            var go = new GameObject("Chest" + palier);
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 128f;
            le.preferredHeight = 128f;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;

            var img = go.AddComponent<Image>();
            // Visuels générés (repli : boîte procédurale teintée par palier).
            Sprite art = ChestArt(palier);
            if (art != null)
            {
                img.sprite = art;
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
                img.color = claimed ? new Color(0.62f, 0.62f, 0.62f, 1f)
                    : ready || tease ? Color.white
                    : new Color(0.72f, 0.72f, 0.72f, 1f);
            }
            else
            {
                img.sprite = GetRoundedRectSprite();
                img.type = Image.Type.Simple;
                img.color = claimed ? new Color(0.914f, 0.886f, 0.835f, 1f)
                    : ready ? tier
                    : new Color(0.969f, 0.934f, 0.867f, 1f);
            }
            img.raycastTarget = ready || tease;
            if (ready || claimed)
            {
                var sh = go.AddComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.20f);
                sh.effectDistance = new Vector2(0f, -4f);
            }

            if (claimed)
            {
                var checkGO = new GameObject("Check", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                checkGO.transform.SetParent(go.transform, false);
                var checkRect = (RectTransform)checkGO.transform;
                checkRect.anchorMin = new Vector2(0.5f, 0.5f);
                checkRect.anchorMax = new Vector2(0.5f, 0.5f);
                checkRect.pivot = new Vector2(0.5f, 0.5f);
                checkRect.sizeDelta = new Vector2(64f, 64f);
                checkRect.anchoredPosition = Vector2.zero;
                var checkImg = checkGO.GetComponent<Image>();
                checkImg.sprite = KenneyUI.Checkmark() ?? GetStarSprite();
                checkImg.preserveAspect = true;
                checkImg.raycastTarget = false;
            }
            else
            {
                var numGO = new GameObject("Seuil", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                numGO.transform.SetParent(go.transform, false);
                var numRect = (RectTransform)numGO.transform;
                numRect.anchorMin = Vector2.zero;
                numRect.anchorMax = Vector2.one;
                numRect.offsetMin = Vector2.zero;
                numRect.offsetMax = new Vector2(0f, 18f);
                var numTxt = numGO.GetComponent<TextMeshProUGUI>();
                numTxt.font = _fontTitle;
                numTxt.text = StarChestManager.Thresholds[palier].ToString();
                numTxt.fontSize = 44;
                numTxt.fontStyle = FontStyles.Bold;
                numTxt.color = ready ? Color.white : new Color(0.373f, 0.326f, 0.278f, 1f);
                numTxt.alignment = TextAlignmentOptions.Center;
                numTxt.raycastTarget = false;
                if (ready)
                {
                    var ol = numGO.AddComponent<Outline>();
                    ol.effectColor = new Color(0f, 0f, 0f, 0.35f);
                    ol.effectDistance = new Vector2(2f, -2f);
                }
                var starGO = new GameObject("Star", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                starGO.transform.SetParent(go.transform, false);
                var starRect = (RectTransform)starGO.transform;
                starRect.anchorMin = new Vector2(0.5f, 0f);
                starRect.anchorMax = new Vector2(0.5f, 0f);
                starRect.pivot = new Vector2(0.5f, 0f);
                starRect.sizeDelta = new Vector2(34f, 34f);
                starRect.anchoredPosition = new Vector2(0f, 10f);
                var starImg = starGO.GetComponent<Image>();
                starImg.sprite = GetStarSprite();
                starImg.preserveAspect = true;
                starImg.color = ready ? Color.white : new Color(0.79f, 0.75f, 0.68f, 1f);
                starImg.raycastTarget = false;
            }

            if (tease && !ready)
            {
                // Teasing : respiration douce (le gros pulse reste réservé au prêt).
                var teaser = go.AddComponent<ChestTeaser>();
                teaser.Amplitude = 0.03f;
            }
            if (ready)
            {
                // Pastille "!" + pulse d'appel (stop au claim via rebuild).
                var badgeGO = new GameObject("Badge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                badgeGO.transform.SetParent(go.transform, false);
                var badgeRect = (RectTransform)badgeGO.transform;
                badgeRect.anchorMin = new Vector2(1f, 1f);
                badgeRect.anchorMax = new Vector2(1f, 1f);
                badgeRect.pivot = new Vector2(0.5f, 0.5f);
                badgeRect.sizeDelta = new Vector2(44f, 44f);
                badgeRect.anchoredPosition = new Vector2(-6f, -6f);
                var badgeImg = badgeGO.GetComponent<Image>();
                badgeImg.sprite = CreerSpriteArrondi(64, 0.5f);
                badgeImg.color = new Color(1f, 0.42f, 0.42f, 1f);
                badgeImg.raycastTarget = false;
                var bangGO = new GameObject("Bang", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                bangGO.transform.SetParent(badgeGO.transform, false);
                var bangRect = (RectTransform)bangGO.transform;
                bangRect.anchorMin = Vector2.zero;
                bangRect.anchorMax = Vector2.one;
                bangRect.offsetMin = Vector2.zero;
                bangRect.offsetMax = Vector2.zero;
                var bangTxt = bangGO.GetComponent<TextMeshProUGUI>();
                bangTxt.font = _fontTitle;
                bangTxt.text = "!";
                bangTxt.fontSize = 30;
                bangTxt.fontStyle = FontStyles.Bold;
                bangTxt.color = Color.white;
                bangTxt.alignment = TextAlignmentOptions.Center;
                bangTxt.raycastTarget = false;
                go.AddComponent<ChestPulse>();
                var btn = go.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => ShowChestPopup(world, palier, false));
            }
            else if (tease)
            {
                // Verrouillé mais prochain : tap = aperçu du contenu + manque.
                var btn = go.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => ShowChestPopup(world, palier, true));
            }
        }

        /// <summary>Respiration douce du prochain palier (le gros pulse = prêt).</summary>
        private class ChestTeaser : MonoBehaviour
        {
            public float Amplitude = 0.03f;
            private void Update()
            {
                if (this == null) return;
                float s = 1f + (Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI * 0.5f) * 0.5f + 0.5f) * Amplitude;
                transform.localScale = new Vector3(s, s, s);
            }
        }

        /// <summary>Visuel généré du coffre (Coffre_Mousse/Corail/Aurore).</summary>
        private static Sprite ChestArt(int palier)
        {
            string file = palier == 0 ? "Coffre_Mousse" : palier == 1 ? "Coffre_Corail" : "Coffre_Aurore";
            try { return Resources.Load<Sprite>("Sprites/" + file); } catch { return null; }
        }

        private class ChestPulse : MonoBehaviour
        {
            private void Update()
            {
                if (this == null) return;
                float s = 1f + (Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI) * 0.5f + 0.5f) * 0.06f;
                transform.localScale = new Vector3(s, s, s);
            }
        }

        private static Color RarityBg(string r) => r switch
        {
            "C" => new Color(0.910f, 0.957f, 0.910f, 1f),
            "R" => new Color(0.890f, 0.941f, 1.000f, 1f),
            "E" => new Color(0.953f, 0.910f, 1.000f, 1f),
            _ => new Color(1.000f, 0.953f, 0.839f, 1f),
        };

        private static Color RarityText(string r) => r switch
        {
            "C" => new Color(0.184f, 0.239f, 0.180f, 1f),
            "R" => new Color(0.141f, 0.204f, 0.302f, 1f),
            "E" => new Color(0.227f, 0.165f, 0.353f, 1f),
            _ => new Color(0.290f, 0.204f, 0.063f, 1f),
        };

        private static string RarityKey(string r) => "mascot.rarity." + r switch
        {
            "C" => "common",
            "R" => "rare",
            "E" => "epic",
            _ => "legendary",
        };

        /// <summary>Popup de claim : aperçu puis résultat (mascotte révélée).</summary>
        /// <param name="previewOnly">Coffre verrouillé : aperçu + manque, sans claim.</param>
        private void ShowChestPopup(int world, int palier, bool previewOnly = false)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            UiInputGuard.EnsureSingleEventSystem();
            SFXManager.Instance.PlayMenuOpen();

            var root = new GameObject("ChestPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(canvas.transform, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            var rootImg = root.GetComponent<Image>();
            rootImg.color = new Color(0.24f, 0.15f, 0.11f, 0.62f);
            rootImg.raycastTarget = true;
            root.transform.SetAsLastSibling();

            var card = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            card.transform.SetParent(root.transform, false);
            var cardRect = card.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(700f, 640f);
            cardRect.anchoredPosition = Vector2.zero;
            var cardImg = card.GetComponent<Image>();
            cardImg.sprite = B1UI.Bubble ?? GetRoundedRectSprite();
            cardImg.type = Image.Type.Sliced;
            cardImg.color = new Color(1f, 0.98f, 0.95f, 1f);
            cardImg.raycastTarget = false;
            var cardShadow = card.AddComponent<Shadow>();
            cardShadow.effectColor = new Color(0.18f, 0.11f, 0.06f, 0.32f);
            cardShadow.effectDistance = new Vector2(0f, -10f);

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(32, 32, 28, 28);
            vlg.spacing = 16f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            // Carte auto-ajustée au contenu (titre + 2-4 lignes + CTA) : fini
            // les débordements (titre éjecté, CTA à cheval). Largeur fixe 700.
            var fitter = card.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var titleGO = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titleGO.transform.SetParent(card.transform, false);
            var titleTxt = titleGO.GetComponent<TextMeshProUGUI>();
            titleTxt.font = _fontTitle;
            titleTxt.text = Zoologic.Localization.LocalizationManager.Get("chest.title") + " · " + StarChestManager.Thresholds[palier];
            Zoologic.Localization.LocalizationManager.ApplyTo(titleTxt);
            titleTxt.fontSize = 40;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.color = TitleColor;
            titleTxt.alignment = TextAlignmentOptions.Center;
            titleTxt.enableAutoSizing = true;
            titleTxt.fontSizeMin = 28;
            titleTxt.fontSizeMax = 40;
            titleTxt.raycastTarget = false;
            var titleLE = titleGO.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 56f;
            titleLE.flexibleWidth = 1f;

            var rowsGO = new GameObject("Rows", typeof(RectTransform));
            rowsGO.transform.SetParent(card.transform, false);
            var rowsVLG = rowsGO.AddComponent<VerticalLayoutGroup>();
            rowsVLG.spacing = 12f;
            rowsVLG.childAlignment = TextAnchor.MiddleCenter;
            rowsVLG.childForceExpandWidth = true;
            rowsVLG.childForceExpandHeight = false;
            var rowsLE = rowsGO.AddComponent<LayoutElement>();
            rowsLE.flexibleWidth = 1f;
            rowsLE.flexibleHeight = 1f;

            var ctaGO = new GameObject("BtnClaim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            ctaGO.transform.SetParent(card.transform, false);
            var ctaImg = ctaGO.GetComponent<Image>();
            var ctaNormal = JellyUI.ButtonGreen;
            ctaImg.sprite = ctaNormal ?? GetRoundedRectSprite();
            ctaImg.type = Image.Type.Sliced;
            ctaImg.color = Color.white;
            var ctaBtn = ctaGO.GetComponent<Button>();
            ctaBtn.targetGraphic = ctaImg;
            JellyUI.ApplyJellyButton(ctaBtn, ctaImg, ctaNormal, JellyUI.ButtonYellow, JellyUI.ButtonRed, JellyUI.ButtonGrey);
            var ctaLE = ctaGO.AddComponent<LayoutElement>();
            ctaLE.preferredHeight = 96f;
            ctaLE.flexibleWidth = 1f;
            var ctaTxtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            ctaTxtGO.transform.SetParent(ctaGO.transform, false);
            var ctaTxtRect = (RectTransform)ctaTxtGO.transform;
            ctaTxtRect.anchorMin = Vector2.zero;
            ctaTxtRect.anchorMax = Vector2.one;
            ctaTxtRect.offsetMin = Vector2.zero;
            ctaTxtRect.offsetMax = Vector2.zero;
            var ctaTxt = ctaTxtGO.GetComponent<TextMeshProUGUI>();
            ctaTxt.font = _fontTitle;
            ctaTxt.text = Zoologic.Localization.LocalizationManager.Get("missions.claim");
            ctaTxt.fontSize = 34;
            ctaTxt.fontStyle = FontStyles.Bold;
            ctaTxt.color = Color.white;
            ctaTxt.alignment = TextAlignmentOptions.Center;
            ctaTxt.raycastTarget = false;

            BuildChestPreviewRows(rowsGO.transform, world, palier);
            StartCoroutine(PopupPopRoutine(card.transform));
            if (previewOnly)
            {
                // Aperçu incitatif : contenu + étoiles manquantes, CTA = fermer.
                int missing = Mathf.Max(0, StarChestManager.Thresholds[palier] - StarChestManager.StarsOfWorld(world));
                BuildRewardRow(rowsGO.transform, GetStarSprite(), "+" + missing,
                    new Color(0.55f, 0.32f, 0.08f, 1f), GoldStar);
                ctaTxt.text = Zoologic.Localization.LocalizationManager.Get("menu.ok");
                ctaBtn.onClick.AddListener(() =>
                {
                    SFXManager.Instance.PlayMenuClose();
                    Destroy(root);
                });
                return;
            }
            ctaBtn.onClick.AddListener(() =>
            {
                SFXManager.Instance.PlayUnlock();
                Zoologic.Haptics.VibrateLight();
                StarChestManager.Grant g = StarChestManager.Execute(world, palier);
                for (int i = rowsGO.transform.childCount - 1; i >= 0; i--)
                    Destroy(rowsGO.transform.GetChild(i).gameObject);
                BuildChestResultRows(rowsGO.transform, g);
                ctaTxt.text = Zoologic.Localization.LocalizationManager.Get("menu.ok");
                ctaBtn.onClick.RemoveAllListeners();
                ctaBtn.onClick.AddListener(() =>
                {
                    SFXManager.Instance.PlayMenuClose();
                    Destroy(root);
                    RefreshChestGauge();
                });
                ConfettiHelper.Burst(this, canvas, 40);
                StartCoroutine(PunchRowsRoutine(rowsGO.transform));
            });
        }

        /// <summary>Lignes d'aperçu : pièces + indices (overflow déjà converti).</summary>
        private void BuildChestPreviewRows(Transform parent, int world, int palier)
        {
            StarChestManager.Grant preview = StarChestManager.Preview(world, palier);
            int free = Mathf.Max(0, HintStockManager.MaxStock - HintStockManager.Get());
            int hintsShown = Mathf.Min(preview.Hints, free);
            int coinsShown = preview.Coins + HintOverflowValue * (preview.Hints - hintsShown);
            BuildRewardRow(parent, Resources.Load<Sprite>("UI/coin"), "+" + coinsShown,
                new Color(0.55f, 0.32f, 0.08f, 1f));
            if (hintsShown > 0)
                BuildRewardRow(parent, LoadHintMedal(), "×" + hintsShown,
                    new Color(0.29f, 0.18f, 0.10f, 1f));
            if (palier >= 1)
                BuildMascotPreviewRow(parent, palier);
        }

        /// <summary>Ligne mascotte intuitive : une face par rareté tirage
        /// (non-possédées uniquement) + label. Rien si pools épuisés (doublon
        /// déjà converti en pièces). Fini le "?" cryptique.</summary>
        private void BuildMascotPreviewRow(Transform parent, int palier)
        {
            string[] rarities = palier == 2
                ? new[] { "R", "E", "L" }
                : new[] { "C", "R" };
            var faces = new System.Collections.Generic.List<Sprite>();
            foreach (string r in rarities)
            {
                int idx = StarChestManager.FirstUnowned(r);
                if (idx < 0) continue;
                Sprite f = StarChestManager.MascotFace(idx);
                if (f != null) faces.Add(f);
            }
            if (faces.Count == 0) return;

            var rowGO = new GameObject("MascotPreview", typeof(RectTransform));
            rowGO.transform.SetParent(parent, false);
            var rowHLG = rowGO.AddComponent<HorizontalLayoutGroup>();
            rowHLG.spacing = 12f;
            rowHLG.childAlignment = TextAnchor.MiddleCenter;
            rowHLG.childForceExpandWidth = false;
            var rowLE = rowGO.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 84f;
            rowLE.flexibleWidth = 1f;
            foreach (Sprite f in faces)
            {
                var iconGO = new GameObject("Face", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconGO.transform.SetParent(rowGO.transform, false);
                var iconLE = iconGO.AddComponent<LayoutElement>();
                iconLE.preferredWidth = 68f;
                iconLE.preferredHeight = 68f;
                var iconImg = iconGO.GetComponent<Image>();
                iconImg.sprite = f;
                iconImg.preserveAspect = true;
                // Aperçu = non-possédés par construction : silhouettes, comme
                // en collection. Fini les mascottes affichées "débloquées".
                iconImg.color = new Color(0.10f, 0.09f, 0.11f, 0.45f);
                iconImg.raycastTarget = false;
            }
            var txtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            txtGO.transform.SetParent(rowGO.transform, false);
            var txt = txtGO.GetComponent<TextMeshProUGUI>();
            txt.font = _fontTitle;
            txt.text = Zoologic.Localization.LocalizationManager.Get("collection.tab_mascots");
            txt.fontSize = 30;
            txt.fontStyle = FontStyles.Bold;
            txt.color = new Color(0.29f, 0.18f, 0.10f, 1f);
            txt.alignment = TextAlignmentOptions.Center;
            txt.raycastTarget = false;
            txt.enableAutoSizing = true;
            txt.fontSizeMin = 20;
            txt.fontSizeMax = 30;
            var txtLE = txtGO.AddComponent<LayoutElement>();
            txtLE.flexibleWidth = 1f;
        }

        private const int HintOverflowValue = 20;

        private Sprite LoadHintMedal()
        {
            try
            {
                return Resources.LoadAll<Sprite>("Sprites/hi").FirstOrDefault(s => s.name == "hi_0");
            }
            catch { return null; }
        }

        private void BuildRewardRow(Transform parent, Sprite icon, string text, Color textColor, Color iconTint = default)
        {
            var rowGO = new GameObject("RewardRow", typeof(RectTransform));
            rowGO.transform.SetParent(parent, false);
            var rowHLG = rowGO.AddComponent<HorizontalLayoutGroup>();
            rowHLG.spacing = 16f;
            rowHLG.childAlignment = TextAnchor.MiddleCenter;
            rowHLG.childForceExpandWidth = false;
            var rowLE = rowGO.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 84f;
            rowLE.flexibleWidth = 1f;
            if (icon != null)
            {
                var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconGO.transform.SetParent(rowGO.transform, false);
                var iconLE = iconGO.AddComponent<LayoutElement>();
                iconLE.preferredWidth = 64f;
                iconLE.preferredHeight = 64f;
                var iconImg = iconGO.GetComponent<Image>();
                iconImg.sprite = icon;
                iconImg.preserveAspect = true;
                iconImg.color = iconTint == default ? Color.white : iconTint;
                iconImg.raycastTarget = false;
            }
            var txtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            txtGO.transform.SetParent(rowGO.transform, false);
            var txt = txtGO.GetComponent<TextMeshProUGUI>();
            txt.font = _fontTitle;
            txt.text = text;
            txt.fontSize = 38;
            txt.fontStyle = FontStyles.Bold;
            txt.color = textColor;
            txt.alignment = TextAlignmentOptions.Center;
            txt.raycastTarget = false;
            var txtLE = txtGO.AddComponent<LayoutElement>();
            txtLE.flexibleWidth = 1f;
        }

        /// <summary>Lignes résultat : totaux + mascotte révélée (pop).</summary>
        private void BuildChestResultRows(Transform parent, StarChestManager.Grant g)
        {
            if (g.Coins > 0)
                BuildRewardRow(parent, Resources.Load<Sprite>("UI/coin"), "+" + g.Coins,
                    new Color(0.55f, 0.32f, 0.08f, 1f));
            if (g.Hints > 0)
                BuildRewardRow(parent, LoadHintMedal(), "×" + g.Hints,
                    new Color(0.29f, 0.18f, 0.10f, 1f));
            if (g.MascotIndex >= 0)
            {
                var face = StarChestManager.MascotFace(g.MascotIndex);
                var rowGO = new GameObject("MascotRow", typeof(RectTransform));
                rowGO.transform.SetParent(parent, false);
                var rowHLG = rowGO.AddComponent<HorizontalLayoutGroup>();
                rowHLG.spacing = 16f;
                rowHLG.childAlignment = TextAnchor.MiddleCenter;
                rowHLG.childForceExpandWidth = false;
                var rowLE = rowGO.AddComponent<LayoutElement>();
                rowLE.preferredHeight = 150f;
                rowLE.flexibleWidth = 1f;
                var faceGO = new GameObject("Face", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                faceGO.transform.SetParent(rowGO.transform, false);
                var faceLE = faceGO.AddComponent<LayoutElement>();
                faceLE.preferredWidth = 140f;
                faceLE.preferredHeight = 140f;
                var faceImg = faceGO.GetComponent<Image>();
                faceImg.sprite = face ?? GetStarSprite();
                faceImg.preserveAspect = true;
                faceImg.color = face != null ? Color.white : RarityBg(g.MascotRarity);
                faceImg.raycastTarget = false;
                var nameGO = new GameObject("Rarity", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                nameGO.transform.SetParent(rowGO.transform, false);
                var nameTxt = nameGO.GetComponent<TextMeshProUGUI>();
                nameTxt.font = _fontTitle;
                nameTxt.text = Zoologic.Localization.LocalizationManager.Get(RarityKey(g.MascotRarity));
                nameTxt.fontSize = 34;
                nameTxt.fontStyle = FontStyles.Bold;
                nameTxt.color = RarityText(g.MascotRarity);
                nameTxt.alignment = TextAlignmentOptions.Center;
                nameTxt.raycastTarget = false;
                var nameLE = nameGO.AddComponent<LayoutElement>();
                nameLE.flexibleWidth = 1f;
            }
        }

        private IEnumerator PopupPopRoutine(Transform card)
        {
            card.localScale = new Vector3(0.7f, 0.7f, 1f);
            const float duration = 0.32f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                float s = 0.7f + 0.3f * Zoologic.Easing.EaseOutBack(t);
                card.localScale = new Vector3(s, s, 1f);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            card.localScale = Vector3.one;
        }

        private IEnumerator PunchRowsRoutine(Transform rows)
        {
            for (int i = 0; i < rows.childCount; i++)
            {
                var rt = rows.GetChild(i) as RectTransform;
                if (rt != null)
                    Zoologic.Punch.Scale(this, rt, 1.10f, 0.25f);
                SFXManager.Instance.PlayUnlock();
                yield return new WaitForSecondsRealtime(0.12f);
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

        private class DailyPulse : MonoBehaviour
        {
            private Vector3 _base;
            private void Awake() { _base = transform.localScale; }
            private void Update()
            {
                if (transform == null) return;
                float s = 1f + Mathf.Sin(Time.unscaledTime * 3.2f) * 0.025f;
                transform.localScale = _base * s;
            }
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
            if (isCurrent)
            {
                bubbleImg.sprite = CreerSpriteGradientArrondi(256, 0.22f, CurrentLevelGradientBottom, CurrentLevelGradientTop);
                bubbleImg.type = Image.Type.Simple;
                bubbleImg.color = Color.white;
            }
            else if (unlocked)
            {
                bubbleImg.color = SuccessBg;
            }
            else
            {
                bubbleImg.color = BubbleLocked;
            }
            bubbleImg.raycastTarget = unlocked;

            if (unlocked)
            {
                var btn = bubbleGO.AddComponent<Button>();
                btn.targetGraphic = bubbleImg;
                btn.transition = Selectable.Transition.ColorTint;
                var colors = btn.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, 0.96f, 0.85f);
                colors.pressedColor = new Color(1f, 0.88f, 0.70f);
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

            if (unlocked)
            {
                CreerTexteNiveau(bubbleGO.transform, level, isCurrent ? CurrentNumber : NumberColor);
                CreerEtoiles(bubbleGO.transform, stars, unlocked);
                CreerBordureBasse(bubbleGO.transform, isCurrent ? CurrentEdge : SuccessEdge);
                if (isCurrent)
                    CreerBadgeEnCours(bubbleGO.transform);
            }
            else
            {
                // Spec bloqué : chiffre fantôme + cadenas 3D 96px bas-droite.
                // Nommé NumGhost : le lint UX le classe décoratif (seuil 2.0).
                CreerTexteNiveau(bubbleGO.transform, level, NumberLockedColor, "NumGhost");
                CreerCadenas(bubbleGO.transform);
                CreerBordureBasse(bubbleGO.transform, LockedEdge);
            }

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

        private void CreerTexteNiveau(Transform parent, int level, Color color, string objName = "Num")
        {
            var txtGO = new GameObject(objName);
            txtGO.transform.SetParent(parent, false);
            var rect = txtGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.05f, 0.42f);
            rect.anchorMax = new Vector2(0.95f, 0.88f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var txt = txtGO.AddComponent<TextMeshProUGUI>();
            txt.font = _fontTitle;
            txt.text = level.ToString();
            txt.fontSize = 96;
            txt.fontStyle = FontStyles.Bold;
            txt.color = color;
            txt.alignment = TextAlignmentOptions.Center;
            txt.enableAutoSizing = true;
            txt.fontSizeMin = 64f;
            txt.fontSizeMax = 120f;
            txt.raycastTarget = false;
        }

        /// <summary>Bordure basse 3D 12px : relief clay sous chaque carte.</summary>
        private void CreerBordureBasse(Transform parent, Color color)
        {
            var edgeGO = new GameObject("BottomEdge");
            edgeGO.transform.SetParent(parent, false);
            var rect = edgeGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.08f, 0.02f);
            rect.anchorMax = new Vector2(0.92f, 0.12f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = edgeGO.AddComponent<Image>();
            img.sprite = GetRoundedRectSprite();
            img.color = color;
            img.raycastTarget = false;
            edgeGO.transform.SetSiblingIndex(1);
        }

        /// <summary>Badge "• EN COURS •" sur la carte du niveau courant.</summary>
        private void CreerBadgeEnCours(Transform parent)
        {
            var badgeGO = new GameObject("CurrentBadge");
            badgeGO.transform.SetParent(parent, false);
            var rect = badgeGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(200f, 40f);
            rect.anchoredPosition = new Vector2(0f, -4f);
            var bg = badgeGO.AddComponent<Image>();
            bg.sprite = GetRoundedRectSprite();
            bg.color = new Color(1f, 1f, 1f, 0.95f);
            bg.raycastTarget = false;
            var txtGO = new GameObject("Text");
            txtGO.transform.SetParent(badgeGO.transform, false);
            var txtRect = txtGO.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;
            var txt = txtGO.AddComponent<TextMeshProUGUI>();
            txt.font = _fontTitle;
            txt.text = "• " + Zoologic.Localization.LocalizationManager.Get("levelmap.current") + " •";
            txt.fontSize = 28;
            txt.fontStyle = FontStyles.Bold;
            txt.color = new Color(0.180f, 0.490f, 0.196f, 1f);
            txt.alignment = TextAlignmentOptions.Center;
            txt.enableAutoSizing = true;
            txt.fontSizeMin = 20;
            txt.fontSizeMax = 30;
            txt.raycastTarget = false;
        }

        private void CreerEtoiles(Transform parent, int starCount, bool unlocked)
        {
            if (!unlocked) return;

            var starsGO = new GameObject("Stars");
            starsGO.transform.SetParent(parent, false);
            var rect = starsGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.05f, 0.10f);
            rect.anchorMax = new Vector2(0.95f, 0.38f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var hlg = starsGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            Sprite starSprite = GetStarSprite();

            for (int i = 0; i < 3; i++)
            {
                var starGO = new GameObject("Star_" + i);
                starGO.transform.SetParent(starsGO.transform, false);
                var starImg = starGO.AddComponent<Image>();
                bool earned = i < starCount;
                starImg.sprite = earned ? starSprite : GridView.StarGrey;
                starImg.preserveAspect = true;
                starImg.color = earned ? GoldStar : new Color(1f, 1f, 1f, 0.55f);
                starImg.raycastTarget = false;
                if (earned)
                {
                    // Contour or #8D5A00 : lisible au soleil (deutan-safe).
                    var outline = starGO.AddComponent<Outline>();
                    outline.effectColor = StarOutline;
                    outline.effectDistance = new Vector2(2f, -2f);
                }

                var starLE = starGO.AddComponent<LayoutElement>();
                starLE.preferredWidth = 80f;
                starLE.preferredHeight = 80f;
            }
        }

        private void CreerCadenas(Transform parent)
        {
            // Spec bloqué : cadenas 3D 96px bas-droite (chiffre fantôme reste visible).
            var lockGO = new GameObject("Lock");
            lockGO.transform.SetParent(parent, false);
            var rect = lockGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.66f, 0.24f);
            rect.anchorMax = new Vector2(0.66f, 0.24f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(96f, 96f);
            rect.anchoredPosition = Vector2.zero;

            var lockImg = lockGO.AddComponent<Image>();
            // cadena.png = cadenas clay 3D (vraies couleurs, blanc). Les tuiles
            // déjà cuites ne sont jamais teintées (taupe = procédural seul).
            Sprite lockAsset = Resources.Load<Sprite>("Sprites/cadena")
                ?? Resources.Load<Sprite>("UI/Icons/level_locked")
                ?? Resources.Load<Sprite>("UI/level_locked");
            if (lockAsset != null)
            {
                lockImg.sprite = lockAsset;
                lockImg.color = Color.white;
            }
            else
            {
                lockImg.sprite = CreerCadenasSprite();
                lockImg.color = LockColor;
            }
            lockImg.preserveAspect = true;
            lockImg.raycastTarget = false;
            var lockShadow = lockGO.AddComponent<Shadow>();
            lockShadow.effectColor = new Color(0f, 0f, 0f, 0.30f);
            lockShadow.effectDistance = new Vector2(0f, -5f);
        }

        private GameObject CreerGlowBorder(Transform parent)
        {
            var glowOuterGO = new GameObject("GlowOuter");
            glowOuterGO.transform.SetParent(parent, false);
            var outerRect = glowOuterGO.AddComponent<RectTransform>();
            outerRect.anchorMin = Vector2.zero;
            outerRect.anchorMax = Vector2.one;
            outerRect.offsetMin = new Vector2(-14f, -14f);
            outerRect.offsetMax = new Vector2(14f, 14f);
            var outerImg = glowOuterGO.AddComponent<Image>();
            outerImg.sprite = CreerSpriteArrondi(128, 0.28f);
            outerImg.color = new Color(CurrentLevelGlow.r, CurrentLevelGlow.g, CurrentLevelGlow.b, 0.18f);
            outerImg.raycastTarget = false;
            glowOuterGO.transform.SetAsFirstSibling();

            var glowGO = new GameObject("Glow");
            glowGO.transform.SetParent(parent, false);
            var rect = glowGO.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-6f, -6f);
            rect.offsetMax = new Vector2(6f, 6f);

            var img = glowGO.AddComponent<Image>();
            img.sprite = CreerSpriteArrondi(128, 0.28f);
            img.color = CurrentLevelGlow;
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
            // Singleton : un double-tap créait 2 popups empilés et "Fermer"
            // semblait mort (il fermait celle du dessus seulement).
            var prev = canvas.transform.Find("LivesPopup");
            if (prev != null) Destroy(prev.gameObject);
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
            cRect.sizeDelta = new Vector2(600f, 360f);
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
            title.text = Zoologic.Localization.LocalizationManager.Get("levelmap.no_lives");
            title.fontSize = 36;
            title.fontStyle = FontStyles.Bold;
            title.color = new Color(0.29f, 0.18f, 0.10f);
            title.alignment = TextAlignmentOptions.Center;
            var titleLE = titleGO.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 46f;
            var timerGO = new GameObject("Timer", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            timerGO.transform.SetParent(card.transform, false);
            var timer = timerGO.GetComponent<TextMeshProUGUI>();
            timer.font = _fontBody;
            int secs = LivesManager.GetSecondsUntilNextLife();
            timer.text = secs > 0 ? Zoologic.Localization.LocalizationManager.Get("levelmap.next_life", secs / 60, secs % 60) : Zoologic.Localization.LocalizationManager.Get("levelmap.full_lives");
            timer.fontSize = 22;
            timer.color = new Color(0.60f, 0.48f, 0.35f);
            timer.alignment = TextAlignmentOptions.Center;
            var timerLE = timerGO.AddComponent<LayoutElement>();
            timerLE.preferredHeight = 24f;
            var btnRow = new GameObject("BtnRow", typeof(RectTransform));
            btnRow.transform.SetParent(card.transform, false);
            var rowLE = btnRow.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 72f;
            var hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            var pubBtn = CreateLivesButton(btnRow.transform, Zoologic.Localization.LocalizationManager.Get("levelmap.watch_ad"), new Color(0.22f, 0.65f, 0.30f), () =>
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
                // Families: no reward without a real ad view.
                if (admob2 != null && admob2.IsRewardedReady()) admob2.ShowRewarded(grant2, () => SFXManager.Instance.PlayFailure());
                else SFXManager.Instance.PlayFailure();
            });
            pubBtn.gameObject.SetActive(AdMobManager.AreAdsAllowed());
            var closeBtn = CreateLivesButton(btnRow.transform, "Fermer", new Color(0.75f, 0.75f, 0.78f), () => Destroy(root));
            root.AddComponent<PopupCloser>().Init(root);
            Zoologic.Localization.LocalizationManager.ApplyFontsToScene();
        }

        private Button CreateLivesButton(Transform parent, string label, Color bg, System.Action onClick)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(240f, 64f);
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
            txt.fontSize = 24;
            txt.fontStyle = FontStyles.Bold;
            txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.Center;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 240f;
            le.preferredHeight = 64f;
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

        /// <summary>Cadenas procédural de secours (blanc pur, à teinter taupe).</summary>
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

        private static Sprite GetBackIconSprite()
        {
            var s = Resources.Load<Sprite>("UI/Icons/back");
            if (s != null) return s;
            s = Resources.Load<Sprite>("UI/back");
            if (s != null) return s;
            return CreerFlecheRetourSprite();
        }

        /// <summary>Chevron retour "‹" épais (lisible, teintable). Remplace l'ancien
        /// blob disque+barres illisible.</summary>
        private static Sprite CreerFlecheRetourSprite()
        {
            int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);

            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                    tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));

            float cx = s * 0.5f;
            float cy = s * 0.5f;
            float half = 5f;
            // Apex à gauche, bras vers la droite (haut et bas).
            float ax = cx - 13f, ay = cy;
            float b1x = cx + 13f, b1y = cy - 22f;
            float b2x = cx + 13f, b2y = cy + 22f;

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    if (DistToSegment(px, py, ax, ay, b1x, b1y) <= half
                        || DistToSegment(px, py, ax, ay, b2x, b2y) <= half)
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, 1f));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        }

        private static float DistToSegment(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax;
            float dy = by - ay;
            float len2 = dx * dx + dy * dy;
            float t = len2 > 0f ? ((px - ax) * dx + (py - ay) * dy) / len2 : 0f;
            t = Mathf.Clamp01(t);
            float qx = ax + t * dx - px;
            float qy = ay + t * dy - py;
            return Mathf.Sqrt(qx * qx + qy * qy);
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
