# Zombie War — Audio Ship List

**Authority:** danh sách quản lý audio duy nhất của project  
**Phase:** SHIP — M3 Vertical Slice Feel  
**Cập nhật:** 2026-08-07

File này thay thế toàn bộ manifest/plan audio cũ. Con số 970 clip AI cũ và 330 cue cũ không còn giá trị: chúng đã bị xóa khỏi project và khỏi Addressables.

## Current implementation — 2026-08-08

Curated runtime audio is now built and wired. These two generated CSV files are the source of truth for asset-level tracking:

- `AUDIO_SOURCE_INVENTORY.csv`: all 3,256 discovered source clips, including whether each clip is Addressable or source-only.
- `AUDIO_RUNTIME_MAPPING.csv`: all 666 runtime-key variants with exact asset path, Addressables label, import profile, and gameplay use case.

Current build result:

| Metric | Result |
|---|---:|
| Source clips audited | 3,256 |
| Clips included in runtime Addressables | 269 |
| Source-only clips excluded from player | 2,987 |
| Runtime mapping rows | 666 |
| Unique runtime keys | 361 |
| Missing mapped files | 0 |
| Raw size before Unity compression | 172.8 MB |

The 3,011-clip footsteps pack is intentionally not shipped whole. Only 24 selected earth/concrete/metal footsteps are Addressable; the remaining takes stay available as source. Music uses Streaming Vorbis, creature/world sounds use compressed-in-memory Vorbis, and latency-critical weapon/impact sounds use ADPCM. `QA` remains the honest status until the mix is heard in an Android horde build.

## 1. Ship target

Audio MVP phải làm Stage 1 thành một vertical slice hoàn chỉnh:

`Hub → Start Run → Gun/Hits/Horde → Wave beats → Victory hoặc Defeat → Result → Hub`

Hướng âm thanh: **cute chaotic zombie arcade** — punchy, vui, nhanh, hơi toybox; không horror nặng, không cinematic nghiêm túc.

### Trạng thái

| Ký hiệu | Nghĩa |
|---|---|
| `SOURCE` | Có candidate trong source pack, chưa nghe/chọn |
| `GENERATE` | Cần tạo mới |
| `CURATE` | Cần chọn, trim, normalize và đưa vào `Curated/` |
| `WIRE` | Có file nhưng chưa nối runtime |
| `QA` | Đã nối; cần nghe trong build mobile |
| `DONE` | Đã pass mobile mix |
| `LATER` | Không thuộc Stage 1 MVP |

Không đánh dấu `DONE` chỉ vì file tồn tại. `DONE` nghĩa là đã nghe trong horde thật trên điện thoại.

## 2. Source inventory

| Pack | Clip | Dung lượng | Dùng cho | Trạng thái |
|---|---:|---:|---|---|
| `Assets/FreeWeaponSounds` | 40 | 11.2 MB | Handgun, assault rifle, shotgun, grenade launcher | `SOURCE` |
| `Assets/ZombieHorrorPackageFree` | 44 | 13.5 MB | Flesh hit, bite, body fall, generic zombie vocal | `SOURCE` |
| `Assets/Deadly Kombat Free version` | 50 | 24.6 MB | Body hit, bone break, gore, whoosh | `SOURCE` |
| `Assets/Footsteps Pack Expanded` | 3,011 | 811.7 MB | Concrete, earth, metal và các surface khác | `SOURCE` |

Không đưa nguyên source pack vào AudioLibrary hoặc Addressables. Chỉ copy các take được duyệt sang:

```text
Assets/_Project/Audio/Curated/
├── Music/
├── Stingers/
├── Weapons/
├── Impacts/
├── Creatures/
├── Player/
├── Pickups/
├── World/
├── UI/
└── Ambience/
```

## 3. Master MVP checklist — Stage 1

### 3.1 Music và kết quả run

