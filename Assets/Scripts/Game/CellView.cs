using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zoodoku
{
    /// <summary>
    /// Une case de la grille affichée : couleur de fond de la zone, pion, marqueur "X"
    /// et interactions tactiles (tap court / appui long).
    ///
    /// Gère aussi le "juice" local de la case :
    ///  - apparition "pop" du pion (échelle 0 → 115 % → 100 %, easeOutBack) ;
    ///  - feedback de conflit : rouge temporaire + tremblement horizontal rapide ;
    ///  - apparition / disparition en fondu du marqueur "X" (mode brouillon).
    ///
    /// Les coroutines ne sont utilisées qu'en play mode (le mode édition sert aux
    /// tests de fumée, où l'on applique directement l'état final).
    /// </summary>
    public sealed class CellView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private static readonly Color ConflictColor = new Color(0.85f, 0.12f, 0.12f, 1f);
        private static readonly Color XTintColor = new Color(0.55f, 0.55f, 0.60f, 0.7f);

        // Constantes de durée des animations (toutes en secondes).
        public const float PopDuration = 0.18f;
        public const float ShakeDuration = 0.3f;
        public const float FlashDuration = 1f;
        public const float FadeDuration = 0.15f;
        private const float ShakeFrequency = 50f;

        /// <summary>Appelé lors d'un tap court sur la case.</summary>
        public Action OnTap;

        /// <summary>Appelé lors d'un appui long sur la case.</summary>
        public Action OnLongPress;

        private Image _background;
        private Image _pion;
        private Image _xMark;
        private RectTransform _pionRect;
        private Vector3 _basePosition;
        private Color _baseColor;
        private float _longPressDuration;
        private float _shakeAmplitude;

        private bool _pointerDown;
        private float _pressedAt;

        private Coroutine _popRoutine;
        private Coroutine _feedbackRoutine;
        private Coroutine _fadeRoutine;

        /// <summary>
        /// Configure la case : couleur de zone, et crée les enfants "Pion" et "X".
        /// </summary>
        public void Init(Color baseColor, Image background, Sprite pionSprite, Font font,
            float cellSize, float pionRatio, float longPressDuration)
        {
            _baseColor = baseColor;
            _background = background;
            _background.color = baseColor;
            _longPressDuration = longPressDuration;
            _shakeAmplitude = cellSize * 0.03f;

            // Pion : l'icône d'animal de la zone (ou cercle blanc de secours), centrée
            // dans la case et dimensionnée à ~62 % de la case → marge garantie partout.
            var pionGameObject = new GameObject("Pion", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            pionGameObject.transform.SetParent(transform, false);
            _pionRect = (RectTransform)pionGameObject.transform;
            _pionRect.anchorMin = new Vector2(0.5f, 0.5f);
            _pionRect.anchorMax = new Vector2(0.5f, 0.5f);
            _pionRect.pivot = new Vector2(0.5f, 0.5f);
            float pionSize = cellSize * pionRatio;
            _pionRect.sizeDelta = new Vector2(pionSize, pionSize);
            _pionRect.anchoredPosition = Vector2.zero;

            _pion = pionGameObject.GetComponent<Image>();
            _pion.sprite = pionSprite;
            _pion.type = Image.Type.Simple;
            _pion.preserveAspect = true;
            _pion.color = Color.white;
            _pion.raycastTarget = false;
            pionGameObject.SetActive(false);

            // Marqueur "X" (mode brouillon) : image X.png, gris discret,
            // plus petit qu'un pion, pour ne jamais être confondu avec une pièce posée.
            var xGameObject = new GameObject("X", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            xGameObject.transform.SetParent(transform, false);
            var xRect = (RectTransform)xGameObject.transform;
            xRect.anchorMin = new Vector2(0.5f, 0.5f);
            xRect.anchorMax = new Vector2(0.5f, 0.5f);
            xRect.pivot = new Vector2(0.5f, 0.5f);
            xRect.sizeDelta = new Vector2(cellSize * 0.55f, cellSize * 0.55f);
            xRect.anchoredPosition = Vector2.zero;

            _xMark = xGameObject.GetComponent<Image>();
            Sprite xSprite = Resources.Load<Sprite>("UI/X");
            _xMark.sprite = xSprite;
            _xMark.type = Image.Type.Simple;
            _xMark.preserveAspect = true;
            _xMark.color = XTintColor;
            _xMark.raycastTarget = false;
            xGameObject.SetActive(false);
        }

        // ------------------------------------------------------------------
        // Pion (avec effet "pop").
        // ------------------------------------------------------------------

        /// <summary>Affiche ou masque le pion (un pion présent masque le "X").</summary>
        public void SetPion(bool active)
        {
            if (_pion == null)
                return;

            if (active)
            {
                if (_xMark != null)
                    HideXInstant();

                _pion.gameObject.SetActive(true);

                if (Application.isPlaying)
                {
                    if (_popRoutine != null)
                        StopCoroutine(_popRoutine);
                    _popRoutine = StartCoroutine(PopRoutine());
                }
                else
                {
                    _pionRect.localScale = Vector3.one;
                }
            }
            else
            {
                if (_popRoutine != null)
                {
                    StopCoroutine(_popRoutine);
                    _popRoutine = null;
                }

                _pionRect.localScale = Vector3.one;
                _pion.gameObject.SetActive(false);
            }
        }

        /// <summary>Pop du pion : 0 % → ~115 % → 100 % (easeOutBack, très rapide).</summary>
        private IEnumerator PopRoutine()
        {
            float elapsed = 0f;

            while (elapsed < PopDuration)
            {
                float t = Mathf.Clamp01(elapsed / PopDuration);
                float scale = Easing.EaseOutBack(t);

                _pionRect.localScale = new Vector3(scale, scale, scale);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _pionRect.localScale = Vector3.one;
            _popRoutine = null;
        }

        // ------------------------------------------------------------------
        // Marqueur "X" (image, apparition / disparition en fondu).
        // ------------------------------------------------------------------

        /// <summary>Affiche ou masque le marqueur "X" (un "X" présent masque le pion).</summary>
        public void SetX(bool active)
        {
            if (_xMark == null)
                return;

            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
            }

            if (active)
            {
                if (_pion != null)
                {
                    if (_popRoutine != null)
                    {
                        StopCoroutine(_popRoutine);
                        _popRoutine = null;
                    }
                    _pion.gameObject.SetActive(false);
                }

                _xMark.gameObject.SetActive(true);
                SetXAlpha(0f);

                if (Application.isPlaying)
                    _fadeRoutine = StartCoroutine(FadeXIn());
                else
                    SetXAlpha(1f);
            }
            else
            {
                if (Application.isPlaying && _xMark.gameObject.activeSelf)
                    _fadeRoutine = StartCoroutine(FadeXOut());
                else
                    HideXInstant();
            }
        }

        private IEnumerator FadeXIn()
        {
            float elapsed = 0f;

            while (elapsed < FadeDuration)
            {
                float t = Mathf.Clamp01(elapsed / FadeDuration);
                SetXAlpha(Easing.EaseOutCubic(t));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            SetXAlpha(1f);
            _fadeRoutine = null;
        }

        private IEnumerator FadeXOut()
        {
            float elapsed = 0f;

            while (elapsed < FadeDuration)
            {
                float t = Mathf.Clamp01(elapsed / FadeDuration);
                SetXAlpha(1f - t);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            HideXInstant();
            _fadeRoutine = null;
        }

        private void SetXAlpha(float alpha)
        {
            Color color = _xMark.color;
            color.a = alpha;
            _xMark.color = color;
        }

        private void HideXInstant()
        {
            if (_xMark == null)
                return;

            if (_fadeRoutine != null)
            {
                StopCoroutine(_fadeRoutine);
                _fadeRoutine = null;
            }

            SetXAlpha(1f);
            _xMark.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------
        // Feedback de conflit : rouge temporaire + tremblement horizontal.
        // ------------------------------------------------------------------

        /// <summary>
        /// Déclenche le feedback de conflit : la case passe en rouge et tremble
        /// horizontalement pendant <see cref="ShakeDuration"/>, puis retrouve sa couleur
        /// au bout de <see cref="FlashDuration"/>.
        /// </summary>
        public void FlashConflict()
        {
            if (!isActiveAndEnabled || !Application.isPlaying)
                return;

            if (_feedbackRoutine != null)
                StopCoroutine(_feedbackRoutine);
            _feedbackRoutine = StartCoroutine(ConflictFeedbackRoutine());
        }

        private IEnumerator ConflictFeedbackRoutine()
        {
            _basePosition = transform.localPosition;
            _background.color = ConflictColor;

            float shakeElapsed = 0f;

            while (shakeElapsed < ShakeDuration)
            {
                float t = shakeElapsed / ShakeDuration;
                float decay = 1f - t;
                float offset = Mathf.Sin(shakeElapsed * ShakeFrequency) * _shakeAmplitude * decay;

                transform.localPosition = _basePosition + new Vector3(offset, 0f, 0f);
                shakeElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            transform.localPosition = _basePosition;

            float flashElapsed = ShakeDuration;

            while (flashElapsed < FlashDuration)
            {
                flashElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            _background.color = _baseColor;
            _feedbackRoutine = null;
        }

        // ------------------------------------------------------------------
        // Interactions tactiles (tap court / appui long).
        // ------------------------------------------------------------------

        public void OnPointerDown(PointerEventData eventData)
        {
            _pointerDown = true;
            _pressedAt = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_pointerDown)
                return;
            _pointerDown = false;

            bool isLongPress = Time.unscaledTime - _pressedAt >= _longPressDuration;
            if (isLongPress)
                OnLongPress?.Invoke();
            else
                OnTap?.Invoke();
        }
    }
}
