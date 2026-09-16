using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Zoologic.Core;
using Zoologic.Localization;

namespace Zoologic
{
    public sealed class TutorialManager : MonoBehaviour
    {
        public const string HasCompletedKey = "HasCompletedTutorial";
        public static bool HasCompleted => PlayerPrefs.GetInt(HasCompletedKey, 0) == 1;
        public static void MarkCompleted() { PlayerPrefs.SetInt(HasCompletedKey, 1); PlayerPrefs.Save(); ForceShow = false; }
        public static void ResetTutorial() { PlayerPrefs.DeleteKey(HasCompletedKey); ForceShow = false; }
        public static bool ShouldShow => !HasCompleted;
        public static bool ForceShow = false;

        private static readonly int[,] TutorialRegions =
        {
            { 0, 0, 1, 1 },
            { 0, 0, 1, 1 },
            { 2, 2, 3, 3 },
            { 2, 2, 3, 3 },
        };

        private static readonly int[,] Grid3_RowCol =
        {
            { 0, 1, 2 },
            { 3, 4, 5 },
            { 6, 7, 8 },
        };

        private static readonly int[,] Grid3_Zone =
        {
            { 0, 0, 1 },
            { 0, 0, 1 },
            { 1, 1, 1 },
        };

        private static readonly (int row, int col)[] Step4Setup = { (0, 1), (1, 3), (3, 2) };

        private static readonly Color HighlightColor = new Color(1f, 0.83f, 0.30f, 0f);
        private const float HighlightAlphaMin = 0.45f;
        private const float HighlightAlphaMax = 0.95f;
        private const float HighlightScaleAmp = 0.08f;
        private const float HighlightSpeed = 4.5f;

        private GridView _gridView;
        private PuzzleGrid _grid;
        private CellView[,] _cells;
        private RectTransform _canvasRect;
        private TMP_FontAsset _fontTitle;
        private TMP_FontAsset _fontBody;
        private Sprite _frameSprite;
        private Sprite _roundedSprite;

        private readonly List<(Image image, RectTransform rect)> _highlightOverlays = new List<(Image, RectTransform)>();
        private readonly HashSet<(int row, int col)> _interactive = new HashSet<(int row, int col)>();
        private readonly Queue<(int row, int col, float time)> _tapQueue = new Queue<(int row, int col, float time)>();
        private readonly Queue<(string kind, int row, int col, int fromRow, int fromCol)> _dropQueue = new Queue<(string, int, int, int, int)>();
        private (int row, int col) _lastTap;
        private bool _acceptTaps;
        private bool _acceptDrops;
        private bool _victory;

        private BoardDragController _drag;
        private AnimalTray _tray;

        private GameObject _overlayRoot;
        private Image _overlayTop; private Image _overlayBottom; private Image _overlayLeft; private Image _overlayRight;
        private Image _blocker;
        private GameObject _bubbleRoot;
        private TextMeshProUGUI _bubbleText;
        private Button _bubbleNext;
        private bool _nextClicked;
        private UIHandPointer _hand;

        private void Awake()
        {
            _gridView = GetComponent<GridView>();
            if (_gridView == null) _gridView = gameObject.AddComponent<GridView>();
            EnsureEventSystem();
        }

        private void Start()
        {
            Canvas earlyCanvas = EnsureCanvas();
            if (!AgeGateManager.HasChosen)
            {
                AgeGateManager.Show(earlyCanvas, _ => InitTutorial());
                return;
            }
            InitTutorial();
        }

        private void InitTutorial()
        {
            bool isTutorialScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Tutorial";
            if (HasCompleted && !isTutorialScene)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
                return;
            }
            if (HasCompleted && isTutorialScene && !ForceShow)
            {
                Debug.Log("[Tutorial] Relecture manuelle autorisée même si déjà complété (HasCompleted=1).");
            }
            Canvas canvas = EnsureCanvas();
            CreateBackground(canvas);
            CreateTray(canvas);
            _canvasRect = (RectTransform)canvas.transform;
            _fontTitle = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Bold SDF");
            _fontBody = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Regular SDF");
            _frameSprite = CreateFrameSprite();
            _roundedSprite = CreateRoundedRectSprite();
            CreateOverlay(canvas);
            CreateBubble(canvas);
            CreateSkipButton(canvas);
            _hand = UIHandPointer.Create(canvas);

            Canvas.ForceUpdateCanvases();
            _grid = new PuzzleGrid(TutorialRegions);
            _gridView.OnCellTapped = HandleCellTapped;
            _gridView.Build(_grid, _canvasRect);
            LocateCells();
            SetupDrag(canvas);
            LocalizationManager.ApplyFontsToScene();
            StartCoroutine(RunTutorial());
        }

