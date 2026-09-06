# Zombie War — M6 Endless Run System Design

**Authority:** Consolidated M6 design workspace. Supersedes
`Review/M6_Conceive/M6_ENDLESS_RUN_SKILLS_AND_INTERACTIONS.md`.
**Status: LOCKED — M6 CONCEPT LOCK.** The seven owner decisions `W1`–`W7` are answered and recorded
in section 1. Statements marked **OWNER-LOCKED** are production contract. Everything still marked
`PROPOSAL`, `HYPOTHESIS` or `TUNING` is design work inside a locked frame, not an open question about
the frame.
**Updated:** 2026-08-15
**Companions:** [`GAME_DESIGN.md`](GAME_DESIGN.md) · [`MVP_SHIP_PLAN.md`](MVP_SHIP_PLAN.md) ·
[`WORLD_STREAMING_TECHNICAL_DESIGN.md`](WORLD_STREAMING_TECHNICAL_DESIGN.md)
**Evidence:** `Review/M6_Conceive/Evidence/` (outfit/skill/enemy sheets) · **`Review/M6_WeaponFactory/`** (M6.1 arsenal audit: 415 prefabs, 6 weapon sheets, balance model) · **`Review/M6_DecisionLock/`** (M6.2 decision lock: onboarding gate, tier ladder, economy v2, shared primitives)

> **Skill note (stated once):** `game-designer` and `simplifier-vi` were applied. `ship-director` is not
> installed here, so its MUST/SHOULD/LATER/CUT scope discipline was applied manually.

## 0. Provenance legend

Every design statement in this document carries one of these classifications. Section headers and
status columns carry the label; individual sentences are not tagged.

| Label | Meaning |
|---|---|
| **FACT** | Directly verified in production source or assets |
| **OWNER-LOCKED** | Explicitly decided by the project owner |
| **MEASURED** | Produced by a recorded experiment in this project |
| **INFERENCE** | Interpretation of facts or evidence |
| **HYPOTHESIS** | Plausible direction that requires a prototype or playtest |
| **PROPOSAL** | Candidate awaiting owner approval |
| **TUNING** | Provisional number only; requires simulation or playtest |
| **REJECTED** | Known-incorrect direction, retained so it is not re-proposed |

> **The seven decisions `W1`–`W7` are answered.** Section 1 records them as taken, with the owner's
> own wording for the three `CHANGE` answers. `OWNER-LOCKED` now means *decided*, not *recommended*.
> Labels below `OWNER-LOCKED` still mean what they say: a `TUNING` number is still an invented starting
> point, and a `HYPOTHESIS` is still unproven, whether or not it sits inside a locked frame.

---

## 1. OWNER DECISIONS — ANSWERED AND LOCKED

**Seven decisions, `W1`–`W7`, answered by the owner on 2026-08-15.** Four `APPROVE`, three `CHANGE`.
These are **decisions taken**, not proposals awaiting approval. Nothing in this section may be
re-opened, re-argued or re-presented as a question.

| | Decision | Verdict |
|---|---|---|
| **W1** | Arsenal model: six families, variants inside families | **APPROVE — OWNER-LOCKED** |
| **W2** | Candidate card pool: the 23-card catalog | **APPROVE — OWNER-LOCKED** |
| **W3** | Weapon onboarding gate | **CHANGE — OWNER-LOCKED** → universal gate + tier ladder (**Delta A**) |
| **W4** | First three interactives: Signal Relay, Supply Cache, Boss Beacon | **APPROVE — OWNER-LOCKED** |
| **W5** | Relic Fragment: collection-only | **APPROVE — OWNER-LOCKED** (2D/billboard art) |
| **W6** | Legacy economy | **CHANGE — OWNER-LOCKED** → replacement economy with Blueprint (**Delta B**) |
| **W7** | Outfit H2 | **CHANGE — OWNER-LOCKED** → **approved for production** under a 0 % hard-conflict gate (**Delta C**) |

> **REJECTED — do not re-propose.** An earlier version of this section asked the owner to approve
> "three prototype weapons first" (`WD_Sidearm_FiveSeven` / `WD_SMG_Generic` / `WD_Shotgun_AA12`) and a
> 15-card shortlist. The M6.1 arsenal audit retired both: the arsenal is a **Weapon Factory** problem,
> not a three-gun problem, and the card pool is the owner's **23-card** list. That framing is historical
> and is **not** a live question.

---

### W1 — Arsenal model: six families, variants inside families — **APPROVED**

**Decision — OWNER-LOCKED.** Six active families. A weapon that cannot state a different combat
question is a **variant inside a family**, never a new family. One of the two identical shotguns
(`WPN_Shotgun_BenelliM4` / `WPN_Shotgun_Generic`, same mesh: 2 133 tris, bounds
0.082 × 0.302 × 1.465) is re-declared a variant of the other.

**What this makes true:** weapon 26 is a **data entry**, not a feature. The Factory is the mechanism;
the family is the design unit.

**No fixed weapon count is assumed anywhere in this design.** The arsenal is 24 mechanically distinct
weapons **today**, from 33 usable bodies out of 415 scanned prefabs. That is a measurement of the
current repository, not a target, a cap or a scope statement. Every system specified in this document
— the onboarding gate, the tier ladder, the Blueprint cost table, the card family tags — is written
against *families and properties*, so that adding the 25th, 40th or 100th weapon changes data and
changes nothing structural. Any sentence elsewhere that reads as "three weapons first", "the 25
weapons" as a design boundary, or "only after N weapons work" is **superseded by this lock**.

**Still not evidenced:** how different the six families actually *feel* in play. Family identity is
designed, not proven; it needs the M7.2 playtest. Approval of the model is not approval of the feel.

### W2 — Candidate card pool: the 23-card catalog — **APPROVED**

**Decision — OWNER-LOCKED.** The 23 named cards are the candidate pool: 5 stat · 12 signature
(2 per family × 6) · 4 autonomous · 2 universal. The rule **"at most one pure-stat card per 1-of-3
offer"** is locked with them — it is the cheapest available fix for the Damage → Fire Rate dominant
strategy.

**All 23 are in scope.** There is no MUST-only subset, no 15-card slice and no deferred remainder in
the design. The MUST 15 / SHOULD 7 / LATER 1 rating is a **build-order hint for M7.2**, not a scope
cut: a `SHOULD` card is a card whose runtime work is scheduled later, and a `LATER` card
(`Shockwave Belt`) is still a designed card with a full schema row. Nothing in the catalog is dropped.

**The cost is real and is stated as a shared-primitive problem, not 20 separate features.** 20 of the
23 cards need new runtime work; those 20 rest on **9 shared primitives**:

| # | Primitive | Cards served |
|---|---|---:|
| **P1** | Per-enemy status / record carrier (slow, Exposed, marked, per-target hit count) | 4 |
| **P2** | Autonomous power framework (trigger hook + cooldown state + HUD readout) | 4 |
| **P3** | Multi-target spatial query (chain, densest cluster, cone, priority override) | 5 |
| **P4** | Player distance accumulator | 2 |
| **P5** | Ramp / charge accumulator with decay | 5 |
| **P6** | Damage-path interception hook | 3 |
| **P7** | Distance-scaled damage curve (extends `WeaponData.RangeFalloff`) | 2 |
| **P8** | Stat soft-cap curve | 2 |
| **P9** | Power VFX / HUD kit (arc, wave, charge meter, cooldown readout) | 4 |

Row data: **`Review/M6_DecisionLock/skill_shared_primitives.csv`**. The 3 cards needing nothing new
are `Damage Up`, `Max Health Up`, `Coin Gain Up`.

**Verified, and it does not soften:** `FireMode.ChainLightning` is an enum value with no code behind
it (`chainCount = 0` on all 25 assets; `Weapon.cs` implements only `PiercingLine` among the special
modes), and **no densest-cluster query exists anywhere in runtime**. P3 is genuinely new work and is
the single largest item in the catalog.

**Still not evidenced:** whether any of the 20 unimplemented cards is fun. Approving the pool approves
the *list*, not the outcome of any card.

### W3 — Weapon onboarding gate — **CHANGED**

> **Owner decision, as given:** replace the MW4-specific hard gate with a **universal Weapon Visual
> Onboarding Gate**. Future weapons are continuously added. Introduce **weapon quality/tier** into
> progression and permit visually rarer/better weapons to occupy **higher controlled power tiers**.

**Decision — OWNER-LOCKED.** The MW4-specific gate is retired. `MW4` is a supplier, not a category; a
gate that names a supplier expires the moment a new pack arrives. It is replaced by a gate that tests
**properties of a weapon**, and by a tier ladder derived from measured power.

This is **Delta A**, specified in section 1c and in
**`Review/M6_DecisionLock/WEAPON_ONBOARDING_GATE.md`** (+ `weapon_tier_ladder.csv`).

### W4 — First three interactives: Signal Relay, Supply Cache, Boss Beacon — **APPROVED**

**Decision — OWNER-LOCKED.** Build order: **Signal Relay** first (it directly tests whether the world
is worth traversing), then **Supply Cache** (cheapest, and it gives Coin an in-run use), then **Boss
Beacon** using existing bosses and elites so no new enemy art is needed.

**The station visual language is prop-independent — this is part of the lock.** The prop sheet proved
containers and barrels are credible *bodies* for Supply Cache and the explosive barrel. It proved
nothing about communicating station *purpose*, and Signal Relay and Boss Beacon have **no proven
visual body** at all. Station identity therefore does **not** depend on finding the right prop.
Every station is defined by a **World Signal Language** — an authored, prop-independent layer:

```text
ground ring · vertical beam · floating icon · emissive accent
progress indicator · distinct colour contract per station type · distinct audio motif
```

Any prop may sit inside that language, or none: a station must be readable as
"hold this zone to charge" from the signal layer alone, at gameplay distance, before any prop is
chosen. This makes station identity independent of what art the repository happens to contain, and it
means a future prop swap is a cosmetic change rather than a redesign. **The signal layer is authored
art that does not exist yet** — that is the real cost of W4, and it is art work, not programming.

**Still not evidenced:** that players will leave a safe position to reach a signal. That is the M7.3
stop condition, and it is unchanged by this lock.

### W5 — Relic Fragment: collection-only — **APPROVED**

**Decision — OWNER-LOCKED.** `Relic Fragment` is added as a **collection-only** item: it unlocks
cosmetics, weapon skins, power visual variants and lore badges; it is **secured the moment it is
picked up**; it **never grants raw stats**. Pity counts **eligible sources** (Boss Chest, Mythic
Cache), never time, so idling cannot farm it. Duplicates convert to Gem, so no drop is dead loot.

**Art revision — OWNER-LOCKED.** Relic Fragment uses **2D / billboard art**, not a modelled 3D pickup:
a billboard sprite with an emissive tint and an audio sting. **Cost is revised from MEDIUM to
LOW–MEDIUM**, and the remaining cost is dominated by the Archive screen and its save data rather than
by art.

**Still not evidenced:** whether the collection hook motivates players here. No drop rates are approved
— every number in §12.2 remains `TUNING`.

### W6 — Legacy economy — **CHANGED**

> **Owner decision, as given:** do not merely freeze the legacy economy. Design a **replacement**
> economy around **Coin + Gem + weapon unlock resource / Blueprint + Relic collection**. Legacy
> **Gold / Weapon Shard / star upgrades / Gacha** remain in code but **lose design authority**.

**Decision — OWNER-LOCKED.** "Recommended dormant, owner approval required" is retired everywhere. The
four legacy systems are **present in code, without design authority** — a settled status, not a
pending one. In their place, four resources with disjoint jobs: **Coin** (spend now), **Gem** (rare
cosmetics), **Blueprint** (weapon unlocks — new), **Relic Fragment** (collection).

This is **Delta B**, specified in section 1d and in **`Review/M6_DecisionLock/ECONOMY_V2.md`**.

### W7 — Outfit H2 — **CHANGED: APPROVED FOR PRODUCTION**

> **Owner decision, as given:** **approve H2 for production.** **0 % hard conflict is the mandatory
> gate**; **~70 %+ random thematic coherence is acceptable**. Manual Wardrobe remains the path for
> deliberately curated outfits.

**Decision — OWNER-LOCKED.** H2 is **approved for production**. The previous 85 % pass gate and the
`≥80 %` M7.8 gate are **superseded by owner decision** — they were provisional bars I proposed, and
the owner has replaced them with a different pair of criteria. H2 is no longer "the best candidate,
do not adopt".

This is **Delta C**, specified in section 1e.

---

**Tuning numbers were not part of this packet and are not approved by it.** They are listed in
section 25 and are approved later, after simulation or playtest. A locked decision above never
promotes a `TUNING` number to a fact.

---

## 1b. Decision ledger — claim provenance

Every load-bearing claim in this document, classified. Confidence is my confidence in the
classification, not in the design being correct.

| Claim | Classification | Evidence / source | Confidence | Owner approval required |
|---|---|---|---|---|
| 25 `WeaponData` assets exist, all with prefabs | **FACT** | `AssetDatabase` sweep; `Evidence/Weapons_All.png` | High | No |
| All 25 share `buildTag="generalist"`, empty `roleTag`/`buildHint` | **FACT** | asset field dump | High | No |
| `resourceModel = Magazine` on all 25 is dead data | **FACT** | `Weapon.cs:417` comment; M4 removed magazines | High | No |
| Most sidearms/ARs are hard to distinguish at gameplay scale | **MEASURED** | rendered sheet, inspected | High | No |
| 16 `ZombieData` assets; 6 archetypes; 3 elites; 11 unused in production | **FACT** | asset dump; `WD_Level1` composition | High | No |
| Existing bosses/elites can carry Boss Beacon without new art | **INFERENCE** | `BossBeacon_Candidates.png` | Medium | No |
| 7 `RunPerk` entries exist; all 5 `RunPerkKind` consumed at runtime | **FACT** | `RunPerkPool.cs`, `Weapon.cs:120/650`, `PlayerMovement.cs:78`, `RunState.cs:129`, `RunOverlays.cs:375` | High | No |
| Damage→FireRate is a dominant strategy | **INFERENCE** | perk math over 8 picks | High | No |
| 453 costume entries across 18 player slots + 2 technical | **FACT** | catalog dump; 453 renders | High | No |
| `PickupManager` auto-collects on `WaveClearedEvent` | **FACT** | `PickupManager.cs:141` | High | No |
| `LoadoutState` models 3 weapon slots | **FACT** | `LoadoutState.cs:24–45` | High | No |
| ~~Three prototype weapons first~~ | **REJECTED** | superseded by the M6.1 arsenal audit; historical only | High | no — retired, not a live question |
| Weapon Factory: 6 families, variants inside families, 33 bodies → 24 distinct | **OWNER-LOCKED** | 415-prefab scan; mesh-signature duplicate detection | High | Answered — W1 APPROVE |
| 23-card catalog is the pool; all 23 in scope | **OWNER-LOCKED** | full schema in SKILL_CATALOG.md; 20/23 need new primitives | Medium | Answered — W2 APPROVE |
| The 20 unimplemented cards rest on **9 shared primitives** | **INFERENCE** | grouping of the catalog's `newPrimitive` column; `skill_shared_primitives.csv` | Medium–High | No |
| Each individual card is fun | **HYPOTHESIS** | none — nothing implemented | Low–Medium | No — pool locked, outcomes unproven |
| "Max one stat card per offer" | **OWNER-LOCKED** | dominant-strategy analysis | High | Answered — W2 APPROVE |
| `RangeFalloff` / `knockback` / fire-rate multiplier are reusable | **FACT** | `WeaponData.cs:221`, `Weapon.cs:652/666/120` | High | No |
| Chain lightning is a reusable primitive | **REJECTED** | `FireMode.ChainLightning` is an enum value only; `Weapon.cs` implements just `PiercingLine`; `chainCount=0` on all assets | High | No |
| Densest-cluster targeting exists | **REJECTED** | no cluster/density query anywhere in runtime | High | No |
| `Bomb.Explode()` gives a reusable explosion | **FACT** (asset + partial mechanic) | `Bomb.cs:70–76` OverlapSphere + TakeDamage | High | No |
| Signal Relay / Supply Cache / Boss Beacon as first interactives, in that order | **OWNER-LOCKED** | none implemented | Medium | Answered — W4 APPROVE |
| Container/barrel props are credible Cache/Barrel bodies | **MEASURED** | `MapInteractive_PropCandidates.png` | Medium–High | No |
| Relay / Beacon / Scanner / Medical visual bodies | **NOT YET PROVEN** | no assembled visual exists | Low | No — station identity is prop-independent (W4 lock) |
| World Signal Language carries station identity, not props | **OWNER-LOCKED** | W4 lock; prop sheet proves bodies, not purpose | High | Answered — W4 APPROVE |
| Relic Fragment collection system, 2D/billboard art | **OWNER-LOCKED** | nothing exists | Low | Answered — W5 APPROVE |
| Relic 2 % base / +2 % pity / 25–30 hard pity / 15 Gem dupe | **TUNING** | invented starting points | Low | Later, not now |
| Coin as primary common currency | **OWNER-LOCKED** | owner direction | High | No |
| Gem as rare cosmetic currency | **OWNER-LOCKED** | owner direction: Coin common, Gem rare and secured | High | no |
| Gold / Shard / star upgrades: in code, **no design authority** | **OWNER-LOCKED** | W6 CHANGE; no faucet exists | High | Answered — W6 CHANGE |
| Gacha: in code, **no design authority** | **OWNER-LOCKED** | W6 CHANGE; unused by the loop | High | Answered — W6 CHANGE |
| **Blueprint** — fungible, deterministic weapon unlock resource | **OWNER-LOCKED** (design), **TUNING** (all costs) | W6 CHANGE; `ECONOMY_V2.md` | High / Low | Answered — W6 CHANGE |
| XP curve `25 + (L-1)^1.35 × 12` | **TUNING** | invented; current curve is `10+(L-1)*8` (FACT) | Low | Later, not now |
| Death banks 25 % Coin | **OWNER-LOCKED** | existing `RunClosure.DefeatCoinFraction = 0.25f` (FACT) + owner | High | No |
| Manual abandon banks 0 % | **OWNER-LOCKED** (corrected) | owner correction of a rejected 100 % proposal | High | No |
| `Secure Station / Banking Shrine` | **PROPOSAL — future** | not designed | Low | Later |
| H2 outfit grammar: 73 % pass, 0 % hard conflicts | **MEASURED** | 300 seeded outfits, `Metrics/outfit_runs.json` | High | No |
| ~~H2 blocked by an 85 % pass gate~~ | **SUPERSEDED BY OWNER DECISION** | the 85 % bar was mine; W7 replaces it with 0 % hard conflicts mandatory + ~70 %+ coherence acceptable | High | Answered — W7 CHANGE |
| H2 approved for production | **OWNER-LOCKED** | W7 CHANGE | High | Answered — W7 CHANGE |
| Authored metadata will lift coherence above 73 % | **HYPOTHESIS** | untested; the re-test has not been run | Low | No — must be measured, not assumed |
| ~~MW4-specific hard onboarding gate~~ | **SUPERSEDED BY OWNER DECISION** | W3 CHANGE — a gate that names a supplier expires when a new pack arrives | High | Answered — W3 CHANGE |
| All 263 MW4 prefabs are URP Lit, no toon outline | **MEASURED** | prefab sweep; sheets inspected at magnification | High | No — still true, now expressed as gate checks G1/G2 |
| Universal Weapon Visual Onboarding Gate (G1–G8) | **OWNER-LOCKED** (that a universal gate exists) + **PROPOSAL** (the specific 8 checks) | W3 CHANGE; `WEAPON_ONBOARDING_GATE.md` | High / Medium | Answered — W3 CHANGE |
| Tier ladder derived from measured `powerBudgetUsed` | **MEASURED** (bands) + **OWNER-LOCKED** (that tier carries power) | `weapon_tier_ladder.csv`; edges placed inside measured gaps | High | Answered — W3 CHANGE |
| **16 of 24 authored `WeaponData.tier` values disagree with the measured band** | **MEASURED** | `weapon_tier_ladder.csv`, 24 rows | High | No — data defect, fix scheduled M7.1 |
| Visual grip validation of any weapon | **NOT RUN** | the held sheet shows no hand in any frame; gate check G5 has never been executed | High | No — M7.0 tooling |
| 2D pixel mass is a valid silhouette proxy | **REJECTED** | ranked wide bobs over tall mohawks | High | No |
| `bodyCompatibility` field is required | **INFERENCE** | beards on feminine/child faces | High | No |
| Station frequencies, prices, heal %, cooldowns, threat growth | **TUNING** | invented | Low | Later, not now |

