using BillGameCore;

namespace ZombieWar
{
    // Fired through Bill.Events so the HUD / audio / analytics can react without a hard reference
    // to the WaveDirector. Structs implement IEvent (see BillGameCore.Interfaces).

    public readonly struct WaveStartedEvent : IEvent
    {
        public readonly int WaveNumber;   // 1-based
        public readonly int TotalWaves;
        public readonly int ZombiesInWave;

        public WaveStartedEvent(int waveNumber, int totalWaves, int zombiesInWave)
        {
            WaveNumber = waveNumber;
            TotalWaves = totalWaves;
            ZombiesInWave = zombiesInWave;
        }
    }

    public readonly struct WaveClearedEvent : IEvent
    {
        public readonly int WaveNumber;   // 1-based
        public WaveClearedEvent(int waveNumber) { WaveNumber = waveNumber; }
    }

    public readonly struct AllWavesClearedEvent : IEvent { }

    public readonly struct ZombieCountChangedEvent : IEvent
    {
        public readonly int AliveCount;
        public ZombieCountChangedEvent(int aliveCount) { AliveCount = aliveCount; }
    }
}
