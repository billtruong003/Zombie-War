# Zombie War — Skill System Design

**Authority:** Skill design rules and candidate library

**Status:** Source of truth for exploration; no candidate is production-locked

**Updated:** 2026-08-08

**Prerequisite:** [`COMBAT_GRAMMAR.md`](COMBAT_GRAMMAR.md)

> **LOCKED RULE:** Skills support weapon gameplay. Skills do not replace weapon gameplay.

A valid skill changes how the player uses a gun, chooses a gun, creates/exploits an opening,
repositions or manages a resource rhythm. A skill whose main value is independently killing enemies
while the player watches is outside the intended system.

## 1. Repository reality — PROJECT FACT

- `RunState` stores XP, level and a list of `RunPerk`; `AddXp()` can award multiple levels and returns
  the count.
- Enemy death currently adds XP directly, but the gained-level result is not connected to a choice
  queue.
- `RunPerkPool` contains seven stat perks: small/medium damage, small/medium fire rate, move speed,
  max health and coin.
- `RunOverlays.ShowLevelUp()` is only a test hook. `PickPerk()` closes the overlay without choosing or
  applying a perk.
- Existing run multipliers are not fully consumed by weapon/player runtime gameplay.
- `Assets/Icons` contains 22 skill PNGs and 10 potion PNGs. They are visual ingredients, not proof
  that 32 mechanics should exist.

## 2. Recommended architecture — PROVISIONAL

### Layer 1 — Base Weapon Identity

Not technically a skill. Each family must already answer a different combat question. If removing a
skill makes two families identical, Phase 2 failed and the skill is hiding the problem.

### Layer 2 — Signature Technique

One selected technique changes how an equipped family is used. This is the main candidate source of
weapon/build expression: setup, punish, movement, sustain, emergency control or execution.

### Layer 3 — Arsenal Link

A small shared vocabulary connects weapons: for example Exposed, Staggered, Pinned and Momentum.
Links are systemic, not 25 × 25 handcrafted pairs. A link is valuable only when the player can read
who created it, who can consume it and why they should act now.

### Layer 4 — Run Mutation

During a run, a chosen technique may branch into behavior changes. Prefer “the blast now groups
targets” over repeated `+10% damage`. Mutation must preserve the technique's original purpose.

### Layer 5 — Stat Perks

Damage, fire rate, movement and health are support/filler. They smooth a build or power curve; they
are not signature techniques and cannot carry the hook.

## 3. Candidate skill library

Every item below is **CANDIDATE**. Names, values, trigger details and even survival are provisional.
No candidate is authorized for implementation until its phase explicitly selects it.

### Sidearm

#### Quickdraw Verdict — CANDIDATE

- **Trigger:** switching to pistol while a valid Exposed or low-health target exists.
- **Player intent:** use the sidearm as a deliberate emergency finisher.
- **Effect:** first eligible shot gains fast acquisition and a decisive execution bonus.
- **Situation:** short cash-out window after another weapon creates vulnerability.
- **Tradeoff:** weak if entered without setup; cooldown/eligibility prevents constant cycling.
- **Synergy:** Exposed from AR/SMG; Staggered target from shotgun.
- **Feel:** instant draw snap, sharp report, clean hit-stop/confirm.
- **Readability:** eligible target highlight plus a single primed chamber indicator.
- **Scaling/mutation:** threshold vs multi-target relay vs refund-on-clean-execution.
- **Risks:** can become free universal burst or reward blind spam-switching.

#### Last Chamber — CANDIDATE

- **Trigger:** the final round in the pistol magazine.
- **Player intent:** choose whether to spend a predictable high-value shot now.
- **Effect:** final round gains consequence, ideally execution or setup utility rather than raw DPS only.
- **Situation:** priority target at the end of a short sidearm rhythm.
- **Tradeoff:** miss/wrong auto-target wastes the chamber; reload follows.
- **Synergy:** Exposed/Punish windows make the last shot deliberate.
- **Feel:** escalating chamber cue and heavy final crack.
- **Readability:** ammo UI and weapon cue clearly announce “one left”.
- **Scaling/mutation:** ricochet-on-execution, reload tempo or mark transfer.
- **Risks:** auto-fire may spend it before the player can express intent.

#### Bounty Tag — CANDIDATE

