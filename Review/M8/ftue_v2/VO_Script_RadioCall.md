# HordeCall · FTUE radio call · voice-over script

> **Cho anh:** file này để đưa cho AI lồng tiếng (ví dụ ElevenLabs, PlayHT, Azure TTS).
> Copy nguyên phần **Voice brief** làm mô tả giọng, rồi đọc từng dòng trong cột **Line**.
> Chữ trên màn hình (phụ đề) nằm trong mockup canvas trang *FTUE v3 · radio call*; câu nói ngắn
> và tự nhiên hơn chữ trên màn, nhưng cùng ý. Hiệu ứng bộ đàm (lọc tần số, tiếng rè, tiếng click)
> sẽ làm trong game, nên **thu giọng sạch**. Tên nhân vật chưa chốt: trên màn đang để `HQ · CH 1`.

## Voice brief (paste this into the voice tool)

The speaker is the game's mascot: a cheeky, brave kid of about 12 to 14 wearing a blue shark hoodie,
calling the player over a field radio during a cartoon monster-survival game. Bright, warm and
energetic, a big-sibling coach who is never bossy. Clear international English with a neutral
accent, easy for non-native players in the Philippines, Indonesia and Vietnam. Medium-fast pace,
crisp consonants, smiles audible. Kid-friendly: no shouting, no sarcasm, no slang beyond "rookie".

## Recording spec

- Clean dry voice: no music, no reverb, no radio effect (the game adds it).
- WAV, 48 kHz, 16-bit, mono. Peak at most -1 dBTP, loudness about -18 LUFS, the same for every line.
- 150 ms of silence before and after each line. One file per line, named as in the **File** column.
- Keep each line under 4.5 s; the on-screen message stays up about 4 s.
- Deliver 2 takes per line (`_a`, `_b`) so we can pick.

## Lines (19)

| # | File | Plays when | On screen | Line | Delivery | Length |
|---|------|------------|-----------|------|----------|--------|
| 01 | `vo_ftue_home` | Home opens for the first time | YOUR FIRST RUN | Hey, rookie! HQ here. Hit play, let's see what you've got. | warm welcome, a grin in the voice | ~4.6 s |
| 02 | `vo_ftue_move` | First run starts | DRAG TO MOVE | Drag anywhere to move. Your gun fires on its own. Just stay alive! | quick, encouraging | ~5.4 s |
| 03 | `vo_ftue_xp` | First kill | XP | See that glow up top? That's XP. Fill it up and you level up! | pointing something out, light | ~5.8 s |
| 04 | `vo_ftue_card` | First level-up | PICK A CARD | Level up! Pick a card. No rush this time. I'd go with this one. | excited about the level-up, relaxed on "no rush" | ~5.8 s |
| 05 | `vo_ftue_relay` | First Signal Relay on screen | SIGNAL RELAY | Signal relay! Hold the ring for twelve seconds and you get a free card. | alert, helpful | ~5.8 s |
| 06 | `vo_ftue_cache` | First Supply Cache on screen | SUPPLY CACHE | A supply cache. Step in, spend some coins, grab a card. | casual, a bit sly about spending | ~4.6 s |
| 07 | `vo_ftue_beacon` | First Boss Beacon on screen | BOSS BEACON | Careful, that's a boss beacon. Step in only when you're ready. Big boss, big chest! | warning first, then hyped on "big chest" | ~6.2 s |
| 08 | `vo_ftue_drop` | First Supply Drop on screen | SUPPLY DROP | Supply drop! Stand next to the pod to crack it open. | excited, urgent | ~4.6 s |
| 09 | `vo_ftue_heal` | First Heal Zone on screen | HEAL ZONE | Hurt? Switch on the heal zone and stay inside to patch up. | caring, calm | ~5.0 s |
| 10 | `vo_ftue_magnet` | First Magnet picked up | MAGNET | Magnet! Every coin on the map is coming your way. | delighted | ~4.2 s |
| 11 | `vo_ftue_bomb` | First Bomb picked up | BOMB | Bomb! Boom, screen cleared. | short and punchy, a laugh on "boom" | ~1.9 s |
| 12 | `vo_ftue_freeze` | First Freeze Clock picked up | FREEZE CLOCK | Freeze clock! They're stuck for four seconds. Move! | snappy, urgent on "Move!" | ~3.5 s |
| 13 | `vo_ftue_chest` | First chest opens | YOUR FIRST CHEST | Your first chest! Max out a power and grab its partner card. The next chest evolves it. | impressed, explaining | ~6.9 s |
| 14 | `vo_ftue_revive` | First death ever | FIRST ONE'S ON US | Whoa, you went down! First revive's on the house. Get back up! | surprised, then reassuring | ~5.0 s |
| 15 | `vo_ftue_result` | First result screen | NEWCOMER GIFT | Not bad for a first run! I topped you up to four hundred coins. Go grab a real gun. | proud of the player | ~7.7 s |
| 16 | `vo_ftue_gun` | First Arsenal visit after run 1 | YOUR FIRST NEW GUN | Here it is, the Makarov. Hits harder, shoots faster. Tap buy! | showing off a prize | ~4.6 s |
| 17 | `vo_ftue_lv2` | Account level 2 popup | MISSIONS + PASS | Account level two! Missions are open. Clear them for Pass rewards. | celebrating | ~4.6 s |
| 18 | `vo_ftue_lv3` | Account level 3 popup | GACHA | Level three! The gacha is open, and your first pull is free. | celebrating, teasing the free pull | ~5.0 s |
| 19 | `vo_ftue_lv5` | Account level 5 popup | GUN STARS | Level five! Now you can star up your guns. More stars, more damage. | celebrating, confident | ~5.4 s |

## In the game (for the build, not for the voice tool)

- Each file becomes the audio cue `vo.ftue.<id>` (for example `vo.ftue.move`) in the Addressable audio
  catalog and plays through `Bill.Audio` on the Voice channel, with a short radio click in and out.
- Every line plays once per player, together with its FTUE step; players who mute voice still get the
  on-screen text.
- The portrait is a live RenderTexture of the hero (the outfit the player wears) with the hologram
  shader; the hand is LayerLab `tutorial_hand_3` with the same shader.
