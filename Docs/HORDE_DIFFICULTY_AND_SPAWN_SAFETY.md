# Zombie War — Horde density, difficulty curve và spawn safety

**Trạng thái:** PROPOSED — chờ duyệt  
**Phạm vi:** design/acceptance criteria. Chưa sửa WaveData, WaveDirector, scene hay spawner.

## 1. Vấn đề đã xác nhận

Campaign hiện có 61 / 72 / 90 / 103 / 92 quái mỗi stage. `maxConcurrent` chủ yếu chỉ 6–18 và `spawnInterval` khoảng 0.65–1.20 giây. Flow hiện tại:

1. Spawn từng con.
2. Chạm `maxConcurrent` thì ngừng spawn.
3. Spawn hết queue.
4. Chờ `AliveCount == 0`.
5. Nghỉ 5–8 giây rồi mới qua wave.

Kết quả là màn hình thường thưa và có nhiều đoạn trống.

Spawn hiện chỉ lấy Transform/ring point rồi gọi `NavMesh.SamplePosition` trong bán kính tối đa 4 m. Nó **chưa kiểm tra**:

- Điểm có nằm trong collider đá/prop không.
- Capsule của zombie có đủ khoảng trống không.
- NavMesh path từ điểm spawn tới player có `PathComplete` không.
- Điểm spawn có đang lộ ngay trong camera không.
- Fixed spawn point có bị author đè lên rock sau khi map thay đổi không.

Do `SamplePosition` có thể trả về NavMesh gần rock thay vì một chỗ thực sự đủ trống, lỗi zombie spawn trên/kẹt cạnh đá là phù hợp với logic hiện tại.

## 2. Mục tiêu cảm giác

- Sau 3 giây đầu wave, player gần như luôn thấy hoặc sắp thấy một nhóm zombie.
- Không tạo quãng chết chỉ vì queue đang chờ cap.
- Không spawn “pop” ngay trước mắt. Quái xuất hiện ngoài camera rồi tiến vào nhanh.
- Breather vẫn tồn tại nhưng là giảm áp lực 2–3 giây, không phải màn hình sạch hoàn toàn 5–8 giây.
- Nhiều quái yếu tạo cảm giác horde; quái mạnh tạo quyết định. Không tăng khó chỉ bằng HP.

## 3. Pressure contract

Director theo dõi ba số:

- `alive`: tổng zombie đang sống.
- `visible`: zombie nằm trong camera và đủ gần để tạo áp lực.
- `reserve`: zombie ngoài màn hình nhưng có thể vào camera trong khoảng 1–3 giây.

Mỗi wave có:

- `targetAlive`: lượng quái muốn duy trì.
- `visibleFloor`: số quái tối thiểu nên có trên màn hình.
- `reserveFloor`: số quái chờ ngay ngoài camera.
- `spawnBudgetPerSecond`: tốc độ bơm quái tối đa.

Nếu `visible < visibleFloor` và queue còn:

1. Bỏ qua interval thường và dùng `recoveryInterval`.
2. Spawn thành mini-batch 2–4 con ở nhiều hướng.
3. Ưu tiên điểm ngoài camera có thời gian tới player ngắn.
4. Dừng boost khi `visible + reserve` hồi về target.

`visibleFloor` là target mềm, không phải lý do teleport quái hoặc spawn ngay trong camera.

## 4. Curve đề xuất

Đây là target cho prototype high-density; phải profiler trên mobile trước khi khoá.

| Stage | Tổng quái mục tiêu | Target alive | Visible floor | Reserve floor | Spawn interval thường | Recovery interval |
|---|---:|---:|---:|---:|---:|---:|
| 1 | 130–160 | 20–28 | 8 | 6 | 0.40–0.55s | 0.15s |
| 2 | 180–220 | 28–36 | 10 | 8 | 0.32–0.48s | 0.12s |
| 3 | 230–280 | 34–44 | 12 | 10 | 0.28–0.42s | 0.10s |
| 4 | 280–340 | 40–52 | 14 | 12 | 0.24–0.38s | 0.09s |
| 5 | 340–420 | 48–64 | 16 | 14 | 0.20–0.34s | 0.08s |

### Luật tăng khó trong một stage

- 20% đầu: nhiều walker, dạy nhịp và tạo cảm giác đông.
- 20–60%: thêm runner/pouncer theo cụm nhỏ.
- 60–85%: thêm ranged/burrower để ép di chuyển.
- 85–100%: elite/boss cùng một lớp zombie yếu liên tục; boss không bao giờ đứng một mình.

### Breather mới

- Giữa wave: 2–3 giây.
- Trong breather vẫn giữ 3–6 zombie yếu/reserve xa, trừ lúc mở modal reward.
- Wave sau được pre-spawn reserve trong 0.5 giây cuối để không có màn hình trống.
- Boss intro tối đa 2 giây; minion đã ở vị trí ngoài camera trước khi boss vào.

## 5. Tăng số lượng mà không phá balance

Không nhân toàn bộ quái hiện tại lên rồi giữ nguyên reward. Cần chia vai trò:

- **Horde unit:** HP thấp hơn, reward nhỏ, số lượng lớn.
- **Threat unit:** runner/ranged/burrower giữ stat và reward gần hiện tại.
- **Elite/boss:** số lượng ít, telegraph rõ.

Khi tăng tổng quái 3–4 lần:

- Tổng coin/XP của stage chỉ nên tăng khoảng 25–50%, không tăng tuyến tính theo số con.
- Horde unit dùng reward fraction/roll hoặc gom reward theo nhóm.
- Giảm chi phí AI/VAT tick cho zombie xa; giữ Full tier cho con gần player.
- Pool warmup theo `targetAlive + reserve`, không giữ mặc định 8 mỗi type.

## 6. Spawn ring theo camera

Không chọn ring thuần quanh player. Chọn vùng spawn theo camera:

- `innerBand`: ngay ngoài mép camera, khoảng 1.5–3 m. Dùng khi cần hồi pressure nhanh.
- `normalBand`: ngoài camera, khoảng 3–7 m. Dùng mặc định.
- `farBand`: 7–12 m. Dùng ranged/boss intro hoặc reserve.

Quy tắc:

- Candidate phải nằm ngoài viewport có margin, ví dụ viewport x/y ngoài `[-0.08, 1.08]`.
- Không spawn tất cả cùng một hướng; dùng sector cooldown.
- Không spawn ngay phía sau player quá thường xuyên.
- Ranged cần vị trí có path hoàn chỉnh và khoảng cách chiến đấu phù hợp.
- Boss dùng authored zone nhưng vẫn chạy toàn bộ validation.

## 7. Spawn safety pipeline bắt buộc

Một candidate chỉ hợp lệ khi qua đủ các bước:

1. **NavMesh sample:** tìm điểm trên đúng walkable area với bán kính nhỏ, không dùng 4 m để “nhảy” qua vật cản.
2. **Ground ray:** raycast xuống để xác nhận mặt đất và lấy normal.
3. **Slope:** từ chối slope vượt giới hạn agent.
4. **Clearance:** `Physics.CheckCapsule/OverlapCapsule` theo radius/height thật của loại zombie.
5. **Obstacle reject:** từ chối Rock, Cliff, Prop, Barrel, Crate và mọi collider không thuộc Ground.
6. **Edge clearance:** điểm không quá sát mép NavMesh; sample thêm vòng quanh capsule.
7. **Complete path:** `NavMesh.CalculatePath` tới vùng gần player và bắt buộc `PathComplete`.
8. **Camera rule:** ngoài camera với margin, trừ spawn scripted có telegraph rõ.
9. **Distance rule:** không quá gần player, không xa đến mức mất hơn 3 giây mới tạo pressure.
10. **Spacing:** không chồng capsule với zombie vừa spawn hoặc candidate cùng batch.

Nếu fail:

- Thử candidate khác tối đa 16–24 lần.
- Thử band/sector khác.
- Dùng danh sách `lastKnownSafePoints`.
- Nếu vẫn fail: bỏ spawn lần đó và log warning có stage/wave/type/reason; tuyệt đối không spawn ở raw Transform.

## 8. Fixed spawn point phải được validate

Mỗi fixed point cần Gizmo:

- Xanh: hợp lệ.
- Vàng: path dài/xa hoặc sát mép.
- Đỏ: nằm trong rock/prop, ngoài NavMesh hoặc path incomplete.

Editor validation phải chạy khi save/generate map và báo:

- Tên point.
- Collider đang đè lên point.
- Khoảng cách tới NavMesh.
- Path status tới tâm arena.
- Clearance radius còn lại.

Map không được pass validation nếu số safe point thấp hơn:

- 12 point thường.
- Ít nhất 3 point ở mỗi quadrant.
- 4 point dành cho boss/large agent.

Khi generator đặt rock/prop, occupancy của spawn zone phải được reserve trước. Không được đặt scenery lên vùng capsule của spawn point.

## 9. Acceptance tests

### Spawn correctness

- Chạy 1,000 lần lấy candidate trên mỗi map, 0 điểm overlap rock/prop.
- Mọi spawn có `PathComplete` tới player.
- Zombie lớn dùng đúng capsule lớn, không reuse clearance của zombie nhỏ.
- Không zombie nào đứng bất động quá 2 giây vì invalid path sau spawn.

### Horde feel

- Sau warm-up, `visible` không dưới floor quá 1.5 giây khi queue còn.
- Không có đoạn `visible == 0` quá 1 giây trong wave thường.
- Boss luôn có ít nhất 6 minion/reinforcement theo target của stage.
- Không nhìn thấy zombie pop-in trong camera.

### Performance

- Test stage 5 ở 64 alive + pickups + impacts.
- Không allocation tăng đều theo thời gian.
- FPS target của thiết bị thấp nhất vẫn đạt yêu cầu project.
- Audio voice limiter vẫn giữ được gunfire, player hurt và boss warning.

## 10. Thứ tự implementation sau khi duyệt

1. Thêm spawn validation + editor gizmo trước.
2. Chuyển ring spawn sang camera bands và sector.
3. Thêm pressure contract/visible-reserve tracking.
4. Tune WaveData từ Stage 1 tới Stage 5.
5. Profiler, sau đó mới khoá số lượng và reward.

