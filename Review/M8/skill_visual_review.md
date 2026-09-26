# Skill visual review + fix proposal (for owner approval)

Date: 2026-09-26. Everything was captured in the skill sandbox (ZombieWar/Dev/Play Skill Sandbox)
with every card maxed, 3–10 dummies on screen, and the time scale at 0.3–0.5.
**Nothing below is built yet.** Order after approval: this skill pass first, then A (UI) and B (the rest).

Evidence (`Review/M8/skills/_sheets/`):
- `autonomous_0/1.png`: 10 autonomous powers.
- `evo_uni_0/1.png`: 6 evolutions and 2 universal cards.
- `signature_0/1/2.png`: 12 weapon signatures, each with the right gun equipped.
- `burst.png`: the most-changed frames from 8 s of continuous capture (Thunderstorm, Carpet Bomb, Absolute Zero, Frost Nova, Emergency).
- `proc_0/1.png`: frames captured right after the effect fired.

Raw frames are in `Review/M8/skills/`.

## Verdict in one line per card

| Card | Now | Verdict |
|---|---|---|
| Chain Lightning | Straight blue "pipes" in a T, the same shape the whole time, hard cut-off | **Weak**: doesn't read as lightning |
| Ordnance Core | Huge red telegraph ring + white disc + brown smoke + yellow crescents | **Messy** |
| Soul Burst | One thin green ring expanding | **Weak**: almost invisible |
| Emergency Detonation | A white/pink blast that covers the whole screen and the player | **Too much**: hides the game |
| Orbit Blades | Small grey spiky shards, no motion read | **Weak** |
| Drone Buddy | A glowing particle ball with a thin yellow tracer, no drone | **Placeholder** |
| Frost Nova | A hemisphere dome **cut by the ground** plus a thick ring running off screen; frozen tint barely shows | **Looks like a bug** |
| Fire Trail | Separate flame puffs like candles, not a trail | **Weak** |
| Boomerang | Small yellow crescents | **Weak** |
| Airstrike | Many giant red rings covering the screen, a comet streak for a bomb | **Messy** |
| Execution Round | Nothing visible | **Missing** |
| Kinetic Shield | A purple half-dome cut by the ground (it also stays after the card is removed, in the sandbox) | **Looks like a bug** |
| Thunderstorm | Thick violet "staircase" pipes + white glare boxes | **Heavy and odd** |
| Carpet Bomb | A golden flash over the whole screen + giant rings | **Too much** |
| Buzzsaw Halo | Bigger blades with trails | **OK**: best of the set |
| Absolute Zero | Same clipped dome as Frost Nova, bigger | **Looks like a bug** |
| Drone Squadron | 3 glowing balls | **Placeholder** |
| Reaper | Green rings + pink burst mixed together | **Messy** |
| 12 weapon signatures | Only Static Build-up shows anything (the same "pipe" arcs). Shockwave Belt **has no visual in code at all** (damage + push only). Hunter's Mark shows no mark. | **Mostly missing** |
| 5 stat cards | No world visual (expected) | OK: add pick feedback only |

## Root causes (fix once, all cards gain)

1. **Material bug:** `Materials/FX/M_SkillChainArc.mat` is Opaque with ZWrite on. Every arc, `Pulse` and
   `Converge` ring uses it, so nothing can fade: rings stay hard and bright until they vanish. That
   explains the huge red/orange rings on Airstrike, Ordnance, Carpet Bomb, Frost and Reaper.
2. **One telegraph ring per target**, drawn at full blast radius and at 0.3 m width. With 5 blasts
   that makes 5 giant rings.
3. **Spheres on a ground plane:** Frost Nova, Absolute Zero and Kinetic Shield use half-spheres
   that the ground cuts in half, so they read as a rendering bug.
4. **White/gold full-screen flashes** (Emergency, Carpet Bomb, Thunderstorm glare): no screen budget.
5. **Arcs are drawn once as straight segments** (no re-jag, no forks, no taper), so lightning reads as pipes.
6. **No per-status look on enemies:** frozen, marked, burning and shocked all use the same white hit
   flash, so status cards show nothing.

## Rules every card will follow (the "skill visual language")

- **Screen budget:** a single cast never covers more than ~25% of the screen, never hides the
  player, and makes no full-screen flash. Big moments use a short shake plus a small vignette
  instead of white.
- **Three beats:** telegraph (only when delayed, thin and fading) → travel (the eye can follow
  it) → impact at hit height (chest, not feet) + spark + sound.
- **One colour per element:**

  | Element | Colour |
  |---|---|
  | Lightning | cyan core, white hot centre |
  | Fire / explosive | orange → yellow |
  | Frost | ice blue |
  | Soul / death | green → violet |
  | Kinetic | violet |
  | Weapon signatures | the gun's amber |

  An evolution keeps its element colour and adds **gold accents** and size.
