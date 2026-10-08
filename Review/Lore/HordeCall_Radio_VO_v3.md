# HordeCall · Radio VO v3

> 256 câu, 11,730 ký tự tiếng Anh. Viết theo tính cách đã duyệt 03/10 (xem `HordeCall_Agents_Lore.md`); thay v1 và v2. Nguồn dữ liệu: `vo_lines.json`.
> Gen bằng ElevenLabs `eleven_multilingual_v2`, mp3 44.1 kHz; game tự thêm lọc bộ đàm. File = cột *File*, cue = `vo.<agent>.<cat>.<key>` (kênh Voice).

## Giọng đã chốt

| Đặc vụ | Callsign | Thị trường | Giọng ElevenLabs | voice_id | Tính cách khi đọc |
|---|---|---|---|---|---|
| Riley Hayes | NIGHTFIN | Mỹ | Zara – Warm, Real-World | `jqcCZkN6Knx8BJ5TBdYR` | Chị cả chỉ huy: tự tin, điềm, ấm. Ra lệnh bằng câu hoàn chỉnh như đang trò chuyện, không quát. Hài khô kiểu nói giảm, khen ngầm. |
| Lukas Brandt | RAPTOR | Đức / EU | Little Dude II – Cartoon Character | `fBD19tfE58bkETeiwUoC` | Gremlin thợ súng: nhỏ con, cáu kỉnh đáng yêu, mê súng tới mức kịch tính. Phẫn nộ thái quá khi ai làm sai với súng, nói chuyện với súng như thú cưng, chêm tiếng Đức. |
| Chen Long | DRAGON | Trung Quốc | Alan – Seamless Chinglish | `kWb73BrEnNOO40EVbd2k` | HLV lạnh lùng, vững như đá. Không bao giờ hào hứng; lời khen hiếm nên mới quý. Hài cực khô bằng sự thản nhiên. |
| Kaito Mizuno | SHARK | Nhật | Taro – Upbeat, Youthful | `UznIBkKIQe3ZG2tGydre` | Em út tăng động: hiếu thắng, khoe khoang, phấn khích với mọi thứ, coi tân binh là đối thủ. Thua thì chối kiểu dễ thương. |
| Kang Ji-ho | SMOG | Hàn Quốc | Joon – Calm & Friendly | `AKF7f2y1L8ktV5vxXILw` | Kỹ sư hướng nội dịu dàng: ít nói, nghĩ rồi mới nói, tốt bụng, ngại thể hiện. Hài nhẹ tự giễu, bí ẩn về cái mặt nạ, trêu mềm như bạn thân. |
| Mai Nguyen | TIGER | Việt Nam | Sapphire – Sweet, Youthful | `zmcVlqmyk3Jpn5AVYcAL` | Chị hậu cần ngọt ngào, chu đáo, tinh tế: chăm cả đội, trêu nhẹ nhàng, rất giỏi tính tiền. Vui thì cười dịu chứ không hét. |

## FTUE (P0) — theo 18 màn FTUE v2

43 câu · 2,660 ký tự

