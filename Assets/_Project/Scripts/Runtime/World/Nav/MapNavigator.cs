using UnityEngine;

namespace ZombieWar.WorldNav
{
    /// <summary>
    /// The enemies' pathing on a baked map (2026-10-01): one <see cref="FlowField"/> toward the
    /// player over a 72 m window, re-read from the NavObstacle colliders when the window moves or an
    /// obstacle inside it appears or goes (<see cref="MarkDirty"/>), and re-solved a few times a
    /// second. Enemies ask it which way to go and whether a step
    /// would land in an obstacle; with no navigator (procedural world, sandboxes) they behave as before.
    /// </summary>
    public sealed class MapNavigator : MonoBehaviour
    {
        const float Window = 72f, CellSize = 0.5f, SolveInterval = 0.2f, RecentreDistance = 10f;

        public static MapNavigator Instance { get; private set; }
        public static bool Active => Instance != null && Instance._field.HasSolution;

        /// The point the field leads to (the player).
        public static Vector3 Target { get; private set; }

        FlowField _field;
        Vector3 _windowCentre;
        float _nextSolve;
        static bool _dirty = true;

        /// An obstacle inside the square of half-size <paramref name="halfExtent"/> around
        /// <paramref name="centre"/> appeared or went. Only changes the window can see re-read it:
        /// streamed chunks at the ring's edge are outside it and are read when the window moves there.
        public static void MarkDirty(Vector3 centre, float halfExtent)
        {
            if (Instance == null) { _dirty = true; return; }
            float reach = Window * 0.5f + halfExtent;
            Vector3 d = centre - Instance._windowCentre;
            if (Mathf.Abs(d.x) < reach && Mathf.Abs(d.z) < reach) _dirty = true;
        }

        void Awake()
        {
            Instance = this;
            _field = new FlowField(Window, CellSize, LayerMask.GetMask("NavObstacle"));
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _dirty = true;

        void LateUpdate()
        {
            var player = PlayerMovement.Instance;
            if (player == null) return;
            Vector3 p = player.transform.position;
            if (_dirty || (p - _windowCentre).sqrMagnitude > RecentreDistance * RecentreDistance)
            {
                Physics.SyncTransforms();
                _field.Rasterize(p);
                _windowCentre = p;
                _dirty = false;
                _nextSolve = 0f;
            }
            if (Time.time >= _nextSolve)
            {
                Target = p;
                _field.Solve(p);
                _nextSolve = Time.time + SolveInterval;
            }
        }

        /// Which way to walk toward the player from <paramref name="from"/>; false when the field
        /// has no answer there (outside the window), and the caller goes straight.
        public static bool TryDirection(Vector3 from, out Vector3 direction)
        {
            direction = Vector3.zero;
            if (!Active) return false;
            direction = Instance._field.Direction(from);
            return direction != Vector3.zero;
        }

        /// True when <paramref name="position"/> is inside an obstacle (water, lava, a rock).
        public static bool Blocked(Vector3 position) => Instance != null && Instance._field.IsBlocked(position);
    }
}
