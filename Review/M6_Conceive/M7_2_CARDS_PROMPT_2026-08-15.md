# PHASE: EXECUTE — M7.2 · B2–B5: the 23 cards, the offer system, and a level-up that applies

Work in:

```text
D:\Projects\Zombie-War
```

The nine shared primitives (B1) are built and live. This run turns them into the actual game system:
twenty-three cards, the 1-of-3 offer, and a level-up that changes the run instead of just showing a
screen. This is the single biggest change to how the game plays since the project began.

**Scope is B2–B5 plus one backfill. Do not start Part C (stations, signal language, threat model) in
this run** — it has been pushed twice already and deserves its own pass rather than a rushed half.
If you finish B with capacity left, spend it on hardening: more tests, a longer play-test, and a clean
owner task list. Do not open C.

Run continuously; do not stop to ask. Decide, record the decision and the reasoning, keep going. If you
run out of capacity, finish the card or system you are inside, leave the project building and playable,
and say exactly where you stopped. The last two runs did this correctly.

Follow `CLAUDE.md`, `AGENTS.md`, `Docs/FRAMEWORK.md` and the `billgamecore` skill. BillTween only, never
DOTween.

---

## 0. Standing rules

```text
Run impact analysis before editing any symbol; report blast radius. Warn on HIGH/CRITICAL in the report
  but do not pause — make the change additive and reversible instead.
NEVER edit Menu.unity or any Assets/_Project/UI/Prefabs/Screens/UI_*.prefab. The owner owns UI layout.
  Implement everything code-side; record any needed prefab change as an owner task.
You never place a grip, muzzle or hand transform by inference. That is permanently the owner's work.
Play-test from Bootstrap.unity only, using the 25 already-authored weapons.
Do not stage, commit or push. Note: .git/index.lock is stale and pre-existing; do not delete it, and
  do not work around it by mutating .git.
Never generate or play a spoken/TTS report.
```

## 1. Verified starting state — independently audited

```text
54 WeaponData assets, 54 unique ids. 25 Ready · 26 PendingOwnerAuthoring · 2 BlockedNeedsDecimation
  · 1 BlockedNeedsProjectileFireMode. Catalog 54, Hub 24 playable.
Families: AssaultRifle 15 · Sidearm 13 · Shotgun 12 · SMG 8 · Marksman 3 · LMG 2 · Launcher 1
A5 closed: editor tooling enumerates through WeaponCatalogAccess; a weapon without a catalog entry
  breaks the build.
A6 closed: ShotGun_D correctly reverted to WD_Shotgun_LegacyD with real evidence in the inventory.
B1 delivered: 9 primitives in Assets/_Project/Scripts/Runtime/Gameplay/Skills/SkillPrimitives.cs
  (513 lines) — StatusCarrier, AutonomousPower, TargetQuery, DistanceAccumulator, RampAccumulator,
  DamageInterceptor, distance curve, SoftCap, PowerFxKit.
Guardrails live IN the primitives: the ≤2 procs/s ceiling is global across all powers, ≤6 chain arcs,
  ≤64 clustered enemies, one OverlapSphereNonAlloc per proc, pooled FX with a concurrency ceiling.
Test suite: 555 passing.
```

## 2. Backfill first — P3 has no tests

`SkillPrimitiveTests.cs` holds 19 tests covering P1, P2, P4, P5, P6, P7, P8 and P9. **P3
(`TargetQuery` — chain-to-nearest, densest cluster, cone, priority override) has zero tests anywhere.**
A search of the whole test folder for `TargetQuery`, `DensestCluster`, `ChainTo` and `P3_` returns
nothing.

P3 is the riskiest primitive in the set: it is the only one written from scratch (chain and density
targeting did not exist in the runtime), it carries the tightest performance constraints, and cards
`Chain Lightning`, `Static Build-up`, `Ordnance Core` and `Soul Burst` all depend on it.

Write its tests before building any card on top of it. At minimum: chain-to-nearest hop ordering and
termination, densest-cluster selection with a known enemy layout, cone and line selection boundaries,
priority-target override, the ≤64 enemy ceiling, the single-`OverlapSphereNonAlloc`-per-proc guarantee,
and correct behaviour with zero candidates.

