using System.Collections.Generic;
using BillGameCore;
using NUnit.Framework;
using UnityEngine;

namespace ZombieWar.Tests
{
    /// <summary>Backlog 3a (owner Q2, 05/10): a 3-star gun's spare shards become coin.</summary>
    public class ShardExchangeTests
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

        static WeaponData Gun(string id, WeaponTier tier)
        {
            var w = ScriptableObject.CreateInstance<WeaponData>();
            w.tier = tier;
            typeof(WeaponData).GetField("weaponId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)?.SetValue(w, id);
            return w;
        }

        static void SetStars(string id, int level)
        {
            var data = typeof(PlayerProfile).GetProperty("Data", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public).GetValue(null);
            var list = (List<PlayerProfile.WeaponUpgradeEntry>)data.GetType().GetField("weaponUpgrades").GetValue(data);
            list.Add(new PlayerProfile.WeaponUpgradeEntry { weaponId = id, level = level });
        }

        [Test]
        public void MaxedGunShards_BecomeCoin_OthersStay()
        {
            var legend = Gun("gun.legend", WeaponTier.Legendary);
            var rare = Gun("gun.rare", WeaponTier.Rare);
            PlayerProfile.AddOwnedWeapon("gun.legend");
            PlayerProfile.AddOwnedWeapon("gun.rare");
            SetStars("gun.legend", 3);
            SetStars("gun.rare", 2);
            PlayerProfile.AddWeaponShards("gun.legend", 10);
            PlayerProfile.AddWeaponShards("gun.rare", 7);
            long coin0 = PlayerProfile.Coin;

            var (shards, coin) = PlayerProfile.ExchangeMaxedShards(new List<WeaponData> { legend, rare });

            Assert.AreEqual(10, shards);
            Assert.AreEqual(400, coin, "40 coin per Legendary shard");
            Assert.AreEqual(coin0 + 400, PlayerProfile.Coin);
            Assert.AreEqual(0, PlayerProfile.GetWeaponShards("gun.legend"));
            Assert.AreEqual(7, PlayerProfile.GetWeaponShards("gun.rare"), "a gun below 3 stars keeps its shards");
            Assert.AreEqual((0, 0L), PlayerProfile.ExchangeMaxedShards(new List<WeaponData> { legend, rare }), "nothing twice");
        }
    }
}
