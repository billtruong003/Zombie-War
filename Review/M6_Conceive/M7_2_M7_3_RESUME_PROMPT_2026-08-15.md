# PHASE: EXECUTE — RESUME: A5 + M7.2 + M7.3

Work in:

```text
D:\Projects\Zombie-War
```

M7.1 is delivered. This run finishes the leftover catalog debt and then builds the two parts that turn
the project into a game: **the level-up system that actually changes the run**, and **a world with
somewhere worth going**.

**Run all parts in order, in one pass. Do not stop to ask the owner anything.** Decide, record the
decision and the reasoning, keep going. If you run out of capacity, finish the part you are in, leave
the project building and playable, and say exactly where you stopped — the previous run did this
correctly and it was the right call.

Follow `CLAUDE.md`, `AGENTS.md`, `Docs/FRAMEWORK.md` and the `billgamecore` skill. BillTween only, never
DOTween.

---

## 0. Standing rules

```text
Run impact analysis before editing any symbol; report blast radius. Warn on HIGH/CRITICAL in the report
  but do not pause — make the change additive and reversible instead.
NEVER edit Menu.unity or any Assets/_Project/UI/Prefabs/Screens/UI_*.prefab. The owner owns UI layout.
  If a feature needs a UI prefab change, implement everything code-side and record the exact prefab
  change needed as an owner task. Do not do it yourself.
Never rebuild the Player skeleton/Animator/WeaponRig.
You never place a grip, muzzle or hand transform by inference. That is permanently the owner's work.
Play-test from Bootstrap.unity only, using the 25 already-authored weapons.
Do not stage, commit or push. After every editor operation, check git status and revert unintended
  UI-file changes.
Never generate or play a spoken/TTS report.
```

## 1. Verified starting state — independently audited, do not redo

```text
Inventory 891 rows; buckets reconcile (773 attachment · 39 duplicate · 28 complete · 24 needs-shader
  · 15 thrown/melee · 7 later · 4 demo · 1 colour variant)
54 WeaponData assets, 54 unique weaponIds
25 Ready · 28 PendingOwnerAuthoring · 1 BlockedNeedsProjectileFireMode
Families: AssaultRifle 15 · Sidearm 13 · Shotgun 12 · SMG 8 · Marksman 3 · LMG 2 · Launcher 1
Catalog holds 54; the Hub shows 24 playable
Weapon materials are project-owned (G1b closed)
GameplayOutlineContractTests now asserts coverage (prefabs == WeaponData count) instead of frozen
  counts; the per-renderer mask contract is unchanged and still strict
```

**Do not re-run the arsenal audit, re-onboard weapons, or touch grip/muzzle anchors.**

---

# PART A5 — Close the catalog debt (do this first, it has slipped twice)

Make the audio builders, dev/cheat tools, `CombatPower` and `GachaService` read `WeaponCatalog` instead
of scanning folders.

The audio requirement is the one that matters: with 54 weapons in the catalog, **a weapon without an
audio key must fail loudly at build time**, not silently at runtime. Today that failure surfaces only
when a player fires the gun.

Targets: `ZombieWarCuratedAudioBuilder`, `ZombieWarAddressableAudioCatalogBuilder`, `DevProfileTools`
(including the hard-coded "Unlock all 25 weapons" label — it is now wrong twice over),
`ZombieWarCheatPanel`, `CombatPowerAuditWindow`, `CombatPower`, `GachaService`.

`SceneFlowBuilder` and `LoadoutMenuInstaller` write into scenes/prefabs — leave them, and say so.

Prove "weapon 55 = one catalog entry and no consumer edit" with a test.

# PART A6 — Two inconsistencies the review found

