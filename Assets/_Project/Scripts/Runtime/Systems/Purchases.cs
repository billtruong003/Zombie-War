using System;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// Rewarded ads by placement ("revive", "double_coins", "shop_deal"). Android/iOS play AdMob
    /// (<see cref="Online.AdService"/>; development builds get Google's test ads). The editor and
    /// development WebGL builds grant the reward straight away so flows can be tested.
    /// </summary>
    public static class RewardedAds
    {
        public static void Show(string placement, Action onReward)
        {
            void Rewarded() { Online.Interstitials.NoteRewardedWatched(); onReward?.Invoke(); }
#if UNITY_EDITOR
            Debug.Log($"[RewardedAds] DEV: simulated ad for {placement}");
            Rewarded();
#elif UNITY_ANDROID || UNITY_IOS
            if (!Online.AdService.ShowRewarded(placement, Rewarded))
                UI.Toast.Show("No ad right now - try again in a moment");
#elif DEVELOPMENT_BUILD
            Debug.Log($"[RewardedAds] DEV: simulated ad for {placement}");
            Rewarded();
#else
            UI.Toast.Show("Ads are not ready yet");
#endif
        }
    }

    /// <summary>
    /// M10 hook for real-money products (premium pass, gem packs, starter pack). The store SDK is
    /// wired in M11; until then a development build simulates a successful purchase so the flows
    /// can be tested, and a release build says the store is not open yet. Nothing is charged here.
    /// </summary>
    public static class Purchases
    {
        public static void Buy(string productId, string priceLabel, Action onSuccess)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Purchases] DEV: simulated purchase of {productId} ({priceLabel})");
            onSuccess?.Invoke();
            UI.Toast.Show("Dev build: purchase simulated");
#else
            UI.Toast.Show("The store opens soon");
#endif
        }
    }
}
