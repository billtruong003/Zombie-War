using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>Fire Trail — burning patches dropped behind the player as they move.</summary>
    public sealed class FireTrailPower : PowerModule
    {
        [Serializable]
        public sealed class Assets
        {
            public ParticleSystem patchFx;
            public float nativeRadius = 0.75f;
            public float radius = 1.1f;
            public float seconds = 2.2f;
            public Color burnTint = new(1f, 0.55f, 0.15f, 0.45f);
        }

        struct Patch { public Vector3 pos; public float until; }

        const int MaxPatches = 24;
        const float TickSeconds = 0.25f;
        readonly List<Patch> _patches = new(MaxPatches);
        float _tickAt;

        public override void Tick(SkillRuntime run, Vector3 p, float dt)
        {
            var a = Lib?.fireTrail;
            int drops = run.ConsumeFireTrailDrops();
            if (a == null) return;
            float now = Time.time;
            float radius = a.radius * run.AreaMultiplier;
            for (int i = 0; i < drops && _patches.Count < MaxPatches; i++)
            {
                _patches.Add(new Patch { pos = p, until = now + a.seconds });
                FxPool.PlayFor(a.patchFx, p + Vector3.up * 0.05f, PowerKit.Flat(a.patchFx),
                               0.75f * radius / Mathf.Max(0.1f, a.nativeRadius), a.seconds);
                // A glowing burn under the flames. Patches overlap, so the trail reads as one
                // continuous strip of fire instead of separate candles.
                Host.ShowDisc(p, radius * 1.05f, radius * 0.8f, new Color(1f, 0.42f, 0.08f, 0.5f), a.seconds, false);
                Host.Sfx("sfx.skill.fire", p, 0.25f, 0.9f);
            }

            for (int i = _patches.Count - 1; i >= 0; i--)
                if (now >= _patches[i].until) _patches.RemoveAt(i);
            if (_patches.Count == 0 || now < _tickAt) return;
            _tickAt = now + TickSeconds;

            // One query around the player covers every patch (patches trail at most ~12 m behind).
            float reach = 0f;
            for (int i = 0; i < _patches.Count; i++)
                reach = Mathf.Max(reach, (_patches[i].pos - p).magnitude);
            int found = TargetQuery.GatherEnemies(p, reach + radius + 0.5f, Host.EnemyMask);
            float damage = run.PowerDamage(run.FireTrailDps, SkillCatalogDefs.AutoFireTrail) * TickSeconds;
            float r2 = radius * radius;

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
                var enemy = PowerKit.EnemyOf(TargetQuery.Candidate(c));
                if (enemy == null || enemy.IsDead) continue;
                PowerKit.Hit(enemy, damage, 0f, SkillCatalogDefs.AutoFireTrail);
                SkillFxDirector.Instance?.TintEnemy(enemy, a.burnTint, 0.4f);
            }
        }

        public override void ResetForRun() => _patches.Clear();
    }
}
