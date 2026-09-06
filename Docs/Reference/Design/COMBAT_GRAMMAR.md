# Zombie War — Combat Grammar

**Authority:** Core combat behavior and prototype questions

**Status:** Source of truth; directions remain provisional until playtest

**Updated:** 2026-08-08

**Vision:** [`../../DESIGN_BRIEF.md`](../../DESIGN_BRIEF.md)

**Execution:** [`../../Plans/ZOMBIE_WAR_GAMEPLAY_EXECUTION_PLAN.md`](../../Plans/ZOMBIE_WAR_GAMEPLAY_EXECUTION_PLAN.md)

## 1. Current combat reality — PROJECT FACT

Verified against the current working tree, which is newer than the `68fbc090` GitNexus snapshot.

- Portrait/mobile intent; current combat inputs are joystick, bomb and one weapon-switch button.
- `PlayerMovement` auto-selects the nearest valid target within range, with dwell/stickiness and an
  immediate-threat override. The player does not directly choose a target.
- `Weapon` auto-fires when that target is inside the equipped weapon's range and auto-reloads when
  the magazine empties. There is no fire or reload input.
- The runtime loadout has three slots: mandatory one-handed pistol in slot 0 and up to two two-handed
  long guns in slots 1–2. `SwitchWeapon()` cycles occupied slots.
- Player agency currently exists in movement/spacing, bomb timing, loadout before the run and cycling
  weapons. It does not yet reliably exist in target priority or a proven weapon interaction loop.
- The current 25 `WeaponData` assets are mostly a tier/stat ladder: every serialized asset uses
  `SingleHitscan`, `Magazine`, `buildTag=generalist`, empty `roleTag`, zero knockback and no authored
  falloff. Shotguns mainly differ through pellet count/cone. The sniper asset is not authored as
  `PiercingLine` although the runtime supports it.
- Current approximate sustained DPS spans overlap heavily and often reward picking the higher number:
  sidearm 34–66, SMG 63, AR 94–150, shotgun 38–116 if all pellets connect, LMG 103, sniper 54.

## 2. Root problem — DESIGN INFERENCE

The project already produces spectacle, density and competent presentation, but the recurring combat
decision is weak. Auto-aim and auto-fire remove mechanical execution by design; weapon data currently
does not replace that lost agency with strong tactical roles. The result risks becoming:

`move away from danger → let highest DPS gun fire → switch only for empty magazine or curiosity`

The missing value is not another isolated system. It is a readable interaction where enemy behavior
creates a problem and the player's arsenal decision visibly changes the answer.

## 3. Core grammar — HOOK HYPOTHESIS, UNPROVEN

### READ

The player recognizes a meaningful problem: a pouncer commits, ranged pressure accumulates, a heavy
blocks a lane, a group crowds the escape route, or a recovery window opens.

**Required signal:** enemy silhouette, composition, telegraph and audio/visual response identify the
problem before the punishment lands.

### SET UP

The active weapon or movement creates leverage rather than merely dealing generic DPS: displace a
crowd, strip protection, expose a target, hold a lane or arrange a line.

**Required signal:** the created state is visible and short-lived enough to demand a next action.

### SWAP

The player deliberately selects a weapon because it exploits the situation. A switch caused only by
empty ammo is resource maintenance, not proof of the hook.

**Required signal:** current mobile controls let the player reach the intended weapon reliably.

### CASH OUT

The second weapon converts the setup into a strong result: punish a recovery, pierce a line, execute a
weakened priority target or safely sustain fire into controlled enemies.

**Required signal:** damage, motion, sound and state consumption let the player attribute payoff to
their own sequence.

### RESET

Magazines, reloads, positioning, new spawns or the end of the shared state prevent the same answer
from running forever and create the next read.

**Required signal:** reset produces another decision, not dead time.

## 4. Intended weapon family identity — PROVISIONAL

These are directions for Phase 2–3, not descriptions of the current serialized assets. A signature
skill may strengthen an identity; it may not manufacture one from zero.

### Pistol

- **Fantasy:** dependable sidearm, snap response, clean finish.
- **Baseline role:** emergency recovery and reliable execution.
- **Strength:** accessible, quick cadence, low commitment, useful when long guns are between rhythms.
- **Weakness:** low crowd throughput and limited peak payoff.
- **Ideal situation:** one exposed threat, low-health priority target, compromised long-gun cycle.
- **Switch to it:** finish a target or regain control without committing to a long reload.
- **Switch away:** crowd or high-health pressure demands specialization.
- **Resource rhythm:** small magazine, fast recovery; must not become infinite universal fallback.
- **Potential setup:** tag/mark or finish threshold that benefits the next weapon.
- **Potential cash-out:** quickdraw execution of Exposed or low-health targets.

### SMG

