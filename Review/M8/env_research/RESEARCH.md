# HordeCall — Environment themes research (M8, 2026-09-30)

Read-only research. No project file was changed. Paths are relative to `D:\Projects\Zombie-War`.
Triangle counts are estimates from binary FBX headers (`PolygonVertexIndex` corners / 3). They are
exact for triangulated meshes and an upper bound for quad meshes. Baked decoration counts are exact
(read from `DMS_*.asset`).

## 1. How the map is built today

**Streaming.** `Assets/_Project/Scripts/Runtime/World/Streaming/`
- `WorldStreamingConfig.cs` / `Data/World/WorldStreamingConfig.asset`: 32 m chunks, `renderRadius 2`
  gives 25 active chunks, ground grid res 17 (289 verts / 512 tris per chunk), seed 20260809. It holds
  one `DecorationPalette` reference (`guid 7922d…` = `Data/World/Decoration/DecorationPalette.asset`).
- `ChunkInstance.cs:143-145`: each pooled chunk root has exactly 3 renderers, `Ground`,
  `SolidDecor` and `Foliage`, with no shadows (`:185-186`). Only `SolidDecor` gets the environment
  outline rendering-layer bit (`:156-158`). Empty outputs disable the renderer; nothing is created or
  destroyed at runtime.
- `ProductionWorldStreaming.cs:27-29,102`: the three materials are serialized fields passed to
  `WorldStreamingRig.Build`. **This is the natural seam for a theme swap.**

**Ground.** `BiomeSampler.cs` is a deterministic global value-noise field (macro 256 m, moisture,
fertility, rockiness). It outputs 4 normalized weights, Dry/Grass/Sand/Rock, written to **vertex
colour RGBA** (`BiomeSample.ToColor`). There is no biome enum, so chunk seams are continuous.
- Shader `Art/Shaders/WorldStreamingGround.shader` (286 lines, hand-written HLSL, target 3.0) blends
  4 world-XZ-tiled albedo textures by the vertex RGBA, plus a noise texture and optional normals
  (`_NORMALMAP` shader_feature). That is 5 samples, or 9 with normals.
- Material `Art/Materials/M_WorldStreamingGround.mat` uses 4 layers from the Cartoon Texture Pack:
  - Dirt_Path (dry)
  - Grass_Dense_Tint_01 (grass)
  - Sand_Beach (sand)
  - Rock_Volcanic_B (rock)
  
  It also uses `_Project/Art/Textures/Noise/Noise_Perlin_01.png`. All are **2048²** with an Android
  max size of 2048. Tiling is `_DryTiling 13`, `_GrassTiling 6` (metres per repeat), so the texel
  density is far higher than a phone at a 58° top-down view can show.

**Decor.** `Decoration/DecorationPalette.cs` holds entries with a biome affinity `Vector4` (D,G,S,R),
cell size, density, spacing, clumping, tint, wind amplitude and a far-ring density flag.
`DecorationMeshSource` assets hold vendor geometry baked in the Editor, remapped into **one atlas per
category**:
- `Art/WorldStreaming/Decoration/T_WorldStreamingFoliageAtlas.png` (2048²)
- `T_WorldStreamingSolidAtlas.png` (2048²)

The builder combines everything per chunk into two UInt16 meshes (`DecorationMeshBuilder.cs:236`).
Active budgets are 45k verts Solid and 45k verts Foliage per chunk, with a hard ceiling of 60k
(`DecorationPalette.asset:322-324`). Other palettes (`_C1`, `_CandidateB`, `_H1`) already exist, so
**palette-per-look is proven**.

| Mesh source (active palette) | Verts | Tris | Vendor |
|---|---|---|---|
| grass_b / grass_c | 230 | 210 | Lux `S_Grass_B/C` |
| bush_a / b / c / d | 1863 / 2393 / 3508 / 3913 | 1656–4170 | Lux `S_Bush_A-D` (**heavy**) |
| tree_a (leaf + trunk) | 1840 + 2564 | 2200 | Lux `S_Tree_A` |
| rock_a / rock_b | 1194 / 3372 | 554 / 1578 | KayKit `Stone_Chunks_*` |
| debris / crate / barrel / pallet | 398–2424 | 192–1556 | KayKit Resource Bits |
| tire | 825 | 704 | JC MegaCity `SM_IndustrialProps_Wheel_01` |

