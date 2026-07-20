# ZombieWar — UI/UX Design Rationale (vì sao thiết kế như vậy)

> **Historical design rationale.** Its phase/status table is obsolete. Use `HANDOFF_UI_CODEX.md` for current implementation status.

> Tài liệu này là **phần "tại sao"** đi kèm `UIUX_WIREFRAME_PROMPT.md` (phần "vẽ gì").
> Mọi quyết định đều có lý do gắn với gameplay thật + roadmap (P0→P10). Đọc doc này trước khi sửa bất kỳ layout nào.

---

## 1. Decompose (Phase 1 theo skill unity-ui-ux-designer)

| Trục | Kết luận | Căn cứ |
|---|---|---|
| **Genre** | Roguelite top-down endless survivor (Survivor.io-like) | PRODUCT_ROADMAP: endless + loot + perk + meta shop |
| **Platform / Input** | Mobile **portrait 9:16**, one-thumb touch | Screenshots 360×640, joystick + auto-aim đã code |
| **Session** | Run ngắn 3–10 phút, chơi nhiều run | Endless + die = hết run |
| **Aim model** | **Auto-aim + auto-fire** (density-seeking đã có) | → KHÔNG cần fire button, giải phóng ngón phải |

### Player Journey (flow đề xuất)

```
Boot/Splash
   │
   ▼
MAIN MENU HUB (portrait, char preview giữa)
   ├── tab: SHOP        [PLACEHOLDER P4]
   ├── tab: LOADOUT     (đã code — slot 1 pistol khoá, slot 2-3, bomb)
   ├── tab: PLAY  ◄── CTA chính, to nhất
   ├── tab: COSTUME     (đã code — modular parts)
   └── tab: STATS       [PLACEHOLDER P5 — achievements/high score]
   ├── gear: SETTINGS overlay
   ▼ PLAY
IN-RUN HUD ──► LEVEL-UP OVERLAY (3 perk cards, time-stop) [PLACEHOLDER P3]
   │   ▲             │ pick 1 → resume
   │   └─────────────┘
   ├── PAUSE overlay (resume / settings / quit-run)
   ▼ chết
GAME OVER (score + coins count-up)
   ├── RESTART (primary — vòng lặp nhanh)
   └── HOME → về Hub (coins đập vào mắt → mở Shop)
```

**Vì sao Hub-and-Spoke chứ không phải menu tuyến tính:** meta loop (giết→coin→shop→mạnh hơn→chơi lại) là engine giữ chân của thể loại. Hub với tab bar cố định làm mọi hệ meta (shop/loadout/costume/stats) luôn cách người chơi **1 tap**, và nhân vật 3D preview đứng giữa hub tạo cảm giác *sở hữu* — nền tảng để cosmetic có giá trị.

### Core Loop 60 giây & hệ quả UI

Di chuyển liên tục (ngón trái) → súng tự bắn → né horde → nhặt loot (magnet) → XP đầy → chọn perk → lặp.
- Thứ cần thấy **NGAY**: HP, đạn/reload, XP progress. → đặt ở vị trí mắt đang nhìn (quanh nhân vật + mép trên).
- Hành động tần suất: move (liên tục) > switch weapon (trung bình) > bomb (thấp) > pause (hiếm). → move chiếm nửa trái, switch/bomb cụm ngón phải, pause đẩy góc xa.

### Emotional Beats → tông UI từng màn

| Beat | Màn | Tông |
|---|---|---|
| Căng thẳng | HUD in-run | Tối giản tuyệt đối, contrast cao, không chrome |
| Quyết định | Level-up cards | Time-stop, dim nền, 3 lựa chọn to rõ |
| Phần thưởng | Game Over | Count-up số, celebrate, funnel về Shop |
| Sở hữu / thể hiện | Hub, Costume | Nhân vật to giữa màn, ánh sáng đẹp |
| Mua sắm | Shop | Grid card, giá rõ, rarity màu |

---

## 2. Các quyết định then chốt + lý do

