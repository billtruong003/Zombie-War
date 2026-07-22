using System.Collections.Generic;
using BillGameCore;
using NUnit.Framework;
using UnityEngine;
using ZombieWar;
using ZombieWar.UI;

namespace ZombieWar.Tests
{
    /// EditMode tests cho giao dich mua sung atomic (PlayerProfile.TryPurchaseWeapon)
    /// + ProfileCurrencyProvider + format tien. Storage in-memory — khong cham PlayerPrefs that.
    public class ShopPurchaseTests
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
        private int _walletEvents;
        private int _loadoutEvents;

        [SetUp]
        public void SetUp()
        {
            _save = new InMemorySave();
            PlayerProfile.StorageOverride = _save;
            PlayerProfile.LegacyReadString = _ => "";
            PlayerProfile.LegacyReadInt = _ => 0;
            PlayerProfile.ResetCacheForTests();
            _walletEvents = 0;
            _loadoutEvents = 0;
            PlayerProfile.WalletChanged += CountWallet;
            PlayerProfile.LoadoutChanged += CountLoadout;
        }

        [TearDown]
        public void TearDown()
        {
            PlayerProfile.WalletChanged -= CountWallet;
            PlayerProfile.LoadoutChanged -= CountLoadout;
            PlayerProfile.StorageOverride = null;
            PlayerProfile.LegacyReadString = k => PlayerPrefs.GetString(k, "");
            PlayerProfile.LegacyReadInt = k => PlayerPrefs.GetInt(k, 0);
            PlayerProfile.ResetCacheForTests();
        }

        private void CountWallet() => _walletEvents++;
        private void CountLoadout() => _loadoutEvents++;
        private void ResetCounters() { _walletEvents = 0; _loadoutEvents = 0; }

        [Test]
        public void Purchase_Succeeds_DeductsExactPrice_EventsOnce()
        {
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 500);
            ResetCounters();

            var result = PlayerProfile.TryPurchaseWeapon("weapon.smg.generic", 300);

