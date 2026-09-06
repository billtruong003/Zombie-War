# M5 — In-Play Systems Audit

**Scope:** every system the player touches between pressing PLAY and returning to the hub, traced
against `GAME_DESIGN.md` (the GDD) and measured in live runs. **Audit only — no gameplay code or data
was changed.** The only runtime manipulation was disclosed observation instrumentation (§ Method).
**Date:** 2026-08-12.
**Status of the director's symptom list:** the original "seven combat symptoms" list existed only in a
chat session that was lost; it is not in the repository. The symptoms below are re-derived from live
observation plus the standing design-debt register (`CURRENT_GAME_DESIGN_AUDIT.md` D1–D8). Where the
original list resurfaces, map it onto these IDs.

## Method

Two production-path runs (Bootstrap → Menu/Hub → PLAY → Map_Level1), profile baseline
13,240 Coin / 0 Gold / 0 Gem:

- **Run 1 — untouched.** Stationary player. Died in 11 seconds. Used to measure the defeat path.
- **Run 2 — instrumented for observation.** Auto-heal on damage (so later waves are reachable),
  `timeScale = 5` (time compression only), and an auto-unsticker that force-killed enemies parked
  inside the point-blank dead zone after a 12-second kill stall — 4 interventions, 18 enemies, all
  logged. Per-wave counts, coin and XP are unaffected by any of these; durations are reported in
  game-time.

Evidence images: `Temp/CodexReview/M5_Audit/01_spawn.png` … `06_victory_screen.png`.

## Headline verdict

The production run is a **finite five-wave arena survival** (`WD_Level1`, 244 enemies) with a working
kill → coin-drop → bank loop and working idempotent closure, but:

- one **game-breaking combat defect** (S1) that deadlocks every wave tail,
- a **payout rule that contradicts the GDD** on defeat (S4),
- **result screens that fabricate or omit the reward breakdown** (S5),
- an **XP/perk loop that is pure scaffolding** (S6),
- and **none of the GDD's expedition structure** — objectives, extraction, boss decision, threat
  bands, reasons to move — exists in the runtime (S7).

## Findings

### S1 — CRITICAL: point-blank dead zone; the weapon cannot hit the enemy touching the player

**Symptom (measured live):** wave 1 kills froze at 26 for 67+ game-seconds while four Dog Pups chewed
on the player from 0.8–1.3 units away. The weapon kept firing — tracers visible — and hit nothing.

**Root cause (captured in the live session):** `Weapon.FireOnce` raycasts from
`MuzzlePoint.position` along the muzzle axis (`Weapon.cs:413`–419). The muzzle sits ≈0.9 units in
front of the player's center. Auto-aim locks the **nearest** enemy
(`PlayerMovement.cs:104`–141; the 7-unit danger-radius override also picks the nearest). When the
nearest enemy is closer than the muzzle offset, the ray **originates inside or beyond its collider**,
and Unity raycasts do not hit a collider they start inside. Measured: player at (-2.5, 0, 1.5),
target at (-1.7, 0, 1.4), aim distance 0.78, muzzle at (-1.65, 0.94, 1.39) — past the target —
`Physics.RaycastAll` along the muzzle: **0 hits**. The lock never breaks (stickiness keeps the
nearest target, which stays nearest), so the state is a permanent deadlock, not a transient.

**Blast radius:** every slow melee enemy that closes to contact becomes unkillable; a cornered or
swarmed player literally cannot shoot the enemy attacking them. 18 enemies across waves 2–5 required
force-kills in the measurement run.

**Consequence S2:** `WaveDirector.RunLoop` waits `while (ZombieManager.AliveCount > 0)`
(`WaveDirector.cs:117`), so each wave tail stalls the run indefinitely.

**Fix direction (M5.1):** originate the hit test at the player/chest position (muzzle stays the VFX
origin), or clamp effective fire distance to `max(muzzleOffset, aimDistance)` with a contact-range
auto-hit. Any fix must keep the tracer visuals anchored to the muzzle.

### S3 — HIGH: opening burst spawns on top of the player

`01_spawn.png`: HP was 70/100 within seconds of control, and the untouched run died at
**11 seconds**. Corrected root cause after reading `ZombieSpawner`: spawn *placement* is correct —
candidates come from an off-camera 12–22 m ring around the player. The killer is opening *pressure
tuning*: `initialBurst: 12` plus `visibleFloor: 12` (`WD_Level1` "Scouting") converge twelve enemies
onto a brand-new player within ~7 seconds of control. The GDD's insertion band promises low threat
and "regain control immediately" (`GAME_DESIGN.md` §12, §5 first-ten-minutes table).
**M5.1 action taken:** wave 1 `initialBurst` 12→4, `visibleFloor` 12→8 (provisional tuning, needs
playtest).

### S4 — HIGH: defeat banks 100% of everything; GDD says 25% of Coin only

`RunClosure.Close` calls `run.Payout()` before checking the outcome (`RunClosure.cs:53`), and
`Payout()` banks Coin, Gold and Gem in full (`RunState.cs:171`–180). Measured live: run 1 died with
1 run-Coin; the profile went 13,240 → 13,241 (**100% banked on Defeat**). The GDD closure rule
(§11): defeat banks **25% of common Coin**, loses unbanked rare reward and completion bonus. Abandon
(`RunState.Abandon`, called from `GameFlow.ReturnToMenu`, `GameFlow.cs:120`) banks **0%** — the GDD
leaves the exact retention open but "no completion bonus" is its only firm rule; 0% is a choice that
was never made deliberately. Risk-of-the-bank — the GDD's climax mechanism — currently does not
exist: dying costs nothing.

### S5 — HIGH: result screens are not truthful

- **Defeat** shows a full reward breakdown — "BEST Wave 12", "Collected in run +1,250", "Wave bonus
  +350", "First-clear bonus +580", "Total 2,180", "Gems collected +5", "PASS XP +40" — that is
  **hardcoded placeholder text** baked by the installer (`HudInstaller.cs:443`–444, 463). The real
  run behind that screen: wave 1, 2 kills, 1 Coin, 0 Gems. Evidence: `02_defeat_screen.png`.
