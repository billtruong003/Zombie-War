# Zombie War — Current Game Design Audit

**Audit date:** 2026-08-09  
**Project phase:** SHIP  
**Purpose:** Reconstruct the complete game design that exists in the current repository, separating playable reality from intended or deferred design.  
**Authority rule:** Current source and serialized data win over older documents. `DESIGN_BRIEF.md`, `MVP_SHIP_PLAN.md`, and the canonical files under `Reference/Design/` define intent only where implementation does not contradict them.

---

## 1. Executive summary

Zombie War is a portrait, one-hand, top-down mobile survivor shooter. The player directly controls movement, bomb timing, and weapon switching; aiming, firing, and reloading are automatic. The game is built around short linear campaign stages in which a mobile survivor fights authored waves of stylized zombie-like monsters, earns temporary XP, collects currency, and returns to a hub to strengthen and customize an arsenal.

The shipped fantasy is not intended to be an idle autobattler. Its current product hypothesis is that low input count can still produce agency when the player reads an enemy problem, creates an advantage with one weapon, deliberately swaps, and cashes that advantage out with another:

`READ → SET UP → SWAP → CASH OUT → RESET`

That sequence is an **unproven hook hypothesis**, not a currently complete mechanic. The playable combat foundation is strong enough to produce motion, density, gun feedback, enemy telegraphs, and short-term pressure, but most weapon assets are still general-purpose stat ladders. Shared states such as Exposed, Staggered, Pinned, and Momentum are candidates rather than implemented systems.

The project already contains unusually broad meta infrastructure: five campaign stages, 25 weapons, 15 active enemy definitions, three currencies, weapon ownership and upgrades, two gacha pools, 448 costume items, 30 outfit sets, missions, a free-pass structure, and persistent save data. This breadth is ahead of the proven combat depth. The release-critical gap is not more content; it is closing the player-facing run loop and proving that weapon choice produces meaningful decisions.

### Current design verdict

| Area | Status | Meaning |
|---|---|---|
| Product fantasy and three-input control | **LOCKED** | Stable foundation of the game |
| Basic combat, waves, enemies, drops | **IMPLEMENTED** | Playable in current source/data |
| Campaign selection and run closure | **IMPLEMENTED / needs full manual verification** | Backend and selector exist; current working tree is newer than prior audit |
| Weapon family identities | **PROVISIONAL** | Direction exists, assets mostly do not express it yet |
| In-run XP/perks | **PARTIAL / player-facing stub** | XP arithmetic exists; choice/application flow does not |
| Results presentation | **PARTIAL** | Run summary exists; overlays do not display the full result |
| FTUE | **PARTIAL** | Joystick overlay exists; complete onboarding sequence does not |
| Revive and haptics | **STUB** | Presentation/settings exist without real system behavior |
| Economy, collection, costume, gacha | **IMPLEMENTED backend, provisional balance** | Broad persistent systems exist; tuning is not validated |
| Battle Pass | **PARTIAL** | Mission catalog/progress backend exists; full seasonal product loop is not proven |
| Audio | **IN TRANSITION** | Runtime hooks/catalog exist; content and working tree are actively changing |
| Combat hook | **NEEDS EVIDENCE** | Must be proven by uncoached playtest |

---

## 2. Concept lock

### Core fantasy

The player is a highly mobile survivor directing an arsenal against a screen full of colorful, cartoon-styled monsters. The gun handles aiming and firing; the player handles survival, spacing, emergency tools, and weapon choice.

The desired player statement is:

> “I have very few buttons, but each decision clearly changes the rhythm and outcome of the fight.”

### Core loop

`Choose stage and loadout → survive authored waves → gain temporary power → defeat the final pressure/boss → receive rewards → improve arsenal → unlock or replay a stage`

### Three pillars

1. **Readable, forceful gun feel.** Shots, hits, deaths, recoil, motion, and sound must clearly communicate power and consequence.
2. **One-hand clarity.** A new player should understand the controls within seconds: move, bomb, switch.
3. **Visible short-term progression.** The player should see growth during the run and a useful reward after it.

### Anti-pillars

Zombie War is not a manual-aim shooter, open world, PvP game, procedural endless roguelike, heavy live service, deep narrative RPG, or ability-bar action game. It is also not intended to be a passive “run in circles while the highest DPS gun solves everything” experience.

### Audience

- **Primary — Active Survivor Player:** wants survivor-like spectacle and accessibility with more tactical agency than pure movement-only play.
- **Secondary — Arsenal Collector:** values acquiring, upgrading, displaying, and comparing a broad weapon collection.
- **Secondary — Short-session Mastery Player:** wants three-to-eight-minute sessions with readable mistakes and improvement between attempts.

