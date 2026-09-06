# PHASE: EXECUTE — M7.3b · Station hold bug, magnet pickup, signal polish, then threat

Work in:

```text
D:\Projects\Zombie-War
```

The owner played the station build. Stations spawn and are visible — that part works. Two things came
back, one a bug and one a design decision, and they are the front of this run.

```text
1. BUG — standing in a Signal Relay ring shows no progress. The hold never advances.
2. Collection without auto-collect is annoying. The owner's decision: coins stay on the ground,
   and a MAGNET PICKUP drops — collecting it sucks everything in. Do NOT restore blanket auto-collect.
```

Then the polish and the unfinished parts from last run, in order.

Run continuously; do not stop to ask. Decide, record it, keep going. If capacity runs out, finish the
item you are inside, leave the project building and playable, and say where you stopped.

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

# TASK 1 — The relay hold never progresses (highest priority)

The whole station system is unplayable until this works. The logic reads correctly on inspection —
`StationDirector.Update` resolves the player from `PlayerMovement.Instance`, calls
`Station.Tick(dt, playerPosition)`, which tests `Signal.Contains(playerPosition)`, accumulates
`_progressSeconds` and calls `Signal.SetProgress`. So the failure is in a detail, not the design.

Four candidates, in the order I would check them. **Diagnose the actual cause; do not fix all four
blindly and hope.**

```text
1. RADIUS MISMATCH — the most likely. WorldSignal serialises ringRadius = 3.5f, while
   Station.Setup calls signal.Configure(anchor.kind, radius) with its own radius (3.5 normal,
   4.5 for a beacon). If Configure does not assign that into ringRadius, the ring the player SEES
   and the ring the code TESTS are different sizes. The player stands inside the drawn ring and is
   outside the logical one.
2. Y-AXIS — Contains may measure full 3D distance while the station sits on the ground plane and the
   player's transform origin is at capsule centre. The check should be planar.
3. PROGRESS NEVER DRAWN — SetProgress may update _progress01 without rebuilding the progress
   LineRenderer, so the hold advances invisibly and completes with no feedback.
4. NOT TICKED — the director may only tick claimed/active stations, so the one the player is standing
   in is never ticked at all.
```

Whatever the cause, the fix must come with a test that **fails first**: place a player position inside
the drawn ring, tick, and assert progress advanced. Say in the report that you saw it fail.

Then verify it in play, not only in a test: stand in the ring, watch the progress ring fill, receive the
card offer. That sequence has never once been observed working.

# TASK 2 — The magnet pickup

The owner's design, and it is consistent with the locked M6 pickup taxonomy: coins are not swept up for
free at a wave boundary, but a magnet drop rewards the player with a big satisfying sweep.

```text
Normal state      dropped coins and gems lie where they fell and are walked over
Magnet pickup     drops occasionally; collecting it pulls in everything currently on the ground
```

Most of this exists already — use it, do not rebuild it:

```text
Pickup.cs already has magnetSpeed, magnetAcceleration and a bob-before-magnetable delay
PickupManager already drives magnet attraction as a distance check
CollectAll() is still public and unsubscribed — this is exactly its use
PickupEffect is currently { Currency, Health, Bomb } and needs the new kind
```

Specify and implement: drop rule and rarity, whether the sweep is instant or a travelling pull, whether
it is global or generous-radius, its feedback, and its pooling. Prefer the version that reads best: a
visible mass of coins flying to the player is the point of the pickup.

Art: choose from `Assets/Icons` and Epic Toon FX; a pickup the player does not recognise is a pickup
they walk past. Pooled, no runtime material instances.

Report the drop rate you chose as `TUNING`, and say how a player who never finds a magnet still gets
their coins — the fallback matters more than the magnet.

# TASK 3 — Signal polish, from the owner-side review of your own capture

I read `Review/M7_3_Stations/signal_language.png` myself. Colour separation works. Three problems your
report did not catch:

```text
TEAL BEAM IS SLANTED and off-centre — it leans roughly 30° and passes through the edge of its ring
  instead of rising from the middle. Only the amber one is a true vertical beam.
CRIMSON IS NEARLY INVISIBLE — the ring is very faint, the beam is a short diagonal dash, and its
  hexagon icon floats detached above and to the left. It reads as "broken", not "completed".
THE CAPTURE IS AGAINST EMPTY BLACK — no ground, no enemies, no foliage. Legible on black is not
  legible in the game. Gate 1 asked for gameplay camera distance and this does not answer it.
```

