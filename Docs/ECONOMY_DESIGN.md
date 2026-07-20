# ZombieWar — Economy & Progression Design (v2.0)

> **Proposed design, not implemented backend.** The next phase must reconcile these values with the
> real 25-weapon roster and create an authoritative wallet/profile before wiring purchases.

> v2.0 — chốt hệ Rarity 5 màu + Sao súng + nội tại, tab UPGRADES nhỏ, Battle Pass free track.
> Asset đã verify: `Assets/KayKit/Packs/Bits/KayKit - Resource Bits (for Unity)/Prefabs/` (Money_*, Gem_*, Gems_*), UI skin: `Assets/ThirdParty/Layer Lab/GUI Pro-SuperCasual`.

---

## 0. Nguyên tắc xương sống

- Game **auto-aim + auto-fire**. KHÔNG có nút bắn, KHÔNG có nút reload (hết băng tự nạp). Input thật chỉ có: joystick di chuyển, nút ném bom, nút đổi súng.
- Power nằm ở **SÚNG**, không nằm ở nhân vật (trừ tab UPGRADES nhỏ cố định). Costume thuần cosmetic, zero chỉ số.
- Mỗi đồng tiền có đúng 1 việc: **Coin → nâng người + súng rẻ · Vàng → mọi thứ về súng · KC → skin/gacha skin**.

## 1. Tiền tệ (3 loại + XP)

| Currency | Icon nguồn | Độ hiếm | Dùng để | Nguồn thu |
|---|---|---|---|---|
| **Coin (Tiền)** | KayKit `Money_Single` / `Money_Coins_Stack_*` | Common | Tab UPGRADES (3 dòng), súng Xám/Xanh lá, costume part thường, mở slot 3 | Drop in-run (zombie thường), payout cuối run, quest |
| **Vàng (Gold)** | KayKit `Money_Bill*` (tint vàng) | Uncommon | **Chuyên về súng**: mua súng Xanh biển+, gacha súng, lên sao | Elite/boss drop, milestone wave, quest, battle pass |
| **KC (Gem)** | KayKit `Gem_Small/Medium/Large` | Rất hiếm — 1–2 viên/run là hên, cap mềm 3 | Skin set cao cấp (200/500/1000 KC), gacha skin | Drop cực hiếm, achievement, battle pass, **IAP hook sau** |
| XP | — | Per-run | Level-up chọn perk in-run, reset khi chết | Kill |

## 2. Drop in-run

- Prefab pooled qua `Bill.Pool`, model KayKit Resource Bits.
- Zombie thường: Coin 1–3. Elite/Ranged: + Vàng nhỏ. Boss: Vàng chắc chắn + roll KC.
- Magnet radius (perk) hút drop; hết wave auto-hút toàn map.

## 3. Payout cuối run

```
CoinBonus  = wave*10 + kills*0.5   (cộng vào Coin đã nhặt)
Score      = time(s) + kills*10 + wave*100
```
GameOver: breakdown 3 dòng đếm dồn (nhặt trong trận / bonus wave / bonus kill) + dòng KC riêng nếu có.

## 4. HỆ SÚNG — Rarity 5 màu + Sao + Nội tại (CORE của game)

### 4.1 Rarity = field `tier` có sẵn trong WeaponData

| Tier | Màu | Mua bằng | Vai trò | Nội tại |
|---|---|---|---|---|
| T1 | ⬜ Xám | Coin | Súng lính đầu game, max sao rẻ | ❌ |
| T2 | 🟩 Xanh lá | Coin | Phổ thông | ❌ |
| T3 | 🟦 Xanh biển | Vàng | Khá | ❌ |
| T4 | 🟪 Tím | Vàng / gacha | Hiếm | ✅ bản YẾU |
| T5 | 🟧 Cam | Gacha / gold-only | Chase item | ✅ bản FULL |

Skin dùng CHUNG 5 màu này → cả game 1 ngôn ngữ rarity.

### 4.2 Sao = chỉ số (mọi súng như nhau)

| Sao | Cần | Được |
|---|---|---|
| ★1 | Sở hữu | Súng gốc |
| ★2 | Mảnh + Vàng (bảng 4.4) | +15% damage |
| ★3 | Mảnh + Vàng | +15% nữa |

Nội tại KHÔNG phụ thuộc sao — Tím/Cam có từ lúc nhận súng.

### 4.3 Nội tại theo HỌ (6 họ) — khớp cơ chế auto-fire đã verify trong code

