# Zombie War — Full audio asset manifest

**Trạng thái toàn bộ:** PROPOSED — chưa generate  
`Var` là số clip khác nhau dự kiến ship, không phải số candidate cần generate.  
`MUST` cần cho bản audio đầu; `SHOULD` tăng chất lượng; `LATER` dành cho feature chưa tồn tại.

## A. UI và meta

| ID / key gốc | Cue | Var | Ưu tiên |
|---|---|---:|---|
| `ui.tap.primary` | Nút chính: Play, mua, claim, xác nhận | 3 | MUST |
| `ui.tap.secondary` | Nút phụ, card, tab | 3 | MUST |
| `ui.back` | Back/close modal | 2 | MUST |
| `ui.open` | Mở screen/modal | 2 | MUST |
| `ui.close` | Đóng screen/modal | 2 | MUST |
| `ui.denied` | Nút khoá, thao tác không hợp lệ | 2 | MUST |
| `ui.insufficient_currency` | Không đủ tiền | 2 | MUST |
| `ui.toggle.on` | Toggle bật | 2 | MUST |
| `ui.toggle.off` | Toggle tắt | 2 | MUST |
| `ui.slider.tick` | Tick nhẹ khi kéo slider | 2 | SHOULD |
| `ui.currency.coin.add` | Coin cộng/đếm | 3 | MUST |
| `ui.currency.gold.add` | Gold cộng | 3 | MUST |
| `ui.currency.gem.add` | Gem cộng, sáng hơn coin | 3 | MUST |
| `ui.purchase.success` | Mua thành công | 2 | MUST |
| `ui.weapon.equip` | Lắp súng vào slot | 3 | MUST |
| `ui.weapon.unequip` | Tháo súng | 2 | MUST |
| `ui.weapon.upgrade` | Nâng sao/nâng súng | 3 | MUST |
| `ui.costume.equip` | Mặc costume | 3 | MUST |
| `ui.costume.randomize` | Random costume | 2 | SHOULD |
| `ui.pass.progress` | Pass XP chạy | 2 | SHOULD |
| `ui.quest.complete` | Quest hoàn thành | 2 | MUST |
| `ui.reward.claim` | Claim reward thường | 3 | MUST |
| `ui.reward.rare` | Reward hiếm | 2 | MUST |
| `ui.stage.select` | Chọn stage | 2 | MUST |
| `ui.stage.locked` | Stage bị khoá/thiếu power | 2 | MUST |
| `ui.loading.start` | Chuyển scene bắt đầu | 1 | SHOULD |
| `ui.count.tick` | Payout/count-up từng nhịp | 3 | MUST |
| `ui.count.finish` | Payout dừng ở tổng | 2 | MUST |
| `ui.new_record` | Kỷ lục mới | 1 | MUST |
| `ui.ftue.focus` | Tooltip FTUE xuất hiện | 2 | SHOULD |
| `ui.ftue.complete` | Hoàn thành bước FTUE | 2 | SHOULD |

### Gacha

| ID / key gốc | Cue | Var | Ưu tiên |
|---|---|---:|---|
| `ui.gacha.start` | Máy quay bắt đầu | 2 | MUST |
| `ui.gacha.spin.loop` | Loop quay, tăng căng thẳng | 1 | MUST |
| `ui.gacha.card.flip` | Lật từng card | 3 | MUST |
| `ui.gacha.reveal.common` | Reveal Common | 2 | MUST |
| `ui.gacha.reveal.uncommon` | Reveal Uncommon | 2 | MUST |
| `ui.gacha.reveal.rare` | Reveal Rare | 2 | MUST |
| `ui.gacha.reveal.epic` | Reveal Epic | 2 | MUST |
| `ui.gacha.reveal.legendary` | Reveal Legendary | 2 | MUST |
| `ui.gacha.duplicate` | Đổi đồ trùng thành mảnh | 2 | MUST |
| `ui.gacha.skip` | Skip animation | 1 | SHOULD |

## B. Player, movement và trạng thái

### Bước chân

| ID / key gốc | Bề mặt | Var | Ghi chú |
|---|---|---:|---|
| `sfx.player.footstep.sand` | Cát/đất khô, bề mặt chính của map | 6 | MUST |
| `sfx.player.footstep.rock` | Đá/cliff | 6 | MUST |
| `sfx.player.footstep.wood` | Thùng/sàn gỗ nếu đi được | 6 | SHOULD |
| `sfx.player.footstep.metal` | Tấm kim loại/khu công nghiệp | 6 | SHOULD |
| `sfx.player.footstep.bone` | Khu nghĩa địa xương | 6 | SHOULD |

