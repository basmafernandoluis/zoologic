using System;
using System.Collections;
using UnityEngine;
using GoogleMobileAds.Api;

namespace Zoologic
{
    public sealed class AdMobManager : MonoBehaviour
    {
        public static AdMobManager Instance { get; private set; }

        private const string ProdAppId = "ca-app-pub-7435856398879419~7807957345";
        private const string ProdBannerId = "ca-app-pub-7435856398879419/6435927669";
        private const string ProdInterstitialId = "ca-app-pub-7435856398879419/7300135970";
        private const string ProdRewardedInterstitialId = "ca-app-pub-7435856398879419/2351061627";
        private const string ProdAppOpenId = "ca-app-pub-7435856398879419/8827016238";
        private const string ProdRewardedId = "ca-app-pub-7435856398879419/3360890967";

        private const string TestAppId = "ca-app-pub-3940256099942544~3347511713";
        private const string TestBannerId = "ca-app-pub-3940256099942544/6300978111";
        private const string TestInterstitialId = "ca-app-pub-3940256099942544/1033173712";
        private const string TestRewardedInterstitialId = "ca-app-pub-3940256099942544/5354046379";
        private const string TestAppOpenId = "ca-app-pub-3940256099942544/3419835294";
        private const string TestRewardedId = "ca-app-pub-3940256099942544/5224354917";

        private static bool IsProduction
        {
            get
            {
#if ADMOB_TEST
                return false;
#elif UNITY_EDITOR
                return false;
#else
                return !Debug.isDebugBuild;
#endif
            }
        }

        public static string AppId => IsProduction ? ProdAppId : TestAppId;
        public static string BannerId => IsProduction ? ProdBannerId : TestBannerId;
        public static string InterstitialId => IsProduction ? ProdInterstitialId : TestInterstitialId;
        public static string RewardedInterstitialId => IsProduction ? ProdRewardedInterstitialId : TestRewardedInterstitialId;
        public static string AppOpenId => IsProduction ? ProdAppOpenId : TestAppOpenId;
        public static string RewardedId => IsProduction ? ProdRewardedId : TestRewardedId;

        private int _victoryCount;
        private GameObject _bannerGO;

        private RewardedAd _rewardedAd;
        private InterstitialAd _interstitialAd;
        private BannerView _bannerView;
        private AppOpenAd _appOpenAd;
        private bool _rewardedLoading;
        private bool _interstitialLoading;
        private bool _appOpenLoading;
        private DateTime _appOpenExpire;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("AdMobManager");
            go.AddComponent<AdMobManager>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            ConfigureAndInitialize();
        }

        private void ConfigureAndInitialize()
        {
            Debug.Log($"[AdMob] Configure IsProduction={IsProduction} AppId={AppId} Banner={BannerId} Rewarded={RewardedId}");
            try
            {
                var config = new RequestConfiguration
                {
                    TagForChildDirectedTreatment = TagForChildDirectedTreatment.True,
                    TagForUnderAgeOfConsent = TagForUnderAgeOfConsent.True,
                    MaxAdContentRating = MaxAdContentRating.G
                };
                MobileAds.SetRequestConfiguration(config);
                Debug.Log("[AdMob] RequestConfiguration set: TFCD=True TFA=True MaxRating=G BEFORE Initialize (privacy enfants)");
            }
            catch (Exception e) { Debug.LogWarning("[AdMob] RequestConfiguration failed: " + e.Message); }

            try
            {
                MobileAds.Initialize(initStatus =>
                {
                    Debug.Log("[AdMob] MobileAds Initialized: " + initStatus);
                    LoadRewarded();
                    LoadInterstitial();
                    LoadAppOpen();
                });
            }
            catch (Exception e) { Debug.LogWarning("[AdMob] Initialize failed: " + e.Message); }
        }

        private AdRequest CreateNpaRequest()
        {
            var req = new AdRequest();
            try { req.Extras.Add("npa", "1"); } catch { }
            return req;
        }

        private void LoadRewarded()
        {
            if (_rewardedLoading) return;
            _rewardedLoading = true;
            var req = CreateNpaRequest();
            RewardedAd.Load(RewardedId, req, (ad, err) =>
            {
                _rewardedLoading = false;
                if (err != null || ad == null) { Debug.LogWarning("[AdMob] Rewarded load failed: " + err); return; }
                _rewardedAd = ad;
                _rewardedAd.OnAdFullScreenContentFailed += (AdError e) => { _rewardedAd = null; LoadRewarded(); };
                _rewardedAd.OnAdFullScreenContentClosed += () => { _rewardedAd = null; LoadRewarded(); };
                Debug.Log("[AdMob] Rewarded loaded: " + RewardedId + " NPA=1");
            });
        }

