# Enemy campaign — trạng thái thực thi

> Cập nhật 2026-07-21. Hợp đồng gốc: `Docs/ENEMY_CAMPAIGN_EXPANSION_PROMPT.md`.
> File này ghi việc **đã làm thật** và **việc còn lại**, để phiên sau tiếp tục không phải audit lại.

## Đã xong

### Phase 0 — audit nguồn (xong)

Bằng chứng lấy trực tiếp từ Unity, không suy từ tên file.

15 quái Cute Series đều hợp lệ để bake VAT:

- đúng **1 SkinnedMeshRenderer** mỗi con;
- 1.052–3.391 đỉnh (vertex);
- material sẵn **URP/Lit**;
- có sẵn clip locomotion **In Place**, nên không cần gỡ root motion;
- tên clip trùng đúng phần sau dấu `@` của tên file, không có tiền tố `root|`.

Hai con rig kiểu **Humanoid** (Skeleton, Skeleton Giant), 13 con còn lại Generic.
Cả hai vẫn bake ra kết quả bình thường.

### Ba lỗi thật trong pipeline VAT cũ (đã sửa)

1. **Clip không bao giờ lặp.** Mọi clip FBX import ra `WrapMode.Default`, mà
   `VAT_Animator` xử lý Default theo kiểu clamp. Baker cũ không set wrap mode, nên idle và
   walk đứng hình ở frame cuối. Giờ set theo vai trò: idle/move/underground = `Loop`,
   attack/hit/death/transition = `ClampForever`.
2. **Enemy không có texture.** `CreateOptimizedMaterial` chỉ `new Material(shader)`, không
   gán `_MainTex`. Mọi bake cũ đều ra enemy trắng trơn. Giờ copy albedo từ material gốc
   của vendor (`_BaseMap` hoặc `_MainTex`), không sửa material vendor.
3. **Collider/agent là số đoán tay.** Giờ đo từ bounds bake thật.

### Phase 2 — bake 15/15 (xong)

Đã kiểm tra có trên đĩa:

- 15 prefab `Assets/_Project/Prefabs/Enemies/ENM_*_VAT.prefab`
- 15 data `Assets/_Project/Data/Zombies/ZD_*.asset`
- 15 thư mục `Assets/_Project/Art/VAT/Enemies/enemy.cute.*/`

Dung lượng: **45,2 MB** texture VAT lúc chạy, 95 MB trên đĩa.

`Docs/ENEMY_ROSTER_AUDIT.md` do chính baker sinh ra mỗi lần chạy, nên không lệch với thực tế.

### ZombieData mở rộng (thuần cộng thêm, không phá cũ)

Thêm: `enemyId` (dạng `enemy.cute.dog_pup`), `archetype`, `isElite`, `attackWindup`,
`specialWindup`, `coinReward`, `xpReward`, `specialClip`, `burrowIn/Loop/OutClip`.

GitNexus impact chạy trên `ZombieVATBaker` và `ZombieData`: cả hai **LOW**, không có caller thật.

### HUGO — chặn có bằng chứng, chủ dự án đã chốt

Mesh `Hugo` có **16.567 đỉnh**. Texture VAT là `Texture2D(vertexCount, totalFrames)`, nên
chiều rộng vượt `SystemInfo.maxTextureSize` (**16.384**) và không tạo được. Mesh còn có 3
submesh với 3 material **Standard** (không phải URP), trong khi VAT chỉ ra được 1 submesh.

Chốt: **không bake HUGO**. Cactus Boss và Skeleton Giant làm boss cuối chiến dịch.
Muốn bake HUGO sau này thì phải giảm mesh (decimate) và chuyển material sang URP.

## Đã xong (đợt 2)

### Hướng và kích thước Visual (xong, 15/15)

Art gốc dựng theo trục Z-up. Mọi `Visual` giờ đặt `rotation = (-90, 0, 0)` và `position = (0,0,0)`.
Vì xoay như vậy nên trục Z của mesh thành trục đứng trong world.

