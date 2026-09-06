# VAT ForwardLit batch-break diagnosis (2026-08-13)

## Correction first: the previous sweep was contaminated

The earlier "1 / 10 / 30 same-archetype DogPup" sweep was **not same-archetype**. Enemies were picked
from the pool by instance-id order, and the pool holds four archetypes. Inspecting the live set that
had been reported as "30 DogPup" showed:

```text
18 x ENM_Skeleton_VAT   (Skeleton_Mat  matId 60660, Skeleton_Mesh meshId 60666)
12 x ENM_DogBark_VAT    (DogBark_Mat   matId 60228, DogBark_Mesh  meshId 60234)
distinctMeshIds = 2, distinctMaterialIds = 2
```

So the previously reported **~1.15 batches/enemy is withdrawn** — it mixed two archetypes, and two
archetypes cannot batch together by definition. Everything below is re-measured on a genuinely single
archetype (`ENM_Skeleton_VAT`, 40 available in pool), verified live as
**distinctMesh = 1, distinctMaterial = 1**.

## Controlled frame

Bootstrap → Menu → PLAY → Map_Level1, waves suspended, `Time.timeScale = 0`, camera pinned,
Occlusion Culling OFF, enemies activated from the pool onto a fixed grid inside the camera footprint.

## Same-archetype scaling (clean)

```text
N=0  : 111 batches   setPass 45   instanced   0
N=1  : 115 batches   setPass 48   instanced   3    (+4)
N=10 : 124 batches   setPass 49   instanced  30   (+13)
N=30 : 144 batches   setPass 49   instanced  90   (+33)
N=40 : 154 batches   setPass 49   instanced 120   (+43)
```

Slope, measured on three independent intervals:

```text
 1 -> 10 : (13 - 4)  / 9  = 1.00 batches per enemy
10 -> 30 : (33 - 13) / 20 = 1.00 batches per enemy
30 -> 40 : (43 - 33) / 10 = 1.00 batches per enemy
```

**Exactly +1.00 batch per additional enemy**, perfectly linear, with SetPass flat at 49 from N=1 to
N=40 (so material/variant count is genuinely not growing).

`instanced` is exactly `3 x N` at every step — every enemy's three passes are counted as
instancing-path draws, yet the batch count still rises one-for-one. This confirms the brief's warning:
**`instanced = 90` never proved effective grouping.** The instancing path is being taken and is
producing batches that hold ~1 instance each.

## What has been positively excluded

| candidate | evidence | verdict |
|---|---|---|
| mesh identity | `distinctMeshIds = 1` live at N=30 | not the cause |
| material identity | `distinctMaterialIds = 1` live; no runtime clones | not the cause |
| immutable data leaking into MPB | `_PositionTexture`, `_NormalTexture`, `_MainTex`, `_BaseMap`, `_PositionMin`, `_PositionMax` — **NONE** present in any of the 30 blocks | not the cause |
| non-uniform MPB layout | all 30 blocks have the identical property set `CPBHD` (`_CurrentAnimNormalizedTime`, `_PreviousAnimNormalizedTime`, `_AnimationBlendWeight`, `_HitFlash`, `_Dissolve`) | not the cause |
| shader instancing contract | ForwardLit has `#pragma multi_compile_instancing`; all five values declared `UNITY_DEFINE_INSTANCED_PROP` and read via `UNITY_ACCESS_INSTANCED_PROP`; none duplicated in `CBUFFER_START(UnityPerMaterial)`; OutlineSelectionMask / ShadowCaster / DepthOnly / DepthNormals all carry the pragma too | no violation found |
| SetPass / variant growth | flat at 49 across N=1..40 | not the cause |
| light + reflection probes, shadow casting | previously toggled **together**, which is why that experiment was inconclusive; it made the total worse (173) and is not quoted as a cause | inconclusive, retired |
| MPB removal | clearing the block and disabling `VAT_Animator` produced **235 batches, instanced 0** — strictly worse | MPB is enabling instancing, not blocking it |

## Not yet determined

The specific batch-break reason is **still unproven**. What is established is narrow and reliable:
grouping fails at ~1 instance per batch under conditions where mesh, material, shader variant, MPB
layout and SetPass are all verified uniform.

Frame Debugger per-draw inspection (Task 1's event-by-event table: instance count per draw, Unity's
reported break reason) **was not performed** — it is a GUI-only tool and could not be driven from this
session. That table remains the missing evidence, and no cause should be acted on until it exists.

