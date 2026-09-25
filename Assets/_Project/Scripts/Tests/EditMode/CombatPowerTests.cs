using NUnit.Framework;
using UnityEngine;
using ZombieWar;

namespace ZombieWar.Tests
{
    /// <summary>Combat Power ranks weapons by what they sustain, not by raw per-shot damage. The
    /// balance audit and the weapon tier ladder read it, so a wrong ordering mis-tiers the arsenal.</summary>
    public class CombatPowerTests
    {
        private WeaponData _smg;
        private WeaponData _shotgun;

        static WeaponData MakeWeapon(string id, float damage, float fireRate, int pellets)
        {
            var w = ScriptableObject.CreateInstance<WeaponData>();
            var so = new UnityEditor.SerializedObject(w);
            so.FindProperty("weaponId").stringValue = id;
            so.ApplyModifiedPropertiesWithoutUndo();
            w.damage = damage;
            w.fireRate = fireRate;
            w.pelletCount = pellets;
            return w;
        }

        [SetUp]
        public void SetUp()
        {
            // Fast, low-damage SMG vs slow, multi-pellet shotgun: raw `damage` would rank these
            // wrongly, which is exactly what CombatPower exists to avoid.
            _smg = MakeWeapon("weapon.smg.test", 10f, 12f, 1);
            _shotgun = MakeWeapon("weapon.shotgun.test", 8f, 1.5f, 8);
        }

        [TearDown]
        public void TearDown()
        {
            if (_smg != null) Object.DestroyImmediate(_smg);
            if (_shotgun != null) Object.DestroyImmediate(_shotgun);
        }

        [Test]
        public void EffectiveDps_AccountsForPelletsAtContinuousFire()
        {
            // No magazine, no reload: sustained DPS is what the weapon does every second.
            // Shotgun: 8 dmg x 8 pellets = 64 per shot, at 1.5 shots/s -> 96 dps.
            float dps = CombatPower.EffectiveDps(_shotgun, 1);

            float expected = WeaponUpgradeMath.EffectiveDamage(_shotgun, 1)
                             * _shotgun.pelletCount
                             * WeaponUpgradeMath.EffectiveFireRate(_shotgun, 1);
            Assert.AreEqual(expected, dps, 0.5f);
            Assert.AreEqual(96f, dps, 0.5f);
        }

        [Test]
        public void EffectiveDps_IsZeroForNullWeapon() =>
            Assert.AreEqual(0f, CombatPower.EffectiveDps(null, 1));

        [Test]
        public void EffectiveDps_RisesWithStarLevel()
        {
            float one = CombatPower.EffectiveDps(_smg, 1);
            float three = CombatPower.EffectiveDps(_smg, 3);
            Assert.Greater(three, one, "star upgrades must raise effective dps");
        }

        [Test]
        public void WeaponPower_KeepsTheDpsOrdering()
        {
            Assert.Greater(CombatPower.WeaponPower(_smg, 1), CombatPower.WeaponPower(_shotgun, 1),
                "120 dps must out-rank 96 dps even though the shotgun hits harder per pellet set");
        }
    }
}
