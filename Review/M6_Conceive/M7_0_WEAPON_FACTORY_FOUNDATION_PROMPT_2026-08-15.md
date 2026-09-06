# PHASE: EXECUTE — M7.0 WEAPON FACTORY FOUNDATION

Work in:

```text
D:\Projects\Zombie-War
```

M6 is **LOCKED**. This is the first implementation milestone of M7 and the first code-touching work in
this phase. Everything before it was design. Build the foundation the Factory needs; do **not** onboard
new weapons yet — that is M7.1.

Follow `CLAUDE.md`, `AGENTS.md`, `Docs/FRAMEWORK.md` and the `billgamecore` skill. BillTween only, never
DOTween.

---

## 1. Verified starting state

Read first, in this order:

```text
Docs/M6_ENDLESS_RUN_SYSTEM_DESIGN.md  §1 (W1–W7 locked), §1c, §1d, §1e, §10, §32
Review/M6_DecisionLock/WEAPON_ONBOARDING_GATE.md      G1–G8 gate definitions
Review/M6_DecisionLock/weapon_tier_ladder.csv         24 rows, measured band vs authored tier
Review/M6_WeaponFactory/WEAPON_FAMILIES_AND_FACTORY.md  Step 5 pipeline + manual-fix queue
Review/M6_WeaponFactory/LOOP_ECONOMY_AND_MIGRATION.md   "Inventory of every place the roster is enumerated"
Review/M6_WeaponFactory/VISION_REVIEW.md               why visual grip validation is NOT RUN
```

Accepted measured facts — do not recompute, do not contradict:

```text
415 prefabs · 343 attachments · 33 usable bodies · 24 mechanically distinct weapons · 25 WeaponData assets
WPN_Shotgun_BenelliM4 and WPN_Shotgun_Generic are the same mesh (2133 tris, 0.082×0.302×1.465)
Measured tier bands: Common ≤0.340 (13) · Uncommon ≤0.490 (5) · Rare ≤0.650 (4) · Epic >0.650 (2)
16 of 24 authored WeaponData.tier values disagree with the measured band
25/25 prefabs carry WeaponGripPoints (R/L/muzzle); 25/25 WeaponData set useAuthoredGripPositions = true
Visual grip validation (G5) has NEVER been run on any weapon, including the shipped 25
```

### Two things already exist — reuse them, do not write parallel systems

1. **`Assets/_Project/Scripts/Editor/WeaponCatalog.cs`** is already taken: a static class
   `ZombieWar.EditorTools.WeaponCatalog` that renders contact sheets from four vendor pack folders.
   **There must not be two `WeaponCatalog` types.** Resolve the collision deliberately: either rename the
   existing sheet renderer to something honest about what it does, or give the new authoritative asset a
   different type name. Run impact analysis first, use the `rename` tool if you rename — never
   find-and-replace — and state your choice and reasoning at the top of your report.
2. **`Assets/_Project/Scripts/Editor/Weapons/WeaponRosterMigration.cs`** (≈41 KB) already implements
   `Audit()`, `Execute()`, `Validate()` and `GenerateReferencedContactSheet()`. The Factory pipeline
   **extends this tool**. Do not create a second migration/validation tool beside it. If its structure
   genuinely cannot carry the new work, say why before writing anything new.

### Gate 0 — impact analysis before any symbol edit

Before modifying any function, class or method, run impact analysis and report blast radius (direct
callers, affected processes, risk level). **Warn me before proceeding on anything HIGH or CRITICAL.**
At minimum analyse: `WeaponData`, `Weapon`, `WeaponGripPoints`, `WeaponIKController`, `LoadoutState`,
`PlayerProfile`, `CombatPower`, `GachaService`, `WeaponRosterMigration`, `WeaponCatalog`,
`SceneFlowBuilder`, `LoadoutMenuInstaller`, `DevProfileTools`, `ZombieWarCheatPanel`, and both audio
builders (`ZombieWarCuratedAudioBuilder`, `ZombieWarAddressableAudioCatalogBuilder`).

---

## 2. Task 1 — the rig-relative grip validation camera (do this first)

G5 is declared **blocking**, and the tool to run it does not exist. Until it does, no weapon can be
signed off visually — including the 25 already shipping. This unblocks everything else.

Build an editor tool that captures a weapon held by the real player with a camera solved **from the
player rig**, not from renderer bounds. The M6.1 attempt solved distance from the character's own bounds
(1.38 × 1.27 × 2.30 m) and ended up above the backpack with no hand visible in any frame. Do not repeat
that.

Requirements:

- Camera framing is derived from the **hand/grip transforms**, not from the character's bounds.
- **The grip hand must be visibly in frame** in every capture. That is the acceptance condition of the
  tool itself. If a capture does not show the hand, the tool has failed, not the weapon.
- Capture uses the real equip path, on the real player, in gameplay — same approach that worked in M6.1.
  Two-handed weapons equip to a different slot than one-handed; handle that explicitly, because that bug
  already produced four byte-identical frames once.
- Both one-handed and two-handed cases, and a view that makes hand penetration visible.
- Output a per-weapon PNG plus a contact sheet under `Review/M7_0_Factory/Evidence/`.

Run it over **all 25 shipped weapons**. Open and vision-review the results yourself. Report honestly:
how many pass G5, how many show hand penetration, misalignment or a muzzle inside the model. **A failure
list is a successful outcome for this task** — the shipped arsenal has never been checked, so finding
defects is expected and useful. Do not fix weapon poses in this milestone; record them in the manual-fix
queue for M7.1.

## 3. Task 2 — one authoritative catalog

Create the single source of truth described in `LOOP_ECONOMY_AND_MIGRATION.md`:

```text
entries[] { weaponId, family, variantGroupId, baseWeaponId, data, catalogOrder, unlockMethod, tier }
```

Rules that must hold:

- **`weaponId` is save identity.** It never changes and is never reused. An existing profile that owns
  weapons must resolve identically before and after this change — prove it with a test.
- `catalogOrder` is presentation only and may be reordered without affecting ownership or starter seeding.
  `PlayerProfile` currently seeds the starter weapon by `CatalogOrder`; make sure reordering cannot change
  which weapon an existing player owns or starts with.
- Adding weapon 26 must require **one catalog entry and nothing else**.
- Variants group by `variantGroupId` and do not inflate the family count.

Migrate consumers to read the catalog instead of scanning the folder, in this priority order:

1. `PlayerProfile` save resolution and starter seeding — **HIGH, save identity, most dangerous**.
2. Both audio builders — **HIGH**: a new weapon without an audio key must fail loudly at build time
   rather than silently at runtime.
3. `DevProfileTools` (including the hard-coded "Unlock all 25 weapons" label), `ZombieWarCheatPanel`,
   `CombatPowerAuditWindow`, `CombatPower`.
4. `GachaService` — dormant, but new weapons must not silently join a dormant pool.

**Scope boundary.** `SceneFlowBuilder` and `LoadoutMenuInstaller` write serialized arrays into scenes and
prefabs, and Shop/Armory list building touches UI. **Do not modify any scene, any `UI_*.prefab`, or
`Menu.unity`.** If a consumer cannot be migrated without touching those, leave it, and list it as
deferred work with the reason. That is an accepted outcome, not a failure.

## 4. Task 3 — correct the 16 wrong tier values

`weapon_tier_ladder.csv` records, per weapon, the measured band, the authored tier and whether they agree.
Sixteen disagree. Today the shop shows `Python357` and `DesertEagle` as Rare while they are among the
weakest weapons in the game, and `LMG_Generic` — the fourth strongest — as Common. The rarity colour is
lying to the player.

Align authored `tier` to the measured band. Before editing: run impact analysis on `WeaponData.tier` and
report every consumer, because tier currently drives shop colour and price. State explicitly whether
changing tier changes any **price** the player already paid or any owned-weapon state; if it does, say so
and stop for my decision before proceeding.

Produce a before/after table of all 24 in the report.

## 5. Task 4 — the duplicate shotgun

`WPN_Shotgun_BenelliM4` and `WPN_Shotgun_Generic` are the same mesh. Per W1, one becomes a
colour/variant entry of the other inside the catalog. **Neither `weaponId` may be deleted or reused** —
a player may already own either one. Choose the base, record the variant relationship, and prove that an
existing profile owning the demoted one still loads correctly.

## 6. Task 5 — wire G1–G8 into validation

Implement the gate checks from `WEAPON_ONBOARDING_GATE.md` inside the existing validation path:

```text
G1 material contract  · AUTOMATIC · blocking
G2 outline contract   · AUTOMATIC · blocking
G3 mesh-signature duplicate detection · AUTOMATIC · blocking
G4 grip authoring present · AUTOMATIC · blocking
G5 grip looks correct · MANUAL · blocking   (fed by the Task 1 tool)
G6 triangle budget · AUTOMATIC · warning
G7 silhouette · HEURISTIC · warning
G8 colour distinctness · HEURISTIC · warning
```

Run the automatic gates over all 25 shipped weapons and report the results as a table. Again: failures
are information, not a reason to stop. Do not mass-edit weapons to force a pass.

## 7. Task 6 — documentation closeout

