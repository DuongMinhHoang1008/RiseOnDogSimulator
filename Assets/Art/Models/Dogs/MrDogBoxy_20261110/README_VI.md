# MrDog Boxy

Bản thử dùng MrDog làm tham chiếu cho dáng Shiba, tai dựng, đuôi cuộn và màu lông. Ảnh SimplePoly City định hướng tỷ lệ và hình khối. Phần hông khuất và bụng được dựng theo đối xứng và màu lông quan sát được. Mẫu dùng hai màu thân và một vật liệu cho mặt.

Đầu và tai thu 8% quanh tâm đầu. Vùng cổ nối liền với lưới vai, mảng kem nhô lên ở giữa ngực, và phần đỉnh chân sau chia trọng số với xương hông.

- `model/MrDogBoxy_Rigged.blend` là file nguồn có rig và đèn preview. FBX và GLB cùng tên chứa hai mesh skinned và 7 clip Idle, Run, Jump, Bark, Bite, Poop và Pee.
- `FaceSurface` là mặt phẳng 4 đỉnh nằm khít với vỏ đầu. Mặt dùng trọng số 100% vào xương Head, UV0 phủ 0–1 và ảnh cam trơn 512 × 512. Cả ảnh MrDog và các chi tiết mắt, mũi, miệng đều được loại khỏi asset. Rig vẫn có xương hàm, còn hình mặt giữ phẳng khi Bark và Bite.
- `previews/` có ba góc Idle và bảng `motion.png`. Các hàng từ trên xuống lần lượt là Run, Jump, Bark và Bite.
- `validation/` có kết quả mở lại ba định dạng. Tất cả có 422 tam giác và 27 xương. BLEND và FBX có 233 đỉnh. GLB có 834 đỉnh sau khi tách thuộc tính tại các cạnh và ranh giới vật liệu.

Trong Unity 6000.3.19f1, mở cảnh `SimplePolyCity_Gameplay`, chọn **Singleplayer → MrDog Boxy**. Mục catalog 1103 dùng prefab `Unity/MrDogBoxy.prefab`, bộ override 7 clip và `DogFaceSurface` layout 1. **Customize Face** mở luồng ảnh và vẽ của game.

Dựng lại bằng Blender 5.2 với `--background --python scripts/build_mrdog.py`. Script đọc rig và dùng chung các hàm dựng mesh và xuất file từ thư mục anh em `BlankFaceDog_20261107`.
