using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar
{
    /// Loadout nguoi choi chon o MENU (3 slot sung + modular parts). Storage nam trong
    /// PlayerProfile (versioned, luu qua Bill.Save) — class nay giu nguyen seam cu cho UI/gameplay:
    /// PlayerSpawner van goi ApplyTo(weapon) sau khi spawn, CharacterModularApplier van doc Parts.
    /// Weapon id = WeaponData.WeaponId (stable). Save cu (truoc migration) luu ten asset
    /// (vd "WD_Pistol") — Resolve fallback qua LegacyAliases, ApplyTo canonical hoa o lan load dau.
    public static class LoadoutState
    {
        [Serializable]
        public struct PartSel
        {
            public string slot;   // logical costume slot ("Hair", "Face", ...)
            public string guid;   // asset guid trong ModularCostumeCatalog
        }

        /// Profile luon ton tai sau lan truy cap dau (fresh profile duoc seed starter o ApplyTo).
        public static bool HasSave => PlayerProfile.HasProfile;

        // ===== Weapon slots (0=pistol, 1-2=long) =====

        public static string GetWeaponId(int slot) => PlayerProfile.GetWeaponSlot(slot);

        /// id = "" nghia la slot trong (chi hop le cho slot 1-2).
        public static void SetWeaponId(int slot, string id) => PlayerProfile.SetWeaponSlot(slot, id);

        public enum EquipResult { Equipped, NotOwned, Incompatible, InvalidSlot, InvalidWeapon }

        /// Thao tac equip tu UI: kiem tra slot contract (0 = 1-tay, 1-2 = 2-tay), so huu qua
        /// PlayerProfile, roi persist WeaponId chuan. That bai = KHONG doi bat ky state nao.
        /// Rule trung lap (documented): mot khau chi nam trong 1 slot — equip vao slot dai nay
        /// khi dang nam o slot dai kia thi GO khoi slot cu (move, khong swap ngam).
        public static EquipResult TryEquip(int slot, WeaponData data)
        {
            if (slot < 0 || slot > 2) return EquipResult.InvalidSlot;
            if (data == null || string.IsNullOrEmpty(data.WeaponId)) return EquipResult.InvalidWeapon;
            bool slotWantsTwoHanded = slot != 0;
            if (data.twoHanded != slotWantsTwoHanded) return EquipResult.Incompatible;
            if (!PlayerProfile.IsWeaponOwned(data.WeaponId)) return EquipResult.NotOwned;

            if (slot != 0)
            {
                int otherLong = slot == 1 ? 2 : 1;
                if (PlayerProfile.GetWeaponSlot(otherLong) == data.WeaponId)
                    PlayerProfile.SetWeaponSlot(otherLong, "");
            }
            PlayerProfile.SetWeaponSlot(slot, data.WeaponId);
            return EquipResult.Equipped;
        }

        /// Tim WeaponData trong arsenal. Uu tien WeaponId (stable) - fallback LegacyAliases (ten asset
        /// TRUOC KHI rename, vd "WD_Pistol") de khong mat loadout nguoi choi da luu truoc migration.
        /// KHONG dung arsenal[i].name lam fallback: AssetDatabase.MoveAsset doi luon .name, nen sau
        /// migration khong con asset nao con giu ten cu de so khop.
        public static WeaponData Resolve(string id, IReadOnlyList<WeaponData> arsenal)
        {
            if (string.IsNullOrEmpty(id) || arsenal == null) return null;
            for (int i = 0; i < arsenal.Count; i++)
                if (arsenal[i] != null && arsenal[i].WeaponId == id) return arsenal[i];
            for (int i = 0; i < arsenal.Count; i++)
            {
                var aliases = arsenal[i]?.LegacyAliases;
                if (aliases == null) continue;
                for (int j = 0; j < aliases.Count; j++)
                    if (aliases[j] == id) return arsenal[i];
            }
            return null;
        }

        /// Ghi loadout da luu vao Weapon slot API ngay sau khi spawn player.
        /// Buoc 1 (pure data): PlayerProfile.EnsureValidLoadout — seed starter cho profile moi
        /// (khau 1-tay dau tien trong arsenal, dung rule cua Weapon.Start), canonical hoa id legacy,
        /// dam bao sung dang trang bi deu owned. Buoc 2: trang bi tu profile vao slot API.
        /// Id khong resolve duoc thi KHONG trang bi va KHONG thay the — Weapon.Start tu fallback.
        public static void ApplyTo(Weapon weapon)
        {
            if (weapon == null || !weapon.UseSlotSystem) return;

            var arsenal = weapon.Weapons;
            PlayerProfile.EnsureValidLoadout(arsenal);

            var pistol = Resolve(PlayerProfile.GetWeaponSlot(0), arsenal);
            if (pistol != null && !pistol.twoHanded)
                weapon.EquipToSlot(0, pistol);

            ApplyLongSlot(weapon, 1, arsenal);
            ApplyLongSlot(weapon, 2, arsenal);
        }

        private static void ApplyLongSlot(Weapon weapon, int slot, IReadOnlyList<WeaponData> arsenal)
        {
            string id = PlayerProfile.GetWeaponSlot(slot);
            if (string.IsNullOrEmpty(id)) { weapon.UnequipSlot(slot); return; }
            var data = Resolve(id, arsenal);
            if (data == null) return; // id co ma khong resolve duoc (asset doi/xoa) -> giu nguyen, khong pha.
            weapon.EquipToSlot(slot, data);
        }

        // ===== Modular costume parts =====

        public static IReadOnlyList<PartSel> Parts => PlayerProfile.Parts;

        public static string GetPart(string slot) => PlayerProfile.GetPart(slot);

        /// guid = null/"" -> bo chon part slot do (ve default).
        public static void SetPart(string slot, string guid) => PlayerProfile.SetPart(slot, guid);
    }
}
