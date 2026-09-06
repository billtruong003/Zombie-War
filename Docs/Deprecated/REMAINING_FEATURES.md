# Zombie War — việc còn lại

> **REFERENCE ONLY — không còn quyết định thứ tự thi công.** Từ 2026-08-07, scope, priority,
> milestone và acceptance gate nằm duy nhất ở `MVP_SHIP_PLAN.md`. File này giữ chi tiết kỹ thuật
> và bằng chứng gap G1…G10 để phục vụ implementation.

**Cập nhật:** 2026-07-31 · dựng từ audit source thật, không chép doc cũ.
Bằng chứng chi tiết cho từng mục: `CURRENT_STATE.md` §3 (mã G1…G10).

Nguyên tắc xếp thứ tự: **ưu tiên thứ đã sản xuất xong nhưng chưa cắm dây**, vì nó cho nhiều
cảm giác "game thật" nhất trên mỗi giờ công.

---

## P0 — Nối audio (gap G1)

Nội dung đã có sẵn: 970 clip / 330 cue key. Runtime mới phát 3 tiếng. Đây là việc rẻ nhất mà
đổi chất lượng cảm nhận nhiều nhất.

- [ ] Thêm field sfx vào `ZombieData` (aggro/attack/hurt/death) — hiện chưa có field nào.
- [ ] Nối sfx theo các seam đã tồn tại:

  | Seam | File:line | Cue |
  |---|---|---|
  | Zombie trúng đạn | `ZombieBase.cs:379` | `sfx.impact.flesh.*` / `.bone` / `.fur` / `.plant` |
  | Zombie chết | `ZombieBase.cs:476` | `sfx.zombie.<species>.death` |
  | Nhặt pickup | `Pickup.cs:124` | `sfx.pickup.collect.*` |
  | Player trúng đòn | `Health.OnDamaged` | `sfx.player.hurt.light` / `.heavy` |
  | Player chết | `PlayerController.cs:54` | `sfx.player.death` |
  | Bắt đầu / hết wave | `WaveDirector.cs:91`, `:99` | `stinger.wave.start` / `.clear` |
  | Nút UI, gacha, currency | `UIFx`, `ButtonRelay`, `ShopScreen` | 89 key `ui.*` |
  | Ambience theo map | scene / `GameBootstrap` | `amb.stage<N>.*.base_loop` |

- [ ] Nhạc: `Bill.Audio.PlayMusic(key, fade)` cho menu và in-run.
- [ ] Voice limiter theo nhóm (súng 6 · zombie vocal 8 · footstep 6 · impact 10) — thiết kế đã
      chốt ở `AUDIO_CONTENT_AND_SETTINGS_PLAN.md` §4, chưa implement.
- [ ] Volume setting phải sống qua restart (hiện chỉ nằm trong RAM).

## P0b — Haptics (gap G5)

- [ ] Một service rung nhỏ đọc `PlayerPrefs["haptics"]`, gọi ở hit / pickup / bomb / player death.
      Toggle UI đã có sẵn ở `RunOverlays.cs:92-93, 105-108` nhưng hiện không điều khiển gì.

---

## P1 — Đóng vòng lặp run (gap G2, G3, G4, G6, G7)

Làm đúng thứ tự nhân-quả, không đảo — mỗi bước mở khoá bước sau.

1. [ ] **Level select.** Gọi `GameFlow.SelectLevel(level)` trước `StartGameplay()`.
       Màn campaign đẹp là việc của owner; tối thiểu bind `PlayerProfile.LastSelectedLevelId`
       để `RunState.LevelId` khác rỗng. Mở khoá: Level 2–5, first-clear reward, mission.
2. [ ] **Sửa `RunDirector.Finish`** — fire `RunFinishedEvent` cho **mọi** outcome, đặt trước
       nhánh victory-only (`RunDirector.cs:56, 61, 70`).
3. [ ] **Result screen** — `GameOverScreen`/`VictoryPanel` đã dựng sẵn trong scene; chỉ cần khai
       thêm field TMP_Text trong `RunOverlays` và đổ `RunSummary` vào (dữ liệu đã đủ ở
       `RunState.cs:165`). Payout đếm dồn theo `DESIGN_BRIEF` luật 8.
