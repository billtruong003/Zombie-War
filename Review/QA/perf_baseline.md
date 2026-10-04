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

## After G9 (2026-10-04) — same scenario

| Metric | Baseline | After G5–G9 |
|---|---|---|
| Run time / kills | 94 s / 147 | 98 s / 140 |
| Save flushes during the run | 147 | 6 (1 more at run close) |
| GC alloc | 3,530 KB/s | 2,028 KB/s (−43%) |
| PlayerLoop avg | 45.1 ms | 46.5 ms (editor-bound, unfocused) |
| Peak alive | 36 | 82 (heavier horde this run) |

The GC rate fell even though more than twice as many enemies were alive at the peak. PlayerLoop in an
unfocused editor is dominated by editor overhead and does not move; read it on device with the
profiler (device-profiler-only).

Stress reference (G8, 100 alive, powers maxed): 5,348 KB/s before G9.

Left for later, profiling needed first: moving per-zombie Update into one manager tick (FullTick), and
pool SetParent churn.
