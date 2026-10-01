# Zombie War — trạng thái thật của project

> **2026-10-01 — Catch-up sau phase A, B, C, E (commits `eaaccef6`..`05a8256c`).** Khối này thắng mọi
> khối và mục bên dưới khi mâu thuẫn. Soát bằng đọc code và asset; đường dẫn tính từ `Assets/_Project/Scripts/Runtime/`
> nếu không ghi khác. Việc còn mở và quyết định: board "HordeCall Board" (artifact, db `items`).
>
> **Trong trận — đã có**
> - Vòng trận: `GameFlow.StartGameplay` → `RunState.Begin`, một scene `Map_Level1`. Chỉ có mode Endless; campaign,
>   chọn màn và wave đã xoá từ M7 (§2–3 bên dưới đã cũ). Kết thúc: Died hoặc Abandoned, giữ 100% coin
>   (`Systems/RunClosure.cs:19-22`, owner chốt; GAME_DESIGN.md còn ghi 25%/0% là cũ).
> - ThreatDirector: tier = trạm xong + mỗi 90 m + mỗi 90 s, tối đa 30 (`Gameplay/Threat/ThreatDirector.cs:266-274`);
>   tier 0–3 nằm trên `Prefabs/Player.prefab`; quái của map cộng vào tier 0/2/3 (`World/Maps/MapTheme.cs`);
>   từ tier 6 quái +4% chỉ số mỗi tier; pool nạp trước một tier, mỗi frame một loại.
> - Skill: 82 thẻ (10 chỉ số, 14 signature, 12 universal, 23 autonomous, 19 tiến hoá, 4 BONUS)
>   (`Gameplay/Skills/SkillCatalogDefs.cs:204-365`), đủ module chạy và đủ icon (`UI/Data/SkillIconSet.asset`).
>   Rank 5, 6 ô skill + 4 ô chỉ số, mở thẻ theo cấp tài khoản LV1–34 (`:382-389`), popup mở thẻ sau trận.
>   Tiến hoá chỉ từ rương (elite 4% × May mắn, pity 45 s, boss Beacon, Supply Drop 20%).
> - Súng: 54 `Data/Weapons/WD_*` (không phải 25), auto-aim, tự bắn, một súng mỗi trận.
> - Quái: 33 loại (16 Cute + 17 Blob), đều VAT; hành vi walker, pouncer, ranged, burrower, charger, boss;
>   boss chỉ qua trạm Boss Beacon (CactusBoss, MoleRatKing, SkeletonGiant).
> - Trạm: Signal Relay, Supply Cache, Boss Beacon, Supply Drop, Heal Zone (`Gameplay/Stations/`);
>   vật phẩm Nam châm, Bom, Đồng hồ băng chỉ rơi từ elite (`Gameplay/Pickups/MechanicItems.cs`).
> - Map: 5 theme bake sẵn (meadow mặc định, forest, swamp, volcano, tundra), mỗi map 192 m cuộn vòng, ô 32 m,
>   stream 5×5 (`World/Maps/BakedMapStreamer.cs`); vùng lõm là vật cản có cầu, quái theo flow field
>   (`World/Nav/MapNavigator.cs`); bảng màu riêng mỗi theme (`Editor/World/EnvThemeLook.cs`); thế giới procedural
>   cũ còn làm dự phòng. Map đã commit: mesh trong kho nhị phân nén qua LFS (`BakedMeshStore`), 162 MB.
> - Âm thanh: catalog 397 key; khoảng 105 key được dùng; nhạc hub + một nhạc trận chung cho mọi map.
> - Công cụ: bảng QA 9 tab (có MAP), SkillSandbox, EnvSandbox, GameShot, HordeStressTest.
>   Test: EditMode 784 pass, PlayMode 227 pass (01/10).
>
> **Meta — đã có:** Home v2, Arsenal, Shop v2, Gacha (3 banner), Pass 30 cấp + nhiệm vụ, Daily (7 ngày + stamp 28 ngày),
> Studio, Profile (avatar, khung), Settings (đồ hoạ Low/Mid/High, âm lượng, 30/60 fps, rung, xoá dữ liệu),
> Revive + Result v2. Cấp tài khoản: LV2 Pass/nhiệm vụ, LV3 Gacha/Events (chỉ chặn ở UI), LV5 nâng sao (chặn trong
> `Systems/PlayerProfile.cs:814`). Lưu: một JSON trong PlayerPrefs, chỉ trên máy.
>
> **Còn thiếu cho V1 (xếp theo mức chặn)**
> 1. Lên store: chưa có keystore, package id còn là id mẫu Unity (`ProjectSettings.asset:170`, ReleaseGuard không bắt
>    vì so chữ hoa thường, `Editor/BuildTools/HordeCallBuild.cs:20`), productName "Zombie War", chưa gắn icon,
>    chưa build Android lần nào, chưa đo trên máy thật, chưa có privacy policy.
> 2. SDK: không có Firebase, quảng cáo, IAP, UMP (`Systems/Purchases.cs` chỉ giả lập; comment còn ghi AppLovin, đã bị loại).
> 3. FTUE: flow 15 bước đã duyệt (board `tk-ftue-review`), chưa làm.
> 4. Game không tự dừng khi ra màn hình chính: `AppPauseEvent` (BillGameCore `Core.cs:86`) không có ai nghe.
> 5. Người chơi chưa chọn được map (chỉ có cheat QA); chưa có thiết kế mở map theo cấp.
> 6. 17 Blob không có âm thanh tấn công, bị đau, chết (`Data/Zombies/ZD_Blob*.asset`); trạm không có âm riêng;
>    một nhạc trận cho cả 5 map.
> 7. Thùng rơi máu (`Prefabs/Props/PROP_*`) không được prefab hay asset nào dùng (scene `Map_Level1` lưu nhị phân, chưa
>    soát), nên máu chỉ đến từ Heal Zone, Siphon, Regen, thẻ Heal, hồi sinh.
> 8. Loot (gem, rương, vật phẩm) chỉ từ elite, mà elite chỉ vào từ tier 3: đầu trận gần như không có loot.
> 9. Chưa có điểm theo kill (chỉ lưu thời gian sống lâu nhất); cần mockup màn kết quả.
> 10. PowerBudget thiếu hệ số cho 7 power (Emergency, Frost Nova, Fire Trail, Thorns, Ice Shards, Landmine, Time Warp).
> 11. Meta: không có bản dịch (mọi chữ hard-code tiếng Anh); lưu không mã hoá; ngày/giờ lấy từ đồng hồ máy (dễ gian lận);
>     màn Events chưa có (nút mở Gacha); banner gacha thường dùng pity 30, không 50/50 (khác banner sự kiện 90 + 50/50);
>     gói 2 vé giá 50 gem rẻ hơn quay đơn 30 gem; Settings còn Language, Help, Ad privacy, Privacy, Restore là "Coming soon".
> 12. Dọn: màn M8 cũ trong Menu.unity, kết quả/hồi sinh cũ trong `UI/RunOverlays.cs`; GAME_DESIGN.md và §1–5 dưới đây đã cũ.

