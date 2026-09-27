using System.Collections.Generic;
using BillGameCore;
using NUnit.Framework;
using UnityEngine;

namespace ZombieWar.Tests
{
    /// M10 Shop v2: daily deals once per day, packs once, gem skin set.
    public class ShopOffersTests
    {
        private class InMemorySave : ISaveService
        {
            public readonly Dictionary<string, string> store = new();
            public void Set(string key, string val) => store[key] = val;
            public void Set(string key, int val) => store[key] = val.ToString();
            public void Set(string key, float val) => store[key] = val.ToString();
            public void Set(string key, bool val) => store[key] = val ? "1" : "0";
            public void Set<T>(string key, T val) where T : class => store[key] = JsonUtility.ToJson(val);
            public string GetString(string key, string fb = "") => store.TryGetValue(key, out var v) ? v : fb;
            public int GetInt(string key, int fb = 0) => store.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : fb;
            public float GetFloat(string key, float fb = 0f) => store.TryGetValue(key, out var v) && float.TryParse(v, out var f) ? f : fb;
            public bool GetBool(string key, bool fb = false) => store.TryGetValue(key, out var v) ? v == "1" : fb;
            public T Get<T>(string key) where T : class =>
                store.TryGetValue(key, out var j) && !string.IsNullOrEmpty(j) ? JsonUtility.FromJson<T>(j) : null;
            public bool Has(string key) => store.ContainsKey(key);
            public void Delete(string key) => store.Remove(key);
            public void SetSlot(int slot) { }
            public void Flush() { }
        }

        InMemorySave _save;

        [SetUp]
        public void SetUp()
        {
            _save = new InMemorySave();
            PlayerProfile.StorageOverride = _save;
            PlayerProfile.LegacyReadString = _ => "";
            PlayerProfile.LegacyReadInt = _ => 0;
            PlayerProfile.ResetCacheForTests();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerProfile.StorageOverride = null;
            PlayerProfile.ResetCacheForTests();
        }

        const int Day0 = 9000;

        [Test]
        public void Deals_BuyOncePerDay_ResetTomorrow()
        {
            var deals = ShopOffers.DealsFor(Day0, null);
            Assert.AreEqual(3, deals.Length);
            Assert.AreEqual(ShopOffers.Kind.Tickets, deals[0].kind, "no guns owned -> ticket fallback");
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Gem, 60);
            long t0 = PlayerProfile.Tickets;
            Assert.IsTrue(ShopOffers.BuyDeal(Day0, deals[1]));
            Assert.AreEqual(t0 + ShopOffers.TicketDealAmount, PlayerProfile.Tickets);
            Assert.IsFalse(ShopOffers.BuyDeal(Day0, deals[1]), "once a day");
            Assert.IsTrue(ShopOffers.IsDealBought(Day0, 1));
            Assert.IsFalse(ShopOffers.IsDealBought(Day0 + 1, 1), "new day");
        }

        [Test]
        public void Deals_FailWithoutFunds_ChangeNothing()
        {
            PlayerProfile.TrySpend(PlayerProfile.CurrencyKind.Gem, PlayerProfile.Gem);
            var deals = ShopOffers.DealsFor(Day0, null);
            Assert.IsFalse(ShopOffers.BuyDeal(Day0, deals[1]));
            Assert.IsFalse(ShopOffers.IsDealBought(Day0, 1));
        }

        [Test]
        public void Packs_OnceOnlyPacksCannotRepeat()
        {
            var starter = ShopOffers.FindPack("pack.starter");
            long g0 = PlayerProfile.Gem;
            Assert.IsTrue(ShopOffers.GrantPack(starter));
            Assert.AreEqual(g0 + starter.gems, PlayerProfile.Gem);
            Assert.IsFalse(ShopOffers.GrantPack(starter));
            var gems = ShopOffers.FindPack("pack.gems440");
            Assert.IsTrue(ShopOffers.GrantPack(gems));
            Assert.IsTrue(ShopOffers.GrantPack(gems), "consumable");
            Assert.IsTrue(ShopOffers.GrantPack(ShopOffers.FindPack("pack.legend")));
            Assert.IsTrue(PlayerProfile.IsSkinOwned("gilded"));
            Assert.IsTrue(ShopOffers.GrantPack(ShopOffers.FindPack("pack.noads")));
            Assert.IsTrue(PlayerProfile.NoAds);
        }

        [Test]
        public void SkinSet_Biohazard_CostsGems()
        {
            PlayerProfile.TrySpend(PlayerProfile.CurrencyKind.Gem, PlayerProfile.Gem);
            Assert.IsFalse(ShopOffers.BuySkinWithGems("biohazard"));
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Gem, ShopOffers.BiohazardGems);
            Assert.IsTrue(ShopOffers.BuySkinWithGems("biohazard"));
            Assert.AreEqual(0, PlayerProfile.Gem);
            Assert.IsFalse(ShopOffers.BuySkinWithGems("neon"), "neon is a gacha prize");
        }
    }
}
