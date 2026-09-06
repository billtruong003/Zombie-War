# M6.1 Step 6 — Full skill catalog (exactly 23 candidates)

**Status:** CANDIDATE SHORTLIST awaiting owner approval. Nothing here is implemented.

Provenance: the 5 stat cards are `FACT` (they exist and are consumed at runtime). Every one of the other 18 is a `DESIGN HYPOTHESIS`. Every number marked TUNING is provisional.

**Counts:** 23 total — Stat 5 · Signature 12 · Autonomous 4 · Universal 2. Status: MUST 15 · SHOULD 7 · LATER 1. **20 of 23 need at least one new runtime primitive.**

Machine-readable form: `skill_catalog.csv` (same 23 rows, all fields).

---

## Offer contract

```text
Slot A   prefers a signature card compatible with the equipped weapon family
Slot B   an autonomous or universal card
Slot C   any valid card

Hard rules
- at most ONE pure-stat card in any offer
- never offer a card whose requiredFamily does not match the equipped weapon
- never offer a card already at maxRank
- never offer a card whose prerequisite is unmet or whose exclusion is held
- early run  (level 1-4)  : bias toward NEW mechanic unlocks  (newSkillWeight)
- later run  (level 5+)   : bias toward coherent rank-ups     (rankUpWeight)
- offers are deterministic from (runSeed, levelIndex)
- on the 30 s timeout the highlighted card is auto-picked; it must always be valid
- exhausted pool: if fewer than 3 valid cards exist, fill remaining slots with the
  highest-weight valid rank-up; if none exist, offer stat cards; never show an empty slot
```

Trigger vocabulary used: `Passive, OnShot, OnHit, OnKill, OnDistanceTravelled, OnContinuousFire, OnTargetChanged, OnHealthThreshold, OnTimer, OnDamageTaken`.

Targeting vocabulary used: `Self, CurrentTarget, NearestEnemy, PriorityEnemy, DensestCluster, EnemiesAroundPlayer, EnemiesInCone, EnemiesInLine`.

Rank 1 unlocks the behaviour; ranks 2–3 strengthen the same fantasy and never change its rules.

---

## Stat cards (5)

### stat.damage — Damage Up  ·  **MUST**  ·  cost LOW

> All weapon damage increases.

Multiplies outgoing weapon damage. Pure filler: it smooths the power curve and never carries a build.

| | |
|---|---|
| **Identity** | icon `powerstrike` · rarity Common · maxRank 5 |
| **Classification** | layer Stat · family Any · fireModes All · synergy `-` · effect `Damage` |
| **Eligibility** | minLevel 1 · requires - · blocked - · prereq - · exclusions - |
| **Offer** | baseW 100 · newW 100 · rankUpW 80 · familyBias none · autoPick yes · duplicates RankUp |
| **Trigger** | `Passive` · threshold - · cooldown - · reset - |
| **Effect** | StatMultiplier → `Self` · base +15% damage · perRank +10% damage · duration run · radius - · maxTargets - · maxStacks 5 · Multiplicative · source Weapon |
| **Balance** | budget 1.0 · contributes DPS · softCap none · hardCap rank 5 · Multiplicative · procLimit - |
| **Feedback** | VFX none · SFX card confirm · HUD build list entry · colour grey · readability numeric only |
| **Tech** | existing: RunPerkKind.Damage consumed at Weapon.cs:650 (FACT) · **new: none** · pooling - · alloc 0 · queries 0 · spawnCap 0 · WebGL none · deterministic |
| **Verify** | unit: multiplier stacks multiplicatively · play: applies to all fire modes · visual: - · perf: - · question: *Is it ever the best of three?* · fail: Always chosen -> other cards too weak |

### stat.firerate — Fire Rate Up  ·  **MUST**  ·  cost LOW

> Weapon fires faster, up to a soft cap.

Raises fire rate with diminishing returns near the cap so rapid families cannot break audio or VFX budgets.

| | |
|---|---|
| **Identity** | icon `machine` · rarity Common · maxRank 5 |
| **Classification** | layer Stat · family Any · fireModes All · synergy `Rapid` · effect `FireRate` |
| **Eligibility** | minLevel 1 · requires - · blocked - · prereq - · exclusions - |
| **Offer** | baseW 100 · newW 100 · rankUpW 80 · familyBias none · autoPick yes · duplicates RankUp |
| **Trigger** | `Passive` · threshold - · cooldown - · reset - |
| **Effect** | StatMultiplier → `Self` · base +12% fire rate · perRank +8% fire rate · duration run · radius - · maxTargets - · maxStacks 5 · Multiplicative · source - |
| **Balance** | budget 1.0 · contributes DPS · softCap 2.2x base rate · hardCap 2.5x base rate · Multiplicative · procLimit - |
| **Feedback** | VFX none · SFX cadence audibly faster · HUD build list entry · colour grey · readability audible |
| **Tech** | existing: RunPerkKind.FireRate consumed at Weapon.cs:120 (FACT) · **new: soft-cap curve** · pooling - · alloc 0 · queries 0 · spawnCap 0 · WebGL rapid-fire audio voice cap · deterministic |
| **Verify** | unit: soft cap never exceeded · play: LMG at cap does not exceed audio budget · visual: - · perf: shot audio voices <= cap · question: *Does the cap feel bad?* · fail: Cap makes the card feel dead late |

### stat.movespeed — Move Speed Up  ·  **MUST**  ·  cost LOW

> You move faster.

Raises movement speed. Supports every movement-triggered skill.

