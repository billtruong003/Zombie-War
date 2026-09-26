# M8 step B — skill feel audit (2026-09-26)

Owner approved the whole list in `skill_proposal.md` with one condition: "làm chuẩn chỉ… game feel
khi thể hiện ra chưa ổn". Every power was granted in the editor with the dev cheat
`zw.skill` / `zw.skill.max <id>`, frozen at the moment it lands, and screenshotted
(`feel/before_*.jpg`, `feel/after_*.jpg`). What each capture showed, and what was changed because of it:

| Power | What the capture showed | Fix |
|---|---|---|
| All powers | Silent. The catalog had no skill cue at all. | 17 `sfx.skill.*` cues from Epic Toon FX / Deadly Kombat, throttled per key |
| All targeted powers | `enemyMask = Everything` and every object sits on Default: the sweep returned the PLAYER and props. Drone's "nearest target" was the player (never fired); an airstrike landed on the player. | `TargetQuery.GatherEnemies` keeps living enemies only |
| Chain Lightning | Bolt invisible on white skeletons; 12 m bolts drawn straight read as a laser; crowd 8 m away got nothing but the cooldown was spent; hop search walked the WHOLE buffer (stale enemies from older queries) | two-layer bolt (saturated glow + white core), jag and segments scale with length, first arc reaches 12 m, empty procs refund (0.25 s retry, no shared budget), hops limited to this proc's candidates |
| Ordnance | Marker and blast in the same frame — no anticipation; could pick a cluster off screen | marker + converging red target ring, shell lands 0.45 s later; on-screen clusters only |
| Area powers (Frost, blasts, Soul Burst, Emergency) | Effects at prefab size: a 6 m frost nova read as a few snowflakes | every effect scaled from its measured native radius + an expanding ground ring that IS the hitbox |
| Orbit Blades | Knife model 0.37 m long, Built-in Standard material rendered magenta in URP | procedural saw disc (vertex-coloured, URP), trail |
| Boomerang | TrailRenderer at 20-36 m/s kept its launch point: an 8 m orange bar that read as a laser | velocity streak recomputed every frame, bounded to 2.5 m |
| Airstrike | Targets picked in a 13 m sphere: most bombs landed off the sides of a portrait screen | on-screen enemies only, then on-screen ground near the player; converging ring + falling missile + blast + decal + shake |
| Drone Buddy | 0.35 scale glow, never fired (see targeting) | 0.6 scale, fires tracer + muzzle + hit |
| Frost Nova / Absolute Zero | — | slow/freeze plus a per-instance status tint (new `_StatusTint` on the enemy shader) |
| Kinetic Shield | charge state invisible | shield bubble on the player while charged + sounds |

Balance notes (not changed yet — playtest first): Boomerang rank 3 (22/hit, pierce all, twice)
clears a 70-enemy tier-0 crowd in one throw; Absolute Zero overkills tier-0 so the frozen tint is
rarely seen on fodder. Evolution requirement was eased from "partner at max" to "partner owned",
because a 4-6 minute run cannot max both.

Still to judge on a device: explosion glow quads intersecting the ground draw a faint straight
edge (ETFX billboards, no soft particles); fire patch brightness.

## Pacing with the new powers (starter pistol, circling bot, cards picked in rotation)

| Probe | First card | Level 5 | Level 10 | Level at 5:00 | Damage taken |
|---|---|---|---|---|---|
| XP 25 + (L-1)^1.35 x 12 (old) | 19 s | 113 s | — (died 3:31 at Lv 7) | — | died |
| XP 18 + (L-1)^1.2 x 7 | ~20 s | ~70 s | ~240 s | 11 | 0 (run cut short) |
| **XP 14 + (L-1)^1.05 x 5 (shipped)** | **10 s** | **55 s** | **152 s** | **18** | 0 — every one of 44 hits in 8 min absorbed by Kinetic Shield |

The last column drove one balance change: Kinetic Shield recharges over 60/52/44 m (was 40/32/24 m,
i.e. every ~5 s at run speed). A circling bot is not a player — human playtest is the next gate.
