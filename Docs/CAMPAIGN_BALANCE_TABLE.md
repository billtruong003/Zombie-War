# Campaign balance table

> Generated from the live assets by the campaign authoring tools. All tuning is PROVISIONAL
> and expected to move once real run telemetry exists.

## Stage summary

| Stage | Name | Scene | Waves | Enemies | Min power | Rec power | Boss | First-clear |
|---|---|---|---:|---:|---:|---:|---|---|
| level.1 | Bùng Phát | Map_Level1 | 5 | 61 | 0 | 300 | - | 400c / 0g / 0gem |
| level.2 | Cánh Đồng Gai | Map_Level2 | 6 | 72 | 700 | 950 | cactus_boss | 700c / 5g / 0gem |
| level.3 | Nghĩa Địa Xương | Map_Level3 | 7 | 90 | 1100 | 1400 | skeleton_giant | 1100c / 10g / 0gem |
| level.4 | Bầy Hoang | Map_Level4 | 7 | 103 | 1500 | 1800 | mole_rat_king | 1600c / 15g / 1gem |
| level.5 | Vây Hãm Titan | Map_Level5 | 7 | 92 | 1900 | 2200 | cactus_boss | 2400c / 25g / 3gem |

**Global Combat Power ceiling** (best 3 weapons at 3 stars): **2372**. Every stage gate is
below this, so no stage can become unreachable. Stage 5 sits at ~80% of the ceiling.

## Enemy stats and rewards

| Enemy | Behaviour | Archetype | HP | DMG | Speed | Range | Windup | Coin | XP |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|
| Burrow | Burrower | Burrower | 65 | 12 | 2.8 | 1.6 | 0.40s | 3 | 3 |
| Cacti | Ranged | Ranged | 40 | 8 | 1.8 | 9.0 | 0.45s | 2 | 2 |
| Cactus | Ranged | Ranged | 70 | 11 | 1.9 | 10.0 | 0.45s | 3 | 3 |
| Cactus Boss | Charger | Boss | 1400 | 30 | 2.6 | 3.2 | 0.50s | 40 | 35 |
| Cat Bolt | Pouncer | Runner | 50 | 11 | 5.2 | 1.4 | 0.30s | 2 | 3 |
| Cat Lightning | Pouncer | Runner | 60 | 12 | 5.0 | 1.4 | 0.30s | 3 | 3 |
| Cat Meow | Walker | Walker | 45 | 7 | 2.6 | 1.3 | 0.35s | 1 | 1 |
| Dog Bark | Pouncer | Runner | 55 | 10 | 4.6 | 1.4 | 0.30s | 2 | 2 |
| Dog Bowwow | Pouncer | Runner | 110 | 16 | 4.2 | 1.8 | 0.35s | 4 | 4 |
| Dog Pup | Walker | Walker | 40 | 6 | 2.4 | 1.3 | 0.35s | 1 | 1 |
| Mole Rat | Burrower | Burrower | 75 | 13 | 3.0 | 1.6 | 0.40s | 3 | 3 |
| Mole Rat King | Burrower | Boss | 1100 | 24 | 3.2 | 2.2 | 0.40s | 35 | 30 |
| Skeleton | Walker | Walker | 60 | 9 | 2.5 | 1.5 | 0.40s | 2 | 2 |
| Skeleton Giant | Boss | Heavy | 900 | 26 | 2.2 | 2.8 | 0.50s | 30 | 25 |
| Skeleton Mage | Ranged | Ranged | 80 | 14 | 2.6 | 12.0 | 0.50s | 4 | 4 |

## Per-stage wave detail

### Bùng Phát (level.1)

Suggested families: Pistol, SMG  
Suggested weapons: `weapon.sidearm.pistol_a`, `weapon.smg.generic`

| Wave | Composition | Interval | Max concurrent | Rest |
|---|---|---:|---:|---:|
| Dò đường | Dog Pup x6 | 1.10s | 6 | 5s |
| Bầy nhỏ | Dog Pup x8, Cat Meow x4 | 0.90s | 8 | 5s |
| Xương khô | Cat Meow x8, Skeleton x4 | 0.90s | 10 | 5s |
| Đông dần | Dog Pup x10, Skeleton x8 | 0.80s | 12 | 6s |
| Kẻ chạy | Skeleton x10, Dog Bark x3 | 0.80s | 14 | 0s |

### Cánh Đồng Gai (level.2)

Suggested families: Shotgun, AR  
Suggested weapons: `weapon.shotgun.generic`, `weapon.assault_rifle.generic`, `weapon.smg.generic`

| Wave | Composition | Interval | Max concurrent | Rest |
|---|---|---:|---:|---:|
| Gai nhỏ | Dog Pup x8, Cacti x3 | 0.90s | 10 | 5s |
| Bắn tỉa | Cacti x6, Dog Bark x4 | 0.85s | 12 | 5s |
| Phục kích | Burrow x4, Cat Meow x8 | 0.85s | 12 | 6s |
| Rừng gai | Cactus x5, Cacti x6, Dog Bark x4 | 0.80s | 14 | 6s |
| Vây hãm | Burrow x5, Cactus x6, Skeleton x8 | 0.75s | 16 | 8s |
| BOSS | Cactus Boss x1, Cacti x4 | 1.20s | 8 | 0s |

### Nghĩa Địa Xương (level.3)

Suggested families: AR, Marksman, Shotgun  
Suggested weapons: `weapon.assault_rifle.m4a1`, `weapon.marksman.sniper_generic`, `weapon.shotgun.generic`

