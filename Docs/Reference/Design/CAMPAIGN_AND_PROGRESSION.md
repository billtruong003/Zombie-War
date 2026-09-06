# Zombie War — Campaign and Progression

> **SUPERSEDED — 2026-08-09.** The five-stage authored-map campaign is replaced by the expedition
> contract model in [`../../GAME_DESIGN.md`](../../GAME_DESIGN.md). This file is retained only as
> evidence about existing catalog/profile behavior and has no current campaign authority.

**Authority:** Campaign navigation, unlock rules, stage role, run/meta progression boundaries

**Status:** Source of truth

**Updated:** 2026-08-08

**Balance snapshot:** [`CAMPAIGN_BALANCE_TABLE.md`](CAMPAIGN_BALANCE_TABLE.md)

## 1. Current repository reality — PROJECT FACT

- `CampaignCatalog.asset` contains five ordered entries for `Map_Level1` through `Map_Level5`, with
  stable level IDs, wave data, power fields, advice and rewards.
- `CampaignCatalog.Evaluate()` currently enforces two hard gates for stages after Stage 1: previous
  stage completion and `minimumPower`. `recommendedPower` is authored as advice.
- `PlayerProfile` persists completed level IDs and `LastSelectedLevelId`, and emits
  `CampaignChanged` after completion/reward changes.
- `GameFlow.SelectLevel()` persists the selected ID; `StartGameplay()` loads the selected scene, with
  Stage 1 as the default. Run identity/finish closure exists independently of a selector UI.
- The current `HubScreen` wires PLAY directly to `GameFlow.StartGameplay()`. There is no campaign
  selector, so normal Hub flow does not expose Stages 2–5.
- Stage data already varies composition: Stage 1 high-density walkers/runners; Stage 2 introduces
  ranged/burrow pressure and a cactus boss; Stage 3 skeleton mage/heavy; Stage 4 pouncer/burrower and
  Mole King; Stage 5 mixed roster/final boss.
- Loadout runtime is one mandatory pistol plus two long-gun slots. Current HUD has one cycle button.
- XP accumulation exists, but level-up choice/application is not an end-to-end player system.
- Meta backend already includes Coin/Gold/Gem, buying/upgrading weapons, loadout, gacha, costume,
  missions and save. Breadth exceeds the currently proven combat identity.

## 2. Campaign selector — LOCKED foundation design

Prototype presentation, directly above PLAY:

```text
                 STAGE 2

       ‹   ● ━ ● ─ ○ ─ • ─ •   ›

                  PLAY
```

Rules:

- Compact horizontal layout; no Hub redesign.
- One dot per current `CampaignCatalog` entry; never hardcode stage count.
- Stage 1 is available on a fresh profile.
- Clearing Stage N unlocks Stage N+1.
- Completed, available, selected and locked states are visually distinct.
- Locked stages are muted and cannot become the PLAY selection.
- Left/right moves one valid catalog index at a time; no wrap.
- At boundaries, the corresponding arrow is disabled or has no effect.
- PLAY launches the selected stage through the existing flow.
- Returning to Hub refreshes completion/unlock state without relaunching the app.
- Relaunch restores the last selected stage only if it is still present and playable; otherwise it
  falls back to the nearest valid choice, ultimately Stage 1.

This phase exposes existing content and closes a broken navigation path. It is not hook validation.

## 3. Unlock logic and Combat Power conflict

### Conflict — PROJECT FACT

The visual progression line says “complete N to reach N+1”, but `Evaluate()` can still return
`Underpowered` when completion is satisfied. Current authoring uses positive `minimumPower` from Stage
2 onward. A player can therefore clear the preceding stage and see the next dot yet remain hard-blocked
by a second progression system.

### Decision — LOCKED for campaign UX

- Previous-stage completion is the only hard campaign progression gate.
- Combat Power is a **soft recommended-power warning**, not a second lock.
- Under-recommended players may still choose PLAY after seeing a concise warning.
- Recommended Power communicates risk; it does not force shop/grind.

Reason: a linear completion path must keep its promise. Hard power gates create misleading UI, make
balance errors into progression blockers and encourage stat grind before weapon identity is proven.

### Implementation note — not architecture

Phase 1 must reconcile `minimumPower`, `recommendedPower`, `LevelGate` and existing tests without
silently changing reward or save contracts. If a technical/project constraint contradicts this rule,
the agent stops and reports the contradiction rather than inventing a third gate.

## 4. Stage design role — PROVISIONAL