### Player feedback

| ID / key gốc | Cue | Var | Ưu tiên |
|---|---|---:|---|
| `sfx.player.hurt.light` | Trúng hit nhẹ | 4 | MUST |
| `sfx.player.hurt.heavy` | Trúng hit nặng/boss | 3 | MUST |
| `sfx.player.low_health.loop` | Nhịp tim low HP | 1 | MUST |
| `sfx.player.heal` | Hồi máu | 3 | MUST |
| `sfx.player.death` | Player chết | 2 | MUST |
| `sfx.player.revive` | Hồi sinh | 2 | MUST |
| `sfx.player.dodge_whoosh` | Whoosh khi đổi hướng nhanh | 3 | LATER |
| `sfx.player.weapon_switch` | Đổi slot súng | 3 | MUST |
| `sfx.player.empty_mag` | Bóp cò/rơi vào reload khi hết đạn | 3 | MUST |
| `sfx.player.reload.complete` | Reload hoàn tất, feedback nhỏ | 2 | SHOULD |
| `sfx.player.bomb.throw` | Vung/ném bom | 3 | MUST |
| `sfx.player.bomb.bounce` | Bom chạm đất/prop | 4 | MUST |
| `sfx.player.bomb.fuse` | Fuse ngắn trước nổ | 2 | MUST |
| `sfx.player.bomb.explode` | Bom người chơi nổ | 4 | MUST |
| `sfx.player.bomb.ready` | Bom hồi/nhặt thêm | 2 | SHOULD |

## C. 25 súng — mỗi súng có tiếng riêng

Mỗi dòng cần: `fire` 4 var, `reload` 2 var, `mechanical/dry` 2 var. Reload có thể ghép layer dùng chung theo họ súng, nhưng **final cue của mỗi súng phải nghe khác nhau**.

| Prefix key | Súng | Cá tính âm thanh |
|---|---|---|
| `sfx.weapon.pistol_a` | Pistol A | 9mm gọn, trung tính, dễ nghe lâu |
| `sfx.weapon.glock19` | Glock 19 | 9mm polymer, snap ngắn và sạch |
| `sfx.weapon.p226` | P226 | 9mm kim loại, slide nặng hơn Glock |
| `sfx.weapon.m1911` | M1911 | .45 trầm, lực, nhịp chậm |
| `sfx.weapon.beretta_m9` | Beretta M9 | 9mm sáng, open-slide metallic |
| `sfx.weapon.usp45` | USP .45 | .45 gọn nhưng sâu và chắc |
| `sfx.weapon.desert_eagle` | Desert Eagle | handgun cực nặng, crack lớn, tail ngắn |
| `sfx.weapon.five_seven` | Five-seveN | 5.7 sắc, tốc độ cao, ít bass hơn |
| `sfx.weapon.makarov` | Makarov | compact cũ, nhỏ và khô |
| `sfx.weapon.python357` | Python .357 | revolver metallic, crack mạnh, cylinder reload |
| `sfx.weapon.smg_generic` | SMG Generic | 9mm auto nhanh, transient nhỏ để không mệt tai |
| `sfx.weapon.ar_generic` | Assault Rifle Generic | 5.56 cân bằng, arcade military |
| `sfx.weapon.m4a1` | M4A1 | 5.56 sạch, controlled, mechanical rõ |
| `sfx.weapon.ak47` | AK-47 | 7.62 gắt, bass mạnh, cơ khí thô |
| `sfx.weapon.scarl` | SCAR-L | 5.56 dày, hiện đại, bolt nặng |
| `sfx.weapon.famas` | FAMAS | bullpup sắc, cadence cao |
| `sfx.weapon.g36c` | G36C | compact rifle, punch ngắn và sáng |
| `sfx.weapon.shotgun_generic` | Shotgun Generic | pump 12-gauge, boom gọn |
| `sfx.weapon.benelli_m4` | Benelli M4 | semi-auto shotgun, action nhanh |
| `sfx.weapon.mossberg500` | Mossberg 500 | pump rõ, boom hơi thô |
| `sfx.weapon.spas12` | SPAS-12 | shotgun nặng, metallic action |
| `sfx.weapon.double_barrel` | Double Barrel | blast đôi cực dày, break-open reload |
| `sfx.weapon.aa12` | AA-12 | automatic shotgun, thump ngắn để giữ mix sạch |
| `sfx.weapon.sniper_generic` | Sniper Generic | high-caliber, crack mạnh, bolt-action rõ |
| `sfx.weapon.lmg_generic` | LMG Generic | belt-fed dày, sustained fire, belt/rattle |

