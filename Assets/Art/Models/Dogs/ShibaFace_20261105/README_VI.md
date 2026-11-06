# Shiba mặt người — model rigged

Model được dựng từ một ảnh tham chiếu, theo trục `-Y` forward / `+Z` up trong Blender. Phần khuất được ước lượng vì ảnh chỉ có một góc nhìn. Khuôn mặt dùng ảnh chiếu lên bề mặt 3D lồi có relief; kính có mesh riêng và đi theo xương đầu.

## File chính

- `model/Dog_Rigged.blend`: file Blender có texture đã pack, rig và mọi Action.
- `model/Dog_Rigged.fbx`: mesh, skin, texture nhúng và 7 clip đã bake.
- `model/Dog_Rigged.glb`: mesh, skin, texture nhúng và 7 clip đã bake.
- `model/Dog_BaseColor.png`, `model/Dog_Normal.png`: texture 2048 × 2048 dùng riêng.
- `asset_report.json`: thống kê hình học, rig và clip.
- `validation/*.json`: kết quả kiểm tra native và round-trip export.
- `previews/Run.gif`, `previews/Jump.gif`: xem nhanh chuyển động.

## Thông số

- 9.948 vertex hình học, 19.728 triangle trong `.blend`/FBX.
- GLB import lại có 12.541 vertex do tách ở UV/normal; số triangle giữ nguyên.
- 27 xương, tối đa 4 influence/vertex; không có vertex mất weight.
- Kích thước xấp xỉ 0,483 × 1,296 × 1,181 m.
- 7 clip ở 30 fps: `Idle`, `Run`, `Jump`, `Bark`, `Poop`, `Pee`, `Bite`.
- `Idle` và `Run` loop kín; mọi chuyển động đã bake, không phụ thuộc constraint lúc runtime.

## Nghiệm thu

Validator của skill đã PASS cho `Dog_Rigged.blend`, `Dog_Rigged.fbx` và `Dog_Rigged.glb`, không có warning. Run và Jump đã được render toàn chu kỳ; Bark/Bite được kiểm tra ở pose mở hàm, Pee ở pose nhấc chân và Poop ở pose crouch.

## Giới hạn

Chỉ có một ảnh nên lưng, mặt sau đầu, mặt trong chân và phía khuất của đuôi là ước lượng. Ánh sáng và chi tiết của ảnh vẫn nằm trong texture khuôn mặt. Model chưa được gắn vào prefab/controller Unity vì yêu cầu hiện tại chỉ là tạo asset có animation.
