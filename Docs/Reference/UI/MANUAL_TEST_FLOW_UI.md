# ZombieWar — Luồng test tay UI menu (sau đợt tween + wire backend 2026-07-23)

> Phạm vi: 5 màn menu (Hub/Loadout/Shop/Costume/Pass) + phần tween mới + wiring backend mới.
> Mỗi mục có bước làm, kết quả mong đợi, và edge case. Đánh dấu ✅/❌ từng dòng khi test.
> Baseline máy: 191/191 EditMode test xanh, Validate All UI References pass phần cấu trúc.

## 0. Chuẩn bị

1. Mở scene `Bootstrap.unity` rồi Play (đừng play thẳng Menu/Map — thiếu Bill services sẽ spam
   `SERVICE NOT FOUND`, không phải bug).
2. Cần một profile "sạch" cho một số case: menu `ZombieWar/Dev/Player & Economy Tools`
   → Reset Profile. Cheat wallet cũng nằm ở đây (seed Coin/Gold/Gem).
3. Một số case cần tiền đúng-đủ hoặc thiếu — dùng cheat wallet chỉnh trước từng case.
4. Test trên Game view 1080×1920 (9:16), sau đó lặp nhanh mục 8 ở 19.5:9 và 20:9.

## 1. Hub — hiển thị lần đầu

| # | Bước | Mong đợi |
|---|------|----------|
| 1.1 | Boot vào Hub | Màn fade-in + trượt nhẹ từ dưới lên (transition chuẩn UIScreen) |
| 1.2 | Nhìn cụm tiền góc phải | Coin/Gem đúng số dư profile, format gọn (12.3K, 1.23M) |
| 1.3 | Pill Coin và Gem | Mỗi pill có nút `+` bấm được (hit target không bị che) |
| 1.4 | Mission card | Hiện **mission Pass thật** (title tiếng Anh từ catalog 20 mission), reward `+<coin>` — KHÔNG còn text cứng "SURVIVE THE NEXT WAVE" trừ khi… xem edge 1.E3 |
| 1.5 | Record ở AvatarChip | Chưa chơi lần nào → `—`; đã có best_score → `WAVE <n>` |
| 1.6 | Nút PLAY | Thở phồng-xẹp chậm (breathe) liên tục |
| 1.7 | Dock 5 tab | HOME đang active (không bấm được), 4 tab kia bấm được |
| 1.8 | Notify dot trên tab | Profile sạch: **không** dot nào sáng. HOME/SHOP không bao giờ có dot |

**Edge case:**
- **1.E1** Bấm giữ bất kỳ nút nào (PLAY, tab, `+`, mission card) → nút thu nhỏ ~96% khi đè, nhả ra bung lại. Thả tay NGOÀI nút → vẫn về scale 1, không kẹt nhỏ.
- **1.E2** Spam bấm PLAY thật nhanh nhiều lần → chỉ vào gameplay một lần, không double-load scene.
- **1.E3** Claim hết toàn bộ mission active (mục 6) rồi quay lại Hub → mission card ghi `ALL MISSIONS CLAIMED`, reward `—`.
- **1.E4** Dùng cheat cộng Coin khi đang đứng ở Hub → số Coin **nhảy kèm punch scale** (phóng to nhẹ rồi về). Gem không đổi thì không punch.
- **1.E5** Bật Reduced Motion (`PlayerPrefs reduced_motion = 1`, có thể set qua console/execute) → mọi press/punch/pulse/breathe tắt, chỉ còn fade màn. Tắt lại để test tiếp.

## 2. Hub — điều hướng

| # | Bước | Mong đợi |
|---|------|----------|
| 2.1 | Bấm `+` cạnh Coin | Mở Shop, tab **Weapons** |
| 2.2 | Back về Hub, bấm `+` cạnh Gem | Mở Shop, tab **Gacha** |
| 2.3 | Bấm mission card | Mở màn **Pass** |
| 2.4 | Bấm EditChip "COSTUME" trên podium | Mở Costume |
| 2.5 | 4 tab dock | Mở đúng màn tương ứng |
| 2.6 | Từ màn con bấm Back / phím Escape | Về Hub, Hub focus lại (record + mission card + dot refresh) |
| 2.7 | Escape khi đang ở Hub | KHÔNG thoát/không pop gì (Hub là root) |

**Edge case:**
- **2.E1** Vào Shop bằng `+` Coin (tab Weapons), back, vào lại Shop bằng tab dock SHOP → vẫn mở tab Weapons (pending tab reset sau mỗi lần show, không dính tab cũ).
- **2.E2** Push liên tiếp: Hub → Pass → (nút Gacha trong Pass) → Shop → Back → về Pass → Back → về Hub. Đúng thứ tự stack, không màn nào trắng/đen.

## 3. Loadout

| # | Bước | Mong đợi |
|---|------|----------|
| 3.1 | Mở Loadout | 3 slot + kho 25 súng, súng sở hữu sáng, chưa sở hữu khoá |
| 3.2 | Đổi súng slot | Preview stat cập nhật, quay ra vào lại vẫn giữ (persist) |
| 3.3 | Link "No guns yet? Go to Shop →" | Mở Shop |
| 3.4 | Toàn bộ copy | 100% tiếng Anh |

