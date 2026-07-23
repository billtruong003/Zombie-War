# ZombieWar — UI IMPLEMENTATION SPEC (v2 POLISH)

> **Nguồn chuẩn duy nhất** để build UI. Transfer 1:1 từ 3 sheet design
> (`Screenshot 2026-07-19 111106.png` = màn 01–07, `111136.png` = màn 08–15, `111154.png` = Sheet A/B/C hệ thống).
> Doc này THAY THẾ `UIUX_WIREFRAME_PROMPT.md` (bản ASCII cũ đã lỗi thời — PLAY xanh + 5 tab là SAI,
> v2 = PLAY **gold** + floating dock **4 tab**).
> Mọi số liệu bên dưới là **canvas px trên reference 1080×1920** (sheet vẽ ~390 wide → nhân 2.77, làm tròn grid 8).

> **Direction 02 correction (2026-07-23):** `Docs/UI_DIRECTION_02_HANDOFF.md` is the current
> execution overlay for Hub/Costume. Where this older v2 layout conflicts, Direction 02 wins.
> Specifically: all player-facing menu copy is English; Coin and Gem each have a `+` action;
> Mission reward is not white; Hub/Costume bottom panels pin to the bottom safe area; Costume
> controls may not overlap; and RawImage content must preserve its native aspect ratio.

---

## 0. QUY ƯỚC ĐỌC SPEC

Ký hiệu anchor (anchorMin → anchorMax của RectTransform):

| Ký hiệu | anchorMin | anchorMax | Ý nghĩa |
|---|---|---|---|
| `TL` | (0,1) | (0,1) | ghim góc trái-trên |
| `TC` | (0.5,1) | (0.5,1) | ghim giữa-trên |
| `TR` | (1,1) | (1,1) | ghim góc phải-trên |
| `C` | (0.5,0.5) | (0.5,0.5) | ghim tâm |
| `BL` | (0,0) | (0,0) | ghim góc trái-dưới |
| `BC` | (0.5,0) | (0.5,0) | ghim giữa-dưới |
| `BR` | (1,0) | (1,0) | ghim góc phải-dưới |
| `STRETCH-T` | (0,1) | (1,1) | dải ngang bám mép trên (sizeDelta.y = height, offset trái/phải) |
| `STRETCH-B` | (0,0) | (1,0) | dải ngang bám mép dưới |
| `FULL` | (0,0) | (1,1) | phủ toàn parent (offsets = padding) |

- **Pos** = `anchoredPosition` (px, tính từ anchor, y dương hướng lên).
- **Pivot** mặc định = tâm của anchor tương ứng (TL→(0,1), BC→(0.5,0)…); ghi riêng nếu khác.
- Mọi list dài đặt trong **ScrollRect** (vertical, movement Elastic, scrollbar ẩn).
- Mọi element tương tác: **touch target ≥ 132px** (48dp — Sheet C mục A11Y); nếu visual nhỏ hơn thì thêm hit-area trong suốt.

---

## 1. GLOBAL SETUP (mọi scene có UI)

```
UIRoot (Canvas)
├─ Canvas: Screen Space – Overlay, sortingOrder 10
├─ CanvasScaler: Scale With Screen Size, ref 1080×1920, Match = 0.5
├─ GraphicRaycaster
└─ <Screen>/Safe (SafeArea component — kéo offset theo Screen.safeArea, notch-aware)
```

- Mỗi màn = 1 prefab `UIScreen` con của UIRoot, cấu trúc: `Screen → Bg (FULL) → Safe (FULL + SafeArea) → content`.
- SafeArea chỉ áp cho content; **Bg luôn FULL** tràn cả notch (sheet B: "safe-area env() top").
- Điều hướng: push = slide phải 250ms, pop = fade + scale 0.98 (Sheet C).

---

## 2. DESIGN TOKENS

### 2.1 Màu (khoá cứng — không chế thêm)