        private IEnumerator RunTutorial()
        {
            if (_skipRoot != null) _skipRoot.SetActive(true);
            yield return StartCoroutine(Step1_RowCol());
            yield return StartCoroutine(Step2_Zone());
            yield return StartCoroutine(Step3_Adjacency());
            yield return StartCoroutine(Step4_XElimination());
            SetAccept(false); SetAcceptDrops(false); ClearHighlights(); HideOverlay(); _hand.Hide();
            if (_drag != null) _drag.Cancel();
            if (_skipRoot != null) _skipRoot.SetActive(false);
            ConfettiHelper.Burst(this, _canvasRect.GetComponent<Canvas>(), 40);
            SFXManager.Instance.PlaySuccess();
            Haptics.VibrateStrong();
            yield return ShowBubble(LocalizationManager.Get("tutorial.ready"), true);
            MarkCompleted();
            yield return new WaitForSecondsRealtime(1f);
            SetBubbleVisible(false);
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        private IEnumerator Step1_RowCol()
        {
            RebuildGrid(Grid3_RowCol);
            yield return ShowBubble(LocalizationManager.Get("tutorial.welcome"), true);
            yield return ShowBubble(LocalizationManager.Get("tutorial.one_per_row"), true);
            var target = (0, 0);
            HighlightSingle(target.Item1, target.Item2);
            _hand.PlayDragFromTo(GetTrayChipRect(0), GetCellRect(target.Item1, target.Item2));
            yield return ShowBubble(LocalizationManager.Get("tutorial.double_tap"), false);
            SetInteractive(target); SetAcceptDrops(true);
            yield return WaitDropOnCell(0, 0); SetAcceptDrops(false);
            PlacePiece(target.Item1, target.Item2); PunchAndConfetti(target.Item1,target.Item2); ClearHighlights(); _hand.Hide();
            yield return new WaitForSecondsRealtime(0.4f);
            var forbid = new (int,int)[] { (0,1),(0,2),(1,0),(2,0) };
            SetHighlights(forbid); ShowMultiOverlay(forbid); yield return ShowBubble(LocalizationManager.Get("tutorial.row_blocked"), true);
            // Démo automatique de l'erreur : on ne demande jamais à l'utilisateur
            // de jouer un coup interdit, le hibou montre puis l'utilisateur répare.
            ClearHighlights(); HideOverlay();
            PlacePiece(0,1); FlashAllConflicts(); PunchAndConfetti(0,1);
            yield return ShowBubble(LocalizationManager.Get("tutorial.same_row"), true);
            HighlightSingle(0,1);
            SetInteractive((0,1)); yield return ShowBubble(LocalizationManager.Get("tutorial.tap_removes"), false);
            _hand.PlayDragToLocal(GetCellRect(0,1), DropOffBoardLocal(GetCellRect(0,1))); _hand.Show();
            SetAcceptDrops(true);
            yield return WaitDropRemove(0, 1); SetAcceptDrops(false);
            RemovePiece(0,1); ClearHighlights(); _hand.Hide();
            yield return ShowBubble(LocalizationManager.Get("tutorial.perfect"), true);
            RemovePiece(0,0);
        }

        private IEnumerator Step2_Zone()
        {
            RebuildGrid(Grid3_Zone);
            yield return ShowBubble(LocalizationManager.Get("tutorial.one_per_color"), true);
            SetHighlights(new (int,int)[] { (0,0),(0,1),(1,0),(1,1) }); ShowMultiOverlay(new (int,int)[] { (0,0),(0,1),(1,0),(1,1) });
            yield return ShowBubble(LocalizationManager.Get("tutorial.zone_full"), true);
            ClearHighlights(); HideOverlay();
            PlacePiece(0,0); PunchAndConfetti(0,0); yield return new WaitForSecondsRealtime(0.4f);
            yield return ShowBubble(LocalizationManager.Get("tutorial.tap_here"), false);
            HighlightSingle(2,2); _hand.PlayDragFromTo(GetTrayChipRect(0), GetCellRect(2,2)); _hand.Show();
            SetInteractive((2,2)); SetAcceptDrops(true);
            yield return WaitDropOnCell(2, 2); SetAcceptDrops(false); _hand.Hide(); ClearHighlights();
            PlacePiece(2,2); PunchAndConfetti(2,2); yield return ShowBubble(LocalizationManager.Get("tutorial.exact"), true);
            RemovePiece(0,0); RemovePiece(2,2);
        }

        private IEnumerator Step3_Adjacency()
        {
            RebuildGrid(TutorialRegions);
            yield return ShowBubble(LocalizationManager.Get("tutorial.no_contact"), true);
            PlacePiece(1,1); PunchAndConfetti(1,1);
            var forbid = new (int,int)[] { (0,0),(0,1),(0,2),(1,0),(1,2),(2,0),(2,1),(2,2) };
            SetHighlights(forbid); ShowMultiOverlay(forbid);
            yield return ShowBubble(LocalizationManager.Get("tutorial.blocked8"), true);
            ClearHighlights(); HideOverlay();
            yield return ShowBubble(LocalizationManager.Get("tutorial.tap_x"), true);
            yield return ShowBubble(LocalizationManager.Get("tutorial.touch_here"), false);
            HighlightSingle(0,0); ShowOverlay(0,0); _hand.PointTo(GetCellRect(0,0)); _hand.Show();
            SetInteractive((0,0)); SetAccept(true);
            yield return WaitSingleTap(); SetAccept(false);
            if (_lastTap == (0,0)) { _gridView.SetX(0,0,true); _hand.PlayTap(); Haptics.VibrateLight(); }
            ClearHighlights(); HideOverlay(); _hand.Hide();
            yield return new WaitForSecondsRealtime(0.4f);
            yield return ShowBubble(LocalizationManager.Get("tutorial.tap_animal"), true);
            yield return ShowBubble(LocalizationManager.Get("tutorial.place_here"), false);
            HighlightSingle(3,3); _hand.PlayDragFromTo(GetTrayChipRect(0), GetCellRect(3,3)); _hand.Show();
            SetInteractive((3,3)); SetAcceptDrops(true);
            yield return WaitDropOnCell(3, 3); SetAcceptDrops(false); _hand.Hide(); ClearHighlights();
            PlacePiece(3,3); PunchAndConfetti(3,3); yield return ShowBubble(LocalizationManager.Get("tutorial.validated"), true);
            RemovePiece(1,1); RemovePiece(3,3); _gridView.SetX(0,0,false);
        }

        private IEnumerator Step4_XElimination()
        {
            RebuildGrid(TutorialRegions);
            foreach (var p in Step4Setup) { PlacePiece(p.row, p.col); PunchAndConfetti(p.row,p.col); }
            _gridView.SetX(1,1,true); _gridView.SetX(2,2,true);
            yield return ShowBubble(LocalizationManager.Get("tutorial.x_clears"), true);
            yield return ShowBubble(LocalizationManager.Get("tutorial.more_x"), false);
            HighlightSingle(1,0); ShowOverlay(1,0); _hand.PointTo(GetCellRect(1,0)); _hand.Show();
            SetInteractive((1,0)); SetAccept(true);
            yield return WaitSingleTap(); SetAccept(false);
            if (_lastTap == (1,0)) { _gridView.SetX(1,0,true); _hand.PlayTap(); }
            ClearHighlights(); HideOverlay(); _hand.Hide();
            yield return ShowBubble(LocalizationManager.Get("tutorial.find_last"), false);
            SetInteractiveAll(); SetAccept(true); SetAcceptDrops(true);
            _hand.PlayDragFromTo(GetTrayChipRect(0), GetCellRect(2,0)); _hand.Show();
            while (!_victory)
            {
                yield return WaitActivity();
                if (_lastActivityIsDrop)
                {
                    var d = _lastDrop;
                    if (d.kind == "remove")
                    {
                        RemovePiece(d.row, d.col);
                        continue;
                    }
                    // place / move : le tuto n'accepte que les poses valides.
                    var conflits = RuleValidator.GetConflicts(_grid, d.row, d.col);
                    if (conflits.Count > 0) { _gridView.FlashConflict(d.row, d.col); Haptics.VibrateLight(); continue; }
                    if (d.kind == "move") RemovePiece(d.fromRow, d.fromCol);
                    PlacePiece(d.row, d.col); PunchAndConfetti(d.row, d.col);
                    if (RuleValidator.IsSolved(_grid)) { _victory = true; PlayVictory(); }
                    else { _hand.PlayDragFromTo(GetTrayChipRect(0), GetCellRect(2,0)); }
                    continue;
                }
                var (row,col) = _lastTap;
                // En résolution libre le tap ne pose plus : il efface un X.
                if (_gridView != null && HasX(row,col)) { _gridView.SetX(row,col,false); SFXManager.Instance.PlayClickedOut(); continue; }
                SFXManager.Instance.PlayDialogueBlip();
            }
            SetAcceptDrops(false);
            SetAccept(false); _hand.Hide(); ClearHighlights(); HideOverlay();
            yield return ShowBubble(LocalizationManager.Get("tutorial.solved"), true);
        }

        private bool HasX(int r, int c)
        {
            var rt = GetCellRect(r,c);
            if (rt == null) return false;
            var t = rt.transform.Find("X");
            return t != null && t.gameObject.activeSelf;
        }

        private bool TryPlaceWithValidation(int r,int c)
        {
            if (_grid.HasPion(r,c)) return false;
            var rt = GetCellRect(r,c);
            if (rt == null) return false;
            PlacePiece(r,c);
            return true;
        }

        private void CreateTray(Canvas canvas)
        {
            var holder = new GameObject("TrayHolder", typeof(RectTransform));
            holder.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)holder.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(800f, 100f);
            rect.anchoredPosition = new Vector2(0f, 190f);
        }

