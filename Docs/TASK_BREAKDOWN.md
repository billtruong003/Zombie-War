# ZombieWar — Active Task Breakdown

> Updated 2026-07-21. Current truth is in `ACCOUNT_SWITCH_HANDOFF.md`; the active large execution
> contract is `ENEMY_CAMPAIGN_EXPANSION_PROMPT.md`, which absorbs the unfinished run-loop prerequisite.

## Current migration checkpoint — 2026-07-21

- [x] Pro Casual bind-pose proof, 448-item catalog and core Player/menu integration.
- [x] Pro Casual data-driven Costume UI and 448 real item icons.
- [x] Phase 6: rebuild Costume Shop/Gacha/Economy around stable Pro Casual `itemId` values.
- [x] Corrective commerce pass: separate loose items from 30 named outfit sets, Coin/Gem offers,
  full-set icons, purchase modal, and real weapon shard upgrades applied to combat. (2026-07-21)
- [ ] Phase 7 cleanup: prove zero active Fantasy dependency before removing rollback assets. Keep it
  as a later bounded audit; do not block current gameplay work on historical cleanup.
- [ ] **NEXT:** execute `Docs/ENEMY_CAMPAIGN_EXPANSION_PROMPT.md`: audit/bake 16 VAT enemies, finish
  missing run-loop prerequisites, create five campaign stages/selector/waves/power gates/rewards and
  real Pass missions, with full Unity MCP proof.

`Docs/ACCOUNT_SWITCH_HANDOFF.md` is the canonical status. Fantasy-specific tasks below are retained as
implementation history and must not override the current Casual migration plan.

## A. UI/state wiring

- [x] Audit current save/load and `LoadoutState` execution flow with GitNexus. (2026-07-20)
- [x] Define profile schema: currencies, owned weapon IDs, equipped slots, weapon upgrades, owned/equipped costume GUIDs. (`Docs/PROFILE_SAVE.md`)
- [x] Add version/migration handling for existing legacy weapon aliases. (migrate-on-load via `EnsureValidLoadout`)
- [x] Implement one wallet/profile service and change events. (`PlayerProfile` over `Bill.Save`; UI not rebound yet)
- [x] Enable Player prefab slot system; Bootstrap → Menu → Map applies saved loadout end-to-end. (verified in Play Mode)
- [x] Fix spawn bug: player now moved into spawner's scene, no orphan across 3 verified cycles + PlayMode tests. (2026-07-21)
- [x] Wire Loadout card ownership, selected slot, equip action and persistence. (Slice 2 — `LoadoutState.TryEquip`, active slot, `LoadoutChanged` refresh)
- [x] Wire Shop price/owned/affordable/equip states and atomic transactions. (Slice 3 — `TryPurchaseWeapon`, ProfileCurrencyProvider, dev wallet tools; Gacha/Costume/Upgrades buttons disabled honestly)
- [x] Wire Costume ownership/equip/save and menu/gameplay character synchronization. (Slice 4 — 14-slot chip selector, TryEquipCostume/Outfit, CostumeChanged, dev unlock-all, applier layer+Clear fixes)
- [x] Costume corrective 4.1: real icons, design-default ownership, no-naked invariant, resets. (2026-07-21)
- [x] Costume corrective 4.2 (final): vendor screenshot icons (846 + 6 Body colors, generated removed), Body composite (6 colors + Normal/Elf, no 132 cards, no color mismatch), essential "Mặc định" / optional "Không mang", Feet optional, default ownership 9 guid + White/Normal, applier disables baked (single source), rotatable + living-idle preview, raw-Body migration. (2026-07-21)
- [x] Costume commerce (Slice 5): `EconomyConfig` rarity bands + 854 records; `TryPurchaseCostume` atomic; Costume screen shows price on unowned, tap-2 buy→auto-equip; starter/gacha-only not sellable. (2026-07-21)
- [x] Real gacha (Slice 6): `GachaService` (deterministic, pity, dup compensation) + both pools from `EconomyConfig`; single/x10; reveal NEW/DUP; starter/gacha excluded; cross-screen sync. (2026-07-21)
- [x] Full menu integration (Slice 7 + corrective pass): one profile/wallet/ownership/catalog across
  screens; Shop Costume sells real loose items/sets; new-item badges; dev wallet cheat seeds all currencies. (2026-07-21)
- [x] Real weapon Upgrade tab: owned-weapon paging, shards + Gold costs, star levels, DMG/ROF preview,
  atomic upgrade and combat-effective scaling. (2026-07-21)
- [ ] Bind GameOver payout and run result data. (currency clusters done Slice 3)
- [ ] Decide Pass/revive scope; implement or disable with truthful copy. (Gacha done Slice 6)
- [ ] Replace prototype stat normalization with documented player-facing rules.
- [x] Add EditMode tests for data/migrations (25 tests: profile, migration, equip rules). Transactions pending Shop slice.
- [x] Add PlayMode tests for spawn scene ownership / no-orphan cycles. Menu → Map loadout application verified via MCP runtime; automated Bootstrap-flow test still open.
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
