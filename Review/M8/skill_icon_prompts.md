# HordeCall: prompt vẽ icon skill bằng ChatGPT (35 icon)

Mỗi thẻ skill trong `SkillCatalogDefs` có một icon. Hiện game đang hiện chữ viết tắt thay icon ("CL", "MS"…), xem `icon_style/levelup_context.png`.
Tên file đúng bằng id thẻ, thả vào game là tự nhận.

Prompt viết bằng tiếng Anh để ChatGPT vẽ ổn định. Phần hướng dẫn viết tiếng Việt.
Cột "Skill làm gì" lấy từ mô tả trong game (`SkillDescriptions.cs`), để hình vẽ đúng cơ chế.

---

## 1. Kích thước và cách game hiển thị

| Mục | Giá trị |
|---|---|
| Kích thước xin ChatGPT vẽ | **1024 × 1024**, vuông, **nền trong suốt** |
| Kích thước trong game | Tự thu còn tối đa 256 px khi import |
| Kích thước thật trên màn hình | **46–64 px**: ô bên trái thẻ level-up, thanh skill trên HUD, bảng build cuối trận |
| Nền phía sau icon | Ô vuông bo góc **tối** (#2F3544), viền thẻ theo màu tầng skill |

Quy tắc đọc ở 48 px:
- Một vật thể duy nhất, to, chiếm ~80% khung.
- Viền tối dày.
- Màu phẳng, 1 dải bóng và 1 điểm sáng.
- Không chữ, không số.

Nền trong suốt là bắt buộc, vì icon đặt lên ô tối.

---

## 2. Cách làm (đọc một lần)

1. **Mở MỘT cuộc chat ChatGPT** và làm cả 35 icon trong đó để style đồng đều.
2. **Tin nhắn đầu tiên:**
   - Đính kèm 3 ảnh mẫu trong `icon_style/` (`sample_auto.drone.png`, `sample_stat.maxhealth.png`, `sample_evo.carpetbomb.png`) và `icon_style/levelup_context.png`.
   - Dán **STYLE BLOCK** ở mục 3, rồi dán prompt của icon đầu tiên (`auto.drone`).
   - Vẽ lại đến khi ưng. Icon đó thành **icon chuẩn** (anchor).
3. **Các icon sau:** dán nguyên khối prompt của icon đó. Mỗi khối đã mở đầu bằng "Same style as the approved icon".
   - Nếu style bắt đầu lệch, cứ ~8 icon đính kèm lại icon chuẩn một lần.
4. **Kiểm tra nền trong suốt:** mở PNG phải thấy nền caro. Nếu ra nền trắng hay ô màu, trả lời:
   *"Regenerate with a fully transparent background (alpha 0). No backdrop, no circle, no square tile, no frame, no floor shadow."*
5. **Lưu file** tên `<id>.png`, ví dụ `auto.orbit.png`.

---

## 3. STYLE BLOCK (dán ở tin nhắn đầu)

```
You are drawing skill icons for HordeCall, a cute cartoon zombie-survival mobile shooter
(auto-aim, level-up cards). I will ask for 35 icons one by one; keep them one consistent set.

Style rules for EVERY icon:
- Minimal toon icon: ONE bold central object, chunky rounded shapes, slight 3/4 view.
- Thick dark outline #1F2330, about 6% of the icon width, on the whole silhouette.
- Flat colours: one base colour, ONE soft cel-shade band, ONE small white highlight.
  No gradients, no texture, no noise, no realistic rendering.
- The object fills about 80% of the square canvas, centred, nothing touching the edges.
- Must read clearly at 48 x 48 pixels on a dark tile (#2F3544): high contrast, few details.
- Fully transparent background (PNG with alpha). No backdrop, no circle, no square tile,
  no frame, no floor shadow.
- No text, no letters, no numbers, no logos.
- Friendly and cute, not gory: no blood.
- Square 1024 x 1024.
The attached samples show the rules only (outline, flat colour, one shade band, one highlight,
transparent); draw more polished and cuter than them, do not copy them.
The attached level-up screenshot shows where the icon sits (the small square on the left of each card).
```

**Màu chủ đạo theo tầng skill** (trùng màu viền thẻ trong game, đã ghi sẵn trong từng prompt):

| Tầng | Màu | Hex |
|---|---|---|
| Stat | xám xanh thép | #9EA8B8 |
| Signature (theo loại súng) | cam ấm | #FF9E3D |
| Universal | xanh ngọc | #42D1C2 |
| Autonomous power | tím | #A86CFF |
| Evolution | vàng kim + chùm sáng lấp lánh phía sau | #FFCC33 |

---

## 4. Prompt từng icon

Mỗi khối là một tin nhắn: copy nguyên khối, dán vào chat.

### 4.1 Stat (#9EA8B8)

**`stat.damage`**: Damage Up. Skill làm gì: toàn bộ sát thương +%.
```
Same style as the approved icon. Icon "stat.damage": a chunky bullet pointing up with a small
burst star at its tip and two short upward arrows beside it. Main colour steel grey-blue #9EA8B8,
the burst star pale yellow.
```

**`stat.firerate`**: Fire Rate Up. Skill làm gì: tốc độ bắn +%.
```
Same style as the approved icon. Icon "stat.firerate": three bullets flying side by side to the
right with short speed lines behind them. Main colour steel grey-blue #9EA8B8.
```

**`stat.movespeed`**: Move Speed Up. Skill làm gì: tốc độ chạy +%.
```
Same style as the approved icon. Icon "stat.movespeed": a chunky cartoon sneaker with a small
wing on the heel and speed lines behind it. Main colour steel grey-blue #9EA8B8, white sole.
```

**`stat.maxhealth`**: Max Health Up. Skill làm gì: máu tối đa +%.
```
Same style as the approved icon. Icon "stat.maxhealth": a plump heart with a white plus sign
on it. Main colour steel grey-blue #9EA8B8 with a soft pink shade band.
```

**`stat.coingain`**: Coin Gain Up. Skill làm gì: coin nhận từ quái +%.
```
Same style as the approved icon. Icon "stat.coingain": a stack of three round gold coins with a
horseshoe magnet hovering above, pulling. Main colour steel grey-blue #9EA8B8 for the magnet,
gold coins.
```

### 4.2 Signature: theo loại súng (#FF9E3D)

**`sidearm.rungun`**: Run & Gun (súng lục). Skill làm gì: vừa chạy vừa bắn nhanh hơn.
```
Same style as the approved icon. Icon "sidearm.rungun": a chunky pistol tilted forward with
motion lines behind it and a small running-dust puff under it. Main colour warm orange #FF9E3D.
```

**`sidearm.quickstep`**: Quickstep Round (súng lục). Skill làm gì: cứ đi đủ quãng đường, phát bắn kế tiếp gây x2.5.
```
Same style as the approved icon. Icon "sidearm.quickstep": a glowing pistol bullet with a trail
of three small footprints behind it. Main colour warm orange #FF9E3D.
```

**`smg.static`**: Static Build-up (SMG). Skill làm gì: cứ vài phát trúng, tia sét nhảy sang quái khác.
```
Same style as the approved icon. Icon "smg.static": a compact SMG crackling with a small blue
electric spark jumping off the muzzle. Main colour warm orange #FF9E3D, electric blue spark.
```

**`smg.bullethose`**: Bullet Hose (SMG). Skill làm gì: giữ bắn liên tục thì tốc độ bắn tăng dần.
```
Same style as the approved icon. Icon "smg.bullethose": a spray of many small bullets fanning out
from a round muzzle, getting denser. Main colour warm orange #FF9E3D.
```

**`ar.focusfire`**: Focus Fire (rifle). Skill làm gì: bắn trúng cùng một con liên tục thì sát thương tăng.
```
Same style as the approved icon. Icon "ar.focusfire": a crosshair locked onto a small target,
with three rings tightening inward. Main colour warm orange #FF9E3D.
```

**`ar.breach`**: Breach Round (rifle). Skill làm gì: vài phát một lần, đạn xuyên nhiều quái và làm chúng dễ bị đánh.
```
Same style as the approved icon. Icon "ar.breach": a long rifle bullet punching through a cracked
wooden plank, splinters flying. Main colour warm orange #FF9E3D, light brown plank.
```

**`shotgun.pointblank`**: Point Blank (shotgun). Skill làm gì: bắn càng gần sát thương càng cao.
```
Same style as the approved icon. Icon "shotgun.pointblank": a shotgun muzzle seen close up with a
big round muzzle flash bursting out of it. Main colour warm orange #FF9E3D, yellow flash.
```

**`shotgun.concussion`**: Concussion (shotgun). Skill làm gì: bắn trúng làm quái chạy chậm lại.
```
Same style as the approved icon. Icon "shotgun.concussion": a chunky red shotgun shell with small
dizzy stars circling above it. Main colour warm orange #FF9E3D, pale yellow stars.
```

**`lmg.heavypressure`**: Heavy Pressure (LMG). Skill làm gì: bắn càng lâu sát thương càng tăng.
```
Same style as the approved icon. Icon "lmg.heavypressure": a heavy ammo belt coiled in a loop,
the bullets glowing hot at one end with small heat waves. Main colour warm orange #FF9E3D.
```

**`lmg.shockwave`**: Shockwave Belt (LMG). Skill làm gì: vài phát một lần, bắn ra một làn sóng xung kích hình nón.
```
Same style as the approved icon. Icon "lmg.shockwave": a cone-shaped shockwave of three curved
arcs blasting outward to the right from a small point. Main colour warm orange #FF9E3D.
```

**`marksman.longshot`**: Longshot (sniper). Skill làm gì: bắn càng xa sát thương càng cao.
```
Same style as the approved icon. Icon "marksman.longshot": a round sniper scope lens with a tiny
distant target and a thin crosshair inside it. Main colour warm orange #FF9E3D, pale blue lens.
```

**`marksman.hunters`**: Hunter's Mark (sniper). Skill làm gì: phát đầu tiên vào mỗi mục tiêu mới gây thêm sát thương.
```
Same style as the approved icon. Icon "marksman.hunters": a target reticle with a small cute
cartoon skull mark in its centre. Main colour warm orange #FF9E3D, white skull.
```

### 4.3 Universal (#42D1C2)

**`uni.execution`**: Execution Round. Skill làm gì: thêm sát thương lên quái sắp chết (máu thấp).
```
Same style as the approved icon. Icon "uni.execution": a bullet diving down onto a short, almost
empty, cracked health bar. Main colour teal #42D1C2, the bar red.
```

**`uni.kinetic`**: Kinetic Shield. Skill làm gì: cứ đi đủ quãng đường, được một khiên đỡ một đòn.
```
Same style as the approved icon. Icon "uni.kinetic": a round bubble shield with a small footprint
inside it and a glossy highlight. Main colour teal #42D1C2.
```

### 4.4 Autonomous power: tự kích hoạt (#A86CFF)

**`auto.chainlightning`**: Chain Lightning. Skill làm gì: định kỳ, tia sét nối qua nhiều quái.
```
Same style as the approved icon. Icon "auto.chainlightning": a zig-zag lightning bolt linking
three small round dots in a chain. Main colour violet #A86CFF, bright white-yellow bolt core.
```

**`auto.ordnance`**: Ordnance Core. Skill làm gì: định kỳ, một quả pháo rơi vào chỗ đông quái nhất.
```
Same style as the approved icon. Icon "auto.ordnance": a round artillery shell falling onto a
target circle on the ground. Main colour violet #A86CFF, red target circle.
```

**`auto.soulburst`**: Soul Burst. Skill làm gì: cứ 12 lần giết, một vụ nổ tỏa quanh người chơi.
```
Same style as the approved icon. Icon "auto.soulburst": a cute little ghost bursting out of a
ring of light, arms up. Main colour violet #A86CFF, white ghost.
```

**`auto.emergency`**: Emergency Detonation. Skill làm gì: máu dưới 30% thì nổ đẩy quái ra xa.
```
Same style as the approved icon. Icon "auto.emergency": a cracked heart at the centre of a round
blast ring pushing outward. Main colour violet #A86CFF, the heart red.
```

**`auto.orbit`**: Orbit Blades. Skill làm gì: lưỡi cưa quay vòng quanh người chơi.
```
Same style as the approved icon. Icon "auto.orbit": three spinning saw blades arranged in a
circle around an empty centre, with a curved motion arc. Main colour violet #A86CFF, silver blades.
```

**`auto.drone`**: Drone Buddy. Skill làm gì: một drone bắn con quái gần nhất.
```
Icon "auto.drone" (this is the first icon: it becomes the style anchor): a cute round little
drone with one big eye and two tiny propellers on top. Main colour violet #A86CFF.
```

**`auto.frostnova`**: Frost Nova. Skill làm gì: định kỳ, vòng băng làm chậm quái xung quanh.
```
Same style as the approved icon. Icon "auto.frostnova": a big chunky snowflake inside an
expanding icy ring. Main colour violet #A86CFF, ice blue snowflake.
```

**`auto.firetrail`**: Fire Trail. Skill làm gì: đi đến đâu để lại lửa đốt quái đến đó.
```
Same style as the approved icon. Icon "auto.firetrail": three small flames in a row shaped like
footprints. Main colour violet #A86CFF outline accents, orange-yellow flames.
```

**`auto.boomerang`**: Boomerang. Skill làm gì: boomerang bay ra rồi quay về, cắt qua mọi quái.
```
Same style as the approved icon. Icon "auto.boomerang": a chunky curved boomerang with a swoosh
arc showing it flies out and returns. Main colour violet #A86CFF.
```

**`auto.airstrike`**: Airstrike. Skill làm gì: định kỳ, bom rơi xuống quái trên màn hình.
```
Same style as the approved icon. Icon "auto.airstrike": a missile pointing down at a red target
marker on the ground. Main colour violet #A86CFF, red marker.
```

### 4.5 Evolution: tiến hoá (#FFCC33, chùm sáng lấp lánh phía sau)

**`evo.thunderstorm`**: Thunderstorm (tiến hoá từ Chain Lightning). Skill làm gì: sét 2 giây một lần qua 6 quái, mạnh hơn.
```
Same style as the approved icon. Icon "evo.thunderstorm": a chubby storm cloud shooting a big
lightning bolt downward, with a small sparkle burst behind the cloud. Main colour gold #FFCC33,
dark blue-grey cloud.
```

**`evo.carpetbomb`**: Carpet Bomb (tiến hoá từ Ordnance). Skill làm gì: pháo rơi thành một hàng 3 quả.
```
Same style as the approved icon. Icon "evo.carpetbomb": three shells falling in a diagonal row
onto the ground, a small sparkle burst behind. Main colour gold #FFCC33.
```

**`evo.buzzsaw`**: Buzzsaw Halo (tiến hoá từ Orbit Blades). Skill làm gì: 6 lưỡi cưa to hơn, quay nhanh hơn.
```
Same style as the approved icon. Icon "evo.buzzsaw": one big glowing buzzsaw ring with many
teeth, spinning, a small sparkle burst behind. Main colour gold #FFCC33.
```

**`evo.absolutezero`**: Absolute Zero (tiến hoá từ Frost Nova). Skill làm gì: đóng băng quái, quái bị đóng băng nhận thêm 50% sát thương.
```
Same style as the approved icon. Icon "evo.absolutezero": a cute cartoon zombie head frozen
inside a clear ice cube, a small sparkle burst behind. Main colour gold #FFCC33 sparkles, ice
blue cube, green zombie.
```

**`evo.squadron`**: Drone Squadron (tiến hoá từ Drone Buddy). Skill làm gì: 3 drone, giết quái có thể rơi coin.
```
Same style as the approved icon. Icon "evo.squadron": three small round drones flying in a V
formation, a small sparkle burst behind. Main colour gold #FFCC33.
```

**`evo.reaper`**: Reaper (tiến hoá từ Soul Burst). Skill làm gì: giết quái có thể giải phóng vụ nổ linh hồn.
```
Same style as the approved icon. Icon "evo.reaper": a small cute grim-reaper scythe with a ghost
wisp curling around the blade, a small sparkle burst behind. Main colour gold #FFCC33.
```

---

## 5. Câu sửa nhanh

- Rối ở cỡ nhỏ → `Bolder and simpler: fewer details, thicker outline, bigger shapes, readable at 48 px.`
- Lệch style so với icon chuẩn → `Match the approved icon exactly: same outline width, same flat colours, one shade band, one highlight.`
- Có nền → `Fully transparent background, alpha 0, no backdrop or tile.`
- Có chữ hoặc số → `Remove all text, letters and numbers.`
- Quá nhỏ trong khung → `Make the object fill 80% of the canvas, centred.`

---

## 6. Đưa icon vào game

1. Chép 35 file PNG vào **`Assets/_Project/UI/Icons/Skills/`**, tên đúng id (ví dụ `auto.orbit.png`).
2. Trong Unity bấm menu **`ZombieWar/UI/Authoring/Refresh Skill Icons`**.
   - Lệnh tự đặt import: Sprite, nền trong suốt, tối đa 256 px.
   - Rồi ghép icon theo id thẻ.
   - Thẻ nào chưa có file vẫn hiện chữ viết tắt như bây giờ.
3. Kiểm tra: vào trận, dùng tab **SKILLS** trong bảng cheat QA để lấy thẻ, rồi xem thẻ level-up, thanh skill HUD và bảng build cuối trận.
