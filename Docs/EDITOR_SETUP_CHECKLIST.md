# Editor Assembly Checklist — Phase 1 & 2 (Player + Weapons)

Code cho Phase 1/2 đã viết xong (`Assets/_Project/Scripts/Runtime/Gameplay/` + `Systems/` + `UI/`). Phần này liệt kê chính xác những gì cần lắp trong Unity Editor để chạy được — không cần đọc code trước, làm theo thứ tự là chạy.

## 1. Noise texture cho Shake/Recoil (làm trước tiên — 2 script đang chờ field này)

- Import 1 texture noise (Perlin/Simplex strip, RGB) vào `Assets/_Project/Art/Textures/`.
- Gán vào field `Noise Texture` trên:
  - `CameraFollow.cs` (sẽ gắn ở bước 4)
  - `Weapon.cs` → field `Recoil Noise Texture` (bước 3)

## 2. Player GameObject

1. Tạo empty GameObject `Player` tại gốc scene, thêm:
   - `Rigidbody` (Freeze Rotation X/Z, Use Gravity tuỳ địa hình — top-down nên có thể tắt gravity + constrain Y nếu muốn nhân vật không rơi qua dốc).
   - `Animator` (Controller sẽ tạo ở bước 2b).
   - `Health.cs`
   - `PlayerMovement.cs` — kéo `VirtualJoystick` (bước 5) vào field `Joystick`, kéo `Animator` vào field `Animator`.
   - `Weapon.cs` — field `Weapon Socket` = 1 child Transform rỗng đặt đúng vị trí tay cầm súng (tạm thời, IK sẽ tinh chỉnh sau ở bước 6); field `Weapons` = danh sách 2 `WeaponData` asset (bước 3); field `Recoil Noise Texture` = texture ở bước 1.
   - `BombThrower.cs` — field `Bomb Prefab` = prefab Bomb (bước 3d), field `Throw Origin` = child Transform trước ngực/tay player.
