using System.Collections.Generic;
using UnityEngine;
using BillGameCore;

namespace ZombieWar.Stations
{
    /// <summary>
    /// M7.3 — streams stations around the player and owns their lifetime.
    ///
    /// One director ticks every live station; stations have no Update of their own, so the per-frame
    /// cost is proportional to the handful of nearby anchors and NOT to the enemy count.
    ///
    /// Everything run-scoped it owns is registered with <see cref="RunScope"/> in Awake.
    /// </summary>
    [DisallowMultipleComponent]
    public class StationDirector : MonoBehaviour
    {
        public static StationDirector Instance { get; private set; }

        [Header("Bodies — dressed by the signal language, never relied on for meaning")]
        [SerializeField] private GameObject relayBody;
        [SerializeField] private GameObject cacheBody;
        [SerializeField] private GameObject beaconBody;

        [Header("Materials")]
        [SerializeField] private Material signalLineMaterial;

        [Header("Streaming (TUNING)")]
        [Tooltip("Cells around the player considered for stations. 1 = the 3x3 block of 60 m cells.")]
        [SerializeField] private int cellRadius = 1;
        [Tooltip("Beyond this the station object is released; the LEDGER keeps its state.")]
        [SerializeField] private float releaseDistance = 110f;

        [Header("Boss Beacon")]
        [SerializeField] private ZombieData[] bossRoster;
        public System.Collections.Generic.IReadOnlyList<ZombieData> BossRoster => bossRoster;

        readonly Dictionary<long, Station> _live = new(8);
        // Which beacon spawned which boss, so a kill can be routed back to the right anchor.
        readonly Dictionary<ZombieBase, long> _bossAnchors = new(4);
        readonly List<long> _scratch = new(8);
        readonly List<long> _gone = new(4);
        static readonly StationAnchors.Anchor[] AnchorBuffer = new StationAnchors.Anchor[32];

        Transform _player;
        StationCompass _compass;
        int _worldSeed = 20260816;

        public int LiveStationCount => _live.Count;

        /// <summary>Supply Caches bought this run; each one raises the next price. Run-scoped.</summary>
        public static int CachePurchasesThisRun { get; set; }

        [Header("Rewards (TUNING)")]
        [Tooltip("A beacon boss is the biggest opt-in risk, so it pays the most.")]
        [SerializeField] private int bossRewardCoin = 150;
        [SerializeField] private int bossRewardGem = 3;

        void OnEnable() => Bill.Events?.Subscribe<ZombieKilledEvent>(OnAnyZombieKilled);
        void OnDisable() => Bill.Events?.Unsubscribe<ZombieKilledEvent>(OnAnyZombieKilled);

        void OnAnyZombieKilled(ZombieKilledEvent e)
        {
            // Route a beacon boss's death back to its anchor: pays the reward, frees the encounter
            // slot and marks the station spent. Without this the beacon stayed claimed forever.
            ZombieBase dead = null;
            foreach (var kv in _bossAnchors)
                if (kv.Key == null || !kv.Key.gameObject.activeInHierarchy) { dead = kv.Key; break; }
            if (dead == null) return;

            long anchorId = _bossAnchors[dead];
            _bossAnchors.Remove(dead);
            NotifyBossKilled(anchorId, e.Position);
        }

        void Awake()
        {
            Instance = this;

            // Registered on the day it was written — the rule M7.2c introduced.
            StationRegistry.EnsureRegisteredWithRunScope();
            RunScope.Register(ReleaseAllStations);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            RunScope.Unregister(ReleaseAllStations);
        }

        void Start()
        {
            _compass = gameObject.AddComponent<StationCompass>();
            _compass.SetMaterial(signalLineMaterial);

            var pm = PlayerMovement.Instance;
            if (pm != null) _player = pm.transform;
            var cfg = FindFirstObjectByType<ZombieWar.WorldStreaming.WorldStreamingConfig>();
            if (cfg != null) _worldSeed = cfg.WorldSeed;
        }

