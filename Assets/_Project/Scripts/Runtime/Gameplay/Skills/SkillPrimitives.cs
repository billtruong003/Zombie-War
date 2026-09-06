using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.Skills
{
    // ═════════════════════════════════════════════════════════════════════════════════════════════
    // M7.2 B1 — the nine shared primitives.
    //
    // Twenty of the twenty-three cards are compositions of these. The rule that keeps that true: if a
    // card needs bespoke logic, the PRIMITIVE is wrong and gets fixed — the card does not grow a
    // special case. Every primitive here is allocation-free in steady state (pre-sized buffers, no
    // LINQ, no per-frame closures) because the run budget is 0 bytes/frame.
    // ═════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Status kinds a card can hang on an enemy. Kept as an enum so P1 needs no allocation.</summary>
    public enum StatusKind
    {
        Slow = 0,
        Exposed = 1,     // takes bonus damage
        Marked = 2,      // per-target empower record (Hunter's Mark)
        HitCount = 3,    // per-target hit counter (Focus Fire)
        ShotCount = 4,   // per-target shot counter (Breach Round)
    }

    // ─────────────────────────────────────────────────────────────────────────── P1
    /// <summary>
    /// <b>P1 — per-enemy status / record carrier.</b> A timed modifier bag attached to an enemy,
    /// read at damage time and decayed centrally.
    ///
    /// Serves: Focus Fire, Breach Round, Concussion, Hunter's Mark.
    ///
    /// Keyed by instance id rather than by component so a pooled/recycled enemy cannot inherit the
    /// previous occupant's statuses — <see cref="Clear"/> is called on despawn.
    /// </summary>
    public static class StatusCarrier
    {
        struct Entry
        {
            public float value;
            public float expiresAt;
        }

        // instanceId -> (kind -> entry). Dictionaries are allocated once per enemy and reused.
        static readonly Dictionary<int, Entry[]> Map = new(256);
        static readonly Stack<Entry[]> Pool = new(256);
        static readonly int KindCount = Enum.GetValues(typeof(StatusKind)).Length;

        public static int TrackedCount => Map.Count;

        public static void Apply(int instanceId, StatusKind kind, float value, float duration, float now)
        {
            if (!Map.TryGetValue(instanceId, out var arr))
            {
                arr = Pool.Count > 0 ? Pool.Pop() : new Entry[KindCount];
                for (int i = 0; i < arr.Length; i++) { arr[i].value = 0f; arr[i].expiresAt = 0f; }
                Map[instanceId] = arr;
            }
            arr[(int)kind].value = value;
            arr[(int)kind].expiresAt = now + duration;
        }

        /// <summary>Adds to an existing value and refreshes the window — for counters.</summary>
        public static float Accumulate(int instanceId, StatusKind kind, float delta, float duration, float now)
        {
            float current = Get(instanceId, kind, now);
            Apply(instanceId, kind, current + delta, duration, now);
            return current + delta;
        }

        public static float Get(int instanceId, StatusKind kind, float now)
        {
            if (!Map.TryGetValue(instanceId, out var arr)) return 0f;
            ref var e = ref arr[(int)kind];
            return e.expiresAt > now ? e.value : 0f;
        }

        public static bool Has(int instanceId, StatusKind kind, float now) => Get(instanceId, kind, now) > 0f;

        /// <summary>Drops every record for an enemy. MUST be called when an enemy dies or is pooled.</summary>
        public static void Clear(int instanceId)
        {
            if (!Map.TryGetValue(instanceId, out var arr)) return;
            Map.Remove(instanceId);
            if (Pool.Count < 256) Pool.Push(arr);
        }

        public static void ClearAll() { Map.Clear(); Pool.Clear(); }
    }

    // ─────────────────────────────────────────────────────────────────────────── P2
    /// <summary>
    /// <b>P2 — autonomous power framework.</b> A cooldown-driven slot: a trigger condition, cooldown
    /// state and a 0..1 readout for the HUD. Every autonomous card is one instance of this rather
    /// than its own timer.
    ///
    /// Serves: Chain Lightning, Ordnance Core, Soul Burst, Emergency Detonation.
    /// </summary>
    [Serializable]
    public class AutonomousPower
    {
        public enum TriggerKind { Interval, KillCount, HealthThreshold }

        public readonly string id;
        public readonly TriggerKind trigger;

        float _cooldown;
        float _readyAt;
        int _killsRequired, _killsSeen;
        float _healthFraction;
        bool _armed = true;

        /// <summary>Global proc ceiling shared by ALL powers — the ≤2 procs/s guardrail.</summary>
        public const float GlobalProcsPerSecond = 2f;
        static float _lastGlobalProcAt = float.NegativeInfinity;
        static int _globalProcsThisSecond;
        static float _globalWindowStart;

        public AutonomousPower(string id, TriggerKind trigger, float cooldown,
                               int killsRequired = 0, float healthFraction = 0f)
        {
            this.id = id;
            this.trigger = trigger;
            _cooldown = Mathf.Max(0.05f, cooldown);
            _killsRequired = Mathf.Max(1, killsRequired);
            _healthFraction = healthFraction;
        }

        public float Cooldown { get => _cooldown; set => _cooldown = Mathf.Max(0.05f, value); }

        /// <summary>0..1 for a HUD ring. 1 = ready.</summary>
        public float Readiness(float now) =>
            _cooldown <= 0f ? 1f : Mathf.Clamp01(1f - Mathf.Max(0f, _readyAt - now) / _cooldown);

        public void NotifyKill() => _killsSeen++;

        /// <summary>Re-arms a health-threshold power once the player is healthy again.</summary>
        public void NotifyHealthFraction(float fraction)
        {
            if (trigger != TriggerKind.HealthThreshold) return;
            if (fraction > _healthFraction * 1.25f) _armed = true;
        }

        /// <summary>
        /// True at most once per cooldown, and never more often than the global proc ceiling. The
        /// global cap is enforced here rather than in each card so no combination of powers can
        /// exceed it.
        /// </summary>
        public bool TryProc(float now, float playerHealthFraction = 1f)
        {
            if (now < _readyAt) return false;

            bool triggered = trigger switch
            {
                TriggerKind.Interval => true,
                TriggerKind.KillCount => _killsSeen >= _killsRequired,
                TriggerKind.HealthThreshold => _armed && playerHealthFraction <= _healthFraction,
                _ => false,
            };
            if (!triggered) return false;

            if (now - _globalWindowStart >= 1f) { _globalWindowStart = now; _globalProcsThisSecond = 0; }
            if (_globalProcsThisSecond >= GlobalProcsPerSecond) return false;

            _globalProcsThisSecond++;
            _lastGlobalProcAt = now;
            _readyAt = now + _cooldown;
            if (trigger == TriggerKind.KillCount) _killsSeen = 0;
            if (trigger == TriggerKind.HealthThreshold) _armed = false;
            return true;
        }

        public static void ResetGlobalBudget()
        {
            _lastGlobalProcAt = float.NegativeInfinity;
            _globalProcsThisSecond = 0;
            _globalWindowStart = 0f;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────── P3
    /// <summary>
    /// <b>P3 — multi-target spatial query.</b> Chain-to-nearest, densest cluster, cone overlap and
    /// priority override. None of this existed: <c>FireMode.ChainLightning</c> was an enum value with
    /// no code behind it, and no density query existed anywhere in the runtime.
    ///
    /// Serves: Static Build-up, Shockwave Belt, Chain Lightning, Ordnance Core, Hunter's Mark.
    ///
    /// Every query writes into caller-supplied buffers and uses <c>OverlapSphereNonAlloc</c>, so a
    /// proc costs zero allocations and at most ONE physics query — the stated guardrail.
    /// </summary>
    public static class TargetQuery
    {
        public const int MaxConsidered = 64;   // clustering operates over at most 64 enemies
        public const int MaxChain = 6;         // ≤6 arcs per proc

        static readonly Collider[] Hits = new Collider[MaxConsidered];
        static readonly Vector3[] Points = new Vector3[MaxConsidered];
        static readonly int[] Ids = new int[MaxConsidered];

        /// <summary>Fills the shared buffer once. Returns how many enemies were found.</summary>
        public static int Gather(Vector3 origin, float radius, LayerMask mask)
        {
            int n = Physics.OverlapSphereNonAlloc(origin, radius, Hits, mask, QueryTriggerInteraction.Ignore);
            if (n > MaxConsidered) n = MaxConsidered;
            for (int i = 0; i < n; i++)
            {
                Points[i] = Hits[i].transform.position;
                Ids[i] = Hits[i].transform.GetInstanceID();
            }
            return n;
        }

        public static Collider Candidate(int i) => Hits[i];
        public static Vector3 CandidatePoint(int i) => Points[i];

        /// <summary>
        /// Seeds the candidate buffer directly, without a physics query. This exists so the selection
        /// maths (chain / cluster / cone / priority) can be tested against exact enemy layouts, and so
        /// card logic can be exercised without a physics scene. It performs NO query, which is also
        /// how the "one OverlapSphereNonAlloc per proc" guarantee stays checkable.
        /// </summary>
        public static int SeedForTest(Vector3[] points, int[] ids, int count)
        {
            if (points == null) return 0;
            count = Mathf.Clamp(count, 0, Mathf.Min(MaxConsidered, points.Length));
            for (int i = 0; i < count; i++)
            {
                Points[i] = points[i];
                Ids[i] = ids != null && i < ids.Length ? ids[i] : i + 1;
                Hits[i] = null;
            }
            return count;
        }

        /// <summary>
        /// Chain: walk from <paramref name="start"/> to the nearest unvisited candidate within
        /// <paramref name="jumpRange"/>, up to <paramref name="maxJumps"/> (capped at 6).
        /// Writes collider indices into <paramref name="outIndices"/>; returns the count.
        /// </summary>
        public static int Chain(int count, Vector3 start, float jumpRange, int maxJumps, int[] outIndices)
        {
            maxJumps = Mathf.Min(maxJumps, MaxChain);
            int written = 0;
            Vector3 from = start;
            Span<bool> used = stackalloc bool[MaxConsidered];

            while (written < maxJumps)
            {
                int best = -1;
                float bestSqr = jumpRange * jumpRange;
                for (int i = 0; i < count; i++)
                {
                    if (used[i]) continue;
                    float d = (Points[i] - from).sqrMagnitude;
                    if (d <= bestSqr) { bestSqr = d; best = i; }
                }
                if (best < 0) break;
                used[best] = true;
                outIndices[written++] = best;
                from = Points[best];
            }
            return written;
        }

        /// <summary>
        /// Densest cluster: the candidate with the most neighbours inside <paramref name="radius"/>.
        /// O(n²) over at most 64 points — bounded and cheap, and it is the query Ordnance Core needs.
        /// Returns -1 when nothing qualifies.
        /// </summary>
        public static int DensestCluster(int count, float radius, out int neighbours)
        {
            neighbours = 0;
            int best = -1;
            float r2 = radius * radius;
            for (int i = 0; i < count; i++)
            {
                int n = 0;
                for (int j = 0; j < count; j++)
                    if (i != j && (Points[i] - Points[j]).sqrMagnitude <= r2) n++;
                if (n > neighbours || best < 0) { neighbours = n; best = i; }
            }
            return best;
        }

        /// <summary>Cone overlap: candidates within <paramref name="halfAngleDeg"/> of a direction.</summary>
        public static int Cone(int count, Vector3 origin, Vector3 direction, float halfAngleDeg, int[] outIndices)
        {
            int written = 0;
            float cos = Mathf.Cos(halfAngleDeg * Mathf.Deg2Rad);
            Vector3 dir = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
            for (int i = 0; i < count && written < outIndices.Length; i++)
            {
                Vector3 to = Points[i] - origin;
                if (to.sqrMagnitude < 1e-6f) { outIndices[written++] = i; continue; }
                if (Vector3.Dot(to.normalized, dir) >= cos) outIndices[written++] = i;
            }
            return written;
        }

        /// <summary>Priority override: the candidate carrying a status, else the nearest.</summary>
        public static int Priority(int count, Vector3 origin, StatusKind kind, float now)
        {
            int nearest = -1;
            float nearestSqr = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (StatusCarrier.Has(Ids[i], kind, now)) return i;
                float d = (Points[i] - origin).sqrMagnitude;
                if (d < nearestSqr) { nearestSqr = d; nearest = i; }
            }
            return nearest;
        }

        public static int CandidateId(int i) => Ids[i];
    }

    // ─────────────────────────────────────────────────────────────────────────── P4
    /// <summary>
    /// <b>P4 — player distance accumulator.</b> An odometer with a consume rule.
    /// Serves: Quickstep Round, Kinetic Shield.
    /// </summary>
    public class DistanceAccumulator
    {
        Vector3 _last;
        bool _seeded;
        public float Distance { get; private set; }

        public void Sample(Vector3 position)
        {
            if (!_seeded) { _last = position; _seeded = true; return; }
            Distance += Vector3.Distance(_last, position);
            _last = position;
        }

        /// <summary>Spends <paramref name="threshold"/> metres if available. True when it fired.</summary>
        public bool TryConsume(float threshold)
        {
            if (Distance < threshold) return false;
            Distance -= threshold;
            return true;
        }

        public void Reset() { Distance = 0f; _seeded = false; }
    }

    // ─────────────────────────────────────────────────────────────────────────── P5
    /// <summary>
    /// <b>P5 — ramp / charge accumulator with decay.</b> A 0..1 value that builds while a condition
    /// holds and decays when it stops.
    /// Serves: Run &amp; Gun, Static Build-up, Bullet Hose, Breach Round, Heavy Pressure.
    /// </summary>
    public class RampAccumulator
    {
        readonly float _risePerSecond, _fallPerSecond;
        public float Value { get; private set; }

        public RampAccumulator(float risePerSecond, float fallPerSecond)
        {
            _risePerSecond = Mathf.Max(0f, risePerSecond);
            _fallPerSecond = Mathf.Max(0f, fallPerSecond);
        }

        public void Tick(bool active, float dt)
        {
            Value = Mathf.Clamp01(Value + (active ? _risePerSecond : -_fallPerSecond) * dt);
        }

        /// <summary>Discrete charge, for "N consecutive hits" style cards.</summary>
        public bool AddCharge(float amount, float threshold)
        {
            Value += amount;
            if (Value < threshold) return false;
            Value -= threshold;
            return true;
        }

        public void Reset() => Value = 0f;
    }

    // ─────────────────────────────────────────────────────────────────────────── P6
    /// <summary>
    /// <b>P6 — damage-path interception hook.</b> The single point where a card may read the target
    /// and modify outgoing damage. Cards register a delegate; the weapon calls
    /// <see cref="Modify"/> once per hit.
    ///
    /// Serves: Quickstep Round, Execution Round, Kinetic Shield.
    ///
    /// The list is pre-sized and iterated by index so a hit costs no allocation and no enumerator.
    /// </summary>
    public static class DamageInterceptor
    {
        public struct Context
        {
            public int targetId;
            public float distance;
            public float targetHealthFraction;
            public float now;
        }

        public delegate float Modifier(float damage, in Context ctx);

        static readonly List<Modifier> Modifiers = new(16);

        public static int Count => Modifiers.Count;
        public static void Register(Modifier m) { if (m != null && !Modifiers.Contains(m)) Modifiers.Add(m); }
        public static void Unregister(Modifier m) => Modifiers.Remove(m);
        public static void Clear() => Modifiers.Clear();

        public static float Modify(float damage, in Context ctx)
        {
            for (int i = 0; i < Modifiers.Count; i++) damage = Modifiers[i](damage, in ctx);
            return damage;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────── P7
    /// <summary>
    /// <b>P7 — distance-scaled damage curve.</b> Extends the existing
    /// <c>WeaponData.RangeFalloff</c> rather than replacing it: the weapon's own falloff still
    /// applies, and this multiplies on top.
    /// Serves: Point Blank, Longshot.
    /// </summary>
    public readonly struct DistanceDamageCurve
    {
        readonly float _nearMul, _farMul, _nearMetres, _farMetres;

        public DistanceDamageCurve(float nearMetres, float nearMul, float farMetres, float farMul)
        {
            _nearMetres = nearMetres; _nearMul = nearMul;
            _farMetres = Mathf.Max(farMetres, nearMetres + 0.01f); _farMul = farMul;
        }

        public float Evaluate(float distance)
        {
            float t = Mathf.InverseLerp(_nearMetres, _farMetres, distance);
            return Mathf.Lerp(_nearMul, _farMul, t);
        }

        /// <summary>Close range hurts more. Point Blank.</summary>
        public static DistanceDamageCurve PointBlank(float bonus, float range) => new(0f, 1f + bonus, range, 1f);
        /// <summary>Long range hurts more. Longshot.</summary>
        public static DistanceDamageCurve Longshot(float bonus, float range) => new(0f, 1f, range, 1f + bonus);
    }

    // ─────────────────────────────────────────────────────────────────────────── P8
    /// <summary>
    /// <b>P8 — stat soft-cap curve.</b> Diminishing returns so repeat stat picks stop being strictly
    /// correct, and a hard ceiling nothing can exceed.
    /// Serves: Fire Rate Up, Move Speed Up. Guardrail: fire rate soft 2.2× / hard 2.5×.
    /// </summary>
    public readonly struct SoftCap
    {
        public readonly float soft, hard;

        public SoftCap(float soft, float hard)
        {
            this.soft = soft;
            this.hard = Mathf.Max(hard, soft);
        }

        /// <summary>
        /// Below the soft cap the multiplier is untouched. Above it, the excess is compressed
        /// asymptotically toward the hard cap so the hard cap can be approached but never passed.
        /// </summary>
        public float Apply(float raw)
        {
            if (raw <= soft) return raw;
            float excess = raw - soft;
            float room = hard - soft;
            if (room <= 0f) return soft;
            return soft + room * (1f - Mathf.Exp(-excess / room));
        }

        public static readonly SoftCap FireRate = new(2.2f, 2.5f);
        public static readonly SoftCap MoveSpeed = new(1.6f, 1.9f);
    }

    // ─────────────────────────────────────────────────────────────────────────── P9
    /// <summary>
    /// <b>P9 — power VFX / HUD kit.</b> The shared presentation layer for P2 and P3 cards: pooled
    /// one-shot effects and cooldown readouts. Pooling is mandatory — the run budget is zero
    /// allocations per frame and zero runtime material instances.
    /// Serves: Static Build-up, Shockwave Belt, Chain Lightning, Ordnance Core.
    /// </summary>
    public class PowerFxKit
    {
        readonly Dictionary<string, Queue<GameObject>> _pools = new(8);
        readonly Dictionary<string, GameObject> _prefabs = new(8);
        readonly Transform _root;
        readonly int _maxPerKey;

        /// <summary>≤2 concurrent explosions is a stated guardrail; the pool enforces it per key.</summary>
        public PowerFxKit(Transform root, int maxPerKey = 2)
        {
            _root = root;
            _maxPerKey = Mathf.Max(1, maxPerKey);
        }

        public void Register(string key, GameObject prefab)
        {
            if (string.IsNullOrEmpty(key) || prefab == null) return;
            _prefabs[key] = prefab;
            if (!_pools.ContainsKey(key)) _pools[key] = new Queue<GameObject>(_maxPerKey);
        }

        public int LiveCount { get; private set; }

        /// <summary>Spawns from the pool. Returns null when the per-key ceiling is already reached.</summary>
        public GameObject Play(string key, Vector3 position, Quaternion rotation)
        {
            if (!_prefabs.TryGetValue(key, out var prefab) || prefab == null) return null;
            var pool = _pools[key];

            GameObject go;
            if (pool.Count > 0) { go = pool.Dequeue(); go.transform.SetPositionAndRotation(position, rotation); go.SetActive(true); }
            else if (LiveCount < _maxPerKey) { go = UnityEngine.Object.Instantiate(prefab, position, rotation, _root); }
            else return null;

            LiveCount++;
            return go;
        }

        public void Recycle(string key, GameObject go)
        {
            if (go == null || !_pools.TryGetValue(key, out var pool)) return;
            go.SetActive(false);
            pool.Enqueue(go);
            LiveCount = Mathf.Max(0, LiveCount - 1);
        }
    }
}
