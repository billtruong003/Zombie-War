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
                case SkillCatalogDefs.StatCooldown: return $"Powers recharge {pct} faster";
                case SkillCatalogDefs.StatArea: return $"Power areas {pct} bigger";
                case SkillCatalogDefs.StatPickup: return $"Pickups fly to you from {pct} farther";
                case SkillCatalogDefs.StatRegen: return $"Heal {v * 100f:0.#}% of max health every second";
                case SkillCatalogDefs.StatLuck: return $"Items and chests drop {pct} more often";

                // ── signatures ──
                case SkillCatalogDefs.SidearmRunGun: return $"Fire up to {pct} faster while moving";
                case SkillCatalogDefs.SidearmQuickstep: return $"Every {v:0.#} m walked, the next shot deals x2.5";
                case SkillCatalogDefs.SmgStatic: return $"Every {def.At("every", rank, 5f):0} hits, lightning jumps to {v:0} enemies";
                case SkillCatalogDefs.SmgBulletHose: return $"Holding fire ramps fire rate up to +{pct}";
                case SkillCatalogDefs.ArFocusFire: return $"Each hit on the same enemy +{pct} damage";
                case SkillCatalogDefs.ArBreach: return $"Every {def.At("every", rank, 5f):0} shots pierce {v:0} enemies and expose them";
                case SkillCatalogDefs.ShotgunPointBlank: return $"Up to +{pct} damage up close";
                case SkillCatalogDefs.ShotgunConcussion: return $"Hits slow enemies by {pct}";
                case SkillCatalogDefs.LmgHeavyPressure: return $"Sustained fire ramps damage up to +{pct}";
                case SkillCatalogDefs.LmgShockwave: return $"Every {def.At("every", rank, 10f):0} shots, a shockwave cone";
                case SkillCatalogDefs.MarksmanLongshot: return $"Up to +{v * 500f:0}% damage at long range";
                case SkillCatalogDefs.MarksmanHunters: return $"First hit on each new target +{pct}";
                case SkillCatalogDefs.RocketCluster:
                    return $"Each blast throws {v:0} bomblets ({def.At("dmg", rank, 0.4f) * 100f:0}% damage)";
                case SkillCatalogDefs.RocketNapalm:
                    return $"Blasts leave fire for {v:0.#}s ({def.At("dps", rank, 0.3f) * 100f:0}% damage a second)";

                // ── universals ──
                case SkillCatalogDefs.UniExecution:
                    return $"+{pct} damage to enemies under {def.At("threshold", rank, 0.2f) * 100f:0.#}% health";
                case SkillCatalogDefs.UniKinetic: return $"Every {v:0} m walked, a shield blocks one hit";
                case SkillCatalogDefs.UniPierce:
                    return $"Bullets pass through {v:0} more {(v > 1f ? "enemies" : "enemy")} ({(1f - def.At("dmg", rank, 0.9f)) * 100f:0}% less each)";
                case SkillCatalogDefs.UniRicochet:
                    return $"Hits bounce to {v:0} more {(v > 1f ? "enemies" : "enemy")} ({def.At("dmg", rank, 0.6f) * 100f:0}% damage)";
                case SkillCatalogDefs.UniSplit:
                    return $"Every {def.At("every", rank, 5f):0} shots, {v:0} extra bullets fan out";
                case SkillCatalogDefs.UniCrit: return $"{pct} chance to deal double damage";
                case SkillCatalogDefs.UniSiphon: return $"Every {v:0} kills, heal 3% of max health";
                case SkillCatalogDefs.UniAcid: return $"Hits poison: {v:0.#} damage a second per stack (up to 5)";
                case SkillCatalogDefs.UniExplosive: return $"{pct} of hits burst, hurting enemies nearby";
                case SkillCatalogDefs.UniDoubleTap: return $"{pct} chance to fire a free extra bullet";
                case SkillCatalogDefs.UniGuardian: return $"Once per run, a fatal hit heals you {pct} instead";
                case SkillCatalogDefs.UniGreed:
                    return $"Coins +{pct}, but new enemies have +{def.At("hp", rank, 0.1f) * 100f:0}% health";

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
                    return $"Every {Cd(def, rank)}s, a frost ring ({v:0.#} m) slows enemies {def.At("slow", rank, 0.35f) * 100f:0}%";
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

                // ── overflow ──
                case SkillCatalogDefs.OverHeal: return $"Heal {pct} of your max health now";
                case SkillCatalogDefs.OverMagnet: return "Pull every coin and gem on the map to you";
                case SkillCatalogDefs.OverCoin: return $"+{v:0} Coin now";
                case SkillCatalogDefs.OverMight: return $"All damage +{pct} (stacks)";
            }
            return def.displayName;
        }

        /// <summary>"EVOLUTION" / "NEW" / "LV 2" — the small tag above the card name.</summary>
        public static string Tag(SkillDef def, int rank)
        {
            if (def == null) return string.Empty;
            if (def.IsEvolution) return "EVOLUTION";
            if (def.IsOverflow) return "BONUS";
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
            SkillLayer.Overflow => new Color(0.36f, 0.86f, 0.52f),
            _ => Color.white,
        };

        static string Cd(SkillDef def, int rank) =>
            SkillRuntime.CooldownAt(def.id, rank, evolved: false).ToString("0.#");
    }
}
