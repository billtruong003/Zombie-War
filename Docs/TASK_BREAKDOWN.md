# ZombieWar — Active Task Breakdown

> Updated 2026-07-20. Completed historical work is summarized in `HANDOFF.md`; this file tracks remaining execution.

## A. UI/state wiring

- [ ] Audit current save/load and `LoadoutState` execution flow with GitNexus.
- [ ] Define profile schema: currencies, owned weapon IDs, equipped slots, weapon upgrades, owned/equipped costume GUIDs.
- [ ] Add version/migration handling for existing legacy weapon aliases.
- [ ] Implement one wallet/profile service and change events.
- [ ] Wire Loadout card ownership, selected slot, equip action and persistence.
- [ ] Wire Shop price/owned/affordable/equip states and atomic transactions.
- [ ] Wire Costume ownership/equip/save and menu/gameplay character synchronization.
- [ ] Bind all currency clusters, GameOver payout and run result data.
- [ ] Decide Gacha/Pass/revive scope; implement or disable with truthful copy.
- [ ] Replace prototype stat normalization with documented player-facing rules.
- [ ] Add EditMode tests for data/migrations/transactions.
- [ ] Add PlayMode tests for Menu → Map loadout application and save/reload.
- [ ] Capture visual evidence for every screen/tab and run overlay.

## B. Progression and balance design

- [ ] Export current 25-weapon stats and identify outliers/missing values.
- [ ] Define family roles and target TTK/reload/ammo/recoil bands.
- [ ] Define player baseline and each reachable power tier.
- [ ] Export enemy and wave tunables; define archetype roles.
- [ ] Balance enemy HP/damage/speed/reward against weapon tiers.
- [ ] Define XP/perk/drop/reward and meta purchase cadence.
- [ ] Produce a concise gameplay-loop and power-curve document.

## C. Map_Level1

- [ ] Audit current scene hierarchy, NavMeshSurface, spawn logic and camera bounds.
- [ ] Block out bounded arena dimensions.
- [ ] Add obstacle/choke/recovery-space layout.
- [ ] Define spawn zones and safe distance/visibility constraints.
- [ ] Re-bake NavMesh and validate all zombie archetypes.
- [ ] Tune wave concurrency and cadence against mobile budget.
- [ ] Verify full run and return-to-menu flow.

## D. In-run systems

- [ ] Drops/pickups/magnet.
- [ ] XP and perk selection backend.
- [ ] Runtime stat modifiers.
- [ ] Score/high score and results.

## E. Polish and hardening

- [ ] VFX/SFX/music/haptics pass.
- [ ] FTUE/settings/accessibility.
- [ ] Pool/GC/render/memory profiling.
- [ ] Save recovery/versioning tests.
- [ ] Addressables decision after scene/content contracts stabilize.
- [ ] Platform QA and release pipeline.
