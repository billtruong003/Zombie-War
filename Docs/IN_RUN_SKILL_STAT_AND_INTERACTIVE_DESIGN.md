# Zombie War: In-run Skill, Stat và Interactive Object

> Trạng thái: Hướng gameplay do chủ dự án chốt ngày 2026-07-24. Các con số vẫn cần playtest
> trước khi trở thành balance cuối.
>
> Mục tiêu của tài liệu này là làm cho mỗi run có lựa chọn rõ ràng. Người chơi vẫn mạnh chủ yếu nhờ
> súng. Skill hỗ trợ cách dùng súng. Zombie, pickup và vật thể trong map cùng tham gia vào tiến trình
> của run.

## 1. Trải nghiệm cốt lõi

Người chơi mang ba súng vào trận:

- Một Pistol bắt buộc.
- Hai súng dài lấy từ Loadout.

Mỗi súng có ba skill để chọn. Một loadout vì thế có chín skill tiềm năng.

Trong một run, người chơi chỉ mở tối đa ba skill:

- Một skill của Pistol.
- Một skill của súng dài thứ nhất.
- Một skill của súng dài thứ hai.

Skill được nạp khi khẩu súng đang được cất. Khi người chơi đổi sang khẩu súng đã nạp đầy, skill của
khẩu đó tự kích hoạt.

Vòng chơi chính:

```text
Giết zombie
-> nhặt XP và các vật phẩm rơi ra
-> lên cấp
-> chọn một skill hoặc nâng skill đã có
-> bắn vỡ vật thể trong map để lấy thêm tài nguyên
-> nạp skill cho các súng đang cất
-> đổi súng đúng lúc để kích hoạt skill
-> khi ba skill đã đạt cấp tối đa, tiếp tục nâng chỉ số nhân vật
```

Mỗi quyết định đều phải nhìn thấy được trong trận. Một card tốt không chỉ đổi con số trong bảng.
Nó phải giúp người chơi chạy thoát, gom quái, giữ khoảng cách, bắn xuyên một hàng hoặc giữ được vị
trí lâu hơn.

## 2. Những gì game đã có

Các hệ thống sau đã tồn tại và phải được giữ lại:

- Zombie có `coinReward` và `xpReward`.
- Zombie thường rơi Coin vật lý.
- Elite và boss có thể rơi Gem.
- Pickup có cơ chế nằm trên đất, bay về Player khi vào tầm hút và chỉ được nhận một lần.
- Pickup hiện có Coin, Gem, Health và Bomb.
- Khi hết wave, các pickup còn sót lại được tự thu.
- Loot Crate có thể rơi Coin, Gem, Health và Bomb.
- Explosive Barrel gây damage theo bán kính.
- Explosive Barrel có thể kích nổ dây chuyền.
- Vụ nổ của thùng có thể làm đau cả zombie lẫn Player.
- RunState đã giữ XP, level, perk, kill, tiền và kết quả của run.

Các thay đổi trong tài liệu này phải mở rộng hệ thống trên. Không tạo một hệ pickup thứ hai chạy
song song.

## 3. Ba đường tăng sức mạnh trong một run

Một run có ba đường tiến bộ khác nhau.

### 3.1. XP từ zombie

Zombie chết sẽ rơi XP Orb.

- XP Orb là nguồn lên cấp chính.
- XP Orb dùng cùng cơ chế magnet với Coin.
- XP chỉ có tác dụng trong run hiện tại.
- XP không đi vào ví tiền ngoài trận.
- Khi hết wave, XP còn trên đất được tự thu.

Khi bật XP Orb vật lý, `RecordKill` không được cộng XP trực tiếp nữa. Nếu vừa cộng trực tiếp vừa rơi
XP Orb, một kill sẽ trả XP hai lần.

### 3.2. Upgrade Core từ vật thể và mốc quan trọng

Upgrade Core cho một lần chọn hoặc nâng skill ngay lập tức.

Nguồn chính:

- Bắn vỡ Skill Crate trong map.
- Hạ boss.
- Hạ một số elite đặc biệt.
- Hoàn thành mốc wave do designer đặt.

