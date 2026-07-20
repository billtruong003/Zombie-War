# ZombieWar — Current Handoff

> Updated 2026-07-20. This is the canonical project-status document. Design intent lives in
> `GAMEPLAY_DESIGN.md`, the visual target in `UI_REDESIGN_SPEC.md`, weapon identity in
> `WeaponRosterMapping.json`, and the rig investigation history in `PlayerRigSocketIncident.md`.

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
- 25 generated weapon thumbnails and 108 representative costume thumbnails exist.
- Full costume catalog contains 978 usable parts across 14 slots; held-item categories are excluded.
- Menu character preview is a prefab + RenderTexture setup available in the Editor.
- `UIManager`, `UIScreen`, transitions, safe area, currency widget and UIFx components exist.
- Editor authoring/validation tools live under `ZombieWar/UI/Authoring/...`.

## 3. Current limitations — next work, not weapon setup

### UI data wiring

The screens are visually authored but several interactions are still prototype-only:

- `UIPrototypeCatalog.cheatUnlockAll` is enabled so all weapons/costumes can be inspected.
- `LoadoutScreen` selection only previews slot 0; it does not persist through `LoadoutState` yet.
- `ShopScreen` selects cards visually but does not buy/equip anything.
- `CostumeScreen` applies parts to preview only and does not persist ownership/equipment.
- Wallet/currency currently uses a PlayerPrefs-backed prototype provider, not a production economy service.
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
