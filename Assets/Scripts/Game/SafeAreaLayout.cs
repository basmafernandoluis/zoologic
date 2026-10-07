using UnityEngine;

namespace Zoologic
{
    /// <summary>
    /// Applique la Safe Area iOS/Android au Header/Footer sans casser les Layout Groups.
    /// Cible : iPhone SE (16:9), 19.5:9 notch + punch-hole, gestes Android.
    /// Ref Canvas : 1080x1920, match 0.5 (voir PuzzleGameController.EnsureCanvas).
    ///
    /// Usage : ajouter sur le GameObject Header (Mode=Top) et DockBois/Footer (Mode=Bottom).
    /// Aucun GetComponent dans Update : on ne recalcule que si résolution/safeArea change.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaLayout : MonoBehaviour
    {
        public enum ApplyMode { TopOnly, BottomOnly, TopAndBottom }

        [SerializeField] private ApplyMode _mode = ApplyMode.TopOnly;
        [SerializeField] private float _referenceHeight = 1920f;
        [SerializeField] private float _simulatedTopNotch = 70f;
        [SerializeField] private float _minBottomReserve = 48f;

        private RectTransform _rect;
        private Vector2 _baseOffsetMin;
        private Vector2 _baseOffsetMax;
        private int _lastW;
        private int _lastH;
        private Rect _lastSafe;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _baseOffsetMin = _rect.offsetMin;
            _baseOffsetMax = _rect.offsetMax;
            Apply();
        }

        private void OnRectTransformDimensionsChange()
        {
            // Appelé par Unity lors d'une rotation / resize : recalcul paresseux.
            if (!isActiveAndEnabled) return;
            ApplyIfChanged();
        }

        private void ApplyIfChanged()
        {
            if (Screen.width == _lastW && Screen.height == _lastH && Screen.safeArea == _lastSafe)
                return;
            Apply();
        }

        /// <summary>Top inset en unités canvas (ref 1920).</summary>
        public static float TopInsetCanvas(float refHeight = 1920f, float simulated = 70f)
        {
            float sh = Mathf.Max(Screen.height, 1);
            float insetPx = sh - Screen.safeArea.yMax;
            if (insetPx > 1f)
                return insetPx * (refHeight / sh);
            return simulated; // Éditeur / desktop : prévisu encoche.
        }

        /// <summary>Bottom inset en unités canvas (ref 1920). Gère gestes Android (safeArea.yMin≈0).</summary>
        public static float BottomInsetCanvas(float refHeight = 1920f, float minReserve = 48f)
        {
            // Délègue à GameHUD.SafeBottomPx (dpi fallback) pour rester cohérent.
            float px = GameHUD.SafeBottomPx();
            float canvas = px * (refHeight / Mathf.Max(Screen.height, 1));
            return Mathf.Max(canvas, minReserve);
        }

        public void Apply()
        {
            if (_rect == null) _rect = (RectTransform)transform;
            _lastW = Screen.width;
            _lastH = Screen.height;
            _lastSafe = Screen.safeArea;

            float top = TopInsetCanvas(_referenceHeight, _simulatedTopNotch);
            float bottom = BottomInsetCanvas(_referenceHeight, _minBottomReserve);

            Vector2 omin = _baseOffsetMin;
            Vector2 omax = _baseOffsetMax;

            // On ne touche qu'au padding haut/bas, jamais aux ancres (ne casse pas les Layouts).
            switch (_mode)
            {
                case ApplyMode.TopOnly:
                    omax.y -= top;
                    break;
                case ApplyMode.BottomOnly:
                    omin.y += bottom;
                    break;
                default:
                    omax.y -= top;
                    omin.y += bottom;
                    break;
            }

            _rect.offsetMin = omin;
            _rect.offsetMax = omax;
        }
    }
}
