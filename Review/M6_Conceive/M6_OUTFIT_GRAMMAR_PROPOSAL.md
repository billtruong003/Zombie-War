# M6.1 — Outfit Grammar Proposal

**Status:** **APPROVED FOR PRODUCTION (W7, 2026-08-15)** — still **not implemented**, and no production
asset has been touched.

> **M6.2 DECISION LOCK.** The owner approved H2 for production under a different pair of criteria than
> the ones proposed here: **0 % hard conflicts is the mandatory gate**, and **~70 %+ random thematic
> coherence is acceptable**. The **85 % PASS gate quoted throughout this document was mine and is
> superseded by owner decision** — it was not lowered to fit the result, it was replaced. The measured
> numbers below (H0 17 % · H1 58 % · H2 73 % · H2 hard conflicts 0 %) are unchanged and remain the
> evidence. Manual Wardrobe remains the path for deliberately curated outfits. Costume metadata
> authoring over 453 items and the post-authoring re-test are still outstanding.
**Backing experiment:** `Evidence/Metrics/outfit_runs.json` (300 outfits), sheets
`Evidence/OutfitCandidates/SHEET_{H0,H1,H2}.png` (30 rendered each).

---

## 1. Experiment result

300 outfits generated (100 per hypothesis) over **identical seed ranges** (1000–1099), scored by one
shared classifier so the three are directly comparable.

| Metric | H0 (production) | H1 (hard rules) | H2 (hard + scored) | Provisional gate |
|---|---:|---:|---:|---:|
| **Overall PASS** | **17 %** | **58 %** | **73 %** | ≥85 % |
| Hard conflicts | **43 %** | **0 %** | **0 %** | 0 % ✓ |
| Over-accessorised | 57 % | 1 % | 7 % | ≤10 % ✓ |
| Excessive face coverage | 32 % | 5 % | 7 % | — |
| Theme mismatch | 24 % | **33 %** | **4 %** | — |
| Palette conflict | 21 % | 11 % | 11 % | — |
| Competing anchors | 0 % | 0 % | 0 % | — |
| Bland | 0 % | 0 % | 1 % | ≤15 % ✓ |
| Missing mesh/material | 0 % | 0 % | 0 % | 0 % ✓ |
| Deterministic replay | 100 % | 100 % | 100 % | 100 % ✓ |
| Mean items worn | **11.8** | 9.2 | 9.6 | — |

### Honest reading

- **H0 is the shipping randomizer**, not a strawman. `CostumeScreen.RandomizeCasual()` picks uniformly
  per slot with a flat 50 % "none" on optional slots. Its 43 % hard-conflict rate and 11.8 items worn
  are the current player experience.
- **H1 eliminates 100 % of hard conflicts** for very little machinery — but *makes theme mismatch
  worse* (24 % → 33 %). Removing conflicts does not create coherence; it just stops the obvious breakage.
- **H2 is the only hypothesis that addresses coherence** (theme mismatch 24 % → 4 %).
- **H2 does NOT meet the provisional 85 % PASS gate.** It reaches 73 %. I am reporting that as a miss,
  not adjusting the model until it passes. The residual failures are palette conflict (11 %),
  over-accessorising (7 %) and face coverage (7 %).

### Vision review vs automatic score

Graded by eye from the three rendered sheets, kept separate from the automatic score:

| | Auto PASS (of 30 shown) | My visual PASS | Agreement |
|---|---:|---:|---|
| H0 | 4 | ~3 | Auto is slightly **generous** — several "PASS" outfits are merely un-flagged, not good |
| H1 | 16 | ~15 | close |
| H2 | 22 | ~21 | close |

**What vision caught that the metric did not:**

1. **Facial hair on feminine/child faces** (H2 seeds 1000, 1019; widespread in H1). Reads as an error,
   is invisible to every metric, and cannot be fixed without a body/gender compatibility field.
2. **The green leaf `HairAccessory`** is the single most damaging item in H0 — it floats through hats
   in roughly one outfit in six and instantly reads as a bug.
3. **H0's real problem is not any single rule** but density: near-every character wears gloves +
   backpack + earrings + goggles + mask + hat simultaneously. The 11.8-items-worn figure is the
   headline defect.

