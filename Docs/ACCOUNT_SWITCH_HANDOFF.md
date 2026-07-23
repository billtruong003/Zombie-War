# ZombieWar — Account-switch handoff

> Canonical status as of 2026-07-23 EOD, after the world/framework push (`61be2cf2`).
> Read this file before older handoffs.

## 0-bis. 2026-07-23 milestone — READ FIRST (supersedes the UI section below)

Everything committed and pushed on `main` through `61be2cf2`. Session commits:
`ce1d6100` (owner-layout snapshot) → `205348c5` (installer resync) → `976e6d00`
(joystick/HUD-economy) → `20dd7f38` (toon rig + 5 desert maps) → `02fc1498` (embedded toon kit)
→ `79dc2c78` (rig color/intensity) → `54b03ba5` (occlusion bakes) → `61be2cf2` (owner UI fixes
+ build serialization). 191/191 EditMode tests green at every step.

### ⚠️ UI OWNERSHIP RULE (hard, permanent)

**The owner hand-authors ALL menu UI. `Menu.unity` and every `UI_*.prefab` under
`Assets/_Project/UI/Prefabs/Screens/` are OFF-LIMITS to any agent** — no layout fixes, no
aspect fitters, no color tweaks, nothing. One violation this session silently corrupted the
owner's hand layout via an editor-side scene save and had to be restored from snapshot.
The only UI work an agent may do is **tween/animation CODE (.cs only)** when explicitly asked.
Never open or save the Menu scene as a side effect; after any editor operation, check
`git status` and revert unintended UI-file changes immediately.
`Docs/UI_DIRECTION_02_HANDOFF.md` is now HISTORY — do not execute it.

### Done this session (2026-07-23)

- **All player-facing content is English**: 149 baked prefab strings, campaign stage/wave names,
  448 costume item + 30 set names, 20 Pass missions, 7 run perks, every modal/notice. Dev-facing
  logs/tooltips stay Vietnamese by design.
- **Icon pipeline v2**: costume icons re-captured face-on at 2048+MSAA → stepped bilinear
  downscale → unpremultiplied 512 PNG (448/448 bound, GUIDs stable). Weapon icons re-rendered as
  side profiles (muzzle right) on transparent background with roster-relative proportion
  (geometric-mean blend) and a Pillow stroke outline via `Tools/outline_icons.py`
  (**run exactly once after each regen** — running twice thickens the outline).
  Menu additions: Missing-only, Size-preview (2048→128 to `Assets/Screenshots/UIAudit/IconSizePreview`).
- **Hub/Pass runtime binding**: Hub mission card shows the nearest-complete active Pass mission
  (tap → Pass), notify dots (Loadout/Costume/Pass) driven by profile events, `+` buttons open
  Shop Weapons/Gacha. PassScreen binds real missions: progress bars, `x/y` counters, one-shot
  CLAIM (pays coin + PassXp, provisional 500 XP/level in `PassScreen.XpPerLevel`), UTC rollover
  on show. `UIFx.Punch` on currency ticks.
- **Installers resynced FROM the hand layout** (`205348c5`): a destructive rebuild now reproduces
  the owner's structure (dock HLG, Panel Reward HLG, PanelLoad HLG, nested Info, 3-col arsenal,
  bottom pager/random, no RESET button, quest rows with Counter+Claim). Never run them anyway
  without explicit owner request — see the ownership rule.
- **BillVirtualJoystick** (`Assets/ThirdParty/BillGameCore/Runtime/UI/`): floating origin,
  pointer-id lock, rect-based radius, dead-zone remap, handle range. `ZombieWar.VirtualJoystick`
  is a compatibility shell; PlayerMovement/HUD wiring untouched.
- **In-run economy visible**: HUD coin pill binds `RunState.Changed` (pickup → bank → HUD punch).
  Backend path Pickup→RunState→idempotent Payout was verified correct and untouched.
  HudController per-frame string allocations removed (cached shown values).
