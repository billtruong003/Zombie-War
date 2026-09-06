# M5+.2 — character shader production integration (2026-08-14)

## Codex decision applied

H1 approved · H0 rejected · H2 rejected. Work continued from the existing H1 prototype; no new look
was invented and H1 was not softened toward H2.

## The legs question — CLOSED with evidence

Live renderer inventory of the captured outfit (Hub, runtime clone):

```text
active: Costume_Earring, Costume_Bracelet, Costume_Eye, Costume_Mask, Costume_Hair, Costume_Mouth,
        Costume_Face, Costume_Brow, Costume_Hands, Costume_Body, Costume_Legs, Costume_HairAccessory,
        Costume_Beard, Costume_Chest, Costume_HandAccessory, Costume_Back
inactive: Mouth_White_1, Body_White_Head_1, Body_White_1, Eye_Black_1, Brow_White_1

Costume_Feet : NOT PRESENT in the outfit at all
```

Confirms Codex exactly: the warm legs/feet are **bare atlas skin**, not a shader tint defect. H0 had
washed them toward white via its SH/GI term. They were not recoloured.

## ShadowCaster contract — resolved by measurement, not assumption

The prototype shipped a pass named `ShadowCaster` that used a plain `TransformObjectToHClip` with no
`ApplyShadowBias`. Rather than "fix" it, the project was measured:

```text
Mobile_RPAsset : supportsMainLightShadows = False
                 shadowDistance = 0
                 supportsAdditionalLightShadows = False
Player.prefab  : all 5 renderers -> shadowCastingMode = Off, receiveShadows = False
```

The project uses **no character shadow maps anywhere** — grounding is the batched contact-shadow mesh
from M5. So the correct action was to **omit ShadowCaster entirely**, not to implement a biased one. A
pass named `ShadowCaster` that silently produces acne if shadows are ever enabled is worse than no
pass.

For the same reason `GetMainLight()` is used **without shadow coordinates**: there is no shadow map to
receive. This is documented in the shader header so it is not mistaken for an unfinished
MinionsArt-style implementation.

`supportsCameraDepthTexture = True`, so **DepthOnly is genuinely required** and is kept. Final passes:

```text
ZombieWar/Character/Toon   : ForwardLit + DepthOnly   (2 passes, 0 shader messages)
ZombieWar/Character/Metal  : ForwardLit + DepthOnly   (2 passes, 0 shader messages)
```

## Production assets created

```text
Assets/_Project/Shaders/Character/CharacterToon.shader    ZombieWar/Character/Toon
Assets/_Project/Shaders/Character/CharacterMetal.shader   ZombieWar/Character/Metal
Assets/_Project/Art/Materials/Character/M_Character_Toon.mat      (H1 values baked)
Assets/_Project/Art/Materials/Character/M_Character_ToonAlt.mat   (H1 values baked)
Assets/_Project/Art/Materials/Character/M_Character_Metal.mat     (H1 values baked)
Assets/_Project/Art/Materials/Character/M_Character_Glass.mat     (glass RETAINED)
```

**Glass was retained, not redesigned.** `ColorB` uses `StylizedToonWorldKit/Surface/Glass`, queue 3000.
No visual defect was demonstrated, so a project-owned material was created against the same shader with
its properties copied, preserving the intended transparency. Changing glass architecture without
evidence was explicitly out of scope.

## Hub result (vision-inspected)

`Hub/Hub_H1_fullUI.png` — real full Game View, yellow character card visible, portrait aspect.

Live assertion at capture: `_ToonLightDirection = (0,0,0,0)` — the Hub exercises the **URP main-light
fallback** path, gameplay exercises the rig path.

Reviewed with vision against the yellow card:

- white hair **holds its planes** — cool lavender under-planes against a brighter top; it does not
  flatten and does not lose contrast against the warm yellow background
- face stays the focal read; dark brows/eyes anchor it
- shirt keeps a clean graphic division
- gauntlets read as metal with a visible band, not grey putty
- no chrome, no white-ceramic metal, no neon rim halo

## Vendor and production integrity

Hashes identical before and after this phase (not timestamps — actual `git hash-object`):

```text
ColorA 58cb767c… ColorB 0d220f3a… ColorC 76c07744… ColorD 010c82e8…   unchanged
CasualCostumeCatalog 75ed6f21…  Player.prefab b6a6bad7…              unchanged
Menu.unity 0d2a074a…  Map_Level1.unity 2a827019…                     unchanged
staged: 0
```