- **Victory** shows "SURVIVED! / Area secured" with **no breakdown at all** (`06_victory_screen.png`)
  while the profile silently banked +753 (353 run Coin + 400 first-clear).
- The truth exists and is broadcast: `RunDirector` fires `RunFinishedEvent` with the frozen
  `RunSummary` (`RunDirector.cs:58`). Its only subscribers are `GameplayAudioDirector` and
  `MissionTracker` — **no UI consumes it**.

This fails the GDD run-closure promise ("produces a clear reward breakdown", §7) and M5.7's exit
question ("Is the result truthful?") in both directions: fabrication on defeat, omission on victory.

### S6 — HIGH: XP/level/perk loop is scaffolding that runs silently

Measured: the victory run ended at **Level 9 — eight level-ups — with zero player-facing events.**

- `RunState.RecordKill` discards `AddXp`'s levels-gained return (`RunState.cs:108`); nothing queues a
  choice.
- The level-up overlay is a test hook; its pick button is empty by design:
  "Perk backend (roadmap #48) chưa có — chọn chỉ đóng overlay" (`RunOverlays.cs:211`–223).
- Even if a perk were added, **`RunState.Multiplier(RunPerkKind)` has zero callers** — no weapon,
  movement, health or economy system reads perk multipliers.
- The HUD reads only `RunState.Current.Coin` (`HudController.cs:161`) — no XP bar, level, or kills.

The GDD's "first meaningful choice at 90–150 seconds" (§10) is not late — it is absent at every
layer: trigger, UI, effect application, and presentation.

### S7 — HIGH: no reason to move; a stationary player wins the entire production run

Run 2's player never moved and finished 244/244 kills → Victory. The only thing movement would have
fixed is S1's dead zone. Nothing in the runtime implements the GDD's exploration/expedition layer:
no objectives, no POIs, no extraction, no boss decision, no threat/distance bands, no route signals.
The production run is `WaveDirector`'s finite authored wave list (`WaveDirector.cs:105`–128) ending
in Victory on the last clear — wave-count survival, which the GDD explicitly supersedes (§6.3, §23).
This is the known M5.x ladder (all LOCKED in `MVP_SHIP_PLAN.md` §8); the audit's contribution is
evidence that the current combat core plays *worse* than static because standing still is optimal.

### S8 — MEDIUM: run length and pacing

The five-wave run took **982 game-seconds (~16.4 min)** with continuous pistol fire and instant
straggler clears — over the GDD's 8–12 minute session target, and the fresh profile owns only the
pistol (34–65 DPS vs 244 enemies ≈ 12k HP), so the weapon-switch mastery layer is untestable on a
new account. Pacing is authored (`WD_Level1` pressure plans work as designed — floors/bursts/caps
were observed operating); the mismatch is between roster DPS, enemy HP totals and the session target.

### S9 — MEDIUM: weapon data is still generalist (design-debt D2 unchanged)

Spot-check `WD_AssaultRifle_AK47.asset`: `roleTag:` empty, `knockback: 0`, `pelletCount: 1`, no
falloff authoring. The runtime supports pellets, pierce, chains, knockback, falloff — the data does
not use them. Family grammar (§ intended identities) remains unimplemented; DPS ladder remains the
only real difference.

### S10 — MEDIUM: auto-target is nearest-only (design-debt D3 confirmed, and S1 makes it harmful)

`PlayerMovement.UpdateAimTarget` locks the nearest targetable enemy with stickiness/dwell and a
danger-radius override that also picks the nearest (`PlayerMovement.cs:104`–141). There is no
priority weighting for ranged/heavy/boss. Combined with S1, "nearest" actively selects the one enemy
the weapon is geometrically unable to hit.

### S11 — MEDIUM: Gold and Gem income is zero in the production loop

Measured run: 0 Gold, 0 Gem (WD_Level1 authors no elites or bosses; elite Gem roll exists at 50%
in `PickupManager` but nothing elite spawns). With Maps 2–5 retired, their first-clear Gold/Gem is
unreachable. Gold sinks (weapon stars, weapon gacha) and Gem sinks (costume gacha) therefore have
**no live faucet** apart from mission rewards, which were not exercised. The economy above Coin is
currently decorative.

### Working as designed (verified, no defect)

- **Closure idempotency:** one `RunFinishedEvent` per run; payout paid exactly once
  (`RunClosure`/`RunState` contract, backed by `RunClosureTests`).
- **Physical drop loop:** kills spawn ≤4 pooled Coin drops, magnet 3.5 works, wave-clear
  auto-collect prevents lost reward (`PickupManager`); measured 353 Coin banked from 244 kills
  (≈1.45/kill, matching authored `coinReward`).
- **Continuous no-reload fire** matches the locked M4 weapon design, including honest cadence via
  the fire accumulator (`Weapon.cs:252`–279) — when the target is hittable (see S1).
- **Wave pressure plans** (floors, bursts, caps, recovery bands) operate as authored.
- **No-reload HUD:** no reload widget; ammo ring disabled (M4 contract held).

### Minor / dev-only

- CHEAT button overlaps the BEST badge (top-left, all aspects; ZW_CHEATS builds only).
- Hub "NEXT MISSION Kill 50 monsters +200" chip was not exercised by this audit; mission progress
  and claim flow remain unverified live (mission catalog breadth vs proven callers — see
  `CURRENT_GAME_DESIGN_AUDIT.md` §13).

## M5.1 execution — COMPLETE (2026-08-12)

All six items below were implemented and verified live the same day. Verification run
(Bootstrap → PLAY, stationary player, auto-heal + auto-pick instrumentation, 5× time compression):
**244/244 kills → Victory with ZERO unstick interventions** (the audit run needed 18 force-kills),
343 game-seconds vs 982 pre-fix, 8 level-ups each produced a 1-of-3 perk choice, fire rate measured
4.00→5.00 on picking Fire Rate +25%, Max Health stacking visible (HP 164), defeat run banked exactly
25% (5 earned → 1 kept, wallet verified), result screens show real numbers on both outcomes.
Tests: EditMode 502/502, PlayMode 145/145. Evidence: `Temp/CodexReview/M5_1/01–04*.png`.

