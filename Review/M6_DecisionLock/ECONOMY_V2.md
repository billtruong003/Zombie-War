# Delta B — Replacement economy: Coin · Gem · Blueprint · Relic

**Authority:** designed under owner decision **W6 — CHANGE** (2026-08-15). Replaces the
"freeze the legacy economy" recommendation in the M6.1 packet.
**Scope:** documentation and design only. No runtime code, no config edits, no UI changes.
**Companion:** `WEAPON_ONBOARDING_GATE.md` (tier ladder — Blueprint cost is keyed to it)

---

## 0. What the owner changed

> **W6 — CHANGE (owner, verbatim intent):** do not merely freeze the legacy economy. Design a
> **replacement** economy around **Coin + Gem + a weapon unlock resource / Blueprint + Relic
> collection**. Legacy **Gold / Weapon Shard / star upgrades / Gacha** remain in code but **lose
> design authority**.

The M6.1 recommendation was "stop designing against Gold and shards". That leaves a hole: nothing
then answers *what a run is for* beyond Coin. This delta fills the hole with four resources that each
answer a different question, and it removes every luck-based acquisition path.

---

## 1. The four resources

| Resource | The question it answers | Faucet | Sink | Random? | Secured on pickup |
|---|---|---|---|---|---|
| **Coin** | *"What do I spend right now?"* | kills, crates, containers, Greed Terminal | **in-run:** Supply Cache, Medical Station · **Hub:** common costume, consumable restocks | no | no — **25 % banked on death, 0 % on abandon** |
| **Gem** | *"What am I saving for that is beautiful?"* | elites, Boss Chest, golden stations | Hub: rare cosmetics, costume sets, Archive entries | no | **yes — exempt from death loss** |
| **Blueprint** | *"How do I get the next weapon?"* | **Boss Chest (guaranteed) + completed stations** — event-driven only | **weapon unlocks, and nothing else** | **no — deterministic** | **yes — exempt** |
| **Relic Fragment** | *"What do I still not have?"* | Boss Chest, Mythic Cache | Archive sets only — never stats | rare drop, with pity on **eligible sources** | **yes — exempt** |

Each resource has exactly one job. No two of them buy the same thing, which is the failure the legacy
economy had (Coin and Gold both being "the common currency").

---

## 2. Blueprint — the new resource, answered in full

The owner asked for a weapon unlock resource. Here it is, with the five questions that decide whether
it is a good idea answered directly rather than deferred.

### 2.1 Design

```text
Blueprint  — a single fungible resource. NOT per-weapon.
faucet     — Boss Chest: guaranteed award, every chest, every run
             Completed station (Signal Relay / Supply Cache / Boss Beacon): smaller award
             NEVER from time, never from idling, never from a random roll
sink       — weapon unlocks only. Cost is fixed per weapon and keyed to its tier band.
display    — the shop shows "Blueprint 14 / 30" against every locked weapon, from run one
```

### 2.2 Is it a non-luck path? — **Yes, and that is its entire justification**

Blueprint is **fungible**, not per-weapon. That single choice removes every luck mechanic at once:

- there are no duplicate Blueprints, so no duplicate-conversion rule is needed;
- there is nothing to roll, so no pity counter is needed;
- the distance to any weapon is a subtraction the player can do in their head.

**Rejected alternative — per-weapon blueprints** ("collect 30 AK-47 blueprints"). It reintroduces
exactly the thing the owner is removing: you get blueprints for guns you do not want, which needs a
conversion rule, which needs a pity rule, which is gacha wearing a different hat.

### 2.3 Does it scale with tier? — **Yes, and only by tier**

Cost is a function of the **measured** band from Delta A, so pricing cannot drift from power:

| Band | Weapons in band | Blueprint cost | Rationale |
|---|---:|---|---|
| **Common** | 13 | **lowest** | 13 weapons here; they must be cheap or the band is dead content |
| **Uncommon** | 5 | low–mid | includes both single-body families (SMG, Marksman) and the LMG |
| **Rare** | 4 | mid–high | |
| **Epic** | 2 | **highest** | only `FAMAS` and `G36C` |

**All numeric costs are `TUNING`** and are not approved by this document. What *is* fixed is the
shape: cost is monotonic in band, and the Common band must be reachable early enough that a player
sees a second weapon inside their first session (§2.5).

### 2.4 How does it coexist with Coin? — **Disjoint sinks, no shared decision**

> **Blueprint opens weapons. Coin buys everything else.**

A weapon costs Blueprint **only**. It does not also cost Coin. This is deliberate:

- If a weapon cost both, the player would face "save Coin for the gun or spend it on the Supply Cache
  mid-run" — which quietly punishes the in-run spending the Supply Cache exists to encourage.
- With disjoint sinks, Coin is always safe to spend, which is what makes an in-run Coin sink work at
  all.

**This obsoletes `WeaponData.price` as a Coin price.** `price` becomes the Blueprint unlock cost, or a
new `unlockCost` field is added and `price` is left for cosmetics. Either is a data-level change; the
migration note is in `WEAPON_ONBOARDING_GATE.md` §4.2 step 4.

### 2.5 What does the first hour feel like?

