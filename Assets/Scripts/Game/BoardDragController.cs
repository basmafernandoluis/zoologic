using System;
using UnityEngine;
using UnityEngine.UI;

namespace Zoologic
{
    /// <summary>
    /// Drag &amp; drop du plateau : fantôme sous le doigt, cadre de drop sur la
    /// case survolée, résolution du drop (poser / déplacer / retirer / annuler).
    ///
    /// Deux origines : un jeton de la barre d'animaux (pose) ou un pion posé
    /// (déplacement ou retrait hors plateau). Le tap simple reste géré par
    /// <see cref="CellView"/> ; <see cref="SuppressTap"/> évite le tap fantôme
    /// quand un drop se termine sur une case.
    /// </summary>
    public sealed class BoardDragController : MonoBehaviour
    {
        /// <summary>Dernier drop (temps unscaled) : ignore les taps parasites juste après.</summary>
        public static float LastDropTime = -10f;

        public static bool SuppressTap()
        {
            return Time.unscaledTime - LastDropTime < 0.25f;
        }

        /// <summary>Jeton barre → case libre.</summary>
        public Action<int, int> OnTrayDropOnCell;

        /// <summary>Jeton barre → case occupée.</summary>
        public Action<int, int> OnTrayDropInvalid;

        /// <summary>Pion → autre case libre (déplacement).</summary>
        public Action<int, int, int, int> OnPawnMove;

        /// <summary>Pion → case occupée.</summary>
        public Action<int, int> OnPawnDropInvalid;

        /// <summary>Pion → hors plateau (retrait).</summary>
        public Action<int, int> OnPawnDropOutside;

        /// <summary>La case accepte-t-elle une pose (libre) ?</summary>
        public Func<int, int, bool> CanPlaceAt;

        private Canvas _canvas;
        private GridView _gridView;
        private Image _ghost;
        private GameObject _dropFrame;
        private Image _dropFrameImg;

        private int _activePointer = -1;
        private bool _dragging;
        private bool _fromTray;
        private int _fromRow = -1;
        private int _fromCol = -1;

        // Point écran du FANTÔME (pas du doigt) : cadre et résolution du drop
        // suivent ce que le joueur voit, jamais la case sous le doigt.
        private Vector2 _ghostScreen;
        private bool _ghostScreenValid;

        // Le fantôme flotte au-dessus du doigt pour laisser la cible visible (S22).
        private const float GhostLift = 110f;
        private const float GhostSizeFallback = 128f;

        public static BoardDragController Create(Canvas canvas, GridView gridView)
        {
            var go = new GameObject("BoardDrag");
            go.transform.SetParent(canvas.transform, false);
            var comp = go.AddComponent<BoardDragController>();
            comp._canvas = canvas;
            comp._gridView = gridView;
            return comp;
        }

        public void SetGridView(GridView gridView)
        {
            _gridView = gridView;
        }

        public bool IsDragging => _dragging;

        public void BeginTrayDrag(Sprite sprite, int pointerId)
        {
            if (_dragging || sprite == null)
                return;
            _dragging = true;
            _fromTray = true;
            _fromRow = -1;
            _fromCol = -1;
            _activePointer = pointerId;
            ShowGhost(sprite);
        }

        public void BeginPawnDrag(int row, int col, Sprite sprite, int pointerId)
        {
            if (_dragging || sprite == null)
                return;
            _dragging = true;
            _fromTray = false;
            _fromRow = row;
            _fromCol = col;
            _activePointer = pointerId;
            if (_gridView != null)
                _gridView.SetCellDimmed(row, col, true);
            ShowGhost(sprite);
        }

        public void UpdateDrag(int pointerId, Vector2 screenPosition)
        {
            if (!_dragging || pointerId != _activePointer)
                return;
            MoveGhost(screenPosition);
            Vector2 dropPoint = GhostScreenPoint(screenPosition);
            UpdateDropFrame(dropPoint);
        }

        public void EndDrag(int pointerId, Vector2 screenPosition)
        {
            if (!_dragging || pointerId != _activePointer)
                return;
            // Résolution au fantôme : là où le joueur visait, pas sous son doigt.
            Vector2 dropPoint = _ghostScreenValid ? _ghostScreen : screenPosition;
            Camera cam = _canvas != null ? _canvas.worldCamera : null;
            int row = -1;
            int col = -1;
            bool overCell = _gridView != null
                && _gridView.TryGetCellAtScreenPoint(dropPoint, cam, out row, out col);
            bool overBoard = !overCell && _gridView != null && _gridView.IsOverBoard(dropPoint, cam);

            try
            {
                if (_fromTray)
                {
                    if (overCell)
                    {
                        if (CanPlaceAt != null && CanPlaceAt(row, col))
                            OnTrayDropOnCell?.Invoke(row, col);
                        else
                            OnTrayDropInvalid?.Invoke(row, col);
                    }
                    // Hors plateau depuis la barre : simple annulation.
                }
                else
                {
                    if (overCell)
                    {
                        if (row == _fromRow && col == _fromCol)
                        {
                            // Repos sur sa case : annulation.
                        }
                        else if (CanPlaceAt != null && CanPlaceAt(row, col))
                            OnPawnMove?.Invoke(_fromRow, _fromCol, row, col);
                        else
                            OnPawnDropInvalid?.Invoke(row, col);
                    }
                    else if (!overBoard)
                    {
                        OnPawnDropOutside?.Invoke(_fromRow, _fromCol);
                    }
                    // Sur le fond du plateau mais hors case (gaps) : annulation.
                }
            }
            finally
            {
                LastDropTime = Time.unscaledTime;
                StopDrag();
            }
        }