> **2026-09-27 — M10 Meta v2 (commits `daa3e94f`..`3735a2dc`, chờ owner duyệt trước M11).**
> - Menu mở vào **Home v2** (`Runtime/UI/V2/HomeScreen.cs`; `GameFlow.EnterMenu` → `Replace<HomeScreen>`).
>   Màn v2: Home, Profile, Settings, Daily, Pass (`PassScreenV2`), Arsenal, Shop (`ShopScreenV2`), Gacha,
>   Studio; Revive + Result v2 nằm trong `UI_Hud.prefab` (`RunEndV2`). Màn M8 cũ còn trong Menu.unity
>   (ẩn) tới bước dọn dẹp.
> - Dựng lại bằng `HordeCall/UI v2/Build All + Shots` rồi `Install Into Menu` (`Editor/UI/V2/*Builder.cs`);
>   `Build Run End (into UI_Hud)`. Ảnh kiểm tra 4 tỉ lệ màn (16:9, 19.5:9, 20:9, tablet) ở `Review/V2/shots/*_sheet.png`.
> - Luật: `DailyRewards`, `PassRewards` (30 cấp × 750 XP, XP chỉ từ nhiệm vụ), `ShopOffers`, `GachaBanners`,
>   `ReviveRules`, `GameSettings`; skin cộng sát thương (`WeaponSkins.DamageBonus`, áp trong `Weapon.ApplyHit`).
> - Tiền thật và quảng cáo mới là hook (`Purchases`, `RewardedAds`): bản Dev giả lập thành công, bản Release
>   báo "chưa mở" — SDK thật ở M11. Gold = Coin: nâng sao trừ Coin.

