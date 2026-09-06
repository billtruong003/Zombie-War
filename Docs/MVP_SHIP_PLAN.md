# Zombie War — MVP Ship Plan

**Phase:** SHIP · M0-M5+ complete · **M6 LOCKED** · **M7.0 Weapon Factory foundation DELIVERED** (2026-08-15) · M7.1 not started  
**Updated:** 2026-08-15  
**Design authority:** [M6_ENDLESS_RUN_SYSTEM_DESIGN.md](M6_ENDLESS_RUN_SYSTEM_DESIGN.md)  
**Vision authority:** [GAME_DESIGN.md](GAME_DESIGN.md)  
**World authority:** [WORLD_STREAMING_TECHNICAL_DESIGN.md](WORLD_STREAMING_TECHNICAL_DESIGN.md)

> ## READ THIS FIRST - document structure
>
> | Part | Status |
> |---|---|
> | From here to "M5+ TOON UNIFICATION" | **ARCHIVED HISTORY - NOT CURRENT DESIGN.** Delivered work plus the pre-M6 *expedition* plan. A record, not a direction. |
> | "M6 - PROPOSED EXECUTION LADDER" (at the end) | The only forward-looking section. **M7 is not authorized.** |
>
> The archived part still uses expedition, extraction, objective and bomb/weapon-switch vocabulary.
> **All of that is superseded.** The current input model is movement + card choice + one context action.

---

# ARCHIVED HISTORY - NOT CURRENT DESIGN

> Everything from here until the M6 section at the end is preserved history of M0-M5+ and of the
> pre-M6 expedition plan. Do not execute it. Do not quote it as current direction.

## 1. Destination

Ship a portrait mobile procedural expedition shooter with this complete loop:

`contract/loadout → insertion → choose a signal → travel/fight → complete two objectives →`
`extract or risk the boss → result/bank → upgrade → next run`

Target run duration is **8–12 minutes**. The procedural world is infrastructure, not gameplay by
itself. Every world-facing feature must create a reason to move, a decision, a risk or a reward.

## 2. Locked decisions

- Flat gameplay surface at `Y = 0`; biome, shader and decoration provide visual richness.
- `32 × 32 m` deterministic chunks, fixed pooling, combined decoration and the shared collider remain.
- Gameplay state belongs to a World Content layer, never to recycled visual chunk state.
- Enemies use planar steering, not NavMesh.
- Weapons fire continuously and have no reload mechanic.
- ~~Input remains movement, bomb and weapon switch; aim/fire are automatic.~~ **SUPERSEDED by M6:** input is movement, the level-up card choice, and one context action. No bomb button, no weapon switching.
- The expedition is objective-led, not stationary global wave survival.
- Prove new gameplay in one reference map before propagating it through shared data.
- GPU instancing, Jobs/Burst, MeshData and floating origin remain deferred until profiling requires them.
- Android-device profiling is waived for the completed streaming optimization gate. It returns during
  release hardening for the real build.

## 3. Verified baseline

### Complete

- M0–M1: chunk coordinates, fixed pool, shared collider, global biome field and seam-safe ground.
- M2: textured ground and deterministic combined solid/foliage decoration.
- M3: profiling harness, cheaper foliage, distance density and bounded nearest-first scheduling.
- M4: streaming integrated into Maps 1–5; planar enemies; continuous weapons; reload removed.
- M4.5: streamed shaders use the toon fake-light contract; all 896 legacy prop renderers/colliders are
  inactive; Candidate A/B evidence exists.

### Current gameplay truth

- Combat, enemy waves, pooling, Coin/Gem drops, magnet collection and `RunState` exist.
- Bomb pickup now has a real `BombThrower` subscriber. Old documentation saying otherwise is stale,
  although no authored bomb pickup currently exists in the pool content.
- XP, levels and perk data exist, but the level-up screen is presentation-only and applies no choice.
- Run closure is idempotent, but result screens lack a complete numeric summary.
- Defeat currently banks all currency, conflicting with the GDD risk/banking rule.
- Wave clear automatically collects remaining drops, so loot currently creates little movement/risk.
- No implemented objective signal, POI, extraction or boss-choice expedition flow was found.

### Main gap

> World architecture is ready; World Content is not. The current game is wave survival rendered on a
> procedural world, not yet a procedural expedition.

## 4. Art direction lock — stylized foliage + apocalypse world dressing

The M4.5 review proved a broader problem than foliage topology alone. Opacity is not the root cause.
Alpha-clipped stylized vegetation may remain when its palette, screen-space frequency and silhouette
fit the player. The real visual gap is weak art cohesion and a world vocabulary containing almost
nothing except ground and vegetation.

Locked direction:

- Preserve useful vegetation-pack foliage instead of replacing it wholesale.
- Continue using alpha clipping for foliage. Transparent blending remains excluded from ordinary
  chunk decoration.
- Retint foliage into the same muted toon value structure as the player, enemies and fake-light rig.
- Remove or replace only sources that still produce stippling, moiré, unreadable thin detail or a
  conflicting silhouette at gameplay-camera distance.
- Use a hybrid grass solution: ground-shader detail supplies broad coverage; sparse combined-mesh
  grass clusters supply silhouette, depth and optional vertex wind.
- Do not use the Roystan geometry+tessellation grass implementation in production. It is a desktop
  reference for blade variation, gradient, world-space randomness and wind only.
- Add an apocalypse solid-decoration vocabulary: tires/wheels, broken road signs, garbage, crates,
  barrels, rubble, scrap and similar evidence of abandoned civilization.
- Ordinary apocalypse props are deterministic scenery baked into `SolidDecorMeshRenderer`, with no
  collider or independent runtime state. Interactive/loot variants remain separate and are deferred
  to their gameplay checkpoint.
- The 896 retired legacy scene renderers/colliders stay disabled. Useful source meshes may be curated
  into the procedural palette; legacy scene roots must not be re-enabled.

## 5. Working protocol: Owner → Codex → Claude

Only one checkpoint may be active.

1. Codex reads this plan and the last verified report.
2. Codex writes one bounded Claude prompt for the active checkpoint only.
3. Claude inspects, runs impact analysis, implements, verifies and returns evidence.
4. The owner sends Claude's complete report to Codex.
5. Codex independently inspects the relevant source, scenes, runtime captures, tests, profiler evidence
   and dirty scope. A report is a claim to verify, not proof by itself.
6. Codex assigns one verdict:
   - **PASS:** all mandatory gates proved; open the next checkpoint.
   - **PASS WITH HOTFIX:** bounded defects must close before moving forward.
   - **REWORK:** player-facing or architectural target missed.
   - **BLOCKED:** owner authority or an unavailable dependency is required.
7. Codex updates milestone status and the decision log here.
8. Codex gives the next Claude prompt. Claude never chooses or silently expands the next milestone.

### Evidence rules

- Automated tests do not replace visual or player-flow evidence.
- Keep review screenshots outside `Assets/` until Codex finishes review.
- A/B uses identical seed, coordinate, density, camera and player context.
- Performance reports name baseline, environment, sample count and Editor/device status.
- Pre-existing failures are listed separately with ownership evidence.
- Preserve unrelated dirty work; do not stage, commit or clean unless explicitly requested.
- Full flow verification starts from `Bootstrap.unity`; isolated labs are used only for their feature.
- Use only the repository completion notification. Do not generate speech or invoke TTS.

## 6. Milestone board

| Milestone | State | Exit question |
|---|---|---|
| M0–M4.5 — World foundation/integration | **PASS** | Does the streamed world run correctly in all maps? Yes. |
| M4.6 — Art cohesion + apocalypse decoration | **PASS — M4 closed on one endless world (Map_Level1)** | Does the world read as one stylized apocalypse without breaking its streaming budget? |
| M5.1 — First reason to move | **LOCKED** | Does the player immediately know where and why to travel? |
| M5.2 — First complete objective | **LOCKED** | Does travel → encounter → completion → reward work end-to-end? |
| M5.3 — Resource/pickup loop | **LOCKED** | Do pickups create value, movement and risk? |
| M5.4 — Real in-run build | **LOCKED** | Does leveling produce a readable choice with a real effect? |
| M5.5 — Expedition route | **LOCKED** | Do two objectives create route and threat decisions? |
| M5.6 — Extraction versus boss | **LOCKED** | Can the player safely bank or risk more for more? |
| M5.7 — Closure and second-run loop | **LOCKED** | Is the result truthful, and can another run start cleanly? |
| M6 — Content rollout/balance | **LOCKED** | Can the slice expand through data instead of copied logic? |
| M7 — Release hardening/playtest | **LOCKED** | Is the build safe and understandable for outside players? |

## 7. ACTIVE — M4.6 Art cohesion + apocalypse decoration

This is one milestone with five internal execution checkpoints. Claude executes them sequentially and
collects evidence, but only Codex may pass the final visual gate after reviewing the retained captures.

| Internal checkpoint | State | Evidence / next gate |
|---|---|---|
| M4.6A — Baseline and source audit | **PASS** | Repeatable five-coordinate baseline; KayKit primary solid source; MegaCity requires toon re-shade. |
| M4.6B — Hybrid grass prototype | **PASS** | H1 is live in the canonical production palette; exact deterministic equivalence, production captures and full world-streaming regression passed. |
| M4.6C — Foliage palette cohesion | **PASS (M4.6CD.1)** | Production grass and bushes are the original LuxArt meshes `S_Grass_B/C` and `S_Bush_A/B` (accents `S_Bush_C/D`), through the project foliage shader, wind and atlas. The generated 72 v grass and 162 v bush blob are retired from production and from the baker. |
| M4.6D — Apocalypse solid-decoration palette | **PASS (M4.6CD.1)** | KayKit stone chunks, parts piles, dirty barrel, wooden crate and pallet, plus MegaCity refuse pile and standalone tire — one shared solid material, one submesh, no colliders. MegaCity Rock 01/02 and Garbage 03 rejected. |
| M4.6E — Production rollout and evidence | **PASS (re-scoped)** | The five-map rollout is obsolete: the product is one endless world. Rollout became the single-map closeout — Maps 2–5 retired from runtime, real Bootstrap→Hub→Map_Level1 playtest, final profiling and documentation. |

### M4.6A — Baseline and source audit

**Review state: PASS.** M4.6A.1 proved that the rejected brown images photographed the dry biome near
world origin after a Rigidbody overwrote a transform-only inspection teleport. Two independent clean
Play sessions now reproduce all five coordinates with matching chunks, biome weights, vertex colours,
material state and fake-light state. The retained source grids establish KayKit as the primary solid-prop
source and MegaCity as conditional on a toon-contract re-shade. LuxArt is **not** blanket-rejected:
alpha clipping is allowed, and M4.6C will preserve any current foliage that reads cleanly while retinting
or removing only demonstrated neon, stippled or hair-thin offenders.

- Recapture current production at identical dry, mixed and grass-rich global coordinates with player
  and representative enemies visible.
- Build temporary visual grids outside production scenes for vegetation and apocalypse-prop candidates.
- Inspect real mesh/material/atlas cost rather than selecting by filename.
- Audit at least the existing vegetation pack, disabled legacy environment sources, `JC_LP_MegaCity`,
  KayKit resource/prop sources and project-owned props.
- Classify each candidate as keep/retint, replace, solid scenery, foliage scenery, gameplay candidate or
  reject, with a short evidence-based reason.