Upgrade Core không cộng XP. Nó mở thẳng màn chọn skill.

### 3.3. Stat Upgrade sau khi skill đã hoàn chỉnh

Mỗi skill có năm cấp. Ba skill tối đa cần tổng cộng 15 lần chọn.

Sau khi cả ba skill đạt cấp 5:

- Level-up mới chỉ đưa card tăng chỉ số.
- Upgrade Core mới cũng đổi thành một lần chọn chỉ số.
- Skill đã đạt cấp 5 không xuất hiện lại.
- Người chơi không thể mở skill thứ tư.

Giai đoạn đầu run là lúc tạo build. Giai đoạn cuối run là lúc đẩy chỉ số của build đó lên cao.

## 4. Luật chọn skill

### 4.1. Khi chưa chọn đủ ba skill

Mỗi lần lên cấp, game đưa ba card hợp lệ.

Ưu tiên offer:

1. Skill của khẩu súng chưa có skill.
2. Skill đã chọn nhưng chưa đạt cấp 5.
3. Không đưa skill của súng ngoài Loadout.

Khi người chơi chọn một skill của một khẩu súng:

- Skill đó được mở ở cấp 1.
- Hai skill còn lại của khẩu đó bị khóa trong run.
- Lần lên cấp sau chỉ có thể nâng skill đã chọn của khẩu đó.

### 4.2. Khi đã chọn đủ ba skill

Game chỉ đưa card nâng ba skill đang dùng.

Nếu một skill đã đạt cấp 5, game loại nó khỏi pool card.

Nếu còn hai skill chưa tối đa, ba card có thể gồm:

- Một cấp của skill A.
- Một cấp của skill B.
- Một lựa chọn đổi card miễn phí hoặc một bản nâng hiếm của A/B.

Không được tạo card rỗng hoặc card không còn tác dụng.

### 4.3. Khi cả ba skill đã tối đa

Game chuyển hẳn sang Stat Upgrade.

Mỗi lần lên cấp vẫn đưa ba card và người chơi chọn một.

## 5. Nạp và kích hoạt skill bằng đổi súng

### 5.1. Nạp skill

Skill của khẩu súng đang cầm không tự nạp.

Hai khẩu đang cất nhận charge từ:

- Kill bằng khẩu đang cầm.
- Damage lên elite và boss.
- Nhặt một số pickup đặc biệt.
- Hiệu ứng skill hoặc stat cho thêm charge.

Skill nhẹ nên đầy sau khoảng 10 đến 14 kill. Skill mạnh nên cần khoảng 18 đến 25 kill.

### 5.2. Kích hoạt

Khi skill đầy:

- Viền nút súng sáng lên.
- Vòng charge đạt 100%.
- Skill icon nhấp nhẹ.

Người chơi chạm nút súng để đổi sang khẩu đó. Skill tự kích hoạt ngay sau khi đổi xong.

Nếu skill cần mục tiêu nhưng không có mục tiêu hợp lệ:

- Game vẫn đổi súng.
- Skill chưa tiêu charge.
- Skill chờ tối đa 1,5 giây để tìm mục tiêu.
- Nếu vẫn không có mục tiêu, charge được giữ lại.

### 5.3. Không ép người chơi đổi súng

Người chơi có thể tiếp tục dùng khẩu đang cầm. Game không giảm damage và không phạt họ.

Skill chỉ là phần thưởng cho người biết đổi súng đúng nhịp.

## 6. Damage mới: có dao động và có crit

Damage hiện tại dùng một giá trị cố định. Điều này làm mỗi hit giống hệt nhau.

Hệ mới thêm hai phần:

- Damage dao động nhẹ.
- Critical hit, gọi ngắn là crit, có xác suất gây damage lớn.

### 6.1. Công thức damage

```text
Damage cuối =
Damage gốc của súng
× nâng cấp sao vĩnh viễn
× Damage Bonus trong run
× số ngẫu nhiên từ 0,90 đến 1,10
× hệ số crit nếu hit đó crit
× giảm damage theo khoảng cách
× hệ số riêng của pellet, xuyên, chain hoặc vụ nổ
```