| Token | Hex | Dùng cho |
|---|---|---|
| `bg` | `#141821` | nền mọi màn |
| `surface` | `#1B2130` | card, dock, modal, pill |
| `surface2` | `#232B3A` | card lồng trong card, hover |
| `hairline` | `#2A3244` | viền 2px, divider, dashed border |
| `text` | `#F4F6F8` | chữ chính |
| `textDim` | `#9AA3B2` | chữ phụ, label, icon inactive |
| `gold` | `#F5B841` | Coin, PLAY CTA, giá, kỷ lục, FTUE highlight |
| `goldHi` / `goldLo` | `#FFCF66` / `#E89A2B` | 2 đầu gradient dọc của nút gold |
| `onGold` | `#14181F` | chữ đen trên nền gold |
| `cyan` | `#5BD9E8` | KC (gem) — TUYỆT ĐỐI không dùng gold cho KC |
| `green` | `#4CAF6E` | positive: TIẾP TỤC, CLAIM, mua được, toggle ON, +42% |
| `greenLo` | `#37814F` | pressed/đáy bevel của nút green |
| `danger` | `#E5484D` | BỎ TRẬN, thiếu tiền, notification dot, HP thấp |
| `rarity0..4` | `#9AA3B2` `#4CAF6E` `#4FA3F7` `#8B7BD8` `#F2994A` | Xám→Xanh lá→Xanh biển→Tím→Cam (Sheet A) |

### 2.2 Typography (Sheet A "TYPOGRAPHY HIERARCHY" — sheet ghi size/weight ở 390, đã ×2.77)

| Style | Size / Weight | TMP setup |
|---|---|---|
| `Header` | 76 / 800 | Bold, tracking +2, UPPERCASE khi là tiêu đề màn |
| `Subheader` | 52 / 600 | SemiBold |
| `Body` | 38 / 500 | "chữ ngắn, đời thường" — không viết hoa |
| `Number` | theo ngữ cảnh / 700 | **tabular nums** (TMP: font feature `tnum` hoặc font mono-digit) — mọi số tiền/score |
| `Label` | 30 / 700 | UPPERCASE, tracking +4, màu `textDim` |

Font: 1 bộ sans (Inter/Roboto SDF) + tạo 3 material preset: default, glow-gold (outline 0, underlay gold soft), glow-green.

### 2.3 Shape / Elevation

| Token | Giá trị |
|---|---|
| radius card lớn | 32 |
| radius card nhỏ / button | 24 |
| radius pill | height/2 (full round) |
| bevel button | đáy tối cao **10px** cùng hue (goldLo/greenLo) — cảm giác bấm |
| glow rarity | sprite glow 9-slice màu rarity, alpha 0.35, nở 24px quanh card |
| glass pill | surface alpha 0.85 + hairline 2px + không blur (mobile) |
| dashed border | sprite `rounded_dashed_2px` (dash 12/8) màu hairline |

### 2.4 Sprite assets cần làm 1 lần (Assets/_Project/UI/Sprites)

`rounded_24`, `rounded_32` (9-slice trắng), `rounded_dashed`, `circle`, `ring_thin`,
`glow_soft_9slice`, `grad_gold_v` (dọc goldHi→goldLo), `grad_red_vignette`, `icon_*`
(coin, gem, lock, back, gear, bomb, fire, dice, pencil — lấy từ GUI Pro-SuperCasual nếu khớp style, không thì vẽ flat đơn sắc).

---

## 3. COMPONENT LIBRARY (Sheet A — build thành prefab, mọi màn tái dùng)

### 3.1 `BtnPrimary` (gold) / `BtnGreen` / `BtnDanger` / `BtnGhost`

```
Btn (Image = màu Lo — chính là phần đáy bevel, radius 24, Button component)
└─ Face (Image = grad/màu Hi, FULL, offsetMin.y = 10)   ← đáy hở 10px = bevel
   └─ Label (TMP FULL, Header/Subheader, onGold hoặc trắng)
```

| State (Sheet A) | Thể hiện |
|---|---|
| Default | như trên; **PLAY riêng có idle breathing** scale 1→1.02 loop 2.4s |
| Pressed | Face offsetMin.y 10→2 (lún xuống), scale 0.96, 100ms |
| Disabled | toàn bộ đổi `surface2`, label `textDim`, không glow |
| Ghost | không nền, viền 2px hairline, label textDim |

### 3.2 `Card` (surface, radius 32) — states: Default / Hover(lift) / **Selected**

- Selected: viền 2px màu rarity/green + `glow_soft` cùng màu alpha 0.35 + dấu ✦/check-dot xanh 28px góc phải-trên.
- Cấu trúc: `Card(Image surface) → Glow(behind, tắt mặc định) → Border(Image rounded viền, tắt mặc định) → Content`.

