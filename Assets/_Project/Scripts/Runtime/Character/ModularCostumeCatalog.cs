using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// Data-only catalog of the Layer Lab modular character parts, produced by the editor
    /// extractor (ZombieWar/Extract Modular Costume Catalog). We deliberately store metadata
    /// (name + asset path + guid) rather than hard GameObject references so the asset stays
    /// light and does not force-load ~2000 part prefabs.
    ///
    /// COSTUME-ONLY: held items (Wield_Gear_Left / Wield_Gear_Right) are excluded on purpose —
    /// the player holds guns from the weapon pack, so we only swap cosmetic slots
    /// (hair, face, eyewear, clothes, etc.).
    /// </summary>
    [CreateAssetMenu(fileName = "ModularCostumeCatalog", menuName = "ZombieWar/Modular Costume Catalog")]
    public class ModularCostumeCatalog : ScriptableObject
    {
        public enum CostumeGroup { Head, Body, Legs }

        [Serializable]
        public struct PartEntry
        {
            public string name;
            public string assetPath;
            public string guid;

            // Stable player-facing identity (save/economy/gacha key), e.g. "casual.chest.top.024".
            // GUID stays as internal asset-resolution metadata; moving an asset must not break a save.
            public string itemId;

            // Real per-item icon (editor-generated from this exact mesh). Bound by the Casual icon
            // generator; preserved across catalog regen by itemId. Null = fall back to a neutral icon.
            public Sprite icon;
            public GameObject prefab; // direct ref de runtime instantiate (menu preview + in-game apply)

            // ---- Skinned binding (extract tu scene Demo cua Layer Lab) ----
            // Mesh skinned nam trong FBX/Character/Characters.fbx; bones remap theo ten
            // len skeleton QuickRigCharacter2_* cua Player luc runtime.
            public Mesh skinnedMesh;
            public Material[] materials;
            public string[] boneNames;   // thu tu PHAI khop bindposes cua skinnedMesh
            public string rootBoneName;
        }

        [Serializable]
        public class Slot
        {
            public string slot;                 // logical costume slot, e.g. "Hair"
            public bool isBaseBody;             // true for the base body skin slot
            public List<PartEntry> parts = new();
        }

        [Serializable]
        public struct PartRef
        {
            public string slot;
            public string guid;
        }

        /// <summary>
        /// Outfit mac dinh AUTHORITATIVE (Slice 4.1) — nguon duy nhat cho ownership khoi tao va
        /// bo do bat buoc. Author bang lenh "ZombieWar/UI/Authoring/Author Costume Defaults"
        /// (resolve ten design -> guid, validate) — KHONG hand-edit guid.
        /// </summary>
        [Serializable]
        public class DefaultOutfit
        {
            [Tooltip("Toan bo guid so huu mac dinh (design-default free set) — KHONG gom Body color/ear.")]
            public List<string> ownedGuids = new();

            [Tooltip("Slot ESSENTIAL + part mac dinh duoc mac (Hair/Eye/Brow/Mouth/Chest/Legs). Body qua color/ear; optional de trong.")]
            public List<PartRef> equipped = new();

            [Tooltip("Mau body mac dinh (luon so huu).")] public string defaultBodyColor = "White";
            [Tooltip("Bien the tai mac dinh (luon so huu).")] public string defaultBodyEar = "Normal";

            public bool IsAuthored => equipped != null && equipped.Count > 0;

            public string GetEquippedGuid(string slot)
            {
                for (int i = 0; i < equipped.Count; i++)
                    if (equipped[i].slot == slot) return equipped[i].guid;
                return null;
            }
        }

        /// <summary>
        /// Authored, data-driven slot presentation. Replaces the hard-coded EssentialSlots/
        /// OptionalSlots arrays for catalogs that populate this list (Casual). UI grouping, order,
        /// required/optional and default selection all come from here — no per-slot code branches.
        /// Fantasy catalog leaves this empty and falls back to the static arrays below.
        /// </summary>
        [Serializable]
        public class SlotDefinition
        {
            public string id;                 // logical slot id, matches Slot.slot (e.g. "Chest")
            public string displayName;        // VN label shown on the tab/chip
            public CostumeGroup group;        // Head / Body / Legs
            public int sortOrder;
            public bool required;             // true = must always resolve a part ("Mặc định", no None)
            public bool allowNone;            // true = optional, may be cleared ("Không mang")
            public string defaultItemId;      // starter itemId for this slot (required slots only)
        }

        [Tooltip("Data-driven slot presentation (Casual). Empty = fall back to Essential/OptionalSlots.")]
        public List<SlotDefinition> slotDefinitions = new();

        [Tooltip("Fantasy uses a 6-color composite Body (ApplyBody). Casual Body is a normal slot with discrete meshes → false.")]
        public bool compositeBody = true;

        public SlotDefinition GetSlotDefinition(string slotId)
        {
            for (int i = 0; i < slotDefinitions.Count; i++)
                if (string.Equals(slotDefinitions[i].id, slotId, StringComparison.OrdinalIgnoreCase))
                    return slotDefinitions[i];
            return null;
        }

        public bool TryFindByItemId(string itemId, out string slotName, out PartEntry entry)
        {
            slotName = null; entry = default;
            if (string.IsNullOrEmpty(itemId)) return false;
            for (int i = 0; i < slots.Count; i++)
            {
                var ps = slots[i].parts;
                for (int j = 0; j < ps.Count; j++)
                    if (ps[j].itemId == itemId) { slotName = slots[i].slot; entry = ps[j]; return true; }
            }
            return false;
        }

        // ============================================================ PRESENTATION MODEL (Slice 4.2)
        // Quy tac thiet ke CO DINH (khong per-asset) — raw catalog van la 978 mesh, nhung UI chi
        // trinh bay "player-facing option": non-Body dung vendor screenshot 1-1, Body la composite
        // (6 mau + 2 tai), cac mesh assembly Body (Arm/Leg/Top/Bottom/Neck/Hand...) KHONG hien.

        public const string BodySlot = "Body";
        public const string CasualBaseHeadSlot = "Face";

        /// Pro Casual Face/Body are renderer infrastructure, not player-facing cosmetics.
        public bool IsTechnicalCasualSlot(string slot) => !compositeBody
            && (string.Equals(slot, CasualBaseHeadSlot, StringComparison.OrdinalIgnoreCase)
                || string.Equals(slot, BodySlot, StringComparison.OrdinalIgnoreCase));

        public static string CasualBodyMeshName(bool hasGloves, bool hasShoes)
        {
            int variant = hasGloves ? (hasShoes ? 4 : 2) : (hasShoes ? 3 : 1);
            return $"Body_{variant}";
        }

        /// Slot BAT BUOC co "Mac dinh" — khong bao gio de trong (tranh mannequin).
        public static readonly string[] EssentialSlots = { "Hair", "Brow", "Eye", "Mouth", "Chest", "Legs" };

        /// Slot OPTIONAL co "Khong mang" — mac dinh trong.
        public static readonly string[] OptionalSlots = { "Beard", "Eyewear", "Earring", "Head", "Hands", "Back", "Feet" };

        public static readonly string[] BodyColors = { "White", "Black", "Brown", "Green", "Purple", "Yellow" };
        public static readonly string[] BodyEars = { "Normal", "Elf" };

        public static bool IsEssentialSlot(string slot) => Array.IndexOf(EssentialSlots, slot) >= 0;
        public static bool IsOptionalSlot(string slot) => Array.IndexOf(OptionalSlots, slot) >= 0;
        public static bool IsValidBodyColor(string color) => Array.IndexOf(BodyColors, color) >= 0;
        public static bool IsValidBodyEar(string ear) => Array.IndexOf(BodyEars, ear) >= 0;

        /// Mesh full-body cho mau: Body_&lt;Color&gt;_1.
        public static string BodyMeshName(string color) => $"Body_{color}_1";
        /// Mesh dau/tai: Normal -> _Head_1, Elf -> _Head_2.
        public static string BodyHeadName(string color, string ear) => $"Body_{color}_Head_{(ear == "Elf" ? 2 : 1)}";

        /// True neu part Body la mesh assembly (Arm/Leg/Top/Bottom/Neck/Hand/_2/_3/_4) — KHONG hien lam card.
        public static bool IsBodyAssemblyPart(string partName)
        {
            foreach (var col in BodyColors)
            {
                if (partName == BodyMeshName(col) || partName == $"Body_{col}_Head_1" || partName == $"Body_{col}_Head_2")
                    return false; // 3 mesh duoc dung boi composite — khong phai assembly, nhung cung khong hien card
            }
            return true; // moi mesh Body con lai la assembly
        }

        public PartEntry? FindPartByName(string slotName, string partName)
        {
            var slot = GetSlot(slotName);
            if (slot == null) return null;
            for (int i = 0; i < slot.parts.Count; i++)
                if (slot.parts[i].name == partName) return slot.parts[i];
            return null;
        }

        [Tooltip("Default ownership + mandatory outfit — author bang lenh, khong hand-edit.")]
        public DefaultOutfit defaults = new();

        [Tooltip("Absolute source folder the catalog was scanned from.")]
        public string sourceFolder;

        [Tooltip("Held-item categories that were intentionally skipped.")]
        public List<string> excludedCategories = new();

        public List<Slot> slots = new();

        public int TotalParts
        {
            get
            {
                int n = 0;
                for (int i = 0; i < slots.Count; i++) n += slots[i].parts.Count;
                return n;
            }
        }

        public Slot GetSlot(string name)
        {
            for (int i = 0; i < slots.Count; i++)
                if (string.Equals(slots[i].slot, name, StringComparison.OrdinalIgnoreCase))
                    return slots[i];
            return null;
        }
    }
}