Khoảng 0,90 đến 1,10 đủ làm damage bớt máy móc nhưng không phá balance.

Ví dụ súng có damage gốc 100:

- Hit thường có thể gây từ 90 đến 110.
- Nếu crit multiplier là 1,75, một crit có thể gây từ 157,5 đến 192,5.

### 6.2. Chỉ số crit mặc định

- Crit Chance ban đầu: 5%.
- Crit Damage ban đầu: 175%.
- Crit Chance nên có giới hạn 60% trong run.
- Crit Damage nên có giới hạn 300%.

### 6.3. Luật roll damage

Mỗi hành động bắn chỉ roll một lần:

- Pistol, AR, SMG và LMG roll theo từng viên.
- Shotgun roll một lần cho cả phát bắn. Mọi pellet của phát đó cùng crit hoặc cùng không crit.
- Sniper và Railgun roll một lần cho cả đường đạn xuyên.
- Rocket roll một lần cho cả vụ nổ.
- Chain Lightning roll một lần cho toàn bộ chuỗi.
- Beam roll theo mỗi damage tick, nhưng nên dùng một seed ngắn để số không nhảy quá loạn.

### 6.4. Những damage không crit

Mặc định, các nguồn sau không crit:

- Damage của Explosive Barrel.
- Damage của zombie.
- Damage theo thời gian như Burn hoặc Bleed.
- Damage trực tiếp từ skill hỗ trợ.

Một skill chỉ được crit nếu mô tả của skill nói rõ điều đó.

### 6.5. Damage lên Player

Damage của zombie và môi trường không dùng dao động ngẫu nhiên.

Người chơi cần học được một đòn của zombie đau bao nhiêu. Nếu damage lên Player cũng nhảy ngẫu
nhiên, họ có thể chết dù đã tính đúng khoảng máu còn lại.

### 6.6. Hiển thị damage

- Hit thường dùng số trắng.
- Hit mạnh dùng số lớn hơn theo damage thật.
- Crit dùng số vàng hoặc cam, có chữ `CRIT`.
- Không hiện số lẻ.
- Shotgun gộp damage pellet trong một khoảng thời gian rất ngắn để tránh phủ kín màn hình bằng số.

## 7. Stat Upgrade

Stat Upgrade chỉ xuất hiện sau khi cả ba skill đã đạt cấp 5.

Hệ stat giữ ở mức dễ hiểu. Không thêm quá nhiều chỉ số nhỏ.

| Stat | Mỗi lần chọn | Giới hạn gợi ý | Ảnh hưởng |
|---|---:|---:|---|
| Damage | +8% | +120% | Tăng damage của mọi súng |
| Fire Rate | +6% | +60% | Bắn nhanh hơn |
| Crit Chance | +4 điểm % | 60% tổng | Crit thường xuyên hơn |
| Crit Damage | +15 điểm % | 300% tổng | Crit đau hơn |
| Reload Speed | +10% | +60% | Giảm thời gian reload |
| Max Health | +10% | +80% | Tăng máu tối đa và hồi 10% máu mới |
| Move Speed | +5% | +40% | Chạy và kite tốt hơn |
| Armor | +4% | 40% | Giảm damage Player nhận vào |
| Pickup Range | +15% | 2,5 lần gốc | Hút XP và loot từ xa hơn |
| Skill Charge | +8% | +50% | Nạp skill súng đang cất nhanh hơn |

Không cần chỉ số Accuracy vì game đang auto-aim.

Không cần chỉ số Luck trong bản đầu. Luck làm drop table khó đọc và khó balance. Chỉ thêm khi hệ
drop đã ổn định.

## 8. Pickup và vật phẩm rơi

Mỗi loại pickup phải có mục đích riêng.

