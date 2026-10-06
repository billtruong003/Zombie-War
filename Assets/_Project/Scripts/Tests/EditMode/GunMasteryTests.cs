using System.Collections.Generic;
using BillGameCore;
using NUnit.Framework;
using UnityEngine;
using ZombieWar.Skills;

namespace ZombieWar.Tests
{
    /// <summary>Gun mastery (backlog #21, owner-approved 05/10).</summary>
    public class GunMasteryTests
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
        public void TenLevels_OnRisingThresholds()
        {
            Assert.AreEqual(0, GunMastery.LevelFor(0));
            Assert.AreEqual(1, GunMastery.LevelFor(100));
            Assert.AreEqual(4, GunMastery.LevelFor(999));
            Assert.AreEqual(5, GunMastery.LevelFor(1000));
            Assert.AreEqual(10, GunMastery.LevelFor(4000));
            Assert.AreEqual(10, GunMastery.LevelFor(int.MaxValue));
            for (int l = 1; l < GunMastery.MaxLevel; l++)
                Assert.Greater(GunMastery.XpToReach(l + 1) - GunMastery.XpToReach(l), GunMastery.XpToReach(l) - GunMastery.XpToReach(l - 1) - 1);
        }

        [Test]
        public void RunXp_KillsAndMinutes_PistolFaster_DailyOpsDouble()
        {
            Assert.AreEqual(300, GunMastery.XpForRun(100, 600f, WeaponClass.AssaultRifle, false));   // 100 + 20 x 10
            Assert.AreEqual(450, GunMastery.XpForRun(100, 600f, WeaponClass.Sidearm, false));
            Assert.AreEqual(600, GunMastery.XpForRun(100, 600f, WeaponClass.AssaultRifle, true));
        }

        [Test]
        public void LevellingUp_PaysEveryLevelCrossed()
        {
            long coin = PlayerProfile.Coin, gem = PlayerProfile.Gem;
            var (before, after) = PlayerProfile.AddMasteryXp("gun.a", 1000);   // 0 -> 5
            Assert.AreEqual(0, before); Assert.AreEqual(5, after);
            Assert.AreEqual(10 + 15, PlayerProfile.GetWeaponShards("gun.a"));
            Assert.AreEqual(coin + 500 + 1000, PlayerProfile.Coin);
            Assert.AreEqual(gem + 30, PlayerProfile.Gem);
            Assert.AreEqual(5, PlayerProfile.MasteryLevel("gun.a"));
            var (b2, a2) = PlayerProfile.AddMasteryXp("gun.a", 1);
            Assert.AreEqual(5, b2); Assert.AreEqual(5, a2);
        }

        [Test]
        public void FamilyBonuses_TierAtFiveAndTen_ForEveryGun()
        {
            var b = GunMastery.BonusesFor(new Dictionary<WeaponClass, int> { { WeaponClass.Sidearm, 5 }, { WeaponClass.Shotgun, 10 }, { WeaponClass.SMG, 4 } });
            Assert.AreEqual(0.05f, b.crit, 1e-4f);
            Assert.AreEqual(0.10f, b.maxHealth, 1e-4f);
            Assert.AreEqual(0f, b.moveSpeed, 1e-4f, "level 4 pays no family bonus");
            Assert.AreEqual("+5% crit chance for every gun", GunMastery.BonusText(WeaponClass.Sidearm, 1));
        }

        [Test]
        public void AccountBonuses_ReachTheRunStats()
        {
            var run = new SkillRuntime { UnlockLevel = int.MaxValue };
            float dmg = run.DamageMultiplier, area = run.AreaMultiplier;
            var b = new GunMastery.Bonuses(); b.damage = 0.05f; b.area = 0.1f;
            run.ApplyAccount(b, null);
            Assert.AreEqual(dmg * 1.05f, run.DamageMultiplier, 1e-4f);
            Assert.AreEqual(area + 0.1f, run.AreaMultiplier, 1e-4f);
            run.Reset();
            // The map's player equips in Start, before RunState.Begin resets the build (07/10: a reset
            // that cleared them left every run without mastery).
            Assert.AreEqual(dmg * 1.05f, run.DamageMultiplier, 1e-4f, "the bonuses belong to the equipped gun and survive the run reset");
        }

        [Test]
        public void AccountMaxHealth_IsAppliedOncePerPlayer()
        {
            var run = new SkillRuntime { UnlockLevel = int.MaxValue };
            var b = new GunMastery.Bonuses(); b.maxHealth = 0.1f;
            var first = new GameObject("p1").AddComponent<Health>();
            var second = new GameObject("p2").AddComponent<Health>();
            first.Configure(100f); second.Configure(100f);
            try
            {
                float baseMax = first.Max;
                run.ApplyAccount(b, first);
                run.ApplyAccount(b, first);   // a gun swap re-applies the account
                Assert.AreEqual(baseMax * 1.1f, first.Max, 1e-3f, "once per player");
                run.Reset();
                run.ApplyAccount(b, second);  // next run, new player
                Assert.AreEqual(second.Max > baseMax, true, "the next run's player gets it too");
            }
            finally { Object.DestroyImmediate(first.gameObject); Object.DestroyImmediate(second.gameObject); }
        }
    }
}
