using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.Skills
{
    /// <summary>
    /// M7.2 — the per-run build. Holds which of the 23 cards are taken and at what rank, and turns
    /// them into behaviour by driving the nine primitives.
    ///
    /// The design rule: <b>a card owns no logic of its own.</b> Every effect below is a primitive
    /// call plus a magnitude read from <see cref="SkillDef.ValueAt"/>. If a card ever needs its own
    /// targeting, ramping, status or FX code, the primitive is wrong and gets fixed instead.
    ///
    /// Not a MonoBehaviour: the run owns it, tests construct it directly, and nothing about it needs a
    /// scene. <see cref="Active"/> lets the existing weapon/movement code consult it additively
    /// without any of those systems taking a hard dependency on skills.
    /// </summary>
    public class SkillRuntime
    {
        public static SkillRuntime Active { get; set; }

        readonly Dictionary<string, int> _ranks = new(23);

        // ── primitive instances, one per card that needs one ───────────────────────────
        readonly DistanceAccumulator _quickstepDistance = new();
        readonly DistanceAccumulator _kineticDistance = new();
        readonly RampAccumulator _bulletHose = new(1.0f, 2.0f);
        readonly RampAccumulator _heavyPressure = new(0.8f, 1.6f);
        readonly RampAccumulator _staticCharge = new(0f, 0f);
        readonly RampAccumulator _runGunMoving = new(4f, 4f);

        readonly AutonomousPower _chain = new(SkillCatalogDefs.AutoChainLightning, AutonomousPower.TriggerKind.Interval, 6f);
        readonly AutonomousPower _ordnance = new(SkillCatalogDefs.AutoOrdnance, AutonomousPower.TriggerKind.Interval, 7f);
        readonly AutonomousPower _soulBurst = new(SkillCatalogDefs.AutoSoulBurst, AutonomousPower.TriggerKind.KillCount, 0.5f, killsRequired: 12);
        readonly AutonomousPower _emergency = new(SkillCatalogDefs.AutoEmergency, AutonomousPower.TriggerKind.HealthThreshold, 30f, healthFraction: 0.3f);

        // counters that are themselves primitives (per-weapon shot counters)
        int _shotsSinceBreach, _shotsSinceShockwave;
        bool _quickstepArmed, _kineticCharged;

        public WeaponClass EquippedFamily { get; set; } = WeaponClass.Sidearm;

        // ── build state ────────────────────────────────────────────────────────────────
        public int RankOf(string skillId) => _ranks.TryGetValue(skillId, out var r) ? r : 0;
        public bool Has(string skillId) => RankOf(skillId) > 0;
        public IReadOnlyDictionary<string, int> Ranks => _ranks;

        public bool IsMaxRank(string skillId)
        {
            var def = SkillCatalogDefs.ById(skillId);
            return def != null && RankOf(skillId) >= def.maxRank;
        }

        /// <summary>Takes a card, or ranks it up. False when it is already at max rank.</summary>
        public bool Take(string skillId)
        {
            var def = SkillCatalogDefs.ById(skillId);
            if (def == null) return false;
            int current = RankOf(skillId);
            if (current >= def.maxRank) return false;
            _ranks[skillId] = current + 1;
            OnTaken(def, current + 1);
            return true;
        }

        void OnTaken(SkillDef def, int rank)
        {
            // Max Health Up is the one card that must act at pick time rather than continuously.
            if (def.id == SkillCatalogDefs.StatMaxHealth) PendingMaxHealthBonus += def.perRank == 0f ? def.baseValue : (rank == 1 ? def.baseValue : def.perRank);
            if (def.layer == SkillLayer.Autonomous) SyncAutonomousCooldowns();
        }

        /// <summary>Health granted by rank-ups that the player component has not yet consumed.</summary>
        public float PendingMaxHealthBonus { get; private set; }
        public float ConsumeMaxHealthBonus() { float v = PendingMaxHealthBonus; PendingMaxHealthBonus = 0f; return v; }

        float Value(string id) => SkillCatalogDefs.ById(id)?.ValueAt(RankOf(id)) ?? 0f;

        void SyncAutonomousCooldowns()
        {
            // Rank shortens the interval; the magnitude table holds the rank-1 interval.
            if (Has(SkillCatalogDefs.AutoChainLightning)) _chain.Cooldown = 6f - 1f * (RankOf(SkillCatalogDefs.AutoChainLightning) - 1);
            if (Has(SkillCatalogDefs.AutoOrdnance)) _ordnance.Cooldown = 7f - 1f * (RankOf(SkillCatalogDefs.AutoOrdnance) - 1);
            if (Has(SkillCatalogDefs.AutoEmergency)) _emergency.Cooldown = 30f - 5f * (RankOf(SkillCatalogDefs.AutoEmergency) - 1);
        }

        // ══════════════════════════════════════════════════════════ STAT (P8 soft caps)

        /// <summary>Damage Up. Plain multiplier — the only stat with no cap, by design.</summary>
        public float DamageMultiplier => 1f + Value(SkillCatalogDefs.StatDamage);

        /// <summary>Fire Rate Up + Run &amp; Gun + Bullet Hose, then P8's 2.2/2.5 soft cap.</summary>
        public float FireRateMultiplier
        {
            get
            {
                float raw = 1f + Value(SkillCatalogDefs.StatFireRate);
                if (Has(SkillCatalogDefs.SidearmRunGun) && EquippedFamily == WeaponClass.Sidearm)
                    raw += Value(SkillCatalogDefs.SidearmRunGun) * _runGunMoving.Value;
                if (Has(SkillCatalogDefs.SmgBulletHose) && EquippedFamily == WeaponClass.SMG)
                    raw += Value(SkillCatalogDefs.SmgBulletHose) * _bulletHose.Value;
                return SoftCap.FireRate.Apply(raw);
            }
        }

        /// <summary>Move Speed Up, minus Heavy Pressure's self-slow. Soft-capped by P8.</summary>
        public float MoveSpeedMultiplier
        {
            get
            {
                float raw = 1f + Value(SkillCatalogDefs.StatMoveSpeed);
                if (Has(SkillCatalogDefs.LmgHeavyPressure) && EquippedFamily == WeaponClass.LMG)
                    raw *= Mathf.Lerp(1f, 0.75f, _heavyPressure.Value);   // the cost of the ramp
                return SoftCap.MoveSpeed.Apply(raw);
            }
        }

        public float CoinMultiplier => 1f + Value(SkillCatalogDefs.StatCoinGain);

        // ══════════════════════════════════════════════════════════ TICK

        public void Tick(float dt, Vector3 playerPosition, bool isMoving, bool isFiring, float playerHealthFraction)
        {
            // P4 — distance accumulators
            _quickstepDistance.Sample(playerPosition);
            _kineticDistance.Sample(playerPosition);

            if (Has(SkillCatalogDefs.SidearmQuickstep) && !_quickstepArmed &&
                _quickstepDistance.TryConsume(Value(SkillCatalogDefs.SidearmQuickstep)))
                _quickstepArmed = true;

            if (Has(SkillCatalogDefs.UniKinetic) && !_kineticCharged &&
                _kineticDistance.TryConsume(Value(SkillCatalogDefs.UniKinetic)))
                _kineticCharged = true;

            // P5 — ramps
            _runGunMoving.Tick(isMoving, dt);
            _bulletHose.Tick(isFiring, dt);
            _heavyPressure.Tick(isFiring, dt);

            // P2 — health-threshold power re-arms once the player recovers
            _emergency.NotifyHealthFraction(playerHealthFraction);
        }

        // ══════════════════════════════════════════════════════════ SHOT / HIT / KILL

        public struct ShotPlan
        {
            public int bonusPierce;      // Breach Round
            public bool shockwave;       // Shockwave Belt
            public float shockwaveAngle;
        }

        /// <summary>OnShot. Drives the two shot-counter cards.</summary>
        public ShotPlan OnShotFired()
        {
            var plan = default(ShotPlan);

            if (Has(SkillCatalogDefs.ArBreach) && EquippedFamily == WeaponClass.AssaultRifle)
            {
                int every = Mathf.Max(2, 6 - RankOf(SkillCatalogDefs.ArBreach));
                if (++_shotsSinceBreach >= every)
                {
                    _shotsSinceBreach = 0;
                    plan.bonusPierce = Mathf.RoundToInt(Value(SkillCatalogDefs.ArBreach));
                }
            }

            if (Has(SkillCatalogDefs.LmgShockwave) && EquippedFamily == WeaponClass.LMG)
            {
                int every = Mathf.Max(4, 12 - 2 * RankOf(SkillCatalogDefs.LmgShockwave));
                if (++_shotsSinceShockwave >= every)
                {
                    _shotsSinceShockwave = 0;
                    plan.shockwave = true;
                    plan.shockwaveAngle = Value(SkillCatalogDefs.LmgShockwave);
                }
            }
            return plan;
        }

        /// <summary>
        /// OnHit. Every damage-shaping card resolves here through P6's context, P7's curves and P1's
        /// status carrier — no card reaches into the damage path itself.
        /// </summary>
        public float ModifyHitDamage(float damage, int targetId, float distance, float targetHealthFraction, float now)
        {
            // Universal — Execution Round (P6 threshold read)
            if (Has(SkillCatalogDefs.UniExecution))
            {
                float threshold = 0.20f + 0.05f * (RankOf(SkillCatalogDefs.UniExecution) - 1);
                if (targetHealthFraction <= threshold) damage *= 1f + Value(SkillCatalogDefs.UniExecution);
            }

            // Shotgun — Point Blank (P7 curve)
            if (Has(SkillCatalogDefs.ShotgunPointBlank) && EquippedFamily == WeaponClass.Shotgun)
                damage *= DistanceDamageCurve.PointBlank(Value(SkillCatalogDefs.ShotgunPointBlank), 6f).Evaluate(distance);

            // Marksman — Longshot (P7 curve, opposite direction)
            if (Has(SkillCatalogDefs.MarksmanLongshot) && EquippedFamily == WeaponClass.Marksman)
                damage *= DistanceDamageCurve.Longshot(Value(SkillCatalogDefs.MarksmanLongshot) * 5f, 40f).Evaluate(distance);

            // AR — Focus Fire (P1 per-target hit counter)
            if (Has(SkillCatalogDefs.ArFocusFire) && EquippedFamily == WeaponClass.AssaultRifle)
            {
                float hits = StatusCarrier.Accumulate(targetId, StatusKind.HitCount, 1f, 2f, now);
                damage *= 1f + Value(SkillCatalogDefs.ArFocusFire) * Mathf.Min(hits, 8f);
            }

            // AR — Breach Round's Exposed status (P1)
            if (StatusCarrier.Has(targetId, StatusKind.Exposed, now)) damage *= 1.25f;

            // Marksman — Hunter's Mark first-hit empower (P1)
            if (Has(SkillCatalogDefs.MarksmanHunters) && StatusCarrier.Has(targetId, StatusKind.Marked, now))
            {
                damage *= 1f + Value(SkillCatalogDefs.MarksmanHunters);
                StatusCarrier.Clear(targetId);   // the empower is consumed by the first hit
            }

            // LMG — Heavy Pressure ramp (P5)
            if (Has(SkillCatalogDefs.LmgHeavyPressure) && EquippedFamily == WeaponClass.LMG)
                damage *= 1f + Value(SkillCatalogDefs.LmgHeavyPressure) * _heavyPressure.Value;

            // Sidearm — Quickstep Round: distance-charged, consumed by the next automatic shot (P4)
            if (_quickstepArmed && EquippedFamily == WeaponClass.Sidearm)
            {
                _quickstepArmed = false;
                damage *= 2.5f;
            }

            return damage * DamageMultiplier;
        }

        /// <summary>OnHit side effects that are statuses rather than damage (P1).</summary>
        public void ApplyHitStatuses(int targetId, float now)
        {
            if (Has(SkillCatalogDefs.ShotgunConcussion) && EquippedFamily == WeaponClass.Shotgun)
                StatusCarrier.Apply(targetId, StatusKind.Slow, Value(SkillCatalogDefs.ShotgunConcussion), 1.5f, now);

            if (Has(SkillCatalogDefs.ArBreach) && EquippedFamily == WeaponClass.AssaultRifle)
                StatusCarrier.Apply(targetId, StatusKind.Exposed, 1f, 3f, now);

            if (Has(SkillCatalogDefs.SmgStatic) && EquippedFamily == WeaponClass.SMG)
                _staticCharge.AddCharge(1f, 6f - RankOf(SkillCatalogDefs.SmgStatic));
        }

        public void OnKill()
        {
            _soulBurst.NotifyKill();
        }

        public void OnTargetChanged(int newTargetId, float now)
        {
            if (Has(SkillCatalogDefs.MarksmanHunters) && EquippedFamily == WeaponClass.Marksman)
                StatusCarrier.Apply(newTargetId, StatusKind.Marked, 1f, 8f, now);
        }

        /// <summary>OnDamageTaken. Kinetic Shield blocks one hit per charge (P4).</summary>
        public bool TryAbsorbDamage()
        {
            if (!_kineticCharged) return false;
            _kineticCharged = false;
            return true;
        }

        // ══════════════════════════════════════════════════════════ AUTONOMOUS PROCS

        public struct PowerProc
        {
            public string skillId;
            public int targets;
            public float radius;
        }

        static readonly List<PowerProc> ProcBuffer = new(4);

        /// <summary>
        /// Every autonomous power resolves through P2's framework, which owns the global ≤2 procs/s
        /// ceiling. No card may proc outside it, however many are taken.
        /// </summary>
        public IReadOnlyList<PowerProc> PollPowers(float now, float playerHealthFraction)
        {
            ProcBuffer.Clear();

            if (Has(SkillCatalogDefs.AutoChainLightning) && _chain.TryProc(now))
                ProcBuffer.Add(new PowerProc { skillId = SkillCatalogDefs.AutoChainLightning,
                    targets = Mathf.Min(TargetQuery.MaxChain, Mathf.RoundToInt(Value(SkillCatalogDefs.AutoChainLightning))), radius = 8f });

            if (Has(SkillCatalogDefs.AutoOrdnance) && _ordnance.TryProc(now))
                ProcBuffer.Add(new PowerProc { skillId = SkillCatalogDefs.AutoOrdnance,
                    targets = 1, radius = Value(SkillCatalogDefs.AutoOrdnance) });

            if (Has(SkillCatalogDefs.AutoSoulBurst) && _soulBurst.TryProc(now))
                ProcBuffer.Add(new PowerProc { skillId = SkillCatalogDefs.AutoSoulBurst,
                    targets = 0, radius = Value(SkillCatalogDefs.AutoSoulBurst) });

            if (Has(SkillCatalogDefs.AutoEmergency) && _emergency.TryProc(now, playerHealthFraction))
                ProcBuffer.Add(new PowerProc { skillId = SkillCatalogDefs.AutoEmergency,
                    targets = 0, radius = Value(SkillCatalogDefs.AutoEmergency) });

            // SMG Static Build-up is charge-driven rather than timer-driven, but it shares the chain
            // selection primitive with Chain Lightning.
            if (Has(SkillCatalogDefs.SmgStatic) && EquippedFamily == WeaponClass.SMG && _staticCharge.Value <= 0f && _staticFired)
            {
                _staticFired = false;
                ProcBuffer.Add(new PowerProc { skillId = SkillCatalogDefs.SmgStatic,
                    targets = Mathf.RoundToInt(Value(SkillCatalogDefs.SmgStatic)), radius = 6f });
            }
            return ProcBuffer;
        }

        bool _staticFired;

        /// <summary>Called by the SMG hit path when Static Build-up reaches its charge.</summary>
        public void NotifyStaticDischarge() => _staticFired = true;

        public float ReadinessOf(string skillId) => skillId switch
        {
            SkillCatalogDefs.AutoChainLightning => _chain.Readiness(Time.time),
            SkillCatalogDefs.AutoOrdnance => _ordnance.Readiness(Time.time),
            SkillCatalogDefs.AutoSoulBurst => _soulBurst.Readiness(Time.time),
            SkillCatalogDefs.AutoEmergency => _emergency.Readiness(Time.time),
            _ => 1f,
        };

        public void Reset()
        {
            _ranks.Clear();
            _quickstepDistance.Reset(); _kineticDistance.Reset();
            _bulletHose.Reset(); _heavyPressure.Reset(); _staticCharge.Reset(); _runGunMoving.Reset();
            _quickstepArmed = _kineticCharged = _staticFired = false;
            _shotsSinceBreach = _shotsSinceShockwave = 0;
            PendingMaxHealthBonus = 0f;
            AutonomousPower.ResetGlobalBudget();
        }
    }
}
