# ZombieWar — Portrait Menu UI Direction 02 handoff

> Current UI execution contract as of 2026-07-23. This document overrides older menu-layout notes
> when they conflict with the active portrait Direction 02 implementation. Gameplay, economy and
> campaign status remain authoritative in `ACCOUNT_SWITCH_HANDOFF.md`.

## 1. Current checkpoint

- Branch: `main`.
- UI implementation checkpoint: `2cbe3b88` (`feat(ui): align portrait menu with direction 02`).
- Target: portrait mobile, reference canvas 1080×1920, notch-aware.
- Active menu hierarchy: `UIRoot`. The old `MenuCanvas` is legacy and inactive.
- Authored screen prefabs:
  - `Assets/_Project/UI/Prefabs/Screens/UI_HubScreen.prefab`
  - `Assets/_Project/UI/Prefabs/Screens/UI_LoadoutScreen.prefab`
  - `Assets/_Project/UI/Prefabs/Screens/UI_CostumeScreen.prefab`
  - `Assets/_Project/UI/Prefabs/Screens/UI_ShopScreen.prefab`
  - `Assets/_Project/UI/Prefabs/Screens/UI_PassScreen.prefab`
- Shared authoring code:
  - `Assets/_Project/Scripts/Editor/UI/SuperCasualSkin.cs`
  - `Assets/_Project/Scripts/Editor/UI/HubInstaller.cs`
  - `Assets/_Project/Scripts/Editor/UI/MenuScreensInstaller.cs`
  - `Assets/_Project/Scripts/Editor/UI/PassScreenInstaller.cs`
- Verified captures from the checkpoint are under
  `Assets/Screenshots/UIAudit/Direction02/`.

The checkpoint established the intended visual family and prefab-first structure, but it is not a
final visual sign-off. The owner has identified regressions on Hub and Costume that must be corrected
in the next pass.

## 2. Product decisions already approved

1. All player-facing menu content must be English. Do not leave mixed Vietnamese/English labels,
   helper copy, tabs, mission text, buttons, empty states, tooltips or modal text.
2. Keep both Coin and Gem in the top currency cluster. Each currency pill needs a clearly tappable
   `+` action for opening the appropriate earn/store flow.
3. The Mission card reward control must not be white. Give it a deliberate accent fill with readable
   contrast and a state distinct from the primary PLAY CTA.
4. No Settings shortcut is required in this pass.
5. Preserve the existing real profile, wallet, costume, loadout, shop, gacha and pass data wiring.
   This is a presentation/layout correction, not a parallel economy implementation.

## 3. Known visual defects to fix

### Hub

- The bottom navigation panel/dock is shifted too far upward. It must sit against the bottom safe
  area with only the intended home-indicator clearance.
- Rebalance the vertical stack after fixing the dock; do not compensate by squeezing or stretching
  the character preview.
- Coin and Gem pills currently lack the approved `+` actions.
- The Mission reward treatment is white and visually disconnected from the rest of Direction 02.
- Confirm the Mission card, character preview, PLAY button and bottom dock do not collide on 9:16,
  19.5:9 and 20:9 portrait profiles.

### Costume

- Controls currently stack and overlap. Category tabs, slot chips, paging/random controls, preview,
  item grid and bottom action area need a clear non-overlapping hierarchy.
- The bottom panel is also lifted too far upward and must be pinned to the bottom safe area.
- Keep the four product groups in English and preserve the live 448-item Pro Casual catalog and
  ownership/equip behavior.
- Costume icons must resolve from:
  `Assets/_Project/UI/Icons/Generated/CasualCostume`.
- Do not substitute the historical Fantasy catalog or generated icons from another directory.

### RawImage and texture sizing

- Several UI textures are stretched. A `RawImage` that presents a source texture or RenderTexture
  must preserve the source aspect ratio.
