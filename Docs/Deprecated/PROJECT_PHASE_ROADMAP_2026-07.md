# Zombie War: roadmap kỹ thuật và gameplay

> **HISTORICAL ROADMAP.** Từ 2026-08-07, kế hoạch thực thi đã được thay bởi `MVP_SHIP_PLAN.md`.
> File này chỉ giữ lịch sử quyết định và bối cảnh kỹ thuật; không dùng để chọn task tiếp theo.

**Lập:** 2026-07-24 · **Đối chiếu source:** 2026-07-31 (HEAD `68fbc090`)
**Nguyên tắc:** Làm tuần tự. Mỗi phase phải qua acceptance test rồi mới mở phase tiếp.

> Trạng thái từng phase dưới đây đã được đối chiếu với source thật ngày 2026-07-31.
> Bằng chứng `file:line`: `CURRENT_STATE.md` §3. Backlog thi công: `REMAINING_FEATURES.md`.

## Phase 0: Project health

**Trạng thái (2026-07-31):** ⚠️ phần lớn xong, còn 2 điểm chưa xác minh được.

- ✅ Bootstrap → AudioService → Addressables khởi động đúng thứ tự
  (`AddressableAudioRuntime` tự bootstrap `BeforeSceneLoad`).
- ⚠️ "Không missing script trong scene gameplay" **chưa kiểm chứng được**: `Map_Level1..5.unity`
  hiện là file binary nên không review được bằng code (`CURRENT_STATE.md` §3 G8).
- ⚠️ Chưa có smoke test tự động cho vào trận / pause / resume / thoát trận.

- Không có missing script trong scene gameplay.
- Bootstrap, AudioService và Addressables khởi động đúng thứ tự.
- Console không có compile error.
- Có smoke test vào trận, pause, resume, thoát trận.

## Phase 1: Spawn, NavMesh và generator

**Trạng thái (2026-07-31):** ✅ nền tảng xong. Còn soak test.

- Sand-only NavMesh.
- Spawn capsule clearance.
- PathComplete bắt buộc.
- Generator chống overlap bằng collider footprint thật.
- Convex collider contract.
- 5 campaign map đã regenerate và rebake.

Còn lại: soak test AI, stuck recovery, spawn gizmo và camera-aware spawn.

Chi tiết: `Docs/TASK1_SPAWN_NAVMESH_GENERATOR.md`.

## Phase 2: Audio runtime và Addressables

**Trạng thái (2026-07-31):** ⚠️ hạ tầng xong, **nội dung chưa nối**.

- ✅ 970 clip / 330 cue key đã generate và đưa vào `AddressableAudioCatalog`.
- ✅ `AddressableAudioRuntime` nạp theo label vào `AudioLibrary` lúc runtime.
- ❌ Gameplay mới phát **3 cue**: fire, reload, bomb explode. Không nhạc, không ambience,
  không tiếng zombie/pickup/UI/footstep/player-hurt (`CURRENT_STATE.md` §3 G1).
- ❌ Chưa có voice limiter theo nhóm. Chưa preload theo map/loadout.
- ❌ Volume setting chưa sống qua restart.

- Hoàn thiện catalog Addressables cho SFX/music.
- Preload theo map và loadout, không load toàn bộ 1.600 clip.
- Voice limiter theo nhóm: gun, enemy, footsteps, impacts, ambience, UI.
- Crowd audio phải tạo cảm giác đông bằng cluster emitter, không phát một AudioSource cho mỗi zombie.
- Settings gồm master, music, SFX, UI, ambience và mute.
- Test mất mạng, cache thiếu và fallback local.

## Phase 3: SFX gameplay và music state

**Trạng thái (2026-07-31):** ❌ chưa bắt đầu. Đây là **P0** trong `REMAINING_FEATURES.md` vì
clip đã trả tiền xong, chỉ còn cắm dây.

- Nối sound cho từng súng, reload, dry fire, shell, bullet impact theo surface.
- Footstep player và enemy theo surface.
- Zombie vocal theo archetype, khoảng cách và pressure.
- Ambience theo map.
- Music có intro, loop, pressure layer, boss layer và outro chuyển mượt.
- QA loudness, clipping, noise và loop seam cho từng nhóm sound.

## Phase 4: Horde pressure và difficulty

**Trạng thái (2026-07-31):** ❌ chưa implement. Thiết kế đầy đủ ở
`HORDE_DIFFICULTY_AND_SPAWN_SAFETY.md` (vẫn PROPOSED).

