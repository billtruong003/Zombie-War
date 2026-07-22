# Claude execution prompt — VAT enemy roster + five-stage campaign

> Recommended model: **Claude Opus**, strongest available. Do not use Haiku. Use Sonnet only if
> manually splitting this contract into separate independently verified tasks.

Copy everything below the separator into Claude.

---

# ZombieWar — uninterrupted VAT enemy roster, five-stage campaign, rewards and Pass missions

Work in:

`D:\Project\ZombieWar`

Use `expert-developer` for the whole task. Also use the project `billgamecore` skill, GitNexus
exploration/debugging/impact-analysis, and the relevant Unity skills for project scouting, assets,
importers, Animator inspection, VAT authoring, prefabs, ScriptableObjects, scenes, UI, NavMesh,
profiling, validation and tests. Use Unity MCP continuously for source inspection and runtime proof.

This is a large implementation task. Execute it phase by phase without stopping for ordinary
decisions. Do not ask the owner to choose filenames, clip mappings or numbers that can be discovered
from imported assets/source. A HIGH/CRITICAL GitNexus result must be surfaced immediately before the
edit, but continue when the change is directly required and bounded by tests. Stop only for a truly
destructive ambiguity, missing licensed source, unrecoverable corruption or a blocker that cannot be
worked around safely.

## Non-negotiable animation architecture

**Every gameplay monster must use the existing VAT architecture.** The imported packs are source
art only.

Runtime enemy visuals must be:

```text
Enemy root
├── NavMeshAgent
├── CapsuleCollider
├── Health
├── existing ZombieBase-derived behavior
└── Visual
    ├── MeshFilter with baked VAT mesh
    ├── MeshRenderer with VAT material
    └── VAT_Animator with VAT_AnimationData
```

Forbidden in production enemy prefabs:

- `Animator`;
- `Animation`;
- `SkinnedMeshRenderer`;
- vendor controller as a runtime controller;
- a parallel Mecanim enemy FSM;
- a second pool/spawner/health/damage architecture;
- runtime VAT baking or runtime prefab construction.

Animator Controllers and skinned vendor prefabs may exist only in the editor bake source/temp stage.
If a monster cannot bake correctly, mark that individual monster blocked with exact evidence. Never
silently ship it as a Mecanim exception.

Read and follow:

- `Docs/PREFAB_CONVENTIONS.md`
- `Docs/GAMEPLAY_DESIGN.md`, especially the VAT requirement
- `Assets/_Project/Scripts/Editor/ZombieVATBaker.cs`
- `Assets/ThirdParty/VAT/Editor/VAT_BakerEditorWindow.cs`
- `Assets/ThirdParty/VAT/VAT_AnimationData.cs`
- `Assets/ThirdParty/VAT/VAT_Animator.cs`
- current `ZombieBase`, `ZombieWalker`, `ZombieRunner`, `ZombieRanged`, `ZombieBoss`

Do not reinterpret this rule.

## Imported source packs — inspect, never mutate

### Monster source pack A

`Assets/Monsters Ultimate Pack 03 Cute Series`

Observed import inventory: 958 files, about 226.5 MB, 303 FBX, 19 prefabs, 16 Animator Controllers,
15 actual creature groups plus demo assets:

1. Burrow
2. Cacti
3. Cactus Boss
4. Cactus
5. Cat Bolt
6. Cat Lightning
7. Cat Meow
8. Dog Bark
9. Dog Bowwow
10. Dog Pup
11. Mole Rat
12. Mole Rat King
13. Skeleton
14. Skeleton Giant
15. Skeleton Mage

The source includes meaningful clips such as idle/walk/run, damage/death, bite/slash/projectile,
pounce, dash, cast, underground transitions, resurrect, fly, jump-smash and boss attacks. Inspect the
real AnimationClip sub-assets and import settings; filenames alone are not proof.

### Monster source pack B

`Assets/GAMWILL Character Pack Monster  Bionic Cartoon Zombie Gorilla`

