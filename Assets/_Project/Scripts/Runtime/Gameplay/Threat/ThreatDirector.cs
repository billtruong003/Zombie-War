using System.Collections.Generic;
using UnityEngine;
using BillGameCore;

namespace ZombieWar.Threat
{
    /// <summary>
    /// The endless-world pressure model and the only thing that spawns enemies.
    ///
    /// <code>ThreatTier = objectiveProgress + distanceBand + timePressure</code>
    ///
    /// The rule that shapes every number below: <b>more enemies before stronger enemies</b> (owner
    /// decision, 2026-09-26: the crowd read as thin). Tiers 0-3 each add one kind of enemy, and every
    /// tier grows the crowd and tightens the cadence. Health and damage only start scaling late
    /// (statScalingStartsAtTier), and gently, so a long run still ends by attrition without every
    /// enemy becoming a sponge.
    ///
    /// On top of the steady stream, a <b>horde surge</b> arrives on a fixed clock: for a few seconds
    /// the crowd ceiling rises, arrivals speed up and enemies close in from every bearing instead of
    /// along the player's path.
    ///
    /// Time pressure is NOT capped (owner decision, 2026-09-25): an endless run with a capped clock
    /// lets a player who never moves survive forever at tier 1. The clock always keeps climbing.
    /// </summary>
    [DisallowMultipleComponent]
    public class ThreatDirector : MonoBehaviour
    {
        public static ThreatDirector Instance { get; private set; }

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
        [Tooltip("Seconds between arrivals at tier 0, delivered one at a time so pressure is " +
                 "continuous instead of clumped.")]
        [SerializeField] private float baseSpawnInterval = 0.35f;
        [Tooltip("Interval multiplier per tier.")]
        [SerializeField] private float intervalTightenPerTier = 0.85f;
        [Tooltip("Floor on the arrival interval, however high the tier climbs.")]
        [SerializeField] private float minSpawnInterval = 0.08f;
        [Tooltip("Interval multiplier when the crowd is empty; eases to 1 as it reaches its target. " +
                 "Measured 2026-09-26: at a flat cadence the player killed arrivals as fast as they " +
                 "came and the crowd sat at ~15 against a target of 40. Density is the controlled " +
                 "variable, so arrivals speed up until the crowd is actually there.")]
        [SerializeField, Range(0.05f, 1f)] private float catchUpIntervalScale = 0.2f;
        [SerializeField] private int baseAlive = 40;
        [SerializeField] private int alivePerTier = 14;
        [Tooltip("Crowd ceiling at any tier outside a surge. Measured 2026-09-26: 30, 100 and 200 alive " +
                 "cost the same PlayerLoop time in the editor, so the horde is not the CPU bottleneck. " +
                 "Still to be confirmed on a mid-range Android device.")]
        [SerializeField] private int maxAlive = 160;

        [Header("Opening (TUNING)")]
        [Tooltip("Seconds the run eases in. Measured: with a 30 s ramp at full cadence an idle player " +
                 "holding the starter pistol (~48 DPS, ~1 kill/s) was at 12 HP by second 14, because " +
                 "arrivals (1.25/s) outpaced kills and the crowd piled up.")]
        [SerializeField] private float openingSeconds = 25f;
        [Tooltip("Crowd ceiling at second 0, as a fraction of the tier's ceiling. Grows to 1.")]
        [SerializeField, Range(0.05f, 1f)] private float openingAliveFraction = 0.35f;
        [Tooltip("Arrival interval multiplier at second 0. Shrinks to 1 across the opening, so the " +
                 "starter weapon out-kills arrivals while the player learns to move.")]
        [SerializeField] private float openingIntervalScale = 1.8f;

        [Header("Threat inputs (TUNING)")]
        [Tooltip("Metres from origin per distance band. Travelling outward raises pressure.")]
        [SerializeField] private float metresPerDistanceBand = 90f;
        [Tooltip("Seconds of survival per time-pressure step. Uncapped.")]
        [SerializeField] private float secondsPerTimeStep = 90f;
        [Tooltip("Sanity ceiling on the tier, far above anything a run reaches.")]
        [SerializeField] private int maxTier = 30;

