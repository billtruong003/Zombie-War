using System;
using System.Collections.Generic;
using UnityEngine;
using BillGameCore;
using ZombieWar.Skills.Powers;

namespace ZombieWar.Skills
{
    /// <summary>
    /// Host of the power modules (<see cref="PowerModule"/>, one per power under Skills/Powers) and of
    /// the services they share: one camera-shake budget, one sound gate, pooled ground discs, delayed
    /// effects, delayed blasts and soul wisps. Every power's assets live in <see cref="SkillFxLibrary"/>.
    ///
    /// <b>Game-feel contract</b> (owner, 2026-09-26: "the feel when it shows is not right yet"). Every
    /// power must:
    /// <list type="number">
    /// <item>have a visual the size of its hitbox — effects are scaled from their measured native
    /// radius, never left at prefab size;</item>
    /// <item>have a sound, throttled so a crowd does not turn it into noise;</item>
    /// <item>telegraph anything that lands later (a shadow and a closing ring before the bomb);</item>
    /// <item>push or tint what it hits, so the player sees WHICH enemies it affected;</item>
    /// <item>shake the camera only for the big moments, from one shared budget.</item>
    /// </list>
    ///
    /// Numbers (counts, radii, rates) come from <see cref="SkillRuntime"/>, the same helpers the
    /// card text reads. Nothing here allocates per frame in steady state.
    /// </summary>
    [DisallowMultipleComponent]
    public class SkillArsenal : MonoBehaviour, IPowerHost
    {
        [Tooltip("Every power's assets and tuning (Assets/_Project/Data/Skills/SkillFxLibrary.asset).")]
        [SerializeField] private SkillFxLibrary library;

        public static SkillArsenal Instance { get; private set; }

        Transform _tr, _root;
        CameraFollow _camera;
        Camera _cam;
        MaterialPropertyBlock _mpb;

        // The modules, in tick order. A new power: a new module, a library section, one line here.
        readonly List<PowerModule> _modules = new(16);
        readonly Dictionary<string, PowerModule> _byProc = new(16);
        KineticShieldPower _shield;
        PoisonPower _poison;
        GuardianAngelPower _guardian;

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
            Register(new OrbitPower());
            Register(new DronePower());
            Register(new FireTrailPower());
            Register(new BoomerangPower());
            Register(new FrostNovaPower());
            Register(new AirstrikePower());
            Register(new OrdnancePower());
            Register(new ChainPower());
            Register(new SelfBurstPower());
            Register(_shield = new KineticShieldPower());
            Register(new RunGunTrail());
            Register(_poison = new PoisonPower());
            Register(new GunModsPower());
            Register(new LauncherPower());
            Register(_guardian = new GuardianAngelPower());
        }

        void Register(PowerModule m)
        {
            _modules.Add(m);
            foreach (var id in m.ProcIds) _byProc[id] = m;
            m.Attach(this);
        }

