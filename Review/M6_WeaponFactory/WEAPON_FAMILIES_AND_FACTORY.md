# M6.1 Steps 3–5 — Family grammar, weapon schema, and the Weapon Factory

**Status:** PROPOSAL awaiting owner approval. No runtime, asset or `WeaponData` change was made.
Asset counts are `MEASURED` (see `Inventory/weapon_inventory_summary.md`).
> **M6.2 DECISION LOCK (2026-08-15).** The owner has answered `W1`–`W7`. Where this M6.1 document
> says a decision is *recommended*, *proposed* or *awaiting approval*, the answer is now recorded in
> `Docs/M6_ENDLESS_RUN_SYSTEM_DESIGN.md` §1 and its deltas §1c–§1e. Measured numbers below are
> unchanged and remain valid; only decision status moved.
>
> Specifically: the **family model is OWNER-LOCKED (W1)**, and the MW4-specific onboarding gate proposed
> here is **retired**, replaced by the universal **Weapon Visual Onboarding Gate G1–G8** in
> `../M6_DecisionLock/WEAPON_ONBOARDING_GATE.md` (W3).


---

# Step 3 — Weapon-family grammar

Six **active** families. A family exists only if it answers a different combat question *and* has
asset and runtime evidence. Mechanically redundant models become **variants**, never new families.

## 3.1 Active families

### Sidearm — "movement is my damage stat"

```text
familyId            fam.sidearm
fantasy             A light, fast weapon that rewards never standing still.
gameplayIdentity    Converts mobility into offence.
primaryMechanic     Movement-conditional fire rate and movement-charged empowered shots.
strength            Highest uptime while kiting; strongest early survivability.
weakness            Lowest raw single-target DPS; punished when cornered.
positioningDemand   Constant motion, wide circles.
targetPreference    NearestEnemy.
signatureDecision   "Do I keep running, or stop to be accurate?"
fireModes           SingleHitscan                              (IMPLEMENTED)
statEnvelope        damage low · fireRate high · range 7-8 · pellets 1 · pierce 0
allowedTags         Move, Rapid, Crit
forbiddenTags       Heavy, Channel
twoSignatureSkills  Run & Gun · Quickstep Round
buildPaths          (a) mobility-DPS: Run&Gun + MoveSpeed + Chain Lightning
                    (b) burst-executioner: Quickstep + Execution Round + Ordnance Core
visualRule          Small silhouette; needs colour to read (most sidearms are dark grey).
audioRhythm         Fast, light, dry cracks.
performanceRisk     LOW.
assetCoverage       12 bodies (11 toon-ready) — the deepest family in the project.
```

### SMG — "the longer I hold, the more the screen lights up"

```text
familyId            fam.smg
fantasy             Sustained spray that builds into an electrical discharge.
gameplayIdentity    Trades precision for accumulation speed.
primaryMechanic     Hit-count accumulation (fastest trigger source in the game).
strength            Highest hits/second, so it charges every on-hit effect fastest.
weakness            Short range (5.8 m), weak per shot, poor against a lone durable target.
positioningDemand   Mid-close, facing the densest group.
targetPreference    NearestEnemy / DensestCluster.
signatureDecision   "Do I keep the beam on one target, or sweep?"
fireModes           SingleHitscan                              (IMPLEMENTED)
statEnvelope        damage very low · fireRate very high · range 5-6 · pellets 1
allowedTags         Rapid, Shock, Move
forbiddenTags       Precision, Heavy
twoSignatureSkills  Static Build-up · Bullet Hose
buildPaths          (a) shock: Static Build-up + Chain Lightning + FireRate
                    (b) hose: Bullet Hose + Damage + Soul Burst
visualRule          Compact; the Uzi profile reads well at scale.
audioRhythm         Continuous, high-rate; NEEDS a voice cap.
performanceRisk     MEDIUM — highest shot count drives audio and tracer budgets.
assetCoverage       2 bodies, only 1 toon-ready. WEAKEST coverage; SMG_P needs conversion.
```

### Assault Rifle — "steady line pressure"

```text
familyId            fam.assaultrifle
fantasy             A disciplined all-rounder that rewards holding a target.
gameplayIdentity    Ramping single-target damage with periodic penetration.
primaryMechanic     Same-target ramp; periodic piercing shot.
strength            Best all-round envelope; good at every range band.
weakness            Deliberately best at nothing; loses ramp on target switch.
positioningDemand   Medium; stable lines of fire.
targetPreference    CurrentTarget.
signatureDecision   "Do I hold this target through the ramp, or retarget?"
fireModes           SingleHitscan (IMPLEMENTED) · PiercingLine for Breach (IMPLEMENTED)
statEnvelope        damage medium · fireRate medium-high · range 9 · pierce 0-2
allowedTags         Precision, Pierce, Rapid
forbiddenTags       Close
twoSignatureSkills  Focus Fire · Breach Round
buildPaths          (a) focus: Focus Fire + Execution Round + Damage
                    (b) breach: Breach Round + Longshot-style range play + Ordnance Core
visualRule          FAMAS is the only shape-distinct AR; the rest need colour.
audioRhythm         Even, mechanical.
performanceRisk     LOW.
assetCoverage       10 bodies (6 integrated, 2 toon-ready spare, 2 need conversion).
                    NOTE: G36C at 240 DPS is currently the highest-DPS weapon in the game.
```