            Assert.AreEqual(PlayerProfile.PurchaseResult.Purchased, result);
            Assert.AreEqual(200, PlayerProfile.Coin, "Tru dung gia, khong hon khong kem.");
            Assert.IsTrue(PlayerProfile.IsWeaponOwned("weapon.smg.generic"));
            Assert.AreEqual(1, _walletEvents, "WalletChanged dung 1 lan.");
            Assert.AreEqual(1, _loadoutEvents, "Ownership event dung 1 lan.");
        }

        [Test]
        public void Purchase_Persists_AfterReload()
        {
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 500);
            PlayerProfile.TryPurchaseWeapon("weapon.smg.generic", 300);

            PlayerProfile.ResetCacheForTests(); // reload tu cung storage

            Assert.AreEqual(200, PlayerProfile.Coin);
            Assert.IsTrue(PlayerProfile.IsWeaponOwned("weapon.smg.generic"));
        }

        [Test]
        public void FreeWeapon_Purchased_NoCoinChange_NoWalletEvent()
        {
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 100);
            ResetCounters();

            var result = PlayerProfile.TryPurchaseWeapon("weapon.sidearm.pistol_a", 0);

            Assert.AreEqual(PlayerProfile.PurchaseResult.Purchased, result);
            Assert.AreEqual(100, PlayerProfile.Coin);
            Assert.AreEqual(0, _walletEvents, "Gia 0 -> vi khong doi -> khong ban WalletChanged.");
            Assert.AreEqual(1, _loadoutEvents);
        }

        [Test]
        public void DuplicatePurchase_ChargesZero_NoEvents()
        {
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 1000);
            PlayerProfile.TryPurchaseWeapon("weapon.smg.generic", 300);
            ResetCounters();

            var result = PlayerProfile.TryPurchaseWeapon("weapon.smg.generic", 300);

            Assert.AreEqual(PlayerProfile.PurchaseResult.AlreadyOwned, result);
            Assert.AreEqual(700, PlayerProfile.Coin, "Mua trung khong duoc charge.");
            Assert.AreEqual(0, _walletEvents);
            Assert.AreEqual(0, _loadoutEvents);
        }

        [Test]
        public void InsufficientFunds_ChangesNothing_NoEvents()
        {
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 100);
            ResetCounters();

            var result = PlayerProfile.TryPurchaseWeapon("weapon.lmg.generic", 250);

            Assert.AreEqual(PlayerProfile.PurchaseResult.InsufficientFunds, result);
            Assert.AreEqual(100, PlayerProfile.Coin, "Khong tru tien khi that bai.");
            Assert.IsFalse(PlayerProfile.IsWeaponOwned("weapon.lmg.generic"));
            Assert.AreEqual(0, _walletEvents, "That bai khong duoc ban event nao.");
            Assert.AreEqual(0, _loadoutEvents);
        }

        [Test]
        public void InvalidInputs_Rejected_NoStateChange()
        {
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 100);
            ResetCounters();

            Assert.AreEqual(PlayerProfile.PurchaseResult.InvalidPrice,
                PlayerProfile.TryPurchaseWeapon("weapon.smg.generic", -1));
            Assert.AreEqual(PlayerProfile.PurchaseResult.InvalidWeapon,
                PlayerProfile.TryPurchaseWeapon("", 100));
            Assert.AreEqual(PlayerProfile.PurchaseResult.InvalidWeapon,
                PlayerProfile.TryPurchaseWeapon(null, 100));

            Assert.AreEqual(100, PlayerProfile.Coin);
            Assert.AreEqual(0, _walletEvents);
            Assert.AreEqual(0, _loadoutEvents);
        }

        [Test]
        public void Balance_NeverUnderflows()
        {
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 10);
            PlayerProfile.TryPurchaseWeapon("weapon.a", 10);
            Assert.AreEqual(0, PlayerProfile.Coin);
            Assert.AreEqual(PlayerProfile.PurchaseResult.InsufficientFunds,
                PlayerProfile.TryPurchaseWeapon("weapon.b", 1));
            Assert.AreEqual(0, PlayerProfile.Coin);
        }

        [Test]
        public void LargeBalance_PurchaseKeepsPrecision()
        {
            long big = long.MaxValue - 100;
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, big);
            PlayerProfile.TryPurchaseWeapon("weapon.expensive", 1_000_000_000_000L);
            Assert.AreEqual(big - 1_000_000_000_000L, PlayerProfile.Coin);

            PlayerProfile.ResetCacheForTests();
            Assert.AreEqual(big - 1_000_000_000_000L, PlayerProfile.Coin, "long round-trip khong mat chinh xac.");
        }

        [Test]
        public void SaveFailure_RollsBack_NoEvents()
        {
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 500);
            ResetCounters();
            _save.throwOnSet = true;

            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex("Luu profile that bai"));
            var result = PlayerProfile.TryPurchaseWeapon("weapon.smg.generic", 300);
            _save.throwOnSet = false;

            Assert.AreEqual(PlayerProfile.PurchaseResult.SaveFailed, result);
            Assert.AreEqual(500, PlayerProfile.Coin, "Rollback: khong tru tien khi save fail.");
            Assert.IsFalse(PlayerProfile.IsWeaponOwned("weapon.smg.generic"));
            Assert.AreEqual(0, _walletEvents);
            Assert.AreEqual(0, _loadoutEvents);
        }

        [Test]
        public void CheatUnlockAll_CannotBypassTransactionRules()
        {
            var catalog = ScriptableObject.CreateInstance<UIPrototypeCatalog>();
            catalog.cheatUnlockAll = true;

            // Cheat chi la display flag cua catalog — profile/transaction khong hoi no:
            Assert.IsFalse(PlayerProfile.IsWeaponOwned("weapon.smg.generic"),
                "Ownership that khong bi cheat ghi de.");
            Assert.AreEqual(PlayerProfile.PurchaseResult.InsufficientFunds,
                PlayerProfile.TryPurchaseWeapon("weapon.smg.generic", 300),
                "Cheat khong lam giao dich thanh cong.");

            Object.DestroyImmediate(catalog);
        }

        // ===== Provider + format =====

        [Test]
        public void ProfileCurrencyProvider_ReadsProfile_AndForwardsChanged()
        {
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 111);
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Gold, 22);
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Gem, 3);

            var provider = new ProfileCurrencyProvider();
            Assert.AreEqual(111, provider.Coin);
            Assert.AreEqual(22, provider.Gold);
            Assert.AreEqual(3, provider.Gem);

            int changed = 0;
            System.Action handler = () => changed++;
            provider.Changed += handler;
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 1);
            Assert.AreEqual(1, changed, "Changed forward dung 1 lan tu WalletChanged.");
            provider.Changed -= handler;
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 1);
            Assert.AreEqual(1, changed, "Unsubscribe phai go listener that.");
        }

        [Test]
        public void CurrencyFormat_LargeValues()
        {
            Assert.AreEqual("999", CurrencyClusterWidget.Format(999));
            Assert.AreEqual((9999L).ToString("N0"), CurrencyClusterWidget.Format(9999)); // culture-agnostic
            Assert.AreEqual("12.3K", CurrencyClusterWidget.Format(12_345));
            Assert.AreEqual("1.23M", CurrencyClusterWidget.Format(1_234_567));
            StringAssert.EndsWith("M", CurrencyClusterWidget.Format(long.MaxValue), "Gia tri long lon van format duoc.");
        }
    }
}
