using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ZombieWar;
using ZombieWar.UI;

namespace ZombieWar.Tests
{
    /// Kiem chung ASSET THAT (Slice 4.1): defaults dung design rule cua user, icon phu 100%.
    /// Chay SAU khi 'Author Costume Defaults' va 'Generate Missing Costume Thumbnails' da chay.
    public class CostumeAssetTruthTests
    {
        const string CatalogPath = "Assets/_Project/Data/Character/ModularCostumeCatalog.asset";
        const string CasualCatalogPath = "Assets/_Project/Data/Character/CasualCostumeCatalog.asset";
        const string UiCatalogPath = "Assets/_Project/UI/Data/UIPrototypeCatalog.asset";

        static ModularCostumeCatalog Catalog => AssetDatabase.LoadAssetAtPath<ModularCostumeCatalog>(CatalogPath);
        static UIPrototypeCatalog Ui => AssetDatabase.LoadAssetAtPath<UIPrototypeCatalog>(UiCatalogPath);

        static string NameOf(ModularCostumeCatalog cat, string guid)
        {
            foreach (var s in cat.slots)
                foreach (var p in s.parts)
                    if (p.guid == guid) return p.name;
            return null;
        }

        [Test]
        public void RealDefaults_MatchUserFinalRule()
        {
            var cat = Catalog;
            Assert.IsTrue(cat.defaults.IsAuthored, "Chua author defaults — chay 'Author Costume Defaults'.");

            // FINAL rule (Slice 4.2). Essential MAC san: Hair/Eye/Brow/Mouth Black_1, Chest_61, Legs_62.
            var expectEquip = new Dictionary<string, string>
            {
                { "Hair", "Hair_Black_1" }, { "Eye", "Eye_Black_1" }, { "Brow", "Brow_Black_1" },
                { "Mouth", "Mouth_Black_1" }, { "Chest", "Chest_61" }, { "Legs", "Legs_62" },
            };
            Assert.AreEqual(6, cat.defaults.equipped.Count);
            foreach (var eq in cat.defaults.equipped)
            {
                Assert.IsTrue(expectEquip.ContainsKey(eq.slot), $"Slot equip la: {eq.slot}");
                Assert.AreEqual(expectEquip[eq.slot], NameOf(cat, eq.guid), $"Default equip sai cho {eq.slot}");
            }
            Assert.IsNull(cat.defaults.GetEquippedGuid("Head"), "Head khong tu mac.");
            Assert.IsNull(cat.defaults.GetEquippedGuid("Feet"), "Feet KHONG tu mac (optional, body mesh co feet).");
            Assert.AreEqual("White", cat.defaults.defaultBodyColor);
            Assert.AreEqual("Normal", cat.defaults.defaultBodyEar);

            // Owned CHINH XAC 9 guid: 4 face Black_1 + Chest_61 + Legs_62 + Feet 1/2/3.
            var perSlot = new Dictionary<string, List<string>>();
            foreach (var guid in cat.defaults.ownedGuids)
                foreach (var s in cat.slots)
                    foreach (var p in s.parts)
                        if (p.guid == guid)
                        {
                            if (!perSlot.TryGetValue(s.slot, out var list)) perSlot[s.slot] = list = new List<string>();
                            list.Add(p.name);
                        }
            Assert.AreEqual(9, cat.defaults.ownedGuids.Count, "Owned mac dinh = 1+1+1+1+1+1 + Feet 3.");
            Assert.AreEqual(new[] { "Hair_Black_1" }, perSlot["Hair"].ToArray(), "Hair CHI Hair_Black_1.");
            Assert.AreEqual(new[] { "Eye_Black_1" }, perSlot["Eye"].ToArray());
            Assert.AreEqual(new[] { "Brow_Black_1" }, perSlot["Brow"].ToArray());
            Assert.AreEqual(new[] { "Mouth_Black_1" }, perSlot["Mouth"].ToArray());
            Assert.AreEqual(new[] { "Chest_61" }, perSlot["Chest"].ToArray());
            Assert.AreEqual(new[] { "Legs_62" }, perSlot["Legs"].ToArray());
            Assert.AreEqual(3, perSlot["Feet"].Count, "Feet 1/2/3 free.");
            foreach (var s in new[] { "Beard", "Eyewear", "Earring", "Head", "Hands", "Back", "Body" })
                Assert.IsFalse(perSlot.ContainsKey(s), $"Slot '{s}' khong duoc co default ownership.");
        }