| P | Runtime key | Asset đề xuất | Trigger | Deliverable | Hiện trạng |
|---:|---|---|---|---|---|
| P0 | `music.hub` | `music_hub_arcade_01.wav` | Vào Hub/Menu | 1 seamless loop 45–75s | `GENERATE` |
| P0 | `music.run.stage1` | `music_run_arcade_01.wav` | GameplayState bắt đầu | 1 seamless loop 45–75s | `GENERATE` |
| P0 | `stinger.victory` | `stinger_victory_01.wav` | `RunFinishedEvent(Victory)` | 2 variant, 2–4s | `GENERATE` |
| P0 | `music.result.victory` | `music_result_victory_01.wav` | Sau victory stinger | 1 seamless loop 24–40s | `GENERATE` |
| P0 | `stinger.defeat` | `stinger_defeat_01.wav` | `RunFinishedEvent(Defeat)` | 2 variant, 2–4s | `GENERATE` |
| P0 | `music.result.defeat` | `music_result_defeat_01.wav` | Sau defeat stinger | 1 seamless loop 24–40s | `GENERATE` |
| P0 | `stinger.run.start` | `stinger_run_start_01.wav` | Player xuất hiện/run bắt đầu | 1 variant, 1–2s | `GENERATE` |
| P0 | `stinger.wave.start` | `stinger_wave_start_01..02.wav` | `WaveStartedEvent` | 2 variant, 0.8–1.5s | `GENERATE` |
| P0 | `stinger.wave.clear` | `stinger_wave_clear_01..02.wav` | `WaveClearedEvent` | 2 variant, 1–2s | `GENERATE` |
| P0 | `stinger.level_up` | `stinger_level_up_01..02.wav` | Perk selection mở | 2 variant, 1–2s | `GENERATE` |

Playback contract:

- Hub: `Bill.Audio.PlayMusic("music.hub", 0.5f)`.
- Start run: fade Hub 0.35–0.5s, phát `stinger.run.start`, rồi chạy `music.run.stage1`.
- Victory/Defeat: fade run music 0.2–0.35s, phát stinger đúng outcome, sau khoảng 0.8–1.2s chạy result loop tương ứng.
- Retry: dừng result music và chạy lại run music.
- Home: crossfade về `music.hub`.
- Wave/level-up stinger là SFX overlay; không restart BGM.

### 3.2 Weapons

Source chính: `Assets/FreeWeaponSounds`.

| P | Runtime key | Nội dung | Variant ship | Hiện trạng |
|---:|---|---|---:|---|
| P0 | `sfx.weapon.handgun.fire` | Handgun dry gunshot | 3 | `SOURCE → CURATE` |
| P0 | `sfx.weapon.handgun.reload` | Mag out/in + slide | 1 sequence hoặc 3 animation cues | `SOURCE → CURATE` |
| P0 | `sfx.weapon.rifle.fire` | Assault rifle dry gunshot | 3 | `SOURCE → CURATE` |
| P0 | `sfx.weapon.rifle.reload` | Mag out/in + bolt | 1 sequence hoặc 3 animation cues | `SOURCE → CURATE` |
| P0 | `sfx.weapon.shotgun.fire` | Shotgun dry gunshot | 3 | `SOURCE → CURATE` |
| P0 | `sfx.weapon.shotgun.reload` | Chamber/pump | 2–3 | `SOURCE → CURATE` |
| P1 | `sfx.weapon.grenade.fire` | Grenade launcher launch | 1–2 | `SOURCE → CURATE` |
| P1 | `sfx.weapon.grenade.reload` | Breech/casing/insert/close | 4 animation cues | `SOURCE → CURATE` |
| P1 | `sfx.weapon.tail.indoor/outdoor` | Environment tail layer | 2 | `SOURCE → CURATE` |

25 `WeaponData` có thể giữ key riêng `sfx.weapon.<weaponId>.fire/reload`, nhưng trong MVP nhiều key được phép resolve về cùng family pool. Không cần tạo 25 bộ sound riêng trước soft launch.

### 3.3 Bullet impacts

