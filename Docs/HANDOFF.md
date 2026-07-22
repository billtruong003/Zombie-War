# ZombieWar — Historical handoff through pre-Casual slices

> **Superseded for current execution:** read `Docs/ACCOUNT_SWITCH_HANDOFF.md` first. This file remains
> useful history for weapon, profile and original Fantasy UI slices, but its costume counts, current
> limitations and recommended next step are no longer authoritative after Casual Phase 4.

> Updated 2026-07-20. This is the canonical project-status document. Design intent lives in
> `GAMEPLAY_DESIGN.md`, the visual target in `UI_REDESIGN_SPEC.md`, weapon identity in
> `WeaponRosterMapping.json`, and the rig investigation history in `PlayerRigSocketIncident.md`.

## Current correction (2026-07-21)

The active appearance stack is now **Pro Casual**, not the Fantasy figures described later in this
historical file. Current authoritative state: 448 player-facing items, 30 curated outfit sets,
four wardrobe tabs (`ĐẦU / THÂN / CHÂN / BỘ`), real Shop Costume modes (`ITEM LẺ / BỘ`) with
Coin/Gem confirmation purchases, and a real weapon shard Upgrade tab. Loose items purchase/equip by
their exact `itemId`; they never resolve through `FindSetContaining`. Set icons are full-character
renders. Weapon stars change actual damage/fire rate through `WeaponUpgradeMath`.

See `ACCOUNT_SWITCH_HANDOFF.md` for paths, current limits and verification evidence.

## 1. Project snapshot

- Project: `D:\Project\ZombieWar`
- Unity: 6000.3.10f1, URP, portrait mobile reference 1080 × 1920.
- Framework: BillGameCore services (`Bill.Events`, `Bill.Pool`, `Bill.State`, `Bill.Scene`, `Bill.Audio`) and BillTween.
- Gameplay: top-down auto-aim/auto-fire zombie survivor; virtual joystick; weapon switch and bomb controls.
- Scenes: `Bootstrap.unity` → additive `Menu.unity` / `Map_Level1.unity` through `GameFlow`.
- Main branch: `main`.

## 2. Completed foundation

### Gameplay

- Player spawn, movement, camera follow, health/death, animation controller, damage feedback and GameOver flow exist.
- Zombie manager, pooled spawner, wave director, VAT zombie variants and NavMesh chasing exist.
- HUD and run overlays are authored in `Map_Level1`: HP, wave, pause, revive, level-up presentation and GameOver presentation.
- Damage-number world text is present.

### Weapon system — setup phase complete

- 25 stable `WeaponData` assets under `Assets/_Project/Data/Weapons`.
- 25 canonical prefabs under `Assets/_Project/Prefabs/Weapons`, named `WPN_<Family>_<Model>`.
- Stable IDs use `weapon.<family>.<model>`; catalog order is presentation only. Legacy aliases preserve old save compatibility.
- Every weapon has an icon, muzzle/grip markers and authored pose data.
- Player weapon rig graph builds valid inside the Animator avatar hierarchy.
- One-handed/two-handed weights switch correctly; weapon follows `GunMount/RecoilPivot`; hands IK toward weapon grips.
- `WeaponPoseAuthoring` provides Manual/Live modes and `CAPTURE ALL -> WEAPON DATA + PLAYER IK`.
- Capture All stores model position/rotation/scale plus root-local right/left grip positions in `WeaponData`; Player target rotations remain global Player-rig data.
- Equip restores authored markers before `OnWeaponEquipped`, so target placement is correct on spawn and weapon switching.

Do not rebuild or re-parent the rig casually. Read `PlayerRigSocketIncident.md` before touching `PlayerRigBuilder`, `WeaponIKController`, `GunMount`, `RecoilPivot` or hand targets.

### UI visual foundation

- UI uses editable scene/prefab authoring, not runtime-only screen construction.
- Six screen prefabs exist: Hub, Loadout, Costume, Shop, Pass and HUD.
- Generated rounded frames, pills, glow, gradients and rarity visuals exist under `Assets/_Project/UI/Sprites`.
- 25 generated weapon thumbnails and full 978/978 costume thumbnails exist (Slice 4.1 — icon thật
  cho mọi part, không còn "representative subset").
- Full costume catalog contains 978 usable parts across 14 slots; held-item categories are excluded.
- Menu character preview is a prefab + RenderTexture setup available in the Editor.
- `UIManager`, `UIScreen`, transitions, safe area, currency widget and UIFx components exist.
- Editor authoring/validation tools live under `ZombieWar/UI/Authoring/...`.

## 3. Current limitations — next work, not weapon setup

### Save/profile foundation (done 2026-07-20, slice 1)

