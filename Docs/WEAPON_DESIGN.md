# WEAPON DESIGN — ZombieWar

> Bản thiết kế vũ khí modular + shop. Mục tiêu: **chọn súng = chọn một lối build**, mỗi loại súng trả lời một câu hỏi tactical khác nhau, mở rộng vô hạn mà không phá cân bằng.
>
> Trạng thái: setup/prefab/pose/icon hoàn tất cho roster 25 khẩu. Các fire mode đặc biệt và balance
> economy vẫn là `[SPEC]` cho phase sau; xem `WeaponRosterMapping.json` và `HANDOFF.md` cho trạng thái thật.

---

## 0. Bối cảnh & insight cốt lõi

Game là **top-down/3rd-person wave survival shooter**. Người chơi **auto-aim density-seeking** (KDE cluster — hệ thống tự ngắm vào cụm zombie dày nhất). Zombie đến theo **bầy đàn dày đặc và xếp thành hàng** khi dồn về người chơi.

→ Ba insight định hình toàn bộ thiết kế vũ khí:

1. **Hình học của bầy đàn là tài nguyên.** Vì zombie xếp hàng, súng khai thác hình học (xuyên hàng, AoE, chain) có vai trò riêng biệt so với DPS đơn mục tiêu. Đây là lý do súng bắn tỉa xuyên (railgun) và laser cực mạnh về mặt *fantasy*.
2. **Auto-aim ngắm vào cụm dày nhất** → súng "diện rộng" tự động có giá trị cao vì đạn luôn rơi đúng chỗ đông. Thiết kế phải bù lại bằng *chi phí* (đạn, nhiệt, giá tiền, tốc độ bắn).
3. **Người chơi mobile, màn dọc** → HUD/shop phải đọc nhanh, mỗi súng cần **1 role tag + 1 build hint** rõ ràng để quyết định trong 2 giây.

**Nguyên tắc vàng:** không có súng "tốt hơn tuyệt đối". Mỗi súng mạnh ở một *tình huống mật độ* + *cự ly* + *nhịp độ* khác nhau, và **kéo theo một lối build khác nhau** qua hệ upgrade.

---

## 1. Trục thiết kế (design axes)

Mỗi vũ khí được định vị trên 6 trục. Đây là "DNA" giúp mở rộng roster mà vẫn giữ mỗi khẩu một bản sắc.

| Trục | Ý nghĩa | Ví dụ 2 cực |
|------|---------|-------------|
| **Engagement range** | Cự ly hiệu quả | Shotgun (sát) ↔ Railgun (cực xa) |
| **Target profile** | Đơn mục tiêu ↔ diện rộng | Pistol (1 con) ↔ Rocket (cả cụm) |
| **Cadence** | Nhịp bắn | Sniper (1 phát nặng) ↔ SMG (mưa đạn) |
| **Geometry exploit** | Khai thác hình học bầy | None ↔ Pierce-line / Chain / Cone |
| **Resource model** | Kinh tế bắn | Ammo mag ↔ Heat ↔ Charge ↔ Free |
| **Skill expression** | Thưởng cho kỹ năng | Auto-aim spray ↔ Timing charge/lineup |

Một khẩu súng "hay" = có **ít nhất 1 trục cực đoan** (bản sắc) + **1 điểm yếu rõ ràng** (chi phí cơ hội).

---

## 2. Weapon Class (role / nhiệm vụ)

10 class. Mỗi class = 1 câu hỏi tactical + 1 build archetype mà nó "lái" người chơi vào.

| Class | Câu hỏi tactical | Fantasy | Resource | Build archetype | Tier khởi điểm |
|-------|------------------|---------|----------|-----------------|----------------|
| **Sidearm** | "Hết đạn thì sao?" | Đáng tin, vô hạn | Mag nhỏ, reload nhanh | **Precision/Crit** | Common |
| **SMG** | "Đám đông sát mặt?" | Máy xay gần | Mag lớn, ăn đạn | **Bloodletter** (lifesteal/firerate) | Common |
| **Assault Rifle** | "Không biết chọn gì?" | Đa dụng | Cân bằng | **Generalist** (flat dmg) | Uncommon |
| **Shotgun** | "Bị dồn góc?" | Nút hoảng loạn | Mỗi phát 1 cone | **Bruiser/Knockback** | Uncommon |
| **LMG** | "Giữ chốt lâu?" | Áp chế bền bỉ | Mag khổng lồ, recoil ramp | **Suppressor/Sustain** | Rare |
| **Marksman (Sniper)** | "Xếp hàng chưa?" | Xuyên 1 lằn | Mag nhỏ, charge | **Marksman/Pierce** | Rare |
| **Railgun** | "Cả hàng dọc chết cùng lúc?" | Xuyên VÔ HẠN, sạc điện | Charge + heat | **Pierce/Charge** | Epic |
| **Flamethrower** | "Chặn 1 hướng?" | Biển lửa, DoT | Fuel/heat | **Pyro/Burn stacks** | Epic |
| **Tesla / Chain** | "Bầy nhầy nhầy?" | Sét lan | Heat | **Storm/Chain** | Legendary |
| **Laser (Energy)** | "Elite/boss cần melt?" | Tia liên tục, dí là ramp | Heat, không đạn | **Overheat/Melt** | Legendary |
| **Rocket / GL** | "Xoá nguyên cụm?" | Bùm diện rộng | Đạn hiếm, đắt | **Demolition/AoE** | Legendary |