| | |
|---|---|
| **Identity** | icon `runner` · rarity Common · maxRank 5 |
| **Classification** | layer Stat · family Any · fireModes All · synergy `Move` · effect `Mobility` |
| **Eligibility** | minLevel 1 · requires - · blocked - · prereq - · exclusions - |
| **Offer** | baseW 100 · newW 100 · rankUpW 80 · familyBias Sidearm+10 · autoPick yes · duplicates RankUp |
| **Trigger** | `Passive` · threshold - · cooldown - · reset - |
| **Effect** | StatMultiplier → `Self` · base +10% move speed · perRank +7% move speed · duration run · radius - · maxTargets - · maxStacks 5 · Multiplicative · source - |
| **Balance** | budget 1.0 · contributes Survival+DPS via Run&Gun · softCap 1.6x base · hardCap 1.8x base · Multiplicative · procLimit - |
| **Feedback** | VFX none · SFX - · HUD build list entry · colour grey · readability felt immediately |
| **Tech** | existing: RunPerkKind.MoveSpeed consumed at PlayerMovement.cs:78 (FACT) · **new: soft cap** · pooling - · alloc 0 · queries 0 · spawnCap 0 · WebGL none · deterministic |
| **Verify** | unit: cap respected · play: camera keeps up at max speed · visual: - · perf: streaming keeps up at max speed · question: *Does the world load fast enough?* · fail: Player outruns chunk streaming |

### stat.maxhealth — Max Health Up  ·  **MUST**  ·  cost LOW

> Max and current health increase now.

Raises max health and heals the same amount at pick time, so it is useful even when taken while hurt.

| | |
|---|---|
| **Identity** | icon `sturdy` · rarity Common · maxRank 5 |
| **Classification** | layer Stat · family Any · fireModes All · synergy `-` · effect `Survival` |
| **Eligibility** | minLevel 1 · requires - · blocked - · prereq - · exclusions - |
| **Offer** | baseW 100 · newW 100 · rankUpW 80 · familyBias Shotgun+10 · autoPick yes · duplicates RankUp |
| **Trigger** | `Passive` · threshold - · cooldown - · reset - |
| **Effect** | StatAdd → `Self` · base +20% max HP and heal the same · perRank +15% · duration run · radius - · maxTargets - · maxStacks 5 · Additive · source - |
| **Balance** | budget 1.0 · contributes Survival · softCap none · hardCap rank 5 · Additive · procLimit - |
| **Feedback** | VFX brief heal flash · SFX heal · HUD health bar grows · colour grey · readability visible on the bar |
| **Tech** | existing: Health.IncreaseMax applied at RunOverlays.cs:375 (FACT) · **new: none** · pooling - · alloc 0 · queries 0 · spawnCap 0 · WebGL none · deterministic |
| **Verify** | unit: max and current both rise · play: taking it at low HP heals · visual: health bar · perf: - · question: *Is it ever better than damage?* · fail: Never picked -> defensive play is unrewarded |

### stat.coingain — Coin Gain Up  ·  **SHOULD**  ·  cost LOW

> You earn more Coin.

Raises Coin gain. Only meaningful once Coin has in-run sinks; otherwise it is a dead card.

| | |
|---|---|
| **Identity** | icon `packaging` · rarity Common · maxRank 3 |
| **Classification** | layer Stat · family Any · fireModes All · synergy `-` · effect `Economy` |
| **Eligibility** | minLevel 1 · requires - · blocked - · prereq - · exclusions - |
| **Offer** | baseW 70 · newW 70 · rankUpW 50 · familyBias none · autoPick yes · duplicates RankUp |
| **Trigger** | `Passive` · threshold - · cooldown - · reset - |
| **Effect** | StatMultiplier → `Self` · base +25% Coin · perRank +15% Coin · duration run · radius - · maxTargets - · maxStacks 3 · Multiplicative · source - |
| **Balance** | budget 0.6 · contributes Economy · softCap none · hardCap rank 3 · Multiplicative · procLimit - |
| **Feedback** | VFX none · SFX - · HUD build list entry · colour grey · readability numeric only |
| **Tech** | existing: RunPerkKind.CoinGain consumed at RunState.cs:129 (FACT) · **new: none** · pooling - · alloc 0 · queries 0 · spawnCap 0 · WebGL none · deterministic |
| **Verify** | unit: applies once at the ledger · play: Supply Cache affordable earlier · visual: - · perf: - · question: *Does Coin matter in-run yet?* · fail: No in-run sink -> card is dead; then demote to LATER |

## Signature cards (12)

### sidearm.rungun — Run & Gun  ·  **MUST**  ·  cost LOW

> Moving makes you shoot faster; standing still loses it.

While moving above a speed threshold, fire rate ramps up. Stopping decays the bonus over a short window.

| | |
|---|---|
| **Identity** | icon `runner` · rarity Uncommon · maxRank 3 |
| **Classification** | layer Signature · family Sidearm · fireModes SingleHitscan · synergy `Move,Rapid` · effect `FireRate,Mobility` |
| **Eligibility** | minLevel 2 · requires Sidearm · blocked LMG · prereq - · exclusions - |
| **Offer** | baseW 120 · newW 140 · rankUpW 90 · familyBias Sidearm+60 · autoPick yes · duplicates RankUp |
| **Trigger** | `OnDistanceTravelled` · threshold moving above 60% max speed · cooldown - · reset decays over 1.5s when below threshold |
| **Effect** | ConditionalStat → `Self` · base +25% fire rate while moving · perRank +12% · duration while condition holds · radius - · maxTargets - · maxStacks 1 · Replace · source - |
| **Balance** | budget 2.0 · contributes DPS conditional on mobility · softCap shares stat.firerate cap · hardCap shared cap · Multiplicative · procLimit - |
| **Feedback** | VFX subtle muzzle cadence · SFX audible rate change · HUD small ramp pip · colour cyan · readability audible within 1s of moving |
| **Tech** | existing: Weapon.PerkedFireRate + PlayerMovement velocity (FACT) · **new: velocity threshold sampler + decay timer** · pooling - · alloc 0 · queries 0 · spawnCap 0 · WebGL none · deterministic |
| **Verify** | unit: ramp rises and decays on threshold cross · play: no bonus while stationary · visual: HUD pip · perf: 0 allocations per frame · question: *Does the player notice they must keep moving?* · fail: Player cannot tell it is active |