- `PlayerProfile` is the single versioned save authority (schema v1, key `zw.profile` via `Bill.Save`):
  wallet (long Coin/Gold/Gem), owned weapon IDs, 3 equipped slots, owned/equipped costume GUIDs,
  forward-compatible upgrade field. Contract: `Docs/PROFILE_SAVE.md`.
- Legacy `zw.loadout` + `wallet_*` migrate once, idempotently; old keys stay for rollback.
- `Player.prefab` now has `Weapon.useSlotSystem = 1`; `Bootstrap → Menu → Map` spawns the saved
  3-slot loadout end-to-end (verified in Play Mode incl. legacy-alias canonicalization and
  Play-Mode-restart persistence). Starter rule: first one-handed roster weapon, owned on fresh profile.
- EditMode suite: `_Project.Tests.EditMode` (16 tests, all passing).
- Spawned-player scene bug FIXED (Slice 2, 2026-07-21): `PlayerSpawner.Spawn` moves the player into
  the spawner's scene; 3 Menu↔Map cycles verified = 1 player in-game, 0 after unload. PlayMode tests
  cover scene ownership + no-orphan cycles.

### Loadout screen wiring (done 2026-07-21, slice 2)

- `UI_LoadoutScreen.prefab` is live-wired: ownership from `PlayerProfile` (cheatUnlockAll ignored
  here), 3 slots read/write through `LoadoutState`, explicit active target slot, equips via
  `LoadoutState.TryEquip` (slot contract + duplicate-move rule, see `Docs/PROFILE_SAVE.md`),
  refresh via `PlayerProfile.LoadoutChanged` (subscribe OnEnable/unsubscribe OnDisable).
- Card icons bind at runtime from `UIPrototypeCatalog.GetWeaponIcon` (they were never baked in the
  prefab); locked cards show details but shake and never mutate state.
- `EnsureValidLoadout` also runs when the screen opens, so a fresh profile gets its starter
  (lowest-CatalogOrder one-handed weapon) before ever entering gameplay.
- Validator gained duplicate-WeaponId/catalogOrder checks; EditMode 25 + PlayMode 2 tests green.

### Shop Weapons + wallet (done 2026-07-21, slice 3)

- Atomic purchase: `PlayerProfile.TryPurchaseWeapon` (validate → deduct → own → one save → events
  after commit; rollback on save failure). Price = `WeaponData.price`, `unlockCost` ignored.
- Shop Weapons tab live: runtime owned/affordable/price states from profile (overrides installer
  bake), tap-select then tap-again-buy, danger price + shake when unaffordable, no auto-equip.
- `ProfileCurrencyProvider` is the production currency default — widgets read the profile wallet;
  raw `wallet_*` keys are migration inputs only.
- Gacha/Costume/Upgrades tab buttons disabled (honest) until real backends exist.
- Dev menu: Reset Profile / Reset+Seed 5.000 Coin / Add 1.000 Coin. Contract: `Docs/PROFILE_SAVE.md`.

### Costume wardrobe (done 2026-07-21, slice 4)

- Full 14-logical-slot wardrobe: 3 primary tabs + authored 8-chip slot selector row
  (`Ensure Costume Slot Selector`, idempotent) — grid always filters exactly one slot, pooled
  18 cells + paging over 978 parts. VN labels + per-slot counts on chips.
- Ownership/equipped are profile-authoritative (`TryEquipCostume`/`TryClearCostumeSlot`/
  `TryEquipOutfit`, GUID identity, slot resolved from catalog, base Body cannot be cleared,
  dedicated `CostumeChanged` event). Randomize = whole-outfit batch of OWNED parts (1 save/event).
- Preview + gameplay Player use the same saved outfit (verified 14/14 identity match in Play Mode,
  survives restart). Two applier fixes: name-based Clear (no duplicate/stale renderers) and
  layer inheritance (applied parts were invisible on the preview camera before — pre-existing bug).
- Dev tools: Unlock All Costume Parts (978/978, batch, idempotent), Reset Costume Progress To
  Design Defaults (ownership := đúng bộ default, ví/súng giữ nguyên).
- Validator: catalog integrity (14 slots/unique GUIDs/bindings/base Body/held-item exclusion) +
  chip wiring + bounded cell pool. Costume Shop/prices remain deferred — locked parts shake, no fake buy.
- **Slice 4.1 corrections:** default ownership + outfit + no-naked invariant + runtime "MẶC ĐỊNH"
  vs dev reset progress (superseded by 4.2 rules below).
