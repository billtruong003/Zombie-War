# PHASE: CONCEIVE — M6.1 CORRECTION PASS (documentation only)

Work in:

```text
D:\Projects\Zombie-War
```

M6.1 Weapon Factory was executed and its **analysis is accepted**. This is a **narrow correction pass**,
not a re-run. Four defects were found by independent audit. Fix exactly those four, prove them, stop.

---

## 1. Verified starting state — audited on disk, do not re-derive

These were independently verified by the reviewer and are **accepted as correct**. Do not recompute,
re-render or "improve" them:

```text
Review/M6_WeaponFactory/Inventory/weapon_inventory.csv      415 rows, buckets sum to 415
  AttachmentOrPart 343 · DuplicateModel 30 · CompleteWeaponCandidate 28
  NeedsShaderConversion 5 · LaterSpecialFamily 5 · DemoOrSceneObject 3 · ColourVariant 1
Packs: MW4 263 · ShotgunPack1 77 · PistolPack1 34 · Project 25 · WeaponsVol1 16  (= 415)
All 263 MW4 prefabs are urp-lit-needs-conversion
33 active bodies: Sidearm 12 · AssaultRifle 10 · Shotgun 6 · Marksman 2 · SMG 2 · LMG 1
  (+5 LATER: 4 Launcher + M2_50cal)
WPN_Shotgun_BenelliM4 == WPN_Shotgun_Generic (2133 tris, bounds 0.082×0.302×1.465) → 24 distinct
skill_catalog.csv    23 rows × 64 columns, names exactly as owner-specified, MUST 15 / SHOULD 7 / LATER 1
weapon_balance_model.csv  24 rows; AR 0.465–0.745 (avg 0.599) vs Sidearm 0.205–0.328 (avg 0.261)
BUILD_SIMULATIONS.md  12 simulations, 2 per active family
LOOP_ECONOMY_AND_MIGRATION.md  6 interactives contracted; all 16 enemy assets accounted for
Production state clean: 0 files under Assets/, ProjectSettings/, Packages/ modified; 0 files staged
```

The reviewer also opened `Sheet_02_SMG_AR_LMG.png` at magnification and **confirms** the URP-Lit
finding: the MW4 bodies carry no toon outline and show speckled specular noise while the project's
toon bodies have clean outlines. Material conversion as a hard gate is accepted.

## 2. Owner-locked — never reopen, never turn into a question

```text
One endless procedural world; production uses Map_Level1; Maps 2–5 retired
One weapon chosen before a run; no in-run switching
Auto-fire; no reload/magazine; no manual grenade button
Level-up 1-of-3, pause ≤30 s unscaled, then valid auto-pick
Skills belong to families/tags, not individual weapon models
WebGL/mobile is a hard constraint
Coin common; Gem rare and secured on pickup
Death banks 25 % Coin; manual abandon banks 0 %
Outfit H2 = 73 % overall pass, 0 % hard conflicts — NOT production-ready, never "0 % failure"
M7 is not authorized
```

---

## 3. DEFECT 1 — the canonical M6 document contradicts itself (highest priority)

**Evidence.** `Docs/M6_ENDLESS_RUN_SYSTEM_DESIGN.md`:

- line 30 — "Nothing in this document is a production commitment until the owner answers section 1."
- line 34 — `## 1. OWNER DECISIONS REQUIRED BEFORE M6 LOCK` — "Eight decisions."
- line 38 — `### D1 — Three prototype weapon candidates`, still recommending
  `WD_Sidearm_FiveSeven` / `WD_SMG_Generic` / `WD_Shotgun_AA12`.
- line 122 — the `D1 Weapons: APPROVE / CHANGE` answer template.
- lines 155/157/158 — ledger rows still routing approval to **D1** and **D2**.
- line 300 — section 10 states the three-gun framing is **retired**.
- line 424 — section 12 states the 15-card shortlist is **replaced** by the 23-card catalog.

So the same file both retires and demands approval of the retired framing. The M6.1 handoff required
removing the three-gun roster limitation, eliminating conflicting directions, and avoiding unexplained
`D1–D8` bureaucracy. Section 1 was never updated.

**Fix.** Replace section 1 with a new, Weapon-Factory-era decision list, and repair everything that
points at the old one.

### 3.1 The new decision list — exactly these seven, no more, no fewer

Use plain identifiers `W1`–`W7`. Every entry must carry: **Evidence · Recommendation · Alternatives ·
Consequence · Cost · Not yet evidenced**. Each must be answerable without reading another document,
and must explain any term the owner has not already approved.

