# M6.0 — Current-State Audit

**Phase:** CONCEIVE — investigation only. Nothing here is implemented or promoted to production.
**Skill applied:** `game-designer` (extracted read-only from `game-designer.skill`).
**Evidence:** repository code and production data read directly; runtime traced in a real play session
`Bootstrap → Hub → PLAY → Map_Level1`.

Facts are labelled. `FACT` = read from repository/production data. `ASSUMPTION` = my number, stated as
mine. `HYPOTHESIS` = proposed, unverified.

---

## 0. Repository baseline

```text
HEAD            1996a7aa  "Configure portrait WebGL builds with cheats"
dirty files     2591      (pre-existing; the Docs/* deletions are an earlier reorganisation, NOT mine)
staged          0
```

This phase created only `Review/M6_Conceive/**`. No production file was modified.

---

## 1. The game that actually exists

### 1.1 Traced production path

```text
Bootstrap ──► Menu (Hub) ──► PlayButton ──► Map_Level1
   │              │                              │
   │              └─ MenuCharacterStage preview   ├─ WaveDirector: 5 authored waves
   │                 (real catalog, fallback light)│
   │                                              ├─ auto-aim + continuous fire (no reload)
   │                                              ├─ kill → XP + Coin
   │                                              ├─ level-up → timeScale 0 → 1-of-3 perk
   │                                              ├─ all waves cleared → Victory
   │                                              └─ player dies → Defeat
   └─────────────────────── HOME ◄── RunClosure banks currency ◄──┘
```

Verified live: player spawned, 22 concurrent enemies, level-up modal appeared and paused the game,
perk applied, run closed, currency banked, returned to Hub. Two full runs completed.

### 1.2 System-by-system

| System | Intended design | Actual implementation | Production data | Mismatch | Consequence |
|---|---|---|---|---|---|
| Session start | Hub → choose **contract** + loadout | Hub → single PLAY button → `Map_Level1` | 1 campaign catalog | No contract selection exists | The GDD's route/loadout decision never happens |
| Movement | Joystick, mobile survivor | `PlayerMovement`, twin-stick decoupled aim | — | none | Works |
| Aim / fire | Auto-aim, **no reload cycle** | Auto-target, continuous fire; M4 removed the ammo/reload ledger | — | none in code | Works |
| Weapon families | Each family answers a different combat question | 6 classes; differ only by damage/fireRate/range/pellets | 25 `WeaponData` | **`buildTag`="generalist" on all 25, `roleTag` empty, `buildHint` empty, `chainCount`=0, `chargeTime`=0** | Families are numerically different, not tactically different |
| Weapon resource | No magazine | `resourceModel` = `Magazine` on all 25 | 25 assets | **Dead data** — no runtime consumer since M4 | Schema lies about the design |
| Enemy roster | Composition-driven threat | `ZombieSpawner` + pooled variants | 16 `ZombieData` (13 normal + 3 boss) | Roster is rich; only 5 types used in `WD_Level1` | Most of the roster is unreachable in production |
| XP | Kills + POI completion | `RunState.AddXp`, kills only | `xpReward` 1–4 (35 boss) | No POI exists | XP is a kill counter |
| Level-up | First choice at 90–150 s, then widening | `XpForNextLevel = 10 + (Level-1)*8`; pauses game | — | **Far too fast** (§4) | Combat is interrupted ~8× per run |
| Perks | Behaviour-changing run build | `RunPerkPool` 7 entries, 5 kinds, 1-of-3 draw | 7 perks, all pure numbers | **No behavioural perk exists** | No build identity; see skill audit |
| Perk application | — | **All 5 kinds are consumed** (Damage, FireRate, MoveSpeed, CoinGain continuously; MaxHealth at pick time) | — | none | ⚠️ `SKILL_SYSTEM_DESIGN.md §1` still says perks are not applied — **that PROJECT FACT is stale** |
| Waves | Bounded expedition, 2 objectives, extract-or-boss | `WaveDirector` runs 5 authored waves then Victory | `WD_Level1`: 5 waves, 244 enemies | **Wave survival, not an expedition** | The entire GDD session structure is unimplemented |
| Pickups | Coin/heal/bomb | `Pickup`, `PickupManager`, `DestructibleProp` (LootCrate) | present | — | Implemented and used |
| Coin | Common upgrade currency | Banked per kill and via pickups, `CoinGain` perk scales at one ledger point | `coinReward` 1–4 | Income is tiny vs. profile balance | Economy has no felt pressure (§4) |
| Gold / Gem | Rare / cosmetic lanes | Fields exist in `RunState`, banked on victory | No production enemy drops Gold or Gem | **Currencies with no faucet in the run** | Two of three currencies are inert during gameplay |
| Victory | Extraction or boss | `AllWavesCleared` → Victory | — | No extraction, no boss in `WD_Level1` | Victory = "survived 5 authored waves" |
| Defeat | Bank 25% common | `RunClosure.DefeatCoinFraction = 0.25`, rare dropped | — | none | Matches GDD §11 |
| Meta progression | Contract tiers, unlocks | `PlayerProfile`, weapon levels, first-clear rewards, gacha, costume commerce | 1 campaign entry | Breadth exists, depth unreachable | Meta backend outruns the loop |
| Outfit selection | — | `CostumeScreen.RandomizeCasual()` | 453 parts | **Uniform random per slot, flat 50% "none"** | This *is* hypothesis H0; see outfit audit |
| World streaming | Deterministic endless world | Implemented and shipping (M4/M5) | — | none | The world exists; nothing uses it for content |

