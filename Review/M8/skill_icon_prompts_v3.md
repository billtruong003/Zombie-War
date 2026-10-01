# HordeCall: prompt vẽ icon skill, đợt 3 (4 icon thẻ BONUS)

Đợt 1 (`skill_icon_prompts.md`, 35 icon) và đợt 2 (`skill_icon_prompts_v2.md`, 43 icon) đã xong. Đã đối chiếu ngày 01/10:
68/68 thẻ trong `SkillCatalogDefs` đã có icon trong `Assets/_Project/UI/Icons/Skills/`.

Còn đúng **4 thẻ BONUS**. Các thẻ này hiện ra khi mọi thẻ đã max và đã đầy ô. Icon của chúng hiện là bản tạm (tách hoặc đổi màu từ icon có sẵn).
Vật phẩm cơ chế (nam châm, bom, đồng hồ băng) và trạm là model 3D trong trận, không cần icon 2D.

Kích thước, quy tắc đọc ở 48 px và STYLE BLOCK **giữ nguyên như đợt 1** (mục 1 và 3 của `skill_icon_prompts.md`).
Tên file đúng bằng id thẻ. Thả đè lên file tạm là game tự nhận.

| Nhóm | Số icon | Màu chủ đạo |
|---|---|---|
| Overflow (BONUS) | 4 | trắng kem #FFF4D6, viền thẻ BONUS; mỗi icon có một màu nhấn riêng |

## Cách làm

1. Mở **cuộc chat ChatGPT đã dùng cho đợt 2**. Nếu không còn, mở chat mới và đính kèm các ảnh sau:
   - 5 icon neo: `auto.drone.png`, `stat.maxhealth.png`, `evo.carpetbomb.png`, `uni.kinetic.png`, `auto.toxic.png`
   - `Review/M8/icon_style/levelup_context.png`
2. Dán STYLE BLOCK của đợt 1. Thêm câu: `The attached icons are the approved set: match their outline, colours and finish exactly.`
3. Dán lần lượt từng khối bên dưới.
4. Kiểm tra nền trong suốt. Nếu nền không trong suốt, dùng câu sửa ở mục 2 của đợt 1.

## Prompt

**`over.heal`**: Heal. Thẻ làm gì: hồi 25% máu tối đa ngay lập tức.
```
Same style as the approved icons. Icon "over.heal": a plump cartoon heart with a small white
plus sign on it and three tiny sparkles rising above, as if it is refilling. Heart colour warm
red #FF5A6A, sparkles cream #FFF4D6.
```

**`over.magnet`**: Magnet. Thẻ làm gì: hút mọi coin và ngọc XP trên bản đồ về phía người chơi.
```
Same style as the approved icons. Icon "over.magnet": a chunky horseshoe magnet seen at 3/4,
red body #FF5A6A with cream tips #FFF4D6, two small gold coins and one small cyan gem being
pulled toward its tips with short motion lines.
```

**`over.coin`**: Coin Bag. Thẻ làm gì: nhận ngay 60 coin.
```
Same style as the approved icons. Icon "over.coin": a round cloth money bag tied with a rope,
cream #FFF4D6 with a warm shade band, three gold coins #FFC83D spilling from the open top.
No symbols or letters on the bag.
```

**`over.might`**: Might. Thẻ làm gì: +3% sát thương, cộng dồn mỗi lần chọn.
```
Same style as the approved icons. Icon "over.might": a cartoon flexed arm (bicep) with a small
upward arrow beside it, skin-neutral cream #FFF4D6 with an orange accent #FF9E3D on the arrow.
Friendly, not muscular-realistic.
```

Sau khi có đủ 4 file, chép đè vào `Assets/_Project/UI/Icons/Skills/`. Giữ nguyên file `.meta` để không mất tham chiếu.
