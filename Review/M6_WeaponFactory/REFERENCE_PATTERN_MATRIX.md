# M6.1 Step 1 — Reference pattern matrix

## Provenance warning — read first

I have **not** played or instrumented these games during this session. Everything below is
**structural designer knowledge**, not direct observation, and it is labelled accordingly:

- `STRUCTURAL` — a well-established, widely-documented structural pattern of the genre.
- `UNVERIFIED` — a pattern I believe holds but cannot confirm from this environment.

**No balance numbers are copied.** Where a number would matter, it is left to this project's own
simulation. Two of the five titles named in the handoff (Megabonk, HoloCure) I know less concretely
than the others; those rows say so rather than inventing detail.

The only *measured* facts in this document are the ones about **this** repository.

---

## 1. One-weapon identity

| | |
|---|---|
| Pattern | The run's whole feel is set by a single starting choice, and upgrades reinterpret that choice rather than replacing it. `STRUCTURAL` — Brotato characters/weapons, 20 Minutes Till Dawn weapon choice. |
| Transfers | Strongly. Owner has already locked one weapon per run. The weapon must therefore be the *loudest* variable in the run, which our arsenal currently fails at — 24 distinct models but only 6 mechanical families. |
| Does not transfer | Brotato lets the player carry **six** weapons simultaneously and buy more mid-run. That is a shop-driven inventory loop; our owner-locked rule forbids it. Do not import "weapon slots" thinking. |
| Consequence here | Identity must be delivered by **family mechanic + signature cards**, not by model count. |

## 2. Large arsenals with small mechanical footprints

| | |
|---|---|
| Pattern | Many visible weapons resolve to a handful of behaviours; variants differ by numbers and looks. `STRUCTURAL` across the genre. |
| Transfers | Directly, and it is the core of the Weapon Factory. Our audit measured **33 bodies / 6 families / 343 attachments** — exactly this shape. |
| Does not transfer | Vampire Survivors' *evolution* system (weapon + passive → new weapon) is a content multiplier we cannot afford: each evolution is bespoke art, VFX and balance. |
| Consequence here | Onboard bodies as **variants inside existing families**. A new model must never require a new mechanic. |

## 3. Family differentiation

| | |
|---|---|
| Pattern | Families are separated by *what question they answer* (crowd vs single target, range, control), not by damage numbers. `STRUCTURAL`. |
| Transfers | Yes, and it is our biggest current gap: all 25 `WeaponData` carry `buildTag = "generalist"` (FACT). |
| Does not transfer | Genre norms often use elemental typing (fire/ice/lightning) as the differentiator. We have no elemental system and adding one is a content tax. Use **positioning and target-preference** instead. |
| Consequence here | Six families, six different questions. Anything that cannot state its question becomes a variant. |

## 4. Rank / offer logic

| | |
|---|---|
| Pattern | Level-up offers a small hand from a weighted pool; ranks deepen an existing choice; the pool respects what the player already owns. `STRUCTURAL`. |
| Transfers | Yes. Our level-up already exists (1-of-3, 30 s, auto-pick) — owner-locked. |
| Does not transfer | Vampire Survivors' banish/reroll/skip economy is a second currency layer. Our Coin already has too few sinks; do not add reroll currency yet. |
| Consequence here | Offer slots must be **role-shaped** (see the offer contract), and at most one pure-stat card. |

## 5. Autonomous powers

| | |
|---|---|
| Pattern | Weapons that fire themselves create spectacle and let the player concentrate on movement. `STRUCTURAL` — the defining loop of Vampire Survivors and its descendants. |
| Transfers | Very well: our combat is already auto-fire with movement as the only continuous input, which is exactly the substrate these powers need. Owner explicitly wants them. |
| Does not transfer | The genre norm of 10–20 simultaneous autonomous effects. On WebGL/mobile with a shared contact-shadow mesh and pooled enemies, that is a performance and readability risk. |
| Consequence here | **Cap concurrent autonomous powers**, budget their spawned effects, and prefer three good ones over ten cheap ones. |

## 6. Stations / shrines

| | |
|---|---|
| Pattern | Fixed world objects trade a legible cost (time, position, danger, currency) for a legible reward, giving a procedural map destinations. `UNVERIFIED` in detail for Megabonk specifically; `STRUCTURAL` for the genre. |
| Transfers | This is the single highest-value import for us: our world is deterministic and traversable but currently has **zero reasons to move** (FACT — no interactive system exists). |
| Does not transfer | Long multi-stage shrine rituals. Our sessions are mobile and interruption-sensitive. |
| Consequence here | One reusable grammar — `Signal + Trigger + Cost/Risk + Completion + Reward + Persistence` — and stations that resolve in 10–60 s. |

## 7. Optional bosses

| | |
|---|---|
| Pattern | Player-summoned elites/bosses convert accepted risk into concentrated reward. `STRUCTURAL`. |
| Transfers | Strongly, and cheaply: we already own three boss-scale enemies and three elite-scale enemies (FACT), all unused in production. |
| Does not transfer | Bespoke multi-phase boss arenas with walls. Our world streams and our player must always be able to run. |
| Consequence here | Boss Beacon spawns an **existing** boss that chases; no arena walls, no new art. |

## 8. Endless pressure

| | |
|---|---|
| Pattern | Difficulty rises continuously through spawn composition and density, with the run ending by attrition rather than a win screen. `STRUCTURAL`. |
| Transfers | Yes — owner-locked endless direction. |
| Does not transfer | Fixed-length timers (Vampire Survivors' 30 minutes, Brotato's wave count) that produce a *victory*. Owner has removed Victory entirely. |
| Consequence here | Threat grows from time, distance and player-chosen activations; composition changes before HP multipliers. |

## 9. Meta unlocks

| | |
|---|---|
| Pattern | Between runs the player unlocks **new options**, not raw power, so early runs stay honest. `STRUCTURAL`. |
| Transfers | Yes, and it supports the W6 decision that weapon star upgrades keep **no design authority**. |
| Does not transfer | Deep permanent stat trees. They inflate the balance surface and make new weapons feel weak. |
| Consequence here | Unlocks are **horizontal within a tier band and controlled between bands** (W3). A newly unlocked weapon is immediately playable **at its full designed power** — no per-weapon upgrade track. |

---

## What this project should deliberately *not* copy

| Tempting import | Why it is refused here |
|---|---|
| Weapon evolutions | Bespoke art/VFX per evolution; our art budget is the constraint |
| Elemental type chart | New content tax with no existing asset support |
| Reroll / banish currency | Coin already lacks sinks; adds a second economy before the first works |
| Six simultaneous carried weapons | Contradicts the owner-locked one-weapon rule |
| Fixed-length run with a victory screen | Contradicts the owner-locked endless direction |
| 10–20 concurrent autonomous effects | WebGL/mobile budget and combat readability |

## The one genuinely novel constraint

None of the reference titles has our specific problem: **a large, visually homogeneous, dark-grey
arsenal in a bright toon world**. Sheet 01 and Sheet 02 show that most bodies are separable only by
colour. That is a *presentation* problem the references do not solve for us, and it is why the Factory
must treat **material/colour treatment as a first-class onboarding step**, not an afterthought.