The isolation matrix V0–V4 (Task 2) was likewise not run as a separate harness; the exclusions above
were obtained on the production stack instead.

## Explicitly not changed

- Blob shadows: measured at 8 draws for 30 enemies. Untouched, per the brief.
- Outline mask: measured at 2 draws for 30 enemies. Untouched.
- No shader rewrite: no contract violation was found, so none was justified.
- VAT constants still move to `sharedMaterial` at runtime (Task 8 not done).

---

# V0–V4 ISOLATION MATRIX (2026-08-13) — cause found, and my previous exoneration was WRONG

Harness: 30 bare GameObjects (MeshFilter + MeshRenderer only) built in memory during Play, using the
production `Skeleton_Mesh` + `Skeleton_Mat`, same camera, same frozen frame, Occlusion OFF. Nothing
was written under `Assets/`; the harness was destroyed before leaving Play Mode.

Base (0 enemies, no harness): **116 batches / 32 SetPass**.

| case | MPB contents | batches | delta for 30 | instanced | verdict |
|---|---|---:|---:|---:|---|
| V0 | none | 176 | **+60** (2.00 each) | 0 | no instancing at all |
| V1 | `_CurrentAnimNormalizedTime` | 118 | **+2** | 60 | fully instanced |
| V2 | + `_PreviousAnimNormalizedTime`, `_AnimationBlendWeight` | 118 | **+2** | 60 | fully instanced |
| V3 identical | + `_HitFlash`, `_Dissolve` (same values) | 118 | **+2** | 60 | fully instanced |
| V3 varied | all five, **different per renderer** | 118 | **+2** | 60 | fully instanced |
| V4 | real production stack | 149 | **+33** | 90 | breaks |

**First breaking transition: V3 → V4.** The MaterialPropertyBlock is completely exonerated — even
thirty *different* per-instance value sets batch into 2 draws. V0 also shows that removing the MPB
makes things worse (no instancing at all), confirming the earlier observation.

## Isolating V4, one variable at a time

Production VAT body renderer state was first verified **uniform across all 30**:
`shadowCast=Off, receiveShadows=False, layerMask=5, lightProbeUsage=Off, motionVectors=ForceNoMotion,
rendererPriority=0, staticFlags=0, lossyScale=(1,1,1) uniform, non-negative`.

| single toggle from full V4 (149) | batches | delta |
|---|---:|---:|
| `shadowCastingMode = Off` | 149 | 0 (already Off — no-op) |
| `VAT_Animator.enabled = false`, MPB retained | 149 | 0 |
| **blob shadow renderers disabled** | **119** | **−30** |
| blobs off **and** outline bit stripped | 118 | −31 |

## Root cause

```text
M_BlobShadow
shader     : Universal Render Pipeline/Unlit
renderQueue: 3000  (TRANSPARENT)
instancing : enabled (but irrelevant here)
one shared material, one shared quad mesh
```

**The blob shadow is the linear pass: 30 draws for 30 enemies, exactly 1.00 per enemy.**

Transparent geometry is sorted back-to-front per object, so Unity submits each blob as its own draw
regardless of `enableInstancing`. With blobs disabled, the entire production enemy stack collapses to
**+2 draws for 30 enemies** — identical to the V3 harness. VAT ForwardLit is therefore **already fully
instanced in production**; it was never the defect.

## Correction to the previous report

The previous session concluded *"blob shadows are exonerated: 8 draws for 30 enemies"* and
*"the linear pass is VAT ForwardLit"*. **Both statements are withdrawn.** That measurement was taken
on the contaminated mixed-archetype frame (18 Skeleton + 12 DogBark), where the two archetypes could
never batch and the residual was misattributed to ForwardLit. Re-measured cleanly on a single
archetype with single-variable toggles, the attribution reverses:

```text
VAT ForwardLit          : +2 draws / 30 enemies   (fully instanced)
VAT OutlineSelectionMask: +1 draw  / 30 enemies
blob shadow             : +30 draws / 30 enemies  <-- the linear pass
```

The brief's original strong suspect (`M_BlobShadow`, Queue = Transparent 3000) was correct all along.

## Fix not applied

The H1 route (project-owned instanced transparent blob shader with a per-instance `_InstanceAlpha`)
and the H2 fallback (alpha-clipped/dithered opaque blob) are both now justified by evidence, but
neither was implemented in this session. Nothing was changed in production this round.