| Màn | File | Ai | Loại | Khi nào | Câu (EN) | Dịch | Giọng đọc |
|---|---|---|---|---|---|---|---|
| 01 Home | `vo_riley_ftue_home` | Riley | main | Mở Home lần đầu (chỉ có nút Play) | Recruit, this is Nightfin. You're cleared for your first deployment. Hit Play when you're ready. | Tân binh, Nightfin đây. Cậu được phép xuất kích lần đầu. Sẵn sàng thì bấm Play. | ấm, chào đón |
| 01 Home | `vo_riley_ftue_home_nudge` | Riley | nudge | Đứng ở Home 10 giây chưa bấm | It's the big button, Recruit. The one that says Play. | Là cái nút to đó, tân binh. Cái ghi chữ Play. | nói giảm, hơi buồn cười |
| 02 Move | `vo_riley_ftue_move` | Riley | main | Trận đầu bắt đầu | Drag anywhere to move. Your gun fires on its own. Just stay alive. | Kéo bất kỳ đâu để di chuyển. Súng tự bắn. Cậu chỉ cần sống sót. | rõ ràng, điềm |
| 02 Move | `vo_riley_ftue_move_nudge` | Riley | nudge | 4 giây không di chuyển | Keep moving, Recruit. Standing still is how they get you. | Di chuyển đi, tân binh. Đứng yên là bị tóm đấy. | gấp hơn, không quát |
| 02 Move | `vo_riley_ftue_move_done` | Riley | done | Đã di chuyển liên tục 3 giây | That's it. You're a natural. | Đúng rồi. Cậu có khiếu đấy. | khen nhẹ |
| 02 Move | `vo_kaito_ftue_xp` | Kaito | main | Kill đầu tiên | First kill! See the bar up top? That's your XP! | Kill đầu tiên! Thấy thanh trên cùng không? XP đấy! | phấn khích |
| 02 Move | `vo_kaito_ftue_xp_done` | Kaito | done | Kill thứ 3 (thanh XP sáng lần cuối) | Fill the bar, you level up! Easy, right? | Đầy thanh là lên cấp! Dễ mà, đúng không? | vui |
| 03 First card | `vo_chen_ftue_card` | Chen | main | Lên cấp lần đầu (không đếm giờ) | Level up. Pick one card. No timer this time. Think. | Lên cấp. Chọn một thẻ. Lần này không đếm giờ. Suy nghĩ đi. | chậm, chắc |
| 03 First card | `vo_chen_ftue_card_done` | Chen | done | Sau khi chọn thẻ đầu | One card each level. Six skills. Four stats. Then rank them up. | Mỗi cấp một thẻ. Sáu kỹ năng. Bốn chỉ số. Rồi nâng hạng. | giảng gọn |
| 04a Signal Relay | `vo_jiho_ftue_relay` | Kang | main | Thấy Signal Relay lần đầu | That's a signal relay. ...Stand in the ring for twelve seconds. I'll handle the rest. | Đó là trạm relay. ...Đứng trong vòng 12 giây. Phần còn lại để tôi. | nhẹ nhàng, có ngắt |
| 04a Signal Relay | `vo_jiho_ftue_relay_out` | Kang | nudge | Bước ra khỏi vòng khi chưa xong | You stepped out. ...It's okay, it drains slowly. Hop back in. | Cậu bước ra rồi. ...Không sao, nó tụt chậm thôi. Vào lại đi. | trấn an |
| 04a Signal Relay | `vo_jiho_ftue_relay_done` | Kang | done | Xong relay lần đầu | Uplink's up. ...HQ sent you a card. Pick one. | Kết nối xong. ...Tổng bộ gửi thẻ rồi. Chọn một cái. | hài lòng, kiệm lời |
| 04b Supply Cache | `vo_mai_ftue_cache` | Mai | main | Thấy Supply Cache lần đầu | Ooh, a supply cache. Stand on the pad, pay a few coins, and pick a card. | Ồ, kho tiếp tế. Đứng lên bệ, trả ít xu, rồi chọn một thẻ. | ngọt, hướng dẫn |
| 04b Supply Cache | `vo_mai_ftue_cache_poor` | Mai | nudge | Đứng lên bệ khi không đủ xu | Not quite enough coins yet, sweetie. Come back after a few more kills. | Chưa đủ xu đâu. Giết thêm vài con rồi quay lại nhé. | dịu dàng |
| 04b Supply Cache | `vo_mai_ftue_cache_done` | Mai | done | Mua lần đầu ở Cache | The price goes up every time you buy. Spend it wisely, okay? | Mỗi lần mua giá lại tăng. Tiêu khéo nhé? | dặn dò |
| 04c Boss Beacon | `vo_kaito_ftue_beacon` | Kaito | main | Thấy Boss Beacon lần đầu | Boss beacon! Step in and an Alpha comes for you! Only if you're ready! | Mồi gọi boss! Bước vào là Alpha tới! Sẵn sàng hãy vào! | hào hứng |
| 04c Boss Beacon | `vo_kaito_ftue_beacon_go` | Kaito | done | Bước vào Beacon lần đầu | Here it comes! Keep moving, keep moving! | Nó tới rồi! Chạy đi, chạy đi! | gấp, vui |
| 04c Boss Beacon | `vo_riley_ftue_beacon_kill` | Riley | done | Hạ Alpha đầu tiên | Alpha down. That's a first for you. Go grab the chest it dropped. | Alpha gục. Lần đầu của cậu đấy. Đi lấy cái rương nó rơi. | điềm, tự hào ngầm |
| 04d Supply Drop | `vo_mai_ftue_drop` | Mai | main | Supply Drop đầu tiên rơi xuống | Drop incoming. Stand next to the pod for a second to open it. | Hàng đang thả xuống. Đứng cạnh thùng một chút để mở. | vui nhẹ |
| 04d Supply Drop | `vo_mai_ftue_drop_done` | Mai | done | Mở pod đầu tiên | Coins and a little something extra. I packed it myself. | Xu và chút quà thêm. Chị tự tay đóng gói đấy. | ngọt |
| 04e Heal Zone | `vo_jiho_ftue_heal` | Kang | main | Thấy Heal Zone lần đầu | Med station. ...Stand in it to switch it on, then stay inside. | Trạm y tế. ...Đứng vào để bật, rồi ở yên trong đó. | dịu |
| 04e Heal Zone | `vo_jiho_ftue_heal_done` | Kang | done | Bật Heal Zone lần đầu | It's healing you now. ...Eight seconds. Don't wander off. | Nó đang hồi máu. ...Tám giây. Đừng đi lung tung. | dặn khẽ |
| 04e Heal Zone | `vo_mai_ftue_hurt` | Mai | nudge | Máu dưới 40% lần đầu (chưa gặp Heal Zone) | Oh, you're hurt. Look for a med station, sweetie. Green light, you can't miss it. | Ôi, cậu bị thương rồi. Tìm trạm y tế nhé. Đèn xanh, dễ thấy lắm. | lo lắng dịu |
| 05a Magnet | `vo_mai_ftue_magnet` | Mai | main | Nhặt Nam châm lần đầu | Ooh, a magnet. Every coin on the map is flying to you. | Ồ, nam châm. Xu khắp map đang bay về phía cậu. | vui |
| 05b Bomb | `vo_lukas_ftue_bomb` | Lukas | main | Nhặt Bom lần đầu | Bomb! Boom! Everything on screen, gone! Elites... less gone. | Bom! Bùm! Cả màn hình sạch! Elite thì... sạch ít hơn. | hào hứng rồi xìu |
| 05c Freeze | `vo_jiho_ftue_freeze` | Kang | main | Nhặt Đồng hồ băng lần đầu | Freeze clock. ...Four seconds. Make them count. | Đồng hồ băng. ...Bốn giây. Tận dụng nhé. | điềm |
| 06 First chest | `vo_chen_ftue_chest` | Chen | main | Mở rương lần đầu | First chest. Rank a power to five. Own its partner card. The next chest evolves it. | Rương đầu tiên. Nâng một kỹ năng lên hạng năm. Có thẻ đôi của nó. Rương sau sẽ tiến hoá. | chậm, rõ |
| 06 First chest | `vo_chen_ftue_chest_claim` | Chen | done | Sau câu trên, rương vẫn mở | Claim it. No rush this time. | Nhận đi. Lần này không vội. | ấm ngầm |
| 07 Revive | `vo_riley_ftue_revive` | Riley | main | Chết lần đầu (hồi sinh miễn phí) | You're down, Recruit. The first one's on HQ. Get back up. | Cậu gục rồi, tân binh. Lần đầu tổng bộ bao. Đứng dậy nào. | điềm, chắc |
| 07 Revive | `vo_riley_ftue_revive_done` | Riley | done | Sau khi hồi sinh | There you are. Let's not make that a habit. | Đây rồi. Đừng biến chuyện đó thành thói quen nhé. | nói giảm |
| 08 Result | `vo_riley_ftue_result` | Riley | main | Trận đầu kết thúc (màn kết quả) | The horde got you. It gets everyone, eventually. That was good work for day one. | Bầy quái tóm được cậu. Sớm muộn ai cũng vậy. Ngày đầu thế là tốt. | trầm, an ủi |
| 08 Result | `vo_mai_ftue_result_gift` | Mai | done | Dòng Newcomer gift hiện ra | I topped you up to four hundred coins. Go say hi to Lukas, he's been waiting. | Chị bù cho đủ 400 xu rồi. Qua chào Lukas đi, cậu ấy chờ nãy giờ. | ngọt, hào phóng |
| 09 First gun | `vo_lukas_ftue_gun` | Lukas | main | Vào Arsenal lần đầu | Finally! Makarov. Hits harder, shoots faster than that sad little pistol. You have the coins. Buy her! | Cuối cùng! Makarov. Mạnh hơn, bắn nhanh hơn khẩu pistol tội nghiệp kia. Đủ xu rồi. Mua nó đi! | hối thúc, hào hứng |
| 09 First gun | `vo_lukas_ftue_gun_done` | Lukas | done | Mua Makarov | Ja! Now you have a real gun. Be nice to her! | Ja! Giờ cậu có súng thật rồi. Đối xử tử tế với nó nhé! | tự hào kiểu bố |
| 09 First gun | `vo_lukas_ftue_gun_leave` | Lukas | nudge | Rời Arsenal mà không mua | Wait, wait! You're leaving? With all those coins? Nein! | Khoan, khoan! Cậu đi à? Với đống xu đó? Nein! | phẫn nộ hài |
| 09 First gun | `vo_mai_ftue_daily` | Mai | main | Về Home sau trận 1 (rail Daily hiện) | Daily gifts are open now. Come by every day, I'll always have something for you. | Quà hằng ngày mở rồi. Ngày nào cũng ghé nhé, chị luôn có quà cho cậu. | ngọt |
| 10 LV2 | `vo_chen_ftue_lv2` | Chen | main | Popup mở Nhiệm vụ + Pass (LV2) | Level two. Missions are open. Clear them. Earn Pass rewards. Every day. | Cấp hai. Nhiệm vụ đã mở. Làm xong. Nhận quà Pass. Mỗi ngày. | HLV |
| 10 LV2 | `vo_chen_ftue_lv2_done` | Chen | done | Mở màn Nhiệm vụ lần đầu | Three missions. No excuses. | Ba nhiệm vụ. Không lý do. | cộc |
| 11 LV3 | `vo_mai_ftue_lv3` | Mai | main | Popup mở Gacha (LV3) | Level three. The supply crates are open, and your first pull is on me. | Cấp ba. Thùng tiếp tế mở rồi, lượt đầu chị bao. | ấm, hào hứng nhẹ |
| 11 LV3 | `vo_mai_ftue_lv3_done` | Mai | done | Kết quả lượt quay đầu | Ooh, let me see. Oh, and doubles turn into shards, so nothing goes to waste. | Ồ, cho chị xem nào. À, trùng thì thành mảnh, nên không phí gì đâu. | tò mò, chu đáo |
| 12 LV5 | `vo_lukas_ftue_lv5` | Lukas | main | Popup mở nâng sao súng (LV5) | Level five! Gun stars! Shards and coins go in, damage comes out! Wunderbar! | Cấp năm! Nâng sao súng! Bỏ mảnh và xu vào, ra sát thương! Wunderbar! | phấn khích |
| 12 LV5 | `vo_lukas_ftue_lv5_done` | Lukas | done | Nâng sao lần đầu | One more star! Look at her shine! I'm not crying. Gun oil. | Thêm một sao! Nhìn nó sáng kìa! Tôi không khóc. Dầu súng thôi. | cảm động hài |
| 12 LV5 | `vo_riley_ftue_graduate` | Riley | done | Xong bước FTUE cuối | That's basic training done, Recruit. From here on, it's you and the horde. Nightfin out. | Xong huấn luyện cơ bản rồi, tân binh. Từ giờ là cậu với bầy quái. Nightfin ngắt. | trầm, tin tưởng |

