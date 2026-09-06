# PHASE: CONCEIVE — M6.2 DECISION LOCK + THREE DESIGN DELTAS (documentation only)

Work in:

```text
D:\Projects\Zombie-War
```

The owner has answered the seven decisions in section 1 of `Docs/M6_ENDLESS_RUN_SYSTEM_DESIGN.md`.
Four are APPROVE, three are CHANGE. The CHANGE answers introduce **new design work**, so M6 cannot be
marked LOCKED by simply stamping the answers in. Record the locks, design the three deltas, reconcile
every line they contradict, then lock M6.

**This is documentation and design only. No runtime implementation. M7 is still not authorized.**

---

## 1. Owner answers — verbatim, now OWNER-LOCKED

Reproduce these in the document exactly as decisions taken, never as proposals awaiting approval.

```text
W1 — APPROVE. Six-family scalable Weapon Factory; no fixed weapon-count assumption.

W2 — APPROVE. Full 23-card catalog is in scope; implement all, with reusable runtime primitives.

W3 — CHANGE. Replace the MW4-specific hard gate with a universal Weapon Visual Onboarding Gate.
     Future weapons are continuously added. Introduce weapon quality/tier into progression and
     permit visually rarer/better weapons to occupy higher controlled power tiers.

W4 — APPROVE. Use a modular World Signal Language; the owner will provide/pick base props from
     future asset additions, while shared FX/icon/beam/ring language communicates function.

W5 — APPROVE. Relic collection system; 2D icon/billboard assets are acceptable.

W6 — CHANGE. Do not merely freeze the legacy economy. Design a replacement economy around
     Coin + Gem + weapon unlock resource/Blueprint + Relic collection; legacy Gold/Shard/Stars/Gacha
     remain in code but lose design authority.

W7 — CHANGE / APPROVE H2 FOR PRODUCTION. 0 % hard conflict is the mandatory gate; ~70 %+ random
     thematic coherence is acceptable. Manual Wardrobe remains the path for deliberately curated
     outfits.
```

Previously locked direction is unchanged and must not be reopened: one endless world on `Map_Level1`;
one weapon per run, no in-run switching; auto-fire, no reload, no manual grenade button; level-up 1-of-3
with a ≤30 s unscaled pause then auto-pick; skills belong to families/tags; WebGL/mobile is a hard
constraint; Coin common, Gem rare and secured on pickup; death banks 25 % Coin, manual abandon 0 %.

## 2. Verified starting state — accepted, do not recompute

```text
415 prefabs scanned; 343 attachments; 33 usable bodies; 24 mechanically distinct weapons
Families: Sidearm 12 · AssaultRifle 10 · Shotgun 6 · Marksman 2 · SMG 2 · LMG 1 (+5 LATER)
263 MW4 prefabs are URP Lit and unconverted
23 skills × 64 fields; MUST 15 / SHOULD 7 / LATER 1; 20 of 23 need new runtime primitives
weapon_balance_model.csv: 24 rows. Measured powerBudgetUsed by family:
  Sidearm 0.205–0.328 (avg 0.261) · SMG 0.355 · Marksman 0.388 · LMG 0.488 · AR 0.465–0.745 (avg 0.599)
  Shotgun 0.254–0.595
WeaponData.tier already EXISTS_AND_USED — it currently drives shop colour and price only
Visual grip validation is NOT RUN; the rig-relative camera is queued in M7.0
H2 outfit: MEASURED 73 % overall pass, 0 % hard conflicts, over 300 seeded outfits
```

Do not re-run the arsenal audit, re-render weapons, or change any verified count.

---

## 3. DELTA A (W3) — universal visual onboarding gate + the weapon tier ladder

### A1 — Generalise the gate

The gate is no longer about MW4. Define a **Weapon Visual Onboarding Gate** that every candidate body
must pass regardless of source pack, including packs bought in the future. Specify the checks, who or
what performs each one, and what a failure does. At minimum: toon material contract, outline contract,
silhouette readability at gameplay camera distance, triangle budget for WebGL, grip and muzzle anchors
present, correct handedness, no hand penetration, muzzle not inside the player or the model, no runtime
material instances, and a stable ID with no save collision.

Mark each check `AUTOMATIC`, `HEURISTIC` or `MANUAL`, and state which ones **block onboarding** versus
which produce a warning. MW4's 263 prefabs become the first, largest client of this gate — an example,
not the definition. State plainly that the gate is what makes "weapons are added continuously" safe.

### A2 — Design the tier ladder

The owner has authorised **controlled vertical power**. Design it so it does not become a grind
treadmill, using the measurement you already have:

- Derive tier bands from the **measured `powerBudgetUsed` spread**, not from invented numbers. Say how
  many tiers, what each band covers, and which of the 24 weapons currently sits in each.
- Tier is a property of the **weapon**, not an upgrade level applied to a weapon. There is no per-weapon
  star or level progression — the legacy star system stays without design authority (see Delta B).
- Every weapon must be **fully playable at its own tier the moment it is unlocked**. Nothing may be
  useless until improved.
