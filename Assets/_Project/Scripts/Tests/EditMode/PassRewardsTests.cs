using System.Collections.Generic;
using BillGameCore;
using NUnit.Framework;
using UnityEngine;

namespace ZombieWar.Tests
{
    /// M10 Pass v2: levels from mission XP, free/premium claims, season rollover.
    public class PassRewardsTests
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

        static void GiveXp(int xp)
        {
            // XP only comes from missions; the test seam adds it the same way a claim does.
            PlayerProfile.DailyData.passXp += xp;
        }

        [Test]
        public void Level_FollowsXp_CappedAt30()
        {
            PassRewards.EnsureSeason(Day0);
            Assert.AreEqual(1, PassRewards.Level);
            GiveXp(PassRewards.XpPerLevel * 3 + 10);
            Assert.AreEqual(4, PassRewards.Level);
            Assert.AreEqual(10, PassRewards.XpIntoLevel);
            GiveXp(PassRewards.XpPerLevel * 100);
            Assert.AreEqual(PassRewards.MaxLevel, PassRewards.Level);
        }

        [Test]
        public void Free_ClaimsOnce_PremiumNeedsUnlock()
        {
            PassRewards.EnsureSeason(Day0);
            GiveXp(PassRewards.XpPerLevel * 9);   // level 10
            Assert.IsTrue(PassRewards.Claim(10, false, out var r));
            Assert.AreEqual(PassRewards.Kind.Skin, r.kind);
            Assert.IsTrue(PlayerProfile.IsSkinOwned(PassRewards.FreeSkin));
            Assert.IsFalse(PassRewards.Claim(10, false, out _), "twice");
            Assert.IsFalse(PassRewards.Claim(11, false, out _), "not reached");
            Assert.IsFalse(PassRewards.Claim(1, true, out _), "premium locked");
            PassRewards.UnlockPremium();
            Assert.IsTrue(PassRewards.Claim(1, true, out _));
            Assert.AreEqual(9 + 9, PassRewards.ClaimAll());   // free 1-9 and premium 2-10
            Assert.AreEqual(0, PassRewards.ClaimableCount());
        }

        [Test]
        public void Season_RollsAfter28Days_ResettingXpAndClaims()
        {
            PassRewards.EnsureSeason(Day0);
            GiveXp(PassRewards.XpPerLevel * 2);
            PassRewards.UnlockPremium();
            PassRewards.Claim(1, false, out _);
            Assert.AreEqual(PassRewards.SeasonDays, PassRewards.DaysLeft(Day0));
            PassRewards.EnsureSeason(Day0 + PassRewards.SeasonDays + 3);
            Assert.AreEqual(2, PassRewards.Season);
            Assert.AreEqual(1, PassRewards.Level);
            Assert.IsFalse(PassRewards.IsPremium);
            Assert.IsFalse(PassRewards.IsClaimed(1, false));
            Assert.AreEqual(PassRewards.SeasonDays - 3, PassRewards.DaysLeft(Day0 + PassRewards.SeasonDays + 3));
        }

        [Test]
        public void Skins_EquipOnlyWhenOwned_BonusFollowsSet()
        {
            Assert.IsFalse(PlayerProfile.SetEquippedSkin("weapon.test", "inferno"), "not owned");
            PlayerProfile.AddSkin("inferno");
            Assert.IsTrue(PlayerProfile.SetEquippedSkin("weapon.test", "inferno"));
            Assert.AreEqual("inferno", PlayerProfile.GetEquippedSkin("weapon.test"));
            Assert.IsNull(PlayerProfile.GetEquippedSkin("weapon.other"), "one gun at a time");
            Assert.Greater(Skins.WeaponSkins.DamageBonus("inferno"), Skins.WeaponSkins.DamageBonus("frostbite"));
            Assert.AreEqual(0f, Skins.WeaponSkins.DamageBonus((string)null));
            PlayerProfile.SetEquippedSkin("weapon.test", null);
            Assert.IsNull(PlayerProfile.GetEquippedSkin("weapon.test"));
        }

        [Test]
        public void Lanes_Have30Rewards_WithTheSeasonSkins()
        {
            Assert.AreEqual(PassRewards.Kind.Skin, PassRewards.Free(10).kind);
            Assert.AreEqual(PassRewards.PremiumSkinTop, PassRewards.Premium(30).id);
            for (int l = 1; l <= PassRewards.MaxLevel; l++)
            {
                Assert.Greater(PassRewards.Free(l).amount, 0);
                Assert.Greater(PassRewards.Premium(l).amount, 0);
            }
        }
    }
}
