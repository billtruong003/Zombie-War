using System.Collections.Generic;
using BillGameCore;
using NUnit.Framework;
using UnityEngine;
using ZombieWar;

namespace ZombieWar.Tests
{
    /// EditMode tests for the Casual costume model (compositeBody=false, stable itemId identity):
    /// fresh-profile seeding, idempotency, non-destructive migration/purge, reset, randomize, and the
    /// honest "no Fantasy debit for an unavailable Casual item" guarantee.
    public class CasualCostumeTests
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

        private ModularCostumeCatalog _cat;

        [SetUp]
        public void SetUp()
        {
            PlayerProfile.StorageOverride = new InMemorySave();
            PlayerProfile.LegacyReadString = _ => "";
            PlayerProfile.LegacyReadInt = _ => 0;
            PlayerProfile.ResetCacheForTests();
            _cat = BuildCasualCatalog();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerProfile.StorageOverride = null;
            PlayerProfile.ResetCacheForTests();
        }

        // Minimal Casual catalog: 8 player-facing definitions plus internal Face/Body render infrastructure.
        private static ModularCostumeCatalog BuildCasualCatalog()
        {
            var cat = ScriptableObject.CreateInstance<ModularCostumeCatalog>();
            cat.compositeBody = false;
            var H = ModularCostumeCatalog.CostumeGroup.Head;
            var B = ModularCostumeCatalog.CostumeGroup.Body;
            var L = ModularCostumeCatalog.CostumeGroup.Legs;
            void Def(string id, ModularCostumeCatalog.CostumeGroup g, int o, bool req, bool none, string def) =>
                cat.slotDefinitions.Add(new ModularCostumeCatalog.SlotDefinition
                { id = id, displayName = id, group = g, sortOrder = o, required = req, allowNone = none, defaultItemId = def });
            Def("Hair", H, 0, true, false, "casual.hair.001");
            Def("Head", H, 2, false, true, null);
            Def("Eyewear", H, 3, false, true, null);
            Def("Chest", B, 4, true, false, "casual.chest.top.054");
            Def("Hands", B, 5, false, true, null);
            Def("Back", B, 6, false, true, null);
            Def("Legs", L, 8, true, false, "casual.legs.bottom.062");
            Def("Feet", L, 9, false, true, "casual.feet.shoes.001");

            void Slot(string s, params string[] ids)
            {
                var slot = new ModularCostumeCatalog.Slot { slot = s, isBaseBody = s == "Body" };
                foreach (var id in ids) slot.parts.Add(new ModularCostumeCatalog.PartEntry { name = id, itemId = id, guid = "casualfbx" });
                cat.slots.Add(slot);
            }
            Slot("Hair", "casual.hair.001", "casual.hair.002");
            Slot("Face", "casual.face.a01", "casual.face.a02");
            Slot("Head", "casual.headgear.001", "casual.headgear.002");
            Slot("Eyewear", "casual.eyewear.001");
            Slot("Chest", "casual.chest.top.054", "casual.chest.top.001");
            Slot("Hands", "casual.hands.glove.001");
            Slot("Back", "casual.back.bag.001", "casual.back.bag.002");
            Slot("Body", "casual.body.004", "casual.body.001");
            Slot("Legs", "casual.legs.bottom.062", "casual.legs.bottom.001");
            Slot("Feet", "casual.feet.shoes.001", "casual.feet.shoes.002");
            return cat;
        }

        private static string Part(string slot) => PlayerProfile.GetPart(slot);

        [Test]
        public void FreshProfile_SeedsCasualStarter_AndIsIdempotent()
        {
            bool first = PlayerProfile.EnsureValidCostumeLoadout(_cat);
            Assert.IsTrue(first, "Fresh profile should be seeded.");

            Assert.IsTrue(string.IsNullOrEmpty(Part("Body")), "Technical Body must not be persisted.");
            Assert.IsTrue(string.IsNullOrEmpty(Part("Face")), "Technical base Head must not be persisted.");
            Assert.AreEqual("casual.hair.001", Part("Hair"));
            Assert.AreEqual("casual.chest.top.054", Part("Chest"));
            Assert.AreEqual("casual.legs.bottom.062", Part("Legs"));
            Assert.AreEqual("casual.feet.shoes.001", Part("Feet"));
            // optional slots without a default stay empty
            Assert.IsTrue(string.IsNullOrEmpty(Part("Head")));
            Assert.IsTrue(string.IsNullOrEmpty(Part("Back")));

            bool second = PlayerProfile.EnsureValidCostumeLoadout(_cat);
            Assert.IsFalse(second, "Second ensure must be a no-op (idempotent).");
        }

