# Boss lab — Titan (backlog #39, 2026-10-06) — for owner approval

How to run it: Play from Bootstrap, start a run, then the cheat `zw.boss.lab "14 solid"` (radius in
metres, `solid` or `shock`); `zw.boss.end` removes it. Kit: `ZombieWar/Dev/Build Boss Lab`.
Code: `Gameplay/Bosses/` (TitanBoss, GroundTelegraph, BossArena, BossBarView), `Dev/BossLab.cs`.
Pictures: `Review/QA/bosslab/`.

## The Titan

- Model: Synty PolygonKaiju **Kaiju_04** (a giant gorilla), 7.9k triangles, ×3 scale ≈ 6 m tall.
- Animations: Malbers humanoid idle / run / death; FreeFighter re-imported as humanoid for the attacks
  (axe kick = slam, charge fist = charge, super blast = roar). The FreeFighter folder's import settings
  were changed to Humanoid for this (that folder is not in git yet).
- Wears the game's character toon shader (Synty's own shader draws nothing in this URP setup).
- Colours: five texture variants, `titan_colours_ABCDE.png`. A (black) blends into the ground from
  above; **C (white) and E (gold) read best**. Proposal: each Titan wave a colour (5 / 10 / 15 / 20 min).

## Attacks (all telegraphed, genre rule 8)

| Attack | Telegraph | Effect | Picture |
|---|---|---|---|
| Ground slam | red circle 5.5 m, fills over 1.3 s | 30 damage inside, shake | `attack_slam.png` |
| Charge | red strip 3.2 × 14 m, fills over 1.1 s | rushes at 13 m/s, 25 damage on contact | `attack_charge.png` |
| Call | roar | 8 crowd enemies around it, every 18 s | `attack_call.png` |

Walks at 3.2 m/s (player 5, rule 3). Enrages after 90 s: cadence ×0.6, speed ×1.25, red tint,
"ENRAGED" on its bar. Death drops a chest. Lab health 6,000.

## Arena (choose one) — pictures `*_game.png` (in-game) and `*_top.png` (from above)

| Option | Feel |
|---|---|
| **10 m, solid wall** | tight: the wall is on screen, the Titan is always close; little room to dodge the charge |
| **14 m, solid wall** | the charge strip fits with room to sidestep; the wall shows at the screen edge |
| **18 m, shock wall** | roomy; touching the wall knocks you back inside and stings (8 damage) |

Walls are on the NavObstacle layer: they stop the player and the crowd, bullets pass through.

## Found in the lab (needs a decision)

1. **The crowd piles up outside the wall** (`14_solid_top.png`): the walls keep the normal horde out,
   so they line the outside. Options: (a) pause crowd spawns during the Titan fight, only its called
   minions; (b) let the crowd spawn inside the ring; (c) keep it outside as a "cage match" look.
2. **Minions**: the call uses the map's crowd enemies. Alternative: a fixed escort (e.g. the map's elite
   at most 2).
3. **Skills do not hit the Titan yet** (most skills look only at the normal enemy list); the gun does.
   Wiring it into the skills comes with the real integration (#41–#43) after approval.
4. **Boss bar** is a lab overlay below the HUD top; its final place goes with the approved HUD
   milestone bar (mockup U1).

## After approval (#40–#44)

Titan joins the run at 5 / 10 / 15 / 20 min (stronger each time, its own colour), the HUD milestone
bar shows it coming, the arena rises around the player with a short entrance, skills hit it, and the
Boss Beacon's boss gets the red ring and bar.
