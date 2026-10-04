using System;
using System.Collections.Generic;
using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    /// PlayerProfile, part: Weapon ownership and the run weapon.
    public static partial class PlayerProfile
    {
        // ===== Weapon ownership =====

        public static IReadOnlyList<string> OwnedWeaponIds => Data.ownedWeaponIds;

        public static bool IsWeaponOwned(string weaponId) =>
            !string.IsNullOrEmpty(weaponId) && Data.ownedWeaponIds.Contains(weaponId);

        public static void AddOwnedWeapon(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId) || Data.ownedWeaponIds.Contains(weaponId)) return;
            Data.ownedWeaponIds.Add(weaponId);
            SaveNow();
            Notify(Change.Loadout);
        }

        public static int UnlockAllWeaponsForDev(IReadOnlyList<WeaponData> weapons)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || ZW_CHEATS
            if (weapons == null) return 0;
            int added = 0;
            for (int i = 0; i < weapons.Count; i++)
            {
                string id = weapons[i] != null ? weapons[i].WeaponId : null;
                if (string.IsNullOrEmpty(id) || Data.ownedWeaponIds.Contains(id)) continue;
                Data.ownedWeaponIds.Add(id); added++;
            }
            if (added > 0) { SaveNow(); Notify(Change.Loadout); }
            return added;
#else
            return 0;
#endif
        }

        // ===== Run weapon =====

        /// <summary>WeaponId of the one weapon the player takes into a run. "" only on a fresh profile
        /// before <see cref="EnsureValidLoadout"/> has seeded the starter.</summary>
        public static string EquippedWeaponId => Data.weapon;

        public static void SetEquippedWeapon(string id)
        {
            if (string.IsNullOrEmpty(id) || Data.weapon == id) return;
            Data.weapon = id;
            SaveNow();
            Notify(Change.Loadout);
        }

        /// Makes the run weapon valid against the real arsenal before it is shown or equipped:
        /// - Empty -> seed the catalog's starter (an explicit flag, never "lowest CatalogOrder") and
        ///   grant ownership.
        /// - Resolved through a legacy alias -> upgraded to the canonical WeaponId.
        /// - Unresolvable -> warned once and KEPT; nothing is silently swapped in.
        /// - The equipped weapon is always owned.
        /// Pure data - no Weapon component involved, so EditMode tests cover it.
        public static void EnsureValidLoadout(IReadOnlyList<WeaponData> arsenal)
        {
            if (arsenal == null || arsenal.Count == 0) return;
            var d = Data;
            bool changed = false;

            if (string.IsNullOrEmpty(d.weapon))
            {
                var starter = ResolveStarter(arsenal);
                if (starter != null)
                {
                    d.weapon = starter.WeaponId;
                    changed = true;
                }
            }

            changed |= CanonicalizeSlot(ref d.weapon, arsenal, d);

            if (changed)
            {
                SaveNow();
                Notify(Change.Loadout);
            }
        }

        static WeaponData ResolveStarter(IReadOnlyList<WeaponData> arsenal)
        {
            // Only accept the catalog's starter if it is actually in this arsenal, so a stale catalog
            // can never seed a weapon the player cannot equip.
            var catalogStarter = WeaponCatalog.Active?.Starter;
            if (catalogStarter != null && catalogStarter.data != null)
                for (int i = 0; i < arsenal.Count; i++)
                    if (arsenal[i] != null && arsenal[i].WeaponId == catalogStarter.weaponId) return arsenal[i];

            // Fallback so a missing catalog degrades to "first authored sidearm" rather than leaving a
            // new profile unarmed.
            WeaponData starter = null;
            for (int i = 0; i < arsenal.Count; i++)
            {
                var w = arsenal[i];
                if (w == null || w.twoHanded || string.IsNullOrEmpty(w.WeaponId)) continue;
                if (starter == null || w.CatalogOrder < starter.CatalogOrder) starter = w;
            }
            return starter;
        }

        private static bool CanonicalizeSlot(ref string id, IReadOnlyList<WeaponData> arsenal, ProfileData d)
        {
            if (string.IsNullOrEmpty(id)) return false;
            var resolved = LoadoutState.Resolve(id, arsenal);
            if (resolved == null)
            {
                if (_warnedUnknownIds.Add(id))
                    Debug.LogWarning($"[PlayerProfile] Weapon id '{id}' khong resolve duoc trong arsenal — giu nguyen save, khong trang bi.");
                return false;
            }

            bool changed = false;
            if (!string.IsNullOrEmpty(resolved.WeaponId) && id != resolved.WeaponId)
            {
                id = resolved.WeaponId;
                changed = true;
            }
            if (!string.IsNullOrEmpty(resolved.WeaponId) && !d.ownedWeaponIds.Contains(resolved.WeaponId))
            {
                d.ownedWeaponIds.Add(resolved.WeaponId);
                changed = true;
            }
            return changed;
        }
    }
}
