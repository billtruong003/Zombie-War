# Quy tắc FX toon (phase A2)

Ngày 30/09. Không phải style mới: đây là ghi chú rút từ FX đang có trong game (Epic Toon FX + các
shader SkillLine / SkillDisc / SkillShield), để 33 skill mới làm ra khớp với skill cũ. Bản gốc của
các luật hình ảnh là `skill_visual_review.md` (26–27/09); trang này gom lại và thêm giới hạn hiệu năng.

## 1. Nhìn

| Luật | Cụ thể |
|---|---|
| Phẳng, viền cứng | Màu đặc, cạnh sắc, ít gradient. Hiệu ứng tan bằng cách **bị ăn mòn** (viền răng cưa), không mờ dần mềm như khói thật. |
| Ba nhịp | Báo trước (chỉ khi rơi trễ: 1 vòng mảnh khép lại + bóng đổ) → đường bay mắt theo được → trúng ở **ngang ngực** + tia lửa + tiếng. |
| Ngân sách màn hình | 1 lần tung ≤ ~25% màn hình, không bao giờ che nhân vật, không nháy trắng toàn màn. Khoảnh khắc lớn = rung ngắn, không phải flash. |
| Hitbox = hình | Hiệu ứng scale theo bán kính gốc đo được (`nativeRadius`), vòng `Pulse` nở đúng bằng vùng gây sát thương. Nổ to cap 1.3–1.4×. |
| Mặt đất phẳng | Vòng, vết cháy, băng: quad phẳng (`SkillDisc`) + hạt bay lên. Không dùng nửa cầu bị đất cắt. |
| Kẻ địch cho thấy trạng thái | Đóng băng xanh băng, cháy cam nhấp nháy, bị đánh dấu có tâm ngắm đỏ, giật điện tia nhỏ (`TintEnemy`, `MarkEnemy`). |

### Màu theo nguyên tố (đang dùng)

| Nguyên tố | Màu | Ví dụ hiện có |
|---|---|---|
| Sét | lõi cyan, tâm trắng | Chain Lightning (0.3, 0.7, 1) |
| Lửa / nổ | cam → vàng | Airstrike, Ordnance, Fire Trail (1, 0.42, 0.08) |
| Băng | xanh băng | Frost Nova (0.55, 0.9, 1) |
| Linh hồn | xanh lá → tím | Soul Burst xanh lá, Reaper tím (0.62, 0.3, 1) |
| Động năng | tím | Kinetic Shield (0.7, 0.55, 1) |
| Lưỡi / kim loại | trắng thép, vệt xanh nhạt | Orbit Blades |
| Hồi máu | xanh lá tươi | thẻ BONUS hồi máu (0.35, 1, 0.5) |
| Độc (mới) | xanh vàng chanh + tím sẫm | Mây độc, Đạn axit |
| Thẻ súng | màu hổ phách của súng | chữ ký 12 súng |

**Tiến hoá** giữ màu nguyên tố, thêm **viền vàng** và to hơn (Buzzsaw vàng, Squadron hồng đậm, Thunderstorm tím).

## 2. Độ đậm theo sức nặng

| Loại | Rung (ngân sách chung 0.45) | Âm lượng | Ví dụ |
|---|---|---|---|
| Nhịp nhỏ (mỗi phát, liên tục) | 0 | 0.25–0.5, cách ≥ 0.05 s | lưỡi cưa chạm, drone bắn, lửa cháy |
| Kích hoạt thường | 0.08–0.18 | 0.7–0.85 | Chain, Frost, Ordnance, Airstrike |
| Khoảnh khắc lớn | 0.2–0.4 | 0.9–1 | Emergency, Absolute Zero, lấy tiến hoá |

Số sát thương: nhịp nhỏ không cần nổi bật; chí mạng vàng + to (A3).

## 3. Hiệu năng (mobile)

