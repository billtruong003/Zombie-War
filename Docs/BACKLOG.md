# HordeCall · Backlog (05/10)

Một danh sách duy nhất, làm từ trên xuống. Gom từ: các yêu cầu của owner trong các phiên chơi thử 04–05/10, memory, `MVP_SHIP_PLAN.md`, `CURRENT_STATE.md`, `GAME_DESIGN.md` và HordeCall Board.

**Quy tắc của owner:**
- Việc **không liên quan boss** làm trước.
- Boss chỉ làm sau khi có **lab trong Unity** để owner duyệt.
- UI chỉ sửa sau khi **mockup được duyệt**: canvas *HordeCall UI Mockups*, trang *UI 05/10*.
- Mọi thay đổi độ khó, quái hay đồ rơi phải đối chiếu **10 luật thể loại** (`GAME_DESIGN.md` §4).
- Xong mỗi đợt thì build lại cho owner chơi thử.

**Thứ tự chạy dài (owner chốt 05/10, đè thứ tự số đợt bên dưới):**
1. Mọi việc **không cần build Android** của Đợt 1–4, chưa build cho tới khi được yêu cầu.
2. Tới mục 28 (lab bot): dựng bot giả lập, lên kế hoạch nội dung chế độ chơi. Game chỉ cần **6 map**.
3. Rà VFX, shader, AO (Đợt 7).
4. Lab boss, rồi Kaiju (Đợt 5).
5. SDK và store (Đợt 8).
6. Bản địa hóa **vi, en, ja, ko**: ngôn ngữ mặc định theo quốc gia của người chơi, trang store cũng đủ 4 thứ tiếng.

**Đã xong 05/10** (chưa build, chờ owner chơi thử):
- hồi sinh giết toàn bộ quái trên map (`MechanicItems.ReviveClear`); Concept Lock ghi vào `GAME_DESIGN.md`
- #2 báo cáo hiệu năng `Review/QA/perf_device_1005.md`
- #3 cú giật 300 ms: nguyên nhân là log có stack trace; đã bỏ trace cho Log/Warning (`LogCost`) — cần đo lại trên máy
- #4 lỗi quái cũ không còn trong log máy; trạm có tiếng tick khi nạp
- #5 ẩn nút Events
- #7 HUD: cần điều khiển giữa đáy, lưới skill 3×2 dưới thanh máu (thanh mốc rương/Horde/Titan còn chờ Titan)
- #8 Pause có thẻ build (6 skill, 4 chỉ số, tiến hóa), nút RESUME/SETTINGS/LEAVE RUN
- #9 viền đỏ nhịp tim khi máu dưới 25%
- #10 Horde Call: báo trước 10 s, 1 hướng, mũi tên, 3-2-1, HORDE CLEARED + rương

## Chờ owner quyết (ghi 05/10)
| # | Câu hỏi | Lựa chọn | Đề xuất |
| --- | --- | --- | --- |
| Q1 | #11 Lồng tiếng câu hướng dẫn Studio (Tiger) | làm giọng như các câu FTUE cũ / tạm chỉ chữ, giọng sau | tạm chữ, giọng gom 1 đợt sau |
| Q2 | #16 Súng full đổi phần dư ra gì | coin / gem / mảnh súng khác / vé gacha; tỉ lệ | mảnh dư → mảnh "đa năng" dùng cho súng khác |
| Q3 | #26 Giá súng Legendary | 18.000 (memory) / 12.000 (dữ liệu) | chạy `econ_sim.py` rồi chốt |
| Q4 | #68 Dọn thư mục | xoá Screenshots 118 MB, VATEnemy 170 MB, DuNguyn 59 MB, Monsters, _Recovery? | xoá Screenshots, _Recovery; giữ cái còn dùng |
| Q5 | Build sớm | build 1 lần sau Đợt 2 (toàn UI) / giữ "không build tới lab bot" | build sau Đợt 2 |

