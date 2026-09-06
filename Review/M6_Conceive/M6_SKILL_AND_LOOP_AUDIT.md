# M6 — Skill System and Loop Forensic Audit

**Phase:** CONCEIVE. No skill is approved, implemented or promoted.
**Reference read:** `references/loop-and-progression.md`, `references/combat-and-cast.md`,
`references/anti-patterns.md`, `references/math-and-balance.md`.
**Sources audited:** `Docs/Reference/Design/SKILL_SYSTEM_DESIGN.md`,
`Docs/Icebox/IN_RUN_SKILL_STAT_AND_INTERACTIVE_DESIGN.md`, `RunPerkPool`, `RunState`, `RunOverlays`,
25 `WeaponData`, 32 icons (`Assets/Icons/skills` 22 + `Assets/Icons/potions` 10).

---

## 1. Correction to the canonical skill document

`SKILL_SYSTEM_DESIGN.md §1` records as PROJECT FACT:

> "`RunOverlays.ShowLevelUp()` is only a test hook. `PickPerk()` closes the overlay without choosing
> or applying a perk. Existing run multipliers are not fully consumed by weapon/player runtime."

**This is out of date.** As of the current build:

| Perk kind | Consumer | Verified at |
|---|---|---|
| Damage | `Weapon.ApplyHit` | `Weapon.cs:650` |
| FireRate | `Weapon.PerkedFireRate` | `Weapon.cs:120` |
| MoveSpeed | `PlayerMovement.FixedUpdate` | `PlayerMovement.cs:78` |
| CoinGain | `RunState.ScaleCoin` (single ledger point) | `RunState.cs:129` |
| MaxHealth | `Health.IncreaseMax` at pick time | `RunOverlays.cs:375` |

The real flow is `RunState.LevelsGained → queue → pause → 1-of-3 offer → AddPerk → resume`, with
queued level-ups presented one at a time. The plumbing is **complete and correct**. The problem is not
wiring — it is that there is nothing interesting to wire.

---

## 2. Perk inventory — everything that exists

| Perk | Icon | Documented intent | Runtime implementation | Effect applied | Stackable | Max stacks | Weapon dep. | Synergy dep. | Status | Problem |
|---|---|---|---|---|---|---|---|---|---|---|
| Damage +15% | none | filler stat (Layer 5) | `RunPerkPool` | ×1.15 all weapons | yes | **uncapped** | none | none | IMPLEMENTED | pure number |
| Damage +30% | none | filler stat | `RunPerkPool` | ×1.30 all weapons | yes | uncapped | none | none | IMPLEMENTED | strictly better than +15%, same slot cost |
| Fire Rate +12% | none | filler stat | `RunPerkPool` | ×1.12 | yes | uncapped | none | none | IMPLEMENTED | pure number |
| Fire Rate +25% | none | filler stat | `RunPerkPool` | ×1.25 | yes | uncapped | none | none | IMPLEMENTED | strictly better than +12% |
| Move Speed +10% | none | filler stat | `RunPerkPool` | ×1.10 | yes | uncapped | none | none | IMPLEMENTED | only defensive option |
| Max Health +20% | none | filler stat | `RunPerkPool` | +20% max & current, at pick | yes | uncapped | none | none | IMPLEMENTED | invisible mid-fight |
| Coin Drops +25% | none | filler stat | `RunPerkPool` | ×1.25 coin | yes | uncapped | none | none | IMPLEMENTED | economy has no sink (see audit §2.4) |

**No perk in the game uses any of the 32 icons.** The level-up UI binds title/description text only
(`BindOfferText` → `Perk{i}/Name`, `Perk{i}/Desc`). The icon library is entirely unused.

**Everything in `SKILL_SYSTEM_DESIGN.md §3` — all 21 candidate techniques across 6 weapon families
plus 4 arsenal/defensive candidates — is `CANDIDATE`, document-only, zero implementation.**

---

## 3. The ten required questions, answered

**1. How many genuinely distinct gameplay decisions exist?**
**Zero.** Seven perks, five of which are "a bigger number in one of five categories". A decision
requires a trade-off; none of these trade anything against anything.