| # | Status | What shipped |
|---|--------|--------------|
| 1 | ✅ | Hit-test ray originates on the player axis (`Weapon.FireOnce/FireRay/FireRayPiercing`); muzzle stays VFX origin; range extended by the pull-back |
| 2 | ✅ | Wave 1 opening pressure: `initialBurst` 12→4, `visibleFloor` 12→8 (`WD_Level1`, provisional) |
| 3 | ✅ | `RunClosure.DefeatCoinFraction = 0.25`; defeat drops Gold/Gem; banked amounts carried in `Result`; +2 tests |
| 4 | ✅ | `RunOverlays` binds both terminal screens from `RunFinishedEvent`; installer placeholders zeroed; Pass-XP widgets hidden until a live value exists |
| 5 | ✅ | `RunState.LevelsGained` → pause → 1-of-3 draw (`RunPerkPool`) → `AddPerk`; multipliers consumed in `Weapon` (damage, fire rate), `PlayerMovement` (speed), `RunState.ScaleCoin` (coin), `Health.IncreaseMax` (max HP); HUD pill shows `· Lv N` |
| 6 | ✅ | Cheat tab moved off the avatar/BEST badge to the mid-left edge |

## M5.1 FULL — Combat & Locomotion Repair — COMPLETE (2026-08-13)

Second M5.1 wave, executed against the director's full 10-checkpoint brief. Root causes were
reproduced live and measured before fixing. Evidence map: `Temp/CodexReview/M5.1_Full/GATES.md`.

**Root causes confirmed and fixed:**

- **Camera drift (CP5):** `CameraFollow` added the shake offset INTO `transform.position` after
  SmoothDamp, so damping restarted each frame from a contaminated pose — measured 76 m of drift in
  15 s of automatic fire, and 150 m → 654 m while PAUSED (frozen `Time.time` froze the noise into a
  constant vector re-added every frame; the reported "bomb + level-up displaces the camera forever").
  Rewritten as one authoritative `_basePosition` with the bounded shake composed on top; trauma
  decays in unscaled time. Post-fix: 30 s of 10/s fire → max deviation 1.42 m; bomb + 10 stacked
  level-ups → deviation 0.000. Guarded by 3 new `CameraFollowBoundsTests` (PlayMode).
- **"Seeing the world edge" (CP6).** Measured perspective ground footprint: portrait 14.5 m,
  landscape ~25 m from the player, versus a minimum active-ring coverage of 64 m (5×5 × 32 m). No
  streaming change made; traversal (+X 105 m, diagonal, 400 m teleport) and portrait/landscape/tall
  captures show zero edge, refreshes firing exactly per chunk crossing.

  > **Superseded in part (M5 final closeout, 2026-08-13).** The attribution above — that camera
  > *drift* explained the exposed ground — is **withdrawn**: the owner later reproduced the same
  > report with a drift-free orthographic camera at size 10. Re-measured with real frustum-to-plane
  > math, the footprint/coverage numbers above hold and the defect **does not reproduce at all**, but
  > the true sensitivity is camera **pitch**, not drift and not ring radius. See
  > `Review/M5_Final/GroundCoverage/GATES.md`.
- **Enemy rotation fights + thrash (CP2):** during Attack, `ZombieBase` snapped rotation instantly
  every frame while the motor also rotated toward separation micro-velocities — two writers per
  frame. Now the motor yields facing while stopped, attack facing turns at the authored 720°/s, and
  Chase↔Attack has enter/exit hysteresis (`EngageExitFactor` 1.3). Measured: max yaw rate 721°/s
  (= cap + rounding), zero violations while orbiting an attacker.
- **Phantom swings / missing swing anim (CP3):** the baked looping attack clip replayed during
  cooldown, and `VAT_Animator.CrossFade`'s same-clip guard meant the next real swing never
  restarted visually. Swings now hold through their follow-through then hand the VAT back to idle.
- **Weapon audio gaps (CP7):** `AudioService` policy (`perKeyLimit = 3`) silently dropped shots for
  any weapon whose fire rate stacked more than three live one-shots — the "fake/disconnected" SMG
  sound. New `RetriggerCue` restarts ONE dedicated voice at the exact fire cadence for high-rate
  weapons (interval < 0.15 s); slow weapons keep natural per-shot tails. Measured: 10.2 shots/s
  sustained with the retrigger voice alive in 190/190 samples; switch/teardown stops the voice.
- **Input lifecycle (CP9):** `BillVirtualJoystick` now releases on `OnApplicationFocus(false)` /
  `OnApplicationPause(true)` so a swallowed pointer-up cannot leave a stale movement vector.
- **Time ownership (CP9):** audited every `Time.timeScale` writer — `RunOverlays` is the single
  gameplay owner (terminal-wins), exits reset to 1 as part of leaving the scene, cheat panel's
  SetTimeScale is an explicit dev override. Pause → 0, resume countdown → 1 verified live; pause is
  correctly refused while a terminal overlay is up.
- **Roster (CP4):** production = `WD_Level1` → DogPup/CatMeow/Skeleton (walkers) + DogBark
  (pouncer), all with valid prefabs and data. Twelve authored enemies are UNUSED in production
  (ranged/burrower/chargers/bosses — M6 content, unreachable at runtime). Pool reuse verified: three
  consecutive victories and every re-entry started with a clean camera, one WaveDirector and a
  fresh run ledger.
- **Prop scale (CP8):** player-scale captures recorded (barrel, tire, bush, pallet, parts pile,
  grass). All read as recognizable at gameplay scale; no evidence-backed correction made — tire and
  bush flagged for director judgement.

**Tests:** EditMode 502/502, PlayMode 148/148 (+3 camera-bounds tests). **Live playtests:** defeat
path, three victories, second-run cleanliness ×3, pause/resume, bomb+level-up collision, world-edge
traversal at three aspects, teleport, sustained-fire drift probe, enemy facing probe, audio cadence
probe, perf snapshot (editor: avg 17.7 ms with 22–34 enemies + sustained fire; spikes attributable
to editor/harness hitches).

