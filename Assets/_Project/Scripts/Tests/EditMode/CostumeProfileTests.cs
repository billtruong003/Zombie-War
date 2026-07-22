using System.Collections.Generic;
using BillGameCore;
using NUnit.Framework;
using UnityEngine;
using ZombieWar;

namespace ZombieWar.Tests
{
    /// EditMode tests cho costume domain cua PlayerProfile (Slice 4): equip/clear/outfit/unlock-all
    /// atomic + rollback, va kiem chung catalog THAT (14 slot / 978 part / guid unique).
    public class CostumeProfileTests
    {
        private class InMemorySave : ISaveService
        {
            public readonly Dictionary<string, string> store = new();
            public bool throwOnSet;
            private int _slot;
            private string K(string key) => $"s{_slot}_{key}";
            public void Set(string key, string val) => store[K(key)] = val;
            public void Set(string key, int val) => store[K(key)] = val.ToString();
            public void Set(string key, float val) => store[K(key)] = val.ToString();
            public void Set(string key, bool val) => store[K(key)] = val ? "1" : "0";
            public void Set<T>(string key, T val) where T : class
            {
                if (throwOnSet) throw new System.IO.IOException("simulated save failure");
                store[K(key)] = JsonUtility.ToJson(val);
            }
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

        private static readonly string[] AllSlots =
        {
            "Hair", "Beard", "Brow", "Mouth", "Eyewear", "Eye", "Earring", "Head",
            "Chest", "Hands", "Back", "Body", "Legs", "Feet",
        };

        private InMemorySave _save;
        private ModularCostumeCatalog _catalog;
        private Mesh _dummyMesh;
        private int _costumeEvents;

        [SetUp]
        public void SetUp()
        {
            _save = new InMemorySave();
            PlayerProfile.StorageOverride = _save;
            PlayerProfile.LegacyReadString = _ => "";
            PlayerProfile.LegacyReadInt = _ => 0;
            PlayerProfile.ResetCacheForTests();
            _costumeEvents = 0;
            PlayerProfile.CostumeChanged += CountEvent;
            _dummyMesh = new Mesh { name = "dummy" };

            _catalog = ScriptableObject.CreateInstance<ModularCostumeCatalog>();
            foreach (var s in AllSlots)
            {
                var slot = new ModularCostumeCatalog.Slot { slot = s, isBaseBody = s == "Body" };
                if (s == "Body")
                {
                    // Body slot THAT: 6 mau x (_1 + _Head_1 + _Head_2), moi mesh co skinned binding.
                    foreach (var col in ModularCostumeCatalog.BodyColors)
                    {
                        AddBody(slot, $"Body_{col}_1");
                        AddBody(slot, $"Body_{col}_Head_1");
                        AddBody(slot, $"Body_{col}_Head_2");
                        AddBody(slot, $"Body_{col}_ArmA_1"); // assembly (khong hien card)
                    }
                }
                else
                    for (int i = 0; i < 2; i++)
                        slot.parts.Add(new ModularCostumeCatalog.PartEntry { name = $"{s}_{i}", guid = $"{s.ToLower()}-{i}" });
                _catalog.slots.Add(slot);
            }
        }

        private void AddBody(ModularCostumeCatalog.Slot slot, string name) =>
            slot.parts.Add(new ModularCostumeCatalog.PartEntry
            {
                name = name, guid = "g-" + name, skinnedMesh = _dummyMesh, boneNames = new[] { "b" }, materials = new Material[1]
            });

        [TearDown]
        public void TearDown()
        {
            PlayerProfile.CostumeChanged -= CountEvent;
            PlayerProfile.StorageOverride = null;
            PlayerProfile.LegacyReadString = k => PlayerPrefs.GetString(k, "");
            PlayerProfile.LegacyReadInt = k => PlayerPrefs.GetInt(k, 0);
            PlayerProfile.ResetCacheForTests();
            Object.DestroyImmediate(_catalog);
            if (_dummyMesh != null) Object.DestroyImmediate(_dummyMesh);
        }

