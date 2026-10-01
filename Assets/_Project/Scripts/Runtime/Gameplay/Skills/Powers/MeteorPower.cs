using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>Meteor — a burning rock falls on the biggest crowd on screen: a long telegraph, a
    /// heavy hit and a crater that keeps burning.</summary>
    public sealed class MeteorPower : PowerModule
    {
        [Serializable]
        public sealed class Assets
        {
            [Tooltip("SK_MeteorFall: the rock wrapped in its fire trail, flown into the mark.")]
            public ParticleSystem fallFx;
            public ParticleSystem impactFx;
            public float impactNativeRadius = 2f;
            public ParticleSystem craterFx;
            [Tooltip("Burning ground left in the crater (looping; stopped after its time).")]
            public ParticleSystem burnFx;
            public float burnNativeRadius = 1f;
            public float damage = 90f;
            public float burnSeconds = 3f;
            [Tooltip("Burn damage a second, share of the impact.")]
            public float burnShare = 0.12f;
            public float delay = 1.1f;
            public float scanRadius = 22f;
        }

        struct Burn { public Vector3 pos; public float radius, dps, until; }

        static readonly Color Ember = new(1f, 0.5f, 0.12f, 1f);
        const int MaxBurns = 3;
        const float BurnTick = 0.25f;
        static readonly string[] Ids = { SkillCatalogDefs.AutoMeteor };
        public override string[] ProcIds => Ids;

        readonly List<Burn> _burns = new(MaxBurns);
        float _burnAt, _pendingRadius, _pendingDamage;
        Predicate<Vector3> _onScreen;
        Action<Vector3> _landed;

        Assets A => Lib != null ? Lib.meteor : null;

        public override void OnProc(SkillRuntime run, in SkillRuntime.PowerProc proc, Vector3 origin)
        {
            var a = A;
            if (a == null) return;
            int found = TargetQuery.GatherEnemies(origin, a.scanRadius, Host.EnemyMask);
            found = TargetQuery.Compact(found, _onScreen ??= Host.OnScreen);
            int best = found > 0 ? TargetQuery.DensestCluster(found, proc.radius, out _) : -1;
            if (best < 0) { run.Refund(proc.skillId); return; }
            Vector3 at = TargetQuery.CandidatePoint(best);
            float damage = run.PowerDamage(a.damage, SkillCatalogDefs.AutoMeteor);
            _pendingRadius = proc.radius; _pendingDamage = damage;
            Vector3 flight = at - origin; flight.y = 0f;
            Host.Sfx("sfx.skill.airstrike.mark", at, 0.7f, 0.3f);
            var spec = new BlastSpec
            {
                pos = at, radius = proc.radius, damage = damage, delay = a.delay,
                fx = a.impactFx, fxNativeRadius = a.impactNativeRadius, sfx = "sfx.skill.meteor",
                shake = 0.32f, push = 1.8f, bomb = a.fallFx, decal = a.craterFx, flight = flight,
                source = SkillCatalogDefs.AutoMeteor, wave = Ember, landed = _landed ??= Land,
            };
            if (!run.IsEvolved(SkillCatalogDefs.AutoMeteor)) { Host.ScheduleBlast(spec); return; }
            // Meteor Storm: five rocks walking across the crowd along the flight line, in sequence.
            Vector3 across = flight.sqrMagnitude > 0.01f ? flight.normalized : Vector3.forward;
            spec.source = SkillCatalogDefs.EvoMeteorStorm;
            for (int k = -2; k <= 2; k++)
            {
                spec.pos = at + across * (k * proc.radius * 1.1f);
                spec.delay = a.delay + (k + 2) * 0.22f;
                spec.shake = 0.18f;
                Host.ScheduleBlast(spec);
            }
        }

        void Land(Vector3 at)
        {
            var a = A;
            float r = _pendingRadius * 0.75f;
            if (_burns.Count >= MaxBurns) _burns.RemoveAt(0);
            _burns.Add(new Burn { pos = at, radius = r, dps = _pendingDamage * a.burnShare, until = Time.time + a.burnSeconds });
            FxPool.PlayFor(a.burnFx, at + Vector3.up * 0.05f, PowerKit.Flat(a.burnFx), r / Mathf.Max(0.1f, a.burnNativeRadius), a.burnSeconds);
        }

        public override void Tick(SkillRuntime run, Vector3 player, float dt)
        {
            if (_burns.Count == 0) return;
            float now = Time.time;
            for (int i = _burns.Count - 1; i >= 0; i--) if (now >= _burns[i].until) _burns.RemoveAt(i);
            if (_burns.Count == 0 || now < _burnAt) return;
            _burnAt = now + BurnTick;
            for (int i = 0; i < _burns.Count; i++)
            {
                var b = _burns[i];
                int found = TargetQuery.GatherEnemies(b.pos, b.radius, Host.EnemyMask);
                for (int k = 0; k < found; k++)
                {
                    var e = TargetQuery.CandidateEnemy(k);
                    if (e == null || e.IsDead) continue;
                    PowerKit.Hit(e, b.dps * BurnTick, 0f, SkillCatalogDefs.AutoMeteor);
                    SkillFxDirector.Instance?.TintEnemy(e, new Color(1f, 0.5f, 0.12f, 0.45f), 0.4f);
                }
            }
        }

        public override void ResetForRun() => _burns.Clear();
    }
}