| Pickup | Nguồn chính | Tác dụng |
|---|---|---|
| XP Orb | Mọi zombie | Tăng level trong run |
| Coin | Zombie và Loot Crate | Tiền kiếm được trong run |
| Gem | Elite, boss và Loot Crate hiếm | Tiền hiếm |
| Health | Loot Crate, food và drop hiếm | Hồi máu |
| Bomb | Loot Crate và drop hiếm | Thêm một lượt ném bomb |
| Upgrade Core | Skill Crate, boss, mốc wave | Mở hoặc nâng skill |
| Green Apple | Prop hoặc enemy đặc biệt | Hồi máu ngay |
| Blue Berry | Prop hoặc enemy đặc biệt | Thêm shield |
| Red Apple | Prop hoặc enemy đặc biệt | Vô hạn đạn trong thời gian ngắn |
| Cheese | Prop hoặc enemy đặc biệt | Nhân đôi Coin rơi trong thời gian ngắn |

### 8.1. Zombie thường

Zombie thường:

- Luôn cho XP.
- Cho Coin theo `ZombieData`.
- Có tỉ lệ rất thấp rơi Health, Bomb hoặc Food nếu drop table của loại zombie cho phép.
- Không rơi Gem thường xuyên.
- Không rơi Upgrade Core ngẫu nhiên.

### 8.2. Elite

Elite:

- Cho nhiều XP hơn zombie thường.
- Cho nhiều Coin hơn.
- Có cơ hội rơi Gem.
- Có cơ hội rơi Health, Bomb hoặc Food.
- Một số elite đặc biệt có thể bảo đảm một Upgrade Core.

### 8.3. Boss

Boss:

- Cho một cụm XP lớn.
- Cho Coin và Gem.
- Bảo đảm một Upgrade Core nếu người chơi còn skill chưa tối đa.
- Nếu skill đã tối đa, Upgrade Core đổi thành một lần chọn Stat Upgrade.

### 8.4. Tránh trả thưởng hai lần

Một phần thưởng chỉ đi qua một đường.

Ví dụ:

- Nếu zombie rơi XP Orb, `RecordKill` không cộng XP trực tiếp.
- Nếu zombie rơi Coin vật lý, `RecordKill` không cộng Coin trực tiếp.
- Nếu boss rơi Upgrade Core, hệ boss không được đồng thời gọi thẳng màn nâng cấp lần thứ hai.

## 9. Interactive Object

Interactive Object là vật thể người chơi có thể bắn để thay đổi tình hình trận đấu.

Game không cần nút tương tác riêng. Người chơi chỉ cần bắn trúng vật thể.

## 9.1. Skill Crate

Skill Crate là vật thể quan trọng nhất cho tiến trình skill.

### Cách hoạt động

- Crate có máu riêng.
- Đạn, xuyên, explosion và chain đều có thể phá crate.
- Khi vỡ, crate rơi một Upgrade Core bảo đảm.
- Crate có thể rơi thêm Coin, Health, Bomb hoặc Food theo bảng hiện có.

### Luật xuất hiện

- Chỉ nên có một đến hai Skill Crate trong một wave.
- Crate không hồi lại sau khi bị phá.
- Crate phải xuất hiện ở vị trí buộc người chơi di chuyển.
- Không đặt crate ngay cạnh điểm bắt đầu.

### Ảnh hưởng gameplay

Người chơi phải chọn:

- Giữ vị trí an toàn và bỏ crate.
- Chạy qua đám zombie để lấy một cấp skill sớm.
- Dùng Explosive Barrel gần đó để phá crate nhanh nhưng có nguy cơ tự bị thương.

## 9.2. Loot Crate

Loot Crate thường giữ vai trò hồi phục và tiếp tế.

Nó có thể rơi:

- Coin.
- Gem hiếm.
- Health.
- Bomb.
- Food buff.

Loot Crate thường không bảo đảm Upgrade Core. Nhờ vậy Skill Crate vẫn có hình dáng và giá trị riêng.

## 9.3. Explosive Barrel

Explosive Barrel là công cụ chiến đấu, không phải hộp quà.

### Cách hoạt động

