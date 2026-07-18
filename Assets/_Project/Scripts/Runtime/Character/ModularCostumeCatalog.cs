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
        [Serializable]
        public struct PartEntry
        {
            public string name;
            public string assetPath;
            public string guid;
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