## Menu / meta (P1)

28 câu · 1,226 ký tự

| File | Ai | Khi nào | Câu (EN) | Dịch | Giọng đọc |
|---|---|---|---|---|---|
| `vo_riley_meta_home_01` | Riley | Mở Home (ngẫu nhiên) | Ready when you are, Recruit. | Sẵn sàng khi cậu sẵn sàng, tân binh. | điềm |
| `vo_riley_meta_home_02` | Riley | Mở Home (ngẫu nhiên) | The board's quiet right now. I don't trust it. | Bảng đang yên. Tôi không tin nó đâu. | khô |
| `vo_riley_meta_deploy_01` | Riley | Bấm Play | Deploying you now. Keep your eyes open. | Đang thả cậu xuống. Mở to mắt nhé. | điềm |
| `vo_riley_meta_deploy_02` | Riley | Bấm Play | The drop zone's hot today. Move fast and you'll be fine. | Vùng thả hôm nay nóng. Di chuyển nhanh là ổn. | trấn an |
| `vo_kaito_meta_sector_new` | Kaito | Mở vùng mới | New sector on the map! Race you there! | Vùng mới trên bản đồ! Đua tới đó nào! | hào hứng |
| `vo_riley_meta_record` | Riley | Kỷ lục mới ở màn kết quả | New record. I'm putting that one on the board. | Kỷ lục mới. Cái này tôi ghi lên bảng. | tự hào ngầm |
| `vo_riley_meta_quit` | Riley | Bỏ trận giữa chừng | Pulling you out. We'll talk about it later. | Rút cậu ra. Chuyện này nói sau nhé. | hơi trách |
| `vo_lukas_meta_arsenal_01` | Lukas | Mở Arsenal (ngẫu nhiên) | Welcome to the Nest! Touch nothing! Okay, touch a little. | Chào mừng tới the Nest! Không đụng gì hết! Thôi, đụng chút cũng được. | hớn hở |
| `vo_lukas_meta_arsenal_02` | Lukas | Mở Arsenal (ngẫu nhiên) | Don't name your guns. You'll get attached. ...They're all Greta. | Đừng đặt tên cho súng. Sẽ quyến luyến. ...Của tôi tên Greta hết. | nói xong mới thú nhận |
| `vo_lukas_meta_gun_buy` | Lukas | Mua súng mới | Ka-chunk! Oh, that one kicks! You'll love her! | Cạch! Ôi, khẩu này giật đã lắm! Cậu sẽ mê! | khoái |
| `vo_lukas_meta_gun_legend` | Lukas | Có súng Legendary | Wunderbar! That... that is a real weapon. Give me a moment. | Wunderbar! Đó... đó mới là vũ khí. Cho tôi một phút. | xúc động |
| `vo_lukas_meta_poor` | Lukas | Không đủ xu mua súng | Not enough coins! I don't do discounts! ...Except for Mai. Once. | Không đủ xu! Tôi không giảm giá! ...Trừ Mai. Một lần. | gắt hài |
| `vo_chen_meta_missions` | Chen | Mở Nhiệm vụ | Three missions. No excuses. | Ba nhiệm vụ. Không lý do. | HLV |
| `vo_chen_meta_mission_done` | Chen | Xong nhiệm vụ | Done. Good habit. | Xong. Thói quen tốt. | gật |
| `vo_chen_meta_pass_up` | Chen | Lên cấp Pass | Pass level up. You earned it. | Pass lên cấp. Cậu xứng đáng. | gọn |
| `vo_chen_meta_acc_up` | Chen | Lên cấp tài khoản | Stronger than yesterday. That is the job. | Mạnh hơn hôm qua. Việc là vậy. | ấm ngầm |
| `vo_chen_meta_missions_empty` | Chen | Hết nhiệm vụ trong ngày | Rest is training too. Come back tomorrow. | Nghỉ cũng là tập. Mai quay lại. | ấm |
| `vo_mai_meta_shop` | Mai | Mở Shop | Fresh stock today. I saw you looking at that one. | Hôm nay có hàng mới. Chị thấy cậu nhìn cái đó rồi. | trêu nhẹ |
| `vo_mai_meta_gacha` | Mai | Mở Gacha | Feeling lucky today, sweetie? | Hôm nay thấy hên không? | tinh nghịch |
| `vo_mai_meta_gacha_legend` | Mai | Gacha ra Legendary | Oh my. A Legendary. Look at you! | Trời ơi. Legendary. Nhìn cậu kìa! | vui dịu |
| `vo_mai_meta_gacha_pity` | Mai | Gần pity (lượt 80+) | It's getting close. I can feel it. ...Probably. | Sắp rồi. Chị cảm nhận được. ...Chắc vậy. | hồi hộp |
| `vo_mai_meta_daily` | Mai | Quà ngày sẵn sàng | Your daily crate is ready. I wrapped it nicely. | Thùng quà hôm nay sẵn rồi. Chị gói đẹp lắm. | ngọt |
| `vo_mai_meta_streak7` | Mai | Chuỗi đăng nhập 7 ngày | Seven days in a row. You're family now, you know that? | Bảy ngày liền. Giờ cậu là người nhà rồi, biết không? | cảm động |
| `vo_jiho_meta_settings` | Kang | Mở Cài đặt | Settings. ...Change whatever feels right. | Cài đặt. ...Chỉnh gì thấy hợp thì chỉnh. | dịu |
| `vo_jiho_meta_studio` | Kang | Mở Studio | Fit check. ...Honestly, not bad. | Kiểm tra outfit. ...Thật lòng, không tệ. | khen khẽ |
| `vo_jiho_meta_studio_smog` | Kang | Mặc bộ Smog | Oh. ...You picked mine. Good taste. | Ồ. ...Cậu chọn bộ của tôi. Gu tốt. | ngượng, vui |
| `vo_kaito_meta_profile` | Kaito | Mở Profile | Checking my record? It's still standing! Try harder! | Xem kỷ lục của tôi à? Vẫn đứng đó! Cố lên! | khoe |
| `vo_riley_meta_comeback` | Riley | Quay lại sau 3+ ngày vắng | Welcome back, Recruit. The horde wasn't polite while you were gone. | Mừng cậu quay lại, tân binh. Lúc cậu vắng bầy quái chẳng lịch sự gì đâu. | ấm, khô |

## Sự kiện trong trận (P1)

26 câu · 960 ký tự