### Shotgun — "I take ground"

```text
familyId            fam.shotgun
fantasy             Get close, blow a hole in the crowd, walk into it.
gameplayIdentity    Burst plus crowd displacement.
primaryMechanic     Multi-pellet burst; knockback and slow.
strength            Highest burst; 8 pellets trigger on-hit effects 8x per shot.
weakness            Range 5-5.5 m; helpless against r9-r12 ranged archetypes.
positioningDemand   Deliberately INSIDE the horde.
targetPreference    EnemiesInCone.
signatureDecision   "Do I use this blast for damage, or for space?"
fireModes           MultiPelletHitscan                         (IMPLEMENTED)
statEnvelope        damage per pellet low · pellets 8 · fireRate low · range 5-5.5
allowedTags         Close, Blast, Control
forbiddenTags       Precision, Pierce
twoSignatureSkills  Point Blank · Concussion
buildPaths          (a) breacher: Point Blank + Max Health + Soul Burst
                    (b) controller: Concussion + Ordnance Core + Move Speed
visualRule          AA12 and DoubleBarrel are shape-distinct; the pumps are not.
audioRhythm         Slow, heavy, punctuated.
performanceRisk     MEDIUM — mass knockback must not destabilise crowd steering.
assetCoverage       6 bodies, all toon-ready. Healthy.
```

### Marksman — "one shot, the right target"

```text
familyId            fam.marksman
fantasy             Pick the target that matters and delete it.
gameplayIdentity    Range-scaled damage and target priority.
primaryMechanic     Piercing line; distance scaling; priority targeting.
strength            Only family that reliably chooses ITS target; best boss DPS per shot.
weakness            81 DPS baseline; swarms that close the gap win.
positioningDemand   Hold distance; retreat lanes matter.
targetPreference    PriorityEnemy / EnemiesInLine.
signatureDecision   "Is holding this distance worth the swarm closing?"
fireModes           PiercingLine                               (IMPLEMENTED, Weapon.cs:618)
statEnvelope        damage very high · fireRate very low · range 16 · pierce 3
allowedTags         Precision, Pierce
forbiddenTags       Close, Rapid
twoSignatureSkills  Longshot · Hunter's Mark
buildPaths          (a) line: Longshot + Breach-style pierce stacking + Damage
                    (b) hunter: Hunter's Mark + Execution Round + Kinetic Shield
visualRule          Long barrel reads well; Recon_P is the best asset but needs conversion.
audioRhythm         Sparse, heavy, with space between shots.
performanceRisk     LOW.
assetCoverage       2 bodies, 1 toon-ready. WEAK coverage.
```

### LMG — "I hold this ground"

```text
familyId            fam.lmg
fantasy             Plant yourself and turn a lane into a wall of fire.
gameplayIdentity    Sustained pressure bought with mobility.
primaryMechanic     Charge that raises damage while lowering movement speed.
strength            Highest sustained output; best against dense committed pushes.
weakness            Mobility cost; vulnerable to flanks and burrowers.
positioningDemand   Commit to a position, then pay to leave it.
targetPreference    EnemiesInCone.
signatureDecision   "Is this position worth being slow in?"
fireModes           SingleHitscan                              (IMPLEMENTED)
statEnvelope        damage low-medium · fireRate high · range 9.5 · pellets 1
allowedTags         Heavy, Control, Blast
forbiddenTags       Move
twoSignatureSkills  Heavy Pressure · Shockwave Belt
buildPaths          (a) anchor: Heavy Pressure + Max Health + Chain Lightning
                    (b) suppressor: Shockwave Belt + Concussion-style control + Damage
visualRule          M249 (bipod + green ammo box) is the STRONGEST silhouette in the arsenal.
audioRhythm         Heavy continuous roar.
performanceRisk     MEDIUM — high shot count plus cone queries.
assetCoverage       1 body. SINGLE-BODY FAMILY — any promise of LMG variety is unsupported.
```

> ⚠ **Design tension the owner must resolve:** the LMG identity rewards *holding ground*, which sits
> against the "move with purpose" pillar. Either the pillar admits "commit to a position" as one valid
> answer, or this family should be cut. Flagged, not decided.

## 3.2 Later families

| Family | Assets | Why LATER |
|---|---|---|
| Launcher (grenade / RPG) | 4 bodies | `FireMode.Projectile` has no implemented runtime path; needs projectile flight, arming, AoE and safety against self-damage |
| Mounted (`M2_50cal`) | 1 body | Tripod-mounted emplacement; incompatible with a one-weapon mobile run |
| Tesla / Laser / Flamethrower / Railgun | **no complete bodies exist** | `FireMode.ContinuousBeam` and `ChainLightning` are unimplemented enum values, and the only "laser/light" prefabs are attachment modules, not weapons. **Do not plan these families.** |

---

# Step 4 — Weapon schema and balance