---

## 3. Player-facing session

### Intended session length

Campaign stages target approximately **3–8 minutes**. Actual duration depends on authored wave counts, spawn pressure, player power, and whether pauses/perk interruptions are later completed.

### Session entry

1. Bootstrap initializes persistent services.
2. Menu/Hub loads additively.
3. Player chooses an available campaign stage.
4. Player enters loadout or launches PLAY.
5. The selected map loads additively and becomes the active scene.
6. A fresh `RunState` begins with the stage identity.

### During the run

- Move with the virtual joystick.
- Character automatically finds a valid nearby target, rotates, and fires while the target is in the equipped weapon's range.
- Switch through occupied weapon slots with one button.
- Throw a bomb when a charge and cooldown are available.
- Survive authored wave compositions; collect Coin/Gem pickups through proximity magnetism.
- Gain XP directly from kills.
- Reach a terminal Victory after all waves clear or Defeat after player death.

### Run exit

- A terminal run freezes a summary containing outcome, kills, highest wave, level, remaining XP, Coin, Gold, Gem, and duration.
- Earned run currency is paid to the persistent profile exactly once.
- Victory may mark stage completion and grant a first-clear reward once.
- Replay reloads the active stage. Home abandons the in-memory run and returns to the Hub.

The backend closure is implemented. The result overlays still lack the complete numeric presentation needed for a finished player experience.

---

## 4. Controls and player agency

### Locked input set

| Input | Player verb | Current behavior |
|---|---|---|
| Virtual joystick | Move / position / kite | Direct movement; base authored speed is 5 m/s before modifiers |
| Bomb button | Emergency AoE / reset pressure | Throws in aim/facing direction; charge-limited and cooldown-limited |
| Weapon switch button | Change combat tool | Cycles occupied slots; no direct three-button selection |

There is no fire button, reload button, manual aim stick, dodge button, or active skill bar.

### Current sources of agency

- Spacing and movement path.
- Which enemies enter the auto-target/search radius.
- Bomb timing and self-risk.
- Pre-run weapon ownership, upgrade level, and loadout.
- Mid-run weapon cycling.
- Future perk decisions, once the player-facing loop is connected.

### Current agency limits

- Target selection is automatic and mostly nearest-target driven.
- One switch button may require cycling past an unwanted weapon.
- Existing weapon data often differs by sustained DPS rather than tactical purpose.
- Props are damageable but not part of the main target registry, making deliberate barrel/crate play unreliable.
- Perks currently do not provide a working choice loop.

---

## 5. Player combat model

### Movement and aiming

- Base movement speed: **5**.
- General aim/search range: **11**.
- Auto-targeting uses target stickiness, a minimum dwell time, periodic recomputation, and a danger-radius override.
- Current authored defaults include approximately 0.75 aim stickiness, 0.2-second minimum dwell, 0.1-second recomputation, and 7-unit danger radius.
- Body rotation is smoothed; aim direction is stabilized to reduce rapid target jitter.

### Health and death

- Base health component defaults to **100 HP**.
- Enemy contact, special actions, projectiles, explosive props, and bombs can damage `IDamageable` targets.
- Death disables configured player behaviors, plays death presentation, and emits Game Over after a delay (default 2 seconds).
- A revive overlay exists but is presentation-only; there is no complete revive economy/ad/health restore loop.

### Bomb

- Maximum charges: **3** in the current thrower default.
- Cooldown: **3 seconds**.
- Throw speed: 9 horizontal / 4.5 upward; release delay 0.3 seconds.
- Fuse: **1.5 seconds**.
- Explosion: **4-unit radius**, **80 damage**, camera shake 0.6.
- Damage uses the general damage interface, so friendly/self damage is possible depending on layer configuration.
- Bomb pickups now subscribe into the thrower and add one charge, clamped to maximum.

Design role: an emergency reset and crowd-shaping tool, not a second primary build system.

---

## 6. Weapon system

### Loadout contract

- **Slot 0:** mandatory one-handed pistol.
- **Slots 1–2:** optional two-handed long guns.
- One control cycles occupied slots.
- The profile persists slot choices; the player spawner applies them when the run begins.
- Each slot now preserves magazine and reload state while unequipped. Switching is not intended to refill a weapon for free.

### Shared weapon behavior