```text
W1  Arsenal model: 6 active families (Sidearm, SMG, Assault Rifle, Shotgun, Marksman, LMG),
    variants living inside families, 33 usable bodies → 24 mechanically distinct weapons.
    Includes the BenelliM4 / Shotgun_Generic duplicate: make one a declared colour/variant entry
    rather than a separate weapon.
W2  Candidate card pool: the 23-card catalog (5 stat + 12 signature + 4 autonomous + 2 universal),
    MUST 15 / SHOULD 7 / LATER 1, with the "max one pure-stat card per 1-of-3 offer" rule.
    State plainly that 20 of 23 need new runtime primitives.
W3  MW4 material conversion as a hard gate: 263 prefabs are URP Lit; no MW4 body may be onboarded
    before toon conversion. This gate governs the best available SMG (SMG_P) and marksman (Recon_P)
    bodies, so it decides whether those two single-body families can grow at all.
W4  First three interactives: Signal Relay, Supply Cache, Boss Beacon — and the fact that Relay and
    Beacon still have no proven visual body, which is authored art work, not code.
W5  Relic Fragment: collection-only proposal, no locked drop rates.
W6  Gold / Weapon Shard / star upgrades / gacha remain DORMANT.
W7  Outfit H2: continue as best candidate and author real metadata; do NOT adopt as production.
```

Provide an answer template in the same shape as before:

```text
W1 Arsenal model:      APPROVE / CHANGE [...]
W2 23-card pool:       APPROVE / CHANGE [...]
...
```

### 3.2 Everything that must stop referring to the old packet

- Delete or rewrite the line-30 preamble so the document's authority no longer hangs on the old
  eight decisions.
- Rewrite ledger rows in section 1b so approval routes to `W1`–`W7`.
- Keep the retired framing visible **only** as an explicitly labelled `REJECTED` / historical line, so
  nobody re-proposes it. It must be impossible to read it as a live question.
- Search every canonical document for `D1`…`D8`, "three prototype", "15-card", "answers section 1",
  and repair each hit in `Docs/M6_ENDLESS_RUN_SYSTEM_DESIGN.md`, `Docs/GAME_DESIGN.md`,
  `Docs/MVP_SHIP_PLAN.md`, `Docs/README.md`. Report every hit and what you did with it.
- Update the Vietnamese Simplifier section so it describes `W1`–`W7`, not `D1`–`D8`.

---

## 4. DEFECT 2 — `VISION_REVIEW.md` claims a grip result the image cannot show

**Evidence.** `Review/M6_WeaponFactory/VISION_REVIEW.md` lines 77–79 state that in every frame the
weapon is attached at the hand, oriented forward, "with no visible hand penetration through the model
and no weapon floating away from the grip."

The reviewer opened `Evidence/Sheet_06_PlayerHeld.png`. The camera sits directly above the character's
backpack; **no hand is visible in any of the six frames** — only a partial barrel at the frame edge.
The stated positive cannot be read from those pixels. The document currently calls this merely "poor
framing", which understates it: the hand-penetration check **was not performed**, it is `NOT RUN`.

**Fix.**

1. Replace the "Positive result" paragraph with an honest status: the six frames prove that six
   distinct weapons were equipped on the real player through the real equip path (that part is
   genuine — the frames differ and the earlier byte-identical bug is gone), and prove nothing about
   hand penetration, grip alignment or muzzle placement.
2. Mark the visual grip validation `NOT RUN — requires a fixed rig-relative camera`.
3. Keep the data evidence exactly as it stands, because it is genuine and independently confirmed:
   25/25 production prefabs carry `WeaponGripPoints` with right-hand, left-hand and muzzle transforms,
   and 25/25 `WeaponData` assets set `useAuthoredGripPositions = true`.
4. Move "build a rig-relative grip-validation camera and re-shoot the holding sheet" into the
   Weapon Factory manual-fix / tooling queue in `WEAPON_FAMILIES_AND_FACTORY.md` section 5.3, and into
   the **M7.0** deliverable list. It is tooling work, not analysis work.
5. Do **not** re-shoot the sheet in this pass. Do not enter play mode. Do not open or modify any scene.

---

## 5. DEFECT 3 — a required deliverable is missing from its specified path

The handoff required `Review/M6_WeaponFactory/WEAPON_BALANCE_MODEL.md`. It does not exist; the content
lives inside `WEAPON_FAMILIES_AND_FACTORY.md` under "Step 4 — Weapon schema and balance", and the final
report did not disclose the merge.

**Fix.** Create `Review/M6_WeaponFactory/WEAPON_BALANCE_MODEL.md` containing the balance work at the
required path: field marking, the power model and its coefficients (all still labelled `TUNING`),
family envelopes, tier philosophy, side-grade rules, soft caps, boss-versus-crowd DPS, and a pointer to
`weapon_balance_model.csv`. Either move the content out of `WEAPON_FAMILIES_AND_FACTORY.md` or leave a
short pointer there — but the two files must not disagree. Numbers must not change: AR 0.465–0.745,
Sidearm 0.205–0.328, 24 rows.

