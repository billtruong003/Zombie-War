# M6.1 — Outfit Asset Vision Audit

**Phase:** CONCEIVE. Nothing was written into `CasualCostumeCatalog.asset` or any production asset.
**Method:** all 453 catalog parts were **rendered** on a fixed character (fixed pose, camera, light,
background, scale, real project-owned M5+ toon materials) and inspected visually. Filenames were not
trusted.

**Evidence:**
`Evidence/OutfitSourceSheets/SLOT_*.png` (20 sheets, 453 tiles) ·
`Evidence/OutfitSourceSheets/parts/**` (453 individual renders) ·
`Evidence/OutfitSourceSheets/part_metadata_raw.json` ·
`Evidence/Metrics/slot_metadata_summary.md`

---

## 1. Verified catalog scale

| Measure | Reported previously | **Verified now** |
|---|---:|---:|
| Catalog entries | ~453 | **453** ✓ |
| Material slots | ~517 | **517** ✓ (from M5+ contract tests) |
| Player-facing slot definitions | — | **18** |
| Technical (renderer infrastructure) slots | — | **2** (`Face`, `Body`) |
| Distinct shared materials after M5+ | — | **3** (Toon / ToonAlt / Glass) |

## 2. Real slot taxonomy — differs from the brief's suggested list

The brief suggested "body, face, hair, headgear, eyewear, mask, facial hair, chest, legs, hands,
shoes, back, earrings, bracelets, watches". The actual catalog is:

| Slot id | UI name | Group | Required | Allows "none" | Parts |
|---|---|---|---|---|---:|
| Eye | Eyes | Head | **yes** | no | 12 |
| Brow | Brows | Head | **yes** | no | 23 |
| Mouth | Mouth | Head | **yes** | no | 11 |
| Hair | Hair | Head | **yes** | no | 28 |
| Beard | Beard | Head | no | yes | 29 |
| Mask | Mask | Head | no | yes | 5 |
| HairAccessory | Hair Accessory | Head | no | yes | 3 |
| Head | Hat | Head | no | yes | **63** |
| Eyewear | Glasses | Head | no | yes | 18 |
| Earring | Earring | Head | no | yes | 20 |
| Chest | Top | Body | **yes** | no | **71** |
| Hands | Gloves | Body | no | yes | 22 |
| Bracelet | Bracelet | Body | no | yes | 5 |
| HandAccessory | Hand Accessory | Body | no | yes | 10 |
| Watch | Watch | Body | no | yes | 5 |
| Back | Backpack | Body | no | yes | 18 |
| Legs | Pants | Legs | **yes** | no | **55** |
| Feet | Shoes | Legs | no | yes (has default) | 50 |
| *Face* | — | technical | — | — | 1 |
| *Body* | — | technical | — | — | 4 |

**Differences worth reporting:**

- **Eye / Brow / Mouth are equippable slots**, not part of a fixed face. That is 12 × 23 × 11 = 3 036
  face permutations before any garment — and they are *required*, so they are always chosen.
- There is **no separate "face" slot**; `Face` is one technical mesh, and `Body_1..4` are chosen
  automatically from glove/shoe occupancy (renderer infrastructure, never player-facing).
- **Shoes are `allowNone = true` but carry a default** — barefoot is legal.
- **10 of 18 player slots are Head-group.** The head is where all the conflict risk lives.

## 3. Measured metadata (FACT — derived from the renders, not guessed)

Per-slot palette and pixel-mass summary: `Evidence/Metrics/slot_metadata_summary.md`.

Extracted per part: dominant / secondary colour family, palette proportions, chroma (share of
non-neutral pixels), garment pixel mass (excluding skin), bounding box. Visual weight is the pixel
mass normalised against that slot's median; `bulky` = ≥1.6× median.

Notable measured facts:

- **Head is the most saturated slot** (mean chroma 0.62) and the second largest population (63).
  It is simultaneously the biggest colour risk and the biggest silhouette risk.
- **Legs has the largest pixel mass** (median 2 615) — trousers dominate the silhouette more than tops.
- **Hands is tiny and uniform** (median 360, max 381) — gloves can never be an anchor piece.
- **Bracelet mass ranges 443 → 1 742 (3.9×)**: `Bracelet_2` is a completely different scale of object
  from the rest of its slot. Slot membership does not imply comparable visual weight.
- **HandAccessory is large (median 1 703) and almost colourless** (chroma 0.29) — these are held
  props (tools/phones), not jewellery, despite the "accessory" name.

## 4. Inferred metadata (ASSUMPTION — from visual inspection, not production data)

Assigned by reading the contact sheets directly. **This is the layer that would need real authoring**
if the grammar is promoted.

### 4.1 Head occlusion classes — the single most important inference

| Class | Behaviour | Items (by number) |
|---|---|---|
| `open` | cap/hat, hair remains visible | the remaining ~32 |
| `hair_replacing` | **hat ships its own hair mesh** | 5, 25, 49, 50 |
| `full_hood` | animal/character hood, covers the whole head | 18, 19, 20, 24, 26, 32, 33, 34, 35, 55, 58, 59, 60 |
| `closed_helmet` | rigid helmet, covers cranium | 36, 37, 38, 39, 43, 44, 45, 51, 52, 53, 54, 61, 62, 63 |
| `face_guard` (overlaps above) | also covers the face | 26, 38, 45, 54 |

