# ZombieWar — UI Architecture

> Updated 2026-07-20. Visual source: `UI_REDESIGN_SPEC.md`. Wiring status: `HANDOFF_UI_CODEX.md`.

## Authoring model

UI is prefab-first and Inspector-editable. Editor installers create or intentionally rebuild assets;
runtime screen classes bind state and handle interaction. Do not return to runtime-only UI construction.

```text
Assets/_Project/UI/
  Prefabs/Screens/      UI_HubScreen, UI_LoadoutScreen, UI_CostumeScreen,
                       UI_ShopScreen, UI_PassScreen, UI_Hud
  Prefabs/Preview/      MenuCharacterPreviewStage
  Data/                 UIPrototypeCatalog
  Icons/Generated/      25 weapon + representative costume thumbnails
  RenderTextures/       MenuCharacterPreview
  Sprites/              rounded frames, pills, glow, gradients, rarity visuals
```

## Runtime layers

1. `UIManager` owns navigation stack and screen visibility.
2. `UIScreen` owns lifecycle and CanvasGroup behavior.
3. Screen classes bind authored references; view classes bind individual cards/slots.
4. Data providers/services own state. UI must not mutate ScriptableObject assets at runtime.
5. `RunOverlays` and `HudController` bind gameplay events in `Map_Level1`.

## Scene contracts

- `Menu.unity`: UIRoot/UIManager, authored screen instances and character preview stage.
- `Map_Level1.unity`: HUD, RunOverlays, EventSystem, camera and gameplay managers.
- `Bootstrap.unity`: Bill startup and additive scene flow.

Use `ZombieWar/UI/Authoring/Validate All UI References` after wiring. Rebuild commands marked
`(Destructive)` require explicit diff review.

## Current content

- Six editable screen prefabs.
- Four Shop tabs.
- Pass presentation and gameplay overlays.
- 25 real weapon thumbnails.
- 978 costume entries grouped across 14 slots; pooled/paged UI prevents mass instantiation.
- 108 representative costume thumbnails with fallback for uncaptured entries.

## Remaining architecture work

- Replace PlayerPrefs/prototype ownership with one versioned profile/wallet service.
- Wire Loadout, Shop and Costume to that authority.
- Bind real run results, payout and progression.
- Keep `UIPrototypeCatalog` for icon/featured authoring metadata, not production ownership.
