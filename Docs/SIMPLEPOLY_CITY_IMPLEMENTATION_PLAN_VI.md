# Kế hoạch triển khai SimplePoly City cho RiseOnDogSimulator

## 1. Phạm vi đã chốt

- Dùng toàn bộ bố cục scene demo làm map gameplay. Giữ vị trí và cấu trúc phố, ngã tư, vỉa hè, nhà, cây, đồ đường phố và xe có sẵn. Các đợt thử có thể bật ít xe tự chạy trước, ngay trên map đầy đủ.
- **Xe giao thông** tự chạy qua các waypoint trên đường. Khi chó lao tới hoặc cắt ngang quá nhanh, xe chỉ giảm tốc nhẹ và có thể đâm chó. Va chạm với tường, công trình và xe khác vẫn phải chặn xe đúng cách.
- **Xe cho chó lái** đỗ sẵn ngoài luồng giao thông, chẳng hạn ở bãi xe hoặc lối đỗ cạnh nhà. Xe đứng yên cho đến khi chó vào ghế và điều khiển. Khi ghế trống, xe phanh về trạng thái đỗ và chờ người lái tiếp theo.
- **Xe quá nặng để ngoạm**, kể cả xe đang đỗ hoặc ghế trống. Đồ rời ngoài nhà có thể ngoạm và đẩy. Đồ cũ như bóng, xương, thùng và poop cũng dùng chung quy tắc đẩy theo khối lượng.

Quy ước cho bản đầu là xe giao thông chỉ dùng cho giao thông, xe đỗ dành cho người chơi có một ghế lái. Nhà, nền, đường và các chi tiết gắn liền công trình dùng collider tĩnh. Xe và đồ rời có thể dịch chuyển khi lực tác động đủ lớn. Mốc này dùng knockback và trạng thái gục hiện có khi xe đâm chó.

## 2. Chuyển scene demo thành map gameplay

1. Tạo `Assets/Game/Scenes/SimplePolyCity_Gameplay.unity` từ [scene demo gốc](<../Assets/SimplePoly City - Low Poly Assets/Demo/SimplePoly City - Low Poly Assets_Demo Scene.unity>). Giữ nguyên scene trong thư mục asset pack để đối chiếu. Giữ hierarchy, prefab instance và transform của bố cục gốc. Ghi rõ các vị trí cần chỉnh vì lý do gameplay, ví dụ xe cho chó lái đang nằm trên tuyến giao thông.
2. Đưa các root phục vụ menu, session, HUD và camera theo chó từ [M1_FusionPreview](../Assets/Game/Scenes/M1_FusionPreview.unity) vào bản gameplay. Dùng ánh sáng của thành phố làm điểm xuất phát. Tắt camera và AudioListener của demo khi camera game hoạt động. Đặt điểm spawn, respawn và vùng hồi vị trí trong khu an toàn ngoài làn xe.
3. Bổ sung collider tĩnh cho mặt đường, vỉa hè, nhà và vật cố định. Kiểm tra các đoạn nối mặt đường, mép vỉa hè, góc nhà, lối vào khu đỗ và rìa map bằng kích thước `CharacterController` thực của chó. Dùng collider ghép đơn giản cho vật động và thân xe.
4. Tạo prefab gameplay trong `Assets/Game/Prefabs` dùng model của asset pack. Phân loại các instance sẵn có thành vật cố định, đồ rời, xe theo tuyến và xe đỗ. Với đồ rời và xe cần mô phỏng, thay instance hiển thị bằng điểm spawn có ID ổn định, prefab và pose gốc để session spawn đúng một bản. Xe chưa được chọn cho tuyến hoặc ghế lái có thể ở trạng thái đỗ; xe có va chạm vẫn dùng quy tắc khối lượng của xe.
5. Đăng ký scene thành phố trong Build Settings và dùng làm scene gameplay mặc định của bản PC khi qua nghiệm thu. Giữ preview hiện tại làm scene kiểm tra hồi quy trong Editor. Cả offline và Host/Client dùng cùng scene thành phố và catalog prefab. [FusionSessionService](../Assets/Game/Scripts/Fusion/FusionSessionService.cs) hiện dùng môi trường tĩnh đã nạp sẵn trên mỗi máy và spawn đối tượng mạng qua runner, nên có thể giữ luồng này. Bản build phải đối chiếu version map và giao thức khi tạo hoặc vào phòng.