        /// <summary>Annule le drag en cours (victoire, défaite, retry).</summary>
        public void Cancel()
        {
            StopDrag();
        }

        private void StopDrag()
        {
            if (!_dragging)
            {
                HideGhost();
                HideDropFrame();
                return;
            }
            _dragging = false;
            _activePointer = -1;
            if (!_fromTray && _gridView != null && _fromRow >= 0)
                _gridView.SetCellDimmed(_fromRow, _fromCol, false);
            _fromRow = -1;
            _fromCol = -1;
            HideGhost();
            HideDropFrame();
        }

        private void ShowGhost(Sprite sprite)
        {
            if (_ghost == null)
            {
                var go = new GameObject("DragGhost", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(_canvas.transform, false);
                _ghost = go.GetComponent<Image>();
                _ghost.raycastTarget = false;
            }
            _ghost.sprite = sprite;
            _ghost.type = Image.Type.Simple;
            _ghost.preserveAspect = true;
            Color tint = SkinManager.SelectedTint;
            _ghost.color = new Color(tint.r, tint.g, tint.b, 0.92f);
            float slot = _gridView != null ? _gridView.SlotSize : 0f;
            float ghostSize = slot > 0f ? Mathf.Clamp(slot * 0.8f, 72f, 170f) : GhostSizeFallback;
            var rect = (RectTransform)_ghost.transform;
            rect.sizeDelta = new Vector2(ghostSize, ghostSize);
            rect.pivot = new Vector2(0.5f, 0.5f);
            _ghost.transform.SetAsLastSibling();
            _ghost.gameObject.SetActive(true);
            _ghostScreenValid = false;
            HideDropFrame();
        }

        private void MoveGhost(Vector2 screenPosition)
        {
            if (_ghost == null || _canvas == null)
                return;
            Camera cam = _canvas.worldCamera;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)_canvas.transform, screenPosition, cam, out local))
                return;
            ((RectTransform)_ghost.transform).anchoredPosition = local + new Vector2(0f, GhostLift);
            _ghostScreen = GhostScreenPoint(screenPosition);
            _ghostScreenValid = true;
        }

        /// <summary>Point écran du fantôme (= doigt + lift), pour cadre et drop.</summary>
        private Vector2 GhostScreenPoint(Vector2 fingerScreen)
        {
            if (_ghost == null || _canvas == null)
                return fingerScreen;
            Camera cam = _canvas.worldCamera;
            Vector3 world = _canvas.transform.TransformPoint(
                ((RectTransform)_ghost.transform).anchoredPosition);
            return RectTransformUtility.WorldToScreenPoint(cam, world);
        }

        private void HideGhost()
        {
            if (_ghost != null)
                _ghost.gameObject.SetActive(false);
        }

        private void UpdateDropFrame(Vector2 screenPosition)
        {
            Camera cam = _canvas != null ? _canvas.worldCamera : null;
            if (_gridView == null
                || !_gridView.TryGetCellAtScreenPoint(screenPosition, cam, out int row, out int col))
            {
                HideDropFrame();
                return;
            }
            RectTransform cellRect = _gridView.GetCellRect(row, col);
            if (cellRect == null)
            {
                HideDropFrame();
                return;
            }
            if (_dropFrame == null)
            {
                _dropFrame = new GameObject("DropFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                _dropFrame.transform.SetParent(_gridView.BoardContainer, false);
                _dropFrameImg = _dropFrame.GetComponent<Image>();
                _dropFrameImg.sprite = GridView.SharedRoundedRect;
                _dropFrameImg.type = Image.Type.Simple;
                _dropFrameImg.color = new Color(1f, 0.82f, 0.18f, 0.55f);
                _dropFrameImg.raycastTarget = false;
            }
            if (_dropFrame.transform.parent != _gridView.BoardContainer)
                _dropFrame.transform.SetParent(_gridView.BoardContainer, false);
            var frameRect = (RectTransform)_dropFrame.transform;
            frameRect.anchorMin = cellRect.anchorMin;
            frameRect.anchorMax = cellRect.anchorMax;
            frameRect.pivot = cellRect.pivot;
            frameRect.sizeDelta = cellRect.sizeDelta * 1.04f;
            frameRect.anchoredPosition = cellRect.anchoredPosition;
            // Vert = la case accepte le drop, rouge = occupée, doré = case
            // d'origine du pion (reposer = annuler). Lisible avant de lâcher.
            bool isOrigin = !_fromTray && row == _fromRow && col == _fromCol;
            bool free = CanPlaceAt != null && CanPlaceAt(row, col);
            _dropFrameImg.color = isOrigin
                ? new Color(1f, 0.82f, 0.18f, 0.55f)
                : free
                    ? new Color(0.20f, 0.78f, 0.42f, 0.60f)
                    : new Color(0.90f, 0.25f, 0.25f, 0.60f);
            if (!_dropFrame.activeSelf)
                _dropFrame.SetActive(true);
        }

        private void HideDropFrame()
        {
            if (_dropFrame != null)
                _dropFrame.SetActive(false);
        }
    }
}
