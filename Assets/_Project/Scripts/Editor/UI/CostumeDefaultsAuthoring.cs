using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ZombieWar.Editor.UI
{
    /// <summary>
    /// NGUON DUY NHAT cua design-default costume (quyet dinh cuoi cua user, Slice 4.1):
    /// resolve ten part -> guid va ghi vao ModularCostumeCatalog.defaults. Idempotent.
    /// Runtime/tests/validator CHI doc guid tu catalog.defaults — khong duplicate list ten o cho khac.
    /// </summary>
    public static class CostumeDefaultsAuthoring
    {
        public const string CatalogPath = "Assets/_Project/Data/Character/ModularCostumeCatalog.asset";

        // Final initial ownership (Slice 4.2 — user decision cuoi):
        // essential mac dinh (Hair/Eye/Brow/Mouth/Chest/Legs Black_1 hoac 61/62) + Feet 1/2/3 free.
        // Body color White + ear Normal luon so huu (implicit, khong nam trong ownedGuids).
        static readonly (string slot, string[] names)[] OwnedDefaults =
        {
            ("Hair", new[] { "Hair_Black_1" }),
            ("Eye", new[] { "Eye_Black_1" }),
            ("Brow", new[] { "Brow_Black_1" }),
            ("Mouth", new[] { "Mouth_Black_1" }),
            ("Chest", new[] { "Chest_61" }),
            ("Legs", new[] { "Legs_62" }),
            ("Feet", new[] { "Feet_1", "Feet_2", "Feet_3" }), // free alternatives, KHONG tu mac
        };

        // Essential slots MAC san (bat buoc khong trong). Feet KHONG mac (optional). Body qua color/ear.
        static readonly (string slot, string name)[] EquippedDefaults =
        {
            ("Hair", "Hair_Black_1"),
            ("Eye", "Eye_Black_1"),
            ("Brow", "Brow_Black_1"),
            ("Mouth", "Mouth_Black_1"),
            ("Chest", "Chest_61"),
            ("Legs", "Legs_62"),
        };

        [MenuItem("ZombieWar/UI/Authoring/Author Costume Defaults")]
        public static void Author()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ZombieWar.ModularCostumeCatalog>(CatalogPath);
            if (catalog == null) { Debug.LogError($"[CostumeDefaults] Khong thay catalog: {CatalogPath}"); return; }

            var owned = new List<string>();
            int missing = 0;
            foreach (var (slotName, names) in OwnedDefaults)
                foreach (var name in names)
                {
                    string guid = Resolve(catalog, slotName, name);
                    if (guid == null) { Debug.LogError($"[CostumeDefaults] Thieu part '{slotName}/{name}' trong catalog."); missing++; continue; }
                    if (!owned.Contains(guid)) owned.Add(guid);
                }

            var equipped = new List<ZombieWar.ModularCostumeCatalog.PartRef>();
            foreach (var (slotName, name) in EquippedDefaults)
            {
                string guid = Resolve(catalog, slotName, name);
                if (guid == null) { Debug.LogError($"[CostumeDefaults] Thieu default equip '{slotName}/{name}'."); missing++; continue; }
                equipped.Add(new ZombieWar.ModularCostumeCatalog.PartRef { slot = slotName, guid = guid });
            }

            if (missing > 0)
            {
                Debug.LogError($"[CostumeDefaults] {missing} entry khong resolve duoc — KHONG ghi defaults (fail ro rang).");
                return;
            }

            catalog.defaults.ownedGuids = owned;
            catalog.defaults.equipped = equipped;
            catalog.defaults.defaultBodyColor = "White";
            catalog.defaults.defaultBodyEar = "Normal";
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CostumeDefaults] Authored: owned={owned.Count} guid " +
                      $"(Hair/Eye/Brow/Mouth Black_1, Chest_61, Legs_62, Feet 1/2/3), equipped={equipped.Count} essential slot; " +
                      $"Body default White/Normal.");
        }

        static string Resolve(ZombieWar.ModularCostumeCatalog catalog, string slotName, string partName)
        {
            var slot = catalog.GetSlot(slotName);
            if (slot == null) return null;
            foreach (var p in slot.parts)
                if (p.name == partName)
                    return string.IsNullOrEmpty(p.guid) ? null : p.guid;
            return null;
        }
    }
}
