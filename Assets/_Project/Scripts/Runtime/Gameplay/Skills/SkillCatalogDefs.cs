using System.Collections.Generic;

namespace ZombieWar.Skills
{
    public enum SkillLayer { Stat, Signature, Autonomous, Universal, Evolution, Overflow }

    /// <summary>Which build slot a card occupies (owner 2026-09-29: 6 skill slots + 4 stat slots).</summary>
    public enum SkillSlot { None, Skill, Stat }

    /// <summary>
    /// One card. The original 23 mirror `Review/M6_WeaponFactory/skill_catalog.csv`; the owner
    /// unlocked the list on 2026-09-26 and approved six more autonomous powers and six evolutions
    /// (`Review/M8/skill_proposal.md`), then five ranks per card, build slots, overflow cards and
    /// account-level unlocks on 2026-09-29 (`Review/M8/skill_proposal_v2.md`, part A).
    ///
    /// Every number a card uses is a table indexed by rank (index 0 = rank 1), so balance lives in
    /// one place and no formula elsewhere does rank arithmetic. Magnitudes are TUNING: starting
    /// hypotheses, not balance claims. Rank 5 of a 5-rank card matches the old rank 3.
    /// </summary>
    public sealed class SkillDef
    {
        public readonly string id;
        public readonly string displayName;
        public readonly SkillLayer layer;
        /// <summary>Null = any weapon. Otherwise the FAMILY this signature card belongs to — never a weapon id.</summary>
        public readonly WeaponClass? family;
        public readonly int maxRank;
        public readonly string status;   // MUST / SHOULD / LATER — build order, not scope
        /// <summary>Account level that unlocks the card (1 = in the starter set). Signature cards come
        /// with their gun; an evolution is unlocked once both of its parts are.</summary>
        public readonly int unlockLevel;

        /// <summary>Evolution only: the power this card evolves (must be at max rank).</summary>
        public readonly string evolvesFrom;
        /// <summary>Evolution only: the partner card the player must also own.</summary>
        public readonly string partner;

        readonly float[] _values;
        readonly Dictionary<string, float[]> _tables;

        public SkillDef(string id, string displayName, SkillLayer layer, WeaponClass? family, float[] values,
                        string status, int unlockLevel = 1, string evolvesFrom = null, string partner = null,
                        Dictionary<string, float[]> tables = null)
        {
            this.id = id; this.displayName = displayName; this.layer = layer; this.family = family;
            _values = values ?? new[] { 1f };
            maxRank = layer == SkillLayer.Overflow ? int.MaxValue : _values.Length;
            this.status = status; this.unlockLevel = unlockLevel;
            this.evolvesFrom = evolvesFrom; this.partner = partner;
            _tables = tables;
        }

        public bool IsEvolution => layer == SkillLayer.Evolution;
        public bool IsOverflow => layer == SkillLayer.Overflow;

        public SkillSlot Slot => layer switch
        {
            SkillLayer.Stat => SkillSlot.Stat,
            SkillLayer.Evolution or SkillLayer.Overflow => SkillSlot.None,
            _ => SkillSlot.Skill,
        };

        /// <summary>Rank 1 unlocks the behaviour; ranks 2+ strengthen the SAME fantasy.</summary>
        public float ValueAt(int rank) => rank <= 0 ? 0f : _values[System.Math.Min(rank, _values.Length) - 1];

        /// <summary>A named per-rank number other than the main value ("cd" cooldown, "dmg" damage scale,
        /// "every" shots or hits between procs, "threshold", "slow"); <paramref name="fallback"/> when the
        /// card has no such table.</summary>
        public float At(string table, int rank, float fallback)
        {
            if (_tables == null || !_tables.TryGetValue(table, out var t) || t.Length == 0) return fallback;
            return t[System.Math.Clamp(rank, 1, t.Length) - 1];
        }

        public bool HasTable(string table) => _tables != null && _tables.ContainsKey(table);

        public bool IsCompatibleWith(WeaponClass equipped) => family == null || family.Value == equipped;
    }

    /// <summary>The card list: stats, signatures, powers, universals, evolutions, and the overflow cards
    /// offered once everything the player owns is maxed.</summary>
    public static class SkillCatalogDefs
    {
        public const int MaxSkillSlots = 6;
        public const int MaxStatSlots = 4;

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

