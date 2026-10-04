using System;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>Ground Stomp — the player slams the ground: a dust wave throws everything nearby back.
    /// Room to breathe, with a little damage.</summary>
    public sealed class GroundStompPower : PowerModule
    {
        [Serializable]
        public sealed class Assets
        {
            public ParticleSystem dustFx;
            public ParticleSystem crackFx;
            public float baseDamage = 20f;
            public float push = 2.6f;
        }

        static readonly Color Earth = new(0.85f, 0.66f, 0.38f, 1f);
        static readonly string[] Ids = { SkillCatalogDefs.AutoStomp };
        public override string[] ProcIds => Ids;

        public override void OnProc(SkillRuntime run, in SkillRuntime.PowerProc proc, Vector3 origin)
        {
            var a = Lib?.stomp;
            if (a == null) return;
            float r = proc.radius;
            FxPool.Play(a.dustFx, origin, PowerKit.Flat(a.dustFx), 1.2f);
            FxPool.Play(a.crackFx, origin + Vector3.up * 0.1f, PowerKit.Flat(a.crackFx), 0.9f);
            Host.Shockwave(origin, 0.4f, r, Earth, 0.5f);
            Host.Sfx("sfx.player.bomb.explode", origin, 0.7f, 0.2f);
            Host.Shake(0.2f);
            bool quake = run.IsEvolved(SkillCatalogDefs.AutoStomp);
            float damage = run.PowerDamage(a.baseDamage, SkillCatalogDefs.AutoStomp);
            float now = Time.time;
            int found = TargetQuery.GatherEnemies(origin, r);
            for (int i = 0; i < found; i++)
            {
                var e = TargetQuery.CandidateEnemy(i);
                if (e == null || e.IsDead) continue;
                PowerKit.Hit(e, damage, quake ? a.push * 0.4f : a.push, quake ? SkillCatalogDefs.EvoEarthquake : SkillCatalogDefs.AutoStomp);
                if (!quake) continue;
                // Earthquake: the ground cracks under them and they are stunned where they stand.
                StatusCarrier.Apply(e.transform.GetInstanceID(), StatusKind.Frozen, 1f, 1.4f, now);
                SkillFxDirector.Instance?.TintEnemy(e, new Color(0.85f, 0.66f, 0.38f, 0.6f), 1.4f);
            }
            if (quake)
            {
                Host.Shake(0.28f);
                for (int k = 0; k < 5; k++)
                {
                    Vector3 c = origin + Quaternion.Euler(0f, k * 72f + UnityEngine.Random.Range(-20f, 20f), 0f) * Vector3.forward * (r * 0.55f);
                    Host.PlayDelayed(a.crackFx, c + Vector3.up * 0.1f, 0.7f, 0.05f * k);
                }
            }
        }
    }
}