## 1c. Delta A — Weapon Visual Onboarding Gate + tier ladder (W3)

**Full specification: `Review/M6_DecisionLock/WEAPON_ONBOARDING_GATE.md` · data:
`Review/M6_DecisionLock/weapon_tier_ladder.csv` (24 rows).** Summarised here so the canonical document
is self-contained.

### 1c.1 The gate — a property test, never a source test

Every weapon body entering the arsenal clears **G1–G8**. Each check names a property of the asset, so
a weapon from any pack, commission or in-house author passes through the same eight checks.

| # | Check | Verification | Blocking |
|---|---|---|---|
| **G1** | Project toon material contract on every renderer; no `Universal Render Pipeline/Lit`, no vendor material referenced | **AUTOMATIC** | **BLOCKING** |
| **G2** | Toon outline present at gameplay scale | **AUTOMATIC** | **BLOCKING** |
| **G3** | Mesh signature (tris + bounds) does not duplicate a body already in the arsenal | **AUTOMATIC** | **BLOCKING** |
| **G4** | `WeaponGripPoints` present with right/left/muzzle assigned; `useAuthoredGripPositions = true` | **AUTOMATIC** | **BLOCKING** |
| **G5** | Grip **looks** correct: hands meet the weapon, no penetration, muzzle down the aim axis | **MANUAL** (fixed rig-relative capture) | **BLOCKING** |
| **G6** | Triangle budget within the arsenal band (shipped 25 run 2 133 – 11 614) | **AUTOMATIC** | WARNING |
| **G7** | Silhouette separable from same-family bodies. **2D pixel mass is a rejected proxy** — use 3D mesh bounds + a rendered sheet + a human call | **HEURISTIC** | WARNING |
| **G8** | Carries at least one distinguishing colour region | **HEURISTIC** | WARNING |

**MW4 is no longer named by the gate.** Its 263 prefabs fail G1/G2 like any unconverted asset and pass
once converted. `Recon_P` (marksman) and `SMG_P` (SMG) remain the two most valuable unused bodies —
each would lift a family that has exactly one usable weapon today.

> **G5 has never been run on any weapon, including the shipped 25.** The existing held sheet shows no
> hand in any frame. The rig-relative capture tool is M7.0 work. This is stated as a gap, not softened.

### 1c.2 The tier ladder — MEASURED bands

Bands are derived from the measured `powerBudgetUsed` of the 24 distinct weapons (spread 0.205 – 0.745,
median 0.314). **Every band edge sits inside a measured gap**, so no weapon lands on a boundary by
rounding:

| Band | Range | Edge falls in gap | Gap | Weapons |
|---|---|---|---:|---:|
| **Common** | ≤ 0.340 | 0.328 → 0.355 | 0.027 | 13 |
| **Uncommon** | 0.340 – 0.490 | 0.488 → 0.535 | 0.047 | 5 |
| **Rare** | 0.490 – 0.650 | 0.622 → 0.686 | **0.064** (largest in the arsenal) | 4 |
| **Epic** | > 0.650 | — | — | 2 |

```text
Common   PistolA .205 · Makarov .223 · Glock19 .238 · P226 .252 · DoubleBarrel .254
         BenelliM4 .257 · Python357 .259 · BerettaM9 .271 · M1911 .273 · USP45 .273
         DesertEagle .290 · Mossberg500 .300 · FiveSeven .328
Uncommon SMG_Generic .355 · SniperGeneric .388 · SPAS12 .426 · AR_Generic .465 · LMG_Generic .488
Rare     AK47 .535 · M4A1 .543 · AA12 .595 · SCARL .622
Epic     FAMAS .686 · G36C .745
```

**What the distribution forces — MEASURED.** 13 of 24 weapons sit inside a single 0.12-wide band, and
5 of the 6 weapons in Rare + Epic are assault rifles. Two consequences follow and neither is optional:
**tier cannot carry family variety** (or the player's path reads "pistols → assault rifles" and four
families become scenery), and **the Common band needs identity work, not a power raise** — 13 weapons
separated by less than 0.12 of power budget are separated by almost nothing the player can feel. Their
identity comes from signature cards and fire behaviour, which is what the 23-card catalog is for.

### 1c.3 Anti-grind and anti-dominance rules — OWNER-LOCKED frame, specific rules PROPOSAL

| | Rule |
|---|---|
| **A1** | The run must be playable to a personal best on a **Common** weapon. Tier is a preference lane, never a difficulty key. |
| **A2** | Epic ≤ 2× the weakest Common. **The current spread is 3.6× (0.745 / 0.205) and already violates this** — it is closed by raising Common floors, never by inflating Epic. |
| **A3** | A weapon unlocks at **100 % of its designed power**. No per-weapon upgrade track, no level-1 version of any gun. |
| **A4** | Acquisition is deterministic and countable from run one. No random weapon drops, no duplicates, no pity. |
| **A5** | Unlock resource comes from run *events*, never elapsed time — idling cannot buy a tier. |
| **D1** | Band ceilings are hard. A weapon measuring above its band is **re-tiered, not re-labelled**. |
| **D2** | **Cards outrank tier.** A fully built Common must reach the effective power of a bare Rare, or build choice stops mattering. |
| **D3** | Every band keeps ≥ 2 families. **Rare holds 2 and Epic holds 1 today — a known violation**, and the reason `Recon_P` / `SMG_P` onboarding matters. |
| **D4** | Tier drives colour, price and unlock cost. It never drives drop luck, because there are no random weapon drops. |
| **D5** | Any `WeaponData` stat edit re-runs the power budget; crossing a band edge changes tier or the edit is reverted. |

### 1c.4 Reconciliation — tiers vs "immediately playable"

The old rule — *"a newly unlocked weapon must be immediately playable at base level"* — was written to
kill **star upgrades**, the mechanic that makes a freshly unlocked gun worse than the one you already
levelled. That is a statement about **upgrade tracks**, not about weapons having different power. The
corrected rule separates them:

> **Unlocks are horizontal within a tier band and controlled between bands. A newly unlocked weapon is
> immediately playable at its full designed power — there is no per-weapon upgrade track, and no weapon
> has a weaker early version of itself.**

Still true: you never grind a weapon to make it good; it arrives finished (A3). Still true: inside a
band, choice is horizontal. Now also true: a visually rarer weapon may sit in a higher band, bounded by
D1/D2. Still dead: star upgrades and shard-fed vertical weapon power.

### 1c.5 `WeaponData.tier` — schema and migration

**FACT:** `tier` exists and is used today, but only for **shop colour and price**. Under this delta it
also carries a power band and an unlock cost, which makes its current values a data-correctness problem.

**MEASURED DEFECT: 16 of 24 authored `tier` values disagree with the measured band.** Only 8 agree.
The errors run both ways — `Python357` (0.259) and `DesertEagle` (0.290) are authored **Rare** with
Common power, while `LMG_Generic` (0.488) and `AssaultRifle_Generic` (0.465) are authored **Common**
and outperform authored Rares. The underpriced-strong pair is what a player notices first: it makes one
purchase obviously correct and every other purchase wrong.

| Step | Work | Kind |
|---|---|---|
| 1 | Re-tier the 16 mismatched assets to their measured band | data edit |
| 2 | Re-price each re-tiered weapon | data edit |
| 3 | Edit-time assertion `tier == band(powerBudgetUsed)` | new editor validation |
| 4 | Add `unlockCost` (Blueprint), keyed to tier | **new field** |
| 5 | Re-declare one of `BenelliM4` / `Generic` as a variant of the other | data edit |

**No save-data change and no runtime path change** for steps 1–3 and 5; only step 4 adds a field. The
shop already reads `tier` and `price` — it reads new values, not new fields. **All of this is M7 work
and none of it is performed here.**

---

## 1d. Delta B — Replacement economy: Coin · Gem · Blueprint · Relic (W6)

**Full specification: `Review/M6_DecisionLock/ECONOMY_V2.md`.**

### 1d.1 Four resources, four disjoint jobs

| Resource | The question it answers | Faucet | Sink | Random? | On death |
|---|---|---|---|---|---|
| **Coin** | *"What do I spend right now?"* | kills, crates, containers, Greed Terminal | in-run: Supply Cache, Medical Station · Hub: common costume | no | **25 % banked · 0 % on abandon** |
| **Gem** | *"What am I saving for that is beautiful?"* | elites, Boss Chest, golden stations | Hub: rare cosmetics, sets, Archive | no | **secured at pickup — exempt** |
| **Blueprint** *(new)* | *"How do I get the next weapon?"* | **Boss Chest (guaranteed) + completed stations** | **weapon unlocks only** | **no — deterministic** | **secured at pickup — exempt** |
| **Relic Fragment** | *"What do I still not have?"* | Boss Chest, Mythic Cache | Archive sets only, never stats | rare, pity on eligible sources | **secured at pickup — exempt** |

**Only Coin is at risk.** That keeps death meaningful without making any long-term goal reversible.

### 1d.2 Blueprint — the five questions, answered

**Is it a non-luck path? — Yes, and that is its whole justification.** Blueprint is **fungible, not
per-weapon**. That one choice removes every luck mechanic at once: no duplicates exist, so no
conversion rule is needed; nothing is rolled, so no pity counter is needed; the distance to any weapon
is a subtraction the player can do in their head. **Rejected alternative:** per-weapon blueprints
("collect 30 AK-47 blueprints") reintroduce exactly what the owner is removing — you earn progress
toward guns you do not want, which needs conversion, which needs pity, which is gacha renamed.

**Does it scale with tier? — Yes, and only by tier.** Cost is a function of the measured band from
Delta A, so pricing cannot drift from power: Common lowest (13 weapons live there and must be cheap or
the band is dead content) → Uncommon → Rare → Epic highest (only `FAMAS` and `G36C`). **Every numeric
cost is `TUNING` and is not approved here.** What is fixed is the *shape*: monotonic in band, with
Common reachable inside the first session.

**How does it coexist with Coin? — Disjoint sinks.** **Blueprint opens weapons; Coin buys everything
else.** A weapon costs Blueprint **only**. If it cost both, the player would face "save Coin for the
gun or spend it at the Supply Cache" — which quietly punishes the in-run spending the Supply Cache
exists to create. With disjoint sinks, Coin is always safe to spend. **Consequence:**
`WeaponData.price` stops being a Coin price for weapons (see 1c.5 step 4).

**Conversion or pity? — Neither, by construction.** There is nothing to convert and nothing to pity,
because there are no duplicates and no roll.

**First hour — HYPOTHESIS, with a stated failure condition.** Run 1 ends with a visible non-zero
Blueprint number, earned rather than explained. The first **Common** weapon lands in runs 2–3; by runs
4–8 the player has felt two families; the Rare band is a countable target by ~run 10. **Fails if** a
player finishes their first session still holding only the starting weapon — that means Common costs
are too high.

**Recommendation: adopt, with one caution.** Blueprint must never become a second Coin. The moment
anything other than a weapon costs Blueprint, the two compete for the same decision and this collapses
back into the Coin/Gold problem it replaces.

### 1d.3 The decision it creates

At any Boss Beacon: risk a 25 % Coin payout you have already earned, to gain **Blueprint** (progress
toward a weapon you can name and count) and a **Relic** roll (progress toward a set you can see is
incomplete). **Blueprint is guaranteed; only the Relic roll is random** — so the risk is never
"I got nothing".

### 1d.4 UI surface — specification only

Hub wallet: Coin, Gem. Weapon shop: Blueprint balance and `n / N` against every locked weapon, visible
before it is reachable. Run-end settlement: Coin banked with its 25 % / 0 % reason stated, plus Gem,
Blueprint and Relic gained. Archive: Relic sets. In-run HUD: Coin only — the other three are secured
and need no live counter.

> **No UI prefab or scene is edited by this pass, and none may be without an explicit request.**

### 1d.5 Legacy systems — in code, without design authority — OWNER-LOCKED

| System | Code state | Design authority | Replaced by |
|---|---|---|---|
| **Gold** | `EconomyConfig` entries and wallet remain | **NONE** | Coin (common) + Blueprint (unlocks) |
| **Weapon Shard** | entries remain | **NONE** | Blueprint — fungible, deterministic, no duplicates |
| **Weapon star upgrades** | costs remain, unused | **NONE** | the tier ladder — power is authored, never grinded |
| **Gacha** | pool config remains, entry point not surfaced | **NONE** | direct Blueprint unlock — the explicit non-luck path |

> **"In code" is not design authority**; equally, none of these is deleted — dormant is reversible,
> deletion is not. **Language rule for every document:** describe these four as *"present in code,
> without design authority"*, never as "recommended dormant", "pending approval" or "awaiting an owner
> decision". That decision has been taken.

---

## 1e. Delta C — Outfit H2 approved for production (W7)

### 1e.1 The decision

**OWNER-LOCKED.** H2 is **approved for production**, under two criteria set by the owner:

| Criterion | Bar | Status |
|---|---|---|
| **Hard conflicts** | **0 % — mandatory** | **MEASURED: 0 % over 300 seeded outfits** |
| **Random thematic coherence** | **~70 %+ — acceptable** | **MEASURED: 73 % over the same 300 outfits** |
| Deliberately curated outfits | **Manual Wardrobe remains the path** | unchanged, already shipping |

### 1e.2 Superseded gates

| Retired gate | Where it appeared | Why it is gone |
|---|---|---|
| **85 % pass rate** before adoption | old §1 W7, old §29 design state | **SUPERSEDED BY OWNER DECISION.** The 85 % bar was mine, not a requirement. The owner replaced it with 0 % hard conflicts mandatory + ~70 %+ coherence acceptable. |
| **`≥80 %` PASS** on 100 seeded outfits | old §24 M7.8, old §32 M7.4 | Same. The M7.8 pass condition is restated in 1e.4. |

> These gates were **not lowered to fit a result** — they were replaced by the owner with different
> criteria, and the distinction matters: the mandatory bar (hard conflicts) got *stricter* in kind, from
> a blended pass rate to an absolute zero.

### 1e.3 Claim discipline — the sentence that must not be written