        [Header("Late-run stats (TUNING)")]
        [Tooltip("First tier whose enemies arrive with more health and damage. Before it, pressure " +
                 "is crowd size, cadence and composition only.")]
        [SerializeField] private int statScalingStartsAtTier = 6;
        [Tooltip("Enemy health and damage growth per tier from statScalingStartsAtTier on.")]
        [SerializeField] private float statGrowthPerTier = 0.04f;

        [Header("Horde surge (TUNING)")]
        [Tooltip("Seconds between surges, counted from the end of the opening. 0 disables surges.")]
        [SerializeField] private float surgeEverySeconds = 75f;
        [Tooltip("How long a surge lasts.")]
        [SerializeField] private float surgeSeconds = 10f;
        [Tooltip("Crowd ceiling multiplier during a surge.")]
        [SerializeField] private float surgeAliveMultiplier = 1.5f;
        [Tooltip("Arrival interval multiplier during a surge.")]
        [SerializeField] private float surgeIntervalScale = 0.35f;
        [Tooltip("Absolute ceiling during a surge, whatever the multiplier gives.")]
        [SerializeField] private int surgeMaxAlive = 200;

        // ── run-scoped state. Reset by RunScope. ───────────────────────────────────────────
        static int _objectiveProgress;
        static float _enemyStatMultiplier = 1f;

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
        Vector3 _playerVelocity;
        ZombieSpawner _spawner;
        Transform _player;

        public int CurrentTier { get; private set; }

        /// <summary>Total arrivals this run — lets a probe measure arrival RATE, not just alive count.</summary>
        public static int ArrivalsThisRun { get; private set; }
        public static void ResetArrivals() => ArrivalsThisRun = 0;

        /// <summary>Stations completed this run. Each one raises pressure — progress costs safety.</summary>
        public static int ObjectiveProgress => _objectiveProgress;
        public static void ReportObjectiveCompleted() => _objectiveProgress++;
        public static void ResetRunState()
        {
            _objectiveProgress = 0;
            _enemyStatMultiplier = 1f;
        }

        /// <summary>Health and damage multiplier for enemies spawned now. 1 until the roster is
        /// exhausted; read once per spawn so an enemy keeps the stats it arrived with.</summary>
        public static float EnemyStatMultiplier => _enemyStatMultiplier;

        /// <summary>Stat multiplier for enemies spawned at <paramref name="tier"/>: 1 below
        /// <paramref name="startsAtTier"/>, then +<paramref name="growthPerTier"/> per tier from it on.</summary>
        public static float StatMultiplierFor(int tier, int startsAtTier, float growthPerTier) =>
            1f + Mathf.Max(0, tier - startsAtTier + 1) * Mathf.Max(0f, growthPerTier);

        /// <summary>
        /// True while a horde surge is running. Pure so the schedule is testable: the first surge
        /// starts <paramref name="every"/> seconds after the opening ends, later ones follow every
        /// <paramref name="every"/> seconds, and each lasts <paramref name="length"/> seconds.
        /// </summary>
        public static bool IsSurgeAt(float runSeconds, float openingSeconds, float every, float length)
        {
            if (every <= 0f || length <= 0f) return false;
            float t = runSeconds - Mathf.Max(0f, openingSeconds);
            if (t < every) return false;
            return (t % every) < Mathf.Min(length, every);
        }

        public bool Surging { get; private set; }

        /// <summary>
        /// Seconds until the next surge starts (0 while one is running, +inf when surges are off).
        /// Drives the HUD warning so a surge is announced before it lands, not after.
        /// </summary>
        public static float SecondsUntilSurge(float runSeconds, float openingSeconds, float every, float length)
        {
            if (every <= 0f || length <= 0f) return float.PositiveInfinity;
            if (IsSurgeAt(runSeconds, openingSeconds, every, length)) return 0f;
            float t = runSeconds - Mathf.Max(0f, openingSeconds);
            if (t < every) return every - t;
            return every - (t % every);
        }

