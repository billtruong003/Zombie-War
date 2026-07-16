# Zombie War — Gameplay Design Document (B1)

> Trạng thái: **DRAFT — chờ duyệt.** Tài liệu này chốt xong mới sang B2 (setup project + push git).
> Triết lý xuyên suốt: **Simple is Key** (topdown-zombie-survival-designer skill) — chọn giải pháp ít component nhất, ít class nhất, ưu tiên built-in Unity/Cinemachine, chỉ dùng ScriptableObject khi thực sự cần data-driven (súng, zombie type).

---

## 0. Đề bài → phạm vi thật sự

Đề bài có 8 yêu cầu cứng (camera/control/soldier/zombie/gun/bomb/level/audio). Deadline 7 ngày, chấm 80% theo bài test + 20% phỏng vấn. Kết luận về scope:

- **Không phải multiplayer, không phải RPG progression** — đúng phạm vi skill "top-down zombie survival, singleplayer".
- Level 2 (dốc + zombie khổng lồ) là **điểm cộng, không bắt buộc** — theo đúng nguyên tắc skill, ta làm chắc Level 1 + toàn bộ pipeline (gun/bomb/juice/audio) trước, Level 2 là extension thêm vào cuối nếu còn thời gian, không đánh đổi chất lượng Level 1 để có Level 2.
- Giám khảo nhìn vào **visual + độ mượt (juice)** ngang hàng với việc "đủ tính năng" — nên trong lịch 7 ngày, không dồn hết thời gian vào feature mới cuối cùng mà phải chừa buffer polish.

---

## 1. Nguồn asset & lý do dùng — quyết định kiến trúc cấp cao

Đây là phần quan trọng nhất của B1 vì nó quyết định toàn bộ kiến trúc code phía sau. Đã khảo sát trực tiếp 3 project nguồn, không đoán:

> **Sửa lại (sau B4):** ban đầu tôi khảo sát nhầm `ShadersLab-BillShader` làm nguồn VAT/enemy. Bạn đã xác nhận nguồn đúng là **`D:\Project\My project (1)`** (dự án "EchoMage" — top-down wave-survivor roguelite). Đã re-copy lại `Assets/ThirdParty/VAT` từ đúng project này (bản đầy đủ hơn, thêm `VAT_Animator_Instanced.cs`, `VAT_Ghost.shader`, `VAT_Toonlit.shader`, `BuildSizeOptimizer.cs`/`TextureOptimizer.cs`), và mang thêm cả **`Assets/ThirdParty/VATEnemy`** — kho baked VAT data đã có sẵn (~170MB, LFS): `Batty_*`, `Cute_Spider_*`/`Cute_Spider_King_*`, `Whispa_*`, `Prizon_*`, `Lusif*`, và đặc biệt **`Boss/SM_Chr_Kaiju_01/02/04_VAT_Data.asset`** (30-42MB mỗi file) — khớp thẳng vào yêu cầu #7 "zombie khổng lồ" Level 2, không cần tự bake gì cả. Có cả `GhostCompanion/Ghost_VAT_Data.asset` (không dùng, thuộc cơ chế "echo" riêng của EchoMage).