        private void CountEvent() => _costumeEvents++;
        private void Own(string guid) => PlayerProfile.AddOwnedCostume(guid);

        // ===== Catalog that (asset thuc te) =====

        [Test]
        public void RealCatalogAsset_Has14Slots_978UniqueParts_HeldExcluded()
        {
            var real = UnityEditor.AssetDatabase.LoadAssetAtPath<ModularCostumeCatalog>(
                "Assets/_Project/Data/Character/ModularCostumeCatalog.asset");
            Assert.IsNotNull(real, "Thieu catalog asset.");
            Assert.AreEqual(14, real.slots.Count);

            var expected = new HashSet<string>(AllSlots);
            var guids = new HashSet<string>();
            int total = 0;
            bool baseBody = false;
            foreach (var slot in real.slots)
            {
                Assert.IsTrue(expected.Contains(slot.slot), $"Slot la '{slot.slot}'");
                if (slot.isBaseBody) { baseBody = true; Assert.AreEqual("Body", slot.slot); }
                foreach (var p in slot.parts)
                {
                    total++;
                    Assert.IsFalse(string.IsNullOrEmpty(p.guid), $"'{slot.slot}/{p.name}' thieu guid");
                    Assert.IsTrue(guids.Add(p.guid), $"Guid trung: {p.guid}");
                }
            }
            Assert.AreEqual(978, total, "Tong part thay doi so voi inventory da chot — kiem tra catalog.");
            Assert.IsTrue(baseBody, "Thieu slot base Body.");
            foreach (var held in new[] { "Wield_Gear_Left", "Wield_Gear_Right", "Wield_Gear" })
                Assert.IsNull(real.GetSlot(held), $"Held category '{held}' phai bi exclude.");
        }

        // ===== Equip =====

        [Test]
        public void EquipOwned_Succeeds_SlotResolvedFromCatalog_EventOnce()
        {
            Own("hair-0");
            _costumeEvents = 0;

            var result = PlayerProfile.TryEquipCostume(_catalog, "hair-0");

            Assert.AreEqual(PlayerProfile.CostumeEquipResult.Equipped, result);
            Assert.AreEqual("hair-0", PlayerProfile.GetPart("Hair"));
            Assert.AreEqual(1, _costumeEvents);
        }

        [Test]
        public void EquipUnowned_Rejected_NoStateNoEvent()
        {
            _costumeEvents = 0;
            Assert.AreEqual(PlayerProfile.CostumeEquipResult.NotOwned,
                PlayerProfile.TryEquipCostume(_catalog, "hair-0"));
            Assert.IsNull(PlayerProfile.GetPart("Hair"));
            Assert.AreEqual(0, _costumeEvents);
        }

        [Test]
        public void EquipInvalidGuid_Rejected()
        {
            _costumeEvents = 0;
            Assert.AreEqual(PlayerProfile.CostumeEquipResult.InvalidPart,
                PlayerProfile.TryEquipCostume(_catalog, "khong-ton-tai"));
            Assert.AreEqual(PlayerProfile.CostumeEquipResult.InvalidPart,
                PlayerProfile.TryEquipCostume(_catalog, ""));
            Assert.AreEqual(PlayerProfile.CostumeEquipResult.InvalidPart,
                PlayerProfile.TryEquipCostume(null, "hair-0"));
            Assert.AreEqual(0, _costumeEvents);
        }

        [Test]
        public void EquipOneSlot_PreservesAllOtherSlots()
        {
            foreach (var s in AllSlots)
            {
                if (s == "Body") continue; // Body driven by color/ear
                Own($"{s.ToLower()}-0"); PlayerProfile.TryEquipCostume(_catalog, $"{s.ToLower()}-0");
            }
            Own("hair-1");
            _costumeEvents = 0;

            PlayerProfile.TryEquipCostume(_catalog, "hair-1");

            Assert.AreEqual("hair-1", PlayerProfile.GetPart("Hair"));
            foreach (var s in AllSlots)
                if (s != "Hair" && s != "Body")
                    Assert.AreEqual($"{s.ToLower()}-0", PlayerProfile.GetPart(s), $"Slot '{s}' bi thay doi oan.");
            Assert.AreEqual(1, _costumeEvents);
        }

