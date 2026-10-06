#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
#define HC_ADMOB
#endif
using System;
using System.Collections.Generic;
using UnityEngine;
#if HC_ADMOB
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
#endif

namespace ZombieWar.Online
{
    /// <summary>
    /// AdMob (account billtruong004, app "HordeCall"): Google's consent form (UMP) first, then the
    /// SDK, then one rewarded ad kept loaded per placement and one interstitial. Development builds
    /// use Google's public test units (real units must never be clicked while testing); the editor
    /// and WebGL grant rewards straight away (<see cref="RewardedAds"/>).
    /// </summary>
    public static class AdService
    {
        // Ids from Config/admob/admob_ids.json. Ad unit ids are public by design.
        const string RewardedRevive = "ca-app-pub-2681948403948920/2434480191";
        const string RewardedDoubleCoin = "ca-app-pub-2681948403948920/1284215909";
        const string RewardedDeal = "ca-app-pub-2681948403948920/9015455554";
        const string InterstitialLongRun = "ca-app-pub-2681948403948920/9238816834";
        const string TestRewarded = "ca-app-pub-3940256099942544/5224354917";
        const string TestInterstitial = "ca-app-pub-3940256099942544/1033173712";

#if DEVELOPMENT_BUILD
        static string Unit(string real, string test) => test;
#else
        static string Unit(string real, string test) => real;
#endif

        public static string RewardedUnitFor(string placement) => placement switch
        {
            "revive" => Unit(RewardedRevive, TestRewarded),
            "shop_deal" => Unit(RewardedDeal, TestRewarded),
            _ => Unit(RewardedDoubleCoin, TestRewarded),   // "double_coins" and anything new
        };

        public static string InterstitialUnit => Unit(InterstitialLongRun, TestInterstitial);

        public static bool Ready { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Ready = false;

#if HC_ADMOB
        static readonly Dictionary<string, RewardedAd> Rewarded = new();
        static InterstitialAd _interstitial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            // Not aimed at children (SDK decision 28/09); consent asked only where the law needs it.
            ConsentInformation.Update(new ConsentRequestParameters { TagForUnderAgeOfConsent = false }, error =>
            {
                if (error != null) { Debug.LogWarning("[Ads] Consent info: " + error.Message); StartSdk(); return; }
                ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                {
                    if (formError != null) Debug.LogWarning("[Ads] Consent form: " + formError.Message);
                    StartSdk();
                });
            });
        }

        /// <summary>Whether the settings screen must offer "Privacy options" (UMP requirement).</summary>
        public static bool PrivacyOptionsRequired =>
            ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

        public static void ShowPrivacyOptions(Action closed = null) =>
            ConsentForm.ShowPrivacyOptionsForm(_ => closed?.Invoke());

        static void StartSdk()
        {
            if (Ready || !ConsentInformation.CanRequestAds()) return;
            MobileAds.Initialize(_ =>
            {
                Ready = true;
                foreach (var p in new[] { "revive", "double_coins", "shop_deal" }) LoadRewarded(RewardedUnitFor(p));
                LoadInterstitial();
            });
        }

        static void LoadRewarded(string unit)
        {
            if (Rewarded.TryGetValue(unit, out var old) && old != null && old.CanShowAd()) return;
            RewardedAd.Load(unit, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null) { Debug.LogWarning("[Ads] Rewarded load: " + error?.GetMessage()); return; }
                Rewarded[unit] = ad;
            });
        }

        static void LoadInterstitial()
        {
            InterstitialAd.Load(InterstitialUnit, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null) { Debug.LogWarning("[Ads] Interstitial load: " + error?.GetMessage()); return; }
                _interstitial = ad;
            });
        }

        /// <summary>Shows the placement's rewarded ad; <paramref name="onReward"/> runs after the ad
        /// closes, and only when it was watched to the reward. False when no ad is loaded.</summary>
        public static bool ShowRewarded(string placement, Action onReward)
        {
            string unit = RewardedUnitFor(placement);
            if (!RemoteConfig.AdsOn) return false;
            if (!Ready || !Rewarded.TryGetValue(unit, out var ad) || ad == null || !ad.CanShowAd())
            {
                if (Ready) LoadRewarded(unit);
                return false;
            }
            Rewarded.Remove(unit);
            bool earned = false;
            ad.OnAdFullScreenContentClosed += () =>
            {
                ad.Destroy();
                LoadRewarded(unit);
                if (earned) onReward?.Invoke();
            };
            ad.OnAdFullScreenContentFailed += _ => { ad.Destroy(); LoadRewarded(unit); };
            ad.Show(_ => earned = true);
            GameAnalytics.Log("ad_rewarded_show", ("placement", placement));
            return true;
        }

        public static bool ShowInterstitial(Action closed)
        {
            var ad = _interstitial;
            if (!Ready || ad == null || !ad.CanShowAd()) { if (Ready && ad == null) LoadInterstitial(); return false; }
            _interstitial = null;
            ad.OnAdFullScreenContentClosed += () => { ad.Destroy(); LoadInterstitial(); closed?.Invoke(); };
            ad.OnAdFullScreenContentFailed += _ => { ad.Destroy(); LoadInterstitial(); closed?.Invoke(); };
            ad.Show();
            GameAnalytics.Log("ad_interstitial_show");
            return true;
        }
#else
        public static bool PrivacyOptionsRequired => false;
        public static void ShowPrivacyOptions(Action closed = null) => closed?.Invoke();
        public static bool ShowRewarded(string placement, Action onReward) => false;
        public static bool ShowInterstitial(Action closed) => false;
#endif
    }

    /// <summary>
    /// When an interstitial may play: only when the player leaves the result screen of a long run
    /// (SDK decision 28/09), never in the first runs of an install, never right after a rewarded
    /// ad on the same result, and at most once every few minutes.
    /// </summary>
    public static class Interstitials
    {
        public const float LongRunSeconds = 300f;
        public const int SkipFirstRuns = 3;
        public const float MinGapSeconds = 180f;
        const string RunsKey = "hc.ads.runs";

        static float _lastRunSeconds;
        static bool _rewardedThisResult;
        static float _lastShownAt = -9999f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _lastRunSeconds = 0f; _rewardedThisResult = false; _lastShownAt = -9999f; }

        public static void NoteRunEnded(float seconds)
        {
            _lastRunSeconds = seconds;
            _rewardedThisResult = false;
            PlayerPrefs.SetInt(RunsKey, PlayerPrefs.GetInt(RunsKey, 0) + 1);
        }

        public static void NoteRewardedWatched() => _rewardedThisResult = true;

        public static bool Due(float now, int runsSoFar) =>
            _lastRunSeconds >= LongRunSeconds && !_rewardedThisResult
            && runsSoFar > SkipFirstRuns && now - _lastShownAt >= MinGapSeconds;

        /// <summary>Runs <paramref name="go"/> after the interstitial when one is due and loaded,
        /// otherwise straight away.</summary>
        public static void ThenGo(Action go)
        {
            float now = Time.realtimeSinceStartup;
            if (!RemoteConfig.AdsOn || !RemoteConfig.InterstitialOn) { go(); return; }
            if (!Due(now, PlayerPrefs.GetInt(RunsKey, 0))) { go(); return; }
            _lastRunSeconds = 0f;
            if (AdService.ShowInterstitial(go)) _lastShownAt = now;
            else go();
        }
    }
}