- Bắn đủ damage sẽ làm thùng nổ.
- Damage giảm dần từ tâm ra mép.
- Vụ nổ làm đau zombie và Player.
- Thùng gần nhau phát nổ dây chuyền.
- Thùng không rơi loot.

### Ảnh hưởng gameplay

- Súng bắn nhanh có thể kích thùng dễ.
- Sniper có thể bắn xuyên zombie rồi trúng thùng phía sau.
- Shotgun có thể gom hoặc đẩy zombie về gần thùng.
- AR Mark hoặc SMG Shred không tăng damage thùng trừ khi skill ghi rõ.

## 9.4. Food Object

Một số food có thể nằm trực tiếp trong map hoặc rơi từ crate.

- Green Apple hồi máu.
- Blue Berry tạo shield.
- Red Apple cho vô hạn đạn trong thời gian ngắn.
- Cheese nhân đôi số Coin rơi.

Food không thay thế XP hoặc skill upgrade. Nó tạo một đợt sức mạnh ngắn trong combat.

## 9.5. Quy tắc chung cho vật thể

- Vật thể phải có hình dáng dễ nhận ra.
- Skill Crate, Loot Crate và Explosive Barrel không được dùng cùng một màu chính.
- Vật thể sắp nổ phải có cảnh báo rõ.
- Auto-aim không được bỏ qua zombie nguy hiểm chỉ để bắn crate.
- Người chơi có thể chủ động nhắm crate bằng cách đứng sao cho crate nằm trên đường bắn hoặc dùng
  cơ chế ưu tiên mục tiêu sau này.

## 10. Danh sách skill theo loại súng

Mỗi dòng dưới đây là một skill hoàn chỉnh. Cấp 1 mở skill. Cấp 5 là bản tiến hóa mạnh nhất.

## 10.1. Pistol

Pistol là súng dự phòng. Skill của Pistol cứu người chơi khi hai súng dài chưa sẵn sàng.

| Skill | Cấp 1 | Cấp 2 | Cấp 3 | Cấp 4 | Cấp 5 | Thay đổi cách chơi |
|---|---|---|---|---|---|---|
| Quickdraw | Đổi sang Pistol sẽ nạp đầy đạn. Bốn viên đầu gây thêm 40% damage | Tăng thành sáu viên | Viên đầu xuyên thêm một zombie | Kill hoàn lại một viên Quickdraw | Kill bắn thêm một viên vào mục tiêu gần đó | Dùng Pistol để xử lý nhanh mối nguy gần nhất rồi đổi lại súng chính |
| Combat Roll | Đổi sang Pistol sẽ lướt 3 m theo hướng chạy và miễn damage 0,25 giây | Lướt 4 m | Để lại bóng giả hút quái 1 giây | Tăng 20% tốc độ chạy trong 3 giây | Bóng giả phát nổ và đẩy quái | Dùng Pistol như nút thoát thân khi bị vây |
| Bounty Hunter | Đánh dấu zombie mạnh nhất trong tầm. Pistol gây thêm 25% damage lên nó | Dấu tồn tại lâu hơn | Giết mục tiêu hồi một ít máu | Mọi súng đều gây thêm damage lên mục tiêu | Elite bị giết rơi thêm pickup | Biến Pistol thành công cụ săn elite và nạp lại tài nguyên |

## 10.2. SMG

SMG mạnh khi chạy và bắn liên tục ở gần địch.

