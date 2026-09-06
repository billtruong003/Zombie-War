using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    // The core-loop driver (see GAMEPLAY_DESIGN.md). Reads a WaveData asset and runs the loop:
    //   spawn wave -> wait until every zombie is dead -> breather -> next wave -> ... -> all clear.
    // Uses ZombieManager.AliveCount as the single source of truth for "is the wave cleared?" and the
    // ZombieSpawner for placement. Broadcasts progress through Bill.Events (decoupled) AND local C#
    // events (for tightly-coupled listeners like a HUD sitting on the same object).
    //
    // Spawning is driven by PRESSURE, not by a queue drain rate. The wave opens with a burst so the
    // field is already full when the player takes their first shot, then tops the field back up to
    // the wave's alive target in mini-batches. If the screen thins out below the wave's visible
    // floor the loop drops to the recovery interval and the inner spawn band until both the screen
    // and the approach lane behind it have refilled. The old behaviour - one zombie per interval,
    // waiting on a hard cap the field almost never reached - is what left 5-8 second holes with
    // 0-3 enemies on screen. Waves that author no pressure fields still get exactly that old
    // behaviour, which is what stages 2-5 are balanced against.
    [RequireComponent(typeof(ZombieSpawner))]
    public class WaveDirector : MonoBehaviour
    {
        [SerializeField] private WaveData waveData;
        [SerializeField] private bool autoStart = true;
        [SerializeField] private float initialDelay = 2f;
        [Tooltip("M7.4a: seconds per pacing beat while the ThreatDirector drives spawning. A 'wave' " +
                 "is then an internal rhythm for audio/HUD/missions, not a designed encounter.")]
        [SerializeField] private float threatBeatSeconds = 45f;   // TUNING

        // A batch that places nothing this many ticks in a row means the arena genuinely has no room
        // for that enemy right now. One queued enemy is dropped so the wave can always finish -
        // a wave that can never complete is a dead run, which is strictly worse than one short zombie.
        private const int MaxConsecutiveFailedBatches = 8;

        // Missed placements tolerated inside a single batch before giving up on the rest of it and
        // trying again next tick. Small: a couple of misses is a crowded sector, not a broken arena.
        private const int MaxMissesPerBatch = 2;

        private ZombieSpawner _spawner;
        private Coroutine _loop;
        private int _lastReportedAlive = -1;

        // Reused across every wave of the run - the spawn loop must not allocate per wave or per tick.
        private readonly List<ZombieData> _queue = new(128);

        public int CurrentWave { get; private set; }
        public bool IsRunning { get; private set; }

        /// <summary>The wave set this director is running. Exposed for dev tooling (the horde stress
        /// test needs a real authored ZombieData to spawn, rather than carrying its own copy).</summary>
        public WaveData Waves => waveData;

        public event System.Action<int> WaveStarted;
        public event System.Action<int> WaveCleared;
        public event System.Action AllWavesCleared;

        private void Awake() => _spawner = GetComponent<ZombieSpawner>();

        private void OnEnable()
        {
            Bill.Events?.Subscribe<PlayerDiedEvent>(OnPlayerDied);
            if (autoStart) StartRun();
        }

        private void OnDisable()
        {
            Bill.Events?.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
            StopRun();
        }

        // Dead players don't get more waves - halt the loop mid-flight so nothing else spawns
        // while the death sequence / lose screen plays out.
        private void OnPlayerDied(PlayerDiedEvent e) => StopRun();

        private void Update()
        {
            // Cheap change-detection so subscribers only get an event when the count actually moves.
            int alive = ZombieManager.AliveCount;
            if (alive != _lastReportedAlive)
            {
                _lastReportedAlive = alive;
                Bill.Events?.Fire(new ZombieCountChangedEvent(alive));
            }
        }

        public void StartRun()
        {
            if (IsRunning || waveData == null || waveData.waves == null || waveData.waves.Length == 0) return;
            IsRunning = true;
            _loop = StartCoroutine(RunLoop());
        }

        public void StopRun()
        {
            if (_loop != null) StopCoroutine(_loop);
            _loop = null;
            IsRunning = false;
        }

        private IEnumerator RunLoop()
        {
            if (initialDelay > 0f) yield return new WaitForSeconds(initialDelay);

            yield return WarmPools();

            for (int w = 0; w < waveData.waves.Length; w++)
            {
                var wave = waveData.waves[w];
                CurrentWave = w + 1;

                int total = waveData.TotalZombies(w);
                WaveStarted?.Invoke(CurrentWave);
                Bill.Events?.Fire(new WaveStartedEvent(CurrentWave, waveData.waves.Length, total));

                // M7.4a — when the ThreatDirector drives spawning, this loop stops SPAWNING and
                // becomes a pure pacing beat. It still fires WaveStarted/WaveCleared, so
                // HudController, GameplayAudioDirector, MissionTracker and RunDirector keep working
                // unchanged.
                bool threatDrives = Threat.ThreatDirector.Instance != null &&
                                    Threat.ThreatDirector.Instance.DrivingSpawning;

                if (!threatDrives) yield return SpawnWave(wave);

                // THE GATE THAT BROKE THE OPEN WORLD.
                //
                // "wait until every zombie is dead" is unsatisfiable once the player can simply run:
                // the horde never dies, the wave never clears, and no further wave ever spawns — the
                // player ends up alone in an empty world with a tail strung out behind them.
                //
                // Under the threat director progression is continuous and position-relative, so the
                // beat is a fixed interval rather than a kill-everything gate.
                if (threatDrives)
                    yield return new WaitForSeconds(threatBeatSeconds);
                else
                    while (ZombieManager.AliveCount > 0) yield return null;

                WaveCleared?.Invoke(CurrentWave);
                Bill.Events?.Fire(new WaveClearedEvent(CurrentWave));

                if (wave.restAfterClear > 0f) yield return new WaitForSeconds(wave.restAfterClear);
            }

            AllWavesCleared?.Invoke();
            Bill.Events?.Fire(new AllWavesClearedEvent());
            IsRunning = false;
        }

        // Pre-register/warm every pool up front so the first spawn of each type never hitches - sized
        // to the peak number of that type that can be alive at once, not a flat eight. A 60-strong
        // wave otherwise falls back to Instantiate mid-fight, which is the one thing pooling exists
        // to prevent.
        private IEnumerator WarmPools()
        {
            foreach (var wave in waveData.waves)
            {
                if (wave?.entries == null) continue;
                foreach (var entry in wave.entries)
                {
                    if (entry.zombie == null) continue;

                    // One frame per newly warmed type. Stage 1 warms ~104 instances across four
                    // types; doing that in a single frame is a visible hitch even behind the
                    // pre-wave delay, and it costs one yield to avoid.
                    if (_spawner.EnsureRegistered(entry.zombie, WavePressurePlan.PeakDemand(waveData, entry.zombie)))
                        yield return null;
                }
            }
        }

        private IEnumerator SpawnWave(WaveData.Wave wave)
        {
            BuildQueue(wave);
            if (_queue.Count == 0) yield break;

            // Two cached waits per wave rather than one per tick.
            var normalWait = new WaitForSeconds(wave.NormalInterval);
            var recoveryWait = new WaitForSeconds(wave.RecoveryInterval);

            int spawned = 0;
            bool recovering = false;
            int failedBatches = 0;

            // Opening burst: the horde is already there when the wave title fades. Placed in
            // batch-sized chunks one frame apart - 30 placements in a single frame means 30 capsule
            // checks and 30 NavMesh path solves at once, which reads as a hitch. Two or three frames
            // still reads as instant.
            int burst = WavePressurePlan.OpeningBurst(wave, ZombieManager.AliveCount, _queue.Count);
            while (spawned < burst)
            {
                int placed = SpawnBatch(
                    Mathf.Min(wave.BatchSize, burst - spawned),
                    wave.AliveCap, spawned, ZombieSpawner.SpawnBand.Normal);
                spawned += placed;

                // Nothing placed - let the main loop's failure handling deal with it.
                if (placed == 0) break;
                if (spawned < burst) yield return null;
            }

            while (spawned < _queue.Count)
            {
                recovering = WavePressurePlan.Recovering(
                    wave, recovering, ZombieManager.HasPressure,
                    ZombieManager.VisibleCount, ZombieManager.ReserveCount);

                int want = WavePressurePlan.BatchSize(
                    wave, recovering, ZombieManager.AliveCount, _queue.Count - spawned);

                // Field is at its ceiling. Poll per frame so the next kill is topped up immediately
                // rather than after a full interval - this is the check, not the spawn rate.
                if (want <= 0)
                {
                    yield return null;
                    continue;
                }

                var band = recovering ? ZombieSpawner.SpawnBand.Recovery : ZombieSpawner.SpawnBand.Normal;
                int placed = SpawnBatch(want, wave.AliveCap, spawned, band);
                spawned += placed;

                if (placed > 0) failedBatches = 0;
                else if (++failedBatches >= MaxConsecutiveFailedBatches)
                {
                    Debug.LogError(
                        $"[WaveDirector] Wave {CurrentWave}: dropping {_queue[spawned].name} - no safe " +
                        $"reachable spawn after {MaxConsecutiveFailedBatches} batches. The wave continues.");
                    spawned++;
                    failedBatches = 0;
                }

                yield return recovering ? recoveryWait : normalWait;
            }
        }

        // Places up to `count` enemies as one group. The hard cap is re-checked per enemy, not per
        // batch, so a batch sized against a stale alive count can still never overshoot it.
        private int SpawnBatch(int count, int aliveCap, int fromIndex, ZombieSpawner.SpawnBand band)
        {
            if (count <= 0) return 0;

            _spawner.BeginBatch();

            int placed = 0;
            int misses = 0;

            while (placed < count && misses <= MaxMissesPerBatch)
            {
                if (ZombieManager.AliveCount >= aliveCap) break;

                int index = fromIndex + placed;
                if (index >= _queue.Count) break;

                if (_spawner.Spawn(_queue[index], band) != null) placed++;
                else misses++;
            }

            return placed;
        }

        private void BuildQueue(WaveData.Wave wave)
        {
            _queue.Clear();
            if (wave.entries != null)
                foreach (var e in wave.entries)
                    if (e.zombie != null)
                        for (int i = 0; i < Mathf.Max(1, e.count); i++)
                            _queue.Add(e.zombie);

            // Fisher-Yates shuffle so mixed waves don't spawn in rigid type-blocks.
            for (int i = _queue.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (_queue[i], _queue[j]) = (_queue[j], _queue[i]);
            }
        }
    }
}