**Never write "0 % failure", "no failures" or "H2 always produces a valid outfit".** The measured claim
is narrower and must be stated in full:

> Over **300 seeded outfits**, H2 produced **0 hard conflicts** (structural defects: clipping,
> occlusion, incompatible body pairings) and **73 % thematic coherence**. The residual **27 % are
> thematic mismatches, not structural defects** — they look like odd taste, not like a bug.

Two limits stay attached to that claim: it is a **300-seed sample**, not a proof over the whole
combination space; and the outfit metadata H2 depends on is currently **inferred**, not authored.

### 1e.4 What still has to happen — visible, not hidden by approval

Approval is not completion. Both items below are required work, and neither is done:

| Item | State |
|---|---|
| **Costume metadata authoring** — real per-item `theme`, `palette`, `silhouette`, `bodyCompatibility` over **453 items**. Colour bakes automatically; theme, set membership and body compatibility need a designer. | **NOT DONE.** The largest single item of M7.4/M7.8. |
| **Re-test after authoring** — re-run the 300-seed measurement on authored metadata. | **NOT DONE.** |
| `bodyCompatibility` field added to the costume schema | **NOT DONE** (fixes a visible error: beards on feminine/child faces) |
| **Silhouette data baked from 3D mesh bounds** | **NOT DONE.** 2D pixel mass is a **rejected** proxy — it ranked wide bobs above tall mohawks and missed the hair that actually clips helmets. |
| Hair-replacing hats **satisfy and suppress** the Hair slot rather than conflicting with it | **DESIGNED, NOT IMPLEMENTED.** This grammar rule is what removed the residual 5 % hard conflicts. |

**M7.8 pass condition (restated):** *costume metadata authored; H2 measured over ≥300 seeded outfits at
**0 hard conflicts** with **≥70 % thematic coherence**; Manual Wardrobe unaffected.*

**Failure condition, stated in advance:** if authored metadata *reduces* coherence below 70 %, or
reintroduces any hard conflict, H2 does not ship on that metadata — the fallback is H1 hard rules only,
which removes structural conflicts and leaves theme clashes.

---

## 2. Simplifier summary

```text
Ở Hub: chọn MỘT khẩu súng + mặc đồ
   ↓
Vào một thế giới endless: chạy, auto-fire, né
   ↓
Lên level: chọn 1 trong 3 card → súng hoặc power tự động mạnh dần
   ↓
Thấy tín hiệu trên map: đi tới trạm / hòm / boss, chấp nhận rủi ro
   ↓
Chết hoặc tự kết thúc → giữ tài nguyên theo luật rõ ràng
   ↓
Về Hub: mở súng mới, skin mới, đồ sưu tầm
   ↓
Chơi lại với súng khác → run khác hẳn
```

Five load-bearing sentences. A feature that serves none of them is not MVP:

- **Súng** quyết định cách bắn.
- **Skill** quyết định run này "điên" theo kiểu nào.
- **Trạm/pickup** cho lý do rời chỗ an toàn.
- **Coin/Gem/Blueprint/Relic** cho lý do chơi run kế tiếp. Blueprint là thứ duy nhất mở súng mới,
  và nó không dựa vào may rủi.
- **Outfit** cho người chơi thể hiện bản thân, không cộng damage.

## 3. Product direction — OWNER-LOCKED

| Locked | Meaning |
|---|---|
| One endless procedural world | `Map_Level1` only; Maps 2–5 stay retired |
| One weapon per run | Chosen in the Hub, fixed for the run |
| Auto-fire, no reload | Weapon rhythm comes from cadence and cards, not magazines |
| No manual grenade | Explosive content returns as `Ordnance Core` / `Emergency Detonation` |
| Autonomous powers allowed | They are the spectacle layer |
| Level-up = 1-of-3 | Max 30 s unscaled pause, then auto-pick the highlighted/best-fit card |
| Terrain is not gameplay | The world needs signals, stations, bosses and risk/reward |
| Small team, WebGL/mobile | Scope is a hard constraint, not a preference |
| Dedicated character metal | CUT at M5+ — not reopened |

## 4. Player fantasy — PROPOSAL

> **"Tôi cầm một khẩu súng, chạy giữa bầy quái, và mỗi vài chục giây tôi lại tự biến mình thành một
> thứ mạnh hơn — cho tới khi thế giới thắng tôi."**

The player should feel mobile, escalating, and responsible for the risks they took. Not: trapped in an
arena, reading buff icons, or watching a build play itself.

## 5. Full global game loop — PROPOSAL

```text
┌──────────────────────── HUB ────────────────────────┐
│ PLAY · ARMORY · WARDROBE · ARCHIVE · SHOP           │
│ choose 1 weapon · choose outfit · spend Coin/Gem     │
└───────────────┬─────────────────────────────────────┘
                │
        ┌───────▼────────┐
        │ ENDLESS RUN    │  pressure rises with time + activations
        │ move·auto-fire │
        └───┬────────┬───┘
            │        │
     level-up│        │signals on the map
   1-of-3 card│        │Relay · Cache · Beacon · Station · Scanner
            │        │
            └───┬────┘
                │ death OR manual end
        ┌───────▼────────┐
        │ SETTLEMENT     │ Coin banked by rule · Gem/Relic already secure
        └───────┬────────┘
                │
                └──────────► back to HUB (unlock, collect, restyle)
```

## 6. Hub loop — PROPOSAL

The Hub answers exactly four questions:

1. What does my character look like? → **WARDROBE**
2. Which gun am I taking? → **ARMORY**
3. What did I just bring home? → settlement screen → wallet
4. What am I trying to unlock next? → **ARCHIVE** / **SHOP**

Battle Pass, daily missions and the campaign selector may remain in code but are **not M6 pillars** and
must not occupy the primary navigation.

## 7. Pre-run flow — OWNER-LOCKED

```text
PLAY → weapon select (owned weapons, 1 pick) → outfit already equipped → enter world
```

No consumable loadout, no pre-picked skills, no grenade slot. A newly unlocked weapon is
**immediately playable at its full designed power** — there is no per-weapon upgrade track and no
weapon has a weaker early version of itself. A weapon that is useless until upgraded is a broken
unlock. Weapons differ by **tier band** (a controlled power band fixed at authoring time, §1c.2), not
by upgrade level: unlocks are horizontal *within* a band and controlled *between* bands.

## 8. The one-weapon contract — OWNER-LOCKED

- The player enters with exactly **one** `WeaponData`.
- No in-run weapon switching, no weapon pickups.
- The weapon determines which **signature cards** can appear in the level-up pool.
- `LoadoutState` currently models 3 weapon slots (0 = one-handed, 1–2 = two-handed). **M6 uses slot 0
  semantics only**: one selected weapon. The 3-slot code is **DORMANT**, not deleted, and its removal
  is future implementation work.

---

## 9. Weapon family table — INFERENCE

| Family | Promise (one sentence) | Assets | MVP |
|---|---|---:|---|
| **Pistol / Sidearm** | Movement becomes damage — keep running, keep shooting. | 10 | **prototype** |
| **SMG / rapid** | Sustained fire builds charge that discharges as chain lightning. | 1 | **prototype** |
| **Shotgun / close** | Get close, push the horde off you, punish what's left. | 6 | **prototype** |
| Assault Rifle | Steady line pressure, armour break. | 6 | LATER |
| Marksman | Long line, pierce, elite priority. | 1 | LATER |
| LMG | Hold a lane, bank pressure, shockwave. | 1 | LATER |

## 10. Weapon Factory — the arsenal model (M6.1) — MODEL OWNER-LOCKED (W1), pipeline PROPOSAL

> **The earlier "three prototype weapons first" framing is retired.** It was a scope answer to a
> question the asset audit has now answered properly. Full detail:
> `Review/M6_WeaponFactory/WEAPON_FAMILIES_AND_FACTORY.md`.

### 10.1 What the arsenal actually is — MEASURED

415 weapon-pack prefabs were scanned in Unity and reconciled into exactly one bucket each:

```text
CompleteWeaponCandidate    28      AttachmentOrPart          343
NeedsShaderConversion       5      DuplicateModel             30
LaterSpecialFamily          5      ColourVariant               1
DemoOrSceneObject           3      Broken/OffStyle/Reject      0
                                   ------------------------------
                                   TOTAL                     415
```

**The repository does not contain hundreds of weapons. It contains 33 distinct weapon bodies and 343
attachment parts.** 83 % of the "weapon prefabs" are modular attachments. The scaling lever is family
and tag design plus attachment recombination — not more gun models.

| Family | distinct bodies | integrated | asset health |
|---|---:|---:|---|
| Sidearm | 12 | 11 | deepest family |
| AssaultRifle | 10 | 6 | healthy |
| Shotgun | 6 | 5 | healthy |
| Marksman | 2 | 1 | **weak** |
| SMG | 2 | 1 | **weak — single usable body** |
| LMG | 1 | 1 | **single-body family** |

Other measured facts:

- **All 263 MW4 prefabs are `Universal Render Pipeline/Lit`, not toon.** They render as near-black
  shapes with no outline in this art style (see `VISION_REVIEW.md`). Material conversion is a **hard
  onboarding gate**, not polish.
- Only **4 bodies** are drop-in ready today (`AR_A_2`, `M4_8`, `ShotGun_D`, `M1911`).
- **`WPN_Shotgun_BenelliM4` and `WPN_Shotgun_Generic` are the same mesh.** The shipped arsenal has
  **24 distinct models, not 25**.
- **25/25** production prefabs carry `WeaponGripPoints` with right-hand, left-hand and muzzle
  transforms assigned, and **25/25** `WeaponData` set `useAuthoredGripPositions = true`. The grip
  pipeline exists and works.
- Vendor bodies expose consistent sub-part names (`*_Grip`, `*_Barrel`, `*_Bolt`), which is the
  heuristic that makes mostly-automatic onboarding plausible — and which must always be validated.

### 10.2 Six active families

Each family answers a different question. A model that cannot state a different question becomes a
**variant**, never a new family.

| Family | The question it answers | Signature cards |
|---|---|---|
| Sidearm | "Can I turn movement into damage?" | Run & Gun · Quickstep Round |
| SMG | "Can I hold fire long enough to discharge?" | Static Build-up · Bullet Hose |
| AssaultRifle | "Can I hold this target through the ramp?" | Focus Fire · Breach Round |
| Shotgun | "Do I spend this blast on damage or on space?" | Point Blank · Concussion |
| Marksman | "Is this distance worth the swarm closing?" | Longshot · Hunter's Mark |
| LMG | "Is this position worth being slow in?" | Heavy Pressure · Shockwave Belt |

**Later families:** Launcher and Mounted (assets exist, `FireMode.Projectile` does not).
**Do not plan** Tesla / Laser / Flamethrower / Railgun — **no complete bodies exist** and their fire
modes are unimplemented enum values.

### 10.3 The Factory pipeline

```text
vendor prefab → classify → copy project-owned → material conversion
→ grip/muzzle discovery → family pose template → WeaponData generation
→ stable ID / variant group → icon + contact sheet → catalog → validation
```

Roughly **85 %** of the steps are automatic or heuristic. Classification, ID assignment, data
generation, icon rendering and catalog registration are automatic; **material conversion and
grip/muzzle discovery are heuristic and must be visually validated**. Perfect grip inference is not
promised. Fail-loud conditions and the manual-fix queue are specified in the Factory document.

### 10.4 Balance model

`Review/M6_WeaponFactory/weapon_balance_model.csv` scores all 24 distinct integrated weapons.

```text
rawDps          = damage x fireRate x pelletCount
powerBudgetUsed = 0.45*norm(singleTargetDps) + 0.25*norm(crowdDps)
                + 0.12*controlScore + 0.10*mobilityScore + 0.08*rangeScore
```

Measured family bands (min / median / max):

```text
Sidearm 0.205 / 0.265 / 0.328      AssaultRifle 0.465 / 0.583 / 0.745
Shotgun 0.254 / 0.300 / 0.595      SMG 0.355      Marksman 0.388      LMG 0.488
```

> **Balance defect — FACT.** The Assault Rifle band sits roughly **2.2x** the Sidearm band, and
> `WD_AssaultRifle_G36C` (0.745) is the single strongest weapon in the game while belonging to the
> all-round family. Rarity must buy **side-grades inside a band**, never a higher band.

Retired from active design: reload, magazine, ammo, overheat. `resourceModel = Magazine` on all 25
assets is dead data.

## 11. Skill architecture — POOL OWNER-LOCKED (W2), individual cards HYPOTHESIS

Three readable layers. A card belongs to exactly one.

```text
LAYER 1 — Weapon signature      changes how THIS gun behaves       (2 per prototype weapon)
LAYER 2 — Autonomous power      acts on its own, creates spectacle (3 shared)
LAYER 3 — Common stat           smooths the curve, never the hook  (5 existing RunPerks)
```

Synergy comes from **tags**, never from hand-authored pairs:
`Rapid · Precision · Close · Crit · Shock · Blast · Move · Kill`

### 10.1 Readability gate (every card must pass all 7)

1. One short sentence explains it.
2. Rank 1 = one trigger + one effect.
3. Visible or audible within 3 seconds.
4. No new manual input.
5. No private minigame.
6. Reuses an existing runtime primitive where possible.
7. Upgrades make the same fantasy stronger, not different.

## 12. Card catalog (23) — POOL OWNER-LOCKED (W2) / each card still HYPOTHESIS

> **Superseded count.** The earlier 15-card shortlist is replaced by the owner's 23-card list, fully
> specified in **`Review/M6_WeaponFactory/SKILL_CATALOG.md`** (+ `skill_catalog.csv`).
>
> ```text
> 23 = Stat 5 · Signature 12 (2 per family x 6) · Autonomous 4 · Universal 2
> Status: MUST 15 · SHOULD 7 · LATER 1
> 20 of 23 require at least one new runtime primitive.
> ```
>
> Verified against source, correcting an earlier over-claim: **chain lightning does not exist**
> (`FireMode.ChainLightning` is an enum value only; `Weapon.cs` implements only `PiercingLine` among
> the special modes; `chainCount = 0` on all 25 assets), and **no densest-cluster query exists**.
> Genuinely reusable today: `RangeFalloff`, `knockback` → `ApplyPhysicalPush`, `PerkedFireRate`, the
> damage-multiply path, `PiercingLine`, and `Bomb.Explode()`.
>
> Build simulations: **12, two per active family** — `BUILD_SIMULATIONS.md`.
>
> **W2 lock (§1).** All 23 are in scope; MUST/SHOULD/LATER is a **build-order hint for M7.2**, not a
> scope cut. The 20 cards needing new work rest on **9 shared primitives**, not 20 separate features —
> P1 status carrier (4 cards) · P2 autonomous power framework (4) · P3 multi-target spatial query (5) ·
> P4 distance accumulator (2) · P5 ramp/charge accumulator (5) · P6 damage-path hook (3) ·
> P7 distance-scaled curve (2) · P8 stat soft-cap (2) · P9 power VFX/HUD kit (4).
> Row data: `Review/M6_DecisionLock/skill_shared_primitives.csv`.

### HISTORICAL — the superseded 15-card table

> **Not a live proposal.** Retained only because the five stat-card rows below are `FACT` and the
> "max one stat card per offer" rule is still live under `W2`. The 23-card catalog is the current pool.


**Status: CANDIDATE SHORTLIST — not a production pool.** Statuses below are per-layer.

### 12.1 Common stat cards — FACT (they exist) + PROPOSAL (how to use them)

| ID | Title | Effect | Numeric? | Verdict |
|---|---|---|---|---|
| `perk.damage.s` / `.m` | Damage +15% / +30% | ×1.15 / ×1.30 all weapons | yes | **KEEP** as filler; collapse to one entry with rank-ups |
| `perk.firerate.s` / `.m` | Fire Rate +12% / +25% | ×1.12 / ×1.25 | yes | **KEEP** as filler; collapse to one entry |
| `perk.speed.s` | Move Speed +10% | ×1.10 | yes | **KEEP** — genuinely supports Runner |
| `perk.health.s` | Max Health +20% | +20% max & current, at pick | yes | **KEEP** — the only defensive card |
| `perk.coin.s` | Coin Drops +25% | ×1.25 coin | yes | **KEEP** — becomes meaningful once Coin has in-run sinks |

- **FACT:** all seven entries exist and every `RunPerkKind` is consumed at runtime.
- **PROPOSAL:** collapse the duplicate strength variants (`.s` / `.m`) into five filler roles with ranks.
- **PROPOSAL:** at most **one** stat card may appear in any 1-of-3 offer. This is the single cheapest
  fix for the current Damage→FireRate dominant strategy, and could be prototyped on its own.

### 12.2 Weapon signature cards (6) — DESIGN HYPOTHESES

| Card | Weapon | Trigger | Effect | Feedback | Primitive | Cx | Status |
|---|---|---|---|---|---|---|---|
| **Run & Gun** | Pistol | player velocity > threshold | fire rate +X% while moving | muzzle cadence audibly speeds up | existing `PerkedFireRate` + velocity read | S | **MUST** |
| **Quickstep Shot** | Pistol | player travels N metres | the **next automatically fired shot** is empowered; counter resets when that shot is consumed | bright tracer + hit-stop | new: distance accumulator + one-shot empower flag | S | **HYPOTHESIS** |
| **Static Build-up** | SMG | N consecutive hits | discharge chain lightning to K targets | arcing bolt VFX + rising whine | **NEW — chain target selection does not exist** (see 12.6) | M | **HYPOTHESIS** |
| **Bullet Hose** | SMG | continuous fire | fire rate + spread ramp to a cap; resets on stop | barrel spin-up audio | ramp timer on `Weapon` | S | **MUST** |
| **Point Blank** | Shotgun | hit within R metres | damage ×X, falling off with distance | heavier impact + screen kick | existing `RangeFalloff` curve | S | **MUST** |
| **Concussion** | Shotgun | any blast hit | push + brief slow on hit enemies | bodies visibly shoved | existing `knockback` field | S–M | **MUST** |