| Skill | Cấp 1 | Cấp 2 | Cấp 3 | Cấp 4 | Cấp 5 | Thay đổi cách chơi |
|---|---|---|---|---|---|---|
| Blood Rush | Trong 5 giây, damage SMG hồi máu bằng 2% damage gây ra | Kéo dài thành 6 giây | Kill tăng tốc độ chạy, cộng dồn bốn lần | Máu hồi dư đổi thành shield | Kill kéo dài Blood Rush 0,3 giây, có giới hạn | Khuyến khích lao vào đám đông để tự hồi phục |
| Smoke Runner | Khi đổi sang SMG, người chơi để lại đường khói làm chậm quái 25% | Khói rộng hơn | Người chơi chạy nhanh hơn trong khói | Quái ranged vừa vào khói tạm ngừng bắn | Quái chết trong khói kéo dài vùng khói | Dùng đường chạy để chia cắt bầy zombie rồi đổi sang súng tầm xa |
| Shredder Rounds | Đạn SMG đặt Shred, làm mục tiêu nhận thêm tối đa 10% damage từ súng kế tiếp | Tăng tối đa lên 16% | Shred lan sang mục tiêu gần đó khi zombie chết | Elite và boss nhận thêm stack | Phát đầu của súng kế tiếp tiêu thụ Shred để gây burst | Dùng SMG để làm mềm mục tiêu trước khi đổi sang súng damage lớn |

## 10.3. Assault Rifle

AR là súng đa dụng. Skill của AR có thể mở giao tranh, dọn cụm hoặc hỗ trợ khẩu tiếp theo.

| Skill | Cấp 1 | Cấp 2 | Cấp 3 | Cấp 4 | Cấp 5 | Thay đổi cách chơi |
|---|---|---|---|---|---|---|
| Hunter's Mark | Đánh dấu năm zombie. Chúng nhận thêm 15% damage | Đánh dấu bảy zombie | Kill mục tiêu trả một phần charge | Dấu chuyển sang mục tiêu mới | Mục tiêu cuối phát nổ khi chết | Chuẩn bị một nhóm mục tiêu cho Sniper, Shotgun hoặc Bomb |
| Frag Launcher | Bắn một grenade vào cụm đông nhất | Tăng bán kính và damage | Để lại vùng lửa nhỏ | Bắn hai grenade vào hai cụm | Kill bởi grenade tạo vụ nổ phụ | Cho AR một lần dọn cụm nhưng không thay súng chính |
| Combat Drone | Gọi drone hỗ trợ trong 8 giây | Drone bắn nhanh hơn | Drone đánh dấu mục tiêu | Drone chặn một projectile | Drone lao vào mục tiêu và nổ khi hết giờ | Kích hoạt bằng AR rồi đổi súng, drone vẫn tiếp tục hỗ trợ |

## 10.4. Shotgun

Shotgun phải vào gần địch. Skill của Shotgun tạo khoảng trống hoặc giúp sống sót ở cự ly gần.

| Skill | Cấp 1 | Cấp 2 | Cấp 3 | Cấp 4 | Cấp 5 | Thay đổi cách chơi |
|---|---|---|---|---|---|---|
| Breach Shield | Nhận shield bằng 20% máu tối đa trong 5 giây | Shield tăng thành 30% | Kill bằng Shotgun hồi 5% shield | Shield vỡ sẽ đẩy zombie ra xa | Damage shield hấp thụ tăng damage phát Shotgun kế tiếp | Cho phép lao vào giữa bầy quái để phản công |
| Shockwave | Đẩy zombie trong bán kính 3 m ra xa | Tăng lực đẩy | Zombie va vào vật cản bị choáng | Bán kính tăng lên 4,5 m | Phản lại projectile nhỏ | Cứu người chơi khỏi bị khóa đường và tạo lane cho súng khác |
| Gravity Buckshot | Phát đầu kéo zombie trong cone vào gần tâm | Cone rộng hơn | Zombie bị kéo nhận Stagger | Có thể kéo lệch projectile nhỏ | Tự bắn thêm một phát hút nhẹ | Gom quái để pellet trúng nhiều hơn hoặc chuẩn bị cho grenade |

## 10.5. Sniper

Sniper gây damage lớn nhưng bắn chậm. Skill của Sniper tạo thời gian và một đường bắn đẹp.