- Auto-fire when a valid target is within the current weapon's range.
- Infinite reserve ammunition with magazine/reload cadence.
- Automatic reload when the magazine is empty.
- Permanent weapon star level modifies damage and fire rate.
- Hitscan is the authored default for all current weapon assets.
- Runtime also supports multi-pellet cones, piercing lines, distance falloff, knockback, muzzle flash, tracer, smoke trail, audio, camera shake, and three-axis recoil spring.
- Shot impact can produce damage numbers, material hit flash, hit reaction, knockback, and death dissolve.

### Current arsenal

There are **25 authored weapons** across six families:

| Family | Count | Current reality | Intended identity |
|---|---:|---|---|
| Pistol / sidearm | 11 | Broad stat ladder, mandatory baseline slot | Reliable emergency recovery and clean execution |
| SMG | 1 | General-purpose automatic gun | Mobile close-pressure and rapid shred |
| Assault rifle | 6 | Generally the strongest sustained DPS group | Stable all-range setup and armor pressure |
| Shotgun | 6 | Pellet count/cone creates most differentiation | Close-range displacement and crowd shaping |
| LMG | 1 | Long sustained-fire profile | Commit to a lane and suppress pressure |
| Marksman/sniper | 1 | Currently authored as ordinary single hitscan | Deliberate line/recovery punish and high-value cash-out |

### Effective DPS snapshot

The current authored one-star sustained DPS spans approximately:

- Pistols: **34–65**.
- SMG: **63**.
- Assault rifles: **94–150**.
- Shotguns: **38–116** if all pellets connect.
- LMG: **103**.
- Sniper: **54**.

At three stars, the strongest current weapon reaches roughly **217 DPS / 2170 weapon power**. The global three-slot Combat Power ceiling is documented as **2372**, because backup weapons contribute at reduced weight.

### Design problem

All current serialized weapon assets are described as `SingleHitscan`, `Magazine`, `generalist`, with empty role tags, zero knockback, and no meaningful falloff authoring. The runtime has more expressive capability than the data uses. This makes upgrade level and raw DPS a stronger choice than tactical role.

### Intended family grammar — not yet implemented

- **Pistol:** finish or recover.
- **SMG:** take proximity risk to create fast pressure.
- **AR:** build a stable opening against mixed packs.
- **Shotgun:** create space or reshape a crowd.
- **LMG:** exploit a safe lane through committed sustain.
- **Sniper:** cash out alignment, exposure, or enemy recovery.

The candidate shared vocabulary is **Exposed**, **Staggered**, **Pinned**, and **Momentum**. None should be treated as current playable design until a narrow prototype and uncoached playtest prove it.

---

## 7. Enemy and encounter design

### Roster

The active campaign uses **15 baked VAT enemies**. A sixteenth zombie definition exists as a data asset, while the HUGO gorilla is excluded from production because its source mesh exceeds the VAT texture-width constraint.

| Enemy | Behavior | Tactical pressure | HP | Damage | Speed | Reward Coin / XP |
|---|---|---|---:|---:|---:|---:|
| Dog Pup | Walker | Basic spacing fodder | 40 | 6 | 2.4 | 1 / 1 |
| Cat Meow | Walker | Basic spacing fodder | 45 | 7 | 2.6 | 1 / 1 |
| Skeleton | Walker | Tougher baseline body | 60 | 9 | 2.5 | 2 / 2 |
| Dog Bark | Pouncer | Fast committed leap | 55 | 10 | 4.6 | 2 / 2 |
| Cat Bolt | Pouncer | Fast close-pressure leap | 50 | 11 | 5.2 | 2 / 3 |
| Cat Lightning | Pouncer | Tougher fast leap | 60 | 12 | 5.0 | 3 / 3 |
| Dog Bowwow | Pouncer/heavy runner | Durable breach pressure | 110 | 16 | 4.2 | 4 / 4 |
| Cacti | Ranged | Light projectile pressure | 40 | 8 | 1.8 | 2 / 2 |
| Cactus | Ranged | Durable projectile pressure | 70 | 11 | 1.9 | 3 / 3 |
| Skeleton Mage | Ranged | Long-range priority pressure | 80 | 14 | 2.6 | 4 / 4 |
| Burrow | Burrower | Untargetable approach/emerge AoE | 65 | 12 | 2.8 | 3 / 3 |
| Mole Rat | Burrower | Faster underground approach | 75 | 13 | 3.0 | 3 / 3 |
| Skeleton Giant | Boss/heavy | Durable slam pressure | 900 | 26 | 2.2 | 30 / 25 |
| Mole Rat King | Burrower boss | Boss underground timing | 1100 | 24 | 3.2 | 35 / 30 |
| Cactus Boss | Charger boss | Telegraph, charge, recovery | 1400 | 30 | 2.6 | 40 / 35 |