**2. How many choices are only numerical upgrades?** **7 of 7 (100%).**

**3. How many choices alter player behaviour?**
**Arguably one, weakly.** Move Speed +10% marginally changes kiting viability. Nothing changes what
the player *does*: no new input, no new timing, no new target priority, no new positioning rule.

**4. How many meaningfully different builds are possible?**
**One.** With 8 level-ups drawn 3-of-7, the player will see Damage and/or Fire Rate in
essentially every offer. P(neither of the 4 DPS perks appears in a 3-of-7 draw) = C(3,3)/C(7,3) =
1/35 ≈ **2.9%**. Over 8 draws, the chance of *never* being offered a DPS perk is ~0. There is no
build; there is a queue of multipliers.

**5. Is there a dominant strategy?**
**Yes, unambiguously.** Damage and Fire Rate multiply the same output term and are uncapped.
Taking all 8 as DPS gives, e.g., 1.30⁴ × 1.25⁴ ≈ **6.97× damage output**. Max Health +20% ×8 ≈ 4.3×
effective HP but does not shorten any fight. In a run whose only failure mode is being overwhelmed,
killing 7× faster dominates surviving 4× longer.

**6. Can the player understand why one choice is useful?**
Yes — trivially, and that is the problem. "+30% damage" needs no understanding, teaches nothing and
creates no expertise. This is textbook **fake choice** (`anti-patterns.md`): three options, one
correct answer, no information gained.

**7. Does the current level-up cadence interrupt combat too frequently?**
**Yes.** 8 level-ups per full clear, first at ~10 kills, each a full-screen `Time.timeScale = 0`
pause. GDD §10 asks for the first choice at 90–150 s and widening intervals; the shipped curve
(`10 + (L-1)×8`) is near-linear and produces roughly one pause every ~35 s. Two separate level-up
modals appeared within the live test session's first minute of real combat.

**8. Do the 22 skill icons correspond to a coherent taxonomy?**
**Partly.** Grouping them by the fantasy they actually depict:

| Cluster | Icons | Coherent? |
|---|---|---|
| Movement / evasion | acrobat, runner, runningfist, runningstrike, highkick | yes — maps to a Momentum/reposition family |
| Impact / control | fist, lowkick, powerstrike, fighter, sturdy | yes — maps to Stagger/Pinned/breach |
| Execution / punish | backstab, punisher, knifemastery, pistol | yes — maps to cash-out on a created state |
| Weapon rhythm / sustain | reload, packaging, machine, repair | yes — maps to resource/tempo |
| Ambiguous | armyman, alchemy, beast, revive | weak — no obvious mechanic |
| Potions (10) | adrenaline, antidote, energetic, falc_mixture, gemostatic, ofi, painkillers ×2, rage_potion, salve | consumable framing, **not** a run-perk framing |

So: **four coherent mechanic families and one leftover pile.** The icons support roughly
*Momentum / Control / Execution / Tempo* — which is very close to the Arsenal Link vocabulary already
proposed (Exposed, Staggered, Pinned, Momentum). That is a genuine asset, but it is **32 icons, not 32
mechanics**. Forcing one mechanic per icon is the trap the design doc already warns about.

**9. Which previous skill ideas are worth keeping?**
Judged against: does it change behaviour, is it readable under auto-fire, is it affordable?

| Keep | Why |
|---|---|
| **Arsenal Link states** (Exposed / Staggered / Pinned / Momentum) | One shared vocabulary makes N weapons interact without N² authoring. This is the single highest-leverage idea in the document. |
| **Punish Window** (sniper) | Uses enemy recovery states that **already exist** (pouncer, charger, burrower telegraphs). Cheapest real decision available. |
| **Breach Blast** (shotgun) | Displacement/space creation is legible at mobile scale and gives the shotgun a non-DPS reason to exist. |
| **Suppression Lock / Pressure Bank** (LMG) | Gives the LMG the "commit and cash out" identity it currently lacks. |
| **Armor Drill** (AR) | Turns sustained fire into a state, which is what an AR should do under auto-fire. |
| **Quickdraw Verdict** (sidearm) | Makes the weakest weapon class an intentional finisher instead of a strictly-worse AR. |
| **Field Repair** | The only defensive candidate that is *earned* rather than passive. |

