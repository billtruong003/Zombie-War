# PHASE: EXECUTE — M7.2c · Run reset bug + make skills legible

Work in:

```text
D:\Projects\Zombie-War
```

The owner played the build. The verdict on feel is **good** — the loop is enjoyable after a few levels.
Three problems came out of that session, and they are the whole scope of this run:

```text
1. BUG — skills persist across runs. Finish a run, exit, start again: the build is still there.
   The owner reports the same class of bug on coin pickup drops.
2. The player cannot tell when a skill triggers. Owner's words: this is bad UX design.
3. The VFX does not represent the skill. Chain Lightning must actually look like chain lightning.
```

Everything else the owner is happy with. **Do not open Part C** (stations, signal language, threat
model). Do not rebalance card identities. Do not touch the slot-A grammar question.

Run continuously; do not stop to ask. Decide, record it, keep going.

Follow `CLAUDE.md`, `AGENTS.md`, `Docs/FRAMEWORK.md` and the `billgamecore` skill. BillTween only, never
DOTween.

---

## 0. Standing rules

```text
NEVER edit Menu.unity or any Assets/_Project/UI/Prefabs/Screens/UI_*.prefab. The owner owns UI layout.
  This constrains Part 2 heavily — read its instructions before reaching for a HUD element.
You never place a grip, muzzle or hand transform by inference.
Play-test from Bootstrap.unity only. Do not stage, commit or push. Do not touch .git.
Disclose every vendor asset that changes, even when Unity changed it for you. Last run modified
  Assets/ThirdParty/Epic Toon FX/Materials/Misc/Magic/magic_blast_ADD.mat without reporting it —
  Unity re-serialised it Opaque→Transparent. Keep that change (it is more correct than the original),
  but report anything like it from now on.
Never generate or play a spoken/TTS report.
```

---

# PART 1 — The run-reset bug (highest priority)

## 1.1 The diagnosis, already done — verify it, then go wider

`SkillRuntime.Active` is a **static**. It is assigned once, in `SkillCombatDriver`:

```csharp
if (SkillRuntime.Active == null) SkillRuntime.Active = new SkillRuntime();
```

Nothing in gameplay ever sets it back to null — only tests do. Statics survive scene reloads inside a
play session, so leaving a run and starting a new one reuses the same runtime with every card still
taken. That is exactly what the owner saw.

At least two more run-scoped statics have the same hole. The tests call them; gameplay never does:

```text
StatusCarrier.ClearAll()            — statuses from the previous run survive
AutonomousPower.ResetGlobalBudget() — the ≤2 procs/s budget carries its old state across runs
```

**Do not fix only these three.** Audit every static that holds run-scoped state — skills, pickups, coin
drops, run counters, kill counters, pooled-entity registries, anything scoped to one run — and list what
you found. The owner reports coin pickup drops behaving the same way; treat that as the same bug class,
find its actual cause, and fix it properly rather than special-casing coins.

## 1.2 One reset path, not scattered cleanup

`RunState.Begin(string levelId)` already exists as the run-start hook, and `RunState.Abandon()` on the
way out. Give the project **one explicit place** where run-scoped state is reset, called from the run
lifecycle, so a system added next month has an obvious place to register.

Scattering `Clear()` calls across `OnEnable` handlers is how this bug came back a second time. Prefer one
reset that each system opts into.

## 1.3 Prove it the way the owner found it

The failing scenario is a play sequence, not a unit case:

```text
start a run → take several cards → let coins drop → end the run → start another run
expected: zero cards, a fresh offer pool, no statuses, no carried coins, proc budget reset
```

Write a test that reproduces that sequence, and confirm it fails before your fix. State in the report
that you saw it fail first — a regression test that never failed proves nothing.

---

# PART 2 — Make every skill legible

The owner's complaint is precise: **you cannot tell when a skill triggers.** A power that fires
invisibly feels like no power at all, which is also part of why the cards feel weak.

## 2.1 The rule