| Moment | State | Why |
|---|---|---|
| Run 1 | Starting weapon. Blueprint counter appears at run end with a visible non-zero number. | The resource is introduced by *earning* it, not by a tutorial. |
| Runs 2–3 | First **Common** weapon unlocked. | If the second weapon does not arrive inside the first session, the unlock system is invisible on the day it matters most. |
| Runs 4–8 | Two or three Commons owned; the player has felt two families. | Family identity is the actual hook; tier is not. |
| ~Run 10+ | First **Uncommon** in reach; the Rare band is a visible, countable target. | Long-term goal is a subtraction, never a hope. |

**This is a `HYPOTHESIS` about pacing, not a measured result.** The failure condition is explicit: if a
player finishes their first session still holding only the starting weapon, Common Blueprint costs are
too high.

### 2.6 Would I recommend against it? — **No, with one caution**

Recommended. It gives the run a second reason to exist that is not cosmetic, and being fungible it
carries no luck surface. The caution: **Blueprint must never become a second Coin.** The moment
anything other than a weapon costs Blueprint, the two currencies start competing for the same decision
and the design collapses back into the Coin/Gold problem this replaces.

---

## 3. Relic Fragment — collection only (W5 approved, art revised)

Unchanged in intent from W5, with the owner's art revision applied:

- collection-only: cosmetic sets, weapon skins, power VFX variants, Archive lore badges;
- **never grants raw stats** — RNG must not become a power wall;
- secured the instant it is picked up, exempt from death loss, never despawns in the active ring;
- pity counts **eligible sources** (Boss Chest, Mythic Cache), never time, so idling cannot farm it;
- duplicates convert to Gem, so no drop is dead loot.

**Art revision (owner):** Relic Fragment uses **2D / billboard art**, not a 3D authored model.
Cost drops accordingly — a billboard sprite with an emissive tint and an audio sting replaces a modelled
pickup. Revised cost: **LOW–MEDIUM**, dominated by the Archive screen and its save data rather than by
art. All drop rates remain `TUNING` and are not approved here.

---

## 4. Security on death

| Resource | On death | On manual abandon | Reason |
|---|---|---|---|
| **Coin** | **25 % banked** (existing `RunClosure.DefeatCoinFraction = 0.25f`) | **0 %** | Dying is a partial payout; quitting before danger pays nothing, or quitting becomes optimal |
| **Gem** | **100 %, secured at pickup** | 100 % | A rare drop that a later death deletes teaches "take no risks" |
| **Blueprint** | **100 %, secured at pickup** | 100 % | Progression toward a weapon must not be gambled on survival, or players stop opening Boss Chests |
| **Relic Fragment** | **100 %, secured at pickup** | 100 % | Same, more strongly — a lost Relic is invisible to the player |

The asymmetry is the point: **only Coin is at risk.** That keeps death meaningful without making any
long-term goal reversible.

---

## 5. UI surface

| Surface | Shows | When |
|---|---|---|
| Hub wallet | Coin, Gem | always |
| Weapon shop | Blueprint balance + `n / N` against every locked weapon | always — the target must be visible before it is reachable |
| Run-end settlement | Coin banked (with the 25 % / 0 % reason stated), Gem gained, Blueprint gained, Relic gained | every run end |
| Archive | Relic sets, progress per set | its own screen |
| In-run HUD | Coin only | Gem/Blueprint/Relic are secured and need no live counter |

**Owner-owned UI note:** every item above is a specification. **No UI prefab or scene is edited by this
pass**, and none may be edited without an explicit request.

---

## 6. The player decision this creates

Before: *"kill things, collect Coin, buy a gun eventually."* One resource, one verb.

After, at any Boss Beacon:

> Take the beacon and you risk a 25 % Coin payout you already earned, to gain **Blueprint** (progress
> toward a weapon you can name and count) and a **Relic** roll (progress toward a set you can see is
> incomplete). Skip it and you keep what you have.

That is a real decision with a stated cost, a stated gain, and no hidden probability on the part that
matters. **Blueprint is guaranteed; only the Relic roll is random** — so the risk is never "I got
nothing".

---

## 7. Legacy systems — in code, without design authority

**OWNER-LOCKED (W6).** These four remain in the repository, untouched, and are **not designed
against**. Nothing is deleted, hidden, or re-implemented.

| System | Code state | Design authority | Replaced by |
|---|---|---|---|
| **Gold** | `EconomyConfig` entries and wallet remain | **NONE** | Coin (common) + Blueprint (unlocks) |
| **Weapon Shard** | entries remain | **NONE** | Blueprint — fungible, deterministic, no duplicates |
| **Weapon star upgrades** | costs remain in `EconomyConfig`, unused | **NONE** | the tier ladder (Delta A) — power is authored, never grinded |
| **Gacha** | pool config remains, entry point not surfaced | **NONE** | direct Blueprint unlock — the explicit non-luck path |

> **"In code" is not design authority.** The presence of a config entry is not an argument for a
> system's existence. Equally, none of these is deleted: dormant is reversible, deletion is not.

**Language rule for every document:** these four are described as *"present in code, without design
authority"*. They are **not** described as "dormant pending approval", "recommended dormant", or
"awaiting an owner decision" — that decision has been taken.
