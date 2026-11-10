# Biên bản nghiệm thu P0 — Xương Vàng và SimplePoly City

**Ngày ghi nhận** · 09/11/2026, giờ Việt Nam  
**Bản ghi nhận** · Unity 6000.3.19f1, Fusion Single trong Editor, map `simplepoly-city-v4`  
**Kết luận** · Đạt 4/4 ca Play Mode của thành phố trong Fusion Single. Nghiệm thu P0 trên Windows Host/Client và đánh giá hiệu năng phát hành còn mở.

Phạm vi gồm P0-A và P0-B theo [kế hoạch gameplay](GOLDEN_BONE_AND_CITY_GAMEPLAY_PLAN_VI.md), cùng đợt điều chỉnh bố trí khu dân cư, hành vi bảo vệ và thời gian chó nằm. Scene bàn giao là [SimplePolyCity_Gameplay](../Assets/Game/Scenes/SimplePolyCity_Gameplay.unity).

## Phạm vi bàn giao

| Hạng mục | Bản bàn giao |
| --- | --- |
| Xe và giao thông | 104 xe, trong đó 6 xe tự chạy trên hai vòng. Khối lượng xe tăng 1,5 lần. Mục tiêu tốc độ là 12 m/s trên đoạn thẳng và 5 m/s ở cua. |
| Bố trí Xương Vàng | Bệ lấy xương sát dãy nhà phía bắc, cách spawn chính khoảng 129 m. Chuồng giao đặt trên sân lát sẵn của một căn nhà phía đông, cách bệ khoảng 79 m. Hai khoảng cách đo theo đường thẳng trên mặt bằng. HUD hiện hướng và khoảng cách đến mục tiêu hiện tại. |
| NPC và bảo vệ | 16 NPC tuần tra và 3 bảo vệ. Khi chó lấy xương, bảo vệ truy đuổi. Bảo vệ nhặt xương đã rơi trong tầm, có thể tự mở cổng để đi về bệ, và làm rơi xương khi bị đánh ngã. |
| Ngoạm và hồi phục | Đồ vật, chó và NPC dùng chung một chỗ mang. **E** đặt xuống, chuột trái ném đồ hoặc NPC đang mang, **Space** để chó được mang tự thoát. Thời gian chó nằm sau khi đã ổn định được chỉnh từ 3 giây xuống 1,35 giây. |
| Chơi lại và lưu phiên | **F6** hồi xương, **F7** reset nhiệm vụ, **R** reset sandbox. Các slot thành phố đọc theo namespace `simplepoly-city-v1`. |

## Kết quả kiểm thử

| Ca Play Mode trên bản v4 | Kết quả |
| --- | --- |
| Xương Vàng | Đạt. Kiểm tra thùng mở cổng, cổng chờ chó rời lối, chó mang xương bật qua rào, bảo vệ truy đuổi và mang xương rơi về bệ, người chơi lấy lại để giao, xương cắt qua vùng giao giữa hai mẫu, và ba phạm vi reset. |
| Xe và taxi | Đạt. Kiểm tra đủ số xe, cả sáu xe bắt đầu chạy tuyến, va chạm xe làm chó ngã rồi hồi phục, vào ghế, lái, phanh và xuống xe qua lối còn trống. |
| NPC tuần tra | Đạt. NPC di chuyển theo tuyến, khựng khi trúng bóng ném và phản công sau khi bị cắn. |
| Xe đâm NPC và ngoạm NPC | Đạt. NPC ngã, nằm ổn định, được ngoạm và ném, sau đó đứng dậy trên NavMesh. |

Báo cáo v4 ghi **4 đạt, 0 lỗi**. Ở lượt hồi quy P0 trước đợt sửa khu dân cư, bộ Play Mode đạt **22/23** trong cấu hình mặc định. Ca còn lại cần scene Preview cũ và đã đạt khi bật tạm scene này. Các kết quả thuộc những lượt chạy khác nhau. [Play Mode v4](../Logs/GoldenBone-Neighborhood-PlayMode.json) · [Play Mode P0 trước điều chỉnh](../Logs/GoldenBone-Final-PlayMode.json) · [Kiểm tra mở lại sandbox cũ](../Logs/GoldenBone-LegacySandbox.json)

## Số đo xe ở lượt P0 trước đợt sửa khu dân cư

| Kịch bản | Kết quả trong Editor |
| --- | --- |
| Sáu xe trên hai vòng trong 600,1 giây mô phỏng | Mỗi xe hoàn tất khoảng 18–22 vòng, tốc độ cực đại 12,1–12,3 m/s. Nhật ký không ghi nhận xe kẹt hoặc hồi xe trong kịch bản này. |
| Taxi và xe buýt trên nền phẳng, một rồi hai chó tì trong 3 giây | Cả bốn lượt ghi 0,0000 m theo độ phân giải báo cáo, đạt ngưỡng tối đa 0,10 m. Ở lần lấy mẫu lúc nghỉ, 98/98 xe đỗ đã ngủ vật lý. |

Lượt chạy giao thông dùng thời gian mô phỏng tăng tốc và mất khoảng 150 giây thực. Hai số đo trên thuộc cấu hình trước đợt tăng NPC. [Nhật ký giao thông](../Logs/GoldenBone-TrafficSoak.txt) · [Nhật ký xe đỗ](../Logs/GoldenBone-ParkProbe.txt)

## Các hạng mục còn mở

| Hạng mục | Điều kiện nghiệm thu |
| --- | --- |
| Windows Host/Client và late join | Chơi thử 2–4 người trên ít nhất hai máy. Kiểm tra đồng bộ xương, cổng, bảo vệ, tranh quyền ngoạm, thoát mang sát tường, người chơi rời phòng và khách vào muộn. |
| Ma trận va chạm | Bổ sung các ca ném vật vào xe đỗ, xe–xe, xe lùi, va quệt, NPC sau tường và điểm hồi xe bị chiếm. Ghi kết quả trên host và quan sát từ client. |
| Đo tải Windows | So cấu hình 3 và 6 xe trên cùng máy và preset với bố trí NPC hiện tại. Ghi p95 frame time, chi phí mô phỏng host, Rigidbody thức, băng thông, độ trễ ngoạm và số lần hit lặp, kẹt hoặc hồi xe. |
| P1 Giao hàng rơi vãi | Bắt đầu sau khi P0 qua chơi thử theo kế hoạch. |

Nhật ký trong `Logs/` là dữ liệu cục bộ của workspace. Trong lần khởi động phép đo giao thông P0, slot 3 thành phố của Unity Editor có thể đã bị ghi lại. Giá trị trước lượt đo chưa được xác minh.
