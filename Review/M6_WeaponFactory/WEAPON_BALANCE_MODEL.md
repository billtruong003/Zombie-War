# M6.1 Step 4 — Weapon schema and balance model

**Status:** PROPOSAL awaiting owner approval. Every coefficient is `TUNING`. No `WeaponData` asset,
runtime value or production file was changed.
**Machine-readable companion:** `weapon_balance_model.csv` — **24 rows**, one per mechanically distinct
integrated weapon (the shipped 25 minus the `WPN_Shotgun_Generic` duplicate).

> **Why this file exists.** The M6.1 handoff required the balance work at this path. It was originally
> written inside `WEAPON_FAMILIES_AND_FACTORY.md` under "Step 4", and that merge was not disclosed in
> the final report. The content now lives **here**; that document keeps a short pointer and no longer
> duplicates it.

---

## 4.1 Field marking

`EXISTS_AND_USED` · `EXISTS_BUT_DEAD` · `EXISTS_BUT_PARTIAL` · `DERIVED` · `NEEDS_NEW_FIELD` · `EDITOR_ONLY`

### IDENTITY
| Field | Status | Note |
|---|---|---|
| weaponId | EXISTS_AND_USED | save identity |
| displayName (`weaponName`) | EXISTS_AND_USED | |
| family | **NEEDS_NEW_FIELD** | only `weaponClass` exists; family must be explicit |
| class (`weaponClass`) | EXISTS_AND_USED | |
| tier | EXISTS_AND_USED | drives shop colour/price |
| catalogOrder | EXISTS_AND_USED | presentation + starter seeding |
| roleTag | **EXISTS_BUT_DEAD** | empty on all 25 |
| buildHint | **EXISTS_BUT_DEAD** | empty on all 25 |
| synergyTags | **NEEDS_NEW_FIELD** | `buildTag` exists but is `"generalist"` on all 25 |
| variantGroupId | **NEEDS_NEW_FIELD** | required to stop duplicates inflating family count |
| baseWeaponId | **NEEDS_NEW_FIELD** | variant → base link |

### VISUAL
| Field | Status |
|---|---|
| sourcePrefab | NEEDS_NEW_FIELD (provenance is currently untracked) |
| projectPrefab (`weaponPrefab`) | EXISTS_AND_USED |
| icon / thumbnail | EXISTS_AND_USED (generated) |
| materialContract | NEEDS_NEW_FIELD |
| modelBounds / visualScale | DERIVED (measurable from the prefab) |
| handedness (`twoHanded`) | EXISTS_AND_USED |

### IK
| Field | Status |
|---|---|
| rightHandGrip / leftHandGrip / muzzlePoint | EXISTS_AND_USED (on `WeaponGripPoints`, 25/25) |
| gripLocalPosition / Euler / Scale | EXISTS_AND_USED |
| rightHandGripRootPosition / leftHandGripRootPosition | EXISTS_AND_USED |
| useAuthoredGripPositions | EXISTS_AND_USED (true on 25/25) |
| poseTemplateId | NEEDS_NEW_FIELD (the key to family-level pose reuse) |
| poseValidationStatus | NEEDS_NEW_FIELD (drives the manual-fix queue) |

### FIRE
| Field | Status | Note |
|---|---|---|
| fireMode | EXISTS_BUT_PARTIAL | 6 enum values, **3 implemented** (SingleHitscan, MultiPelletHitscan, PiercingLine) |
| damage / fireRate / range / automatic | EXISTS_AND_USED | |
| pelletCount / spreadAngle | EXISTS_AND_USED | shotgun |
| pierceCount / pierceDamageFalloff | EXISTS_AND_USED | marksman only |
| knockback | EXISTS_AND_USED | `ApplyPhysicalPush` |
| damageFalloffCurve / `RangeFalloff` | EXISTS_AND_USED | |
| projectileSpeed / explosionRadius | EXISTS_BUT_DEAD | no Projectile path |
| chainCount / chainRange | **EXISTS_BUT_DEAD** | 0 on all 25; no chain code |
| beamWidth / beamRamp / beamTickRate | **EXISTS_BUT_DEAD** | no beam path |
| **resourceModel / ammoType / reloadSfxKey** | **EXISTS_BUT_DEAD — RETIRED** | M4 removed magazines. `Magazine` on all 25 is a lie in the data. |
| heatPerShot / heatCapacity / coolRate / overheatLockTime | EXISTS_BUT_DEAD | |
| chargeTime / chargeHold | EXISTS_BUT_DEAD | |

> **Explicit retirement:** reload, magazine, ammo and overheat are removed from active design. The
> fields stay in the asset until a migration removes them; the design must never reference them.

### HANDLING / PRESENTATION / ECONOMY / DERIVED
| Group | Status |
|---|---|
| recoilKick / recoilSideKick / recoilAimKick | EXISTS_AND_USED |
| moveSpeedModifier / aimTurnRateModifier | **NEEDS_NEW_FIELD** (required for the LMG identity) |
| targetPreference / min-maxEffectiveRange | **NEEDS_NEW_FIELD** |
| fireSfxKey / muzzleFlashPrefab / impactPrefab / tracerPrefab / smokeTrailPrefab | EXISTS_AND_USED |
| beamPrefab / projectilePrefab | EXISTS_BUT_DEAD |
| outlineContract | NEEDS_NEW_FIELD |
| price / unlockCost / tier | EXISTS_AND_USED |
| unlockMethod / cosmeticVariantGroup | NEEDS_NEW_FIELD |
| rawDps, effectiveDps, crowdDps, singleTargetDps, controlScore, mobilityScore, rangeScore, powerBudgetUsed | **DERIVED** — computed by the balance model, never authored |

