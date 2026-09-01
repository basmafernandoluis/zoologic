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
            var groundGO = new GameObject("GroundNature");
            groundGO.transform.SetParent(parent, false);
            var groundRect = groundGO.AddComponent<RectTransform>();
            groundRect.anchorMin = new Vector2(0f, 0f);
            groundRect.anchorMax = new Vector2(1f, 0f);
            groundRect.pivot = new Vector2(0.5f, 0f);
            groundRect.sizeDelta = new Vector2(0f, 220f);
            groundRect.anchoredPosition = Vector2.zero;
            var groundImg = groundGO.AddComponent<Image>();
            groundImg.sprite = CreateGroundSprite();
            groundImg.type = Image.Type.Simple;
            groundImg.color = Color.white;
            groundImg.raycastTarget = false;

            for (int i = 0; i < 2; i++)
            {
                var tuftGO = new GameObject("Tuft" + i);
                tuftGO.transform.SetParent(groundGO.transform, false);
                var tuftRect = tuftGO.AddComponent<RectTransform>();
                tuftRect.anchorMin = new Vector2(i == 0 ? 0.12f : 0.88f, 1f);
                tuftRect.anchorMax = new Vector2(i == 0 ? 0.12f : 0.88f, 1f);
                tuftRect.pivot = new Vector2(0.5f, 0f);
                tuftRect.sizeDelta = new Vector2(80f, 40f);
                tuftRect.anchoredPosition = new Vector2(0f, -6f);
                var tuftImg = tuftGO.AddComponent<Image>();
                tuftImg.sprite = CreateTuftSprite();
                tuftImg.color = new Color(0.62f, 0.78f, 0.55f, 0.85f);
                tuftImg.raycastTarget = false;
            }
        }

        private void BuildTitle(Transform parent)
        {
            var go = new GameObject("Title");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.72f);
            rect.anchorMax = new Vector2(0.5f, 0.72f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(800f, 140f);
            rect.anchoredPosition = Vector2.zero;

            var txt = go.AddComponent<TextMeshProUGUI>();
            txt.font = _fontTitle;
            txt.text = "ZOO LOGIC";
            txt.fontSize = 80;
            txt.fontStyle = FontStyles.Bold;
            txt.color = TitleOrange;
            txt.alignment = TextAlignmentOptions.Center;
            txt.raycastTarget = false;

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            shadow.effectDistance = new Vector2(4f, -4f);

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(TitleOutline.r, TitleOutline.g, TitleOutline.b, 0.50f);
            outline.effectDistance = new Vector2(3f, -3f);
        }

        private void BuildPlayButton(Transform parent)
        {
            var go = new GameObject("PlayButton");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.32f);
            rect.anchorMax = new Vector2(0.5f, 0.32f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(440f, 100f);
            rect.anchoredPosition = Vector2.zero;

            var img = go.AddComponent<Image>();
            Sprite wood = Resources.Load<Sprite>("UI/Cozy/btn_wood_light");
            img.sprite = wood != null ? wood : CreerSpriteArrondi(128, 0.35f);
            img.type = wood != null ? Image.Type.Sliced : Image.Type.Simple;
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
            txt.fontSize = 52;
            txt.fontStyle = FontStyles.Bold;
            txt.color = new Color(0.32f, 0.20f, 0.12f);
            txt.alignment = TextAlignmentOptions.Center;
            txt.raycastTarget = false;
            txt.outlineWidth = 0.18f;
            txt.outlineColor = new Color(1f, 1f, 1f, 0.85f);

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.38f, 0.24f, 0.14f, 0.14f);
            shadow.effectDistance = new Vector2(0f, -5f);
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
            hlg.padding = new RectOffset(14, 12, 0, 0);
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childControlWidth = false;

            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGO.transform.SetParent(go.transform, false);
            var iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(28f, 28f);
            var iconImg = iconGO.GetComponent<Image>();
            iconImg.sprite = Resources.Load<Sprite>("UI/Icons/gem_icon");
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            iconGO.AddComponent<LayoutElement>().preferredWidth = 28f;

            var txtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            txtGO.transform.SetParent(go.transform, false);
            var txt = txtGO.GetComponent<TextMeshProUGUI>();
            txt.font = _fontTitle;
            txt.text = "Cadeau";
            txt.fontSize = 21;
            txt.fontStyle = FontStyles.Bold;
            txt.color = canClaim ? new Color(0.32f, 0.20f, 0.10f) : new Color(0.45f, 0.40f, 0.36f);
            txt.alignment = TextAlignmentOptions.Center;
            txtGO.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() =>
            {
                SFXManager.Instance.PlayMenuOpen();
                var canvas = FindFirstObjectByType<Canvas>();
                if (canvas != null) DailyRewardUI.Show(canvas);
            });

            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0.38f, 0.24f, 0.14f, 0.14f);
            sh.effectDistance = new Vector2(0f, -4f);

            if (canClaim) go.AddComponent<DailyPulse>();
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
            hlg.padding = new RectOffset(14, 12, 0, 0);
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;

            var iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconGO.transform.SetParent(go.transform, false);
            var iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(26f, 26f);
            var iconImg = iconGO.GetComponent<Image>();
            iconImg.sprite = Resources.Load<Sprite>("UI/Icons/scroll_icon");
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            iconGO.AddComponent<LayoutElement>().preferredWidth = 26f;

            var txtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            txtGO.transform.SetParent(go.transform, false);
            var txt = txtGO.GetComponent<TextMeshProUGUI>();
            txt.font = _fontTitle;
            txt.text = "Missions";
            txt.fontSize = 20;
            txt.fontStyle = FontStyles.Bold;
            txt.color = hasClaim ? new Color(0.32f, 0.20f, 0.10f) : Color.white;
            txt.alignment = TextAlignmentOptions.Center;
            txtGO.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() =>
            {
                SFXManager.Instance.PlayMenuOpen();
                var canvas = FindFirstObjectByType<Canvas>();
                if (canvas != null) MissionUI.Show(canvas);
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
            }
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

            var branchGO = new GameObject("Branch");
            branchGO.transform.SetParent(parent, false);
            var branchRect = branchGO.AddComponent<RectTransform>();
            branchRect.anchorMin = new Vector2(0.5f, 0.46f);
            branchRect.anchorMax = new Vector2(0.5f, 0.46f);
            branchRect.pivot = new Vector2(0.5f, 0.5f);
            branchRect.sizeDelta = new Vector2(360f, 22f);
            branchRect.anchoredPosition = new Vector2(0f, -18f);
            var branchImg = branchGO.AddComponent<Image>();
            branchImg.sprite = CreateBranchSprite();
            branchImg.type = Image.Type.Simple;
            branchImg.color = Color.white;
            branchImg.raycastTarget = false;
            var branchSh = branchGO.AddComponent<Shadow>();
            branchSh.effectColor = new Color(0f, 0f, 0f, 0.12f);
            branchSh.effectDistance = new Vector2(0f, -6f);

            for (int i = 0; i < 2; i++)
            {
                var leafGO = new GameObject("Leaf" + i);
                leafGO.transform.SetParent(branchGO.transform, false);
                var leafRect = leafGO.AddComponent<RectTransform>();
                leafRect.anchorMin = new Vector2(i == 0 ? 0.08f : 0.92f, 0.5f);
                leafRect.anchorMax = new Vector2(i == 0 ? 0.08f : 0.92f, 0.5f);
                leafRect.pivot = new Vector2(0.5f, 0.5f);
                leafRect.sizeDelta = new Vector2(26f, 26f);
                leafRect.anchoredPosition = new Vector2(0f, 10f);
                leafRect.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? -22f : 22f);
                var leafImg = leafGO.AddComponent<Image>();
                leafImg.sprite = CreateLeafSprite();
                leafImg.color = new Color(0.68f, 0.84f, 0.58f, 1f);
                leafImg.raycastTarget = false;
            }

            var go = new GameObject("OwlMascot");
            go.transform.SetParent(branchGO.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(260f, 260f);
            rect.anchoredPosition = new Vector2(0f, 8f);

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
            rect.sizeDelta = new Vector2(200f, 40f);
            rect.anchoredPosition = new Vector2(0f, 20f);

            var txt = go.AddComponent<TextMeshProUGUI>();
            txt.font = _fontBody;
            txt.text = "v0.1";
            txt.fontSize = 20;
            txt.color = new Color(0.40f, 0.45f, 0.48f, 0.70f);
            txt.alignment = TextAlignmentOptions.Center;
            txt.raycastTarget = false;
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

        private static Sprite CreateGroundSprite()
        {
            int w = 256; int h = 80;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
            Color bottom = new Color(0.68f, 0.84f, 0.60f);
            Color top = new Color(0.92f, 0.95f, 0.88f, 0f);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float t = (float)y / h;
                float wave = Mathf.Sin((float)x / w * Mathf.PI * 3f) * 0.04f;
                Color c = Color.Lerp(bottom, top, Mathf.Clamp01(t + wave));
                float a = Mathf.Lerp(0.95f, 0f, t);
                c.a = a; tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f));
        }

        private static Sprite CreateTuftSprite()
        {
            int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++) tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
            Vector2 c = new Vector2(s * 0.5f, s * 0.35f);
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                Vector2 p = new Vector2(x, y);
                float d = (p - c).magnitude;
                float angle = Mathf.Atan2(p.y - c.y, p.x - c.x);
                float r = 22f + Mathf.Sin(angle * 3f) * 4f;
                if (d <= r && p.y >= c.y - 4f) tex.SetPixel(x, y, Color.white);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0f));
        }

        private static Sprite CreateBranchSprite()
        {
            int w = 360; int h = 22;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
            Color wood = new Color(0.72f, 0.52f, 0.36f);
            Color dark = new Color(0.48f, 0.34f, 0.22f);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float t = Mathf.Abs(y - h * 0.5f) / (h * 0.5f);
                float round = Mathf.Clamp01(1f - t * 0.85f);
                float vein = Mathf.Sin((float)x * 0.08f + y * 0.2f) * 0.04f;
                Color c = Color.Lerp(dark, wood, round + vein);
                float endFade = Mathf.Clamp01(Mathf.Min(x, w - 1 - x) / 18f);
                c.a = endFade;
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        }

        private static Sprite CreateLeafSprite()
        {
            int s = 32;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++) tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
            Vector2 c = new Vector2(s * 0.5f, s * 0.5f);
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                Vector2 p = new Vector2(x, y) - c;
                float d = (p.x * p.x) / 64f + (p.y * p.y) / 100f;
                if (d <= 1f) tex.SetPixel(x, y, Color.white);
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
        }
    }
}