### 4.2 Theme families

`casual` (default) · `formal` · `athletic` · `fantasy` · `costume` · `military`.

From the sheets, the wardrobe spans **school uniforms, business suits, sports jerseys, medieval plate
armour, royal coats, monk robes, animal onesies, Santa, pirate, witch, astronaut, firefighter and
graduation gowns**. Casual streetwear is the plurality, but the costume/fantasy tail is large enough
that uniform random selection reliably produces theme collisions.

> **This is the dominant coherence risk in the library — larger than clipping.**

### 4.3 Hair volume

Measured, then classed: `big_hair` = ≥1.35× the Hair-slot median mass. Captures the long blonde,
tall mohawk, twin buns and large curly styles that physically cannot fit inside a closed helmet.

---

## 5. Compatibility rules — audited against the renders

### 5.1 Hard conflicts (deterministic, must never ship)

| Rule | Evidence |
|---|---|
| `hair_replacing` hat **+** Hair | hat already carries hair; two hair meshes interpenetrate |
| `full_hood`/`closed_helmet` **+** `big_hair` | large hair punches through the shell |
| `full_hood`/`closed_helmet` **+** Earring | earrings render inside the shell |
| `full_hood`/`closed_helmet` **+** HairAccessory | observed repeatedly in H0: green leaf accessory floating through hats |
| `face_guard` **+** Eyewear | glasses inside a visor |
| `face_guard` **+** Mask | two face layers |
| `face_guard` **+** Beard | beard through a rigid faceplate |
| `full_hood` **+** Eyewear | glasses under a hood shell |
| Mask **+** Beard | observed in H0: moustaches protruding through masks |
| Bracelet **+** Watch | both anchor to the same wrist |

**A hard conflict is not a scoring penalty. It is an illegal combination and must be unreachable.**

### 5.2 Soft rules (scored, not forbidden)

- **Face-coverage stacking** — at most one of {`face_guard`/`full_hood`, Mask, Eyewear}.
- **Accessory count** — Bracelet, Watch, HandAccessory, Earring, HairAccessory, Back, Eyewear, Mask
  collectively ≤ 3.
- **Competing anchors** — at most one `bulky` piece among Head / Chest / Back.
- **Theme spread** — at most one non-casual theme per outfit.
- **Palette** — at most three distinct saturated hue families across worn garments.
- **Empty slots are correct.** A plausible outfit routinely leaves 6–9 of the 12 optional slots empty.
  Nothing should ever reward "wearing the maximum number of items".

### 5.3 Rules the brief asked about — findings

| Asked | Finding |
|---|---|
| closed helmet vs hair | **Real.** Needs `big_hair` volume class, not a blanket ban — short hair fits fine under most helmets. |
| helmet vs eyewear | **Real, but only for `face_guard`/`full_hood`.** Open caps combine with glasses perfectly well. |
| helmet vs earrings | **Real** for hood/helmet. |
| mask vs beard | **Real and highly visible** — moustache-through-mask is the most obvious H0 artefact. |
| large hair vs headgear | **Real**, volume-dependent. |
| chest vs legs | **No hard conflict found.** Tops and trousers do not intersect on this rig. It is a *theme/palette* relationship, not a clipping one. |
| chest vs back item | **No clipping conflict found**, but both compete as bulky anchors. |
| bulky sleeves vs bracelet/watch | **Not observed.** Gloves are small (max mass 381) and did not intersect wrist items in any render. |
| face coverage stacking | **Real** — soft rule. |
| multiple oversized pieces | **Real** — soft rule. |
| multiple saturated accents | **Real** — soft rule; Head and Chest are the main offenders. |
| items only working as part of a set | **Real.** 30 authored set icons exist (`UI/Icons/Generated/CasualSets`, 30 files) — e.g. the knight, pirate, santa and animal-onesie pieces are clearly designed as sets. |
| slots that should often stay empty | **Yes** — HairAccessory (3 items), Mask (5), Watch (5), Bracelet (5), HandAccessory (10) should be rare, not 50/50. |
| **gender/body dependency** | **Real, and currently unmodelled.** Beard/Mustache (29 items) are applied to visibly feminine and child-read faces by both the production randomizer and H1/H2. Observed in H2 seeds 1000 and 1019. There is **no gender or body-type field in the catalog**, so this cannot currently be expressed. |

---

## 6. Quality status of the library

**The asset library itself is in good shape.** Across 453 renders:

- 0 missing meshes, 0 missing materials, 0 pink/error materials.
- All parts resolve to the 3 approved M5+ materials.
- Silhouettes are clean and read at player scale.
- The art is internally consistent in style; the incoherence produced by randomisation is a
  **selection** failure, not an asset failure.

The one genuine authoring gap is **metadata**: the catalog stores mesh, bones, icon and material, but
carries **no theme, palette, silhouette, occlusion, set-membership or body-compatibility fields**.
Every rule in §5 currently has nowhere to live.
