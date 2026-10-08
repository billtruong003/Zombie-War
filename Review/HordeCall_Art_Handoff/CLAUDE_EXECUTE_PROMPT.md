# HordeCall art handoff — execution prompt for Claude

Work in the project root: `D:/Projects/Zombie-War`.

The completed art batch is collected in:

`Review/HordeCall_Art_Handoff/`

It contains 10 avatar PNGs, 35 skill-icon deliverables, two contact sheets, and this prompt. The original source folders remain intact:

- Avatar brief: `Review/Avatars/AVATAR_PROMPTS.md`
- Avatar originals: `Review/Avatars/generated/`
- Skill-icon brief: `Review/M8/skill_icon_prompts.md`
- Skill-icon originals: `Review/M8/skill_icons_generated/`

## Execute these tasks

1. Read both briefs completely and treat them as the source of truth.
2. Audit every deliverable before importing:
   - exact expected id and no skipped ids;
   - avatar files are square, solid-background images and remain recognizable at 120 px;
   - skill icons are square transparent PNGs, readable at 48 px, and corner pixels have alpha 0;
   - no text, letters, numbers, logos, watermarks, or frames inside the art.
3. Fix the only known failed file:
   - `ar.breach__CHECK.png` has a baked checkerboard and opaque corners;
   - remove the checkerboard to true alpha 0 while preserving the bullet-breaking-through-plank artwork;
   - save the corrected result as `ar.breach.png` in the handoff folder;
   - do not import or rename the `__CHECK` file as the final asset.
4. Install the avatars:
   - resize the 10 avatar images from 1024x1024 to 512x512 using high-quality bicubic resampling;
   - copy them to `Assets/Resources/UI/Avatars/` with their exact ids and `.png` extension;
   - preserve the 1024x1024 masters in the handoff/source folders.
5. Install the 35 skill icons:
   - copy the valid transparent files to `Assets/_Project/UI/Icons/Skills/` using exact ids;
   - the destination must contain `ar.breach.png`, not `ar.breach__CHECK.png`;
   - preserve alpha transparency.
6. Run the Unity authoring command `ZombieWar/UI/Authoring/Refresh Skill Icons` using the available Unity/editor automation. If it cannot be run non-interactively, report that clearly and leave the files correctly installed for the next editor launch.
7. Verify the result:
   - exactly 10 avatar ids are installed at 512x512;
   - exactly 35 skill-icon ids are installed;
   - all installed skill-icon corner pixels have alpha 0;
   - inspect Unity import settings or generated metadata where available: Sprite, no unwanted mipmaps, correct max size;
   - do not modify gameplay code unless a genuine integration blocker requires it. If code changes become necessary, follow the repository `AGENTS.md` GitNexus impact-analysis rules before editing.
8. Do not delete the handoff folder or either original generated folder. Avoid unrelated changes.

Return a concise report with installed counts, verification performed, the status of `ar.breach.png`, whether Refresh Skill Icons ran, and any blocker.

## Completion notification

After the task is genuinely complete and verified:

1. Use the `simplifier-vi` skill at Level 1 to reduce the final report to the outcome, verification performed, and any important remaining issue.
2. Rewrite that summary in natural English using no more than 80 words.
3. Run:

   python Tools\notify_done.py "<English summary>" --repeat 2

If the task is blocked or fails, run the same command with a short English explanation of the blocker. Report any notification-script failure in the written response.