        [Test]
        public void EquipAlreadyEquipped_Idempotent_NoEvent()
        {
            Own("back-0");
            PlayerProfile.TryEquipCostume(_catalog, "back-0");
            _costumeEvents = 0;

            Assert.AreEqual(PlayerProfile.CostumeEquipResult.AlreadyEquipped,
                PlayerProfile.TryEquipCostume(_catalog, "back-0"));
            Assert.AreEqual(0, _costumeEvents);
        }

        [Test]
        public void EquipSaveFailure_RollsBack_NoEvent()
        {
            Own("hair-0"); Own("hair-1");
            PlayerProfile.TryEquipCostume(_catalog, "hair-0");
            _costumeEvents = 0;
            _save.throwOnSet = true;

            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex("Luu profile that bai"));
            var result = PlayerProfile.TryEquipCostume(_catalog, "hair-1");
            _save.throwOnSet = false;

            Assert.AreEqual(PlayerProfile.CostumeEquipResult.SaveFailed, result);
            Assert.AreEqual("hair-0", PlayerProfile.GetPart("Hair"), "Rollback ve part cu.");
            Assert.AreEqual(0, _costumeEvents);
        }

        // ===== Clear =====

        [Test]
        public void ClearOptionalSlot_Works_ClearBaseBody_Rejected()
        {
            Own("back-0");
            PlayerProfile.TryEquipCostume(_catalog, "back-0");
            _costumeEvents = 0;

            Assert.AreEqual(PlayerProfile.CostumeEquipResult.Equipped,
                PlayerProfile.TryClearCostumeSlot(_catalog, "Back"));
            Assert.IsNull(PlayerProfile.GetPart("Back"));
            Assert.AreEqual(1, _costumeEvents);

            // Body slot base body -> khong clear duoc (dù giờ Body driven bằng color/ear).
            Assert.AreEqual(PlayerProfile.CostumeEquipResult.CannotClearBaseBody,
                PlayerProfile.TryClearCostumeSlot(_catalog, "Body"));

            Assert.AreEqual(PlayerProfile.CostumeEquipResult.InvalidSlot,
                PlayerProfile.TryClearCostumeSlot(_catalog, "Wield_Gear_Left"));
        }

        // ===== Outfit batch =====

        [Test]
        public void OutfitBatch_NonBodySlots_OneSaveOneEvent_Roundtrip()
        {
            var outfit = new List<LoadoutState.PartSel>();
            foreach (var s in AllSlots)
            {
                if (s == "Body") continue; // Body driven by color/ear, không qua equippedParts
                Own($"{s.ToLower()}-1");
                outfit.Add(new LoadoutState.PartSel { slot = s, guid = $"{s.ToLower()}-1" });
            }
            _costumeEvents = 0;

            var result = PlayerProfile.TryEquipOutfit(_catalog, outfit);

            Assert.AreEqual(PlayerProfile.CostumeEquipResult.Equipped, result);
            Assert.AreEqual(1, _costumeEvents, "Batch = dung 1 event.");

            PlayerProfile.ResetCacheForTests();
            foreach (var s in AllSlots)
            {
                if (s == "Body") continue;
                Assert.AreEqual($"{s.ToLower()}-1", PlayerProfile.GetPart(s), $"Slot '{s}' mat sau reload.");
            }
        }