- **Toon light rig**: `ZombieWar.ToonLightRig` (ExecuteAlways, arrow gizmos, default 50/-30/0,
  Inspector color + intensity) pushes `_ToonLightDirection` + `_ToonLightColor` globals.
  Consumers: `VAT_EnemyToon`, `VAT_Toonlit`, and the whole **stylized-toon-world-kit** via one
  patch in `Packages/com.billtruong.stylized-toon-world-kit/Core/URPCompat.hlsl`
  (`STW_GetMainLight` overrides direction+color when rig active; distanceAttenuation forced 1;
  fallback to real main light when no rig). The kit is now **embedded** in `Packages/` (owner
  owns upstream repo; the single-hunk diff can be pushed back). Inverted-hull outline needs no
  light — unpatched. Directional light can be fully off; rig supplies color so nothing renders black.
- **All 5 campaign maps generated**: `Tools/ZombieWar/Generate All Campaign Maps` generates the
  desert environment IN PLACE (no cloning → per-map wiring preserved; WaveData binds at runtime
  via CampaignCatalog, verified), seeds 101–105, idempotent re-run, auto-places one ToonLightRig,
  NavMesh rebaked, 12/12 spawn paths reachable on every map.
- **Static perf pipeline complete per map**: BatchingStatic + shadow-caster stripping (generator)
  + **occlusion culling baked** for all 5 maps (~66–70KB Umbra each, smallestOccluder=5 uniform).
- **First Android build**: owner built + hand-fixed UI for device; build side-effect
  serialization committed in `61be2cf2`. Android bundle id is still the Unity template default
  (`com.UnityTechnologies.com.unity.template.urpblank`) — rename before any store upload.

### Known gaps after this session

- No PlayMode/profiler stress numbers yet (25/50/100 horde) — top priority before calling
  mobile-safe; maps + occlusion are ready for measuring.
- Pass reward TRACK (6 tiles) + premium strip remain presentation-only; missions are real.
- Food buffs, campaign selector screen, level-up perk overlay binding, result screen binding —
  unchanged backlog (see REMAINING_FEATURES.md).
- `Tools/notify_done.py` can exceed 120 s (RVC voice) — run it in background.

## UI handoff update — 2026-07-23 (SUPERSEDED — see §0-bis ownership rule)

`Docs/UI_DIRECTION_02_HANDOFF.md` described an agent-driven UI correction pass. That flow is
dead: the owner now hand-authors the UI directly. Keep the document only as design-intent
reference (English copy, accent colors, safe-area rules).

## 0. Enemy campaign milestone (2026-07-22) — READ FIRST

Everything below is committed and pushed on `main`. The blow-by-blow execution record (every fix,
every measured number, every deferred item) is `Docs/ENEMY_CAMPAIGN_TASK_STATE.md` — treat that
file as the canonical detail; this section is the summary.

### Done and verified (191/191 EditMode tests green; baseline was 134)

- **15 Cute-pack monsters baked** through `ZombieVATBaker` as MeshRenderer + VAT_Animator
  (zero Animator/SkinnedMeshRenderer, tests enforce it). **HUGO is blocked**: 16,567 verts exceeds
  the 16,384 texture-width limit + 3 Standard materials; documented in `Docs/ENEMY_ROSTER_AUDIT.md`
  (generated by the baker). Cactus Boss / Skeleton Giant carry the late campaign.
- **Five real VAT pipeline bugs found and fixed**: clips never looped (WrapMode.Default clamps);
  `_MainTex` never assigned (all bakes shipped untextured); `_Dissolve` didn't exist in any VAT
  shader (death dissolve was a silent no-op); Mole Rat FBX has no embedded material (falls back to
  pack texture); **normals were bind-pose static** — now a per-frame NORMAL map is baked next to the
  position map, so specular tracks the animation.
- **Shader** `ZombieWar/VAT/EnemyToon` (in `_Project`, vendor untouched): unlit albedo + stepped
  specular (authored 1.5 steps), per-instance `_HitFlash` and `_Dissolve` via MaterialPropertyBlock,
  noise-TEXTURE dissolve with independent X/Y tiling, dissolve clips ShadowCaster/DepthOnly too.
- **One shared look**: `VatLookConfig.asset` + `Tools/ZombieWar/Apply VAT Look` pushes spec/dissolve
  params AND dissolve/hit-flash durations onto all 15 materials + prefabs. Live-preview editing in
  `Tools/ZombieWar/Dissolve Test`. Blob shadows: one instanced material, fade with dissolve.