        private void LoadInterstitial()
        {
            if (_interstitialLoading) return;
            _interstitialLoading = true;
            var req = CreateNpaRequest();
            InterstitialAd.Load(InterstitialId, req, (ad, err) =>
            {
                _interstitialLoading = false;
                if (err != null || ad == null) { Debug.LogWarning("[AdMob] Interstitial load failed: " + err); return; }
                _interstitialAd = ad;
                _interstitialAd.OnAdFullScreenContentFailed += (AdError e) => { _interstitialAd = null; LoadInterstitial(); };
                _interstitialAd.OnAdFullScreenContentClosed += () => { _interstitialAd = null; LoadInterstitial(); };
                Debug.Log("[AdMob] Interstitial loaded: " + InterstitialId + " NPA=1");
            });
        }

        private void LoadAppOpen()
        {
            if (_appOpenLoading) return;
            _appOpenLoading = true;
            var req = CreateNpaRequest();
            AppOpenAd.Load(AppOpenId, req, (ad, err) =>
            {
                _appOpenLoading = false;
                if (err != null || ad == null) { Debug.LogWarning("[AdMob] AppOpen load failed: " + err); return; }
                _appOpenAd = ad;
                _appOpenExpire = DateTime.Now.AddHours(4);
                _appOpenAd.OnAdFullScreenContentFailed += (AdError e) => { _appOpenAd = null; LoadAppOpen(); };
                _appOpenAd.OnAdFullScreenContentClosed += () => { _appOpenAd = null; LoadAppOpen(); };
                Debug.Log("[AdMob] AppOpen loaded: " + AppOpenId + " NPA=1");
            });
        }

        public void ShowRewarded(Action onRewarded)
        {
            Debug.Log($"[AdMob] ShowRewarded IsProduction={IsProduction} ID={RewardedId} NPA=1");
            if (_rewardedAd != null && _rewardedAd.CanShowAd())
            {
                _rewardedAd.OnAdPaid += (AdValue v) => Debug.Log($"[AdMob] OnAdPaid {v.Value} {v.CurrencyCode}");
                _rewardedAd.OnAdFullScreenContentClosed += () => { onRewarded?.Invoke(); _rewardedAd = null; LoadRewarded(); };
                _rewardedAd.OnAdFullScreenContentFailed += (AdError e) => { Debug.LogWarning("[AdMob] Rewarded show failed: " + e); StartCoroutine(RewardedStubRoutine(onRewarded)); _rewardedAd = null; LoadRewarded(); };
                try { _rewardedAd.Show((Reward r) => { Debug.Log($"[AdMob] Reward earned {r.Amount} {r.Type}"); }); return; } catch (Exception e) { Debug.LogWarning("[AdMob] Show exception: " + e.Message); }
            }
            Debug.LogWarning("[AdMob] Rewarded not ready (encore en chargement) -> fallback simulé 1s, vraie test ad prête après 2-3s");
            LoadRewarded();
            StartCoroutine(RewardedStubRoutine(onRewarded));
        }