### 3.3 `RarityCard` — card item súng/costume

```
RarityCard (200×200 hoặc 440×320 tuỳ màn)
├─ Glow  (glow_soft, màu rarity, alpha .35, size +48/+48, raycast off)
├─ Bg    (rounded_24, surface)
├─ Border(rounded_24 viền 3px, màu rarity)
├─ Icon  (giữa, ~60% card)
├─ NameLabel (Label, đáy card — chỉ card to)
├─ PriceChip (pill nhỏ đáy: icon coin/gem 32 + Number 30) — thiếu tiền → chữ danger
├─ Dot   (circle 22 danger, TR pos (6,6)) — notification/NEW
└─ Lock  (icon lock 56 giữa + Bg alpha 0.6 đè lên) — chưa sở hữu
```

Legendary có thêm **shine**: gradient trắng alpha chạy dọc viền mỗi 3s (Sheet C).

### 3.4 `CurrencyPill` (glass)

```
Pill (H 72, W auto ≥ 176, full-round, surface .85 + hairline)
├─ Icon (circle 44: coin=gold / gem=cyan, L pos (14,0))
└─ Value (Number 34 tabular, màu = màu icon, R margin 24)
```

Coin bay về pill khi cộng tiền (Sheet C "Coin collect"); pill nảy scale 1.1 khi nhận.

### 3.5 `Toggle` (Sheet A)

- Track: pill 96×52; ON = green, OFF = surface2 + hairline. Knob: circle 44 trắng, x = ±22, tween 150ms Standard.

### 3.6 `SegmentedTabs` (Shop/Costume)

- Khung: pill full-width H 88, surface. Mỗi tab = pill con; **active = fill màu ngữ cảnh** (WEAPONS/COSTUME/UPGRADES = green, GACHA = gold), label onGold/trắng; inactive = trong suốt + label textDim. Tab đổi = active pill tween vị trí 200ms.

### 3.7 `ProgressPips` (Upgrades) — 5 ô 40×16 radius 8, gap 8; filled = green, empty = surface2.

### 3.8 `ProgressBar` — track pill H 20 surface2, fill pill màu ngữ cảnh (green/purple), animate fill 300ms Enter.

### 3.9 `Skeleton` — **KHÔNG spinner** (Sheet A/C): các bar rounded surface2 + shimmer gradient chạy 1.2s loop.

### 3.10 `Modal`

```
ModalRoot (FULL)
├─ Dim (FULL, đen alpha 0.7, Button = tap-outside tuỳ màn)
└─ Panel (C, W 800, radius 32, surface, ContentSizeFitter dọc)
```
Enter: fade dim + panel scale 0.95→1 (Enter 300ms). Exit: 150ms.

### 3.11 `HeaderBack` (màn con: Loadout/Costume)

| Element | Anchor | Pos | Size | Style |
|---|---|---|---|---|
| BackBtn | TL | (32,-32) | 88×88 | rounded_24 `#4FA3F7`, icon ← trắng 44 |
| Title | TC | (0,-48) | 600×80 | Header UPPERCASE, trắng |

---

## 4. SPEC 15 MÀN

### 04.1 — MÀN 01 · HUB (`Menu.unity`)

```
HubScreen
├─ Bg (FULL, bg)
└─ Safe
   ├─ AvatarChip      TL   pivot(0,1)
   ├─ CurrencyRow     TR   pivot(1,1)
   ├─ PreviewCard     TC
   ├─ PlayBtn         BC
   └─ Dock            BC
```

