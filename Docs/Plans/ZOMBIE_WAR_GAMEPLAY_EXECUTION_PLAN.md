# Zombie War — Gameplay Execution Plan

> **PAUSED / HISTORICAL — 2026-08-09.** This execution order predates the procedural expedition
> redesign. It must not open implementation work. Use [`../GAME_DESIGN.md`](../GAME_DESIGN.md) and
> [`../WORLD_STREAMING_TECHNICAL_DESIGN.md`](../WORLD_STREAMING_TECHNICAL_DESIGN.md) as current
> authority, then wait for an explicit `[PHASE: EXECUTE]`.

**Authority:** Risk-driven gameplay execution order

**Status:** Canonical

**Updated:** 2026-08-08

**Release gates:** [`../MVP_SHIP_PLAN.md`](../MVP_SHIP_PLAN.md)

**Design vision:** [`../DESIGN_BRIEF.md`](../DESIGN_BRIEF.md)

> This plan answers **what gameplay risk to close next**. `MVP_SHIP_PLAN.md` remains the authority for
> release readiness, store, device and production gates. If an old task list conflicts with the order
> below, this plan wins for gameplay sequencing.

## Current phase status — 2026-08-08

| Phase | Status | Evidence / next gate |
|---|---|---|
| 0 — Design consolidation | **PASS** | Canonical docs, authority links and phase contracts verified |
| 1 — Campaign selector | **PASS** | 30/30 focused tests; 278/280 full EditMode with only two pre-existing weapon-test failures; Bootstrap Play Mode selector flow; live defeat and abandon both left `level.4` incomplete |
| 2 — Weapon identity foundation | **NEXT** | Must define and test representative baseline family identity; no signature technique work |
| 3–8 | **CLOSED** | Open only through the preceding phase's exit decision |

Phase 1 note: the obsolete separate “five-node Campaign screen” requirement in
`Reference/UI/UI_ARCHITECTURE.md` was reconciled to the canonical compact Hub selector. Runtime live
checks recorded `Defeat: MarkedComplete=false` and `Abandon: RunState.Current=null`, with completion
remaining false in both cases.

## Operating rules

- Only one phase may be active. A later phase is not implied by completing code in an earlier one.
- A feature is not complete without the required player-facing and technical evidence.
- A design phase may exit PASS, PARTIAL or FAIL. FAIL is valid and blocks dependent phases.
- Preserve unrelated dirty changes. Do not clean, stage or commit unless explicitly requested.
- Inspect current source/assets before implementation; the working tree is newer than the indexed
  `68fbc090` baseline.
- No new feature from a later phase may be “helpfully” bundled into current work.

---

## PHASE 0 — Design Consolidation & Baseline

### PURPOSE

Give future agents one coherent statement of project reality, design decisions, unproven hypotheses
and execution order.

### STRUCTURAL PROBLEM

Old docs mixed implementation facts, brainstorms and production commitments. The same perk/weapon
direction appeared with different authority and outdated assumptions.

### PROJECT FACTS / PREREQUISITES

- Working tree is extensively dirty and newer than GitNexus/current-state snapshots.
- Existing canonical release and framework docs are valuable and must remain.
- Old gameplay/weapon/skill docs contain useful history but overstate unimplemented directions.

### IN SCOPE

- Vision, combat grammar, skill system, campaign/progression source-of-truth docs.
- This execution plan, doc index, conflict notes and supersession banners.

### OUT OF SCOPE

- Runtime code, tests, data tuning, scene/prefab/UI modification and Phase 1 implementation.

### IMPLEMENTATION RISKS

- Accidentally turning a hypothesis into a locked feature.
- Duplicating authority or erasing useful historical reasoning.
- Treating stale docs/index as newer than dirty source.

### PLAYER-FACING SUCCESS CRITERIA

No immediate build change. Future work should target a coherent player experience instead of adding
unrelated systems.

### TECHNICAL ACCEPTANCE CRITERIA

- Canonical docs exist and cross-link.
- Obsolete docs are preserved but unmistakably lower authority.
- Documentation-only diff; Markdown links resolve to real files.

### EVIDENCE REQUIRED

- `git diff -- Docs` review.
- Link/path audit and explicit list of conflicts/decisions/open questions.

### EXIT DECISION

- **PASS:** no major undocumented contradiction; Phase 1 may be opened by the Director.
- **PARTIAL:** docs are usable but named facts require verification; record owners before Phase 1.
- **FAIL:** authority remains ambiguous; do not implement anything.

---

## PHASE 1 — Campaign Selector Closure

### PURPOSE

