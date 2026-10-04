# HordeCall (Zombie War) Docs

**Phase:** SHIP · **Active checkpoint:** Meta v2, phases A–E, FTUE v3 and fix plan G0–G12 delivered (2026-10-04) · next: Android device build + profiling, cleanup, balance · **Updated:** 2026-10-04

> Đọc trước: khối 2026-10-04 đầu `CURRENT_STATE.md` (trạng thái có `file:line`), mục "Current plan — 2026-10-04"
> đầu `MVP_SHIP_PLAN.md` (việc tiếp theo), khối đầu `GAME_DESIGN.md` (thiết kế đã đổi). M6 bên dưới là lịch sử
> ở những chỗ ba khối đó đã thay (payout 100 %, không Blueprint/Relic, gacha đang chạy, 6 map bake sẵn).

Top-level chứa authority cốt lõi và hai artifact được editor tool sinh tự động. Design chi tiết và
execution plan nằm dưới `Reference/Design/` và `Plans/`, nhưng được index tại đây.

## Start here

1. [`M6_ENDLESS_RUN_SYSTEM_DESIGN.md`](M6_ENDLESS_RUN_SYSTEM_DESIGN.md) — **M6 design, LOCKED**
   (endless run, one-weapon contract, card pool, pickups, interactives, economy, settlement).
   Section 1 records the seven owner decisions (`W1`-`W7`); §1c–§1e hold the three deltas they created.
   **Đã bị thay một phần (2026-09-27):** kinh tế W6 (Blueprint, Relic) không làm; payout 100 %; gacha đang chạy. Items still marked `PROPOSAL`, `HYPOTHESIS` or `TUNING`
   are design work inside a locked frame, not open questions about the frame.
   M6.1 Weapon Factory evidence lives under [`Review/M6_WeaponFactory/`](../Review/M6_WeaponFactory/);
   M6.2 decision-lock data under [`Review/M6_DecisionLock/`](../Review/M6_DecisionLock/).
2. [`GAME_DESIGN.md`](GAME_DESIGN.md) — canonical game vision, player fantasy, world và content philosophy.
3. [`WORLD_STREAMING_TECHNICAL_DESIGN.md`](WORLD_STREAMING_TECHNICAL_DESIGN.md) — canonical procedural world, chunk streaming và rendering architecture.
4. [`MVP_SHIP_PLAN.md`](MVP_SHIP_PLAN.md) — canonical execution order; mục "Current plan — 2026-10-04" là kế hoạch sống duy nhất.
5. [`CURRENT_STATE.md`](CURRENT_STATE.md) — trạng thái đã soát (khối 2026-10-04 mới nhất); claim cũ phải nhường source.
6. [`M5_INPLAY_SYSTEMS_AUDIT.md`](M5_INPLAY_SYSTEMS_AUDIT.md) — audit in-play systems (2026-08-12): symptoms đo được, GDD-vs-code trace, backlog M5.1/M6.
7. [`Reference/Design/COMBAT_GRAMMAR.md`](Reference/Design/COMBAT_GRAMMAR.md) — combat detail còn hiệu lực khi không mâu thuẫn với GDD mới.
8. [`FRAMEWORK.md`](FRAMEWORK.md) — BillGameCore/BillTween/BillInspector và luật implementation.

`README.md` là bản đồ tài liệu; không chứa requirement riêng.

Hai file generated giữ ở top-level vì editor tooling đang ghi trực tiếp vào đúng đường dẫn này:

- `ENEMY_ROSTER_AUDIT.md`
- `WeaponRosterMapping.json`

Hai file lịch sử ở top-level, không có authority:

- `DESIGN_BRIEF.md` — tầm nhìn cũ, tự ghi SUPERSEDED 2026-08-09.
- `CURRENT_GAME_DESIGN_AUDIT.md` — audit 2026-08-09, trước M6–M10.

## Các khu vực còn lại

| Thư mục | Dùng khi nào | Quyền quyết định |
|---|---|---|
| [`Reference/`](Reference/README.md) | Cần design, technical, UI hoặc audio detail | Reference; không tự mở scope |
| [`Plans/`](Plans/ZOMBIE_WAR_GAMEPLAY_EXECUTION_PLAN.md) | Điều tra execution plan cũ đã bị endless-run direction thay thế | Historical; không mở scope |
| [`Icebox/`](Icebox/README.md) | Tra ý tưởng/spec đã giữ lại sau MVP | Không được thi công trong MVP |
| [`Deprecated/`](Deprecated/README.md) | Điều tra lịch sử hoặc quyết định cũ | Không có authority |

## Authority order

Khi tài liệu mâu thuẫn, dùng thứ tự:

0. Quyết định owner mới nhất, ghi ở khối đầu của `CURRENT_STATE.md` / `GAME_DESIGN.md` / `MVP_SHIP_PLAN.md`.
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