All material swaps were applied to the **runtime clone only** and reverted before exiting Play Mode.

## NOT DONE — remaining gates

- **Step 6 catalog migration** — `CasualCatalogGenerator` / `PrepareProMaterials` were not modified.
  The 453 catalog parts still reference vendor ColorA/B/C/D. **The game does not yet use the new
  materials**; they exist and are proven in-context, but are not wired into the authoring path.
- Step 1 gameplay captures, Step 8 outline verification (MaskOnly / EdgeOnly / composite)
- Step 7 wardrobe vision matrix (11 representatives)
- Step 9 rendering/allocation before-after comparison
- Step 10 tests
- Step 11 WebGL 2 build

Because the catalog migration is the substance of "production integration", **M5+ is INCOMPLETE**.

---

# M5+.3 — production migration (2026-08-14)

## The actual defect in the authoring path

`CasualCatalogGenerator.PrepareProMaterials()` copied vendor `ColorA/B/C/D` into a **second ThirdParty
folder** (`3D Characters Pro-Casual/Materials`) and remapped the FBX importer to those copies. So the
"production" materials lived inside vendor space, and every catalog rebuild pulled the vendor toon
shader back — which would have silently erased the approved M5+ look on the next bake.

**The FBX importer remap is the single point of control.** `Generate()` reads `smr.sharedMaterials`
straight off the FBX, so fixing the remap makes all 453 parts follow. No entry was hand-patched.

New mapping (`RoleToProjectMaterial`, the only place it is defined):

```text
FBX ColorA -> Assets/_Project/Art/Materials/Character/M_Character_Toon.mat
FBX ColorB -> Assets/_Project/Art/Materials/Character/M_Character_Glass.mat
FBX ColorC -> Assets/_Project/Art/Materials/Character/M_Character_Metal.mat
FBX ColorD -> Assets/_Project/Art/Materials/Character/M_Character_ToonAlt.mat
```

Missing material now **fails loudly and aborts** rather than falling back to vendor. Remap writes only
when the value differs, which is what makes a second run produce no drift.

## Catalog migration result

```text
parts                    : 453 / 453
null meshes              : 0      null bone arrays : 0      null root bones : 0
ThirdParty/vendor refs   : 0
422 x M_Character_Toon      (ColorA role)
 84 x M_Character_Metal     (ColorC role)
  8 x M_Character_Glass     (ColorB role)
  3 x M_Character_ToonAlt   (ColorD role)
```

Distribution matches the expected 422 / 84 / 8 / 3 exactly.

### Idempotence

```text
catalog hash after run 1 : 0b489beea763ebb8a460cb3ed196e33050c9656f
catalog hash after run 2 : 0b489beea763ebb8a460cb3ed196e33050c9656f   -> byte-identical
```

## Real wiring — no runtime swapping

Hub (`MenuCharacterStage`, real catalog path):

```text
16 active skinned renderers · vendor refs 0 · runtime material instances 0
14 x M_Character_Toon  | ZombieWar/Character/Toon
 2 x M_Character_Metal | ZombieWar/Character/Metal
_ToonLightDirection = (0,0,0,0)  -> URP main-light FALLBACK path
```

Gameplay (real spawned player in Map_Level1):

```text
16 skinned renderers · vendor refs 0 · runtime material instances 0
14 x M_Character_Toon · 2 x M_Character_Metal      (identical roles)
_ToonLightDirection = (0.32, 0.77, -0.56), colour alpha 1  -> RIG path
batches 98 / SetPass 49
```

Hub and gameplay resolve the **same project-owned materials through the same catalog**, each exercising
its intended light source. Evidence: `Hub/Hub_production_migrated.png`.

## Protected state

```text
ColorA 58cb767c…  ColorB 0d220f3a…  ColorC 76c07744…  ColorD 010c82e8…   unchanged before/after
Player.prefab b6a6bad7…  Menu.unity 0d2a074a…  Map_Level1 2a827019…      unchanged
staged: 0
```

Vendor integrity proven by `git hash-object` before and after, not timestamps.

## Tests

```text
EditMode 504 / 504  PASS
PlayMode 227 / 227  PASS
```

## NOT DONE

- second consecutive full PlayMode run
- WebGL 2 build with the new production shaders
- wardrobe vision matrix (11 representatives)
- outline verification (MaskOnly / EdgeOnly / composite)
- lifecycle second-run check
- rendering before/after comparison table
- the new catalog/material assertions from Step 8

