# HordeCall concept references v2 (30/09/2026)

Every image is a real in-game render (Unity sandbox). Files per concept in `final/`: `<id>.jpg` colour, `<id>_sketch.jpg` B/W proportion check, `<id>_layout.jpg` composition with thirds grid.

## Batch prompt (all 20 in one go)

```
You are producing 20 key-art images for HordeCall in one batch. Do not stop to ask me anything; work through the whole list.

Read first:
1. The style block below (applies to every image).
2. The reference folder D:\Projects\Zombie-War\Review\KeyArt\concepts_v2\final. For each concept id there are three files:
   <id>.jpg = in-game colour render (ground truth: outfit, colours, enemy design, proportions)
   <id>_sketch.jpg = black and white line + tone (proportion check)
   <id>_layout.jpg = 3-value layout with a thirds grid (composition)
3. The per-concept brief (listed below, and in D:\Projects\Zombie-War\Review\KeyArt\concepts_v2\CONCEPT_PROMPTS.md).

For each concept, in order 01 to 20:
a. Open its three reference files.
b. Self-check (no need to show me): hero head about 45% of body height (about 2.2 heads tall); the Blue Shark hood keeps round black eyes, white teeth rim, blue shorts, shark boots and tail; gun and enemy sizes relative to the hero match the render; big dark/mid/light shapes and subject placement match the layout.
c. Render the final colour image following the style block. Keep the camera angle of the reference.
d. Save it to D:\Projects\Zombie-War\Review\KeyArt\generated_v2\<id>.png (create the folder if needed), then continue with the next one.

Rules: no text, logo, UI or watermark; no blood or gore; enemies stay cute and goofy; do not redesign characters, only repaint them as illustrated key art. If one image fails the self-check, redo that image before moving on.
At the end, list the 20 saved paths and mark any you are unsure about.

STYLE BLOCK
Art direction for HordeCall, a cute zombie-survival mobile shooter (portrait, auto-aim, huge hordes).
- Chunky low-poly 3D toy look like vinyl figures: big heads, short bodies, soft rounded shapes,
  flat-shaded faces with gentle gradients, clean silhouettes. Match the attached character and
  enemy references exactly in outfit, colours and proportions.
- Bright, saturated, sunny palette: sky blue, grass green, warm yellow, with purple/magenta accents.
  Cheerful and action-packed, a little funny.
- Zombies are cute and goofy, never scary: no blood, no gore, no wounds.
- Guns are real models drawn chunky and toy-like; muzzle flashes and tracers are bright and clean.
- Soft cinematic light, warm key light from the top-left, light dust and glow for depth.
- No text, no letters, no logo, no UI, no watermark unless the brief asks for the logo.

BRIEFS
01 01_turnaround: Blue Shark turnaround (Character sheet). Front, 3/4, side and back of the Blue Shark hero holding an assault rifle. Keep the hood with round black eyes and white teeth rim, blue shorts, blue shark boots, tail behind. Head is about 45% of the full height (about 2.2 heads tall).
02 02_outfit_lineup: Outfit lineup (Character sheet). Six heroes side by side, same pose and scale: Tiger Cub, Turtle Ninja, Blue Shark (centre, the iconic one), Viking, Cotton Sheep, Red Mask. Same chibi body under every outfit; only hood/costume changes.
03 03_weapon_lineup: Weapon classes (Prop sheet). The Blue Shark hero holding each gun class in turn: pistol, SMG, assault rifle, shotgun, LMG, marksman rifle, grenade launcher. Guns keep their real proportions against the small body: rifles are as long as the hero's torso plus head.
04 04_enemy_roster: Enemy roster at scale (Enemy sheet). The hero at the left for scale, then the enemies in a row facing the viewer: pup, cactus, mole rat, lightning cat, bark dog, big cactus, bowwow, skeleton, skeleton mage. Keep the relative heights exactly as in the reference.
05 05_boss_scale: Bosses at scale (Enemy sheet). Hero next to the three bosses: Cactus Boss (about 2.5x hero height, horns of cactus), Mole Rat King, Skeleton Giant (tall and thin). Bosses stay goofy, not scary.
06 06_poster_low: Hero poster (Key art · portrait). Low camera, Blue Shark hero in the foreground turned toward the viewer with the rifle across the chest, a crowd of cute enemies behind at mid distance, big open sky for a title.
07 07_wave_back_wide: Facing the wave (Key art · landscape). Over-the-shoulder: the hero from behind facing a wide wave of mixed enemies coming across the plain. Hero on the left third, crowd fills the right two thirds.
08 08_wave_back_portrait: Facing the wave (portrait) (Key art · 9:16). Same moment in portrait for the loading screen: hero large in the lower half seen from behind, the horde above toward the horizon, clear sky at top for the logo.
09 09_gameplay_buzzsaw: Gameplay: Buzzsaw Halo (Gameplay camera). The real game camera, tilted top-down: hero in the centre, six golden saw blades orbiting on a hexagon, enemies closing from every side.
10 10_launcher_blast: Grenade launcher (Action). Hero from behind-right firing a grenade launcher into a packed crowd; one bright blast, enemies bouncing, no gore.
11 11_relay_hexpad: Relay capture (Mechanic). The hexagonal capture pad (dark metal tiles, yellow-black hazard rim, blue energy lines) with the hero standing on it while the horde closes in from all sides.
12 12_drop_pod: Supply drop pod (Mechanic). The orange-white drop pod on four legs landing behind the hero, pink ring on the ground marking the zone to stand in.
13 13_heal_plinth: Heal plinth (Mechanic). The low round plinth with a floating green crystal and dashed green rings on the ground; hero resting inside while enemies come from far away.
14 14_cactus_boss: Cactus Boss fight (Boss moment). Over the shoulder of the hero facing the Cactus Boss up close, small cacti around it. Low camera so the boss looks huge.
15 15_molerat_king: Mole Rat King (Boss moment). The hero on the right aiming across the frame at the Mole Rat King and a crowd of mole rats on the left.
16 16_skeleton_host: Skeleton host (Enemy moment). Portrait: hero in the foreground from behind with a rifle, a squad of goofy skeletons and a hooded skeleton mage in front, a skeleton giant behind.
17 17_pets_swarm: Cats and dogs swarm (Gameplay camera). Tilted top-down: hero in the middle with a shotgun, cute cats and dogs (lightning cat, pups, fire dog) running in from all sides.
18 18_meteor_storm: Meteor Storm (Skill moment). Meteors falling in a line across the crowd, orange burn circles on the ground, the hero standing safe in front.
19 19_absolute_zero: Absolute Zero (Skill moment). Frost ring bursting out from the hero, enemies in a circle turned to pale ice statues, snow sparkles on the ground.
20 20_feature_wide: Store feature graphic (Key art · 1024x500). Wide: hero in the centre with the rifle, two groups of enemies coming from left and right, empty sky across the top for the logo.
```