Every card that procs, ramps, charges or blocks must announce itself at the moment it happens, in a way
the player can read mid-fight without inspecting numbers. Three separate things need to be legible:

```text
WHEN it fired     — a cue at the instant of the proc
WHAT it did       — the effect visibly belongs to that skill, not a generic blast
WHERE it is going — for charge/cooldown cards, some sense of "almost ready"
```

## 2.2 The UI constraint — read this before designing anything

The owner asked for UI that tells the player what is happening, **and the owner owns every UI prefab.**
You may not edit them. So:

- Build the feedback in **world space** wherever possible — indicators above the player, marks on
  enemies, arcs between targets, a ring at the player's feet. These are spawned objects, not UI prefabs,
  and they land this run.
- For anything that genuinely belongs in the HUD (a power cooldown row, a charge bar), **do not build a
  workaround.** Write the exact prefab change the owner would need to make: which prefab, which child
  path, what element, what the code will bind to. Make it specific enough to do in one pass.
- The code side of any HUD element should already work headlessly, so wiring it later is only layout.

## 2.3 Chain Lightning must chain — the pack cannot do it for you

I checked the library. Epic Toon FX ships 49 prefabs named `Lightning*`, but **every one is an
explosion** (`LightningExplosionBlue`, `LightningSoftExplosionGreen`, …). There is no beam, no arc, no
prefab that connects two points. `Laser*` are muzzle explosions and missile meshes; `*Trail` are
particle trails that follow a transform.

So a real chain arc has to be built: a pooled line renderer (or stretched quad) drawn between successive
target positions, with a short lifetime, dressed at each endpoint with the pack's electric hits
(`ElectricDeathBlue`, `Zap`, `LightningExplosionBlue` are reasonable). Pool it like every other effect —
no per-proc allocation, no runtime material instances.

The test is simple and the owner will apply it: **can you see the bolt jump from one zombie to the
next?** If the answer is a flash on each enemy with nothing between them, it is not chain lightning.

## 2.4 Give each power an effect that matches its mechanic

```text
Chain Lightning       arcs hopping target to target, in order, visibly a chain
Ordnance Core         something arrives on the cluster before it detonates — the player should read
                      "that group was chosen", not "something exploded somewhere"
Soul Burst            a burst radiating from the player, triggered by kills, clearly self-centred
Emergency Detonation  unmistakable and distinct from Soul Burst — it fires when you are nearly dead
                      and must read as a panic button, not another blast
Kinetic Shield        a break flash on the player at the moment a hit is eaten. The owner must be able
                      to tell a hit was absorbed rather than missed
Statuses              Exposed / slow / Hunter's Mark need a visible mark on the enemy carrying them
Ramp / charge cards   some readable build-up, so Run & Gun, Bullet Hose, Focus Fire and Heavy Pressure
                      show that they are working
```

Hold the budgets: pooled FX only, ≤2 concurrent explosions, zero runtime material instances,
0 bytes/frame steady state. Legibility must not cost the frame budget — a readable cue beats a big one.

---

# PART 3 — Finish the two stubbed cards, then a bounded tuning pass

## 3.1 Consume `ShotPlan`

`Breach Round`'s bonus pierce and `Shockwave Belt`'s cone are computed by `OnShotFired` and never read
by the weapon. Consume the plan in the weapon's pierce/cone path so both cards fire their projectile
half. After this, no card is stubbed.

## 3.2 Tuning — bounded, and honest about what changed

The owner says the skills do not feel overpowered, while also saying the overall feel is good. Some of
that is legibility: a power you cannot see feels weaker than it is. **Do Part 2 first, then tune** — you
may find less is needed than you thought.

Then make one bounded pass, and report before/after for every number you touch:

```text
Rank 3 of a signature card should be visibly stronger than rank 1, not marginally
Autonomous powers should read as spectacle when they fire
Everything stays inside the guardrails: fire-rate caps, ≤2 procs/s, ≤6 arcs, ≤2 explosions
Every number stays labelled TUNING
```

