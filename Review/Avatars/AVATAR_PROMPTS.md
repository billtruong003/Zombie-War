# HordeCall: prompt vẽ avatar bằng ChatGPT

File này dùng để tạo 10 ảnh đại diện (avatar) trong khung chọn avatar ở màn Profile.
Làm lần lượt từng avatar: đính kèm đúng ảnh tham chiếu, dán khối **STYLE CHUNG** rồi dán prompt của avatar đó.

Prompt viết bằng tiếng Anh vì ChatGPT vẽ ổn định hơn. Phần hướng dẫn viết tiếng Việt.

---

## 1. Kích thước và cách game hiển thị

| Mục | Giá trị |
|---|---|
| Kích thước xin ChatGPT vẽ | **1024 × 1024**, vuông 1:1 |
| Kích thước đưa vào game | **512 × 512 PNG** (thu nhỏ từ bản 1024) |
| Kích thước thật trên màn hình | 120–220 px (thẻ chọn avatar ~140 px, Profile ~170–220 px, góc Home ~120 px) |
| Hình dạng | Game cắt ảnh thành **hình vuông bo góc**, rồi phủ **viền khung** (shader) lên mép |
| Nền | **Nền kín màu** (không trong suốt) để mọi avatar đồng bộ khi đứng cạnh nhau |

**Vùng an toàn:** xem `prompt_refs/safe_zone_1024.png`.
- **Dải đỏ** (khoảng 7–12% mỗi mép) và 4 góc bị viền khung che: chỉ để nền ở đó.
- **Khung xanh lá** (vào trong 12%): mọi chi tiết quan trọng phải nằm trong này.
- **Vòng vàng** (giữa 60%): mặt hoặc chủ thể chính đặt ở đây. Ở 120 px vẫn phải nhận ra.

Quy tắc đọc ở kích thước nhỏ:
- Một chủ thể, to, rõ, nằm chính giữa.
- Viền đen/tối dày quanh chủ thể.
- Tối đa 3–4 màu chính.
- Không có chữ và không có logo.

---

## 2. STYLE CHUNG (dán đầu mỗi prompt)

```
Style bible for a mobile game called HordeCall (cute zombie-survival shooter for teens and adults):
- Chunky low-poly 3D toy look, like a vinyl figure: soft rounded shapes, big head, short body,
  flat-shaded faces with gentle gradients, thick dark outline around the silhouette.
- Bright, saturated, friendly colours; soft studio key light from top-left, warm rim light on the edge.
- Match the attached reference character EXACTLY: same outfit pieces, same colours, same hat/hood,
  same proportions. Do not invent new clothing.
- Square 1:1 image, 1024x1024. Full-bleed solid or softly gradient background (no transparency).
- The subject is centred and big: face/subject inside the middle 60% of the image, and nothing
  important within 12% of any edge or in the corners (a frame covers them).
- Must stay readable as a 120-pixel icon. No text, no letters, no numbers, no logo, no watermark,
  no border or frame (the game draws its own frame).
- Not scary, no blood, no gore. Guns are toy-like and chunky.
```

---

## 3. Mười avatar

Tên file phải khớp đúng id. Ảnh tham chiếu nằm trong `Review/Avatars/refs/` (nhân vật, 1024×1280, nền trong suốt) và `Review/Avatars/prompt_refs/` (súng, zombie, nhân vật cầm súng).

### 3.1 `avatar.chibi_1`: Chibi Gunner (miễn phí)
- **Đính kèm:** `refs/06_blue_shark_front.png`, `refs/06_blue_shark_3q.png`, `prompt_refs/gun_m4a1.png`
- **Nền:** xanh biển nhạt → xanh ngọc
```
Head-and-shoulders portrait of the attached character (the Blue Shark hood outfit), three-quarter
view facing slightly right, big shiny eyes, confident small smile. He holds the attached rifle
(chunky toy version) diagonally across the chest, barrel pointing up-right. Background: soft
gradient from light sky blue (#8fd8ff) to aqua (#3cc7c7) with a faint radial light behind the head.
```

### 3.2 `avatar.chibi_2`: Chibi Hero (level 8)
- **Đính kèm:** `refs/05_little_dino_front.png`, `refs/05_little_dino_3q.png`, `prompt_refs/gun_ak47.png`
- **Nền:** cam ấm → vàng
```
Head-and-shoulders hero portrait of the attached character (teal dino hood, turtle-ninja body),
three-quarter view facing slightly left, determined happy face, one fist raised. The attached
rifle (chunky toy version) rests on the shoulder. Background: warm gradient from orange (#ff9a3d)
to golden yellow (#ffd65a) with soft sun rays behind the head.
```

### 3.3 `avatar.badge_1`: Squad Badge (miễn phí)
- **Đính kèm:** `refs/09_red_mask_front.png`
- **Nền:** xanh navy
```
A round shield emblem (badge) filling the middle of the image. Inside the emblem: the face and
shoulders of the attached character (Red Mask Warrior), front view. The emblem has a thick silver
rim, two small gold stars at the top, and a red ribbon across the bottom with NO text on it.
Background: deep navy (#1f2a5a) with a subtle diagonal pattern.
```

### 3.4 `avatar.badge_2`: Elite Badge (level 12)
- **Đính kèm:** `refs/12_viking_front.png`
- **Nền:** tím đậm, ánh vàng
```
An elite military-style shield badge filling the middle of the image. Inside: the face of the
attached character (Viking Warrior), front view, fierce but friendly grin. Thick gold rim with
small wings on both sides, three gold stars above, laurel leaves below, no text anywhere.
Background: royal purple (#3b1f6b) with gold sparkles.
```

