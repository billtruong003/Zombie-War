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
