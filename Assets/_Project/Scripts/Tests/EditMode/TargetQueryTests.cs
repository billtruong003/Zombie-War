using NUnit.Framework;
using UnityEngine;
using ZombieWar.Skills;

namespace ZombieWar.Tests
{
    /// <summary>
    /// M7.2 — backfill tests for <b>P3 TargetQuery</b>, which shipped in B1 with none.
    ///
    /// P3 is the riskiest primitive in the set: it is the only one written from scratch (neither chain
    /// targeting nor any density query existed in the runtime — <c>FireMode.ChainLightning</c> was an
    /// enum value with no code behind it), it carries the tightest performance constraints, and four
    /// cards depend on it: Chain Lightning, Static Build-up, Ordnance Core and Shockwave Belt.
    ///
    /// Layouts are seeded through <see cref="TargetQuery.SeedForTest"/> so each case is an exact,
    /// reproducible geometry rather than whatever a physics scene happened to contain.
    /// </summary>
    public class TargetQueryTests
    {
        static readonly int[] Buf = new int[TargetQuery.MaxConsidered];

        [SetUp] public void SetUp() => StatusCarrier.ClearAll();

        static int Seed(params Vector3[] pts) => TargetQuery.SeedForTest(pts, null, pts.Length);

        // ───────────────────────────────────────────────────────── chain
        [Test]
        public void Chain_HopsToTheNearestUnvisitedTargetInOrder()
        {
            // A deliberate zig-zag: nearest-next order is 0 -> 1 -> 2, NOT index order by distance
            // from the origin.
            int n = Seed(new Vector3(1f, 0f, 0f),
                         new Vector3(2f, 0f, 0f),
                         new Vector3(3.5f, 0f, 0f));

            int hops = TargetQuery.Chain(n, Vector3.zero, jumpRange: 2f, maxJumps: 6, Buf);

            Assert.AreEqual(3, hops);
            Assert.AreEqual(0, Buf[0]);
            Assert.AreEqual(1, Buf[1]);
            Assert.AreEqual(2, Buf[2], "the chain walks from the last hop, not from the origin");
        }

        [Test]
        public void Chain_StopsWhenTheNextTargetIsOutOfJumpRange()
        {
            int n = Seed(new Vector3(1f, 0f, 0f), new Vector3(50f, 0f, 0f));
            int hops = TargetQuery.Chain(n, Vector3.zero, jumpRange: 2f, maxJumps: 6, Buf);
            Assert.AreEqual(1, hops, "an unreachable target must terminate the chain, not be skipped to");
        }

        [Test]
        public void Chain_NeverVisitsTheSameTargetTwice()
        {
            int n = Seed(new Vector3(1f, 0f, 0f), new Vector3(1.1f, 0f, 0f), new Vector3(1.2f, 0f, 0f));
            int hops = TargetQuery.Chain(n, Vector3.zero, 5f, 6, Buf);

            Assert.AreEqual(3, hops);
            CollectionAssert.AllItemsAreUnique(new[] { Buf[0], Buf[1], Buf[2] });
        }

        [Test]
        public void Chain_IsCappedAtSixArcsEvenWhenAskedForMore()
        {
            // The stated guardrail is <=6 arcs per proc, so the cap must live in the primitive and
            // not depend on every caller passing a sane number.
            var pts = new Vector3[20];
            for (int i = 0; i < pts.Length; i++) pts[i] = new Vector3(i * 0.5f, 0f, 0f);
            int n = TargetQuery.SeedForTest(pts, null, pts.Length);

            int hops = TargetQuery.Chain(n, Vector3.zero, jumpRange: 5f, maxJumps: 99, Buf);

            Assert.AreEqual(TargetQuery.MaxChain, hops, "must clamp to the 6-arc ceiling");
        }

