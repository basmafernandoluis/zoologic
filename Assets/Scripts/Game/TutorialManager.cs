using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Zoologic.Core;

namespace Zoologic
{
    public sealed class TutorialManager : MonoBehaviour
    {
        public const string HasCompletedKey = "HasCompletedTutorial";
        public static bool HasCompleted => PlayerPrefs.GetInt(HasCompletedKey, 0) == 1;
        public static void MarkCompleted() => PlayerPrefs.SetInt(HasCompletedKey, 1);
        public static void ResetTutorial() => PlayerPrefs.DeleteKey(HasCompletedKey);
        public static bool ShouldShow => !HasCompleted;

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
        private const float HighlightScaleAmp = 0.05f;
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
        private readonly Queue<(int row, int col)> _tapQueue = new Queue<(int row, int col)>();
        private (int row, int col) _lastTap;
        private bool _acceptTaps;
        private bool _victory;

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
        }

        private void Start()
        {
            if (HasCompleted)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
                return;
            }
            Canvas canvas = EnsureCanvas();
            EnsureEventSystem();
            CreateBackground(canvas);
            _canvasRect = (RectTransform)canvas.transform;
            _fontTitle = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Bold SDF");
            _fontBody = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Regular SDF");
            _frameSprite = CreateFrameSprite();
            _roundedSprite = CreateRoundedRectSprite();
            CreateOverlay(canvas);
            CreateBubble(canvas);
            _hand = UIHandPointer.Create(canvas);

