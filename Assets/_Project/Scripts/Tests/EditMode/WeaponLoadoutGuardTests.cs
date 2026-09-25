using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ZombieWar;

namespace ZombieWar.Tests
{
    /// <summary>
    /// Softlock guards for a player who ends up with no usable weapon - an empty roster, a saved
    /// weapon id that no longer resolves, or a WeaponData whose prefab was deleted.
    ///
    /// Before these guards, an empty `weapons` list turned the auto-fire tick into a
    /// NullReferenceException once per frame (`Current.range`), which is not recoverable in a
    /// shipped build.
    ///
    /// Start() does not run in EditMode, so the equip-on-spawn fallback is a manual check; what is
    /// asserted here is that every public entry point survives the degenerate state.
    /// </summary>
    public class WeaponLoadoutGuardTests
    {
        private readonly List<Object> _created = new();

        private Weapon MakeWeapon(params WeaponData[] roster)
        {
            var go = new GameObject("WeaponGuardTest");
            _created.Add(go);
            var weapon = go.AddComponent<Weapon>();

            var so = new UnityEditor.SerializedObject(weapon);
            var list = so.FindProperty("weapons");
            list.arraySize = roster.Length;
            for (int i = 0; i < roster.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = roster[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            return weapon;
        }

        private WeaponData MakeData(string id)
        {
            var data = ScriptableObject.CreateInstance<WeaponData>();
            data.name = id;
            data.damage = 10f;
            data.range = 12f;
            _created.Add(data);
            return data;   // deliberately no weaponPrefab: an asset that cannot be equipped
        }

        // Several of these deliberately drive the refusal paths, which log an error by design. The
        // Unity runner fails a test on any unexpected LogError, so the whole fixture opts out -
        // the assertions here are about not throwing, not about what got logged.
        /// <summary>
        /// Declares one expected "equip refused" error.
        ///
        /// These tests deliberately author WeaponData with no prefab, so the refusal is the behaviour
        /// under test, not an accident. `LogAssert.ignoreFailingMessages` was used here before and
        /// did NOT suppress them - the runner still failed on the unhandled message, which is why two
        /// tests in this fixture failed persistently. Expecting each message explicitly fixes that and
        /// is stricter: an unexpected error still fails the test.
        /// </summary>
        private static void ExpectRefusal(string weaponName) =>
            LogAssert.Expect(LogType.Error, $"[Weapon] Equip refused: '{weaponName}' has no weaponPrefab.");

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created)
                if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        [Test]
        public void Equip_Null_IsRefusedWithoutThrowing()
        {
            var weapon = MakeWeapon();
            Assert.IsFalse(weapon.Equip(null));
            Assert.IsNull(weapon.Current);
        }

        [Test]
        public void Current_IsNullOnEmptyRoster_RatherThanThrowing()
        {
            var weapon = MakeWeapon();
            Assert.IsNull(weapon.Current);
            Assert.AreEqual(0f, weapon.CurrentFireRate, "an unarmed weapon has no cadence");
        }

        [Test]
        public void TryFire_WithNothingEquipped_DoesNotThrow()
        {
            var weapon = MakeWeapon();
            Assert.DoesNotThrow(() => weapon.TryFire(Vector3.forward));
        }

        [Test]
        public void TryFire_WithRosterEntryButNoEquippedInstance_DoesNotThrow()
        {
            // A roster entry that was never instantiated (Start has not run) is not a weapon in hand.
            var weapon = MakeWeapon(MakeData("WD_Test"));
            Assert.IsNull(weapon.Current, "an un-instantiated roster entry must not read as armed");
            Assert.DoesNotThrow(() => weapon.TryFire(Vector3.forward));
        }

        [Test]
        public void Equip_UnusableWeapon_KeepsThePlayerUnarmedRatherThanHalfEquipped()
        {
            var weapon = MakeWeapon();
            ExpectRefusal("WD_A");
            Assert.IsFalse(weapon.Equip(MakeData("WD_A")));
            Assert.IsNull(weapon.Current, "a refused equip must not leave a half-equipped weapon");
            Assert.DoesNotThrow(() => weapon.TryFire(Vector3.forward));
        }

        [Test]
        public void WeaponsRosterIsExposedEvenWhenEmpty()
        {
            var weapon = MakeWeapon();
            Assert.IsNotNull(weapon.Weapons, "the loadout fallback reads this - it must never be null");
            Assert.AreEqual(0, weapon.Weapons.Count);
        }

        [Test]
        public void ResolveOnEmptyOrUnknownIdReturnsNullWithoutTouchingState()
        {
            var arsenal = new List<WeaponData> { MakeData("WD_Known") };

            Assert.IsNull(LoadoutState.Resolve("", arsenal));
            Assert.IsNull(LoadoutState.Resolve(null, arsenal));
            Assert.IsNull(LoadoutState.Resolve("weapon.deleted.gone", arsenal),
                "a saved id that no longer resolves must not throw or clear the arsenal");
            Assert.IsNull(LoadoutState.Resolve("weapon.any", null));
            Assert.AreEqual(1, arsenal.Count);
        }
    }
}