- Patch the stale banner at `Docs/GAME_DESIGN.md:320–321`, which still says the Gem, Relic and dormancy
  lines are "recommendations awaiting owner approval" while the section below it is already
  `OWNER-LOCKED (W6)`.
- Update `Docs/MVP_SHIP_PLAN.md` and `Docs/M6_ENDLESS_RUN_SYSTEM_DESIGN.md` §32 to mark **M7.0 delivered**
  with its real outcome, including anything deferred.
- Record the G5 results and the manual-fix queue under `Review/M7_0_Factory/`.
- Run `detect_changes()` before closing out and report the affected symbols and execution flows.

## 8. Prohibited

```text
Never modify Menu.unity or any Assets/_Project/UI/Prefabs/Screens/UI_*.prefab — the owner owns UI layout.
Never open or save the Menu scene as a side effect. After any editor operation, check git status and
  revert unintended UI-file changes.
No DOTween. No rebuilding the Player skeleton, Animator or WeaponRig — read
  Docs/Reference/Technical/PlayerRigSocketIncident.md before touching that area.
No new weapon onboarding — that is M7.1.
No weapon pose or grip fixing in this pass — record defects instead.
No find-and-replace renaming; use the rename tool that understands the call graph.
No changes to EconomyConfig, no gacha reactivation, no per-weapon star/level upgrade system.
No changes to any verified count or to the measured power-budget numbers.
No staging, committing or pushing. Play-test from Bootstrap.unity only.
No spoken or TTS report.
```

## 9. Acceptance gates

1. Impact analysis run and reported before every symbol edit; HIGH/CRITICAL surfaced to the owner.
2. The `WeaponCatalog` naming collision is resolved; exactly one type owns the name; the rename (if any)
   went through the call-graph-aware tool.
3. `WeaponRosterMigration` was extended, not duplicated — or the reason it could not be is stated.
4. The grip camera tool exists and **the grip hand is visible in every capture**.
5. All 25 weapons captured, vision-reviewed by you, with an honest G5 pass/fail list.
6. The authoritative catalog exists and the listed consumers read from it, except those deferred for the
   UI/scene boundary, which are named with reasons.
7. Save identity proven stable: a profile owning weapons — including the demoted duplicate shotgun —
   resolves identically before and after. Backed by a test, not by assertion.
8. The 16 tier mismatches are corrected, with a before/after table for all 24.
9. G1–G8 exist in validation; automatic gates ran over 25 weapons with results reported.
10. Existing tests stay green: `WeaponLoadoutGuardTests`, `WeaponBallisticsTests`,
    `WeaponContinuousFireTests`, `WeaponAudioTransientTests`. New behaviour has new tests.
11. A Bootstrap play-test runs: equip a weapon, enter the run, confirm firing and IK still work.
12. `git status` shows zero modified UI prefabs, zero modified scenes, nothing staged.
13. `detect_changes()` run and reported.

If a gate cannot pass, name it exactly. Do not present partial work as complete, and do not stop at a
convenient midpoint.

## 10. Final report format

```text
PHASE: EXECUTE — M7.0 WEAPON FACTORY FOUNDATION COMPLETE / INCOMPLETE

Naming collision: how resolved, and why
Impact analysis: symbols analysed, blast radius, HIGH/CRITICAL items
Task 1 grip camera: tool design, 25 captures, G5 pass/fail list, defects found
Task 2 catalog: schema, consumers migrated, consumers deferred (with reasons)
Task 3 tier: before/after table for all 24, price/ownership impact
Task 4 duplicate shotgun: base chosen, variant recorded, save-safety proof
Task 5 G1–G8: results table over 25 weapons
Task 6 docs: what changed
Tests: existing green, new tests added
Bootstrap play-test result
detect_changes() output: affected symbols and flows
Protected state: UI prefabs, scenes, staged files — git evidence
Files modified — full list
Open risks and the manual-fix queue handed to M7.1
M7.0 STATUS: DELIVERED / INCOMPLETE
M7.1 STATUS: NOT STARTED
BLOCKERS: none / exact blocker
```

Report failures plainly. The shipped arsenal has never passed a visual grip check, so an honest defect
list is the expected result of this milestone, not a problem with your execution.

Completion audio policy: do not generate or play a spoken/TTS report. The owner wants only the
project's short completion notification behavior.

## Completion notification

After the task is genuinely complete and verified:

1. Use the `simplifier-vi` skill at Level 1 to reduce the final report to the outcome, verification performed, and any important remaining issue.
2. Rewrite that summary in natural English using no more than 80 words.
3. Run:

   python Tools\notify_done.py "<English summary>" --repeat 2

If the task is blocked or fails, run the same command with a short English explanation of the blocker. Report any notification-script failure in the written response.