## Single prompts (to redo one image)

### 01 · Blue Shark turnaround

```
Concept 01: Blue Shark turnaround (Character sheet).
Front, 3/4, side and back of the Blue Shark hero holding an assault rifle. Keep the hood with round black eyes and white teeth rim, blue shorts, blue shark boots, tail behind. Head is about 45% of the full height (about 2.2 heads tall).
References: D:\Projects\Zombie-War\Review\KeyArt\concepts_v2\final\01_turnaround.jpg (colour, ground truth for outfit, colours and enemy design), 01_turnaround_sketch.jpg (proportions), 01_turnaround_layout.jpg (composition, thirds grid).
Before drawing, check your plan against the sketch: head about 45% of the hero's height, gun length and enemy heights relative to the hero exactly as in the reference, subjects on the same thirds as the layout. Fix anything that is off, then render the final colour image in the style block.
Save as D:\Projects\Zombie-War\Review\KeyArt\generated_v2\01_turnaround.png
```

### 02 · Outfit lineup

```
Concept 02: Outfit lineup (Character sheet).
Six heroes side by side, same pose and scale: Tiger Cub, Turtle Ninja, Blue Shark (centre, the iconic one), Viking, Cotton Sheep, Red Mask. Same chibi body under every outfit; only hood/costume changes.
References: D:\Projects\Zombie-War\Review\KeyArt\concepts_v2\final\02_outfit_lineup.jpg (colour, ground truth for outfit, colours and enemy design), 02_outfit_lineup_sketch.jpg (proportions), 02_outfit_lineup_layout.jpg (composition, thirds grid).
Before drawing, check your plan against the sketch: head about 45% of the hero's height, gun length and enemy heights relative to the hero exactly as in the reference, subjects on the same thirds as the layout. Fix anything that is off, then render the final colour image in the style block.
Save as D:\Projects\Zombie-War\Review\KeyArt\generated_v2\02_outfit_lineup.png
```

