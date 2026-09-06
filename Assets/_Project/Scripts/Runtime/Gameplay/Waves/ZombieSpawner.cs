using System.Collections.Generic;
using UnityEngine;
using BillGameCore;

namespace ZombieWar
{
    // Turns a ZombieData into a live, pooled zombie placed on a clear spot on the gameplay plane. Owns the
    // pool-key convention (mirrors BombThrower: "zombie_" + prefab instance id) and the placement
    // policy. The spawned prefab already carries its own ZombieData, so it self-configures
    // (Health.Configure + register) on OnEnable - the spawner only decides WHERE, never WHAT stats.
    //
    // Placement is camera-relative, not map-relative. A horde has to arrive from every side of the
    // player and it must never blink into existence on screen, so candidates are drawn from a ring
    // BAND around the player, split into sectors so one batch fans out instead of stacking, and
    // rejected outright if they land inside the camera rect. Two bands exist: the normal band sits
    // well outside the view, and a tighter recovery band hugs it for when the screen has emptied and
    // the next group needs to be in frame in a second, not five.
    public class ZombieSpawner : MonoBehaviour
    {
        /// <summary>Which distance band a spawn is drawn from. Recovery sits closer to the camera so
        /// enemies reach the screen fast; it is only used while the director is recovering pressure.</summary>
        public enum SpawnBand
        {
            Normal,
            Recovery
        }

        [Header("Normal spawn band (radius from player, well outside the camera)")]
        [SerializeField] private float minSpawnRadius = 12f;
        [SerializeField] private float maxSpawnRadius = 22f;

        [Header("Inner recovery band (just outside the camera)")]
        [SerializeField] private float recoveryMinSpawnRadius = 8f;
        [SerializeField] private float recoveryMaxSpawnRadius = 13f;

        [Header("Placement validation")]
        [SerializeField, Min(1)] private int maxPlacementAttempts = 32;
        [SerializeField, Min(0f)] private float obstacleClearance = 0.12f;

        [Header("Camera pop-in rejection")]
        [Tooltip("Off - authored spawn points are used verbatim and no camera test runs. Kept as an " +
                 "escape hatch for maps that place enemies deliberately.")]
        [SerializeField] private bool useCameraBands = true;
        [Tooltip("Viewport padding around the camera rect that a spawn must stay outside of.")]
        [SerializeField] private float cameraRejectMargin = 0.06f;
        [Tooltip("Height above the spawn point also checked against the camera, so a zombie whose " +
                 "feet are below the frame but whose head is not still counts as visible.")]
        [SerializeField] private float visibilityProbeHeight = 1.8f;

        [Header("Batch distribution")]
        [Tooltip("The ring is divided into this many sectors; consecutive spawns in one batch step " +
                 "across them so a group arrives spread around the player.")]
        [SerializeField, Min(2)] private int sectorCount = 8;
        [Tooltip("Minimum distance between two members of the same batch.")]
        [SerializeField, Min(0f)] private float batchSpacing = 1.6f;

        [Header("Fixed spawn points (fallback when the band finds nothing)")]
        [SerializeField] private Transform[] spawnPoints;

        [Header("Pool warmup")]
        [Tooltip("Floor for every type. WaveDirector raises this per type to the wave set's peak " +
                 "simultaneous demand.")]
        [SerializeField] private int warmCountPerType = 8;
        [Tooltip("Ceiling on warmed instances per type, so a badly authored wave cannot blow out " +
                 "load time and memory.")]
        [SerializeField] private int maxWarmPerType = 96;

        private const float FailureLogInterval = 2f;
        private const int MaxBatchMemory = 64;

        // How far, and in how many steps, a candidate may be pushed radially outward to clear the
        // camera rect before the attempt is abandoned.
        private const float CameraPushStep = 2f;
        private const int CameraPushSteps = 8;
        private const float NormalBandPushSlack = 10f;

        private readonly HashSet<string> _registered = new();

        // Batch state. Fixed-size and reused - a batch must not allocate.
        private readonly Vector3[] _batchPositions = new Vector3[MaxBatchMemory];
        private int _batchCount;
        private int _sectorCursor;
        private int _sectorStride = 3;

        private int _obstacleMask;
        private Camera _camera;
        private float _nextFailureLogTime;
        private int _failuresSinceLog;

        public static string KeyFor(ZombieData data) => "zombie_" + data.prefab.GetInstanceID();

