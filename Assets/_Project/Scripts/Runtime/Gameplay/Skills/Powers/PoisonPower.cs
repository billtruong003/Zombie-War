using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>
    /// Poison, the status every poison card shares (Acid Rounds now, Toxic Cloud and Plague later):
    /// stacks up to <see cref="SkillRuntime.PoisonMaxStacks"/>, each stack burns its damage a second,
    /// a new stack refreshes the timer. Poisoned enemies pulse lime and bubble on each tick.
    /// </summary>
    public sealed class PoisonPower : PowerModule
    {
        [Serializable]
        public sealed class Assets
        {
            [Tooltip("A small poison puff on a poisoned enemy, each tick (budgeted).")]
            public ParticleSystem tickFx;
            public Color tint = new(0.62f, 1f, 0.2f, 0.7f);
        }

        struct Entry
        {
            public ZombieBase enemy;
            public int stacks;
            public float dps, until;
            public string source;
        }

        const int MaxTracked = 96;
        const float TickSeconds = 0.5f;
        const int MaxPuffsPerTick = 6;
        readonly List<Entry> _entries = new(MaxTracked);
        readonly Dictionary<ZombieBase, int> _index = new(MaxTracked);   // enemy -> slot (no linear scans)

        void RemoveAt(int i)
        {
            var gone = _entries[i].enemy;
            int last = _entries.Count - 1;
            if (i != last)
            {
                _entries[i] = _entries[last];
                if (!ReferenceEquals(_entries[i].enemy, null)) _index[_entries[i].enemy] = i;
            }
            _entries.RemoveAt(last);
            if (!ReferenceEquals(gone, null)) _index.Remove(gone);
        }
        float _tickAt;

        /// <summary>Adds <paramref name="stacks"/> to <paramref name="enemy"/> (capped) and refreshes the
        /// timer. The strongest per-stack damage seen wins, so a weak source never dilutes a strong one.</summary>
        public void Apply(ZombieBase enemy, int stacks, float dpsPerStack, float seconds, string source)
        {
            if (enemy == null || enemy.IsDead || stacks <= 0 || dpsPerStack <= 0f) return;
            float until = Time.time + seconds;
            if (_index.TryGetValue(enemy, out int i))
            {
                var e = _entries[i];
                e.stacks = Mathf.Min(SkillRuntime.PoisonMaxStacks, e.stacks + stacks);
                e.until = Mathf.Max(e.until, until);
                if (dpsPerStack >= e.dps) { e.dps = dpsPerStack; e.source = source; }
                _entries[i] = e;
                return;
            }
            if (_entries.Count >= MaxTracked) return;
            _index[enemy] = _entries.Count;
            _entries.Add(new Entry { enemy = enemy, stacks = Mathf.Min(SkillRuntime.PoisonMaxStacks, stacks), dps = dpsPerStack, until = until, source = source });
            var a = Lib?.poison;
            if (a != null) SkillFxDirector.Instance?.TintEnemy(enemy, a.tint, seconds);
        }

        public int StacksOn(ZombieBase enemy)
        {
            return enemy != null && _index.TryGetValue(enemy, out int i) ? _entries[i].stacks : 0;
        }

        public override void Tick(SkillRuntime run, Vector3 player, float dt)
        {
            if (_entries.Count == 0) return;
            float now = Time.time;
            if (now < _tickAt) return;
            _tickAt = now + TickSeconds;
            var a = Lib?.poison;
            int puffs = 0;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var e = _entries[i];
                if (e.enemy == null || e.enemy.IsDead || !e.enemy.isActiveAndEnabled || now >= e.until)
                {
                    RemoveAt(i);
                    continue;
                }
                PowerKit.Hit(e.enemy, e.stacks * e.dps * TickSeconds, 0f, e.source);
                if (a == null) continue;
                SkillFxDirector.Instance?.TintEnemy(e.enemy, a.tint, TickSeconds + 0.1f);
                if (puffs < MaxPuffsPerTick && a.tickFx != null && Host.OnScreen(e.enemy.transform.position))
                {
                    puffs++;
                    FxPool.Play(a.tickFx, PowerKit.Chest(e.enemy), PowerKit.Flat(a.tickFx), 0.25f + 0.05f * e.stacks);
                }
            }
        }

        /// Plague: a poisoned enemy that dies passes two stacks to the three nearest enemies.
        public override void OnKill(SkillRuntime run, Vector3 at)
        {
            if (!run.IsEvolved(SkillCatalogDefs.AutoToxic)) return;
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e.enemy == null || !e.enemy.IsDead) continue;
                Vector3 d = e.enemy.transform.position - at; d.y = 0f;
                if (d.sqrMagnitude > 0.5f) continue;
                RemoveAt(i);
                // Deferred a beat: kills arrive from inside damage loops walking the shared buffer.
                _spreadAt = at; _spreadDps = e.dps;
                Host.Delay(0.05f, _spread ??= Spread, at);
                return;
            }
        }

        Vector3 _spreadAt;
        float _spreadDps;
        Action<Vector3> _spread;

        void Spread(Vector3 at)
        {
            var a = Lib?.poison;
            int found = TargetQuery.GatherEnemies(at, 3f);
            int given = 0;
            for (int i = 0; i < found && given < 3; i++)
            {
                var e = TargetQuery.CandidateEnemy(i);
                if (e == null || e.IsDead) continue;
                Apply(e, 2, _spreadDps, SkillRuntime.PoisonSeconds, SkillCatalogDefs.EvoPlague);
                if (a != null) SkillFxDirector.Instance?.DrawArc(at + Vector3.up * 0.8f, PowerKit.Chest(e), a.tint, 0.5f, 0.02f * given, 0);
                given++;
            }
            if (given > 0 && a != null) FxPool.Play(a.tickFx, at + Vector3.up * 0.6f, PowerKit.Flat(a.tickFx), 0.6f);
        }

        public override void ResetForRun() { _entries.Clear(); _index.Clear(); }
    }
}
