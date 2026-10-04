using System;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>Ordnance Core (evolution: Carpet Bomb) — a shell dropped on the densest cluster on screen.</summary>
    public sealed class OrdnancePower : PowerModule
    {
        [Serializable]
        public sealed class Assets
        {
            [Tooltip("The blast on the chosen cluster.")]
            public ParticleSystem blastFx;
            public float blastNativeRadius = 1.5f;
            [Tooltip("A2: the scorch left where the shell lands (the pack's grenade blast is mostly grey smoke).")]
            public ParticleSystem decalFx;
            [Tooltip("Seconds between choosing the cluster and the shell landing (plus the fall).")]
            public float delay = 0.45f;
            public float damage = 34f;
            [Tooltip("How far around the player the shell looks for a crowd.")]
            public float scanRadius = 22f;
        }

        static readonly string[] Ids = { SkillCatalogDefs.AutoOrdnance };
        public override string[] ProcIds => Ids;
        Predicate<Vector3> _onScreen;

        public override void OnProc(SkillRuntime run, in SkillRuntime.PowerProc proc, Vector3 origin)
        {
            var a = Lib?.ordnance;
            if (a == null) return;
            // The shell lands where the player can watch it: the densest cluster ON SCREEN.
            int found = TargetQuery.GatherEnemies(origin, a.scanRadius);
            found = TargetQuery.Compact(found, _onScreen ??= Host.OnScreen);
            if (found == 0) { run.Refund(proc.skillId); return; }
            int best = TargetQuery.DensestCluster(found, proc.radius, out _);
            if (best < 0) { run.Refund(proc.skillId); return; }

            Vector3 centre = TargetQuery.CandidatePoint(best);
            float damage = run.PowerDamage(a.damage, SkillCatalogDefs.AutoOrdnance);
            Host.Sfx("sfx.skill.target", centre, 0.5f, 0.2f);

            // The shell is a bomb falling into a shadow (the same drop as Airstrike): the player sees
            // WHICH group was chosen, then watches it get hit.
            Vector3 toCrowd = centre - origin; toCrowd.y = 0f;
            var spec = new BlastSpec
            {
                radius = proc.radius, damage = damage, fx = a.blastFx, fxNativeRadius = a.blastNativeRadius,
                sfx = "sfx.skill.blast", push = 1.2f, bomb = Lib.shared.bombFx, decal = a.decalFx,
                wave = new Color(1f, 0.55f, 0.18f, 1f),
            };
            if (run.IsEvolved(SkillCatalogDefs.AutoOrdnance))
            {
                // Carpet Bomb: five shells walking across the crowd along one line, in sequence.
                Vector3 across = toCrowd.sqrMagnitude > 0.01f ? Vector3.Cross(Vector3.up, toCrowd.normalized) : Vector3.right;
                spec.flight = across; spec.shake = 0.16f; spec.push = 1.3f; spec.source = SkillCatalogDefs.EvoCarpetBomb;
                for (int k = -2; k <= 2; k++)
                {
                    spec.pos = centre + across * (k * proc.radius * 0.9f);
                    spec.delay = a.delay + 0.25f + (k + 2) * 0.12f;
                    Host.ScheduleBlast(spec);
                }
            }
            else
            {
                spec.pos = centre; spec.delay = a.delay + 0.25f; spec.flight = toCrowd; spec.shake = 0.18f;
                spec.source = SkillCatalogDefs.AutoOrdnance;
                Host.ScheduleBlast(spec);
            }
        }
    }
}
