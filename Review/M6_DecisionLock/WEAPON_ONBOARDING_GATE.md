# Delta A — Weapon Visual Onboarding Gate + tier ladder

**Authority:** designed under owner decision **W3 — CHANGE** (2026-08-15). Supersedes the
MW4-specific hard gate proposed in the M6.1 packet.
**Scope:** documentation and design only. No runtime code, no asset changes, no tooling built.
**Companion data:** `weapon_tier_ladder.csv` (24 rows) · `../M6_WeaponFactory/weapon_balance_model.csv`

---

## 0. What the owner changed and why it matters

> **W3 — CHANGE (owner, verbatim intent):** replace the MW4-specific hard gate with a **universal
> Weapon Visual Onboarding Gate**, because future weapons are continuously added from sources that do
> not exist yet. Introduce weapon quality/tier into progression, and permit visually rarer or better
> weapons to occupy **higher controlled power tiers**.

The M6.1 proposal was written against one vendor pack. That is the defect: `MW4` is not a category,
it is one supplier. A gate that names a supplier expires the moment a new pack arrives. This document
replaces it with a gate that names **properties of a weapon**, so any weapon from any source —
vendor pack, commission, or authored in-house — passes through the same checks.

---

## 1. The gate — source-agnostic

Every weapon body entering the production arsenal must clear all **G1–G8** checks. The gate is a
property test on the asset, never a test on where it came from.

| # | Check | What passes | Verification | Blocking? |
|---|---|---|---|---|
| **G1** | **Material contract** | Every renderer uses the project toon contract (`StylizedToonWorldKit/Toon/Toon Lit`, or the project Glass material where transparency is intended). No `Universal Render Pipeline/Lit`, no vendor material referenced directly. | **AUTOMATIC** — shader/material sweep over the prefab, identical in kind to `CharacterMaterialContractTests` | **BLOCKING** |
| **G2** | **Outline present** | The weapon renders with the project toon outline at gameplay scale. | **AUTOMATIC** — asserts the toon material's outline parameters are non-zero | **BLOCKING** |
| **G3** | **Duplicate-model rejection** | The mesh signature (triangle count + bounds, the check that caught `BenelliM4` == `Generic`) does not match any body already in the arsenal. | **AUTOMATIC** — signature lookup against the arsenal manifest | **BLOCKING** |
| **G4** | **Grip and muzzle authoring** | `WeaponGripPoints` is present with `rightHandGrip`, `leftHandGrip` and `muzzlePoint` all assigned; `WeaponData.useAuthoredGripPositions = true`. | **AUTOMATIC** — component/field presence sweep (this is exactly the 25/25 state the shipped arsenal already holds) | **BLOCKING** |
| **G5** | **Grip looks correct in the hand** | Right hand meets the grip, support hand meets the fore-end, no hand penetrates the model, the muzzle points down the aim axis. | **MANUAL** — inspection of a **fixed rig-relative** capture. This capture tool does not exist yet; building it is M7.0 work. See §4. | **BLOCKING** |
| **G6** | **Triangle budget** | Body is within the arsenal budget band. The shipped 25 run 2 133 – 11 614 tris; anything materially above the top of that band needs an explicit exception with a WebGL frame-cost note. | **AUTOMATIC** — mesh statistics, threshold from the current arsenal distribution | **WARNING** (blocking only above an authored hard ceiling) |
| **G7** | **Silhouette separation** | At gameplay scale the weapon is not confusable with a body already shipped in the same family. | **HEURISTIC** — 3D mesh-bounds proportions plus a rendered same-family contact sheet, then a human call. **2D pixel mass is a rejected proxy** — it ranked wide shapes over tall ones and must not be used. | **WARNING** — a failure means "give it colour identity", not "reject" |
| **G8** | **Colour identity** | The weapon carries at least one distinguishing colour region, because the vision review found the arsenal is overwhelmingly dark grey and only about six bodies are readable by shape alone. | **HEURISTIC** — dominant-colour sample against the family's existing bodies | **WARNING** |

**Blocking set: G1, G2, G3, G4, G5.** Four are automatic; one (G5) is manual and currently
un-runnable, which is stated plainly rather than papered over.
**Warning set: G6, G7, G8** — recorded on the weapon's onboarding record, never silently dropped.

### 1.1 Why each blocking check is blocking

- **G1/G2** — measured, not asserted: all 263 MW4 prefabs are `Universal Render Pipeline/Lit`, and at
  magnification they render as near-black shapes with no outline beside toon neighbours. A weapon that
  fails G1 is not "slightly off style", it is unreadable.
- **G3** — the arsenal already shipped this defect. `WPN_Shotgun_BenelliM4` and `WPN_Shotgun_Generic`
  are the same mesh (2 133 tris, bounds 0.082 × 0.302 × 1.465), so two shop entries are the same gun.
  The check that found it costs nothing to run on every future import.
- **G4/G5** — a weapon whose grip is wrong is visible in every frame of every run.

### 1.2 What this does to MW4

