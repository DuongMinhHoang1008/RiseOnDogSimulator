# Reference Dog — Rig và animation cơ bản

Hai bản model đã thêm cùng một bộ xương **27 bone**, skin tối đa **4 trọng số/vertex**, action bake **30 fps**.
Phần mặt đã sửa ở V2 được giữ lại; thêm khe miệng, hàm dưới, khoang miệng, lưỡi và răng để sủa/cắn.

## File nên dùng

- low_poly/Dog_Rigged.blend, Dog_Rigged.fbx, Dog_Rigged.glb.
- realistic_low_vertex/Dog_Rigged.blend, Dog_Rigged.fbx, Dog_Rigged.glb.
- File Dog.blend/FBX/GLB là bản tĩnh trước khi rig, vẫn được giữ riêng.
- Mesh Blender: low-poly **1782 vertex / 3494 triangle**; bản mượt **6305 vertex / 12526 triangle**.
- Khi Unity tách cạnh UV/normal, số vertex GPU khác số vertex hình học; xem unity_rig_verification.json.

## Các clip

| Clip | Thời lượng | Hành động | Loop |
| --- | ---: | --- | --- |
| Idle | 2.0s | Thở nhẹ, đuôi đung đưa | Có |
| Run | 0.8s | Chạy tại chỗ theo cặp chân chéo | Có |
| Jump | 1.4s | Lấy đà, bật lên, co chân, tiếp đất | Không |
| Bark | 1.2s | Hai nhịp sủa: đầu/cổ và há hàm | Không |
| Poop | 3.6s | Hạ thân, gập chân sau, giữ rồi đứng lên | Không |
| Pee | 3.2s | Nhấc chân sau phải, giữ rồi hạ xuống | Không |
| Bite | 0.9s | Lấy đà, há hàm và cắn chụp | Không |

Các clip chỉ gồm chuyển động; không có âm thanh, nước tiểu, phân, hitbox hay logic gây sát thương.
Đây là animation cơ bản, chưa phải bộ chuyển động đã polish để phát hành.

## Dùng trong Unity

Prefab dùng được ngay:

- Unity/Prefabs/ReferenceDog_low_poly_Rigged.prefab.
- Unity/Prefabs/ReferenceDog_realistic_low_vertex_Rigged.prefab.

Controller chung: Unity/ReferenceDog.controller. Rig dùng **Generic**, không dùng Humanoid.
Material đã chuyển sang URP/Lit; texture normal bản mượt đã được đặt đúng kiểu Normal Map.
Hai prefab dùng cùng các clip của bản low-poly vì có cùng hệ xương và đường dẫn transform.

Mở **Unity/ReferenceDog_AnimationPreview.unity**, nhấn Play. Hai model tự chạy lần lượt các clip.
Nhấn phím trong Game view để chọn: **1 đứng, 2 chạy, 3 nhảy, 4 sủa, 5 ỉa, 6 đái, 7 cắn**.
Chọn thủ công sẽ dừng vòng trình diễn tự động.

Trong code gameplay: Animator.SetFloat("Speed", speed) để chuyển Idle/Run;
Animator.SetTrigger("Jump"/"Bark"/"Poop"/"Pee"/"Bite") để chạy hành động rồi về Idle.
applyRootMotion = false. Clip Jump có độ nâng thân dùng cho demo; khi ghép với DogMotor đang tự điều khiển nhảy,
cần phối hợp phần dịch chuyển dọc để không cộng hai lần độ cao.
Component ReferenceDogAnimationPreview chỉ phục vụ trình diễn, không gắn vào player mạng đang chạy.

Menu tái tạo/kiểm tra: **Tools > Dog Simulator > Reference Dog**.
Scene gameplay và prefab player hiện tại không được thay model tự động trong lần này.

## Xem trước và kiểm tra

- previews/animation_showcase.mp4: video viewport xem thứ tự 7 clip.
- previews/animation_overview.png: ảnh render các tư thế tiêu biểu.
- rig_animation_report.json: trọng số và mẫu biến dạng trong Blender.
- rig_export_verification.json: đã nhập lại FBX/GLB, đủ 27 xương và 7 clip ở cả hai bản.
- unity_rig_verification.json: Generic avatar, các clip, loop và skin được kiểm tra lại trong Unity thật.
- scripts/rig_and_animate.py: tái tạo rig và clip từ hai file model tĩnh.

Unity CLI Pipeline được cài để kiểm tra trong Editor: com.unity.pipeline 0.10.0-exp.1.