Expose the five already-authored campaign stages through a compact, honest Hub selector. This is
foundation, not hook validation.

### STRUCTURAL PROBLEM

Normal Hub flow sends PLAY directly to gameplay and does not let players select Stages 2–5. Current
power gating also contradicts the promised linear completion path.

### PROJECT FACTS / PREREQUISITES

- `CampaignCatalog`, five entries, `GameFlow.SelectLevel`, completion persistence,
  `LastSelectedLevelId` and `CampaignChanged` already exist.
- Hub currently has PLAY but no selector.
- `Evaluate()` enforces previous completion and `minimumPower`; design decision makes power advisory.
- UI ownership/prefab constraints in project docs remain binding.

### IN SCOPE

- Compact stage label, dot line and left/right controls directly above PLAY.
- Data-driven catalog count and four visual states.
- Completion-only hard unlock; recommended-power warning.
- One-step/no-wrap selection, locked-stage refusal, PLAY selected stage.
- Refresh on return and restore last valid selection.
- Focused automated tests plus Play Mode flow validation.

### OUT OF SCOPE

- Hub redesign, 3D menu background, new campaign content, wave/balance changes.
- Weapon identity, combat grammar, skills, economy tuning or Phase 2 work.
- General UI polish outside the selector's minimum readable state.

### IMPLEMENTATION RISKS

- Dirty UI/scene/prefab ownership collision.
- Changing `LevelGate` semantics breaks tests/editor audit tools.
- Stale selection points at removed/locked entry.
- Event subscriptions leak or Hub refresh runs before profile state is available.
- PLAY bypasses selected identity or duplicate click loads multiple scenes.

### PLAYER-FACING SUCCESS CRITERIA

- Fresh player immediately sees Stage 1 as playable and later dots as locked.
- After clearing N and returning, N is completed and N+1 becomes available without restart.
- Player can understand current selection and launch it without guessing.
- Under-recommended power warns but does not trap the player in grind.

### TECHNICAL ACCEPTANCE CRITERIA

- Count/order derive from catalog; no literal five-stage branch.
- Empty/null/malformed catalog fails safely.
- Selection cannot wrap or land on a locked stage.
- Saved valid selection restores; invalid selection falls back deterministically.
- Existing run identity/reward idempotency remains intact.
- Unity compile 0 errors; focused tests pass; no repeated console exceptions.

### EVIDENCE REQUIRED

- Screenshots: fresh profile, completed/available/selected/locked states, power warning.
- Play Mode video: Bootstrap → Hub select → play correct scene → clear → Hub refresh → next stage.
- Test results and console log summary.
- Diff scope showing no Phase 2 or unrelated cleanup.

### EXIT DECISION

- **PASS:** selector behavior and progression integrity satisfy all criteria; close foundation.
- **PARTIAL:** selection works but restore/refresh/warning evidence fails; fix Phase 1 only.
- **FAIL:** catalog/profile contract cannot support honest flow without broader redesign; stop and report.

---

## PHASE 2 — Weapon Identity Foundation

### PURPOSE

Make major weapon families tactically legible before any signature technique is allowed to create
their identity.

### DESIGN HYPOTHESIS

Players can understand why pistol, SMG, AR, shotgun, LMG and sniper exist as different tools through
baseline firing/resource/control behavior alone.

### PROJECT FACTS / PREREQUISITES

- 25 assets exist, but current serialized data is overwhelmingly SingleHitscan/Magazine/generalist.
- Runtime supports pellet cones, pierce, falloff and knockback beyond current authoring.
- Current DPS/tier spread can overshadow role identity.
- Phase 1 must be closed; Phase 3 slice needs representative families.

### IN SCOPE

- Audit and choose one representative asset per relevant family.
- Author/test the smallest baseline differences needed to express role and weakness.
- Preserve guns as primary combat actors; document tuning evidence.
- Family readability through cadence, range, resource and spatial response.

### OUT OF SCOPE

- Full 25-weapon rebalance, new models, rarity/economy pass, signature techniques.
- Shared-state combo system, full input redesign, new enemies or campaign-wide tuning.

### IMPLEMENTATION RISKS

- Raw DPS remains the only rational choice.
- Over-tuning produces novelty but not reusable identity.
- Auto-aim makes line/cone/priority roles impossible to express.
- Changing weapon data affects campaign power calculations and difficulty.

### PLAYER-FACING SUCCESS CRITERIA

- Testers can identify each representative family's job and weakness from play.
- They switch away when that weapon's situation ends, not only when ammo empties.
- No representative solves emergency, sustain and punish equally well.

