using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>Ice Shards — a ring of ice spikes bursts out of the player, piercing everything in
    /// its path and slowing what it cuts.</summary>
    public sealed class IceShardsPower : PowerModule
    {
        [Serializable]
        public sealed class Assets
        {
            [Tooltip("SK_IceShard: the pack's frost missile without its projectile script.")]
            public ParticleSystem shardFx;
            public ParticleSystem hitFx;
            public ParticleSystem castFx;
            public float baseDamage = 12f;
            public float speed = 14f;
            public float range = 9f;
            public float slow = 0.4f;
        }

        struct Shard
        {
            public ParticleSystem ps;
            public Vector3 pos, dir;
            public float travelled;
            public HashSet<int> hit;
        }

        const int MaxShards = 20;
        const float Contact = 0.8f;
        static readonly string[] Ids = { SkillCatalogDefs.AutoIceShards };
        public override string[] ProcIds => Ids;
        readonly List<Shard> _shards = new(MaxShards);
        readonly Stack<HashSet<int>> _sets = new();

        Assets A => Lib != null ? Lib.iceShards : null;

        public override void OnProc(SkillRuntime run, in SkillRuntime.PowerProc proc, Vector3 origin)
        {
            var a = A;
            if (a == null) return;
            if (run.IsEvolved(SkillCatalogDefs.AutoIceShards)) return;   // Blizzard's ring never stops
            int n = Mathf.Max(1, proc.targets);
            float spin = UnityEngine.Random.Range(0f, 360f);
            Vector3 from = origin + Vector3.up * 0.9f;
            for (int k = 0; k < n && _shards.Count < MaxShards; k++)
            {
                Vector3 dir = Quaternion.Euler(0f, spin + k * 360f / n, 0f) * Vector3.forward;
                var ps = FxPool.PlayFor(a.shardFx, from, Quaternion.LookRotation(dir), 0.8f, a.range / a.speed + 0.1f);
                var set = _sets.Count > 0 ? _sets.Pop() : new HashSet<int>();
                set.Clear();
                _shards.Add(new Shard { ps = ps, pos = from, dir = dir, hit = set });
            }
            FxPool.Play(a.castFx, origin + Vector3.up * 0.1f, PowerKit.Flat(a.castFx), 0.8f);
            Host.Shockwave(origin, 0.3f, 2.2f, new Color(0.6f, 0.9f, 1f, 1f), 0.35f);
            Host.Sfx("sfx.skill.frost", origin, 0.7f, 0.15f);
        }

        // ── Blizzard
        const int Ring = 6;
        readonly ParticleSystem[] _ring = new ParticleSystem[Ring];
        readonly Dictionary<int, float> _ringHit = new(64);
        readonly List<int> _stale = new(64);
        float _ringAngle, _ringFxAt, _ringScanAt;

        void TickBlizzard(SkillRuntime run, Vector3 p, float dt)
        {
            var a = A;
            float now = Time.time;
            float radius = 3f * run.AreaMultiplier;
            _ringAngle = (_ringAngle + 150f * dt) % 360f;
            bool refresh = now >= _ringFxAt;
            if (refresh) _ringFxAt = now + 2.9f;
            for (int k = 0; k < Ring; k++)
            {
                float ang = (_ringAngle + k * 360f / Ring) * Mathf.Deg2Rad;
                Vector3 pos = p + new Vector3(Mathf.Cos(ang) * radius, 0.9f, Mathf.Sin(ang) * radius);
                Vector3 tangent = new(-Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                if (refresh || _ring[k] == null) _ring[k] = FxPool.PlayFor(a.shardFx, pos, Quaternion.LookRotation(tangent), 0.8f, 3f);
                if (_ring[k] != null) { _ring[k].transform.position = pos; _ring[k].transform.rotation = Quaternion.LookRotation(tangent); }
            }
            if (now < _ringScanAt) return;
            _ringScanAt = now + 0.1f;
            float damage = run.PowerDamage(a.baseDamage, SkillCatalogDefs.AutoIceShards) * 0.6f;
            int found = TargetQuery.GatherEnemies(p, radius + 1f, Host.EnemyMask);
            for (int c = 0; c < found; c++)
            {
                Vector3 ep = TargetQuery.CandidatePoint(c);
                bool touched = false;
                for (int k = 0; k < Ring && !touched; k++)
                {
                    float ang = (_ringAngle + k * 360f / Ring) * Mathf.Deg2Rad;
                    float dx = ep.x - (p.x + Mathf.Cos(ang) * radius), dz = ep.z - (p.z + Mathf.Sin(ang) * radius);
                    touched = dx * dx + dz * dz <= Contact * Contact * 1.4f;
                }
                if (!touched) continue;
                int id = TargetQuery.CandidateId(c);
                if (_ringHit.TryGetValue(id, out float next) && now < next) continue;
                var e = TargetQuery.CandidateEnemy(c);
                if (e == null || e.IsDead) continue;
                _ringHit[id] = now + 0.8f;
                PowerKit.Hit(e, damage, 0.3f, SkillCatalogDefs.EvoBlizzard);
                StatusCarrier.Apply(e.transform.GetInstanceID(), StatusKind.Frozen, 1f, 1f, now);
                SkillFxDirector.Instance?.TintEnemy(e, Lib.frost.frozenTint, 1f);
                FxPool.Play(a.hitFx, PowerKit.Chest(e), PowerKit.Flat(a.hitFx), 0.35f);
            }
            if (_ringHit.Count > 96)
            {
                _stale.Clear();
                foreach (var kv in _ringHit) if (now - kv.Value > 2f) _stale.Add(kv.Key);
                foreach (var id in _stale) _ringHit.Remove(id);
            }
        }

        public override void Tick(SkillRuntime run, Vector3 p, float dt)
        {
            if (A != null && run.IsEvolved(SkillCatalogDefs.AutoIceShards)) TickBlizzard(run, p, dt);
            if (_shards.Count == 0) return;
            var a = A;
            float damage = run.PowerDamage(a.baseDamage, SkillCatalogDefs.AutoIceShards);
            float now = Time.time;
            for (int i = _shards.Count - 1; i >= 0; i--)
            {
                var s = _shards[i];
                float step = a.speed * dt;
                s.pos += s.dir * step; s.travelled += step;
                if (s.ps != null) s.ps.transform.position = s.pos;
                if (s.travelled >= a.range)
                {
                    if (s.ps != null) s.ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    _sets.Push(s.hit);
                    _shards.RemoveAt(i);
                    continue;
                }
                int found = TargetQuery.GatherEnemies(s.pos, Contact, Host.EnemyMask);
                for (int k = 0; k < found; k++)
                {
                    var e = TargetQuery.CandidateEnemy(k);
                    if (e == null || e.IsDead || !s.hit.Add(e.GetInstanceID())) continue;
                    PowerKit.Hit(e, damage, 0.3f, SkillCatalogDefs.AutoIceShards);
                    StatusCarrier.Apply(e.transform.GetInstanceID(), StatusKind.Slow, a.slow, 1.5f, now);
                    SkillFxDirector.Instance?.TintEnemy(e, Lib.frost.slowTint, 1.5f);
                    FxPool.Play(a.hitFx, PowerKit.Chest(e), PowerKit.Flat(a.hitFx), 0.35f);
                }
                _shards[i] = s;
            }
        }

        public override void ResetForRun()
        {
            foreach (var s in _shards) { if (s.ps != null) s.ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); _sets.Push(s.hit); }
            _shards.Clear();
            // Blizzard's ring holds pooled systems: stop them now, and never keep steering one
            // after the pool may have handed it to someone else.
            for (int k = 0; k < Ring; k++)
            {
                if (_ring[k] != null) _ring[k].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _ring[k] = null;
            }
            _ringHit.Clear();
            _ringFxAt = _ringScanAt = 0f;
        }
    }
}
