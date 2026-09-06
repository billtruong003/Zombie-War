using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// The arithmetic behind pressure spawning, kept out of <see cref="WaveDirector"/>'s coroutine so
    /// it can be unit-tested without a scene, a camera or a NavMesh. Pure functions over
    /// (wave, current pressure) - no Unity object touched, nothing cached, nothing allocated.
    /// </summary>
    public static class WavePressurePlan
    {
        /// <summary>
        /// Whether the director should be spawning on recovery settings (faster interval, inner
        /// band) this tick.
        ///
        /// Asymmetric on purpose. Recovery STARTS as soon as the screen thins out past the visible
        /// floor, because an empty screen is the failure the player actually notices. It only STOPS
        /// once the screen is refilled AND enough enemies are closing in behind them - dropping out
        /// on the visible count alone just empties the screen again a second later, which is the
        /// oscillation the old serial feed produced.
        /// </summary>
        public static bool Recovering(
            WaveData.Wave wave, bool wasRecovering, bool hasPressure, int visible, int reserve)
        {
            if (wave == null || !wave.UsesPressureRecovery) return false;
            // No snapshot yet (no camera, no player) is not evidence of an empty screen.
            if (!hasPressure) return false;

            if (!wasRecovering) return visible < wave.visibleFloor;
            return visible < wave.visibleFloor || reserve < wave.reserveFloor;
        }

        /// <summary>
        /// How many enemies to place this tick. Recovery is allowed to fill up to the wave's hard
        /// cap; normal spawning stops at the softer alive target so there is headroom left to
        /// recover into. Never returns more than the queue has left, and never exceeds the cap.
        /// </summary>
        public static int BatchSize(WaveData.Wave wave, bool recovering, int alive, int remaining)
        {
            if (wave == null || remaining <= 0) return 0;

            int ceiling = recovering ? wave.AliveCap : wave.TargetAlive;
            int room = Mathf.Min(ceiling - alive, remaining);
            if (room <= 0) return 0;

            return Mathf.Min(wave.BatchSize, room);
        }

        /// <summary>Opening group for a wave - the field has to read as a horde before the first
        /// shot, so this ignores the alive target and is bounded only by the hard cap and the queue.</summary>
        public static int OpeningBurst(WaveData.Wave wave, int alive, int remaining)
        {
            if (wave == null || remaining <= 0) return 0;
            int room = Mathf.Min(wave.AliveCap - alive, remaining);
            return room <= 0 ? 0 : Mathf.Min(wave.InitialBurst, room);
        }

        /// <summary>
        /// Peak simultaneous demand for one enemy type across a whole wave set - what the pool must
        /// be warmed to. A type can never have more instances alive than the wave's own hard cap,
        /// so the per-wave demand is clamped there before taking the maximum.
        /// </summary>
        public static int PeakDemand(WaveData waveData, ZombieData zombie)
        {
            if (waveData?.waves == null || zombie == null) return 0;

            int peak = 0;
            foreach (var wave in waveData.waves)
            {
                if (wave?.entries == null) continue;
                int inWave = 0;
                foreach (var entry in wave.entries)
                    if (entry.zombie == zombie) inWave += Mathf.Max(1, entry.count);

                peak = Mathf.Max(peak, Mathf.Min(inWave, wave.AliveCap));
            }
            return peak;
        }
    }
}