        [Test]
        public void RequiredSlots_CannotBecomeEmpty()
        {
            PlayerProfile.EnsureValidCostumeLoadout(_cat);
            // Clearing a required slot is not exposed by UI; ensure repair restores it.
            PlayerProfile.SetPart("Chest", null); // force-empty
            Assert.IsTrue(string.IsNullOrEmpty(Part("Chest")));
            PlayerProfile.EnsureValidCostumeLoadout(_cat);
            Assert.AreEqual("casual.chest.top.054", Part("Chest"), "Required slot repaired to default.");
        }

        [Test]
        public void OptionalSlot_ClearsAndStaysCleared()
        {
            PlayerProfile.EnsureValidCostumeLoadout(_cat);   // Feet seeded to shoes.001 (owned)
            Assert.AreEqual("casual.feet.shoes.001", Part("Feet"));
            var r = PlayerProfile.TryClearCostumeSlot(_cat, "Feet");
            Assert.AreEqual(PlayerProfile.CostumeEquipResult.Equipped, r);
            Assert.IsTrue(string.IsNullOrEmpty(Part("Feet")), "Feet cleared.");
            // Re-ensure must NOT re-seed Feet (already owned once).
            PlayerProfile.EnsureValidCostumeLoadout(_cat);
            Assert.IsTrue(string.IsNullOrEmpty(Part("Feet")), "Cleared optional stays cleared.");
        }

