using System.Collections.Generic;

namespace ZombieWar.Skills
{
    public enum SkillLayer { Stat, Signature, Autonomous, Universal, Evolution }

    /// <summary>
    /// One card. The original 23 mirror `Review/M6_WeaponFactory/skill_catalog.csv`; the owner
    /// unlocked the list on 2026-09-26 and approved six more autonomous powers and six evolutions
    /// (`Review/M8/skill_proposal.md`).
    ///
    /// Magnitudes are TUNING: they are starting hypotheses, not balance claims.
    /// </summary>
    public sealed class SkillDef
    {
        public readonly string id;
        public readonly string displayName;
        public readonly SkillLayer layer;
        /// <summary>Null = any weapon. Otherwise the FAMILY this signature card belongs to — never a weapon id.</summary>
        public readonly WeaponClass? family;
        public readonly int maxRank;
        public readonly float baseValue;
        public readonly float perRank;
        public readonly string status;   // MUST / SHOULD / LATER — build order, not scope

        /// <summary>Evolution only: the power this card evolves (must be at max rank).</summary>
        public readonly string evolvesFrom;
        /// <summary>Evolution only: the partner card the player must also own.</summary>
        public readonly string partner;

        public SkillDef(string id, string displayName, SkillLayer layer, WeaponClass? family,
                        int maxRank, float baseValue, float perRank, string status,
                        string evolvesFrom = null, string partner = null)
        {
            this.id = id; this.displayName = displayName; this.layer = layer; this.family = family;
            this.maxRank = maxRank; this.baseValue = baseValue; this.perRank = perRank; this.status = status;
            this.evolvesFrom = evolvesFrom; this.partner = partner;
        }

        public bool IsEvolution => layer == SkillLayer.Evolution;

        /// <summary>Rank 1 unlocks the behaviour; ranks 2+ strengthen the SAME fantasy.</summary>
        public float ValueAt(int rank) => rank <= 0 ? 0f : baseValue + perRank * (rank - 1);

        public bool IsCompatibleWith(WeaponClass equipped) => family == null || family.Value == equipped;
    }

    /// <summary>The card list: the 23 originals, six M8 powers and their six evolutions.</summary>
    public static class SkillCatalogDefs
    {
        public const string StatDamage = "stat.damage";
        public const string StatFireRate = "stat.firerate";
        public const string StatMoveSpeed = "stat.movespeed";
        public const string StatMaxHealth = "stat.maxhealth";
        public const string StatCoinGain = "stat.coingain";
        public const string SidearmRunGun = "sidearm.rungun";
        public const string SidearmQuickstep = "sidearm.quickstep";
        public const string SmgStatic = "smg.static";
        public const string SmgBulletHose = "smg.bullethose";
        public const string ArFocusFire = "ar.focusfire";
        public const string ArBreach = "ar.breach";
        public const string ShotgunPointBlank = "shotgun.pointblank";
        public const string ShotgunConcussion = "shotgun.concussion";
        public const string LmgHeavyPressure = "lmg.heavypressure";
        public const string LmgShockwave = "lmg.shockwave";
        public const string MarksmanLongshot = "marksman.longshot";
        public const string MarksmanHunters = "marksman.hunters";
        public const string AutoChainLightning = "auto.chainlightning";
        public const string AutoOrdnance = "auto.ordnance";
        public const string AutoSoulBurst = "auto.soulburst";
        public const string AutoEmergency = "auto.emergency";
        public const string UniExecution = "uni.execution";
        public const string UniKinetic = "uni.kinetic";

        // ── M8 powers (owner-approved 2026-09-26) ──
        public const string AutoOrbit = "auto.orbit";
        public const string AutoDrone = "auto.drone";
        public const string AutoFrostNova = "auto.frostnova";
        public const string AutoFireTrail = "auto.firetrail";
        public const string AutoBoomerang = "auto.boomerang";
        public const string AutoAirstrike = "auto.airstrike";

        // ── M8 evolutions: power at max rank + partner card owned ──
        public const string EvoThunderstorm = "evo.thunderstorm";
        public const string EvoCarpetBomb = "evo.carpetbomb";
        public const string EvoBuzzsaw = "evo.buzzsaw";
        public const string EvoAbsoluteZero = "evo.absolutezero";
        public const string EvoSquadron = "evo.squadron";
        public const string EvoReaper = "evo.reaper";