**Edge case:**
- **3.E1** Sau khi gacha ra súng mới (mục 4) chưa mở Loadout → Hub tab LOADOUT có **dot đỏ pulse**. Mở Loadout rồi về Hub → dot tắt.
- **3.E2** Trang bị 1 súng rồi vào trận, chết, về Hub → loadout giữ nguyên.

## 4. Shop

### 4.1 Weapons
| # | Bước | Mong đợi |
|---|------|----------|
| 4.1.1 | Tap 1 lần vào card chưa sở hữu | Card được chọn (highlight), CHƯA mua |
| 4.1.2 | Tap lần 2 đúng card đó, đủ tiền | Mua thành công: trừ Coin (số dư punch), badge OWNED hiện |
| 4.1.3 | Thiếu tiền, tap 2 lần | Card **shake**, không trừ tiền |
| 4.1.4 | Giá trên card | Đủ tiền = vàng, thiếu tiền = đỏ |

**Edge case:**
- **4.1.E1** Chỉnh ví = ĐÚNG bằng giá súng → mua được, ví về 0.
- **4.1.E2** Card đã OWNED, tap 2 lần → không trừ tiền lần nữa (idempotent).
- **4.1.E3** Mua xong quay ra vào lại Shop → vẫn OWNED (persist qua push/pop và qua restart play).

### 4.2 Gacha
| # | Bước | Mong đợi |
|---|------|----------|
| 4.2.1 | Nhãn nút | `Pull 1 · <giá>` / `Pull 10 · <giá>` đọc từ EconomyConfig, khớp asset |
| 4.2.2 | Pull 1 đủ tiền | Trừ đúng giá, panel kết quả hiện item + rarity tiếng Anh (Common…Legendary), tag NEW/Dup |
| 4.2.3 | Pull 10 | 10 dòng kết quả; dup súng ghi `Dup +<n> shards` |
| 4.2.4 | "Tap to close" | Đóng panel kết quả |

**Edge case:**
- **4.2.E1** Ví không đủ → thông báo "Not enough funds or the pool is empty — no pull.", KHÔNG trừ tiền.
- **4.2.E2** Pull đến khi ra dup → mở tab Upgrades xem shards súng đó tăng đúng.
- **4.2.E3** Pull nhiều lần liên tục (spam nút khi panel đang mở) → không pull chồng, tiền trừ khớp số lần thật.

### 4.3 Costume (trong Shop) & 4.4 Upgrades
| # | Bước | Mong đợi |
|---|------|----------|
| 4.3.1 | ITEMS / SETS | Hai mode card thật, giá Coin/Gem, tên tiếng Anh (Santa Claus, Viking Warrior…) |
| 4.3.2 | Bấm card chưa sở hữu | Modal xác nhận `Buy <tên>?` + giá; CANCEL không trừ tiền; BUY trừ đúng |
| 4.3.3 | Modal khi thiếu tiền | BUY → modal shake, không trừ |
| 4.4.1 | Upgrades | Chỉ súng SỞ HỮU hiện; `LV x/3`, `<shards>/<cần> shards · <gold> Gold` |
| 4.4.2 | Đủ shards+Gold bấm nâng | Sao tăng, DMG/ROF preview tăng, trừ tài nguyên; LV 3 → `MAX`, nút disable |
| 4.4.3 | Thiếu → bấm | Card shake, không trừ |

**Edge case:**
- **4.3.E1** Mua set → mọi item trong set thành owned bên màn Costume; item lẻ đã owned trước đó không bị tính tiền lại.
- **4.4.E1** Paging Upgrades khi sở hữu >6 súng → prev/next đúng, không card ma trang cuối.

## 5. Costume

| # | Bước | Mong đợi |
|---|------|----------|
| 5.1 | Tab HEAD/BODY/LEGS/SETS + slot chip | Đổi đúng nhóm, label tiếng Anh (Hair, Hat, Glasses, Pants…) |
| 5.2 | Preview | Kéo ngang xoay nhân vật (chỉ xoay, không dời); nhân vật idle động |
| 5.3 | Equip item sở hữu | Preview đổi ngay, persist khi thoát vào lại |
| 5.4 | Slot optional | Có ô **None**; slot bắt buộc (Hair/Top/Pants…) KHÔNG có None |
| 5.5 | RESET | Về outfit mặc định nhưng GIỮ ownership |
| 5.6 | Random | Chỉ random trong đồ đã sở hữu |
| 5.7 | Item khoá | Bấm → modal CONFIRM PURCHASE (BUY/CANCEL) — không equip lậu |

**Edge case:**
- **5.E1** Mua item trong modal Costume xong → equip được ngay, dot COSTUME trên Hub xử lý như 3.E1.
- **5.E2** Equip đồ ở Costume → vào trận → nhân vật gameplay mặc đúng đồ đó.
- **5.E3** Trang SETS: card set hiện `x/y items` sở hữu; mua set thiếu tiền → shake.

## 6. Pass — phần wire mới, test kỹ nhất

