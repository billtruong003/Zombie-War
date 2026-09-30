# HordeCall: prompt vẽ icon skill, đợt 2 (43 icon)

Đợt 1 (`skill_icon_prompts.md`) đã xong 35 icon; chúng đang nằm trong `Assets/_Project/UI/Icons/Skills/`.
Đợt này là **43 thẻ mới từ phase A (A3–A7)**, hiện game vẫn hiện chữ viết tắt cho chúng.
Tên file đúng bằng id thẻ, thả vào game là tự nhận. Cột "Skill làm gì" lấy từ mô tả trong game (`SkillDescriptions.cs`, rank cao nhất).

Kích thước, quy tắc đọc ở 48 px, STYLE BLOCK và bảng màu theo tầng **giữ nguyên như đợt 1** (mục 1 và 3 của `skill_icon_prompts.md`). Prompt tiếng Anh, hướng dẫn tiếng Việt.

| Nhóm | Số icon | Màu chủ đạo |
|---|---|---|
| Stat (chỉ số) | 5 | xám xanh thép #9EA8B8 |
| Signature Launcher | 2 | cam ấm #FF9E3D |
| Universal (đổi đạn, đa năng) | 10 | xanh ngọc #42D1C2 |
| Autonomous power | 13 | tím #A86CFF |
| Evolution | 13 | vàng kim #FFCC33 + chùm sáng lấp lánh phía sau |
| **Tổng** | **43** | |

---

## 1. Cách làm

1. **Mở MỘT cuộc chat ChatGPT mới** cho cả 43 icon.
2. **Tin nhắn đầu tiên:**
   - Đính kèm 5 icon đã duyệt của đợt 1 làm mẫu (lấy trong `Assets/_Project/UI/Icons/Skills/`):
     `auto.drone.png`, `stat.maxhealth.png`, `evo.carpetbomb.png`, `uni.kinetic.png`, `sidearm.rungun.png`.
     Kèm `Review/M8/icon_style/levelup_context.png` (chỗ icon nằm trên thẻ).
   - Dán **STYLE BLOCK** của đợt 1, thêm câu: `The attached icons are the approved set: match their outline, colours and finish exactly.`
   - Dán prompt icon đầu tiên (`auto.toxic`), vẽ lại đến khi ưng. Icon đó thành **icon neo** của đợt 2.
3. **Các icon sau:** dán nguyên khối prompt. Cứ ~8 icon đính kèm lại icon neo và 1–2 icon đợt 1 để style không trôi.
4. **Nền trong suốt:** mở PNG phải thấy nền caro; nếu không, dùng câu sửa ở mục 3.
5. **Lưu file** `<id>.png`, ví dụ `auto.toxic.png`.

Mẹo phân biệt với icon đã có: mấy cặp dễ trùng được ghi chú ngay dưới prompt (ví dụ `uni.crit` phải khác `marksman.hunters`).

---

## 2. Prompt từng icon

Mỗi khối là một tin nhắn: copy nguyên khối, dán vào chat.

### 2.1 Stat (#9EA8B8)

**`stat.cooldown`**: Cooldown. Skill làm gì: power hồi chiêu nhanh hơn 35%.
```
Same style as the approved icons. Icon "stat.cooldown": a chunky round stopwatch with a curved
arrow looping around it clockwise, as if spinning faster. Main colour steel grey-blue #9EA8B8,
the arrow pale cyan.
```

**`stat.area`**: Area. Skill làm gì: vùng tác dụng của power to hơn 40%.
```
Same style as the approved icons. Icon "stat.area": a small round target dot with three
concentric rings spreading outward and four short arrows pointing out from it. Main colour
steel grey-blue #9EA8B8.
```

**`stat.pickup`**: Pickup Range. Skill làm gì: vật phẩm bay về từ xa hơn 125%.
```
Same style as the approved icons. Icon "stat.pickup": a big cartoon hand, palm open, with two
small gold coins and a green gem flying toward it along curved lines. Main colour steel grey-blue
#9EA8B8 for the hand.
```
*Khác `stat.coingain` (nam châm + chồng coin): ở đây là bàn tay đón đồ bay tới.*

