# ZombieWar — Remaining Features & Next Steps

> Snapshot tại thời điểm commit "gun aim fix + roadmap note". Top-down zombie survival shooter.
> Đã wired-thật (không phải wireframe): win/lose loop, Level 1 scene, wave director, weapon roster, zombie AI/VAT/pooling.
> Còn lại chia theo mức độ chặn "playable".

---

## ✅ Vừa xong (session này)
- **AimType fix (pistol 1 tay ↔ súng 2 tay)** — 3 tầng:
  1. Data: set `twoHanded: 1` cho `WD_Rifle / WD_Shotgun / WD_Sniper / WD_LMG` (trước đó thiếu field → default false).
  2. Prefab: add lại `ZombieWar.WeaponIKController` vào root `Player.prefab` (đã bị gỡ ở fix rig crash).
  3. Code: `Weapon.CurrentGrips` getter + `WeaponIKController.OnEnable` gọi `HandleWeaponEquipped` ngay → AimType đúng cả lần equip đầu (spawn-order independent).

---

## 🔴 A. Functional gaps — PHẢI wire (chặn playtest/balance)
| # | Feature | Ghi chú |
|---|---------|---------|
| 34 | **Bomb throw button** trong HUD → `BombThrower.TryThrow` | Bomb có logic, thiếu nút trigger |
| 41 | **Weapon-switch button** trong HUD → `Weapon.SwitchWeapon()` | Chưa wire nút đổi súng |

> Không có 2 nút này thì không thể test đủ đồ chơi để cân bằng.

---

## 🟡 B. BALANCE pass — tuning số (quyết định game vui/dở, KHÔNG fix-sau được)
Systems đủ, cần chỉnh **số**:
1. **Wave curve** (`Assets/_Project/Data/Waves/WD_Level1.asset`): số wave, zombie/wave, `spawnInterval`, `maxConcurrent`, độ khó tăng dần, boss xuất hiện wave mấy.
2. **Player survivability**: HP + speed + i-frame vs zombie damage/speed.
3. **Weapon economy**: damage từng súng vs zombie HP (TTK), ammo/magazine/reload, súng trị loại nào; recoil/spread ảnh hưởng TTK.
4. **Zombie mix**: tỉ lệ Melee/Ranged/Speed/Boss theo wave.
5. **Bomb**: cooldown / damage / radius.

> Cách làm: wire nhóm A → playtest Level 1 end-to-end 1 lần → dump bảng số tunable → chỉnh cho "winnable but hard".

---

## 🟢 C. Defer được (wireframe/juice — fix sau OK)
| # | Feature |
|---|---------|
| 36 | Súng hai tay: support-hand IK (left hand grip) — visual cầm 2 tay |
| 39 | Damage number popup: pool + TextMeshPro 3D (không UI) + tween up/fade |
| 40 | Phase D: Weapon roster đầy đủ + tiering + HUD icon |
| 21 | UI/HUD skin (đang wireframe, chạy được) + mobile joystick polish |
| 13 | Phase 5: Juice, audio hooks, camera shake/impulse, hit feedback |

---

## 🎯 Phase lớn còn lại (roadmap)
- **Phase 4** (#12): Wave + **world streaming** + Level 1 hoàn thiện — scene + director đã có, cần streaming + balance.
- **Phase 5** (#13): Juice, audio, UI/HUD polish.
- **Phase 6** (#14, bonus): Level 2 — slope terrain + Kaiju boss (chỉ nếu còn thời gian).
- **Phase 7** (#15): End-to-end playtest + polish pass.

---

## 📌 Thứ tự đề xuất khi quay lại
1. **Nhóm A** — wire bomb button + weapon-switch button (nhanh).
2. **Playtest** Level 1 end-to-end 1 lần.
3. **Nhóm B** — balance pass (dump bảng số → chỉnh).
4. Nhóm C / Phase 5 — juice & polish.
5. Phase 6 bonus nếu dư thời gian.

## Key paths
- Player prefab: `Assets/_Project/Prefabs/Player.prefab`
- Weapons data: `Assets/_Project/Data/Weapons/WD_*.asset`
- Wave data: `Assets/_Project/Data/Waves/WD_Level1.asset`
- Scenes: `Assets/_Project/Scenes/{Bootstrap,Menu,Map_Level1}.unity`
- Gameplay scripts: `Assets/_Project/Scripts/Runtime/Gameplay/`
- HUD: `Assets/_Project/Scripts/Runtime/UI/HudController.cs`
- Design docs: `Docs/GAMEPLAY_DESIGN.md`, `Docs/TASK_BREAKDOWN.md`, `Docs/EDITOR_SETUP_CHECKLIST.md`
