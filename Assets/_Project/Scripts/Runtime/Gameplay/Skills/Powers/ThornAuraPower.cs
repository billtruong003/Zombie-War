using System;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>Thorn Aura — a field of thorns around the player: whatever touches it bleeds and is
    /// thrown back. A tank card: it rewards letting the horde come close.</summary>
    public sealed class ThornAuraPower : PowerModule
    {
        [Serializable]
        public sealed class Assets
        {
            [Tooltip("The spikes bursting on an enemy the aura hits.")]
            public ParticleSystem hitFx;
            public Color ringColor = new(0.95f, 0.35f, 0.55f, 1f);
            [Tooltip("The aura on the ground: Epic Toon Magic Field (white) tinted pink, looping, sized to the radius.")]
            public ParticleSystem auraFx;
            [Tooltip("Iron Maiden's aura: the same field tinted deep red.")]
            public ParticleSystem maidenAuraFx;
            public float auraNativeRadius = 2.1f;
        }

        const float TickSeconds = 0.5f;
        const int MaxHitFx = 4;
        ParticleSystem _aura, _maidenAura;
        float _tickAt, _flash;

        Assets A => Lib != null ? Lib.thorns : null;

        public override void Tick(SkillRuntime run, Vector3 p, float dt)
        {
            var a = A;
            float radius = run.ThornAuraRadius;
            bool on = a != null && radius > 0f;
            DrawAura(on, run.IsEvolved(SkillCatalogDefs.AutoThorns), p, radius, dt);
            if (!on) return;

            float now = Time.time;
            if (now < _tickAt) return;
            _tickAt = now + TickSeconds;
            int found = TargetQuery.GatherEnemies(p, radius, Host.EnemyMask);
            if (found == 0) return;
            bool maiden = run.IsEvolved(SkillCatalogDefs.AutoThorns);
            // PowerDamage already adds the evolution's x1.5; Iron Maiden is twice the plain aura.
            float damage = run.PowerDamage(run.ThornDps, SkillCatalogDefs.AutoThorns) * TickSeconds * (maiden ? 4f / 3f : 1f);
            int fx = 0;
            for (int i = 0; i < found; i++)
            {
                var e = TargetQuery.CandidateEnemy(i);
                if (e == null || e.IsDead) continue;
                PowerKit.Hit(e, damage, 1.3f, maiden ? SkillCatalogDefs.EvoIronMaiden : SkillCatalogDefs.AutoThorns);
                if (fx++ < MaxHitFx) FxPool.Play(a.hitFx, PowerKit.Chest(e), PowerKit.Flat(a.hitFx), 0.35f);
            }
            _flash = 1f;
            Host.Sfx("sfx.skill.blade.hit", p, 0.35f, 0.15f);
        }

        /// The aura is one looping Epic Toon field that follows the player; a hit makes it swell briefly.
        void DrawAura(bool on, bool maiden, Vector3 p, float radius, float dt)
        {
            var a = A;
            if (!on || a == null) { Show(_aura, false); Show(_maidenAura, false); return; }
            var want = maiden ? a.maidenAuraFx : a.auraFx;
            if (want == null) return;
            ref ParticleSystem live = ref maiden ? ref _maidenAura : ref _aura;
            if (live == null)
            {
                live = UnityEngine.Object.Instantiate(want, Host.Root);
                live.name = maiden ? "IronMaidenAura" : "ThornAura";
            }
            Show(maiden ? _aura : _maidenAura, false);
            Show(live, true);
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 3f);
            live.transform.SetPositionAndRotation(new Vector3(p.x, 0.06f, p.z), PowerKit.Flat(want));
            float r = radius * (1f + 0.08f * _flash);
            live.transform.localScale = Vector3.one * (r / Mathf.Max(0.1f, a.auraNativeRadius));
        }

        static void Show(ParticleSystem ps, bool on)
        {
            if (ps == null || ps.gameObject.activeSelf == on) return;
            ps.gameObject.SetActive(on);
            if (on) ps.Play(true);
        }

        /// <summary>Iron Maiden: the Kinetic Shield broke — a ring of spikes bursts out of the player.</summary>
        public void OnShieldBroken(SkillRuntime run)
        {
            var a = A;
            if (a == null || run == null || !run.IsEvolved(SkillCatalogDefs.AutoThorns)) return;
            Vector3 p = Host.Player.position;
            float r = 4f * run.AreaMultiplier;
            Host.Shockwave(p, 0.4f, r, a.ringColor, 0.45f);
            Host.Shake(0.18f);
            float damage = run.PowerDamage(40f, SkillCatalogDefs.AutoThorns);
            int found = TargetQuery.GatherEnemies(p, r, Host.EnemyMask);
            for (int i = 0; i < found; i++)
            {
                var e = TargetQuery.CandidateEnemy(i);
                if (e == null || e.IsDead) continue;
                PowerKit.Hit(e, damage, 2.5f, SkillCatalogDefs.EvoIronMaiden);
                if (i < MaxHitFx) FxPool.Play(a.hitFx, PowerKit.Chest(e), PowerKit.Flat(a.hitFx), 0.45f);
            }
        }

        public override void ResetForRun() { Show(_aura, false); Show(_maidenAura, false); }
    }
}