### sidearm.quickstep — Quickstep Round  ·  **MUST**  ·  cost LOW

> Every N metres moved, fire an empowered shot.

Travelling accumulates distance. On reaching the threshold the NEXT automatically fired shot is empowered. The counter resets when that shot is consumed. The player never has to withhold fire.

| | |
|---|---|
| **Identity** | icon `runningstrike` · rarity Uncommon · maxRank 3 |
| **Classification** | layer Signature · family Sidearm · fireModes SingleHitscan,MultiPelletHitscan · synergy `Move,Crit` · effect `Damage,Burst` |
| **Eligibility** | minLevel 2 · requires Sidearm · blocked - · prereq - · exclusions - |
| **Offer** | baseW 120 · newW 140 · rankUpW 90 · familyBias Sidearm+60 · autoPick yes · duplicates RankUp |
| **Trigger** | `OnDistanceTravelled` · threshold N metres (TUNING) · cooldown - · reset on empowered shot consumed |
| **Effect** | NextShotEmpower → `CurrentTarget` · base next shot deals x2.5 damage · perRank -15% distance required · duration until consumed · radius - · maxTargets 1 · maxStacks 1 · Replace · source Weapon |
| **Balance** | budget 2.0 · contributes Burst DPS · softCap one charge stored · hardCap one charge stored · Multiplicative · procLimit one per threshold |
| **Feedback** | VFX bright tracer + hit flash · SFX distinct crack · HUD charge pip fills · colour cyan · readability pip full = next shot is the big one |
| **Tech** | existing: damage multiply path exists (Weapon.cs:650) · **new: distance accumulator + one-shot empower flag** · pooling - · alloc 0 · queries 0 · spawnCap 1 tracer · WebGL none · deterministic |
| **Verify** | unit: counter resets exactly on consumption; never fires without a valid target · play: works under auto-fire with no player input · visual: tracer distinct from normal shots · perf: 0 alloc · question: *Can the player feel the empowered shot land?* · fail: REJECTED VARIANT: any version requiring the player to stop firing is invalid under auto-fire |

### smg.static — Static Build-up  ·  **MUST**  ·  cost MEDIUM

> Consecutive hits charge a lightning discharge.

Each hit adds charge. At full charge the next hit discharges chain lightning through nearby enemies.

| | |
|---|---|
| **Identity** | icon `energetic` · rarity Rare · maxRank 3 |
| **Classification** | layer Signature · family SMG · fireModes SingleHitscan · synergy `Rapid,Shock` · effect `ChainDamage` |
| **Eligibility** | minLevel 3 · requires SMG · blocked Marksman · prereq - · exclusions - |
| **Offer** | baseW 110 · newW 130 · rankUpW 90 · familyBias SMG+60 · autoPick yes · duplicates RankUp |
| **Trigger** | `OnHit` · threshold N consecutive hits (TUNING) · cooldown 0.5s internal · reset charge decays if no hit for 2s |
| **Effect** | ChainDamage → `NearestEnemy from hit point` · base arc to 3 enemies · perRank +1 enemy · duration instant · radius 4m per jump · maxTargets 5 · maxStacks 1 · Replace · source Power |
| **Balance** | budget 3.0 · contributes Crowd DPS · softCap 5 targets · hardCap 5 targets · Additive to hit · procLimit max 2 discharges/second |
| **Feedback** | VFX arc between targets · SFX rising whine then crack · HUD charge meter · colour electric blue · readability meter fills visibly |
| **Tech** | existing: NONE for chaining — FireMode.ChainLightning is an enum value only; Weapon.cs implements only PiercingLine among special modes; chainCount=0 on all 25 assets (FACT) · **new: chain target selection + arc VFX + charge meter** · pooling arc VFX pooled · alloc 0 per arc after warmup · queries 1 OverlapSphere per jump · spawnCap 5 arcs per discharge · WebGL query + particle cost — must be capped · deterministic given seed |
| **Verify** | unit: never exceeds maxTargets; never re-hits the same enemy in one discharge · play: discharge visible against 40 enemies · visual: arc capture · perf: <= 2 discharges/s, <= 5 arcs each, 0 alloc steady state · question: *Is the discharge readable in a crowd?* · fail: Arc spam makes the screen unreadable |

### smg.bullethose — Bullet Hose  ·  **MUST**  ·  cost LOW

> Holding fire ramps rate and spread; losing the target decays it.

Continuous fire on a valid target ramps fire rate and spread toward a cap. Losing the target decays both.

| | |
|---|---|
| **Identity** | icon `machine` · rarity Uncommon · maxRank 3 |
| **Classification** | layer Signature · family SMG · fireModes SingleHitscan · synergy `Rapid` · effect `FireRate,Spread` |
| **Eligibility** | minLevel 2 · requires SMG · blocked Marksman · prereq - · exclusions - |
| **Offer** | baseW 120 · newW 140 · rankUpW 90 · familyBias SMG+60 · autoPick yes · duplicates RankUp |
| **Trigger** | `OnContinuousFire` · threshold 1s sustained · cooldown - · reset OnTargetChanged or fire stop, decay over 1s |
| **Effect** | RampStat → `Self` · base +40% fire rate, +30% spread at cap · perRank +15% rate at cap · duration while sustained · radius - · maxTargets - · maxStacks 1 · Replace · source - |
| **Balance** | budget 2.0 · contributes Sustained DPS, worse precision · softCap shares fire-rate cap · hardCap shared cap · Multiplicative · procLimit - |
| **Feedback** | VFX barrel heat tint · SFX spin-up · HUD ramp bar · colour orange · readability audible spin-up |
| **Tech** | existing: Weapon.PerkedFireRate; spreadAngle field exists on WeaponData (FACT) · **new: ramp timer + spread modulation** · pooling - · alloc 0 · queries 0 · spawnCap 0 · WebGL audio voice cap at high rate · deterministic |
| **Verify** | unit: decays on target change · play: spread increase is visible · visual: spread cone capture · perf: audio voices capped · question: *Is the accuracy cost felt?* · fail: Pure upside -> no decision |