| File | Ai | Khi nào | Câu (EN) | Dịch | Giọng đọc |
|---|---|---|---|---|---|
| `vo_riley_run_tier_01` | Riley | Tăng cấp đe doạ | Threat's rising. They know you're here now. | Đe doạ tăng. Giờ chúng biết cậu ở đây. | trầm |
| `vo_riley_run_tier_02` | Riley | Tăng cấp đe doạ | Another tier up. Keep moving and you'll be fine. | Lên thêm bậc. Cứ di chuyển là ổn. | điềm |
| `vo_riley_run_tier_10` | Riley | Đạt cấp đe doạ 10 | Threat ten. This is where most agents call for pickup. You don't have to. | Đe doạ mười. Đến đây phần lớn đặc vụ gọi rút. Cậu thì không cần. | tin tưởng |
| `vo_kaito_run_surge_01` | Kaito | Bầy tràn tới | Here they come! So many of them! | Chúng tới rồi! Đông quá trời! | gấp |
| `vo_kaito_run_surge_02` | Kaito | Bầy tràn tới | Surge! Don't get boxed in! | Bầy tràn! Đừng để bị vây! | gấp |
| `vo_kaito_run_alpha_spawn` | Kaito | Alpha xuất hiện | Alpha on the field! Keep your distance! | Alpha trên sân! Giữ khoảng cách! | căng |
| `vo_riley_run_alpha_kill` | Riley | Hạ Alpha (sau lần đầu) | Alpha neutralized. Nicely done. Grab the chest. | Alpha đã bị hạ. Làm tốt. Lấy rương đi. | điềm |
| `vo_lukas_run_elite_01` | Lukas | Giết Elite | Big one down! Check the drop! | Con to gục rồi! Xem đồ rơi! | khoái |
| `vo_lukas_run_elite_02` | Lukas | Giết Elite | Ha! Did you see her work? Beautiful! | Ha! Thấy nó làm việc chưa? Đẹp! | tự hào về súng |
| `vo_kaito_run_kills_100` | Kaito | 100 kill trong trận | A hundred down! Keep going! | Một trăm con! Tiếp đi! | vui |
| `vo_kaito_run_kills_500` | Kaito | 500 kill trong trận | Five hundred?! Okay, okay, I'm still ahead. Probably! | Năm trăm?! Được rồi, tôi vẫn hơn. Chắc vậy! | ganh |
| `vo_kaito_run_kills_1000` | Kaito | 1000 kill trong trận | A thousand! Okay. I'm impressed. Don't tell anyone! | Một nghìn! Được. Tôi phục. Đừng kể ai nha! | lí nhí |
| `vo_chen_run_levelup` | Chen | Lên cấp trong trận (thỉnh thoảng) | Level up. Choose well. | Lên cấp. Chọn cho khéo. | HLV |
| `vo_chen_run_evolve` | Chen | Tiến hoá kỹ năng | Evolution. This is what training looks like. | Tiến hoá. Tập luyện trông như vậy đấy. | tự hào trầm |
| `vo_jiho_run_station_done` | Kang | Xong một trạm | Station's done. ...Nice. | Trạm xong. ...Tốt. | khẽ |
| `vo_jiho_run_station_done_2` | Kang | Xong một trạm | Clean work. ...I'm taking notes. | Gọn gàng. ...Tôi đang ghi lại đấy. | khen khẽ |
| `vo_mai_run_lowhp_01` | Mai | Máu dưới 25% | You're hurt, sweetie. Find a med station, okay? | Cậu bị thương rồi. Tìm trạm y tế nhé? | lo |
| `vo_mai_run_lowhp_02` | Mai | Máu dưới 25% | Hey. Please be careful. I mean it. | Này. Cẩn thận giùm chị. Chị nói thật đấy. | nghiêm hiếm hoi |
| `vo_mai_run_magnet` | Mai | Nhặt nam châm (sau lần đầu) | Coins on the way to you. | Xu đang về với cậu. | vui nhẹ |
| `vo_lukas_run_bomb` | Lukas | Nhặt bom (sau lần đầu) | Boom! Wunderbar! | Bùm! Wunderbar! | khoái |
| `vo_jiho_run_freeze` | Kang | Nhặt đồng hồ băng (sau lần đầu) | Frozen. ...Go. | Đóng băng rồi. ...Đi. | gọn |
| `vo_riley_run_best_time` | Riley | Vượt thời gian kỷ lục | You just beat your best time. Every second from here is a new record. | Cậu vừa vượt kỷ lục. Mỗi giây từ giờ là kỷ lục mới. | khích lệ |
| `vo_riley_run_death_01` | Riley | Chết (sau lần đầu) | You're down, Recruit. | Cậu gục rồi, tân binh. | trầm |
| `vo_riley_run_death_02` | Riley | Chết (sau lần đầu) | You're down. Your call, Recruit. | Cậu gục rồi. Cậu quyết, tân binh. | trầm |
| `vo_riley_run_end_01` | Riley | Kết thúc trận | Deployment's over. That's good data, Recruit. | Hết đợt triển khai. Dữ liệu tốt đấy, tân binh. | điềm |
| `vo_riley_run_end_02` | Riley | Kết thúc trận | Get some rest. We go again soon. | Nghỉ ngơi đi. Lát nữa mình đi tiếp. | ấm |

## Tình huống + chê/khen theo hành vi (P1)

16 câu · 913 ký tự

| File | Ai | Khi nào | Câu (EN) | Dịch | Giọng đọc |
|---|---|---|---|---|---|
| `vo_riley_sit_still` | Riley | Đứng yên 6 giây giữa trận | You stopped moving, Recruit. They didn't. | Cậu dừng lại, tân binh. Chúng thì không. | khô |
| `vo_mai_sit_hoard` | Mai | Có 2000+ xu chưa tiêu khi vào trận lần thứ 3 | You're sitting on a lot of coins, sweetie. Lukas keeps staring at them. | Cậu ôm nhiều xu quá đấy. Lukas cứ nhìn chằm chằm. | trêu nhẹ |
| `vo_mai_sit_skip_drop` | Mai | Bỏ qua Supply Drop (pod tự biến mất) | You walked right past my drop. I packed that one myself, you know. | Cậu đi ngang thùng của chị luôn. Chị tự tay đóng gói đấy. | dỗi dịu |
| `vo_kaito_sit_skip_beacon` | Kaito | Đi ngang Boss Beacon 3 lần không vào | Scared of the beacon? It's fine! I was too! For one day! | Sợ beacon à? Không sao! Tôi cũng từng sợ! Một ngày thôi! | khiêu khích |
| `vo_chen_sit_early_death` | Chen | Chết trước phút 1 | Under a minute. Again. Slower this time. | Chưa tới một phút. Lại. Lần này chậm thôi. | nghiêm, không gắt |
| `vo_riley_sit_early_death_2` | Riley | Chết trước phút 1 hai trận liền | Twice in a row. Take a breath, Recruit. Then we'll try again. | Hai trận liền. Hít một hơi đi, tân binh. Rồi mình thử lại. | quan tâm |
| `vo_chen_sit_no_hit_2m` | Chen | 2 phút không mất máu | Two minutes. No hits. That is not luck. That is footwork. | Hai phút. Không trúng đòn. Không phải may. Là bộ pháp. | khen hiếm |
| `vo_riley_sit_bridge` | Riley | Đứng trên cầu khi bầy đông | Careful on the bridge. Choke points work for them, not for you. | Cẩn thận trên cầu. Điểm nghẽn có lợi cho chúng, không phải cậu. | chiến thuật |
| `vo_jiho_sit_basin` | Kang | Đứng sát vùng lõm chất lỏng lâu | Please don't touch the goo. ...I have a list of reasons. | Làm ơn đừng chạm vào chất nhờn. ...Tôi có cả danh sách lý do. | nhẹ nhàng |
| `vo_mai_sit_long_run` | Mai | Trận kéo dài quá 15 phút | Fifteen minutes. I'm warming up your snacks now. | Mười lăm phút rồi. Chị đang hâm đồ ăn cho cậu. | ngọt |
| `vo_chen_sit_reroll` | Chen | Không chọn thẻ, để hết giờ tự chọn | You let the timer choose. The timer does not care about you. I do. | Cậu để đồng hồ chọn. Đồng hồ không quan tâm cậu. Tôi thì có. | ấm ngầm |
| `vo_chen_sit_all_stats` | Chen | Đầy 4 ô chỉ số mà chỉ 1 kỹ năng | All stats. No skills. Muscle without technique. | Toàn chỉ số. Không kỹ năng. Cơ bắp không có kỹ thuật. | chê nhẹ |
| `vo_lukas_sit_same_gun` | Lukas | Dùng Pistol 10 trận liền dù có súng khác | Still the pistol?! She's tired! Let her rest! Take Greta! | Vẫn khẩu pistol?! Nó mệt rồi! Cho nó nghỉ! Lấy Greta đi! | phẫn nộ hài |
| `vo_jiho_sit_afk_home` | Kang | Để yên ở Home 60 giây | You've been looking at the menu for a while. ...Me too, honestly. | Cậu nhìn menu lâu rồi đấy. ...Thật ra tôi cũng vậy. | đồng cảm |
| `vo_jiho_sit_full_hp_heal` | Kang | Bật Heal Zone khi máu đầy | Full health... using a med station. ...Bold. I respect it. | Máu đầy... dùng trạm y tế. ...Táo bạo. Tôi tôn trọng. | trêu mềm |
| `vo_riley_sit_comeback_win` | Riley | Hồi sinh rồi trụ thêm hơn 3 phút | Most agents quit after the first fall. You didn't. I noticed. | Phần lớn đặc vụ bỏ cuộc sau lần gục đầu. Cậu thì không. Tôi để ý đấy. | trầm, tôn trọng |