### Reload layer dùng để ráp final cue

| ID | Layer | Var |
|---|---|---:|
| `sfx.reload.mag.small.out/in` | Mag pistol/SMG rút và gắn | 4 mỗi loại |
| `sfx.reload.mag.rifle.out/in` | Mag rifle rút và gắn | 4 mỗi loại |
| `sfx.reload.mag.lmg.out/in` | Box/belt LMG | 3 mỗi loại |
| `sfx.reload.shell.insert` | Nhét shell shotgun | 6 |
| `sfx.reload.action.slide` | Slide pistol | 4 |
| `sfx.reload.action.bolt` | Bolt rifle/sniper | 4 |
| `sfx.reload.action.pump` | Pump shotgun | 4 |
| `sfx.reload.action.breakopen` | Break-open double barrel | 3 |
| `sfx.reload.action.cylinder` | Revolver cylinder/eject | 3 |

## D. Bullet travel và impact theo vật liệu

| ID / key gốc | Cue | Var | Ưu tiên |
|---|---|---:|---|
| `sfx.bullet.whiz.near` | Đạn sượt camera/player | 4 | SHOULD |
| `sfx.bullet.ricochet` | Ricochet hiếm trên đá/kim loại | 6 | SHOULD |
| `sfx.impact.flesh.light` | Đạn nhẹ trúng zombie | 6 | MUST |
| `sfx.impact.flesh.heavy` | Sniper/shotgun trúng zombie | 6 | MUST |
| `sfx.impact.bone` | Trúng skeleton | 6 | MUST |
| `sfx.impact.plant` | Trúng cactus | 6 | MUST |
| `sfx.impact.fur` | Trúng dog/cat/mole | 6 | MUST |
| `sfx.impact.sand` | Đạn chạm cát/đất | 6 | MUST |
| `sfx.impact.rock` | Đạn chạm đá/cliff | 6 | MUST |
| `sfx.impact.wood` | Đạn chạm crate/gỗ | 6 | MUST |
| `sfx.impact.metal` | Đạn chạm barrel/kim loại | 6 | MUST |
| `sfx.impact.explosive_critical` | Hit cuối kích hoạt barrel | 3 | SHOULD |
| `sfx.impact.pierce` | Đạn xuyên qua mục tiêu | 4 | SHOULD |

Surface routing bản đầu cần tối thiểu: `Sand`, `Rock`, `Wood`, `Metal`, `Flesh`, `Bone`, `Plant`, `Fur`.

## E. Pickup và interactive object

| ID / key gốc | Cue | Var | Ưu tiên |
|---|---|---:|---|
| `sfx.pickup.drop.coin` | Coin rơi/bật ra | 4 | MUST |
| `sfx.pickup.drop.gem` | Gem rơi | 4 | MUST |
| `sfx.pickup.drop.health` | Health rơi | 3 | MUST |
| `sfx.pickup.drop.bomb` | Bomb pickup rơi | 3 | MUST |
| `sfx.pickup.magnet` | Bắt đầu hút về player | 3 | SHOULD |
| `sfx.pickup.collect.coin` | Nhặt coin | 5 | MUST |
| `sfx.pickup.collect.gem` | Nhặt gem | 4 | MUST |
| `sfx.pickup.collect.health` | Nhặt heal | 3 | MUST |
| `sfx.pickup.collect.bomb` | Nhặt bomb | 3 | MUST |
| `sfx.pickup.collect.sweep` | Tự gom loot cuối wave | 2 | SHOULD |
| `sfx.prop.crate.hit` | Đạn trúng loot crate | 6 | MUST |
| `sfx.prop.crate.break` | Crate vỡ | 4 | MUST |
| `sfx.prop.crate.loot_burst` | Loot bung ra | 3 | MUST |
| `sfx.prop.barrel.hit` | Đạn trúng barrel | 6 | MUST |
| `sfx.prop.barrel.warning` | Kim loại rung/hiss trước nổ | 2 | SHOULD |
| `sfx.prop.barrel.explode` | Barrel nổ | 4 | MUST |
| `sfx.prop.barrel.chain` | Nhịp báo chain explosion | 3 | SHOULD |
| `sfx.prop.skill_crate.upgrade` | Thùng cho một lần nâng skill | 3 | LATER |
| `sfx.prop.skill_crate.stat` | Skill max, đổi thành stat upgrade | 3 | LATER |