### 3.5 `avatar.mascot_1`: Zombie Pal (chơi 25 trận)
- **Đính kèm:** `prompt_refs/zombie_enemy.png`
- **Nền:** xanh lá nhạt
```
A cute, friendly cartoon zombie mascot based on the attached enemy (lime-green skin, purple
overalls, big round eyes), head and shoulders, waving hello, one crooked tooth showing in a happy
smile, a few simple stitches on the cheek. Toy-like and adorable, not scary, no blood.
Background: soft mint green (#b8f0c8) with a faint radial glow.
```

### 3.6 `avatar.mascot_2`: Zombie King (giết 5.000 zombie)
- **Đính kèm:** `prompt_refs/zombie_enemy.png`
- **Nền:** đỏ rượu, ánh vàng
```
The same cute cartoon zombie mascot (lime-green skin, purple overalls, big round eyes) as a proud
king: a big shiny gold crown slightly tilted, a small red royal cape, chin up with a smug happy
grin and one tooth. Toy-like, funny, not scary, no blood. Background: deep wine red (#5a1a2a)
with golden light rays behind the crown.
```

### 3.7 `avatar.weapon_1`: Crossed Guns (miễn phí)
- **Đính kèm:** `prompt_refs/gun_ak47.png`, `prompt_refs/gun_m4a1.png`
- **Nền:** xanh ngọc đậm với vệt nổ
```
Two chunky toy-like rifles (the attached AK-47 and M4A1) crossed in an X in the centre, over a
bright starburst explosion shape in orange and yellow. Thick dark outlines, glossy highlights.
No characters, no text. Background: dark teal (#12353f).
```

### 3.8 `avatar.weapon_2`: Golden Rifle (sở hữu 10 súng)
- **Đính kèm:** `prompt_refs/gun_hk416.png`, `prompt_refs/gun_awm.png`
- **Nền:** đen xanh, ánh vàng
```
One shiny GOLD version of the attached rifle, shown big and diagonal (bottom-left to top-right)
across the centre, polished gold with bright white sparkles and a warm glow, small floating gold
bullets around it. Chunky toy style, thick dark outline. No text.
Background: very dark blue (#0f1630) with a soft golden radial light behind the gun.
```

### 3.9 `avatar.sticker_1`: Thumbs Up (chơi 50 trận)
- **Đính kèm:** `refs/04_tiger_cub_front.png`, `refs/04_tiger_cub_3q.png`
- **Nền:** vàng chanh
```
Full-body sticker of the attached character (Playful Tiger Cub outfit), big head, giving a big
thumbs-up with one hand and winking. Sticker look: a thick white cut-out outline around the whole
character and a soft drop shadow. The character fills most of the square but stays inside the
safe margin. Background: lemon yellow (#ffe45c) with small white confetti dots.
```

### 3.10 `avatar.sticker_2`: Victory (sống 15:00)
- **Đính kèm:** `refs/01_santa_front.png`, `prompt_refs/lobby_character_holding_gun.png`
- **Nền:** đỏ tươi → cam
```
Full-body sticker of the attached character (Santa outfit), jumping with a victory sign (V with
two fingers), holding a chunky toy pistol in the other hand pointed up (see the second reference
for how the character holds a gun). Thick white sticker outline and soft drop shadow.
Background: bright red (#ff4d5a) to orange (#ff9a3d) gradient with star sparkles.
```

---

## 4. Nếu ảnh ra chưa ổn: câu sửa nhanh

Dán thêm vào cùng cuộc trò chuyện:
- Lệch đồ so với ảnh mẫu → `Keep the outfit exactly like the reference: same hat, same colours, same pieces.`
- Chủ thể nhỏ → `Make the subject 30% bigger and centred; keep 12% empty margin at every edge.`
- Có chữ → `Remove all text and letters.`
- Quá tả thực → `More toy-like: chunkier shapes, fewer small details, thicker dark outline.`
- Nhìn nhỏ bị rối → `Simplify for a 120-pixel icon: fewer details, stronger contrast between subject and background.`

---

## 5. Đưa ảnh vào game

1. Tải bản 1024×1024 về, đặt tên đúng id (ví dụ `avatar.chibi_1.png`).
2. Thu nhỏ còn **512×512**. Nếu dùng Photoshop/GIMP thì chọn bicubic sharper.
3. Chép vào **`Assets/Resources/UI/Avatars/`**.
   - Game tự nhận bằng `Resources.Load("UI/Avatars/<id>")`.
   - Chưa có file thì khung chọn avatar hiện "ART SOON".
4. Unity tự áp thiết lập import cho thư mục này (xem `AvatarImportSettings.cs`): Sprite, không mipmap, tối đa 512, nén chất lượng cao.
5. Kiểm tra:
   - Mở Profile → bấm avatar → tab AVATAR, xem ở cỡ ô ~140 px có nhận ra không.
   - Thử vài khung (Gold, Neon) xem viền có che mất chi tiết nào không.

| id | Tên trong game | Mở khoá |
|---|---|---|
| avatar.chibi_1 | Chibi Gunner | Miễn phí |
| avatar.chibi_2 | Chibi Hero | Level 8 |
| avatar.badge_1 | Squad Badge | Miễn phí |
| avatar.badge_2 | Elite Badge | Level 12 |
| avatar.mascot_1 | Zombie Pal | Chơi 25 trận |
| avatar.mascot_2 | Zombie King | Giết 5.000 zombie |
| avatar.weapon_1 | Crossed Guns | Miễn phí |
| avatar.weapon_2 | Golden Rifle | Sở hữu 10 súng |
| avatar.sticker_1 | Thumbs Up | Chơi 50 trận |
| avatar.sticker_2 | Victory | Sống 15:00 |
