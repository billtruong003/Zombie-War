# ZombieWar — Remaining Features

> Current snapshot: 2026-07-21. Read `ACCOUNT_SWITCH_HANDOFF.md`; the active expansion contract is
> `ENEMY_CAMPAIGN_EXPANSION_PROMPT.md`, which absorbs the missing run-loop prerequisite.

## Immediate blockers

1. **Imported monsters are source art only.** Fifteen Cute creatures plus HUGO must be baked through
   the existing VAT pipeline; no runtime Animator/SkinnedMeshRenderer enemy is acceptable.
2. **Campaign/run state is missing.** Five stages, selection/progression, kills, Coin/XP/perks,
   results and terminal payout need authoritative data and save flow.
3. **Maps are placeholders.** Create five simple Plane scenes now; authored trees/obstacles come in
   the owner's next environment-design pass.

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
