using System;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>Flame Burst — a cone of fire roars out toward the nearest crowd for a moment,
    /// burning everything inside it.</summary>
    public sealed class FlameBurstPower : PowerModule
    {
        [Serializable]
        public sealed class Assets
        {
            [Tooltip("A flamethrower jet (looping; played for the burst and stopped).")]
            public ParticleSystem jetFx;
            [Tooltip("Metres the jet reaches at scale 1 (measured in the sandbox: the pack's jet is short).")]
            public float jetNativeRange = 2.2f;
            [Tooltip("Flames licking an enemy inside the cone (budgeted).")]
            public ParticleSystem burnFx;
            public float dps = 30f;
            public float seconds = 0.8f;
            public float coneDegrees = 70f;
            public Color burnTint = new(1f, 0.5f, 0.12f, 0.5f);
        }

        const float TickSeconds = 0.1f;
        static readonly string[] Ids = { SkillCatalogDefs.AutoFlameBurst };
        public override string[] ProcIds => Ids;
        static readonly int[] Cone = new int[TargetQuery.MaxConsidered];

        float _until, _tickAt, _range;
        Vector3 _dir;
        ParticleSystem _jet;

        Assets A => Lib != null ? Lib.flameBurst : null;

        public override void OnProc(SkillRuntime run, in SkillRuntime.PowerProc proc, Vector3 origin)
        {
            var a = A;
            if (a == null) return;
            int found = TargetQuery.GatherEnemies(origin, proc.radius + 2f, Host.EnemyMask);
            if (found == 0) { run.Refund(proc.skillId); return; }
            Vector3 d = TargetQuery.CandidatePoint(TargetQuery.Nearest(found, origin)) - origin; d.y = 0f;
            _dir = d.sqrMagnitude > 0.01f ? d.normalized : Host.Player.forward;
            _range = proc.radius;
            _until = Time.time + a.seconds;
            _jet = FxPool.PlayFor(a.jetFx, origin + Vector3.up * 0.8f + _dir * 0.4f, Quaternion.LookRotation(_dir),
                                  _range / Mathf.Max(0.1f, a.jetNativeRange), a.seconds);
            SkillFxDirector.Instance?.ConeWave(origin, _dir, a.coneDegrees, _range, new Color(1f, 0.55f, 0.15f, 0.85f), a.seconds * 0.6f);
            Host.Sfx("sfx.skill.fire", origin, 0.85f, 0.2f);
        }

        public override void Tick(SkillRuntime run, Vector3 p, float dt)
        {
            float now = Time.time;
            if (now >= _until) return;
            var a = A;
            if (_jet != null) _jet.transform.position = p + Vector3.up * 0.8f + _dir * 0.4f;   // it comes out of the player
            if (now < _tickAt) return;
            _tickAt = now + TickSeconds;
            int found = TargetQuery.GatherEnemies(p, _range, Host.EnemyMask);
            int hits = TargetQuery.Cone(found, p, _dir, a.coneDegrees * 0.5f, Cone);
            float damage = run.PowerDamage(a.dps, SkillCatalogDefs.AutoFlameBurst) * TickSeconds;
            int flames = 0;
            for (int i = 0; i < hits; i++)
            {
                var e = PowerKit.EnemyOf(TargetQuery.Candidate(Cone[i]));
                if (e == null || e.IsDead) continue;
                PowerKit.Hit(e, damage, 0.15f, SkillCatalogDefs.AutoFlameBurst);
                SkillFxDirector.Instance?.TintEnemy(e, a.burnTint, 0.5f);
                if (flames++ < 3 && a.burnFx != null) FxPool.Play(a.burnFx, PowerKit.Chest(e), PowerKit.Flat(a.burnFx), 0.35f);
            }
        }

        public override void ResetForRun() => _until = 0f;
    }
}