| Giới hạn | Giá trị |
|---|---|
| Texture mỗi hiệu ứng | 1 (tối đa 2), dùng lại atlas Epic Toon FX hoặc `tex_fx_toon_pack` |
| Shader | chỉ dùng: Epic Toon FX có sẵn, `SkillLine`, `SkillDisc`, `SkillShield`, `ToonErode` (mới). Không tạo material lúc chạy (dùng property block). |
| Hạt sống mỗi lần tung | ≤ 40 (vụ nổ lớn ≤ 60) |
| Nổ cùng khung | ≤ 2 (đã có ở Self Burst), blast chờ ≤ 16, disc mặt đất ≤ 48, wisp ≤ 12 |
| Pool | mọi thứ qua `FxPool` / pool của module; không `Instantiate` khi đang đánh |
| Cấm | soft particles / depth texture, GrabPass / méo hình, đèn realtime, bóng đổ của FX, full-screen post |
| Overdraw | hạt nhỏ + cắt cứng thay vì nhiều lớp alpha mềm chồng nhau; tránh quad to phủ màn |

## 4. Kỹ thuật MinionsArt, bản tối ưu

Shader mới `ZombieWar/FX/ToonErode` + texture `tex_fx_toon_pack` (R vệt tròn, G noise lặp, B vòng, A vệt dài):

| Kỹ thuật MinionsArt | Cách làm ở đây | Tối ưu |
|---|---|---|
| Ăn mòn / dissolve theo đời hạt | Custom1.x của particle (TEXCOORD0.z) cộng vào ngưỡng cắt | 1 material cho mọi đường cong đời sống; không script |
| Viền toon khi tan | 1 dải `_EdgeColor` ngay trong đường cắt | không pass thứ 2 |
| Méo UV bằng noise | noise kênh G làm lệch UV lần lấy mẫu thứ 2 | cùng 1 texture, 2 lần sample |
| Nhiều hình từ 1 texture | `_ShapeMask` chọn kênh R/B/A | gói 4 hình vào 1 file 256² |
| Cạnh sắc không răng cưa | step + `fwidth` | không cần MSAA riêng |
| Sóng xung kích | mesh vòng hoặc hạt 1 quad kênh B, nở ra + ăn mòn | 1 hạt thay cho cả trăm |
| Lửa / khói toon | kênh A (vệt) cuộn noise lên trên, ăn mòn phần đuôi | ít hạt to hơn là nhiều hạt nhỏ |

## 5. Checklist cho mỗi skill mới

- [ ] Có sound (throttle), có rung đúng hạng, có telegraph nếu rơi trễ.
- [ ] Hình = hitbox (scale theo `nativeRadius`), trúng ở ngang ngực.
- [ ] Kẻ địch bị ảnh hưởng có tint/push/mark.
- [ ] Màu đúng nguyên tố; tiến hoá thêm vàng.
- [ ] ≤ 1–2 texture, ≤ 40 hạt, pooled, không alloc mỗi khung.
- [ ] Chụp contact sheet trong Sandbox cạnh các skill cũ cùng nguyên tố, không lệch style.
- [ ] Đo DPS bằng Bench ở rank 1/3/5.

## 6. Rà 10 power cũ (30/09) — đã nâng

Chụp trước/sau trong Sandbox: `Review/M8/a2_before/`, `Review/M8/a2_after2/`, `Review/M8/sandbox/*a2_firetrail*`.

| Power | Trước | Sau |
|---|---|---|
| Frost Nova, Airstrike, Chain Lightning, Orbit, Drone, Boomerang | đạt chuẩn | giữ nguyên |
| Soul Burst | chấm xanh nhỏ + 1 vòng mảnh | nova xanh phẳng (họ Frost Nova) đúng bán kính + sóng toon xanh |
| Emergency | tia hồng (sai nguyên tố), vòng mảnh | nova lửa đỏ (tắt khói phủ màn) + cầu lửa đỏ + 2 sóng toon đỏ/cam + dải đỏ, rung 0.4 |
| Ordnance / Carpet Bomb | nổ ra toàn khói xám | + sóng toon cam + vết cháy như Airstrike |
| Fire Trail | chuỗi "nến" lõi trắng | lửa cartoon trải mặt đất (`FireFieldRed`): một dải cháy liền |

Tài sản mới: shader `ZombieWar/FX/ToonErode`, `tex_fx_toon_pack`, `M_FX_ToonShockwave`, `NovaSoul_M8`,
`NovaEmergency_M8` (menu HordeCall/Skills/Build A2 FX). Dịch vụ `Shockwave()` dùng chung cho mọi power.
