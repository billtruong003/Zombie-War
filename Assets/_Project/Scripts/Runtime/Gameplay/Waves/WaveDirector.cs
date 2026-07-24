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
    [RequireComponent(typeof(ZombieSpawner))]
    public class WaveDirector : MonoBehaviour
    {
        [SerializeField] private WaveData waveData;
        [SerializeField] private bool autoStart = true;
        [SerializeField] private float initialDelay = 2f;

        private ZombieSpawner _spawner;
        private Coroutine _loop;
        private int _lastReportedAlive = -1;

        public int CurrentWave { get; private set; }
        public bool IsRunning { get; private set; }

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

            // Pre-register/warm every pool up front so the first spawn of each type never hitches.
            foreach (var wave in waveData.waves)
                if (wave?.entries != null)
                    foreach (var e in wave.entries)
                        _spawner.EnsureRegistered(e.zombie);

            for (int w = 0; w < waveData.waves.Length; w++)
            {
                var wave = waveData.waves[w];
                CurrentWave = w + 1;

                int total = waveData.TotalZombies(w);
                WaveStarted?.Invoke(CurrentWave);
                Bill.Events?.Fire(new WaveStartedEvent(CurrentWave, waveData.waves.Length, total));

                yield return SpawnWave(wave);

                // Wave is only cleared once every zombie has died AND returned to the pool.
                while (ZombieManager.AliveCount > 0) yield return null;

                WaveCleared?.Invoke(CurrentWave);
                Bill.Events?.Fire(new WaveClearedEvent(CurrentWave));

                if (wave.restAfterClear > 0f) yield return new WaitForSeconds(wave.restAfterClear);
            }

            AllWavesCleared?.Invoke();
            Bill.Events?.Fire(new AllWavesClearedEvent());
            IsRunning = false;
        }

        private IEnumerator SpawnWave(WaveData.Wave wave)
        {
            var queue = new List<ZombieData>();
            if (wave.entries != null)
                foreach (var e in wave.entries)
                    if (e.zombie != null)
                        for (int i = 0; i < Mathf.Max(1, e.count); i++)
                            queue.Add(e.zombie);

            // Fisher-Yates shuffle so mixed waves don't spawn in rigid type-blocks.
            for (int i = queue.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (queue[i], queue[j]) = (queue[j], queue[i]);
            }

            int cap = Mathf.Max(1, wave.maxConcurrent);
            var wait = new WaitForSeconds(Mathf.Max(0.01f, wave.spawnInterval));

            int spawned = 0;
            int consecutiveFailures = 0;
            while (spawned < queue.Count)
            {
                // Respect the concurrency cap - hold spawning while the field is full.
                if (ZombieManager.AliveCount >= cap)
                {
                    yield return null;
                    continue;
                }

                if (_spawner.Spawn(queue[spawned]) != null)
                {
                    spawned++;
                    consecutiveFailures = 0;
                }
                else
                {
                    consecutiveFailures++;
                    if (consecutiveFailures >= 8)
                    {
                        Debug.LogError($"[WaveDirector] Skipping {queue[spawned].name}: " +
                                       "no safe reachable spawn after 8 retries.");
                        spawned++;
                        consecutiveFailures = 0;
                    }
                }
                yield return wait;
            }
        }
    }
}