- “Native size” means the displayed content uses the texture's width:height ratio and is scaled
  uniformly to fit its allocated frame. It does not mean blindly assigning the source pixel
  dimensions on a 1080×1920 canvas.
- Use `AspectRatioFitter`, a deterministic aspect calculation, or equivalent prefab-authored
  constraints. Never use independent X/Y stretching that deforms the art.
- For the shared character RenderTexture, calculate from the actual texture dimensions and verify
  both Hub and Costume after the change.
- Decorative 9-sliced sprites may resize through their borders; character/item artwork may not be
  distorted.

## 4. Authoring and wiring rules

- Screens are prefab-first and installer-authored. Fix the responsible installer/shared skin, run
  the matching authoring command, review the generated prefab and scene diffs, and then repair all
  serialized references.
- Do not drag elements by hand only in `Menu.unity`; that produces a scene/prefab mismatch and will
  be overwritten by the next rebuild.
- Keep `UIRoot` at 1080×1920 with safe-area content. Backgrounds may bleed full screen; interactive
  content must respect the safe area.
- Every button must have one intended persistent listener, a mobile-sized hit target, and no
  transparent raycast blocker above it.
- The currency `+` buttons must call an existing navigation/store/earn boundary if one exists. If
  the product flow is not implemented, wire an explicit safe relay/disabled state and document the
  gap. Do not silently grant currency in production UI.
- Keep runtime state application separate from editor-time hierarchy construction.
- Do not modify vendor assets under `Assets/ThirdParty` to solve layout issues.

## 5. Acceptance criteria

- Hub and Costume match one coherent portrait Direction 02 system in Unity, not only in a web mockup.
- All visible menu copy in Hub, Loadout, Costume, Shop and Pass is English.
- Coin and Gem pills each display a working, unobstructed `+` action.
- Mission reward uses an intentional non-white accent color with accessible text/icon contrast.
- Hub and Costume bottom panels are aligned to the bottom safe area on 9:16, 19.5:9 and 20:9.
- Costume has zero overlapping interactive controls at those ratios.
- RawImage/texture artwork preserves its native aspect ratio; no character or icon is visibly
  stretched.
- Hub/Costume still use their source prefabs, all runtime fields are serialized correctly, all
  buttons are wired, and there are zero missing scripts/references.
- Costume resolves all assigned item icons from the approved `CasualCostume` directory.
- EditMode baseline is at least 191 passing tests with zero failures and no new Console errors.
- Fresh Unity screenshots are saved under a new bounded folder in `Assets/Screenshots/UIAudit/`.
- Run GitNexus `detect_changes` before commit. Commit and push only the intended UI/docs changes;
  preserve unrelated dirty vendor, recovery, tooling and screenshot files.

## 6. Required verification sequence

1. Pull latest `main`, inspect Git status and preserve unrelated work.
2. Read `AGENTS.md`, `CLAUDE.md`, this document and the current prefab/installer code.
3. Run GitNexus impact analysis for every code symbol before editing it. Warn before proceeding on
   HIGH or CRITICAL blast radius.
4. Run the 191-test EditMode baseline.
5. Inspect Hub and Costume in Unity at all three portrait aspect ratios before changing them.
6. Fix the shared component/installer source, rebuild prefab-first and validate serialized wiring.
7. Exercise navigation, currency actions, costume browsing/equip and back navigation in Play Mode.
8. Capture comparison screenshots, run the full EditMode suite, clear new Console errors and run
   `detect_changes`.
9. Review the scoped Git diff, commit with a clear message and push `main`.

## 7. Out of scope

- Player rig, WeaponSocket, GunMount, RecoilPivot and weapon pose data.
- Vendor asset edits.
- Running or committing `Tools/process_weapon_icons.py`.
- Running or committing the unapproved GUI sprite catalog/icon processing output.
- Replacing Bill.Pool, BillTween or Bill.Events conventions.
- Broad cleanup unrelated to the menu UI correction.
