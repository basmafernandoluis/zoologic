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

        public static bool Under5Mode { get; private set; } = true;
        public static bool AreAdsAllowed() => !Under5Mode;

        public void OnAgeBandChosen(bool under5) => ApplyAgeBand(under5);

        public void ApplyAgeBand(bool under5)
        {
            bool wasAllowed = !Under5Mode;
            Under5Mode = under5;
            Debug.Log($"[AdMob] Age band applied Under5={under5} AdsAllowed={!under5}");
            if (!under5 && !wasAllowed) TryInitializeAds();
            if (under5)
            {
                _rewardedAd = null;
                _interstitialAd = null;
                try { _bannerView?.Destroy(); } catch { }
                _bannerView = null;
            }
        }

        private bool _adsInitialized;
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
        private bool _adMusicPaused;

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
            try { Under5Mode = !AgeGateManager.HasChosen || AgeGateManager.IsUnder5; }
            catch { Under5Mode = true; }
            ConfigureAndInitialize();
        }

        private void ConfigureAndInitialize()
        {
            Debug.Log($"[AdMob] Configure IsProduction={IsProduction} AppId={AppId} Banner={BannerId} Rewarded={RewardedId}");
            try
            {
#pragma warning disable CS0618
                var config = new RequestConfiguration
                {
                    TagForChildDirectedTreatment = TagForChildDirectedTreatment.True,
                    TagForUnderAgeOfConsent = TagForUnderAgeOfConsent.True,
                    MaxAdContentRating = MaxAdContentRating.G
                };
                MobileAds.SetRequestConfiguration(config);
#pragma warning restore CS0618
                Debug.Log("[AdMob] RequestConfiguration set: TFCD=True TFA=True MaxRating=G BEFORE Initialize (privacy enfants)");
            }
            catch (Exception e) { Debug.LogWarning("[AdMob] RequestConfiguration failed: " + e.Message); }

            TryInitializeAds();
        }

        private bool _consentResolved;

        private void TryInitializeAds()
        {
            if (Under5Mode)
            {
                Debug.Log("[AdMob] Under-5 mode: SDK init skipped (zero ads for young children)");
                return;
            }
            // GDPR/TTCF : le SDK ne s'initialise qu'après résolution UMP (6+ uniquement).
            if (!_consentResolved)
            {
                Debug.Log("[AdMob] Waiting for UMP consent before init");
                UmpConsent.RequestConsent(() =>
                {
                    _consentResolved = true;
                    TryInitializeAds();
                });
                return;
            }
            if (_adsInitialized) return;
            _adsInitialized = true;
            try
            {
                MobileAds.Initialize(initStatus =>
                {
                    Debug.Log("[AdMob] MobileAds Initialized: " + initStatus);
                    LoadRewarded();
                    LoadInterstitial();
                    // Families: no AppOpen — interstitial on launch is prohibited.
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

        private void PauseMusicForAd()
        {
            try
            {
                if (SFXManager.Instance != null && SFXManager.Instance.IsMusicPlaying)
                {
                    SFXManager.Instance.PauseMusic();
                    _adMusicPaused = true;
                    Debug.Log("[AdMob] Music paused for ad");
                }
                else _adMusicPaused = false;
            }
            catch { _adMusicPaused = false; }
        }

        private void ResumeMusicAfterAd()
        {
            try
            {
                if (_adMusicPaused && SFXManager.Instance != null)
                {
                    SFXManager.Instance.ResumeMusic();
                    Debug.Log("[AdMob] Music resumed after ad");
                }
            }
            catch (Exception e) { Debug.LogWarning("[AdMob] ResumeMusic failed: " + e.Message); }
            _adMusicPaused = false;
        }

        private void LoadRewarded()
        {
            if (Under5Mode) return;
            if (_rewardedLoading) return;
            _rewardedLoading = true;
            var req = CreateNpaRequest();
            RewardedAd.Load(RewardedId, req, (ad, err) =>
            {
                _rewardedLoading = false;
                if (err != null || ad == null) { Debug.LogWarning("[AdMob] Rewarded load failed: " + err); return; }
                _rewardedAd = ad;
                _rewardedAd.OnAdFullScreenContentFailed += (AdError e) => { Debug.LogWarning("[AdMob] Rewarded prefail: " + e); _rewardedAd = null; ResumeMusicAfterAd(); LoadRewarded(); };
                Debug.Log("[AdMob] Rewarded loaded: " + RewardedId + " NPA=1");
            });
        }

        private void LoadInterstitial()
        {
            if (Under5Mode) return;
            if (_interstitialLoading) return;
            _interstitialLoading = true;
            var req = CreateNpaRequest();
            InterstitialAd.Load(InterstitialId, req, (ad, err) =>
            {
                _interstitialLoading = false;
                if (err != null || ad == null) { Debug.LogWarning("[AdMob] Interstitial load failed: " + err); return; }
                _interstitialAd = ad;
                _interstitialAd.OnAdFullScreenContentFailed += (AdError e) => { Debug.LogWarning("[AdMob] Interstitial prefail: " + e); _interstitialAd = null; ResumeMusicAfterAd(); LoadInterstitial(); };
                Debug.Log("[AdMob] Interstitial loaded: " + InterstitialId + " NPA=1");
            });
        }

        private void LoadAppOpen()
        {
            // Families: disabled — AppOpen at launch = policy violation.
            return;
        }

        public void ShowRewarded(Action onRewarded, Action onClosedNoReward = null)
        {
            if (Under5Mode)
            {
                Debug.Log("[AdMob] ShowRewarded blocked: under-5 mode (zero ads)");
                try { onClosedNoReward?.Invoke(); } catch { }
                return;
            }
            Debug.Log($"[AdMob] ShowRewarded IsProduction={IsProduction} ID={RewardedId} NPA=1");
            if (_rewardedAd != null && _rewardedAd.CanShowAd())
            {
                PauseMusicForAd();
                var ad = _rewardedAd;
                _rewardedAd = null;
                bool rewardEarned = false;
                bool settled = false;
                Action<AdValue> paidHandler = (AdValue v) => Debug.Log($"[AdMob] OnAdPaid {v.Value} {v.CurrencyCode}");
                Action closedHandler = null;
                Action<AdError> failedHandler = null;
                Action unsubscribe = () =>
                {
                    try
                    {
                        ad.OnAdPaid -= paidHandler;
                        ad.OnAdFullScreenContentClosed -= closedHandler;
                        ad.OnAdFullScreenContentFailed -= failedHandler;
                    }
                    catch { }
                };
                closedHandler = () =>
                {
                    if (settled) return;
                    settled = true;
                    unsubscribe();
                    StartCoroutine(RewardedCloseSequence(rewardEarned, onRewarded, onClosedNoReward));
                    LoadRewarded();
                };
                failedHandler = (AdError e) =>
                {
                    if (settled) return;
                    settled = true;
                    unsubscribe();
                    Debug.LogWarning("[AdMob] Rewarded show failed: " + e);
                    StartCoroutine(RewardedCloseSequence(false, null, onClosedNoReward));
                    LoadRewarded();
                };
                ad.OnAdPaid += paidHandler;
                ad.OnAdFullScreenContentClosed += closedHandler;
                ad.OnAdFullScreenContentFailed += failedHandler;
                try
                {
                    ad.Show((Reward r) =>
                    {
                        rewardEarned = true;
                        Debug.Log($"[AdMob] Reward earned {r.Amount} {r.Type}");
                    });
                    return;
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[AdMob] Show exception: " + e.Message);
                    if (settled) return;
                    settled = true;
                    unsubscribe();
                    ResumeMusicAfterAd();
                }
            }
            Debug.LogWarning("[AdMob] Rewarded not ready -> no grant (Families: no fake ad)");
            ResumeMusicAfterAd();
            LoadRewarded();
            try { onClosedNoReward?.Invoke(); } catch (Exception e) { Debug.LogError("[AdMob] onClosedNoReward exception: " + e); }
            return;
        }

        private IEnumerator RewardedCloseSequence(bool earned, Action onRewarded, Action onClosedNoReward)
        {
            yield return null;
            yield return null;
            ResumeMusicAfterAd();
            if (earned)
            {
                try { onRewarded?.Invoke(); } catch (Exception e) { Debug.LogError("[AdMob] onRewarded exception: " + e); }
            }
            else
            {
                Debug.Log("[AdMob] Rewarded closed without reward - no grant");
                try { onClosedNoReward?.Invoke(); } catch (Exception e) { Debug.LogError("[AdMob] onClosedNoReward exception: " + e); }
            }
        }

        public bool IsRewardedReady() => !Under5Mode && _rewardedAd != null && _rewardedAd.CanShowAd();

        public void ShowInterstitialIfNeeded()
        {
            _victoryCount++;
            if (Under5Mode) return;
            if (_victoryCount % 4 != 0) return;
            Debug.Log($"[AdMob] Interstitial trigger 4th victory IsProduction={IsProduction} ID={InterstitialId} NPA=1");
            if (_interstitialAd != null && _interstitialAd.CanShowAd())
            {
                PauseMusicForAd();
                Action closedHandler = null;
                Action<AdError> failedHandler = null;
                closedHandler = () =>
                {
                    try
                    {
                        if (_interstitialAd != null)
                        {
                            _interstitialAd.OnAdFullScreenContentClosed -= closedHandler;
                            _interstitialAd.OnAdFullScreenContentFailed -= failedHandler;
                        }
                    }
                    catch { }
                    ResumeMusicAfterAd();
                    _interstitialAd = null;
                    LoadInterstitial();
                };
                failedHandler = (AdError e) =>
                {
                    try
                    {
                        if (_interstitialAd != null)
                        {
                            _interstitialAd.OnAdFullScreenContentClosed -= closedHandler;
                            _interstitialAd.OnAdFullScreenContentFailed -= failedHandler;
                        }
                    }
                    catch { }
                    Debug.LogWarning("[AdMob] Interstitial failed: " + e);
                    ResumeMusicAfterAd();
                    _interstitialAd = null;
                    LoadInterstitial();
                };
                _interstitialAd.OnAdFullScreenContentClosed += closedHandler;
                _interstitialAd.OnAdFullScreenContentFailed += failedHandler;
                try { _interstitialAd.Show(); return; } catch (Exception e) { Debug.LogWarning("[AdMob] Interstitial show failed: " + e.Message); ResumeMusicAfterAd(); }
                try
                {
                    _interstitialAd.OnAdFullScreenContentClosed -= closedHandler;
                    _interstitialAd.OnAdFullScreenContentFailed -= failedHandler;
                }
                catch { }
            }
            LoadInterstitial();
        }

        public void ShowBanner()
        {
            if (Under5Mode) return;
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
            // Families: no custom stub banner — unlabeled house banner = "unclear ads" rejection.
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
            // Families: disabled — never show ad immediately on launch.
            return;
        }
    }
}