## Lore: briefing vùng, Alpha, tổ chức (P1)

18 câu · 1,243 ký tự

| File | Ai | Khi nào | Câu (EN) | Dịch | Giọng đọc |
|---|---|---|---|---|---|
| `vo_riley_lore_s1_brief` | Riley | Lần đầu vào Greenbelt | Greenbelt. The first infected sector. They're dense but slow, so it's a good place to learn. | Greenbelt. Vùng nhiễm đầu tiên. Đông nhưng chậm, nên là chỗ tốt để học. | briefing |
| `vo_kaito_lore_s2_brief` | Kaito | Lần đầu vào Deepwood | Deepwood! Trees everywhere, and elites hiding in them! I counted! Too many! | Deepwood! Cây khắp nơi, Elite núp trong đó! Tôi đếm rồi! Nhiều lắm! | briefing hào hứng |
| `vo_jiho_lore_s3_brief` | Kang | Lần đầu vào Blackmire | Blackmire. ...Narrow paths between toxic pools. Always know where the bridge is. | Blackmire. ...Đường hẹp giữa các vũng độc. Luôn biết cây cầu ở đâu. | briefing dịu |
| `vo_lukas_lore_s4_brief` | Lukas | Lần đầu vào Cinder Ridge | Cinder Ridge! Hot ground! Hot guns! And something big is digging under the lava! | Cinder Ridge! Đất nóng! Súng nóng! Và có thứ gì to đang đào dưới dung nham! | briefing kịch tính |
| `vo_riley_lore_s5_brief` | Riley | Lần đầu vào Whiteout | Whiteout. The worst infection we know of. Visibility's low, so stay close to the radio. | Whiteout. Vùng nhiễm nặng nhất từng biết. Tầm nhìn thấp, nên bám sát bộ đàm. | briefing nghiêm |
| `vo_kaito_lore_a_thorn` | Kaito | Alpha Thornback xuất hiện lần đầu | That's Thornback! Plant-type Alpha! Don't hug it! Seriously! | Đó là Thornback! Alpha dạng cây! Đừng ôm nó! Thật đấy! | căng, đùa |
| `vo_kaito_lore_a_burrow` | Kaito | Alpha Burrow King xuất hiện lần đầu | Burrow King! It comes up right under you! Don't stand still! | Burrow King! Nó trồi lên ngay dưới chân! Đừng đứng yên! | gấp |
| `vo_riley_lore_a_bone` | Riley | Alpha Bone Colossus xuất hiện lần đầu | Bone Colossus. It's the biggest thing we've ever logged. Don't let it corner you. | Bone Colossus. Thứ to nhất từng ghi nhận. Đừng để nó dồn cậu vào góc. | trầm, nặng |
| `vo_riley_lore_why_us` | Riley | Ngẫu nhiên ở Home (hiếm) | When the horde comes, people make a call. We're the ones who pick up. | Khi bầy quái kéo tới, người ta gọi. Mình là người nhấc máy. | trầm, tự hào |
| `vo_chen_lore_powers` | Chen | Ngẫu nhiên ở Home (hiếm) | Where do the powers come from. Does not matter. Use them well. | Sức mạnh từ đâu ra. Không quan trọng. Dùng cho tốt. | dứt khoát |
| `vo_jiho_lore_the_mask` | Kang | Ngẫu nhiên ở Studio (hiếm) | The mask filters the air. ...And people. Mostly people. | Mặt nạ lọc không khí. ...Và cả người. Chủ yếu là người. | tự giễu |
| `vo_mai_lore_credits` | Mai | Ngẫu nhiên ở Shop (hiếm) | Every coin you bring back keeps a supply line open somewhere. | Mỗi đồng xu cậu mang về giữ cho một tuyến tiếp tế ở đâu đó còn chạy. | chân thành |
| `vo_riley_lore_nightfin` | Riley | Ngẫu nhiên ở Home (hiếm) | I used to do night rescues. On radar, I was just a little fin in the dark. | Tôi từng cứu hộ ban đêm. Trên radar, tôi chỉ là một chiếc vây nhỏ trong bóng tối. | hoài niệm |
| `vo_lukas_lore_raptor` | Lukas | Ngẫu nhiên ở Arsenal (hiếm) | Why Raptor? Raptors are small, loud, and correct! Like me! | Sao lại Raptor? Vì khủng long raptor nhỏ, ồn, và luôn đúng! Như tôi! | tự hào |
| `vo_chen_lore_dragon` | Chen | Ngẫu nhiên ở Nhiệm vụ (hiếm) | Kaito named me Dragon. I did not agree. It stayed. | Kaito đặt tên tôi là Dragon. Tôi không đồng ý. Nó vẫn ở lại. | khô |
| `vo_kaito_lore_shark` | Kaito | Ngẫu nhiên ở Profile (hiếm) | Why Shark? Because sharks are cool! ...Not because of Riley! Shut up! | Sao lại Shark? Vì cá mập ngầu! ...Không phải vì Riley đâu! Im đi! | chối đáng yêu |
| `vo_jiho_lore_smog` | Kang | Ngẫu nhiên ở Cài đặt (hiếm) | They call me Smog because of a week in a bad district. ...It's accurate. | Họ gọi tôi là Smog vì một tuần ở khu nhiễm khói. ...Nghe cũng đúng. | thản nhiên |
| `vo_mai_lore_tiger` | Mai | Ngẫu nhiên ở Shop (hiếm) | Why Tiger? I'm very sweet. Until somebody wastes supplies. | Sao lại Tiger? Chị ngọt lắm. Cho tới khi có ai lãng phí đồ tiếp tế. | ngọt mà có móng |

## Bộ đàm ngẫu nhiên trong trận (P2)

31 câu · 1,686 ký tự