> **Moved.** The schema field-marking and the balance model now live in their own deliverable:
>
> ## → [`WEAPON_BALANCE_MODEL.md`](WEAPON_BALANCE_MODEL.md)
>
> It covers: every `WeaponData` field marked `EXISTS_AND_USED` / `EXISTS_BUT_DEAD` /
> `EXISTS_BUT_PARTIAL` / `DERIVED` / `NEEDS_NEW_FIELD` / `EDITOR_ONLY`; the power model and its
> coefficients (all `TUNING`); family envelopes and their measured bands; tier philosophy and
> side-grade rules; fire-rate and move-speed soft caps; boss-versus-crowd DPS separation; and the
> immediately-playable-unlock rule.
>
> Machine-readable form: `weapon_balance_model.csv` (24 rows).
>
> **This section is deliberately a pointer, not a copy** — the two files must never be able to disagree.
> Headline numbers, restated once for convenience and matching the CSV exactly:
> AssaultRifle band **0.465–0.745**, Sidearm band **0.205–0.328**, and reload/magazine/ammo/overheat are
> **retired from active design**.

---

# Step 5 — Weapon Factory architecture

```text
vendor prefab
  → 1 classify           → 2 copy project-owned  → 3 material conversion
  → 4 grip/muzzle        → 5 family pose template → 6 WeaponData generation
  → 7 stable ID/variant  → 8 icon + contact sheet → 9 catalog registration
  → 10 validation
```

| # | Step | Mode | Detail |
|---|---|---|---|
| 1 | Classify | **AUTOMATIC** | Mesh signature (tris + bounds) + child-name analysis reproduces the 10-bucket split used in this audit. Duplicate detection is free and would have caught the BenelliM4/Generic collision. |
| 2 | Copy project-owned | **AUTOMATIC** | Copy under `Assets/_Project/Prefabs/Weapons`; never edit vendor files. |
| 3 | Material conversion | **HEURISTIC** | Map URP Lit → project toon material by base-colour sampling. 263 MW4 prefabs need this. Ambiguous multi-material bodies go to the manual queue. |
| 4 | Grip / muzzle discovery | **HEURISTIC** | `*_Grip` child → right-hand anchor; far end of `*_Barrel` along +Z → muzzle. Vendor naming is consistent enough to try, and **never trustworthy enough to skip validation**. |
| 5 | Family pose template | **AUTOMATIC given family** | One authored pose per family; the weapon inherits it and stores only a delta. This is what makes onboarding cheap. |
| 6 | `WeaponData` generation | **AUTOMATIC** | Family template + measured bounds + balance band → a filled asset. |
| 7 | Stable ID / variant group | **AUTOMATIC** | `weaponId` from a stable slug; `variantGroupId` from the mesh signature. |
| 8 | Icon + contact sheet | **AUTOMATIC** | The renderer built for this audit is directly reusable. |
| 9 | Catalog registration | **AUTOMATIC** | Append to one authoritative `WeaponCatalog`. |
| 10 | Validation | **AUTOMATIC gate + MANUAL review** | Fail-loud list below, then a human looks at the held sheet. |

**Realistic split: ~85 % of steps are automatic or heuristic; grip validation and material ambiguity are
the irreducible manual cost.** Perfect grip/muzzle inference is **not** promised — the naming heuristic
will be wrong on any body that does not follow the vendor convention, and `M1911`/`M4_8` already show
different child naming than the `AR_*`/`ShotGun_*` families.

## 5.1 Fail-loud conditions

The pipeline must **refuse to produce an asset** on any of:

```text
duplicate weaponId · duplicate mesh signature without a variantGroupId
missing prefab · zero renderers · zero materials · missing mesh
bounds longest-axis outside 0.15-2.0 m · non-uniform or zero scale
missing right-hand grip · missing muzzle · muzzle not forward of the grip
muzzle point inside the player capsule or inside the weapon mesh
handedness disagrees with bounds (one-handed body longer than 0.5 m)
visible hand penetration in the held capture
runtime material instance created at equip time
missing catalog entry · missing thumbnail · missing UI entry
weaponId collides with a saved-profile identity
```

## 5.2 Manual-fix and tooling queue

Anything that fails validation is queued with its evidence capture rather than silently dropped:
`assetPath · failedRule · capture · suggested fix · owner`. A weapon leaves the queue only when a
human has looked at its held capture.

### Open tooling item — carried in from the M6.1 audit

| Item | Why | Status |
|---|---|---|
| **Fixed rig-relative grip-validation camera** | The M6.1 holding sheet solved its camera from the player's renderer bounds and ended up above the character's backpack, with **no hand visible in any frame**. Visual grip validation is therefore `NOT RUN` (see `VISION_REVIEW.md`). Hand penetration, grip alignment and muzzle placement cannot be checked without it. | **QUEUED — required before any weapon can be signed off visually.** Delivered in **M7.0**. |

## 5.3 Real-player holding contact sheets

Every onboarded weapon must produce a held capture on the real player with the real IK, from a
**fixed rig-relative camera** (the audit's bounds-derived camera proved unreliable — see
`VISION_REVIEW.md`). The sheet is the human gate before a weapon is considered done.
