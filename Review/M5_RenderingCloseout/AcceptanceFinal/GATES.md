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
