using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>Toxic Cloud — a gas canister falls on the biggest crowd on screen and bursts into a
    /// cloud; everything inside keeps gaining poison stacks while it stays.</summary>
    public sealed class ToxicCloudPower : PowerModule
    {
        [Serializable]
        public sealed class Assets
        {
            [Tooltip("The canister falling into the mark.")]
            public ParticleSystem canisterFx;
            [Tooltip("The burst where it lands.")]
            public ParticleSystem burstFx;
            public float burstNativeRadius = 2f;
            [Tooltip("The lingering cloud (looping; stopped after its time).")]
            public ParticleSystem cloudFx;
            public float cloudNativeRadius = 2f;
            public float seconds = 3.5f;
            [Tooltip("Poison damage a second per stack (before Damage Up and rank).")]
            public float poisonDps = 4f;
            public float impactDamage = 8f;
            public float scanRadius = 22f;
        }

        struct Cloud { public Vector3 pos; public float radius, until; }

        static readonly Color Lime = new(0.62f, 1f, 0.25f, 1f);
        const int MaxClouds = 4;
        const float TickSeconds = 0.5f;
        static readonly string[] Ids = { SkillCatalogDefs.AutoToxic };
        public override string[] ProcIds => Ids;

        readonly List<Cloud> _clouds = new(MaxClouds);
        float _tickAt, _pendingRadius;
        Predicate<Vector3> _onScreen;
        Action<Vector3> _landed;

        Assets A => Lib != null ? Lib.toxic : null;

        public override void OnProc(SkillRuntime run, in SkillRuntime.PowerProc proc, Vector3 origin)
        {
            var a = A;
            if (a == null) return;
            int found = TargetQuery.GatherEnemies(origin, a.scanRadius, Host.EnemyMask);
            found = TargetQuery.Compact(found, _onScreen ??= Host.OnScreen);
            int best = found > 0 ? TargetQuery.DensestCluster(found, proc.radius, out _) : -1;
            if (best < 0) { run.Refund(proc.skillId); return; }
            Vector3 at = TargetQuery.CandidatePoint(best);
            _pendingRadius = proc.radius;
            Vector3 flight = at - origin; flight.y = 0f;
            Host.ScheduleBlast(new BlastSpec
            {
                pos = at, radius = proc.radius, damage = run.PowerDamage(a.impactDamage, SkillCatalogDefs.AutoToxic),
                delay = 0.6f, fx = a.burstFx, fxNativeRadius = a.burstNativeRadius, sfx = "sfx.skill.poison",
                shake = 0.06f, push = 0.3f, bomb = a.canisterFx, flight = flight, source = SkillCatalogDefs.AutoToxic,
                wave = Lime, landed = _landed ??= Land,
            });
        }

        void Land(Vector3 at)
        {
            var a = A;
            if (a == null) return;
            float r = _pendingRadius;
            if (_clouds.Count >= MaxClouds) _clouds.RemoveAt(0);
            _clouds.Add(new Cloud { pos = at, radius = r, until = Time.time + a.seconds });
            FxPool.PlayFor(a.cloudFx, at + Vector3.up * 0.3f, PowerKit.Flat(a.cloudFx), r / Mathf.Max(0.1f, a.cloudNativeRadius), a.seconds);
            Host.ShowDisc(at, r * 1.02f, r * 0.9f, new Color(0.45f, 0.85f, 0.15f, 0.35f), a.seconds, false, 0.18f);
        }

        public override void Tick(SkillRuntime run, Vector3 player, float dt)
        {
            if (_clouds.Count == 0) return;
            float now = Time.time;
            for (int i = _clouds.Count - 1; i >= 0; i--) if (now >= _clouds[i].until) _clouds.RemoveAt(i);
            if (_clouds.Count == 0 || now < _tickAt) return;
            _tickAt = now + TickSeconds;
            var a = A;
            float dps = run.PowerDamage(a.poisonDps, SkillCatalogDefs.AutoToxic);
            for (int i = 0; i < _clouds.Count; i++)
            {
                var c = _clouds[i];
                int found = TargetQuery.GatherEnemies(c.pos, c.radius, Host.EnemyMask);
                for (int k = 0; k < found; k++)
                    Host.Poison(TargetQuery.CandidateEnemy(k), 1, dps, SkillRuntime.PoisonSeconds, SkillCatalogDefs.AutoToxic);
            }
        }

        public override void ResetForRun() => _clouds.Clear();
    }
}