> **2026-09-27 — M9 (HordeCall).** Điều gì dưới đây mâu thuẫn với khối này thì khối này thắng.
> - **Tên game: HordeCall** (store: "HordeCall: Zombie Shooter"). Tên hiển thị đã đổi ở splash và bảng cheat;
>   `productName`, bundle id và namespace `ZombieWar.*` giữ nguyên tới lúc phát hành.
> - **Payout:** mọi kết thúc trận giữ 100% coin (`Systems/RunClosure.cs:19-22`). Giá súng theo bảng D2
>   (`Data/Weapons/WD_*.asset`, commit `0dd77756`).
> - **Meta v2 đã được owner duyệt** (canvas "HordeCall UI Mockups", trang "Meta v2 (current)"): Home, Studio,
>   Arsenal, Shop, Gacha, Pass, Daily, Revive, Result, Profile, Settings, Leaderboard, Splash. Agent được dựng
>   UI theo đúng mockup đã duyệt; thay đổi layout ngoài mockup vẫn phải hỏi owner.
> - **Cấp tài khoản:** `Systems/AccountProgress.cs` + `PlayerProfile.AccountXp/AccountLevel`; trận trả XP qua
>   `RunClosure.Close`. Mở khóa: LV2 Pass/nhiệm vụ, LV3 Gacha/Events, LV5 nâng sao.
> - **Build:** `ZW_CHEATS` đã gỡ; cheat chỉ có trong Editor và bản Development. `HordeCall/Build/*` build
>   Android Dev/Release; `ReleaseGuard` chặn bản Release còn cheat hoặc bundle id mặc định.
> - **UI kit v2:** `Scripts/Editor/UI/V2/UIKitV2.cs` (thành phần theo design system đã duyệt),
>   `UiShot.cs` (render prefab ra PNG không cần Play), `Runtime/UI/Core/GridFit.cs`.
> - **Backend để sau:** leaderboard, bạn bè, lưu đám mây, thư từ server, cấu hình sự kiện từ xa chỉ thiết kế.
> - Lộ trình M9–M14: artifact "HordeCall Roadmap".


> **2026-09-25 — M7 Slice A.** Phần "M7 Slice A — DELIVERED" trong `MVP_SHIP_PLAN.md` là trạng thái
> hiện hành và THẮNG mọi mô tả cũ bên dưới về campaign/stage, wave, Victory, 3 slot súng, bom,
> perk pool 7 thẻ, gacha/nâng sao trong Shop. Các mục đó đã bị xoá khỏi code.

> **2026-08-08 CONSOLIDATION DELTA:** Header/baseline chi tiết bên dưới là snapshot 2026-07-31 tại
> `68fbc090`; working tree hiện tại mới hơn và là authority khi mâu thuẫn. Các correction đã verify:
> run identity/terminal closure, bomb-pickup charge và weapon null/empty guards đã có source/tests;
> audio runtime/catalog đã được mở rộng; campaign selector Phase 1 đã tồn tại và pass closure evidence;
> perk choice/application vẫn chưa end-to-end; `Weapon.EquipData()` vẫn refill magazine và xóa reload khi swap. Design truth
> hiện nằm trong `DESIGN_BRIEF.md` và ba canonical file dưới `Reference/Design/`. Không dùng các gap
> G1/G7/G10 cũ dưới đây như task mới nếu chưa re-check source.