        [Test]
        public void PresentationModel_BodyColors6_Ears2_AssemblyExcluded()
        {
            var cat = Catalog;
            Assert.AreEqual(6, ModularCostumeCatalog.BodyColors.Length);
            Assert.AreEqual(2, ModularCostumeCatalog.BodyEars.Length);
            // Moi mau resolve _1 + _Head_1 + _Head_2; assembly parts bi danh dau non-presentable.
            foreach (var col in ModularCostumeCatalog.BodyColors)
            {
                Assert.IsNotNull(cat.FindPartByName("Body", ModularCostumeCatalog.BodyMeshName(col)), $"Thieu Body_{col}_1");
                Assert.IsNotNull(cat.FindPartByName("Body", ModularCostumeCatalog.BodyHeadName(col, "Normal")), $"Thieu {col} Head_1");
                Assert.IsNotNull(cat.FindPartByName("Body", ModularCostumeCatalog.BodyHeadName(col, "Elf")), $"Thieu {col} Head_2");
            }
            // Assembly (vd Body_White_ArmA_1) phai bi coi la assembly (khong hien card).
            Assert.IsTrue(ModularCostumeCatalog.IsBodyAssemblyPart("Body_White_ArmA_1"));
            Assert.IsFalse(ModularCostumeCatalog.IsBodyAssemblyPart("Body_White_1"));
            Assert.IsFalse(ModularCostumeCatalog.IsBodyAssemblyPart("Body_Green_Head_2"));
        }

        [Test]
        public void BodyColorIcons_AllSixVendor()
        {
            var ui = Ui;
            foreach (var col in ModularCostumeCatalog.BodyColors)
                Assert.IsNotNull(ui.GetBodyColorIcon(col), $"Thieu vendor Body_{col}.png");
        }

        [Test]
        public void RealIcons_FullCoverage_NoHelmetFallback_NoDuplicates()
        {
            var cat = AssetDatabase.LoadAssetAtPath<ModularCostumeCatalog>(CasualCatalogPath);
            var ui = Ui;

            var mapped = new Dictionary<string, Sprite>();
            foreach (var e in ui.costumeIcons)
            {
                Assert.IsFalse(mapped.ContainsKey(e.guid), $"Mapping guid trung: {e.guid}");
                mapped[e.guid] = e.icon;
            }

            // Active Pro Casual: every player-facing item uses stable itemId and its real baked icon.
            int playerFacing = 0;
            var missing = new List<string>();
            foreach (var slot in cat.slots)
            {
                if (cat.IsTechnicalCasualSlot(slot.slot)) continue;
                foreach (var p in slot.parts)
                {
                    playerFacing++;
                    if (!mapped.TryGetValue(p.itemId, out var s) || s == null)
                        missing.Add($"{slot.slot}/{p.name}");
                }
            }
            Assert.AreEqual(448, playerFacing, "448 Pro Casual item player-facing.");
            Assert.AreEqual(playerFacing, mapped.Count, "UI mapping chi chua catalog dang active, khong giu Fantasy stale IDs.");
            Assert.IsEmpty(missing, $"{missing.Count} Pro Casual item thieu icon (vd: {(missing.Count > 0 ? missing[0] : "")}).");

            Assert.IsNotNull(ui.costumeFallbackIcon, "Van can fallback trung tinh cho loi runtime.");
            StringAssert.DoesNotContain("Helmet", ui.costumeFallbackIcon.name,
                "Fallback khong duoc la helmet/semantic icon.");
        }
    }
}
