using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zoologic.Core;

namespace Zoologic
{
    /// <summary>
    /// Génère et gère l'affichage d'une grille Zoologic en UI Canvas.
    /// Purement visuel : la logique (pions, règles, victoire) appartient à
    /// <see cref="PuzzleGameController"/>.
    ///
    /// Fonctionne pour n'importe quelle taille de grille (de 4x4 à 8x8) : la taille
    /// des cases est calculée automatiquement à partir de la zone disponible du
    /// canvas. Les zones sont colorées via une palette pastel fixe (voir
    /// <see cref="RegionPalette"/>), attribuée dans l'ordre pour un rendu harmonieux.
    /// Chaque zone reçoit en plus une icône d'animal différente (voir
    /// <see cref="AnimalIconSet"/>), affichée sur les pions posés dans la zone.
    /// </summary>
    public sealed class GridView : MonoBehaviour
    {
        // ------------------------------------------------------------------
        // Configuration visuelle (tu peux ajuster librement ces valeurs).
        // ------------------------------------------------------------------

        /// <summary>
        /// Palette pastel des zones, attribuée dans l'ordre (pas de hasard) pour un
        /// rendu harmonieux. 8 couleurs douces ; ajoute ou retire des entrées librement.
        /// </summary>
        public static readonly Color[] RegionPalette =
        {
            new Color(0.72f, 0.90f, 0.82f), // Vert menthe vif
            new Color(0.80f, 0.88f, 0.97f), // Bleu ciel
            new Color(0.88f, 0.82f, 0.96f), // Lavande
            new Color(1.00f, 0.86f, 0.88f), // Rose pêche
            new Color(1.00f, 0.91f, 0.78f), // Pêche dorée
            new Color(0.98f, 0.96f, 0.76f), // Jaune crème
            new Color(0.78f, 0.94f, 0.86f), // Vert d'eau
            new Color(0.83f, 0.85f, 0.97f), // Bleu lavande
        };

        /// <summary>
        /// Espacement visible entre deux cases, en fraction de la taille d'une case
        /// (0.05 = 5 %). Augmente la valeur pour des cases plus "aérées".
        /// </summary>
        public const float CellGapRatio = 0.05f;

        /// <summary>Décalage de l'ombre portée, en fraction de la taille d'une case (x : droite, y : bas).</summary>
        private static readonly Vector2 ShadowOffsetRatio = new Vector2(0.02f, -0.04f);

        /// <summary>Couleur de l'ombre portée douce sous chaque case.</summary>
        private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.10f);

        /// <summary>Appelé quand une case est tapée (paramètres : ligne, colonne).</summary>
        public Action<int, int> OnCellTapped;

        // Animation d'entrée de la grille.
        private const float EntranceDuration = 0.4f;
        private const float EntranceStep = 0.012f; // délai entre chaque cellule (vague)

        private PuzzleGrid _grid;
        private CellView[,] _cells;
        private RectTransform _boardContainer;
        private readonly Dictionary<int, Color> _regionColors = new Dictionary<int, Color>();
        private readonly Dictionary<int, Sprite> _regionIcons = new Dictionary<int, Sprite>();

        // Icônes d'animaux mélangées pour le niveau en cours : piochées dans l'ordre,
        // chaque zone reçoit donc un animal différent des autres zones de la grille.
        private Sprite[] _levelIcons;
        private int _iconIndex;

        private const float BoardFill = 0.9f;          // part de l'écran occupée par la grille
        private const float PionRatio = 0.62f;         // taille du pion par rapport à la case

        private static Sprite _circleSprite;
        private static Sprite _roundedRectSprite;
        private static Font _builtinFont;

        // Indice / highlight
        private static readonly Color HighlightColor = new Color(1f, 0.85f, 0.2f, 0.45f);
        private const float HighlightDuration = 3f;
        private const float HighlightPulseSpeed = 3f;
        private GameObject _highlightRoot;
        private Image _highlightImage;
        private Coroutine _highlightRoutine;

        /// <summary>Nombre de lignes (et de colonnes) de la grille affichée (0 si aucune).</summary>
        public int Size => _grid != null ? _grid.Size : 0;

        /// <summary>Conteneur racine du plateau (pour décalage positionnel par le contrôleur).</summary>
        public RectTransform BoardContainer => _boardContainer;

