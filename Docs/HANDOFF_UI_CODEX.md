# ZombieWar UI — Implementation Handoff

> Updated 2026-07-20. Read `HANDOFF.md` first. This document covers the current UI implementation and the next wiring phase. `UI_REDESIGN_SPEC.md` remains the visual source of truth.

## Current architecture

The UI is prefab/scene-authored and remains editable in the Unity Inspector. Editor installers are authoring tools, not runtime factories.

### Runtime

- `Runtime/UI/Core/UIManager.cs`: screen stack and navigation.
- `Runtime/UI/Core/UIScreen.cs`: lifecycle, CanvasGroup visibility and opaque/popup behavior.
- `Runtime/UI/Core/UITransition.cs`: DOTween-free transitions.
- `Runtime/UI/Core/UITheme.cs`: visual tokens and rarity colors.
- `Runtime/UI/Core/SafeArea.cs`: device safe-area handling.
- `Runtime/UI/Core/CurrencyClusterWidget.cs`: currency presentation with a replaceable provider.
- `Runtime/UI/Screens`: Hub, Loadout, Costume, Shop and Pass behaviors.
- `Runtime/UI/RunOverlays.cs`: pause/revive/level-up/GameOver presentation lifecycle.
- `Runtime/UI/HudController.cs`: gameplay HUD bindings.
- `Runtime/UI/MenuCharacterStage.cs`: prefab-based RenderTexture character preview.
- `Runtime/UI/Data/UIPrototypeCatalog.cs`: temporary icon/owned/featured metadata only; not an economy backend.

### Editable authored assets

- Screens: `Assets/_Project/UI/Prefabs/Screens/UI_*.prefab`.
- Character stage: `Assets/_Project/UI/Prefabs/Preview/MenuCharacterPreviewStage.prefab`.
- RenderTexture: `Assets/_Project/UI/RenderTextures/MenuCharacterPreview.renderTexture`.
- Generated UI sprites: `Assets/_Project/UI/Sprites`.
- Generated weapon/costume thumbnails: `Assets/_Project/UI/Icons/Generated`.
- Menu and gameplay scene contracts: `Menu.unity` and `Map_Level1.unity`.

### Editor tools

| Menu | Purpose |
|---|---|
| `ZombieWar/UI/Authoring/Ensure Menu Scene Contract` | Ensure UIRoot/screens/preview references exist in Menu |
| `ZombieWar/UI/Authoring/Ensure Gameplay HUD Contract` | Ensure HUD/overlays exist in Map_Level1 |
| `ZombieWar/UI/Authoring/Validate All UI References` | Find missing authored references |
| `ZombieWar/UI/Authoring/Create Missing UI Prefabs` | Create missing screen prefabs without replacing existing ones |
| `ZombieWar/UI/Authoring/Generate Item Thumbnails` | Refresh weapon/costume icon catalog |
| `ZombieWar/UI/Authoring/Preview/...` | Inspect one screen in Edit Mode |
| `ZombieWar/UI/Authoring/Rebuild ... (Destructive)` | Rebuild a screen intentionally; review diff before accepting |

## What is complete

- Hub, Loadout, Costume, Shop, Pass and HUD prefabs exist and are editable.
- Shop has Weapons, Gacha, Costume and Upgrades tabs.
- Run HUD plus pause, revive, perk presentation and GameOver overlays exist.
- Visual foundation includes real rounded corners, soft glow, rarity borders, gradients and interaction animations.
- Menu preview uses a persistent prefab + camera + RenderTexture setup.
- Weapon icon pipeline covers all 25 canonical WeaponData assets.
- Costume browsing covers all 14 catalog slots through three body-group tabs and pooled paging cells.
- UI authoring no longer depends on spawning every element at runtime.

## What is still prototype-only

| Area | Current behavior | Required wiring |
|---|---|---|
| Loadout | Card selection updates preview and slot 0 only | Read/write authoritative equipped slots through `LoadoutState`; enforce slot rules; persist and apply to spawned Player |
| Weapon ownership | `UIPrototypeCatalog.cheatUnlockAll=true` | Ownership belongs to save/economy state; catalog keeps icons only |
| Shop Weapons | Selection visual only | Price validation, purchase transaction, owned/equip state, currency refresh and failure feedback |
| Shop Gacha | Presentation only | Explicitly defer or implement a deterministic backend; do not fake purchases |
| Shop Upgrades | Presentation only | Define upgrade model, max levels, price curve and WeaponData-derived result |
| Costume | Preview apply only | Ownership, equipped parts, save/load and Player/menu preview synchronization |
| Currency | PlayerPrefs prototype provider | One authoritative wallet/save service with change events |
| Pass | Claim buttons log placeholder | Quest/progress/reward backend or remain visibly locked |
| GameOver | Payout values are placeholder | Bind real run result and wallet transaction |
| Revive | Ad button is placeholder | SDK/service boundary or disable cleanly |
| Stats | Raw data available | Define normalized bars and exact comparison rules |

## Data ownership rules for the next phase

1. `WeaponData` owns immutable weapon identity, base combat stats, presentation tier/price metadata and prefab references.
2. `LoadoutState` owns equipped weapon IDs/slots and compatibility migration from legacy aliases.
3. A single save/economy state must own currency, weapon ownership, upgrades and equipped costume IDs.
4. `UIPrototypeCatalog` must not become the production save database. Keep only icon/featured authoring metadata after migration.
5. UI views render state and send commands; they must not mutate ScriptableObject assets at runtime.
6. Purchases must be atomic: validate → subtract currency → unlock/upgrade → persist → emit changed event → refresh UI.
7. Missing assets/data must show a safe fallback and a clear Editor validation error; never silently grant or charge.

## Next phase acceptance criteria

- Loadout shows all 25 real weapons, correct icon/name/rarity/stats, ownership and equipped slots.
- Equipping in UI survives scene transition and save/reload, and the spawned Player uses the selected loadout.
- Shop buy/equip states remain correct after reopening screens and restarting Play Mode.
- Currency cannot become negative; duplicate purchases do not charge twice.
- Costume selection persists and is reflected both on menu preview and gameplay Player.
- All prototype hard-coded values are either replaced by real data or explicitly labeled/disabled.
- `cheatUnlockAll` remains an Editor/dev convenience and is not the runtime authority.
- Console has no new C# errors, missing references or repeated UI exceptions.
- Visual comparison is captured for Hub, Loadout, Costume, all four Shop tabs, Pass and gameplay HUD/overlays.

## Known test gotchas

- Test flow from `Bootstrap.unity` so Bill services are registered.
- ScreenSpaceOverlay UI is not captured by a camera RenderTexture; use `ScreenCapture.CaptureScreenshot`.
- Editing an installer does not automatically mutate an existing prefab/scene.
- Destructive rebuild commands require diff review; use contract/validation commands for ordinary wiring.
- Unity fake-null means `component ?? AddComponent` is unsafe; use `if (component == null)`.
- Do not instantiate the 978 costume cells at runtime; keep the existing pooled-page approach.
