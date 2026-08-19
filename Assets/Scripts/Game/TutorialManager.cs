using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zoodoku.Core;

namespace Zoodoku
{
    /// <summary>
    /// Mode Tutoriel : enseigne les règles du Zoodoku sur une grille 4x4 fixe, étape
    /// par étape, avec des messages, des surbrillances pulsantes et des interactions
    /// guidées.
    ///
    /// Séquence :
    ///   1. La règle de zone (un seul pion par zone colorée) ;
    ///   2. Les lignes et les colonnes (jamais deux pions alignés) ;
    ///   3. La diagonale adjacente (deux pions ne se touchent pas par un coin) ;
    ///   4. La déduction : trois pions pré-posés, un seul emplacement valide restant.
    ///
    /// Le câblage est entièrement automatique (comme <see cref="PuzzleGameController"/>) :
    /// le composant crée le canvas, l'EventSystem, le fond dégradé et le panneau de
    /// message s'ils n'existent pas déjà.
    /// </summary>
    public sealed class TutorialManager : MonoBehaviour
    {
        // Grille fixe du tutoriel : 4 zones carrées de 2x2.
        private static readonly int[,] TutorialRegions =
        {
            { 0, 0, 1, 1 },
            { 0, 0, 1, 1 },
            { 2, 2, 3, 3 },
            { 2, 2, 3, 3 },
        };

        // Solution connue : (0,1), (1,3), (2,0), (3,2). Trois pions sont pré-posés à
        // l'étape 4 ; il ne reste qu'UNE case valide pour déduire le dernier.
        private static readonly (int row, int col)[] Step4Setup = { (0, 1), (1, 3), (3, 2) };

        private static readonly Color HighlightColor = new Color(1f, 0.83f, 0.30f, 0f);
        private const float HighlightAlphaMin = 0.45f;
        private const float HighlightAlphaMax = 0.95f;
        private const float HighlightScaleAmp = 0.05f;
        private const float HighlightSpeed = 4.5f;

        // Fond d'écran : même dégradé que le mode jeu.
        private static readonly Color BackgroundTopColor = new Color(0.97f, 0.96f, 0.93f);
        private static readonly Color BackgroundBottomColor = new Color(0.84f, 0.91f, 0.97f);

        private GridView _gridView;
        private PuzzleGrid _grid;
        private CellView[,] _cells;
        private Text _messageText;
        private RectTransform _canvasRect;
        private Font _font;
        private Sprite _frameSprite;
        private Sprite _roundedSprite;

        private readonly List<(Image image, RectTransform rect)> _highlightOverlays = new List<(Image, RectTransform)>();
        private readonly HashSet<(int row, int col)> _interactive = new HashSet<(int row, int col)>();
        private readonly Queue<(int row, int col)> _tapQueue = new Queue<(int row, int col)>();
        private (int row, int col) _lastTap;

        private bool _acceptTaps;
        private bool _victory;

        private void Awake()
        {
            _gridView = GetComponent<GridView>();
            if (_gridView == null)
                _gridView = gameObject.AddComponent<GridView>();
        }

        private void Start()
        {
            Canvas canvas = EnsureCanvas();
            EnsureEventSystem();
            CreateBackground(canvas);
            _canvasRect = (RectTransform)canvas.transform;
            _font = FindBuiltinFont();
            _frameSprite = CreateFrameSprite();
            _roundedSprite = CreateRoundedRectSprite();
            CreateMessagePanel(canvas);

            _grid = new PuzzleGrid(TutorialRegions);
            _gridView.OnCellTapped = HandleCellTapped;
            _gridView.OnCellLongPressed = null; // l'appui long n'a pas de rôle dans le tutoriel
            _gridView.Build(_grid, _canvasRect);

            LocateCells();
            StartCoroutine(RunTutorial());
        }

        // ------------------------------------------------------------------
        // Déroulé du tutoriel.
        // ------------------------------------------------------------------

        private IEnumerator RunTutorial()
        {
            yield return StartCoroutine(Step1_Zone());
            yield return StartCoroutine(Step2_LigneColonne());
            yield return StartCoroutine(Step3_Diagonale());
            yield return StartCoroutine(Step4_Deduction());

            SetAccept(false);
            ClearHighlights();
            SetMessage("Bravo, le tutoriel est terminé ! Tu es prêt pour les vrais niveaux.");
            yield return new WaitForSecondsRealtime(5f);
            SetMessage("");
        }