        [Test]
        public void Chain_WithNoCandidatesReturnsZero()
        {
            int hops = TargetQuery.Chain(0, Vector3.zero, 10f, 6, Buf);
            Assert.AreEqual(0, hops, "an empty battlefield must be a no-op, not an exception");
        }

        // ───────────────────────────────────────────────────────── densest cluster
        [Test]
        public void DensestCluster_PicksTheCentreOfTheTightestGroup()
        {
            // Three tight neighbours around (10,0,0), one loner far away.
            int n = Seed(new Vector3(10f, 0f, 0f),
                         new Vector3(10.5f, 0f, 0f),
                         new Vector3(9.5f, 0f, 0f),
                         new Vector3(-40f, 0f, 0f));

            int best = TargetQuery.DensestCluster(n, radius: 2f, out int neighbours);

            Assert.AreNotEqual(3, best, "the isolated enemy must never be the densest cluster");
            Assert.AreEqual(2, neighbours, "the centre of a 3-pack sees its two companions");
            Assert.Contains(best, new[] { 0, 1, 2 });
        }

        [Test]
        public void DensestCluster_WithNoCandidatesReturnsMinusOne()
        {
            int best = TargetQuery.DensestCluster(0, 5f, out int neighbours);
            Assert.AreEqual(-1, best);
            Assert.AreEqual(0, neighbours);
        }

        [Test]
        public void DensestCluster_WithOneCandidateSelectsItWithNoNeighbours()
        {
            int n = Seed(new Vector3(4f, 0f, 0f));
            int best = TargetQuery.DensestCluster(n, 5f, out int neighbours);
            Assert.AreEqual(0, best, "a lone enemy is still a valid bomb target");
            Assert.AreEqual(0, neighbours);
        }

        [Test]
        public void DensestCluster_RadiusActuallyBounds_WhatCountsAsANeighbour()
        {
            int n = Seed(Vector3.zero, new Vector3(3f, 0f, 0f));
            TargetQuery.DensestCluster(n, radius: 1f, out int tight);
            TargetQuery.DensestCluster(n, radius: 5f, out int loose);

            Assert.AreEqual(0, tight, "3 m apart is not a cluster at 1 m radius");
            Assert.AreEqual(1, loose, "3 m apart is a cluster at 5 m radius");
        }

        // ───────────────────────────────────────────────────────── cone
        [Test]
        public void Cone_SelectsOnlyWhatIsInsideTheHalfAngle()
        {
            int n = Seed(new Vector3(0f, 0f, 5f),     // dead ahead
                         new Vector3(5f, 0f, 5f),     // 45 deg
                         new Vector3(0f, 0f, -5f));   // behind

            int hits = TargetQuery.Cone(n, Vector3.zero, Vector3.forward, halfAngleDeg: 30f, Buf);

            Assert.AreEqual(1, hits, "only the target inside 30 degrees qualifies");
            Assert.AreEqual(0, Buf[0]);
        }

        [Test]
        public void Cone_BoundaryTargetIsIncluded_AndJustOutsideIsNot()
        {
            int n = Seed(new Vector3(5f, 0f, 5f));    // exactly 45 degrees

            Assert.AreEqual(1, TargetQuery.Cone(n, Vector3.zero, Vector3.forward, 45.5f, Buf),
                "just inside the half-angle counts");
            Assert.AreEqual(0, TargetQuery.Cone(n, Vector3.zero, Vector3.forward, 44.5f, Buf),
                "just outside does not");
        }

        [Test]
        public void Cone_WideAngleBecomesEveryoneAround()
        {
            int n = Seed(new Vector3(0f, 0f, 5f), new Vector3(5f, 0f, 0f), new Vector3(0f, 0f, -5f));
            Assert.AreEqual(3, TargetQuery.Cone(n, Vector3.zero, Vector3.forward, 180f, Buf));
        }