- **Trigger:** a deliberate pistol hit on a priority enemy or first hit after draw.
- **Player intent:** nominate the next target for arsenal payoff.
- **Effect:** short mark amplifies or modifies the next qualifying weapon interaction.
- **Situation:** ranged/heavy priority among a crowd.
- **Tradeoff:** low immediate value and target-selection limitations.
- **Synergy:** sniper/LMG cash-out, mission/reward fantasy only if combat value is already clear.
- **Feel:** crisp tag sound and readable reticle/silhouette marker.
- **Readability:** one mark at a time; no HUD list.
- **Scaling/mutation:** transfer on kill, longer window or specialized family benefit.
- **Risks:** nearest auto-aim may tag the wrong enemy; economy bonus could distract from combat role.

### SMG

#### Shred Run — CANDIDATE

- **Trigger:** sustained SMG hits while the player is moving near the target.
- **Player intent:** accept close-range risk to create Exposed quickly.
- **Effect:** builds shred; full stack exposes the target for another weapon.
- **Situation:** fast threat or durable enemy that can be pressured safely.
- **Tradeoff:** stacks decay when distance/movement condition breaks; high ammo cost.
- **Synergy:** pistol/sniper cash-out; Momentum.
- **Feel:** accelerating hit cadence and visible armor strip.
- **Readability:** compact target meter/state change, not floating stack spam.
- **Scaling/mutation:** spread a partial shred, retain stacks briefly, trade speed for stronger expose.
- **Risks:** may become generic boss debuff or impossible to control with auto-target switching.

#### Killstream Capacitor — CANDIDATE

- **Trigger:** rapid SMG kills inside a short chain window.
- **Player intent:** route through weak enemies to bank a temporary resource.
- **Effect:** stores charge used by the next swap or burst.
- **Situation:** mixed pack containing fodder and a priority target.
- **Tradeoff:** chain breaks under poor positioning; little value against a lone boss.
- **Synergy:** Arsenal Execution or LMG/sniper transition.
- **Feel:** rising audio pitch/weapon glow culminating in a charged swap.
- **Readability:** one capacitor bar with clear full state.
- **Scaling/mutation:** longer chain, overflow utility or family-specific spend.
- **Risks:** rewards farming trash instead of reading danger; can become passive snowball.

#### Slipstream Feed — CANDIDATE

- **Trigger:** movement/Momentum while SMG is active.
- **Player intent:** keep moving to extend the weapon's pressure window.
- **Effect:** movement partially sustains ammo/reload tempo without creating infinite fire.
- **Situation:** kiting runners or rotating around a pack.
- **Tradeoff:** standing still loses value; risky pathing is required.
- **Synergy:** Momentum and movement techniques.
- **Feel:** rhythmic feed clicks and brighter tracer cadence at speed.
- **Readability:** ammo feedback shows earned feed explicitly.
- **Scaling/mutation:** reload while sprinting, limited ammo rebate or Momentum transfer.
- **Risks:** can erase the SMG resource weakness or encourage meaningless circles.

### Assault Rifle

#### Armor Drill — CANDIDATE

- **Trigger:** controlled consecutive hits on the same target.
- **Player intent:** hold a stable line long enough to create Exposed.
- **Effect:** breaks protection/creates an opening after a readable hit threshold.
- **Situation:** heavy, charger recovery or boss window.
- **Tradeoff:** retargeting resets progress; moderate rather than peak damage.
- **Synergy:** sniper/pistol cash-out.
- **Feel:** progressive crack, culminating armor-break burst.
- **Readability:** target material/VFX progresses in a few clear steps.
- **Scaling/mutation:** fewer hits, partial persistence or small nearby fracture.
- **Risks:** auto-aim may break lock; otherwise becomes a passive always-on damage amp.

#### Target Relay — CANDIDATE

- **Trigger:** completing a setup or kill with AR, then switching.
- **Player intent:** carry a deliberately prepared target into the next weapon.
- **Effect:** preserves or transfers the target opportunity for a brief swap window.
- **Situation:** mixed composition where the desired cash-out target risks being lost.
- **Tradeoff:** one target, short duration, no value without intentional follow-up.
- **Synergy:** every arsenal cash-out, especially sniper.
- **Feel:** target line/lock visually jumps to the incoming weapon.
- **Readability:** single relay marker and expiration cue.
- **Scaling/mutation:** longer hold, one transfer on death or partial state inheritance.
- **Risks:** can secretly override auto-aim and confuse targeting ownership.

#### Opening Burst — CANDIDATE