**Deferred to M6:** objectives/POIs, pickups/reward redesign, extraction/boss, economy content,
weapon-family grammar, target priority, new archetype production data.

## M5.1.1 — Verification closeout (2026-08-13)

Separate wave, run AFTER the M5.1 FULL report: proves the remaining contracts by measurement, fixes
only what reproduced. The M5.1 report's own evidence is untouched — everything below is new.

**CP1 — Aim/ballistics: DEFECT REPRODUCED, FIXED, RE-MEASURED.**
Per-shot instrumentation across stationary/strafe/away/circle scenarios showed the muzzle tracks
the logical aim near-perfectly (max 1.5°), but the RAY disagreed with the direction to the locked
target by up to **152.7° while strafing** (avg 26.4°), and up to half of a burst's shots fired at
targets that had died within the frame. Root cause: the shot inherited the smoothed aim/muzzle
sweep and never re-validated the lock at fire time. Fix: an authoritative fire-time snapshot in
`Weapon.FireOnce` — a validated `ITargetable` defines the ray direction (origin→target), an
invalidated one refuses the shot; muzzle stays the VFX origin; the dev `ShotProbe` now reports the
actual ray. Post-fix: target error **0.00–0.03°** in every scenario; remaining "stale" counts are
shots that killed their target in the same frame. Locomotion never enters the direction.

**CP2 — Target switching: DID NOT REPRODUCE; contract now guarded by tests.**
Controlled equal-distance danger-radius stubs (±5 cm alternation, 2 s) produced ≤2 lock switches
in repeated runs — no oscillation on the shipped selection logic, so no hysteresis was added
(per the brief: fix only what reproduces). Four new `TargetStabilityTests` hold the contract:
no equal-distance flicker, dead-target replacement, materially-closer steal, deterministic ties.
One early flaky failure was traced to `DisableDomainReload` static leakage from live sessions and
is now prevented by explicit registry/run-state isolation in the fixtures.

**CP3 — Enemy motion at 60/30/15 FPS: measured, no stop-start, tolerances documented.**
Per-frame probes on a production chaser: zero-speed-while-chasing frames = 1/0/0 at 60/30/15 FPS,
`IsStopped` thrash = 0 at every cap. Turn rate re-measured with dt-normalisation: **max 721°/s vs
the authored 720°/s cap** (sampling rounding, ~0.1% tolerance) — the earlier raw-delta numbers were
editor-frame artifacts. Mean-speed differences between caps were confounded by crowd density and
are not attributed to frame rate. Settings restored after each cap.

**CP4 — Weapon audio: STRUCTURAL PASS, PENDING AUDITORY APPROVAL.**
Clip inventory quantifies the old defect: fire clips are 1.25–1.75 s with 3 variants and NO loop
assets; at 10 shots/s the per-key voice policy (limit 3) silently dropped ~80% of shots — the
reported gaps. `RetriggerCue` (one dedicated voice at true cadence) is the safest behaviour the
existing assets support; H1 (managed sustain loop) is impossible without loop content — recorded as
an **M6 audio-content gap**. In-editor output capture was attempted three ways (AudioRenderer via
editor callback and via player-frame coroutine); the API renders below real time in live play mode
and the editor's master mute intercepted one attempt — no truthful recording could be produced, so
per the brief this gate is **pending human listening**, not claimed from source alone.

**CP5 — Prop scale: H1 PROMOTED.**
Deterministic same-anchor A/B (identical bush instance at (391,397), seed unchanged):
H0 0.80–1.20 reads flat and sinks; H2 (+15%) still reads close to H0; **H1 promoted — bush_a/b
1.00–1.50, bush_c/d 0.94–1.38, tire_a 0.90–1.30** in BOTH the production
`DecorationPalette.asset` and the `DecorationAuthoring` rebuild defaults (a rebuild cannot restore
the old scale). Captures at portrait/landscape/tall + grass-rich in
`Temp/CodexReview/M5.1_Full/props/`. Salts/budgets unaffected (scale is not hashed; vertex counts
unchanged); palette-lock and decoration suites green.

**CP6 — Steady-state allocation: isolated from harness, measured clean.**
First pass was dominated by the verification harness's own per-frame `FindObjectsByType`
(35.6 KB/frame — a lesson recorded here on purpose). Clean 30 s window (no MCP traffic, cached-ref
harness): **23.06 KB/frame, 13.9 MB total, GC 1/1/1 collections, p50 17.1 ms, p95 43.6 ms,
max 1230 ms** (single wave-boundary/editor spike). Remaining allocation is damage-number TMP text,
per-event `WaitForSeconds`, and HUD strings — none in the per-frame paths M5.1 touched (fire,
steering, camera are allocation-free by design). Deferred to M6 polish, not a P0.

**CP7 — Focus/pause: regression-tested.**
Three new `JoystickLifecycleTests` prove focus-loss/app-pause clear the held vector and a fresh
pointer re-claims; camera-paused-no-drift already held by `CameraFollowBoundsTests`; live pause →
0, resume countdown → 1, and pause correctly refused over a terminal overlay.

**CP8 — Regression: EditMode 502/502, PlayMode 155/155** (148 + 4 target-stability + 3 joystick).
Two fresh production runs through Bootstrap→Hub→PLAY: run A exercised fire-while-moving, bomb +
6 stacked level-ups (camera deviation 0.000 after), pause/resume, +70 m chunk traversal; run B
verified clean lifecycle — exactly one WaveDirector/RunDirector/streaming world/camera/audio
listener, zero camera deviation, no stale fire voice, joystick zero, fresh run ledger.

**Incidents found and corrected during M5.1.1:**
- A live session had runtime-mutated the production outline profile
  (`Assets/Settings/SampleSceneProfile.asset`: selection mask 30→22, debugMode on) — VolumeProfile
  assets persist play-mode writes. Restored to the contract values (mask 30, debug off); the
  runtime writer was not identified and is an open risk below.
