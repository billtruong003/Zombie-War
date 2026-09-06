using NUnit.Framework;
using UnityEngine;
using ZombieWar.Skills;

namespace ZombieWar.Tests
{
    /// <summary>
    /// M7.2 B1 — each of the nine shared primitives tested directly, before any card is built on it.
    ///
    /// The point of testing primitives rather than cards: twenty of the twenty-three cards are
    /// compositions of these, so a defect here would surface as twenty separate "card bugs". If a card
    /// ever needs bespoke logic, the primitive is wrong and gets fixed here.
    /// </summary>
    public class SkillPrimitiveTests
    {
        [SetUp]
        public void SetUp()
        {
            StatusCarrier.ClearAll();
            DamageInterceptor.Clear();
            AutonomousPower.ResetGlobalBudget();
        }

        // ───────────────────────────────────────────────────────────── P1
        [Test]
        public void P1_StatusExpiresOnItsOwnClock()
        {
            StatusCarrier.Apply(1, StatusKind.Slow, 0.5f, duration: 2f, now: 10f);
            Assert.AreEqual(0.5f, StatusCarrier.Get(1, StatusKind.Slow, 11f), 1e-4);
            Assert.AreEqual(0f, StatusCarrier.Get(1, StatusKind.Slow, 12.5f), 1e-4,
                "an expired status must read as absent, not linger");
        }

        [Test]
        public void P1_AccumulateBuildsPerTargetCounters()
        {
            StatusCarrier.Accumulate(7, StatusKind.HitCount, 1f, 3f, 0f);
            StatusCarrier.Accumulate(7, StatusKind.HitCount, 1f, 3f, 0.5f);
            float v = StatusCarrier.Accumulate(7, StatusKind.HitCount, 1f, 3f, 1f);
            Assert.AreEqual(3f, v, 1e-4);
            Assert.AreEqual(0f, StatusCarrier.Get(8, StatusKind.HitCount, 1f), "counters must not leak across targets");
        }

        [Test]
        public void P1_ClearPreventsARecycledEnemyInheritingStatuses()
        {
            StatusCarrier.Apply(42, StatusKind.Exposed, 1f, 99f, 0f);
            StatusCarrier.Clear(42);
            Assert.AreEqual(0f, StatusCarrier.Get(42, StatusKind.Exposed, 1f),
                "a pooled enemy reusing this id must start clean");
        }

        // ───────────────────────────────────────────────────────────── P2
        [Test]
        public void P2_IntervalPowerRespectsItsCooldown()
        {
            var p = new AutonomousPower("t", AutonomousPower.TriggerKind.Interval, cooldown: 5f);
            Assert.IsTrue(p.TryProc(0f));
            Assert.IsFalse(p.TryProc(2f), "must not fire inside its cooldown");
            Assert.IsTrue(p.TryProc(5.1f));
        }

        [Test]
        public void P2_KillCountPowerNeedsItsKills()
        {
            var p = new AutonomousPower("k", AutonomousPower.TriggerKind.KillCount, 0.1f, killsRequired: 3);
            Assert.IsFalse(p.TryProc(0f));
            p.NotifyKill(); p.NotifyKill();
            Assert.IsFalse(p.TryProc(0f));
            p.NotifyKill();
            Assert.IsTrue(p.TryProc(0f));
            Assert.IsFalse(p.TryProc(1f), "the kill counter resets after firing");
        }

        [Test]
        public void P2_GlobalProcCeilingIsEnforcedAcrossAllPowers()
        {
            // The guardrail is <=2 procs/s across ALL sources, so it cannot live inside one card.
            var a = new AutonomousPower("a", AutonomousPower.TriggerKind.Interval, 0.01f);
            var b = new AutonomousPower("b", AutonomousPower.TriggerKind.Interval, 0.01f);
            var c = new AutonomousPower("c", AutonomousPower.TriggerKind.Interval, 0.01f);

            Assert.IsTrue(a.TryProc(0.0f));
            Assert.IsTrue(b.TryProc(0.1f));
            Assert.IsFalse(c.TryProc(0.2f), "third proc inside one second must be refused");
            Assert.IsTrue(c.TryProc(1.2f), "the window reopens after a second");
        }