**Cập nhật:** 2026-07-31 · **HEAD:** `68fbc090` · **Branch:** `main`
**Cách lập:** đọc source/asset thật + đọc scene qua Unity MCP. Mọi khẳng định có `file:line`
hoặc nguồn MCP. Không chép lại doc cũ.

> ⚠️ **Đọc scene phải qua Unity MCP.** `Map_Level1..5.unity` là file binary — grep không ra gì
> KHÔNG có nghĩa là scene trống. Dùng `manage_scene` (load additive → `set_active_scene` →
> `get_hierarchy`) rồi `close_scene` với `remove_scene=true`, và **không save**.

> Đây là tài liệu trạng thái **chuẩn**. Nó thay thế `Deprecated/ACCOUNT_SWITCH_HANDOFF.md`
> và `Deprecated/HANDOFF.md`. Scope, task và milestone nằm duy nhất ở `MVP_SHIP_PLAN.md`;
> framework nằm ở `FRAMEWORK.md`.

---

## 0. Luật cứng (không đổi)

1. **UI ownership.** `Menu.unity` và mọi `Assets/_Project/UI/Prefabs/Screens/UI_*.prefab` là
   của owner tự vẽ tay. Agent **không sửa** layout/prefab/scene UI, không mở/save Menu scene
   như side-effect. Việc UI duy nhất được phép: **code tween/animation (.cs)** khi được yêu cầu rõ.
   Sau mọi thao tác editor phải check `git status` và revert file UI lạ ngay.
2. **Cấm DOTween.** Chỉ dùng BillTween/UITransition — xem `FRAMEWORK.md`.
3. Không rename symbol bằng find-replace. Chạy GitNexus impact analysis trước khi sửa symbol,
   `detect_changes()` trước khi commit.
4. Không stage/commit/push trừ khi task yêu cầu rõ.
5. Test UI phải Play từ `Bootstrap.unity`. Play thẳng Menu/Map sẽ spam `SERVICE NOT FOUND` —
   đó không phải bug.
6. Không thêm Addressables/Resources migration mới trong phase này. (Audio **đã** dùng
   Addressables từ trước — xem §3.)
7. Không rebuild Player skeleton/Animator/WeaponRig/GunMount/RecoilPivot. Đọc
   `Reference/Technical/PlayerRigSocketIncident.md` trước khi đụng vùng đó.

---

## 1. Snapshot kỹ thuật

| Mục | Giá trị | Nguồn |
|---|---|---|
| Unity | 6000.3.10f1, URP, portrait 1080×1920 | `ProjectSettings/ProjectSettings.asset` |
| Framework | BillGameCore 3.0.0 + BillInspector + BillTween | `Assets/ThirdParty/BillGameCore/package.json` |
| Shader kit | `com.billtruong.stylized-toon-world-kit` 0.6.0 (embedded) | `Packages/` |
| Scene flow | `Bootstrap` → additive `Menu` / `Map_Level1..5` | `Runtime/Flow/GameFlow.cs` |
| Runtime C# | 163 file, ~13.2k dòng | `Assets/_Project/Scripts/Runtime` |
| EditMode test | 159 `[Test]`/`[UnityTest]` | `Assets/_Project/Scripts/Tests/EditMode` |
| Build scenes | 7 scene đã đăng ký (Bootstrap, Menu, Map_Level1..5) | `ProjectSettings/EditorBuildSettings.asset:7-28` |
| Bundle id | ⚠️ vẫn là template default | `ProjectSettings.asset:170` |
| Scripting define | ⚠️ `ZW_CHEATS` đang BẬT cho Android/iOS/Standalone | `ProjectSettings.asset:833-835` |

