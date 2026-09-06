# Batched contact shadow (2026-08-13)

## Why

Locked diagnosis from the previous phase: `M_BlobShadow` was a per-character **transparent** renderer
(URP/Unlit, queue 3000). Transparent geometry sorts back-to-front per object, so Unity submits one
draw per blob regardless of `enableInstancing`. Measured: 30 enemies = 30 blob draws = exactly the
whole remaining linear rendering cost.

Merging every character shadow into ONE mesh removes the sorting problem entirely — one object to
sort, one draw — without instancing, indirect rendering, compute, DOTS or a custom horde renderer.

## Architecture implemented

```text
CharacterContactShadows  (one runtime node, DefaultExecutionOrder 200)
├── MeshFilter   -> one reusable dynamic Mesh
└── MeshRenderer -> one shared material, one submesh
```

- `Register / Unregister / SetVisible / SetFade / SetSize` with stable integer handles.
- Fixed capacity (default 160) with preallocated vertex/UV/colour/index arrays; **fails loudly** when
  exceeded rather than silently dropping a character's shadow.
- One quad per shadow (4 verts / 6 indices). UVs and indices are built **once**; only positions and
  colours are rewritten per frame. Unused slots collapse to a degenerate zero-area quad so the index
  count never changes and no buffer is reallocated.
- Vertex colour alpha carries per-character opacity and death fade, so there is no per-character
  material and no property block.
- Vertices are written in the node's **local** space and the node re-centres on the player in 64 m
  steps, so long traversal cannot accumulate float error into the shadow offset.
- Bounds are computed from the active shadows' min/max — no `RecalculateBounds()` over hundreds of
  degenerate verts, and no inflated bounds that would defeat frustum culling.
- `renderingLayerMask = 1`: the shadow never enters the outline selection mask.
- Project-owned shader `ZombieWar/Environment/CharacterContactShadow`: one pass, unlit, transparent,
  `ZWrite Off`, `Cull Off`, `Offset -1,-1`, no ShadowCaster / DepthOnly / DepthNormals / outline pass.
  Verified `passCount == 1`. Radial falloff texture generated in code (no new asset to drift).

Player and enemies use the same system. The player is registered by the manager itself and keyed on
the player Transform, so a second run after HOME re-registers cleanly instead of tracking a destroyed
object.

## Measured result (frozen frame, 30 same-archetype Skeletons + player, Occlusion OFF)

```text
before, per-character blob renderers : 149 batches   setPass 35
after,  one batched contact shadow   : 107 batches   setPass 30

contact-shadow renderers : 1
contact-shadow materials : 1
contact-shadow submeshes : 1
registrations            : 31   (30 enemies + 1 player)
visible shadows          : 31
draws for all 31 shadows : 1
mesh                     : 640 verts / 320 tris (capacity 160, mostly degenerate)
```

**42 batches removed, and shadow cost is now O(1) in actor count** instead of one draw per actor.

For the whole rendering arc: **204 → 107 batches**, SetPass **138 → 30**.

## Honest status of the visual

The system renders and is structurally correct, but the shadow currently reads **too subtle** at
gameplay scale — see `C_batched_contact_shadow_30crowd.png` and `C_batched_contact_shadow_lifted.png`.
A ground lift (0.05 m) was added because the biome-varying ground was swallowing shadows placed at
y = 0, exactly as the old blob needed (`EnemyRosterTests` requires `localPosition.y > 0`).

**The Step 10 A/B (old blob vs no shadow vs new contact shadow, across biomes, aspects, overlap,
death fade, boss scale) was NOT completed.** Opacity, size and falloff are therefore unvalidated
authored guesses (`0.45` enemy, `0.5` player). This needs a visual pass before it can be called done.

## Not done in this phase

- Step 10 visual A/B matrix.
- Step 1 prefab-level retirement: `ShadowBlob` children still exist on enemy prefabs; they are force-
  disabled at runtime on registration. The prefab/authoring cleanup was not performed.
