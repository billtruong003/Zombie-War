using System;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>
    /// Chain Lightning (evolution: Thunderstorm) and the SMG's Static Build-up, which shares the
    /// chain-to-nearest selection: a bolt that visibly jumps from enemy to enemy.
    /// </summary>
    public sealed class ChainPower : PowerModule
    {
        [Serializable]
        public sealed class Assets
        {
            [Tooltip("Electric hit at each chain endpoint. The connecting bolt is drawn by SkillFxDirector.")]
            public ParticleSystem sparkFx;
            [Tooltip("Thunderstorm: the bolt from the sky on a chain target.")]
            public ParticleSystem skyStrikeFx;
            [Tooltip("Damage per chain arc (Chain Lightning / Static Build-up).")]
            public float damage = 14f;
            [Tooltip("Chain jump range in metres, between enemies.")]
            public float jumpRange = 7f;
            [Tooltip("How far the FIRST arc may reach from the player. Measured: at 7 m a crowd standing " +
                     "8 m away got nothing — the power fired, cost its cooldown and drew no bolt.")]
            public float firstReach = 12f;
            public float scanRadius = 22f;
        }

        // Cyan lightning, violet for the Thunderstorm evolution; hops travel.
        static readonly Color ChainColor = new(0.3f, 0.7f, 1f, 1f);
        static readonly Color StormColor = new(0.62f, 0.45f, 1f, 1f);
        static readonly Color StaticColor = new(0.75f, 0.95f, 1f, 1f);
        const float HopSeconds = 0.035f;
        const float SparkScale = 0.3f;   // the spark blooms: small, or it hides the bolt

        static readonly string[] Ids = { SkillCatalogDefs.AutoChainLightning, SkillCatalogDefs.SmgStatic };
        public override string[] ProcIds => Ids;
        static readonly int[] Buffer = new int[TargetQuery.MaxChain];
        Action<Vector3> _skyStrike;

        public override void OnProc(SkillRuntime run, in SkillRuntime.PowerProc proc, Vector3 origin)
        {
            var a = Lib?.chain;
            if (a == null) return;
            string skillId = proc.skillId;
            bool staticJump = skillId == SkillCatalogDefs.SmgStatic;
            bool storm = !staticJump && run.IsEvolved(skillId);
            float damage = run.PowerDamage(a.damage, skillId);

            int found = TargetQuery.GatherEnemies(origin, a.scanRadius, Host.EnemyMask);
            // Only the candidates THIS proc gathered: passing the whole buffer walked stale entries
            // from earlier queries (arcs to enemies long dead or already pooled).
            int hops = found == 0 ? 0 : TargetQuery.Chain(found, origin, a.jumpRange,
                                                          Mathf.Min(proc.targets, TargetQuery.MaxChain), Buffer, a.firstReach);
            if (hops == 0) { run.Refund(skillId); return; }
            Host.Sfx(storm ? "sfx.skill.thunderstorm" : "sfx.skill.chain", origin, 0.75f, 0.1f);
            var fx = SkillFxDirector.Instance;
            string source = storm ? SkillCatalogDefs.EvoThunderstorm : skillId;

            Vector3 from = origin;
            for (int i = 0; i < hops; i++)
            {
                Vector3 point = TargetQuery.CandidatePoint(Buffer[i]);
                var col = TargetQuery.Candidate(Buffer[i]);

                // The BOLT is what makes this read as a chain: one call draws glow + white core +
                // forks, and each hop starts a beat after the last, so the eye follows it jumping.
                Vector3 a0 = from + Vector3.up * 1.0f, a1 = point + Vector3.up * 1.0f;
                float hopDelay = i * HopSeconds;
                // Static Build-up is a small spark jump off the gun, not the full Chain Lightning bolt.
                fx?.DrawArc(a0, a1, storm ? StormColor : staticJump ? StaticColor : ChainColor,
                            storm ? 1.25f : staticJump ? 0.65f : 1f, hopDelay, storm ? 2 : staticJump ? 0 : 1);
                // Thunderstorm: a bolt from the sky on every other enemy the storm jumps through.
                if (storm && i % 2 == 0) Host.Delay(hopDelay, _skyStrike ??= SkyStrike, point);

                PowerKit.Hit(col, damage, 0.3f, source);
                // Spark at chest height where the bolt lands.
                Host.PlayDelayed(a.sparkFx, a1, SparkScale, hopDelay);
                from = point;                        // next hop starts where this one landed
            }
        }

        void SkyStrike(Vector3 at)
        {
            var a = Lib.chain;
            FxPool.Play(a.skyStrikeFx, at, PowerKit.Flat(a.skyStrikeFx), 1.3f);
            SkillFxDirector.Instance?.Pulse(at, 1.6f, new Color(0.62f, 0.45f, 1f, 0.9f), 0.28f, 0.2f);
            Host.Sfx("sfx.skill.thunderstorm", at, 0.8f, 0.3f);
            Host.Shake(0.08f);
        }
    }
}
