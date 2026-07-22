# Hướng làm map — bản khảo sát, chưa implement

> Đây là tài liệu tìm hướng theo yêu cầu. Chưa viết code generation nào.
> Số liệu đo từ asset thật trong dự án, không phải ước lượng.

## 1. Khảo sát asset

### Low Poly Desert Environment (Tiny Teacup Studio)

13 prefab dùng được:

| Nhóm | Prefab | Tris | Kích thước (m) |
|---|---|---:|---|
| Nền | `Ground_01` | 100 | 5,0 × 0,2 × 5,0 |
| Vách | `Cliff_01`, `CliffCorner_01/02` | 332–894 | 5,0–7,8 rộng, cao 5,6 |
| Đá | `Rock_01..05` | 80–162 | 0,2–3,4 |
| Cây | `Cactus_01..03`, `Tree_01` | 517–2710 | 0,5–2,8 |

**Điểm quan trọng nhất: cả pack dùng chung đúng 1 material** (`Mat_01`, shader
`StylizedToonWorldKit/Toon/Toon Lit`, 1 texture `Palette`). Nền dùng `Mat_01 1`.

Nghĩa là 2 material cho toàn bộ môi trường. Đây là điều kiện lý tưởng để batch.

**`Ground_01` là tile 5×5 m** → pack này vốn được thiết kế để ghép lưới, không phải rải tự do.

### KayKit Resource Bits

132 prefab. Theo ý anh, dùng làm **props đặt lên map** (crate, thùng, bao) chứ không phải nền.
Hiện đã dùng `Money_Coins_Stack_Single` và `Gem_Small` cho pickup.

## 2. Ràng buộc thật của game

Đo từ camera gameplay: portrait, FOV 60, cao 12 m, ngẩng 60°.

- Vùng nhìn thấy trên nền: **~7,8 m ngang × ~13,9 m dọc**
- Nhìn về phía trước từ Player: chỉ **~6 m**

Sân hiện tại 75–90 m. Tức **người chơi chỉ thấy khoảng 1% diện tích sân tại một thời điểm**.

Đây là ràng buộc chi phối mọi quyết định về map:

- Chi tiết xa hơn ~8 m là **lãng phí hoàn toàn** — không bao giờ thấy.
- Ngược lại, vật cản gần phải rất "đọc được", vì người chơi không có thời gian nhìn xa để né.
- Sân 90 m gần như chắc chắn quá lớn. Nên xem lại xuống 40–50 m, hoặc dùng vùng chơi nhỏ hơn
  trong sân lớn.

## 3. Ba hướng khả thi

### Hướng A — Lưới tile 5×5 m (khuyến nghị)

Đúng thiết kế của pack. Sân 50 m = lưới 10×10 = 100 tile `Ground_01`.

Cách làm: mỗi ô lưới nhận một "biome weight" (trống / đá / cây / vách), rải prop theo mật độ
Poisson-disk để không chồng nhau, chừa hành lang thông nhau.

- Ưu: hợp asset, dễ bake NavMesh, dễ đảm bảo không kẹt đường.
- Nhược: dễ lộ tính lưới nếu không xoay/lệch ngẫu nhiên tile.

### Hướng B — Vành đai vách + lõi mở

Dùng `Cliff_*` làm biên cứng thay cho tường vô hình, giữa là sân mở rải thưa đá/cây.

- Ưu: giải quyết luôn chuyện "chạy ra rìa bản đồ", rất ít prop.
- Nhược: bản thân nó không tạo được chiều sâu chiến thuật, cần ghép với A.

### Hướng C — Vùng chơi trôi theo Player (arena thu nhỏ)

Vì chỉ thấy 1% sân, không cần sân to. Làm arena 40 m có vách bao quanh, mỗi màn một layout
authored sẵn thay vì generate.

- Ưu: rẻ nhất, kiểm soát tốt nhất, hợp mobile.
- Nhược: mất tính lặp lại vô hạn.

**Đề xuất: A cho layout + B cho biên.** C là phương án lùi nếu hiệu năng hoặc thời gian ép.

