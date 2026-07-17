using System;
using UnityEngine;

namespace ZombieWar
{
    // Data-driven wave definition (see GAMEPLAY_DESIGN.md - core loop). A designer authors these as
    // assets; the WaveDirector reads them at runtime. No spawn logic lives here - pure data.
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

            [Tooltip("Seconds between individual spawns.")]
            [Min(0f)] public float spawnInterval = 0.75f;

            [Tooltip("Hard cap on zombies alive on the field at once during this wave.")]
            [Min(1)] public int maxConcurrent = 20;

            [Tooltip("Breather (seconds) after the wave is fully cleared before the next one starts.")]
            [Min(0f)] public float restAfterClear = 4f;
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