| Wave | Composition | Interval | Max concurrent | Rest |
|---|---|---:|---:|---:|
| Xương trỗi | Skeleton x12 | 0.85s | 12 | 5s |
| Pháp sư | Skeleton x10, Skeleton Mage x3 | 0.80s | 14 | 5s |
| Giữ khoảng | Skeleton Mage x5, Dog Bark x6 | 0.80s | 14 | 6s |
| Kẻ nặng | Skeleton Giant x1, Skeleton x10 | 0.80s | 14 | 6s |
| Hợp lực | Skeleton Mage x5, Cactus x5, Skeleton x10 | 0.75s | 16 | 6s |
| Áp đảo | Skeleton Giant x1, Skeleton Mage x4, Dog Bark x8 | 0.70s | 16 | 8s |
| BOSS | Skeleton Giant x2, Skeleton x8 | 1.10s | 10 | 0s |

### Bầy Hoang (level.4)

Suggested families: SMG, AR, Shotgun  
Suggested weapons: `weapon.smg.generic`, `weapon.assault_rifle.scar_l`, `weapon.shotgun.benelli_m4`

| Wave | Composition | Interval | Max concurrent | Rest |
|---|---|---:|---:|---:|
| Vuốt nhanh | Cat Bolt x6, Dog Bark x6 | 0.80s | 12 | 5s |
| Sấm sét | Cat Lightning x6, Cat Bolt x6 | 0.75s | 14 | 5s |
| Đào ngầm | Mole Rat x5, Cat Bolt x8 | 0.75s | 14 | 6s |
| Đè nặng | Dog Bowwow x4, Cat Lightning x8 | 0.70s | 16 | 6s |
| Bầy đàn | Cat Bolt x10, Cat Lightning x8, Mole Rat x5 | 0.70s | 18 | 6s |
| Săn mồi | Dog Bowwow x6, Mole Rat x6, Dog Bark x8 | 0.65s | 18 | 8s |
| BOSS | Mole Rat King x1, Mole Rat x4, Cat Bolt x6 | 1.10s | 12 | 0s |

### Vây Hãm Titan (level.5)

Suggested families: AR, Shotgun, Marksman  
Suggested weapons: `weapon.assault_rifle.ak_47`, `weapon.shotgun.aa_12`, `weapon.marksman.sniper_generic`

| Wave | Composition | Interval | Max concurrent | Rest |
|---|---|---:|---:|---:|
| Tiền trạm | Skeleton x10, Cat Bolt x6 | 0.75s | 14 | 5s |
| Hoả lực | Skeleton Mage x5, Cactus x5 | 0.75s | 14 | 5s |
| Ngầm & nhanh | Mole Rat x5, Cat Lightning x8 | 0.70s | 16 | 6s |
| Tinh nhuệ | Skeleton Giant x1, Dog Bowwow x4, Skeleton Mage x4 | 0.70s | 16 | 6s |
| Tổng lực | Cat Bolt x10, Skeleton x10, Cacti x6 | 0.65s | 18 | 6s |
| Song tinh | Skeleton Giant x1, Mole Rat King x1, Dog Bark x8 | 0.80s | 14 | 8s |
| BOSS CUỐI | Cactus Boss x1, Cactus x4, Skeleton Mage x3 | 1.20s | 10 | 0s |

## Weapon effective DPS

Sustained DPS including pellets, fire rate, magazine and reload. Not raw `damage`.

| Weapon | 1 star DPS | 3 star DPS | 3 star power |
|---|---:|---:|---:|
| `weapon.assault_rifle.g36c` | 150 | 217 | 2170 |
| `weapon.assault_rifle.famas` | 129 | 186 | 1856 |
| `weapon.assault_rifle.scar_l` | 120 | 173 | 1735 |
| `weapon.shotgun.aa_12` | 116 | 168 | 1680 |
| `weapon.assault_rifle.ak_47` | 114 | 165 | 1652 |
| `weapon.assault_rifle.m4a1` | 109 | 158 | 1578 |
| `weapon.lmg.generic` | 103 | 151 | 1511 |
| `weapon.assault_rifle.generic` | 94 | 136 | 1357 |
| `weapon.shotgun.spas_12` | 82 | 118 | 1184 |
| `weapon.sidearm.five_seven` | 65 | 96 | 958 |
| `weapon.smg.generic` | 63 | 90 | 902 |
| `weapon.shotgun.mossberg_500` | 56 | 82 | 816 |
| `weapon.marksman.sniper_generic` | 54 | 78 | 783 |
| `weapon.shotgun.generic` | 53 | 76 | 763 |
| `weapon.sidearm.desert_eagle` | 52 | 75 | 748 |
| `weapon.sidearm.beretta_m9` | 50 | 72 | 724 |
| `weapon.sidearm.usp_45` | 50 | 72 | 724 |
| `weapon.shotgun.benelli_m4` | 44 | 64 | 642 |
| `weapon.sidearm.p226` | 43 | 63 | 630 |
| `weapon.sidearm.glock_19` | 43 | 63 | 627 |
| `weapon.sidearm.python_357` | 43 | 62 | 624 |
| `weapon.sidearm.m1911` | 43 | 62 | 620 |
| `weapon.shotgun.double_barrel` | 38 | 54 | 542 |
| `weapon.sidearm.pistol_a` | 34 | 50 | 501 |
| `weapon.sidearm.makarov` | 34 | 50 | 497 |
