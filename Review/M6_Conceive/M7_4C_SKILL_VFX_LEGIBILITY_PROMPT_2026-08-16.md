# PHASE: EXECUTE — M7.4c · Make each skill look like itself

Work in:

```text
D:\Projects\Zombie-War
```

Run this **after** `M7_4B_PACING_AND_SURVIVABILITY_PROMPT_2026-08-16.md`. That run reshapes enemy
pacing and bounds incoming damage; do not undo any of it. This run touches skill presentation only.

The owner's words after playing:

> The effects are visible, but you cannot tell them apart. Chain Lightning should actually look like
> chain lightning. And I still cannot tell when a skill triggers — that is bad UX.

Legibility work landed for the autonomous powers in M7.2c, but the owner has now played it and says the
effects still blur together. Four ramp cards have no cue at all.

Run continuously; do not stop to ask. If capacity runs out, finish the card you are inside, leave the
game playable, and say where you stopped.

Follow `CLAUDE.md`, `AGENTS.md`, `Docs/FRAMEWORK.md` and the `billgamecore` skill. BillTween only, never
DOTween.

## 0. Standing rules

```text
NEVER edit Menu.unity or any Assets/_Project/UI/Prefabs/Screens/UI_*.prefab. The HUD prefab is
  UI_Hud.prefab — read a prefab before naming anything inside it.
Prefer world-space feedback: it lands this run. Anything that genuinely belongs in the HUD becomes a
  precise owner task, never a workaround.
Every new run-scoped static registers with RunScope on the day it is written.
Play-test from Bootstrap.unity only. Do not stage, commit or push. Do not touch .git.
Disclose every vendor asset that changes, including anything Unity re-serialises on its own.
Never generate or play a spoken/TTS report.
```

## 1. What already exists — extend it, do not rebuild

```text
P9 pooled FX kit — every effect goes through it; no ad-hoc Instantiate, no runtime material instances
A project-built pooled chain-arc renderer (LineRenderer, 12 instances, 7 points, tapered jitter)
Bound today: EnergyExplosionBlue (arcs) · ExplosionFireballFire (blasts) · FlashExplosionBlue (shield)
World-space status marks: Exposed orange · slow ice-blue · Hunter's Mark gold
Budgets that must hold: ≤2 concurrent explosions · pooled everything · zero runtime material
  instances · 0 bytes/frame steady state
```

---

# TASK 1 — Make the four autonomous powers unmistakable from each other

Right now they read as "a blast happened". Each must read as **which** power fired, in a glance, mid-fight,
on a busy screen.

```text
Chain Lightning       the arc is the identity — it must visibly travel enemy to enemy, in order.
                      Check the current renderer actually reads as a jumping bolt in play, not as a
                      flash on each target. If it does not, the arc is the thing to fix first.
Ordnance Core         something must ARRIVE on the chosen cluster before it detonates. The player
                      should read "that group was targeted", not "an explosion happened somewhere".
                      A brief marker or falling ordnance sells the choice the power made.
Soul Burst            radiates outward from the player, triggered by kills. Self-centred and rhythmic.
Emergency Detonation  fires when you are nearly dead. It must be the loudest, most distinct effect in
                      the game and impossible to confuse with Soul Burst — different colour, different
                      shape, different timing. This is a panic button; it should feel like one.
```

Give each a distinct **shape and motion**, not just a distinct colour. Colour alone fails on a busy
screen and fails for colour-blind players — the station signals already follow this rule with icon
side-count, and skills should match it.

# TASK 2 — The four ramp cards have no cue at all

`Run & Gun`, `Bullet Hose`, `Focus Fire`, `Heavy Pressure` all build up over time and show nothing. The
player cannot tell they are working, which is exactly the "I don't know when it triggers" complaint.

These are harder than procs because they are continuous, not instantaneous. Build what world space
allows:

```text
Run & Gun        movement builds it — a trail or accent at the player's feet that intensifies with the ramp
Bullet Hose      sustained fire builds it — heat building at the muzzle, or a spin-up read on the weapon
Focus Fire       locks onto one target — that specific enemy should visibly brighten as the stack grows
Heavy Pressure   sustained fire ramps damage with a movement cost — the trade-off should be visible
```