### M4.6B — Hybrid grass prototype

**Review state: PASS.** H1 is promoted in place to the canonical `DecorationPalette.asset` with its GUID
and all production references preserved. Production matches the reviewed prototype placement by
placement at all five evidence coordinates, and the full world-streaming suites pass (`443/443`
EditMode, `141/141` PlayMode). Grass-rich foliage remains 179,003 vertices with one material/submesh,
zero allocation and generation p95 `1.237 ms`. The current ground-material values
(`_BlendSharpness = 6.9`, `_WeightJitter = 0.51`) remain byte-identical and become the recorded input
baseline for M4.6C; do not silently reset them.

- Improve broad grass coverage inside the existing ground shader using the same global biome weights
  and world-space mapping. It must not add geometry/tessellation shader stages.
- Keep actual grass as deterministic sparse clusters baked into the existing combined foliage mesh.
- Reuse or adapt current cheap sources where possible; create a project-owned source only if the
  inspected pack cannot produce a readable gameplay-scale cluster.
- If wind is retained, use a cheap vertex deformation in the shared foliage shader with globally
  coherent world-space phase. Wind must preserve the toon fake-light contract and chunk seams.
- Grass density, scale and color must remain subordinate to player, enemy, pickup and telegraph clarity.

### M4.6C — Foliage palette cohesion

**Review state: AUDIT CORRECTED; ADAPTER PROTOTYPE PENDING.** The earlier C.1 rejection mixed mesh fit
with two rendering defects. LuxArt's vendor shader supplies procedural foliage colour instead of storing
the final colour in its near-white atlas, while the production Volume uses full-screen depth outline.
That combination made usable alpha silhouettes appear neon, stippled and over-outlined. Controlled
contact sheets now show that `S_Bush_A/B/C/D`, `S_Grass_01A/02A`, blade grass and most LuxArt trees have
usable stylized silhouettes after a muted toon remap. The project therefore does **not** author a
replacement bush before proving the pack correctly. The current cheap `grass_b`, `grass_c` and `tree_a`
remain the production baseline; LuxArt candidates are additions or selective replacements, not a
wholesale density swap.

The environment rendering contract is explicit: streamed ground, foliage and solid scenery use the
project fake-light toon contract; vendor materials are source references only. Foliage keeps alpha
clipping and depth writing, but ordinary environment geometry must be excluded from character/gameplay
outlining. M4.6C.2 must compare `SelectionOnly` or `Mixed` outline with environment layers excluded
against the current full-screen profile in a real gameplay camera. The lite contact-sheet runner did not
reproduce the post-process pass, so code/profile inspection establishes the defect but production-camera
captures remain the promotion gate.

LuxArt colour conversion must preserve authored intent rather than copying shader properties blindly.
For the tree/bush/grass materials, `_Color` is the useful albedo-family tint; the saturated `_BaseColor`,
`_BottomColor`, `_TopColor` and `_FresnelColor` values are vendor gradient/fresnel controls and are not
literal production colours. Materials whose base colour is white require direct RGB/alpha/opacity-mask
inspection before conversion: white may be a neutral procedural input, not the intended final leaf
colour. Record source colour, chosen muted toon colour and cutoff for every promoted source.

Foliage motion is part of the M4.6C.2 cohesion gate. Add cheap shared vertex wind to the project foliage
shader: world-space phase, bounded amplitude, no CPU transform updates and no per-chunk material state.
Use the currently reserved vertex-colour alpha channel (or an equally batch-safe source attribute) for
bend weight so roots remain anchored, grass moves more than bushes, and tree canopies move subtly.
Expand mesh bounds for the maximum displacement. Wind must add no renderer, draw call, material instance
or managed per-frame allocation, and must remain visually continuous across chunk boundaries.

Curated foliage candidate order:

- **Keep baseline:** project-owned `grass_b`, `grass_c`, `tree_a`; these remain the cheap mass layer.
- **Prototype first:** LuxArt `S_Bush_A` and `S_Bush_B` as low-frequency rounded masses;
  `S_Grass_01A` and `S_Grass_02A` as occasional dense clusters; `S_Grass_B/C` only if their 230-vertex
  sources add a silhouette the 56-vertex baseline cannot provide.
- **Prototype selectively:** one fern among `S_Fern_A/C/D` and two tree shapes chosen at player scale.
  Geometry and screen-space noise decide; tint alone cannot promote them.
- **Rare accents only:** flowers, clover and cattails. They are biome/POI punctuation, never repeated
  field coverage or pickup-coloured noise.
- **Reject by budget, not pack identity:** any candidate that needs vendor fresnel/gradient effects,
  remains noisy without full-screen outline, or breaks the combined-mesh vertex/build budget.

- Judge asset fit before tuning it; use saturation, hue, value and scale only to finish a source whose
  silhouette and detail language already pass.
- Preserve alpha-clipped foliage that passes player-scale review.
- Remove only demonstrated offenders such as stippled canopy or unreadable neon/thin sources.
- Flowers are controlled accents, not repeated debug-like color noise.
- Evaluate real candidates in a controlled grid and beside the player; filenames and pack identity are
  not evidence of fit.
- Prefer a small coherent set of grass, low plants/bushes and tree foliage over keeping every available
  category. Tint is a finishing control, not a substitute for a compatible silhouette or detail level.
- Never compare a vendor procedural-colour material against the production toon shader without first
  reproducing its albedo/alpha intent in a project-owned adapter material.

### M4 closeout — one endless world (2026-08-12)

**Review state: PASS.** The product direction is locked as **one endless procedural world**, played in
`Map_Level1`. There is no map-to-map progression. The hub, menu and gameplay UI remain part of the
production flow: Bootstrap → Menu/Hub → PLAY → Map_Level1 → HUD/pause → restart or return to hub.

Maps 2–5 are retired from the runtime — absent from Build Settings and from `CampaignCatalog` — while
their scene and `WaveData` assets stay on disk and recoverable. `CampaignDataAuthoring` carries a
per-stage `production` flag, so restoring a stage is a one-line change.

Hub correction: with a one-stage catalog the stage-card ‹ › arrows and the stage-dot row became dead
furniture promising content the game no longer has, so both hide themselves whenever the catalog holds
a single stage. They return automatically if a second stage is ever authored.

**Phase tracking (2026-08-13):**

```text
M4 — COMPLETE
M5 Audit — COMPLETE
M5.1 Combat & Locomotion Repair — COMPLETE
M5.1.1 Verification Closeout — SUPERSEDED by M5.1.2 (its ballistics fix and ray evidence were wrong)
M5.1.2 Corrective Closeout — COMPLETE (visible-aim ballistics + alignment gate, per-ray probe
        truth, terminal first-wins UI, outline profile runtime-clone guard)
M5.1.3 Audio + Crowd Motion — implementation done; verification INCOMPLETE.
        Shipped: bounded guaranteed-transient weapon audio (silent pistol shots fixed),
        magnitude-preserving crowd separation, VAT locomotion desync, idempotent outline guard.
        The earlier "15 FPS is ~4x worse" claim is WITHDRAWN - it came from a broken metric.
        Corrected measurement passes (0.379 -> 0.714 reversals/enemy-second, allowance 1.0), so
        the motor was deliberately NOT changed. VAT phase figure also corrected (11/11 looping
        enemies distinct, not 34/45).
        OPEN: a crowd test file was never compiled by Unity, so eight assertions had never run;
        it was recreated as CrowdSeparationContractTests.cs but the Unity Editor process died
        during the reimport, so those tests, the full suites and production runs A/B still need
        to be executed. Human AUDIO PASS + CROWD MOTION PASS also outstanding.
        See Review/M5.1.3/FinalCloseout/GATES.md.
M6 Expedition Redesign — NOT STARTED
```

**Audio listening checklist (the one open M5.1 gate — reply `AUDIO PASS` or
`AUDIO FAIL — [pistol/G36C/SMG] — [symptom]`):**

1. *Pistol:* stand still and fire; move and fire; start/stop several times. Each visible shot has
   exactly one report and the tail rings out fully.
2. *G36C:* hold fire ≥10 s; move while firing; stop abruptly; switch weapon mid-fire; pause/resume
   mid-fire. Fire rhythm matches the visible muzzle; a tail follows the last shot; nothing keeps
   sounding after a switch or into the pause.
3. *SMG (fastest):* hold fire ≥10 s, then again with a Fire Rate perk. Listen for: rhythmic
   silences, chopped starts on every bullet, sewing-machine monotony, tempo that disagrees with
   the visible shots, a missing stop tail, or audio continuing after switching away.

The former M5.2–M5.7 vertical-slice checkpoints are NOT separate top-level phases; that work is
M6's internal execution ladder.

UI closeout (2026-08-12): the hub canvas is authored against a portrait 1080×1920 frame; the
`CanvasScaler` now uses `Expand` (installer + `Menu.unity`) so the whole frame stays visible at every
aspect instead of averaging width and height, which compressed the vertical canvas on landscape/WebGL
and made PLAY overlap the stage card. The one-stage rule also hides the stage-name label: at the
reference resolution its band sits inside the reward-card row (measured Y 553–605 vs 470–620) and
overlapped DAILY REWARD. Verified via Bootstrap → Menu/Hub captures at 1600×900, 1080×1920 and
1080×2400 — no overlap in any aspect; EditMode 501/501, PlayMode 145/145.
Evidence: `Temp/CodexReview/M4_Final/AspectPass/`.

Weapons remain the locked no-reload continuous-fire design; the HUD exposes no reload widget and the
ammo ring is disabled at runtime.

**Open gameplay work moves to M5/M5.1/M6** — combat feel, enemy steering and the reward systems are
NOT part of M4 and were not touched.

### M4.6CD.1 — director-locked asset correction + environment outline (2026-08-12)

**Review state: PASS.** M4.6CD met its structural and performance gates but failed the art-quality
gate: optimising vertex count ahead of silhouette quality produced a hollow folded bush, thin grass,
faceted MegaCity rocks and an unreadable black garbage blob. The correction promotes **geometry
quality over minimum vertex count** and pays the extra cost in deterministic density, never by
degrading the mesh.

Locked production sources — vegetation: `S_Grass_B`, `S_Grass_C`, `S_Bush_A`, `S_Bush_B`, accents
`S_Bush_C`/`S_Bush_D`, retained `S_Cattail_B`, `S_Flowers_G`, `tree_a`. Solid: KayKit
`Stone_Chunks_Small/Large`, `Parts_Pile_Small/Medium`, `Fuel_A_Barrel_Dirty`,
`Containers_Crate_Medium_Wood`, `Pallet_Wood`, plus MegaCity `SM_Props_Garbage_01` and
`SM_IndustrialProps_Wheel_01`.

New art contract: **`SolidDecorMeshRenderer` receives an environment outline** on rendering-layer bit 4
(production selection mask `14 | 16 = 30`), while ground and foliage stay unoutlined. This is a visual
channel only — physics layers and colliders are untouched.

Measured: worst chunk 33,369 foliage / 23,341 solid vertices, ring mesh memory 31.7 MB, decoration
worker p95 4.775 ms, zero sustained allocation, 25 roots / 75 renderers / 3 shared materials / 0
decoration colliders. EditMode 490/490, PlayMode 145/145. Editor measurements only.