**Foliage shader.** `Art/Shaders/WorldStreamingFoliage.shader` does alpha-cutout toon shading with
two-sided wrap. **Wind runs in the vertex shader**: a sine wave plus gust along the wind direction.
UV1.x holds the clump phase and UV1.y the per-entry amplitude (`:91-118`).

**Solid shader.** `WorldStreamingSolidDecor.shader` is an opaque toon atlas with vertex tint. All
three shaders are SRP-Batcher-friendly; `WorldStreamingDebugView.cs:64` deliberately avoids MPBs.

**In-house shader kit.** Package `Packages/com.billtruong.stylized-toon-world-kit` (v0.6.0, URP 17,
hand-written HLSL, instancing and SRP Batcher). Relevant parts:
- `Environment/StylizedTerrain.shader`: slope and height 3-layer blend with triplanar cliff.
- `StylizedGrass.shader`: card wind and root→tip gradient.
- `StylizedWater.shader` and `StylizedOcean.shader`: **both need the Depth Texture**.
- `Surface/StylizedLava.shader`: 2× 4-octave procedural FBM with flow per pixel (`:148`), which is
  **ALU-heavy on low-end if it covers the screen**.
- `StylizedIce.shader`: voronoi sparkle.
- `VFX/StylizedDissolve.shader`.

## 2. Third-party pack inventory (environment-relevant)

Style key: **A** = matches cute chunky low-poly, **B** = usable with retint or toon shader,
**C** = style clash (sharper, realistic or grim).

| Pack (path) | Env content | Size / textures | Tris (samples) | Style | Themes |
|---|---|---|---|---|---|
| `Assets/Cartoon_Texture_Pack` | Tiling PBR-ish cartoon textures with normals:<br>• DIRT_Path<br>• GRASS_Dense (tints) and GRASS_Flower<br>• ROCKS_Cliff A/B and ROCKS_Volcanic A/B<br>• SAND_Beach, SAND_Underwater, shell variants<br>• WALL Bricks/Stone, WOOD, ROOF | 304 tex, 87 mats, 694 MB on disk, all 2048² | none (textures only) | A/B | ground layers for every theme except snow and toxic |
| `Assets/Vegetation_Stylized_Pack_ByLuxArtStudios` | 24 bushes/plants:<br>• 4 bush, 4 fern, 7 flowers, 5 grass<br>• 2 cattail, 2 clover<br>• 7 trees + LODs (`Mesh/Trees/LODS`)<br>• own ShaderGraphs | 16 tex (2048² D/N/RMA atlases), 92 MB | trees 1.5–2.1k tris; bushes 1.6–4.2k | A | grassland, forest, swamp |
| `Assets/KayKit/Packs/Bits/KayKit - Resource Bits` | 132 small props:<br>• stone chunks, wood logs/planks, pallets<br>• barrels, crates, parts piles<br>• ore, gems, money | **one 1024² palette texture**, 8 MB | 100–450 typical; Money_Pile 2.4k | **A** (best fit) | all themes (debris and loot) |
| `Assets/JC_LP_MegaCity` | 747 prefabs:<br>• 127 buildings, 75 building decor<br>• 100 floor tiles (road, park, parking, railway, port)<br>• 67 floor props, 162 props, 26 industrial<br>• 30 nature (12 trees, 10 landscape, rocks, grass), 67 vehicles, 87 food | 8 tex (2048² palette `T_MegaCity_01/02`, water normal), 127 MB | tree 450–1k; landscape 1.5–4.6k; house up to **12.5k** | A/B | city ruins, park, port/beach |
| `Assets/Synty/PolygonGeneric` | 220 env prefabs:<br>• Ground 43, Road 26, Rock 15, Bush 14, Grass 11, Tree 9<br>• Flowers 8, Dirt 8, Cliff 7, Ivy 16<br>• Mushroom 3, Lilypads 3, Water 2, Waterfall 2, Stump/Log/Root/Fern<br>• 107 props; FX Snow, Rain, Leaves, Fog, Fireflies | 22 tex (1–2k), 456 MB with the other Synty packs | ground tile ~230; fern ~330 | B | forest, swamp, grassland, city edge |
| `Assets/Synty/PolygonDarkFantasy` | Env:<br>• Vine 25, Rock 12, Grunge decals 12, Ground 8<br>• Basalt 5, Moss 5, Tree 5, Cliff 4, Landslide<br>• 10 tombs, 10 gargoyles, skulls, candles, pyres, statues<br>• body, noose and gibbet props | Synty palette mats | hill 1.3k; grunge 5; rocks ~230 | **C** (grim) | graveyard/night, volcano (basalt) |
| `Assets/Synty/PolygonKaiju` | 7 destroyed city blocks, 9 highrises, rubble, bridge rubble<br>`SM_Env_Fissure_01`, ocean waves, shipwrecks, boats, neon | 16 tex, `PolygonKaiju_01` is **4096²** | not sampled | B | city ruins, beach, cracked earth (fissure) |
| `Assets/Tiny Teacup Studio/Low Poly Desert Environment` | 13 prefabs:<br>• 3 cactus, 3 cliff, 5 rocks<br>• 1 ground, 1 tree | palette `Palette.png` 1216×640 | rock 42–72; cactus 390–2.7k | **A** | desert, cracked earth, canyon |
| `Assets/Tiny Teacup Studio/Military Base Pack` | hangars, garage, tower, containers, barrels, fence, ground blocks | shares palette; ships baked lightmaps (not needed) | 50–1.3k | A | desert outpost, wasteland |
| `Assets/Playground Low Poly` | 60 prefabs: playground sets, toys, benches, sandbox, 3 trees, grass, flowers, stones | `T_Generic_colors.psd` palette | 6–1.6k | **A** | park/toy town (cute) |
| `Assets/ThirdParty/Epic Toon FX` | 1455 FX prefabs:<br>• LiquidLava, LiquidAcid, LiquidWater (+OBJ), WaterFlowing/Dripping<br>• SnowExplosion, dust, bubbles | 551 MB | particles | A | ambient/hazard VFX per theme |
| `Assets/Scalable Grid Prototype Materials`, `DuNguyn/Loot Box/Model/envi` (plane, skybox) | prototype/grid | small | — | — | none |