| File | Ai | Khi nào | Câu (EN) | Dịch | Giọng đọc |
|---|---|---|---|---|---|
| `vo_riley_chat_01` | Riley | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | HQ pays per kill. Please don't ask me who set the rates. | Tổng bộ trả theo đầu quái. Làm ơn đừng hỏi tôi ai đặt giá. | lore |
| `vo_riley_chat_02` | Riley | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | Every sector we clear, somebody gets to go home. Keep that in mind. | Mỗi vùng mình dọn, có người được về nhà. Nhớ điều đó nhé. | deep |
| `vo_riley_chat_03` | Riley | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | Status check. You're alive. That's a great report. | Kiểm tra tình hình. Cậu còn sống. Báo cáo tuyệt vời. | funny |
| `vo_riley_chat_04` | Riley | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | Nobody knows where the outbreak started. We just know where it's heading. | Không ai biết dịch bắt đầu từ đâu. Mình chỉ biết nó đang lan tới đâu. | lore |
| `vo_riley_chat_05` | Riley | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | Mai wants me to tell you she's proud of you. I'm only relaying. | Mai muốn tôi nói là cô ấy tự hào về cậu. Tôi chỉ chuyển lời thôi. | cute |
| `vo_riley_chat_06` | Riley | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | Don't fight the whole horde. Just win the next ten seconds. | Đừng đánh cả bầy. Chỉ cần thắng mười giây tiếp theo. | deep |
| `vo_lukas_chat_01` | Lukas | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | Hear that sound? A well-kept weapon! Music! | Nghe tiếng đó không? Súng được chăm kỹ! Âm nhạc đấy! | funny |
| `vo_lukas_chat_02` | Lukas | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | If it's green and it moves, shoot it! If it's green and it doesn't move... shoot it anyway! | Xanh mà động thì bắn! Xanh mà không động... thì cứ bắn! | funny |
| `vo_lukas_chat_03` | Lukas | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | In Hamburg I fixed fishing boats. Now I fix guns. Same smell! Better company! | Ở Hamburg tôi sửa thuyền cá. Giờ sửa súng. Cùng mùi! Bạn đồng hành tốt hơn! | lore |
| `vo_lukas_chat_04` | Lukas | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | I talk to my guns at night. They listen better than Kaito! | Tối tôi nói chuyện với súng. Chúng nghe lời hơn Kaito! | cute |
| `vo_lukas_chat_05` | Lukas | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | Your aim is fine! The gun does all the work, but fine! | Ngắm vậy là được! Súng làm hết việc, nhưng mà được! | critique |
| `vo_chen_chat_01` | Chen | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | Breathe. Move. Shoot. Repeat. | Thở. Di chuyển. Bắn. Lặp lại. | deep |
| `vo_chen_chat_02` | Chen | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | Your footwork is better. I noticed. | Bộ pháp khá hơn rồi. Tôi để ý. | deep |
| `vo_chen_chat_03` | Chen | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | I used to box. Mutants don't follow rules either. Fair. | Tôi từng đấm bốc. Bọn quái cũng chẳng theo luật. Công bằng. | lore |
| `vo_chen_chat_04` | Chen | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | Strong is not hitting hard. Strong is getting up. | Mạnh không phải đánh mạnh. Mạnh là đứng dậy được. | deep |
| `vo_chen_chat_05` | Chen | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | Mai says I should smile more on the radio. This is me smiling. | Mai bảo tôi nên cười nhiều hơn trên bộ đàm. Đây là tôi đang cười. | funny |
| `vo_kaito_chat_01` | Kaito | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | It's quiet. Too quiet! I hate it! Make some noise! | Yên quá. Yên quá trời! Ghét ghê! Làm ồn lên đi! | funny |
| `vo_kaito_chat_02` | Kaito | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | They look cute until they bite! ...Okay, still kind of cute. | Trông dễ thương tới khi chúng cắn! ...Được rồi, vẫn hơi dễ thương. | cute |
| `vo_kaito_chat_03` | Kaito | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | Bet I'd be at five hundred by now! Just saying! | Cá là tôi đã năm trăm rồi! Nói vậy thôi! | critique |
| `vo_kaito_chat_04` | Kaito | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | Scouted the next sector! It's worse! You'll love it! | Vừa trinh sát vùng kế! Tệ hơn! Cậu sẽ thích! | lore |
| `vo_kaito_chat_05` | Kaito | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | Ji-ho bet me lunch you'd last ten minutes! Don't make me pay! | Ji-ho cá với tôi bữa trưa là cậu trụ được mười phút! Đừng bắt tôi trả! | funny |
| `vo_jiho_chat_01` | Kang | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | Relay signal's clean. ...For now. | Tín hiệu relay sạch. ...Tạm thời. | lore |
| `vo_jiho_chat_02` | Kang | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | It smells worse than yesterday. ...Somehow. | Mùi tệ hơn hôm qua. ...Không hiểu sao. | funny |
| `vo_jiho_chat_03` | Kang | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | Note to self... never touch the goo. Again. | Tự nhắc... không bao giờ chạm vào chất nhờn. Lần nữa. | funny |
| `vo_jiho_chat_04` | Kang | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | Machines break. People too. ...Both can be fixed. | Máy hỏng được. Người cũng vậy. ...Cả hai đều sửa được. | deep |
| `vo_jiho_chat_05` | Kang | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | Kaito asked if the mask is for the smell. ...It is now. | Kaito hỏi mặt nạ có phải để chống mùi không. ...Giờ thì phải. | cute |
| `vo_mai_chat_01` | Mai | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | I packed snacks for when you're back. So come back, okay? | Chị chuẩn bị đồ ăn cho lúc cậu về rồi. Nên nhớ về nhé? | cute |
| `vo_mai_chat_02` | Mai | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | Grab the coins, sweetie. I'm counting. Out loud. | Nhặt xu đi. Chị đang đếm. Thành tiếng. | funny |
| `vo_mai_chat_03` | Mai | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | This sector is so pretty. Shame about the mutants. | Vùng này đẹp ghê. Tiếc là có quái. | lore |
| `vo_mai_chat_04` | Mai | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | Supply runs are boring. That's the point. Boring means everyone eats. | Tiếp tế thì chán. Chính là vậy. Chán nghĩa là ai cũng có ăn. | deep |
| `vo_mai_chat_05` | Mai | Ngẫu nhiên trong trận, lúc yên (cooldown 60–90 s) | You took a hit. I saw. ...Everyone saw, sweetie. | Cậu trúng đòn rồi. Chị thấy. ...Cả đội thấy đấy. | critique |

## Hội thoại giữa đặc vụ (P2)

68 câu · 1,814 ký tự


**01 · Đếm kill** — Trong trận, yên tĩnh, sau phút 3 · _funny_

| File | Ai | Câu | Dịch |
|---|---|---|---|
| `vo_dlg_01_1_kaito` | Kaito | Ji-ho! How many today? | Ji-ho! Hôm nay được bao nhiêu? |
| `vo_dlg_01_2_jiho` | Kang | ...Didn't count. | ...Không đếm. |
| `vo_dlg_01_3_kaito` | Kaito | So, less than me! | Vậy là ít hơn tôi! |
| `vo_dlg_01_4_jiho` | Kang | ...Three hundred twelve. | ...Ba trăm mười hai. |

**02 · Khẩu pistol** — Trong trận, đang dùng Pistol · _funny_

| File | Ai | Câu | Dịch |
|---|---|---|---|
| `vo_dlg_02_1_lukas` | Lukas | Chen! Your recruit is holding my pistol wrong! | Chen! Tân binh của anh cầm sai khẩu pistol của tôi! |
| `vo_dlg_02_2_chen` | Chen | My recruit is alive. Your pistol is fine. | Tân binh của tôi còn sống. Khẩu pistol của anh vẫn ổn. |

**03 · Đồ ăn trong thùng** — Sau khi mở Supply Drop · _cute_

| File | Ai | Câu | Dịch |
|---|---|---|---|
| `vo_dlg_03_1_mai` | Mai | Boss, can I put snacks in the drops? | Boss, em bỏ đồ ăn vặt vào thùng thả được không? |
| `vo_dlg_03_2_riley` | Riley | Drops are for ammo, Mai. | Thùng thả là để đựng đạn, Mai. |
| `vo_dlg_03_3_mai` | Mai | Ammo doesn't taste very good, though. | Nhưng đạn đâu có ngon. |

**04 · Cái mặt nạ** — Home, để yên 20 giây · _funny_

| File | Ai | Câu | Dịch |
|---|---|---|---|
| `vo_dlg_04_1_mai` | Mai | Ji-ho, do you ever take that mask off? | Ji-ho, em có bao giờ tháo mặt nạ không? |
| `vo_dlg_04_2_jiho` | Kang | ...No. | ...Không. |
| `vo_dlg_04_3_mai` | Mai | Not even to eat? | Ăn cũng không à? |
| `vo_dlg_04_4_jiho` | Kang | ...Next question. | ...Câu tiếp theo. |

**05 · Súng to nhất** — Arsenal, xem súng Legendary · _funny_

| File | Ai | Câu | Dịch |
|---|---|---|---|
| `vo_dlg_05_1_kaito` | Kaito | Lukas! I want the biggest gun you've got! | Lukas! Tôi muốn khẩu to nhất anh có! |
| `vo_dlg_05_2_lukas` | Lukas | Nein! You'll fall over! | Nein! Cậu sẽ ngã! |
| `vo_dlg_05_3_kaito` | Kaito | Worth it! | Đáng mà! |

**06 · Whiteout** — Mở vùng 5 · _lore_

| File | Ai | Câu | Dịch |
|---|---|---|---|
| `vo_dlg_06_1_riley` | Riley | Threat climbs faster in Whiteout. We need a plan. | Ở Whiteout đe doạ tăng nhanh hơn. Mình cần kế hoạch. |
| `vo_dlg_06_2_chen` | Chen | Then we train harder. | Vậy thì tập chăm hơn. |
| `vo_dlg_06_3_riley` | Riley | Or we send the recruit. | Hoặc gửi tân binh đi. |
| `vo_dlg_06_4_chen` | Chen | ...Same thing. | ...Cũng như nhau. |