### 03 · Weapon classes

```
Concept 03: Weapon classes (Prop sheet).
The Blue Shark hero holding each gun class in turn: pistol, SMG, assault rifle, shotgun, LMG, marksman rifle, grenade launcher. Guns keep their real proportions against the small body: rifles are as long as the hero's torso plus head.
References: D:\Projects\Zombie-War\Review\KeyArt\concepts_v2\final\03_weapon_lineup.jpg (colour, ground truth for outfit, colours and enemy design), 03_weapon_lineup_sketch.jpg (proportions), 03_weapon_lineup_layout.jpg (composition, thirds grid).
Before drawing, check your plan against the sketch: head about 45% of the hero's height, gun length and enemy heights relative to the hero exactly as in the reference, subjects on the same thirds as the layout. Fix anything that is off, then render the final colour image in the style block.
Save as D:\Projects\Zombie-War\Review\KeyArt\generated_v2\03_weapon_lineup.png
```

### 04 · Enemy roster at scale

```
Concept 04: Enemy roster at scale (Enemy sheet).
The hero at the left for scale, then the enemies in a row facing the viewer: pup, cactus, mole rat, lightning cat, bark dog, big cactus, bowwow, skeleton, skeleton mage. Keep the relative heights exactly as in the reference.
References: D:\Projects\Zombie-War\Review\KeyArt\concepts_v2\final\04_enemy_roster.jpg (colour, ground truth for outfit, colours and enemy design), 04_enemy_roster_sketch.jpg (proportions), 04_enemy_roster_layout.jpg (composition, thirds grid).
Before drawing, check your plan against the sketch: head about 45% of the hero's height, gun length and enemy heights relative to the hero exactly as in the reference, subjects on the same thirds as the layout. Fix anything that is off, then render the final colour image in the style block.
Save as D:\Projects\Zombie-War\Review\KeyArt\generated_v2\04_enemy_roster.png
```

### 05 · Bosses at scale

```
Concept 05: Bosses at scale (Enemy sheet).
Hero next to the three bosses: Cactus Boss (about 2.5x hero height, horns of cactus), Mole Rat King, Skeleton Giant (tall and thin). Bosses stay goofy, not scary.
References: D:\Projects\Zombie-War\Review\KeyArt\concepts_v2\final\05_boss_scale.jpg (colour, ground truth for outfit, colours and enemy design), 05_boss_scale_sketch.jpg (proportions), 05_boss_scale_layout.jpg (composition, thirds grid).
Before drawing, check your plan against the sketch: head about 45% of the hero's height, gun length and enemy heights relative to the hero exactly as in the reference, subjects on the same thirds as the layout. Fix anything that is off, then render the final colour image in the style block.
Save as D:\Projects\Zombie-War\Review\KeyArt\generated_v2\05_boss_scale.png
```

### 06 · Hero poster

```
Concept 06: Hero poster (Key art · portrait).
Low camera, Blue Shark hero in the foreground turned toward the viewer with the rifle across the chest, a crowd of cute enemies behind at mid distance, big open sky for a title.
References: D:\Projects\Zombie-War\Review\KeyArt\concepts_v2\final\06_poster_low.jpg (colour, ground truth for outfit, colours and enemy design), 06_poster_low_sketch.jpg (proportions), 06_poster_low_layout.jpg (composition, thirds grid).
Before drawing, check your plan against the sketch: head about 45% of the hero's height, gun length and enemy heights relative to the hero exactly as in the reference, subjects on the same thirds as the layout. Fix anything that is off, then render the final colour image in the style block.
Save as D:\Projects\Zombie-War\Review\KeyArt\generated_v2\06_poster_low.png
```