Not environment: `ThirdParty/Layer Lab` (UI, 1 GB), `Monsters*`, `GAMWILL*`, weapon packs, audio packs.

## 3. MinionsArt (Joyce, @minionsart) techniques

**Sources and licence.** GitHub `github.com/MinionsArt` has 5 repos, all HTML. The main one is the
tutorial index at `minionsart.github.io/tutorials`. There is **no shader code on GitHub and no
LICENSE file**. The code lives on Patreon, mostly paid tiers.
- The grass-system page says the files may be used in free and commercial projects, may be
  modified, and may not be resold.
- A Patreon "License & Usage" post reportedly says tutorials are CC BY 4.0. Only a search snippet
  was seen; the page returned 403, so **confirm before shipping any copied code**.

The table below is based on the tutorial index plus search snippets. Patreon internals were not
read directly.

| Technique | What it does | URP | Mobile cost | Mobile-friendly approach for HordeCall |
|---|---|---|---|---|
| Vertex grass sway (patreon 13724221, SG/URP 32245525) | sine wind in vertex shader, masked by UV/vertex colour | yes (SG) | cheap | **already done** in `WorldStreamingFoliage.shader` |
| Interactive bend (19844414) | vertices pushed away from the player within a radius | Built-in, easy port | cheap | add a global `_PlayerPosRadius` and apply only to grass entries (per-entry flag in UV1) |
| Geometry-shader grass + painter (40090373, 40077798; URP 47447321) | blades generated in a GS | URP HLSL | **Not usable.** Metal/iOS has no GS; slow or broken on many Mali/Adreno GPUs | baked card/clump meshes combined per chunk (current path) |
| Compute grass (URP 54164790) and Grass System 2023–26 (83683483, 85356573) | compute-built blades, indirect draw, octree culling, painter, cutting | URP + SG | needs compute and SSBO in the vertex shader. Reported pink on GLES (`maxComputeBufferInputsVertex = 0`) | skip. Revisit only for a Vulkan-only build after checks on real devices |
| Quad/octree culling (80481278) | CPU culling of instanced sets | any | cheap | the chunk ring already culls; no need |
| Lava (Built-in 32245619, SG 33388865) and Vertex Flowmap Painter (2026, 161842846) | scrolling noise masked by vertex colour; flow direction stored in vertex colour | SG/URP | cheap: 2–3 texture reads, no depth | **use this style.** Flat lava plane or ground channel; 1 baked noise texture plus 2-phase flow |
| Cracked earth | **no MinionsArt tutorial found** | — | — | mesh-decal quads (alpha-cut crack atlas) or a crack mask in a ground layer. Avoid URP screen-space decals (they need depth) |
| Mesh decals (72532653), forward decals (49899065) | projected or mesh decals | URP | mesh decals cheap; SS decals need depth | mesh decals only, combined into SolidDecor or a separate transparent slot |
| Toon water (15121329, 30490169; Stylized Water 2026) | depth foam, refraction, waves | SG/URP | depth + opaque texture = extra bandwidth | vertex-colour foam, scrolling normal, no refraction, no depth |
| Snow (15944770; URP trails 47452596; vertex-colour snow/icicles 118211037) | build-up by normal.y; RT trails; tessellation variant | URP | tessellation is **not mobile**; an RT trail costs one RT per frame | vertex-colour or normal.y snow cap on props plus a white ground layer; skip trails |
| Triplanar (16714688) | 3-axis projection | SG | 3× reads | only on cliffs (world kit `StylizedTerrain` already has it) |
| Terrain/mesh blending (URP, 2026, 152116212) | top-down RT bake of ground albedo/depth; props blend into it | URP renderer feature | 3 extra passes if done every frame | fake it: tint the SolidDecor base by the biome colour sampled at bake time (vertex colour) |
| Vertex-colour ground (Polybrush 21033280, SG 27942969) | 2–4 textures blended by RGBA | SG | cheapest | **already done** (`WorldStreamingGround.shader`) |
| Dissolve / world-pos reveal (35698820; urp-world parts 1–3) | noise threshold clip | SG | clip breaks early-Z on tiler GPUs | short-lived objects only (stations already have `StationDissolve.shader`) |
| Light-cookie cloud shadows (88618240) | animated main-light cookie | URP | 1 texture read | optional cheap atmosphere for forest/swamp |

