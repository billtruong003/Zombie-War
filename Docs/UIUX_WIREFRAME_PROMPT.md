# ZombieWar — Prompts cho Claude Design (wireframe)

> **Archived wireframe prompt. Do not execute as a rebuild task.** The implemented visual target is
> `UI_REDESIGN_SPEC.md`; current work is data wiring, not regenerating the UI from this prompt.

> Cách dùng: copy **nguyên 1 code block** dán vào Claude Design. Mỗi prompt tự-đủ-thông-tin (Claude Design không đọc được project).
> Lý do đằng sau mọi quyết định: xem `UIUX_DESIGN_RATIONALE.md`.
> Thứ tự vẽ khuyến nghị: 1 → 2 → 3 (in-run, sắp code P0–P3) rồi 4 → 7 (meta), cuối 8–9.

---

## 1. In-Run HUD

```
Design a mobile in-game HUD wireframe for a top-down zombie survivor roguelite (Survivor.io-like), portrait 9:16 (390x844). The game uses auto-aim and auto-fire, so there is NO fire button — one-thumb play.

Layout: Full-screen gameplay view (dark battlefield, player character centered). All interactive elements sit in the bottom 40% (thumb zone); the top edge is display-only. Top edge: a full-width thin XP progress bar (4px, purple fill) flush to the very top. Below it, left corner: wave number + run timer stacked ("WAVE 7" bold, "04:32" under it). Right corner: kill count with skull icon and coin count with coin icon, stacked; pause button (small ⏸ 40x40, 50% opacity) below them. Player character at screen center has a small world-space HP bar above its head (60x6px, red fill on dark track). When taking damage, a red vignette glows around all screen edges. Bottom-left half: floating virtual joystick (outer ring 120px, inner knob 56px, 25% opacity, shown at touch position). Bottom-right corner, stacked vertically: weapon-switch button (72x72 rounded square showing current gun icon + tiny "2/3" slot pips, with a circular ammo ring around it that depletes and turns red when reloading) above a bomb button (88x88 circle, grenade icon, badge showing bomb count, cooldown radial sweep when used). Floating damage numbers rise above zombies; picked-up coins fly toward the top-right coin counter.

Palette: bg #141821, surface #1E2430, primary #4CAF6E, danger/HP #E5484D, gold/coins #F5B841, XP purple #8B7BD8, text #F4F6F8, muted #9AA3B2.
Typography: condensed bold uppercase headers (Anton-like); numbers bold tabular; body Inter 16.
Key components & states: XP bar (fills, flashes on level-up) — HP bar (world-space, shakes on hit) — red damage vignette (pulses at <30% HP) — joystick (idle hidden, appears on touch) — weapon switch (tap cycles 3 slots, disabled state when only 1 weapon) — bomb (disabled/grayed at 0 bombs, radial cooldown) — pause (opens overlay). Coin counter and XP bar are [PLACEHOLDER — loot system not wired yet] but must be drawn fully.
```

---

## 2. Level-Up Perk Overlay

```
Design a level-up perk selection overlay wireframe for a mobile top-down zombie survivor roguelite, portrait 9:16 (390x844). Time freezes when this appears — it is the key dopamine beat of a run.

Layout: The gameplay is dimmed 70% behind. Top third: "LEVEL UP!" big condensed uppercase title (56px) with a purple glow burst, small "LV 8" chip under it. Middle: three vertical perk cards side by side (each ~110x220px, 12px gap), rounded 12px, dark surface with colored rarity border (common gray, rare purple, epic gold). Each card: perk icon (48px) top, perk name bold (e.g. "DAMAGE +20%"), one short description line (muted 13px), rarity label chip at bottom. The center card is slightly larger (scale 1.05) as visual anchor. Bottom: a small muted "reroll" ghost button with dice icon [PLACEHOLDER — reroll not wired] and a tiny hint text "Tap a card to choose". Cards animate in staggered from below.

Palette: bg #141821, surface #1E2430, primary #4CAF6E, danger #E5484D, gold #F5B841, purple #8B7BD8, text #F4F6F8, muted #9AA3B2.
Typography: title condensed bold uppercase 56; card name bold 16; description Inter 13.
States: card tap = instant pick (no confirm button), picked card flashes green then overlay closes; other cards fade. Entire screen is [PLACEHOLDER — XP/perk system P3, draw fully].
```

---

## 3. Game Over

