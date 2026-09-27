using System;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// M10 hook for rewarded ads (AppLovin MAX + AdMob land in M11). Development builds grant the
    /// reward straight away so flows can be tested; release builds say ads are not ready.
    /// </summary>
    public static class RewardedAds
    {
        public static void Show(string placement, Action onReward)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[RewardedAds] DEV: simulated ad for {placement}");
            onReward?.Invoke();
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