| Nguồn | Cái gì | Trạng thái thực tế sau khảo sát | Quyết định |
|---|---|---|---|
| `My project (1)` ("EchoMage") | VAT bake tool + shader + baked enemy data (URP, Unity 6000.2.7f2) | `Assets/Layer Lab/Shaders/VAT/` là **package độc lập hoàn toàn** (cùng họ "BillTheDev" VAT, bản đầy đủ hơn ShadersLab-BillShader). `Assets/VATEnemy/` chứa data đã bake sẵn cho ~7 loại quái + 1 boss Kaiju. Kiến trúc enemy tham khảo: `Scripts/Enemy/EnemyBase.cs` (namespace `EchoMage.Enemies`, implements `IPoolableObject, IDamageable`) + `EnemyStats.cs` SO, state machine `Spawning/Idle/Chasing/Attacking/Stunned/Dead` gọi `VatAnimator.CrossFade()`, dùng `ObjectPoolManager` (custom pool, KHÔNG phải BillGameCore) riêng của project đó. Subclass theo hành vi: `MeleeEnemy.cs`, `SpeedEnemy.cs`, `BossEnemy.cs` (combo-attack timing), `RangedEnemy.cs` — đúng nguyên tắc "chỉ tách class khi hành vi thực sự khác". | **Copy nguyên `VAT/` + `VATEnemy/` sang** (đã làm lại ở B4). **Học kiến trúc `EnemyBase`/`EnemyStats`/pooling, không copy nguyên văn** — namespace `EchoMage.*` gắn với game khác, và có state "Stunned"/"Spawning" không cần cho zombie đơn giản. Viết lại bản tối giản theo state machine 4 trạng thái (Idle/Chase/Attack/Dead) như đã chốt, nhưng tái dùng ý tưởng interface `IPoolableObject`/`IDamageable` nếu BillGameCore chưa có sẵn tương đương. Cũng tham khảo thêm (không copy) các script gameplay khác của project này cho B5: `TopDownCamera.cs`, `Combat/OrbShooter.cs` (auto-aim shooter), `GamePlay/EnemySpawner.cs` + `WaveData.cs`, `GamePlay/DifficultyManager.cs`, `GamePlay/GameManager.cs` — cùng thể loại top-down wave-survivor nên kiến trúc rất sát với đề bài. |
| `TOSSZONE` | BillGameCore (`com.bill.gamecore` v3.0.0, Unity 6000.3.8f1) | Core Runtime (`ServiceLocator`, `Bill` facade, `BillTween`, `PoolService`, `EventBus`, `AudioService`, `TimerService`, `SceneService`, `GameStateMachine`) **không có asmdef riêng** (biên dịch vào default assembly) và **phụ thuộc cứng vào `BillInspector`** (attribute `[BillTitle]`, `[BillRequired]`...) dùng khắp nơi. `NetworkService`/Fusion bị gate bởi `#if PHOTON_FUSION` — an toàn, không kéo theo nếu không định nghĩa symbol đó. | **Mang cả `BillGameCore` + `BillInspector` sang nguyên khối** (B3) — không tách lẻ, vì core phụ thuộc cứng vào BillInspector nên tách sẽ vỡ. Đây vẫn là lựa chọn đơn giản nhất (nguyên tắc "code sẵn có trong project" của expert-developer skill) hơn là viết lại BillTween/Pool/EventBus từ đầu. Sẽ **không** bật `PHOTON_FUSION` — networking không dùng. |
| `TOSSZONE` — súng | Không có package súng nào trong `manifest.json` (đã kiểm tra kỹ — không tồn tại "gun package qua manifest" như giả định ban đầu). Súng thực chất là các asset-store folder thô (`Low Poly Pistol/AR/ShotGun Weapon Pack...`) + code riêng `Assets/_Game/Scripts/Guns/` (`Gun.cs` abstract, `HitscanGun.cs`, `GunConfig.cs` SO, `GunCatalog.cs`), namespace `TossZone.Guns`, gate bởi `#if PHOTON_FUSION`. | **Mang model súng (Low Poly Weapon Pack) sang làm visual**, và **học kiến trúc `Gun.cs`/`HitscanGun.cs`/`GunConfig.cs`** (đúng pattern skill khuyên: 1 class `Weapon.cs` + `WeaponData` SO, không tạo class con cho từng súng) — viết lại bản không phụ thuộc Fusion. |
| `stylized-toon-world-kit` (GitHub) | UPM git package, URP 17 / Unity 6, HLSL thuần (không Shader Graph). 31 shader: toon lit + outline, environment (nước, cỏ, foliage, terrain blend), VFX (dissolve, teleport, force field...), material, anime NPR. Proprietary/không redistribute qua Asset Store — nhưng **cài qua Package Manager bằng git URL**, không phải copy file, nên không vi phạm gì khi thêm vào `manifest.json` của chính dự án đang làm cho công ty. | **Thêm vào `manifest.json`** dạng git package (B2): `"com.billtruong.stylized-toon-world-kit": "https://github.com/billtruong003/stylized-toon-world-kit.git"`. Dùng: **toon lit + outline** cho soldier/zombie/gun, **Dissolve** cho hiệu ứng zombie biến mất khi chết (yêu cầu #4), **terrain/grass** cho ground chunk nếu kịp thời gian. |
| `unity-mcp` (CoplayDev) | Git package `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main`, hỗ trợ Unity 2021.3→6.x, cần Python 3.10+ (qua `uv`) cho server, cấu hình client bằng menu `Window → MCP for Unity → Configure All Detected Clients`. | Thêm vào `manifest.json` (B2) để dùng skill `unity-skills` (unity_automation MCP) điều khiển Editor trực tiếp trong các bước sau. |
| `ShadersLab-BillShader` — animation | `Malbers Animations/Common/Human Anims` — bộ animation Humanoid thuần, xác nhận `animationType: 3` toàn bộ clip kiểm tra. Có đủ locomotion 8-hướng, aim-idle, animation cầm súng lục/rifle riêng biệt, animation ném 4 hướng, hit/death. | **Copy các folder cần dùng** (không lấy nguyên 192MB `Human Anims`) vào `Assets/ThirdParty/MalbersHumanAnims/` (~46MB) — xem mục 8. |
| "3D Characters Pro - Fantasy" (Layer Lab, `.unitypackage` người dùng tải) | Character pack modular (rig + parts, KHÔNG có animation đi kèm). | Đã tự giải nén `.unitypackage` (gzip-tar, không cần mở Editor) vào `Assets/ThirdParty/Layer Lab/3D Casual Character/`. Dùng làm model soldier. **Cân nhắc tính năng modular character** (đổi trang phục/bộ phận) làm điểm cộng visual nếu còn thời gian ở Phase 7 — xem cách `My project (1)/Scripts/ModularUtils/CharacterModularApplier.cs` consume đúng format Layer Lab này (rebind SkinnedMeshRenderer theo skeleton chung) nếu muốn làm, không bắt buộc theo đề bài. |
| "Epic Toon FX" v1.82 (`.unitypackage` người dùng tải) | Bộ particle VFX phong cách toon (nổ, máu, phép thuật, hit effect...). | Giải nén tương tự vào `Assets/ThirdParty/Epic Toon FX/` (~547MB). Dùng cho: muzzle flash, smoke trail, impact particle (yêu cầu #5), particle nổ bomb (yêu cầu #6 — **bắt buộc particle-only, không mesh**, xem mục 6), particle máu zombie trúng đạn (yêu cầu #4). |
| "GUI Pro - Super Casual" v1.0 (`.unitypackage` người dùng tải) | Bộ UI kit casual (button, panel, popup, icon...). | Giải nén vào `Assets/ThirdParty/Layer Lab/GUI Pro-SuperCasual/` (~161MB, cùng vendor Layer Lab nên lồng chung thư mục). Dùng cho HUD/menu ở Phase 5, thay vì tự vẽ UI từ đầu. |

**Rủi ro version đã kiểm tra:** ZombieWar = `6000.3.10f1`, TOSSZONE = `6000.3.8f1`, ShadersLab = `6000.2.7f2` — tất cả đều Unity 6.x cùng thế hệ URP 17.x, rủi ro lệch version thấp nhưng vẫn cần build-test lại (B4) vì asset copy thủ công giữa các minor version Unity 6 đôi khi lệch shader serialization nhẹ.

---

## 2. Camera & Control

> **Đổi hướng (sau B4):** bạn yêu cầu đổi từ Cinemachine sang **1 script camera custom tự viết**. Lưu ý: đề bài gốc ghi rõ yêu cầu #1 "dùng Cinemachine follow nhân vật" — đây là lựa chọn có chủ đích khác với đề, note lại để bạn cân nhắc khi phỏng vấn (có thể giải thích lý do chọn custom: kiểm soát chặt hơn, không cần phụ thuộc package cho một tính năng đơn giản). Đã gỡ `com.unity.cinemachine` khỏi manifest vì không còn dùng.

**Camera:** 1 `CameraFollow.cs` gắn lên Main Camera — giữ offset cố định phía trên/sau player (góc nghiêng ~60-70°, không thẳng đứng 90° để vẫn thấy rõ model soldier/zombie/gun), dùng `Vector3.SmoothDamp` để đuổi theo vị trí player mỗi `LateUpdate`, rotation set 1 lần (cố định, không đổi theo player). Camera shake (bắn súng mạnh, bom nổ) viết tay bằng kỹ thuật **trauma-based shake** (decay theo thời gian, offset random scale theo trauma) ngay trong cùng script — rẻ, không cần Cinemachine Impulse.

**Control:** đề bài chỉ nói **1 joystick ảo điều khiển soldier** (không nói joystick bắn riêng) → quyết định: **auto-aim vào zombie gần nhất trong tầm bắn**, thân player xoay theo joystick di chuyển, riêng "điểm ngắm"/gun xoay theo hướng zombie gần nhất (nếu không có zombie trong tầm, gun xoay theo hướng di chuyển). Đây là lựa chọn rẻ nhất, đúng chuẩn skill (1 joystick → 1 input đọc trực tiếp, không thêm input provider layer), và hợp lý vì zombie tràn từ 4 phía nên auto-aim tránh việc người chơi phải xoay tay bằng joystick thứ 2 không tồn tại.

**Bắn — auto-fire, KHÔNG có nút bắn (chốt sau B7):** đi vào tầm súng (`WeaponData.range`) tự động bắn, không cần input bắn thủ công. `Weapon.cs` tự kiểm tra `PlayerMovement.HasTarget` + khoảng cách mỗi frame (gated bởi cooldown fire rate sẵn có, không tốn thêm chi phí). UI chỉ còn joystick di chuyển + nút switch súng + nút bomb — đúng tinh thần tối giản input cho mobile.

**Effort ước lượng:** 0.5–1 ngày (theo skill).

---

## 3. Soldier (Player)

- **Animation:** Animator Controller với 2 **Animation Layer** tách biệt — Layer 0 (Base, full body) = Locomotion (Idle/Run theo `Speed` param từ joystick magnitude), Layer 1 (Upper Body, mask chỉ phần thân trên + tay, weight luôn = 1 khi cầm súng) = Aim/Fire, dùng Avatar Mask loại trừ chân. Đây là cách rẻ nhất đáp ứng đúng yêu cầu #3 "animation layer tách riêng chạy và bắn" mà không cần blend tree phức tạp.
- **Máu / hit feedback:** `Health.cs` đơn giản (currentHP, `TakeDamage()`, sự kiện `OnDamaged`/`OnDeath` qua `IEventBus` của BillGameCore) + flash trắng material 1-2 frame + hit-stop ngắn khi trúng đòn nặng (theo `juice-vfx-shader.md`) + UI health bar world-space hoặc HUD.
- **Súng trên tay + xoay đúng hướng + IK chân — đây là phần cần R&D, xem mục 8 (B5).** Ghi nhận rõ trong doc này: **chưa chốt kỹ thuật**, sẽ nghiên cứu 2 hướng (Animation Rigging IK vs simple bone-parent) ở B5 trước khi code, đúng theo note của bạn.

**Effort ước lượng:** animation layer + health/hit feedback ~ 1 ngày (chưa tính phần súng+IK, xem mục 8).

---

## 4. Zombie AI

- Kiến trúc: 1 `ZombieAI.cs` + `ZombieData` ScriptableObject (giống pattern `WeaponData`) — reuse ý tưởng từ `EnemyBase.cs`/`EnemyStats.cs` của project "EchoMage" (`My project (1)`) nhưng tối giản hoá: state machine chỉ còn **Idle → Chase → Attack → Dead** (bỏ Spawning/Stunned, không cần theo đề bài).
- **Render/animation:** dùng VAT (`VAT_Animator.CrossFade()`) thay Animator Controller truyền thống — lý do: hàng chục/hàng trăm zombie cùng lúc từ 4 phía, VAT rẻ hơn nhiều so với Animator instance per-zombie (không cần Mecanim runtime cho từng con). **Đã có sẵn data bake thật** (`Assets/ThirdParty/VATEnemy/`) cho ~7 loại quái (Batty, Cute_Spider/King, Whispa, Prizon, Lusif) — dùng thẳng làm zombie thường, không cần tự bake.
- **Zombie khổng lồ (Level 2) đã có sẵn asset, không cần làm mới:** `VATEnemy/Boss/SM_Chr_Kaiju_01/02/04_VAT_Data.asset` — baked Kaiju boss, dùng thẳng cho yêu cầu #7 "zombie khổng lồ", chỉ cần scale + AC_Run_F.controller đi kèm đã có sẵn trong cùng thư mục.
- **Hiệu ứng trúng đạn + biến mất (yêu cầu #4):** flash hit + particle máu tại điểm trúng đạn, khi hết máu → Dissolve shader (từ `stylized-toon-world-kit`) chạy dissolve ~0.5-1s rồi trả về pool (không `Destroy`).
- **Pooling + gating theo khoảng cách — 3 tier (chốt thêm sau B7, không chỉ 2 tier như bản đầu):**
  - Object pool cố định (BillGameCore `PoolService`) cho từng `ZombieData` type.
  - Spawn **luôn ngoài phạm vi màn hình** quanh player (vòng tròn bán kính > far frustum) rồi chạy vào — tạo cảm giác đông mà không cần render/tính toán full ngay từ đầu.
  - **Tier 1 — Gần (trong active radius / gần hoặc trong camera):** full logic — NavMeshAgent.SetDestination, animation crossfade đúng nhịp, attack check, hit reaction (giật lùi khi bị bắn), âm thanh.
  - **Tier 2 — Xa (ngoài active radius nhưng vẫn trong chunk đang active):** logic "ngáo ngơ" cực rẻ — lerp thẳng về 1 điểm gần player (KHÔNG NavMesh, KHÔNG tìm target, KHÔNG animation crossfade tốn), throttle update mỗi N frame qua `ZombieManager.cs` trung tâm.
  - **Tier 3 — Ngoài chunk active (player đã di chuyển xa sang chunk khác):** **tự tắt hẳn** — `gameObject.SetActive(false)` hoặc trả thẳng về pool, KHÔNG chạy bất kỳ logic gì (kể cả tier 2). Zombie thuộc chunk đã rời khỏi lưới 3×3 quanh player (xem mục 7 World Generation) không tồn tại về mặt xử lý cho tới khi chunk đó active trở lại.
  - Việc chuyển tier chỉ là 1 check khoảng cách + trạng thái chunk trong 1 manager trung tâm (`ZombieManager.cs`) chạy theo interval, KHÔNG phải mỗi zombie tự kiểm tra — đúng nguyên tắc "if chặn logic nặng" bạn đề cập, tránh N zombie × N check/frame.
- **Zombie khổng lồ (Level 2, điểm cộng):** 1 `ZombieData` asset riêng trỏ vào Kaiju VAT data đã có sẵn (xem trên) — HP/damage cao, field `isBoss` bật thêm VFX/SFX đặc biệt khi xuất hiện — dùng chung `ZombieAI.cs`, không cần class riêng, đúng nguyên tắc data-driven variety của skill.

**Effort ước lượng:** Zombie AI + pooling/gating ~ 1.5 ngày (cao hơn ước lượng mặc định của skill vì có thêm tier logic theo khoảng cách).

---

## 5. Gun

- Kiến trúc data: `WeaponData`/`GunConfig` ScriptableObject (tên, fireRate, damage, range, bulletVFX prefab, muzzleFx, fireSfx, recoil params) — reuse pattern từ `TOSSZONE/Gun.cs` nhưng bỏ Photon Fusion gate.
- **Bắn: hitscan** (Physics.Raycast) — rẻ, khớp nhịp game dồn dập, đúng khuyến nghị mặc định của skill cho horde lớn. "Particle đạn nổ" trong yêu cầu #5 = muzzle flash + **particle khói mô tả đường đạn** (1 particle kéo dài dọc theo tia raycast, từ nòng súng đến điểm trúng — dùng Line/Trail-style particle hoặc đơn giản là 1 particle burst kéo dài theo hướng bắn) + impact particle tại điểm raycast trúng (không phải projectile vật lý bay).
- **≥2 loại súng + switch:** `List<WeaponData>` 2 phần tử trên player + button switch tăng index modulo — dùng model từ Low Poly Weapon Pack (TOSSZONE) làm visual (vd: Pistol + AR/SMG — khác rõ rệt về fire rate/recoil để "cảm" được sự khác biệt khi switch).
- **Hiệu ứng súng giật (recoil) — noise-based cho cảm giác tự nhiên/"satisfying":** dùng `BillTween` để kick vị trí/rotation gun model theo 1 offset **random theo noise** (Perlin hoặc `Random.Range` trong 1 cone góc nhỏ, không phải offset cố định lặp lại y hệt mỗi phát) rồi ease-out về vị trí gốc — mỗi phát bắn giật hơi khác nhau (giống kỹ thuật trauma-based đã dùng cho `CameraFollow.Shake()`), tránh cảm giác máy móc lặp lại của 1 tween cố định.

**Effort ước lượng:** ~1 ngày (theo skill, 2 súng + switch).

---

## 6. Bomb

- 1 `Bomb.cs`: player ném ra trước mặt (theo hướng aim hiện tại — tái dùng hướng auto-aim ở mục 2, không cần cơ chế ném quỹ đạo vật lý phức tạp cho MVP), delay ngòi nổ N giây, nổ = `Physics.OverlapSphere` gây damage vật lý đúng yêu cầu #6, kèm particle nổ + `CameraFollow.Shake()` (custom, xem mục 2) + SFX.
- **Hiệu ứng nổ — BẮT BUỘC particle-only, không mesh:** `explosionPrefab` là `ParticleSystem` (đã ép kiểu trong `Bomb.cs`, không nhận GameObject có mesh renderer thường). Dùng particle từ **Epic Toon FX** (đã import vào `Assets/ThirdParty/Epic Toon FX/`) cho vùng nổ — không dựng mesh cầu/sphere giả nổ.
- **Animation ném:** `BombThrower.cs` bắn `Animator.SetTrigger("Throw")` ngay khi bấm nút, bomb thực sự spawn sau `releaseDelay` (mặc định 0.3s, tune lại theo đúng frame "buông tay" của animation ném thật khi có clip) — khớp cảm giác ném thay vì bomb bay ra tức thì không có anim.
- Input: 1 button riêng (UI) để ném bomb, có cooldown/số lượng giới hạn hiển thị trên HUD.

**Effort ước lượng:** ~0.5 ngày.

---

## 7. Level & World Generation

**World rộng vô hạn (theo hướng bạn đã chốt):**
- 1 `GroundChunk` prefab (plane) kích thước cố định (vd 30×30m), 1 `WorldStreamer.cs` giữ lưới 3×3 chunk quanh player, khi player băng qua ranh giới chunk trung tâm → dịch chuyển/tái sử dụng (pool) chunk ở rìa xa nhất sang rìa mới cần — không sinh/huỷ GameObject liên tục, chỉ move + re-randomize nội dung (obstacle spawn points) của chunk đó.
- Obstacle spawn (Level 1: vật cản trên mặt đất bằng phẳng) và slope pieces (Level 2) đều là prefab con được spawn theo từng `GroundChunk` từ 1 pool, dùng seed/random per-chunk để tạo cảm giác đa dạng dù chunk lặp lại.

**Level 1 — mặt đất phẳng + vật cản:**
- `GroundChunk` phẳng, obstacle set = props tĩnh (thùng, xe, rào chắn...) vừa che tầm nhìn vừa cản đường zombie (NavMesh Obstacle hoặc NavMesh static baked theo prefab).

**Level 2 — dốc + zombie khổng lồ (điểm cộng, làm sau khi Level 1 + toàn bộ pipeline core đã chắc):**
- `GroundChunk` biến thể có ramp/dốc (mesh riêng hoặc Terrain blend dùng shader terrain của toon kit), zombie khổng lồ xuất hiện theo mốc thời gian cuối level (giống gợi ý "boss/elite phút cuối" trong skill).

**Nhịp độ (pacing) — 3 phút/level, tăng dần:**
- Dùng `AnimationCurve spawnRateOverTime` (đúng pattern skill) — chọn hướng **"có nhịp nghỉ" (breathing room)**: tăng dồn dập rồi có khoảng lặng ngắn, tạo cảm giác "đợt sóng" rõ hơn tuyến tính thuần, đúng với yêu cầu "nhịp điệu dồn dập tăng cao dần" trong đề bài. Nếu có Level 2, spawn rate nền của Level 2 bắt đầu cao hơn điểm kết Level 1 — tạo cảm giác leo thang xuyên suốt 2 level chứ không reset về 0.

**Effort ước lượng:** World streaming + chunk pooling ~1–1.5 ngày. Level 1 dựng nội dung ~0.5 ngày. Level 2 (nếu làm) ~1 ngày thêm.

---

## 8. Player cầm súng đúng tư thế + IK — ĐÃ CHỐT (B5)

Đã khảo sát cả 3 project nguồn để tìm giải pháp có sẵn trước khi tự viết (nguyên tắc "tìm trước khi viết" của expert-developer skill):

- **`ShadersLab-BillShader`:** `Malbers Animations` chỉ là animal/creature controller — không có gì liên quan súng/IK/tay. Nhưng project này có cài **MoreMountains TopDownEngine** (asset trả phí) — có sẵn `WeaponIK.cs` (Animator IK Pass, `SetIKPosition/Rotation` cho tay theo target transform mỗi súng), `WeaponAim3D.cs` (xoay thân trên theo hướng ngắm), `CharacterHandleWeapon.cs` (switch nhiều súng, mỗi súng có điểm cầm riêng) — **giải pháp drop-in hoàn chỉnh, có demo lính cầm súng trường chạy thật**.
- **`My project (1)`:** `LocomotionModular/` chỉ là state machine di chuyển thuần (Dash/Fall/Grounded/Jump) — không liên quan tay/súng/IK. `CharacterModularApplier.cs` là hệ thống đổi trang phục/bộ phận cơ thể (rebind SkinnedMeshRenderer theo skeleton chung), không phải hệ animation/IK. Animation Rigging **đã cài nhưng chưa dùng ở đâu cả** trong project này.
- **`TOSSZONE`:** súng cầm trên tay chỉ bằng **bone-parenting thuần** (parent thẳng vào wrist transform, không IK) cho phần cosmetic; phần tương tác thật dùng Autohand (VR grab, không áp dụng được cho top-down non-VR).

**Quyết định (bạn chốt trực tiếp):** **không dùng TopDownEngine** (tránh kéo theo cả framework character/ability riêng của nó) — **tự viết bằng Animation Rigging package** (`com.unity.animation.rigging`, đã có sẵn trong manifest từ B2). Code đã viết ở `Weapon.cs`/`WeaponGripPoints.cs`, phần Rig dựng trong Editor (xem `Docs/EDITOR_SETUP_CHECKLIST.md`):
- **Tay — quy ước cầm súng theo loại (chốt thêm sau B7):**
  - **Súng 1 tay có thể cầm 2 tay kiểu "cup and saucer" (súng lục):** `WeaponGripPoints.rightHandGrip` VÀ `leftHandGrip` trỏ **cùng 1 Transform** (điểm cầm duy nhất) — cả 2 Two-Bone IK Constraint tay đều nhắm vào đúng 1 điểm đó.
  - **Súng 2 tay (AR/rifle):** `rightHandGrip`/`leftHandGrip` là **2 Transform riêng biệt** (tay súng + tay đỡ nòng/báng trước) — không cần đổi code, chỉ là cách đặt Transform khác nhau trên từng prefab súng.
  - Khi switch súng, `Weapon.OnWeaponEquipped` (đã có sẵn) bắn ra `WeaponGripPoints` mới để 1 script nhỏ ở Editor cập nhật lại target của 2 Two-Bone IK Constraint.
- Hướng ngắm: **Multi-Aim Constraint** trên xương thân trên (spine/chest), target = điểm auto-aim (zombie gần nhất hoặc hướng di chuyển, theo mục 2).
- **Chân: Two-Bone IK Constraint + raycast xuống đất, LUÔN bật** (không chỉ khi có dốc) — đảm bảo chân luôn chạm đất kể cả trên nền phẳng có chênh lệch nhỏ (obstacle, mép chunk), bắt buộc rõ hơn cho Level 2 dốc.
- Toàn bộ chạy qua 1 `RigBuilder` + `Rig` layer trên Animator, weight điều chỉnh qua code khi cần tắt IK (VD: lúc chết).

**Animation nguồn — ĐÃ CHỐT:** Layer Lab character pack không có animation nào đi kèm (chỉ có rig + modular parts). Đã khảo sát cả 3 project — `My project (1)` và `TOSSZONE` không có animation Humanoid nào dùng được (chỉ có code, không có file .fbx/.anim). **`ShadersLab-BillShader` có sẵn `Malbers Animations/Common/Human Anims`** — bộ animation Humanoid thuần (không phải animal-specific dù tên gói), xác nhận `animationType: 3` (Humanoid) trên toàn bộ clip đã kiểm tra, khớp gần như hoàn hảo với nhu cầu:
- `Locomotion/Strafe/` — bộ 8 hướng strafe walk/jog đầy đủ (N/NE/E/SE/S/SW/W/NW) + turn-in-place — đúng chuẩn top-down.
- `Idle/Idle_Combat.fbx` — pose aim-idle/sẵn sàng chiến đấu.
- `Weapons/Pistol/H_Weapon_Pistol_AimFire.FBX` + `Weapons/Rifle/S_Rifle_Aim.fbx` — animation cầm/ngắm riêng theo loại súng (khớp đúng quy ước 1 tay/2 tay ở trên).
- `Weapons/Throwable/H_Throw_N/E/S/W.fbx` — 4 animation ném theo hướng, dùng cho bomb.
- `Hit/`, `Deaths/` — hit reaction + death animation cho player (yêu cầu #3 "hiệu ứng mất máu").

Đã copy các folder này vào `Assets/ThirdParty/MalbersHumanAnims/` (~46MB, chỉ lấy phần cần dùng, không copy nguyên `Human Anims` 192MB gồm cả Sword/Axe/Bow/Spear/Swim/Climb không liên quan). Import vào Editor với **Animation Type = Humanoid** trên từng .fbx để retarget lên rig Layer Lab.

**Effort ước lượng:** Rig setup (RigBuilder + constraints cơ bản) ~0.5 ngày, tune theo từng súng + chân IK ~0.5-1 ngày tuỳ độ phức tạp model súng.

---

## 9. Audio & Juice tổng hợp (yêu cầu #8)

- **SFX:** dùng `AudioService` có sẵn trong BillGameCore (không viết AudioManager riêng) — bắn, đổi súng, bom nổ, zombie chết, player trúng đòn, nhạc nền loop.
- **Particle:** muzzle flash, máu khi trúng đạn, nổ bomb, dissolve residual (nếu cần) — mỗi cái 1 `ParticleSystem` prefab dùng chung, `Stop Action: Destroy`/pool, không viết hệ thống VFX riêng.
- **Tween/juice nhỏ (giật súng, UI popup damage, chunk transition mượt):** `BillTween` — đã có sẵn, không thêm DOTween.
- **Camera shake:** đã đổi sang `CameraFollow.Shake()` tự viết (trauma-based, xem mục 2) — gọi khi bắn súng mạnh, bom nổ, không lạm dụng cho mọi hit nhỏ.
- **Hit-stop:** `Time.timeScale` ngắn khi trúng đòn nặng — rẻ, ưu tiên làm sớm theo skill (impact/effort cao nhất).

---

## 10. Thứ tự cắt giảm nếu vỡ deadline (theo đúng nguyên tắc skill, không phải đoán)

Giữ theo thứ tự impact/effort giảm dần — cắt từ dưới lên nếu thiếu thời gian:

1. Toàn bộ Level 1 + gun + bomb + zombie AI cơ bản + camera/control (core loop chơi được) — **không được cắt, đây là bài test.**
2. Hit-stop, camera shake, particle máu/muzzle cơ bản, dissolve shader zombie chết — rẻ, impact cao, giữ lại.
3. Animation layer chạy/bắn tách biệt + IK chân cơ bản — giữ, đúng yêu cầu #3.
4. Súng cầm đúng tư thế bằng Animation Rigging đầy đủ (nếu tune constraint tốn quá nhiều thời gian ở B7) — có thể fallback: súng parent cứng vào 1 bone tay (không IK động), chấp nhận kém tự nhiên hơn nhưng vẫn "cầm đúng vị trí".
5. World streaming vô hạn đầy đủ (nếu không kịp) — fallback: 1 map lớn cố định đủ rộng, bo viền bằng invisible wall, vẫn đáp ứng "mặt đất bằng phẳng có vật cản" dù không sinh vô hạn thật.
6. **Level 2 (dốc + zombie khổng lồ)** — cắt cuối cùng, đúng như đề bài ghi rõ "điểm cộng".

---

## 11. Trạng thái quyết định (đã chốt qua B2-B5)

Toàn bộ câu hỏi mở ở bản draft đầu đã được chốt qua các bước thực tế:

1. **Camera:** đổi từ Cinemachine sang `CameraFollow.cs` tự viết (mục 2) — quyết định trực tiếp của bạn ở B4, ghi chú rủi ro so với đề bài gốc.
2. **BillGameCore + BillInspector:** mang nguyên khối, đã fix xong các lỗi asmdef boundary phát sinh (`BillGameCore.Runtime.asmdef`, di chuyển `DevTools.cs`) — build sạch ở B4.
3. **Auto-aim súng vào zombie gần nhất:** giữ nguyên như đề xuất ban đầu, không có phản đối.
4. **Player cầm súng + IK:** chốt Animation Rigging tự viết, không dùng TopDownEngine (mục 8).
5. **Nguồn VAT/enemy:** sửa lại đúng nguồn `My project (1)` (không phải `ShadersLab-BillShader`) — đã re-copy `VAT` + `VATEnemy` (baked data có sẵn, gồm cả Kaiju boss cho Level 2).

B5 coi như hoàn tất. Tiếp theo: **B6** — lên task đầy đủ theo phase cho bản playable test.
