# PHASE: EXECUTE — M7.3c · Polish first, boss fix, then replace waves with threat

Work in:

```text
D:\Projects\Zombie-War
```

**Order matters in this prompt and it is not negotiable.** Two items have now slipped twice and three
times respectively, because they kept being placed last. They are first here. Do not reorder them, and
do not skip ahead to the threat model because it is more interesting.

```text
1. Signal polish + an in-world capture   (deferred twice)
2. Synty props through the toon contract (deferred three times — gate 7)
3. Boss Beacon: the boss stands at the pillar and never chases
4. Threat model — begin replacing wave pacing
```

Run continuously; do not stop to ask. If capacity runs out, finish the item you are inside and say where
you stopped. Items 1–3 are small; if you cannot reach item 4 at all, that is an acceptable outcome and
far better than a half-built threat director.

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

---

# ITEM 1 — Signal polish and a real capture

From the owner-side review of `Review/M7_3_Stations/signal_language.png`, uncorrected for two runs:

```text
TEAL BEAM IS SLANTED — leans roughly 30° and passes through the edge of its ring instead of rising
  from the centre. Only the amber beam is truly vertical.
COMPLETED STATE READS AS BROKEN — the crimson signal is nearly invisible: faint ring, a short diagonal
  dash for a beam, and a hexagon icon floating detached above and to the left. A finished station
  should look finished, not unfinished.
ICONS TOO SMALL — you flagged this yourself; they do not carry meaning next to the beam.
THE CAPTURE WAS AGAINST EMPTY BLACK — no ground, no props, no enemies. Legible on black is not
  legible in the game.
```

Fix all four. The new capture must be taken **in the real world**, with ground, props and enemies in
frame, at gameplay camera distance, showing at least two station types plus a completed one. That
capture is the deliverable; a claim that it looks better is not.

# ITEM 2 — Synty props through the toon contract

Gate 7 has been unmet for three runs. Station bodies are attached but never converted, so they ship raw
Synty materials in a toon world: `SM_Bld_Base_Pillar_01`, `SM_Prop_Chest_01`, `SM_Prop_Altar_Table_01`,
and the barrel/crate bodies.

Run them through the same material and outline contract the weapons use. Copy vendor materials into
project ownership first; never edit a vendor `.mat` in place. Show the before/after in the same in-world
capture from item 1.

# ITEM 3 — The Boss Beacon boss stands at the pillar

The owner's report: the boss spawns, then stands at the column and does nothing. Then the beacon never
produces another.

## 3.1 The likely cause — verify before fixing

`StationDirector.SpawnBossFor` does this:

```csharp
spawner.EnsureRegistered(data, 1);
spawner.BeginBatch();
var boss = spawner.Spawn(data);              // line 208 — the WAVE spawner places it by its own band logic
...
boss.transform.position = station.transform.position + Vector3.forward * 3f;   // line 215 — then teleported
```

Two suspects:

```text
TELEPORT AFTER SPAWN — Spawn(data) goes through ZombieSpawner.Spawn(data, SpawnBand.Normal), which
  positions the enemy by its own sector/band logic and initialises its state there. Moving the transform
  afterwards can leave steering, target or home state anchored to the original point. Place the boss
  through the spawner's own positioning, or re-initialise its steering after the move — do not just
  set transform.position and hope.
UNCLOSED BATCH — BeginBatch() is called with no matching end. Check whether the batch must be committed
  for the spawned enemy to become active.
```

Diagnose which it is. Then confirm **in play**: the boss spawns, chases the player, can be damaged and
killed, and `NotifyBossKilled` pays out. None of that has ever been observed.

## 3.2 A consumed beacon must say so

Boss Beacon is once-only by design, so "it never spawns another" is correct behaviour — but there is no
feedback saying the station is spent, so it reads as broken. Give a completed beacon the same clear
completed state item 1 is fixing, and make walking back into a spent ring do something legible rather
than nothing.

# ITEM 4 — Begin replacing waves with the threat model

The owner played and said, correctly, that the game still runs on waves. It does:
`WaveDirector`, `WavePressurePlan` and `ZombieSpawner` (all under `Runtime/Gameplay/Waves/`) still drive
every spawn, and `WaveStartedEvent` / `WaveClearedEvent` feed `HudController`, `GameplayAudioDirector`,
`MissionTracker` and `RunDirector` (which writes the wave number into `RunState`).

This contradicts the locked M6 direction — one endless world, no wave structure — and it is the half of
M7.3 that was never started.

## 4.1 What to build

```text
ThreatTier = base + objectiveProgress + distanceBand + boundedTimePressure

Tier 0   walkers and runners
Tier 1   one specialist — ranged or pouncer
Tier 2   mixed specialists, tighter cadence, elite chance
Tier 3   recovery-window enemies, heavy pressure, boss route
```

