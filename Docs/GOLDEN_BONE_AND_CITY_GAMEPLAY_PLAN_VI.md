# Kế hoạch triển khai Vụ trộm Xương Vàng và nâng cấp SimplePoly City

## 1. Phạm vi

Triển khai trên `SimplePolyCity_Gameplay` cho bản Windows, dùng Fusion Single khi chơi solo và Host/Client khi chơi co-op. P0 gồm hai phần bàn giao nối tiếp. P0-A cải thiện xe, NPC và giao thông hiện có. P0-B hoàn thiện ngoạm chó đang nằm và nhiệm vụ Xương Vàng trên cùng thành phố. P1 thêm sự kiện **Giao hàng rơi vãi** sau khi P0 qua chơi thử.

Vòng chơi Xương Vàng là khám phá → lấy xương → phối hợp hoặc phá rối → đưa xương về chuồng → chơi lại bằng cách khác. Cả phòng hoàn thành khi Xương Vàng vào vùng chuồng. Đi đường vòng, mở cổng, ném qua rào, bật đệm nhún và cứu đồng đội đều là lựa chọn trong thế giới. Solo phải có đường giải đầy đủ. Chó đang nằm có thể được ngoạm, mang và đặt xuống. NPC đang nằm có thể được ngoạm, mang, đặt xuống **và ném**.

## 2. Mốc hiện tại trong repo

