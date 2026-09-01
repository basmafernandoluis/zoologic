using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Zoologic
{
    public class MainMenuBuilder : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterCallback()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != "MainMenu") return;
            var go = new GameObject("MainMenuBuilder");
            go.AddComponent<MainMenuBuilder>();
        }

        private static readonly Color TitleOrange = new Color(0.95f, 0.55f, 0.15f);
        private static readonly Color TitleOutline = new Color(0.60f, 0.30f, 0.05f);
        private static readonly Color PlayGreen = new Color(0.30f, 0.69f, 0.31f);
        private static readonly Color PlayGreenHighlight = new Color(0.36f, 0.78f, 0.37f);
        private static readonly Color PlayGreenPressed = new Color(0.22f, 0.56f, 0.23f);
        private static readonly Color AccentBlue = new Color(0.22f, 0.50f, 0.85f);

        private TMP_FontAsset _fontTitle;
        private TMP_FontAsset _fontBody;
        private Image _owlImage;

        private void Start()
        {
            _fontTitle = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Bold SDF");
            _fontBody = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Regular SDF");

            var canvasGO = new GameObject("Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            if (EventSystem.current == null)
            {
                canvasGO.AddComponent<EventSystem>();
                canvasGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            EnsureMainCamera();

            BuildBackground(canvasGO.transform);
            BuildAnimalDecorations(canvasGO.transform);
            BuildTitle(canvasGO.transform);
            BuildOwlMascot(canvasGO.transform);
            BuildPlayButton(canvasGO.transform);
            BuildDailyButton(canvasGO.transform);
            BuildMissionsButton(canvasGO.transform);
            BuildSettingsButton(canvasGO.transform);
            BuildVersion(canvasGO.transform);

            if (DailyRewardManager.CanClaimToday())
                StartCoroutine(ShowDailyPopupDelayed());
        }

        private static void EnsureMainCamera()
        {
            Camera existing = FindFirstObjectByType<Camera>();
            if (existing != null)
            {
                if (FindFirstObjectByType<AudioListener>() == null)
                    existing.gameObject.AddComponent<AudioListener>();
                return;
            }

            var cameraGO = new GameObject("Main Camera");
            cameraGO.tag = "MainCamera";
            var camera = cameraGO.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundHelper.BgBottom;
            cameraGO.AddComponent<AudioListener>();
        }

        private void BuildBackground(Transform parent)
        {
            BackgroundHelper.ApplyBackground(parent);
        }

        private void BuildAnimalDecorations(Transform parent)
        {
            var decoGO = new GameObject("DecoGround");
            decoGO.transform.SetParent(parent, false);
            var decoRect = decoGO.AddComponent<RectTransform>();
            decoRect.anchorMin = new Vector2(0f, 0f);
            decoRect.anchorMax = new Vector2(1f, 0f);
            decoRect.pivot = new Vector2(0.5f, 0f);
            decoRect.sizeDelta = new Vector2(0f, 420f);
            decoRect.anchoredPosition = Vector2.zero;
            var decoImg = decoGO.AddComponent<Image>();
            decoImg.sprite = CreateDecoGroundSprite();
            decoImg.type = Image.Type.Simple;
            decoImg.color = new Color(1f, 1f, 1f, 0.55f);
            decoImg.raycastTarget = false;
        }

        private void BuildTitle(Transform parent)
        {
            var go = new GameObject("Title");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.76f);
            rect.anchorMax = new Vector2(0.5f, 0.76f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(860f, 170f);
            rect.anchoredPosition = Vector2.zero;

            var pawGO = new GameObject("Paws");
            pawGO.transform.SetParent(go.transform, false);
            var pawRect = pawGO.AddComponent<RectTransform>();
            pawRect.anchorMin = new Vector2(0.5f, 1f);
            pawRect.anchorMax = new Vector2(0.5f, 1f);
            pawRect.pivot = new Vector2(0.5f, 1f);
            pawRect.sizeDelta = new Vector2(520f, 36f);
            pawRect.anchoredPosition = new Vector2(0f, 18f);
            for (int i = 0; i < 3; i++)
            {
                var paw = new GameObject("Paw" + i);
                paw.transform.SetParent(pawGO.transform, false);
                var pr = paw.AddComponent<RectTransform>();
                pr.anchorMin = new Vector2(0f, 0.5f); pr.anchorMax = new Vector2(0f, 0.5f);
                pr.pivot = new Vector2(0.5f, 0.5f);
                pr.sizeDelta = new Vector2(28f, 28f);
                pr.anchoredPosition = new Vector2(70f + i * 185f, 0f);
                var pi = paw.AddComponent<Image>();
                pi.sprite = CreatePawSprite();
                pi.color = new Color(0.95f, 0.72f, 0.42f, 0.35f);
                pi.raycastTarget = false;
            }

            var txt = go.AddComponent<TextMeshProUGUI>();
            txt.font = _fontTitle;
            txt.text = "ZOO LOGIC";
            txt.fontSize = 84;
            txt.fontStyle = FontStyles.Bold;
            txt.color = TitleOrange;
            txt.alignment = TextAlignmentOptions.Center;
            txt.raycastTarget = false;
            txt.extraPadding = true;
            txt.margin = new Vector4(8f, 8f, 8f, 8f);

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.38f, 0.20f, 0.06f, 0.22f);
            shadow.effectDistance = new Vector2(0f, -6f);

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.99f, 0.95f, 1f);
            outline.effectDistance = new Vector2(4f, -4f);

            var innerGO = new GameObject("InnerShadow");
            innerGO.transform.SetParent(go.transform, false);
            var ir = innerGO.AddComponent<RectTransform>();
            ir.anchorMin = Vector2.zero; ir.anchorMax = Vector2.one;
            ir.offsetMin = Vector2.zero; ir.offsetMax = Vector2.zero;
        }

        private void BuildPlayButton(Transform parent)
        {
            var go = new GameObject("PlayButton");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.30f);
            rect.anchorMax = new Vector2(0.5f, 0.30f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(460f, 110f);
            rect.anchoredPosition = Vector2.zero;

            var img = go.AddComponent<Image>();
            Sprite wood = Resources.Load<Sprite>("UI/Cozy/btn_wood");
            img.sprite = wood != null ? wood : KenneyUI.Button("Green") ?? CreerSpriteArrondi(128, 0.35f);
            img.type = Image.Type.Sliced;
            img.color = Color.white;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.96f, 0.88f);
            colors.pressedColor = new Color(0.92f, 0.86f, 0.78f);
            btn.colors = colors;

            btn.onClick.AddListener(() =>
            {
                SFXManager.Instance.PlayMenuOpen();
                StartCoroutine(PlayButtonPunch(go.transform));
                SceneManager.LoadScene("LevelMap");
            });

            var txtGO = new GameObject("Text");
            txtGO.transform.SetParent(go.transform, false);
            var txtRect = txtGO.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;
            var txt = txtGO.AddComponent<TextMeshProUGUI>();
            txt.font = _fontTitle;
            txt.text = "JOUER";
            txt.fontSize = 54;
            txt.fontStyle = FontStyles.Bold;
            txt.color = new Color(0.32f, 0.20f, 0.12f);
            txt.alignment = TextAlignmentOptions.Center;
            txt.raycastTarget = false;
            txt.outlineWidth = 0.22f;
            txt.outlineColor = new Color(1f, 1f, 1f, 0.85f);

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.38f, 0.24f, 0.14f, 0.18f);
            shadow.effectDistance = new Vector2(0f, -6f);

            go.AddComponent<PlayButtonFloat>();
        }

        private IEnumerator PlayButtonPunch(Transform t)
        {
            Vector3 baseScale = t.localScale;
            float d = 0.12f; float e = 0f;
            while (e < d) { e += Time.unscaledDeltaTime; float s = 1f + Mathf.Sin(e / d * Mathf.PI) * 0.06f; t.localScale = baseScale * s; yield return null; }
            t.localScale = baseScale;
        }

        private class PlayButtonFloat : MonoBehaviour
        {
            private void Update() { float s = 1f + Mathf.Sin(Time.unscaledTime * 2f) * 0.015f; transform.localScale = new Vector3(s, s, s); }
        }

        private void BuildSettingsButton(Transform parent)
        {
            var go = new GameObject("SettingsButton");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(80f, 80f);
            rect.anchoredPosition = new Vector2(-20f, -20f);

            var img = go.AddComponent<Image>();
            img.sprite = GetSettingsSprite();
            img.color = new Color(0.85f, 0.85f, 0.90f, 1f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor = new Color(0.85f, 0.85f, 0.90f, 1f);
            colors.highlightedColor = new Color(0.70f, 0.72f, 0.78f, 1f);
            colors.pressedColor = new Color(0.55f, 0.58f, 0.65f, 1f);
            btn.colors = colors;

            btn.onClick.AddListener(() =>
            {
                SFXManager.Instance.PlayMenuOpen();
                SettingsPanel.Open();
            });
        }

        private void BuildDailyButton(Transform parent)
        {
            bool canClaim = DailyRewardManager.CanClaimToday();
            var go = new GameObject("DailyButton");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(210f, 66f);
            rect.anchoredPosition = new Vector2(18f, -18f);

            var img = go.AddComponent<Image>();
            img.sprite = KenneyUI.Button(canClaim ? "Yellow" : "Grey") ?? CreerSpriteArrondi(128, 0.35f);
            img.type = Image.Type.Simple;
            img.color = Color.white;

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(14, 14, 0, 0);
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childControlWidth = false;

            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(go.transform, false);
            var iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(28f, 28f);
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = Resources.Load<Sprite>("UI/Icons/gift");
            iconImg.preserveAspect = true;
            iconImg.color = canClaim ? new Color(0.32f, 0.20f, 0.12f) : new Color(0.42f, 0.38f, 0.34f);
            iconImg.raycastTarget = false;
            var iconLE = iconGO.AddComponent<LayoutElement>(); iconLE.preferredWidth = 28f; iconLE.preferredHeight = 28f;

            var txtGO = new GameObject("Text");
            txtGO.transform.SetParent(go.transform, false);
            var txt = txtGO.AddComponent<TextMeshProUGUI>();
            txt.font = _fontTitle;
            txt.text = "Cadeau";
            txt.fontSize = 21;
            txt.fontStyle = FontStyles.Bold;
            txt.color = canClaim ? new Color(0.32f, 0.20f, 0.12f) : new Color(0.45f, 0.40f, 0.36f);
            txt.alignment = TextAlignmentOptions.Center;
            var txtLE = txtGO.AddComponent<LayoutElement>(); txtLE.flexibleWidth = 1f;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() =>
            {
                SFXManager.Instance.PlayMenuOpen();
                var canvas = FindFirstObjectByType<Canvas>();
                if (canvas != null) StartCoroutine(OpenDailyWithPop(canvas));
            });

            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0.38f, 0.24f, 0.14f, 0.14f);
            sh.effectDistance = new Vector2(0f, -4f);

            if (canClaim) go.AddComponent<DailyPulse>();
        }

        private IEnumerator OpenDailyWithPop(Canvas canvas)
        {
            var t = canvas.transform; float d = 0.12f; float e = 0f;
            var cg = canvas.GetComponent<CanvasGroup>(); if (cg == null) cg = canvas.gameObject.AddComponent<CanvasGroup>();
            while (e < d) { e += Time.unscaledDeltaTime; float s = 1f + Easing.EaseOutBack(Mathf.Clamp01(e / d)) * 0.02f; canvas.transform.localScale = new Vector3(s, s, s); yield return null; }
            canvas.transform.localScale = Vector3.one;
            DailyRewardUI.Show(canvas);
        }

        private void BuildMissionsButton(Transform parent)
        {
            int done = MissionManager.GetCompletedCount();
            bool hasClaim = done > 0;
            var go = new GameObject("MissionsButton");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(210f, 66f);
            rect.anchoredPosition = new Vector2(236f, -18f);
            var img = go.AddComponent<Image>();
            img.sprite = KenneyUI.Button(hasClaim ? "Yellow" : "Blue") ?? CreerSpriteArrondi(128, 0.35f);
            img.type = Image.Type.Simple;
            img.color = Color.white;

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(14, 14, 0, 0);
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleCenter;

            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(go.transform, false);
            var iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(28f, 28f);
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = Resources.Load<Sprite>("UI/Icons/trophy");
            iconImg.preserveAspect = true;
            iconImg.color = hasClaim ? new Color(0.32f, 0.20f, 0.12f) : Color.white;
            iconImg.raycastTarget = false;
            var iconLE = iconGO.AddComponent<LayoutElement>(); iconLE.preferredWidth = 28f; iconLE.preferredHeight = 28f;

            var txtGO = new GameObject("Text");
            txtGO.transform.SetParent(go.transform, false);
            var txt = txtGO.AddComponent<TextMeshProUGUI>();
            txt.font = _fontTitle;
            txt.text = "Missions";
            txt.fontSize = 20;
            txt.fontStyle = FontStyles.Bold;
            txt.color = hasClaim ? new Color(0.32f, 0.20f, 0.12f) : Color.white;
            txt.alignment = TextAlignmentOptions.Center;
            var txtLE = txtGO.AddComponent<LayoutElement>(); txtLE.flexibleWidth = 1f;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() =>
            {
                SFXManager.Instance.PlayMenuOpen();
                var canvas = FindFirstObjectByType<Canvas>();
                if (canvas != null) StartCoroutine(OpenMissionsWithPop(canvas));
            });

            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0.38f, 0.24f, 0.14f, 0.14f);
            sh.effectDistance = new Vector2(0f, -4f);

            if (done > 0)
            {
                var badgeGO = new GameObject("Badge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                badgeGO.transform.SetParent(go.transform, false);
                var badgeRect = badgeGO.GetComponent<RectTransform>();
                badgeRect.anchorMin = new Vector2(1f, 1f); badgeRect.anchorMax = new Vector2(1f, 1f);
                badgeRect.pivot = new Vector2(0.5f, 0.5f);
                badgeRect.sizeDelta = new Vector2(28f, 28f);
                badgeRect.anchoredPosition = new Vector2(10f, 10f);
                var badgeImg = badgeGO.GetComponent<Image>();
                badgeImg.sprite = CreerSpriteArrondi(64, 0.5f);
                badgeImg.color = new Color(0.92f, 0.36f, 0.42f);
                var badgeTxtGO = new GameObject("Count", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                badgeTxtGO.transform.SetParent(badgeGO.transform, false);
                var btr = badgeTxtGO.GetComponent<RectTransform>();
                btr.anchorMin = Vector2.zero; btr.anchorMax = Vector2.one;
                btr.offsetMin = Vector2.zero; btr.offsetMax = Vector2.zero;
                var btxt = badgeTxtGO.GetComponent<TextMeshProUGUI>();
                btxt.font = _fontTitle; btxt.text = done.ToString(); btxt.fontSize = 18;
                btxt.fontStyle = FontStyles.Bold; btxt.color = Color.white; btxt.alignment = TextAlignmentOptions.Center;
                badgeGO.AddComponent<BadgePulse>();
            }
        }

        private IEnumerator OpenMissionsWithPop(Canvas canvas)
        {
            float d = 0.12f; float e = 0f;
            while (e < d) { e += Time.unscaledDeltaTime; float s = 1f + Easing.EaseOutBack(Mathf.Clamp01(e / d)) * 0.02f; canvas.transform.localScale = new Vector3(s, s, s); yield return null; }
            canvas.transform.localScale = Vector3.one;
            MissionUI.Show(canvas);
        }

        private class BadgePulse : MonoBehaviour
        {
            private void Update() { float s = 1f + Mathf.Sin(Time.unscaledTime * 3f) * 0.08f; transform.localScale = new Vector3(s, s, s); }
        }

        private IEnumerator ShowDailyPopupDelayed()
        {
            yield return new WaitForSecondsRealtime(0.6f);
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null && DailyRewardManager.CanClaimToday())
                DailyRewardUI.Show(canvas);
        }

        private class DailyPulse : MonoBehaviour
        {
            private void Update()
            {
                float s = 1f + Mathf.Sin(Time.unscaledTime * 3f) * 0.06f;
                transform.localScale = new Vector3(s, s, 1f);
            }
        }

        // ------------------------------------------------------------------
        // Hibou mascotte — flottement idle + clignement périodique.
        // ------------------------------------------------------------------

        private void BuildOwlMascot(Transform parent)
        {
            Sprite owlSprite = Resources.Load<Sprite>("Art/Animals/owl");
            if (owlSprite == null) return;

            var podiumGO = new GameObject("Podium");
            podiumGO.transform.SetParent(parent, false);
            var podiumRect = podiumGO.AddComponent<RectTransform>();
            podiumRect.anchorMin = new Vector2(0.5f, 0.48f);
            podiumRect.anchorMax = new Vector2(0.5f, 0.48f);
            podiumRect.pivot = new Vector2(0.5f, 0.5f);
            podiumRect.sizeDelta = new Vector2(420f, 140f);
            podiumRect.anchoredPosition = new Vector2(0f, -30f);
            var podiumImg = podiumGO.AddComponent<Image>();
            Sprite wood = Resources.Load<Sprite>("UI/Cozy/wood_podium");
            podiumImg.sprite = wood ?? CreerSpriteArrondi(128, 0.22f);
            podiumImg.type = wood != null ? Image.Type.Sliced : Image.Type.Simple;
            podiumImg.color = Color.white;
            podiumImg.raycastTarget = false;
            var podiumSh = podiumGO.AddComponent<Shadow>();
            podiumSh.effectColor = new Color(0f, 0f, 0f, 0.14f);
            podiumSh.effectDistance = new Vector2(0f, -8f);

            var shadowGO = new GameObject("OwlShadow");
            shadowGO.transform.SetParent(podiumGO.transform, false);
            var shadowRect = shadowGO.AddComponent<RectTransform>();
            shadowRect.anchorMin = new Vector2(0.5f, 1f); shadowRect.anchorMax = new Vector2(0.5f, 1f);
            shadowRect.pivot = new Vector2(0.5f, 0.5f);
            shadowRect.sizeDelta = new Vector2(160f, 26f);
            shadowRect.anchoredPosition = new Vector2(0f, 12f);
            var shadowImg = shadowGO.AddComponent<Image>();
            shadowImg.sprite = CreerSpriteArrondi(128, 0.5f);
            shadowImg.color = new Color(0f, 0f, 0f, 0.13f);
            shadowImg.raycastTarget = false;

            var go = new GameObject("OwlMascot");
            go.transform.SetParent(podiumGO.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(280f, 280f);
            rect.anchoredPosition = new Vector2(0f, 18f);

            _owlImage = go.AddComponent<Image>();
            _owlImage.sprite = owlSprite;
            _owlImage.preserveAspect = true;
            _owlImage.raycastTarget = false;

            StartCoroutine(OwlBobRoutine(rect));
            StartCoroutine(OwlBlinkRoutine(_owlImage));
        }

        private IEnumerator OwlBobRoutine(RectTransform rect)
        {
            Vector2 basePos = rect.anchoredPosition;
            const float amplitude = 12f;
            const float period = 2.5f;
            float time = 0f;

            while (true)
            {
                time += Time.unscaledDeltaTime;
                float y = Mathf.Sin(time * 2f * Mathf.PI / period) * amplitude;
                rect.anchoredPosition = basePos + new Vector2(0f, y);
                yield return null;
            }
        }

        private IEnumerator OwlBlinkRoutine(Image owlImage)
        {
            if (owlImage == null) yield break;
            Transform t = owlImage.transform;
            const float blinkScaleY = 0.85f;
            const float blinkDuration = 0.15f;

            while (true)
            {
                float wait = UnityEngine.Random.Range(3f, 5f);
                yield return new WaitForSecondsRealtime(wait);

                // Clignement : scale Y vers 0.85 puis retour à 1.0
                float elapsed = 0f;
                while (elapsed < blinkDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t2 = elapsed / blinkDuration;
                    float scaleY = t2 < 0.5f
                        ? Mathf.Lerp(1f, blinkScaleY, t2 * 2f)
                        : Mathf.Lerp(blinkScaleY, 1f, (t2 - 0.5f) * 2f);
                    t.localScale = new Vector3(1f, scaleY, 1f);
                    yield return null;
                }
                t.localScale = Vector3.one;
            }
        }

        private void BuildVersion(Transform parent)
        {
            var go = new GameObject("Version");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(300f, 40f);
            rect.anchoredPosition = new Vector2(0f, 18f);

            var txt = go.AddComponent<TextMeshProUGUI>();
            txt.font = _fontBody;
            txt.text = "v" + Application.version;
            txt.fontSize = 18;
            txt.color = new Color(0.55f, 0.48f, 0.42f, 0.85f);
            txt.alignment = TextAlignmentOptions.Center;
            txt.raycastTarget = false;
        }

        private static Sprite CreateDecoGroundSprite()
        {
            int w = 256; int h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
            Color c1 = new Color(0.96f, 0.88f, 0.78f, 0.45f);
            Color c2 = new Color(0.92f, 0.82f, 0.70f, 0.0f);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float t = (float)y / h;
                float a = Mathf.Lerp(0.45f, 0f, t);
                float wave = Mathf.Sin((float)x / w * Mathf.PI * 4f) * 0.06f;
                Color c = Color.Lerp(c1, c2, t + wave);
                c.a = a; tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f));
        }

        private static Sprite CreatePawSprite()
        {
            int s = 48;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++) tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
            Vector2 c = new Vector2(s * 0.5f, s * 0.45f);
            float[] rx = { 9f, 5f, 5f, 5f };
            Vector2[] pos = { new Vector2(0f, -6f), new Vector2(-8f, 6f), new Vector2(0f, 8f), new Vector2(8f, 6f) };
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                Vector2 p = new Vector2(x, y);
                for (int i = 0; i < 4; i++) if ((p - (c + pos[i])).magnitude <= rx[i]) tex.SetPixel(x, y, Color.white);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
        }

        private void Update()
        {
            if (Keyboard.current?.escapeKey.wasPressedThisFrame ?? false)
            {
                if (DailyRewardUI.IsOpen) { DailyRewardUI.Close(); return; }
                if (MissionUI.IsOpen) { MissionUI.Close(); return; }
                if (SettingsPanel.HandleBackButton()) return;
                ShowQuitConfirmation();
            }
        }

        private void ShowQuitConfirmation()
        {
            var existing = GameObject.Find("QuitDialog");
            if (existing != null) return;

            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            var confirmGO = new GameObject("QuitDialog");
            confirmGO.transform.SetParent(canvas.transform, false);
            var confirmRect = confirmGO.AddComponent<RectTransform>();
            confirmRect.anchorMin = Vector2.zero;
            confirmRect.anchorMax = Vector2.one;
            confirmRect.offsetMin = Vector2.zero;
            confirmRect.offsetMax = Vector2.zero;

            var confirmBg = confirmGO.AddComponent<Image>();
            confirmBg.color = new Color(0.15f, 0.20f, 0.25f, 0.50f);
            confirmBg.raycastTarget = true;

            var panel = new GameObject("Panel");
            panel.transform.SetParent(confirmGO.transform, false);
            var cpRect = panel.AddComponent<RectTransform>();
            cpRect.anchorMin = new Vector2(0.5f, 0.5f);
            cpRect.anchorMax = new Vector2(0.5f, 0.5f);
            cpRect.pivot = new Vector2(0.5f, 0.5f);
            cpRect.sizeDelta = new Vector2(700f, 300f);
            cpRect.anchoredPosition = Vector2.zero;
            var cpImg = panel.AddComponent<Image>();
            cpImg.color = new Color(0.95f, 0.97f, 0.98f);
            cpImg.raycastTarget = true;

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 24f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(40, 40, 30, 30);

            var msgGO = new GameObject("Message");
            msgGO.transform.SetParent(panel.transform, false);
            var msgTxt = msgGO.AddComponent<TextMeshProUGUI>();
            msgTxt.font = _fontBody;
            msgTxt.text = "Quitter l'application ?";
            msgTxt.fontSize = 30;
            msgTxt.color = new Color(0.20f, 0.22f, 0.25f);
            msgTxt.alignment = TextAlignmentOptions.Center;
            msgTxt.raycastTarget = false;
            var msgLE = msgGO.AddComponent<LayoutElement>();
            msgLE.preferredHeight = 60f;

            var btnRow = new GameObject("ButtonRow");
            btnRow.transform.SetParent(panel.transform, false);
            btnRow.AddComponent<RectTransform>();
            var btnHLG = btnRow.AddComponent<HorizontalLayoutGroup>();
            btnHLG.spacing = 30f;
            btnHLG.childAlignment = TextAnchor.MiddleCenter;
            btnHLG.childForceExpandWidth = false;
            btnHLG.childForceExpandHeight = false;
            btnRow.AddComponent<LayoutElement>().preferredHeight = 60f;

            var btnOui = CreerBouton(btnRow.transform, "Oui", new Color(0.90f, 0.35f, 0.35f), 26f);
            btnOui.onClick.AddListener(() =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });

            var btnAnnuler = CreerBouton(btnRow.transform, "Annuler", new Color(0.35f, 0.65f, 0.85f), 26f);
            btnAnnuler.onClick.AddListener(() =>
            {
                SFXManager.Instance.PlayMenuClose();
                Object.Destroy(confirmGO);
            });
        }

        private static Button CreerBouton(Transform parent, string label, Color bgColor, float fontSize)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(250f, 55f);

            var img = go.AddComponent<Image>();
            img.sprite = CreerSpriteArrondi(128, 0.3f);
            img.color = bgColor;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor = bgColor;
            colors.highlightedColor = bgColor * 1.15f;
            colors.pressedColor = bgColor * 0.8f;
            btn.colors = colors;

            var txtGO = new GameObject("Text");
            txtGO.transform.SetParent(go.transform, false);
            var txtRect = txtGO.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero;
            txtRect.offsetMax = Vector2.zero;
            var txt = txtGO.AddComponent<TextMeshProUGUI>();
            txt.font = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Regular SDF");
            txt.text = label;
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyles.Bold;
            txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.Center;
            txt.raycastTarget = false;

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 250f;
            le.preferredHeight = 55f;

            return btn;
        }

        private static Sprite CreerSpriteArrondi(int resolution, float coinRatio)
        {
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float half = (resolution - 1) * 0.5f;
            float radius = resolution * coinRatio;
            float inner = half - radius;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float px = x - half;
                    float py = y - half;
                    float qx = Mathf.Clamp(px, -inner, inner);
                    float qy = Mathf.Clamp(py, -inner, inner);
                    float dx = px - qx;
                    float dy = py - qy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius + 0.5f - dist);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, resolution, resolution),
                new Vector2(0.5f, 0.5f));
        }

        private static Sprite _settingsSprite;

        private static Sprite GetSettingsSprite()
        {
            if (_settingsSprite == null)
                _settingsSprite = Resources.Load<Sprite>("UI/settings");
            return _settingsSprite;
        }

        private static Sprite CreerSpriteEngrenage(int resolution)
        {
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            float center = (resolution - 1) * 0.5f;
            float outerRadius = resolution * 0.46f;
            float hubRadius = resolution * 0.20f;
            float toothWidth = resolution * 0.12f;
            float toothLength = resolution * 0.18f;
            int toothCount = 8;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);

                    bool inside = dist <= hubRadius;

                    for (int i = 0; i < toothCount; i++)
                    {
                        float toothAngle = (2f * Mathf.PI * i) / toothCount;
                        float angleDiff = Mathf.Abs(angle - toothAngle);
                        if (angleDiff > Mathf.PI)
                            angleDiff = 2f * Mathf.PI - angleDiff;

                        float halfWidth = Mathf.Atan2(toothWidth * 0.5f, outerRadius);
                        if (angleDiff <= halfWidth && dist >= hubRadius && dist <= outerRadius + toothLength)
                            inside = true;
                    }

                    float alpha = inside ? 1f : 0f;
                    if (!inside)
                    {
                        float edgeDist = dist - (outerRadius + toothLength);
                        if (edgeDist > -1.5f && edgeDist < 0f)
                            alpha = Mathf.Clamp01(edgeDist + 1.5f);
                    }

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, resolution, resolution),
                new Vector2(0.5f, 0.5f));
        }
    }
}
