# ZombieWar — Product Roadmap (tới Publish)

> Roguelite top-down survival shooter. **Không time-box** — chỉ phase + scope. Dependency-ordered:
> phase sau dựa trên phase trước. Mỗi item đánh ✅ done / �27 in-progress / ⬜ todo.
> Nguyên tắc: đóng core loop & chứng minh *vui* trước, rồi mới bung bề rộng & meta, cuối là polish/publish.

---

## 🧱 ĐÃ CÓ (foundation — không phải làm lại)
- **Player**: twin-stick move, strafe locomotion (Malbers), camera follow, IK aim (Multi-Aim + two-bone hands + foot IK), health, weapon system (6 súng + switch), bomb, magazine/reload.
- **Zombies**: inheritance (Melee/Ranged/Speed/Boss), VAT rendering, pooling, tiered LOD, NavMesh chase, auto-aim density-seeking.
- **Waves**: WaveDirector + ZombieSpawner (pooled).
- **Architecture**: Bill services (Events/Pool/State/Audio/Scene), GameStateMachine (Boot/Menu/Gameplay/Pause/GameOver), bootstrap + additive scene load.
- **FX**: mesh tracer, muzzle flash, impact, recoil, camera shake→impulse.
- **Tools**: VAT baker, modular costume extractor, player rig builder, scene flow builder.
- **Core loop fix**: ✅ PlayerController (damage nhìn thấy + chết + GameOver).

---

## P0 — Core Loop Closure *(make it a game)*
Mục tiêu: bật lên là chơi được, thắng/thua rõ.
- ✅ PlayerController: damage→HUD bar, death→GameOver.
- ⬜ **Damage number popup** (pooled TMP 3D + tween up/fade) — feedback khi bắn. *(đang thiếu)*
- ⬜ Hit feedback: flash/impact khi zombie trúng đạn.
- ⬜ Convert win→**endless**: bỏ win-state, chết = hết run.
- ⬜ Verify loop end-to-end (spawn→hit→bar tụt→chết→GameOver→restart).

## P1 — Endless Combat Core
Mục tiêu: sống càng lâu càng khó, có điểm.
- ⬜ Endless WaveDirector + **difficulty scaling** (density, HP, speed, elite theo time).
- ⬜ **Score system**: survival time + kills + combo/multiplier.
- ⬜ GameOver hiện score + high score + restart.
- ⬜ Arena giới hạn (bounded map) — *streaming cắt, để post-launch*.

## P2 — Loot & In-Run Economy *(dopamine loop)*
Mục tiêu: giết → rớt → nhặt → mạnh lên.
- ⬜ **Loot drop system**: zombie chết spawn pickup (pooled) theo **DropTable** (chance-based).
- ⬜ **Pickup types**: coin, cục máu (heal), cục dmg, ammo, XP.
- ⬜ **Magnet**: hút pickup trong bán kính về player.
- ⬜ Currency in-run + XP counter + VFX/SFX nhặt.

## P3 — In-Run Progression *(roguelite level-up)*
Mục tiêu: mỗi run mạnh dần, có lựa chọn.
- ⬜ XP → **level-up → chọn 1/3 perk** (card pick).
- ⬜ **PlayerStats** data-driven (modifier cộng dồn).
- ⬜ Perk pool: dmg, fire rate, move speed, maxHP, pickup radius, crit, multishot, pierce, lifesteal…
- ⬜ Scale khó vs sức mạnh player (power fantasy nhưng vẫn thua được).

## P4 — Meta Economy: Shop + Inventory + Character
Mục tiêu: tiêu tiền giữa run, tạo competitive/collection.
- ⬜ **Meta currency** (giữ giữa run).
- ⬜ **Shop hub**: mua/unlock/nâng súng + stat + cosmetic.
- ⬜ **Inventory + equip/loadout** (thay list súng cứng bằng loadout chọn được).
- ⬜ **Modular character wear** (runtime apply — hiện mới có extraction tool).

## P5 — Persistence & Meta Progression
Mục tiêu: tiến trình được lưu, có mục tiêu dài.
- ⬜ **Save/persistence** (currency, unlocks, high score, settings) — JSON/BillSave.
- ⬜ **Achievements** (event-driven) + unlock gating.
- ⬜ **Leaderboard** (local; online optional).
- ⬜ (optional) daily/seeded run.

## P6 — Content
Mục tiêu: đủ đa dạng để chơi lâu.
- ⬜ Enemy variety: thêm zombie types, elites, **bosses**, affixes.
- ⬜ Weapon variety + weapon feel riêng từng súng.
- ⬜ Arena/biome variety.
- ⬜ Content balance pass.

## P7 — Game Feel & AV Polish
Mục tiêu: cảm giác "product".
- ⬜ Juice: damage numbers, hit feedback, screen shake, muzzle/impact VFX pass.
- ⬜ **Audio**: full SFX bank + music + mixer.
- ⬜ **UI/HUD skin** + transitions + juice.

## P8 — UX, Menus & Onboarding
- ⬜ Main menu, pause, **settings** (audio/graphics/controls/bindings).
- ⬜ Tutorial/onboarding.
- ⬜ Mobile controls polish (joystick) nếu target mobile.
- ⬜ (optional) localization.

## P9 — Hardening
- ⬜ Save/load robustness + versioning.
- ⬜ **Performance pass** (pool audit, draw calls, GC spikes).
- ⬜ Analytics hooks.
- ⬜ QA / bug bash pass.

## P10 — Release & Publish
- ⬜ Platform build(s) (target: PC/mobile?).
- ⬜ Store page: screenshots, **trailer**, description.
- ⬜ Build pipeline, signing, cert (store requirements).
- ⬜ (nếu thương mại) monetization: IAP/ads/premium + economy tuning.
- ⬜ Marketing assets + soft launch → launch.

---

## 🛑 Cắt / để post-launch
- **Chunk world streaming / procedural infinite map** — endless chạy arena giới hạn là đủ; streaming là bản mở rộng sau khi core vui đã chứng minh.

## ✅ "Publishable" nghĩa là
Endless chơi được vòng lặp: **giết → loot → level-up → chết → tiêu tiền shop → mạnh hơn → chơi lại**, có save + achievement + leaderboard, đủ content để chơi 30+ phút, juice + audio + menu hoàn chỉnh, build ổn định trên 1 platform + store page.

## 👉 Next ngay
Đóng **P0 → P2** (damage text + endless + loot loop) = có ngay bản chơi được & vui để test. Đây là khúc quyết định nhất.
