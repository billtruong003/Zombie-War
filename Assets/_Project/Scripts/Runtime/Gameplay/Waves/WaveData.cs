using System;
using UnityEngine;

namespace ZombieWar
{
    // Data-driven wave definition (see GAMEPLAY_DESIGN.md - core loop). A designer authors these as
    // assets; the WaveDirector reads them at runtime. No spawn logic lives here - pure data.
    //
    // Pacing is authored as PRESSURE, not as a spawn rate: a wave says how many enemies it wants
    // alive, how many of those must be on screen, and how big a group arrives at a time. The
    // director then feeds the field to hit that shape. The older rate-only fields (spawnInterval /
    // maxConcurrent) still mean exactly what they always did, so a wave that leaves every pressure
    // field at 0 keeps the original one-at-a-time behaviour - that is what stages 2-5 rely on.
    [CreateAssetMenu(menuName = "ZombieWar/Wave Data", fileName = "WD_")]
    public class WaveData : ScriptableObject
    {
        [Serializable]
        public struct SpawnEntry
        {
            public ZombieData zombie;
            [Min(1)] public int count;
        }

        [Serializable]
        public class Wave
        {
            public string label = "Wave";

            [Tooltip("Which zombie types + how many of each spawn during this wave.")]
            public SpawnEntry[] entries;

            [Tooltip("Seconds between spawn batches while pressure is healthy.")]
            [Min(0f)] public float spawnInterval = 0.75f;

            [Tooltip("Hard cap on zombies alive on the field at once during this wave. Never exceeded.")]
            [Min(1)] public int maxConcurrent = 20;

            [Tooltip("Breather (seconds) after the wave is fully cleared before the next one starts.")]
            [Min(0f)] public float restAfterClear = 4f;

            [Header("Pressure (leave at 0 for legacy one-at-a-time spawning)")]

            [Tooltip("How many enemies the director tries to keep alive while the queue still has " +
                     "enemies left. Soft target - clamped to maxConcurrent. 0 = use maxConcurrent.")]
            [Min(0)] public int targetAlive;

            [Tooltip("Fewest enemies that should be on camera before the director switches to the " +
                     "faster recovery interval and the inner spawn band. 0 = never recover.")]
            [Min(0)] public int visibleFloor;

            [Tooltip("Fewest enemies that should be closing on the camera (roughly 1-3s out) before " +
                     "recovery is allowed to stop. Keeps the screen from emptying again immediately.")]
            [Min(0)] public int reserveFloor;

            [Tooltip("Enemies placed in one go the moment the wave starts, so the field reads as a " +
                     "horde before the first shot. 0 = 1 (legacy).")]
            [Min(0)] public int initialBurst;

            [Tooltip("Enemies placed per spawn tick. Groups arrive together instead of in a queue. " +
                     "0 = 1 (legacy).")]
            [Min(0)] public int spawnBatchSize;

            [Tooltip("Seconds between batches while recovering from an empty screen. 0 = reuse " +
                     "spawnInterval.")]
            [Min(0f)] public float recoverySpawnInterval;

            /// <summary>Hard ceiling on alive enemies. The director never crosses this, recovery included.</summary>
            public int AliveCap => Mathf.Max(1, maxConcurrent);

            /// <summary>Soft alive target used while pressure is healthy. Recovery is allowed to push
            /// past this up to <see cref="AliveCap"/>, which is why the two are separate numbers.</summary>
            public int TargetAlive => targetAlive > 0 ? Mathf.Min(targetAlive, AliveCap) : AliveCap;

            public int InitialBurst => Mathf.Clamp(initialBurst, 1, AliveCap);

            public int BatchSize => Mathf.Clamp(spawnBatchSize, 1, AliveCap);

            public float NormalInterval => Mathf.Max(0.01f, spawnInterval);

            public float RecoveryInterval => recoverySpawnInterval > 0f
                ? Mathf.Max(0.01f, recoverySpawnInterval)
                : NormalInterval;

            /// <summary>A wave opts into recovery spawning purely by authoring a visible floor -
            /// there is no separate toggle to forget to tick.</summary>
            public bool UsesPressureRecovery => visibleFloor > 0;
        }

        public Wave[] waves;

        public int TotalZombies(int waveIndex)
        {
            if (waves == null || waveIndex < 0 || waveIndex >= waves.Length) return 0;
            var wave = waves[waveIndex];
            int total = 0;
            if (wave?.entries != null)
                foreach (var e in wave.entries) total += Mathf.Max(1, e.count);
            return total;
        }
    }
}
