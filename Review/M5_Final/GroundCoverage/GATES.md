# M5 Final Closeout — Ground coverage: gate → evidence map (2026-08-13)

## Headline

The reported defect — *"the gameplay camera exposes the edge/underside/background beyond the
generated ground"* — **did not reproduce** in the current build, and the measurement explains why it
cannot: with the authored production camera the visible ground footprint is **13–23 m across**, while
the 5×5 ring guarantees **≥ 42.7 m of spare ground in the worst direction**, at every aspect ratio and
in both projections.

The measurement also found what the coverage is *actually* sensitive to, and it is **not the ring
radius**: it is **camera pitch**. Below ~31° of pitch the camera sees the horizon, and at that point
**no finite ring size can cover the view**. Increasing `renderRadius` — the instinctive fix — would
have bought nothing.

No streaming, pool, ring or camera production code was changed.

---

## CP2 — Reproduction attempts (all through Bootstrap → Menu → PLAY → Map_Level1)

| # | Condition | Result |
|---|---|---|
| 1 | Perspective, spawn, portrait 1080×1920 | ground fills frame — `persp_spawn_z50.png` |
| 2 | Orthographic size 10, player on a chunk min-corner | ground fills frame — `ortho10_chunk_low_edge.png` |
| 3 | Perspective, chunk corner (96.02, 64.02) — thinnest −X/−Z coverage | ground fills frame — `runA_persp_chunk_corner.png` |
| 4 | Perspective, after a long jump to a fresh non-overlapping ring | ground fills frame — `runA_deep_negative_teleport.png` |

`runA_persp_chunk_corner.png` shows a bluish band across the top of the screen. It is **not** exposed
background. A per-row ray probe through the live gameplay camera resolves every viewport row onto
enabled ground:

```text
viewportY=1.00  dir=(0.000,-0.500,0.866)  t=23.9  hit=(96.0,0.0,76.8)  chunk=(3,2) active=True groundOn=True
viewportY=0.90  dir=(0.000,-0.577,0.817)  t=20.7  hit=(96.0,0.0,73.0)  chunk=(3,2) active=True groundOn=True
viewportY=0.50  dir=(0.000,-0.866,0.500)  t=13.8  hit=(96.0,0.0,62.9)  chunk=(3,1) active=True groundOn=True
viewportY=0.00  dir=(0.000,-1.000,0.000)  t=11.9  hit=(96.0,0.0,56.0)  chunk=(3,1) active=True groundOn=True
```

The top row of pixels lands on ground 23.9 m away, inside an active chunk with its renderer enabled.
The band is post-processing, not missing geometry.

---

## CP3 — The measured camera/ground contract

Live production state, player at a chunk corner (96.02, 0, 64.02):

```text
camera        pos (96.02, 12.00, 56.02)  euler (60, 0, 0)  fov 60  aspect 0.5625  far 100  clear Skybox
footprint     X[-6.7, 6.8]  Z[-7.8, 13.0]   (13.5 m x 20.8 m)   seesHorizon = False
coverage      160 m x 160 m centred on chunk (3,2)
MARGIN        56.0 m        guard band (32 m) satisfied
ground        25 enabled / 25 leases,  0 disabled,  0 holes,  0 duplicate coords
renderer      bounds match ChunkCoord.ToWorldMin exactly on all 25 (mismatched = 0/25)
parent chain  ChunkPool / StreamingWorld / ProceduralWorld all at origin, scale 1 — no hidden offset
```

Worst case over a full sub-chunk sweep (every 1 m offset inside one chunk), both projections, three
aspects:

| projection | aspect | worst margin | worst position |
|---|---|---:|---|
| perspective fov 60 | portrait 1080×1920 | **52.22 m** | offset (0,31) |
| perspective fov 60 | tall 1080×2340 | **52.22 m** | offset (0,31) |
| perspective fov 60 | landscape 1920×1080 | **42.67 m** | offset (0,13) |
| orthographic size 10 | portrait 1080×1920 | **51.38 m** | offset (0,0) |
| orthographic size 10 | tall 1080×2340 | **51.38 m** | offset (0,0) |
| orthographic size 10 | landscape 1920×1080 | **46.22 m** | offset (0,0) |