Observed source: `Mesh/HUGO_T_Pose.fbx`, one prefab, three materials/five textures, controller and
11 animation FBXs: `Dashe`, `Death`, `Idle_1..4`, `Jump`, `Run`, `Run_Attack`,
`Run_Attack_Left`, `Run_Up_attack`.

`ZombieVATBaker.Configs` already contains a HUGO entry, but no HUGO VAT output/prefab/data currently
exists. Extend/fix the existing pipeline; do not create a second Gorilla implementation.

### Resource/reward source pack

`Assets/KayKit/Packs/Bits/KayKit - Resource Bits (for Unity)`

Observed source: 132 models and 132 prefabs. Reuse only semantically correct assets such as
`Money_Coins_Stack_Single`, money piles/bills, Gold bars/nuggets, `Gem_Small`, gem piles/chest and
containers for reward/pickup/Pass presentation. Do not copy or rename the vendor pack. Do not use a
random resource mesh merely because it exists.

All three vendor roots are read-only inputs. Any URP material duplicates, baked VAT data, gameplay
prefabs, thumbnails and configs belong under `Assets/_Project/`.

## Existing project truth — reuse it

- Unity 6000.3.10f1, URP, portrait mobile.
- Bootstrap loads Menu/gameplay additively through `GameFlow` and BillGameCore.
- `WaveDirector` + `WaveData` + `ZombieSpawner` + `ZombieManager.AliveCount` already own waves.
- Enemies already use `Bill.Pool`, `Health`, `IDamageable`, target registry, NavMesh and a four-state
  `ZombieBase` FSM with tiered Full/Cheap/Inactive simulation.
- Existing behaviors: walker, runner, ranged projectile, boss AoE.
- Existing source-to-VAT pipeline: `ZombieVATBaker` resolves clips, builds a temporary controller,
  calls `VAT_BakerEditorWindow.BakeObjects`, creates `VAT_AnimationData`, `ZombieData` and the exact
  root/Visual prefab structure above.
- Current baked baseline is `ZD_Zombie` + `Zombie_VAT.prefab` + `Zombie_VAT_Data.asset`.
- `GameFlow` currently hardcodes `Map_Level1`; campaign selection does not exist.
- `WD_Level1.asset` is five simple waves using the baseline zombie only.
- Hub PLAY currently starts gameplay directly. Pass UI exists but its missions/claims are placeholders.
- `PlayerProfile` is the one persistent save/wallet/loadout/costume/gacha/upgrade authority.
- 25 canonical weapons, weapon stars, Player rig, Pro Casual 448 items + 30 sets, Shop and Gacha are
  complete. Do not rebuild them.
- UI must remain prefab-first and Inspector-editable.
- The current run-result/reward/XP/perk prerequisite is specified in
  `Docs/NEXT_PHASE_RUN_LOOP_PROMPT.md`. Audit actual source first: if that task was already completed,
  reuse it; if absent or partial, finish only the missing prerequisite inside this task. Never create
  a parallel run/wallet/perk system.
- Baseline before this expansion was 134/134 EditMode and 5/5 PlayMode tests.

## Phase 0 — complete read-only audit

Before edits:

1. Read `AGENTS.md`, `CLAUDE.md`, `Docs/ACCOUNT_SWITCH_HANDOFF.md`,
   `Docs/NEXT_PHASE_RUN_LOOP_PROMPT.md`, `Docs/PREFAB_CONVENTIONS.md`, `Docs/GAMEPLAY_DESIGN.md`,
   `Docs/ECONOMY_DESIGN.md`, `Docs/PROFILE_SAVE.md`, `Docs/UI_ARCHITECTURE.md`,
   `Docs/TASK_BREAKDOWN.md`, `Docs/PRODUCT_ROADMAP.md` and weapon roster docs.
2. Preserve the large dirty worktree. No reset, clean, restore, broad reimport, vendor move or
   opportunistic cleanup.
