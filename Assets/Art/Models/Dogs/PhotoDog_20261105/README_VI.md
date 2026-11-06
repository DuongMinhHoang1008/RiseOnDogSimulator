# PhotoDog — bàn giao model 3D

Model được dựng từ một ảnh chính diện hơi lệch. Mặt người, tai lớn, màu lông vàng cam và dáng chân được giữ theo ảnh. Lưng, đuôi và các vùng khuất là phần ước lượng vì không có ảnh cạnh/sau.

## File dùng chính

- `model/Dog_Rigged.blend`: mesh chỉnh sửa được, rig, texture đã pack và đủ action.
- `model/Dog_Rigged.fbx`: skin và toàn bộ animation, texture nhúng.
- `model/Dog_Rigged.glb`: skin, material/texture nhúng và toàn bộ animation.
- `model/Dog_BaseColor.png`, `model/Dog_Normal.png`: texture rời 2048 × 2048.
- `previews/animation_showcase.mp4`: video xem nhanh toàn bộ clip.

## Thông số

- Blender 5.2.3 LTS; đơn vị mét.
- 9.100 vertex hình học, 18.048 triangle, 27 xương.
- GLB round-trip có 11.895 vertex do tách vertex tại UV/normal/material.
- Tối đa 4 ảnh hưởng xương trên mỗi vertex.
- Miệng có khe hình học và khoang miệng; hàm là xương riêng.
- Hướng dựng trong Blender: tiến về `-Y`, trục lên `+Z`. FBX xuất với forward `-Z`, up `+Y`.

## Animation 30 fps

`Idle` 61 frame (loop), `Run` 25 frame (loop), `Jump` 43 frame, `Bark` 37 frame, `Poop` 109 frame, `Pee` 97 frame, `Bite` 28 frame.

Tất cả transform xương đã bake thành keyframe. Playback không phụ thuộc IK hay constraint runtime.

## Kiểm tra

Validator đã PASS cho `.blend`, `.fbx` và `.glb`: đủ action, texture có pixel, UV hợp lệ, trọng số chuẩn, tối đa 4 ảnh hưởng, và mesh thực sự biến dạng trong từng animation. Báo cáo nằm trong `validation/`.

## Giới hạn

Ảnh nguồn chỉ có một góc nên mặt bên, lưng và đuôi không thể là tái tạo chính xác. Ánh sáng trong ảnh chân dung và mẫu lông là một phần của base color. Model chưa được gắn vào prefab/controller Unity vì yêu cầu hiện tại chỉ là tạo asset 3D có animation.
