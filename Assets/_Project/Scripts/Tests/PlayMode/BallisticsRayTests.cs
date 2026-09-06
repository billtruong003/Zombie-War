using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ZombieWar;

namespace ZombieWar.Tests
{
    /// <summary>
    /// M5.1.2 CP1+CP2 - the shot contract, proven against ACTUAL physics rays.
    ///
    /// Every assertion here reads Weapon.RayProbe, which is emitted with the exact origin and
    /// direction handed to Physics.Raycast, one record per pellet, AFTER spread. (M5.1.1's evidence
    /// read the pre-spread base vector; these tests exist so that cannot pass for a spread weapon.)
    /// </summary>
    public class BallisticsRayTests
    {
        private sealed class StubTarget : MonoBehaviour, ITargetable
        {
            public bool Targetable = true;
            Transform ITargetable.Transform => transform;
            bool ITargetable.IsTargetable => Targetable;
        }

        private readonly List<GameObject> _spawned = new();
        private readonly List<Weapon.ShotRay> _rays = new();
        private GameObject _playerGo;
        private PlayerMovement _player;
        private Weapon _weapon;
        private GameObject _model;

        private static FieldInfo WF(string n) => typeof(Weapon).GetField(n, BindingFlags.NonPublic | BindingFlags.Instance);
        private static FieldInfo PF(string n) => typeof(PlayerMovement).GetField(n, BindingFlags.NonPublic | BindingFlags.Instance);

        private GameObject Track(GameObject go) { _spawned.Add(go); return go; }

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            RunState.Abandon();

            _playerGo = Track(new GameObject("RayPlayer", typeof(Rigidbody)));
            _playerGo.GetComponent<Rigidbody>().isKinematic = true;
            _player = _playerGo.AddComponent<PlayerMovement>();
            // Disable FIRST (OnDisable clears Instance), THEN pin Instance - aim state is set
            // explicitly by each test and FixedUpdate must not overwrite it.
            _player.enabled = false;
            typeof(PlayerMovement).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                .SetValue(null, _player);

            _model = Track(new GameObject("Model"));
            var host = Track(new GameObject("WeaponHost"));
            _weapon = host.AddComponent<Weapon>();

            _rays.Clear();
            Weapon.RayProbe += OnRay;
        }

        [TearDown]
        public void TearDown()
        {
            Weapon.RayProbe -= OnRay;
            typeof(PlayerMovement).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                .SetValue(null, null);
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            LogAssert.ignoreFailingMessages = false;
        }

        private void OnRay(Weapon.ShotRay ray) => _rays.Add(ray);

        private WeaponData MakeWeapon(float fireRate, float spread = 0f, int pellets = 1,
                                      FireMode mode = FireMode.SingleHitscan)
        {
            var d = ScriptableObject.CreateInstance<WeaponData>();
            d.weaponName = "Test";
            d.damage = 5f;
            d.fireRate = fireRate;
            d.range = 30f;
            d.spreadAngle = spread;
            d.pelletCount = pellets;
            d.fireMode = mode;
            d.weaponPrefab = _model;
            Track(d.weaponPrefab);
            return d;
        }

        private void Equip(WeaponData d)
        {
            typeof(Weapon).GetMethod("EquipData", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_weapon, new object[] { d });
        }

        private void SetAim(Vector3 dir, bool hasTarget = true, float distance = 5f)
        {
            var flags = BindingFlags.Public | BindingFlags.Instance;
            typeof(PlayerMovement).GetProperty("AimDirection", flags).SetValue(_player, dir.normalized);
            typeof(PlayerMovement).GetProperty("HasTarget", flags).SetValue(_player, hasTarget);
            typeof(PlayerMovement).GetProperty("AimTargetDistance", flags).SetValue(_player, distance);
        }

        private void SetAimTarget(StubTarget target) => PF("_aimTarget").SetValue(_player, target);

        private void Fire(Vector3 aim)
        {
            WF("_fireAccumulator").SetValue(_weapon, 10f);   // budget for one shot
            typeof(Weapon).GetMethod("TryFire", BindingFlags.Public | BindingFlags.Instance)
                .Invoke(_weapon, new object[] { aim });
        }

        // ---------------------------------------------------------------- direction contracts

        [Test]
        public void ZeroSpread_RayEqualsVisibleAim()
        {
            Equip(MakeWeapon(5f));
            SetAim(Vector3.right);
            Fire(Vector3.right);

            Assert.AreEqual(1, _rays.Count, "one ray per zero-spread shot");
            Assert.Less(Vector3.Angle(_rays[0].RayDirection, Vector3.right), 0.01f,
                "zero-spread ray must equal the authoritative visible aim");
        }

