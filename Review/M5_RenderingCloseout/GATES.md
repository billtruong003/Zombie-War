# M5 Final Rendering Closeout (2026-08-13)

## Root cause, corrected

```text
Missing-ground root cause:
Camera Occlusion Culling / stale baked occlusion interacting with RECYCLED procedural chunk renderers.

Rejected root cause:
camera pitch / footprint / coverage-origin was NOT proven to cause the owner-reported defect.
```

Owner reproduced it directly: with Camera Occlusion Culling disabled the ground is correct. The
player-centred chunk streaming logic was never the cause.

Everything R2 added to chase the misdiagnosis has been removed (see "Reverted" below). The R1/R2
measurements that remain valid and are NOT withdrawn: the authored camera footprint does fit inside
the 5x5 ring with 42.67 m to spare, and a horizon-visible camera cannot be covered by any finite ring.
Those stay recorded as a separate architectural note, not as a root cause.

## Occlusion policy

`CameraFollow.Awake()` now sets `useOcclusionCulling = false` on the gameplay camera, in CODE rather
than as a scene tick-box, so a stray scene save cannot re-enable it and every map using the follow
camera inherits the policy. Frustum culling is untouched.

**Measured cost of turning it off: essentially zero.**

```text
world + player + UI, 0 enemies, occlusion ON  : 116 batches, setPass 31
world + player + UI, 0 enemies, occlusion OFF : 114 batches, setPass 31
```

It was not culling anything meaningful in this open procedural world — it was pure correctness risk
for no measured gain.

## Batch attribution (controlled frozen frame, same camera pose, Editor)

### Before

```text
world + player + UI            116   (25 ground + 25 solid + 25 foliage + 25 solid outline mask + player/weapons/UI)
26 enemies                      88   (~3.4 per enemy)
TOTAL                          204   setPass 138
```

### After

```text
world + player + UI            114   (occlusion OFF)
27 enemies                      39   (~1.44 per enemy)
TOTAL                          153   setPass 65
```

`204 -> 153` batches with **one more enemy**, and `setPass 138 -> 65`.
Per-enemy cost fell **3.4 -> 1.44**, a 58% reduction.

## Three defects fixed

1. **VAT instancing was fully disabled by a texture in a property block.**
   `VAT_Animator.ApplyAnimationDataToMaterial()` wrote `_PositionTexture`, `_PositionMin` and
   `_PositionMax` into **each renderer's** `MaterialPropertyBlock`. Those are archetype constants, and
   a *texture* in an MPB cannot be instanced, so automatic GPU instancing was off entirely despite
   `enableInstancing = true` on the material. Verified before the fix: `DogPup_Mat._PositionTexture`
   was `NULL` on the material and supplied only per-renderer.
   Now written once to the shared archetype material via `sharedMaterial` (never `.material`, so no
   runtime material instances), guarded so it only writes when the value differs. The MPB now carries
   only genuinely per-instance values.
   The enemy shader already declares all five as `UNITY_DEFINE_INSTANCED_PROP`
   (`_CurrentAnimNormalizedTime`, `_PreviousAnimNormalizedTime`, `_AnimationBlendWeight`, `_HitFlash`,
   `_Dissolve`), so no shader change was needed.

2. **Outline mask pass never asked for instancing.** `BillOutlineFeature.LayerMaskPass` builds its
   `DrawingSettings` by constructor, which does not inherit the pipeline's instancing setting. Both
   the generic mask list and the VAT material-driven mask list now set `enableInstancing = true`
   explicitly. The VAT mask still uses the real VAT pass, so the silhouette still follows the animation.

3. **Blob shadows left the batch while fully alive.** Every spawn calls `SetDissolve(0)`, which wrote
   a non-instanced `_BaseColor`/`_Color` per renderer. Alive blobs now carry **no** property block at
   all (`SetPropertyBlock(null)`), which also clears stale alpha on pooled reuse; only an actively
   dissolving enemy temporarily leaves the shared batch.

## Reverted (R2 camera-aware coverage)

Removed because its only premise was the rejected root cause:

- `WorldStreamManager`: `CoverageOrigin`, `SetCoverageCamera`, split player/coverage update,
  `VerifyCoverageInvariant`, `[DefaultExecutionOrder(100)]`. Ring is centred on `CurrentChunk` again,
  one refresh per genuine chunk crossing, teleport still one refresh, 25 fixed roots.
- `GroundCoverage`: `SelectCoverageOrigin`, `OriginDecision`, `RequiredDiameterFor`, `AxisOrigin`.
- `WorldStreamingConfig`: `coverageGuardBand` and its validation.
- `ProductionWorldStreaming`: camera binding.
- Deleted `CameraAwareCoverageTests.cs` (22) and `CoverageOriginSelectionTests.cs` (15).

