# Task 1: Spawn, NavMesh và map generator

**Cập nhật:** 2026-07-24  
**Trạng thái:** Đã làm xong phần nền tảng. Cần playtest dài để bắt lỗi AI phát sinh khi trận đấu chạy.

## Kết quả đã chốt

- NavMesh chỉ bake từ layer `WalkableGround`. Đá, cây, thùng và vách không thể tạo thành đảo NavMesh.
- Cả ba đường tạo map dùng chung một luật bake: `SceneFlowBuilder`, `CampaignStageBuilder` và `DesertMapGeneratorWindow`.
- Generator đo footprint từ collider thật sau khi scale và xoay prefab.
- Generator dùng chung một bảng occupancy cho player, spawn point, boundary, prop và object tương tác.
- Mỗi obstacle có ít nhất một collider không phải trigger.
- MeshCollider có tối đa 255 tam giác được bật `convex`.
- Mesh quá phức tạp dùng BoxCollider thay thế để tránh lỗi convex cooking.
- Spawner dùng radius và height thật từ `NavMeshAgent` của từng loại zombie.
- Candidate chỉ hợp lệ khi capsule trống và có `PathComplete` tới player.
- Bán kính `NavMesh.SamplePosition` giảm từ 4 m xuống 1 m. Spawn point không còn được phép nhảy qua đá để tìm NavMesh gần nhất.
- Wave chỉ tăng số đã spawn khi pool trả về zombie thật. Nếu không có vị trí an toàn, hệ thống thử lại có giới hạn rồi báo lỗi rõ ràng.

## Kết quả kiểm tra tự động

| Map | Spawn clear | Path complete | Obstacle | Thiếu collider | MeshCollider non-convex | Cặp overlap |
|---|---:|---:|---:|---:|---:|---:|
| Level 1 | 12/12 | 12/12 | 213 | 0 | 0 | 0 |
| Level 2 | 12/12 | 12/12 | 224 | 0 | 0 | 0 |
| Level 3 | 12/12 | 12/12 | 211 | 0 | 0 | 0 |
| Level 4 | 12/12 | 12/12 | 226 | 0 | 0 | 0 |
| Level 5 | 12/12 | 12/12 | 222 | 0 | 0 | 0 |

`Cặp overlap` dùng `Physics.ComputePenetration` và bỏ qua các đoạn boundary chạm nhau theo thiết kế.

## Luồng spawn mới

1. Chọn authored spawn point hoặc một điểm trên vòng quanh player.
2. Sample NavMesh trong phạm vi 1 m.
3. Tạo capsule theo đúng kích thước zombie.
4. Loại điểm chạm đá, prop, thùng, zombie khác hoặc boundary.
5. Sample vị trí player trên NavMesh.
6. Chỉ nhận điểm có path hoàn chỉnh.
7. Spawn qua pool.
8. Nếu thất bại, thử candidate khác. Không spawn vào raw Transform.

## Việc còn lại của Task 1

- Chạy soak test 15 đến 30 phút trên từng map.
- Theo dõi zombie không đổi vị trí trong hơn 2 giây khi đang ở trạng thái đuổi player.
- Thêm recovery cho zombie bị đẩy khỏi NavMesh trong lúc chơi, không chỉ lúc spawn.
- Thêm gizmo xanh, vàng, đỏ cho spawn point trong Scene View.
- Kiểm tra candidate ngoài camera. Đây là bước tạo cảm giác tốt hơn, không phải điều kiện chống kẹt.
- Test riêng zombie lớn và boss với capsule lớn nhất.

## File chính

- `Assets/_Project/Scripts/Runtime/Gameplay/Waves/ZombieSpawner.cs`
- `Assets/_Project/Scripts/Runtime/Gameplay/Waves/WaveDirector.cs`
- `Assets/_Project/Scripts/Editor/MapNavigationAuthoring.cs`
- `Assets/_Project/Scripts/Editor/DesertMapGeneratorWindow.cs`