| Element | Anchor | Pos | Size | Chi tiết |
|---|---|---|---|---|
| **AvatarChip** | TL | (32,-32) | 96×96 + text | circle avatar 96 (`#4FA3F7` placeholder); bên phải cách 16: 2 dòng — `KỶ LỤC` Label 26 dim / `WAVE 12` Subheader 40 trắng |
| **CurrencyRow** | TR | (-32,-44) | H 72 | 2 × CurrencyPill: `8,458` gold, `10` cyan; gap 16; xếp phải→trái |
| **PreviewCard** | TC | (0,-200) | 930×860 | rounded_dashed hairline; **RawImage** render texture camera preview nhân vật 3D (podium trong scene); tap card = xoay nhân vật (drag X) |
| EditChip | (con PreviewCard) BR | (-24,24) | 120×72 | pill surface + "EDIT" Label gold → mở Costume |
| **PlayBtn** | BC | (0,264) | 960×150 | `BtnPrimary` gold gradient, label **PLAY** Header 64 `onGold`; idle breathing; đứng trên dock 48px |
| **Dock** | BC | (0,32) | 1016×152 | rounded_32 surface + hairline; **floating** (hở 32 hai bên + đáy) |
| Dock.Tab ×4 | trong dock, chia đều 254/tab | — | 200×152 | thứ tự: **LOADOUT · SHOP · COSTUME · PASS** (KHÔNG có tab PLAY). Icon 56 màu gold-green flat, label Label 24 dim ở dưới (icon y +28, label y −40) |
| Dock.SHOP.Dot | TR icon | (6,6) | 22 | circle danger, pulse loop (Sheet C "Notification dot") |

Ẩn hoàn toàn: settings gear không có trên sheet HUB → gear nằm trong PASS? **Không** — Settings mở từ Pause + 1 icon nhỏ: sheet 01 KHÔNG có gear ⇒ bỏ gear khỏi hub, Settings vào từ Pause (màn 11) ✔.

### 04.2 — MÀN 02 · LOADOUT

Header: `HeaderBack` ("LOADOUT").

| Element | Anchor | Pos | Size | Chi tiết |
|---|---|---|---|---|
| SlotRow | TC | (0,-160) | 3×RarityCard 200×200, gap 24 | slot vũ khí: viền rarity; slot đang chọn: Selected state + Dot đỏ nếu có nâng cấp; slot trống: rounded_dashed + icon + mờ |
| InfoPanel | TC | (0,-420) | 1016×300 | card surface: dòng 1 `Rifle · RARE` Subheader (tên trắng + rarity màu `rarity2`); dưới: 3 stat bar — `DMG` (fill `#4FA3F7`), `TỐC BẮN` (green), `TẦM` (gold); mỗi bar: Label trái 160 + ProgressBar còn lại |
| BombRow | TC | (0,-760) | 1016×120 | card surface: circle icon bom 72 trái + `Bom — 1 loại` Body + chip `x3` pill gold phải |
| KhoLabel | TL(Safe) | (32,-920) | — | `DÒNG DÀI / KHO SỞ HỮU` Label dim |
| KhoGrid | STRETCH-T | y -980 | ScrollRect, GridLayout 3 cột, cell 312×312, gap 24, pad 32 | RarityCard từng súng sở hữu; chưa sở hữu = Lock state |
| ShopLink | BC | (0,48) | — | Body 34: `Chưa có súng?` dim + `Tới Shop →` **gold** (button) |

### 04.3 — MÀN 03 · COSTUME

Header: `HeaderBack` ("COSTUME").

| Element | Anchor | Pos | Size | Chi tiết |
|---|---|---|---|---|
| PreviewCard | TC | (0,-160) | 700×520 | rounded_dashed + RawImage preview (same camera hub, crop) |
| PartTabs | TC | (0,-720) | 640×88 | `SegmentedTabs` 3 tab: **Đầu / Thân / Chân**, active green |
| PartGrid | STRETCH-T | y -840, đáy chừa 200 | ScrollRect Grid 3 cột, cell 292×292, gap 24 | RarityCard part: selected = viền blue + check-dot green; locked = Lock + PriceChip |
| RandomBtn | BC | (0,48) | 520×120 | `BtnPrimary` gold: icon dice + `Ngẫu nhiên` Subheader onGold |

### 04.4 — MÀN 04–07 · SHOP (1 màn, 4 tab — shell chung)

| Element | Anchor | Pos | Size | Chi tiết |
|---|---|---|---|---|
| Title | TC | (0,-40) | — | `SHOP` Header |
| Tabs | TC | (0,-140) | 1016×88 | `SegmentedTabs` 4: **WEAPONS · GACHA · COSTUME · UPGRADES** |
| Content | FULL | top -260, bottom 32 | ScrollRect | nội dung theo tab ↓ |