        private void Awake()
        {
            int groundLayer = LayerMask.NameToLayer("WalkableGround");
            _obstacleMask = groundLayer >= 0 ? ~(1 << groundLayer) : Physics.AllLayers;

            // A stride co-prime with the sector count visits every sector exactly once before
            // repeating, so an 8-member batch covers all 8 sectors instead of clustering.
            _sectorStride = PickCoprimeStride(sectorCount);
        }

        // Idempotent - safe to call every wave. Registers + warms the pool the first time only.
        // expectedPeak is the most instances of this type that can be alive at once across the run;
        // the pool is warmed to that so a 60-strong wave never falls back to Instantiate mid-fight.
        /// <returns>True if this call actually registered and warmed the pool - the caller can use
        /// that to spread the Instantiate cost of several types across frames.</returns>
        public bool EnsureRegistered(ZombieData data, int expectedPeak = 0)
        {
            if (data == null || data.prefab == null) return false;
            string key = KeyFor(data);
            if (_registered.Contains(key)) return false;

            var pool = Bill.Pool;
            if (pool == null) return false;

            int warm = Mathf.Clamp(
                expectedPeak > 0 ? expectedPeak : warmCountPerType,
                Mathf.Max(1, warmCountPerType),
                Mathf.Max(1, maxWarmPerType));

            pool.Register(key, data.prefab, warm);
            _registered.Add(key);
            return true;
        }

        /// <summary>Starts a new spawn batch: re-seeds the sector walk and forgets the previous
        /// batch's positions, so spacing is enforced within a group but not across groups.</summary>
        public void BeginBatch()
        {
            _batchCount = 0;
            _sectorCursor = Random.Range(0, Mathf.Max(2, sectorCount));
        }

        /// <summary>
        /// Optional point to place enemies around instead of the player's current position. The
        /// threat director sets this to a lead point ahead of the player's motion. Null = player.
        /// </summary>
        public Vector3? SpawnFocusOverride { get; set; }

        public ZombieBase Spawn(ZombieData data) => Spawn(data, SpawnBand.Normal);

        public ZombieBase Spawn(ZombieData data, SpawnBand band)
        {
            if (data == null || data.prefab == null) return null;

            var pool = Bill.Pool;
            if (pool == null)
            {
                Debug.LogWarning("[ZombieSpawner] Bill.Pool not ready - skipping spawn.");
                return null;
            }

            EnsureRegistered(data);

            if (!TryGetSpawnPosition(data, band, out Vector3 pos))
            {
                ReportPlacementFailure(data, band);
                return null;
            }

            RememberBatchPosition(pos);
            var go = pool.Spawn(KeyFor(data), pos, Quaternion.identity);
            return go != null ? go.GetComponent<ZombieBase>() : null;
        }

        private bool TryGetSpawnPosition(ZombieData data, SpawnBand band, out Vector3 result)
        {
            GetAgentDimensions(data, out float radius, out float height);
            var player = PlayerMovement.Instance;
            Vector3 target = player != null ? player.transform.position : transform.position;

            // M7.4a — spawn around where the player is GOING, not where they were.
            //
            // Measured: running in one direction produced "ahead 0 | behind 5". The ring band is
            // 12-22 m, and at 5 m/s the player crosses that in ~3 s, so anything placed around their
            // current position is behind them almost immediately — the tail the owner described.
            // Leading the focus point makes the player run INTO pressure instead of away from it.
            if (SpawnFocusOverride.HasValue) target = SpawnFocusOverride.Value;

            if (!useCameraBands && spawnPoints != null && spawnPoints.Length > 0)
                return TryAuthoredPoints(target, radius, height, false, out result);

            GetBand(band, out float bandMin, out float bandMax);
            var cam = ResolveCamera();
            float pushLimit = band == SpawnBand.Normal
                ? maxSpawnRadius + NormalBandPushSlack
                : Mathf.Max(bandMax, maxSpawnRadius);

            for (int i = 0; i < maxPlacementAttempts; i++)
            {
                float angle = NextSectorAngle();
                float r = Random.Range(bandMin, bandMax);
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                if (!TryClearCamera(cam, target, direction, r, pushLimit, out Vector3 candidate)) continue;
                if (IsTooCloseToBatch(candidate)) continue;
                if (!IsClearAndReachable(candidate, target, radius, height, out result)) continue;
                // Kept from the NavMesh era: validation may still adjust the point, so the camera
                // test re-runs on the final position rather than the raw candidate.
                if (IsOnCamera(cam, result)) continue;
                if (IsTooCloseToBatch(result)) continue;

                return true;
            }

            // Band exhausted (tight arena, player cornered, heavy prop cover). Authored perimeter
            // points are the safety net so a wave can never wedge - they are still camera-tested.
            return TryAuthoredPoints(target, radius, height, true, out result);
        }