        /// <summary>Seconds left in the running surge, 0 when none is running.</summary>
        public static float SurgeSecondsLeft(float runSeconds, float openingSeconds, float every, float length)
        {
            if (!IsSurgeAt(runSeconds, openingSeconds, every, length)) return 0f;
            float t = runSeconds - Mathf.Max(0f, openingSeconds);
            return Mathf.Min(length, every) - (t % every);
        }

        public float SecondsUntilSurgeNow(float runSeconds) =>
            SecondsUntilSurge(runSeconds, openingSeconds, surgeEverySeconds, surgeSeconds);

        public float SurgeSecondsLeftNow(float runSeconds) =>
            SurgeSecondsLeft(runSeconds, openingSeconds, surgeEverySeconds, surgeSeconds);

        void Awake()
        {
            Instance = this;
            // Reset is owned by RunScope's explicit list, NOT registered from here: the state is a
            // static and must reset even in a run where no director component exists.
        }

        void OnDestroy()
        {
            if (_playerHealth != null) _playerHealth.OnDamaged -= OnPlayerDamaged;
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            _spawner = FindFirstObjectByType<ZombieSpawner>();
            if (_spawner == null)
                Debug.LogError("[ThreatDirector] No ZombieSpawner in the scene - nothing will spawn.", this);
            else
                WarmPools();

            var pm = PlayerMovement.Instance;
            if (pm != null) _player = pm.transform;
        }

        // Pools are warmed one tier ahead of the run (2026-10-01): tiers 0 and 1 before the first
        // spawn, then each next tier as soon as the current one starts, one enemy type per frame.
        // Every type is still sized to the crowd it can reach at the top tier, so the first spawn of
        // a late-tier enemy never falls back to Instantiate mid-fight; the cost of warming every type
        // of every tier at once (more now that maps add their own monsters) is spread out instead.
        int _warmedTier = -1;
        readonly Queue<ZombieData> _warmQueue = new();

        void WarmPools()
        {
            _warmedTier = -1;
            _warmQueue.Clear();
            QueueWarm(1);
            while (_warmQueue.Count > 0) WarmNext();
        }

        void QueueWarm(int upToTier)
        {
            while (_warmedTier < Mathf.Min(upToTier, 3))
            {
                _warmedTier++;
                Pool.Clear();
                AppendTier(Pool, _warmedTier);
                for (int i = 0; i < Pool.Count; i++) _warmQueue.Enqueue(Pool[i]);
            }
        }

        void WarmNext()
        {
            if (_warmQueue.Count == 0 || _spawner == null) return;
            _spawner.EnsureRegistered(_warmQueue.Dequeue(), Mathf.Max(maxAlive, surgeMaxAlive));
        }

        ZombieData[] BaseTier(int i) => i == 0 ? tier0Basic : i == 1 ? tier1Specialist : i == 2 ? tier2Mixed : tier3Heavy;

        // The baked map in play adds its own monsters (MapTheme): its crowd joins tier 0, its later
        // kinds tier 2, its elites tier 3. Without a baked map these are empty: roster unchanged.
        static ZombieData[] ThemeTier(int i)
        {
            var streamer = ZombieWar.World.BakedMapStreamer.Active;
            var theme = streamer != null ? streamer.Theme : null;
            if (theme == null) return null;
            return i == 0 ? theme.crowd : i == 2 ? theme.later : i == 3 ? theme.elites : null;
        }

        void AppendTier(List<ZombieData> into, int i)
        {
            Append(into, BaseTier(i));
            Append(into, ThemeTier(i));
        }

        /// <summary>
        /// The threat formula. Pure and static so it can be tested without a scene, and so the tier a
        /// player is experiencing is always explainable from three visible inputs.
        /// </summary>
        public static int ComputeTier(int objectiveProgress, float distanceFromOrigin, float runSeconds,
                                      float metresPerBand, float secondsPerStep, int cap)
        {
            int distanceBand = Mathf.FloorToInt(Mathf.Max(0f, distanceFromOrigin) / Mathf.Max(1f, metresPerBand));
            int timePressure = Mathf.FloorToInt(Mathf.Max(0f, runSeconds) / Mathf.Max(1f, secondsPerStep));

            int tier = objectiveProgress + distanceBand + timePressure;
            return Mathf.Clamp(tier, 0, cap);
        }

