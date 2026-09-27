# HordeCall — profile pictures (avatars) and frames

Owner 2026-09-27: the profile picture need not be the character. These reference renders are for
drawing 1:1 avatars in ChatGPT; the game shows them in the profile picker as soon as a sprite with
the avatar id exists in `Assets/Resources/UI/Avatars/<id>.png` (512x512, square, transparent or
full-bleed). Until then the picker shows an "ART SOON" placeholder.

## Reference renders (`refs/`, transparent, 1024x1280)

`NN_<look>_front.png` (facing) and `NN_<look>_3q.png` (turned, near side view) for 12 in-game looks:
Santa, Street Artist, Red Rebel, Tiger Cub, Little Dino (turtle-ninja body + teal dino hood),
Blue Shark, Offbeat Punk, Candy Scientist, Red Mask Warrior, Medieval Adventurer,
Jungle Marksman, Viking Warrior. `refs_sheet.png` shows them side by side.

## Five styles (ids the game already expects)

| Style | Ids | Idea | Unlock (in game) |
|---|---|---|---|
| Chibi gunner portrait | `avatar.chibi_1`, `avatar.chibi_2` | head + shoulders of a look, big eyes, holding a toy-like gun, bright rim light | free / level 8 |
| Squad badge | `avatar.badge_1`, `avatar.badge_2` | round or shield emblem with the character's face in the middle, stars and ribbon | free / level 12 |
| Zombie mascot | `avatar.mascot_1`, `avatar.mascot_2` | a cute, not scary, cartoon zombie (green, stitched, one tooth), with a crown for the King | 25 runs / 5,000 kills |
| Weapon emblem | `avatar.weapon_1`, `avatar.weapon_2` | two crossed guns over a burst; the gold version is a shiny rifle | free / own 10 guns |
| Sticker pose | `avatar.sticker_1`, `avatar.sticker_2` | sticker cut-out of a look doing thumbs up / victory sign, white sticker border | 50 runs / survive 15:00 |

Good pairs from the refs: chibi → Blue Shark, Little Dino; badge → Red Mask Warrior, Viking;
sticker → Tiger Cub, Santa (seasonal); weapon → Jungle Marksman palette.

## Prompt template (paste the reference image with it)

> Square 1:1 mobile game avatar icon, cute chunky low-poly 3D toy style matching the attached
> character (same outfit, colours and proportions). [STYLE LINE]. Soft studio lighting, bright
> saturated colours, clean simple background in [COLOUR], centred, readable at 64 px, no text,
> no watermark.

Style lines:
- Chibi: "head and shoulders portrait, big shiny eyes, holding a small toy blaster, rim light"
- Badge: "character face inside a round shield emblem with two stars and a ribbon, gold trim"
- Mascot: "a cute friendly cartoon zombie mascot wearing this outfit, green skin, one tooth, happy"
- Weapon: "two crossed toy guns over a starburst, the outfit colours as the burst"
- Sticker: "full body sticker, thumbs-up pose, thick white sticker outline, slight drop shadow"

## Frames (in game, shader-drawn — no art needed)

Classic (always), Bronze (lv 5), Silver (lv 10), Gold (lv 20), Stamp Master (28 stamps),
Season 1 (Pass lv 20), Neon (x10 on an event banner), Toxic (10,000 kills), Flame (survive 20:00),
Frost (25 bosses), Royal (win a Legendary), Legend (every badge, rainbow).