Every value exceeds a full one-chunk (32 m) guard band. The diagnosis category is therefore **none of
1–7** — required-set coverage, ring size, streaming target, renderer enable state, bounds/culling,
mesh placement and material are all measured correct. It is category **8: the reported cause is not
present in the current build**.

### What coverage IS sensitive to

Same worst-case player placement, sweeping the camera instead of the ring:

```text
orthographic size :  10 -> 51.4 m   20 -> 39.8 m   30 -> 30.2 m*  47 -> 15.4 m   60 -> 4.2 m
perspective pitch :  60 -> 56.0 m   45 -> 51.0 m   40 -> 35.9 m   35 -> 4.0 m*   30 -> HORIZON
                                                                     (* guard band lost)
```

Pitch is the cliff. At 30° and below the frustum's top edge rises above the horizon, `SeesHorizon`
becomes true, and the view can no longer be covered by *any* ring size. Authored pitch is 60°, which
sits far from that cliff — but a camera change is what would reintroduce this defect, not a
streaming change.

---

## Decision Gate A — chosen fix

**None of the streaming branches applies.** Active ground is sufficient, the renderer state is
correct, the target is bound correctly and the ring is neither mis-centred nor refreshed late.
Per the brief's own instruction not to jump to a larger ring without proof, `renderRadius` stays
at 2 and `PoolCapacity` stays at 25.

What was added is the missing *proof*, not a behaviour change:

- `Assets/_Project/Scripts/Runtime/World/Streaming/GroundCoverage.cs` — a pure, allocation-free
  calculator (`ComputeFootprint` / `MarginToRing` / `IsFullyCovered` / `TouchedChunks`) that derives
  the footprint from real frustum rays intersected with the gameplay plane. It reads no scene state
  and changes nothing; `WorldStreamManager`, `ChunkPool`, `ChunkInstance` and `CameraFollow` are
  untouched.
- `Assets/_Project/Scripts/Tests/PlayMode/GroundCoverageTests.cs` — 22 tests that lock the visible
  invariant so that any future camera or config change which enters the unsafe envelope fails here
  instead of in a playtest.

`SeesHorizon` is deliberately a hard failure rather than a large footprint: a horizon-visible camera
is unbounded, and reporting it as "needs a bigger ring" is exactly the wrong diagnosis.

---

## CP4 — Automated coverage tests: 22 discovered, 22 executed, 22 passed

Covering the brief's list: perspective (1), ortho size 10 (2), portrait/tall/landscape (3–5), the four
cardinal traversals and both diagonals driven per-frame through the real `LateUpdate` path (6–7),
deep positive and deep negative coordinates (8–9), zero crossing (10), either side of a refresh
threshold including the negative-side floor case (11), non-overlapping teleport (12), stationary
no-repeat-refresh (13), duplicate coords/leases (14), exact active ground count (15), point-by-point
coverage of the footprint (16), guard band (17), exactly one shared collider (18), ground immediate
before decoration runs (19), and decoration tier correctness on retained chunks after an origin
shift (20).

Test 16 samples a 13×13 grid **inside** the footprint rather than comparing two rectangles — a hole in
the middle of the ring leaves the outer bounds intact, so a bounds-only check would miss the worst
failure mode.

`TheCoverageCalculationFailsWhenItShould` is a negative control: it asserts the calculator reports
failure for a horizon-facing camera, for a 400 m-wide footprint on a 160 m ring, and for a ring
centred 1200 m away. Without it the other 21 assertions would also pass against a function that
always returned "covered".

**No test asserts `RenderRadius == 2`.** The radius is a means; the invariant is the contract.

---

## Streaming invariants (measured live, Map_Level1)

```text
WorldStreamManager      1
pooled chunk roots      25          leases 25        distinct coords 25       holes 0
renderers per chunk     3 (Ground / SolidDecor / Foliage)
ground renderers on     25 / 25
shared gameplay collider 1          decoration colliders 0
traversal Instantiate/Destroy       0
refresh while stationary            0 over 4.5 s pinned, and 0 over 60 frames in test 13
last refresh in/out                 5 in / 5 out on a one-chunk origin shift
teleport refresh                    25 in, single refresh, no intermediate frame
```