- **Ground effects are flat** (decal rings, scorch, frost patches) plus vertical particles. No clipped spheres.
- **Status is visible on the enemy:**

  | Status | Look |
  |---|---|
  | Frozen | ice-blue tint + frost shell |
  | Burning | orange flicker |
  | Marked | red reticle above the head |
  | Shocked | a small spark |

  One shader tint channel (`_StatusTint`) is already in the enemy shader.

## Per-card proposal

**Priority 1** (broken or messy, the most seen):

| Card | Proposal | Size |
|---|---|---|
| (all) | Fix the arc/ring material: transparent additive, fades. Telegraph = one thin ring (0.12 m) that fades in, never at full radius over the screen. | S |
| Chain Lightning | Arc re-jagged every 0.05 s, 3 layers (wide cyan glow, white core, 1–2 thin forks), tapered ends. Hops travel 0.03 s apart. Chest-height spark + flash on each target, fades in 0.25 s. | M |
| Thunderstorm | Chain arcs in violet/white, plus a **sky bolt** striking every 2nd target (`LightningStrikeTallBlue`, tinted), plus a soft storm-cloud shadow drifting over the area. No glare boxes. | M |
| Airstrike | A bombing run: thin marker rings in a line, a growing shadow under each drop point, a **bomb mesh** falling from ~14 m in 0.55 s at an angle with a whistle, and impacts landing in sequence. Keep the current explosion and smoke. | M |
| Carpet Bomb | The same run, bigger: 5 shells walking across the cluster, a gold rim on the explosions. **No full-screen flash.** | S (after Airstrike) |
| Frost Nova | Flat **frost ring** expanding on the ground + ice shards bursting outward + snow drift. Enemies turn ice-blue and slow down visibly. No dome. | M |
| Absolute Zero | Frost Nova, plus frozen enemies get an **ice-block shell** that shatters into shards on death. | S (after Frost) |
| Emergency Detonation | A red vignette pulse for 0.3 s (the warning), then a compact ground shockwave (0.25 s) + orange/red sparks + short shake. At most ~60% of the screen, player always visible. | S |
| Kinetic Shield | Charge shown as a thin ground ring filling around the player while walking. Charged = a light hex bubble (a full sphere raised above the ground, not clipped). A block = the hex shatters with a sound. Removed cleanly when the card is gone. | M |
| Shockwave Belt | Currently invisible. Every Nth shot, a translucent **cone wave** from the muzzle matching the damage cone, with dust at its edge. | S |

**Priority 2** (weak but readable):

| Card | Proposal | Size |
|---|---|---|
| Drone Buddy / Squadron | The real drone rig: hover/bank/strafe, turret, 3-round bursts through the player's muzzle + tracer + impact pipeline. **Emissive colour by rank:** cyan → green → gold, Squadron magenta and brighter with a light trail. A primitive placeholder model until the owner's drone arrives. | L |
| Orbit Blades | Bright metal saw discs (the Buzzsaw mesh, smaller) with a motion-blur trail ring and sparks on hit. | S |
| Buzzsaw Halo | Keep it; add a gold glow + sparks so it reads as the evolved Orbit. | S |
| Boomerang | A bigger spinning blade (≈1.2 m) with a trail, a visible return path, sparks on hit. | S |
| Fire Trail | A **continuous burn strip** on the ground (a quad with scrolling fire texture + scorch) with small flames and embers along it. Enemies inside flicker orange. | M |
| Ordnance Core | Shells lobbed from behind the player on a visible arc (0.5 s, smoke trail), a small marker, the explosion sized to the damage radius. No big ring. | S |
| Soul Burst | Each kill sends a green **soul wisp** flying to the player, with a counter on the skill slot. At 12: a green ground shockwave + ghost skulls flying out. | M |
| Reaper | A dark violet **scythe sweep** arc on execute kills, souls flying in. The green ring clutter is removed. | M |
| Execution Round | Enemies under 20% HP show a small red skull reticle. An executing hit = a red slash + a big crit number. | S |

**Priority 3** (weapon signatures: today they are mostly numbers only):

| Card | Proposal |
|---|---|
| Run & Gun | Speed lines at the feet and a brighter muzzle while shooting on the move |
| Quickstep Round | After a step, the next shot has a thick gold tracer and a gold crit number |
| Static Build-up | Every 5th hit, a **short** spark jump (a small version of the new chain arc, not the "pipe") |
| Bullet Hose | Heat ramp: the muzzle glow goes orange → white and the tracer thickens as the fire rate climbs |
| Focus Fire | Stacking reticle ticks (1–3) on the target you keep hitting |
| Breach Round | Longer tracer, and a penetration spark on each enemy it passes through |
| Point Blank | Big muzzle blast + a dust puff on close hits |
| Concussion | Stun stars / a small shock ring on the hit enemy |
| Heavy Pressure | Barrel heat glow + cracks on the enemies it keeps hitting |
| Longshot | Long-range hits get a longer tracer and a distance crit number |
| Hunter's Mark | A **red reticle** above the marked enemy (none today) and a bonus spark on the hit |
| Stat cards | On pick, a small aura pulse on the player and a glow on the HUD chip |

