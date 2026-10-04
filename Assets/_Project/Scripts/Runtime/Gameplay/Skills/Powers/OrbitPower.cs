using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>Orbit Blades (evolution: Buzzsaw) — saws circling the player, cutting what they touch.</summary>
    public sealed class OrbitPower : PowerModule
    {
        [Serializable]
        public sealed class Assets
        {
            [Tooltip("Optional mesh override. Empty = the built-in saw disc, which reads far better from " +
                     "the top-down camera than a knife.")]
            public GameObject bladeModel;
            [Tooltip("Vertex-coloured unlit material for the saw disc.")]
            public Material bladeMaterial;
            public ParticleSystem hitFx;
            public float baseDamage = 9f;
            [Tooltip("A blade can hit the same enemy at most this often.")]
            public float hitInterval = 0.4f;
            public float bladeScale = 0.9f;
            public Color trailColor = new(0.75f, 0.95f, 1f, 0.9f);
            [Tooltip("Alpha-blended SkillLine material for the faint path ring.")]
            public Material pathMaterial;
            [Tooltip("Faint ring on the blades' path. Off: the owner found it ugly (2026-09-29).")]
            public bool showPath;
        }

        const int MaxBlades = 6;
        const float Contact = 0.95f;
        static readonly Color PathColor = new(0.7f, 0.92f, 1f, 0.22f);
        static readonly Color BuzzsawPathColor = new(1f, 0.8f, 0.3f, 0.3f);
        static readonly Color BuzzsawTint = new(1f, 0.82f, 0.4f, 1f);

        readonly Transform[] _blades = new Transform[MaxBlades];
        readonly TrailRenderer[] _trails = new TrailRenderer[MaxBlades];
        readonly Dictionary<int, float> _nextHit = new(128);
        readonly List<int> _scratch = new(128);
        float _angle, _scanAt;
        bool _gold;
        LineRenderer _path;

        Assets A => Lib != null ? Lib.orbit : null;

        protected override void OnAttach()
        {
            var a = A;
            if (a == null) return;
            for (int i = 0; i < MaxBlades; i++)
                _blades[i] = BladeVisuals.Make(Host.Root, "blade", a.bladeModel, a.bladeMaterial, Lib.shared.trailMaterial,
                                               a.bladeScale, a.trailColor, 0.28f, Color.white, out _trails[i]);
        }

        public override void Tick(SkillRuntime run, Vector3 p, float dt)
        {
            var a = A;
            if (a == null) return;
            int count = Mathf.Min(MaxBlades, run.OrbitBladeCount);
            float radius = run.OrbitRadius;
            bool evolved = run.IsEvolved(SkillCatalogDefs.AutoOrbit);
            if (count > 0 && evolved != _gold) Tint(evolved);
            DrawPath(p, radius, count > 0, evolved);
            _angle = (_angle + run.OrbitDegreesPerSecond * dt) % 360f;

            for (int i = 0; i < MaxBlades; i++)
            {
                var b = _blades[i];
                if (b == null) continue;
                bool on = i < count;
                if (b.gameObject.activeSelf != on)
                {
                    b.gameObject.SetActive(on);
                    if (on) _trails[i].Clear();
                }
                if (!on) continue;

                float ang = (_angle + i * 360f / count) * Mathf.Deg2Rad;
                b.position = p + new Vector3(Mathf.Cos(ang) * radius, 0.8f, Mathf.Sin(ang) * radius);
                // Spin on its own axis too: a saw, not a satellite.
                b.rotation = Quaternion.Euler(0f, -_angle * 3f - i * 60f, 0f);
            }
            if (count == 0) return;

            // Damage scan at 20 Hz: one physics query for all blades.
            float now = Time.time;
            if (now < _scanAt) return;
            _scanAt = now + 0.05f;

            float damage = run.PowerDamage(a.baseDamage, SkillCatalogDefs.AutoOrbit);
            string source = run.Has(SkillCatalogDefs.EvoBuzzsaw) ? SkillCatalogDefs.EvoBuzzsaw : SkillCatalogDefs.AutoOrbit;
            int found = TargetQuery.GatherEnemies(p, radius + 1.2f);
            for (int c = 0; c < found; c++)
            {
                Vector3 ep = TargetQuery.CandidatePoint(c);
                bool touched = false;
                for (int i = 0; i < count && !touched; i++)
                {
                    Vector3 bp = _blades[i].position;
                    float dx = ep.x - bp.x, dz = ep.z - bp.z;
                    touched = dx * dx + dz * dz <= Contact * Contact;
                }
                if (!touched) continue;

                int id = TargetQuery.CandidateId(c);
                if (_nextHit.TryGetValue(id, out float next) && now < next) continue;
                var enemy = TargetQuery.CandidateEnemy(c);
                if (enemy == null) continue;
                _nextHit[id] = now + a.hitInterval;

                PowerKit.Hit(enemy, damage, 0.35f, source);
                FxPool.Play(a.hitFx, PowerKit.Chest(enemy), PowerKit.Flat(a.hitFx), 0.6f);
                Host.Sfx("sfx.skill.blade.hit", ep, 0.45f, 0.07f);
            }

            // Forget enemies not touched for a while, so the map does not grow for the whole run.
            if (_nextHit.Count > 96)
            {
                _scratch.Clear();
                foreach (var kv in _nextHit) if (now - kv.Value > 2f) _scratch.Add(kv.Key);
                for (int i = 0; i < _scratch.Count; i++) _nextHit.Remove(_scratch[i]);
            }
        }

        void Tint(bool gold)
        {
            _gold = gold;
            var mpb = Host.Block;
            for (int i = 0; i < MaxBlades; i++)
            {
                if (_blades[i] == null) continue;
                foreach (var r in _blades[i].GetComponentsInChildren<MeshRenderer>(true))
                {
                    mpb.Clear();
                    if (gold) mpb.SetColor(BladeVisuals.BaseColorId, BuzzsawTint);
                    r.SetPropertyBlock(mpb);
                }
                var tr = _trails[i];
                if (tr == null) continue;
                var c = gold ? new Color(1f, 0.8f, 0.3f, 0.95f) : A.trailColor;
                tr.startColor = c;
                tr.endColor = new Color(c.r, c.g, c.b, 0f);
                tr.time = gold ? 0.34f : 0.28f;   // long enough to show past the blade
            }
        }

        void DrawPath(Vector3 p, float radius, bool on, bool gold)
        {
            var a = A;
            if (a.pathMaterial == null || !a.showPath) { if (_path != null) _path.enabled = false; return; }
            if (_path == null)
            {
                var go = new GameObject("orbitPath");
                go.transform.SetParent(Host.Root, false);
                _path = go.AddComponent<LineRenderer>();
                _path.useWorldSpace = true;
                _path.loop = true;
                _path.positionCount = 40;
                _path.alignment = LineAlignment.View;
                _path.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _path.receiveShadows = false;
                _path.sharedMaterial = a.pathMaterial;
            }
            if (_path.gameObject.activeSelf != on) _path.gameObject.SetActive(on);
            if (!on) return;
            for (int i = 0; i < 40; i++)
            {
                float ang = i * Mathf.PI * 2f / 40;
                _path.SetPosition(i, p + new Vector3(Mathf.Cos(ang) * radius, 0.8f, Mathf.Sin(ang) * radius));
            }
            _path.startColor = _path.endColor = gold ? BuzzsawPathColor : PathColor;
            _path.widthMultiplier = gold ? 0.5f : 0.35f;
        }

        public override void ResetForRun() => _nextHit.Clear();
    }
}