        private void SetupDrag(Canvas canvas)
        {
            _drag = BoardDragController.Create(canvas, _gridView);
            _drag.CanPlaceAt = (r, c) => _grid != null && !_grid.HasPion(r, c);
            _drag.OnTrayDropOnCell = (r, c) =>
            {
                if (_acceptDrops && _interactive.Contains((r, c)))
                    _dropQueue.Enqueue(("place", r, c, -1, -1));
                else if (_acceptDrops)
                {
                    _gridView.FlashConflict(r, c);
                    Haptics.VibrateLight();
                }
            };
            _drag.OnTrayDropInvalid = (r, c) =>
            {
                if (!_acceptDrops) return;
                _gridView.FlashConflict(r, c);
                Haptics.VibrateLight();
            };
            _drag.OnPawnMove = (fr, fc, tr, tc) =>
            {
                if (_acceptDrops && _interactive.Contains((tr, tc)))
                    _dropQueue.Enqueue(("move", tr, tc, fr, fc));
                else if (_acceptDrops)
                {
                    _gridView.FlashConflict(tr, tc);
                    Haptics.VibrateLight();
                }
            };
            _drag.OnPawnDropInvalid = (r, c) =>
            {
                if (!_acceptDrops) return;
                _gridView.FlashConflict(r, c);
                Haptics.VibrateLight();
            };
            _drag.OnPawnDropOutside = (r, c) =>
            {
                if (_acceptDrops && _interactive.Contains((r, c)))
                    _dropQueue.Enqueue(("remove", r, c, -1, -1));
            };
            _gridView.OnPawnDragStart = (r, c, e) =>
            {
                if (!_acceptDrops || !_interactive.Contains((r, c))) return;
                _drag.BeginPawnDrag(r, c, _gridView.GetPawnSprite(r, c), e.pointerId);
                _drag.UpdateDrag(e.pointerId, e.position);
            };
            _gridView.OnPawnDrag = (r, c, e) =>
            {
                if (_drag == null || !_drag.IsDragging) return;
                _drag.UpdateDrag(e.pointerId, e.position);
            };
            _gridView.OnPawnDragEnd = (r, c, e) =>
            {
                if (_drag == null || !_drag.IsDragging) return;
                _drag.EndDrag(e.pointerId, e.position);
            };
            RefreshTraySprites();
        }