**Tab WEAPONS (04):** group theo class — `SectionLabel` (Label dim, `PISTOL`, `SMG`…) + grid 2 cột `RarityCard` 476×340 (icon súng, tên Body, PriceChip coin).
States: mua được = viền green + giá gold · sở hữu = badge `ĐÃ CÓ` + nút `Lên nạc` nhỏ · **thiếu tiền = giá danger, tap → shake ×4px + red flash + haptic** (Sheet C) · NEW = ribbon đỏ góc phải.

**Tab GACHA (05):** 2 banner card full-width 952×560, gap 32:
- `Súng Gacha` — viền + glow `rarity4` cam/gold; art cổng gacha giữa (placeholder rounded tối); dòng rate Body dim (`Rớt tỉ lệ theo Tier…`); đáy 2 nút gold cạnh nhau 440×96: `Quay 1 · 100` / `Quay 10 · 900` (icon coin).
- `Skin Gacha` — viền cyan, 2 nút **cyan** `Quay 1 · 10` / `Quay 10 · 90` (icon KC).
Reveal anim: card lật + light rays + particles theo rarity (Sheet C).

**Tab COSTUME (06):** SetCard full-width 952×360 viền `rarity3` tím: tên set Subheader, `5 món — SET giảm 30%` Body dim, PriceChip **cyan KC**. Dưới: `ITEM LẺ` Label + hàng ngang scroll cards 220×220 (part lẻ giá coin, locked).

**Tab UPGRADES (07):** đầu: `TỔNG SỨC MẠNH` Label dim + `+42%` Header **green**. 3 hàng card 1016×176 gap 20 — mỗi hàng: icon 88 (rounded surface2) + tên Body trắng + `ProgressPips` 5 nấc + nút phải 240×96:
- còn nâng được: `BtnPrimary` gold `NÂNG` + giá coin
- đủ cấp: chip `MAX` surface2/textDim (disabled)
- nâng thành công: scale 1.05 + green glow + **confetti burst** (Sheet C).

### 04.5 — MÀN 08 · BATTLE PASS

| Element | Anchor | Pos | Size | Chi tiết |
|---|---|---|---|---|
| Title | TC | (0,-40) | — | `BATTLE PASS` Header |
| MilestoneLabel | TL | (32,-140) | — | `MIỄN PHÍ` Label dim |
| TrackRow | STRETCH-T | y -190, H 220 | ScrollRect ngang | tile 200×200 RarityCard: coin/skin/khoá; tile hiện tại = Selected viền gold; đã nhận = mờ + check |
| PremiumBanner | TC | (0,-460) | 1016×120 | card surface2 + lock: `Premium Pass 🔒 [PLACEHOLDER]` Body dim — không bán |
| QuestLabel | TL | (32,-620) | — | `Nhiệm vụ hôm nay` Subheader |
| QuestList ×3 | STRETCH-T | từ -690, mỗi hàng 140 + gap 16 | 1016×140 | card: tên Body + `ProgressBar` green + phải: `CLAIM` BtnGreen 180×80 khi đủ, ngược lại counter `x/y` Number dim |
| GachaLink | BC | (0,48) | — | `Quay Gacha →` gold |

### 04.6 — MÀN 09 · HUD IN-RUN (Sheet B: **mọi thứ snap grid 48px**, thumb-zone = 40% dưới)

| Element | Anchor | Pos | Size | Chi tiết |
|---|---|---|---|---|
| HPBar | TL | (48,-48) | 400×36 | pill: fill green **segmented** (vạch 4px mỗi 20%); <30% → pulse + glow đỏ + heartbeat (Sheet C "Low HP") |
| WavePill | TC | (0,-48) | ~360×64 | glass pill: `Wave 3 — 12 🧟` Number 34 (12 = còn lại) |
| CoinPill | TR | (-48,-48) | CurrencyPill | chỉ Coin in-run; coin fly-up arc từ zombie về đây |
| BombBtn | TR | (-48,-160) | 112×112 | rounded_24 `#4FA3F7` + icon bom + count chip 36 góc; disabled = mờ |
| KillStreak | TC | (0,-260) | — | `x5 KILL STREAK!` Subheader gold, chỉ hiện khi streak, pop + fade |
| Joystick | BL | (96,96) | outer 288 / knob 128 | circle surface alpha .6 + knob; **fixed position** (trong thumb-zone) |
| FireBtn | BR | (-96,96) | 224×224 | circle green + icon; **ammo ring** = radial fill ring_thin gold quanh nút + count 40 |
| DamageNumber | world | — | — | TMP 3D: thường trắng 34, **crit gold to bold** + float-up + screen shake nhẹ |