---

## 2. Cái đã chạy thật (verified)

### 2.1 Combat core — chắc, đừng viết lại

- **Auto-aim** có chống rung đầy đủ: stickiness, min-dwell, danger-radius override, rate-limit
  vector aim để đạn luôn bay đúng hướng nòng đang chỉ. `Runtime/Gameplay/PlayerMovement.cs:102-159`
- **Auto-fire + auto-reload**, không nút bắn/nạp. `Runtime/Gameplay/Weapon.cs:188-208`
- **Recoil spring 3 trục** (lùi -Z, hất pitch, lệch yaw) với phase golden-ratio kiểu blue-noise.
  `Weapon.cs:524-543`, spring hồi ở `:175-184`
- **Hitscan + PiercingLine**: `RaycastNonAlloc` + sort theo cự ly + falloff xuyên.
  `Weapon.cs:385-423`
- **Hit feedback**: hit flash per-instance qua MaterialPropertyBlock, damage number, hit-react
  một lần (rapid fire không lock flinch), knockback, dissolve khi chết.
  `Runtime/Gameplay/Zombies/ZombieBase.cs:379-432, 476-509`
- **Camera shake** khi bắn và khi bom nổ. `Weapon.cs:329-335`, `Bomb.cs:74`

### 2.2 Enemy roster

15 quái Cute Series đã bake VAT (MeshRenderer + VAT_Animator, không Animator/SkinnedMesh),
6 class hành vi: Walker · Runner → Pouncer · Ranged · Burrower · Boss → Charger.
Bảng số liệu: `ENEMY_ROSTER_AUDIT.md`. HUGO bị chặn (16.567 vert > giới hạn texture).

### 2.3 Spawn / NavMesh

NavMesh chỉ bake từ layer `WalkableGround`; spawn kiểm capsule clearance + `PathComplete`;
12/12 spawn path hợp lệ trên cả 5 map. Chi tiết: `Reference/Technical/TASK1_SPAWN_NAVMESH_GENERATOR.md`.

### 2.4 Meta / economy backend

- `PlayerProfile` (1.614 dòng) là save authority qua `Bill.Save`: ví, sở hữu súng, 3 slot,
  shard/sao, costume, gacha pity, mission progress.
- `RunState.Payout()` **idempotent**, `Abandon()` không bank. `Runtime/Systems/RunState.cs:171-180`
- `GachaService` deterministic theo seed, atomic debit, pity, đền bù trùng.
- `WeaponUpgradeMath` sao súng ảnh hưởng damage/ROF **thật** trong combat. `Weapon.cs:293, 366`
- Loadout tới được gameplay: `Player.prefab:224 useSlotSystem: 1` →
  `PlayerSpawner.cs:51 LoadoutState.ApplyTo(weapon)`

### 2.5 Hợp đồng scene gameplay — verified qua Unity MCP (2026-07-31)

Đọc trực tiếp từ `Map_Level1.unity` đang mở trong Editor (15 root object). **Tất cả wiring đều có
thật**, đúng như handoff cũ mô tả:

| GameObject | Component | Ghi chú |
|---|---|---|
| `RunSystems` | `RunDirector` + `MissionTracker` + `PickupManager` | `RunDirector.campaign` = `CampaignCatalog.asset` ✅ |
| `WaveDirector` | `ZombieSpawner` + `WaveDirector` | |
| `HUD` | `HudController` + `RunOverlays` | Canvas + CanvasScaler + GraphicRaycaster |
| `PlayerSpawner` / `PlayerSpawnPoint` | | player spawn runtime, không bake sẵn |
| `ZombieManager` | | nguồn `AliveCount` |
| `Main Camera` | `CameraFollow` + AudioListener | |
| `NavMesh` | `NavMeshSurface` | |
| `ToonLightRig` | | `Directional Light` **đang tắt** (`activeSelf: false`) — đúng thiết kế toon rig |
| `SpawnPoints` | 12 con | khớp "12/12 spawn path" trong `TASK1_*` |
| `DamageNumber`, `Global Volume`, `Environment`, `EventSystem` | | |