The gameplay capture attempt landed on a terminal screen (player died) and was not retaken.

---

# M5+ FINAL ACCEPTANCE (2026-08-14)

## Gate 1 — the 453 vs 517 ambiguity, resolved

Both numbers were right; they count different things, and the earlier report did not say which.

```text
catalogPartCount   = 453      (entries in the catalog)
materialSlotCount  = 517      (material slots across those entries)
difference         =  64

392 parts x 1 material slot = 392
 58 parts x 2 material slots = 116
  3 parts x 3 material slots =   9
                       total = 517   and   392 + 58 + 3 = 453
```

Multi-slot parts are genuinely multi-material meshes, e.g.
`Headgear_26 -> [M_Character_Glass, M_Character_Metal, M_Character_ToonAlt]`.

Role totals are **material-slot** counts: Toon 422 · Metal 84 · Glass 8 · ToonAlt 3 = 517.

`CharacterMaterialContractTests` now asserts both counts separately and requires them to reconcile, and
every failure message names the offending part and slot index.

## Gate 2 — authoring durability

```text
catalog hash run 1 : 0b489beea763ebb8a460cb3ed196e33050c9656f
catalog hash run 2 : 0b489beea763ebb8a460cb3ed196e33050c9656f   byte-identical
```

The source of truth is `RoleToProjectMaterial` inside `CasualCatalogGenerator`, applied through the FBX
importer remap. No catalog entry was hand-patched. A missing role material aborts generation loudly
instead of silently falling back to vendor.

## Gate 4 / 5 — real Hub and gameplay wiring (no swap harness)

```text
Hub      : 16 renderers · vendor refs 0 · runtime material instances 0
           14 x M_Character_Toon (ZombieWar/Character/Toon) · 2 x M_Character_Metal
           _ToonLightDirection = (0,0,0,0)                -> URP main-light FALLBACK path
Gameplay : 16 renderers · vendor refs 0 · runtime material instances 0 · identical roles
           _ToonLightDirection = (0.32, 0.77, -0.56), a=1 -> ToonLightRig path
           batches 98 / SetPass 49
```

Evidence: `Hub/Hub_production_migrated.png`.

## Gate 10 — WebGL 2

```text
result   : Succeeded      time 00:05:16
output   : Build/WebGL_M5Plus  (30 files, 255.6 MB, index.html + .wasm 104.2 MB + .data 133.6 MB)
scenes   : Bootstrap, Menu, Map_Level1 only
included : Compiling shader "ZombieWar/Character/Toon"
           Compiling shader "ZombieWar/Character/Metal"
           M_Character_Toon / Metal / ToonAlt / Glass all in build contents
```

The summary reported `errors=4`. **Investigated, none are shader or character related:** one
pre-existing `ProfileValueReference: GetValue called with empty id`, and two Performance-Testing
package `.meta` write failures (`PerformanceTestRunSettings.json.meta`,
`PerformanceTestRunInfo.json.meta`). The two `Shader error` lines in the log are historical — the
`MetalPrototype` `[Header(A - view normal band)]` parse error fixed earlier in this session. Both
production shaders currently report `GetShaderMessageCount = 0`.

**Browser execution NOT VERIFIED** — no browser automation in this environment. A successful build is
not the same as successful browser execution.

## Prototype retirement

`ToonPrototype.shader`, `MetalPrototype.shader` and all four `M_Char_*_H1/H2` materials were deleted.
Their evidence lives under `Review/M5Plus_CharacterShaderPrototype/`. Zero assets under
`Assets/_Project/Shaders/Character` or `.../Materials/Character` contain "Prototype" or "M_Char_".

## Tests

```text
targeted CharacterMaterialContractTests :   7 /   7
EditMode                                : 511 / 511
PlayMode pass 1                         : 227 / 227
PlayMode pass 2                         : 227 / 227
```

## Protected asset integrity (before vs after, git hash-object)

```text
ColorA 58cb767c… ColorB 0d220f3a… ColorC 76c07744… ColorD 010c82e8…   unchanged
Player.prefab b6a6bad7…  Menu.unity 0d2a074a…  Map_Level1 2a827019…   unchanged
catalog 0b489bee… (intended change, stable across both runs)          staged: 0
```

## Gates NOT executed

- Gate 3 wardrobe matrix (11 representatives)
- Gate 6 outline contract (MaskOnly / EdgeOnly / composite)
- Gate 7 two-run lifecycle counts
- Gate 8 rendering before/after table

