using System;
using System.Collections.Generic;
using System.Reflection;
using BillGameCore;
using NUnit.Framework;
using UnityEngine;
using ZombieWar;

namespace ZombieWar.Tests
{
    /// EditMode tests cho PlayerProfile + migration. Storage va legacy-PlayerPrefs deu duoc gia lap
    /// (in-memory) — test KHONG dung PlayerPrefs that, khong dung cham save that cua dev.
    public class PlayerProfileTests
    {
        // ISaveService in-memory: giong SaveService that (prefix s{slot}_, JsonUtility) nhung
        // luu Dictionary de test co the doc/ghi/lam hong du lieu tuy y.
        private class InMemorySave : ISaveService
        {
            public readonly Dictionary<string, string> store = new();
            public int flushCount;
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
            public void Flush() => flushCount++;
        }

        private InMemorySave _save;
        private Dictionary<string, string> _legacyStrings;
        private Dictionary<string, int> _legacyInts;
        private readonly List<WeaponData> _createdAssets = new();

        [SetUp]
        public void SetUp()
        {
            _save = new InMemorySave();
            _legacyStrings = new Dictionary<string, string>();
            _legacyInts = new Dictionary<string, int>();
            PlayerProfile.StorageOverride = _save;
            PlayerProfile.LegacyReadString = k => _legacyStrings.TryGetValue(k, out var v) ? v : "";
            PlayerProfile.LegacyReadInt = k => _legacyInts.TryGetValue(k, out var v) ? v : 0;
            PlayerProfile.ResetCacheForTests();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerProfile.StorageOverride = null;
            PlayerProfile.LegacyReadString = k => PlayerPrefs.GetString(k, "");
            PlayerProfile.LegacyReadInt = k => PlayerPrefs.GetInt(k, 0);
            PlayerProfile.ResetCacheForTests();
            foreach (var wd in _createdAssets)
                if (wd != null) UnityEngine.Object.DestroyImmediate(wd);
            _createdAssets.Clear();
        }

