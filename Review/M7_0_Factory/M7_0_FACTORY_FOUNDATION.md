# M7.0 — Weapon Factory foundation: gates, catalog, tier correction

**Status:** DELIVERED 2026-08-15. **M7.1 NOT STARTED.**
Companion: `G5_GRIP_VALIDATION.md` (the manual gate and its defect list).

---

## 1. G1-G8 over all 25 shipped weapons

`ZombieWar/Weapons/Factory/Run Onboarding Gates G1-G8` -> `g1_g8_gate_results.csv`.

**G1 was split into two halves during this pass.** As one boolean it reported "25/25 blocking
failure" with an empty diagnosis, which is useless. The two halves fail for completely different
reasons, and only one of them is actually failing:

| Gate | Kind | Blocking | Pass | Finding |
|---|---|---|---:|---|
| **G1a** material shader contract | AUTOMATIC | yes | **25/25** | Every weapon is on `StylizedToonWorldKit/Toon/Toon Lit`. Clean. |
| **G1b** no vendor-owned material | AUTOMATIC | yes | **0/25** | **Every shipped weapon references a `.mat` under `Assets/ThirdParty`.** The material has already been converted to the toon shader, so it *looks* right, but the asset is owned by a vendor pack. A pack reimport would silently restyle the whole arsenal. |
| **G2** outline present | AUTOMATIC | yes | **25/25** | Clean. |
| **G3** mesh-signature duplicate | AUTOMATIC | yes | **24/25** | The known `BenelliM4` / `Generic` collision, now recorded as a variant group. |
| **G4** grip + muzzle authored | AUTOMATIC | yes | **25/25** | Confirms the 25/25 state the M6.1 audit reported. |
| **G5** grip looks correct | **MANUAL** | yes | **11/25** | See `G5_GRIP_VALIDATION.md`. First time ever run. |
| **G6** triangle budget | AUTOMATIC | advisory | **25/25** | 2 133 - 11 614 tris, all within budget. |
| **G7** silhouette separation | HEURISTIC | advisory | **8/25** | 17 weapons share near-identical proportions with a same-family sibling. Independently corroborates the M6.1 vision review. |
| **G8** colour distinctness | HEURISTIC | advisory | **0/25** | The arsenal is uniformly grey, exactly what the vision review concluded. The 0.06 threshold is mine and is a starting point, not a locked bar. |

| Weapon | G1a | G1b | G2 | G3 | G4 | tris | G7 | G8 |
|---|---|---|---|---|---|---:|---|---|
| `WD_Sidearm_PistolA` | True | False | True | True | True | 3120 | True | False |
| `WD_SMG_Generic` | True | False | True | True | True | 2744 | True | False |
| `WD_AssaultRifle_Generic` | True | False | True | True | True | 5130 | True | False |
| `WD_Shotgun_Generic` | True | False | True | True | True | 2133 | True | False |
| `WD_Marksman_SniperGeneric` | True | False | True | True | True | 3836 | True | False |
| `WD_LMG_Generic` | True | False | True | True | True | 11614 | True | False |
| `WD_Sidearm_Glock19` | True | False | True | True | True | 2516 | False | False |
| `WD_Sidearm_P226` | True | False | True | True | True | 2330 | False | False |
| `WD_Sidearm_M1911` | True | False | True | True | True | 3092 | False | False |
| `WD_Sidearm_BerettaM9` | True | False | True | True | True | 2680 | False | False |
| `WD_Sidearm_USP45` | True | False | True | True | True | 3482 | False | False |
| `WD_Sidearm_DesertEagle` | True | False | True | True | True | 3532 | True | False |
| `WD_Sidearm_FiveSeven` | True | False | True | True | True | 2942 | False | False |
| `WD_Sidearm_Makarov` | True | False | True | True | True | 2958 | False | False |
| `WD_Sidearm_Python357` | True | False | True | True | True | 2874 | False | False |
| `WD_Shotgun_BenelliM4` | True | False | True | False | True | 2133 | False | False |
| `WD_Shotgun_Mossberg500` | True | False | True | True | True | 2964 | False | False |
| `WD_Shotgun_SPAS12` | True | False | True | True | True | 4532 | False | False |
| `WD_Shotgun_DoubleBarrel` | True | False | True | True | True | 2262 | False | False |
| `WD_Shotgun_AA12` | True | False | True | True | True | 4506 | False | False |
| `WD_AssaultRifle_M4A1` | True | False | True | True | True | 8104 | True | False |
| `WD_AssaultRifle_AK47` | True | False | True | True | True | 5620 | False | False |
| `WD_AssaultRifle_SCARL` | True | False | True | True | True | 7602 | False | False |
| `WD_AssaultRifle_FAMAS` | True | False | True | True | True | 3664 | False | False |
| `WD_AssaultRifle_G36C` | True | False | True | True | True | 6348 | False | False |