Also report the test count before and after every run. A previous run reported a green suite while a
new test file was failing to compile — the count was the only signal that caught it. **A green suite
does not prove your new tests ran.**

---

# B2 — Implement all 23 cards

All 23 are in scope (owner decision W2). Build **MUST 15 → SHOULD 7 → LATER 1**, in that order, and do
not stop early.

Full schema: `Review/M6_WeaponFactory/SKILL_CATALOG.md` and `skill_catalog.csv` (23 rows × 64 fields).
Do not add, remove or rename a card. The names are owner-locked.

```text
STAT (5)        Damage Up · Fire Rate Up · Move Speed Up · Max Health Up · Coin Gain Up
SIDEARM (2)     Run & Gun · Quickstep Round
SMG (2)         Static Build-up · Bullet Hose
ASSAULT (2)     Focus Fire · Breach Round
SHOTGUN (2)     Point Blank · Concussion
LMG (2)         Heavy Pressure · Shockwave Belt
MARKSMAN (2)    Longshot · Hunter's Mark
AUTONOMOUS (4)  Chain Lightning · Ordnance Core · Soul Burst · Emergency Detonation
UNIVERSAL (2)   Execution Round · Kinetic Shield
```

Rules that decide whether this system is good or a mess:

- **Every card is a composition of primitives.** If you are writing bespoke logic inside a card, the
  primitive is wrong — fix the primitive. That is what B1 was for.
- **Signature cards resolve by weapon family, never by weapon id.** There are 54 weapons today and more
  coming; a card that names a gun is a bug.
- **Rank 1 unlocks the behaviour. Ranks 2–3 strengthen the same fantasy** — they never turn into a
  different skill.
- No card may depend on reload, magazines, an extra input button, or a primitive that does not exist.
- Trigger vocabulary is fixed: Passive, OnShot, OnHit, OnKill, OnDistanceTravelled, OnContinuousFire,
  OnTargetChanged, OnHealthThreshold, OnTimer, OnDamageTaken.

`Ordnance Core` and `Emergency Detonation` have art: `Frag_Grenade_A`, `Impact Grenade_A`,
`ClayMore_A`, `Land Mine_A` in the Series V4 pack, plus the existing pooled `Bomb.Explode()`. The manual
grenade button stays retired — this content returns as automatic powers only.

# B3 — The offer system

```text
Slot A prefers a signature card compatible with the equipped weapon's family
Slot B autonomous or universal
Slot C any valid card
At most ONE pure-stat card per offer
Never offer an incompatible card, a max-rank card, or one whose prerequisites are unmet
Deterministic from the run seed — same seed, same offers, every time
The pause is ≤30 s unscaled, then auto-picks a VALID card — never a broken or ineligible one
Early run favours mechanic unlocks; later run favours coherent rank-ups
Explicit, tested behaviour when the pool is exhausted
```

The offer builder is the part most likely to look fine and be subtly wrong. Test it directly, not only
through play.

# B4 — Wire it into the level-up, code-side only

The level-up screen currently shows and applies nothing. Make the choice real: the card is offered,
picked (or auto-picked on timeout), applied to the run, and visible in whatever run state the HUD reads.

**Do not edit any UI prefab.** Drive what you can from code against the existing overlay. If the card
visuals genuinely need a prefab change, implement the logic anyway, make it work headlessly, and write
the exact prefab change needed into the owner task list with enough detail that the owner can do it in
one pass.

Use Epic Toon FX (`Assets/ThirdParty/Epic Toon FX/Prefabs/{Combat,Environment,Interactive,Misc}`) for
card pick, power procs and charge/cooldown readouts, all through P9's pooled kit.

Hold the budget: fire rate soft cap 2.2× / hard cap 2.5×; ≤6 lightning arcs per proc; ≤2 procs/s across
all sources; ≤2 concurrent explosions; ≤1 `OverlapSphere` per power proc; clustering ≤1/s over ≤64
enemies; zero runtime material instances; 0 bytes/frame steady state.

# B5 — Prove it

