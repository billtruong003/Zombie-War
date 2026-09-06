using System.Collections.Generic;
using UnityEngine;
using BillGameCore;

namespace ZombieWar.Threat
{
    /// <summary>
    /// M7.3d — the endless-world pressure model that replaces designed waves.
    ///
    /// <code>ThreatTier = base + objectiveProgress + distanceBand + boundedTimePressure</code>
    ///
    /// The rule that shapes every number below: <b>composition changes before stats.</b> Eleven of the
    /// sixteen enemy assets were unused in production — ranged, burrowers, an elite and two bosses —
    /// so pressure is expressed by WHO shows up, not by multiplying health. A tier introduces at most
    /// one new tactical question.
    ///
    /// <b>Ships DISABLED behind <see cref="enabled"/>.</b> Waves still drive the game. A half-migrated
    /// spawner that spawns nothing would be far worse than a game that still uses waves, so this runs
    /// only when switched on deliberately.
    /// </summary>
    [DisallowMultipleComponent]
    public class ThreatDirector : MonoBehaviour
    {
        public static ThreatDirector Instance { get; private set; }

        [Header("Migration")]
        [Tooltip("OFF by default. While off, WaveDirector/WavePressurePlan keep driving every spawn " +
                 "exactly as before and this component only observes.")]
        [SerializeField] private bool driveSpawning = false;

        [Header("Roster — composition IS the difficulty (TUNING)")]
        [Tooltip("Tier 0: the baseline crowd. Walkers and runners only.")]
        [SerializeField] private ZombieData[] tier0Basic;
        [Tooltip("Tier 1 adds exactly ONE specialist: ranged or pouncer.")]
        [SerializeField] private ZombieData[] tier1Specialist;
        [Tooltip("Tier 2 mixes specialists and introduces an elite chance.")]
        [SerializeField] private ZombieData[] tier2Mixed;
        [Tooltip("Tier 3: recovery-window enemies and the boss route.")]
        [SerializeField] private ZombieData[] tier3Heavy;

        [Header("Cadence (TUNING)")]
        [Tooltip("M7.4b: was 2.2 s with a burst of 3 (1.36 arrivals/s in packs). Now ~1.25 arrivals/s " +
                 "delivered ONE at a time — same average pressure, continuous instead of clumped.")]
        [SerializeField] private float baseSpawnInterval = 0.8f;   // TUNING (was 2.2)
        [Tooltip("Interval multiplier per tier — tighter cadence, never bigger health bars.")]
        [SerializeField] private float intervalTightenPerTier = 0.82f;
        [SerializeField] private int baseAlive = 18;
        [SerializeField] private int alivePerTier = 8;

        [Header("Threat inputs (TUNING)")]
        [Tooltip("Metres from origin per distance band. Travelling outward raises pressure.")]
        [SerializeField] private float metresPerDistanceBand = 90f;
        [Tooltip("Seconds of survival per time-pressure step.")]
        [SerializeField] private float secondsPerTimeStep = 75f;
        [Tooltip("CAP on time pressure. A losing player is encouraged to finish, never made " +
                 "mathematically doomed by the clock alone.")]
        [SerializeField] private int maxTimePressure = 1;
        [SerializeField] private int maxTier = 3;

        // ── run-scoped state. Registered with RunScope on the day this was written. ──────────
        static int _objectiveProgress;

        float _nextSpawnAt;
        int _spawnsUntilSectorReset;

        [Tooltip("Spawns before the sector cursor is reset. Matches the spawner's 8 sectors so a " +
                 "full cycle covers every bearing around the player.")]
        [SerializeField] private int sectorsPerCycle = 8;
        [Tooltip("Seconds of player motion to lead the spawn focus by. Higher = more enemies placed " +
                 "in the path of a running player.")]
        [SerializeField] private float leadSeconds = 2.5f;      // TUNING
        [Tooltip("M7.4b: back to 1. A burst of 3 made enemies leave, travel and ARRIVE as a pack — " +
                 "the owner's 'pressure arrives in a clump then the world thins out', and the same " +
                 "packs delivered the instant deaths. The shortened interval keeps the average.")]
        [SerializeField] private int spawnBurst = 1;            // TUNING (was 3)
        [Tooltip("Random fraction applied to each interval so arrivals do not fall on a metronome " +
                 "and re-form into packs.")]
        [SerializeField, Range(0f, 0.9f)] private float intervalJitter = 0.45f;   // TUNING

        Vector3 _lastPlayerPos;
        ZombieSpawner _spawner;
        Transform _player;

        public int CurrentTier { get; private set; }

        /// <summary>Total arrivals this run — lets a probe measure arrival RATE, not just alive count.</summary>
        public static int ArrivalsThisRun { get; private set; }
        public static void ResetArrivals() => ArrivalsThisRun = 0;
        public bool DrivingSpawning => driveSpawning;

        /// <summary>Stations completed this run. Each one raises pressure — progress costs safety.</summary>
        public static int ObjectiveProgress => _objectiveProgress;
        public static void ReportObjectiveCompleted() => _objectiveProgress++;
        public static void ResetRunState() => _objectiveProgress = 0;

        void Awake()
        {
            Instance = this;
            // Reset is owned by RunScope's explicit list, NOT registered from here: the state is a
            // static and must reset even in a run where no director component exists.
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            _spawner = FindFirstObjectByType<ZombieSpawner>();
            var pm = PlayerMovement.Instance;
            if (pm != null) _player = pm.transform;
        }