        /// <summary>Arrival interval multiplier during the opening: slow at second 0, normal by the end.</summary>
        public static float OpeningIntervalScale(float runSeconds, float openingSeconds, float startScale)
        {
            if (openingSeconds <= 0f || runSeconds >= openingSeconds) return 1f;
            return Mathf.Lerp(Mathf.Max(1f, startScale), 1f, Mathf.Clamp01(runSeconds / openingSeconds));
        }

        /// <summary>Arrival interval multiplier from how full the crowd is: <paramref name="emptyScale"/>
        /// with nobody alive, 1 at (or above) the target.</summary>
        public static float CatchUpScale(int alive, int target, float emptyScale)
        {
            if (target <= 0) return 1f;
            return Mathf.Lerp(Mathf.Clamp01(emptyScale), 1f, Mathf.Clamp01((float)alive / target));
        }

        /// <summary>Crowd ceiling during the opening: a fraction of the tier's ceiling that grows to
        /// the full value, so the first seconds teach movement instead of ending the run.</summary>
        public static int OpeningAliveTarget(int fullTarget, float runSeconds, float openingSeconds, float startFraction)
        {
            if (openingSeconds <= 0f || runSeconds >= openingSeconds) return fullTarget;
            // Ease-in, not linear: the crowd stays small while the player is still learning to move
            // and only fills in over the back half of the opening.
            float t = Mathf.Clamp01(runSeconds / openingSeconds);
            return Mathf.Max(1, Mathf.CeilToInt(fullTarget * Mathf.Lerp(startFraction, 1f, t * t)));
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

            // Sampled every frame: the lead below needs the player's CURRENT velocity. It used to be
            // sampled only on spawn ticks and divided by one frame's delta, so it always read as a
            // flat-out sprint and clamped to the maximum lead.
            _playerVelocity = (p - _lastPlayerPos) / Mathf.Max(Time.deltaTime, 1e-4f);
            _lastPlayerPos = p;

            // Genre rule 7 (05/10): distance adds at most one band per DistanceBandEverySeconds, so
            // running to dodge never spikes the tier.
            float allowedBands = Mathf.Floor(run.Duration / DistanceBandEverySeconds);
            distance = Mathf.Min(distance, (allowedBands + 0.999f) * Mathf.Max(1f, metresPerDistanceBand));
            TickRest(run.Duration);

            int tier = ComputeTier(_objectiveProgress, distance, run.Duration,
                                   metresPerDistanceBand, secondsPerTimeStep, maxTier);
            _enemyStatMultiplier = StatMultiplierFor(tier, statScalingStartsAtTier, statGrowthPerTier);
            QueueWarm(tier + 1);
            WarmNext();
            if (tier != CurrentTier)
            {
                bool rising = tier > CurrentTier;
                CurrentTier = tier;
                if (Bill.IsReady) Bill.Events.Fire(new ThreatTierChangedEvent(tier, rising));
            }

            bool surging = !Resting && IsSurgeAt(run.Duration, openingSeconds, surgeEverySeconds, surgeSeconds);
            if (surging != Surging)
            {
                Surging = surging;
                if (Bill.IsReady) Bill.Events.Fire(new HordeSurgeEvent(surging));
            }

            if (_spawner == null) return;

            if (Time.time < _nextSpawnAt) return;

            int target = surging
                ? SurgeAliveTargetFor(CurrentTier)
                : OpeningAliveTarget(AliveTargetFor(CurrentTier), run.Duration, openingSeconds, openingAliveFraction);

            // Jittered interval: a fixed cadence lets arrivals re-synchronise into packs even at
            // burst 1, because they all travel at the same speed from the same band.
            float interval = SpawnIntervalFor(CurrentTier)
                             * OpeningIntervalScale(run.Duration, openingSeconds, openingIntervalScale)
                             * (surging ? surgeIntervalScale : 1f)
                             * CatchUpScale(ZombieManager.AliveCount, target, catchUpIntervalScale)
                             * (Resting ? RestIntervalScale : 1f);
            _nextSpawnAt = Time.time + Mathf.Max(0.03f, interval) * (1f + Random.Range(-intervalJitter, intervalJitter));

            var data = Resting ? PickFodder() : PickFor(CurrentTier, run.Duration, target);
            if (data == null) return;
            _spawner.EnsureRegistered(data, target);

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
            if (ZombieManager.AliveCount >= target) return;

            // Lead the player. A stationary player gets a normal ring; a running player gets pressure
            // placed along their path so the world keeps meeting them. A surge does NOT lead: it
            // closes in from every bearing, which is what makes it read as being surrounded.
            Vector3 lead = !surging && _playerVelocity.sqrMagnitude > 1f
                ? p + Vector3.ClampMagnitude(_playerVelocity, 8f) * leadSeconds
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

            _spawner.SpawnFocusOverride = null;   // never leak the override into other spawners
        }

