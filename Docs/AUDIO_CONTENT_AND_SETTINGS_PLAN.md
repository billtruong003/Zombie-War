# Zombie War — Audio, music và volume plan

**Trạng thái:** PROPOSED — chờ duyệt  
**Ngày audit:** 2026-07-24  
**Phạm vi:** chỉ thiết kế nội dung và cách tích hợp. Chưa tạo file âm thanh, chưa sửa gameplay.

## 1. Kết luận audit hiện tại

- Project hiện có **0 file `.wav/.mp3/.ogg/.aiff`** trong `Assets`.
- 25 súng đều đang trỏ về hai key chung: `gun_fire` và `gun_reload`. Vì vậy các súng chưa có cá tính âm thanh riêng.
- Runtime mới gọi âm thanh ở ba chỗ chính: bắn, bắt đầu reload và bom nổ.
- UI Settings đã có slider Music/SFX và toggle Master trong `RunOverlays`, nhưng volume chỉ sống trong RAM; chưa thấy lưu/khôi phục setting.
- `AudioService` có các channel `Master`, `Music`, `SFX`, `UI`, `Voice`, nhưng mọi SFX hiện đều chạy qua channel `SFX`. Chưa có mixer group, limiter theo nhóm, loop ambience hay giới hạn voice chuyên biệt cho horde.
- Chưa có âm thanh cho bước chân, đổi súng, đạn chạm vật liệu, zombie, pickup, thùng đồ, thùng nổ, UI, atmosphere và music.

## 2. Hướng âm thanh

Game có hình ảnh low-poly/cute nhưng nhịp chiến đấu đông. Âm thanh nên theo hướng:

- **Punchy, rõ, arcade:** mỗi phát bắn có lực nhưng không quá realistic hoặc chói tai.
- **Đọc được tình huống:** nghe là biết súng nào, zombie nào đang áp sát, barrel nào sắp gây nguy hiểm.
- **Không biến thành một cục noise:** súng người chơi luôn đứng trước; boss và cảnh báo đứng kế; tiếng zombie xa và bước chân đám đông bị giới hạn.
- **Mỗi map có một không khí riêng**, nhưng ambience không che tiếng combat.

Không dùng tiếng súng thật có tail quá dài. Top-down camera và tốc độ bắn cao cần transient ngắn, tail gọn.

## 3. Settings người chơi

### Giao diện đề xuất

| Setting | Mặc định | Ý nghĩa |
|---|---:|---|
| Master volume | 100% | Âm lượng toàn game |
| Music volume | 70% | Nhạc menu và nhạc trong trận |
| SFX volume | 100% | Súng, impact, player, zombie, prop, pickup |
| UI volume | 90% | Nút bấm, popup, reward, gacha |
| Ambience volume | 75% | Gió, môi trường, atmosphere loop |
| Vibration | On | Rung khi bắn mạnh, trúng đòn, nổ, reward lớn |

Nếu muốn UI đơn giản cho bản đầu, chỉ hiện `Music`, `SFX`, `Vibration`; giữ `UI` và `Ambience` làm sub-bus nội bộ. Master mute ở Pause không được làm mất giá trị slider đã lưu.

### Quy tắc lưu

- Lưu ngay khi slider/toggle thay đổi.
- Khôi phục trước khi phát music đầu tiên.
- Mute chỉ là trạng thái riêng, không ghi đè volume cũ thành 0.
- Cùng một setting được dùng ở Hub và trong trận.
- Có nút `Reset audio defaults`.

## 4. Bus và độ ưu tiên

```text
Master
├── Music
├── UI
├── Ambience
└── SFX
    ├── Weapons
    ├── Impacts
    ├── Player
    ├── Zombies
    ├── Interactives
    └── Pickups
```

Thứ tự ưu tiên khi quá nhiều âm thanh cùng lúc:

1. Cảnh báo nguy hiểm, player hurt/death, boss telegraph.
2. Súng đang cầm và vụ nổ gần player.
3. Impact gần, zombie tấn công gần.
4. Pickup và interactive.
5. Zombie idle/footstep xa và ambience detail.

### Chống “vỡ chợ” khi zombie đông

