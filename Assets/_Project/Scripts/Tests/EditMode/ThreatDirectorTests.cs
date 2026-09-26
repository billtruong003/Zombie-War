using NUnit.Framework;
using UnityEngine;
using ZombieWar.Threat;

namespace ZombieWar.Tests
{
    /// <summary>
    /// The endless threat model. The formula is pure and static precisely so it can be asserted
    /// without a scene: the tier a player is experiencing must always be explainable from three
    /// visible inputs.
    /// </summary>
    public class ThreatDirectorTests
    {
        const float Band = 90f, Step = 90f;
        const int Cap = 30;

        static int Tier(int objectives, float distance, float seconds) =>
            ThreatDirector.ComputeTier(objectives, distance, seconds, Band, Step, Cap);

        [SetUp] public void SetUp() => ThreatDirector.ResetRunState();
        [TearDown] public void TearDown() { ThreatDirector.ResetRunState(); RunState.Abandon(); }

        [Test]
        public void AFreshRunStartsAtTierZero()
        {
            Assert.AreEqual(0, Tier(0, 0f, 0f), "the opening minute must be walkers and runners only");
        }

        [Test]
        public void TravellingOutwardRaisesThreat()
        {
            Assert.AreEqual(0, Tier(0, 40f, 0f));
            Assert.AreEqual(1, Tier(0, 95f, 0f), "crossing a distance band adds pressure");
            Assert.AreEqual(2, Tier(0, 185f, 0f));
        }

        [Test]
        public void CompletingObjectivesRaisesThreat_ProgressCostsSafety()
        {
            Assert.AreEqual(0, Tier(0, 0f, 0f));
            Assert.AreEqual(1, Tier(1, 0f, 0f));
            Assert.AreEqual(2, Tier(2, 0f, 0f));
        }

        [Test]
        public void TimePressureNeverStops_StandingStillCannotLastForever()
        {
            Assert.AreEqual(0, Tier(0, 0f, 10f));
            Assert.AreEqual(1, Tier(0, 0f, 95f));
            Assert.AreEqual(6, Tier(0, 0f, 600f),
                "an endless run must end by attrition: the clock keeps raising pressure");
        }

        [Test]
        public void StatsOnlyScaleLate_AndGently()
        {
            // More enemies before stronger enemies: nothing gains health before the start tier.
            Assert.AreEqual(1f, ThreatDirector.StatMultiplierFor(0, 6, 0.04f), 1e-5f);
            Assert.AreEqual(1f, ThreatDirector.StatMultiplierFor(5, 6, 0.04f), 1e-5f);
            Assert.AreEqual(1.04f, ThreatDirector.StatMultiplierFor(6, 6, 0.04f), 1e-5f);
            Assert.AreEqual(1.20f, ThreatDirector.StatMultiplierFor(10, 6, 0.04f), 1e-5f);
        }

        [Test]
        public void FarEnemiesPursueFaster_NearOnesKeepTheirSpeed()
        {
            Assert.AreEqual(1f, ZombieManager.PursuitMultiplierAt(5f, 10f, 25f, 2.2f), 1e-5f, "on screen: authored speed");
            Assert.AreEqual(1f, ZombieManager.PursuitMultiplierAt(10f, 10f, 25f, 2.2f), 1e-5f);
            Assert.AreEqual(1.6f, ZombieManager.PursuitMultiplierAt(17.5f, 10f, 25f, 2.2f), 1e-5f);
            Assert.AreEqual(2.2f, ZombieManager.PursuitMultiplierAt(60f, 10f, 25f, 2.2f), 1e-5f);
        }

        [Test]
        public void TheTailIsFarEnemiesBehindAMovingPlayer()
        {
            var north = new Vector3(0f, 0f, 5f);   // running north at 5 m/s
            Assert.IsTrue(ZombieManager.IsInTail(new Vector3(0f, 0f, -20f), north, 16f, 1.5f, -0.3f), "far behind");
            Assert.IsFalse(ZombieManager.IsInTail(new Vector3(0f, 0f, -10f), north, 16f, 1.5f, -0.3f), "behind but close");
            Assert.IsFalse(ZombieManager.IsInTail(new Vector3(0f, 0f, 20f), north, 16f, 1.5f, -0.3f), "ahead is never tail");
            Assert.IsFalse(ZombieManager.IsInTail(new Vector3(20f, 0f, 0f), north, 16f, 1.5f, -0.3f), "beside is not tail");
            Assert.IsFalse(ZombieManager.IsInTail(new Vector3(0f, 0f, -20f), Vector3.zero, 16f, 1.5f, -0.3f),
                "a standing player has no tail");
        }

        [Test]
        public void AnEmptyCrowdRefillsFaster_AFullOneAtNormalCadence()
        {
            Assert.AreEqual(0.2f, ThreatDirector.CatchUpScale(0, 40, 0.2f), 1e-5f);
            Assert.AreEqual(0.6f, ThreatDirector.CatchUpScale(20, 40, 0.2f), 1e-5f);
            Assert.AreEqual(1f, ThreatDirector.CatchUpScale(40, 40, 0.2f), 1e-5f);
            Assert.AreEqual(1f, ThreatDirector.CatchUpScale(90, 40, 0.2f), 1e-5f, "over target never slows below 1");
        }

