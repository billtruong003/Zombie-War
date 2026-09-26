# M8 — Skill overhaul proposal (for owner approval)

Status: **APPROVED and IMPLEMENTED, 2026-09-26** (owner: "chốt duyệt nguyên danh sách"). Feel audit and the
fixes it drove: `skill_feel_audit.md`. One deviation: an evolution needs its partner card OWNED, not
maxed (see the audit).

## What is wrong today (read from code)

| Problem | Evidence |
|---|---|
| A run only ever sees 13 cards | 5 stat + 2 signature (per weapon family) + 4 autonomous + 2 universal — `SkillCatalogDefs.cs` |
| Only two powers act on their own | Chain Lightning and Ordnance Core. Soul Burst waits for 12 kills, Emergency only fires under 30 % HP — `SkillRuntime.cs:32-35` |
| Power damage never grows | Chain 14 and blast 34 are flat numbers; rank only shortens the cooldown — `SkillCombatDriver.cs:38-40` |
| All powers together fire at most 2 times per second | `AutonomousPower.GlobalProcsPerSecond = 2` — a full build feels the same as one card |
| Cards are text only | no icon, no rarity colour, no rank pips — `RunOverlays.BindOfferText` |
| The build is invisible during play | the HUD shows no owned skill, no cooldown |
| No payoff for committing | no evolution / synergy; max rank just stops appearing |

## Proposed system changes (no new cards needed)

1. **Power damage scales with the build**: base × weapon damage-per-shot factor × Damage Up × rank.
2. **Global proc ceiling 2/s → 6/s**, per-power cooldowns unchanged (still bounded, now a full build reads as one).
3. **TargetQuery.MaxConsidered 64 → 128** so powers see the denser crowd.
4. **Offer weighting**: level 2 and 3 always contain at least one autonomous power, so the build visibly changes in the first 30 s.
5. **Skill bar on the HUD** (code-drawn): owned powers as icons with a cooldown ring; passive cards as small pips.
6. **Card visuals**: icon + layer colour (Stat grey, Signature weapon-colour, Power purple, Universal teal, Evolution gold) + rank pips.

## New autonomous powers (6)

All reuse existing primitives (P2 cooldown slot, P3 target query, FxPool, StatusCarrier). Max rank 3.

| # | Name (EN / VI) | What it does | Rank scaling |
|---|---|---|---|
| A1 | **Orbit Blades** / Lưỡi xoay | 2 blades circle the player (2.2 m), hit anything they touch every 0.4 s | +1 blade per rank |
| A2 | **Drone Buddy** / Drone bắn phụ | A drone over the shoulder shoots the nearest enemy 3×/s | fire rate +30 % per rank |
| A3 | **Frost Nova** / Vòng băng | Every 5 s a ring (5 m) damages and slows 40 % for 2 s | radius +1 m, slow +10 % |
| A4 | **Fire Trail** / Vệt lửa | Moving leaves burning ground (1.5 s) that damages what walks through | trail lasts longer, burns harder |
| A5 | **Boomerang** / Boomerang | Every 2.5 s a blade flies to the nearest enemy and back, piercing everything | +1 boomerang per rank |
| A6 | **Airstrike** / Không kích | Every 8 s, 3 marked blasts on random enemies on screen | +1 blast per rank |

## Evolutions (6)

Offered as a gold card once BOTH requirements are met; taking it replaces the power with its evolved form.

| Evolution | Requires | Effect |
|---|---|---|
| **Thunderstorm** | Chain Lightning 3 + Fire Rate Up 5 | chains every 2 s, 6 targets, arcs split |
| **Carpet Bomb** | Ordnance Core 3 + Damage Up 5 | 3 blasts in a line on the densest cluster |
| **Buzzsaw Halo** | Orbit Blades 3 + Move Speed Up 5 | 6 blades, larger ring, blades pierce knockback |
| **Absolute Zero** | Frost Nova 3 + Max Health Up 5 | nova freezes (full stop) 1.5 s, frozen enemies take +50 % |
| **Drone Squadron** | Drone Buddy 3 + Coin Gain Up 3 | 3 drones, each kill has 10 % chance of a coin |
| **Reaper** | Soul Burst 3 + Execution Round 3 | every kill below 20 % HP triggers a mini soul burst |

## Result

- A run sees **19 cards** (was 13) plus 6 evolution cards.
- **10 autonomous powers** (was 4), 8 of them always-on.
- Every stat card now also feeds an evolution, so stat picks stop being a dead end.

## Cost and order

| Step | Work | Size |
|---|---|---|
| 1 | Damage scaling, proc ceiling, query size, offer weighting | S |
| 2 | Skill bar on HUD + card icons/colours (icons generated to one style) | M |
| 3 | 6 new powers (+ pooled FX from existing packs) | L |
| 4 | Evolution system + 6 evolutions | M |
| 5 | Pacing re-measure (first card, level at 3 / 5 min, build strength) | S |

Owner to decide: approve / cut / rename any row above.