- Súng player: tối đa 6 voice đồng thời; súng auto dùng fire-loop/burst logic hoặc voice stealing.
- Zombie vocal: tối đa 8 voice thường trong toàn map; chọn con gần player nhất.
- Cùng một loại zombie không phát idle/aggro quá 2 voice cùng lúc.
- Zombie footsteps: tối đa 6 voice; đám đông xa dùng một `horde_movement_bed` nhẹ thay vì mỗi con phát một tiếng.
- Impact bullet: tối đa 10 voice; impact cùng vật liệu trong 50 ms được gộp/steal.
- Boss telegraph không bị cắt bởi tiếng zombie thường.

## 5. Chuẩn file và đặt tên

- Source master: WAV, 44.1 kHz, 24-bit.
- Mono: súng, impact, bước chân, zombie và prop 3D.
- Stereo: UI, music, ambience và stinger không gian rộng.
- Music loop: 90–150 giây; ambience loop: 45–90 giây.
- SFX thường: 0.1–2.5 giây; telegraph/boss có thể dài hơn.
- Tên key: chữ thường, dấu chấm phân cấp, ví dụ `sfx.weapon.ak47.fire.01`.
- Không bake khoảng lặng thừa ở đầu clip.
- Loop phải qua kiểm tra seam bằng tai và waveform.

### Mức âm tham chiếu để bắt đầu mix

| Nhóm | Mục tiêu khởi điểm |
|---|---|
| Music | khoảng -16 LUFS integrated, true peak ≤ -1 dBTP |
| Ambience | khoảng -24 đến -20 LUFS |
| UI | peak khoảng -8 đến -4 dBFS |
| Weapon/player danger | peak khoảng -6 đến -2 dBFS trước Master limiter |
| Zombie/impact phụ | thấp hơn súng player 3–8 dB |

Đây là điểm bắt đầu, không phải luật cứng. Quyết định cuối phải dựa trên mix trong gameplay thật.

## 6. Quy tắc variation

- Fire của mỗi súng: tạo 8 candidate, chọn 4 take tốt để ship.
- Footstep và bullet impact: 8 candidate, chọn 6.
- Zombie idle/hurt/attack: 6 candidate, chọn 3–4.
- Zombie death và skill đặc biệt: 4 candidate, chọn 2–3.
- UI lặp nhiều: 4 candidate, chọn 2–3.
- Mỗi lần phát random clip, volume ±1 dB và pitch ±2–4%; không pitch-random music hoặc cảnh báo gameplay quan trọng.

## 7. Dùng AI-SFX-Studio sau khi duyệt

`D:\Project\AI-SFX-Studio` phù hợp với pipeline này:

- Stable Audio 3 Small SFX cho SFX 0.25–11 giây.
- Music engine cho track tối đa 240 giây.
- Output 44.1 kHz stereo; clip 3D sẽ được convert/author lại thành mono khi import.
- Mỗi prompt SFX tạo 4 take; recipe có thể resume và sinh nhiều variant.

Quy trình sau khi user duyệt:

1. Khoá danh sách cue và các mục bị cắt.
2. Viết recipe JSON theo từng batch nhỏ: UI → movement/impact → weapon → zombie → ambience → music.
3. Generate candidate, **không tự động import Unity**.
4. Nghe, chấm `approve/reject/retry`, cắt silence và làm sạch loop.
5. Import các take được chọn, tạo AudioLibrary key và mixer.
6. Wire event, test bằng loa điện thoại và tai nghe.

## 8. Definition of Done cho phase audio

- Mỗi cue `MUST` trong manifest có ít nhất số variant ship đã ghi.
- Không còn 25 súng dùng chung một fire/reload key.
- Settings tồn tại qua restart.
- Trong combat đông, súng và cảnh báo nguy hiểm vẫn nghe rõ.
- Không có click ở loop, clip bị cắt, peak đỏ hoặc tiếng zombie spam.
- Test ít nhất 15 phút liên tục trên map đông nhất.

## 9. Cổng duyệt

Hiện tại tất cả cue trong manifest là `PROPOSED`. Chỉ bắt đầu generate khi user duyệt một trong ba kiểu:

- `Duyệt toàn bộ`.
- `Duyệt theo batch`, ví dụ chỉ UI + 25 súng.
- `Duyệt có chỉnh sửa`, ghi cue cần thêm/bớt hoặc đổi style.

