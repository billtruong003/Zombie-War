using System.Collections.Generic;

namespace ZombieWar.Skills
{
    public enum SkillLayer { Stat, Signature, Autonomous, Universal }

    /// <summary>
    /// One of the 23 owner-locked cards. Names and ids are NOT editable — W2 locked the list, so this
    /// table mirrors `Review/M6_WeaponFactory/skill_catalog.csv` exactly.
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

        public SkillDef(string id, string displayName, SkillLayer layer, WeaponClass? family,
                        int maxRank, float baseValue, float perRank, string status)
        {
            this.id = id; this.displayName = displayName; this.layer = layer; this.family = family;
            this.maxRank = maxRank; this.baseValue = baseValue; this.perRank = perRank; this.status = status;
        }

        /// <summary>Rank 1 unlocks the behaviour; ranks 2+ strengthen the SAME fantasy.</summary>
        public float ValueAt(int rank) => rank <= 0 ? 0f : baseValue + perRank * (rank - 1);

        public bool IsCompatibleWith(WeaponClass equipped) => family == null || family.Value == equipped;
    }

    /// <summary>The 23. Do not add, remove or rename — the names are owner-locked (W2).</summary>
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
            new(UniKinetic,   "Kinetic Shield",  SkillLayer.Universal, null, 3, 40f,   -8f,   "SHOULD"),
        };

        public static IReadOnlyList<SkillDef> All => _all;

        public static SkillDef ById(string id)
        {
            for (int i = 0; i < _all.Count; i++) if (_all[i].id == id) return _all[i];
            return null;
        }
    }
}