### 07 · Facing the wave

```
Concept 07: Facing the wave (Key art · landscape).
Over-the-shoulder: the hero from behind facing a wide wave of mixed enemies coming across the plain. Hero on the left third, crowd fills the right two thirds.
References: D:\Projects\Zombie-War\Review\KeyArt\concepts_v2\final\07_wave_back_wide.jpg (colour, ground truth for outfit, colours and enemy design), 07_wave_back_wide_sketch.jpg (proportions), 07_wave_back_wide_layout.jpg (composition, thirds grid).
Before drawing, check your plan against the sketch: head about 45% of the hero's height, gun length and enemy heights relative to the hero exactly as in the reference, subjects on the same thirds as the layout. Fix anything that is off, then render the final colour image in the style block.
Save as D:\Projects\Zombie-War\Review\KeyArt\generated_v2\07_wave_back_wide.png
```

### 08 · Facing the wave (portrait)

```
Concept 08: Facing the wave (portrait) (Key art · 9:16).
Same moment in portrait for the loading screen: hero large in the lower half seen from behind, the horde above toward the horizon, clear sky at top for the logo.
References: D:\Projects\Zombie-War\Review\KeyArt\concepts_v2\final\08_wave_back_portrait.jpg (colour, ground truth for outfit, colours and enemy design), 08_wave_back_portrait_sketch.jpg (proportions), 08_wave_back_portrait_layout.jpg (composition, thirds grid).
Before drawing, check your plan against the sketch: head about 45% of the hero's height, gun length and enemy heights relative to the hero exactly as in the reference, subjects on the same thirds as the layout. Fix anything that is off, then render the final colour image in the style block.
Save as D:\Projects\Zombie-War\Review\KeyArt\generated_v2\08_wave_back_portrait.png
```

### 09 · Gameplay: Buzzsaw Halo

```
Concept 09: Gameplay: Buzzsaw Halo (Gameplay camera).
The real game camera, tilted top-down: hero in the centre, six golden saw blades orbiting on a hexagon, enemies closing from every side.
References: D:\Projects\Zombie-War\Review\KeyArt\concepts_v2\final\09_gameplay_buzzsaw.jpg (colour, ground truth for outfit, colours and enemy design), 09_gameplay_buzzsaw_sketch.jpg (proportions), 09_gameplay_buzzsaw_layout.jpg (composition, thirds grid).
Before drawing, check your plan against the sketch: head about 45% of the hero's height, gun length and enemy heights relative to the hero exactly as in the reference, subjects on the same thirds as the layout. Fix anything that is off, then render the final colour image in the style block.
Save as D:\Projects\Zombie-War\Review\KeyArt\generated_v2\09_gameplay_buzzsaw.png
```

### 10 · Grenade launcher

```
Concept 10: Grenade launcher (Action).
Hero from behind-right firing a grenade launcher into a packed crowd; one bright blast, enemies bouncing, no gore.
References: D:\Projects\Zombie-War\Review\KeyArt\concepts_v2\final\10_launcher_blast.jpg (colour, ground truth for outfit, colours and enemy design), 10_launcher_blast_sketch.jpg (proportions), 10_launcher_blast_layout.jpg (composition, thirds grid).
Before drawing, check your plan against the sketch: head about 45% of the hero's height, gun length and enemy heights relative to the hero exactly as in the reference, subjects on the same thirds as the layout. Fix anything that is off, then render the final colour image in the style block.
Save as D:\Projects\Zombie-War\Review\KeyArt\generated_v2\10_launcher_blast.png
```

### 11 · Relay capture