2. **Animator Controller** (2 layer theo yêu cầu #3):
   - Layer 0 "Base" (full body): Blend Tree hoặc 2 state Idle/Run theo param `Speed` (float, `PlayerMovement.cs` đã set giá trị này mỗi frame).
   - Layer 1 "UpperBody" (Avatar Mask loại trừ chân, weight = 1): state Aim/Fire — trigger animation bắn khi `Weapon.TryFire()` được gọi (thêm 1 dòng `animator.SetTrigger("Fire")` trong `Weapon.cs` nếu muốn — hiện code chưa có, dễ thêm sau).
3. Input tạm thời (chưa có UI button thật): có thể test bằng 1 script debug gọi `weapon.TryFire(playerMovement.AimDirection)` khi bấm phím, trước khi nối vào UI thật ở bước 5.

## 3. Weapon assets

1. Tạo 2 `WeaponData` asset: `Assets/_Project/ScriptableObjects/Configs/WD_Pistol.asset`, `WD_AR.asset` (`Create > ZombieWar > Weapon Data`).
2. Với mỗi asset: gán `Weapon Prefab` = 1 prefab súng lấy từ `Assets/ThirdParty/Low Poly Pistol Weapon Pack 1/` hoặc `Low Poly Weapon Pack 4_MW_1/` (chọn 2 model khác biệt rõ fire rate/recoil).
3. Trên **mỗi prefab súng**, thêm component `WeaponGripPoints.cs` + tạo 3 child Transform rỗng đặt đúng vị trí: `RightHandGrip`, `LeftHandGrip` (dùng cho IK ở bước 6), `MuzzlePoint` (đầu nòng, dùng để bắn raycast + spawn muzzle flash).
4. Set số liệu (`fireRate`, `damage`, `range`) khác nhau rõ rệt giữa 2 súng để cảm nhận được sự khác biệt khi switch.
5. Gán `Muzzle Flash Prefab` / `Smoke Trail Prefab` / `Impact Prefab` — 3 `ParticleSystem` prefab riêng (particle khói mô tả đường đạn = 1 particle system kéo dài theo scale Z, `Weapon.cs` tự set `localScale.z` theo khoảng cách bắn trúng, chỉ cần dựng particle dạng "kéo dài theo trục Z" trong Editor, VD: 1 stretched billboard/quad hoặc trail-shaped particle).
6. Tạo prefab `Bomb` riêng: GameObject rỗng + `Bomb.cs`, gán `Explosion Prefab` (particle nổ), set `Explosion Sfx Key`/`Fuse Time`/`Radius`/`Damage`. Gán prefab này vào `BombThrower.Bomb Prefab` ở Player.

## 4. Camera

1. Trên Main Camera: thêm `CameraFollow.cs`.
2. `Target` = Player transform, `Noise Texture` = texture ở bước 1.
3. Chỉnh `Offset`/`Look Euler Angles` trong Scene view cho tới khi thấy rõ player + đủ tầm nhìn zombie xung quanh (mặc định `(0,12,-8)` + góc `60°` — chỉ là điểm khởi đầu, cần tune bằng mắt).

## 5. UI Canvas (virtual joystick + nút bắn/switch/bomb)

1. Canvas (Screen Space - Overlay) + EventSystem (Unity tự tạo nếu chưa có).
2. Joystick: 1 `Image` nền (background) + 1 `Image` con (handle), gắn `VirtualJoystick.cs` lên background, kéo 2 RectTransform vào field tương ứng.
3. Nút bắn: có thể dùng `OnPointerDown`/`OnPointerUp` custom hoặc đơn giản 1 `Button` gọi `Weapon.TryFire(playerMovement.AimDirection)` liên tục khi giữ (cần 1 script nhỏ kiểm tra "đang giữ" — hoặc tạm thời cho bắn tự động khi có zombie trong tầm, tuỳ bạn quyết định UX).
4. Nút switch súng: `Button.onClick` → `Weapon.SwitchWeapon()`.
5. Nút bomb: `Button.onClick` → `BombThrower.TryThrow(playerMovement.AimDirection)`.

## 6. Animation Rigging (IK — theo quyết định B5, mục 8 trong GAMEPLAY_DESIGN.md)

Chưa có script tự động cho phần này (đúng theo quyết định: tự làm trong Editor bằng package có sẵn, không viết code custom):

1. Player Animator cần **Humanoid hoặc Generic rig** đã có sẵn từ model soldier.
2. Thêm `Rig Builder` component trên Player (hoặc trên 1 child object chứa rig), tạo 1 `Rig` con.
3. Trong `Rig`: thêm `Two-Bone IK Constraint` cho tay phải (Root=Upper Arm, Mid=Forearm, Tip=Hand, Target = `WeaponGripPoints.RightHandGrip` của súng đang cầm), tương tự cho tay trái. `Weapon.cs` đã có `event Action<WeaponGripPoints> OnWeaponEquipped` bắn ra mỗi khi đổi súng — viết 1 script nhỏ `WeaponIKTargetUpdater.cs` subscribe event này, gán `rightHandConstraint.data.target = gripPoints.RightHandGrip` (tương tự left) mỗi lần đổi súng.
4. Thêm `Multi-Aim Constraint` trên xương spine/chest, source object = 1 Transform trống di chuyển theo `PlayerMovement.AimDirection` (cần 1 script nhỏ đặt vị trí target đó = `transform.position + AimDirection * const`, hoặc dùng ngay Transform của zombie/aim point).
5. Chân: `Two-Bone IK Constraint` + `Rig` riêng, nếu chưa làm Level 2 (dốc) thì có thể để sau — không bắt buộc cho Level 1 mặt phẳng.

## 7. NavMesh

Bake NavMesh cho scene Level 1 (`Window > AI > Navigation`) trước khi test `NavMeshAgent` trên zombie (Phase 3, chưa viết).

---

Sau khi lắp xong theo checklist này, chạy thử: player di chuyển bằng joystick, xoay đúng hướng, bắn ra particle khói + tiếng súng + giật nhẹ theo noise, ném bomb nổ có damage/shake. Báo lại nếu có lỗi hoặc chỗ nào chưa rõ.
