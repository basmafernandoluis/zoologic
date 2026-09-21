using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zoologic.Localization;

namespace Zoologic
{
    /// <summary>
    /// Écran de lancement 100 % Sprites : fond uni + déco b.png, bannière bois,
    /// médaillon hibou sp1_3, vraie barre de progression b_30.
    /// titre + sous-titre localisé, barre de chargement. Précharge les sprites
    /// gameplay en tâche de fond (masque le vrai chargement), durée min 2.8s
    /// avec progression lisse pilotée par le temps,
    /// skippable au toucher, puis fondu vers MainMenu.
    ///
    /// Tout est construit procéduralement (aucun prefab) comme le reste du jeu.
    /// La scène Splash.unity (index 0) est générée par SplashSceneBuilder.
    /// </summary>
    public sealed class SplashController : MonoBehaviour
    {
        private const string MainMenuScene = "MainMenu";
        private const float DureeMinimale = 2.8f;
        private const float DelaiAvantSkip = 0.4f;

        private static readonly Color TitreCream = new Color(1f, 0.98f, 0.94f, 1f);
        private static readonly Color ContourBrun = new Color(0.29f, 0.18f, 0.10f, 1f);
        private static readonly Color FondOrange = new Color(0.96f, 0.68f, 0.25f, 1f);

        private struct DecoInfo
        {
            public RectTransform Rect;
            public Vector2 Base;
            public float Phase;
        }

        private Canvas _canvas;
        private Image _barreFillImg;
        private RectTransform _barreFillRect;
        private readonly System.Collections.Generic.List<DecoInfo> _deco =
            new System.Collections.Generic.List<DecoInfo>();
        private bool _skipDemande;
        private bool _termine;
        private bool _preloadTermine;
        private float _progressionPreload;
        private float _debut;

        private void Start()
        {
            _debut = Time.unscaledTime;
            try
            {
                _canvas = EnsureCanvas();
                EnsureEventSystem();
                ConstruireFond(_canvas);
                ConstruireTitre(_canvas);
                ConstruireBarre(_canvas);
                ConstruireSkip(_canvas);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Zoologic] Splash: échec de construction UI, passage direct au menu.\n" + e);
                ChargerMenu();
                return;
            }
            SceneFader.FadeIn(this, _canvas, 0.3f);
            StartCoroutine(RoutineSplash());
            StartCoroutine(DeriveDecoRoutine());
        }

        /// <summary>
        /// Le préchargement réel est quasi instantané : la barre est donc
        /// pilotée par le temps (remplissage lisse sur toute la durée),
        /// avec le preload comme plancher. Skip au toucher (travail arrêté).
        /// </summary>
        private IEnumerator RoutineSplash()
        {
            Coroutine preload = StartCoroutine(Precharger());
            float elapsed = 0f;
            while (true)
            {
                elapsed += Time.unscaledDeltaTime;
                float tTemps = Easing.EaseInOutQuad(Mathf.Clamp01(elapsed / DureeMinimale));
                SetProgression(Mathf.Max(_progressionPreload, tTemps));
                if ((elapsed >= DureeMinimale && _preloadTermine) || _skipDemande)
                    break;
                yield return null;
            }
            if (preload != null)
                StopCoroutine(preload);
            SetProgression(1f);
            if (_termine) yield break;
            _termine = true;
            SceneFader.FadeOut(this, _canvas, 0.3f, ChargerMenu);
        }

        private void ChargerMenu()
        {
            try { SceneManager.LoadScene(MainMenuScene); }
            catch (System.Exception e) { Debug.LogError("[Zoologic] Splash: impossible de charger " + MainMenuScene + ".\n" + e); }
        }

        /// <summary>
        /// Précharge les assets gameplay + les 6 atlas Sprites (b, bak, hi,
        /// sp1, mx_, x) consommés dès le MainMenu ; avance le plancher de progression.
        /// </summary>
        private IEnumerator Precharger()
        {
            string[] sprites =
            {
                "Sprites/bak_0", "Sprites/bak_2", "Sprites/bak_3", "Sprites/bak_4",
                "Sprites/b_11", "Sprites/b_13", "Sprites/b_9", "Sprites/b_8",
                "Sprites/b_38", "Sprites/b_44",
                "Art/Animals/owl",
            };
            string[] atlas =
            {
                "Sprites/b", "Sprites/bak", "Sprites/hi",
                "Sprites/sp1", "Sprites/mx_", "Sprites/x",
            };
            int total = sprites.Length + atlas.Length + 2;
            int faits = 0;
            foreach (string path in sprites)
            {
                try { Resources.Load<Sprite>(path); } catch { }
                faits++;
                _progressionPreload = faits / (float)total;
                yield return null;
            }
            foreach (string dossier in atlas)
            {
                try { Resources.LoadAll<Sprite>(dossier); } catch { }
                faits++;
                _progressionPreload = faits / (float)total;
                yield return null;
            }
            try { AnimalIconSet.LoadAll(); } catch { }
            faits++;
            _progressionPreload = faits / (float)total;
            yield return null;
            try { LocalizationManager.ApplyFontsToScene(); } catch { }
            _progressionPreload = 1f;
            _preloadTermine = true;
            yield return null;
        }