### TECHNICAL ACCEPTANCE CRITERIA

- Representative data/runtime paths compile and preserve save/loadout compatibility.
- Damage/resource math has focused tests or deterministic captures.
- No missing prefab/grip/audio/Addressable regression.
- Campaign balance impact is recorded, not silently normalized.

### EVIDENCE REQUIRED

- Side-by-side capture under controlled enemy situations.
- TTK/resource/role tuning sheet for representatives.
- Blind player description of perceived roles and misuse weaknesses.

### EXIT DECISION

- **PASS:** at least the Phase 3 families have readable baseline identity; open Phase 3.
- **PARTIAL:** some roles read but one overlaps; revise only ambiguous representatives.
- **FAIL:** auto-combat prevents meaningful family distinction; reconsider targeting/control premise
  before adding skills.

---

## PHASE 3 — Combat Grammar Prototype

### PURPOSE

Test the highest-risk hypothesis: enemy problem → setup weapon → deliberate swap → cash-out weapon is
actually fun with automated aim/fire.

### DESIGN HYPOTHESIS

READ → SET UP → SWAP → CASH OUT → RESET can produce mastery, agency and replay intent without adding
manual shooting.

### PROJECT FACTS / PREREQUISITES

- Phase 2 representative identities must read before state interactions are added.
- Existing enemies already provide runner/pouncer, ranged, heavy/charger and recovery windows.
- Current weapon switching refills the magazine and clears reload, invalidating honest tests.
- One-button cycling may not permit intentional weapon choice.

### IN SCOPE

- Deliberately narrow slice, for example:
  - shotgun creates Stagger/space;
  - AR or SMG creates Exposed/setup;
  - sniper uses line/recovery cash-out;
  - runner/pouncer, ranged and heavy/charger mix.
- Honest per-slot magazine/reload persistence across swaps.
- Temporary test-only weapon selection method if needed to isolate combat value from final mobile UX.
- Minimum VFX/audio/readability required for testers to attribute cause and effect.

### OUT OF SCOPE

- Economy, rarity, meta progression, full perk tree, all weapon candidates, new campaign content.
- Final input architecture, 20+ skills, broad status library or production balance.

### IMPLEMENTATION RISKS

- Setup is busywork before damage.
- Auto-target chooses the wrong enemy/line.
- States are technically active but visually incomprehensible.
- Temporary selection controls accidentally become production design.
- One high-DPS weapon remains dominant.

### PLAYER-FACING SUCCESS CRITERIA

Without coaching, a majority of a small directional test group should:

- intentionally change weapons when the situation changes;
- explain what setup they made and what the next weapon exploited;
- recall at least one mastery/payoff moment;
- not simply return to the highest-DPS weapon;
- express interest in trying another loadout or interaction.

### TECHNICAL ACCEPTANCE CRITERIA

- Unequipped slots preserve meaningful magazine/reload state.
- Shared states have deterministic create/consume/expire behavior and no pooled-object leakage.
- Targeting, death and state cleanup remain valid under horde pressure.
- Compile/tests pass and prototype runs from Bootstrap without economy dependency.

### EVIDENCE REQUIRED

- Uncoached playtest video and exact player comments.
- Instrumented or manually coded counts: intentional swaps, state created, valid cash-outs, fallback
  to dominant weapon, deaths during attempted sequence.
- Post-test questions: “Why did you switch?”, “What did the first gun create?”, “What would you try next?”
- Failure log including random cycling, ammo-only switching, invisible states and setup-as-tax.

### EXIT DECISION

- **PASS:** behavior and comprehension support the hypothesis; open Phase 4.
- **PARTIAL:** players perceive roles but sequence/control/readability fails; identify one cause and
  rerun a smaller variant. Do not expand content.
- **FAIL:** players do not intentionally create/exploit setups or prefer one gun regardless of
  situation. Kill/revise the hook; Phase 4–8 remain closed.

---

## PHASE 4 — Signature Technique Vertical Slice

### PURPOSE

Determine whether techniques create build expression by changing weapon use rather than adding
independent damage.

### DESIGN HYPOTHESIS

A small representative set can deepen a validated grammar across emergency/control, movement,
setup, punish, sustain and execution categories.

### PROJECT FACTS / PREREQUISITES

- Phase 3 must PASS.
- Candidate library and icon ingredients exist; no candidate is locked.
- Current level-up backend is incomplete, so test delivery may be intentionally narrow.

### IN SCOPE

- Select the minimum techniques needed to compare categories, not every family candidate.
- Clear trigger, tradeoff, feedback and behavioral mutation.
- Test harness or bounded acquisition path sufficient for comparison.

