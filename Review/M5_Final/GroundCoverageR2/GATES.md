# M5 Final Closeout R2 — camera-aware ground coverage (2026-08-13)

## Original defect record

```text
OWNER-REPORTED INTERMITTENT DEFECT
NOT REPRODUCED IN THE CURRENT AUTOMATED SESSION
```

The R1 session could not reproduce the owner's report through automation. That result is recorded as
*not reproduced*, **not** as *does not exist*. What R1's measurements actually proved is narrower:

- the authored camera footprint fits inside the current 5×5 ring, with 42.67 m to spare in the worst
  tested configuration;
- a camera that can see the horizon cannot be covered by **any** finite ring, so pitch is an
  architectural risk in its own right;
- blindly increasing `renderRadius` was not justified.

**Pitch sensitivity was never proven to be the owner's root cause**, and R1's wording that leaned that
way is withdrawn. What R1 did establish is that the architecture had a real hole regardless of
reproduction: the ring reacted **only to player chunk changes** and never asked what the camera could
actually see. R2 closes that hole rather than continuing to chase the exact manual movement pattern.

### Invalid prior evidence, retired

```text
Review/M5_Final/GroundCoverage/ortho10_chunk_low_edge.png   INVALID
```

That capture shows the `RUN OVER` result screen, not orthographic gameplay coverage — the player had
died while the camera was being reconfigured. It is retired and must not be cited as orthographic
gameplay evidence. Replacement captures in this folder are written only after an explicit gameplay
assertion passes (see *Capture guard* below).

### `GroundCoverage` utility truth

Before R2, `GroundCoverage` was called only by tests and diagnostics; production streaming never used
it. The class comment claiming production and tests shared one calculation was **overstated** and has
been corrected in place.

After R2 the claim is true: `WorldStreamManager.ResolveCoverageOrigin()` calls
`GroundCoverage.ComputeFootprint` and `GroundCoverage.SelectCoverageOrigin` every frame, and the tests
call the same two functions.

---

## Coverage architecture

### Two coordinates, deliberately distinct

| | `PlayerChunk` (= `CurrentChunk`) | `CoverageOrigin` |
|---|---|---|
| meaning | chunk containing the player | centre of the 5×5 rendered ring |
| drives | decoration near/far tier, `DecorationScheduler` priority, shared collider recentre, debug labels | required chunk coordinates, early prefetch, safe release, coverage diagnostics |
| changes when | the player crosses a chunk boundary | the camera footprint + guard would leave current coverage |

`CurrentChunk` keeps its original meaning and every existing caller. `PlayerChunk` was added as a
self-documenting alias returning the same value. Nothing silently re-pointed at `CoverageOrigin`.

### Footprint method

Real frustum-border rays intersected with the gameplay plane (`GroundCoverage.ComputeFootprint`).
Perspective and orthographic both work because the rays come from `Camera.ViewportPointToRay`, which
already encodes projection, aspect, position and rotation. A ray travelling up or parallel sets
`SeesHorizon`, which is treated as **unbounded/invalid** rather than "a large footprint" — the
distinction separates *needs more chunks* from *needs a different camera*.

Border-only sampling (`samplesPerEdge = 8` → 32 border samples) is retained rather than reduced to
four corners. Cost is 32 struct-only ray/plane intersections per frame, no allocation and no physics.
Four corners are sufficient for the authored camera, but the border stays correct if a future camera
has roll or a non-centred projection matrix, where an edge midpoint can be the extreme. Measured
allocation for 2000 full `ComputeFootprint` + `SelectCoverageOrigin` pairs is asserted below 16 KB by
`LongTraversal_CreatesNothingAndAllocatesNothingInSteadyState`.

No wall-clock cost is claimed here — it was not profiled.

### Guard band: 32 m, and why

Configurable as `WorldStreamingConfig.coverageGuardBand`, default **32 m** (exactly one chunk).

The ring is 160 m but **grid-aligned to 32 m**, so it only guarantees containment of an arbitrarily
placed span of `160 − 32 = 128 m`, not 160 m. With the widest measured footprint (~42.6 m, landscape),
the largest feasible guard is `(128 − 42.6) / 2 ≈ 42.7 m`. 32 m sits below that with ~21 m of
headroom, and absorbs conservatively:

| source of slack | magnitude |
|---|---|
| max camera shake offset (`CameraFollow.maxShakeOffset`) | 0.5 m |
| `SmoothDamp` follow lag | sub-metre at normal speed |
| one frame of travel | < 1 m |
| float boundary error (`BoundaryEpsilon`) | 0.001 m |

A wider guard does **not** increase shift frequency — frequency follows distance travelled; the guard
only decides how *early* the shift happens.

`WorldStreamingConfig.Validate` now rejects `2 × guard >= (ringSpan − chunkSize)`, comparing against
the arbitrary-placement span rather than the full ring. Comparing against the full ring would pass a
configuration that can never actually cover.

### Hysteresis / containment rule

