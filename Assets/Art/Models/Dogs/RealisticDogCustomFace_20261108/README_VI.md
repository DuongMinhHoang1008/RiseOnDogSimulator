# Realistic Dog Custom Face v1

Biến thể từ `Assets/Art/ReferenceDog/realistic_low_vertex/Dog_Rigged.fbx`. `FaceSurface` gồm 428 tam giác tách trực tiếp từ bề mặt đầu. Hai lưới dùng cùng vị trí và trọng số skin tại 44 đỉnh mép. UV0 chiếu theo trục X/Z của model trong Blender, với U tăng từ trái sang phải khi nhìn chính diện và V tăng từ dưới lên.

Màu mặc định là PNG nâu sáng đồng nhất 512 × 512. Độ nổi của mũi, môi và nét góc cạnh nhẹ ở viền đến từ hình học gốc. Khi sủa hoặc cắn, ảnh người chơi biến dạng cùng hàm. Lòng miệng và lưỡi được lùi nhẹ vào trong để xử lý phần xuyên qua cằm trong FBX nguồn.

## Tệp model

- [BLEND chỉnh sửa được, có texture đóng gói](model/RealisticDogCustomFace_Rigged.blend)
- [FBX skinned và 7 take hoạt ảnh](model/RealisticDogCustomFace_Rigged.fbx)
- [GLB skinned và 7 hoạt ảnh](model/RealisticDogCustomFace_Rigged.glb)
- [Màu mặc định](model/BlankFace_Default.png), [base color](model/Dog_BaseColor.png) và [normal map](model/Dog_Normal.png)

## Kiểm tra bằng Blender 5.2.3

| Định dạng | Đỉnh | Tam giác | Kết quả |
| --- | ---: | ---: | --- |
| BLEND | 6.349 | 12.526 | [PASS](validation/blend.json) |
| FBX nhập lại | 6.349 | 12.526 | [PASS](validation/fbx.json) |
| GLB nhập lại | 8.136 | 12.526 | [PASS](validation/glb.json) |

GLB tách thêm đỉnh ở ranh giới UV, pháp tuyến và vật liệu. Cả ba bản có 27 xương và tối đa 4 trọng số xương trên mỗi đỉnh. Kiểm tra round-trip xác nhận UV0 phủ 0–1 và độ lệch tại các cặp đỉnh mép bằng 0 ở các mẫu hoạt ảnh.

Các clip ở 30 fps trong bản BLEND gồm Idle (1–61), Run (1–25), Jump (1–43), Bark (1–37), Poop (1–109), Pee (1–97) và Bite (1–28).

- [Chính diện](previews/front.png), [nhìn ngang](previews/side.png), [góc 3/4](previews/hero.png) và [cận cảnh](previews/face_angle.png)
- [JPEG 512 × 512 kiểm tra chiều UV trong Blender](previews/face_uv_probe.png)
- Các khung hình mẫu của [Run](previews/Run_motion_review.png), [Jump](previews/Jump_motion_review.png), [Bark](previews/Bark_motion_review.png) và [Bite](previews/Bite_motion_review.png)
- [Thông số model](asset_report.json)

## Trong game

Model này thay visual cũ của **Reference Dog (fallback)** trong catalog, giữ ID `1`. [Visual prefab](Unity/RealisticDogCustomFace.prefab) dùng FBX với rig Generic và gắn `DogFaceSurface` layout 1 vào skinned renderer của mặt. Cả prefab `DogPlayer` và scene `M1_LocalPreview` dùng visual mới.

Animator Override giữ đủ 7 clip của bản chuyển đổi. Bản Jump dùng trong game giữ các pose co chân và tiếp đất, đồng thời cố định tịnh tiến của xương `Root` để `DogMotor` điều khiển độ cao.

Unity 6000.3.19f1 đã chạy 10 bài EditMode và 6 bài PlayMode, tất cả đều đạt. [Ảnh kiểm tra từ màn hình Customize Face](previews/unity_face_probe.png) dùng JPEG 512 × 512 tổng hợp. Kiểm tra PlayMode cũng xác nhận `DogPlayer` nhận JPEG và trả về đúng texture mặc định. Console của lần thử giao diện ghi nhận 0 lỗi và 0 cảnh báo. Bộ test Fusion có một cảnh báo khi runner thử nghiệm khởi động với scene trống.
