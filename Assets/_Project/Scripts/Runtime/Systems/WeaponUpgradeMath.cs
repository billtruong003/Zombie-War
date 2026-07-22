using UnityEngine;

namespace ZombieWar
{
    /// <summary>Single source of truth for permanent weapon-star combat scaling.</summary>
    public static class WeaponUpgradeMath
    {
        private static readonly float[] DamageMultipliers = { 0f, 1f, 1.15f, 1.35f };
        private static readonly float[] FireRateMultipliers = { 0f, 1f, 1.05f, 1.12f };

        public static float DamageMultiplier(int level) => DamageMultipliers[Mathf.Clamp(level, 1, 3)];
        public static float FireRateMultiplier(int level) => FireRateMultipliers[Mathf.Clamp(level, 1, 3)];

        public static float EffectiveDamage(WeaponData weapon, int level) =>
            weapon != null ? weapon.damage * DamageMultiplier(level) : 0f;

        public static float EffectiveFireRate(WeaponData weapon, int level) =>
            weapon != null ? weapon.fireRate * FireRateMultiplier(level) : 0f;
    }
}