- **Fantasy:** mobile close-pressure hose.
- **Baseline role:** movement interaction and rapid shredding at risky range.
- **Strength:** builds pressure quickly while repositioning; responsive against fast threats.
- **Weakness:** range, ammunition efficiency and sustained accuracy/control.
- **Ideal situation:** runner pressure, close flank, target whose defense can be stripped by many hits.
- **Switch to it:** exploit safe proximity or rapidly create Exposed/Momentum.
- **Switch away:** distance opens, magazine collapses or a high-value punish window appears.
- **Resource rhythm:** fast consumption, frequent reset, reward for moving through a short window.
- **Potential setup:** repeated hits create Exposed.
- **Potential cash-out:** spend Momentum during a mobile burst.

### Assault Rifle

- **Fantasy:** disciplined all-range workhorse.
- **Baseline role:** stable setup, armor pressure and general combat control.
- **Strength:** predictable output, usable range, readable burst cadence.
- **Weakness:** should not dominate emergency crowd control, execution or maximum sustain.
- **Ideal situation:** establishing advantage against mixed packs and priority targets.
- **Switch to it:** create a reliable setup when no extreme tool is yet correct.
- **Switch away:** a specialized crowd, sustain or punish opportunity appears.
- **Resource rhythm:** medium magazine/reload; burst discipline can create an opening.
- **Potential setup:** armor drill or target relay creates Exposed.
- **Potential cash-out:** opening burst against a state created by another weapon.

### Shotgun

- **Fantasy:** immediate concussive authority at close range.
- **Baseline role:** displacement, crowd shaping and emergency control.
- **Strength:** cone coverage, stagger/space creation, reliable response to a breach.
- **Weakness:** range, reload cadence and poor sustained single-target efficiency.
- **Ideal situation:** crowd enters personal space or blocks an escape line.
- **Switch to it:** urgently make room or group/reposition threats.
- **Switch away:** space is restored and a controlled target is ready for payoff.
- **Resource rhythm:** few high-value shells separated by reload decisions.
- **Potential setup:** Stagger, knockback or crowd alignment.
- **Potential cash-out:** burst against a tightly packed or already Exposed group.

### LMG

- **Fantasy:** commit to a lane and overwhelm it with sustained fire.
- **Baseline role:** suppression and holding pressure.
- **Strength:** long uptime, benefits from stable control and Pinned targets.
- **Weakness:** slow recovery, poor emergency response, movement/retargeting cost.
- **Ideal situation:** controlled lane, heavy pressure, target group unable to break the firing line.
- **Switch to it:** the player has created enough safety to commit.
- **Switch away:** flank/telegraph breaks the hold or heat/magazine forces reset.
- **Resource rhythm:** long magazine followed by meaningful vulnerability; no free refill on swap.
- **Potential setup:** sustained hits create Pinned.
- **Potential cash-out:** banked pressure spent on a heavy or boss window.

### Sniper / Marksman

- **Fantasy:** one deliberate line turns preparation into a decisive hit.
- **Baseline role:** line punish, recovery punish and high-value cash-out.
- **Strength:** range, precision-by-positioning, pierce or high single-target consequence.
- **Weakness:** slow cadence, weak emergency control and dependence on a readable line/window.
- **Ideal situation:** aligned enemies, exposed heavy, pouncer/charger recovery.
- **Switch to it:** setup has created a short, valuable shot.
- **Switch away:** the window is spent or close pressure invalidates deliberate cadence.
- **Resource rhythm:** low shot count/long chamber; every shot should have a reason.
- **Potential setup:** creates a brief high-value mark only if it does not dilute cash-out identity.
- **Potential cash-out:** consume Exposed/Staggered or pierce an arranged line.

## 5. Existing enemy tactical grammar — PROJECT FACT + DESIGN INFERENCE

| Existing behavior | Project fact | Tactical question it can support | Check type |
|---|---|---|---|
| Walker | Base chase/contact loop | Can the player manage space while other threats change priority? | sustain/crowd baseline |
| Runner | Speeds up near the player | Can the player react before close pressure collapses spacing? | movement check |
| Pouncer | Telegraphs a committed pounce, then has recovery vulnerability | Can the player dodge the commitment and punish recovery? | timing + punish window |
| Ranged | Holds range and fires a projectile | Will the player reposition or prioritize a distant source of pressure? | spacing + target priority |
| Burrower | Untargetable/invulnerable underground; emerges with AoE | Can the player read ground telegraph and preserve escape space? | movement + timing |
| Heavy | Authored archetype with higher pressure/durability; behaviors vary by concrete enemy | Can the player use setup instead of feeding generic DPS? | burst/setup requirement |
| Charger | Telegraphs a charge, can hit once per charge, then recovers | Can the player bait, evade and exploit a committed line? | timing + recovery punish |
| Boss | Existing boss family includes slam behavior | Can the player read a major window while the horde changes positioning? | mixed pressure/burst |