Hai cue `skill_crate` giữ trong manifest nhưng chưa generate cho tới khi hệ skill được chốt.

## F. Zombie voice packs

Mỗi zombie dưới đây cần các cue: `idle` 3 var, `aggro` 3, `attack` 3, `hurt` 4, `death` 3. Cột `Special` là cue thêm.

| Prefix key | Zombie | Chất giọng | Special |
|---|---|---|---|
| `sfx.zombie.dog_pup` | Dog Pup | gầm/gừ nhỏ, nhanh, không quá đáng sợ | — |
| `sfx.zombie.cat_meow` | Cat Meow | hiss/meow méo nhẹ | — |
| `sfx.zombie.skeleton` | Skeleton | xương lách cách, rên khô | `bone_rattle` |
| `sfx.zombie.dog_bark` | Dog Bark | bark gắt, báo pounce | `pounce_windup`, `pounce_air`, `pounce_land` |
| `sfx.zombie.cat_bolt` | Cat Bolt | hiss điện, nhanh | `pounce_windup`, `pounce_air`, `pounce_land` |
| `sfx.zombie.cat_lightning` | Cat Lightning | growl có electric crackle | `pounce_windup`, `pounce_air`, `pounce_land` |
| `sfx.zombie.dog_bowwow` | Dog Bowwow | chó lớn, bass hơn | `pounce_windup`, `pounce_air`, `pounce_land` |
| `sfx.zombie.cacti` | Cacti | plant squeak/rasp nhỏ | `spit_launch` |
| `sfx.zombie.cactus` | Cactus | plant growl dày hơn | `spit_launch` |
| `sfx.zombie.skeleton_mage` | Skeleton Mage | whisper phép + xương | `cast_windup`, `spit_launch` |
| `sfx.zombie.burrow` | Burrow | đất cào, growl dưới đất | `dig_down`, `underground_loop`, `emerge_warn`, `emerge_burst` |
| `sfx.zombie.mole_rat` | Mole Rat | squeal trầm, móng cào | `dig_down`, `underground_loop`, `emerge_warn`, `emerge_burst` |
| `sfx.zombie.mole_rat_king` | Mole Rat King | boss squeal/bass rumble | `dig_down`, `underground_loop`, `emerge_warn`, `emerge_burst`, `boss_intro` |
| `sfx.zombie.skeleton_giant` | Skeleton Giant | xương lớn, sub thump | `slam_windup`, `slam_impact`, `boss_intro` |
| `sfx.zombie.cactus_boss` | Cactus Boss | plant roar nặng | `slam_windup`, `slam_impact`, `charge_warn`, `charge_loop`, `charge_hit`, `boss_intro` |

### Zombie movement và projectile dùng chung

| ID / key gốc | Cue | Var |
|---|---|---:|
| `sfx.zombie.step.paw_small` | Dog/cat nhỏ | 6 |
| `sfx.zombie.step.paw_large` | Dog/mole lớn | 6 |
| `sfx.zombie.step.bone_small` | Skeleton | 6 |
| `sfx.zombie.step.bone_large` | Skeleton Giant | 6 |
| `sfx.zombie.step.plant` | Cacti/cactus | 6 |
| `sfx.zombie.horde.movement_bed` | Lớp chuyển động đám đông xa | 2 |
| `sfx.zombie.projectile.fly` | Projectile bay | 3 |
| `sfx.zombie.projectile.impact_ground` | Projectile chạm đất | 4 |
| `sfx.zombie.projectile.impact_player` | Projectile trúng player | 4 |

## G. Wave, combat state và kết quả