Composition changes before stats. Eleven of the sixteen enemy assets are unused — ranged, burrowers,
elites and bosses are all sitting there. Each tier introduces at most one new tactical question. Time
pressure stays capped so a losing player is encouraged to finish, not doomed.

## 4.2 The migration constraint that decides your approach

`HudController` subscribes to `WaveStartedEvent` and displays a wave number. **The HUD is an owner-locked
prefab.** So you may not simply delete the wave events and leave the HUD dead.

Take the path that keeps the game playable at every step:

```text
Replace what DRIVES spawning — the threat director decides composition and cadence instead of
  WavePressurePlan.
Keep the event surface alive so HUD, audio, MissionTracker and RunDirector keep working, even if what
  a "wave" means becomes an internal pacing beat rather than a designed wave.
Then write the owner task precisely: which HUD element shows a wave number, what should replace it
  (threat tier, distance band, or nothing), and what the code already exposes to bind to.
```

Do not remove `MissionTracker`'s ClearWave metric or `RunState.SetWave` without a replacement — a
dangling mission metric is worse than a stale one. List everything that still depends on the wave
concept, and say which of it is now vestigial.

If you reach item 4 with little capacity, **build the threat director and leave it disabled behind a
flag** rather than half-migrating the spawner. A game that still uses waves is fine; a game that spawns
nothing is not.

---

## Decision authority

**Decide yourself, record it:** all signal proportions, colours, icon sizes; toon conversion choices;
how the boss is placed and re-initialised; threat tiers, cadence and composition numbers (all TUNING);
whether the threat director ships enabled or behind a flag.

**Never decide — record as owner tasks:** any UI prefab or scene edit (name the real prefab and a real
child path), adding/removing/renaming a card, the slot-A grammar, or any owner-locked rule (one weapon
per run, auto-fire, no reload, no manual grenade, 1-of-3 with a ≤30 s pause, 25% / 0% banking).

## Acceptance gates

1. Beam alignment fixed; the completed state reads as finished; icons sized to carry meaning.
2. A new capture exists **in the real world** with ground, props and enemies in frame, showing at least
   two station types and a completed one.
3. Synty station props pass the toon material/outline contract; no vendor `.mat` edited in place;
   before/after visible.
4. The boss-stands-still cause is diagnosed by cause, fixed, and **confirmed in play**: it spawns,
   chases, takes damage, dies, and pays out.
5. A spent beacon is legibly spent.
6. Threat director exists — enabled or behind a flag — with tiers driving composition, not HP.
7. Everything still depending on the wave concept is listed, with what is now vestigial.
8. The game remains playable end to end; no system left spawning nothing.
9. Any new run-scoped state registers with `RunScope`, with a test.
10. Existing 664 tests stay green; count reported before → after; new behaviour has new tests.
11. `git status`: zero modified UI prefabs, zero modified scenes, nothing staged. Vendor changes disclosed.
12. `detect_changes()` run and reported.

## Final report format

```text
PHASE: EXECUTE — M7.3c — COMPLETE / PARTIAL (say exactly where you stopped)

Item 1 signal polish: what changed, with the in-world capture
Item 2 Synty toon: what was converted, before/after
Item 3 boss: the real cause, the fix, and the in-play confirmation of spawn → chase → death → payout
Item 4 threat: what exists, enabled or flagged, what still depends on waves and what is vestigial
Decisions made under delegated authority, and why
Owner tasks recorded (exact prefab, real child path, element, binding)
Tests: count before → after, new tests, any modified test and exactly why
Play-test: what you verified in play this time, and what still needs a human
Vendor assets changed, including anything Unity changed on its own
detect_changes() output
Protected state: UI prefabs, scenes, staged files — git evidence
Files modified — full list
Open risks
M7.3 STATUS: DELIVERED / PARTIAL
BLOCKERS: none / exact blocker
```

Items 1 and 2 are small and have been carried for three runs. If this report comes back with them
deferred again in favour of item 4, the run has failed its own brief regardless of what else it built.

Completion audio policy: do not generate or play a spoken/TTS report. The owner wants only the
project's short completion notification behavior.

## Completion notification

After the task is genuinely complete and verified:

1. Use the `simplifier-vi` skill at Level 1 to reduce the final report to the outcome, verification performed, and any important remaining issue.
2. Rewrite that summary in natural English using no more than 80 words.
3. Run:

   python Tools\notify_done.py "<English summary>" --repeat 2

If the task is blocked or fails, run the same command with a short English explanation of the blocker. Report any notification-script failure in the written response.