        [Test]
        public void BodyColorEar_RoundTrip_NoMismatch()
        {
            PlayerProfile.AddOwnedBodyColor("Green");
            PlayerProfile.AddOwnedBodyEar("Elf");
            _costumeEvents = 0;

            Assert.AreEqual(PlayerProfile.CostumeEquipResult.Equipped, PlayerProfile.TryEquipBodyColor(_catalog, "Green"));
            Assert.AreEqual(PlayerProfile.CostumeEquipResult.Equipped, PlayerProfile.TryEquipBodyEar(_catalog, "Elf"));
            Assert.AreEqual(2, _costumeEvents);
            Assert.AreEqual("Green", PlayerProfile.BodyColor);
            Assert.AreEqual("Elf", PlayerProfile.BodyEar);

            // Đổi màu giữ ear; đổi ear giữ màu.
            PlayerProfile.AddOwnedBodyColor("Purple");
            PlayerProfile.TryEquipBodyColor(_catalog, "Purple");
            Assert.AreEqual("Purple", PlayerProfile.BodyColor);
            Assert.AreEqual("Elf", PlayerProfile.BodyEar, "Ear giữ nguyên khi đổi màu.");

            PlayerProfile.ResetCacheForTests();
            Assert.AreEqual("Purple", PlayerProfile.BodyColor, "Body color persist qua reload.");
            Assert.AreEqual("Elf", PlayerProfile.BodyEar);
        }

        [Test]
        public void BodyColor_Unowned_Rejected()
        {
            Assert.AreEqual(PlayerProfile.CostumeEquipResult.NotOwned,
                PlayerProfile.TryEquipBodyColor(_catalog, "Green"));
            Assert.AreEqual("White", PlayerProfile.BodyColor);
            Assert.IsTrue(PlayerProfile.IsBodyColorOwned("White"), "White luôn owned.");
            Assert.IsTrue(PlayerProfile.IsBodyEarOwned("Normal"), "Normal luôn owned.");
            Assert.IsFalse(PlayerProfile.IsBodyEarOwned("Elf"));
        }

        [Test]
        public void MigrateRawBody_FromEquippedGuid_ToColorEar()
        {
            // Giả lập profile 4.1: Body_Green_Head_2 trong equippedParts + owned Body_Green_1.
            AuthorTestDefaults();
            PlayerProfile.AddOwnedCostume("g-Body_Green_1");
            PlayerProfile.SetPart("Body", "g-Body_Green_Head_2");

            PlayerProfile.EnsureValidCostumeLoadout(_catalog); // chạy migration

            Assert.IsNull(PlayerProfile.GetPart("Body"), "Body raw guid phải rút khỏi equippedParts.");
            Assert.AreEqual("Green", PlayerProfile.BodyColor);
            Assert.AreEqual("Elf", PlayerProfile.BodyEar, "_Head_2 -> Elf.");
            Assert.IsTrue(PlayerProfile.IsBodyColorOwned("Green"), "Body_Green_1 owned -> Green color owned.");
            Assert.IsFalse(PlayerProfile.IsCostumeOwned("g-Body_Green_1"), "Guid Body bỏ khỏi ownedCostumeGuids.");
        }

        [Test]
        public void OutfitBatch_OneInvalidEntry_RejectsWholeBatch()
        {
            Own("hair-0");
            var outfit = new List<LoadoutState.PartSel>
            {
                new() { slot = "Hair", guid = "hair-0" },
                new() { slot = "Hair", guid = "legs-0" }, // sai slot cho guid nay
            };
            _costumeEvents = 0;

            Assert.AreEqual(PlayerProfile.CostumeEquipResult.InvalidSlot,
                PlayerProfile.TryEquipOutfit(_catalog, outfit));
            Assert.IsNull(PlayerProfile.GetPart("Hair"), "Batch fail khong duoc ap 1 nua.");
            Assert.AreEqual(0, _costumeEvents);
        }

        // ===== Unlock all =====