A stage is not a different background plus more HP. Its composition should introduce or combine a
combat question that reinforces the grammar.

| Stage | Existing content foundation | Intended learning/question | Status |
|---|---|---|---|
| 1 — Bùng Phát | dense walkers, runner pressure, no boss | movement baseline, emergency space, first reason to swap | PROVISIONAL; needs grammar-friendly test slice |
| 2 — Cánh Đồng Gai | ranged, burrower, cactus boss | spacing and priority under ground/ranged pressure | PROVISIONAL |
| 3 — Nghĩa Địa Xương | skeleton small/mage/giant | distinguish fast pressure, ranged cast and heavy punish | PROVISIONAL |
| 4 — Bầy Hoang | pouncer, burrower, Mole King | commitment telegraphs and recovery punishment | PROVISIONAL |
| 5 — Vây Hãm Titan | mixed roster and final boss | combine learned questions without unreadable noise | PROVISIONAL |

Existing waves are useful ingredients, not proof that each stage already asks its intended question.
Do not rebalance all five before the narrow Phase 3 prototype validates the interaction.

## 5. Loadout

### Current facts

- Slot 0: mandatory one-handed pistol.
- Slots 1–2: two-handed long guns, optional by data but intended as the rest of the combat loadout.
- One HUD button cycles occupied slots.
- `LoadoutState` and profile save already support applying the selected arsenal at spawn.

### Potential strategic role — PROVISIONAL

A loadout should represent a sequence of answers, not three independent DPS values. Composition and
possibly slot order may determine:

- what problem the player can set up;
- which weapon can cash it out;
- how emergency recovery works;
- where ammo/reload resets create vulnerability.

### Open control relationship

Do not finalize whether pistol is a dedicated emergency layer or a normal slot until Phase 3 reveals
how often players need direct access to specific weapons. Phase 5 owns the control decision; existing
one-button cycling is a constraint to test, not a design truth that can overrule validated behavior.

## 6. XP, perks and run progression

### Current reality — PROJECT FACT

- XP and level arithmetic work in `RunState`.
- Enemy kills add XP directly.
- A gained level is not connected to a queued choice.
- Level-up UI can pause/close but does not draw/apply a real perk.
- Existing perk data is mostly stat bonuses and its multipliers are not fully consumed by combat.
- Mission metrics for perk choice, weapon switch and boss defeat exist but currently have no runtime
  callers in the audited source.

Therefore “XP/perks exist” is true as backend scaffolding but false as player-facing run progression.

### Intended direction — PROVISIONAL

Only after combat grammar validation:

1. XP creates bounded choice moments without destroying horde rhythm.
2. Player selects a signature technique compatible with the equipped arsenal.
3. Later choices mutate behavior or support the technique with stats.
4. Run state resets honestly at run end.
5. Different runs create different tactical expression, not only a larger multiplier.

**OPEN:** whether XP remains direct or becomes a physical orb. If physical XP is introduced, direct
kill XP must be removed to avoid double reward. This is a Phase 6 decision, not Phase 1 work.

## 7. Meta progression

### Existing foundation — PROJECT FACT

The profile already supports weapon ownership/upgrades, currencies, shop, gacha, costume, missions and
loadout persistence. This infrastructure can support a shipped loop, but it does not establish why
the player wants another run.

### Product rule — LOCKED

Meta progression supports combat diversity; it cannot substitute for it.

- Do not balance retention around a pure weapon-stat ladder before family identity is proven.
- Do not make grind the answer to a combat problem caused by unclear roles.
- Unlocks should increasingly offer new tactical expression; permanent power may support pacing.
- Gacha, mission breadth and currency optimization belong after the core loop earns replay intent.

## 8. Progression sequence

```text
Campaign selector foundation
→ baseline weapon identity
→ combat grammar proof
→ representative signature techniques
→ mobile weapon-selection solution
→ full run build formation
→ content expansion
→ meta/economy/retention tuning
```

The release plan may track shipping gates in parallel, but it may not use already-built economy breadth
as a reason to skip the high-risk combat questions.

## 9. Open questions

1. Can Stage 1's current composition expose enough distinct weapon decisions for first-session learning?
2. Should recommendation warning appear on selection, PLAY, or both without adding friction?
3. Does the last valid selection remain intuitive when progression data is migrated or catalog entries move?
4. Does slot order become strategic, or should control guarantee direct access to a desired family?
5. What is the first run-build choice timing that improves agency without interrupting combat too often?
6. Which meta unlock creates the first genuinely new tactical option rather than a stronger number?