Nothing special, which is the point. MW4 bodies now fail **G1 and G2** like any other unconverted
asset, and pass once converted. `Recon_P` (marksman) and `SMG_P` (SMG) remain the two most valuable
unused bodies in the repository — both would lift a family that has exactly one usable weapon today —
and both are now gated on a property they can be made to satisfy, not on their pack name.

---

## 2. The tier ladder — MEASURED bands, not invented ones

Tier is derived from the **measured** `powerBudgetUsed` of the 24 mechanically distinct production
weapons, not chosen by feel.

```text
powerBudgetUsed = 0.45*norm(singleTargetDps) + 0.25*norm(crowdDps)
                + 0.12*controlScore + 0.10*mobilityScore + 0.08*rangeScore
measured spread: 0.205 (WD_Sidearm_PistolA) .. 0.745 (WD_AssaultRifle_G36C), median 0.314
```

Every band edge is placed **inside a measured gap**, so no weapon sits on a boundary by a rounding
accident:

| Band | Range | Edge sits in the gap | Gap width | Weapons |
|---|---|---|---|---:|
| **Common** | ≤ 0.340 | 0.328 → 0.355 | 0.027 | **13** |
| **Uncommon** | 0.340 – 0.490 | 0.488 → 0.535 | 0.047 | **5** |
| **Rare** | 0.490 – 0.650 | 0.622 → 0.686 | **0.064** (largest gap in the arsenal) | **4** |
| **Epic** | > 0.650 | — | — | **2** |

### 2.1 Which weapon sits in which band — MEASURED

| Band | Weapons |
|---|---|
| **Common** (13) | `WD_Sidearm_PistolA` 0.205 · `WD_Sidearm_Makarov` 0.223 · `WD_Sidearm_Glock19` 0.238 · `WD_Sidearm_P226` 0.252 · `WD_Shotgun_DoubleBarrel` 0.254 · `WD_Shotgun_BenelliM4` 0.257 · `WD_Sidearm_Python357` 0.259 · `WD_Sidearm_BerettaM9` 0.271 · `WD_Sidearm_M1911` 0.273 · `WD_Sidearm_USP45` 0.273 · `WD_Sidearm_DesertEagle` 0.290 · `WD_Shotgun_Mossberg500` 0.300 · `WD_Sidearm_FiveSeven` 0.328 |
| **Uncommon** (5) | `WD_SMG_Generic` 0.355 · `WD_Marksman_SniperGeneric` 0.388 · `WD_Shotgun_SPAS12` 0.426 · `WD_AssaultRifle_Generic` 0.465 · `WD_LMG_Generic` 0.488 |
| **Rare** (4) | `WD_AssaultRifle_AK47` 0.535 · `WD_AssaultRifle_M4A1` 0.543 · `WD_Shotgun_AA12` 0.595 · `WD_AssaultRifle_SCARL` 0.622 |
| **Epic** (2) | `WD_AssaultRifle_FAMAS` 0.686 · `WD_AssaultRifle_G36C` 0.745 |

### 2.2 What the distribution reveals — MEASURED, and it constrains the design

**13 of 24 weapons fall inside a single 0.12-wide band.** The arsenal is bottom-heavy: more than half
of it is sidearms and pump shotguns clustered at the low end, and the top of the ladder is almost
entirely assault rifles (5 of the 6 weapons in Rare + Epic).

Two consequences follow directly, and neither is optional:

1. **Tier cannot carry family variety.** If tier were the progression spine, the player's path would
   read "pistols → assault rifles" and four families would be scenery. Family choice must stay
   horizontal *within* a band.
2. **The Common band needs internal identity work, not a power raise.** Thirteen weapons separated by
   less than 0.12 of power budget are separated by almost nothing the player can feel. Their identity
   has to come from signature cards and fire behaviour, which is exactly what the 23-card catalog is
   for.

### 2.3 Anti-grind rules

| Rule | Statement |
|---|---|
| **A1 — No weapon is required** | The endless run must be completable-to-a-personal-best on a **Common** weapon. Tier is a preference lane, never a difficulty key. If a Rare weapon becomes necessary to progress, the ladder has failed and the bands must be compressed. |
| **A2 — Ceiling ratio** | Epic ≤ **2×** the power budget of the weakest Common (0.745 / 0.205 = 3.6× today, so the *current* spread already exceeds this and must be compressed by raising Common floors, never by inflating Epic). |
| **A3 — No per-weapon upgrade track** | A weapon is unlocked at **100 % of its designed power**. There is no level-1 version of any gun. This is what preserves "immediately playable" — see §3. |
| **A4 — Acquisition is deterministic** | Higher tiers cost more of the unlock resource, and that cost is **visible and countable** from the first run. No random weapon drops, no duplicates, no pity. See Delta B. |
| **A5 — Time is not the gate** | Unlock resource comes from run *events* (boss chests, completed stations), never from elapsed time, so idling cannot buy a tier. |

### 2.4 Anti-dominance rules