- `DisableDomainReload` lets live-session statics (an in-progress `RunState`, registry entries)
  leak into subsequent test runs; affected fixtures now isolate explicitly.

**Open risks:** unidentified runtime writer into the outline VolumeProfile (find in M6 — likely a
debug toggle reachable in play mode); one ~1.2 s frame spike near wave boundaries (editor-measured,
uninvestigated); post-victory death can swap the victory screen for the defeat screen (reachable
only via harness damage today); CP4 auditory sign-off outstanding.

## M5.1.2 — Corrective closeout (2026-08-13)

Corrects three technical defects that survived M5.1.1, and hardens against the outline incident.
**The record, corrected explicitly:** M5.1.1's ballistics evidence reported the PRE-SPREAD base
vector as "the actual ray" (0.00–0.03° could never describe a 1.5°–14° spread weapon), and its
fix made every bullet home onto the selected target — able to hit an enemy ~150° away from where
the gun visibly pointed. Both are replaced here; earlier sections stand as written, wrong where
they were wrong.

**Ballistics contract (final):** selected target → smoothed `AimDirection` → visible body/muzzle →
alignment gate → base shot direction = `AimDirection` → authored spread → physics ray. Direct
origin→target snapping removed. The alignment gate holds fire while the visible aim is outside
`max(5°, angle subtended by the target body)` — the distance-scaled term was added after live
measurement showed a fixed 5° cone starved fire in a surrounding pack (1 kill in 6 s of SMG fire;
with the subtended term: 9 kills in the same scenario). A refused or stale-target shot produces no
flash/audio/recoil/tracer/cadence spend. The close-range player-axis ray origin is preserved.

**Probe truth:** new dev-only `Weapon.RayProbe` emits one record per ACTUAL physics ray — the exact
origin/direction handed to `Physics.Raycast`, post-spread, per pellet, with hit identity and
pierce results. Nine `BallisticsRayTests` hold the contract: zero-spread ray == visible aim;
G36C 1.5°/SMG 2°/LMG 3° rays inside authored cones (and provably deviating); shotgun = one record
per pellet inside a 12° cone; piercing reported; strafing never enters the ray; misaligned aim
holds then fires promptly without a banked burst; dead target = zero side effects.

**Terminal result ownership:** `RunOverlays` and `HudController` no longer drive result roots from
raw `AllWavesClearedEvent`/`GameOverEvent`. The single terminal transition renders from
`RunFinishedEvent`'s frozen summary (first-wins ledger). Four `TerminalResultFirstWinsTests` cover
both orders, repeated events (one payout) and replayed finished events. Verified live in both
directions: Victory locked → forced lethal damage → screen and ledger stayed Victory (banked 352);
Defeat locked → forced late wave-clear → screen and ledger stayed Defeat (banked 25% fraction, no
double payout).

**Outline profile:** exhaustive search found no project code writing `selectionLayer`/`debugMode`
at runtime (the only Volume wiring is editor-time `sharedProfile` assignment in the Lab builder);
the original writer remains unidentified and the mutation never reproduced after restore. The
class of failure is now removed by `OutlineProfileRuntimeGuard` (Art/Rendering/BillSSOutline):
every scene Volume is switched onto an instantiated profile clone on scene load, so play-mode and
capture writes can never reach the imported asset; the clone dies with Play Mode. Protected-asset
hashes (profile, palette, build settings) verified byte-identical across the entire M5.1.2
session's play/capture/test cycles.

**Regression:** EditMode 502/502, PlayMode 168/168 (155 + 9 ray + 4 terminal; camera-bounds test's
amplitude bound corrected to the two-axis √2 magnitude). Production run A: pistol → SMG (14/s,
strafing, surrounded) → G36C, bomb + stacked level-ups (camera dev 0.000), pause/resume, victory +
forced late damage. Run B: forced defeat + late wave-clear, HOME, second run — one of each
manager/camera/listener, camera dev 0.010, no stale voice/joystick/terminal roots, timeScale 1.

**Audio: PENDING USER APPROVAL** (unchanged H0 retrigger implementation; checklist in
`MVP_SHIP_PLAN.md`). M5.1 moves to LOCKED only on an explicit `AUDIO PASS`.

## M5.1.3 — Weapon audio + crowd motion (2026-08-13) — ONE GATE OPEN

A human playtest rejected two behaviours that every prior wave's automated evidence had passed.
**The record, corrected:** M5.1/M5.1.1 measured audio *cadence* (shots per second) and concluded the
weapon audio was fine. Cadence counters cannot hear silence — the shots were fired, the sounds were
dropped, and only a person listening caught it. Likewise, single-enemy steering tests proved a lone
chaser moves smoothly and said nothing about twenty of them packed together.

**Audio — root cause and fix.** `AudioService.PlayCue` treats a voice as busy for the whole imported
clip. The handgun clip is 1.25 s; at the production pistol's 4 shots/s the third overlap fills
`perKeyLimit = 3` and every later shot is discarded silently. Measured baseline in a test that
reproduces it: firing the old path at pistol cadence drops shots to `perKeyLimit`; the audible
handgun body is only ~0.3–0.4 s, so the voices were being held long after they stopped saying
anything. New `PlayGuaranteedTransient(key, priority, maxSameKeyVoices)` gives player weapons their
own small budget: inside it voices are acquired normally, and once full the key's OLDEST voice (the
one nearest its end) is recycled rather than the new shot being dropped. The global policy is
untouched, so horde impacts still cannot flood the mix.
Routing is authored data, not a name match: `WeaponData.weaponClass` of Sidearm/Shotgun/Marksman
takes the discrete path (budget 3, or 4 when `pelletCount > 1`), SMG/AR/LMG keep the single managed
retrigger voice, and anything driven past the ~6.7 shots/s merge threshold falls back to retrigger.
Live in production gameplay: pistol **52 visible shots → 52 accepted, 0 dropped**; AA-12
**70 blasts → 70 accepted**; G36C 8 shots on exactly **1** voice (1 initial + 7 retriggers).
Tail assets were NOT layered (H0 kept): the authored tails are 4–6 s and stacking one per shot would
smear the mix; H1/H2 rejected without listening evidence. No new audio content was created.