### 12.3 Autonomous powers (3) — DESIGN HYPOTHESES

| Card | Trigger | Effect | Feedback | Primitive | Cx | Status |
|---|---|---|---|---|---|---|
| **Chain Lightning** | every T seconds | bolt jumps between K nearby enemies | arc VFX + thunder | **NEW — chain target selection** | M | **HYPOTHESIS** |
| **Ordnance Core** | every T seconds | auto-lobs a bomb at the densest cluster | Bomb art/VFX/audio exist; **targeting does not** | `Bomb.Explode()` exists · **NEW: cluster query + auto-throw scheduling** | M | **HYPOTHESIS** |
| **Soul Burst** | every N kills | explosion centred on the player | radial shockwave | `Bomb.Explode()` OverlapSphere pattern is reusable; **NEW: kill-count trigger** | S–M | **HYPOTHESIS** |

### 12.4 Universal modifier (1) — DESIGN HYPOTHESIS

| Card | Trigger | Effect | Primitive | Cx | Status |
|---|---|---|---|---|---|
| **Execution Round** | hit on enemy below 20 % HP | large bonus damage | `IDamageable` HP is readable at damage time; **NEW: threshold check** | S | **HYPOTHESIS** |

**Total: 15 candidates = 5 stat + 6 signature + 3 autonomous + 1 universal.** Ten are mechanic/power
cards. Every one of the ten is a **hypothesis requiring a prototype**, not an approved feature.

### 12.6 Reuse audit — what actually exists in source

An earlier draft over-claimed reuse. Verified against production source:

| Claimed primitive | Reality | Verdict |
|---|---|---|
| `RangeFalloff` (Point Blank) | `WeaponData.RangeFalloff(distance01)` exists and is applied in `Weapon.cs:652` | **EXISTING REUSABLE MECHANIC** |
| `knockback` (Concussion) | `WeaponData.knockback` exists; `Weapon.cs:666` calls `ZombieBase.ApplyPhysicalPush` | **EXISTING REUSABLE MECHANIC** |
| Fire-rate multiplier (Run & Gun, Bullet Hose) | `Weapon.PerkedFireRate` exists (`Weapon.cs:120`) | **EXISTING REUSABLE MECHANIC** |
| Explosion (Soul Burst, Ordnance Core) | `Bomb.Explode()` does `Physics.OverlapSphere` + `TakeDamage`; Bomb prefab, VFX and audio exist | **EXISTING ASSET + PARTIAL MECHANIC** |
| Bomb throw physics | `BombThrower.TryThrow` / `ReleaseBomb` exist, driven by manual input | **EXISTING ASSET ONLY** — input path is being retired |
| **Chain lightning** | `FireMode.ChainLightning` is an **enum value only**; `chainCount`/`chainRange` are `0` on all 25 assets; `Weapon.cs` implements only `PiercingLine` among the special modes | **REQUIRES NEW RUNTIME PRIMITIVE** |
| **Densest-cluster targeting** | No cluster or density query exists anywhere in runtime | **REQUIRES NEW RUNTIME PRIMITIVE** |
| **Distance accumulator** (Quickstep Shot) | Not present | **REQUIRES NEW RUNTIME PRIMITIVE** |
| **Kill-count trigger** (Soul Burst) | `RunState.Kills` exists but no per-power trigger hook | **REQUIRES NEW RUNTIME PRIMITIVE** |

> A visual explosion asset is **not** a completed autonomous-power system. Three of the ten
> mechanic/power candidates need genuinely new runtime work, and two more need new trigger plumbing.

### 12.5 The cut — every removed candidate, with a reason and a destination

| Removed | Reason | Cheaper replacement | Destination |
|---|---|---|---|
| Ricochet | new bounce-target AI for low visual payoff | Chain Lightning already gives "it spreads" | LATER |
| Split Shot | 3× projectile count on a WebGL budget | Bullet Hose gives the same "more bullets" read | LATER |
| Hunter Mark, Duelist, Priority Kill | invisible damage bookkeeping; no 3-second feedback | Execution Round | CUT (duplicate role) |
| Overkill Burst | needs damage-overflow plumbing | Soul Burst | CUT |
| Lucky Chamber | random per-shot buff is unreadable under auto-fire | Quickstep Shot (deterministic) | CUT |
| Heat Engine | duplicates Bullet Hose | — | CUT (duplicate) |
| Knockback Rhythm | duplicates Concussion | — | CUT (duplicate) |
| Coin Trick | ties economy to combat rhythm; confusing | — | CUT |
| Last Bullet Fantasy | simulates a magazine the game deleted at M4 | — | **CUT (contradicts no-reload)** |
| Slipstream, Sawed-Off Dash, Backstep Guard | movement-state skills needing new locomotion code | Run & Gun | LATER |
| Shred | invisible armour bookkeeping | Execution Round | CUT |
| Spark Spray | duplicates Static Build-up | — | CUT (duplicate) |
| Momentum Battery | second-order (movement → cooldown) — too indirect to read | — | LATER |
| Shell Bloom | delayed-bloom VFX cost, unclear causality | Concussion | LATER |
| Breach | needs an armour system that does not exist | — | LATER |
| Deadeye, Long Line, Headline, Calm Trigger | rifle/marksman family — not in the slice | — | LATER (with Marksman) |
| Suppression, Thunder Belt, Walking Fortress, Hot Brass, Siege Rhythm | LMG family — not in the slice; Walking Fortress also rewards standing still | — | LATER (with LMG) |
| Thunder Field, Fire Trail, Frost Ring, Toxic Wake | each needs its own persistent-area primitive; three area powers is redundant at slice scale | Chain Lightning + Soul Burst | LATER (pick 1 next) |
| Orbit Blades, Guardian Orb | new persistent companion entity + collision | — | LATER |
| Meteor Ping | telegraph + delayed impact scheduling | Ordnance Core does the same job | CUT (duplicate) |
| Black Hole Pulse | physics pull destabilises the crowd system | — | **ICEBOX** |
| Clone Gunner, Storm Crown | new AI/animation/particle subsystems | — | **ICEBOX** |
| Death Payload | duplicates Ordnance Core with a worse trigger | Ordnance Core | CUT (duplicate) |

---

## 13. Pickup taxonomy — mixed FACT / PROPOSAL

| Pickup | Source | Freq | Visual | Magnet | Lifetime | Secure now | Defeat banking | Duplicate | Purpose | Status |
|---|---|---|---|---|---|---|---|---|---|---|
| **Coin** | kills, crates, containers | very high | gold coin, value-merged | yes, 3.5 m | 60 s | no | **yes, 25% on death** | merges by value | run currency + Hub currency | `EXISTS AND RUNS` |
| **Small Medkit** | kills (gated), crates | low | red cross, soft pulse | yes | 45 s | on contact | n/a | reroll to Coin at full HP | attrition answer | `EXISTS AND RUNS` |
| **Gem** | elites, boss chest, golden station | rare | violet, beam | yes | none | **yes** | **exempt** | stacks | rare meta currency | `EXISTS AND RUNS` |
| **Power Charge** | crates, station rewards | low | blue spark | yes | 45 s | on contact | n/a | — | instantly refill autonomous power cooldown | `PROPOSED` |
| **Emergency Detonation** | elites, rare crates | rare | red warning orb | yes | none | detonates on pickup | n/a | — | panic-button that needs no button | `REUSE CANDIDATE` (bomb art) |
| **Blueprint** | **boss chest (guaranteed)**, completed stations | reliable | schematic card, no roll | yes | **never despawns in the active ring** | **yes** | **exempt** | n/a — fungible, no duplicates | weapon unlocks, and nothing else | `PROPOSED` (W6 lock) |
| **Relic Fragment** | boss chest, mythic cache | very rare | **2D/billboard** gold shard + audio sting | yes | **never despawns in the active ring** | **yes** | **exempt** | → Gem | long-term collection | `PROPOSED` (W5 lock) |
| **Magnet Pulse** | rare crate | rare | wide blue ring | n/a | 30 s | instant | n/a | — | battlefield cleanup moment | `PROPOSED — LATER` |
| **Legendary Core** | boss chest, cursed challenge | very rare | rainbow core | yes | none | yes | exempt | — | opens a power evolution choice | `PROPOSED — LATER` |
| ~~Bomb pickup~~ | enemies | — | — | — | — | — | — | — | **RETIRED** — art becomes Emergency Detonation | `CUT` |

### 12.1 Explicit decisions

| Question | Decision | Reason |
|---|---|---|
| XP direct or physical orbs? | **Direct** (unchanged) | Hundreds of orbs force backtracking, fight the endless run's forward pressure, and add pickup load. Use a HUD particle for feel only. |
| Health drop adapts to low HP? | **Yes, capped + pity** | Chance rises below HP thresholds up to a cap; a drought counter guarantees one after N eligible kills. Prevents RNG deaths without free sustain. |
| Gem secured immediately? | **Yes** | A rare drop that a later death deletes teaches "don't take risks" — the opposite of the design. |
| Relic secured immediately? | **Yes** | Same reason, more strongly. |
| Blueprint secured immediately? | **Yes** | Progression toward a named weapon must not be gambled on survival, or players stop opening Boss Chests. |
| Is any weapon acquisition random? | **No** | Blueprint is fungible and deterministic — no drops, no duplicates, no pity. See §1d.2. |
| Can rare pickups despawn? | **No**, while inside the active 5×5 ring | Losing a Relic to a lifetime timer is a bug the player cannot see. |
| Does `WaveClearedEvent` auto-collect survive? | **NO — REMOVED** | `PickupManager.cs:141` currently calls `CollectAll()` on wave clear. Endless has no reliable wave boundary. |
| What replaces the bomb pickup? | **Emergency Detonation** + `Ordnance Core` card | Reuses existing Bomb prefab, VFX and audio with zero new art. |

### 12.2 Rarity and collection — `Relic Fragment`

Provisional, **clearly labelled hypotheses**, not balance locks:

```text
base chance          2%  per ELIGIBLE source (boss chest / mythic cache only — never per-kill, never per-second)
pity growth         +2%  per eligible source that misses
hard pity           guaranteed at the 25th–30th eligible source
duplicate           → 15 Gem  (never dead loot)
set size            6 fragments per Relic; 4 Relics at launch
reward              cosmetic set · weapon skin · power VFX variant · Archive lore badge
NEVER               raw permanent damage/HP — RNG must not become a power wall
```

Pity counts **eligible sources**, not time, so AFK cannot farm it.

---

## 14. Map object board — PROPOSAL

Every object uses one grammar:

```text
Signal + Trigger + Cost/Risk + Completion + Reward + Persistence
```

Trigger primitives: `enter/hold zone` · `pay currency` · `kill quota` · `survive timer` ·
`destroy target` · `defeat spawned target`
Cost primitives: `time/position` · `Coin` · `HP` · `Threat+` · `spawn elite/boss` · `lock an alternative`
Reward primitives: `heal` · `Coin/Gem` · `1-of-3 offer` · `upgrade a held power` · `temp buff` ·
`tiered chest` · `Relic`

**Six primitives produce every station below. No station gets its own subsystem.**

### 13.1 Signal Relay — MUST (first interactive built)

| Field | Spec |
|---|---|
| Signal | Vertical light beam, visible off-screen via compass tick |
| Frequency | Most common station: ~1 per 2–3 chunks |
| Placement | Global deterministic cell → `worldSeed + cell + archetype + slot` |
| Trigger | Enter and **hold** the zone |
| Cost / risk | Position lock + enemies keep spawning; pressure rises while charging |
| Leaving zone | **Charge decays slowly (does NOT reset)** — a mistake costs time, not the whole attempt |
| Completion | Charge bar full |
| Reward | 1-of-3 card offer (same UI as level-up) |
| Repeatable | No — becomes `exhausted`, visually dimmed |
| Persistence | `untouched → active → completed → exhausted`; survives chunk recycle |
| Chunk ownership | Anchor cell owns it; it is a gameplay entity, never baked into decoration mesh |
| Offscreen HUD | One compass tick; only one **pin** at a time |
| Failure | Player leaves permanently → returns to `untouched` with partial charge retained |
| Prop | **NOT YET PROVEN.** `Lamp1` is too thin to read alone. No existing prop communicates "hold this zone to charge". |
| Cost | MEDIUM |

### 13.2 Supply Cache — MUST

| Field | Spec |
|---|---|
| Signal | Coloured shipping container (`Container1` blue) with a marker - **credible body, evidenced** |
| Frequency | ~1 per 3–4 chunks |
| Trigger | **Pay Coin** (or short hold if the player is broke) |
| Cost | Coin — this is the **primary in-run Coin sink**, which is what gives Coin value before settlement |
| Completion | Instant on payment |
| Reward | Supply Chest → 1-of-3 minor card, or a reroll token |
| Repeatable | No |
| Persistence | `untouched → completed` |
| Failure | None (cannot fail, only skip) |
| Cost | LOW–MEDIUM |

### 13.3 Boss Beacon — MUST

| Field | Spec |
|---|---|
| Signal | Tall red beacon, largest compass icon; **shows boss type + reward tier before activation**. **Visual body NOT YET PROVEN** |
| Frequency | Rare: ~1 per 8–12 chunks |
| Trigger | Enter and confirm (single context action) |
| Cost / risk | Spawns a boss or elite pack; the player opted in |
| Enemy | **Existing assets only — no new boss authored.** `CactusBoss` (1400 HP), `MoleRatKing` (1100 HP), `SkeletonGiant` (900 HP, elite). Elites: `DogBowwow`, `SkeletonMage`, `CatLightning` |
| Arena | **No walls.** The boss chases; the player may always run |
| Completion | Boss defeated |
| Reward | Boss Chest → Legendary Core / Gem / Relic roll |
| Persistence | `untouched → spawned → defeated → chestClaimed`; **duplicate-spawn prevention via the stable ID** |
| Unload/reload | If the player leaves the active ring mid-fight, the boss despawns and the beacon returns to `untouched` — no orphan boss, no free chest |
| Failure | Player dies → run ends normally |
| Cost | MEDIUM (no new art) |

### 13.4 Medical Station — SHOULD

| Field | Spec |
|---|---|
| Signal | White/red container (`Container2` red) - **candidate only, not visually assembled** |
| Frequency | ~1 per 4–6 chunks |
| Trigger | Enter zone + pay |
| Cost | **Coin, or a short stand-still** — never free |
| Heal | ~40–50% max HP (ASSUMPTION — needs damage-per-minute simulation) |
| Low-HP relation | Does **not** scale with missing HP; visiting at full HP is deliberately wasteful |
| Repeatable | No — one use per stable world ID |
| Cost | LOW |

### 13.5 Route Scanner — SHOULD

| Field | Spec |
|---|---|
| Signal | Small antenna/lamp prop - **visual body NOT YET PROVEN** |
| Trigger | Enter zone (auto) |
| Cost | None — it is a **navigation** reward, not a power reward |
| Reward | Reveals the **2–3 nearest** station signals on the compass for ~60 s |
| HUD limit | Never more than 3 compass ticks + 1 pin |
| Cost | LOW — cheapest way to make a procedural map feel directed |

### 13.6 Greed Terminal — SHOULD

Trigger: activate. Cost: **permanent +Threat for this run**. Reward: `Coin ×1.25` and `reward tier +1`
for the rest of the run, or an immediate Elite Chest. UI must state the numbers plainly:
`Threat +10% / Reward tier +1`. Cost: LOW–MEDIUM.

### 13.7 Existing breakables — KEEP

| Object | Current behaviour | New role |
|---|---|---|
| `PROP_Crate_Loot` | `DestructibleProp.LootCrate`, 30 HP, pops loot | **Scrap Crate** — Coin, small heal, Power Charge |
| `PROP_Crate_Small` | same | same, smaller payout |
| `PROP_Barrel_Fuel` (red) | `ExplosiveBarrel`, damages everything nearby **including the player** | **KEEP unchanged** — reads as explosive |
| `PROP_Barrel_Fuel_B` (blue) | same | **Visual finding:** blue does not read as explosive. Either recolour or use as a non-explosive prop. |

### 14.9 World Signal Language — OWNER-LOCKED (W4), art not yet authored

**Station identity is prop-independent.** The prop sheet proves that **containers and barrels are
credible bodies** for Supply Cache and Explosive Barrel, and that existing loot crates are valid
breakables. It proves **nothing** about communicating station *function* — and Signal Relay and Boss
Beacon have no proven visual body at all.

Every station is therefore defined by an authored **World Signal Language**, not by its prop:

```text
ground ring · vertical beam · floating icon · emissive accent
progress indicator · distinct colour contract per station type · distinct audio motif
```

**The contract:** a station must read as its own function — "hold this zone to charge", "pay here",
"this spawns a boss" — from the signal layer alone, at gameplay distance, **before any prop is
chosen**. A prop may sit inside that language, or there may be no prop at all.

