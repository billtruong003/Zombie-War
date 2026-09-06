# M6.1 Weapon Factory + Skill System — Claude/Opus Handoff

Copy everything from `PHASE` onward into Claude/Opus.

---

# PHASE: CONCEIVE — M6.1 WEAPON FACTORY + FULL SKILL SYSTEM

Project: `D:\Projects\Zombie-War`

Act as Lead Game Designer, Systems/Economy Designer, and Lead Technical Designer. This is a large investigation and canonical-documentation task. **Do not implement runtime systems.**

Before acting, read and follow:

```text
C:\Users\lilia\Downloads\files\game-designer.skill
```

Use repository instructions, Unity MCP/editor inspection, source inspection, prefab/material inspection, screenshots and vision. Do not infer visual suitability from filenames.

## Direction correction

The old three-gun-first framing is obsolete. The repository has 25 integrated weapons and hundreds of weapon-pack prefabs. M6 must design a scalable **Weapon Factory**: many visual weapons onboarded cheaply through a manageable set of gameplay families, shared balance formulas, family/tag skills, and mostly automated IK/authoring.

Three previously shortlisted weapons may remain examples, never roster limits. Do not create a bespoke mechanic/tree for every model.

## Owner-locked rules

- One endless procedural world; production uses `Map_Level1`.
- One weapon chosen before a run; no active in-run switching design.
- Auto-fire; no reload/magazine gameplay.
- No manual grenade button; explosive content becomes automatic powers.
- Level-up is 1-of-3, pauses at most 30 seconds, then auto-picks a valid card.
- Autonomous spectacle powers are desired.
- Skills belong to families/tags, not individual models.
- WebGL/mobile is a hard constraint.
- Maps 2–5 stay retired.
- M7 is not authorized.

## Read first

Read all current canonical M6/GDD/ship-plan documents and everything under `Review/M6_Conceive/`. Inspect the actual flow and data around `WeaponData`, `Weapon`, `WeaponGripPoints`, `WeaponIKController`, pose authoring/editor tools, roster migration, `LoadoutState`, Player roster, Hub/Shop/catalog, thumbnail generation, perks/offers, target selection, Bomb/explosion, pickups, enemies/bosses and save identity.

Use GitNexus where available. Documentation only means no production-symbol edits.

## Claim discipline

Every important claim must be marked:

```text
FACT · MEASURED · INFERENCE · PROPOSAL · TUNING HYPOTHESIS
OWNER-LOCKED · REJECTED
```

An enum, unused field, visual prefab or proposal is not an implemented mechanic.

---

# 1 — Reference-pattern study

Study verified structural patterns from relevant shipped games such as Megabonk, Brotato, Vampire Survivors, 20 Minutes Till Dawn and HoloCure. For each pattern provide a source/direct observation, what transfers, and what does not. Study one-weapon identity, large arsenals, family differentiation, rank/offer logic, autonomous powers, stations, optional bosses, endless pressure and meta unlocks. Do not copy balance numbers or fabricate familiarity.

Output: `Review/M6_WeaponFactory/REFERENCE_PATTERN_MATRIX.md`.

# 2 — Full arsenal audit

Audit every weapon-related ThirdParty pack and all project-owned weapons. Reconcile every prefab into exactly one bucket:

```text
CompleteWeaponCandidate · AttachmentOrPart · ColourVariant · DuplicateModel
DemoOrSceneObject · BrokenOrMissingDependency · OffStyle
NeedsShaderConversion · LaterSpecialFamily · Reject
```

For every complete candidate record:

```text
sourceAssetPath, sourcePack, observedModelIdentity, proposedWeaponFamily,
handedness, likelyFireMode, visualDistinctness, variantGroup,
materialCount, rendererCount, geometryEstimate, axes/bounds,
gripAvailability, muzzleAvailability, toonCompatibility,
recommendedDisposition, evidenceReference
```

Generate compact contact sheets grouped as pistols; SMG/AR/LMG; shotgun/marksman; launcher/energy/special; attachments/duplicates/rejects; representative weapons held by the real player. Open and vision-review the sheets.

Outputs:

```text
Review/M6_WeaponFactory/Inventory/weapon_inventory.csv
Review/M6_WeaponFactory/Inventory/weapon_inventory_summary.md
Review/M6_WeaponFactory/Evidence/*.png
Review/M6_WeaponFactory/VISION_REVIEW.md
```

All discovered counts must reconcile. Do not call every vendor prefab a weapon.

# 3 — Weapon-family grammar

