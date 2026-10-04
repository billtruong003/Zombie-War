using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using BillGameCore;

namespace ZombieWar
{
    /// Weapon, part: rays, pierce lines, splash, shockwave and hit resolution.
    public partial class Weapon
    {
        private void FireRay(WeaponData data, Vector3 rayOrigin, Vector3 muzzlePosition,
                             float rayRangeBonus, Vector3 direction)
        {
            float rayRange = data.range + rayRangeBonus;

            // PiercingLine (sniper/railgun): bắn 1 đường xuyên hết zombie. Docs/Reference/Design/WEAPON_DESIGN.md §3.
            // A3: Piercing Rounds and Breach Round make any bullet pierce (not a launcher shell,
            // which bursts on the first thing it meets).
            if (data.fireMode == FireMode.PiercingLine || (_shotPlan.bonusPierce > 0 && data.splashRadius <= 0f))
            {
                FireRayPiercing(data, rayOrigin, muzzlePosition, rayRange, direction);
                return;
            }

            // SingleHitscan / MultiPelletHitscan: dừng ở target đầu tiên.
            Vector3 hitPoint = muzzlePosition + direction * data.range;
            ZombieBase directEnemy = null;
            bool didHit = Physics.Raycast(rayOrigin, direction, out RaycastHit hit, rayRange, hitMask);

            if (didHit)
            {
                hitPoint = hit.point;
                directEnemy = null;
                ApplyHit(data, Damageable(hit.collider, out directEnemy), directEnemy, hit, rayOrigin, 1f);
            }
            if (data.splashRadius > 0f)
                Splash(data, hitPoint, didHit ? directEnemy : null);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EmitRay(data, rayOrigin, direction, hitPoint,
                didHit ? hit.collider.transform : null, blocked: false, pierceHits: 0);
#endif

            SpawnTracer(data, muzzlePosition, hitPoint);
            SpawnSmokeTrail(data, muzzlePosition, hitPoint);
        }

        // Launcher blast: every other living enemy within splashRadius of where the shot landed takes
        // a share of the shot's damage (the direct hit already took the full hit in ApplyHit).
        private bool _hitCrit;

        private void Splash(WeaponData data, Vector3 at, ZombieBase direct)
        {
            float damage = WeaponUpgradeMath.EffectiveDamage(data, _starLevel) * data.splashDamageFraction * (1f + _skinBonus);
            // A3 Launcher signatures (Cluster Charge, Napalm Shell) build on this blast.
            var run = ZombieWar.Skills.SkillRuntime.Active;
            if (run != null)
                ZombieWar.Skills.SkillArsenal.Instance?.OnLauncherBlast(run, at, data.splashRadius,
                    WeaponUpgradeMath.EffectiveDamage(data, _starLevel) * (1f + _skinBonus) * run.DamageMultiplier);
            int found = ZombieWar.Skills.TargetQuery.GatherEnemies(at, data.splashRadius);
            for (int i = 0; i < found; i++)
            {
                var enemy = ZombieWar.Skills.TargetQuery.CandidateEnemy(i);
                if (enemy != null && enemy != direct) { enemy.TakeDamage(damage); ZombieWar.Skills.DamageLedger.Record(ZombieWar.Skills.DamageLedger.Gun, damage); }
            }
            if (data.splashFx != null)
                FxPool.Play(data.splashFx, at + Vector3.up * 0.1f, Quaternion.identity, Mathf.Min(data.splashRadius / 2f, 1.3f));
            if (!string.IsNullOrEmpty(data.splashSfxKey)) Bill.Audio?.Play(data.splashSfxKey, at, 0.8f);
            ShakeCamera(0.2f);
        }

        /// One resolve per hit: the enemy (via the collider map) or, for props, any IDamageable.
        private static IDamageable Damageable(Collider c, out ZombieBase enemy)
        {
            enemy = ZombieBase.FromCollider(c);
            if (enemy != null) return enemy;
            return c != null ? c.GetComponentInParent<IDamageable>() : null;
        }

