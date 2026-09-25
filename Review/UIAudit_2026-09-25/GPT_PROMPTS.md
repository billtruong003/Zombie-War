# Zombie War: UI prompts for image generation (2026-09-25)

Attach the matching screenshot from this folder with each prompt and add: "Keep the layout
logic, redesign the look." Generate at a 9:16 portrait ratio (1080×1920).

## Shared style block (paste before every prompt)

> Mobile game UI screen, portrait 9:16, casual cartoon shooter "Zombie War". Chunky rounded
> panels on a dark navy background (#10141C, panels #1B2130), one warm gold call-to-action color
> (#F5B841 with a darker bottom bevel #B9791A), cyan only for gems (#5BD9E8). Rarity colors:
> common grey #9AA3B2, uncommon green #4CAF6E, rare blue #4FA3F7, epic purple #8B7BD8, legendary
> orange #F2994A. Bold rounded display font like Lilita One for titles and big numbers, and a
> heavy rounded sans like Nunito Black for labels. Big thumb-sized buttons, strong readable
> hierarchy, no clutter, no gore. The characters are chibi cartoon survivors and the enemies are
> cute-creepy monsters. Clean flat UI illustration, high contrast, no photorealism, no
> watermark, English text only.

## 1. Hub (01_hub.png)

> Home screen. Top: small avatar with a player level badge, then coin and gem counters with
> green "+" buttons. Below, a stage card: "STAGE 2 / 5 · Thorn Fields · 6 waves", a green power
> bar reading "Power 820 / 700", and a row of reward chips. The middle is a large stage showing
> the 3D chibi hero standing on a round podium, with three small weapon slot cards stacked on
> the right edge (purple, blue, grey borders). A huge gold PLAY button with the subtitle "Stage
> 2 · Thorn Fields". Two small cards under it: "Daily reward · Ready" and "Pass Lv 3" with a
> cyan progress bar. Bottom nav with 5 tabs: Home (active, gold), Loadout, Shop, Costume, Pass.

## 2. Campaign select (the screen is missing today)

> Vertical list of 5 stage cards. Stage 1 "Outbreak" cleared with 3 gold stars. Stage 2 "Thorn
> Fields" is highlighted with a gold border and a "NEXT" tag. Stage 3 "Bone Graveyard" is locked
> with a padlock and an orange progress bar "Need Power 1,100". Stages 4–5 are dimmed. A sticky
> bottom bar with a big gold "PLAY STAGE 2" button.

## 3. Loadout (02_loadout.png)

> Three large equipped weapon slot cards at the top (sidearm, slot 2, slot 3), each with a
> colored rarity border, a side-profile gun silhouette, the gun name and 1–3 stars. Below, a
> detail panel for the selected gun "G36C · EPIC · RIFLE" with four stat tiles (Damage 18, Rate
> 9/s, Mag 30, DPS 142), a shard progress bar and a green "Upgrade · 800" button. Family filter
> chips, then a 3-column grid of owned guns. Unowned guns have dashed borders and a price.

## 4. In-run HUD (13_ingame_wave.png)

> Top-down view of a dusty arena with a chibi survivor in the center shooting at cute-creepy
> monsters. A thin cyan XP bar across the very top with "Lv 3". Top left: a green health bar
> "82 / 100" with a thin blue shield bar under it and two small buff tiles ("2× COIN 12s",
> "∞ AMMO 5s"). Top right: coin counter and pause button. Top center: "WAVE 2 / 6" with a red
> progress bar and "14 enemies left". Bottom left: a translucent floating joystick. Bottom
> right: a big round active-weapon button with a gold ammo ring, two smaller weapon buttons and
> a bomb button with a count badge. Floating damage numbers, crits in bigger yellow.

## 5. Level-up choice

> The game is paused under a dark overlay. Big cyan title "LEVEL UP!" and "pick one". Three
> horizontal cards stacked vertically, each with a square icon, a title, a type tag (RIFLE
> SKILL / STAT) and a one-line effect with a bold number. The first card has a gold border and 5
> level pips (2 filled). At the bottom: a "Your build" chip row and an outlined "Reroll (1 left)"
> button.

## 6. Result (11_run_over.png)

> Victory screen, "CLEARED!" in big gold, 2 of 3 stars and the hint "3rd star: finish above 50%
> HP". Three stat tiles: Kills 184, Time 6:42, Level 9. A reward breakdown card: coins picked up,
> stage reward, first-clear bonus, then a total with coin and gem icons. A Pass XP bar that
> levels up. Gold "NEXT STAGE" button and an outlined "Home" button. Same fonts and colors as the
> menus.
> Defeat variant: title "DOWN AT WAVE 4", kept coins shown clearly (50%), and two suggestion
> buttons: "Upgrade G36C" and "Try Shotgun".

## 7. Shop crate / gacha (04_shop_gacha.png)

> A shop tab bar (GUNS, CRATES active, OUTFITS, UPGRADE). A purple-framed "Weapon Crate" banner
> that shows the 3 best guns in it, a "Drop rates" button, a pity bar "Epic or better guaranteed
> in 12 pulls", and two big buttons: green "OPEN ×1 · 100 gold" and gold "OPEN ×10 · 900 gold"
> with a red "-10%" tag. Below: a smaller cyan "Outfit Crate" row and a "Last opened" strip of
> rarity-colored squares.
> Bonus prompt: a reveal animation frame of a crate bursting open with a purple glow, the gun
> card flipping in.

## 8. Costume (07_costume.png, 05_shop_costume.png)

> A big 3D preview of the chibi hero at the top, with the set name "Desert Ranger · 5 pieces" and
> a cyan "Buy set · 120" button. A tab bar: SETS (active), HAT, TOP, BOTTOM. A 3-column grid of
> round bust-portrait icons (face and shoulders, not tiny full bodies), each with a name and a
> state: EQUIPPED, a gem price, a coin price, or an unlock condition ("Pass Lv 10", "Clear Stage
> 5").

## Ideas to try across several variants

- **Theme identity:** "cute-creepy monsters" (current Cute pack) vs "cartoon zombies". Generate
  the same Hub in both and pick one. Today's name and art contradict each other.
- **Hub background:** a blurred stage diorama behind the hero instead of the yellow/black hazard
  stripes.
- **Weapon icons:** filled, colored side-profile guns with a dark outline, not white line art.
- **Costume icons:** bust crops at 3/4 angle for face parts. Full-body icons only for sets.
- **Result screen:** use the menu font and navy panels. The red full-screen tint reads as an error.