### Behavioral grammar

- **Walker:** direct NavMesh pursuit and contact attack; establishes baseline crowd pressure.
- **Runner:** closes distance quickly; tests movement and reaction.
- **Pouncer:** crouch telegraph → leap → recovery. Default timing is roughly 0.35-second crouch, 0.45-second leap, 0.5-second recovery, 1.5× damage.
- **Ranged:** holds or seeks useful range and fires a pooled projectile; authored projectile speed is 14.
- **Burrower:** surface → dive → untargetable underground chase → 0.7-second emerge telegraph → 2.2-radius AoE at 1.4× damage.
- **Charger:** 0.8-second telegraph → high-speed line charge → 1.2-second recovery; authored special damage multiplier is 1.8×.
- **Boss:** durable enemy with a special action such as area slam; generic boss special defaults to 6-second cooldown, 4-unit radius, and 2× damage.

### Encounter readability intent

Enemies should ask different questions, not only increase HP:

- Can the player maintain space against a baseline crowd?
- Can the player react to a committed leap or charge?
- Can the player reposition against ranged and underground threats?
- Can a heavy force setup rather than generic DPS feeding?
- Can a boss remain readable while lesser enemies alter the safe space?

The behaviors support these questions, but nearest-target auto-aim may prevent intentional priority targeting. This remains an open design risk.

---

## 8. Wave and spawn structure

### Wave model

Each stage uses authored wave data rather than procedural generation. A wave defines:

- Label and enemy groups.
- Count per enemy type.
- Normal spawn interval.
- Maximum simultaneous living enemies.
- Rest after clear.
- Optional pressure controls: target alive count, visible floor, reserve floor, initial burst, batch size, and recovery interval.

The director starts after a short delay (default 2 seconds), fills the authored pressure plan, waits for all required enemies to die, emits wave completion, observes the rest period, and advances. Clearing the last wave ends the run in Victory.

### Spawn safety

- Gameplay maps use baked NavMesh ground.
- Spawn points require physical clearance and a complete path to the player.
- Prior scene audit reported 12 valid spawn points on each of the five maps.
- Physical pickup collection can auto-resolve remaining drops on wave clear, preventing inaccessible currency from blocking payout.

---

## 9. Campaign

The campaign is a five-stage linear sequence. Completion of the previous stage is the intended hard progression gate. Combat Power is intended as a warning, not a grind wall.

| Stage | Theme/name | Waves | Enemies | Main lesson/pressure | Recommended power | Boss | First-clear reward |
|---|---|---:|---:|---|---:|---|---|
| 1 | Bùng Phát | 5 | 61 | Walkers, rising density, first runner pressure | 300 | None | 400 Coin |
| 2 | Cánh Đồng Gai | 6 | 72 | Ranged + burrow spacing | 950 | Cactus Boss | 700 Coin, 5 Gold |
| 3 | Nghĩa Địa Xương | 7 | 90 | Ranged mage + heavy punish | 1400 | Skeleton Giant pair | 1100 Coin, 10 Gold |
| 4 | Bầy Hoang | 7 | 103 | Pouncer/burrow timing | 1800 | Mole Rat King | 1600 Coin, 15 Gold, 1 Gem |
| 5 | Vây Hãm Titan | 7 | 92 | Mixed roster mastery | 2200 | Cactus Boss | 2400 Coin, 25 Gold, 3 Gem |

### Campaign navigation

- The Hub contains a compact catalog-driven selector above PLAY.
- Dots represent catalog stages; arrows move without wrapping.
- Completed, available, selected, and locked states are distinct.
- Last selected stage persists, with safe fallback to a valid stage.
- Returning to the Hub refreshes completion and availability.
- Under-recommended players should be warned but allowed to play once the previous-stage condition is satisfied.

### Stage design maturity

The maps and compositions exist, but their learning roles remain provisional. Their content is evidence of production breadth, not evidence that the intended combat grammar is already legible or fun.

---

## 10. In-run progression

### Implemented backend

- Every enemy grants authored XP directly on death.
- Level starts at 1.
- XP required for the next level is `10 + (Level - 1) × 8`.
- One kill can advance multiple levels if enough XP is granted.
- `RunState` can store temporary perks and multiply matching effects.
- The current pool contains seven stat perks:

| Perk | Effect |
|---|---|
| Damage S | +15% all-weapon damage |
| Damage M | +30% all-weapon damage |
| Fire Rate S | +12% fire rate |
| Fire Rate M | +25% fire rate |
| Move Speed S | +10% move speed |
| Max Health S | +20% maximum health |
| Coin Gain S | +25% stage Coin |

