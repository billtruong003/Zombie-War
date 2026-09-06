# PHASE: EXECUTE — M7.2b · Connect the skill system to combat

Work in:

```text
D:\Projects\Zombie-War
```

The card system is built and tested, and it currently changes nothing in play. A player levels up, sees
three cards, picks one — and the fight is identical. This run closes that gap. It is the smallest piece
of work left that turns the last three milestones into something the player can feel.

**Scope is the integration layer, the FX binding, and an honest play-test. Do not open Part C** (signal
language, stations, threat model). Do not add, remove or rename a card.

Run continuously; do not stop to ask. Decide, record the decision and the reasoning, keep going.

Follow `CLAUDE.md`, `AGENTS.md`, `Docs/FRAMEWORK.md` and the `billgamecore` skill. BillTween only, never
DOTween.

---

## 0. The actual problem, measured

`ZombieWar.Skills` is referenced by exactly one file outside its own folder: `RunOverlays.cs`, the
level-up screen. Nothing in combat knows the skill system exists. Four entry points are dead:

```text
SkillRuntime.ModifyHitDamage   — no callers. Every damage-shaping card is inert:
                                 Damage Up · Point Blank · Longshot · Focus Fire · Execution Round
                                 · Heavy Pressure · Hunter's Mark · Exposed from Breach Round
SkillRuntime.OnKill            — no callers. Soul Burst never charges; kill-driven cards are dead
SkillRuntime.TryAbsorbDamage   — no callers. Kinetic Shield blocks nothing
SkillRuntime.PollPowers        — called only from tests. All four autonomous powers never fire:
                                 Chain Lightning · Ordnance Core · Soul Burst · Emergency Detonation
```

So the honest state is not "3 autonomous cards need damage wiring" — it is that **all 23 cards are
inert in play**. Everything below the level-up screen is missing.

The previous report described the two-run play-test in terms of which cards were offered and taken. It
could not describe a difference in the fight, because there was none to describe.

## 1. Standing rules

```text
Run impact analysis before editing any symbol; report blast radius. Weapon and the damage path are
  high-traffic — warn on HIGH/CRITICAL in the report, and make every change additive and reversible.
NEVER edit Menu.unity or any Assets/_Project/UI/Prefabs/Screens/UI_*.prefab.
You never place a grip, muzzle or hand transform by inference.
Play-test from Bootstrap.unity only, using the 25 already-authored weapons.
Do not stage, commit or push. Do not touch .git (the stale index.lock is pre-existing).
Never generate or play a spoken/TTS report.
```

## 2. Verified starting state

```text
9 primitives in Runtime/Gameplay/Skills/SkillPrimitives.cs — guardrails live inside them
  (global ≤2 procs/s, ≤6 chain arcs, ≤64 clustered enemies, one OverlapSphereNonAlloc per proc,
   pooled FX with a concurrency ceiling)
23 card definitions + SkillOfferBuilder + rank system, tested across hundreds of seeds
RunOverlays builds the offer, applies the pick through SkillRuntime.Take, auto-picks at 30 s
Test suite: 607 passing (52 added last run: TargetQueryTests 19, SkillCardAndOfferTests 33)
Known design finding, NOT to be tuned away in this run: each family has 2 signature cards × 3 ranks,
  and slot A always prefers a family signature — so a slot-0 player's first six levels are
  predictable. That is the owner's call once the cards can actually be felt.
```

---

# Task 1 — The four call sites

These anchors are real; verify each before editing.

## 1.1 Damage — `Weapon.cs:662`

```csharp
dmg.TakeDamage(WeaponUpgradeMath.EffectiveDamage(data, weaponLevel) * perkMult ...
```

Route the final damage through `SkillRuntime.Active?.ModifyHitDamage(...)` before it lands. The method
needs the target id, the shot distance and the target's health fraction — supply real values, not
placeholders. If a value is genuinely unavailable at that site, get it properly rather than passing a
constant that silently disables a card.

Preserve the existing `perkMult` path: the legacy 7-perk system must keep working when no
`SkillRuntime` is active, exactly as `RunOverlays` already does it.

Apply the OnHit side effects too (statuses from `Breach Round`, `Concussion`, `Hunter's Mark`), which
live in the runtime's status method, not in the cards.

## 1.2 Kills — `ZombieKilledEvent`, fired at `ZombieBase.cs:661`

`PickupManager` and `MissionTracker` already subscribe through `Bill.Events`. Subscribe the skill
runtime the same way — no new plumbing, no per-enemy Update. Unsubscribe on teardown like they do.

## 1.3 Player damage — `Health.cs:24`

`TryAbsorbDamage` gives Kinetic Shield one blocked hit per stored charge. Hook it on the **player's**
health only; an enemy must never absorb with the player's shield. Blocking must be visible — the player
has to understand a hit was eaten, or the card feels like a bug.

## 1.4 Autonomous powers — the missing tick

`PollPowers` needs an owner that ticks once per frame during a run and applies each returned
`PowerProc`. Put it where the run already ticks; do not add an Update to every enemy. One driver,
pooled, allocation-free.

Each proc currently returns intent only — `{ skillId, targets, radius }`. Apply it:

```text
Chain Lightning        TargetQuery chain from the current target, damage each arc, ≤6 arcs
Ordnance Core          TargetQuery densest cluster, then the existing pooled Bomb.Explode() path
Soul Burst             radial around the player, reusing the same explosion primitive
Emergency Detonation   radial around the player at low HP; NO heal, NO invulnerability
Static Build-up        SMG charge discharge shares the chain selection path
```

