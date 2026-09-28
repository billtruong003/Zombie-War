# HordeCall: brief concept art, logo và màn loading

Brief này để tạo bộ ảnh nhận diện của game bằng ChatGPT.

Cùng một bộ ảnh dùng cho nhiều chỗ:
- màn loading lúc mở game và lúc vào trận,
- logo,
- ảnh bìa trên Google Play,
- icon ứng dụng.

Prompt viết bằng tiếng Anh để ChatGPT vẽ ổn định. Phần hướng dẫn viết tiếng Việt.

---

## 1. Game là gì (để ChatGPT hiểu đúng tinh thần)

- **Tên:** HordeCall.
- **Thể loại:** bắn zombie sinh tồn (kiểu "survivor"), chơi dọc màn hình điện thoại.
  - Nhân vật chạy trong một bản đồ mở, súng tự ngắm tự bắn.
  - Zombie kéo đến thành từng đàn đông; lên cấp thì chọn thẻ kỹ năng.
- **Nhân vật:** kiểu chibi đồ chơi 3D low-poly (đầu to, người ngắn), mặc đồ rất đa dạng (mũ cá mập, mũ khủng long, viking…), cầm súng thật nhưng vẽ tròn trịa như đồ chơi.
- **Quái:** zombie xanh lá mặc yếm tím, skeleton, skeleton khổng lồ, skeleton pháp sư. Dễ thương hơn đáng sợ.
- **Tông màu:** sáng, bão hoà, vui mắt. Trời xanh, cỏ xanh, nắng ấm. UI trắng kem với nút vàng, xanh, tím.
- **Người chơi:** thiếu niên và người lớn (không nhắm trẻ em). Thị trường đầu tiên là Philippines, Indonesia, Việt Nam, sau đó các nước Tier-1.
- **Giọng điệu:** hài hước, hành động, "một mình chống cả đàn". **Không máu me**, không kinh dị.

---

## 2. Cần những gì (danh sách giao)

| # | Tên file | Kích thước xin vẽ | Dùng ở đâu | Nền |
|---|---|---|---|---|
| A | `loading_bg.png` | **1080 × 2400** (dọc) | Nền màn loading | Kín, KHÔNG có chữ, KHÔNG có logo |
| B | `logo_hordecall.png` | **2048 × 1024** | Logo trên màn loading, ảnh bìa, trailer | **Trong suốt** |
| C | `feature_graphic.png` | **1024 × 500** (ngang) | Ảnh bìa Google Play | Kín, có thể có logo |
| D | `app_icon.png` | **1024 × 1024** | Icon ứng dụng (Play Store thu còn 512) | Kín, không chữ |

**Tách logo khỏi nền loading.** Game tự đặt logo, dòng mẹo và thanh tiến trình lên trên nền. Nếu logo vẽ dính vào nền thì không đổi được lúc có sự kiện.

---

## 3. Bố cục màn loading (quan trọng)

Xem `refs/loading_layout_1080x2400.png`. Toạ độ tính trên ảnh 1080 × 2400:

| Vùng | Vị trí | Yêu cầu với nền |
|---|---|---|
| **Vùng an toàn 16:9** | y 240 → 2160 | Máy 16:9 chỉ thấy phần này. Mọi thứ quan trọng nằm trong đây. |
| **Phần thêm của máy 20:9** | y 0–240 và 2160–2400 | Chỉ kéo dài nền (trời, đất), không đặt chi tiết chính. |
| **Logo** | khung 620 × 360, tâm ở (540, 1080) | Nền phía sau **yên, tối hơn hoặc mờ**, không có chi tiết rối. |
| **Dòng mẹo** | khung 860 × 120, tâm ở (540, 1500) | Nền yên để chữ trắng đọc được. |
| **Thanh tiến trình** | 640 × 22, tâm ở (540, 1600) | Như trên. |
| **Vùng hành động** | y 240 → 760 (trên) và y 1680 → 2160 (dưới) | Nhân vật, đàn zombie, chớp nòng đặt ở đây. |

Cách bố trí gợi ý:
- **Phía trên:** nhân vật chính đứng nổi bật, bắn chéo lên.
- **Phía dưới:** đàn zombie tràn lên từ mép dưới.
- **Ở giữa:** một vùng sáng mờ (khói, bụi, ánh nắng) để logo và chữ đè lên đọc rõ.

---

## 4. STYLE CHUNG (dán ở tin nhắn đầu)

```
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
```

---

## 5. Prompt từng ảnh