        private bool TryAuthoredPoints(
            Vector3 target, float radius, float height, bool rejectOnCamera, out Vector3 result)
        {
            result = default;
            if (spawnPoints == null || spawnPoints.Length == 0) return false;

            var cam = rejectOnCamera ? ResolveCamera() : null;
            int start = Random.Range(0, spawnPoints.Length);

            // Walk the whole set once from a random offset: bounded, allocation-free, and it does
            // not keep re-rolling the same blocked point the way random sampling did.
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                var t = spawnPoints[(start + i) % spawnPoints.Length];
                if (t == null) continue;
                if (cam != null && IsOnCamera(cam, t.position)) continue;
                if (IsTooCloseToBatch(t.position)) continue;
                if (!IsClearAndReachable(t.position, target, radius, height, out result)) continue;
                if (cam != null && IsOnCamera(cam, result)) continue;

                return true;
            }

            result = default;
            return false;
        }

        private void GetBand(SpawnBand band, out float bandMin, out float bandMax)
        {
            if (band == SpawnBand.Normal)
            {
                bandMin = Mathf.Max(0.5f, minSpawnRadius);
                bandMax = Mathf.Max(bandMin + 0.5f, maxSpawnRadius);
                return;
            }

            // The recovery band is clamped to sit strictly inside the normal one, so retuning the
            // normal band in the inspector can never leave recovery spawning further out than normal.
            bandMax = Mathf.Clamp(recoveryMaxSpawnRadius, 1f, Mathf.Max(1.5f, minSpawnRadius));
            bandMin = Mathf.Clamp(recoveryMinSpawnRadius, 0.5f, bandMax - 0.5f);
        }

        // Steps across sectors rather than picking a free angle, so consecutive members of a batch
        // cannot land on top of each other even before the spacing test runs.
        private float NextSectorAngle()
        {
            int sectors = Mathf.Max(2, sectorCount);
            _sectorCursor = (_sectorCursor + _sectorStride) % sectors;
            float sectorSize = Mathf.PI * 2f / sectors;
            return (_sectorCursor + Random.value) * sectorSize;
        }

        private static int PickCoprimeStride(int sectors)
        {
            sectors = Mathf.Max(2, sectors);
            for (int stride = sectors / 2 + 1; stride > 1; stride--)
                if (Gcd(stride, sectors) == 1) return stride;
            return 1;
        }

        private static int Gcd(int a, int b)
        {
            while (b != 0) (a, b) = (b, a % b);
            return a;
        }

        // Walks the candidate radially outward until it clears the camera rect.
        //
        // A fixed radius will not do. The gameplay camera is pitched down and pulled back, so it sees
        // roughly 13m ahead of the player but only ~8m behind: any single band is off-camera behind
        // the player and on-camera in front of them. Rejecting instead of pushing would quietly turn
        // "spawn in 8 sectors" into "spawn in the 3 sectors behind the player", which is the serial
        // line the batching is meant to break up.
        private bool TryClearCamera(
            Camera cam, Vector3 center, Vector3 direction, float radius, float maxRadius, out Vector3 candidate)
        {
            candidate = center + direction * radius;
            if (cam == null) return true;

            for (int step = 0; step < CameraPushSteps; step++)
            {
                if (!IsOnCamera(cam, candidate)) return true;

                radius += CameraPushStep;
                if (radius > maxRadius) return false;
                candidate = center + direction * radius;
            }

            return !IsOnCamera(cam, candidate);
        }

        private bool IsTooCloseToBatch(Vector3 candidate)
        {
            if (batchSpacing <= 0f) return false;
            float sqr = batchSpacing * batchSpacing;
            for (int i = 0; i < _batchCount; i++)
                if ((_batchPositions[i] - candidate).sqrMagnitude < sqr) return true;
            return false;
        }

        private void RememberBatchPosition(Vector3 position)
        {
            if (_batchCount >= MaxBatchMemory) return;
            _batchPositions[_batchCount++] = position;
        }

        private bool IsOnCamera(Camera cam, Vector3 world)
        {
            if (cam == null) return false;
            return IsPointOnCamera(cam, world) ||
                   IsPointOnCamera(cam, world + Vector3.up * Mathf.Max(0f, visibilityProbeHeight));
        }