`RunOverlays` cũng đã bind đủ reference trong scene: `levelUpRoot` = `LevelUpOverlay`,
`perkButtons` = 3 nút (`Perk0/1/2`), `gameOverRoot` = `GameOverScreen`, `victoryRoot`,
`reviveRoot`, `settingsRoot`, `ftueRoot`, toàn bộ slider/toggle.

> Nghĩa là các gap G3/G4 **không phải thiếu scene wiring** — presentation đã dựng đủ. Thiếu là ở
> phía **code**: `PickPerk()` không làm gì, và `RunOverlays` không khai field text nào để đổ
> `RunSummary` vào. Sửa chỉ đụng `.cs` + gán thêm reference, không phải dựng lại overlay.

`PickupManager` trong scene: `coinPoolKey=pickup_coin`, `gemPoolKey=pickup_gem`,
`magnetRadius=3.5`, `maxCoinDropsPerKill=4`, `eliteGemChance=0.5`.

### 2.6 Nội dung đã sản xuất xong

- 25 `WeaponData` + 25 `WPN_*` prefab, pose/grip/muzzle authored, icon đầy đủ.
- 448 item Pro Casual + 30 outfit set, icon 448/448.
- 5 map desert đã generate in-place, occlusion baked, ToonLightRig mỗi map.
- Bốn source pack audio mới: **3.145 clip / ~861 MB**. Đây là source library, chưa phải runtime content;
  danh sách curate/wire duy nhất nằm ở `Reference/Audio/AUDIO_SHIP_LIST.md`.

---

## 3. Cái KHÔNG chạy — gap đã verify

Đây là danh sách gap gốc; thứ tự thực thi và acceptance nằm ở `MVP_SHIP_PLAN.md`.

### G1 — Audio: source mới đã import nhưng curated runtime library còn trống

970 clip AI cũ và năm Addressables group cũ đã bị xóa. Catalog giữ nguyên GUID nhưng đã reset về
`ZW_CURATED_PENDING` với `preloadLabels: []` và `variants: []`. Runtime vẫn chỉ có ba hook âm thanh:

- `Weapon.cs:322` (fire) · `Weapon.cs:215` (reload) · `Bomb.cs:71` (explode)

Không nhạc, victory/defeat result music, ambience, tiếng zombie/pickup/UI/footstep/player-hurt.
`ZombieData.cs` chưa có field sfx nào để author. Scope và key authority:
`Reference/Audio/AUDIO_SHIP_LIST.md`.

### G2 — SUPERSEDED 2026-08-08: Campaign selector Phase 1 đã đóng

Compact catalog-driven selector hiện nằm trực tiếp trên Hub PLAY, hỗ trợ completion unlock, persisted
selection, no-wrap navigation và recommended-Power warning. Giữ phần lịch sử dưới đây chỉ để điều tra
baseline 2026-07-31; không dùng nó để mở lại task selector.

`GameFlow.SelectLevel()` (`Flow/GameFlow.cs:31`) **không có caller production**. Cả hai đường
vào trận gọi thẳng `StartGameplay`: `UI/Screens/HubScreen.cs:41`, `Flow/MainMenuController.cs:15`.

Chuỗi hệ quả:

```
SelectedLevel = null
 → PendingGameplayScene = "Map_Level1"          GameFlow.cs:26-29
 → RunState.Begin("")                            GameFlow.cs:111
 → RunDirector.Finish: levelId rỗng → return     RunDirector.cs:61
     ✗ MarkLevelCompleted
     ✗ TryClaimFirstClear      (first-clear reward không bao giờ trả)
     ✗ Fire(RunFinishedEvent)  (dòng 70 nằm SAU return)
 → MissionTracker.OnRunFinished không bao giờ chạy   MissionTracker.cs:56
     ✗ FinishRun / CollectCoin / FinishStage / ClearAllStages đứng yên
```

