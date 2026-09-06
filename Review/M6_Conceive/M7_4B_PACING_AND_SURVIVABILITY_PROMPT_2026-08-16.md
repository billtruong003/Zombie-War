# PHASE: EXECUTE — M7.4b · Smooth the pressure, stop the instant melt

Work in:

```text
D:\Projects\Zombie-War
```

The owner played the threat build and reported it is **fun** — that is the headline, and the direction is
right. Three concrete problems came out of the session, and they share one root cause.

```text
"Áp lực dồn một lúc rồi mất"  — pressure arrives in a clump, then the world thins out
"Húc một cái là chết luôn"     — died apparently instantly, to a cactus-family enemy
RunAwayProbe ships in the runtime assembly
```

**Closed by owner decision — do not work on it:** the behind-dominant arrival distribution. The owner
played it and is fine with it ("game mà, cũng không nên logic quá"). Spend nothing on lead-point tuning
or ahead-biased spawning.

Run continuously; do not stop to ask. If capacity runs out, finish what you are inside, leave the game
playable, and say where you stopped.

Follow `CLAUDE.md`, `AGENTS.md`, `Docs/FRAMEWORK.md` and the `billgamecore` skill. BillTween only, never
DOTween.

## 0. Standing rules

```text
NEVER edit Menu.unity or any Assets/_Project/UI/Prefabs/Screens/UI_*.prefab. The HUD prefab is
  UI_Hud.prefab — read a prefab before naming anything inside it.
Every new run-scoped static registers with RunScope on the day it is written.
You never place a grip, muzzle or hand transform by inference.
Play-test from Bootstrap.unity only. Do not stage, commit or push. Do not touch .git.
Disclose every vendor asset that changes, including anything Unity re-serialises on its own.
Never generate or play a spoken/TTS report.
```

## 1. The diagnosis, measured — the two complaints are one bug

Nothing one-shots the player. Measured across every `ZombieData` asset against 100 player HP:

```text
Highest single hit    ZD_CactusBoss 30 · ZD_SkeletonGiant 26 · ZD_MoleRatKing 24
Per-enemy sustained   5-14 damage per second (damage ÷ attackCooldown)
EIGHT enemies in contact   ~70-90 DPS combined  →  100 HP gone in a little over one second
Health.cs has NO invulnerability window, no grace period, no damage cooldown of any kind
```

And the clumping is what delivers those eight at once. Last run raised spawns per tick from **1 to 3**,
so enemies leave in packs, travel as packs and arrive as packs. The owner's "clump then empty" and
"died instantly" are the same phenomenon seen from two angles: a pack lands, several connect in the same
beat, and there is no mechanic anywhere that limits simultaneous damage.

To the owner's question — *does rising tier fix the sparseness?* Partly. Cadence tightens ×0.82 per tier
and the alive target grows +8 per tier, so density does rise. But tier scaling does not change the
**shape** of arrivals, so a bigger burst just means a bigger clump. Shape is what this run fixes.

---

# TASK 1 — Smooth the arrival stream

Same average pressure, delivered continuously instead of in packs.

```text
Reduce the burst size and shorten the interval so the alive-count curve stays where it is now
  (it reached 23 against a tier-2 ceiling of 34 — that number was good).
Stagger arrivals so a pack does not travel as one body: vary sector, band radius and a small
  per-spawn delay.
Keep the crowd ceiling from AliveTargetFor(tier). Do not raise total pressure — reshape it.
```

Measure it: sample alive count and arrivals-per-second over 60 s, and show that the peaks and troughs
are flatter than the previous run's curve. The metric that matters is variance, not the average.

# TASK 2 — Stop the instant melt without killing the horde fantasy

A crowd game must let a crowd surround you. It must not let a crowd delete you in one second with no
readable moment. Investigate what exists, choose an approach, and justify it.

Two established levers, and the first is the one I would reach for:

```text
CAP SIMULTANEOUS ATTACKERS — only N enemies may occupy an attacking slot at once; the rest crowd,
  jostle and wait their turn. The screen still looks like a horde, but incoming damage is bounded and
  the player can read who is committing. This is the standard horde-game answer and it does not make
  the player feel invincible.
BRIEF INVULNERABILITY AFTER A HIT — a short grace window so simultaneous hits cannot stack into one
  frame of death. Cheap, but it can feel mushy if it is long, and it weakens every individual threat.
```

Whichever you choose, these must hold:

- Bosses and elites are never denied their attack by the cap — a boss must always be able to commit.
- The player must be able to **see** why they are not being hit: a waiting enemy should read as
  waiting, not as a frozen or broken enemy.
