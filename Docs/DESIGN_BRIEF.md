# Zombie War — Game Design Vision (historical baseline)

> **SUPERSEDED — 2026-08-09.** The current product/game authority is
> [`GAME_DESIGN.md`](GAME_DESIGN.md), and the current world architecture authority is
> [`WORLD_STREAMING_TECHNICAL_DESIGN.md`](WORLD_STREAMING_TECHNICAL_DESIGN.md). This brief remains as
> evidence of the combat direction that preceded the procedural-world redesign.

**Authority:** Product fantasy, audience, design pillars and anti-pillars

**Status:** Source of truth

**Updated:** 2026-08-08

**Release scope:** [`MVP_SHIP_PLAN.md`](MVP_SHIP_PLAN.md)

**Combat rules:** [`Reference/Design/COMBAT_GRAMMAR.md`](Reference/Design/COMBAT_GRAMMAR.md)

> Tài liệu này nói game đang cố trở thành gì. Nó không chứng minh rằng gameplay hiện tại đã đạt
> được điều đó. Trạng thái implementation nằm trong `CURRENT_STATE.md` và source/asset hiện hành.

## 1. Ngôn ngữ trạng thái

- **LOCKED** — quyết định đủ chắc để các phase tiếp theo dựa vào.
- **PROVISIONAL** — hướng đang được ưu tiên nhưng phải được prototype/playtest xác nhận.
- **HOOK HYPOTHESIS** — giả thuyết về lý do người chơi muốn chơi tiếp; chưa được gọi là hook thật.
- **CANDIDATE** — ý tưởng đáng giữ nhưng chưa được đưa vào scope.
- **OPEN** — câu hỏi chưa có đủ evidence để quyết định.
- **DEFERRED / REJECTED** — không theo đuổi lúc này; không được tự đưa vào task.

## 2. Product fantasy

**LOCKED**

Zombie War là game mobile portrait, top-down, chơi một tay: người chơi là một survivor cơ động
đang điều phối cả kho súng giữa một màn hình đầy zombie hoạt hình. Súng tự ngắm và tự bắn; năng
lực của người chơi đến từ di chuyển, đọc tình huống, chọn đúng công cụ và đổi súng đúng nhịp.

Fantasy không phải “nhìn nhân vật tự thắng”. Fantasy là:

> Tôi ít nút bấm nhưng mỗi quyết định của tôi làm cả trận đánh đổi nhịp và bùng nổ rõ ràng.

Loop sản phẩm hiện tại vẫn là campaign stage 3–8 phút:

`chọn stage/loadout → sống sót và hình thành build → thắng/thua → nhận reward → củng cố arsenal → run tiếp`

## 3. Target audience

### 3.1 Primary — Active Survivor Player

Người thích spectacle và power growth của survivor-like nhưng muốn nhiều agency hơn việc chỉ chạy
trong lúc game tự giải quyết combat.

- **Why they download:** đàn quái đông, gun feel rõ, điều khiển một tay, session ngắn.
- **What they expect:** hiểu ngay cách chơi nhưng vẫn có quyết định chiến đấu để học.
- **Why they return:** muốn thử loadout khác, xử lý encounter tốt hơn và tạo một payoff đẹp hơn.
- **Why they quit:** một khẩu DPS giải mọi bài; đổi súng vô nghĩa; game chơi thay họ; build chỉ là số.

### 3.2 Secondary — Arsenal Collector

Người thích sở hữu, nâng và trưng bày nhiều khẩu súng.

- **Why they download:** roster 25 súng, visual weapon rõ, rarity và progression dễ đọc.
- **What they expect:** mỗi family hoặc model tạo một lý do sử dụng, không chỉ là số lớn hơn.
- **Why they return:** một unlock mở ra cách chơi hoặc tổ hợp mới.
- **Why they quit:** collection là thang DPS; súng cũ mất giá trị; shop quan trọng hơn combat.

### 3.3 Secondary — Short-session Mastery Player

Người muốn một run ngắn nhưng có thể tự nhận ra mình chơi tốt hơn.

- **Why they download:** 3–8 phút, portrait, vào trận nhanh.
- **What they expect:** telegraph rõ, sai đúng dễ hiểu, ít input nhưng không ngẫu nhiên.
- **Why they return:** “lần sau tôi có thể đọc và cash out tình huống đó tốt hơn”.
- **Why they quit:** thắng/thua chủ yếu do stat, auto-target hoặc RNG ngoài khả năng can thiệp.

## 4. Core experience theo thời gian

### 10 giây đầu

**LOCKED INTENT:** di chuyển lập tức dễ hiểu; súng tự tìm và bắn mục tiêu; hit/death feedback và mật
độ quái nhanh chóng bán được fantasy “một người chống cả đàn”. Không cần tutorial dài.

### Phút đầu

**PROVISIONAL:** người chơi phải gặp ít nhất hai tình huống khiến một lựa chọn di chuyển hoặc đổi
súng có kết quả dễ thấy. Nếu chỉ chạy vòng và chờ DPS, loop chưa đạt mục tiêu.

### Run đầu

**LOCKED INTENT:** người mới hiểu ba input, hoàn thành hoặc thất bại có nguyên nhân đọc được, thấy
reward và biết mục tiêu tiếp theo. **OPEN:** độ sâu build tối thiểu cần thiết cho run đầu.

### Các run sau

**PROVISIONAL:** loadout, enemy composition và mutation trong run phải tạo cách xử lý khác nhau.
Tiến bộ meta mở thêm lựa chọn; không được chỉ làm mọi con số lớn hơn.

## 5. Current hook hypothesis — UNPROVEN

### READ → SET UP → SWAP → CASH OUT → RESET

Đây là **HOOK HYPOTHESIS**, không phải fact và chưa phải identity đã lock.