**`stat.regen`**: Regeneration. Skill làm gì: hồi 2% máu tối đa mỗi giây.
```
Same style as the approved icons. Icon "stat.regen": a plump heart with a small green leaf
sprouting from its top and two tiny rising plus signs beside it. Main colour steel grey-blue
#9EA8B8, soft green leaf and plus signs.
```
*Khác `stat.maxhealth` (tim có dấu cộng trắng ở giữa): ở đây là lá non và dấu cộng bay lên.*

**`stat.luck`**: Luck. Skill làm gì: vật phẩm và rương rơi nhiều hơn 50%.
```
Same style as the approved icons. Icon "stat.luck": a four-leaf clover with a tiny sparkle on
one leaf. Leaves steel grey-blue #9EA8B8 with a soft green shade band.
```

### 2.2 Signature, súng phóng lựu (#FF9E3D)

**`rocket.cluster`**: Cluster Charge. Skill làm gì: mỗi vụ nổ ném ra 4 bom con (45% sát thương).
```
Same style as the approved icons. Icon "rocket.cluster": one round grenade at the centre breaking
open with four small bomblets flying out in a cross. Main colour warm orange #FF9E3D, dark grey
bomblets.
```

**`rocket.napalm`**: Napalm Shell. Skill làm gì: vụ nổ để lại lửa 4 giây.
```
Same style as the approved icons. Icon "rocket.napalm": a chunky grenade shell lying on its side
with a puddle of cartoon flames spreading under it. Main colour warm orange #FF9E3D, red-orange
flames.
```

### 2.3 Universal (#42D1C2)

**`uni.pierce`**: Piercing Rounds. Skill làm gì: đạn xuyên thêm 3 quái.
```
Same style as the approved icons. Icon "uni.pierce": a sharp bullet flying right and passing
straight through three thin round targets lined up in a row. Main colour teal #42D1C2.
```
*Khác `ar.breach` (đạn phá tấm gỗ): ở đây là 3 bia tròn xếp hàng.*

**`uni.ricochet`**: Ricochet. Skill làm gì: đạn nảy sang thêm 3 quái (70% sát thương).
```
Same style as the approved icons. Icon "uni.ricochet": a bullet bouncing in a zigzag path, with
small spark bursts at each of its two bounce points. Main colour teal #42D1C2, yellow sparks.
```

**`uni.split`**: Split Shot. Skill làm gì: cứ 3 phát, 4 viên đạn phụ toả ra.
```
Same style as the approved icons. Icon "uni.split": one bullet on the left splitting into a fan
of four smaller bullets spreading to the right. Main colour teal #42D1C2.
```
*Khác `smg.bullethose` (chùm đạn cam dày): ở đây một viên tách thành 4.*

**`uni.crit`**: Critical Rounds. Skill làm gì: 20% cơ hội gây x2 sát thương.
```
Same style as the approved icons. Icon "uni.crit": a bullet striking a big jagged yellow impact
star, with a second smaller star popping behind it, like a double hit. Main colour teal #42D1C2
bullet, bright yellow stars.
```
*Khác `marksman.hunters` (đầu lâu trong tâm ngắm): không có đầu lâu, không có tâm ngắm.*

**`uni.siphon`**: Blood Siphon. Skill làm gì: cứ 12 kill hồi 3% máu.
```
Same style as the approved icons. Icon "uni.siphon": a small heart being filled through a curved
glass tube from a floating red soul wisp. Main colour teal #42D1C2 tube, red heart and wisp.
No blood drops.
```

**`uni.acid`**: Acid Rounds. Skill làm gì: đạn gây độc cộng dồn (tối đa 5 lớp).
```
Same style as the approved icons. Icon "uni.acid": a bullet dripping bright green acid, with two
small bubbling drops falling from it. Main colour teal #42D1C2 bullet, toxic green acid.
```

**`uni.explosive`**: Explosive Rounds. Skill làm gì: 40% phát trúng nổ lan sang quái gần.
```
Same style as the approved icons. Icon "uni.explosive": a bullet with a lit fuse on its back and a
small round explosion puff at its tip. Main colour teal #42D1C2 bullet, orange-yellow explosion.
```

**`uni.doubletap`**: Double Tap. Skill làm gì: 30% cơ hội bắn thêm 1 viên miễn phí.
```
Same style as the approved icons. Icon "uni.doubletap": two identical bullets flying side by side,
the second one slightly behind and outlined with a faint glow. Main colour teal #42D1C2.
```

