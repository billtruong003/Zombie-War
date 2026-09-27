using System.Collections.Generic;
using BillGameCore;
using NUnit.Framework;
using UnityEngine;

namespace ZombieWar.Tests
{
    /// M10 Gacha v2 event banner: free daily pull, tickets before gems, hard guarantee, dupes to tickets.
    public class GachaBannersTests
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

        sealed class FixedRng : GachaService.IRng
        {
            readonly int _v; public FixedRng(int v) { _v = v; }
            public int Range(int max) => max <= 0 ? 0 : Mathf.Min(_v, max - 1);
        }

        static GachaBanners.Banner Event => GachaBanners.All[0];

        [Test]
        public void FreePull_OncePerDay()
        {
            Assert.AreEqual(GachaBanners.Pay.Free, GachaBanners.BestPay(Event, 1, Day0));
            Assert.IsNotNull(GachaBanners.Pull(Event, 1, GachaBanners.Pay.Free, Day0, null, null, new FixedRng(9999)));
            Assert.IsFalse(GachaBanners.CanPay(Event, 1, GachaBanners.Pay.Free, Day0));
            Assert.IsTrue(GachaBanners.CanPay(Event, 1, GachaBanners.Pay.Free, Day0 + 1));
        }

        [Test]
        public void Pay_TicketsFirst_ThenGems_NoneMeansNull()
        {
            PlayerProfile.TrySpend(PlayerProfile.CurrencyKind.Gem, PlayerProfile.Gem);
            GachaBanners.Pull(Event, 1, GachaBanners.Pay.Free, Day0, null, null, new FixedRng(9999));
            Assert.IsNull(GachaBanners.Pull(Event, 1, GachaBanners.Pay.Gems, Day0, null, null, new FixedRng(9999)));
            PlayerProfile.AddTickets(1);
            Assert.AreEqual(GachaBanners.Pay.Tickets, GachaBanners.BestPay(Event, 1, Day0));
            Assert.IsNotNull(GachaBanners.Pull(Event, 1, GachaBanners.Pay.Tickets, Day0, null, null, new FixedRng(9999)));
            Assert.AreEqual(0, PlayerProfile.Tickets);
        }

        [Test]
        public void Guarantee_HitsFeatured_ThenDupesGiveTickets()
        {
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Gem, 100000);
            var rng = new FixedRng(9999);   // always the most common prize unless guaranteed
            int pulls = 0;
            while (!PlayerProfile.IsSkinOwned(Event.featuredSkin) && pulls < 200)
            { GachaBanners.Pull(Event, 10, GachaBanners.Pay.Gems, Day0, null, null, rng); pulls += 10; }
            Assert.IsTrue(PlayerProfile.IsSkinOwned(Event.featuredSkin));
            Assert.LessOrEqual(pulls, Event.guarantee);
            Assert.AreEqual(0, PlayerProfile.GetPity(Event.pityKey) % Event.guarantee, "pity restarts after the featured prize");
            var dupe = GachaBanners.Pull(Event, 1, GachaBanners.Pay.Gems, Day0, null, null, new FixedRng(0));
            Assert.AreEqual(GachaBanners.FeaturedDupeTickets, dupe[0].tickets);
        }

        [Test]
        public void Rates_ArePublic_AndSumTo100()
        {
            float sum = 0f; foreach (var r in GachaBanners.RatesFor(Event, null)) sum += r.percent;
            Assert.AreEqual(100f, sum, 0.01f);
        }
    }
}
