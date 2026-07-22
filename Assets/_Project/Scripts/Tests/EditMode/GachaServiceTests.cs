using System.Collections.Generic;
using System.Reflection;
using BillGameCore;
using NUnit.Framework;
using UnityEngine;
using ZombieWar;

namespace ZombieWar.Tests
{
    /// EditMode tests cho GachaService: deterministic theo seed, atomic debit, new/dupe + compensation,
    /// pity guarantee, loai starter. Storage in-memory.
    public class GachaServiceTests
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

        private InMemorySave _save;

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
            PlayerProfile.LegacyReadString = k => PlayerPrefs.GetString(k, "");
            PlayerProfile.LegacyReadInt = k => PlayerPrefs.GetInt(k, 0);
            PlayerProfile.ResetCacheForTests();
        }

        private static WeaponData MakeWeapon(string id, WeaponTier tier)
        {
            var w = ScriptableObject.CreateInstance<WeaponData>();
            w.weaponName = id;
            typeof(WeaponData).GetField("weaponId", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(w, id);
            typeof(WeaponData).GetField("tier", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(w, tier);
            var pub = typeof(WeaponData).GetField("tier", BindingFlags.Public | BindingFlags.Instance);
            if (pub != null) pub.SetValue(w, tier);
            return w;
        }

        // ---- costume pool helpers (khong can WeaponData) ----
        private static EconomyConfig CostumeEcon(params (string id, WeaponTier r, AcquireSource s)[] entries)
        {
            var e = ScriptableObject.CreateInstance<EconomyConfig>();
            e.costumeItems = new List<EconomyConfig.CostumeEntry>();
            foreach (var it in entries)
                e.costumeItems.Add(new EconomyConfig.CostumeEntry { itemId = it.id, displayName = it.id, slot = "Hair", rarity = it.r, source = it.s });
            e.RebuildLookups();
            return e;
        }

        private static EconomyConfig.GachaPool CostumePool(long single, long multi) => new()
        {
            poolId = "test.costume", kind = "costume", enabled = true, currency = WalletCurrency.Gem,
            singleCost = single, multiCost = multi, multiCount = 10,
            rarityWeights = new[] { 100, 0, 0, 0, 0 }, pityThreshold = 3, pityMinRarity = WeaponTier.Epic,
            dupCurrency = WalletCurrency.Coin, dupCompensation = new long[] { 5, 10, 20, 40, 80 },
        };

        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            var econ = CostumeEcon(("a", WeaponTier.Common, AcquireSource.Gacha),
                                   ("b", WeaponTier.Common, AcquireSource.Gacha),
                                   ("c", WeaponTier.Common, AcquireSource.Gacha));
            var pool = CostumePool(1, 9);
            pool.rarityWeights = new[] { 100, 0, 0, 0, 0 };
            var items = GachaService.BuildPool(pool, econ, null, null);

            PlayerProfile.Add(PlayerProfile.CurrencyKind.Gem, 1000);
            var r1 = GachaService.Pull(econ, pool, items, 10, new GachaService.SystemRng(12345));

            // Reset ve trang thai GIONG HET (xoa store: pity/ownership) truoc khi lap lai voi cung seed.
            _save.store.Clear();
            PlayerProfile.ResetCacheForTests();
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Gem, 1000);
            var r2 = GachaService.Pull(econ, pool, items, 10, new GachaService.SystemRng(12345));

            Assert.IsNotNull(r1); Assert.IsNotNull(r2);
            Assert.AreEqual(10, r1.Count);
            for (int i = 0; i < 10; i++) Assert.AreEqual(r1[i].id, r2[i].id, $"Cung seed -> cung id o pull {i}.");
            Object.DestroyImmediate(econ);
        }

        [Test]
        public void MultiPull_DebitsMultiCostOnce()
        {
            var econ = CostumeEcon(("a", WeaponTier.Common, AcquireSource.Gacha));
            var pool = CostumePool(10, 90);
            var items = GachaService.BuildPool(pool, econ, null, null);
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Gem, 100);

            var r = GachaService.Pull(econ, pool, items, 10, new GachaService.SystemRng(1));

            Assert.IsNotNull(r);
            Assert.AreEqual(10, r.Count);
            Assert.AreEqual(10, PlayerProfile.Gem, "Tru dung multiCost 90 mot lan.");
            Object.DestroyImmediate(econ);
        }

        [Test]
        public void NewItem_GrantsOwnership_MarksUnseen()
        {
            var econ = CostumeEcon(("a", WeaponTier.Common, AcquireSource.Gacha));
            var pool = CostumePool(10, 90);
            var items = GachaService.BuildPool(pool, econ, null, null);
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Gem, 10);

            var r = GachaService.Pull(econ, pool, items, 1, new GachaService.SystemRng(1));

            Assert.IsNotNull(r);
            Assert.IsTrue(r[0].isNew);
            Assert.IsTrue(PlayerProfile.IsCostumeOwned("a"));
            Assert.IsTrue(PlayerProfile.IsUnseen("a"));
            Object.DestroyImmediate(econ);
        }

        [Test]
        public void DuplicateItem_GivesCompensation_NoRegrant()
        {
            var econ = CostumeEcon(("a", WeaponTier.Common, AcquireSource.Gacha));
            var pool = CostumePool(10, 90); // dupCompensation Common = 5 Coin
            var items = GachaService.BuildPool(pool, econ, null, null);
            PlayerProfile.AddOwnedCostume("a"); // da so huu
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Gem, 10);
            long coinBefore = PlayerProfile.Coin;

            var r = GachaService.Pull(econ, pool, items, 1, new GachaService.SystemRng(1));

            Assert.IsNotNull(r);
            Assert.IsFalse(r[0].isNew, "Da so huu -> duplicate.");
            Assert.AreEqual(5, r[0].dupComp);
            Assert.AreEqual(coinBefore + 5, PlayerProfile.Coin, "Den bu Coin.");
            Object.DestroyImmediate(econ);
        }

        [Test]
        public void Pity_ForcesHighRarity_AtThreshold_ThenResets()
        {
            // Pool: Common "c" + Epic "e". Weight chi Common -> luon Common khi chua pity.
            var econ = CostumeEcon(("c", WeaponTier.Common, AcquireSource.Gacha),
                                   ("e", WeaponTier.Epic, AcquireSource.Gacha));
            var pool = CostumePool(1, 9);
            pool.pityThreshold = 3; pool.pityMinRarity = WeaponTier.Epic;
            var items = GachaService.BuildPool(pool, econ, null, null);
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Gem, 100);

            var rng = new GachaService.SystemRng(7);
            for (int i = 0; i < 3; i++)
            {
                var r = GachaService.Pull(econ, pool, items, 1, rng);
                Assert.AreEqual(WeaponTier.Common, r[0].rarity, $"Pull {i} chua pity -> Common.");
                Assert.AreEqual(i + 1, PlayerProfile.GetPity("test.costume"));
            }
            var forced = GachaService.Pull(econ, pool, items, 1, rng);
            Assert.AreEqual(WeaponTier.Epic, forced[0].rarity, "Dat nguong -> guarantee Epic.");
            Assert.AreEqual(0, PlayerProfile.GetPity("test.costume"), "Pity reset sau khi trung.");
            Object.DestroyImmediate(econ);
        }

        [Test]
        public void InsufficientFunds_ReturnsNull_NoPityChange_NoWalletChange()
        {
            var econ = CostumeEcon(("a", WeaponTier.Common, AcquireSource.Gacha));
            var pool = CostumePool(10, 90);
            var items = GachaService.BuildPool(pool, econ, null, null);
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Gem, 5); // khong du 10

            var r = GachaService.Pull(econ, pool, items, 1, new GachaService.SystemRng(1));

            Assert.IsNull(r);
            Assert.AreEqual(5, PlayerProfile.Gem);
            Assert.AreEqual(0, PlayerProfile.GetPity("test.costume"), "Khong du tien -> khong tang pity.");
            Object.DestroyImmediate(econ);
        }

        [Test]
        public void WeaponPool_ExcludesStarter()
        {
            var pistol = MakeWeapon("weapon.starter", WeaponTier.Common);
            var rifle = MakeWeapon("weapon.rifle", WeaponTier.Rare);
            var weapons = new List<WeaponData> { pistol, rifle };
            var pool = new EconomyConfig.GachaPool { poolId = "test.weapon", kind = "weapon", enabled = true };
            var starter = new HashSet<string> { "weapon.starter" };

            var items = GachaService.BuildPool(pool, null, weapons, starter);

            Assert.AreEqual(1, items.Count);
            Assert.AreEqual("weapon.rifle", items[0].id);
            Assert.IsTrue(items[0].isWeapon);
            Object.DestroyImmediate(pistol); Object.DestroyImmediate(rifle);
        }

        [Test]
        public void UnseenBadge_ClassifiesAndClearsByCategory()
        {
            PlayerProfile.MarkUnseen("weapon.smg.generic");           // weapon (co '.')
            PlayerProfile.MarkUnseen("d1d1f080935599847b73d214774e"); // costume guid
            PlayerProfile.MarkUnseen("casual.pro.hair.001");          // Pro costume also contains dots
            PlayerProfile.MarkUnseen(EconomyConfig.BodyColorId("Black"));
            Assert.IsTrue(PlayerProfile.HasUnseenWeapon());
            Assert.IsTrue(PlayerProfile.HasUnseenCostume());

            PlayerProfile.ClearUnseenWeapons();
            Assert.IsFalse(PlayerProfile.HasUnseenWeapon(), "Da xem sung -> tat badge sung.");
            Assert.IsTrue(PlayerProfile.HasUnseenCostume(), "Skin badge van con.");

            PlayerProfile.ClearUnseenCostumes();
            Assert.IsFalse(PlayerProfile.HasUnseenCostume());
        }

        [Test]
        public void CostumePool_OnlyIncludesGachaSources()
        {
            var econ = CostumeEcon(("shop", WeaponTier.Common, AcquireSource.Shop),
                                   ("gacha", WeaponTier.Rare, AcquireSource.Gacha),
                                   ("both", WeaponTier.Epic, AcquireSource.ShopAndGacha),
                                   ("starter", WeaponTier.Common, AcquireSource.Starter),
                                   ("disabled", WeaponTier.Common, AcquireSource.Disabled));
            var pool = new EconomyConfig.GachaPool { poolId = "test.costume", kind = "costume", enabled = true };

            var items = GachaService.BuildPool(pool, econ, null, null);

            var ids = new HashSet<string>();
            foreach (var it in items) ids.Add(it.id);
            Assert.AreEqual(2, items.Count);
            Assert.IsTrue(ids.Contains("gacha"));
            Assert.IsTrue(ids.Contains("both"));
            Assert.IsFalse(ids.Contains("shop"));
            Assert.IsFalse(ids.Contains("starter"));
            Object.DestroyImmediate(econ);
        }

        [Test]
        public void WeaponDuplicate_GrantsWeaponSpecificShards_NotCurrencyCompensation()
        {
            var weapon = MakeWeapon("weapon.rifle.test", WeaponTier.Rare);
            var econ = ScriptableObject.CreateInstance<EconomyConfig>();
            econ.weaponStar2ShardCost = new[] { 5, 5, 12, 15, 20 };
            econ.weaponStar2GoldCost = new long[] { 10, 10, 40, 60, 100 };
            var pool = new EconomyConfig.GachaPool
            {
                poolId = "test.weapon", kind = "weapon", enabled = true,
                currency = WalletCurrency.Coin, singleCost = 10,
                rarityWeights = new[] { 0, 0, 100, 0, 0 }, pityThreshold = 99,
                weaponDuplicateShards = new[] { 1, 2, 17, 40, 80 },
                dupCurrency = WalletCurrency.Gem, dupCompensation = new long[] { 9, 9, 9, 9, 9 },
            };
            PlayerProfile.AddOwnedWeapon(weapon.WeaponId);
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 100);
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Gold, 100);
            var items = GachaService.BuildPool(pool, econ, new[] { weapon }, new HashSet<string>());

            var result = GachaService.Pull(econ, pool, items, 1, new GachaService.SystemRng(7));

            Assert.IsNotNull(result);
            Assert.IsFalse(result[0].isNew);
            Assert.AreEqual(17, result[0].weaponShards);
            Assert.AreEqual(17, PlayerProfile.GetWeaponShards(weapon.WeaponId));
            Assert.AreEqual(0, PlayerProfile.Gem, "Weapon duplicate must not grant old currency compensation.");
            Assert.AreEqual(90, PlayerProfile.Coin);
            Assert.AreEqual(PlayerProfile.WeaponUpgradeResult.Upgraded, PlayerProfile.TryUpgradeWeapon(weapon, econ));
            Assert.AreEqual(2, PlayerProfile.GetWeaponLevel(weapon.WeaponId));
            Assert.AreEqual(5, PlayerProfile.GetWeaponShards(weapon.WeaponId));
            Assert.AreEqual(60, PlayerProfile.Gold);
            Object.DestroyImmediate(weapon); Object.DestroyImmediate(econ);
        }

        [Test]
        public void CostumeSet_PurchaseAndGachaPool_UseWholeSetAsOneUnit()
        {
            var econ = ScriptableObject.CreateInstance<EconomyConfig>();
            var set = new EconomyConfig.CostumeSetEntry
            {
                setId = "casual.pro.set.test", displayName = "Test Set", rarity = WeaponTier.Epic,
                source = AcquireSource.ShopAndGacha, gemPrice = 50,
                itemIds = new List<string> { "casual.pro.hair.001", "casual.pro.chest.top.001" },
            };
            econ.costumeSets = new List<EconomyConfig.CostumeSetEntry> { set };
            econ.RebuildLookups();
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Gem, 70);

            var purchase = PlayerProfile.TryPurchaseCostumeSet(econ, set.setId);
            var pool = GachaService.BuildPool(new EconomyConfig.GachaPool { kind = "costume" }, econ, null, null);

            Assert.AreEqual(PlayerProfile.PurchaseResult.Purchased, purchase);
            Assert.AreEqual(20, PlayerProfile.Gem);
            Assert.IsTrue(PlayerProfile.IsCostumeItemOwned(set.itemIds[0]));
            Assert.IsTrue(PlayerProfile.IsCostumeItemOwned(set.itemIds[1]));
            Assert.AreEqual(1, pool.Count);
            Assert.IsTrue(pool[0].isCostumeSet);
            Assert.AreEqual(set.setId, pool[0].id);
            Object.DestroyImmediate(econ);
        }
    }
}
