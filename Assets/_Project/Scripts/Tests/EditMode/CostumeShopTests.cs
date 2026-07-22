using System.Collections.Generic;
using BillGameCore;
using NUnit.Framework;
using UnityEngine;
using ZombieWar;

namespace ZombieWar.Tests
{
    /// EditMode tests cho giao dich mua COSTUME atomic (PlayerProfile.TryPurchaseCostume) qua
    /// EconomyConfig: gia theo rarity band, starter/gacha-only khong ban, body/ear resolve dung,
    /// rollback khi save fail. Storage in-memory — khong cham PlayerPrefs that.
    public class CostumeShopTests
    {
        private class InMemorySave : ISaveService
        {
            public readonly Dictionary<string, string> store = new();
            public bool throwOnSet;
            private int _slot;
            private string K(string key) => $"s{_slot}_{key}";
            public void Set(string key, string val) => store[K(key)] = val;
            public void Set(string key, int val) => store[K(key)] = val.ToString();
            public void Set(string key, float val) => store[K(key)] = val.ToString();
            public void Set(string key, bool val) => store[K(key)] = val ? "1" : "0";
            public void Set<T>(string key, T val) where T : class
            {
                if (throwOnSet) throw new System.IO.IOException("simulated save failure");
                store[K(key)] = JsonUtility.ToJson(val);
            }
            public string GetString(string key, string fb = "") => store.TryGetValue(K(key), out var v) ? v : fb;
            public int GetInt(string key, int fb = 0) => store.TryGetValue(K(key), out var v) && int.TryParse(v, out var i) ? i : fb;
            public float GetFloat(string key, float fb = 0f) => store.TryGetValue(K(key), out var v) && float.TryParse(v, out var f) ? f : fb;
            public bool GetBool(string key, bool fb = false) => store.TryGetValue(K(key), out var v) ? v == "1" : fb;
            public T Get<T>(string key) where T : class
            {
                if (!store.TryGetValue(K(key), out var j) || string.IsNullOrEmpty(j)) return null;
                try { return JsonUtility.FromJson<T>(j); } catch { return null; }
            }
            public bool Has(string key) => store.ContainsKey(K(key));
            public void Delete(string key) => store.Remove(K(key));
            public void SetSlot(int slot) => _slot = Mathf.Max(0, slot);
            public void Flush() { }
        }

        private InMemorySave _save;
        private EconomyConfig _econ;
        private int _walletEvents, _costumeEvents;

        [SetUp]
        public void SetUp()
        {
            _save = new InMemorySave();
            PlayerProfile.StorageOverride = _save;
            PlayerProfile.LegacyReadString = _ => "";
            PlayerProfile.LegacyReadInt = _ => 0;
            PlayerProfile.ResetCacheForTests();

            _econ = ScriptableObject.CreateInstance<EconomyConfig>();
            _econ.costumeBands = new List<EconomyConfig.RarityBand>
            {
                new() { rarity = WeaponTier.Common,    currency = WalletCurrency.Coin, price = 300 },
                new() { rarity = WeaponTier.Rare,      currency = WalletCurrency.Gold, price = 120 },
                new() { rarity = WeaponTier.Epic,      currency = WalletCurrency.Gold, price = 350 },
                new() { rarity = WeaponTier.Legendary, currency = WalletCurrency.Gold, price = 900 },
            };
            _econ.costumeItems = new List<EconomyConfig.CostumeEntry>
            {
                new() { itemId = "guid.starter", displayName = "Starter Hair", slot = "Hair", rarity = WeaponTier.Common, source = AcquireSource.Starter },
                new() { itemId = "guid.shop",    displayName = "Shop Hair",    slot = "Hair", rarity = WeaponTier.Common, source = AcquireSource.Shop },
                new() { itemId = "guid.gacha",   displayName = "Gacha Hair",   slot = "Hair", rarity = WeaponTier.Legendary, source = AcquireSource.Gacha },
                new() { itemId = EconomyConfig.BodyColorId("Black"), displayName = "Da Black", slot = "Body", rarity = WeaponTier.Rare, source = AcquireSource.Shop },
                new() { itemId = EconomyConfig.BodyEarId("Elf"),     displayName = "Tai Elf",  slot = "Body", rarity = WeaponTier.Epic, source = AcquireSource.ShopAndGacha },
            };
            _econ.RebuildLookups();

            _walletEvents = _costumeEvents = 0;
            PlayerProfile.WalletChanged += CountWallet;
            PlayerProfile.CostumeChanged += CountCostume;
        }

        [TearDown]
        public void TearDown()
        {
            PlayerProfile.WalletChanged -= CountWallet;
            PlayerProfile.CostumeChanged -= CountCostume;
            PlayerProfile.StorageOverride = null;
            PlayerProfile.LegacyReadString = k => PlayerPrefs.GetString(k, "");
            PlayerProfile.LegacyReadInt = k => PlayerPrefs.GetInt(k, 0);
            PlayerProfile.ResetCacheForTests();
            if (_econ != null) Object.DestroyImmediate(_econ);
        }

        private void CountWallet() => _walletEvents++;
        private void CountCostume() => _costumeEvents++;
        private void Reset() { _walletEvents = _costumeEvents = 0; }

        [Test]
        public void BuyShopPart_DebitsCoin_GrantsOwnership_EventsOnce_Persists()
        {
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 500);
            Reset();

            var r = PlayerProfile.TryPurchaseCostume(_econ, "guid.shop");