        void Update()
        {
            if (_player == null)
            {
                var pm = PlayerMovement.Instance;
                if (pm == null) return;
                _player = pm.transform;
            }
            if (RunState.Current == null || RunState.Current.IsOver) return;

            Vector3 p = _player.position;
            float dt = Time.deltaTime;

            // 1. spawn anchors that came into range
            int n = StationAnchors.Around(_worldSeed, p, cellRadius, AnchorBuffer);
            for (int i = 0; i < n; i++)
            {
                var a = AnchorBuffer[i];
                if (_live.ContainsKey(a.id)) continue;
                // A9b: a station that has paid out dissolved away; only a fresh one (or a repeatable
                // one whose cooldown has run out) materialises again.
                if (StationRegistry.StatusOf(a.id, Time.time) != StationRegistry.Status.Untouched) continue;
                Spawn(a);
            }

            // 2. tick the live ones, release those the player has left behind, and find the nearest
            //    one still worth walking to for the compass
            _scratch.Clear();
            Station nearest = null;
            float nearestSqr = float.MaxValue;
            foreach (var kv in _live)
            {
                var st = kv.Value;
                if (st == null) { _scratch.Add(kv.Key); continue; }
                if (st.Gone) { _gone.Add(kv.Key); continue; }

                float sqr = (st.transform.position - p).sqrMagnitude;
                if (sqr > releaseDistance * releaseDistance) { _scratch.Add(kv.Key); continue; }

                st.Tick(dt, p);
                if (!st.Finished && sqr < nearestSqr) { nearest = st; nearestSqr = sqr; }
            }
            for (int i = 0; i < _scratch.Count; i++) Release(_scratch[i]);
            // A dissolved station just goes: its boss (a beacon's) and the encounter slot stay owned.
            for (int i = 0; i < _gone.Count; i++)
            {
                if (_live.TryGetValue(_gone[i], out var st) && st != null) Destroy(st.gameObject);
                _live.Remove(_gone[i]);
            }
            _gone.Clear();

            _compass?.Point(p, nearest);
        }

        Station Spawn(StationAnchors.Anchor a)
        {
            var go = new GameObject($"Station_{a.kind}_{a.id}");
            // The map is dressed in advance: step off obstacles, then clear the decoration on the ring.
            go.transform.position = StationClearance.Resolve(a.position);
            go.AddComponent<StationClearing>();

            var signal = go.AddComponent<WorldSignal>();
            signal.SetMaterial(signalLineMaterial);

            // The body is decoration. If none is bound the station is still fully readable, which is
            // the whole point of the signal language being prop-independent.
            GameObject bodyPrefab = a.kind switch
            {
                StationKind.SignalRelay => relayBody,
                StationKind.SupplyCache => cacheBody,
                _ => beaconBody,
            };
            // A9: the sci-fi bodies and reward icons (StationArt) replace the old props when present.
            var art = StationArt.Instance;
            var artBody = art != null ? art.BodyFor(a.kind) : null;
            if (artBody != null) bodyPrefab = artBody;
            else if (a.kind == StationKind.SupplyDrop || a.kind == StationKind.HealZone) bodyPrefab = null;
            if (bodyPrefab != null)
            {
                var body = Instantiate(bodyPrefab, go.transform);
                body.transform.localPosition = Vector3.zero;
            }

            var station = go.AddComponent<Station>();
            station.Bind(a, signal);
            var visual = go.GetComponentInChildren<StationVisual>();
            if (visual != null)
            {
                visual.Init(WorldSignal.ColorOf(a.kind), art != null ? art.dissolveMaterial : null);
                signal.UseVisual(visual);
            }
            else if (art != null) signal.SetRewardIcon(art.IconFor(a.kind));
            _live[a.id] = station;
            return station;
        }

        static long _debugIds = long.MinValue / 2;

        /// <summary>Sandbox: a station of any type, right here, outside the world's anchor grid.</summary>
        public Station SpawnDebug(StationKind kind, Vector3 at)
        {
            at.y = 0f;
            return Spawn(new StationAnchors.Anchor(++_debugIds, at, kind));
        }

        // ───────────────────────────────────────────────────────── A9 rewards and effects

        [Header("Supply Drop (A9, TUNING)")]
        [SerializeField] private int supplyDropCoin = 45;
        [Tooltip("Chance a Supply Drop also holds a chest, before Luck.")]
        [SerializeField, Range(0f, 1f)] private float supplyChestChance = 0.2f;