Two consequences: station design no longer depends on what art the repository happens to contain, and
a future prop swap becomes a cosmetic change rather than a redesign.

**This signal layer is authored art that does not exist yet.** It is the real cost of W4, it is art
work rather than programming, and **no assets are added in this pass**.

### 13.8 Later / Icebox

| Object | Disposition | Reason |
|---|---|---|
| Hunter Totem | **LATER** | Good rhythm variant, but only after Signal Relay proves the hold-zone is fun |
| Cursed Altar | **LATER** | Greed Terminal already owns the risk/reward decision; two is redundant |
| Black Market | **LATER** | Needs its own shop UI; Supply Cache is already the Coin sink |
| Distress Escort / Moving Shrine | **ICEBOX** | New AI, pathing, fail states and streaming ownership. XL. |

---

## 15. Reward containers — PROPOSAL

| Container | Contents | Source | Status |
|---|---|---|---|
| **Scrap Crate** | Coin, small heal, Power Charge | breakable props | `EXISTS` (retune) |
| **Supply Chest** | 1-of-3 minor card | Supply Cache, Signal Relay | `PROPOSED` |
| **Elite Chest** | upgrade a held power, or a rare card | elite kill, Greed Terminal | `PROPOSED` |
| **Boss Chest** | Legendary Core, Gem, Relic roll | Boss Beacon | `PROPOSED` |
| **Mythic Cache** | Relic chance + cosmetic token | very rare world cell | `PROPOSED — LATER` |

Tier must be readable by **colour + silhouette + audio**, never by reading a loot table mid-combat.

---

## 16. Resource ledger — OWNER-LOCKED (W6), tuning numbers still open

| Resource | Source | Sink | Purpose | Cadence | Cap | Conversion | Inflation risk | **MVP verdict** |
|---|---|---|---|---|---|---|---|---|
| **Coin** | kills, crates, containers, Greed Terminal | **in-run:** Supply Cache, Medical Station · **Hub:** weapons, common costume | The one currency the player thinks about | every run | none | — | Medium — needs sinks to keep value | **KEEP — primary** |
| **Gem** | elites, Boss Chest, golden stations | Hub: rare cosmetics, costume sets, Archive | Rare aspirational cosmetic lane | a few per run at most | none | Relic dupes → Gem | Low | **KEEP — rare** |
| **Blueprint** | **Boss Chest (guaranteed) + completed stations** — event-driven, never time | **weapon unlocks only** | The one non-luck path to a new weapon | reliable, every run | none | none — fungible, no duplicates | Low — single disjoint sink | **ADD — new, OWNER-LOCKED (W6)** |
| **Relic Fragment** | Boss Chest, Mythic Cache | Archive sets only | Long-term collection hook | rare | per-set | dupes → 15 Gem | None (not a currency) | **ADD — collection only, 2D/billboard art** |
| **Gold** | — (no in-run faucet exists) | weapon star upgrades | none distinct from Coin | — | — | — | — | **IN CODE, NO DESIGN AUTHORITY** |
| **Weapon Shard** | gacha duplicates | weapon star upgrades | makes new weapons start weak | — | — | — | High grind risk | **IN CODE, NO DESIGN AUTHORITY** |
| **Power Charge** | crates, stations | instant use | pickup, not a wallet | in-run | n/a | — | none | **ADD — pickup** |

### 16.1 Fates — DECIDED (W6)

Each row below is a **decision taken**, not a recommendation. **Nothing is deleted, hidden or
re-implemented in this pass**, and no UI changes. Full design: §1d and
`Review/M6_DecisionLock/ECONOMY_V2.md`.

| System | What it does today | Why it may be unnecessary | What breaks if it stays active | If made dormant | Status |
|---|---|---|---|---|---|
| **Coin** | Earned per kill, banked on run end, buys weapons and costumes | - | - | - | **OWNER-LOCKED as the primary common currency direction** |
| **Gem** | Earned from elites, banked, buys rare cosmetics and sets | - | - | - | **OWNER-LOCKED as the rare cosmetic/collection currency** |
| **Gold** | A second wallet currency; pays weapon star upgrades | Has **no in-run faucet** and makes no decision Coin does not already make | Two "common" currencies split player attention and double the pricing work | Code, wallet and UI stay untouched; design simply stops using it | **IN CODE, NO DESIGN AUTHORITY** — replaced by Coin + Blueprint |
| **Weapon Shards** | Gacha duplicates convert to shards; shards raise weapon stars | Only exists to feed star upgrades | Makes a newly unlocked weapon start weak — conflicts with the rule that a weapon unlocks at its **full designed power** (§1c.4) | `EconomyConfig` entries remain; nothing deleted | **IN CODE, NO DESIGN AUTHORITY** — replaced by Blueprint |
| **Weapon star upgrades** | 3 levels per weapon, costed in shards + Gold | Vertical weapon power hides the real problem, which is missing weapon identity | Encourages grinding one weapon instead of trying others | Costs stay in `EconomyConfig`, unused | **IN CODE, NO DESIGN AUTHORITY** — replaced by the authored tier ladder (§1c.2) |
| **Gacha** | Weapon pool with pity and duplicate conversion | Blueprint is the explicit non-luck path; a roll would reintroduce duplicates and pity | Adds a monetisation surface before the loop is proven fun | Pool config stays; entry point not surfaced | **IN CODE, NO DESIGN AUTHORITY** — replaced by deterministic Blueprint unlock |
| **Weapon unlock path** | `WeaponData.price` drives shop purchase in Coin today | Coin and Blueprint would compete for the same decision | - | - | **CHANGED — weapons cost Blueprint only; Coin buys everything else (§1d.2)** |
| **Costume purchase (Coin) / sets (Gem)** | Works today | - | - | - | **OWNER-LOCKED — keep** |
| **Relic Fragment** | Does not exist | - | - | - | **OWNER-LOCKED (W5) — collection-only, 2D/billboard art** |
| **Blueprint** | Does not exist | - | - | - | **OWNER-LOCKED (W6) — new fungible weapon unlock resource; all costs `TUNING`** |

> Existing code is **not** a justification to keep a system in the design — and equally, losing design
> authority is **not** deletion. Everything marked **IN CODE, NO DESIGN AUTHORITY** stays in the
> repository untouched, and is described that way rather than as "dormant pending approval": that
> decision has been taken (W6).

---

## 17. Run flow and settlement

### 16.1 Pacing checkpoints (ASSUMPTION — to be validated by playtest)

| Time | Event |
|---|---|
| 0:00 | Spawn, immediate control, one visible signal within ~1–2 chunks |
| 0:30–0:45 | **First level-up** — identity card, not a stat card |
| 1:30 | First Signal Relay reachable |
| 2:30 | First station reward resolves; second level-up |
| 3:30 | First Supply Cache — Coin becomes spendable |
| 5:00 | Greed Terminal / first elite opportunity |
| 7:00 | Build is legible: 1 weapon identity + 1 autonomous power + 1 synergy |
| 9:00 | First Boss Beacon worth taking |
| 10:00+ | Threat keeps rising; route choice between heal / cache / beacon / rare signal |
| 15:00+ | **Repetition control:** station density thins, Threat growth continues → runs end by attrition, not boredom |

### 16.2 Level-up cadence — REPLACES the current curve

Current `XpForNextLevel = 10 + (Level-1)×8` delivers the first choice at ~10 kills and ~8 level-ups per
short run. For an endless run the curve must **widen**:

```text
PROPOSED (hypothesis):  XpForNextLevel = 25 + (Level-1)^1.35 × 12
target: first choice ~30-45s, then widening; ~10-14 choices in a 15-minute run
```

Pause is **max 30 s unscaled**; on timeout the highlighted / best-fit valid card auto-applies.

### 16.3 Threat model

```text
Threat = f(time alive) + f(distance travelled) + Σ(station activations) + Greed Terminal modifiers
```

Threat raises **spawn budget and composition first**, global HP/damage multipliers last.

### 17.4 Settlement — OWNER-CORRECTED

| Ending | Trigger | Coin | Gem / Relic | Screen |
|---|---|---|---|---|
| **Death** | HP reaches 0 | **bank 25 %** | already secured | Result |
| **Manual abandon / quit** | player quits from pause, **with confirmation** | **bank 0 %** | already secured | Result |
| ~~Victory~~ | ~~all waves cleared~~ | — | — | **REMOVED — no such state in an endless world** |
| Technical recovery | app interruption | idempotent; never double-pays | — | — |

> **REJECTED — do not re-propose.** An earlier draft banked **100 %** on a manual "End Run". That
> creates a dominant exploit: the optimal play becomes quitting the instant danger appears, which makes
> death risk and endless pressure fake. Manual abandon is **not** a victory and **not** a successful
> settlement — it forfeits common Coin.

Gem, **Blueprint** and Relic are exempt from banking in every ending: they are secured the moment they
are picked up, so abandoning never destroys a rare drop or weapon progress the player already earned.
**Only Coin is at risk** — that keeps death meaningful without making any long-term goal reversible.

**PROPOSAL — future, not MVP.** A `Secure Station / Banking Shrine` could let the player deliberately
risk time, position or an encounter to secure a portion of common Coin mid-run. That would restore a
legitimate "bank it now" decision without rewarding a panic quit. It is **not designed in this pass**.

Result screen shows only: time survived · kills / bosses · final build · Coin earned vs banked (with
the 25 % / 0 % reason stated) · Gem / **Blueprint** / Relic secured · new unlocks. **The word
"Victory" does not appear.**

---

## 18. Player journeys — HYPOTHESIS

### Journey A — first run

```text
Hub      Sidearm_FiveSeven (starter, auto-seeded by CatalogOrder) + default outfit
0:00     movement + auto-fire teach themselves; no tutorial modal
0:40     LEVEL 1-of-3 → offer is [Run & Gun] [Damage +15%] [Move Speed +10%]
         (rule: max one stat card per offer, so an identity card is always present)
1:40     first Signal Relay beam — holds the zone under pressure, earns Static-style card
4:00     first Supply Cache — has 180 Coin, spends 120, gets a card. Coin now means something.
6:30     dies to a Runner pack
         Settlement: 6:31 survived · 143 kills · banked 25% of 420 Coin = 105
Hub      sees Sidearm_M1911 at 500 Coin → a concrete goal exists after one run
```

**Teaches:** move, level up, one signal. Does **not** open Shop, Gacha or Archive.

### Journey B — early returning player (run 3–5)

```text
Hub      switches to SMG_Generic — the run feels different from the first press of fire
0:45     Static Build-up → the first chain-lightning discharge is the "oh" moment
3:00     Supply Cache spends Coin mid-run without hesitation
5:30     Greed Terminal: accepts Threat +10% for reward tier +1
8:00     Boss Beacon shows "MoleRatKing · Boss Chest" → opts in, wins
         first Gem drops and is SECURED IMMEDIATELY (visible "secured" flash)
9:40     dies. Build was visibly electric: chain lightning + soul burst + bullet hose
```

**Feels:** the gun changed the run; the map had destinations; risk paid.

### Journey C — long-term collector

```text
Goal     complete Relic set 2/4 · unlock the pirate costume set
Plays    Shotgun_AA12 because Boss Beacons die fastest to burst → Relic sources are boss chests
Hub      randomizes an outfit, then hand-tunes two slots
Drive    personal best survival time · Archive completion · a 4th weapon family
```

If long-term motivation is only "bigger damage number", the meta has failed. It is **collection +
variety**, not power.

## 19. Six build simulations — HYPOTHESIS

| # | Weapon | Card sequence | Player behaviour | Visual result | Strength | Weakness | Map decisions | Primitives needed |
|---|---|---|---|---|---|---|---|---|
| 1 | **Pistol** *Storm Runner* | Run & Gun → Chain Lightning → Move Speed → Quickstep Shot → Soul Burst | Never stops circling | Trail of lightning arcs behind a sprinting player | Excellent vs packs; always escapable | Melts on any forced stand-still | Skips hold-zones; loves Route Scanner | velocity read, chain select |
| 2 | **Pistol** *Executioner* | Quickstep Shot → Execution Round → Damage → Ordnance Core → Fire Rate | Sprint-stop-snipe rhythm | Clean single-target pops, bombs on the crowd | Kills elites fast | Weak raw clear | Prioritises Boss Beacon | distance accumulator, HP check |
| 3 | **SMG** *Thunder Hose* | Bullet Hose → Static Build-up → Chain Lightning → Fire Rate → Soul Burst | Seeks the densest crowd | Screen-wide electric web | Best crowd clear in the game | Poor vs a lone boss | Farms Relays, avoids Beacons early | ramp timer, chain select |
| 4 | **SMG** *Attrition* | Static Build-up → Max Health → Concussion-free, Ordnance Core → Coin Gain | Mid-range grinder | Steady bombs + arcs | Longest survival time | Slow, low spectacle | Buys every Cache and Medical Station | cluster query |
| 5 | **Shotgun** *Breacher* | Point Blank → Concussion → Max Health → Soul Burst → Damage | Charges **into** the horde | Bodies flung outward, then a burst | Highest burst; controls space | Ranged archetypes (r9–r12) punish it | Takes Beacons early; needs Medical Stations | knockback reuse, falloff curve |
| 6 | **Shotgun** *Bomber* | Ordnance Core → Point Blank → Execution Round → Move Speed → Chain Lightning | Close for the kill, bombs cover range | Constant explosions at mid-range | Covers the shotgun's one real weakness | Cooldown-dependent | Hunts crowds for Ordnance value | cluster query |

**None of these six differ only by DPS.** Each changes *where the player stands* and *what they walk
toward* — which is the test the previous perk pool failed.

---

## 20. Smart outfit randomizer — H2 APPROVED FOR PRODUCTION (W7), metadata authoring outstanding

Backed by the 300-outfit experiment in `Review/M6_Conceive/M6_OUTFIT_GRAMMAR_PROPOSAL.md`.

```text
H0 (the randomizer shipping today)  PASS 17%  · hard conflicts 43% · 11.8 items worn
H1 (hard rules only)                PASS 58%  · hard conflicts  0% · theme mismatch 33%
H2 (hard rules + scored softmax)    PASS 73%  · hard conflicts  0% · theme mismatch  4%
```

> ## H2 — APPROVED FOR PRODUCTION (W7, 2026-08-15)
>
> **Gate, as set by the owner:**
>
> ```text
> 0 % hard conflicts        MANDATORY   -> measured 0 % over 300 seeded outfits
> ~70 %+ thematic coherence ACCEPTABLE  -> measured 73 % over the same 300
> >= 300 seeded outputs                 -> met
> Manual Wardrobe remains the curated path
> ```
>
> **The earlier `>= 85 %` bar was mine and is superseded by owner decision — it was replaced, not
> lowered.** The distinction matters: the mandatory criterion became *stricter in kind*, from a blended
> pass rate to an absolute zero on structural defects.
>
> **Approval is not completion.** Costume metadata authoring over 453 items and the post-authoring
> re-test are still outstanding. If authored metadata reintroduces any hard conflict or drops coherence
> below 70 %, H2 does not ship on that metadata — the fallback is H1 hard rules only.
>
> **Claim discipline:** the measured result is *0 hard conflicts over a 300-seed sample*, with the
> residual 27 % being **thematic mismatches, not structural defects**. Never write "0 % failure".

**Preserved measured findings:**

- H2 is clearly and substantially better than H0, the randomizer shipping today.
- H2 removed **100 %** of the measured hard conflicts (43 % -> 0 %).
- **Pixel-area silhouette measurement was unreliable** - it ranked wide bobs above tall mohawks and
  missed the hair that actually clips through helmets.
- Production silhouette metadata must use **3D mesh bounds plus authored exception tags**, not render
  pixels. Colour extraction from renders remains valid.
- **Body compatibility must be explicit** - beards currently land on feminine and child-read faces.

### 19.1 Model

```text
choose visual anchor (Head OR Chest OR Back)
  ↓ choose compatible style family
  ↓ choose main + secondary + neutral colour family
  ↓ fill required slots (Eye, Brow, Mouth, Hair, Chest, Legs)
  ↓ limit silhouette mass (≤1 bulky anchor)
  ↓ limit accessories (≤3)
  ↓ score → reject hard conflicts → sample from the top-scoring valid candidates
```

**Head is resolved before Hair** — otherwise the hair-suppression rule cannot be expressed.

### 19.2 Style families (visually audited, `Evidence/Costume_StyleFamilies.png`)

`casual` (default, majority) · `fantasy` (32) · `athletic/street` (19) · `formal/school` (18) ·
`costume/animal` (14) · `military/utility` (6)

### 19.3 Colour families (**measured** from rendered pixels, `Evidence/Costume_ColorFamilies.png`)

`grey/neutral` 62 · `orange` 45 · `blue` 45 · `red` 24 · `white` 18 · `cyan` 11 · `yellow` 10 ·
`green` 7 · `purple` 7 · `black` 5 · `pink` 5

The library is strongly biased to neutral + orange + blue, so a "1 main + 1 secondary + neutral" rule is
comfortably satisfiable.

### 19.4 Hard conflicts (`Evidence/Costume_SilhouetteConflicts.png`)