- **READ:** enemy composition, telegraph hoặc combat state đặt ra một bài toán.
- **SET UP:** khẩu đang dùng kiểm soát, expose, stagger, dồn hàng hoặc tạo vị trí thuận lợi.
- **SWAP:** người chơi cố ý chuyển sang công cụ phù hợp hơn.
- **CASH OUT:** khẩu mới biến setup thành payoff mạnh, rõ và đáng nhớ.
- **RESET:** ammo/reload, recovery, vị trí hoặc composition tạo quyết định kế tiếp.

Câu hỏi rủi ro cao nhất:

> Khi aim và fire đều tự động, việc đọc enemy situation rồi phối hợp weapon role có đủ tạo mastery,
> agency và replayable fun không?

Phase 2–3 trong execution plan phải trả lời câu hỏi này. Compile xanh hoặc status effect chạy đúng
không được tính là chứng minh hook.

## 6. Design pillars

### P1 — Arsenal là động từ, không phải bảng stat

**LOCKED.** Súng là tác nhân chiến đấu chính. Mỗi family phải thay đổi hành vi và bài toán người
chơi; skill chỉ khuếch đại hoặc nối các hành vi đó.

### P2 — Low input, high consequence

**LOCKED.** Ba input hiện tại là joystick, bomb và weapon switch. Không thêm nút chỉ để tạo vẻ sâu.
Agency phải đến từ timing, positioning, preparation và tool choice. Control cuối có thể đổi cách chọn
súng nếu playtest chứng minh one-button cycle không biểu đạt được quyết định.

### P3 — Enemy tạo câu hỏi, weapon trả lời

**PROVISIONAL.** Composition và telegraph phải khiến người chơi ưu tiên, giữ khoảng cách, chịu áp
lực hoặc chờ punish window. HP tăng đơn thuần không tạo stage identity.

### P4 — Power phải gắn với hành động của người chơi

**LOCKED.** Payoff lớn phải cho người chơi hiểu “mình đã tạo ra nó”. Tránh ability tự xóa màn hình,
proc không đọc được hoặc spectacle không liên quan tới quyết định vừa thực hiện.

### P5 — Depth từ interaction, không từ status soup

**PROVISIONAL.** Một vocabulary trạng thái nhỏ dùng lại giữa weapon, enemy và skill tốt hơn hàng
chục exception. Content phải scale bằng tổ hợp có nghĩa, không bằng viết 25 × 25 cặp riêng.

### P6 — Ship evidence, không ship niềm tin

**LOCKED.** Mỗi phase có hypothesis, evidence và exit PASS/PARTIAL/FAIL. Prototype thất bại được
quyền giết hoặc sửa hướng; không mở content chỉ vì foundation đã tốn công.

## 7. Anti-pillars — game này không phải

- **LOCKED:** không phải idle game nơi combat phần lớn tự giải quyết.
- **LOCKED:** không phải ability-spam game khiến súng thành nguồn damage phụ.
- **LOCKED:** không phải 25 khẩu súng chỉ xếp theo DPS/tier.
- **LOCKED:** không phải hardcore manual-aim shooter hoặc game cần thêm fire/reload button.
- **LOCKED:** không phải RPG nặng lore, dialogue, cutscene hay faction trước gameplay.
- **LOCKED:** không phải survivor-like mà mọi level-up chỉ là `+X%`.
- **DEFERRED:** procedural/endless run kiểu MegaBonk; chỉ xét lại bằng evidence sau core validation.
- **DEFERRED:** PvP, clan, online leaderboard, energy/stamina và live-service nặng.

## 8. Major open questions

1. **OPEN:** Setup/payoff switching có vui tự thân hay chỉ là damage combo có thêm bước?
2. **OPEN:** One-button cycle có đủ cho lựa chọn chiến thuật hay làm người chơi bấm vòng ngẫu nhiên?
3. **OPEN:** Pistol là emergency layer luôn truy cập, slot ngang hàng, hay đơn giản là slot đầu trong cycle?
4. **OPEN:** Auto-aim nearest có cướp mất khả năng ưu tiên ranged/heavy hoặc chủ động bắn prop không?
5. **OPEN:** Một run cần bao nhiêu variance trước khi người chơi thật sự muốn thử run khác?
6. **OPEN:** Bao nhiêu shared state là đủ để đọc rõ mà vẫn tạo tổ hợp?
7. **OPEN:** Model khác nhau trong cùng family là sidegrade, technique carrier hay rarity/stat ladder?
8. **OPEN:** Stage 1 hiện có đủ enemy question để test grammar hay cần test arena/loadout riêng?

## 9. Decision boundaries

- Campaign selector và completion-based unlock là foundation; nó không chứng minh hook.
- Base weapon identity phải được chứng minh trước signature technique.
- Signature technique chỉ mở sau khi Phase 3 cho evidence tích cực.
- XP/build system chỉ mở sau core combat; không dùng perk breadth để che combat phẳng.
- Meta/economy/retention chỉ được tối ưu sau khi người chơi có lý do muốn thêm một run.
- Story chỉ giải thích gameplay đã có giá trị; không được dùng để hợp thức hóa mechanic yếu.

## 10. Câu hỏi sản phẩm phải luôn trả lời được

> Người chơi sẽ kể với bạn mình điều gì là “cool” ở Zombie War?

Hiện câu trả lời tốt nhất vẫn là giả thuyết: “Tôi dùng một khẩu để bẻ thế trận rồi đổi khẩu khác để
nổ payoff.” Cho tới khi playtest chứng minh người chơi tự làm, tự hiểu và muốn lặp lại hành vi đó,
Zombie War **vẫn chưa tìm thấy hook đã được xác nhận**.