        /// <summary>A Supply Drop opens: coin, a mechanic item (if none is out), sometimes a chest.</summary>
        public void GrantSupplyDrop(Vector3 at)
        {
            var pickups = PickupManager.Instance;
            if (pickups == null) return;
            float luck = ZombieWar.Skills.SkillRuntime.Active?.LuckMultiplier ?? 1f;
            bool item = pickups.SpawnMechanic(MechanicItems.Pick(Random.value), at + new Vector3(1.4f, 0f, -0.6f));
            pickups.DropReward(at, item ? supplyDropCoin : supplyDropCoin * 2, 0);
            if (Random.value < supplyChestChance * luck) pickups.SpawnChest(at + new Vector3(-1.4f, 0f, -0.6f));
        }

        public void PlayCompleteFx(StationKind kind, Vector3 at)
        {
            var art = StationArt.Instance;
            var fx = art != null ? art.CompleteFxFor(kind) : null;
            if (fx != null) FxPool.Play(fx, at, fx.transform.localRotation, 1f);
            ZombieWar.Skills.SkillArsenal.Instance?.Shockwave(at, 0.5f, Station.RadiusFor(kind) * 2.2f, WorldSignal.ColorOf(kind), 0.55f);
            if (Bill.IsReady) Bill.Audio?.PlayCue(kind == StationKind.HealZone ? "sfx.pickup.health" : "sfx.station.complete", at, SfxPriority.High, 0.7f);
        }

        public void PlayHealField(Vector3 at, float radius, float seconds)
        {
            var art = StationArt.Instance;
            if (art == null || art.healFieldFx == null) return;
            FxPool.PlayFor(art.healFieldFx, at + Vector3.up * 0.05f, art.healFieldFx.transform.localRotation,
                           radius / Mathf.Max(0.1f, art.healFieldNativeRadius), seconds);
        }

        float _healTickFxAt;

        public void PlayHealTick(Vector3 at)
        {
            var art = StationArt.Instance;
            if (art == null || art.healTickFx == null || Time.time < _healTickFxAt) return;
            _healTickFxAt = Time.time + 0.9f;
            FxPool.Play(art.healTickFx, at + Vector3.up * 0.9f, art.healTickFx.transform.localRotation, 1f);
        }

        void Release(long id)
        {
            if (_live.TryGetValue(id, out var st) && st != null)
            {
                // A boss whose anchor leaves the ring must not be orphaned.
                if (StationRegistry.IsBossAlive(id)) DespawnBossFor(id);
                st.Release();
                Destroy(st.gameObject);
            }
            _live.Remove(id);
        }

        void ReleaseAllStations()
        {
            _bossAnchors.Clear();
            _scratch.Clear();
            foreach (var kv in _live) _scratch.Add(kv.Key);
            for (int i = 0; i < _scratch.Count; i++) Release(_scratch[i]);
            _live.Clear();
        }

        // ───────────────────────────────────────────────────────── rewards

        /// <summary>Signal Relay's reward: a 1-of-3 card offer, through the existing level-up path.</summary>
        public void GrantCardOffer()
        {
            var overlays = FindFirstObjectByType<RunOverlays>();
            if (overlays != null) overlays.ShowLevelUp();
        }

        // ───────────────────────────────────────────────────────── boss beacon

