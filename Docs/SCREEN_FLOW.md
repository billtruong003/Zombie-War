# ZombieWar — Screen Flow (bản đồ màn hình)

> **Target flow.** Screen prefabs now exist; backend state completeness is tracked in `HANDOFF_UI_CODEX.md`.

> Chỉ được design các màn có tên ở đây. Thiếu thì hỏi, cấm bịa thêm màn.

## Sơ đồ tổng

```
Boot/Splash
   │
   ▼
 HUB (menu chính) ◄──────────────────────────────┐
   │  ├─► LOADOUT (chọn súng 3 slot)             │
   │  ├─► COSTUME (thay đồ + preview)            │
   │  ├─► SHOP ─ 4 tab: WEAPONS │ GACHA │ COSTUME │ UPGRADES
   │  ├─► BATTLE PASS (free track + quest ngày)  │
   │  └─► SETTINGS (modal)                       │
   ▼                                             │
 PLAY = HUD in-run                               │
   │  ├─ (đầy XP) ─► LEVEL-UP PERK (modal, pause game)
   │  ├─ (nút ⏸) ─► PAUSE (modal) ─ Tiếp tục / Âm thanh / Bỏ trận
   │  └─ (chết lần 1) ─► REVIVE (modal, xem ad, 1 lần/run)
   ▼                                             │
 GAME OVER (payout đếm dồn) ── [Về nhà] ─────────┘
                └─ [Chơi lại] ─► PLAY luôn
```

## FTUE (lần chơi đầu — overlay, không phải màn riêng)

1. Mở game lần đầu → vào THẲNG trận (bỏ qua Hub). 3 tooltip lần lượt: kéo joystick → auto bắn → nút bom.
2. Chết/hết wave → GameOver payout thưởng đậm → về Hub.
3. Hub: highlight duy nhất nút SHOP (tab GACHA) + tặng 1 vé quay → quay ra **SMG Xanh lá (rigged, 100%)** → celebration.
4. Highlight LOADOUT → lắp SMG vào Slot 2 → highlight PLAY. Hết FTUE.

## Luật back / thoát

- Mọi màn con trong Hub: nút **←** góc trái trên → về Hub. Android back = ←.
- Modal (Pause/Settings/Level-up/Revive/celebration gacha): nút ✕ hoặc chạm ngoài → đóng modal, KHÔNG đổi màn.
- Trong trận: KHÔNG có back. Chỉ thoát qua Pause → "Bỏ trận" (confirm 2 bước: "Mất hết tiền nhặt trong trận?").
- GameOver: không back được vào trận cũ. Chỉ [Về nhà] hoặc [Chơi lại].

## Deep-link trong game

- GameOver có dòng "Súng mới ở Shop →" (khi đủ tiền mua khẩu rẻ nhất chưa có) → nhảy thẳng Shop tab WEAPONS.
- Battle pass claim xong có nút "Quay Gacha →" nếu vừa nhận vé.
- Loadout slot trống chạm vào → gợi ý "Chưa có súng? Tới Shop" → Shop tab WEAPONS.

## Trạng thái chờ design ngoài happy-path (mỗi màn đều phải có)

- **Empty**: chưa có gì (kho súng trống, chưa có skin…)
- **Không đủ tiền**: nút mua xám + rung nhẹ + bay chữ "Thiếu 200 💰"
- **Đã sở hữu / đã claim**: card đổi trạng thái, không biến mất
- **Loading**: spinner chung của bộ GUI Pro