        void Update()
        {
            var run = SkillRuntime.Active;
            if (run == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;   // paused (level-up screen): nothing moves, nothing hits

            Vector3 p = _tr.position;
            for (int i = 0; i < _modules.Count; i++) _modules[i].Tick(run, p, dt);
            TickBlasts();
            TickDelayed();
            TickDiscs();
            TickWaves();
            TickWisps(p);
            _shakeBudget = Mathf.Max(0f, _shakeBudget - dt * 1.5f);
        }

        // ═══════════════════════════════════════════════════════════════ routing (driver → modules)

        /// <summary>A power's proc from <see cref="SkillRuntime.PollPowers"/>. False: no module owns it.</summary>
        public bool Dispatch(SkillRuntime run, in SkillRuntime.PowerProc proc, Vector3 origin)
        {
            if (!_byProc.TryGetValue(proc.skillId, out var m)) return false;
            m.OnProc(run, proc, origin);
            return true;
        }

        public void OnKill(SkillRuntime run, Vector3 at)
        {
            for (int i = 0; i < _modules.Count; i++) _modules[i].OnKill(run, at);
        }

        /// <summary>Kinetic Shield ate a hit — make it legible, or the card reads as a bug.</summary>
        public void OnShieldBlocked() => _shield?.Break();

        /// <summary>A bullet from the player's gun landed (the weapon calls this after the damage).</summary>
        public void OnGunHit(SkillRuntime run, ZombieBase enemy, Vector3 point, float damage, bool crit)
        {
            for (int i = 0; i < _modules.Count; i++) _modules[i].OnGunHit(run, enemy, point, damage, crit);
        }

        /// <summary>The grenade launcher's shell burst.</summary>
        public void OnLauncherBlast(SkillRuntime run, Vector3 at, float radius, float damage)
        {
            for (int i = 0; i < _modules.Count; i++) _modules[i].OnLauncherBlast(run, at, radius, damage);
        }

        /// <summary>Guardian Angel turned a fatal hit into a heal.</summary>
        public void OnGuardianAngel() => _guardian?.Save();

        public void Poison(ZombieBase enemy, int stacks, float dpsPerStack, float seconds, string source) =>
            _poison?.Apply(enemy, stacks, dpsPerStack, seconds, source);

        /// <summary>The moment an evolution is taken: a flash on the player.</summary>
        public void PlayEvolve()
        {
            var fx = library != null ? library.shared.evolveFx : null;
            FxPool.Play(fx, _tr.position + Vector3.up * 0.5f, PowerKit.Flat(fx), 1f);
            Sfx("sfx.skill.evolve", _tr.position, 1f, 0.5f);
            Shake(0.2f);
        }

        /// <summary>Clears every live effect — the run ended or restarted.</summary>
        public void ResetForRun()
        {
            _blasts.Clear();
            _delayed.Clear();
            _wisps.Clear();
            for (int i = 0; i < _discs.Count; i++) { _discs[i].live = false; _discs[i].tr.gameObject.SetActive(false); }
            for (int i = 0; i < _waves.Count; i++) { _waves[i].live = false; _waves[i].tr.gameObject.SetActive(false); }
            for (int i = 0; i < _modules.Count; i++) _modules[i].ResetForRun();
        }

        // ═══════════════════════════════════════════════════════════════ IPowerHost

        public Transform Root => _root;
        public Transform Player => _tr;
        public SkillFxLibrary Library => library;
        public LayerMask EnemyMask => library != null ? library.shared.enemyMask : (LayerMask)~0;
        public MaterialPropertyBlock Block => _mpb ??= new MaterialPropertyBlock();

        float _shakeBudget;

        public void Shake(float amount)
        {
            if (_camera == null) return;
            float allowed = Mathf.Max(0f, 0.45f - _shakeBudget);
            float a = Mathf.Min(amount, allowed);
            if (a <= 0f) return;
            _shakeBudget += a;
            _camera.Shake(a);
        }

        readonly Dictionary<string, float> _nextSfx = new(16);

        public void Sfx(string key, Vector3 at, float volume = 0.8f, float minGap = 0.08f)
        {
            if (string.IsNullOrEmpty(key)) return;
            float now = Time.time;
            if (_nextSfx.TryGetValue(key, out float next) && now < next) return;
            _nextSfx[key] = now + minGap;
            // Big moments must not be the first voice dropped when the horde gets loud.
            Bill.Audio?.PlayCue(key, at, volume >= 0.7f ? SfxPriority.High : SfxPriority.Medium, volume);
        }

        public bool OnScreen(Vector3 world)
        {
            const float margin = 0.08f;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return true;
            var v = _cam.WorldToViewportPoint(world);
            return v.z > 0f && v.x > margin && v.x < 1f - margin && v.y > margin && v.y < 1f - margin;
        }

        // ── delayed effects and actions (a chain's spark lands when its bolt arrives)
        struct DelayedFx { public ParticleSystem fx; public Action<Vector3> action; public Vector3 pos; public float scale, at; }
        readonly List<DelayedFx> _delayed = new(32);

        public void PlayDelayed(ParticleSystem fx, Vector3 pos, float scale, float delay)
        {
            if (fx == null) return;
            if (delay <= 0f) { FxPool.Play(fx, pos, PowerKit.Flat(fx), scale); return; }
            if (_delayed.Count < 32) _delayed.Add(new DelayedFx { fx = fx, pos = pos, scale = scale, at = Time.time + delay });
        }

        public void Delay(float delay, Action<Vector3> action, Vector3 at)
        {
            if (action == null) return;
            if (delay <= 0f) { action(at); return; }
            if (_delayed.Count < 32) _delayed.Add(new DelayedFx { action = action, pos = at, at = Time.time + delay });
        }

        void TickDelayed()
        {
            float now = Time.time;
            for (int i = _delayed.Count - 1; i >= 0; i--)
            {
                var d = _delayed[i];
                if (now < d.at) continue;
                _delayed.RemoveAt(i);
                if (d.action != null) d.action(d.pos);
                else FxPool.Play(d.fx, d.pos, PowerKit.Flat(d.fx), d.scale);
            }
        }

        // ── ground discs (pooled flat quads, one shared material, colour through a property block)
        sealed class Disc
        {
            public Transform tr;
            public MeshRenderer mr;
            public float bornAt, duration, fromRadius, toRadius;
            public Color color;
            public bool live, fadeIn;
            public float ring;      // 0 = filled disc, >0 = a band of that width (share of the radius)
        }
        const int MaxDiscs = 48;
        readonly List<Disc> _discs = new(MaxDiscs);
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
            _groundQuad.colors = new[] { Color.white, Color.white, Color.white, Color.white };   // ToonErode reads vertex colour
            _groundQuad.RecalculateBounds();
            return _groundQuad;
        }