## Hàng chạy không cần owner (05/10)
Nhãn: **A** tự làm, tự kiểm bằng test/số đo · **B** tự làm, tự chụp so mockup; owner xem lại sau, không chặn · **C** cần owner (quyết định, duyệt, cảm giác trên máy).

Thứ tự chạy (A/B):
1. #11 Studio lần đầu (B; giọng Tiger tạm chữ đến khi có Q1)
2. #12 Màn mở gacha bằng shader (B)
3. #13 WEAR NOW (B)
4. #14a Điểm theo kill + kỷ lục (A)
5. #18 Ghi súng đã dùng, thống kê theo súng (A)
6. #19 Daily Ops (A logic, B UI)
7. #20 Rương ngày, chuỗi 3/7, điểm danh 28 ngày (A/B)
8. #21 Thông thạo súng (A/B)
9. #22 Thành tựu + báo bộ đàm (A/B)
10. #25 Nối 29 câu thoại chưa dùng (A)
11. #23 Tiến hoá súng (B)
12. #15 Thẻ Daily Ops ở Home (B) · #14b 3 dòng tiến độ ở Result (B)
13. #26a 50/50 banner thường, kiểm giá gói 2 vé (A)
14. #6 Lỗi bố cục 30/09 (B)
15. #17 Kiểm 16:9 / 19.5:9 / 20:9 / tablet (A)
16. #29 #30 #31 #34 #35 #36 cân bằng đầu trận, elite, thang thưởng, tỉ lệ rơi, nhịp thẻ, đổi thẻ (A)
17. #33 econ_sim, công thức coin (A; giá chốt = Q3)
18. #32 cảm giác thưởng (B) · #37 rương máy đánh bạc (B)
19. #28 Lab bot: bot tự chơi, số liệu sống 6–8 phút / ai cũng chết (A)

Cần owner (C), để riêng:
- Q1–Q5 ở trên; #16 Arsenal đổi phần dư (Q2); #26b giá Legendary (Q3); #68 dọn thư mục (Q4); build (Q5)
- #24 chế độ thử súng: duyệt plan trước khi code
- #27 skin súng mùa 1: chọn style shader
- #38 quái mới phút 10–12: chọn con hoặc mua asset
- #28b plan nội dung chế độ chơi (sau bot)
- cảm giác tay trên máy thật: rung/giật, độ khó, Horde Call, HUD mới
- Đợt 7 VFX/shader/AO (luôn đưa option), Đợt 5 lab boss, Đợt 8 SDK/store (tài khoản, khoá), duyệt bản dịch ja/ko

## Đợt 1 · Lên máy và sửa lỗi
1. *(Để sau, chưa build)* Build Android có mọi bản sửa 05/10, cài lên máy, ghi log. Kiểm tra:
   - màn hình đứng đúng chiều, thanh tab ở đáy
   - icon xem trước hiện ra
   - bloom và hiệu ứng ô vật phẩm có trên máy
   - viền món đồ trong Studio
   - log sạch
2. So log máy `device_1005*` với `perf_baseline.md`, ghi báo cáo vào `Review/QA/`.
3. Sửa cú giật 300 ms khi bấm nút (EventSystem). Đo chi phí bảng cheat IMGUI.
4. Kiểm lại các lỗi quái cũ (thiếu pool ZombieSpit, SkeletonGiant/Mage sinh lỗi) và âm thanh riêng của trạm.
5. Ẩn nút Events đến khi có màn Events.
6. Lỗi bố cục UI 30/09: súng đè tiêu đề Gacha, thẻ Pass bị cắt, nhãn Level-up.

## Đợt 2 · UI theo mockup đã duyệt
7. HUD: cần điều khiển giữa đáy, 6 ô skill dạng lưới 3×2 dưới thanh máu, thanh mốc (rương / Horde Call / Titan).
8. Pause hiện cả build: 6 skill, 4 chỉ số, dòng EVOLUTION READY.
9. Máu dưới 25%: viền đỏ đập theo nhịp tim.
10. Horde Call:
    - báo trước 10 giây, đếm ngược 3-2-1
    - quái đến theo hướng, có mũi tên chỉ
    - qua được thì hiện HORDE CLEARED và cho rương
