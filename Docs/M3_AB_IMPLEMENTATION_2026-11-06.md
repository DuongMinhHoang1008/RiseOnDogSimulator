# M3-A/M3-B — triển khai sandbox tương tác

**Ngày:** 06/11/2026, Asia/Saigon
**Phạm vi:** ngoạm + đồ vật + sân nhỏ, sau đó chồng chó. NPC, ragdoll và xe chưa triển khai.

> **Ghi chú lịch sử:** tài liệu này ghi trạng thái triển khai trung gian ngày 06/11. Mapping input, cơ chế support và các bản vá gameplay cuối đã thay đổi; dùng [biên bản nghiệm thu ngày 07/11](M3_AB_ACCEPTANCE_2026-11-07.md) làm trạng thái hiện hành.

## Đã triển khai

- Sân blockout 50×50 m trong `M1_FusionPreview`: hàng rào, bục thấp, dốc, bệ co-op và khung mục tiêu bóng.
- Năm vật network spawn bởi Master: một xương, một bóng lăn và ba thùng. `E` ngoạm/thả vật gần nhất; `R` reset vật về vị trí đầu.
- Request tương tác dùng replicated state kèm sequence và ack trong registry; không dùng lại đường RPC từng gây `MethodAccessException`.
- Mỗi vật chỉ có một carrier. Vật đang mang bám `MouthAnchor`, tắt collider/physics; khi free chỉ authority mô phỏng bóng và `NetworkTransform` phát pose.
- Registry và vật là `MasterClientObject`, giữ state qua Master handoff; request của player rời được dọn khỏi bảng ack.
- `DogPlayer` có `BackSupport` riêng. `DogMotor` nhận delta tịnh tiến/xoay của mặt đỡ, chặn chu trình support và giới hạn prototype tối đa ba tầng.
- Game version đổi thành `office-m3-sandbox-stack-v1` để không ghép nhầm client M2.

## Kiểm tra trong lượt triển khai

- Unity compile sạch sau khi tạo lại prefab/scene.
- EditMode: **37/37 pass**.
- PlayMode baseline sau thay đổi scene/prefab: **17/17 pass**. Test input `E/R` được thêm sau lượt này và cần chạy lại cùng suite.
- Chưa đánh dấu gate multiplayer hoàn tất: cần chạy hai process/Web client để thử tranh cùng vật, disconnect/Master handoff và chó dưới di chuyển/xoay khi đang đỡ chó trên.

## Điều khiển

- `E`: ngoạm hoặc thả.
- `R`: reset toàn bộ đồ trong sân.
- Chuột phải vẫn là animation cắn; hai hành động không dùng chung input.

## Bản vá phản hồi playtest

- Spawn được rải theo các vị trí vòng quanh spawn point để CharacterController không chồng lên nhau.
- Bỏ giới hạn model độc quyền; nhiều người được chọn cùng một loại chó.
- Mọi vật khi thả đều chuyển về dynamic Rigidbody, kế thừa vận tốc đo trực tiếp tại `MouthAnchor` trên cả ba trục và nhận `VelocityChange` hướng tới + hướng lên.
- Chó trên được parent cứng trực tiếp vào `BackSupport`; không còn thuật toán follow/hard-snap mỗi frame. Parent chỉ được tháo khi nhảy, đi khỏi lưng, respawn hoặc support mất.