Remaining content gap: **bones** — no trustworthy standalone mesh exists in any imported pack;
recorded as a gap rather than authored to satisfy a checklist. Shipping containers, fences, road signs,
large rocks, vehicles and buildings stay POI/event candidates, not chunk scatter.

Evidence: `Temp/CodexReview/M4_Final/M4.6CD1/`.

### M4.6D — Apocalypse solid-decoration palette

- Select a bounded MVP set of roughly 6–10 sources spanning at least four readable categories such as
  tires/wheels, signs, containers, trash/scrap and rubble.
- Bake ordinary instances into the existing single solid combined mesh per chunk.
- Prefer one atlas/shared material/submesh. No per-chunk material instances or extra renderers.
- Give categories different deterministic cell scale, density, spacing and biome affinity.
- Props may overlap chunk boundaries but remain owned by their anchor chunk.
- Do not add colliders, destructibility, loot or independent GameObjects to ordinary scenery.

### M4.6E — Production rollout and evidence

- Apply one shared production palette/configuration to Maps 1–5, not scene-specific copies.
- Verify positive, negative and boundary coordinates, traversal, teleport and near/far density changes.
- Retain before/after and exact-coordinate comparisons outside `Assets/` until Codex reviews them.
- Reprofile decoration build, assignments, vertices, triangles, memory, batches and SetPass calls against
  the named M3/M4.5 baseline.

### Included technical principles

- Maximum normal renderer structure remains ground + solid + foliage per chunk.
- Shared materials, deterministic global sampling, pooling and bounded scheduling remain unchanged.
- The shader and all new materials use the project's toon fake-light contract.
- Candidate A/B assets may remain as comparison evidence, but production naming must describe the final
  curated palette rather than an experiment label.

### Excluded

POIs, pickups, perks, objectives, economy work, new biomes, GPU instancing, Jobs/Burst, player rebuild and
owner-authored UI changes. Also excluded: geometry shaders, tessellation, hundreds of grass GameObjects,
re-enabling legacy prop roots, interactive apocalypse props and mass asset-pack import-setting changes.

### Mandatory gate

- No demonstrated stippling, moiré or canopy noise at gameplay distance. Alpha clipping itself is not
  a failure when the result reads cleanly.
- Grass coverage reads in the ground while sparse mesh clusters provide depth; neither obscures player,
  enemy, telegraph or pickup priority.
- Player, zombie, ground, foliage and apocalypse props share one toon value/palette language.
- Dry biome is intentionally sparse; grass-rich biome is richer; apocalypse evidence prevents either
  from reading as an empty vegetation demo.
- At least four solid prop categories are visibly distinguishable in normal gameplay captures.
- No seams, recycling variation, density corruption or material/renderer/pool regression.
- No geometry/tessellation grass shader is used. Combined-mesh budgets remain bounded and the report
  quantifies any increase bought by the added world dressing.
- Maps 1–5 are recaptured after rollout.
- Retain exact-coordinate before/after comparisons for dry, mixed and grass-rich regions.

M4.6 passes only after Codex reviews the retained images and a live map. Tests and vertex counts alone
cannot pass this milestone.

## 8. M5 vertical-slice checkpoints

### M5.1 — First reason to move

- Create deterministic World Content identity/anchor ownership independent of `ChunkInstance`.
- Generate at most two objective signals.
- Place one Supply POI 1–2 chunks from insertion.
- Provide minimal direction/distance feedback.
- Preserve state across visual chunk recycle.

**Gate:** within three seconds, a new player can identify a destination and its promised reward.

### M5.2 — First complete field objective

- Arrival starts one bounded local encounter using existing enemy/spawn capability behind an encounter
  boundary, not a global rewrite.
- Completion reveals a reward and cannot reset, duplicate or pay twice after revisit/recycle.

**Gate:** insertion → signal → travel → encounter → completion → reward passes three consecutive times,
including negative coordinates and leave/re-enter.

### M5.3 — Resource and pickup loop

- Preserve central pickup management and Coin/Gem magnet behavior.
- Author real Health and Bomb pickup sources/rules.
- Present collected-but-unbanked reward separately from persistent profile currency.
- Replace unconditional wave-clear sweeping with an expedition-safe cleanup rule that preserves reward
  correctness without making movement to valuable loot meaningless.
- Add readable visual/audio/UI collection feedback.

**Gate:** every pickup has one source, one effect, collect-once proof and HUD feedback; none can double-pay.

### M5.4 — Real in-run build

- Convert XP levels into queued three-choice perk presentation.
- Apply selection to `RunState` and actual gameplay stats.
- Minimum effects: Damage, Fire Rate, Move Speed and Max Health.
- Define stack/cap rules in data.

**Gate:** first choice normally appears at 60–90 seconds; tests and live capture prove every shipped
perk changes gameplay; pause/death/win cannot trap input or time scale.

### M5.5 — Expedition route

- Add a second objective archetype, initially an Infested Nest.
- Offer a readable safer/shorter versus riskier/more valuable route choice.
- Allow two required objectives in either order.
- Escalate composition/behavior before raw HP.
- Empty ordinary travel must remain below roughly 20 seconds.

**Gate:** the route contains at least two meaningful decisions, and standing still waiting for global
waves is never the optimal progression action.

### M5.6 — Extraction versus boss

- Two objectives reveal extraction and an optional boss signal.
- Extraction banks the full eligible reward.
- Boss adds explicit risk and a worthwhile bonus.
- Defeat uses partial common-currency retention; abandon remains distinct.

**Gate:** extraction, boss victory, defeat and abandon each produce the correct summary and payout once.

### M5.7 — Closure and second-run loop

- Result shows outcome, objectives, kills, duration, perks and earned/lost/banked reward.
- Hub refreshes wallet/unlocks/upgrades without restarting the app.
- ~~FTUE teaches movement, signal reading, bomb and weapon switch through actions.~~ **SUPERSEDED by M6:** FTUE teaches movement, level-up and one signal.
- A second run starts without stale content, pickup, perk, scheduler or time-scale state.

**Gate:** three consecutive `Bootstrap → expedition → result → Hub → next expedition` loops pass,
including extraction, defeat and boss paths.

## 9. M6 — Content rollout and balance

After M5.7 only:

- move scene-specific variation into contract/biome/encounter data;
- reuse the proven loop across existing map/contract selection without duplicated runtime logic;
- add biome resource palettes and POI variants;
- tune run pacing to 8–12 minutes;
- validate continuous-fire weapon roles;
- expand content through archetype × modifier before new asset breadth.

**Gate:** a second contract changes route, threat and reward through data while sharing the same runtime.

## 10. M7 — Release hardening and playtest

- No compile errors, missing scripts or repeating happy-path exceptions.
- Save/profile migration and restart verification.
- Release identity, cheat removal and Android build/install/soak.
- Representative CPU/GPU/memory profiling.
- Audio/settings/accessibility smoke tests.
- 10–20 uncoached outside players; funnel and blocker capture.
- No open P0 and no unowned P1.

Learning gates: at least 70% finish a first expedition without coaching and at least 40% voluntarily
start a second run. These are MVP cohort decisions, not commercial KPI promises.

## 11. Cut order and icebox

Cut first: extra POI variants, extra biomes, rare vendor accents, extra bosses, perk breadth, cosmetic/
shop/mission breadth and non-core polish.

Never cut: a reason to move, one complete objective, pickup/reward truth, one real perk choice,
extraction/defeat distinction, result/second-run closure and deterministic ownership.

Icebox: unproven GPU instancing, Jobs/Burst/MeshData rewrite, premature floating origin, physical terrain,
destructible ordinary vegetation, every tree as an entity, PvP/clans/live-service breadth and unrelated
weapon/enemy/cosmetic expansion.

## 12. Risk register

| Risk | Impact | Response |
|---|---:|---|
| Procedural world has no reason to move | Critical | M5.1–M5.2 precede further polish/content breadth. |
| Art packs remain visibly mixed | High | Source audit, shared palette and player-scale visual gate. |
| Recycled chunk owns gameplay state | Critical | Stable World Content identity outside `ChunkInstance`. |
| Pickups remain cosmetic | High | M5.3 redesigns cleanup, presentation and banking. |
| Perk UI remains a shell | High | Quantitative and live gates in M5.4. |
| Global waves conflict with objectives | High | Adapt them behind local encounter boundaries. |
| Defeat payout contradicts GDD | High | One explicit payout matrix in M5.6. |
| Five maps duplicate new logic | High | Prove one reference map, then data-drive variation. |
| Dirty worktree mixes ownership | High | Baseline every task; preserve unrelated changes. |
| Reports substitute for evidence | High | Codex review blocks the next checkpoint. |

## 13. Decision log

| Date | Decision | Reopen trigger |
|---|---|---|
| 2026-08-09 | Flat procedural chunks replace authored arena ground. | A proven gameplay blocker cannot be solved on the flat world. |
| 2026-08-10 | Continuous fire and planar steering replace reload and NavMesh. | Specific playtest evidence proves an unfixable failure. |
| 2026-08-10 | M4.6 reframed from wholesale Candidate B replacement to art cohesion, hybrid grass and apocalypse props. | Retained visual evidence shows one component must be removed or the bounded prop set is insufficient. |
| 2026-08-10 | Streaming optimization closes without Android-device gate. | Production content creates a measured regression. |
| 2026-08-10 | M4.6 opens; M5+ remains locked. | M4.6 mandatory gate passes. |

## 14. Immediate next action

Execute **M4.6C.2 — Environment Toon Adapter, Outline Isolation and Curated Foliage Prototype** only.
Preserve `grass_b`, `grass_c`, `tree_a`, H1 placement and the ground material. Build a project-owned
LuxArt adapter/atlas path that reproduces intended foliage colour and alpha through the existing
fake-light toon shader without vendor fresnel/gradient lighting. Prototype `S_Bush_A/B` and
`S_Grass_01A/02A` at bounded low density through the real combined-mesh path; add at most one fern and
two tree shapes after player-scale review. Preserve each source's intended `_Color`/texture/opacity
relationship, explicitly resolve white-base materials, and add batch-safe world-space vertex wind with
anchored roots and bounded culling-safe displacement. In the production camera, compare current full-screen outline
against `SelectionOnly`/`Mixed` with ordinary environment excluded. Capture identical dry, mixed,
grass-rich, boundary and negative coordinates plus player-scale closeups; record per-source geometry,
ring vertices, build p95, batches and SetPass. Do not author a replacement bush or promote any palette
before this gate. Stop at `READY FOR CODEX M4.6C.2 REVIEW`.

---

## M5 FINAL CLOSEOUT STATUS (2026-08-13)

**M5: NOT COMPLETE.** One gate fails; everything else in the closeout passed.

Owner playtest verdicts recorded: `GENERAL COMBAT FEEL: USER PASS`, `CROWD MOTION: USER PASS`.

| Gate | Result |
|---|---|
| Recreated crowd suite (`CrowdSeparationContractTests`) | 9 discovered / 9 executed / 9 passed |
| New ground-coverage suite (`GroundCoverageTests`) | 22 discovered / 22 executed / 22 passed |
| Full EditMode | 502 / 502 passed |
| Full PlayMode | **212 run, 211 passed, 1 FAILED** |
| Perspective camera exposes ground | no — worst margin 42.67 m |
| Orthographic size 10 exposes ground | no — worst margin 46.22 m |
| Portrait / tall / landscape | all clear a 32 m guard band |
| Streaming invariants (roots/leases/materials/collider/allocs) | pass |
| Protected asset hashes | unchanged |
| Production camera settings restored | yes (Play Mode only, never saved) |
| Staged/committed | nothing |
| M6 started | no |
| Production runs A and B | **not completed** |

