using System.Collections.Generic;
using System.Linq;
using BillGameCore;
using NUnit.Framework;
using UnityEngine;

namespace ZombieWar.Tests
{
    /// <summary>Achievements (backlog #22): the table, once-only gems, profile checks.</summary>
    public class AchievementTests
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

        [Test]
        public void Achievements_EachWithGems_RecordedLinesForTheFirstTen()
        {
            Assert.AreEqual(15, Achievements.All.Count);
            CollectionAssert.AllItemsAreUnique(Achievements.All.Select(a => a.id).ToList());
            var json = Resources.Load<TextAsset>("VO/vo_subtitles");
            Assert.IsNotNull(json);
            foreach (var a in Achievements.All)
            {
                Assert.Greater(a.gems, 0, a.id);
                if (a.voice != null) StringAssert.Contains($"\"{a.voice}\"", json.text, $"{a.id} has no recorded line");
            }
            // The five gun-collection achievements (06/10) wait for the owner's next voice pick.
            Assert.AreEqual(5, Achievements.All.Count(a => a.voice == null));
        }

        [Test]
        public void Unlock_PaysOnce()
        {
            long gem = PlayerProfile.Gem;
            int fired = 0;
            void Count(Achievements.Def _) => fired++;
            Achievements.Unlocked += Count;
            try
            {
                Assert.IsTrue(Achievements.Unlock(Achievements.FirstAlpha));
                Assert.IsFalse(Achievements.Unlock(Achievements.FirstAlpha));
            }
            finally { Achievements.Unlocked -= Count; }
            Assert.AreEqual(gem, PlayerProfile.Gem, "the gems wait to be claimed");
            Assert.AreEqual(1, Achievements.ClaimableCount);
            Assert.IsTrue(Achievements.ClaimReward(Achievements.FirstAlpha));
            Assert.IsFalse(Achievements.ClaimReward(Achievements.FirstAlpha));
            Assert.AreEqual(gem + Achievements.Find(Achievements.FirstAlpha).gems, PlayerProfile.Gem);
            Assert.AreEqual(0, Achievements.ClaimableCount);
            Assert.AreEqual(1, fired);
            Assert.IsTrue(Achievements.IsUnlocked(Achievements.FirstAlpha));
            Assert.AreEqual(1, Achievements.UnlockedCount);
            Assert.IsFalse(Achievements.Unlock("not.an.achievement"));
        }

        [Test]
        public void ProfileChecks_TenGunsAndFiftyThousandCoins()
        {
            for (int i = 0; i < 9; i++) PlayerProfile.AddOwnedWeapon("g" + i);
            Achievements.CheckProfile();
            Assert.IsFalse(Achievements.IsUnlocked(Achievements.Guns10));
            PlayerProfile.AddOwnedWeapon("g9");
            Achievements.CheckProfile();
            Assert.IsTrue(Achievements.IsUnlocked(Achievements.Guns10));

            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 50000);
            Achievements.CheckProfile();
            Assert.IsTrue(Achievements.IsUnlocked(Achievements.Coins50K));
        }
    }
}