```text
hairReplacing hat        SATISFIES the Hair slot (suppress Hair — do NOT conflict)
fullHood/closedHelmet    ✗ big hair · ✗ Earring · ✗ HairAccessory
faceGuard                ✗ Eyewear · ✗ Mask · ✗ Beard
fullHood                 ✗ Eyewear
Mask                     ✗ Beard
Bracelet                 ✗ Watch          (same wrist anchor)
```

### 19.5 Proposed schema (ScriptableObject-friendly, **not created**)

```text
slot · themes[] · paletteFamily · dominantColour · accentColour
silhouetteClass · visualWeight · accentLevel · faceCoverage · headOcclusion
anchorEligible · setId · pairTags[] · hardConflictTags[]
bodyCompatibility        ← NEW: any | masculineOnly | feminineOnly
clippingRisk · qualityStatus
```

### 19.6 Two honest corrections to the earlier proposal

1. **2D pixel mass is NOT a valid silhouette/volume proxy.** The conflict sheet exposed this: a wide
   flat bob has more pixels than a tall mohawk, so the "big hair" class picked bobs and **missed the
   mohawk and twin-buns that actually clip through helmets**. Production must bake `visualWeight` and
   `silhouetteClass` from **3D mesh bounds relative to the head/body socket**, not from render pixels.
   Colour extraction from renders remains valid.
2. **`bodyCompatibility` is required, not optional.** Both the production randomizer and H1/H2 place
   beards and moustaches on visibly feminine and child-read faces. No catalog field can currently
   express this.

### 19.7 Cost

| Work | Cost |
|---|---|
| Metadata authoring (453 parts; colour bakes automatically, style/set/body need a designer) | MEDIUM (~1–2 designer-days) |
| Editor bake + validation tooling (render harness already exists from this phase) | LOW–MEDIUM |
| Runtime selection (~8 rules + weighted sample) | LOW |
| Visual QA per wardrobe growth | MEDIUM |

---

## 21. Difficulty and content scalability — INFERENCE

Content scales by **combination**, not by new assets:

```text
6 enemy archetypes × 4 threat compositions × 6 station types × 6 weapon families × 23 cards
```

The enemy roster already provides the archetypes: `Walker` (4) · `Runner` (3) · `Ranged` (3) ·
`Burrower` (2) · `Heavy` (1 elite) · `Boss` (2). **No new enemy art is required for M6.**

## 22. Persistence and streaming implications — PROPOSAL

Consistent with `WORLD_STREAMING_TECHNICAL_DESIGN.md`; **no contradiction found, so that document is
not modified.**

- Interactives are placed from **global deterministic cells** (`worldSeed + cell + archetype + slot`),
  never from chunk-local random.
- An interactive is a **gameplay entity**, never baked into decoration mesh.
- Budget: average **0–1 major interactive per chunk**, with global spacing so a 5×5 ring holds roughly
  **3–7 notable signals**.
- State: `untouched · active · completed · exhausted` (+ `spawned · defeated · chestClaimed` for Boss
  Beacon). **Chunk recycling never resets a used station.**
- **Boss unload rule (locked):** leaving the active ring mid-fight despawns the boss and resets the
  beacon to `untouched`. No orphaned boss, no free chest.
- HUD: **1 pin + ≤3 compass ticks**, never more.

---

## 23. Scope board — LOCKED FRAME, item costs still estimates

> **`W1`–`W7` are answered (§1).** The dispositions below are the scope of M6. Cost and risk columns
> remain estimates, and every item still carries whatever `HYPOTHESIS` or `TUNING` label it had — a
> locked scope is not a proven design.

| Item | Impact | Cost | Runtime risk | Depends on | Disposition | Reason |
|---|---|---|---|---|---|---|
| One-weapon run contract | ★★★ | MED | LOW | — | **MUST** | Everything else depends on it |
| ~~3 prototype weapons~~ | — | — | — | — | **RETIRED** | Superseded by W1: the arsenal is a Factory problem. No fixed weapon count is a scope boundary. |
| Weapon Visual Onboarding Gate G1–G5 (blocking checks) | ★★★ | MED | LOW | — | **MUST** | W3 lock — a weapon that fails G1/G2 is unreadable, and G3 already shipped a defect |
| Rig-relative grip-validation capture (G5 tool) | ★★★ | MED | LOW | — | **MUST** | G5 has never been run on any weapon, including the shipped 25 |
| Tier ladder + re-tier the 16 mismatched assets | ★★★ | LOW | LOW | balance model | **MUST** | W3 lock — 16 of 24 authored tiers disagree with measured power |
| 5 stat cards (existing) | ★ | — | — | — | **MUST** (keep) | Already implemented |
| 1-stat-card-per-offer rule | ★★★ | LOW | LOW | — | **MUST** | Single cheapest fix for the dominant strategy |
| 12 weapon signature cards (2 per family) | ★★★ | MED | MED | weapons | **MUST** | Creates build identity; all 23 cards are in scope (W2) |
| Chain Lightning | ★★★ | MED | MED | — | **MUST** | The spectacle anchor |
| Ordnance Core | ★★ | MED | LOW | bomb art | **MUST** | Reuses retired grenade content |
| Soul Burst | ★★ | LOW-MED | LOW | — | **MUST** | Cheapest autonomous power |
| Execution Round | ★★ | LOW | LOW | — | **MUST** | Universal, readable |
| Widened XP curve | ★★★ | LOW | LOW | — | **MUST** | Current cadence interrupts constantly |
| Remove wave-clear auto-collect | ★★ | LOW | LOW | — | **MUST** | Endless has no wave boundary |
| Endless settlement (death 25 % / abandon 0 %) | ★★★ | MED | LOW | — | **MUST** | Removes fake Victory and the quit-before-danger exploit |
| Signal Relay | ★★★ | MED | MED | — | **MUST** | Proves the world is worth traversing |
| Supply Cache | ★★ | LOW-MED | LOW | Coin | **MUST** | Makes Coin matter in-run |
| Boss Beacon (existing enemies) | ★★★ | MED | MED | elites | **MUST** | Risk/reward climax, no new art |
| Boss / Elite Chest | ★★ | LOW | LOW | Beacon | **MUST** | Payoff for the above |
| Coin | ★★★ | — | — | — | **MUST** (keep) | Primary currency |
| Gem (secure on pickup) | ★★ | LOW | LOW | — | **MUST** | Rare lane |
| Explosive Barrel / Scrap Crate | ★ | — | — | — | **MUST** (keep) | Already works |
| Medical Station | ★★ | LOW | LOW | Coin | **SHOULD** | Attrition answer |
| Route Scanner | ★★ | LOW | LOW | HUD | **SHOULD** | Cheapest directional aid |
| Greed Terminal | ★★ | LOW-MED | LOW | Threat | **SHOULD** | Player-authored difficulty |
| Power Charge | ★ | LOW | LOW | powers | **SHOULD** | — |
| Emergency Detonation | ★ | LOW | LOW | bomb art | **SHOULD** | Reuse |
| Relic Fragment + Archive | ★★ | LOW-MED | LOW | chests | **SHOULD** | Long-term hook; cost revised down — 2D/billboard art (W5) |
| **Blueprint** resource + shop unlock path | ★★★ | MED | LOW | chests, tier ladder | **MUST** | W6 lock — the non-luck path to every new weapon |
| World Signal Language (station identity layer) | ★★★ | MED | LOW | — | **MUST** | W4 lock — stations must read without depending on any prop |
| Smart outfit randomizer (H2) | ★★ | MED | LOW | metadata authoring | **MUST** | W7 lock — approved for production at 0 % hard conflicts |
| `bodyCompatibility` field | ★ | LOW | LOW | schema | **SHOULD** | Fixes a visible error |
| Growing SMG / Marksman beyond one body each | ★★ | MED | MED | G1/G2 conversion | **SHOULD** | Single-body families; `SMG_P` and `Recon_P` are the two best unused bodies |
| Hunter Totem · Cursed Altar · Black Market | ★ | MED | MED | Relay proven | **LATER** | Redundant for now |
| Magnet Pulse · Legendary Core · Mythic Cache | ★ | MED | LOW | — | **LATER** | — |
| Remaining ~20 skill candidates | ★ | HIGH | MED | — | **LATER** | See §11.5 |
| Gold · Weapon Shard · star upgrades · Gacha | ✗ | — | — | — | **IN CODE, NO DESIGN AUTHORITY** | W6 lock. Code stays untouched; design does not use it. Not "pending approval" |
| Manual grenade button | ✗ | — | — | — | **CUT** | Retired; content reused |
| Extraction as core structure | ✗ | — | — | — | **CUT** | Endless has no mandatory exit |
| Five-wave Victory | ✗ | — | — | — | **CUT** | No such state |
| Maps 2–5 / campaign progression | ✗ | — | — | — | **CUT** | Already retired |
| 3-weapon in-run switching | ✗ | — | — | — | **CUT** | Contradicts one-weapon |
| Escort · Moving Shrine · Black Hole · Clone Gunner · Storm Crown | ✗ | VERY HIGH | HIGH | — | **ICEBOX** | New subsystems |

## 24. Implementation ladder — **M7 NOT STARTED / NOT AUTHORIZED**

> **M6 is locked; M7 is a separate authorization and has NOT been given.** This ladder exists so the
> owner can see the shape and cost of what M7 approval would trigger. **Do not begin any step.**

| Step | Deliverable | Pass condition |
|---|---|---|
| **M7.1** | One-weapon contract + widened XP curve + 1-stat-per-offer rule + re-tier the 16 mismatched `WeaponData` assets | Two runs with the same weapon differ; no offer is all-stats; `tier == band(powerBudgetUsed)` for every weapon |
| **M7.2** | Offer system + approved subset of the 23 cards | ≥2 visibly different builds per family |
| **M7.3** | Signal Relay + compass/pin HUD | Players leave a safe position to reach a signal |
| **M7.4** | Supply Cache + in-run Coin sink | Players spend Coin before dying |
| **M7.5** | Boss Beacon + Boss Chest using existing elites | Players opt into a boss they could have skipped |
| **M7.6** | Endless settlement + remove wave-clear auto-collect | No "Victory" string reachable; banking correct |
| **M7.7** | Relic Archive + Blueprint shop path + Gem/Blueprint/Relic secure-on-pickup | A rare drop survives a death; a locked weapon shows a countable `n / N` Blueprint target from run one |
| **M7.8** | Costume metadata authoring + H2 randomizer (**approved for production**, W7) | Metadata authored over 453 items; measured over **≥300 seeded outfits**: **0 hard conflicts (mandatory)** and **≥70 % thematic coherence**; Manual Wardrobe unaffected. **The earlier ≥80 % / 85 % bars are superseded by owner decision.** |

**Smallest coherent slice = M7.1 + M7.2 + M7.3.** If players will not voluntarily start a third run
after those three, nothing later is worth building.

## 25. TUNING HYPOTHESES - require simulation / playtest

**None of the numbers below is locked balance.** They are starting points only.

| Variable | Starting hypothesis | Who approves | How it gets tested |
|---|---|---|---|
| XP curve constants | `25 + (L-1)^1.35 x 12` | designer, then owner | simulate level count over a 15-min run; playtest interruption feel |
| Threat growth per minute / per activation | unset | designer | simulate deaths-per-minute vs player power curve |
| Signal Relay charge time and decay rate | unset | designer | playtest - must be tense, not tedious |
| Supply Cache Coin price | unset | designer | must be affordable by ~3:30 on an average run |
| Medical Station heal % and price | 40-50 % max HP | designer | simulate damage-taken-per-minute first |
| Greed Terminal numbers | `Threat +10 % / reward tier +1` | owner | playtest - must feel worth it |
| Relic base chance | 2 % per eligible source | **owner** | simulate expected sources-to-first-Relic |
| Relic pity growth | +2 % per miss | **owner** | same simulation |
| Relic hard pity | 25-30 eligible sources | **owner** | same simulation |
| Relic duplicate conversion | -> 15 Gem | **owner** | economy simulation |
| Small Medkit heal | 15-20 % max HP | designer | damage-per-minute simulation |
| Low-HP drop-chance curve and cap | unset | designer | must not become free sustain |
| Boss Beacon frequency | 1 per 8-12 chunks | designer | playtest pacing |
| Station spacing | 3-7 signals per 5x5 ring | designer | playtest - HUD must stay legible |
| Power cooldowns | unset | designer | playtest spectacle vs autopilot |
| **Coin banking** | **death 25 % / abandon 0 %** | **OWNER-CORRECTED - already decided** | not a tuning variable |
| Level-up pause 30 s, auto-pick on timeout | - | **OWNER-LOCKED** | not a tuning variable |

Simplifier text must not present any of these as settled fact.

## 26. What this document explicitly retires

| Obsolete assumption | Where it came from | Replaced by |
|---|---|---|
| Bounded expedition with two field objectives | `GAME_DESIGN.md` §5, §22 | Endless run with optional stations |
| Extraction as the mandatory ending | `GAME_DESIGN.md` §14, §20 | Death or manual end; extraction not required |
| Finite five-wave Victory | `WD_Level1`, `RunDirector.OnAllWavesCleared` | Endless pressure; no Victory state |
| Campaign progression through Maps 2–5 | Campaign catalog, `MVP_SHIP_PLAN` | One endless world |
| Three-weapon in-run switching | `LoadoutState` slots 0–2 | One weapon per run |
| Manual grenade button + charges | `BombThrower`, `Bomb`, HUD | `Ordnance Core` + `Emergency Detonation` |
| Auto-collect on `WaveClearedEvent` | `PickupManager.cs:141` | Removed — no wave boundary in endless |
| Purely numeric perk pool as the final skill system | `RunPerkPool` (7 entries) | 23-card catalog; stat cards demoted to filler |
| MW4-specific onboarding gate | M6.1 packet | Universal Weapon Visual Onboarding Gate G1–G8 (W3) |
| "A new weapon must be immediately playable **at base level**" read as "all weapons are equal power" | `GAME_DESIGN.md` §18 | Full designed power at unlock + controlled tier bands (§1c.4) |
| Weapons bought with Coin | `WeaponData.price` | Weapons cost **Blueprint** only; Coin buys everything else (W6) |
| Gold / Shard / star upgrades / Gacha as live design | `EconomyConfig` | Present in code, **without design authority** (W6) |
| H2 blocked by an 85 % pass gate | M6.1 packet | H2 **approved for production** at 0 % hard conflicts + ~70 %+ coherence (W7) |
| Station identity depends on finding the right prop | prop candidate sheet | Prop-independent **World Signal Language** (W4) |
| Any fixed weapon count as a design boundary ("3 prototype weapons", "the 25") | earlier M6 drafts | Families and properties; adding weapon N changes data only (W1) |

---

## 27. Appendix A — complete content atlas — FACT

Status legend: `EXISTS AND RUNS` · `EXISTS BUT UNUSED` · `PARTIAL` · `PROPOSED` · `REUSE CANDIDATE` · `CUT`

### A1 — All 25 `WeaponData` assets

| ID | Family | Tier | DPS | Range | Notes | Status | M6 role |
|---|---|---|---:|---:|---|---|---|
| WD_Sidearm_PistolA | Sidearm | Common | 48 | 8.0 | starter candidate | `EXISTS AND RUNS` | cosmetic variant |
| WD_Sidearm_Makarov | Sidearm | Common | 55 | 7.5 | white grip | `EXISTS AND RUNS` | cosmetic variant |
| WD_Sidearm_Glock19 | Sidearm | Common | 60 | 7.5 | — | `EXISTS AND RUNS` | cosmetic variant |
| WD_Sidearm_P226 | Sidearm | Common | 65 | 7.5 | — | `EXISTS AND RUNS` | cosmetic variant |
| WD_Sidearm_Python357 | Sidearm | Rare | 67 | 7.5 | revolver | `EXISTS AND RUNS` | later variant |
| WD_Sidearm_BerettaM9 | Sidearm | Common | 72 | 7.5 | — | `EXISTS AND RUNS` | cosmetic variant |
| WD_Sidearm_M1911 | Sidearm | Uncommon | 72 | 7.5 | — | `EXISTS AND RUNS` | cosmetic variant |
| WD_Sidearm_USP45 | Sidearm | Uncommon | 72 | 7.5 | tan | `EXISTS AND RUNS` | cosmetic variant |
| WD_Sidearm_DesertEagle | Sidearm | Rare | 78 | 7.5 | distinct silhouette | `EXISTS AND RUNS` | later variant |
| **WD_Sidearm_FiveSeven** | Sidearm | Uncommon | **91** | 7.5 | tan, readable | `EXISTS AND RUNS` | **PROTOTYPE — Runner** |
| **WD_SMG_Generic** | SMG | Common | **112** | 5.8 | only SMG | `EXISTS AND RUNS` | **PROTOTYPE — Static** |
| WD_LMG_Generic | LMG | Common | 143 | 9.5 | best silhouette in the arsenal | `EXISTS AND RUNS` | later family (4th) |
| WD_AssaultRifle_Generic | AR | Common | 144 | 9.0 | — | `EXISTS AND RUNS` | later variant |
| WD_AssaultRifle_AK47 | AR | Uncommon | 168 | 9.0 | orange wood | `EXISTS AND RUNS` | later variant |
| WD_AssaultRifle_M4A1 | AR | Uncommon | 171 | 9.0 | — | `EXISTS AND RUNS` | later variant |
| WD_AssaultRifle_SCARL | AR | Rare | 198 | 9.0 | tan | `EXISTS AND RUNS` | later variant |
| WD_AssaultRifle_FAMAS | AR | Rare | 220 | 9.0 | — | `EXISTS AND RUNS` | later variant |
| WD_AssaultRifle_G36C | AR | Epic | **240** | 9.0 | **highest DPS in game — balance risk** | `EXISTS AND RUNS` | later variant |
| WD_Marksman_SniperGeneric | Marksman | Common | 81 | 16.0 | **pierce 3 — only unique mechanic** | `EXISTS AND RUNS` | later family |
| WD_Shotgun_DoubleBarrel | Shotgun | Uncommon | 66 | 5.5 | break-action silhouette | `EXISTS AND RUNS` | later variant |
| WD_Shotgun_BenelliM4 | Shotgun | Common | 67 | 5.5 | — | `EXISTS AND RUNS` | cosmetic variant |
| WD_Shotgun_Mossberg500 | Shotgun | Uncommon | 83 | 5.5 | — | `EXISTS AND RUNS` | cosmetic variant |
| WD_Shotgun_Generic | Shotgun | Common | 90 | 5.0 | — | `EXISTS AND RUNS` | cosmetic variant |
| WD_Shotgun_SPAS12 | Shotgun | Rare | 130 | 5.5 | — | `EXISTS AND RUNS` | cosmetic variant |
| **WD_Shotgun_AA12** | Shotgun | Epic | **192** | 5.5 | drum mag, distinct | `EXISTS AND RUNS` | **PROTOTYPE — Breacher** |