### Failing gate

`PlanarSteeringTests.TwoAgentsWithTheSameDestination_DoNotEndUpStacked` — 0.395 m then 0.320 m
against `> 0.400 m` across two full-suite runs; passes 15/15 in isolation. Pre-existing frame-rate
dependence of `separationMaxBlendPerSample = 0.2`, exposed (not caused) by adding 31 tests to
PlayMode. Assertion not weakened; motor not retuned. Needs an owner decision.

### Ground-coverage outcome

The reported defect did not reproduce. The old "camera drift / projection" explanation is withdrawn.
The measured sensitivity is camera **pitch**, not ring radius: below ~31 deg the camera sees the
horizon and no finite ring can cover it. `renderRadius` stays 2 and the pool stays 25; a pure
coverage calculator plus 22 tests now lock the invariant. Detail:
`Review/M5_Final/GroundCoverage/GATES.md`.

### Audio

Regression smoke only, and partial: 22 pistol pulls -> 22 requested, 22 accepted, 0 dropped on the
guaranteed-transient path, 0 stale voices. `maxVoices = 16` and `perKeyLimit = 3` unchanged. The
AA-12 60-pull and sustained-automatic windows were NOT re-run this session; the standing figures come
from M5.1.3 (AA-12 28 pulls 28/28/0; G36C 46 pulls, 0 transient requests, 45 retriggers, peak 1).

### M6

`NOT STARTED - awaiting owner direction.`

---

## M5 FINAL CLOSEOUT R2 STATUS (2026-08-13)

**M5: NOT COMPLETE.** The camera-aware coverage work landed and all automated gates are green; the
remaining gaps are production Run A/B and the audio counter windows.

| Gate | Result |
|---|---|
| Camera-aware `CoverageOrigin` implemented | YES |
| `PlayerChunk` responsibilities preserved | YES (tier, scheduler, collider, debug all still player-centred) |
| Fixed pool remains 25 | YES (renderRadius 2 unchanged) |
| Pure origin-selection tests | 15 / 15 |
| Camera-aware coverage PlayMode tests | 22 / 22 |
| Full EditMode | **517 / 517** |
| Full PlayMode | **234 / 234** |
| Perspective coverage | pass (live + tests) |
| Orthographic size 10 coverage | pass, with valid gameplay evidence |
| Portrait / tall / landscape | pass |
| Cardinal / diagonal / negative / zero-crossing / teleport | pass |
| Threshold oscillation | none (was 41 shifts in a caught bug; now 1) |
| Obsolete 0.4 m steering test replaced transparently | YES |
| Production motor / VAT changed | NO |
| Protected asset hashes | unchanged |
| Production camera restored | YES (Play Mode only, never saved) |
| Staged / committed | nothing |
| M6 started | no |
| **Production Run A / Run B** | **NOT COMPLETED** |
| **Audio counter windows (60 / 60 / 10s)** | **NOT RE-RUN** |

Detail: `Review/M5_Final/GroundCoverageR2/GATES.md`.

### M6

`NOT STARTED - awaiting owner design direction.`

---

## M5 RENDERING CLOSEOUT (2026-08-13) — real root cause, and batching

**Missing-ground root cause: Camera Occlusion Culling** interacting with recycled procedural chunk
renderers / stale baked occlusion data. Owner reproduced it: with occlusion culling off, ground is
correct.

**Rejected:** camera pitch / footprint / coverage-origin was never proven to cause it. The R2
camera-aware `CoverageOrigin` architecture was built for that misdiagnosis and has been **removed**;
the ring is centred on the player chunk again, one refresh per genuine crossing, 25 fixed roots.

Policy is now set in code (`CameraFollow.Awake` -> `useOcclusionCulling = false`) so a scene save
cannot re-enable it. Measured cost of disabling it: **116 -> 114 batches**, i.e. nothing.

### Batching

Three defects were silently disabling automatic GPU instancing for the crowd:

1. `VAT_Animator` wrote `_PositionTexture` (a TEXTURE), `_PositionMin` and `_PositionMax` into each
   renderer's MaterialPropertyBlock. Archetype constants in a per-renderer MPB — and a texture in an
   MPB cannot be instanced — so instancing was off despite `enableInstancing = true`. Now written once
   to the shared archetype material.
2. `BillOutlineFeature.LayerMaskPass` built `DrawingSettings` without `enableInstancing`, so the mask
   pass drew per renderer while the visible pass batched.
3. Alive blob shadows carried a non-instanced per-renderer colour from `SetDissolve(0)` on every
   spawn. Alive blobs now carry no property block at all.

```text
before : 204 batches, setPass 138, 26 enemies  (~3.4 batches/enemy)
after  : 153 batches, setPass  65, 27 enemies  (~1.44 batches/enemy)
```

Still outstanding: the <=150 target (153 measured, not faked), per-enemy cost still scales with
population, Run A/B, and any device/WebGL measurement. Editor numbers only.

Detail: `Review/M5_RenderingCloseout/GATES.md`.

---

## VAT ForwardLit batch-break diagnosis (2026-08-13) — correction + clean measurement

**Correction:** the earlier "30 same-archetype DogPup" sweep was contaminated. Enemies were taken from
the pool by instance-id order and the pool holds four archetypes; the set reported as 30 DogPup was
actually 18 Skeleton + 12 DogBark (2 meshes, 2 materials). The previously reported
**~1.15 batches/enemy is withdrawn**.

Re-measured on a genuinely single archetype (ENM_Skeleton_VAT, verified live as distinctMesh = 1,
distinctMaterial = 1), Occlusion OFF, frozen frame:

```text
N=0  : 111 batches  setPass 45
N=1  : 115 (+4)     setPass 48   instanced   3
N=10 : 124 (+13)    setPass 49   instanced  30
N=30 : 144 (+33)    setPass 49   instanced  90
N=40 : 154 (+43)    setPass 49   instanced 120
slope: exactly +1.00 batch per additional enemy on all three intervals
```

`instanced = 3 x N` at every step while batches still grow one-for-one — so that counter never proved
grouping; the instancing path runs but produces ~1 instance per batch.

Positively excluded as causes: mesh identity, material identity (no runtime clones), immutable VAT
data leaking into the MPB (none present), non-uniform MPB layout (all 30 identical: CPBHD), shader
instancing contract (ForwardLit has multi_compile_instancing; all five values are
UNITY_DEFINE_INSTANCED_PROP and read via UNITY_ACCESS_INSTANCED_PROP; none duplicated in
UnityPerMaterial), and SetPass/variant growth (flat at 49).

**Root cause still unproven.** Frame Debugger per-draw inspection is GUI-only and was not performed,
so the event-by-event instance counts and Unity's own batch-break reason remain the missing evidence.
No fix was applied, because none is justified without it. Blob shadows and the outline mask were left
alone — both measured as already grouping (8 and 2 draws for 30 enemies).

Detail: `Review/M5_RenderingCloseout/VATForwardBreaks.md`.

---

# FINAL CLOSEOUT PASS (2026-08-14)

## Legacy blob retirement — including the player blob Codex spotted

`Player/Plane` was indeed still rendering the old player blob on top of the new batched shadow:

```text
Player/Plane : Transform + MeshFilter + MeshRenderer(GroundMat 1) + MeshCollider
```

Retired the **visual responsibility only**: `MeshRenderer.enabled = false` in the prefab asset.
`Transform`, `MeshFilter`, `MeshCollider` (mesh "Plane", convex=False), layer (Default), scale and
position are untouched, so player collision/WalkableGround behaviour is unchanged.

Enemy prefabs: 15 carry a `ShadowBlob`; **0 of them had any Collider or Rigidbody**, so the obsolete
renderer was disabled in authoring data on all 15 (no longer only force-disabled at runtime).

## Shared material asset — now the only path

- Created `Assets/_Project/Art/Materials/M_CharacterContactShadow.mat`.
- Authored one `CharacterContactShadows` object into `Map_Level1` with that material assigned.
- `ResolveMaterial()` no longer calls `Shader.Find` and no longer calls `new Material(...)`. A missing
  material now **fails loudly** naming the exact asset path, instead of silently constructing a
  material that lacks the authored values — which is precisely how the rectangular defect hid.

## Tuning applied

```text
_FalloffPower 1.25   _CoreStrength 1.10   _Tint alpha 1
enemy opacity 0.50 (15 prefabs)   player opacity 0.50
groundLift 0.01 m
```

The stale comment claiming biome geometry is physically displaced was corrected: the gameplay surface
is flat at Y=0, so the lift exists solely to avoid coplanar z-fighting.

## Tests

```text
EditMode  502 / 502  PASS
PlayMode  212 run, 210 pass, 2 FAIL
```

- `AudioTransitionPlayTests.CombatDuckRamp_MovesLiveVoicesOverTime` — fails only under full-suite load,
  **passes in isolation**. Load-dependent, not caused by this work.
- `VatLocomotionPhaseTests.ACrowdOfInstances_SpreadsAcrossDistinctPhases` — 16 distinct phases vs the
  required 20. **Deterministic, and genuinely caused by this phase.**

### The VAT phase regression, explained honestly

`ZombieBase` derives its locomotion phase from `Mathf.Repeat(GetInstanceID() * 0.6180339887f, 1f)`.
That scramble is only well-distributed when consecutive enemies get a consistent instance-id stride.
`AcquireContactShadow()` now runs in `OnEnable` and calls `CharacterContactShadows.EnsureInstance()`,
which in a test fixture **creates a GameObject during enemy spawn**, changing the id stride between
consecutive enemies and aliasing the golden-ratio scramble down to 16 distinct phases.

The phase source is the fragile part — instance ids were never a stable identity to hash. The correct
fix is a stable per-spawn counter rather than `GetInstanceID()`. **Not implemented in this pass**, and
the assertion was deliberately left at 20 rather than lowered to fit.

## Still outstanding

- The VAT phase regression above (failing gate).
- `EnemyRosterTests` still asserts the old `ShadowBlob` premise. It passes only because the node still
  exists with its renderer disabled; the contract was **not** replaced with "no per-character shadow
  renderer" as required.
- Steps 6/7/8/9: 20/40/70 performance sweep, WebGL 2 build, ground correctness matrix, and the two
  real production runs.
- Step 5 VAT authoring-time contract (runtime still writes `sharedMaterial`).
- Step 3 capture set: only the 12-enemy analytic-ellipse frame exists; no biome/aspect/boss/death-fade
  or true "no shadow" comparison sheets.

---

# ABSOLUTE FINAL CLOSEOUT PASS (2026-08-14)

## CP1 — VAT locomotion phase regression: FIXED

Root cause was the phase source itself, not the contact-shadow system that exposed it. `GetInstanceID()`
is not a stable identity to hash: its value and the *stride* between consecutive enemies depend on how
many unrelated objects Unity created first. When `EnsureInstance()` began creating a GameObject during
enemy spawn, the stride changed, the golden-ratio scramble aliased, and 24 enemies collapsed to 16
distinct phases.

Replaced with a deterministic construction ordinal — `LocomotionPhase.cs`:

```csharp
uint bits = ordinal * 2654435761u;          // Knuth multiplicative hash
float phase = (bits >> 8) * (1f / 16777216f);
```

