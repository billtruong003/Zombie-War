# M8-C2 plan — UI polish, weapon icons, weapon roster, UX (for owner approval)

Status: **P0–P3 DONE 2026-09-26 (owner approved; icons: B = not owned, A = owned). P4 pending.** Results at the end.
Owner feedback that triggered it: the UI still does not feel improved; thin bars look pointy and
stretched; gun icons need proper material or a flat per-rarity look; many guns sit unused; UX is
not smooth.

## What the checks found

### 1. Why the UI does not feel better (it is not the legacy installers)
The legacy installer scripts (task running separately) only act if someone runs their menu; they
are not applied at runtime. The real causes:

| Cause | Evidence |
|---|---|
| M8-C changed **structure**, not **look** | new elements reused the existing flat grey sprites and colours; no depth, no new component style |
| `Menu.unity` overrides the screen prefabs | ~860 overrides on UI instances: Shop 295, Loadout 206, Costume 202, Hub 101, Pass 56 — mostly RectTransform positions/sizes and 52 font-material overrides. Prefab edits can be silently cancelled (the Shop grid needed 6 of these removed) |
| Thin bars are mis-sliced | `pill.png` has a 30 px 9-slice border drawn in 14-18 px bars → corners collapse into points; fills shrunk via anchorMax go below 2x border → stretched. **Recorded as a lesson** (memory `ui-sliced-bars`) |
| Two type families | HUD/overlays use LiberationSans, the Hub uses Cairo Line Black |
| No component kit | every card/pill/button is hand-assembled per screen, so quality varies screen to screen |

### 2. Gun icons
Every weapon uses `StylizedToonWorldKit/Toon/Toon Lit` with one palette texture that is mostly
near-black gunmetal — so "textured" renders are dark by nature. Current icons also keep the
roster's relative scale, so pistols sit tiny in their tile. Three styles rendered for comparison
(`Review/M8/icon_style_compare.png`, cropped to fill the tile, on a solid rarity tile):

| Style | Look | Verdict |
|---|---|---|
| A — original material, strong light | real detail (wood on the AK), dark metal pops on a bright tile | most "real", less icon-like |
| **B — flat unlit, pale rarity tint + dark outline** | clean sticker icon, exactly the owner's suggestion | **recommended** |
| C — unlit duotone (palette mapped to rarity) | keeps panel detail but low contrast | needs more contrast work |

### 3. Weapon roster
| Fact | Number |
|---|---|
| In the catalog | 54 |
| Playable now (`Ready`) | **25** |
| Waiting for grip/muzzle anchors (`PendingOwnerAuthoring`) | 26 |
| Need decimation / need a projectile fire mode | 2 / 1 |
| Rarity spread | almost all Common; 0 Legendary |
| Thin families | LMG 2, Sniper 3 |
| Unused pack content | ~900 prefabs/models across 6 weapon packs (includes attachments; gun roots still to be counted) |

### 4. UX not smooth (from code + play)
| Issue | Where |
|---|---|
| PLAY → ~15 s of frozen screen, no loading screen | `GameFlow.StartGameplay` → `Bill.Scene.LoadAdditive` with no UI |
| Level-up, pause and result pop on/off instantly, cards do not animate in | `RunOverlays.Show` is a bare `SetActive` |
| Numbers jump (coins banked, kills) | result screen sets text once |
| Equip gives no feedback beyond a border change | `LoadoutScreen.OnCardClicked` |
| No UI sound on taps, no haptics on key moments | no UI click cue wired; vibration toggle exists but nothing calls it |

## The plan (in order)

### P0 — foundation (makes every later change stick) · ~1 day
1. **Menu.unity override cleanup**: list every override on the five screen instances, keep only
   the intentional ones (none expected), revert the rest so the prefab is the single source of
   truth. Screenshot every screen before/after; owner signs off.
2. **Design tokens** in `UITheme`: one palette (slate grounds, yellow primary, rarity colours, layer
   colours), one display font (Cairo) + one body font, a type scale, corner radii, spacing.
3. **Component kit** (prefabs + small scripts, used by every screen):
   `Bar` (full-width fill clipped by RectMask2D, border scaled to height), `Pill`, `Chip`,
   `Card` (ground + 1 px light top edge + soft bottom shadow), `Button` primary/secondary/blue
   with the pressed-down bottom lip from the mockup, `RarityTile`.
   Rule from the lesson: every sliced image sets `pixelsPerUnitMultiplier` for its height.

### P1 — screen polish pass · ~1.5 days
Rebuild each screen on the kit and match the approved mockup pixel-for-pixel at phone size, with
zoomed captures of every thin element: Hub, Loadout, Shop, HUD, Level Up, Result, Pause, then
Costume and Pass (same kit, their layouts unchanged).

