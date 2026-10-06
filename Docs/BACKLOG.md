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

## Toàn bộ việc còn lại tới khi xong game (cập nhật 05/10 đêm)
Nhãn: **A** tự làm + tự đo · **B** tự làm + tự chụp · **C** cần owner. **HOLD** = owner bảo giữ lại, chỉ làm khi được gọi.

### Thứ tự phase lớn (owner chốt 05/10 đêm, đè mọi thứ tự cũ)
Tạm bỏ qua build. Chạy liền một mạch, chỉ dừng ở chỗ ghi **DỪNG**:
1. **1b Lồng tiếng** — xong 05/10 đêm. Gen xong → **DỪNG** chờ owner chọn bản → áp giọng vào game → chạy tiếp không dừng:
2. **Đợt 4** — xong 06/10 (aca35c4f2, báo cáo `Review/QA/botlab_1006.md`); còn #36 chờ mockup, #38 chờ owner (lab bot, cân bằng, kinh tế, cảm giác thưởng, plan chế độ chơi #28b)
3. **Súng và kinh tế** — phần logic xong 06/10 (3a đổi mảnh, Daily Ops từng khẩu, 5 thành tựu sưu tập, module hòm súng `GunCrates`, thông thạo → Arsenal); giao diện 3b/3c chờ mockup (`Docs/Plans/UI_MOCKUPS_NEEDED_1006.md`)
4. **Đợt 5 Boss** — lab xong 06/10 (e4409cdb0, `Review/QA/bosslab_1006.md`), chờ owner chọn vòng rào / tường / màu / đám đông rồi mới tích hợp #40–#44
5. **Đợt 7** — xong phần tự làm 06/10 (âm thanh 132/136 cue, kiểm VFX, `Review/QA/round7_1006.md`); chờ owner chấm 42 VFX, chọn màu viền, chọn fps VAT
6. **DỪNG** ← *đang ở đây (06/10)*: trao đổi kỹ thuật với owner. Owner có VPS, muốn một backend làm data server host trên VPS.
7. **Đợt 8 SDK/store + bản địa hoá**
8. **Build kiểm trên máy + Đợt 9 dọn dẹp, hiệu năng**
9. **Sau launch** (mục 10)
Đợt 6 (6 map, chọn map, MegaCity) chưa có trong thứ tự này: hỏi owner ở điểm DỪNG số 6.

### Quyết định owner 05/10 đêm
- **Q1 → lồng tiếng theo cách cũ**, mỗi câu gen **2 bản: tự nhiên và có cảm xúc**, owner chọn.
- **Q2 → súng full đổi phần dư ra coin**, tỉ lệ do Claude cân (xem mục 3a).
- **Q3 → mọi súng chỉ ra từ gacha, gacha ra mảnh**; hòm coin tối đa tím, hòm gem mới có Legendary; vẫn farm mảnh được; Shop GUNS bán gói tiền thật + VIP (xem 3b, 3c, chờ duyệt số).

