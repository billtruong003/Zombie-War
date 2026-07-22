using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// Logic gacha THUAN — RNG inject duoc (test deterministic). Pool tham chieu item ID co san
    /// (weapon: WeaponData; costume: EconomyConfig costume entry source Gacha/ShopAndGacha).
    /// Starter/Disabled/assembly bi loai. Pity + dupe compensation + commit atomic qua PlayerProfile.
    /// </summary>
    public static class GachaService
    {
        public interface IRng { int Range(int maxExclusive); }

        public sealed class SystemRng : IRng
        {
            private readonly System.Random _r;
            public SystemRng(int seed) => _r = new System.Random(seed);
            public SystemRng() => _r = new System.Random();
            public int Range(int maxExclusive) => maxExclusive <= 0 ? 0 : _r.Next(maxExclusive);
        }

        public struct PoolItem
        {
            public string id;          // WeaponId hoac costume itemId
            public string displayName;
            public WeaponTier rarity;
            public bool isWeapon;
            public bool isCostumeSet;
            public List<string> itemIds;
        }

        public struct GachaResult
        {
            public string id;
            public string displayName;
            public WeaponTier rarity;
            public bool isWeapon;
            public bool isNew;
            public long dupComp;
            public WalletCurrency dupCurrency;
            public int weaponShards;
        }

        /// Dung pool item hop le. Weapon: tat ca WeaponData tru starter (pistol_a). Costume: entry
        /// source Gacha/ShopAndGacha, khong Starter/Disabled. Loai tru id rong.
        public static List<PoolItem> BuildPool(EconomyConfig.GachaPool pool, EconomyConfig econ,
            IReadOnlyList<WeaponData> weapons, ICollection<string> starterWeaponIds)
        {
            var items = new List<PoolItem>();
            if (pool == null) return items;

            if (pool.kind == "weapon")
            {
                if (weapons != null)
                    foreach (var w in weapons)
                    {
                        if (w == null || string.IsNullOrEmpty(w.WeaponId)) continue;
                        if (starterWeaponIds != null && starterWeaponIds.Contains(w.WeaponId)) continue; // starter loai
                        items.Add(new PoolItem { id = w.WeaponId, displayName = w.weaponName, rarity = w.tier, isWeapon = true });
                    }
            }
            else if (econ != null && econ.costumeSets != null && econ.costumeSets.Count > 0)
            {
                foreach (var set in econ.costumeSets)
                {
                    if (set == null || string.IsNullOrEmpty(set.setId) || set.itemIds == null || set.itemIds.Count == 0) continue;
                    if (set.source != AcquireSource.Gacha && set.source != AcquireSource.ShopAndGacha) continue;
                    items.Add(new PoolItem { id = set.setId, displayName = set.displayName, rarity = set.rarity,
                        isWeapon = false, isCostumeSet = true, itemIds = set.itemIds });
                }
            }
            else if (econ != null)
            {
                foreach (var e in econ.costumeItems)
                {
                    if (string.IsNullOrEmpty(e.itemId)) continue;
                    if (e.source != AcquireSource.Gacha && e.source != AcquireSource.ShopAndGacha) continue;
                    items.Add(new PoolItem { id = e.itemId, displayName = e.displayName, rarity = e.rarity, isWeapon = false });
                }
            }
            return items;
        }

        /// Chon rarity theo trong so, ap pity: neu pity >= threshold ep rarity >= pityMinRarity.
        private static WeaponTier PickRarity(EconomyConfig.GachaPool pool, List<PoolItem> items, int pity, IRng rng)
        {
            bool forced = pity >= pool.pityThreshold;
            int total = 0;
            var weights = new int[5];
            for (int r = 0; r < 5; r++)
            {
                if (forced && r < (int)pool.pityMinRarity) { weights[r] = 0; continue; }
                int w = pool.rarityWeights != null && r < pool.rarityWeights.Length ? pool.rarityWeights[r] : 0;
                // chi tinh rarity co it nhat 1 item trong pool
                if (!HasRarity(items, (WeaponTier)r)) w = 0;
                weights[r] = w; total += w;
            }
            if (total <= 0)
            {
                // fallback: rarity cao nhat co item
                for (int r = 4; r >= 0; r--) if (HasRarity(items, (WeaponTier)r)) return (WeaponTier)r;
                return WeaponTier.Common;
            }
            int roll = rng.Range(total);
            int acc = 0;
            for (int r = 0; r < 5; r++) { acc += weights[r]; if (roll < acc) return (WeaponTier)r; }
            return WeaponTier.Common;
        }

        private static bool HasRarity(List<PoolItem> items, WeaponTier r)
        {
            for (int i = 0; i < items.Count; i++) if (items[i].rarity == r) return true;
            return false;
        }

        /// Resolve + COMMIT 1 batch pull (count) atomic. Validate currency truoc; tru 1 lan (batch cost);
        /// moi pull resolve rarity(pity) -> item -> new/dupe; pity reset khi ra >= pityMinRarity, else +1.
        /// Dupe -> compensation. Tra ve results; null neu khong du tien/pool rong/save fail.
        public static List<GachaResult> Pull(EconomyConfig econ, EconomyConfig.GachaPool pool,
            List<PoolItem> poolItems, int count, IRng rng)
        {
            if (econ == null || pool == null || !pool.enabled || poolItems == null || poolItems.Count == 0) return null;
            if (count <= 0) return null;
            long cost = count == 1 ? pool.singleCost : pool.multiCost;
            var kind = ToKind(pool.currency);
            if (PlayerProfile.GetBalance(kind) < cost) return null; // khong du -> khong tang pity

            var results = new List<GachaResult>(count);
            int pity = PlayerProfile.GetPity(pool.poolId);
            var grantWeapons = new List<string>();
            var grantCostumes = new List<string>();
            long dupCoin = 0, dupGold = 0, dupGem = 0;

            for (int n = 0; n < count; n++)
            {
                var rarity = PickRarity(pool, poolItems, pity, rng);
                var candidates = new List<PoolItem>();
                foreach (var it in poolItems) if (it.rarity == rarity) candidates.Add(it);
                var chosen = candidates[rng.Range(candidates.Count)];

                bool owned = chosen.isWeapon ? PlayerProfile.IsWeaponOwned(chosen.id)
                    : chosen.isCostumeSet ? AllCostumeItemsOwned(chosen.itemIds)
                    : PlayerProfile.IsCostumeItemOwned(chosen.id);
                var res = new GachaResult { id = chosen.id, displayName = chosen.displayName, rarity = rarity, isWeapon = chosen.isWeapon };

                if (owned)
                {
                    if (chosen.isWeapon)
                    {
                        int shards = pool.weaponDuplicateShards != null && (int)rarity < pool.weaponDuplicateShards.Length
                            ? pool.weaponDuplicateShards[(int)rarity] : 0;
                        res.isNew = false;
                        res.weaponShards = shards;
                        results.Add(res);
                        pity = (int)rarity >= (int)pool.pityMinRarity ? 0 : pity + 1;
                        continue;
                    }
                    long comp = pool.dupCompensation != null && (int)rarity < pool.dupCompensation.Length ? pool.dupCompensation[(int)rarity] : 0;
                    res.isNew = false; res.dupComp = comp; res.dupCurrency = pool.dupCurrency;
                    switch (pool.dupCurrency) { case WalletCurrency.Gold: dupGold += comp; break; case WalletCurrency.Gem: dupGem += comp; break; default: dupCoin += comp; break; }
                }
                else
                {
                    res.isNew = true;
                    if (chosen.isWeapon) grantWeapons.Add(chosen.id);
                    else if (chosen.isCostumeSet && chosen.itemIds != null) grantCostumes.AddRange(chosen.itemIds);
                    else grantCostumes.Add(chosen.id);
                }

                pity = (int)rarity >= (int)pool.pityMinRarity ? 0 : pity + 1;
                results.Add(res);
            }

            bool ok = PlayerProfile.CommitGacha(kind, cost, () =>
            {
                foreach (var w in grantWeapons) { PlayerProfile.GrantWeaponInMemory(w); PlayerProfile.MarkUnseen(w); }
                foreach (var c in grantCostumes) { PlayerProfile.GrantCostumeInMemory(c); PlayerProfile.MarkUnseen(c); }
                foreach (var r in results) if (!r.isNew && r.isWeapon && r.weaponShards > 0)
                    PlayerProfile.AddWeaponShardsInMemory(r.id, r.weaponShards);
                if (dupCoin > 0) PlayerProfile.AddInMemory(PlayerProfile.CurrencyKind.Coin, dupCoin);
                if (dupGold > 0) PlayerProfile.AddInMemory(PlayerProfile.CurrencyKind.Gold, dupGold);
                if (dupGem > 0) PlayerProfile.AddInMemory(PlayerProfile.CurrencyKind.Gem, dupGem);
                PlayerProfile.SetPityInMemory(pool.poolId, pity);
            });
            return ok ? results : null;
        }

        private static bool AllCostumeItemsOwned(List<string> itemIds)
        {
            if (itemIds == null || itemIds.Count == 0) return false;
            for (int i = 0; i < itemIds.Count; i++)
                if (!PlayerProfile.IsCostumeItemOwned(itemIds[i])) return false;
            return true;
        }

        private static PlayerProfile.CurrencyKind ToKind(WalletCurrency c) =>
            c == WalletCurrency.Gold ? PlayerProfile.CurrencyKind.Gold :
            c == WalletCurrency.Gem ? PlayerProfile.CurrencyKind.Gem : PlayerProfile.CurrencyKind.Coin;
    }
}
