# ZombieWar — Product Roadmap

> Updated 2026-07-21. Menu/profile/Pro Casual commerce is complete. Current execution focus is the
> VAT enemy + five-stage campaign contract in `ENEMY_CAMPAIGN_EXPANSION_PROMPT.md`; it includes any
> missing run-loop prerequisite from `NEXT_PHASE_RUN_LOOP_PROMPT.md`.

## Completed foundations

- ✅ Core player/zombie/wave/game-state loop.
- ✅ 25-weapon canonical roster, stable IDs, migrated prefabs/data/icons.
- ✅ Weapon muzzle/grip/IK/recoil pose authoring and runtime restore.
- ✅ UI visual system, six editable screen prefabs, gameplay HUD and run overlays.
- ✅ Menu character RenderTexture preview and modular costume catalog extraction.
- ✅ Generated item thumbnails and Editor validation/authoring tools.

## Phase A — UI and state wiring (completed except run results)

- ✅ Define one authoritative wallet/save profile.
- ✅ Move ownership/equipment authority out of `UIPrototypeCatalog`.
- ✅ Wire Loadout to `LoadoutState` and spawned Player.
- ✅ Wire real weapon/Costume item+set Shop, Gacha and weapon upgrades.
- ✅ Persist Pro Casual costume ownership/equipment and synchronize preview/gameplay character.
- ⬜ Bind GameOver payout, Pass/revive availability and all currency displays.
- ⬜ Replace hard-coded stats with documented normalized values.
- ⬜ Save/reload and scene-transition tests.

## Phase B — Progression and combat balance

- ⬜ Establish player baseline and reachable in-run power tiers.
- ⬜ Balance all weapon families by role, TTK, ammo/reload, accuracy and economy.
- ⬜ Balance zombie HP/damage/speed/reward and wave composition.
- ⬜ Define XP/perk/drop/reward curves and run-length targets.
- ⬜ Visualize the complete run loop and meta loop in a concise design sheet.

## Phase C — Level 1 map and encounter

- ⬜ Lock bounded-arena footprint and camera-readable margins.
- ⬜ Build navigation geometry, obstacles, lanes, choke points and recovery space.
- ⬜ Author spawn zones with minimum/maximum player distance and visibility rules.
- ⬜ Re-bake NavMesh and validate every enemy archetype.
- ⬜ Tune concurrency/spawn cadence against mobile performance budget.
- ⬜ Validate full run: Menu → Loadout → Map → waves → death/revive → payout → Shop.

## Phase D — In-run roguelite systems (current execution slice)

Execution contract: `Docs/NEXT_PHASE_RUN_LOOP_PROMPT.md`.

- ⬜ Pickup/drop table and magnet behavior.
- ⬜ XP, level-up and 1-of-3 perk choice.
- ⬜ Runtime stat modifiers and perk stacking rules.
- ⬜ Score/high score and real GameOver result data.

## Phase E — Content and feel

- ⬜ More enemy/elite/boss content and encounter milestones.
- ⬜ VFX/SFX/music/haptics pass.
- ⬜ Animation and UI feedback polish.
- ⬜ FTUE/onboarding and settings persistence.

## Phase F — Hardening and release

- ⬜ Save migration/versioning and failure recovery.
- ⬜ Profiling: GC, pooling, draw calls, memory and maximum-enemy stress.
- ⬜ Addressables/content-loading decision after asset ownership and scene boundaries stabilize.
- ⬜ QA matrix, platform builds, store assets and release pipeline.

## Deferred intentionally

- Infinite/chunk world streaming.
- Addressables migration.
- IAP/ads/premium backend.
- Online leaderboard.

These should not interrupt UI/state wiring or the first balanced bounded arena.
