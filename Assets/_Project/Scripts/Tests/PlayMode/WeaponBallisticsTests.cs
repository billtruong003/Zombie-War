using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ZombieWar;

namespace ZombieWar.Tests
{
    /// <summary>
    /// Correctness of the two ballistic contracts that actually changed in Phase 2A: pierce target
    /// resolution, and the guard that stops a weapon-authored push from firing on a non-zombie.
    ///
    /// PlayMode rather than EditMode because the real firing path runs <c>Destroy</c> on the previous
    /// weapon model (illegal in edit mode) and needs live physics colliders.
    ///
    /// These prove correctness only. Whether pierce produces *worthwhile* line value in play is a
    /// telemetry/playtest question, measured separately - deliberately not asserted here.
    /// </summary>
    public class WeaponBallisticsTests
    {
        /// <summary>Minimal damage sink. Records every application so double-damage is visible.</summary>
        private sealed class Dummy : MonoBehaviour, IDamageable
        {
            public readonly List<float> Hits = new();
            public void TakeDamage(float amount) => Hits.Add(amount);
        }

        private readonly List<GameObject> _spawned = new();
        private Weapon _weapon;
        private WeaponData _sniper;
        private GameObject _model;

        private static FieldInfo F(string n) => typeof(Weapon).GetField(n, BindingFlags.NonPublic | BindingFlags.Instance);

        private GameObject Track(GameObject go) { _spawned.Add(go); return go; }

        /// <summary>A damageable box at the given distance along +X.</summary>
        private Dummy Target(float x, float size = 1f)
        {
            var go = Track(new GameObject($"Target_{x}"));
            go.transform.position = new Vector3(x, 0f, 0f);
            go.AddComponent<BoxCollider>().size = Vector3.one * size;
            return go.AddComponent<Dummy>();
        }

        /// <summary>A damageable whose collider lives on a CHILD - the duplicate-damage case.</summary>
        private Dummy TargetWithChildColliders(float x)
        {
            var root = Track(new GameObject($"Multi_{x}"));
            root.transform.position = new Vector3(x, 0f, 0f);
            var dmg = root.AddComponent<Dummy>();
            for (int i = 0; i < 3; i++)   // three colliders the line will cross in sequence
            {
                var child = new GameObject($"Hitbox{i}");
                child.transform.SetParent(root.transform, false);
                child.transform.localPosition = new Vector3(i * 0.2f, 0f, 0f);
                child.AddComponent<BoxCollider>().size = Vector3.one * 0.15f;
            }
            return dmg;
        }

        /// <summary>Solid geometry with no IDamageable - must stop the line.</summary>
        private GameObject Wall(float x)
        {
            var go = Track(new GameObject($"Wall_{x}"));
            go.transform.position = new Vector3(x, 0f, 0f);
            go.AddComponent<BoxCollider>().size = new Vector3(0.5f, 4f, 4f);
            return go;
        }

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;

            _model = Track(new GameObject("WeaponModel"));
            var host = Track(new GameObject("WeaponHost"));
            host.transform.position = Vector3.zero;
            _weapon = host.AddComponent<Weapon>();

            _sniper = ScriptableObject.CreateInstance<WeaponData>();
            _sniper.name = "TestSniper";
            _sniper.fireMode = FireMode.PiercingLine;
            _sniper.pierceCount = 3;                 // => at most 4 distinct targets
            _sniper.pierceDamageFalloff = 0.5f;      // halves per successive distinct target
            _sniper.damage = 100f;
            _sniper.fireRate = 100f;                 // cooldown never blocks a test shot
            _sniper.range = 60f;
            _sniper.pelletCount = 1;
            _sniper.spreadAngle = 0f;
            _sniper.knockback = 0f;
            _sniper.twoHanded = true;
            _sniper.weaponPrefab = _model;

            typeof(Weapon).GetField("useSlotSystem", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_weapon, false);
            F("_currentData").SetValue(_weapon, _sniper);
            F("_currentInstance").SetValue(_weapon, _model);
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            if (_sniper != null) Object.DestroyImmediate(_sniper);
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        /// <summary>
        /// Fires one shot down +X through the real firing path.
        ///
        /// M4: there is no magazine to refill and no cooldown to clear. What must be topped up is the
        /// cadence budget, so each call resolves exactly one shot regardless of the weapon's rate.
        /// </summary>
        private void Fire()
        {
            F("_fireAccumulator").SetValue(_weapon, 1000f);
            _weapon.TryFire(Vector3.right);
        }

        // ---- Pierce correctness -------------------------------------------------------------

