# PHASE: EXECUTE — M7.1 + M7.2 + M7.3 IN ONE CONTINUOUS RUN

Work in:

```text
D:\Projects\Zombie-War
```

This is the vertical slice that turns the project from "a world you walk in" into a game: weapons
onboarded, a level-up system that actually changes the run, and a world with somewhere worth going.

**Run all three parts in order, in one pass. Do not stop to ask the owner anything.** Where a decision
is needed, decide it, record the decision and the reasoning, and keep going. The owner has explicitly
delegated in-flight decisions for this run.

Follow `CLAUDE.md`, `AGENTS.md`, `Docs/FRAMEWORK.md` and the `billgamecore` skill. BillTween only, never
DOTween.

---

## 0. Standing rules for the whole run

```text
Run impact analysis before editing any symbol; report blast radius. Warn in the report on HIGH/CRITICAL
  but do not pause — make the change additive and reversible instead.
NEVER edit Menu.unity or any Assets/_Project/UI/Prefabs/Screens/UI_*.prefab. The owner owns UI layout.
  If a feature needs a UI prefab change, implement everything code-side and record the exact prefab
  change needed as an owner task. Do not do it yourself.
Never rebuild the Player skeleton/Animator/WeaponRig — read
  Docs/Reference/Technical/PlayerRigSocketIncident.md first.
Play-test from Bootstrap.unity only. Do not stage, commit or push.
After every editor operation, check git status and revert unintended UI-file changes.
Never generate or play a spoken/TTS report.
```

**The owner authors all 3D grip/muzzle placement by hand.** You never set a grip, muzzle or hand
transform by inference. This is a permanent project rule from this point on, not a limitation of this
task. Your job is to prepare everything around it and to verify what the owner authored.

## 1. Verified starting state

M7.0 delivered: `WeaponCatalog` asset (25 entries), `VendorWeaponSheetRenderer`, G1–G8 inside
`WeaponRosterMigration`, 16 tier values corrected, duplicate shotgun recorded as a variant, and the
rig-relative grip capture tool (`WeaponGripValidationCapture`).

Owner decisions since:

```text
The 25 shipped weapons are OWNER-ACCEPTED for G5 (grandfathered — poses were hand-authored by eye
  and verified in game). Their "out of reach" metric stays as a diagnostic, never a blocker.
G5 becomes an owner sign-off flag, not an automated visual judgement.
All 23 skill cards are in scope (W2). Build shared primitives, not 23 bespoke features.
```

Debt carried from M7.0 — close it in Part A: the audio builders, dev/cheat tools, `CombatPower` and
`GachaService` still folder-scan instead of reading the catalog, so "weapon 26 = one catalog entry"
is not yet true.

### Weapon packs — THREE PACKS WERE NEVER AUDITED

The M6.1 inventory (415 prefabs) was taken on 2026-08-14. The owner has imported more weapon packs
since. Measured on disk just now, these contain weapon bodies the audit has never seen:

```text
Assets/Low Poly Weapon Series V 4      334 prefabs. 10 weapon bodies:
    Prefabs/Low Poly Series V 4 New/Weapons: WWII_LMG_B, WWII_Recon_B, WWII_Rifle_A,
                                             WWII_Rifle_B, WWII_SMG_B
    Prefabs/New/Weapons:                     AR_W, AR_X, Launcher_G, Pistol_R, ShotGun_P
    Every body also ships a "_PreSet" prefab — the same model, not a second weapon.
    Prefabs/Previous + Series V1/V2/V3 hold attachments, grenades and items, not bodies.
    ALSO USEFUL: Frag_Grenade_A, Flash Grenade_A/B, Smoke Grenade_A, Stun Grenade_A,
                 Impact Grenade_A, ClayMore_A, Land Mine_A, Combat Knife_A, Shovel_A

Assets/Low Poly ShotGun Weapon Pack 2   66 prefabs. 5 bodies: ShotGun_F, G, H, I, J
Assets/Low Poly SMG Weapon Pack 2       76 prefabs. 5 bodies: SMG_F, G, H, I, J
```

Plus the 9 usable bodies the M6.1 audit already found but never onboarded: `AR_A_2`, `M4_8`, `AR_T`,
`AR_U` (AssaultRifle), `M1911`, `Pistol_P` (Sidearm), `Recon_P` (Marksman), `SMG_P` (SMG),
`ShotGun_D` (Shotgun).

