# M5.1.3 Final Closeout — Gate → Evidence map (2026-08-13)

## Headline

The 15 FPS "failure" was a **measurement defect, not a motor defect**. Re-measured with a corrected,
frame-rate-independent metric on a controlled production crowd, the gate **passes**, so
`PlanarEnemyMotor` and `PlanarSteeringWorld` were **not modified** in this closeout (Decision Gate A).

Two further defects were found in my own prior verification work and are recorded below: a crowd test
file that Unity never compiled (so eight assertions had never run), and a VAT phase figure computed
against the wrong denominator.

---

## CP1 — The old 15 FPS metric, audited

Old harness (inline probe, since discarded):

```csharp
foreach (enemy) {
    lat = Dot(motor.Velocity, right);
    if (|lat| > 0.15f) { if (sign != lastSign[instanceId]) reversals++; lastSign[id] = sign; }
}
report = reversals / 6f          // <-- wall-clock seconds only
```

Signal sampled: **actual motor velocity**, projected onto the axis perpendicular to the direction to
the player. Per enemy per frame, summed into ONE crowd-wide counter.

Defects, in order of severity:

1. **No population normalization.** The counter was summed across the whole crowd and divided only
   by wall-clock seconds. It was therefore *crowd-total reversals per second*, never per enemy. The
   compared windows did not even hold population constant (25–70 enemies). This alone explains how
   the figure "exceeded the available 15 samples per second": with ~30–45 enemies, 26.5 crowd-total
   reversals/s is ~0.6–0.9 per enemy per second, which is inside the sampling budget.
2. **No dwell requirement.** A single-sample sign flip counted. At 15 FPS each sample integrates
   four times more velocity change, so noise crossings of the 0.15 deadband are far likelier per
   sample — the metric was structurally biased against low frame rates.
3. **Pooled-id contamination.** Per-enemy state was keyed by `GetInstanceID()` in a dictionary that
   outlived pool reuse, so a recycled enemy inherited the previous occupant's sign and produced a
   phantom reversal on its first sample.
4. **Mixed populations.** Chasing and stopped/attacking enemies were combined into one unexplained
   number.
5. **Deadband on raw velocity only** (0.15 m/s), with no separation of "moving" from "jittering".

### Corrected formula

```text
qualified reversal :=
    |lateral| > 0.25 m/s                     (deadband)
    AND sign(lateral) != committed sign
    AND the new sign persists >= 0.15 s      (dwell, time-based => frame-rate independent)

rate := total qualified reversals / total observed enemy-seconds
enemy-seconds := sum over enemies of dt while that enemy is active and in the measured population
```

Per-enemy state is held on direct object references (no id keys), so pool reuse cannot contaminate
it. Chasing and stopped populations are reported separately as well as combined.

---

## CP2 — Controlled 60/30/15 FPS protocol

Identical scenario at each rate: 24 production `ENM_DogPup_VAT` on a deterministic ring (r = 3 m)
around a stationary player at the origin, live waves suspended, player weapon disabled so the crowd
survives all windows, 8 s per window, settings restored after each.

| window | qualified reversals | enemy-seconds | **rate /enemy-s** | p95 per-enemy | latAccel mean/p95 | latJerk mean/p95 | minGap | maxStoppedDisp |
|---|---:|---:|---:|---:|---|---|---:|---:|
| 60 FPS, all | 56 | 147.9 | **0.379** | 0.973 | 3.04 / 5.53 | 87 / 143 | 0.10 | 0.91 |
| 30 FPS, all | 95 | 158.9 | **0.598** | 1.057 | 3.74 / 6.94 | 74 / 114 | 0.01 | 0.61 |
| 15 FPS, all | 119 | 166.7 | **0.714** | 1.152 | 3.99 / 6.97 | 56 / 87 | 0.12 | 1.91 |
| 60 FPS, chasing only | 49 | 93.0 | 0.527 | 1.047 | 4.52 / 7.55 | 147 / 251 | 0.07 | – |
| 15 FPS, chasing only | 39 | 84.5 | **0.461** | 0.756 | 3.10 / 5.68 | 48 / 94 | 0.03 | – |

### Gate arithmetic

Allowance = larger of (25% above the 60 FPS rate) and (one additional qualified reversal per
enemy-second) = max(0.095, 1.000) = **1.000 /enemy-s**.

- All-population: 0.714 − 0.379 = **+0.335 /enemy-s** → within the 1.000 allowance → **PASS**.
- Chasing-only: 0.461 < 0.527 → 15 FPS is *lower* than 60 FPS → **PASS**.

**Decision Gate A → 15 FPS passes → no production motor change.** H1/H2/H3 alternatives were never
built, because building them would have been a fix in search of a defect.

### Honest observations, not gates

- `minGap` of 0.01–0.12 m with 24 enemies converging on one stationary player is far tighter than
  the 0.4 m *pairwise* contract asserted in unit tests. A 24-strong ring collapsing onto one point is
  the severe-overlap case; enemies do interpenetrate visibly there. This is pre-existing production
  behaviour, unchanged by M5.1.3, and is a candidate for M6 tuning — not a regression.
- `maxStoppedDisp` 1.91 m at 15 FPS is cumulative drift over 8 s of an attack ring being jostled,
  including enemies that left and re-entered the stopped state (68 transitions), not continuous
  sliding at speed.

---

## CP4 — VAT phase, with the denominator corrected

Prior report said "34 distinct phases across 45 enemies" and treated 45 as the denominator. That was
wrong: it counted every active enemy, including those playing **non-looping** gameplay clips, which
correctly all start at frame zero and therefore cluster.

