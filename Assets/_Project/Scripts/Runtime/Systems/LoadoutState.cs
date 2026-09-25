using System;
using System.Collections.Generic;

namespace ZombieWar
{
    /// The player's menu-side choices: the ONE weapon taken into a run and the modular costume parts.
    /// Storage lives in PlayerProfile (versioned, saved through Bill.Save); this class keeps the seam
    /// the UI and gameplay already use - PlayerSpawner calls ApplyTo(weapon) after spawning, and
    /// CharacterModularApplier reads Parts.
    /// Weapon id = WeaponData.WeaponId (stable). Old saves stored asset names (e.g. "WD_Pistol");
    /// Resolve falls back through LegacyAliases and ApplyTo canonicalises on the first load.
    public static class LoadoutState
    {
        [Serializable]
        public struct PartSel
        {
            public string slot;   // logical costume slot ("Hair", "Face", ...)
            public string guid;   // asset guid in the costume catalog
        }

        public static bool HasSave => PlayerProfile.HasProfile;

        // ===== Run weapon =====

        public static string WeaponId => PlayerProfile.EquippedWeaponId;

        public enum EquipResult { Equipped, NotOwned, InvalidWeapon }

        /// Equip from the UI: the weapon must be authored and owned. A failure changes nothing.
        public static EquipResult TryEquip(WeaponData data)
        {
            // Authoring gate: a weapon whose grip/muzzle anchors are not hand-authored yet would put
            // a gun in the hand at an arbitrary transform, so it can never become the run weapon.
            if (data == null || !data.IsPlayable || string.IsNullOrEmpty(data.WeaponId))
                return EquipResult.InvalidWeapon;
            if (!PlayerProfile.IsWeaponOwned(data.WeaponId)) return EquipResult.NotOwned;

            PlayerProfile.SetEquippedWeapon(data.WeaponId);
            return EquipResult.Equipped;
        }

        /// Finds a WeaponData in the arsenal. WeaponId first (stable), then LegacyAliases (asset names
        /// from BEFORE the rename) so a pre-migration save does not lose its weapon. Never falls back
        /// to arsenal[i].name: AssetDatabase.MoveAsset renamed those, so no asset still has the old name.
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

        /// Equips the saved run weapon onto a freshly spawned player. EnsureValidLoadout first seeds
        /// the starter for a new profile and canonicalises legacy ids. An id that does not resolve is
        /// left alone and Weapon.Start falls back to its roster - nothing is silently swapped in.
        public static void ApplyTo(Weapon weapon)
        {
            if (weapon == null) return;

            // The catalog is the authoritative roster; the prefab's list is only a fallback for scenes
            // and tests that run without one.
            IReadOnlyList<WeaponData> arsenal = WeaponCatalog.Active != null
                ? WeaponCatalog.Active.AllData()
                : weapon.Weapons;
            PlayerProfile.EnsureValidLoadout(arsenal);

            var data = Resolve(PlayerProfile.EquippedWeaponId, arsenal);
            if (data != null) weapon.Equip(data);
        }

        // ===== Modular costume parts =====

        public static IReadOnlyList<PartSel> Parts => PlayerProfile.Parts;

        public static string GetPart(string slot) => PlayerProfile.GetPart(slot);

        /// guid = null/"" -> clear the part (back to default).
        public static void SetPart(string slot, string guid) => PlayerProfile.SetPart(slot, guid);
    }
}
