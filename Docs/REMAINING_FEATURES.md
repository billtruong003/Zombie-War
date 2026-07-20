# ZombieWar — Remaining Features

> Current snapshot: 2026-07-20. For architecture and exact paths, read `HANDOFF.md`.

## Immediate blockers

1. **UI state is split between real gameplay data and prototype metadata.** Loadout/Shop/Costume must share one ownership/equipment/save model.
2. **Economy is not authoritative.** Currency, purchase, upgrade, payout and rewards are still prototype or presentation-only.
3. **Map_Level1 is a test arena.** It needs encounter geometry, spawn rules, NavMesh rebake and balance against the finished weapon roster.

## UI wiring checklist

- [ ] Wallet/profile schema with versioned save data.
- [ ] Weapon ownership keyed by stable `WeaponId`.
- [ ] Equipped slots persisted through `LoadoutState` and applied on Player spawn.
- [ ] Shop transaction service with atomic purchase/upgrade behavior.
- [ ] Costume ownership/equipped GUIDs, preview sync and gameplay application.
- [ ] Real GameOver result and payout.
- [ ] Currency widgets subscribe to one change source.
- [ ] Remove production dependency on `cheatUnlockAll`.
- [ ] Bind/disable Gacha, Pass and rewarded revive honestly.
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