### 1.3 Implementation status classification

| Status | Systems |
|---|---|
| **Implemented and used** | movement, auto-fire, wave spawning, XP, level-up + perks, pickups, coin, run closure, profile, costume equip/randomize, world streaming, contact shadows, outline |
| **Implemented but not wired to the loop** | Gold/Gem (no in-run faucet), 3 boss `ZombieData`, 11 of 16 enemy types, `DestructibleProp` loot crates (no placement rule), gacha/battle-pass/missions backend |
| **Data exists but unreachable** | `Map_Level2–5` scenes + `WD_Level2–5`, `resourceModel`/`heatCapacity`/`chargeTime`/`chainCount` weapon fields, `roleTag`/`buildHint`/`buildTag` |
| **Placeholder / scaffolding** | `RunPerkPool` (7 numeric perks — the GDD itself calls this scaffolding) |
| **Document-only (never implemented)** | expedition contracts, POIs, objectives, extraction, boss objective, threat tiers, spatial bands, biome content weighting, arsenal link states (Exposed/Staggered/Pinned/Momentum) |
| **Iceboxed** | food buffs, horde difficulty spec, in-run skill/stat design, toon depth AO/outline decision, dedicated character metal (cut at M5+) |

### 1.4 The central contradiction

`Docs/GAME_DESIGN.md` is a **CONCEIVE-phase document** ("concept locked, implementation not started")
describing an *expedition shooter*: objective-led travel, two field POIs, extract-or-boss climax,
8–12 minute bounded session, threat tiers, contracts.

`Map_Level1` is a **wave-survival arena**: 5 authored waves, 244 enemies, no objectives, no
destination, no extraction, no boss, no route decision.

The endless procedural world was built (M4/M5) and works — **but nothing in gameplay consumes it as
content**. The player fights waves on top of a streaming world that could be anywhere. Pillar 1 of the
GDD ("Move with purpose") has no implementation at all: there is no reason to walk in any direction.

> **P0.** The gap between design and build is not a missing feature list — it is the entire session
> structure. Every downstream system (skills, economy, difficulty, POIs) is specified against a loop
> that does not exist yet.

---

## 2. Quantified session model