| Họ | 🟪 Tím | 🟧 Cam | Code hook |
|---|---|---|---|
| Pistol | Đổi súng nhanh +50%, viên đầu sau đổi ×1.5 | Nhanh ×2, viên đầu ×2 | `Weapon.SwitchWeapon` |
| SMG | Đang di chuyển → +10% tốc bắn | +20% | nhân fireRate trong `TryFire` khi velocity > 0 |
| Rifle | Auto-fire liên tục 2s → +15% tốc bắn tới khi ngừng | +30%, kích hoạt sau 1.5s | ramp timer |
| Shotgun | Đạn xuyên +1 zombie | Xuyên +2 | `pierceCount` (đã có #54) |
| Sniper | Đạn xuyên 3 mục tiêu | Xuyên cả hàng (999) | `pierceCount` |
| LMG | Đứng yên → băng tự nạp dần (chậm) | Nạp nhanh gấp đôi | regen `_ammoInMag` khi velocity ≈ 0 |

### 4.4 Mảnh (shard) — per súng

| Tier | ★2 / ★3 cần | 1 dupe gacha = | Gacha rate |
|---|---|---|---|
| Xám | 10 / 25 | 10 mảnh | 40% |
| Xanh lá | 15 / 35 | 10 | 30% |
| Xanh biển | 20 / 50 | 12 | 18% |
| Tím | 30 / 75 | 15 | 9% |
| Cam | 40 / 100 | 20 | 3% |

Mảnh còn rớt từ: quest daily/weekly + battle pass free track (người không gacha vẫn lên sao được, chậm hơn).

### 4.5 Sở hữu & slot

- **Start: chỉ Pistol** (slot 1, không tháo — chỉ thay bằng pistol khác).
- Slot 2 free. **Slot 3 mở bằng Coin lớn hoặc milestone wave 10.** Slot bom riêng.
- Giá mua thẳng = `basePrice × tierMult` (WEAPON_DESIGN). Vài khẩu Cam chỉ ra từ gacha.

## 5. Tab UPGRADES (nâng nhân vật — KHOÁ CỨNG 3 dòng, không được phình)

| Dòng | 5 nấc, mỗi nấc | Giá nấc 1→5 (Coin) |
|---|---|---|
| 🗡 Sát thương | +4% | 100 / 250 / 500 / 1.000 |
| ❤ Máu tối đa | +4% | 100 / 250 / 500 / 1.000 |
| ⚡ Tốc bắn | +4% | 100 / 250 / 500 / 1.000 |

Full cả 3 dòng = 5.550 Coin, +16% mỗi loại. Đây là "nền rẻ tiền", KHÔNG phải hệ chính. Cấm thêm dòng mới.

## 6. Gacha (2 banner)

| Banner | Currency | Pool | Pity |
|---|---|---|---|
| **Gacha Súng** | Vàng (x1/x10) | Súng theo rate 4.4 | 30 pull chưa ra Tím+ → guarantee |
| **Gacha Skin** | KC | Costume part/set theo rarity | tương tự |

- Dupe súng → mảnh (4.4). Dupe skin → refund KC %.
- Rate hiển thị công khai trên màn gacha.
- **FTUE rigged pull: lần quay đầu tiên auto ra SMG XANH LÁ + tự trang bị** (không cho Tím sớm, giữ chase).

## 7. Costume (modular Layer Lab) — thuần cosmetic

**Free:** Hair ~4–5 · Face (mouth/eye/brow) 4–5 · Beard: default "không râu" + vài kiểu free; **màu râu/tóc = option riêng trong cùng category** (~4–5 màu).
**Default (có thể thay, không được trần truồng):** Chest `61–66` · Leg `62–67` · Feet `1,2,3,4,6,7,55` · Head `38,53,55`. Ngoài danh sách: mua hết.
**Bán:** part lẻ = Coin/Vàng theo rarity 5 màu; **SET full-body chỉ KC: 200 / 500 / 1000**; vài set gacha-only, vài set achievement-only (prestige).

## 8. Battle Pass — FREE track ngay, premium để sau

- 1 track free ~30 bậc/season. Reward: mảnh súng, Vàng, KC lẻ, costume part, gacha ticket.
- Điểm pass từ **quest daily/weekly** — quest thiết kế để dạy dùng nhiều súng ("giết 500 zombie bằng Shotgun").
- Premium track: `[PLACEHOLDER]` — hiện khoá, KHÔNG design màn mua.

## 9. In-run perk vs meta (giữ nguyên)

In-run: level-up chọn 1/3 card (%, mất khi chết). Meta: tab UPGRADES + sao súng (vĩnh viễn). UI phải nhìn khác hẳn (card vs bar).

## 10. Bomb & Revive & Ammo

- Bomb: consumable per-run — start 2, +1 mỗi 5 wave, drop hiếm từ elite. Không mua mid-run.
- Revive: 1 lần/run, rewarded ad `[PLACEHOLDER]`, countdown 5s + Skip. KHÔNG revive bằng KC.
- Ammo: **vô hạn + magazine/reload tự động** ("gai đạn" là nhịp cố ý). `ammoType` chỉ là flavor.

## 11. Modes

- **v1: Endless offline** — xem ai trụ lâu, high score lưu PlayerPrefs. Không leaderboard online.
- Planned (KHÔNG design bây giờ): Weekly fixed-loadout leaderboard, mode khác.

## 12. FTUE

1. First launch → trận tutorial (pistol), 3 tooltip contextual: di chuyển / súng TỰ bắn / ném bom. Dismiss bằng hành động.
2. Hết wave 3 → về Hub, highlight Gacha → **rigged pull SMG Xanh lá auto-equip**.
3. Highlight PLAY. Hết FTUE.

## 13. Implementation notes

- `WalletService` (Bill service): Coin/Vàng/KC + `OnCurrencyChanged`, persist cùng `LoadoutState`.
- Ownership per súng: `{owned, starLevel, shards}` — persist.
- Nội tại: đọc từ tier + class trong `Weapon.cs`, 6 hook ở bảng 4.3.
- Drop: `PickupBase` + magnet, pool `Bill.Pool`, prefab KayKit.
- Catalog costume thêm: `priceCurrency, price, isDefault, acquire(shop/gacha/achievement), rarity`.
