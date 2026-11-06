# Thiết kế bước tiếp theo — Sandbox chó có tương tác

**Ngày:** 06/11/2026, Asia/Saigon.  
**Trạng thái:** thiết kế và khuyến nghị, chưa triển khai gameplay trong lượt này.  
**Đầu vào:** [rà soát M1.1–M2](M1_1_M2_ACCEPTANCE_2026-11-06.md), bảy đề xuất của người dùng và nền Fusion Shared/Web hiện có.

## 1. Nhận xét công tâm

Hướng này có tiềm năng hơn việc chỉ thêm model và một map lớn: người chơi cần thứ để thử, phá, mang đi và phối hợp. Giá trị nằm ở **các cơ chế kết hợp**, chẳng hạn lấy xương → chọc NPC → né cú đấm → ngoạm người đã gục → cùng bạn đưa lên bục. Rủi ro là làm đồng thời AI, combat, ragdoll, xe và world physics trước khi biết cảm giác tương tác có vui hay không.

Các mức chi phí dưới đây là ước lượng tương đối theo code hiện tại, không phải lịch giao hàng.

| Đề xuất | Giá trị gameplay | Chi phí / rủi ro multiplayer | Nhận xét và ưu tiên |
| --- | --- | --- | --- |
| Chó đứng trên nhau | Tạo trò co-op, leo chỗ cao và hình huống hài mà không cần nhiều content | Trung bình–cao: moving support, rung, vòng lặp đỡ nhau, mất peer | Nên làm sớm dưới dạng 2 chó, rồi mở 3; không cần chuyển chó sang Rigidbody |
| Mở rộng map / asset ngoài | Cần để có không gian và mục tiêu thử cơ chế | Trung bình; map rộng rỗng làm người chơi phân tán, asset nặng làm Web chậm | Làm một sân chơi nhỏ có mật độ tương tác cao trước; ưu tiên blockout, mua/import art sau |
| Bóng, xương và đồ vật | Hiệu quả/chi phí tốt nhất; hỗ trợ ngoạm, ném, đẩy, nhặt và mini-game | Xương thấp–trung bình; bóng có va chạm thật trung bình–cao | Ưu tiên đầu tiên; bóng phải lăn và phản ứng, không chỉ là mesh trang trí |
| NPC đi lại, bị cắn thì đấm | Tạo cảm giác thế giới phản hồi và một vòng chọc phá rõ ràng | Cao: điều hướng, chọn mục tiêu, hit timing, recovery, quyền ghi state | Rất đáng làm sau carry/ball; 1 NPC trước, chưa làm AI thành phố như GTA |
| NPC hết HP thành ragdoll | Phần thưởng thị giác mạnh, liên kết tốt với ngoạm người | Cao–rất cao nếu đồng bộ từng chi và cho va chạm chính xác | Giữ trong hướng sản phẩm; prototype ragdoll có giới hạn rồi quyết định mức fidelity |
| Ngoạm đồ bất kể kích thước, cả ragdoll | Cơ chế nhận diện game mạnh nhất; sự phi lý tạo chất hài | Trung bình với attach; rất cao nếu kéo mọi joint theo lực hai chiều | Ưu tiên lõi; kích thước không là điều kiện từ chối, nhưng đối tượng phải được đánh dấu carryable |
| Xe để lái | Có thể rất vui và mở ra chở bạn/chở đồ | Rất cao: input, camera, seat, authority, va chạm, thoát xe, lật xe | Giữ ở giai đoạn thử nghiệm sau; bắt đầu xe nhỏ tốc độ thấp, một ghế |

**Khuyến nghị:** carry + đồ vật + blockout trước; chồng chó và NPC tiếp theo; ragdoll là đợt riêng; xe sau khi vòng chơi đi bộ đã đáng chơi. Không cần hoàn tất ba quest cũ trước khi thử hướng sandbox mới.

## 2. Quan hệ với thiết kế cũ

[Thiết kế gốc](Specifications/DOG_SIMULATOR_DESIGN_AND_CODEX_RULES.md) giữ nguyên như snapshot lịch sử. Yêu cầu mới chủ động đề xuất thay các giới hạn sau:

| Giới hạn cũ | Hướng mới được đề xuất |
| --- | --- |
| Không HP/combat NPC; NPC chỉ P1 | Cho phép HP của NPC và phản công đơn giản ở giai đoạn gameplay mới |
| Không dynamic Rigidbody ở P0/P1 | Thử nghiệm có giới hạn cho bóng và ragdoll; không chuyển toàn bộ world sang mô phỏng vật lý |
| Carry chỉ attach/drop pose | Giữ attach làm nền, bổ sung ném/đẩy và ngoạm NPC gục theo giai đoạn |
| Không xe | Thêm một prototype xe lái ở giai đoạn sau, có gate riêng |
| M4 bắt buộc ba quest P0 | Tạm ưu tiên sân chơi tương tác; quest đóng vai trò hướng dẫn tùy chọn sau khi core vui |