        private bool IsPointOnCamera(Camera cam, Vector3 world)
        {
            Vector3 viewport = cam.WorldToViewportPoint(world);
            if (viewport.z <= 0f) return false;
            float m = cameraRejectMargin;
            return viewport.x >= -m && viewport.x <= 1f + m &&
                   viewport.y >= -m && viewport.y <= 1f + m;
        }

        private Camera ResolveCamera()
        {
            if (!useCameraBands) return null;
            if (_camera != null && _camera.isActiveAndEnabled) return _camera;
            _camera = Camera.main;
            return _camera;
        }

        // Throttled so a genuinely blocked arena produces one actionable line every couple of
        // seconds instead of a wall of identical warnings that hides everything else.
        private void ReportPlacementFailure(ZombieData data, SpawnBand band)
        {
            _failuresSinceLog++;
            if (Time.unscaledTime < _nextFailureLogTime) return;
            _nextFailureLogTime = Time.unscaledTime + FailureLogInterval;

            GetBand(band, out float bandMin, out float bandMax);
            Debug.LogWarning(
                $"[ZombieSpawner] {_failuresSinceLog} placement failure(s) in the last " +
                $"{FailureLogInterval}s (latest: {data.name}, {band} band {bandMin:0.#}-{bandMax:0.#}m, " +
                $"{maxPlacementAttempts} attempts + {(spawnPoints?.Length ?? 0)} authored points). " +
                "Arena may be too tight or too heavily obstructed for the current alive target.");
            _failuresSinceLog = 0;
        }

        /// <summary>
        /// Validates a spawn candidate on the flat gameplay plane.
        ///
        /// M4 removed three navigation queries from this method and did not replace them with an
        /// equivalent, because on the streamed world they no longer test anything real:
        ///
        /// - <c>NavMesh.SamplePosition(candidate)</c> snapped the point onto baked geometry. There is
        ///   no baked geometry now; the gameplay surface is one flat plane at Y=0, so the candidate is
        ///   already valid by construction and only needs projecting onto that plane.
        /// - <c>NavMesh.SamplePosition(target)</c> did the same for the player, who is on the same plane.
        /// - <c>NavMesh.CalculatePath</c> asked "can the enemy walk here from there". On an open plane
        ///   with no baked obstacles the answer is always yes, and the planar motor slides around the
        ///   few authored blockers that do exist rather than needing a path to avoid them.
        ///
        /// What remains is the check that still has teeth: the capsule overlap against real gameplay
        /// blockers, so an enemy never materialises inside one.
        /// </summary>
        private bool IsClearAndReachable(
            Vector3 candidate, Vector3 target, float radius, float height, out Vector3 result)
        {
            result = default;

            Vector3 grounded = new Vector3(candidate.x, GameplayPlaneY, candidate.z);

            float checkRadius = radius + obstacleClearance;
            float upperY = Mathf.Max(checkRadius, height - checkRadius);
            Vector3 lower = grounded + Vector3.up * checkRadius;
            Vector3 upper = grounded + Vector3.up * upperY;

            if (Physics.CheckCapsule(lower, upper, checkRadius, _obstacleMask,
                                     QueryTriggerInteraction.Ignore))
                return false;

            result = grounded;
            return true;
        }

        /// <summary>Shared gameplay plane height. Enemies, the player and spawns all live on it.</summary>
        private const float GameplayPlaneY = 0f;

        /// <summary>
        /// Footprint used for the blocker overlap test.
        ///
        /// Previously read from the prefab's NavMeshAgent. With the agent gone the footprint comes
        /// from the enemy's own collider, which is what actually occupies space — and is what the
        /// overlap test was really approximating all along.
        /// </summary>
        private static void GetAgentDimensions(ZombieData data, out float radius, out float height)
        {
            radius = 0.4f;
            height = 1.8f;
            if (data.prefab == null) return;

            Vector3 scale = data.prefab.transform.lossyScale;
            float lateral = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));

            if (data.prefab.TryGetComponent(out CapsuleCollider capsule))
            {
                radius = Mathf.Max(0.1f, capsule.radius * lateral);
                height = Mathf.Max(radius * 2f, capsule.height * Mathf.Abs(scale.y));
            }
            else if (data.prefab.TryGetComponent(out Collider collider))
            {
                Vector3 size = collider.bounds.size;
                radius = Mathf.Max(0.1f, Mathf.Max(size.x, size.z) * 0.5f);
                height = Mathf.Max(radius * 2f, size.y);
            }
        }
    }
}