        [Test]
        public void UnlockAll_AddsEverything_OneEvent_Idempotent()
        {
            _costumeEvents = 0;

            // Non-Body 13 slot x 2 = 26 guid + 5 màu (trừ White) + 1 ear (Elf) = 32.
            int added = PlayerProfile.UnlockAllCostumes(_catalog);
            Assert.AreEqual(32, added, "26 non-body guid + 5 màu + 1 ear.");
            Assert.AreEqual(1, _costumeEvents, "1 batch = 1 event.");
            foreach (var s in AllSlots)
            {
                if (s == "Body") continue;
                Assert.IsTrue(PlayerProfile.IsCostumeOwned($"{s.ToLower()}-0"));
                Assert.IsTrue(PlayerProfile.IsCostumeOwned($"{s.ToLower()}-1"));
            }
            Assert.IsFalse(PlayerProfile.IsCostumeOwned("g-Body_Green_1"), "Body assembly KHÔNG vào ownedCostumeGuids.");
            foreach (var col in ModularCostumeCatalog.BodyColors) Assert.IsTrue(PlayerProfile.IsBodyColorOwned(col));
            Assert.IsTrue(PlayerProfile.IsBodyEarOwned("Elf"));

            _costumeEvents = 0;
            Assert.AreEqual(0, PlayerProfile.UnlockAllCostumes(_catalog), "Idempotent.");
            Assert.AreEqual(0, _costumeEvents, "Khong co gi moi -> khong event.");
        }

        // ===== Migration/missing =====

        [Test]
        public void MissingSavedGuid_DoesNotCorruptOtherSlots()
        {
            Own("hair-0");
            PlayerProfile.TryEquipCostume(_catalog, "hair-0");
            PlayerProfile.SetPart("Chest", "guid-da-bi-xoa-khoi-catalog"); // raw setter (legacy path)

            Assert.AreEqual("hair-0", PlayerProfile.GetPart("Hair"));
            Assert.AreEqual("guid-da-bi-xoa-khoi-catalog", PlayerProfile.GetPart("Chest"),
                "Guid la duoc GIU trong save (khong pha), applier se skip an toan.");
            Own("legs-0");
            Assert.AreEqual(PlayerProfile.CostumeEquipResult.Equipped,
                PlayerProfile.TryEquipCostume(_catalog, "legs-0"), "Slot khac van equip binh thuong.");
        }

        // ===== Slice 4.1: defaults + resets =====

        private void AuthorTestDefaults()
        {
            // FINAL: essential Hair/Eye/Brow/Mouth/Chest/Legs -0 owned+equipped; Feet 0/1 owned (không mặc).
            _catalog.defaults.ownedGuids = new List<string>
                { "hair-0", "eye-0", "brow-0", "mouth-0", "chest-0", "legs-0", "feet-0", "feet-1" };
            _catalog.defaults.equipped = new List<ModularCostumeCatalog.PartRef>
            {
                new() { slot = "Hair", guid = "hair-0" },
                new() { slot = "Eye", guid = "eye-0" },
                new() { slot = "Brow", guid = "brow-0" },
                new() { slot = "Mouth", guid = "mouth-0" },
                new() { slot = "Chest", guid = "chest-0" },
                new() { slot = "Legs", guid = "legs-0" },
            };
            _catalog.defaults.defaultBodyColor = "White";
            _catalog.defaults.defaultBodyEar = "Normal";
        }

        private int OwnedCostumeCount()
        {
            int n = 0;
            foreach (var s in AllSlots)
                for (int i = 0; i < 2; i++)
                    if (PlayerProfile.IsCostumeOwned($"{s.ToLower()}-{i}")) n++;
            return n;
        }

        [Test]
        public void FreshProfile_Ensure_SeedsDefaultOwnershipAndMandatoryOutfit()
        {
            AuthorTestDefaults();
            _costumeEvents = 0;

            bool changed = PlayerProfile.EnsureValidCostumeLoadout(_catalog);

            Assert.IsTrue(changed);
            Assert.AreEqual(1, _costumeEvents, "Repair = dung 1 event.");
            Assert.AreEqual("hair-0", PlayerProfile.GetPart("Hair"));
            Assert.AreEqual("eye-0", PlayerProfile.GetPart("Eye"));
            Assert.AreEqual("chest-0", PlayerProfile.GetPart("Chest"));
            Assert.AreEqual("legs-0", PlayerProfile.GetPart("Legs"));
            Assert.IsNull(PlayerProfile.GetPart("Feet"), "Feet KHÔNG tự mặc (optional, body có feet).");
            Assert.IsTrue(PlayerProfile.IsCostumeOwned("feet-0"), "Feet-0 owned (free alternative).");
            Assert.AreEqual("White", PlayerProfile.BodyColor);
            Assert.AreEqual("Normal", PlayerProfile.BodyEar);
            Assert.IsFalse(PlayerProfile.IsCostumeOwned("hair-1"), "Ngoai default khong duoc grant.");

            _costumeEvents = 0;
            Assert.IsFalse(PlayerProfile.EnsureValidCostumeLoadout(_catalog), "Idempotent.");
            Assert.AreEqual(0, _costumeEvents, "Da hop le -> khong event.");
        }