Derive active families from usable assets and runtime feasibility. At minimum evaluate Sidearm, SMG, Assault Rifle, Shotgun, LMG and Marksman. Treat Launcher/Tesla/Laser/Flamethrower/Railgun as future only when real assets and credible runtime paths exist.

Every family needs:

```text
familyId, fantasy, gameplayIdentity, primaryMechanic, strength, weakness,
positioningDemand, targetPreference, signatureDecision, fireModes,
statEnvelope, allowedTags, forbiddenTags, twoSignatureSkills,
atLeastTwoBuildPaths, visualRule, audioRhythm, performanceRisk, assetCoverage
```

Mechanically redundant models become variants/skins, not fake unique content.

# 4 — Weapon schema and balance

Document these field groups and mark every field `EXISTS_AND_USED`, `EXISTS_BUT_DEAD`, `EXISTS_BUT_PARTIAL`, `DERIVED`, `NEEDS_NEW_FIELD` or `EDITOR_ONLY`.

```text
IDENTITY: weaponId, displayName, family, class, tier, catalogOrder,
roleTag, buildHint, synergyTags, variantGroupId, baseWeaponId

VISUAL: sourcePrefab, projectPrefab, icon, thumbnail, materialContract,
modelBounds, visualScale, handedness

IK: rightHandGrip, leftHandGrip, muzzlePoint, gripLocalPosition,
gripLocalEuler, gripLocalScale, rightHandGripRootPosition,
leftHandGripRootPosition, poseTemplateId, poseValidationStatus

FIRE: fireMode, damage, fireRate, range, automatic, pelletCount,
spreadAngle, pierceCount, pierceFalloff, knockback, damageFalloff,
projectileSpeed, explosionRadius, chainCount, chainRange,
beamWidth, beamRamp

HANDLING: moveSpeedModifier, aimTurnRateModifier, recoilKick,
recoilSideKick, targetPreference, min/maxEffectiveRange

PRESENTATION: fireSfx, muzzleVfx, impactVfx, tracer, beamVfx,
projectilePrefab, outlineContract

ECONOMY: unlockMethod, unlockCost, shopPrice, rarity,
cosmeticVariantGroup

DERIVED: rawDps, effectiveDps, crowdDps, singleTargetDps,
controlScore, mobilityScore, rangeScore, powerBudgetUsed
```

Explicitly retire reload/magazine from active design.

Define a spreadsheet-friendly power model starting from damage × fire rate × effective pellet contribution, then valuing accuracy, range, uptime, pierce, AoE, control, mobility and target priority. Specify family envelopes, tier philosophy, side-grade rules, fire-rate/move-speed soft caps, VFX/audio limits, boss versus crowd DPS and immediately playable unlocks. Numbers are hypotheses.

Outputs: `WEAPON_BALANCE_MODEL.md` and `weapon_balance_model.csv` under `Review/M6_WeaponFactory/`.

# 5 — Weapon Factory architecture

Design:

```text
Vendor prefab → classify → copy project-owned → material conversion
→ grip/muzzle discovery/authoring → family pose template
→ WeaponData generation → stable ID/variant group
→ icon/contact sheet → authoritative catalog → validation
```

Mark every step automatic, heuristic or manual. Target 80–90% automatic onboarding, but never promise perfect grip/muzzle inference.

Fail loudly on duplicate/missing ID, prefab, renderer/material, bad bounds/scale, missing grips/muzzle, wrong handedness, muzzle inside player/model, visible hand penetration, runtime material instances, missing catalog/UI/thumbnail or save collision. Specify real-player holding contact sheets and a manual-fix queue.

# 6 — Full 23-skill catalog

Fully specify exactly these 23 candidates. You may mark MUST/SHOULD/LATER/CUT but may not silently remove, rename or add entries.