11. Studio lần đầu: thẻ bộ đàm hướng dẫn, góc ngắm, **thêm câu lồng tiếng mới** cho đặc vụ Tiger.
12. Màn mở gacha làm lại bằng shader, đẹp nhất có thể:
    - **bỏ vòng sáng dưới chân**
    - tự chụp kiểm tra sau khi làm
13. Mọi lần tặng đồ (gacha, Daily, thành tựu, Pass) đều có nút **WEAR NOW** mở Studio đúng ô món đó.
14. Result:
    - 3 dòng tiến độ (Daily Ops, thông thạo súng, thành tựu) dẫn tới nơi nhận
    - điểm tính theo số quái giết, lưu làm kỷ lục
15. Home: thẻ Daily Ops thay cho thẻ Missions.
16. Arsenal: súng đã full thì **đổi được** phần dư (mảnh, bản trùng) ra thứ khác, kèm làm đẹp Arsenal.
17. Kiểm tra mọi màn mới ở 16:9, 19.5:9, 20:9 và tablet.

## Đợt 3 · Meta: quay lại mỗi ngày, tiến trình súng
18. Ghi súng đã dùng vào dữ liệu trận, lưu thống kê theo súng.
19. Daily Ops:
    - 4 nhiệm vụ mỗi ngày
    - nhiệm vụ súng chỉ chọn nhóm súng đã có
    - mỗi nhiệm vụ thưởng mảnh của súng vừa dùng
20. Rương ngày, chuỗi 3 và 7 ngày (ngày 7 tặng súng), làm lại quà điểm danh 28 ngày.
21. Thông thạo súng: 10 cấp, thưởng nhóm súng áp dụng cho mọi súng.
22. Thành tựu:
    - 10 câu thoại đã ghi sẵn
    - báo bằng bộ đàm ngay trong trận
    - tab trong Profile
23. Tiến hóa súng: cần 3 sao và thông thạo 10.
24. Chế độ riêng để thử súng chưa có.
25. Nối 29 câu thoại chưa dùng, thêm phản ứng bộ đàm theo sự kiện.
26. Gacha:
    - thêm 50/50 cho banner thường
    - kiểm lại giá gói 2 vé
    - thống nhất giá súng Legendary (memory ghi 18.000, dữ liệu ghi 12.000)
27. Skin súng theo bộ shader cho mùa 1.

## Đợt 4 · Gameplay và cân bằng
28. Chạy bot giả lập và chơi trên máy để xác nhận:
    - người mới sống 6–8 phút
    - ai cũng phải chết, dù build mạnh đến đâu
29. Mạnh lên sớm (luật 1). Trận đầu phải tới được trạm đầu tiên và rương đầu tiên.
30. Elite ít hơn, không tụ thành cục.
31. Thang thưởng đầu trận:
    - thùng tiếp tế ở 0:45
    - zombie vàng ở 1:00
    - rương ở 2:00 và 4:00
32. Cảm giác thưởng:
    - chữ chuỗi giết
    - banner kỷ lục mới
    - khoảnh khắc tiến hóa
    - phản hồi khi trúng, hạ quái, lên cấp
33. Kinh tế:
    - đo coin mỗi trận trên máy
    - cân giá súng với thu nhập (`econ_sim.py`)
    - công thức coin để không lạm phát
34. Xem lại tỉ lệ rơi (`drop_rates.md`) sau khi đổi sang đồ rơi theo nhu cầu.
35. Kiểm các giả định: thẻ đầu ở giây 30–45, có điểm đến mỗi 30–60 giây.
36. Đổi thẻ / loại thẻ khi lên cấp.
37. Mở rương kiểu máy đánh bạc.
38. Thêm quái mới xuất hiện tới phút 10–12 (15 con Blob chưa dùng, hoặc mua asset).

