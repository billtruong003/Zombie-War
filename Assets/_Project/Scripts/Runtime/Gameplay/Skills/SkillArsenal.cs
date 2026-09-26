using System.Collections.Generic;
using UnityEngine;
using BillGameCore;

namespace ZombieWar.Skills
{
    /// <summary>
    /// M8 — the powers that live in the world rather than in a single proc: Orbit Blades, Drone
    /// Buddy, Fire Trail, Boomerang, Airstrike and Frost Nova, plus the delayed-blast system the
    /// driver's Ordnance / Carpet Bomb / Reaper share.
    ///
    /// <b>Game-feel contract</b> (owner, 2026-09-26: "the feel when it shows is not right yet"). Every
    /// power here must:
    /// <list type="number">
    /// <item>have a visual the size of its hitbox — effects are scaled from their measured native
    /// radius, never left at prefab size;</item>
    /// <item>have a sound, throttled so a crowd does not turn it into noise;</item>
    /// <item>telegraph anything that lands later (airstrike marker before the bomb);</item>
    /// <item>push or tint what it hits, so the player sees WHICH enemies it affected;</item>
    /// <item>shake the camera only for the big moments, from one shared budget.</item>
    /// </list>
    ///
    /// Numbers (counts, radii, rates) come from <see cref="SkillRuntime"/>, the same helpers the
    /// card text reads. Nothing here allocates per frame in steady state.
    /// </summary>
    [DisallowMultipleComponent]
    public class SkillArsenal : MonoBehaviour
    {
        [SerializeField] private LayerMask enemyMask = ~0;

        [Header("Orbit Blades")]
        [Tooltip("Optional mesh override. Empty = the built-in saw disc, which reads far better from " +
                 "the top-down camera than a knife (and the knife pack's Standard material renders " +
                 "magenta in URP).")]
        [SerializeField] private GameObject bladeModel;
        [Tooltip("Vertex-coloured unlit material for the saw disc.")]
        [SerializeField] private Material bladeMaterial;
        [SerializeField] private Material trailMaterial;
        [SerializeField] private ParticleSystem bladeHitFx;
        [SerializeField] private float orbitBaseDamage = 9f;
        [Tooltip("A blade can hit the same enemy at most this often.")]
        [SerializeField] private float orbitHitInterval = 0.4f;
        [SerializeField] private float bladeScale = 0.9f;
        [SerializeField] private Color orbitTrailColor = new(0.75f, 0.95f, 1f, 0.9f);

        [Header("Drone Buddy")]
        [SerializeField] private ParticleSystem droneBodyFx;
        [SerializeField] private GameObject droneTracer;
        [SerializeField] private ParticleSystem droneMuzzleFx;
        [SerializeField] private ParticleSystem droneHitFx;
        [SerializeField] private float droneBaseDamage = 7f;
        [SerializeField] private float droneRange = 11f;
        [SerializeField] private float droneBodyScale = 0.6f;

        [Header("Frost Nova")]
        [SerializeField] private ParticleSystem frostNovaFx;
        [SerializeField] private float frostNovaNativeRadius = 4f;
        [SerializeField] private float frostBaseDamage = 16f;
        [SerializeField] private Color frostTint = new(0.45f, 0.8f, 1f, 0.6f);
        [SerializeField] private Color frozenTint = new(0.7f, 0.95f, 1f, 0.85f);

        [Header("Fire Trail")]
        [SerializeField] private ParticleSystem firePatchFx;
        [SerializeField] private float firePatchNativeRadius = 0.75f;
        [SerializeField] private float firePatchRadius = 1.1f;
        [SerializeField] private float firePatchSeconds = 2.2f;
        [SerializeField] private Color burnTint = new(1f, 0.55f, 0.15f, 0.45f);

        [Header("Boomerang")]
        [SerializeField] private ParticleSystem boomerangHitFx;
        [SerializeField] private float boomerangBaseDamage = 14f;
        [SerializeField] private float boomerangRange = 9f;
        [SerializeField] private float boomerangOutSeconds = 0.5f;
        [SerializeField] private float boomerangScale = 1.25f;
        [SerializeField] private Color boomerangTint = new(1f, 0.72f, 0.3f, 1f);
        [SerializeField] private Color boomerangTrailColor = new(1f, 0.7f, 0.25f, 0.9f);