        [Test]
        public void TheHudCanAnnounceASurgeBeforeItLands()
        {
            // opening 25 s, a 10 s surge every 75 s -> first surge 100..110
            Assert.AreEqual(3f, ThreatDirector.SecondsUntilSurge(97f, 25f, 75f, 10f), 1e-4f);
            Assert.AreEqual(0f, ThreatDirector.SecondsUntilSurge(104f, 25f, 75f, 10f), 1e-4f, "running");
            Assert.AreEqual(6f, ThreatDirector.SurgeSecondsLeft(104f, 25f, 75f, 10f), 1e-4f);
            Assert.AreEqual(0f, ThreatDirector.SurgeSecondsLeft(120f, 25f, 75f, 10f), 1e-4f, "none running");
            Assert.AreEqual(55f, ThreatDirector.SecondsUntilSurge(120f, 25f, 75f, 10f), 1e-4f, "next one at 175");
            Assert.IsTrue(float.IsPositiveInfinity(ThreatDirector.SecondsUntilSurge(50f, 25f, 0f, 10f)));
        }

        [Test]
        public void SurgesRunOnAFixedClock_AfterTheOpening()
        {
            // opening 25 s, a 10 s surge every 75 s
            Assert.IsFalse(ThreatDirector.IsSurgeAt(0f, 25f, 75f, 10f), "never during the opening");
            Assert.IsFalse(ThreatDirector.IsSurgeAt(99f, 25f, 75f, 10f), "not before the first interval");
            Assert.IsTrue(ThreatDirector.IsSurgeAt(100f, 25f, 75f, 10f), "first surge at opening + 75 s");
            Assert.IsTrue(ThreatDirector.IsSurgeAt(109.9f, 25f, 75f, 10f));
            Assert.IsFalse(ThreatDirector.IsSurgeAt(110f, 25f, 75f, 10f), "a surge ends");
            Assert.IsTrue(ThreatDirector.IsSurgeAt(175f, 25f, 75f, 10f), "and comes back");
            Assert.IsFalse(ThreatDirector.IsSurgeAt(500f, 25f, 0f, 10f), "0 disables surges");
        }

        [Test]
        public void TheOpeningEasesIn_ThenReachesTheFullCrowd()
        {
            Assert.AreEqual(4, ThreatDirector.OpeningAliveTarget(18, 0f, 60f, 0.2f),
                "the first seconds are a small crowd, not a wall");
            Assert.LessOrEqual(ThreatDirector.OpeningAliveTarget(18, 20f, 60f, 0.2f), 6,
                "a third of the way in, the crowd is still small (ease-in)");
            Assert.AreEqual(18, ThreatDirector.OpeningAliveTarget(18, 60f, 60f, 0.2f));
            Assert.AreEqual(18, ThreatDirector.OpeningAliveTarget(18, 300f, 60f, 0.2f));

            Assert.AreEqual(2.5f, ThreatDirector.OpeningIntervalScale(0f, 60f, 2.5f), 1e-5f,
                "arrivals start well below full cadence");
            Assert.AreEqual(1f, ThreatDirector.OpeningIntervalScale(60f, 60f, 2.5f), 1e-5f);
        }

        [Test]
        public void TierIsClampedToItsCeiling()
        {
            Assert.AreEqual(Cap, Tier(99, 9999f, 99999f));
        }

        [Test]
        public void TierNeverGoesNegative()
        {
            Assert.AreEqual(0, Tier(0, -50f, -20f));
        }

        [Test]
        public void CompositionWidensWithTier_ItDoesNotSwapTheCrowdOut()
        {
            var go = new GameObject("threat");
            var d = go.AddComponent<ThreatDirector>();

            var so = new UnityEditor.SerializedObject(d);
            System.Action<string, int> fill = (field, n) =>
            {
                var arr = so.FindProperty(field);
                arr.arraySize = n;
                for (int i = 0; i < n; i++)
                    arr.GetArrayElementAtIndex(i).objectReferenceValue = ScriptableObject.CreateInstance<ZombieData>();
            };
            fill("tier0Basic", 2); fill("tier1Specialist", 1); fill("tier2Mixed", 2); fill("tier3Heavy", 2);
            so.ApplyModifiedPropertiesWithoutUndo();

            // Each tier ADDS a kind — the crowd learned at tier 0 is still present at tier 3.
            Assert.AreEqual(2, d.RosterSizeFor(0));
            Assert.AreEqual(3, d.RosterSizeFor(1));
            Assert.AreEqual(5, d.RosterSizeFor(2));
            Assert.AreEqual(7, d.RosterSizeFor(3));

            Object.DestroyImmediate(go);
        }

        [Test]
        public void PressureIsExpressedAsCadenceAndCount_NotHealth()
        {
            var go = new GameObject("threat2");
            var d = go.AddComponent<ThreatDirector>();

            Assert.Less(d.SpawnIntervalFor(3), d.SpawnIntervalFor(0), "higher tier spawns faster");
            Assert.Greater(d.AliveTargetFor(3), d.AliveTargetFor(0), "higher tier allows a bigger crowd");
            Assert.GreaterOrEqual(d.SpawnIntervalFor(99), 0.08f, "the cadence has a floor");
            Assert.LessOrEqual(d.AliveTargetFor(99), 160, "the crowd has a ceiling for the frame budget");
            Assert.GreaterOrEqual(d.AliveTargetFor(0), 40, "tier 0 is already a crowd, not a trickle");
            Assert.Greater(d.SurgeAliveTargetFor(0), d.AliveTargetFor(0), "a surge is bigger than the stream");
            Assert.LessOrEqual(d.SurgeAliveTargetFor(99), 200, "even a surge is bounded");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ObjectiveProgressIsRunScopedAndResets()
        {
            RunState.Begin();
            ThreatDirector.ReportObjectiveCompleted();
            ThreatDirector.ReportObjectiveCompleted();
            Assert.AreEqual(2, ThreatDirector.ObjectiveProgress);

            RunState.Abandon();
            RunState.Begin();

            Assert.AreEqual(0, ThreatDirector.ObjectiveProgress,
                "threat earned in the last run must not carry into the next one");
        }
    }
}