Fix the beam alignment and the completed state, do the icon size pass you already flagged, and re-shoot
the capture **in the actual world** with ground, props and enemies in frame. That capture is the
evidence; a claim is not.

# TASK 4 — Synty props through the toon contract

Gate 7 was unmet last run: station bodies are attached but never converted, so they ship raw Synty
materials in a toon world. Run them through the same material/outline contract the weapons use —
`SM_Bld_Base_Pillar_01`, `SM_Prop_Chest_01`, `SM_Prop_Altar_Table_01`, and the barrel/crate bodies.

Copy vendor materials into project ownership before converting; never edit a vendor `.mat` in place.

# TASK 5 — The threat model (only if the four above are genuinely done)

`ThreatTier = base + objectiveProgress + distanceBand + boundedTimePressure`

```text
Tier 0   walkers and runners
Tier 1   one specialist — ranged or pouncer
Tier 2   mixed specialists, tighter cadence, elite chance
Tier 3   recovery-window enemies, heavy pressure, boss route
```

Composition before stats. Eleven of the sixteen enemy assets are unused — ranged, burrowers, elites and
bosses are all available. Each tier adds at most one new tactical question. Time pressure stays capped.

**If you reach this task with little capacity left, do not start it.** A half-built threat director is
worse than none, and it has already waited one milestone; it can wait another.

---

## Decision authority

**Decide yourself, record it:** magnet drop rate, sweep style and radius, all signal proportions and
colours, toon conversion choices, threat numbers (all TUNING), where new state registers with RunScope.

**Never decide — record as owner tasks:** any UI prefab or scene edit (name the real prefab and a real
child path), adding/removing/renaming a card, the slot-A grammar, or any owner-locked rule (one weapon
per run, auto-fire, no reload, no manual grenade, 1-of-3 with a ≤30 s pause, 25% / 0% banking).

## Acceptance gates

1. The relay hold bug is diagnosed by **cause**, stated plainly, with a test that failed before the fix.
2. Standing in a relay ring visibly fills the progress ring and delivers the card offer — verified in
   play, not only in a test.
3. The magnet pickup exists, drops, and sweeps; blanket auto-collect on `WaveClearedEvent` stays removed.
4. Coins remain collectable without a magnet; the fallback is stated.
5. Beam alignment fixed; the completed state is clearly "done", not "broken"; icons sized to carry meaning.
6. A new capture exists **in the real world with ground and enemies in frame**, and two station types
   are distinguishable in it.
7. Synty station props pass the toon material/outline contract; no vendor `.mat` edited in place.
8. Any new run-scoped state registers with `RunScope`, with a test.
9. Threat model either delivered properly or **not started** — say which, plainly.
10. Existing 657 tests stay green; count reported before → after; new behaviour has new tests.
11. `git status`: zero modified UI prefabs, zero modified scenes, nothing staged. Vendor changes disclosed.
12. `detect_changes()` run and reported.

## Final report format

```text
PHASE: EXECUTE — M7.3b — COMPLETE / PARTIAL (say exactly where you stopped)

Relay bug: the real cause, how you found it, the test that failed first, and the in-play confirmation
Magnet: drop rule, sweep behaviour, feedback, fallback for a player who never finds one
Signal polish: beam, completed state, icon sizing — with the new in-world capture
Synty props: what was converted and how
Threat model: delivered, or not started and why
Decisions made under delegated authority, and why
Owner tasks recorded
Tests: count before → after, new tests, any modified test and exactly why
Play-test: what you verified in play this time
Vendor assets changed, including anything Unity changed on its own
detect_changes() output
Protected state: UI prefabs, scenes, staged files — git evidence
Files modified — full list
Open risks
M7.3 STATUS: DELIVERED / PARTIAL
BLOCKERS: none / exact blocker
```

The owner has now played three builds in a row and reported real problems each time. That loop is worth
more than any test suite — so the play-test section is not a formality. If you cannot verify something
in play, say which thing and why.

Completion audio policy: do not generate or play a spoken/TTS report. The owner wants only the
project's short completion notification behavior.

## Completion notification

After the task is genuinely complete and verified:

1. Use the `simplifier-vi` skill at Level 1 to reduce the final report to the outcome, verification performed, and any important remaining issue.
2. Rewrite that summary in natural English using no more than 80 words.
3. Run:

   python Tools\notify_done.py "<English summary>" --repeat 2

If the task is blocked or fails, run the same command with a short English explanation of the blocker. Report any notification-script failure in the written response.