| ID / key gốc | Cue | Var | Ưu tiên |
|---|---|---:|---|
| `stinger.run.start` | Vào trận | 1 | MUST |
| `stinger.wave.start` | Wave bắt đầu | 2 | MUST |
| `stinger.wave.pressure_up` | Mật độ quái tăng | 2 | SHOULD |
| `stinger.wave.clear` | Clear wave | 2 | MUST |
| `stinger.elite.spawn` | Elite xuất hiện | 2 | MUST |
| `stinger.boss.warning` | Cảnh báo boss | 1 | MUST |
| `stinger.boss.spawn` | Boss vào sân | 2 | MUST |
| `stinger.level_up` | Level up/perk modal | 2 | LATER |
| `stinger.victory` | Thắng trận | 2 | MUST |
| `stinger.defeat` | Thua trận | 2 | MUST |
| `stinger.revive.countdown` | Tick countdown revive | 2 | MUST |
| `stinger.revive.go` | Revive hoàn tất | 2 | MUST |

## H. Atmosphere theo 5 map

Mỗi map có một `base_loop`, một `detail_set` phát ngẫu nhiên và một `danger_layer` fade in khi pressure cao.

| Prefix key | Map | Base atmosphere | Detail |
|---|---|---|---|
| `amb.stage1.outbreak` | Bùng Phát | Gió sa mạc nhẹ, không gian mở | bụi, gỗ/kim loại xa |
| `amb.stage2.thorn_fields` | Cánh Đồng Gai | Gió khô qua gai | lá khô/cactus rít |
| `amb.stage3.bone_yard` | Nghĩa Địa Xương | Gió rỗng, eerie nhưng không horror nặng | xương lách cách xa |
| `amb.stage4.wild_pack` | Bầy Hoang | Gió mạnh hơn, thú xa | howl/bark rất xa |
| `amb.stage5.titan_siege` | Vây Hãm Titan | Gió bụi và low rumble | rung đất/impact xa |

Cue chung: `amb.wind.gust` 4 var, `amb.dust.pass` 3, `amb.distant_rumble` 3, `amb.silence_tension` 1 transition.

## I. Music

| ID / key | Track | Dài/loop | Vai trò |
|---|---|---|---|
| `music.main_theme` | Main Theme | 100–130s loop | Nhận diện game, energetic survival arcade |
| `music.hub` | Hub | 90–120s loop | Nhẹ, có groove, không căng |
| `music.shop_loadout` | Shop & Loadout | 90–120s loop | Nhanh hơn Hub, cảm giác nâng đồ |
| `music.gacha` | Gacha machine | 45–70s loop | Tò mò, mechanical sparkle |
| `music.campaign_select` | Campaign select | 70–100s loop | Chuẩn bị bước vào trận |
| `music.stage1` | Bùng Phát | 100–140s loop | Mở đầu, nhịp dễ đọc |
| `music.stage2` | Cánh Đồng Gai | 100–140s loop | Percussion khô, căng hơn |
| `music.stage3` | Nghĩa Địa Xương | 100–140s loop | Bone percussion, dark playful |
| `music.stage4` | Bầy Hoang | 100–140s loop | Nhanh, chase rhythm |
| `music.stage5` | Vây Hãm Titan | 110–150s loop | Quy mô lớn, áp lực cao |
| `music.boss` | Boss combat | 90–130s loop | Layer mạnh, ưu tiên nhịp và telegraph |
| `music.victory` | Victory result | 35–60s loop | Thắng, payout |
| `music.defeat` | Defeat result | 35–60s loop | Thua nhưng thúc chơi lại |

Gameplay music nên có thêm một stem `music.combat_pressure_layer` dùng chung, fade theo mật độ quái. Bản đầu không cần hệ adaptive phức tạp hơn.

## J. Tổng scope dự kiến

| Nhóm | Logical cues xấp xỉ | Số clip ship xấp xỉ |
|---|---:|---:|
| UI/meta/gacha | 40 | 90 |
| Player/movement/bomb | 25 | 90 |
| 25 weapon + reload layers | 84 | 260 |
| Bullet/impact | 13 | 74 |
| Pickup/interactive | 19 | 64 |
| Zombie voices/special/movement | 110 | 340 |
| Stinger/wave/result | 12 | 22 |
| Ambience | 20 | 35 |
| Music | 14 | 14 |
| **Tổng** | **khoảng 337 cue** | **khoảng 989 clip** |

Đây là full scope. Để kiểm soát thời gian, batch MVP nên làm theo thứ tự:

1. Settings + UI nền + 25 fire/reload.
2. Footstep + impacts + player + pickup/prop.
3. 15 zombie voice pack + special.
4. Atmosphere + music.

