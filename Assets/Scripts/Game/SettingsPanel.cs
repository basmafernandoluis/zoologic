using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Zoologic
{
    public static class SettingsPanel
    {
        public static bool IsOpen { get; private set; }

        private static GameObject _root;
        private static Canvas _overlayCanvas;

        private static readonly Color BgOverlay = new Color(0f, 0f, 0f, 0.62f);
        private static readonly Color PanelBg = new Color(0.16f, 0.12f, 0.24f);
        private static readonly Color AccentBlue = new Color(0.20f, 0.55f, 0.95f);
        private static readonly Color DangerRed = new Color(0.88f, 0.28f, 0.28f);
        private static readonly Color ToggleOn = new Color(0.25f, 0.76f, 0.50f);
        private static readonly Color ToggleOff = new Color(0.48f, 0.44f, 0.54f);
        private static readonly Color MutedText = new Color(0.62f, 0.60f, 0.68f);

        private static TMP_FontAsset _fontTitle;
        private static TMP_FontAsset _fontBody;

        private static Toggle _sfxToggle;
        private static Toggle _hapticsToggle;

        public static void Open()
        {
            if (IsOpen) return;
            IsOpen = true;

            _fontTitle = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Bold SDF");
            _fontBody = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Regular SDF");

            Build();
        }

        public static void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;

            if (_root != null)
                Object.Destroy(_root);
            _root = null;
            _overlayCanvas = null;
        }

        public static bool HandleBackButton()
        {
            if (!IsOpen) return false;
            Close();
            return true;
        }

        private static void Build()
        {
            _root = new GameObject("SettingsOverlay");

            _overlayCanvas = _root.AddComponent<Canvas>();
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.sortingOrder = 1000;

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            _root.AddComponent<GraphicRaycaster>();

            if (UnityEngine.EventSystems.EventSystem.current == null)
                _root.AddComponent<UnityEngine.EventSystems.EventSystem>();
            if (Object.FindFirstObjectByType<StandaloneInputModule>() == null)
                _root.AddComponent<StandaloneInputModule>();

            CreerOverlayFond(_root.transform);
            BuildPanel(_root.transform);
        }

        private static void CreerOverlayFond(Transform parent)
        {
            var go = new GameObject("BgOverlay");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = BgOverlay;
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(Close);
        }

        private static void BuildPanel(Transform parent)
        {
            var panel = new GameObject("Panel");
            panel.transform.SetParent(parent, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(900f, 760f);
            panelRect.anchoredPosition = Vector2.zero;

            // Ombre portée (frère derrière le panneau, légèrement décalée).
            var shadowGO = new GameObject("Shadow");
            shadowGO.transform.SetParent(parent, false);
            var shadowRect = shadowGO.AddComponent<RectTransform>();
            shadowRect.anchorMin = new Vector2(0.5f, 0.5f);
            shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
            shadowRect.pivot = new Vector2(0.5f, 0.5f);
            shadowRect.sizeDelta = new Vector2(930f, 790f);
            shadowRect.anchoredPosition = new Vector2(0f, -10f);
            var shadowImg = shadowGO.AddComponent<Image>();
            shadowImg.sprite = CreerSpriteArrondi(128, 0.08f);
            shadowImg.color = new Color(0f, 0f, 0f, 0.42f);
            shadowImg.raycastTarget = false;
            shadowGO.transform.SetSiblingIndex(panel.transform.GetSiblingIndex());

            var panelImg = panel.AddComponent<Image>();
            panelImg.sprite = CreerSpriteArrondi(128, 0.08f);
            panelImg.type = Image.Type.Simple;
            panelImg.color = PanelBg;
            panelImg.raycastTarget = true;

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 24f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(55, 55, 46, 34);

            CreerTitre(panel.transform);
            CreerToggle(panel.transform, "Sons", SFXManager.Instance.IsEnabled,
                val => { SFXManager.Instance.IsEnabled = val; });
            CreerToggle(panel.transform, "Vibrations", Haptics.IsEnabled,
                val => { Haptics.IsEnabled = val; });
            CreerBoutonResetProgression(panel.transform);
            CreerVersion(panel.transform);

            CreerBoutonFermer(panel.transform);
        }

        private static void CreerTitre(Transform parent)
        {
            var go = new GameObject("Title");
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<TextMeshProUGUI>();
            txt.font = _fontTitle;
            txt.text = "Réglages";
            txt.fontSize = 48;
            txt.fontStyle = FontStyles.Bold;
            txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.Center;
            txt.raycastTarget = false;

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 64f;
        }

        private static void CreerToggle(Transform parent, string label, bool isOn, System.Action<bool> onValueChanged)
        {
            var row = new GameObject("Toggle_" + label);
            row.transform.SetParent(parent, false);
            var rowRect = row.AddComponent<RectTransform>();
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.padding = new RectOffset(10, 10, 5, 5);

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(row.transform, false);
            var labelText = labelGO.AddComponent<TextMeshProUGUI>();
            labelText.font = _fontBody;
            labelText.text = label;
            labelText.fontSize = 34;
            labelText.color = Color.white;
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            labelText.raycastTarget = false;
            var labelLE = labelGO.AddComponent<LayoutElement>();
            labelLE.flexibleWidth = 1f;

            var toggleGO = new GameObject("Toggle");
            toggleGO.transform.SetParent(row.transform, false);
            var toggleRect = toggleGO.AddComponent<RectTransform>();
            toggleRect.sizeDelta = new Vector2(104f, 56f);

            var toggle = toggleGO.AddComponent<Toggle>();
            toggle.isOn = isOn;
            toggle.transition = Selectable.Transition.None;

            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(toggleGO.transform, false);
            var bgRect = bgGO.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = isOn ? ToggleOn : ToggleOff;
            bgImg.sprite = CreerSpriteArrondi(128, 0.4f);
            toggle.targetGraphic = bgImg;

            var checkGO = new GameObject("Checkmark");
            checkGO.transform.SetParent(bgGO.transform, false);
            var checkRect = checkGO.AddComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkRect.pivot = new Vector2(0.5f, 0.5f);
            checkRect.sizeDelta = new Vector2(42f, 42f);
            checkRect.anchoredPosition = Vector2.zero;
            var checkImg = checkGO.AddComponent<Image>();
            checkImg.sprite = CreerSpriteArrondi(64, 0.5f);
            checkImg.color = Color.white;

            toggle.graphic = checkImg;
            toggle.onValueChanged.AddListener(val =>
            {
                bgImg.color = val ? ToggleOn : ToggleOff;
                onValueChanged(val);
                SFXManager.Instance.PlayMenuOpen();
            });

            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 50f;
        }

        private static void CreerBoutonResetProgression(Transform parent)
        {
            var btn = CreerBouton(parent, "Réinitialiser la progression", DangerRed, 34f);
            btn.onClick.AddListener(() =>
            {
                SFXManager.Instance.PlayMenuClose();
                ShowResetConfirmation();
            });
        }

        private static void ShowResetConfirmation()
        {
            if (_root == null) return;

            var confirmGO = new GameObject("ConfirmDialog");
            confirmGO.transform.SetParent(_root.transform, false);
            var confirmRect = confirmGO.AddComponent<RectTransform>();
            confirmRect.anchorMin = Vector2.zero;
            confirmRect.anchorMax = Vector2.one;
            confirmRect.offsetMin = Vector2.zero;
            confirmRect.offsetMax = Vector2.zero;

            var confirmBg = confirmGO.AddComponent<Image>();
            confirmBg.color = new Color(0f, 0f, 0f, 0.7f);
            confirmBg.raycastTarget = true;

            var confirmPanel = new GameObject("ConfirmPanel");
            confirmPanel.transform.SetParent(confirmGO.transform, false);
            var cpRect = confirmPanel.AddComponent<RectTransform>();
            cpRect.anchorMin = new Vector2(0.5f, 0.5f);
            cpRect.anchorMax = new Vector2(0.5f, 0.5f);
            cpRect.pivot = new Vector2(0.5f, 0.5f);
            cpRect.sizeDelta = new Vector2(740f, 380f);
            cpRect.anchoredPosition = Vector2.zero;
            var cpImg = confirmPanel.AddComponent<Image>();
            cpImg.sprite = CreerSpriteArrondi(128, 0.08f);
            cpImg.color = PanelBg;
            cpImg.raycastTarget = true;

            var vlg = confirmPanel.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 24f;
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(40, 40, 30, 30);

            var msgGO = new GameObject("Message");
            msgGO.transform.SetParent(confirmPanel.transform, false);
            var msgTxt = msgGO.AddComponent<TextMeshProUGUI>();
            msgTxt.font = _fontBody;
            msgTxt.text = "Es-tu sûr ?\nCette action est irréversible.";
            msgTxt.fontSize = 32;
            msgTxt.color = Color.white;
            msgTxt.alignment = TextAlignmentOptions.Center;
            msgTxt.raycastTarget = false;
            var msgLE = msgGO.AddComponent<LayoutElement>();
            msgLE.preferredHeight = 80f;

            var btnRow = new GameObject("ButtonRow");
            btnRow.transform.SetParent(confirmPanel.transform, false);
            btnRow.AddComponent<RectTransform>();
            var btnHLG = btnRow.AddComponent<HorizontalLayoutGroup>();
            btnHLG.spacing = 30f;
            btnHLG.childAlignment = TextAnchor.MiddleCenter;
            btnHLG.childForceExpandWidth = false;
            btnHLG.childForceExpandHeight = false;
            btnRow.AddComponent<LayoutElement>().preferredHeight = 60f;

            var btnOui = CreerBouton(btnRow.transform, "Oui", DangerRed, 30f);
            btnOui.onClick.AddListener(() =>
            {
                LevelProgressManager.ResetAll();
                Object.Destroy(confirmGO);
                Close();
                SceneManager.LoadScene("MainMenu");
            });

            var btnAnnuler = CreerBouton(btnRow.transform, "Annuler", AccentBlue, 30f);
            btnAnnuler.onClick.AddListener(() =>
            {
                SFXManager.Instance.PlayMenuClose();
                Object.Destroy(confirmGO);
            });
        }

        private static void CreerVersion(Transform parent)
        {
            var go = new GameObject("Version");
            go.transform.SetParent(parent, false);
            var txt = go.AddComponent<TextMeshProUGUI>();
            txt.font = _fontBody;
            txt.text = "v0.1";
            txt.fontSize = 23;
            txt.color = MutedText;
            txt.alignment = TextAlignmentOptions.Center;
            txt.raycastTarget = false;

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 30f;
            le.flexibleHeight = 1f;
        }

        private static void CreerBoutonFermer(Transform parent)
        {
            var go = new GameObject("CloseBtn");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(72f, 72f);

            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
            btn.colors = colors;

            var iconGO = new GameObject("X");
            iconGO.transform.SetParent(go.transform, false);
            var iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconTxt = iconGO.AddComponent<TextMeshProUGUI>();
            iconTxt.text = "\u00D7";
            iconTxt.fontSize = 52;
            iconTxt.fontStyle = FontStyles.Bold;
            iconTxt.color = Color.white;
            iconTxt.alignment = TextAlignmentOptions.Center;
            iconTxt.raycastTarget = false;

            btn.onClick.AddListener(() =>
            {
                SFXManager.Instance.PlayMenuClose();
                Close();
            });

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 72f;
            le.preferredHeight = 72f;
        }

        private static Button CreerBouton(Transform parent, string label, Color bgColor, float fontSize)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(320f, 66f);

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
            txt.font = _fontBody;
            txt.text = label;
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyles.Bold;
            txt.color = Color.white;
            txt.alignment = TextAlignmentOptions.Center;
            txt.raycastTarget = false;

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 520f;
            le.preferredHeight = 66f;

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
    }
}
