# M6.1 — Build simulations (2 per active family, 12 total)

**Status:** HYPOTHESIS. These are prototype targets, not approved designs. Every card referenced is a
candidate from `SKILL_CATALOG.md`; every number is TUNING.

Each simulation shows early (levels 1–3), mid (4–7) and late (8+) with the card taken, the behaviour
change, and the primitive it depends on. Offer rules assumed: max one stat card per offer, family
signature biased into slot A, autonomous/universal into slot B.

**Rejection rule applied:** a pair is only listed if the two builds differ in *where the player stands*
or *what they walk toward*, not merely in DPS.

---

## Sidearm

### S1 — "Kite Runner"
| Phase | Card | Behaviour change |
|---|---|---|
| Early | Run & Gun | Player stops standing still entirely; circling becomes the default. |
| Early | Move Speed Up | Widens the circle; more of the map is reachable per wave. |
| Mid | Chain Lightning | Damage now happens *while* running; player stops trying to aim-hold. |
| Mid | Quickstep Round | Distance is now doubly rewarded — rate and burst. |
| Late | Kinetic Shield | Movement also buys safety; player takes riskier lines through the horde. |
| Late | Fire Rate Up (rank) | Pushes toward the soft cap. |

Strength: never cornered; best station-runner (reaches Signal Relays fastest).
Weakness: forced stops (hold-zones) are its worst case — a real tension with Signal Relay.
Map decisions: takes Route Scanner and distant caches; avoids hold-zones unless healthy.
Primitives: velocity sampler, distance accumulator, chain targeting, damage-intercept.

### S2 — "Executioner"
| Phase | Card | Behaviour change |
|---|---|---|
| Early | Quickstep Round | Player watches the charge pip and times approaches. |
| Early | Execution Round | Finishing wounded elites becomes the priority. |
| Mid | Ordnance Core | Bombs cover the crowd so the player can hunt single targets. |
| Mid | Damage Up | Raises the empowered shot above elite HP thresholds. |
| Late | Emergency Detonation | Allows hunting deeper into the pack. |
| Late | Quickstep rank 3 | Charge arrives often enough to feel like a rhythm. |

Strength: kills elites and bosses far above its DPS class.
Weakness: poor raw clear; dies to sheer numbers.
Map decisions: takes Boss Beacons early; skips crowded caches.
Contrast with S1: S1 *avoids* the pack, S2 *enters* it to finish targets.

---

## SMG

### M1 — "Thunder Hose"
| Phase | Card | Behaviour change |
|---|---|---|
| Early | Bullet Hose | Player holds fire on one group instead of tapping between targets. |
| Early | Static Build-up | Actively seeks the densest crowd to charge faster. |
| Mid | Chain Lightning | Two independent lightning sources; screen becomes electric. |
| Mid | Fire Rate Up | Charges arrive faster. |
| Late | Soul Burst | Kills feed explosions; the crowd becomes the resource. |
| Late | Static rank 3 | 5-target discharges. |

Strength: best crowd clear in the game.
Weakness: near-useless against a lone boss; range 5.8 m.
Map decisions: farms Signal Relays (crowds come to it); avoids Boss Beacons until late.
Primitives: ramp timer, chain targeting (NEW), kill-count trigger.

### M2 — "Attrition Sweeper"
| Phase | Card | Behaviour change |
|---|---|---|
| Early | Static Build-up | Same charge-seeking opener. |
| Early | Max Health Up | Allows standing in the crowd rather than at its edge. |
| Mid | Ordnance Core | Handles range weakness; player stops retreating from distant packs. |
| Mid | Coin Gain Up | Funds Supply Cache and Medical Station usage. |
| Late | Emergency Detonation | Panic button for the moments standing still fails. |
| Late | Max Health rank 3 | Survives long enough to out-attrite waves. |

Strength: longest survival time; best economy per run.
Weakness: low burst; slow to resolve elites.
Contrast with M1: M1 maximises spectacle and clear speed; M2 buys *time* and spends Coin.

---

## Assault Rifle

### A1 — "Focus Line"
| Phase | Card | Behaviour change |
|---|---|---|
| Early | Focus Fire | Player deliberately holds one target through the ramp. |
| Early | Execution Round | Ramp plus threshold makes the last 20 % vanish. |
| Mid | Hunter's-style priority via Damage Up | Larger targets become the preferred ramp host. |
| Mid | Chain Lightning | Covers the trash the player is ignoring. |
| Late | Focus Fire rank 3 | 8 stacks reachable within one elite's lifespan. |
| Late | Fire Rate Up | More hits per second = faster ramp. |

Strength: highest single-target sustained damage.
Weakness: crowds break the ramp constantly.
Map decisions: Boss Beacon is its payoff; hold-zones are dangerous.

### A2 — "Breacher Line"
| Phase | Card | Behaviour change |
|---|---|---|
| Early | Breach Round | Player lines enemies up deliberately for the Nth shot. |
| Early | Move Speed Up | Repositioning to create lines becomes routine. |
| Mid | Ordnance Core | Bombs group enemies into the lines Breach wants. |
| Mid | Damage Up | Raises the pierce payoff. |
| Late | Soul Burst | Rewards the crowding the build creates. |
| Late | Breach rank 3 | Pierce 4 makes lines genuinely lucrative. |