Boundary loitering does swap a full 5-chunk row per crossing (5 in / 5 out). Ground is immediate on
assignment, so this is decoration/CPU churn, not a visible hole — recorded as an observation for M6
tuning, not a regression, and not fixed here because nothing measured shows it costing a frame.

---

## CP1 — Recovered verification

`CrowdSeparationContractTests` is now genuinely in the assembly and genuinely ran:

```text
_Project.Tests sourceFiles = 24, containing CrowdSeparationContractTests.cs
type ZombieWar.Tests.CrowdSeparationContractTests resolved from the loaded assembly
test methods reflected = 9
discovered 9 | executed 9 | passed 9 | failed 0 | skipped 0   (22.1 s)
no CrowdSeparationTests type remains anywhere in the AppDomain
```

The previous report's 181/181 is **not** quoted as covering these — it could not have.

---

## Crowd contract, corrected

`CROWD MOTION: USER PASS` (owner playtest).

The old universal live **0.4 m pairwise spacing** claim is **superseded and withdrawn**. Live
measurement of a 24-enemy crowd converging on one stationary player recorded `minGap` of 0.01–0.12 m;
the 0.4 m figure was never true of production, only of a two-agent synthetic case.

The accepted contract is now behavioural: no complete sustained stacking; no push-pull hopping; no
synchronised crowd surging; no full-speed attack-ring sliding; chasing enemies keep reaching the
player; pooled enemies inherit no stale steering. Slight visual overlap inside a dense horde is
accepted by the owner.

The synthetic suite keeps its small non-degenerate spacing guards. They are **not** evidence of a
universal live 0.4 m minimum and are no longer described as such.

---

## Protected assets

```text
VolumeProfile   11263ac5694eaa134faba8e6561fdd9232ceea9e   unchanged
DecorationPalette 2a5ade120b5f2ebeb06d670976ce5a729e813143 unchanged
Map_Level1      43912f0ffacc2ca29e75f9bb50a5e2735e1fc31f   unchanged
staged files    0
```

The orthographic camera settings used for reproduction were applied **in Play Mode only** and were
reverted before exiting; no scene or prefab was saved.

---

## Open

- **Full PlayMode does not pass.** 212 tests, one failure, reproduced twice:

  ```text
  run 1: TwoAgentsWithTheSameDestination_DoNotEndUpStacked  gap 0.395 m  (expected > 0.400)
  run 2: TwoAgentsWithTheSameDestination_DoNotEndUpStacked  gap 0.320 m  (expected > 0.400)
  isolation (PlanarSteeringTests alone): 15 / 15 passed
  ```

  Not a flake and not caused by this task — no motor, steering or separation code was touched.
  The mechanism is a pre-existing frame-rate dependence: `PlanarEnemyMotor` clamps its approach to
  the separation target with `separationMaxBlendPerSample = 0.2`, so the *fraction* of the gap closed
  per sample is capped no matter how long the frame was. Under the heavier 212-test suite the editor
  runs fewer, longer frames, the smoothed separation ramps up more slowly in wall-clock terms, and
  the test's fixed frame budget measures an under-converged gap — 0.395 m under moderate load,
  0.320 m under heavier load, 0.4 m+ when it runs alone.

  Adding 31 tests to PlayMode **exposed** this; it did not create it. Deliberately left alone on both
  sides: the assertion is not weakened (standing instruction), and the motor is not retuned (scope
  lock — the recreated crowd suite passes and the owner recorded `CROWD MOTION: USER PASS`, so
  retuning on a synthetic metric is exactly what this task must not do). It needs an owner decision
  in M6: either make the test frame-rate independent, or lift the per-sample blend cap.
- Production runs A and B did not complete their full scripted arcs; see the closeout report.
- Audio re-verification this session is partial: 22 pistol pulls, 22 requested, 22 accepted,
  0 dropped on the guaranteed-transient path.