| P | Runtime key | Đối tượng | Variant | Source | Hiện trạng |
|---:|---|---|---:|---|---|
| P0 | `sfx.impact.fur.light` | Dog/Cat/Mole hit thường | 5–6 | Deadly Kombat body hit | `SOURCE → CURATE` |
| P0 | `sfx.impact.fur.heavy` | Shotgun/sniper vào fur | 3–4 | Deadly Kombat finisher | `SOURCE → CURATE` |
| P0 | `sfx.impact.bone.light` | Skeleton hit thường | 5–6 | Kombat block/bone nhẹ hoặc generate | `SOURCE → CURATE` |
| P0 | `sfx.impact.bone.heavy` | Skeleton crit/death | 2 | `bone_breaking_*` | `SOURCE → CURATE` |
| P1 | `sfx.impact.plant.light` | Cacti/Cactus | 5–6 | Generate/select | `GENERATE` |
| P1 | `sfx.impact.plant.heavy` | Cactus crit/boss | 3 | Generate/select | `GENERATE` |
| P1 | `sfx.impact.flesh.light` | Generic zombie/flesh | 5 | ZombieHorror Impact | `SOURCE → CURATE` |
| P1 | `sfx.impact.flesh.heavy` | Heavy flesh/gore | 3 | ZombieHorror + Kombat gore | `SOURCE → CURATE` |
| P1 | `sfx.impact.sand` | Đạn trúng đất/cát | 4 | Generate/source later | `CURATE` |
| P1 | `sfx.impact.rock` | Đạn trúng đá | 4 | Generate/source later | `CURATE` |
| P1 | `sfx.impact.wood` | Crate/gỗ | 4 | Generate/source later | `CURATE` |
| P1 | `sfx.impact.metal` | Barrel/kim loại | 4 | Kombat block/metal | `SOURCE → CURATE` |

Một hit tối đa hai layer: physical impact luôn có + creature hurt vocal có xác suất. Không bake impact và vocal chung một clip.

### 3.4 Stage 1 creature families

| Enemy | Family key | Attack | Hurt | Death | Hiện trạng |
|---|---|---:|---:|---:|---|
| Dog Pup | `sfx.creature.furry_small.*` | 3 | 6 | 4 | `GENERATE` |
| Cat Meow | `sfx.creature.furry_small.*` | 3 | 6 | 4 | Dùng chung family |
| Dog Bark | `sfx.creature.furry_heavy.*` | 3 | 5 | 3 | `GENERATE` |
| Skeleton | `sfx.creature.skeleton_small.*` | 3 | 6 | 4 | `GENERATE/SOURCE` |

Suffix chuẩn:

```text
.attack
.hurt
.death
.special
```

Idle vocal bị cắt khỏi Stage 1 MVP. Đám đông đã đủ ồn; chỉ thêm nếu playtest chứng minh battlefield quá im.

### 3.5 Player, bomb và pickup

| P | Runtime key | Variant | Source | Hiện trạng |
|---:|---|---:|---|---|
| P0 | `sfx.player.hurt` | 4 | Generate/source | `CURATE` |
| P0 | `sfx.player.death` | 2 | Generate/source | `CURATE` |
| P0 | `sfx.player.heal` | 2–3 | Generate/source | `CURATE` |
| P1 | `sfx.player.low_health.loop` | 1 loop | Generate | `GENERATE` |
| P0 | `sfx.bomb.throw` | 2–3 | Kombat whoosh | `SOURCE → CURATE` |
| P0 | `sfx.bomb.bounce` | 3 | Generate/source | `CURATE` |
| P0 | `sfx.bomb.explode` | 2–3 | FreeWeapon GL explosion | hook tồn tại, content cần curate |
| P0 | `sfx.pickup.coin` | 3–4 | Generate/source | `CURATE` |
| P0 | `sfx.pickup.gem` | 3 | Generate/source | `CURATE` |
| P0 | `sfx.pickup.health` | 2–3 | Generate/source | `CURATE` |
| P0 | `sfx.pickup.bomb` | 2–3 | Generate/source | `CURATE` |

### 3.6 World và combat beats

| P | Runtime key | Variant | Trigger | Hiện trạng |
|---:|---|---:|---|---|
| P0 | `sfx.prop.crate.hit` | 3–4 | Crate nhận damage | `CURATE` |
| P0 | `sfx.prop.crate.break` | 3 | Crate vỡ | `CURATE` |
| P0 | `sfx.prop.barrel.hit` | 3–4 | Barrel nhận damage | `CURATE` |
| P0 | `sfx.prop.barrel.warning` | 1–2 | Trước nổ | `GENERATE` |
| P0 | `sfx.prop.barrel.explode` | 2–3 | Barrel nổ | `SOURCE → CURATE` |
| P1 | `sfx.horde.movement_bed` | 2 | Horde đông ngoài camera | `GENERATE` |
| P1 | `stinger.elite.spawn` | 2 | Elite xuất hiện | `GENERATE` |
| P1 | `stinger.boss.warning` | 1 | Trước boss | `GENERATE` |
| P1 | `stinger.boss.spawn` | 1–2 | Boss spawn | `GENERATE` |

### 3.7 Player footsteps và ambience