```text
STAT (5)
1 Damage Up — weapon damage.
2 Fire Rate Up — fire rate with a soft cap.
3 Move Speed Up — movement speed.
4 Max Health Up — max/current health at selection.
5 Coin Gain Up — Coin gain; useful only with sinks.

SIDEARM (2)
6 Run & Gun — movement increases fire rate; stopping decays it.
7 Quickstep Round — distance travelled empowers the next actual auto-shot.

SMG (2)
8 Static Build-up — consecutive hits charge a chain-lightning discharge.
9 Bullet Hose — continuous fire ramps fire rate and spread; target loss decays it.

ASSAULT RIFLE (2)
10 Focus Fire — same-target hits ramp damage; switching/reset explicit.
11 Breach Round — every Nth shot pierces and applies temporary Exposed.

SHOTGUN (2)
12 Point Blank — close-range damage bonus.
13 Concussion — blast pushes and briefly slows enemies.

LMG (2)
14 Heavy Pressure — sustained fire ramps damage/control with a high-charge movement trade-off.
15 Shockwave Belt — every N bullets emits a forward cone shockwave.

MARKSMAN (2)
16 Longshot — distance increases damage/pierce.
17 Hunter's Mark — prioritizes boss/elite/special targets and empowers the first marked hit.

AUTONOMOUS (4)
18 Chain Lightning — periodic arcs through nearby enemies.
19 Ordnance Core — auto-bombs a dense cluster; reuses retired grenade content.
20 Soul Burst — every N kills explodes around the player.
21 Emergency Detonation — low-HP cooldown explosion for breathing room; no heal/invulnerability.

UNIVERSAL (2)
22 Execution Round — bonus damage below an enemy-HP threshold.
23 Kinetic Shield — distance travelled grants one stored hit-blocking shield.
```

Every skill must contain:

```text
IDENTITY: skillId, displayName, short/longDescription, iconCandidate,
rarity, status

CLASSIFICATION: layer, family, compatibleFireModes, synergyTags,
effectTags, implementationCost

ELIGIBILITY: minimumLevel, requiredFamilies/tags, blockedFamilies,
prerequisites, exclusions, maximumRank

OFFER: baseWeight, newSkillWeight, rankUpWeight, familyBias,
allowAutoPick, duplicatePolicy

TRIGGER: triggerType, threshold, cooldown/internalCooldown,
distanceRequired, hitsRequired, killsRequired, healthThreshold,
resetCondition

EFFECT: effectType, targetRule, baseMagnitude, perRankMagnitude,
duration/perRankDuration, radius/perRankRadius, maxTargets,
maxStacks, stackRule, damageSource/type

BALANCE: powerBudgetCost, expectedDps/survival/economy contribution,
softCap, hardCap, additiveOrMultiplicative, procRateLimit

FEEDBACK: VFX, SFX, tracer/impact, shakeLimit, HUD/world indicator,
colour, chargeReadability, cooldownReadability

TECH: existingPrimitive, newPrimitive, pooling, allocationBudget,
maxQueriesPerSecond, maxSpawnedEffects, WebGLRisk, determinism

VERIFY: unitTest, PlayModeTest, visualCapture, performanceGate,
playtestQuestion, failureCondition
```

Allowed trigger vocabulary: Passive, OnShot, OnHit, OnKill, OnDistanceTravelled, OnContinuousFire, OnTargetChanged, OnHealthThreshold, OnTimer, OnDamageTaken.

Allowed targeting vocabulary must cover CurrentTarget, NearestEnemy, PriorityEnemy, RandomEnemy, DensestCluster, EnemiesAroundPlayer, EnemiesInCone and EnemiesInLine.

Rank 1 unlocks the behavior; ranks 2–3 strengthen the same fantasy rather than becoming another skill.

Offer contract:

```text
Slot A prefers a compatible family signature
Slot B autonomous/universal
Slot C any valid card
```

Maximum one pure-stat card; no incompatible/max-rank cards; early run favors mechanic unlocks; later run favors coherent rank-ups; deterministic seeded offers; valid timeout auto-pick; explicit exhausted-pool and prerequisite/exclusion behavior.

Create at least two early/mid/late build simulations per active conventional family.

Outputs: `SKILL_CATALOG.md`, `skill_catalog.csv`, `BUILD_SIMULATIONS.md`.

# 7 — Pickups, interactives and endless loop

Audit and specify Coin, Gem, Health, XP delivery, optional temporary magnet/shield, skill reward, rare Relic Fragment, boss reward and retired Bomb pickup/art reuse. Fields: ID, status, source, drop rule, rarity, effect, collection method, auto-collect, secure/lost-on-death, destination, feedback, pooling/performance.

Fully specify Signal Relay, Supply Cache, Boss Beacon, Medical Station, Greed Terminal and Breakable Barrel/Crate. Fields: ID/status, deterministic spawn/ownership, visual body, required authored VFX/icon/ring, HUD signal, interaction/duration/cancel, risk/reward, cooldown/repeatability, persistence, enemy-pressure response, performance, failures and tests. A container/lamp alone does not solve Relay/Beacon visual language.

Specify the full endless run: Hub → one weapon → movement/auto-fire → level-up build → signals/interactives → optional bosses → death/manual abandon → settlement → unlock/collect/restyle → repeat. Account for all 16 enemy data assets, unused ranged/burrower/elite/boss roles and composition-based difficulty rather than infinite HP multiplication.

# 8 — Economy/outfit boundaries