### P2 — gun icons · ~0.5 day
Style **B** (or the owner's pick): unlit render, cropped per gun to fill the tile (a pistol no
longer tiny), pale rarity tint, dark toon outline, solid rarity tile. One editor command
regenerates all guns; the tile colour comes from the same rarity token.

### P3 — UX smoothness · ~1.5 days
1. **Loading screen** Hub → map: dark ground, run weapon, rotating tip, real progress from the
   async load; pre-warm pools behind it. Target: no frozen frame.
2. **Overlay motion** (BillTween, unscaled time): level-up dim fades in and cards pop in
   staggered; picking a card punches it and flies to the skill bar; result time and coins
   count up; pause/confirm scale in.
3. **Feedback**: UI tap/confirm/back sounds, equip "EQUIPPED" stamp + sound, purchase success
   burst, "not enough coins" shake + link to where coins come from; haptic ticks on level-up,
   evolution and death (respecting the vibration toggle).
4. Walk every flow (first launch → FTUE → run → result → equip → shop → run) on a phone-size
   game view and fix what stutters.

### P4 — weapon roster · plan now, build after P0-P3
1. **Anchor authoring tool**: open a gun in a preview scene with a guessed grip/muzzle (from
   bounds), owner nudges and saves → the 26 pending guns become playable (~1 min each).
2. **Roster ladder**: per family Common → Uncommon → Rare → Epic → Legendary, a stat curve and a
   coin price curve re-tuned for M8's ~3x coin income. Output: a table for the owner to approve.
3. **New guns from the packs**: script lists gun roots in the 6 packs and renders a contact sheet;
   owner picks ~10-15 to fill Legendary, LMG and Sniper; onboard them through the same pipeline.
4. **Unlock UX**: "NEW" badges, recommended next gun, clear locked/affordable states in the Shop.

## Needs the owner
- Approve this order (P0 → P1 → P2 → P3, P4 after).
- Pick the icon style: A / **B** / C.
- P4.2 roster table and P4.3 gun picks when they are ready.
- Still pending from part D: bundle id and the upload keystore.

## Results (2026-09-26)

Captures: `Review/UiTour/before/` → `Review/UiTour/final/` (+ `final2/` for locked icons and the buy modal).
Every capture is measured live: **0 mis-sliced images on all 14 screens/states** (was 6–25 per screen).

| Step | What shipped |
|---|---|
| Legacy retire | the retire session's patch applied here (HudInstaller, MenuScreensInstaller, SceneFlowBuilder, LoadoutSlotView gone) |
| P0 overrides | Menu.unity screen instances 845 → 115 overrides (root-only kept). Before/after captures identical except the Shop grid now uses the prefab's width |
| P0 kit | `UISliceFit` (border always fits, 528 images), `UIBarClip` (fill = clip of a full pill; HUD HP/XP, Hub/Loadout stats, Pass, sliders, loading bar). Tools: `UiAudit`, `UiKitApply`, `UiTour` (play-mode capture + live slice check) |
| P1 look | `M8UiPolish`: LiberationSans gone (60 labels → Cairo), lip buttons in mockup colours, modals as cards on a scrim, slider knobs, settings close button, placeholder Language/Restore rows hidden, rank badges on slot corners, M8 tokens in `UITheme` |
| P2 icons | `WeaponIconsM8`: 54 guns × owned (real materials) + locked (pale rarity silhouette), fitted per gun, solid rarity tile behind every weapon card, the Hub plate and the buy modal. Card borders now follow the live tier (M1911 showed a stale green border) |
| P3 UX | loading screen = Bootstrap splash reused (PLAY/Replay/Home never freeze on the old screen), modal pop-in, level-up cards deal in, result time/kills/banked count up, 8 synthesised UI sounds (`Tools/gen_ui_sfx.py`, cues `sfx.ui.*`), Android haptics honouring the Vibration toggle, lip buttons sink when held, guns bought through a confirm modal (was: second tap spent coins with no hint), walk-away row shows coins lost |

Verified: EditMode 662/662; flow probe PLAY → Replay → Home: loader up on the first frame, gone
after 6.5 s / 0.9 s / 5.3 s, no exceptions.

Open:
- GitNexus index is broken (FTS repair failed), so `detect_changes` could not run; impact was
  checked by text search for every edited method.
- Skill icons still show initials until the owner's ChatGPT icons land in `UI/Icons/Skills/`.
- Splash title is text until the logo art exists. Menu load (Home) is slow (~5 s) — now covered, not fixed.
- P4 (roster) and part D (bundle id, keystore) unchanged.
