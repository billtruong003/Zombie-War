# PHASE: EXECUTE — M7.3 · Stations, signal language, endless pressure

Work in:

```text
D:\Projects\Zombie-War
```

Combat is now genuinely enjoyable — the owner played it and confirmed. Cards fire, powers land, the run
resets cleanly. What the world still lacks is a **reason to go anywhere**. Right now the player fights
wherever they happen to be standing, and nothing on the map asks them to move.

This run builds that: a visual language for signals, three stations worth walking to, and pressure that
rises through enemy composition instead of inflated health.

Run continuously; do not stop to ask. Decide, record the decision and the reasoning, keep going. If you
run out of capacity, finish the station or system you are inside, leave the project building and
playable, and say exactly where you stopped.

Follow `CLAUDE.md`, `AGENTS.md`, `Docs/FRAMEWORK.md` and the `billgamecore` skill. BillTween only, never
DOTween.

---

## 0. Standing rules

```text
NEVER edit Menu.unity or any Assets/_Project/UI/Prefabs/Screens/UI_*.prefab (the HUD prefab is
  UI_Hud.prefab — note the name; a previous report guessed "UI_HudScreen" and sent the owner to a file
  that does not exist. Read prefabs before naming their contents.)
You never place a grip, muzzle or hand transform by inference.
Play-test from Bootstrap.unity only. Do not stage, commit or push. Do not touch .git.
Disclose every vendor asset that changes, including anything Unity re-serialises on its own.
Never generate or play a spoken/TTS report.
```

## 1. The rule that did not exist before this milestone

`RunScope.ResetAll()` now exists, called from `RunState.Begin`. **Every run-scoped piece of state you
create in this milestone registers with it, on the day you create it.** Station progress, claimed
anchors, boss-alive flags, threat tier, spawned station instances — all of it.

The skill build and the pickup registry both leaked across runs because they were written before that
path existed. Nothing written from now on has that excuse.

## 2. Verified starting state

```text
Combat: 23 cards live, powers proc and damage, guardrails hold (≤2 procs/s, ≤6 arcs, ≤2 explosions)
Reset: RunScope.ResetAll() from RunState.Begin; 7 reset tests, all previously failing, now green
Arsenal: 54 weapons, 24 playable in the Hub; 26 pending owner grip authoring
Tests: 640 passing
Enemies: VAT-baked (ENM_*_VAT). 11 of 16 enemy assets are unused in production.
Bosses available: ENM_CactusBoss_VAT, ENM_MoleRatKing_VAT, ENM_SkeletonGiant_VAT
Props: Assets/Synty/PolygonDarkFantasy — 9 statues (incl. gargoyle), SM_Prop_Altar_Table_01,
  SM_Prop_Brazier_01, 32 pillars, SM_Prop_Chest_01, SM_Prop_Barrel_01/Open, SM_Prop_Crate_01/02
FX: Assets/ThirdParty/Epic Toon FX/Prefabs/{Combat,Environment,Interactive,Misc}
  plus the project's own pooled chain-arc renderer and M_SkillChainArc / M_SkillStatusMark
```

**Not in scope, deliberately deferred** — do not spend this run on them: VFX distinguishability polish
between skills, cues for the four ramp cards (Run & Gun, Bullet Hose, Focus Fire, Heavy Pressure), and
the HUD power-cooldown row that waits on owner prefab work.

---

# PART 1 — The World Signal Language

Build the visual contract **before** any station, and build it prop-independent. The prop carries the
mass; the language carries the meaning. Any prop the owner supplies later gets dressed by it.

```text
ground ring          the station's footprint, and where the player must stand if it needs holding
vertical beam        visible from off-screen; this is what makes the player turn and walk
floating icon        what kind of station this is, readable at a glance
emissive accent      idle / active / completed / on cooldown, distinguishable at gameplay distance
progress indicator   in world space on the station, never in a HUD corner
one colour per type  Relay, Cache, Beacon each own a colour and keep it everywhere
```

Assemble from Epic Toon FX. Pooled, no runtime material instances, no per-frame allocation.

**The readability test the owner will actually apply:** stand in a fight, look at the screen, and know
without thinking which station that is and whether it is done. If two station types read the same, the
language has failed regardless of what the code does.

# PART 2 — Three stations

Bodies — choose from what is on disk, dress them with the language, and produce a review sheet:

```text
Signal Relay   a pillar / obelisk / statue — needs vertical mass, readable from distance
Supply Cache   an existing container/crate, or SM_Prop_Chest_01
Boss Beacon    a gargoyle statue or SM_Prop_Altar_Table_01 — ominous, clearly not lootable
Breakable      SM_Prop_Barrel_01 / SM_Prop_Crate_01
```

Every Synty prop entering the game passes the same toon material/outline contract the weapons do.
Convert; never ship raw Synty materials.

Each station needs its full contract: deterministic anchor-based spawn, ownership, the signal that
announces it, interaction and duration, cancel rule, risk and reward, cooldown and repeatability,
persistence, how it responds to enemy pressure, and what happens when it fails.

```text
Signal Relay   hold a compact zone while pressure continues → a 1-of-3 card offer
Supply Cache   pay Coin or a short hold → supplies; this is what finally gives Coin an in-run use
Boss Beacon    opt in, fight a chasing boss from the existing VAT roster → the run's biggest reward
```

