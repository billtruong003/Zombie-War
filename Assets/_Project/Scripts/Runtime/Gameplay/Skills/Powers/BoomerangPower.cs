using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>Boomerang — blades thrown at nearby enemies that cut once out and once back.</summary>
    public sealed class BoomerangPower : PowerModule
    {
        [Serializable]
        public sealed class Assets
        {
            public ParticleSystem hitFx;
            public float baseDamage = 14f;
            public float range = 9f;
            public float outSeconds = 0.5f;
            [Tooltip("Boomerang mesh (lies in its XY plane, spun about Y). Empty = the saw disc.")]
            public GameObject model;
            public float scale = 1.25f;
            public Color tint = new(1f, 0.72f, 0.3f, 1f);
            public Color trailColor = new(1f, 0.7f, 0.25f, 0.9f);
        }

        const int MaxBoomerangs = 4;

        struct Blade
        {
            public Transform visual;
            public TrailRenderer tipTrail;
            public bool live;
            public Vector3 dir;
            public float age, nextScan;
            public HashSet<int> hit;
        }

        readonly Blade[] _blades = new Blade[MaxBoomerangs];
        static readonly string[] Ids = { SkillCatalogDefs.AutoBoomerang };
        public override string[] ProcIds => Ids;

        Assets A => Lib != null ? Lib.boomerang : null;

        protected override void OnAttach()
        {
            var a = A;
            if (a == null) return;
            for (int i = 0; i < MaxBoomerangs; i++)
            {
                var t = BladeVisuals.Make(Host.Root, "boomerang", a.model, Lib.orbit.bladeMaterial, Lib.shared.trailMaterial,
                                          a.scale, a.trailColor, 0f, a.tint, out _);
                _blades[i].visual = t;
                _blades[i].tipTrail = BladeVisuals.TipTrail(t, a.scale, Lib.shared.trailMaterial);
                _blades[i].hit = new HashSet<int>();
            }
        }

        /// Throws up to proc.targets blades at distinct nearby enemies, fanned out if there are fewer
        /// targets than blades.
        public override void OnProc(SkillRuntime run, in SkillRuntime.PowerProc proc, Vector3 from)
        {
            var a = A;
            if (a == null) return;
            int count = proc.targets;
            int found = TargetQuery.GatherEnemies(from, a.range + 2f);
            Vector3 fallback = Host.Player.forward;
            if (found > 0) fallback = TargetQuery.CandidatePoint(TargetQuery.Nearest(found, from)) - from;
            fallback.y = 0f;
            if (fallback.sqrMagnitude < 0.01f) fallback = Vector3.forward;
            fallback.Normalize();

            int thrown = 0;
            for (int k = 0; k < MaxBoomerangs && thrown < count; k++)
            {
                ref var b = ref _blades[k];
                if (b.live || b.visual == null) continue;
                float spread = (thrown - (count - 1) * 0.5f) * 35f;
                b.dir = Quaternion.Euler(0f, spread, 0f) * fallback;
                b.age = 0f;
                b.live = true;
                b.nextScan = 0f;
                b.hit.Clear();
                b.visual.position = from + Vector3.up * 0.9f;
                b.visual.gameObject.SetActive(true);
                b.tipTrail?.Clear();
                thrown++;
            }
            if (thrown > 0) Host.Sfx("sfx.skill.boomerang.throw", from, 0.7f, 0.1f);
        }

        public override void Tick(SkillRuntime run, Vector3 p, float dt)
        {
            var a = A;
            if (a == null) return;
            float damage = 0f;
            for (int k = 0; k < MaxBoomerangs; k++)
            {
                ref var b = ref _blades[k];
                if (!b.live) continue;
                if (damage == 0f) damage = run.PowerDamage(a.baseDamage, SkillCatalogDefs.AutoBoomerang);

                b.age += dt;
                float t = b.age / Mathf.Max(0.05f, a.outSeconds);
                Vector3 pos;
                if (t <= 1f)
                {
                    // Out: fast, easing to a stop at full range.
                    float e = 1f - (1f - t) * (1f - t);
                    pos = p + b.dir * (a.range * e);
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
                // Two turns a second: fast enough to read as a boomerang, slow enough not to blur.
                b.visual.rotation = Quaternion.Euler(0f, b.age * 720f, 0f);

                if (Time.time < b.nextScan) continue;
                b.nextScan = Time.time + 0.05f;
                int found = TargetQuery.GatherEnemies(pos, 1.3f);
                for (int i = 0; i < found; i++)
                {
                    var enemy = TargetQuery.CandidateEnemy(i);
                    if (enemy == null || enemy.IsDead) continue;
                    // Once on the way out and once on the way back — the fantasy is "it cuts twice".
                    int key = enemy.GetInstanceID() * 2 + (t <= 1f ? 0 : 1);
                    if (!b.hit.Add(key)) continue;
                    PowerKit.Hit(enemy, damage, 0.5f, SkillCatalogDefs.AutoBoomerang);
                    FxPool.Play(a.hitFx, PowerKit.Chest(enemy), Quaternion.identity, 0.7f);
                    Host.Sfx("sfx.skill.blade.hit", pos, 0.5f, 0.06f);
                }
            }
        }

        public override void ResetForRun()
        {
            for (int k = 0; k < MaxBoomerangs; k++)
            {
                _blades[k].live = false;
                if (_blades[k].visual != null) _blades[k].visual.gameObject.SetActive(false);
                _blades[k].tipTrail?.Clear();
            }
        }
    }
}
