using System.Collections.Generic;
using BillGameCore;
using NUnit.Framework;
using UnityEngine;

namespace ZombieWar.Tests
{
    /// <summary>Gun crates (backlog 3b, owner 05/10 night): crates give shards, shards unlock guns.</summary>
    public class GunCratesTests
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

        sealed class SeqRng : GachaService.IRng
        {
            readonly System.Random _r;
            public SeqRng(int seed) { _r = new System.Random(seed); }
            public int Range(int maxExclusive) => maxExclusive <= 0 ? 0 : _r.Next(maxExclusive);
        }

        static WeaponData Gun(string id, WeaponTier tier)
        {
            var w = ScriptableObject.CreateInstance<WeaponData>();
            w.tier = tier;
            typeof(WeaponData).GetField("weaponId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)?.SetValue(w, id);
            return w;
        }

        static List<WeaponData> Roster()
        {
            var list = new List<WeaponData>();
            for (int t = 0; t < 5; t++) for (int i = 0; i < 3; i++) list.Add(Gun($"gun.t{t}.{i}", (WeaponTier)t));
            return list;
        }

        [Test]
        public void CoinCrate_NeverLegendary_EpicWithinForty()
        {
            var guns = Roster(); var rng = new SeqRng(3);
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, GunCrates.CoinMulti * 4);
            int epics = 0;
            for (int k = 0; k < 4; k++)
                foreach (var r in GunCrates.Pull(GunCrates.Crate.Coin, 10, false, guns, rng))
                {
                    Assert.AreNotEqual(WeaponTier.Legendary, r.tier);
                    if (r.tier == WeaponTier.Epic) epics++;
                }
            Assert.Greater(epics, 0, "an Epic within 40 pulls");
        }

        [Test]
        public void GemCrate_WholeLegendaryByNinety()
        {
            var guns = Roster(); var rng = new SeqRng(11);
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Gem, GunCrates.GemMulti * 9);
            bool legend = false;
            for (int k = 0; k < 9 && !legend; k++)
                foreach (var r in GunCrates.Pull(GunCrates.Crate.Gem, 10, false, guns, rng))
                    if (r.tier == WeaponTier.Legendary && r.wholeGun) legend = true;
            Assert.IsTrue(legend);
        }

        [Test]
        public void ShardsUnlockTheGun_AtTheThreshold()
        {
            var guns = new List<WeaponData> { Gun("gun.c", WeaponTier.Common) };
            var rng = new SeqRng(1);
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, GunCrates.CoinSingle * 7);
            bool unlocked = false;
            for (int i = 0; i < 7; i++)   // 3-5 shards a pull (one gun in the catalog); Common needs 20
                foreach (var r in GunCrates.Pull(GunCrates.Crate.Coin, 1, false, guns, rng)) unlocked |= r.unlocked;
            Assert.IsTrue(unlocked);
            Assert.IsTrue(PlayerProfile.IsWeaponOwned("gun.c"));
            Assert.Less(PlayerProfile.GetWeaponShards("gun.c"), 20, "the unlock spent its 20 shards");
        }

        [Test]
        public void ShardsLeanTowardTheGunBeingCollected()
        {
            var guns = new List<WeaponData> { Gun("gun.a", WeaponTier.Rare), Gun("gun.b", WeaponTier.Rare), Gun("gun.c", WeaponTier.Rare) };
            PlayerProfile.AddWeaponShards("gun.b", 4);
            var rng = new SeqRng(5);
            int b = 0;
            for (int i = 0; i < 200; i++) if (GunCrates.PickGun(WeaponTier.Rare, guns, rng).WeaponId == "gun.b") b++;
            Assert.Greater(b, 100, "about 60 % to the collected gun");
        }

        [Test]
        public void CannotPull_WithoutTheMoney()
        {
            if (PlayerProfile.Coin > 0) PlayerProfile.TrySpend(PlayerProfile.CurrencyKind.Coin, PlayerProfile.Coin);
            Assert.IsNull(GunCrates.Pull(GunCrates.Crate.Coin, 1, false, Roster(), new SeqRng(1)));
        }
    }
}
