using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar
{
    /// Loadout nguoi choi chon o MENU (3 slot sung + modular parts), persist PlayerPrefs JSON.
    /// Gameplay KHONG doc PlayerPrefs truc tiep - PlayerSpawner goi ApplyTo(weapon) sau khi spawn.
    /// Weapon id = ten asset WeaponData (unique, khong doi khi rename weaponName hien thi).
    public static class LoadoutState
    {
        [Serializable]
        public struct PartSel
        {
            public string slot;   // logical costume slot ("Hair", "Face", ...)
            public string guid;   // asset guid trong ModularCostumeCatalog
        }

        [Serializable]
        private class SaveData
        {
            public string pistol = "";
            public string longA = "";
            public string longB = "";
            public List<PartSel> parts = new();
        }

        private const string Key = "zw.loadout";
        private static SaveData _data;

        private static SaveData Data
        {
            get
            {
                if (_data == null)
                {
                    string json = PlayerPrefs.GetString(Key, "");
                    _data = string.IsNullOrEmpty(json)
                        ? new SaveData()
                        : (JsonUtility.FromJson<SaveData>(json) ?? new SaveData());
                }
                return _data;
            }
        }

        /// Chua tung save -> ApplyTo khong dong vao gi ca (giu default cua prefab).
        public static bool HasSave => PlayerPrefs.HasKey(Key);

        private static void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
        }

        // ===== Weapon slots (0=pistol, 1-2=long) =====

        public static string GetWeaponId(int slot) =>
            slot == 0 ? Data.pistol : slot == 1 ? Data.longA : slot == 2 ? Data.longB : "";

        /// id = "" nghia la slot trong (chi hop le cho slot 1-2).
        public static void SetWeaponId(int slot, string id)
        {
            id ??= "";
            if (slot == 0) { if (id.Length == 0) return; Data.pistol = id; }
            else if (slot == 1) Data.longA = id;
            else if (slot == 2) Data.longB = id;
            else return;
            Save();
        }

        /// Tim WeaponData theo ten asset trong arsenal (list weapons cua player prefab).
        public static WeaponData Resolve(string id, IReadOnlyList<WeaponData> arsenal)
        {
            if (string.IsNullOrEmpty(id) || arsenal == null) return null;
            for (int i = 0; i < arsenal.Count; i++)
                if (arsenal[i] != null && arsenal[i].name == id) return arsenal[i];
            return null;
        }

        /// Ghi loadout da luu vao Weapon slot API ngay sau khi spawn player.
        public static void ApplyTo(Weapon weapon)
        {
            if (weapon == null || !weapon.UseSlotSystem || !HasSave) return;

            var arsenal = weapon.Weapons;

            var pistol = Resolve(Data.pistol, arsenal);
            if (pistol != null && !pistol.twoHanded)
                weapon.EquipToSlot(0, pistol);

            ApplyLongSlot(weapon, 1, Data.longA, arsenal);
            ApplyLongSlot(weapon, 2, Data.longB, arsenal);
        }

        private static void ApplyLongSlot(Weapon weapon, int slot, string id, IReadOnlyList<WeaponData> arsenal)
        {
            if (string.IsNullOrEmpty(id)) { weapon.UnequipSlot(slot); return; }
            var data = Resolve(id, arsenal);
            if (data != null && data.twoHanded) weapon.EquipToSlot(slot, data);
            // id co ma khong resolve duoc (asset doi ten/xoa) -> giu nguyen default, khong pha.
        }

        // ===== Modular costume parts =====

        public static IReadOnlyList<PartSel> Parts => Data.parts;

        public static string GetPart(string slot)
        {
            var parts = Data.parts;
            for (int i = 0; i < parts.Count; i++)
                if (parts[i].slot == slot) return parts[i].guid;
            return null;
        }

        /// guid = null/"" -> bo chon part slot do (ve default).
        public static void SetPart(string slot, string guid)
        {
            if (string.IsNullOrEmpty(slot)) return;
            var parts = Data.parts;
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i].slot != slot) continue;
                if (string.IsNullOrEmpty(guid)) parts.RemoveAt(i);
                else parts[i] = new PartSel { slot = slot, guid = guid };
                Save();
                return;
            }
            if (!string.IsNullOrEmpty(guid))
            {
                parts.Add(new PartSel { slot = slot, guid = guid });
                Save();
            }
        }
    }
}