> **G1b and G8 are new information, not restatements.** G1b was never checked before; G8 turns a
> written impression from the M6.1 vision review into a measured, repeatable number.

## 2. The authoritative catalog

`Assets/_Project/Resources/WeaponCatalog.asset` - 25 entries, contract clean.

```text
Entry { weaponId . data . family . variantGroupId . baseWeaponId . catalogOrder . unlockMethod . tier }
```

| Rule | How it is enforced |
|---|---|
| `weaponId` is save identity, never reused | `BuildCatalog()` copies it from the asset and never generates one; a `WeaponData` without an id is refused, not invented |
| `catalogOrder` is presentation only | The starter is an explicit `unlockMethod == Starter` flag. Test `ReorderingCatalogOrder_DoesNotChangeTheStarter` locks this |
| Weapon 26 = one entry, no consumer edits | `BuildCatalog()` is idempotent and rebuilds from the asset folder |
| Variants never inflate the family count | `DisplayEntries()` folds variants into their base |

**Rebuilds are identity-safe.** Re-running `BuildCatalog()` refreshes only derived fields (data
reference, family, tier, order). `weaponId`, `variantGroupId`, `baseWeaponId` and `unlockMethod`
survive, so it can be run against a live save.

## 3. The duplicate shotgun

`weapon.shotgun.generic` is now a variant of `weapon.shotgun.benelli_m4` (same mesh: 2 133 tris,
bounds 0.082 x 0.302 x 1.465), marked `Disabled` so it is folded out of shop listings.

**Neither id was deleted or reused.** Four tests hold that line, including
`DemotedVariant_StillResolvesToItsOwnWeaponData_NotTheBase` - silently swapping the demoted weapon
for its base would change what an existing owner has.

## 4. Tier correction - 16 of 24 were wrong

Authored `tier` now equals the measured band from `Review/M6_DecisionLock/weapon_tier_ladder.csv`.

| Weapon | powerBudgetUsed | tier before | tier after | | price (unchanged) |
|---|---|---|---|---|---:|
| `WD_Sidearm_PistolA` | 0.205 | Common | **Common** | kept | 0 |
| `WD_SMG_Generic` | 0.355 | Common | **Uncommon** | CHANGED | 0 |
| `WD_AssaultRifle_Generic` | 0.465 | Common | **Uncommon** | CHANGED | 0 |
| `WD_Shotgun_Generic` | (variant) | Common | **Common** | kept | 0 |
| `WD_Marksman_SniperGeneric` | 0.388 | Common | **Uncommon** | CHANGED | 0 |
| `WD_LMG_Generic` | 0.488 | Common | **Uncommon** | CHANGED | 0 |
| `WD_Sidearm_Glock19` | 0.238 | Common | **Common** | kept | 100 |
| `WD_Sidearm_P226` | 0.252 | Common | **Common** | kept | 150 |
| `WD_Sidearm_M1911` | 0.273 | Uncommon | **Common** | CHANGED | 250 |
| `WD_Sidearm_BerettaM9` | 0.271 | Common | **Common** | kept | 180 |
| `WD_Sidearm_USP45` | 0.273 | Uncommon | **Common** | CHANGED | 300 |
| `WD_Sidearm_DesertEagle` | 0.290 | Rare | **Common** | CHANGED | 700 |
| `WD_Sidearm_FiveSeven` | 0.328 | Uncommon | **Common** | CHANGED | 350 |
| `WD_Sidearm_Makarov` | 0.223 | Common | **Common** | kept | 80 |
| `WD_Sidearm_Python357` | 0.259 | Rare | **Common** | CHANGED | 650 |
| `WD_Shotgun_BenelliM4` | 0.257 | Common | **Common** | kept | 400 |
| `WD_Shotgun_Mossberg500` | 0.300 | Uncommon | **Common** | CHANGED | 550 |
| `WD_Shotgun_SPAS12` | 0.426 | Rare | **Uncommon** | CHANGED | 900 |
| `WD_Shotgun_DoubleBarrel` | 0.254 | Uncommon | **Common** | CHANGED | 500 |
| `WD_Shotgun_AA12` | 0.595 | Epic | **Rare** | CHANGED | 1400 |
| `WD_AssaultRifle_M4A1` | 0.543 | Uncommon | **Rare** | CHANGED | 600 |
| `WD_AssaultRifle_AK47` | 0.535 | Uncommon | **Rare** | CHANGED | 650 |
| `WD_AssaultRifle_SCARL` | 0.622 | Rare | **Rare** | kept | 1000 |
| `WD_AssaultRifle_FAMAS` | 0.686 | Rare | **Epic** | CHANGED | 950 |
| `WD_AssaultRifle_G36C` | 0.745 | Epic | **Epic** | kept | 1500 |