M5+ therefore remains **INCOMPLETE**.

---

# M5+ TOON UNIFICATION — dedicated character metal CUT

Closes the four gates left unexecuted above (wardrobe matrix, outline, two-run lifecycle, rendering).
Full evidence: `Review/M5Plus_ToonUnification/`. Authoritative narrative: `Docs/MVP_SHIP_PLAN.md`,
section "M5+ TOON UNIFICATION".

## Decision

```text
Dedicated character metal: CUT for MVP
Reason:
- low gameplay-scale visual value
- random noise became dirt or disappeared
- brushed grain did not materially improve readability
- normal Toon preserves atlas colour and improves cohesion
- removes one material/shader role
```

Deliberate scope cut, not a missing feature. Geometry, atlas UVs and authored atlas colours unchanged;
only the lighting contract changed. Future stylized-metal work belongs in the Icebox.

Final roles: **Toon · ToonAlt · Glass**.

## Step 1 — authoring source of truth

```text
ColorA -> M_Character_Toon      ColorB -> M_Character_Glass
ColorC -> M_Character_Toon      ColorD -> M_Character_ToonAlt      (only ColorC changed)

catalog hash run 1 = run 2 = 6992ef669a0b   FBX remap run 1 = run 2
entries 453 · material slots 517 · Toon 506 · Glass 8 · ToonAlt 3 · Metal 0
vendor refs 0 · runtime instances 0 · null refs 0
392x1 + 58x2 + 3x3 = 517   and   392 + 58 + 3 = 453
```

Zero catalog entries hand-patched — all 84 former-ColorC slots migrated through the FBX importer remap.
Missing project material still fails loudly and aborts.

## Step 2 — automated contracts

`CharacterMaterialContractTests` — 11 tests, all passing. Asserts each count separately
(453 / 517 / 506 / 8 / 3 / metal 0 / vendor 0 / runtime instances 0), that the totals reconcile, that
ColorC resolves to `M_Character_Toon` through the FBX remap, that a regeneration would therefore be a
no-op (idempotence without mutating assets), that no runtime costume path can recreate the metal
material (`sharedMaterials` only, no `.material` assignment), and that the retired shader/material stay
gone. Every failure message names the offending slot, part and material-slot index.

## Step 3 — retirement

```text
CharacterMetal.shader (+ .meta)        deleted
M_Character_Metal.mat (+ .meta)        deleted
```

Zero references proven first: catalog 84 -> 0 · prefabs 0 · scenes 0 · other ScriptableObjects 0 ·
materials 0 · shaders 0 · AlwaysIncludedShaders absent · PreloadedAssets absent · tests updated.
`Shader.Find("ZombieWar/Character/Metal")` returns null. Kept: `CharacterToon.shader`,
`CharacterLighting.hlsl`, `M_Character_Toon/ToonAlt/Glass`, all earlier `Review/` evidence.

## Step 4 — visual acceptance (8 cases, real catalog assembly)

| # | case | catalog part | former role | final material | shader | renderers | slots | inst | verdict |
|---|---|---|---|---|---|---|---|---|---|
| 1 | large silver helmet | Headgear_45 | ColorC | M_Character_Toon | ZombieWar/Character/Toon | 3 | 3 | 0 | PASS |
| 2 | silver gauntlet | Glove_16 | ColorC | M_Character_Toon | ZombieWar/Character/Toon | 3 | 3 | 0 | PASS |
| 3 | silver boots | Shoes_46 | ColorC | M_Character_Toon | ZombieWar/Character/Toon | 3 | 3 | 0 | PASS |
| 4 | gold | Headgear_46 | ColorC | M_Character_Toon | ZombieWar/Character/Toon | 3 | 3 | 0 | PASS |
| 5 | dark former-metal | Headgear_61 + Glove_17 | ColorC | M_Character_Toon | ZombieWar/Character/Toon | 4 | 4 | 0 | PASS |
| 6 | small jewellery | Earring_9 + Bracelet_2 | ColorC | M_Character_Toon | ZombieWar/Character/Toon | 5 | 5 | 0 | PASS |
| 7 | mixed Toon+metal+Glass | Headgear_26 + Eyewear_14 | ColorB/C/D | Toon + Glass + ToonAlt | Toon + STWK/Glass | 4 | 8 | 0 | PASS |
| 8 | ordinary, metal accents | Top_42 Bottom_35 Shoes_21 Headgear_44 | ColorA+ColorC | M_Character_Toon | ZombieWar/Character/Toon | 7 | 10 | 0 | PASS |

