# PHASE: EXECUTE — M7.3d · Console hygiene, then replace waves with threat

Work in:

```text
D:\Projects\Zombie-War
```

The owner ran a real play session and sent the console. It carried one confirmation and four defects.
Fix the four first — they are small, they have been shipping for a while, and one of them is corrupting
camera shake every frame. Then build the threat model, which is the last unfinished piece of M7.3 and
the owner's own complaint: the game still runs on waves.

**Order is not negotiable. Part 1 before Part 2.** Small items placed last have been skipped three runs
running in this project.

Run continuously; do not stop to ask. If capacity runs out, finish the item you are inside and say where
you stopped.

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

## 1. Confirmed working — do not re-litigate

From the owner's live session:

```text
[StationDirector] Boss Beacon spawned 'ZD_SkeletonGiant'.
```

The beacon armed and spawned in a real run — and it placed the very boss that previously failed every
band. The roster-walk fix works. Leave it alone.

Still unobserved by anyone, and worth confirming opportunistically if a play-test allows: boss death →
`NotifyBossKilled` payout, Relay completion → card offer, magnet drop → sweep.

---

# PART 1 — Four defects from the console

## 1.1 The player carries a stray Plane with a concave MeshCollider

```text
Concave Mesh Colliders are not supported when used with dynamic Rigidbody GameObjects.
Scene hierarchy path "Player(Clone)/Plane", Mesh "Plane" from Library/unity default resources
```

Verified on disk: `Assets/_Project/Prefabs/Player.prefab` has a child named `Plane` (line 586) carrying
a `MeshCollider` (line 616). It is Unity's built-in default Plane — debris from earlier work, not a
gameplay object.

Find out what it is actually for before deleting it. If nothing references it, remove it. If something
does — a shadow catcher, a capture rig helper — keep the object and remove or fix the collider, because
a concave MeshCollider on a dynamic Rigidbody is invalid and Unity warns on every single load.

## 1.2 No AudioListener during Menu

```text
There are no audio listeners in the scene
```

`AudioListener` lives on `MainCamera.prefab`, which is not present at Menu time. So menu audio may be
silent. Determine whether that is actually true in play, then fix it properly: one listener, always
exactly one, present from boot through gameplay. Two listeners is a worse bug than none — do not solve
this by adding a second.

## 1.3 Camera shake is reading garbage every frame

```text
GetPixelBilinear called on a Crunch compressed texture
  → NoiseTextureSampler.Sample → CameraFollow.GetShakeOffset → CameraFollow.LateUpdate
```

`GetShakeOffset` samples `noiseTexture` through `NoiseTextureSampler.Sample` every `LateUpdate`, and the
texture is Crunch-compressed, so `GetPixelBilinear` is invalid on it.

The existing Perlin fallback does **not** save this: it only triggers when the sample returns exactly
`Vector2.zero`, and a crunch texture returns garbage rather than zero. So the shake is currently driven
by junk data *and* logging a warning every frame.

Pick one and say why: make the noise texture readable/uncompressed, or drop the texture path entirely
and use the procedural Perlin that is already written. Perlin is likely the better answer — it is
allocation-free, needs no asset, and the texture is buying nothing.

## 1.4 A missing glyph renders as a box

```text
The character with Unicode value \u2713 was not found in [LiberationSans SDF]... replaced by \u25A1
```

A checkmark is rendering as □ in a `Label`. Find where the ✓ is produced. Either add the glyph to the
font asset's fallback, or replace it with a character the font has. **Do not edit a UI prefab to fix
this** — if the only fix is in a prefab, write the owner task instead.

---

# PART 2 — The threat model: stop running on waves

The owner played and said the game still runs on waves. It does. `WaveDirector`, `WavePressurePlan` and
`ZombieSpawner` (all under `Runtime/Gameplay/Waves/`) drive every spawn, and `WaveStartedEvent` /
`WaveClearedEvent` feed `HudController`, `GameplayAudioDirector`, `MissionTracker` and `RunDirector`,
which writes the wave number into `RunState`.

This contradicts the locked M6 direction — one endless world, no wave structure — and it is the last
unfinished piece of M7.3.

## 2.1 What to build

```text
ThreatTier = base + objectiveProgress + distanceBand + boundedTimePressure

Tier 0   walkers and runners — room to learn the route and the controls
Tier 1   one specialist pressure — ranged or pouncer
Tier 2   mixed specialists, tighter spawn cadence, elite chance
Tier 3   recovery-window enemies, heavy pressure, the boss route
```

