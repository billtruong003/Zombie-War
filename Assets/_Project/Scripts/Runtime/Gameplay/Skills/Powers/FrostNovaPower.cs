using System;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>Frost Nova (evolution: Absolute Zero) — a frost ring out of the player that slows,
    /// or once evolved freezes, everything it reaches.</summary>
    public sealed class FrostNovaPower : PowerModule
    {
        [Serializable]
        public sealed class Assets
        {
            public ParticleSystem fx;
            public float nativeRadius = 4f;
            public float baseDamage = 16f;
            public Color slowTint = new(0.45f, 0.8f, 1f, 0.6f);
            public Color frozenTint = new(0.7f, 0.95f, 1f, 0.85f);
            [Tooltip("Ice bursting on each enemy Absolute Zero freezes.")]
            public ParticleSystem freezeBurstFx;
        }

        static readonly string[] Ids = { SkillCatalogDefs.AutoFrostNova };
        public override string[] ProcIds => Ids;

        public override void OnProc(SkillRuntime run, in SkillRuntime.PowerProc proc, Vector3 centre)
        {
            var a = Lib?.frost;
            if (a == null) return;
            float radius = proc.radius;
            bool freeze = run.FrostFreezes;
            FxPool.Play(a.fx, centre + Vector3.up * 0.1f, PowerKit.Flat(a.fx), radius / Mathf.Max(0.1f, a.nativeRadius));
            // A band of frost racing out to the edge of the chilled area and melting (a filled disc
            // this size read as fog over the whole screen).
            Host.Sfx("sfx.skill.frost", centre, 0.9f, 0.2f);
            Host.Shake(freeze ? 0.25f : 0.12f);

            float damage = run.PowerDamage(a.baseDamage, SkillCatalogDefs.AutoFrostNova);
            string source = run.Has(SkillCatalogDefs.EvoAbsoluteZero) ? SkillCatalogDefs.EvoAbsoluteZero : SkillCatalogDefs.AutoFrostNova;
            float now = Time.time;
            int found = TargetQuery.GatherEnemies(centre, radius, Host.EnemyMask);
            for (int i = 0; i < found; i++)
            {
                var enemy = PowerKit.EnemyOf(TargetQuery.Candidate(i));
                if (enemy == null || enemy.IsDead) continue;
                int id = enemy.transform.GetInstanceID();
                if (freeze)
                {
                    StatusCarrier.Apply(id, StatusKind.Frozen, 1f, 1.5f, now);
                    SkillFxDirector.Instance?.TintEnemy(enemy, a.frozenTint, 1.5f);
                    // Absolute Zero: ice bursts on each enemy it locks (capped, a crowd is a lot of ice).
                    if (i < 12) Host.PlayDelayed(a.freezeBurstFx, PowerKit.Chest(enemy), 0.28f, 0.05f + 0.02f * i);
                }
                else
                {
                    StatusCarrier.Apply(id, StatusKind.Slow, run.FrostSlow, 2f, now);
                    SkillFxDirector.Instance?.TintEnemy(enemy, a.slowTint, 2f);
                }
                PowerKit.Hit(enemy, damage, 0.6f, source);
            }
        }
    }
}