### ar.focusfire — Focus Fire  ·  **MUST**  ·  cost MEDIUM

> Hits on the same target ramp damage; switching resets it.

Consecutive hits on one target ramp damage. Changing target resets the ramp explicitly and visibly.

| | |
|---|---|
| **Identity** | icon `ofi` · rarity Rare · maxRank 3 |
| **Classification** | layer Signature · family AssaultRifle · fireModes SingleHitscan · synergy `Precision` · effect `Damage,Ramp` |
| **Eligibility** | minLevel 3 · requires AssaultRifle · blocked Shotgun · prereq - · exclusions - |
| **Offer** | baseW 110 · newW 130 · rankUpW 90 · familyBias AssaultRifle+60 · autoPick yes · duplicates RankUp |
| **Trigger** | `OnHit` · threshold consecutive hits on the same target · cooldown - · reset OnTargetChanged (immediate, with a visible cue) |
| **Effect** | RampDamage → `CurrentTarget` · base +8% damage per hit · perRank +4% per hit · duration while target held · radius - · maxTargets 1 · maxStacks 8 · Additive · source Weapon |
| **Balance** | budget 2.5 · contributes Single-target DPS · softCap 8 stacks · hardCap 8 stacks · Additive · procLimit - |
| **Feedback** | VFX target-side impact escalation · SFX pitch rises with stacks · HUD stack pips on the target · colour amber · readability pips on the enemy, not a corner HUD |
| **Tech** | existing: damage multiply path (Weapon.cs:650); auto-target already tracks a current target · **new: per-target hit counter with reset on target change** · pooling - · alloc 0 · queries 0 · spawnCap 0 · WebGL none · deterministic |
| **Verify** | unit: reset is exact on target change · play: auto-aim switching does not silently farm stacks · visual: stack pips · perf: 0 alloc · question: *Does auto-target make this uncontrollable?* · fail: Auto-aim retargets so often the ramp never builds -> cut or move to Marksman |

### ar.breach — Breach Round  ·  **SHOULD**  ·  cost MEDIUM

> Every Nth shot pierces and leaves the target Exposed.

A periodic shot pierces through targets and applies a short Exposed state that increases damage taken.

| | |
|---|---|
| **Identity** | icon `powerstrike` · rarity Rare · maxRank 3 |
| **Classification** | layer Signature · family AssaultRifle · fireModes SingleHitscan · synergy `Precision,Pierce` · effect `Pierce,Debuff` |
| **Eligibility** | minLevel 4 · requires AssaultRifle · blocked - · prereq - · exclusions - |
| **Offer** | baseW 100 · newW 120 · rankUpW 85 · familyBias AssaultRifle+60 · autoPick yes · duplicates RankUp |
| **Trigger** | `OnShot` · threshold every Nth shot (TUNING) · cooldown - · reset on fire |
| **Effect** | PierceAndDebuff → `EnemiesInLine` · base pierce 2, Exposed +20% damage taken for 3s · perRank +1 pierce · duration 3s Exposed · radius - · maxTargets 3 · maxStacks 1 · RefreshDuration · source Weapon |
| **Balance** | budget 3.0 · contributes Single-target + light crowd · softCap pierce 4 · hardCap pierce 4 · Multiplicative on incoming · procLimit one per N shots |
| **Feedback** | VFX heavier tracer · SFX deeper report · HUD Exposed marker on the enemy · colour amber · readability marker on the enemy silhouette |
| **Tech** | existing: pierceCount/pierceDamageFalloff fields exist; PiercingLine fire mode IS implemented (Weapon.cs:618) (FACT) · **new: shot counter + Exposed status carrier on the enemy** · pooling - · alloc 0 · queries RaycastAll on the Nth shot only · spawnCap 1 tracer · WebGL RaycastAll bounded by N · deterministic |
| **Verify** | unit: Exposed applies and expires; pierce respects cap · play: visible on a boss · visual: Exposed marker · perf: RaycastAll frequency bounded · question: *Is Exposed legible to the player?* · fail: Exposed becomes an invisible bookkeeping buff -> cut |

### shotgun.pointblank — Point Blank  ·  **MUST**  ·  cost LOW

> Damage rises sharply at close range.

Applies a steep damage bonus inside a short radius, rewarding the shotgun's positional risk.

| | |
|---|---|
| **Identity** | icon `fist` · rarity Uncommon · maxRank 3 |
| **Classification** | layer Signature · family Shotgun · fireModes MultiPelletHitscan · synergy `Close` · effect `Damage` |
| **Eligibility** | minLevel 2 · requires Shotgun · blocked Marksman · prereq - · exclusions - |
| **Offer** | baseW 120 · newW 140 · rankUpW 90 · familyBias Shotgun+60 · autoPick yes · duplicates RankUp |
| **Trigger** | `OnHit` · threshold hit within R metres (TUNING) · cooldown - · reset - |
| **Effect** | ConditionalDamage → `CurrentTarget` · base +60% damage inside R · perRank +25% · duration instant · radius R metres · maxTargets all pellets · maxStacks 1 · Replace · source Weapon |
| **Balance** | budget 2.0 · contributes Burst DPS at risk · softCap none · hardCap rank 3 · Multiplicative · procLimit - |
| **Feedback** | VFX heavier impact + screen kick · SFX fuller boom · HUD none needed · colour red · readability impact weight makes it obvious |
| **Tech** | existing: WeaponData.RangeFalloff(distance01) exists and is applied at Weapon.cs:652 (FACT) · **new: close-range bonus curve (extends an existing curve)** · pooling - · alloc 0 · queries 0 · spawnCap 0 · WebGL none · deterministic |
| **Verify** | unit: bonus applies per pellet consistently · play: range boundary feels fair · visual: impact capture · perf: 0 alloc · question: *Does it justify closing the distance?* · fail: Player still kites -> risk is not paying |