        // Áp damage (range falloff + dmgMult) + impact FX + knockback cho 1 hit. The enemy is resolved
        // once by the caller and passed down (it used to be looked up again at every step).
        private void ApplyHit(WeaponData data, IDamageable dmg, ZombieBase enemy, RaycastHit hit, Vector3 origin, float dmgMult)
        {
            if (dmg != null)
            {
                float distance = Vector3.Distance(origin, hit.point);
                float dist01 = data.range > 0f ? distance / data.range : 0f;
                float damage = WeaponUpgradeMath.EffectiveDamage(data, _starLevel)
                               * dmgMult * data.RangeFalloff(dist01) * (1f + _skinBonus);

                // Every damage-shaping card, Damage Up included, resolves here.
                var skills = ZombieWar.Skills.SkillRuntime.Active;
                if (skills != null)
                {
                    // Real values, not placeholders: a constant here would silently disable
                    // Execution Round, Point Blank, Longshot and Focus Fire.
                    var targetHealth = enemy != null ? enemy.Life : hit.collider.GetComponentInParent<Health>();
                    int targetId = targetHealth != null
                        ? targetHealth.transform.GetInstanceID()
                        : hit.collider.transform.GetInstanceID();
                    float healthFraction = targetHealth != null && targetHealth.Max > 0f
                        ? targetHealth.Current / targetHealth.Max
                        : 1f;

                    // Hunter's Mark is consumed by this hit, so read it before the damage does.
                    bool empowered = ZombieWar.Skills.StatusCarrier.Has(targetId, ZombieWar.Skills.StatusKind.Marked, Time.time);
                    damage = skills.ModifyHitDamage(damage, targetId, distance, healthFraction, Time.time);
                    skills.ApplyHitStatuses(targetId, Time.time);

                    // A3 Critical Rounds: double damage and a gold number.
                    if (skills.RollCrit())
                    {
                        damage *= ZombieWar.Skills.SkillRuntime.CritMultiplier;
                        _hitCrit = true;
                        if (enemy != null) enemy.MarkNextHitCrit();
                    }

                    // M7.2c legibility — a status the player cannot see is a status they will call a
                    // bug. Marks are WORLD-SPACE (owner owns every UI prefab, so nothing goes in the HUD).
                    var fx = ZombieWar.Skills.SkillFxDirector.Instance;
                    if (fx != null && targetHealth != null)
                    {
                        var t = targetHealth.transform;
                        float now = Time.time;
                        if (empowered)
                            fx.MarkEnemy(t, new Color(1f, 0.85f, 0.2f, 1f), 0.5f);      // Hunter's Mark paid out: gold
                        else if (skills.IsExecutionTarget(healthFraction))
                            fx.MarkEnemy(t, new Color(1f, 0.15f, 0.15f, 1f), 0.5f);     // Execution: red lock-on
                        else if (ZombieWar.Skills.StatusCarrier.Has(targetId, ZombieWar.Skills.StatusKind.Exposed, now))
                            fx.MarkEnemy(t, new Color(1f, 0.45f, 0.15f, 0.9f), 0.6f);   // Breach: orange
                        else if (ZombieWar.Skills.StatusCarrier.Has(targetId, ZombieWar.Skills.StatusKind.Slow, now))
                            fx.MarkEnemy(t, new Color(0.4f, 0.8f, 1f, 0.9f), 0.6f);     // Concussion: ice blue
                        else if (ZombieWar.Skills.StatusCarrier.Has(targetId, ZombieWar.Skills.StatusKind.Marked, now))
                            fx.MarkEnemy(t, new Color(1f, 0.9f, 0.2f, 0.9f), 0.6f);     // Hunter's Mark: gold
                        else if (skills.FocusFireActive)
                        {
                            // Focus Fire: the lock-on grows with every stacked hit on the same enemy.
                            float stacks = ZombieWar.Skills.StatusCarrier.Get(targetId, ZombieWar.Skills.StatusKind.HitCount, now);
                            if (stacks >= 2f)
                                fx.MarkEnemy(t, new Color(1f, 0.55f, 0.2f, 0.95f), 0.45f, 2f, 0.22f + 0.035f * Mathf.Min(stacks, 8f));
                        }
                    }
                }

                dmg.TakeDamage(damage);
                ZombieWar.Skills.DamageLedger.Record(ZombieWar.Skills.DamageLedger.Gun, damage);

                // A3 gun modifiers that act after the bullet lands (ricochet, burst, acid).
                var run = ZombieWar.Skills.SkillRuntime.Active;
                if (run != null && enemy != null)
                    ZombieWar.Skills.SkillArsenal.Instance?.OnGunHit(run, enemy, hit.point, damage, _hitCrit);
                _hitCrit = false;
            }
            // The hit answers in the material of what it hit (blood, bone dust, sap, dirt, splinters…);
            // the weapon's own impact effect is the fallback when no material entry covers it.
            if (!SurfaceImpact.Play(hit, enemy) && data.impactPrefab != null)
                FxPool.Play(data.impactPrefab, hit.point, Quaternion.LookRotation(hit.normal));
            ApplyKnockback(data, enemy);
        }