        private void RefreshTraySprites()
        {
            if (_tray == null)
            {
                var holder = _canvasRect != null ? _canvasRect.Find("TrayHolder") : null;
                if (holder == null) return;
                _tray = AnimalTray.Build(holder, _drag);
            }
            else
            {
                _tray.SetDrag(_drag);
            }
            if (_gridView != null)
            {
                // Démo : 3 jetons suffisent (réutilisables à l'infini), même si la
                // grille d'exemple a plus de zones (ex. 9 pour Grid3_RowCol).
                var all = _gridView.GetZoneAnimalSprites();
                var few = new List<Sprite>();
                for (int i = 0; i < all.Count && few.Count < 3; i++)
                    few.Add(all[i]);
                _tray.SetSprites(few);
            }
        }

        private RectTransform GetTrayChipRect(int index)
        {
            return _tray != null ? _tray.GetChipRect(index) : null;
        }

        private void RebuildGrid(int[,] regions)
        {
            ClearHighlights(); HideOverlay();
            if (_drag != null) _drag.Cancel();
            if (_canvasRect != null) Canvas.ForceUpdateCanvases();
            _grid = new PuzzleGrid(regions);
            _gridView.Build(_grid, _canvasRect);
            LocateCells();
            RefreshTraySprites();
            _interactive.Clear(); _tapQueue.Clear(); _dropQueue.Clear(); _victory = false;
        }

        private void HandleCellTapped(int row, int col)
        {
            if (!_acceptTaps) return;
            if (!_interactive.Contains((row,col))) { Haptics.VibrateLight(); return; }
            _tapQueue.Enqueue((row,col,Time.unscaledTime));
        }

        private IEnumerator WaitTap()
        {
            _tapQueue.Clear();
            while (_tapQueue.Count == 0) yield return null;
            var t = _tapQueue.Dequeue();
            _lastTap = (t.row, t.col);
        }

        private IEnumerator WaitSingleTap()
        {
            _tapQueue.Clear();
            while (_tapQueue.Count == 0) yield return null;
            var t = _tapQueue.Dequeue();
            _lastTap = (t.row, t.col);
            yield return new WaitForSecondsRealtime(0.05f);
        }

        private (string kind, int row, int col, int fromRow, int fromCol) _lastDrop;
        private bool _lastActivityIsDrop;