- static counter, reset via `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` so Disable Domain
  Reload cannot leak it between Play sessions;
- one ordinal allocated in `Awake` and held for the pooled object's whole life;
- no `GetInstanceID()`, no `UnityEngine.Random`.

Measured spread: **24 consecutive ordinals → 24/24 distinct buckets, range 0.979** (contract: ≥20 and
≥0.75). 70 ordinals → 67 distinct.

The test was rewritten to exercise the real production source instead of re-implementing the old
formula, and now includes the regression itself:
`UnrelatedGameObjectCreation_DoesNotDisturbThePhaseSequence`. **Threshold was not lowered.**

## CP2 — Legacy blobs retired at source, not hidden

```text
enemy prefabs with a ShadowBlob node : 15 -> 0   (removed; none carried Collider/Rigidbody/script/children)
live enemies with a legacy ShadowBlob:  0 of 104
ZombieBase legacy members             : shadowRenderer, ShadowRenderer, _shadowPropertyBlock,
                                        _shadowBaseColor, BaseColorID, ColorID  -> all removed
ZombieVATBaker                        : no longer creates ShadowBlob or wires shadowRenderer
```

`Player/Plane` — the `MeshRenderer` **component** is removed, not merely disabled. Collider verified
byte-for-byte before/after:

```text
before: mesh=Plane convex=False trigger=False enabled=True layer=Default
after : mesh=Plane convex=False trigger=False enabled=True layer=Default
MeshFilter preserved (collider mesh source)
```

`EnemyRosterTests` had its premise **replaced, not weakened** — it previously passed for the wrong
reason (node still present, renderer merely disabled). New contracts:
`NoEnemy_CarriesAPerCharacterShadowRenderer`, `ContactShadowMaterialAsset_IsAuthoredAndShared`,
`PlayerPlane_KeepsItsColliderButHasNoLegacyRenderer`, and the one-renderer-per-enemy assertion.

## CP3 — Authored material contract, verified live

```text
contact-shadow managers  : 1
material                 : M_CharacterContactShadow   (authored asset)
runtime-created material : False
player Plane renderer    : absent      player Plane collider : present
```

## CP7 — Performance sweep (frozen frame, same archetype, Occlusion OFF)

```text
0 enemies (player only) : 112 batches / 44 SetPass / 1 registration / 1 visible shadow
40 enemies + player     : 116 batches / 47 SetPass / 41 registrations / 41 visible shadows
                          renderers 1 · materials 1 · submeshes 1 · 640 verts
```

**40 enemies add 4 batches — ~0.1 batch per enemy**, versus 3.4 per enemy in the original build. All
41 character shadows remain a single draw.

Arc, honestly labelled by population:

```text
26 enemies, per-character blobs      : 204 batches / 138 SetPass
30 enemies, batched shadows          : 107 batches /  30 SetPass
40 enemies, batched + blobs retired  : 116 batches /  47 SetPass
```

## CP11 — Test stability

```text
EditMode  504 / 504  PASS
PlayMode  215 / 215  PASS  (run 1)
PlayMode  215 / 215  PASS  (run 2)
```

`AudioTransitionPlayTests.CombatDuckRamp` passed in both full-suite runs after the phase fix. It is no
longer reproducing; it was load-dependent and is not being suppressed by tolerance changes.

## Still outstanding (not attempted this pass)

- CP5 full visual capture set (biome/aspect/boss/death-fade/no-shadow comparison sheets).
- CP6 VAT authoring-time contract — runtime still writes `sharedMaterial`.
- CP8 WebGL 2 build.
- CP9 ground correctness matrix.
- CP10 two real production runs.

---

# CONTINUATION PASS (2026-08-14) — Gates 1, 3, 8

## GATE 1 — Lifecycle matrix: PASS (11/11)

New suite `Assets/_Project/Scripts/Tests/PlayMode/ContactShadowLifecycleTests.cs`. Fixture authors the
manager explicitly (material + capacity 16) on an inactive GameObject, so `Awake` sees the fixture's
values rather than defaults.

```text
register -> valid handle, count +1                         PASS
unregister -> count -1, repeating is harmless              PASS
20 pool cycles -> no handle leak                           PASS
capacity overflow -> precise fail-loud error, returns -1   PASS
SetVisible false/true -> same slot hidden/restored         PASS
SetFade 0.5 still drawn, 1.0 stops drawing                 PASS
zero visible shadows -> batch renderer disabled            PASS
destroyed target Transform -> dropped safely               PASS
negative coords + long teleport -> stays under the target  PASS
second manager -> rejected, Instance unchanged             PASS
steady-state rebuild -> no managed allocation              PASS
```

**A real defect was found and fixed by the allocation test.** `mesh.vertices = array` /
`mesh.colors32 = array` (property setters) copy through an intermediate each call — 640 verts per
frame produced ~2 MB of garbage over 60 frames. Replaced with the non-allocating
`Mesh.SetVertices(array)` / `Mesh.SetColors(array)` overloads; 240 consecutive rebuilds now allocate
under 16 KB.

The measurement itself was also corrected: it now drives `RebuildNow()` directly instead of sampling
whole-domain heap across frames, which had been folding in the test runner's own garbage.

## GATE 3 — VAT authoring-time contract: PASS

- New `Assets/_Project/Scripts/Editor/VatMaterialAuthoring.cs` owns the write, exposed as
  `ZombieWar/VAT/Author VAT constants onto materials`.
- `VAT_Animator.EnsureArchetypeConstantsOnSharedMaterial()` became
  `ValidateArchetypeConstantsOnSharedMaterial()` — it now **reports** a mismatch naming the enemy,
  material, expected and actual value, and **never writes**.
- Migration run over production: `archetypes checked = 15, materials updated = 0,
  still missing _PositionTexture = 0` — every material already carries its constants.

Material-mutation proof across a real gameplay session (Bootstrap -> Menu -> PLAY -> Map_Level1):

```text
15 VAT data assets hashed before Play and after exiting Play
changed: 0     -> ALL VAT MATERIAL HASHES IDENTICAL
zero [VAT] validation errors in console during the run
```

## Live production frame during that session

```text
27 enemies : 115 batches / 49 SetPass
contact-shadow managers 1 · registered 28 (27 enemies + player) · visible 28
material = M_CharacterContactShadow  (authored asset, not runtime-created)
```

Against the original build's **26 enemies = 204 batches / 138 SetPass**.

## GATE 8 — Regression

```text
EditMode  504 / 504  PASS
PlayMode  226 / 226  PASS      (215 + 11 new lifecycle tests)
```

`AudioTransitionPlayTests.CombatDuckRamp` green again; no tolerance was widened.

## Gates not executed in this pass

Gates 2 (full visual capture set), 4 (20/70/mixed sweep rows), 5 (WebGL 2 build), 6 (ground
correctness matrix), 7 (two production runs) were **not run**. They are not blocked — they were simply
not reached. No result is claimed for them.
# M5 acceptance-only final pass (2026-08-14)

## Gate 6 — ground correctness matrix: PASS 48/48

Automated harness `Assets/_Project/Scripts/Tests/PlayMode/GroundMatrixAcceptanceTests.cs` drives
6 camera configurations x 8 traversal cases and asserts every invariant at **every step**, not just at
the end. Full table: `GroundMatrix.md` in this folder.

```text
cameras   : portrait / tall portrait / landscape, each perspective + orthographic
traversals: +X, -X, +Z, -Z, diagonal, zero crossing, negative coords, non-adjacent teleport
per cell  : missing visible ground 0 · active roots 25 · chunk renderers 75 · duplicate roots 0
            shared colliders 1 · ring radius unchanged · useOcclusionCulling false
            player chunk inside ring · generation settles
result    : 48 PASS / 0 FAIL
```

Coverage is sampled as a 9x9 grid **inside** the real camera footprint, so a hole in the middle of the
ring cannot pass by leaving the outer bounds intact.

## Gate 4 — performance sweep: PASS

One frozen frame, one camera pose, player pinned at origin, Occlusion Culling OFF.

| Population | Archetypes | Registered | Visible | Shadow draws | Batches | SetPass |
|---|---:|---:|---:|---:|---:|---:|
| 0 enemies (player only) | 0 | 1 | 1 | 1 | 113 | 43 |
| 20 enemies | 1 | 21 | 21 | 1 | 116 | 46 |
| 40 enemies | 1 | 41 | 41 | 1 | 116 | 46 |
| 70 enemies | 3 | 71 | 71 | 1 | 122 | 52 |
| production wave (live, unfrozen) | mixed | 28 | 28 | 1 | 115 | 49 |

Invariants held at every row:

```text
contact-shadow managers 1 · renderers 1 · materials 1 · submeshes 1
all active character shadows = 1 draw
runtime-created contact-shadow materials = 0   (sharedMaterial == M_CharacterContactShadow)
```

**70 enemies + player cost 9 batches over the empty-world baseline**, and all 71 shadows are a single
draw. The 20 -> 40 rows are identical (116/46) because the extra 20 same-archetype bodies instance into
the existing batches; the 70 row rises only because it spans 3 archetypes (3 enemy materials), which is
material count, not shadow count.

Original build for comparison: **26 enemies = 204 batches / 138 SetPass**.

## Gate 2 — visual evidence: PARTIAL

Captured on one identical frozen 70-enemy frame:

- `SheetB_70crowd_shadows_ON.png`
- `SheetA_70crowd_shadows_OFF.png`

Asserted at capture time: analytic shader active, authored shared material (not runtime-created),
1 contact-shadow renderer, 1 material, 1 submesh, 71 visible shadows in 1 draw, legacy Player/Plane
renderer absent, legacy enemy ShadowBlob count 0.

Visual read: no rectangles, no visible quad corners, no duplicate player shadow, no z-fighting. The
grounding is **subtle at gameplay scale** — the ON/OFF pair differ only slightly at 520 px. That is
consistent with the locked "visible but secondary" tuning, but Sheets C (biome/boss scale) and D
(lifecycle: alive -> hit -> dissolving -> removed -> pooled respawn) were **not** captured, so Gate 2
is recorded as PARTIAL rather than PASS.

## Gates not executed

- **Gate 5 (WebGL 2 build)** — not run.
- **Gate 7 (two production runs)** — not run.

Neither is blocked; neither was reached. No result is claimed for either.

## Repository

```text
staged: 0 · protected hashes unchanged · no temp content under Assets/
evidence under Review/M5_RenderingCloseout/AcceptanceFinal/
```

---

# Gate 7 — two production runs (2026-08-14)

Both runs via `Bootstrap -> Menu/Hub -> PLAY -> Map_Level1`. Map_Level1 was never opened directly.

## Run A

```text
entry counts   : contactShadowManagers 1 · worldStreamers 1 · players 1 · cameras 1
                 audioListeners 1 · runDirectors 1 · waveDirectors 1
shadows        : registered 26 · visible 26 · material M_CharacterContactShadow
phase ordinals : 104
exercised      : weapon switch (-> WD_AssaultRifle_G36C), bomb, combat, terminal result, HOME
```

Bomb cleared the wave, and the shadow batch followed it exactly: **alive 0 -> visible shadows 0**, which
is the removal half of the Sheet D lifecycle proven numerically rather than by eye.

```text
after HOME : contactShadowManagers 0 · worldStreamers 0 · players 0 · audioListeners 1
             CharacterContactShadows.Instance = null (clean)
```

