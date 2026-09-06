# M6.1 Steps 7–9 — Pickups, interactives, endless loop, economy, UI/WebGL, exact-25 migration

**Status:** PROPOSAL unless marked otherwise. No runtime change was made.
> **M6.2 DECISION LOCK (2026-08-15).** The owner has answered `W1`–`W7`. Where this M6.1 document
> says a decision is *recommended*, *proposed* or *awaiting approval*, the answer is now recorded in
> `Docs/M6_ENDLESS_RUN_SYSTEM_DESIGN.md` §1 and its deltas §1c–§1e. Measured numbers below are
> unchanged and remain valid; only decision status moved.


---

# Step 7 — Pickups

| ID | Status | Source | Drop rule | Rarity | Effect | Collection | Auto-collect | Secure on death | Destination | Feedback | Pooling |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `pickup.coin` | EXISTS AND RUNS | kills, crates, containers | value-merged, max 4 physical drops per kill | common | +Coin | magnet 3.5 m | **remove wave-clear CollectAll** | 25 % banked | run ledger → wallet | gold sparkle, chime | pooled (`pickup_coin`) |
| `pickup.gem` | EXISTS AND RUNS | elites, boss chest, golden station | `eliteGemChance` 0.5, amount 1 | rare | +Gem | magnet | no | **secured immediately** | wallet | violet beam, sting | pooled (`pickup_gem`) |
| `pickup.health` | EXISTS AND RUNS | kills (gated), crates | chance rises below HP thresholds, capped, with drought pity | uncommon | heal 15–20 % (TUNING; asset says 25 flat) | contact | no | n/a | Health | red cross pulse | pooled (`pickup_health`) |
| XP | EXISTS AND RUNS | kills | direct award, **no physical orb** | n/a | run XP | none | n/a | n/a | RunState | HUD particle only | none |
| `pickup.powercharge` | PROPOSED | crates, station rewards | low | uncommon | instantly refills one autonomous power cooldown | contact | no | n/a | active power | blue spark | pool |
| `pickup.magnet` | PROPOSED — LATER | rare crate | rare | temporary wide magnet | instant | n/a | n/a | n/a | blue ring | pool |
| `pickup.shield` | PROPOSED — LATER | rare crate | rare | temporary shield charge | contact | no | n/a | Kinetic Shield | cyan shimmer | pool |
| `pickup.skillreward` | PROPOSED | Signal Relay, Supply Cache | station-driven | n/a | opens a 1-of-3 offer | station | n/a | n/a | level-up UI | station VFX | none |
| `pickup.relic` | PROPOSED | Boss Chest, Mythic Cache | 2 % per eligible source + pity (TUNING) | very rare | collection fragment | magnet | **never despawns in the active ring** | **secured immediately** | Archive | gold shard + unique sting | pool |
| `pickup.bossreward` | PROPOSED | Boss Beacon | guaranteed on boss death | n/a | Boss Chest contents | station | n/a | mixed | as contents | chest VFX | none |
| `pickup.bomb` | **CUT** | — | — | — | — | — | — | — | — | — | art reused by `Ordnance Core` / `Emergency Detonation` |