Chiều cao lấy từ **clip idle**, không lấy bounds hợp mọi frame (clip nhảy làm phồng số đo).
Radius cố định 0,35 m theo yêu cầu. Kết quả hợp lý hơn hẳn:

| Quái | Cao cũ (bounds mọi frame) | Cao mới (idle) |
|---|---:|---:|
| Dog Pup | 1,70 | 1,13 |
| Cactus Boss | 6,78 | 3,41 |
| Skeleton Giant | 4,08 | 3,03 |

### Shader enemy mới (xong)

`Assets/_Project/Art/Shaders/VAT_EnemyToon.shader`, tên shader `ZombieWar/VAT/EnemyToon`.
Đặt trong `_Project`, **không sửa file vendor** trong ThirdParty.

Gồm: VAT playback + toon (port từ `VAT_Toonlit`), **hit flash**, và **dissolve thật**.

Phát hiện thêm **lỗi thứ tư**: không shader VAT nào có `_Dissolve`, nên
`ZombieBase.SetDissolve()` từ trước tới nay chạy mà không có tác dụng gì. Giờ đã có thật.

`_HitFlash` và `_Dissolve` đều là per-instance, ghi qua MaterialPropertyBlock, nên một material
dùng chung cho cả đàn vẫn đúng. Đã chứng minh ba giá trị (animTime, flash, dissolve) cùng tồn tại
trên một renderer mà không đè nhau.

Dissolve clip cả ở pass ShadowCaster và DepthOnly, nên xác chết đang tan cũng thôi đổ bóng.

### Lỗi thứ năm — normal không chạy theo animation (xong)

Đây là lỗi nặng nhất về hình ảnh, do chủ dự án chỉ ra.

Baker gốc **chỉ bake position**. Normal vẫn là normal của mesh gốc lúc đứng yên. Nên khi shader
kéo vertex đi theo VAT thì normal đứng im. Với shader Lit thường thì khó thấy, nhưng toon
quantize theo `NdotL` nên ranh giới sáng/tối là một đường sắc nét — nó đứng yên trong khi thân
quái chuyển động qua nó.

Cách sửa: bake thêm **normal texture** theo đúng frame layout của position texture. Không cần
sửa `VAT_AnimationData` (ThirdParty) vì mỗi quái đã có material riêng, gán texture ở mức material
là đủ.

- Normal lưu dạng `RGB24` (3 byte/texel) thay vì `RGBAHalf` (8 byte). Toon chỉ cần độ chính xác
  góc cỡ 8 bit, sai số dưới 1 độ.
- Shader lấy normal theo frame, blend cả lúc crossfade giống hệt position, rồi `normalize` lại.
- Pass ShadowCaster cũng dùng normal động, vì shadow bias đẩy theo normal.
- Có `_UseAnimatedNormals`: material bake kiểu cũ vẫn chạy được, không bị đen.

Đã kiểm chứng: normal texture của cả 15 con **trùng đúng kích thước** với position texture, chứng
minh frame layout khớp.

**Giá phải trả:** bộ nhớ texture tăng từ 45,2 MB lên **62,1 MB** (đúng +3/8 như tính toán).
Nếu sau này cần giảm: bỏ bớt clip không dùng, hoặc hạ tần số bake 30 fps — cả hai đều giảm số
frame nên giảm cả hai texture.

### Lỗi texture Mole Rat (xong)

FBX của Mole Rat không nhúng material nên Unity gán tạm `Lit.mat` mặc định của URP (xám, không
texture). Pack vẫn có `Textures/Mole Rat.psd`. Baker giờ tự tìm texture trong thư mục pack khi
material gốc không có albedo.

### Phase 1 — run loop (xong phần lõi)

- `Assets/_Project/Scripts/Runtime/Systems/RunState.cs` — sổ cái duy nhất của một màn chơi:
  kills, wave, Coin/Gold/Gem, XP, level, perk tạm thời, thời gian, kết quả.
- `Assets/_Project/Scripts/Runtime/Systems/RunPerkPool.cs` — 7 perk, rút 1 trong 3.
- `ZombieKilledEvent` trong `PlayerEvents.cs`.
- `ZombieBase.HandleDeath` ghi kill đúng một lần, có chốt chặn `_state == Dead`.