**What the metric caught that vision would have missed:** palette conflicts across Chest/Legs/Feet/Head
are easy to rationalise one outfit at a time but show up clearly at 11 % across 100 samples.

### Two errors found in my own experiment (disclosed)

1. **Off-by-one in the inferred metadata.** Occlusion classes and themes were written using the item
   *number* (`Headgear_26`) but matched against the 0-based *index*, shifting every class by one item.
   After the fix, H0 PASS dropped 22 % → 17 % and H2 73 %. The pre-fix numbers were wrong.
2. **A residual 5 % hard-conflict rate in H2** traced to a real grammar rule, not a coding slip: when
   the chosen hat is `hair_replacing`, *every* Hair candidate is illegal, so a "required" Hair slot
   forces a broken outfit. **Finding: a `hair_replacing` hat must SATISFY and suppress the Hair slot.**
   With that rule, H2 hard conflicts reach 0 %.

---

## 2. Recommendation

> ## **PROMOTE H2** — hard compatibility + scored weighted random.

**Why not H1:** it fixes breakage but not incoherence. 33 % theme mismatch is worse than the
production randomizer, and theme collision is exactly what makes a generated outfit look machine-made
rather than designed. H1 is the *floor*, and should ship as part of H2, not instead of it.

**Why not "cut procedural outfits":** the library carries 453 hand-made parts and 30 authored sets. A
manual-only wardrobe wastes the combinatorial value the asset spend already bought, and the
`Randomize` button already exists in the shipping UI.

**Why not REWORK:** H2's shortfall is concentrated in the *inferred* metadata layer, which is exactly
what real authoring would replace. The architecture is sound; the data is provisional. Reworking the
architecture would be solving the wrong problem.

**Condition on the recommendation:** H2 is promoted **with the 85 % gate unmet at 73 %**. I recommend
accepting 73 % as the prototype result and re-testing after real metadata is authored, rather than
tuning weights to manufacture 85 %. Expected recoverable margin once theme/palette/set fields are
authored rather than inferred: I estimate **80–88 %** — ASSUMPTION, not a measurement.

---

## 3. Proposed design (not implemented)

### 3.1 Metadata schema — per catalog part

```text
slot                string    (existing)
themes              flags     casual | formal | athletic | fantasy | costume | military
paletteFamily       enum      neutral | warm | cool | earth | vivid
dominantColour      enum      red orange yellow green cyan blue purple pink white grey black
accentColour        enum      (nullable)
silhouetteClass     enum      slim | regular | bulky
visualWeight        float     0..2, normalised within slot        (can be BAKED from renders)
accentLevel         enum      none | subtle | strong
faceCoverage        enum      none | partial | full
headOcclusion       enum      open | hairReplacing | fullHood | closedHelmet   (Head slot only)
anchorEligible      bool      may act as the outfit's focal piece
setId               string    (nullable) — 30 authored sets already exist
pairTags            string[]  soft affinity
hardConflictTags    string[]  symmetric, evaluated as a set
bodyCompatibility   flags     any | masculineOnly | feminineOnly     ← NEW, required for Beard
clippingRisk        enum      none | low | high
qualityStatus       enum      approved | review | excluded
```

`visualWeight`, `dominantColour`, `accentColour` and `clippingRisk` can be **baked automatically** from
the render pipeline already built in this phase — that is the cheap half. `themes`, `setId`,
`bodyCompatibility`, `anchorEligible` and `headOcclusion` need a designer pass — that is the real cost.

### 3.2 Hard compatibility representation

Symmetric tag pairs, evaluated as a set operation, **not** an N² matrix:

```text
conflict(hairReplacing, slot:Hair)
conflict(fullHood|closedHelmet, tag:bigHair)
conflict(fullHood|closedHelmet, slot:Earring)
conflict(fullHood|closedHelmet, slot:HairAccessory)
conflict(faceCoverage:full, slot:Eyewear | slot:Mask | slot:Beard)
conflict(slot:Mask, slot:Beard)
conflict(slot:Bracelet, slot:Watch)
satisfies(headOcclusion:hairReplacing, slot:Hair)     ← suppression, not conflict
```

With 18 slots this is ~8 rules, not 453² relationships.

### 3.3 Scoring model (starting point, to be re-tuned on authored data)

