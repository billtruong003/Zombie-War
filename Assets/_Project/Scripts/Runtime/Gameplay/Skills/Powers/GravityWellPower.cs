using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>Gravity Well — a vortex opens on the biggest crowd on screen, drags everything in
    /// range to its centre for 1.5 s, then bursts. Control first, damage second.</summary>
    public sealed class GravityWellPower : PowerModule
    {
        [Serializable]
        public sealed class Assets
        {
            [Tooltip("Particles streaming into the centre while it pulls.")]
            public ParticleSystem vortexFx;
            public float vortexNativeRadius = 2f;
            [Tooltip("The burst when it closes.")]
            public ParticleSystem popFx;
            public float popNativeRadius = 2f;
            public float popDamage = 40f;
            public float pullSeconds = 1.5f;
            [Tooltip("Metres an enemy is dragged per second.")]
            public float pullSpeed = 5f;
            public float scanRadius = 22f;
        }

        struct Well { public Vector3 pos; public float radius, popAt; }

        static readonly Color Violet = new(0.55f, 0.35f, 1f, 1f);
        const int MaxWells = 3;
        const float PullTick = 0.1f;
        static readonly string[] Ids = { SkillCatalogDefs.AutoGravity };
        public override string[] ProcIds => Ids;

        readonly List<Well> _wells = new(MaxWells);
        float _pullAt;
        Predicate<Vector3> _onScreen;

        Assets A => Lib != null ? Lib.gravity : null;

        public override void OnProc(SkillRuntime run, in SkillRuntime.PowerProc proc, Vector3 origin)
        {
            var a = A;
            if (a == null) return;
            int found = TargetQuery.GatherEnemies(origin, a.scanRadius, Host.EnemyMask);
            found = TargetQuery.Compact(found, _onScreen ??= Host.OnScreen);
            int best = found > 0 ? TargetQuery.DensestCluster(found, proc.radius * 0.6f, out _) : -1;
            if (best < 0) { run.Refund(proc.skillId); return; }
            Vector3 at = TargetQuery.CandidatePoint(best); at.y = 0f;
            if (_wells.Count >= MaxWells) _wells.RemoveAt(0);
            _wells.Add(new Well { pos = at, radius = proc.radius, popAt = Time.time + a.pullSeconds });

            // The pull reads as a hole: a dark disc tightening, rings closing, particles pouring in.
            FxPool.PlayFor(a.vortexFx, at + Vector3.up * 0.4f, PowerKit.Flat(a.vortexFx),
                           proc.radius / Mathf.Max(0.1f, a.vortexNativeRadius), a.pullSeconds);
            Host.ShowDisc(at, proc.radius, proc.radius * 0.2f, new Color(0.12f, 0.05f, 0.25f, 0.55f), a.pullSeconds, true);
            SkillFxDirector.Instance?.Converge(at, proc.radius, Violet, a.pullSeconds * 0.5f, 0.1f);
            Host.Sfx("sfx.skill.target", at, 0.6f, 0.2f);
        }

        public override void Tick(SkillRuntime run, Vector3 player, float dt)
        {
            if (_wells.Count == 0) return;
            var a = A;
            float now = Time.time;
            for (int i = _wells.Count - 1; i >= 0; i--)
            {
                var w = _wells[i];
                if (now < w.popAt) continue;
                _wells.RemoveAt(i);
                Pop(run, w);
            }
            if (_wells.Count == 0 || now < _pullAt) return;
            _pullAt = now + PullTick;
            for (int i = 0; i < _wells.Count; i++)
            {
                var w = _wells[i];
                int found = TargetQuery.GatherEnemies(w.pos, w.radius, Host.EnemyMask);
                for (int k = 0; k < found; k++)
                {
                    var e = TargetQuery.CandidateEnemy(k);
                    if (e == null || e.IsDead) continue;
                    e.ApplyPull(w.pos, a.pullSpeed * PullTick * 1.4f, PullTick * 1.4f);
                    SkillFxDirector.Instance?.TintEnemy(e, new Color(0.6f, 0.4f, 1f, 0.5f), 0.2f);
                }
            }
        }

        void Pop(SkillRuntime run, Well w)
        {
            var a = A;
            float r = w.radius * 0.6f;
            PowerKit.PlaySized(a.popFx, w.pos + Vector3.up * 0.2f, r, a.popNativeRadius, 1.3f);
            Host.Shockwave(w.pos, r * 0.2f, r * 1.3f, Violet, 0.45f);
            SkillFxDirector.Instance?.Pulse(w.pos, r, Violet, 0.3f, 0.2f);
            Host.Sfx("sfx.skill.blast", w.pos, 0.8f, 0.1f);
            Host.Shake(0.16f);
            float damage = run.PowerDamage(a.popDamage, SkillCatalogDefs.AutoGravity);
            int found = TargetQuery.GatherEnemies(w.pos, r, Host.EnemyMask);
            for (int k = 0; k < found; k++) PowerKit.Hit(TargetQuery.Candidate(k), damage, 1f, SkillCatalogDefs.AutoGravity);
        }

        public override void ResetForRun() => _wells.Clear();
    }
}