### D1. Portrait + one-thumb là ràng buộc số 1
Mọi element tương tác in-run nằm **40% dưới màn hình** (thumb zone). Mép trên = thông tin thuần, không bấm. *Lý do:* dữ liệu thumb-reach trên phone; auto-fire đã bỏ nhu cầu ngón phải liên tục → chơi được bằng 1 tay, đúng session "chơi vặt".

### D2. Không có fire button
Auto-aim density-seeking đã là core mechanic. Thêm fire button = thêm cognitive load vô nghĩa. Ngón phải chỉ còn 2 nút: **Weapon Switch** (cycle 3 slot) và **Bomb** — xếp dọc góc phải-dưới, bomb thấp nhất (to hơn, dùng lúc khẩn cấp).

### D3. HP không nằm trên top bar
Mắt người chơi khoá vào nhân vật giữa màn, không đọc mép trên khi đang né horde. → **HP bar nhỏ world-space trên đầu nhân vật** + **red vignette toàn màn khi trúng đòn** (đọc được bằng ngoại vi mắt). Top chỉ giữ: XP bar full-width (mép trên cùng — salience của progression, giống Survivor.io), wave/timer trái, kill/coin phải.

### D4. Level-up = time-stop + 3 card
Touch không có độ chính xác để vừa né vừa chọn. Time-stop biến level-up thành **nhịp thở + khoảnh khắc dopamine** — beat cảm xúc quan trọng nhất của run. 3 card (không 4, không list) vì portrait chỉ đủ 3 card đọc được 1 nhãn + 1 dòng mô tả.

### D5. Game Over là phễu vào meta, không phải màn thua
Thứ tự hiển thị: **Score to nhất → high score (so sánh) → coins earned count-up** (animation + SFX). Nút: RESTART primary (vòng lặp nhanh giữ flow), HOME secondary. Coins count-up là *lý do* người chơi mở Shop — GameOver phải bán được meta loop.

### D6. Loadout giữ luật thiết kế đã code, nói bằng hình
Slot 1 pistol **không tháo được** → icon khoá + tooltip "Pistol là vũ khí sinh tồn". Slot 2-3 tháo/đổi tự do. Bomb slot riêng biệt hình khác (tròn vs vuông) để không nhầm là súng. Picker filter đúng theo slot đang chọn — không bao giờ hiện súng không hợp lệ (prevent error > báo error).

### D7. Costume: preview là nhân vật chính của màn
Nửa trên = character 3D xoay được; nửa dưới = slot tabs (Hair/Head/Body/Legs/Hands/Beard) + grid part. Tap part = apply ngay lên preview (**instant feedback, không nút Confirm**) — save khi rời màn. *Lý do:* thử đồ phải zero-friction; confirm dialog giết cảm giác "ướm đồ".

### D8. Shop [PLACEHOLDER P4] thiết kế đủ ngay từ giờ
Tabs: Weapons / Upgrades / Cosmetics. Card grid 2 cột, rarity viền màu, giá + nút mua trạng thái đủ tiền/thiếu tiền (disabled + đỏ). Thiết kế đủ để dev wire sau, đánh tag PLACEHOLDER ở logic giá/currency.

### D9. Settings là overlay, không phải scene
Audio (music/SFX sliders), haptics toggle, graphics (Low/Med/High), restore. Overlay dùng chung cho Hub + Pause. *Lý do:* settings từ trong run không được unload scene.

### D10. Onboarding contextual, không tutorial screen
First-run: 3 tooltip lần lượt (kéo để đi chuyển → nút bomb → nhặt loot), dismiss theo hành động thật. Không màn tutorial riêng. *Lý do:* survivor-like học bằng chơi trong 10 giây; tutorial screen tăng drop-off trước lần chơi đầu.

---

## 3. Visual language (chốt để mọi màn nhất quán)

