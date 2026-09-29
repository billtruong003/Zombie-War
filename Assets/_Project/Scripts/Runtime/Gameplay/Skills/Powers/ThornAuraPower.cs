using System;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>Thorn Aura — a ring of thorns around the player: whatever touches it bleeds and is
    /// thrown back. A tank card: it rewards letting the horde come close.</summary>
    public sealed class ThornAuraPower : PowerModule
    {
        [Serializable]
        public sealed class Assets
        {
            [Tooltip("The spikes bursting on an enemy the aura hits.")]
            public ParticleSystem hitFx;
            public Color ringColor = new(0.95f, 0.35f, 0.55f, 1f);
        }

        const float TickSeconds = 0.5f;
        const int MaxHitFx = 4;
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int ErodeId = Shader.PropertyToID("_Erode");

        MeshRenderer _ring;
        float _tickAt, _flash;

        Assets A => Lib != null ? Lib.thorns : null;

        public override void Tick(SkillRuntime run, Vector3 p, float dt)
        {
            var a = A;
            float radius = run.ThornAuraRadius;
            bool on = a != null && radius > 0f;
            DrawRing(on, p, radius, dt);
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

        void DrawRing(bool on, Vector3 p, float radius, float dt)
        {
            if (!on) { if (_ring != null && _ring.gameObject.activeSelf) _ring.gameObject.SetActive(false); return; }
            if (_ring == null)
            {
                _ring = Host.MakeGroundRenderer("ThornRing");
                if (_ring == null || Lib.shared.shockwaveMaterial == null) return;
                _ring.sharedMaterial = Lib.shared.shockwaveMaterial;
            }
            if (!_ring.gameObject.activeSelf) _ring.gameObject.SetActive(true);
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 3f);
            _ring.transform.position = new Vector3(p.x, 0.07f, p.z);
            // Breathes a little, and snaps outward on each hit.
            float r = radius * (1f + 0.04f * Mathf.Sin(Time.time * 4f) + 0.08f * _flash);
            _ring.transform.localScale = new Vector3(r * 2f, 1f, r * 2f);
            _ring.transform.rotation = Quaternion.Euler(0f, Time.time * 40f, 0f);
            var mpb = Host.Block;
            mpb.Clear();
            var c = A.ringColor; c.a = 0.75f + 0.25f * _flash;
            mpb.SetColor(TintId, c);
            mpb.SetFloat(ErodeId, 0.28f - 0.12f * _flash);
            _ring.SetPropertyBlock(mpb);
        }

        /// <summary>Iron Maiden: the Kinetic Shield broke — a ring of spikes bursts out of the player.</summary>
        public void OnShieldBroken(SkillRuntime run)
        {
            var a = A;
            if (a == null || run == null || !run.IsEvolved(SkillCatalogDefs.AutoThorns)) return;
            Vector3 p = Host.Player.position;
            float r = 4f * run.AreaMultiplier;
            Host.Shockwave(p, 0.4f, r, a.ringColor, 0.45f);
            SkillFxDirector.Instance?.Pulse(p, r, a.ringColor, 0.3f, 0.2f);
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

        public override void ResetForRun() { if (_ring != null) _ring.gameObject.SetActive(false); }
    }
}
