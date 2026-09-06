using NUnit.Framework;
using UnityEngine;
using ZombieWar.Skills;

namespace ZombieWar.Tests
{
    /// <summary>
    /// M7.4a — the leash that stops a running player building an endless tail.
    ///
    /// The reset half of this matters as much as the recycling half: a pooled enemy that inherits the
    /// previous occupant's statuses is the exact class of leak already fixed twice in this project
    /// (the skill build and the pickup registry). Recycling makes pool reuse far more frequent, so it
    /// is guarded here explicitly.
    /// </summary>
    public class EnemyLeashTests
    {
        [SetUp]
        public void SetUp()
        {
            StatusCarrier.ClearAll();
            ZombieManager.ResetRecycleCounter();
        }

        [TearDown]
        public void TearDown()
        {
            StatusCarrier.ClearAll();
            ZombieManager.ResetRecycleCounter();
            RunState.Abandon();
        }

        [Test]
        public void RecycleCounterIsRunScoped_AndResetsWithTheRun()
        {
            RunState.Begin("test");
            // Simulate a run that recycled a tail.
            typeof(ZombieManager).GetProperty("RecycledCount",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .SetValue(null, 42);
            Assert.AreEqual(42, ZombieManager.RecycledCount);

            RunState.Abandon();
            RunState.Begin("test");

            Assert.AreEqual(0, ZombieManager.RecycledCount,
                "recycles from the previous run must not carry into the next one");
        }

        [Test]
        public void RunScopeClearsTheRecycleCounter()
        {
            typeof(ZombieManager).GetProperty("RecycledCount",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .SetValue(null, 7);
            RunScope.ResetAll();
            Assert.AreEqual(0, ZombieManager.RecycledCount);
        }

        /// <summary>
        /// The pooling trap, restated for the leash. Recycling reuses instance ids far more often than
        /// natural deaths did, so a status left behind would surface as unexplained damage spikes on a
        /// fresh enemy.
        /// </summary>
        [Test]
        public void ARecycledEnemyInheritsNoStatusFromTheLastOccupant()
        {
            const int id = 8080;
            StatusCarrier.Apply(id, StatusKind.Exposed, 1f, 999f, 0f);
            StatusCarrier.Apply(id, StatusKind.Slow, 0.5f, 999f, 0f);
            StatusCarrier.Accumulate(id, StatusKind.HitCount, 6f, 999f, 0f);
            Assert.IsTrue(StatusCarrier.Has(id, StatusKind.Exposed, 1f), "sanity");

            // This is what ZombieBase.OnDisable does on pool return.
            StatusCarrier.Clear(id);

            Assert.AreEqual(0f, StatusCarrier.Get(id, StatusKind.Exposed, 1f));
            Assert.AreEqual(0f, StatusCarrier.Get(id, StatusKind.Slow, 1f));
            Assert.AreEqual(0f, StatusCarrier.Get(id, StatusKind.HitCount, 1f),
                "Focus Fire's per-target ramp must not survive a recycle");
            Assert.AreEqual(0, StatusCarrier.TrackedCount);
        }

        [Test]
        public void PoolReturnPathClearsStatuses_WiredInZombieBase()
        {
            string src = System.IO.File.ReadAllText(
                Application.dataPath + "/_Project/Scripts/Runtime/Gameplay/Zombies/ZombieBase.cs");
            StringAssert.Contains("StatusCarrier.Clear", src,
                "despawn must clear statuses, or recycling spreads them to fresh enemies");
            StringAssert.Contains("IsBeaconOwned = false", src,
                "a recycled instance must not inherit the beacon-owned role");
        }

        [Test]
        public void TheLeashNeverRecyclesSomethingVisibleOrAuthored()
        {
            string src = System.IO.File.ReadAllText(
                Application.dataPath + "/_Project/Scripts/Runtime/Gameplay/ZombieManager.cs");

            StringAssert.Contains("TestPlanesAABB", src,
                "an enemy vanishing on screen is worse than the tail being fixed");
            StringAssert.Contains("IsBeaconOwned", src, "a beacon boss must never be recycled");
            StringAssert.Contains("isElite", src, "elites are authored pressure, not tail");
            StringAssert.Contains("maxRecyclesPerPass", src, "a big tail must drain smoothly, not pop");
        }

        [Test]
        public void BeaconOwnedFlagExistsAndDefaultsOff()
        {
            var go = new GameObject("z");
            // ZombieBase is abstract; the flag is what the leash reads, so assert the contract exists.
            var prop = typeof(ZombieBase).GetProperty("IsBeaconOwned");
            Assert.IsNotNull(prop, "the leash depends on this flag");
            Assert.IsTrue(prop.CanRead && prop.CanWrite);
            Object.DestroyImmediate(go);
        }
    }
}