- **Trigger:** first short burst after equipping AR or after a complete reload.
- **Player intent:** enter the weapon for a reliable, bounded setup/output window.
- **Effect:** improved control, armor pressure or state application during opening rounds.
- **Situation:** re-establishing control after an emergency tool.
- **Tradeoff:** sustained fire loses the bonus; cycling must not refresh it for free.
- **Synergy:** honest preserved ammo/reload state and Target Relay.
- **Feel:** disciplined burst cadence with distinct muzzle/audio signature.
- **Readability:** chamber/burst indicator, no hidden timer.
- **Scaling/mutation:** wider setup application vs concentrated single-target burst.
- **Risks:** swap exploit if resource state is not honest; may resemble plain DPS buff.

### Shotgun

#### Breach Blast — CANDIDATE

- **Trigger:** close-range shotgun hit against enemies breaching a danger radius.
- **Player intent:** spend a shell to force immediate space.
- **Effect:** strong displacement/Stagger with modest damage emphasis.
- **Situation:** runner/pouncer crowd collapses on player.
- **Tradeoff:** poor at range; moves enemies away from follow-up if used carelessly.
- **Synergy:** creates safe LMG/sniper line or escape route.
- **Feel:** concussive bass, exaggerated recoil and bodies visibly displaced.
- **Readability:** danger-radius response and consistent displacement rule.
- **Scaling/mutation:** cone width, group pull-then-push or stronger center stagger.
- **Risks:** NavMesh/physics reliability; permanent stun/control dominance.

#### Crowd Shaper — CANDIDATE

- **Trigger:** shotgun pellets hit multiple targets in one blast.
- **Player intent:** arrange the horde rather than maximize isolated damage.
- **Effect:** pushes/flanks targets into a tighter lane or away from an exit.
- **Situation:** setting up line pierce or lane sustain.
- **Tradeoff:** low value on single targets; wrong angle ruins alignment.
- **Synergy:** sniper Linebreaker and LMG Suppression Lock.
- **Feel:** broad spatial response with a readable crowd wave.
- **Readability:** enemy movement must clearly follow shot direction.
- **Scaling/mutation:** fan-out vs funnel-in branch; brief Stagger at center.
- **Risks:** expensive crowd simulation; auto-aim direction may reduce player control.

#### Shell Rhythm — CANDIDATE

- **Trigger:** alternating fire/reposition windows or timing around a shell reload cadence.
- **Player intent:** treat each shell as a beat: fire, move, re-angle, fire.
- **Effect:** clean timing grants faster chamber, stronger control or retained setup.
- **Situation:** repeated breaches without turning shotgun into full-auto DPS.
- **Tradeoff:** mistiming loses the rhythm; requires honest reload state.
- **Synergy:** Momentum and short-lived Stagger windows.
- **Feel:** strong mechanical cadence with success accent.
- **Readability:** visible/audio beat window around chamber cycle.
- **Scaling/mutation:** wider timing, mobility reward or final-shell payoff.
- **Risks:** auto-fire can remove timing agency; may need behavior rather than button timing.

### LMG

#### Suppression Lock — CANDIDATE

- **Trigger:** sustained LMG hits into a lane/group.
- **Player intent:** commit ammo to hold pressure and create Pinned.
- **Effect:** affected enemies advance less effectively while maintained.
- **Situation:** controlled lane or heavy push.
- **Tradeoff:** commitment, long recovery and vulnerability to flank/telegraph.
- **Synergy:** sniper punish, movement around pinned enemies.
- **Feel:** escalating barrel/audio weight and enemy suppression response.
- **Readability:** a coherent lane effect, not an icon on every zombie.
- **Scaling/mutation:** wider lane, stronger boss pressure resistance or slower decay.
- **Risks:** trivializes melee hordes or becomes invisible slow.

#### Pressure Bank — CANDIDATE

- **Trigger:** sustained hits without breaking the LMG commitment.
- **Player intent:** choose when to stop holding and spend accumulated pressure.
- **Effect:** banks a bounded resource for a cash-out shot/swap or heavy stagger.
- **Situation:** build-up against dense packs or durable threats.
- **Tradeoff:** bank decays on poor timing; long commitment exposes player.
- **Synergy:** Arsenal Execution, Exposed heavy, next-weapon payoff.
- **Feel:** rising mechanical roar then a clear release.
- **Readability:** one weapon pressure gauge with obvious spend.
- **Scaling/mutation:** safety-oriented release vs damage-oriented release.
- **Risks:** another meter without a real decision; can reward stationary autopilot.

#### Fortress Feed — CANDIDATE