That is roughly **29 candidate bodies**, which would take the arsenal from 24 distinct weapons to
around 50 — and it lands where the arsenal is starved: SMG has one body today and could reach eight,
Marksman three, LMG two.

### Other assets — measured on disk, use these exact paths

```text
Assets/Synty/PolygonKaiju        4 kaiju characters (SM_Chr_Kaiju_01..04) + 4 tails,
                                 city/rubble/rock props, FX_StompRing_01/02
                                 *** ZERO animation clips: clipAnimations: [], importAnimation: 0,
                                 0 .anim, 0 .controller. Rigged humanoid but unanimated. ***
Assets/Synty/PolygonDarkFantasy  619 prefabs — 15 characters, 9 statues (incl. gargoyle statues),
                                 SM_Prop_Altar_Table_01, SM_Prop_Brazier_01, 32 pillars,
                                 SM_Prop_Chest_01, SM_Prop_Barrel_01/Open, SM_Prop_Crate_01/02
Assets/Synty/PolygonGeneric      shared Synty base materials/shaders
Assets/ThirdParty/Epic Toon FX   1455 prefabs under Prefabs/{Combat, Environment, Interactive, Misc}
Existing enemies                 ENM_*_VAT prefabs — the enemy pipeline is VAT-baked
                                 (Assets/ThirdParty/VAT, VAT_BakerEditorWindow)
Existing bosses                  ENM_CactusBoss_VAT, ENM_MoleRatKing_VAT, ENM_SkeletonGiant_VAT
```

**Kaiju is explicitly OUT OF SCOPE as an enemy or boss in this run.** It ships with no animation and the
enemy pipeline is VAT-baked, so making it fight requires sourcing/retargeting animation and then baking
VAT — two steps with high visual risk that the owner wants to supervise. Record it as a follow-up
milestone. You may use Kaiju **props and FX** (rubble, rocks, `FX_StompRing`) freely.

Every Synty or Kaiju asset that enters the game must pass the same toon material/outline contract the
weapons do (G1/G2). Convert, do not ship raw Synty materials.

---

# PART A — M7.1 · Weapons

## A0 — Audit the three new packs first

`Low Poly Weapon Series V 4`, `Low Poly ShotGun Weapon Pack 2` and `Low Poly SMG Weapon Pack 2` are not
in the M6.1 inventory. Run the same classification over them that produced
`Review/M6_WeaponFactory/Inventory/weapon_inventory.csv`, and extend that CSV rather than starting a
new one. Every prefab lands in exactly one bucket, and the totals must reconcile.

Mesh-signature deduplication is mandatory, not optional. Three known traps:

```text
Every V4 body ships a "_PreSet" twin — same model, not a second weapon
The V4 pack's Previous/V1/V2/V3 folders may repeat models from the older packs
Vendor originals of weapons you already ship will reappear (this is how the
  BenelliM4 / Shotgun_Generic duplicate was found)
```

## A1 — Onboard every body that passes the gate

Onboard all usable bodies — the ~20 from the new packs plus the 9 already-identified leftovers.
Prioritise the starved families first (`SMG`, `Marksman`, `LMG`), then the rest.

For each: copy into project ownership, convert materials to the toon contract, generate `WeaponData`,
assign a stable unique `weaponId`, set `tier` from the measured power band, render the icon, create the
catalog entry.

**Size and poly discipline — the owner's explicit requirement.** Everything must read as low poly and
sit at the project's scale. Reference from the shipped arsenal: triangles 2133–11614 (avg 4109),
longest axis 0.193–1.465 m. Reject or flag anything outside that envelope rather than silently scaling
it to fit. Note that `Recon_P` at 11700 tris already exceeds the current maximum — record it as a G6
warning and watch the total in the WebGL budget.

**Launcher bodies** (`Launcher_G`, and the 4 from the M6.1 audit) may be onboarded as data, but they
must be marked `BLOCKED_NEEDS_PROJECTILE_FIREMODE` and kept out of the playable pool — `FireMode.Projectile`
does not exist. `M2_50cal` is a tripod emplacement and stays cut. Do not implement Projectile in this run.

## A1b — Naming, because names are forever

`weaponId` is save identity: never reused, never changed, never colliding — including against the 25
already shipped. Where a model name collides (there is already a `WD_Sidearm_M1911`, and the vendor
`M1911` is a different mesh), **invent a distinct name**. The owner has explicitly delegated naming.

Two rules constrain the invention:

```text
The name must survive later colour/shader variants of the same body — reserve a clear shape such as
  <baseName>__<variant> so a recolour never has to rename its parent.
Variants group by variantGroupId and must not inflate the family count in any UI.
```

