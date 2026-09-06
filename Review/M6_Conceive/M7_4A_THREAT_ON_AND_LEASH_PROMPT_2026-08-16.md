# PHASE: EXECUTE — M7.4a · Switch threat on, and fix the run-away hole

Work in:

```text
D:\Projects\Zombie-War
```

The owner played and found the structural failure of wave spawning in an open world:

> Enemies do not keep coming. A wave spawns, say, 50; if I run away they just chase from one direction,
> and nothing new appears. A player who keeps running plays forever with nothing happening.

That is accurate, and the code confirms three separate causes. This run fixes all three by switching the
threat director on and giving enemies a leash.

Run continuously; do not stop to ask. If capacity runs out, finish what you are inside, leave the game
playable, and say where you stopped.

Follow `CLAUDE.md`, `AGENTS.md`, `Docs/FRAMEWORK.md` and the `billgamecore` skill. BillTween only, never
DOTween.

## 0. Standing rules

```text
NEVER edit Menu.unity or any Assets/_Project/UI/Prefabs/Screens/UI_*.prefab. The HUD prefab is
  UI_Hud.prefab — read a prefab before naming anything inside it.
Every new run-scoped static registers with RunScope on the day it is written.
You never place a grip, muzzle or hand transform by inference.
Play-test from Bootstrap.unity only. Do not stage, commit or push. Do not touch .git.
Disclose every vendor asset that changes, including anything Unity re-serialises on its own.
Never generate or play a spoken/TTS report.
```

## 1. The three causes, verified in code

```text
CAUSE 1 — the wave gate is unsatisfiable in an open world.
  WaveDirector runs: spawn wave -> wait until EVERY zombie is dead -> breather -> next wave.
  A player who outruns the horde never clears the wave, so no further wave ever spawns.

CAUSE 2 — spawn position is a snapshot, not a relationship.
  ZombieSpawner places enemies in a 12-22 m ring band around the player, split into 8 sectors, at the
  moment of spawning. Fan-out is correct at t=0. Once the player runs in one direction, all of them
  end up behind — the one-direction tail the owner described.

CAUSE 3 — nothing is ever recycled.
  A search of ZombieSpawner for despawn / recycle / tooFar / maxDistance / cull returns NOTHING.
  Enemies left far behind stay alive forever: they hold the wave open, they never return to the pool,
  and they cost frame time chasing a player they will never reach.
```

The threat director already exists (`driveSpawning = false`, tiers unit-tested, no HP multipliers). It
solves cause 1 and 2 by design. **It does not solve cause 3 — you must add that.**

---

# TASK 1 — The leash: recycle enemies the player has left behind

This is the piece that does not exist yet, and without it turning the director on just produces an
ever-growing tail.

```text
An enemy beyond a leash distance behind the player is returned to the pool and re-spawned into the
active band around the player instead.
```

Design decisions are yours, but respect these:

- **Never despawn anything the player can see.** Use a distance comfortably beyond the camera, and check
  visibility before recycling. An enemy vanishing on screen is a worse bug than the one being fixed.
- Recycled enemies come back **spread across sectors**, not all in the player's face and not all behind.
  The point is that pressure follows the player from several directions, not one.
- An enemy engaged with the player, or one that is a boss or beacon-spawned, is never recycled.
- Recycling must preserve the alive-count contract that `ZombieManager.AliveCount` and the HUD depend on.
- Pool-recycled enemies must reset cleanly — `StatusCarrier.Clear`, per-target counters, ramp state.
  This is the exact class of leak already fixed twice in this project; do not reintroduce it.

State the leash distance and the recycle rate as `TUNING`, and say how you chose them.

# TASK 2 — Turn the threat director on

```text
Assign the tier rosters on the director yourself — this is a serialized ZombieData field on a prefab
  you already edit. It is NOT an owner task; the previous report wrongly listed it as one.
    tier 0  walkers/runners — the existing production set
    tier 1  ZD_Cacti, ZD_Cactus, ZD_SkeletonMage    (ranged)
    tier 2  ZD_Burrow, ZD_MoleRat                   (burrowers) + elite chance
    tier 3  ZD_SkeletonGiant + the two bosses       (heavy / boss route)
Set driveSpawning = true.
Retire the "wait until every zombie is dead" gate as the driver of progression — pressure is now
  continuous and position-relative, not wave-gated.
```

