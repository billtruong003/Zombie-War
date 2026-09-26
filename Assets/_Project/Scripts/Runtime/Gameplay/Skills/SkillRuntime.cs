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

        readonly Dictionary<string, int> _ranks = new(40);

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

        // M8 burst powers. Orbit Blades, Drone Buddy and Fire Trail are continuous: SkillArsenal reads
        // their magnitudes every frame instead of polling a proc.
        readonly AutonomousPower _frost = new(SkillCatalogDefs.AutoFrostNova, AutonomousPower.TriggerKind.Interval, 5f);
        readonly AutonomousPower _boomerang = new(SkillCatalogDefs.AutoBoomerang, AutonomousPower.TriggerKind.Interval, 2.5f);
        readonly AutonomousPower _airstrike = new(SkillCatalogDefs.AutoAirstrike, AutonomousPower.TriggerKind.Interval, 8f);
        readonly DistanceAccumulator _trailDistance = new();
        int _trailDropsPending;
        uint _reaperRng = 0x9E3779B9u;

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

        /// <summary>Takes a card, or ranks it up. False when it is already at max rank, or when it is
        /// an evolution whose requirements are not met.</summary>
        public bool Take(string skillId)
        {
            var def = SkillCatalogDefs.ById(skillId);
            if (def == null) return false;
            if (def.IsEvolution && !CanEvolve(def)) return false;
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
            if (def.layer == SkillLayer.Autonomous || def.IsEvolution) SyncAutonomousCooldowns();
        }

        // ══════════════════════════════════════════════════════════ EVOLUTIONS

        /// <summary>An evolution is offered once its power is at max rank and its partner is owned.</summary>
        public bool CanEvolve(SkillDef evo) =>
            evo != null && evo.IsEvolution && !Has(evo.id) && IsMaxRank(evo.evolvesFrom) && Has(evo.partner);

        /// <summary>True once the power's evolution has been taken.</summary>
        public bool IsEvolved(string powerId)
        {
            var evo = SkillCatalogDefs.EvolutionOf(powerId);
            return evo != null && Has(evo.id);
        }

        // ══════════════════════════════════════════════════════════ POWER MAGNITUDES
        //
        // One source of truth for the driver, the arsenal AND the card text, so a description can
        // never promise a number the game does not deliver.

        /// <summary>
        /// Damage of a power hit. Grows with the power's rank and Damage Up, and keeps pace with the
        /// enemies' own late-run stat growth so no power falls off in a long run. Evolutions hit
        /// harder on top.
        /// </summary>
        public float PowerDamage(float baseDamage, string powerId)
        {
            int rank = Mathf.Max(1, RankOf(powerId));
            float evolved = IsEvolved(powerId) ? 1.5f : 1f;
            return baseDamage * (1f + 0.3f * (rank - 1)) * DamageMultiplier
                   * Threat.ThreatDirector.EnemyStatMultiplier * evolved;
        }

        public int OrbitBladeCount => !Has(SkillCatalogDefs.AutoOrbit) ? 0
            : IsEvolved(SkillCatalogDefs.AutoOrbit) ? 6 : Mathf.RoundToInt(Value(SkillCatalogDefs.AutoOrbit));
        public float OrbitRadius => IsEvolved(SkillCatalogDefs.AutoOrbit) ? 3.2f : 2.3f;
        public float OrbitDegreesPerSecond => IsEvolved(SkillCatalogDefs.AutoOrbit) ? 300f : 200f;

        public int DroneCount => !Has(SkillCatalogDefs.AutoDrone) ? 0 : IsEvolved(SkillCatalogDefs.AutoDrone) ? 3 : 1;
        public float DroneShotsPerSecond => Value(SkillCatalogDefs.AutoDrone);

        public float FrostRadius => Value(SkillCatalogDefs.AutoFrostNova) + (IsEvolved(SkillCatalogDefs.AutoFrostNova) ? 1f : 0f);
        public float FrostSlow => 0.35f + 0.1f * (RankOf(SkillCatalogDefs.AutoFrostNova) - 1);
        /// <summary>Absolute Zero freezes instead of slowing.</summary>
        public bool FrostFreezes => IsEvolved(SkillCatalogDefs.AutoFrostNova);

        public float FireTrailDps => Value(SkillCatalogDefs.AutoFireTrail);
        public const float FireTrailSpacing = 0.7f;   // M8: closer drops read as one strip of fire

        public int BoomerangCount => Mathf.RoundToInt(Value(SkillCatalogDefs.AutoBoomerang));
        public int AirstrikeBlasts => Mathf.RoundToInt(Value(SkillCatalogDefs.AutoAirstrike));
        public int ChainTargets => IsEvolved(SkillCatalogDefs.AutoChainLightning) ? TargetQuery.MaxChain
            : Mathf.Min(TargetQuery.MaxChain, Mathf.RoundToInt(Value(SkillCatalogDefs.AutoChainLightning)));

        /// <summary>Cooldown in seconds of a timed power at a rank — used by the card text too.</summary>
        public static float CooldownAt(string powerId, int rank, bool evolved)
        {
            rank = Mathf.Max(1, rank);
            return powerId switch
            {
                SkillCatalogDefs.AutoChainLightning => evolved ? 2f : 6f - (rank - 1),
                SkillCatalogDefs.AutoOrdnance => 7f - (rank - 1),
                SkillCatalogDefs.AutoEmergency => 30f - 5f * (rank - 1),
                SkillCatalogDefs.AutoFrostNova => evolved ? 4f : 5f,
                SkillCatalogDefs.AutoBoomerang => 2.5f,
                SkillCatalogDefs.AutoAirstrike => 8f,
                _ => 0f,
            };
        }

        /// <summary>
        /// Reaper: a kill has a quarter chance to release a small soul burst where the enemy fell.
        /// Deterministic per runtime so tests can count it.
        /// </summary>
        public bool RollReaper()
        {
            if (!IsEvolved(SkillCatalogDefs.AutoSoulBurst)) return false;
            _reaperRng ^= _reaperRng << 13; _reaperRng ^= _reaperRng >> 17; _reaperRng ^= _reaperRng << 5;
            return (_reaperRng % 100u) < 25u;
        }

        /// <summary>Fire patches owed by distance travelled. The arsenal drains this each frame.</summary>
        public int ConsumeFireTrailDrops()
        {
            int n = _trailDropsPending;
            _trailDropsPending = 0;
            return n;
        }

        /// <summary>Health granted by rank-ups that the player component has not yet consumed.</summary>
        public float PendingMaxHealthBonus { get; private set; }
        public float ConsumeMaxHealthBonus() { float v = PendingMaxHealthBonus; PendingMaxHealthBonus = 0f; return v; }

        float Value(string id) => SkillCatalogDefs.ById(id)?.ValueAt(RankOf(id)) ?? 0f;

        void SyncAutonomousCooldowns()
        {
            Sync(_chain, SkillCatalogDefs.AutoChainLightning);
            Sync(_ordnance, SkillCatalogDefs.AutoOrdnance);
            Sync(_emergency, SkillCatalogDefs.AutoEmergency);
            Sync(_frost, SkillCatalogDefs.AutoFrostNova);
            Sync(_boomerang, SkillCatalogDefs.AutoBoomerang);
            Sync(_airstrike, SkillCatalogDefs.AutoAirstrike);
        }

        void Sync(AutonomousPower power, string id)
        {
            if (Has(id)) power.Cooldown = CooldownAt(id, RankOf(id), IsEvolved(id));
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
            else if (plan.bonusPierce > 0)
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
                _staticCharge.AddCharge(1f, 6f - RankOf(SkillCatalogDefs.SmgStatic)))
                _staticFired = true;
        }

        public void OnKill()
        {
            _soulBurst.NotifyKill();
        }

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
            Has(SkillCatalogDefs.UniExecution) &&
            targetHealthFraction <= 0.20f + 0.05f * (RankOf(SkillCatalogDefs.UniExecution) - 1);

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

        /// <summary>
        /// Every autonomous power resolves through P2's framework, which owns the global ≤2 procs/s
        /// ceiling. No card may proc outside it, however many are taken.
        /// </summary>
        public IReadOnlyList<PowerProc> PollPowers(float now, float playerHealthFraction)
        {
            ProcBuffer.Clear();

            if (Has(SkillCatalogDefs.AutoChainLightning) && _chain.TryProc(now))
                ProcBuffer.Add(new PowerProc { skillId = SkillCatalogDefs.AutoChainLightning,
                    targets = ChainTargets, radius = 8f });

            if (Has(SkillCatalogDefs.AutoOrdnance) && _ordnance.TryProc(now))
                ProcBuffer.Add(new PowerProc { skillId = SkillCatalogDefs.AutoOrdnance,
                    targets = 1, radius = Value(SkillCatalogDefs.AutoOrdnance) });

            if (Has(SkillCatalogDefs.AutoSoulBurst) && _soulBurst.TryProc(now))
                ProcBuffer.Add(new PowerProc { skillId = SkillCatalogDefs.AutoSoulBurst,
                    targets = 0, radius = Value(SkillCatalogDefs.AutoSoulBurst) });

            if (Has(SkillCatalogDefs.AutoEmergency) && _emergency.TryProc(now, playerHealthFraction))
                ProcBuffer.Add(new PowerProc { skillId = SkillCatalogDefs.AutoEmergency,
                    targets = 0, radius = Value(SkillCatalogDefs.AutoEmergency) });

            if (Has(SkillCatalogDefs.AutoFrostNova) && _frost.TryProc(now))
                ProcBuffer.Add(new PowerProc { skillId = SkillCatalogDefs.AutoFrostNova,
                    targets = 0, radius = FrostRadius });

            if (Has(SkillCatalogDefs.AutoBoomerang) && _boomerang.TryProc(now))
                ProcBuffer.Add(new PowerProc { skillId = SkillCatalogDefs.AutoBoomerang,
                    targets = BoomerangCount, radius = 9f });

            if (Has(SkillCatalogDefs.AutoAirstrike) && _airstrike.TryProc(now))
                ProcBuffer.Add(new PowerProc { skillId = SkillCatalogDefs.AutoAirstrike,
                    targets = AirstrikeBlasts, radius = 2.6f });

            // SMG Static Build-up is charge-driven rather than timer-driven, but it shares the chain
            // selection primitive with Chain Lightning.
            if (Has(SkillCatalogDefs.SmgStatic) && EquippedFamily == WeaponClass.SMG && _staticFired)
            {
                _staticFired = false;
                ProcBuffer.Add(new PowerProc { skillId = SkillCatalogDefs.SmgStatic,
                    targets = Mathf.RoundToInt(Value(SkillCatalogDefs.SmgStatic)), radius = 6f });
            }
            return ProcBuffer;
        }

        bool _staticFired;

        /// <summary>A targeted power (chain, ordnance) proc'd with nothing in reach. Measured: a chain
        /// firing at a crowd 8-12 m away drew no bolt and still waited its full cooldown.</summary>
        public void Refund(string skillId)
        {
            switch (skillId)
            {
                case SkillCatalogDefs.AutoChainLightning: _chain.Refund(Time.time); break;
                case SkillCatalogDefs.AutoOrdnance: _ordnance.Refund(Time.time); break;
            }
        }

        public float ReadinessOf(string skillId) => skillId switch
        {
            SkillCatalogDefs.AutoChainLightning => _chain.Readiness(Time.time),
            SkillCatalogDefs.AutoOrdnance => _ordnance.Readiness(Time.time),
            SkillCatalogDefs.AutoSoulBurst => _soulBurst.Readiness(Time.time),
            SkillCatalogDefs.AutoEmergency => _emergency.Readiness(Time.time),
            SkillCatalogDefs.AutoFrostNova => _frost.Readiness(Time.time),
            SkillCatalogDefs.AutoBoomerang => _boomerang.Readiness(Time.time),
            SkillCatalogDefs.AutoAirstrike => _airstrike.Readiness(Time.time),
            _ => 1f,
        };

        public void Reset()
        {
            _ranks.Clear();
            _quickstepDistance.Reset(); _kineticDistance.Reset(); _trailDistance.Reset();
            _trailDropsPending = 0;
            _bulletHose.Reset(); _heavyPressure.Reset(); _staticCharge.Reset(); _runGunMoving.Reset();
            _quickstepArmed = _kineticCharged = _staticFired = _shotWasQuickstep = false;
            _shotsSinceBreach = _shotsSinceShockwave = 0;
            PendingMaxHealthBonus = 0f;
            AutonomousPower.ResetGlobalBudget();
        }
    }
}