**OPEN:** nearest-target auto-aim may prevent the player from expressing priority against ranged,
heavy or props. Do not solve this by inventing new enemy classes; first test whether positioning and
targetability rules can make the existing questions legible.

## 6. Shared combat states — CANDIDATE vocabulary

Use the smallest vocabulary that survives playtest. Each state must create a decision inside a short
window. Names and exact numbers are not locked.

### Exposed — CANDIDATE

- **Who creates:** likely AR/SMG pressure or a deliberate enemy recovery event.
- **Who benefits:** high-consequence weapons, especially pistol/sniper; possibly any weapon at a
  smaller benefit if readability demands consistency.
- **Player sees:** strong armor-break/target highlight, not a tiny HUD icon.
- **How long:** short enough to require intent; long enough for the actual mobile switch action.
- **Decision:** cash out now, preserve current rhythm, or reposition for a better shot?

### Staggered — CANDIDATE

- **Who creates:** shotgun impact, heavy collision or an enemy's failed commitment.
- **Who benefits:** weapons needing safety or a stable line; player movement also benefits.
- **Player sees:** unmistakable recoil/stance break and brief interruption.
- **How long:** a brief control/punish window; never a permanent stun loop.
- **Decision:** escape, align the crowd, or swap into punishment?

### Pinned — CANDIDATE

- **Who creates:** sustained LMG pressure or concentrated suppression.
- **Who benefits:** lane-holding weapons and movement around the controlled group.
- **Player sees:** reduced advance/pressure response on affected enemies, with coherent VFX/audio.
- **How long:** maintained by commitment, quickly decays when fire shifts.
- **Decision:** keep committing ammo or release the trigger rhythm to exploit elsewhere?

### Momentum — CANDIDATE

- **Who creates:** active movement, close-risk SMG play or successful reposition sequences.
- **Who benefits:** movement-linked techniques and possibly the next weapon after a clean swap.
- **Player sees:** player/weapon feedback rather than another enemy debuff icon.
- **How long:** rapidly decays to reward continued intentional movement.
- **Decision:** spend now, extend the risky line or reset safely?

**Rejected for prototype:** bleed/burn/freeze/poison/curse stacks simply to increase content count.
They add vocabulary before the core setup/payoff grammar is proven.

## 7. Ammo, reload and honest resource rhythm

### Verified problem — PROJECT FACT

`Weapon.EquipData()` destroys/recreates the weapon visual and sets `_ammoInMag` to full while clearing
reload state. Therefore cycling away and back refills that weapon. This makes ammunition dishonest,
removes vulnerability and can turn switching into a free-reload exploit.

### Design requirement — LOCKED

Each equipped weapon slot preserves meaningful runtime resource state while unequipped: at minimum
magazine/reload state, and later heat/charge if those rhythms exist. Switching cannot be a free refill
unless a clearly authored technique explicitly creates that payoff.

Architecture is intentionally unspecified until Phase 3 implementation planning inspects existing
lifecycle constraints.

## 8. Bomb, props and pickups inside the grammar

### Bomb — PROJECT FACT / PROVISIONAL role

The bomb has three charges, cooldown, arc, fuse and AoE; damage can reach any `IDamageable`, including
the player. It belongs as a deliberate emergency reset or crowd-shaping tool, not a parallel primary
damage build. Its self-risk can create meaningful positioning if telegraph/readability supports it.

### Loot crate and explosive barrel — PROJECT FACT / OPEN usability

Loot crates and explosive barrels exist; barrels can self-damage and chain. However, player auto-aim
selects registered enemies, not props, so deliberate prop play is currently weak or incidental.

Before expanding prop content, answer: can the player intentionally cause the interaction with the
existing targeting/input model? If not, props are spectacle or hazards, not a reliable combat verb.

### Coin/Gem/pickups — PROJECT FACT / grammar role

Physical pickups use magnet collection and remaining pickups can auto-collect on wave clear. Pickups
should influence position/risk/reset decisions, not become a second progression loop that distracts
from weapon interaction. XP source and timing remain open until the run-build phase; avoid double
granting through direct XP plus physical orb.

## 9. Prototype proof standard

Combat grammar is supported only if testers, without coaching:

- intentionally switch when the combat situation changes;
- can describe the setup they created and what the second weapon exploited;
- remember at least one specific mastery/payoff moment;
- do not consistently return to one highest-DPS answer;
- want to try another loadout or interaction.

Failure signals:

- random cycling or switching only on empty magazine;
- states happen but the player cannot explain them;
- setup feels like tax before damage;
- auto-target prevents the intended decision;
- one weapon wins every composition;
- the player watches rather than decides.

These criteria are locked before implementation so Phase 3 cannot move the goalposts.