## Đợt 5 · Boss: lab trong Unity trước
39. **Lab boss trong Unity** để owner duyệt:
    - vòng đấu rộng bao nhiêu
    - người chơi va vào tường rào thì sao
    - đòn đánh của boss
    - quái con là những con nào
40. Kaiju_04:
    - đòn đánh từ gói FreeFighter
    - di chuyển, chết, trúng đòn từ MalbersHumanAnims
41. AI của Titan:
    - đập đất kèm vòng đỏ
    - lao tới
    - gọi quái con
    - nổi điên sau 90 giây
42. Tường vòng đấu, thanh máu boss, màn xuất hiện, phần thưởng khi hạ.
43. Titan xuất hiện ở phút 5/10/15/20 và mạnh dần theo thời gian, nối với thanh mốc.
44. Boss của trạm Boss Beacon: vòng sáng đỏ và thanh máu.

## Đợt 6 · Nội dung và map
45. Chốt theme cho V1 và màn chọn map cho người chơi (owner: để sau).
46. Mode thứ 2, ví dụ Thử thách ngày.
47. Map MegaCity.
48. Nhạc riêng cho map Sa mạc.
49. Campaign (để sau).

## Đợt 7 · Art, VFX, âm thanh
50. Rà VFX mọi skill: đã mắt và đồng bộ một style.
51. Phủ đủ hiệu ứng trúng đạn theo vật liệu.
52. Bóng và AO rẻ, shader mặt đất và chất lỏng theo map, lava, vệt chân trên tuyết, thử các màu viền.
53. Phủ âm thanh: mới dùng 105 trên 397 key.
54. Tối ưu quái và súng:
    - nén VAT
    - nạp quái qua Addressables
    - giảm poly súng nặng

## Đợt 8 · SDK và lên store
55. Chọn AdMob hay ironSource.
56. Quảng cáo có thưởng (hồi sinh, x2 coin, deal) và interstitial đúng luật đã chốt.
57. Unity IAP 8 sản phẩm, xác thực hóa đơn.
58. Firebase Analytics, Crashlytics, Remote Config, kèm funnel FTUE.
59. Firebase Auth, xóa tài khoản trong game, lưu đám mây (để sau).
60. UMP consent, cấu hình quảng cáo 13+, gacha ở Bỉ.
61. Các dòng Settings: Language, Help, Ad privacy, Privacy policy, Restore purchases.
62. Bản dịch vi, en, ja, ko, chọn ngôn ngữ mặc định theo quốc gia (làm sau SDK và store).
63. Mã hóa file lưu, chống chỉnh giờ máy.
64. Build release:
    - keystore
    - tắt ZW_CHEATS
    - ReleaseGuard
    - icon app
    - kiểm dung lượng
65. Trang store, link chính sách riêng tư và domain, ảnh bìa, quyết định tên súng thật hay tự đặt.
66. Kiểm build WebGL với bundle Addressables.
67. Soft launch PH/ID/VN.

## Đợt 9 · Dọn dẹp và hiệu năng
68. Dọn thư mục (chờ owner quyết):
    - Screenshots 118 MB
    - VATEnemy 170 MB
    - DuNguyn 59 MB
    - Monsters
    - _Recovery
69. Bỏ comment cũ (AppLovin, Blueprint), widget FTUE v2 bị ẩn, đồ thừa trong UI_Hud.
70. Cập nhật `CURRENT_STATE` và `MVP_SHIP_PLAN` theo 05/10, cho §15–16 về hưu.
71. Cập nhật HordeCall Board, commit tool `device_sampler`, dọn `.utmp`.
72. Hiệu năng nếu số đo yêu cầu:
    - gom Update của quái vào một tick chung
    - giảm SetParent trong pool
    - giới hạn thời gian giải flow-field
    - ngân sách khung hình và bộ nhớ trên một máy tầm trung tham chiếu