4. [ ] **Perk thật** — `LevelUpOverlay` + 3 nút `Perk0/1/2` đã có sẵn trong scene và đã bind vào
       `RunOverlays.perkButtons`. Cần: `AddXp` trả levels → `ShowLevelUp()` → `RunPerkPool.Draw(3)`
       → đổ tiêu đề/mô tả lên 3 nút → `AddPerk`.
5. [ ] **Perk phải có tác dụng** — nhân `RunState.Multiplier(Damage/FireRate)` vào
       `Weapon.cs:293, 366`; `MoveSpeed` vào `PlayerMovement`; `MaxHealth` vào `Health`.
       Không có bước này thì perk chỉ là số trong bảng.
6. [ ] Gọi `MissionTracker.ReportPerkChosen()` / `ReportWeaponSwitched()` / `ReportBossDefeated()`
       — 3 hook đã khai nhưng chưa ai gọi (`MissionTracker.cs:78/81/84`).
7. [ ] `BombThrower` subscribe `BombPickedUpEvent` (`Pickup.cs:120` đã fire, chưa ai nghe).
8. [ ] HUD: thanh XP/level + kill counter (`HudController` hiện không có).

---

## P2 — Boss moment và readability

- [ ] Stinger `stinger.boss.spawn` + thanh máu boss + hit-stop ngắn khi boss chết.
- [ ] Banner wave rõ hơn text pill hiện tại.
- [ ] Telegraph màu/threat rõ hơn trước khi thêm post-processing (theo `TOON_DEPTH_AO_OUTLINE_DECISION.md`).

## P3 — Horde pressure

Thiết kế đã chốt ở `HORDE_DIFFICULTY_AND_SPAWN_SAFETY.md`, chưa implement:
theo dõi `alive/visible/reserve`, recovery spawn khi màn hình thiếu áp lực, spawn ngoài camera
theo sector, breather ngắn thay vì màn hình trống 5–8 giây.

## P4 — In-run skill/stat

Thiết kế đã chốt ở `IN_RUN_SKILL_STAT_AND_INTERACTIVE_DESIGN.md`: mỗi súng 3 skill, một run mở tối
đa 3 skill, skill nạp khi súng đang cất. Phụ thuộc P1 (perk/level-up phải chạy trước).

## P5 — Food buff

Spec đã duyệt ở `FOOD_BUFF_SPEC.md` (shield 150 cap, ammo vô hạn 8s, 2× coin 20s). Chưa implement.
Phụ thuộc P1 bước 7 (bomb pickup) vì dùng chung đường pickup.

---

## P6 — Hardening trước bất kỳ bản phát hành nào (gap G8, G9)

- [ ] Tắt `ZW_CHEATS` cho build release (`ProjectSettings.asset:833-835`).
- [ ] Đổi bundle id khỏi template default (`ProjectSettings.asset:170`).
- [ ] Chuyển `Map_Level1..5.unity` về text serialization — hiện là binary nên không
      diff/merge/review được bằng git. Nội dung scene thì đúng (đã verify qua MCP,
      `CURRENT_STATE.md` §2.5); đây thuần là vấn đề quy trình review.
- [ ] Profiler 25/50/100 horde trên thiết bị thật. Chưa có **bất kỳ** số đo nào trong repo.
- [ ] Sửa `Weapon.cs:192` deref `Current.range` không null-check.
- [ ] Soak test 15–30 phút mỗi map + stuck recovery cho zombie rớt khỏi NavMesh
      (`TASK1_SPAWN_NAVMESH_GENERATOR.md`).

---

## Việc chưa xếp lịch

- Pass reward TRACK (6 tile) + premium strip vẫn là presentation. Mission thì thật.
  `PassScreen.XpPerLevel` (500) là số tạm.
- Revive bằng rewarded ad: chưa có ad SDK, `RunOverlays.cs:71-75` là placeholder trung thực.
- Phase 7 cleanup: chứng minh không còn phụ thuộc Fantasy trước khi xoá asset rollback.
- Addressables cho content nặng (texture enemy, music) — quyết định ở cuối, sau khi content ổn định.

## Checklist design còn trống

- [ ] Bảng role/stat/economy cho đủ 25 súng.
- [ ] Baseline người chơi + các bậc power đạt tới được.
- [ ] Bảng HP/damage/speed/reward theo archetype enemy.
- [ ] Đường cong wave + mốc elite/boss.
- [ ] Đường cong XP/perk/drop/reward.
- [ ] Thời lượng run mục tiêu và nhịp mua sắm kỳ vọng.
