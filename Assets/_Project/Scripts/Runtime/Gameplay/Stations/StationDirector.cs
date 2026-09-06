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

        readonly Dictionary<long, Station> _live = new(8);
        // Which beacon spawned which boss, so a kill can be routed back to the right anchor.
        readonly Dictionary<ZombieBase, long> _bossAnchors = new(4);
        readonly List<long> _scratch = new(8);
        static readonly StationAnchors.Anchor[] AnchorBuffer = new StationAnchors.Anchor[32];

        Transform _player;
        int _worldSeed = 20260816;

        public int LiveStationCount => _live.Count;

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
                if (StationRegistry.StatusOf(a.id, Time.time) == StationRegistry.Status.Destroyed) continue;
                Spawn(a);
            }

            // 2. tick the live ones, and release those the player has left behind
            _scratch.Clear();
            foreach (var kv in _live)
            {
                var st = kv.Value;
                if (st == null) { _scratch.Add(kv.Key); continue; }

                float sqr = (st.transform.position - p).sqrMagnitude;
                if (sqr > releaseDistance * releaseDistance) { _scratch.Add(kv.Key); continue; }

                st.Tick(dt, p);
            }
            for (int i = 0; i < _scratch.Count; i++) Release(_scratch[i]);
        }

        void Spawn(StationAnchors.Anchor a)
        {
            var go = new GameObject($"Station_{a.kind}_{a.id}");
            go.transform.position = a.position;

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
            if (bodyPrefab != null)
            {
                var body = Instantiate(bodyPrefab, go.transform);
                body.transform.localPosition = Vector3.zero;
            }

            var station = go.AddComponent<Station>();
            station.Bind(a, signal);
            _live[a.id] = station;
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

        /// <summary>Supply Cache's reward: pickups on the floor, which the player must now walk to.</summary>
        public void GrantSupplies(Vector3 at)
        {
            var pm = PickupManager.Instance;
            if (pm == null) return;
            pm.DropReward(at);
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
            PickupManager.Instance?.DropReward(at);   // no free chest: it pays on death only
        }
    }
}