- **Trigger:** maintaining a stable position/controlled lane for a bounded interval.
- **Player intent:** trade mobility for longer sustain.
- **Effect:** improves feed/recoil rhythm while the commitment remains valid.
- **Situation:** after the player has created enough safety to hold ground.
- **Tradeoff:** movement or flank breaks it; recovery remains costly.
- **Synergy:** Pinned enemies, Field Repair defensive window.
- **Feel:** weapon settles and gains heavy continuous authority.
- **Readability:** stance/weapon state clearly transitions in and out.
- **Scaling/mutation:** mobile fortress compromise or stronger stationary payoff.
- **Risks:** conflicts with survivor movement; may make the game play itself.

### Sniper / Marksman

#### Linebreaker — CANDIDATE

- **Trigger:** firing through multiple aligned targets or a Crowd-Shaper line.
- **Player intent:** turn positioning/setup into one decisive lane shot.
- **Effect:** pierces with consequence increasing from a valid line, not generic splash.
- **Situation:** dense aligned pack.
- **Tradeoff:** weak emergency cadence and wasted value on poor alignment.
- **Synergy:** shotgun Crowd Shaper, Pinned lane.
- **Feel:** brief anticipation followed by an unmistakable line-cleave.
- **Readability:** projected/telegraphed line and sequential hit response.
- **Scaling/mutation:** retain force through more targets vs stronger final target.
- **Risks:** nearest auto-aim may select a bad line; technical pierce exists but asset is not authored.

#### Punish Window — CANDIDATE

- **Trigger:** shot lands during pouncer/charger/boss recovery or on Staggered/Exposed.
- **Player intent:** recognize timing and swap for high-value punishment.
- **Effect:** amplified consequence or state consumption.
- **Situation:** clear enemy commitment/recovery.
- **Tradeoff:** ordinary shots stay modest; missed timing loses value.
- **Synergy:** existing enemy recoveries, AR/shotgun setup.
- **Feel:** time-tight confirm, heavy impact and enemy collapse.
- **Readability:** recovery state and eligible shot both highly visible.
- **Scaling/mutation:** longer window vs stronger perfect-timing tier.
- **Risks:** hidden enemy-state checks or windows too short for mobile cycling.

#### Backstep Chamber — CANDIDATE

- **Trigger:** meaningful reposition away from threat during chamber/reload.
- **Player intent:** create distance as part of preparing the next shot.
- **Effect:** movement completes or improves the next chamber rhythm.
- **Situation:** pressure closes after a sniper cash-out.
- **Tradeoff:** requires safe backward path; no benefit while stationary.
- **Synergy:** Momentum, shotgun-created space.
- **Feel:** movement and bolt/chamber snap land on the same beat.
- **Readability:** progress cue attached to weapon, not an abstract buff.
- **Scaling/mutation:** lateral dodge variant or state retention during retreat.
- **Risks:** joystick movement direction ambiguity and passive kiting exploit.

### Arsenal / defensive

#### Combat Roll — CANDIDATE

- **Trigger:** explicit future control solution or tightly defined automatic danger response.
- **Player intent:** reposition through a telegraphed threat and preserve combat flow.
- **Effect:** short displacement/avoidance, not an independent damage nuke.
- **Situation:** pounce, charge, burrow emergence.
- **Tradeoff:** cooldown and positional risk; adding input is not pre-approved.
- **Synergy:** Momentum, Backstep Chamber, Slipstream Feed.
- **Feel:** crisp movement, invulnerability feedback only if actually granted.
- **Readability:** clear start/end and cooldown.
- **Scaling/mutation:** reload interaction or state carry, not repeated damage AoE.
- **Risks:** violates three-input lock; automatic roll removes agency.

#### Field Repair — CANDIDATE

- **Trigger:** earning a safe window through control/sustain or completing a combat condition.
- **Player intent:** convert good pressure management into recovery.
- **Effect:** restore bounded armor/health while maintaining weapon relevance.
- **Situation:** between dangerous compositions or during LMG hold.
- **Tradeoff:** interrupted by damage/movement condition; low burst value.
- **Synergy:** Fortress Feed, Staggered/Pinned safety.
- **Feel:** tactile repair pulses layered under gunplay.
- **Readability:** clear recoverable segment and interruption.
- **Scaling/mutation:** armor vs weapon-resource branch.
- **Risks:** passive sustain erases attrition; may become mandatory.

#### Adrenal Response — CANDIDATE

