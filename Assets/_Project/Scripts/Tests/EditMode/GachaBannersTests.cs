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
        public void HardPity90_LostFiftyFifty_ThenFeaturedGuaranteed()
        {
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Gem, 100000);
            var never = new FixedRng(99999);   // always the most common prize, and always loses a 50/50
            int boxes = 0;
            while (!PlayerProfile.IsSkinOwned(Event.featuredSkin) && boxes < 400)
                boxes += GachaBanners.Pull(Event, GachaBanners.MultiPaid, GachaBanners.Pay.Gems, Day0, null, null, never).Count;
            Assert.IsTrue(PlayerProfile.IsSkinOwned(Event.featuredSkin));
            // First Legendary at 90 is off-rate, the next one (by 180) must be the featured prize.
            Assert.LessOrEqual(boxes, Event.hardPity * 2 + GachaBanners.MultiBoxes);
            Assert.Greater(boxes, Event.hardPity, "the first Legendary lost the 50/50");
            Assert.IsFalse(GachaBanners.FeaturedGuaranteed(Event), "guarantee used up");
        }

        [Test]
        public void WinningFiftyFifty_GivesFeatured_AndDupesBecomeTickets()
        {
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Gem, 1000);
            var lucky = new FixedRng(0);        // Legendary on the first box, wins the 50/50
            var r = GachaBanners.Pull(Event, 1, GachaBanners.Pay.Gems, Day0, null, null, lucky);
            Assert.IsTrue(PlayerProfile.IsSkinOwned(Event.featuredSkin));
            Assert.IsFalse(r[0].offRate);
            var dupe = GachaBanners.Pull(Event, 1, GachaBanners.Pay.Gems, Day0, null, null, lucky);
            Assert.AreEqual(GachaBanners.FeaturedDupeTickets, dupe[0].tickets);
        }

        [Test]
        public void TenPull_OpensElevenBoxes_LastIsBonus_HoldsEpic()
        {
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Gem, 1000);
            var r = GachaBanners.Pull(Event, GachaBanners.MultiPaid, GachaBanners.Pay.Gems, Day0, null, null, new FixedRng(99999));
            Assert.AreEqual(GachaBanners.MultiBoxes, r.Count);
            Assert.IsTrue(r[GachaBanners.MultiBoxes - 1].bonus);
            Assert.IsFalse(r[0].bonus);
            Assert.IsTrue(r.Exists(x => x.tier >= WeaponTier.Epic));
        }

        [Test]
        public void Rates_ArePublic_AndSumTo100()
        {
            float sum = 0f; foreach (var r in GachaBanners.RatesFor(Event, null)) sum += r.percent;
            Assert.AreEqual(100f, sum, 0.01f);
        }

        // ---- G11 (04/10): first free pull, and copy built from the rules

        static WeaponData Gun(string id, WeaponTier tier, int price)
        {
            var w = ScriptableObject.CreateInstance<WeaponData>();
            w.weaponName = id;
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
            typeof(WeaponData).GetField("weaponId", flags).SetValue(w, id);
            typeof(WeaponData).GetField("tier", flags)?.SetValue(w, tier);
            typeof(WeaponData).GetField("price", flags)?.SetValue(w, price);
            return w;
        }

        [Test]
        public void FirstFreePull_IsTheCheapestUnownedGun_ThenPullsRollAsUsual()
        {
            var guns = new List<WeaponData> { Gun("t.common", WeaponTier.Common, 10), Gun("t.rare", WeaponTier.Rare, 900), Gun("t.unc", WeaponTier.Uncommon, 300) };
            var first = GachaBanners.Pull(Event, 1, GachaBanners.Pay.Free, Day0, null, guns, new FixedRng(9999));
            Assert.AreEqual(1, first.Count);
            Assert.AreEqual(GachaBanners.Prize.Gun, first[0].prize);
            Assert.AreEqual("t.unc", first[0].id, "cheapest Uncommon-or-better gun");
            Assert.IsTrue(PlayerProfile.IsWeaponOwned("t.unc"));
            Assert.IsFalse(GachaBanners.CanPay(Event, 1, GachaBanners.Pay.Free, Day0), "the gift spends the daily free pull");

            var second = GachaBanners.Pull(Event, 1, GachaBanners.Pay.Free, Day0 + 1, null, guns, new FixedRng(99999));
            Assert.AreEqual(GachaBanners.Prize.Coin, second[0].prize, "the next free pull rolls the banner");
        }

        [Test]
        public void PaidPull_NeverTakesTheFirstPullGift()
        {
            PlayerProfile.AddTickets(1);
            var guns = new List<WeaponData> { Gun("t.unc", WeaponTier.Uncommon, 300) };
            var r = GachaBanners.Pull(Event, 1, GachaBanners.Pay.Tickets, Day0, null, guns, new FixedRng(99999));
            Assert.AreNotEqual(GachaBanners.Prize.Gun, r[0].prize);
            Assert.IsFalse(PlayerProfile.IsWeaponOwned("t.unc"));
        }

        [Test]
        public void DuplicateCopy_NamesTheTicketsTheGrantGives()
        {
            PlayerProfile.AddSkin(Event.featuredSkin);
            long before = PlayerProfile.Tickets;
            PlayerProfile.SetPityInMemory(Event.pityKey, Event.hardPity - 1);
            PlayerProfile.SetPityInMemory(Event.GuaranteeKey, 1);
            PlayerProfile.AddTickets(1);
            GachaBanners.Pull(Event, 1, GachaBanners.Pay.Tickets, Day0, null, null, new FixedRng(99999));
            long got = PlayerProfile.Tickets - before;
            Assert.AreEqual(GachaBanners.FeaturedDupeTickets, got);
            StringAssert.Contains($"{got} tickets", GachaBanners.DuplicateRule(Event));
            StringAssert.Contains("gun turns into shards", GachaBanners.DuplicateRule(GachaBanners.All[2]));
            StringAssert.Contains("ticket", GachaBanners.DuplicateRule(GachaBanners.All[1]));
        }

        [Test]
        public void PoolPityCountdown_EndsOnTheForcedPull()
        {
            var econ = ScriptableObject.CreateInstance<EconomyConfig>();
            var shards = GachaBanners.All[2];
            int th = econ.weaponPool.pityThreshold;
            Assert.AreEqual(th + 1, GachaBanners.PullsToPoolPity(shards, econ), "fresh: the forced pull is number threshold + 1");
            PlayerProfile.SetPityInMemory(shards.pityKey, th);
            Assert.AreEqual(1, GachaBanners.PullsToPoolPity(shards, econ), "pity == threshold: the next pull is forced");
            StringAssert.Contains($"by pull {th + 1}", GachaBanners.PityRule(shards, econ));
            Object.DestroyImmediate(econ);
        }

        [Test]
        public void EventRateLabels_UseTheGrantAmounts()
        {
            StringAssert.Contains(GachaBanners.EventShards.ToString(), Event.rates[2].label);
            StringAssert.Contains(GachaBanners.EventCoins.ToString(), Event.rates[4].label);
        }
    }
}