3. Use GitNexus `query`/`context` to trace the existing VAT bake, spawn, wave, damage/death, pooling,
   run results, profile save, Hub PLAY, GameFlow restart/home and Pass flows.
4. Before every symbol edit run upstream `impact` and record direct callers, processes/modules and
   risk. The current GitNexus index reports 29,018 nodes, 36,863 edges and 300 flows.
5. Use Unity MCP to inspect every imported model/prefab/controller/material and AnimationClip
   sub-asset. Record rig type, avatar, scale, facing, root motion, clip duration/wrap mode, renderer,
   material/shader, bounds, bones and missing references.
6. Check Console after import. Current known MCP transport-disconnect messages are tooling noise;
   do not confuse them with asset/compiler/runtime errors.
7. Generate `Docs/ENEMY_ROSTER_AUDIT.md` with one row per creature and columns:
   stable ID, source paths, selected role, size class, chosen idle/move/attack/hit/death/special clips,
   clip durations, VAT viability, shader/material status, intended first stage, reward/XP, prefab/data
   output and any blocker.
8. Capture editor previews/contact sheets showing each source creature front/three-quarter view with
   its name. Visual inspection is mandatory; filenames are insufficient.

Then continue automatically.

## Phase 1 — finish the run-loop prerequisite only where missing

Audit whether `NEXT_PHASE_RUN_LOOP_PROMPT.md` has already been implemented. The campaign requires:

- one in-memory run authority for kills, wave, Coin/Gold/Gem actually earned, XP, level, temporary
  perks, elapsed time and terminal result;
- exactly-once kill/reward semantics across pooled disable/return;
- real HUD run Coin/XP;
- real 1-of-3 temporary perks that compose with weapon stars;
- Defeat and Victory result snapshots;
- one atomic/idempotent terminal payout into `PlayerProfile`;
- clean Replay/Home reset.

Implement only missing parts, using the previous contract and tests. Do not pause after this phase.

## Phase 2 — professional, repeatable VAT authoring pipeline

Extend/refactor the existing `ZombieVATBaker`; do not fork it.

### Authoring requirements

- Represent all 16 monsters through deterministic bake definitions. Use the smallest editor-visible
  configuration that avoids an unreadable one-off script. A ScriptableObject bake catalog is valid
  if it materially improves clip mapping/auditing; do not add abstraction for appearance alone.
- Give each creature a stable enemy ID, e.g. `enemy.cute.dog_pup`, `enemy.cute.skeleton_mage`,
  `enemy.gamwill.hugo`.
- Deterministic outputs:
  - `Assets/_Project/Art/VAT/Enemies/<enemy-id>/...`
  - `Assets/_Project/Prefabs/Enemies/ENM_<Name>_VAT.prefab`
  - `Assets/_Project/Data/Zombies/ZD_<Name>.asset`
- Re-running the baker updates outputs without duplicate assets, sub-assets, components or GUID churn
  where avoidable. Use Undo/SetDirty/SaveAssets correctly and produce an audit summary.
- Select only clips used by runtime: idle, in-place move, primary attack, hit, death and at most the
  genuinely used spawn/special clips. Do not bake all 303 FBXs into giant textures.
- Prefer in-place locomotion. VAT runtime root motion is forbidden; NavMeshAgent owns translation.
- Set loop semantics correctly: idle/move/fly/underground loop; attacks/hit/death/transitions once or
  clamp. Verify actual imported clip names after Unity prefixes such as `root|`.
- Validate baked mesh, texture, bounds, clip lookup, material and Visual facing/offset/scale.
- Duplicate/fix materials under `_Project`; never mutate vendor materials. Use actual compatible URP
  shader properties, preserve albedo/normal/emission when present, and prove zero pink material.
- Production prefabs must contain zero Animator/SkinnedMeshRenderer and exactly one active
  `VAT_Animator`/MeshRenderer Visual.