        private void SetProgression(float t)
        {
            t = Mathf.Clamp01(t);
            if (_barreFillImg != null && _barreFillImg.type == Image.Type.Filled)
            {
                _barreFillImg.fillAmount = t;
                return;
            }
            if (_barreFillRect != null)
                _barreFillRect.sizeDelta = new Vector2(Mathf.Max(56f, 612f * Easing.EaseOutCubic(t)), 101f);
        }

        // ------------------------------------------------------------------
        // Construction UI.
        // ------------------------------------------------------------------

        private static Canvas EnsureCanvas()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null) return canvas;
            var go = new GameObject("SplashCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            foreach (var legacy in FindObjectsByType<UnityEngine.EventSystems.StandaloneInputModule>(FindObjectsSortMode.None))
                Destroy(legacy);
            if (UnityEngine.EventSystems.EventSystem.current == null)
            {
                var go = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
                go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
            else if (UnityEngine.EventSystems.EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
            {
                UnityEngine.EventSystems.EventSystem.current.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
        }

        /// <summary>
        /// Fond uni orange chaud + déco flottante issue de l'atlas b.png
        /// (pattes, abeilles, feuilles). 100 % Sprites, aucun key-art.
        /// </summary>
        private void ConstruireFond(Canvas canvas)
        {
            var go = new GameObject("Fond", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = FondOrange;
            img.raycastTarget = false;

            AjouterDeco(canvas, "b_1", new Vector2(-380f, 760f), 90f, 0f);
            AjouterDeco(canvas, "b_3", new Vector2(400f, 640f), 110f, 2f);
            AjouterDeco(canvas, "b_7", new Vector2(-420f, 200f), 100f, 1f);
            AjouterDeco(canvas, "b_5", new Vector2(430f, -80f), 90f, 3f);
            AjouterDeco(canvas, "b_14", new Vector2(-440f, -420f), 80f, 4f);
            AjouterDeco(canvas, "b_17", new Vector2(440f, -480f), 90f, 5f);
            AjouterDeco(canvas, "b_2", new Vector2(180f, 830f), 80f, 2.5f);
        }

        private void AjouterDeco(Canvas canvas, string nomSprite, Vector2 pos, float taille, float phase)
        {
            Sprite s = null;
            try { s = B1UI.Get(nomSprite); } catch { }
            if (s == null) return;
            var go = new GameObject("Deco_" + nomSprite, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(taille, taille);
            rect.anchoredPosition = pos;
            var img = go.GetComponent<Image>();
            img.sprite = s;
            img.preserveAspect = true;
            img.raycastTarget = false;
            _deco.Add(new DecoInfo { Rect = rect, Base = pos, Phase = phase });
        }

        /// <summary>Dérive lente des décorations (flottement + léger balancement).</summary>
        private IEnumerator DeriveDecoRoutine()
        {
            while (true)
            {
                float t = Time.unscaledTime;
                for (int i = 0; i < _deco.Count; i++)
                {
                    DecoInfo d = _deco[i];
                    if (d.Rect == null) continue;
                    d.Rect.anchoredPosition = d.Base + new Vector2(
                        Mathf.Sin(t * 0.7f + d.Phase) * 18f,
                        Mathf.Cos(t * 0.55f + d.Phase) * 22f);
                    d.Rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.5f + d.Phase) * 6f);
                }
                yield return null;
            }
        }

        private void ConstruireTitre(Canvas canvas)
        {
            TMP_FontAsset gras = Resources.Load<TMP_FontAsset>("Fonts/Fredoka/Fredoka-Bold SDF");

            // Bannière bois de l'atlas b.png (b_4, 9-slice) derrière le titre.
            var bandeauGO = new GameObject("Bandeau", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bandeauGO.transform.SetParent(canvas.transform, false);
            var bandeauRect = (RectTransform)bandeauGO.transform;
            bandeauRect.anchorMin = new Vector2(0.5f, 0.5f);
            bandeauRect.anchorMax = new Vector2(0.5f, 0.5f);
            bandeauRect.pivot = new Vector2(0.5f, 0.5f);
            bandeauRect.sizeDelta = new Vector2(960f, 190f);
            bandeauRect.anchoredPosition = new Vector2(0f, 470f);
            var bandeauImg = bandeauGO.GetComponent<Image>();
            Sprite bois = null;
            try { bois = B1UI.WoodBar ?? B1UI.Bubble; } catch { }
            if (bois != null)
            {
                bandeauImg.sprite = bois;
                bandeauImg.type = Image.Type.Sliced;
                bandeauImg.color = Color.white;
            }
            else
            {
                bandeauImg.sprite = GridView.SharedRoundedRect;
                bandeauImg.type = Image.Type.Sliced;
                bandeauImg.color = ContourBrun;
            }
            bandeauImg.raycastTarget = false;

            // Mascotte : médaillon hibou bois sp1_3 (repli : hibou Art).
            var hibouGO = new GameObject("Hibou", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            hibouGO.transform.SetParent(canvas.transform, false);
            var hibouRect = (RectTransform)hibouGO.transform;
            hibouRect.anchorMin = new Vector2(0.5f, 0.5f);
            hibouRect.anchorMax = new Vector2(0.5f, 0.5f);
            hibouRect.pivot = new Vector2(0.5f, 0.5f);
            hibouRect.sizeDelta = new Vector2(300f, 300f);
            hibouRect.anchoredPosition = new Vector2(0f, -30f);
            var hibouImg = hibouGO.GetComponent<Image>();
            hibouImg.sprite = ChargerHibouBois();
            hibouImg.preserveAspect = true;
            hibouImg.raycastTarget = false;
            if (hibouImg.sprite != null)
                Punch.Scale(this, hibouRect, 1.12f, 0.5f, elastic: true);
            else
                hibouGO.SetActive(false);

            var titreGO = new GameObject("Titre", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            titreGO.transform.SetParent(canvas.transform, false);
            var titreRect = (RectTransform)titreGO.transform;
            titreRect.anchorMin = new Vector2(0.5f, 0.5f);
            titreRect.anchorMax = new Vector2(0.5f, 0.5f);
            titreRect.pivot = new Vector2(0.5f, 0.5f);
            titreRect.sizeDelta = new Vector2(900f, 150f);
            titreRect.anchoredPosition = new Vector2(0f, 478f);
            var titre = titreGO.GetComponent<TextMeshProUGUI>();
            titre.font = gras;
            titre.text = "ZOO LOGIC";
            titre.fontSize = 100;
            titre.fontStyle = FontStyles.Bold;
            titre.alignment = TextAlignmentOptions.Center;
            titre.color = TitreCream;
            titre.outlineWidth = 0.22f;
            titre.outlineColor = ContourBrun;
            titre.raycastTarget = false;
            var ombre = titreGO.AddComponent<Shadow>();
            ombre.effectColor = new Color(0.25f, 0.15f, 0.08f, 0.35f);
            ombre.effectDistance = new Vector2(0f, -6f);
            StartCoroutine(ApparitionTexte(titreRect, 0.1f));

            var sousGO = new GameObject("SousTitre", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            sousGO.transform.SetParent(canvas.transform, false);
            var sousRect = (RectTransform)sousGO.transform;
            sousRect.anchorMin = new Vector2(0.5f, 0.5f);
            sousRect.anchorMax = new Vector2(0.5f, 0.5f);
            sousRect.pivot = new Vector2(0.5f, 0.5f);
            sousRect.sizeDelta = new Vector2(900f, 70f);
            sousRect.anchoredPosition = new Vector2(0f, 345f);
            var sous = sousGO.GetComponent<TextMeshProUGUI>();
            sous.font = gras;
            sous.text = LocalizationManager.Get("menu.subtitle");
            sous.fontSize = 40;
            sous.fontStyle = FontStyles.Bold;
            sous.alignment = TextAlignmentOptions.Center;
            sous.color = TitreCream;
            sous.outlineWidth = 0.15f;
            sous.outlineColor = ContourBrun;
            sous.raycastTarget = false;
            LocalizationManager.ApplyTo(sous);
            StartCoroutine(ApparitionTexte(sousRect, 0.22f));
        }

        private static IEnumerator ApparitionTexte(RectTransform rect, float delai)
        {
            var canvas = rect.GetComponent<CanvasRenderer>();
            if (canvas != null) canvas.SetAlpha(0f);
            Vector2 basePos = rect.anchoredPosition;
            rect.anchoredPosition = basePos + new Vector2(0f, -24f);
            yield return new WaitForSecondsRealtime(delai);
            float duree = 0.4f;
            float elapsed = 0f;
            while (elapsed < duree)
            {
                float t = Easing.EaseOutCubic(Mathf.Clamp01(elapsed / duree));
                if (canvas != null) canvas.SetAlpha(t);
                rect.anchoredPosition = basePos + new Vector2(0f, -24f * (1f - t));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            if (canvas != null) canvas.SetAlpha(1f);
            rect.anchoredPosition = basePos;
        }

        /// <summary>
        /// Vraie barre du kit b.png : b_30 assombrie en piste + b_30 pleine
        /// couleur par-dessus en remplissage horizontal (fillAmount).
        /// Repli procédural si l'atlas est indisponible.
        /// </summary>
        private void ConstruireBarre(Canvas canvas)
        {
            Sprite barre = null;
            try { barre = B1UI.Get("b_30"); } catch { }

            var pisteGO = new GameObject("Piste", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            pisteGO.transform.SetParent(canvas.transform, false);
            var pisteRect = (RectTransform)pisteGO.transform;
            pisteRect.anchorMin = new Vector2(0.5f, 0.5f);
            pisteRect.anchorMax = new Vector2(0.5f, 0.5f);
            pisteRect.pivot = new Vector2(0.5f, 0.5f);
            pisteRect.sizeDelta = new Vector2(620f, 109f);
            pisteRect.anchoredPosition = new Vector2(0f, -700f);
            var pisteImg = pisteGO.GetComponent<Image>();
            if (barre != null)
            {
                pisteImg.sprite = barre;
                pisteImg.type = Image.Type.Simple;
                pisteImg.color = new Color(0.55f, 0.55f, 0.55f, 1f);
            }
            else
            {
                pisteImg.sprite = GridView.SharedRoundedRect;
                pisteImg.type = Image.Type.Sliced;
                pisteImg.color = new Color(0.29f, 0.18f, 0.10f, 0.45f);
            }
            pisteImg.raycastTarget = false;
            var pisteOmbre = pisteGO.AddComponent<Shadow>();
            pisteOmbre.effectColor = new Color(0.25f, 0.15f, 0.08f, 0.35f);
            pisteOmbre.effectDistance = new Vector2(0f, -5f);

            var fillGO = new GameObject("Remplissage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGO.transform.SetParent(pisteGO.transform, false);
            _barreFillRect = (RectTransform)fillGO.transform;
            if (barre != null)
            {
                _barreFillRect.anchorMin = Vector2.zero;
                _barreFillRect.anchorMax = Vector2.one;
                _barreFillRect.offsetMin = Vector2.zero;
                _barreFillRect.offsetMax = Vector2.zero;
            }
            else
            {
                _barreFillRect.anchorMin = new Vector2(0f, 0.5f);
                _barreFillRect.anchorMax = new Vector2(0f, 0.5f);
                _barreFillRect.pivot = new Vector2(0f, 0.5f);
                _barreFillRect.sizeDelta = new Vector2(56f, 101f);
                _barreFillRect.anchoredPosition = new Vector2(4f, 0f);
            }
            var fillImg = fillGO.GetComponent<Image>();
            if (barre != null)
            {
                fillImg.sprite = barre;
                fillImg.type = Image.Type.Filled;
                fillImg.fillMethod = Image.FillMethod.Horizontal;
                fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
                fillImg.fillAmount = 0f;
                fillImg.color = Color.white;
            }
            else
            {
                fillImg.sprite = GridView.SharedRoundedRect;
                fillImg.type = Image.Type.Sliced;
                fillImg.color = new Color(1f, 0.98f, 0.94f, 1f);
            }
            fillImg.raycastTarget = false;
            _barreFillImg = fillImg;
        }

        /// <summary>Médaillon hibou bois sp1_3, repli hibou Art/Animals.</summary>
        private static Sprite ChargerHibouBois()
        {
            try
            {
                Sprite[] tous = Resources.LoadAll<Sprite>("Sprites/sp1");
                if (tous != null)
                {
                    foreach (Sprite s in tous)
                        if (s != null && s.name == "sp1_3")
                            return s;
                }
            }
            catch { }
            try { return Resources.Load<Sprite>("Art/Animals/owl"); }
            catch { return null; }
        }

        /// <summary>Zone tactile invisible : un toucher après le délai termine le splash.</summary>
        private void ConstruireSkip(Canvas canvas)
        {
            var go = new GameObject("Skip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = true;
            var btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                if (Time.unscaledTime - _debut >= DelaiAvantSkip)
                    _skipDemande = true;
            });
        }
    }
}
