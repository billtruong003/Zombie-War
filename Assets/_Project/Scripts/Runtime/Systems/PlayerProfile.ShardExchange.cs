using System.Collections.Generic;

namespace ZombieWar
{
    /// PlayerProfile, part: shards of a maxed gun turn into coin (owner 05/10, backlog 3a / Q2).
    public static partial class PlayerProfile
    {
        /// <summary>Coin per spare shard by rarity (Common..Legendary): about half what a shard is worth
        /// toward buying the gun, so hoarding shards for coin never beats using them.</summary>
        public static readonly int[] CoinPerSpareShard = { 10, 15, 20, 25, 40 };

        public const int MaxStars = 3;

        /// <summary>Turns every shard of a 3-star gun into coin. Returns (shards, coin) exchanged; zero
        /// when nothing was spare. Called where it is safe to show a toast (Home, Arsenal), never from
        /// the shard grant paths themselves.</summary>
        public static (int shards, long coin) ExchangeMaxedShards(IReadOnlyList<WeaponData> guns)
        {
            if (guns == null) return (0, 0);
            int shards = 0; long coin = 0;
            for (int i = 0; i < guns.Count; i++)
            {
                var g = guns[i];
                if (g == null || string.IsNullOrEmpty(g.WeaponId) || GetWeaponLevel(g.WeaponId) < MaxStars) continue;
                int have = GetWeaponShards(g.WeaponId);
                if (have <= 0) continue;
                for (int k = 0; k < Data.weaponShards.Count; k++)
                    if (Data.weaponShards[k].weaponId == g.WeaponId) { var e = Data.weaponShards[k]; e.count = 0; Data.weaponShards[k] = e; break; }
                shards += have;
                coin += (long)have * CoinPerSpareShard[UnityEngine.Mathf.Clamp((int)g.tier, 0, CoinPerSpareShard.Length - 1)];
            }
            if (coin > 0) { Add(CurrencyKind.Coin, coin); SaveNow(); Notify(Change.Loadout); }
            return (shards, coin);
        }
    }
}