> **Mở rộng:** thêm class mới = trả lời một câu hỏi tactical *chưa được trả lời*. Nếu câu hỏi đã có súng lo → đó là *variant* (skin/stat khác) chứ không phải class mới.

---

## 3. Fire model (cách đạn tương tác thế giới)

Đây là phần code cốt lõi. 6 `FireMode`:

1. **`SingleHitscan`** — 1 raycast, dừng ở target đầu tiên. (Pistol, AR, LMG, Sniper thường)
2. **`MultiPelletHitscan`** — N raycast trong cone, mỗi cái dừng ở target đầu. (Shotgun)
3. **`PiercingLine`** — `RaycastAll` dọc 1 đường, **damage TẤT CẢ** target trên đường, có `pierceCount` giới hạn và `pierceDamageFalloff` (giảm dần mỗi lần xuyên). Đây là **súng bắn tỉa xuyên / railgun** người dùng mô tả: "bắn 1 đường là tụi nó ăn đạn hết". `pierceCount = -1` = xuyên vô hạn (railgun legendary).
4. **`ContinuousBeam`** — laser: tick damage mỗi frame vào target đầu tiên trong beam, **ramp damage** khi giữ tia trên cùng 1 con (heat tăng, dí lâu = melt). Vẽ `LineRenderer` liên tục thay vì tracer.
5. **`Projectile`** — spawn vật thể bay (rocket/grenade), nổ AoE khi chạm (`explosionRadius`, `explosionFalloff`).
6. **`ChainLightning`** — hit target đầu → nhảy sang N target gần nhất trong `chainRange`, mỗi nhảy giảm damage.

**Damage falloff:** mọi mode dùng chung `damageFalloffCurve` (theo cự ly) + mode-specific (pierce/chain/beam-ramp).

---

## 4. Resource model (kinh tế bắn)

| Model | Field | Súng dùng | Cảm giác |
|-------|-------|-----------|----------|
| **Magazine** | `magazineSize`, `reloadDuration` | súng đạn thường | reload = nhịp thở, rủi ro |
| **Heat** | `heatPerShot`, `heatCapacity`, `coolRate`, `overheatLockTime` | laser, tesla, flamethrower | dí quá = kẹt nòng, phải nhả |
| **Charge** | `chargeTime`, `chargeHold` | railgun, sniper | timing — sạc đầy mới xuyên hết |
| **Ammo pool** | `ammoType` (Light/Heavy/Shell/Energy/Rocket) | tất cả | shop bán đạn theo loại → kinh tế |
| **Free** | (mag = 0) | pistol khởi đầu | fallback an toàn |

`ammoType` cho phép **kinh tế đạn dùng chung**: mua 1 thùng Heavy → dùng cho cả LMG lẫn Sniper. Tạo quyết định "đầu tư vào hệ đạn nào".

---

## 5. Tier & Pricing

5 tier, màu HUD/shop + hệ số giá geometric.

| Tier | Màu (hex) | Nhân giá | Ví dụ |
|------|-----------|----------|-------|
| Common | `#B8C2CC` xám | ×1 | Pistol, SMG |
| Uncommon | `#3FB950` lục | ×2.5 | AR, Shotgun |
| Rare | `#3B82F6` lam | ×6 | LMG, Sniper |
| Epic | `#A855F7` tím | ×14 | Railgun, Flamethrower |
| Legendary | `#F5A623` cam | ×32 | Laser, Tesla, Rocket |

**Công thức giá gợi ý:** `price = round( basePrice × tierMult × (1 + dpsIndex) )` với `dpsIndex` chuẩn hoá từ `damage × fireRate × didiệnRộngFactor`. Súng diện rộng đắt hơn dù DPS đơn thấp — vì auto-aim khiến chúng "quá tiện".

**2 loại tiền:**
- **Cash (in-run)** — rơi từ zombie, tiêu trong ván (mua súng tạm, đạn, hồi máu).
- **Meta currency** — giữ giữa các ván, unlock vĩnh viễn súng vào pool + nâng cấp gốc.

---

## 6. Build archetypes — "chọn súng = chọn build"

