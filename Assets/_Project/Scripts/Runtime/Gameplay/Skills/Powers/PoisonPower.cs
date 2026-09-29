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
        float _tickAt;

        /// <summary>Adds <paramref name="stacks"/> to <paramref name="enemy"/> (capped) and refreshes the
        /// timer. The strongest per-stack damage seen wins, so a weak source never dilutes a strong one.</summary>
        public void Apply(ZombieBase enemy, int stacks, float dpsPerStack, float seconds, string source)
        {
            if (enemy == null || enemy.IsDead || stacks <= 0 || dpsPerStack <= 0f) return;
            float until = Time.time + seconds;
            for (int i = 0; i < _entries.Count; i++)
            {
                var e = _entries[i];
                if (e.enemy != enemy) continue;
                e.stacks = Mathf.Min(SkillRuntime.PoisonMaxStacks, e.stacks + stacks);
                e.until = Mathf.Max(e.until, until);
                if (dpsPerStack >= e.dps) { e.dps = dpsPerStack; e.source = source; }
                _entries[i] = e;
                return;
            }
            if (_entries.Count >= MaxTracked) return;
            _entries.Add(new Entry { enemy = enemy, stacks = Mathf.Min(SkillRuntime.PoisonMaxStacks, stacks), dps = dpsPerStack, until = until, source = source });
            var a = Lib?.poison;
            if (a != null) SkillFxDirector.Instance?.TintEnemy(enemy, a.tint, seconds);
        }

        public int StacksOn(ZombieBase enemy)
        {
            for (int i = 0; i < _entries.Count; i++) if (_entries[i].enemy == enemy) return _entries[i].stacks;
            return 0;
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
                    _entries.RemoveAt(i);
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

        public override void ResetForRun() => _entries.Clear();
    }
}