Scene hiện có **899 instance prefab**, gồm **104 xe bánh liền**. Đường và vật liệu URP đã có sẵn. Tuyến giao thông, collider, ghế và phần điều khiển cần được author trong bản gameplay. Dùng [taxi tách bánh](<../Assets/SimplePoly City - Low Poly Assets/Prefab/Vehicles/Vehicle with Separated Wheels/Vehicle_Taxi_separate.prefab>) làm mẫu kiểm tra tỉ lệ, góc lái và vị trí ghế trước khi áp dụng cho các mẫu xe khác.

## 3. Xe giao thông

- Đặt waypoint theo tim từng làn trên đường hiện có. Mỗi tuyến lưu hướng, điểm bắt đầu, đoạn nối qua ngã tư và tốc độ mục tiêu theo đoạn. Kiểm tra chiều rẽ, bán kính cua và khoảng trống theo kích thước thân xe. Bật thử 2–4 xe trên các tuyến kín trước khi tăng mật độ.
- Dùng một Rigidbody động trên host và bộ điều khiển lái kiểu arcade. Bộ bám tuyến điều chỉnh ga, phanh và hướng theo pose thực của Rigidbody. Khi xe bị lệch do va chạm, giảm tốc và tìm lại đoạn hợp lệ phía trước. Nếu kẹt lâu, chỉ hồi xe về điểm trống để tránh xuất hiện chồng lên chó, xe hoặc đồ vật.
- Tách xử lý **chó** và **vật cản cứng**. Với chó cắt ngang đột ngột trong vùng phản ứng ngắn, chỉ giảm nhẹ tốc độ mục tiêu và cho phép va chạm. Có thể bắt đầu playtest với mức giảm mục tiêu **10–15%** trong tình huống này, rồi chỉnh theo cảm giác. Đây là mức giảm do logic phát hiện chó trước va chạm. Tốc độ sau khi đâm vẫn phụ thuộc kết quả va chạm. Với tường, xe khác hoặc chướng ngại lớn, dùng quy tắc phanh riêng.
- Xác định hit trên host bằng phép quét thể tích thân xe từ pose trước đến pose hiện tại, kiểm tra collider chó và vật cản theo thứ tự gặp. Xác nhận va chạm khi quỹ đạo thực chạm chó, với ngưỡng tốc độ tương đối và cooldown theo cặp xe–chó để tránh đánh liên tiếp khi còn chồng collider. Dùng CCD cho Rigidbody xe và kiểm tra ca tốc độ cao trong Play Mode.
- Dùng chung đường áp dụng impact của [NetworkDogPlayer](../Assets/Game/Scripts/Fusion/NetworkDogPlayer.cs). Va nhẹ tạo knockback, va đủ mạnh làm chó gục theo trạng thái sẵn có. Cả xe giao thông và xe chó lái đều có thể đâm chó đi bộ. Loại người lái khỏi truy vấn hit của chính xe mình.

## 4. Xe chó lái

| Trạng thái | Hành vi |
| --- | --- |
| Đỗ, ghế trống | Spawn ở điểm ngoài tuyến giao thông. Xe giữ phanh, có collider và khối lượng lớn. HUD hiện thao tác vào xe khi chó đủ gần cửa. |
| Có người lái | Host giữ ID người lái, trạng thái ghế và pose xe. Chó theo điểm ghế, dừng mô phỏng đi bộ và bỏ va chạm gây tự đẩy xe khi đang ngồi. Input di chuyển chuyển sang ga, lùi/phanh và đánh lái. Camera máy người lái theo xe. |
| Xuống xe | Cho xuống khi tốc độ đủ thấp và có vị trí đứng hợp lệ ở một bên xe. Host kiểm tra nền và khoảng trống bằng kích thước collider chó. Nếu cả hai bên bị chắn, giữ chó trên ghế và hiện lý do. Xe phanh khi ghế trống. |
| Mất kết nối, xe kẹt hoặc rơi khỏi map | Host giải phóng ghế trước khi despawn avatar. Xe phanh hoặc trở về điểm đỗ an toàn khi cần reset. Nếu có người lái trong lúc reset, đưa chó ra điểm an toàn và khôi phục điều khiển đi bộ. |