Strength: converts crowds into lines; scales with density.
Weakness: wasted entirely on isolated targets.
Contrast with A1: A1 holds one target still; A2 constantly repositions to build lines.

---

## Shotgun

### G1 — "Breacher"
| Phase | Card | Behaviour change |
|---|---|---|
| Early | Point Blank | Player charges *into* the horde instead of kiting. |
| Early | Max Health Up | Makes the approach survivable. |
| Mid | Soul Burst | Kills at close range detonate around the player. |
| Mid | Damage Up | Raises the close-range spike above elite thresholds. |
| Late | Emergency Detonation | Escapes the one approach that goes wrong. |
| Late | Point Blank rank 3 | Close range becomes decisive. |

Strength: highest burst; the only build that moves toward danger.
Weakness: ranged archetypes (Cactus r9–10, SkeletonMage r12) punish it hard.
Map decisions: takes Medical Stations; needs them.

### G2 — "Crowd Controller"
| Phase | Card | Behaviour change |
|---|---|---|
| Early | Concussion | Player uses blasts to *shape* the crowd, not to kill it. |
| Early | Move Speed Up | Push-then-reposition becomes the loop. |
| Mid | Ordnance Core | Bombs land in the gaps Concussion opens. |
| Mid | Kinetic Shield | Movement between pushes is now defended. |
| Late | Chain Lightning | Clears the shoved crowd without re-approaching. |
| Late | Concussion rank 3 | 50 % slow makes lanes stay open. |

Strength: best survivability of any shotgun build; creates space for stations.
Weakness: lowest kill speed; can push enemies out of its own range.
Contrast with G1: G1 kills what it touches; G2 rearranges the battlefield.

---

## Marksman

### K1 — "Long Line"
| Phase | Card | Behaviour change |
|---|---|---|
| Early | Longshot | Player actively backs away to increase damage. |
| Early | Move Speed Up | Retreat lanes become the positioning game. |
| Mid | Breach-style pierce via Damage Up | Lines of retreating shots hit more. |
| Mid | Chain Lightning | Covers the close range Longshot punishes. |
| Late | Kinetic Shield | Survives the one pack that closes. |
| Late | Longshot rank 3 | Max-range shots dominate. |

Strength: enormous per-shot value; safest against slow archetypes.
Weakness: Runner archetypes (CatBolt 5.2 spd, CatLightning 5.0) close the gap.
⚠ Tension: this build is rewarded for *retreating*, which pulls against "move with purpose".

### K2 — "Hunter"
| Phase | Card | Behaviour change |
|---|---|---|
| Early | Hunter's Mark | Player stops choosing targets; the build chooses elites. |
| Early | Execution Round | Marked elites die inside two shots. |
| Mid | Ordnance Core | Trash is handled automatically while hunting. |
| Mid | Damage Up | Raises first-hit burst. |
| Late | Emergency Detonation | Survives being swarmed mid-hunt. |
| Late | Hunter's Mark rank 3 | First hit becomes a near-execution. |

Strength: fastest Boss Beacon clears in the game.
Weakness: helpless in an undifferentiated swarm with no priority targets.
Contrast with K1: K1 controls *distance*, K2 controls *target selection*.

---

## LMG

### L1 — "Anchor"
| Phase | Card | Behaviour change |
|---|---|---|
| Early | Heavy Pressure | Player picks a spot and commits, accepting the slow. |
| Early | Max Health Up | Makes commitment survivable. |
| Mid | Chain Lightning | Damage continues while the player is slow. |
| Mid | Damage Up | Raises the charged output. |
| Late | Emergency Detonation | Breaks a commitment that went wrong. |
| Late | Heavy Pressure rank 3 | Full charge becomes worth the immobility. |

Strength: highest sustained output; ideal at Signal Relay hold-zones.
Weakness: burrowers emerging inside the position; flanks.
Map decisions: Signal Relay is its *best* station — a nice inversion of the Sidearm.

### L2 — "Suppressor"
| Phase | Card | Behaviour change |
|---|---|---|
| Early | Shockwave Belt | Player faces the densest direction to farm cone procs. |
| Early | Move Speed Up | Offsets the family's mobility penalty. |
| Mid | Ordnance Core | Second automatic AoE source. |
| Mid | Fire Rate Up | More bullets = more shockwaves. |
| Late | Soul Burst | Three overlapping AoE sources. |
| Late | Shockwave rank 3 | Wide cones cover approach lanes. |

Strength: continuous area denial without standing still.
Weakness: lowest single-target damage in the game.
Contrast with L1: L1 trades mobility for damage; L2 keeps moving and trades single-target away.

---

## Coverage check

| Family | Builds | Differ by position/target, not just DPS |
|---|---|---|
| Sidearm | S1, S2 | avoid the pack vs enter it |
| SMG | M1, M2 | clear speed vs survival/economy |
| AssaultRifle | A1, A2 | hold a target vs build lines |
| Shotgun | G1, G2 | kill vs rearrange |
| Marksman | K1, K2 | control distance vs control target |
| LMG | L1, L2 | commit position vs mobile area denial |

**12 simulations, 2 per active family.** Note the deliberate inversion: the Sidearm build hates
hold-zones and the LMG build loves them, so the same Signal Relay station produces opposite decisions
depending on the weapon — which is the clearest evidence that the family grammar is doing real work.