- Keep logic/physics root axis-aligned. Any art correction is on `Visual`.
- Build collider/agent dimensions from measured visual bounds, then inspect them in Scene view.
- Validate VAT asset size/texture dimensions and report total added disk/memory cost. Avoid needless
  4K/8K textures and duplicate materials.

### Required VAT smoke proof per monster

For all 16: instantiate the final VAT prefab, play/crossfade every selected clip, verify visible
movement, no exploding vertices, no frozen first frame, correct facing/ground contact/bounds and no
material corruption. Capture a labeled contact sheet of the final VAT outputs—not vendor skinned
sources.

## Phase 3 — group by gameplay behavior, not by species

Use existing behavior components wherever their mechanics fit. Do not create 16 subclasses.

Target grouping, subject to measured animation/mesh evidence:

| Archetype | Candidates | Intended behavior |
|---|---|---|
| Walker | Dog Pup, Cat Meow, Skeleton | readable basic melee/chase |
| Runner/Pouncer | Dog Bark, Dog Bowwow, Cat Bolt, Cat Lightning | fast close, telegraphed pounce/dash |
| Ranged/Caster | Cacti, Cactus, Skeleton Mage | keep distance, pooled projectile/cast |
| Burrow/Ambush | Burrow, Mole Rat, Mole Rat King | underground transition, reposition, emerge telegraph |
| Heavy/Elite | Skeleton Giant, Cactus Boss | slow pressure, slam/area or heavy attack |
| Final boss | HUGO Gorilla | dash/charge plus heavy telegraphed attack |

Rules:

- Reuse `ZombieWalker`, `ZombieRunner`, `ZombieRanged`, `ZombieBoss` when sufficient.
- Add a behavior class only for a materially different mechanic such as burrowing or a boss charge.
- Common FSM/pooling/death/reward/tiering remains in `ZombieBase`.
- Extend data for stable ID, threat tier, reward/XP, attack wind-up, optional special clip names and
  tuning only when those are shared authored facts. Never hardcode per-species stats in UI.
- Sync damage/projectile release with a measured attack wind-up from the actual clip. VAT has no
  Mecanim events at runtime; use deterministic timing, one hit per attack, cancellation on death and
  pool-safe reset.
- Pounce/dash/burrow abilities need clear anticipation, recover window, cooldown and collision-safe
  motion. No teleport directly onto the Player without a warning.
- Ranged projectiles use `Bill.Pool`; reuse/extend `ZombieSpitProjectile` rather than instantiate.
- Specials must degrade safely in Cheap/Inactive tiers and stop on Player death/GameOver.
- Bosses cannot apply duplicate damage during one animation. Death/pool return remains exact once.

## Phase 4 — five-stage campaign data and progression

Create one editor-visible campaign authority, not five hardcoded switch statements. A compact
`CampaignCatalog`/level definition asset should provide:

- stable level ID and ordered index;
- localized/display name;
- scene name;
- WaveData reference;
- recommended/minimum Combat Power;
- suggested weapon families and exact current weapon IDs;
- first-clear and repeat rewards;
- completion/unlock state contract;
- optional boss/elite metadata.

Persist through `PlayerProfile` additively and safely:

- highest unlocked/completed stage or stable completed IDs;
- best score/wave/time per stage where useful;
- claimed first-clear rewards;
- current/last selected stage;
- Pass mission progress if implemented later in this task.

Stage 1 is always available. Stage N requires Stage N-1 cleared plus the authored minimum Combat
Power. Calculate Combat Power from actual equipped weapon effective DPS, slot coverage and star
levels using one documented formula—not asset index, price or rarity alone. Display both minimum and
recommended values. Never require ownership of one exact paid weapon: recommendations must include
at least one attainable alternative. A failed gate changes no state and offers navigation to Loadout
or Shop.

Completion and first-clear rewards must be atomic/idempotent. Replaying is allowed and cannot repeat
first-clear rewards.

## Phase 5 — simple prefab-first campaign selector