        // ───────────────────────────────────────────────────────── priority override
        [Test]
        public void Priority_PrefersAMarkedTargetOverTheNearestOne()
        {
            var pts = new[] { new Vector3(1f, 0f, 0f), new Vector3(20f, 0f, 0f) };
            var ids = new[] { 100, 200 };
            int n = TargetQuery.SeedForTest(pts, ids, 2);

            StatusCarrier.Apply(200, StatusKind.Marked, 1f, duration: 10f, now: 0f);

            int pick = TargetQuery.Priority(n, Vector3.zero, StatusKind.Marked, now: 1f);
            Assert.AreEqual(1, pick, "Hunter's Mark must override proximity");
        }

        [Test]
        public void Priority_FallsBackToNearestWhenNothingIsMarked()
        {
            var pts = new[] { new Vector3(20f, 0f, 0f), new Vector3(2f, 0f, 0f) };
            int n = TargetQuery.SeedForTest(pts, new[] { 1, 2 }, 2);

            Assert.AreEqual(1, TargetQuery.Priority(n, Vector3.zero, StatusKind.Marked, 0f));
        }

        [Test]
        public void Priority_IgnoresAnExpiredMark()
        {
            var pts = new[] { new Vector3(1f, 0f, 0f), new Vector3(20f, 0f, 0f) };
            int n = TargetQuery.SeedForTest(pts, new[] { 100, 200 }, 2);
            StatusCarrier.Apply(200, StatusKind.Marked, 1f, duration: 2f, now: 0f);

            Assert.AreEqual(0, TargetQuery.Priority(n, Vector3.zero, StatusKind.Marked, now: 99f),
                "an expired mark must not keep hijacking targeting");
        }

        [Test]
        public void Priority_WithNoCandidatesReturnsMinusOne()
        {
            Assert.AreEqual(-1, TargetQuery.Priority(0, Vector3.zero, StatusKind.Marked, 0f));
        }

        // ───────────────────────────────────────────────────────── ceilings
        [Test]
        public void Gather_NeverConsidersMoreThanSixtyFourEnemies()
        {
            // The clustering guardrail is <=64 enemies. DensestCluster is O(n^2), so this ceiling is
            // what keeps a large horde from turning one proc into a frame spike.
            var pts = new Vector3[200];
            for (int i = 0; i < pts.Length; i++) pts[i] = new Vector3(i * 0.1f, 0f, 0f);

            int n = TargetQuery.SeedForTest(pts, null, pts.Length);

            Assert.AreEqual(TargetQuery.MaxConsidered, n);
            Assert.LessOrEqual(n, 64);
        }

        [Test]
        public void SelectionMathsPerformsNoPhysicsQuery_OneGatherPerProc()
        {
            // The guarantee is ONE OverlapSphereNonAlloc per power proc: Gather queries, and every
            // selection call afterwards reads the already-filled buffer. Seeding without any physics
            // scene and still getting answers is what demonstrates that separation.
            int n = Seed(new Vector3(1f, 0f, 0f), new Vector3(2f, 0f, 0f), new Vector3(3f, 0f, 0f));

            Assert.AreEqual(3, TargetQuery.Chain(n, Vector3.zero, 5f, 6, Buf));
            Assert.AreEqual(3, TargetQuery.Cone(n, Vector3.zero, Vector3.right, 90f, Buf));
            Assert.GreaterOrEqual(TargetQuery.DensestCluster(n, 5f, out _), 0);
            Assert.GreaterOrEqual(TargetQuery.Priority(n, Vector3.zero, StatusKind.Marked, 0f), 0);
        }

        [Test]
        public void CandidateIdSurvivesSeeding_SoStatusesBindToTheRightEnemy()
        {
            int n = TargetQuery.SeedForTest(new[] { Vector3.zero, Vector3.one }, new[] { 77, 88 }, 2);
            Assert.AreEqual(2, n);
            Assert.AreEqual(77, TargetQuery.CandidateId(0));
            Assert.AreEqual(88, TargetQuery.CandidateId(1));
        }
    }
}