All 25 accounted for. All have prefabs. All share `buildTag = "generalist"`, empty `roleTag`/`buildHint`,
`resourceModel = Magazine` (**dead data** since M4 removed magazines) — authoring these fields is M7 work.

### A2 — All 7 existing `RunPerk` entries

| ID | Title | Effect | Numeric | Useful as filler? |
|---|---|---|---|---|
| `perk.damage.s` | Damage +15% | ×1.15 damage | yes | yes |
| `perk.damage.m` | Damage +30% | ×1.30 damage | yes | yes (as rank 2) |
| `perk.firerate.s` | Fire Rate +12% | ×1.12 fire rate | yes | yes |
| `perk.firerate.m` | Fire Rate +25% | ×1.25 fire rate | yes | yes (as rank 2) |
| `perk.speed.s` | Move Speed +10% | ×1.10 move speed | yes | yes — supports Runner |
| `perk.health.s` | Max Health +20% | +20% max & current at pick | yes | yes — only defensive card |
| `perk.coin.s` | Coin Drops +25% | ×1.25 Coin | yes | yes — once Coin has sinks |

All five `RunPerkKind` values are consumed by runtime today (`Weapon.cs:650`, `Weapon.cs:120`,
`PlayerMovement.cs:78`, `RunState.cs:129`, `RunOverlays.cs:375`).

### A3 — All 16 enemy archetypes

| Asset | Archetype | Elite | HP | Dmg | Spd | Range | XP | Coin | Boss Beacon? | Status |
|---|---|---|---:|---:|---:|---:|---:|---:|---|---|
| ZD_DogPup | Walker | — | 40 | 6 | 2.4 | 1.3 | 1 | 1 | no | `EXISTS AND RUNS` |
| ZD_CatMeow | Walker | — | 45 | 7 | 2.6 | 1.3 | 1 | 1 | no | `EXISTS AND RUNS` |
| ZD_Skeleton | Walker | — | 60 | 9 | 2.5 | 1.5 | 2 | 2 | no | `EXISTS AND RUNS` |
| ZD_Zombie | Walker | — | 100 | 10 | 3.2 | 1.6 | 1 | 1 | no | `EXISTS BUT UNUSED` |
| ZD_DogBark | Runner | — | 55 | 10 | 4.6 | 1.4 | 2 | 2 | no | `EXISTS AND RUNS` |
| ZD_CatBolt | Runner | — | 50 | 11 | 5.2 | 1.4 | 3 | 2 | no | `EXISTS BUT UNUSED` |
| ZD_CatLightning | Runner | — | 60 | 12 | 5.0 | 1.4 | 3 | 3 | **elite** | `EXISTS BUT UNUSED` |
| ZD_DogBowwow | Runner | — | 110 | 16 | 4.2 | 1.8 | 4 | 4 | **elite** | `EXISTS BUT UNUSED` |
| ZD_Cacti | Ranged | — | 40 | 8 | 1.8 | 9.0 | 2 | 2 | no | `EXISTS BUT UNUSED` |
| ZD_Cactus | Ranged | — | 70 | 11 | 1.9 | 10.0 | 3 | 3 | no | `EXISTS BUT UNUSED` |
| ZD_SkeletonMage | Ranged | — | 80 | 14 | 2.6 | 12.0 | 4 | 4 | **elite** | `EXISTS BUT UNUSED` |
| ZD_Burrow | Burrower | — | 65 | 12 | 2.8 | 1.6 | 3 | 3 | no | `EXISTS BUT UNUSED` |
| ZD_MoleRat | Burrower | — | 75 | 13 | 3.0 | 1.6 | 3 | 3 | no | `EXISTS BUT UNUSED` |
| ZD_SkeletonGiant | Heavy | **yes** | 900 | 26 | 2.2 | 2.8 | 25 | 30 | **BOSS** | `EXISTS BUT UNUSED` |
| ZD_MoleRatKing | Boss | **yes** | 1100 | 24 | 3.2 | 2.2 | 30 | 35 | **BOSS** | `EXISTS BUT UNUSED` |
| ZD_CactusBoss | Boss | **yes** | 1400 | 30 | 2.6 | 3.2 | 35 | 40 | **BOSS** | `EXISTS BUT UNUSED` |

**11 of 16 are unused in production today.** Boss Beacon needs no new enemy art.
**Gap:** `Burrower` archetypes have burrow clips authored but no encounter that uses the telegraph;
`Ranged` archetypes are absent from `WD_Level1` entirely.

### A4 — Pickups, breakables, interactives, containers

Covered in full in §12, §13 and §14. Existing runtime objects:

| Object | Implementation | Status |
|---|---|---|
| `pickup_coin` | `Pickup` · `PickupEffect.Currency(Coin)` | `EXISTS AND RUNS` |
| `pickup_gem` | `Pickup` · `PickupEffect.Currency(Gem)` | `EXISTS AND RUNS` |
| `pickup_health` | `Pickup` · `PickupEffect.Health` heal 25 | `EXISTS AND RUNS` |
| `pickup_bomb` | `Pickup` · `PickupEffect.Bomb` → `BombPickedUpEvent` | **`CUT`** — art reused |
| `PROP_Crate_Loot` / `_Small` | `DestructibleProp.LootCrate` | `EXISTS AND RUNS` |
| `PROP_Barrel_Fuel` / `_B` | `DestructibleProp.ExplosiveBarrel` | `EXISTS AND RUNS` |
| `Container1–4` (Military Base) | prop only | `REUSE CANDIDATE` — station bodies, colour-coded |
| `Containers_Crate_Large` / `Box_Large` / `Pile_Large` (KayKit) | prop only | `REUSE CANDIDATE` — chest tiers |
| `Copper_Bars_Stack_Large` | prop only | `REUSE CANDIDATE` — Mythic Cache visual |
| `Lamp1` | prop only | `REUSE CANDIDATE` — Relay pole, **needs a beam VFX to read** |
| `Toilet` | prop only | **`CUT`** — off-tone |
| `BombThrower` / `Bomb` / HUD bomb button | runtime | **`CUT`** from active design; code dormant |

### A5 — Economy and collection

| Item | Where | Status |
|---|---|---|
| Coin / Gold / Gem wallet | `PlayerProfile.CurrencyKind` | `EXISTS AND RUNS` |
| Weapon ownership + purchase | `PlayerProfile`, `WeaponData.price` | `EXISTS AND RUNS` |
| Weapon shard + star upgrade | `EconomyConfig.weaponStar2/3*` | `EXISTS BUT UNUSED` → **IN CODE, NO DESIGN AUTHORITY** (W6) |
| Gacha pool + pity | `EconomyConfig.weaponPool` | `EXISTS BUT UNUSED` → **IN CODE, NO DESIGN AUTHORITY** (W6) |
| Costume ownership / purchase | `EconomyConfig.costumeItems` | `EXISTS AND RUNS` |
| Costume sets (30 authored) | `EconomyConfig.costumeSets`, `UI/Icons/Generated/CasualSets` | `EXISTS AND RUNS` |
| Unseen / new badges | `PlayerProfile.unseenItems` | `EXISTS AND RUNS` |
| Battle pass / missions | `MissionTracker` | `EXISTS BUT UNUSED` in M6 |
| Relic Fragment + Archive | — | `PROPOSED` — design **OWNER-LOCKED (W5)**, nothing implemented |
| Blueprint wallet + shop unlock path | — | `PROPOSED` — design **OWNER-LOCKED (W6)**, nothing implemented |

### A6 — Costume inventory (453 entries, all accounted for)

| Slot | UI name | Required | Allows none | Parts | Audit status |
|---|---|---|---|---:|---|
| Eye | Eyes | yes | no | 12 | rendered · neutral, no conflicts |
| Brow | Brows | yes | no | 23 | rendered · neutral |
| Mouth | Mouth | yes | no | 11 | rendered · neutral |
| Hair | Hair | yes | no | 28 | rendered · **4 volume-conflict candidates** |
| Beard | Beard | no | yes | 29 | rendered · **needs `bodyCompatibility`** |
| Mask | Mask | no | yes | 5 | rendered · conflicts with Beard |
| HairAccessory | Hair Accessory | no | yes | 3 | rendered · **highest defect rate in H0** |
| Head | Hat | no | yes | 63 | rendered · **4 occlusion classes assigned** |
| Eyewear | Glasses | no | yes | 18 | rendered · conflicts with faceGuard/hood |
| Earring | Earring | no | yes | 20 | rendered · conflicts with closed head |
| Chest | Top | yes | no | 71 | rendered · 6 style families |
| Hands | Gloves | no | yes | 22 | rendered · never an anchor (max mass 381) |
| Bracelet | Bracelet | no | yes | 5 | rendered · conflicts with Watch |
| HandAccessory | Hand Accessory | no | yes | 10 | rendered · held props, near-colourless |
| Watch | Watch | no | yes | 5 | rendered · conflicts with Bracelet |
| Back | Backpack | no | yes | 18 | rendered · anchor competitor |
| Legs | Pants | yes | no | 55 | rendered · largest pixel mass |
| Feet | Shoes | no | yes (default) | 50 | rendered · neutral |
| *Face* | technical | — | — | 1 | renderer infrastructure |
| *Body* | technical | — | — | 4 | auto-selected by glove/shoe occupancy |
| **TOTAL** | | | | **453** | **all rendered and inspected** |

Per-item renders: `Review/M6_Conceive/Evidence/OutfitSourceSheets/parts/<Slot>/`.
Per-item measured metadata: `.../part_metadata_raw.json`.

### A7 — Icon library (32, currently unused by any perk)

22 skill icons + 10 potion icons — `Evidence/SkillIconSheets/SKILL_ICONS.png`.
Coherent clusters: **movement/evasion** (acrobat, runner, runningfist, runningstrike, highkick) ·
**impact/control** (fist, lowkick, powerstrike, fighter, sturdy) · **execution** (backstab, punisher,
knifemastery, pistol) · **rhythm/sustain** (reload, packaging, machine, repair) · **ambiguous**
(armyman, alchemy, beast, revive) · **potions** (10, consumable framing).

Proposed M6 assignments: Run & Gun → `runner` · Quickstep Shot → `runningstrike` ·
Static Build-up → `machine` · Bullet Hose → `machine`/`packaging` · Point Blank → `fist` ·
Concussion → `powerstrike` · Chain Lightning → `energetic` · Ordnance Core → `alchemy` ·
Soul Burst → `rage_potion` · Execution Round → `punisher`.

---

## 28. Evidence index

| Sheet | File | Reviewed |
|---|---|---|
| Weapons_All | `Evidence/Weapons_All.png` | ✔ |
| Weapons_PrototypeCandidates | `Evidence/Weapons_PrototypeCandidates.png` | ✔ |
| Enemies_All | `Evidence/Enemies_All.png` | ✔ |
| BossBeacon_Candidates | `Evidence/BossBeacon_Candidates.png` | ✔ |
| MapInteractive_PropCandidates | `Evidence/MapInteractive_PropCandidates.png` | ✔ |
| Pickup_IconCandidates | `Evidence/Pickup_IconCandidates.png` | ✔ |
| Skill_IconCandidates | `Evidence/SkillIconSheets/SKILL_ICONS.png` | ✔ |
| Costume_BySlot_* (20 sheets) | `Evidence/OutfitSourceSheets/SLOT_*.png` | ✔ |
| Costume_StyleFamilies | `Evidence/Costume_StyleFamilies.png` | ✔ |
| Costume_ColorFamilies | `Evidence/Costume_ColorFamilies.png` | ✔ |
| Costume_SilhouetteConflicts | `Evidence/Costume_SilhouetteConflicts.png` | ✔ |
| Outfit_GoodBadExamples | `Evidence/Outfit_GoodBadExamples.png` | ✔ |

## 29. Design state

```text
DESIGN STATE — Zombie War — M6 v2 (OWNER-LOCKED 2026-08-15; W1–W7 answered)

Genre/platform/team : top-down endless action roguelite; WebGL/mobile; small team
Core loop           : choose 1 gun → move/auto-fire → level-up build → chase signals
                      → risk/reward boss → die or end → unlock/collect → repeat
Progression axes    : in-run build · weapon variety · cosmetic collection · Relic collection
Currencies          : Coin (spend now) · Gem (rare cosmetics) · Blueprint (weapon unlocks, NEW)
                      Relic (collection) · Gold/Shard/star/Gacha IN CODE, NO DESIGN AUTHORITY
Owner-locked        : one endless world - one weapon per run - auto-fire - no reload -
                      manual grenade retired - 1-of-3 level-up - 30s pause + auto-pick -
                      Maps 2-5 retired - dedicated metal cut -
                      settlement = death 25% / abandon 0% -
                      W1 six families, variants inside families, no fixed weapon count -
                      W2 all 23 cards in scope, max one stat card per offer -
                      W3 universal onboarding gate + measured tier ladder -
                      W4 Relay/Cache/Beacon in that order, prop-independent signal language -
                      W5 Relic collection-only, 2D/billboard art -
                      W6 Coin+Gem+Blueprint+Relic; legacy economy loses design authority -
                      W7 H2 approved for production, 0% hard conflicts mandatory
Locked frame,       : the 8 gate checks G1-G8 - the 9 shared card primitives -
still unproven        6 map objects - Blueprint cost shape - every individual card
Dang mo             : all tuning numbers in section 25 - Relic reward catalog -
                      costume metadata authoring (453 items) - World Signal Language art -
                      G5 grip validation has NEVER been run on any weapon
Đã cắt              : campaign 2–5 · extraction · five-wave Victory · manual grenade ·
                      3-weapon switching · wave-clear auto-collect · dedicated metal
```

---

## 30. Visual audit observations — MEASURED / INFERENCE

Every sheet below was **opened and inspected**. These are observations, not filename inference.

### 29.1 Weapons (`Weapons_All.png`)

- **Silhouette:** only 4 of 25 are individually recognisable — `LMG_Generic` (bipod + olive box
  magazine, the strongest silhouette in the project), `Shotgun_AA12` (drum magazine, boxy),
  `Marksman_SniperGeneric` (long thin barrel), `Shotgun_DoubleBarrel` (break action).
- **The 10 sidearms are one weapon.** Same profile, same size, same dark grey. Only `FiveSeven` (tan),
  `USP45` (tan), `Makarov` (white grip) and `DesertEagle` (revolver) differ at all.
- **The 6 assault rifles are likewise near-identical**; only `AK47` (orange wood) and `SCARL` (tan)
  carry colour.
- **Palette:** overwhelmingly desaturated dark grey. Against this game's brown ground and orange
  enemies, a held gun reads as a dark blob. **Weapon colour is the cheapest identity lever available.**
- **Readability at gameplay scale:** poor for everything except the LMG and AA12 — the weapon is small
  and top-down. This is precisely why weapon identity must come from *behaviour cards*, not from art.
- **Toon compatibility:** good. Flat shading, no PBR detail, consistent with the environment.

### 29.2 Enemies (`Enemies_All.png`, `BossBeacon_Candidates.png`)

- **Palette codes role almost for free:** green = Ranged (cactus family), orange/red = dog Walkers and
  Runners, black/purple = cat Runners, brown = Burrowers, grey/white = skeletons.
- **Boss silhouettes are genuinely distinct** from their small versions: `CactusBoss` is spikier with a
  huge mouth; `MoleRatKing` is hulking and crowned; `SkeletonGiant` is tall, pale and hunched. **No new
  boss art is needed.**
- **Good elite candidates:** `SkeletonMage` (dark hood, red horns — reads as a caster at a glance),
  `DogBowwow` (larger orange dog), `CatLightning` (yellow bolts).
- **Redundancy:** `CatBolt` and `CatLightning` are visually very similar (dark cat + electric accent).
- **Scale caveat — stated honestly:** each tile is bounds-fitted, so this sheet **cannot** be used to
  compare true world scale. `SkeletonGiant` appears similar in size to `Skeleton` here; that is a
  framing artefact, not a finding.
- **Toon compatibility:** excellent — bright, flat, consistent.