**Crowd motion — root cause and fix.** `PlanarEnemyMotor` normalised the weighted separation vector,
so the faintest imbalance between neighbours became a full-speed movement command and an
`IsStopped` attacker could still travel at its full authored speed. Separation is now
magnitude-preserving: a smoothstep dead-zone gate silences settled-neighbour noise, a gain saturates
the push once a neighbour is inside roughly half the separation radius, the result is clamped,
exponentially smoothed with a per-sample responsiveness cap, and limited to 20% of move speed while
stopped. Two follow-on defects surfaced during measurement and were fixed: the dead zone originally
*subtracted* from the magnitude (which weakened the useful mid-range push and let two chasers settle
0.25 m apart, breaking the authored 0.4 m minimum), and `ComputeDesiredDirection` returned a flat
zero within 5 cm of the destination — so enemies that arrived together stopped separating and
stacked 2 cm apart. Arrived enemies now keep their separation term.

**VAT locomotion desync.** `Play`/`CrossFade` gained an optional normalized start phase, used only
for looping idle/move; attack, hit, death and every gameplay-timed clip still start at frame zero
(the API enforces this — a non-zero phase on a non-looping clip is ignored). Each pooled enemy
derives one stable phase from a golden-ratio scramble of its instance id, kept across idle↔move and
across hit/attack recovery. Live crowd: **34 distinct locomotion phases** across a 45-enemy wave
versus 10–16 when synchronized.

**Outline guard lifecycle.** Registration is now idempotent (unsubscribe-then-subscribe) plus a
`SubsystemRegistration` reset, so repeated Play sessions under disabled domain reload cannot stack
duplicate callbacks. Protected asset hashes verified byte-identical after the whole session:
profile `11263ac5…`, audio catalog `fe6f57f7…`, runtime library `152b8777…`, enemy prefab `9b8770e4…`.

**Regression:** EditMode 502/502, PlayMode 181/181 (+8 crowd separation, +7 weapon audio,
+6 VAT phase). The pre-existing `TwoAgentsWithTheSameDestination_DoNotEndUpStacked` contract was NOT
weakened — it failed twice during development and both times the production code was fixed.

### M5.1.3 final closeout (2026-08-13) — the 15 FPS failure was a measurement defect

**Superseded, not deleted:** the section immediately below reported ~4× worse crowd oscillation at
15 FPS. That conclusion came from a broken metric and is **withdrawn**. The metric summed reversals
across the whole crowd and divided by wall-clock seconds only (never per enemy, with populations of
25–70 differing between the compared windows), counted single-sample sign flips with no dwell (which
structurally biases against low frame rates, since each 15 FPS sample integrates four times more
velocity change), keyed per-enemy state by instance id across pool reuse (phantom reversals on
recycled enemies), and mixed chasing with stopped enemies.

Re-measured on a controlled crowd — 24 production enemies, deterministic ring, stationary player,
live waves suspended, weapon disabled, 8 s per window, identical at every rate — with the corrected
definition (deadband 0.25 m/s, **time-based** dwell 0.15 s, normalized by observed enemy-seconds):

| population | 60 FPS | 30 FPS | 15 FPS |
|---|---:|---:|---:|
| all active | 0.379 | 0.598 | **0.714** reversals/enemy-second |
| chasing only | 0.527 | – | **0.461** |

Allowance = max(25% of 0.379, 1.0/enemy-s) = 1.0. Measured delta = +0.335/enemy-s → **within
allowance, gate PASSES**. Chasing-only is *lower* at 15 FPS than at 60. Per Decision Gate A,
`PlanarEnemyMotor` and `PlanarSteeringWorld` were therefore **not modified**; no H1/H2/H3
sub-stepping candidates were built.

**VAT denominator corrected.** The earlier "34 distinct phases across 45 enemies" counted every
active enemy, including those in non-looping gameplay clips that correctly start at frame zero.
Against the valid denominator — enemies actually in a looping idle/move clip — the result is
**11 distinct phases out of 11 looping enemies** (epsilon 0.01), range 0.97, with the other 13
enemies correctly playing a non-offset attack clip. No code change needed.

**Audio counters re-measured** (real fire path, production gameplay): pistol 60 pulls → 60 requested,
60 accepted, 0 dropped, peak 3 same-key voices; AA-12 28 pulls → 28 requested (one gunshot per pull,
not one per pellet), 0 dropped, peak 4; G36C 46 pulls in 10 s → 0 on the transient path, 45
retriggers, peak 1 voice. No stale same-key voice after stopping any of them. Global `maxVoices=16`
and `perKeyLimit=3` untouched.

**A defect in my own prior verification:** `CrowdSeparationTests.cs` was never compiled into the test
assembly (absent from `CompilationPipeline` source files and from the loaded `_Project.Tests`
assembly, `MonoScript.GetClass()` null while siblings resolved), so the eight crowd assertions
previously reported as passing **had never run**. Deleting the meta and reimporting did not fix it;
the file was recreated as `CrowdSeparationContractTests.cs` with a fresh GUID, identical assertions,
plus a regression test that locks the corrected reversal metric.

**Not completed:** the Unity Editor process terminated during the forced reimport meant to pick up
that recreated file. The recreated tests have not been executed, the full suites have not been re-run
since, and production runs A/B were not performed. Repository verified safe afterwards: nothing
staged, all protected asset hashes byte-identical. Resume by reopening the project, running both
suites (PlayMode should go 181 → 190) and then runs A and B.

Full detail, formulas and raw numerator/denominator: `Review/M5.1.3/FinalCloseout/GATES.md`.

### Superseded — original 15 FPS crowd oscillation claim (withdrawn, see above)