        [Header("Airstrike / delayed blasts")]
        [SerializeField] private ParticleSystem strikeMarkerFx;
        [SerializeField] private float strikeMarkerNativeRadius = 1.2f;
        [SerializeField] private ParticleSystem strikeMissileFx;
        [SerializeField] private ParticleSystem strikeBlastFx;
        [SerializeField] private float strikeBlastNativeRadius = 2f;
        [SerializeField] private ParticleSystem strikeDecalFx;
        [SerializeField] private float airstrikeBaseDamage = 45f;
        [SerializeField] private float airstrikeDelay = 0.75f;
        [SerializeField] private float missileFallSeconds = 0.3f;

        [Header("Evolutions / shield")]
        [SerializeField] private ParticleSystem thunderStrikeFx;
        [SerializeField] private ParticleSystem reaperFx;
        [SerializeField] private ParticleSystem shieldAuraFx;
        [SerializeField] private ParticleSystem evolveFx;

        public static SkillArsenal Instance { get; private set; }

        Transform _tr;
        CameraFollow _camera;

        void Awake()
        {
            _tr = transform;
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_root != null) Destroy(_root.gameObject);
        }

        void Start()
        {
            _camera = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
            _root = new GameObject("~SkillArsenal").transform;
            BuildBlades();
        }

        Transform _root;

        void Update()
        {
            var run = SkillRuntime.Active;
            if (run == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;   // paused (level-up screen): nothing moves, nothing hits

            Vector3 p = _tr.position;
            TickOrbit(run, p, dt);
            TickDrones(run, p, dt);
            TickFireTrail(run, p, dt);
            TickBoomerangs(run, p, dt);
            TickBlasts(run);
            TickShield(run, p);
            _shakeBudget = Mathf.Max(0f, _shakeBudget - dt * 1.5f);
        }

        // ═══════════════════════════════════════════════════════════════ shared helpers

        float _shakeBudget;

        /// <summary>One shared shake budget, so three powers landing together shake like one big
        /// hit, not three stacked ones.</summary>
        void Shake(float amount)
        {
            if (_camera == null) return;
            float allowed = Mathf.Max(0f, 0.45f - _shakeBudget);
            float a = Mathf.Min(amount, allowed);
            if (a <= 0f) return;
            _shakeBudget += a;
            _camera.Shake(a);
        }

        readonly Dictionary<string, float> _nextSfx = new(16);

        /// <summary>A power's sound, at most once per <paramref name="minGap"/> seconds per key.</summary>
        void Sfx(string key, Vector3 at, float volume = 0.8f, float minGap = 0.08f)
        {
            if (string.IsNullOrEmpty(key)) return;
            float now = Time.time;
            if (_nextSfx.TryGetValue(key, out float next) && now < next) return;
            _nextSfx[key] = now + minGap;
            // Big moments must not be the first voice dropped when the horde gets loud.
            Bill.Audio?.PlayCue(key, at, volume >= 0.7f ? SfxPriority.High : SfxPriority.Medium, volume);
        }

        Camera _cam;

        public bool OnScreen(Vector3 world) => OnScreen(world, 0.08f);

        bool OnScreen(Vector3 world, float margin)
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return true;
            var v = _cam.WorldToViewportPoint(world);
            return v.z > 0f && v.x > margin && v.x < 1f - margin && v.y > margin && v.y < 1f - margin;
        }

        static ZombieBase EnemyOf(Collider c) => c != null ? c.GetComponentInParent<ZombieBase>() : null;

        static void Hit(ZombieBase enemy, float damage, float push)
        {
            if (enemy == null || enemy.IsDead) return;
            enemy.TakeDamage(damage);
            if (push > 0f && !enemy.IsDead) enemy.ApplyPhysicalPush(push);
        }

        static Vector3 Chest(ZombieBase e) => e.transform.position + Vector3.up * 0.9f;

        // ═══════════════════════════════════════════════════════════════ Orbit Blades

        const int MaxBlades = 6;
        readonly Transform[] _blades = new Transform[MaxBlades];
        readonly TrailRenderer[] _bladeTrails = new TrailRenderer[MaxBlades];
        float _orbitAngle, _orbitScanAt;
        readonly Dictionary<int, float> _orbitNextHit = new(128);
        readonly List<int> _scratchIds = new(128);

        void BuildBlades()
        {
            for (int i = 0; i < MaxBlades; i++)
            {
                var t = MakeBlade("blade", bladeScale, orbitTrailColor, 0.12f, Color.white, out _bladeTrails[i]);
                _blades[i] = t;
            }
            for (int i = 0; i < MaxBoomerangs; i++)
            {
                var t = MakeBlade("boomerang", boomerangScale, boomerangTrailColor, 0f, boomerangTint, out _);
                _boom[i].visual = t;
                _boom[i].streak = MakeStreak(t, boomerangTrailColor, 0.4f * boomerangScale);
            }
        }

