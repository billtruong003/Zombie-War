using System.Collections.Generic;
using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    // Central authority for the 3-tier distance gating (see GAMEPLAY_DESIGN.md mục 4) - zombies
    // never decide their own tier or run their own cheap-movement Update(); this single component
    // re-evaluates everyone on a throttled interval instead of N zombies checking distance every frame.
    public class ZombieManager : MonoBehaviour
    {
        [SerializeField] private float fullTierRadius = 20f;
        [SerializeField] private float cheapTierRadius = 60f;
        [SerializeField] private float tierReevaluateInterval = 0.25f;

        private static readonly List<ZombieBase> _zombies = new();
        private float _reevaluateTimer;

        // Single source of truth for "how many zombies are alive right now". A zombie leaves this
        // list the moment it is returned to the pool (OnDisable -> Unregister), so the threat director
        // can hold its crowd ceiling against it - no separate bookkeeping needed.
        public static int AliveCount => _zombies.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _zombies.Clear();

        /// <summary>Every enemy currently in play. The one list target queries read (no physics sweep).</summary>
        public static IReadOnlyList<ZombieBase> Alive => _zombies;

        // O(1) both ways: each enemy remembers its slot (spawns/deaths/recycles hit this every frame
        // of a horde; Contains/Remove scanned up to 300 entries each time).
        public static void Register(ZombieBase zombie)
        {
            if (zombie == null || zombie.RegistryIndex >= 0) return;
            zombie.RegistryIndex = _zombies.Count;
            _zombies.Add(zombie);
        }

        public static void Unregister(ZombieBase zombie)
        {
            if (zombie == null) return;
            int i = zombie.RegistryIndex;
            if (i < 0 || i >= _zombies.Count || !ReferenceEquals(_zombies[i], zombie)) return;
            int last = _zombies.Count - 1;
            if (i != last) { _zombies[i] = _zombies[last]; _zombies[i].RegistryIndex = i; }
            _zombies.RemoveAt(last);
            zombie.RegistryIndex = -1;
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
            _pursuitNear = pursuitNearDistance;
            _pursuitFar = Mathf.Max(pursuitNearDistance + 0.1f, pursuitFarDistance);
            _pursuitMax = Mathf.Max(1f, pursuitMaxMultiplier);

            var player = PlayerMovement.Instance;
            _hasPursuitTarget = player != null;
            if (player == null) return;

            Vector3 playerPosition = player.transform.position;
            _pursuitTarget = playerPosition;
            SamplePlayerVelocity(playerPosition);
            TickCheapMovement(playerPosition);

            _reevaluateTimer -= Time.deltaTime;
            if (_reevaluateTimer > 0f) return;
            _reevaluateTimer = tierReevaluateInterval;

            ReevaluateTiers(playerPosition);
            RecycleFarBehind(playerPosition);
        }

        // ── M7.4a: the leash ────────────────────────────────────────────────────────────────
        //
        // The open-world failure the owner hit: run in one direction and the horde becomes a tail
        // strung out behind you. Nothing recycled them, so they stayed alive forever — filling the
        // crowd ceiling, never returning to the pool, and burning frame time chasing someone they
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

        [Header("Tail recycle (M8) — TUNING")]
        [Tooltip("Off-screen enemies BEHIND a moving player and farther than this are recycled, so " +
                 "the director re-spawns them ahead. Measured 2026-09-26: a player circling at base " +
                 "speed dragged a full 120-enemy crowd behind them for five minutes; because the crowd " +
                 "was full, nothing new ever spawned in front, and the run stalled with no kills.")]
        [SerializeField] private float tailRecycleDistance = 16f;
        [Tooltip("Player speed (m/s) above which the tail rule applies. A standing player has no " +
                 "'behind'.")]
        [SerializeField] private float tailMinPlayerSpeed = 1.5f;
        [Tooltip("How far behind counts: the enemy's bearing must be at least this far from the " +
                 "movement direction (cosine; -0.3 is about 107 degrees).")]
        [SerializeField, Range(-1f, 0f)] private float tailBehindCosine = -0.3f;

        Vector3 _lastPlayerPosition;
        Vector3 _playerVelocity;
        bool _haveLastPlayerPosition;

        // Smoothed so one hitch frame does not flip "behind"; jumps faster than any run speed are a
        // floating-origin rebase, not motion, and are ignored.
        void SamplePlayerVelocity(Vector3 position)
        {
            float dt = Time.deltaTime;
            if (_haveLastPlayerPosition && dt > 0f)
            {
                Vector3 v = (position - _lastPlayerPosition) / dt;
                v.y = 0f;
                if (v.sqrMagnitude < 30f * 30f)
                    _playerVelocity = Vector3.Lerp(_playerVelocity, v, 1f - Mathf.Exp(-6f * dt));
            }
            _lastPlayerPosition = position;
            _haveLastPlayerPosition = true;
        }

        /// <summary>True when an enemy at <paramref name="offset"/> from the player is in the tail:
        /// far enough, and behind a player who is actually moving.</summary>
        public static bool IsInTail(Vector3 offset, Vector3 playerVelocity, float minDistance,
                                    float minSpeed, float behindCosine)
        {
            offset.y = 0f; playerVelocity.y = 0f;
            float dist = offset.magnitude;
            float speed = playerVelocity.magnitude;
            if (dist < minDistance || speed < minSpeed) return false;
            return Vector3.Dot(offset / dist, playerVelocity / speed) <= behindCosine;
        }

        // ── M8: pursuit ─────────────────────────────────────────────────────────────────────
        //
        // Measured 2026-09-26: a player running a wide circle outpaces every enemy (5 m/s against
        // 2-3 m/s). The crowd became a 100-strong tail that never caught up: the player took no
        // damage, but also made no kills and stalled at level 4 for five minutes. Enemies far from
        // the player now close the gap faster, so the tail is pulled back into the fight. Near the
        // player (where they are on screen and in reach) they move at their authored speed.

        [Header("Pursuit (M8) — TUNING")]
        [Tooltip("Within this distance an enemy moves at its authored speed.")]
        [SerializeField] private float pursuitNearDistance = 10f;
        [Tooltip("At or beyond this distance an enemy moves at the full pursuit multiplier.")]
        [SerializeField] private float pursuitFarDistance = 25f;
        [Tooltip("Speed multiplier for far enemies. Lifts the common crowd (2.4 m/s and up) above the " +
                 "player's base 5 m/s, so a runner cannot leave it behind; slow plants stay slow.")]
        [SerializeField] private float pursuitMaxMultiplier = 2.2f;

        static float _pursuitNear = 10f, _pursuitFar = 25f, _pursuitMax = 2.2f;
        static bool _hasPursuitTarget;
        static Vector3 _pursuitTarget;

        /// <summary>Speed multiplier for an enemy at <paramref name="position"/>: 1 near the player,
        /// rising to the pursuit maximum far away. 1 when there is no player (tests, menus).</summary>
        public static float PursuitMultiplier(Vector3 position)
        {
            if (!_hasPursuitTarget) return 1f;
            float dx = position.x - _pursuitTarget.x, dz = position.z - _pursuitTarget.z;
            return PursuitMultiplierAt(Mathf.Sqrt(dx * dx + dz * dz), _pursuitNear, _pursuitFar, _pursuitMax);
        }

        public static float PursuitMultiplierAt(float distance, float near, float far, float max) =>
            Mathf.Lerp(1f, Mathf.Max(1f, max), Mathf.InverseLerp(near, Mathf.Max(near + 0.1f, far), distance));

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
                bool beyondLeash = dx * dx + dz * dz >= leashSqr;
                bool inTail = !beyondLeash && IsInTail(p - playerPosition, _playerVelocity,
                    tailRecycleDistance, tailMinPlayerSpeed, tailBehindCosine);
                if (!beyondLeash && !inTail) continue;                  // still in play

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
    }
}