        // Weapon-authored physical response. Routed through the enemy's own push API rather than a
        // Rigidbody impulse. The original reason was that a NavMeshAgent overwrote any Rigidbody
        // motion the same frame; since M4 there is no agent, but the routing stays because the enemy
        // still owns its displacement - one owner means the shove cannot fight the steering motor.
        private void ApplyKnockback(WeaponData data, ZombieBase enemy)
        {
            if (data.knockback <= 0f || enemy == null) return;
            enemy.ApplyPhysicalPush(data.knockback);
        }

        // The plan produced by the skill runtime for the shot currently being resolved. Gameplay, not a
        // probe: it must compile in every build, not only editor/development ones.
        private ZombieWar.Skills.SkillRuntime.ShotPlan _shotPlan;
        private static readonly int[] ConeBuffer = new int[ZombieWar.Skills.TargetQuery.MaxConsidered];

        /// <summary>
        /// Shockwave Belt (LMG). Every Nth shot sweeps a cone in front of the player. Uses the shared
        /// P3 cone query and the same one-gather-per-effect rule as the autonomous powers.
        /// </summary>
        private void FireShockwave(Vector3 aimDirection)
        {
            var skills = ZombieWar.Skills.SkillRuntime.Active;
            if (skills == null) return;

            Vector3 origin = transform.position;
            // M8: the wave is drawn whether or not it hits, across exactly the cone it checks.
            ZombieWar.Skills.SkillArsenal.Instance?.Cone(origin, aimDirection, 12f);
            int found = ZombieWar.Skills.TargetQuery.GatherEnemies(origin, 12f);
            if (found == 0) return;

            int hits = ZombieWar.Skills.TargetQuery.Cone(
                found, origin, aimDirection, _shotPlan.shockwaveAngle * 0.5f, ConeBuffer);

            for (int i = 0; i < hits; i++)
            {
                var enemy = ZombieWar.Skills.TargetQuery.CandidateEnemy(ConeBuffer[i]);
                if (enemy == null) continue;
                float wave = skills.PowerDamage(18f, ZombieWar.Skills.SkillCatalogDefs.LmgShockwave);
                enemy.TakeDamage(wave);
                ZombieWar.Skills.DamageLedger.Record(ZombieWar.Skills.SkillCatalogDefs.LmgShockwave, wave);
                enemy.ApplyPhysicalPush(1.5f);               // the "shockwave" part
            }
        }

        // PiercingLine — RaycastAll dọc 1 đường: damage TẤT CẢ zombie, dừng khi gặp tường (vật
        // không có IDamageable). pierceCount = -1 xuyên vô hạn (railgun). Docs/Reference/Design/WEAPON_DESIGN.md §3,§7.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Development-only ballistics probe, invoked in the SAME frame as the shot with the
        /// exact values the ray used. Exists so miss-rate can be measured from inside the fire path
        /// instead of reconstructed a frame later. Nothing in the game subscribes to it.</summary>
        public static System.Action<WeaponData, Vector3, Vector3, Vector3, int, bool> ShotProbe;

        /// <summary>One record per ACTUAL physics ray, emitted with the exact origin/direction handed
        /// to Physics.Raycast - post-spread, per pellet. M5.1.1's evidence reported the pre-spread
        /// base vector as "the ray", which cannot describe a 1.5°–14° spread weapon; this exists so
        /// that mistake cannot be repeated (M5.1.2 CP2).</summary>
        public struct ShotRay
        {
            public WeaponData Data;
            public int ShotId, RayIndex, RayCount;
            public Vector3 Origin, MuzzlePosition, MuzzleForward, AimDirection, TargetDirection, RayDirection, HitPoint;
            public bool TargetWasValid, HitSelectedTarget, Blocked;
            public Transform SelectedTarget, HitTransform;
            public int PierceHits;
        }
        public static System.Action<ShotRay> RayProbe;

        private static int _probeShotId;

        private int _probeRayIndex, _probeRayCount;
        private Vector3 _probeMuzzlePos, _probeMuzzleFwd, _probeAim, _probeTargetDir;
        private bool _probeTargetValid;
        private Transform _probeTarget;

        private void EmitRay(WeaponData data, Vector3 origin, Vector3 dir, Vector3 hitPoint,
                             Transform hitTransform, bool blocked, int pierceHits)
        {
            RayProbe?.Invoke(new ShotRay
            {
                Data = data,
                ShotId = _probeShotId,
                RayIndex = _probeRayIndex,
                RayCount = _probeRayCount,
                Origin = origin,
                MuzzlePosition = _probeMuzzlePos,
                MuzzleForward = _probeMuzzleFwd,
                AimDirection = _probeAim,
                TargetDirection = _probeTargetDir,
                RayDirection = dir,
                HitPoint = hitPoint,
                TargetWasValid = _probeTargetValid,
                SelectedTarget = _probeTarget,
                HitTransform = hitTransform,
                HitSelectedTarget = hitTransform != null && _probeTarget != null
                    && (hitTransform == _probeTarget || hitTransform.IsChildOf(_probeTarget)),
                Blocked = blocked,
                PierceHits = pierceHits,
            });
        }
#endif

