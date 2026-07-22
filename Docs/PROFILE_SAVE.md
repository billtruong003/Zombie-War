# Player profile & save contract

> Added 2026-07-20 (slice 1 of the UI/state wiring phase). This is the authoritative description of
> persistent player state. Anything not stored here is prototype-only.

> **2026-07-21 correction:** active costume identity is Pro Casual stable `itemId`, not the historical
> Fantasy GUID model described later. The profile also persists weapon-specific shards and star
> levels; `WeaponUpgradeMath` applies them to real combat. Run XP, temporary perks, kills and
> uncommitted earnings are intentionally not profile fields. The next slice commits only a terminal
> run payout atomically; see `NEXT_PHASE_RUN_LOOP_PROMPT.md`.

## Authority

`PlayerProfile` (static, `Assets/_Project/Scripts/Runtime/Systems/PlayerProfile.cs`) is the single
versioned owner of persistent player state. It stores one JSON DTO through **`Bill.Save`**
(BillGameCore `SaveService`, PlayerPrefs-backed) under key `zw.profile` (physical PlayerPrefs key:
`s0_zw.profile` because SaveService prefixes the save slot).

`LoadoutState` remains the public seam for gameplay/UI (`ApplyTo`, `Resolve`, `GetWeaponId`,
`SetWeaponId`, `Parts`, `GetPart`, `SetPart`) but delegates all storage to `PlayerProfile`.
`PlayerSpawner → LoadoutState.ApplyTo(weapon)` is still the one gameplay application path.

## Schema (version 1)

| Field | Type | Notes |
|---|---|---|
| `version` | int | `PlayerProfile.SchemaVersion` = 1 |
| `coin`, `gold`, `gem` | long | Never negative. `Add` clamps overflow at `long.MaxValue`; `TrySpend` is atomic (false = no change) |
| `ownedWeaponIds` | List<string> | Canonical `WeaponData.WeaponId` only; deduped on load |
| `pistol`, `longA`, `longB` | string | 3 equipped slots. Slot 0 never empty; "" = empty long slot (valid) |
| `ownedCostumeGuids` | List<string> | Asset GUIDs from `ModularCostumeCatalog` |
| `equippedParts` | List<PartSel> | logical slot + GUID |
| `weaponUpgrades` | List<{weaponId, level}> | Persistent star level; affects combat through `WeaponUpgradeMath` |
| `weaponShards` | List<{weaponId, count}> | Weapon-specific duplicate shards consumed by star upgrades |
| `ownedBodyColors`, `ownedBodyEars` | List<string> | White/Normal always owned implicitly |
| `gachaPity` (Slice 6) | List<{poolId, count}> | Per-pool pity counter; reset when a pull ≥ `pityMinRarity` |
| `unseenItems` (Slice 7) | List<string> | Gacha-granted ids not yet viewed (new-item badge). Weapon id has `.`; costume id is GUID/`body:`/`ear:` |

Migration: profiles saved before these fields load with null lists → `Normalize()` seeds empty
lists (no version bump needed; additive JSON fields default to null then dedupe).

Change events: `PlayerProfile.WalletChanged`, `PlayerProfile.LoadoutChanged`, `PlayerProfile.CostumeChanged`.

## Legacy migration

Runs once, on first profile load, only when no valid profile exists. Sources (read-only, **never
deleted or rewritten** — kept for rollback): `zw.loadout`, `wallet_coin`, `wallet_gold`, `wallet_gem`.

- Weapon slot ids are copied verbatim (may be pre-migration asset names). They are canonicalized on
  the first `ApplyTo` via `PlayerProfile.EnsureValidLoadout` using `LoadoutState.Resolve` +
  `WeaponData.LegacyAliases` (migrate-on-load). Unknown ids are kept, warned once, never replaced.
- Equipped costume parts are preserved; their GUIDs are seeded as owned (only these — never all 978).
- Negative legacy currency imports as 0 with a warning. Corrupt/partial JSON recovers to defaults
  with one warning; legacy keys stay intact so a corrupt profile rebuilds from them.

## Starter rule (fresh profile)

`PlayerProfile.EnsureValidLoadout(arsenal)` runs on the first `ApplyTo` **and** when the Loadout
screen opens (so a fresh profile is valid in the menu before ever entering gameplay). If slot 0 is
empty, the starter is the one-handed weapon with the **lowest `CatalogOrder`** in the arsenal
(order-independent — the Player roster and the Loadout card array serialize in different orders;
currently resolves to `weapon.sidearm.pistol_a`). It is written canonically to slot 0 and added to
owned. Every equipped weapon that resolves is auto-owned (equipped ⇒ owned invariant).

## Slot compatibility & equip rules (Slice 2)