            Assert.AreEqual(PlayerProfile.PurchaseResult.Purchased, r);
            Assert.AreEqual(200, PlayerProfile.Coin);
            Assert.IsTrue(PlayerProfile.IsCostumeOwned("guid.shop"));
            Assert.AreEqual(1, _walletEvents);
            Assert.AreEqual(1, _costumeEvents);

            PlayerProfile.ResetCacheForTests();
            Assert.IsTrue(PlayerProfile.IsCostumeOwned("guid.shop"), "So huu ton tai sau reload.");
            Assert.AreEqual(200, PlayerProfile.Coin);
        }

        [Test]
        public void StarterItem_NeverSellable()
        {
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 10000);
            Reset();
            var r = PlayerProfile.TryPurchaseCostume(_econ, "guid.starter");
            Assert.AreEqual(PlayerProfile.PurchaseResult.InvalidWeapon, r);
            Assert.AreEqual(10000, PlayerProfile.Coin);
            Assert.AreEqual(0, _walletEvents + _costumeEvents);
        }

        [Test]
        public void GachaOnlyItem_NotSellableInShop()
        {
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Gold, 10000);
            Reset();
            var r = PlayerProfile.TryPurchaseCostume(_econ, "guid.gacha");
            Assert.AreEqual(PlayerProfile.PurchaseResult.InvalidWeapon, r);
            Assert.IsFalse(PlayerProfile.IsCostumeOwned("guid.gacha"));
            Assert.AreEqual(0, _walletEvents + _costumeEvents);
        }

        [Test]
        public void InsufficientFunds_ChangesNothing()
        {
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 100);
            Reset();
            var r = PlayerProfile.TryPurchaseCostume(_econ, "guid.shop");
            Assert.AreEqual(PlayerProfile.PurchaseResult.InsufficientFunds, r);
            Assert.AreEqual(100, PlayerProfile.Coin);
            Assert.IsFalse(PlayerProfile.IsCostumeOwned("guid.shop"));
            Assert.AreEqual(0, _walletEvents + _costumeEvents);
        }

        [Test]
        public void DuplicatePurchase_ChargesZero()
        {
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 1000);
            PlayerProfile.TryPurchaseCostume(_econ, "guid.shop");
            Reset();
            var r = PlayerProfile.TryPurchaseCostume(_econ, "guid.shop");
            Assert.AreEqual(PlayerProfile.PurchaseResult.AlreadyOwned, r);
            Assert.AreEqual(700, PlayerProfile.Coin);
            Assert.AreEqual(0, _walletEvents + _costumeEvents);
        }

        [Test]
        public void BuyBodyColor_GrantsBodyColorOwnership_DebitsGold()
        {
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Gold, 500);
            Reset();
            var r = PlayerProfile.TryPurchaseCostume(_econ, EconomyConfig.BodyColorId("Black"));
            Assert.AreEqual(PlayerProfile.PurchaseResult.Purchased, r);
            Assert.AreEqual(380, PlayerProfile.Gold);
            Assert.IsTrue(PlayerProfile.IsBodyColorOwned("Black"));
            Assert.AreEqual(1, _walletEvents);
            Assert.AreEqual(1, _costumeEvents);
        }

        [Test]
        public void BuyEar_GrantsEarOwnership()
        {
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Gold, 500);
            var r = PlayerProfile.TryPurchaseCostume(_econ, EconomyConfig.BodyEarId("Elf"));
            Assert.AreEqual(PlayerProfile.PurchaseResult.Purchased, r);
            Assert.AreEqual(150, PlayerProfile.Gold);
            Assert.IsTrue(PlayerProfile.IsBodyEarOwned("Elf"));
        }

        [Test]
        public void DefaultOwned_ReportedOwned_NotSellable()
        {
            // White + Normal la default (owned) — IsCostumeItemOwned true; mua lai -> AlreadyOwned/Invalid.
            Assert.IsTrue(PlayerProfile.IsCostumeItemOwned(EconomyConfig.BodyColorId("White")));
            Assert.IsTrue(PlayerProfile.IsCostumeItemOwned(EconomyConfig.BodyEarId("Normal")));
        }

        [Test]
        public void SaveFailure_RollsBack_NoEvents()
        {
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 500);
            Reset();
            _save.throwOnSet = true;
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex("Save fail mua costume"));
            var r = PlayerProfile.TryPurchaseCostume(_econ, "guid.shop");
            _save.throwOnSet = false;

            Assert.AreEqual(PlayerProfile.PurchaseResult.SaveFailed, r);
            Assert.AreEqual(500, PlayerProfile.Coin, "Rollback tien.");
            Assert.IsFalse(PlayerProfile.IsCostumeOwned("guid.shop"), "Rollback so huu.");
            Assert.AreEqual(0, _walletEvents + _costumeEvents);
        }

        [Test]
        public void UnknownItem_Rejected()
        {
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 500);
            Assert.AreEqual(PlayerProfile.PurchaseResult.InvalidWeapon,
                PlayerProfile.TryPurchaseCostume(_econ, "guid.nope"));
            Assert.AreEqual(PlayerProfile.PurchaseResult.InvalidWeapon,
                PlayerProfile.TryPurchaseCostume(_econ, null));
            Assert.AreEqual(PlayerProfile.PurchaseResult.InvalidWeapon,
                PlayerProfile.TryPurchaseCostume(null, "guid.shop"));
        }
    }
}