```text
silver     : holds mid-grey with distinct plane steps, does NOT clip to flat white
gold       : remains clearly gold, darker inner faces intact
dark metal : does NOT crush, bright/dark facet split legible
jewellery  : gold hoops + gem bracelets read at TRUE Hub preview scale (512x900, real stage camera)
mixed      : Glass still reads as glass through the dome, former-metal rim separates
gameplay   : silhouette readable top-down, player distinct from enemies and ground
no pink material · no broken atlas/UV · no unwanted glow · runtime material instances 0
```

Representatives were chosen from a 29-part survey of every pure former-ColorC item
(`_survey_former_metal.png`), not guessed.

**Two harness corrections made during this gate, both caught by inspecting the output:** the first
contact sheet did not match its labels because `ClearAll()` uses deferred `Destroy()` and old renderers
survived inside a single call (fixed with immediate teardown); and an early "jewellery invisible at Hub
scale" reading was an artifact of 300 px survey tiles, disproved at true Hub framing.

## Step 5 — real Hub and gameplay

```text
Hub      : 15 renderers · 18 slots · 1 distinct material · vendor 0 · inst 0 · metal 0
           _ToonLightDirection (0,0,0,0)          -> URP main-light FALLBACK
Gameplay : 15 renderers · 18 slots · 1 distinct material · vendor 0 · inst 0 · metal 0
           _ToonLightDirection (0.32,0.77,-0.56)  -> ToonLightRig
```

## Step 6 — outline

```text
player        22/22 in mask (16 costume bit 3 + 6 weapon bit 1) — ONE contiguous silhouette
enemies        8/8  bit 2 (VAT material-driven, avoids bind-pose freeze)
solid decor   25/25 bit 4 (deliberate, pre-existing)
ground         0/25   foliage 0/25   contact shadow not in mask -> no foot rectangle
not frozen    mask 83,961 -> 198,338 px over 8.5 s of game time, centroid moved
profile hash  014a03d6… unchanged   restored: SelectionOnly / debug None / active / thickness 2
```

## Step 7 — two-run lifecycle

Both runs identical: players 1 gameplay / 0 Hub · cameras 1 gameplay / 2 Hub · AudioListeners 1 ·
world streamers 1 · costume renderers 15 · slots 18 · distinct 1 · runtime instances 0 · vendor 0 ·
metal 0 · stale costume slots 0. Weapon switch exercised (Pistol -> G36C).

## Step 8 — rendering

```text
Hub       batches 67-76 · SetPass 23-32 · 15 renderers · 18 slots · 1 distinct · inst 0
Gameplay  0 enemies : batches 103 / SetPass 28 / draws 103
          13 enemies: batches 109 / SetPass 57      22 enemies: batches 125 / SetPass 59
          player distinct OPAQUE materials 1 · Toon shader passes 2 (ForwardLit + DepthOnly)
```

Distinct opaque character materials drop **2 -> 1** for any outfit containing former-ColorC geometry;
the dedicated metal shader/pass no longer exists. **No batch reduction is claimed** — no frame
comparable enough to attribute a delta was captured.

## Step 9 — tests

```text
targeted material + outline/lifecycle :  24 /  24
EditMode                              : 515 / 515
PlayMode pass 1                       : 227 / 227
PlayMode pass 2                       : 227 / 227
```

## Step 9 — WebGL 2

```text
result      : Succeeded   totalErrors 0   totalWarnings 16   130 MB / 29 files / 16m43s
CharacterToon.shader + M_Character_Toon/ToonAlt/Glass  : present in build contents
CharacterMetal.shader + M_Character_Metal.mat          : ABSENT from build contents
no missing shader · no pink material · Toon shader 0 messages / 2 passes
browser execution : NOT VERIFIED (no browser automation available)
```

## Protected assets (sha1, before vs after)

```text
ColorA fb51310d…  ColorB 53f4ea21…  ColorC e5284e42…  ColorD 4bda571b…   unchanged
Player.prefab d7b14e9d…  Menu.unity 1bc90c03…  Map_Level1 058c4cc5…     unchanged
outline profile 014a03d6…  EditorBuildSettings 8b83756b…                unchanged
catalog 34cb63e2… -> 6992ef66…  (intended, stable across both runs)      staged: 0
```

**M5+ COMPLETE.** M6 NOT STARTED.
