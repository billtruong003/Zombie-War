using System.Collections.Generic;
using BillGameCore;
using NUnit.Framework;
using UnityEngine;
using ZombieWar.UI;

namespace ZombieWar.Tests
{
    /// M10 Profile/Settings data: lifetime stats, a stable player ID, and "Delete my data".
    public class ProfileStatsTests
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

        [Test]
        public void RunStats_Accumulate_PeakKeepsTheBest()
        {
            PlayerProfile.RecordRunStats(120, 5, 300f);
            PlayerProfile.RecordRunStats(80, 3, 100f);
            PlayerProfile.RecordBossDefeated();
            Assert.AreEqual(200, PlayerProfile.TotalKills);
            Assert.AreEqual(5, PlayerProfile.PeakThreat);
            Assert.AreEqual(400f, PlayerProfile.TotalSeconds, 1e-3f);
            Assert.AreEqual(1, PlayerProfile.BossesDefeated);
        }

        [Test]
        public void RunStats_IgnoreBadInput()
        {
            PlayerProfile.RecordRunStats(-5, -1, float.NaN);
            Assert.AreEqual(0, PlayerProfile.TotalKills);
            Assert.AreEqual(0, PlayerProfile.PeakThreat);
            Assert.AreEqual(0f, PlayerProfile.TotalSeconds);
        }

        [Test]
        public void PlayerId_SurvivesReload()
        {
            string id = PlayerProfile.PlayerId;
            Assert.AreEqual(8, id.Length);
            PlayerProfile.ResetCacheForTests();
            Assert.AreEqual(id, PlayerProfile.PlayerId);
            StringAssert.EndsWith(id.Substring(4), PlayerProfile.DisplayName);
        }

        [Test]
        public void DeleteAllData_WipesProgress()
        {
            PlayerProfile.RecordRunStats(50, 2, 60f);
            PlayerProfile.SetDisplayName("Bill");
            PlayerProfile.DeleteAllData();
            Assert.AreEqual(0, PlayerProfile.TotalKills);
            Assert.AreNotEqual("Bill", PlayerProfile.DisplayName);
        }

        [Test]
        public void Formatting()
        {
            Assert.AreEqual("4821 3390", ProfileScreen.FormatId("48213390"));
            Assert.AreEqual("45M", ProfileScreen.PlayTime(45 * 60f));
            Assert.AreEqual("31H", ProfileScreen.PlayTime(31 * 3600f + 50));
        }
    }
}
