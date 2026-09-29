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
            Host.ShowDisc(origin, r * 0.2f, r, new Color(0.55f, 0.42f, 0.25f, 0.35f), 0.45f, false, 0.12f);
            SkillFxDirector.Instance?.Pulse(origin, r, Earth, 0.3f, 0.22f);
            Host.Sfx("sfx.player.bomb.explode", origin, 0.7f, 0.2f);
            Host.Shake(0.2f);
            float damage = run.PowerDamage(a.baseDamage, SkillCatalogDefs.AutoStomp);
            int found = TargetQuery.GatherEnemies(origin, r, Host.EnemyMask);
            for (int i = 0; i < found; i++) PowerKit.Hit(TargetQuery.Candidate(i), damage, a.push, SkillCatalogDefs.AutoStomp);
        }
    }
}
