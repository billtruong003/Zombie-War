# PHASE: CONCEIVE — EXECUTE M6.1 WEAPON FACTORY HANDOFF

Work in:

```text
D:\Projects\Zombie-War
```

The authoritative task specification is:

```text
Review/M6_Conceive/M6_1_WEAPON_FACTORY_HANDOFF_2026-08-14.md
```

Read that file completely from beginning to end, then execute it faithfully.

## Verified starting state

This is not a mid-gate resume. M6.1 Weapon Factory has **not started**:

- `Review/M6_WeaponFactory/` does not exist.
- The canonical M6/GDD/ship-plan/README files predate the handoff.
- No Weapon Factory inventory, contact sheets, balance model, 23-skill catalog, build simulations, or exact-25 migration audit has been produced.

Therefore begin at Step 0 of the handoff. Do not claim or infer progress that is not present on disk.

## Preserve completed history

Do not redo M0–M5+, the 300-outfit experiment, character rendering, world streaming, combat repair, or contact-shadow work. Read those records only as constraints/evidence. H2 outfit status remains 73% overall pass, 0% hard conflicts, not production-ready.

## Required execution behavior

- Complete every numbered step and every acceptance gate in the handoff.
- Inspect actual assets/source; do not trust filenames or old counts.
- Open and vision-review every final contact sheet.
- Keep FACT/MEASURED/INFERENCE/PROPOSAL/TUNING/OWNER-LOCKED separate.
- Fully specify exactly the 23 named skills; do not silently add/remove/rename them.
- Rewrite the four canonical documents coherently.
- Store durable evidence under `Review/M6_WeaponFactory/`, never `Temp/`.
- Modify documentation/review files only.
- Do not implement or begin M7.
- Do not stop after the arsenal audit or another convenient sub-checkpoint. If genuinely incomplete, identify the exact failed/unreached gate without presenting partial work as complete.

## Final state required

The final report must follow the report schema in the handoff and explicitly state:

```text
M6 STATUS: LOCKED / NOT LOCKED
M7 STATUS: NOT STARTED / NOT AUTHORIZED
BLOCKERS: none / exact blocker
```

Completion audio policy: do not generate or play a spoken/TTS report. The owner wants only the project's short completion notification behavior.

## Completion notification

After the task is genuinely complete and verified:

1. Use the `simplifier-vi` skill at Level 1 to reduce the final report to the outcome, verification performed, and any important remaining issue.
2. Rewrite that summary in natural English using no more than 80 words.
3. Run:

   python Tools\notify_done.py "<English summary>" --repeat 2

If the task is blocked or fails, run the same command with a short English explanation of the blocker. Report any notification-script failure in the written response.