        /// <summary>
        /// Point local pour la démo "glisser hors grille" : à la verticale de la
        /// case mais sous le bas du plateau (jamais sur une autre case).
        /// </summary>
        private Vector2 DropOffBoardLocal(RectTransform cellRect)
        {
            if (cellRect == null || _canvasRect == null) return new Vector2(0f, -500f);
            Vector3 world = cellRect.TransformPoint(cellRect.rect.center);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, world);
            Vector2 cellLocal;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out cellLocal);
            float boardBottom = cellLocal.y - 380f;
            var board = _gridView != null ? _gridView.BoardContainer : null;
            if (board != null)
            {
                Vector3[] corners = new Vector3[4];
                board.GetWorldCorners(corners);
                Vector2 cornerScreen = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
                Vector2 cornerLocal;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, cornerScreen, null, out cornerLocal))
                    boardBottom = cornerLocal.y - 140f;
            }
            return new Vector2(cellLocal.x, boardBottom);
        }

        private IEnumerator WaitDropOnCell(int row, int col)
        {
            _dropQueue.Clear();
            while (true)
            {
                while (_dropQueue.Count == 0) yield return null;
                var d = _dropQueue.Dequeue();
                if ((d.kind == "place" || d.kind == "move") && d.row == row && d.col == col)
                    yield break;
                Haptics.VibrateLight();
            }
        }

        private IEnumerator WaitDropRemove(int row, int col)
        {
            _dropQueue.Clear();
            while (true)
            {
                while (_dropQueue.Count == 0) yield return null;
                var d = _dropQueue.Dequeue();
                if (d.kind == "remove" && d.row == row && d.col == col)
                    yield break;
                Haptics.VibrateLight();
            }
        }

        /// <summary>Attend indifféremment un tap ou un drop (résolution libre).</summary>
        private IEnumerator WaitActivity()
        {
            _tapQueue.Clear();
            _dropQueue.Clear();
            while (_tapQueue.Count == 0 && _dropQueue.Count == 0) yield return null;
            if (_dropQueue.Count > 0)
            {
                _lastDrop = _dropQueue.Dequeue();
                _lastActivityIsDrop = true;
            }
            else
            {
                var t = _tapQueue.Dequeue();
                _lastTap = (t.row, t.col);
                _lastActivityIsDrop = false;
                yield return new WaitForSecondsRealtime(0.05f);
            }
        }

        private void SetAccept(bool a) { _acceptTaps = a; _tapQueue.Clear(); }
        private void SetAcceptDrops(bool a) { _acceptDrops = a; _dropQueue.Clear(); }
        private void SetInteractive(params (int row,int col)[] cells) { _interactive.Clear(); if (cells!=null) foreach(var c in cells) _interactive.Add(c); }
        private void SetInteractiveAll() { _interactive.Clear(); for(int r=0;r<_grid.Size;r++) for(int c=0;c<_grid.Size;c++) _interactive.Add((r,c)); }

        private void PlacePiece(int r,int c){ _grid.PlacePion(r,c); _gridView.SetPion(r,c,true); }
        private void PunchAndConfetti(int r,int c){ _gridView.PunchBoard(1.04f,0.15f); SFXManager.Instance.PlayConfirm(); Haptics.VibrateLight(); try{ ConfettiHelper.Burst(this, _canvasRect.GetComponent<Canvas>(), 18); }catch{} }
        private void RemovePiece(int r,int c){ _grid.RemovePion(r,c); _gridView.SetPion(r,c,false); }
        private void FlashAllConflicts(){ bool any=false; foreach(var p in _grid.Pions) if(RuleValidator.GetConflicts(_grid,p.row,p.col).Count>0){ _gridView.FlashConflict(p.row,p.col); any=true; } if(any) Haptics.VibrateLight(); }
        private void PlayVictory(){ _gridView.PlayVictoryZoom(); Haptics.VibrateStrong(); }

        private void SetHighlights(IEnumerable<(int row,int col)> cells){ ClearHighlights(); if(cells==null) return; foreach(var cell in cells) AddHighlight(cell.row, cell.col); }
        private void HighlightSingle(int r,int c){ SetHighlights(new (int,int)[]{ (r,c)}); }
        private void ClearHighlights(){ foreach(var (img,rect) in _highlightOverlays) if(img!=null) Destroy(img.gameObject); _highlightOverlays.Clear(); }
        private void AddHighlight(int r,int c)
        {
            if (_cells==null) return; var cell=_cells[r,c]; if(cell==null) return;
            var go=new GameObject("TutorialHighlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect=(RectTransform)go.transform; rect.SetParent(cell.transform,false); rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=Vector2.zero; rect.offsetMax=Vector2.zero;
            var img=go.GetComponent<Image>(); img.sprite=_frameSprite; img.type=Image.Type.Simple; img.color=HighlightColor; img.raycastTarget=false;
            _highlightOverlays.Add((img,rect));
        }

        private void Update()
        {
            if (_highlightOverlays.Count==0) return;
            float pulse=(Mathf.Sin(Time.unscaledTime*HighlightSpeed)+1f)*0.5f;
            float a=Mathf.Lerp(HighlightAlphaMin,HighlightAlphaMax,pulse);
            float s=1f+HighlightScaleAmp*pulse;
            for(int i=_highlightOverlays.Count-1;i>=0;i--){ var (img,rect)=_highlightOverlays[i]; if(img==null){ _highlightOverlays.RemoveAt(i); continue; } var col=img.color; col.a=a; img.color=col; rect.localScale=new Vector3(s,s,s); }
        }

        private void LocateCells()
        {
            RectTransform board = _gridView.BoardContainer;
            if(board==null) board = (RectTransform)_canvasRect.Find("Board");
            if(board==null) throw new InvalidOperationException("[Zoologic] TutorialManager : Board introuvable.");
            CellView[] views=board.GetComponentsInChildren<CellView>(true);
            int n=_grid.Size;
            if(views.Length!=n*n)
            {
                var filtered=new List<CellView>();
                foreach(var v in views) if(v.transform.parent!=board) filtered.Add(v); else filtered.Add(v);
                views=filtered.ToArray();
            }
            if(views.Length!=n*n) throw new InvalidOperationException("[Zoologic] TutorialManager : attendu "+(n*n)+" cases, obtenu "+views.Length+".");
            _cells=new CellView[n,n]; for(int i=0;i<views.Length;i++) _cells[i/n,i%n]=views[i];
        }

        private RectTransform GetCellRect(int r,int c)
        {
            if(_cells==null||r<0||r>=_cells.GetLength(0)||c<0||c>=_cells.GetLength(1)) return null;
            var cell=_cells[r,c]; return cell!=null ? (RectTransform)cell.transform : null;
        }

        private void CreateOverlay(Canvas canvas)
        {
            _overlayRoot=new GameObject("FocusOverlay", typeof(RectTransform));
            _overlayRoot.transform.SetParent(canvas.transform,false);
            var rt=(RectTransform)_overlayRoot.transform;
            rt.anchorMin=Vector2.zero; rt.anchorMax=Vector2.one; rt.offsetMin=Vector2.zero; rt.offsetMax=Vector2.zero;
            _overlayRoot.SetActive(false);
            _blocker = CreateOverlayPanel(_overlayRoot.transform, "Blocker", new Color(0.12f,0.08f,0.05f,0.45f), true);
            _overlayTop = CreateOverlayPanel(_overlayRoot.transform, "Top", new Color(0.12f,0.08f,0.05f,0.45f), true);
            _overlayBottom = CreateOverlayPanel(_overlayRoot.transform, "Bottom", new Color(0.12f,0.08f,0.05f,0.45f), true);
            _overlayLeft = CreateOverlayPanel(_overlayRoot.transform, "Left", new Color(0.12f,0.08f,0.05f,0.45f), true);
            _overlayRight = CreateOverlayPanel(_overlayRoot.transform, "Right", new Color(0.12f,0.08f,0.05f,0.45f), true);
        }

        private Image CreateOverlayPanel(Transform parent, string name, Color col, bool raycast)
        {
            var go=new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent,false);
            var img=go.GetComponent<Image>(); img.color=col; img.raycastTarget=raycast;
            return img;
        }

        private void ShowOverlay(int r,int c)
        {
            var rect=GetCellRect(r,c); if(rect==null) return;
            ShowOverlayRect(rect);
        }

        private void ShowMultiOverlay(IEnumerable<(int row,int col)> cells)
        {
            if(cells==null) return;
            var list=new List<RectTransform>();
            foreach(var cell in cells){ var rt=GetCellRect(cell.row,cell.col); if(rt!=null) list.Add(rt); }
            if(list.Count==0) return;
            if(list.Count==1){ ShowOverlayRect(list[0]); return; }
            _overlayRoot.SetActive(true);
            var bounds=GetCombinedBounds(list);
            UpdateOverlayPanels(bounds);
        }

        private void ShowOverlayRect(RectTransform target)
        {
            _overlayRoot.SetActive(true);
            var bounds=GetWorldBounds(target);
            UpdateOverlayPanels(bounds);
        }

        private Bounds GetWorldBounds(RectTransform rt)
        {
            Vector3[] c=new Vector3[4]; rt.GetWorldCorners(c);
            Vector3 min=c[0]; Vector3 max=c[2];
            return new Bounds((min+max)*0.5f, max-min);
        }

        private Bounds GetCombinedBounds(List<RectTransform> rects)
        {
            Bounds b=GetWorldBounds(rects[0]);
            for(int i=1;i<rects.Count;i++) b.Encapsulate(GetWorldBounds(rects[i]));
            return b;
        }

        private void UpdateOverlayPanels(Bounds worldBounds)
        {
            Canvas canvas=_canvasRect.GetComponent<Canvas>();
            Camera cam=canvas.worldCamera;
            Vector2 minScreen=RectTransformUtility.WorldToScreenPoint(cam, worldBounds.min);
            Vector2 maxScreen=RectTransformUtility.WorldToScreenPoint(cam, worldBounds.max);
            Vector2 minLocal, maxLocal;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, minScreen, cam, out minLocal);
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, maxScreen, cam, out maxLocal);
            float pad=12f;
            minLocal-=new Vector2(pad,pad); maxLocal+=new Vector2(pad,pad);
            Rect canvasRect=((RectTransform)canvas.transform).rect;
            float topY=canvasRect.yMax, bottomY=canvasRect.yMin, leftX=canvasRect.xMin, rightX=canvasRect.xMax;
            SetPanelRect((RectTransform)_overlayTop.transform, new Vector2(leftX, maxLocal.y), new Vector2(rightX, topY));
            SetPanelRect((RectTransform)_overlayBottom.transform, new Vector2(leftX, bottomY), new Vector2(rightX, minLocal.y));
            SetPanelRect((RectTransform)_overlayLeft.transform, new Vector2(leftX, minLocal.y), new Vector2(minLocal.x, maxLocal.y));
            SetPanelRect((RectTransform)_overlayRight.transform, new Vector2(maxLocal.x, minLocal.y), new Vector2(rightX, maxLocal.y));
            _blocker.transform.SetAsLastSibling();
        }

        private void SetPanelRect(RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin=new Vector2(0.5f,0.5f); rt.anchorMax=new Vector2(0.5f,0.5f); rt.pivot=new Vector2(0.5f,0.5f);
            Vector2 size=max-min; rt.sizeDelta=size; rt.anchoredPosition=(min+max)*0.5f;
        }

        private void HideOverlay(){ if(_overlayRoot!=null) _overlayRoot.SetActive(false); }

        private GameObject _skipRoot;
        private GameObject _actionRoot; private TextMeshProUGUI _actionText;
        private void CreateSkipButton(Canvas canvas)
        {
            _skipRoot = new GameObject("SkipButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            _skipRoot.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)_skipRoot.transform;
            rt.anchorMin = new Vector2(1f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(240f, 84f); rt.anchoredPosition = new Vector2(-16f, -16f);
            var img = _skipRoot.GetComponent<Image>();
            img.sprite = B1UI.Skip ?? JellyUI.ButtonGrey ?? _roundedSprite;
            img.type = Image.Type.Sliced; img.color = Color.white; img.raycastTarget = true;
            var btn = _skipRoot.GetComponent<Button>();
            var skip = B1UI.Skip ?? JellyUI.ButtonGrey;
            JellyUI.ApplyJellyButton(btn, img, skip, skip, skip, skip);
            btn.onClick.AddListener(SkipTutorial);
            var txtGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            txtGO.transform.SetParent(_skipRoot.transform, false);
            var txtRect = (RectTransform)txtGO.transform; txtRect.anchorMin = Vector2.zero; txtRect.anchorMax = Vector2.one; txtRect.offsetMin = new Vector2(64f, 6f); txtRect.offsetMax = new Vector2(-12f, -6f);
            var txt = txtGO.GetComponent<TextMeshProUGUI>();
            txt.font = _fontTitle != null ? _fontTitle : Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Bold SDF");
            txt.text = LocalizationManager.Get("tutorial.skip"); txt.fontSize = 28; txt.fontStyle = FontStyles.Bold; txt.color = Color.white; txt.alignment = TextAlignmentOptions.Center;
            txt.outlineWidth = 0.18f; txt.outlineColor = new Color(0.25f, 0.12f, 0.06f, 0.9f);
            txt.raycastTarget = false;
            var tsh = txtGO.AddComponent<UnityEngine.UI.Shadow>(); tsh.effectColor = new Color(0f, 0f, 0f, 0.35f); tsh.effectDistance = new Vector2(0f, -2f);
            LocalizationManager.ApplyTo(txt);
            _skipRoot.transform.SetAsLastSibling();
        }

        private void SkipTutorial()
        {
            StopAllCoroutines();
            if (_drag != null) _drag.Cancel();
            SetAccept(false); SetAcceptDrops(false); ClearHighlights(); HideOverlay(); _hand?.Hide();
            SetBubbleVisible(false); SetActionVisible(false);
            if (_skipRoot != null) _skipRoot.SetActive(false);
            MarkCompleted();
            SFXManager.Instance.PlayMenuClose();
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }

        private void CreateBubble(Canvas canvas)
        {
            _bubbleRoot=new GameObject("InstructionBubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _bubbleRoot.transform.SetParent(canvas.transform,false);
            var rt=(RectTransform)_bubbleRoot.transform;
            rt.anchorMin=new Vector2(0.5f,1f); rt.anchorMax=new Vector2(0.5f,1f); rt.pivot=new Vector2(0.5f,1f);
            rt.sizeDelta=new Vector2(900f,190f); rt.anchoredPosition=new Vector2(0f,-120f);
            var img=_bubbleRoot.GetComponent<Image>(); img.sprite=B1UI.Bubble ?? _roundedSprite; img.type=Image.Type.Sliced; img.color=new Color(1f,0.985f,0.95f,1f); img.raycastTarget=false;
            var bOl=_bubbleRoot.AddComponent<Outline>(); bOl.effectColor=new Color(1f,1f,1f,0.9f); bOl.effectDistance=new Vector2(3f,-3f);
            var bSh=_bubbleRoot.AddComponent<Shadow>(); bSh.effectColor=new Color(0.25f,0.15f,0.08f,0.30f); bSh.effectDistance=new Vector2(0f,-8f);
            var owlGO=new GameObject("Mascot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            owlGO.transform.SetParent(_bubbleRoot.transform,false);
            var owlRect=(RectTransform)owlGO.transform;
            owlRect.anchorMin=new Vector2(0f,0.5f); owlRect.anchorMax=new Vector2(0f,0.5f); owlRect.pivot=new Vector2(0.5f,0.5f);
            owlRect.sizeDelta=new Vector2(110f,110f); owlRect.anchoredPosition=new Vector2(80f,0f);
            var owlImg=owlGO.GetComponent<Image>();
            owlImg.sprite=Resources.Load<Sprite>("Art/Animals/owl");
            owlImg.preserveAspect=true; owlImg.raycastTarget=false;
            var txtGO=new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            txtGO.transform.SetParent(_bubbleRoot.transform,false);
            var txtRect=(RectTransform)txtGO.transform;
            txtRect.anchorMin=Vector2.zero; txtRect.anchorMax=Vector2.one; txtRect.offsetMin=new Vector2(200f,18f); txtRect.offsetMax=new Vector2(-28f,-18f);
            _bubbleText=txtGO.GetComponent<TextMeshProUGUI>();
            _bubbleText.font=_fontTitle!=null?_fontTitle:Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Bold SDF");
            _bubbleText.fontSize=36; _bubbleText.fontStyle=FontStyles.Bold; _bubbleText.color=new Color(0.29f,0.18f,0.10f,1f); _bubbleText.alignment=TextAlignmentOptions.MidlineLeft;
            _bubbleText.textWrappingMode=TextWrappingModes.Normal; _bubbleText.overflowMode=TextOverflowModes.Truncate; _bubbleText.raycastTarget=false;
            var sh=txtGO.AddComponent<Shadow>(); sh.effectColor=new Color(1f,0.98f,0.92f,0.9f); sh.effectDistance=new Vector2(0f,-2f);
            _bubbleRoot.SetActive(false);

            _actionRoot=new GameObject("ActionBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            _actionRoot.transform.SetParent(canvas.transform,false);
            var art=(RectTransform)_actionRoot.transform;
            art.anchorMin=new Vector2(0.5f,0f); art.anchorMax=new Vector2(0.5f,0f); art.pivot=new Vector2(0.5f,0f);
            art.sizeDelta=new Vector2(480f,100f); art.anchoredPosition=new Vector2(0f,64f);
            var aImg=_actionRoot.GetComponent<Image>();
            var jellyGreen=B1UI.Primary ?? JellyUI.ButtonGreen ?? _roundedSprite;
            aImg.sprite=jellyGreen; aImg.type=Image.Type.Sliced; aImg.pixelsPerUnitMultiplier=1f; aImg.color=Color.white; aImg.raycastTarget=true;
            var aOl=_actionRoot.AddComponent<Outline>(); aOl.effectColor=new Color(1f,1f,1f,0.7f); aOl.effectDistance=new Vector2(2f,-2f);
            var aSh=_actionRoot.AddComponent<Shadow>(); aSh.effectColor=new Color(0.15f,0.35f,0.15f,0.40f); aSh.effectDistance=new Vector2(0f,-6f);
            _bubbleNext=_actionRoot.GetComponent<Button>();
            JellyUI.ApplyJellyButton(_bubbleNext, aImg, JellyUI.ButtonGreen, JellyUI.ButtonYellow, JellyUI.ButtonRed, JellyUI.ButtonGrey);
            _bubbleNext.onClick.AddListener(()=>{ _nextClicked=true; SFXManager.Instance.PlayMenuClose(); });
            var atxtGO=new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            atxtGO.transform.SetParent(_actionRoot.transform,false);
            var atxtRect=(RectTransform)atxtGO.transform; atxtRect.anchorMin=Vector2.zero; atxtRect.anchorMax=Vector2.one; atxtRect.offsetMin=new Vector2(18f,6f); atxtRect.offsetMax=new Vector2(-18f,-6f);
            _actionText=atxtGO.GetComponent<TextMeshProUGUI>();
            _actionText.font=_fontTitle!=null?_fontTitle:Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Bold SDF");
            _actionText.text=LocalizationManager.Get("tutorial.next"); _actionText.fontSize=34; _actionText.fontStyle=FontStyles.Bold; _actionText.color=Color.white; _actionText.alignment=TextAlignmentOptions.Center;
            var ash=atxtGO.AddComponent<Shadow>(); ash.effectColor=new Color(0f,0f,0f,0.35f); ash.effectDistance=new Vector2(0f,-3f);
            _actionRoot.SetActive(false);
        }

        private Coroutine _bubblePopRoutine; private Coroutine _actionPopRoutine;
        private void SetBubbleVisible(bool v)
        {
            if(_bubbleRoot==null) return;
            if(v)
            {
                _bubbleRoot.SetActive(true);
                _bubbleRoot.transform.SetAsLastSibling();
                _bubbleRoot.transform.localScale = Vector3.zero;
                if(_bubblePopRoutine!=null) StopCoroutine(_bubblePopRoutine);
                _bubblePopRoutine = StartCoroutine(BubblePopRoutine(_bubbleRoot.transform));
                if(_skipRoot!=null) _skipRoot.transform.SetAsLastSibling();
                if(_hand!=null) _hand.transform.SetAsLastSibling();
            }
            else
            {
                if(_bubblePopRoutine!=null) { StopCoroutine(_bubblePopRoutine); _bubblePopRoutine=null; }
                _bubbleRoot.SetActive(false);
            }
        }
        private void SetActionVisible(bool v, string label=null)
        {
            if(_actionRoot==null) return;
            if(v)
            {
                if(_actionText!=null) _actionText.text = label ?? LocalizationManager.Get("tutorial.next");
                _actionRoot.SetActive(true);
                _actionRoot.transform.SetAsLastSibling();
                _actionRoot.transform.localScale = Vector3.zero;
                if(_actionPopRoutine!=null) StopCoroutine(_actionPopRoutine);
                _actionPopRoutine = StartCoroutine(BubblePopRoutine(_actionRoot.transform));
                if(_skipRoot!=null) _skipRoot.transform.SetAsLastSibling();
                if(_hand!=null) _hand.transform.SetAsLastSibling();
            }
            else
            {
                if(_actionPopRoutine!=null) { StopCoroutine(_actionPopRoutine); _actionPopRoutine=null; }
                _actionRoot.SetActive(false);
            }
        }

        private IEnumerator BubblePopRoutine(Transform t)
        {
            float d = 0.32f; float e = 0f;
            while(e < d)
            {
                e += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(e / d);
                float s = Easing.EaseOutBack(k);
                t.localScale = new Vector3(s, s, s);
                yield return null;
            }
            t.localScale = Vector3.one;
        }

        private IEnumerator BubblePopRoutine()
        {
            yield return BubblePopRoutine(_bubbleRoot.transform);
        }

        private IEnumerator ShowBubble(string text, bool showNext)
        {
            if(_bubbleText==null) { yield return new WaitForSecondsRealtime(1.2f); yield break; }
            _bubbleText.text=text;
            SetBubbleVisible(true);
            if(showNext)
            {
                SetActionVisible(true);
                _nextClicked=false;
                yield return new WaitUntil(()=>_nextClicked);
                SetActionVisible(false);
                SetBubbleVisible(false);
                yield return new WaitForSecondsRealtime(0.2f);
            }
            else
            {
                SetActionVisible(false);
                yield return new WaitForSecondsRealtime(1.4f);
                SetBubbleVisible(false);
            }
        }

        private static Canvas EnsureCanvas()
        {
            Canvas canvas=FindFirstObjectByType<Canvas>();
            if(canvas!=null) return canvas;
            var go=new GameObject("UICanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas=go.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=go.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1080f,1920f); scaler.matchWidthOrHeight=0.5f;
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            foreach(var m in UnityEngine.Object.FindObjectsByType<StandaloneInputModule>(FindObjectsSortMode.None)) UnityEngine.Object.DestroyImmediate(m);
            if(EventSystem.current!=null)
            {
                var cur=EventSystem.current;
                if(cur.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>()==null) cur.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                return;
            }
            var es=new GameObject("EventSystem", typeof(EventSystem));
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        private void CreateBackground(Canvas canvas){ BackgroundHelper.ApplyBackground(canvas.transform); }

        private static Sprite CreateRoundedRectSprite()
        {
            const int res=128; const float cr=0.16f;
            var tex=new Texture2D(res,res,TextureFormat.RGBA32,false); tex.wrapMode=TextureWrapMode.Clamp; tex.filterMode=FilterMode.Bilinear;
            float half=(res-1)*0.5f; float rad=res*cr;
            for(int y=0;y<res;y++) for(int x=0;x<res;x++){ float px=x-half; float py=y-half; float d=SdfRoundedRect(px,py,rad,half); float a=Mathf.Clamp01(0.5f-d); tex.SetPixel(x,y,new Color(1f,1f,1f,a)); }
            tex.Apply(); return Sprite.Create(tex,new Rect(0,0,res,res),new Vector2(0.5f,0.5f));
        }

        private static Sprite CreateFrameSprite()
        {
            const int res=128; const float cr=0.22f; const float br=0.14f;
            var tex=new Texture2D(res,res,TextureFormat.RGBA32,false); tex.wrapMode=TextureWrapMode.Clamp; tex.filterMode=FilterMode.Bilinear;
            float half=(res-1)*0.5f; float outer=res*cr; float inner=outer-res*br;
            for(int y=0;y<res;y++) for(int x=0;x<res;x++){ float px=x-half; float py=y-half; float o=SdfRoundedRect(px,py,outer,half); float i=SdfRoundedRect(px,py,inner,half); float ring=Mathf.Clamp01(0.5f-o)*Mathf.Clamp01(0.5f+i); tex.SetPixel(x,y,new Color(1f,1f,1f,ring)); }
            tex.Apply(); return Sprite.Create(tex,new Rect(0,0,res,res),new Vector2(0.5f,0.5f));
        }

        private static float SdfRoundedRect(float px,float py,float rad,float half){ float inner=half-rad; float qx=Mathf.Clamp(px,-inner,inner); float qy=Mathf.Clamp(py,-inner,inner); float dx=px-qx; float dy=py-qy; return Mathf.Sqrt(dx*dx+dy*dy)-rad; }

        private static Font FindBuiltinFont()
        {
            try{ var f=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); if(f!=null) return f; }catch(Exception){ }
            try{ return Resources.GetBuiltinResource<Font>("Arial.ttf"); }catch(Exception){ return null; }
        }
    }
}
