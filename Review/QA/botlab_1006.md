# Bot lab — round 4 balance (2026-10-06)

Tool: `Assets/_Project/Scripts/Runtime/Dev/BotLab.cs` (dev builds only), report `Tools/botlab_report.py`.
The bot plays real runs on the real map without god mode at 4× time: kites away from the crowd,
collects XP while the crowd is a few metres off, pushes for chests and needed heals, walks round Boss
Beacons, takes an evolution when offered, otherwise a new card 70 % of the time (a casual player likes
new toys), claims chests at once and declines revives. Per-second CSVs and one summary line per run
live in `Review/QA/botlab/`.

Profiles: **new** = fresh account, starter Pistol, account level 1. **strong** = Legendary (HK416-class)
at 3 stars, mastery 10, evolved, account level 10.

## What the lab found before the fixes (base_new, base_strong)

| Finding | Evidence | Rule |
|---|---|---|
| Elites piled up from tier 3: 14–40 alive, every new-player death inside that pile | `base_new_*.csv`, elites column | 2, #30 |
| No chest in most new-player runs (median 0) | `careful_new` summary | 1, #31 |
| A strong build lived the full 25 min; banked 14,744 coin | `base_strong_1` | "everyone dies", #33 |
| From minute 10 a full build kept ~20 enemies alive against a target of 160: one spawn per tick cannot refill | live check at 18:05, tier 14, alive 19 | 1 |
| Coin Gain Up did nothing on 1-coin kills (`Math.Round(1 × 1.1) = 1`) | `RunState.ScaleCoin` | — |

## Changes (round 4)

- **#30 elite cap** — none before 2:30, then 1, +1 every 75 s, max 6, at least 8 s between two elites
  (`ThreatDirector.EliteCapAt`).
- **#31 reward ladder** — first supply crate 0:45, golden zombie 1:00 (×10 health, bursts into a level of
  XP + 40 coin), chests dropped ahead of the player at 2:00 and 4:00 (`RewardLadder`).
- **#33 coin** — kill coin is full for 5 minutes, then ×(5 / minutes); fractions carry, so Coin Gain Up
  counts (`RunState.KillCoin`). Simulated on the lab runs: 6 min unchanged (~750), 25 min strong 14,688 → ~6,350.
- **Late pressure** — from 10:00 enemy damage follows the health curve at the full compound rate, and
  spawn ticks arrive in bursts of 2 → 6 (`ThreatDirector.TimeDamageMultiplier`, `BurstAt`).
- **#32 reward feel** — kill streak labels (15 / 30 / 60 kills in 1.5 s), NEW BEST TIME / NEW HIGH
  SCORE during the run (`RunMoments`).
- **#37 slot-machine chest** — the chest icon spins through other cards, slowing, then lands with a punch.
- Radio situations wired: Kaito when three Boss Beacons are walked past, Mai when a Supply Drop is.

## After (r4_new, 6 runs)

| Metric | Target | Result |
|---|---|---|
| New player survival | 6–8 min, everyone dies | median **6:10** (4:34–9:46), 6/6 died |
| First card | 25–45 s | median 43 s (24–43) |
| Cards by 2:00 | power fantasy early | median 5 |
| Chests | something to walk to | median 5 per run |
| Elites alive | never a pack | max 6 |
| XP pickup | — | median 85 % |
| Coin per minute | flat after 5 min | median 160 |

Strong profile (r4_strong, 2 runs): died at **13:33** and **17:56** (before: alive at the 25:00 cap),
level 30 / 41, 2,979 / 4,046 coin (before: 14,744), 9 / 13 chests. A strong build lives 2–3× a new
player and still dies. Peak alive 171–200 late in these runs: **profile on the device** before trusting
it (backlog Đợt 1 / build check).

## Still open

- #36 reroll / banish needs a level-up mockup (UI_Hud is owner-owned).
- #38 new enemies for minutes 10–12 needs the owner's pick.
- The bot is a proxy, not a player: a device playtest (held) has the last word on feel.
