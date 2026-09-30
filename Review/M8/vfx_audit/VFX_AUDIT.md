# VFX audit — skill effects vs Epic Toon FX (30/09/2026)

## Finding

Every particle prefab in `SkillFxLibrary` (65 references) is already an Epic Toon FX prefab or a
flat variant of one. The "hand-made" look comes from six procedural helpers drawn on top of them
with our own shaders (hard edges, flat alpha, no soft falloff):

| Helper | What it draws | Shader | Call sites |
|---|---|---|---|
| `SkillArsenal.Shockwave` | expanding ground ring | own `ZombieWar/FX/ToonErode` | Gravity, Stomp, Guardian, Ice Shards, Landmine, Soul/Emergency burst (2), Thorns, Time Warp, Bomb/Freeze/Magnet items (3), station claim, LMG belt |
| `SkillArsenal.ShowDisc` | flat coloured disc / band | own `ZombieWar/FX/SkillDisc` | Fire Trail, Frost Nova, Gravity (2), Stomp, Launcher napalm, Meteor burn, Run & Gun, Emergency, Turret drop, Toxic, Bomb/Freeze items |
| `SkillFxDirector.Pulse` | thin line ring | own `ZombieWar/FX/SkillLine` | Chain, Frost, Gravity, Stomp, Guardian, Explosive rounds, Soul burst, Storm Cloud, Thorns, Time Warp, overflow heal, items |
| `SkillFxDirector.ConeWave` | line fan | `SkillLine` | Flame Burst, Reaper, LMG Shockwave Belt |
| `SkillFxDirector.Converge` | closing line rings | `SkillLine` | Gravity, delayed-blast telegraph (Airstrike, Ordnance, Carpet Bomb) |
| `SkillFxDirector.DrawArc` | zig-zag bolt | `SkillLine` additive | Chain Lightning, Storm Cloud, Plague link |

Persistent custom visuals (second pass, owner call): Thorn Aura ring, Kinetic Shield shell
(`SkillShield`), orbit path line, Storm Cloud shadow.

## Plan (Epic Toon only, nothing invented)

1. `Shockwave` → an Epic Toon nova, flat variant, picked by colour family (Fire, Blue, Green, Pink,
   Yellow, Frost), scaled to the ring radius with `PowerKit.PlaySized`.
2. `ShowDisc`, `Pulse`, `Converge`, `ConeWave` → removed where an Epic Toon effect already covers the
   moment (fire field, stinky cloud, vortex, flamethrower, nova, heal burst). Where it is the only
   visual:
   - blast telegraph → Epic Toon `Magic Circle Simple`;
   - LMG cone → `Sword Wave`;
   - Run & Gun trail → `Dust` puff;
   - overflow heal → `HealOnceBurst`.
3. `DrawArc` keeps its zig-zag path (a bolt must join two points) but draws with Epic Toon's own
   `lightning1_ADD` material.
4. Delete the unused shaders and materials afterwards; before/after captures per skill with forced
   procs.

## Risk

GitNexus upstream impact: `ShowDisc`, `Pulse`, `ConeWave` and `Converge` are CRITICAL (168–394
symbols), and `Shockwave` is UNKNOWN. They are called from nearly every power, so the graph pulls in
the whole combat loop. The helpers only draw; damage and targeting code is untouched. Test
`SkillLegibilityTests` requires a telegraph for delayed blasts, so the telegraph stays, drawn by
Epic Toon.

Evidence: `Review/M8/sandbox/0930_140141_vfx_before/` (42 skills × 5 frames),
`Review/M8/vfx_audit/candidates/` (Epic Toon nova candidates at game camera).