Hai bảo đảm quan trọng, có test:

- `Payout()` **idempotent**: gọi nhiều lần chỉ cộng tiền một lần.
- `Abandon()` (nút Home) **không** cộng tiền.

Tiền chỉ ghi vào `PlayerProfile` đúng một lần lúc kết thúc, không ghi rải rác mỗi lần nhặt.

## Bằng chứng

- `Assets/Screenshots/EnemyCampaign/VATRoster/VATRoster_ContactSheet.png` — 15 quái đứng đúng
  hướng, đúng texture, không hồng, không vỡ mesh, tỷ lệ to nhỏ hợp lý.
- `Assets/Screenshots/EnemyCampaign/VATRoster/HitFlash_Dissolve_Proof.png` — bốn trạng thái:
  thường, flash 0.5, flash 1.0, dissolve 0.55 (viền cháy cam).
- `Assets/Screenshots/EnemyCampaign/VATRoster/AnimatedNormals_AB.png` — cùng một khung hình đánh,
  cùng ánh sáng, chỉ khác nguồn normal. Trái = normal tĩnh (lỗi cũ), phải = normal động (đã sửa).
  Thấy rõ khác biệt ở sọ, lồng ngực và xương chậu.
- Test: **146/146 EditMode pass** (134 cũ + 12 mới). Console không có lỗi mới.

## Đã xong (đợt 3)

### Shader rút gọn cho nhẹ

Bỏ toàn bộ diffuse toon ramp, rim light, shadow/lit tint và smoothing. Nền giờ là albedo phẳng.
Chỉ giữ **specular có cắt step**: `_SpecSteps` (Range 1..5), đặt **1.5** cho cả 15 material.

Vẫn phải giữ normal texture, vì specular tính theo `N·H` — không có normal động thì đốm sáng
đứng im trên thân đang chuyển động.

### Phase 3 — hành vi riêng cho từng con (xong)

Sáu class, phủ 15 quái bằng kế thừa, không phải 15 subclass:

```
ZombieBase
├── ZombieWalker      Dog Pup, Cat Meow, Skeleton
├── ZombieRunner
│   └── ZombiePouncer Dog Bark, Cat Bolt, Cat Lightning, Dog Bowwow
├── ZombieRanged      Cacti, Cactus, Skeleton Mage
├── ZombieBurrower    Burrow, Mole Rat, Mole Rat King
└── ZombieBoss        Skeleton Giant
    └── ZombieCharger Cactus Boss
```

**ZombieBurrower — ý tưởng "thợ đào đất":** chu kỳ Surface → Diving → Underground → Emerging.
Lúc dưới đất thì bất tử, không bị auto-aim nhắm, ẩn mesh, chạy nhanh gấp 2–3 lần tới gần Player.
Trồi lên luôn cách Player tối thiểu một khoảng và có thời gian báo trước, rồi mới nổ sát thương
vùng. Không bao giờ trồi đúng chỗ Player đứng.

**ZombiePouncer:** Crouch (báo trước, đứng yên) → Leap (khoá hướng, không lái giữa chừng) →
Recover (đứng yên, ăn đòn). Người chơi né rồi phản đòn.

**ZombieCharger:** slam lo tầm gần, charge lo tầm trung, nên không có khoảng an toàn — chỉ có
cửa sổ hồi chiêu sau mỗi chiêu.

Mỗi loài còn có thông số riêng qua `tuning` trong baker, ghi thẳng vào serialized field nên vẫn
chỉnh được trong Inspector. Ví dụ Cat Bolt nhảy nhanh 16 nhưng mỏng manh, Dog Bowwow nhảy chậm 11
nhưng vùng sát thương rộng 2.8.

### Sửa thêm trong ZombieBase

- **Sát thương theo wind-up thật.** Trước đây đánh là trúng ngay khung hình đầu. Giờ chờ
  `attackWindup` đo từ clip thật rồi mới trúng. Đúng một đòn mỗi lần vung.
