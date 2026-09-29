using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>Airstrike — one bombing pass over what the player can see, bombs landing in order
    /// along the flight line.</summary>
    public sealed class AirstrikePower : PowerModule
    {
        [Serializable]
        public sealed class Assets
        {
            public ParticleSystem blastFx;
            public float blastNativeRadius = 2f;
            public ParticleSystem decalFx;
            public float baseDamage = 45f;
            public float delay = 0.75f;
        }

        static readonly string[] Ids = { SkillCatalogDefs.AutoAirstrike };
        public override string[] ProcIds => Ids;

        readonly List<int> _visible = new(32);
        readonly List<Vector3> _targets = new(8);

        public override void OnProc(SkillRuntime run, in SkillRuntime.PowerProc proc, Vector3 around)
        {
            var a = Lib?.airstrike;
            if (a == null) return;
            float damage = run.PowerDamage(a.baseDamage, SkillCatalogDefs.AutoAirstrike);
            int found = TargetQuery.GatherEnemies(around, 16f, Host.EnemyMask);
            Host.Sfx("sfx.skill.airstrike.mark", around, 0.6f, 0.3f);

            // Only enemies the player can SEE. Measured: picking inside a 13 m sphere put most bombs
            // off the sides of a portrait screen, so the power landed where nobody could watch it.
            _visible.Clear();
            for (int i = 0; i < found; i++)
                if (Host.OnScreen(TargetQuery.CandidatePoint(i))) _visible.Add(i);

            // One pass overhead: every bomb comes from the same direction and they land in order
            // along that line — a bombing run, not five random pops.
            Vector3 flight = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f) * Vector3.forward;
            _targets.Clear();
            for (int b = 0; b < proc.targets; b++)
            {
                Vector3 target;
                if (_visible.Count > 0)
                {
                    int pick = UnityEngine.Random.Range(0, _visible.Count);
                    target = TargetQuery.CandidatePoint(_visible[pick]);
                    _visible.RemoveAt(pick);           // distinct enemies while they last
                }
                else
                {
                    // Nothing (left) on screen: carpet the ground around the player instead.
                    target = around;
                    for (int tries = 0; tries < 6; tries++)
                    {
                        var r = UnityEngine.Random.insideUnitCircle * 4.5f;
                        target = around + new Vector3(r.x, 0f, r.y);
                        if (Host.OnScreen(target)) break;
                    }
                }
                _targets.Add(target);
            }
            _targets.Sort((x, y) => Vector3.Dot(x, flight).CompareTo(Vector3.Dot(y, flight)));
            for (int b = 0; b < _targets.Count; b++)
                Host.ScheduleBlast(new BlastSpec
                {
                    pos = _targets[b], radius = proc.radius, damage = damage, delay = a.delay + b * 0.14f,
                    fx = a.blastFx, fxNativeRadius = a.blastNativeRadius, sfx = "sfx.skill.airstrike.blast",
                    shake = 0.22f, push = 1.4f, bomb = Lib.shared.bombFx, decal = a.decalFx, flight = flight,
                    source = SkillCatalogDefs.AutoAirstrike,
                });
        }
    }
}