- Higher tier must cost something real: unlock price, rarity, unlock ordering, or all three.
- Visual quality may correlate with tier — that is the owner's decision — but state how you prevent
  "the best-looking gun is always the correct gun" from collapsing family choice. Higher tier should
  buy **more power inside its family fantasy**, not erase the reason to play other families.
- Preserve horizontal identity **inside** a tier: two weapons in the same tier must still ask different
  combat questions.
- Say what happens to a low-tier weapon late in a run: does it stay viable through cards, or is it
  simply superseded? Give a recommendation and mark it `PROPOSAL`.

### A3 — Reconcile the contradictions (verified line numbers)

These currently state the opposite of W3 and must be rewritten, not deleted silently:

```text
Docs/GAME_DESIGN.md:495–496   "Weapon unlocks are horizontal - a new weapon is a new playstyle,
                               not a bigger number. A newly unlocked weapon must be immediately
                               playable at base level."
Docs/M6_ENDLESS_RUN_SYSTEM_DESIGN.md:279   the same "immediately playable at base level" rule
Docs/M6_ENDLESS_RUN_SYSTEM_DESIGN.md:108   W6 reasoning that vertical weapon power is inherently wrong
```

The reconciled rule should read roughly: horizontal inside a tier, controlled vertical across tiers,
always immediately playable at its own tier, never a per-weapon upgrade grind. Write it in your own
words, keep it short, and make sure every copy of the old rule now agrees.

### A4 — Schema and migration impact

`WeaponData.tier` already exists and drives shop colour and price. Document what changes when tier also
carries power: which fields the `WeaponCatalog` must expose, what validation must reject (a weapon whose
measured power budget does not match its declared tier), and how weapon 26+ enters a tier without editing
every consumer. Name the impact-analysis targets for M7.0. **Do not edit them.**

## 4. DELTA B (W6) — the replacement economy

Design the economy the owner asked for, not a re-freeze of the old one.

Four live resources:

```text
Coin      common, in-run sink + Hub purchases, death banks 25 %, abandon banks 0 %   OWNER-LOCKED
Gem       rare, secured on pickup, cosmetic/collection                                OWNER-LOCKED
Blueprint the weapon unlock resource — NEW, nothing like it exists today
Relic     collection only, secured on pickup, never raw stats                          approved W5
```

For each, specify: faucets and exactly where they drop, sinks, rarity, per-run expected yield as a
`TUNING` hypothesis, security on death, destination, UI surface, and what decision the player makes with
it. Then answer specifically for **Blueprint**:

- What produces it — boss chests, station rewards, run milestones, duplicates, or a mix? A weapon unlock
  path that only unlocks by luck is rejected; the player must be able to work toward a chosen weapon.
- How does its cost scale with the tier ladder from Delta A?
- How does it coexist with Coin so the two do not collapse into one currency with two names? If you
  cannot justify a genuine second decision, say so and recommend Coin-only unlocks instead.
- Does it convert, and at what pity or floor, so that a long unlucky streak still ends?
- What is the first-hour experience: which weapon does a new player realistically unlock first, and when?

State the legacy position exactly as the owner put it: Gold, Weapon Shards, star upgrades and gacha
**remain in code but lose design authority**. Update all eight-plus places currently marked
`DORMANT / NOT PART OF ACTIVE M6` so they say this, and so none of them reads as "the economy question
is closed with nothing in its place". Keep `EconomyConfig` untouched — this is a design document change.

All numbers are `TUNING` hypotheses. Do not present invented rates as balance facts.

## 5. DELTA C (W7) — H2 approved for production, with the owner's gate

1. Change H2's status everywhere from "best candidate, do not implement" to **APPROVED FOR PRODUCTION**
   under the owner's gate: **0 % hard conflicts is mandatory and non-negotiable; ~70 %+ thematic
   coherence is acceptable.**
2. Remove the old 85 % gate and the line stating the gate is not being lowered. That line was correct
   under the old gate and is now obsolete; mark it superseded by an owner decision, not wrong.
3. Update the M7 ladder entry (currently `M7.8`, gate "≥80 % PASS, 0 hard conflicts") to the new gate.
4. Keep the outstanding work visible: metadata authoring across 453 items (~1–2 designer-days; colour
   bakes automatically, theme/set/body compatibility need a designer), and a re-test that must confirm
   **0 % hard conflicts still holds with authored metadata**. Approval does not delete this work.
5. Record that Manual Wardrobe remains the path for deliberately curated outfits, so the randomiser
   never has to be perfect.
6. **Claim discipline, unchanged:** H2 measured 73 % overall pass and 0 % hard conflicts. Never write
   "0 % failure". Roughly a quarter of random outfits are still thematically weak but not broken — say
   that plainly wherever the number appears.

## 6. Smaller updates from the APPROVE answers

- **W1:** state explicitly that no document may assume a fixed weapon count. Re-check the exact-25
  migration section still holds under "weapons are added continuously".