1. **`ShotGun_D` / `WD_AssaultRifle_LegacyD`.** It was reclassified from Shotgun to AssaultRifle, but
   `weapon_inventory.csv` still records `proposedWeaponFamily: Shotgun`, and the reason column says only
   "distinct weapon body" — it does not carry the AK-pattern evidence the decision was based on.
   Reconcile the two records and write the actual evidence into the reason column. The body render is
   ambiguous between an AK rifle and a Saiga-pattern combat shotgun, so if you cannot evidence it,
   say so and flag it for the owner to confirm when authoring its grips.
2. **`ModernW` (20,708 tris) and `ModernX` (15,900 tris)** are roughly 2× the arsenal envelope
   (2,133–11,614). Mark them `NEEDS_DECIMATION` and keep them out of the playable pool even after grip
   authoring, until the owner decides keep / decimate / cut. They are unplayable today anyway, so this
   costs nothing and prevents a WebGL surprise later.

Also record, as known debt rather than a task: **43 of 54 weapons sit at tier Common**, because the 29
new bodies have no authored stats to measure a power band from. That is correct — inventing a tier
would invent a balance claim — but the M7.5 gate `tier == band(powerBudgetUsed)` cannot pass until those
stats exist. Note it in the M7 ladder so it is not discovered late.

---

# PART B — M7.2 · Skills, cards, and a level-up that does something

Today the level-up screen is presentation only and applies no choice. Kills grant XP, levels happen,
and nothing changes. This part is where the game gets its build.

## B1 — Build the 9 shared primitives first

From `Review/M6_DecisionLock/skill_shared_primitives.csv`:

```text
P1 per-enemy status/record carrier      P2 autonomous power framework
P3 multi-target spatial query           P4 player distance accumulator
P5 ramp/charge accumulator with decay   P6 damage-path interception hook
P7 distance-scaled damage curve         P8 stat soft-cap curve
P9 power VFX/HUD kit
```

Build and test these before any card. Twenty of the twenty-three cards are compositions of them. **If
you find yourself writing bespoke logic for a card, the primitive is wrong** — fix the primitive.

Reuse what genuinely exists: `WeaponData.RangeFalloff`, `knockback` → `ApplyPhysicalPush`, the perked
fire-rate multiplier, `PiercingLine`, `Bomb.Explode()`. Chain lightning and densest-cluster targeting do
**not** exist — `FireMode.ChainLightning` is an enum value with no code behind it. They are P3 work.

## B2 — Implement all 23 cards

Owner decision W2: all 23 are in scope. Build **MUST 15 → SHOULD 7 → LATER 1**, in that order, and do
not stop early. Full schema in `Review/M6_WeaponFactory/SKILL_CATALOG.md` and `skill_catalog.csv`
(23 rows × 64 fields). Do not add, remove or rename a card. Rank 1 unlocks the behaviour; ranks 2–3
strengthen the same fantasy rather than becoming a different skill.

Family-tagged signature cards must resolve against the **family**, not against a weapon id — there are
54 weapons now and there will be more.

## B3 — The offer system

```text
Slot A prefers a signature card compatible with the equipped weapon's family
Slot B autonomous or universal
Slot C any valid card
At most ONE pure-stat card per offer
Never offer an incompatible card, a max-rank card, or one whose prerequisites are unmet
Deterministic from the run seed — same seed, same offers
The ≤30 s unscaled pause auto-picks a VALID card on timeout, never a broken one
Explicit, tested behaviour when the pool is exhausted
```

Wire the choice into the existing level-up overlay **code-side only**. Do not edit UI prefabs. If the
card visuals need a prefab change, implement the logic, drive what you can from code, and write the
exact required prefab change into the owner task list.

## B4 — Feedback inside the budget

Use Epic Toon FX (`Assets/ThirdParty/Epic Toon FX/Prefabs/{Combat,Environment,Interactive,Misc}`) for
card pick, power procs and charge/cooldown readouts.

`Ordnance Core` and `Emergency Detonation` have art now: `Frag_Grenade_A`, `Impact Grenade_A`,
`ClayMore_A`, `Land Mine_A` in the Series V4 pack. Reuse them for the autonomous explosive powers and
for the retired Bomb pickup's art — the manual grenade button stays retired, the content returns as
automatic powers.

