using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zoologic
{
    /// <summary>
    /// Barre d'animaux du niveau : inventaire de triplets d'humeurs. L'animal
    /// glissé EST l'animal posé (fini le faux-semblant zone→animal).
    /// Poser consomme (mode jeu), retirer rend ; mode infini pour le tutoriel.
    ///
    /// Construite dans le dock du bas ; <see cref="SetInventory"/> la repeuple.
    /// </summary>
    public sealed class AnimalTray : MonoBehaviour
    {
        // DesignDoctor #5 : slots 56dp (148px ref) + spacing 12dp, mini 48dp (132px).
        // 8x8 : dégradation gracieuse à 72px pour tenir dans 796px (scroll évité).
        private const float ChipSize = 148f;
        private const float ChipSpacing = 24f;

        // Largeur utile de la rangée (holder 820 − padding 2×12) : au-delà,
        // les jetons rétrécissent au lieu de déborder (8x8 → ~85px).
        private const float TrayUsableWidth = 796f;
        private const float ChipMinSize = 72f;
        private const float ChipMinComfort = 132f; // 48dp mini si N<=5

        private BoardDragController _drag;
        private Transform _row;
        private readonly List<AnimalIconSet.MoodSet> _base = new List<AnimalIconSet.MoodSet>();
        private readonly List<AnimalIconSet.MoodSet> _used = new List<AnimalIconSet.MoodSet>();
        private bool _consumeMode = true;

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

        /// <summary>
        /// (Re)peuple l'inventaire. consume=true (jeu) : poser consomme, retirer
        /// rend. consume=false (tutoriel) : jetons infinis.
        /// </summary>
        public void SetInventory(System.Collections.Generic.List<AnimalIconSet.MoodSet> sets, bool consume)
        {
            _base.Clear();
            _used.Clear();
            _consumeMode = consume;
            if (sets != null)
            {
                for (int i = 0; i < sets.Count; i++)
                {
                    if (sets[i].IsComplete)
                        _base.Add(sets[i]);
                }
            }
            Refresh(animate: true);
        }

        /// <summary>Inventaire complet (pour reset retry).</summary>
        public void ResetInventory()
        {
            _used.Clear();
            Refresh(animate: false);
        }

        /// <summary>Consomme le set (pose). Retourne false si déjà consommé.</summary>
        public bool Consume(AnimalIconSet.MoodSet set)
        {
            if (!_consumeMode)
                return true;
            if (!ContainsSet(_base, set) || ContainsSet(_used, set))
                return false;
            _used.Add(set);
            Refresh(animate: false);
            return true;
        }

        /// <summary>Rend un set consommé (retrait, gomme).</summary>
        public void Return(AnimalIconSet.MoodSet set)
        {
            if (!_consumeMode)
                return;
            for (int i = 0; i < _used.Count; i++)
            {
                if (SetsEqual(_used[i], set))
                {
                    _used.RemoveAt(i);
                    break;
                }
            }
            Refresh(animate: false);
        }

        private static bool SetsEqual(AnimalIconSet.MoodSet a, AnimalIconSet.MoodSet b)
        {
            return a.Neutral == b.Neutral && a.Happy == b.Happy && a.Sad == b.Sad;
        }

        private static bool ContainsSet(System.Collections.Generic.List<AnimalIconSet.MoodSet> list, AnimalIconSet.MoodSet set)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (SetsEqual(list[i], set))
                    return true;
            }
            return false;
        }

        private System.Collections.Generic.List<AnimalIconSet.MoodSet> VisibleSets()
        {
            var visible = new System.Collections.Generic.List<AnimalIconSet.MoodSet>();
            for (int i = 0; i < _base.Count; i++)
            {
                if (_consumeMode && ContainsSet(_used, _base[i]))
                    continue;
                visible.Add(_base[i]);
            }
            return visible;
        }

        private void Refresh(bool animate = false)
        {
            if (_row == null)
                return;
            for (int i = _row.childCount - 1; i >= 0; i--)
                Destroy(_row.GetChild(i).gameObject);

            var visible = VisibleSets();
            int n = visible.Count;
            float size = ChipSize;
            if (n > 1)
            {
                float minForCount = n <= 5 ? ChipMinComfort : ChipMinSize;
                size = Mathf.Clamp((TrayUsableWidth - (n - 1) * ChipSpacing) / n, minForCount, ChipSize);
                // 8x8 : si min confort déborde, retombe sur min absolu (évite overflow).
                if (size * n + (n - 1) * ChipSpacing > TrayUsableWidth + 1f)
                    size = Mathf.Clamp((TrayUsableWidth - (n - 1) * ChipSpacing) / n, ChipMinSize, ChipSize);
            }
            var chips = new System.Collections.Generic.List<TrayChip>(n);
            for (int i = 0; i < n; i++)
                chips.Add(CreateChip(visible[i], size));
            // Pop en cascade uniquement à la (re)construction complète, pas à
            // chaque pose/retrait pour éviter le yoyo visuel.
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

        private TrayChip CreateChip(AnimalIconSet.MoodSet set, float size)
        {
            Sprite sprite = set.Neutral;
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
            chip.Set = set;
            return chip;
        }

        /// <summary>Jeton : drag vers le plateau, punch + sélection 1.15x au tap.</summary>
        private sealed class TrayChip : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
        {
            public BoardDragController Drag;
            public AnimalIconSet.MoodSet Set;

            /// <summary>Flottement idle autorisé (coupé pendant pop/punch).</summary>
            public bool IdleAnim = true;

            private Coroutine _punchRoutine;
            private Image _selectRing;
            private static TrayChip _selected;

            private void Update()
            {
                if (!IdleAnim) return;
                if (Drag != null && Drag.IsDragging) return;
                // Sélection persistante 1.15x (DesignDoctor #5) : pas de yoyo idle.
                float baseScale = _selected == this ? 1.15f : 1f;
                float phase = transform.GetSiblingIndex() * 0.9f;
                float t = Time.unscaledTime * 2f + phase;
                float s = baseScale + Mathf.Sin(t) * 0.03f;
                transform.localScale = new Vector3(s, s, s);
                if (_selected != this)
                    transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.8f) * 3f);
            }

            public void OnBeginDrag(PointerEventData eventData)
            {
                if (Drag != null && Set.IsComplete)
                    Drag.BeginTrayDrag(Set, eventData.pointerId);
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
                SetSelected(this);
                if (_punchRoutine != null)
                    StopCoroutine(_punchRoutine);
                _punchRoutine = StartCoroutine(PunchRoutine());
            }

            private static void SetSelected(TrayChip chip)
            {
                if (_selected != null && _selected != chip)
                    _selected.SetRing(false);
                _selected = chip;
                chip.SetRing(true);
            }

            private void SetRing(bool on)
            {
                if (on && _selectRing == null)
                {
                    var ringGO = new GameObject("SelectRing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    ringGO.transform.SetParent(transform, false);
                    var ringRect = (RectTransform)ringGO.transform;
                    ringRect.anchorMin = new Vector2(0.5f, 0.5f);
                    ringRect.anchorMax = new Vector2(0.5f, 0.5f);
                    ringRect.pivot = new Vector2(0.5f, 0.5f);
                    var sd = ((RectTransform)transform).sizeDelta;
                    ringRect.sizeDelta = new Vector2(sd.x * 1.08f, sd.y * 1.08f);
                    ringRect.anchoredPosition = Vector2.zero;
                    ringRect.SetAsFirstSibling();
                    _selectRing = ringGO.GetComponent<Image>();
                    _selectRing.sprite = GridView.SharedRing;
                    _selectRing.color = new Color(0.30f, 0.20f, 0.18f, 1f); // #4E342E 3dp
                    _selectRing.raycastTarget = false;
                }
                if (_selectRing != null)
                    _selectRing.gameObject.SetActive(on);
                if (!on && _selected == this)
                    _selected = null;
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