- **Slice 4.2 (final costume model):** icon = OFFICIAL VENDOR SCREENSHOT (846/846 non-Body + 6 màu
  Body; generated costume PNG đã xoá — hết mắt/miệng đen). Body = composite 6 màu + 2 tai (Normal/Elf),
  không phải 132 mesh; body/head luôn cùng màu; assembly pieces ẩn. Slot essential có "Mặc định",
  optional có "Không mang"; Feet optional (bàn chân từ body mesh). Default ownership CHỐT = 9 guid
  (Hair/Eye/Brow/Mouth Black_1 + Chest_61 + Legs_62 + Feet 1/2/3) + White/Normal. Applier disable
  baked → 1 nguồn appearance. Preview kéo-xoay + idle showcase sống. Costume Shop/giá vẫn deferred.
- Contract chi tiết: `Docs/PROFILE_SAVE.md`.

### UI data wiring

The screens are visually authored but several interactions are still prototype-only:

- `UIPrototypeCatalog.cheatUnlockAll` only affects installer bake now — Loadout and Shop runtime
  ignore it; ownership/purchases are profile-only.
- `LoadoutScreen` is fully wired to `PlayerProfile`/`LoadoutState` (Slice 2) — remaining loadout
  polish: lock-overlay art is a placeholder sprite, stat bars still use provisional normalization.
- `ShopScreen` selects cards visually but does not buy/equip anything.
- `CostumeScreen` applies parts to preview only and does not persist ownership/equipment.
- Currency UI still binds the PlayerPrefs prototype provider; `PlayerProfile` wallet exists but the
  widgets are not rebound yet.
- Gacha, upgrades, Pass claims, rewarded revive and GameOver payout are presentation placeholders.
- Weapon stats are real `WeaponData`, but normalization, price/rarity curve and player-facing comparison still need a design pass.

### Map and encounter design

`Map_Level1` currently contains a simple ground, NavMeshSurface, player spawn, camera, managers, wave director and HUD. It is a functional test arena, not a finished combat map. Remaining work:

- Decide bounded-arena dimensions and camera-readable play space.
- Author obstacle/choke-point layout, enemy spawn ring/points and safe distances.
- Re-bake and validate NavMesh after geometry changes.
- Tune wave density, spawn cadence, concurrency, enemy mix and boss/elite milestones against real weapon stats.
- Define pickup/drop placement and run length before final economy balancing.
- Validate mobile performance and visibility at intended maximum enemy count.

Chunk streaming/Addressables are intentionally deferred until content and scene contracts stabilize.

## 4. Recommended execution order

1. Wire Loadout/Shop/Costume to one authoritative ownership, purchase and equipment state.
2. Replace hard-coded UI numbers with real WeaponData/player/economy data and define user-facing stat normalization.
3. Verify all six screen prefabs plus run overlays from `Bootstrap.unity`, including save/reload.
4. Lock the combat progression sheet: weapon curve, player curve, enemy curve, rewards and wave milestones.
5. Build the bounded Level 1 arena around that curve; bake NavMesh and run end-to-end balance tests.
6. Add content/juice/audio, then performance and asset-loading optimization.

## 5. Important paths

| Area | Path |
|---|---|
| Player prefab | `Assets/_Project/Prefabs/Player.prefab` |
| Weapon data | `Assets/_Project/Data/Weapons/WD_*.asset` |
| Weapon prefabs | `Assets/_Project/Prefabs/Weapons/WPN_*.prefab` |
| Weapon roster | `Docs/WeaponRosterMapping.json` |
| Weapon runtime | `Assets/_Project/Scripts/Runtime/Gameplay/Weapon*.cs` |
| Rig builder | `Assets/_Project/Scripts/Editor/PlayerRigBuilder.cs` |
| UI screen prefabs | `Assets/_Project/UI/Prefabs/Screens` |
| UI runtime | `Assets/_Project/Scripts/Runtime/UI` |
| UI authoring tools | `Assets/_Project/Scripts/Editor/UI` |
| UI prototype catalog | `Assets/_Project/UI/Data/UIPrototypeCatalog.asset` |
| Costume catalog | `Assets/_Project/Data/Character/ModularCostumeCatalog.json` |
| Gameplay scene | `Assets/_Project/Scenes/Map_Level1.unity` |
| Visual UI spec | `Docs/UI_REDESIGN_SPEC.md` |

## 6. Verification and editing rules

- Run impact analysis before changing any symbol and `detect_changes` before commit.
- Preserve unrelated dirty work; do not modify ThirdParty sources to solve project logic.
- Rebuild authoring output only through the matching `ZombieWar/UI/Authoring/...` command and inspect prefab/scene diffs.
- Test UI from `Bootstrap.unity`; direct Menu play can miss Bill service registration.
- Use actual screen capture for ScreenSpaceOverlay verification; camera RenderTexture screenshots do not include it.
- Do not use DOTween. Use BillTween/UITransition.
- Do not introduce Addressables/Resources migration in the next phase.
- Do not stage, commit or push unless the task explicitly asks.
