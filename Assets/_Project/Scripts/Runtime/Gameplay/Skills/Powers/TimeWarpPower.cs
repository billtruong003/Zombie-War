using System;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>Time Warp — time drags for every enemy on the field: all of them move at half speed
    /// for a few seconds. A clock face opens under the player and a ring sweeps the whole screen.</summary>
    public sealed class TimeWarpPower : PowerModule
    {
        [Serializable]
        public sealed class Assets
        {
            [Tooltip("The clock / magic circle under the player while time is slow.")]
            public ParticleSystem circleFx;
            public float slow = 0.5f;
            public float reach = 30f;
            public Color tint = new(0.55f, 0.6f, 1f, 0.55f);
        }

        static readonly Color Clock = new(0.6f, 0.65f, 1f, 1f);
        static readonly string[] Ids = { SkillCatalogDefs.AutoTimeWarp };
        public override string[] ProcIds => Ids;

        float _until, _refreshAt;
        bool _stop;
        static readonly Color StopTint = new(0.8f, 0.85f, 1f, 0.75f);

        public override void OnProc(SkillRuntime run, in SkillRuntime.PowerProc proc, Vector3 origin)
        {
            var a = Lib?.timeWarp;
            if (a == null) return;
            _stop = run.IsEvolved(SkillCatalogDefs.AutoTimeWarp);
            float seconds = _stop ? 2f : run.TimeWarpSeconds;
            _until = Time.time + seconds;
            _refreshAt = 0f;
            FxPool.PlayFor(a.circleFx, origin + Vector3.up * 0.05f, PowerKit.Flat(a.circleFx), 1.2f, seconds);
            // A clock ring at the feet and a thin line sweeping the screen: the slow tint on every
            // enemy is the real read (a 14 m shockwave band turned the whole screen white).
            Host.Shockwave(origin, 0.5f, 5f, Clock, 0.7f);
            Host.Sfx("sfx.skill.timewarp", origin, 0.8f, 0.3f);
            Host.Shake(0.1f);
        }

        public override void Tick(SkillRuntime run, Vector3 p, float dt)
        {
            float now = Time.time;
            if (now >= _until || now < _refreshAt) return;
            _refreshAt = now + 0.5f;
            var a = Lib.timeWarp;
            // Re-applied twice a second so enemies that walk in during the warp slow down too.
            float left = _until - now;
            int found = TargetQuery.GatherEnemies(p, a.reach);
            for (int i = 0; i < found; i++)
            {
                var e = TargetQuery.CandidateEnemy(i);
                if (e == null || e.IsDead) continue;
                if (_stop)
                {
                    // Time Stop: frozen solid (and frozen enemies take +50%).
                    StatusCarrier.Apply(e.transform.GetInstanceID(), StatusKind.Frozen, 1f, Mathf.Min(0.6f, left), now);
                    SkillFxDirector.Instance?.TintEnemy(e, StopTint, 0.6f);
                }
                else
                {
                    StatusCarrier.Apply(e.transform.GetInstanceID(), StatusKind.Slow, a.slow, Mathf.Min(0.6f, left), now);
                    SkillFxDirector.Instance?.TintEnemy(e, a.tint, 0.6f);
                }
            }
        }

        public override void ResetForRun() => _until = 0f;
    }
}