### shotgun.concussion — Concussion  ·  **MUST**  ·  cost LOW

> Blasts shove enemies back and briefly slow them.

Each blast applies knockback and a short slow to everything it hits, converting the shotgun into a space tool.

| | |
|---|---|
| **Identity** | icon `highkick` · rarity Uncommon · maxRank 3 |
| **Classification** | layer Signature · family Shotgun · fireModes MultiPelletHitscan · synergy `Close,Control` · effect `Knockback,Slow` |
| **Eligibility** | minLevel 2 · requires Shotgun · blocked - · prereq - · exclusions - |
| **Offer** | baseW 120 · newW 140 · rankUpW 90 · familyBias Shotgun+60 · autoPick yes · duplicates RankUp |
| **Trigger** | `OnHit` · threshold any blast hit · cooldown - · reset - |
| **Effect** | KnockbackAndSlow → `EnemiesInCone` · base push + 30% slow for 1s · perRank +10% slow, +push · duration 1s slow · radius blast cone · maxTargets 8 · maxStacks 1 · RefreshDuration · source - |
| **Balance** | budget 2.5 · contributes Control / survival · softCap slow 50% · hardCap slow 50% · Replace · procLimit - |
| **Feedback** | VFX bodies visibly shoved · SFX concussive thump · HUD none · colour red · readability the shove IS the feedback |
| **Tech** | existing: WeaponData.knockback + ZombieBase.ApplyPhysicalPush called at Weapon.cs:666 (FACT) · **new: slow status on the enemy motor** · pooling - · alloc 0 · queries 0 extra (reuses pellet hits) · spawnCap 0 · WebGL crowd steering stability under mass push · deterministic |
| **Verify** | unit: slow expires; push respects crowd separation · play: pushing 20 enemies does not destabilise steering · visual: crowd push capture · perf: steering stable with 40 enemies · question: *Does it create usable space?* · fail: Push scatters enemies so far the player cannot kill them |

### lmg.heavypressure — Heavy Pressure  ·  **SHOULD**  ·  cost MEDIUM

> Sustained fire ramps damage and control, but slows you down.

Holding fire builds a pressure charge that raises damage and adds a light slow to hits. The charge trades movement speed away as it grows.

| | |
|---|---|
| **Identity** | icon `sturdy` · rarity Rare · maxRank 3 |
| **Classification** | layer Signature · family LMG · fireModes SingleHitscan · synergy `Heavy,Control` · effect `Damage,Slow,MobilityCost` |
| **Eligibility** | minLevel 4 · requires LMG · blocked Sidearm · prereq - · exclusions - |
| **Offer** | baseW 100 · newW 120 · rankUpW 85 · familyBias LMG+60 · autoPick yes · duplicates RankUp |
| **Trigger** | `OnContinuousFire` · threshold sustained fire · cooldown - · reset decays on fire stop |
| **Effect** | RampDamageAndSlowSelf → `Self+CurrentTarget` · base +30% damage at full charge, -25% move speed at full charge · perRank +12% damage · duration while sustained · radius - · maxTargets - · maxStacks 1 · Replace · source Weapon |
| **Balance** | budget 3.0 · contributes Sustained DPS with a mobility cost · softCap charge 100% · hardCap -40% move speed floor · Multiplicative · procLimit - |
| **Feedback** | VFX barrel glow at charge · SFX deepening roar · HUD charge bar · colour orange · readability movement change is felt |
| **Tech** | existing: damage multiply + move-speed multiply both exist (FACT) · **new: charge accumulator with self-debuff** · pooling - · alloc 0 · queries 0 · spawnCap 0 · WebGL none · deterministic |
| **Verify** | unit: speed floor respected · play: player can still escape at full charge · visual: charge bar · perf: 0 alloc · question: *Does the mobility cost create a real decision?* · fail: Rewards standing still with no downside -> conflicts with Move-with-purpose pillar |

### lmg.shockwave — Shockwave Belt  ·  **LATER**  ·  cost MEDIUM

> Every N bullets sends a shockwave cone forward.

A bullet counter periodically emits a forward cone shockwave that damages and staggers.

| | |
|---|---|
| **Identity** | icon `fighter` · rarity Rare · maxRank 3 |
| **Classification** | layer Signature · family LMG · fireModes SingleHitscan · synergy `Heavy,Blast` · effect `ConeDamage,Stagger` |
| **Eligibility** | minLevel 4 · requires LMG · blocked - · prereq - · exclusions - |
| **Offer** | baseW 90 · newW 110 · rankUpW 80 · familyBias LMG+60 · autoPick yes · duplicates RankUp |
| **Trigger** | `OnShot` · threshold every N bullets (TUNING) · cooldown - · reset on emit |
| **Effect** | ConeDamage → `EnemiesInCone` · base cone damage + brief stagger · perRank +cone width · duration instant · radius cone 6m · maxTargets 10 · maxStacks 1 · Replace · source Power |
| **Balance** | budget 3.0 · contributes Crowd DPS · softCap 10 targets · hardCap 10 targets · Additive · procLimit max 1 per 0.7s |
| **Feedback** | VFX cone wave · SFX heavy thud · HUD bullet counter pip · colour orange · readability cone is visible |
| **Tech** | existing: none for cone queries · **new: cone overlap query + wave VFX** · pooling wave VFX pooled · alloc 0 after warmup · queries 1 OverlapSphere + angle filter per emit · spawnCap 1 wave · WebGL query frequency bounded by N · deterministic |
| **Verify** | unit: cone respects angle and cap · play: readable in a crowd · visual: cone capture · perf: <= 1.4 emits/s · question: *Is it distinct from Soul Burst?* · fail: Reads the same as Soul Burst -> merge or cut |