| Skill | Cấp 1 | Cấp 2 | Cấp 3 | Cấp 4 | Cấp 5 | Thay đổi cách chơi |
|---|---|---|---|---|---|---|
| Tripwire Mine | Đặt một mine làm chậm 35% trong 3 giây | Mine rộng hơn | Mine tạo dây điện làm choáng ngắn | Tồn tại hai mine cùng lúc | Quái trúng mine nhận thêm damage từ Sniper | Tạo vùng an toàn để đứng bắn |
| Tactical Backstep | Lùi 3 m, giữ hướng aim và nạp đầy Sniper | Lùi 4 m | Để lại vùng slow | Phát đầu tăng độ xuyên | Phát đầu tạo thêm một đường đạn song song | Đợi quái áp sát rồi đổi Sniper để vừa né vừa phản công |
| Decoy Beacon | Ném beacon hút tám zombie trong 3 giây | Tăng bán kính hút | Quái quanh beacon bị Marked | Hút elite trong thời gian ngắn | Phát Sniper xuyên beacon làm mục tiêu quanh đó nổ | Gom quái thành hàng để tận dụng damage xuyên |

## 10.6. LMG

LMG mạnh khi giữ cò lâu. Skill của LMG giúp người chơi giữ vị trí và sống qua lúc reload.

| Skill | Cấp 1 | Cấp 2 | Cấp 3 | Cấp 4 | Cấp 5 | Thay đổi cách chơi |
|---|---|---|---|---|---|---|
| Fortress Mode | Dựng lá chắn phía trước trong 5 giây | Lá chắn rộng hơn | Phản projectile nhỏ | Đứng sau lá chắn tăng fire rate | Lá chắn vỡ gây nổ và đẩy zombie | Chọn một lane tốt rồi đứng giữ vị trí |
| Suppression Field | Hit liên tục làm chậm tối đa 30% | Slow tồn tại lâu hơn | Đủ hit sẽ ghim zombie thường | Hiệu ứng lan sang zombie gần đó | Quái bị ghim nhận thêm damage từ súng kế tiếp | Giữ đám đông đứng yên trước khi đổi sang súng kết liễu |
| Emergency Feed | Không tốn đạn trong 4 giây | Kéo dài thành 5 giây | Fire rate tăng dần | Kill kéo dài hiệu ứng, có giới hạn | Kết thúc tạo vòng nhiệt gây damage | Tạo một đợt xả đạn lớn, sau đó buộc người chơi đổi súng |

## 11. Rarity

Rarity chỉ cộng thêm sức mạnh. Nó không thay thế mechanic chính của skill.

Rarity có thể tăng:

- Damage hoặc Skill Power.
- Bán kính.
- Thời gian.
- Số mục tiêu.
- Tốc độ nạp skill.
- Một hiệu ứng phụ nhỏ ở Epic hoặc Legendary.

Rarity không được sửa điểm yếu cốt lõi của súng.

Ví dụ:

- Sniper Legendary vẫn bắn chậm.
- Shotgun Legendary vẫn cần tới gần.
- LMG Legendary vẫn có khoảng nghỉ.

Skill phải hữu ích ngay từ Common. Legendary làm skill mạnh và đẹp hơn, không biến một skill chán
thành skill dùng được.

## 12. Ví dụ một run hoàn chỉnh

Loadout:

- Pistol chọn Combat Roll.
- Shotgun chọn Gravity Buckshot.
- Sniper chọn Decoy Beacon.

Đầu run:

1. Người chơi dùng Shotgun giết zombie và nhặt XP.
2. Lần lên cấp đầu mở Gravity Buckshot.
3. Người chơi thấy Skill Crate ở phía bên kia một nhóm zombie.
4. Họ dùng Explosive Barrel gần đó để dọn đường.
5. Vụ nổ cũng làm Player mất máu vì đứng quá gần.
6. Skill Crate rơi Upgrade Core.
7. Upgrade Core mở Decoy Beacon cho Sniper.

Giữa run:

1. Người chơi dùng Shotgun để nạp skill Sniper.
2. Icon Sniper sáng lên.
3. Người chơi đổi sang Sniper.
4. Decoy Beacon kéo quái thành cụm.
5. Sniper bắn xuyên cụm đó.
6. Zombie còn lại áp sát.
7. Người chơi đổi sang Pistol.
8. Combat Roll đưa người chơi ra khỏi vòng vây.

