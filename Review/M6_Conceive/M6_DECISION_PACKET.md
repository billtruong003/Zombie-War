# M6 — Decision Packet

**READY FOR CODEX + OWNER REVIEW. M6 IS NOT LOCKED.**

Readable standalone. Detail lives in `M6_CURRENT_STATE_AUDIT.md`, `M6_SKILL_AND_LOOP_AUDIT.md`,
`M6_OUTFIT_ASSET_AUDIT.md`, `M6_OUTFIT_GRAMMAR_PROPOSAL.md`.

---

## 1. What the game currently is

A **wave-survival arena**. Press PLAY, spawn in `Map_Level1`, kill 244 enemies across 5 authored
waves with auto-aim and no reload, take 8 numeric perks, win or die, bank Coin, return to the Hub.
A run is roughly 4–6 minutes.

It is polished: the streaming world, character materials, contact shadows, outline, run closure and
profile persistence are all production quality.

## 2. What prior documents intended

An **expedition shooter**. `Docs/GAME_DESIGN.md` (concept-locked, implementation explicitly not
started) specifies: choose a contract, insert into a continuous procedural frontier, follow objective
signals, complete two field POIs, then consciously choose extraction or a deeper boss route, in a
bounded 8–12 minute session, with difficulty rising through enemy *composition* rather than HP.

## 3. What is actually implemented

| | |
|---|---|
| **Implemented and used** | movement, auto-fire, wave spawning, XP, level-up + 7 numeric perks, pickups, Coin, run closure, profile, costume equip/randomize, streaming world |
| **Implemented, not wired** | Gold/Gem (no in-run source), 3 bosses, 11 of 16 enemy types, loot crates, gacha/pass/mission backends |
| **Data present, unreachable** | `Map_Level2–5`, weapon `resourceModel`/`heat`/`charge`/`chain`, `roleTag`/`buildTag`/`buildHint` |
| **Document-only, never built** | contracts, POIs, objectives, extraction, boss objective, threat tiers, spatial bands, arsenal-link states, all 21 candidate skills |

## 4. Major contradictions

1. **Session structure.** GDD = objective-led expedition. Build = 5-wave survival. Nothing in the loop
   asks the player to go anywhere. The procedural world is decorative.
2. **Weapon identity.** GDD and the skill doc both require families to answer different combat
   questions. All 25 weapons are tagged `buildTag = "generalist"` with empty `roleTag`/`buildHint`.
   Only Marksman has a unique mechanic (range + pierce).
3. **Reload.** M4 deleted magazines; `resourceModel = Magazine` still sits on all 25 weapons, and four
   documented skill candidates depend on a reload cycle that no longer exists.
4. **Stale project fact.** `SKILL_SYSTEM_DESIGN.md §1` says perks are never applied. They *are* — all
   five kinds, correctly. The document needs correcting.
5. **Currencies.** Gold and Gem are designed as reward lanes but have **zero in-run faucet**.
6. **Level-up pacing.** GDD wants the first choice at 90–150 s; the shipped curve delivers it at ~10
   kills (~15–25 s) and then ~8 times per run, each a hard pause.

## 5. Findings by priority

### P0 — breaks the design; do not build further on top

| # | Finding |
|---|---|
| P0-1 | The designed session structure is entirely unimplemented. Every other system is specified against a loop that does not exist. |
| P0-2 | **Dominant strategy:** Damage → Fire Rate, uncapped and multiplicative (≈6.97× output over 8 picks). Exactly one build exists. |
| P0-3 | **Weapon identity is unauthored.** Building a skill layer now would hide this, not fix it. The schema to express identity already exists and is empty. |

### P1 — large experience damage

| # | Finding |
|---|---|
| P1-1 | Level-up cadence ~5× too fast, and each level-up hard-pauses combat. |
| P1-2 | Gold/Gem have no faucet; Coin income (~352/run) is negligible against the observed 45.3 K balance. No sink pressure. |
| P1-3 | Nothing rewards movement — GDD Pillar 1 has no implementation. |
| P1-4 | Production outfit randomizer produces a hard visual conflict in **43 %** of outfits and wears 11.8 items at once. |