Takeaway: the game's current pipeline already covers MinionsArt's mobile-safe ideas (vertex-colour
ground, vertex wind). The missing pieces worth taking are player bend, flow-map lava and mesh-decal
cracks.

## 4. Proposed multi-theme plan

**Principle.** Keep the proven 3-renderer chunk (`Ground` / `SolidDecor` / `Foliage`) and the
deterministic RGBA biome field. A theme **re-interprets** the 4 channels; it does not add biomes.
One theme is loaded per run, and no new scenes are needed.

**`MapTheme` ScriptableObject** (proposed; new file in `Runtime/World/Streaming/`):
- `groundMaterial`: same `WorldStreamingGround` shader. Per theme, 4 layer textures + 4 tints +
  tiling; optionally one `Texture2DArray` (4 slices) to cut samplers to 2.
- `solidMaterial` / `foliageMaterial`: same shaders with a per-theme atlas.
- `DecorationPalette`: already an SO. It holds affinities and budgets and one palette per theme,
  e.g. `DecorationPalette_Desert.asset`.
- `Optional hazardSurface` material and prefab (lava or water plane), ambient VFX prefab from Epic
  Toon FX, and values for `ToonLightRig` (sun colour, ambient, fog colour), background colour and
  wind strength.
- Swap flow: menu or contract picks a theme → `ProductionWorldStreaming` reads
  `theme.groundMaterial` etc. instead of its 3 serialized fields → `WorldStreamingRig.Build`
  (`ProductionWorldStreaming.cs:102`). The swap happens once per run at pool warmup; runtime swaps
  are not needed.
- Load each theme's textures and atlases through Addressables (`AddressableAssetsData` already
  exists) so only one theme is resident.

**Editor bake.** Reuse `Scripts/Editor/World/DecorationAuthoring.cs` and
`CandidateBPaletteBuilder.cs` to bake `DMS_*` sources and one Solid + one Foliage atlas per theme.
Prefer palette-texture packs (KayKit 1024², Tiny Teacup 1216×640, Playground and MegaCity palettes):
their UVs point at small colour swatches, so they pack into a **512–1024 atlas cell** without loss.