        [Test]
        public void P2_HealthThresholdPowerRearmsOnlyAfterRecovery()
        {
            var p = new AutonomousPower("h", AutonomousPower.TriggerKind.HealthThreshold, 0.1f, healthFraction: 0.3f);
            Assert.IsTrue(p.TryProc(0f, 0.25f));
            Assert.IsFalse(p.TryProc(1f, 0.25f), "must not re-fire while still low");
            p.NotifyHealthFraction(0.9f);
            Assert.IsTrue(p.TryProc(2f, 0.25f));
        }

        [Test]
        public void P2_ReadinessReadsZeroToOneForTheHud()
        {
            var p = new AutonomousPower("r", AutonomousPower.TriggerKind.Interval, 4f);
            p.TryProc(0f);
            Assert.AreEqual(0f, p.Readiness(0f), 0.02f);
            Assert.AreEqual(0.5f, p.Readiness(2f), 0.02f);
            Assert.AreEqual(1f, p.Readiness(4f), 0.02f);
        }

        // ───────────────────────────────────────────────────────────── P4
        [Test]
        public void P4_DistanceAccumulatesAndConsumesInThresholds()
        {
            var d = new DistanceAccumulator();
            d.Sample(Vector3.zero);
            d.Sample(new Vector3(3f, 0f, 0f));
            d.Sample(new Vector3(3f, 0f, 4f));
            Assert.AreEqual(7f, d.Distance, 1e-3);

            Assert.IsTrue(d.TryConsume(5f));
            Assert.AreEqual(2f, d.Distance, 1e-3, "consuming spends exactly the threshold, keeping the remainder");
            Assert.IsFalse(d.TryConsume(5f));
        }

        [Test]
        public void P4_FirstSampleSeedsWithoutCountingDistance()
        {
            var d = new DistanceAccumulator();
            d.Sample(new Vector3(100f, 0f, 100f));
            Assert.AreEqual(0f, d.Distance, 1e-4, "spawn position must not count as travel");
        }

        // ───────────────────────────────────────────────────────────── P5
        [Test]
        public void P5_RampRisesWhileActiveAndDecaysWhenNot()
        {
            var r = new RampAccumulator(risePerSecond: 1f, fallPerSecond: 2f);
            r.Tick(true, 0.5f);
            Assert.AreEqual(0.5f, r.Value, 1e-3);
            r.Tick(false, 0.1f);
            Assert.AreEqual(0.3f, r.Value, 1e-3);
            r.Tick(true, 10f);
            Assert.AreEqual(1f, r.Value, 1e-3, "ramp is clamped to 1");
        }

        [Test]
        public void P5_ChargeFiresAtThresholdAndKeepsTheOverflow()
        {
            var r = new RampAccumulator(0f, 0f);
            Assert.IsFalse(r.AddCharge(2f, 5f));
            Assert.IsFalse(r.AddCharge(2f, 5f));
            Assert.IsTrue(r.AddCharge(2f, 5f));
            Assert.AreEqual(1f, r.Value, 1e-3, "overflow carries to the next charge rather than being lost");
        }

        // ───────────────────────────────────────────────────────────── P6
        [Test]
        public void P6_ModifiersComposeInOrderAndUnregisterCleanly()
        {
            DamageInterceptor.Modifier doubler = (float d, in DamageInterceptor.Context c) => d * 2f;
            DamageInterceptor.Modifier plusTen = (float d, in DamageInterceptor.Context c) => d + 10f;

            DamageInterceptor.Register(doubler);
            DamageInterceptor.Register(plusTen);

            var ctx = new DamageInterceptor.Context { targetId = 1, distance = 5f, targetHealthFraction = 1f };
            Assert.AreEqual(30f, DamageInterceptor.Modify(10f, in ctx), 1e-4);

            DamageInterceptor.Unregister(doubler);
            Assert.AreEqual(20f, DamageInterceptor.Modify(10f, in ctx), 1e-4);
        }