        /// <summary>Étape 1 : un seul pion par zone colorée.</summary>
        private IEnumerator Step1_Zone()
        {
            SetAccept(false);
            SetHighlights(new (int, int)[] { (0, 0), (0, 1), (1, 0), (1, 1) });
            SetMessage("Bienvenue dans le Zoodoku ! Règle n°1 : UN SEUL pion par zone colorée.");
            yield return new WaitForSecondsRealtime(2f);

            PlacePiece(0, 0);
            SetMessage("Un pion est posé en (0,0) : la zone menthe est pleine.");
            yield return new WaitForSecondsRealtime(1.8f);

            SetInteractive((0, 1));
            SetAccept(true);
            SetMessage("Essaie de poser un second pion dans la MÊME zone : touche la case (0,1).");
            yield return WaitTap();
            SetAccept(false);

            PlacePiece(0, 1);
            FlashAllConflicts();
            SetMessage("Conflit ! Un seul pion par zone colorée. (Ici ils sont aussi alignés : chaque règle compte.)");
            yield return new WaitForSecondsRealtime(2f);

            SetInteractive((0, 1));
            SetAccept(true);
            SetMessage("Retire le pion fautif : touche à nouveau la case (0,1).");
            yield return WaitTap();
            SetAccept(false);

            RemovePiece(0, 1);
            SetMessage("Bien vu ! On retient : UN SEUL pion par zone colorée.");
            yield return new WaitForSecondsRealtime(1.8f);

            RemovePiece(0, 0);
            ClearHighlights();
            SetMessage("Règle suivante : les lignes et les colonnes.");
            yield return new WaitForSecondsRealtime(1.6f);
        }

        /// <summary>Étape 2 : jamais deux pions sur la même ligne ni la même colonne.</summary>
        private IEnumerator Step2_LigneColonne()
        {
            SetAccept(false);
            SetHighlights(new (int, int)[] { (0, 0), (0, 1), (0, 2), (0, 3), (1, 2), (2, 2), (3, 2) });
            SetMessage("Règle n°2 : jamais deux pions sur la même ligne, ni sur la même colonne.");
            yield return new WaitForSecondsRealtime(2f);

            PlacePiece(0, 2);
            SetMessage("Un pion est en (0,2) : il occupe sa LIGNE (0) et sa COLONNE (2), en surbrillance.");
            yield return new WaitForSecondsRealtime(2f);

            SetInteractive((2, 2));
            SetAccept(true);
            SetMessage("Pose un pion sur la même COLONNE : touche la case (2,2).");
            yield return WaitTap();
            SetAccept(false);

            PlacePiece(2, 2);
            FlashAllConflicts();
            SetMessage("Colonne déjà occupée ! Deux pions ne peuvent pas être alignés en colonne.");
            yield return new WaitForSecondsRealtime(1.8f);

            SetInteractive((2, 2));
            SetAccept(true);
            SetMessage("Retire ce pion : touche la case (2,2).");
            yield return WaitTap();
            SetAccept(false);

            RemovePiece(2, 2);
            SetInteractive((0, 0));
            SetAccept(true);
            SetMessage("Essaie maintenant sur la même LIGNE : touche la case (0,0).");
            yield return WaitTap();
            SetAccept(false);

            PlacePiece(0, 0);
            FlashAllConflicts();
            SetMessage("Ligne déjà occupée ! Interdit aussi. Retire-le : touche la case (0,0).");
            yield return new WaitForSecondsRealtime(1.6f);

            SetInteractive((0, 0));
            SetAccept(true);
            SetMessage("Touche (0,0) pour retirer le pion.");
            yield return WaitTap();
            SetAccept(false);

            RemovePiece(0, 0);
            RemovePiece(0, 2);
            ClearHighlights();
            SetMessage("Retenu : jamais deux pions sur la même ligne, ni sur la même colonne.");
            yield return new WaitForSecondsRealtime(1.8f);

            SetMessage("Règle suivante : la diagonale.");
            yield return new WaitForSecondsRealtime(1.5f);
        }