        public MeshRenderer MakeGroundRenderer(string name)
        {
            var mat = library != null ? library.shared.discMaterial : null;
            if (mat == null || _root == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = GroundQuad();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.SetActive(false);
            return mr;
        }

        public void ShowDisc(Vector3 at, float fromRadius, float toRadius, Color color, float duration, bool fadeIn, float ring = 0f)
        {
            Disc d = null;
            for (int i = 0; i < _discs.Count; i++) if (!_discs[i].live) { d = _discs[i]; break; }
            if (d == null)
            {
                if (_discs.Count >= MaxDiscs) return;               // full: drop the visual, never allocate mid-fight
                var mr = MakeGroundRenderer("disc");
                if (mr == null) return;
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
            var mpb = Block;
            mpb.Clear();
            mpb.SetColor(ColorId, c);
            mpb.SetFloat(RingId, d.ring);
            mpb.SetFloat(FillId, 1f);
            d.mr.SetPropertyBlock(mpb);
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

        // ── toon shockwaves (ToonErode ring on a ground quad; erosion and tint by property block)
        sealed class Wave { public Transform tr; public MeshRenderer mr; public float bornAt, duration, from, to; public Color color; public bool live; }
        const int MaxWaves = 12;
        readonly List<Wave> _waves = new(MaxWaves);
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int ErodeId = Shader.PropertyToID("_Erode");

        public void Shockwave(Vector3 at, float fromRadius, float toRadius, Color color, float duration)
        {
            var mat = library != null ? library.shared.shockwaveMaterial : null;
            if (mat == null || _root == null) return;
            Wave w = null;
            for (int i = 0; i < _waves.Count; i++) if (!_waves[i].live) { w = _waves[i]; break; }
            if (w == null)
            {
                if (_waves.Count >= MaxWaves) return;          // full: drop the visual, never allocate mid-fight
                var mr = MakeGroundRenderer("wave");
                if (mr == null) return;
                mr.sharedMaterial = mat;
                w = new Wave { tr = mr.transform, mr = mr };
                _waves.Add(w);
            }
            at.y = 0.06f;                                       // just above the discs
            w.tr.position = at;
            w.bornAt = Time.time; w.duration = Mathf.Max(0.05f, duration);
            w.from = fromRadius; w.to = toRadius; w.color = color; w.live = true;
            w.tr.gameObject.SetActive(true);
            DrawWave(w, 0f);
        }

        void DrawWave(Wave w, float t)
        {
            // Fast out, then it slows as it eats itself away: the toon read of a blast wave.
            float grow = 1f - (1f - t) * (1f - t) * (1f - t);
            float r = Mathf.Lerp(w.from, w.to, grow);
            w.tr.localScale = new Vector3(r * 2f, 1f, r * 2f);
            var mpb = Block;
            mpb.Clear();
            mpb.SetColor(TintId, w.color);
            mpb.SetFloat(ErodeId, Mathf.Lerp(0.05f, 1f, t * t));
            w.mr.SetPropertyBlock(mpb);
        }

        void TickWaves()
        {
            float now = Time.time;
            for (int i = 0; i < _waves.Count; i++)
            {
                var w = _waves[i];
                if (!w.live) continue;
                float t = (now - w.bornAt) / w.duration;
                if (t >= 1f) { w.live = false; w.tr.gameObject.SetActive(false); continue; }
                DrawWave(w, t);
            }
        }

        // ── delayed blasts (Airstrike, Ordnance, Carpet Bomb)
        struct Blast
        {
            public BlastSpec spec;
            public float landAt;
            public Vector3 dropFrom;
            public ParticleSystem bombInstance;
            public bool bombLaunched;
        }
        const int MaxBlasts = 16;
        readonly List<Blast> _blasts = new(MaxBlasts);

        public void ScheduleBlast(in BlastSpec spec)
        {
            if (_blasts.Count >= MaxBlasts) return;
            var s = spec;
            s.pos.y = 0f;
            if (s.delay > 0f)
            {
                // Telegraph: one thin ring closing in, plus the shadow of what is coming, darkening
                // and tightening as it gets close.
                SkillFxDirector.Instance?.Converge(s.pos, s.radius, new Color(1f, 0.35f, 0.2f, 1f), s.delay, 0.08f);
                if (s.bomb != null) ShowDisc(s.pos, s.radius * 0.9f, s.radius * 0.35f, new Color(0.05f, 0.03f, 0.02f, 0.5f), s.delay, true);
            }
            Vector3 dir = s.flight; dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            var sh = library != null ? library.shared : null;
            float height = sh != null ? sh.bombFallHeight : 14f, drift = sh != null ? sh.bombDrift : 5f;
            _blasts.Add(new Blast
            {
                spec = s,
                landAt = Time.time + s.delay,
                // Falls in at an angle along the flight line, so it reads as dropped from a pass overhead.
                dropFrom = s.pos - dir.normalized * drift + Vector3.up * height,
            });
        }

        void TickBlasts()
        {
            if (_blasts.Count == 0) return;
            float now = Time.time;
            float fall = library != null ? library.shared.bombFallSeconds : 0.55f;
            for (int i = _blasts.Count - 1; i >= 0; i--)
            {
                var b = _blasts[i];
                ref readonly var s = ref b.spec;

                // The bomb is visible for the last moments before impact: it falls INTO the mark.
                if (s.bomb != null && !b.bombLaunched && now >= b.landAt - fall)
                {
                    b.bombLaunched = true;
                    b.bombInstance = FxPool.PlayFor(s.bomb, b.dropFrom, Quaternion.LookRotation(s.pos - b.dropFrom), 1f, fall);
                    Sfx("sfx.skill.airstrike.mark", b.dropFrom, 0.35f, 0.25f);
                    _blasts[i] = b;
                }
                if (b.bombInstance != null)
                {
                    // Accelerating fall into the shadow.
                    float k = Mathf.Clamp01(1f - (b.landAt - now) / fall);
                    b.bombInstance.transform.position = Vector3.Lerp(b.dropFrom, s.pos + Vector3.up * 0.3f, k * k);
                }
                if (now < b.landAt) continue;

                _blasts.RemoveAt(i);
                if (b.bombInstance != null) b.bombInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                // Capped: the pack's smoke grows past the blast radius, and big blasts buried the crowd.
                PowerKit.PlaySized(s.fx, s.pos + Vector3.up * 0.1f, s.radius, s.fxNativeRadius, 1.3f);
                if (s.decal != null) FxPool.Play(s.decal, s.pos + Vector3.up * 0.03f, PowerKit.Flat(s.decal), s.radius / 1.5f);
                Sfx(s.sfx, s.pos, 0.85f, 0.05f);
                Shake(s.shake);
                SkillFxDirector.Instance?.Pulse(s.pos, s.radius, new Color(1f, 0.62f, 0.2f, 0.85f), 0.3f, 0.18f);
                if (s.wave.a > 0f) Shockwave(s.pos, s.radius * 0.3f, s.radius * 1.1f, s.wave, 0.45f);

                int found = TargetQuery.GatherEnemies(s.pos, s.radius, EnemyMask);
                for (int c = 0; c < found; c++) PowerKit.Hit(TargetQuery.Candidate(c), s.damage, s.push, s.source);
            }
        }

        // ── soul wisps: a kill's soul flies to the player (Soul Burst / Reaper)
        struct Wisp { public ParticleSystem ps; public Vector3 from; public float bornAt; }
        const int MaxWisps = 12;
        const float WispSeconds = 0.45f;
        readonly List<Wisp> _wisps = new(MaxWisps);

        public void SoulWisp(Vector3 from) => SoulWisp(from, library != null ? library.shared.soulWispFx : null);

        public void SoulWisp(Vector3 from, ParticleSystem fx)
        {
            if (fx == null || _wisps.Count >= MaxWisps) return;
            from.y = 0.9f;
            var ps = FxPool.PlayFor(fx, from, Quaternion.identity, 0.45f, WispSeconds);
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
                var step = pos - w.ps.transform.position;
                w.ps.transform.rotation = Quaternion.LookRotation(step.sqrMagnitude > 1e-6f ? step : Vector3.up);
                w.ps.transform.position = pos;
            }
        }
    }
}