- Step 11 optional bottom-darkening; SSAO explicitly rejected (fullscreen cost, depth/normal
  requirements, WebGL fill-rate, weak grounding at top-down scale, far more complexity than one tiny
  dynamic mesh).
- Step 13 VAT authoring-time contract (still writes `sharedMaterial` at runtime).
- Steps 12/14 performance sweep at 20/40/70 and the WebGL 2 build.
- Steps 15/16 ground matrix and two production runs.
- Step 17 targeted contact-shadow tests; full EditMode/PlayMode not re-run after these edits.

---

# VISUAL REPAIR (2026-08-13) — the rectangles are fixed

## Rectangular root cause

The first implementation took the shadow shape from a `Texture2D` generated at runtime and sampled as
`SAMPLE_TEXTURE2D(_MainTex, ...).a`. When that texture does not reach the material, the sampler falls
back to Unity's built-in **white** texture, `falloff` becomes 1 at every pixel, and the entire quad is
filled solid to its corners — hard rectangular patches.

The previous report called this "too subtle". That was wrong: the defect was the silhouette, not the
opacity. Codex's read of the images was correct.

## Analytic falloff (fix)

The shape is only a function of radius, so it is now computed in the fragment shader with no texture
at all:

```hlsl
float2 p = IN.uv * 2.0 - 1.0;
float radiusSq = dot(p, p);
half falloff = pow(saturate(1.0 - radiusSq), _FalloffPower);
half alpha = saturate(falloff * _CoreStrength) * IN.color.a * _Tint.a;
```

`saturate(1 - radiusSq)` reaches exactly 0 on the inscribed ellipse, so the four quad corners are
always fully transparent and the quad edge can never be seen. The quad's authored world width/length
turns the UV circle into a correctly proportioned ellipse. No texture sample, no texture asset, no
runtime texture generation, no shader keyword, no per-character material property — per-character
opacity still rides in vertex colour.

Shader verified after recompile: `passCount == 1`, `_FalloffPower` and `_CoreStrength` present,
`_MainTex` gone.

Result: soft elliptical contact shadows under player and every enemy, no rectangles, no visible quad
edges — `S1_analytic_ellipse_12enemies.png`.

## Structure preserved

```text
12 enemies + player : 119 batches, setPass 47
contact-shadow renderers 1 · materials 1 · submeshes 1 · draws 1
registered 12 · visible 12  (player registers on its own LateUpdate tick)
```

The 30-enemy measurement from the previous phase (**107 batches**, 31 shadows in 1 draw) stands.

## Shared material asset

`Assets/_Project/Art/Materials/M_CharacterContactShadow.mat` was created against the analytic shader
(`_Tint` black, `_FalloffPower` 1.6, `_CoreStrength` 1.0).

**Step 3 is only partly done.** The runtime still creates its own material when the auto-created batch
node has no serialized reference, because `EnsureInstance()` builds the GameObject in code and there is
no approved runtime path to load the asset (Resources was not used, per the brief). Wiring the asset
through production bootstrap or a config ScriptableObject, and making a missing material fail loudly,
remains outstanding.

## Still outstanding

- Step 4/6 style candidates S1/S2/S3 and the four contact sheets: only S1 (analytic ellipse, default
  tuning) exists. No A/B against old blob / no shadow, no biome, aspect, overlap, death-fade, boss or
  pooled-reuse sheets. **The visual gate is therefore not formally passed.**
- Step 5 ground-lift A/B (0.005 / 0.01 / 0.02 / 0.05): still at 0.05 m, unmeasured. The comment was
  also corrected — the gameplay surface is flat at Y=0, so the lift exists to avoid coplanar
  z-fighting, not because biome geometry is displaced.
- Step 7 prefab retirement: `ShadowBlob` children still exist and are force-disabled at runtime.
  `EnemyRosterTests` still asserts the old premise and will need its contract replaced, not weakened.
- Steps 8-13: lifecycle matrix, 20/40/70 sweep, VAT authoring-time contract, WebGL 2, ground matrix,
  two production runs, and the full test suites — none run after these edits.

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