        public float SpawnIntervalFor(int tier) =>
            Mathf.Max(minSpawnInterval, baseSpawnInterval * Mathf.Pow(intervalTightenPerTier, Mathf.Max(0, tier)));

        public int AliveTargetFor(int tier) => Mathf.Min(maxAlive, baseAlive + alivePerTier * Mathf.Max(0, tier));

        public int SurgeAliveTargetFor(int tier) =>
            Mathf.Min(surgeMaxAlive, Mathf.CeilToInt(AliveTargetFor(tier) * Mathf.Max(1f, surgeAliveMultiplier)));

        static readonly List<ZombieData> Pool = new(8);

        /// <summary>
        /// Composition for a tier. Each tier ADDS a kind rather than replacing one, so the crowd the
        /// player learned in tier 0 is still there — it simply gains a new problem alongside it.
        /// </summary>
        public ZombieData PickFor(int tier)
        {
            Pool.Clear();
            for (int i = 0; i <= Mathf.Min(tier, 3); i++) AppendTier(Pool, i);
            if (Pool.Count == 0) return null;
            return Pool[Random.Range(0, Pool.Count)];
        }

        // ── 05/10 genre rule 2: the crowd is fodder, specialists are a capped share ─────────────
        public const float RangedFrom = 120f, RangedStepSeconds = 45f, RangedShare = 0.15f;
        public const int RangedStart = 2;

        /// <summary>Ranged enemies allowed alive at once: none before 2:00, then 2, one more every
        /// 45 s, never above 15% of the crowd target.</summary>
        public static int RangedCapAt(float runSeconds, int aliveTarget)
        {
            if (runSeconds < RangedFrom) return 0;
            int byTime = RangedStart + Mathf.FloorToInt((runSeconds - RangedFrom) / RangedStepSeconds);
            return Mathf.Min(byTime, Mathf.Max(RangedStart, Mathf.CeilToInt(aliveTarget * RangedShare)));
        }

        /// <summary>Extra weight of the tier-0 fodder in the pick: 3 before 2:00, 2 before 4:00.</summary>
        public static int FodderWeightAt(float runSeconds) => runSeconds < 120f ? 3 : runSeconds < 240f ? 2 : 1;

        static readonly List<ZombieData> Weighted = new(32);
        static readonly List<ZombieData> Tier0Scratch = new(8);

        /// <summary>The tier's roster, weighted toward fodder early, with ranged kinds held back by
        /// <see cref="RangedCapAt"/>.</summary>
        public ZombieData PickFor(int tier, float runSeconds, int aliveTarget)
        {
            Pool.Clear();
            for (int i = 0; i <= Mathf.Min(tier, 3); i++) AppendTier(Pool, i);
            if (Pool.Count == 0) return null;

            int rangedAlive = 0;
            var alive = ZombieManager.Alive;
            for (int i = 0; i < alive.Count; i++)
                if (alive[i] != null && !alive[i].IsDead && alive[i].Data != null && alive[i].Data.archetype == ZombieArchetype.Ranged) rangedAlive++;
            bool rangedOk = rangedAlive < RangedCapAt(runSeconds, aliveTarget);

            Weighted.Clear();
            int fodder = FodderWeightAt(runSeconds);
            var tier0 = Tier0Scratch;
            tier0.Clear();
            AppendTier(tier0, 0);
            for (int i = 0; i < Pool.Count; i++)
            {
                var d = Pool[i];
                if (d.archetype == ZombieArchetype.Ranged && !rangedOk) continue;
                int w = tier0.Contains(d) ? fodder : 1;
                for (int k = 0; k < w; k++) Weighted.Add(d);
            }
            if (Weighted.Count == 0) return Pool[Random.Range(0, Pool.Count)];
            return Weighted[Random.Range(0, Weighted.Count)];
        }