Đây là **đề xuất sửa phạm vi**, không phải âm thầm áp dụng mọi mở rộng vào M3 hiện tại. Khi được yêu cầu triển khai, dùng các đợt nhỏ bên dưới và ghi rõ thay đổi giới hạn vào changelog. Giữ Unity/URP, Shared Mode, Web PC, một map liên thông, motor hiện tại và một network avatar/player. Không có nhu cầu đổi topology chỉ từ danh sách ý tưởng này.

## 3. Vòng chơi và sân chơi đầu tiên

Vòng chơi: **khám phá → tác động → thế giới phản ứng → tạo tình huống hài/co-op → reset thử cách khác**. Không cần hệ thống tiền, XP hoặc nhiệm vụ dài để vòng này chạy được.

Sân thử đề xuất khoảng **50×50 m** là ngân sách blockout ban đầu, không phải diện tích đã benchmark:

- Góc đồ chơi: bóng, xương, vài thùng; vòng/gôn nhỏ để có mục tiêu tự nhiên.
- Sân có bục thấp, dốc và một bệ chỉ với tới khi phối hợp đứng lên nhau; có đường vòng cho chơi một mình.
- Lối đi hình vòng cho NPC; có chỗ nhìn rõ cú đấm và đường né, không ngõ cụt giữ người chơi.
- Điểm trả đồ/chuồng chó và nút reset sân. Các khu thấy nhau để người chơi dễ tụ tập.
- Khi thử xe mới thêm vòng đường nhỏ vào cùng map. Chưa mở thành phố hoặc map thứ hai.

Asset ngoài chỉ được shortlist/import sau khi blockout cho thấy kích thước gameplay phù hợp. Chọn bộ low-poly đồng nhất, hỗ trợ URP, collider tách đơn giản; NPC cần rig, clip đi/đấm/ngã/đứng dậy và khả năng dựng ragdoll. Lưu URL nguồn, tác giả, phiên bản, điều kiện sử dụng/phân phối và chi phí vào manifest trước khi dùng. Chưa chọn hoặc khuyến nghị mua asset cụ thể trong tài liệu này; không import kèm controller/demo/network package nếu không cần. Kiểm tra thử trong bản Web về download, texture, draw calls và skinned meshes, không suy ra nhẹ chỉ từ số polygon.

## 4. Ngoạm đồ: cơ chế trung tâm

- Đề xuất **E** nhìn gần mục tiêu để ngoạm, E lần nữa để thả; chuột phải vẫn cắn. **Q** ném khi đang ngoạm, đưa vào sau khi pickup/drop ổn. HUD chỉ hiện hành động hiện có.
- Một chó giữ một vật, một vật có một carrier. Không check kích thước/khối lượng để từ chối: thùng to hoặc người gục vẫn được ngoạm, không tự thu nhỏ mesh.
- Mỗi vật có điểm ngoạm và offset riêng; attach vào `MouthAnchor`. Camera không bị mesh vật lớn che kín; tạm giảm collider vật đang cầm để tránh đẩy chủ/người khác bay khỏi map.
- “Không quan trọng kích thước” là quy tắc cho **mọi đối tượng có capability carryable**, không mặc định nhấc nền, tòa nhà, NPC đang tỉnh hoặc xe đang có người lái. Xương, bóng, thùng và NPC gục là danh sách đầu.
- Với vật lớn, chấp nhận xuyên thị giác có kiểm soát khi đang mang; điểm thả phải hợp lệ và không chèn player vào tường. Nếu muốn vật lớn chặn cửa hoặc kéo người chơi theo khối lượng thật, đó là mức vật lý tiếp theo, không giả vờ attach đã đáp ứng.
- State đề xuất: `Free → Carried → Free/Thrown`, kèm `Carrier`, `GripId`, pose/velocity khi thả và revision. Chỉ authority của vật commit; hai request cùng lúc chỉ một người thắng.
- Leave/disconnect/respawn làm rơi vật ở vị trí hợp lệ gần carrier cuối; không despawn mất vật. Request lặp không nhân đôi pickup/impulse.

Xương dùng attach/drop đơn giản. Bóng có giai đoạn lăn/đẩy/nhấc/thả; khi free do một authority mô phỏng, khi carried tắt động lực và mỗi client attach visual. Không đồng thời để physics tự kéo và renderer ghi transform.

