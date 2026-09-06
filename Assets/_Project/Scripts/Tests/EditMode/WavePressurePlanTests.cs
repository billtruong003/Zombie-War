using NUnit.Framework;
using UnityEngine;
using ZombieWar;

namespace ZombieWar.Tests
{
    /// <summary>
    /// The horde spawn maths. These guard three things a playtest cannot cheaply prove: that a wave
    /// which authors no pressure fields still behaves exactly like the old serial feed (stages 2-5
    /// are balanced against that), that the hard alive cap is never crossed, and that a wave can
    /// always finish rather than wedging against its own ceiling.
    /// </summary>
    public class WavePressurePlanTests
    {
        static WaveData.Wave Horde() => new WaveData.Wave
        {
            maxConcurrent = 70,
            targetAlive = 60,
            visibleFloor = 30,
            reserveFloor = 15,
            initialBurst = 30,
            spawnBatchSize = 15,
            spawnInterval = 0.18f,
            recoverySpawnInterval = 0.08f,
        };

        // A wave as stages 2-5 author them: rate + cap only, every pressure field left at zero.
        static WaveData.Wave Legacy() => new WaveData.Wave
        {
            maxConcurrent = 16,
            spawnInterval = 0.85f,
        };

        [Test]
        public void LegacyWave_SpawnsOneAtATime_UpToItsCap()
        {
            var wave = Legacy();

            Assert.AreEqual(16, wave.TargetAlive, "no target authored -> the cap IS the target");
            Assert.AreEqual(1, wave.BatchSize);
            Assert.AreEqual(1, wave.InitialBurst);
            Assert.AreEqual(wave.NormalInterval, wave.RecoveryInterval);
            Assert.IsFalse(wave.UsesPressureRecovery);

            Assert.AreEqual(1, WavePressurePlan.BatchSize(wave, false, 0, 40));
            Assert.AreEqual(1, WavePressurePlan.BatchSize(wave, false, 15, 40));
            Assert.AreEqual(0, WavePressurePlan.BatchSize(wave, false, 16, 40), "at cap");
        }

        [Test]
        public void LegacyWave_NeverEntersRecovery_EvenOnAnEmptyScreen()
        {
            var wave = Legacy();
            Assert.IsFalse(WavePressurePlan.Recovering(wave, false, true, 0, 0));
            Assert.IsFalse(WavePressurePlan.Recovering(wave, true, true, 0, 0));
        }

        [Test]
        public void Recovery_StartsWhenScreenThins_AndHoldsUntilReserveRefills()
        {
            var wave = Horde();

            Assert.IsFalse(WavePressurePlan.Recovering(wave, false, true, 30, 0),
                "at the visible floor - nothing to recover");
            Assert.IsTrue(WavePressurePlan.Recovering(wave, false, true, 29, 40),
                "below the visible floor - recover regardless of reserve");

            // Hysteresis: the screen alone refilling is not enough to stop, or it empties again.
            Assert.IsTrue(WavePressurePlan.Recovering(wave, true, true, 30, 14),
                "visible restored but reserve still short");
            Assert.IsFalse(WavePressurePlan.Recovering(wave, true, true, 30, 15),
                "both floors met - stop recovering");
        }

        [Test]
        public void Recovery_IsSuppressedWhileNoPressureSnapshotExists()
        {
            var wave = Horde();
            // No camera/player yet reports visible == 0. Treating that as an empty screen would run
            // the whole wave on recovery settings.
            Assert.IsFalse(WavePressurePlan.Recovering(wave, false, false, 0, 0));
            Assert.IsFalse(WavePressurePlan.Recovering(wave, true, false, 0, 0));
        }

        [Test]
        public void NormalSpawning_StopsAtTargetAlive_RecoveryMayUseTheCapHeadroom()
        {
            var wave = Horde();

            Assert.AreEqual(15, WavePressurePlan.BatchSize(wave, false, 0, 70));
            Assert.AreEqual(5, WavePressurePlan.BatchSize(wave, false, 55, 70), "clamped to the target");
            Assert.AreEqual(0, WavePressurePlan.BatchSize(wave, false, 60, 70), "at the target");

            Assert.AreEqual(10, WavePressurePlan.BatchSize(wave, true, 60, 70),
                "recovery spends the gap between target and hard cap");
            Assert.AreEqual(0, WavePressurePlan.BatchSize(wave, true, 70, 70), "hard cap is absolute");
        }

