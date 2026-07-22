# ZombieWar — UI Architecture

> Updated 2026-07-21. Visual source: `UI_REDESIGN_SPEC.md`. Current status: `ACCOUNT_SWITCH_HANDOFF.md`.

## Authoring model

UI is prefab-first and Inspector-editable. Editor installers create or intentionally rebuild assets;
runtime screen classes bind state and handle interaction. Do not return to runtime-only UI construction.

```text
Assets/_Project/UI/
  Prefabs/Screens/      UI_HubScreen, UI_LoadoutScreen, UI_CostumeScreen,
                       UI_ShopScreen, UI_PassScreen, UI_Hud
  Prefabs/Preview/      MenuCharacterPreviewStage
  Data/                 UIPrototypeCatalog
  Icons/Generated/      25 weapon + 448 Pro Casual item icons + 30 set icons
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
- 448 Pro Casual player-facing items across 18 definitions; pooled/paged UI prevents mass instantiation.
- `CasualCostumeCatalog` owns stable `itemId → real baked sprite`; `CasualIconCatalogSync` copies the
  exact 448 mappings into `UIPrototypeCatalog`. Missing icon/fallback is a test failure.
- 30 curated outfit sets use separate full-character icons and never share loose-item card identity.
- Costume wardrobe has a dedicated `BỘ` tab. Shop Costume has separate `ITEM LẺ / BỘ` modes and an
  editor-authored confirmation modal. Shop Upgrade cards are editor-authored and bind runtime state.

## Remaining architecture work

- Add a prefab-first five-node Campaign screen between Hub PLAY and gameplay; parameterize GameFlow
  around the selected stage. Contract: `ENEMY_CAMPAIGN_EXPANSION_PROMPT.md`.
- Bind real run Coin/XP, temporary perks, Defeat/Victory results and atomic payout by extending
  `HudController`/`RunOverlays`; reuse the prerequisite contract, never build a parallel run system.
- Replace fake Pass quest percentages with the campaign mission catalog/progress/claim flow. Keep
  premium/revive unavailable until their product/backend scope is real.
- Keep `UIPrototypeCatalog` for icon/featured authoring metadata, not production ownership.