**07 · Ngày đầu** — Sau trận thứ 3 của người chơi · _cute_

| File | Ai | Câu | Dịch |
|---|---|---|---|
| `vo_dlg_07_1_mai` | Mai | The new one's doing really well, don't you think? | Bạn mới làm tốt lắm, đúng không? |
| `vo_dlg_07_2_kaito` | Kaito | Better than my first day! | Hơn ngày đầu của tôi! |
| `vo_dlg_07_3_mai` | Mai | You cried on your first day, sweetie. | Ngày đầu em khóc mà. |
| `vo_dlg_07_4_kaito` | Kaito | Wind in my eyes! | Gió bay vào mắt! |

**08 · Ống ngắm** — Nâng sao súng · _funny_

| File | Ai | Câu | Dịch |
|---|---|---|---|
| `vo_dlg_08_1_jiho` | Kang | I could add a scope to that. ...If you want. | Tôi gắn ống ngắm vào được. ...Nếu anh muốn. |
| `vo_dlg_08_2_lukas` | Lukas | She has a scope! | Nó có ống ngắm rồi! |
| `vo_dlg_08_3_jiho` | Kang | ...A better one. | ...Cái tốt hơn. |
| `vo_dlg_08_4_lukas` | Lukas | ...Show me. Right now. | ...Cho xem. Ngay bây giờ. |

**09 · Hạ Alpha đầu tiên** — Lần đầu hạ boss · _deep_

| File | Ai | Câu | Dịch |
|---|---|---|---|
| `vo_dlg_09_1_kaito` | Kaito | Did you see that?! The recruit took down an Alpha! | Thấy không?! Tân binh hạ được Alpha rồi! |
| `vo_dlg_09_2_riley` | Riley | I saw it. Put it in the log. | Thấy rồi. Ghi vào sổ. |
| `vo_dlg_09_3_chen` | Chen | Now do it again. | Giờ làm lại lần nữa. |

**10 · Bảy ngày** — Chuỗi đăng nhập 7 ngày · _cute_

| File | Ai | Câu | Dịch |
|---|---|---|---|
| `vo_dlg_10_1_mai` | Mai | Seven days in a row. That's lovely. | Bảy ngày liên tục. Dễ thương ghê. |
| `vo_dlg_10_2_chen` | Chen | Discipline. | Kỷ luật. |
| `vo_dlg_10_3_mai` | Mai | I was going to say snacks. | Chị định nói là đồ ăn vặt. |

**11 · Ai đặt tên trạm** — Trong trận, sau khi xong relay thứ 3 · _lore_

| File | Ai | Câu | Dịch |
|---|---|---|---|
| `vo_dlg_11_1_kaito` | Kaito | Who named it Signal Relay? So boring! | Ai đặt tên Signal Relay vậy? Chán òm! |
| `vo_dlg_11_2_jiho` | Kang | ...I did. | ...Tôi. |
| `vo_dlg_11_3_kaito` | Kaito | Great name! Classic! Love it! | Tên hay! Kinh điển! Thích lắm! |

**12 · Riley có ngủ không** — Home, sau 22:00 giờ máy · _deep_

| File | Ai | Câu | Dịch |
|---|---|---|---|
| `vo_dlg_12_1_mai` | Mai | Boss, do you ever sleep? | Boss, chị có ngủ bao giờ không? |
| `vo_dlg_12_2_riley` | Riley | When the board is clear. | Khi bảng sạch. |
| `vo_dlg_12_3_mai` | Mai | The board is never clear. | Bảng có bao giờ sạch đâu. |
| `vo_dlg_12_4_riley` | Riley | Then you have your answer, Mai. | Vậy là em có câu trả lời rồi, Mai. |

**13 · Greta** — Arsenal, mua súng thứ 3 · _funny_

| File | Ai | Câu | Dịch |
|---|---|---|---|
| `vo_dlg_13_1_kaito` | Kaito | Lukas, what did you name the shotgun? | Lukas, anh đặt tên khẩu shotgun là gì? |
| `vo_dlg_13_2_lukas` | Lukas | Greta! | Greta! |
| `vo_dlg_13_3_kaito` | Kaito | And the sniper? | Còn khẩu bắn tỉa? |
| `vo_dlg_13_4_lukas` | Lukas | Also Greta! She knows! | Cũng Greta! Nó biết mà! |

**14 · Quái có dễ thương không** — Trong trận, sau surge đầu tiên · _cute_

| File | Ai | Câu | Dịch |
|---|---|---|---|
| `vo_dlg_14_1_mai` | Mai | Some of them are honestly kind of cute. | Thật ra có mấy con dễ thương ghê. |
| `vo_dlg_14_2_chen` | Chen | Focus. | Tập trung. |
| `vo_dlg_14_3_mai` | Mai | The round one, with the tiny feet. | Con tròn tròn, có chân bé xíu kìa. |
| `vo_dlg_14_4_chen` | Chen | ...Shoot the round one. | ...Bắn con tròn đó. |

**15 · Chen khen** — Kết thúc trận trụ quá 10 phút · _deep_

| File | Ai | Câu | Dịch |
|---|---|---|---|
| `vo_dlg_15_1_kaito` | Kaito | Chen! Say something nice for once! | Chen! Nói gì đó tử tế một lần đi! |
| `vo_dlg_15_2_chen` | Chen | ...Acceptable. | ...Chấp nhận được. |
| `vo_dlg_15_3_kaito` | Kaito | That's the nicest thing he's ever said! | Đó là câu tử tế nhất anh ấy từng nói! |

**16 · Cà phê** — Home, buổi sáng (6:00–10:00 giờ máy) · _cute_

| File | Ai | Câu | Dịch |
|---|---|---|---|
| `vo_dlg_16_1_mai` | Mai | Coffee, anyone? Vietnamese style. Very strong. | Có ai uống cà phê không? Kiểu Việt. Rất đậm. |
| `vo_dlg_16_2_lukas` | Lukas | How strong? | Đậm cỡ nào? |
| `vo_dlg_16_3_mai` | Mai | Ji-ho had one and fixed three relays in an hour. | Ji-ho uống một ly và sửa xong ba relay trong một tiếng. |
| `vo_dlg_16_4_jiho` | Kang | ...I don't remember that hour. | ...Tôi không nhớ gì về tiếng đó. |

**17 · Tân binh là ai** — Home, sau 10 trận · _deep_

| File | Ai | Câu | Dịch |
|---|---|---|---|
| `vo_dlg_17_1_kaito` | Kaito | Riley, where did you find this recruit? | Riley, chị tìm đâu ra tân binh này vậy? |
| `vo_dlg_17_2_riley` | Riley | They found us, actually. | Thật ra là họ tìm tới mình. |
| `vo_dlg_17_3_kaito` | Kaito | That's not an answer! | Đó đâu phải câu trả lời! |
| `vo_dlg_17_4_riley` | Riley | It's the only one that matters, Kaito. | Đó là câu duy nhất quan trọng, Kaito. |

**18 · Con mèo** — Home, ngẫu nhiên (hiếm) · _cute_

| File | Ai | Câu | Dịch |
|---|---|---|---|
| `vo_dlg_18_1_mai` | Mai | Chen, someone's been feeding the hangar cat. | Chen, có ai đó cho con mèo ở Hangar ăn. |
| `vo_dlg_18_2_chen` | Chen | Not me. | Không phải tôi. |
| `vo_dlg_18_3_mai` | Mai | It sleeps on your jacket. | Nó ngủ trên áo khoác của anh. |
| `vo_dlg_18_4_chen` | Chen | ...It is a good cat. | ...Nó là con mèo ngoan. |

**19 · Sợ Chen** — Arsenal, ngẫu nhiên (hiếm) · _funny_