        [Test]
        public void BatchSize_NeverExceedsWhatIsLeftInTheQueue()
        {
            var wave = Horde();
            Assert.AreEqual(3, WavePressurePlan.BatchSize(wave, false, 0, 3));
            Assert.AreEqual(0, WavePressurePlan.BatchSize(wave, false, 0, 0));
        }

        [Test]
        public void OpeningBurst_IgnoresTheTarget_ButNotTheHardCap()
        {
            var wave = Horde();
            Assert.AreEqual(30, WavePressurePlan.OpeningBurst(wave, 0, 70));
            Assert.AreEqual(4, WavePressurePlan.OpeningBurst(wave, 66, 70), "cap leaves room for 4");
            Assert.AreEqual(0, WavePressurePlan.OpeningBurst(wave, 70, 70));
            Assert.AreEqual(2, WavePressurePlan.OpeningBurst(wave, 0, 2), "short queue");
        }

        [Test]
        public void AuthoredValuesAboveTheCapAreClamped_NotHonoured()
        {
            var wave = new WaveData.Wave { maxConcurrent = 10, targetAlive = 99, initialBurst = 99, spawnBatchSize = 99 };
            Assert.AreEqual(10, wave.TargetAlive);
            Assert.AreEqual(10, wave.InitialBurst);
            Assert.AreEqual(10, wave.BatchSize);
            Assert.AreEqual(10, WavePressurePlan.BatchSize(wave, false, 0, 50));
        }

        [Test]
        public void PeakDemand_IsTheLargestSingleWaveDemand_ClampedToThatWavesCap()
        {
            var pup = ScriptableObject.CreateInstance<ZombieData>();
            var skel = ScriptableObject.CreateInstance<ZombieData>();
            var data = ScriptableObject.CreateInstance<WaveData>();
            data.waves = new[]
            {
                new WaveData.Wave { maxConcurrent = 28, entries = new[] { Entry(pup, 30) } },
                new WaveData.Wave { maxConcurrent = 56, entries = new[] { Entry(pup, 24), Entry(skel, 32) } },
                new WaveData.Wave { maxConcurrent = 70, entries = new[] { Entry(skel, 40), Entry(pup, 18) } },
            };

            // 30 pups are queued in wave 1 but only 28 can ever be alive there; wave 2/3 want fewer.
            Assert.AreEqual(28, WavePressurePlan.PeakDemand(data, pup));
            Assert.AreEqual(40, WavePressurePlan.PeakDemand(data, skel));

            Object.DestroyImmediate(data);
            Object.DestroyImmediate(pup);
            Object.DestroyImmediate(skel);
        }

        [Test]
        public void PeakDemand_SumsRepeatedEntriesOfTheSameTypeWithinAWave()
        {
            var pup = ScriptableObject.CreateInstance<ZombieData>();
            var data = ScriptableObject.CreateInstance<WaveData>();
            data.waves = new[]
            {
                new WaveData.Wave { maxConcurrent = 40, entries = new[] { Entry(pup, 8), Entry(pup, 9) } },
            };

            Assert.AreEqual(17, WavePressurePlan.PeakDemand(data, pup));

            Object.DestroyImmediate(data);
            Object.DestroyImmediate(pup);
        }

        [Test]
        public void PlanIsNullSafe()
        {
            Assert.IsFalse(WavePressurePlan.Recovering(null, true, true, 0, 0));
            Assert.AreEqual(0, WavePressurePlan.BatchSize(null, false, 0, 10));
            Assert.AreEqual(0, WavePressurePlan.OpeningBurst(null, 0, 10));
            Assert.AreEqual(0, WavePressurePlan.PeakDemand(null, null));
        }

        static WaveData.SpawnEntry Entry(ZombieData zombie, int count) =>
            new WaveData.SpawnEntry { zombie = zombie, count = count };
    }
}