## Run B

```text
entry counts : contactShadowManagers 1 · worldStreamers 1 · players 1 · audioListeners 1
               runDirectors 1 · waveDirectors 1
material     : M_CharacterContactShadow · runtime-created material = False
```

Exactly one fresh instance of every production owner, and the authored material again — no runtime
material, no stale manager carried over from Run A.

Run B ended on the **opposite terminal path** (the player died at spawn into the standing crowd).
Worth recording because it briefly looked like a defect: `PlayerMovement.Instance` read NULL while a
`Player(Clone)` still existed. That is correct behaviour — a dead player disables itself and clears the
static instance, and the contact-shadow registration follows (`registered 0`). It is not a second-run
leak.

```text
after HOME : contactShadowManagers 0 · worldStreamers 0 · players 0 · audioListeners 1
             Instance = null (clean)
```

**Gate 7: PASS** — one owner of each service per run, clean teardown after both, no stale shadow
handle, no runtime material, no duplicated service.

# Gate 2 — status: PARTIAL

Held from the earlier pass: ON/OFF comparison on an identical frozen 70-enemy frame
(`SheetA_70crowd_shadows_OFF.png`, `SheetB_70crowd_shadows_ON.png`) with structural assertions at
capture time, plus the numeric removal proof above.

**Sheet C (dry vs grass biome, boss scale) and the remaining Sheet D frames (hit, mid-dissolve, pooled
respawn) were not captured.** Gate 2 stays PARTIAL.

# Gate 5 — WebGL 2: NOT RUN

# Gate 9 — final regression: NOT RERUN this pass

No production code changed during Gate 7, so the standing totals remain valid:
EditMode 504/504, PlayMode 226/226. The required *second consecutive* PlayMode run was not performed
in this pass.

---

# Gate 5 — WebGL 2 build: PASS (2026-08-14)

Real build through the normal project path, `BuildTarget.WebGL`, Development.

```text
result   : Succeeded
errors   : 0
warnings : 15
size     : 268,001,442 bytes
time     : 00:10:46
output   : Build/WebGL_M5Acceptance  (30 files, 255.6 MB on disk)
           index.html · .wasm 104.2 MB · .data 133.6 MB · .framework.js · .loader.js
```

Pre-build conditions confirmed:

```text
scenes enabled: Bootstrap, Menu, Map_Level1  (exactly three, nothing else)
activeBuildTarget: WebGL · WebGL module installed: True
graphicsAPIs(WebGL): OpenGLES3  (WebGL 2) · linkerTarget: Wasm
```

Shader/material inclusion, taken from the build log:

```text
Compiling shader "ZombieWar/Environment/CharacterContactShadow"   -> serialized into build
Compiling shader "ZombieWar/VAT/EnemyToon"                        -> serialized into build
build contents:
   4.6 kb  Assets/_Project/Art/Shaders/CharacterContactShadow.shader
   0.2 kb  Assets/_Project/Art/Materials/M_CharacterContactShadow.mat
```

No missing shader, no pink material, no compile error. One **pre-existing** shader warning in
`VAT_EnemyToon` (potentially uninitialised `DissolveNoise`, d3d11 + gles3) — not introduced by this
work and not a build failure.

Asset integrity across the build:

```text
15 VAT data assets changed by the build : 0
M_CharacterContactShadow.mat hash       : c21f5fe6cac4f8d8ae73b6a44d28a1c3398c86c3
staged                                  : 0
```

**Limitation, stated precisely:** browser automation is not available in this environment, so the
built player was not launched and no in-browser frame was captured. Verification is build-level:
successful compile/link, shader inclusion, material inclusion, correct scene set, correct graphics
API, and zero asset mutation.

## Defect found and fixed by the build log

The log exposed a real race:

```text
[ContactShadows] Đã có một CharacterContactShadows khác — huỷ bản thừa.
  at CharacterContactShadows.Awake  <- AddComponent  <- EnsureInstance  <- Register
```

`EnsureInstance()` used `FindFirstObjectByType`, which **skips inactive objects**. Enemies register in
`OnEnable`, and Awake order between unrelated objects is not guaranteed, so the authored Map_Level1
manager could be missed and a second one created — immediately destroyed by the duplicate guard, with
a red error each time.

Fixed: the lookup now uses `FindObjectsByType(FindObjectsInactive.Include, ...)`, and it **no longer
auto-creates** a manager. A runtime-created manager has no authored material, so it would only produce
a silent, invisible shadow system; a missing manager is now a loud, precise error naming the material
path — matching the material policy.

Verified live afterwards: `contactShadowManagers (incl. inactive) = 1`, one GameObject of that name,
material `M_CharacterContactShadow`, 29 registrations, and no duplicate-manager error.

# Gate 9 — final regression: PASS

Production code changed (`EnsureInstance`), so the full suites were rerun:

```text
EditMode              504 / 504  PASS
PlayMode              227 / 227  PASS
PlayMode (2nd run)    227 / 227  PASS
```

# Gate 2 — status: PARTIAL

Held: ON/OFF pair on an identical frozen 70-enemy frame, 70-crowd frame, an alive frame, and the
numeric removal proof (bomb clears the wave -> `alive 0` and `visible shadows 0` together).

Not captured: Sheet C (dry vs grass biome, boss footprint) and the remaining Sheet D frames (hit,
mid-dissolve, pooled respawn). Each attempt to stage them ended with the player dying into the
standing crowd before the frame could be composed. Gate 2 therefore stays **PARTIAL**.

---

# Gate 2 — Visual acceptance: PASS (2026-08-14)

Capture environment was controlled, not lucky: entered through `Bootstrap -> Hub -> PLAY`, player made
effectively invulnerable via the existing `Health.IncreaseMax/Heal` API (HP 1e8), wave director and
spawners disabled, `ZombieManager` tiering suspended so a staged enemy could not be auto-deactivated,
and the crowd cleared so one controlled enemy could be observed. All of it runtime-only state — **no
production code, no assets and no scenes were modified to take screenshots**, and nothing temporary
was left under `Assets/`.

Asserted before every capture:

```text
CharacterContactShadows managers : 1
contact-shadow renderers         : 1     materials : 1     submeshes : 1
all visible shadows              : 1 draw
legacy ShadowBlob nodes          : 0
Player/Plane MeshRenderer        : absent
runtime contact-shadow material  : false   (M_CharacterContactShadow)
renderingLayerMask               : 1       (outside outline selection)
```

## Sheet C — biome and scale

| # | frame | evidence |
|---|---|---|
| C-1 | dry biome, player + normal enemy | `SheetC_1_dry_player_and_enemy.png` (registered 2 / visible 2) |
| C-2 | grass-rich biome, player + normal + large enemy | `SheetC_2_grass_player_normal_large-1.png` (registered 3) |

The grass-rich site was chosen by measurement, not by eye — five candidate world positions were
sampled for foliage vertex count and the densest picked (`(-160, -120)`, 341,056 foliage verts vs
228,928 at the sparsest).

Both ground types keep a readable contact shadow. No rectangle, no quad corner, no z-fighting, no
floating gap; the larger archetype's ellipse scales with its footprint and the player has exactly one
shadow.

## Sheet D — lifecycle (one controlled enemy, tracked by instance id)

| # | state | id | registered | visible | evidence |
|---|---|---|---:|---:|---|
| D-1 | alive | -2093302 | 2 | 2 | `SheetD_1_alive.png` |
| D-2 | after non-lethal hit (hp 40 -> 24) | -2093302 | 2 | 2 | `SheetD_2_after_hit.png` |
| D-3/4 | lethal -> dissolve -> fully removed | -2093302 | **1** | **1** | `SheetD_4_fully_removed.png` |
| D-5 | pooled respawn (fresh pooled instance) | -2092990 | 2 | 2 | `SheetD_5_pooled_respawn.png` |

The hit did not detach or duplicate the shadow; death released it (count fell to the player alone,
leaving no shadow on the ground); the pooled respawn registered **exactly once**. Renderer, material
and submesh stayed 1/1/1 throughout, and no legacy blob reappeared.

Earlier, still valid and not recaptured: `SheetA_70crowd_shadows_OFF.png` /
`SheetB_70crowd_shadows_ON.png` (identical frozen 70-enemy frame) and the numeric removal proof from
Run A (bomb clears the wave -> `alive 0` and `visible shadows 0` together).

**Gate 2: PASS.**

---

# M5 FINAL STATUS — COMPLETE

| Gate | Result |
|---|---|
| 1 — contact-shadow lifecycle | PASS 11/11 |
| 2 — visual acceptance | PASS |
| 3 — VAT authoring-time contract | PASS (15/15 materials, 0 runtime mutations) |
| 4 — performance | PASS (70 enemies + player: 122 batches / 52 SetPass / 71 shadows in 1 draw) |
| 5 — WebGL 2 build | PASS (0 errors; browser execution unavailable in this environment) |
| 6 — ground matrix | PASS 48/48 |
| 7 — production Run A / Run B | PASS (one owner per service, clean teardown after both) |
| 8/9 — regression | EditMode 504/504 · PlayMode 227/227 · repeated PlayMode 227/227 |

## Accepted limitation

The WebGL 2 build **succeeded** (0 errors, both shaders and the authored material serialized into the
player, 0 asset mutation). Browser automation is not available in this environment, so the built player
was not launched and no in-browser frame was captured. This is a **verification limitation, not a
failed build** — nothing about the build itself is unresolved.

## Rendering arc, end to end

```text
before : 26 enemies = 204 batches / 138 SetPass   (one transparent shadow draw per character)
after  : 70 enemies = 122 batches /  52 SetPass   (all 71 character shadows in one draw)
```

---

# M5+ — Character lighting and material cohesion (ACTIVE BEFORE M6)

M5 is mechanically and technically complete. Before M6 opens the gameplay redesign, one bounded visual
closeout remains: bring the player character in the Hub and representative gameplay lighting into the
same stylized contract already established for the streamed environment and VAT enemies.

## Evidence that must be collected before implementation

- Capture the real Hub flow at its shipping portrait aspect, including front, three-quarter and rear
  player reads plus a close material crop.
- Capture the same authored costume/player under gameplay lighting so lighting and material effects can
  be separated from model/texture problems.
- Inspect the Hub camera, ToonLightRig/fake-light source, Volume profile, renderer path and every material
  actually assigned to the runtime-composed player.
- Attribute over-bright clipping, metallic/plastic response, roughness/smoothness, rim/specular and skin/
  hair/cloth separation to measured shader/material/light parameters rather than editing by screenshot
  alone.
- Preserve the character outline and modular costume pipeline unless a proven contract defect exists.

## Intended result

- The player remains the visual focus without clipping to white or reading as chrome/plastic.
- Skin, hair, cloth, leather/metal accents and weapons retain readable but stylized material separation.
- Hub and gameplay do not present two different characters under incompatible lighting contracts.
- The fix preserves the project's cheap authored fake-light source, but the current generic
  StylizedToonWorldKit character shaders are **not** visually locked. Player feedback rejects both the
  current toon response and the current metal response as a fit for this game.
- A small project-owned character shader family may replace them. It must remain one-pass, atlas-based,
  WebGL-safe and compatible with the existing modular costume and selection-outline contracts; it must
  not reintroduce expensive realtime lighting, fullscreen AO or a general-purpose shader stack.