### Missing player-facing loop

- No runtime coordinator queues a perk choice for each gained level.
- The overlay does not draw perk title/description/icon from the pool.
- Every perk button calls the same empty `PickPerk()` behavior and only closes the overlay.
- The current audited overlay labels itself presentation-only.
- Not every multiplier is proven to be consumed by the relevant live gameplay system.

Therefore the game currently has XP arithmetic and perk data, but not a functional run-build system.

### Intended direction

After combat grammar proof, run progression should let the player select or mutate techniques compatible with the equipped arsenal. Stat perks should support pacing and smoothing, not hide flat weapon identity. The full 25-weapons-by-bespoke-tree design is rejected for MVP scope.

---

## 11. Persistent progression and Combat Power

### Save authority

`PlayerProfile` persistently owns:

- Coin, Gold, and Gem wallets.
- Weapon ownership.
- Per-weapon star/upgrade state and shards.
- Three weapon slots.
- Campaign completion, first-clear claims, and last selected stage.
- Costume ownership and equipped appearance.
- Gacha pity state.
- Mission progress and claims.

### Weapon stars

- Ownership corresponds to the base weapon state.
- Star 2 and Star 3 use weapon-specific shards plus Gold.
- Permanent star scaling affects live weapon damage and fire rate.
- Current cost arrays vary by rarity:
  - Star 2 shards: 10 / 15 / 20 / 30 / 40.
  - Star 3 shards: 25 / 35 / 50 / 75 / 100.
  - Star 2 Gold: 100 / 150 / 250 / 400 / 650.
  - Star 3 Gold: 250 / 350 / 600 / 900 / 1500.

### Combat Power

- Weapon power derives from sustained DPS × 10.
- Loadout power uses the strongest equipped weapon at full value and backups at reduced weight (0.35).
- Campaign UI can compare current power with stage recommendation.
- Design rule: recommendation communicates risk; it must not become a hard requirement that breaks the visible linear unlock promise.

---

## 12. Economy and collection

### Currency roles

| Currency | Current role | Main sources |
|---|---|---|
| Coin | Common purchases, lower-rarity content, mission rewards, future small upgrades | Enemy drops, run payout, missions, first-clear |
| Gold | Weapon acquisition, gacha, weapon stars | Elite/boss/campaign rewards, missions |
| Gem | Costume/high-value cosmetic economy and skin gacha | Rare elite drop, late first-clear, pass/achievement direction |
| Run XP | Temporary level progression; resets between runs | Enemy kills |

The economy configuration is explicitly **provisional**. No current value should be considered retention-balanced without real run-income telemetry.

### Physical drops

- Enemy Coin reward is converted into up to four pooled Coin drops.
- Pickup magnet radius is 3.5.
- Elite Gem chance is currently authored at 50%, one Gem on success.
- Drops scatter around death positions.
- Pickups credit `RunState`, not the persistent wallet directly.
- Remaining wave pickups may auto-collect, avoiding lost reward.

### Gacha

Two deterministic, atomic pull pools exist:

- **Weapon gacha:** Gold, single or ten-pull.
- **Costume gacha:** Gem, single or ten-pull.

Shared default rarity weights are **55 / 25 / 12 / 6 / 2**. Pity is **30 pulls** for Epic-or-better behavior. Weapon duplicates grant rarity-scaled weapon shards (10 / 10 / 12 / 15 / 20); other duplicate compensation arrays exist in Coin. Debit, result, pity, ownership, and compensation are committed as one profile transaction.

### Costume

- 448 player-facing modular costume items.
- 30 curated outfit sets.
- Cosmetic only; no combat stats.
- Technical composite body meshes are excluded from player-facing ownership.
- Default ownership/equipment supplies a clothed character.
- Body color and ears are separate appearance ownership concepts.
- Mandatory visual slots fall back to defaults; optional slots can be cleared.
- Rarity price bands currently include Common 300 Coin, Uncommon 800 Coin, Rare 120 Gold, Epic 350 Gold, Legendary 900 Gold.

### Meta-design warning

The collection/economy breadth is much larger than the currently validated reason to play another run. For MVP, economy supports the combat loop; it cannot be used to compensate for weak weapon decisions.

---

## 13. Missions and pass structure

### Current backend

- Deterministic daily and weekly rotation based on UTC date/week.
- Four active daily and four active weekly missions.
- Metrics include kills, archetype kills, waves, finished stages/runs, collected Coin, perks, switches, bosses, all-stage completion, and flawless clear.
- Rewards include Pass XP and Coin.
- Progress and claims persist in the profile.

