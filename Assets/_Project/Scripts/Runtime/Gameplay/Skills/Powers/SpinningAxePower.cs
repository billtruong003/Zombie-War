using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>Spinning Axe — axes are hurled up and forward in an arc and come down through the
    /// crowd, spinning, cutting everything on the way.</summary>
    public sealed class SpinningAxePower : PowerModule
    {
        [Serializable]
        public sealed class Assets
        {
            [Tooltip("SK_Axe: lies in its XY plane like the boomerang, spun about Y.")]
            public GameObject model;
            public float modelScale = 1.5f;
            public ParticleSystem hitFx;
            public float baseDamage = 24f;
            public float flightSeconds = 1.4f;
            public float reach = 8f;
            [Tooltip("Height of the lob. Kept low: the camera looks down at an angle, and a tall arc left the screen at the top.")]
            public float apex = 2f;
        }

        sealed class Axe
        {
            public Transform tr;
            public TrailRenderer trail;
            public Vector3 from, dir;
            public float reach;
            public float born, spin;
            public bool storm;
            public bool live;
            public readonly HashSet<int> hit = new();
        }

        const int MaxAxes = 6;
        const float Contact = 1.1f;
        static readonly string[] Ids = { SkillCatalogDefs.AutoAxe };
        public override string[] ProcIds => Ids;
        readonly List<Axe> _axes = new(MaxAxes);
        bool _storm;
        const float OrbitSeconds = 0.9f;

        Assets A => Lib != null ? Lib.axe : null;

        public override void OnProc(SkillRuntime run, in SkillRuntime.PowerProc proc, Vector3 origin)
        {
            var a = A;
            if (a == null || a.model == null) return;
            _storm = run.IsEvolved(SkillCatalogDefs.AutoAxe);
            int n = _storm ? 4 : Mathf.Max(1, proc.targets);
            int found = TargetQuery.GatherEnemies(origin, a.reach + 3f);
            Vector3 aim = found > 0 ? TargetQuery.CandidatePoint(TargetQuery.Nearest(found, origin)) - origin : Host.Player.forward;
            aim.y = 0f;
            // Lands just past the nearest enemy, so the fall cuts through the crowd, not empty ground.
            float reach = Mathf.Clamp(aim.magnitude + 1.5f, 4f, a.reach);
            aim = aim.sqrMagnitude > 0.01f ? aim.normalized : Vector3.forward;
            for (int k = 0; k < n; k++)
            {
                var x = Take();
                float spread = _storm ? k * 90f : (k - (n - 1) * 0.5f) * 28f;
                x.dir = Quaternion.Euler(0f, spread, 0f) * aim;
                x.from = origin + Vector3.up * 1f;
                x.reach = reach;
                x.born = Time.time + (_storm ? 0f : k * 0.08f);
                x.storm = _storm;
                x.spin = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                x.live = true;
                x.hit.Clear();
                x.tr.position = x.from;
                x.tr.gameObject.SetActive(true);
                x.trail?.Clear();
            }
            Host.Sfx("sfx.skill.boomerang.throw", origin, 0.7f, 0.1f);
        }

        Axe Take()
        {
            Axe oldest = null;
            foreach (var x in _axes)
            {
                if (!x.live) return x;
                if (oldest == null || x.born < oldest.born) oldest = x;
            }
            if (_axes.Count < MaxAxes) { var n = Build(); _axes.Add(n); return n; }
            return oldest;
        }

        Axe Build()
        {
            var a = A;
            var t = BladeVisuals.Make(Host.Root, "axe", a.model, Lib.orbit.bladeMaterial, Lib.shared.trailMaterial, a.modelScale,
                                      Color.white, 0f, Color.white, out _);
            return new Axe { tr = t, trail = BladeVisuals.TipTrail(t, a.modelScale, Lib.shared.trailMaterial) };
        }

        public override void Tick(SkillRuntime run, Vector3 p, float dt)
        {
            var a = A;
            if (a == null) return;
            float now = Time.time, damage = 0f;
            foreach (var x in _axes)
            {
                if (!x.live) continue;
                float age = now - x.born;
                Vector3 pos;
                if (x.storm && age < OrbitSeconds)
                {
                    // Axe Storm: a turn around the player first, cutting what comes close...
                    float ang = Mathf.Atan2(x.dir.z, x.dir.x) + age / OrbitSeconds * Mathf.PI * 2f;
                    pos = p + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 2.2f + Vector3.up * 1f;
                    x.from = pos;
                    x.dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                    x.reach = a.reach;
                }
                else
                {
                    float t = (age - (x.storm ? OrbitSeconds : 0f)) / a.flightSeconds;
                    if (t < 0f) continue;
                    if (t >= 1f) { x.live = false; x.tr.gameObject.SetActive(false); continue; }
                    // ...then out. Up and out, then down: a lob that lands past the crowd.
                    pos = x.from + x.dir * (x.reach * t) + Vector3.up * (4f * a.apex * t * (1f - t)) - Vector3.up * (0.5f * t);
                    if (t < 0.02f) x.hit.Clear();   // the flight may cut the same enemies again
                }
                x.tr.position = pos;
                x.tr.rotation = Quaternion.Euler(0f, x.spin * (now - x.born) * 900f, 0f);
                if (damage == 0f) damage = run.PowerDamage(a.baseDamage, SkillCatalogDefs.AutoAxe);
                // Cuts only while low enough to reach bodies.
                if (pos.y > 2.2f) continue;
                Vector3 ground = new(pos.x, 0f, pos.z);
                int found = TargetQuery.GatherEnemies(ground, Contact);
                for (int k = 0; k < found; k++)
                {
                    var e = TargetQuery.CandidateEnemy(k);
                    if (e == null || e.IsDead || !x.hit.Add(e.GetInstanceID())) continue;
                    PowerKit.Hit(e, damage, 0.6f, x.storm ? SkillCatalogDefs.EvoAxeStorm : SkillCatalogDefs.AutoAxe);
                    FxPool.Play(a.hitFx, PowerKit.Chest(e), PowerKit.Flat(a.hitFx), 0.5f);
                    Host.Sfx("sfx.skill.blade.hit", pos, 0.5f, 0.06f);
                }
            }
        }

        public override void ResetForRun()
        {
            foreach (var x in _axes) { x.live = false; if (x.tr != null) x.tr.gameObject.SetActive(false); }
        }
    }
}