Damage numbers are `TUNING` — pick sane starting values from the catalog magnitudes and label them.

## 1.5 The pooling trap — do not skip this

Enemies are pooled. `StatusCarrier` keys statuses by target id, and P1 already has a `Clear` with a test
named `ClearPreventsARecycledEnemyInheritingStatuses`. **Call it on despawn.** If you do not, a recycled
enemy inherits the Exposed/slow/mark of whatever died in its slot, and the bug will look like random
damage spikes weeks later.

Same discipline for per-target hit counters used by `Focus Fire`.

# Task 2 — Bind the FX

P9's pooled kit exists with no effects bound. Choose per-card effects from
`Assets/ThirdParty/Epic Toon FX/Prefabs/{Combat,Environment,Interactive,Misc}` and bind them through the
kit — never instantiated ad hoc, never a runtime material instance.

Explosive powers reuse the Series V4 art: `Frag_Grenade_A`, `Impact Grenade_A`, `ClayMore_A`,
`Land Mine_A`, plus the existing pooled `Bomb` visual.

Every card that procs must be legible without reading a number: the player should see the arc, the
blast, the shield break. Hold the ceilings: ≤2 concurrent explosions, pooled tracers and impacts, zero
runtime material instances, 0 bytes/frame steady state.

# Task 3 — Prove it in play, not only in tests

Tests first: each of the four call sites has a test proving the runtime is consulted; a pooled-enemy
recycle test proving statuses do not leak; an integration test that a taken card measurably changes
output damage.

Then play from `Bootstrap.unity` and answer the question this milestone exists for:

```text
Run A and Run B, SAME weapon, different seeds, played to at least level 6.
For each: which cards were taken, and what actually happened in the fight.
Did enemies die differently? Did you survive differently? Did a power visibly fire?
```

Describe what you saw, with numbers where you have them — damage per hit before and after a Damage Up,
kill rate before and after Chain Lightning, a shield actually eating a hit. **If the two runs still feel
the same, say so plainly.** That is a finding about the family grammar, and it is the owner's decision,
not something to tune away in this run.

---

## 3. Decision authority

**Decide yourself, record it:**

```text
Where the power driver lives and how it ticks
Starting damage/radius numbers for the four powers (all TUNING)
Which Epic Toon FX prefab serves which card
How target id, distance and health fraction are obtained at the hit site
```

**Never decide — record as owner tasks:**

```text
Anything requiring a UI prefab or scene edit
Adding, removing, renaming or rebalancing a card's identity
Changing slot A's signature-card guarantee (the known grammar finding)
Changing an owner-locked rule (one weapon per run, auto-fire, no reload, no manual grenade,
  1-of-3 with a ≤30 s pause)
```

## 4. Acceptance gates

1. All four call sites connected; `ZombieWar.Skills` is now referenced by combat code, not only by
   `RunOverlays`.
2. Every one of the 23 cards has an observable effect in play. For each card, state how it is observed.
   A card whose effect cannot be observed is **stubbed** — say so.
3. The four autonomous powers actually fire and actually damage, through the pooled explosion path.
4. `StatusCarrier.Clear` (and any per-target counter) is called on enemy despawn, with a test.
5. The legacy 7-perk fallback still works when no `SkillRuntime` is active.
6. FX bound through P9's pooled kit; no ad-hoc instantiation; no runtime material instances.
7. Guardrails hold under load: global ≤2 procs/s with several powers taken, ≤2 concurrent explosions,
   0 bytes/frame steady state.
8. Test count reported before → after; new tests demonstrably ran.
9. Existing 607 stay green; any modified test states which contract was preserved and what changed.
10. Play-test described concretely, including whether two runs actually felt different.
11. `git status`: zero modified UI prefabs, zero modified scenes, nothing staged.
12. `detect_changes()` run and reported.
13. Part C not started.

## 5. Final report format

```text
PHASE: EXECUTE — M7.2b COMBAT INTEGRATION — COMPLETE / PARTIAL (say exactly where you stopped)

Call sites: each of the four, what was changed, impact analysis and how it was made safe
Cards: 23 rows — for each, HOW its effect is observable in play, or the word "stubbed"
Autonomous powers: what fires, what it damages, through which path
Pooling: where Clear is called, and the test that proves no leak
FX: which prefab serves which card; concurrency and allocation numbers under load
Decisions made under delegated authority, and why
Owner tasks recorded
Tests: count before → after, new tests, any modified test and exactly why
Play-test: Run A vs Run B — cards taken, what happened in the fight, whether they differed
detect_changes() output
Protected state: UI prefabs, scenes, staged files — git evidence
Files modified — full list
Open risks
M7.2 STATUS: DELIVERED / PARTIAL
M7.3 STATUS: NOT STARTED
BLOCKERS: none / exact blocker
```

The bar for "delivered" this time is not that the code compiles and the tests pass. It is that a card,
once taken, changes what happens on screen. Anything short of that is reported as stubbed.

Completion audio policy: do not generate or play a spoken/TTS report. The owner wants only the
project's short completion notification behavior.

## Completion notification

After the task is genuinely complete and verified:

1. Use the `simplifier-vi` skill at Level 1 to reduce the final report to the outcome, verification performed, and any important remaining issue.
2. Rewrite that summary in natural English using no more than 80 words.
3. Run:

   python Tools\notify_done.py "<English summary>" --repeat 2

If the task is blocked or fails, run the same command with a short English explanation of the blocker. Report any notification-script failure in the written response.