Everything pooled. Hold the guardrails: fire rate soft cap 2.2× / hard cap 2.5×; ≤6 lightning arcs per
proc and ≤2 procs/s across all sources; ≤2 concurrent explosions; ≤1 `OverlapSphere` per power proc;
clustering ≤1/s over ≤64 enemies; zero runtime material instances; 0 bytes/frame steady state.

## B5 — Prove it

Tests: offer determinism from a seed, no all-stat offer, no incompatible or max-rank card, timeout
auto-pick always valid, exhausted pool handled, soft caps enforced, each primitive tested directly.

Then play from `Bootstrap.unity`: two runs with the same weapon must produce visibly different builds.

---

# PART C — M7.3 · Stations, pickups, endless pressure

This part answers "why would I walk over there".

## C1 — The World Signal Language

Build the prop-independent visual contract first: ground ring, vertical beam, floating icon, emissive
accent, progress indicator, one distinct colour per station type. Assemble from Epic Toon FX. Any prop
the owner supplies later gets dressed by this language — the prop carries the mass, the language
carries the meaning.

## C2 — Station bodies — choose and record

Pick final bodies yourself and produce a review sheet so the owner can override later:

```text
Signal Relay   a Dark Fantasy pillar/obelisk or statue — vertical mass, readable at distance
Supply Cache   an existing container/crate, or SM_Prop_Chest_01
Boss Beacon    a gargoyle statue or SM_Prop_Altar_Table_01 — ominous, not lootable
Breakable      SM_Prop_Barrel_01 / SM_Prop_Crate_01
```

Every Synty prop entering the game must pass the same toon material/outline contract the weapons do.
Convert; do not ship raw Synty materials.

Check the result at gameplay camera distance: Relay and Beacon must read as two different things.

## C3 — The three stations

Signal Relay, Supply Cache and Boss Beacon, each with the full contract: deterministic anchor-based
spawn, ownership, HUD signal, interaction and duration, cancel rule, risk/reward, cooldown and
repeatability, persistence, enemy-pressure response, failure handling.

```text
Anchors are deterministic and global — never chunk-local random
Recycling a visual chunk must not reset an objective, duplicate loot or respawn a destroyed station
No orphan boss when its chunk unloads
No free chest
At most one major encounter owns the player's attention at a time
```

**Boss Beacon uses existing VAT bosses only** (`ENM_CactusBoss_VAT`, `ENM_MoleRatKing_VAT`,
`ENM_SkeletonGiant_VAT`). Kaiju stays out — it ships with zero animation clips and the enemy pipeline is
VAT-baked, so making it fight is its own milestone under owner supervision.

## C4 — Endless pressure

Remove the wave-clear auto-collect (`PickupManager.cs:141` collects on `WaveClearedEvent`). An endless
world has no reliable wave boundary, and auto-collecting removes the reason to move toward loot.

Implement the Threat model as composition change before stat inflation:
`ThreatTier = base + objectiveProgress + distanceBand + boundedTimePressure`. Tier 0 walkers/runners;
tier 1 adds one specialist; tier 2 mixed specialists plus elite chance; tier 3 recovery-window enemies
and the boss route. Eleven of the sixteen enemy assets are unused in production — reach for composition,
not HP multiplication.

---

## 2. Decision authority

**Decide yourself, record the choice and why:**

```text
Primitive API shape, file layout, namespaces, class names
Card implementation order inside each MUST/SHOULD/LATER band
Station body prefabs, colours, FX selection, signal proportions and timings
Anchor spacing, station frequency, threat curve starting numbers (all TUNING)
Which existing boss serves the first Boss Beacon
Toon conversion choices for Synty props
```

**Never decide — record as owner tasks:**

