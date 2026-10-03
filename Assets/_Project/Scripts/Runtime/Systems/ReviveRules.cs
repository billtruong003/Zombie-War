using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// M10 revive rules (owner decisions 2026-09-27): at most <see cref="MaxRevives"/> per run; one
    /// may be a free rewarded ad; the rest cost Coin from the wallet. The first coin revive costs
    /// 60% of the coins carried in this run and each further one doubles, so two coin revives
    /// always cost more than the run carries (0.6 + 1.2 = 1.8x).
    /// </summary>
    public static class ReviveRules
    {
        public const int MaxRevives = 3, MinCoinCost = 100, OfferSeconds = 7;
        public const float FirstCostShare = 0.6f;

        static RunState _run;
        static int _used, _coinUsed;
        static bool _adUsed;

        static void Sync()
        {
            if (ReferenceEquals(_run, RunState.Current)) return;
            _run = RunState.Current; _used = 0; _coinUsed = 0; _adUsed = false;
        }

        public static int Used { get { Sync(); return _used; } }
        public static bool AdAvailable { get { Sync(); return !_adUsed; } }
        public static bool CanOffer { get { Sync(); return _run != null && !_run.IsOver && _used < MaxRevives; } }
        public static long Carried => RunState.Current != null ? RunState.Current.Coin : 0;

        /// <summary>Coin price of the next coin revive for a run carrying <paramref name="carried"/>.</summary>
        public static long CoinCost(long carried, int coinRevivesUsed) =>
            System.Math.Max(MinCoinCost, (long)Mathf.Round(Mathf.Max(0, carried) * FirstCostShare * (1 << Mathf.Clamp(coinRevivesUsed, 0, 10))));

        public static long NextCoinCost { get { Sync(); return CoinCost(Carried, _coinUsed); } }

        public static void UseAd() { Sync(); _adUsed = true; _used++; }

        /// <summary>FTUE v2: the first revive ever is free. It counts toward the three revives of the
        /// run but leaves the ad revive available.</summary>
        public static void UseFree() { Sync(); _used++; }

        public static bool TryPayCoin()
        {
            Sync();
            if (!PlayerProfile.TrySpend(PlayerProfile.CurrencyKind.Coin, NextCoinCost)) return false;
            _coinUsed++; _used++;
            return true;
        }

        /// <summary>Test seam: forget the per-run counters.</summary>
        internal static void ResetForTests() { _run = null; _used = 0; _coinUsed = 0; _adUsed = false; }
    }
}
