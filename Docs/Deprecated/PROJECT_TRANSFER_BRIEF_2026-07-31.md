# Zombie War — Transfer Brief

**Chốt ngày:** 2026-07-31 · **Commit:** `68fbc090` · **Branch:** `main`
**Mục đích:** tài liệu tự chứa để bàn giao cho một AI/người quản lý dự án. Đọc file này là đủ
hiểu dự án đang ở đâu mà không cần mở repo.

**Cách lập:** đọc trực tiếp source C#, asset và ProjectSettings; scene gameplay đọc qua Unity MCP.
Không chép lại tài liệu cũ — nhiều doc cũ đã lạc hậu.

---

## 1. Game này là gì

Game mobile bắn zombie top-down, phong cách casual cartoon, màn dọc 9:16 (1080×1920), chơi 1 tay.

- Súng **tự ngắm, tự bắn**. Hết đạn **tự nạp**.
- Chỉ có **3 input**: joystick di chuyển · nút ném bom · nút đổi súng.
  Không có nút bắn, không có nút nạp đạn. Thêm 2 nút đó là sai thiết kế.
- Vòng chơi: vào màn → sống sót qua các wave → chết hoặc thắng → nhận tiền → về Hub mua sắm.
- Session ngắn 3–10 phút. Không PvP, không leaderboard online, không energy.

**Kinh tế 3 loại tiền:** Coin (vàng, nâng cấp lặt vặt) · Gold (mọi thứ về súng: mua/gacha/lên sao)
· Gem (skin xịn, gacha skin).

---

## 2. Tech stack

| Mục | Giá trị |
|---|---|
| Engine | Unity 6000.3.10f1, URP |
| Framework | **BillGameCore 3.0.0** — framework tự viết của chủ dự án, là tài sản quan trọng nhất |
| Tween | **BillTween** (trong BillGameCore). **CẤM DOTween tuyệt đối** |
| Inspector | BillInspector (attribute kiểu Odin) |
| Shader | Stylized Toon World Kit 0.6.0 (package embedded, chủ dự án sở hữu repo upstream) |
| Enemy anim | VAT (Vertex Animation Texture) — enemy là MeshRenderer + VAT_Animator, **cấm** Animator/SkinnedMeshRenderer |
| Scene flow | `Bootstrap.unity` (persistent) → additive `Menu.unity` / `Map_Level1..5.unity` |
| Quy mô code | 163 file C# runtime, ~13.200 dòng, 159 EditMode test |

### BillGameCore — điểm cốt lõi

Framework auto-boot bằng `[RuntimeInitializeOnLoadMethod]`, lộ mọi thứ qua một facade tĩnh `Bill`:

```
Bill.Pool  Bill.Events  Bill.Scene  Bill.Audio  Bill.Save
Bill.Tween Bill.Timer   Bill.State  Bill.UI     Bill.Config
```

Luật: mọi code gameplay phải đi qua `Bill.*`. Không raw `Instantiate`/`Destroy` trong hot path,
không `FindObjectOfType`, không `new AudioSource`, không tự viết singleton.

**Hệ quả vận hành:** nếu không Play từ `Bootstrap.unity` thì framework không boot → console spam
`SERVICE NOT FOUND` và không gì animate. Đó là hành vi đúng, không phải bug.

---

## 3. ĐÃ CÓ — phần này đừng cho làm lại

### 3.1 Combat core (chắc chắn, chất lượng tốt)

- **Auto-aim** đầy đủ chống rung: stickiness margin, min-dwell time, danger-radius override
  (quái sát mặt luôn thắng), rate-limit vector ngắm để đạn luôn bay đúng hướng nòng đang chỉ.
- **Auto-fire + auto-reload**, không nút bắn.
- **Recoil spring 3 trục** (giật lùi + hất nòng + lệch trái/phải) với phân bố blue-noise.
- **Hitscan** thường + **PiercingLine** (sniper/railgun xuyên hàng, có falloff theo số mục tiêu xuyên).
- **Hit feedback đủ bộ:** hit flash trên shader (per-instance), damage number bay lên, hit-react
  một lần (bắn liên thanh không khoá quái vào vòng lặp giật), knockback, dissolve khi chết,
  camera shake khi bắn và khi bom nổ.