**`uni.guardian`**: Guardian Angel. Skill làm gì: một lần mỗi trận, đòn chí mạng hồi 70% máu thay vì chết.
```
Same style as the approved icons. Icon "uni.guardian": a small heart with a pair of white angel
wings and a golden halo floating above it. Main colour teal #42D1C2 heart, white wings, gold halo.
```

**`uni.greed`**: Greed. Skill làm gì: coin +60%, nhưng quái mới có thêm 30% máu.
```
Same style as the approved icons. Icon "uni.greed": an overflowing sack of gold coins with a small
red horned devil tail curling out from behind it. Main colour teal #42D1C2 sack, gold coins.
```

### 2.4 Autonomous power (#A86CFF)

**`auto.toxic`**: Toxic Cloud. Skill làm gì: mỗi 5 giây, một đám mây độc rơi vào đám quái đông nhất.
```
Same style as the approved icons. Icon "auto.toxic": a gas canister falling nose-down into a puffy
green poison cloud with a small skull-shaped wisp in the cloud. Main colour purple #A86CFF
canister, toxic green cloud.
```

**`auto.gravity`**: Gravity Well. Skill làm gì: mỗi 7 giây, một xoáy hút quái trong 5 m lại rồi nổ.
```
Same style as the approved icons. Icon "auto.gravity": a swirling spiral vortex with a dark centre
and three small rocks being pulled into it along curved lines. Main colour purple #A86CFF.
```

**`auto.thorns`**: Thorn Aura. Skill làm gì: quái chạm vào bạn bị đâm và bật lùi.
```
Same style as the approved icons. Icon "auto.thorns": a round ring of sharp pink-purple thorns
spikes pointing outward, like a spiky crown lying flat. Main colour purple #A86CFF, pink tips.
```

**`auto.turret`**: Sentry Turret. Skill làm gì: thả tháp súng tự bắn trong 6 giây.
```
Same style as the approved icons. Icon "auto.turret": a small squat automatic gun turret on a
tripod, barrel pointing right with a tiny muzzle flash. Main colour purple #A86CFF, grey barrel.
```

**`auto.meteor`**: Meteor. Skill làm gì: thiên thạch rơi nghiền đám quái và để lại lửa.
```
Same style as the approved icons. Icon "auto.meteor": a round rocky meteor diving down to the
left with a fiery orange tail behind it. Main colour purple #A86CFF rock, orange fire tail.
```

**`auto.stormcloud`**: Storm Cloud. Skill làm gì: đám mây giông theo bạn và đánh sét vào quái.
```
Same style as the approved icons. Icon "auto.stormcloud": a small dark purple rain cloud with one
bright zigzag lightning bolt striking down from it. Main colour purple #A86CFF cloud, yellow bolt.
```
*Khác `evo.thunderstorm` (mây đen to + tia sét vàng, có chùm sáng): ở đây mây nhỏ, tím, không chùm sáng.*

**`auto.iceshards`**: Ice Shards. Skill làm gì: mỗi 3 giây, 10 mảnh băng bắn toả ra, xuyên và làm chậm.
```
Same style as the approved icons. Icon "auto.iceshards": five sharp ice crystal shards bursting
outward in a star pattern from a small centre. Main colour purple #A86CFF outline glow, pale ice
blue shards.
```
*Khác `auto.frostnova` (bông tuyết): ở đây là mảnh băng nhọn bắn ra.*

**`auto.flameburst`**: Flame Burst. Skill làm gì: mỗi 4 giây, một luồng lửa hình nón đốt đám quái gần nhất.
```
Same style as the approved icons. Icon "auto.flameburst": a short nozzle shooting a wide cone of
cartoon fire to the right. Main colour purple #A86CFF nozzle, orange-yellow flames.
```

**`auto.landmine`**: Landmines. Skill làm gì: đi đủ quãng thì thả mìn, quái dẫm lên là nổ.
```
Same style as the approved icons. Icon "auto.landmine": a flat round landmine with a red blinking
light on top and four little prongs. Main colour purple #A86CFF, red light.
```