Đây là phần **thông minh** người dùng yêu cầu. Mỗi súng có `buildTag` → hệ upgrade (in-run level-up, task #48) **lọc perk theo tag** để mỗi khẩu tự nhiên đẩy vào 1 lối chơi. Không khoá cứng, chỉ *nghiêng xác suất* perk xuất hiện.

| Súng | buildTag | Perk pool nghiêng về | Lối chơi thành hình |
|------|----------|----------------------|---------------------|
| Pistol | `crit` | crit chance, headshot mult, first-shot bonus | Bắn chính xác, 1 phát 1 mạng |
| SMG | `bloodletter` | lifesteal, firerate, movespeed-while-firing | Áp sát, hút máu, không đứng yên |
| AR | `generalist` | flat dmg %, ammo, reload | An toàn, mọi tình huống |
| Shotgun | `bruiser` | knockback, close-range dmg, armor/thorns | Cận chiến, đẩy lùi, chịu đòn |
| LMG | `sustain` | mag size, sustained-fire ramp, no-move-penalty | Đứng máy, càng bắn càng mạnh |
| Sniper | `marksman` | pierce+1, charge dmg, slow-but-crit | Kiên nhẫn, xếp hàng, headshot |
| Railgun | `pierce` | pierce count, charge speed, chain-on-kill | Farm hàng dọc, xoá lane |
| Flamethrower | `pyro` | burn stacks, burn spread, area size | Kiểm soát khu vực, DoT |
| Tesla | `storm` | chain count, chain range, stun | Bầy đàn, khống chế |
| Laser | `melt` | heat cap, beam ramp, elite dmg | Diệt elite/boss, dí liên tục |
| Rocket | `demolition` | radius, direct-hit dmg, cluster bonus | Xoá cụm, burst khổng lồ |

> **Ví dụ playstyle divergence:** cùng 1 upgrade "movespeed", người chơi SMG (bloodletter) sẽ thấy nó thường xuyên (kite + hút máu), người chơi LMG (sustain) hiếm khi thấy (họ muốn đứng máy). → cùng một game, 2 người chơi 2 build khác hẳn nhau chỉ vì chọn súng khác.

---

## 7. ROSTER hiện tại `[DONE SETUP]`

Nguồn định danh chính xác là `WeaponRosterMapping.json`. Hiện có 25 WeaponData + 25 prefab + 25 icon,
đều có stable ID, muzzle/grip marker và authored pose. Thứ tự catalog 0–24:

| Order | Nhóm | Asset |
|---:|---|---|
| 0–5 | Generic baseline | PistolA, SMG Generic, AssaultRifle Generic, Shotgun Generic, Sniper Generic, LMG Generic |
| 6–14 | Sidearm | Glock19, P226, M1911, BerettaM9, USP45, DesertEagle, FiveSeven, Makarov, Python357 |
| 15–19 | Shotgun | BenelliM4, Mossberg500, SPAS12, DoubleBarrel, AA12 |
| 20–24 | Assault rifle | M4A1, AK47, SCARL, FAMAS, G36C |

Các concept Railgun/Laser/Tesla/Flamethrower/Rocket bên dưới là định hướng content tương lai, không phải
asset/runtime đã hoàn thành. Không đưa chúng vào UI ownership/economy của phase hiện tại.

---

## 8. Mapping stat → cảm giác (tuning guide)

Để designer chỉnh nhanh mà không phá vai trò:

- **damage × fireRate** = DPS trần. Súng diện rộng phải có DPS đơn *thấp hơn* để bù auto-aim.
- **range + damageFalloff** = định vị cự ly. Shotgun falloff dốc, Railgun phẳng.
- **pierceCount** = "bao nhiêu con xếp hàng thì đáng bắn". 3 = tình huống thường, ∞ = fantasy lane-clear.
- **chargeTime** = skill gate. Cao = thưởng người xếp hàng giỏi.
- **heatCapacity / coolRate** = bao lâu được melt trước khi phải nhả.
- **spreadAngle / pelletCount** = độ rộng cone + độ "chắc tay".
- **knockback** = kiểm soát vs damage tradeoff.

---

## 9. SHOP — thiết kế màn dọc (portrait)

### Layout (mobile portrait)
```
┌─────────────────────────┐
│   [ANIMATED BG SHADER]  │ ← scrolling pattern + gradient dọc + vignette
│   ╔═══════════════════╗ │
│   ║  💰 Cash / Meta   ║ │ ← top bar tiền
│   ╚═══════════════════╝ │
│   ┌─────────────────┐   │
│   │ WEAPON CARD     │   │ ← 1 card / súng, vuốt dọc
│   │ [icon] [tier]   │   │
│   │ Role: Railgun   │   │ ← role tag (đọc 1 giây)
│   │ ▓▓▓▓░ DMG       │   │ ← stat bars
│   │ ▓▓░░░ RATE      │   │
│   │ 💡 Build: Pierce│   │ ← build hint (chọn súng = build gì)
│   │ [  BUY  1200 ]  │   │
│   └─────────────────┘   │
│   ┌─────────────────┐   │
│   │ ... card kế ... │   │
│   └─────────────────┘   │
└─────────────────────────┘
```

### Nguyên tắc UX
- **Mỗi card đọc trong 2 giây:** icon + tier màu + role tag + build hint + giá. Không bắt đọc số nhỏ.
- **Tier = màu** → quét mắt biết ngay đắt/hiếm.
- **Build hint** là điểm bán hàng: người chơi mua *một lối chơi*, không phải một dãy số.
- **Stat bars tương đối** (so với trần roster), không phải số thô.
- **Background động nhưng subtle** — không cướp attention khỏi card. Đây là lý do shader phải *chậm + tối + gradient dọc* để card nổi ở giữa.

### Background shader (task #56) — spec
Theo kỹ thuật chung (không copy asset gốc):
1. **Screen-independent UV** — chia X theo aspect để pattern không méo trên màn dọc.
2. **Layer 1 — scrolling pattern:** noise/tiled texture cuộn chậm theo `Time × speed`, xoay nhẹ.
3. **Layer 2 — vertical gradient:** gradient dọc (đỉnh tối → giữa sáng nhẹ → đáy tối) để card ở giữa nổi. 4-corner tint tuỳ chọn.
4. **Layer 3 — vignette:** tối 4 góc, focus vào cột card giữa.
5. **Motion:** cực chậm (breathing), tránh gây nhiễu. Có slider `_Speed`, `_PatternScale`, `_Vignette`, `_TopColor/_MidColor/_BotColor`.
6. **Portrait-first:** default tuned cho tỉ lệ 9:16/9:19.5.

---

## 10. Roadmap tích hợp

| Bước | Task | Phụ thuộc |
|------|------|-----------|
| WeaponData mở rộng (enum, pierce, tier, price, ammo, buildTag) | #53 | — |
| Firing: PiercingLine + ContinuousBeam | #54 | #53 |
| Tạo assets roster mới (Railgun, Lancer, Laser...) | #55 | #53, model có sẵn |
| Shop BG shader dọc | #56 | — |
| Shop UI (card, currency, buy) | (D4 #49) | #53, #55 |
| Build-tag → upgrade perk filter | (D3 #48) | #53 |
| Ammo economy trong shop | (D4 #49) | ammoType |

---

## 11. Mở rộng thông minh — checklist khi thêm súng mới

1. Nó trả lời **câu hỏi tactical nào chưa có?** (nếu đã có → làm variant)
2. Trục cực đoan là gì? Điểm yếu rõ ràng là gì?
3. FireMode nào? Có cần code mode mới không (thường KHÔNG)?
4. Resource model? (mag/heat/charge/ammo)
5. buildTag → nghiêng vào perk pool nào?
6. Tier & giá theo công thức.
7. Role tag + build hint 1 dòng cho shop card.
8. Model/prefab + grip pose (grip tuner).

> Nếu trả lời được cả 8 → súng có bản sắc, không phải "reskin số to hơn".

---

## ADDENDUM v2.0 — Rarity 5 màu + Sao + Nội tại (sync ECONOMY_DESIGN.md §4)

### Hệ thống
- Rarity = field `tier` (regrade lại theo 5 màu): ⬜Xám → 🟩Xanh lá → 🟦Xanh biển → 🟪Tím → 🟧Cam.
- Sao ★2/★3 = +15%/+15% damage (mọi súng). Nội tại = CHỈ Tím/Cam, theo họ, có từ lúc sở hữu (bảng ECONOMY §4.3).
- Skin dùng chung 5 màu → 1 ngôn ngữ rarity toàn game.

### Chia màu roster (đề xuất design — chưa phải balance cuối)

| Họ | ⬜ Xám | 🟩 Xanh lá | 🟦 Xanh biển | 🟪 Tím | 🟧 Cam |
|---|---|---|---|---|---|
| Pistol (9) | Makarov, Glock 19 | P226, Beretta M9 | M1911, USP-45 | Five-seveN, Desert Eagle | Python .357 |
| Rifle/AR (5) | M4A1 | AK-47 | SCAR-L, FAMAS | G36C | *(chờ model)* |
| Shotgun (5) | Remington 870 | Mossberg 500, Double Barrel | SPAS-12 | AA-12 | *(hoặc AA-12 lên Cam)* |

### Trạng thái data 2026-07-20

1. Stable identity, class, canonical prefab path, icon, muzzle/grip and authored pose are complete for 25/25.
2. SMG/Sniper/LMG currently have one Generic entry each; this is a content-depth gap, not a broken placeholder path.
3. Tier/price/combat stats still require a holistic balance pass before Shop wiring is considered final.
