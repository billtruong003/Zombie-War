# Mode content plan (backlog #28b, #24, #46) — draft for owner approval

Status: **proposal, 2026-10-06**. Nothing here is built. Owner approves or edits, then it goes into the
backlog as tasks. Concept lock (05/10): casual first, a faithful survivor-like, guns + upgrades keep
players; 6 maps; no Mode button on Home (owner 27/09), so every mode is entered from where it belongs.

## V1 modes

| Mode | Entered from | Length | Why it exists |
|---|---|---|---|
| **Endless** (main, built) | Home PLAY | until death, 6–8 min for a new player | the core loop on 6 maps |
| **Gun Trial** (#24) | Arsenal, on a gun you do not own: **TRY 5 MIN** | 5 min, then a result | let players feel a gun before they chase its shards; feeds the shard gacha (3b) |
| **Daily Challenge** (#46) | Daily Ops screen, a card under the four missions | until death, one attempt counts per day | a reason to come back with a twist; a score to beat |

Later, not V1: Boss Rush (after the boss lab, Đợt 5), Campaign (owner: after V1).

## Gun Trial (#24)

- **What:** a 5-minute run on a fixed map with the chosen gun at 1 star, a starter build of 2 cards, no
  meta bonuses. Cannot borrow the gun for Endless (owner 05/10).
- **Why:** the shard gacha only works if players *want* a specific gun; trying it creates that want.
- **How in game:** Arsenal → unowned gun → TRY 5 MIN (free 2 tries a day, then 1 ticket). The run ends at
  5:00 with a result card: kills, the gun's DPS, and **+3 shards of that gun** (first try of the day only).
  The radio: Lukas introduces the gun at the start (new VO line, 2 takes).
- **Rules:** no coin or account XP (it is a test drive), shards only; a death before 5:00 still pays the
  shards.

## Daily Challenge (#46)

- **What:** one map + one modifier per day, the same for every player (seeded by date). Modifiers rotate:
  *Pistols only*, *Elite night* (elite cap ×2), *No healing stations*, *Double horde calls*, *Glass
  cannon* (×2 damage dealt and taken), *Magnet storm* (items ×3).
- **Why:** variety without new content; a daily reason to play beyond missions.
- **How in game:** Daily Ops screen → DAILY CHALLENGE card (today's modifier, your best score). First run of
  the day pays **20 gems + 10 shards of the gun used**; more attempts allowed, only the best score is kept.
  Leaderboard waits for the backend (stop point after Đợt 7); until then it shows your personal best.

## Order of work (after approval)

1. Gun Trial (reuses Endless with a time limit and a fixed loadout): logic, result card, one VO line.
2. Daily Challenge: modifier table (data), seeded pick, Daily Ops card, rewards.
3. Both need mockups first (Arsenal TRY button, trial result card, Daily Ops challenge card).

## Open questions for the owner

1. Gun Trial cost after the 2 free tries: 1 ticket, or ads (rewarded) once the SDK is in?
2. Daily Challenge modifiers: keep the six above, or add/remove?
3. Should the Daily Challenge count toward the 4 Daily Ops (e.g. "play today's challenge" as one op)?