```
Concept 11: Relay capture (Mechanic).
The hexagonal capture pad (dark metal tiles, yellow-black hazard rim, blue energy lines) with the hero standing on it while the horde closes in from all sides.
References: D:\Projects\Zombie-War\Review\KeyArt\concepts_v2\final\11_relay_hexpad.jpg (colour, ground truth for outfit, colours and enemy design), 11_relay_hexpad_sketch.jpg (proportions), 11_relay_hexpad_layout.jpg (composition, thirds grid).
Before drawing, check your plan against the sketch: head about 45% of the hero's height, gun length and enemy heights relative to the hero exactly as in the reference, subjects on the same thirds as the layout. Fix anything that is off, then render the final colour image in the style block.
Save as D:\Projects\Zombie-War\Review\KeyArt\generated_v2\11_relay_hexpad.png
```

### 12 · Supply drop pod

```
Concept 12: Supply drop pod (Mechanic).
The orange-white drop pod on four legs landing behind the hero, pink ring on the ground marking the zone to stand in.
References: D:\Projects\Zombie-War\Review\KeyArt\concepts_v2\final\12_drop_pod.jpg (colour, ground truth for outfit, colours and enemy design), 12_drop_pod_sketch.jpg (proportions), 12_drop_pod_layout.jpg (composition, thirds grid).
Before drawing, check your plan against the sketch: head about 45% of the hero's height, gun length and enemy heights relative to the hero exactly as in the reference, subjects on the same thirds as the layout. Fix anything that is off, then render the final colour image in the style block.
Save as D:\Projects\Zombie-War\Review\KeyArt\generated_v2\12_drop_pod.png
```

### 13 · Heal plinth

```
Concept 13: Heal plinth (Mechanic).
The low round plinth with a floating green crystal and dashed green rings on the ground; hero resting inside while enemies come from far away.
References: D:\Projects\Zombie-War\Review\KeyArt\concepts_v2\final\13_heal_plinth.jpg (colour, ground truth for outfit, colours and enemy design), 13_heal_plinth_sketch.jpg (proportions), 13_heal_plinth_layout.jpg (composition, thirds grid).
Before drawing, check your plan against the sketch: head about 45% of the hero's height, gun length and enemy heights relative to the hero exactly as in the reference, subjects on the same thirds as the layout. Fix anything that is off, then render the final colour image in the style block.
Save as D:\Projects\Zombie-War\Review\KeyArt\generated_v2\13_heal_plinth.png
```

### 14 · Cactus Boss fight

```
Concept 14: Cactus Boss fight (Boss moment).
Over the shoulder of the hero facing the Cactus Boss up close, small cacti around it. Low camera so the boss looks huge.
References: D:\Projects\Zombie-War\Review\KeyArt\concepts_v2\final\14_cactus_boss.jpg (colour, ground truth for outfit, colours and enemy design), 14_cactus_boss_sketch.jpg (proportions), 14_cactus_boss_layout.jpg (composition, thirds grid).
Before drawing, check your plan against the sketch: head about 45% of the hero's height, gun length and enemy heights relative to the hero exactly as in the reference, subjects on the same thirds as the layout. Fix anything that is off, then render the final colour image in the style block.
Save as D:\Projects\Zombie-War\Review\KeyArt\generated_v2\14_cactus_boss.png
```

### 15 · Mole Rat King

```
Concept 15: Mole Rat King (Boss moment).
The hero on the right aiming across the frame at the Mole Rat King and a crowd of mole rats on the left.
References: D:\Projects\Zombie-War\Review\KeyArt\concepts_v2\final\15_molerat_king.jpg (colour, ground truth for outfit, colours and enemy design), 15_molerat_king_sketch.jpg (proportions), 15_molerat_king_layout.jpg (composition, thirds grid).
Before drawing, check your plan against the sketch: head about 45% of the hero's height, gun length and enemy heights relative to the hero exactly as in the reference, subjects on the same thirds as the layout. Fix anything that is off, then render the final colour image in the style block.
Save as D:\Projects\Zombie-War\Review\KeyArt\generated_v2\15_molerat_king.png
```

### 16 · Skeleton host

