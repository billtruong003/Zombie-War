# Zombie War — skill icon prompts for ChatGPT (35 icons)

Owner request 2026-09-26: minimal toon style, **transparent background**. One icon per card in
`SkillCatalogDefs` (23 originals + 6 powers + 6 evolutions). File names match the card ids so they
drop straight into the game.

## How to generate (read once)

1. Open ONE ChatGPT chat and keep every icon in it, so the style stays consistent.
2. First message: paste the **STYLE BLOCK** below, then the first icon line. Generate, and repeat
   until you like it. That icon becomes the style anchor.
3. For every next icon: "Same style as the approved icon. " + the icon line. Attach the approved
   anchor image again every ~8 icons if the style starts drifting.
4. **Transparent check:** the downloaded PNG must show the checkerboard in an image viewer. If
   ChatGPT returns a white or coloured square, reply: *"Regenerate with a fully transparent
   background (alpha 0). No backdrop, no frame, no square, no shadow on the ground."*
5. Save as `<id>.png` (for example `auto.orbit.png`), 1024 × 1024. The game shrinks them to
   46–64 px, so if a detail disappears at thumbnail size, ask for "bolder, fewer details".

## STYLE BLOCK (paste first, English works best)

> Game UI skill icon for a cute cartoon zombie-survival mobile game. Minimal toon style: one bold
> central object, thick dark outline (#1F2330, about 6% of the icon width), flat colours with ONE
> soft cel-shade band and ONE small white highlight, no gradients, no texture, no text, no
> letters, no numbers. Chunky rounded shapes that read clearly at 48 × 48 pixels. The object fills
> about 80% of the canvas, centred, slight 3/4 view. **Fully transparent background (PNG with
> alpha), no backdrop circle, no square tile, no frame, no drop shadow on a floor.** Square
> 1024 × 1024. Main colour for this icon: {COLOUR}.

Replace `{COLOUR}` with the layer colour of the icon (the card border in game uses the same one):

| Layer | Colour | Hex |
|---|---|---|
| Stat | steel grey-blue | #9EA8B8 |
| Signature (weapon family) | warm orange | #FF9E3D |
| Autonomous power | violet | #A86CFF |
| Universal | teal | #42D1C2 |
| Evolution | gold, with a small sparkle burst behind the object | #FFCC33 |

## Icon list

### Stat (#9EA8B8)
| File | Subject line |
|---|---|
| `stat.damage.png` | A chunky upward-pointing bullet with a small burst star at its tip |
| `stat.firerate.png` | Three bullets flying side by side with short speed lines |
| `stat.movespeed.png` | A cartoon sneaker with a winged heel and speed lines |
| `stat.maxhealth.png` | A plump heart with a small plus sign cut into it |
| `stat.coingain.png` | A stack of three round coins with a magnet hovering above |

### Signature (#FF9E3D)
| File | Subject line |
|---|---|
| `sidearm.rungun.png` | A pistol with motion lines behind it, as if drawn while running |
| `sidearm.quickstep.png` | A pistol bullet trailing footprints behind it |
| `smg.static.png` | A small SMG crackling with a blue electric spark |
| `smg.bullethose.png` | A spray of many bullets fanning out from a round muzzle |
| `ar.focusfire.png` | A crosshair locked onto a small target, tightening rings |
| `ar.breach.png` | A long rifle bullet punching through a cracked wooden plank |
| `shotgun.pointblank.png` | A shotgun muzzle blast, wide and very close, big flash |
| `shotgun.concussion.png` | A shotgun shell with dizzy stars circling it |
| `lmg.heavypressure.png` | A heavy ammo belt coiled like a snake, glowing hot |
| `lmg.shockwave.png` | A cone-shaped shockwave blast pushing outward |
| `marksman.longshot.png` | A sniper scope with a tiny distant target inside |
| `marksman.hunters.png` | A target reticle with a small skull mark in the centre |

### Universal (#42D1C2)
| File | Subject line |
|---|---|
| `uni.execution.png` | A bullet above a cracked, almost empty health bar |
| `uni.kinetic.png` | A round bubble shield with a footprint inside it |

### Autonomous power (#A86CFF)
| File | Subject line |
|---|---|
| `auto.chainlightning.png` | A zig-zag lightning bolt linking three small dots |
| `auto.ordnance.png` | A round artillery shell falling onto a target circle |
| `auto.soulburst.png` | A cute little ghost bursting out in a ring of light |
| `auto.emergency.png` | A red panic button with a small explosion around it |
| `auto.orbit.png` | Three spinning saw blades arranged in a ring |
| `auto.drone.png` | A cute round little drone with one eye and tiny propellers |
| `auto.frostnova.png` | A big snowflake inside an expanding icy ring |
| `auto.firetrail.png` | Three small flames in a row, like footprints on fire |
| `auto.boomerang.png` | A curved boomerang with a swoosh arc showing it returns |
| `auto.airstrike.png` | A missile pointing down at a red target marker |

### Evolution (#FFCC33, sparkle burst behind)
| File | Subject line |
|---|---|
| `evo.thunderstorm.png` | A storm cloud shooting a big lightning bolt downward |
| `evo.carpetbomb.png` | Three shells falling in a row onto the ground |
| `evo.buzzsaw.png` | One big glowing buzzsaw ring with many teeth |
| `evo.absolutezero.png` | A cute zombie head frozen inside an ice cube |
| `evo.squadron.png` | Three small drones flying in a V formation |
| `evo.reaper.png` | A small cute grim-reaper scythe with a ghost wisp |

## Style anchor (made for this project)

`icon_style/sample_auto.drone.png`, `sample_stat.maxhealth.png`, `sample_evo.carpetbomb.png` —
rough programmer art that shows the RULES (thick dark outline, flat colour, one cel-shade band,
one highlight, layer colour, transparent background). You may attach them to the first ChatGPT
message with: *"Follow the rules these show, but draw it polished and cuter — they are only a
rough guide."* Do not ask it to copy them.

## Style references (look, don't copy)

Packs with the minimal, thick-outline, transparent look we want. Use them only as a mood
reference for yourself; do not upload them to ChatGPT as images to copy.

- [Cartoon UI and Icon pack — Asep Bagus](https://asep-bagus.itch.io/cartoon-ui-and-icon-pack): flat cartoon, friendly colours
- [120+ Casual Game Icons — Zhaohui Li](https://lizhaohui12138gmailcom.itch.io/120-casual-game-icons-pack): flat cartoon with a black outline, transparent PNG
- [600 Minimal Game Icons — SunGraphica](https://sungraphica.itch.io/minimal-game-icons-pack): minimal, one object per icon
- The Level Up and HUD artboards in the M8 mockup canvas show where and how big the icons sit.

The existing `Assets/Icons/skills` set (74 px, opaque grey tile, military two-tone) does not fit
the cute toon look and is not transparent — do not use it as the reference.

## When they are ready

Drop the 35 PNGs into `Assets/_Project/UI/Icons/Skills/` with the names above. The game will map
them by card id (`SkillCatalogDefs.ById`); a missing file falls back to the coloured layer badge.