Keep the event surface alive so `HudController`, `GameplayAudioDirector`, `MissionTracker` and
`RunDirector` keep working. A "wave" becomes an internal pacing beat, not a designed wave. Do not remove
`MissionTracker.ClearWave` or `RunState.SetWave` without a replacement.

If turning it on reveals that the threat formula is wrong in play — cadence too slow, crowd too thin,
tiers arriving too early — **tune it and say what you changed**. It has never run; expect to.

# TASK 3 — The acceptance test, in the owner's own words

This is the scenario that must work at the end:

```text
Run in one direction continuously for at least 60 seconds.
Pressure must be maintained: enemies keep arriving, from more than one direction, at a rate that
  rises with tier. Running away must not empty the world, and must not build an endless tail behind.
Then stand still and fight: pressure must still be coherent, not a sudden dogpile.
```

Report what actually happened, with numbers — alive count over time, spawn rate, how many were recycled,
where they arrived from. If running away still produces a one-direction tail, say so; that is the whole
point of the run.

While you are in play, verify opportunistically the three chains no one has ever observed end to end:
boss death → payout, Relay completion → card offer, magnet drop → sweep. If you cannot, say which.

---

## Decision authority

**Decide yourself, record it:** leash distance, recycle rate and rules, tier roster assignment, cadence
and crowd numbers, any threat-formula tuning the play-test demands (all `TUNING`), and whether anything
must stay flagged off.

**Never decide — record as owner tasks:** any UI prefab or scene edit (name the real prefab and a real
child path), adding/removing/renaming a card, the slot-A grammar, or any owner-locked rule (one weapon
per run, auto-fire, no reload, no manual grenade, 1-of-3 with a ≤30 s pause, 25% / 0% banking).

## Acceptance gates

1. The leash exists: enemies left behind are recycled, never while visible, and re-enter spread across
   sectors.
2. Recycled enemies reset cleanly — statuses, per-target counters and ramp state — proven by test.
3. `driveSpawning = true` with tier rosters assigned by you, not deferred to the owner.
4. The "wait until every zombie is dead" gate no longer governs progression.
5. Running in one direction for 60 s maintains pressure from multiple directions — **verified in play**,
   with numbers.
6. No endless tail: the alive count stays bounded and far-behind enemies return to the pool.
7. `ZombieManager.AliveCount` and the HUD stay coherent; nothing depending on wave events is broken.
8. New run-scoped state registers with `RunScope`, with a test.
9. Existing 679 tests stay green; count reported before → after; new behaviour has new tests.
10. `git status`: zero modified UI prefabs, zero modified scenes, nothing staged. Vendor changes disclosed.
11. `detect_changes()` run and reported.

## Final report format

```text
PHASE: EXECUTE — M7.4a — COMPLETE / PARTIAL (say exactly where you stopped)

Leash: distance, rules, visibility guard, what is never recycled, reset proof
Threat on: rosters assigned, what the wave gate now does, what a "wave" now means
The 60-second run-away test: alive count over time, arrival directions, recycle count — with numbers
Tuning done after seeing it live, and why
The three unobserved chains: which you verified, which you could not
Decisions made under delegated authority, and why
Owner tasks recorded (exact prefab, real child path, element, binding)
Tests: count before → after, new tests, any modified test and exactly why
Vendor assets changed, including anything Unity changed on its own
detect_changes() output
Protected state: UI prefabs, scenes, staged files — git evidence
Files modified — full list
Open risks
M7.4a STATUS: DELIVERED / PARTIAL
BLOCKERS: none / exact blocker
```

Gate 5 is the one that matters. Everything else in this run exists to make that sentence true: the
player runs, and the world keeps coming.

Completion audio policy: do not generate or play a spoken/TTS report. The owner wants only the
project's short completion notification behavior.

## Completion notification

After the task is genuinely complete and verified:

1. Use the `simplifier-vi` skill at Level 1 to reduce the final report to the outcome, verification performed, and any important remaining issue.
2. Rewrite that summary in natural English using no more than 80 words.
3. Run:

   python Tools\notify_done.py "<English summary>" --repeat 2

If the task is blocked or fails, run the same command with a short English explanation of the blocker. Report any notification-script failure in the written response.