Cuối run:

1. Cả ba skill đạt cấp 5.
2. Level-up mới đưa Damage, Crit Chance và Move Speed.
3. Người chơi chọn Crit Chance.
4. Damage number bắt đầu xuất hiện nhiều crit hơn.
5. Boss rơi Upgrade Core.
6. Vì skill đã tối đa, Core đổi thành một lần chọn Stat Upgrade.

## 13. Nguyên tắc balance

### Skill không được chơi thay người

- Skill hỗ trợ súng.
- Skill không tự xóa cả wave quá thường xuyên.
- Skill mạnh phải cần charge lâu hơn.
- Skill phòng thủ không được tạo bất tử.

### Mỗi súng vẫn giữ điểm yếu

- Pistol an toàn nhưng damage thấp.
- SMG nhanh nhưng cần tới gần.
- AR dễ dùng nhưng không cực mạnh ở mặt nào.
- Shotgun mạnh gần nhưng nguy hiểm.
- Sniper damage lớn nhưng chậm.
- LMG bền nhưng khó di chuyển và có khoảng reload.

### Vật thể trong map phải đáng chú ý

- Skill Crate đủ giá trị để người chơi đổi đường chạy.
- Loot Crate giúp hồi phục hoặc bổ sung tài nguyên.
- Explosive Barrel có thể cứu người chơi hoặc giết họ.
- Không đặt quá nhiều crate đến mức skill lên cấp nhanh hơn XP.

### Drop phải đọc được

- XP, Coin, Gem, Health, Bomb và Upgrade Core phải có màu và hình dáng khác nhau.
- Pickup hiếm cần âm thanh riêng.
- Upgrade Core cần hiệu ứng rõ hơn các pickup thường.

## 14. Phạm vi prototype đầu tiên

Prototype đầu tiên chỉ dùng ba súng:

- Assault Rifle.
- Shotgun.
- Sniper.

Nội dung cần có:

- Chín skill của ba súng trên.
- XP Orb vật lý.
- Một Skill Crate.
- Một Loot Crate.
- Một Explosive Barrel.
- Damage dao động từ 90% đến 110%.
- Crit Chance và Crit Damage.
- Mười Stat Upgrade.
- UI charge trên ba nút súng.
- Màn chọn ba card.

Không cần làm toàn bộ 25 súng trước khi prototype này vui.

## 15. Điều kiện nghiệm thu gameplay

Prototype đạt nếu:

- Người chơi hiểu XP dùng để lên cấp.
- Người chơi nhận ra Skill Crate có giá trị cao.
- Người chơi chủ động đổi đường chạy để lấy crate.
- Người chơi nhìn HUD và biết skill súng nào đã sẵn sàng.
- Người chơi đổi súng ít nhất vài lần trong mỗi wave.
- Ba skill tạo ra ba hành động khác nhau, không chỉ ba mức tăng damage.
- Crit nhìn và nghe khác hit thường.
- Hai run cùng Loadout có thể khác nhau vì chọn skill khác.
- Sau khi ba skill đạt cấp 5, game không còn đưa card skill vô dụng.
- Pickup, XP và tiền không bị cộng hai lần.

## 16. Thứ tự triển khai đề xuất

1. Thêm `DamageResult` gồm damage cuối, có crit hay không và nguồn damage.
2. Thêm damage variance và crit cho súng.
3. Thêm Run Stat và áp các stat vào Weapon, Player và Pickup.
4. Chuyển XP từ cộng trực tiếp sang XP Orb vật lý.
5. Thêm Upgrade Core.
6. Tách Skill Crate khỏi Loot Crate.
7. Tạo Skill Definition và ba skill cho Assault Rifle.
8. Nối màn chọn card thật.
9. Thêm ba skill Shotgun.
10. Thêm ba skill Sniper.
11. Thêm charge và kích hoạt skill khi đổi súng.
12. Chạy một map thử từ đầu tới boss.
13. Chỉ sau khi vòng này vui mới mở rộng sang Pistol, SMG và LMG.
