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
            void Rewarded()
            {
                Online.Interstitials.NoteRewardedWatched();
                Online.GameAnalytics.Log("ad_rewarded_reward", ("placement", placement));
                onReward?.Invoke();
            }
#if UNITY_EDITOR
            Debug.Log($"[RewardedAds] DEV: simulated ad for {placement}");
            Rewarded();
#elif UNITY_ANDROID || UNITY_IOS
            if (!Online.AdService.ShowRewarded(placement, Rewarded))
            {
                Online.GameAnalytics.Log("ad_rewarded_unavailable", ("placement", placement));
                UI.Toast.Show("No ad right now - try again in a moment");
            }
#elif DEVELOPMENT_BUILD
            Debug.Log($"[RewardedAds] DEV: simulated ad for {placement}");
            Rewarded();
#else
            UI.Toast.Show("Ads are not ready yet");
#endif
        }
    }

    /// <summary>
    /// Real-money products (premium pass, gem packs, starter pack, no ads, legend pack). On Android
    /// they go through Google Play (<see cref="Online.IapStore"/>): the item is granted by
    /// <see cref="Grant"/> once the purchase is checked, also when it comes back after a restart or
    /// a reinstall; <c>onSuccess</c> only refreshes the screen. The editor (and development builds
    /// without a store) simulate a successful purchase so the flows can be tested.
    /// </summary>
    public static class Purchases
    {
        public static event Action<string> Granted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Granted = null;

        public static void Buy(string productId, string priceLabel, Action onSuccess)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Online.IapStore.Buy(productId, ok => { if (ok) onSuccess?.Invoke(); })) return;
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Purchases] DEV: simulated purchase of {productId} ({priceLabel})");
            Grant(productId);
            onSuccess?.Invoke();
            UI.Toast.Show("Dev build: purchase simulated");
#else
            UI.Toast.Show("The store is not available right now");
#endif
        }

        /// <summary>Gives what a product contains. Safe to call twice for owned-for-good items;
        /// gem packs are guarded by the purchase token upstream.</summary>
        public static bool Grant(string productId)
        {
            bool ok;
            if (productId == PassRewards.PremiumProductId) { PassRewards.UnlockPremium(); ok = true; }
            else ok = ShopOffers.GrantPack(ShopOffers.FindPack(productId));
            if (ok) Granted?.Invoke(productId);
            return ok;
        }
    }
}