### Current content pool

- 12 daily mission definitions.
- 8 weekly/campaign mission definitions.
- Daily targets cover 50–150 kills, five waves, one stage, 250 Coin, three perks, archetype kills, one elite, suggested-weapon clear, and ten switches.
- Weekly targets cover 1,000 kills, 25 waves, ten runs, final boss, all stages, 5,000 Coin, all bosses, and flawless clear.

### Incomplete or risky metrics

Some metrics depend on systems that are not complete or may lack reliable callers, especially perk choice, exact weapon-switch reporting, boss identity, suggested-weapon validation, and flawless-run logic. The mission catalog is broader than the proven live event coverage.

The free-pass direction is approximately 30 seasonal tiers, but a complete seasonal cadence, presentation, and reward track should not be treated as a finished product system.

---

## 14. Hub, screens, and UX flow

### Main flow

`Bootstrap → Hub → Loadout / Costume / Shop / Pass → selected campaign stage → HUD → Victory or Game Over → Hub or Replay`

### Hub

- Displays character preview and currencies.
- Provides campaign selection and PLAY.
- Routes to Loadout, Costume, Shop, and Pass.
- Settings exist as an overlay/modal direction.

### Loadout

- Three slots with pistol constraint in slot 0.
- Equip/unequip validation is backed by profile ownership.
- Empty long-gun slots are allowed at backend level.
- Current combat control cycles occupied slots.

### Shop

Intended sections are Weapons, Gacha, Costume, and Upgrades. The backend supports ownership, purchases, gacha, and weapon upgrading. The small permanent character-upgrade tab described by an older economy document must not be assumed complete unless current UI/source explicitly exposes it.

### Costume

- Modular avatar preview.
- Part or outfit equip/clear.
- Ownership validation and safe defaults.
- Cosmetic-only contract.

### Pass

- Daily/weekly mission display and claims are backed by profile data.
- Premium monetization is outside MVP scope.

### In-run HUD

The HUD supports core combat information such as health, weapon/ammo/reload, bombs, wave state, currency, pause, and weapon cycling. Current source audit indicates that XP/level/kill presentation is not yet a complete release-ready loop.

### Overlays

- Pause with resume countdown and abandon confirmation.
- Settings with music, SFX, and haptic values.
- Level-up overlay shell.
- Revive overlay shell with countdown.
- Game Over and Victory roots.
- First-time joystick instruction.

### Result gap

`RunSummary` has the correct numbers, but Game Over/Victory UI does not yet present a complete breakdown. A finished result must clearly show outcome, kills, time, wave, and earned currencies, then return the player to a refreshed Hub.

---

## 15. FTUE

### Intended full onboarding

1. First launch enters a tutorial run rather than the normal Hub.
2. Contextually teach movement.
3. Explain that firing is automatic.
4. Teach bomb use.
5. Finish the first run with a readable reward.
6. Guide the player to a rigged first weapon acquisition (SMG direction).
7. Guide equipping the new long gun.
8. Return focus to PLAY.

### Current implementation

- A first-time overlay watches for joystick movement held for roughly 0.5 seconds, then marks `ftue_done`.
- A skip button can complete that overlay.
- The full sequence for auto-fire explanation, bomb action, direct-to-run entry, rigged pull, auto/equip guidance, and Hub highlights is not demonstrated by the audited runtime.

Current FTUE status is therefore **movement tutorial fragment**, not complete onboarding.

---

## 16. Feedback, art, audio, and game feel

### Visual direction

- Stylized/toon 3D presentation.
- Portrait camera and readable top-down silhouettes.
- Cute monster packs are converted to VAT animation for crowd rendering.
- Five authored desert/military-style maps use generated layouts, baked occlusion, NavMesh, and a toon light rig.
- Player appearance uses a large modular costume catalog.

### Combat feedback already supported

- Muzzle flash, tracer, optional smoke trail.
- Recoil pivot with rear kick, pitch, and yaw variation.
- Camera shake on weapon fire and bomb explosion.
- Damage numbers.
- Material-property hit flash.
- Hit reaction without permanently locking rapid-fire enemies.
- Knockback hook.
- Death animation/dissolve.
- Enemy telegraphs for pounce, charge, burrow emerge, ranged shot, and boss special.

### Audio state