Duplicate-mesh bodies from the packs are **registered as variants** of the weapon they duplicate, with
invented names, inside the same `variantGroupId`. The owner wants them available as the base for
colour/shader variants later. They are not separate weapons and must never appear as a second entry of
the same gun in the shop.

## A2 — Stop at grip and muzzle

**Leave every newly onboarded weapon's grip and muzzle anchors unauthored** and mark it:

```text
authoringStatus = PENDING_OWNER_AUTHORING
```

A weapon in that state must not be equippable, must not reach the Hub, shop or loadout, and must fail
validation loudly if anything tries to equip it. This is not a blocker for this run: **all testing and
play-testing uses the 25 already-authored weapons.** The owner authors the new grips in a dedicated
pass later, after the game is polished, using the existing `WeaponPoseAuthoring` play-mode workflow.

You never place a grip, muzzle or hand transform by inference — not for one weapon, not for thirty.

## A3 — Make the owner's later pass fast

Add a small editor window listing every weapon with `PENDING_OWNER_AUTHORING`, with a one-click "equip
this one on the player" action so the owner can walk the queue without hunting through assets. Reuse
`WeaponPoseAuthoring` / `WeaponPoseAuthoringEditor` — do not build a second authoring system. With ~29
weapons queued this window is now worth real care: show family, tier, and which anchors are still
missing, and let the owner move to the next weapon without leaving play mode.

## A4 — Own the materials (G1b)

All 25 shipped weapons currently reference `.mat` files living inside vendor pack folders, so a pack
reimport could restyle the whole arsenal. Copy those materials into project-owned folders and repoint
the prefabs. Then G1b passes for the arsenal, and newly onboarded weapons are project-owned from birth.

## A5 — Close the catalog debt

Make the audio builders, dev/cheat tools, `CombatPower` and `GachaService` read `WeaponCatalog` instead
of scanning folders. The audio requirement is the important one: **a weapon without an audio key must
fail loudly at build time**, not silently at runtime. Prove "weapon 26 = one catalog entry" with a test.

## A6 — Housekeeping

Revert `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset`, which a
play-test dirtied during M7.0. Confirm it is clean in `git status`.

---

# PART B — M7.2 · Skills, cards and the level-up that actually does something

Today the level-up screen is presentation only and applies no choice. This part makes the run build.

## B1 — Build the 9 shared primitives first

From `Review/M6_DecisionLock/skill_shared_primitives.csv`: per-enemy status carrier, autonomous power
framework, multi-target spatial query, distance accumulator, ramp/charge accumulator with decay,
damage-path interception hook, distance-scaled damage curve, stat soft-cap curve, power VFX/HUD kit.

Build these as real, tested primitives before any card. Twenty of the twenty-three cards are
compositions of them. If you find yourself writing bespoke logic for a card, the primitive is wrong.

Reuse what genuinely exists: `WeaponData.RangeFalloff`, `knockback` → `ApplyPhysicalPush`, the perked
fire-rate multiplier, `PiercingLine`, `Bomb.Explode()`. Chain lightning and densest-cluster targeting do
**not** exist — `FireMode.ChainLightning` is an enum value with no code behind it. Build them as P3.

## B2 — Implement the 23 cards

All 23 are in scope (owner decision W2). Build in this order and do not stop early: **MUST 15 →
SHOULD 7 → LATER 1**. Full schema in `Review/M6_WeaponFactory/SKILL_CATALOG.md` and `skill_catalog.csv`
(23 rows × 64 fields). Do not add, remove or rename a card. Rank 1 unlocks the behaviour; ranks 2–3
strengthen the same fantasy rather than becoming a different skill.

## B3 — The offer system

```text
Slot A prefers a signature card compatible with the equipped weapon family
Slot B autonomous or universal
Slot C any valid card
At most ONE pure-stat card per offer
Never offer an incompatible card, a max-rank card, or a card whose prerequisites are unmet
Deterministic from the run seed — the same seed produces the same offers
The ≤30 s unscaled pause auto-picks a VALID card on timeout, never a broken one
Explicit behaviour when the pool is exhausted
```

Wire the choice into the existing level-up overlay **code-side only**. Do not edit UI prefabs. If the
card visuals need a prefab change, implement the logic, drive what you can from code, and write the
exact required prefab change into the owner task list.

## B4 — Feedback within budget

Use Epic Toon FX for card pick, power procs and charge/cooldown readouts.

