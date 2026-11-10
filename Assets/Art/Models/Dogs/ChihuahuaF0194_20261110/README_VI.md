# Chihuahua F0194

Mẫu soft low-poly từ ảnh F0194 với tai lớn dựng đứng, mõm ngắn, ngực sáng và chân thon. Đuôi cong nhẹ và phần thân khuất được dựng ước lượng từ một góc ảnh.

- `model/` chứa BLEND chỉnh sửa được, FBX và GLB có skin và animation, cùng texture mặc định `BlankFace_Default.png`. Cả ba định dạng có 602 tam giác và 27 xương. BLEND và FBX có 349 đỉnh. GLB có 1.154 đỉnh sau khi tách thuộc tính ở các cạnh và vùng vật liệu.
- Các clip ở 30 fps gồm Idle (1–61), Run (1–25), Jump (1–43), Bark (1–37), Bite (1–28), Poop (1–109) và Pee (1–97).
- `FaceSurface` dùng một material với texture trơn 512 × 512. UV0 chiếu từ trước, U sang phải và V lên trên. Căn môi ảnh tại khe dưới mõm, ở khoảng V = 0,19. Ảnh tham chiếu chỉ xuất hiện trong ảnh kiểm tra miệng.
- `previews/` có các góc chính diện, bên, ba phần tư và cận mặt. `motion_review.png` có Run ở hai hàng trên và Jump ở hai hàng dưới. `mouth_review.png` hiển thị Idle, Bark và Bite từ trái sang phải.
- `validation/` ghi kết quả mở lại ba định dạng, gồm clip, skin, UV, texture và khoảng hở mép ghép qua các khung hình.

Trong Unity 6000.3.19f1, mở cảnh `SimplePolyCity_Gameplay` rồi chọn **Singleplayer → Chihuahua F0194 → Play**. Mẫu dùng ID 1105, visual prefab `Unity/ChihuahuaF0194.prefab` và bộ override bảy clip. Clip Jump gameplay giữ chuyển động chân và bỏ tịnh tiến của xương Root để bộ điều khiển nhân vật xử lý quỹ đạo nhảy. Trong **Customize Face → Adjust**, căn môi ảnh vào khe dưới mõm.

Đã thử chọn mẫu từ menu, spawn trong Singleplayer, áp dụng ảnh JPEG căn môi và khôi phục mặt mặc định. Đã lấy mẫu Idle, Bark và Bite trên mesh nhập vào Unity. Bộ `CustomDogFaceTests` đạt 8/8 và `M2CatalogReservationTests` đạt 3/3.

Chạy `scripts/build_chihuahua.py` bằng Blender 5.2 ở chế độ background để dựng lại. Script dùng rig của Blank Face Dog và các hàm dựng mesh của Tan Muzzle Dog. Chạy `scripts/verify_chihuahua.py` với `-- --validator <validate_dog_asset.py> --reference <ảnh F0194>` để tạo lại báo cáo và các ảnh kiểm tra.
