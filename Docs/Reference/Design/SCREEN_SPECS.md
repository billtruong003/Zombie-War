# ZombieWar — Screen Specs (spec từng màn hình)

> **Presentation specification.** Placeholder labels indicate intentionally missing backend, not missing screen prefabs.

> Mỗi màn: mục đích 1 câu → thành phần → data hiện gì → trạng thái phụ. Portrait 9:16.
> Cụm 3 tiền (Coin 🟡 / Vàng 💰 / KC 💎) luôn ở góc phải trên MỌI màn meta — không nhắc lại từng màn nữa.

## 1. HUB (menu chính)

Mục đích: bàn đạp — nhìn nhân vật của mình + bấm PLAY.

- Giữa màn: nhân vật 3D mặc costume hiện tại, cầm súng Slot 2 (hoặc pistol), idle anim.
- Nút PLAY to nhất màn, đáy giữa.
- Hàng nút dưới (icon + chữ): LOADOUT · SHOP · COSTUME · PASS.
- Góc trái trên: nút ⚙ Settings. Dưới nó: high score ("Kỷ lục: Wave 12").
- Badge chấm đỏ trên SHOP/PASS khi có gì claim được / đủ tiền mua súng mới.

## 2. LOADOUT

Mục đích: lắp súng vào 3 slot + slot bom.

- 3 card slot to hàng ngang: Slot 1 (pistol — chỉ THAY, không tháo), Slot 2–3 (súng dài, tháo được, có nút ✕).
- Slot bom riêng bên dưới (v1 chỉ 1 loại bom → hiện khoá chọn).
- Chạm slot → mở picker: grid card súng ĐANG SỞ HỮU hợp slot (slot 1 lọc pistol; slot 2–3 lọc súng dài). Card: viền rarity + ảnh + tên + sao + DPS ước tính.
- Slot trống + không có súng → dòng "Chưa có súng? Tới Shop →".
- Đổi xong tự lưu, không nút Save.

## 3. COSTUME

Mục đích: thay đồ, khoe. KHÔNG chỉ số.

- Trái: preview nhân vật 3D xoay được (drag).
- Phải: cột part (Đầu/Thân/Chân…) → chạm part → grid item sở hữu, viền rarity.
- Item chưa có → mờ + giá (KC) → chạm nhảy Shop tab COSTUME.
- Nút "Ngẫu nhiên" 🎲 nhỏ.

## 4. SHOP — tab WEAPONS

Mục đích: mua súng bằng tiền thẳng (không may rủi).

- Grid card súng theo họ (6 section: Pistol/SMG/Rifle/Shotgun/Sniper/LMG). Card: viền rarity, ảnh, tên, giá.
- Giá: Xám/Xanh lá/Xanh biển = Coin; Tím/Cam = Vàng (theo ECONOMY §5).
- Đã sở hữu → card đổi "ĐÃ CÓ" + nút "Lên sao" (hiện mảnh: 12/20 🧩 + giá Vàng).
- Không đủ tiền → nút xám, chạm rung + "Thiếu X".
- Tím/Cam có dòng nội tại ngắn ("Xuyên 2 mục tiêu").

## 5. SHOP — tab GACHA

Mục đích: máy quay 2 banner.

- 2 banner card to: **Súng** (quay bằng Vàng) · **Skin** (quay bằng KC).
- Mỗi banner: ảnh key, nút Quay 1 (giá) + Quay 10 (giảm ~10%), dòng tỉ lệ nhỏ "Tỉ lệ ▸" mở modal bảng %.
- Pity: thanh "Còn 27 lượt chắc chắn ra Tím+".
- Trùng súng → tự đổi mảnh ★ (hiện dòng quy đổi trong kết quả).
- Kết quả quay: modal celebration — card lật, viền màu rarity phát sáng, Tím+ có hiệu ứng riêng.
- FTUE: lần đầu vào tab có 1 vé free, kết quả rigged SMG Xanh lá.

## 6. SHOP — tab COSTUME