- Huỷ đòn đang chờ khi chết, khi về pool, khi Player chết. Không còn đòn "ma" trúng sau khi chết.
- Boss slam giờ cũng có wind-up, trước đây trúng tức thì không né được.

### Test

**160/160 EditMode pass** (146 + 14 test roster mới). Có test chặn Animator/SkinnedMeshRenderer,
test loop semantics, test normal map khớp position map, test burrow clip, test wind-up.

Ghi chú asmdef: `_Project.Tests.EditMode.asmdef` (không phải `_Project.Tests.asmdef`) mới là cái
quản thư mục EditMode. Đã thêm reference `VAT.Runtime` vào đó.

## Đã xong (đợt 4)

### Phase 4 — Combat Power và CampaignCatalog

`CombatPower.cs` tính **DPS bền vững**, không lấy `damage` thô. Có tính đạn/phát (shotgun),
tốc độ bắn, băng đạn và thời gian nạp, cộng nhân sao. Không dùng giá tiền hay độ hiếm.

Công thức loadout: súng mạnh nhất tính đủ 100%, hai súng còn lại tính 35% (chỉ bắn được một khẩu
một lúc, nhưng lấp đủ 3 ô vẫn luôn hơn để trống).

`CampaignCatalog.asset` — 5 màn, mỗi màn có ID ổn định, scene, WaveData, cổng lực chiến,
gợi ý vũ khí thật, phần thưởng lần đầu và metadata boss.

`PlayerProfile` thêm (thuần cộng): `completedLevelIds`, `claimedFirstClearIds`,
`lastSelectedLevelId`. Lưu theo **ID chuỗi**, không theo chỉ số, nên đổi thứ tự màn không làm
khoá/mở nhầm.

### Phase 6 — 5 scene thật

`CampaignStageBuilder` copy `Map_Level1` bằng API Unity rồi chỉnh lại, nên giữ nguyên mọi
reference đã nối sẵn (HUD, camera, spawner, EventSystem). **Không đụng `Map_Level1`.**

| Scene | Nền | Màu riêng | Điểm spawn | NavMesh |
|---|---:|---|---:|---|
| Map_Level2 | 75 m | xanh ô-liu | 10 | đã bake |
| Map_Level3 | 80 m | xám xương | 12 | đã bake |
| Map_Level4 | 85 m | xanh lam | 12 | đã bake |
| Map_Level5 | 90 m | đỏ nâu | 14 | đã bake |

**48/48 điểm spawn có đường NavMesh hoàn chỉnh tới Player**, gần nhất cách 31,5 m nên không con
nào hiện ra trong tầm nhìn ban đầu. Build Settings đã đăng ký đủ 5 scene.

### Phase 7 — WaveData 5 màn

Giới thiệu quái tăng dần: màn 1 có 4 loại mới, màn 2 thêm 4, màn 3 thêm 2, màn 4 thêm 5,
màn 5 không thêm gì (chỉ trộn lại). **Dùng đủ 15/15 quái.** Mỗi màn 5–7 đợt.

### Phase 9 — cửa sổ audit lực chiến

`Tools/ZombieWar/Combat Power Audit`, chỉ đọc.

**Nó bắt được lỗi thật ngay lần chạy đầu:** cổng màn 4 (3200) và màn 5 (4800) **không thể đạt
được**, vì trần lực chiến của cả kho 25 súng ở 3 sao chỉ là **2372**. Đây đúng là lỗi tiến trình
vòng lặp mà đề bài cảnh báo. Đã chỉnh lại theo số đo thật:

| Màn | Min cũ | Min mới | % của trần |
|---|---:|---:|---:|
| 1 | 0 | 0 | 0% |
| 2 | 900 | 700 | 30% |
| 3 | 2000 | 1100 | 46% |
| 4 | 3200 | 1500 | 63% |
| 5 | 4800 | 1900 | 80% |

### Phase 10 — Battle Pass thật

`PassMissions.cs`: **20 nhiệm vụ** (12 ngày + 8 tuần), ID ổn định, target/thưởng dạng dữ liệu.

