using System.Collections.Generic;

namespace ZombieWar.Skills
{
    /// <summary>
    /// Phase A10 balance pass: one damage factor per power, and one more for its evolution, so every
    /// power lands in the same budget whatever its base numbers were written as.
    ///
    /// Measured with the sandbox crowd bench (Review/M8/bench/0930_045008_a10_crowd_r5.csv: rank 5,
    /// 30 immortal enemies swarming the player, 16 s, starter gun ~900 DPS on the same crowd):
    /// powers ranged 23–544 DPS and evolutions 103–2446, so a pick was either dead weight or the
    /// whole build. The budget:
    ///   • a power at rank 5 ≈ 160–270 DPS (a fifth to a third of the gun), control powers lower;
    ///   • its evolution ≈ 420–460 DPS (about twice the power), control evolutions lower.
    /// Second pass (0930_051138_a10_crowd_r5_after.csv) trimmed the ones still outside the band.
    /// Contact powers (Boomerang, Landmine, Minefield, Axe, Stomp) vary 2–4× run to run with where
    /// the crowd stands, so their factors aim at the average, not one sample.
    /// A factor here multiplies the power's own base damage (PowerDamage), so the rank curve, Damage
    /// Up and the enemy scaling stay as they were. Re-bench after changing a power's numbers.
    /// </summary>
    public static class PowerBudget
    {
        static readonly Dictionary<string, float> Power = new()
        {
            { SkillCatalogDefs.AutoChainLightning, 4.0f },   //  44 → ~176 (few targets per strike)
            { SkillCatalogDefs.AutoOrdnance,       0.55f },  // 473 → ~260
            { SkillCatalogDefs.AutoSoulBurst,      0.85f },  // 291 → ~247
            { SkillCatalogDefs.AutoOrbit,          0.75f },  // 317 → ~238
            { SkillCatalogDefs.AutoDrone,          7.0f },   //  23 → ~161 (one gun)
            { SkillCatalogDefs.AutoAirstrike,      0.5f },   // 544 → ~272
            { SkillCatalogDefs.AutoToxic,          0.75f },  // 323 → ~242
            { SkillCatalogDefs.AutoGravity,        0.85f },  // 235 → ~200 (control first)
            { SkillCatalogDefs.AutoTurret,         6.0f },   //  33 → ~150 (after: 126 at 5.0)
            { SkillCatalogDefs.AutoMeteor,         0.85f },  // 289 → ~245
            { SkillCatalogDefs.AutoStormCloud,     6.0f },   //  29 → ~171 (one bolt at a time)
            { SkillCatalogDefs.AutoFlameBurst,     6.0f },   //  30 → ~178
            { SkillCatalogDefs.AutoAxe,            0.5f },   // 432 → ~216 (after: 388 at 0.6, noisy)
            { SkillCatalogDefs.AutoWarDog,         3.0f },   //  54 → ~162
            { SkillCatalogDefs.AutoStomp,          1.1f },   // 110 → ~120-215 (after: 295 at 1.5, noisy; control first)
            { SkillCatalogDefs.AutoBoomerang,      2.0f },   // 20-178 run to run: three narrow lines through the crowd
        };

        /// Applied on top of the power's factor once it has evolved.
        static readonly Dictionary<string, float> Evolved = new()
        {
            { SkillCatalogDefs.AutoChainLightning, 0.85f },  // Thunderstorm   136×4    → ~462
            { SkillCatalogDefs.AutoOrdnance,       0.33f },  // Carpet Bomb    2446×.55 → ~444
            { SkillCatalogDefs.AutoOrbit,          2.4f },   // Buzzsaw Halo   238×.75  → ~427
            { SkillCatalogDefs.AutoFrostNova,      1.4f },   // Absolute Zero  274      → ~384 (freezes)
            { SkillCatalogDefs.AutoDrone,          0.62f },  // Drone Squadron 103×7    → ~447
            { SkillCatalogDefs.AutoSoulBurst,      1.1f },   // Reaper         461×.85  → ~431
            { SkillCatalogDefs.AutoToxic,          0.78f },  // Plague         676×.75  → ~457 (after: 527 at 0.9)
            { SkillCatalogDefs.AutoGravity,        0.65f },  // Singularity    804×.85  → ~444
            { SkillCatalogDefs.AutoTurret,         0.2f },   // Fortress       422×6    → ~430
            { SkillCatalogDefs.AutoMeteor,         0.36f },  // Meteor Storm   1165×.85 → ~457 (after: 571 at 0.45)
            { SkillCatalogDefs.AutoThorns,         0.9f },   // Iron Maiden    401      → ~443 (after: 542 at 1.1)
            { SkillCatalogDefs.AutoStormCloud,     0.3f },   // Supercell      247×6    → ~444
            { SkillCatalogDefs.AutoIceShards,      3.7f },   // Blizzard       119      → ~440
            { SkillCatalogDefs.AutoFlameBurst,     0.5f },   // Dragon Breath  152×6    → ~455
            { SkillCatalogDefs.AutoLandmine,       0.62f },  // Minefield      259-918 run to run → ~450 on average
            { SkillCatalogDefs.AutoAxe,            0.45f },  // Axe Storm      1661×.6  → ~448
            { SkillCatalogDefs.AutoWarDog,         1.15f },  // Alpha Pack     127×3    → ~438
            { SkillCatalogDefs.AutoStomp,          1.45f },  // Earthquake     266×1.1  → ~425 (stuns)
        };

        /// <summary>The damage factor for a power (1 for anything not in the budget, e.g. gun cards).</summary>
        public static float Of(string powerId, bool evolved)
        {
            if (string.IsNullOrEmpty(powerId)) return 1f;
            float f = Power.TryGetValue(powerId, out var p) ? p : 1f;
            if (evolved && Evolved.TryGetValue(powerId, out var e)) f *= e;
            return f;
        }

        public static IEnumerable<string> BudgetedPowers => Power.Keys;
        public static IEnumerable<string> BudgetedEvolutions => Evolved.Keys;
    }
}