### OUT OF SCOPE

- Full run progression, all icons, five-level trees, final rarity/economy and ability nukes.

### IMPLEMENTATION RISKS

- Techniques become mandatory damage multipliers or play the game automatically.
- Visual complexity obscures base grammar.
- Candidate breadth creates balance debt before learning.

### PLAYER-FACING SUCCESS CRITERIA

- Tester changes positioning/timing/weapon choice because of selected technique.
- Two techniques produce describably different behavior, not just different damage.
- Base weapon weakness remains visible.

### TECHNICAL ACCEPTANCE CRITERIA

- Technique state resets per run and cannot duplicate/subscriber-leak.
- Trigger/effect is testable and works with pooling, death and swap lifecycle.
- No dependency on unbuilt economy or final UI.

### EVIDENCE REQUIRED

- A/B play videos and behavior notes.
- Trigger frequency, successful use and ignored-choice observations.
- Readability screenshot/capture per technique.

### EXIT DECISION

- **PASS:** retain proven categories and open Phase 5.
- **PARTIAL:** one category works; cut weak techniques and retest only the learning gap.
- **FAIL:** techniques add spectacle/numbers but no behavior; return to grammar, not more candidates.

---

## PHASE 5 — Weapon Switch / Input UX

### PURPOSE

Map validated tactical choice onto portrait mobile input without adding unnecessary controls.

### DESIGN HYPOTHESIS

The player can reliably choose the intended weapon quickly enough for setup/payoff windows using a
minimal scheme.

### PROJECT FACTS / PREREQUISITES

- Current input is ordered cycling across three slots.
- Pistol is mandatory; two long guns form the rest of the loadout.
- Phase 3 may have used a temporary direct-selection test control.

### IN SCOPE

- Compare ordered cycle, pistol emergency layer + two long guns, hold/flick/radial or another minimal
  scheme against the same validated encounters.
- Thumb reach, error rate, selection latency and state readability.
- Integrate the winning minimal scheme with existing HUD conventions.

### OUT OF SCOPE

- General HUD redesign, manual fire/reload, extra active ability bar or unrelated UI polish.

### IMPLEMENTATION RISKS

- Control complexity breaks one-hand promise.
- Cycling hides intent or misses short windows.
- Radial/hold pauses flow or conflicts with movement thumb.

### PLAYER-FACING SUCCESS CRITERIA

- Player selects intended weapon with low error and without looking away from combat.
- Setup windows remain achievable; controls feel learnable in seconds.
- Pistol relationship is understood without a tutorial paragraph.

### TECHNICAL ACCEPTANCE CRITERIA

- Input is safe across pause/result/reload, different occupied-slot counts and device aspect ratios.
- No double invocation or illegal locked selection.
- Existing FTUE path can teach it with minimal update.

### EVIDENCE REQUIRED

- On-device capture and selection latency/error comparison.
- Thumb-reach checks on min/target devices.
- Uncoached first-use observation.

### EXIT DECISION

- **PASS:** lock the simplest scheme meeting behavior thresholds; open Phase 6.
- **PARTIAL:** combat choice is valid but mapping fails; iterate control only.
- **FAIL:** no low-input mapping expresses the choice; revisit loadout/grammar scope.

---

## PHASE 6 — Run Build System

### PURPOSE

Turn validated weapon/technique choices into run-to-run tactical variation and a readable power arc.

### DESIGN HYPOTHESIS

Technique selection/mutation plus supportive stats creates distinct runs without diluting gunplay.

### PROJECT FACTS / PREREQUISITES

- XP math and stat-perk data exist; choice/application loop is incomplete.
- Phase 4 has identified which technique categories deserve delivery.
- Phase 5 has locked how the player accesses arsenal choices during combat.

### IN SCOPE

- XP level-up queue, valid choice generation, technique selection, mutation branches, stat support,
  pause/resume safety, run reset and necessary HUD clarity.
- Prevent duplicate rewards/choices and invalid out-of-loadout options.

### OUT OF SCOPE

- Full candidate library, huge rarity trees, meta economy rebalance, physical XP unless explicitly
  chosen and scoped, new content breadth.

### IMPLEMENTATION RISKS

- Choice overlays interrupt combat too often.
- Offers are fake choices or incompatible with loadout.
- Direct XP and physical drops double-award.
- Multipliers exist in data but do not affect runtime.

### PLAYER-FACING SUCCESS CRITERIA

- Player can describe their build and how it changed decisions.
- Two runs with different choices produce different tactical behavior.
- Power growth is visible without erasing weapon weaknesses.