Sizes: S ≈ 1–2 h, M ≈ half a day, L ≈ 1 day.
- Priority 1 ≈ 3 days.
- Priority 2 ≈ 3 days (1 of it the drone).
- Priority 3 ≈ 1.5 days.

## How it will be checked

- Every card is re-captured in the sandbox with the same script, before/after side by side, and
  judged against the rules above.
- No new effect may cost frame time on device. Pooled only; FX count per frame stays inside the
  existing global budget (`GlobalProcsPerSecond`).

## Sandbox issues found while capturing (fixed or to fix with the pass)

| Issue | Status |
|---|---|
| Kills in Mortal mode opened level-up and froze the bench | fixed (auto-closes, no random pick) |
| "Reset" leaves the Kinetic Shield aura behind | to fix (reset through the runtime, not the rank table) |
| `TotalProcs` does not count evolutions or area powers, so proc-triggered capture misses them | to fix (count every cast) |
| White skeleton dummies hide status tints | to fix (use a coloured zombie as the dummy) |
| The "HORDE INCOMING" banner still counts down in the sandbox | to fix (hide in the sandbox) |

## Needs the owner

1. Approve the rules and the per-card proposals (or mark the cards to change).
2. The priority order (1 → 2 → 3) and the drone model when it's ready.

## Progress — Priority 1 done (2026-09-27)

Before/after: `Review/M8/skills/_sheets/p1_before_after.png`; raw captures in `skills/force*`.

| Item | Done |
|---|---|
| Line material | New shaders `ZombieWar/FX/SkillLine` (additive arcs, alpha rings — both fade), `SkillDisc` (ground shadows/bands), `SkillShield` (Kinetic shell) |
| Root cause 2 | **Epic Toon FX prefabs were played with `Quaternion.identity`**, which stood flat effects upright (the Frost "dome", standing decals). Every skill effect now plays in its authored rotation (`SkillArsenal.Flat`) |
| Chain / Thunderstorm | Re-jagged every 0.05 s, glow + white core + forks, tapered, hops travel 0.035 s apart, small chest-height spark; storm: violet + sky bolts on every 2nd target |
| Airstrike / Ordnance / Carpet Bomb | One thin telegraph ring + a shadow that darkens as the bomb falls; bomb falls at an angle along one flight line (0.55 s from 14 m); impacts land in order; Ordnance uses the grenade blast; blast visuals capped at 1.3× |
| Frost Nova / Absolute Zero | Flat nova (no dome), a frost band racing to the edge, `NovaFrost_M8` variant without the screen-wide fog |
| Emergency | Visual capped (no more screen-wide pink/white), red ring |
| Kinetic Shield | Hex fresnel shell (clear inside, rim only, back faces culled), resting on the ground; charge ring fills at the feet while walking; flashes on a block |
| Shockwave Belt | Cone wave drawn over exactly the hit cone |
| Sandbox | `Capture` / `ForceCapture` (fires a power now, captures after), reset via the runtime (no leftover shield) |

Not yet: Absolute Zero ice-block shell on frozen enemies (moved to P2 with the other status visuals).

## Progress — Priority 2 and 3 done (2026-09-27)

Sheets: `_sheets/p2_*.png`, `_sheets/p3_signatures_*.png`.

| Card | Done |
|---|---|
| Drone / Squadron | Real rig (`Prefabs/Skills/Drone_Placeholder.prefab`, built by ZombieWar/Skills/Build Skill Pass Assets): figure-8 flight, banking, rotors, turret yaw, 3-round bursts from `Muzzle`, the player's tracer tinted by rank. Rank colours: cyan → green → gold → magenta Squadron (+ trail). **Swap in the owner's model** by keeping `Muzzle`, `Rotor*` and a `Glow` group |
| Orbit / Buzzsaw | Faint path ring; Buzzsaw turns the blades, trails and ring gold |
| Boomerang | Bigger blade (1.6×) |
| Fire Trail | Drops every 0.7 m (was 1.1), smaller flames over a glowing burn: one strip |
| Soul Burst | Each kill's soul flies into the player (budgeted), burst = `SoulExplosionGreen` |
| Reaper | Violet scythe crescent + soul burst (`SoulExplosionPurple`) where the enemy fell; no orange ring |
| Absolute Zero | Ice bursts on each frozen enemy |
| Execution | Red lock-on on enemies in the execute window |
| Status marks (all) | **Bug fixed:** marks had no sprite, so Breach/Concussion/Hunter's marks never showed. New reticle sprite, slow spin |
| Hunter's Mark | **Bug fixed:** the mark was consumed before it was checked; now shows on lock-on and on the empowered hit |
| Signatures | Tracer/muzzle show the card: Quickstep gold slug, Breach orange armour-piercer, Longshot bright long tracer, Point Blank orange close hits, Bullet Hose white-hot ramp, Heavy Pressure red-hot ramp, Run & Gun cyan tracer + footprints; Focus Fire reticle grows with stacks; Static Build-up is a small spark jump |
| Stat cards | A pulse in the card's layer colour on pick |

EditMode 662/662. GitNexus index still fails to rebuild, so impact was checked by text search.