        [Test]
        public void Ensure_PreservesPurchases_AndRepairsOnlyMandatory()
        {
            AuthorTestDefaults();
            PlayerProfile.EnsureValidCostumeLoadout(_catalog);
            Own("hair-1"); Own("back-0");
            PlayerProfile.TryEquipCostume(_catalog, "hair-1");
            PlayerProfile.TryEquipCostume(_catalog, "back-0");
            PlayerProfile.SetPart("Mouth", ""); // mô phỏng save cũ thiếu slot ESSENTIAL

            PlayerProfile.EnsureValidCostumeLoadout(_catalog);

            Assert.AreEqual("hair-1", PlayerProfile.GetPart("Hair"), "Hair hop le (da mua) phai duoc GIU.");
            Assert.AreEqual("back-0", PlayerProfile.GetPart("Back"), "Optional hop le phai duoc GIU.");
            Assert.AreEqual("mouth-0", PlayerProfile.GetPart("Mouth"), "Slot essential trống -> sửa về default.");
            Assert.IsTrue(PlayerProfile.IsCostumeOwned("hair-1"), "Khong xoa ownership da mua.");
        }

        [Test]
        public void RuntimeResetOutfit_PreservesOwnership_EquipsDefaults_OneEvent_Idempotent()
        {
            AuthorTestDefaults();
            PlayerProfile.UnlockAllCostumes(_catalog); // gia lap dev-unlock/mua nhieu do
            PlayerProfile.TryEquipCostume(_catalog, "hair-1");
            PlayerProfile.TryEquipCostume(_catalog, "back-0");
            PlayerProfile.TryEquipCostume(_catalog, "feet-1"); // optional Feet
            PlayerProfile.TryEquipBodyColor(_catalog, "Green");
            PlayerProfile.TryEquipBodyEar(_catalog, "Elf");
            int ownedBefore = OwnedCostumeCount();
            _costumeEvents = 0;

            var result = PlayerProfile.TryResetOutfitToDefaults(_catalog);

            Assert.AreEqual(PlayerProfile.CostumeEquipResult.Equipped, result);
            Assert.AreEqual(1, _costumeEvents);
            Assert.AreEqual("hair-0", PlayerProfile.GetPart("Hair"));
            Assert.AreEqual("chest-0", PlayerProfile.GetPart("Chest"));
            Assert.IsNull(PlayerProfile.GetPart("Back"), "Optional custom phai duoc go.");
            Assert.IsNull(PlayerProfile.GetPart("Feet"), "Feet ve Khong mang.");
            Assert.AreEqual(6, PlayerProfile.Parts.Count, "Chi con 6 slot essential.");
            Assert.AreEqual("White", PlayerProfile.BodyColor, "Body ve White.");
            Assert.AreEqual("Normal", PlayerProfile.BodyEar, "Ear ve Normal.");
            Assert.AreEqual(ownedBefore, OwnedCostumeCount(), "Reset outfit KHONG dong den ownership.");
            Assert.IsTrue(PlayerProfile.IsBodyColorOwned("Green"), "Body color ownership giu nguyen.");

            _costumeEvents = 0;
            Assert.AreEqual(PlayerProfile.CostumeEquipResult.AlreadyEquipped,
                PlayerProfile.TryResetOutfitToDefaults(_catalog), "Idempotent.");
            Assert.AreEqual(0, _costumeEvents);
        }

