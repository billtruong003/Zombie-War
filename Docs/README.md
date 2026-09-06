# Zombie War Docs

**Phase:** SHIP · **Active checkpoint:** **M6 LOCKED** (W1–W7 answered 2026-08-15; three CHANGE answers designed as Deltas A/B/C) · M7 not started, not authorized · **Updated:** 2026-08-15

Top-level chứa authority cốt lõi và hai artifact được editor tool sinh tự động. Design chi tiết và
execution plan nằm dưới `Reference/Design/` và `Plans/`, nhưng được index tại đây.

## Start here

1. [`M6_ENDLESS_RUN_SYSTEM_DESIGN.md`](M6_ENDLESS_RUN_SYSTEM_DESIGN.md) — **M6 design, LOCKED**
   (endless run, one-weapon contract, card pool, pickups, interactives, economy, settlement).
   Section 1 records the seven owner decisions (`W1`-`W7`) as **answered and OWNER-LOCKED**; §1c–§1e hold
   the three deltas the CHANGE answers created — onboarding gate + tier ladder, replacement economy with
   Blueprint, and H2 approved for production. Items still marked `PROPOSAL`, `HYPOTHESIS` or `TUNING`
   are design work inside a locked frame, not open questions about the frame.
   M6.1 Weapon Factory evidence lives under [`Review/M6_WeaponFactory/`](../Review/M6_WeaponFactory/);
   M6.2 decision-lock data under [`Review/M6_DecisionLock/`](../Review/M6_DecisionLock/).
2. [`GAME_DESIGN.md`](GAME_DESIGN.md) — canonical game vision, player fantasy, world và content philosophy.
3. [`WORLD_STREAMING_TECHNICAL_DESIGN.md`](WORLD_STREAMING_TECHNICAL_DESIGN.md) — canonical procedural world, chunk streaming và rendering architecture.
4. [`MVP_SHIP_PLAN.md`](MVP_SHIP_PLAN.md) — canonical execution order, active checkpoint và Claude/Codex review protocol.
5. [`CURRENT_STATE.md`](CURRENT_STATE.md) — baseline implementation; claim cũ phải nhường source và report đã verify.
6. [`M5_INPLAY_SYSTEMS_AUDIT.md`](M5_INPLAY_SYSTEMS_AUDIT.md) — audit in-play systems (2026-08-12): symptoms đo được, GDD-vs-code trace, backlog M5.1/M6.
7. [`Reference/Design/COMBAT_GRAMMAR.md`](Reference/Design/COMBAT_GRAMMAR.md) — combat detail còn hiệu lực khi không mâu thuẫn với GDD mới.
8. [`FRAMEWORK.md`](FRAMEWORK.md) — BillGameCore/BillTween/BillInspector và luật implementation.

`README.md` là bản đồ tài liệu; không chứa requirement riêng.

Hai file generated giữ ở top-level vì editor tooling đang ghi trực tiếp vào đúng đường dẫn này:

- `ENEMY_ROSTER_AUDIT.md`
- `WeaponRosterMapping.json`

## Các khu vực còn lại

| Thư mục | Dùng khi nào | Quyền quyết định |
|---|---|---|
| [`Reference/`](Reference/README.md) | Cần design, technical, UI hoặc audio detail | Reference; không tự mở scope |
| [`Plans/`](Plans/ZOMBIE_WAR_GAMEPLAY_EXECUTION_PLAN.md) | Điều tra execution plan cũ đã bị endless-run direction thay thế | Historical; không mở scope |
| [`Icebox/`](Icebox/README.md) | Tra ý tưởng/spec đã giữ lại sau MVP | Không được thi công trong MVP |
| [`Deprecated/`](Deprecated/README.md) | Điều tra lịch sử hoặc quyết định cũ | Không có authority |

## Authority order

Khi tài liệu mâu thuẫn, dùng thứ tự:

1. `GAME_DESIGN.md` — vision/fantasy/world authority, và các quyết định OWNER-LOCKED của M6.
2. `M6_ENDLESS_RUN_SYSTEM_DESIGN.md` — run-system design, **đã LOCKED** (W1–W7 trả lời 2026-08-15).
   Phần gắn nhãn `PROPOSAL`, `HYPOTHESIS` hay `TUNING` vẫn chưa được chứng minh, nhưng khung thiết kế
   thì đã chốt.
3. `WORLD_STREAMING_TECHNICAL_DESIGN.md` — world/runtime architecture authority mới.
4. `MVP_SHIP_PLAN.md` — execution order, active checkpoint và evidence gate.
5. `CURRENT_STATE.md` + source/asset hiện hành — bằng chứng về implementation đang tồn tại.
6. `Reference/Design/COMBAT_GRAMMAR.md` — combat-domain detail tương thích với GDD mới.
7. `FRAMEWORK.md` — implement đúng project như thế nào.
8. Plan, brief và reference cũ — lịch sử hoặc evidence; không được phục hồi hướng map/stage cũ hay tự mở scope.

Không dùng `Icebox/` hoặc `Deprecated/` để giao task.

## Luật an toàn

- Không sửa `Menu.unity` hoặc `UI_*.prefab`; owner giữ quyền UI layout/reference.
- Không DOTween; chỉ BillTween.
- Không rebuild Player rig; đọc
  [`Reference/Technical/PlayerRigSocketIncident.md`](Reference/Technical/PlayerRigSocketIncident.md)
  trước khi chạm vùng đó.
- Playtest từ `Bootstrap.unity`.
- Impact analysis trước khi sửa symbol; `detect_changes()` trước commit.
- Không stage/commit/push nếu task không yêu cầu rõ.