### 3.2 Enemy

- **15 quái** đã bake VAT xong, chia **6 class hành vi**: Walker · Runner → Pouncer (nhảy vồ) ·
  Ranged · Burrower (chui đất, bất tử khi dưới đất, trồi lên có telegraph) · Boss (giậm đất
  có telegraph) → Charger (lao thẳng theo đường khoá).
- 1 quái (HUGO Gorilla) bị chặn vì mesh 16.567 đỉnh vượt giới hạn texture 16.384.

### 3.3 Map & spawn

- **5 map campaign** đã generate xong (môi trường sa mạc), occlusion culling đã bake,
  static batching, mỗi map một ToonLightRig.
- NavMesh chỉ bake từ layer `WalkableGround`.
- Spawn an toàn: kiểm capsule clearance theo đúng kích thước từng loại quái + bắt buộc
  `PathComplete` tới player. 12/12 spawn path hợp lệ trên cả 5 map.

### 3.4 Meta / kinh tế (backend hoàn chỉnh)

- `PlayerProfile` (1.614 dòng): ví tiền, sở hữu súng, 3 slot loadout, shard + sao súng,
  costume, gacha pity, mission progress. Có versioning + migration.
- `RunState`: sổ cái trong trận. **`Payout()` idempotent** (gọi 2 lần chỉ trả 1 lần),
  `Abandon()` không bao giờ bank tiền.
- `GachaService`: deterministic theo seed, trừ tiền atomic, pity, đền bù đồ trùng.
- Sao súng ảnh hưởng **damage/tốc bắn thật** trong combat, không phải số trang trí.
- Loadout chọn ở menu áp được vào trận.

### 3.5 Content đã sản xuất

| Loại | Số lượng |
|---|---|
| Súng (data + prefab + pose + icon) | 25 |
| Quái (data + VAT prefab) | 16 |
| Wave data | 5 màn |
| Campaign stage | 5 (có gate power, first-clear reward) |
| Item costume | 448 + 30 bộ outfit, icon đủ 448/448 |
| **File âm thanh** | **970 clip `.wav`, 330 cue key** |

### 3.6 UI

5 màn menu (Hub / Loadout / Shop 4 tab / Costume / Battle Pass) + HUD in-run + overlay
(Pause / Settings / Revive / Level-up / GameOver / Victory / FTUE) — **đã dựng xong hết trong
scene và prefab**, đã bind reference đầy đủ.

---

## 4. CHƯA CÓ — các lỗ hổng đã xác minh

> Điểm chung: phần lớn không phải "chưa làm", mà là **"làm xong rồi nhưng chưa cắm dây"**.

### G1 — Âm thanh: có 970 clip, game phát đúng 3 tiếng ⭐ nghiêm trọng nhất

Catalog có 330 cue key phủ hết: tiếng đạn trúng theo vật liệu, tiếng từng loài quái, pickup,
bước chân, player bị thương/chết, ambience 5 map, stinger wave/boss, 89 key UI.

Runtime chỉ gọi phát âm ở **3 chỗ**: bắn súng, nạp đạn, bom nổ.
→ Không nhạc, không ambience, không tiếng quái, không tiếng UI, không tiếng nhặt đồ.

### G2 — Không có màn chọn màn chơi → Level 2–5 vào không được

Hàm `GameFlow.SelectLevel()` tồn tại nhưng **không nơi nào gọi**. Cả nút PLAY ở Hub lẫn menu
chính đều nhảy thẳng vào game.

Chuỗi hậu quả dây chuyền:

```
Không chọn màn
 → luôn load Map_Level1        (Level 2–5 chết)
 → RunState.LevelId = rỗng
 → RunDirector thoát sớm
     ✗ không đánh dấu hoàn thành màn
     ✗ không trả first-clear reward
     ✗ không phát sự kiện kết thúc run
 → MissionTracker không nhận được gì
     ✗ mission Battle Pass loại "hoàn thành run/màn/gom coin" đứng yên vĩnh viễn
```

Thêm: khi **thua** cũng không phát sự kiện kết thúc run (chỉ phát khi thắng).

### G3 — Level-up perk là vỏ rỗng, và perk chọn xong cũng vô tác dụng

