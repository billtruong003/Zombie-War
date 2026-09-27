using System.Collections.Generic;
using BillGameCore;
using NUnit.Framework;
using UnityEngine;
using ZombieWar;

namespace ZombieWar.Tests
{
    /// <summary>
    /// The terminal-run contract for the endless mode: a run ends by death (25% of Coin banked) or by
    /// walking away (0%), exactly once, and Gem is never at risk because it was secured on pickup.
    /// </summary>
    public class RunClosureTests
    {
        // Same in-memory storage the other profile tests use: these assert on real currency totals,
        // so they must not read or write the developer's actual save.
        private class InMemorySave : ISaveService
        {
            public readonly Dictionary<string, string> store = new();
            private int _slot;
            private string K(string key) => $"s{_slot}_{key}";

            public void Set(string key, string val) => store[K(key)] = val;
            public void Set(string key, int val) => store[K(key)] = val.ToString();
            public void Set(string key, float val) => store[K(key)] = val.ToString();
            public void Set(string key, bool val) => store[K(key)] = val ? "1" : "0";
            public void Set<T>(string key, T val) where T : class => store[K(key)] = JsonUtility.ToJson(val);
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

        [SetUp]
        public void SetUp()
        {
            PlayerProfile.StorageOverride = new InMemorySave();
            PlayerProfile.LegacyReadString = _ => "";
            PlayerProfile.LegacyReadInt = _ => 0;
            PlayerProfile.ResetCacheForTests();
        }

        [TearDown]
        public void TearDown()
        {
            RunState.Abandon();

            PlayerProfile.StorageOverride = null;
            PlayerProfile.LegacyReadString = k => PlayerPrefs.GetString(k, "");
            PlayerProfile.LegacyReadInt = k => PlayerPrefs.GetInt(k, 0);
            PlayerProfile.ResetCacheForTests();
        }

        static RunState BeginRunWorth(long coin)
        {
            var run = RunState.Begin();
            run.AddCurrency(PlayerProfile.CurrencyKind.Coin, coin);
            return run;
        }

        [Test]
        public void Death_BanksAllTheCoin()
        {
            long before = PlayerProfile.Coin;
            var run = BeginRunWorth(40);

            var result = RunClosure.Close(run, RunOutcome.Died);

            Assert.IsTrue(result.Closed);
            Assert.AreEqual(RunOutcome.Died, result.Summary.Outcome);
            Assert.AreEqual(40, result.BankedCoin);
            Assert.AreEqual(before + 40, PlayerProfile.Coin);
        }

        [Test]
        public void WalkingAway_BanksAllTheCoin()
        {
            long before = PlayerProfile.Coin;
            var run = BeginRunWorth(40);

            var result = RunClosure.Close(run, RunOutcome.Abandoned);

            Assert.IsTrue(result.Closed);
            Assert.AreEqual(RunOutcome.Abandoned, result.Summary.Outcome);
            Assert.AreEqual(40, result.BankedCoin);
            Assert.AreEqual(before + 40, PlayerProfile.Coin, "owner M8: walking away keeps every coin");
        }

        [Test]
        public void Gem_IsSecuredOnPickup_AndSurvivesEveryEnding()
        {
            long before = PlayerProfile.Gem;
            var run = RunState.Begin();
            run.AddCurrency(PlayerProfile.CurrencyKind.Gem, 3);

            Assert.AreEqual(before + 3, PlayerProfile.Gem, "the gem is in the profile the moment it is picked up");

            var result = RunClosure.Close(run, RunOutcome.Abandoned);
            Assert.AreEqual(3, result.Summary.Gem, "the result still reports what was picked up");
            Assert.AreEqual(before + 3, PlayerProfile.Gem, "and closing never pays it a second time");
        }

        [Test]
        public void Close_IsFirstWins_AndPaysOnce()
        {
            long before = PlayerProfile.Coin;
            var run = BeginRunWorth(40);

            var died = RunClosure.Close(run, RunOutcome.Died);
            var walked = RunClosure.Close(run, RunOutcome.Abandoned);

            Assert.IsTrue(died.Closed);
            Assert.IsFalse(walked.Closed, "a second ending in the same frame must be a no-op");
            Assert.AreEqual(RunOutcome.Died, run.Outcome);
            Assert.AreEqual(before + 40, PlayerProfile.Coin, "paid once, not twice");
        }

        [Test]
        public void Close_RecordsTheLongestSurvivalOnly()
        {
            var first = RunState.Begin();
            first.Tick(90f);
            Assert.IsTrue(RunClosure.Close(first, RunOutcome.Died).NewSurvivalRecord);
            Assert.AreEqual(90f, PlayerProfile.BestSurvivalSeconds, 0.001f);

            var shorter = RunState.Begin();
            shorter.Tick(30f);
            Assert.IsFalse(RunClosure.Close(shorter, RunOutcome.Died).NewSurvivalRecord);
            Assert.AreEqual(90f, PlayerProfile.BestSurvivalSeconds, 0.001f);
        }

        [Test]
        public void Close_RejectsNoRunAndInProgress()
        {
            Assert.IsFalse(RunClosure.Close(null, RunOutcome.Died).Closed);
            Assert.IsFalse(RunClosure.Close(BeginRunWorth(0), RunOutcome.InProgress).Closed);
        }
    }
}