- Slot 0: one-handed only (`!twoHanded`), never null/empty.
- Slots 1–2: two-handed only; empty is valid (`SwitchWeapon` skips empty slots).
- UI equips go through `LoadoutState.TryEquip(slot, WeaponData)` → `Equipped / NotOwned /
  Incompatible / InvalidSlot / InvalidWeapon`. Failure changes no state.
- **Duplicate rule:** one weapon occupies one slot — equipping into a long slot while it sits in the
  other long slot *moves* it (the old slot is cleared; no silent swap, no copy).
- `Weapon.EquipToSlot` remains the gameplay-side enforcement of the same contract.

## Costume presentation model (Slice 4.2)

**Raw catalog (978 mesh) ≠ player-facing options (854).** Raw Body = 132 mesh (6 màu × 22:
`_1` + `_Head_1` + `_Head_2` + 19 assembly pieces). UI KHÔNG hiện 132 mesh — Body là **composite**:
6 màu (`Body_<Color>.png` vendor icon) + 2 tai (Normal/Elf). Player-facing = 846 non-Body + 6 màu
+ 2 tai = 854. Mesh resolve: màu → `Body_<Color>_1` (full body, chứa cả bàn chân) + tai →
`Body_<Color>_Head_<1|2>`. Assembly pieces (Arm/Leg/Top/Bottom/Neck/Hand) đánh dấu non-presentable,
không xoá khỏi catalog/ThirdParty.

**Icons = OFFICIAL VENDOR SCREENSHOT** (Layer Lab `ScreenShot/`), không generated (mắt/miệng đen bị
bỏ). `ZombieWar/UI/Authoring/Bind Vendor Costume Icons`: 846/846 non-Body (`<name>.png`) + 6 màu Body.
Generated costume PNGs đã xoá (weapon icons giữ). Fallback = `rounded_dashed` trung tính.

**Slot 2 loại:** essential (Hair/Brow/Eye/Mouth/Chest/Legs) có ô "Mặc định" — không bao giờ trống;
optional (Beard/Eyewear/Earring/Head/Hands/Back/Feet) có ô "Không mang" — mặc định trống. Ô ảo
Mặc định/Không mang không phải catalog entry, không sở hữu, không giá/khoá.

**Profile Body:** 2 field `bodyColor` (default White) + `bodyEar` (default Normal), owned qua
`ownedBodyColors`/`ownedBodyEars` (White/Normal luôn owned). `TryEquipBodyColor`/`TryEquipBodyEar`
atomic (validate màu/tai + owned + resolve 2 mesh, 1 save, 1 event, rollback; đổi màu giữ tai,
đổi tai giữ màu — không lệch màu). `TryEquipLook` = randomize cả look 1 batch.

**Default ownership CHỐT (9 guid + White/Normal):** Hair_Black_1, Eye_Black_1, Brow_Black_1,
Mouth_Black_1, Chest_61, Legs_62, Feet_1/2/3 (Feet free nhưng KHÔNG mặc). Mọi thứ khác locked.
**Default outfit:** Body White_1 + White_Head_1, Hair/Eye/Brow/Mouth Black_1, Chest_61, Legs_62;
Feet + optional = trống (bàn chân từ body mesh). Không mannequin, không mũ/râu/kính.

**Applier:** disable HẾT baked renderer (Body/Brow/Eye/Mouth) → applier là nguồn appearance DUY
NHẤT (không trùng baked). Body composite = `Costume_Body` + `Costume_BodyHead` (đúng 1 mỗi loại).
`ApplySavedParts` reconcile per-slot (mỗi Apply tự Clear slot với SetActive+Destroy → không flash),
gỡ slot không còn equip. `MigrateRawBodyEquip`: profile 4.1 có Body GUID trong equippedParts →
rút thành bodyColor/ear, map `Body_<Color>_1` owned → ownedBodyColors; idempotent.

**Reset:** runtime "MẶC ĐỊNH" (`TryResetOutfitToDefaults`) = equipped về default + Body White/Normal,
optional về trống, OWNERSHIP giữ. Dev "Reset Costume Progress To Design Defaults" = ownership :=
đúng 9 guid + White/Normal (xoá dev-unlock), ví/súng giữ. Dev "Unlock All" = 846 non-Body + 6 màu
+ 2 tai (không 132 assembly).

**Preview sống:** `CostumePreviewDragRotator` trên PreviewRT (kéo ngang xoay Y, giữ vị trí, quán
tính, reset về facing authored). `CostumePreviewIdleDirector` (PlayableGraph, KHÔNG đụng
PlayerAnimator) base idle + crossfade showcase 7–12s (Idle_Look/Yawn/Idle2), không lặp liền,
tắt root motion.

**Costume Shop/giá vẫn deferred** — locked item hiện vendor icon + dim, không giả vờ bán.

## Weapon purchase (Slice 3)

