using System;
using System.Collections;
using UnityEngine;

namespace Zoologic
{
    public sealed class AdMobManager : MonoBehaviour
    {
        public static AdMobManager Instance { get; private set; }

        private const string TestAppId = "ca-app-pub-3940256099942544~3347511713";
        private const string RewardedId = "ca-app-pub-3940256099942544/5224354917";
        private const string InterstitialId = "ca-app-pub-3940256099942544/1033173712";
        private const string BannerId = "ca-app-pub-3940256099942544/6300978111";

        private int _victoryCount;
        private GameObject _bannerGO;

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
            Debug.Log("[AdMob] Manager ready (stub, test IDs). AppId=" + TestAppId);
#if GOOGLE_MOBILE_ADS
            try { GoogleMobileAds.Api.MobileAds.Initialize(init => Debug.Log("[AdMob] Initialized: " + init)); }
            catch (Exception e) { Debug.LogWarning("[AdMob] Init failed (stub fallback): " + e.Message); }
#endif
        }

        public void ShowRewarded(Action onRewarded)
        {
            Debug.Log("[AdMob] ShowRewarded (stub) - granting reward");
#if GOOGLE_MOBILE_ADS
            // Real implementation would load and show rewarded ad here.
            // For now, fallback to stub if SDK not ready.
            try
            {
                // Attempt to show real ad if available, otherwise fallback
                // This stub will be replaced when SDK is fully integrated
                StartCoroutine(RewardedStubRoutine(onRewarded));
                return;
            }
            catch { }
#endif
            StartCoroutine(RewardedStubRoutine(onRewarded));
        }

        private IEnumerator RewardedStubRoutine(Action onRewarded)
        {
            yield return new WaitForSecondsRealtime(0.4f);
            onRewarded?.Invoke();
        }

        public void ShowInterstitialIfNeeded()
        {
            _victoryCount++;
            if (_victoryCount % 4 != 0) return;
            Debug.Log("[AdMob] Interstitial check (stub) - would show on every 4th victory");
#if GOOGLE_MOBILE_ADS
            // Real interstitial show logic here
#endif
        }

        public void ShowBanner()
        {
            if (_bannerGO != null) return;
            var canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;
            _bannerGO = new GameObject("AdBannerStub", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
            _bannerGO.transform.SetParent(canvas.transform, false);
            var rect = _bannerGO.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(0f, 90f);
            rect.anchoredPosition = Vector2.zero;
            var img = _bannerGO.GetComponent<UnityEngine.UI.Image>();
            img.color = new Color(0.92f, 0.89f, 0.86f, 1f);
            img.raycastTarget = false;
            var txtGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
            txtGO.transform.SetParent(_bannerGO.transform, false);
            var txtRect = txtGO.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero; txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = Vector2.zero; txtRect.offsetMax = Vector2.zero;
            var txt = txtGO.GetComponent<TMPro.TextMeshProUGUI>();
            txt.font = Resources.Load<TMPro.TMP_FontAsset>("Fonts/Fredoka/Fredoka-Regular SDF");
            txt.text = "Publicité — bannière (stub)";
            txt.fontSize = 20;
            txt.color = new Color(0.50f, 0.42f, 0.35f);
            txt.alignment = TMPro.TextAlignmentOptions.Center;
        }

        public void HideBanner()
        {
            if (_bannerGO != null) Destroy(_bannerGO);
            _bannerGO = null;
        }
    }
}