| Rule | Statement |
|---|---|
| **D1 — Band ceiling is hard** | No weapon may be authored above its band ceiling. A weapon that measures above its band is **re-tiered, not re-labelled**. |
| **D2 — Cards outrank tier** | A fully built Common weapon must be able to reach the *effective* power of a bare Rare weapon. Tier is a starting position; the 1-of-3 card offers are the actual power curve. If tier out-scales cards, build choice stops mattering. |
| **D3 — Every band keeps ≥ 2 families** | A band that contains only assault rifles is a band that deletes five families at that point in the ladder. Rare and Epic currently hold 2 and 1 family respectively — this is a **known violation** and is why `Recon_P` / `SMG_P` onboarding matters. |
| **D4 — Tier is not rarity theatre** | Tier drives shop colour, price and unlock cost. It must never drive drop luck, because there are no random weapon drops. |
| **D5 — Re-measure on every change** | Any `WeaponData` stat edit re-runs the power-budget calculation. A weapon that crosses a band edge changes tier, or the edit is reverted. |

---

## 3. Reconciliation — tiers vs "a new weapon must be immediately playable"

The old rule read: *"Weapon unlocks are horizontal — a new weapon is a new playstyle, not a bigger
number. A newly unlocked weapon must be immediately playable at base level."*

That rule was written to kill **star upgrades** — the mechanic that makes a freshly unlocked gun
*worse than the one you already levelled*. That is a statement about **upgrade tracks**, not about
weapons having different power. The corrected rule separates the two:

> **Unlocks are horizontal within a tier band and controlled between bands. A newly unlocked weapon is
> immediately playable at its full designed power — there is no per-weapon upgrade track, and no
> weapon has a weaker early version of itself.**

Under this reading nothing is contradicted:

- **Still true:** you never grind a weapon to make it good. It arrives finished. (A3)
- **Still true:** a new weapon is primarily a new playstyle — inside a band, choice is horizontal.
- **Now also true:** weapons occupy measured power bands, and a visually rarer weapon may sit higher,
  bounded by D1/D2 and by the anti-grind rules.
- **Still dead:** star upgrades and shard-fed vertical weapon power. Those remain without design
  authority (Delta B).

---

## 4. `WeaponData.tier` — schema and migration impact

**Current state — FACT.** The `tier` field exists on `WeaponData` and is used, but only to drive
**shop colour and price**. It carries no power meaning today.

**Under this delta, `tier` becomes load-bearing:** it drives shop colour, price, **unlock cost**, and
it asserts a power band. That makes its current values a data-correctness problem.

### 4.1 The measured defect

**16 of 24 authored `tier` values disagree with the measured power band.** Only 8 agree. Full row
data in `weapon_tier_ladder.csv`; the direction of error runs both ways:

| Example | Authored | Measured band | Reads to the player as |
|---|---|---|---|
| `WD_Sidearm_Python357` (0.259) | Rare | Common | a Rare-priced sidearm with Common power |
| `WD_Sidearm_DesertEagle` (0.290) | Rare | Common | the same |
| `WD_LMG_Generic` (0.488) | Common | Uncommon | a Common-priced weapon that outperforms Rares |
| `WD_AssaultRifle_Generic` (0.465) | Common | Uncommon | the same |
| `WD_AssaultRifle_FAMAS` (0.686) | Rare | Epic | underpriced top-end |
| `WD_Shotgun_AA12` (0.595) | Epic | Rare | overpriced |

The two the player will notice fastest are the **underpriced strong weapons** (`LMG_Generic`,
`AssaultRifle_Generic`): they make the correct purchase obvious and every other purchase wrong.

### 4.2 Migration — no schema change required

| Step | Work | Kind |
|---|---|---|
| **1** | Re-tier the 16 mismatched `WeaponData` assets to their measured band | data edit, 16 assets |
| **2** | Re-price each re-tiered weapon so price follows the new tier | data edit |
| **3** | Add an edit-time assertion: `tier` must equal `band(powerBudgetUsed)` | new editor validation |
| **4** | Add `unlockCost` (Blueprint) keyed by tier — see Delta B | **new field** |
| **5** | Re-declare one of `BenelliM4` / `Generic` as a variant of the other (G3 caught it) | data edit, 1 asset |

**No field is removed, no save data changes, no runtime code path changes** for steps 1–3 and 5. Only
step 4 adds a field. The shop already reads `tier` and `price`; it reads new values, not new fields.

**Not authorised here.** Every step above is M7 work. This document specifies it; it does not execute
any part of it.

---

## 5. What is queued and not done

| Item | State | Where it lands |
|---|---|---|
| **G5 rig-relative grip capture tool** | **NOT BUILT.** Grip validation across the shipped 25 is `NOT RUN` — the existing holding sheet shows no hand in any frame. | M7.0 |
| Automatic gate checks G1–G4, G6 | **NOT BUILT.** Specified above; each is a sweep of a kind this project has already written once (`CharacterMaterialContractTests`). | M7.0 |
| Re-tiering the 16 mismatched assets | **NOT DONE** — data edit, deliberately not performed in a documentation pass | M7.1 |
| `Recon_P` / `SMG_P` conversion | **NOT DONE** | M7.1 |

> **Claim discipline:** nothing in this document asserts that any weapon currently looks correct in
> the hand. G5 has never been run on any weapon, including the shipped 25.