`Ordnance Core` and `Emergency Detonation` finally have art: the V4 pack ships `Frag_Grenade_A`,
`Impact Grenade_A`, `ClayMore_A` and `Land Mine_A`. Reuse them for the autonomous explosive powers and
for the retired Bomb pickup's art, exactly as the M6 design intended — the manual grenade button stays
retired, the content returns as automatic powers.

Everything pooled. Hold the
guardrails: fire rate soft cap 2.2× / hard cap 2.5×; ≤6 lightning arcs per proc and ≤2 procs/s across
all sources; ≤2 concurrent explosions; ≤1 `OverlapSphere` per power proc; clustering ≤1/s over ≤64
enemies; zero runtime material instances; 0 bytes/frame steady-state allocation.

## B5 — Prove it

Tests: offer determinism from a seed, no all-stat offer, no incompatible or max-rank card, timeout
auto-pick always valid, soft caps enforced. Then play from `Bootstrap.unity` and confirm two runs with
the same weapon produce visibly different builds.

---

# PART C — M7.3 · Stations, pickups and endless pressure

This is the part that answers "why would I walk over there".

## C1 — The World Signal Language

Build the prop-independent visual contract first: ground ring, vertical beam, floating icon, emissive
accent, progress indicator, one distinct colour per station type. Assemble it from Epic Toon FX. Any
prop the owner supplies later gets dressed by this language — the prop carries the mass, the language
carries the meaning.

## C2 — Station bodies — choose and record

Pick the final bodies yourself from what is on disk, and produce a review sheet so the owner can
override later:

```text
Signal Relay    a Dark Fantasy pillar/obelisk or statue — needs vertical mass, readable from distance
Supply Cache    existing container/crate, or SM_Prop_Chest_01
Boss Beacon     a gargoyle statue or SM_Prop_Altar_Table_01 — must read as ominous, not lootable
Breakable       SM_Prop_Barrel_01 / SM_Prop_Crate_01
```

A statue alone does not say "hold this zone to charge it". The signal language does that work — check
that the two read as different things at gameplay camera distance.

## C3 — The three stations

Implement Signal Relay, Supply Cache and Boss Beacon with the full contract each: deterministic
anchor-based spawn, ownership, HUD signal, interaction and duration, cancel rule, risk/reward,
cooldown/repeatability, persistence, enemy-pressure response, and failure handling.

```text
Anchors are deterministic and global — never chunk-local random
Recycling a visual chunk must not reset an objective, duplicate loot or respawn a destroyed station
No orphan boss when its chunk unloads
No free chest
At most one major encounter owns the player's attention at a time
```

**Boss Beacon uses the existing VAT bosses only** (`ENM_CactusBoss_VAT`, `ENM_MoleRatKing_VAT`,
`ENM_SkeletonGiant_VAT`). No new boss is authored in this run.

## C4 — Endless pressure

Remove the wave-clear auto-collect (`PickupManager.cs:141` collects on `WaveClearedEvent`) — an endless
world has no reliable wave boundary, and auto-collecting removes the reason to move toward loot.

Implement the Threat model as composition change before stat inflation:
`ThreatTier = base + objectiveProgress + distanceBand + boundedTimePressure`. Tier 0 walkers/runners;
tier 1 adds one specialist; tier 2 mixed specialists and elite chance; tier 3 recovery-window enemies
and the boss route. Eleven of the sixteen enemy assets are unused in production — use composition, not
HP multiplication.

---

## 2. Decision authority

**Decide these yourself, record the choice and why — do not ask:**

```text
Station body prefabs, colours and FX selection
Signal language proportions, timings and readability tuning
Primitive API shape, file layout, namespaces, class names
Card implementation order inside each MUST/SHOULD/LATER band
Anchor spacing, station frequency, threat curve starting numbers (all TUNING)
Which existing boss serves the first Boss Beacon
Poly/material conversion choices for Synty props
```

**Never decide these — record them as owner tasks instead:**

```text
Anything requiring a UI prefab or scene edit
Grip, muzzle or hand transform placement
Whether a hand-authored weapon pose looks right
Reactivating gacha, star upgrades or any dormant economy system
Changing an owner-locked rule (one weapon per run, auto-fire, no reload, banking rates,
  the 23 card names, the six families)
```

## 3. Order and honesty

Run A → B → C. If you run out of capacity, finish the part you are in, leave the project **building and
playable**, and say exactly where you stopped and what remains. Do not half-land all three parts. Do not
present a partially implemented card or station as done. Running out of capacity is truthful incomplete
execution, not a blocker.