**Do not** change what a card *is*, add or remove cards, or touch the slot-A signature grammar. Identity
and grammar are owner decisions; magnitudes are yours to propose.

---

## 4. Decision authority

**Decide yourself, record it:**

```text
Where the single run-reset path lives and how systems register with it
Every visual and audio cue: shape, colour, duration, and which pack prefab serves it
How the chain arc is drawn and pooled
Tuning magnitudes within the guardrails
```

**Never decide — record as owner tasks:**

```text
Any UI prefab or scene edit (specify it precisely instead)
Adding, removing or renaming a card, or changing what a card does
The slot-A signature-card grammar
Owner-locked rules: one weapon per run, auto-fire, no reload, no manual grenade, 1-of-3 with a ≤30 s pause
```

## 5. Acceptance gates

1. Every run-scoped static is enumerated in the report, with its reset state before and after.
2. One explicit run-reset path exists and is called from the run lifecycle.
3. The coin-drop bug is diagnosed by cause, not patched by special case.
4. A test reproduces the owner's sequence (run → cards → coins → exit → new run) and **failed before the
   fix**; say so explicitly.
5. Every card that procs, ramps, charges or blocks has a cue the player can read mid-fight. List each
   card and its cue.
6. Chain Lightning draws a visible arc between successive targets — not a flash on each enemy.
7. Each autonomous power's effect matches its mechanic and is distinguishable from the others,
   especially Soul Burst versus Emergency Detonation.
8. Absorbed hits are visibly different from missed hits.
9. Any HUD element that could not be built is written up as a precise owner task: prefab, child path,
   element, and the binding the code already exposes.
10. `ShotPlan` consumed; no card remains stubbed.
11. Tuning pass reported with before/after numbers, all labelled TUNING, all inside the guardrails.
12. Performance: pooled FX, ≤2 concurrent explosions, zero runtime material instances, 0 alloc/frame.
13. Test count before → after; new tests demonstrably ran; existing 621 stay green.
14. `git status`: zero modified UI prefabs, zero modified scenes, nothing staged. Every changed vendor
    asset disclosed.
15. `detect_changes()` run and reported.
16. Part C not started.

## 6. Final report format

```text
PHASE: EXECUTE — M7.2c RESET + LEGIBILITY — COMPLETE / PARTIAL (say exactly where you stopped)

Run-scoped statics: full list, what leaked, what resets now, where the single path lives
Coin bug: the actual cause, and why the fix is general rather than a special case
The reproduction test: what it does, and confirmation that it failed before the fix
Legibility: one row per card — what the player now sees, and where it is drawn
Chain arc: how it is drawn, pooled and bounded
Powers: how Soul Burst and Emergency Detonation read differently
HUD owner tasks: exact prefab, path, element, binding
ShotPlan: both cards now firing their projectile half
Tuning: every number before → after, and why
Performance under load: allocation, concurrency, proc ceiling
Vendor assets changed, including anything Unity changed on its own
Tests: count before → after, new tests, any modified test and exactly why
Play-test: what you could and could not verify without a human driving the player
detect_changes() output
Protected state: UI prefabs, scenes, staged files — git evidence
Files modified — full list
Open risks
M7.2c STATUS: DELIVERED / PARTIAL
M7.3 STATUS: NOT STARTED
BLOCKERS: none / exact blocker
```

The owner will judge this run by playing it, not by reading it. The bar is: they take a card, and they
can see it working.

Completion audio policy: do not generate or play a spoken/TTS report. The owner wants only the
project's short completion notification behavior.

## Completion notification

After the task is genuinely complete and verified:

1. Use the `simplifier-vi` skill at Level 1 to reduce the final report to the outcome, verification performed, and any important remaining issue.
2. Rewrite that summary in natural English using no more than 80 words.
3. Run:

   python Tools\notify_done.py "<English summary>" --repeat 2

If the task is blocked or fails, run the same command with a short English explanation of the blocker. Report any notification-script failure in the written response.
