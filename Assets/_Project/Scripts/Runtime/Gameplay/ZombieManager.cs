using System.Collections.Generic;
using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    // Central authority for the 3-tier distance gating (see GAMEPLAY_DESIGN.md mục 4) - zombies
    // never decide their own tier or run their own cheap-movement Update(); this single component
    // re-evaluates everyone on a throttled interval instead of N zombies checking distance every frame.
    //
    // It also owns the PRESSURE SNAPSHOT the WaveDirector spawns against. That lives here rather
    // than in the director because this is already the one place that walks every zombie on a
    // throttle - counting who is on camera costs one extra branch in a loop we were running anyway,
    // instead of a second full sweep (and a second camera lookup) somewhere else.
    public class ZombieManager : MonoBehaviour
    {
        [SerializeField] private float fullTierRadius = 20f;
        [SerializeField] private float cheapTierRadius = 60f;
        [SerializeField] private float tierReevaluateInterval = 0.25f;

        [Header("Pressure snapshot (read by WaveDirector)")]
        [Tooltip("How often visible/reserve counts are recounted. Faster than tiering because the " +
                 "spawn loop reacts to it.")]
        [SerializeField] private float pressureInterval = 0.15f;
        [Tooltip("Viewport padding, in screen fractions, added around the camera rect when deciding " +
                 "'on screen'. Catches enemies half-way into frame.")]
        [SerializeField] private float visibleViewportMargin = 0.08f;
        [Tooltip("An on-camera enemy further than this from the player is scenery, not pressure - " +
                 "it does not count toward the visible floor.")]
        [SerializeField] private float visiblePressureRadius = 26f;
        [Tooltip("Off-camera enemies that would reach the pressure radius within this many seconds " +
                 "at their own move speed count as reserve.")]
        [SerializeField] private float reserveLeadSeconds = 3f;

        private static readonly List<ZombieBase> _zombies = new();
        private float _reevaluateTimer;
        private float _pressureTimer;
        private Camera _camera;

        // Single source of truth for "how many zombies are alive right now". A zombie leaves this
        // list the moment it is returned to the pool (OnDisable -> Unregister), so the wave director
        // can poll this to know when a wave is cleared - no separate bookkeeping needed.
        public static int AliveCount => _zombies.Count;

        /// <summary>Alive enemies currently on camera AND close enough to be immediate pressure.</summary>
        public static int VisibleCount { get; private set; }

        /// <summary>Alive enemies off camera that are roughly 1-3 seconds from entering it.</summary>
        public static int ReserveCount { get; private set; }

        /// <summary>False until a snapshot has been taken against a live camera + player. The wave
        /// director must not treat "no data" as "screen is empty", or it would spawn on recovery
        /// settings for the whole run.</summary>
        public static bool HasPressure { get; private set; }

        public static void Register(ZombieBase zombie)
        {
            if (!_zombies.Contains(zombie)) _zombies.Add(zombie);
        }

        public static void Unregister(ZombieBase zombie)
        {
            _zombies.Remove(zombie);
        }

        private void OnEnable()
        {
            var bus = Bill.Events;
            if (bus == null) return;
            bus.Subscribe<PlayerDiedEvent>(OnPlayerDied);
            bus.Subscribe<GameOverEvent>(OnGameOver);
        }

        private void OnDisable()
        {
            InvalidatePressure();
            var bus = Bill.Events;
            if (bus == null) return;
            bus.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
            bus.Unsubscribe<GameOverEvent>(OnGameOver);
        }

        // The corpse is not a target: everyone drops to Idle the moment the player dies.
        // (PlayerMovement disables itself on death -> Instance goes null -> Update() above
        // stops ticking cheap movement/tiers too.)
        private void OnPlayerDied(PlayerDiedEvent e)
        {
            for (int i = 0; i < _zombies.Count; i++)
                _zombies[i].OnPlayerLost();
        }

        // Once the lose screen takes over, the field is cleared - every zombie goes straight
        // back to the pool (Return -> OnDisable -> Unregister prunes the list as we walk it).
        private void OnGameOver(GameOverEvent e)
        {
            for (int i = _zombies.Count - 1; i >= 0; i--)
            {
                var zombie = _zombies[i];
                if (zombie != null) Bill.Pool?.Return(zombie.gameObject);
            }
        }

        private void Update()
        {
            _attackSlotCap = Mathf.Max(1, maxSimultaneousAttackers);

            var player = PlayerMovement.Instance;
            if (player == null)
            {
                InvalidatePressure();
                return;
            }

            Vector3 playerPosition = player.transform.position;
            TickCheapMovement(playerPosition);

            _pressureTimer -= Time.deltaTime;
            if (_pressureTimer <= 0f)
            {
                _pressureTimer = pressureInterval;
                RecomputePressure(playerPosition);
            }

            _reevaluateTimer -= Time.deltaTime;
            if (_reevaluateTimer > 0f) return;
            _reevaluateTimer = tierReevaluateInterval;

            ReevaluateTiers(playerPosition);
            RecycleFarBehind(playerPosition);
        }

        // ── M7.4a: the leash ────────────────────────────────────────────────────────────────
        //
        // The open-world failure the owner hit: run in one direction and the horde becomes a tail
        // strung out behind you. Nothing recycled them, so they stayed alive forever — holding the
        // wave gate open, never returning to the pool, and burning frame time chasing someone they
        // could never reach.
        //
        // Enemies left far behind are returned to the pool. The threat director then re-spawns
        // pressure around the player's CURRENT position, so the crowd follows rather than trails.

        [Header("Leash (M7.4a) — TUNING")]
        [Tooltip("Planar metres behind the player before an enemy is recycled. Chosen well beyond " +
                 "the ~22 m spawn band and far outside camera view, so recycling is never visible.")]
        [SerializeField] private float leashDistance = 55f;
        [Tooltip("Extra margin on the visibility test. An enemy vanishing on screen is a worse bug " +
                 "than the tail being fixed, so anything the camera can see is exempt.")]
        [SerializeField] private float visibilityMargin = 6f;
        [Tooltip("Most enemies recycled per pass, so a big tail drains smoothly instead of popping.")]
        [SerializeField] private int maxRecyclesPerPass = 4;

        // ── M7.4b: the attacker cap ──────────────────────────────────────────────────────
        //
        // Measured cause of "húc một cái là chết luôn": nothing one-shots the player — the highest
        // single hit in the game is 30 against 100 HP — but eight enemies in contact stack to roughly
        // 70-90 DPS, and Health.cs has no invulnerability, grace period or damage cooldown at all.
        // A pack landing together deletes the player in about a second with no readable moment.
        //
        // The fix is the standard horde-game answer: only N enemies may hold an ATTACKING slot. The
        // rest still surround, press in and face the player — the screen still looks like a horde —
        // but incoming damage is bounded and the player can see who is committing.
        //
        // Chosen over a post-hit invulnerability window because that weakens every individual threat
        // and feels mushy; this bounds the crowd without making any single enemy less dangerous.

        [Header("Attacker cap (M7.4b) — TUNING")]
        [Tooltip("Enemies that may swing at once. At 5-14 DPS each this bounds incoming damage to " +
                 "roughly 20-56 DPS against 100 HP: overwhelming, but survivable long enough to read.")]
        [SerializeField] private int maxSimultaneousAttackers = 4;

        static int _attackSlotsInUse;
        static int _attackSlotCap = 4;

        public static int AttackSlotsInUse => _attackSlotsInUse;
        public static int AttackSlotCap => _attackSlotCap;

        /// <summary>Run-scoped: a run must never start with slots leaked from the last one.</summary>
        public static void ResetAttackSlots() => _attackSlotsInUse = 0;

        /// <summary>
        /// Claims an attacking slot. Bosses and elites ALWAYS get one — an authored encounter that
        /// cannot commit its attack is a bug, not balance.
        /// </summary>
        public static bool TryClaimAttackSlot(ZombieBase zombie)
        {
            if (zombie == null) return false;
            if (zombie.Data != null && zombie.Data.isElite) return true;   // never denied
            if (zombie.IsBeaconOwned) return true;                         // never denied

            if (_attackSlotsInUse >= _attackSlotCap) return false;
            _attackSlotsInUse++;
            return true;
        }

        public static void ReleaseAttackSlot(ZombieBase zombie)
        {
            if (zombie == null) return;
            if (zombie.Data != null && zombie.Data.isElite) return;        // never took one
            if (zombie.IsBeaconOwned) return;
            _attackSlotsInUse = Mathf.Max(0, _attackSlotsInUse - 1);
        }

        /// <summary>Enemies recycled this run. Diagnostics for the play-test; reset with the run.</summary>
        public static int RecycledCount { get; private set; }
        public static void ResetRecycleCounter() => RecycledCount = 0;

        private static readonly Plane[] FrustumPlanes = new Plane[6];

        private void RecycleFarBehind(Vector3 playerPosition)
        {
            if (leashDistance <= 0f) return;

            var cam = Camera.main;
            bool haveCam = cam != null;
            if (haveCam) GeometryUtility.CalculateFrustumPlanes(cam, FrustumPlanes);

            float leashSqr = leashDistance * leashDistance;
            int recycled = 0;

            for (int i = _zombies.Count - 1; i >= 0 && recycled < maxRecyclesPerPass; i--)
            {
                var zombie = _zombies[i];
                if (zombie == null) continue;

                // Never recycle a boss or a beacon-spawned encounter: those are authored events, and
                // one vanishing mid-fight would be indistinguishable from a bug.
                if (zombie.Data != null && zombie.Data.isElite) continue;
                if (zombie.IsBeaconOwned) continue;

                Vector3 p = zombie.transform.position;
                float dx = p.x - playerPosition.x, dz = p.z - playerPosition.z;
                if (dx * dx + dz * dz < leashSqr) continue;          // still in play

                // Visibility guard: anything the camera can see stays, whatever the distance.
                if (haveCam)
                {
                    var bounds = new Bounds(p, Vector3.one * (2f + visibilityMargin));
                    if (GeometryUtility.TestPlanesAABB(FrustumPlanes, bounds)) continue;
                }

                Bill.Pool?.Return(zombie.gameObject);                // OnDisable unregisters + clears statuses
                RecycledCount++;
                recycled++;
            }
        }

        private void TickCheapMovement(Vector3 playerPosition)
        {
            for (int i = 0; i < _zombies.Count; i++)
            {
                var zombie = _zombies[i];
                if (zombie.Tier == ZombieTier.Cheap) zombie.CheapTick(playerPosition);
            }
        }

        // Reverse iteration: RecoverIfStranded may pool-return a stranded zombie, which unregisters
        // it (OnDisable) and mutates the list mid-walk.
        private void ReevaluateTiers(Vector3 playerPosition)
        {
            for (int i = _zombies.Count - 1; i >= 0; i--)
            {
                var zombie = _zombies[i];
                float distance = Vector3.Distance(zombie.transform.position, playerPosition);

                if (distance <= fullTierRadius) zombie.SetTier(ZombieTier.Full);
                // A blocked Cheap zombie that asked for real pathfinding keeps Full tier inside the
                // cheap radius, so it can walk around the obstacle instead of grinding against it.
                else if (distance <= cheapTierRadius)
                    zombie.SetTier(zombie.NeedsFullTier ? ZombieTier.Full : ZombieTier.Cheap);
                else zombie.SetTier(ZombieTier.Inactive);

                zombie.RecoverIfStranded();
            }
        }

        // One pass, no allocation, no LINQ, no FindObjectOfType - the camera is cached and only
        // re-resolved when the previous one is gone (scene reload, camera rebuilt).
        private void RecomputePressure(Vector3 playerPosition)
        {
            var cam = ResolveCamera();
            if (cam == null)
            {
                InvalidatePressure();
                return;
            }

            int visible = 0;
            int reserve = 0;

            for (int i = 0; i < _zombies.Count; i++)
            {
                var zombie = _zombies[i];
                if (zombie == null) continue;

                Vector3 position = zombie.transform.position;
                float distance = Vector3.Distance(position, playerPosition);

                if (distance <= visiblePressureRadius && IsOnCamera(cam, position))
                {
                    visible++;
                    continue;
                }

                // Reserve = "about to become visible". Measured in seconds at the enemy's own speed
                // rather than a flat radius, so a runner counts as reserve from further out than a
                // walker does - which is exactly when it stops being reserve and starts being alive
                // on screen.
                float speed = zombie.Data != null ? Mathf.Max(0.1f, zombie.Data.moveSpeed) : 3f;
                if ((distance - visiblePressureRadius) / speed <= reserveLeadSeconds) reserve++;
            }

            VisibleCount = visible;
            ReserveCount = reserve;
            HasPressure = true;
        }

        private bool IsOnCamera(Camera cam, Vector3 world)
        {
            Vector3 viewport = cam.WorldToViewportPoint(world);
            if (viewport.z <= 0f) return false;
            float margin = visibleViewportMargin;
            return viewport.x >= -margin && viewport.x <= 1f + margin &&
                   viewport.y >= -margin && viewport.y <= 1f + margin;
        }

        private Camera ResolveCamera()
        {
            if (_camera != null && _camera.isActiveAndEnabled) return _camera;
            _camera = Camera.main;
            return _camera;
        }

        private static void InvalidatePressure()
        {
            HasPressure = false;
            VisibleCount = 0;
            ReserveCount = 0;
        }
    }
}
