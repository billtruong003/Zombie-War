using System;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>
    /// The gun modifiers that act after a bullet lands (Ricochet, Explosive Rounds, Acid Rounds, the
    /// crit pop) and Blood Siphon's heal. Piercing, Split Shot and Double Tap change the shot itself
    /// and live in the weapon's fire path; their numbers come from <see cref="SkillRuntime.OnShotFired"/>.
    /// </summary>
    public sealed class GunModsPower : PowerModule
    {
        [Serializable]
        public sealed class Assets
        {
            [Tooltip("Tracer drawn along each ricochet bounce (the gun tracer mesh, tinted).")]
            public GameObject ricochetTracer;
            public ParticleSystem ricochetSparkFx;
            [Tooltip("Explosive Rounds: the small burst on a hit.")]
            public ParticleSystem explosiveFx;
            [Tooltip("Critical Rounds: a gold sparkle on the hit (budgeted; the gold number shows every crit).")]
            public ParticleSystem critPopFx;
            [Tooltip("Blood Siphon: the blood soul flying from the kill into the player.")]
            public ParticleSystem siphonWispFx;
            [Tooltip("Blood Siphon: the heal on the player.")]
            public ParticleSystem siphonHealFx;
        }

        const float RicochetRange = 7f;
        static readonly Color RicochetTint = new(1f, 0.9f, 0.45f, 1f);
        float _critPopAt, _explosiveSfxAt;
        readonly int[] _visited = new int[8];

        Assets A => Lib != null ? Lib.gunMods : null;

        /// <summary>A bullet from the player's gun hit <paramref name="enemy"/> for <paramref name="damage"/>.</summary>
        public override void OnGunHit(SkillRuntime run, ZombieBase enemy, Vector3 point, float damage, bool crit)
        {
            var a = A;
            if (a == null || enemy == null) return;
            float now = Time.time;

            if (crit && now >= _critPopAt && a.critPopFx != null)
            {
                _critPopAt = now + 0.35f;              // the gold number shows every crit; the pop is a garnish
                FxPool.Play(a.critPopFx, point, PowerKit.Flat(a.critPopFx), 0.4f);
                Host.Sfx("sfx.skill.crit", point, 0.5f, 0.12f);
            }

            float acid = run.AcidDps;
            if (acid > 0f) Host.Poison(enemy, 1, acid, SkillRuntime.PoisonSeconds, SkillCatalogDefs.UniAcid);

            if (run.RollExplosive()) Explode(enemy, point, damage * SkillRuntime.ExplosiveShare);

            int bounces = run.RicochetBounces;
            if (bounces > 0) Ricochet(run, enemy, point, damage * run.RicochetShare, bounces);
        }

        void Explode(ZombieBase direct, Vector3 point, float damage)
        {
            var a = A;
            point.y = Mathf.Max(0.3f, point.y);
            FxPool.Play(a.explosiveFx, point, PowerKit.Flat(a.explosiveFx), 0.55f);
            if (Time.time >= _explosiveSfxAt) { _explosiveSfxAt = Time.time + 0.08f; Host.Sfx("sfx.skill.blast", point, 0.35f, 0.08f); }
            int found = TargetQuery.GatherEnemies(point, SkillRuntime.ExplosiveRadius);
            for (int i = 0; i < found; i++)
            {
                var e = TargetQuery.CandidateEnemy(i);
                if (e != null && e != direct) PowerKit.Hit(e, damage, 0.4f, SkillCatalogDefs.UniExplosive);
            }
        }

        void Ricochet(SkillRuntime run, ZombieBase from, Vector3 point, float damage, int bounces)
        {
            var a = A;
            int visited = 0;
            _visited[visited++] = from.GetInstanceID();
            Vector3 at = point;
            for (int b = 0; b < bounces && visited < _visited.Length; b++)
            {
                int found = TargetQuery.GatherEnemies(at, RicochetRange);
                ZombieBase next = null;
                float best = float.MaxValue;
                for (int i = 0; i < found; i++)
                {
                    var e = TargetQuery.CandidateEnemy(i);
                    if (e == null || e.IsDead || Seen(e.GetInstanceID(), visited)) continue;
                    float d = (TargetQuery.CandidatePoint(i) - at).sqrMagnitude;
                    if (d < best) { best = d; next = e; }
                }
                if (next == null) break;
                _visited[visited++] = next.GetInstanceID();
                Vector3 to = PowerKit.Chest(next);
                if (a.ricochetTracer != null) TracerPool.Play(a.ricochetTracer, at, to, RicochetTint, 0.8f);
                FxPool.Play(a.ricochetSparkFx, to, PowerKit.Flat(a.ricochetSparkFx), 0.35f);
                PowerKit.Hit(next, damage, 0.2f, SkillCatalogDefs.UniRicochet);
                at = to;
            }
            if (visited > 1) Host.Sfx("sfx.skill.ricochet", point, 0.4f, 0.06f);
        }

        bool Seen(int id, int count)
        {
            for (int i = 0; i < count; i++) if (_visited[i] == id) return true;
            return false;
        }

        public override void OnKill(SkillRuntime run, Vector3 at)
        {
            if (!run.ConsumeSiphonTrigger()) return;
            var a = A;
            if (a == null) return;
            // The heal itself is queued in the runtime and paid by the driver; this is its read.
            Host.SoulWisp(at, a.siphonWispFx);
            FxPool.Play(a.siphonHealFx, Host.Player.position + Vector3.up * 0.2f, PowerKit.Flat(a.siphonHealFx), 0.8f);
            Host.Sfx("sfx.pickup.health", Host.Player.position, 0.6f, 0.2f);
        }
    }
}
