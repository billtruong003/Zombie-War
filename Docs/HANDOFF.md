# HANDOFF — P0 Core Loop Closure

> Điểm dừng hiện tại + cách tiếp tục. Đọc kèm `Docs/PRODUCT_ROADMAP.md` (scope tổng tới publish)
> và `Docs/GAMEPLAY_DESIGN.md` (design gốc).

## 📍 Đang ở đâu
Foundation xong (player, weapons, zombies, waves, architecture, FX, tools).
Vừa **đóng được vòng lặp cơ bản**: bắn → zombie mất máu → player bị đánh → chết → GameOver.

### Vừa làm (commit này)
- ✅ `PlayerController` (`Assets/_Project/Scripts/Runtime/Gameplay/PlayerController.cs`): route damage → HUD health bar, xử lý death → GameOver state.
- ✅ Docs: `PRODUCT_ROADMAP.md` (phase tới publish), `HANDOFF.md` (file này).
- 📦 Asset mới import: `Assets/KayKit/`, `Assets/DuNguyn/` (chưa wire vào gameplay).

## 🎯 P0 còn lại (làm tiếp theo, đúng thứ tự)
1. ⬜ **Damage number popup** — pool + TextMeshPro 3D (world-space, KHÔNG dùng UI Canvas) + tween up/fade.
   - Hook: `ZombieBase.HandleDamaged(float amount)` (~line 219) → spawn popup tại `transform.position`.
   - Dùng `PoolService`: `Register(key, prefab, warmCount)` → `Spawn<T>(key, pos, rot)` → `Return(obj, delay)`.
   - Prefab đặt ở `Assets/_Project/Prefabs/FX/`.
2. ⬜ **Hit feedback** — flash/impact khi zombie trúng đạn (material flash hoặc VFX ngắn).
3. ⬜ **Convert endless** — bỏ win-condition; chết = hết run (chuẩn bị cho P1 score).
4. ⬜ **Verify loop end-to-end** — spawn → hit → damage number → HP bar tụt → chết → GameOver → restart.

## ➡️ Sau P0 (xem roadmap)
- **P1** Endless combat: difficulty scaling + score/combo + GameOver highscore.
- **P2** Loot & economy: drop table + pickups (coin/máu/dmg/ammo/XP) + magnet.

## ⚙️ Ràng buộc kỹ thuật (BẮT BUỘC theo)
- **Correct project**: `Assets/_Project/...` là code game chính. VAT/enemy ref đã chốt đúng.
- **KHÔNG raw `Instantiate`/`Destroy`** cho vật thể lặp lại → dùng `Bill.Pool` (`PoolService`, `PooledBehaviour`).
- **De-singleton**: đi qua Bill services (Events/Pool/State/Audio/Scene). Không thêm singleton mới.
- **VAT enemies**: animation baked vào vertex — không dùng Animator cho zombie.
- **Player**: Humanoid + Animation Rigging (Multi-Aim/two-bone/foot IK). KHÔNG gắn RigBuilder kiểu cũ gây crash (đã fix #3).
- Sự kiện gameplay qua `Bill.Events` (Fire/Subscribe/Unsubscribe).

## ▶️ Cách chạy lại
```
git pull && git lfs pull
```
- Mở scene bootstrap → Play (bootstrap tự load Menu → Gameplay).
- Scene chính: `Assets/_Project/Scenes/` (Bootstrap + Map_Level1).
- Nếu Unity báo compile/rig lỗi: kiểm tra Console, phần lớn lỗi rig/param đã xử lý (xem tasks #25-28).

## 🔑 File hay đụng
| Việc | File |
|---|---|
| Player | `Assets/_Project/Scripts/Runtime/Gameplay/PlayerController.cs`, `PlayerMovement.cs` |
| Weapon | `Assets/_Project/Scripts/Runtime/Gameplay/Weapon.cs`, `WeaponIKController.cs` |
| Zombie | `Assets/_Project/Scripts/Runtime/Gameplay/Zombies/ZombieBase.cs` |
| Waves | `Assets/_Project/Scripts/Runtime/Gameplay/Waves/WaveDirector.cs` |
| Pool/services | `Assets/ThirdParty/BillGameCore/Runtime/...` |
| HUD | `Assets/_Project/Scripts/Runtime/UI/HudController.cs` |
