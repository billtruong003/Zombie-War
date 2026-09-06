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
    /// Before these guards, an empty `weapons` list turned the HUD's switch button into a
    /// DivideByZeroException (`% weapons.Count`) and the auto-fire tick into a NullReferenceException
    /// once per frame (`Current.range`). Neither is recoverable in a shipped build.
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

        /// <summary>Declares the refusal logged for a null hole in the roster.</summary>
        private static void ExpectNullRefusal() =>
            LogAssert.Expect(LogType.Error, "[Weapon] Equip refused: WeaponData is null.");

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created)
                if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        [Test]
        public void SwitchWeapon_OnEmptyRoster_DoesNotThrow()
        {
            var weapon = MakeWeapon();
            Assert.DoesNotThrow(() => weapon.SwitchWeapon(), "`% weapons.Count` on an empty list");
        }

        [Test]
        public void SwitchWeapon_OnEmptyRoster_IsRepeatable()
        {
            var weapon = MakeWeapon();
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 5; i++) weapon.SwitchWeapon();
            });
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
            // Current can fall back to a roster entry that was never instantiated (Start has not run).
            // The muzzle lookup dereferences the live instance, so this path must bail out early.
            var weapon = MakeWeapon(MakeData("WD_Test"));
            Assert.DoesNotThrow(() => weapon.TryFire(Vector3.forward));
        }

        [Test]
        public void SwitchWeapon_WithOnlyUnusableEntries_DoesNotThrowOrLoop()
        {
            // Every entry lacks a prefab, so no equip can succeed. The cycle must terminate after one
            // pass rather than spinning forever looking for a valid one.
            var weapon = MakeWeapon(MakeData("WD_A"), MakeData("WD_B"));
            ExpectRefusal("WD_B");
            ExpectRefusal("WD_A");
            Assert.DoesNotThrow(() => weapon.SwitchWeapon());
            Assert.IsNull(weapon.Current, "a refused equip must not leave a half-equipped weapon");
        }

        [Test]
        public void RosterWithNullHoles_DoesNotThrow()
        {
            // Cycle order from index 0 is: 1 (WD_Real, no prefab) -> 2 (null) -> 0 (null).
            var weapon = MakeWeapon(null, MakeData("WD_Real"), null);
            ExpectRefusal("WD_Real");
            ExpectNullRefusal();
            ExpectNullRefusal();
            Assert.DoesNotThrow(() => weapon.SwitchWeapon());
            Assert.DoesNotThrow(() => weapon.TryFire(Vector3.forward));
        }

        [Test]
        public void WeaponsRosterIsExposedForTheHudEvenWhenEmpty()
        {
            var weapon = MakeWeapon();
            Assert.IsNotNull(weapon.Weapons, "HUD rebuilds from this - it must never be null");
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