- Overlay level-up + 3 nút perk **đã dựng sẵn trong scene**, nhưng code bấm nút chỉ đóng overlay.
- Không ai đọc "vừa lên mấy level" nên overlay không bao giờ tự mở.
- Quan trọng hơn: hàm tính hệ số perk **không được gọi ở đâu trong gameplay**. Damage/tốc bắn/
  tốc chạy đều không nhân perk. Nghĩa là kể cả nối UI xong, perk vẫn không đổi gì.
- HUD không có thanh XP/level → tiến trình trong trận vô hình.

### G4 — Màn kết quả không hiển thị gì

Dữ liệu kết quả run (số kill, wave, level, coin/gold/gem, thời lượng) được chốt đầy đủ,
nhưng màn GameOver/Victory không có ô text nào để hiện. Thắng/thua xong người chơi không thấy
mình được gì. (Theo design brief, đây đáng lẽ là màn "đã" nhất game với hiệu ứng đếm dồn tiền.)

### G5 — Nút rung là nút giả

Toggle "Vibration" lưu vào PlayerPrefs nhưng toàn repo không có một lệnh rung nào.

### G6 — 3 loại mission không bao giờ tiến triển

Ba hook "đã chọn perk" / "đã đổi súng" / "đã hạ boss" được khai báo nhưng không ai gọi.

### G7 — Nhặt bom xong không được gì

Sự kiện nhặt bom được phát nhưng không có ai lắng nghe → số bom không tăng.

### G8 — 5 scene gameplay là file binary

`Map_Level1..5.unity` không phải text YAML dù project cấu hình ForceText. **Nội dung scene hoàn
toàn ổn** (đã kiểm bằng Unity MCP), nhưng git không diff/merge/review được 5 file này.
Đây là vấn đề quy trình, không phải lỗi scene.

> ⚠️ Bẫy: `grep` 5 file này không ra gì **không có nghĩa là scene trống**. Phải đọc qua Unity.

### G9 — Rủi ro phát hành

- **Cheat panel đang được bật cho build Android/iOS/Standalone.** Build release hiện tại
  sẽ kèm bảng cheat.
- Bundle id vẫn là mặc định của template Unity.
- **Không có bất kỳ số đo profiler nào.** Mọi tuyên bố "chạy mượt trên mobile" hiện vô căn cứ.

### G10 — 1 lỗi nhỏ

Một chỗ trong code súng deref biến không kiểm null → có thể văng exception mỗi frame trong
trường hợp biên.

---

## 5. Thứ tự ưu tiên đề xuất

Nguyên tắc: **ưu tiên thứ đã trả tiền sản xuất nhưng chưa cắm dây.**

| Ưu tiên | Việc | Vì sao trước |
|---|---|---|
| **P0** | Nối âm thanh (~330 cue đã có sẵn) | Rẻ nhất, đổi cảm giác "game thật" nhiều nhất, không đụng scene |
| **P0b** | Haptics (toggle đã có, chỉ thiếu API rung) | Dùng chung seam với P0 |
| **P1** | Đóng vòng lặp run, đúng thứ tự: ①chọn màn → ②sửa sự kiện kết thúc run → ③màn kết quả → ④perk hiện ra → ⑤**perk có tác dụng thật** → ⑥nối 3 mission hook → ⑦bom pickup → ⑧thanh XP trên HUD | Mỗi bước mở khoá bước sau. Bước ⑤ là bước hay bị quên nhất |
| **P2** | Khoảnh khắc boss (nhạc hiệu + thanh máu + hit-stop), banner wave | Tăng cao trào |
| **P3** | Horde pressure (thiết kế đã chốt, chưa code) | Màn hình hiện hay bị thưa quái |
| **P4** | Hệ skill trong trận (mỗi súng 3 skill, 1 run mở tối đa 3) | Phụ thuộc P1 |
| **P5** | Food buff (shield / ammo vô hạn / x2 coin — spec đã duyệt) | Phụ thuộc P1 |
| **P6** | Hardening: tắt cheat, đổi bundle id, chuyển scene về text, đo profiler | Bắt buộc trước mọi bản phát hành |

---

## 6. LUẬT CỨNG — vi phạm là hỏng việc