        // WeaponData.weaponId/legacyAliases la private serialized — set qua reflection, KHONG dung
        // asset that (instance in-memory, DestroyImmediate o TearDown, khong mutate asset nao).
        private WeaponData MakeWeapon(string id, bool twoHanded, int catalogOrder, params string[] aliases)
        {
            var wd = ScriptableObject.CreateInstance<WeaponData>();
            typeof(WeaponData).GetField("weaponId", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(wd, id);
            typeof(WeaponData).GetField("catalogOrder", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(wd, catalogOrder);
            typeof(WeaponData).GetField("legacyAliases", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(wd, aliases);
            wd.twoHanded = twoHanded;
            _createdAssets.Add(wd);
            return wd;
        }

        private List<WeaponData> DefaultArsenal() => new()
        {
            MakeWeapon("weapon.sidearm.pistol_a", false, 0, "WD_Pistol"),
            MakeWeapon("weapon.smg.generic", false, 1, "WD_SMG"),
            MakeWeapon("weapon.assault_rifle.generic", true, 2, "WD_Rifle"),
            MakeWeapon("weapon.shotgun.generic", true, 3, "WD_Shotgun"),
        };

        private void ReloadFromStorage() => PlayerProfile.ResetCacheForTests();

        // ===== Fresh profile =====

        [Test]
        public void FreshProfile_HasSafeDefaults_AndPersists()
        {
            Assert.IsFalse(PlayerProfile.HasProfile);
            Assert.AreEqual(0, PlayerProfile.Coin);
            Assert.AreEqual(0, PlayerProfile.Gold);
            Assert.AreEqual(0, PlayerProfile.Gem);
            Assert.IsEmpty(PlayerProfile.OwnedWeaponIds);
            Assert.AreEqual("", PlayerProfile.GetWeaponSlot(0));
            Assert.IsTrue(PlayerProfile.HasProfile, "Truy cap dau tien phai tao va luu profile.");
            StringAssert.Contains("\"version\":1", _save.store[$"s0_{PlayerProfile.SaveKey}"]);
        }

        [Test]
        public void RoundTrip_PersistsAllDomains()
        {
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 123);
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Gem, 7);
            PlayerProfile.AddOwnedWeapon("weapon.smg.generic");
            PlayerProfile.SetWeaponSlot(0, "weapon.sidearm.pistol_a");
            PlayerProfile.SetWeaponSlot(1, "weapon.assault_rifle.generic");
            PlayerProfile.SetPart("Hair", "guid-hair-01");
            PlayerProfile.AddOwnedCostume("guid-hair-01");

            ReloadFromStorage();

            Assert.AreEqual(123, PlayerProfile.Coin);
            Assert.AreEqual(7, PlayerProfile.Gem);
            Assert.IsTrue(PlayerProfile.IsWeaponOwned("weapon.smg.generic"));
            Assert.AreEqual("weapon.sidearm.pistol_a", PlayerProfile.GetWeaponSlot(0));
            Assert.AreEqual("weapon.assault_rifle.generic", PlayerProfile.GetWeaponSlot(1));
            Assert.AreEqual("guid-hair-01", PlayerProfile.GetPart("Hair"));
            Assert.IsTrue(PlayerProfile.IsCostumeOwned("guid-hair-01"));
        }

        // ===== Migration =====

        [Test]
        public void Migration_CanonicalIds_CopiedVerbatim()
        {
            _legacyStrings["zw.loadout"] =
                "{\"pistol\":\"weapon.sidearm.pistol_a\",\"longA\":\"weapon.assault_rifle.generic\",\"longB\":\"\"," +
                "\"parts\":[{\"slot\":\"Hair\",\"guid\":\"guid-hair-01\"}]}";
            _legacyInts["wallet_coin"] = 250;
            _legacyInts["wallet_gold"] = 40;

            Assert.AreEqual("weapon.sidearm.pistol_a", PlayerProfile.GetWeaponSlot(0));
            Assert.AreEqual("weapon.assault_rifle.generic", PlayerProfile.GetWeaponSlot(1));
            Assert.AreEqual("", PlayerProfile.GetWeaponSlot(2), "Slot trong hop le phai giu trong.");
            Assert.AreEqual(250, PlayerProfile.Coin);
            Assert.AreEqual(40, PlayerProfile.Gold);
            Assert.AreEqual(0, PlayerProfile.Gem);
            Assert.AreEqual("guid-hair-01", PlayerProfile.GetPart("Hair"));
            Assert.IsTrue(PlayerProfile.IsCostumeOwned("guid-hair-01"),
                "Part dang trang bi phai duoc seed owned de giu ngoai hinh.");
        }

        [Test]
        public void Migration_LegacyWeaponNames_CanonicalizedOnEnsureValidLoadout()
        {
            _legacyStrings["zw.loadout"] = "{\"pistol\":\"WD_Pistol\",\"longA\":\"WD_Rifle\",\"longB\":\"\",\"parts\":[]}";

            PlayerProfile.EnsureValidLoadout(DefaultArsenal());

            Assert.AreEqual("weapon.sidearm.pistol_a", PlayerProfile.GetWeaponSlot(0));
            Assert.AreEqual("weapon.assault_rifle.generic", PlayerProfile.GetWeaponSlot(1));
            Assert.IsTrue(PlayerProfile.IsWeaponOwned("weapon.sidearm.pistol_a"));
            Assert.IsTrue(PlayerProfile.IsWeaponOwned("weapon.assault_rifle.generic"));

            ReloadFromStorage();
            Assert.AreEqual("weapon.sidearm.pistol_a", PlayerProfile.GetWeaponSlot(0), "Id chuan phai duoc luu lai.");
        }

        [Test]
        public void Migration_CorruptLoadoutJson_RecoversAndStillImportsWallet()
        {
            _legacyStrings["zw.loadout"] = "{{{not json";
            _legacyInts["wallet_coin"] = 99;

            Assert.AreEqual("", PlayerProfile.GetWeaponSlot(0));
            Assert.AreEqual(99, PlayerProfile.Coin);
            Assert.AreEqual("{{{not json", PlayerProfile.LegacyReadString("zw.loadout"), "Key cu phai giu nguyen.");
        }

        [Test]
        public void CorruptProfile_RebuildsFromLegacy_WithoutThrowing()
        {
            _save.store[$"s0_{PlayerProfile.SaveKey}"] = "{broken";
            _legacyInts["wallet_gold"] = 15;

            Assert.DoesNotThrow(() => { var _ = PlayerProfile.Gold; });
            Assert.AreEqual(15, PlayerProfile.Gold);
        }

        [Test]
        public void PartialProfileJson_MissingCollections_BecomeSafeEmpty()
        {
            _save.store[$"s0_{PlayerProfile.SaveKey}"] = "{\"coin\":7}";

            Assert.AreEqual(7, PlayerProfile.Coin);
            Assert.IsEmpty(PlayerProfile.OwnedWeaponIds);
            Assert.IsEmpty(PlayerProfile.Parts);
            Assert.AreEqual("", PlayerProfile.GetWeaponSlot(0));
        }

        [Test]
        public void UnknownWeaponId_KeptInSave_NotReplaced_NotOwned()
        {
            _legacyStrings["zw.loadout"] = "{\"pistol\":\"weapon.unknown.gone\",\"longA\":\"\",\"longB\":\"\",\"parts\":[]}";

            PlayerProfile.EnsureValidLoadout(DefaultArsenal());

            Assert.AreEqual("weapon.unknown.gone", PlayerProfile.GetWeaponSlot(0),
                "Id la khong duoc lang le thay bang sung khac.");
            Assert.IsFalse(PlayerProfile.IsWeaponOwned("weapon.unknown.gone"));
            Assert.IsFalse(PlayerProfile.IsWeaponOwned("weapon.sidearm.pistol_a"),
                "Slot 0 co id (du la) -> khong seed starter de len.");
        }

        [Test]
        public void NegativeLegacyCurrency_ClampedToZero()
        {
            _legacyInts["wallet_coin"] = -50;
            Assert.AreEqual(0, PlayerProfile.Coin);
        }

        [Test]
        public void Migration_RunsOnce_NoDoubleImport()
        {
            _legacyInts["wallet_coin"] = 100;
            Assert.AreEqual(100, PlayerProfile.Coin); // lan 1: import + save profile

            _legacyInts["wallet_coin"] = 999;         // doi key cu SAU migration
            ReloadFromStorage();
            Assert.AreEqual(100, PlayerProfile.Coin, "Da co profile -> khong import lai tu legacy.");
        }

        [Test]
        public void DuplicateOwnedIds_InStoredJson_AreNormalized()
        {
            _save.store[$"s0_{PlayerProfile.SaveKey}"] =
                "{\"version\":1,\"ownedWeaponIds\":[\"a\",\"a\",\"\",\"b\",\"a\"]}";

            Assert.AreEqual(2, PlayerProfile.OwnedWeaponIds.Count);
            Assert.IsTrue(PlayerProfile.IsWeaponOwned("a"));
            Assert.IsTrue(PlayerProfile.IsWeaponOwned("b"));
        }

        // ===== Wallet rules =====

        [Test]
        public void Wallet_SpendRules_AtomicAndSafe()
        {
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 100);

            Assert.IsFalse(PlayerProfile.TrySpend(PlayerProfile.CurrencyKind.Coin, 101), "Khong du tien -> false.");
            Assert.AreEqual(100, PlayerProfile.Coin, "That bai khong duoc doi so du.");
            Assert.IsFalse(PlayerProfile.TrySpend(PlayerProfile.CurrencyKind.Coin, -1), "So am -> false.");
            Assert.IsTrue(PlayerProfile.TrySpend(PlayerProfile.CurrencyKind.Coin, 100));
            Assert.AreEqual(0, PlayerProfile.Coin);
        }