        Transform MakeBlade(string name, float scale, Color trailColor, float trailTime, Color tint,
                            out TrailRenderer trail)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(_root, false);
            if (bladeModel == null)
            {
                var disc = new GameObject("saw");
                disc.transform.SetParent(holder, false);
                disc.transform.localScale = Vector3.one * scale;
                disc.AddComponent<MeshFilter>().sharedMesh = SawMesh();
                var mr = disc.AddComponent<MeshRenderer>();
                mr.sharedMaterial = bladeMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                if (tint != Color.white)
                {
                    var block = new MaterialPropertyBlock();
                    block.SetColor(BaseColorId, tint);
                    mr.SetPropertyBlock(block);
                }
            }
            else
            {
                var model = Instantiate(bladeModel, holder);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // lie flat, edge outward
                model.transform.localScale = Vector3.one * scale;
                foreach (var col in model.GetComponentsInChildren<Collider>()) Destroy(col);
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
            }
            trail = null;
            if (trailTime <= 0f)
            {
                holder.gameObject.SetActive(false);
                return holder;
            }
            trail = holder.gameObject.AddComponent<TrailRenderer>();
            trail.time = trailTime;
            trail.minVertexDistance = 0.08f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.45f * scale), new Keyframe(1f, 0f));
            trail.startColor = trailColor;
            trail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            if (trailMaterial != null) trail.sharedMaterial = trailMaterial;
            holder.gameObject.SetActive(false);
            return holder;
        }

        void TickOrbit(SkillRuntime run, Vector3 p, float dt)
        {
            int count = Mathf.Min(MaxBlades, run.OrbitBladeCount);
            float radius = run.OrbitRadius;
            _orbitAngle = (_orbitAngle + run.OrbitDegreesPerSecond * dt) % 360f;

            for (int i = 0; i < MaxBlades; i++)
            {
                var b = _blades[i];
                if (b == null) continue;
                bool on = i < count;
                if (b.gameObject.activeSelf != on)
                {
                    b.gameObject.SetActive(on);
                    if (on) _bladeTrails[i].Clear();
                }
                if (!on) continue;

                float a = (_orbitAngle + i * 360f / count) * Mathf.Deg2Rad;
                b.position = p + new Vector3(Mathf.Cos(a) * radius, 0.8f, Mathf.Sin(a) * radius);
                // Spin on its own axis too: a saw, not a satellite.
                b.rotation = Quaternion.Euler(0f, -_orbitAngle * 3f - i * 60f, 0f);
            }
            if (count == 0) return;

            // Damage scan at 20 Hz: one physics query for all blades.
            if (Time.time < _orbitScanAt) return;
            _orbitScanAt = Time.time + 0.05f;

            float now = Time.time;
            float damage = run.PowerDamage(orbitBaseDamage, SkillCatalogDefs.AutoOrbit);
            int found = TargetQuery.GatherEnemies(p, radius + 1.2f, enemyMask);
            const float contact = 0.95f;
            for (int c = 0; c < found; c++)
            {
                Vector3 ep = TargetQuery.CandidatePoint(c);
                bool touched = false;
                for (int i = 0; i < count && !touched; i++)
                {
                    Vector3 bp = _blades[i].position;
                    float dx = ep.x - bp.x, dz = ep.z - bp.z;
                    touched = dx * dx + dz * dz <= contact * contact;
                }
                if (!touched) continue;

                int id = TargetQuery.CandidateId(c);
                if (_orbitNextHit.TryGetValue(id, out float next) && now < next) continue;
                var enemy = EnemyOf(TargetQuery.Candidate(c));
                if (enemy == null) continue;
                _orbitNextHit[id] = now + orbitHitInterval;

                Hit(enemy, damage, 0.35f);
                FxPool.Play(bladeHitFx, Chest(enemy), Quaternion.identity, 0.6f);
                Sfx("sfx.skill.blade.hit", ep, 0.45f, 0.07f);
            }

            // Forget enemies not touched for a while, so the map does not grow for the whole run.
            if (_orbitNextHit.Count > 96)
            {
                _scratchIds.Clear();
                foreach (var kv in _orbitNextHit) if (now - kv.Value > 2f) _scratchIds.Add(kv.Key);
                for (int i = 0; i < _scratchIds.Count; i++) _orbitNextHit.Remove(_scratchIds[i]);
            }
        }

        /// <summary>
        /// A velocity streak for fast projectiles. A TrailRenderer at 20-36 m/s and a low frame rate
        /// kept its launch point alive for the whole flight — measured: an 8 m bar from the player
        /// to the blade, which read as a laser. The streak is recomputed every frame from the
        /// actual velocity, so its length is bounded whatever the frame rate.
        /// </summary>
        LineRenderer MakeStreak(Transform holder, Color color, float width)
        {
            var go = new GameObject("streak");
            go.transform.SetParent(_root, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.alignment = LineAlignment.View;
            lr.numCapVertices = 2;
            lr.widthCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, width));
            lr.startColor = new Color(color.r, color.g, color.b, 0f);
            lr.endColor = color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            if (trailMaterial != null) lr.sharedMaterial = trailMaterial;
            go.SetActive(false);
            return lr;
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static Mesh _saw;

        /// <summary>
        /// A flat eight-tooth saw disc (radius 0.5, in the ground plane), vertex-coloured: dark hub,
        /// steel body, bright swept teeth. Built once and shared by every blade and boomerang.
        /// Double-sided so it reads from any camera tilt.
        /// </summary>
        static Mesh SawMesh()
        {
            if (_saw != null) return _saw;
            const int teeth = 8;
            const float hub = 0.16f, valley = 0.36f, tip = 0.5f;
            var hubCol = new Color(0.30f, 0.33f, 0.40f);
            var bodyCol = new Color(0.78f, 0.84f, 0.92f);
            var tipCol = Color.white;

            var v = new List<Vector3> { Vector3.zero };
            var c = new List<Color> { hubCol };
            int ring = teeth * 2;
            for (int i = 0; i < ring; i++)
            {
                float a = i * Mathf.PI * 2f / ring;
                v.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * hub); c.Add(hubCol);
            }
            for (int i = 0; i < ring; i++)
            {
                // Even = valley, odd = tooth tip swept forward, so it reads as a saw that spins.
                bool isTip = (i & 1) == 1;
                float a = (i + (isTip ? 0.6f : 0f)) * Mathf.PI * 2f / ring;
                float r = isTip ? tip : valley;
                v.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r); c.Add(isTip ? tipCol : bodyCol);
            }

            var t = new List<int>();
            for (int i = 0; i < ring; i++)
            {
                int h0 = 1 + i, h1 = 1 + (i + 1) % ring;
                int o0 = 1 + ring + i, o1 = 1 + ring + (i + 1) % ring;
                t.AddRange(new[] { 0, h1, h0 });
                t.AddRange(new[] { h0, h1, o1, h0, o1, o0 });
            }
            int single = t.Count;
            for (int i = 0; i < single; i += 3) t.AddRange(new[] { t[i], t[i + 2], t[i + 1] });   // back faces

            _saw = new Mesh { name = "SkillSawDisc" };
            _saw.SetVertices(v);
            _saw.SetColors(c);
            _saw.SetTriangles(t, 0);
            _saw.RecalculateBounds();
            _saw.RecalculateNormals();
            return _saw;
        }

        // ═══════════════════════════════════════════════════════════════ Drone Buddy

        const int MaxDrones = 3;
        readonly ParticleSystem[] _drones = new ParticleSystem[MaxDrones];
        readonly Vector3[] _droneVel = new Vector3[MaxDrones];
        readonly float[] _droneNextShot = new float[MaxDrones];

        void TickDrones(SkillRuntime run, Vector3 p, float dt)
        {
            int count = Mathf.Min(MaxDrones, run.DroneCount);
            float interval = 1f / Mathf.Max(0.1f, run.DroneShotsPerSecond);
            float t = Time.time;

            for (int i = 0; i < MaxDrones; i++)
            {
                bool on = i < count;
                if (on && _drones[i] == null && droneBodyFx != null)
                {
                    _drones[i] = Instantiate(droneBodyFx, _root);
                    _drones[i].transform.localScale = Vector3.one * droneBodyScale;
                    _drones[i].transform.position = p + Vector3.up * 2.2f;
                    _droneNextShot[i] = t + interval * (i + 1) / (count + 1);   // stagger the squad
                }
                var d = _drones[i];
                if (d == null) continue;
                if (d.gameObject.activeSelf != on) d.gameObject.SetActive(on);
                if (!on) continue;

                // Hover beside the player's shoulder, the squad fanned out, with a gentle bob.
                float fan = count == 1 ? -50f : -80f + i * 80f;
                float a = (fan + t * 25f) * Mathf.Deg2Rad;
                Vector3 target = p + new Vector3(Mathf.Cos(a) * 1.3f, 2.1f + Mathf.Sin(t * 3f + i) * 0.12f, Mathf.Sin(a) * 1.3f);
                d.transform.position = Vector3.SmoothDamp(d.transform.position, target, ref _droneVel[i], 0.18f);

                if (t < _droneNextShot[i]) continue;
                _droneNextShot[i] = t + interval;
                DroneShoot(run, d.transform.position);
            }
        }

        void DroneShoot(SkillRuntime run, Vector3 from)
        {
            int found = TargetQuery.GatherEnemies(from, droneRange, enemyMask);
            int best = TargetQuery.Nearest(found, from);
            if (best < 0) return;
            var enemy = TargetQuery.CandidateEnemy(best);
            if (enemy == null || enemy.IsDead) return;

            Vector3 to = Chest(enemy);
            if (droneTracer != null) TracerPool.Play(droneTracer, from, to);
            FxPool.Play(droneMuzzleFx, from, Quaternion.LookRotation(to - from), 0.5f);
            FxPool.Play(droneHitFx, to, Quaternion.identity, 0.5f);
            Sfx("sfx.skill.drone", from, 0.35f, 0.06f);

            Hit(enemy, run.PowerDamage(droneBaseDamage, SkillCatalogDefs.AutoDrone), 0.15f);

            // Drone Squadron: a kill can pay out a coin on the spot.
            if (enemy.IsDead && run.IsEvolved(SkillCatalogDefs.AutoDrone) && Random.value < 0.1f)
                PickupManager.SpawnBonusCoin(enemy.transform.position, 1);
        }

        // ═══════════════════════════════════════════════════════════════ Frost Nova

        /// <summary>Called by the driver when Frost Nova procs.</summary>
        public void FrostNova(SkillRuntime run, Vector3 centre, float radius)
        {
            FxPool.Play(frostNovaFx, centre + Vector3.up * 0.1f, Quaternion.identity,
                        radius / Mathf.Max(0.1f, frostNovaNativeRadius));
            Sfx("sfx.skill.frost", centre, 0.9f, 0.2f);
            Shake(run.FrostFreezes ? 0.25f : 0.12f);
            // The ring IS the hitbox: it expands to exactly the radius that was checked.
            SkillFxDirector.Instance?.Pulse(centre, radius, new Color(0.55f, 0.9f, 1f, 1f), 0.4f, 0.45f);

            float damage = run.PowerDamage(frostBaseDamage, SkillCatalogDefs.AutoFrostNova);
            float now = Time.time;
            int found = TargetQuery.GatherEnemies(centre, radius, enemyMask);
            for (int i = 0; i < found; i++)
            {
                var enemy = EnemyOf(TargetQuery.Candidate(i));
                if (enemy == null || enemy.IsDead) continue;
                int id = enemy.transform.GetInstanceID();
                if (run.FrostFreezes)
                {
                    StatusCarrier.Apply(id, StatusKind.Frozen, 1f, 1.5f, now);
                    SkillFxDirector.Instance?.TintEnemy(enemy, frozenTint, 1.5f);
                }
                else
                {
                    StatusCarrier.Apply(id, StatusKind.Slow, run.FrostSlow, 2f, now);
                    SkillFxDirector.Instance?.TintEnemy(enemy, frostTint, 2f);
                }
                Hit(enemy, damage, 0.6f);
            }
        }

        // ═══════════════════════════════════════════════════════════════ Fire Trail

        struct FirePatch { public Vector3 pos; public float until; }
        readonly List<FirePatch> _patches = new(16);
        float _fireTickAt;
        const float FireTick = 0.25f;

        void TickFireTrail(SkillRuntime run, Vector3 p, float dt)
        {
            int drops = run.ConsumeFireTrailDrops();
            float now = Time.time;
            for (int i = 0; i < drops && _patches.Count < 16; i++)
            {
                _patches.Add(new FirePatch { pos = p, until = now + firePatchSeconds });
                FxPool.PlayFor(firePatchFx, p + Vector3.up * 0.05f, Quaternion.identity,
                               firePatchRadius / Mathf.Max(0.1f, firePatchNativeRadius), firePatchSeconds);
                Sfx("sfx.skill.fire", p, 0.25f, 0.9f);
            }

            for (int i = _patches.Count - 1; i >= 0; i--)
                if (now >= _patches[i].until) _patches.RemoveAt(i);
            if (_patches.Count == 0 || now < _fireTickAt) return;
            _fireTickAt = now + FireTick;

            // One query around the player covers every patch (patches trail at most ~12 m behind).
            float reach = 0f;
            for (int i = 0; i < _patches.Count; i++)
                reach = Mathf.Max(reach, (_patches[i].pos - p).magnitude);
            int found = TargetQuery.GatherEnemies(p, reach + firePatchRadius + 0.5f, enemyMask);
            float damage = run.PowerDamage(run.FireTrailDps, SkillCatalogDefs.AutoFireTrail) * FireTick;
            float r2 = firePatchRadius * firePatchRadius;

            for (int c = 0; c < found; c++)
            {
                Vector3 ep = TargetQuery.CandidatePoint(c);
                bool burning = false;
                for (int i = 0; i < _patches.Count && !burning; i++)
                {
                    float dx = ep.x - _patches[i].pos.x, dz = ep.z - _patches[i].pos.z;
                    burning = dx * dx + dz * dz <= r2;
                }
                if (!burning) continue;
                var enemy = EnemyOf(TargetQuery.Candidate(c));
                if (enemy == null || enemy.IsDead) continue;
                Hit(enemy, damage, 0f);
                SkillFxDirector.Instance?.TintEnemy(enemy, burnTint, 0.4f);
            }
        }

        // ═══════════════════════════════════════════════════════════════ Boomerang

        const int MaxBoomerangs = 4;

        struct Boomerang
        {
            public Transform visual;
            public LineRenderer streak;
            public Vector3 lastPos;
            public bool live;
            public Vector3 dir;
            public float age;
            public float nextScan;
            public HashSet<int> hit;
        }

        readonly Boomerang[] _boom = new Boomerang[MaxBoomerangs];

        /// <summary>Called by the driver when Boomerang procs: throws up to <paramref name="count"/>
        /// blades at distinct nearby enemies, fanned out if there are fewer targets than blades.</summary>
        public void ThrowBoomerangs(SkillRuntime run, Vector3 from, int count)
        {
            int found = TargetQuery.GatherEnemies(from, boomerangRange + 2f, enemyMask);
            Vector3 fallback = _tr.forward;
            if (found > 0) fallback = TargetQuery.CandidatePoint(TargetQuery.Nearest(found, from)) - from;
            fallback.y = 0f;
            if (fallback.sqrMagnitude < 0.01f) fallback = Vector3.forward;
            fallback.Normalize();

            int thrown = 0;
            for (int k = 0; k < MaxBoomerangs && thrown < count; k++)
            {
                if (_boom[k].live || _boom[k].visual == null) continue;
                float spread = (thrown - (count - 1) * 0.5f) * 35f;
                _boom[k].dir = Quaternion.Euler(0f, spread, 0f) * fallback;
                _boom[k].age = 0f;
                _boom[k].live = true;
                _boom[k].nextScan = 0f;
                (_boom[k].hit ??= new HashSet<int>()).Clear();
                _boom[k].visual.position = from + Vector3.up * 0.9f;
                _boom[k].lastPos = _boom[k].visual.position;
                _boom[k].visual.gameObject.SetActive(true);
                _boom[k].streak.gameObject.SetActive(true);
                thrown++;
            }
            if (thrown > 0) Sfx("sfx.skill.boomerang.throw", from, 0.7f, 0.1f);
        }

        void TickBoomerangs(SkillRuntime run, Vector3 p, float dt)
        {
            float damage = 0f;
            for (int k = 0; k < MaxBoomerangs; k++)
            {
                ref var b = ref _boom[k];
                if (!b.live) continue;
                if (damage == 0f) damage = run.PowerDamage(boomerangBaseDamage, SkillCatalogDefs.AutoBoomerang);

                b.age += dt;
                float t = b.age / Mathf.Max(0.05f, boomerangOutSeconds);
                Vector3 pos;
                if (t <= 1f)
                {
                    // Out: fast, easing to a stop at full range.
                    float e = 1f - (1f - t) * (1f - t);
                    pos = p + b.dir * (boomerangRange * e);
                }
                else
                {
                    // Back: accelerate toward wherever the player is NOW.
                    float back = Mathf.Clamp01(t - 1f);
                    Vector3 far = b.visual.position; far.y = p.y;
                    pos = Vector3.MoveTowards(far, p, (8f + 30f * back) * dt);
                    if ((pos - p).sqrMagnitude < 0.6f * 0.6f || t > 3f)
                    {
                        b.live = false;
                        b.visual.gameObject.SetActive(false);
                        b.streak.gameObject.SetActive(false);
                        continue;
                    }
                }
                b.visual.position = pos + Vector3.up * 0.9f;
                Vector3 head = b.visual.position;
                Vector3 tail = head - Vector3.ClampMagnitude((head - b.lastPos) / dt * 0.06f, 2.5f);
                b.streak.SetPosition(0, tail);
                b.streak.SetPosition(1, head);
                b.lastPos = head;
                b.visual.rotation = Quaternion.Euler(0f, b.age * 1440f, 0f);   // spin

                if (Time.time < b.nextScan) continue;
                b.nextScan = Time.time + 0.05f;
                int found = TargetQuery.GatherEnemies(pos, 1.3f, enemyMask);
                for (int i = 0; i < found; i++)
                {
                    var enemy = EnemyOf(TargetQuery.Candidate(i));
                    if (enemy == null || enemy.IsDead) continue;
                    // Once on the way out and once on the way back — the fantasy is "it cuts twice".
                    int key = enemy.GetInstanceID() * 2 + (t <= 1f ? 0 : 1);
                    if (!b.hit.Add(key)) continue;
                    Hit(enemy, damage, 0.5f);
                    FxPool.Play(boomerangHitFx, Chest(enemy), Quaternion.identity, 0.7f);
                    Sfx("sfx.skill.blade.hit", pos, 0.5f, 0.06f);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════ Airstrike + delayed blasts

        struct Blast
        {
            public Vector3 pos;
            public float radius, damage, landAt, push, shake;
            public ParticleSystem fx, missile, decal;
            public float fxNativeRadius;
            public string sfx;
            public ParticleSystem missileInstance;
            public bool missileLaunched;
        }

        readonly List<Blast> _blasts = new(16);

        /// <summary>
        /// Queues a blast that lands after <paramref name="delay"/>. With a marker the player sees
        /// where it will land first — the anticipation beat the instant blasts were missing.
        /// </summary>
        public void ScheduleBlast(Vector3 pos, float radius, float damage, float delay,
                                  ParticleSystem fx, float fxNativeRadius, string sfx,
                                  float shake, float push = 1.2f,
                                  ParticleSystem marker = null, float markerNativeRadius = 1f,
                                  ParticleSystem missile = null, ParticleSystem decal = null)
        {
            if (_blasts.Count >= 16) return;
            pos.y = 0f;
            if (marker != null && delay > 0f)
            {
                FxPool.PlayFor(marker, pos + Vector3.up * 0.05f, Quaternion.identity,
                               radius / Mathf.Max(0.1f, markerNativeRadius), delay);
                SkillFxDirector.Instance?.Converge(pos, radius, new Color(1f, 0.25f, 0.15f, 1f), delay);
            }
            _blasts.Add(new Blast
            {
                pos = pos, radius = radius, damage = damage, landAt = Time.time + delay, push = push,
                shake = shake, fx = fx, fxNativeRadius = fxNativeRadius, sfx = sfx, missile = missile, decal = decal,
            });
        }

        /// <summary>Called by the driver when Airstrike procs.</summary>
        public void Airstrike(SkillRuntime run, Vector3 around, int blasts, float radius)
        {
            float damage = run.PowerDamage(airstrikeBaseDamage, SkillCatalogDefs.AutoAirstrike);
            int found = TargetQuery.GatherEnemies(around, 16f, enemyMask);
            Sfx("sfx.skill.airstrike.mark", around, 0.6f, 0.3f);

            // Only enemies the player can SEE. Measured: picking inside a 13 m sphere put most bombs
            // off the sides of a portrait screen, so the power landed where nobody could watch it.
            _scratchIds.Clear();
            for (int i = 0; i < found; i++)
                if (OnScreen(TargetQuery.CandidatePoint(i))) _scratchIds.Add(i);

            for (int b = 0; b < blasts; b++)
            {
                Vector3 target;
                if (_scratchIds.Count > 0)
                {
                    int pick = Random.Range(0, _scratchIds.Count);
                    target = TargetQuery.CandidatePoint(_scratchIds[pick]);
                    _scratchIds.RemoveAt(pick);           // distinct enemies while they last
                }
                else
                {
                    // Nothing (left) on screen: carpet the ground around the player instead.
                    target = around;
                    for (int tries = 0; tries < 6; tries++)
                    {
                        var r = Random.insideUnitCircle * 4.5f;
                        target = around + new Vector3(r.x, 0f, r.y);
                        if (OnScreen(target)) break;
                    }
                }
                ScheduleBlast(target, radius, damage, airstrikeDelay + b * 0.12f,
                              strikeBlastFx, strikeBlastNativeRadius, "sfx.skill.airstrike.blast", 0.22f, 1.4f,
                              strikeMarkerFx, strikeMarkerNativeRadius, strikeMissileFx, strikeDecalFx);
            }
        }

        void TickBlasts(SkillRuntime run)
        {
            float now = Time.time;
            for (int i = _blasts.Count - 1; i >= 0; i--)
            {
                var b = _blasts[i];

                // The missile is visible for the last moments before impact: it falls INTO the mark.
                if (b.missile != null && !b.missileLaunched && now >= b.landAt - missileFallSeconds)
                {
                    b.missileLaunched = true;
                    b.missileInstance = FxPool.PlayFor(b.missile, b.pos + Vector3.up * 12f,
                                                       Quaternion.LookRotation(Vector3.down), 1f, missileFallSeconds);
                    _blasts[i] = b;
                }
                if (b.missileInstance != null)
                {
                    float k = Mathf.Clamp01(1f - (b.landAt - now) / missileFallSeconds);
                    b.missileInstance.transform.position = b.pos + Vector3.up * (12f * (1f - k * k));
                }
                if (now < b.landAt) continue;

                _blasts.RemoveAt(i);
                if (b.missileInstance != null) b.missileInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                FxPool.Play(b.fx, b.pos + Vector3.up * 0.1f, Quaternion.identity,
                            b.radius / Mathf.Max(0.1f, b.fxNativeRadius));
                if (b.decal != null) FxPool.Play(b.decal, b.pos + Vector3.up * 0.03f, Quaternion.identity,
                                                 b.radius / 1.5f);
                Sfx(b.sfx, b.pos, 0.85f, 0.05f);
                Shake(b.shake);
                SkillFxDirector.Instance?.Pulse(b.pos, b.radius, new Color(1f, 0.62f, 0.2f, 1f), 0.3f, 0.3f);

                int found = TargetQuery.GatherEnemies(b.pos, b.radius, enemyMask);
                for (int c = 0; c < found; c++) Hit(EnemyOf(TargetQuery.Candidate(c)), b.damage, b.push);
            }
        }

        // ═══════════════════════════════════════════════════════════════ Kinetic Shield

        ParticleSystem _shieldAura;
        bool _shieldShown;

        void TickShield(SkillRuntime run, Vector3 p)
        {
            bool charged = run.KineticCharged;
            if (charged && _shieldAura == null && shieldAuraFx != null)
            {
                _shieldAura = Instantiate(shieldAuraFx, _tr);
                _shieldAura.transform.localPosition = Vector3.up * 0.9f;
                _shieldAura.transform.localScale = Vector3.one * 0.7f;
            }
            if (_shieldAura == null || charged == _shieldShown) return;
            _shieldShown = charged;
            _shieldAura.gameObject.SetActive(charged);
            if (charged) Sfx("sfx.skill.shield.ready", p, 0.5f, 0.5f);
        }

        // ═══════════════════════════════════════════════════════════════ evolution moments

        /// <summary>Thunderstorm: a sky strike on the first chain target.</summary>
        public void SkyStrike(Vector3 at)
        {
            FxPool.Play(thunderStrikeFx, at, Quaternion.identity, 1.6f);
            SkillFxDirector.Instance?.Pulse(at, 2.2f, new Color(0.55f, 0.45f, 1f, 1f), 0.3f, 0.35f);
            Sfx("sfx.skill.thunderstorm", at, 0.8f, 0.3f);
            Shake(0.12f);
        }

        /// <summary>Reaper: a small soul burst where an enemy fell.</summary>
        public void ReaperBurst(SkillRuntime run, Vector3 at, float damage)
        {
            ScheduleBlast(at, 2.4f, damage, 0f, reaperFx, 1.5f, "sfx.skill.reaper", 0f, 0.8f);
        }

        /// <summary>The moment an evolution is taken: a flash on the player.</summary>
        public void PlayEvolve()
        {
            FxPool.Play(evolveFx, _tr.position + Vector3.up * 0.5f, Quaternion.identity, 1f);
            Sfx("sfx.skill.evolve", _tr.position, 1f, 0.5f);
            Shake(0.2f);
        }

        /// <summary>Shared by the driver for its own big moments, so there is one shake budget.</summary>
        public void RequestShake(float amount) => Shake(amount);

        /// <summary>Shared sound gate for the driver's powers.</summary>
        public void RequestSfx(string key, Vector3 at, float volume, float minGap) => Sfx(key, at, volume, minGap);

        /// <summary>Clears every live effect — the run ended or restarted.</summary>
        public void ResetForRun()
        {
            _blasts.Clear();
            _patches.Clear();
            _orbitNextHit.Clear();
            for (int k = 0; k < MaxBoomerangs; k++)
            {
                _boom[k].live = false;
                if (_boom[k].visual != null) _boom[k].visual.gameObject.SetActive(false);
                if (_boom[k].streak != null) _boom[k].streak.gameObject.SetActive(false);
            }
        }
    }
}
