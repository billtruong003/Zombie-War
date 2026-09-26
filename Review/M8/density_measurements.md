# M8 step A — enemy density (2026-09-26)

Owner direction: "more enemies instead of stronger enemies". Measured in the editor through MCP
(Map_Level1, starter pistol `weapon.sidearm.pistol_a`: 12 dmg × 4/s, 8 m range; cards auto-picked).

## Horde cost (HordeStressTest, PlayerLoop CPU ms, editor)

| Alive | PlayerLoop ms | Note |
|---|---|---|
| 30 | 37.5 | editor overhead dominates |
| 100 | 35.0 | |
| 200 | 34.8 | no measurable growth with crowd size |

The horde is not the CPU bottleneck in the editor. **Still to confirm on a mid-range Android device.**
GC reads ~2.5 MB/s in the editor; this includes editor allocations and must be re-read in a
development player build.

## What changed

| Knob | Before | After |
|---|---|---|
| Crowd target | 18 + 8/tier, cap 60 | 40 + 14/tier, cap 160 |
| Arrival interval | 0.8 s, floor 0.2 | 0.35 s, floor 0.08, and ×0.2 when the crowd is empty (catch-up) |
| Opening | 60 s, starts at 20 % | 25 s, starts at 35 % |
| Enemy stat growth | +8 %/tier from tier 4 | +4 %/tier from tier 6 |
| Horde surge | none | every 75 s after the opening, 10 s, crowd ×1.5 (cap 200), arrivals ×2.9, from every bearing; HUD "HORDE!" + stinger |
| Non-boss enemy HP / damage | e.g. Pup 40/6, Zombie 100/10 | about half HP, two-thirds damage (Pup 20/4, Zombie 50/7) |
| Pursuit | none | far enemies move faster: ×1 inside 10 m → ×2.2 at 25 m |
| Tail recycle | only beyond 55 m | also off-screen enemies > 16 m behind a moving player, so they re-spawn ahead |

## Probes (starter pistol)

| Probe | Before | After |
|---|---|---|
| Idle, first card | 32 s | 13 s |
| Idle, death | 79 s (crowd ≈ 5-7) | 37 s (crowd 39) — standing still is punished |
| Circle-kiting, no dodging | not measured | **before tail recycle:** 120 enemies trailed behind, 0 damage, stuck at Lv 4 for 5 min |
| Circle-kiting, after tail recycle | — | dies 4:21, 331 kills, Lv 8, 7 cards; ~1.3 kills/s throughout, third surge is the killer |

Open item for step B: levels arrive slowly after Lv 5 (Lv 8 at 4 min). Re-tune the XP curve together
with the skill overhaul, then re-measure.

## Follow-up: loot at horde density

A surge screenshot showed the ground carpeted with coins: each normal kill split its 2-4 coin reward
into 2-4 objects, and there are ~3x the kills. Now a normal enemy drops one coin carrying its full
value (elites still burst), above 60 live pickups a new coin merges into the nearest resting coin
within 4 m, and the walk-over magnet radius is 4.5 m (was 3.5). Coin value per kill is unchanged, so
**coin income per run is roughly 3x the Slice A figure — re-tune shop prices after playtest.**
The surge pill no longer overflows: during a surge it reads `1:47 · HORDE · Lv 7` in red.
Screenshot: `density_surge.png` (second surge, 78 alive, 60 pickups).