Paired on the same crowd, same wave: **60 FPS 4.8–5.7 lateral reversals/s vs 15 FPS 19.8–26.5** —
about 4× worse at 15 FPS. The per-sample responsiveness cap improved the ratio from ~18× to ~4.6×,
but a further tightening (cap 0.1) bought almost nothing and broke the 0.4 m spacing contract, so it
was reverted. The residual is structural: at 15 FPS enemies move ~0.2 m between samples and can
leapfrog each other, genuinely changing which side the push comes from. Smallest resume action:
sub-step the motor integration when `deltaTime` exceeds a threshold, or move steering to a fixed
tick, then re-run the paired 60/15 FPS protocol.

**Method limitation, stated plainly:** the H0/H1/H3 attribution runs emulated the old behaviour by
overriding serialized fields at runtime. That emulation could not reproduce the original's
normalize-to-unit step (it clamps but cannot force a floor), so the H0 numbers in the session logs
are not a faithful "before" and are not quoted as such here. The evidence that the separation change
does what it claims is the eight focused unit tests plus the live 60 FPS behaviour, not that A/B.

**Evidence-location incident.** Every earlier wave stored captures under `Temp/CodexReview/…`.
Unity owns `Temp/` and cleared it during this session, deleting all M5.1 / M5.1.1 / M5.1.2 images
and the previous `GATES.md`. Only two crowd frames captured shortly before the wipe survived.
Evidence now lives in `Review/M5.1.3/`, which Unity does not manage; the numbers in this document
come from console measurement logs, not from the lost images. Future waves must not write evidence
into `Temp/`.

**Human review still required:** `AUDIO PASS` and `CROWD MOTION PASS`. M5.1 stays NOT LOCKED.

## M5.1 backlog — original (kept for traceability; each item needed impact analysis before edit)

| # | Fix | Anchors |
|---|-----|---------|
| 1 | Kill the point-blank dead zone: hit-test from player center (muzzle stays VFX origin) or contact-range auto-hit | `Weapon.cs:406`+, `PlayerMovement.cs:104`+ |
| 2 | Minimum spawn distance / threat-free insertion beat at run start | `ZombieSpawner` bands, `WD_Level1` burst |
| 3 | Defeat payout = 25% Coin, rare lost; make Abandon retention an explicit authored value | `RunClosure.cs:53`, `RunState.Payout` |
| 4 | Result screens consume `RunFinishedEvent`: real breakdown on both outcomes; delete placeholder strings | `HudInstaller.cs:443`, `RunOverlays.cs` |
| 5 | Minimum honest XP loop: level-up → pause → 3-perk choice → `AddPerk` → consume `Multiplier()` in damage/fire-rate/move-speed/max-HP/coin paths → HUD XP bar. If M5.1 cuts this instead, remove XP silently accruing | `RunState.cs:108/123/148`, `RunOverlays.cs:211` |
| 6 | Dev polish: CHEAT/BEST overlap | cheat overlay |

Items 1–2 restore the combat core; 3–4 restore truthfulness; 5 is the smallest version of the GDD's
run-build promise. Everything else stays out of M5.1.

## M6 backlog — after the loop is honest

- Expedition structure per GDD/M5.x ladder: first reason to move, POIs, extraction, boss decision,
  threat bands (this is the existing `MVP_SHIP_PLAN.md` §8 ladder, unchanged).
- Weapon family grammar authoring (roles, knockback, falloff, pellets) — S9.
- Target priority beyond nearest (ranged/heavy weighting, prop targeting) — S10.
- Session length tuning against the 8–12 min target (roster DPS vs wave HP budget) — S8.
- Gold/Gem faucets that exist inside the production loop — S11.
- Mission/pass live verification — unproven metrics list in `CURRENT_GAME_DESIGN_AUDIT.md` §13.

## Evidence index

| File | Shows |
|---|---|
| `Temp/CodexReview/M5_Audit/01_spawn.png` | Opening burst surrounding spawn, HP 70 within seconds |
| `Temp/CodexReview/M5_Audit/02_defeat_screen.png` | Fabricated defeat breakdown vs real run (wave 1, 1 Coin) |
| `Temp/CodexReview/M5_Audit/03_run2_wave1_combat.png` | Swarm engulfing stationary player; physical coin drop |
| `Temp/CodexReview/M5_Audit/04_wave1_stall.png` | 4 unkillable point-blank stragglers, tracer firing past them |
| `Temp/CodexReview/M5_Audit/05_wave4_density.png` | Mixed-pack density; distant drops waiting for wave-clear collect |
| `Temp/CodexReview/M5_Audit/06_victory_screen.png` | Victory with no reward breakdown while +753 banked silently |

Live measurements (console `[AUDIT]` lines, session 2026-08-12): 4 unstick interventions, 18
force-killed stragglers; defeat payout 1/1 Coin banked; victory payout 353 + 400 first-clear;
Level 9 with zero level-up presentations; run duration 982 game-seconds.

---

## M5 FINAL CLOSEOUT (2026-08-13) — ground coverage, crowd contract, recovered verification

### The ground-coverage defect did not reproduce; the old cause is superseded

The owner reported the gameplay camera exposing the edge/underside/background beyond the generated
ground, reproduced with BOTH the perspective camera and an orthographic camera at size 10 — which
withdrew the earlier "camera drift / projection" explanation.

Re-measured with real frustum-ray-to-plane math through the live production camera, the defect does
not reproduce, and the numbers show why it cannot:

| projection | aspect | worst margin over a full sub-chunk sweep |
|---|---|---:|
| perspective fov 60 | portrait 1080x1920 | 52.22 m |
| perspective fov 60 | tall 1080x2340 | 52.22 m |
| perspective fov 60 | landscape 1920x1080 | 42.67 m |
| orthographic size 10 | portrait 1080x1920 | 51.38 m |
| orthographic size 10 | tall 1080x2340 | 51.38 m |
| orthographic size 10 | landscape 1920x1080 | 46.22 m |

Every value clears a full one-chunk (32 m) guard band. Live state at a chunk corner: 25/25 ground
renderers enabled, 0 holes, 0 duplicate coordinates, renderer bounds matching `ToWorldMin` exactly on
all 25, no parent-transform offset, and a per-row ray probe resolving every viewport row (including
the topmost) onto enabled ground.