### P2 — significant improvement

| # | Finding |
|---|---|
| P2-1 | 11 of 16 enemy types and all 3 bosses are unreachable in production content. |
| P2-2 | Dead weapon data fields mislead future authoring. |
| P2-3 | `SKILL_SYSTEM_DESIGN.md §1` factually stale. |
| P2-4 | 32 skill/potion icons exist and **none** is used by any perk. |

### P3 — polish

| # | Finding |
|---|---|
| P3-1 | `Map_Level2–5` and `WD_Level2–5` remain on disk, unreachable. |

## 6. KEEP / ADAPT / CUT / LATER

| Category | Status | Evidence | Reason | Player-facing consequence | Cost | Pri | Depends on | Belongs in |
|---|---|---|---|---|---|---|---|---|
| Player fantasy (frontier hunter) | **KEEP** | GDD §3 | Coherent, matches assets | Identity to build toward | LOW | P0 | — | M6.2 |
| Session structure (expedition) | **NEEDS OWNER DECISION** | GDD §5 vs `WD_Level1` | Biggest gap; expensive | Defines everything | VERY HIGH | P0 | — | M6.2 |
| Wave survival (current) | **ADAPT** | `WaveDirector` | Works; can become the encounter layer inside an expedition | Familiar pressure | LOW | P0 | session decision | M6.2 |
| Exploration / movement motivation | **NOT IMPLEMENTED** | no POI system | Pillar 1 has no code | Reason to move | HIGH | P0 | session decision | M6.3 |
| Combat (auto-fire, no reload) | **KEEP** | `Weapon.cs` | Correct for portrait mobile; owner-passed feel | Unchanged | — | — | — | — |
| Weapon identity | **ADAPT** | 25 assets, all `generalist` | Schema exists, data empty — authoring not engineering | Reason to switch weapons | MEDIUM | **P0** | — | M6.5 |
| Weapon count (25) | **ADAPT** | 6 families | 25 SKUs, 1 real question | Fewer, more distinct guns | LOW | P1 | identity | M6.5 |
| Skills — Arsenal Link states | **KEEP (prototype first)** | Skill doc L3 | Highest leverage; avoids N² | Cross-weapon play | MEDIUM | P0 | weapon identity | M6.4 |
| Skills — 7 numeric perks | **ADAPT** | `RunPerkPool` | Keep as filler (Layer 5), never as the build | Smooths power | LOW | P1 | — | M6.4 |
| Skills — 6 signature techniques | **LATER** | Skill doc L2 | Only after identity is authored | Build identity | HIGH | P1 | weapon identity | M6.4 |
| Skills — reload-dependent candidates (Last Chamber, Opening Burst, Shell Rhythm, Slipstream Feed) | **CUT** | M4 removed magazines | Depend on a deleted system | — | — | P2 | — | — |
| Skills — Crowd Shaper | **CUT** | duplicate of Breach Blast | Higher cost, same job | — | — | P2 | — | — |
| Skills — Fortress Feed | **CUT** | contradicts Pillar 1 | Rewards standing still | — | — | P2 | — | — |
| Skills — Combat Roll | **LATER** | needs 4th input | Violates three-input lock | — | HIGH | P3 | control review | Icebox |
| Skills — 25×5 bespoke trees | **CUT** | Icebox doc self-rejects | Unjustifiable cost | — | VERY HIGH | — | — | Icebox |
| XP / levelling | **ADAPT** | `XpForNextLevel` | Curve too flat and too fast | Fewer, bigger choices | LOW | P1 | — | M6.4 |
| Level-up pause | **ADAPT** | `timeScale = 0` | 8 hard pauses per run | Preserve flow | LOW | P1 | cadence | M6.4 |
| Pickups | **KEEP** | `Pickup`, `PickupManager` | Works | — | — | P2 | — | M6.8 |
| Healing | **NEEDS OWNER DECISION** | only MaxHealth perk | No in-run heal faucet found | Attrition model | MEDIUM | P1 | — | M6.8 |
| Coin | **ADAPT** | `RunState`, `coinReward` | Faucet works, no sink pressure | Meaningful earning | MEDIUM | P1 | meta sinks | M6.9 |
| Gold | **NEEDS OWNER DECISION** | no faucet | Currency with no role | Either give it a source or cut | MEDIUM | P1 | economy | M6.9 |
| Gem | **NEEDS OWNER DECISION** | no faucet | Same | Cosmetic lane only? | MEDIUM | P1 | economy | M6.9 |
| Enemies (16 types) | **KEEP** | `ZombieData` | Strong, varied roster | Composition difficulty | LOW | P0 | — | M6.6 |
| Elites | **NOT IMPLEMENTED** | none found | Cheap via modifiers | Threat spikes | MEDIUM | P2 | enemy roles | M6.6 |
| Boss | **ADAPT** | 3 boss `ZombieData` exist | Built, unreachable | Session climax | MEDIUM | P1 | session decision | M6.7 |
| Objectives / POIs | **NOT IMPLEMENTED** | document-only | Core of the intended loop | Reason to travel | HIGH | P0 | session decision | M6.7 |
| Breakables | **ADAPT** | `DestructibleProp` | Exists, no placement rule | Minor reward beats | LOW | P2 | POI | M6.8 |
| Extraction | **NOT IMPLEMENTED** | document-only | The stated climax | Risk/bank decision | HIGH | P1 | session decision | M6.7 |
| Defeat banking (25 %) | **KEEP** | `RunClosure` | Correct and idempotent | Real stakes | — | P2 | — | M6.9 |
| Meta progression | **ADAPT** | `PlayerProfile` | Backend outruns the loop | Long-term goals | MEDIUM | P2 | economy | M6.9 |
| Outfit generation | **ADAPT → promote H2** | 300-outfit experiment | H0 fails 83 % of the time | Believable characters | MEDIUM | P1 | metadata authoring | M6.1 |
| Costume metadata schema | **NOT IMPLEMENTED** | catalog has no theme/palette fields | Every rule needs somewhere to live | Enables the grammar | MEDIUM | P1 | — | M6.1 |
| Body/gender compatibility | **NEEDS OWNER DECISION** | beards on feminine faces | Unmodellable today | Fixes a visible error | LOW | P2 | schema | M6.1 |
| `Map_Level2–5` | **CUT** | absent from Build Settings | Already retired | none | LOW | P3 | — | — |