The current origin *is* the hysteresis state. `SelectCoverageOrigin` retains it whenever
footprint + guard is still contained — explicitly including the case where the player has moved to a
different chunk and some other origin is "closer". Only when containment would fail does it move, and
then to the **valid origin nearest the current one, per axis**, so only the overflowing axis shifts and
only by the minimum number of chunks.

`MovingBackAndForthAcrossTheOldThreshold_DoesNotOscillate` drives 20 alternating crossings of the exact
threshold just vacated and asserts zero further shifts.

### Update order

`WorldStreamManager` carries `[DefaultExecutionOrder(100)]`, so its `LateUpdate` runs after
`CameraFollow` (order 0) has written the final camera pose. Two unrelated `LateUpdate` methods have no
guaranteed order, so this is stated explicitly rather than assumed. Nothing global was introduced.

`ProductionWorldStreaming` binds the camera **before** `Initialize()`, because `Initialize` builds the
first ring inside that call; binding afterwards would leave the very first frame player-centred.

The bound camera is the one carrying `CameraFollow`, not `Camera.main` — the MainCamera tag can sit on
a menu or character-stage camera, whereas the follow camera is by definition what the player sees.

### Shift rule and release/assign ordering

Release-then-lease is retained and is safe by construction: released coordinates lie outside the
**new** ring, and the new ring contains footprint + guard, so anything switched off is already outside
the visible and safety region. Both loops run inside one synchronous `RebuildRing()` call with no frame
boundary between them, so no frame can exist with missing ground. Pool capacity is never exceeded,
which a lease-before-release ordering could not guarantee for a non-overlapping teleport.

Expected set difference: 5 out / 5 in for one-axis movement, 9 out / 9 in for a diagonal shift,
25 out / 25 in for a non-overlapping teleport.

### Runtime safety check

`VerifyCoverageInvariant()` is `[Conditional("UNITY_EDITOR")]` / `[Conditional("DEVELOPMENT_BUILD")]`,
so it compiles out of release builds entirely. It walks only the chunks the footprint touches (a
handful) and does dictionary lookups — no hierarchy scan, no allocation. It reports two **distinct**
failures:

- coordinate in view with **no lease** → coverage is wrong (next frame shifts the origin);
- coordinate leased but renderer/mesh/active invalid → **pool/renderer** invariant failure, explicitly
  labelled as *not* a radius problem.

### Failure when the footprint cannot fit

`OriginDecision.FootprintTooLarge` logs the measured span, the ring span, and the minimum diameter that
would be required — then falls back to the player chunk. It never silently grows the radius.

---

## Two real bugs the new tests caught

Both were in code written earlier in this same closeout, and both were found by the new suite rather
than by inspection:

1. **`default(Footprint)` was "valid".** A default struct has `SeesHorizon = false` and four zeros, so
   `IsValid` returned true and described a degenerate rectangle at the world origin. The safety check
   then demanded ground at chunk (0,0) from anywhere on the map. Fixed with an explicit
   `Footprint.Invalid` sentinel.
2. **`isActiveAndEnabled` was the wrong camera test.** `Camera.enabled` only controls whether a camera
   renders; its projection is readable either way. Requiring it silently dropped coverage onto the
   player-centred fallback — which has no hysteresis — and the ring oscillated at chunk boundaries
   exactly as before. Measured: **41 shifts where 1 was expected**. Fixed to a null check only.

---

## Steering test closeout

| | old | new |
|---|---|---|
| name | `TwoAgentsWithTheSameDestination_DoNotEndUpStacked` | `TwoAgentsWithTheSameDestination_SeparateInsteadOfStacking` |
| contract | gap > **0.4 m** | gap > **0.15 m**, gap grew materially, no full-speed churn, settles when stopped, no stale pooled state |
| termination | fixed **90 frames** | elapsed **simulation time** + settle condition |

This is a **deliberate contract replacement, not a hidden assertion weakening**. 0.4 m was never
production behaviour — a live 24-enemy crowd converging on a stationary player measured `minGap`
0.01–0.12 m — and the owner has visually approved slight overlap (`CROWD MOTION: USER PASS`). The
0.15 m threshold matches the non-degenerate guard already used by
`CrowdSettlesIntoAStableRing_WithoutStacking`.

The fixed 90-frame budget was the other half of the problem: separation converges per **sample**, and
`separationMaxBlendPerSample = 0.2` caps how much of the gap closes per sample, so under Editor load
(fewer, longer frames) 90 frames measured an unconverged state — 0.395 m then 0.320 m in full-suite
runs, while passing alone.

Writing the replacement surfaced two facts worth recording, both measured rather than assumed:

- **Two agents sharing a destination never come to rest.** The destination pulls them in, separation
  pushes them out, and they stabilise into a mutual jostle at ~2.7 m/s (~54 % of max) indefinitely.
  Requiring "at rest" here would assert something production never promised, so the moving phase
  asserts only *not full speed*.
