using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ZombieWar.Tests
{
    /// <summary>
    /// M5.1.3 - the separation contract that stops a packed crowd from hopping.
    ///
    /// The defect a human playtest saw: <c>ComputeSeparation</c> returns a magnitude-weighted push,
    /// but the motor NORMALISED it, so the faintest imbalance between neighbours became a full-speed
    /// movement command, and the sign flipping around zero made whole clusters surge back and forth
    /// in step. These tests hold the corrected shape: magnitude is preserved, a settled crowd
    /// produces no correction, a real overlap still separates, and a stopped attacker can only creep.
    ///
    /// (This file replaces an earlier one at CrowdSeparationTests.cs which Unity's asset database
    /// silently refused to include in the test assembly - it never appeared in that assembly's
    /// source list, so none of these assertions had ever actually run.)
    /// </summary>
    public class CrowdSeparationContractTests
    {
        private readonly List<GameObject> _spawned = new();

        [SetUp]
        public void SetUp() => PlanarSteeringWorld.Clear();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            PlanarSteeringWorld.Clear();
        }

        private PlanarEnemyMotor MakeMotor(Vector3 position, float speed = 3f)
        {
            var go = new GameObject("Motor");
            go.transform.position = position;
            _spawned.Add(go);
            var motor = go.AddComponent<PlanarEnemyMotor>();
            motor.ConfigureFromData(speed);
            return motor;
        }

        private static IEnumerator Settle(int frames = 90)
        {
            for (int i = 0; i < frames; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator SettledNeighbours_ProduceNoSeparationCorrection()
        {
            var a = MakeMotor(new Vector3(-1.4f, 0f, 0f));
            MakeMotor(new Vector3(1.4f, 0f, 0f));
            a.IsStopped = true;

            yield return Settle();

            Assert.Less(a.SmoothedSeparation.magnitude, 0.05f,
                "a settled crowd is still generating separation corrections");
            Assert.Less(a.CurrentSpeed, 0.05f, "a settled stopped enemy is still drifting");
        }

        [UnityTest]
        public IEnumerator TinyImbalance_DoesNotBecomeFullSpeedMovement()
        {
            var a = MakeMotor(Vector3.zero, speed: 3f);
            MakeMotor(new Vector3(0.98f, 0f, 0f));
            MakeMotor(new Vector3(-1.02f, 0f, 0f));
            a.IsStopped = true;

            yield return Settle();

            Assert.Less(a.CurrentSpeed, 3f * 0.25f + 0.05f,
                "a near-balanced stopped enemy moved at more than the stopped separation cap");
        }

        [UnityTest]
        public IEnumerator StoppedAttacker_MovementStaysUnderTheCap()
        {
            const float speed = 3f;
            var a = MakeMotor(Vector3.zero, speed);
            for (int i = 0; i < 5; i++)
                MakeMotor(new Vector3(Mathf.Cos(i * 1.2f), 0f, Mathf.Sin(i * 1.2f)) * 0.6f, speed);
            a.IsStopped = true;

            float maxSpeed = 0f;
            for (int i = 0; i < 120; i++)
            {
                yield return null;
                maxSpeed = Mathf.Max(maxSpeed, a.CurrentSpeed);
            }

            Assert.LessOrEqual(maxSpeed, speed * 0.25f + 0.1f,
                "a stopped attacker moved faster than the authored stopped-separation cap");
        }

        [UnityTest]
        public IEnumerator RealOverlap_StillSeparates()
        {
            var a = MakeMotor(new Vector3(-0.05f, 0f, 0f));
            var b = MakeMotor(new Vector3(0.05f, 0f, 0f));
            a.IsStopped = true; b.IsStopped = true;
            float before = Vector3.Distance(a.transform.position, b.transform.position);

            yield return Settle(180);

            float after = Vector3.Distance(a.transform.position, b.transform.position);
            Assert.Greater(after, before + 0.15f,
                "two overlapping enemies never pushed apart - separation was over-damped");
        }

        [UnityTest]
        public IEnumerator ChasingEnemy_StillReachesItsTarget()
        {
            var motor = MakeMotor(new Vector3(-8f, 0f, 0f), speed: 4f);
            motor.SetDestination(Vector3.zero);

            for (int i = 0; i < 240 && motor.transform.position.x < -0.6f; i++) yield return null;

            Assert.Greater(motor.transform.position.x, -0.8f,
                "separation smoothing slowed pursuit materially");
        }

        [UnityTest]
        public IEnumerator SmoothedSeparation_DoesNotAlternateAtFullScale()
        {
            var a = MakeMotor(Vector3.zero);
            var b = MakeMotor(new Vector3(0.8f, 0f, 0f));
            var c = MakeMotor(new Vector3(-0.82f, 0f, 0f));
            a.IsStopped = true; b.IsStopped = true; c.IsStopped = true;

            yield return Settle(30);

            int reversals = 0;
            float lastSign = 0f;
            for (int i = 0; i < 120; i++)
            {
                yield return null;
                float x = a.SmoothedSeparation.x;
                if (Mathf.Abs(x) < 0.02f) continue;
                float sign = Mathf.Sign(x);
                if (lastSign != 0f && sign != lastSign) reversals++;
                lastSign = sign;
            }

            Assert.LessOrEqual(reversals, 6,
                $"smoothed separation flipped direction {reversals} times - the crowd will pulse");
        }

        [UnityTest]
        public IEnumerator ResetMotion_ClearsSmoothedSeparation()
        {
            var a = MakeMotor(Vector3.zero);
            MakeMotor(new Vector3(0.3f, 0f, 0f));
            a.IsStopped = true;
            yield return Settle(30);

            a.ResetMotion();

            Assert.AreEqual(Vector3.zero, a.SmoothedSeparation,
                "a pooled instance would inherit the previous life's separation push");
        }

        /// <summary>
        /// Locks the CORRECTED reversal metric (M5.1.3 closeout).
        ///
        /// The metric that once reported a 15 FPS failure summed reversals across the whole crowd,
        /// divided by wall-clock seconds only, counted single-sample sign flips with no dwell, and
        /// keyed per-enemy state by instance id across pooled reuse. Re-measured properly - per
        /// enemy-second, with a deadband AND a time-based dwell - a controlled 24-enemy production
        /// crowd ran at 0.379 (60 FPS), 0.598 (30 FPS) and 0.714 (15 FPS) qualified reversals per
        /// enemy-second. This test holds the production motor under that definition with headroom.
        /// </summary>
        [UnityTest]
        public IEnumerator QualifiedReversalRate_StaysWithinContract()
        {
            const float deadband = 0.25f;      // lateral speed below this is not a direction
            const float dwell = 0.15f;         // a new sign must survive this long to count
            const int count = 12;

            var motors = new List<PlanarEnemyMotor>();
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                motors.Add(MakeMotor(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 2.5f, speed: 3f));
            }

            var target = new GameObject("ReversalTarget").transform;
            _spawned.Add(target.gameObject);
            target.position = Vector3.zero;

            var committed = new float[count];
            var candidate = new float[count];
            var candidateTime = new float[count];
            int qualified = 0;
            float enemySeconds = 0f;

            float end = Time.time + 5f;
            while (Time.time < end)
            {
                foreach (var m in motors) m.SetDestination(target.position);
                yield return null;
                float dt = Time.deltaTime;
                if (dt <= 0f) continue;

                for (int i = 0; i < motors.Count; i++)
                {
                    enemySeconds += dt;
                    Vector3 toTarget = target.position - motors[i].transform.position;
                    toTarget.y = 0f;
                    if (toTarget.sqrMagnitude < 0.0001f) continue;

                    Vector3 right = Vector3.Cross(Vector3.up, toTarget.normalized);
                    float lateral = Vector3.Dot(motors[i].Velocity, right);

                    if (Mathf.Abs(lateral) <= deadband) { candidate[i] = 0f; candidateTime[i] = 0f; continue; }

                    float sign = Mathf.Sign(lateral);
                    if (committed[i] == 0f) { committed[i] = sign; continue; }
                    if (Mathf.Approximately(sign, committed[i])) { candidate[i] = 0f; candidateTime[i] = 0f; continue; }

                    if (Mathf.Approximately(sign, candidate[i]))
                    {
                        candidateTime[i] += dt;
                        if (candidateTime[i] >= dwell)
                        {
                            qualified++;
                            committed[i] = sign;
                            candidate[i] = 0f; candidateTime[i] = 0f;
                        }
                    }
                    else { candidate[i] = sign; candidateTime[i] = dt; }
                }
            }

            float rate = enemySeconds > 0.01f ? qualified / enemySeconds : 0f;
            Assert.Less(rate, 1.5f,
                $"qualified reversal rate {rate:F3}/enemy-second (raw {qualified} reversals over " +
                $"{enemySeconds:F1} enemy-seconds) exceeded the documented contract");
        }

        [UnityTest]
        public IEnumerator CrowdSettlesIntoAStableRing_WithoutStacking()
        {
            const int count = 16;
            var motors = new List<PlanarEnemyMotor>();
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                motors.Add(MakeMotor(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 1.1f));
            }
            foreach (var m in motors) m.IsStopped = true;

            yield return Settle(180);

            float minGap = float.MaxValue;
            for (int i = 0; i < motors.Count; i++)
                for (int j = i + 1; j < motors.Count; j++)
                    minGap = Mathf.Min(minGap, Vector3.Distance(
                        motors[i].transform.position, motors[j].transform.position));

            Assert.Greater(minGap, 0.15f, "enemies stacked on top of each other");
            foreach (var m in motors)
                Assert.Less(m.CurrentSpeed, 0.6f, "the settled ring is still churning");
        }
    }
}