        /// <summary>
        /// Spawns the beacon's boss. Returns TRUE only when a boss actually exists in the world.
        ///
        /// It used to return void, and every failure was silent: the beacon armed, claimed the single
        /// encounter slot, marked itself finished — and no boss appeared. Measured in play as
        /// `bossAlive=False, zombieCount=0` on an Active beacon. Each failure now says which step
        /// failed, because "nothing happened" is the hardest bug to report.
        /// </summary>
        public bool SpawnBossFor(Station station)
        {
            if (bossRoster == null || bossRoster.Length == 0)
            {
                Debug.LogError("[StationDirector] Boss Beacon has no boss roster assigned.");
                return false;
            }

            var spawner = FindFirstObjectByType<ZombieSpawner>();
            if (spawner == null)
            {
                Debug.LogError("[StationDirector] No ZombieSpawner in the scene — a beacon cannot deliver.");
                return false;
            }

            long id = station.Anchor.id;
            int pick = Mathf.Abs(station.Anchor.position.GetHashCode()) % bossRoster.Length;
            // Walk the roster from the hashed pick until one boss actually PLACES.
            //
            // Measured, not assumed: ZD_CactusBoss placed at 20.9 m and ZD_MoleRatKing at 15.1 m,
            // while ZD_SkeletonGiant failed every attempt — it is simply too large for the arena's
            // placement constraints (12 attempts + 12 authored points, all rejected). Failing the
            // whole beacon because one roster entry does not fit would make the feature unreliable
            // for a reason the player can never see.
            //
            // The earlier "pool not ready" message was wrong: the pool was fine, placement was not.
            ZombieBase boss = null;
            ZombieData chosen = null;
            for (int i = 0; i < bossRoster.Length && boss == null; i++)
            {
                var data = bossRoster[(pick + i) % bossRoster.Length];
                if (data == null) continue;

                spawner.EnsureRegistered(data, 1);
                spawner.BeginBatch();
                boss = spawner.Spawn(data, ZombieSpawner.SpawnBand.Normal)
                    ?? spawner.Spawn(data, ZombieSpawner.SpawnBand.Recovery);
                if (boss != null) chosen = data;
            }

            if (boss == null)
            {
                Debug.LogError("[StationDirector] No boss in the roster could be PLACED — every band " +
                               "was obstructed for every entry. The beacon stays unspent and retries.");
                return false;
            }
            Debug.Log($"[StationDirector] Boss Beacon spawned '{chosen.name}'.");

            // DO NOT reposition the boss.
            //
            // The owner reported the boss "stands at the column and does nothing". That was this line:
            // ZombieSpawner.Spawn already places the enemy in its ring band AROUND THE PLAYER and
            // initialises its steering there. Teleporting it to the pillar afterwards both parked it
            // at the column and left its state anchored to the original point.
            //
            // Spawning around the player is also the behaviour a chasing boss actually wants, so the
            // fix is to let the spawner own placement — which is its job.
            boss.IsBeaconOwned = true;      // exempt from the M7.4a leash
            if (Bill.IsReady) Bill.Audio?.PlayCue("sfx.station.beacon", SfxPriority.High, 0.8f);   // the boss is coming
            ZombieWar.UI.UIFeedback.Haptic(ZombieWar.UI.UIFeedback.Buzz.Medium);
            StationRegistry.SetBossAlive(id, true);
            _bossAnchors[boss] = id;
            return true;
        }

        void DespawnBossFor(long anchorId)
        {
            // The registry flag is what lets us know an orphan is possible at all.
            StationRegistry.SetBossAlive(anchorId, false);
            StationRegistry.ReleaseEncounter(anchorId);
        }

        /// <summary>Called when a beacon boss dies: pays the reward and frees the encounter slot.</summary>
        public void NotifyBossKilled(long anchorId, Vector3 at)
        {
            StationRegistry.SetBossAlive(anchorId, false);
            StationRegistry.SetStatus(anchorId, StationRegistry.Status.Completed, Time.time);
            StationRegistry.ReleaseEncounter(anchorId);
            PickupManager.Instance?.DropReward(at, bossRewardCoin, bossRewardGem);   // pays on death only
            PickupManager.Instance?.SpawnChest(at);   // A7: a boss always drops a chest (evolutions come from chests)
            ReportCompleted(StationKind.BossBeacon);
        }

        /// <summary>
        /// The single place a finished station is announced. Relays and beacons are objectives, so
        /// they raise threat - progress costs safety (GDD §15). A Supply Cache is a purchase, not an
        /// objective, and does not.
        /// </summary>
        public static void ReportCompleted(StationKind kind)
        {
            // A Supply Drop is loot and a Heal Zone is relief: neither is an objective either (A9).
            if (kind == StationKind.SignalRelay || kind == StationKind.BossBeacon) Threat.ThreatDirector.ReportObjectiveCompleted();
            if (Bill.IsReady) Bill.Events.Fire(new StationCompletedEvent(kind));
        }
    }

    /// <summary>A station paid out: a relay held, a cache bought, or a beacon boss killed.</summary>
    public readonly struct StationCompletedEvent : IEvent
    {
        public readonly StationKind Kind;
        public StationCompletedEvent(StationKind kind) { Kind = kind; }
    }
}