        ZombieData PickFodder()
        {
            Pool.Clear();
            AppendTier(Pool, 0);
            return Pool.Count == 0 ? PickFor(CurrentTier) : Pool[Random.Range(0, Pool.Count)];
        }

        // ── 05/10 genre rule 10: tension breathes ──────────────────────────────────────────
        // When the player is taking a beating (a big share of health lost in the last few seconds,
        // or low health) the director rests: only fodder arrives, at half the cadence, and no surge
        // starts. The crowd stays dense - it just stops getting harder for a moment.
        public const float StressWindow = 6f, StressLossFraction = 0.35f, StressLowHealth = 0.3f;
        public const float RestSeconds = 10f, RestCooldown = 30f, RestIntervalScale = 2f;
        public const float DistanceBandEverySeconds = 120f;

        public bool Resting { get; private set; }
        float _restUntil, _restReadyAt;
        Health _playerHealth;
        readonly Queue<(float at, float amount)> _recentLoss = new();

        void OnPlayerDamaged(float amount) => _recentLoss.Enqueue((Time.time, amount));

        void TickRest(float runSeconds)
        {
            if (_playerHealth == null && _player != null && _player.TryGetComponent(out _playerHealth))
                _playerHealth.OnDamaged += OnPlayerDamaged;
            if (Resting && Time.time >= _restUntil) Resting = false;
            while (_recentLoss.Count > 0 && Time.time - _recentLoss.Peek().at > StressWindow) _recentLoss.Dequeue();
            if (Resting || Time.time < _restReadyAt || _playerHealth == null || _playerHealth.Max <= 0f) return;

            float lost = 0f;
            foreach (var l in _recentLoss) lost += l.amount;
            float hp = _playerHealth.Current / _playerHealth.Max;
            if (lost / _playerHealth.Max < StressLossFraction && hp >= StressLowHealth) return;
            Resting = true;
            _restUntil = Time.time + RestSeconds;
            _restReadyAt = _restUntil + RestCooldown;
            _recentLoss.Clear();
        }

        static void Append(List<ZombieData> into, ZombieData[] src)
        {
            if (src == null) return;
            for (int i = 0; i < src.Length; i++) if (src[i] != null) into.Add(src[i]);
        }

        /// <summary>Every enemy type across all tiers, each once (the Skill Sandbox picks from it).</summary>
        public void CollectRoster(List<ZombieData> into)
        {
            for (int i = 0; i <= 3; i++)
                foreach (var tier in new[] { BaseTier(i), ThemeTier(i) })
                    if (tier != null)
                        foreach (var d in tier)
                            if (d != null && !into.Contains(d)) into.Add(d);
        }

        /// <summary>Roster size available at a tier — used by tests to prove composition widens.</summary>
        public int RosterSizeFor(int tier)
        {
            Pool.Clear();
            for (int i = 0; i <= Mathf.Min(tier, 3); i++) AppendTier(Pool, i);
            return Pool.Count;
        }
    }

    /// <summary>A horde surge started (<see cref="Started"/> true) or ended.</summary>
    public readonly struct HordeSurgeEvent : IEvent
    {
        public readonly bool Started;
        public HordeSurgeEvent(bool started) { Started = started; }
    }

    /// <summary>The run's threat tier moved. <see cref="Rising"/> is false only when the player
    /// walks back toward the origin and the distance band drops.</summary>
    public readonly struct ThreatTierChangedEvent : IEvent
    {
        public readonly int Tier;
        public readonly bool Rising;
        public ThreatTierChangedEvent(int tier, bool rising) { Tier = tier; Rising = rising; }
    }
}
