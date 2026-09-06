using NUnit.Framework;
using UnityEngine;
using ZombieWar.Threat;

namespace ZombieWar.Tests
{
    /// <summary>
    /// M7.3d — the threat model that replaces designed waves.
    ///
    /// The formula is pure and static precisely so it can be asserted without a scene: the tier a
    /// player is experiencing must always be explainable from four visible inputs.
    /// </summary>
    public class ThreatDirectorTests
    {
        const float Band = 90f, Step = 75f;
        const int TimeCap = 1, Cap = 3;

        static int Tier(int objectives, float distance, float seconds) =>
            ThreatDirector.ComputeTier(objectives, distance, seconds, Band, Step, TimeCap, Cap);

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
        public void TimePressureIsCapped_ALosingPlayerIsNotDoomedByTheClock()
        {
            // The cap is the point: survive forever and the clock alone must never push you to tier 3.
            Assert.AreEqual(0, Tier(0, 0f, 10f));
            Assert.AreEqual(1, Tier(0, 0f, 80f));
            Assert.AreEqual(1, Tier(0, 0f, 6000f),
                "time pressure must stop climbing — finishing is encouraged, not made impossible");
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

            // There is deliberately no health multiplier anywhere in this component.
            string src = System.IO.File.ReadAllText(
                Application.dataPath + "/_Project/Scripts/Runtime/Gameplay/Threat/ThreatDirector.cs");
            Assert.IsFalse(src.Contains("maxHealth") || src.Contains("healthMultiplier"),
                "composition before stats — the threat model must not inflate HP");

            Object.DestroyImmediate(go);
        }

        [Test]
        public void ShipsDisabledSoWavesKeepDrivingTheGame()
        {
            var go = new GameObject("threat3");
            var d = go.AddComponent<ThreatDirector>();
            Assert.IsFalse(d.DrivingSpawning,
                "it must ship OFF: a half-migrated spawner that spawns nothing is worse than waves");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ObjectiveProgressIsRunScopedAndResets()
        {
            RunState.Begin("test");
            ThreatDirector.ReportObjectiveCompleted();
            ThreatDirector.ReportObjectiveCompleted();
            Assert.AreEqual(2, ThreatDirector.ObjectiveProgress);

            RunState.Abandon();
            RunState.Begin("test");

            Assert.AreEqual(0, ThreatDirector.ObjectiveProgress,
                "threat earned in the last run must not carry into the next one");
        }
    }
}
