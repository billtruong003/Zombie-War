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

        /// <summary>
        /// A power that fires in procs (continuous powers — Orbit Blades, Drone Buddy, Fire Trail —
        /// read their magnitudes every frame instead). Its trigger, and how one proc is sized; the
        /// power module that owns the id turns the proc into damage. A new burst power is one line in
        /// <see cref="_procPowers"/>.
        /// </summary>
        sealed class ProcPower
        {
            public readonly AutonomousPower timer;
            public readonly System.Func<SkillRuntime, int> targets;
            public readonly System.Func<SkillRuntime, float> radius;
            /// Cooldown follows the card's "cd" table (kill- and health-triggered powers have none).
            public readonly bool timed;

            public ProcPower(AutonomousPower timer, System.Func<SkillRuntime, int> targets,
                             System.Func<SkillRuntime, float> radius, bool timed = true)
            {
                this.timer = timer; this.targets = targets; this.radius = radius; this.timed = timed;
            }
        }

        static AutonomousPower Every(string id, float cooldown) => new(id, AutonomousPower.TriggerKind.Interval, cooldown);

        readonly ProcPower[] _procPowers =
        {
            new(Every(SkillCatalogDefs.AutoChainLightning, 6f), r => r.ChainTargets, _ => 8f),
            new(Every(SkillCatalogDefs.AutoOrdnance, 7f), _ => 1, r => r.Value(SkillCatalogDefs.AutoOrdnance) * r.AreaMultiplier),
            new(new AutonomousPower(SkillCatalogDefs.AutoSoulBurst, AutonomousPower.TriggerKind.KillCount, 0.5f, killsRequired: 12),
                _ => 0, r => r.Value(SkillCatalogDefs.AutoSoulBurst) * r.AreaMultiplier, timed: false),
            new(new AutonomousPower(SkillCatalogDefs.AutoEmergency, AutonomousPower.TriggerKind.HealthThreshold, 30f, healthFraction: 0.3f),
                _ => 0, r => r.Value(SkillCatalogDefs.AutoEmergency) * r.AreaMultiplier),
            new(Every(SkillCatalogDefs.AutoFrostNova, 5f), _ => 0, r => r.FrostRadius),
            new(Every(SkillCatalogDefs.AutoBoomerang, 2.5f), r => r.BoomerangCount, _ => 9f),
            new(Every(SkillCatalogDefs.AutoAirstrike, 8f), r => r.AirstrikeBlasts, r => 2.6f * r.AreaMultiplier),
        };

        ProcPower ProcOf(string id)
        {
            for (int i = 0; i < _procPowers.Length; i++) if (_procPowers[i].timer.id == id) return _procPowers[i];
            return null;
        }
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

        // ══════════════════════════════════════════════════════════ SLOTS AND UNLOCKS

        /// <summary>
        /// Account level the build may draw from (cards above it are not offered). int.MaxValue =
        /// everything, which is what tests and the sandbox get; the driver sets the real level when a
        /// run starts.
        /// </summary>
        public int UnlockLevel { get; set; } = int.MaxValue;

        public bool IsUnlocked(SkillDef def) => SkillCatalogDefs.IsUnlocked(def, UnlockLevel);

        public int SlotsUsed(SkillSlot slot)
        {
            int n = 0;
            foreach (var kv in _ranks)
            {
                var def = SkillCatalogDefs.ById(kv.Key);
                if (def != null && kv.Value > 0 && def.Slot == slot) n++;
            }
            return n;
        }

        public static int SlotCapacity(SkillSlot slot) => slot switch
        {
            SkillSlot.Skill => SkillCatalogDefs.MaxSkillSlots,
            SkillSlot.Stat => SkillCatalogDefs.MaxStatSlots,
            _ => int.MaxValue,
        };

        /// <summary>True when the card is owned, or its slot group still has room for a new card.</summary>
        public bool HasRoomFor(SkillDef def) =>
            def != null && (Has(def.id) || def.Slot == SkillSlot.None || SlotsUsed(def.Slot) < SlotCapacity(def.Slot));

        /// <summary>Takes a card, or ranks it up. False when it is already at max rank, when it is an
        /// evolution whose requirements are not met, or when it is new and its slot group is full.</summary>
        public bool Take(string skillId)
        {
            var def = SkillCatalogDefs.ById(skillId);
            if (def == null) return false;
            if (def.IsEvolution && !CanEvolve(def)) return false;
            if (!HasRoomFor(def)) return false;
            int current = RankOf(skillId);
            if (current >= def.maxRank) return false;
            _ranks[skillId] = current + 1;
            OnTaken(def, current + 1);
            return true;
        }

        void OnTaken(SkillDef def, int rank)
        {
            // Max Health Up is the one card that must act at pick time rather than continuously.
            if (def.id == SkillCatalogDefs.StatMaxHealth) PendingMaxHealthBonus += def.ValueAt(rank) - def.ValueAt(rank - 1);
            if (def.layer == SkillLayer.Autonomous || def.IsEvolution || def.id == SkillCatalogDefs.StatCooldown) SyncAutonomousCooldowns();
            if (def.IsOverflow) TakeOverflow(def);
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
            float scale = SkillCatalogDefs.ById(powerId)?.At("dmg", rank, 1f) ?? 1f;
            return baseDamage * scale * DamageMultiplier
                   * Threat.ThreatDirector.EnemyStatMultiplier * evolved;
        }

        public int OrbitBladeCount => !Has(SkillCatalogDefs.AutoOrbit) ? 0
            : IsEvolved(SkillCatalogDefs.AutoOrbit) ? 6 : Mathf.RoundToInt(Value(SkillCatalogDefs.AutoOrbit));
        public float OrbitRadius => (IsEvolved(SkillCatalogDefs.AutoOrbit) ? 3.2f : 2.3f) * AreaMultiplier;
        public float OrbitDegreesPerSecond => IsEvolved(SkillCatalogDefs.AutoOrbit) ? 300f : 200f;

        public int DroneCount => !Has(SkillCatalogDefs.AutoDrone) ? 0 : IsEvolved(SkillCatalogDefs.AutoDrone) ? 3 : 1;
        public float DroneShotsPerSecond => Value(SkillCatalogDefs.AutoDrone);

        public float FrostRadius => (Value(SkillCatalogDefs.AutoFrostNova) + (IsEvolved(SkillCatalogDefs.AutoFrostNova) ? 1f : 0f)) * AreaMultiplier;
        public float FrostSlow => Table(SkillCatalogDefs.AutoFrostNova, "slow", 0.35f);
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
            if (evolved)
            {
                // Evolutions set their own rhythm.
                if (powerId == SkillCatalogDefs.AutoChainLightning) return 2f;   // Thunderstorm
                if (powerId == SkillCatalogDefs.AutoFrostNova) return 4f;        // Absolute Zero
            }
            return SkillCatalogDefs.ById(powerId)?.At("cd", rank, 0f) ?? 0f;
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

        // ══════════════════════════════════════════════════════════ OVERFLOW
        //
        // Offered once every owned card is maxed and every slot is full, so a level-up always means
        // something. Effects that touch other systems wait here for the driver, like the max-health
        // bonus does, so the runtime stays free of scene references.

        public const float MightPerStack = 0.03f;
        public int MightStacks { get; private set; }
        public float PendingHealFraction { get; private set; }
        public int PendingCoin { get; private set; }
        public bool PendingMagnet { get; private set; }

        void TakeOverflow(SkillDef def)
        {
            switch (def.id)
            {
                case SkillCatalogDefs.OverHeal: PendingHealFraction += def.ValueAt(1); break;
                case SkillCatalogDefs.OverMagnet: PendingMagnet = true; break;
                case SkillCatalogDefs.OverCoin: PendingCoin += Mathf.RoundToInt(def.ValueAt(1)); break;
                case SkillCatalogDefs.OverMight: MightStacks++; break;
            }
        }

        public float ConsumeHealFraction() { float v = PendingHealFraction; PendingHealFraction = 0f; return v; }
        public int ConsumeCoin() { int v = PendingCoin; PendingCoin = 0; return v; }
        public bool ConsumeMagnet() { bool v = PendingMagnet; PendingMagnet = false; return v; }

        /// <summary>Health granted by rank-ups that the player component has not yet consumed.</summary>
        public float PendingMaxHealthBonus { get; private set; }
        public float ConsumeMaxHealthBonus() { float v = PendingMaxHealthBonus; PendingMaxHealthBonus = 0f; return v; }

        float Value(string id) => SkillCatalogDefs.ById(id)?.ValueAt(RankOf(id)) ?? 0f;

        /// <summary>A card's named per-rank number at its current rank.</summary>
        float Table(string id, string table, float fallback) =>
            SkillCatalogDefs.ById(id)?.At(table, Mathf.Max(1, RankOf(id)), fallback) ?? fallback;

        void SyncAutonomousCooldowns()
        {
            foreach (var p in _procPowers)
            {
                string id = p.timer.id;
                if (p.timed && Has(id)) p.timer.Cooldown = CooldownAt(id, RankOf(id), IsEvolved(id)) * CooldownMultiplier;
            }
        }

        // ══════════════════════════════════════════════════════════ STAT (P8 soft caps)

        /// <summary>Damage Up. Plain multiplier — the only stat with no cap, by design.</summary>
        public float DamageMultiplier => (1f + Value(SkillCatalogDefs.StatDamage)) * (1f + MightStacks * MightPerStack);

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

        public float CoinMultiplier => (1f + Value(SkillCatalogDefs.StatCoinGain)) * (1f + Value(SkillCatalogDefs.UniGreed));

        // ── A4 stats ──
        /// <summary>Cooldown: every timed power's recharge is multiplied by this.</summary>
        public float CooldownMultiplier => 1f - Value(SkillCatalogDefs.StatCooldown);
        /// <summary>Area: every power area (nova, burst, blast, orbit, burning ground) grows by this.</summary>
        public float AreaMultiplier => 1f + Value(SkillCatalogDefs.StatArea);
        /// <summary>Pickup Range: the radius pickups start flying to the player from.</summary>
        public float PickupRangeMultiplier => 1f + Value(SkillCatalogDefs.StatPickup);
        /// <summary>Regeneration: share of max health healed each second.</summary>
        public float RegenPerSecond => Value(SkillCatalogDefs.StatRegen);
        /// <summary>Luck: item and chest drop chances are multiplied by this (coins are not items).</summary>
        public float LuckMultiplier => 1f + Value(SkillCatalogDefs.StatLuck);

        /// <summary>Greed's price: enemies that spawn from now on have this much more health.</summary>
        public float EnemyHealthMultiplier => Has(SkillCatalogDefs.UniGreed) ? 1f + Table(SkillCatalogDefs.UniGreed, "hp", 0f) : 1f;

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
        public bool RollCrit() => Has(SkillCatalogDefs.UniCrit) && Roll(Value(SkillCatalogDefs.UniCrit));

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

        /// <summary>
        /// Every autonomous power resolves through P2's framework, which owns the global ≤2 procs/s
        /// ceiling. No card may proc outside it, however many are taken.
        /// </summary>
        public IReadOnlyList<PowerProc> PollPowers(float now, float playerHealthFraction)
        {
            ProcBuffer.Clear();

            foreach (var p in _procPowers)
            {
                string id = p.timer.id;
                if (Has(id) && p.timer.TryProc(now, playerHealthFraction))
                    ProcBuffer.Add(new PowerProc { skillId = id, targets = p.targets(this), radius = p.radius(this) });
            }

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
            // Only interval powers wait for a target; a kill or health trigger is not re-armed here.
            var p = ProcOf(skillId);
            if (p != null && p.timer.trigger == AutonomousPower.TriggerKind.Interval) p.timer.Refund(Time.time);
        }

        public float ReadinessOf(string skillId) => ProcOf(skillId)?.timer.Readiness(Time.time) ?? 1f;

        public void Reset()
        {
            _ranks.Clear();
            _quickstepDistance.Reset(); _kineticDistance.Reset(); _trailDistance.Reset();
            _trailDropsPending = 0;
            _bulletHose.Reset(); _heavyPressure.Reset(); _staticCharge.Reset(); _runGunMoving.Reset();
            _quickstepArmed = _kineticCharged = _staticFired = _shotWasQuickstep = false;
            _shotsSinceBreach = _shotsSinceShockwave = _shotsSinceSplit = _siphonKills = 0;
            _siphonTriggered = _guardianUsed = false;
            PendingMaxHealthBonus = 0f;
            PendingHealFraction = 0f;
            PendingCoin = 0;
            PendingMagnet = false;
            MightStacks = 0;
            AutonomousPower.ResetGlobalBudget();
        }
    }
}
