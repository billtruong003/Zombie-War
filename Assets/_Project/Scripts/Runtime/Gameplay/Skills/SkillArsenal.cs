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
        [Tooltip("M8 drone model. Children by name: 'Muzzle' (gun tip), 'Rotor*' (spun), and every renderer " +
                 "under 'Glow' takes the rank colour. Empty = the old particle placeholder.")]
        [SerializeField] private GameObject droneModel;
        [SerializeField] private float droneModelScale = 1f;
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
        [Tooltip("Boomerang mesh (lies in its XY plane, spun about Y). Empty = the saw disc.")]
        [SerializeField] private GameObject boomerangModel;
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
        [Tooltip("M8: 0.3 s from 12 m read as a streak; a bomb the eye can follow needs ~0.55 s.")]
        [SerializeField] private float missileFallSeconds = 0.55f;

        [Header("M8 skill pass: souls, frost, orbit path")]
        [Tooltip("A soul flying from a kill to the player (Soul Burst's counter made visible).")]
        [SerializeField] private ParticleSystem soulWispFx;
        [Tooltip("Ice bursting on each enemy Absolute Zero freezes.")]
        [SerializeField] private ParticleSystem freezeBurstFx;
        [Tooltip("Alpha-blended SkillLine material for the faint Orbit Blades path.")]
        [SerializeField] private Material pathMaterial;
        [Tooltip("Faint ring on the Orbit Blades' path. Off: the owner found it ugly (2026-09-29).")]
        [SerializeField] private bool showOrbitPath;

        [Header("M8 skill pass: ground visuals")]
        [Tooltip("ZombieWar/FX/SkillDisc: bomb shadows, frost patches, the Kinetic charge ring.")]
        [SerializeField] private Material discMaterial;
        [Tooltip("ZombieWar/FX/SkillShield: the Kinetic Shield shell (clear inside, glowing rim, back faces culled).")]
        [SerializeField] private Material shieldMaterial;
        [Tooltip("Bombs fall from this height, at an angle along the run's flight line.")]
        [SerializeField] private float bombFallHeight = 14f;
        [SerializeField] private float bombDrift = 5f;

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
            TickShield(run, p, dt);
            TickDelayed();
            TickDiscs();
            TickWisps(p);
            TickRunGun(run, p);
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

        /// <summary>
        /// The rotation the effect was authored with. Many Epic Toon FX prefabs are built lying flat
        /// (root at -90° X); playing them with Quaternion.identity stood them upright, so ground rings
        /// became half-domes cut by the floor (Frost Nova) and decals stood on their edge.
        /// </summary>
        public static Quaternion Flat(ParticleSystem prefab) =>
            prefab != null ? prefab.transform.localRotation : Quaternion.identity;

        /// <summary>The falling bomb shared by Airstrike, Ordnance and Carpet Bomb.</summary>
        public ParticleSystem BombFx => strikeMissileFx;

        // ── delayed effects (a chain's spark lands when its bolt arrives)
        struct DelayedFx { public ParticleSystem fx; public Vector3 pos; public float scale, at; public bool sky; }
        readonly List<DelayedFx> _delayed = new(32);

        /// <summary>Plays an effect after <paramref name="delay"/> seconds, in its authored orientation.</summary>
        public void PlayDelayed(ParticleSystem fx, Vector3 pos, float scale, float delay)
        {
            if (fx == null) return;
            if (delay <= 0f) { FxPool.Play(fx, pos, Flat(fx), scale); return; }
            if (_delayed.Count < 32) _delayed.Add(new DelayedFx { fx = fx, pos = pos, scale = scale, at = Time.time + delay });
        }

        void TickDelayed()
        {
            float now = Time.time;
            for (int i = _delayed.Count - 1; i >= 0; i--)
            {
                var d = _delayed[i];
                if (now < d.at) continue;
                _delayed.RemoveAt(i);
                if (d.sky) DoSkyStrike(d.pos);
                else FxPool.Play(d.fx, d.pos, Flat(d.fx), d.scale);
            }
        }

        // ── ground discs (pooled flat quads, one shared material, colour through a property block)
        class Disc
        {
            public Transform tr;
            public MeshRenderer mr;
            public float bornAt, duration, fromRadius, toRadius;
            public Color color;
            public bool live, fadeIn;
            public float ring;      // 0 = filled disc, >0 = a band of that width (share of the radius)
        }
        readonly List<Disc> _discs = new(48);
        MaterialPropertyBlock _mpb;
        static Mesh _groundQuad;
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int RingId = Shader.PropertyToID("_Ring");
        static readonly int FillId = Shader.PropertyToID("_Fill");

        static Mesh GroundQuad()
        {
            if (_groundQuad != null) return _groundQuad;
            _groundQuad = new Mesh { name = "SkillGroundQuad" };
            _groundQuad.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
            _groundQuad.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            _groundQuad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            _groundQuad.RecalculateBounds();
            return _groundQuad;
        }

        MeshRenderer MakeGroundRenderer(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = GroundQuad();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = discMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.SetActive(false);
            return mr;
        }

        /// <summary>A flat soft disc on the ground that grows from one radius to another and fades
        /// (or fades in, for a bomb shadow that darkens as the bomb gets close).</summary>
        public void ShowDisc(Vector3 at, float fromRadius, float toRadius, Color color, float duration, bool fadeIn, float ring = 0f)
        {
            if (discMaterial == null || _root == null) return;
            Disc d = null;
            for (int i = 0; i < _discs.Count; i++) if (!_discs[i].live) { d = _discs[i]; break; }
            if (d == null)
            {
                if (_discs.Count >= 48) return;               // full: drop the visual, never allocate mid-fight
                var mr = MakeGroundRenderer("disc");
                d = new Disc { tr = mr.transform, mr = mr };
                _discs.Add(d);
            }
            at.y = 0.04f;
            d.tr.position = at;
            d.bornAt = Time.time; d.duration = Mathf.Max(0.05f, duration);
            d.fromRadius = fromRadius; d.toRadius = toRadius; d.color = color; d.fadeIn = fadeIn; d.ring = ring;
            d.live = true;
            d.tr.gameObject.SetActive(true);
            DrawDisc(d, 0f);
        }

        void DrawDisc(Disc d, float t)
        {
            float r = Mathf.Lerp(d.fromRadius, d.toRadius, d.fadeIn ? t * t : 1f - (1f - t) * (1f - t));
            d.tr.localScale = new Vector3(r * 2f, 1f, r * 2f);
            var c = d.color; c.a *= d.fadeIn ? t : 1f - t;
            _mpb ??= new MaterialPropertyBlock();
            _mpb.Clear();
            _mpb.SetColor(ColorId, c);
            _mpb.SetFloat(RingId, d.ring);
            _mpb.SetFloat(FillId, 1f);
            d.mr.SetPropertyBlock(_mpb);
        }

        void TickDiscs()
        {
            float now = Time.time;
            for (int i = 0; i < _discs.Count; i++)
            {
                var d = _discs[i];
                if (!d.live) continue;
                float t = (now - d.bornAt) / d.duration;
                if (t >= 1f) { d.live = false; d.tr.gameObject.SetActive(false); continue; }
                DrawDisc(d, t);
            }
        }

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
                var t = MakeBlade("blade", bladeModel, bladeScale, orbitTrailColor, 0.28f, Color.white, out _bladeTrails[i]);
                _blades[i] = t;
            }
            for (int i = 0; i < MaxBoomerangs; i++)
            {
                var t = MakeBlade("boomerang", boomerangModel, boomerangScale, boomerangTrailColor, 0f, boomerangTint, out _);
                _boom[i].visual = t;
                _boom[i].tipTrail = MakeTipTrail(t, boomerangScale);
            }
        }

        Transform MakeBlade(string name, GameObject meshModel, float scale, Color trailColor, float trailTime, Color tint,
                            out TrailRenderer trail)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(_root, false);
            if (meshModel == null)
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
                var model = Instantiate(meshModel, holder);
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
            // The trail lies in the blade's own plane (owner 2026-09-29: a camera-facing ribbon read
            // as a tilted fin): a flat ribbon on a child, turned so its visible side faces the camera above.
            var flat = new GameObject("trail").transform;
            flat.SetParent(holder, false);
            flat.localRotation = Quaternion.Euler(90f, 0f, 0f);   // ribbon faces up: the trail material is one-sided
            flat.localPosition = Vector3.up * 0.06f;   // just above the blade, which would hide it
            trail = flat.gameObject.AddComponent<TrailRenderer>();
            trail.alignment = LineAlignment.TransformZ;
            trail.time = trailTime;
            trail.minVertexDistance = 0.08f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.15f * scale), new Keyframe(1f, 0f));
            trail.startColor = trailColor;
            trail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            if (trailMaterial != null) trail.sharedMaterial = trailMaterial;
            holder.gameObject.SetActive(false);
            return holder;
        }

        LineRenderer _orbitPath;
        bool _orbitGold;
        static readonly Color OrbitPathColor = new(0.7f, 0.92f, 1f, 0.22f);
        static readonly Color BuzzsawPathColor = new(1f, 0.8f, 0.3f, 0.3f);
        static readonly Color BuzzsawTint = new(1f, 0.82f, 0.4f, 1f);

        /// M8: a faint ring on the blades' path, so the orbit reads as one weapon, not loose shards.
        void DrawOrbitPath(Vector3 p, float radius, bool on, bool gold)
        {
            if (pathMaterial == null || !showOrbitPath) { if (_orbitPath != null) _orbitPath.enabled = false; return; }
            if (_orbitPath == null)
            {
                var go = new GameObject("orbitPath");
                go.transform.SetParent(_root, false);
                _orbitPath = go.AddComponent<LineRenderer>();
                _orbitPath.useWorldSpace = true;
                _orbitPath.loop = true;
                _orbitPath.positionCount = 40;
                _orbitPath.alignment = LineAlignment.View;
                _orbitPath.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _orbitPath.receiveShadows = false;
                _orbitPath.sharedMaterial = pathMaterial;
            }
            if (_orbitPath.gameObject.activeSelf != on) _orbitPath.gameObject.SetActive(on);
            if (!on) return;
            for (int i = 0; i < 40; i++)
            {
                float a = i * Mathf.PI * 2f / 40;
                _orbitPath.SetPosition(i, p + new Vector3(Mathf.Cos(a) * radius, 0.8f, Mathf.Sin(a) * radius));
            }
            var c = gold ? BuzzsawPathColor : OrbitPathColor;
            _orbitPath.startColor = _orbitPath.endColor = c;
            _orbitPath.widthMultiplier = gold ? 0.5f : 0.35f;
        }

        void TintBlades(bool gold)
        {
            _orbitGold = gold;
            _mpb ??= new MaterialPropertyBlock();
            for (int i = 0; i < MaxBlades; i++)
            {
                if (_blades[i] == null) continue;
                foreach (var r in _blades[i].GetComponentsInChildren<MeshRenderer>(true))
                {
                    _mpb.Clear();
                    if (gold) _mpb.SetColor(BaseColorId, BuzzsawTint);
                    r.SetPropertyBlock(_mpb);
                }
                var tr = _bladeTrails[i];
                if (tr == null) continue;
                var c = gold ? new Color(1f, 0.8f, 0.3f, 0.95f) : orbitTrailColor;
                tr.startColor = c;
                tr.endColor = new Color(c.r, c.g, c.b, 0f);
                tr.time = gold ? 0.34f : 0.28f;   // long enough to show past the blade
            }
        }

        void TickOrbit(SkillRuntime run, Vector3 p, float dt)
        {
            int count = Mathf.Min(MaxBlades, run.OrbitBladeCount);
            float radius = run.OrbitRadius;
            bool evolved = run.IsEvolved(SkillCatalogDefs.AutoOrbit);
            if (count > 0 && evolved != _orbitGold) TintBlades(evolved);
            DrawOrbitPath(p, radius, count > 0, evolved);
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
                FxPool.Play(bladeHitFx, Chest(enemy), Flat(bladeHitFx), 0.6f);
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
        /// A thin white trail on the boomerang's tip (the mesh point farthest from its centre), so
        /// the spin draws a loop around the flight path. Flat, like the saws' trails.
        TrailRenderer MakeTipTrail(Transform holder, float scale)
        {
            Vector3 tip = new(0.5f * scale, 0f, 0f);
            var mf = holder.GetComponentInChildren<MeshFilter>(true);
            if (mf != null && mf.sharedMesh != null)
            {
                float best = 0f;
                foreach (var v in mf.sharedMesh.vertices)
                    if (v.sqrMagnitude > best) { best = v.sqrMagnitude; tip = holder.InverseTransformPoint(mf.transform.TransformPoint(v)); }
                tip.y = 0f;
            }
            var go = new GameObject("tipTrail").transform;
            go.SetParent(holder, false);
            go.localPosition = tip;
            go.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.localPosition += Vector3.up * 0.04f;
            var tr = go.gameObject.AddComponent<TrailRenderer>();
            tr.alignment = LineAlignment.TransformZ;
            tr.time = 0.3f;
            tr.minVertexDistance = 0.05f;
            tr.widthCurve = new AnimationCurve(new Keyframe(0f, 0.07f * scale), new Keyframe(1f, 0f));
            tr.startColor = new Color(1f, 1f, 1f, 0.9f);
            tr.endColor = new Color(1f, 1f, 1f, 0f);
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            if (trailMaterial != null) tr.sharedMaterial = trailMaterial;
            return tr;
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

        // M8 drone rig (model path)
        readonly Transform[] _rigs = new Transform[MaxDrones];
        readonly Transform[] _rigMuzzle = new Transform[MaxDrones];
        readonly List<Transform>[] _rigRotors = new List<Transform>[MaxDrones];
        readonly Renderer[][] _rigGlow = new Renderer[MaxDrones][];
        /// Merged drone model (one mesh): its second material is the rotor smear, tinted by rank.
        readonly Renderer[] _rigRotorMat = new Renderer[MaxDrones];
        static readonly int InkColorId = Shader.PropertyToID("_InkColor");
        readonly TrailRenderer[] _rigTrail = new TrailRenderer[MaxDrones];
        readonly int[] _burstLeft = new int[MaxDrones];
        int _rigColourKey = -1;
        const int BurstShots = 3;
        const float BurstGap = 0.08f;

        /// M8 emissive colour by upgrade: cyan → green → gold, and a hot magenta for the Squadron.
        static Color DroneColour(SkillRuntime run)
        {
            // Kept at or just over 1: brighter values bloom and tone-map to white and lose the hue.
            if (run.IsEvolved(SkillCatalogDefs.AutoDrone)) return new Color(1.15f, 0.2f, 1.0f, 1f);
            switch (run.RankOf(SkillCatalogDefs.AutoDrone))
            {
                case 1: return new Color(0.15f, 0.8f, 1.1f, 1f);
                case 2: return new Color(0.3f, 1.1f, 0.3f, 1f);
                default: return new Color(1.15f, 0.7f, 0.1f, 1f);
            }
        }

        Transform BuildRig(int i)
        {
            var go = Instantiate(droneModel, _root);
            go.name = "drone" + i;
            go.transform.localScale = Vector3.one * droneModelScale;
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) Destroy(col);
            _rigMuzzle[i] = FindChild(go.transform, "Muzzle") ?? go.transform;
            _rigRotors[i] = new List<Transform>(4);
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("Rotor")) _rigRotors[i].Add(t);
            var glow = FindChild(go.transform, "Glow");
            _rigGlow[i] = glow != null ? glow.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            _rigRotorMat[i] = null;
            if (glow == null)
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    if (r.sharedMaterials.Length > 1) { _rigRotorMat[i] = r; break; }
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            // From the tail, above the hull: a trail from the pivot sat under the body and never showed.
            var tail = new GameObject("trail").transform;
            tail.SetParent(go.transform, false);
            var mesh = go.GetComponentInChildren<MeshFilter>(true);
            if (mesh != null && mesh.sharedMesh != null)
            {
                var bb = mesh.sharedMesh.bounds;
                tail.position = mesh.transform.TransformPoint(new Vector3(bb.center.x, bb.max.y, bb.min.z));
            }
            var trail = tail.gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.6f;
            trail.minVertexDistance = 0.05f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.07f), new Keyframe(1f, 0f));
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (trailMaterial != null) trail.sharedMaterial = trailMaterial;
            trail.emitting = false;
            _rigTrail[i] = trail;
            _rigColourKey = -1;
            return go.transform;
        }

        static Transform FindChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        void TickDrones(SkillRuntime run, Vector3 p, float dt)
        {
            if (droneModel == null) { TickDroneParticles(run, p, dt); return; }

            int count = Mathf.Min(MaxDrones, run.DroneCount);
            // Same damage per second as before, delivered in bursts of three: bursts read as a gunner.
            float burstInterval = BurstShots / Mathf.Max(0.1f, run.DroneShotsPerSecond);
            float t = Time.time;
            bool squad = run.IsEvolved(SkillCatalogDefs.AutoDrone);
            Color colour = DroneColour(run);
            int key = squad ? 9 : run.RankOf(SkillCatalogDefs.AutoDrone);

            for (int i = 0; i < MaxDrones; i++)
            {
                bool on = i < count;
                if (on && _rigs[i] == null)
                {
                    _rigs[i] = BuildRig(i);
                    _rigs[i].position = p + Vector3.up * 2.2f;
                    _droneNextShot[i] = t + burstInterval * (i + 1) / (count + 1);
                }
                var rig = _rigs[i];
                if (rig == null) continue;
                if (rig.gameObject.activeSelf != on) rig.gameObject.SetActive(on);
                if (!on) continue;

                // Flight: a lazy figure-eight around the player, each drone on its own phase, so the
                // squad weaves instead of sitting still at the shoulder.
                float ph = t * 0.9f + i * 2.1f;
                Vector3 off = new Vector3(Mathf.Sin(ph) * 1.9f, 2.2f + Mathf.Sin(t * 3f + i) * 0.12f,
                                          Mathf.Sin(ph * 2f) * 0.9f + 0.4f);
                Vector3 before = rig.position;
                rig.position = Vector3.SmoothDamp(rig.position, p + off, ref _droneVel[i], 0.3f);
                Vector3 vel = (rig.position - before) / Mathf.Max(0.0001f, dt);

                // Aim: turn to the nearest enemy in range, else along the flight; bank into the turn.
                Vector3 look = vel; look.y = 0f;
                int found = TargetQuery.GatherEnemies(rig.position, droneRange, enemyMask);
                int best = TargetQuery.Nearest(found, rig.position);
                ZombieBase target = best >= 0 ? TargetQuery.CandidateEnemy(best) : null;
                if (target != null) { look = target.transform.position - rig.position; look.y = 0f; }
                if (look.sqrMagnitude > 0.001f)
                {
                    var yaw = Quaternion.LookRotation(look.normalized);
                    Vector3 local = Quaternion.Inverse(yaw) * vel;
                    var bank = Quaternion.Euler(Mathf.Clamp(local.z * 6f, -18f, 18f), 0f, Mathf.Clamp(-local.x * 8f, -28f, 28f));
                    rig.rotation = Quaternion.Slerp(rig.rotation, yaw * bank, 1f - Mathf.Exp(-10f * dt));
                }
                foreach (var r in _rigRotors[i]) r.Rotate(0f, 2200f * dt, 0f, Space.Self);

                if (_rigTrail[i] != null)
                {
                    // Always a thin white trail that fades out (owner 2026-09-29).
                    _rigTrail[i].emitting = true;
                    _rigTrail[i].startColor = new Color(1f, 1f, 1f, 0.8f);
                    _rigTrail[i].endColor = new Color(1f, 1f, 1f, 0f);
                }

                if (t < _droneNextShot[i] || target == null || target.IsDead) continue;
                if (_burstLeft[i] <= 0) _burstLeft[i] = BurstShots;
                _burstLeft[i]--;
                _droneNextShot[i] = t + (_burstLeft[i] > 0 ? BurstGap : burstInterval - (BurstShots - 1) * BurstGap);
                DroneShoot(run, _rigMuzzle[i].position, target, colour);
            }

            if (key != _rigColourKey)
            {
                _rigColourKey = key;
                _mpb ??= new MaterialPropertyBlock();
                for (int i = 0; i < MaxDrones; i++)
                {
                    if (_rigRotorMat[i] != null)
                    {
                        _mpb.Clear();
                        _mpb.SetColor(InkColorId, colour);
                        _rigRotorMat[i].SetPropertyBlock(_mpb, 1);
                    }
                    if (_rigGlow[i] == null) continue;
                    foreach (var r in _rigGlow[i])
                    {
                        _mpb.Clear();
                        _mpb.SetColor(BaseColorId, colour);
                        _mpb.SetColor(EmissionId, colour);
                        r.SetPropertyBlock(_mpb);
                    }
                }
            }
        }

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        /// The old placeholder: a particle sphere per drone (used when no model is assigned).
        void TickDroneParticles(SkillRuntime run, Vector3 p, float dt)
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

                float fan = count == 1 ? -50f : -80f + i * 80f;
                float a = (fan + t * 25f) * Mathf.Deg2Rad;
                Vector3 target = p + new Vector3(Mathf.Cos(a) * 1.3f, 2.1f + Mathf.Sin(t * 3f + i) * 0.12f, Mathf.Sin(a) * 1.3f);
                d.transform.position = Vector3.SmoothDamp(d.transform.position, target, ref _droneVel[i], 0.18f);

                if (t < _droneNextShot[i]) continue;
                _droneNextShot[i] = t + interval;
                int found = TargetQuery.GatherEnemies(d.transform.position, droneRange, enemyMask);
                int best = TargetQuery.Nearest(found, d.transform.position);
                var enemy = best >= 0 ? TargetQuery.CandidateEnemy(best) : null;
                if (enemy != null && !enemy.IsDead) DroneShoot(run, d.transform.position, enemy, DroneColour(run));
            }
        }

        void DroneShoot(SkillRuntime run, Vector3 from, ZombieBase enemy, Color colour)
        {
            Vector3 to = Chest(enemy);
            // The player's own gun pipeline (muzzle, tracer, impact), tinted with the drone's rank colour.
            if (droneTracer != null) TracerPool.Play(droneTracer, from, to, new Color(colour.r, colour.g, colour.b, 1f), 0.45f);
            FxPool.Play(droneMuzzleFx, from, Quaternion.LookRotation(to - from), 0.35f);
            FxPool.Play(droneHitFx, to, Flat(droneHitFx), 0.45f);
            Sfx("sfx.skill.drone", from, 0.3f, 0.05f);

            Hit(enemy, run.PowerDamage(droneBaseDamage, SkillCatalogDefs.AutoDrone), 0.15f);

            // Drone Squadron: a kill can pay out a coin on the spot.
            if (enemy.IsDead && run.IsEvolved(SkillCatalogDefs.AutoDrone) && Random.value < 0.1f)
                PickupManager.SpawnBonusCoin(enemy.transform.position, 1);
        }

        // ═══════════════════════════════════════════════════════════════ Frost Nova

        /// <summary>Called by the driver when Frost Nova procs.</summary>
        public void FrostNova(SkillRuntime run, Vector3 centre, float radius)
        {
            FxPool.Play(frostNovaFx, centre + Vector3.up * 0.1f, Flat(frostNovaFx),
                        radius / Mathf.Max(0.1f, frostNovaNativeRadius));
            // A band of frost racing out to the edge of the chilled area and melting (a filled disc
            // this size read as fog over the whole screen).
            ShowDisc(centre, radius * 0.3f, radius, new Color(0.7f, 0.93f, 1f, run.FrostFreezes ? 0.55f : 0.45f),
                     run.FrostFreezes ? 1.1f : 0.8f, false, 0.14f);
            Sfx("sfx.skill.frost", centre, 0.9f, 0.2f);
            Shake(run.FrostFreezes ? 0.25f : 0.12f);
            // The ring IS the hitbox: it expands to exactly the radius that was checked.
            SkillFxDirector.Instance?.Pulse(centre, radius, new Color(0.55f, 0.9f, 1f, 0.9f), 0.4f, 0.28f);

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
                    // Absolute Zero: ice bursts on each enemy it locks (capped, a crowd is a lot of ice).
                    if (i < 12) PlayDelayed(freezeBurstFx, Chest(enemy), 0.28f, 0.05f + 0.02f * i);
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
        readonly List<FirePatch> _patches = new(24);
        float _fireTickAt;
        const float FireTick = 0.25f;

        void TickFireTrail(SkillRuntime run, Vector3 p, float dt)
        {
            int drops = run.ConsumeFireTrailDrops();
            float now = Time.time;
            for (int i = 0; i < drops && _patches.Count < 24; i++)
            {
                _patches.Add(new FirePatch { pos = p, until = now + firePatchSeconds });
                FxPool.PlayFor(firePatchFx, p + Vector3.up * 0.05f, Flat(firePatchFx),
                               0.75f * firePatchRadius / Mathf.Max(0.1f, firePatchNativeRadius), firePatchSeconds);
                // M8: a glowing burn under the flames. Patches overlap, so the trail reads as one
                // continuous strip of fire instead of separate candles.
                ShowDisc(p, firePatchRadius * 1.05f, firePatchRadius * 0.8f, new Color(1f, 0.42f, 0.08f, 0.5f), firePatchSeconds, false);
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
            public TrailRenderer tipTrail;
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
                _boom[k].tipTrail?.Clear();
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
                        continue;
                    }
                }
                b.visual.position = pos + Vector3.up * 0.9f;
                Vector3 head = b.visual.position;
                b.lastPos = head;
                // Two turns a second: fast enough to read as a boomerang, slow enough not to blur.
                b.visual.rotation = Quaternion.Euler(0f, b.age * 720f, 0f);

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
            public Vector3 dropFrom;
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
                                  ParticleSystem missile = null, ParticleSystem decal = null, Vector3? flightDir = null)
        {
            if (_blasts.Count >= 16) return;
            pos.y = 0f;
            if (delay > 0f)
            {
                // M8 telegraph: one thin ring closing in, plus the shadow of what is coming, darkening
                // and tightening as it gets close. The old marker (a magic circle and a thick red ring
                // per target, at full radius) covered the screen with five blasts.
                SkillFxDirector.Instance?.Converge(pos, radius, new Color(1f, 0.35f, 0.2f, 1f), delay, 0.08f);
                if (missile != null) ShowDisc(pos, radius * 0.9f, radius * 0.35f, new Color(0.05f, 0.03f, 0.02f, 0.5f), delay, true);
            }
            Vector3 dir = flightDir ?? Vector3.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            _blasts.Add(new Blast
            {
                pos = pos, radius = radius, damage = damage, landAt = Time.time + delay, push = push,
                shake = shake, fx = fx, fxNativeRadius = fxNativeRadius, sfx = sfx, missile = missile, decal = decal,
                // Falls in at an angle along the flight line, so it reads as dropped from a pass overhead.
                dropFrom = pos - dir.normalized * bombDrift + Vector3.up * bombFallHeight,
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

            // One pass overhead: every bomb comes from the same direction and they land in order
            // along that line — a bombing run, not five random pops.
            float heading = Random.Range(0f, 360f);
            Vector3 flight = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
            _runTargets.Clear();
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
                _runTargets.Add(target);
            }
            _runTargets.Sort((x, y) => Vector3.Dot(x, flight).CompareTo(Vector3.Dot(y, flight)));
            for (int b = 0; b < _runTargets.Count; b++)
                ScheduleBlast(_runTargets[b], radius, damage, airstrikeDelay + b * 0.14f,
                              strikeBlastFx, strikeBlastNativeRadius, "sfx.skill.airstrike.blast", 0.22f, 1.4f,
                              null, 1f, strikeMissileFx, strikeDecalFx, flight);
        }

        readonly List<Vector3> _runTargets = new(8);

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
                    b.missileInstance = FxPool.PlayFor(b.missile, b.dropFrom,
                                                       Quaternion.LookRotation(b.pos - b.dropFrom), 1f, missileFallSeconds);
                    Sfx("sfx.skill.airstrike.mark", b.dropFrom, 0.35f, 0.25f);
                    _blasts[i] = b;
                }
                if (b.missileInstance != null)
                {
                    // Accelerating fall into the shadow.
                    float k = Mathf.Clamp01(1f - (b.landAt - now) / missileFallSeconds);
                    b.missileInstance.transform.position = Vector3.Lerp(b.dropFrom, b.pos + Vector3.up * 0.3f, k * k);
                }
                if (now < b.landAt) continue;

                _blasts.RemoveAt(i);
                if (b.missileInstance != null) b.missileInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                // Capped: the pack's smoke grows past the blast radius, and big blasts buried the crowd.
                FxPool.Play(b.fx, b.pos + Vector3.up * 0.1f, Flat(b.fx),
                            Mathf.Min(b.radius / Mathf.Max(0.1f, b.fxNativeRadius), 1.3f));
                if (b.decal != null) FxPool.Play(b.decal, b.pos + Vector3.up * 0.03f, Flat(b.decal),
                                                 b.radius / 1.5f);
                Sfx(b.sfx, b.pos, 0.85f, 0.05f);
                Shake(b.shake);
                SkillFxDirector.Instance?.Pulse(b.pos, b.radius, new Color(1f, 0.62f, 0.2f, 0.85f), 0.3f, 0.18f);

                int found = TargetQuery.GatherEnemies(b.pos, b.radius, enemyMask);
                for (int c = 0; c < found; c++) Hit(EnemyOf(TargetQuery.Candidate(c)), b.damage, b.push);
            }
        }

        // ═══════════════════════════════════════════════════════════════ Kinetic Shield

        ParticleSystem _shieldAura;
        bool _shieldShown;
        Transform _shell;
        MeshRenderer _shellRenderer;
        MeshRenderer _chargeRing;
        float _shellFade, _shellFlash;
        static readonly int FadeId = Shader.PropertyToID("_Fade");
        static readonly int FlashId = Shader.PropertyToID("_Flash");

        /// <summary>
        /// M8 (owner: "transparent inside, rendered on the outside"): a shell around the player that is
        /// clear in the middle and glows at its rim, drawn with back faces culled so nothing is drawn
        /// over the player. Sits fully above the ground (no half-dome cut by the floor). While it
        /// recharges, a thin ring at the feet fills with the distance walked.
        /// </summary>
        void TickShield(SkillRuntime run, Vector3 p, float dt)
        {
            bool has = run.Has(SkillCatalogDefs.UniKinetic);
            bool charged = has && run.KineticCharged;

            if (shieldMaterial == null)
            {
                // Fallback: the old particle aura.
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
                return;
            }

            if (has && _shell == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "KineticShell";
                Destroy(go.GetComponent<Collider>());
                _shell = go.transform;
                _shell.SetParent(_tr, false);
                _shell.localPosition = Vector3.up * 1.05f;
                _shell.localScale = Vector3.one * 2.1f;   // radius 1.05 around y 1.05: rests on the ground
                _shellRenderer = go.GetComponent<MeshRenderer>();
                _shellRenderer.sharedMaterial = shieldMaterial;
                _shellRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _shellRenderer.receiveShadows = false;
            }
            if (has && _chargeRing == null && discMaterial != null)
            {
                _chargeRing = MakeGroundRenderer("KineticChargeRing");
                _chargeRing.transform.SetParent(_tr, false);
                _chargeRing.transform.localPosition = Vector3.up * 0.04f;
                _chargeRing.transform.localScale = new Vector3(2.3f, 1f, 2.3f);
            }

            if (charged != _shieldShown)
            {
                _shieldShown = charged;
                if (charged) { _shellFlash = 0.6f; Sfx("sfx.skill.shield.ready", p, 0.5f, 0.5f); }
            }
            _shellFade = Mathf.MoveTowards(_shellFade, charged ? 1f : 0f, dt * 4f);
            _shellFlash = Mathf.MoveTowards(_shellFlash, 0f, dt * 2.5f);

            if (_shell != null)
            {
                bool visible = has && _shellFade > 0.001f;
                if (_shell.gameObject.activeSelf != visible) _shell.gameObject.SetActive(visible);
                if (visible)
                {
                    _mpb ??= new MaterialPropertyBlock();
                    _mpb.Clear();
                    _mpb.SetFloat(FadeId, _shellFade);
                    _mpb.SetFloat(FlashId, _shellFlash);
                    _shellRenderer.SetPropertyBlock(_mpb);
                }
            }
            if (_chargeRing != null)
            {
                float fill = run.KineticChargeFraction;
                bool ring = has && !charged && fill > 0.01f;
                if (_chargeRing.gameObject.activeSelf != ring) _chargeRing.gameObject.SetActive(ring);
                if (ring)
                {
                    _mpb ??= new MaterialPropertyBlock();
                    _mpb.Clear();
                    _mpb.SetColor(ColorId, new Color(0.7f, 0.55f, 1f, 0.75f));
                    _mpb.SetFloat(RingId, 0.07f);
                    _mpb.SetFloat(FillId, fill);
                    _chargeRing.SetPropertyBlock(_mpb);
                }
            }
        }

        /// <summary>The shield ate a hit: the shell lights up as it breaks (the shard burst is the driver's).</summary>
        public void OnShieldBlocked()
        {
            _shellFlash = 1f;
            _shellFade = Mathf.Max(_shellFade, 0.8f);
        }

        // ═══════════════════════════════════════════════════════════════ evolution moments

        /// <summary>Thunderstorm: a bolt from the sky on a chain target, landing when its arc arrives.</summary>
        public void SkyStrike(Vector3 at, float delay = 0f)
        {
            if (delay <= 0f) { DoSkyStrike(at); return; }
            if (_delayed.Count < 32) _delayed.Add(new DelayedFx { pos = at, at = Time.time + delay, sky = true });
        }

        void DoSkyStrike(Vector3 at)
        {
            FxPool.Play(thunderStrikeFx, at, Flat(thunderStrikeFx), 1.3f);
            SkillFxDirector.Instance?.Pulse(at, 1.6f, new Color(0.62f, 0.45f, 1f, 0.9f), 0.28f, 0.2f);
            Sfx("sfx.skill.thunderstorm", at, 0.8f, 0.3f);
            Shake(0.08f);
        }

        /// <summary>Reaper: a small soul burst where an enemy fell.</summary>
        public void ReaperBurst(SkillRuntime run, Vector3 at, float damage)
        {
            // M8: a scythe sweep where the enemy fell — a violet crescent and a soul burst — instead of
            // a generic blast with an orange ring.
            at.y = 0f;
            FxPool.Play(reaperFx, at + Vector3.up * 0.3f, Flat(reaperFx), 0.8f);
            SkillFxDirector.Instance?.ConeWave(at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward,
                                               230f, 2.4f, new Color(0.62f, 0.3f, 1f, 0.9f), 0.3f);
            SoulWisp(at);
            Sfx("sfx.skill.reaper", at, 0.8f, 0f);
            int found = TargetQuery.GatherEnemies(at, 2.4f, enemyMask);
            for (int c = 0; c < found; c++) Hit(EnemyOf(TargetQuery.Candidate(c)), damage, 0.8f);
        }

        // ── Run & Gun: cyan footprints of speed while the ramp is up
        Vector3 _runGunLast;
        float _runGunNext;

        void TickRunGun(SkillRuntime run, Vector3 p)
        {
            float ramp = run.RunGunRamp;
            bool moving = (p - _runGunLast).sqrMagnitude > 0.0004f;
            _runGunLast = p;
            if (ramp < 0.3f || !moving || Time.time < _runGunNext) return;
            _runGunNext = Time.time + 0.12f;
            ShowDisc(p, 0.5f, 0.25f, new Color(0.55f, 0.92f, 1f, 0.3f + 0.25f * ramp), 0.35f, false, 0.25f);
        }

        // ── soul wisps: a kill's soul flies to the player (Soul Burst / Reaper)
        struct Wisp { public ParticleSystem ps; public Vector3 from; public float bornAt; }
        readonly List<Wisp> _wisps = new(12);
        const float WispSeconds = 0.45f;

        public void SoulWisp(Vector3 from)
        {
            if (soulWispFx == null || _wisps.Count >= 12) return;
            from.y = 0.9f;
            var ps = FxPool.PlayFor(soulWispFx, from, Quaternion.identity, 0.45f, WispSeconds);
            if (ps != null) _wisps.Add(new Wisp { ps = ps, from = from, bornAt = Time.time });
        }

        void TickWisps(Vector3 p)
        {
            float now = Time.time;
            Vector3 chest = p + Vector3.up * 1.1f;
            for (int i = _wisps.Count - 1; i >= 0; i--)
            {
                var w = _wisps[i];
                float k = (now - w.bornAt) / WispSeconds;
                if (w.ps == null || k >= 1f) { _wisps.RemoveAt(i); continue; }
                // Rises, then dives into the player.
                Vector3 pos = Vector3.Lerp(w.from, chest, k * k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 1.2f);
                w.ps.transform.rotation = Quaternion.LookRotation((pos - w.ps.transform.position).sqrMagnitude > 1e-6f ? pos - w.ps.transform.position : Vector3.up);
                w.ps.transform.position = pos;
            }
        }

        /// <summary>The moment an evolution is taken: a flash on the player.</summary>
        public void PlayEvolve()
        {
            FxPool.Play(evolveFx, _tr.position + Vector3.up * 0.5f, Flat(evolveFx), 1f);
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
            _delayed.Clear();
            _wisps.Clear();
            for (int i = 0; i < _discs.Count; i++) { _discs[i].live = false; _discs[i].tr.gameObject.SetActive(false); }
            _patches.Clear();
            _orbitNextHit.Clear();
            for (int k = 0; k < MaxBoomerangs; k++)
            {
                _boom[k].live = false;
                if (_boom[k].visual != null) _boom[k].visual.gameObject.SetActive(false);
                _boom[k].tipTrail?.Clear();
            }
        }
    }
}