Hub PLAY must open a new authored `UI_CampaignScreen.prefab`; it must no longer immediately hardcode
Map_Level1.

Keep the screen intentionally simple as requested:

- Back button to Hub;
- five clear circular stage dots/nodes;
- left/right arrow navigation;
- selected stage number and real stage name;
- Locked / Available / Completed state;
- minimum/recommended Combat Power and current Player power;
- suggested weapon family plus real owned/unowned weapon examples and icons;
- primary `CHƠI` button;
- honest lock explanation and Loadout/Shop shortcut when underpowered.

No fake map thumbnails and no runtime-built permanent UI. Use existing UIKit/theme/safe-area/card
patterns. Author serialized references into prefab and Menu scene. Add the screen to UI navigation,
scene contracts, validation and authoring preview. Back/Escape must work.

Parameterize `GameFlow` around the selected campaign scene. Restart must reload the active selected
stage, and Home must unload whichever campaign scene is loaded. Never leave an orphan Player or two
maps loaded. Update build settings deterministically.

## Phase 6 — five editable placeholder maps

Create five real editor-visible scenes, preserving the existing scene contract. Prefer retaining
`Map_Level1` as Stage 1 and create clearly named Stage 2–5 scenes unless the audit proves a safer
contract.

Each stage initially contains only a clean placeholder arena:

- one large flat Plane/ground, approximately 70–90 m across after measured camera/spawn testing;
- distinct simple material tint so screenshots cannot be confused;
- PlayerSpawnPoint;
- gameplay camera/follow setup;
- Directional Light and required render/volume settings;
- EventSystem/HUD/RunOverlays;
- ZombieManager, WaveDirector, ZombieSpawner and stage WaveData;
- NavMeshSurface/baked data;
- authored spawn points/zones around the perimeter with minimum Player distance and no immediate
  spawn on camera;
- no chunk streaming, procedural environment, trees or decorative obstacle design yet.

Do not hide scene construction in runtime code. Build/clone with an idempotent editor authoring tool
using Unity APIs, not raw YAML. Do not blindly rerun a destructive old scene builder. Inspect diffs
for `Map_Level1` and preserve its working references.

Use KayKit resource props only for pickups/reward presentation in this pass, not as arbitrary map
decoration or collision obstacles. The owner will provide a separate tree/obstacle design later.

Bake NavMesh for every scene and prove paths from every spawn zone to representative Player
positions. Planes must not have holes, wrong layers or unusable agent settings.

## Phase 7 — staged enemy introduction and WaveData

Author separate WaveData per stage. Introduce mechanics gradually; difficulty must come from enemy
mix, cadence and behavior before raw HP inflation.

Use this roster direction, correcting only when measured animation/behavior evidence requires it:

### Stage 1 — First Outbreak

- Dog Pup, Cat Meow, basic Skeleton;
- simple walkers, low concurrency, generous rests;
- late Dog Bark runner as first elite pressure;
- suggested weapons: starter pistol / Generic SMG; teach movement and switching.

### Stage 2 — Thorn Fields

- Cacti, Cactus, Burrow, Dog Bark;
- introduce ranged projectile and one readable underground ambusher;
- Cactus Boss as stage boss;
- suggested families: shotgun for ambush + AR/SMG for ranged control.

### Stage 3 — Bone Yard

- Skeleton, Skeleton Mage, Skeleton Giant, selected earlier support enemy;
- introduce caster spacing and heavy slam;
- Skeleton Giant as boss/elite climax;
- suggested families: accurate AR/marksman plus strong sidearm/shotgun backup.

### Stage 4 — Wild Pack

- Cat Bolt, Cat Lightning, Dog Bowwow, Mole Rat, Mole Rat King;
- fast pounce packs plus burrow pressure, lower simultaneous ranged noise;
- Mole Rat King as boss;
- suggested families: high fire-rate SMG/AR and semi/auto shotgun.

### Stage 5 — Titan Siege