        [Test]
        public void Wallet_OverflowClampsAtMaxValue()
        {
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Gold, long.MaxValue - 5);
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Gold, 10);
            Assert.AreEqual(long.MaxValue, PlayerProfile.Gold);

            ReloadFromStorage();
            Assert.AreEqual(long.MaxValue, PlayerProfile.Gold, "long phai round-trip khong mat chinh xac.");
        }

        // ===== Starter seeding + slot rules =====

        [Test]
        public void FreshProfile_EnsureValidLoadout_SeedsFirstOneHandedAsStarter()
        {
            // List CO TINH xao thu tu: SMG (1-tay, order 1) dung TRUOC pistol_a (order 0) —
            // starter phai chon theo CatalogOrder nho nhat, khong theo vi tri trong list.
            var arsenal = new List<WeaponData>
            {
                MakeWeapon("weapon.assault_rifle.generic", true, 2),
                MakeWeapon("weapon.smg.generic", false, 1),
                MakeWeapon("weapon.sidearm.pistol_a", false, 0),
            };
            PlayerProfile.EnsureValidLoadout(arsenal);

            Assert.AreEqual("weapon.sidearm.pistol_a", PlayerProfile.GetWeaponSlot(0),
                "Starter = khau 1-tay co CatalogOrder nho nhat (khop Player roster).");
            Assert.IsTrue(PlayerProfile.IsWeaponOwned("weapon.sidearm.pistol_a"), "Starter phai duoc own.");
            Assert.AreEqual("", PlayerProfile.GetWeaponSlot(1), "Long slot duoc phep trong.");
            Assert.AreEqual("", PlayerProfile.GetWeaponSlot(2));
        }

        [Test]
        public void EnsureValidLoadout_Idempotent()
        {
            var arsenal = DefaultArsenal();
            PlayerProfile.EnsureValidLoadout(arsenal);
            string json1 = _save.store[$"s0_{PlayerProfile.SaveKey}"];
            PlayerProfile.EnsureValidLoadout(arsenal);
            Assert.AreEqual(json1, _save.store[$"s0_{PlayerProfile.SaveKey}"], "Chay lai khong duoc doi du lieu.");
        }

        [Test]
        public void SlotZero_RejectsEmptyId()
        {
            PlayerProfile.SetWeaponSlot(0, "weapon.sidearm.pistol_a");
            PlayerProfile.SetWeaponSlot(0, "");
            Assert.AreEqual("weapon.sidearm.pistol_a", PlayerProfile.GetWeaponSlot(0));
        }
    }
}
