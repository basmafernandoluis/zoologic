using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zoologic
{
    /// <summary>
    /// Barre d'animaux du niveau : un jeton par zone, à glisser vers les cases
    /// pour poser un pion. Visuel uniquement (jetons infinis, aucune règle
    /// ajoutée : la zone impose toujours son animal).
    ///
    /// Construite dans le dock du bas ; <see cref="SetSprites"/> la repeuple à
    /// chaque niveau (les icônes sont mélangées par niveau).
    /// </summary>
    public sealed class AnimalTray : MonoBehaviour
    {
        private const float ChipSize = 112f;
        private const float ChipSpacing = 6f;

        // Largeur utile de la rangée (holder 800 − padding 2×12) : au-delà,
        // les jetons rétrécissent au lieu de déborder.
        private const float TrayUsableWidth = 776f;
        private const float ChipMinSize = 52f;

        private BoardDragController _drag;
        private Transform _row;
        private readonly List<Sprite> _allSprites = new List<Sprite>();

        public static AnimalTray Build(Transform parent, BoardDragController drag)
        {
            var go = new GameObject("AnimalTray", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(12f, -50f);
            rect.offsetMax = new Vector2(-12f, 50f);

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = ChipSpacing;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var tray = go.AddComponent<AnimalTray>();
            tray._drag = drag;
            tray._row = go.transform;
            return tray;
        }

        public void SetDrag(BoardDragController drag)
        {
            _drag = drag;
        }

        public void SetSprites(IReadOnlyList<Sprite> sprites)
        {
            _allSprites.Clear();
            if (sprites != null)
            {
                for (int i = 0; i < sprites.Count; i++)
                {
                    if (sprites[i] != null)
                        _allSprites.Add(sprites[i]);
                }
            }
            ShowFirst(_allSprites.Count, animate: true);
        }

        /// <summary>
        /// Inventaire : n'affiche que les `remaining` premiers jetons.
        /// Poser consomme, retirer au dock rend. Sans appel : infini (tutoriel).
        /// </summary>
        public void SetRemaining(int remaining)
        {
            ShowFirst(Mathf.Max(0, remaining));
        }

        private void ShowFirst(int count, bool animate = false)
        {
            if (_row == null)
                return;
            for (int i = _row.childCount - 1; i >= 0; i--)
                Destroy(_row.GetChild(i).gameObject);

            int n = Mathf.Min(count, _allSprites.Count);
            float size = ChipSize;
            if (n > 1)
                size = Mathf.Clamp((TrayUsableWidth - (n - 1) * ChipSpacing) / n, ChipMinSize, ChipSize);
            var chips = new System.Collections.Generic.List<TrayChip>(n);
            for (int i = 0; i < n; i++)
                chips.Add(CreateChip(_allSprites[i], size));
            // Pop en cascade uniquement à la (re)construction complète, pas à
            // chaque pose/retrait (SetRemaining) pour éviter le yoyo visuel.
            if (animate && Application.isPlaying && chips.Count > 0)
                StartCoroutine(PopCascadeRoutine(chips));
        }

        private System.Collections.IEnumerator PopCascadeRoutine(System.Collections.Generic.List<TrayChip> chips)
        {
            foreach (var chip in chips)
            {
                if (chip == null) continue;
                chip.transform.localScale = Vector3.zero;
                chip.IdleAnim = false;
            }
            foreach (var chip in chips)
            {
                if (chip != null)
                    StartCoroutine(PopOneRoutine(chip));
                yield return new WaitForSecondsRealtime(0.04f);
            }
        }

        private System.Collections.IEnumerator PopOneRoutine(TrayChip chip)
        {
            float duration = 0.22f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (chip == null) yield break;
                elapsed += Time.unscaledDeltaTime;
                float s = Easing.EaseOutBack(Mathf.Clamp01(elapsed / duration));
                chip.transform.localScale = new Vector3(s, s, s);
                yield return null;
            }
            if (chip == null) yield break;
            chip.transform.localScale = Vector3.one;
            chip.IdleAnim = true;
        }

        /// <summary>RectTransform d'un jeton (pour la main du tutoriel).</summary>
        public RectTransform GetChipRect(int index)
        {
            if (_row == null || index < 0 || index >= _row.childCount)
                return null;
            return _row.GetChild(index) as RectTransform;
        }

        public int ChipCount => _row != null ? _row.childCount : 0;

        private TrayChip CreateChip(Sprite sprite, float size)
        {
            var go = new GameObject("Chip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(_row, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(size, size);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = size;
            le.preferredHeight = size;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;

            // Pas de fond blanc : le médaillon détouré se suffit, la zone
            // transparente capte toujours le toucher (raycast actif).
            var bg = go.GetComponent<Image>();
            bg.sprite = GridView.SharedRoundedRect;
            bg.type = Image.Type.Simple;
            bg.color = new Color(1f, 1f, 1f, 0f);
            bg.raycastTarget = true;

            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGO.transform.SetParent(go.transform, false);
            var iconRect = (RectTransform)iconGO.transform;
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(4f, 4f);
            iconRect.offsetMax = new Vector2(-4f, -4f);
            var icon = iconGO.GetComponent<Image>();
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.color = SkinManager.SelectedTint;
            icon.raycastTarget = false;

            var chip = go.AddComponent<TrayChip>();
            chip.Drag = _drag;
            chip.Sprite = sprite;
            return chip;
        }

        /// <summary>Jeton : drag vers le plateau, punch au tap.</summary>
        private sealed class TrayChip : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
        {
            public BoardDragController Drag;
            public Sprite Sprite;

            /// <summary>Flottement idle autorisé (coupé pendant pop/punch).</summary>
            public bool IdleAnim = true;

            private Coroutine _punchRoutine;

            private void Update()
            {
                if (!IdleAnim) return;
                if (Drag != null && Drag.IsDragging) return;
                float phase = transform.GetSiblingIndex() * 0.9f;
                float t = Time.unscaledTime * 2f + phase;
                float s = 1f + Mathf.Sin(t) * 0.03f;
                transform.localScale = new Vector3(s, s, s);
                transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.8f) * 3f);
            }

            public void OnBeginDrag(PointerEventData eventData)
            {
                if (Drag != null)
                    Drag.BeginTrayDrag(Sprite, eventData.pointerId);
            }

            public void OnDrag(PointerEventData eventData)
            {
                if (Drag != null)
                    Drag.UpdateDrag(eventData.pointerId, eventData.position);
            }

            public void OnEndDrag(PointerEventData eventData)
            {
                if (Drag != null)
                    Drag.EndDrag(eventData.pointerId, eventData.position);
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                if (_punchRoutine != null)
                    StopCoroutine(_punchRoutine);
                _punchRoutine = StartCoroutine(PunchRoutine());
            }

            private System.Collections.IEnumerator PunchRoutine()
            {
                IdleAnim = false;
                float duration = 0.22f;
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float s = t < 0.5f
                        ? Mathf.Lerp(1f, 1.15f, t * 2f)
                        : Mathf.Lerp(1.15f, 1f, (t - 0.5f) * 2f);
                    transform.localScale = new Vector3(s, s, s);
                    yield return null;
                }
                transform.localScale = Vector3.one;
                transform.localRotation = Quaternion.identity;
                IdleAnim = true;
                _punchRoutine = null;
            }
        }
    }
}