        [Test]
        public void RuntimeResetOutfit_SaveFailure_RollsBack()
        {
            AuthorTestDefaults();
            PlayerProfile.EnsureValidCostumeLoadout(_catalog);
            Own("back-0");
            PlayerProfile.TryEquipCostume(_catalog, "back-0");
            _costumeEvents = 0;
            _save.throwOnSet = true;

            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex("Luu profile that bai"));
            var result = PlayerProfile.TryResetOutfitToDefaults(_catalog);
            _save.throwOnSet = false;

            Assert.AreEqual(PlayerProfile.CostumeEquipResult.SaveFailed, result);
            Assert.AreEqual("back-0", PlayerProfile.GetPart("Back"), "Rollback: outfit cu giu nguyen.");
            Assert.AreEqual(0, _costumeEvents);
        }

        [Test]
        public void DevResetProgress_ExactDefaultOwnership_PreservesWalletAndWeapons()
        {
            AuthorTestDefaults();
            PlayerProfile.Add(PlayerProfile.CurrencyKind.Coin, 777);
            PlayerProfile.AddOwnedWeapon("weapon.test.keep");
            PlayerProfile.UnlockAllCostumes(_catalog);
            _costumeEvents = 0;

            PlayerProfile.ResetCostumeProgressForDev(_catalog);

            Assert.AreEqual(1, _costumeEvents);
            Assert.AreEqual(8, OwnedCostumeCount(), "Ownership := CHINH XAC bo default (6 essential + Feet 0/1).");
            Assert.IsFalse(PlayerProfile.IsCostumeOwned("hair-1"), "Dev-unlock bi xoa.");
            Assert.IsFalse(PlayerProfile.IsBodyColorOwned("Green"), "Body color dev-unlock bi xoa.");
            Assert.IsTrue(PlayerProfile.IsBodyColorOwned("White"), "White luon owned.");
            Assert.AreEqual("hair-0", PlayerProfile.GetPart("Hair"));
            Assert.AreEqual(777, PlayerProfile.Coin, "Vi tien GIU NGUYEN.");
            Assert.IsTrue(PlayerProfile.IsWeaponOwned("weapon.test.keep"), "Sung GIU NGUYEN.");
        }

        [Test]
        public void MandatorySlot_ClearRestoresDefault_OptionalSlotClearsEmpty()
        {
            AuthorTestDefaults();
            PlayerProfile.EnsureValidCostumeLoadout(_catalog);
            Own("hair-1"); Own("back-0");
            PlayerProfile.TryEquipCostume(_catalog, "hair-1");
            PlayerProfile.TryEquipCostume(_catalog, "back-0");

            Assert.AreEqual(PlayerProfile.CostumeEquipResult.Equipped,
                PlayerProfile.TryClearCostumeSlot(_catalog, "Hair"));
            Assert.AreEqual("hair-0", PlayerProfile.GetPart("Hair"),
                "Clear slot BAT BUOC = tro ve default, khong bao gio trong.");

            Assert.AreEqual(PlayerProfile.CostumeEquipResult.Equipped,
                PlayerProfile.TryClearCostumeSlot(_catalog, "Back"));
            Assert.IsNull(PlayerProfile.GetPart("Back"), "Optional clear ve trong.");
        }

        [Test]
        public void InvalidDefaults_FailLoudly_NoRandomFallback()
        {
            _catalog.defaults.ownedGuids = new List<string> { "hair-0" };
            _catalog.defaults.equipped = new List<ModularCostumeCatalog.PartRef>
            {
                new() { slot = "Hair", guid = "guid-khong-ton-tai" },
            };
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error,
                new System.Text.RegularExpressions.Regex("Costume default hong"));

            bool changed = PlayerProfile.EnsureValidCostumeLoadout(_catalog);

            Assert.IsFalse(changed, "Defaults hong -> khong sua bay, log ro.");
            Assert.IsNull(PlayerProfile.GetPart("Hair"));
        }
    }
}