        // ── Overflow: offered when every owned card is maxed and every slot is full ──
        public const string OverHeal = "over.heal";
        public const string OverMagnet = "over.magnet";
        public const string OverCoin = "over.coin";
        public const string OverMight = "over.might";

        /// <summary>Damage scale of a power per rank (Power damage = base × this × Damage Up …).</summary>
        static readonly float[] PowerDamage = { 1f, 1.15f, 1.3f, 1.45f, 1.6f };

        static float[] R(params float[] v) => v;
        static Dictionary<string, float[]> T(params (string key, float[] values)[] tables)
        {
            var d = new Dictionary<string, float[]>(tables.Length);
            foreach (var (key, values) in tables) d[key] = values;
            return d;
        }
        static Dictionary<string, float[]> Power(params (string key, float[] values)[] extra)
        {
            var d = T(extra);
            if (!d.ContainsKey("dmg")) d["dmg"] = PowerDamage;
            return d;
        }

        static readonly List<SkillDef> _all = new()
        {
            // ── STAT (5) — the 4 stat slots ─────────────────────────────────────────────
            new(StatDamage,    "Damage Up",     SkillLayer.Stat, null, R(0.15f, 0.25f, 0.35f, 0.45f, 0.55f), "MUST"),
            new(StatFireRate,  "Fire Rate Up",  SkillLayer.Stat, null, R(0.12f, 0.20f, 0.28f, 0.36f, 0.44f), "MUST"),
            new(StatMoveSpeed, "Move Speed Up", SkillLayer.Stat, null, R(0.10f, 0.17f, 0.24f, 0.31f, 0.38f), "MUST"),
            new(StatMaxHealth, "Max Health Up", SkillLayer.Stat, null, R(0.20f, 0.35f, 0.50f, 0.65f, 0.80f), "MUST"),
            new(StatCoinGain,  "Coin Gain Up",  SkillLayer.Stat, null, R(0.25f, 0.325f, 0.40f, 0.475f, 0.55f), "SHOULD"),

            // ── SIGNATURE (12) — two per family, resolved by FAMILY, never by weapon id ──
            new(SidearmRunGun,      "Run & Gun",       SkillLayer.Signature, WeaponClass.Sidearm, R(0.25f, 0.31f, 0.37f, 0.43f, 0.49f), "MUST"),
            // value = metres walked to arm the next shot
            new(SidearmQuickstep,   "Quickstep Round", SkillLayer.Signature, WeaponClass.Sidearm, R(8f, 7.4f, 6.8f, 6.2f, 5.6f), "MUST"),
            // value = enemies the spark jumps to; every = hits to charge
            new(SmgStatic,          "Static Build-up", SkillLayer.Signature, WeaponClass.SMG, R(3f, 3f, 4f, 4f, 5f), "MUST",
                tables: T(("every", R(5f, 4f, 4f, 3f, 3f)))),
            new(SmgBulletHose,      "Bullet Hose",     SkillLayer.Signature, WeaponClass.SMG, R(0.40f, 0.475f, 0.55f, 0.625f, 0.70f), "MUST"),
            new(ArFocusFire,        "Focus Fire",      SkillLayer.Signature, WeaponClass.AssaultRifle, R(0.08f, 0.10f, 0.12f, 0.14f, 0.16f), "MUST"),
            // value = enemies pierced; every = shots between breach rounds
            new(ArBreach,           "Breach Round",    SkillLayer.Signature, WeaponClass.AssaultRifle, R(2f, 2f, 3f, 3f, 4f), "SHOULD",
                tables: T(("every", R(5f, 4f, 4f, 3f, 3f)))),
            new(ShotgunPointBlank,  "Point Blank",     SkillLayer.Signature, WeaponClass.Shotgun, R(0.60f, 0.725f, 0.85f, 0.975f, 1.10f), "MUST"),
            new(ShotgunConcussion,  "Concussion",      SkillLayer.Signature, WeaponClass.Shotgun, R(0.30f, 0.35f, 0.40f, 0.45f, 0.50f), "MUST"),
            new(LmgHeavyPressure,   "Heavy Pressure",  SkillLayer.Signature, WeaponClass.LMG, R(0.30f, 0.36f, 0.42f, 0.48f, 0.54f), "SHOULD"),
            // value = cone angle; every = shots between waves
            new(LmgShockwave,       "Shockwave Belt",  SkillLayer.Signature, WeaponClass.LMG, R(45f, 50f, 55f, 60f, 65f), "LATER",
                tables: T(("every", R(10f, 9f, 8f, 7f, 6f)))),
            new(MarksmanLongshot,   "Longshot",        SkillLayer.Signature, WeaponClass.Marksman, R(0.10f, 0.125f, 0.15f, 0.175f, 0.20f), "SHOULD"),
            new(MarksmanHunters,    "Hunter's Mark",   SkillLayer.Signature, WeaponClass.Marksman, R(0.50f, 0.625f, 0.75f, 0.875f, 1.0f), "SHOULD"),

            // ── AUTONOMOUS (4) ──────────────────────────────────────────────────────────
            // value = enemies the chain jumps through
            new(AutoChainLightning, "Chain Lightning",      SkillLayer.Autonomous, null, R(3f, 3f, 4f, 4f, 5f), "MUST", 1,
                tables: Power(("cd", R(6f, 5.5f, 5f, 4.5f, 4f)))),
            // value = blast radius
            new(AutoOrdnance,       "Ordnance Core",        SkillLayer.Autonomous, null, R(3.5f, 3.8f, 4.1f, 4.4f, 4.7f), "MUST", 14,
                tables: Power(("cd", R(7f, 6.5f, 6f, 5.5f, 5f)))),
            new(AutoSoulBurst,      "Soul Burst",           SkillLayer.Autonomous, null, R(4f, 4.4f, 4.8f, 5.2f, 5.6f), "MUST", 17,
                tables: Power()),
            new(AutoEmergency,      "Emergency Detonation", SkillLayer.Autonomous, null, R(5f, 5.5f, 6f, 6.5f, 7f), "SHOULD", 26,
                tables: Power(("cd", R(30f, 27.5f, 25f, 22.5f, 20f)))),

            // ── UNIVERSAL (2) ───────────────────────────────────────────────────────────
            // value = bonus damage; threshold = target health fraction
            new(UniExecution, "Execution Round", SkillLayer.Universal, null, R(0.80f, 0.95f, 1.10f, 1.25f, 1.40f), "MUST", 1,
                tables: T(("threshold", R(0.20f, 0.225f, 0.25f, 0.275f, 0.30f)))),
            // M8: 40/32/24 m recharged every ~5 s at run speed; a circling test bot then went eight
            // minutes without losing a single hit point (44 hits, all absorbed). value = metres per charge.
            new(UniKinetic,   "Kinetic Shield",  SkillLayer.Universal, null, R(60f, 56f, 52f, 48f, 44f), "SHOULD", 11),

            // ── M8 AUTONOMOUS (6) — value = the number each rank grows ─────────────────────
            new(AutoOrbit,     "Orbit Blades",  SkillLayer.Autonomous, null, R(2f, 2f, 3f, 3f, 4f), "MUST", 1, tables: Power()),            // blades
            new(AutoDrone,     "Drone Buddy",   SkillLayer.Autonomous, null, R(2.5f, 2.875f, 3.25f, 3.625f, 4f), "MUST", 1, tables: Power()), // shots/s
            new(AutoFrostNova, "Frost Nova",    SkillLayer.Autonomous, null, R(4.5f, 4.875f, 5.25f, 5.625f, 6f), "MUST", 1,                   // radius m
                tables: Power(("cd", R(5f, 5f, 5f, 5f, 5f)), ("slow", R(0.35f, 0.40f, 0.45f, 0.50f, 0.55f)))),
            new(AutoFireTrail, "Fire Trail",    SkillLayer.Autonomous, null, R(14f, 17.5f, 21f, 24.5f, 28f), "MUST", 2, tables: Power()),     // burn dps
            new(AutoBoomerang, "Boomerang",     SkillLayer.Autonomous, null, R(1f, 1f, 2f, 2f, 3f), "MUST", 1,                               // blades
                tables: Power(("cd", R(2.5f, 2.4f, 2.3f, 2.2f, 2.1f)))),
            new(AutoAirstrike, "Airstrike",     SkillLayer.Autonomous, null, R(3f, 3f, 4f, 4f, 5f), "MUST", 5,                               // blasts
                tables: Power(("cd", R(8f, 7.75f, 7.5f, 7.25f, 7f)))),

            // ── M8 EVOLUTIONS (6) — one rank, offered once the power is maxed and the partner owned ─
            new(EvoThunderstorm, "Thunderstorm",   SkillLayer.Evolution, null, R(1f), "MUST", 1, AutoChainLightning, StatFireRate),
            new(EvoCarpetBomb,   "Carpet Bomb",    SkillLayer.Evolution, null, R(1f), "MUST", 1, AutoOrdnance,       StatDamage),
            new(EvoBuzzsaw,      "Buzzsaw Halo",   SkillLayer.Evolution, null, R(1f), "MUST", 1, AutoOrbit,          StatMoveSpeed),
            new(EvoAbsoluteZero, "Absolute Zero",  SkillLayer.Evolution, null, R(1f), "MUST", 1, AutoFrostNova,      StatMaxHealth),
            new(EvoSquadron,     "Drone Squadron", SkillLayer.Evolution, null, R(1f), "MUST", 1, AutoDrone,          StatCoinGain),
            new(EvoReaper,       "Reaper",         SkillLayer.Evolution, null, R(1f), "MUST", 1, AutoSoulBurst,      UniExecution),
        };

