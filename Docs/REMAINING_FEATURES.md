# ZombieWar — Remaining Features

> Current snapshot: 2026-07-23 EOD (`61be2cf2`). Read `ACCOUNT_SWITCH_HANDOFF.md` §0-bis first
> (including the hard UI-ownership rule); execution detail for the enemy milestone is in
> `ENEMY_CAMPAIGN_TASK_STATE.md`.

## Resolved since 2026-07-22

- ~~Mixed VN/EN content~~ -> ALL player-facing text is English (prefabs, data assets, runtime
  strings). Dev logs stay Vietnamese by design.
- ~~Pass screen binding~~ -> PassScreen binds the real mission backend (progress, counters,
  one-shot CLAIM, UTC rollover). Reward TRACK tiles + premium strip are still presentation.
- ~~HUD run coin~~ -> coin pill binds RunState (pickup -> bank -> HUD). Economy path verified.
- ~~Joystick offset feel~~ -> BillVirtualJoystick in BillGameCore (floating origin, pointer lock,
  dead zone). Framework-level, reusable by future games.
- ~~Maps visually sparse / placeholder Planes~~ -> all 5 campaign maps carry generated desert
  environments (in-place, per-map wiring intact, 12/12 spawn paths) + occlusion baked +
  static batching + one ToonLightRig each (direction/color/intensity authored per map).
- ~~Toon shaders ignore lighting intent~~ -> rig-driven `_ToonLightDirection/_ToonLightColor`
  consumed by VAT shaders and the embedded stylized-toon-world-kit; works with the
  directional light fully off.
- Icon pipeline v2 (face-on costume 512, side-profile outlined weapons, size previews).

## Immediate next (owner-approved order)

1. **PlayMode/profiler evidence** — 25/50/100 horde on the new maps; the static pipeline is done,
   so numbers now reflect reality. Blocks any "mobile-safe" claim. (First device build exists.)
2. **Food buff system** — full approved spec in `Docs/FOOD_BUFF_SPEC.md`. NOT implemented.
3. **Campaign selector UI** — backend complete; screen is OWNER-BUILT now (agents supply code
   binding only when asked).
4. **Run-loop UI binding** — level-up 1-of-3 perk overlay (RunOverlays still placeholder),
   result screen reading run summary + Payout.
5. **Pass reward track backend** — level rewards for the 6-tile track + premium decision;
   `PassScreen.XpPerLevel` (500) is provisional.
6. **Android identity** — bundle id is still the Unity template default; rename before any
   distribution.

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
