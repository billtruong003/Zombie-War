# Resume prompt — paste this into a new Claude Code session at the office

```
Đang làm game "Zombie War" (top-down zombie survival shooter, đề bài test tuyển dụng, deadline 7 ngày).
Repo: https://github.com/billtruong003/Zombie-War.git — mới clone về máy này, project Unity 6000.3.10f1 URP.

Việc đầu tiên: đọc kỹ 3 file sau trước khi làm gì cả, đừng hỏi lại những gì đã quyết định trong đó:
- Docs/GAMEPLAY_DESIGN.md — toàn bộ kiến trúc/quyết định thiết kế đã chốt (camera, control, player,
  zombie AI, gun, bomb, level/world-gen, audio/juice, cách làm IK). Đọc mục 11 để biết cái gì đã chốt.
- Docs/EDITOR_SETUP_CHECKLIST.md — checklist lắp Editor cho Phase 1 (Player) + Phase 2 (Weapon/Bomb),
  làm theo đúng thứ tự nếu cần lắp scene/prefab.
- Docs/TASK_BREAKDOWN.md — bản đầy đủ B6, có sub-task `[x]`/`[ ]` cho cả 7 phase (không chỉ tóm tắt
  1 dòng như dưới đây) — đây là nguồn sự thật cho "cái gì đã xong, cái gì chưa", cập nhật checkbox
  mỗi khi làm xong 1 mục.
- `git log --oneline` — xem full lịch sử quyết định qua từng commit (mỗi commit đều có message giải
  thích lý do, không chỉ liệt kê thay đổi).

## Trạng thái hiện tại (theo flow B1-B7 đã thống nhất từ đầu)

- B1 (design doc), B2 (setup project + manifest + push git), B3 (mang BillGameCore + VAT + weapon
  packs qua), B4 (check/fix lỗi compile), B5 (R&D: VAT enemy architecture, IK approach), B6 (task
  breakdown 7 phase) — ĐÃ XONG.
- B7 (execute) — đang làm, task tracker (tạo lại bằng TaskCreate nếu cần, không persist qua session):
  1. Phase 1: Player core — CODE XONG (PlayerMovement, Health, VirtualJoystick, CameraFollow với
     noise-texture shake), CHƯA lắp Editor (prefab, Animator 2-layer, Animation Rigging IK).
  2. Phase 2: Weapons — CODE XONG (WeaponData, WeaponGripPoints, Weapon.cs hitscan+smoke trail+
     noise recoil, Bomb.cs, BombThrower.cs), CHƯA lắp Editor (2 WeaponData asset, weapon prefab +
     grip points, particle prefabs, noise texture assignment).
  3. Phase 3: Zombie AI + VAT rendering + pooling — CHƯA BẮT ĐẦU.
  4. Phase 4: Wave system + world streaming + Level 1 — CHƯA BẮT ĐẦU.
  5. Phase 5: Juice/audio/UI polish — CHƯA BẮT ĐẦU.
  6. Phase 6: Level 2 (bonus, dốc + Kaiju boss) — CHƯA BẮT ĐẦU, làm sau cùng nếu còn thời gian.
  7. Phase 7: Playtest + polish pass — CHƯA BẮT ĐẦU.

## Quyết định quan trọng cần nhớ (đừng đề xuất lại hướng khác)

- Camera: KHÔNG dùng Cinemachine (dù đề bài yêu cầu) — tự viết `CameraFollow.cs` (SmoothDamp +
  trauma-based shake). Đã gỡ `com.unity.cinemachine` khỏi manifest.
- Shake/recoil: sample từ **Texture2D bạn tự assign trong Inspector** (2 texture noise CC0 đã có sẵn ở
  `Assets/_Project/Art/Textures/Noise/`), TUYỆT ĐỐI không dùng `Mathf.PerlinNoise`/`Random` procedural.
  Xem `NoiseTextureSampler.cs`.
- Player cầm súng + IK: dùng Animation Rigging package (đã cài), tự viết Two-Bone IK + Multi-Aim
  Constraint trong Editor — KHÔNG dùng MoreMountains TopDownEngine dù nó có sẵn giải pháp drop-in
  (quyết định trực tiếp của user: "vụ IK này ko khó, tự làm được").
- BillGameCore (từ TOSSZONE) mang nguyên khối kèm BillInspector — đã fix xong asmdef boundary issues.
- VAT + VATEnemy (baked data, gồm Kaiju boss cho Level 2) lấy từ `D:\Project\My project (1)` —
  KHÔNG phải từ `ShadersLab-BillShader` (đã sửa nhầm lẫn này ở B4).
- Character model: đã import "3D Characters Pro - Fantasy" (Layer Lab) vào
  `Assets/ThirdParty/Layer Lab/3D Casual Character/` — dùng model này cho soldier.
- Gun visual: dùng model trong `Assets/ThirdParty/Low Poly Pistol Weapon Pack 1/`,
  `Low Poly ShotGun Weapon Pack 1/`, `Low Poly Weapon Pack 4_MW_1/`, `Low Poly Weapons VOL.1/`.

## Vấn đề môi trường cần biết

- Cầu nối `unity-mcp`/`unity-skills` để điều khiển Editor trực tiếp CHƯA hoạt động được trong các
  session trước — package đã resolve xong (`com.coplaydev.unity-mcp` trong manifest) nhưng REST
  bridge chưa phản hồi. Nếu ở máy mới có bridge hoạt động, tận dụng để tự lắp Editor thay vì làm thủ
  công theo checklist.
- Máy mới clone về sẽ có Library rỗng — Unity cần thời gian import lại toàn bộ asset (project khá
  nặng, có Low Poly Weapon Packs + Layer Lab character pack + VATEnemy baked data, tổng ~600MB+ qua
  Git LFS) và resolve lại git packages (toon-kit, unity-mcp) — cần internet, và cẩn thận nếu mở nhiều
  Unity instance cùng lúc trên project này (sẽ bị lock, không batch-mode compile-check được).

## Việc cần làm tiếp theo (chọn 1 hoặc theo yêu cầu mới của user)

A. Lắp Editor cho Phase 1+2 theo `Docs/EDITOR_SETUP_CHECKLIST.md`, test chạy (di chuyển, bắn, ném bomb).
B. Viết tiếp code Phase 3 (Zombie AI + VAT + pooling) trong lúc chờ lắp Editor — không phụ thuộc Editor,
   làm được ngay như Phase 1/2.
C. Theo yêu cầu mới nếu user đổi hướng.
```