            _grid = new PuzzleGrid(TutorialRegions);
            _gridView.OnCellTapped = HandleCellTapped;
            _gridView.Build(_grid, _canvasRect);
            LocateCells();
            StartCoroutine(RunTutorial());
        }

        private IEnumerator RunTutorial()
        {
            yield return StartCoroutine(Step1_RowCol());
            yield return StartCoroutine(Step2_Zone());
            yield return StartCoroutine(Step3_Adjacency());
            yield return StartCoroutine(Step4_XElimination());
            SetAccept(false); ClearHighlights(); HideOverlay(); _hand.Hide();
            yield return ShowBubble("Bravo, le tutoriel est terminé ! Tu es prêt pour les vrais niveaux.", true);
            MarkCompleted();
            yield return new WaitForSecondsRealtime(1f);
            SetBubbleVisible(false);
        }

        private IEnumerator Step1_RowCol()
        {
            RebuildGrid(Grid3_RowCol);
            yield return ShowBubble("Bienvenue dans Zoo Logic ! Découvrons les règles.", true);
            yield return ShowBubble("Règle 1 : Chaque ligne et colonne ne peut contenir qu'un seul animal.", true);
            var target = (0, 0);
            HighlightSingle(target.Item1, target.Item2);
            ShowOverlay(target.Item1, target.Item2);
            _hand.PointTo(GetCellRect(target.Item1, target.Item2));
            yield return ShowBubble("Touche la case surlignée pour placer un animal.", false);
            SetInteractive(target); SetAccept(true);
            yield return WaitTap(); _hand.PlayTap(); SetAccept(false);
            PlacePiece(target.Item1, target.Item2); ClearHighlights(); HideOverlay(); _hand.Hide();
            yield return new WaitForSecondsRealtime(0.6f);
            var forbid = new (int,int)[] { (0,1),(0,2),(1,0),(2,0) };
            SetHighlights(forbid); ShowMultiOverlay(forbid); yield return ShowBubble("Sa ligne et sa colonne sont maintenant bloquées. Essaie de placer un second pion sur la même ligne : touche (0,1).", false);
            SetInteractive((0,1)); SetAccept(true); _hand.PointTo(GetCellRect(0,1));
            yield return WaitTap(); SetAccept(false); _hand.Hide();
            PlacePiece(0,1); FlashAllConflicts(); yield return ShowBubble("Conflit ! Même ligne = interdit.", true);
            SetInteractive((0,1)); SetAccept(true); yield return ShowBubble("Retire-le : retouche la case (0,1).", false); _hand.PointTo(GetCellRect(0,1));
            yield return WaitTap(); SetAccept(false); RemovePiece(0,1); ClearHighlights(); HideOverlay(); _hand.Hide();
            yield return ShowBubble("Parfait : une seule fois par ligne et par colonne.", true);
            RemovePiece(0,0);
        }

        private IEnumerator Step2_Zone()
        {
            RebuildGrid(Grid3_Zone);
            yield return ShowBubble("Règle 2 : Un seul animal par zone de couleur.", true);
            SetHighlights(new (int,int)[] { (0,0),(0,1),(1,0),(1,1) }); ShowMultiOverlay(new (int,int)[] { (0,0),(0,1),(1,0),(1,1) });
            yield return ShowBubble("Cette zone menthe contient 4 cases, mais une seule peut accueillir un animal.", true);
            ClearHighlights(); HideOverlay();
            PlacePiece(0,0); yield return new WaitForSecondsRealtime(0.6f);
            yield return ShowBubble("Zone occupée. La bonne cible est dans l'autre zone : touche (2,2).", false);
            HighlightSingle(2,2); ShowOverlay(2,2); _hand.PointTo(GetCellRect(2,2));
            SetInteractive((2,2)); SetAccept(true);
            yield return WaitTap(); SetAccept(false); _hand.Hide(); ClearHighlights(); HideOverlay();
            PlacePiece(2,2); yield return ShowBubble("Exact ! Chaque couleur n'accueille qu'un pion.", true);
            RemovePiece(0,0); RemovePiece(2,2);
        }

        private IEnumerator Step3_Adjacency()
        {
            RebuildGrid(TutorialRegions);
            yield return ShowBubble("Règle 3 : Les animaux ne peuvent pas se toucher, même en diagonale.", true);
            PlacePiece(1,1);
            var forbid = new (int,int)[] { (0,0),(0,1),(0,2),(1,0),(1,2),(2,0),(2,1),(2,2) };
            SetHighlights(forbid); ShowMultiOverlay(forbid);
            yield return ShowBubble("Un pion au centre bloque ses 8 voisines. Les cases rouges sont interdites.", true);
            ClearHighlights(); HideOverlay();
            yield return ShowBubble("Marque une case interdite avec une croix. Touche (0,0) pour mettre un X.", false);
            HighlightSingle(0,0); ShowOverlay(0,0); _hand.PointTo(GetCellRect(0,0));
            SetInteractive((0,0)); SetAccept(true);
            yield return WaitTap(); SetAccept(false);
            if (_lastTap == (0,0)) { _gridView.SetX(0,0,true); _hand.PlayTap(); }
            ClearHighlights(); HideOverlay(); _hand.Hide();
            yield return new WaitForSecondsRealtime(0.5f);
            yield return ShowBubble("Bien ! Ensuite place un animal valide loin du centre : touche (3,3).", false);
            HighlightSingle(3,3); ShowOverlay(3,3); _hand.PointTo(GetCellRect(3,3));
            SetInteractive((3,3)); SetAccept(true);
            yield return WaitTap(); SetAccept(false); _hand.Hide(); ClearHighlights(); HideOverlay();
            PlacePiece(3,3); yield return ShowBubble("Parfait, aucun contact même en diagonale.", true);
            RemovePiece(1,1); RemovePiece(3,3); _gridView.SetX(0,0,false);
        }

        private IEnumerator Step4_XElimination()
        {
            RebuildGrid(TutorialRegions);
            foreach (var p in Step4Setup) PlacePiece(p.row, p.col);
            _gridView.SetX(1,1,true); _gridView.SetX(2,2,true);
            yield return ShowBubble("Dernière astuce : la croix élimine l'impossible pour déduire le reste.", true);
            yield return ShowBubble("Deux X bloquent déjà des cases. Marque encore (1,0) en X.", false);
            HighlightSingle(1,0); ShowOverlay(1,0); _hand.PointTo(GetCellRect(1,0));
            SetInteractive((1,0)); SetAccept(true);
            yield return WaitTap(); SetAccept(false);
            if (_lastTap == (1,0)) _gridView.SetX(1,0,true);
            ClearHighlights(); HideOverlay(); _hand.Hide();
            yield return ShowBubble("Plus que une case valide dans la zone lavande. Déduis-la !", false);
            SetInteractiveAll(); SetAccept(true);
            _hand.PointTo(GetCellRect(2,0));
            while (!_victory)
            {
                yield return WaitTap();
                var (row,col) = _lastTap;
                if (_grid.HasPion(row,col)) { RemovePiece(row,col); continue; }
                if (_gridView != null && HasX(row,col)) { _gridView.SetX(row,col,false); continue; }
                var conflits = RuleValidator.GetConflicts(_grid, row, col);
                if (conflits.Count > 0) { _gridView.FlashConflict(row,col); Haptics.VibrateLight(); continue; }
                if (TryPlaceWithValidation(row,col)) { if (RuleValidator.IsSolved(_grid)) { _victory = true; PlayVictory(); } else { _hand.PointTo(GetCellRect(2,0)); } }
            }
            SetAccept(false); _hand.Hide(); ClearHighlights(); HideOverlay();
            yield return ShowBubble("Grille résolue ! Le X t'a aidé à voir la seule case possible.", true);
        }

        private bool HasX(int r, int c)
        {
            var cell = GetCell(r,c);
            if (cell == null) return false;
            var t = cell.transform.Find("X");
            return t != null && t.gameObject.activeSelf;
        }

        private bool TryPlaceWithValidation(int r,int c)
        {
            if (_grid.HasPion(r,c)) return false;
            var cell = GetCell(r,c);
            if (cell == null) return false;
            PlacePiece(r,c);
            return true;
        }

        private void RebuildGrid(int[,] regions)
        {
            ClearHighlights(); HideOverlay();
            _grid = new PuzzleGrid(regions);
            _gridView.Build(_grid, _canvasRect);
            LocateCells();
            _interactive.Clear(); _tapQueue.Clear(); _victory = false;
        }

        private void HandleCellTapped(int row, int col)
        {
            if (!_acceptTaps) return;
            if (!_interactive.Contains((row,col))) { Haptics.VibrateLight(); return; }
            _tapQueue.Enqueue((row,col));
        }

        private IEnumerator WaitTap()
        {
            _tapQueue.Clear();
            while (_tapQueue.Count == 0) yield return null;
            _lastTap = _tapQueue.Dequeue();
        }

        private void SetAccept(bool a) { _acceptTaps = a; _tapQueue.Clear(); }
        private void SetInteractive(params (int row,int col)[] cells) { _interactive.Clear(); if (cells!=null) foreach(var c in cells) _interactive.Add(c); }
        private void SetInteractiveAll() { _interactive.Clear(); for(int r=0;r<_grid.Size;r++) for(int c=0;c<_grid.Size;c++) _interactive.Add((r,c)); }

        private void PlacePiece(int r,int c){ _grid.PlacePion(r,c); _gridView.SetPion(r,c,true); }
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
            Transform board=_canvasRect.Find("Board");
            if(board==null) throw new InvalidOperationException("[Zoologic] TutorialManager : Board introuvable.");
            CellView[] views=board.GetComponentsInChildren<CellView>(true);
            int n=_grid.Size; if(views.Length!=n*n) throw new InvalidOperationException("[Zoologic] TutorialManager : attendu "+(n*n)+" cases, obtenu "+views.Length+".");
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
            _blocker = CreateOverlayPanel(_overlayRoot.transform, "Blocker", new Color(0f,0f,0f,0.62f), true);
            _overlayTop = CreateOverlayPanel(_overlayRoot.transform, "Top", new Color(0f,0f,0f,0.62f), true);
            _overlayBottom = CreateOverlayPanel(_overlayRoot.transform, "Bottom", new Color(0f,0f,0f,0.62f), true);
            _overlayLeft = CreateOverlayPanel(_overlayRoot.transform, "Left", new Color(0f,0f,0f,0.62f), true);
            _overlayRight = CreateOverlayPanel(_overlayRoot.transform, "Right", new Color(0f,0f,0f,0.62f), true);
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

        private void CreateBubble(Canvas canvas)
        {
            _bubbleRoot=new GameObject("Bubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _bubbleRoot.transform.SetParent(canvas.transform,false);
            var rt=(RectTransform)_bubbleRoot.transform;
            rt.anchorMin=new Vector2(0.5f,0f); rt.anchorMax=new Vector2(0.5f,0f); rt.pivot=new Vector2(0.5f,0f);
            rt.sizeDelta=new Vector2(860f,190f); rt.anchoredPosition=new Vector2(0f,36f);
            var img=_bubbleRoot.GetComponent<Image>(); img.sprite=_roundedSprite; img.type=Image.Type.Simple; img.color=new Color(0.10f,0.12f,0.16f,0.92f); img.raycastTarget=false;
            var vlg=_bubbleRoot.AddComponent<VerticalLayoutGroup>(); vlg.padding=new RectOffset(22,22,18,14); vlg.spacing=10f; vlg.childAlignment=TextAnchor.MiddleCenter;
            var txtGO=new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            txtGO.transform.SetParent(_bubbleRoot.transform,false);
            _bubbleText=txtGO.GetComponent<TextMeshProUGUI>();
            _bubbleText.font=_fontBody!=null?_fontBody:Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Regular SDF");
            _bubbleText.fontSize=26; _bubbleText.color=Color.white; _bubbleText.alignment=TextAlignmentOptions.Center;
            _bubbleText.enableWordWrapping=true; _bubbleText.raycastTarget=false;
            var txtLE=txtGO.AddComponent<LayoutElement>(); txtLE.preferredHeight=90f;
            var btnGO=new GameObject("Suivant", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(_bubbleRoot.transform,false);
            var btnRect=(RectTransform)btnGO.transform; btnRect.sizeDelta=new Vector2(240f,52f);
            var btnImg=btnGO.GetComponent<Image>(); btnImg.sprite=KenneyUI.Button("Green")??CreateRoundedRectSprite(); btnImg.type=Image.Type.Simple; btnImg.color=Color.white;
            _bubbleNext=btnGO.GetComponent<Button>(); _bubbleNext.targetGraphic=btnImg;
            _bubbleNext.onClick.AddListener(()=>{ _nextClicked=true; SFXManager.Instance.PlayMenuClose(); });
            var btnTxtGO=new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            btnTxtGO.transform.SetParent(btnGO.transform,false);
            var btnTxtRect=(RectTransform)btnTxtGO.transform; btnTxtRect.anchorMin=Vector2.zero; btnTxtRect.anchorMax=Vector2.one; btnTxtRect.offsetMin=Vector2.zero; btnTxtRect.offsetMax=Vector2.zero;
            var btnTxt=btnTxtGO.GetComponent<TextMeshProUGUI>(); btnTxt.font=_fontTitle!=null?_fontTitle:Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Bold SDF");
            btnTxt.text="Suivant"; btnTxt.fontSize=24; btnTxt.fontStyle=FontStyles.Bold; btnTxt.color=Color.white; btnTxt.alignment=TextAlignmentOptions.Center;
            var btnLE=btnGO.AddComponent<LayoutElement>(); btnLE.preferredHeight=52f;
            _bubbleRoot.SetActive(false);
        }

        private void SetBubbleVisible(bool v){ if(_bubbleRoot!=null) _bubbleRoot.SetActive(v); }

        private IEnumerator ShowBubble(string text, bool showNext)
        {
            if(_bubbleText==null) { yield return new WaitForSecondsRealtime(1.2f); yield break; }
            _bubbleText.text=text;
            if(_bubbleNext!=null) _bubbleNext.gameObject.SetActive(showNext);
            SetBubbleVisible(true);
            if(!showNext){ yield return new WaitForSecondsRealtime(1.8f); yield break; }
            _nextClicked=false;
            yield return new WaitUntil(()=>_nextClicked);
            SetBubbleVisible(false);
            yield return new WaitForSecondsRealtime(0.2f);
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
            if(EventSystem.current!=null)
            {
                var cur=EventSystem.current;
                var legacy=cur.GetComponent<StandaloneInputModule>();
                if(legacy!=null) UnityEngine.Object.Destroy(legacy);
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
