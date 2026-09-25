using System.Collections.Generic;
using System.Reflection;
using BillGameCore;
using NUnit.Framework;
using UnityEngine;
using ZombieWar;

namespace ZombieWar.Tests
{
    /// LoadoutState.TryEquip for the one-weapon contract: ownership, authoring gate, persistence.
    /// Storage and legacy reads are faked as in PlayerProfileTests - the real PlayerPrefs are untouched.
    public class LoadoutEquipTests
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

        private readonly List<WeaponData> _created = new();
        private WeaponData _pistol;
        private WeaponData _rifle;
        private WeaponData _shotgun;

        [SetUp]
        public void SetUp()
        {
            PlayerProfile.StorageOverride = new InMemorySave();
            PlayerProfile.LegacyReadString = _ => "";
            PlayerProfile.LegacyReadInt = _ => 0;
            PlayerProfile.ResetCacheForTests();

            _pistol = MakeWeapon("weapon.sidearm.pistol_a", twoHanded: false);
            _rifle = MakeWeapon("weapon.assault_rifle.generic", twoHanded: true);
            _shotgun = MakeWeapon("weapon.shotgun.generic", twoHanded: true);
        }

        [TearDown]
        public void TearDown()
        {
            PlayerProfile.StorageOverride = null;
            PlayerProfile.LegacyReadString = k => PlayerPrefs.GetString(k, "");
            PlayerProfile.LegacyReadInt = k => PlayerPrefs.GetInt(k, 0);
            PlayerProfile.ResetCacheForTests();
            foreach (var wd in _created)
                if (wd != null) Object.DestroyImmediate(wd);
            _created.Clear();
        }

        private WeaponData MakeWeapon(string id, bool twoHanded)
        {
            var wd = ScriptableObject.CreateInstance<WeaponData>();
            typeof(WeaponData).GetField("weaponId", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(wd, id);
            wd.twoHanded = twoHanded;
            _created.Add(wd);
            return wd;
        }

        [Test]
        public void OwnedWeapon_Equips_AndPersistsCanonicalId()
        {
            PlayerProfile.AddOwnedWeapon(_rifle.WeaponId);
            var result = LoadoutState.TryEquip(_rifle);
            Assert.AreEqual(LoadoutState.EquipResult.Equipped, result);
            Assert.AreEqual("weapon.assault_rifle.generic", LoadoutState.WeaponId);
        }

        [Test]
        public void AnyFamily_CanBeTheRunWeapon()
        {
            // One weapon per run: a sidearm and a two-handed gun are equally valid choices.
            PlayerProfile.AddOwnedWeapon(_pistol.WeaponId);
            PlayerProfile.AddOwnedWeapon(_shotgun.WeaponId);

            Assert.AreEqual(LoadoutState.EquipResult.Equipped, LoadoutState.TryEquip(_shotgun));
            Assert.AreEqual("weapon.shotgun.generic", LoadoutState.WeaponId);
            Assert.AreEqual(LoadoutState.EquipResult.Equipped, LoadoutState.TryEquip(_pistol));
            Assert.AreEqual("weapon.sidearm.pistol_a", LoadoutState.WeaponId, "equipping replaces, never adds");
        }

        [Test]
        public void Reopen_RestoresEquippedState()
        {
            PlayerProfile.AddOwnedWeapon(_rifle.WeaponId);
            LoadoutState.TryEquip(_rifle);
            PlayerProfile.ResetCacheForTests(); // same StorageOverride -> reload from the store
            Assert.AreEqual("weapon.assault_rifle.generic", LoadoutState.WeaponId);
        }

        [Test]
        public void LockedWeapon_Rejected_NoStateChange()
        {
            string before = LoadoutState.WeaponId;
            var result = LoadoutState.TryEquip(_rifle); // not owned
            Assert.AreEqual(LoadoutState.EquipResult.NotOwned, result);
            Assert.AreEqual(before, LoadoutState.WeaponId);
        }

        [Test]
        public void ReEquip_IsIdempotent()
        {
            PlayerProfile.AddOwnedWeapon(_shotgun.WeaponId);
            LoadoutState.TryEquip(_shotgun);
            Assert.AreEqual(LoadoutState.EquipResult.Equipped, LoadoutState.TryEquip(_shotgun));
            Assert.AreEqual("weapon.shotgun.generic", LoadoutState.WeaponId);
        }

        [Test]
        public void InvalidInputs_FailSafely()
        {
            Assert.AreEqual(LoadoutState.EquipResult.InvalidWeapon, LoadoutState.TryEquip(null));
            var noId = MakeWeapon("", twoHanded: true);
            Assert.AreEqual(LoadoutState.EquipResult.InvalidWeapon, LoadoutState.TryEquip(noId));
        }

        [Test]
        public void MissingCatalogWeapon_ResolveReturnsNull_NoReplacement()
        {
            PlayerProfile.SetEquippedWeapon("weapon.gone.forever");
            var arsenal = new List<WeaponData> { _pistol, _rifle, _shotgun };
            Assert.IsNull(LoadoutState.Resolve("weapon.gone.forever", arsenal));
            Assert.AreEqual("weapon.gone.forever", LoadoutState.WeaponId, "an unknown id is kept, never replaced");
        }
    }
}
