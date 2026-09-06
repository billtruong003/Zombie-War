using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// The one documented formula for "how strong is this player right now", used by the campaign
    /// stage gates.
    ///
    /// It is built from SUSTAINED damage per second, not raw <see cref="WeaponData.damage"/>, because
    /// raw damage ranks a slow shotgun slug above a fast SMG and would gate the player on the wrong
    /// thing. Sustained DPS accounts for:
    ///   * pellets per shot (a shotgun fires many projectiles per trigger pull),
    ///   * fire rate,
    ///   * magazine size and reload time - a weapon that reloads half the time does half the damage,
    ///   * permanent star scaling via <see cref="WeaponUpgradeMath"/>.
    ///
    /// Deliberately NOT included: price, rarity or catalog order. Those are commerce facts, not
    /// combat facts, and gating on them would let an expensive weak weapon unlock a hard stage.
    /// </summary>
    public static class CombatPower
    {
        /// <summary>Scales raw DPS into the friendlier 3-4 digit range players expect from a
        /// "power" number. Pure presentation - it cannot change the ORDER of any comparison.</summary>
        public const float PowerScale = 10f;

        /// <summary>
        /// Sustained damage per second at continuous fire, with star scaling.
        ///
        /// M4 removed the magazine/reload cycle from this model because it no longer exists in the
        /// game. The old formula amortised a full magazine over "time to empty it + one reload",
        /// which was the correct description of a weapon that had to stop. Weapons never stop now,
        /// so sustained DPS is simply what the weapon does every second it holds a target:
        ///
        ///     damage per shot x pellets x shots per second
        ///
        /// This raises every weapon's number, but it does not silently reorder them: reload downtime
        /// was the only term that differed between weapons here beyond damage and rate, so removing
        /// it rescales rather than reshuffles - except where a weapon's identity was specifically a
        /// long reload, which is called out in the M4 balance table in the design doc.
        ///
        /// Splash, penetration and other conditional effects are deliberately NOT modelled. They are
        /// situational multipliers whose real value depends on enemy density, and inventing a
        /// coefficient for them would be false precision in a number the player reads as authoritative.
        /// </summary>
        public static float EffectiveDps(WeaponData weapon, int starLevel)
        {
            if (weapon == null) return 0f;

            float damagePerShot = WeaponUpgradeMath.EffectiveDamage(weapon, starLevel)
                                  * Mathf.Max(1, weapon.pelletCount);
            float fireRate = WeaponUpgradeMath.EffectiveFireRate(weapon, starLevel);
            if (fireRate <= 0f) return 0f;

            return damagePerShot * fireRate;
        }

        /// <summary>Power contributed by a single weapon.</summary>
        public static float WeaponPower(WeaponData weapon, int starLevel) =>
            EffectiveDps(weapon, starLevel) * PowerScale;

        /// <summary>
        /// Total power for a loadout. The best weapon counts in full; the others count at a reduced
        /// weight because only one can be firing at a time - but they still count, so filling all
        /// three slots is always better than leaving them empty (slot coverage).
        /// </summary>
        public static int Evaluate(IReadOnlyList<WeaponData> equipped)
        {
            if (equipped == null || equipped.Count == 0) return 0;

            float best = 0f;
            float rest = 0f;
            for (int i = 0; i < equipped.Count; i++)
            {
                var w = equipped[i];
                if (w == null) continue;

                float power = WeaponPower(w, PlayerProfile.GetWeaponLevel(w.WeaponId));
                if (power > best)
                {
                    rest += best;     // the previous best demotes to a backup
                    best = power;
                }
                else rest += power;
            }

            const float BackupWeight = 0.35f;
            return Mathf.RoundToInt(best + rest * BackupWeight);
        }

        /// <summary>Power of what the player currently has equipped, resolved through the profile's
        /// three weapon slots against the given arsenal.</summary>
        public static int Current(IReadOnlyList<WeaponData> arsenal)
        {
            if (arsenal == null) return 0;

            var equipped = new List<WeaponData>(3);
            for (int slot = 0; slot < 3; slot++)
            {
                string id = PlayerProfile.GetWeaponSlot(slot);
                if (string.IsNullOrEmpty(id)) continue;

                for (int i = 0; i < arsenal.Count; i++)
                {
                    if (arsenal[i] != null && arsenal[i].WeaponId == id)
                    {
                        equipped.Add(arsenal[i]);
                        break;
                    }
                }
            }
            return Evaluate(equipped);
        }
    }
}