## 4. Acceptance gates

**Part A**
1. The three new packs are audited into the existing inventory CSV; every prefab in one bucket; totals
   reconcile; `_PreSet` twins and cross-pack duplicates caught by mesh signature, not by filename.
2. Every usable body onboarded — materials converted, `WeaponData` + catalog entries created — with
   starved families (SMG, Marksman, LMG) done first. Report the final distinct-weapon count per family.
3. Every `weaponId` unique against the shipped 25 and against each other; invented names recorded with
   their reasoning; duplicate-mesh bodies registered as variants inside a `variantGroupId`, never as a
   second copy of the same gun.
4. Size/poly envelope enforced: anything outside 2133–11614 tris or 0.193–1.465 m longest axis is
   flagged, not silently rescaled.
5. Every new weapon marked `PENDING_OWNER_AUTHORING`, unequippable and unreachable from Hub/shop/loadout;
   launcher bodies additionally marked `BLOCKED_NEEDS_PROJECTILE_FIREMODE`; no grip, muzzle or hand
   transform placed by inference anywhere.
6. Pending-authoring queue window exists, reuses `WeaponPoseAuthoring`, and shows family, tier and which
   anchors are missing.
7. All weapon materials project-owned; G1b passes.
8. Audio builders, dev/cheat tools, `CombatPower`, `GachaService` read the catalog; a weapon missing an
   audio key fails the build; "weapon 26 = one entry" proven by test.
9. TMP fallback asset clean in `git status`.

**Part B**
10. The 9 primitives exist, are tested, and the cards are compositions of them.
11. All 23 cards implemented, none added/removed/renamed, ranks 1–3 behave as specified.
12. Offer rules hold: max one stat card, no incompatible/max-rank, deterministic from seed, timeout
    auto-pick always valid, exhausted pool handled.
13. Level-up choice applies in game, wired without editing any UI prefab.
14. Performance guardrails hold; 0 alloc/frame steady state.

**Part C**
15. Signal language exists independently of any prop and reads at gameplay camera distance.
16. Three stations implemented with full contracts and deterministic anchors.
17. Chunk recycling cannot reset, duplicate or orphan station state; no free chest; no orphan boss.
18. `CollectAll()` on `WaveClearedEvent` removed.
19. Threat model changes composition before stats.

**Whole run**
20. Existing tests stay green; new behaviour has new tests.
21. Bootstrap play-test on the already-authored 25: pick a weapon, level up and take cards, reach a
    station, trigger a boss beacon.
22. `git status` shows zero modified UI prefabs and zero modified scenes; nothing staged.
23. `detect_changes()` run and reported.

## 5. Final report format

```text
PHASE: EXECUTE — M7.1/M7.2/M7.3 VERTICAL SLICE — COMPLETE / PARTIAL (say exactly where you stopped)

PART A — new packs audited (bucket totals), bodies onboarded per family, invented names + variant
         groups, size/poly flags, pending-authoring queue, materials owned, catalog debt closed
         Final table: distinct weapons per family, before and after
PART B — primitives built, 23 cards implemented, offer rules, level-up wiring, performance
PART C — signal language, station bodies chosen (with reasons), three stations, threat model,
         auto-collect removed
Decisions I made under delegated authority, and why
Owner tasks recorded (UI prefab changes, grip authoring queue, anything I was not allowed to decide)
Impact analysis: symbols, blast radius, HIGH/CRITICAL items and how each was made safe
Tests: existing green, new tests added
Bootstrap play-test result
detect_changes() output
Protected state: UI prefabs, scenes, staged files — git evidence
Files modified — full list
Open risks
M7.1 / M7.2 / M7.3 STATUS: each DELIVERED / PARTIAL / NOT STARTED
BLOCKERS: none / exact blocker
```

Report what is genuinely working, not what was attempted. If a card, station or primitive is stubbed,
say the word "stubbed" rather than listing it as delivered.

Completion audio policy: do not generate or play a spoken/TTS report. The owner wants only the
project's short completion notification behavior.

## Completion notification

After the task is genuinely complete and verified:

1. Use the `simplifier-vi` skill at Level 1 to reduce the final report to the outcome, verification performed, and any important remaining issue.
2. Rewrite that summary in natural English using no more than 80 words.
3. Run:

   python Tools\notify_done.py "<English summary>" --repeat 2

If the task is blocked or fails, run the same command with a short English explanation of the blocker. Report any notification-script failure in the written response.