**Explicit decisions.** XP stays direct (physical orbs would force backtracking and fight the endless
run's forward pressure). Health drop chance adapts to low HP with a cap plus a drought counter. Gem and
Relic are secured on pickup. Rare pickups never despawn inside the active 5×5 ring.
`PickupManager.cs:141` currently calls `CollectAll()` on `WaveClearedEvent` — **this must be removed**;
an endless world has no reliable wave boundary.

---

# Step 7b — World interactives

All six share one grammar: `Signal + Trigger + Cost/Risk + Completion + Reward + Persistence`.

| Field | Signal Relay | Supply Cache | Boss Beacon | Medical Station | Greed Terminal | Breakable Barrel/Crate |
|---|---|---|---|---|---|---|
| **Status** | PROPOSED (MUST) | PROPOSED (MUST) | PROPOSED (MUST) | PROPOSED (SHOULD) | PROPOSED (SHOULD) | **EXISTS AND RUNS** |
| **Deterministic spawn** | global cell `worldSeed+cell+archetype+slot` | same | same, rarer | same | same | decoration-adjacent, existing |
| **Ownership** | anchor cell; gameplay entity, never baked into decoration mesh | same | same | same | same | existing prop |
| **Visual body** | **NOT PROVEN** — `Lamp1` too thin | `Container1` (blue) — **evidenced credible** | **NOT PROVEN** | `Container2` (red) — candidate only | `Container4` (orange) — candidate only | `PROP_Crate_Loot`, `PROP_Barrel_Fuel` |
| **Required authored VFX** | ground ring + vertical beam + progress arc | marker + open anim | tall beam + boss-type icon + tier badge | cross icon + ring | risk icon + numeric plate | none (exists) |
| **HUD signal** | 1 pin or compass tick | compass tick | **largest** icon; shows boss type + reward tier before activation | compass tick | compass tick | none |
| **Interaction** | enter and hold | pay Coin, or short hold if broke | enter + confirm (single context action) | pay Coin or stand still | activate + confirm | shoot it |
| **Duration** | charge bar (TUNING) | instant on payment | until boss dies | ~2 s | instant | instant |
| **Cancel** | leaving decays charge slowly — **never resets** | walk away | cannot cancel once spawned | walk away | cannot undo | n/a |
| **Risk/reward** | position lock while pressure continues → 1-of-3 card | Coin → Supply Chest / reroll | boss fight → Boss Chest (Legendary Core, Gem, Relic roll) | Coin/time → 40–50 % heal | permanent +Threat → Coin ×1.25 and reward tier +1 | small damage risk → Coin/heal/Power Charge |
| **Repeatability** | once → `exhausted`, visually dimmed | once | once | once per stable ID | once | once |
| **Persistence** | `untouched→active→completed→exhausted` | `untouched→completed` | `+spawned→defeated→chestClaimed` | `untouched→used` | `untouched→used` | destroyed |
| **Enemy pressure** | spawns continue and rise while charging | unchanged | boss **chases**; no arena walls | unchanged | rises permanently | unchanged |
| **Chunk recycle** | never resets a used station | same | leaving the active ring mid-fight despawns the boss and resets the beacon to `untouched` — no orphan boss, no free chest | same | same | n/a |
| **Performance** | 1 zone check/frame | trivial | one boss + existing pooling | trivial | trivial | existing |
| **Failure** | player leaves permanently → partial charge retained | none (skip only) | player dies → run ends normally | none | none | none |
| **Tests** | charge decays not resets; survives chunk recycle | pays once; Coin deducted once | no duplicate spawn; no free chest on unload | one use per ID | Threat persists for the run | existing |

**Boss Beacon uses existing assets only:** bosses `CactusBoss` (1400 HP), `MoleRatKing` (1100),
`SkeletonGiant` (900, elite); elites `DogBowwow`, `SkeletonMage`, `CatLightning`. No new boss is authored.

> A container or lamp alone does **not** communicate station function. Relay and Beacon need the
> authored visual language above before they can be considered designed.

---

# Step 7c — The endless run

```text
Hub (choose ONE weapon + outfit)
  → insert into Map_Level1
  → move + auto-fire under rising Threat
  → level-up 1-of-3 (≤30 s, auto-pick on timeout)
  → chase signals: Relay / Cache / Scanner / Medical / Greed
  → optional Boss Beacon
  → death (bank 25 % Coin) OR manual abandon (bank 0 %, confirmed)
  → settlement: time survived · kills/bosses · final build · Coin earned vs banked · Gem/Relic secured
  → Hub: unlock, collect, restyle
  → repeat
```

**No Victory state.** Threat rises from time + distance + activations, and changes **composition before
HP multipliers**.

## Enemy role coverage — all 16 assets accounted for

| Archetype | Assets | Production use today | M6 role |
|---|---|---|---|
| Walker | DogPup, CatMeow, Skeleton, **Zombie** | 3 used, Zombie unused | baseline pressure at all Threat levels |
| Runner | DogBark, **CatBolt**, **CatLightning**, **DogBowwow** | 1 used, 3 unused | punish stationary builds; enter at Threat 1 |
| Ranged | **Cacti**, **Cactus**, **SkeletonMage** | **all 3 unused** | the answer to Shotgun/close builds; enter at Threat 2 |
| Burrower | **Burrow**, **MoleRat** | both unused | punish hold-zones and LMG anchoring; enter at Threat 2 |
| Heavy (elite) | **SkeletonGiant** | unused | Boss Beacon / Threat 3 |
| Boss | **CactusBoss**, **MoleRatKing** | unused | Boss Beacon only |

**11 of 16 enemy assets are unused in production.** Composition-based difficulty is therefore available
immediately without new art — which is exactly what avoids HP-multiplication.

---

# Step 8 — Economy and outfit boundaries (preserved, not re-decided)

| Resource | Status |
|---|---|
| Coin | **OWNER-LOCKED** primary common currency; in-run sinks (Cache, Medical) + Hub purchases |
| Gem | rare, **secured on pickup**, cosmetic/collection only |
| **Blueprint** | **OWNER-LOCKED (W6)** — new fungible weapon unlock resource; deterministic, no duplicates, no pity. Weapons cost Blueprint only. All costs `TUNING` |
| Gold | **IN CODE, NO DESIGN AUTHORITY (W6)** — no in-run faucet, no decision Coin does not already make |
| Weapon Shard | **IN CODE, NO DESIGN AUTHORITY (W6)** — replaced by Blueprint |
| Weapon star upgrades | **IN CODE, NO DESIGN AUTHORITY (W6)** — replaced by the authored tier ladder |
| Gacha | **IN CODE, NO DESIGN AUTHORITY (W6)** — replaced by the deterministic Blueprint unlock |
| Relic Fragment | **OWNER-LOCKED (W5)**; collection only, 2D/billboard art, no locked rates, never raw stats |
| Death banking | **25 % Coin** |
| Manual abandon | **0 % Coin**, confirmed; not a victory |

**Outfit:** H2 measured **0 % hard conflicts and 73 % thematic coherence** over 300 seeded outfits. It
is **APPROVED FOR PRODUCTION (W7, 2026-08-15)** under the owner's gate — 0 % hard conflicts mandatory,
~70 %+ coherence acceptable. The earlier 85 % bar was mine and is **superseded by owner decision**.
Costume metadata authoring over 453 items and the post-authoring re-test are still outstanding. The
300-outfit audit is **not** repeated here.

---

# Step 9a — UI / VFX / audio / WebGL guardrails

| Surface | Rule |
|---|---|
| Weapon presentation | family badge + variant name; variants never inflate the family list |
| Skill card | icon + one-sentence short text + rank pips; long text only on inspect |
| Rank display | filled pips, never a number the player must decode |
| Level-up countdown | visible ring, ≤30 s unscaled, highlighted card auto-picked |
| Power charge/cooldown | one ring per active power, max 3 concurrent |
| Signal compass | **1 pin + ≤3 ticks**, never more |
| Pickup rarity | colour + silhouette + audio sting; never a text tooltip mid-combat |
| Station progress | on the station in world space, not in a HUD corner |

| Budget | Guardrail |
|---|---|
| Fire rate | soft cap 2.2×, hard cap 2.5× base |
| Tracers | pooled; cap concurrent tracers; no per-shot allocation |
| Impacts | pooled; cap concurrent impact VFX |
| Lightning arcs | ≤6 per proc, ≤2 procs/s across all sources |
| Explosions | ≤2 concurrent; reuse the existing pooled `Bomb` |
| Physics queries | ≤1 `OverlapSphere` per power proc; clustering ≤1/s over ≤64 enemies |
| Rapid-fire audio | hard voice cap; the SMG at fire-rate cap is the worst case |
| Materials | zero runtime material instances (already the character contract) |
| Allocations | 0 bytes/frame steady state for skills and powers |
| WebGL | no new shader variants per weapon; MW4 bodies are 2–3× the poly count of project weapons — onboard selectively |

---

# Step 9b — Exact-25 assumptions and migration

## Inventory of every place the roster is enumerated (FACT)

| Location | Assumption | Risk at weapon 26+ |
|---|---|---|
| `Assets/_Project/Data/Weapons` folder scan (`FindAssets("t:WeaponData")`) | folder **is** the roster | LOW — grows naturally |
| `Editor/SceneFlowBuilder.cs:182` | scans the folder to populate a scene | MEDIUM — rebuild needed |
| `Editor/LoadoutMenuInstaller.cs:32` | scans the folder into `ctrl.weapons` | MEDIUM — a serialized array in a scene/prefab |
| `Editor/DevProfileTools.cs:45` | button literally says **"Unlock all 25 weapons"** | LOW (cosmetic) but a documented hard-coded 25 |
| `Editor/DevProfileTools.cs:86` + `Runtime/Dev/ZombieWarCheatPanel.cs:219` | `UnlockAllWeaponsForDev(weapons)` | LOW |
| `Editor/CombatPowerAuditWindow.cs:33` | scans all `WeaponData` | LOW |
| `Editor/Audio/ZombieWarCuratedAudioBuilder.cs:101,454` | per-weapon audio entries | **HIGH** — every new weapon needs an audio key or the build breaks |
| `Editor/Audio/ZombieWarAddressableAudioCatalogBuilder.cs:121,161` | per-weapon addressable entries | **HIGH** — same |
| `Runtime/Systems/GachaService.cs` | pool membership | dormant, but must not silently include new weapons |
| `Runtime/Systems/PlayerProfile.cs` | ownership, slots, starter seeding by `CatalogOrder` | **HIGH** — save identity |
| `Runtime/Systems/LoadoutState.cs` | 3 weapon slots | M6 uses slot 0 only; slots 1–2 dormant |
| `Runtime/Systems/CombatPower.cs` | power scoring | LOW |
| Shop / Armory UI | list building from the folder scan | MEDIUM |
| Thumbnail generation | one icon per weapon | MEDIUM — must be generated, not hand-placed |

## Proposed authoritative `WeaponCatalog`

```text
WeaponCatalog (one ScriptableObject)
  entries[]  { weaponId, family, variantGroupId, baseWeaponId, data, catalogOrder, unlockMethod }
  ↓ single source consumed by
  Player equip · Hub/Armory list · Shop · thumbnail generation · cheat panel
  · save resolution · audio catalog builders · validation
```

Rules:

- **`weaponId` is save identity.** It never changes and never gets reused.
- `catalogOrder` is **presentation only** and may be reordered freely.
- Adding weapon 26 must require **one catalog entry and nothing else** — no consumer edits.
- **Variants do not inflate the family count**: UI groups by `variantGroupId`.
- Audio and addressable builders must derive from the catalog, not from folder scans, so a missing
  audio key fails loudly at build time instead of at runtime.

**Future impact-analysis targets (named, not edited):** `PlayerProfile`, `LoadoutState`,
`GachaService`, `CombatPower`, `SceneFlowBuilder`, `LoadoutMenuInstaller`, `DevProfileTools`,
`ZombieWarCheatPanel`, both audio builders, and the Shop/Armory screens.