**Ground shader changes (small).**
- Add a per-theme `_LayerTint[4]`.
- Add an optional **5th "feature" mask**, reusing the noise texture's G/B channels, for cracks,
  toxic puddles or lava seams. It is emissive-capable and costs one extra sample and a lerp.
- Add player bend to foliage: one global vector, gated by UV1.y amplitude.

**Budget targets (low-end Android: Mali-G52/G57, Adreno 610 class).**
| Item | Now | Target per theme |
|---|---|---|
| Env draw calls | 3 per visible chunk, plus the outline pass for SolidDecor. The 58° camera sees about 9–12 of the 25 chunks, so roughly 30–40 | **≤ 60** total including hazard plane, ambient VFX and outline |
| Ground textures | 8 × 2048² + noise, about 20 MB with ASTC 6×6 and mips | 4 albedo at **1024²** (normals optional or off on low tier) ≈ 3–5 MB. 2048 is wasted at 6–13 m tiling |
| Decor atlases | 2 × 2048² ≈ 5 MB | Solid 1024–2048, Foliage 1024 ≈ 3–5 MB |
| Theme extras (lava flow, decal atlas, VFX textures) | — | ≤ 8 MB |
| **Total env textures resident** | ~25 MB | **≤ 15–20 MB** (hard cap 30 MB) |
| Foliage verts per chunk | budget 45k (Lux bushes are 1.9–3.9k verts each) | **≤ 12k**; replace Lux bushes with ≤ 500-vert cards/clumps |
| Solid verts per chunk | budget 45k | **≤ 10–15k**; props ≤ 1.5k verts each, big buildings only as border set-dressing |
| Ground | 289 v / 512 t per chunk | keep |

Rough frame total at target: 12 visible chunks × ~25k verts ≈ 300k env verts. Measure it with the
existing `Profiling/WorldStreamingBenchmark` (profiler only, per the device-testing rule).

**LOD and culling.**
- The chunk ring is the culling unit, and URP frustum-culls each chunk's 3 renderers.
- Keep `distanceDensity` (outer ring foliage at 0.4). Add an outer-ring **solid LOD**: bake a low
  variant per `DMS` (e.g. Lux `Mesh/Trees/LODS`) and choose it by `DecorationDensityTier`.
- No `LODGroup` per prop; props are combined.
- Keep shadows off for decor (already off) and use the shadow blob or contact shadow for actors.
- Keep alpha-clip limited to Foliage. Solid stays opaque for early-Z.

**Hazards and special surfaces.**
- Lava: a flat plane or ground channel with a baked noise texture and 2-phase flow (MinionsArt
  style). Do **not** run `StylizedLava`'s 8-octave procedural FBM full-screen on low tier; bake its
  output to a 512² texture instead.
- Water: `StylizedWater` needs depth. For low tier, write an unlit variant with vertex-colour foam.

## 5. Candidate themes