| P | Runtime key | Variant | Source | Hiện trạng |
|---:|---|---:|---|---|
| P1 | `sfx.footstep.player.earth` | 6–8 | Footsteps/SingleSteps/Earthground | `SOURCE → CURATE` |
| P1 | `sfx.footstep.player.concrete` | 6–8 | Footsteps/SingleSteps/Concrete | `SOURCE → CURATE` |
| P1 | `sfx.footstep.player.metal` | 6–8 | Footsteps/SingleSteps/Metal | `SOURCE → CURATE` |
| P1 | `amb.stage1.base` | 1 loop 45–75s | Generate/source | `GENERATE` |
| P1 | `amb.stage1.detail` | 3–4 one-shot | Wind/dust/distant debris | `GENERATE` |

Không wire cả 3,011 footsteps. Enemy footsteps riêng bị cắt khỏi MVP; nếu cần, dùng một horde movement bed nhẹ.

### 3.8 UI happy path

Chỉ wire sau khi owner cho phép đụng UI code/reference.

| P | Runtime key | Dùng cho | Variant | Hiện trạng |
|---:|---|---|---:|---|
| P1 | `ui.confirm` | Play, claim, purchase, equip | 2–3 | `CURATE` |
| P1 | `ui.secondary` | Tab/card/selection | 2–3 | `CURATE` |
| P1 | `ui.back` | Back/close | 2 | `CURATE` |
| P1 | `ui.denied` | Locked/invalid/insufficient | 2 | `CURATE` |
| P1 | `ui.reward` | Reward/mission complete | 2–3 | `CURATE` |
| P1 | `ui.purchase` | Purchase/upgrade success | 2 | `CURATE` |
| P1 | `ui.weapon.equip` | Equip/switch weapon | 2–3 | `SOURCE → CURATE` |

Gacha, battle pass premium và toàn bộ UI breadth ngoài first-session path là `LATER`.

## 4. Later roster mapping — không generate trước Stage 1 mix pass

| Family | Enemy | Required set |
|---|---|---|
| `furry_fast` | Cat Bolt, Cat Lightning | attack, hurt, death, pounce |
| `furry_heavy` | Dog Bowwow | attack, hurt, death, pounce/slam |
| `plant_small` | Cacti | attack, hurt, death, projectile |
| `plant_heavy` | Cactus | attack, hurt, death, projectile/slash |
| `skeleton_mage` | Skeleton Mage | hurt, death, cast, projectile |
| `burrow_small` | Burrow | hurt, death, dig, emerge |
| `burrow_heavy` | Mole Rat | attack, hurt, death, dig, emerge |
| `bone_boss` | Skeleton Giant | hurt, death, slam telegraph/impact |
| `burrow_boss` | Mole Rat King | hurt, death, boss intro, dig/emerge |
| `plant_boss` | Cactus Boss | hurt, death, boss intro, charge/slam |

Stage 2–3 là P1 sau Stage 1. Stage 4–5 là `LATER` cho tới khi launch cohort cần chúng.

## 5. Voice budget và chống “vỡ chợ”

`Bill.Audio` hiện có 16 SFX sources tổng và chưa có limiter theo category. Trước khi wire horde audio phải thêm concurrency gate; không gọi hurt vocal cho mọi damage event.

| Category | Max voices | Cooldown/rule | Priority |
|---|---:|---|---:|
| Player danger / boss warning | 2 | Không random skip | 100 |
| Player gun | 4 | Mỗi phát súng được nghe; steal tail trước | 90 |
| Explosion | 2 | 80–120ms global | 85 |
| Nearby physical impact | 4 | 30–50ms global/material | 75 |
| Creature attack vocal | 2 | 1.5–2s mỗi enemy | 70 |
| Creature death | 3 | Boss luôn phát; mob có thể sample | 65 |
| Creature hurt vocal | 3 | 25–35% hit; 0.4–0.6s mỗi enemy | 55 |
| Pickup/UI | 3 | Không spam cùng key dưới 60ms | 50 |
| Footstep | 4 | Chỉ nguồn gần nhất | 30 |
| Idle/horde detail | 1–2 | Cắt đầu tiên khi đông | 10 |

Quy tắc bắt buộc:

- Audio creature là 3D positional; UI/music/stinger kết quả là 2D.
- Boss/telegraph không bị mob vocal steal.
- Cùng key có nhiều variant phải random không lặp liên tiếp.
- Pitch variation: impact ±3–5%, creature ±4–7%, music/stinger quan trọng = 0.
- BGM bắt đầu khoảng 25–35% volume; creature/impact thấp hơn gun 3–8 dB.
- Test cả loa điện thoại và tai nghe; không mix chỉ bằng loa PC.