`PlayerProfile.TryPurchaseWeapon(weaponId, price)` is the ONLY purchase path — atomic:
validate → check duplicate → check balance → deduct Coin → add ownership → **one** `SaveNow` →
events after commit. Results: `Purchased / AlreadyOwned / InsufficientFunds / InvalidWeapon /
InvalidPrice / SaveFailed` (save failure rolls back in-memory, no events). Rules:

- Price authority = `WeaponData.price` (Coin). `price == 0` = free/starter. `unlockCost` is ignored.
- Duplicate purchase charges exactly zero. Failure paths change nothing and emit nothing.
- Events: `WalletChanged` once (only if price > 0) then `LoadoutChanged` once.
- Shop UX: tap card = select, tap the selected card again = buy. Purchase never auto-equips —
  equipping stays in Loadout.

## Currency provider (Slice 3)

`ProfileCurrencyProvider` is the production `CurrencyClusterWidget.DefaultProvider`: reads
`PlayerProfile.Coin/Gold/Gem`, `Changed` forwards `WalletChanged` (no polling). Widgets subscribe
OnEnable/unsubscribe OnDisable — hidden widgets refresh on re-show. Runtime production code no
longer reads `wallet_*` keys; those remain migration inputs only. `PlayerPrefsCurrencyProvider`
is kept solely for tests/compat. `cheatUnlockAll` no longer affects Loadout or Shop runtime state
(installer bake still uses it; runtime overrides it). Shop Gacha/Costume/Upgrades tab buttons are
disabled (non-interactable) until real backends exist.

Dev tools (Editor menu `ZombieWar/Dev/...`): Reset Player Profile · Reset + Seed Test Wallet
(5.000 Coin) · Add 1.000 Coin — all via PlayerProfile APIs, never raw PlayerPrefs.

## Costume domain (Slice 4)

Identity = **catalog asset GUID** (`ModularCostumeCatalog` = source of truth cho identity/slot/mesh/
isBaseBody; 14 wardrobe slot, 978 part; `Wield_Gear*` excluded). Profile = source of truth cho
ownership (`ownedCostumeGuids`) va equipped (`equippedParts`: 1 part/slot). UI khong bao gio la authority.

- `TryEquipCostume(catalog, guid)` — slot LUON resolve tu catalog (khong the equip sai slot);
  validate ownership; 1 save; `CostumeChanged` sau commit; rollback khi save fail. Results:
  `Equipped/AlreadyEquipped/NotOwned/InvalidPart/InvalidSlot/CannotClearBaseBody/SaveFailed`.
- `TryClearCostumeSlot` — slot `Body` (isBaseBody) KHONG clear duoc (chi thay part khac);
  slot optional clear ve default prefab.
- `TryEquipOutfit(catalog, list)` — batch nguyen bo: validate het truoc, 1 save, 1 event
  (Randomize dung duong nay: moi slot boc 1 part OWNED, nut o day man = whole-outfit theo spec §4.3).
- **Defaults authority (4.1):** `ModularCostumeCatalog.defaults` (author bang
  `ZombieWar/UI/Authoring/Author Costume Defaults` — resolve ten design → guid, fail ro khi thieu).
  Owned mac dinh CHINH XAC 23 part: Hair `Hair_Black_1` (DUY NHAT), Chest 61–66, Legs 62–67,
  Feet 1/2/3/4/6/7/55, Head 38/53/55. Moi part khac (955) khoi tao LOCKED — phai mua/thuong/
  gacha/achievement/dev-unlock sau nay. Bo do MAC san bat buoc: Hair_Black_1 + Chest_61 +
  Legs_62 + Feet_1; Head owned nhung KHONG tu doi mu; slot optional de trong.
- **No-naked invariant:** Hair/Chest/Legs/Feet khong bao gio trong.
  `EnsureValidCostumeLoadout(catalog)` (idempotent, goi o CostumeScreen.OnShow va trong
  `ApplySavedParts` — moi diem ap costume) cap ownership default con thieu + sua slot bat buoc
  trong/hong ve default; GIU nguyen do da mua va optional hop le; khong doi gi → khong save/event.
  Clear slot bat buoc = tro ve default (khong bao gio trong). Body van prefab-underlayer khi
  chua equip — underlayer chi la luoi an toan, KHONG phai outfit chinh thuc.
- **2 loai reset TACH BIET:** (1) Runtime "MẶC ĐỊNH" (nut authored tren CostumeScreen, header TR)
  = `TryResetOutfitToDefaults` — equipped := dung bo mac dinh, OWNERSHIP GIU NGUYEN (do mua/
  unlock-all khong mat), 1 save/1 event, idempotent, rollback. (2) Dev
  `ZombieWar/Dev/Reset Costume Progress To Design Defaults` = `ResetCostumeProgressForDev` —
  ownership := CHINH XAC bo default (xoa dev-unlock), equip bo mac dinh, VI TIEN + SUNG +
  loadout + field khac GIU NGUYEN.
