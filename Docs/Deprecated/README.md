# Deprecated docs

> Tài liệu trong thư mục này bị đóng băng để tra lịch sử. Link nội bộ có thể trỏ tới layout cũ;
> không dùng bất kỳ file nào ở đây làm scope, priority hoặc bằng chứng trạng thái hiện tại.

Các tài liệu quản trị cũ được thay thế như sau:

- `PROJECT_PHASE_ROADMAP_2026-07.md` → `../MVP_SHIP_PLAN.md`
- `REMAINING_FEATURES.md` → `../MVP_SHIP_PLAN.md` + `../CURRENT_STATE.md`
- `RESUME_PROMPT.md` → `../README.md`
- `PROJECT_TRANSFER_BRIEF_2026-07-31.md` → snapshot lịch sử; trạng thái mới ở `../CURRENT_STATE.md`

**Ngày dọn:** 2026-07-31

Các file trong thư mục này **không còn là nguồn chuẩn**. Giữ lại vì chúng chứa lịch sử quyết định,
bằng chứng thực thi và design intent vẫn dùng tham khảo được. **Không thực thi** bất kỳ file nào ở
đây như một task đang mở.

Trạng thái hiện tại: `../CURRENT_STATE.md` · Việc còn lại: `../REMAINING_FEATURES.md`

| File | Là gì | Vì sao deprecate | Thay bằng |
|---|---|---|---|
| `ACCOUNT_SWITCH_HANDOFF.md` | Handoff chuyển tài khoản, 551 dòng, chốt 2026-07-23 | Trộn lẫn nhiều tầng lịch sử Fantasy/Casual nên khó dùng làm status. **Các claim kỹ thuật của nó đã được verify lại qua Unity MCP và là ĐÚNG** (RunSystems/WaveDirector/HUD có đủ trong scene) | `CURRENT_STATE.md` |
| `HANDOFF.md` | Handoff status trước migration Casual | Tự khai superseded | `CURRENT_STATE.md` |
| `HANDOFF_UI_CODEX.md` | Handoff implementation UI | Tự khai historical; số liệu Fantasy 14-slot/978-part đã sai | `UI_ARCHITECTURE.md` + `CURRENT_STATE.md` |
| `UI_DIRECTION_02_HANDOFF.md` | Đợt sửa layout menu Direction 02 | Owner tự vẽ UI từ 2026-07-23; flow agent-driven đã chết. Giữ làm design intent (English copy, accent, safe-area) | `UI_REDESIGN_SPEC.md` |
| `UIUX_WIREFRAME_PROMPT.md` | Prompt sinh wireframe ASCII | UI thật đã build; prompt này nếu chạy sẽ tạo layout sai (PLAY xanh + 5 tab) | `UI_REDESIGN_SPEC.md` |
| `UIUX_DESIGN_RATIONALE.md` | Phần "tại sao" của wireframe cũ | Bảng phase/status lỗi thời; lý luận UX vẫn đọc tham khảo được | `SCREEN_SPECS.md` |
| `DESIGN_AI_PROMPT.md` | Master prompt thuê AI designer | Archive design-input, không phải trạng thái | `DESIGN_BRIEF.md` |
| `NEXT_PHASE_UI_WIRING_PROMPT.md` | Prompt thực thi đợt wiring UI | Đã thực thi xong | — |
| `NEXT_PHASE_RUN_LOOP_PROMPT.md` | Prompt run loop (thân file rỗng) | Đã được hấp thụ vào contract enemy campaign | `REMAINING_FEATURES.md` P1 |
| `ENEMY_CAMPAIGN_EXPANSION_PROMPT.md` | Contract 615 dòng: bake VAT + 5 stage | Đã thực thi 2026-07-22 | `ENEMY_ROSTER_AUDIT.md`, `CAMPAIGN_BALANCE_TABLE.md` |
| `ENEMY_CAMPAIGN_TASK_STATE.md` | Nhật ký thực thi milestone enemy | Milestone đã đóng; giữ làm bằng chứng | `CURRENT_STATE.md` §2 |
| `TASK_BREAKDOWN.md` | Task list 2026-07-21/23 | Đã hoàn thành hoặc bị thay | `REMAINING_FEATURES.md` |
| `PRODUCT_ROADMAP.md` | Roadmap Phase A–D (2026-07-21) | Bị thay bởi roadmap 7 phase ngày 2026-07-24 | `PROJECT_PHASE_ROADMAP_2026-07.md` |
| `MAP_GENERATION_DIRECTION.md` | Khảo sát hướng làm map, "chưa implement" | Generator đã tồn tại và đã sinh cả 5 map | `TASK1_SPAWN_NAVMESH_GENERATOR.md` |

> **Đã trả lại `Docs/` (2026-07-31):** `CORE_WIRING_DIRECTIVE.md`. Deprecate nhầm — phần
> "Hard requirements" của nó (mọi thứ đi qua BillGameCore, zombie theo thừa kế, player
> spawn-based + map additive, IK) vẫn là luật kiến trúc đang có hiệu lực.

## Vẫn còn giá trị tham khảo

- `ENEMY_CAMPAIGN_TASK_STATE.md` — ghi 5 bug VAT thật đã tìm ra và cách sửa. Đọc trước khi
  đụng lại pipeline VAT.
- `UI_DIRECTION_02_HANDOFF.md` — các quyết định sản phẩm đã duyệt (copy tiếng Anh, `+` trên
  currency pill, safe-area) vẫn là ý định thiết kế hiện hành.
- `ACCOUNT_SWITCH_HANDOFF.md` §3 — hợp đồng Player/weapon rig "không được rebuild" vẫn là luật.
  Nội dung đó đã được chuyển vào `../CURRENT_STATE.md` §0 mục 7.