```
Design a game-over results screen wireframe for a mobile top-down zombie survivor roguelite, portrait 9:16 (390x844). This screen must celebrate the run and funnel the player toward the meta shop — it is a reward beat, not a failure screen.

Layout: Dark background with subtle red vignette. Top: "YOU DIED" condensed uppercase (40px, desaturated red — somber but brief). Center-upper: SCORE as the biggest element (gold, 64px, count-up animation), with "BEST 12,480" small comparison line under it — if beaten, a "NEW RECORD!" gold banner replaces it. Below: a stats strip of 3 compact tiles in a row (surface cards): time survived, kills, wave reached. Center-lower: "COINS EARNED" row — coin icon + amount counting up with sparkle (this is the hook into the shop). Bottom third, stacked: primary button "PLAY AGAIN" (full-width 56px, green) and secondary ghost button "HOME" (full-width 48px, outlined muted). Small "watch ad to double coins" pill above the buttons [PLACEHOLDER — monetization optional, draw it].

Palette: bg #141821, surface #1E2430, primary #4CAF6E, danger #E5484D, gold #F5B841, purple #8B7BD8, text #F4F6F8, muted #9AA3B2.
Typography: headers condensed bold uppercase; score bold tabular 64; body Inter 16.
States: PLAY AGAIN pressed = darker green + 4px edge push; coins/score animate count-up on enter; screen slides up from bottom. Score/coins values are [PLACEHOLDER — score system P1, draw fully].
```

---

## 4. Main Menu Hub

```
Design a main-menu hub wireframe for a mobile top-down zombie survivor roguelite, portrait 9:16 (390x844). Hub-and-spoke structure: every meta system is one tap away; the player's 3D character is the emotional center.

Layout: Top bar: left = player level chip + name, right = coin counter (gold icon + amount) and gem counter [PLACEHOLDER — second currency], and a settings gear (40x40). Upper-middle: game logo "ZOMBIE WAR" condensed uppercase with grunge style (moderate size, not dominating). Center: the player's full-body 3D character preview standing on a subtle circular podium, wearing currently equipped costume + holding equipped weapon — takes ~45% of screen height. Small edit-pencil chip floating beside the character linking to Costume. Below character: wide primary CTA button "PLAY" (full-width minus 32px margins, 64px tall, green, biggest interactive element on screen) with current mode label "Endless — Best: Wave 12" small above it. Bottom: fixed tab bar (5 tabs, 64px tall): SHOP, LOADOUT, PLAY (center, elevated circular tab overlapping the bar), COSTUME, STATS. Active tab tinted green with label; inactive muted icons.

Palette: bg #141821, surface #1E2430, primary #4CAF6E, danger #E5484D, gold #F5B841, purple #8B7BD8, text #F4F6F8, muted #9AA3B2.
Typography: logo/headers condensed bold uppercase; body Inter 16; counters bold tabular.
States: PLAY has idle pulse animation; tabs have pressed/active states; SHOP and STATS tabs show a red notification dot when affordable upgrade / new achievement exists [PLACEHOLDER — shop P4, stats P5, draw fully].
```

---

## 5. Loadout

```
Design a weapon loadout screen wireframe for a mobile top-down zombie survivor roguelite, portrait 9:16 (390x844). Rule set: slot 1 always holds a pistol and can never be emptied (only swapped for another pistol); slots 2–3 hold long guns and can be emptied; one separate bomb slot.

Layout: Top bar: back arrow left, title "LOADOUT" condensed uppercase centered, coin counter right. Upper section: horizontal slot bar with 4 slots — three square weapon slots (88x88, rounded 12px) then a visually distinct circular bomb slot (72px) after a small divider. Slot 1 shows a small padlock badge in its corner with tooltip "Pistol — survival weapon, swap only". Empty slots 2–3 show a dashed border + "+" icon. Selected slot has a green outline glow. Middle: label row "CHOOSE WEAPON — SLOT 2" (updates per selected slot) with a class filter note (muted): picker only ever shows weapons valid for the selected slot. Lower 55%: scrollable 3-column grid of weapon cards (each ~104x128): weapon icon, name (13px), tier stars, small stat chips (DMG / RATE). Currently equipped card shows a green "EQUIPPED" ribbon; owned-but-unequipped are normal; locked weapons are darkened with a lock and price [PLACEHOLDER — unlock via shop P4]. Bottom: an "AUTO-EQUIP BEST" ghost button.

Palette: bg #141821, surface #1E2430, primary #4CAF6E, danger #E5484D, gold #F5B841, purple #8B7BD8, text #F4F6F8, muted #9AA3B2.
Typography: headers condensed bold uppercase 28; card text Inter 13; stats bold tabular.
States: tap card = instant equip into selected slot (no confirm); tap equipped card in slot 2–3 = unequip; slot-1 unequip attempt shakes the padlock. Selecting a different slot re-filters the grid.
```