**10. Which are duplicates, fake choices or too expensive?**

| Cut / defer | Why |
|---|---|
| **Last Chamber** (sidearm) | Depends on a magazine that M4 deleted. Cannot work without reintroducing reload. **CUT.** |
| **Opening Burst** (AR) | Same — "after a complete reload" has no meaning. **CUT.** |
| **Shell Rhythm** (shotgun) | Requires player-timed firing; auto-fire removes the agency it depends on. **CUT.** |
| **Slipstream Feed** (SMG) | Sustains "ammo/reload tempo" — no such resource exists. **CUT.** |
| **Combat Roll** | Requires a 4th input; violates the stated three-input lock. **LATER.** |
| **Crowd Shaper** (shotgun) | Duplicates Breach Blast's space-creation at much higher simulation cost. **CUT as duplicate.** |
| **Killstream Capacitor** (SMG) | Rewards farming fodder — a snowball anti-pattern in a wave game. **LATER.** |
| **Fortress Feed** (LMG) | Rewards standing still; directly contradicts GDD Pillar 1 "Move with purpose". **CUT.** |
| **Bounty Tag** (sidearm) | Marking depends on choosing a target; auto-aim owns targeting. **LATER.** |
| **Target Relay** (AR) | Overrides auto-aim ownership — high confusion risk for low payoff. **LATER.** |
| **Adrenal Response** | Rewards taking damage; noisy at low health. **LATER.** |
| **Arsenal Execution** | Hard-codes the unproven cross-weapon hook as a mandatory meta-skill. **LATER — this is the thing to prove, not to ship first.** |
| **25 weapons × 5-rank bespoke trees** (Icebox doc) | The Icebox document itself already rejects this. Cost is unjustifiable. **STAYS CUT.** |
| Skill Crates / Loot Crates / Upgrade Core (Icebox) | Extra economy layer before the loop is proven. **LATER.** |

---

## 4. The prerequisite nobody has met

`SKILL_SYSTEM_DESIGN.md` Layer 1 states the gate plainly:

> "Each family must already answer a different combat question. If removing a skill makes two families
> identical, Phase 2 failed and the skill is hiding the problem."

**Measured against production data, Phase 2 has failed.** All 25 weapons carry `buildTag =
"generalist"`, empty `roleTag`, empty `buildHint`, `chainCount = 0`, `chargeTime = 0`, and identical
`resourceModel = Magazine` (itself dead since M4). Weapon differences are:

```text
AssaultRifle  144–240 DPS,  range 9
SMG           112 DPS,      range 5.8
LMG           143 DPS,      range 9.5
Shotgun        66–192 DPS,  range 5–5.5,  8 pellets
Marksman       81 DPS,      range 16,     pierce 3
Sidearm        48–91 DPS,   range 7.5
```

Only **Marksman** (long range + pierce 3) has a mechanical property no other family has. The other
five differ by numbers on the same axis. Shotgun pellets are a spread pattern, not a different
question.

> **P0.** Building a skill layer now would hide the weapon-identity problem rather than solve it.
> Weapon identity must be authored **before** signature techniques, and — critically — the data
> schema to express it already exists and is empty. This is authoring cost, not engineering cost.

---

## 5. Three provisional build fantasies

Constructed **only** from hooks that plausibly exist in the current codebase (existing enemy recovery
states, existing `pierceCount`, existing knockback field, existing perk plumbing, existing icons).
These are prototype targets, **not approved designs**.

### Build A — "Breacher" (shotgun anchor)

```text
Build identity      Create space, then punish what is left standing.
Weapon fit          Shotgun primary, sidearm finisher.
Primary decisions   When to spend a blast on space vs on damage; when to step in for the finish.
Required skills     Breach Blast (displacement + Staggered) → Quickdraw Verdict (execute Staggered).
Behaviour change    Player deliberately lets enemies close to breach range instead of kiting.
Enemy matchup       Strong vs DogPup/CatMeow swarms and runners; weak vs SkeletonMage at range.
Weakness            Range 5.5; ranged enemies punish the approach.
Missing impl.       Staggered state; `knockback` field exists but is not a shared state.
Icons available     fist, highkick, powerstrike, punisher, pistol, backstab
```