| Token | Giá trị | Vai trò |
|---|---|---|
| `bg-base` | `#141821` | Nền tối — máu/loot/neon nổi |
| `surface` | `#1E2430` | Panel, card |
| `primary` | `#4CAF6E` | CTA (PLAY, mua được, confirm) |
| `danger` | `#E5484D` | HP, damage, QUIT, thiếu tiền |
| `gold` | `#F5B841` | Currency, score, reward |
| `arcane` | `#8B7BD8` | Cosmetic/rarity/XP |
| `text-hi` | `#F4F6F8` | Chữ chính |
| `text-lo` | `#9AA3B2` | Chữ phụ, label |

Typography: **Header** = condensed bold uppercase (kiểu Anton/Bebas), Title 56, Section 28. **Body** = Inter/Roboto 16–18. **Số** (score/damage/giá) = bold tabular.
Shape: bo góc 12px panel / 8px button; button có face + darker bottom edge 4px (cảm giác bấm được trên touch).

---

## 4. Screen inventory + trạng thái code

| # | Màn | Code status | Ghi chú wireframe |
|---|---|---|---|
| 1 | Main Menu Hub | Menu.unity đã có (PLAY/QUIT/LOADOUT/COSTUME) | Nâng cấp thành hub + tab bar + currency |
| 2 | Loadout | ✅ đã code | Vẽ lại theo visual language |
| 3 | Costume | ✅ đã code | Preview + tabs + grid |
| 4 | Shop | ⬜ P4 | [PLACEHOLDER] toàn bộ logic |
| 5 | Stats/Achievements | ⬜ P5 | [PLACEHOLDER] |
| 6 | Settings overlay | ⬜ P8 | [PLACEHOLDER] |
| 7 | In-run HUD | 1 phần (joystick/bomb/switch có) | XP bar, vignette, coin counter [PLACEHOLDER P2] |
| 8 | Pause overlay | GameStateMachine có Pause state | UI chưa có |
| 9 | Level-up perk cards | ⬜ P3 | [PLACEHOLDER] |
| 10 | Game Over | State có, UI thô | Score/coins count-up [PLACEHOLDER P1] |

Thứ tự đưa Claude Design vẽ: **HUD + Level-up + GameOver trước** (in-run là nơi người chơi sống 90% thời gian, và P0–P3 là khúc roadmap sắp code) → Hub + Shop → còn lại.

---

## ADDENDUM v1.1 — Economy UI (sync với ECONOMY_DESIGN.md)

- **3 currency hiển thị nhất quán mọi màn:** Coin + Gold + Gem cụm góc phải-trên (Hub/Shop/Loadout/Costume/GameOver). In-run HUD chỉ hiện Coin (+floater khi nhặt Gold/Gem — hiếm nên cần moment).
- **Gem = prestige:** mọi chỗ hiện Gem dùng màu riêng biệt (cyan/diamond) + hiệu ứng sparkle nhẹ; KHÔNG dùng chung màu accent #F5B841 (đó là Coin/vàng UI).
- **Shop tách 3 tab:** Weapons (Coin/Gold, tag "GOLD ONLY" cho súng signature) / Gacha (2 banner: Súng-Gold, Skin-Gem) / Costume (part lẻ Coin-Gold, SET full-body chỉ Gem 200/500/1000).
- **Costume grid:** item default (chest 61-66, leg 62-67, feet 1,2,3,4,6,7,55, head 38,53,55) + face/hair/beard free hiện bình thường; item chưa sở hữu → khoá + price tag currency icon; màu râu/tóc là option riêng trong cùng category.
- **GameOver payout breakdown:** 3 dòng đếm dồn (nhặt trong trận / bonus wave / bonus kill) → tổng Coin bay lên counter. Gem nhặt được hiện dòng riêng có sparkle.
- **Gacha screen:** 1 nút pull single + x10, hiện rate, kết quả reveal card (rarity glow). FTUE rigged pull đầu ra SMG auto-equip.
- **Revive prompt:** 1 lần/run, nút xem ad [PLACEHOLDER], countdown 5s, không có nút Gem.
