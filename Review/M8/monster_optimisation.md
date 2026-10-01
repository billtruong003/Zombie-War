# Đề xuất tối ưu quái (phase C, 01/10)

Số đo lấy trong editor ở máy dev, chưa đo trên máy thật. Đọc theo xu hướng, không đọc như con số trên điện thoại.

## Số đo hiện tại

| Đo gì | Kết quả |
|---|---|
| Bộ nhớ VAT, roster gốc (14 loại trong 4 tier) | khoảng 52 MB (editor báo 105 MB vì đếm cả bản CPU) |
| VAT thêm theo map | meadow +11, volcano +11, forest +10, swamp +8, tundra +7 MB |
| 17 Blob: mesh | 580–2.670 đỉnh mỗi con |
| Trận thường, 10–14 quái (5 map) | 133–192 batch, 44–65 SetPass |
| Stress 150 quái, volcano | PlayerLoop 18,4 ms, tối đa 204 batch / **92 SetPass** |

**Kết luận:** chi phí render tăng theo **số loại quái đang sống**, không tăng theo số con.
Lý do: mỗi loại có material VAT riêng (texture vị trí/normal riêng), nên mỗi loại tốn thêm 1–2 SetPass. Còn các con cùng loại thì đã được gộp.
Map cũng có thêm quái riêng, nên một trận muộn có thể có khoảng 20 loại cùng lúc.

## Đề xuất (xếp theo hiệu quả trên công sức)

| # | Việc | Công | Lợi ích dự kiến | Rủi ro |
|---|---|---|---|---|
| 1 | **Giới hạn số loại cùng sống**, tối đa 8 loại. Loại mới vào thì loại cũ nhất trong tier thấp ngừng sinh. Chỉ chỉnh ThreatDirector, không đụng art. | S | SetPass của quái giảm khoảng ⅓ lúc trận muộn | Ít; trận đỡ "lộn xộn" hơn, cũng dễ đọc hơn |
| 2 | **Nén normal VAT** từ RGB24 xuống 2 kênh (RG16, dựng lại trục z trong shader) | M | Texture normal giảm còn ½ trên GPU mobile (RGB24 thường bị nới lên 4 byte/điểm). Tổng VAT giảm khoảng 25% | Ánh sáng toon cần soi lại; test EnemyRoster phải cập nhật |
| 3 | **Bake ít khung hơn** cho clip lặp (đi, đứng): 30 xuống 20 fps | S | VAT vị trí giảm khoảng 30% | Bước đi hơi giật ở tốc độ cao; xem trong Sandbox trước |
| 4 | **Gộp VAT nhiều loại vào một texture + một material** (atlas theo tier, mỗi con mang offset riêng) | L | SetPass của quái gần như còn 1–2 cho cả đàn | Viết lại baker và shader; chỉ làm khi số đo trên máy thật cho thấy SetPass là nút thắt |
| 5 | **Addressables cho quái** (nạp khi tới tier, nhả khi rời) | L | Bộ nhớ đầu trận giảm khoảng 20–30 MB | Lớn; theme đã nạp riêng qua Resources nên phần lợi còn lại nhỏ hơn dự tính trước đây |

Đã làm trong phase B:
- Nạp pool theo tier, mỗi frame một loại (`tk-lazy-pool`)
- Blob đám đông nhỏ và mỏng máu hơn
- Quái của map chỉ nạp khi chơi map đó

## Gợi ý chốt

- **Làm ngay:** 1 và 3, mỗi việc dưới nửa ngày, đo lại bằng stress 150.
- **Làm khi có số đo máy thật:** 2.
- **Chỉ làm nếu máy thật thiếu RAM hoặc nghẽn GPU:** 4 và 5.

Cách đo lại: lặp lại kịch bản stress (volcano, 150 con, god mode). Kết quả nằm trong `Temp/stress.txt` và ảnh `Review/M8/game_shots/stress_volcano.png`.