- **Behaviours by inheritance** (6 classes / 15 species): Walker; Runner→**Pouncer**
  (crouch/leap/recover); Ranged; **Burrower** (dive → invulnerable+untargetable underground travel →
  telegraphed emerge, never on top of the player); Boss (telegraphed slam) → **Charger**
  (locked-line dash). Wind-up-timed hits, exactly-once kill accounting, pool-safe resets.
- **Run loop**: `RunState` in-memory ledger (idempotent `Payout`, `Abandon` never banks),
  `RunDirector` bridges existing wave/game-over events, `RunPerkPool` 1-of-3. HUD/overlay binding is
  still TODO (RunOverlays perk UI remains placeholder).
- **Campaign backend**: `CampaignCatalog.asset` (5 stages, stable IDs, gates, rewards, weapon
  advice), `CombatPower` from sustained DPS (pellets/magazine/reload/stars — NOT raw damage),
  `PlayerProfile` additive persistence (completion, first-clear claim-before-grant, last-selected).
  The `Tools/ZombieWar/Combat Power Audit` window caught stages 4–5 being mathematically
  unreachable; gates rebalanced to the measured weapon ceiling (2372): 0/700/1100/1500/1900.
- **Scenes**: Map_Level2–5 cloned from Map_Level1 (contract preserved), per-stage WaveData
  (15/15 enemies, gradual introduction), spawn rings path-verified, build settings registered,
  RunSystems (RunDirector+MissionTracker+PickupManager) in all five scenes, WaveDirectors enabled.
- **Battle Pass backend**: 20 authored missions, deterministic UTC day/week rotation, independent
  daily/weekly reset, clamp-at-target progress, claim-once. Pass UI binding still TODO.
- **Pickups**: pooled coin/gem/health/bomb (`Resources/Pools/pickup_*`), manager-driven magnet loop
  (no colliders), collect-once, gem scale encodes value, wave-clear auto-collect. Coin banking moves
  from ledger to drops when a PickupManager is present (no double pay).
- **Destructibles**: `PROP_Crate_*` (loot tables) and `PROP_Barrel_Fuel*` (falloff explosion,
  chain detonation, hurts the player too) via `DestructibleProp : IDamageable` — same damage path
  as enemies.
- **Weapon ranges remapped to the portrait camera's real view band** (~6 m ahead): shotgun 5.5 …
  marksman 11; `aimRange` 15→11, `aimDangerRadius` 7→4 (Player.prefab updated). Range identity is
  now nearly flat — differentiate families by damage/ROF, not reach.
- **Desert map generator** `Tools/ZombieWar/Desert Map Generator` (full parameter window):
  tile-grid ground, cliff ring with **flush corners** (outer-face alignment at ±(wallEdge−0.01),
  yaw from measured L-mass direction, size snapped to 5 m so no extra wall chunk), shared
  `Occupancy` record across all scatter passes (no barrel/rock overlap — verified min pair 1.93 m),
  gameplay props placed before decoration, NavMesh rebake + per-spawn path check. Output scene:
  `Map_GenTest.unity`. Also `Tools/ZombieWar/Prefab Contact Sheet` (labelled grid renders; KayKit
  132-prefab sheet under `Assets/Screenshots/EnemyCampaign/SourceAudit/`).

### Approved next (owner-confirmed, NOT yet implemented)

Food buff system: green apple = instant heal (`Health.Heal` exists); blue berry = **shield 150 cap,
no duration, +10–15 per pickup, absorbs 100% before HP**, second bar under health (navy); red
apple = infinite ammo 8 s (snapshot mag on eat, restore exact count after); cheese = 2× coin drops
20 s. HUD: health → shield → square buff tiles (Image + child Text label, icons later). Shield
lives beside `Health` as a resource; only timed buffs go in a `PlayerBuffs` component.

### Honest gaps