## 4.2 Power model (TUNING — all coefficients are hypotheses)

```text
rawDps            = damage × fireRate × pelletCount
crowdDps          = rawDps × pierceFactor × aoeFactor × spreadUtility
singleTargetDps   = rawDps × accuracyFactor × (1 − overkillWaste)

controlScore      = knockback×0.4 + slowPotential×0.4 + staggerPotential×0.2
mobilityScore     = moveSpeedModifier + uptimeWhileMoving
rangeScore        = clamp(range / 16, 0, 1)

powerBudgetUsed   = 0.45×norm(singleTargetDps) + 0.25×norm(crowdDps)
                  + 0.12×controlScore + 0.10×mobilityScore + 0.08×rangeScore
```

**Every weapon in a family must land inside that family's budget band.** Rarity buys *side-grades*
inside the band, never a higher band:

| Tier | Meaning |
|---|---|
| Common | centre of the family band |
| Uncommon → Legendary | same total budget, redistributed (more burst / less range, etc.) plus cosmetic distinction |

Caps: fire rate soft-caps at 2.2× base and hard-caps at 2.5×; move speed soft-caps at 1.6× and hard-caps
at 1.8×; concurrent autonomous powers cap at 3.

**Known balance defect (FACT):** `WPN_AssaultRifle_G36C` at 240 DPS is the highest single-target DPS in
the game while sitting in an all-round family — a strictly-dominant weapon. Under this model it must
come down into the AR band or trade range/handling away.

**Immediately playable rule:** a newly unlocked weapon enters at the centre of its family band, at its
full designed power. There is no ramp-up requirement and no per-weapon upgrade track — which is why
weapon star upgrades hold **no design authority** (W6). Weapons differ by **tier band**, not by upgrade
level; the measured bands are in `../M6_DecisionLock/weapon_tier_ladder.csv`.

---

## Boss versus crowd DPS

The model separates the two explicitly so that a weapon cannot be balanced against the wrong problem:

```text
singleTargetDps  = rawDps x accuracyFactor x (1 - overkillWaste)     -> the boss/elite number
crowdDps         = rawDps x pierceFactor x aoeFactor x spreadUtility -> the wave number
```

- Shotguns take an `accuracyFactor` of 0.85 and an `overkillWaste` of 0.10, because pellets spill
  damage past a dying target — this is why their single-target number sits below their raw number.
- Marksman takes `accuracyFactor` 1.0 and no overkill waste, which is what makes it the boss weapon
  despite the lowest raw DPS in the game.
- A weapon that is strong on **both** axes is a balance failure, not a good weapon. `WD_AssaultRifle_G36C`
  is currently the closest thing to that.

## Immediately playable unlocks

A newly unlocked weapon enters at the **centre of its family band**, never below it, at 100 % of its
designed power. There is no ramp-up requirement and no upgrade prerequisite. This is the direct reason
weapon star upgrades hold **no design authority** under the W6 lock: vertical weapon power would make
every new unlock feel worse than the gun the player already levelled.

**Tier bands (W3 lock).** Weapons *do* differ in power, in four measured bands derived from
`powerBudgetUsed` — Common ≤ 0.340 (13 weapons) · Uncommon ≤ 0.490 (5) · Rare ≤ 0.650 (4) · Epic > 0.650
(2). Every band edge sits inside a measured gap. That is not a contradiction of the rule above: tier is
a power band fixed at authoring time, never an upgrade track. **16 of the 24 authored `WeaponData.tier`
values disagree with the measured band** — see `../M6_DecisionLock/weapon_tier_ladder.csv` and
`WEAPON_ONBOARDING_GATE.md` §4.

## What the CSV contains

`weapon_balance_model.csv` — 24 rows, columns:

```text
weaponId · family · tier · damage · fireRate · range · pellets · pierce
rawDps · crowdDps · singleTargetDps · rangeScore · controlScore · mobilityScore · powerBudgetUsed
```

Measured family bands (`powerBudgetUsed`, min / median / max):

```text
Sidearm       0.205 / 0.265 / 0.328   (n=10)
SMG           0.355                   (n=1)
AssaultRifle  0.465 / 0.583 / 0.745   (n=6)
Shotgun       0.254 / 0.300 / 0.595   (n=5)
Marksman      0.388                   (n=1)
LMG           0.488                   (n=1)
```

Top five by budget: `G36C` 0.745 (Epic) · `FAMAS` 0.686 (Rare) · `SCARL` 0.622 (Rare) ·
`AA12` 0.595 (Epic) · `M4A1` 0.543 (Uncommon).

> **The Assault Rifle band averages 0.599 against the Sidearm band's 0.261 — roughly 2.2x.** Under this
> model that gap is the headline balance defect, and `G36C` sitting at the very top of the all-round
> family is the specific instance of it.