## 6. Import settings

| Loại | Kênh | Load type | Compression | Loop |
|---|---|---|---|---|
| Music | Stereo | Streaming | Vorbis, quality khoảng 70 | On |
| Ambience loop | Stereo | Streaming | Vorbis 60–70 | On |
| Short 3D SFX | Mono | Compressed In Memory | Vorbis/ADPCM tùy artifact | Off |
| UI/stinger | Mono hoặc Stereo | Compressed In Memory | Vorbis | Off |

- Trim silence ở đầu clip.
- Music loop phải cut theo bar; không dùng nguyên phần intro/outro từ Suno.
- Source master ưu tiên WAV. Runtime compression do Unity đảm nhiệm.
- Deadly Kombat có một số IEEE-float WAV; phải xác nhận Unity import sạch trước khi chọn.
- Lưu provenance/license của mọi pack và generation trước publish.

## 7. Music generation briefs

### Gameplay loop material

```text
VIDEO GAME LOOP MATERIAL — NOT A SONG. Instrumental continuous gameplay underscore at exactly 128 BPM, 4/4. Start immediately inside an already-running groove. Long cyclic arcade combat section with constant energy: playful colorful zombie-horde action, compact electronic drums, funky bass, minimal chiptune ostinato, short marimba accents and tiny cartoon brass punctuations. No intro, verse, chorus, build, breakdown, drop, outro, fade, final cadence, resolved final chord or final drum fill. Final eight bars must sound like ordinary middle bars. Maintain the same rhythm and density through the final sample. No vocals, lyrics, horror drones, cinematic trailer sounds, emotional climax or dominant lead melody.
```

Generate 2 phút, bỏ đầu/cuối, cắt đúng 32 bars giữa. 128 BPM/4-4 → 32 bars = 60 giây.

### Victory result

```text
Playful arcade victory result music for a colorful zombie-horde game, exactly 120 BPM, bright confident synth-funk, compact drums, marimba sparkle, short brass celebration, earned and satisfying without sounding epic or cinematic. Create loopable middle-section material after a brief celebratory opening. No vocals, no long fanfare, no emotional ending, no fade-out. The loop section must maintain stable instrumentation and avoid a final cadence.
```

Tách opening 2–4s thành `stinger.victory`; cắt đoạn giữa thành `music.result.victory`.

### Defeat result

```text
Playful arcade defeat result music for a colorful zombie-horde game, exactly 112 BPM, cheeky minor-key synth groove, soft comedic bass fall, light percussion, “you got flattened — try again” mood, encouraging rather than sad or frightening. Create a short readable defeat opening followed by a low-density loopable groove. No tragedy, no horror, no cinematic drama, no vocals, no fade-out, no final cadence.
```

Tách opening 2–4s thành `stinger.defeat`; cắt đoạn giữa thành `music.result.defeat`.

## 8. Execution order

1. Generate/chọn `music.hub`, `music.run.stage1`, victory và defeat packages.
2. Curate handgun/rifle/shotgun fire + reload.
3. Curate fur/bone impacts.
4. Generate Stage 1 furry/skeleton attack-hurt-death families.
5. Implement audio concurrency gate.
6. Wire gameplay events, result transitions và AudioLibrary.
7. Add pickup/prop/player feedback.
8. Owner duyệt UI audio wiring.
9. Mobile horde mix QA.
10. Chỉ sau Stage 1 pass mới mở Stage 2–3 audio.

## 9. Stage 1 Definition of Done

- Hub, run, victory result và defeat result đều có music đúng ngữ cảnh.
- Music loop không có click, cadence kết bài hoặc cảm giác bài bị restart.
- Handgun/rifle/shotgun không dùng một gunshot duy nhất.
- Fur và bone impact đọc được bằng tai.
- Dog/Cat/Skeleton có attack, hurt và death identity tối thiểu.
- 60 enemy không tạo scream wall; gun và cảnh báo nguy hiểm vẫn rõ.
- Victory và defeat phát đúng một stinger, không double-trigger.
- Settings Music/SFX vẫn hoạt động và persist sau restart.
- Không đưa source pack 861 MB vào player build.
- Pass 15 phút trên điện thoại, không clipping, không voice storm, không missing key spam.
