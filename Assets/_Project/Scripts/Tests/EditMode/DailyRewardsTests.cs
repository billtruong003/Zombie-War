using System.Collections.Generic;
using BillGameCore;
using NUnit.Framework;
using UnityEngine;

namespace ZombieWar.Tests
{
    /// M10 Daily rules: welcome check-in once per day, 28-day stamp card, make-ups, cycle reset.
    public class DailyRewardsTests
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
        public void Welcome_OncePerDay_SevenDaysThenGone()
        {
            long c0 = PlayerProfile.Coin;
            Assert.IsTrue(DailyRewards.ClaimWelcome(Day0, out var r1));
            Assert.AreEqual(DailyRewards.Kind.Coin, r1.kind);
            Assert.AreEqual(c0 + 500, PlayerProfile.Coin);
            Assert.IsFalse(DailyRewards.ClaimWelcome(Day0, out _), "second claim the same day");
            // Skipping days does not reset the welcome streak.
            for (int d = 1; d < DailyRewards.WelcomeDays; d++) Assert.IsTrue(DailyRewards.ClaimWelcome(Day0 + d * 2, out _));
            Assert.IsFalse(DailyRewards.WelcomeActive);
            Assert.AreEqual(3, PlayerProfile.Tickets);
        }

        [Test]
        public void Stamp_OncePerDay_MilestonesPay()
        {
            long c0 = PlayerProfile.Coin, g0 = PlayerProfile.Gem;
            for (int d = 0; d < 7; d++) Assert.IsTrue(DailyRewards.Stamp(Day0 + d, out _));
            Assert.IsFalse(DailyRewards.Stamp(Day0 + 6, out _));
            Assert.AreEqual(7, DailyRewards.Stamps);
            Assert.AreEqual(c0 + 600, PlayerProfile.Coin);   // six plain stamps
            Assert.AreEqual(g0 + 50, PlayerProfile.Gem);     // day 7 milestone
        }

        [Test]
        public void MakeUp_CostsGems_LimitedPerCycle()
        {
            DailyRewards.Stamp(Day0, out _);
            PlayerProfile.TrySpend(PlayerProfile.CurrencyKind.Gem, PlayerProfile.Gem);
            int today = Day0 + 4;                     // 3 missed days
            Assert.AreEqual(3, DailyRewards.Missed(today));
            Assert.IsFalse(DailyRewards.MakeUp(today, false, out _), "no gems");
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Gem, 100);
            Assert.IsTrue(DailyRewards.MakeUp(today, false, out _));
            Assert.IsTrue(DailyRewards.MakeUp(today, true, out _));
            Assert.IsFalse(DailyRewards.CanMakeUp(today), "two per cycle");
            Assert.AreEqual(1, DailyRewards.Missed(today));
            Assert.AreEqual(80, PlayerProfile.Gem);
        }

        [Test]
        public void Cycle_ResetsAfter28Days_FullCardGivesFrame()
        {
            for (int d = 0; d < 28; d++) DailyRewards.Stamp(Day0 + d, out _);
            Assert.AreEqual(28, DailyRewards.Stamps);
            Assert.Contains(DailyRewards.StampMasterFrame, (System.Collections.ICollection)PlayerProfile.OwnedFrames);
            Assert.IsFalse(DailyRewards.CanStamp(Day0 + 27));
            DailyRewards.EnsureCycle(Day0 + 28);
            Assert.AreEqual(0, DailyRewards.Stamps);
            Assert.IsTrue(DailyRewards.CanStamp(Day0 + 28));
        }

        [Test]
        public void Claimable_CountsWelcomeAndStamp()
        {
            Assert.AreEqual(2, DailyRewards.ClaimableCount(Day0));
            DailyRewards.Stamp(Day0, out _);
            Assert.AreEqual(1, DailyRewards.ClaimableCount(Day0));
        }
    }
}