        /// <summary>Offered only when nothing else can be: the build is full and maxed. Not part of
        /// <see cref="All"/> (no slot, no rank cap, no unlock).</summary>
        static readonly List<SkillDef> _overflow = new()
        {
            new(OverHeal,   "Heal",       SkillLayer.Overflow, null, R(0.25f), "MUST"),   // fraction of max health
            new(OverMagnet, "Magnet",     SkillLayer.Overflow, null, R(1f), "MUST"),      // pulls every pickup on the map
            new(OverCoin,   "Coin Bag",   SkillLayer.Overflow, null, R(60f), "MUST"),     // Coin
            new(OverMight,  "Might",      SkillLayer.Overflow, null, R(0.03f), "MUST"),   // damage per stack
        };

        public static IReadOnlyList<SkillDef> All => _all;
        public static IReadOnlyList<SkillDef> Overflow => _overflow;

        /// <summary>The evolution a power grows into, or null.</summary>
        public static SkillDef EvolutionOf(string powerId)
        {
            for (int i = 0; i < _all.Count; i++)
                if (_all[i].IsEvolution && _all[i].evolvesFrom == powerId) return _all[i];
            return null;
        }

        /// <summary>
        /// Whether the card can appear for a player at <paramref name="accountLevel"/>. Signature
        /// cards come with the gun; an evolution needs both of its parts unlocked.
        /// </summary>
        public static bool IsUnlocked(SkillDef def, int accountLevel)
        {
            if (def == null) return false;
            if (def.IsOverflow || def.layer == SkillLayer.Signature) return true;
            if (def.IsEvolution)
                return IsUnlocked(ById(def.evolvesFrom), accountLevel) && IsUnlocked(ById(def.partner), accountLevel);
            return def.unlockLevel <= accountLevel;
        }

        /// <summary>Cards whose unlock level is exactly <paramref name="level"/> (for the unlock popup).</summary>
        public static void UnlockedAt(int level, List<SkillDef> into)
        {
            into.Clear();
            for (int i = 0; i < _all.Count; i++)
            {
                var d = _all[i];
                if (d.layer == SkillLayer.Signature || d.IsEvolution) continue;
                if (d.unlockLevel == level) into.Add(d);
            }
        }

        // ById runs several times per pellet hit, so it is a dictionary, not a scan of the list.
        static Dictionary<string, SkillDef> _byId;

        public static SkillDef ById(string id)
        {
            if (id == null) return null;
            if (_byId == null)
            {
                _byId = new Dictionary<string, SkillDef>(_all.Count + _overflow.Count);
                for (int i = 0; i < _all.Count; i++) _byId[_all[i].id] = _all[i];
                for (int i = 0; i < _overflow.Count; i++) _byId[_overflow[i].id] = _overflow[i];
            }
            return _byId.TryGetValue(id, out var def) ? def : null;
        }
    }
}