        static readonly List<SkillDef> _all = new()
        {
            // ── STAT (5) ────────────────────────────────────────────────────────────────
            new(StatDamage,    "Damage Up",     SkillLayer.Stat, null, 5, 0.15f, 0.10f, "MUST"),
            new(StatFireRate,  "Fire Rate Up",  SkillLayer.Stat, null, 5, 0.12f, 0.08f, "MUST"),
            new(StatMoveSpeed, "Move Speed Up", SkillLayer.Stat, null, 5, 0.10f, 0.07f, "MUST"),
            new(StatMaxHealth, "Max Health Up", SkillLayer.Stat, null, 5, 0.20f, 0.15f, "MUST"),
            new(StatCoinGain,  "Coin Gain Up",  SkillLayer.Stat, null, 3, 0.25f, 0.15f, "SHOULD"),

            // ── SIGNATURE (12) — two per family, resolved by FAMILY, never by weapon id ──
            new(SidearmRunGun,      "Run & Gun",       SkillLayer.Signature, WeaponClass.Sidearm, 3, 0.25f, 0.12f, "MUST"),
            new(SidearmQuickstep,   "Quickstep Round", SkillLayer.Signature, WeaponClass.Sidearm, 3, 8f,    -1.2f, "MUST"),
            new(SmgStatic,          "Static Build-up", SkillLayer.Signature, WeaponClass.SMG,     3, 3f,     1f,   "MUST"),
            new(SmgBulletHose,      "Bullet Hose",     SkillLayer.Signature, WeaponClass.SMG,     3, 0.40f, 0.15f, "MUST"),
            new(ArFocusFire,        "Focus Fire",      SkillLayer.Signature, WeaponClass.AssaultRifle, 3, 0.08f, 0.04f, "MUST"),
            new(ArBreach,           "Breach Round",    SkillLayer.Signature, WeaponClass.AssaultRifle, 3, 2f,    1f,   "SHOULD"),
            new(ShotgunPointBlank,  "Point Blank",     SkillLayer.Signature, WeaponClass.Shotgun, 3, 0.60f, 0.25f, "MUST"),
            new(ShotgunConcussion,  "Concussion",      SkillLayer.Signature, WeaponClass.Shotgun, 3, 0.30f, 0.10f, "MUST"),
            new(LmgHeavyPressure,   "Heavy Pressure",  SkillLayer.Signature, WeaponClass.LMG,     3, 0.30f, 0.12f, "SHOULD"),
            new(LmgShockwave,       "Shockwave Belt",  SkillLayer.Signature, WeaponClass.LMG,     3, 45f,   10f,   "LATER"),
            new(MarksmanLongshot,   "Longshot",        SkillLayer.Signature, WeaponClass.Marksman, 3, 0.10f, 0.05f, "SHOULD"),
            new(MarksmanHunters,    "Hunter's Mark",   SkillLayer.Signature, WeaponClass.Marksman, 3, 0.50f, 0.25f, "SHOULD"),

            // ── AUTONOMOUS (4) ──────────────────────────────────────────────────────────
            new(AutoChainLightning, "Chain Lightning",      SkillLayer.Autonomous, null, 3, 3f,   1f,    "MUST"),
            new(AutoOrdnance,       "Ordnance Core",        SkillLayer.Autonomous, null, 3, 3.5f, 0.6f,  "MUST"),
            new(AutoSoulBurst,      "Soul Burst",           SkillLayer.Autonomous, null, 3, 4f,   0.8f,  "MUST"),
            new(AutoEmergency,      "Emergency Detonation", SkillLayer.Autonomous, null, 3, 5f,   1f,    "SHOULD"),

            // ── UNIVERSAL (2) ───────────────────────────────────────────────────────────
            new(UniExecution, "Execution Round", SkillLayer.Universal, null, 3, 0.80f, 0.30f, "MUST"),
            // M8: 40/32/24 m recharged every ~5 s at run speed; a circling test bot then went eight
            // minutes without losing a single hit point (44 hits, all absorbed).
            new(UniKinetic,   "Kinetic Shield",  SkillLayer.Universal, null, 3, 60f,   -8f,   "SHOULD"),

            // ── M8 AUTONOMOUS (6) — value = the number each rank grows ─────────────────────
            new(AutoOrbit,     "Orbit Blades",  SkillLayer.Autonomous, null, 3, 2f,   1f,    "MUST"),   // blades
            new(AutoDrone,     "Drone Buddy",   SkillLayer.Autonomous, null, 3, 2.5f, 0.75f, "MUST"),   // shots/s
            new(AutoFrostNova, "Frost Nova",    SkillLayer.Autonomous, null, 3, 4.5f, 0.75f, "MUST"),   // radius m
            new(AutoFireTrail, "Fire Trail",    SkillLayer.Autonomous, null, 3, 14f,  7f,    "MUST"),   // burn dps
            new(AutoBoomerang, "Boomerang",     SkillLayer.Autonomous, null, 3, 1f,   1f,    "MUST"),   // blades
            new(AutoAirstrike, "Airstrike",     SkillLayer.Autonomous, null, 3, 3f,   1f,    "MUST"),   // blasts

            // ── M8 EVOLUTIONS (6) — one rank, offered once the power is maxed and the partner owned ─
            new(EvoThunderstorm, "Thunderstorm",   SkillLayer.Evolution, null, 1, 1f, 0f, "MUST", AutoChainLightning, StatFireRate),
            new(EvoCarpetBomb,   "Carpet Bomb",    SkillLayer.Evolution, null, 1, 1f, 0f, "MUST", AutoOrdnance,       StatDamage),
            new(EvoBuzzsaw,      "Buzzsaw Halo",   SkillLayer.Evolution, null, 1, 1f, 0f, "MUST", AutoOrbit,          StatMoveSpeed),
            new(EvoAbsoluteZero, "Absolute Zero",  SkillLayer.Evolution, null, 1, 1f, 0f, "MUST", AutoFrostNova,      StatMaxHealth),
            new(EvoSquadron,     "Drone Squadron", SkillLayer.Evolution, null, 1, 1f, 0f, "MUST", AutoDrone,          StatCoinGain),
            new(EvoReaper,       "Reaper",         SkillLayer.Evolution, null, 1, 1f, 0f, "MUST", AutoSoulBurst,      UniExecution),
        };

        public static IReadOnlyList<SkillDef> All => _all;

        /// <summary>The evolution a power grows into, or null.</summary>
        public static SkillDef EvolutionOf(string powerId)
        {
            for (int i = 0; i < _all.Count; i++)
                if (_all[i].IsEvolution && _all[i].evolvesFrom == powerId) return _all[i];
            return null;
        }

        // ById runs several times per pellet hit, so it is a dictionary, not a scan of the list.
        static Dictionary<string, SkillDef> _byId;

        public static SkillDef ById(string id)
        {
            if (id == null) return null;
            if (_byId == null)
            {
                _byId = new Dictionary<string, SkillDef>(_all.Count);
                for (int i = 0; i < _all.Count; i++) _byId[_all[i].id] = _all[i];
            }
            return _byId.TryGetValue(id, out var def) ? def : null;
        }
    }
}