---

## 6. Costume

```
Design a character costume customization screen wireframe for a mobile top-down zombie survivor roguelite, portrait 9:16 (390x844). Modular character: parts swap per body slot with instant preview — trying clothes must be zero-friction (no confirm button).

Layout: Top bar: back arrow left, title "COSTUME" condensed uppercase centered, coin counter right. Upper 45%: large 3D character preview on a podium, drag-to-rotate hint (circular arrows icon, muted). Middle: horizontal scrollable slot tab row (chips 40px tall): HAIR, HEAD, BODY, LEGS, HANDS, BEARD — active chip filled green, inactive outlined muted. Lower 45%: 4-column grid of part thumbnails (each 80x80 rounded 8px) for the active slot; first cell is always "— Default —" (none). Equipped part has green border + check badge. Locked/premium parts darkened with lock + price tag [PLACEHOLDER — cosmetic shop P4]. Tap part = applies to the preview instantly; auto-saved on leaving the screen (small "Saved ✓" toast bottom).

Palette: bg #141821, surface #1E2430, primary #4CAF6E, danger #E5484D, gold #F5B841, purple #8B7BD8, text #F4F6F8, muted #9AA3B2.
Typography: headers condensed bold uppercase 28; chips/labels Inter 14.
States: slot chip active/inactive; part cell states: default, equipped (green border), locked (dark + lock); preview character plays a short reaction animation when a part is applied.
```

---

## 7. Shop

```
Design a meta shop screen wireframe for a mobile top-down zombie survivor roguelite, portrait 9:16 (390x844). Players spend coins earned from runs between sessions. [ENTIRE SCREEN IS PLACEHOLDER — economy P4 not wired; design it fully anyway.]

Layout: Top bar: back arrow left, title "SHOP" condensed uppercase centered, coin counter right (gold, prominent — always visible while shopping). Below: segmented tab control with 3 tabs: WEAPONS, UPGRADES, COSMETICS. Content: scrollable 2-column card grid (cards ~170x200, rounded 12px, surface color) — each card: item icon large, name bold 14, rarity border color (common gray / rare purple / epic gold), short effect line (muted 12px, e.g. "+5% damage"), and a price button at the bottom of the card (full-card-width, 40px): green with coin icon + price when affordable; dark red desaturated + price when unaffordable (disabled); "OWNED" flat gray when purchased; "EQUIP" outline variant for owned cosmetics. One featured card spans both columns at top ("DAILY DEAL" ribbon, gold border, crossed-out old price) [PLACEHOLDER]. Upgrades tab cards show a level pip row (e.g. ● ● ○ ○ ○) and price scaling per level.

Palette: bg #141821, surface #1E2430, primary #4CAF6E, danger #E5484D, gold #F5B841, purple #8B7BD8, text #F4F6F8, muted #9AA3B2.
Typography: headers condensed bold uppercase 28; card name bold 14; prices bold tabular.
States: affordable (green, pressable) / unaffordable (disabled red, coin counter shakes on tap) / owned / equip; purchase success = card flash + coin counter ticks down.
```

---

## 8. Pause + Settings overlay (chung 1 family)

```
Design two stacked modal overlays for a mobile top-down zombie survivor roguelite, portrait 9:16 (390x844): a pause menu and a settings panel, sharing one visual family (centered modal card on 70% dimmed gameplay).

PAUSE: centered modal card (320px wide, rounded 16px, surface color): title "PAUSED" condensed uppercase 32, then run stats mini-row (wave, time, kills — muted), then stacked buttons: "RESUME" (green, 52px, primary), "SETTINGS" (outline, 48px), "QUIT RUN" (ghost red, 44px — with inline confirm state: first tap turns it into "TAP AGAIN TO QUIT" solid red for 3s, to prevent accidental run loss).

SETTINGS (opens over pause, or from main menu gear): same modal family, title "SETTINGS": Music volume slider and SFX volume slider (with icons, 0–100), Haptics toggle switch, Graphics quality segmented control (LOW / MED / HIGH), language row [PLACEHOLDER — localization], small version text bottom "v0.1.0". Back chevron top-left of the modal returns to pause/menu. [PLACEHOLDER — settings persistence P8, draw fully.]

Palette: bg #141821, surface #1E2430, primary #4CAF6E, danger #E5484D, gold #F5B841, purple #8B7BD8, text #F4F6F8, muted #9AA3B2.
Typography: titles condensed bold uppercase 32; labels Inter 16.
States: sliders with knob + filled track; toggle on(green)/off(gray); QUIT RUN two-step confirm; overlays animate scale-in 0.2s.
```