        [Test]
        public void P6_ExecutionRoundStyleThresholdReadsTargetHealth()
        {
            DamageInterceptor.Modifier execute = (float d, in DamageInterceptor.Context c) =>
                c.targetHealthFraction <= 0.2f ? d * 3f : d;
            DamageInterceptor.Register(execute);

            var healthy = new DamageInterceptor.Context { targetHealthFraction = 0.9f };
            var wounded = new DamageInterceptor.Context { targetHealthFraction = 0.15f };

            Assert.AreEqual(10f, DamageInterceptor.Modify(10f, in healthy), 1e-4);
            Assert.AreEqual(30f, DamageInterceptor.Modify(10f, in wounded), 1e-4);
        }

        // ───────────────────────────────────────────────────────────── P7
        [Test]
        public void P7_PointBlankAndLongshotCurveInOppositeDirections()
        {
            var pb = DistanceDamageCurve.PointBlank(bonus: 1f, range: 10f);
            Assert.AreEqual(2f, pb.Evaluate(0f), 1e-3);
            Assert.AreEqual(1f, pb.Evaluate(10f), 1e-3);

            var ls = DistanceDamageCurve.Longshot(bonus: 1f, range: 30f);
            Assert.AreEqual(1f, ls.Evaluate(0f), 1e-3);
            Assert.AreEqual(2f, ls.Evaluate(30f), 1e-3);
        }

        // ───────────────────────────────────────────────────────────── P8
        [Test]
        public void P8_SoftCapIsTransparentBelowTheCapAndNeverPassesTheHardCap()
        {
            var c = SoftCap.FireRate;   // soft 2.2, hard 2.5
            Assert.AreEqual(1.5f, c.Apply(1.5f), 1e-4, "below the soft cap nothing is changed");
            Assert.AreEqual(2.2f, c.Apply(2.2f), 1e-4);

            Assert.Less(c.Apply(3f), c.hard, "at realistic stacking the cap is asymptotic, not yet reached");
            // At an absurd raw multiplier the exponential term underflows to zero and the result lands
            // exactly ON the hard cap. That satisfies the guardrail — "hard cap 2.5x" means it may be
            // reached but never exceeded — so this asserts <=, not <.
            Assert.LessOrEqual(c.Apply(100f), c.hard, "no stack of picks may EXCEED the hard cap");
            Assert.LessOrEqual(c.Apply(1e6f), c.hard, "still bounded at extreme input");
            Assert.Greater(c.Apply(3f), 2.2f, "past the soft cap it still improves, just less");
        }

        [Test]
        public void P8_SoftCapIsMonotonic()
        {
            var c = SoftCap.FireRate;
            float prev = 0f;
            for (float raw = 0f; raw < 8f; raw += 0.25f)
            {
                float v = c.Apply(raw);
                Assert.GreaterOrEqual(v, prev, "more raw multiplier must never yield less effective");
                prev = v;
            }
        }

        // ───────────────────────────────────────────────────────────── P9
        [Test]
        public void P9_FxPoolRefusesToExceedItsConcurrencyCeiling()
        {
            var root = new GameObject("fx-root").transform;
            var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var kit = new PowerFxKit(root, maxPerKey: 2);   // <=2 concurrent explosions
            kit.Register("boom", prefab);

            Assert.IsNotNull(kit.Play("boom", Vector3.zero, Quaternion.identity));
            Assert.IsNotNull(kit.Play("boom", Vector3.zero, Quaternion.identity));
            Assert.IsNull(kit.Play("boom", Vector3.zero, Quaternion.identity),
                "the third concurrent effect must be refused, not spawned");

            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(root.gameObject);
        }

        [Test]
        public void P9_RecycledEffectsAreReusedRatherThanReinstantiated()
        {
            var root = new GameObject("fx-root2").transform;
            var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var kit = new PowerFxKit(root, maxPerKey: 1);
            kit.Register("arc", prefab);

            var first = kit.Play("arc", Vector3.zero, Quaternion.identity);
            kit.Recycle("arc", first);
            var second = kit.Play("arc", Vector3.one, Quaternion.identity);

            Assert.AreSame(first, second, "pooling must reuse the instance — zero runtime allocations");

            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(root.gameObject);
        }
    }
}
