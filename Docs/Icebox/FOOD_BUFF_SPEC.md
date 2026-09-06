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
- **HUD**: chi tiết đầy đủ ở mục "UI spec" bên dưới — đọc mục đó trước khi đụng HudInstaller.
- 4 prefab pickup mới/sửa từ đúng 4 model trên, gắn `Pickup` với effect tương ứng (cần thêm
  effect `Shield`, `InfiniteAmmo`, `CoinBoost` vào enum).
- Thêm các món này vào bảng loot của `DestructibleProp` (tỉ lệ thấp).
- Pool qua `Bill.Pool` như hiện tại. Test: shield trừ đúng thứ tự + tràn dư, snapshot/restore đạn,
  buff hết hạn, cheese nhân đúng, `BombPickedUpEvent` +1 charge.

## UI spec — thanh shield + hàng ô buff

### Cách HUD hiện tại được dựng (đọc trước, đừng dựng tay)

HUD là prefab-first nhưng **được author bằng `HudInstaller.Build()`**
(`Assets/_Project/Scripts/Editor/UI/HudInstaller.cs`, menu
`ZombieWar/UI/Authoring/Rebuild HUD Map_Level1 (Destructive)...`). Installer idempotent: xoá các
widget cũ theo tên rồi dựng lại, giữ nguyên `Joystick_BG`. Mọi widget dựng qua `UIKit` (`K.Image`,
`K.Text`, `K.Place`, anchor enum `A.TL/TC/TR...`), toạ độ **grid 48**, wire vào `HudController`
bằng `K.Wire(soCtrl, "fieldName", obj)`.

→ Thêm UI mới = **sửa `HudInstaller`** (thêm tên widget mới vào danh sách dọn cũ ở đầu `Build()`),
không kéo tay trong scene. Sau khi sửa installer, chạy lại menu Rebuild cho cả 5 map scene
(installer hiện chỉ build Map_Level1 — cân nhắc chạy vòng cho Map_Level2..5 hoặc prefab-hoá HUD).

### Layout cột trái trên (dưới HP hiện có)

HP pill hiện tại: TL `(48, −48)`, size `400×36`, fill xanh lá, label trong bar.

| Widget | Anchor | Vị trí | Size | Ghi chú |
|---|---|---|---|---|
| `ShieldBar` (track) | TL | `(48, −92)` | `400×24` | Pill, nền `Surface2` alpha 0.9 như HpBar |
| `ShieldBar/Fill` | stretch | inset 4 | — | Màu **xanh nước biển** `new Color32(30,144,255,255)` (không có sẵn trong UITheme — thêm `UITheme.Shield`) |
| `ShieldLabel` | TL | `(48, −92)` | `400×24` | "0/150", MidlineRight, size 20, trắng |
| `BuffRow` | TL | `(48, −132)` | `72×72` mỗi ô, spacing 12, HorizontalLayoutGroup, trải phải | Container trống khi không buff |

Quy tắc hiển thị:
- Shield = 0 → **ẩn cả track** (`ShieldBar` + label SetActive(false)) — người mới chưa ăn berry
  không cần biết hệ này tồn tại. Shield > 0 → hiện, fill scale theo `current/150`.
- Shield fill dùng đúng pattern HpBar: scale `anchorMax.x` của Fill (xem `HudController` xử lý
  `healthFillRect`), KHÔNG dùng `Image.fillAmount` (pill sprite 9-slice méo khi fill).

### Cấu trúc một ô buff (`BuffTile`)

```text
BuffTile_<Kind>            Image vuông Rounded24, nền Surface 0.85
├── CooldownFill           Image, fill từ dưới lên (Image.type=Filled, Vertical, fillAmount = timeLeft/duration)
│                          màu trắng alpha 0.25 — nhìn thấy buff còn bao lâu mà không cần chữ số
├── Icon                   Image, inset 8, MẶC ĐỊNH disabled + sprite None
└── Label                  TMP, size 18, Bold, giữa ô: "AMMO" / "COIN" (shield không có tile — nó là thanh)
```

- Tile được **dựng sẵn cả 2 cái** (InfiniteAmmo, CoinBoost) trong installer, mặc định inactive.
  Manager chỉ `SetActive` + cập nhật `CooldownFill.fillAmount` — không Instantiate lúc runtime
  (đúng luật "no runtime-built permanent UI").
- Khi chủ dự án có icon: gán sprite vào `Icon`, bật `Icon`, tắt `Label` — đúng flow đã chốt.

### Icon lấy đâu ra (2 phương án)

1. **Tạm thời (ship được ngay):** để `Label` text như trên, `Icon` disabled. Zero việc.
2. **Tự sinh từ model KayKit** — dự án đã có tiền lệ (`CasualIconGenerator` 323 icon,
   `PrefabContactSheet` render có nhãn): viết `FoodIconGenerator` render 4 model
   (`Food_Apple_Green/Berry_Blue/Apple_Red/Cheese` + có thể barrel/crate) thành PNG 256×256 nền
   trong suốt vào `Assets/_Project/UI/Icons/Generated/Buffs/`, import Sprite, gán vào tile +
   pickup card. ~1 buổi, làm sau khi gameplay chạy.

### Wire vào HudController (field mới, additive)

```csharp
[SerializeField] RectTransform shieldFillRect;   // scale anchorMax.x như healthFillRect
[SerializeField] GameObject    shieldRoot;       // ẩn/hiện cả cụm
[SerializeField] TMP_Text      shieldLabel;      // "75/150"
[SerializeField] BuffTileView[] buffTiles;       // 2 phần tử; view nhỏ: kind + refs bg/icon/label/cooldownFill
```

`HudController` subscribe: shield-changed event (từ component shield) + buff start/tick/end (từ
`PlayerBuffs`). Đừng poll trong Update — HUD hiện tại đã theo pattern event/callback.

### Danh sách dọn cũ trong installer

Thêm `"ShieldBar", "ShieldLabel", "BuffRow"` vào mảng tên ở đầu `HudInstaller.Build()` để rebuild
không nhân đôi widget.

## Thứ tự làm đề xuất

1. Đổi model `pickup_health` sang táo xanh.
2. Shield (resource + TakeDamage order + test) — nặng nhất, làm khi đầu óc tỉnh.
3. `PlayerBuffs` + vô hạn đạn (hook Weapon + snapshot test).
4. Cheese hook PickupManager.
5. `BombThrower` nghe `BombPickedUpEvent`.
6. 4 prefab pickup + thêm vào loot table.
7. HUD theo "UI spec" ở trên: sửa `HudInstaller` (shield bar + 2 buff tile + wire HudController),
   rebuild HUD cho cả 5 map scene.
8. (Tuỳ chọn, sau cùng) `FoodIconGenerator` sinh icon từ model KayKit, gán vào tile, tắt label.
