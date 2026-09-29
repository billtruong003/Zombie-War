using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>
    /// The Launcher's two signature cards, acting on each blast of the grenade launcher: Cluster
    /// Charge throws bomblets that pop around the blast, Napalm Shell leaves the ground burning.
    /// </summary>
    public sealed class LauncherPower : PowerModule
    {
        [Serializable]
        public sealed class Assets
        {
            [Tooltip("A bomblet's pop.")]
            public ParticleSystem bombletFx;
            public float bombletNativeRadius = 1f;
            [Tooltip("Burning ground left by Napalm Shell (looping; stopped after its time).")]
            public ParticleSystem napalmFx;
            public float napalmNativeRadius = 1f;
            public Color burnTint = new(1f, 0.5f, 0.12f, 0.5f);
        }

        struct Patch { public Vector3 pos; public float radius, dps, until; }

        const float BombletRadius = 1.3f;
        const int MaxPatches = 4;   // the oldest burns out first: overlapping fields turned the screen white
        const float BurnTick = 0.25f;
        readonly List<Patch> _patches = new(MaxPatches);
        float _burnAt;

        Assets A => Lib != null ? Lib.launcher : null;

        public override void OnLauncherBlast(SkillRuntime run, Vector3 at, float radius, float damage)
        {
            var a = A;
            if (a == null) return;
            at.y = 0f;

            int bomblets = run.ClusterBomblets;
            if (bomblets > 0)
            {
                float share = damage * run.ClusterShare;
                float spin = UnityEngine.Random.Range(0f, 360f);
                for (int k = 0; k < bomblets; k++)
                {
                    // Scattered on a ring just past the blast, landing one after another.
                    float ang = (spin + k * 360f / bomblets + UnityEngine.Random.Range(-20f, 20f)) * Mathf.Deg2Rad;
                    float dist = radius * UnityEngine.Random.Range(0.9f, 1.4f);
                    Host.ScheduleBlast(new BlastSpec
                    {
                        pos = at + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * dist,
                        radius = BombletRadius, damage = share, delay = 0.18f + 0.08f * k,
                        fx = a.bombletFx, fxNativeRadius = a.bombletNativeRadius, sfx = "sfx.skill.blast",
                        shake = 0.04f, push = 0.6f, source = SkillCatalogDefs.RocketCluster,
                    });
                }
            }

            float seconds = run.NapalmSeconds;
            if (seconds > 0f)
            {
                float r = radius * 0.85f;
                if (_patches.Count >= MaxPatches) _patches.RemoveAt(0);
                _patches.Add(new Patch { pos = at, radius = r, dps = damage * run.NapalmDpsShare, until = Time.time + seconds });
                FxPool.PlayFor(a.napalmFx, at + Vector3.up * 0.05f, PowerKit.Flat(a.napalmFx),
                               r / Mathf.Max(0.1f, a.napalmNativeRadius), seconds);
                Host.ShowDisc(at, r * 1.05f, r * 0.9f, new Color(1f, 0.4f, 0.08f, 0.5f), seconds, false);
            }
        }

        public override void Tick(SkillRuntime run, Vector3 player, float dt)
        {
            if (_patches.Count == 0) return;
            float now = Time.time;
            for (int i = _patches.Count - 1; i >= 0; i--) if (now >= _patches[i].until) _patches.RemoveAt(i);
            if (_patches.Count == 0 || now < _burnAt) return;
            _burnAt = now + BurnTick;
            var a = A;
            for (int i = 0; i < _patches.Count; i++)
            {
                var p = _patches[i];
                int found = TargetQuery.GatherEnemies(p.pos, p.radius, Host.EnemyMask);
                for (int c = 0; c < found; c++)
                {
                    var e = TargetQuery.CandidateEnemy(c);
                    if (e == null || e.IsDead) continue;
                    PowerKit.Hit(e, p.dps * BurnTick, 0f, SkillCatalogDefs.RocketNapalm);
                    if (a != null) SkillFxDirector.Instance?.TintEnemy(e, a.burnTint, 0.4f);
                }
            }
        }

        public override void ResetForRun() => _patches.Clear();
    }
}