Xoay vòng **tất định theo ngày UTC**, không random: cùng một ngày luôn ra cùng bộ nhiệm vụ, nên
cài lại máy hay hai thiết bị đều khớp, và test kiểm được mà không cần giả lập đồng hồ.

`MissionTracker.cs` chạy theo **event có kiểu**, không poll mỗi frame. Một sự kiện tăng tiến độ
mỗi nhiệm vụ liên quan đúng một lần.

Reset ngày và tuần **độc lập**: sang ngày mới không xoá tiến độ tuần.

### GameFlow theo màn đã chọn

Bỏ hardcode `Map_Level1`. Có `SelectedLevel` và `ActiveGameplayScene`. Restart nạp lại **đúng màn
đang chơi**. Đổi màn giữa chừng sẽ unload màn cũ trước, không bao giờ để hai map cùng nằm trong
bộ nhớ. Home gọi `RunState.Abandon()` nên không cộng tiền.

### RunDirector

Nối event có sẵn vào sổ cái, không phải sửa `WaveDirector`/`PlayerController`. Victory/Defeat đều
đi qua một chỗ, có chốt chặn, `Payout` idempotent, `MarkLevelCompleted` và `TryClaimFirstClear`
cũng idempotent.

### Test đợt 4

**188/188 EditMode pass** (173 + 15 test Pass). Trong đó có test cổng màn, test phần thưởng lần
đầu chỉ trả một lần, test reset ngày/tuần, test claim không nhân đôi, test lưu và nạp lại.

Sinh `Docs/CAMPAIGN_BALANCE_TABLE.md` từ asset thật.

## Việc còn lại

Ưu tiên theo thứ tự này.

### Phase 1 phần còn lại

Lõi sổ cái xong. Còn phải nối vào game thật:

- HUD hiện Coin/XP thật của màn đang chơi;
- overlay level-up hiện 3 perk thật (`RunOverlays.PickPerk` vẫn là placeholder);
- perk nhân với sao vũ khí trong `Weapon`/`WeaponUpgradeMath`;
- màn hình kết quả Defeat/Victory đọc `RunSummary`;
- `WaveDirector` gọi `SetWave`, `AllWavesClearedEvent` gọi `Finish(Victory)`,
  `GameOverEvent` gọi `Finish(Defeat)`;
- Replay/Home gọi `Begin`/`Abandon` cho đúng.

### Các phase chưa động tới

- **Phase 5 — `UI_CampaignScreen.prefab`.** Backend đã xong hết (`CampaignCatalog`,
  `CombatPower`, `LevelGate`, `GameFlow.SelectLevel`). Còn phải dựng prefab UI: 5 chấm tròn,
  mũi tên trái/phải, nút Back, số + tên màn, trạng thái khoá/mở/đã qua, lực chiến min/rec so với
  lực chiến hiện tại, gợi ý vũ khí kèm icon, nút CHƠI, và lối tắt sang Loadout/Shop khi yếu.
  Rồi nối Hub PLAY vào màn này thay vì vào thẳng gameplay.
- **Phase 8 — pickup KayKit.** Sổ cái đã sẵn sàng (`RunState.AddCurrency`). Còn phần hình:
  coin/gold pooled, magnet, tự nhặt khi hết đợt.
- **Phase 11 — bằng chứng runtime.** Mới có bằng chứng edit-time (ảnh, test, đo NavMesh).
  Chưa vào Play Mode chạy thật từng màn, chưa đo profiler ở mức 25/50/100 quái.

### Nối nốt run loop vào UI

- HUD hiện Coin/XP thật của màn đang chơi;
- overlay level-up hiện 3 perk thật (`RunOverlays.PickPerk` vẫn là placeholder);
- perk nhân với sao vũ khí trong `Weapon`;
- màn hình kết quả đọc `RunFinishedEvent.Summary`;
- đặt `RunDirector` + `MissionTracker` vào 5 scene gameplay.

## Đã xong (đợt 5)

### Bật WaveDirector