## 7. Skill-system diagnosis (one paragraph)

The perk plumbing is **complete and correct** — five perk kinds, all consumed, queued level-ups,
idempotent banking. The content is empty: 7 perks, 100 % pure numbers, 0 behavioural choices, 1
possible build, an uncapped dominant strategy, and 32 unused icons. The documented candidate library
(21 techniques) is well-reasoned but **blocked by an unmet prerequisite**: its own Layer 1 requires
weapon families to already answer different questions, and production data shows they do not. Four
candidates additionally depend on a reload cycle M4 deleted. **The real M6 deliverable is not a perk
list — it is (a) authored weapon identity in the fields that already exist, and (b) one small shared
enemy-state vocabulary (Exposed / Staggered / Pinned) that weapons create and consume.** All three
build fantasies I could construct from existing assets depend on that one primitive.

## 8. Outfit-grammar recommendation

> **PROMOTE H2** (hard compatibility + scored weighted random), accepting a measured 73 % PASS against
> a provisional 85 % gate, and re-testing after real metadata is authored rather than tuning weights
> to manufacture the number.

## 9. H0 / H1 / H2 results

| Metric | H0 = production | H1 | H2 |
|---|---:|---:|---:|
| PASS | **17 %** | 58 % | **73 %** |
| Hard conflicts | **43 %** | **0 %** | **0 %** |
| Over-accessorised | 57 % | 1 % | 7 % |
| Theme mismatch | 24 % | 33 % | **4 %** |
| Palette conflict | 21 % | 11 % | 11 % |
| Mean items worn | 11.8 | 9.2 | 9.6 |
| Deterministic | 100 % | 100 % | 100 % |

