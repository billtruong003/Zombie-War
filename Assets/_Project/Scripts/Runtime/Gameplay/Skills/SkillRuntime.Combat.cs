using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.Skills
{
    /// SkillRuntime, part: what the build does each frame, each shot, each hit and each kill.
    public partial class SkillRuntime
    {
        public void Tick(float dt, Vector3 playerPosition, bool isMoving, bool isFiring, float playerHealthFraction)
        {
            // P4 — distance accumulators
            _quickstepDistance.Sample(playerPosition);
            _kineticDistance.Sample(playerPosition);
            _trailDistance.Sample(playerPosition);

            // Fire Trail pays out by distance, so standing still leaves no fire: it rewards moving.
            if (Has(SkillCatalogDefs.AutoFireTrail))
                while (_trailDistance.TryConsume(FireTrailSpacing) && _trailDropsPending < 8) _trailDropsPending++;

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
            ProcOf(SkillCatalogDefs.AutoEmergency).timer.NotifyHealthFraction(playerHealthFraction);
        }

        // ══════════════════════════════════════════════════════════ SHOT / HIT / KILL

        public struct ShotPlan
        {
            public int bonusPierce;      // Breach Round + Piercing Rounds
            public bool breach;          // this shot is a Breach Round (its tracer shows it)
            public float pierceFalloff;  // damage kept per enemy passed (Piercing Rounds); 0 = the gun's own
            public bool shockwave;       // Shockwave Belt
            public float shockwaveAngle;
            public int splitBullets;     // Split Shot: extra bullets fanned out with this shot
            public bool doubleTap;       // Double Tap: one free extra bullet
        }

        /// <summary>OnShot. Drives the two shot-counter cards.</summary>
        public ShotPlan OnShotFired()
        {
            var plan = default(ShotPlan);

            if (Has(SkillCatalogDefs.ArBreach) && EquippedFamily == WeaponClass.AssaultRifle)
            {
                int every = Mathf.Max(2, Mathf.RoundToInt(Table(SkillCatalogDefs.ArBreach, "every", 5f)));
                if (++_shotsSinceBreach >= every)
                {
                    _shotsSinceBreach = 0;
                    plan.bonusPierce = Mathf.RoundToInt(Value(SkillCatalogDefs.ArBreach));
                    plan.breach = true;
                }
            }

            // A3 gun modifiers: every gun, every family.
            if (Has(SkillCatalogDefs.UniPierce))
            {
                plan.bonusPierce += Mathf.RoundToInt(Value(SkillCatalogDefs.UniPierce));
                plan.pierceFalloff = Table(SkillCatalogDefs.UniPierce, "dmg", 0.9f);
            }
            if (Has(SkillCatalogDefs.UniSplit))
            {
                int every = Mathf.Max(2, Mathf.RoundToInt(Table(SkillCatalogDefs.UniSplit, "every", 5f)));
                if (++_shotsSinceSplit >= every)
                {
                    _shotsSinceSplit = 0;
                    plan.splitBullets = Mathf.RoundToInt(Value(SkillCatalogDefs.UniSplit));
                }
            }
            if (Has(SkillCatalogDefs.UniDoubleTap) && Roll(Value(SkillCatalogDefs.UniDoubleTap))) plan.doubleTap = true;

            if (Has(SkillCatalogDefs.LmgShockwave) && EquippedFamily == WeaponClass.LMG)
            {
                int every = Mathf.Max(4, Mathf.RoundToInt(Table(SkillCatalogDefs.LmgShockwave, "every", 10f)));
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
                if (targetHealthFraction <= ExecutionThreshold) damage *= 1f + Value(SkillCatalogDefs.UniExecution);
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

            // Absolute Zero — frozen enemies shatter for more (P1)
            if (StatusCarrier.Has(targetId, StatusKind.Frozen, now)) damage *= 1.5f;

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
                _shotWasQuickstep = true;          // M8: this shot's tracer shows it
                damage *= 2.5f;
            }

            return damage * DamageMultiplier;
        }

        // ══════════════════════════════════════════════════════════ M8 SIGNATURE LOOK
        //
        // The weapon signatures were numbers only: nothing on screen said Bullet Hose was ramping or
        // that a Quickstep shot had just fired. The weapon asks here how THIS shot's tracer and muzzle
        // flash should look, so every signature shows on the bullet itself.

        bool _shotWasQuickstep;
        static readonly Color TracerAmber = new(1f, 0.86f, 0.5f, 1f);

        public float RunGunRamp => Has(SkillCatalogDefs.SidearmRunGun) && EquippedFamily == WeaponClass.Sidearm ? _runGunMoving.Value : 0f;
        public float BulletHoseRamp => Has(SkillCatalogDefs.SmgBulletHose) && EquippedFamily == WeaponClass.SMG ? _bulletHose.Value : 0f;
        public float HeavyPressureRamp => Has(SkillCatalogDefs.LmgHeavyPressure) && EquippedFamily == WeaponClass.LMG ? _heavyPressure.Value : 0f;
        public bool FocusFireActive => Has(SkillCatalogDefs.ArFocusFire) && EquippedFamily == WeaponClass.AssaultRifle;

        /// <summary>Muzzle flash size for the next shot: heat ramps grow it, an armed Quickstep doubles up.</summary>
        public float MuzzleFlashScale
        {
            get
            {
                float s = 1f + 0.6f * Mathf.Max(BulletHoseRamp, HeavyPressureRamp);
                if (_quickstepArmed && EquippedFamily == WeaponClass.Sidearm) s = Mathf.Max(s, 1.6f);
                return s;
            }
        }

        /// <summary>
        /// How this shot's tracer should look, or false for the gun's own tracer. Called once per
        /// tracer, after the hit resolved (a Quickstep shot is known by then).
        /// </summary>
        public bool TryTracerLook(ShotPlan plan, float distance, out Color tint, out float thickness)
        {
            tint = TracerAmber; thickness = 1f;
            bool styled = true;
            if (_shotWasQuickstep)
            {
                tint = new Color(1f, 0.82f, 0.25f, 1f); thickness = 2.1f;                     // Quickstep: gold slug
            }
            else if (plan.breach)
            {
                tint = new Color(1f, 0.42f, 0.15f, 1f); thickness = 1.7f;                     // Breach: armour-piercing
            }
            else if (Has(SkillCatalogDefs.MarksmanLongshot) && EquippedFamily == WeaponClass.Marksman && distance > 10f)
            {
                tint = new Color(1f, 0.96f, 0.7f, 1f); thickness = 1.3f + Mathf.Min(0.9f, (distance - 10f) / 20f);   // Longshot
            }
            else if (Has(SkillCatalogDefs.ShotgunPointBlank) && EquippedFamily == WeaponClass.Shotgun && distance < 5f)
            {
                tint = new Color(1f, 0.6f, 0.2f, 1f); thickness = 1.5f;                       // Point Blank
            }
            else if (BulletHoseRamp > 0.15f)
            {
                float r = BulletHoseRamp;                                                     // Bullet Hose: white-hot
                tint = Color.Lerp(TracerAmber, new Color(1f, 1f, 0.95f, 1f), r); thickness = 1f + 0.5f * r;
            }
            else if (HeavyPressureRamp > 0.15f)
            {
                float r = HeavyPressureRamp;                                                  // Heavy Pressure: red-hot
                tint = Color.Lerp(TracerAmber, new Color(1f, 0.32f, 0.1f, 1f), r); thickness = 1f + 0.6f * r;
            }
            else if (RunGunRamp > 0.3f)
            {
                tint = Color.Lerp(TracerAmber, new Color(0.65f, 0.95f, 1f, 1f), RunGunRamp); thickness = 1.1f;   // Run & Gun
            }
            else styled = false;
            _shotWasQuickstep = false;
            return styled;
        }

        /// <summary>OnHit side effects that are statuses rather than damage (P1).</summary>
        public void ApplyHitStatuses(int targetId, float now)
        {
            if (Has(SkillCatalogDefs.ShotgunConcussion) && EquippedFamily == WeaponClass.Shotgun)
                StatusCarrier.Apply(targetId, StatusKind.Slow, Value(SkillCatalogDefs.ShotgunConcussion), 1.5f, now);

            if (Has(SkillCatalogDefs.ArBreach) && EquippedFamily == WeaponClass.AssaultRifle)
                StatusCarrier.Apply(targetId, StatusKind.Exposed, 1f, 3f, now);

            // Static Build-up: every Nth SMG hit discharges a chain. The charge result IS the trigger;
            // it used to be discarded, so the card never fired once.
            if (Has(SkillCatalogDefs.SmgStatic) && EquippedFamily == WeaponClass.SMG &&
                _staticCharge.AddCharge(1f, Table(SkillCatalogDefs.SmgStatic, "every", 5f)))
                _staticFired = true;
        }

        public void OnKill()
        {
            ProcOf(SkillCatalogDefs.AutoSoulBurst).timer.NotifyKill();

            // Blood Siphon: every Nth kill heals 3% through the same queue as the Heal bonus card.
            if (Has(SkillCatalogDefs.UniSiphon) && ++_siphonKills >= Mathf.Max(1, Mathf.RoundToInt(Value(SkillCatalogDefs.UniSiphon))))
            {
                _siphonKills = 0;
                PendingHealFraction += SiphonHealFraction;
                _siphonTriggered = true;
            }
        }

        // ══════════════════════════════════════════════════════════ A3 GUN MODIFIERS

        public const float SiphonHealFraction = 0.03f;
        public const float CritMultiplier = 2f;
        public const int PoisonMaxStacks = 5;
        public const float PoisonSeconds = 3f;
        public const float ExplosiveRadius = 1.2f;
        public const float ExplosiveShare = 0.5f;

        int _shotsSinceSplit, _siphonKills;
        bool _siphonTriggered, _guardianUsed;
        uint _gunRng = 0x2545F491u;

        /// Deterministic per runtime, so tests can count procs.
        bool Roll(float chance)
        {
            if (chance <= 0f) return false;
            _gunRng ^= _gunRng << 13; _gunRng ^= _gunRng >> 17; _gunRng ^= _gunRng << 5;
            return (_gunRng % 10000u) < (uint)(chance * 10000f);
        }

        /// <summary>Critical Rounds: this hit is a crit (x<see cref="CritMultiplier"/>, gold number).</summary>
        public bool RollCrit()
        {
            float chance = (Has(SkillCatalogDefs.UniCrit) ? Value(SkillCatalogDefs.UniCrit) : 0f) + Account.crit;   // + pistol mastery (#21)
            return chance > 0f && Roll(chance);
        }

        /// <summary>Explosive Rounds: this hit bursts.</summary>
        public bool RollExplosive() => Has(SkillCatalogDefs.UniExplosive) && Roll(Value(SkillCatalogDefs.UniExplosive));

        public int RicochetBounces => Has(SkillCatalogDefs.UniRicochet) ? Mathf.RoundToInt(Value(SkillCatalogDefs.UniRicochet)) : 0;
        public float RicochetShare => Table(SkillCatalogDefs.UniRicochet, "dmg", 0.6f);

        /// <summary>Acid Rounds: poison per stack per second, grown by Damage Up like every power.</summary>
        public float AcidDps => !Has(SkillCatalogDefs.UniAcid) ? 0f
            : Value(SkillCatalogDefs.UniAcid) * DamageMultiplier * Threat.ThreatDirector.EnemyStatMultiplier;

        /// <summary>A Blood Siphon heal just triggered (for its effect); reading clears it.</summary>
        public bool ConsumeSiphonTrigger() { bool v = _siphonTriggered; _siphonTriggered = false; return v; }

        /// <summary>Guardian Angel: once per run, a hit that would kill heals instead.</summary>
        public bool TryGuardianAngel(out float healFraction)
        {
            healFraction = 0f;
            if (_guardianUsed || !Has(SkillCatalogDefs.UniGuardian)) return false;
            _guardianUsed = true;
            healFraction = Value(SkillCatalogDefs.UniGuardian);
            return true;
        }

        public bool GuardianReady => Has(SkillCatalogDefs.UniGuardian) && !_guardianUsed;

        // Launcher signatures (Rocket family in hand)
        bool Launcher(string id) => Has(id) && EquippedFamily == WeaponClass.Rocket;
        public int ClusterBomblets => Launcher(SkillCatalogDefs.RocketCluster) ? Mathf.RoundToInt(Value(SkillCatalogDefs.RocketCluster)) : 0;
        public float ClusterShare => Table(SkillCatalogDefs.RocketCluster, "dmg", 0.4f);
        public float NapalmSeconds => Launcher(SkillCatalogDefs.RocketNapalm) ? Value(SkillCatalogDefs.RocketNapalm) : 0f;
        public float NapalmDpsShare => Table(SkillCatalogDefs.RocketNapalm, "dps", 0.3f);

        /// <summary>M8: Hunter's Mark is in play (card held, marksman rifle in hand) — drives its lock-on mark.</summary>
        public bool HuntersMarkActive => Has(SkillCatalogDefs.MarksmanHunters) && EquippedFamily == WeaponClass.Marksman;

        /// <summary>The auto-aim locked a new enemy. Hunter's Mark empowers the first hit on it.</summary>
        public void OnTargetChanged(int newTargetId, float now)
        {
            if (Has(SkillCatalogDefs.MarksmanHunters) && EquippedFamily == WeaponClass.Marksman)
                StatusCarrier.Apply(newTargetId, StatusKind.Marked, 1f, 8f, now);
        }

        /// <summary>True while Kinetic Shield holds a charge — drives the visible bubble.</summary>
        public bool KineticCharged => _kineticCharged;

        /// <summary>M8: true when Execution Round's bonus applies to a target at this health (for its mark).</summary>
        public bool IsExecutionTarget(float targetHealthFraction) =>
            Has(SkillCatalogDefs.UniExecution) && targetHealthFraction <= ExecutionThreshold;

        public float ExecutionThreshold => Table(SkillCatalogDefs.UniExecution, "threshold", 0.2f);

        /// <summary>M8: 0..1 progress toward the next Kinetic Shield charge (1 while charged), for the ground ring.</summary>
        public float KineticChargeFraction => !Has(SkillCatalogDefs.UniKinetic) ? 0f
            : _kineticCharged ? 1f
            : Mathf.Clamp01(_kineticDistance.Distance / Mathf.Max(0.01f, Value(SkillCatalogDefs.UniKinetic)));

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
    }
}