KHÔNG có gì khác trong vùng giữa màn (giữ sạch cho gameplay). Mọi toạ độ chia hết cho 48.

### 04.7 — MÀN 10 · LEVEL-UP PERK (overlay in-run, **time-stop**)

| Element | Anchor | Pos | Size | Chi tiết |
|---|---|---|---|---|
| Dim | FULL | — | — | đen alpha 0.7 |
| Title | TC | (0,-360) | — | `LEVEL UP!` Header 96 **gold** + glow |
| CardStack ×3 | C | y +40, **xếp DỌC** gap 28 | 800×170 | card surface viền rarity của perk: icon 72 trái + tên Subheader + dòng mô tả Body dim (`+15% tốc bắn`); card giữa/được chọn: **scale 1.05 + glow** |
| Hint | BC | (0,140) | — | Body dim `Chạm để chọn` |

Enter: stagger 0.1s, slide-up + rotateZ nhẹ, easing **Elastic spring(1,.8,10)** (Sheet C). Tap = chọn ngay, card flash green rồi overlay đóng.

### 04.8 — MÀN 11 · PAUSE (Modal 800)

Panel content (top→down, pad 40, gap 24):
1. `TẠM DỪNG` Header **green**, giữa.
2. `TIẾP TỤC` BtnGreen 720×120 → đóng + **countdown 3-2-1** giữa màn rồi resume.
3. Hàng toggle: `Âm thanh` Body + Toggle phải; hàng `Rung` tương tự.
4. `BỎ TRẬN` BtnDanger 720×110 → confirm modal nhỏ (`Bỏ trận? Mất thưởng` / 2 nút).
5. (Settings đầy đủ mở từ đây nếu cần — icon gear nhỏ TR panel.)

### 04.9 — MÀN 12 · REVIVE (Modal 760, **1 lần/run**)

1. `HỒI SINH?` Header giữa.
2. CountdownCircle: circle 200 viền gold + Number 96 gold đếm `5→0`; hết giờ tự đóng = chết.
3. `Xem quảng cáo` BtnGreen 640×120 + chip `AD` gold góc [PLACEHOLDER ad SDK].
4. `Thôi` BtnGhost nhỏ dưới.
KHÔNG có nút hồi sinh bằng KC (đúng sheet).

### 04.10 — MÀN 13 · GAME OVER

Bg: bg + `grad_red_vignette` FULL alpha 0.5 (ấm, không punish).

| Element | Anchor | Pos | Size | Chi tiết |
|---|---|---|---|---|
| Banner | TC | (0,-140) | — | `HẾT TRẬN` Header 80 **gold** |
| RecordPill | TC | (0,-250) | pill gold fill | `KỶ LỤC MỚI! Wave 7` Number onGold — chỉ hiện khi phá kỷ lục + **confetti canvas** (Sheet C); không phá → `BEST Wave 12` pill surface dim |
| PayoutCard | TC | (0,-560) | 952×520 | card surface, các dòng H 84: trái icon+tên Body dim / phải Number gold `+1,250` — `Nhặt trong trận`, `Thưởng wave`, `Thưởng lần đầu`; divider hairline; `Tổng` Subheader + Number 64 gold `2,180`; dòng `KC nhặt được +5` **cyan** riêng |
| PassXPBar | TC | (0,-1130) | 952×20 | ProgressBar fill `rarity3` tím + Label `PASS XP +40` — animate fill |
| ShopLink | TC | (0,-1200) | — | `Súng mới ở Shop →` gold |
| ReplayBtn | BC | (0,220) | 960×150 | BtnPrimary gold `CHƠI LẠI` |
| HomeBtn | BC | (0,88) | 960×110 | BtnGhost `VỀ NHÀ` |

Số **nảy từng dòng tuần tự → dồn vào Tổng** (count-up + SFX từng dòng, 0.4s/dòng).

### 04.11 — MÀN 14 · SETTINGS (Modal 840 hoặc màn con)