- Addressable audio catalog/runtime and gameplay audio director exist.
- Weapon fire/reload, bomb explosion, enemy attack/hurt/death/impact, world, footsteps, UI, music, and ambience all have current or intended hook categories.
- The repository contains a large source audio library, but source breadth is not equivalent to curated runtime content.
- The working tree contains extensive active audio changes and deletions, so exact coverage must be verified from a clean Unity run/build before declaring any category complete.

### Haptics

Settings persist an on/off preference, but a real haptic service/call path is not established by the current audit. The toggle should be considered nonfunctional until device evidence exists.

---

## 17. Current player experience by time

### First 10 seconds

Expected: joystick movement is immediately understandable; auto-target and auto-fire demonstrate the premise; enemy density, hit response, and death sell the one-survivor-versus-horde fantasy.

Current risk: audio/content wiring and FTUE completeness may make the opening less readable than the systems imply.

### First minute

Expected: the player faces at least two situations where movement, bomb timing, or weapon switching creates an obvious outcome difference.

Current risk: Stage 1 begins mostly with walkers, and weapon identities are weak. The optimal behavior may collapse to kiting while the highest-DPS gun fires.

### First run

Expected: understand three inputs, experience a temporary power choice, win or lose for a readable reason, see reward, and know what to do next.

Current reality: basic combat and closure exist; temporary choice, result breakdown, and complete FTUE do not.

### Following runs

Expected: different loadouts and stage compositions ask different combat questions; meta rewards unlock new expression.

Current risk: permanent DPS growth, gacha breadth, and collection can overpower tactical identity, making the game a stat ladder rather than a mastery loop.

---

## 18. Design debt and contradictions

These are design-level gaps, not a code-quality review.

### D1 — The hook is designed but not playable

The project names a compelling `READ → SET UP → SWAP → CASH OUT → RESET` loop, but current weapon data does not author the setup/cash-out states. Until testers deliberately perform and explain the sequence, it remains a hypothesis.

### D2 — Arsenal breadth exceeds identity

Twenty-five weapons exist, but family and model differentiation largely comes from numbers. Six assault rifles occupy the top of the current DPS table, creating a strong risk that collecting “better” weapons replaces learning different tools.

### D3 — Auto-target conflicts with priority play

Ranged, heavy, boss, and prop designs ask the player to prioritize threats, while targeting mostly chooses the nearest valid enemy. Positioning may solve this, but it is unproven.

### D4 — In-run progression is scaffolding

XP, levels, perk data, and UI shells can create the false impression of a roguelite build system. The actual choice/application loop is absent.

### D5 — Result and onboarding do not close the comprehension loop

The run can technically finish and pay correctly, but players need clear result numbers and a complete first-session path to understand cause, reward, and next action.

### D6 — Economy is ahead of desire

Gacha, missions, pass, costume, rarity, shards, stars, and three currencies are already broad. They should not be tuned for retention until combat provides evidence that players want a second run.

### D7 — Documents contain historical directions

Older files still describe Endless v1, hard Combat Power gates, free reload on switch, unconnected campaign flow, bomb pickup gaps, and purely proposed economy. The current source has superseded several of these. This audit intentionally does not merge conflicting historical promises into the live design.

### D8 — Evidence gap

There is no repository evidence in this audit of 10–20 uncoached external testers, mobile device funnel data, or a current profiler/soak result proving release readiness.

---

## 19. MVP boundary

### Must exist for the MVP to mean what it claims

- Stage 1–3 selectable and completable.
- Win/loss closure, results, payout, replay, and Hub return.
- Complete first-session onboarding for movement, auto-fire, bomb, and switching.
- A real in-run choice loop with at least six effects that visibly change play.
- Three clearly different weapon families in the release path.
- Core audio/hit/hurt/death/pickup/wave/boss/UI feedback.
- Persistent save and weapon upgrade loop.
- Android release configuration, device profiling, soak, and external playtest evidence.

### Explicitly outside MVP

- Procedural/endless redesign.
- Three bespoke skills for each of 25 weapons.
- Full food-buff system.
- New maps, enemies, weapons, or costumes.
- Premium Battle Pass, IAP, ads, or rewarded revive integration.
- Online leaderboard, PvP, clan, energy/stamina, or remote-content migration.

---

## 20. Proof standard

The current game design is validated only when uncoached target players:

- Understand the three controls without a person explaining them.
- Intentionally switch because the enemy situation changes, not only because a magazine empties.
- Can describe what one weapon set up and what the next weapon exploited.
- Remember at least one specific combat payoff.
- Do not converge on one highest-DPS answer for every composition.
- Understand why they won or lost.
- Understand what reward they received and what to do next.
- Voluntarily want to try another run or loadout.

