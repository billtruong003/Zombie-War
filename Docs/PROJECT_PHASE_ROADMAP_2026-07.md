# Zombie War: roadmap kỹ thuật và gameplay

**Nguồn chuẩn từ:** 2026-07-24  
**Nguyên tắc:** Làm tuần tự. Mỗi phase phải qua acceptance test rồi mới mở phase tiếp.

## Phase 0: Project health

**Trạng thái:** Đã sửa phần Bill Core/audio từng bị đứt. Vẫn cần theo dõi.

- Không có missing script trong scene gameplay.
- Bootstrap, AudioService và Addressables khởi động đúng thứ tự.
- Console không có compile error.
- Có smoke test vào trận, pause, resume, thoát trận.

## Phase 1: Spawn, NavMesh và generator

**Trạng thái:** Nền tảng đã xong ngày 2026-07-24.

- Sand-only NavMesh.
- Spawn capsule clearance.
- PathComplete bắt buộc.
- Generator chống overlap bằng collider footprint thật.
- Convex collider contract.
- 5 campaign map đã regenerate và rebake.

Còn lại: soak test AI, stuck recovery, spawn gizmo và camera-aware spawn.

Chi tiết: `Docs/TASK1_SPAWN_NAVMESH_GENERATOR.md`.

## Phase 2: Audio runtime và Addressables

**Trạng thái:** Asset đã có và kiến trúc nền đã nối một phần.

- Hoàn thiện catalog Addressables cho SFX/music.
- Preload theo map và loadout, không load toàn bộ 1.600 clip.
- Voice limiter theo nhóm: gun, enemy, footsteps, impacts, ambience, UI.
- Crowd audio phải tạo cảm giác đông bằng cluster emitter, không phát một AudioSource cho mỗi zombie.
- Settings gồm master, music, SFX, UI, ambience và mute.
- Test mất mạng, cache thiếu và fallback local.

## Phase 3: SFX gameplay và music state

- Nối sound cho từng súng, reload, dry fire, shell, bullet impact theo surface.
- Footstep player và enemy theo surface.
- Zombie vocal theo archetype, khoảng cách và pressure.
- Ambience theo map.
- Music có intro, loop, pressure layer, boss layer và outro chuyển mượt.
- QA loudness, clipping, noise và loop seam cho từng nhóm sound.

## Phase 4: Horde pressure và difficulty

- Tăng tổng số zombie và `maxConcurrent` theo profiler budget.
- Theo dõi alive, visible và reserve.
- Recovery spawn khi màn hình thiếu pressure.
- Spawn ngoài camera theo sector, không pop trước mặt player.
- Breather ngắn nhưng không để màn hình trống lâu.
- Boss luôn có lớp minion hỗ trợ.

Không tăng reward tuyến tính theo số zombie. Horde unit phải có reward nhỏ hơn threat unit.

## Phase 5: Core in-run progression

- Kill zombie nhận XP và lên level trong trận.
- Mỗi súng có 3 skill hỗ trợ riêng.
- Mỗi lần lên level chỉ chọn một nâng cấp.
- Một run có thể thấy tối đa 9 skill từ loadout nhưng chỉ đầu tư tối đa 3 skill.
- Skill hỗ trợ cách chơi của súng. Sát thương chính vẫn đến từ súng và rarity.
- Thùng đồ có thể cho một lượt nâng skill. Khi skill đã max, reward chuyển sang stat.
- Damage có khoảng dao động và crit thay vì một số cố định.

Chi tiết: `Docs/IN_RUN_SKILL_STAT_AND_INTERACTIVE_DESIGN.md`.

## Phase 6: Visual readability

- Bake AO cho môi trường và prop.
- Tune toon ramp, shadow band và rim light.
- Prototype Bill-SSOutline theo cấu hình Roberts depth-only.
- Chỉ giữ runtime outline nếu qua GPU benchmark.
- Thêm hit flash, telegraph và màu threat rõ hơn trước khi tăng thêm post-processing.

Chi tiết: `Docs/TOON_DEPTH_AO_OUTLINE_DECISION.md`.

## Phase 7: Performance và content delivery

- Profile CPU, GPU, memory và audio voice ở Stage 5.
- Pool zombie, projectile, impact FX, damage number và audio emitter.
- Tách Addressables local/remote theo độ cần thiết khi vào trận.
- Build nhẹ chỉ chứa bootstrap, UI cốt lõi và fallback.
- Texture enemy, SFX, music và content map có thể tải theo catalog sau.
- Có versioning, cache cleanup, download progress và retry.

## Thứ tự làm ngay sau Task 1

1. Chạy soak test và thêm stuck recovery.
2. Khóa audio runtime + Addressables.
3. Nối SFX theo gameplay event.
4. Làm horde pressure.
5. Làm hệ XP, level và weapon skill.
6. Prototype AO + outline.
7. Profile và chuyển content nặng sang remote.