        [Test]
        public void Migration_PurgesFantasyLeftovers_PreservesWalletWeaponsPity()
        {
            // Old Fantasy-shaped state + progression.
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 500);
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Gem, 30);
            PlayerProfile.AddOwnedWeapon("weapon.rifle.ar");
            PlayerProfile.SetWeaponSlot(0, "weapon.pistol.p1");
            PlayerProfile.SetPityInMemory("costume", 7);
            PlayerProfile.SetPart("Beard", "deadbeefcafe0000deadbeefcafe0000"); // Fantasy guid leftover
            PlayerProfile.SetPart("Mouth", "0000deadbeefcafe0000deadbeefcafe");

            PlayerProfile.EnsureValidCostumeLoadout(_cat);

            // Fantasy leftovers purged (slots not in Casual catalog / unresolvable keys).
            Assert.IsTrue(string.IsNullOrEmpty(Part("Beard")));
            Assert.IsTrue(string.IsNullOrEmpty(Part("Mouth")));
            // Technical renderer slots are purged rather than becoming progression state.
            Assert.IsTrue(string.IsNullOrEmpty(Part("Body")));
            Assert.IsTrue(string.IsNullOrEmpty(Part("Face")));
            // Non-costume progression untouched.
            Assert.AreEqual(500, PlayerProfile.Coin);
            Assert.AreEqual(30, PlayerProfile.Gem);
            Assert.IsTrue(PlayerProfile.IsWeaponOwned("weapon.rifle.ar"));
            Assert.AreEqual("weapon.pistol.p1", PlayerProfile.GetWeaponSlot(0));
            Assert.AreEqual(7, PlayerProfile.GetPity("costume"));
        }

        [Test]
        public void Reset_RestoresStarter_ClearsOptionals_PreservesWalletWeapons()
        {
            PlayerProfile.EnsureValidCostumeLoadout(_cat);
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 1000);
            PlayerProfile.AddOwnedWeapon("weapon.smg.s1");
            PlayerProfile.AddOwnedCostume("casual.back.bag.001");
            PlayerProfile.TryEquipCostume(_cat, "casual.back.bag.001"); // optional equipped
            Assert.AreEqual("casual.back.bag.001", Part("Back"));

            var r = PlayerProfile.TryResetOutfitToDefaults(_cat);
            Assert.AreEqual(PlayerProfile.CostumeEquipResult.Equipped, r);

            Assert.AreEqual("casual.chest.top.054", Part("Chest"));
            Assert.AreEqual("casual.feet.shoes.001", Part("Feet"));
            Assert.IsTrue(string.IsNullOrEmpty(Part("Back")), "Optional Back cleared on reset.");
            Assert.AreEqual(1000, PlayerProfile.Coin, "Wallet preserved.");
            Assert.IsTrue(PlayerProfile.IsWeaponOwned("weapon.smg.s1"), "Weapons preserved.");
        }

        [Test]
        public void Randomize_FillsRequired_UsesOwnedOnly_RejectsUnowned()
        {
            PlayerProfile.EnsureValidCostumeLoadout(_cat);
            PlayerProfile.AddOwnedCostume("casual.chest.top.001");

            // Valid: partial outfit (only Chest) -> required slots auto-filled with defaults.
            var ok = PlayerProfile.TrySetCasualOutfit(_cat, new List<LoadoutState.PartSel>
            {
                new() { slot = "Chest", guid = "casual.chest.top.001" },
            });
            Assert.AreEqual(PlayerProfile.CostumeEquipResult.Equipped, ok);
            Assert.AreEqual("casual.chest.top.001", Part("Chest"));
            foreach (var req in new[] { "Hair", "Chest", "Legs" })
                Assert.IsFalse(string.IsNullOrEmpty(Part(req)), $"Required {req} filled.");

            // Invalid: an unowned item is rejected, nothing changes.
            var bad = PlayerProfile.TrySetCasualOutfit(_cat, new List<LoadoutState.PartSel>
            {
                new() { slot = "Back", guid = "casual.back.bag.002" }, // not owned
            });
            Assert.AreEqual(PlayerProfile.CostumeEquipResult.NotOwned, bad);
            Assert.IsTrue(string.IsNullOrEmpty(Part("Back")), "Unowned randomize rejected.");
        }

        [Test]
        public void UnavailableCasualItem_CannotEquip_AndCannotDebitViaFantasyEconomy()
        {
            PlayerProfile.EnsureValidCostumeLoadout(_cat);
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Gold, 999);
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Gem, 999);

            // Not owned -> equip refused, nothing equipped.
            Assert.IsFalse(PlayerProfile.IsCostumeItemOwned("casual.chest.top.001"));
            var eq = PlayerProfile.TryEquipCostume(_cat, "casual.chest.top.001");
            Assert.AreEqual(PlayerProfile.CostumeEquipResult.NotOwned, eq);

            // A Fantasy economy with no Casual record must not sell it or debit currency.
            var econ = ScriptableObject.CreateInstance<EconomyConfig>();
            var buy = PlayerProfile.TryPurchaseCostume(econ, "casual.chest.top.001");
            Assert.AreEqual(PlayerProfile.PurchaseResult.InvalidWeapon, buy);
            Assert.AreEqual(999, PlayerProfile.Gold, "Gold not debited.");
            Assert.AreEqual(999, PlayerProfile.Gem, "Gem not debited.");
        }

        [Test]
        public void TechnicalHeadAndBody_AreInternalOnly_AndBodyVariantMappingIsExplicit()
        {
            Assert.IsTrue(_cat.IsTechnicalCasualSlot("Face"));
            Assert.IsTrue(_cat.IsTechnicalCasualSlot("Body"));
            Assert.IsFalse(_cat.IsTechnicalCasualSlot("Head"), "Head is headgear and remains player-facing.");
            Assert.IsNull(_cat.GetSlotDefinition("Face"));
            Assert.IsNull(_cat.GetSlotDefinition("Body"));

            Assert.AreEqual("Body_1", ModularCostumeCatalog.CasualBodyMeshName(false, false));
            Assert.AreEqual("Body_2", ModularCostumeCatalog.CasualBodyMeshName(true, false));
            Assert.AreEqual("Body_3", ModularCostumeCatalog.CasualBodyMeshName(false, true));
            Assert.AreEqual("Body_4", ModularCostumeCatalog.CasualBodyMeshName(true, true));

            PlayerProfile.EnsureValidCostumeLoadout(_cat);
            PlayerProfile.AddOwnedCostume("casual.body.001");
            PlayerProfile.AddOwnedCostume("casual.face.a01");
            Assert.AreEqual(PlayerProfile.CostumeEquipResult.InvalidSlot,
                PlayerProfile.TryEquipCostume(_cat, "casual.body.001"));
            Assert.AreEqual(PlayerProfile.CostumeEquipResult.InvalidSlot,
                PlayerProfile.TryEquipCostume(_cat, "casual.face.a01"));
        }
    }
}
