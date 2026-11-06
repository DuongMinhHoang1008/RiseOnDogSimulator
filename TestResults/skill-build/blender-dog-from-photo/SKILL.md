---
name: blender-dog-from-photo
description: "Create or refine a textured 3D dog from reference images in Blender, including a quadruped rig and basic animation clips. Use for photo-to-dog modeling and revisions, including meme or human-faced dogs when present in the reference; not for standalone pictures or animated desktop pets."
---

# Chó 3D từ ảnh, có rig và animation

Tạo **một model mượt, hướng chân thực và tiết kiệm vertex**. Không tạo thêm bản low-poly hoặc biến thể thứ hai trừ khi người dùng yêu cầu. Đầu ra phải có mesh 3D thật, có thể chỉnh sửa; ảnh render không thay thế model.

## Đầu vào và phạm vi

- Xem ảnh tham chiếu và ảnh cắt bổ sung trước khi dựng. Nếu chưa có ảnh, yêu cầu ảnh; nếu chỉ có một góc, dựng ước lượng các phần khuất và nói rõ giới hạn đó.
- Giữ giống, dáng, màu lông, tai, đuôi và tỷ lệ của chủ thể. Chỉ giữ mặt người/yếu tố meme khi có trong ảnh hoặc được yêu cầu; không áp mặt người lên chó thông thường.
- Dùng Blender executable người dùng chỉ định. Trên máy này, thử `D:\Blender 5.2\blender.exe` nếu còn tồn tại; đọc phiên bản thực tế. Không phụ thuộc một project hoặc model cũ trong chat để skill hoạt động.
- Dựng trong thư mục đầu ra riêng của workspace. Tạo rig/animation và xuất file không đồng nghĩa với thay prefab gameplay, cài package hoặc sửa scene Unity. Chỉ tích hợp game khi được yêu cầu.

## Dựng và kiểm tra hình

Đọc [references/modeling.md](references/modeling.md) khi dựng hoặc sửa mesh/texture. Chốt silhouette và mặt trước khi rig; render chính diện, nghiêng và cận mặt để đánh giá thay vì chỉ tin vào số vertex.

Mặc định dùng smooth shading, texture màu và normal để tạo chi tiết lông, tránh lông sợi hoặc subdivision không áp dụng làm tăng mesh xuất ngoài dự tính. Với chó dùng cho game, khoảng 5–10 nghìn vertex hình học là điểm bắt đầu hợp lý, không phải hạn mức cố định. Ưu tiên giống ảnh và biến dạng tốt; đo lại ngân sách theo yêu cầu cụ thể.

Với mặt người ghép vào chó, kiểm tra riêng phần má, miệng và cằm: mặt phải nối tự nhiên vào đầu, không bị chìm giữa hai khối má. Với chó bình thường, giữ độ nhô mõm đúng giống. Ảnh cắt mới của người dùng là chuẩn cho lần sửa tương ứng.

## Rig và clip mặc định

Đọc [references/rig-animation.md](references/rig-animation.md) khi thêm xương, skin hoặc animation. Dùng hệ xương bốn chân, có đuôi, cổ/đầu và hàm; vị trí khớp bám mesh thực tế, không sao chép tọa độ của một con chó khác.

Nếu người dùng không chỉ định bộ clip khác, tạo:

| Clip | Chuyển động |
| --- | --- |
| Idle | Đứng chờ, thở/đuôi nhẹ; loop |
| Run | Chạy tại chỗ, chân phối hợp hợp lý; loop |
| Jump | Lấy đà, bật, co chân, tiếp đất |
| Bark | Cổ/đầu và mở–đóng hàm |
| Poop | Hạ thân, gập chân sau, giữ rồi đứng lên |
| Pee | Nhấc một chân sau, giữ rồi hạ xuống |
| Bite | Lấy đà, há hàm, cắn chụp rồi phục hồi |

Đây là animation; âm thanh, nước/phân, hitbox và sát thương là phạm vi riêng. Không thêm các hệ thống đó nếu chưa được yêu cầu.

## Xuất và nghiệm thu

- Lưu `.blend` đã pack texture, xuất `.fbx` và `.glb` có skin cùng các clip; giữ PNG màu/normal để dùng riêng. Nếu người dùng chỉ cần một định dạng, theo đúng yêu cầu đó.
- Xác minh đường dẫn ảnh sau khi di chuyển thư mục. File pack không bảo đảm exporter sẽ tìm đúng file ảnh bên ngoài: cập nhật đường dẫn và pack lại trước khi xuất.
- Bake chuyển động cần thiết thành keyframe; không để animation xuất ngoài phụ thuộc IK/constraint chưa được bake hoặc action slot không gắn đúng armature.
- Chạy helper dưới Blender trên bản `.blend` và bản xuất trong thư mục kiểm tra. Đọc cách gọi và giới hạn tại [references/validation.md](references/validation.md):

  `blender --background --factory-startup --python-exit-code 1 --python scripts/validate_dog_asset.py -- --input MODEL.blend --report validation.json`

- Kiểm tra ảnh các pose và ít nhất một chu kỳ chạy/nhảy. Xem video mẫu nếu đã tạo; không tuyên bố chuyển động tốt chỉ vì action có tên và frame range.
- Báo số vertex/triangle thực tế, số xương, danh sách clip, vị trí file và giới hạn còn lại. Phân biệt vertex hình học với vertex GPU có thể tách ở UV/normal.
- Nếu được yêu cầu gắn vào Unity, dùng rig **Generic** cho chó, nối với motor/input có sẵn, bỏ autoplay demo và tránh nhân đôi độ cao nhảy. Đọc phần tích hợp có điều kiện trong reference rig; không tự dựng lại game hay thay network architecture.