Tests: every card's trigger and effect at rank 1 and at max rank; offer determinism from a seed; no
all-stat offer; no incompatible or max-rank card; timeout auto-pick always valid; exhausted pool;
soft caps enforced; no allocation in the steady state.

Then play from `Bootstrap.unity` and answer the question this milestone exists to answer: **do two runs
with the same weapon produce visibly different builds?** Describe what actually happened in both runs —
which cards were offered, which were taken, and how the two runs differed in play. If they felt the
same, say so; that is a finding about the family grammar, not a failure to report.

---

## 3. Decision authority

**Decide yourself, record the choice and why:**

```text
Card implementation order inside each MUST/SHOULD/LATER band
How each card composes primitives; any primitive API change needed to keep cards thin
Offer weighting numbers, early/late bias curve, rank-up bias (all TUNING)
FX selection and timing from Epic Toon FX
Magnitudes and per-rank scaling as starting hypotheses (all TUNING)
```

**Never decide — record as owner tasks:**

```text
Anything requiring a UI prefab or scene edit
Adding, removing or renaming a card
Changing an owner-locked rule (one weapon per run, auto-fire, no reload, no manual grenade,
  1-of-3 with a ≤30 s pause, family-based skills)
Reactivating gacha, star upgrades or any dormant economy system
```

## 4. Acceptance gates

1. P3 has real tests covering chain, cluster, cone/line, priority override, the ≤64 ceiling, the
   single-query guarantee and the empty case.
2. Test count reported before and after; new tests demonstrably ran.
3. All 23 cards implemented; none added, removed or renamed. Any card that is stubbed is called
   **stubbed** in the report, not listed as delivered.
4. Every card composes primitives; no bespoke targeting, ramping, status or FX logic inside a card.
5. Signature cards resolve by family, never by weapon id.
6. Ranks 1–3 strengthen one fantasy; rank 1 unlocks the behaviour.
7. Offer rules hold: max one stat card, no incompatible/max-rank/unmet-prerequisite card,
   deterministic from seed, valid timeout auto-pick, exhausted pool handled.
8. The level-up choice actually applies in game, wired without editing any UI prefab.
9. Performance guardrails hold; 0 alloc/frame steady state; the global ≤2 procs/s ceiling still holds
   with many cards active at once.
10. Existing tests stay green; any modified test states which contract was preserved and which
    expectation changed.
11. Bootstrap play-test done and described honestly, including whether two runs actually differed.
12. `git status` shows zero modified UI prefabs and zero modified scenes; nothing staged.
13. `detect_changes()` run and reported.
14. Part C not started.

## 5. Final report format

```text
PHASE: EXECUTE — M7.2 B2–B5 — COMPLETE / PARTIAL (say exactly where you stopped)

P3 backfill: tests added, what they cover, anything they exposed
Cards: 23/23 status, one line each — delivered or stubbed, and which primitives it composes
Offer system: rules implemented, determinism, timeout, exhausted pool
Level-up wiring: what applies in game now, what still needs an owner prefab change
Performance: allocation, proc ceiling, FX concurrency under load
Decisions made under delegated authority, and why
Owner tasks recorded
Impact analysis: symbols, blast radius, HIGH/CRITICAL and how each was made safe
Tests: count before → after, new tests added, any modified test and exactly why
Play-test: the two-run comparison, described concretely
detect_changes() output
Protected state: UI prefabs, scenes, staged files — git evidence
Files modified — full list
Open risks
M7.2 STATUS: DELIVERED / PARTIAL
M7.3 STATUS: NOT STARTED
BLOCKERS: none / exact blocker
```

Report what genuinely works. A card that compiles but never triggers is stubbed. A system that passes
tests but feels identical across two runs is a finding worth stating, not hiding.

Completion audio policy: do not generate or play a spoken/TTS report. The owner wants only the
project's short completion notification behavior.

## Completion notification

After the task is genuinely complete and verified:

1. Use the `simplifier-vi` skill at Level 1 to reduce the final report to the outcome, verification performed, and any important remaining issue.
2. Rewrite that summary in natural English using no more than 80 words.
3. Run:

   python Tools\notify_done.py "<English summary>" --repeat 2

If the task is blocked or fails, run the same command with a short English explanation of the blocker. Report any notification-script failure in the written response.