        /// <summary>Étape 3 : deux pions ne se touchent pas en diagonale adjacente.</summary>
        private IEnumerator Step3_Diagonale()
        {
            SetAccept(false);
            SetHighlights(new (int, int)[] { (0, 0), (0, 2), (2, 0), (2, 2) });
            SetMessage("Règle n°3 : pas de pion en diagonale ADJACENTE (une case qui touche un coin).");
            yield return new WaitForSecondsRealtime(2f);

            PlacePiece(1, 1);
            SetMessage("Un pion est au centre, en (1,1). Ses 4 diagonales adjacentes sont interdites (en surbrillance).");
            yield return new WaitForSecondsRealtime(2.2f);

            SetInteractive((0, 2));
            SetAccept(true);
            SetMessage("Pose un pion en (0,2), en diagonale du premier.");
            yield return WaitTap();
            SetAccept(false);

            PlacePiece(0, 2);
            FlashAllConflicts();
            SetMessage("Diagonale adjacente : interdit ! Deux cases qui se touchent par un coin comptent.");
            yield return new WaitForSecondsRealtime(1.8f);

            SetInteractive((0, 2));
            SetAccept(true);
            SetMessage("Retire ce pion : touche la case (0,2).");
            yield return WaitTap();
            SetAccept(false);

            RemovePiece(0, 2);
            RemovePiece(1, 1);
            ClearHighlights();
            SetMessage("C'est la règle la plus subtile ! Passons au défi final.");
            yield return new WaitForSecondsRealtime(1.8f);
        }

        /// <summary>
        /// Étape 4 : la déduction. Trois pions sont pré-posés ; il n'existe qu'une seule
        /// case valide pour compléter la grille. Les mauvais placements déclenchent des
        /// indices contextuels (conflit précis). Aucune surbrillance : à toi de raisonner.
        /// </summary>
        private IEnumerator Step4_Deduction()
        {
            SetAccept(false);
            ClearHighlights();

            foreach ((int row, int col) in Step4Setup)
                PlacePiece(row, col);

            SetMessage("Défi final ! Trois pions sont posés. À toi de déduire le dernier emplacement.");
            yield return new WaitForSecondsRealtime(2f);

            SetMessage("Déduis la dernière case (zone lavande) : une zone, une ligne, une colonne par pion, sans diagonale.");
            SetInteractiveAll();
            SetAccept(true);

            while (!_victory)
            {
                yield return WaitTap();
                (int row, int col) = _lastTap;

                if (_grid.HasPion(row, col))
                {
                    RemovePiece(row, col);
                    SetMessage("Pion retiré. Continue ton raisonnement !");
                    continue;
                }

                List<ConflictType> conflits = RuleValidator.GetConflicts(_grid, row, col);
                if (conflits.Count > 0)
                {
                    _gridView.FlashConflict(row, col);
                    Haptics.VibrateLight();
                    SetMessage("Conflit (" + ConflictHintText(conflits) + ") : essaie une autre case.");
                    continue;
                }

                PlacePiece(row, col);
                if (RuleValidator.IsSolved(_grid))
                {
                    _victory = true;
                    PlayVictory();
                }
            }

            SetAccept(false);
            SetMessage("Bravo, grille résolue ! Le Zoodoku est terminé.");
            yield return new WaitForSecondsRealtime(3f);
        }

        // ------------------------------------------------------------------
        // Interactions (taps guidés).
        // ------------------------------------------------------------------

        private void HandleCellTapped(int row, int col)
        {
            if (!_acceptTaps)
                return;
            if (!_interactive.Contains((row, col)))
                return;
            _tapQueue.Enqueue((row, col));
        }

        private IEnumerator WaitTap()
        {
            _tapQueue.Clear();
            while (_tapQueue.Count == 0)
                yield return null;
            _lastTap = _tapQueue.Dequeue();
        }

        private void SetAccept(bool accept)
        {
            _acceptTaps = accept;
            _tapQueue.Clear();
        }

        private void SetInteractive(params (int row, int col)[] cells)
        {
            _interactive.Clear();
            if (cells != null)
                foreach ((int row, int col) cell in cells)
                    _interactive.Add(cell);
        }