        private IEnumerator RewardedStubRoutine(Action onRewarded)
        {
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                var overlay = new GameObject("TestAdOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                overlay.transform.SetParent(canvas.transform, false);
                var rt = overlay.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                var img = overlay.GetComponent<UnityEngine.UI.Image>();
                img.color = new Color(0f, 0f, 0f, 0.85f); img.raycastTarget = true;
                var card = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                card.transform.SetParent(overlay.transform, false);
                var cr = card.GetComponent<RectTransform>();
                cr.anchorMin = new Vector2(0.5f, 0.5f); cr.anchorMax = new Vector2(0.5f, 0.5f); cr.pivot = new Vector2(0.5f, 0.5f);
                cr.sizeDelta = new Vector2(560f, 360f); cr.anchoredPosition = Vector2.zero;
                var ci = card.GetComponent<UnityEngine.UI.Image>();
                ci.sprite = null; ci.color = Color.white;
                var vlg = card.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
                vlg.padding = new UnityEngine.RectOffset(24, 24, 24, 24); vlg.spacing = 18f; vlg.childAlignment = TextAnchor.MiddleCenter;
                var titleGO = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
                titleGO.transform.SetParent(card.transform, false);
                var title = titleGO.GetComponent<TMPro.TextMeshProUGUI>();
                title.font = Resources.Load<TMPro.TMP_FontAsset>("Fonts/Fredoka/Fredoka-Bold SDF");
                title.text = IsProduction ? "Publicité" : "Publicité test (simulée)";
                title.fontSize = 30; title.fontStyle = TMPro.FontStyles.Bold; title.color = new Color(0.20f, 0.13f, 0.08f);
                title.alignment = TMPro.TextAlignmentOptions.Center;
                var subGO = new GameObject("Sub", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
                subGO.transform.SetParent(card.transform, false);
                var sub = subGO.GetComponent<TMPro.TextMeshProUGUI>();
                sub.font = Resources.Load<TMPro.TMP_FontAsset>("Fonts/Fredoka/Fredoka-Regular SDF");
                sub.text = "Récompense sera accordée dans 1s…";
                sub.fontSize = 20; sub.color = new Color(0.45f, 0.38f, 0.32f); sub.alignment = TMPro.TextAlignmentOptions.Center;
                yield return new WaitForSecondsRealtime(1f);
                if (overlay != null) Destroy(overlay);
            }
            else yield return new WaitForSecondsRealtime(0.4f);
            onRewarded?.Invoke();
        }

        public void ShowInterstitialIfNeeded()
        {
            _victoryCount++;
            if (_victoryCount % 4 != 0) return;
            Debug.Log($"[AdMob] Interstitial trigger 4th victory IsProduction={IsProduction} ID={InterstitialId} NPA=1");
            if (_interstitialAd != null && _interstitialAd.CanShowAd())
            {
                _interstitialAd.OnAdFullScreenContentClosed += () => { _interstitialAd = null; LoadInterstitial(); };
                _interstitialAd.OnAdFullScreenContentFailed += (AdError e) => { _interstitialAd = null; LoadInterstitial(); };
                try { _interstitialAd.Show(); return; } catch (Exception e) { Debug.LogWarning("[AdMob] Interstitial show failed: " + e.Message); }
            }
            LoadInterstitial();
        }

        public void ShowBanner()
        {
            Debug.Log($"[AdMob] ShowBanner IsProduction={IsProduction} ID={BannerId} NPA=1");
            try
            {
                if (_bannerView != null) return;
                _bannerView = new BannerView(BannerId, AdSize.Banner, AdPosition.Bottom);
                var req = CreateNpaRequest();
                _bannerView.LoadAd(req);
                Debug.Log($"[AdMob] Banner load NPA ID={BannerId}");
                return;
            }
            catch (Exception e) { Debug.LogWarning("[AdMob] Banner failed: " + e.Message); }
            if (_bannerGO != null) return;
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            _bannerGO = new GameObject("AdBannerStub", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            _bannerGO.transform.SetParent(canvas.transform, false);
            var rect = _bannerGO.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f); rect.anchorMax = new Vector2(1f, 0f); rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(0f, 90f); rect.anchoredPosition = Vector2.zero;
            var img = _bannerGO.GetComponent<UnityEngine.UI.Image>();
            img.color = new Color(0.92f, 0.89f, 0.86f, 1f); img.raycastTarget = false;
            var txtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
            txtGO.transform.SetParent(_bannerGO.transform, false);
            var txtRect = txtGO.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero; txtRect.anchorMax = Vector2.one; txtRect.offsetMin = Vector2.zero; txtRect.offsetMax = Vector2.zero;
            var txt = txtGO.GetComponent<TMPro.TextMeshProUGUI>();
            txt.font = Resources.Load<TMPro.TMP_FontAsset>("Fonts/Fredoka/Fredoka-Regular SDF");
            txt.text = IsProduction ? "Publicité — bannière" : "Publicité — bannière (test)";
            txt.fontSize = 20; txt.color = new Color(0.50f, 0.42f, 0.35f); txt.alignment = TMPro.TextAlignmentOptions.Center;
        }

        public void HideBanner()
        {
            try { _bannerView?.Destroy(); } catch { }
            _bannerView = null;
            if (_bannerGO != null) Destroy(_bannerGO);
            _bannerGO = null;
        }

        public void ShowAppOpenIfNeeded()
        {
            if (_appOpenAd == null || !_appOpenAd.CanShowAd() || DateTime.Now > _appOpenExpire) { LoadAppOpen(); return; }
            try { _appOpenAd.Show(); Debug.Log("[AdMob] AppOpen shown NPA"); } catch (Exception e) { Debug.LogWarning("[AdMob] AppOpen show failed: " + e.Message); _appOpenAd = null; LoadAppOpen(); }
        }
    }
}