- Phase 5 campaign selector UI (`UI_CampaignScreen.prefab`) not built — backend complete.
- Run loop not yet wired into HUD/level-up/result UI; Pass screen not bound to mission backend.
- No PlayMode/profiler evidence yet (Phase 11): no 25/50/100 stress numbers, no full-stage runs.
- Generated map is structurally correct but visually sparse at gameplay camera; density should be
  computed per visible frame, not per arena area.
- `detect_changes` before this commit: 308 symbols/91 files, risk "critical" — that is the
  cumulative multi-slice tree (all tests green), not a single regression.

> Historical status below (2026-07-21 Pro Casual milestone). Still accurate for
> commerce/costume/weapon-rig areas.

## Current Pro Casual status (2026-07-21)

- Active source is the Pro Casual `Characters.fbx`: 453 usable source parts across 20 internal slots.
- Only 448 parts across 18 slot definitions are player-facing. `Face` (the unique base mesh named
  `Head`) and `Body` (`Body_1..4`) are renderer infrastructure and must never appear in wardrobe,
  ownership, shop, gacha, item icons or saved loadout.
- The base Head is always applied. Body is selected automatically to prevent overlap:
  `Body_1` = no gloves/no shoes, `Body_2` = gloves/no shoes, `Body_3` = no gloves/shoes,
  `Body_4` = gloves/shoes. UI slot `Head` still means headgear/hats and remains player-facing.
- Economy currently contains 448 individual entries and 30 curated Pro outfit sets. Purchases use
  an explicit confirmation modal; weapon gacha duplicates grant weapon-specific upgrade shards.
- Costume wardrobe has four primary tabs: `ĐẦU / THÂN / CHÂN / BỘ`. Loose items never resolve to
  an arbitrary containing set; set cards are a separate offer type with their own full-outfit icon.
- Shop Costume has `ITEM LẺ / BỘ`, paged editor-authored card pools, Coin/Gem offers and a required
  confirmation modal. The active UI icon lookup is 448/448 Pro Casual item IDs with zero fallback.
- Shop Upgrades is live: owned weapons show shards, Gold cost, star level and DMG/ROF preview.
  Stars affect actual combat through `WeaponUpgradeMath` (L2: +15% damage/+5% fire rate;
  L3: +35% damage/+12% fire rate).
- The Editor window `ZombieWar/Dev/Player & Economy Tools` owns wallet, unlock and profile-reset cheats.

Sections below describing 323-item Free Casual/Fantasy commerce are migration history, not current truth.

## 1. Project snapshot

- Root: `D:\Project\ZombieWar`
- Unity: `6000.3.10f1`, URP, portrait-mobile target.
- Branch: `main`.
- Current HEAD: `ddba698f` (`Complete weapon rig and prefab-first UI foundation`).
- Framework: BillGameCore services plus BillTween. Do not add DOTween.
- Scene flow: `Bootstrap.unity` loads `Menu.unity` or `Map_Level1.unity` additively through the project flow.
- Game: top-down auto-aim/auto-fire zombie survivor with joystick movement, three weapon slots,
  modular costume, menu/shop/gacha and a bounded Level 1 test arena.

## 2. Worktree warning

The working tree contains the cumulative implementation from all slices after `ddba698f` and is
intentionally very dirty. At this handoff it contains roughly 425 status entries, including modified,
deleted and untracked assets. Nothing from the recent slices is staged or committed.

Do not reset, restore, clean, reimport, rename or delete files just to make Git status smaller.
In particular:

- deleted Fantasy generated costume icons are part of the ongoing Casual migration;
- the Pro Casual icons, set icons and the Casual catalog are new expected assets;
- existing scene, prefab, profile, economy and UI changes belong to completed slices;
- unrelated material/TMP/scene dirt also exists and must be preserved unless its ownership is proven.

Always inspect scoped diffs and use GitNexus impact analysis before code edits. Run
`detect_changes()` before any future commit.

### Newly imported enemy sources — pending VAT campaign task

- `Assets/Monsters Ultimate Pack 03 Cute Series`: 15 creature groups and 303 FBXs.
- `Assets/GAMWILL Character Pack Monster  Bionic Cartoon Zombie Gorilla`: HUGO T-pose plus 11
  animation FBXs.
- `Assets/KayKit/Packs/Bits/KayKit - Resource Bits (for Unity)`: 132 resource models/prefabs for
  semantically correct reward/pickup/Pass visuals.