Correct measurement (24-enemy controlled crowd):

```text
totalActive           = 24
loopingLocomotion     = 11      <-- the only valid denominator
nonLoopingClip        = 13      (all "Bite Attack" - correctly NOT phase-offset)
noVat                 = 0
distinct phases (eps 0.01) = 11 / 11      (100%)
distinct phases (eps 0.05) =  9 / 11
phase range           = 0.97
```

Epsilon is stated explicitly: two phases count as identical within 0.01 (1% of clip length) for the
primary figure. No collisions among looping enemies. **No code change needed** — the earlier number
was a reporting error, not a phase-source defect.

---

## CP5 — Audio counters (production gameplay, real fire path)

| weapon | trigger pulls | requested | accepted | dropped | retriggered | peak same-key | peak total | same-key after stop |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Pistol (sidearm) | 60 | 60 | 60 | **0** | 0 | 3 (= bound) | 16 | 0 |
| AA-12 (shotgun, 8 pellets) | 28 | 28 | 28 | **0** | 0 | 4 (= bound) | 16 | 0 |
| G36C (automatic, 10 s) | 46 | 0 | 0 | **0** | 45 | 1 | 16 | 0 |

- `requested == trigger pulls` for AA-12 proves **one gunshot per trigger pull**, not one per pellet.
- G36C `requested == 0` proves automatics are **not** routed into the multi-voice transient path;
  46 pulls = 1 initial acquisition + 45 retriggers on a single voice.
- Peak total 16 is the global cap being fully used by horde cues during a dense wave — the weapon
  path never exceeded its own small budget.
- `maxVoices = 16` and global `perKeyLimit = 3` unchanged. No tail clip layered per bullet.

Counters prove routing and bounds. They do not prove perceived quality — that is the human gate.

---

## CP7 — Outline guard

Registration is idempotent (`unsubscribe` then `subscribe`) with a `SubsystemRegistration` reset for
disabled domain reload. Production `VolumeProfile` hash `11263ac5694eaa134faba8e6561fdd9232ceea9e`
verified identical at session start and after all gameplay, scene transitions and play sessions.
Palette `2a5ade12…`, audio catalog `fe6f57f7…` and runtime library `152b8777…` likewise unchanged.

---

## Defect found in prior verification work: a test file that never compiled

`CrowdSeparationTests.cs` was present on disk, syntactically valid, in the correct folder, with a
valid `.meta` — yet Unity never included it in `_Project.Tests`:

```text
CompilationPipeline assembly "_Project.Tests": sourceFiles = 23, matches for CrowdSeparationTests = 0
AppDomain loaded types: PlanarSteeringTests, VatLocomotionPhaseTests, WeaponAudioTransientTests
                        (CrowdSeparationTests absent)
MonoScript.GetClass() = NULL for this file, non-null for its siblings
```

So the **eight crowd-separation assertions reported as passing in the M5.1.3 report had never
actually run**. The earlier "15/15 passed" for `PlanarSteeringTests + CrowdSeparationTests` was
15 tests from `PlanarSteeringTests` alone.

Deleting the `.meta` and forcing reimport did not fix it. The file was recreated under a new name and
GUID as `CrowdSeparationContractTests.cs` with identical assertions plus the new corrected-metric
regression test.

**RESOLVED (M5 final closeout, 2026-08-13).** The Editor was recovered, the file is confirmed present
in the `_Project.Tests` source list (24 sources) and resolvable from the loaded assembly, and the
suite ran: **9 discovered, 9 executed, 9 passed, 0 failed, 0 skipped** (22.1 s). No
`CrowdSeparationTests` type remains anywhere in the AppDomain. See
`Review/M5_Final/GroundCoverage/GATES.md`.

---

## Blocker: the Unity Editor process terminated

While performing the forced full reimport intended to pick up the recreated test file, the Unity
Editor process exited (only Unity Hub processes remain). Everything below therefore did **not**
happen in this closeout:

- executing `CrowdSeparationContractTests` (9 tests, including the corrected-metric regression);
- the full EditMode / PlayMode suites after that change;
- production runs A and B.

Repository state was verified safe after the crash: nothing staged, and all protected asset hashes
byte-identical to the session baseline.

**Resume action:** reopen the project, let it finish importing, run the full EditMode and PlayMode
suites (expect PlayMode 181 → 190 with the recreated file), then perform production runs A and B.

**Closed 2026-08-13.** Editor recovered; the crowd suite ran and passed 9/9. Full EditMode 502/502.
Full PlayMode is now 212 tests (181 + 9 crowd + 22 new ground-coverage) with one failure —
`PlanarSteeringTests.TwoAgentsWithTheSameDestination_DoNotEndUpStacked`, a pre-existing frame-rate
sensitivity exposed by the larger suite. Production runs A and B still did not complete their full
scripted arcs. Details: `Review/M5_Final/GroundCoverage/GATES.md`.

---

## Human review

`AUDIO: READY FOR USER REVIEW` — checklist in `Docs/MVP_SHIP_PLAN.md`.
`CROWD MOTION: READY FOR USER REVIEW` — captures in this folder: `crowd_60fps_a.png` (spawn ring),
`crowd_60fps_b.png` (settled crowd at 60 FPS), `crowd_15fps_a.png` (same crowd at 15 FPS). Still
images cannot show jitter; the numeric table above is the substantive evidence, and a live look at
the three frame rates is the recommended check.

Neither approval may be self-declared. M5.1 remains NOT LOCKED.