100 generated per hypothesis over identical seeds; 30 each rendered and vision-reviewed. Two errors in
my own experiment were found and disclosed (an off-by-one in inferred metadata that changed every
number, and a hair-suppression rule discovered from a residual 5 % conflict).

## 10. Owner decisions required

| # | Decision | Why it blocks | My recommendation |
|---|---|---|---|
| 1 | **Expedition loop, or improved wave survival?** | Determines M6.2–M6.9 entirely | **Hybrid**: keep waves as the encounter layer, add *one* objective + extraction. Full expedition is VERY HIGH cost; pure waves wastes the world. |
| 2 | Does "commit to a position" count as valid play, or does Pillar 1 forbid it? | Decides whether the LMG build exists | Allow it as one answer among several |
| 3 | Do Gold and Gem get in-run faucets, or is one cut? | Two dead currencies today | Give Gold a faucet (objective/boss reward); keep Gem meta-only |
| 4 | Weapon count: keep 25 or consolidate? | Balancing and identity cost | Consolidate to ~12 with real identity |
| 5 | Add `bodyCompatibility` to the costume schema? | Beards on feminine faces | Yes — LOW cost, visible fix |
| 6 | Are the 30 authored costume sets canonical? | Could cheaply raise coherence | Yes, add an explicit "roll a set" branch |
| 7 | Accept 73 % outfit PASS as the prototype gate? | Gates promotion | Accept for prototype; re-gate after authoring |
| 8 | Healing model in-run? | Attrition has no answer today | Earned recovery, not passive regen |

## 11. Recommended M6 sequence

| Order | Section | Question it answers | Merge? |
|---|---|---|---|
| 1 | **M6.2 Player Fantasy + Session Structure** | What *is* a run? | Blocks everything — do first |
| 2 | **M6.5 Weapon Family Grammar** | Why switch weapons? | Merge with M6.6 (enemy roles are the questions weapons answer) |
| 3 | **M6.4 Skill / Run-Build** | What does a level-up mean? | Depends on 2 |
| 4 | **M6.3 + M6.7 Exploration + POI/Extraction** | Why move? | Merge — same problem |
| 5 | **M6.8 + M6.9 Pickups + Economy** | What is worth collecting? | Merge |
| 6 | **M6.12 Balance Model** | Do the numbers hold? | After 1–5 |
| 7 | **M6.10 UI/UX** | How is it read? | After mechanics settle |
| 8 | **M6.11 Content/Data Architecture** | How is content authored? | Can run parallel with 5 |
| 9 | **M6.13 Vertical Slice Scope** | What does M7 build? | Last |

**M6.1 (this phase, outfit grammar) is deliberately independent** of the loop decision and can proceed
in parallel.

## 12. Recommended M7 vertical slice (smallest coherent)

```text
ONE contract · ONE objective · ONE extraction · 4 enemy types · 3 weapon families
with authored identity · 3 shared enemy states · 6 signature techniques · 8-minute run.

Pass:  players voluntarily start a 3rd run; ≥2 distinct builds appear;
       players switch weapons for a reason other than DPS.
Fail:  one technique always correct; runs 2 and 3 play identically.
```

Do **not** build 25 weapons × N skills before proving a level-up is worth stopping for.

## 13. Explicitly NOT implemented in this phase

- No expedition loop, objectives, POIs, extraction or bosses.
- No new skill system; no change to `RunPerkPool`.
- No change to the runtime costume randomizer (`CostumeScreen.RandomizeCasual` untouched).
- No change to `CasualCostumeCatalog.asset`, production scenes, prefabs or gameplay scripts.
- No costume metadata written into any production asset.
- No canonical document rewritten (`GAME_DESIGN.md`, `MVP_SHIP_PLAN.md` untouched).
- No balance data tuned; no runtime ScriptableObject created.
- Nothing staged or committed.

**The H0/H1/H2 generators exist only as a scratch Python script outside the repository.**
