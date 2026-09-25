using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// The one documented formula for "how strong is this weapon", read by the balance audit and the
    /// weapon tier ladder.
    ///
    /// It is built from SUSTAINED damage per second, not raw <see cref="WeaponData.damage"/>, because
    /// raw damage ranks a slow shotgun slug above a fast SMG. Sustained DPS accounts for pellets per
    /// shot, fire rate and permanent star scaling via <see cref="WeaponUpgradeMath"/>.
    ///
    /// Deliberately NOT included: price, rarity or catalog order. Those are commerce facts, not
    /// combat facts.
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
    }
}