**`auto.axe`**: Spinning Axe. Skill làm gì: ném 3 rìu xoay xuyên qua đám quái.
```
Same style as the approved icons. Icon "auto.axe": a chunky double-bitted axe spinning, with
curved motion arcs around it. Main colour purple #A86CFF handle, steel blade.
```

**`auto.wardog`**: War Dog. Skill làm gì: hai chó chiến cắn quái gần nhất.
```
Same style as the approved icons. Icon "auto.wardog": a cute determined cartoon dog head with a
spiked collar and a small tactical ear piece. Main colour purple #A86CFF collar, steel-blue fur.
```

**`auto.stomp`**: Ground Stomp. Skill làm gì: mỗi 4 giây, dậm đất hất lùi mọi quái trong 5 m.
```
Same style as the approved icons. Icon "auto.stomp": a big boot slamming down on cracked ground,
with a round dust shockwave ring spreading out. Main colour purple #A86CFF boot, tan dust.
```

**`auto.timewarp`**: Time Warp. Skill làm gì: mỗi 16 giây, mọi quái đi chậm một nửa trong 4 giây.
```
Same style as the approved icons. Icon "auto.timewarp": an hourglass tilted slightly with its sand
swirling in a spiral, and a small clock hand ring around it. Main colour purple #A86CFF, pale
blue sand.
```
*Khác `stat.cooldown` (đồng hồ bấm giờ + mũi tên): ở đây là đồng hồ cát.*

### 2.5 Evolution (#FFCC33, thêm chùm sáng lấp lánh nhỏ phía sau mỗi icon)

**`evo.plague`**: Plague (Toxic Cloud + Area). Skill làm gì: mây độc bám theo đám quái; quái chết vì độc lây độc sang con khác.
```
Same style as the approved icons. Icon "evo.plague": a big bubbling green poison cloud with a
cute plague-doctor beak mask peeking out of it, a small sparkle burst behind. Main colour gold
#FFCC33 sparkles and mask trim, toxic green cloud.
```

**`evo.singularity`**: Singularity (Gravity Well + Cooldown). Skill làm gì: một xoáy không bao giờ đóng trôi qua đám quái.
```
Same style as the approved icons. Icon "evo.singularity": a glowing black hole with a bright
golden accretion ring tilted around it, a small sparkle burst behind. Main colour gold #FFCC33,
deep purple core.
```

**`evo.fortress`**: Fortress (Sentry Turret + Max Health Up). Skill làm gì: hai tháp súng bắn tên lửa.
```
Same style as the approved icons. Icon "evo.fortress": a sturdy armoured turret with twin rocket
pods, one small rocket launching with a smoke puff, a small sparkle burst behind. Main colour
gold #FFCC33.
```

**`evo.meteorstorm`**: Meteor Storm (Meteor + Damage Up). Skill làm gì: năm thiên thạch rơi thành một hàng.
```
Same style as the approved icons. Icon "evo.meteorstorm": three fiery meteors of different sizes
diving down in a diagonal line, a small sparkle burst behind. Main colour gold #FFCC33 rocks,
orange fire tails.
```

**`evo.ironmaiden`**: Iron Maiden (Thorn Aura + Kinetic Shield). Skill làm gì: gai đâm mạnh gấp đôi; khiên vỡ thì bắn ra sóng gai.
```
Same style as the approved icons. Icon "evo.ironmaiden": a round shield covered in sharp metal
spikes pointing outward, a small sparkle burst behind. Main colour gold #FFCC33 shield, steel
spikes with pink tips.
```

**`evo.supercell`**: Supercell (Storm Cloud + Critical Rounds). Skill làm gì: đám mây giông lớn hơn, mọi tia sét đều chí mạng và nảy sang 3 quái.
```
Same style as the approved icons. Icon "evo.supercell": a tall towering storm cloud with three
forked lightning bolts shooting out of it, a small sparkle burst behind. Main colour gold #FFCC33
bolts, dark grey-purple cloud.
```

**`evo.blizzard`**: Blizzard (Ice Shards + Area). Skill làm gì: mảnh băng xoay quanh bạn liên tục và đóng băng quái chạm phải.
```
Same style as the approved icons. Icon "evo.blizzard": a whirling ring of ice shards and snowflakes
circling around a small empty centre, a small sparkle burst behind. Main colour gold #FFCC33
sparkles, pale ice blue shards.
```