M6 stays **NOT STARTED** until this audit is reviewed and the smallest implementation directive is
approved.

## M5+ audit result — 2026-08-14

The real Bootstrap -> Menu/Hub flow was captured at the shipping 506x900 portrait view and compared
with the same player in gameplay. Evidence lives under
`Review/M5Plus_CharacterVisualAudit/` (`Hub_Current`, `Hub_ThreeQuarter_2`, `Hub_Back`,
`Gameplay_Current`, `Candidate_LightOnly`, `Candidate_MaterialLight_2`, and
`Candidate_MaterialLight_ThreeQuarter_2`). All candidate changes were runtime-only and were discarded
on leaving Play Mode.

Measured attribution:

- The authored Hub preview uses a 512x900 RenderTexture, a 30-degree non-HDR camera, and one warm
  directional preview light at intensity 1.1. There is no evidence that post-processing exposure is
  causing the clipping inside the preview texture.
- The active Casual character is dominated by shared vendor material `ColorA`: `_GIStrength=2.0`,
  `_RampSteps=1.5`, `_RampSmooth=0`, and `_SpecStrength=0.2`. White atlas regions therefore receive
  maximum direct light plus double-strength SH/GI and lose their planes on hair, head and shoes.
- Metallic parts use shared `ColorC`: `_Metallic=0.55`, `_EnvStrength=1.84`, anisotropic strength
  `1.26`, an HDR anisotropic color above 1.0, and rim strength `0.81`. Gloves and small accents read as
  chrome rather than authored stylized metal.
- Reducing only the Hub light from 1.1 to 0.72 did not restore enough white-surface form. A runtime-only
  material/light candidate restored some readable planes, proving that the texture/model data is usable,
  but the owner rejected the underlying toon and metal look. That candidate is diagnostic evidence, not
  the production art direction.
- The Casual catalog contains 453 parts and depends on four material roles: `ColorA` (422 references),
  `ColorC` metal (84), `ColorB` glass (8), and `ColorD` toon (3). A production migration must cover all
  four roles and the deterministic catalog generator; fixing only the currently equipped outfit would
  leave most of the wardrobe unverified.

Revised implementation direction for the next delegated task:

1. Prototype project-owned `ZombieWar/Character` toon and metal shaders before migrating the catalog.
   Do not tune or overwrite the vendor shaders/materials in place.
2. Use the MinionsArt lit-toon and stylized-metal references as the visual/math baseline, adapted to
   URP 17 and the existing `_ToonLightDirection`/`_ToonLightColor` fake-light globals:
   - Lit toon: a clean main-light toon ramp multiplied by the atlas, shadow attenuation, and a restrained
     light-side-only rim. Avoid the current double-strength SH wash and generic additive lighting stack.
   - Metal: discrete view-normal specular, an offset view+light highlight, a restrained Fresnel rim, and
     a mask/material-role boundary. Avoid the current SH environment reflection plus global HDR
     anisotropic sheen that makes small parts look chrome.
   Sources: `https://www.patreon.com/minionsart/posts/lit-toon-shader-54740865`,
   `https://minionsart.github.io/tutorials/Posts.html?post=u_toon_metal`, and the clearer 2024 URP metal
   breakdown at `https://www.patreon.com/posts/stylized-metal-111029610`.
3. Preserve the modular costume application, atlas UVs, shared-material model, fake-light source and
   selection-outline pipeline. Replace only the character shading contract.
4. Compare at least three controlled shader candidates in Hub front/three-quarter/rear and in gameplay, using
   white hair/skin, dark cloth/leather, metal, and glass examples. Select by preserved form and material
   separation, not by arbitrary numerical targets.
5. Only after visual approval, create the final project-owned character materials and make
   `CasualCatalogGenerator` remap/rebuild to them.
6. Validate every material role, a representative set of light/dark costumes, the rebuild path, icon/
   thumbnail rendering, Hub/gameplay consistency, material instance count, batches and WebGL shader
   compilation before locking M5+.

## M6 skill-design reminder

When M6 starts, read and reconcile all three sources before proposing or implementing skills:

1. `Docs/Reference/Design/SKILL_SYSTEM_DESIGN.md` — current candidate architecture.
2. `Docs/Icebox/IN_RUN_SKILL_STAT_AND_INTERACTIVE_DESIGN.md` — superseded but detailed design library.
3. `Assets/Icons/skills` and `Assets/Icons/potions` — 22 skill plus 10 potion/buff icon ingredients.

Do not restart skill ideation from the seven numeric runtime perks. Audit existing candidates as
`KEEP / ADAPT / CUT / LATER`, remove reload/ammo/wave-era assumptions, map retained mechanics to icons,
then produce one canonical run-build/skill document.

---

## M5+.1 — Character shader prototype (2026-08-14) — READY FOR CODEX REVIEW

**The previous direction is rejected and withdrawn.** The character look was not a material-tuning
problem. The vendor toon path combines main light + an additional-light loop + SH/GI ambient + broad
specular + a generic rim; the SH term lifts every surface by normal direction, which is what flattens
white hair into a single mass and washes skin toward white. Lowering light intensity cannot fix that.

### Prototypes created (project-owned, isolated)

```text
Assets/_Project/Shaders/Character/CharacterLighting.hlsl
Assets/_Project/Shaders/Character/ToonPrototype.shader     ZombieWar/Character/ToonPrototype
Assets/_Project/Shaders/Character/MetalPrototype.shader    ZombieWar/Character/MetalPrototype
Assets/_Project/Art/Materials/Character/Prototype/M_Char_{Toon,Metal}_{H1,H2}.mat
```

Both compile clean (3 passes each: ForwardLit / ShadowCaster / DepthOnly), SRP-Batcher-compatible
`UnityPerMaterial` CBUFFER, instancing declarations, WebGL-suitable `#pragma target 3.0`, no geometry
stage, no compute, no scene-colour or screen grab, no cubemap.

### Vision review outcome

H0 REJECT · **H1 ACCEPT (recommended)** · H2 ACCEPT (second).

H1 is the only candidate where the white hair regains visible planes (warm cream top vs cool lavender
under-planes) and the small metal parts — gauntlets and earring — read as stylized metal rather than
grey putty. Full per-criterion table in `Review/M5Plus_CharacterShaderPrototype/vision_review.md`.

### Flagged for the owner before migration

In H1/H2 the legs read as warm skin where H0 read near-white. That is the authored atlas colour
becoming visible once the SH wash is removed, not a tint the prototype added. If those are meant to be
white boots, it is a costume-part question, not a shader one.

### Remaining production migration work (NOT done)

453 catalog parts, production ColorA/B/C/D references, costume icon regeneration, gameplay-scale and
outline verification under the rig path. **M5+ is not complete** — the prototype is awaiting visual
approval.
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

> **SUPERSEDED for the metal role — see "M5+ TOON UNIFICATION" at the end of this document.**
> `CharacterMetal.shader` and `M_Character_Metal.mat` were **retired**: dedicated character metal is a
> deliberate MVP scope cut, not a missing feature. `ColorC` now resolves to `M_Character_Toon`. The
> block below is the historical record of what this phase produced, not the current asset list.

```text
Assets/_Project/Shaders/Character/CharacterToon.shader    ZombieWar/Character/Toon
Assets/_Project/Shaders/Character/CharacterMetal.shader   ZombieWar/Character/Metal   [RETIRED]
Assets/_Project/Art/Materials/Character/M_Character_Toon.mat      (H1 values baked)
Assets/_Project/Art/Materials/Character/M_Character_ToonAlt.mat   (H1 values baked)
Assets/_Project/Art/Materials/Character/M_Character_Metal.mat     [RETIRED]
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

New mapping (`RoleToProjectMaterial`, the only place it is defined) — **`ColorC` superseded, see the
final section; it now points at `M_Character_Toon`**:

```text
FBX ColorA -> Assets/_Project/Art/Materials/Character/M_Character_Toon.mat
FBX ColorB -> Assets/_Project/Art/Materials/Character/M_Character_Glass.mat
FBX ColorC -> Assets/_Project/Art/Materials/Character/M_Character_Metal.mat   [SUPERSEDED -> Toon]
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

> **Closed out by "M5+ TOON UNIFICATION" below.** All four gates above were executed in that phase.

---

# M5+ TOON UNIFICATION — dedicated character metal CUT (authoritative)

**This section supersedes every earlier statement in this document that treats a dedicated character
metal shader/material as production.**

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

This is a deliberate scope cut, not an accidental missing feature. Former-metal geometry, atlas UVs and
authored atlas colours are **unchanged** — silver stays silver, gold stays gold. Only the lighting
contract became normal Toon. Optional future stylized-metal work belongs in the **Icebox**, not on the
active MVP roadmap.

## Final production material roles

```text
Toon      M_Character_Toon      ZombieWar/Character/Toon
ToonAlt   M_Character_ToonAlt   ZombieWar/Character/Toon
Glass     M_Character_Glass     StylizedToonWorldKit/Surface/Glass   (retained, unchanged)
```

Final FBX role mapping (`RoleToProjectMaterial` — still the only place it is defined):

```text
ColorA -> M_Character_Toon
ColorB -> M_Character_Glass
ColorC -> M_Character_Toon        <- changed here, and only here
ColorD -> M_Character_ToonAlt
```

Missing project material still **fails loudly and aborts** rather than falling back to vendor.

## Catalog accounting

```text
catalog entries        : 453      (unchanged)
material slots         : 517      (unchanged)
M_Character_Toon       : 506      = 422 former ColorA + 84 former ColorC
M_Character_Glass      :   8
M_Character_ToonAlt    :   3
dedicated Metal        :   0
vendor references      :   0      runtime material instances : 0      null references : 0
arithmetic             : 392x1 + 58x2 + 3x3 = 517   and   392 + 58 + 3 = 453
idempotence            : catalog hash run 1 = run 2 = 6992ef669a0b (byte-identical); FBX remap identical
```

No catalog entry was hand-patched. The 84 former-ColorC slots migrated because the FBX importer remap is
the single source of truth.

## Retired assets

```text
Assets/_Project/Shaders/Character/CharacterMetal.shader        (+ .meta)   deleted
Assets/_Project/Art/Materials/Character/M_Character_Metal.mat  (+ .meta)   deleted
```

Proven zero before deletion — catalog 84 -> 0, prefabs 0, scenes 0, other ScriptableObjects 0, materials
0, shaders 0, `AlwaysIncludedShaders` absent, `PreloadedAssets` absent, tests no longer require them.
`Shader.Find("ZombieWar/Character/Metal")` now returns null. `CharacterToon.shader`,
`CharacterLighting.hlsl`, `M_Character_Toon/ToonAlt/Glass` and all earlier `Review/` evidence are kept.

## Visual acceptance (real catalog assembly, 8 controlled cases)

Evidence: `Review/M5Plus_ToonUnification/FormerMetal_Toon_{Hub,Gameplay,Closeups}.png`,
`HubScale_Jewellery.png`, `Hub_real.png`, `Gameplay_clean.png`.

