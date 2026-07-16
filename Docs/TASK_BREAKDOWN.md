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
- [ ] Chọn 1 character prefab từ `Assets/ThirdParty/Layer Lab/3D Casual Character/3D Characters Pro - Fantasy/Prefabs/` làm soldier, set Avatar = **Humanoid** để retarget animation Malbers
- [ ] Import animation từ `Assets/ThirdParty/MalbersHumanAnims/` — set Animation Type = Humanoid trên từng .fbx: `Locomotion/Strafe/*` (8-hướng walk/jog), `Idle/Idle_Combat.fbx` (aim-idle), `Hit/*`, `Deaths/*` (player hit/death feedback, yêu cầu #3)
- [ ] Player GameObject: Rigidbody + Animator + Health + PlayerMovement + Weapon + BombThrower, wiring đầy đủ field
- [ ] Animator Controller 2 layer: Base (Locomotion — blend 8-hướng strafe theo `Speed`+hướng di chuyển) + UpperBody (Avatar Mask loại chân, `Idle_Combat`/Aim-Fire state)
- [ ] Animation Rigging: RigBuilder + Two-Bone IK tay phải/trái (theo quy ước pistol=1 điểm chung / rifle=2 điểm riêng, xem `GAMEPLAY_DESIGN.md` mục 8) + Multi-Aim Constraint (aim theo `PlayerMovement.AimDirection`) + `WeaponIKTargetUpdater.cs` (script mới, subscribe `Weapon.OnWeaponEquipped` để đổi IK target khi switch súng)
- [ ] Chân: Two-Bone IK Constraint + raycast xuống đất, **luôn bật** (không chỉ khi có dốc) — đảm bảo chân luôn chạm đất
- [ ] Main Camera: gắn `CameraFollow.cs`, gán Target + Noise Texture (`Assets/_Project/Art/Textures/Noise/Noise_Perlin_01.png`)
- [ ] Canvas + VirtualJoystick UI (background/handle Image) — có thể dùng sprite từ `Assets/ThirdParty/Layer Lab/GUI Pro-SuperCasual/` cho đẹp hơn Image trắng trơn

---

## Phase 2 — Weapons (gun + bomb)

**Code (đã viết):**
- [x] `Gameplay/WeaponData.cs`, `WeaponGripPoints.cs`, `Weapon.cs` (hitscan + smoke trail particle + noise recoil), `Bomb.cs`, `BombThrower.cs` (throw animation trigger + `releaseDelay` trước khi bomb thực sự spawn)

**Editor assembly (chưa làm — xem `Docs/EDITOR_SETUP_CHECKLIST.md` mục 3, 5):**
- [ ] 2 `WeaponData` asset (`WD_Pistol`, `WD_AR`) — số liệu khác rõ rệt (fireRate/damage/range)
- [ ] Weapon prefab pistol (`Low Poly Pistol Weapon Pack 1`): `WeaponGripPoints.cs` với `rightHandGrip` VÀ `leftHandGrip` trỏ **cùng 1 Transform** (2 tay cùng cầm 1 điểm)
- [ ] Weapon prefab AR/rifle (`Low Poly Weapon Pack 4_MW_1` hoặc ShotGun/VOL.1): `WeaponGripPoints.cs` với `rightHandGrip`/`leftHandGrip` là **2 Transform riêng** (tay súng + tay đỡ báng trước)
- [ ] 3 ParticleSystem prefab (dùng particle từ `Assets/ThirdParty/Epic Toon FX/`, **không dựng mesh**): muzzle flash, smoke trail (stretched theo trục Z), impact
- [ ] `Bomb` prefab (`Bomb.cs` + explosion `ParticleSystem` từ Epic Toon FX — bắt buộc particle-only, xem `GAMEPLAY_DESIGN.md` mục 6), gán Animator trên player + trigger name khớp `BombThrower.throwAnimTrigger` ("Throw"), dùng animation `Assets/ThirdParty/MalbersHumanAnims/Weapons/Throwable/H_Throw_N.fbx` (hoặc chọn hướng phù hợp) làm base, tune `releaseDelay` khớp frame buông tay
- [ ] Đăng ký SFX key vào `AudioService`: `gun_fire` (x2 nếu muốn âm khác nhau theo súng), `bomb_explode`
- [ ] UI: dùng sprite/button từ `Assets/ThirdParty/Layer Lab/GUI Pro-SuperCasual/` — **không có nút bắn** (auto-fire, xem `Weapon.TryAutoFire()`), chỉ cần nút switch súng (`Weapon.SwitchWeapon`) và nút bomb (`BombThrower.TryThrow`)

---

## Phase 3 — Zombie AI + VAT rendering + pooling

**Code (đã viết, `Assets/_Project/Scripts/Runtime/Gameplay/`):**
- [x] `ZombieData.cs` (ScriptableObject: maxHealth, damage, moveSpeed, attackRange, attackCooldown, `VAT_AnimationData` reference, tên clip Idle/Move/Attack/Hit/Death, `bool isBoss`)
- [x] `ZombieAI.cs` — state machine Idle/Chase/Attack/Dead (bám theo pattern EchoMage `EnemyBase.cs`, tối giản hoá), implements `IDamageable` + `ITargetable`, dùng `VAT_Animator.CrossFade()`/`Play()` khi đổi state, dùng lại `Health.cs` (reuse với player) thay vì viết HP riêng, dissolve qua property `_Dissolve` (đã tra đúng tên trong shader nguồn `StylizedDissolve.shader`, không phải `_DissolveAmount` như đoán ban đầu), hit reaction có knockback lùi
- [x] `ZombieManager.cs` — 3 tier (Full/Cheap/Inactive) theo bán kính từ player, re-evaluate theo interval (không mỗi frame), Cheap tier chạy qua `CheapTick()` do manager gọi trực tiếp (không qua Update() riêng của từng zombie)
  - **Lưu ý quan trọng:** Tier "Inactive" KHÔNG dùng `gameObject.SetActive(false)` (sẽ trigger `OnDisable()` → tự unregister khỏi manager → không bao giờ được đánh thức lại) — thay vào đó tắt component (`NavMeshAgent`, `VAT_Animator`, `Renderer`) để đạt cùng hiệu quả "cực kỳ nhẹ" mà vẫn giữ đăng ký. `SetActive(false)` chỉ dùng khi pool thật sự trả về lúc chết.
  - **Hiện tại tier dựa theo bán kính đơn giản** (`fullTierRadius`/`cheapTierRadius`), CHƯA gắn với chunk thật của `WorldStreamer` (Phase 4 chưa viết) — khi Phase 4 xong, cân nhắc thay bằng check "zombie thuộc chunk nào" cho đúng yêu cầu "tự tắt khi player ra chunk xa", hiện tại bán kính lớn đóng vai trò xấp xỉ.

**Chưa làm (cần Editor hoặc Phase 4):**
- [ ] Wire 5 `ZombieData` asset trỏ vào baked data có sẵn: `Batty_*`, `Cute_Spider_*`/`Cute_Spider_King_*`, `Whispa_*`, `Prizon_*`, `Lusif*` (`Assets/ThirdParty/VATEnemy/`)
- [ ] 1 `ZombieData` riêng cho boss trỏ `VATEnemy/Boss/SM_Chr_Kaiju_01/02/04_VAT_Data.asset` (dùng ở Phase 6, tạo asset từ bây giờ luôn cho tiện)
- [ ] Material zombie dùng shader Dissolve từ `stylized-toon-world-kit` (`StylizedDissolve.shader`, property `_Dissolve`)
- [ ] Pooling: `PoolService.Register()` mỗi `ZombieData` prefab lúc khởi tạo level (code `ZombieAI` đã gọi `Bill.Pool?.Return()` khi chết, nhưng chưa có chỗ nào gọi `Bill.Pool.Spawn()`/`Register()` — cần `WaveSpawner.cs` ở Phase 4 làm việc này)
- [ ] 1 `ZombieManager` GameObject đặt trong scene (script tự chạy, không cần config gì thêm ngoài 2 bán kính)
- [ ] Particle máu khi trúng đạn (gắn ở nơi gọi `TakeDamage`, có thể thêm vào `Weapon.cs` khi raycast trúng `IDamageable`)
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
- [ ] HUD: dùng UI kit `Assets/ThirdParty/Layer Lab/GUI Pro-SuperCasual/` — health bar (bind `Health.OnDamaged`), ammo/bomb count (bind `BombThrower.BombsRemaining`), level timer, wave/zombie-count indicator
- [ ] Win/Lose screen (Panel từ GUI Pro, bật khi hết giờ / player chết)
- [ ] SFX đầy đủ qua `AudioService`: nhạc nền loop, switch súng, zombie chết, player trúng đòn
- [ ] Polish particle: dùng thêm variant từ `Assets/ThirdParty/Epic Toon FX/` cho máu/muzzle/explosion, tinh chỉnh cho "rẻ nhưng đã tay" theo skill
- [ ] (Tuỳ chọn, điểm cộng nếu còn thời gian) Tính năng modular character: đổi trang phục/bộ phận cho soldier bằng parts trong Layer Lab pack — tham khảo cách `My project (1)/Assets/Scripts/ModularUtils/CharacterModularApplier.cs` consume đúng format này (không bắt buộc theo đề bài)

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