| Hệ thống | Hiện trạng và phần cần bổ sung |
| --- | --- |
| Xe | Thành phố có 104 xe, trong đó 3 xe tự chạy. Prefab taxi có khối lượng khoảng 4.740 kg và xe buýt khoảng 6.977 kg. Xe đỗ đã bỏ qua xung lực đẩy do motor chó gửi trực tiếp, nhưng vẫn nhận va chạm PhysX. Cần đo nguồn dịch chuyển còn lại khi chó tì, ném đồ hoặc xe khác tông. [Bản mở rộng thành phố](SIMPLEPOLY_CITY_IMPLEMENTATION_PLAN_VI.md#9-bản-mở-rộng-xe-và-npc-ngày-09112026) · [Điều khiển xe](../Assets/Game/Scripts/Fusion/NetworkCityVehicle.cs) |
| Va chạm xe và NPC | Nhánh quét thân xe và callback va chạm hiện chỉ xếp hàng tác động lên avatar chó. NPC đang đứng cũng đi vào nhánh vật cản cứng của xe tự chạy. NPC dùng `NavMeshAgent`, biết khựng khi đồ ném trúng và đuổi chó đã cắn. Cần thêm đường tác động lên NPC và trạng thái ngã có vật lý. [Xe](../Assets/Game/Scripts/Fusion/NetworkCityVehicle.cs) · [NPC](../Assets/Game/Scripts/Fusion/NetworkCityPedestrian.cs) |
| Giao thông | Ba xe chia một vòng ở phía tây nam. Waypoint hiện đặt mục tiêu 9 m/s ở đoạn thẳng và 5 m/s ở đoạn cua. Controller còn giảm tốc theo góc cua và vật cản, nên cần đo tốc độ thực khi tăng cấu hình. [Bộ dựng thành phố](../Assets/Game/Editor/SimplePolyCitySetup.cs) |
| Ngoạm và đệm nhún | Đồ vật đã có chủ mang, điểm ngoạm, ném/thả và hồi vị trí. Đệm nhún trong sân thử tác động lên motor chó. Chó đã có `KnockdownActive` và có thể giữ cờ này trong lúc còn bay hoặc trượt. Cần thêm quy tắc dùng chung một chỗ mang và điều kiện ổn định trước khi ngoạm chó hoặc NPC. [Đồ ngoạm](../Assets/Game/Scripts/Fusion/NetworkCarryable.cs) · [Avatar chó](../Assets/Game/Scripts/Fusion/NetworkDogPlayer.cs) · [Motor](../Assets/Game/Scripts/Player/DogMotor.cs) |

## 3. P0-A — Xe nặng hơn, NPC ngã được và giao thông dày hơn

### 3.1 Xe nặng hơn và giữ vị trí khi đỗ

1. Đo trước trên cùng một mặt đường bằng taxi và xe buýt. Ghi độ dịch chuyển và góc xoay khi một chó rồi hai chó tì trong 3 giây, khi bị ném thùng, khi xe có tài xế va ngang và khi hai xe đâm nhau. Tách xung lực từ motor chó khỏi tiếp xúc vật lý và các hiệu chỉnh do bộ điều khiển xe gây ra.
2. Tăng khối lượng các nhóm xe khoảng **1,5 lần** so với prefab hiện tại làm mốc thử, giữ thứ tự xe con < xe tải/xe buýt. Cập nhật công thức trong bộ dựng prefab để lần tạo lại thành phố giữ cùng giá trị. Mức này là điểm bắt đầu để cân chỉnh, chưa phải thông số phát hành.
3. Giữ lực đẩy trực tiếp của chó bằng 0 với xe đỗ. Nếu số đo còn trượt đáng kể do tiếp xúc PhysX, chỉnh lực giữ/phanh đỗ cho lực ngang nhỏ, để xe nặng khác đâm vẫn dịch chuyển. Giới hạn xung lực chó truyền vào xe đang chạy nếu phép đo cho thấy còn lệch làn. Giữ Rigidbody động trên host và kiểm tra lại cảm giác tăng tốc, rẽ, phanh và xuống xe. Bộ điều khiển hiện dùng `VelocityChange` cho phần bám tốc độ, vì vậy kiểm tra riêng đáp ứng điều khiển và đáp ứng va chạm.
4. Gate ban đầu là taxi và xe buýt trên nền phẳng dịch tối đa **0,10 m sau 3 giây chó tì** trong ca một và hai chó. Va chạm xe–xe vẫn có chuyển động thấy rõ, xe có người lái vẫn vào/ra ghế và không tự trượt khi thả ga trên nền phẳng. Ghi kết quả trước và sau trên host và quan sát từ khách.

### 3.2 Xe đâm NPC và NPC có trạng thái ngã

- Mở rộng đường quét thân xe theo chuyển động thực và nhánh tiếp xúc để nhận diện NPC. Cả xe tự chạy lẫn xe người chơi lái đưa hit vào cùng một hàng đợi trên host. Hợp nhất hit theo cặp xe–NPC, lấy lần tiếp cận mạnh nhất trong bước và áp dụng một phản ứng. Ưu tiên vật cản cứng nằm trước NPC trên quỹ đạo. Giữ CCD và kiểm tra trường hợp xe chỉ lướt sát, NPC đứng sau tường và xe lùi trúng NPC.
- Tách NPC đang đứng khỏi nhóm tường và xe trong truy vấn phanh của xe tự chạy. Cho xe giảm tốc có giới hạn khi sắp gặp NPC, vẫn cho tiếp xúc nếu NPC cắt ngang ở cự ly ngắn. Xe đứng yên và va quệt nhẹ chỉ có phản ứng nhẹ hoặc khựng. Cú đâm đủ mạnh hất NPC theo hướng va chạm rồi ngã. Chọn ngưỡng theo tốc độ tiếp cận thực, có ca thử ở tốc độ đường thẳng của tuyến mới. Bóng nhẹ vẫn làm NPC khựng. Vật nặng ném trúng đủ lực có thể làm ngã. Cú cắn giữ vai trò khiêu khích NPC.
- Dùng vòng đời `Đứng → Ngã → Nằm ổn định → Hồi phục`. Khi ngã, dừng `NavMeshAgent`, hủy cú đánh đang lấy đà, đổi sang collider thân nằm và một Rigidbody gốc do host mô phỏng. Hiển thị ngã và đứng lên bằng pose/animation trên các máy. Bản đầu dùng một thân vật lý gốc, phù hợp prefab NPC hiện tại. Khi đã chạm đất và gần như dừng, NPC chuyển sang trạng thái có thể ngoạm. Khi đang được mang, dừng vật lý độc lập và đi theo điểm ngoạm. Khi được thả hoặc ném, trở lại trạng thái ngã rồi tính lại thời gian hồi phục sau khi ổn định.
- Hồi phục ở vị trí có nền, có chỗ đứng và có NavMesh phù hợp. Nếu điểm nằm ngoài NavMesh hoặc sát vật cản, thử điểm an toàn gần đó rồi dùng điểm tuần tra dự phòng. Host khôi phục collider đứng, đặt lại vận tốc, bật agent và tiếp tục tuần tra. Late join thấy đúng trạng thái, pose gốc và người mang của NPC. NPC bị ném có xung lực được giới hạn, va chạm với môi trường như một vật rời và trở lại chu kỳ hồi phục. Kiểm thử ném sát tường và ném ra khỏi vùng tuần tra.

### 3.3 Tăng mật độ và tốc độ giao thông

| Nấc | Cấu hình thử | Điều kiện tăng tiếp |
| --- | --- | --- |
| Mốc so sánh | 3 xe, tuyến và tốc độ hiện tại | Ghi tốc độ thực, thời gian chạy vòng, số lần kẹt/reset và chi phí mô phỏng host. |
| Tăng tốc | 3 xe, mục tiêu **12 m/s** trên đường thẳng, giữ **5 m/s** ở góc cua | Qua được cua và dừng sau xe trước. Ca đâm chó/NPC ở tốc độ mới cho kết quả nhất quán. |
| Tăng mật độ | **6 xe tự chạy trên ít nhất 2 vòng**, xuất phát lệch nhau. Dùng xe đỗ sẵn phù hợp tuyến, giữ tổng số xe hiện tại | Mỗi xe chạy nhiều vòng. Làn và giao lộ thông được, không reset do chờ xe trước. Lối vào khu nhiệm vụ và điểm spawn nằm ngoài vùng xe chạy. |
| Cân chỉnh | Có thể thử **14 m/s** ở đoạn thẳng dài sau khi nấc 6 xe ổn định | Chốt theo tốc độ thực và playtest. So sánh chi phí host, độ ổn định giao lộ và tần suất xe đâm với nấc trước. |

Author thêm tuyến trên đường hiện có. Kiểm tra chiều làn, bán kính cua theo thân xe, chướng ngại tĩnh và khoảng cách xuất phát. Chọn lối ưu tiên rõ ràng tại chỗ các tuyến giao nhau. Giữ phanh theo khoảng dừng với xe và công trình. Bộ đếm kẹt chỉ dẫn đến hồi xe khi thật sự mất tuyến hoặc không thể tiến sau khi đường đã thông. Điểm hồi phải trống và được kiểm tra trên host. Cho xe đỗ còn lại tiếp tục ngủ vật lý khi đủ điều kiện.

## 4. Quy tắc ngoạm áp dụng cho đồ, chó và NPC

| Thao tác | Quy tắc triển khai |
| --- | --- |
| Chọn mục tiêu | Chuột trái giữ cùng ngữ cảnh ngoạm hiện tại. Khi mõm trống, chọn mục tiêu hợp lệ gần hướng mõm và có đường tiếp cận. Hiện gợi ý đúng loại mục tiêu. Host xác nhận lại tầm, trạng thái và quyền thao tác khi xử lý yêu cầu. |
| Chiếm chỗ mang | Mỗi chó có đúng một chỗ mang dùng chung cho đồ vật, chó và NPC. Một mục tiêu có tối đa một người mang. Chó đang bị mang hoặc đang mang mục tiêu khác không thể trở thành một mắt xích mới. Xử lý yêu cầu đồ vật và avatar qua cùng quy tắc kiểm tra ở thời điểm commit, kể cả hai yêu cầu trong cùng tick. |
| Mục tiêu đang nằm | Chó phải bị đánh ngã, chạm nền ổn định, vận tốc dưới ngưỡng trong một khoảng ngắn và đã thả đồ đang mang. NPC chỉ hợp lệ trong trạng thái nằm ổn định. Cân chỉnh thời gian chó nằm tính từ lúc ổn định để có đủ cơ hội cứu đồng đội. |
| Đang mang | Giữ `KnockdownActive` và quan hệ mang tách nhau. Chó được mang vẫn nằm khi bộ đếm ngã hết. Chỉ mô phỏng vị trí gốc theo điểm ngoạm trên host. Khách hiển thị theo cùng điểm ngoạm từ pose người mang đang thấy. Tạm dừng motor, điều hướng và va chạm gameplay của mục tiêu tương ứng để tránh tranh quyền ghi vị trí. |
| Thả, ném, tự thoát | **E** đặt xuống với cả ba loại. Chuột trái khi đang mang cho phép ném **đồ vật và NPC**. **Space** để chó được mang tự thoát. Host gỡ quan hệ, đặt xuống tại vị trí hợp lệ, khôi phục điều khiển khi đủ điều kiện và áp khoảng miễn ngoạm lại dự kiến 2 giây sau khi tự thoát. Nếu quanh người mang bị chắn, dùng vị trí an toàn dự phòng để Space vẫn hoạt động. |
| Bị đánh ngã, rời phòng, reset | Giải phóng chỗ mang trước khi xét yêu cầu ngoạm mới. Chó mang Xương Vàng bị đánh ngã sẽ làm rơi xương. Khi một trong hai chó rời, dọn cả hai đầu quan hệ trước khi despawn. Late join dựng từ trạng thái hiện tại. Host giữ quyền mô phỏng trong cả vòng đời. |

Tái sử dụng `NetworkCarryable` cho đồ và Xương Vàng. NPC và avatar giữ vòng đời riêng, dùng chung quy tắc chọn mục tiêu, chiếm chỗ mang, kiểm tra điểm thả và dọn trạng thái. Chốt các invariant trước khi thêm nhận diện mục tiêu mới vào HUD. Dùng đường vào xe hiện có để kiểm tra chỗ mang chung trước khi cho ngồi và giữ ưu tiên **E** thả mục tiêu đang mang.

## 5. P0-B — Vụ trộm Xương Vàng

1. **Bố trí một khu phố.** Đặt bệ xương trong sân bảo vệ, chuồng đích dễ nhận ra, một rào thấp, một cổng tắt và một đệm nhún. Có đường vòng độc lập cho solo. Công tắc mở cổng khi chó hoặc thùng đè lên. Khi có chó hoặc vật thể trong vùng quét của cổng, cổng chờ đến khi lối đi trống.
2. **Tái sử dụng đệm nhún của sân thử.** Dành khoảng chạy đà và bãi đáp đủ rộng để chó mang xương bật qua rồi đi tiếp. Xương đang được ngoạm dùng trạng thái kinematic của carryable. Kiểm tra phần cập nhật pose trên host và phần hiển thị trên khách để xương bám mõm trong suốt cú bật. Ném xương qua rào rồi bật sang đón vẫn là một đường giải.
3. **Bảo vệ tạo cơ hội đánh lạc hướng.** Dùng NPC cùng vòng đời va chạm ở P0-A. Thêm phản ứng quay sang kiểm tra tiếng sủa gần khi đang tuần tra, có hồi chiêu. Khi thấy chó mang xương trong sân, bảo vệ truy đuổi tới ranh giới khu vực. Nếu bị đánh ngã, bảo vệ theo quy tắc ngoạm và hồi phục như NPC khác. Reset nhiệm vụ giải phóng bảo vệ nếu đang được mang.
4. **Ghi nhận hoàn thành trên host.** Tạo một xương có ID ổn định trong phiên. Sau khi cập nhật pose mô phỏng, kiểm tra vùng đích theo vị trí xương trên host. Tính cả đoạn di chuyển nếu xương bị ném nhanh xuyên qua vùng giữa hai tick. Tách chuyển động thật khỏi dịch chuyển do reset. Ghi nhận đúng một lần trên mỗi lượt và đồng bộ trạng thái hoàn thành cho khách đến muộn. Khi reset và ghi điểm trùng tick, ưu tiên reset. Dùng một lần phản hồi hoàn thành chung cho cả phòng.

| Hành động | Điều kiện và phạm vi |
| --- | --- |
| **Respawn Bone** | Tự chạy nếu xương lọt map, hoặc do chủ phòng/người chơi solo gọi khi xương kẹt. Gỡ người mang nếu có, đặt xương về bệ, xóa vận tốc. Giữ nguyên trạng thái nhiệm vụ và phần còn lại của thế giới. |
| **Reset Mission** | Chủ phòng/người chơi solo bắt đầu lại. Hồi xương, bảo vệ, công tắc, cổng và trạng thái lượt. Giữ nguyên xe, NPC và đồ vật bên ngoài khu nhiệm vụ. Cổng vẫn chờ lối trống trước khi đóng. |
| **Reset Sandbox** | Dùng thao tác reset thành phố hiện có và gọi phần reset nhiệm vụ trong cùng luồng. Dọn quan hệ mang trước khi hồi các đối tượng liên quan. |

State lượt chơi, xương nhiệm vụ, quan hệ mang và trạng thái giao thông thuộc phiên. Giữ ID đồ cũ và namespace lưu thành phố để các slot hiện tại đọc đúng. Tăng phiên bản tương thích của phòng/map khi prefab và state mạng thay đổi để mọi người chơi dùng cùng cấu hình.

## 6. Thứ tự bàn giao và kiểm thử

| Đợt | Bàn giao | Gate |
| --- | --- | --- |
| **P0-A1** | Baseline, tăng khối lượng và giữ xe đỗ. Đường nhận hit xe–NPC và vòng đời ngã/hồi phục. | Xe đỗ qua ca tì; xe đâm NPC làm ngã ở tốc độ thử; va quệt, tường chắn và xe đứng yên cho đúng phản ứng; AI hồi phục và đi tiếp. |
| **P0-A2** | Chỗ mang dùng chung, ngoạm và ném NPC, ngoạm và đặt chó. Nâng tốc độ rồi lên 6 xe trên nhiều tuyến. | Qua tranh mục tiêu, ném NPC và hồi phục, rời phòng khi đang mang. Hai loại xe đâm được NPC. Sáu xe chạy ổn định với mật độ đã chọn. |
| **P0-B** | Một nhiệm vụ hoàn chỉnh cùng bảo vệ, cổng và đệm nhún. | Solo và co-op hoàn thành, chơi lại và phục hồi khi xương mắc kẹt. Kiểm tra tải trên cấu hình xe mới. |
| **P1** | Giao hàng rơi vãi. Tái sử dụng đồ ngoạm, vùng giao và thông báo. | Sự kiện bắt đầu, kết thúc và dọn đồ theo vòng đời riêng khi Xương Vàng đang diễn ra. |

### Checklist nghiệm thu P0

- **Xe và giao thông.** So độ trượt trước/sau. Test taxi và xe lớn với xe đỗ, tường, xe phía trước, giao lộ và điểm hồi bị chiếm. Hai loại xe gây một lần phản ứng khi đâm trực diện NPC. NPC sau tường và chó/NPC chỉ đi sát thân xe tránh được hit giả. Chạy tối thiểu 10 phút ở cấu hình 6 xe và quan sát số vòng, tốc độ thực, kẹt và reset.
- **NPC.** Bóng nhẹ làm khựng, cú đâm mạnh làm ngã. Ngoạm và ném NPC qua thao tác thường, đặt xuống gần tường, hồi phục ngoài vùng tuần tra và xử lý khi carrier rời. Khách vào muộn thấy đúng NPC đang ngã, nằm hoặc được mang.
- **Ngoạm chó và tranh mục tiêu.** Hai người cùng ngoạm một chó chỉ một người thắng. Chó còn bay, còn trượt hoặc đang mang mục tiêu bị từ chối. Một chó tranh nhặt đồ và ngoạm NPC/chó trong cùng tick chỉ chiếm được một chỗ. Space hoạt động sát tường và chặn ngoạm lại trong thời gian miễn. Kiểm tra riêng hai trường hợp người mang rời và chó được mang rời.
- **Xương Vàng.** Solo mở cổng bằng thùng và hoàn thành. Chó mang xương bật đệm, qua rào, đáp rồi vào đích với pose ổn định trên host và khách. Xương đang được ngoạm và xương ném qua đích đều ghi đúng một lần. Cú đánh ngã làm rơi xương, nhóm nhặt lại chơi tiếp. Test đủ ba reset, kể cả khi xương hoặc bảo vệ đang được mang. Cổng chờ khi có chó hoặc thùng chắn lối. Late join thấy đúng cổng, xương và trạng thái lượt.
- **Hồi quy.** Chạy lại ca vào/lái/thoát xe, đồ ngoạm/ném/thả, chồng chó, reset sandbox và đọc slot cũ. Cập nhật ca hiện đang cố định 3 xe theo cấu hình phát hành mới, giữ phép kiểm tra tuyến thực sự chạy. Dùng Fusion Single cho ca tái lập được và Windows Host/Client trên ít nhất hai máy cho ca mạng, sau đó chơi thử 2–4 người. [Play Mode thành phố](../Assets/Game/Tests/PlayMode/SimplePolyCityTests.cs)

Đo cùng máy, preset và kịch bản ở các nấc 3 và 6 xe. Ghi p95 frame time, chi phí mô phỏng host, số Rigidbody thức, băng thông, số lần hit lặp/kẹt/reset và độ trễ ngoạm. Đối chiếu ngân sách Windows đã ghi trong tài liệu PC. Trong playtest, quan sát người chơi có tự hiểu vùng đích, tìm được ít nhất hai cách giải và tiếp tục được lượt sau một pha cứu bạn hoặc phá rối hay không. [Ngân sách Windows](Specifications/PC_ARCHITECTURE.md#build-windows-và-ngân-sách-thử-nghiệm)