| # | Theme | Ground layers (R/G/B/A) | Foliage | Solid props | Hazard / VFX | Missing |
|---|---|---|---|---|---|---|
| 1 | **Meadow** (current) | Dirt_Path / Grass_Dense_Tint_01 / Sand_Beach / Rock_Volcanic_B | Lux grass, flowers, fern, cattail; lighter bushes | KayKit stones, crates, pallets, barrels; MegaCity wheel | Synty FX_Leaves | lighter bush meshes |
| 2 | **Desert wasteland** | Dirt_Path (tinted) / Sand_Beach / Sand_Beach_Shell / ROCKS_Cliff_A | sparse dry grass (retinted Lux S_Grass), Tiny Teacup Cactus_02/03 | Tiny Teacup Rock_01-05, Cliff, CliffCorner; Military barrels, containers, fence; KayKit fuel/jerrycan | Epic Toon dust, Synty FX_Dust_Spots | dunes, bones or skulls in cute style, crack decal texture |
| 3 | **Cracked badlands** | cracked-dirt layer (**missing**; derive from Dirt_Path plus crack mask) / Dirt_Path / Sand / ROCKS_Cliff_B | almost none | Tiny Teacup rocks, Kaiju `SM_Env_Fissure_01`, KayKit stone chunks | heat haze skipped; dust | crack decal atlas; fissure mesh check (Synty style) |
| 4 | **Forest** | Grass_Dense_Tint_02 / GRASS_Flower / Dirt_Path / ROCKS_Cliff_A | Lux trees (outer ring LOD) or MegaCity `SM_Nature_Tree_01-12` (450–1k tris); Lux ferns and clovers; Synty Fern and Mushroom | Synty Stump, Log, Root, Rock; KayKit Wood_Log, planks | Synty FX_Leaves, FX_Flies; cookie cloud shadows | low-tri cute canopy tree (Lux trees 1.5–2k tris each) |
| 5 | **Volcano** | Rock_Volcanic_A / Rock_Volcanic_B / Dirt_Path (dark tint) / lava-seam emissive mask | none or charred | DarkFantasy Basalt ×5, Rocks, Landslide; KayKit stone chunks (dark tint) | flow-map lava plane; Epic Toon LiquidLava, LiquidExplosionLava | baked lava flow texture, charred tree, emissive crack decals |
| 6 | **City ruins** | Synty Generic_Concrete / Generic_Dirt / Cartoon WALL_Stone (tiling) / Generic_Grass | weeds (Lux grass), Synty Ivy | MegaCity FloorProps and Props (cones, bins, cars 67), Kaiju Rubble, Bridge_Rubble, City_Destroyed_01-07 (border only), KayKit parts piles | Synty FX_Smoke, Fire | low-tri ruin chunks for the play area; MegaCity houses up to 12.5k tris, so border use only |
| 7 | **Snow** | white ground (**missing**; derive via tint from SAND_Beach, or author one) / packed snow / ice (world-kit `StylizedIce` cheap variant) / ROCKS_Cliff_B | snowy pines (**missing**); retinted Lux or MegaCity trees with a normal.y snow cap | KayKit stones (tint), Military containers | Synty FX_Snow, Epic Toon SnowExplosion | snow ground texture, pine tree, snowman or cute props |
| 8 | **Toxic swamp** | Dirt_Path (green tint) / Grass_Dense_Tint_02 / SAND_Underwater / toxic-puddle emissive mask | Lux cattails, clovers, ferns; Synty Lilypads, Mushroom | Synty Log, Root, Stump; KayKit barrels (Fuel_A_Barrel_Dirty) | Epic Toon LiquidAcid, bubbles; unlit goo plane | goo decal texture, toxic-barrel hero prop |

**Also possible.**
- **Beach**: SAND_Beach and Shell; Kaiju Shipwreck, Fishing boat, Yacht, ocean waves; MegaCity
  `SM_Floor_Port_01`. Missing: palm trees.
- **Playground / toy town**: `Playground Low Poly`, 60 prefabs, the cutest fit. Good for an event
  or FTUE map.

**Graveyard/night** is possible with PolygonDarkFantasy tombs, gargoyles, candles, grunge and vines.
It is **style C**: sharp and grim. Exclude `SM_Prop_Body`, `Noose` and `Gibbet` for tone. Ship it
only after a retint or toon test.

## 6. Risks and next steps

1. **Style consistency.** Synty (Polygon) and DarkFantasy are sharper than KayKit, Tiny Teacup and
   Playground. Run every theme through the existing toon shaders with palette retint, and check an
   owner-approved capture per theme (`UiShot`-style sheet) before committing.
2. **Current budgets are generous** (45k/45k per chunk) and the Lux bushes are heavy. This is the
   first thing to profile on the remote device (profiler only).
3. **Ground texture memory.** Downscale the 2048² layers to 1024 for Android. This saves about 75%
   with no visible loss at 6–13 m tiling. Verify with a capture.
4. **Licence.** Treat MinionsArt Patreon code as reference only. Re-implement in the project's own
   HLSL, as the existing shaders already do.
5. **Order of work** (fits phase "map theme" in `phase-order-0929`):
   1. `MapTheme` SO + `ProductionWorldStreaming` hook.
   2. Desert and Volcano palettes, since they have the best pack coverage and the biggest look
      change.
   3. Lava-plane shader + crack decals.
   4. Forest, City ruins, Snow, Swamp.