### marksman.longshot — Longshot  ·  **SHOULD**  ·  cost LOW

> The further the target, the more damage and pierce.

Damage and pierce scale with distance to the target, rewarding the marksman's positioning.

| | |
|---|---|
| **Identity** | icon `backstab` · rarity Rare · maxRank 3 |
| **Classification** | layer Signature · family Marksman · fireModes PiercingLine · synergy `Precision,Pierce` · effect `Damage,Pierce` |
| **Eligibility** | minLevel 3 · requires Marksman · blocked Shotgun · prereq - · exclusions - |
| **Offer** | baseW 110 · newW 130 · rankUpW 90 · familyBias Marksman+60 · autoPick yes · duplicates RankUp |
| **Trigger** | `OnHit` · threshold distance to target · cooldown - · reset - |
| **Effect** | DistanceScaledDamage → `EnemiesInLine` · base +10% damage per 4m, +1 pierce beyond 10m · perRank +5% per 4m · duration instant · radius - · maxTargets 4 · maxStacks 1 · Replace · source Weapon |
| **Balance** | budget 2.0 · contributes Single-target + line DPS · softCap +80% damage · hardCap +80% · Multiplicative · procLimit - |
| **Feedback** | VFX longer tracer at distance · SFX sharper crack · HUD none · colour white · readability damage numbers show the difference |
| **Tech** | existing: PiercingLine fire mode implemented (Weapon.cs:618); pierceCount=3 already on the marksman asset (FACT) · **new: distance-scaled damage curve** · pooling - · alloc 0 · queries existing RaycastAll · spawnCap 1 tracer · WebGL none · deterministic |
| **Verify** | unit: scaling clamps at cap · play: close-range penalty is felt · visual: tracer length · perf: 0 alloc · question: *Does it push the player to hold distance?* · fail: Player camps at max range and never moves -> conflicts with the movement pillar |

### marksman.hunters — Hunter's Mark  ·  **SHOULD**  ·  cost MEDIUM

> Targets bosses and elites first, and the first hit on them is empowered.

Overrides target selection to prefer boss/elite/special enemies, and empowers the first hit on a newly marked target.

| | |
|---|---|
| **Identity** | icon `falc_mixture` · rarity Rare · maxRank 3 |
| **Classification** | layer Signature · family Marksman · fireModes PiercingLine,SingleHitscan · synergy `Precision` · effect `Targeting,Damage` |
| **Eligibility** | minLevel 4 · requires Marksman · blocked - · prereq - · exclusions - |
| **Offer** | baseW 100 · newW 120 · rankUpW 85 · familyBias Marksman+60 · autoPick yes · duplicates RankUp |
| **Trigger** | `OnTargetChanged` · threshold a priority enemy is in range · cooldown 1s per target · reset on target death |
| **Effect** | TargetPriorityAndEmpower → `PriorityEnemy` · base prefer elite/boss; first hit x2 damage · perRank +50% first-hit damage · duration until target dies · radius weapon range · maxTargets 1 · maxStacks 1 · Replace · source Weapon |
| **Balance** | budget 2.5 · contributes Boss DPS + target control · softCap one mark · hardCap one mark · Multiplicative · procLimit 1 empower per target |
| **Feedback** | VFX mark icon over the target · SFX lock-on tone · HUD mark on the enemy · colour white · readability mark icon is explicit |
| **Tech** | existing: auto-target selection exists; ZombieData.isElite flag exists (FACT) · **new: target-priority override + per-target empower record** · pooling - · alloc 0 · queries reuses target scan · spawnCap 1 marker · WebGL none · deterministic |
| **Verify** | unit: never marks two targets; empower fires once per target · play: does not fight the player's intent in a crowd · visual: mark capture · perf: 0 extra queries · question: *Does overriding auto-aim feel helpful or annoying?* · fail: Player loses agency over what they shoot -> demote to LATER |

## Autonomous cards (4)

### auto.chainlightning — Chain Lightning  ·  **MUST**  ·  cost MEDIUM

> Lightning periodically arcs through nearby enemies.

On a timer, a bolt strikes a nearby enemy and arcs to further enemies within range.

| | |
|---|---|
| **Identity** | icon `energetic` · rarity Rare · maxRank 3 |
| **Classification** | layer Autonomous · family Any · fireModes All · synergy `Shock` · effect `ChainDamage` |
| **Eligibility** | minLevel 3 · requires - · blocked - · prereq - · exclusions - |
| **Offer** | baseW 110 · newW 130 · rankUpW 90 · familyBias SMG+20 · autoPick yes · duplicates RankUp |
| **Trigger** | `OnTimer` · threshold every T seconds (TUNING) · cooldown T · reset - |
| **Effect** | ChainDamage → `NearestEnemy then chain` · base 3 targets · perRank +1 target, -10% interval · duration instant · radius 5m per jump · maxTargets 6 · maxStacks 1 · Replace · source Power |
| **Balance** | budget 3.0 · contributes Crowd DPS, hands-free · softCap 6 targets · hardCap 6 targets · Additive · procLimit 1 per interval |
| **Feedback** | VFX arc chain · SFX thunder · HUD cooldown ring · colour electric blue · readability cooldown ring + unmistakable arc |
| **Tech** | existing: NONE — FireMode.ChainLightning is an unimplemented enum; no chain code exists (FACT) · **new: chain target selection, arc VFX, power cooldown framework** · pooling arc VFX pooled · alloc 0 after warmup · queries 1 OverlapSphere per jump · spawnCap 6 arcs per proc · WebGL cap arcs and particle count · deterministic given seed |
| **Verify** | unit: no duplicate targets in one chain; respects cap · play: visible with 40 enemies on screen · visual: arc capture · perf: <= 1 proc/s, <= 6 arcs, 0 steady-state alloc · question: *Does it feel like the build is doing something?* · fail: Invisible in a crowd, or tanks frame time on mobile |

