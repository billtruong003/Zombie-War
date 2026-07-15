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

| Nguồn | Cái gì | Trạng thái thực tế sau khảo sát | Quyết định |
|---|---|---|---|
| `ShadersLab-BillShader` | VAT bake tool + shader (URP, Unity 6000.2.7f2) | `Assets/Shaders/VAT/` là **package độc lập hoàn toàn** — không asmdef, không dependency ngoài URP core. Có sẵn kiến trúc enemy tham khảo (`EnemyBase.cs` + `EnemyData.cs` SO) dùng NavMeshAgent + state machine Idle/Chasing/Attacking/Kiting/Dead, gọi `VAT_Animator.CrossFade()` khi đổi state. | **Copy nguyên `Assets/Shaders/VAT/` sang** (B3). **Học kiến trúc `EnemyBase`, không copy nguyên văn** — comment tiếng Việt, gắn với dự án cũ, và có state "Kiting" không cần cho zombie cận chiến. Viết lại bản tối giản hơn theo đúng state machine 4 trạng thái (Idle/Chase/Attack/Dead), bỏ Kiting. |
| `TOSSZONE` | BillGameCore (`com.bill.gamecore` v3.0.0, Unity 6000.3.8f1) | Core Runtime (`ServiceLocator`, `Bill` facade, `BillTween`, `PoolService`, `EventBus`, `AudioService`, `TimerService`, `SceneService`, `GameStateMachine`) **không có asmdef riêng** (biên dịch vào default assembly) và **phụ thuộc cứng vào `BillInspector`** (attribute `[BillTitle]`, `[BillRequired]`...) dùng khắp nơi. `NetworkService`/Fusion bị gate bởi `#if PHOTON_FUSION` — an toàn, không kéo theo nếu không định nghĩa symbol đó. | **Mang cả `BillGameCore` + `BillInspector` sang nguyên khối** (B3) — không tách lẻ, vì core phụ thuộc cứng vào BillInspector nên tách sẽ vỡ. Đây vẫn là lựa chọn đơn giản nhất (nguyên tắc "code sẵn có trong project" của expert-developer skill) hơn là viết lại BillTween/Pool/EventBus từ đầu. Sẽ **không** bật `PHOTON_FUSION` — networking không dùng. |
| `TOSSZONE` — súng | Không có package súng nào trong `manifest.json` (đã kiểm tra kỹ — không tồn tại "gun package qua manifest" như giả định ban đầu). Súng thực chất là các asset-store folder thô (`Low Poly Pistol/AR/ShotGun Weapon Pack...`) + code riêng `Assets/_Game/Scripts/Guns/` (`Gun.cs` abstract, `HitscanGun.cs`, `GunConfig.cs` SO, `GunCatalog.cs`), namespace `TossZone.Guns`, gate bởi `#if PHOTON_FUSION`. | **Mang model súng (Low Poly Weapon Pack) sang làm visual**, và **học kiến trúc `Gun.cs`/`HitscanGun.cs`/`GunConfig.cs`** (đúng pattern skill khuyên: 1 class `Weapon.cs` + `WeaponData` SO, không tạo class con cho từng súng) — viết lại bản không phụ thuộc Fusion. |
| `stylized-toon-world-kit` (GitHub) | UPM git package, URP 17 / Unity 6, HLSL thuần (không Shader Graph). 31 shader: toon lit + outline, environment (nước, cỏ, foliage, terrain blend), VFX (dissolve, teleport, force field...), material, anime NPR. Proprietary/không redistribute qua Asset Store — nhưng **cài qua Package Manager bằng git URL**, không phải copy file, nên không vi phạm gì khi thêm vào `manifest.json` của chính dự án đang làm cho công ty. | **Thêm vào `manifest.json`** dạng git package (B2): `"com.billtruong.stylized-toon-world-kit": "https://github.com/billtruong003/stylized-toon-world-kit.git"`. Dùng: **toon lit + outline** cho soldier/zombie/gun, **Dissolve** cho hiệu ứng zombie biến mất khi chết (yêu cầu #4), **terrain/grass** cho ground chunk nếu kịp thời gian. |
| `unity-mcp` (CoplayDev) | Git package `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main`, hỗ trợ Unity 2021.3→6.x, cần Python 3.10+ (qua `uv`) cho server, cấu hình client bằng menu `Window → MCP for Unity → Configure All Detected Clients`. | Thêm vào `manifest.json` (B2) để dùng skill `unity-skills` (unity_automation MCP) điều khiển Editor trực tiếp trong các bước sau. |

**Rủi ro version đã kiểm tra:** ZombieWar = `6000.3.10f1`, TOSSZONE = `6000.3.8f1`, ShadersLab = `6000.2.7f2` — tất cả đều Unity 6.x cùng thế hệ URP 17.x, rủi ro lệch version thấp nhưng vẫn cần build-test lại (B4) vì asset copy thủ công giữa các minor version Unity 6 đôi khi lệch shader serialization nhẹ.

---

## 2. Camera & Control

**Camera:** 1 `CinemachineCamera` (Cinemachine 3.x, thêm qua manifest ở B2), `Follow` = player, `Body` = Framing Transposer/top-down preset, `Rotation` = Do Nothing, góc nghiêng cố định ~60-70° (isometric-ish, không phải thẳng đứng 90° vì đề bài nói "trên xuống" nhưng vẫn cần thấy được model soldier/zombie/gun cho đẹp — thuần 90° sẽ chỉ thấy đỉnh đầu, phí công polish model). 1 `CinemachineImpulseSource` global cho camera shake (bắn súng mạnh, bom nổ) — dùng built-in, không viết shake code tay.

**Control:** đề bài chỉ nói **1 joystick ảo điều khiển soldier** (không nói joystick bắn riêng) → quyết định: **auto-aim vào zombie gần nhất trong tầm bắn**, thân player xoay theo joystick di chuyển, riêng "điểm ngắm"/gun xoay theo hướng zombie gần nhất (nếu không có zombie trong tầm, gun xoay theo hướng di chuyển). Đây là lựa chọn rẻ nhất, đúng chuẩn skill (1 joystick → 1 input đọc trực tiếp, không thêm input provider layer), và hợp lý vì zombie tràn từ 4 phía nên auto-aim tránh việc người chơi phải xoay tay bằng joystick thứ 2 không tồn tại.

**Effort ước lượng:** 0.5–1 ngày (theo skill).

---

## 3. Soldier (Player)

- **Animation:** Animator Controller với 2 **Animation Layer** tách biệt — Layer 0 (Base, full body) = Locomotion (Idle/Run theo `Speed` param từ joystick magnitude), Layer 1 (Upper Body, mask chỉ phần thân trên + tay, weight luôn = 1 khi cầm súng) = Aim/Fire, dùng Avatar Mask loại trừ chân. Đây là cách rẻ nhất đáp ứng đúng yêu cầu #3 "animation layer tách riêng chạy và bắn" mà không cần blend tree phức tạp.
- **Máu / hit feedback:** `Health.cs` đơn giản (currentHP, `TakeDamage()`, sự kiện `OnDamaged`/`OnDeath` qua `IEventBus` của BillGameCore) + flash trắng material 1-2 frame + hit-stop ngắn khi trúng đòn nặng (theo `juice-vfx-shader.md`) + UI health bar world-space hoặc HUD.
- **Súng trên tay + xoay đúng hướng + IK chân — đây là phần cần R&D, xem mục 8 (B5).** Ghi nhận rõ trong doc này: **chưa chốt kỹ thuật**, sẽ nghiên cứu 2 hướng (Animation Rigging IK vs simple bone-parent) ở B5 trước khi code, đúng theo note của bạn.

**Effort ước lượng:** animation layer + health/hit feedback ~ 1 ngày (chưa tính phần súng+IK, xem mục 8).

---

## 4. Zombie AI

- Kiến trúc: 1 `ZombieAI.cs` + `ZombieData` ScriptableObject (giống pattern `WeaponData`) — reuse ý tưởng từ `EnemyBase.cs`/`EnemyData.cs` nhưng tối giản hoá: state machine chỉ còn **Idle → Chase → Attack → Dead** (bỏ Kiting, không cần zombie tầm xa theo đề bài).
- **Render/animation:** dùng VAT (`VAT_Animator.CrossFade()`) thay Animator Controller truyền thống — lý do: hàng chục/hàng trăm zombie cùng lúc từ 4 phía, VAT rẻ hơn nhiều so với Animator instance per-zombie (không cần Mecanim runtime cho từng con), và team đã có sẵn pipeline bake ổn định.
- **Hiệu ứng trúng đạn + biến mất (yêu cầu #4):** flash hit + particle máu tại điểm trúng đạn, khi hết máu → Dissolve shader (từ `stylized-toon-world-kit`) chạy dissolve ~0.5-1s rồi trả về pool (không `Destroy`).
- **Pooling + gating theo khoảng cách (theo đúng hướng bạn note):**
  - Object pool cố định (BillGameCore `PoolService`) cho từng `ZombieData` type.
  - Spawn ngoài rìa camera view quanh player (vòng tròn bán kính > far frustum).
  - **2 tier logic theo khoảng cách tới player:**
    - *Xa (ngoài "active radius")*: chỉ di chuyển "ngáo ngơ" rẻ tiền — lerp thẳng về 1 điểm gần player (không NavMesh, không tìm target, không animation crossfade tốn — hoặc animation chạy tần suất thấp), throttle Update mỗi N frame qua 1 manager trung tâm chứ không mỗi zombie tự Update (tránh hàng trăm Update() riêng lẻ).
    - *Gần (trong "active radius", trong hoặc sát camera)*: bật full logic — NavMeshAgent.SetDestination, animation crossfade đúng nhịp, attack check, hit reaction (giật lùi khi bị bắn theo note của bạn), âm thanh.
  - Việc chuyển tier chỉ là 1 check khoảng cách trong 1 manager trung tâm (`ZombieManager.cs`) chạy theo interval, KHÔNG phải mỗi zombie tự kiểm tra — đúng nguyên tắc "if chặn logic nặng" bạn đề cập, tránh N zombie × N check/frame.
- **Zombie khổng lồ (Level 2, điểm cộng):** 1 `ZombieData` asset riêng (scale lớn, HP/damage cao, có thể thêm field `isBoss` bật thêm VFX/SFX đặc biệt khi xuất hiện) — dùng chung `ZombieAI.cs`, không cần class riêng, đúng nguyên tắc data-driven variety của skill.

**Effort ước lượng:** Zombie AI + pooling/gating ~ 1.5 ngày (cao hơn ước lượng mặc định của skill vì có thêm tier logic theo khoảng cách).

---

## 5. Gun

- Kiến trúc data: `WeaponData`/`GunConfig` ScriptableObject (tên, fireRate, damage, range, bulletVFX prefab, muzzleFx, fireSfx, recoil params) — reuse pattern từ `TOSSZONE/Gun.cs` nhưng bỏ Photon Fusion gate.
- **Bắn: hitscan** (Physics.Raycast) — rẻ, khớp nhịp game dồn dập, đúng khuyến nghị mặc định của skill cho horde lớn. "Particle đạn nổ" trong yêu cầu #5 = muzzle flash + tracer/impact particle tại điểm raycast trúng (không phải projectile vật lý bay).
- **≥2 loại súng + switch:** `List<WeaponData>` 2 phần tử trên player + button switch tăng index modulo — dùng model từ Low Poly Weapon Pack (TOSSZONE) làm visual (vd: Pistol + AR/SMG — khác rõ rệt về fire rate/recoil để "cảm" được sự khác biệt khi switch).
- **Hiệu ứng súng giật (recoil):** dùng `BillTween` (đã có sẵn, không viết tween riêng) để giật procedural nhẹ vị trí/rotation của gun model mỗi phát bắn, trả về vị trí gốc bằng ease-out — rẻ, không cần animation clip riêng cho recoil.

**Effort ước lượng:** ~1 ngày (theo skill, 2 súng + switch).

---

## 6. Bomb

- 1 `Bomb.cs`: player ném ra trước mặt (theo hướng aim hiện tại — tái dùng hướng auto-aim ở mục 2, không cần cơ chế ném quỹ đạo vật lý phức tạp cho MVP), delay ngòi nổ N giây, nổ = `Physics.OverlapSphere` gây damage vật lý đúng yêu cầu #6, kèm particle nổ + `CinemachineImpulseSource.GenerateImpulse()` (camera shake) + SFX.
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

## 8. Việc chưa chốt — cần R&D ở B5 trước khi code (Player cầm súng đúng tư thế + IK)

Đây là điểm bí bạn nêu, **chưa quyết định kỹ thuật trong tài liệu này** — B5 sẽ nghiên cứu trước khi lên task B6. Ghi nhận 2 hướng khả dĩ để đánh giá ở B5:

1. **Animation Rigging (Unity package `com.unity.animation.rigging`)** — Two-Bone IK cho tay (súng gắn theo `RightHandIK` + `LeftHandIK` bám vào transform trên mesh súng), Multi-Aim Constraint cho hướng ngắm, chân dùng thêm Two-Bone IK + raycast xuống đất (foot placement) khi có dốc (Level 2). Đây là giải pháp built-in Unity, ít phụ thuộc ngoài, khớp nguyên tắc "ưu tiên built-in trước khi viết custom".
2. **Modular Movement / layered animation approach** (như bạn gợi ý) — nếu asset có sẵn (Malbers Animations có trong `ShadersLab-BillShader/Assets`, cần kiểm tra license/khả năng mang qua) cung cấp sẵn hệ thống gắn súng + animation layer theo item cầm tay, tốn ít code hơn nhưng phụ thuộc vào đúng cấu trúc rig/animation đi kèm asset đó.

**Quyết định chốt sẽ được đưa ra ở B5** sau khi so sánh thời gian tích hợp thực tế (Animation Rigging cần tune constraint bằng tay cho từng súng; Modular Movement nhanh hơn nếu asset khớp sẵn nhưng rủi ro nếu rig không tương thích). Chân dùng IK là chắc chắn (Unity Animation Rigging), phần tay/súng là phần cần thử nghiệm.

---

## 9. Audio & Juice tổng hợp (yêu cầu #8)

- **SFX:** dùng `AudioService` có sẵn trong BillGameCore (không viết AudioManager riêng) — bắn, đổi súng, bom nổ, zombie chết, player trúng đòn, nhạc nền loop.
- **Particle:** muzzle flash, máu khi trúng đạn, nổ bomb, dissolve residual (nếu cần) — mỗi cái 1 `ParticleSystem` prefab dùng chung, `Stop Action: Destroy`/pool, không viết hệ thống VFX riêng.
- **Tween/juice nhỏ (giật súng, UI popup damage, chunk transition mượt):** `BillTween` — đã có sẵn, không thêm DOTween.
- **Camera shake ("Juicy có cam shake" — cần bạn xác nhận):** mặc định dùng `CinemachineImpulseSource`/`CinemachineImpulseListener` built-in (đúng khuyến nghị skill, không viết shake code tay). Nếu "Juicy" bạn nhắc tới là 1 asset/package cụ thể riêng (không phải tính từ chung), báo tên chính xác để thêm vào manifest ở B2 — hiện tại giả định là Cinemachine Impulse.
- **Hit-stop:** `Time.timeScale` ngắn khi trúng đòn nặng — rẻ, ưu tiên làm sớm theo skill (impact/effort cao nhất).

---

## 10. Thứ tự cắt giảm nếu vỡ deadline (theo đúng nguyên tắc skill, không phải đoán)

Giữ theo thứ tự impact/effort giảm dần — cắt từ dưới lên nếu thiếu thời gian:

1. Toàn bộ Level 1 + gun + bomb + zombie AI cơ bản + camera/control (core loop chơi được) — **không được cắt, đây là bài test.**
2. Hit-stop, camera shake, particle máu/muzzle cơ bản, dissolve shader zombie chết — rẻ, impact cao, giữ lại.
3. Animation layer chạy/bắn tách biệt + IK chân cơ bản — giữ, đúng yêu cầu #3.
4. Súng cầm đúng tư thế bằng Animation Rigging đầy đủ (nếu R&D ở B5 tốn quá nhiều thời gian) — có thể fallback: súng parent cứng vào 1 bone tay (không IK động), chấp nhận kém tự nhiên hơn nhưng vẫn "cầm đúng vị trí".
5. World streaming vô hạn đầy đủ (nếu không kịp) — fallback: 1 map lớn cố định đủ rộng, bo viền bằng invisible wall, vẫn đáp ứng "mặt đất bằng phẳng có vật cản" dù không sinh vô hạn thật.
6. **Level 2 (dốc + zombie khổng lồ)** — cắt cuối cùng, đúng như đề bài ghi rõ "điểm cộng".

---

## 11. Câu hỏi cần bạn xác nhận trước khi qua B2

1. **"Juicy có cam shake"** — xác nhận đây là ý chung (Cinemachine Impulse, mặc định trong doc này) hay 1 asset cụ thể tên "Juice"/"Feel" bạn đang có ở đâu đó cần mang qua?
2. **Góc camera top-down**: chốt góc nghiêng cố định ~60-70° (thấy được model rõ) thay vì thẳng đứng 90° — đồng ý không?
3. **BillGameCore mang nguyên khối kèm `BillInspector`** (vì phụ thuộc cứng) — đồng ý, hay muốn tối giản hơn nữa (bỏ BillInspector, chỉ giữ phần logic thuần rồi tự thay editor attribute)? Việc tối giản thêm sẽ tốn thời gian refactor, không miễn phí.
4. **Auto-aim súng vào zombie gần nhất** (do đề bài chỉ có 1 joystick di chuyển) — đồng ý hướng này thay vì thêm joystick bắn thứ 2?

Sau khi bạn duyệt (hoặc chỉnh sửa) tài liệu này, sẽ tiến hành **B2: setup project + manifest + push git**.
