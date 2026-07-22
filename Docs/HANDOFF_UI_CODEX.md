# ZombieWar UI — Historical implementation handoff

> **Current status moved to `Docs/ACCOUNT_SWITCH_HANDOFF.md`.** Architecture and authored-prefab rules
> below still apply, but Fantasy-specific 14-slot/978-part descriptions are historical. The active
> Casual Costume screen now uses ten logical slots, 323 stable-itemId parts and 323 real icons.

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

> 2026-07-20 (slice 1): `PlayerProfile` (versioned, via `Bill.Save`) now owns wallet, weapon
> ownership, equipped slots and costume ownership/equipment — see `Docs/PROFILE_SAVE.md`.
> `LoadoutState` delegates storage to it and `Player.prefab` has the slot system enabled, so
> persisted slots reach the spawned Player. The table below is updated accordingly.
>
> 2026-07-21 (slices 5–7): **one economy source of truth** — `EconomyConfig` ScriptableObject
> (`Assets/_Project/Data/Economy/EconomyConfig.asset`, generated deterministically by
> `ZombieWar/Economy/Generate Economy Config` from the costume catalog) holds rarity price bands,
> 854 costume commerce records, and both gacha pools. Costume shop, both gacha banners, weapon
> shop, loadout and costume equip all read one `PlayerProfile` + one catalog. New-item badges
> (minimal): gacha grants mark items unseen (`PlayerProfile.unseenItems`); Hub shows a red dot on
> LOADOUT (súng mới) / COSTUME (skin mới), cleared when that screen opens. Dev wallet cheat:
> `ZombieWar/Dev/*` (Editor-only, seeds Coin+Gold+Gem). All logic covered by 82 EditMode tests.

| Area | Current behavior | Required wiring |
|---|---|---|
| Loadout | **WIRED (Slice 2)**: active slot + equip qua `LoadoutState.TryEquip`, ownership từ `PlayerProfile`, refresh theo `LoadoutChanged`, icon bind runtime từ catalog | Còn lại: lock-overlay art placeholder, stat bar normalization tạm |
| Weapon ownership | `PlayerProfile.ownedWeaponIds` exists (equipped ⇒ owned, starter owned); `cheatUnlockAll=true` still masks UI locks | UI reads `PlayerProfile.IsWeaponOwned`; catalog keeps icons only; cheat becomes Editor-only display override |
| Shop Weapons | **WIRED (Slice 3)**: mua atomic qua `PlayerProfile.TryPurchaseWeapon` (tap-select rồi tap-lại-mua), state owned/affordable runtime từ profile, giá danger + shake khi thiếu tiền | Còn lại: navigation Loadout từ card đã sở hữu (tuỳ chọn) |
| Shop Gacha | **WIRED (Slice 6)**: 2 banner (Súng/Skin) quay thật qua `GachaService` + `EconomyConfig.GachaPool` (cost/rarity weight/pity/dup compensation serialized editor-visible). Single + x10, RNG inject được (test deterministic), pity per-pool lưu trong `PlayerProfile.gachaPity`, reveal overlay hiện MỚI/Trùng+đền bù, loại starter/gacha-only. Cross-screen sync qua Loadout/CostumeChanged | Còn lại: reveal animation polish (hiện là list tĩnh) |
| Shop Upgrades | Presentation only | Define upgrade model, max levels, price curve and WeaponData-derived result |
| Costume | **WIRED (Slice 4→4.2, commerce Slice 5)**: 14 slot chip, ownership/equip qua PlayerProfile, preview+gameplay sync, vendor icons, Body composite, essential/optional, kéo-xoay preview. **Slice 5**: giá theo `EconomyConfig` rarity band hiện trên card chưa sở hữu; tap-1 xem giá "MUA?", tap-2 mua atomic (`PlayerProfile.TryPurchaseCostume`) rồi tự mặc; starter không bán, gacha-only không bán ở shop. Shop-tab Costume redirect sang màn Costume (không placeholder giả) | — |
| Currency | **WIRED (Slice 3)**: `ProfileCurrencyProvider` là DefaultProvider — widget đọc ví profile, refresh theo `WalletChanged` | HUD coin pill in-run vẫn 0 (chưa có run-reward backend — trung thực) |
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
