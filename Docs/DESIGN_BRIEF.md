# ZombieWar — Design Brief (kim chỉ nam cho designer)

> **Product/design intent only.** Check `HANDOFF.md` before assuming any feature is missing or complete.

> Đọc file này TRƯỚC mọi file khác. Mọi quyết định design phải soi qua đây.

## Game này là gì — 1 câu

Game mobile bắn zombie nhìn từ trên xuống. Chơi 1 tay. Súng **tự ngắm, tự bắn**. Người chơi chỉ lo chạy, ném bom, đổi súng. Sống càng lâu càng tốt, mang tiền về nhà nuôi kho súng.

## Người chơi là ai

- Người chơi mobile casual. Chơi lúc rảnh 5–10 phút. Cầm máy 1 tay, màn dọc 9:16.
- Không đọc hướng dẫn dài. Nhìn phát phải hiểu ngay.

## Điều khiển — CHỈ CÓ 3 INPUT, tuyệt đối không thêm

1. **Joystick** (di chuyển) — ngón cái trái
2. **Nút ném bom**
3. **Nút đổi súng**

Súng TỰ bắn khi có zombie trong tầm. Hết đạn TỰ nạp. **KHÔNG có nút bắn. KHÔNG có nút nạp đạn.** Vẽ thêm 2 nút này = sai.

## Nghệ thuật & màu

- Phong cách: casual cartoon, sạch, tươi. Không gore, không u ám.
- UI skin: dùng bộ `GUI Pro-SuperCasual` (Layer Lab). Không tự chế style khác.
- Bảng màu đã khoá:
  - Nền tối: `#141821`
  - Đỏ `#E5484D`: nguy hiểm, máu, cảnh báo
  - Vàng `#F5B841`: Coin, nút phụ
  - Trắng `#F4F6F8`: chữ
  - **KC (kim cương): cyan riêng biệt + lấp lánh** — cấm dùng chung màu vàng
- **Rarity 5 màu dùng CHUNG cho súng và skin:** ⬜ Xám → 🟩 Xanh lá → 🟦 Xanh biển → 🟪 Tím → 🟧 Cam. Đây là ngôn ngữ màu quan trọng nhất game. Mọi card item đều viền theo màu rarity.

## Chữ

Ngắn. Đời thường. "Nâng cấp" chứ không "Tiến hành nâng cấp". Số to, chữ ít.

## 3 loại tiền — nhớ kỹ vai trò

| Tiền | Màu | Mua gì |
|---|---|---|
| Coin | Vàng | Nâng người (3 dòng), súng rẻ, đồ lẻ |
| Vàng (Gold) | Vàng đậm/bill | Mọi thứ về súng: mua, gacha, lên sao |
| KC (Gem) | Cyan | Skin xịn, gacha skin |

## DO — phải làm

- Mọi màn meta đều có cụm 3 tiền ở góc phải trên
- Card súng/skin luôn hiện: viền rarity + sao (nếu có) + thanh mảnh
- GameOver phải "đã": tiền đếm dồn từng dòng, số nhảy
- FTUE: 3 tooltip trong trận + highlight Gacha → quay ra SMG xanh lá → highlight PLAY
- Nút to, chạm được bằng ngón cái, đáy màn hình là vùng thao tác chính

## DON'T — cấm

- ❌ Nút bắn, nút nạp đạn (game auto)
- ❌ Thêm dòng stat ngoài đúng 3 dòng của tab UPGRADES (Sát thương / Máu / Tốc bắn, 5 nấc)
- ❌ Bịa tiền tệ mới, energy/stamina, lượt chơi
- ❌ Bịa màn hình không có trong SCREEN_FLOW.md
- ❌ Design màn mua Premium Pass (chỉ để nút khoá [PLACEHOLDER])
- ❌ Leaderboard online, PvP, clan (v1 offline, high score cá nhân)
- ❌ Cho nhân vật chỉ số từ costume (costume thuần khoe)
- ❌ Revive bằng KC (chỉ có xem ad, 1 lần)

## Thứ tự đọc docs

1. `DESIGN_BRIEF.md` (file này)
2. `SCREEN_FLOW.md` — có những màn nào, nối nhau ra sao
3. `SCREEN_SPECS.md` — từng màn hiện đúng data gì
4. `ECONOMY_DESIGN.md` — luật tiền/gacha/sao/nội tại
5. `WEAPON_DESIGN.md` — roster súng + chia màu
6. `UIUX_DESIGN_RATIONALE.md` + `UIUX_WIREFRAME_PROMPT.md` — nền tảng cũ + addendum