        [TestCase(1.5f)]   // G36C
        [TestCase(2f)]     // SMG
        [TestCase(3f)]     // LMG
        public void SpreadWeapon_EveryActualRayStaysInsideAuthoredCone(float spread)
        {
            Equip(MakeWeapon(5f, spread));
            SetAim(Vector3.forward);
            for (int i = 0; i < 40; i++) Fire(Vector3.forward);

            Assert.AreEqual(40, _rays.Count);
            foreach (var r in _rays)
                Assert.LessOrEqual(Vector3.Angle(r.RayDirection, Vector3.forward), spread * 0.5f + 0.05f,
                    $"actual ray escaped the authored {spread}° cone");
            bool anyDeviates = false;
            foreach (var r in _rays)
                if (Vector3.Angle(r.RayDirection, Vector3.forward) > 0.01f) anyDeviates = true;
            Assert.IsTrue(anyDeviates, "spread produced no deviation - the probe is not reporting real rays");
        }

        [Test]
        public void Shotgun_ReportsOneRecordPerPellet_EachInsideCone()
        {
            Equip(MakeWeapon(2f, spread: 12f, pellets: 6));
            SetAim(Vector3.forward);
            Fire(Vector3.forward);

            Assert.AreEqual(6, _rays.Count, "one probe record per pellet");
            for (int i = 0; i < 6; i++)
            {
                Assert.AreEqual(i, _rays[i].RayIndex);
                Assert.AreEqual(6, _rays[i].RayCount);
                Assert.LessOrEqual(Vector3.Angle(_rays[i].RayDirection, Vector3.forward), 6.05f,
                    "pellet escaped the authored cone");
            }
        }

        [Test]
        public void PiercingWeapon_ProbeReportsTheActualLine()
        {
            Equip(MakeWeapon(1f, mode: FireMode.PiercingLine));
            SetAim(Vector3.right);
            Fire(Vector3.right);

            Assert.AreEqual(1, _rays.Count);
            Assert.Less(Vector3.Angle(_rays[0].RayDirection, Vector3.right), 0.01f);
        }

        [Test]
        public void Strafing_LocomotionNeverEntersTheRay()
        {
            Equip(MakeWeapon(5f));
            SetAim(Vector3.right);
            _playerGo.transform.position += new Vector3(0f, 0f, 3f);   // "moved" sideways
            Fire(Vector3.right);

            Assert.AreEqual(1, _rays.Count);
            Assert.Less(Vector3.Angle(_rays[0].RayDirection, Vector3.right), 0.01f,
                "the ray must follow the visible aim, not the movement");
        }

        // ---------------------------------------------------------------- gate + staleness

        [Test]
        public void MisalignedAim_HoldsFire_ThenFiresOnceAligned()
        {
            Equip(MakeWeapon(5f));
            var stub = Track(new GameObject("Stub")).AddComponent<StubTarget>();
            stub.transform.position = _playerGo.transform.position + Vector3.forward * 4f;
            SetAimTarget(stub);
            SetAim(Vector3.right);   // visible aim 90° away from the target

            var tick = typeof(Weapon).GetMethod("TickAutoFire", BindingFlags.NonPublic | BindingFlags.Instance);
            for (int i = 0; i < 30; i++) tick.Invoke(_weapon, new object[] { 0.05f });
            Assert.AreEqual(0, _rays.Count, "fired while the visible aim was far outside the alignment cone");

            SetAim(Vector3.forward);   // aligned now
            tick.Invoke(_weapon, new object[] { 0.05f });
            Assert.AreEqual(1, _rays.Count, "did not fire promptly once aligned");

            // No banked burst: the very next tick may fire at most one more shot at this cadence.
            tick.Invoke(_weapon, new object[] { 0.05f });
            Assert.LessOrEqual(_rays.Count, 2, "alignment recovery dumped a banked burst");
        }

        [Test]
        public void DeadTarget_RefusesShotWithNoSideEffects()
        {
            Equip(MakeWeapon(5f));
            var stub = Track(new GameObject("Stub")).AddComponent<StubTarget>();
            stub.transform.position = _playerGo.transform.position + Vector3.right * 4f;
            stub.Targetable = false;   // "died" between the aim update and the shot
            SetAimTarget(stub);
            SetAim(Vector3.right);

            WF("_fireAccumulator").SetValue(_weapon, 0.5f);
            typeof(Weapon).GetMethod("TryFire", BindingFlags.Public | BindingFlags.Instance)
                .Invoke(_weapon, new object[] { Vector3.right });

            Assert.AreEqual(0, _rays.Count, "a dead target still produced a physics ray");
            Assert.AreEqual(0.5f, (float)WF("_fireAccumulator").GetValue(_weapon), 0.0001f,
                "a refused shot must not consume cadence");
        }
    }
}