## 5. Đứng trên chó khác

`DogMotor` đang dùng CharacterController; proxy hiện tắt CharacterController trong `NetworkDogPlayer.Spawned()`. Vì vậy chỉ bật collision hoặc sửa layer sẽ không tự tạo moving support ổn định.

Thiết kế đề xuất:

1. Mỗi chó có mặt đỡ đơn giản trên lưng, độc lập mesh/Animator; proxy có query collider cho mặt này, không bật motor của proxy.
2. Chó trên xác định support từ cast xuống mặt lưng, lưu support ID/local offset; motor cộng chuyển động của support tại tick mô phỏng. Không parent toàn bộ NetworkObject vào avatar khác để thay simulation.
3. Phân biệt pose phục vụ support với pose render interpolation; tránh lấy transform render rồi cộng lần nữa vào network position.
4. Một chó chỉ có một support, cấm chu trình A đỡ B trong khi B đỡ A. Đợt đầu tối đa 2 tầng; qua kiểm thử mới mở 3. Giới hạn chiều cao là giới hạn prototype, không âm thầm đổi mong muốn đứng trên nhau.
5. Nhảy, đi khỏi mặt lưng, support leave/teleport/respawn thì tách; chuyển sang rơi hoặc đặt ở điểm an toàn, không tăng tốc vô hạn.

Không cần HP/PvP để cơ chế này vui. Phải thử remote support di chuyển và xoay, không chỉ hai chó đứng yên trong Editor.

## 6. NPC phản công và HP

Đợt đầu 1 NPC, sau đó tối đa 2 trong sân thử. World authority chạy điều hướng và state machine; client khác không tự quyết định damage hoặc mục tiêu.

`Patrol → Alert → Chase → Windup → Punch → Recovery → Patrol`; `HP = 0 → Downed`. NPC chỉ đuổi trong vùng sân, mất mục tiêu thì quay về. Cú đấm có báo hiệu, khoảng cách, cooldown và recovery để người chơi né được; không đánh vô hạn khi chó vừa spawn hoặc đang trong menu.

Thông số để bắt đầu playtest, **chưa cân bằng**: NPC 3 HP, một bite hợp lệ trừ 1, bite cooldown 0,7 s, windup đấm 0,5 s, recovery 1 s, bỏ đuổi sau 5 s không tiếp cận được. Mỗi NPC chọn một attacker còn hợp lệ; quy tắc tie-break ổn định, không đổi mục tiêu mỗi frame.

Trong đợt đầu cú đấm gây knockback ngắn qua motor chó và khóa input tối đa 0,3 s, không cần HP/death của chó. Đây là đề xuất giảm độ phức tạp; HP mà người dùng yêu cầu áp dụng cho NPC. Attack có ID/sequence, kiểm tra source, range, phase và cooldown; mỗi đòn chỉ áp dụng một lần. Knockback gửi cho avatar authority thực hiện, có revision và thời hạn, không gọi AddForce lên CharacterController.

## 7. NPC gục và ragdoll mang được

Khi HP về 0: dừng agent/AI và collider điều khiển đứng; chuyển sang thân mềm ngã; cho phép ngoạm; thả xuống vẫn ở trạng thái gục. Đề xuất hồi phục sau 20 s **kể từ lúc được thả**, có báo hiệu 2 s trước khi đứng dậy, hoặc reset sân. Thông số cần playtest; không đứng dậy trong miệng chó.

Hai mức phải phân biệt khi nghiệm thu:

| Mức | Mô phỏng và đồng bộ | Đáp ứng / giới hạn |
| --- | --- | --- |
| Prototype đề xuất | Một root/proxy collision authoritative; các chi ragdoll cục bộ, ngoạm tại grip gốc và các chi lắc theo | Có ngã mềm, mang nguyên người, root/HP/carrier nhất quán; pose tay chân có thể khác giữa client, chi không làm hitbox gameplay |
| Ragdoll vật lý đồng bộ cao | Authority mô phỏng bộ xương, serialize pose/velocity các phần cần thiết, proxy hiển thị/interpolate, có phục hồi qua handoff | Cần nếu muốn nắm tay/chân kéo đúng từng joint, chi va chạm làm gameplay hoặc mọi máy cùng pose; chi phí bandwidth/CPU và kiểm thử cao hơn |

Khuyến nghị bắt đầu mức một. **Đây chưa tương đương đầy đủ ragdoll vật lý kiểu Goat Simulator**; nếu tính hài không đủ, phải thử mức hai trước khi gọi mục này hoàn tất. Không hứa các máy sẽ tạo cùng ragdoll chỉ vì dùng chung random seed: local physics không được coi là deterministic.