`WaveDirector` trong Map_Level2..5 đang `activeSelf=False` (thừa hưởng lúc copy scene). Đã bật
lại cả GameObject lẫn component ở cả 5 màn, kèm ZombieManager, RunSystems, SpawnPoints.

### Dissolve dùng noise texture

Shader thêm `_DissolveNoiseTex` + toggle `_UseNoiseTex`. Không có texture thì tự quay về noise
thủ tục, nên material bake kiểu cũ vẫn chạy chứ không bị pop.

Sinh sẵn `Assets/_Project/Art/Textures/T_DissolveNoise.png` — Perlin 3 octave **có tiling**
(quan trọng: UV bị nhân 14 lần nên texture không tile sẽ lộ đường nối giữa lúc cháy).

### VatLookConfig — một bộ thông số cho toàn game

`Assets/_Project/Data/Art/VatLookConfig.asset`. Chứa specular steps/size/intensity, noise texture,
noise scale, màu và bề rộng viền cháy, màu hit flash.

Mỗi quái buộc phải có material riêng (position/normal map khác nhau), nên nếu không có config thì
"chỉnh specular gắt hơn" là 15 lần sửa rồi lệch nhau. Giờ sửa một chỗ, bấm **Apply**, cả 15 nhận.

Áp bằng `Tools/ZombieWar/Apply VAT Look`, hoặc ngay trong cửa sổ Dissolve Test. Baker cũng tự áp
sau mỗi lần bake nên không bị lệch trở lại.

### Blob shadow dùng chung

Mỗi quái có `ShadowBlob` — quad phẳng ở `(0, 0.02, 0)`, xoay 90°, kích thước theo radius đo được.

**Cả 15 con dùng chung đúng một material** `M_BlobShadow` (URP Unlit, transparent, ZWrite off,
GPU instancing bật). Texture blob radial cũng sinh sẵn.

Bóng tắt/bật cùng thân qua `ZombieBase.SetVisible()`, nên lúc chui xuống đất hoặc ở tier Inactive
không còn vệt bóng lơ lửng trên nền.

### Test

**190/190 pass.** Thêm test: cả 15 con dùng chung một material blob, cùng vị trí, blob không tự
đổ bóng; và test toàn bộ material khớp với VatLookConfig (kể cả toggle noise khớp với texture).

## Đã xong (đợt 6) — sửa 5 bug của công cụ chỉnh look

| # | Bug | Nguyên nhân | Cách sửa |
|---|---|---|---|
| 1 | Chỉnh config không thấy đổi realtime | `DrawSharedLook` chỉ bật cờ `_configDirty`, không ghi vào material | Gọi `Apply(cfg, save:false)` ngay khi giá trị đổi |
| 2 | Thay noise texture không vào material liền | Cùng gốc #1 | Như trên |
| 3 | Noise scale chỉ 1 chiều, UV quái bị scale nên noise méo | `_DissolveNoiseScale` là `float` | Đổi thành `_DissolveNoiseTiling` (Vector, dùng X/Y riêng) |
| 4 | Dissolve và hit flash dùng chung 1 thanh duration | Window chỉ có một biến `_duration` | Hai thanh riêng, đọc thẳng từ config |
| 5 | Apply không đụng tới duration | Duration nằm trong `ZombieBase` prefab, config không quản | `ApplyTimings()` ghi vào cả 15 prefab |

Chi tiết đáng lưu ý:

- **Live preview không lưu đĩa.** `Apply(cfg, save:false)` chỉ ghi property vào material để Scene
  view đổi ngay trong frame đó. Nếu `SaveAssets()` mỗi lần kéo slider thì editor sẽ khựng. Bấm
  **Save + apply** mới ghi xuống đĩa, và duration chỉ tới prefab ở bước Save này.
- **`returnToPoolDelay` tự nâng theo `dissolveDuration`.** Nếu đặt dissolve dài hơn delay thì xác
  bị thu về pool giữa lúc đang tan. Applier tự đẩy delay lên `dissolve + 0.8s`, và có test chặn.