| File | Ai | Câu | Dịch |
|---|---|---|---|
| `vo_dlg_19_1_kaito` | Kaito | Lukas, are you scared of Chen? | Lukas, anh sợ Chen hả? |
| `vo_dlg_19_2_lukas` | Lukas | Nein! I respect him! From far away! | Nein! Tôi kính trọng anh ấy! Từ xa! |
| `vo_dlg_19_3_chen` | Chen | I can hear you. | Tôi nghe thấy đấy. |
| `vo_dlg_19_4_lukas` | Lukas | ...From very far away. | ...Từ rất xa. |

## Easter egg (P2)

16 câu · 799 ký tự

| File | Ai | Khi nào | Câu (EN) | Dịch | Giọng đọc |
|---|---|---|---|---|---|
| `vo_jiho_egg_tap5` | Kang | Chạm chân dung bộ đàm 5 lần | You keep poking the radio. ...It tickles. Please stop. | Cậu cứ chọc bộ đàm. ...Nhột lắm. Thôi đi mà. | ngượng |
| `vo_mai_egg_tap10` | Mai | Chạm chân dung 10 lần | Okay, okay. Here, have a snack. Now go fight, sweetie. | Rồi rồi. Đây, ăn vặt nè. Giờ đi đánh đi. | nhượng bộ đáng yêu |
| `vo_riley_egg_night3am` | Riley | Mở game 2:00–4:00 sáng giờ máy | Three in the morning, Recruit? ...Alright. I'm up too. | Ba giờ sáng hả tân binh? ...Thôi được. Tôi cũng đang thức. | mệt, ấm |
| `vo_jiho_egg_kills_404` | Kang | Đúng 404 kill khi kết thúc trận | Four oh four. ...Recruit not found. | Bốn không bốn. ...Không tìm thấy tân binh. | mặt lạnh |
| `vo_kaito_egg_kills_1337` | Kaito | Đúng 1337 kill | One three three seven?! That's actually elite! | Một ba ba bảy?! Cái này đúng là elite! | phấn khích |
| `vo_riley_egg_name_agent` | Riley | Đặt tên người chơi trùng tên một đặc vụ | Nice try. There's only one of me, Recruit. | Cố lắm. Chỉ có một tôi thôi, tân binh. | khô |
| `vo_mai_egg_tet` | Mai | Tết Nguyên Đán (theo lịch) | Happy Tết! There's lucky money in the Shop. Okay, it's coins. But they're red! | Chúc mừng năm mới! Có lì xì trong Shop nhé. À, là xu. Nhưng màu đỏ! | rộn ràng |
| `vo_chen_egg_lunar` | Chen | Tết Âm lịch (theo lịch) | New year. New you. Same training. | Năm mới. Bạn mới. Bài tập cũ. | khô |
| `vo_lukas_egg_oktober` | Lukas | Cuối tháng 9 – đầu tháng 10 (Oktoberfest) | Oktoberfest! I'm celebrating! I cleaned every Greta twice! | Oktoberfest! Tôi đang ăn mừng! Lau mỗi Greta hai lần! | hớn hở |
| `vo_riley_egg_july4` | Riley | Ngày 4/7 | Fireworks tonight. Ours are louder, though. | Tối nay có pháo hoa. Nhưng của mình to hơn. | khô vui |
| `vo_jiho_egg_chuseok` | Kang | Tết Trung thu Hàn (Chuseok) | Chuseok. ...Family's far away. The team will do. | Chuseok. ...Nhà thì xa. Có đội là được. | mềm |
| `vo_kaito_egg_goldenweek` | Kaito | Golden Week (29/4 – 5/5) | Golden Week! Everyone's on holiday! Except us! And them! | Golden Week! Ai cũng nghỉ! Trừ mình! Và bọn chúng! | vui |
| `vo_kaito_egg_idle_2m` | Kaito | Để yên ở Home 2 phút | Hello? Recruit? ...I'm counting this as a win! | Alô? Tân binh? ...Tôi tính đây là tôi thắng nha! | lém |
| `vo_lukas_egg_die_at_kill1` | Lukas | Chết khi chỉ có 0–1 kill | One kill?! I've seen gun cleaning take longer! | Một kill?! Tôi lau súng còn lâu hơn! | sốc hài |
| `vo_mai_egg_gacha_10x` | Mai | Quay gacha 10 lần không ra gì mới | The crates don't like you today. Tomorrow they will. ...Probably. | Hôm nay thùng không thương cậu. Mai nó thương. ...Chắc vậy. | an ủi |
| `vo_chen_egg_cat` | Chen | Chạm vào con mèo trong Home (nếu có) | That cat is not mine. ...Her name is Bao. | Con mèo đó không phải của tôi. ...Nó tên Bao. | lộ |

## Thành tựu (P3 — chưa có hệ thống)

10 câu · 429 ký tự

| File | Ai | Khi nào | Câu (EN) | Dịch | Giọng đọc |
|---|---|---|---|---|---|
| `vo_riley_ach_first_alpha` | Riley | Hạ Alpha đầu tiên | Your first Alpha. It won't be your last. | Alpha đầu tiên của cậu. Sẽ không phải cuối cùng. | trầm |
| `vo_kaito_ach_run_1000` | Kaito | 1000 kill trong một trận | A thousand in one run?! Okay. Rival. Official. | Một nghìn trong một trận?! Được. Đối thủ. Chính thức. | ganh vui |
| `vo_chen_ach_survive_10` | Chen | Trụ 10 phút | Ten minutes. Now twenty. | Mười phút. Giờ là hai mươi. | HLV |
| `vo_jiho_ach_all_stations` | Kang | Xong cả 5 loại trạm trong một trận | Every station in one run. ...That made my day. | Đủ mọi trạm trong một trận. ...Làm tôi vui cả ngày. | vui khẽ |
| `vo_chen_ach_first_evo` | Chen | Tiến hoá kỹ năng đầu tiên | Your first evolution. Remember this feeling. | Lần tiến hoá đầu. Nhớ cảm giác này. | ấm |
| `vo_lukas_ach_guns_10` | Lukas | Sở hữu 10 súng | Ten guns! TEN! I'm not crying! Gun oil! | Mười khẩu! MƯỜI! Tôi không khóc! Dầu súng! | xúc động hài |
| `vo_mai_ach_coins_50k` | Mai | Tổng 50.000 xu | Fifty thousand coins. You're buying lunch, sweetie. | Năm mươi nghìn xu. Cậu bao bữa trưa nhé. | trêu |
| `vo_kaito_ach_whiteout` | Kaito | Mở vùng 5 Whiteout | You made it to Whiteout! Dress warm! | Tới được Whiteout rồi! Mặc ấm vào! | vui |
| `vo_riley_ach_threat_10` | Riley | Trụ tới cấp đe doạ 10 | Threat ten, and you're still standing. I'm writing that down. | Đe doạ mười mà cậu vẫn đứng. Tôi ghi lại đây. | tự hào ngầm |
| `vo_chen_ach_nohit_3m` | Chen | Trụ 3 phút không mất máu | Three minutes. No hits. That is technique. | Ba phút. Không trúng đòn. Đó là kỹ thuật. | khen |

## Luật phát (đề xuất)

- Mỗi lúc chỉ một câu; câu FTUE và sự kiện chen trước chatter. Không phát khi đang chọn thẻ hoặc trong 3 s đầu surge.
- Chatter ngẫu nhiên: tối đa 1 câu / 60–90 s, chỉ lúc yên (ít quái gần), không lặp câu trong 5 trận gần nhất.
- Hội thoại: tối đa 1 / trận, phát khi yên; mỗi hội thoại chỉ phát 1 lần cho tới khi đã nghe hết các hội thoại khác.
- Câu chê (critique): mỗi loại tối đa 1 lần / ngày, không phát ngay sau khi người chơi chết lần đầu trong ngày.
- Easter egg: mỗi cái phát 1 lần (lưu cờ), riêng ngày lễ mỗi năm 1 lần.
- Phụ đề luôn hiện trên thẻ bộ đàm (EN, hoặc bản dịch theo ngôn ngữ máy).