- curated mixed roster from prior stages, not all 16 at once;
- Cactus Boss or Skeleton Giant as a milestone elite;
- HUGO Gorilla final boss using real VAT dash/heavy animations;
- suggested families: highest sustainable DPS owned AR/AA-12-class shotgun plus marksman option.

For every stage:

- 5–10 authored waves appropriate to intended duration;
- total counts, max concurrent, spawn interval, rest and boss milestone visible in Inspector;
- no wave deadlock when spawn fails or enemy pools return;
- at most 2–4 enemy concepts introduced at once;
- no boss before its required prefab/ability is proven;
- mobile-safe maximum concurrency based on profiler evidence;
- XP/reward budget consistent with Shop/Upgrade costs and campaign power gate;
- exact level-clear terminal path and reward payout.

Generate `Docs/CAMPAIGN_BALANCE_TABLE.md` with stage roster, first appearance, wave counts, enemy
stats/rewards, expected effective DPS, target TTK, expected run income, minimum/recommended power,
suggested weapons and first-clear rewards. Mark tuning provisional.

## Phase 8 — KayKit pickups and campaign rewards

If the run-loop prerequisite already has abstract instant rewards, extend it cleanly into physical
pooled pickups without changing the one authoritative run ledger.

- Common enemy: pooled Coin visual using KayKit coin assets.
- Elite/boss/milestone: authored Gold chance/guarantee using correct KayKit Gold visual.
- Gem remains rare and authored only; no routine Gem farming.
- Stage-clear reward chest may use `Gems_Chest`/container only when contents match the result UI.
- Pickups use `Bill.Pool`, one trigger/collider, deterministic collect-once guard and magnet behavior.
- Wave clear auto-collects remaining valid pickups before final reward/result.
- Pool cleanup/scene unload cannot grant duplicate currency.
- UI and terminal payout still read one run ledger; pickups do not write profile per collect.

If physical pickups cannot be completed safely in this already large pass, finish campaign with the
existing in-memory reward event, leave pickup visuals explicitly disabled, and report this one scope
deferment. Do not ship half-counting rewards.

## Phase 9 — weapon recommendations and power curve

Read all 25 `WeaponData` assets and `WeaponUpgradeMath`. Calculate effective DPS with pellets,
fire rate, reload/magazine, range/accuracy where relevant, and star modifiers. Do not compare raw
`damage` alone.

- Give each stage 2–4 recommended weapon families and real IDs from the current roster.
- Include a starter/free attainable recommendation at early stages.
- Recommendations are guidance; the Combat Power floor is the actual gate.
- Avoid circular progression where the Player needs a weapon obtainable only after the gated stage.
- Verify Shop prices, expected stage income, shard/Gold upgrade costs and required power form a
  reachable cadence without dev cheats.
- Add an Editor audit window/menu command showing equipped Combat Power, per-weapon effective DPS,
  stage thresholds and pass/fail reason. It must be read-only except existing dev cheats.

## Phase 10 — real Battle Pass mission list

The existing Pass screen is presentation-only. Create a small real mission catalog and progress
backend driven by existing typed gameplay/profile events; do not poll every frame.

At minimum author these mission families with tuned targets, Pass XP and honest rewards:

### Daily pool

1. Kill 50 enemies.
2. Kill 150 enemies.
3. Clear 5 waves.
4. Finish one campaign stage.
5. Collect 250 Coin during runs.
6. Choose 3 temporary perks.
7. Kill 20 Runner/Pouncer enemies.
8. Kill 10 Ranged/Caster enemies.
9. Kill 8 Burrow/Ambush enemies.
10. Defeat one elite or boss.
11. Finish a stage with a recommended weapon family equipped.
12. Switch weapons 10 times during combat.

### Weekly/campaign pool

13. Kill 1,000 enemies.
14. Clear 25 waves.
15. Finish 10 campaign runs.
16. Defeat HUGO once.
17. Clear Stages 1–5 at least once.
18. Earn 5,000 Coin from runs.
19. Defeat each stage boss.
20. Clear a stage without taking lethal damage/revive.

