using System.Collections.Generic;
using BillGameCore;
using NUnit.Framework;
using UnityEngine;

namespace ZombieWar.Tests
{
    /// <summary>A run Android closes in the background still pays its coin (07/10).</summary>
    public class RunInterruptionTests
    {
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
            PlayerProfile.StorageOverride = null;
            PlayerProfile.LegacyReadString = k => PlayerPrefs.GetString(k, "");
            PlayerProfile.LegacyReadInt = k => PlayerPrefs.GetInt(k, 0);
            PlayerProfile.ResetCacheForTests();
        }

        [TearDown]
        public void TearDown2() => RunInterruption.Clear();

        static RunState RunWithCoin(long coin)
        {
            var run = RunState.Begin();
            run.AddCurrency(PlayerProfile.CurrencyKind.Coin, coin);
            return run;
        }

        [Test]
        public void BackgroundedRun_IsBankedAtNextLaunch()
        {
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 10);
            var run = RunWithCoin(50);
            long carried = run.Coin;
            Assert.Greater(carried, 0);
            RunInterruption.NotePause(true, run);
            long before = PlayerProfile.Coin;
            Assert.AreEqual(carried, RunInterruption.Recover());
            Assert.AreEqual(before + carried, PlayerProfile.Coin);
            Assert.AreEqual(0, RunInterruption.Recover(), "paid once");
        }

        [Test]
        public void ComingBack_OrClosingTheRun_ForgetsIt()
        {
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 10);
            var run = RunWithCoin(50);
            RunInterruption.NotePause(true, run);
            RunInterruption.NotePause(false, run);
            Assert.AreEqual(0, RunInterruption.Recover());

            RunInterruption.NotePause(true, run);
            RunClosure.Close(run, RunOutcome.Died);
            Assert.AreEqual(0, RunInterruption.Recover(), "the closure paid it");
        }
    }
}
