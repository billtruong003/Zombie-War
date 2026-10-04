using System;
using System.Collections.Generic;
using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    /// PlayerProfile, part: Costume ownership and the body composite (colour + ears).
    public static partial class PlayerProfile
    {
        // ===== Costume =====

        public static IReadOnlyList<LoadoutState.PartSel> Parts => Data.equippedParts;

        public static bool IsCostumeOwned(string guid) =>
            !string.IsNullOrEmpty(guid) && Data.ownedCostumeGuids.Contains(guid);

        public static void AddOwnedCostume(string guid)
        {
            if (string.IsNullOrEmpty(guid) || Data.ownedCostumeGuids.Contains(guid)) return;
            Data.ownedCostumeGuids.Add(guid);
            SaveNow();
            Notify(Change.Costume);
        }

        // ===== Body composite (Slice 4.2): mau + bien the tai =====

        public static string BodyColor => string.IsNullOrEmpty(Data.bodyColor) ? "White" : Data.bodyColor;
        public static string BodyEar => string.IsNullOrEmpty(Data.bodyEar) ? "Normal" : Data.bodyEar;
        public static bool IsBodyColorOwned(string color) => color == "White" || Data.ownedBodyColors.Contains(color);
        public static bool IsBodyEarOwned(string ear) => ear == "Normal" || Data.ownedBodyEars.Contains(ear);

        public static void AddOwnedBodyColor(string color)
        {
            if (string.IsNullOrEmpty(color) || color == "White" || Data.ownedBodyColors.Contains(color)) return;
            Data.ownedBodyColors.Add(color); SaveNow(); Notify(Change.Costume);
        }

        public static void AddOwnedBodyEar(string ear)
        {
            if (string.IsNullOrEmpty(ear) || ear == "Normal" || Data.ownedBodyEars.Contains(ear)) return;
            Data.ownedBodyEars.Add(ear); SaveNow(); Notify(Change.Costume);
        }

        /// Doi mau body: validate mau hop le + so huu + resolve duoc mesh body & head cho mau+tai
        /// hien tai. 1 save, 1 event, rollback. Body va head luon cung mau (khong lech mau).
        public static CostumeEquipResult TryEquipBodyColor(ModularCostumeCatalog catalog, string color)
        {
            if (catalog == null || !ModularCostumeCatalog.IsValidBodyColor(color)) return CostumeEquipResult.InvalidPart;
            if (!IsBodyColorOwned(color)) return CostumeEquipResult.NotOwned;
            if (!BodyMeshesResolve(catalog, color, BodyEar)) return CostumeEquipResult.InvalidPart;
            if (BodyColor == color) return CostumeEquipResult.AlreadyEquipped;

            string prev = Data.bodyColor; Data.bodyColor = color;
            if (!Commit()) { Data.bodyColor = prev; return CostumeEquipResult.SaveFailed; }
            Notify(Change.Costume); return CostumeEquipResult.Equipped;
        }

        /// Doi bien the tai (Normal/Elf): giu nguyen mau, chi doi mesh head.
        public static CostumeEquipResult TryEquipBodyEar(ModularCostumeCatalog catalog, string ear)
        {
            if (catalog == null || !ModularCostumeCatalog.IsValidBodyEar(ear)) return CostumeEquipResult.InvalidPart;
            if (!IsBodyEarOwned(ear)) return CostumeEquipResult.NotOwned;
            if (!BodyMeshesResolve(catalog, BodyColor, ear)) return CostumeEquipResult.InvalidPart;
            if (BodyEar == ear) return CostumeEquipResult.AlreadyEquipped;

            string prev = Data.bodyEar; Data.bodyEar = ear;
            if (!Commit()) { Data.bodyEar = prev; return CostumeEquipResult.SaveFailed; }
            Notify(Change.Costume); return CostumeEquipResult.Equipped;
        }

        private static bool BodyMeshesResolve(ModularCostumeCatalog catalog, string color, string ear)
        {
            var body = catalog.FindPartByName(ModularCostumeCatalog.BodySlot, ModularCostumeCatalog.BodyMeshName(color));
            var head = catalog.FindPartByName(ModularCostumeCatalog.BodySlot, ModularCostumeCatalog.BodyHeadName(color, ear));
            return body.HasValue && body.Value.skinnedMesh != null && head.HasValue && head.Value.skinnedMesh != null;
        }

        private static bool Commit() => TryCommit(null, "[PlayerProfile] Save failed - rollback.");

        public static string GetPart(string slot)
        {
            var parts = Data.equippedParts;
            for (int i = 0; i < parts.Count; i++)
                if (parts[i].slot == slot) return parts[i].guid;
            return null;
        }

        /// guid = null/"" -> bo chon part slot do (ve default cua prefab).
        /// Raw setter KHONG validate catalog/ownership — UI phai dung TryEquipCostume/TryClearCostumeSlot.
        public static void SetPart(string slot, string guid)
        {
            if (string.IsNullOrEmpty(slot)) return;
            if (!SetPartInMemory(slot, guid)) return;
            SaveNow();
            Notify(Change.Costume);
        }

        /// True neu co thay doi thuc su (dung cho batch: gom nhieu thay doi vao 1 save/event).
        private static bool SetPartInMemory(string slot, string guid)
        {
            var parts = Data.equippedParts;
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].slot != slot) continue;
                if (string.IsNullOrEmpty(guid)) { parts.RemoveAt(i); return true; }
                if (parts[i].guid == guid) return false;
                parts[i] = new LoadoutState.PartSel { slot = slot, guid = guid };
                return true;
            }
            if (string.IsNullOrEmpty(guid)) return false;
            parts.Add(new LoadoutState.PartSel { slot = slot, guid = guid });
            return true;
        }
    }
}
