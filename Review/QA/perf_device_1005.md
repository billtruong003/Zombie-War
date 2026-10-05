# Device perf report — 2026-10-05

Device: b23ae9d5 (Snapdragon 8 Gen 3, Android), `HordeCall-dev.apk` built 06:39 (development build,
cheats on). Data was captured with `Tools/device_sampler.sh`:
- `adb shell top`/`dumpsys meminfo`/battery every 6 s → `samples.csv`
- the Unity profiler's frame summary → `frames.csv`
- logcat

The editor baseline (`perf_baseline.md`) is an unfocused editor and cannot be compared in absolute
terms; this is the first device reference.

## Numbers

| Session | Length | CPU avg / max (% of one core) | PSS avg / max | Battery temp max |
| --- | --- | --- | --- | --- |
| `device_1005` (first runs, menu tour) | 13 min | 153 / 218 | 1,301 / 1,331 MB | 38.2 °C |
| `device_1005b` (owner's long run, 23+ min) | 60 min | 133 / 214 | 1,394 / 1,437 MB | 43.2 °C |

Frame time, `device_1005`, one 2,000-frame window per minute:

| Window | avg ms | p95 ms | max ms | frames > 33 ms | frames > 50 ms |
| --- | --- | --- | --- | --- | --- |
| 03:41 | 17.7 | 22.8 | **357.4** | 14 | 6 |
| 03:42 | 17.5 | 21.6 | 83.9 | 18 | 5 |
| 03:43 | 16.7 | 17.5 | 49.1 | 3 | 0 |
| 03:44 | 16.8 | 17.5 | 51.3 | 6 | 1 |
| 03:45 | 17.1 | 24.2 | 45.2 | 3 | 0 |
| 03:47 | 17.2 | 25.0 | 50.0 | 2 | 1 |
| 03:48 | 17.0 | 21.1 | 38.7 | 2 | 0 |

The game holds 60 fps (avg ≈ 17 ms). Hitches are the issue, not the average.

## Hitches and their causes

| Hitch | Where | Cause | Status |
| --- | --- | --- | --- |
| 357 ms (03:41) | `EventSystem.Update` 297 ms, inside a tap | That tap logged errors with full stack traces: the `MenuBackground _MainTex` error ×4 per screen and DEV purchase/ad notes. On Android every traced line costs milliseconds. | Error sources fixed in `53cf62540`. Log/Warning traces dropped in player builds (`LogCost`, `ecb1e80bb`). |
| 14 ms in the same frame | `GUIStyle.GetDimensions` (IMGUI) | The cheat panel's IMGUI layout | Dev-only. Gone when `ZW_CHEATS` is off for release. |
| 60–84 ms (03:42) | `ActivateAwakeRecursively` + `CanvasUpdate.PreRender` | A screen opening for the first time: Awake of every child plus a full canvas rebuild | Acceptable for a first open. Re-measure once the new UI from round 2 is in. |
| 50 ms (03:44) | `EventSystem.Update` 40 ms | Another tap. Same family as the first. | Verify on the next build. |
| Every frame while a preview is open | Render Graph error, `Outline Selection Mask` MSAA mismatch: **5,338 traced errors** in the logs | MSAA mismatch on turntable cameras | Fixed in `53cf62540`. |
| 115 traced errors | `ContactShadows` out of slots (160) | Capacity too small for a big horde | Raised to 256 in `53cf62540`. |

## Memory and heat

- **PSS 1.3–1.44 GB is high for the mid-range target.** It climbs about 100 MB over an hour, so there
  is no runaway leak, but the base is heavy. Likely contributors to measure with the Memory Profiler on
  the next build: VAT textures for enemies, gun/outfit preview RenderTextures, uncompressed audio
  preloaded at start. This is `BACKLOG` #54/#72.
- Battery reached 43.2 °C in the hour-long session. No thermal throttling was reported
  (thermal_status 0).

## To check on the next build (no build until the owner asks)

1. There is no frame above 100 ms on a tap (Home tabs, Outfit, Gacha, Pass buy).
2. Logcat shows no `Render Graph Execution error`, no `_MainTex`, no `ContactShadows`.
3. PSS is below 1.3 GB after 20 min. Take a Memory Profiler snapshot of the menu and of minute 10.
