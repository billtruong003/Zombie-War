# HordeCall — integrate the completed skill-icon set and update the UI mockup

Work in the Unity project:

`D:/Projects/Zombie-War`

Do not redraw, replace, rename, or restyle the icons. The artwork is finished. Your job is to wire it into the current game, update the skill UI/mockup to use the real icons, and verify the complete flow.

## Source of truth and asset locations

- Original 35-icon brief and tier/style rules: `Review/M8/skill_icon_prompts.md`
- New 43-icon brief and exact ids: `Review/M8/skill_icon_prompts_v2.md`
- New-batch generation report: `Review/M8/icon_batch2_report.md`
- New-batch contact sheet: `Review/M8/icon_batch2_sheet.png`
- Existing level-up context reference: `Review/M8/icon_style/levelup_context.png`
- Existing reviewed level-up capture/mockup: `Review/M8/ui_after/levelup_1.jpg`
- Final runtime icon folder containing both old and new icons: `Assets/_Project/UI/Icons/Skills/`
- Icon refresh editor command: `ZombieWar/UI/Authoring/Refresh Skill Icons`
- Refresh implementation: `Assets/_Project/Scripts/Editor/UI/SkillIconSetBuilder.cs`
- Runtime icon mapping: `Assets/_Project/Scripts/Runtime/UI/Data/SkillIconSet.cs`
- Relevant runtime/editor UI includes `RunOverlays`, `SkillBarView`, `HudController`, `RunOverlays.Chest`, `M8UiLayout`, and `M8UiPolish`. Confirm exact symbols and dependencies before changing anything.

The new batch has 43 final PNGs. Each is 1024x1024 RGBA with transparent background and is already saved directly in `Assets/_Project/UI/Icons/Skills/<id>.png`. Together with the previous 35 icons, the project should now have 78 skill icons.

## Required execution

1. Read the repository `AGENTS.md`, both icon briefs, and the batch report before editing.
2. Inspect the working tree and preserve all unrelated user changes.
3. Follow the repository GitNexus rules:
   - run upstream impact analysis before editing any function, class, method, prefab builder, or runtime UI symbol;
   - warn and stop for unresolved HIGH/CRITICAL impact;
   - treat UNKNOWN as unresolved and confirm with text search;
   - run `detect-changes --scope all` before finishing if code or serialized Unity assets change.
4. Audit `Assets/_Project/UI/Icons/Skills/` against the exact ids in both briefs:
   - all 35 original ids exist;
   - all 43 new ids exist;
   - filenames match ids exactly;
   - no `__CHECK`, temporary, duplicate, or wrongly named file is used;
   - PNG alpha is preserved.
5. Run Unity menu command `ZombieWar/UI/Authoring/Refresh Skill Icons`.
   - Ensure each PNG imports as Sprite (2D and UI), transparent, no mipmaps, max size 256, and appropriate high-quality compression.
   - Rebuild/update the `SkillIconSet` mapping so every current skill id resolves to its sprite.
   - Do not rely on initials as the normal path now that final art exists.
6. Update the current skill UI and its mockup/reference so they use the real icon assets:
   - Level-up cards: real icon on the left, shown at the intended 46–64 px size on the dark `#2F3544` tile; retain tier-colour treatment and readable spacing.
   - HUD skill bar: real icons with rank/level treatment still readable and not covering the art.
   - Chest/skill-choice overlay: real icons for every offered skill.
   - End-of-run/build summary: real icons wherever skills are listed.
   - Any other live skill surface using initials must switch to the shared icon mapping, while keeping a safe fallback only for genuinely missing future ids.
7. Treat the live Unity UI as the implementation source. Do not manually paint icons into a screenshot and call it finished. Update the prefab/builder/runtime path first, then capture the corrected live result.
8. Produce an updated mockup/capture set using the actual game UI and actual sprites:
   - create `Review/M8/icon_batch2_mockup/`;
   - save at least one Level-Up screen showing Stat, Universal, Autonomous, Signature, and Evolution examples across the captures;
   - save a HUD skill-bar capture;
   - save a chest/skill-choice capture if that surface exists in the current playable flow;
   - save an end-of-run/build-summary capture if that surface exists;
   - add a compact contact/overview image if useful, but do not replace `icon_batch2_sheet.png`.
9. Match the approved M8 UI language rather than redesigning it:
   - dark `#2F3544` icon tiles;
   - existing card layout, typography, tier colours, padding, corner radii, and pressed/lip treatment;
   - no enlarged icons that crowd descriptions;
   - no placeholder initials visible when an icon exists.
10. Verify in Unity:
    - wait for import and compilation to finish;
    - console has no new errors;
    - every one of the 78 expected ids resolves to a non-null sprite;
    - inspect representative cards from every tier at phone size;
    - confirm icons remain readable at 46–64 px;
    - confirm Level-Up, HUD, Chest, and Result surfaces do not clip icons or cover them with rank badges;
    - run relevant EditMode tests and any project-specific UI/icon validation tools;
    - if automated play-mode capture is available, use the SKILLS QA/cheat flow to exercise representative cards.
11. Do not modify the icon artwork files unless an actual corrupt/missing import is proven. Do not change gameplay balance, skill descriptions, unlock rules, or unrelated UI.

## Final report

Report:

- icon audit count for old/new/total;
- refresh command result and `SkillIconSet` mapping count;
- code/prefab/serialized assets changed;
- mockup/capture paths created;
- tests and Unity-console result;
- any id still falling back to initials and the exact reason;
- GitNexus impact and detect-changes result.

Do not stop after copying files: completion requires the live game UI to display and verify the icons.

## Completion notification

After the task is genuinely complete and verified:

1. Use the `simplifier-vi` skill at Level 1 to reduce the final report to the outcome, verification performed, and any important remaining issue.
2. Rewrite that summary in natural English using no more than 80 words.
3. Run:

   python Tools\notify_done.py "<English summary>" --repeat 2

If the task is blocked or fails, run the same command with a short English explanation of the blocker. Report any notification-script failure in the written response.
