using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar
{
    public enum AcquireSource { Starter, Shop, Gacha, ShopAndGacha, Disabled }
    public enum WalletCurrency { Coin, Gold, Gem }

    /// <summary>
    /// NGUON DUY NHAT cho commerce + gacha (Slice 5/6). Editor-visible, tune duoc trong Inspector.
    /// Gia costume KHONG rai trong UI: rarity band tap trung o day. Gacha pool tham chieu item ID
    /// co san (khong copy item sang catalog thu 2). GIA TRI TAM (provisional) — chua phai balance
    /// cuoi, se can do run-income truoc khi chot.
    /// </summary>
    [CreateAssetMenu(fileName = "EconomyConfig", menuName = "ZombieWar/Economy Config")]
    public class EconomyConfig : ScriptableObject
    {
        [Header("PROVISIONAL — chua phai game balance cuoi (chua co run income)")]
        public bool provisional = true;

        // ---- Rarity price band cho COSTUME (Coin/Gem theo rarity) ----
        [Serializable]
        public struct RarityBand
        {
            public WeaponTier rarity;
            public WalletCurrency currency;
            public long price;
        }
        public List<RarityBand> costumeBands = new();

        // ---- Commerce record moi costume item (generated, editable) ----
        // itemId: guid part | "body:<Color>" | "ear:<Ear>". rarity -> price qua band.
        [Serializable]
        public struct CostumeEntry
        {
            public string itemId;
            public string displayName;
            public string slot;
            public WeaponTier rarity;
            public AcquireSource source;
            public WalletCurrency currency;
            public long price;
        }
        public List<CostumeEntry> costumeItems = new();

        // ---- Curated complete outfits (Pro Casual) ----
        // A set is the commerce/gacha unit. itemIds still point at the one authoritative
        // ModularCostumeCatalog; no mesh or renderer data is duplicated here.
        [Serializable]
        public class CostumeSetEntry
        {
            public string setId;
            public string displayName;
            public WeaponTier rarity;
            public AcquireSource source = AcquireSource.ShopAndGacha;
            public WalletCurrency currency;
            public long price;
            [Tooltip("Legacy serialized Gem price. Kept only so older assets migrate without losing value.")]
            public long gemPrice;
            public Sprite icon;
            public List<string> itemIds = new();
            [Tooltip("Vendor preset used to author this set (audit/debug only).")]
            public string sourcePreset;
        }
        public List<CostumeSetEntry> costumeSets = new();

        [Header("Weapon shard upgrades (index 0..4 = Common..Legendary)")]
        public int[] weaponStar2ShardCost = { 10, 15, 20, 30, 40 };
        public int[] weaponStar3ShardCost = { 25, 35, 50, 75, 100 };
        public long[] weaponStar2GoldCost = { 100, 150, 250, 400, 650 };
        public long[] weaponStar3GoldCost = { 250, 350, 600, 900, 1500 };

        // ---- Gacha pool ----
        [Serializable]
        public class GachaPool
        {
            public string poolId;
            public string displayName;
            public bool enabled = true;
            public string kind = "weapon"; // "weapon" | "costume"
            public WalletCurrency currency = WalletCurrency.Gold;
            public long singleCost = 100;
            public long multiCost = 900;
            public int multiCount = 10;
            [Tooltip("Trong so rarity, index 0..4 = Common..Legendary. Tong > 0.")]
            public int[] rarityWeights = { 55, 25, 12, 6, 2 };
            [Tooltip("So pull khong ra >= pityMinRarity thi pull ke tiep guarantee.")]
            public int pityThreshold = 30;
            public WeaponTier pityMinRarity = WeaponTier.Epic;
            [Tooltip("Coin den bu khi trung (dupe), index 0..4 theo rarity.")]
            public WalletCurrency dupCurrency = WalletCurrency.Coin;
            public long[] dupCompensation = { 20, 40, 80, 160, 320 };
            [Tooltip("Weapon duplicate shards, index 0..4 by rarity. Costume pools ignore this.")]
            public int[] weaponDuplicateShards = { 10, 10, 12, 15, 20 };
        }
        public GachaPool weaponPool = new() { poolId = "gacha.weapon", displayName = "Súng Gacha", kind = "weapon" };
        public GachaPool costumePool = new() { poolId = "gacha.costume", displayName = "Skin Gacha", kind = "costume",
            currency = WalletCurrency.Gem, singleCost = 10, multiCost = 90 };

        // ================================================================ lookups
        private Dictionary<string, int> _costumeIndex;
        private Dictionary<string, int> _setIndex;
        private Dictionary<WeaponTier, RarityBand> _bandIndex;

        public void RebuildLookups()
        {
            _costumeIndex = new Dictionary<string, int>(costumeItems.Count);
            for (int i = 0; i < costumeItems.Count; i++)
                if (!string.IsNullOrEmpty(costumeItems[i].itemId)) _costumeIndex[costumeItems[i].itemId] = i;
            _bandIndex = new Dictionary<WeaponTier, RarityBand>();
            foreach (var b in costumeBands) _bandIndex[b.rarity] = b;
            _setIndex = new Dictionary<string, int>(costumeSets.Count);
            for (int i = 0; i < costumeSets.Count; i++)
                if (costumeSets[i] != null && !string.IsNullOrEmpty(costumeSets[i].setId))
                    _setIndex[costumeSets[i].setId] = i;
        }

        public bool TryGetCostume(string itemId, out CostumeEntry entry)
        {
            if (_costumeIndex == null) RebuildLookups();
            entry = default;
            if (string.IsNullOrEmpty(itemId) || !_costumeIndex.TryGetValue(itemId, out int i)) return false;
            entry = costumeItems[i];
            return true;
        }

        public bool TryGetPrice(WeaponTier rarity, out WalletCurrency currency, out long price)
        {
            if (_bandIndex == null) RebuildLookups();
            currency = WalletCurrency.Coin; price = 0;
            if (_bandIndex != null && _bandIndex.TryGetValue(rarity, out var b)) { currency = b.currency; price = b.price; return true; }
            return false;
        }

        public bool TryGetCostumePrice(string itemId, out WalletCurrency currency, out long price)
        {
            currency = WalletCurrency.Coin;
            price = 0;
            if (!TryGetCostume(itemId, out var entry)) return false;
            if (entry.price > 0)
            {
                currency = entry.currency;
                price = entry.price;
                return true;
            }
            return TryGetPrice(entry.rarity, out currency, out price);
        }

        public bool TryGetCostumeSetPrice(CostumeSetEntry entry, out WalletCurrency currency, out long price)
        {
            currency = WalletCurrency.Gem;
            price = 0;
            if (entry == null) return false;
            currency = entry.price > 0 ? entry.currency : WalletCurrency.Gem;
            price = entry.price > 0 ? entry.price : entry.gemPrice;
            return price >= 0;
        }

        public bool TryGetCostumeSet(string setId, out CostumeSetEntry entry)
        {
            if (_setIndex == null) RebuildLookups();
            entry = null;
            return !string.IsNullOrEmpty(setId) && _setIndex.TryGetValue(setId, out int i)
                && (entry = costumeSets[i]) != null;
        }

        public CostumeSetEntry FindSetContaining(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            for (int i = 0; i < costumeSets.Count; i++)
            {
                var set = costumeSets[i];
                if (set != null && set.itemIds != null && set.itemIds.Contains(itemId)) return set;
            }
            return null;
        }

        public static string BodyColorId(string color) => "body:" + color;
        public static string BodyEarId(string ear) => "ear:" + ear;
        public static bool IsBodyColorId(string id, out string color)
        { color = null; if (id != null && id.StartsWith("body:")) { color = id.Substring(5); return true; } return false; }
        public static bool IsBodyEarId(string id, out string ear)
        { ear = null; if (id != null && id.StartsWith("ear:")) { ear = id.Substring(4); return true; } return false; }
    }
}