- **Trigger:** taking a meaningful hit or entering low health, with cooldown.
- **Player intent:** recover control through aggressive movement/weapon choice.
- **Effect:** brief movement/resource response, not automatic retaliation kill.
- **Situation:** mistake recovery under pressure.
- **Tradeoff:** danger-gated and temporary; cannot reward repeated face-tanking.
- **Synergy:** SMG Momentum, emergency pistol.
- **Feel:** heartbeat/tempo surge and responsive movement.
- **Readability:** one short, obvious window.
- **Scaling/mutation:** defensive escape vs offensive comeback branch.
- **Risks:** encourages damage-taking or creates noisy low-health effects.

#### Arsenal Execution — CANDIDATE

- **Trigger:** consume a setup created by a different weapon with an eligible finisher.
- **Player intent:** complete the full READ → SET UP → SWAP → CASH OUT sequence.
- **Effect:** extra payoff and possibly resource reset tied to a valid cross-weapon execution.
- **Situation:** priority enemy or climax of a combat phrase.
- **Tradeoff:** no benefit from same-weapon spam; strict eligibility/readability burden.
- **Synergy:** every shared state and family cash-out.
- **Feel:** strongest cross-arsenal confirm in the system.
- **Readability:** creator state, eligible weapon and consumption all visible.
- **Scaling/mutation:** resource refund, chain opportunity or reward branch.
- **Risks:** prematurely hard-codes the unproven hook and becomes mandatory meta-skill.

## 4. Icon inventory — ingredient library

One icon may serve several mechanics; no icon reserves a final skill name.

| Icon | Visual fantasy | Possible mechanic families |
|---|---|---|
| acrobat | agility/evasion | reposition, dodge timing, Momentum |
| alchemy | mixture/status craft | mutation, cleanse, bounded conversion |
| armyman | tactical discipline | mark, burst discipline, target relay |
| backstab | exploit opening | Exposed/recovery punish, execution |
| beast | feral resilience | comeback, close-risk sustain |
| fighter | brace/combat stance | defensive commitment, stagger resistance |
| fist | impact | Stagger, knockback, breach |
| highkick | launch | displacement, emergency control |
| knifemastery | precision/bleed | finisher, precise short state; bleed deferred |
| lowkick | trip/slow | Stagger, Pinned, movement control |
| machine | overdrive/feed | LMG/SMG sustain, pressure meter |
| packaging | capacity/supply | magazine/resource preservation |
| pistol | quickdraw | sidearm execution, emergency layer |
| powerstrike | armor break | Exposed, breach, heavy punish |
| punisher | execution | recovery/low-health cash-out |
| reload | weapon rhythm | chamber, swap reload, honest resource mutation |
| repair | restoration | armor repair, resource recovery |
| revive | recovery | comeback; revive system remains separate/deferred |
| runner | speed | Momentum, run-and-gun, retreat prep |
| runningfist | dash impact | movement into Stagger/control |
| runningstrike | momentum attack | moving setup/cash-out |
| sturdy | shield/poise | hold, suppression resistance, fortress |
| adrenaline | aggressive surge | short comeback/movement window |
| antidote | cleanse | remove future debuff; no current need proven |
| energetic | charge | technique/arsenal resource acceleration |
| falc_mixture | focus/hunt | mark, priority-target window |
| gemostatic | stability/regen | bounded recovery/attrition control |
| ofi | composure | recoil, target stability, timing window |
| painkillers | damage tolerance | temporary mitigation/comeback |
| painkillers2 | recovery cycle | post-hit stabilization |
| rage_potion | kill frenzy | chain/Momentum; snowball risk |
| salve | direct healing | active/bounded recovery |

## 5. Progression direction — PROVISIONAL

- Meta mastery may unlock alternative techniques; it must not make the only interesting technique a
  late grind reward.
- Run progression selects and mutates techniques already compatible with the equipped arsenal.
- Major branches change behavior, tradeoff or interaction. Numbers support the branch.
- Stat perks remain useful filler and power smoothing.
- Evolution is added only where it creates a new combat decision.
- **Rejected for now:** 25 independent weapons × five-level bespoke trees. Cost and balancing debt
  are unjustified before family identity and grammar are proven.

## 6. Selection gates

A candidate can enter a prototype only when:

1. its base weapon already has a readable identity;
2. it answers a specific prototype question;
3. its input → decision → response → payoff → next decision can be stated;
4. it has a visible tradeoff and failure case;
5. it can be tested without requiring economy, rarity or the full skill library.

Phase 4 selects the smallest representative set after Phase 3 evidence. Until then, this library is a
preserved design space, not an implementation backlog.
