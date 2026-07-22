# Food buff system — spec đã duyệt, CHƯA implement

> Chốt với chủ dự án 2026-07-22. Đây là việc ĐẦU TIÊN của phiên sau.
> Mọi con số trong đây là quyết định của chủ dự án, không phải đề xuất — đừng đổi khi chưa hỏi.

## Trạng thái hiện tại (đã commit, đã chạy)

| Có sẵn | Ở đâu |
|---|---|
| Pickup pooled coin/gem/health/bomb, magnet bay về Player, collect-once | `Runtime/Gameplay/Pickups/Pickup.cs`, prefab `Resources/Pools/pickup_*` |
| `PickupEffect` enum: `Currency / Health / Bomb` | `Pickup.cs` |
| `Health.Heal(amount)` — clamp max, từ chối hồi xác chết, event `OnHealed` | `Runtime/Gameplay/Health.cs` |
| Thùng loot + barrel nổ, rớt coin/gem/health/bomb theo bảng tỉ lệ | `Runtime/Gameplay/Pickups/DestructibleProp.cs` |
| `BombPickedUpEvent` — **ĐÃ BẮN nhưng CHƯA AI NGHE** | `Pickup.cs` → cần `BombThrower` subscribe +1 charge |

## Spec 4 món ăn (model KayKit có sẵn)

| Model | Hiệu ứng | Cơ chế chốt |
|---|---|---|
| `Food_Apple_Green` | **Hồi máu** | Tức thời, 1 phát. Dùng `Health.Heal()` có sẵn |
| `Food_Berry_Blue` | **Shield** | **KHÔNG thời hạn** — ăn là giữ, dùng mới hết. Cap **150**. Mỗi cục **+10–15**. Hấp thụ **100%** trước máu: dmg 12 / shield 10 → shield 0, máu −2 |
| `Food_Apple_Red` | **Vô hạn đạn** | ~8 s, không trừ đạn, không reload. **Snapshot số đạn lúc ăn, hết giờ trả về ĐÚNG số đó** (không phải full mag) |
| `Food_Cheese` | **x2 SỐ COIN rơi** | ~20 s (nhân số lượng, không nhân tỉ lệ) |

⚠️ **Việc sửa đầu tiên:** `pickup_health.prefab` hiện dùng `Food_Apple_Red` — phải đổi sang
`Food_Apple_Green` (đỏ dành cho vô hạn đạn).

## Kiến trúc đã chốt

- **Shield KHÔNG phải buff** (không có thời hạn) → là tài nguyên, sống cạnh `Health` trên Player
  (field hoặc component `PlayerShield` bên cạnh). `Health.TakeDamage` (hoặc wrapper ở
  `PlayerController`) trừ shield trước, tràn phần dư sang máu.
- **`PlayerBuffs` component** chỉ quản 2 buff CÓ GIỜ: InfiniteAmmo, CoinBoost. Tick đếm ngược,
  event khi bật/tắt để HUD nghe. Không poll.
- **Vô hạn đạn** hook trong `Weapon.cs`: đang buff → không trừ `_ammoInMag`, không vào reload.
  Snapshot/restore qua buff start/end.
- **Cheese** hook trong `PickupManager.OnZombieKilled`: đang buff → nhân đôi lượng coin mỗi drop.
- **HUD** (prefab, sửa thoải mái): xếp dọc **máu → shield (xanh nước biển, khởi đầu 0) → hàng ô
  buff**. Mỗi ô = `Image` vuông + child `Text` tên ("SHIELD"/"AMMO"/"COIN") — chủ dự án sẽ tự lắp
  icon rồi tắt text sau. Manager chỉ bật/tắt ô theo buff đang chạy.
- 4 prefab pickup mới/sửa từ đúng 4 model trên, gắn `Pickup` với effect tương ứng (cần thêm
  effect `Shield`, `InfiniteAmmo`, `CoinBoost` vào enum).
- Thêm các món này vào bảng loot của `DestructibleProp` (tỉ lệ thấp).
- Pool qua `Bill.Pool` như hiện tại. Test: shield trừ đúng thứ tự + tràn dư, snapshot/restore đạn,
  buff hết hạn, cheese nhân đúng, `BombPickedUpEvent` +1 charge.

## Thứ tự làm đề xuất

1. Đổi model `pickup_health` sang táo xanh.
2. Shield (resource + TakeDamage order + test) — nặng nhất, làm khi đầu óc tỉnh.
3. `PlayerBuffs` + vô hạn đạn (hook Weapon + snapshot test).
4. Cheese hook PickupManager.
5. `BombThrower` nghe `BombPickedUpEvent`.
6. 4 prefab pickup + thêm vào loot table.
7. HUD: thanh shield + hàng ô buff.