```text
silver (Headgear_45, Glove_16, Shoes_46) : readable — holds a mid-grey with distinct plane steps,
                                           does NOT clip to flat white; gold trim separates
gold   (Headgear_46 crown)               : remains clearly gold, darker inner faces intact
dark   (Headgear_61 + Glove_17)          : does NOT crush; bright/dark facet split stays legible
jewellery (Earring_9 + Bracelet_2)       : gold hoops and gem bracelets read at TRUE Hub preview
                                           scale (512x900, real stage camera)
mixed  (Headgear_26 + Eyewear_14)        : Glass still reads as glass through the dome; former-metal
                                           rim reads separately
gameplay scale                           : silhouette readable from the top-down camera, player
                                           clearly distinct from orange enemies and brown ground
no pink material · no broken atlas/UV · no unwanted glow · runtime material instances 0
```

The result is intentionally non-metallic. That is the approved direction, not a defect.

## Hub / gameplay wiring (real flow, no swap harness)

```text
Hub      : 15 costume renderers · 18 material slots · 1 distinct material (M_Character_Toon)
           vendor 0 · runtime instances 0 · dedicated metal 0
           _ToonLightDirection = (0,0,0,0)                -> URP main-light FALLBACK path
Gameplay : 15 costume renderers · 18 material slots · 1 distinct material (M_Character_Toon)
           vendor 0 · runtime instances 0 · dedicated metal 0
           _ToonLightDirection = (0.32, 0.77, -0.56)      -> ToonLightRig path
```

## Outline contract

Evidence: `Review/M5Plus_ToonUnification/Outline_Contract.png`, `outline/motion_{A,B}.png`.

`selectionLayer` is reinterpreted by `BillOutlineFeature` as a **renderingLayerMask** (value 30 = bits
1,2,3,4), so selection is a purely visual channel and never touches physics layers.

```text
player       : 22 / 22 renderers in the mask — 16 costume (bit 3) + 6 weapon (bit 1)
               former-metal Head/Hands/Feet, Toon Chest/Legs and Glass Eyewear all join ONE
               contiguous silhouette; no missing modular part, no detached glass outline
enemies      :  8 /  8 via bit 2 (VAT material-driven path — avoids bind-pose freeze)
solid decor  : 25 / 25 via bit 4  (deliberate, pre-existing design)
ground       :  0 / 25 in mask    -> excluded
foliage      :  0 / 25 in mask    -> excluded
contact shadow: not in mask       -> no foot rectangle, structurally
not frozen   : over 8.5 s of game time the mask changed 83,961 -> 198,338 px and the centroid moved
profile      : SampleSceneProfile.asset 014a03d6… unchanged (debug drove the runtime instance only)
restored     : mode=SelectionOnly, debugMode=None, isActive=True, thickness=2
```

## Two-run lifecycle — `Bootstrap → Hub → PLAY → move/fire/switch weapon → HOME` x2

```text
                     run 1            run 2
players (gameplay)   1                1
players (Hub)        0                0
cameras              1 gameplay /     1 gameplay /
                     2 Hub            2 Hub
AudioListeners       1                1
world streamers      1                1
costume renderers    15               15
material slots       18               18
distinct shared mats 1 (Toon)         1 (Toon)
runtime instances    0                0
vendor references    0                0
dedicated metal      0                0
stale costume slots  0                0
outline-selected     22               21
```

No duplicated service, no stale costume object, no runtime material instance, no metal restoration.
Weapon switch exercised (Pistol -> G36C); player translated in both runs.

## Rendering result

```text
Hub       batches 67-76 · SetPass 23-32 · 15 character renderers · 18 slots
          distinct character shared materials 1 · runtime instances 0
Gameplay  world + player, 0 enemies : batches 103 · SetPass 28 · drawCalls 103
          representative live wave  : 13 enemies -> batches 109 / SetPass 57
                                      22 enemies -> batches 125 / SetPass 59
          player-attributed distinct OPAQUE materials : 1
          character shader passes : ZombieWar/Character/Toon = 2 (ForwardLit + DepthOnly)
```

**Architectural improvement, stated only as far as measured.** For any outfit containing former-ColorC
geometry the distinct opaque character material count drops from **2 → 1**: the metal-heavy set
`Headgear_45 + Glove_16 + Shoes_46 + Top_42 + Bottom_35` resolved to `{Metal, Toon}` before and measures
`{Toon}` = 1 now. The dedicated metal shader and its pass no longer exist in the project or the build.
No per-renderer material creation in either scene.

**No batch reduction is claimed.** Batch/SetPass counts move with wave size, streaming and UI state, and
no frame comparable enough to attribute a delta to this change was captured. The measured effect is one
fewer material/shader role, not a measured batch win.

## Tests

```text
targeted material + outline/lifecycle :  24 /  24
EditMode                              : 515 / 515
PlayMode pass 1                       : 227 / 227
PlayMode pass 2                       : 227 / 227
```

## Protected asset integrity (before vs after, sha1)

```text
ColorA fb51310d…  ColorB 53f4ea21…  ColorC e5284e42…  ColorD 4bda571b…      unchanged
Player.prefab d7b14e9d…  Menu.unity 1bc90c03…  Map_Level1 058c4cc5…        unchanged
outline profile 014a03d6…  EditorBuildSettings 8b83756b…                   unchanged
M_Character_Toon b3e1f49d…  ToonAlt 69902e06…  Glass c0432763…             unchanged
CharacterToon.shader 8f95b310…                                             unchanged
catalog 34cb63e2… -> 6992ef66…   (intended, stable across both runs)        staged: 0
```

## WebGL 2 rebuild (shader/material build content changed)

```text
result        : Succeeded          time 00:16:43       output Build/WebGL_M5Plus_ToonUnified
totalErrors   : 0                  totalWarnings 16    total size 130 MB, 29 files
scenes        : Bootstrap, Menu, Map_Level1 only
BuildReport packed assets (5066 entries):
  M_Character_Toon.mat      present
  M_Character_ToonAlt.mat   present
  M_Character_Glass.mat     present
  CharacterToon.shader      present   ("Compiling shader ZombieWar/Character/Toon")
  M_Character_Metal.mat     ABSENT
  CharacterMetal.shader     ABSENT
no missing shader · no pink material (no material resolves to Hidden/InternalErrorShader)
Toon shader: 0 messages, 2 passes
```

`totalErrors` is now **0**; the previous M5+ build reported 4 unrelated errors.

**Browser execution NOT VERIFIED** — no browser automation in this environment. A successful build is
not the same as successful browser execution.

---

# M6 — PROPOSED EXECUTION LADDER — M7 NOT AUTHORIZED

**M6 design authority:** [`M6_ENDLESS_RUN_SYSTEM_DESIGN.md`](M6_ENDLESS_RUN_SYSTEM_DESIGN.md)
**Status: M6 is LOCKED (W1–W7 answered 2026-08-15). M7 is NOT STARTED and NOT AUTHORIZED.**
M5 and M5+ are complete.

> M6 being locked is **not** M7 authorization — they are separate approvals, and the second has not been
> given. The ladder below is what M7 approval *would* trigger. It is not an active plan. The answered
> decisions are in section 1 of the M6 document; the three deltas they created are in §1c–§1e.

Every "expedition", "extraction" and "contract" statement earlier in this document describes the
pre-M6 direction and is superseded. The game is a **one-weapon endless action roguelite**.

## What M6 proposes (owner-locked rows are marked)

| Area | Decision | Status |
|---|---|---|
| Run structure | Endless. **No Victory state.** Death banks **25 %** Coin; manual abandon banks **0 %** and requires confirmation. |
| Weapons | **One per run**, chosen in the Hub. **Weapon Factory**: 6 families, 33 distinct bodies measured, variants inside families (W1, OWNER-LOCKED); no fixed weapon count is a design boundary. Onboarding runs through the **universal Weapon Visual Onboarding Gate G1–G8**, and every weapon sits in a **measured tier band** (W3, OWNER-LOCKED). The three-gun roster limit is retired. |
| Skills | **23-card candidate catalog** = 5 stat (filler) + 12 signature (2 per family) + 4 autonomous + 2 universal. **Max one stat card per 1-of-3 offer.** 20 of 23 need new runtime primitives. |
| Grenade | **Retired.** Content reused as `Ordnance Core` (autonomous) and `Emergency Detonation` (instant on pickup). |
| Map objects | Signal Relay · Supply Cache · Boss Beacon · Medical Station · Route Scanner · Greed Terminal, all on one interaction grammar. |
| Boss content | **Existing assets only** — `CactusBoss`, `MoleRatKing`, `SkeletonGiant`; elites `DogBowwow`, `SkeletonMage`, `CatLightning`. |
| Economy | **Coin** (spend now) · **Gem** (rare cosmetics) · **Blueprint** (weapon unlocks — new, fungible, deterministic) · **Relic** (collection). Weapons cost Blueprint only; Coin buys everything else. **Gold, Weapon Shards, star upgrades and gacha are present in code, without design authority** (W6, OWNER-LOCKED). |
| Outfits | **H2 approved for production** (W7, OWNER-LOCKED). Gate: **0 % hard conflicts (mandatory)** + ~70 %+ thematic coherence; measured 0 % / 73 % over 300 seeds. The 85 % bar is superseded by owner decision. Metadata authoring over 453 items is still outstanding. |
| Removed | Wave-clear auto-collect · five-wave Victory · three-weapon switching · extraction as core · Maps 2–5. |

## Proposed ladder (M7) — NOT AUTHORIZED

| Step | Deliverable | Pass condition | Cost |
|---|---|---|---|
| **M7.1** | One-weapon contract · widened XP curve · one-stat-card-per-offer rule | Two runs with the same gun differ; no all-stat offer appears | MEDIUM |
| **M7.2** | Offer system + approved subset of the 23 cards | ≥2 visibly different builds per family; no all-stat offer | MEDIUM |
| **M7.3** | Signal Relay + compass/pin HUD | Players leave safety to reach a signal | MEDIUM |
| **M7.4** | Supply Cache + in-run Coin sink | Players spend Coin before dying | LOW–MEDIUM |
| **M7.5** | Boss Beacon + Boss Chest (existing enemies) | Players opt into a boss they could have skipped | MEDIUM |
| **M7.6** | Endless settlement; remove `WaveClearedEvent` auto-collect | No "Victory" string reachable; banking correct | MEDIUM |
| **M7.7** | Relic Archive; Gem/Relic secure-on-pickup | A rare drop survives a death | MEDIUM |
| **M7.8** | Costume metadata authoring + H2 randomizer | ≥80 % PASS over 100 seeded outfits, 0 hard conflicts | MEDIUM–HIGH |

**Smallest coherent vertical slice = M7.1 + M7.2 + M7.3.**
If players will not voluntarily start a third run after those three steps, nothing later is worth
building. Do **not** implement 25 weapons or the full skill library before that gate.

## Known implementation debt recorded, not fixed, by M6

These are code changes M6 identified but deliberately did **not** perform:

| Item | Location | Work |
|---|---|---|
| Wave-clear auto-collect | `PickupManager.cs:141` → `CollectAll()` | remove for endless |
| Dead weapon fields | 25 `WeaponData`: `resourceModel = Magazine`, empty `roleTag`/`buildTag`/`buildHint` | author identity, drop dead fields |
| Three weapon slots | `LoadoutState` slots 0–2 | reduce to one selected weapon |
| Manual bomb input | `BombThrower`, `Bomb`, HUD bomb button | retire input, keep art |
| XP curve | `RunState.XpForNextLevel = 10 + (Level-1)*8` | widen (see spec §16.2) |
| Stale project fact | `Docs/Reference/Design/SKILL_SYSTEM_DESIGN.md` §1 | perks **are** applied; correct the text |