---

## 9. Stats / Achievements

```
Design a stats & achievements screen wireframe for a mobile top-down zombie survivor roguelite, portrait 9:16 (390x844). [ENTIRE SCREEN IS PLACEHOLDER — persistence P5; design fully.]

Layout: Top bar: back arrow, title "STATS" condensed uppercase, settings gear right. Upper section: personal-best summary card (surface, rounded 12px): "BEST RUN" label, big gold score, sub-row: best wave / longest time / total kills (3 mini stats). Below: segmented control: RECORDS | ACHIEVEMENTS. ACHIEVEMENTS tab: vertical scrollable list of achievement rows (64px tall): icon in a rounded square (gold when unlocked, dark gray silhouette when locked), name bold 14, one-line requirement (muted 12px), right side: progress bar with fraction "34/50" for in-progress, gold check for done, and a "CLAIM" green button state for completed-but-unclaimed rewards (+coin amount). Unclaimed achievements sort to top with a subtle glow.

Palette: bg #141821, surface #1E2430, primary #4CAF6E, danger #E5484D, gold #F5B841, purple #8B7BD8, text #F4F6F8, muted #9AA3B2.
Typography: headers condensed bold uppercase 28; rows Inter 14; numbers bold tabular.
States: locked / in-progress (bar) / claimable (green CLAIM, glow) / claimed (check, dimmed); CLAIM press = coin fly-to-counter animation.
```

---

## ADDENDUM v1.1 — Economy wireframe blocks (sync với ECONOMY_DESIGN.md)

- **Currency cluster (mọi màn meta):** góc phải-trên: [Coin icon + số] [Gold icon + số] [Gem icon + số], mỗi cụm có nút "+" (Gem → điểm cắm IAP sau).
- **SHOP tab bar đổi thành:** WEAPONS | GACHA | COSTUME | UPGRADES.
- **GACHA tab:** 2 banner card lớn xếp dọc — "Weapon Gacha" (nút PULL x1 / x10, giá Gold) và "Skin Gacha" (giá Gem); dưới mỗi banner: dòng rate + pity progress bar.
- **WEAPONS tab:** card súng có badge currency (Coin/Gold); súng gold-only có ribbon "GOLD"; súng gacha-only ghi "GACHA" thay giá.
- **COSTUME tab:** grid theo category (Hair/Face/Beard/Chest/Leg/Feet/Head); mỗi màu râu/tóc = 1 ô option riêng; ô locked = icon khoá + price; SET full-body hiện card ngang riêng phía trên grid với giá Gem (200/500/1000) + preview full-body.
- **GAMEOVER:** thêm block payout breakdown (3 dòng + tổng), Gem line riêng nếu có.
- **HUD in-run:** Coin counter cạnh wave counter; toast nhỏ khi nhặt Gold/Gem.
- **REVIVE modal:** trước GameOver — "Watch ad để hồi sinh (1 lần)" [PLACEHOLDER], nút Skip, countdown 5s.
- **FTUE overlay:** 3 tooltip contextual (move/auto-shoot/bomb) + highlight Gacha lần đầu về Hub (rigged SMG) + highlight PLAY.

---

## ADDENDUM v1.2 — sync bộ docs designer (2026-07)

Bộ docs mới cho designer đã tách riêng: `DESIGN_BRIEF.md` → `SCREEN_FLOW.md` → `SCREEN_SPECS.md` → `DESIGN_AI_PROMPT.md`. File này giữ làm tham khảo wireframe nền; nếu mâu thuẫn thì **SCREEN_SPECS.md thắng**. Điểm đổi so với v1.1:

1. **Rarity 5 màu** dùng chung súng + skin: ⬜Xám 🟩Xanh lá 🟦Xanh biển 🟪Tím 🟧Cam — mọi card viền theo rarity (thay hệ tier 0–3 cũ).
2. **Shop thêm tab UPGRADES** (4 tab: WEAPONS | GACHA | COSTUME | UPGRADES): đúng 3 dòng nâng người Sát thương/Máu/Tốc bắn, 5 nấc/dòng, trả bằng Coin.
3. **Battle pass screen** mới: free track + 3 quest ngày; hàng Premium khoá [PLACEHOLDER].
4. Nhắc lại luật cứng: **không nút bắn / không nút nạp đạn** trong HUD (auto-fire, auto-reload).
5. Gacha: trùng súng → mảnh ★; thanh pity "chắc chắn Tím+ sau N lượt"; FTUE rigged SMG Xanh lá.
