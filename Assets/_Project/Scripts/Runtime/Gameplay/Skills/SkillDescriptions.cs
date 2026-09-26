using UnityEngine;

namespace ZombieWar.Skills
{
    /// <summary>
    /// The one-line text on a level-up card: what the card DOES at the rank being offered, in the
    /// player's terms, with the real numbers.
    ///
    /// Replaces the generic "Automatic power · 300%" line, which told the player nothing about what
    /// they were choosing. Every number comes from <see cref="SkillDef.ValueAt"/> or a
    /// <see cref="SkillRuntime"/> helper the game itself uses, so the text cannot drift from the
    /// behaviour.
    /// </summary>
    public static class SkillDescriptions
    {
        public static string Describe(SkillDef def, int rank)
        {
            if (def == null) return string.Empty;
            rank = Mathf.Clamp(rank, 1, def.maxRank);
            float v = def.ValueAt(rank);
            string pct = $"{v * 100f:0}%";

            switch (def.id)
            {
                // ── stats ──
                case SkillCatalogDefs.StatDamage: return $"All damage +{pct}";
                case SkillCatalogDefs.StatFireRate: return $"Fire rate +{pct}";
                case SkillCatalogDefs.StatMoveSpeed: return $"Move speed +{pct}";
                case SkillCatalogDefs.StatMaxHealth: return $"Max health +{pct}";
                case SkillCatalogDefs.StatCoinGain: return $"Coins from kills +{pct}";

                // ── signatures ──
                case SkillCatalogDefs.SidearmRunGun: return $"Fire up to {pct} faster while moving";
                case SkillCatalogDefs.SidearmQuickstep: return $"Every {v:0.#} m walked, the next shot deals x2.5";
                case SkillCatalogDefs.SmgStatic: return $"Every {6 - rank} hits, lightning jumps to {v:0} enemies";
                case SkillCatalogDefs.SmgBulletHose: return $"Holding fire ramps fire rate up to +{pct}";
                case SkillCatalogDefs.ArFocusFire: return $"Each hit on the same enemy +{pct} damage";
                case SkillCatalogDefs.ArBreach: return $"Every {Mathf.Max(2, 6 - rank)} shots pierce {v:0} enemies and expose them";
                case SkillCatalogDefs.ShotgunPointBlank: return $"Up to +{pct} damage up close";
                case SkillCatalogDefs.ShotgunConcussion: return $"Hits slow enemies by {pct}";
                case SkillCatalogDefs.LmgHeavyPressure: return $"Sustained fire ramps damage up to +{pct}";
                case SkillCatalogDefs.LmgShockwave: return $"Every {Mathf.Max(4, 12 - 2 * rank)} shots, a shockwave cone";
                case SkillCatalogDefs.MarksmanLongshot: return $"Up to +{v * 500f:0}% damage at long range";
                case SkillCatalogDefs.MarksmanHunters: return $"First hit on each new target +{pct}";

                // ── universals ──
                case SkillCatalogDefs.UniExecution:
                    return $"+{pct} damage to enemies under {20 + 5 * (rank - 1)}% health";
                case SkillCatalogDefs.UniKinetic: return $"Every {v:0} m walked, a shield blocks one hit";

                // ── original powers ──
                case SkillCatalogDefs.AutoChainLightning:
                    return $"Every {Cd(def, rank)}s, lightning chains through {Mathf.Min(TargetQuery.MaxChain, Mathf.RoundToInt(v))} enemies";
                case SkillCatalogDefs.AutoOrdnance:
                    return $"Every {Cd(def, rank)}s, a shell hits the biggest crowd ({v:0.#} m)";
                case SkillCatalogDefs.AutoSoulBurst: return $"Every 12 kills, a burst around you ({v:0.#} m)";
                case SkillCatalogDefs.AutoEmergency:
                    return $"Below 30% health, blast enemies away (every {Cd(def, rank)}s)";

                // ── M8 powers ──
                case SkillCatalogDefs.AutoOrbit: return $"{v:0} blades spin around you, cutting what they touch";
                case SkillCatalogDefs.AutoDrone: return $"A drone shoots the nearest enemy {v:0.#} times a second";
                case SkillCatalogDefs.AutoFrostNova:
                    return $"Every {Cd(def, rank)}s, a frost ring ({v:0.#} m) slows enemies {35 + 10 * (rank - 1)}%";
                case SkillCatalogDefs.AutoFireTrail: return $"Walking leaves fire that burns {v:0} damage a second";
                case SkillCatalogDefs.AutoBoomerang:
                    return v <= 1f ? "A boomerang cuts out and back through every enemy"
                                   : $"{v:0} boomerangs cut out and back through every enemy";
                case SkillCatalogDefs.AutoAirstrike: return $"Every {Cd(def, rank)}s, {v:0} bombs fall on enemies on screen";

                // ── evolutions ──
                case SkillCatalogDefs.EvoThunderstorm: return "Chain Lightning every 2s through 6 enemies, harder";
                case SkillCatalogDefs.EvoCarpetBomb: return "Ordnance drops a line of 3 shells";
                case SkillCatalogDefs.EvoBuzzsaw: return "6 bigger, faster blades";
                case SkillCatalogDefs.EvoAbsoluteZero: return "Frost Nova freezes enemies solid; frozen take +50%";
                case SkillCatalogDefs.EvoSquadron: return "3 drones; their kills can drop coins";
                case SkillCatalogDefs.EvoReaper: return "Kills can release a soul burst";
            }
            return def.displayName;
        }

        /// <summary>"EVOLUTION" / "NEW" / "LV 2" — the small tag above the card name.</summary>
        public static string Tag(SkillDef def, int rank)
        {
            if (def == null) return string.Empty;
            if (def.IsEvolution) return "EVOLUTION";
            return rank <= 1 ? "NEW" : $"LV {rank}";
        }

        /// <summary>Colour per layer, so the card reads before the text does.</summary>
        public static Color LayerColor(SkillDef def) => def?.layer switch
        {
            SkillLayer.Stat => new Color(0.62f, 0.66f, 0.72f),
            SkillLayer.Signature => new Color(1.00f, 0.62f, 0.24f),
            SkillLayer.Autonomous => new Color(0.66f, 0.45f, 1.00f),
            SkillLayer.Universal => new Color(0.26f, 0.82f, 0.76f),
            SkillLayer.Evolution => new Color(1.00f, 0.80f, 0.20f),
            _ => Color.white,
        };

        static string Cd(SkillDef def, int rank) =>
            SkillRuntime.CooldownAt(def.id, rank, evolved: false).ToString("0.#");
    }
}