### 29.3 Map-interactive props (`MapInteractive_PropCandidates.png`)

- **Shipping containers (`Container1–4`) are the strongest station bodies:** large, unmistakable
  silhouette, and they ship in **blue / red / teal / orange**. Station type can be colour-coded with
  zero new art — Supply Cache blue, Medical red, Greed orange, Scanner teal.
- `Containers_Crate_Large` (dark with yellow straps) reads as military supply → **Supply Chest**.
- `Containers_Box_Large` (cardboard) reads as an ordinary parcel → **Scrap Crate**.
- `Copper_Bars_Stack_Large` reads as treasure → **Mythic Cache / Boss Chest**.
- **`Lamp1` is too thin to carry a station on its own** — very low visual mass. A Signal Relay built on
  it *requires* a beam VFX, or should use a container plus a beam.
- **`PROP_Barrel_Fuel_B` (blue) does not read as explosive.** The red variant does. Either recolour or
  demote the blue one to non-explosive scenery.
- **`Toilet` is off-tone** for a station and is rejected.
- `Rock_01`, `Cliff_01`, `Cactus_01` are landmark dressing, not station bodies. `Cliff_01` is an
  unusually saturated red-orange and reads as abstract at this scale.

### 29.4 Pickups (`Pickup_IconCandidates.png`)

Four pooled prefabs exist and render clearly: coin, gem, health, bomb. The bomb prefab's art, VFX and
audio are **fully reusable** for `Emergency Detonation` and `Ordnance Core` with no new content.

### 29.5 Costume (20 slot sheets + 3 analysis sheets + good/bad)

- **Head is the risk slot:** 63 items, highest mean chroma (0.62), and four distinct occlusion classes.
- **The green leaf `HairAccessory` is the single most damaging item in the library** — it floats
  through hats in roughly one H0 outfit in six and instantly reads as a bug. It appears in 4 of the 6
  "bad" examples.
- **Style spread is wide:** school uniforms, business suits, sports kit, medieval plate, royal coats,
  monk robes, animal onesies, Santa, pirate, witch, astronaut, firefighter, graduation gowns. Theme
  collision — not clipping — is the dominant coherence risk.
- **Colour is strongly biased** to neutral (62) + orange (45) + blue (45), which makes a
  "1 main + 1 secondary + neutral" palette rule easy to satisfy.
- **`Outfit_GoodBadExamples.png` is the decisive image:** H2 produces recognisable characters (sports
  kid, bear onesie, old fisherman, Santa, pirate); H0 produces stacked goggles-mask-leaf-backpack
  characters that read as bugs.
- **Method limitation found and corrected:** 2D pixel mass measures *coverage*, not volume. It selected
  wide bobs as "big hair" and missed the tall mohawk and twin buns that actually clip through helmets.
  Production must bake silhouette class from **3D mesh bounds**, not render pixels (see §19.6).

---

## 31. M6.1 Weapon Factory — documents and evidence

| Document | Contents |
|---|---|
| `Review/M6_WeaponFactory/REFERENCE_PATTERN_MATRIX.md` | 9 genre patterns; what transfers and what does not |
| `Review/M6_WeaponFactory/Inventory/weapon_inventory.csv` | all 415 prefabs, one row each, full field set |
| `Review/M6_WeaponFactory/Inventory/weapon_inventory_summary.md` | bucket reconciliation, provenance of the 25, grip state |
| `Review/M6_WeaponFactory/VISION_REVIEW.md` | written observations for all 6 weapon sheets |
| `Review/M6_WeaponFactory/WEAPON_FAMILIES_AND_FACTORY.md` | family grammar, schema field marking, Factory pipeline |
| `Review/M6_WeaponFactory/weapon_balance_model.csv` | power budget for all 24 distinct weapons |
| `Review/M6_WeaponFactory/SKILL_CATALOG.md` + `.csv` | all 23 skills, full schema |
| `Review/M6_WeaponFactory/BUILD_SIMULATIONS.md` | 12 simulations, 2 per family |
| `Review/M6_WeaponFactory/LOOP_ECONOMY_AND_MIGRATION.md` | pickups, 6 interactives, endless loop, economy, WebGL, exact-25 |
| `Review/M6_WeaponFactory/Evidence/Sheet_01..06*.png` | weapon contact sheets (all opened and reviewed) |

### 31.1 M6.2 decision lock — documents and data

| Document | Contents |
|---|---|
| `Review/M6_DecisionLock/WEAPON_ONBOARDING_GATE.md` | Delta A — universal gate G1–G8, tier ladder, anti-grind/anti-dominance rules, `WeaponData.tier` migration |
| `Review/M6_DecisionLock/weapon_tier_ladder.csv` | 24 rows: measured `powerBudgetUsed`, band, authored tier, agreement, retier action |
| `Review/M6_DecisionLock/ECONOMY_V2.md` | Delta B — Coin / Gem / Blueprint / Relic, faucets, sinks, death security, UI surface, legacy status |
| `Review/M6_DecisionLock/skill_shared_primitives.csv` | 9 shared primitives → the 20 cards each serves |

## 32. Proposed M7 ladder — **NOT STARTED / NOT AUTHORIZED**

Planned only, so the owner can see what approval would trigger. **Do not begin any step.**

### M7.0 — Weapon Factory foundation — **DELIVERED 2026-08-15**

> **Outcome:** the authoritative `WeaponCatalog` asset exists (25 entries, contract clean); the
> naming collision with the old editor sheet renderer is resolved (`VendorWeaponSheetRenderer`);
> G1–G8 are implemented inside `WeaponRosterMigration` and ran over all 25 weapons; the 16 wrong
> `WeaponData.tier` values are corrected with zero price/ownership change; the duplicate shotgun is
> recorded as a variant with both ids intact; and **the G5 grip camera exists and has been run for
> the first time**.
>
> **G5 result: 11/25 pass. All 11 one-handed weapons pass; all 14 two-handed weapons fail** — the
> authored support grip sits 6–45 cm beyond the character's 33.2 cm arm reach, so the IK solves
> correctly and still cannot arrive. **No pose was fixed in M7.0**; the queue is in
> `Review/M7_0_Factory/G5_GRIP_VALIDATION.md`.
>
> **Deferred, and not claimed as done:** the audio builders, cheat/dev tools, `CombatPower` and
> `GachaService` still folder-scan rather than reading the catalog, so "weapon 26 = one entry" is not
> yet true end-to-end. Scene/UI consumers are out of scope by instruction. Full detail:
> `Review/M7_0_Factory/M7_0_FACTORY_FOUNDATION.md`.

**Original plan:**
- **Objective:** one authoritative `WeaponCatalog` + the classify/generate/validate pipeline.
- **Dependencies:** owner approval of the family list and the catalog design.
- **Systems:** `WeaponCatalog` (new), editor pipeline, validation gate.
- **Deliverables:** catalog asset; pipeline; fail-loud validator; manual-fix queue; **a fixed
  rig-relative grip-validation camera plus a re-shot holding sheet** (the M6.1 sheet framed the
  player's backpack, so visual grip validation is currently `NOT RUN`).
- **Gates:** re-onboarding the existing 24 through the pipeline reproduces them byte-comparably;
  adding a 26th weapon requires one catalog entry and no consumer edits; **every weapon has a
  rig-relative held capture in which the grip hand is actually visible.**
- **Stop:** if grip heuristics fall below ~70 % first-pass success, stop and re-scope.

### M7.1 — Mass weapon onboarding — **DELIVERED 2026-08-15**

> **Outcome:** 29 vendor bodies onboarded (arsenal 25 → 54 `WeaponData`); starved families relieved
> (SMG 1→8, Marksman 1→3, LMG 1→2). All new bodies are `PendingOwnerAuthoring` and unequippable until
> the owner hand-authors grips. Weapon materials are project-owned (G1b closed). The catalog is now the
> roster for the audio builders, dev/cheat tools and `CombatPower`.
>
> **KNOWN DEBT — tier:** **43 of 54 weapons sit at tier `Common`.** The 29 onboarded bodies have no
> authored stats, so `powerBudgetUsed` cannot be measured for them, and inventing a tier would invent a
> balance claim. This is deliberate — but it means the **M7.5 gate `tier == band(powerBudgetUsed)`
> cannot pass** until those stats are authored. Sequence: owner authors grips → stats authored →
> power budget measured → tiers assigned → M7.5 gate becomes checkable.
>
> **Held out of the playable pool pending an owner decision:** `WD_AssaultRifle_ModernW` (20 708 tris)
> and `WD_AssaultRifle_ModernX` (15 900 tris) are ~2× the arsenal envelope and are marked
> `BlockedNeedsDecimation`. `WD_Launcher_ModernG` stays `BlockedNeedsProjectileFireMode`.

**Original plan:**
- **Objective:** bring in the 4 drop-in bodies, then any body that clears G1–G8 — starting with
  `Recon_P` and `SMG_P`, which each lift a single-body family.
- **Dependencies:** M7.0; gate checks G1–G8 implemented (§1c.1).
- **Deliverables:** converted materials; held contact sheets per weapon; variant groups.
- **Gates:** 0 duplicate mesh signatures without a variant group; every weapon passes the held-capture
  review; WebGL poly/material budget unchanged.
- **Stop:** if conversion cannot make a body clear G1/G2, that body is cut — not the gate.

### M7.2 — Skill runtime + approved pool
- **Objective:** the offer system plus the approved subset of the 23.
- **Dependencies:** W2 is approved (§1); M7.1 for family tags. Build the 9 shared primitives, not 20 features.
- **Systems:** offer builder, rank system, trigger hooks, the ~6 new primitives.
- **Gates:** no all-stat offer; no incompatible or max-rank card offered; deterministic seeded offers;
  timeout auto-pick always valid; ≥2 visibly different builds per family in playtest.
- **Stop:** if two runs with the same weapon still feel identical, stop and revisit the family grammar.

### M7.3 — Pickups / interactives / endless pressure
- **Objective:** Signal Relay, Supply Cache, Boss Beacon; remove wave-clear auto-collect; Threat model.
- **Dependencies:** M7.2; World Signal Language authored (§14.9).
- **Gates:** players leave safety to reach a signal; no orphan boss on unload; no free chest;
  stations survive chunk recycling; `CollectAll()` on `WaveClearedEvent` removed.
- **Stop:** if players ignore signals, the world content model has failed — stop before adding stations 4–6.

### M7.4 — Economy / meta / outfit
- **Objective:** in-run Coin sinks; **Blueprint** faucet + shop unlock path; Gem/Blueprint/Relic
  secure-on-pickup; Archive; H2 outfit grammar in production.
- **Dependencies:** W5, W6 and W7 are answered (§1). Costume metadata authoring over 453 items.
- **Gates:** a rare drop survives a death; a locked weapon shows a countable `n / N` Blueprint target;
  H2 measured over ≥300 seeds at **0 hard conflicts (mandatory)** and **≥70 % thematic coherence**.
- **Stop:** if authored metadata reintroduces any hard conflict or drops coherence below 70 %, ship
  H1 hard rules only — do not ship H2 on that metadata.

### M7.5 — Balance / WebGL / ship polish
- **Objective:** apply the power model; enforce every guardrail; profile on device.
- **Gates:** `tier == band(powerBudgetUsed)` for every weapon; Epic ≤ 2× the weakest Common (A2 —
  currently 3.6× and violated); every band holds ≥ 2 families (D3 — currently violated in Rare and
  Epic); G36C no longer strictly dominant; 0 alloc/frame in steady state; arc/explosion/query caps
  held; WebGL build succeeds with 0 errors.
- **Stop:** ship gate.

**M7 STATUS: NOT STARTED / NOT AUTHORIZED.**

## 33. SIMPLIFIER — M6.2 bằng tiếng Việt

**Chủ dự án đã trả lời cả bảy câu hỏi.** Bốn câu duyệt nguyên, ba câu đổi. M6 chốt từ đây.

```text
W1 DUYỆT   6 họ súng, biến thể nằm TRONG họ. Không lấy con số súng nào làm giới hạn thiết kế.
W2 DUYỆT   Đủ 23 card đều nằm trong phạm vi. Tối đa 1 card chỉ số mỗi lượt chọn.
W3 ĐỔI     Bỏ cổng riêng cho MW4. Thay bằng cổng kiểm tra chung cho MỌI khẩu súng,
           cộng thang hạng súng (tier) tính từ số đo sức mạnh thật.
W4 DUYỆT   Ba trạm đầu: Signal Relay → Supply Cache → Boss Beacon, đúng thứ tự đó.
W5 DUYỆT   Relic chỉ để sưu tầm. Dùng art 2D/billboard nên rẻ hơn dự tính.
W6 ĐỔI     Không chỉ đóng băng kinh tế cũ. Làm kinh tế mới: Coin + Gem + Blueprint + Relic.
W7 ĐỔI     H2 ĐƯỢC DUYỆT CHO PRODUCTION. Bắt buộc 0% xung đột cứng; ~70%+ hợp chủ đề là chấp nhận được.
```

**Ba việc mới sinh ra từ ba câu "đổi":**

**Một — cổng nhận súng dùng chung.** Cổng cũ chỉ nói về gói MW4, mà MW4 là một nhà cung cấp chứ không
phải một loại súng. Cổng mới kiểm tra **tính chất của khẩu súng**, nên gói nào sau này thêm vào cũng đi
qua đúng tám bước đó. Năm bước chặn: đúng vật liệu toon, có viền, không trùng model khẩu đã có, đã gắn
điểm cầm và điểm nòng, và **nhìn bằng mắt thấy tay cầm đúng**.

> Nói thẳng một điểm yếu: **bước cuối chưa từng chạy trên bất kỳ khẩu nào**, kể cả 25 khẩu đang ship.
> Ảnh chụp cũ không thấy bàn tay trong khung hình nào cả. Công cụ chụp lại là việc của M7.0.

**Thang hạng súng.** Đo sức mạnh thật của 24 khẩu rồi chia bốn bậc: Common ≤ 0.340 có 13 khẩu, Uncommon
≤ 0.490 có 5 khẩu, Rare ≤ 0.650 có 4 khẩu, Epic trên 0.650 chỉ có 2 khẩu (FAMAS và G36C). Ranh giới đặt
đúng vào chỗ trống giữa hai khẩu nên không khẩu nào bị xếp nhầm vì làm tròn.

> **Phát hiện đáng lo: 16 trong 24 khẩu đang gắn hạng sai so với sức mạnh đo được.** Ví dụ `LMG_Generic`
> ghi là Common nhưng mạnh hơn nhiều khẩu ghi Rare — người chơi sẽ nhận ra ngay một lựa chọn mua là
> đúng và mọi lựa chọn khác là sai.

**Luật cũ "súng mới phải chơi được ngay" có mâu thuẫn với hạng không?** Không. Luật đó sinh ra để giết
hệ nâng sao — thứ làm khẩu vừa mở ra yếu hơn khẩu đã cày. Luật mới tách hai chuyện: **mở khóa là có
ngay 100% sức mạnh thiết kế, không có bản yếu hơn của chính nó**; còn hạng là dải sức mạnh đã cố định
từ lúc làm asset, không phải bậc cày.

**Hai — kinh tế mới, bốn thứ, mỗi thứ một việc.**

```text
Coin       tiêu ngay trong run và ở Hub.       Chết mất 75%, tự bỏ run mất 100%.
Gem        mỹ phẩm hiếm.                        Nhặt là an toàn, chết không mất.
Blueprint  MỞ SÚNG, và chỉ làm mỗi việc đó.     Nhặt là an toàn. MỚI.
Relic      sưu tầm, không bao giờ cộng chỉ số.  Nhặt là an toàn.
```

**Blueprint là đường không-may-rủi.** Nó là tài nguyên chung, không phải "mảnh của khẩu AK". Vì thế
không có đồ trùng, không cần luật đổi đồ trùng, không cần pity, và người chơi luôn tự tính được còn
thiếu bao nhiêu. Súng chỉ tốn Blueprint, còn Coin mua mọi thứ khác — hai bên không giành nhau một quyết
định, nên tiêu Coin giữa run luôn là việc an toàn.

**Gold, Weapon Shard, nâng sao, gacha:** vẫn nằm nguyên trong code, **nhưng không còn quyền quyết định
thiết kế**. Đây là chuyện đã chốt, không phải đề xuất đang chờ duyệt. Không xóa dòng code nào.

**Ba — outfit H2 được duyệt cho production.** Đo trên 300 bộ đồ sinh ngẫu nhiên: **0% xung đột cứng**
và **73% hợp chủ đề**. Mốc 85% trước đây là do tôi tự đặt, chủ dự án đã thay bằng cặp tiêu chí khác.

> **Cách nói đúng, không được nói khác:** H2 đạt **0 xung đột cứng trên 300 mẫu**. 27% còn lại là
> **lệch chủ đề, không phải lỗi cấu trúc** — trông như gu ăn mặc lạ, không phải như bug. Không được
> viết "0% thất bại".

**Còn nợ, và duyệt không có nghĩa là xong:** phải gắn metadata thật cho **453 món đồ** rồi đo lại. Nếu
đo lại mà tụt dưới 70% hoặc xuất hiện xung đột cứng thì không ship H2 trên bộ metadata đó, quay về H1.

**Chưa code gì cả.** Đây vẫn là bản thiết kế. **M7 chưa được duyệt và chưa được bắt đầu.**