Đề xuất dùng **E** để vào/ra, **WASD** để lái. Khi đi bộ và đang ngoạm đồ, **E** vẫn thả đồ trước. Chó rảnh miệng, đứng đủ gần cửa của xe đỗ, xe đủ chậm và ghế trống mới được vào. Hai người tranh ghế trong cùng tick chỉ có một người thành công. Xe dùng prefab riêng với `NetworkObject`, `NetworkTransform`, Rigidbody và điểm ghế, cửa, thoát. Luồng tìm mục tiêu ngoạm bỏ qua xe vì xe không thuộc danh sách carryable.

## 5. Ngoạm và đẩy đồ

- Chuyển cọc tiêu, thùng rác, ghế rời, hàng rào di động, chậu cây và các nhóm đồ rời còn lại sang prefab có collider, Rigidbody và [NetworkCarryable](../Assets/Game/Scripts/Fusion/NetworkCarryable.cs). Author điểm ngoạm theo model. Giữ chuột trái nhặt/ném và **E** thả. Kiểm tra khoảng trống lúc thả đồ lớn và dùng điểm thả dự phòng hợp lệ nếu điểm trước mõm bị chắn.
- Bổ sung nhánh đẩy vật từ `DogMotor.OnControllerColliderHit`. Lấy Rigidbody của collider bị chạm, lọc tiếp xúc ngang khi chó thực sự tiến về phía vật, gộp các hit vào cùng body trong một bước và áp xung lực có giới hạn trên host. Cùng một xung lực phải làm vật nhẹ tăng tốc nhiều hơn vật nặng. Vật đang được ngoạm và hình học cố định nằm ngoài nhánh này.
- Tinh chỉnh cùng khối lượng, ma sát và damping. Poop hiện có `mass = 0.18` và vòng đời 45 giây. Giữ quy tắc hết hạn và tạm dừng thời hạn khi được ngoạm. Bóng, xương, thùng cũ và đồ rời mới đều phải đẩy được qua cùng đường xử lý. Xe rất nặng nên một cú đẩy của chó chỉ có hiệu ứng nhỏ, trong khi va chạm từ xe khác vẫn có thể làm xe dịch chuyển.
- Dùng các điểm đặt đồ có ID theo map để lưu pose của vật bền vững. [WorldSaveSlots](../Assets/Game/Scripts/Fusion/WorldSaveSlots.cs) hiện lưu theo thứ tự mảng, nên tạo dữ liệu thành phố riêng theo ID để slot sân cũ không áp nhầm tọa độ sang map mới. Giữ ba slot. Poop, trạng thái ghế và tiến độ chạy tuyến thuộc phiên chơi. Khi reset, xe về điểm đặt ban đầu và đồ rời về pose thiết kế; xử lý người đang ngồi trước khi hồi xe.

## 6. Mạng và tải mô phỏng

- **Fusion Single và Host/Client** dùng cùng logic gameplay. Host hoặc runner Single quyết định ga, phanh, bám tuyến, chỗ ngồi, lực đẩy, hit và reset. Client gửi input hoặc yêu cầu tương tác. Host kiểm tra người gửi, ghế, khoảng cách và trạng thái hiện tại trước khi cập nhật. Client nội suy pose của xe và đồ, lấy pose ghế từ xe đang hiển thị và đổi camera cục bộ.
- Đồng bộ tối thiểu ID người lái, trạng thái ngồi và pose xe. Người vào muộn thấy đúng chó đang lái, xe đang chạy và đồ đang được ngoạm. Xử lý cleanup khi khách rời, host đóng phòng và runner dừng. Giữ một điểm cập nhật mô phỏng vật lý trên host để mỗi bước chỉ áp lực và xử lý hit một lần.
- Giữ mesh và collider tĩnh của thành phố trên từng máy. Chỉ spawn đối tượng mạng cho xe và đồ có trạng thái gameplay. Dùng Rigidbody sleep, collider đơn giản và truy vấn mục tiêu theo vùng để hạn chế chi phí. Đo từng nấc khi tăng số xe đang chạy và đồ động, trên toàn map đã có.
- Ghi số body đang thức, CPU physics của host, frame time, băng thông và độ trễ lái trong bản Windows. Chạy offline, hai máy và bốn người trước khi tăng mật độ xe. Dùng ngân sách PC hiện có làm mốc so sánh. [Kiến trúc PC](Specifications/PC_ARCHITECTURE.md)