### auto.ordnance — Ordnance Core  ·  **MUST**  ·  cost MEDIUM

> Periodically lobs a bomb at the densest group of enemies.

On a timer, a bomb is thrown automatically at the densest nearby cluster and explodes. This is the replacement for the retired manual grenade.

| | |
|---|---|
| **Identity** | icon `alchemy` · rarity Rare · maxRank 3 |
| **Classification** | layer Autonomous · family Any · fireModes All · synergy `Blast` · effect `AoEDamage` |
| **Eligibility** | minLevel 3 · requires - · blocked - · prereq - · exclusions - |
| **Offer** | baseW 110 · newW 130 · rankUpW 90 · familyBias Shotgun+20 · autoPick yes · duplicates RankUp |
| **Trigger** | `OnTimer` · threshold every T seconds (TUNING) · cooldown T · reset - |
| **Effect** | AoEDamage → `DensestCluster` · base one bomb, existing explosion radius · perRank +radius / -interval · duration instant · radius Bomb.explosionRadius · maxTargets all in radius · maxStacks 1 · Replace · source Power |
| **Balance** | budget 3.0 · contributes Crowd DPS, covers range weaknesses · softCap 1 bomb in flight · hardCap 1 bomb in flight · Additive · procLimit 1 per interval |
| **Feedback** | VFX EXISTING Bomb prefab + explosion · SFX EXISTING bomb audio · HUD cooldown ring · colour orange · readability the explosion is the feedback |
| **Tech** | existing: Bomb.Explode() OverlapSphere+TakeDamage exists (Bomb.cs:70-76); Bomb prefab, VFX and audio exist; BombThrower.ReleaseBomb throw physics exists but is driven by manual input (FACT) · **new: densest-cluster query + auto-throw scheduling (NO cluster query exists anywhere in runtime)** · pooling Bomb already pooled (PooledObject) · alloc 0 after warmup · queries 1 clustering pass per proc · spawnCap 1 bomb · WebGL clustering pass must be bounded · deterministic given enemy list order |
| **Verify** | unit: never targets an empty cluster; one bomb in flight · play: does not blow up the player · visual: explosion capture · perf: clustering <= 1/s over <= 64 enemies · question: *Does it feel like help or like noise?* · fail: Bomb lands on the player, or clustering cost spikes |

### auto.soulburst — Soul Burst  ·  **MUST**  ·  cost LOW

> Every N kills, you explode.

A kill counter triggers an explosion centred on the player, clearing the crowd that closed in.

| | |
|---|---|
| **Identity** | icon `rage_potion` · rarity Uncommon · maxRank 3 |
| **Classification** | layer Autonomous · family Any · fireModes All · synergy `Blast,Kill` · effect `AoEDamage` |
| **Eligibility** | minLevel 2 · requires - · blocked - · prereq - · exclusions - |
| **Offer** | baseW 120 · newW 140 · rankUpW 90 · familyBias none · autoPick yes · duplicates RankUp |
| **Trigger** | `OnKill` · threshold every N kills (TUNING) · cooldown 0.5s internal · reset on proc |
| **Effect** | AoEDamage → `EnemiesAroundPlayer` · base radial explosion · perRank +radius / -kills required · duration instant · radius 4m · maxTargets all in radius · maxStacks 1 · Replace · source Power |
| **Balance** | budget 2.0 · contributes Crowd clear + panic relief · softCap - · hardCap max 2 procs/s · Additive · procLimit 2/s |
| **Feedback** | VFX radial shockwave · SFX burst · HUD kill counter pip · colour violet · readability centred on the player, impossible to miss |
| **Tech** | existing: Bomb.Explode() OverlapSphere damage pattern is reusable; RunState.Kills exists (FACT) · **new: per-power kill-count trigger hook** · pooling shockwave VFX pooled · alloc 0 after warmup · queries 1 OverlapSphere per proc · spawnCap 1 shockwave · WebGL bounded by proc limit · deterministic |
| **Verify** | unit: counter resets exactly; proc limit respected · play: does not chain-proc endlessly in a dense wave · visual: shockwave capture · perf: <= 2 procs/s · question: *Does it arrive when the player needs it?* · fail: Procs constantly and becomes background noise |

### auto.emergency — Emergency Detonation  ·  **SHOULD**  ·  cost LOW

> When badly hurt, you explode once to buy space. No heal.

Dropping below a health threshold triggers one explosion around the player on a long cooldown. It grants no healing and no invulnerability — only space.

| | |
|---|---|
| **Identity** | icon `painkillers` · rarity Rare · maxRank 3 |
| **Classification** | layer Autonomous · family Any · fireModes All · synergy `Blast,Defence` · effect `AoEDamage,Panic` |
| **Eligibility** | minLevel 3 · requires - · blocked - · prereq - · exclusions - |
| **Offer** | baseW 100 · newW 120 · rankUpW 85 · familyBias Shotgun+20 · autoPick yes · duplicates RankUp |
| **Trigger** | `OnHealthThreshold` · threshold HP falls below X% (TUNING) · cooldown 30s (TUNING) · reset cooldown expiry |
| **Effect** | AoEDamage → `EnemiesAroundPlayer` · base strong knockback + damage · perRank -cooldown / +radius · duration instant · radius 5m · maxTargets all in radius · maxStacks 1 · Replace · source Power |
| **Balance** | budget 2.0 · contributes Survival · softCap - · hardCap 1 per cooldown · Additive · procLimit 1 per cooldown |
| **Feedback** | VFX red-tinted burst · SFX alarm then blast · HUD cooldown ring + armed indicator · colour red · readability armed state must be visible BEFORE it fires |
| **Tech** | existing: explosion pattern reusable; Health thresholds readable (FACT) · **new: health-threshold trigger with cooldown** · pooling reuses burst VFX · alloc 0 · queries 1 OverlapSphere per proc · spawnCap 1 burst · WebGL negligible · deterministic |
| **Verify** | unit: fires once per cooldown; grants no heal and no i-frames · play: does not save a player who should have died twice · visual: armed indicator capture · perf: negligible · question: *Does it feel like a reprieve or like a free life?* · fail: Becomes a mandatory pick that removes death pressure |