        private void SetInteractiveAll()
        {
            _interactive.Clear();
            for (int row = 0; row < _grid.Size; row++)
                for (int col = 0; col < _grid.Size; col++)
                    _interactive.Add((row, col));
        }

        // ------------------------------------------------------------------
        // Actions sur la grille.
        // ------------------------------------------------------------------

        private void PlacePiece(int row, int col)
        {
            _grid.PlacePion(row, col);
            _gridView.SetPion(row, col, true);
        }

        private void RemovePiece(int row, int col)
        {
            _grid.RemovePion(row, col);
            _gridView.SetPion(row, col, false);
        }

        /// <summary>
        /// Fait clignoter en rouge tous les pions actuellement en conflit (même
        /// comportement visuel que le mode jeu) + vibration légère si au moins un.
        /// </summary>
        private void FlashAllConflicts()
        {
            bool any = false;
            foreach ((int row, int col) in _grid.Pions)
            {
                if (RuleValidator.GetConflicts(_grid, row, col).Count > 0)
                {
                    _gridView.FlashConflict(row, col);
                    any = true;
                }
            }

            if (any)
                Haptics.VibrateLight();
        }

        private void PlayVictory()
        {
            _gridView.PlayVictoryZoom();
            Haptics.VibrateStrong();
        }

        private static string ConflictHintText(List<ConflictType> types)
        {
            var parts = new List<string>();
            foreach (ConflictType type in types)
            {
                switch (type)
                {
                    case ConflictType.Zone:
                        parts.Add("même zone");
                        break;
                    case ConflictType.Row:
                        parts.Add("même ligne");
                        break;
                    case ConflictType.Column:
                        parts.Add("même colonne");
                        break;
                    case ConflictType.Diagonal:
                        parts.Add("diagonale adjacente");
                        break;
                }
            }

            return parts.Count == 0 ? "conflit" : string.Join(" et ", parts);
        }

        // ------------------------------------------------------------------
        // Messages et surbrillances.
        // ------------------------------------------------------------------

        private void SetMessage(string text)
        {
            if (_messageText != null)
                _messageText.text = text;
        }

        private void SetHighlights(IEnumerable<(int row, int col)> cells)
        {
            ClearHighlights();
            if (cells == null)
                return;
            foreach ((int row, int col) cell in cells)
                AddHighlight(cell.row, cell.col);
        }

        private void ClearHighlights()
        {
            foreach ((Image image, RectTransform rect) in _highlightOverlays)
            {
                if (image != null)
                    Destroy(image.gameObject);
            }
            _highlightOverlays.Clear();
        }