        private static readonly RaycastHit[] _pierceBuf = new RaycastHit[64];

        // A pierce line can cross several colliders belonging to ONE enemy (body + child hitboxes).
        // Without this set each of those colliders resolved to the same IDamageable and took a full
        // hit, so a single enemy absorbed multiple pierce "slots" and multiple damage applications.
        // Reused per shot; cleared at the start of every line so it can never leak across shots.
        private readonly HashSet<IDamageable> _pierceDamaged = new HashSet<IDamageable>();
        private int _lastPierceHits;
        private bool _lastPierceBlocked;

        private void FireRayPiercing(WeaponData data, Vector3 rayOrigin, Vector3 muzzlePosition,
                                     float rayRange, Vector3 direction)
        {
            _lastPierceHits = 0; _lastPierceBlocked = false;
            int count = Physics.RaycastNonAlloc(rayOrigin, direction, _pierceBuf, rayRange, hitMask);
            Vector3 endPoint = muzzlePosition + direction * data.range;
            _pierceDamaged.Clear();

            if (count > 0)
            {
                // RaycastNonAlloc không sort => sort theo cự ly để falloff xuyên áp đúng thứ tự.
                Array.Sort(_pierceBuf, 0, count, RaycastDistanceComparer.Instance);

                // Breach Round / Piercing Rounds add pierce for this shot only. -1 (infinite) stays
                // infinite. A gun that does not pierce on its own starts from 0 and keeps the card's
                // damage per enemy passed; a piercing gun keeps its own falloff.
                bool nativePierce = data.fireMode == FireMode.PiercingLine;
                int basePierce = nativePierce ? data.pierceCount : 0;
                int effectivePierce = basePierce < 0
                    ? basePierce
                    : basePierce + Mathf.Max(0, _shotPlan.bonusPierce);
                int maxTargets = effectivePierce < 0 ? int.MaxValue : effectivePierce + 1;
                float falloff = nativePierce || _shotPlan.pierceFalloff <= 0f ? data.pierceDamageFalloff : _shotPlan.pierceFalloff;
                float dmgMult = 1f;
                int hitTargets = 0;

                for (int i = 0; i < count; i++)
                {
                    RaycastHit hit = _pierceBuf[i];
                    var dmg = Damageable(hit.collider, out var hitEnemy);
                    if (dmg == null)
                    {
                        // Tường/vật cản chặn đạn => tracer dừng tại đây.
                        endPoint = hit.point;
                        _lastPierceBlocked = true;
                        break;
                    }

                    // Second collider on an enemy already hit by this line: the shot passes through
                    // without spending a pierce slot or re-damaging it.
                    if (!_pierceDamaged.Add(dmg)) continue;

                    ApplyHit(data, dmg, hitEnemy, hit, rayOrigin, dmgMult);
                    dmgMult *= falloff;
                    hitTargets++;
                    _lastPierceHits = hitTargets;
                    if (hitTargets >= maxTargets)
                    {
                        endPoint = hit.point;
                        break;
                    }
                }
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EmitRay(data, rayOrigin, direction, endPoint, null, _lastPierceBlocked, _lastPierceHits);
#endif

            SpawnTracer(data, muzzlePosition, endPoint);
            SpawnSmokeTrail(data, muzzlePosition, endPoint);
        }

        // So sánh RaycastHit theo cự ly cho Array.Sort (generic => không box struct).
        private sealed class RaycastDistanceComparer : IComparer<RaycastHit>
        {
            public static readonly RaycastDistanceComparer Instance = new();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }

        // Random direction inside a cone of full angle coneAngleDeg around forward (shotgun spread).
        private static Vector3 ScatterDirection(Vector3 forward, float coneAngleDeg)
        {
            if (coneAngleDeg <= 0f) return forward;
            float half = coneAngleDeg * 0.5f * Mathf.Deg2Rad;
            float z = UnityEngine.Random.Range(Mathf.Cos(half), 1f);
            float t = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float r = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
            Vector3 local = new Vector3(r * Mathf.Cos(t), r * Mathf.Sin(t), z);
            return Quaternion.LookRotation(forward) * local;
        }
    }
}