        [Test]
        public void ChildColliders_OfOneEnemy_DamageItOnlyOnce()
        {
            // The defect this guards: every collider resolved to the same IDamageable, so one enemy
            // took several full hits AND consumed several pierce slots.
            var multi = TargetWithChildColliders(4f);
            Physics.SyncTransforms();

            Fire();

            Assert.AreEqual(1, multi.Hits.Count,
                "three colliders on one enemy must produce exactly one damage application");
        }

        [Test]
        public void PierceCountThree_DamagesAtMostFourDistinctTargets()
        {
            var t = new List<Dummy>();
            for (int i = 1; i <= 6; i++) t.Add(Target(i * 2f));
            Physics.SyncTransforms();

            Fire();

            int damaged = 0;
            foreach (var d in t) if (d.Hits.Count > 0) damaged++;
            Assert.AreEqual(4, damaged, "pierceCount 3 means the shooter plus three pass-throughs");
            Assert.AreEqual(0, t[4].Hits.Count, "the fifth target is past the cap");
            Assert.AreEqual(0, t[5].Hits.Count);
        }

        [Test]
        public void FalloffAppliesSequentially_ByDistanceOrder()
        {
            var a = Target(2f); var b = Target(4f); var c = Target(6f);
            Physics.SyncTransforms();

            Fire();

            Assert.AreEqual(1, a.Hits.Count); Assert.AreEqual(1, b.Hits.Count); Assert.AreEqual(1, c.Hits.Count);
            // 0.5 falloff: nearest full, then half, then quarter. Also proves distance ordering,
            // since RaycastNonAlloc does not return hits sorted.
            Assert.Greater(a.Hits[0], b.Hits[0], "nearer target must take more than the one behind it");
            Assert.Greater(b.Hits[0], c.Hits[0]);
            Assert.AreEqual(0.5f, b.Hits[0] / a.Hits[0], 0.01f, "one falloff step");
            Assert.AreEqual(0.25f, c.Hits[0] / a.Hits[0], 0.01f, "two falloff steps");
        }

        [Test]
        public void BlockingGeometry_StopsDamageBehindIt()
        {
            var front = Target(2f);
            Wall(4f);
            var behind = Target(6f);
            Physics.SyncTransforms();

            Fire();

            Assert.AreEqual(1, front.Hits.Count, "the target in front of the wall is hit");
            Assert.AreEqual(0, behind.Hits.Count, "nothing behind solid geometry may be damaged");
        }

        [Test]
        public void PreviousShotResults_DoNotLeakIntoTheNext()
        {
            var far = Target(10f);
            Physics.SyncTransforms();
            Fire();
            Assert.AreEqual(1, far.Hits.Count);

            // Remove everything and fire into empty space: the shared raycast buffer still holds the
            // previous hits, so a stale-entry bug would damage a destroyed target or throw.
            foreach (var go in _spawned) if (go != null && go.name.StartsWith("Target_")) go.SetActive(false);
            Physics.SyncTransforms();

            int before = far.Hits.Count;
            Assert.DoesNotThrow(Fire);
            Assert.AreEqual(before, far.Hits.Count, "a disabled target must not be damaged by stale buffer data");
        }

        [Test]
        public void NoValidTarget_DoesNotThrowOrPhantomHit()
        {
            Physics.SyncTransforms();   // empty world
            Assert.DoesNotThrow(Fire);
            Assert.DoesNotThrow(Fire);
        }

        // ---- Physical response guard --------------------------------------------------------

        [Test]
        public void ZeroKnockback_NeverInvokesPhysicalPush()
        {
            // The sniper authors knockback 0, so ApplyKnockback must bail before touching the target.
            // A plain IDamageable is not a ZombieBase, so this also covers the non-zombie case.
            var plain = Target(3f);
            Physics.SyncTransforms();

            Assert.DoesNotThrow(Fire);
            Assert.AreEqual(1, plain.Hits.Count, "damage still lands");
            Assert.AreEqual(new Vector3(3f, 0f, 0f), plain.transform.position,
                "a zero-knockback weapon must not move anything");
        }

        [Test]
        public void AuthoredKnockback_OnNonZombieDamageable_DoesNotThrow()
        {
            _sniper.knockback = 2.5f;   // authored push, but the target is not a ZombieBase
            var plain = Target(3f);
            Physics.SyncTransforms();

            Assert.DoesNotThrow(Fire);
            Assert.AreEqual(1, plain.Hits.Count);
            Assert.AreEqual(new Vector3(3f, 0f, 0f), plain.transform.position,
                "no agent to move, and no exception either");
        }
    }
}