### Price and ownership impact - **none**

Verified by diffing `weapon_tier_BEFORE.csv` against `weapon_tier_AFTER.csv`:

```text
weaponId changed     0 / 25
price changed        0 / 25
catalogOrder changed 0 / 25
unlockCost changed   0 / 25
tier changed        16 / 25
```

`WeaponData.price` is an independent serialized field and purchase is keyed by `weaponId`, so **no
price a player already paid changed, and no owned-weapon state changed.**

Two systems do read `tier` and are affected in principle:

| Consumer | Effect | Why it does not matter today |
|---|---|---|
| `PlayerProfile.TryUpgradeWeapon` (`:613`) and its `ShopScreen` readout | Indexes the star-upgrade shard/Gold cost tables by tier, so future star-upgrade costs shift | Star upgrades hold **no design authority** (W6), and Gold has no in-run faucet, so the path is unreachable |
| `GachaService` (`:61`) | Uses `tier` as gacha rarity | Gacha holds **no design authority** (W6); the entry point is not surfaced |

Both are exactly the systems W6 removed from the design. Nothing else consumes `tier` beyond the shop
border colour and a label.

## 5. Naming collision

`ZombieWar.EditorTools.WeaponCatalog` (a contact-sheet renderer over four vendor folders) was renamed
to **`VendorWeaponSheetRenderer`**, and the name `WeaponCatalog` now belongs to the authoritative
roster asset.

The old type never was a catalog - it scanned vendor folders and wrote a PNG - while every design
document already used "WeaponCatalog" for the roster. Blast radius was **zero**: the rebuilt call
graph and a full-text search both found exactly one occurrence, its own declaration. Menu item moved
from `Tools/ZombieWar/Weapon Catalog Sheet` to `Tools/ZombieWar/Vendor Weapon Sheet`.

## 6. Consumers - migrated and deferred

| Consumer | State |
|---|---|
| `PlayerProfile` starter seeding | **MIGRATED.** Reads `WeaponCatalog.Active.Starter`, falls back to the old lowest-`catalogOrder` rule if no catalog is present |
| `PlayerProfile` save resolution | **UNCHANGED BY DESIGN.** Already keyed by `weaponId` with `legacyAliases`; it was already correct, and touching it would have added risk for no gain |
| `WeaponRosterMigration` | **EXTENDED** (not duplicated) with `BuildCatalog()` and `RunOnboardingGates()` |
| Audio builders, `DevProfileTools`, `ZombieWarCheatPanel`, `CombatPowerAuditWindow`, `CombatPower`, `GachaService` | **DEFERRED - see below** |
| `SceneFlowBuilder`, `LoadoutMenuInstaller`, Shop/Armory list building | **DEFERRED - out of scope by instruction.** They write serialized arrays into scenes and `UI_*.prefab`s, which this milestone may not touch |

### Why the rest is deferred, stated plainly

The catalog **exists and is authoritative**, but so far only the starter-seeding consumer reads it.
The remaining consumers still folder-scan. That is a real gap against "adding weapon 26 requires one
catalog entry and nothing else": today it would also need an audio key, and the audio builders do not
yet fail loudly on a missing one. **That fail-loud check is the single most valuable remaining item,
and it is not done.** It is first in the M7.1 queue below rather than presented as complete.

## 7. Handed to M7.1

| # | Item | Why it matters |
|---|---|---|
| 1 | **Support grip out of reach on all 14 two-handed weapons** | Largest visual defect in the shipped arsenal. An owner decision, not a data edit - see `G5_GRIP_VALIDATION.md` section 4 |
| 2 | Audio builders must derive from the catalog and **fail the build** on a missing weapon audio key | Today a new weapon fails silently at runtime instead |
| 3 | Migrate `DevProfileTools` (hard-coded "Unlock all 25 weapons"), `ZombieWarCheatPanel`, `CombatPowerAuditWindow`, `CombatPower`, `GachaService` to the catalog | Removes the remaining folder scans |
| 4 | **G1b: move the 25 weapon materials into project ownership** | A vendor pack reimport can currently restyle the whole arsenal |
| 5 | `SceneFlowBuilder` / `LoadoutMenuInstaller` / Shop list building | Needs owner sign-off because it touches scenes and UI prefabs |
| 6 | G7/G8 thresholds are mine and unvalidated | Colour and silhouette bars need an owner call before they gate anything |