Thêm: `RunDirector.cs:56` `if (outcome != Victory) return;` → thua cũng không fire
`RunFinishedEvent`. `CampaignCatalog` hiện chỉ có 2 consumer: `RunDirector` và cheat panel.

### G3 — Level-up perk là vỏ rỗng, perk có chọn cũng vô tác dụng

- `UI/RunOverlays.cs:218-223` — `PickPerk()` chỉ đóng overlay, comment ghi thẳng "backend chưa có".
- `RunOverlays.ShowLevelUp()` (`:211`) không có caller; `RunState.AddXp` trả số level lên
  (`RunState.cs:123-137`) nhưng không ai đọc.
- `RunState.Multiplier()` (`RunState.cs:148`) chỉ được gọi trong test. `Weapon.cs:366` tính damage
  thuần qua `WeaponUpgradeMath`, không nhân perk. `PlayerMovement.moveSpeed` là field cứng.
- HUD không có thanh XP/level (`UI/HudController.cs:17-38`).

### G4 — Màn kết quả không hiển thị gì

`RunSummary` snapshot đủ (kills/wave/level/coin/gold/gem/duration — `RunState.cs:165`), nhưng
phần Game Over/Victory của `RunOverlays.cs:47-54` **chỉ có Button, không TMP_Text nào**.

### G5 — Haptics là toggle giả

`RunOverlays.cs:92-93, 105-108` ghi `PlayerPrefs["haptics"]`. Toàn repo không có
`Handheld.Vibrate` hay bất kỳ API rung nào.

### G6 — Ba mission metric chết vì thiếu caller

`MissionTracker.cs:78/81/84` khai `ReportPerkChosen` / `ReportWeaponSwitched` /
`ReportBossDefeated` — không caller nào. `Weapon.SwitchWeapon()` (`:218`) không report;
`ZombieBoss` chết không report.

### G7 — Bomb pickup nhặt xong không có tác dụng

`Pickups/Pickup.cs:120` fire `BombPickedUpEvent`, `:139` khai struct — **không subscriber nào**.
`BombThrower` không cộng charge.

### G8 — Map_Level1..5 là file BINARY (vấn đề về quy trình, KHÔNG phải scene bị hỏng)

5 scene gameplay không phải YAML, dù `ProjectSettings/EditorSettings.asset:7` đặt
`m_SerializationMode: 2` (ForceText) và `.gitattributes` khai `*.unity text merge=unityyamlmerge`.
`Bootstrap.unity` và `Menu.unity` vẫn là text bình thường.

Hệ quả **duy nhất**: 5 scene này không diff/merge/review được bằng git, và không grep được.
`git ls-files --eol` báo `i/-text w/-text` → git tự nhận diện binary nên **chưa** bị mangle EOL.

**Nội dung scene thì hoàn toàn ổn** — đã verify bằng Unity MCP ngày 2026-07-31 (xem §2.5).
Đọc scene phải qua Unity/MCP, đừng kết luận từ việc grep file không ra gì.

### G9 — Rủi ro ship

- `ZW_CHEATS` bật cho cả 3 platform (`ProjectSettings.asset:833-835`) → build release hiện tại
  kèm cheat panel (`Runtime/Dev/ZombieWarCheatPanel.cs:18`, `BillGameCore/Runtime/DevTools/DevTools.cs:1`).
- Bundle id vẫn `com.UnityTechnologies.com.unity.template.urpblank`.
- Không có bất kỳ evidence profiler nào trong repo → claim "mobile-safe" hiện vô căn cứ.

### G10 — Nhỏ

`Weapon.cs:192` deref `Current.range` không null-check → NRE mỗi frame nếu `weapons` rỗng và
chưa equip.