The common requirement: the player can tell **it is building**, and can tell **it reset**. A ramp that
silently decays teaches nothing.

If a proper meter genuinely needs the HUD, build the world-space version anyway and write the HUD task.

# TASK 3 — Judge the station icon size in play

The owner has not ruled on this yet. In the last capture the station icons went from radius 0.75 to 1.6
and now look very large in world space — the teal triangle is bigger than a nearby tree.

Do not blindly shrink them. Look at them **at gameplay camera distance in portrait**, and report your
judgement with a capture: too large, about right, or worth a smaller value. Recommend a number; the
owner decides.

# TASK 4 — The HUD power row, stated precisely one more time

Still the only piece that needs the owner's hands. Restate it exactly, verified against the real prefab:

```text
Prefab:  Assets/_Project/UI/Prefabs/Screens/UI_Hud.prefab   (UI_Hud — not UI_HudScreen)
Real child paths must be read from the prefab before naming them. Existing nodes include
  Overlays, Panel, CoinPill, RecordPill, Row0V, Perk0, PauseBtn, GearBtn.
Needed: a row of up to 4 power slots, each an icon plus a radial fill
Binding already live: SkillRuntime.Active.ReadinessOf(skillId) returns 0..1
Also available: Health.OnDamageAbsorbed for a shield-break flash
```

---

## Decision authority

**Decide yourself, record it:** every effect's shape, colour, motion, timing and which pack prefab
serves it; ramp cue design; the icon size you recommend; how effects are pooled.

**Never decide — record as owner tasks:** any UI prefab or scene edit, adding/removing/renaming a card
or changing what one does, the slot-A grammar, or any owner-locked rule.

## Acceptance gates

1. The four autonomous powers are distinguishable by **shape and motion**, not colour alone. Show it —
   a capture per power, in the real world, mid-fight.
2. Chain Lightning visibly travels target to target in play; if the current arc does not read as a
   jumping bolt, say so and fix it.
3. Emergency Detonation is unmistakable from Soul Burst.
4. All four ramp cards have a build-up cue and a visible reset.
5. A judgement on station icon size, with a capture and a recommended number.
6. Budgets hold: pooled everything, ≤2 concurrent explosions, zero runtime material instances,
   0 alloc/frame steady state, measured under a tier-2 crowd.
7. Nothing from the pacing/survivability run is undone.
8. Existing tests stay green; count reported before → after; new behaviour has new tests.
9. `git status`: zero modified UI prefabs, zero modified scenes, nothing staged. Vendor changes disclosed.
10. `detect_changes()` run and reported.

## Final report format

```text
PHASE: EXECUTE — M7.4c — COMPLETE / PARTIAL (say exactly where you stopped)

Autonomous powers: one row each — shape, motion, why it cannot be confused with the others, capture
Chain arc: does it read as a jumping bolt in play, and what you changed
Ramp cards: the cue for each, how build-up and reset read
Station icon size: your judgement, capture, recommended number
Performance under a tier-2 crowd: allocation, concurrency
Decisions made under delegated authority, and why
Owner tasks recorded (exact prefab, real child path, element, binding)
Tests: count before → after, new tests, any modified test and exactly why
Play-test: what you verified, what still needs a human
Vendor assets changed, including anything Unity changed on its own
detect_changes() output
Protected state: UI prefabs, scenes, staged files — git evidence
Files modified — full list
Open risks
M7.4c STATUS: DELIVERED / PARTIAL
BLOCKERS: none / exact blocker
```

The test the owner will apply is simple: take a card, play, and know it fired without being told. Every
capture in this report exists to predict that moment.

Completion audio policy: do not generate or play a spoken/TTS report. The owner wants only the
project's short completion notification behavior.

## Completion notification

After the task is genuinely complete and verified:

1. Use the `simplifier-vi` skill at Level 1 to reduce the final report to the outcome, verification performed, and any important remaining issue.
2. Rewrite that summary in natural English using no more than 80 words.
3. Run:

   python Tools\notify_done.py "<English summary>" --repeat 2

If the task is blocked or fails, run the same command with a short English explanation of the blocker. Report any notification-script failure in the written response.
