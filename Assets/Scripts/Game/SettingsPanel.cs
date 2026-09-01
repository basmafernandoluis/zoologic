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

        // Palette chaude pastel, alignée sur MainMenu / LevelMap / GridView
        // (crème, pêche, terracotta, orange "niveau courant", texte brun foncé).
        private static readonly Color BgOverlay = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color TitleText = new Color(0.22f, 0.19f, 0.16f, 1f);
        private static readonly Color BodyText = new Color(0.32f, 0.28f, 0.24f, 1f);
        private static readonly Color MutedText = new Color(0.60f, 0.54f, 0.47f, 1f);
        private static readonly Color AccentOrange = new Color(0.93f, 0.68f, 0.35f, 1f);
        private static readonly Color DangerRed = new Color(0.82f, 0.32f, 0.26f, 1f);
        private static readonly Color ToggleOn = new Color(0.42f, 0.72f, 0.45f, 1f);
        private static readonly Color ToggleOff = new Color(0.78f, 0.74f, 0.69f, 1f);
        private static readonly Color NumberColor = new Color(0.22f, 0.19f, 0.16f, 1f);
        private static readonly Color ToggleCardBg = new Color(1f, 0.96f, 0.89f, 1f);
        private static readonly Color StatusOn = new Color(0.28f, 0.60f, 0.34f, 1f);
        private static readonly Color StatusOff = new Color(0.65f, 0.60f, 0.54f, 1f);
        private static readonly Color ToggleCardBorder = new Color(0.93f, 0.85f, 0.72f, 1f);

        private static TMP_FontAsset _fontTitle;
        private static TMP_FontAsset _fontBody;

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
            else
            {
                var legacy = Object.FindFirstObjectByType<StandaloneInputModule>();
                if (legacy != null) Object.Destroy(legacy);
            }
            if (Object.FindFirstObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
                _root.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

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
            panelImg.sprite = BackgroundHelper.CreateGradientSprite(BackgroundHelper.BgTop, BackgroundHelper.BgBottom);
            panelImg.type = Image.Type.Simple;
            panelImg.color = Color.white;
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
            CreerToggle(panel.transform, "Musique", SFXManager.Instance.MusicEnabled,
                val => { SFXManager.Instance.MusicEnabled = val; });
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
            txt.color = TitleText;
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
            hlg.spacing = 16f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = false;
            hlg.padding = new RectOffset(22, 22, 14, 14);

            // Carte de fond : sépare visuellement chaque réglage.
            var card = row.AddComponent<Image>();
            card.sprite = CreerSpriteArrondi(128, 0.22f);
            card.type = Image.Type.Simple;
            card.color = ToggleCardBg;
            card.raycastTarget = false;

            var cardShadow = row.gameObject.AddComponent<Shadow>();
            cardShadow.effectColor = new Color(0f, 0f, 0f, 0.14f);
            cardShadow.effectDistance = new Vector2(2f, -3f);

            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 88f;
            rowLE.flexibleWidth = 1f;

            // Label (gras et sombre pour une meilleure identification).
            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(row.transform, false);
            var labelText = labelGO.AddComponent<TextMeshProUGUI>();
            labelText.font = _fontTitle;
            labelText.text = label;
            labelText.fontSize = 36;
            labelText.fontStyle = FontStyles.Bold;
            labelText.color = TitleText;
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            labelText.raycastTarget = false;
            var labelLE = labelGO.AddComponent<LayoutElement>();
            labelLE.flexibleWidth = 1f;

            // Statut actuel (Activé / Désactivé), mis à jour en direct.
            var statusGO = new GameObject("Status");
            statusGO.transform.SetParent(row.transform, false);
            var statusText = statusGO.AddComponent<TextMeshProUGUI>();
            statusText.font = _fontBody;
            statusText.text = isOn ? "Activé" : "Désactivé";
            statusText.fontSize = 28;
            statusText.fontStyle = FontStyles.Bold;
            statusText.color = isOn ? StatusOn : StatusOff;
            statusText.alignment = TextAlignmentOptions.MidlineRight;
            statusText.raycastTarget = false;
            var statusLE = statusGO.AddComponent<LayoutElement>();
            statusLE.preferredWidth = 130f;

            // Grand interrupteur clair.
            var toggleGO = new GameObject("Switch");
            toggleGO.transform.SetParent(row.transform, false);
            var toggleRect = toggleGO.AddComponent<RectTransform>();
            toggleRect.sizeDelta = new Vector2(112f, 62f);

            var toggle = toggleGO.AddComponent<Toggle>();
            toggle.isOn = isOn;
            toggle.transition = Selectable.Transition.None;
            toggle.graphic = null;

            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(toggleGO.transform, false);
            var bgRect = bgGO.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = isOn ? ToggleOn : ToggleOff;
            bgImg.sprite = CreerSpriteArrondi(128, 0.45f);
            toggle.targetGraphic = bgImg;

            var knobGO = new GameObject("Knob", typeof(RectTransform));
            knobGO.transform.SetParent(bgGO.transform, false);
            var knobRect = knobGO.GetComponent<RectTransform>();
            knobRect.anchorMin = new Vector2(0f, 0.5f);
            knobRect.anchorMax = new Vector2(0f, 0.5f);
            knobRect.pivot = new Vector2(0.5f, 0.5f);
            knobRect.sizeDelta = new Vector2(52f, 52f);
            knobRect.anchoredPosition = new Vector2(isOn ? 28f : -28f, 0f);
            var knobImg = knobGO.AddComponent<Image>();
            knobImg.sprite = CreerSpriteArrondi(64, 0.5f);
            knobImg.color = Color.white;

            toggle.onValueChanged.AddListener(val =>
            {
                bgImg.color = val ? ToggleOn : ToggleOff;
                statusText.text = val ? "Activé" : "Désactivé";
                statusText.color = val ? StatusOn : StatusOff;
                SwitchAnimator.Animate(knobRect, val ? 28f : -28f);
                onValueChanged(val);
                SFXManager.Instance.PlayMenuOpen();
            });
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
            cpImg.sprite = BackgroundHelper.CreateGradientSprite(BackgroundHelper.BgTop, BackgroundHelper.BgBottom);
            cpImg.type = Image.Type.Simple;
            cpImg.color = Color.white;
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
            msgTxt.color = BodyText;
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

            var btnAnnuler = CreerBouton(btnRow.transform, "Annuler", AccentOrange, 30f);
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
            var go = new GameObject("CloseBtn", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(72f, 72f);

            var img = go.AddComponent<Image>();
            img.sprite = CreerSpriteArrondi(128, 0.5f);
            img.type = Image.Type.Simple;
            img.color = new Color(0.93f, 0.90f, 0.86f, 1f);
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.85f, 0.88f, 0.92f);
            colors.pressedColor = new Color(0.72f, 0.76f, 0.82f);
            btn.colors = colors;

            var iconGO = new GameObject("X", typeof(RectTransform));
            iconGO.transform.SetParent(go.transform, false);
            var iconRect = iconGO.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;
            var iconTxt = iconGO.AddComponent<TextMeshProUGUI>();
            iconTxt.text = "\u00D7";
            iconTxt.fontSize = 52;
            iconTxt.fontStyle = FontStyles.Bold;
            iconTxt.color = NumberColor;
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

        internal static class SwitchAnimator
        {
            public static void Animate(RectTransform knob, float targetX)
            {
                var host = new GameObject("SwitchAnimator");
                Object.DontDestroyOnLoad(host);
                var runner = host.AddComponent<SwitchCoroutine>();
                runner.Run(knob, targetX);
            }

            private class SwitchCoroutine : MonoBehaviour
            {
                private float _from;
                private float _to;
                private RectTransform _knob;
                private float _time;

                public void Run(RectTransform knob, float to)
                {
                    _knob = knob;
                    _from = knob.anchoredPosition.x;
                    _to = to;
                    _time = 0f;
                    StartCoroutine(Move());
                }

                private System.Collections.IEnumerator Move()
                {
                    const float duration = 0.18f;
                    while (_time < duration)
                    {
                        _time += Time.deltaTime;
                        float t = Mathf.Clamp01(_time / duration);
                        t = t * t * (3f - 2f * t);
                        var p = _knob.anchoredPosition;
                        p.x = Mathf.Lerp(_from, _to, t);
                        _knob.anchoredPosition = p;
                        yield return null;
                    }

                    var final = _knob.anchoredPosition;
                    final.x = _to;
                    _knob.anchoredPosition = final;

                    Destroy(gameObject);
                }
            }
        }
    }
}
