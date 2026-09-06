# M6.1 Step 2 — Full arsenal audit summary

**Status:** MEASURED. Every number here came from a Unity sweep of the actual prefabs
(`../_work/_raw_prefab_scan.tsv`, 415 rows), not from filenames or previous documents.
**Row-level data:** `weapon_inventory.csv` (415 rows, one per prefab).

---

## 1. Reconciliation — all 415 prefabs in exactly one bucket

| bucket | count | what it means |
|---|---:|---|
| CompleteWeaponCandidate | 28 | distinct, toon-shaded weapon body |
| NeedsShaderConversion | 5 | distinct weapon body, but URP Lit not toon |
| LaterSpecialFamily | 5 | launcher / tripod-mounted; no runtime fire mode exists |
| DuplicateModel | 30 | same mesh signature as a body already counted |
| ColourVariant | 1 | recolour of a counted body (`GrenadeLauncher_C_Sand`) |
| AttachmentOrPart | 343 | optic, muzzle, rail, grip, light, laser, magazine, bipod, projectile |
| DemoOrSceneObject | 3 | dogtag, heartbeat sensor, distance-measuring gadget |
| BrokenOrMissingDependency | 0 | — |
| OffStyle | 0 | — |
| Reject | 0 | — |
| **TOTAL** | **415** | **reconciles exactly** |

Source packs: `MW4` 263 · `ShotgunPack1` 77 · `PistolPack1` 34 · `Project` 25 · `WeaponsVol1` 16.

> **FACT that reframes the whole phase:** the repository does **not** contain hundreds of weapons.
> It contains **33 distinct weapon bodies** (28 + 5) and **343 attachment parts**. 83 % of the
> "weapon prefabs" are modular attachments. The scaling lever is **attachment recombination and
> family/tag design**, not more gun models.

## 2. Distinct weapon bodies by family

| family | bodies | of which already integrated | notes |
|---|---:|---:|---|
| Sidearm | 12 | 11 | 10 project + `M1911` (VOL1) + `Pistol_P` (MW4, needs conversion) |
| AssaultRifle | 10 | 6 | + `AR_A_2`, `M4_8` toon-ready; `AR_T`, `AR_U` need conversion |
| Shotgun | 6 | 5 | + `ShotGun_D` toon-ready and unused |
| SMG | 2 | 1 | + `SMG_P` (MW4, needs conversion) — **the SMG family has only one usable body today** |
| Marksman | 2 | 1 | + `Recon_P` (MW4, needs conversion) |
| LMG | 1 | 1 | `WPN_LMG_Generic` only |
| **Total** | **33** | **24** | plus 5 LaterSpecialFamily (3 grenade launchers, RPG7, M2_50cal) |

**Immediately onboardable without any shader work: 4 bodies** — `AR_A_2`, `M4_8`, `ShotGun_D`,
`M1911` (all already `StylizedToonWorldKit/Toon/Toon Lit`).

## 3. Material / shader state — FACT

| shader state | prefabs |
|---|---:|
| `StylizedToonWorldKit/Toon/Toon Lit` (+ Glass) | 152 |
| `Universal Render Pipeline/Lit` | 263 |

**Every MW4 prefab (263) is URP Lit.** Onboarding anything from MW4 requires material conversion
first — this is a hard gate, not a polish step (see the vision review: URP-Lit weapons render as
near-black shapes with no outline in this art style).

## 4. Provenance of the shipped 25 — FACT

All 25 production weapons were traced to a vendor source by mesh signature (triangle count + bounds):

| project weapon group | vendor source |
|---|---|
| 10 sidearms | `PistolPack1` `Pistol_A` … `Pistol_J` (1:1) |
| 5 assault rifles | `ShotgunPack1` `AR_A_1`, `AR_B`, `AR_C`, `AR_D`, `AR_E` |
| 1 assault rifle | `WeaponsVol1` `AK74` → `WPN_AssaultRifle_Generic` |
| 4 shotguns | `ShotgunPack1` `ShotGun_A`, `_B`, `_C`, `_E` |
| 2 shotguns | `WeaponsVol1` `Bennelli_M4` → **two** project weapons |
| 1 marksman | `WeaponsVol1` `M107` |
| 1 LMG | `WeaponsVol1` `M249` |
| 1 SMG | `WeaponsVol1` `Uzi` |

> ### DEFECT — FACT
> **`WPN_Shotgun_BenelliM4` and `WPN_Shotgun_Generic` are the same mesh** (2133 tris, bounds
> 0.082 × 0.302 × 1.465). Two of the shipped 25 weapons are the same model with different numbers.
> The effective distinct-model count of the production arsenal is **24, not 25**.

## 5. Grip / muzzle authoring state — FACT

| | production 25 | vendor bodies |
|---|---|---|
| `WeaponGripPoints` component | **25 / 25** | mostly absent (`ShotGun_D` is a rare exception) |
| `rightHandGrip` / `leftHandGrip` / `muzzlePoint` assigned | verified on samples: all three set | not authored |
| `WeaponData.useAuthoredGripPositions` | **25 / 25 = true** | n/a |

**The grip pipeline exists and is fully applied to the shipped arsenal.** Onboarding a vendor body
therefore needs grip/muzzle authoring, which is the main manual cost in the Factory.

> **Heuristic anchor discovered — INFERENCE:** vendor bodies expose consistently named sub-parts
> (`*_Grip`, `*_Barrel`, `*_BarrelGuard`, `*_Bolt`, `*_Mag`). A `_Grip` child gives a right-hand
> anchor and the far end of `_Barrel` gives a muzzle anchor. This is what makes 80–90 % automatic
> onboarding plausible — but it is a heuristic and must be validated visually, never trusted blindly.

## 6. Geometry budget — MEASURED

| | min | median | p75 | max |
|---|---:|---:|---:|---:|
| triangles (all 415) | 104 | 760 | 1 790 | 22 606 |
| triangles (integrated 25) | 2 133 | ~3 500 | ~6 300 | 11 614 |
| MW4 candidate bodies | 6 084 | ~10 400 | ~11 500 | 11 700 |

MW4 bodies are roughly **2–3× the triangle count** of the project's own weapons. For a WebGL/mobile
target this is a real cost, and it argues for treating MW4 as a later, selective source rather than a
bulk import.

## 7. What this means for the Weapon Factory

1. The arsenal problem is **not** "onboard hundreds of guns". It is "make 33 bodies feel like more
   than 6 families" — which is a **skill/tag and attachment** problem.
2. Only **4 bodies** are drop-in ready. Everything else from MW4 needs conversion first.
3. **SMG and LMG are single-body families.** Any design that promises SMG or LMG variety is currently
   unsupported by assets.
4. Duplicate detection must be part of the Factory: a mesh-signature check would have caught the
   BenelliM4/Generic collision before it shipped.
