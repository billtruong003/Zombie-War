using NUnit.Framework;
using UnityEngine;
using ZombieWar.Skills;

namespace ZombieWar.Tests
{
    /// <summary>
    /// M7.2c — the owner's bug, reproduced as a sequence rather than a unit case.
    ///
    /// Reported: "finish a run, exit, start again — the build is still there", and the same for coin
    /// pickup drops. The cause is a class of defect, not one variable: <b>run-scoped state living in
    /// statics that nothing resets</b>. Statics survive scene loads inside a play session, so run two
    /// inherits run one.
    ///
    /// These tests drive the exact sequence the owner used:
    /// <c>start run → take cards → drop coins → end run → start another run</c>.
    /// </summary>
    public class RunScopeResetTests
    {
        SkillRuntime _previous;

        [SetUp] public void SetUp() => _previous = SkillRuntime.Active;

        [TearDown]
        public void TearDown()
        {
            SkillRuntime.Active = _previous;
            RunState.Abandon();
        }

        /// <summary>
        /// The headline bug. Before the fix this FAILED: the second run still held rank 3 in every
        /// card taken during the first.
        /// </summary>
        [Test]
        public void SkillBuildDoesNotSurviveIntoTheNextRun()
        {
            RunState.Begin("test_level");
            SkillRuntime.Active = new SkillRuntime { EquippedFamily = WeaponClass.AssaultRifle };
            SkillRuntime.Active.Take(SkillCatalogDefs.StatDamage);
            SkillRuntime.Active.Take(SkillCatalogDefs.StatDamage);
            SkillRuntime.Active.Take(SkillCatalogDefs.AutoChainLightning);

            Assert.AreEqual(2, SkillRuntime.Active.RankOf(SkillCatalogDefs.StatDamage), "sanity: run one built something");

            RunState.Abandon();
            RunState.Begin("test_level");   // the new run

            Assert.IsNotNull(SkillRuntime.Active, "a run still needs a runtime");
            Assert.AreEqual(0, SkillRuntime.Active.RankOf(SkillCatalogDefs.StatDamage),
                "the previous run's Damage Up must not carry over");
            Assert.AreEqual(0, SkillRuntime.Active.RankOf(SkillCatalogDefs.AutoChainLightning),
                "the previous run's Chain Lightning must not carry over");
            Assert.AreEqual(1f, SkillRuntime.Active.DamageMultiplier, 1e-4,
                "a fresh run starts at base damage");
        }

        [Test]
        public void EnemyStatusesDoNotSurviveIntoTheNextRun()
        {
            RunState.Begin("test_level");
            StatusCarrier.Apply(1234, StatusKind.Exposed, 1f, 999f, 0f);
            StatusCarrier.Accumulate(1234, StatusKind.HitCount, 7f, 999f, 0f);
            Assert.IsTrue(StatusCarrier.Has(1234, StatusKind.Exposed, 1f), "sanity: run one marked an enemy");

            RunState.Abandon();
            RunState.Begin("test_level");

            Assert.AreEqual(0f, StatusCarrier.Get(1234, StatusKind.Exposed, 1f),
                "an Exposed enemy from the last run must not brand a fresh one");
            Assert.AreEqual(0, StatusCarrier.TrackedCount, "no status records may survive a run boundary");
        }

        [Test]
        public void AutonomousProcBudgetDoesNotSurviveIntoTheNextRun()
        {
            RunState.Begin("test_level");
            var power = new AutonomousPower("p", AutonomousPower.TriggerKind.Interval, 0.01f);
            // Spend the whole global 2-procs/second budget inside run one.
            power.TryProc(0f);
            power.TryProc(0.1f);
            Assert.IsFalse(power.TryProc(0.2f), "sanity: run one exhausted the shared budget");

            RunState.Abandon();
            RunState.Begin("test_level");

            var fresh = new AutonomousPower("p2", AutonomousPower.TriggerKind.Interval, 0.01f);
            Assert.IsTrue(fresh.TryProc(0.3f),
                "a new run must start with a clean proc budget, not the last run's exhausted one");
        }

        /// <summary>
        /// The coin half of the owner's report. `PickupManager.Live` is a static list that pickups
        /// register into; nothing cleared it, so entries from previous runs accumulated — including
        /// destroyed ones. Fixed generally (through the run-reset path), not by special-casing coins.
        /// </summary>
        [Test]
        public void PickupRegistryDoesNotSurviveIntoTheNextRun()
        {
            RunState.Begin("test_level");

            var go = new GameObject("coin");
            var pickup = go.AddComponent<Pickup>();
            PickupManager.Register(pickup);
            Assert.AreEqual(1, PickupManager.LiveCount, "sanity: run one dropped a coin");

            RunState.Abandon();
            RunState.Begin("test_level");

            Assert.AreEqual(0, PickupManager.LiveCount,
                "coins from the previous run must not still be registered in the new one");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void OneResetPathCoversEverySystemAtOnce()
        {
            SkillRuntime.Active = new SkillRuntime();
            SkillRuntime.Active.Take(SkillCatalogDefs.StatDamage);
            StatusCarrier.Apply(99, StatusKind.Slow, 1f, 999f, 0f);

            RunScope.ResetAll();

            Assert.AreEqual(0, SkillRuntime.Active.RankOf(SkillCatalogDefs.StatDamage));
            Assert.AreEqual(0, StatusCarrier.TrackedCount);
            Assert.AreEqual(0, PickupManager.LiveCount);
        }

        [Test]
        public void ResetIsIdempotentAndSafeWithNoRuntime()
        {
            SkillRuntime.Active = null;
            Assert.DoesNotThrow(() => RunScope.ResetAll());
            Assert.DoesNotThrow(() => RunScope.ResetAll());
        }

        [Test]
        public void RunStateBeginIsTheSinglePlaceResetHappens()
        {
            string src = System.IO.File.ReadAllText(
                Application.dataPath + "/_Project/Scripts/Runtime/Systems/RunState.cs");
            StringAssert.Contains("RunScope.ResetAll", src,
                "the run lifecycle must own the reset — scattering Clear() calls is how this bug returned twice");
        }
    }
}
