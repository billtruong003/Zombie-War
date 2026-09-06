# M5.1.3 — Gate → Evidence map (2026-08-13)

> **Evidence-location incident.** Earlier waves stored captures under `Temp/CodexReview/…`, which the
> brief suggested. Unity owns and periodically clears `Temp/`, and it did so during this session:
> every M5.1 / M5.1.1 / M5.1.2 capture and the previous `GATES.md` were deleted. Only the two crowd
> frames captured minutes before the wipe survived, and they are here. Evidence now lives in
> `Review/`, which Unity does not manage. Numeric results below come from console measurement logs
> recorded during the session, not from the lost images.

## Audio (CP1–CP3)

| Gate | Result | Evidence |
|---|---|---|
| Baseline reproduces the reported silent shots | PASS | `OldCuePath_DropsPistolShots_ReproducingTheReportedGap`: firing the old `PlayCue` path at 4 shots/s registers `perKeyLimit` drops and accepts < 8 of 8 |
| Pistol: every visible shot audible | PASS | Live production run: `visibleShots=52 requested=52 accepted=52 dropPerKey=0 dropRetrigger=0` (weapon path only; the other drop counts in that log belong to horde cues on the ordinary path) |
| Pistol at perked cadence | PASS | `GuaranteedTransient_AcceptsEveryShotAtPerkedCadence` at ~7.7 shots/s: 20 requested, 20 accepted |
| Shotgun / AA-12 blasts | PASS | Live: `requested=70 accepted=70`; budget 4 selected from `pelletCount > 1` |
| Automatic weapon = one voice | PASS | Live G36C: `visibleShots=8 maxSameKeyVoices=1 retriggered=7` (1 initial + 7 retriggers) |
| Same-key bound never exceeded | PASS | `GuaranteedTransient_NeverExceedsItsSameKeyBudget` (25 rapid shots, bound 3, global ≤ 16) |
| Variant rotation, no immediate repeat | PASS | `AllVariantsRemainReachable_WithoutImmediateRepeats` — reads the clip off the voice the call started; all 3 variants reachable |
| Switch / pause / teardown | PASS | `AutomaticRetrigger_HoldsExactlyOneVoice` + existing `StopFireAudio` lifecycle |
| Tail assets | H0 kept, H1/H2 rejected | Authored tails are 4–6 s; layering one per shot would smear the mix. No listening evidence to justify promotion, so not promoted. No new audio content created |

## Crowd motion (CP4–CP7)

| Gate | Result | Evidence |
|---|---|---|
| Settled crowd stops correcting itself | PASS | `SettledNeighbours_ProduceNoSeparationCorrection` |
| Tiny imbalance ≠ full-speed movement | PASS | `TinyImbalance_DoesNotBecomeFullSpeedMovement` |
| Stopped attacker capped | PASS | `StoppedAttacker_MovementStaysUnderTheCap` (≤25% of move speed); live max stopped displacement 0.76–1.26 m including knockback |
| Real overlap still separates | PASS | `RealOverlap_StillSeparates`; the authored 0.4 m minimum contract (`TwoAgentsWithTheSameDestination_DoNotEndUpStacked`) held — it failed twice mid-development and each time the production code was fixed, never the assertion |
| Chase not materially slower | PASS | `ChasingEnemy_StillReachesItsTarget` |
| Smoothed push does not alternate at full scale | PASS | `SmoothedSeparation_DoesNotAlternateAtFullScale` |
| Pooling reset clears smoothing | PASS | `ResetMotion_ClearsSmoothedSeparation` |
| Crowd settles into a stable ring | PASS | `CrowdSettlesIntoAStableRing_WithoutStacking` (16 enemies, min gap > 0.15 m) |
| VAT locomotion desynchronized | PASS | Live: 34 distinct phases across a 45-enemy wave (10–16 when synchronized); `VatLocomotionPhaseTests` ×6 |
| Attack / hit / death start at frame zero | PASS | `OneShotClip_AlwaysStartsAtZero_EvenIfAPhaseIsRequested`, `CrossFadeIntoAttack_StartsAtZero` |
| **15 FPS not worse than 60 FPS** | **FAIL** | Paired, same crowd, same wave: 60 FPS **4.8–5.7** lateral reversals/s vs 15 FPS **19.8–26.5** (~4×). Per-sample blend cap improved the ratio from ~18× to ~4.6×; tightening it further (0.2 → 0.1) gained almost nothing and broke the 0.4 m spacing contract, so it was reverted |

Visual: `H3_seq_01.png` (dense attack ring — varied per-enemy poses, no shared bob),
`H3_seq_02.png` (post-clear, same run).

## Lifecycle (CP8)

| Gate | Result | Evidence |
|---|---|---|
| Idempotent registration | PASS | `unsubscribe → subscribe` plus a `SubsystemRegistration` reset for disabled domain reload |
| Production profile untouched | PASS | `11263ac5694eaa134faba8e6561fdd9232ceea9e` before and after the whole session |
| Audio catalog / library untouched | PASS | `fe6f57f7…` / `152b8777…` unchanged |
| Enemy prefabs untouched | PASS | `ENM_DogPup_VAT` `9b8770e4…` unchanged |

## Regression (CP9)

EditMode **502/502**. PlayMode **181/181** (+8 crowd separation, +7 weapon audio, +6 VAT phase).
Production runs A and B executed through Bootstrap → Hub → PLAY → Map_Level1 with pistol, AA-12 and
G36C, dense crowds up to 70 enemies, weapon switching, a 15 FPS controlled section, and victory.

## Method limitation, stated plainly

The H0/H1/H3 attribution runs emulated the pre-fix behaviour by overriding serialized fields at
runtime. That emulation cannot reproduce the original's *normalize-to-unit* step (the parameters
clamp a magnitude but cannot force a floor), so those "before" numbers are not a faithful baseline
and are not quoted as one. The evidence that the separation rework does what it claims is the eight
focused unit tests plus live 60 FPS behaviour.

## Human review outstanding

`AUDIO PASS` and `CROWD MOTION PASS`. M5.1 remains NOT LOCKED.