Chỉ root/proxy quyết định vùng hợp lệ, điểm thả và nhận tương tác; pose ragdoll local không được quyết định quest/damage. Late join tái dựng state hiện tại, không replay toàn bộ lịch sử cú cắn. Bắt đầu 1 ragdoll active, thử tối đa 2 trước khi tăng. Dùng sleep/reset và giới hạn lifetime; tránh tạo object/chi mạng vô hạn.

## 8. Xe lái: đợt thử riêng

Một xe kiểu golf cart/xe đồ chơi, một ghế, tốc độ thấp và một vòng đường kín. E vào/ra ghế; khi ngồi input lái thay input chó, camera đổi theo xe; không ngoạm xe đang có người. Chưa có traffic AI, phá hủy xe, nhiều ghế hoặc bám ragdoll vào bánh.

State tối thiểu: `Driver`, `SeatRevision`, pose/velocity và trạng thái reset. Giữ world authority ở prototype đầu, driver gửi input đã kiểm tra driver/sequence/timeout. Phải đo độ trễ điều khiển trước; nếu cảm giác lái không đạt, spike chuyển authority cho driver với quy trình reclaim khi rời/disconnect/Master đổi. Không để hai peer cùng mô phỏng xe authoritative.

Thoát xe phải tìm điểm an toàn; đang lật có nút reset; driver mất kết nối thì xe dừng, ghế trống, authority được thu hồi. Đứng trên/chở vật/đâm NPC bằng xe là ca tích hợp sau, không tự tính đã hỗ trợ chỉ vì xe đã chạy.

## 9. Authority và hướng physics

| Đối tượng | Authority / dữ liệu thật | Chỉ phục vụ hiển thị |
| --- | --- | --- |
| Chó | Owner: motor, input/action; support relation cần validate | Mesh/controller, âm thanh và camera |
| Carryable, bóng | Master/world ở đợt đầu: carrier, revision, free pose/velocity | Attach miệng, hiệu ứng và smoothing |
| NPC | Master: HP, state/target, tick attack/recovery, pose gốc | Animator và reaction effects |
| Ragdoll prototype | Master: lifecycle, root, carrier, recovery tick | Chi ragdoll local |
| Xe prototype | World: driver/seat, accepted input, motion; handoff là spike sau | Camera, bánh xe và animation chó ngồi |

