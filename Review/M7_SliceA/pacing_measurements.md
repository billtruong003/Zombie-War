# M7 Slice A — pacing measurements (editor, 2026-09-25)

All runs: Play from Bootstrap, Hub PLAY, player idle (never moves), first offered card auto-picked.
Numbers are TUNING evidence, not balance claims. Real playtest on device replaces them.

## Before (build of 14/08)

- Starter pistol, idle: dead in ~10 s. Wave 1 put 28 enemies on the field within 0.4 s.

## After Phase 3 (threat uncapped, eased opening, tier 0 = DogPup + Skeleton, XP 25 + (L-1)^1.35 x 12)

| Weapon | First card | HP at 55 s | Death | Peak threat | Level at death |
|---|---|---|---|---|---|
| Starter pistol (48 DPS) | 32 s | 100/100 | 79 s | 0 | 3 |
| FAMAS (measured before the opening fix) | ~18 s | 100/100 at 84 s | 6:29 | 4 (+8% enemy stats) | 8 |

Reading:
- The opening now does what the GDD asks: the first card lands inside 30-45 s and an idle new
  player is not killed before they have learned to move.
- Pressure keeps rising with time, so a player who stands still is ground down (M6 "runs end by
  attrition, not boredom"). An idle pistol player dies once the opening ramp ends (~60-80 s); that is
  intended, since moving and route choice are the core verbs, but it is the first number to check in
  playtest.
- Measured bug fixed on the way: `ZD_Zombie` (100 HP, 10 dmg, fastest at 3.2 m/s, 1 XP) sat in the
  tier-0 roster and out-classed every other opening enemy. It now arrives with tier 1.