Derived from production data. Enemy/XP/coin values are `FACT`; timings are `ASSUMPTION`.

### 2.1 `WD_Level1` full clear — FACT

| Wave | Composition | Enemies | Enemy HP | XP | Coin |
|---|---|---:|---:|---:|---:|
| 1 Scouting | DogPup ×30 | 30 | 1 200 | 30 | 30 |
| 2 Small Pack | DogPup ×24, CatMeow ×16 | 40 | 1 680 | 40 | 40 |
| 3 Dry Bones | CatMeow ×24, Skeleton ×24 | 48 | 2 520 | 72 | 72 |
| 4 Thickening | DogPup ×24, Skeleton ×32 | 56 | 2 880 | 88 | 88 |
| 5 Runners | Skeleton ×40, DogBark ×12, DogPup ×18 | 70 | 3 780 | 122 | 122 |
| **Total** | | **244** | **12 060** | **352** | **352** |

### 2.2 Level-up cadence — FACT (curve) + ASSUMPTION (timing)

`XpForNextLevel = 10 + (Level-1) × 8` — cumulative 10, 28, 54, 88, 130, 180, 238, 304, 378.

352 XP therefore reaches **Level 9 ⇒ 8 level-ups per full clear**.

- First level-up costs **10 XP = 10 DogPup kills**. In wave 1 (`spawnInterval` 0.35 s, `initialBurst` 4)
  that is roughly **15–25 seconds in**. — ASSUMPTION on timing.
- GDD §10 target: first meaningful choice at **90–150 s**. Actual is **~5× too early**.
- 8 level-ups across an estimated 4–6 minute run = a full-screen `timeScale = 0` interruption every
  ~35 s. — ASSUMPTION on run length; kill count and level count are FACT.

> **P1.** The level-up cadence interrupts combat far more often than the design intends, and each
> interruption is a hard pause, not a flow-preserving choice.

### 2.3 Time-to-kill — FACT

| Weapon | Single-target DPS | TTK vs DogPup (40) | vs Skeleton (60) | vs Boss (1 400) |
|---|---:|---:|---:|---:|
| AR G36C (Epic) | 240 | 0.17 s | 0.25 s | 5.8 s |
| AR Generic (Common) | 144 | 0.28 s | 0.42 s | 9.7 s |
| SMG Generic | 112 | 0.36 s | 0.54 s | 12.5 s |
| Shotgun AA12 (8 pellets) | 192 | 0.21 s | 0.31 s | 7.3 s |
| Sniper Generic | 81 | 0.49 s (1 shot) | 1.1 s | 17.3 s |
| Sidearm Glock19 | 60 | 0.67 s | 1.0 s | 23.3 s |

Total enemy HP 12 060 ÷ 240 DPS ≈ **50 seconds of continuous fire** to clear the whole run. The rest
of the run duration is travel, spawn delay and pauses.

> **P2.** Weapon choice is a pure DPS ladder: G36C strictly dominates every other weapon in the game
> (240 DPS vs 48–220), and rarity correlates directly with DPS. There is no tactical reason to ever
> switch weapons — which makes "swap" (a pillar of the stated combat grammar) mechanically pointless.

### 2.4 Economy — FACT + ASSUMPTION

| Value | Per full clear | Note |
|---|---:|---|
| Coin earned | 352 | FACT, before `CoinGain` perk |
| Coin with 1× CoinGain perk | 440 | FACT (×1.25) |
| Gold earned | 0 | FACT — no production enemy drops Gold |
| Gem earned | 0 | FACT — no production enemy drops Gem |
| Banked on victory | 100 % | FACT |
| Banked on defeat | 25 % coin, 0 rare | FACT |

Observed Hub balance in the live session: **45.3 K Coin, 0 Gem**. — ASSUMPTION: accumulated through
testing/cheats rather than play. Either way, **one run returns ~0.8 % of the standing balance**, so
the coin faucet currently has no felt value and the defeat penalty (25 %) risks nothing meaningful.