---

## 4. Bản đồ code

```text
Assets/_Project/Scripts/Runtime/
  Flow/          BootstrapEntry · GameFlow · MainMenuController
  Gameplay/      PlayerMovement · PlayerController · Weapon · BombThrower · CameraFollow · Health
    Waves/       WaveDirector · ZombieSpawner · WaveData
    Zombies/     ZombieBase → Walker/Runner/Pouncer/Ranged/Burrower/Boss/Charger
    Pickups/     Pickup · PickupManager · DestructibleProp
    FX/          DamageNumber(+Spawner) · MeshTracer · TracerPool
  Systems/       PlayerProfile · RunState · RunDirector · RunPerkPool · MissionTracker
                 CampaignCatalog · EconomyConfig · GachaService · CombatPower
                 LoadoutState · WeaponUpgradeMath · FxPool · TargetRegistry
  UI/            HudController · RunOverlays · VirtualJoystick
    Core/        UIManager · UIScreen · UITransition · UIFx · UITheme
    Screens/     HubScreen · LoadoutScreen · ShopScreen · CostumeScreen · PassScreen
  Audio/         AddressableAudioCatalog · AddressableAudioRuntime
  Character/     CharacterModularApplier · ModularCostumeCatalog
  World/         ToonLightRig
  Dev/           ZombieWarCheatPanel
```

Editor tooling đáng nhớ (`Assets/_Project/Scripts/Editor/`):
`DesertMapGeneratorWindow` · `CampaignStageBuilder` · `MapNavigationAuthoring` ·
`ZombieVATBaker` · `WeaponPoseAuthoring(Editor)` · `CombatPowerAuditWindow` ·
`CheatBuildToggle` · `DevCheatPanelInstaller` · `UI/M8UiLayout` (layout M8 owner duyệt) · `UI/*Installer`
(⚠️ destructive — chỉ chạy khi owner yêu cầu). `HudInstaller`, `MenuScreensInstaller`, `SceneFlowBuilder`
đã bị xoá (2026-09-26) vì dựng lại layout trước M8.

---

## 5. Dữ liệu

| Loại | Đường dẫn | Số lượng |
|---|---|---:|
| Weapon | `Data/Weapons/WD_*.asset` | 25 |
| Zombie | `Data/Zombies/ZD_*.asset` | 16 |
| Wave | `Data/Waves/WD_Level1..5.asset` | 5 |
| Campaign | `Data/Campaign/CampaignCatalog.asset` | 5 stage |
| Economy | `Data/Economy/EconomyConfig.asset` | 448 item + 2 gacha pool |
| Costume | `Data/Character/*CostumeCatalog.asset` | 448 player-facing |
| Audio source | Bốn pack ở `Assets/` | 3.145 clip / ~861 MB; chưa curate |
| Audio runtime | `Resources/Audio/AddressableAudioCatalog.asset` | `ZW_CURATED_PENDING`; 0 label / 0 variant |

Bảng balance campaign: `Reference/Design/CAMPAIGN_BALANCE_TABLE.md` (Combat Power ceiling 2372, gate
0/700/1100/1500/1900).

---

## 6. Thứ tự đọc cho phiên mới

1. `AGENTS.md`, `CLAUDE.md`
2. `Docs/CURRENT_STATE.md` (file này)
3. `Docs/FRAMEWORK.md` — cách viết code trong project
4. `Docs/Reference/Technical/CORE_WIRING_DIRECTIVE.md` — ràng buộc kiến trúc gốc (mọi thứ đi qua BillGameCore,
   zombie theo thừa kế, player spawn-based, IK). Vẫn có hiệu lực.
5. Doc chuyên đề theo việc đang làm (xem `Docs/README.md`)
6. `Docs/Reference/Technical/PlayerRigSocketIncident.md` nếu đụng Player rig

Rồi kiểm `git status`, Unity state, GitNexus freshness và **source thật** trước khi đề xuất việc.