1. `CÀI ĐẶT` Header.
2. `♪ Nhạc nền` + Slider (track pill H 16 surface2, fill trắng, knob 44) — hàng H 110.
3. `✦ Hiệu ứng` + Slider.
4. `Rung` + Toggle.
5. `Ngôn ngữ`: 2 chip `VI` / `EN` (chip active = green fill).
6. `Khôi phục mua hàng [PLACEHOLDER]` Body dim underline.

### 04.12 — MÀN 15 · FTUE OVERLAY (đè HUD lần chơi đầu)

| Element | Chi tiết |
|---|---|
| Mask | FULL đen alpha 0.7 có **cutout tròn** quanh element đang dạy (shader/stencil hoặc 4 image ghép) |
| HighlightRing | circle dashed **gold** bao joystick (r 320), pulse |
| TooltipChip | pill gold `Kéo để chạy!` Body onGold, đặt cạnh cutout (36 offset) |
| SkipBtn | TR (-32,-32) BtnGhost `Bỏ qua` |
| StepDots | BC (0,64): 3 dot 16, active gold — bước: di chuyển → bắn (auto) → ném bom |
| Bước cuối | chip `BẮT ĐẦU!` glow rồi tắt overlay |

---

## 5. SHEET B — HUD ALIGNMENT RULES (tóm tắt bắt buộc)

1. Grid 48px: mọi anchoredPosition/size của HUD chia hết cho 48 (trừ nội dung text).
2. Safe-area top: HP/Wave/Coin ghim vào `Safe`, không ghim canvas.
3. Thumb-zone: Joystick + FireBtn + BombBtn nằm hoàn toàn trong 40% chiều cao dưới.
4. Không element nào che tâm màn hình (vùng aim).

---

## 6. SHEET C — ANIMATION SPEC (đóng thành `UIFx` static helper)

### 6.1 Easing tokens

| Token | Curve | Duration |
|---|---|---|
| `Standard` | cubic-bezier(.4,0,.2,1) | 200ms |
| `Enter` | cubic-bezier(0,0,.2,1) | 300ms |
| `Exit` | cubic-bezier(.4,0,1,1) | 150ms |
| `Bounce` | cubic-bezier(.34,1.56,.64,1) | 400ms |
| `Elastic` | spring(1, .8, 10) | perk cards |

### 6.2 Component → Trigger → Motion

| Component | Motion |
|---|---|
| PLAY button | idle breathing 1→1.02 loop 2.4s · press scale .96 100ms |
| Notification dot | pulse scale 1→1.25 + opacity 1→.75, loop 1.8s |
| Legendary card | gradient shine chạy qua viền mỗi 3s |
| Perk cards | stagger 0.1s, slide-up + rotateZ nhẹ, Elastic |
| Coin collect | fly-up arc zombie → HUD coin, rotate + fade |
| Low HP | bar pulse + glow đỏ + heartbeat khi <30% |
| Crit | số vàng to bold, float-up, screen shake nhẹ |
| Error/thiếu tiền | shake ±4px + red flash + haptic 10ms |
| Success/nâng cấp | scale 1.05 + green glow + confetti burst |
| Gacha reveal | card lật + light rays + particles theo rarity |
| Screen push | slide phải 250ms · pop = fade + scale .98 |
| Loading | skeleton shimmer 1.2s — **KHÔNG spinner tròn** |

### 6.3 A11Y

- Touch ≥ 132px (48dp) · contrast WCAG AA · danger luôn kèm icon (không chỉ màu)
- Reduced Motion (setting): mọi motion → chỉ fade
- Haptic: 10ms tap nhẹ / nặng cho error · safe-area top luôn tôn trọng.

---

## 7. THỨ TỰ BUILD LẠI (installer per màn, mỗi cái idempotent)

1. `UIThemeV2` + sprite assets (§2) + component prefabs (§3) — **làm trước, mọi màn dùng chung**
2. Hub (§4.1) → Loadout (§4.2) → Costume (§4.3) → Shop 4 tab (§4.4)
3. HUD (§4.6) → Level-up (§4.7) → Pause/Revive (§4.8–9) → GameOver (§4.10)
4. Pass (§4.5) → Settings (§4.11) → FTUE (§4.12)
5. `UIFx` animation helper (§6) wire vào toàn bộ.

> Deviation log (ghi lại khi build nếu buộc phải lệch sheet): _trống_.
