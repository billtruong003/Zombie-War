using System;
using System.Collections.Generic;
using System.Linq;
using BillGameCore;
using NUnit.Framework;
using UnityEngine;

namespace ZombieWar.Tests
{
    /// <summary>Daily Ops, the daily chest and streaks (backlog #19, owner-approved 05/10).</summary>
    public class DailyOpsTests
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

        static readonly DateTime Day = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);

        [SetUp]
        public void SetUp()
        {
            PlayerProfile.StorageOverride = new InMemorySave();
            PlayerProfile.LegacyReadString = _ => "";
            PlayerProfile.LegacyReadInt = _ => 0;
            PlayerProfile.ResetCacheForTests();
            PassMissions.OwnedFamiliesProvider = () => new[] { WeaponClass.Sidearm, WeaponClass.Shotgun };
        }

        [TearDown]
        public void TearDown()
        {
            PassMissions.OwnedFamiliesProvider = null;
            PlayerProfile.StorageOverride = null;
            PlayerProfile.LegacyReadString = k => PlayerPrefs.GetString(k, "");
            PlayerProfile.LegacyReadInt = k => PlayerPrefs.GetInt(k, 0);
            PlayerProfile.ResetCacheForTests();
        }

        [Test]
        public void FourMissionsADay_GunOnesOnlyForOwnedFamilies()
        {
            var owned = new[] { WeaponClass.Sidearm, WeaponClass.Shotgun };
            for (int day = 100; day < 160; day++)
            {
                var ops = DailyOps.For(day, 1234, owned, 5);
                Assert.AreEqual(DailyOps.Count, ops.Count);
                CollectionAssert.AllItemsAreUnique(ops.Select(m => m.id).ToList());
                var gun = ops.Where(m => DailyOps.FamilyOf(m).HasValue).ToList();
                Assert.That(gun.Count, Is.InRange(1, 2), $"day {day}");
                foreach (var m in gun) CollectionAssert.Contains(owned, DailyOps.FamilyOf(m).Value);
                Assert.IsTrue(ops.All(m => m.scope == MissionScope.Daily));
            }
        }

        [Test]
        public void NoOwnedGun_FallsBackToThePistol()
        {
            var ops = DailyOps.For(10, 1, new WeaponClass[0], 1);
            Assert.AreEqual(WeaponClass.Sidearm, DailyOps.FamilyOf(ops[0]));
        }

        [Test]
        public void TwoDaysInARow_NeverDealTheSameSet()
        {
            var owned = new[] { WeaponClass.Sidearm };
            for (int day = 100; day < 130; day++)
            {
                var a = DailyOps.For(day, 7, owned, 3).Select(m => m.id).ToList();
                var b = DailyOps.For(day + 1, 7, owned, 3).Select(m => m.id).ToList();
                CollectionAssert.AreNotEqual(a, b);
            }
        }

        [Test]
        public void IdsDecodeBackIntoTheSameMission()
        {
            foreach (var m in DailyOps.For(42, 99, new[] { WeaponClass.Shotgun, WeaponClass.AssaultRifle }, 9))
            {
                var d = PassMissions.Find(m.id);
                Assert.IsNotNull(d, m.id);
                Assert.AreEqual(m.title, d.title);
                Assert.AreEqual(m.target, d.target);
                Assert.AreEqual(m.metric, d.metric);
                Assert.AreEqual(DailyOps.FamilyOf(m), DailyOps.FamilyOf(d));
            }
            Assert.IsNull(DailyOps.Decode("ops.kill.NotAGun.10"));
            Assert.IsNull(DailyOps.Decode("daily.kill50"));
        }

        [Test]
        public void AGunMission_CountsOnlyWithItsFamily()
        {
            var op = DailyOps.Make(DailyOps.Template.Kill, WeaponClass.Shotgun, 3);
            Assert.IsTrue(PassMissions.CountsWith(op, WeaponClass.Shotgun));
            Assert.IsFalse(PassMissions.CountsWith(op, WeaponClass.Sidearm));
            Assert.IsFalse(PassMissions.CountsWith(op, null));
            var any = DailyOps.Make(DailyOps.Template.Runs, null, 3);
            Assert.IsTrue(PassMissions.CountsWith(any, WeaponClass.Sidearm));
            Assert.IsTrue(PassMissions.CountsWith(any, null));
        }

        [Test]
        public void ClaimingAnOp_PaysShardsToTheGunThatDidIt()
        {
            PlayerProfile.AddOwnedWeapon("gun.a");
            PlayerProfile.SetEquippedWeapon("gun.a");
            var op = DailyOps.Make(DailyOps.Template.Runs, null, 1);
            PlayerProfile.AddMissionProgress(op.id, op.target);
            int before = PlayerProfile.GetWeaponShards("gun.a");
            Assert.IsTrue(PlayerProfile.TryClaimMission(op.id));
            Assert.AreEqual(before + DailyOps.ShardsPerMission, PlayerProfile.GetWeaponShards("gun.a"));
            Assert.AreEqual("gun.a", PlayerProfile.MissionGun(op.id));
        }

        void FinishTodaysOps(DateTime when)
        {
            PlayerProfile.RefreshMissionWindow(when);
            foreach (var m in PassMissions.ActiveFor(when).Where(m => m.scope == MissionScope.Daily))
            {
                PlayerProfile.AddMissionProgress(m.id, m.target);
                Assert.IsTrue(PlayerProfile.TryClaimMission(m.id), m.id);
            }
        }

        [Test]
        public void TheChest_OpensOnceADay_AfterAllFour()
        {
            PlayerProfile.AddOwnedWeapon("gun.a");
            PlayerProfile.SetEquippedWeapon("gun.a");
            PlayerProfile.RefreshMissionWindow(Day);
            Assert.IsFalse(PlayerProfile.CanClaimDailyChest(Day), "not before the four ops");
            FinishTodaysOps(Day);
            long gems = PlayerProfile.Gem, tickets = PlayerProfile.Tickets;
            Assert.IsTrue(PlayerProfile.TryClaimDailyChest(Day, null, out var r));
            Assert.AreEqual(gems + PlayerProfile.DailyChestGems, PlayerProfile.Gem);
            Assert.AreEqual(tickets + PlayerProfile.DailyChestTickets, PlayerProfile.Tickets);
            Assert.AreEqual(PlayerProfile.DailyChestShards, r.shards);
            Assert.AreEqual(1, r.streak);
            Assert.IsFalse(PlayerProfile.TryClaimDailyChest(Day, null, out _), "once a day");
        }

        [Test]
        public void TheStreak_DoublesShardsOnDayThree_AndResetsAfterAMiss()
        {
            PlayerProfile.AddOwnedWeapon("gun.a");
            PlayerProfile.SetEquippedWeapon("gun.a");
            PlayerProfile.DailyChestReward r = default;
            for (int d = 0; d < 3; d++)
            {
                var when = Day.AddDays(d);
                FinishTodaysOps(when);
                Assert.IsTrue(PlayerProfile.TryClaimDailyChest(when, null, out r));
            }
            Assert.AreEqual(3, r.streak);
            Assert.AreEqual(PlayerProfile.DailyChestShards * 2, r.shards);

            var late = Day.AddDays(5);   // two days missed
            FinishTodaysOps(late);
            Assert.IsTrue(PlayerProfile.TryClaimDailyChest(late, null, out r));
            Assert.AreEqual(1, r.streak);
        }

        [Test]
        public void TheSeventhDay_AddsAGunNotOwned()
        {
            PlayerProfile.AddOwnedWeapon("gun.a");
            PlayerProfile.SetEquippedWeapon("gun.a");
            var rare = ScriptableObject.CreateInstance<WeaponData>();
            rare.tier = WeaponTier.Rare;
            typeof(WeaponData).GetField("weaponId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
                ?.SetValue(rare, "gun.rare");
            var guns = new List<WeaponData> { rare };
            PlayerProfile.DailyChestReward r = default;
            for (int d = 0; d < 7; d++)
            {
                var when = Day.AddDays(d);
                FinishTodaysOps(when);
                Assert.IsTrue(PlayerProfile.TryClaimDailyChest(when, guns, out r));
                if (d < 6) Assert.IsNull(r.newGun);
            }
            Assert.AreEqual(7, r.streak);
            Assert.AreEqual("gun.rare", r.newGun);
            Assert.IsTrue(PlayerProfile.IsWeaponOwned("gun.rare"));
            UnityEngine.Object.DestroyImmediate(rare);
        }
    }
}