---

## 6. DEFECT 4 — evidence hygiene

1. `Evidence/Sheet_02_SMG_AR_LMG.png` is titled "(13)" but contains 14 bodies (2 SMG + 10 AR + 2 LMG),
   which is what `VISION_REVIEW.md` correctly states. Correct the title. Rebuild the sheet from the
   existing renders under `Evidence/bodies/`; do not re-render any weapon.
2. Check the other five sheet titles against their real tile counts and against `VISION_REVIEW.md`.
   Fix any further mismatch, or state that none exists.
3. Move working scratch files out of the deliverable root into `Review/M6_WeaponFactory/_work/`:
   `_bucket_table.md`, `Inventory/_raw_prefab_scan.tsv`, `Inventory/_render_queue.json`,
   `Inventory/_render_queue.tsv`, `Inventory/_render_queue_misc.tsv`, `Evidence/_progress.txt`,
   `Evidence/bodies/_progress.txt`, `Evidence/held/_log.txt`.
   Keep them — they are provenance — just stop them from looking like deliverables. Update any
   document that references their old paths.

---

## 7. Prohibited in this pass

```text
No runtime implementation. No C# edits. No scene loading, play mode, or Unity Editor mutation.
No re-running the arsenal audit. No re-rendering weapon bodies. No new contact-sheet captures.
No changes to any verified count (415 / 343 / 33 / 24 / 23 / 64 / 12 / 263 / 16).
No new, removed or renamed skills. The 23 names are owner-locked.
No edits under Assets/, ProjectSettings/, Packages/, or any vendor directory.
No staging, committing, pushing, resetting or cleaning.
No M7 execution. No UI prefab or Menu scene work of any kind.
No spoken or TTS report.
```

## 8. Acceptance gates — all must pass

1. `Docs/M6_ENDLESS_RUN_SYSTEM_DESIGN.md` section 1 presents `W1`–`W7` with full evidence/recommendation
   /alternatives/consequence/cost, and an answer template.
2. Searching the four canonical documents for `D1`, `D2`, `D8`, "three prototype", "15-card" returns
   only lines explicitly marked historical or `REJECTED` — no live question, no live recommendation.
3. No document states that M6 lock depends on the old eight decisions.
4. The Vietnamese Simplifier section describes `W1`–`W7`.
5. `VISION_REVIEW.md` no longer asserts an unverifiable hand-penetration result, and marks the visual
   grip validation `NOT RUN`.
6. The rig-relative grip camera appears in the Factory manual-fix queue and in M7.0.
7. `Review/M6_WeaponFactory/WEAPON_BALANCE_MODEL.md` exists, and does not disagree with
   `WEAPON_FAMILIES_AND_FACTORY.md` or `weapon_balance_model.csv`.
8. Sheet titles match their real tile counts.
9. Scratch files relocated; no broken path references left behind.
10. Counts re-read from the CSVs and restated unchanged.
11. `git status` proves nothing outside `Docs/` and `Review/` changed, and nothing is staged.
12. M7 remains NOT STARTED / NOT AUTHORIZED.

If a gate cannot pass, name the exact gate and why. Do not report completion with an open gate, and do
not stop at a convenient midpoint.

## 9. Final report format

```text
PHASE: CONCEIVE — M6.1 CORRECTION COMPLETE / INCOMPLETE

Defect 1 — canonical contradiction: what section 1 now says; every D1–D8 hit found and its disposition
Defect 2 — VISION_REVIEW: old wording vs new wording; where the grip camera was queued
Defect 3 — WEAPON_BALANCE_MODEL.md: path, contents, how duplication was avoided
Defect 4 — sheet titles corrected; scratch files relocated; references repaired
Counts re-verified (415 / 33 / 24 / 23×64 / 12 / 263 / 16)
Files modified (docs + review only) — full list
Protected production state — git evidence
Gates: 12/12 or the exact gate that failed
M6 STATUS: LOCKED / NOT LOCKED
M7 STATUS: NOT STARTED / NOT AUTHORIZED
BLOCKERS: none / exact blocker
```

State plainly that M6 stays **NOT LOCKED** until the owner answers `W1`–`W7`. Do not describe owner
approval as obtained. Do not describe a proposal as implemented.

Completion audio policy: do not generate or play a spoken/TTS report. The owner wants only the
project's short completion notification behavior.

## Completion notification

After the task is genuinely complete and verified:

1. Use the `simplifier-vi` skill at Level 1 to reduce the final report to the outcome, verification performed, and any important remaining issue.
2. Rewrite that summary in natural English using no more than 80 words.
3. Run:

   python Tools\notify_done.py "<English summary>" --repeat 2

If the task is blocked or fails, run the same command with a short English explanation of the blocker. Report any notification-script failure in the written response.