**`evo.dragonbreath`**: Dragon Breath (Flame Burst + Fire Rate Up). Skill làm gì: luồng lửa quét vòng quanh bạn không ngừng.
```
Same style as the approved icons. Icon "evo.dragonbreath": a cute cartoon dragon head in profile
breathing a curling stream of fire, a small sparkle burst behind. Main colour gold #FFCC33
dragon, orange-red flames.
```

**`evo.minefield`**: Minefield (Landmines + Move Speed Up). Skill làm gì: mìn chùm, mỗi vụ nổ kích nổ các mìn xung quanh.
```
Same style as the approved icons. Icon "evo.minefield": three landmines in a triangle, the front
one exploding in a round orange blast, a small sparkle burst behind. Main colour gold #FFCC33
mines, orange blast.
```

**`evo.axestorm`**: Axe Storm (Spinning Axe + Luck). Skill làm gì: bốn rìu xoay quanh bạn rồi bay ra xuyên đám quái.
```
Same style as the approved icons. Icon "evo.axestorm": four small axes arranged in a spinning
pinwheel around a centre, with a swirl of wind, a small sparkle burst behind. Main colour gold
#FFCC33.
```

**`evo.alphapack`**: Alpha Pack (War Dog + Regeneration). Skill làm gì: ba chó chiến, mỗi cú cắn hồi máu cho bạn.
```
Same style as the approved icons. Icon "evo.alphapack": a bold cartoon wolf-dog head with a small
crown and a tiny green plus sign by its ear, a small sparkle burst behind. Main colour gold
#FFCC33 crown and collar, steel-blue fur.
```

**`evo.earthquake`**: Earthquake (Ground Stomp + Max Health Up). Skill làm gì: cú dậm làm nứt đất và làm choáng mọi thứ trong vùng.
```
Same style as the approved icons. Icon "evo.earthquake": a chunk of ground splitting apart along
a glowing zigzag crack, with two small rocks jumping up, a small sparkle burst behind. Main
colour gold #FFCC33 crack glow, brown earth.
```

**`evo.timestop`**: Time Stop (Time Warp + Cooldown). Skill làm gì: mọi quái đóng băng hoàn toàn trong 2 giây.
```
Same style as the approved icons. Icon "evo.timestop": a pocket watch frozen inside a block of
clear ice, its hands stopped, a small sparkle burst behind. Main colour gold #FFCC33 watch, ice
blue block.
```
*Khác `evo.absolutezero` (đầu zombie trong khối băng): ở đây là đồng hồ quả quýt trong khối băng.*

---

## 3. Câu sửa nhanh

- Rối ở cỡ nhỏ → `Bolder and simpler: fewer details, thicker outline, bigger shapes, readable at 48 px.`
- Lệch style → `Match the approved icons exactly: same outline width, same flat colours, one shade band, one highlight.`
- Có nền → `Fully transparent background, alpha 0, no backdrop or tile.`
- Có chữ hoặc số → `Remove all text, letters and numbers.`
- Quá nhỏ trong khung → `Make the object fill 80% of the canvas, centred.`
- Trùng icon đã có → `Make it clearly different from the <id> icon: <mô tả khác biệt>.`

---

## 4. Đưa icon vào game

1. Chép 43 file PNG vào **`Assets/_Project/UI/Icons/Skills/`**, tên đúng id.
2. Trong Unity bấm **`ZombieWar/UI/Authoring/Refresh Skill Icons`** (tự đặt import Sprite, nền trong, tối đa 256 px, rồi ghép theo id).
3. Hoặc chỉ cần chép file vào đó và báo tôi: tôi chạy lệnh, kiểm tra từng icon trên thẻ level-up, thanh skill HUD và màn rương, chụp contact sheet để duyệt.

**Tuỳ chọn:** 4 thẻ BONUS (`over.heal`, `over.coin`, `over.magnet`, `over.might`) đang dùng icon tạm tách từ icon có sẵn (A1). Nếu muốn đồng bộ hẳn, vẽ thêm theo tầng Universal:
`over.heal` hộp cứu thương, `over.coin` túi coin, `over.magnet` nam châm hút, `over.might` nắm đấm có tia sáng.