### HOLD
0. **Build và kiểm trên máy** (Đợt 1 #1–#3, m8):
   - build Android dev có cheat, cài, ghi log
   - so `perf_baseline`, đo lại cú giật 300 ms và 13 s vào trận
   - cảm giác tay: rung, độ khó, Horde Call, HUD mới — C
1. **Đợt 4 · Cân bằng + lab bot**:
   - #28 lab bot tự chơi: người mới sống 6–8 phút, ai cũng chết — A
   - #29 mạnh lên sớm · #30 elite thưa, không tụ cục · #31 thang thưởng 0:45 / 1:00 / 2:00 / 4:00 · #34 tỉ lệ rơi · #35 nhịp thẻ 30–45 s · #36 đổi/loại thẻ — A
   - #33 kinh tế: coin hiện cộng thẳng theo kill nên phút 20 đã vài chục nghìn → công thức coin mới chống lạm phát, chạy `econ_sim.py`, đặt giá Common–Epic theo thu nhập — A
   - #32 cảm giác thưởng (chuỗi giết, banner kỷ lục, khoảnh khắc tiến hoá, phản hồi trúng/hạ/lên cấp) · #37 rương kiểu máy đánh bạc — B
   - #38 quái mới tới phút 10–12 (chọn trong 17 Blob hoặc mua asset) — C
1b. **Lồng tiếng — XONG 05/10 đêm** (commit b20d56bfa): 12 câu mới theo bản owner chọn, đã chuẩn hoá + lọc bộ đàm, nối vào Studio, WEAR NOW, Horde Call (4 hướng), rương ngày, chuỗi 3/7, thông thạo, tiến hoá súng; chạm chân dung 5/10 lần. Câu đã thu còn chờ chỗ phát, chuyển sang phase tương ứng:
   - `vo_lukas_meta_gun_shards` (đủ mảnh mở súng) → nối ở 3b
   - `vo_kaito_sit_skip_beacon` (đi ngang Boss Beacon 3 lần), `vo_mai_sit_skip_drop` (thùng tiếp tế hết hạn — hiện thùng chưa có hạn) → Đợt 4
   - `vo_riley_sit_bridge`, `vo_jiho_sit_basin`, `vo_riley_lore_s1..s5_brief`, `vo_kaito_meta_sector_new` → Đợt 6 (map, cầu, vùng lõm, chọn khu)
   - `vo_chen_egg_cat` → khi Home có con mèo (art)
   - giọng theo thị trường → Đợt 8 bản địa hoá

### Làm tiếp sau HOLD
2. **Plan nội dung chế độ chơi** (#28b, sau lab bot) — C duyệt:
   - #24 chế độ thử súng (riêng, không cho mượn súng trong trận thường)
   - #46 mode 2 (Thử thách ngày)
3. **Meta: súng và kinh tế**
   - **3a · #16 đổi phần dư khi súng full → coin** (A/B): mảnh của súng đã 3 sao tự đổi ra coin khi nhận (toast báo), kho mảnh cũ đổi ở Arsenal. Tỉ lệ đề xuất theo độ hiếm: Common 10 · Uncommon 15 · Rare 20 · Epic 25 · Legendary 40 coin/mảnh (~một nửa giá trị mảnh khi mua súng). Chỉnh lại sau #33.
   - **3b · Súng chỉ ra từ gacha, gacha ra MẢNH** (owner 05/10 đêm; bản số dưới đây chờ duyệt, mockup trước khi code):
     - Bỏ mua súng bằng coin ở Arsenal. Súng mở khi đủ mảnh: Common 20 · Uncommon 30 · Rare 40 · Epic 60 · Legendary 100.
     - Gacha chia 2 nhóm: **Skin** (Neon Nights, Street) và **Súng** (2 hòm). Mảnh thấp ra dễ, mảnh cao hiếm nên quý.
     - **Gun Crate (coin, tối đa tím):** mảnh Common ×5 50% · Uncommon ×5 30% · Rare ×4 15% · Epic ×3 5%; chắc chắn 10 mảnh Epic trong 40 lượt; ~1.500 coin/lượt (chốt ở #33, đây là chỗ tiêu coin chính).
     - **Elite Crate (gem):** mảnh Rare ×6 55% · Epic ×4 38% · Legendary ×5 6,4% · **trúng nguyên khẩu Legendary 0,6%**; chắc chắn nguyên khẩu Legendary trong 90 lượt; 30 gem hoặc 1 vé/lượt. Một pool chung, không featured.
     - Mảnh rơi ưu tiên khẩu người chơi đang gom dở (60%), còn lại ngẫu nhiên trong độ hiếm đó → mảnh dồn về một khẩu, không rải đều.
     - Mảnh dư của khẩu đã có dùng lên sao; khẩu full đổi ra coin (3a).
     - Đường mảnh không cần quay: Daily Ops gọi tên từng khẩu, chế độ thử súng (#24), thành tựu sưu tập (5/10/25/tất cả súng, đủ nhóm, Legendary đầu tiên), Pass, rương ngày 7.
     - FTUE: bỏ bù 400 coin để mua súng; lượt Gun Crate đầu miễn phí, chắc chắn đủ mảnh mở 1 khẩu Rare.
     - Home "NEXT BUY" → "khẩu sắp mở" (thanh mảnh gần đủ nhất).
   - **3c · Shop GUNS → gacha hoặc tiền thật, gói nạp và VIP** (chờ duyệt giá):
     - Tab GUNS: lối tắt tới 2 hòm + các gói tiền thật.
     - **Starter Pack $4,99** (1 lần): nhận ngay **Vector** (Legendary yếu nhất) + 300 gem + 5 vé.
     - **VIP Pack $19,99** (1 lần mỗi mùa): chọn 1 Legendary mạnh (AWM / HK416 / Thumper GL) + bộ skin Neon Circuit + 1.000 gem.
     - **Hạng VIP theo tổng nạp:** VIP1 $5 · VIP2 $20 · VIP3 $50 · VIP4 $100 · VIP5 $250 · VIP6 $500. Quyền lợi tăng dần: gem mỗi ngày (10→60), thêm 1 lượt rương ngày, giảm 5–15% gói ×10, khung avatar + huy hiệu VIP. Không bán sức mạnh vượt Legendary.
     - Danh sách IAP #57 làm lại theo các gói này; giá theo vùng PH/ID/VN.
   - #26b món featured 50/50 cho banner Outfits (hòm súng không có featured) — C
   - #27 skin súng mùa 1 (chọn style shader) — C rồi B
   - Q6 súng Pistol (model Tec-9) — C
   - tiến hoá cần 3 sao: dẫn từ màn thông thạo sang nâng sao ở Arsenal — B
   - quà trang phục từ Pass/Daily/thành tựu có WEAR NOW (`StudioScreen.OpenFor`) — B
4. **Đợt 7 · Art, VFX, âm thanh** (luôn đưa option) — B/C:
   - #50 VFX mọi skill đã mắt, đồng bộ một style
   - #51 trúng đạn theo vật liệu (chỉ Epic Toon, máu được phép)
   - #52 bóng/AO rẻ, shader đất và chất lỏng theo map, lava, vệt chân trên tuyết, màu viền
   - #53 phủ âm thanh (mới 105/397 key)
   - #54 tối ưu quái và súng: nén VAT, nạp quái qua Addressables, giảm poly súng nặng
5. **Đợt 5 · Boss** (lab trong Unity cho owner duyệt trước) — B/C:
   - #39 lab: vòng đấu rộng bao nhiêu, va tường rào thì sao, đòn boss, quái con
   - #40 Kaiju_04 (đòn FreeFighter, di chuyển/chết/trúng đòn MalbersHumanAnims)
   - #41 AI Titan: đập đất + vòng đỏ, lao tới, gọi quái con, nổi điên sau 90 s
   - #42 tường vòng đấu, thanh máu boss, màn xuất hiện, thưởng khi hạ
   - #43 Titan phút 5/10/15/20 mạnh dần + thanh mốc trên HUD
   - #44 boss trạm Boss Beacon: vòng sáng đỏ + thanh máu
6. **Đợt 6 · Nội dung và map** — B/C:
   - đủ **6 map**: concept → khu trong sandbox → owner duyệt → ô map bake sẵn
   - #45 theme V1 + màn chọn map
   - #47 MegaCity · #48 nhạc riêng map Sa mạc
   - #49 campaign: sau V1
7. **Đợt 8 · SDK và store**:
   - #55 AdMob hay ironSource — C
   - #56 quảng cáo có thưởng (hồi sinh, x2 coin, deal) + interstitial sau trận dài
   - #57 IAP theo 3c (gem, Starter Vector, VIP Pack, hạng VIP), xác thực hoá đơn
   - #58 Firebase Analytics, Crashlytics, Remote Config, funnel FTUE
   - #60 UMP consent, cấu hình 13+, gacha ở Bỉ
   - #61 các dòng Settings: Language, Help, Ad privacy, Privacy policy, Restore purchases
   - #63 mã hoá save, chống chỉnh giờ máy
   - #64 build release: keystore, tắt ZW_CHEATS, ReleaseGuard, icon app, dung lượng
   - #65 trang store, chính sách riêng tư + domain, ảnh bìa, tên súng thật hay tự đặt — C
   - #66 kiểm WebGL với Addressables
   - #67 soft launch PH/ID/VN
   - tài khoản, khoá, thanh toán — C
8. **Bản địa hoá** (#62): vi / en / ja / ko, mặc định theo quốc gia, trang store 4 thứ tiếng; owner duyệt bản ja/ko — A/C
9. **Đợt 9 · Dọn dẹp và hiệu năng** — A:
   - #68 dọn thư mục (Q4)
   - #69 bỏ comment cũ (AppLovin, Blueprint), widget FTUE v2 ẩn, đồ thừa trong UI_Hud
   - #70 cập nhật CURRENT_STATE, MVP_SHIP_PLAN
   - #71 HordeCall Board, dọn `.utmp`
   - #72 tick chung cho quái, giảm SetParent trong pool, giới hạn flow-field, ngân sách khung hình, bộ nhớ < 1,3 GB
10. **Sau launch**: #59 Firebase Auth + lưu đám mây, leaderboard, bạn bè (chỉ thiết kế), campaign.

**Còn chờ owner chốt:** Q4 dọn thư mục · Q5 build sớm · Q6 súng Pistol · duyệt 3b gacha mảnh súng + 3c gói nạp/VIP · #26b · #24 · #27 · #38 · #55 · #65.

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
| Q1 | #11 Lồng tiếng câu hướng dẫn Studio (Tiger) | **Chốt 05/10:** cách cũ, mỗi câu 2 bản tự nhiên / cảm xúc | — |
| Q2 | #16 Súng full đổi phần dư ra gì | **Chốt 05/10:** coin, Claude cân tỉ lệ | 10/15/20/25/40 coin mỗi mảnh theo độ hiếm |
| Q3 | #26 Giá súng Legendary | **Chốt 05/10:** không bán bằng coin; gacha hoặc farm mảnh | 100 mảnh để mở; plan 3b chờ duyệt |
| Q4 | #68 Dọn thư mục | xoá Screenshots 118 MB, VATEnemy 170 MB, DuNguyn 59 MB, Monsters, _Recovery? | xoá Screenshots, _Recovery; giữ cái còn dùng |
| Q5 | Build sớm | build 1 lần sau Đợt 2 (toàn UI) / giữ "không build tới lab bot" | build sau Đợt 2 |
| Q6 | Súng khởi đầu "Pistol" dùng model Pistol_A trong pack (dáng Tec-9, trông như SMG) | đổi model sang súng ngắn cổ điển trong pack (Pistol_B…J) / đổi tên thành "Auto Pistol" / giữ | đổi model, icon tự sinh lại |

## Rà soát tổng 05/10 tối
**Trạng thái (05/10 khuya):** B1–B4, m1–m3, m5–m7 đã sửa. m4 thành Q6 (là model, không phải icon). m8 đo trên máy (Đợt 1 mục 1).
Lỗi lớn:
- B1 Bộ Daily Ops đổi giữa ngày khi mua súng mới hoặc lên cấp tài khoản → tiến độ đang làm biến mất. Sửa: chốt bộ nhiệm vụ của ngày vào profile.
- B2 Rương ngày chưa có nút nhận → làm xong 4 nhiệm vụ cũng không mở được.
- B3 Thông thạo, tiến hoá, thành tựu chưa có màn hình → người chơi không thấy; tiến hoá chưa có nút bấm nên chưa ai tiến hoá được.
- B4 Profile: ảnh đại diện và khung xem trước là ô xanh cyan đặc (mất chân dung).
Lỗi nhỏ:
- m1 Mỗi kill tạo một danh sách súng mới để tra nhóm súng (rác bộ nhớ). Sửa: nhớ nhóm súng của trận.
- m2 Lời thoại và nhạc màn Result vẫn mừng kỷ lục thời gian, trong khi pill đã tính kỷ lục điểm.
- m3 Phụ đề chat bộ đàm che tiêu đề ở Arsenal, Gacha, Settings.
- m4 Icon "Pistol" là hình giống SMG.
- m5 Toast thành tựu không hiện trong trận (chỉ có câu thoại).
- m6 Quà điểm danh "mảnh súng" mất nếu chưa có súng đang trang bị (hồ sơ mới tinh).
- m7 Vé từ thông thạo cộng thẳng, không báo ví → số vé trên màn có thể chậm cập nhật.
- m8 Vào trận mất 13 giây trong Editor (đo lại trên máy).

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

**Đã xong 05/10 khuya (bước 1–4 sau rà soát):** sửa B1 m1 m2 m6 m7 · UI Daily Ops, rương ngày, thông thạo, tiến hoá, thành tựu, thẻ Home, 3 dòng Result, thẻ thành tựu trong trận · B4 avatar Profile (chờ stage thì hiện placeholder) · #6 tiêu đề Gacha 2 dòng, mép cuộn mờ cho mọi trang dài (thẻ Pass không còn bị cắt cứng), chip SUGGESTED co theo chữ · #17 render 13 màn × 4 tỉ lệ (`Review/V2/shots/resp_*`): không tràn ngang, nút chính ghim đáy · RadioVoice không chạy ngoài Play (test EditMode).

**Đã xong trong lượt chạy dài 05/10 (chiều):** #11 Studio lần đầu (Tiger, chữ) · #12 màn mở gacha bằng shader · #13 WEAR NOW · #14a điểm theo kill + kỷ lục · #18 thống kê theo súng · #19 Daily Ops + rương ngày + chuỗi · #20 điểm danh 28 ngày tăng dần · #21 thông thạo súng (logic + bonus toàn tài khoản) · #22 thành tựu (logic + câu thoại) · #23 tiến hoá súng (logic + thuộc tính) · #25 nối 13 câu thoại · #26a giá gói 2 vé hợp lý (25 gem/lượt < 27).
Còn lại của hàng A/B: phần UI cho #15/#14b/#19-#23 (thẻ Daily Ops, rương ngày, màn thông thạo U7, tab thành tựu U8, màn tiến hoá F3, toast thành tựu trong trận F4), #6, #17, Đợt 4, #28.

Cần owner (C), để riêng:
- #26b 50/50 cho banner thường: banner Outfits/Shards chưa có món "featured" — owner chọn món featured cho mỗi mùa.
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
Trạng thái 06/10: backend C# trên VPS (https://api.billthedevstudio.com), Firebase Analytics + Crashlytics, AdMob + UMP, lưu đám mây, sự kiện analytics đã xong và nối AdMob ↔ Firebase.
55. ~~Chọn AdMob hay ironSource~~ → AdMob (06/10).
56. ~~Quảng cáo có thưởng và interstitial~~ → AdService + Interstitials (06/10), cần test trên máy thật.
57. Unity IAP 8 sản phẩm, xác thực hóa đơn trên server (`/v1/iap/verify`, Google Play Developer API).
58. ~~Firebase Analytics, Crashlytics, funnel FTUE~~ (06/10). Remote Config: dùng `/v1/config` hoặc Firebase Remote Config.
59. ~~Lưu đám mây, xóa tài khoản phía server~~ (06/10). Còn: nút Xóa tài khoản trong Settings (mockup S1).
59b. ~~Đăng nhập chuẩn trên backend C#~~ (06/10): access token 1 giờ có `kid` + phiên bản token, refresh token dùng một lần xoay vòng (dùng lại = khoá mọi phiên), logout-all, liên kết nhà cung cấp (`/v1/auth/provider`, `/v1/auth/link`). Còn: bật Google Play Games khi có Play Console (HC_GOOGLE_CLIENT_ID/SECRET) + plugin GPGS trong game.
60. ~~UMP consent, quảng cáo 13+~~ (06/10). Bỉ: owner chặn Bỉ trong Play Console (06/10).
61. Các dòng Settings: Language, Help, Ad privacy, Privacy policy, Restore purchases, Player ID, Xóa tài khoản (mockup S1). Bảng tỉ lệ rơi (mockup S2).
62. Bản dịch vi, en, ja, ko, chọn ngôn ngữ mặc định theo quốc gia (làm sau SDK và store).
63. ~~Mã hóa file lưu, chống chỉnh giờ máy~~ (06/10): ProtectedSave (AES theo máy), GameClock theo giờ server khi có mạng.
64. Build release:
    - keystore
    - tắt ZW_CHEATS
    - ReleaseGuard
    - icon app
    - ~~kiểm dung lượng~~ (06/10): AAB + tách dữ liệu + Addressables for Android (install-time) + giới hạn texture Android → bản store 359 MB (base 89 MB; Play cho base 500 MB). Tuỳ chọn sau: map ngoài meadow sang fast-follow để lần tải đầu nhẹ hơn.
65. Trang store, link chính sách riêng tư (www.billthedev.com/hordecall/privacy/), ảnh bìa, quyết định tên súng thật hay tự đặt.
66. ~~Kiểm build WebGL với bundle Addressables~~ — bỏ (owner 06/10).
67. Soft launch PH/ID/VN.
68. ~~Vận hành~~ (06/10): công tắc từ xa (/etc/hordecall/config), backup mã hoá sang repo private hordecall-backups (khoá ở VPS + máy dev), watchdog tự khởi động lại API. Còn: báo Telegram (cần bot riêng), Internal testing + Pre-launch report (owner lo Play Console).
69. ~~Giữ chân~~ (06/10): nhắc ngày mới + nhắc sau 48 giờ, xin quyền thông báo sau trận 2, thẻ đánh giá của Play sau kỷ lục, cập nhật bắt buộc qua Play.
Icebox: Facebook Login, bạn bè (owner 06/10: để sau), đăng nhập email bằng link, mediation quảng cáo, server tự tính gem/gacha.

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