The release plan's current external learning gates are at least 70% first-run completion and at least 40% voluntary second-run start among a 10–20-person near-final test cohort. These are learning gates, not commercial-scale KPIs.

---

## 21. Final assessment

Zombie War currently has a credible **walking skeleton moving toward a vertical slice**: the combat engine, enemy behaviors, five-stage campaign data, persistent collection systems, art content, and UI shells are substantial. It is not yet a complete playable-loop product because the most important promise—meaningful decisions from a three-input arsenal—is still unproven, and the run-build/result/onboarding chain is incomplete.

The smallest design truth to pursue is not “add more.” It is:

> Make one Stage 1 slice in which movement, one emergency tool, and three visibly distinct weapon roles produce a decision the player can explain; then close that run with a real choice, result, reward, and next action.

Until that is demonstrated in uncoached play, all broader progression and retention systems remain support infrastructure rather than the game's proven reason to exist.

---

## Appendix A — Evidence map

### Current source/data inspected

- `Assets/_Project/Scripts/Runtime/Flow/GameFlow.cs`
- `Assets/_Project/Scripts/Runtime/Gameplay/PlayerMovement.cs`
- `Assets/_Project/Scripts/Runtime/Gameplay/PlayerController.cs`
- `Assets/_Project/Scripts/Runtime/Gameplay/Health.cs`
- `Assets/_Project/Scripts/Runtime/Gameplay/Weapon.cs`
- `Assets/_Project/Scripts/Runtime/Gameplay/WeaponData.cs`
- `Assets/_Project/Scripts/Runtime/Gameplay/Bomb.cs`
- `Assets/_Project/Scripts/Runtime/Gameplay/BombThrower.cs`
- `Assets/_Project/Scripts/Runtime/Gameplay/Waves/*`
- `Assets/_Project/Scripts/Runtime/Gameplay/Zombies/*`
- `Assets/_Project/Scripts/Runtime/Gameplay/Pickups/*`
- `Assets/_Project/Scripts/Runtime/Systems/RunState.cs`
- `Assets/_Project/Scripts/Runtime/Systems/RunDirector.cs`
- `Assets/_Project/Scripts/Runtime/Systems/RunClosure.cs`
- `Assets/_Project/Scripts/Runtime/Systems/RunPerkPool.cs`
- `Assets/_Project/Scripts/Runtime/Systems/CampaignCatalog.cs`
- `Assets/_Project/Scripts/Runtime/Systems/CampaignSelection.cs`
- `Assets/_Project/Scripts/Runtime/Systems/CombatPower.cs`
- `Assets/_Project/Scripts/Runtime/Systems/PlayerProfile.cs`
- `Assets/_Project/Scripts/Runtime/Systems/EconomyConfig.cs`
- `Assets/_Project/Scripts/Runtime/Systems/GachaService.cs`
- `Assets/_Project/Scripts/Runtime/Systems/PassMissions.cs`
- `Assets/_Project/Scripts/Runtime/Systems/MissionTracker.cs`
- `Assets/_Project/Scripts/Runtime/UI/*`
- `Assets/_Project/Data/Campaign/CampaignCatalog.asset`
- `Assets/_Project/Data/Waves/WD_Level1..5.asset`
- `Assets/_Project/Data/Weapons/WD_*.asset`
- `Assets/_Project/Data/Zombies/ZD_*.asset`
- `Assets/_Project/Data/Economy/EconomyConfig.asset`

### Canonical intent/reference inspected

- `Docs/DESIGN_BRIEF.md`
- `Docs/MVP_SHIP_PLAN.md`
- `Docs/CURRENT_STATE.md`
- `Docs/Reference/Design/COMBAT_GRAMMAR.md`
- `Docs/Reference/Design/CAMPAIGN_AND_PROGRESSION.md`
- `Docs/Reference/Design/CAMPAIGN_BALANCE_TABLE.md`
- `Docs/Reference/Design/ECONOMY_DESIGN.md`
- `Docs/Reference/Design/SCREEN_FLOW.md`
- `Docs/ENEMY_ROSTER_AUDIT.md`

### Audit limitations

- GitNexus MCP was unavailable in this session; its local CLI also failed due to a Windows permission error resolving the user directory. Repository source/data inspection was used as the fallback.
- The five gameplay scenes are binary. This audit relies on the current source contracts, serialized data, and the prior documented Unity-MCP scene verification; it did not reopen or save those scenes.
- The working tree contained many pre-existing modified, deleted, and untracked files, including active audio, weapon, enemy, wave, scene, UI, package, and documentation changes. None were cleaned or reverted. This new audit file is the only intentional change made for this task.