Ảnh tham chiếu nằm trong `refs/`. Đính kèm đúng ảnh ghi ở từng mục.

### A. `loading_bg.png`: nền màn loading
- **Đính kèm:** `hero_holding_gun.png`, `06_blue_shark_3q.png`, `Zombie.png`, `Skeleton.png`, `SkeletonGiant.png`, `loading_layout_1080x2400.png`, `ui_home.png` (lấy màu)
```
Portrait mobile loading screen background, 1080x2400, following the attached layout guide.
Scene: a sunny grassy battlefield seen from a slightly high angle. In the TOP third, the hero
(the attached Blue Shark hood character, same outfit) stands heroically, firing a chunky rifle
diagonally down-right, bright muzzle flash and clean tracer lines. From the BOTTOM edge a big
goofy horde rushes upward: green zombies in purple overalls, skeletons, one giant skeleton in the
back, all cute, no blood. The MIDDLE band (y 900 to 1650) is calm: soft golden dust and light
haze, slightly darker, no characters or busy detail, so a logo and white text can sit on top.
Extend sky at the very top and ground at the very bottom (they may be cropped on shorter phones).
No text, no logo, no UI.
```

### B. `logo_hordecall.png`: logo chữ
- **Đính kèm:** `ui_home.png`, `ui_gacha.png` (để khớp font và màu UI)
```
Game logo wordmark "HordeCall" on a fully transparent background, 2048x1024, centred with margin.
Chunky rounded 3D bubble letters, thick dark-navy outline and a second white outer stroke, warm
yellow-to-orange letters with a glossy top highlight (matching the yellow PLAY button in the
attached UI). "Horde" slightly bigger than "Call". A small cute zombie hand pokes out from behind
the letter "H", and a tiny crosshair replaces the dot-like space near "Call". Playful and bold,
readable at 300 px wide. Transparent PNG, no background, no other text.
```

### C. `feature_graphic.png`: ảnh bìa Google Play
- **Đính kèm:** `hero_holding_gun.png`, `05_little_dino_3q.png`, `12_viking_front.png`, `Zombie.png`, `SkeletonMage.png`, logo B đã duyệt
```
Google Play feature graphic, landscape 1024x500. Left 40%: the approved HordeCall logo, large.
Right 60%: three attached heroes (Blue Shark, Little Dino, Viking) back to back, firing chunky
guns outward at a cute zombie and skeleton horde closing in from the right edge, bright muzzle
flashes, sunny grassy arena. Keep all important content inside the centre 924x400 (edges may be
cropped). No other text, no UI.
```

### D. `app_icon.png`: icon ứng dụng
- **Đính kèm:** `hero_holding_gun.png`, `06_blue_shark_3q.png`, `Zombie.png`
```
Mobile app icon, 1024x1024, full-bleed square (the store rounds the corners). The Blue Shark hood
hero's face, big and close, winking and grinning, holding a chunky rifle across the frame, with
two goofy green zombie hands reaching in from the bottom corners. Bright yellow-orange radial
background. Very bold shapes, readable at 48 px. No text, no letters, no border.
```

---

## 6. Câu sửa nhanh

- Vùng giữa rối → `Make the middle band (y 900–1650) calm and slightly darker: only soft haze and light, no characters.`
- Nhân vật sai đồ → `Keep the hero's outfit exactly like the reference: same hood, colours and pieces.`
- Quá đáng sợ → `Make the zombies goofier and cuter: big eyes, silly faces, no blood, no wounds.`
- Chữ logo sai chính tả → `Spell exactly "HordeCall", one word, capital H and C.`
- Ảnh ra ngang/sai kích thước → `Output exactly <kích thước> in portrait/landscape.`

---

## 7. Đưa vào game (t làm khi có ảnh)

1. Gửi t 4 file đã duyệt.
2. **Nền loading:** đặt vào `Assets/_Project/UI/Sprites/Loading/loading_bg.png`. Khung nền chuyển sang chế độ *phủ kín giữ tỉ lệ*, nên máy 16:9 cắt đều trên dưới, không méo.
3. **Logo:** thay khối chữ "HordeCall" hiện tại trên màn loading. Dùng lại ở màn Settings/About.
4. **Mẹo và thanh tiến trình:** giữ nguyên chỗ. Chữ trắng có viền tối, nên nền giữa chỉ cần yên.
5. **Icon ứng dụng và ảnh bìa:** dùng khi làm trang Play Store; icon cũng đặt vào Player Settings.
