# Toon depth: baked AO và runtime outline

**Cập nhật:** 2026-07-24  
**Quyết định hiện tại:** Dùng baked AO cho depth bề mặt. Chỉ thêm runtime outline sau khi benchmark. Outline không thay thế AO.

## AO và outline làm hai việc khác nhau

- AO tạo bóng nhẹ ở khe, chân vật thể và nơi hai bề mặt gần nhau. Nó giúp model có khối.
- Outline tách silhouette của vật thể khỏi nền. Nó giúp player đọc mục tiêu nhanh.

Nếu chỉ dùng outline, phần bên trong model vẫn dễ bị phẳng. Nếu chỉ dùng AO, zombie có thể chìm vào nền khi màu gần nhau. Hướng phù hợp với Zombie War là dùng cả hai nhưng giữ mỗi phần thật rẻ.

## Hướng shader nên dùng

### Môi trường và prop tĩnh

- Bake AO vào vertex color hoặc channel riêng của texture mask.
- Nhân AO vào phần ambient/indirect của toon shader.
- Không nhân AO quá mạnh vào vùng đã tối do main shadow. Mục tiêu là tạo contact depth, không tạo vết bẩn đen.
- Nếu asset dùng chung texture, ưu tiên vertex AO để không tăng thêm texture fetch.

### Zombie và object gameplay

- Giữ toon ramp 2 đến 3 band rõ.
- Dùng rim light nhẹ để tách nhân vật ở vùng tối.
- Outline chỉ dành cho player, zombie gần camera, elite, boss và object tương tác nếu full-screen outline quá đắt.
- Không bật SSAO full-screen mặc định trên mobile.

## Đánh giá Bill-SSOutline

Repo: [Bill-SSOutline](https://github.com/billtruong003/Bill-SSOutline)

Điểm tốt:

- Hỗ trợ Unity 6 RenderGraph.
- Có Roberts Cross nhanh hơn và Sobel đẹp hơn.
- Có selection mask, occlusion mask, distance fade và height fade.
- Dùng depth, normal và color edge nên điều chỉnh được nhiều kiểu hình ảnh.

Chi phí thật đọc từ source:

- Basic outline cần một render texture full-resolution.
- Mỗi frame chạy một pass composite full-screen và một pass copy-back full-screen.
- Selection thêm một R8 mask và một lượt render object được chọn.
- Occlusion thêm một R8 mask và một lượt render nữa.
- `OutlineFeature.Create()` hiện yêu cầu Color, Depth và Normal cùng lúc, kể cả khi Volume tắt color hoặc normal.
- Sobel lấy 8 sample cho mỗi nguồn edge đang bật. Roberts lấy 4.
- Normal input có thể khiến URP tạo thêm DepthNormals prepass. Color input có thể thêm copy hoặc intermediate texture tùy renderer.

Unity cũng ghi rõ rằng input Color, Depth và Normal của full-screen pass có thể tạo thêm pass; vì vậy chi phí không chỉ nằm ở shader outline. Xem [Full Screen Pass Renderer Feature](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/renderer-features/renderer-feature-full-screen-pass.html). Unity khuyên giảm intermediate texture, depth texture và opaque texture khi không cần trong [URP performance guide](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/optimize-for-better-performance.html).

## Cấu hình prototype đầu tiên

- Roberts Cross.
- Depth edge bật.
- Normal edge tắt ở lần đo đầu.
- Color edge tắt.
- Thickness 1 px.
- Không dùng occlusion mask.
- Selection chỉ dùng nếu cần outline riêng cho actor.
- Distance fade để bỏ outline của zombie quá xa.
- Dynamic resolution phải được test cùng outline.

## Các sửa đổi nên làm trước khi ship

1. Chỉ gọi `ConfigureInput` cho nguồn thực sự đang bật.
2. Không yêu cầu Opaque Texture nếu shader chỉ cần active color của RenderGraph.
3. Thêm tùy chọn half-resolution cho outline buffer. Half-resolution chỉ xử lý một phần tư số pixel. Unity dùng cùng hướng này cho SSAO trong [URP SSAO documentation](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal%4012.1/manual/post-processing-ssao.html).
4. Bỏ copy-back nếu có thể viết trực tiếp theo đúng RenderGraph contract của renderer hiện tại.
5. Giữ mask ở R8 và không cấp phát mask khi layer rỗng.
6. Profile trên thiết bị yếu nhất, không chốt bằng số trong Editor.

## Benchmark gate

Chưa import package vào renderer chính cho tới khi có baseline.

Đo ba cấu hình ở cùng camera và cùng số zombie:

| Cấu hình | Mục đích |
|---|---|
| Toon + baked AO | Baseline |
| Baseline + Roberts depth-only | Đo chi phí outline rẻ nhất |
| Baseline + Roberts depth + normal | Đo giá trị và giá của normal edge |

Pass khi:

- GPU frame tăng không quá 0,8 ms trên thiết bị mục tiêu thấp nhất.
- Không tạo GC mỗi frame.
- Không làm renderer sinh thêm texture/pass ngoài dự kiến.
- Outline không rung ở foliage, VAT zombie và mép camera.

Nếu fail, giữ baked AO + rim light và dùng outline chọn lọc bằng inverted hull cho actor quan trọng.