1. **Quyền sở hữu UI.** `Menu.unity` và mọi `UI_*.prefab` trong `Assets/_Project/UI/Prefabs/Screens/`
   là do **chủ dự án tự vẽ tay**. Agent **không được sửa** layout/prefab/scene UI, không được mở
   hay save Menu scene kể cả như tác dụng phụ. Đã có một lần vi phạm làm hỏng layout và phải khôi
   phục từ snapshot. Việc UI duy nhất được phép: **code tween/animation (.cs)** khi được yêu cầu rõ.
   Sau mọi thao tác editor phải check `git status` và hoàn tác file UI lạ.
2. **Cấm DOTween.** Chỉ BillTween.
3. **Không rebuild** Player skeleton / Animator / WeaponRig / GunMount / RecoilPivot.
   Vùng này từng có sự cố nghiêm trọng, có tài liệu điều tra riêng.
4. **Không rename symbol bằng find-replace.** Chạy impact analysis trước khi sửa symbol.
5. **Không stage/commit/push** trừ khi task yêu cầu rõ ràng.
6. **Play test phải từ `Bootstrap.unity`.**
7. Không thêm Addressables/Resources migration mới ở phase này (audio đã dùng Addressables từ trước).
8. Không gỡ asmdef nào để "sửa lỗi thiếu type" — cách đúng là cấp asmdef cho thư mục bị mồ côi.

---

## 7. Tài liệu trong repo

Thư mục `Docs/` — **29 file live**, **15 file trong `Docs/Deprecated/`** (lịch sử, không thực thi).

**Đọc theo thứ tự:**

1. `Docs/CURRENT_STATE.md` — trạng thái thật, mọi khẳng định có `file:line`
2. `Docs/FRAMEWORK.md` — BillGameCore/BillTween/BillInspector, chữ ký API thật, luật asmdef
3. `Docs/CORE_WIRING_DIRECTIVE.md` — ràng buộc kiến trúc gốc (vì sao thiết kế như vậy)
4. `Docs/REMAINING_FEATURES.md` — backlog P0→P6, có `file:line` cho từng việc
5. `Docs/PROJECT_PHASE_ROADMAP_2026-07.md` — roadmap 7 phase, có trạng thái thật từng phase
6. `Docs/README.md` — mục lục toàn bộ doc

**Doc thiết kế đã chốt nhưng chưa implement:** `IN_RUN_SKILL_STAT_AND_INTERACTIVE_DESIGN.md`
(hệ skill), `FOOD_BUFF_SPEC.md`, `HORDE_DIFFICULTY_AND_SPAWN_SAFETY.md`,
`AUDIO_CONTENT_AND_SETTINGS_PLAN.md` (bus + voice limiter).

---

## 8. Cách giao việc cho agent code (dành cho người quản lý dự án)

Chủ dự án dùng AI vừa làm reviewer kỹ thuật vừa làm người soạn prompt. Một prompt giao việc tốt
cho dự án này cần có:

- **Model đề xuất:** Opus cho kiến trúc/migration/prefab-scene/debug khó; model nhỏ hơn chỉ cho
  việc cơ học có test mạnh bao quanh.
- **Nhắc lại luật cứng** ở mục 6 — đặc biệt luật UI ownership.
- **Tiêu chí chấp nhận cụ thể** + điều kiện dừng.
- **Yêu cầu bằng chứng:** `file:line`, screenshot khi liên quan hình ảnh, số test pass.
- **Cấm dọn dẹp ngoài phạm vi**, cấm commit khi chưa được yêu cầu.
- Một slice nhỏ đã verify kỹ **tốt hơn** một task lớn chỉ kiểm tra qua loa.

**Cách nhận biết báo cáo của agent có đáng tin không:** báo cáo từ model khác là bằng chứng cần
audit, không phải sự thật để chép lại. Kiểm 3 thứ: có mâu thuẫn nội tại không, có verify thiếu
không, có trượt phạm vi không.

**Bẫy đã dính thật trong dự án này:** một agent grep 5 file scene binary, không thấy gì, rồi kết
luận "không xác minh được scene có component nào". Kết luận đúng phải là "công cụ sai, phải đọc
qua Unity". Khi thấy agent kết luận từ việc *không tìm thấy*, hãy hỏi lại: công cụ có đọc được
định dạng đó không?