**Measured root cause: none of categories 1-7.** The real sensitivity is camera **pitch**, not ring
radius: at 30 deg pitch and below the frustum top edge rises above the horizon and NO finite ring can
cover the view (60 deg -> 56.0 m margin; 40 deg -> 35.9 m; 35 deg -> 4.0 m; 30 deg -> horizon).
Authored pitch is 60 deg. Increasing `renderRadius` would have bought nothing.

**No streaming, pool, ring or camera code was changed.** What was added is the missing proof:
`GroundCoverage.cs` (pure, allocation-free footprint/margin/guard-band calculator) and
`GroundCoverageTests.cs` (22 tests, 22 passed, including a negative control proving the calculation
fails when it should). No test asserts `RenderRadius == 2`.

### The universal 0.4 m crowd-spacing claim is withdrawn

Earlier text in this document treats 0.4 m as "the authored minimum" for production crowds. That is
**not** the production contract and never was: live measurement of 24 enemies converging on one
stationary player recorded `minGap` 0.01-0.12 m. The 0.4 m figure holds only for the two-agent
synthetic case.

The accepted contract is behavioural: no complete sustained stacking; no push-pull hopping; no
synchronised surging; no full-speed attack-ring sliding; chasing enemies keep reaching the player;
pooled enemies inherit no stale steering. Slight visual overlap in a dense horde is owner-accepted.

`CROWD MOTION: USER PASS` and `GENERAL COMBAT FEEL: USER PASS` (owner playtest).

### Recovered verification

`CrowdSeparationContractTests`: 9 discovered, 9 executed, 9 passed, 0 failed, 0 skipped. Confirmed
present in the `_Project.Tests` source list and resolvable from the loaded assembly; no
`CrowdSeparationTests` type remains anywhere in the AppDomain. The previous 181/181 result is not
quoted as covering these.

### Known failing gate

Full PlayMode (212 tests) fails ONE test, twice:
`PlanarSteeringTests.TwoAgentsWithTheSameDestination_DoNotEndUpStacked` at 0.395 m then 0.320 m
against `> 0.400 m`; passes 15/15 in isolation. Pre-existing frame-rate dependence —
`separationMaxBlendPerSample = 0.2` caps the fraction of the gap closed per sample, so under the
heavier suite (fewer, longer frames) separation ramps more slowly in wall-clock terms and the test's
fixed frame budget measures an under-converged gap. Adding 31 tests exposed it; nothing in this task
created it. Assertion NOT weakened and motor NOT retuned — both are deliberate. Needs an owner
decision in M6.

Evidence: `Review/M5_Final/GroundCoverage/GATES.md`.

---

## M5 FINAL CLOSEOUT R2 (2026-08-13) — camera-aware coverage landed

### The R1 record, corrected

R1's "the defect does not reproduce" stands as `NOT REPRODUCED IN THE CURRENT AUTOMATED SESSION`, not
as "does not exist". R1's leaning toward **camera pitch as the proven root cause is withdrawn** — pitch
is a separate architectural risk, never established as the owner's intermittent event.

What R1 did establish, and what R2 acted on: the streaming architecture reacted **only** to player chunk
changes and never asked what the camera could see. That hole was real regardless of reproduction, so R2
closed it instead of continuing to chase the manual movement pattern.

The R1 orthographic screenshot `ortho10_chunk_low_edge.png` is **retired as invalid** — it shows the
`RUN OVER` result screen, not gameplay. Replacement captures are written only after a gameplay
assertion passes.

### What changed

A second coordinate, `CoverageOrigin`, now centres the 5x5 rendered ring and shifts **early** whenever
the camera footprint plus a 32 m guard band would leave current coverage. `PlayerChunk`
(= `CurrentChunk`) keeps its meaning and keeps driving decoration tier, scheduler priority, shared
collider recentring and debug labels. `renderRadius` stays 2; pool stays 25.

Measured live in a real run: **22 player chunk crossings absorbed with 1 ring build**. Pre-R2, each of
those would have swapped a full 5-chunk row. Captured states where the two coordinates genuinely differ
while coverage holds: player (0,3) / origin (0,2), and player (-5,-7) / origin (-4,-6).

### Two bugs the new tests caught in R2's own code

1. `default(Footprint)` evaluated as **valid** (zeros + `SeesHorizon = false`), so the safety check
   demanded ground at chunk (0,0) from anywhere on the map. Fixed with an explicit `Invalid` sentinel.
2. `isActiveAndEnabled` was the wrong camera test — it silently dropped coverage onto the
   player-centred fallback, which has no hysteresis, and the ring oscillated at chunk boundaries again:
   **41 shifts where 1 was expected**. Fixed to a null check.

### The 0.4 m steering test, replaced

`TwoAgentsWithTheSameDestination_DoNotEndUpStacked` (gap > 0.4 m, fixed 90 frames) became
`TwoAgentsWithTheSameDestination_SeparateInsteadOfStacking` (elapsed simulation time + settle
condition, frame-capped so it cannot hang). This is a **contract replacement, not a hidden weakening**:
0.4 m was never production behaviour, and the owner has approved slight overlap.

Writing it measured two facts worth keeping:

- two agents sharing an exact destination **never converge** — they form a limit cycle, sampled at both
  2.7 m/s and 5.00 m/s (the speed cap). Production never creates this: enemies chase the player and set
  `IsStopped` at attack range. So the moving phase asserts no speed contract at all.
- arrived-at-the-same-point separation settles at ~0.10 m, matching the live 24-enemy crowd `minGap` of
  0.01-0.12 m. The old 0.4 m figure came from reading at 90 frames, mid-travel, before arrival.

The test now also asserts the state production actually uses: with `IsStopped` set, the pair settles
**and** keeps > 0.15 m.

**Production motor / VAT changed: NO.**

### Results

```text
EditMode  517 / 517
PlayMode  234 / 234      (was 212 with 1 failing before R2)
pure origin-selection tests      15 / 15
camera-aware coverage tests      22 / 22
```

Still outstanding: production Run A/B full arcs, and the 60/60/10s audio counter windows.

Detail: `Review/M5_Final/GroundCoverageR2/GATES.md`.

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