- Grid set + item lẻ. Set = card to (3–5 món, giá Gem 200/500/1000), item lẻ card nhỏ.
- Free/default đánh dấu "Miễn phí". Đã có → "ĐÃ CÓ" + nút "Mặc thử" → nhảy màn COSTUME.

## 7. SHOP — tab UPGRADES (nâng người — Coin sink)

Mục đích: đốt Coin. ĐÚNG 3 dòng, cấm thêm:

| Dòng | Icon | Nấc |
|---|---|---|
| Sát thương | ⚔ | 5 nấc, mỗi nấc +4% |
| Máu | ❤ | 5 nấc, mỗi nấc +10% |
| Tốc bắn | ⚡ | 5 nấc, mỗi nấc +3% |

- Mỗi dòng: icon + tên + 5 chấm nấc + giá nấc kế (Coin, tăng dần) + nút NÂNG.
- Max nấc → "MAX" vàng. Tổng sức mạnh cộng dồn hiện trên đầu tab.

## 8. BATTLE PASS

- Track ngang cuộn: node mốc theo XP pass. Node: quà (Coin/Vàng/mảnh/vé quay) + trạng thái (khoá/claim được/đã claim).
- Hàng Premium mờ + ổ khoá [PLACEHOLDER — không design màn mua].
- Dưới: 3 quest ngày (VD "Giết 200 zombie 120/200") + thanh tiến độ + nút CLAIM.
- Claim vé quay → nút "Quay Gacha →".

## 9. HUD IN-RUN

- Trái dưới: joystick (vùng chạm to, ẩn khi không chạm).
- Phải dưới: nút BOM (to, số bom còn) + nút ĐỔI SÚNG (nhỏ hơn, icon súng kế). KHÔNG nút bắn/nạp.
- Trên giữa: wave + đếm zombie còn ("Wave 3 — 12 🧟"). Trái trên: HP bar. Dưới HP: thanh XP in-run.
- Phải trên: Coin nhặt trong trận (đếm nảy). Nút ⏸ góc phải trên cùng.
- Damage number bay trên đầu zombie (đã có sẵn hệ thống — chỉ cần style).

## 10. LEVEL-UP PERK (modal, game pause)

- "LEVEL UP!" + 3 card perk dọc (icon + tên + 1 dòng: "+15% tốc bắn"). Chạm 1 card → về trận ngay.
- Không nút skip riêng (chọn là skip).

## 11. PAUSE (modal)

- Tiếp tục (to) · Âm thanh on/off · Rung on/off · Bỏ trận (đỏ, confirm "Mất tiền nhặt trong trận?").

## 12. REVIVE (modal, chết lần 1)

- "HỒI SINH?" + nút to "📺 Xem quảng cáo" + đếm ngược 5s vòng tròn → hết giờ tự qua GameOver.
- Nút "Thôi" nhỏ mờ dưới. 1 lần/run — lần 2 chết thẳng.

## 13. GAME OVER (màn payout — phải "đã" nhất game)

- "HẾT TRẬN" / "THẮNG!" + Wave đạt + so kỷ lục ("KỶ LỤC MỚI!" nếu phá).
- Bảng đếm dồn TỪNG DÒNG (số nảy + tiếng): Coin nhặt → thưởng wave → thưởng lần đầu (nếu có) → Vàng quy đổi.
- XP pass cộng vào thanh pass (thanh chạy).
- Nút: [Chơi lại] (to) · [Về nhà]. Deep-link "Súng mới ở Shop →" khi đủ tiền.

## 14. SETTINGS (modal)

- Âm lượng nhạc / SFX (2 slider) · Rung · Ngôn ngữ (VI/EN) · Khôi phục mua [PLACEHOLDER] · version nhỏ đáy.

## 15. FTUE overlays

- Tooltip nền tối 70%, khoét lỗ sáng quanh thứ cần chạm, mũi tên + 1 câu ("Kéo để chạy!"). 3 cái in-run, 2 cái ở Hub (Gacha, PLAY). Mỗi cái tự tắt khi làm đúng.
