using System;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>
    /// The two blasts out of the player: Soul Burst (every N kills; evolution Reaper, whose kills can
    /// burst where the enemy fell) and Emergency Detonation (when nearly dead). They must never read
    /// alike: Soul Burst is routine and green, Emergency is a red panic button.
    /// </summary>
    public sealed class SelfBurstPower : PowerModule
    {
        [Serializable]
        public sealed class Assets
        {
            [Tooltip("Soul Burst: self-centred burst, fired by kills.")]
            public ParticleSystem soulBurstFx;
            public float soulBurstNativeRadius = 1.6f;
            [Tooltip("A2: the flat nova racing out to the edge of the burst (the Frost Nova family, green).")]
            public ParticleSystem soulNovaFx;
            [Tooltip("A2: the flat nova of Emergency Detonation (the same family, fire red).")]
            public ParticleSystem emergencyNovaFx;
            [Tooltip("Radius the nova prefabs cover at scale 1 (measured: the pack's ring reaches ~6.5 m).")]
            public float novaNativeRadius = 6.5f;
            [Tooltip("Reaper: the burst where an enemy fell.")]
            public ParticleSystem reaperFx;
            [Tooltip("Emergency Detonation: must be unmistakable and clearly NOT Soul Burst.")]
            public ParticleSystem emergencyFx;
            public float emergencyNativeRadius = 1.5f;
            [Tooltip("Damage at the centre of a Soul Burst / Emergency blast (Reaper: half).")]
            public float damage = 34f;
            public float scanRadius = 22f;
        }

        const float ScaleCap = 1.4f;
        static readonly string[] Ids = { SkillCatalogDefs.AutoSoulBurst, SkillCatalogDefs.AutoEmergency };
        public override string[] ProcIds => Ids;

        int _burstsThisFrame, _frame = -1;
        float _lastRadius;
        Action<Vector3> _secondWave;

        void SecondWave(Vector3 at) => Host.Shockwave(at, _lastRadius * 0.15f, _lastRadius * 0.8f, new Color(1f, 0.55f, 0.2f, 1f), 0.45f);
        float _reaperBudgetAt, _wispBudgetAt;

        public override void OnProc(SkillRuntime run, in SkillRuntime.PowerProc proc, Vector3 centre)
        {
            var a = Lib?.selfBurst;
            if (a == null) return;
            // <=2 concurrent explosions is a stated ceiling, enforced on the DAMAGE as well as the
            // visual, so a frame where both fire cannot stack a third.
            if (_frame != Time.frameCount) { _frame = Time.frameCount; _burstsThisFrame = 0; }
            if (_burstsThisFrame >= 2) return;

            int found = TargetQuery.GatherEnemies(centre, a.scanRadius, Host.EnemyMask);
            if (found == 0) return;
            _burstsThisFrame++;

            // Emergency: a shove, not a rescue — NO heal, NO invulnerability.
            bool emergency = proc.skillId == SkillCatalogDefs.AutoEmergency;
            float radius = proc.radius;
            float damage = run.PowerDamage(a.damage, proc.skillId);
            float push = emergency ? 3f : 1f;
            float r2 = radius * radius;
            for (int i = 0; i < found; i++)
            {
                Vector3 p = TargetQuery.CandidatePoint(i);
                if ((p - centre).sqrMagnitude > r2) continue;
                PowerKit.Hit(TargetQuery.Candidate(i), damage, push, proc.skillId);
            }
            // Sized to the blast but capped: a self-centred burst scaled to a 4 m radius covered the
            // whole screen and the player. The ring below still shows the true area.
            var fx = emergency ? a.emergencyFx : a.soulBurstFx;
            PowerKit.PlaySized(fx, centre + Vector3.up * 0.1f, radius, emergency ? a.emergencyNativeRadius : a.soulBurstNativeRadius, ScaleCap);
            // A2: a flat nova out to the edge of the hit area (Frost Nova's family, so every "burst out
            // of the player" reads alike) and a toon shockwave on the ground at the same radius.
            PowerKit.PlaySized(emergency ? a.emergencyNovaFx : a.soulNovaFx, centre + Vector3.up * 0.1f, radius, a.novaNativeRadius);
            Host.Shockwave(centre, radius * 0.25f, radius, emergency ? new Color(1f, 0.28f, 0.16f, 1f) : new Color(0.4f, 1f, 0.55f, 1f),
                           emergency ? 0.55f : 0.5f);
            if (emergency)
            {
                // The panic button hits twice as hard on the eye: a second, later ring and a red band.
                Host.Delay(0.12f, _secondWave ??= SecondWave, centre);
                _lastRadius = radius;
                Host.ShowDisc(centre, radius * 0.2f, radius, new Color(1f, 0.25f, 0.12f, 0.45f), 0.6f, false, 0.2f);
            }
            Host.Sfx(emergency ? "sfx.skill.emergency" : "sfx.skill.soulburst", centre, 0.85f, 0.1f);
            Host.Shake(emergency ? 0.4f : 0.14f);
            SkillFxDirector.Instance?.Pulse(centre, radius,
                emergency ? new Color(1f, 0.3f, 0.2f, 0.95f) : new Color(0.45f, 1f, 0.55f, 0.9f),
                emergency ? 0.28f : 0.35f, emergency ? 0.3f : 0.22f);
        }

        public override void OnKill(SkillRuntime run, Vector3 at)
        {
            // Soul Burst's kill counter made visible — each kill's soul flies into the player.
            float now = Time.time;
            if (run.Has(SkillCatalogDefs.AutoSoulBurst) && now >= _wispBudgetAt)
            {
                _wispBudgetAt = now + 0.12f;   // a crowd dying at once must not become a swarm
                Host.SoulWisp(at);
            }
            // Reaper: a kill can release a soul burst where the enemy fell. Budgeted to 4/s so a
            // chain of kills cannot cascade into a screen-wide wipe in one frame.
            if (run.RollReaper() && now >= _reaperBudgetAt) { _reaperBudgetAt = now + 0.25f; Reap(run, at); }
        }

        void Reap(SkillRuntime run, Vector3 at)
        {
            var a = Lib?.selfBurst;
            if (a == null) return;
            float damage = run.PowerDamage(a.damage * 0.5f, SkillCatalogDefs.AutoSoulBurst);
            // A scythe sweep where the enemy fell — a violet crescent and a soul burst.
            at.y = 0f;
            FxPool.Play(a.reaperFx, at + Vector3.up * 0.3f, PowerKit.Flat(a.reaperFx), 0.8f);
            SkillFxDirector.Instance?.ConeWave(at, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f) * Vector3.forward,
                                               230f, 2.4f, new Color(0.62f, 0.3f, 1f, 0.9f), 0.3f);
            Host.SoulWisp(at);
            Host.Sfx("sfx.skill.reaper", at, 0.8f, 0f);
            int found = TargetQuery.GatherEnemies(at, 2.4f, Host.EnemyMask);
            for (int c = 0; c < found; c++) PowerKit.Hit(TargetQuery.Candidate(c), damage, 0.8f, SkillCatalogDefs.EvoReaper);
        }
    }
}