        private void AddHighlight(int row, int col)
        {
            if (_cells == null)
                return;
            CellView cell = _cells[row, col];
            if (cell == null)
                return;

            var gameObject = new GameObject(
                "TutorialHighlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(cell.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = gameObject.GetComponent<Image>();
            image.sprite = _frameSprite;
            image.type = Image.Type.Simple;
            image.color = HighlightColor;
            image.raycastTarget = false; // ne bloque jamais les taps sur la case

            _highlightOverlays.Add((image, rect));
        }

        /// <summary>Pulse les surbrillances actives (alpha + légère échelle).</summary>
        private void Update()
        {
            if (_highlightOverlays.Count == 0)
                return;

            float pulse = (Mathf.Sin(Time.unscaledTime * HighlightSpeed) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(HighlightAlphaMin, HighlightAlphaMax, pulse);
            float scale = 1f + HighlightScaleAmp * pulse;

            for (int i = _highlightOverlays.Count - 1; i >= 0; i--)
            {
                (Image image, RectTransform rect) = _highlightOverlays[i];
                if (image == null)
                {
                    _highlightOverlays.RemoveAt(i);
                    continue;
                }

                Color color = image.color;
                color.a = alpha;
                image.color = color;
                rect.localScale = new Vector3(scale, scale, scale);
            }
        }

        // ------------------------------------------------------------------
        // Repérage des cases créées par GridView.
        // ------------------------------------------------------------------

        /// <summary>
        /// Récupère les CellView créées par <see cref="GridView.Build"/> dans l'ordre
        /// de création (row-major) : l'index i du tableau correspond à (i / n, i % n).
        /// </summary>
        private void LocateCells()
        {
            Transform board = _canvasRect.Find("Board");
            if (board == null)
                throw new InvalidOperationException("[Zoodoku] TutorialManager : Board introuvable après Build.");

            CellView[] views = board.GetComponentsInChildren<CellView>(true);
            int n = _grid.Size;
            if (views.Length != n * n)
                throw new InvalidOperationException(
                    "[Zoodoku] TutorialManager : attendu " + (n * n) + " cases, obtenu " + views.Length + ".");

            _cells = new CellView[n, n];
            for (int i = 0; i < views.Length; i++)
                _cells[i / n, i % n] = views[i];
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

        private void CreateMessagePanel(Canvas canvas)
        {
            var panelGameObject = new GameObject(
                "TutorialMessagePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelGameObject.transform.SetParent(canvas.transform, false);

            var panelRect = (RectTransform)panelGameObject.transform;
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.anchoredPosition = new Vector2(0f, -30f);
            panelRect.sizeDelta = new Vector2(840f, 210f);

            var panel = panelGameObject.GetComponent<Image>();
            panel.sprite = _roundedSprite;
            panel.type = Image.Type.Simple;
            panel.color = new Color(0.10f, 0.12f, 0.16f, 0.82f);
            panel.raycastTarget = false;

            var textGameObject = new GameObject(
                "MessageText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGameObject.transform.SetParent(panelRect, false);

            var textRect = (RectTransform)textGameObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(20f, 10f);
            textRect.offsetMax = new Vector2(-20f, -10f);

            _messageText = textGameObject.GetComponent<Text>();
            _messageText.font = _font;
            _messageText.fontSize = 28;
            _messageText.alignment = TextAnchor.MiddleCenter;
            _messageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _messageText.verticalOverflow = VerticalWrapMode.Overflow;
            _messageText.color = Color.white;
            _messageText.raycastTarget = false;
            _messageText.supportRichText = false;

            var outline = textGameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
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

        /// <summary>Panneau du message : rectangle à coins arrondis (aucun asset).</summary>
        private static Sprite CreateRoundedRectSprite()
        {
            const int resolution = 128;
            const float cornerRatio = 0.16f;

            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float half = (resolution - 1) * 0.5f;
            float radius = resolution * cornerRatio;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float px = x - half;
                    float py = y - half;
                    float distance = SdfRoundedRect(px, py, radius, half); // négative à l'intérieur
                    float alpha = Mathf.Clamp01(0.5f - distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, resolution, resolution), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// Surbrillance des cases cibles : anneau à coins arrondis (une forme "cadre").
        /// Le centre est transparent : le pion et la zone restent visibles.
        /// </summary>
        private static Sprite CreateFrameSprite()
        {
            const int resolution = 128;
            const float cornerRatio = 0.22f;
            const float borderRatio = 0.14f;

            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float half = (resolution - 1) * 0.5f;
            float outerRadius = resolution * cornerRatio;
            float innerRadius = outerRadius - resolution * borderRatio;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float px = x - half;
                    float py = y - half;

                    // Distances signées au rectangle arrondi (négatives à l'intérieur).
                    float outer = SdfRoundedRect(px, py, outerRadius, half);
                    float inner = SdfRoundedRect(px, py, innerRadius, half);

                    // Anneau = à l'intérieur du contour extérieur ET à l'extérieur du trou central.
                    float ring = Mathf.Clamp01(0.5f - outer) * Mathf.Clamp01(0.5f + inner);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, ring));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, resolution, resolution), new Vector2(0.5f, 0.5f));
        }

        /// <summary>Distance signée à un rectangle à coins arrondis (négative à l'intérieur).</summary>
        private static float SdfRoundedRect(float px, float py, float radius, float half)
        {
            float inner = half - radius;
            float qx = Mathf.Clamp(px, -inner, inner);
            float qy = Mathf.Clamp(py, -inner, inner);
            float dx = px - qx;
            float dy = py - qy;
            return Mathf.Sqrt(dx * dx + dy * dy) - radius;
        }

        private static Font FindBuiltinFont()
        {
            try
            {
                Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font != null)
                    return font;
            }
            catch (Exception)
            {
            }

            try
            {
                return Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