Preserve: Coin common; Gem rare/secured/cosmetic-collection; Gold/Shard/star/gacha dormant; Relic remains a proposal without locked rates; death banks 25% Coin; abandon banks 0%. H2 outfit is 73% overall pass and 0% hard conflicts, not “0% failure” and not production-ready. Do not repeat the 300-outfit audit.

# 9 — UI/VFX/audio/WebGL and exact-25 migration

Specify weapon-family/variant presentation, skill cards/ranks/countdown, power charge/cooldown, signal compass, pickup rarity and station progress. Set guardrails for fire rate, tracers, impacts, lightning, explosions, physics queries, rapid-fire audio, materials, allocations and WebGL.

Inventory every exact-25 assumption. Design one authoritative `WeaponCatalog` feeding Player equip, Hub/Armory, Shop, thumbnails, cheats, save resolution and validation. Stable `weaponId` remains save identity; order is presentation only; weapon 26+ must not require editing every consumer; variants do not inflate family count. Name future impact-analysis targets but do not edit them.

# 10 — Canonical rewrite and M7 plan

Rewrite/update:

```text
Docs/M6_ENDLESS_RUN_SYSTEM_DESIGN.md
Docs/GAME_DESIGN.md
Docs/MVP_SHIP_PLAN.md
Docs/README.md
```

Remove the three-gun roster limitation, make Weapon Factory canonical, include the full 23-skill catalog and a Level-1 Vietnamese Simplifier, reconcile all counts, eliminate conflicting directions and avoid unexplained D1–D8 bureaucracy.

Plan but do not execute:

```text
M7.0 Weapon Factory foundation
M7.1 Mass weapon onboarding
M7.2 Skill runtime + approved pool
M7.3 Pickups/interactives/endless pressure
M7.4 Economy/meta/outfit
M7.5 Balance/WebGL/ship polish
```

For each: objective, dependencies, likely systems, deliverables, test/visual/performance gates and stop condition. Mark **M7 NOT STARTED / NOT AUTHORIZED**.

## Acceptance gates

Complete only if:

1. Every weapon-pack prefab is in a reconciled bucket.
2. Every complete candidate is inventoried.
3. Final visual sheets were opened and reviewed.
4. Active families have asset/runtime evidence.
5. Weapon fields are correctly marked used/dead/partial/derived/new.
6. Weapon Factory separates automatic/heuristic/manual work.
7. All 23 skills have the full schema.
8. No skill depends on reload, magazine, extra input or imaginary completed primitives.
9. Offer rules prevent all-stat/incompatible offers.
10. Two build simulations exist per active family.
11. Pickups and all six interactives have full contracts.
12. Exact-25 dependencies and migration are inventoried.
13. Canonical docs agree and claims have provenance.
14. Production code/scenes/prefabs/assets/materials/shaders/packages/build settings/vendor files are untouched.
15. M7 remains unauthorized.

If any gate is missing, report the exact gate; do not claim completion. Do not stop at a convenient checkpoint.

## Prohibited

No runtime implementation, mass prefab edits/imports, production IK/WeaponData changes, final balance assets, scene/build-setting changes, gacha activation, reload, manual grenade, per-model skill trees, or M7 work. Temporary inspection content is allowed but must be removed from `Assets/`; durable evidence goes under `Review/M6_WeaponFactory/`, never `Temp/`.

## Final report

```text
PHASE: CONCEIVE — M6.1 WEAPON FACTORY COMPLETE / INCOMPLETE

Facts verified
Arsenal counts and classification
Active/later families
Weapon Factory: automatic / heuristic / manual
Skills: 23/23 status, MUST/SHOULD/LATER/CUT, new primitives and risks
Pickups/interactives
Balance/build simulations
Canonical docs updated
Evidence created
M7 planning status only
Files modified (docs/review only)
Protected production state
Open risks
M6 STATUS: LOCKED / NOT LOCKED
M7 STATUS: NOT STARTED / NOT AUTHORIZED
BLOCKERS: none / exact blocker
```

Stop after documentation/design.

Completion audio policy: do not generate or play a spoken/TTS report. The owner wants only the project's short completion notification behavior.

## Completion notification

After the task is genuinely complete and verified:

1. Use the `simplifier-vi` skill at Level 1 to reduce the final report to the outcome, verification performed, and any important remaining issue.
2. Rewrite that summary in natural English using no more than 80 words.
3. Run:

   python Tools\notify_done.py "<English summary>" --repeat 2

If the task is blocked or fails, run the same command with a short English explanation of the blocker. Report any notification-script failure in the written response.