```
Concept 16: Skeleton host (Enemy moment).
Portrait: hero in the foreground from behind with a rifle, a squad of goofy skeletons and a hooded skeleton mage in front, a skeleton giant behind.
References: D:\Projects\Zombie-War\Review\KeyArt\concepts_v2\final\16_skeleton_host.jpg (colour, ground truth for outfit, colours and enemy design), 16_skeleton_host_sketch.jpg (proportions), 16_skeleton_host_layout.jpg (composition, thirds grid).
Before drawing, check your plan against the sketch: head about 45% of the hero's height, gun length and enemy heights relative to the hero exactly as in the reference, subjects on the same thirds as the layout. Fix anything that is off, then render the final colour image in the style block.
Save as D:\Projects\Zombie-War\Review\KeyArt\generated_v2\16_skeleton_host.png
```

### 17 · Cats and dogs swarm

```
Concept 17: Cats and dogs swarm (Gameplay camera).
Tilted top-down: hero in the middle with a shotgun, cute cats and dogs (lightning cat, pups, fire dog) running in from all sides.
References: D:\Projects\Zombie-War\Review\KeyArt\concepts_v2\final\17_pets_swarm.jpg (colour, ground truth for outfit, colours and enemy design), 17_pets_swarm_sketch.jpg (proportions), 17_pets_swarm_layout.jpg (composition, thirds grid).
Before drawing, check your plan against the sketch: head about 45% of the hero's height, gun length and enemy heights relative to the hero exactly as in the reference, subjects on the same thirds as the layout. Fix anything that is off, then render the final colour image in the style block.
Save as D:\Projects\Zombie-War\Review\KeyArt\generated_v2\17_pets_swarm.png
```

### 18 · Meteor Storm

```
Concept 18: Meteor Storm (Skill moment).
Meteors falling in a line across the crowd, orange burn circles on the ground, the hero standing safe in front.
References: D:\Projects\Zombie-War\Review\KeyArt\concepts_v2\final\18_meteor_storm.jpg (colour, ground truth for outfit, colours and enemy design), 18_meteor_storm_sketch.jpg (proportions), 18_meteor_storm_layout.jpg (composition, thirds grid).
Before drawing, check your plan against the sketch: head about 45% of the hero's height, gun length and enemy heights relative to the hero exactly as in the reference, subjects on the same thirds as the layout. Fix anything that is off, then render the final colour image in the style block.
Save as D:\Projects\Zombie-War\Review\KeyArt\generated_v2\18_meteor_storm.png
```

### 19 · Absolute Zero

```
Concept 19: Absolute Zero (Skill moment).
Frost ring bursting out from the hero, enemies in a circle turned to pale ice statues, snow sparkles on the ground.
References: D:\Projects\Zombie-War\Review\KeyArt\concepts_v2\final\19_absolute_zero.jpg (colour, ground truth for outfit, colours and enemy design), 19_absolute_zero_sketch.jpg (proportions), 19_absolute_zero_layout.jpg (composition, thirds grid).
Before drawing, check your plan against the sketch: head about 45% of the hero's height, gun length and enemy heights relative to the hero exactly as in the reference, subjects on the same thirds as the layout. Fix anything that is off, then render the final colour image in the style block.
Save as D:\Projects\Zombie-War\Review\KeyArt\generated_v2\19_absolute_zero.png
```

### 20 · Store feature graphic

```
Concept 20: Store feature graphic (Key art · 1024x500).
Wide: hero in the centre with the rifle, two groups of enemies coming from left and right, empty sky across the top for the logo.
References: D:\Projects\Zombie-War\Review\KeyArt\concepts_v2\final\20_feature_wide.jpg (colour, ground truth for outfit, colours and enemy design), 20_feature_wide_sketch.jpg (proportions), 20_feature_wide_layout.jpg (composition, thirds grid).
Before drawing, check your plan against the sketch: head about 45% of the hero's height, gun length and enemy heights relative to the hero exactly as in the reference, subjects on the same thirds as the layout. Fix anything that is off, then render the final colour image in the style block.
Save as D:\Projects\Zombie-War\Review\KeyArt\generated_v2\20_feature_wide.png
```