Bằng chứng: `Assets/Screenshots/EnemyCampaign/VATRoster/Dissolve_TilingXY.png` — cùng dissolve
0.45, bốn tiling `14x14 / 40x6 / 6x40 / 24x10` cho bốn pattern khác hẳn nhau (đốm đều, sọc dọc
mảnh, sọc ngang, hỗn hợp). Chứng minh X và Y độc lập.

**191/191 test pass.** Thêm test duration khớp config và test pool delay không cắt ngang dissolve.

## Đã xong (đợt 7) — map generator hoàn chỉnh góc + chống chồng lấn

### Góc vòng đá — sửa đúng tận gốc

Sai lầm cũ: cho cánh chữ L chĩa **ra ngoài**. Đúng phải là cánh chạy **ngược vào trong, dọc theo
hai bức tường** nó nối. Căn theo **mặt ngoài**: `pivot corner = wallEdge − 0.01` (mặt ngoài corner
2.80 khớp mặt ngoài tường 2.79). Yaw: `(+,+)→270, (−,+)→180, (−,−)→90, (+,−)→0`.

Sân 100 m kiểm bằng số: 4 corner tại `±52.49` đối xứng, 76 tường (19/cạnh), vòng khép kín
(ảnh `GenMap_BoundaryOnly.png`).

### "Dư 1 cục tường" — nguyên nhân và fix

Hai nguồn:
1. Slider size là số liên tục — size không chia hết 5 làm `ceil()` dư một mảnh tường đè lên cánh
   corner. Fix: **snap size về bội số 5** + `RoundToInt`.
2. Tường phải kết thúc đúng chỗ cánh corner bắt đầu (`cornerEdge − 5`).

### Chồng lấn barrel/prop — fix kiến trúc

Nguyên nhân: `ScatterProps` và `ScatterInteractive` giữ **hai danh sách chiếm chỗ riêng**, thùng
không biết đá nằm đâu.

Fix: class `Occupancy` — một nguồn sự thật duy nhất cho "chỗ này trống không", mọi pass
`Reserve`/`IsFree` qua nó, mỗi cặp phải thoả khoảng cách **lớn hơn** của hai bên.

Hai bug phát sinh được bắt ngay trong lúc làm:
- Reserve tâm cụm barrel **trước** khi đặt barrel → mọi barrel fail check của chính cụm mình.
  Đổi thứ tự: reserve tâm sau khi đặt xong.
- Đặt đá trước thùng → 484 hòn đá chiếm hết chỗ trống 6 m, **0 barrel** đặt được (18/18 chỉ toàn
  crate). Đổi thứ tự: **đồ gameplay đặt trước, đá trang trí lấp quanh sau** → 32 crates+barrels.

Kiểm bằng số trên sân 100 m, 481 vật: khoảng cách nhỏ nhất barrel↔vật khác **1,93 m**, cặp gần
nhất toàn map 1,02 m (hai viên sỏi 0,2 m — không giao nhau). 12/12 spawn có đường đi.

**191/191 test pass.**

## Bằng chứng (đợt 3–4)

- `Assets/Screenshots/EnemyCampaign/VATRoster/VATRoster_ContactSheet.png` — 15 quái sau khi bỏ
  diffuse, chỉ còn albedo phẳng + specular cắt step.
- `Assets/Screenshots/EnemyCampaign/VATRoster/AnimatedNormals_AB.png` — chứng minh normal động.
- `Assets/Screenshots/EnemyCampaign/VATRoster/HitFlash_Dissolve_Proof.png`
- `Assets/Screenshots/EnemyCampaign/Stages/Map_Level*_TopDown.png` — 5 sân, mỗi sân một màu.
  Khung màu ở rìa ảnh là Canvas HUD (ScreenSpaceOverlay) vẽ đè lên, **không phải lỗi scene**;
  đã kiểm: `Ground` đúng 80 m với material riêng.
- `Docs/ENEMY_ROSTER_AUDIT.md` và `Docs/CAMPAIGN_BALANCE_TABLE.md` đều sinh từ asset thật.

## Ghi chú an toàn

Chưa stage, chưa commit, chưa push. Không đụng asset vendor. Worktree bẩn cũ giữ nguyên.