## 4. Ràng buộc bắt buộc khi generate

Rút từ những gì đã build:

1. **Không chặn đường NavMesh.** Hiện có 48 điểm spawn, tất cả phải còn đường tới Player.
   Sau khi rải prop **bắt buộc** bake lại NavMesh rồi chạy lại kiểm tra path như đã làm.
2. **Chừa vùng trống quanh điểm spawn** và quanh `PlayerSpawnPoint`.
3. **Vật cản không được cao quá tầm nhìn.** `Cliff_*` cao 5,6 m — đặt trong lòng sân sẽ che
   camera. Chỉ nên dùng ở biên.
4. **Prop phải có collider hợp lý.** Hiện `Rock_*`/`Cactus_*` dùng `MeshCollider` — nên đổi sang
   Box/Capsule khi generate, vì MeshCollider nhiều sẽ đắt.
5. **Deterministic theo seed**, để một màn luôn ra cùng layout và test được.

## 5. Draw call — trả lời câu hỏi

**Bật Static KHÔNG cho ra 1 draw call. Và KHÔNG cần light bake để giảm draw call.**

Ba cơ chế khác nhau, hay bị nhầm:

| Cơ chế | Điều kiện | Kết quả |
|---|---|---|
| **SRP Batcher** (URP, mặc định bật) | Cùng *shader variant*. Material khác nhau vẫn được | Không gộp draw call, nhưng bỏ được phần lớn chi phí set-up giữa các draw. Thường là thứ có lợi nhất trong URP |
| **GPU Instancing** | Cùng *mesh* + cùng *material* | Nhiều instance về **1 draw call** |
| **Static Batching** | Cùng *material*, có cờ Static | Gộp mesh thành buffer lớn. **Vẫn nhiều draw call**, chỉ là rẻ hơn. Tốn thêm RAM/build size vì mesh bị nhân bản |

Cụ thể cho pack desert:

- Cả pack chung 1 material → **GPU Instancing là thứ đáng dùng nhất**. 100 hòn `Rock_01` giống
  hệt nhau = 1 draw call. Đây mới là con đường về "1 draw call", không phải Static.
- Static batching sẽ gộp tất cả thành vài buffer lớn, nhưng vì mesh khác nhau nên **không về 1
  draw call** được.

**Về light bake:** lightmap **làm batching tệ đi**, không tốt lên. Static batching yêu cầu cùng
material *và* cùng lightmap; object nằm ở lightmap atlas khác nhau sẽ bị tách batch. Light bake là
chuyện chất lượng/chi phí ánh sáng, không phải chuyện draw call.

**Tắt shadow** thì đúng là giảm draw call thật — mỗi caster phải vẽ lại ở shadow pass, nên tắt
shadow cho prop môi trường cắt gần một nửa số draw của chúng. Đây là thắng lợi rõ ràng nhất và rẻ
nhất, đã áp dụng cho pickup và blob shadow.

### Khuyến nghị

1. Tắt shadow cast cho toàn bộ prop môi trường (giữ cho nền nhận bóng nếu cần).
2. Bật GPU Instancing trên `Mat_01`.
3. Bật Static cho prop **không di chuyển** — chủ yếu để hưởng occlusion culling, không phải để gộp
   draw call.
4. Chưa cần light bake. Nếu sau này muốn, chấp nhận mất một phần static batching.
5. Đo bằng Frame Debugger trước và sau, đừng tin lý thuyết.

## 6. Việc cần chốt trước khi viết code

1. Kích thước sân thật sự muốn (đề xuất giảm từ 90 m xuống ~50 m).
2. Generate lúc build (authored, lưu vào scene) hay lúc runtime theo seed?
   Hợp đồng hiện tại **cấm** dựng scene lúc runtime, nên mặc định là editor tool.
3. Mỗi màn một biome riêng, hay chung một bộ prop khác mật độ?
4. Vật cản có chặn đạn không, hay chỉ chặn di chuyển?