| # | Bước | Mong đợi |
|---|------|----------|
| 6.1 | Mở Pass (profile sạch) | Season: `LEVEL 1 · 0/500 XP`, bar rỗng. 3 quest row hiện **3 mission thật** (title khớp catalog: "Kill 50 monsters", "Clear 5 waves"…), counter `0/<target>`, KHÔNG nút CLAIM nào hiện |
| 6.2 | Chơi 1 trận giết vài quái, quay lại Pass | Counter mission kill tăng đúng số quái giết; bar fill đúng tỉ lệ |
| 6.3 | Hoàn thành 1 mission (vd giết đủ 50) | Row đó nhảy **lên đầu danh sách**, counter ẩn, nút **CLAIM** hiện |
| 6.4 | Bấm CLAIM | Tên mission punch, row chuyển `CLAIMED` (xanh), bar full; Coin cộng đúng `coinReward` (cluster punch); season XP tăng `passXp`, bar season nhích |
| 6.5 | Dot PASS trên Hub | Có mission claim được → dot sáng; claim hết → dot tắt (không cần rời màn — Hub tự refresh qua event khi quay về) |
| 6.6 | Nút "Go to Gacha →" | Mở Shop ĐÚNG tab Gacha |
| 6.7 | Back | Về Hub |

**Edge case — claim đúng-một-lần là contract quan trọng nhất:**
- **6.E1** Spam nút CLAIM thật nhanh trước khi UI kịp refresh → tiền + XP chỉ cộng **một lần** (kiểm số dư trước/sau bằng mắt hoặc dev tools).
- **6.E2** Claim xong, thoát play, Play lại → mission vẫn `CLAIMED`, không claim lại được, XP giữ nguyên (persist).
- **6.E3** Mission chưa đủ target mà cố bấm vùng CLAIM (không hiện thì bỏ qua case này) → không có gì xảy ra.
- **6.E4** Đổi ngày UTC (chỉnh đồng hồ máy +1 ngày hoặc đợi qua 00:00 UTC) rồi mở Pass → daily reset về 0/<target> và claim lại được; weekly GIỮ tiến độ. Đổi +8 ngày → weekly cũng reset.
- **6.E5** Vượt level: claim đủ để XP vượt 500 → `LEVEL 2 · <dư>/500 XP`, bar tính phần dư (không tràn quá 100%).
- **6.E6** Mở Pass để yên, chạy 1 trận ở cửa sổ khác/quay lại (progress đổi trong lúc màn đang mở) → row tự cập nhật khi có event, không cần thoát vào lại.

## 7. Trong trận (HUD) — pass nhanh, nhiều phần còn placeholder có chủ đích

| # | Bước | Mong đợi |
|---|------|----------|
| 7.1 | Pause | Modal PAUSED: RESUME/QUIT, toggle Sound/Vibration; mọi nút có press FX |
| 7.2 | QUIT | Confirm "Quit run? Rewards lost" — QUIT về Hub, STAY ở lại |
| 7.3 | Chết | REVIVE? đếm ngược; "Watch ad" chỉ log placeholder (chưa có ad SDK — ĐÚNG như thiết kế); "No thanks" → kết quả |
| 7.4 | FTUE lần đầu | "Drag to move!" — kéo joystick là tự đóng, Skip hoạt động, KHÔNG chặn input |
| 7.5 | Copy HUD | 100% tiếng Anh |

> Lưu ý: HUD coin/XP, overlay perk 1-of-3 và màn kết quả đọc `RunFinishedEvent` **chưa wire** — nằm ở
> hạng mục "Run-loop UI binding" kế tiếp, đừng log bug.

## 8. Cross-cutting

- **8.1 Aspect ratio:** lặp lại mục 1 + 5 + 6 ở 19.5:9 và 20:9 — bottom dock/panel dính safe area, không chồng đè, không nút nào ra ngoài màn.
- **8.2 Mixed language sweep:** đảo hết 5 màn + modal + gacha result + HUD — không còn bất kỳ chữ tiếng Việt nào.
- **8.3 Console:** cả phiên test không có error đỏ mới (warning `PanelSettings` theme của BillGameCore là noise cũ đã biết).
- **8.4 Stress điều hướng:** đảo màn ngẫu nhiên 20 lần thật nhanh, có xen kẽ Escape — không màn kẹt alpha 0/không bấm được, không double EventSystem.

## Gap đã biết (đừng log bug)

1. Nút `+` mở Shop chứ chưa có IAP/earn flow riêng (chưa tồn tại trong product).
2. Track thưởng theo level của Pass (6 tile ngang) + premium strip vẫn là trưng bày — backend reward-track chưa có.
3. DailyCard "Ready to claim" / EventCard "2 days left" trên Hub là placeholder tĩnh.
4. Settings screen chưa có (nút settings không gán màn).
5. XP/level Pass đang tạm 500 XP/level (`PassScreen.XpPerLevel`) — chưa có bảng level authored.
6. Nếu sau này chạy lại `PassScreenInstaller` rebuild destructive: installer chưa biết field `questRows` mới — cần cập nhật installer trước khi rebuild màn Pass.