## 7. Thứ tự triển khai và nghiệm thu

| Đợt | Bàn giao | Điều kiện nghiệm thu |
| --- | --- | --- |
| 1. Scene thành phố | Bản gameplay từ toàn bộ demo, collider tĩnh, spawn và luồng menu/session | Vào từ luồng chơi bình thường. Chó đi được qua các khu nối nhau, qua vỉa hè, không lọt mặt đường. Camera theo chó hoạt động đúng và hai máy nạp cùng bố cục. |
| 2. Đồ rời | Prefab ngoạm/đẩy, dữ liệu đặt đồ và lưu/reset theo map | Bóng, xương, thùng, poop và đồ phố cùng đẩy được. Với cùng cú đẩy, mẫu nhẹ tăng tốc hơn mẫu nặng. Nhặt, ném, thả, hết hạn poop và slot sân cũ chạy đúng. Hai người tranh một món chỉ có một người nhặt thành công. |
| 3. Xe giao thông | Tuyến qua waypoint, phanh theo loại vật cản, hit chó | Xe chạy nhiều vòng và rẽ đúng làn. Chó cắt ngang nhanh làm xe giảm nhẹ trước tiếp xúc và vẫn có thể bị đâm. Xe chạm chó ở tốc độ thử tối đa mà không lọt hit hoặc đánh lặp mỗi tick. Tường chặn xe và xe kẹt có đường phục hồi. |
| 4. Xe chó lái | Xe đỗ ngoài đường, điều khiển một ghế, camera và thoát an toàn | Xe đỗ không tự chạy. Chó vào và lái bằng input. Không ngoạm được xe ở mọi trạng thái. Thử hai người tranh ghế, xuống sát tường, đâm chó khác, tài xế mất kết nối và khách vào muộn. |
| 5. Hoàn thiện map | Phân loại các instance còn lại, tăng mật độ theo số đo và chốt bản PC | Toàn bộ bố cục demo có mặt trong build. Hai loại xe và đồ rời chạy cùng nhau, kết quả nhất quán giữa host và khách. Ghi cấu hình máy, số xe hoạt động, số người và số đo theo ngân sách PC trước khi chọn mật độ mặc định. |

Các giá trị tốc độ, khối lượng xe, khoảng cách phản ứng và ngưỡng gục là thông số playtest. Kết quả của bản triển khai được ghi ở mục 8.

## 8. Bản triển khai để playtest

- Mở **Tools > Dog Simulator > Open SimplePoly City Gameplay**, bấm **Play** rồi dùng menu **Singleplayer** hoặc **Multiplayer**. Scene đã được thêm vào Build Settings. Preview vẫn đứng đầu danh sách build chung trong thời gian nghiệm thu.
- Scene gameplay giữ 899 instance gốc. Thành phố spawn 104 xe và 97 đồ rời, gồm 94 món từ demo và ba món cũ là bóng, xương, thùng. Poop được sinh trong phiên. Ba slot thành phố lưu đồ theo ID và dùng không gian lưu riêng với sân preview.
- Hai xe thử chạy vòng kín phía tây nam, qua các giao lộ ở vùng (x = 0–60, z = 0–60). Hai taxi có ghế lái dùng mẫu tách bánh và thay đúng vị trí xe demo `Vehicle_Taxi (8)` và `Vehicle_Taxi (9)` trong hai bãi đỗ. Chó xuất phát trong khu đỗ phía đông, gần taxi số 8.
- `Vehicle_Car_color02 (3)` và `Vehicle_Car_color03` được chuyển lên vòng giao thông. Ba xe đỗ chắn vòng thử được dời sang sân bê tông phía tây. Tọa độ dưới đây là (x, y, z) trong Unity.

| Xe | Vị trí demo | Vị trí gameplay |
| --- | --- | --- |
| `Vehicle_Car_color02 (3)` | (-2.82, 0, 40.46) | (30, 0, -3.30), giao thông |
| `Vehicle_Car_color03` | (42.06, 0, -3.09) | (30, 0, 63.30), giao thông |
| `Vehicle_Truck_color02 (1)` | (34.41, 0, 63.44) | (13, 0.03, 84), đỗ |
| `Vehicle_Pick up Truck_color03` | (63.21, 0, 28.88) | (13, 0.03, 92), đỗ |
| `Vehicle_Bus_color01` | (17.82, 0, -3.28) | (21, 0.03, 100), đỗ |