- `UnlockAllCostumes(catalog)` — DEV-ONLY test tool, 1 batch/1 save/1 event, idempotent
  (`ZombieWar/Dev/Unlock All Costume Parts`); khong lien quan production ownership.
- Missing GUID trong save: applier skip an toan, save GIU nguyen id (khong pha slot khac).
- `CostumeChanged` la event rieng — man sung (LoadoutChanged) khong refresh oan khi doi do.

`CharacterModularApplier` fixes (Slice 4): (1) `Clear`/`ClearAll` don theo TEN child `Costume_<slot>`
— bat bien "1 renderer/slot" ke ca khi nhieu caller xen ke; (2) GO part moi **ke thua layer** cua
character — preview camera cull theo layer `CharacterPreview` nen truoc day part ap len preview
bi tang hinh (bug tu truoc, nay preview hien dung outfit).

UI: `CostumeScreen` 2 tang chon — 3 tab nhom (Đầu/Thân/Chân) + hang 8 chip logical slot + nut
"MẶC ĐỊNH" (bake trong prefab boi `ZombieWar/UI/Authoring/Ensure Costume Slot Selector`, idempotent).
Grid luon loc DUNG 1 slot, pool 18 cell + paging, reset trang 1 khi doi slot. Locked = giu ICON THAT
cua part, toi mau + shake (chua co gia — Costume Shop van deferred, khong fake mua ban).

**Icons (4.1): phu 100%** — moi part hop le (978/978) co icon that render tu dung mesh+material cua
part do (`Generate Missing Costume Thumbnails` resume-safe / `Regenerate All Costume Thumbnails`),
path deterministic `C_<guid8>_<name>.png` duoi `Assets/_Project/UI/Icons/Generated/Costume/`.
Fallback runtime = sprite TRUNG TINH (rounded_dashed), KHONG bao gio la helmet/icon semantic khac;
validator coi part hop le thieu icon la LOI (khong chap nhan "representative coverage").

## Dev reset

Menu `ZombieWar/Dev/Reset Player Profile` (or `PlayerProfile.ResetForDev()`, Editor/dev builds only):
deletes the profile key, keeps legacy keys, so the next load re-runs migration.

## Tests

`Assets/_Project/Scripts/Tests/EditMode/PlayerProfileTests.cs` (assembly `_Project.Tests.EditMode`,
in-memory `ISaveService` + injected legacy readers — never touches real PlayerPrefs): 16 tests
covering fresh profile, round trip, migration (canonical/legacy names/empty slots/corrupt/partial/
unknown alias/negative currency/double run), wallet rules, overflow, starter seeding, idempotency.

## Player scene ownership (FIXED in Slice 2)

`PlayerSpawner` instantiates the player while **Bootstrap** is still the active scene
(`Bill.Scene.LoadAdditive`'s `SetActiveScene` callback runs after the new scene's `Start`), which
used to leave spawned players in Bootstrap where map unload never destroyed them (orphan
accumulation on restart/return). Fixed: `PlayerSpawner.Spawn` now calls
`SceneManager.MoveGameObjectToScene(Current, gameObject.scene)` with validity guards (root object,
target scene valid+loaded, loud error otherwise). Verified: 3 full Menu↔Map cycles = exactly 1
player in `Map_Level1` during play, 0 players/Weapon/PlayerMovement instances after unload.
PlayMode tests: `PlayerSpawnerSceneTests` (scene ownership + 3-cycle no-orphan).

## Campaign & Battle Pass fields (added 2026-07-22)

Additive `ProfileData` fields, all normalized on load (null-safe, deduped):

| Field | Type | Contract |
|---|---|---|
| `completedLevelIds` | `List<string>` | Stable level IDs (`level.1`..), NEVER indices. `MarkLevelCompleted` is idempotent. |
| `claimedFirstClearIds` | `List<string>` | `TryClaimFirstClear` writes the claim BEFORE granting currency — an interruption loses a reward rather than allowing a repeat. Pays exactly once, ever. |
| `lastSelectedLevelId` | `string` | Campaign screen restores selection from this. |
| `missionProgress` | `List<MissionProgressEntry>` | Keyed by mission ID, clamped at target. |
| `claimedMissionIds` | `List<string>` | Claim-once; cleared per scope on rollover. |
| `missionDayKey` / `missionWeekKey` | `int` | UTC epoch day / week. `RefreshMissionWindow` clears ONLY the scope that expired (a new day must not wipe weekly progress). |
| `passXp` | `int` | Battle Pass XP total; clamped >= 0 on load. |

Run-earned currency NEVER writes these directly: it goes through `RunState.Payout()` (idempotent,
once per run) into the wallet. Mission catalog itself is code (`PassMissions`), not saved.