```text
Anything requiring a UI prefab or scene edit
Grip, muzzle or hand transform placement
Whether ModernW/ModernX are kept, decimated or cut
Reactivating gacha, star upgrades or any dormant economy system
Changing an owner-locked rule (one weapon per run, auto-fire, no reload, banking rates,
  the 23 card names, the six families)
```

## 3. Acceptance gates

**A5 / A6**
1. Audio builders, dev/cheat tools, `CombatPower`, `GachaService` read the catalog; a weapon missing an
   audio key fails the build; "weapon 55 = one entry" proven by test.
2. `ShotGun_D` records reconciled between inventory and `WeaponData`, with real evidence or an explicit
   flag for owner confirmation.
3. `ModernW` / `ModernX` marked `NEEDS_DECIMATION` and excluded from the playable pool.
4. The tier-Common debt recorded in the M7 ladder.

**Part B**
5. The 9 primitives exist, are individually tested, and every card is a composition of them.
6. All 23 cards implemented; none added, removed or renamed; ranks 1–3 behave as specified.
7. Signature cards resolve by family, never by weapon id.
8. Offer rules hold: max one stat card, no incompatible/max-rank, deterministic from seed, timeout
   auto-pick always valid, exhausted pool handled.
9. The level-up choice actually applies in game, wired without editing any UI prefab.
10. Performance guardrails hold; 0 alloc/frame steady state.

**Part C**
11. Signal language exists independently of any prop and reads at gameplay camera distance.
12. Three stations implemented with full contracts and deterministic anchors.
13. Chunk recycling cannot reset, duplicate or orphan station state; no free chest; no orphan boss.
14. `CollectAll()` on `WaveClearedEvent` removed.
15. Threat model changes composition before stats.
16. Synty props pass the toon material/outline contract.

**Whole run**
17. Existing tests stay green; new behaviour has new tests.
18. Bootstrap play-test on the authored 25: pick a weapon, level up and take cards across several
    levels, reach a station, trigger a boss beacon.
19. `git status` shows zero modified UI prefabs and zero modified scenes; nothing staged.
20. `detect_changes()` run and reported.

## 4. Final report format

```text
PHASE: EXECUTE — A5 + M7.2 + M7.3 — COMPLETE / PARTIAL (say exactly where you stopped)

A5 — consumers migrated, audio fail-loud proven, what was left and why
A6 — ShotGun_D reconciled, ModernW/X flagged, tier debt recorded
PART B — the 9 primitives, 23 cards (list any that are stubbed and say "stubbed"),
         offer rules, level-up wiring, FX and performance numbers
PART C — signal language, station bodies chosen with reasons, three stations, threat model,
         auto-collect removed
Decisions made under delegated authority, and why
Owner tasks recorded
Impact analysis: symbols, blast radius, HIGH/CRITICAL and how each was made safe
Tests: existing green, new tests added, any test you modified and exactly why
Bootstrap play-test result
detect_changes() output
Protected state: UI prefabs, scenes, staged files — git evidence
Files modified — full list
Open risks
A5 / M7.2 / M7.3 STATUS: each DELIVERED / PARTIAL / NOT STARTED
BLOCKERS: none / exact blocker
```

Report what genuinely works. If a card, primitive or station is stubbed, use the word "stubbed" rather
than listing it as delivered. If you modify an existing test, state which contract you preserved and
which expectation you changed — the last run did this well and it is what makes the test suite worth
trusting.

Completion audio policy: do not generate or play a spoken/TTS report. The owner wants only the
project's short completion notification behavior.

## Completion notification

After the task is genuinely complete and verified:

1. Use the `simplifier-vi` skill at Level 1 to reduce the final report to the outcome, verification performed, and any important remaining issue.
2. Rewrite that summary in natural English using no more than 80 words.
3. Run:

   python Tools\notify_done.py "<English summary>" --repeat 2

If the task is blocked or fails, run the same command with a short English explanation of the blocker. Report any notification-script failure in the written response.
