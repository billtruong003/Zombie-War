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
    public partial class SkillRuntime
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

        // The base rhythm is the power's rank-1 "cd" in its SkillDef (it used to be repeated here).
        static AutonomousPower Every(string id) => new(id, AutonomousPower.TriggerKind.Interval, CooldownAt(id, 1, false));

        readonly ProcPower[] _procPowers =
        {
            new(Every(SkillCatalogDefs.AutoChainLightning), r => r.ChainTargets, _ => 8f),
            new(Every(SkillCatalogDefs.AutoOrdnance), _ => 1, r => r.Value(SkillCatalogDefs.AutoOrdnance) * r.AreaMultiplier),
            new(new AutonomousPower(SkillCatalogDefs.AutoSoulBurst, AutonomousPower.TriggerKind.KillCount, 0.5f, killsRequired: 12),
                _ => 0, r => r.Value(SkillCatalogDefs.AutoSoulBurst) * r.AreaMultiplier, timed: false),
            new(new AutonomousPower(SkillCatalogDefs.AutoEmergency, AutonomousPower.TriggerKind.HealthThreshold, 30f, healthFraction: 0.3f),
                _ => 0, r => r.Value(SkillCatalogDefs.AutoEmergency) * r.AreaMultiplier),
            new(Every(SkillCatalogDefs.AutoFrostNova), _ => 0, r => r.FrostRadius),
            new(Every(SkillCatalogDefs.AutoBoomerang), r => r.BoomerangCount, _ => 9f),
            new(Every(SkillCatalogDefs.AutoAirstrike), r => r.AirstrikeBlasts, r => 2.6f * r.AreaMultiplier),
            new(Every(SkillCatalogDefs.AutoToxic), _ => 1, r => r.Value(SkillCatalogDefs.AutoToxic) * r.AreaMultiplier),
            new(Every(SkillCatalogDefs.AutoGravity), _ => 1, r => r.Value(SkillCatalogDefs.AutoGravity) * r.AreaMultiplier),
            new(Every(SkillCatalogDefs.AutoTurret), r => r.TurretCount, _ => 0f),
            new(Every(SkillCatalogDefs.AutoMeteor), _ => 1, r => r.Value(SkillCatalogDefs.AutoMeteor) * r.AreaMultiplier),
            new(Every(SkillCatalogDefs.AutoIceShards), r => Mathf.RoundToInt(r.Value(SkillCatalogDefs.AutoIceShards)), _ => 0f),
            new(Every(SkillCatalogDefs.AutoFlameBurst), _ => 1, r => r.Value(SkillCatalogDefs.AutoFlameBurst) * r.AreaMultiplier),
            new(Every(SkillCatalogDefs.AutoAxe), r => Mathf.RoundToInt(r.Value(SkillCatalogDefs.AutoAxe)), _ => 0f),
            new(Every(SkillCatalogDefs.AutoStomp), _ => 0, r => r.Value(SkillCatalogDefs.AutoStomp) * r.AreaMultiplier),
            new(Every(SkillCatalogDefs.AutoTimeWarp), _ => 0, _ => 0f),
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
        public int RankOf(string skillId)
        {
            int r = _ranks.TryGetValue(skillId, out var taken) ? taken : 0;
            return _innateCard == skillId && _innateRank > r ? _innateRank : r;
        }

        // An evolved gun's built-in card (backlog #23): counts as ranks of that card, takes no slot.
        string _innateCard; int _innateRank;

        public void SetInnate(string card, int rank) { _innateCard = card; _innateRank = rank; }
        public string InnateCard => _innateCard;
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

        // ══════════════════════════════════════════════════════════ CHESTS (A7)

        public enum ChestKind { Evolution, RankUp, Bonus }

        public struct ChestReward
        {
            public ChestKind kind;
            public SkillDef card;
            /// Rank after the chest (1 for an evolution or a bonus card).
            public int rank;
        }

        static readonly List<SkillDef> ChestScratch = new(16);

        /// <summary>
        /// Opens a chest and applies what it gives, in this order: an evolution the build is ready
        /// for (the only way to get one); else +1 rank on a card already owned; else a bonus card.
        /// Deterministic for a seed. Always gives something.
        /// </summary>
        /// <summary>An evolution the build could take right now (its power maxed, its partner owned).</summary>
        public bool AnyEvolutionReady
        {
            get
            {
                foreach (var d in SkillCatalogDefs.All)
                    if (d.IsEvolution && IsUnlocked(d) && CanEvolve(d)) return true;
                return false;
            }
        }

        public ChestReward OpenChest(int seed)
        {
            uint s = (uint)(seed * 2654435761u) | 1u;
            int Pick(int n) { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return n <= 0 ? 0 : (int)(s % (uint)n); }

            ChestScratch.Clear();
            foreach (var d in SkillCatalogDefs.All)
                if (d.IsEvolution && IsUnlocked(d) && CanEvolve(d)) ChestScratch.Add(d);
            if (ChestScratch.Count > 0)
            {
                var evo = ChestScratch[Pick(ChestScratch.Count)];
                Take(evo.id);
                return new ChestReward { kind = ChestKind.Evolution, card = evo, rank = 1 };
            }

            ChestScratch.Clear();
            foreach (var kv in _ranks)
            {
                var d = SkillCatalogDefs.ById(kv.Key);
                if (d != null && !d.IsEvolution && !d.IsOverflow && kv.Value < d.maxRank) ChestScratch.Add(d);
            }
            if (ChestScratch.Count > 0)
            {
                var d = ChestScratch[Pick(ChestScratch.Count)];
                Take(d.id);
                return new ChestReward { kind = ChestKind.RankUp, card = d, rank = RankOf(d.id) };
            }

            var bonus = SkillCatalogDefs.Overflow[Pick(SkillCatalogDefs.Overflow.Count)];
            Take(bonus.id);
            return new ChestReward { kind = ChestKind.Bonus, card = bonus, rank = 1 };
        }

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
            bool isEvolved = IsEvolved(powerId);
            float evolved = isEvolved ? 1.5f : 1f;
            float scale = SkillCatalogDefs.ById(powerId)?.At("dmg", rank, 1f) ?? 1f;
            return baseDamage * scale * DamageMultiplier
                   * Threat.ThreatDirector.EnemyStatMultiplier * evolved
                   * PowerBudget.Of(powerId, isEvolved);   // A10: every power in one damage budget
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

        // ── A5 powers
        public const float TurretSeconds = 6f;
        public const float ThornRadius = 1.7f;
        public int TurretCount => RankOf(SkillCatalogDefs.AutoTurret) >= 5 ? 2 : 1;
        public float TurretShotsPerSecond => Value(SkillCatalogDefs.AutoTurret);
        public float ThornDps => Value(SkillCatalogDefs.AutoThorns);
        public float ThornAuraRadius => Has(SkillCatalogDefs.AutoThorns) ? ThornRadius * AreaMultiplier : 0f;

        // ── A6 powers
        public float StormCloudInterval => Value(SkillCatalogDefs.AutoStormCloud);
        public float LandmineSpacing => Value(SkillCatalogDefs.AutoLandmine);
        public int WarDogCount => !Has(SkillCatalogDefs.AutoWarDog) ? 0 : IsEvolved(SkillCatalogDefs.AutoWarDog) ? 3
            : RankOf(SkillCatalogDefs.AutoWarDog) >= 5 ? 2 : 1;
        public float WarDogBitesPerSecond => Value(SkillCatalogDefs.AutoWarDog);
        public float TimeWarpSeconds => Value(SkillCatalogDefs.AutoTimeWarp);
        public int AirstrikeBlasts => Mathf.RoundToInt(Value(SkillCatalogDefs.AutoAirstrike));
        public int ChainTargets => IsEvolved(SkillCatalogDefs.AutoChainLightning) ? TargetQuery.MaxChain
            : Mathf.Min(TargetQuery.MaxChain, Mathf.RoundToInt(Value(SkillCatalogDefs.AutoChainLightning)));

        /// <summary>Cooldown in seconds of a timed power at a rank — used by the card text too.</summary>
        public static float CooldownAt(string powerId, int rank, bool evolved)
        {
            rank = Mathf.Max(1, rank);
            // An evolution with a "cd" of its own sets its own rhythm (Thunderstorm, Absolute Zero).
            var evo = evolved ? SkillCatalogDefs.EvolutionOf(powerId) : null;
            if (evo != null && evo.HasTable("cd")) return evo.At("cd", 1, 0f);
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

        /// <summary>Account-wide gun mastery bonuses (backlog #21), set when the gun is equipped.</summary>
        public GunMastery.Bonuses Account;
        bool _accountHealthApplied;

        /// <summary>Takes the account bonuses; the max-health one is applied once per run.</summary>
        public void ApplyAccount(GunMastery.Bonuses bonuses, Health health)
        {
            Account = bonuses;
            if (_accountHealthApplied || health == null) return;
            _accountHealthApplied = true;
            if (bonuses.maxHealth > 0f) health.IncreaseMax(1f + bonuses.maxHealth);
        }

        /// <summary>Damage Up. Plain multiplier — the only stat with no cap, by design.</summary>
        public float DamageMultiplier => (1f + Value(SkillCatalogDefs.StatDamage)) * (1f + MightStacks * MightPerStack) * (1f + Account.damage);

        /// <summary>Fire Rate Up + Run &amp; Gun + Bullet Hose, then P8's 2.2/2.5 soft cap.</summary>
        public float FireRateMultiplier
        {
            get
            {
                float raw = 1f + Value(SkillCatalogDefs.StatFireRate) + Account.fireRate;
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
                float raw = 1f + Value(SkillCatalogDefs.StatMoveSpeed) + Account.moveSpeed;
                if (Has(SkillCatalogDefs.LmgHeavyPressure) && EquippedFamily == WeaponClass.LMG)
                    raw *= Mathf.Lerp(1f, 0.75f, _heavyPressure.Value);   // the cost of the ramp
                return SoftCap.MoveSpeed.Apply(raw);
            }
        }

        public float CoinMultiplier => (1f + Value(SkillCatalogDefs.StatCoinGain)) * (1f + Value(SkillCatalogDefs.UniGreed));

        // ── A4 stats ──
        /// <summary>Cooldown: every timed power's recharge is multiplied by this.</summary>
        public float CooldownMultiplier => 1f - Value(SkillCatalogDefs.StatCooldown) - Account.cooldown;
        /// <summary>Area: every power area (nova, burst, blast, orbit, burning ground) grows by this.</summary>
        public float AreaMultiplier => 1f + Value(SkillCatalogDefs.StatArea) + Account.area;
        /// <summary>Pickup Range: the radius pickups start flying to the player from.</summary>
        public float PickupRangeMultiplier => 1f + Value(SkillCatalogDefs.StatPickup) + Account.pickupRange;
        /// <summary>Regeneration: share of max health healed each second.</summary>
        public float RegenPerSecond => Value(SkillCatalogDefs.StatRegen);
        /// <summary>Luck: item and chest drop chances are multiplied by this (coins are not items).</summary>
        public float LuckMultiplier => 1f + Value(SkillCatalogDefs.StatLuck);

        /// <summary>Greed's price: enemies that spawn from now on have this much more health.</summary>
        public float EnemyHealthMultiplier => Has(SkillCatalogDefs.UniGreed) ? 1f + Table(SkillCatalogDefs.UniGreed, "hp", 0f) : 1f;

        // ══════════════════════════════════════════════════════════ TICK

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
            Account = default; _accountHealthApplied = false;
            _innateCard = null; _innateRank = 0;
            PendingHealFraction = 0f;
            PendingCoin = 0;
            PendingMagnet = false;
            MightStacks = 0;
            AutonomousPower.ResetGlobalBudget();
        }
    }
}