        /// <summary>
        /// Construit (ou reconstruit) l'affichage complet de la grille sous le parent donné
        /// (généralement le RectTransform du canvas).
        /// </summary>
        public void Build(PuzzleGrid grid, RectTransform parent)
        {
            if (grid == null)
                throw new ArgumentNullException(nameof(grid));
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));

            if (_boardContainer != null)
                Destroy(_boardContainer.gameObject);

            StopHighlight();

            _grid = grid;
            int n = grid.Size;
            float slotSize = Mathf.Min(parent.rect.width, parent.rect.height) * BoardFill / n;
            float visualSize = slotSize * (1f - CellGapRatio);

            _boardContainer = new GameObject("Board", typeof(RectTransform)).GetComponent<RectTransform>();
            _boardContainer.SetParent(parent, false);
            _boardContainer.anchorMin = new Vector2(0.5f, 0.5f);
            _boardContainer.anchorMax = new Vector2(0.5f, 0.5f);
            _boardContainer.pivot = new Vector2(0.5f, 0.5f);
            _boardContainer.anchoredPosition = Vector2.zero;
            _boardContainer.sizeDelta = new Vector2(slotSize * n, slotSize * n);

            _regionColors.Clear();
            _regionIcons.Clear();
            _levelIcons = AnimalIconSet.GetShuffled();
            _iconIndex = 0;
            _cells = new CellView[n, n];

            float half = (n - 1) * 0.5f;

            // Centres des "emplacements" : chaque case occupe slotSize en mise en page
            // mais son visuel est réduit à visualSize, ce qui crée le gap entre cases.
            var positions = new Vector2[n, n];
            for (int row = 0; row < n; row++)
            {
                for (int col = 0; col < n; col++)
                    positions[row, col] = new Vector2((col - half) * slotSize, -(row - half) * slotSize);
            }

            // 1) Toutes les ombres d'abord : elles restent ainsi derrière toutes les cases.
            for (int row = 0; row < n; row++)
            {
                for (int col = 0; col < n; col++)
                    CreateShadow(positions[row, col], visualSize);
            }

            // 2) Puis les cases.
            for (int row = 0; row < n; row++)
            {
                for (int col = 0; col < n; col++)
                {
                    CellView cell = CreateCell(row, col, visualSize);
                    cell.transform.SetParent(_boardContainer, false);

                    var rect = (RectTransform)cell.transform;
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = new Vector2(visualSize, visualSize);
                    rect.anchoredPosition = positions[row, col];

                    int rowCapture = row;
                    int colCapture = col;
                    cell.OnTap = () => OnCellTapped?.Invoke(rowCapture, colCapture);

                    _cells[row, col] = cell;
                }
            }

            PlayEntranceAnimation();
        }

        /// <summary>
        /// Apparition en vague des cases au chargement : chacune démarre à l'échelle 0
        /// et "pop" (easeOutBack) avec un léger décalage croissant en diagonale.
        /// Ne s'exécute qu'en play mode.
        /// </summary>
        private void PlayEntranceAnimation()
        {
            if (!Application.isPlaying || _cells == null)
                return;

            int n = _grid.Size;
            for (int row = 0; row < n; row++)
            {
                for (int col = 0; col < n; col++)
                {
                    if (_cells[row, col] == null)
                        continue;
                    CellView cell = _cells[row, col];
                    cell.transform.localScale = Vector3.zero;
                    StartCoroutine(EntranceCellRoutine(cell, (row + col) * EntranceStep));
                }
            }
        }

        private IEnumerator EntranceCellRoutine(CellView cell, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            Transform t = cell.transform;
            float elapsed = 0f;

            while (elapsed < EntranceDuration)
            {
                float p = Mathf.Clamp01(elapsed / EntranceDuration);
                float s = Mathf.Max(0f, Easing.EaseOutBack(p));
                t.localScale = new Vector3(s, s, s);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            t.localScale = Vector3.one;
        }

        /// <summary>Affiche ou masque le pion de la case (row, col).</summary>
        public void SetPion(int row, int col, bool active)
        {
            CellView cell = GetCell(row, col);
            if (cell != null)
                cell.SetPion(active);
        }

        /// <summary>Affiche ou masque le marqueur "X" de la case (row, col).</summary>
        public void SetX(int row, int col, bool active)
        {
            CellView cell = GetCell(row, col);
            if (cell != null)
                cell.SetX(active);
        }

        /// <summary>Fait trembler et rougir la case (row, col) en cas de conflit.</summary>
        public void FlashConflict(int row, int col)
        {
            CellView cell = GetCell(row, col);
            if (cell != null)
                cell.FlashConflict();
        }

        /// <summary>Petit tremblement du plateau entier (feedback d'une erreur).</summary>
        public void ShakeBoard(float amplitude = 18f, float duration = 0.3f)
        {
            if (_boardContainer == null || !Application.isPlaying)
                return;
            ScreenShake.Shake(this, _boardContainer, amplitude, duration);
        }

        /// <summary>Punch d'échelle rapide du plateau (feedback d'un bon placement).</summary>
        public void PunchBoard(float targetScale = 1.02f, float duration = 0.2f)
        {
            if (_boardContainer == null || !Application.isPlaying)
                return;
            Punch.Scale(this, _boardContainer, targetScale, duration, elastic: false);
        }

        // ------------------------------------------------------------------
        // Animation de victoire : léger zoom du plateau (100 % → 105 % → 100 %).
        // ------------------------------------------------------------------

        private const float VictoryZoomDuration = 0.9f;
        private const float VictoryZoomMax = 0.05f; // amplitude du zoom : +5 %

        private Coroutine _victoryZoomRoutine;

        /// <summary>Joue le zoom de victoire sur toute la grille.</summary>
        public void PlayVictoryZoom()
        {
            if (_boardContainer == null || !Application.isPlaying)
                return;

            if (_victoryZoomRoutine != null)
                StopCoroutine(_victoryZoomRoutine);
            _victoryZoomRoutine = StartCoroutine(VictoryZoomRoutine());
        }

        /// <summary>Interrompt le zoom (s'il est en cours) et restaure l'échelle 100 %.</summary>
        public void ResetVictoryZoom()
        {
            if (_victoryZoomRoutine != null)
            {
                StopCoroutine(_victoryZoomRoutine);
                _victoryZoomRoutine = null;
            }

            if (_boardContainer != null)
                _boardContainer.localScale = Vector3.one;
        }

        /// <summary>Zoom sinusoïdal : monte à 105 % à mi-parcours puis revient à 100 %.</summary>
        private IEnumerator VictoryZoomRoutine()
        {
            float elapsed = 0f;

            while (elapsed < VictoryZoomDuration)
            {
                float t = Mathf.Clamp01(elapsed / VictoryZoomDuration);
                float scale = 1f + VictoryZoomMax * Mathf.Sin(Mathf.PI * t);

                _boardContainer.localScale = new Vector3(scale, scale, scale);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _boardContainer.localScale = Vector3.one;
            _victoryZoomRoutine = null;
        }

        // ------------------------------------------------------------------
        // Indice : highlight pulsé sur une case correcte manquante.
        // ------------------------------------------------------------------

        /// <summary>
        /// Demande un indice : identifie une case vide dont le placement est correct
        /// dans la solution, et la met en évidence avec une pulsation dorée pendant 3 secondes.
        /// Retourne true si un indice a été trouvé et affiché, false sinon.
        /// Ne consomme pas de pion — sert juste de guide visuel.
        /// </summary>
        public bool RequestHint()
        {
            if (_grid == null || _cells == null)
                return false;

            if (RuleValidator.IsSolved(_grid))
                return false;

            var pionsActuels = new List<(int row, int col)>(_grid.Pions);

            if (pionsActuels.Count == 0)
            {
                var solveurVide = new PuzzleSolver();
                var solutions = solveurVide.FindAllSolutions(_grid, 1);
                if (solutions.Count > 0)
                {
                    foreach (var (r, c) in solutions[0])
                    {
                        ShowHighlight(r, c);
                        return true;
                    }
                }
            }
            else
            {
                var solveur = new PuzzleSolver();
                var solution = solveur.SolveWithFixedPlacements(_grid, pionsActuels);

                if (solution != null)
                {
                    foreach (var (r, c) in solution)
                    {
                        if (!_grid.HasPion(r, c))
                        {
                            ShowHighlight(r, c);
                            return true;
                        }
                    }
                }
            }

            for (int row = 0; row < _grid.Size; row++)
            {
                for (int col = 0; col < _grid.Size; col++)
                {
                    if (!_grid.HasPion(row, col) && RuleValidator.IsValidPlacement(_grid, row, col))
                    {
                        ShowHighlight(row, col);
                        return true;
                    }
                }
            }

            return false;
        }

        private void ShowHighlight(int row, int col)
        {
            StopHighlight();

            EnsureHighlightObject();
            CellView cell = GetCell(row, col);
            if (cell == null)
                return;

            var cellRect = (RectTransform)cell.transform;
            _highlightRoot.transform.SetParent(_boardContainer, false);
            var hlRect = (RectTransform)_highlightRoot.transform;
            hlRect.anchorMin = cellRect.anchorMin;
            hlRect.anchorMax = cellRect.anchorMax;
            hlRect.pivot = cellRect.pivot;
            hlRect.sizeDelta = cellRect.sizeDelta;
            hlRect.anchoredPosition = cellRect.anchoredPosition;

            _highlightRoot.SetActive(true);
            _highlightRoot.transform.localScale = Vector3.one;
            _highlightRoutine = StartCoroutine(PulseHighlightRoutine());
        }

        private void StopHighlight()
        {
            if (_highlightRoutine != null)
            {
                StopCoroutine(_highlightRoutine);
                _highlightRoutine = null;
            }

            if (_highlightRoot != null)
                _highlightRoot.SetActive(false);
        }

        private void EnsureHighlightObject()
        {
            if (_highlightRoot != null)
                return;

            _highlightRoot = new GameObject("HintHighlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _highlightRoot.transform.SetParent(_boardContainer, false);

            var rect = (RectTransform)_highlightRoot.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            _highlightImage = _highlightRoot.GetComponent<Image>();
            _highlightImage.sprite = GetRoundedRectSprite();
            _highlightImage.type = Image.Type.Simple;
            _highlightImage.color = HighlightColor;
            _highlightImage.raycastTarget = false;

            _highlightRoot.SetActive(false);
        }

        /// <summary>
        /// Pulsation dorée : l'alpha et l'échelle oscillent pendant 3 secondes,
        /// puis la highlight disparaît automatiquement.
        /// </summary>
        private IEnumerator PulseHighlightRoutine()
        {
            float elapsed = 0f;
            Color baseColor = HighlightColor;
            RectTransform hlRect = _highlightRoot != null ? (RectTransform)_highlightRoot.transform : null;

            while (elapsed < HighlightDuration)
            {
                float t = Mathf.PingPong(elapsed * HighlightPulseSpeed, 1f);
                float alpha = Mathf.Lerp(0.25f, 0.55f, t);
                _highlightImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

                if (hlRect != null)
                {
                    float s = Mathf.Lerp(1f, 1.06f, t);
                    hlRect.localScale = new Vector3(s, s, s);
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (hlRect != null)
                hlRect.localScale = Vector3.one;
            _highlightRoot.SetActive(false);
            _highlightRoutine = null;
        }

        private CellView GetCell(int row, int col)
        {
            if (_cells == null || row < 0 || row >= _cells.GetLength(0) || col < 0 || col >= _cells.GetLength(1))
                return null;
            return _cells[row, col];
        }

        // ------------------------------------------------------------------
        // Construction des éléments visuels.
        // ------------------------------------------------------------------

        private CellView CreateCell(int row, int col, float visualSize)
        {
            var gameObject = new GameObject(
                "Cell", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CellView));

            var image = gameObject.GetComponent<Image>();
            image.sprite = GetRoundedRectSprite();
            image.type = Image.Type.Simple;

            Color baseColor = GetRegionColor(_grid.GetRegionId(row, col));
            Sprite pionSprite = GetRegionIcon(_grid.GetRegionId(row, col));
            if (pionSprite == null)
                pionSprite = GetPionSprite(); // secours : cercle blanc si pas d'icônes

            var cell = gameObject.GetComponent<CellView>();
            cell.Init(baseColor, image, pionSprite, GetFont(), visualSize, PionRatio);
            return cell;
        }

        /// <summary>
        /// Ombre portée douce sous une case : la même forme arrondie, décalée vers le
        /// bas-droit et semi-transparente, pour un léger effet de profondeur.
        /// </summary>
        private void CreateShadow(Vector2 position, float size)
        {
            var gameObject = new GameObject("Shadow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            gameObject.transform.SetParent(_boardContainer, false);

            var rect = (RectTransform)gameObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = position + ShadowOffsetRatio * size;

            var image = gameObject.GetComponent<Image>();
            image.sprite = GetRoundedRectSprite();
            image.type = Image.Type.Simple;
            image.color = ShadowColor;
            image.raycastTarget = false; // ne capte jamais les taps
        }

        /// <summary>
        /// Couleur d'une zone, piochée dans <see cref="RegionPalette"/> dans l'ordre
        /// (première zone rencontrée = première couleur), puis boucle sur la palette.
        /// </summary>
        private Color GetRegionColor(int regionId)
        {
            if (_regionColors.TryGetValue(regionId, out Color existing))
                return existing;

            int index = _regionColors.Count;
            Color color = RegionPalette[index % RegionPalette.Length];
            _regionColors.Add(regionId, color);
            return color;
        }

        /// <summary>
        /// Icône d'animal assignée à une zone, piochée sans répétition dans la
        /// permutation du niveau en cours (voir <see cref="AnimalIconSet.GetShuffled"/>).
        /// Renvoie null si aucune icône n'est disponible (secours sur cercle blanc).
        /// </summary>
        private Sprite GetRegionIcon(int regionId)
        {
            if (_regionIcons.TryGetValue(regionId, out Sprite existing))
                return existing;

            Sprite icon = null;
            if (_levelIcons != null && _levelIcons.Length > 0)
            {
                icon = _levelIcons[_iconIndex % _levelIcons.Length];
                _iconIndex++;
            }

            _regionIcons.Add(regionId, icon);
            return icon;
        }

        private static Sprite GetPionSprite()
        {
            if (_circleSprite == null)
                _circleSprite = CreateCircleSprite();
            return _circleSprite;
        }

        private static Sprite GetRoundedRectSprite()
        {
            if (_roundedRectSprite == null)
                _roundedRectSprite = CreateRoundedRectSprite();
            return _roundedRectSprite;
        }

        /// <summary>
        /// Pion : toujours un cercle, mais avec un léger volume (dégradé radial clair +
        /// liseré un peu plus foncé près du bord) au lieu d'un disque blanc plat.
        /// La forme reste blanche : Image.color pourra la teinter plus tard (thème).
        /// </summary>
        private static Sprite CreateCircleSprite()
        {
            const int resolution = 128;

            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float center = (resolution - 1) * 0.5f;
            float radius = resolution * 0.5f - 1f;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    float alpha = Mathf.Clamp01(radius + 0.5f - distance);
                    if (alpha <= 0f)
                    {
                        texture.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    // Volume : centre quasi blanc, assombri vers le bord, avec un liseré
                    // plus marqué juste avant la périphérie (effet de profondeur).
                    float t = Mathf.Clamp01(distance / radius);
                    float shade = Mathf.Lerp(0.99f, 0.80f, t * t);
                    if (t > 0.84f)
                        shade -= 0.12f * ((t - 0.84f) / 0.16f);

                    texture.SetPixel(x, y, new Color(shade, shade, shade, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, resolution, resolution), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// Crée la texture des cases : carré à coins arrondis, bord adouci (aucun asset).
        /// La forme est blanche : c'est Image.color qui la teinte par zone.
        /// </summary>
        private static Sprite CreateRoundedRectSprite()
        {
            const int resolution = 256;
            const float cornerRatio = 0.22f; // rayon des coins (fraction du côté)

            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float half = (resolution - 1) * 0.5f;
            float radius = resolution * cornerRatio;
            float inner = half - radius; // demi-côté du carré central

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float px = x - half;
                    float py = y - half;

                    // Distance signée au rectangle arrondi (SDF simple).
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
            return Sprite.Create(texture, new Rect(0f, 0f, resolution, resolution), new Vector2(0.5f, 0.5f));
        }

        private static Font GetFont()
        {
            if (_builtinFont == null)
            {
                try { _builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                catch (Exception) { _builtinFont = null; }

                if (_builtinFont == null)
                {
                    try { _builtinFont = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                    catch (Exception) { _builtinFont = null; }
                }
            }
            return _builtinFont;
        }
    }
}
