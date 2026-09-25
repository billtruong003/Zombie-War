using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using ZombieWar.Skills;

namespace ZombieWar.Tests
{
    /// <summary>
    /// One behavioural proof per card that used to do nothing: taking it must change the number the
    /// game actually reads. Before these, six cards fed getters nothing consumed, the two ramps reset
    /// every frame, Static Build-up never discharged, Concussion only painted a mark and Hunter's
    /// Mark was never applied.
    /// </summary>
    public class SkillCardBehaviourTests
    {
        private SkillRuntime _previous;
        private GameObject _host;

        [SetUp]
        public void SetUp()
        {
            _previous = SkillRuntime.Active;
            StatusCarrier.ClearAll();
            AutonomousPower.ResetGlobalBudget();
        }

        [TearDown]
        public void TearDown()
        {
            SkillRuntime.Active = _previous;
            StatusCarrier.ClearAll();
            RunState.Abandon();
            if (_host != null) Object.DestroyImmediate(_host);
        }

        static SkillRuntime Build(WeaponClass family, params string[] cards)
        {
            var run = new SkillRuntime { EquippedFamily = family };
            foreach (var c in cards) run.Take(c);
            SkillRuntime.Active = run;
            return run;
        }

        // ── stat cards reach their consumers ────────────────────────────────────────────

        [Test]
        public void FireRateUp_RaisesTheWeaponsRealCadence()
        {
            _host = new GameObject("weapon");
            var weapon = _host.AddComponent<Weapon>();
            var data = ScriptableObject.CreateInstance<WeaponData>();
            data.fireRate = 10f;
            typeof(Weapon).GetField("_currentData", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(weapon, data);

            SkillRuntime.Active = new SkillRuntime();
            float before = weapon.CurrentFireRate;
            Build(WeaponClass.AssaultRifle, SkillCatalogDefs.StatFireRate);
            float after = weapon.CurrentFireRate;

            Object.DestroyImmediate(data);
            Assert.Greater(after, before, "Fire Rate Up must reach Weapon.CurrentFireRate");
        }

        [Test]
        public void MoveSpeedUp_RaisesTheMultiplierMovementReads()
        {
            var run = Build(WeaponClass.Sidearm, SkillCatalogDefs.StatMoveSpeed);
            Assert.Greater(run.MoveSpeedMultiplier, 1f);
        }

        [Test]
        public void CoinGainUp_ScalesCoinEnteringTheLedger()
        {
            var ledger = RunState.Begin();   // a new run resets the build, so take the card after it
            Build(WeaponClass.Sidearm, SkillCatalogDefs.StatCoinGain);
            ledger.AddCurrency(PlayerProfile.CurrencyKind.Coin, 100);
            Assert.AreEqual(125, ledger.Coin, "Coin Gain Up rank 1 is +25%");
        }

        // ── ramps see sustained fire, not single frames ─────────────────────────────────

        [Test]
        public void RunAndGun_RampsWhileMoving()
        {
            var run = Build(WeaponClass.Sidearm, SkillCatalogDefs.SidearmRunGun);
            float idle = run.FireRateMultiplier;
            for (int i = 0; i < 60; i++) run.Tick(1f / 60f, new Vector3(i * 0.1f, 0f, 0f), true, false, 1f);
            Assert.Greater(run.FireRateMultiplier, idle);
        }

        [Test]
        public void BulletHose_RampsUnderSustainedFire()
        {
            var run = Build(WeaponClass.SMG, SkillCatalogDefs.SmgBulletHose);
            float cold = run.FireRateMultiplier;
            for (int i = 0; i < 120; i++) run.Tick(1f / 60f, Vector3.zero, false, true, 1f);
            Assert.Greater(run.FireRateMultiplier, cold);
        }

        [Test]
        public void HeavyPressure_RampsDamageAndSlowsTheShooter()
        {
            var run = Build(WeaponClass.LMG, SkillCatalogDefs.LmgHeavyPressure);
            float coldDamage = run.ModifyHitDamage(100f, 1, 5f, 1f, 0f);
            for (int i = 0; i < 120; i++) run.Tick(1f / 60f, Vector3.zero, false, true, 1f);

            Assert.Greater(run.ModifyHitDamage(100f, 2, 5f, 1f, 0f), coldDamage);
            Assert.Less(run.MoveSpeedMultiplier, 1f, "the ramp's cost is the player's own speed");
        }

        [Test]
        public void FiringWindow_CoversTheGapsBetweenShots()
        {
            // A 10 shots/s gun fires on one frame in six. The grace window must span that gap, or
            // the ramps decay between shots and never climb.
            Assert.Greater(SkillCombatDriver.FiringGraceSeconds, 0.1f);
        }

        // ── discrete triggers ───────────────────────────────────────────────────────────

        [Test]
        public void StaticBuildUp_DischargesAChainAfterEnoughHits()
        {
            var run = Build(WeaponClass.SMG, SkillCatalogDefs.SmgStatic);
            bool fired = false;
            for (int hit = 0; hit < 6 && !fired; hit++)
            {
                run.ApplyHitStatuses(hit, 0f);
                var procs = run.PollPowers(hit, 1f);
                for (int i = 0; i < procs.Count; i++) fired |= procs[i].skillId == SkillCatalogDefs.SmgStatic;
            }
            Assert.IsTrue(fired, "Static Build-up must discharge within one charge cycle");
        }

        [Test]
        public void Concussion_ActuallySlowsTheEnemy()
        {
            _host = new GameObject("enemy");
            var motor = _host.AddComponent<PlanarEnemyMotor>();
            var run = Build(WeaponClass.Shotgun, SkillCatalogDefs.ShotgunConcussion);

            Assert.AreEqual(1f, motor.SlowFactor(), 1e-4f);
            run.ApplyHitStatuses(_host.transform.GetInstanceID(), Time.time);
            Assert.Less(motor.SlowFactor(), 1f, "the slow status must reach the enemy's movement");
        }

        [Test]
        public void HuntersMark_EmpowersTheFirstHitOnANewLock()
        {
            var run = Build(WeaponClass.Marksman, SkillCatalogDefs.MarksmanHunters);
            float unmarked = run.ModifyHitDamage(100f, 7, 20f, 1f, 0f);

            run.OnTargetChanged(8, 0f);
            float marked = run.ModifyHitDamage(100f, 8, 20f, 1f, 0f);
            float second = run.ModifyHitDamage(100f, 8, 20f, 1f, 0f);

            Assert.Greater(marked, unmarked, "the aim lock must mark the target");
            Assert.AreEqual(unmarked, second, 1e-3, "the empower is consumed by the first hit");
        }
    }
}