- **The state production actually uses is `IsStopped`** — enemies stop to attack. The test now adds a
  second phase that sets `IsStopped` on both and asserts they settle *and* keep their separation, which
  is the state the player actually sees in the ring around the character.

**Production motor/VAT changed: NO.**

---

## Structural acceptance

```text
active roots: 25          active leases: 25        distinct coordinates: 25
enabled ground renderers: 25                       renderers per chunk: 3
shared materials: 3 (ground / solid / foliage)     shared gameplay colliders: 1
decoration colliders: 0
roots created after warmup: 0    meshes created after warmup: 0
roots destroyed during traversal: 0
duplicate active coordinates: 0
steady-state coverage allocation: < 16 KB per 2000 calculations
```

Asserted by `AssertPoolInvariants` on every traversal step of every walk test, and after teleport,
deep-negative and horizon-fallback cases.

---

## Files changed

```text
Assets/_Project/Scripts/Runtime/World/Streaming/GroundCoverage.cs        (SelectCoverageOrigin, OriginDecision, Footprint.Invalid, comment correction)
Assets/_Project/Scripts/Runtime/World/Streaming/WorldStreamManager.cs    (CoverageOrigin, split update, camera, safety check, execution order)
Assets/_Project/Scripts/Runtime/World/Streaming/WorldStreamingConfig.cs  (coverageGuardBand + validation)
Assets/_Project/Scripts/Runtime/World/Streaming/ProductionWorldStreaming.cs (camera binding)
Assets/_Project/Scripts/Tests/EditMode/CoverageOriginSelectionTests.cs   (new, 15 tests)
Assets/_Project/Scripts/Tests/PlayMode/CameraAwareCoverageTests.cs       (new, 22 tests)
Assets/_Project/Scripts/Tests/PlayMode/PlanarSteeringTests.cs            (contract replacement)
Assets/_Project/Scripts/Tests/PlayMode/GroundCoverageTests.cs            (assert against CoverageOrigin)
```

---

## Runtime result (live, Bootstrap -> Menu -> PLAY -> Map_Level1)

### The architecture working, measured in a real run

```text
coverageCamera bound      Main Camera (the CameraFollow camera)
playerChunk (-1,-1)  coverageOrigin (0,0)   decision Retained
refresh 1            playerChunkOnlyUpdate 22
```

22 player chunk crossings were absorbed with **one** ring build. Under the pre-R2 architecture each of
those 22 would have swapped a full 5-chunk row. This is the churn the split removed.

### Guarded captures

Every capture below asserted, before writing: gameplay scene active, terminal overlay inactive, player
alive, camera mode matches the label, coverage invariant true, 25 leases, 25 enabled ground renderers.
One capture attempt was **rejected** by this guard (player dead / result overlay up) and no image was
written — the exact failure mode that produced R1's invalid screenshot.

| capture | player | playerChunk | coverageOrigin | decision | margin | in/out |
|---|---|---|---|---|---:|---|
| `persp_before_shift.png` | (16, 16) | (0,0) | (0,0) | Retained | 67.2 m | – |
| `persp_after_shift.png` | (16, 55) | (0,1) | (0,1) | Retained | 60.2 m | 5 / 5 |
| `ortho10_before_shift.png` | (16, 40) | (0,1) | (0,1) | Retained | 59.4 m | – |
| `ortho10_after_shift.png` | (16, 96.4) | (0,3) | **(0,2)** | Retained | 53.1 m | 5 / 5 |
| `persp_negative_coords.png` | (-140.5, -212.5) | (-5,-7) | **(-4,-6)** | Retained | 35.5 m | – |

The last two rows are the point of the whole change: `playerChunk` and `coverageOrigin` are genuinely
different coordinates, and the ring stays put because coverage still holds. The old architecture had no
way to express that state.

One-axis shifts moved exactly 5 out / 5 in, as predicted.

### Structural acceptance, live

```text
active roots 25        active leases 25       distinct coordinates 25
enabled ground renderers 25
roots created after warmup 0       roots destroyed during traversal 0
ground meshes created after warmup 0   decoration meshes created after warmup 0
pool recycles 45
```

### Not completed

- **Production Run A / Run B were not completed as scripted.** The coverage stress sections ran and are
  evidenced above, but the full arcs (weapon rotation, bomb + level-up, Victory, Defeat, return to Hub,
  second-run cleanliness) were not driven end to end. The player died repeatedly during instrumented
  movement, and each restart consumed a run.
- **Audio counters were not re-measured in R2.** The standing figures are R1's partial pistol window
  (22 pulls, 22 requested, 22 accepted, 0 dropped on the guaranteed-transient path, 0 stale voices) plus
  M5.1.3's fuller set. The required 60/60/10s windows were not run.
- **No performance before/after profile was captured.** Renderer, batch, mesh-memory and frame timings
  were not measured, so no claim is made. Structural counters above are unchanged by construction
  (same pool, same renderers, same materials).