These are source/vendor assets, not gameplay enemies. Every production monster must be baked through
the existing `ZombieVATBaker` pipeline and end as `MeshRenderer + VAT_Animator`; runtime Animator or
SkinnedMeshRenderer enemies are forbidden. Active contract: `Docs/ENEMY_CAMPAIGN_EXPANSION_PROMPT.md`.

## 3. Non-negotiable Player and weapon architecture

The weapon setup phase is complete.

- 25 canonical `WeaponData` assets and 25 canonical `WPN_*` prefabs exist.
- Stable weapon IDs use `weapon.<family>.<model>`.
- Every weapon has model pose, muzzle and grip data.
- `WeaponPoseAuthoring` Capture All stores model transform plus grip positions.
- Player hand-target rotations are global Player-rig data, not per-weapon arbitrary rotations.
- WeaponRig lives inside the Animator avatar hierarchy and builds a valid Animation Rigging graph.
- Weapon flow is one-way through `GunMount/RecoilPivot`; do not reintroduce a hand/socket cycle.
- One-handed and two-handed IK weights switch correctly.
- The right-hand WeaponSocket must remain under the real right hand, never under Chest.

Do not replace or rebuild Player skeleton, Animator, Avatar, WeaponRig, WeaponSocket, GunMount,
RecoilPivot, grip targets or weapon pose data unless a new reproducible regression proves it is needed.
Read `Docs/PlayerRigSocketIncident.md` before touching this area.

## 4. Authoritative profile, loadout and menu state

`PlayerProfile` is the versioned save authority through `Bill.Save`. It owns:

- Coin, Gold and Gem;
- weapon ownership;
- three equipped weapon slots;
- weapon upgrades;
- costume ownership/equipment;
- body/costume migration fields;
- gacha pity;
- unseen/new-item markers.

`LoadoutState` delegates persisted weapon slots to the profile. `PlayerSpawner` moves the spawned
Player into the gameplay scene, preventing orphan Players across additive Menu/Map cycles.

Completed menu/economy slices before the Casual migration:

- Loadout reads real ownership and equips through the authoritative slot contract.
- Weapon Shop performs atomic purchases.
- Currency widgets read `PlayerProfile` through `ProfileCurrencyProvider`.
- `EconomyConfig` and `GachaService` implement the previous Fantasy costume economy plus weapon
  and costume single/x10 pulls, rarity weights, pity and duplicate compensation.
- New-item dots exist for Loadout and Costume.
- Dev wallet tools seed Coin, Gold and Gem in Editor/dev builds.

The active costume commerce and costume gacha records are Pro Casual. Fantasy records remain only as
migration history/rollback data and must not be reintroduced into `UIPrototypeCatalog` or live offers.

Runtime evidence is under `Assets/Screenshots/CasualMigration/Commerce/` (loose items, full sets,
Costume `BỘ` tab and weapon upgrades). Verification at this checkpoint: 134/134 EditMode and 5/5
PlayMode tests pass. The only runtime console warnings observed are the pre-existing BillGameCore
`PanelSettings` theme warnings.

## 5. Pro Casual migration and commerce — current verified state

> The older 323-item Free Casual notes below are migration history. The live source is Pro Casual
> and the authoritative player-facing count is 448.

- 448/448 player-facing Pro Casual items have stable `itemId` values and real icons.
- The unique base Head and `Body_1..4` are renderer infrastructure, not wardrobe/shop/gacha items.
- Loose wardrobe items and the dedicated `BỘ` tab are separate offer domains.
- 30 curated outfit sets have semantic names and full-character set icons.
- Shop Costume has real `ITEM LẺ / BỘ` modes, Coin/Gem offers and a confirmation modal.
- Weapon gacha duplicates grant weapon-specific shards. The Upgrade tab consumes shards + Gold and
  applies real star scaling through `WeaponUpgradeMath`.
- Verified baseline: 134/134 EditMode and 5/5 PlayMode tests. Current commerce screenshots are under
  `Assets/Screenshots/CasualMigration/Commerce/`.

### Historical Free Casual migration notes