Rules:

- Stable mission IDs; data-driven target/type/reward/Pass XP.
- Daily/weekly selection deterministic from UTC day/week or a simple clearly documented reset key.
- Progress and claims persist in `PlayerProfile` additively and normalize safely.
- One gameplay event increments each relevant mission at most once.
- Claim is atomic/idempotent; never double reward.
- Premium Pass remains visibly locked/unavailable—no fake IAP.
- Pass UI binds real mission title, progress/target, reward, claim state and refresh event. Use a
  pooled/paged list if needed; remove hardcoded fake percentages.
- The free track may remain minimal, but any visible claim must work. If full tier-track rewards are
  outside the pass, hide/disable them honestly and complete the mission list/backend first.

## Phase 11 — validation, tests and runtime evidence

### Automated coverage

Add or extend EditMode/PlayMode tests for:

- 16 deterministic VAT definitions and unique stable enemy IDs;
- required clip resolution, loop/once semantics and missing-clip failure;
- every baked `VAT_AnimationData.IsValid`, selected clip lookup and output reference;
- every production enemy prefab exact root/Visual contract and zero Animator/SMR;
- collider/agent/bodyRenderer/data wiring;
- pool reuse resets state, cooldowns, death guard, rewards and special abilities;
- attack timing applies exactly one hit; projectile/burrow/pounce/boss paths;
- five Campaign level IDs/scenes/WaveData/build-settings entries;
- scene contract, spawn zones and no orphan Player across level/restart/home transitions;
- progression lock, previous-clear rule, power gate and save/reload;
- first-clear reward idempotency;
- wave completion/deadlock behavior for mixed enemies and bosses;
- Combat Power and weapon recommendation calculations;
- pickup collect-once/auto-collect/payout if implemented;
- all 20 mission definitions, event progress, day/week reset, claim idempotency and save rollback;
- all old profile/shop/gacha/costume/weapon/run tests remain green.

### Unity MCP proof

Do not rely on logs alone.

1. Inspect source and final VAT prefabs in Scene view.
2. Spawn every one of the 16 final enemies from a fresh pool instance.
3. For each: prove idle, movement, attack/special, hit, death, dissolve and pool reuse visually.
4. Verify no final enemy contains Animator/SMR at edit time or runtime.
5. Run a mixed-horde stress sample at 25/50/100 enemies as hardware allows; record CPU, GC,
   draw calls and memory. Do not claim mobile-safe without numbers.
6. Navigate Hub → Campaign, all five dots/arrows/back, lock states, power values and recommendations.
7. Enter each of the five real scenes and verify plane, camera, Player, HUD, NavMesh, spawns and wave.
8. Complete accelerated deterministic versions of all stages, including each boss and HUGO.
9. Verify Defeat, Victory, Replay, Home, progress unlock and first-clear reward.
10. Verify Pass mission progress and claim through real UI.
11. Check Console after compile, bake, every scene and stress pass.

Save evidence under:

- `Assets/Screenshots/EnemyCampaign/SourceAudit/`
- `Assets/Screenshots/EnemyCampaign/VATRoster/`
- `Assets/Screenshots/EnemyCampaign/CampaignUI/`
- `Assets/Screenshots/EnemyCampaign/Stages/`
- `Assets/Screenshots/EnemyCampaign/Pass/`
- `Assets/Screenshots/EnemyCampaign/Performance/`

Required evidence includes labeled final VAT roster contact sheets, collider/agent views, each
campaign screen selection, all five planes, each boss encounter, HUGO attack/death, result/payout,
Pass progress/claim and profiler screenshots/data.

Console target: zero new relevant compile/import/VAT/NavMesh/gameplay/UI error or exception. Report
pre-existing MCP disconnect and PanelSettings theme noise separately.

## Engineering and safety rules

