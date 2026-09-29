# M8 — Skill proposal v2: deeper and wider builds (for owner approval)

Status: **depth changes APPROVED 2026-09-29** ("Ok rồi"); **Parts B, C and D waiting for approval**.
Mockups: canvas "HordeCall UI Mockups", page **Skill design** (https://claude.ai/artifact/CfMFC7odXtwVvDYMpuEJZJ).
Previous round: `skill_proposal.md` (6 powers + 6 evolutions, implemented 2026-09-26).

## Why

| Problem | Evidence |
|---|---|
| A skill maxes in 3 picks | 28 of 29 base cards have `maxRank` 3 (stats 5) — `SkillCatalogDefs.cs` |
| A 6-skill build is full around LV19–25 (minute 7–11) | XP curve `14 + (L-1)^1.05 × 5` (`RunState.cs:78`), ~2–3 XP/s at horde density (estimate, not measured) |
| The "max 6 skills" rule (owner, 2026-09-27) is not in code | no slot cap in `SkillRuntime` / `SkillOfferBuilder` |
| Builds look alike | 10 powers cover lightning, explosive, blade, ice, fire, drone, soul. Nothing changes the main gun's bullets, no poison, no crowd control, no summon you place, no sustain |

## Part A — depth (approved)

1. **Max rank 3 → 5** for autonomous, signature and universal cards. Per-rank values are re-split so rank 5 is as
   strong as today's rank 3 (+ a small bonus at rank 5). Stats stay at 5.
2. **6 skill slots + 4 stat slots.** Autonomous, signature and universal cards share the 6 skill slots; stat cards
   use their own 4. When a slot group is full, new cards of that group are no longer offered, only rank-ups.
3. **Evolutions come from chests only** (boss, elite, Supply Drop zone). A level-up never offers an evolution. A
   chest with no evolution ready gives one rank-up instead. Needs the chest pickup (board: "Chốt bộ vật phẩm cơ chế").
4. **Overflow cards** once everything owned is maxed: Heal 25 %, Coin bag, +3 % damage (stacks without limit).
   The offer always has 3 choices.

Result: full build ≈ 6 × 5 + 4 × 5 = **50 picks** (was ≈ 18–24), so only long runs reach it.

## Part B — new cards (waiting for approval)

Tags in brackets: the build family a card belongs to. Cost: S = code only, M = code + pooled FX from existing
packs, L = needs a new model (Blender, shared palette like the skill models).

### B1. Gun modifiers — Universal (5). Change what every bullet of the equipped gun does.

| # | Name (EN / VI) | Effect at rank 1 → 5 | Tags | Cost |
|---|---|---|---|---|
| U1 | **Piercing Rounds** / Đạn xuyên | bullets pass through +1 → +3 enemies, −10 % damage per enemy passed | gun | S |
| U2 | **Ricochet** / Đạn nảy | a hit bounces to the nearest other enemy: 1 → 3 bounces, 60 % damage | gun, chain | M |
| U3 | **Split Shot** / Đạn chùm | every 5th → 3rd shot fires a 3-bullet fan | gun | S |
| U4 | **Critical Rounds** / Chí mạng | 8 → 20 % chance of ×2 damage; crits show a gold number | gun | S |
| U5 | **Blood Siphon** / Hút máu | every 25 → 12 kills heals 3 % max HP | sustain | S |

### B2. Autonomous powers (5)

| # | Name (EN / VI) | Effect | Rank scaling | Tags | Cost |
|---|---|---|---|---|---|
| A7 | **Toxic Cloud** / Mây độc | every 6 s a poison cloud (3 m, 3 s) lands on the densest cluster; poison stacks up to 5 | radius, stacks, duration | poison | M |
| A8 | **Gravity Well** / Hố hút | every 9 s a vortex pulls enemies within 4 m to its centre for 1.5 s, then pops | radius, pull, pop damage | control | M |
| A9 | **Thorn Aura** / Hào quang gai | enemies touching you take damage and are knocked back | damage, knockback | sustain, control | S |
| A10 | **Sentry Turret** / Tháp súng | every 12 s drops a turret where you stand that shoots for 6 s | fire rate, duration, +1 turret at rank 5 | summon | L (turret model) |
| A11 | **Meteor** / Thiên thạch | every 15 s a meteor hits the densest cluster (big damage) and leaves burning ground | damage, crater size | fire, explosive | L (meteor model) |

### B3. Stats (3). Use the 4 stat slots, so a build now picks 4 of 8 stats.

| # | Name | Effect per rank (max 5) | Cost |
|---|---|---|---|
| S6 | **Cooldown** / Hồi chiêu | powers recharge 7 % faster | S |
| S7 | **Area** / Phạm vi | power areas (nova, cloud, blast, well, aura) +8 % size | S |
| S8 | **Pickup Range** / Tầm nhặt | pickup magnet radius +25 % | S |

### B4. Evolutions (5 new, all from chests)

| Evolution | Requires | Effect |
|---|---|---|
| **Plague** / Đại dịch | Toxic Cloud 5 + Area | clouds follow the densest cluster; a poisoned kill spreads poison to 3 neighbours |
| **Singularity** / Điểm kỳ dị | Gravity Well 5 + Cooldown | a slow, permanent vortex drifts through the horde |
| **Fortress** / Pháo đài | Sentry Turret 5 + Max Health Up | 2 turrets that fire rockets |
| **Meteor Storm** / Mưa sao băng | Meteor 5 + Damage Up | 5 meteors in a line every 12 s |
| **Iron Maiden** / Giáp gai | Thorn Aura 5 + Kinetic Shield | shield breaks reflect a shockwave; aura damage ×2 |

### Result

- A run offers **32 cards** (was 19): 8 stats, 2 signature, 15 powers, 7 universal. Plus **11 evolutions** (was 6).
- New build families: **gun build** (pierce + ricochet + split + crit), **poison**, **control** (well + frost + aura),
  **summon** (drone + turret), **sustain / tank** (siphon + thorns + shield + health).
- Every stat card now also unlocks at least one evolution.

## Order

| Batch | Content | Size |
|---|---|---|
| 1 | Part A items 1, 2, 4; stats S6–S8; U1, U3, U4, U5 | M, no art |
| 2 | U2 Ricochet, A7 Toxic Cloud, A8 Gravity Well, A9 Thorn Aura | M |
| 3 | Chest pickup + Part A item 3; A10 Sentry Turret, A11 Meteor (Blender); the 5 new evolutions | L |
| 4 | 18 new icons (prompts in the same style as `skill_icon_prompts.md`); re-measure pacing in Sandbox | S |

## Part C — 15 more cards (v3, owner asked 2026-09-29)

| Type | Cards |
|---|---|
| Powers (8) | Storm Cloud (lightning cloud follows you), Ice Shards (6-shard radial slow), Flame Burst (fire cone), Landmines (drop a mine every 3 m), Spinning Axe (arcing piercing axe), War Dog (companion, reuses the DogPup model), Ground Stomp (knockback ring), Time Warp (slows every enemy 50 % for 3 s every 20 s) |
| Gun modifiers (3) | Acid Rounds (hits add a poison stack), Explosive Rounds (20 → 40 % of hits burst 1.2 m), Double Tap (10 → 30 % free extra shot) |
| Universal (2) | Guardian Angel (once per run: fatal hit heals 30 → 70 %), Greed (coin +20 → 60 %, enemies +10 → 30 % HP) |
| Stats (2) | Regeneration (0.4 % HP/s per rank), Luck (+10 % item and chest drops per rank) |
| Evolutions (8) | Supercell, Blizzard, Dragon Breath, Minefield, Axe Storm, Alpha Pack, Earthquake, Time Stop |

Totals after v2 + v3: a run offers **47 cards** (10 stats, 2 signature, 23 powers, 12 universal) and **19 evolutions**.
6 skill slots out of 37 skill cards = 2,324,784 combinations, times 210 ways to pick 4 of 10 stats.

## Part D — skills as rewards (unlock road)

- A new player starts with **14 cards**: Damage, Fire Rate, Move Speed, Max Health, Coin Gain; Chain Lightning, Orbit
  Blades, Drone Buddy, Frost Nova, Boomerang; Execution Round, Piercing Rounds; plus the 2 signature cards of the gun.
- From **LV2 to LV34 each account level unlocks exactly 1 card** (33 cards). An evolution unlocks by itself once both
  of its parts are unlocked. Order and levels: see the canvas board "Unlock road".
- Estimated time at ~44 account XP a minute (not measured): LV10 ≈ 33 min, LV20 ≈ 2.2 h, everything ≈ 6.5 h.
- After a run that levels up, a popup shows the new card and a "Try it now" button; Home shows "Skills 20 / 47".

Owner to decide: approve / cut / rename any row in Parts B and C, and the unlock order in Part D.