Composition changes **before** stats. Eleven of the sixteen enemy assets are unused in production —
ranged (`Cacti`, `Cactus`, `SkeletonMage`), burrowers (`Burrow`, `MoleRat`), the `SkeletonGiant` elite
and the two bosses are all sitting there. Reach for them before touching HP multipliers.

Each tier introduces at most one new tactical question. Time pressure stays capped, so a losing player
is encouraged to finish rather than made mathematically doomed.

## 2.2 The migration path that keeps the game playable

`HudController` subscribes to `WaveStartedEvent` and displays a wave number, and **the HUD is an
owner-locked prefab**. So you cannot simply delete the wave events and leave the HUD dead.

```text
Replace what DRIVES spawning — the threat director decides composition and cadence instead of
  WavePressurePlan. This is the real change.
Keep the event surface alive so HUD, audio, MissionTracker and RunDirector keep working, even if a
  "wave" becomes an internal pacing beat rather than a designed wave.
Then write the owner task precisely: which HUD element shows the wave number, what should replace it
  (threat tier, distance band, or nothing), and what the code already exposes to bind to.
```

Do not remove `MissionTracker`'s `ClearWave` metric or `RunState.SetWave` without a replacement — a
dangling mission metric is worse than a stale one. List everything still depending on the wave concept
and say which of it is now vestigial.

Register every new run-scoped static with `RunScope` on the day you write it.

## 2.3 The rule if capacity runs short

Build the threat director and **leave it disabled behind a flag** rather than half-migrating the
spawner. A game that still uses waves is fine. A game that spawns nothing is not.

---

## Decision authority

**Decide yourself, record it:** how the stray Plane is resolved, the AudioListener strategy, texture
versus Perlin for shake, the glyph fix, all threat tiers/cadence/composition numbers (TUNING), and
whether the threat director ships enabled or flagged.

**Never decide — record as owner tasks:** any UI prefab or scene edit (name the real prefab and a real
child path), adding/removing/renaming a card, the slot-A grammar, or any owner-locked rule (one weapon
per run, auto-fire, no reload, no manual grenade, 1-of-3 with a ≤30 s pause, 25% / 0% banking).

## Acceptance gates

1. The `Player/Plane` warning is gone, and you say what the object was for before removing or fixing it.
2. Exactly one `AudioListener` is present from boot through gameplay — never zero, never two.
3. Camera shake no longer samples a crunch texture; the chosen approach is stated with its reason.
4. The ✓ glyph renders, or the owner task naming the exact prefab fix is written.
5. A clean Bootstrap → Menu → Gameplay load produces **no new warnings** from these four sources.
   Paste the relevant console section as evidence.
6. Threat director exists — enabled or behind a flag — with tiers driving composition, not HP.
7. Everything still depending on the wave concept is listed, with what is now vestigial.
8. The game remains playable end to end; no system left spawning nothing.
9. New run-scoped state registers with `RunScope`, with a test.
10. Existing 669 tests stay green; count reported before → after; new behaviour has new tests.
11. `git status`: zero modified UI prefabs, zero modified scenes, nothing staged. Vendor changes disclosed.
12. `detect_changes()` run and reported.

## Final report format

```text
PHASE: EXECUTE — M7.3d — COMPLETE / PARTIAL (say exactly where you stopped)

Part 1 — the four console defects: what each was, how it was fixed, and the clean-load console section
Part 2 — threat: what exists, enabled or flagged, tiers and composition, what still depends on waves
         and what is vestigial
Decisions made under delegated authority, and why
Owner tasks recorded (exact prefab, real child path, element, binding)
Tests: count before → after, new tests, any modified test and exactly why
Play-test: what you verified in play, and what still needs a human
Vendor assets changed, including anything Unity changed on its own
detect_changes() output
Protected state: UI prefabs, scenes, staged files — git evidence
Files modified — full list
Open risks
M7.3 STATUS: DELIVERED / PARTIAL
BLOCKERS: none / exact blocker
```

Gate 5 is the one that proves Part 1: a clean load with those warnings gone, pasted, not described.

Completion audio policy: do not generate or play a spoken/TTS report. The owner wants only the
project's short completion notification behavior.

## Completion notification

After the task is genuinely complete and verified:

1. Use the `simplifier-vi` skill at Level 1 to reduce the final report to the outcome, verification performed, and any important remaining issue.
2. Rewrite that summary in natural English using no more than 80 words.
3. Run:

   python Tools\notify_done.py "<English summary>" --repeat 2

If the task is blocked or fails, run the same command with a short English explanation of the blocker. Report any notification-script failure in the written response.
