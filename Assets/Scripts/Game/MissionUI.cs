using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zoologic.Localization;

namespace Zoologic
{
    public static class MissionUI
    {
        private static GameObject _root;
        private static TMP_FontAsset _fontTitle;
        private static TMP_FontAsset _fontBody;
        public static bool IsOpen => _root != null;
        public static void Close() { if (_root != null) { Object.Destroy(_root); _root = null; } }

        public static void Show(Canvas canvas)
        {
            if (canvas == null) return;
            try
            {
                if (_root != null) Object.Destroy(_root);
                _fontTitle = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Bold SDF");
                _fontBody = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Regular SDF");
                if (_fontTitle == null) _fontTitle = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Regular SDF");
                if (_fontTitle == null) _fontTitle = TMP_Settings.defaultFontAsset;
                if (_fontBody == null) _fontBody = _fontTitle;

            _root = new GameObject("MissionsRoot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _root.transform.SetParent(canvas.transform, false);
            var rr = _root.GetComponent<RectTransform>();
            rr.anchorMin = Vector2.zero; rr.anchorMax = Vector2.one;
            rr.offsetMin = Vector2.zero; rr.offsetMax = Vector2.zero;
            var ri = _root.GetComponent<Image>();
            ri.color = new Color(0.24f, 0.16f, 0.10f, 0.62f);
            ri.raycastTarget = true;

            var card = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            card.transform.SetParent(_root.transform, false);
            var cr = card.GetComponent<RectTransform>();
            cr.anchorMin = new Vector2(0.5f, 0.5f); cr.anchorMax = new Vector2(0.5f, 0.5f);
            cr.pivot = new Vector2(0.5f, 0.5f);
            cr.sizeDelta = new Vector2(760f, 0f);
            var ci = card.GetComponent<Image>();
            ci.sprite = CreateRounded(256, 0.18f);
            ci.type = Image.Type.Simple;
            ci.color = new Color(1f, 0.98f, 0.94f, 1f);
            var cardShadow = card.AddComponent<Shadow>();
            cardShadow.effectColor = new Color(0.18f, 0.11f, 0.06f, 0.30f);
            cardShadow.effectDistance = new Vector2(0f, -10f);

            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(32, 32, 56, 32);
            vlg.spacing = 16f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var csf = card.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var titleGO = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titleGO.transform.SetParent(card.transform, false);
            var title = titleGO.GetComponent<TextMeshProUGUI>();
            title.font = _fontTitle; title.text = LocalizationManager.Get("missions.title"); title.fontSize = 36; title.fontStyle = FontStyles.Bold;
            LocalizationManager.ApplyTo(title);
            title.color = new Color(0.29f, 0.18f, 0.10f); title.alignment = TextAlignmentOptions.Center;
            title.outlineWidth = 0.18f; title.outlineColor = new Color(1f, 0.98f, 0.92f, 0.85f);
            var tle = titleGO.AddComponent<LayoutElement>(); tle.preferredHeight = 52f; tle.flexibleWidth = 1f;

            var list = MissionManager.GetMissions();
            if (list == null) list = new System.Collections.Generic.List<MissionData>();
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (m == null) continue;
                var row = new GameObject($"Row{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                row.transform.SetParent(card.transform, false);
                var rowRect = row.GetComponent<RectTransform>();
                var rowLE = row.AddComponent<LayoutElement>(); rowLE.preferredHeight = 142f; rowLE.flexibleWidth = 1f;
                var rowImg = row.GetComponent<Image>();
                rowImg.sprite = CreateRounded(128, 0.22f);
                rowImg.type = Image.Type.Simple;
                rowImg.color = m.IsCompleted && !m.claimed ? new Color(1f, 0.96f, 0.86f, 1f) : new Color(1f, 0.97f, 0.92f, 1f);
                var rowShadow = row.AddComponent<Shadow>();
                rowShadow.effectColor = new Color(0.18f, 0.11f, 0.06f, 0.16f);
                rowShadow.effectDistance = new Vector2(0f, -4f);
                if (m.IsCompleted && !m.claimed)
                {
                    var ol = row.AddComponent<Outline>();
                    ol.effectColor = new Color(0.95f, 0.70f, 0.20f, 1f);
                    ol.effectDistance = new Vector2(2f, -2f);
                }

                var hlg = row.AddComponent<HorizontalLayoutGroup>();
                hlg.padding = new RectOffset(20, 20, 16, 16);
                hlg.spacing = 16f;
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = true;
                hlg.childControlHeight = true;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;

                var left = new GameObject("Left", typeof(RectTransform));
                left.transform.SetParent(row.transform, false);
                var leftLE = left.AddComponent<LayoutElement>(); leftLE.flexibleWidth = 1f; leftLE.preferredHeight = 110f; leftLE.minWidth = 0f;
                var leftVLG = left.AddComponent<VerticalLayoutGroup>();
                leftVLG.spacing = 8f;
                leftVLG.childControlWidth = true;
                leftVLG.childControlHeight = true;
                leftVLG.childForceExpandWidth = true;
                leftVLG.childForceExpandHeight = false;
                leftVLG.childAlignment = TextAnchor.MiddleLeft;
                leftVLG.padding = new RectOffset(0, 0, 0, 0);

                var labelGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                labelGO.transform.SetParent(left.transform, false);
                var label = labelGO.GetComponent<TextMeshProUGUI>();
                label.font = _fontTitle; label.text = m.Label; label.fontSize = 24; label.fontStyle = FontStyles.Bold;
                label.color = new Color(0.22f, 0.13f, 0.07f); label.alignment = TextAlignmentOptions.MidlineLeft;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.raycastTarget = false;
                var labLE = labelGO.AddComponent<LayoutElement>(); labLE.preferredHeight = 32f; labLE.flexibleWidth = 1f; labLE.minWidth = 0f;

                var barBG = new GameObject("BarBG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                barBG.transform.SetParent(left.transform, false);
                var barBGLE = barBG.AddComponent<LayoutElement>(); barBGLE.preferredHeight = 28f; barBGLE.flexibleWidth = 1f;
                var barBGImg = barBG.GetComponent<Image>();
                barBGImg.sprite = CreateRounded(64, 0.45f);
                barBGImg.type = Image.Type.Sliced;
                barBGImg.color = new Color(0.32f, 0.24f, 0.16f, 0.22f);
                var barInnerShadow = barBG.AddComponent<Shadow>();
                barInnerShadow.effectColor = new Color(0f, 0f, 0f, 0.18f);
                barInnerShadow.effectDistance = new Vector2(0f, -2f);
                var barOutline = barBG.AddComponent<Outline>();
                barOutline.effectColor = new Color(0.18f, 0.11f, 0.06f, 0.14f);
                barOutline.effectDistance = new Vector2(1f, -1f);

                var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                fillGO.transform.SetParent(barBG.transform, false);
                var fillRect = fillGO.GetComponent<RectTransform>();
                fillRect.anchorMin = new Vector2(0f, 0f); fillRect.anchorMax = new Vector2(0f, 1f);
                fillRect.pivot = new Vector2(0f, 0.5f);
                fillRect.offsetMin = new Vector2(3f, 3f); fillRect.offsetMax = new Vector2(-3f, -3f);
                float frac = Mathf.Clamp01((float)m.progress / Mathf.Max(1, m.target));
                fillRect.anchorMax = new Vector2(frac, 1f);
                var fillImg = fillGO.GetComponent<Image>();
                fillImg.sprite = CreateRounded(64, 0.45f);
                fillImg.type = Image.Type.Sliced;
                bool completed = m.IsCompleted;
                fillImg.color = completed ? new Color(0.16f, 0.78f, 0.35f, 1f) : new Color(1f, 0.62f, 0.06f, 1f);
                if (!completed)
                {
                    var gloss = new GameObject("Gloss", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    gloss.transform.SetParent(fillGO.transform, false);
                    var gRect = gloss.GetComponent<RectTransform>();
                    gRect.anchorMin = new Vector2(0f, 0.5f); gRect.anchorMax = new Vector2(1f, 0.78f);
                    gRect.offsetMin = new Vector2(4f, 0f); gRect.offsetMax = new Vector2(-4f, 0f);
                    var gImg = gloss.GetComponent<Image>();
                    gImg.sprite = CreateRounded(64, 0.45f);
                    gImg.type = Image.Type.Sliced;
                    gImg.color = new Color(1f, 1f, 1f, 0.22f);
                    gImg.raycastTarget = false;
                }

                var progGO = new GameObject("Prog", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                progGO.transform.SetParent(barBG.transform, false);
                var progRect = progGO.GetComponent<RectTransform>();
                progRect.anchorMin = Vector2.zero; progRect.anchorMax = Vector2.one;
                progRect.offsetMin = Vector2.zero; progRect.offsetMax = Vector2.zero;
                var prog = progGO.GetComponent<TextMeshProUGUI>();
                prog.font = _fontTitle; prog.text = $"{m.progress}/{m.target}"; prog.fontSize = 20;
                prog.fontStyle = FontStyles.Bold;
                prog.color = Color.white; prog.outlineWidth = 0.22f; prog.outlineColor = new Color(0f, 0f, 0f, 0.55f);
                prog.alignment = TextAlignmentOptions.Center;
                prog.raycastTarget = false;

                int idx = i;
                bool canClaim = m.IsCompleted && !m.claimed;
                bool isClaimed = m.claimed;

                var btnGO = new GameObject("Btn", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                btnGO.transform.SetParent(row.transform, false);
                var btnRect = btnGO.GetComponent<RectTransform>();
                btnRect.sizeDelta = new Vector2(160f, 54f);
                var btnImg = btnGO.GetComponent<Image>();
                btnImg.type = Image.Type.Sliced;

                if (isClaimed)
                {
                    btnImg.sprite = CreateRounded(128, 0.35f);
                    btnImg.color = new Color(0.78f, 0.78f, 0.80f, 1f);
                }
                else if (canClaim)
                {
                    btnImg.sprite = CreateRounded(128, 0.35f);
                    btnImg.color = new Color(0.18f, 0.80f, 0.38f, 1f);
                    var btnOl = btnGO.AddComponent<Outline>();
                    btnOl.effectColor = new Color(1f, 1f, 1f, 0.55f);
                    btnOl.effectDistance = new Vector2(1.5f, -1.5f);
                }
                else
                {
                    btnImg.sprite = CreateRounded(128, 0.35f);
                    btnImg.color = new Color(0.62f, 0.42f, 0.18f, 1f);
                }
                var btnShadow = btnGO.AddComponent<Shadow>();
                btnShadow.effectColor = new Color(0.18f, 0.11f, 0.06f, canClaim ? 0.32f : 0.20f);
                btnShadow.effectDistance = new Vector2(0f, -4f);
                var btn = btnGO.GetComponent<Button>();
                btn.targetGraphic = btnImg;
                btn.interactable = canClaim && !isClaimed;
                if (canClaim) PulseEffect.Start(btnGO.transform);

                var bContent = new GameObject("Content", typeof(RectTransform));
                bContent.transform.SetParent(btnGO.transform, false);
                var bcRect = bContent.GetComponent<RectTransform>();
                bcRect.anchorMin = Vector2.zero; bcRect.anchorMax = Vector2.one;
                bcRect.offsetMin = new Vector2(8f, 4f); bcRect.offsetMax = new Vector2(-8f, -4f);
                var bHlg = bContent.AddComponent<HorizontalLayoutGroup>();
                bHlg.spacing = 6f; bHlg.childAlignment = TextAnchor.MiddleCenter;
                bHlg.childForceExpandWidth = false; bHlg.childForceExpandHeight = false;

                if (!canClaim || isClaimed)
                {
                    if (!isClaimed)
                    {
                        var coinGO = new GameObject("Coin", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                        coinGO.transform.SetParent(bContent.transform, false);
                        var coinRect = coinGO.GetComponent<RectTransform>();
                        coinRect.sizeDelta = new Vector2(22f, 22f);
                        var coinLE = coinGO.AddComponent<LayoutElement>(); coinLE.preferredWidth = 22f; coinLE.preferredHeight = 22f;
                        var coinImg = coinGO.GetComponent<Image>();
                        var coinSprite = Resources.Load<Sprite>("UI/coin");
                        if (coinSprite != null) coinImg.sprite = coinSprite;
                        coinImg.preserveAspect = true;
                        coinImg.raycastTarget = false;
                    }
                }

                var btxtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                btxtGO.transform.SetParent(bContent.transform, false);
                var btxt = btxtGO.GetComponent<TextMeshProUGUI>();
                btxt.font = _fontTitle;
                if (isClaimed) btxt.text = "✓";
                else if (canClaim) btxt.text = LocalizationManager.Get("missions.claim");
                else btxt.text = $"+{m.reward}";
                btxt.fontSize = canClaim ? 18 : 19; btxt.fontStyle = FontStyles.Bold;
                btxt.color = Color.white;
                btxt.outlineWidth = 0.15f; btxt.outlineColor = new Color(0f, 0f, 0f, 0.35f);
                btxt.alignment = TextAlignmentOptions.Center;
                btxt.raycastTarget = false;
                var ble = btnGO.AddComponent<LayoutElement>(); ble.preferredWidth = canClaim ? 148f : 128f; ble.preferredHeight = 54f; ble.flexibleWidth = 0f;

                if (canClaim)
                {
                    btn.onClick.AddListener(() =>
                    {
                        if (m.claimed) return;
                        btn.interactable = false;
                        int reward = m.reward;
                        var runner = SFXManager.Instance;
                        if (runner != null && btnGO != null)
                            Punch.Scale(runner, (RectTransform)btnGO.transform, 1.18f, 0.22f);
                        Haptics.VibrateLight();
                        int oldCoins = CurrencyManager.GetCoins();
                        if (!MissionManager.TryClaim(idx))
                        {
                            btn.interactable = true;
                            return;
                        }
                        if (SFXManager.Instance != null) SFXManager.Instance.PlayUnlock();
                        var c = Object.FindFirstObjectByType<Canvas>();
                        if (c != null)
                            ClaimJuice.Play(c, btnGO.transform, reward, oldCoins, () =>
                            {
                                if (_root != null) { Object.Destroy(_root); _root = null; }
                                var c2 = Object.FindFirstObjectByType<Canvas>();
                                if (c2 != null) Show(c2);
                            });
                        else
                        {
                            if (_root != null) { Object.Destroy(_root); _root = null; }
                            var c2 = Object.FindFirstObjectByType<Canvas>();
                            if (c2 != null) Show(c2);
                        }
                    });
                }
            }

            var closeBtn = new GameObject("Close", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            closeBtn.transform.SetParent(card.transform, false);
            var closeRect = closeBtn.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f); closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f); closeRect.sizeDelta = new Vector2(52f, 52f);
            closeRect.anchoredPosition = new Vector2(-12f, -12f);
            var closeLayout = closeBtn.AddComponent<LayoutElement>();
            closeLayout.ignoreLayout = true;
            var closeImg = closeBtn.GetComponent<Image>();
            closeImg.sprite = CreateRounded(64, 0.5f);
            closeImg.type = Image.Type.Simple;
            closeImg.color = new Color(0.84f, 0.28f, 0.26f, 1f);
            var closeShadow = closeBtn.AddComponent<Shadow>();
            closeShadow.effectColor = new Color(0f, 0f, 0f, 0.25f);
            closeShadow.effectDistance = new Vector2(0f, -3f);
            var closeOl = closeBtn.AddComponent<Outline>();
            closeOl.effectColor = new Color(1f, 1f, 1f, 0.85f);
            closeOl.effectDistance = new Vector2(2f, -2f);
            var closeBtnComp = closeBtn.GetComponent<Button>();
            closeBtnComp.targetGraphic = closeImg;
            closeBtnComp.onClick.AddListener(() => { Object.Destroy(_root); _root = null; });
            var closeTxt = new GameObject("X", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            closeTxt.transform.SetParent(closeBtn.transform, false);
            var closeTxtRect = closeTxt.GetComponent<RectTransform>();
            closeTxtRect.anchorMin = Vector2.zero; closeTxtRect.anchorMax = Vector2.one;
            closeTxtRect.offsetMin = Vector2.zero; closeTxtRect.offsetMax = new Vector2(0f, 2f);
            var closeT = closeTxt.GetComponent<TextMeshProUGUI>();
            closeT.font = _fontTitle ?? TMP_Settings.defaultFontAsset; closeT.text = "×"; closeT.fontSize = 38;
            closeT.fontStyle = FontStyles.Bold;
            closeT.color = Color.white; closeT.alignment = TextAlignmentOptions.Center;
            closeT.outlineWidth = 0.10f; closeT.outlineColor = new Color(0f, 0f, 0f, 0.25f);
            _root.AddComponent<ClickOutsideCloser>().Init(_root);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[MissionUI] Show failed: " + ex);
                if (_root != null) { Object.Destroy(_root); _root = null; }
            }
        }

        private class ClickOutsideCloser : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler
        {
            private GameObject _target;
            public void Init(GameObject root) => _target = root;
            public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
            {
                if (eventData.pointerCurrentRaycast.gameObject == _target)
                {
                    Destroy(_target);
                    MissionUI._root = null;
                }
            }
        }

        private static class PulseEffect
        {
            public static void Start(Transform t)
            {
                var go = t.gameObject;
                var comp = go.AddComponent<PulseMono>();
                comp.Init(t);
            }
            private class PulseMono : MonoBehaviour
            {
                private Transform _t; private Vector3 _base;
                public void Init(Transform t) { _t = t; _base = t.localScale; }
                void Update()
                {
                    if (_t == null) { Destroy(this); return; }
                    float s = 1f + Mathf.Sin(Time.unscaledTime * 4.5f) * 0.04f;
                    _t.localScale = _base * s;
                }
            }
        }

        private static class ClaimJuice
        {
            private const int CoinCount = 7;
            private const float FlyDuration = 0.75f;
            private const float Stagger = 0.07f;

            public static void Play(Canvas canvas, Transform from, int reward, int oldCoins, System.Action onDone)
            {
                var runner = SFXManager.Instance;
                if (runner == null || canvas == null || from == null)
                {
                    onDone?.Invoke();
                    return;
                }
                runner.StartCoroutine(FlyRoutine(canvas, from, reward, oldCoins, onDone));
            }

            private static IEnumerator FlyRoutine(Canvas canvas, Transform from, int reward, int oldCoins, System.Action onDone)
            {
                Vector3 startWorld = from.position;
                Vector3 targetWorld = FindCoinTargetWorld(canvas);
                var flyRoot = new GameObject("CoinFly", typeof(RectTransform));
                flyRoot.transform.SetParent(canvas.transform, false);
                var flyRect = (RectTransform)flyRoot.transform;
                flyRect.anchorMin = Vector2.zero; flyRect.anchorMax = Vector2.one;
                flyRect.offsetMin = Vector2.zero; flyRect.offsetMax = Vector2.zero;
                flyRoot.transform.SetAsLastSibling();

                Sprite coinSprite = Resources.Load<Sprite>("UI/coin");
                var coins = new System.Collections.Generic.List<RectTransform>();
                for (int i = 0; i < CoinCount; i++)
                {
                    var go = new GameObject($"FlyCoin{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    go.transform.SetParent(flyRoot.transform, false);
                    var rt = (RectTransform)go.transform;
                    rt.sizeDelta = new Vector2(38f, 38f);
                    rt.position = startWorld;
                    var img = go.GetComponent<Image>();
                    img.sprite = coinSprite != null ? coinSprite : CreateRounded(64, 0.5f);
                    img.color = coinSprite != null ? Color.white : new Color(1f, 0.78f, 0.15f, 1f);
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                    go.SetActive(false);
                    coins.Add(rt);
                }

                var runner = SFXManager.Instance;
                for (int i = 0; i < coins.Count; i++)
                {
                    int k = i;
                    runner.StartCoroutine(FlyOne(coins[k], startWorld, targetWorld, k * Stagger, FlyDuration));
                }

                float total = Stagger * (CoinCount - 1) + FlyDuration + 0.1f;
                yield return new WaitForSecondsRealtime(total);

                if (SFXManager.Instance != null) SFXManager.Instance.PlaySuccess();
                Haptics.VibrateLight();
                PunchTarget();
                yield return AnimateCounter(oldCoins, oldCoins + reward);
                ShowRewardToast(canvas, reward);

                if (flyRoot != null) Object.Destroy(flyRoot);
                yield return new WaitForSecondsRealtime(0.35f);
                onDone?.Invoke();
            }

            private static IEnumerator FlyOne(RectTransform rt, Vector3 from, Vector3 to, float delay, float dur)
            {
                if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
                if (rt == null) yield break;
                rt.gameObject.SetActive(true);
                rt.position = from;
                rt.localScale = Vector3.one;
                float el = 0f;
                Vector3 ctrl = (from + to) * 0.5f + new Vector3(60f, 220f, 0f);
                while (el < dur)
                {
                    float t = Mathf.Clamp01(el / dur);
                    float e = Easing.EaseInOutQuad(t);
                    Vector3 a = Vector3.Lerp(from, ctrl, e);
                    Vector3 b = Vector3.Lerp(ctrl, to, e);
                    Vector3 p = Vector3.Lerp(a, b, e);
                    rt.position = p;
                    float s = Mathf.Lerp(1.1f, 0.7f, e);
                    rt.localScale = new Vector3(s, s, s);
                    rt.Rotate(0f, 0f, 540f * Time.unscaledDeltaTime);
                    el += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (rt != null)
                {
                    rt.position = to;
                    rt.gameObject.SetActive(false);
                }
            }

            private static Vector3 FindCoinTargetWorld(Canvas canvas)
            {
                var go = GameObject.Find("CoinNombre") ?? GameObject.Find("EconomyPill") ?? GameObject.Find("CoinIcone");
                if (go != null) return go.transform.position;
                var cam = Camera.main;
                if (cam != null) return cam.ViewportToWorldPoint(new Vector3(0.5f, 0.95f, cam.nearClipPlane + 1f));
                return new Vector3(Screen.width * 0.5f, Screen.height - 120f, 0f);
            }

            private static void PunchTarget()
            {
                var runner = SFXManager.Instance;
                if (runner == null) return;
                var go = GameObject.Find("CoinNombre") ?? GameObject.Find("EconomyPill");
                if (go != null) Punch.Scale(runner, (RectTransform)go.transform, 1.22f, 0.28f);
                var hud = Object.FindFirstObjectByType<GameHUD>();
                if (hud != null) hud.RefreshCoins();
            }

            private static IEnumerator AnimateCounter(int from, int to)
            {
                var go = GameObject.Find("CoinNombre");
                TextMeshProUGUI txt = go != null ? go.GetComponent<TextMeshProUGUI>() : null;
                if (txt == null && go != null) txt = go.GetComponentInChildren<TextMeshProUGUI>();
                if (txt == null)
                {
                    var hud = Object.FindFirstObjectByType<GameHUD>();
                    if (hud != null) hud.RefreshCoins();
                    yield break;
                }
                float dur = 0.5f; float el = 0f;
                while (el < dur)
                {
                    float t = Mathf.Clamp01(el / dur);
                    int v = Mathf.RoundToInt(Mathf.Lerp(from, to, Easing.EaseOutCubic(t)));
                    txt.text = v.ToString();
                    el += Time.unscaledDeltaTime;
                    yield return null;
                }
                txt.text = to.ToString();
                var hud2 = Object.FindFirstObjectByType<GameHUD>();
                if (hud2 != null) hud2.RefreshCoins();
            }

            private static void ShowRewardToast(Canvas canvas, int reward)
            {
                if (canvas == null) return;
                var old = GameObject.Find("ClaimToast");
                if (old != null) Object.Destroy(old);
                var t = new GameObject("ClaimToast", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                t.transform.SetParent(canvas.transform, false);
                var tr = (RectTransform)t.transform;
                tr.anchorMin = new Vector2(0.5f, 0.5f); tr.anchorMax = new Vector2(0.5f, 0.5f);
                tr.sizeDelta = new Vector2(480f, 60f); tr.anchoredPosition = new Vector2(0f, -260f);
                t.transform.SetAsLastSibling();
                var ti = t.GetComponent<Image>();
                ti.sprite = CreateRounded(128, 0.35f);
                ti.color = new Color(0.16f, 0.55f, 0.25f, 0.95f);
                ti.raycastTarget = false;
                var ttGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                ttGO.transform.SetParent(t.transform, false);
                var ttr = (RectTransform)ttGO.transform;
                ttr.anchorMin = Vector2.zero; ttr.anchorMax = Vector2.one;
                ttr.offsetMin = Vector2.zero; ttr.offsetMax = Vector2.zero;
                var tt = ttGO.GetComponent<TextMeshProUGUI>();
                tt.font = _fontTitle ?? TMP_Settings.defaultFontAsset;
                tt.text = LocalizationManager.Get("missions.reward_coins", reward);
                tt.fontSize = 26; tt.fontStyle = FontStyles.Bold;
                tt.color = Color.white; tt.alignment = TextAlignmentOptions.Center;
                tt.raycastTarget = false;
                var runner = SFXManager.Instance;
                if (runner != null) Punch.Scale(runner, tr, 1.12f, 0.25f);
                Object.Destroy(t, 1.4f);
            }
        }

        private static Sprite CreateRounded(int res, float ratio)
        {
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
            float half = (res - 1) * 0.5f; float rad = res * ratio; float inner = half - rad;
            for (int y = 0; y < res; y++) for (int x = 0; x < res; x++)
                {
                    float px = x - half; float py = y - half;
                    float qx = Mathf.Clamp(px, -inner, inner); float qy = Mathf.Clamp(py, -inner, inner);
                    float dx = px - qx; float dy = py - qy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(rad + 0.5f - d);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f));
        }
    }
}