- **W2:** all 23 cards are in scope. MUST/SHOULD/LATER now express **build order**, not what ships.
  The single LATER card enters scope. Re-state the requirement that the 20 unimplemented cards are built
  from **reusable runtime primitives** — list the primitives the 23 cards actually share (targeting
  queries, proc rate limiting, stacking/ramp state, pooled explosion, chain/arc resolution, timers,
  distance accumulator, threshold triggers) so M7.2 builds a small primitive set rather than 23 bespoke
  behaviours. Say which cards each primitive serves.
- **W4:** specify the **World Signal Language** as a prop-independent contract: ground ring, vertical
  beam, floating icon, emissive accent, progress indicator, and one distinct colour per station type,
  with a rule for how any future prop the owner supplies is dressed by that language. The prop carries
  the mass; the language carries the meaning. Note that Signal Relay and Boss Beacon still have no
  proven body and that this is authored art, not code.
- **W5:** record that Relic art may be **2D icon or billboard**, and update the Relic cost estimate
  accordingly.

## 7. Prohibited

```text
No runtime implementation. No C# edits. No Unity Editor mutation, scene loading or play mode.
No edits under Assets/, ProjectSettings/, Packages/, or any vendor directory.
No re-running the arsenal audit, no re-rendering, no new captures.
No change to any verified count (415 / 343 / 33 / 24 / 23×64 / 12 / 263 / 16) or to the measured
  power-budget numbers.
No new, removed or renamed skills — the 23 names stay.
No reopening owner-locked direction, and no presenting an owner decision as still awaiting approval.
No per-weapon star/level upgrade system. No gacha reactivation.
No staging, committing, pushing, resetting or cleaning.
No M7 execution. No UI prefab or Menu scene work.
No spoken or TTS report.
```

## 8. Acceptance gates

1. Section 1 of `Docs/M6_ENDLESS_RUN_SYSTEM_DESIGN.md` shows W1–W7 as **answered and OWNER-LOCKED**,
   with the owner's CHANGE wording preserved.
2. The universal Weapon Visual Onboarding Gate is specified, source-agnostic, with every check marked
   automatic/heuristic/manual and blocking/warning.
3. The tier ladder is derived from measured power budgets, names which of the 24 weapons sits where, and
   states the anti-grind and anti-dominance rules.
4. `GAME_DESIGN.md:495–496`, `M6_ENDLESS_RUN_SYSTEM_DESIGN.md:279` and `:108` no longer contradict W3,
   and every other copy of the horizontal-only rule agrees with the new one. List each hit you fixed.
5. The replacement economy specifies Coin, Gem, Blueprint and Relic with faucets, sinks, security and
   player decisions; Blueprint has a non-luck path to a chosen weapon or an explicit recommendation
   against introducing it.
6. Legacy Gold/Shard/star/gacha are described as "in code, without design authority" everywhere they
   appear, with nothing left implying the economy question is closed and empty.
7. H2 is APPROVED FOR PRODUCTION under the 0 % hard-conflict gate; the 85 % and ≥80 % gates are marked
   superseded; metadata authoring and the re-test remain visible as outstanding work.
8. No document anywhere states or implies "0 % failure" for H2.
9. All 23 cards are in scope, with a shared primitive list mapping primitives to cards.
10. The World Signal Language is specified independently of any specific prop.
11. Counts and measured numbers re-read and restated unchanged.
12. `git status` proves only `Docs/` and `Review/` changed and nothing is staged.
13. `M6 STATUS: LOCKED` is justified — or the exact remaining blocker is named.

## 9. Final report format

```text
PHASE: CONCEIVE — M6.2 DECISION LOCK COMPLETE / INCOMPLETE

W1–W7 recorded (owner wording preserved)
Delta A — onboarding gate + tier ladder: tier bands, which weapons sit where, anti-grind rules,
          every contradicting line reconciled (file:line list)
Delta B — replacement economy: Coin / Gem / Blueprint / Relic contracts; Blueprint recommendation;
          legacy repositioned
Delta C — H2 production status, new gate, remaining metadata work, claim discipline preserved
W1/W2/W4/W5 updates: fixed-count assumptions removed · 23 cards in scope + shared primitive list ·
          World Signal Language · Relic 2D art
Counts re-verified unchanged
Files modified (docs + review only)
Protected production state — git evidence
Gates: 13/13 or the exact gate that failed
M6 STATUS: LOCKED / NOT LOCKED
M7 STATUS: NOT STARTED / NOT AUTHORIZED
BLOCKERS: none / exact blocker
```

Separate fact from proposal throughout. An owner decision is a fact; your tier bands, Blueprint rates
and economy numbers are `PROPOSAL` or `TUNING` until playtested. Do not describe anything in this pass
as implemented.

Completion audio policy: do not generate or play a spoken/TTS report. The owner wants only the
project's short completion notification behavior.

## Completion notification

After the task is genuinely complete and verified:

1. Use the `simplifier-vi` skill at Level 1 to reduce the final report to the outcome, verification performed, and any important remaining issue.
2. Rewrite that summary in natural English using no more than 80 words.
3. Run:

   python Tools\notify_done.py "<English summary>" --repeat 2

If the task is blocked or fails, run the same command with a short English explanation of the blocker. Report any notification-script failure in the written response.