Kept, because it is a valid safety property independent of the rejected cause: `GroundCoverage` as a
diagnostic, and `GroundCoverageTests` covering chunk boundaries, negative coordinates, zero crossing
and teleport. Those now assert against `CurrentChunk`.

`PlayerChunk` remains as a self-documenting alias for `CurrentChunk`.

## Tests

```text
EditMode  502 / 502
PlayMode  212 / 212
```

## Not completed

- Production Run A / Run B full arcs.
- The <=150 crowded-frame target: measured **153** at 27 enemies. Close, not met, and not faked.
- Enemy batch count is **reduced but still scales with population** (~1.44/enemy). The remaining
  per-instance cost was not isolated to a specific pass — that needs Frame Debugger attribution,
  which was not performed.
- No 50/70-enemy sweep, no CPU/GPU frame timings, no mesh-memory figures.
- All numbers above are **Editor** measurements. No WebGL or device performance is claimed.

---

# CONTINUATION (2026-08-13) — Task 1 attribution: the linear pass is identified

## Method

One controlled frozen production frame: Bootstrap -> Menu -> PLAY -> Map_Level1, waves suspended,
`Time.timeScale = 0`, camera pinned at (0, 12, -8) euler (60,0,0), Occlusion Culling OFF, all enemies
pooled inactive then activated in a fixed grid inside the camera footprint. Same archetype
(`ENM_DogPup_VAT`) throughout, so archetype/material count is held at 1.

## Scaling sweep (same archetype)

```text
0  enemies : 115 batches   setPass 39   instanced   0
1  enemy   : 118 batches   setPass 42   instanced   3      (+3)
10 enemies : 128 batches   setPass 42   instanced  30     (+13)
30 enemies : 151 batches   setPass 45   instanced  90     (+36)
```

Slope between 10 and 30 enemies: **(36 - 13) / 20 = ~1.15 batches per additional enemy.**
SetPass is effectively flat (39 -> 45 across 0 -> 30 enemies), which confirms material count is not
growing — only draw count is.

## Category attribution at N = 30 (base 115, enemy total 36)

Measured by toggling one category at a time on the same frozen frame:

| category | measurement | draws for 30 enemies | per enemy |
|---|---|---:|---:|
| blob shadow | blobs off -> 143 | **8** | 0.27 |
| VAT OutlineSelectionMask | outline bits stripped -> 149 | **2** | 0.07 |
| VAT ForwardLit (body) | remainder | **26** | 0.87 |

**The linear pass is VAT ForwardLit.**

Two consequences worth stating plainly:

- **The blob shadow is exonerated by measurement.** It was the brief's strong suspect, but 30 blobs
  cost 8 draws — it is already batching. The earlier fix (alive blobs carry no property block) did its
  job. Per Task 3, blob rendering is therefore **not** touched: no H1 shader, no H2 dither fallback.
- **The outline mask is effectively flat** (2 draws for 30 enemies). The explicit `enableInstancing`
  fix did its job too.

## Why the body still costs ~0.87 draws/enemy

Two control experiments on the same frame:

```text
MPB cleared + VAT_Animator disabled : 235 batches, instanced 0     (WORSE)
light probes + reflection probes + shadow casting off : 173 batches (WORSE)
```

Removing the MaterialPropertyBlock **disables instancing entirely** and roughly doubles the enemy
cost. So the MPB-plus-instanced-properties path is what is working today, not what is blocking; the
earlier VAT fix (moving `_PositionTexture`/`_PositionMin`/`_PositionMax` off the per-renderer MPB and
onto the shared archetype material) is what turned instancing on at all (`instanced` 0 -> 90).

Per-renderer light/reflection probe data is **not** the splitter either.

What remains is that instancing merges only ~2.5 instances per batch instead of the whole crowd. That
final cause was **not** isolated. Frame Debugger per-draw inspection was not performed, so no claim is
made about it.

## Corrected wording from the previous report

- SetPass 138 -> 65 is a **~53% reduction**, not "halved and more".
- `3.4 -> 1.44 batches per enemy` is a separate per-enemy metric and must not be quoted as the
  overall reduction.

## Not done in this continuation

Tasks 2 and 4-8 were not performed: VAT constants still move to the shared material at runtime rather
than at bake time (transitional, as the brief notes); no mixed-roster 27/50/70 sweep; no WebGL 2 build
or `SystemInfo.supportsInstancing` check; no ground correctness matrix; no production runs; no
CPU/GPU timings. All numbers here are **Editor** measurements.

Repository check after the session: **no enemy VAT material asset was modified on disk** by the
runtime `sharedMaterial` write, and nothing is staged.

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