Shared Mode có đường hỗ trợ physics qua `NetworkTransform`; Forecast Physics là một lựa chọn cần thử với đúng SDK/config đang cài, không phải lý do khẳng định Shared không làm được bóng hoặc xe. Nguồn: [Photon — Physics overview](https://doc.photonengine.com/fusion/v2/manual/physics/physics-overview), [Forecast Physics trong Shared Mode](https://doc.photonengine.com/fusion/v2/video-tutorials/forecast-physics). Không mặc định cài Physics Addon: [Physics Addon 2.1](https://doc.photonengine.com/fusion/v2/addons/physics-addon-2.1) dành cho Host/Dedicated, khác topology đang dùng.

Phần phân chia root và chi ragdoll, hạn mức đối tượng và thứ tự triển khai ở đây là **quyết định thiết kế đề xuất của dự án**, không phải cam kết sẵn có của Photon. [Unity Character Controller](https://docs.unity3d.com/6000.0/Documentation/Manual/CharacterControllers.html) cũng không tự phản ứng như Rigidbody; support và knockback cần logic motor riêng.

Không bắt buộc dùng RPC cho mọi interaction: lỗi MethodAccessException từng xuất hiện trên đường RPC cũ cần được regression nếu dùng lại. Chọn request channel đã kiểm chứng với SDK; request rời rạc cần ack/sequence và không bị ghi đè làm mất đòn, còn input lái là stream có sequence/timeout. State bền vững nằm ở object authority, không nằm trong RPC lịch sử hoặc dictionary local.

Master đổi: giữ HP, carrier, attack/recovery tick, pose/velocity từ state; hủy windup không còn target, thả vật của peer đã rời; không reset toàn sân chỉ vì đổi Master. Shared client authority phù hợp prototype chơi cùng bạn, không được xem là chống gian lận kiểu server competitive.

## 10. Các đợt triển khai và gate

Các budget là giới hạn **prototype đề xuất**, không phải capacity đã chứng minh. Bắt đầu bằng 2 client; 3 client khi thử tháp 3 chó. Roster hiện chỉ có 3 ID độc quyền, muốn thử 4–8 avatar phải bổ sung model hoặc quyết định cho phép trùng model trước, không dùng 8 slot cấu hình làm bằng chứng.

| Đợt | Đầu ra cụ thể | Gate trước khi đi tiếp |
| --- | --- | --- |
| G0 — Chốt nền | Xác minh animation 3 model, room/input và test thất bại; lưu bằng chứng | Checklist lỗi chặn ở biên bản nghiệm thu có kết quả |
| M3-A — Ngoạm và sân thử | Blockout một sân; 1 xương, 1 bóng, 3 thùng; pickup/drop, reset; bóng lăn/đẩy | Hai client tranh cùng đồ: một carrier; carrier rời không mất đồ; thả không kẹt tường; có tình huống chơi lại |
| M3-B — Chồng chó | Support hai tầng, bục co-op; sau đó thử ba | Người dưới đi/xoay/nhảy/rời phòng, người trên tách an toàn; không rung tăng dần hoặc bay vô hạn |
| M3-C — NPC phản công | 1 NPC patrol/chase/punch, HP, bite/knockback | Hai client đồng ý HP và thời điểm gục; đòn không nhân đôi; né được đấm và không stun-lock |
| M3-D — Ragdoll + ngoạm người | Prototype root + ragdoll local, carry/drop/recovery | Ngoạm cả người không bị giới hạn kích thước; late join/handoff đúng state; xác nhận mức ragdoll đủ vui hay cần spike joint-sync |
| M4-A — Hoàn thiện sân | Art asset đã chọn, 2 NPC nếu budget cho phép, mục tiêu tùy chọn | 10 phút chơi không cạn trò ngay; người chơi tìm được đồ/NPC/bục; Web load/performance không tụt ngoài budget |
| M4-B — Xe experimental | 1 xe/ghế, enter/drive/exit/reset | Hai người tranh ghế, driver rời, lật/thoát sát tường, handoff; độ trễ lái chấp nhận được qua playtest |

Mốc gameplay đầu tiên có thể bàn giao riêng là **M3-A**; không dồn xe và ragdoll vào cùng thay đổi. Tên M4-A/B là roadmap mới đề xuất, chưa thay implementation của quest cũ. M5 vẫn là build/phát hành và kiểm chứng tải, không tự có PASS vì chơi được với 2 người.

## 11. Kiểm thử và cách quyết định có vui không

Mỗi đợt cần test logic cho contention/idempotency/state transition và thử thật ít nhất 2 process trên cùng build Web. Với state persistent: late join, carrier/driver disconnect và Master rời là bắt buộc; với physics: drop dưới sàn, reset khi đang carried, lag và va chạm ở mép map phải có log. Không cần viết test cho mọi giá trị Inspector.

Ghi build/version, máy, browser, resolution, số client, lượt idle và lượt nhiều tương tác. So sánh với baseline cùng môi trường: FPS/frame time, spike, GC nếu đo được và traffic mạng. Dùng mục tiêu tạm >=30 FPS trên máy/browser được ghi; báo cả spike và độ trễ tương tác, không chỉ trung bình. Thiết lập ngưỡng tương tác qua playtest và ghi số đo, chưa hứa 20 người hoặc mọi máy Web đều mượt.

Phiên chơi thử đề xuất: 2–3 người, 10 phút, 2 phút đầu không hướng dẫn. Ghi họ có tự tìm được ngoạm/đẩy/phản ứng NPC không, số tình huống tự thử lại, thời gian không biết làm gì và lỗi input/camera. Nếu người chơi chỉ đi quanh nhìn map, tăng chất lượng phản hồi và tổ hợp tương tác trước khi tăng diện tích hoặc số NPC. Đây là cách kiểm tra thiết kế, không phải thống kê chứng minh sản phẩm đã vui.

## 12. Những lựa chọn còn mở

- Có giữ model độc quyền khi cần nhiều người chơi hơn roster không? Hiện giữ nguyên; cần quyết định trước load test >3 avatar.
- Mức ragdoll nào đủ: local limbs với shared root hay kéo đúng từng chi? Prototype và cảm nhận thực tế là căn cứ chốt.
- Cảm giác phi lý khi ngoạm vật to quan trọng hơn collision chính xác đến đâu? Bản đầu ưu tiên ngoạm không giới hạn kích thước, ghi rõ giới hạn collider khi mang.
- Xe có đóng góp vào vòng chơi hay chỉ là tính năng riêng? Chỉ đưa vào bản chính sau phiên thử có bằng chứng người chơi muốn dùng lại.

Không cần chốt các lựa chọn này để bắt đầu M3-A. Tài liệu này chỉ chuẩn bị công việc; chưa mua asset, thêm package hoặc sửa scene/runtime.