> **P1.** Two of three currencies have **no faucet inside a run**, and the third pays an amount too
> small to matter against the existing balance. There is no sink pressure and therefore no economic
> reason to play another run.

---

## 3. Predicted player optimisation

What an optimising player actually does with the current build, not what the design hopes:

1. **Equip the highest-DPS unlocked weapon and never switch.** Switching costs time and gains nothing.
2. **Take Damage and Fire Rate perks every time they appear**; they multiply and nothing else affects
   clear speed. Move Speed is the only defensible alternative (it avoids damage); Max Health and Coin
   are strictly worse in a run you expect to win.
3. **Stand near the spawn ring and hold position.** Nothing rewards movement — no POI, no objective,
   no resource gradient — so kiting is only a survival tool, never an opportunity.
4. **Ignore the Hub economy.** Coin income is irrelevant to the balance; costume is cosmetic.

The resulting run is: hold a corner, auto-fire, tap Damage/Fire-Rate eight times, win.

> **P0 (dominant strategy).** Damage → Fire Rate is strictly dominant, and both stack multiplicatively
> with no cap. There is exactly one build.

---

## 4. Findings ranked

| # | Priority | Finding | Evidence |
|---|---|---|---|
| 1 | **P0** | The designed session structure (expedition/POI/extraction) is entirely unimplemented; the game is wave survival | `WD_Level1`, `WaveDirector`, `RunDirector` |
| 2 | **P0** | Damage/Fire-Rate is a dominant strategy; only one build exists | `RunPerkPool`, `Weapon.cs:650`, `Weapon.cs:120` |
| 3 | **P0** | Weapon families have no authored identity — all 25 tagged `generalist` | 25 `WeaponData` assets |
| 4 | **P1** | Level-up cadence ~5× too fast and pauses the game each time | `XpForNextLevel`, `RunOverlays.TryShowLevelUp` |
| 5 | **P1** | Gold and Gem have no in-run faucet; Coin income is negligible | `ZombieData` (16), `RunState` |
| 6 | **P1** | Nothing in the loop rewards movement, so the procedural world is decorative | no POI/objective system exists |
| 7 | **P1** | Production outfit randomizer is uniform random with no compatibility rules | `CostumeScreen.RandomizeCasual` |
| 8 | **P2** | 11 of 16 enemy types and all 3 bosses are unreachable in production | `WD_Level1` uses 5 types |
| 9 | **P2** | `resourceModel`/`heatCapacity`/`chargeTime`/`chainCount` are dead weapon fields | `Weapon.cs:417` comment, data |
| 10 | **P2** | `SKILL_SYSTEM_DESIGN.md §1` states perks are not applied — stale since M5 | `RunOverlays.PickPerk:365` |
| 11 | **P3** | `Map_Level2–5` + `WD_Level2–5` remain on disk unreachable | Build Settings |

---

## 5. What is genuinely good and must be kept

The critique discipline requires naming what works, not only what fails:

- **Run closure is correct and idempotent.** First-outcome-wins, single payout, defeat fraction
  applied at one place. This is the kind of thing that is expensive to retrofit; it is already right.
- **Perk application is honest.** One ledger point for Coin, multiplicative stacking, and `MaxHealth`
  correctly applied at pick time because it has no continuous consumer.
- **Weapon/enemy data schemas are richer than the current content uses.** `pierceCount`, `chainCount`,
  `heatCapacity`, `chargeTime`, `roleTag`, `buildTag` already exist — weapon identity is an
  **authoring** problem, not an engineering one. That is a large cost saving for M6.
- **The streaming world, contact shadows, outline and character materials are production quality** and
  need no further work to support M6 design.
- **16 enemy types with distinct speeds/HP/damage** are ready to carry a composition-based difficulty
  model as soon as something selects them.