## Universal cards (2)

### uni.execution — Execution Round  ·  **MUST**  ·  cost LOW

> Big bonus damage to badly hurt enemies.

Hits against enemies below a health threshold deal heavily increased damage.

| | |
|---|---|
| **Identity** | icon `punisher` · rarity Uncommon · maxRank 3 |
| **Classification** | layer Universal · family Any · fireModes All · synergy `Crit` · effect `Damage` |
| **Eligibility** | minLevel 2 · requires - · blocked - · prereq - · exclusions - |
| **Offer** | baseW 120 · newW 140 · rankUpW 90 · familyBias none · autoPick yes · duplicates RankUp |
| **Trigger** | `OnHit` · threshold target below 20% HP (TUNING) · cooldown - · reset - |
| **Effect** | ConditionalDamage → `CurrentTarget` · base +80% damage below threshold · perRank +30% or +5% threshold · duration instant · radius - · maxTargets 1 · maxStacks 1 · Replace · source Weapon |
| **Balance** | budget 1.5 · contributes DPS, strongest against tough targets · softCap none · hardCap rank 3 · Multiplicative · procLimit - |
| **Feedback** | VFX execution flash on kill · SFX sharper kill sound · HUD none · colour white · readability kill flash |
| **Tech** | existing: IDamageable HP readable at damage time (FACT) · **new: HP-threshold check inside the damage path** · pooling - · alloc 0 · queries 0 · spawnCap 0 · WebGL none · deterministic |
| **Verify** | unit: threshold boundary exact · play: feels good on elites · visual: kill flash · perf: 0 alloc · question: *Is it noticeable, or invisible maths?* · fail: Player cannot tell it is working |

### uni.kinetic — Kinetic Shield  ·  **SHOULD**  ·  cost MEDIUM

> Keep moving to store a shield that blocks one hit.

Distance travelled charges a shield. At full charge the shield stores one blocked hit. It is consumed on the next hit taken and must be re-earned by moving.

| | |
|---|---|
| **Identity** | icon `acrobat` · rarity Rare · maxRank 3 |
| **Classification** | layer Universal · family Any · fireModes All · synergy `Move,Defence` · effect `Survival` |
| **Eligibility** | minLevel 3 · requires - · blocked - · prereq - · exclusions - |
| **Offer** | baseW 100 · newW 120 · rankUpW 85 · familyBias Sidearm+20 · autoPick yes · duplicates RankUp |
| **Trigger** | `OnDistanceTravelled` · threshold N metres (TUNING) · cooldown - · reset on shield consumed |
| **Effect** | StoredBlock → `Self` · base blocks 1 hit entirely · perRank -20% distance required · duration until consumed · radius - · maxTargets - · maxStacks 1 · Replace · source - |
| **Balance** | budget 2.5 · contributes Survival tied to mobility · softCap 1 charge · hardCap 1 charge · Replace · procLimit 1 per charge |
| **Feedback** | VFX shield shimmer around the player when charged · SFX charge chime, break crack · HUD shield ring around the player · colour cyan · readability ring visible while charged; break is loud |
| **Tech** | existing: damage entry point exists in Health.TakeDamage (FACT) · **new: distance accumulator (shared with Quickstep) + damage-intercept hook** · pooling - · alloc 0 · queries 0 · spawnCap 1 shimmer · WebGL none · deterministic |
| **Verify** | unit: blocks exactly one hit; does not stack · play: does not silently absorb a boss hit without feedback · visual: shield ring capture · perf: 0 alloc · question: *Does it reward movement without making the player safe?* · fail: Recharges so fast the player is permanently shielded |

---

## Compliance with the handoff constraints

| Constraint | Result |
|---|---|
| Exactly 23, none added/removed/renamed | ✔ 23 ids, all from the handoff list |
| No skill depends on reload or magazine | ✔ none reference ammo, reload or magazines |
| No skill requires extra manual input | ✔ every trigger is passive or automatic; movement is the only player action used |
| No skill claims an imaginary completed primitive | ✔ chain lightning, cluster targeting, distance accumulators and cone queries are all marked **new** |
| Rank 1 = one trigger + one effect | ✔ each card has a single trigger row and a single effect row |
| Offer rules prevent all-stat offers | ✔ max one stat card per offer, enforced in the contract |

## The three reuse claims that were checked and corrected

An earlier draft claimed chain lightning reused an existing primitive. Verified against source:

- `FireMode.ChainLightning` **is an enum value only**. `Weapon.cs` implements `PiercingLine` among the
  special modes; there is no chain code. `chainCount = 0` on all 25 assets. → **new primitive**.
- **No densest-cluster or density query exists anywhere in runtime.** → **new primitive** for Ordnance Core.
- `Bomb.Explode()` (OverlapSphere + TakeDamage) and the Bomb prefab/VFX/audio **do** exist, and
  `BombThrower` throw physics exists but is driven by manual input. That is an *asset plus partial
  mechanic*, not a finished autonomous power.

Genuinely reusable today: `WeaponData.RangeFalloff`, `WeaponData.knockback` →
`ZombieBase.ApplyPhysicalPush`, `Weapon.PerkedFireRate`, the damage-multiply path, and the implemented
`PiercingLine` fire mode.