- Tăng tổng số zombie và `maxConcurrent` theo profiler budget.
- Theo dõi alive, visible và reserve.
- Recovery spawn khi màn hình thiếu pressure.
- Spawn ngoài camera theo sector, không pop trước mặt player.
- Breather ngắn nhưng không để màn hình trống lâu.
- Boss luôn có lớp minion hỗ trợ.

Không tăng reward tuyến tính theo số zombie. Horde unit phải có reward nhỏ hơn threat unit.

## Phase 5: Core in-run progression

**Trạng thái (2026-07-31):** ⚠️ backend một nửa, **không có tác dụng trong trận**.

- ✅ `RunState` đã đếm kill/XP/level/tiền; `RunPerkPool` đã có; `Payout` idempotent.
- ❌ Không ai đọc số level lên → overlay level-up không bao giờ mở (`RunOverlays.cs:211`).
- ❌ `RunState.Multiplier()` không có consumer runtime → perk chọn xong vẫn vô tác dụng.
- ❌ HUD không có thanh XP/level.
- ❌ Hệ 3-skill-mỗi-súng (`IN_RUN_SKILL_STAT_AND_INTERACTIVE_DESIGN.md`) chưa bắt đầu.

- Kill zombie nhận XP và lên level trong trận.
- Mỗi súng có 3 skill hỗ trợ riêng.
- Mỗi lần lên level chỉ chọn một nâng cấp.
- Một run có thể thấy tối đa 9 skill từ loadout nhưng chỉ đầu tư tối đa 3 skill.
- Skill hỗ trợ cách chơi của súng. Sát thương chính vẫn đến từ súng và rarity.
- Thùng đồ có thể cho một lượt nâng skill. Khi skill đã max, reward chuyển sang stat.
- Damage có khoảng dao động và crit thay vì một số cố định.

Chi tiết: `Docs/IN_RUN_SKILL_STAT_AND_INTERACTIVE_DESIGN.md`.

## Phase 6: Visual readability

**Trạng thái (2026-07-31):** ⚠️ đã có toon rig + hit flash + dissolve. Commit `804b5d40` đã chốt
hướng toon-only (bỏ inverted hull, gỡ Beautify). AO bake và outline prototype chưa làm.

- Bake AO cho môi trường và prop.
- Tune toon ramp, shadow band và rim light.
- Prototype Bill-SSOutline theo cấu hình Roberts depth-only.
- Chỉ giữ runtime outline nếu qua GPU benchmark.
- Thêm hit flash, telegraph và màu threat rõ hơn trước khi tăng thêm post-processing.

Chi tiết: `Docs/TOON_DEPTH_AO_OUTLINE_DECISION.md`.

## Phase 7: Performance và content delivery

**Trạng thái (2026-07-31):** ❌ chưa có **bất kỳ** số đo profiler nào trong repo.
Pool đã có sẵn cho zombie/FX/tracer/damage number. Audio emitter chưa pool riêng.

- Profile CPU, GPU, memory và audio voice ở Stage 5.
- Pool zombie, projectile, impact FX, damage number và audio emitter.
- Tách Addressables local/remote theo độ cần thiết khi vào trận.
- Build nhẹ chỉ chứa bootstrap, UI cốt lõi và fallback.
- Texture enemy, SFX, music và content map có thể tải theo catalog sau.
- Có versioning, cache cleanup, download progress và retry.

## Thứ tự làm ngay (cập nhật 2026-07-31)

Thứ tự gốc vẫn hợp lý, nhưng audit ngày 2026-07-31 phát hiện vòng lặp run đang đứt ở chỗ không
ai ngờ (campaign selector không tồn tại → mission và first-clear reward không bao giờ chạy), nên
nó được kéo lên trước horde pressure:

1. **Nối SFX theo gameplay event** — rẻ nhất, đổi cảm giác nhiều nhất.
2. **Đóng vòng lặp run**: level select → `RunState.LevelId` thật → result screen → perk có tác dụng.
3. Haptics (toggle đã có, chưa nối API rung nào).
4. Soak test + stuck recovery.
5. Horde pressure.
6. Hệ XP/level/weapon skill đầy đủ.
7. Prototype AO + outline.
8. Profile, rồi mới quyết chuyển content nặng sang remote.

Chi tiết từng bước kèm `file:line`: `REMAINING_FEATURES.md`.