```text
Score = 3.0·ThemeMatch + 2.5·PaletteHarmony + 2.0·PairCompatibility
      + 1.5·SilhouetteBalance + 1.0·AccentBalance + 0.5·ControlledVariety
      − 100·HardConflict − 4.0·ExcessiveFaceCoverage
      − 3.0·CompetingAnchorPieces − 2.0·ExcessiveVisualWeight

P(candidate) = softmax(Score / T),  T = 1.1
```

Hard conflicts are removed **before** softmax (weight 0), so `−100` is a safety net, not the mechanism.

### 3.4 Generation order (validated in the experiment)

```text
technical base (Face + Body_1..4 from glove/shoe occupancy)
   ↓  Chest ──► Legs                     establish theme + palette anchor
   ↓  Head                               resolve occlusion class FIRST
   ↓  Hair                               (suppressed if the hat replaces it)
   ↓  Feet ──► Hands ──► Back
   ↓  Eye / Brow / Mouth                 required face features
   ↓  Beard / Mask / Eyewear             face layer, at most one
   ↓  HairAccessory / Earring
   ↓  Bracelet / Watch / HandAccessory   jewellery last, low probability
```

**Head must be resolved before Hair.** Resolving Hair first makes the hair-suppression rule
unexpressible and forces the conflict the whole grammar exists to prevent.

### 3.5 Empty-slot probabilities (experiment values)

```text
Feet 0.10 · Hands 0.62 · Beard 0.65 · Back 0.70 · Eyewear 0.72
Earring 0.80 · Bracelet 0.85 · Watch 0.85 · HairAccessory 0.85 · Mask 0.88 · HandAccessory 0.88
Head 0.45
```

Yields ~9.6 items worn vs the production randomizer's 11.8.

### 3.6 Determinism, duplicates, fallback

- **Deterministic:** one seed → one outfit. Verified 100 % across all three hypotheses.
- **Duplicate suppression:** keep a short ring buffer of the last N anchor pieces (Head/Chest) per
  player and down-weight repeats. Not implemented or measured in this phase.
- **Fallback:** if a required slot has no legal candidate, leave it empty **only if** an occlusion rule
  satisfies it; otherwise widen the candidate sample before ever emitting an illegal outfit. The
  experiment showed an unchecked fallback silently re-introduces the exact conflicts being prevented.
- **Ownership:** the production randomizer filters to owned items. The grammar must apply hard rules
  **after** the ownership filter, and must degrade gracefully when a player owns very few items.

### 3.7 Validation tooling

The renderer built for this phase is directly reusable: render N seeded outfits, score them, emit a
contact sheet plus a metrics table. That gives a repeatable regression gate whenever assets change.

---

## 4. Production cost

| Work | Cost | Notes |
|---|---|---|
| Data authoring (453 parts × ~6 designer fields) | **MEDIUM** | 4 of ~14 fields bake automatically from renders; themes/sets/body-compat need a human pass. ~1–2 designer-days. |
| Editor tooling (bake + inspector + validation) | **LOW–MEDIUM** | The render/bake half already exists as scratch code from this phase. |
| Runtime selection | **LOW** | ~8 hard rules + weighted sample over ≤10 candidates per slot; trivial cost, no per-frame work. |
| Automated validation | **LOW** | Reuse the harness; assert 0 hard conflicts over N seeds in CI. |
| Visual QA | **MEDIUM** | Human review of contact sheets each time the wardrobe grows. |
| Maintenance per new asset | **LOW** | One metadata row + rerun the bake. |
| **Total** | **MEDIUM** | Dominated by one-off metadata authoring, not by engineering. |

---

## 5. Open questions for the owner

1. **Body/gender compatibility** — do we add the field (needed to stop beards on feminine faces), or
   accept it as stylistic? This is the only finding that cannot be fixed by tuning.
2. **Are the 30 authored sets canonical?** If yes, "roll a complete set" should be an explicit
   high-probability branch, which would raise theme coherence far more cheaply than scoring.
3. **What is the randomizer actually for?** A cosmetic toy button, a daily-reward outfit generator, or
   NPC/crowd variety? Each implies a different quality bar. I assumed "player-facing cosmetic toy".
4. **Is 73 % acceptable for a prototype gate**, or should authoring proceed until 85 % is demonstrated
   before promotion?