        /// <summary>
        /// The threat formula. Pure and static so it can be tested without a scene, and so the tier a
        /// player is experiencing is always explainable from four visible inputs.
        /// </summary>
        public static int ComputeTier(int objectiveProgress, float distanceFromOrigin, float runSeconds,
                                      float metresPerBand, float secondsPerStep, int timeCap, int cap)
        {
            int distanceBand = Mathf.FloorToInt(Mathf.Max(0f, distanceFromOrigin) / Mathf.Max(1f, metresPerBand));
            int timePressure = Mathf.Min(timeCap,
                Mathf.FloorToInt(Mathf.Max(0f, runSeconds) / Mathf.Max(1f, secondsPerStep)));

            // base 0 + objectives + distance + capped time
            int tier = objectiveProgress + distanceBand + timePressure;
            return Mathf.Clamp(tier, 0, cap);
        }

        void Update()
        {
            var run = RunState.Current;
            if (run == null || run.IsOver) return;

            if (_player == null)
            {
                var pm = PlayerMovement.Instance;
                if (pm == null) return;
                _player = pm.transform;
            }

            Vector3 p = _player.position;
            float distance = new Vector2(p.x, p.z).magnitude;

            CurrentTier = ComputeTier(_objectiveProgress, distance, run.Duration,
                                      metresPerDistanceBand, secondsPerTimeStep, maxTimePressure, maxTier);

            // Observation only until the owner switches this on.
            if (!driveSpawning || _spawner == null) return;

            if (Time.time < _nextSpawnAt) return;
            // Jittered interval: a fixed cadence lets arrivals re-synchronise into packs even at
            // burst 1, because they all travel at the same speed from the same band.
            float interval = SpawnIntervalFor(CurrentTier);
            _nextSpawnAt = Time.time + interval * (1f + Random.Range(-intervalJitter, intervalJitter));

            var data = PickFor(CurrentTier);
            if (data == null) return;

            _spawner.EnsureRegistered(data, AliveTargetFor(CurrentTier));

            // BeginBatch resets the spawner's sector cursor. Calling it per spawn would restart the
            // fan-out every time and pile arrivals into the same sector — the one-direction tail
            // this milestone exists to remove. Reset only once per cycle so consecutive spawns walk
            // the sectors and pressure arrives from several bearings.
            if (--_spawnsUntilSectorReset <= 0)
            {
                _spawner.BeginBatch();
                _spawnsUntilSectorReset = sectorsPerCycle;
            }

            // Crowd ceiling: pressure is cadence and composition, never an unbounded pile.
            int target = AliveTargetFor(CurrentTier);
            if (ZombieManager.AliveCount >= target) return;

            // Lead the player. A stationary player gets a normal ring; a running player gets pressure
            // placed along their path so the world keeps meeting them.
            Vector3 velocity = (p - _lastPlayerPos) / Mathf.Max(Time.deltaTime, 1e-4f);
            _lastPlayerPos = p;
            Vector3 lead = velocity.sqrMagnitude > 1f
                ? p + Vector3.ClampMagnitude(velocity, 8f) * leadSeconds
                : p;
            _spawner.SpawnFocusOverride = lead;

            // Spawn a small burst per tick: one enemy every 2.2 s cannot build a crowd against a
            // player who is also killing them. Measured alive count was 5-7 with single spawns.
            int burst = Mathf.Min(spawnBurst, target - ZombieManager.AliveCount);
            for (int i = 0; i < burst; i++)
            {
                // Vary the lead per spawn so two arrivals in the same tick start at different
                // radii and reach the player at different times rather than as one wall.
                var scatter = Random.insideUnitCircle * 4f;
                _spawner.SpawnFocusOverride = lead + new Vector3(scatter.x, 0f, scatter.y);
                _spawner.Spawn(data);
                ArrivalsThisRun++;
            }

            _spawner.SpawnFocusOverride = null;   // never leak the override into wave spawning
        }

        public float SpawnIntervalFor(int tier) =>
            baseSpawnInterval * Mathf.Pow(intervalTightenPerTier, Mathf.Max(0, tier));

        public int AliveTargetFor(int tier) => baseAlive + alivePerTier * Mathf.Max(0, tier);

        static readonly List<ZombieData> Pool = new(8);

        /// <summary>
        /// Composition for a tier. Each tier ADDS a kind rather than replacing one, so the crowd the
        /// player learned in tier 0 is still there — it simply gains a new problem alongside it.
        /// </summary>
        public ZombieData PickFor(int tier)
        {
            Pool.Clear();
            Append(Pool, tier0Basic);
            if (tier >= 1) Append(Pool, tier1Specialist);
            if (tier >= 2) Append(Pool, tier2Mixed);
            if (tier >= 3) Append(Pool, tier3Heavy);
            if (Pool.Count == 0) return null;
            return Pool[Random.Range(0, Pool.Count)];
        }

        static void Append(List<ZombieData> into, ZombieData[] src)
        {
            if (src == null) return;
            for (int i = 0; i < src.Length; i++) if (src[i] != null) into.Add(src[i]);
        }

        /// <summary>Roster size available at a tier — used by tests to prove composition widens.</summary>
        public int RosterSizeFor(int tier)
        {
            Pool.Clear();
            Append(Pool, tier0Basic);
            if (tier >= 1) Append(Pool, tier1Specialist);
            if (tier >= 2) Append(Pool, tier2Mixed);
            if (tier >= 3) Append(Pool, tier3Heavy);
            return Pool.Count;
        }
    }
}