### Skeleton de-risking

The new Casual pack is under:

`Assets/ThirdParty/Layer Lab/3D Casual Character/3D Casual Character`

Casual has 23 deform bones named `QuickRigCharacter2_*`. All 23 exist on the current Player/Fantasy
deform skeleton. The Fantasy authoring prefab has additional controls/effectors, but those are not
required skin bones.

Casual meshes were rebound to the existing Player skeleton with `0.00000` rest-pose error across all
23 bones. T-pose and live idle were visually verified. The correct design is:

```text
Existing Player skeleton/Animator/WeaponRig
└─ Casual SkinnedMeshRenderers applied by CharacterModularApplier
```

Never nest a complete Casual character and second skeleton under Player.

### Casual catalog

Generator:

`Assets/_Project/Scripts/Editor/Character/CasualCatalogGenerator.cs`

Catalog:

`Assets/_Project/Data/Character/CasualCostumeCatalog.asset`

The generator scans the real Casual skinned source and produces 323 player-facing parts across ten
logical slots:

| Group | Slot | Vendor category | Count |
|---|---|---|---:|
| Head | Hair | Hair | 7 |
| Head | Face | Face | 10 |
| Head | Head | Headgear | 58 |
| Head | Eyewear | Eyewear | 20 |
| Body | Chest | Top | 74 |
| Body | Hands | Glove | 21 |
| Body | Back | Bag | 17 |
| Body | Body | Body | 4 |
| Legs | Legs | Bottom | 67 |
| Legs | Feet | Shoes | 45 |

Fourteen Body assembly/helper meshes are excluded from player-facing options. All 323 entries have
valid mesh/material/bone binding and unique stable `itemId` values. Casual parts share one FBX GUID,
so Unity GUID is ambiguous and must not be used as the Casual save/economy identity.

Examples of stable IDs:

- `casual.body.004`
- `casual.face.a01`
- `casual.chest.top.054`
- `casual.legs.bottom.062`

The generator is deterministic and idempotent.

### Casual starter outfit

Reset/fresh required appearance currently resolves to:

- Body: `Body_4`
- Face: `Face_A1`
- Hair: `Hair_1`
- Chest: `Top_54`
- Legs: `Bottom_62`
- Feet: `Shoes_1`

Head, Eyewear, Hands and Back default to `None`. Feet is optional, but Reset equips `Shoes_1`.
Required slots are Hair, Face, Chest, Body and Legs and may never become empty.

### Runtime integration

- `Player.prefab` references the Casual catalog.
- `MenuCharacterPreviewStage.prefab` references the Casual catalog.
- Menu preview shows the animated Casual starter correctly.
- Gameplay Player spawns as Casual and auto-fire was verified.
- Five representative weapon/IK screenshots exist for pistol, SMG, AR, shotgun and sniper.
- No Casual binding or Animation Rigging error was found during the Phase 4 sweep.

### Phase 4 Costume UI

The Costume screen is now data-driven from `catalog.slotDefinitions` for Casual catalogs. It shows:

```text
Đầu: Tóc / Khuôn mặt / Mũ / Kính
Thân: Áo / Găng tay / Ba lô / Cơ thể
Chân: Quần / Giày
```

- Fantasy-only Brow/Eye/Mouth/Beard/Earring presentation is hidden for Casual.
- Face is one complete Casual slot.
- Body shows exactly four choices.
- Required slots have no None option.
- Optional slots have `Không mang`.
- Equip, clear, reset and owned-only randomize update the preview and profile.
- Locked Casual items cannot debit Fantasy economy records; they remain honestly unavailable until
  Casual economy Phase 6.
- Profile safety tests confirm Casual repair/reset/randomize preserve wallet, weapons, upgrades and pity.

### Casual icon pipeline

Generator:

`Assets/_Project/Scripts/Editor/Character/CasualIconGenerator.cs`

Output:

`Assets/_Project/UI/Icons/Generated/CasualCostume/`

There are 323 real PNG/Sprite icons, one per player-facing Casual item. Icons are rendered editor-time
from a real Player-compatible preview, using slot-specific framing and the actual target mesh.
Contact sheets and screenshots are under:

`Assets/Screenshots/CasualMigration/`

The catalog generator preserves icon bindings across regeneration. Steady-state generation is
idempotent.

### Historical Phase-4 verification

- EditMode: `89/89` passing (`82` previous baseline plus `7` Casual tests).
- PlayMode: real UI/menu/gameplay flows were manually verified through Unity MCP; no new automated
  PlayMode test was added in Phase 4.
- Console: no relevant project error; only documented pre-existing/theme/MCP noise when applicable.
- No recent migration work is staged or committed.

## 6. Migration status and current execution target

The active Pro Casual catalog, wardrobe, loose/set commerce, gacha and weapon upgrades are complete.
Do not rerun the old Phase 6 plan below or rebuild it from the historical 323-item assumptions.
Phase 7 cleanup remains a later bounded audit: prove zero active Fantasy dependency before removing
any rollback asset.

The previous run-loop contract is `Docs/NEXT_PHASE_RUN_LOOP_PROMPT.md`. The active expanded contract,
after importing two new monster packs, is:

`Docs/ENEMY_CAMPAIGN_EXPANSION_PROMPT.md`

It must bake 15 Cute monsters plus HUGO through the existing VAT pipeline, finish only missing
run-loop prerequisites, and build the five-stage campaign/Pass mission loop without parallel wave,
wallet, UI, Animator-enemy or weapon systems.

### Historical migration plan — do not execute as current work

### Phase 6 — Casual Shop, Gacha and Economy

This phase is complete and retained only for provenance.

- Generate Casual commerce records keyed by stable Casual `itemId`.
- Define deterministic rarity and Gold/Gem price rules.
- Exclude starter and internal Body helper items.
- Replace active Fantasy costume gacha pool with the Casual pool.
- Preserve weapon Shop/Gacha behavior.
- Keep atomic purchase, x1/x10, pity and duplicate compensation.
- Verify save/reload ownership and immediate equip after purchase/pull.
- Replace raw vendor card names such as `Top_54` with authored/localized player-facing names where
  appropriate.
- Add an explicit old-Fantasy-profile-to-Casual migration test suite. Preserve wallet, weapons,
  upgrades and pity; unresolved Fantasy costume entries must fall back safely rather than grant all.

### Phase 7 — remove active Fantasy dependencies and update old docs

- Find every runtime/menu/profile/economy reference to the Fantasy costume catalog.
- Switch active costume dependencies to Casual.
- Keep the vendor Fantasy pack on disk for rollback until zero active dependency is proven.
- Update or mark historical Fantasy sections in `HANDOFF.md`, `HANDOFF_UI_CODEX.md`,
  `PROFILE_SAVE.md`, `ECONOMY_DESIGN.md` and related docs.
- Run complete tests, visual flow and final migration notification only when Phases 6 and 7 are done.

## 7. Current product sequence

The next product phase should be an authoritative gameplay vertical slice, not Addressables and not
map decoration first:

```text
Wave → enemy death → run Coin/XP → 1-of-3 perk → Defeat/Victory result → atomic payout → Hub
```

Recommended order:

1. Execute `Docs/ENEMY_CAMPAIGN_EXPANSION_PROMPT.md`: VAT roster, missing run state, five simple
   Plane stages, campaign selector, mixed waves, power gates/rewards and real Pass missions.
2. Replace each placeholder Plane with the owner's stage-specific tree/obstacle/environment design;
   retain the verified spawn/NavMesh/scene contracts.
3. Balance player, 25 weapons, enemies, rewards, XP thresholds, perks and wave milestones using
   measured run data.
4. Complete the remaining Pass track/premium decision, revive scope and run polish after the real
   mission list exists.
5. Add content, VFX/SFX/haptics and performance profiling, then decide Addressables near the end.

`Map_Level1` is currently a functional test arena, not a finished production map.

## 8. Important paths

