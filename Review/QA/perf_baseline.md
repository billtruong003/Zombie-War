# Perf baseline (G0.4) — 2026-10-04, before the fix plan

Scenario (repeat exactly for every perf group):
1. Wipe PlayerPrefs, Play from Bootstrap, mark every FTUE step done, press PLAY (Map_Level1).
2. `zw.god`, `zw.horde.watch` at ~3 s.
3. Drive the joystick in a slow circle (angle = time*0.3, radius 140 px), pick level-up card 0 whenever offered.
4. `zw.horde.report` at ~90 s of run time.

Editor, Game view 1080x1920, Editor window unfocused (owner works remotely), so FPS/PlayerLoop are only comparable run-to-run, not absolute.

| Metric | Baseline |
|---|---|
| Run time / kills | 94 s / 147 |
| Save flushes (PlayerPrefs.Save) | 147 (≈ 1 per kill) |
| GC alloc | 3,530 KB/s |
| PlayerLoop avg | 45.1 ms |
| Worst frame | 4,423 ms (includes level-up pause/unfocus stalls) |
| Peak alive | 36 |

Also seen: "There are no audio listeners in the scene" while loading into the run (zero listeners during the menu→world handoff) — tracked with G4.4.