### TECHNICAL ACCEPTANCE CRITERIA

- Level gains queue exactly once; choices apply exactly once and reset next run.
- All chosen effects have quantified runtime tests.
- Pause/death/win during selection cannot deadlock timescale/input.

### EVIDENCE REQUIRED

- Full-run captures of at least two builds.
- Choice timing/distribution and invalid-offer logs.
- Automated multiplier/reset tests and manual terminal-state matrix.

### EXIT DECISION

- **PASS:** builds alter behavior and loop is safe; open Phase 7.
- **PARTIAL:** backend works but choices are not meaningful; retune/cut before content.
- **FAIL:** run system becomes stat inflation/interruptions; simplify or remove layers.

---

## PHASE 7 — Content Expansion

### PURPOSE

Scale a proven system through combinations of weapons, enemies, modifiers, bosses and stages.

### DESIGN HYPOTHESIS

A limited grammar can create durable variety without bespoke pair explosion or pure stat inflation.

### PROJECT FACTS / PREREQUISITES

- Five stages, 25 weapon assets and a broad enemy roster already exist.
- Only content matching proven interactions should be promoted into production balance.

### IN SCOPE

- Differentiate more weapon models, select more proven techniques, elite modifiers, boss windows,
  stage composition, mastery unlocks and enemy remix.

### OUT OF SCOPE

- New systems that do not strengthen the validated loop; endless mode by default; lore expansion;
  monetization optimization.

### IMPLEMENTATION RISKS

- Content count replaces decision quality.
- Status/exception explosion destroys readability.
- Stage difficulty becomes HP/DPS inflation.

### PLAYER-FACING SUCCESS CRITERIA

- New content asks a new combination/question using known grammar.
- Players want to test alternative loadouts, not only chase stronger rarity.
- Later stages remix mastery without requiring new tutorials for every enemy.

### TECHNICAL ACCEPTANCE CRITERIA

- Performance, pooling, Addressables and save compatibility remain within mobile budgets.
- Data validation catches missing keys, invalid techniques and unreachable campaign content.

### EVIDENCE REQUIRED

- Content matrix mapping each addition to a player decision.
- Stage playtest/comprehension and device profiler captures.
- Cut list for additions that do not justify themselves.

### EXIT DECISION

- **PASS:** sufficient breadth for ship cohort; open Phase 8 evaluation.
- **PARTIAL:** core content works; ship curated subset and defer breadth.
- **FAIL:** expansion reduces clarity/performance; roll back to validated subset.

---

## PHASE 8 — Meta / Economy / Retention

### PURPOSE

Make long-term progression support a core loop that has already demonstrated replay intent.

### DESIGN HYPOTHESIS

Shop, upgrades, missions and unlocks can motivate return by widening tactical expression without
turning the game into a forced stat grind.

### PROJECT FACTS / PREREQUISITES

- Currency, shop, upgrades, gacha, costume, missions and save backend already exist.
- Their breadth predates proof that weapon identity/build variety drives another run.
- Phase 3–7 evidence must identify what players actually want to pursue.

### IN SCOPE

- Evaluate weapon upgrade curve, mission goals, unlock pacing, currencies, mastery and whether gacha
  still fits the product.
- Align progression rewards with proven combat diversity and release cohort needs.

### OUT OF SCOPE

- Monetizing an unproven loop, energy/stamina, live-service complexity, PvP/clan/online leaderboard.

### IMPLEMENTATION RISKS

- Stat gates invalidate campaign promise.
- Gacha distributes power instead of interesting options.
- Missions reward behavior that conflicts with fun combat.

### PLAYER-FACING SUCCESS CRITERIA

- Player has a clear, desirable next goal after a run.
- Unlocks create new valid tactical expression while old weapons retain roles.
- Progress feels rewarding without forced grind or currency confusion.

### TECHNICAL ACCEPTANCE CRITERIA

- Save migration/idempotency, reward accounting and mission reporting pass.
- Economy tables are auditable and no stage is unreachable.
- Funnel/retention events measure actual decisions without blocking offline play.

### EVIDENCE REQUIRED

- First-session/first-hour/day-2 test data, reward pacing simulations and player interviews.
- Economy reachability audit and save/reward test matrix.

### EXIT DECISION

- **PASS:** meta reinforces replay and release gates; proceed to release tuning.
- **PARTIAL:** retain only goals shown to help; defer gacha/mission breadth.
- **FAIL:** meta introduces grind/confusion without replay lift; strip to the smallest ship loop.