- Death must stay possible and fair. The goal is "I got overwhelmed and could see it coming", not
  "I cannot die".
- Whatever you add is `TUNING`, and its numbers are reported.

Then verify by playing: stand in a tier-2 crowd and report how long survival takes with a mid-tier
weapon, before and after. If a player can now stand in a horde forever, the cap is too strong — say so.

# TASK 3 — Get the probe out of the runtime build

`Assets/_Project/Scripts/Runtime/Diagnostics/RunAwayProbe.cs` ships in the runtime assembly. It is inert
unless instantiated, but diagnostics do not belong in a player build. Move it to an Editor assembly, or
wrap it in a define that excludes it from release. Say which and why.

# TASK 4 — Finally verify the three chains

These have gone unobserved for four consecutive runs. The owner has now partially answered one:

```text
BOSS KILL — the owner killed a beacon boss and got a level-up with a card choice. That is the normal
  XP path working, NOT proof of the boss reward. What is still unverified is whether
  NotifyBossKilled -> PickupManager.DropReward actually drops loot at the corpse. Confirm or deny it.
RELAY COMPLETION — hold the ring the full 12 s and confirm the card offer is delivered.
MAGNET — confirm one drops from an elite/boss kill and that collecting it sweeps the ground.
```

Do not make the player invulnerable to test these — that is what blocked the boss chain last time. Use a
strong weapon, a cheat that raises damage, or spawn a weak boss instead.

---

## Decision authority

**Decide yourself, record it:** burst size, interval, stagger and jitter numbers; which survivability
lever and its parameters; where the probe goes; how you set up the play-tests. All numbers `TUNING`.

**Never decide — record as owner tasks:** any UI prefab or scene edit (name the real prefab and a real
child path), adding/removing/renaming a card, the slot-A grammar, or any owner-locked rule (one weapon
per run, auto-fire, no reload, no manual grenade, 1-of-3 with a ≤30 s pause, 25% / 0% banking).

## Acceptance gates

1. Arrivals are measurably smoother: alive-count and arrivals-per-second sampled over 60 s, variance
   compared against the previous run's numbers, average pressure unchanged.
2. Simultaneous incoming damage is bounded by a stated mechanic, with its numbers.
3. Bosses and elites are never blocked from attacking.
4. A waiting enemy reads as waiting, not as broken.
5. Death is still possible: report measured survival time in a tier-2 crowd with a mid-tier weapon.
6. `RunAwayProbe` no longer ships in the runtime assembly.
7. Boss kill → reward drop: confirmed or denied, with evidence.
8. Relay completion → card offer: confirmed or denied.
9. Magnet drop → sweep: confirmed or denied.
10. New run-scoped state registers with `RunScope`, with a test.
11. Existing 685 tests stay green; count reported before → after; new behaviour has new tests.
12. `git status`: zero modified UI prefabs, zero modified scenes, nothing staged. Vendor changes disclosed.
13. `detect_changes()` run and reported.

## Final report format

```text
PHASE: EXECUTE — M7.4b — COMPLETE / PARTIAL (say exactly where you stopped)

Pacing: what changed, the before/after alive-count and arrival-rate curves, variance numbers
Survivability: which lever, why, its numbers, and measured survival time in a tier-2 crowd
Probe: where it went
The three chains: each confirmed or denied, with what you saw
Decisions made under delegated authority, and why
Owner tasks recorded
Tests: count before → after, new tests, any modified test and exactly why
Play-test: what you verified, what still needs a human
Vendor assets changed, including anything Unity changed on its own
detect_changes() output
Protected state: UI prefabs, scenes, staged files — git evidence
Files modified — full list
Open risks
M7.4b STATUS: DELIVERED / PARTIAL
BLOCKERS: none / exact blocker
```

The owner says the game is fun now. The job of this run is to keep that and remove the two moments that
break it: the lull, and the death you never saw coming.

Completion audio policy: do not generate or play a spoken/TTS report. The owner wants only the
project's short completion notification behavior.

## Completion notification

After the task is genuinely complete and verified:

1. Use the `simplifier-vi` skill at Level 1 to reduce the final report to the outcome, verification performed, and any important remaining issue.
2. Rewrite that summary in natural English using no more than 80 words.
3. Run:

   python Tools\notify_done.py "<English summary>" --repeat 2

If the task is blocked or fails, run the same command with a short English explanation of the blocker. Report any notification-script failure in the written response.