Hard constraints:

```text
Anchors are deterministic and global — never chunk-local random
Recycling a visual chunk must not reset an objective, duplicate loot or respawn a destroyed station
No orphan boss when its chunk unloads
No free chest
At most one major encounter owns the player's attention at a time
Boss Beacon uses existing VAT bosses only — no new boss is authored, and Kaiju stays out
  (it ships with zero animation clips; making it fight is its own supervised milestone)
```

# PART 3 — Endless pressure

## 3.1 Remove the wave-clear auto-collect

`PickupManager.cs:141` collects everything on `WaveClearedEvent`. An endless world has no reliable wave
boundary, and auto-collecting removes the reason to move toward loot at all.

Remove it. Then check what that does to the feel: coins now have to be walked to. The magnet pickup
exists and becomes meaningful. If removal makes collection tedious rather than interesting, say so —
that is a design finding, and the fix is magnet tuning, not putting auto-collect back.

## 3.2 The threat model

`ThreatTier = base + objectiveProgress + distanceBand + boundedTimePressure`

```text
Tier 0   walkers and runners; room to learn the route and the controls
Tier 1   one specialist pressure — ranged or pouncer
Tier 2   mixed specialists, tighter spawn cadence, elite chance
Tier 3   recovery-window enemies, heavy pressure, the boss route
```

Composition changes before stats. Eleven of the sixteen enemy assets are unused — ranged, burrowers,
elites and bosses are all sitting there. Reach for them before touching HP multipliers. Each tier
introduces at most one new tactical question. Time pressure is capped, so a losing player is encouraged
to finish rather than made mathematically doomed.

---

## 3. Decision authority

**Decide yourself, record it:**

```text
Which prop serves which station, and how the language dresses it
Colours, beam heights, ring sizes, icon shapes, timings
Anchor spacing, station frequency, hold durations, Coin prices, threat curve numbers (all TUNING)
Which existing boss serves the first Boss Beacon
Toon conversion choices for Synty props
Magnet tuning after auto-collect is removed
```

**Never decide — record as owner tasks:**

```text
Any UI prefab or scene edit (specify it precisely instead: prefab name, real child path, element)
Adding, removing or renaming a card, or changing what one does
The slot-A signature-card grammar
Owner-locked rules: one weapon per run, auto-fire, no reload, no manual grenade,
  1-of-3 with a ≤30 s pause, death banks 25% Coin, manual abandon banks 0%
```

## 4. Acceptance gates

1. The signal language exists independently of any prop, and two station types are distinguishable at
   gameplay camera distance. Show it — a capture, not a claim.
2. Three stations implemented with full contracts and deterministic global anchors.
3. Every new run-scoped static is registered with `RunScope`, with a test proving it resets.
4. Chunk recycling cannot reset, duplicate or orphan station state; no free chest; no orphan boss.
5. `CollectAll()` on `WaveClearedEvent` removed, and the effect on collection feel reported honestly.
6. Threat model changes composition before stats, and uses the unused enemy roster.
7. Synty props pass the toon material/outline contract.
8. Performance holds: pooled everything, zero runtime material instances, 0 alloc/frame steady state,
   station logic does not add a per-frame cost that scales with enemy count.
9. Existing 640 tests stay green; new behaviour has new tests; count reported before → after.
10. Bootstrap play-test: reach a Relay, complete it, take its card; spend Coin at a Cache; trigger a
    Beacon and fight the boss. Report what you could and could not verify without a human driving.
11. `git status`: zero modified UI prefabs, zero modified scenes, nothing staged. Vendor changes disclosed.
12. `detect_changes()` run and reported.

## 5. Final report format

```text
PHASE: EXECUTE — M7.3 STATIONS — COMPLETE / PARTIAL (say exactly where you stopped)

Signal language: the contract, and evidence two station types read differently
Stations: one section each — body chosen and why, full contract, what happens on failure
RunScope: every new run-scoped static, and the test proving it resets
Anchors and persistence: how recycling is survived, how orphans are prevented
Auto-collect removal: what changed in feel, and what magnet tuning followed
Threat model: tiers, which enemies each tier introduces, what stays capped
Decisions made under delegated authority, and why
Owner tasks recorded (exact prefab, real child path, element, binding)
Tests: count before → after, new tests, any modified test and exactly why
Play-test: what you verified, what needs a human
Performance numbers under load
Vendor assets changed, including anything Unity changed on its own
detect_changes() output
Protected state: UI prefabs, scenes, staged files — git evidence
Files modified — full list
Open risks
M7.3 STATUS: DELIVERED / PARTIAL
BLOCKERS: none / exact blocker
```

The owner will judge this by playing it: do they leave a safe fight to go reach a beam on the horizon?
If the honest answer after your own testing is "probably not", say so — that is the most useful finding
this milestone can produce.

Completion audio policy: do not generate or play a spoken/TTS report. The owner wants only the
project's short completion notification behavior.

## Completion notification

After the task is genuinely complete and verified:

1. Use the `simplifier-vi` skill at Level 1 to reduce the final report to the outcome, verification performed, and any important remaining issue.
2. Rewrite that summary in natural English using no more than 80 words.
3. Run:

   python Tools\notify_done.py "<English summary>" --repeat 2

If the task is blocked or fails, run the same command with a short English explanation of the blocker. Report any notification-script failure in the written response.