- Follow `AGENTS.md`: impact before symbol edits; `detect_changes` before any commit.
- Prefer existing Unity/BillGameCore/project code before new code.
- Use `Bill.Pool`, `Bill.Events`, `Bill.State`, `Bill.Scene`, `Bill.Audio`, BillTween. No DOTween.
- No networking, Addressables/YooAsset migration, chunk streaming or procedural environment now.
- No per-frame LINQ/allocations, repeated reflection, scene-wide searches or Animator instances on enemies.
- No runtime building of campaign UI, scenes, permanent prefabs, WaveData or VAT assets.
- No direct hand-edit of scene/prefab YAML when Unity APIs/editor tooling can author safely.
- Do not touch Player skeleton/Animator/RigBuilder/WeaponRig/WeaponSocket/GunMount/RecoilPivot,
  weapon grip/pose data, Pro Casual vendor content or ThirdParty source.
- Never modify/delete imported vendor assets to make Git status smaller.
- Preserve existing dirty work and unrelated scene/material changes.
- Do not stage, commit or push.

## Completion gate

Do not call the task complete until all of these are true:

- the 15 Cute monsters plus HUGO are audited by real source/visual evidence;
- all usable gameplay enemies are baked through the existing VAT pipeline;
- every final enemy prefab is MeshRenderer + VAT_Animator with zero Animator/SMR;
- roles/abilities reflect actual animations and reuse the current ZombieBase architecture;
- all enemy IDs, data, prefabs, VAT outputs and clip mappings are deterministic/editor-visible;
- five editable Plane-based stages, NavMeshes, WaveData and build settings exist;
- campaign selector has five dots, arrows, Back, number/name, lock/completion, power and weapon advice;
- GameFlow loads/restarts/unloads the selected stage safely;
- enemy introduction and difficulty rise gradually across the five stages;
- campaign completion, power gates and first-clear rewards persist and cannot duplicate;
- KayKit reward assets are used correctly, or the physical pickup deferment is explicit and no
  half-implemented reward path ships;
- Pass has a real authored mission list/progress/claim path; premium stays honest;
- old and new tests pass;
- Unity MCP visual/runtime/performance evidence exists;
- no new relevant Console errors;
- docs match actual implementation and explicitly list any individually blocked monster or deferred
  pickup/Pass-track detail.

## Documentation and final handoff

Update at minimum:

- `Docs/ACCOUNT_SWITCH_HANDOFF.md`
- `Docs/TASK_BREAKDOWN.md`
- `Docs/PRODUCT_ROADMAP.md`
- `Docs/REMAINING_FEATURES.md`
- `Docs/GAMEPLAY_DESIGN.md`
- `Docs/ECONOMY_DESIGN.md`
- `Docs/PROFILE_SAVE.md`
- `Docs/UI_ARCHITECTURE.md`
- `Docs/PREFAB_CONVENTIONS.md` only if the actual convention changes—it should not
- new `Docs/ENEMY_ROSTER_AUDIT.md`
- new `Docs/CAMPAIGN_BALANCE_TABLE.md`

Replace stale claims rather than appending contradictions. The next phase after this should be the
owner-authored theme/layout pass for trees and obstacles per stage, followed by final balance and
content polish—not another enemy architecture rewrite.

Before final reporting run GitNexus `detect_changes({scope: "compare", base_ref: "main"})`. Explain
the cumulative dirty-tree caveat, identify this task's scoped files/flows, report all bake outputs,
asset sizes, tests, runtime scenarios, profiler numbers, screenshots, Console state and honest gaps.

## Completion notification

After the task is genuinely complete and verified:

1. Use the `simplifier-vi` skill at Level 1 to reduce the final report to the outcome, verification performed, and any important remaining issue.
2. Rewrite that summary in natural English using no more than 80 words.
3. Run:

   python Tools/notify_done.py "<English summary>" --repeat 2

If the task is blocked or fails, run the same command with a short English explanation of the blocker. Report any notification-script failure in the written response.