### Build B — "Line Holder" (LMG anchor)

```text
Build identity      Commit to a lane, bank pressure, release it.
Weapon fit          LMG primary, marksman secondary.
Primary decisions   When to commit; when to stop holding and spend the bank.
Required skills     Suppression Lock (Pinned in a lane) → Pressure Bank (spend) → Punish Window.
Behaviour change    Player picks and holds a firing lane instead of circling — a real positional choice.
Enemy matchup       Strong vs dense Skeleton lines; weak vs Burrow (emerges inside the lane).
Weakness            Commitment; flanks and burrowers break it.
Missing impl.       Pinned state, pressure meter, lane concept.
Icons available     machine, sturdy, fighter, ofi, packaging
```

⚠️ Design tension to resolve, not paper over: this build rewards **holding ground**, which contradicts
GDD Pillar 1 ("Move with purpose"). Either the pillar admits a "commit to a position" answer, or this
build should be cut. That is an **owner decision**.

### Build C — "Marksman Punisher" (sniper anchor)

```text
Build identity      Read the telegraph, take the one shot that matters.
Weapon fit          Marksman primary, AR to create the opening.
Primary decisions   Which target to set up; whether the recovery window is worth the reposition.
Required skills     Armor Drill (AR creates Exposed) → Punish Window (marksman consumes it).
Behaviour change    Player watches enemy commitment/recovery instead of holding fire down.
Enemy matchup       Strong vs Cactus/MoleRat/Burrow (long recoveries) and bosses; weak vs fast swarms.
Weakness            81 DPS; a swarm that closes the gap wins.
Missing impl.       Exposed state; enemy recovery windows exist as animation but are not queryable.
Icons available     backstab, punisher, knifemastery, armyman, falc_mixture
```

**What these three prove:** distinct runs *are* reachable from the existing asset base, but **all three
depend on the same missing primitive** — a small shared state vocabulary (Exposed / Staggered / Pinned)
that enemies can carry and weapons can create and consume. That primitive, not the perk list, is the
real M6 deliverable.

> **HYPOTHESIS to prototype before authoring any content:**
> *One shared 3-state enemy vocabulary + one signature technique per weapon family (6 total) produces
> ≥3 recognisably different runs.*
> **Prototype:** 1 map, 4 enemy types, 6 techniques, 8-minute run.
> **Pass:** ≥2 distinct builds appear in play; players switch weapons for a reason other than DPS.
> **Fail:** one technique is always correct; players still never switch.

---

## 6. Loop-level diagnosis (`loop-and-progression.md` lens)

| Loop tier | Intended | Actual | Verdict |
|---|---|---|---|
| Action (1–5 s) | read → space → act → swap → exploit | hold position, auto-fire | **broken** — no read, no swap |
| Encounter (20–90 s) | approach → identify → clear priority → collect → move on | waves arrive; kill everything | **degenerate** — no composition reading, no priority |
| Session (8–12 min) | 2 objectives → extract-or-boss → bank | 5 waves → victory | **not implemented** |
| Meta (multi-session) | arsenal breadth → higher tiers → new rules | weapon levels + cosmetics | **exists but disconnected** — no tier ladder to climb |

**Decision density** (`loop-and-progression.md`): the current run offers 8 decisions, all of which are
the same decision, and 0 spatial decisions. A functioning action-roguelite of this length should offer
on the order of 8–12 *distinct* decisions plus continuous positional ones.

**Anti-patterns positively identified** (`anti-patterns.md`):

- **Fake choice** — 1-of-3 where one option always dominates.
- **Dominant strategy** — Damage/Fire Rate, uncapped and multiplicative.
- **Stat inflation as content** — the entire run build is a multiplier stack.
- **Content treadmill risk** — 25 weapons that are one weapon at 6 price points.
- **Currency with no role** — Gold and Gem have no in-run faucet and no pressure.
- **Interrupted flow** — hard pause every ~35 s for a choice that carries no information.

Not present (worth stating): no artificial retention gates, no energy system, no pay-to-progress wall.
