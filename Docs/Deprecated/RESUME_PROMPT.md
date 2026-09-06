# Resume prompt — bối cảnh project hiện tại

Đọc theo thứ tự:

1. `AGENTS.md` và `CLAUDE.md`
2. `Docs/CURRENT_STATE.md` — trạng thái thật, có `file:line` cho từng khẳng định
3. `Docs/FRAMEWORK.md` — cách viết code (BillGameCore, BillTween, asmdef)
4. `Docs/REMAINING_FEATURES.md` — việc còn lại đã xếp ưu tiên
5. Doc chuyên đề theo việc đang làm — mục lục ở `Docs/README.md`

Rồi **verify lại** bằng git status, Unity, GitNexus và source thật trước khi hành động.
Doc là điểm khởi đầu, không phải bằng chứng.

## Luật cứng phải nhớ

- Không sửa `Menu.unity` hay `Assets/_Project/UI/Prefabs/Screens/UI_*.prefab`. Owner tự vẽ UI.
  Việc UI duy nhất được phép: code tween/animation (.cs) khi được yêu cầu rõ.
- Không DOTween. Không rebuild Player skeleton/Animator/WeaponRig — đọc `PlayerRigSocketIncident.md`.
- Không coi GUID chung của FBX Casual là identity của item; identity là `itemId`.
- Không stage/commit/push trừ khi được yêu cầu. Không dọn worktree khi chưa chứng minh được
  quyền sở hữu của từng thay đổi.
- Play test phải bắt đầu từ `Bootstrap.unity`.

## Ba việc đứng đầu hàng đợi

1. **Nối audio** — 970 clip đã có, gameplay mới phát 3 tiếng (`REMAINING_FEATURES.md` P0).
2. **Đóng vòng lặp run** — campaign selector → level id thật → result screen → perk có tác dụng
   (P1). Hiện Level 2–5 không vào được, mission và first-clear reward không bao giờ chạy.
3. **Hardening** — tắt `ZW_CHEATS`, đổi bundle id, chuyển 5 map về text serialization,
   lấy số profiler đầu tiên (P6).
