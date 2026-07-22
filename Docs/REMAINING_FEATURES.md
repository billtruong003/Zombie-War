# ZombieWar — Remaining Features

> Current snapshot: 2026-07-22, after the VAT roster + campaign backend push. Read
> `ACCOUNT_SWITCH_HANDOFF.md` section 0 first; execution detail is in
> `ENEMY_CAMPAIGN_TASK_STATE.md`.

## Resolved since 2026-07-21

- ~~Imported monsters are source art only~~ -> 15/15 Cute monsters baked as VAT (HUGO documented
  blocked: 16,567 verts > 16,384 texture limit). Tests enforce zero Animator/SMR.
- ~~Campaign/run state is missing~~ -> RunState ledger + RunDirector + CampaignCatalog + CombatPower
  gates + PlayerProfile persistence (completion, first-clear, missions) all live, 191/191 tests.
- ~~Maps are placeholders~~ -> Map_Level2-5 cloned with wired waves/spawns/NavMesh; desert map
  GENERATOR exists (`Tools/ZombieWar/Desert Map Generator`) producing Map_GenTest.

## Immediate next (owner-approved order)

1. **Food buff system** — full approved spec in `Docs/FOOD_BUFF_SPEC.md`: green apple heal,
   blue berry shield (150 cap, no duration, 100% absorb, navy bar under health), red apple
   infinite ammo (mag snapshot/restore, ~8 s), cheese 2x coin (~20 s), HUD buff tiles.
   NOT implemented yet.
2. **Campaign selector UI** (`UI_CampaignScreen.prefab`) — backend complete, screen not built.
3. **Run-loop UI binding** — HUD coin/XP, level-up 1-of-3 perk overlay (RunOverlays is still
   placeholder), result screen reading `RunFinishedEvent`.
4. **Pass screen binding** — mission backend (20 missions, UTC rotation) done; UI not bound.
5. **PlayMode/profiler evidence** — no stress numbers yet; do 25/50/100 horde before calling
   anything mobile-safe.

## UI wiring checklist

- [x] Wallet/profile schema with versioned save data.
- [x] Weapon ownership keyed by stable `WeaponId`.
- [x] Equipped slots persisted through `LoadoutState` and applied on Player spawn.
- [x] Shop/Gacha/weapon-upgrade transactions are atomic and real.
- [x] Pro Casual ownership/equipment, preview/gameplay sync and item/set commerce.
- [ ] Real GameOver result and payout.
- [x] Currency widgets subscribe to `PlayerProfile`.
- [x] Production ownership no longer depends on `cheatUnlockAll`.
- [x] Weapon/Costume Gacha is real; Pass/rewarded revive remain honestly deferred.
- [ ] EditMode/PlayMode tests for persistence, duplicate purchase and insufficient funds.

## Game-design checklist

- [ ] Weapon role/stat/economy table for all 25 weapons.
- [ ] Player baseline plus reachable power tiers.
- [ ] Enemy archetype HP/damage/speed/reward table.
- [ ] Wave and boss/elite milestone curve.
- [ ] XP/perk/drop/reward curve.
- [ ] Target run duration and expected purchase cadence.

## Map checklist

- [ ] Arena footprint and camera bounds.
- [ ] Obstacle/choke/recovery-space blockout.
- [ ] Spawn zones and anti-pop-in distance rules.
- [ ] NavMesh rebake and path validation.
- [ ] Pickups and combat readability.
- [ ] Mobile stress test at maximum concurrency.

## Later

- Audio/VFX/haptics polish.
- More enemies, elites and boss content.
- FTUE and accessibility/settings persistence.
- Addressables/asset streaming after content stabilizes.
- Release hardening and platform QA.
