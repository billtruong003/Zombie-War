# Task Breakdown (B6) — Full phase task list for the playable test build

> Đây là bản đầy đủ của B6, thay cho các mô tả ngắn trong task tracker (không persist qua session).
> Đánh dấu `[x]` = đã xong, `[ ]` = chưa làm. Cập nhật file này mỗi khi hoàn thành 1 mục.

---

## Phase 1 — Player core

**Code (đã viết, `Assets/_Project/Scripts/Runtime/`):**
- [x] `Gameplay/PlayerMovement.cs` — Rigidbody top-down movement, auto-aim qua `TargetRegistry`
- [x] `Gameplay/Health.cs` — IDamageable component dùng chung player/zombie
- [x] `UI/VirtualJoystick.cs`
- [x] `Gameplay/CameraFollow.cs` — SmoothDamp follow + trauma-shake (noise texture)
- [x] `Systems/ITargetable.cs`, `Systems/TargetRegistry.cs`, `Systems/NoiseTextureSampler.cs`

**Editor assembly (chưa làm — xem `Docs/EDITOR_SETUP_CHECKLIST.md` mục 1-2, 4, 6):**
- [ ] Chọn 1 character prefab từ `Assets/ThirdParty/Layer Lab/3D Casual Character/3D Characters Pro - Fantasy/Prefabs/` làm soldier
- [ ] Player GameObject: Rigidbody + Animator + Health + PlayerMovement + Weapon + BombThrower, wiring đầy đủ field
- [ ] Animator Controller 2 layer: Base (Locomotion Idle/Run theo `Speed`) + UpperBody (Avatar Mask loại chân, Aim/Fire state)
- [ ] Animation Rigging: RigBuilder + Two-Bone IK tay phải/trái + Multi-Aim Constraint (aim theo `PlayerMovement.AimDirection`) + `WeaponIKTargetUpdater.cs` (script mới, subscribe `Weapon.OnWeaponEquipped` để đổi IK target khi switch súng)
- [ ] Foot IK — có thể hoãn tới khi làm Level 2 (dốc), Level 1 phẳng không bắt buộc
- [ ] Main Camera: gắn `CameraFollow.cs`, gán Target + Noise Texture (`Assets/_Project/Art/Textures/Noise/Noise_Perlin_01.png`)
- [ ] Canvas + VirtualJoystick UI (background/handle Image)

---

## Phase 2 — Weapons (gun + bomb)

**Code (đã viết):**
- [x] `Gameplay/WeaponData.cs`, `WeaponGripPoints.cs`, `Weapon.cs` (hitscan + smoke trail particle + noise recoil), `Bomb.cs`, `BombThrower.cs`

**Editor assembly (chưa làm — xem `Docs/EDITOR_SETUP_CHECKLIST.md` mục 3, 5):**
- [ ] 2 `WeaponData` asset (`WD_Pistol`, `WD_AR`) — số liệu khác rõ rệt (fireRate/damage/range)
- [ ] 2 weapon prefab từ `Low Poly Pistol Weapon Pack 1` + `Low Poly Weapon Pack 4_MW_1` (hoặc ShotGun/VOL.1), mỗi cái thêm `WeaponGripPoints.cs` + 3 child Transform (RightHandGrip/LeftHandGrip/MuzzlePoint)
- [ ] 3 ParticleSystem prefab: muzzle flash, smoke trail (stretched theo trục Z), impact
- [ ] `Bomb` prefab (Bomb.cs + explosion ParticleSystem)
- [ ] Đăng ký SFX key vào `AudioService`: `gun_fire` (x2 nếu muốn âm khác nhau theo súng), `bomb_explode`
- [ ] UI: nút bắn (giữ để bắn liên tục), nút switch súng, nút ném bomb — gọi đúng method (`Weapon.TryFire`, `Weapon.SwitchWeapon`, `BombThrower.TryThrow`)

---

## Phase 3 — Zombie AI + VAT rendering + pooling

Toàn bộ **chưa viết code**. Thứ tự làm:

- [ ] `Gameplay/ZombieData.cs` (ScriptableObject: maxHealth, damage, moveSpeed, attackRange, attackCooldown, `VAT_AnimationData` reference, tên clip Idle/Move/Attack/Hit/Death, `bool isBoss`)
- [ ] `Gameplay/ZombieAI.cs` — state machine Idle/Chase/Attack/Dead (bám theo pattern EchoMage `EnemyBase.cs` đã ghi trong `GAMEPLAY_DESIGN.md` mục 4, nhưng tối giản), implements `IDamageable` + `ITargetable` (để player auto-aim tìm thấy), dùng `VAT_Animator.CrossFade()` khi đổi state
- [ ] Wire 5 `ZombieData` asset trỏ vào baked data có sẵn: `Batty_*`, `Cute_Spider_*`/`Cute_Spider_King_*`, `Whispa_*`, `Prizon_*`, `Lusif*` (`Assets/ThirdParty/VATEnemy/`)
- [ ] 1 `ZombieData` riêng cho boss trỏ `VATEnemy/Boss/SM_Chr_Kaiju_01/02/04_VAT_Data.asset` (dùng ở Phase 6, tạo asset từ bây giờ luôn cho tiện)
- [ ] Dissolve-on-death: material dùng shader Dissolve từ `stylized-toon-world-kit`, animate `_DissolveAmount` qua `BillTween` khi `Health.OnDeath` fire, xong mới trả về pool
- [ ] Pooling: `PoolService.Register()` mỗi `ZombieData` prefab lúc khởi tạo level, `Spawn`/`Return` thay `Instantiate`/`Destroy`
- [ ] `Gameplay/ZombieManager.cs` — quản lý tier theo khoảng cách:
  - Tier xa (ngoài active radius): lerp thẳng về điểm gần player, KHÔNG NavMeshAgent, KHÔNG animation crossfade tốn, throttle update (không mỗi frame)
  - Tier gần: bật full `ZombieAI` Update (NavMeshAgent, animation, attack check)
  - 1 vòng lặp trung tâm kiểm tra khoảng cách toàn bộ zombie theo interval, KHÔNG để mỗi zombie tự check
- [ ] Hit reaction: flash material 1-2 frame + particle máu + nhẹ knockback lùi lại
- [ ] Bake NavMesh cho Level 1 (cần dựng xong ground/obstacle ở Phase 4 trước, hoặc bake tạm trên 1 plane test)

---

## Phase 4 — Wave system + world streaming + Level 1

Toàn bộ **chưa viết code**.

- [ ] `Gameplay/WaveSpawner.cs` — `AnimationCurve spawnRateOverTime` (breathing-room pattern đã chốt ở design doc mục 7), spawn ngoài rìa camera quanh player, chọn `ZombieData` theo mốc thời gian (early/mid/late pool)
- [ ] `Systems/GroundChunk.cs` + `Systems/WorldStreamer.cs` — lưới 3×3 chunk quanh player, pool + reposition khi player băng ranh giới, random obstacle spawn points per chunk
- [ ] Obstacle prefab set (thùng/xe/rào chắn) — lấy free asset có sẵn hoặc primitive tạm, gắn NavMesh Obstacle
- [ ] Level 1 scene: dựng scene mới `Assets/_Project/Scenes/Level1.unity`, đặt Player, WorldStreamer, WaveSpawner, Canvas UI, Main Camera + CameraFollow
- [ ] Level timer 3 phút + flow kết thúc (dừng spawn, hiện kết quả — nối sang Phase 5 UI)

---

## Phase 5 — Juice, audio, UI/HUD polish

- [ ] Hit-stop: coroutine `Time.timeScale = 0.05f` trong ~0.05-0.08s (dùng `WaitForSecondsRealtime`), gọi khi player/zombie trúng đòn nặng
- [ ] Damage popup: `DamagePopup.cs` (TextMeshPro + BillTween di chuyển lên + fade), pool dùng chung 1 prefab
- [ ] HUD: health bar (bind `Health.OnDamaged`), ammo/bomb count (bind `BombThrower.BombsRemaining`), level timer, wave/zombie-count indicator
- [ ] Win/Lose screen (Panel bật khi hết giờ / player chết)
- [ ] SFX đầy đủ qua `AudioService`: nhạc nền loop, switch súng, zombie chết, player trúng đòn
- [ ] Polish particle: tinh chỉnh máu/muzzle/explosion cho "rẻ nhưng đã tay" theo skill

---

## Phase 6 — Level 2 (bonus — chỉ làm nếu Phase 1-5 đã chắc và còn thời gian)

- [ ] `GroundChunk` biến thể dốc (mesh ramp riêng hoặc Terrain blend dùng shader terrain toon-kit)
- [ ] Scene `Level2.unity`, spawn rate nền cao hơn điểm kết Level 1 (leo thang liên tục)
- [ ] Spawn Kaiju boss (`ZombieData` đã tạo ở Phase 3) theo mốc thời gian cuối level
- [ ] Test riêng khả năng leo dốc của `NavMeshAgent` + foot IK (nếu đã làm ở Phase 1)

---

## Phase 7 — End-to-end playtest + polish pass

- [ ] Chơi thử trọn vẹn Level 1 (và Level 2 nếu có) từ đầu đến cuối, không crash/lỗi logic
- [ ] Kiểm tra hiệu năng khi nhiều zombie active cùng lúc (xác nhận tier gating ở Phase 3 thực sự giảm tải)
- [ ] Balance pass: fire rate/damage/spawn curve — chơi thử vài lần, chỉnh số
- [ ] Polish cuối: visual (particle, lighting cơ bản), audio (volume balance), UI (đọc rõ, không đè lên nhau)

---

**Ghi chú:** danh sách này ưu tiên theo đúng thứ tự phụ thuộc (Phase N cần Phase N-1 chạy được trước). Nếu deadline gấp, xem mục 10 trong `GAMEPLAY_DESIGN.md` (thứ tự cắt giảm) để biết bỏ gì trước.
