using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>Sentry Turret — a turret drops where the player stands and guns down the nearest
    /// enemies for six seconds (two turrets at rank 5). A summon that holds ground the player leaves.</summary>
    public sealed class SentryTurretPower : PowerModule
    {
        [Serializable]
        public sealed class Assets
        {
            [Tooltip("SK_Turret: a base and a 'Head' child that turns; a child whose name contains 'Muzzle' marks the gun tip.")]
            public GameObject model;
            public float modelScale = 1.6f;
            public GameObject tracer;
            public ParticleSystem muzzleFx;
            public ParticleSystem hitFx;
            [Tooltip("Dust where it lands and where it folds away.")]
            public ParticleSystem dropFx;
            public float baseDamage = 9f;
            public float range = 12f;
            [Tooltip("Fortress: the rocket's burst.")]
            public ParticleSystem rocketBlastFx;
            public float rocketRadius = 1.6f;
        }

        sealed class Turret
        {
            public Transform root, head, muzzle;
            public float until, nextShot, born;
            public bool live;
            public Quaternion headRest;   // the head's world rotation with the holder unrotated
        }

        static readonly Color TracerTint = new(1f, 0.62f, 0.2f, 1f);
        const int MaxTurrets = 4;
        const float PopSeconds = 0.22f;
        static readonly string[] Ids = { SkillCatalogDefs.AutoTurret };
        public override string[] ProcIds => Ids;

        readonly List<Turret> _turrets = new(MaxTurrets);

        Assets A => Lib != null ? Lib.turret : null;

        public override void OnProc(SkillRuntime run, in SkillRuntime.PowerProc proc, Vector3 origin)
        {
            var a = A;
            if (a == null || a.model == null) return;
            int count = Mathf.Max(1, proc.targets);
            Vector3 side = Host.Player.right; side.y = 0f;
            for (int k = 0; k < count; k++)
            {
                var t = Take();
                Vector3 at = origin + (count > 1 ? side.normalized * (k == 0 ? -1.1f : 1.1f) : -Host.Player.forward * 0.9f);
                at.y = 0f;
                t.root.position = at;
                t.root.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                t.root.localScale = Vector3.zero;
                t.root.gameObject.SetActive(true);
                t.born = Time.time;
                t.until = Time.time + SkillRuntime.TurretSeconds;
                t.nextShot = Time.time + PopSeconds + 0.1f * k;
                t.live = true;
                FxPool.Play(a.dropFx, at, PowerKit.Flat(a.dropFx), 0.8f);
            }
            Host.Sfx("sfx.skill.deploy", origin, 0.7f, 0.2f);
        }

        Turret Take()
        {
            // A free turret, or the oldest one recycled: never more than MaxTurrets in the world.
            Turret oldest = null;
            for (int i = 0; i < _turrets.Count; i++)
            {
                var t = _turrets[i];
                if (!t.live) return t;
                if (oldest == null || t.born < oldest.born) oldest = t;
            }
            if (_turrets.Count < MaxTurrets) { var n = Build(); _turrets.Add(n); return n; }
            return oldest;
        }

        Turret Build()
        {
            var a = A;
            // A holder, so the random yaw and the pop scale never overwrite the model's import pose.
            var holder = new GameObject("turret");
            holder.transform.SetParent(Host.Root, false);
            var go = UnityEngine.Object.Instantiate(a.model, holder.transform);
            go.transform.localPosition = Vector3.zero;
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.Destroy(col);
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            Transform head = null, muzzle = null;
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Head") head = t;
                else if (t.name.Contains("Muzzle")) muzzle = t;
            }
            holder.SetActive(false);
            // Rest pose in world space with the holder unrotated: the head faces +Z in it.
            return new Turret { root = holder.transform, head = head ?? go.transform, muzzle = muzzle ?? go.transform,
                                headRest = head != null ? head.rotation : Quaternion.identity };
        }

        public override void Tick(SkillRuntime run, Vector3 player, float dt)
        {
            var a = A;
            if (a == null) return;
            float now = Time.time;
            float interval = 1f / Mathf.Max(0.1f, run.TurretShotsPerSecond);
            for (int i = 0; i < _turrets.Count; i++)
            {
                var t = _turrets[i];
                if (!t.live) continue;

                // Pop in with an overshoot, fold away at the end.
                float age = now - t.born, left = t.until - now;
                float s = age < PopSeconds ? Mathf.Sin(age / PopSeconds * Mathf.PI * 0.75f) / Mathf.Sin(Mathf.PI * 0.75f)
                        : left < PopSeconds ? Mathf.Clamp01(left / PopSeconds) : 1f;
                t.root.localScale = Vector3.one * (a.modelScale * Mathf.Max(0f, s));
                if (left <= 0f)
                {
                    t.live = false;
                    t.root.gameObject.SetActive(false);
                    FxPool.Play(a.dropFx, t.root.position, PowerKit.Flat(a.dropFx), 0.6f);
                    continue;
                }

                int found = TargetQuery.GatherEnemies(t.root.position, a.range, Host.EnemyMask);
                int best = TargetQuery.Nearest(found, t.root.position);
                var target = best >= 0 ? TargetQuery.CandidateEnemy(best) : null;
                if (target != null)
                {
                    Vector3 look = target.transform.position - t.head.position; look.y = 0f;
                    if (look.sqrMagnitude > 0.001f)
                    {
                        // The model faces +Z (Blender -Y forward, like the drone): yaw the head in world
                        // space on top of its import rest pose.
                        var want = Quaternion.LookRotation(look.normalized) * t.headRest;
                        t.head.rotation = Quaternion.Slerp(t.head.rotation, want, 1f - Mathf.Exp(-14f * dt));
                    }
                }
                if (target == null || target.IsDead || now < t.nextShot || age < PopSeconds) continue;
                t.nextShot = now + interval;
                Shoot(run, t, target);
            }
        }

        void Shoot(SkillRuntime run, Turret t, ZombieBase enemy)
        {
            var a = A;
            Vector3 from = t.muzzle.position, to = PowerKit.Chest(enemy);
            if (a.tracer != null) TracerPool.Play(a.tracer, from, to, TracerTint, 0.6f);
            FxPool.Play(a.muzzleFx, from, Quaternion.LookRotation(to - from), 0.4f);
            FxPool.Play(a.hitFx, to, PowerKit.Flat(a.hitFx), 0.45f);
            float damage = run.PowerDamage(a.baseDamage, SkillCatalogDefs.AutoTurret);
            if (!run.IsEvolved(SkillCatalogDefs.AutoTurret))
            {
                Host.Sfx("sfx.skill.drone", from, 0.3f, 0.05f);
                PowerKit.Hit(enemy, damage, 0.2f, SkillCatalogDefs.AutoTurret);
                return;
            }
            // Fortress: every round is a rocket that bursts on the target.
            Host.Sfx("sfx.skill.blast", to, 0.35f, 0.08f);
            FxPool.Play(a.rocketBlastFx, to, PowerKit.Flat(a.rocketBlastFx), 0.5f);
            int found = TargetQuery.GatherEnemies(to, a.rocketRadius, Host.EnemyMask);
            for (int k = 0; k < found; k++) PowerKit.Hit(TargetQuery.Candidate(k), damage, 0.5f, SkillCatalogDefs.EvoFortress);
        }

        public override void ResetForRun()
        {
            foreach (var t in _turrets) { t.live = false; if (t.root != null) t.root.gameObject.SetActive(false); }
        }
    }
}