| Area | Path |
|---|---|
| Player | `Assets/_Project/Prefabs/Player.prefab` |
| Player preview | `Assets/_Project/UI/Prefabs/Preview/MenuCharacterPreviewStage.prefab` |
| Casual catalog | `Assets/_Project/Data/Character/CasualCostumeCatalog.asset` |
| Fantasy rollback catalog | `Assets/_Project/Data/Character/ModularCostumeCatalog.asset` |
| Casual catalog generator | `Assets/_Project/Scripts/Editor/Character/CasualCatalogGenerator.cs` |
| Casual icon generator | `Assets/_Project/Scripts/Editor/Character/CasualIconGenerator.cs` |
| Casual icons | `Assets/_Project/UI/Icons/Generated/CasualCostume` |
| Costume screen | `Assets/_Project/UI/Prefabs/Screens/UI_CostumeScreen.prefab` |
| Costume runtime | `Assets/_Project/Scripts/Runtime/UI/Screens/CostumeScreen.cs` |
| Modular applier | `Assets/_Project/Scripts/Runtime/Character/CharacterModularApplier.cs` |
| Profile | `Assets/_Project/Scripts/Runtime/Systems/PlayerProfile.cs` |
| Economy | `Assets/_Project/Data/Economy/EconomyConfig.asset` |
| Gacha | `Assets/_Project/Scripts/Runtime/Systems/GachaService.cs` |
| Weapon roster | `Docs/WeaponRosterMapping.json` |
| Rig history | `Docs/PlayerRigSocketIncident.md` |
| Casual evidence | `Assets/Screenshots/CasualMigration` |
| Gameplay scene | `Assets/_Project/Scenes/Map_Level1.unity` |
| Run-loop prerequisite | `Docs/NEXT_PHASE_RUN_LOOP_PROMPT.md` |
| Active enemy/campaign prompt | `Docs/ENEMY_CAMPAIGN_EXPANSION_PROMPT.md` |

## 9. Collaboration contract for the next account

The owner uses the assistant as both technical reviewer and prompt orchestrator. Work at this level:

- Answer the requested decision first, then explain the evidence and trade-off.
- Speak direct, natural Vietnamese; technical English names are fine.
- Do not blindly agree. Correct a wrong assumption with source, MCP, screenshot or runtime evidence.
- Before giving an execution prompt, state the recommended model. Use Opus for architecture,
  migration, Unity prefab/scene work and difficult debugging; use Sonnet only for bounded mechanical
  work with strong tests.
- Every implementation prompt must require the `expert-developer` skill, GitNexus impact checks,
  Unity MCP verification, screenshots where visual correctness matters, explicit acceptance criteria,
  stop conditions, no unrelated cleanup and no stage/commit/push unless requested.
- A report from another model is evidence to audit, not truth to repeat. Check contradictions,
  incomplete verification and scope drift before recommending the next phase.
- Do not produce a prompt immediately when a game-design choice is still ambiguous. Explain the
  options and obtain confirmation first. For implementation details discoverable from source, inspect
  them instead of asking the owner.
- Use `simplifier-vi` Level 1 when translating a technical report for the owner.
- Keep editor-visible authoring: prefabs, cameras, RenderTextures and UI structure should exist before
  Play Mode. Runtime should apply state, not secretly build the whole project from nothing.
- Preserve the stable Player/weapon rig unless measured evidence proves a regression.
- Prefer one well-scoped verified slice over a giant task that can only be superficially checked.

## 10. Required reading order for a fresh account

1. `AGENTS.md`
2. `CLAUDE.md`
3. `Docs/ACCOUNT_SWITCH_HANDOFF.md` (this file)
4. `Docs/ENEMY_CAMPAIGN_EXPANSION_PROMPT.md` for the active task contract
5. `Docs/NEXT_PHASE_RUN_LOOP_PROMPT.md` for the absorbed run-loop prerequisite
6. `Docs/PlayerRigSocketIncident.md`
7. `Docs/PROFILE_SAVE.md`
8. `Docs/ECONOMY_DESIGN.md`
9. `Docs/HANDOFF_UI_CODEX.md` for UI architecture, while treating its Fantasy-specific item counts as history
10. `Docs/UI_REDESIGN_SPEC.md`
11. `Docs/GAMEPLAY_DESIGN.md`
12. `Docs/WEAPON_DESIGN.md` and `Docs/WeaponRosterMapping.json`

Then inspect Git status, Unity state, GitNexus freshness and the actual source before proposing work.