- **E** vào hoặc ra khỏi taxi, và ưu tiên thả đồ khi đang ngoạm. **WASD** lái, thả ga để phanh. **R** khôi phục vị trí thiết kế của xe và đồ rời khi chơi offline hoặc là chủ phòng.

### Kiểm tra ngày 08/11/2026

- Unity 6000.3.19f1 đạt 46/46 Edit Mode và 20/20 Play Mode. Bài kiểm tra thành phố đi qua Fusion Single, xác nhận spawn, xe chạy và gây gục khi đặt tốc độ thử 15 m/s. Bài test dùng input bàn phím để vào taxi, lái, phanh, thử thoát khi cả hai bên bị chắn, rồi xuống ở bên đã thông.
- Windows Development chạy offline và qua Photon với hai rồi bốn tiến trình trên cùng một máy. Khách điều khiển taxi qua input, hai khách vào muộn nhận trạng thái ghế có người và host giải phóng ghế khi tài xế rời. Bản thử dùng Ryzen 7 7445HS, RTX 3050 Laptop 6 GB và RAM khoảng 16 GB. Host render Direct3D 12 ở 1280 × 720, giới hạn 60 FPS, khách chạy headless.

| Phiên Windows, mẫu 4 giây | Rigidbody đang thức cuối mẫu | p95 frame time host | Trung bình `Physics.Simulate` | Fusion vào / ra |
| --- | ---: | ---: | ---: | ---: |
| Offline | 2 | 16,67 ms | 0,230 ms | — |
| Host + 1 khách | 2 | 16,67 ms | 0,180 ms | 1.511 / 1.918 B/s |
| Host + 3 khách | 2 | 16,67 ms | 0,165 ms | 4.212 / 5.504 B/s |

- Một lần đo trên client cho kết quả 25 ms từ lúc gửi phím W đến khi input cục bộ nhận phím, 266 ms đến khi pose taxi dịch 2 cm và 417 ms đến 12 cm. Host giữ input lái gần nhất tối đa 150 ms nếu thiếu tick, rồi phanh.
- Mốc chuyển scene mặc định PC còn chờ nghiệm thu trên hai máy thật, kiểm tra các đoạn nối/vỉa hè trên toàn map và đo tải bản Release theo [ngân sách PC](Specifications/PC_ARCHITECTURE.md). Mẫu ngắn ở trên dùng để kiểm tra bản triển khai và phát hiện vấn đề cần đo lại.

## 9. Bản mở rộng xe và NPC ngày 09/11/2026

- Cả 104 xe dùng chung bộ điều khiển. Ba xe chạy tuyến gồm hai xe con và một bán tải. 101 xe đỗ có thể vào lái bằng **E** và **WASD**, kể cả SUV, taxi, xe cứu thương, xe cảnh sát, xe buýt và xe tải. So với bản trước, khối lượng của mỗi mẫu xe tăng khoảng ba lần. Phanh đỗ giữ xe khi chó tì vào, còn va chạm giữa các xe vẫn dùng vật lý.
- Thêm ba cụm bóng, xương và thùng ở các khu sân/bãi đỗ. Thành phố có 106 đồ rời đặt sẵn. **Chuột trái** nhặt/ném, **E** thả. Ba slot cũ vẫn đọc theo ID đã lưu và đồ mới xuất hiện ở vị trí thiết kế.
- Năm NPC đi qua lại trên NavMesh được bake từ collider thành phố. Ném trúng làm NPC khựng lại. **Chuột phải** cắn NPC ở phía trước và trong tầm. NPC bị cắn sẽ đuổi theo chó đã cắn, vung tay và có thể đánh ngã chó nếu còn trong tầm. NPC trở lại tuần tra khi mất mục tiêu hoặc hết thời gian truy đuổi.
- Phiên mới dùng phiên bản map `simplepoly-city-v2`. Chủ phòng và khách cần chạy cùng bản.
- Unity Editor đạt 21/21 Play Mode, gồm lái và xuống xe, bóng ném trúng NPC và NPC đánh ngã chó sau cú cắn. Game View xác nhận cả ba xe tự chạy. Edit Mode đạt 45/46, với bài kiểm tra catalog chó đang yêu cầu model 1002 mà catalog hiện tại thiếu.
