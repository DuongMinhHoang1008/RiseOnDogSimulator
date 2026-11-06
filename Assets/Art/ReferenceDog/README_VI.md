# Hai model chó theo ảnh tham chiếu

Tạo bằng Blender 5.2.3. Giữ thân chó, tai nhọn, đuôi cuộn và khuôn mặt người như ảnh người dùng cung cấp.

Cập nhật mặt V2 theo source/face_reference.png: bỏ hai khối má phồng, thu gọn hàm và đưa mặt ra trước.
Ảnh cận kiểm tra: previews/face_comparison.png và previews/face_angle_comparison.png.

| Phiên bản | Vertex mesh | Triangle | Texture |
| --- | ---: | ---: | --- |
| Low-poly, flat shading | 1530 | 3048 | Base Color 1024 × 1024 |
| Hướng chân thực, smooth shading | 6010 | 12008 | Base Color + Normal 2048 × 2048 |

Mỗi phiên bản có Dog.blend, Dog.fbx, Dog.glb và các texture PNG. Blender đã đóng gói texture; GLB và FBX cũng chứa texture.
Mỗi nhân vật là một mesh object, một material. Hai tai là các phần mesh kín giao với đầu.
Số vertex trong engine có thể tăng vì tách cạnh UV và normal; bảng trên đếm vertex hình học trong Blender.

## Xem và sử dụng

- Mở low_poly/Dog.blend hoặc realistic_low_vertex/Dog.blend trong Blender.
- Camera và đèn studio chỉ có trong file BLEND. Mesh nằm trong collection MODEL_export_this.
- Ảnh so sánh: previews/comparison.png; mỗi phiên bản có ảnh hero, trước, bên và sau.
- Để đưa vào Unity, copy FBX cùng các texture PNG của phiên bản muốn dùng vào một thư mục trong Assets.
- Với URP, tạo material URP/Lit, gán Dog_BaseColor.png vào Base Map, Metallic = 0, Smoothness khoảng 0.17.
- Với bản mượt, đặt Texture Type của Dog_Normal.png thành Normal map rồi gán vào Normal Map, Strength khoảng 0.35.
- Đơn vị mét, gốc tại mặt đất giữa nhân vật. Điều chỉnh scale theo scene của game.

## Phạm vi và giới hạn

Đây là model tĩnh, chưa rig và chưa có animation. Hình dáng bên hông/sau được ước lượng từ một ảnh.
Khuôn mặt có khối nổi mũi, má, môi và texture chiếu từ ảnh; ánh sáng vốn có trong ảnh vẫn nằm trong texture mặt.
Bản hướng chân thực dùng texture lông và normal để giữ số vertex thấp; không dùng lông sợi hoặc hair cards.
Đây là bản dựng gần theo ảnh, chưa phải bản quét 3D hay asset đã kiểm thử animation trong game.

## Kiểm tra

Đã mở lại file BLEND và nhập lại FBX/GLB trong Blender. Triangle khớp bản gốc, UV hợp lệ, texture đóng gói đầy đủ.
Chi tiết: model_stats.json và verification.json. Script tái tạo: scripts/build_reference_dog.py.
Ảnh gốc nằm trong source/reference.png. Không thay prefab hay scene gameplay của dự án.