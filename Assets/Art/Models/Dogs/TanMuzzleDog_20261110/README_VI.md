# Tan Muzzle Dog

Mẫu low-poly từ ảnh chó lông vàng nâu, giữ dáng bụng thon, ngực rộng, chân dài, tai chếch và đuôi cong về trước. Mặt còn khối trán, má, sống mõm và cằm. Vùng gắn ảnh dùng một màu trơn mặc định. Khe miệng nằm trong vùng ảnh để phần ảnh trên môi dưới chuyển động cùng hàm. Hông khuất, lưng và bụng được dựng đối xứng từ ảnh một góc.

- Bản chỉnh thu đầu còn 90%, kéo nhẹ mõm, giữ cỡ tai và thu bề ngang lẫn bề sâu chân 12%. Thân dài hơn một chút, bụng nhấc lên sau ngực, hông hẹp hơn và đuôi có thêm một đoạn chuyển. `FaceSurface` đi theo đầu mới và giữ nguyên bố cục UV.
- `model/` có BLEND nguồn, FBX và GLB skinned, cùng PNG trơn 512 × 512. Mỗi định dạng có 546 tam giác, 27 xương và đủ clip Idle, Run, Jump, Bark, Bite, Poop, Pee từ rig gốc. BLEND và FBX có 321 đỉnh. GLB có 1.058 đỉnh sau khi tách thuộc tính ở các cạnh và vùng vật liệu.
- `FaceSurface` dùng chung một texture cho môi trên và môi dưới. Môi dưới chia trọng số giữa Head và Jaw để giảm độ há. Khe miệng hẹp nằm ngay dưới chóp mõm tại V ≈ 0,22. UV0 chiếu thẳng từ trước, U sang phải và V lên trên. Vùng hữu dụng có viền theo đầu chó nên các góc ngoài viền của ảnh không hiển thị. Ảnh có thể trông khác khi nhìn nghiêng do mõm nhô.
- `previews/` có góc chính diện, ngang, ba phần tư, cận mặt và bảng chuyển động. Các hàng của bảng lần lượt là Run, Jump, Bark và Bite.
- `validation/` có báo cáo mở lại cả ba định dạng, gồm hướng UV, trọng số, texture và kiểm tra mép mặt qua từng khung hình được lấy mẫu.

Trong Unity 6000.3.19f1, mở cảnh `SimplePolyCity_Gameplay` và chọn **Singleplayer → Tan Muzzle Dog → Play**. Mẫu dùng ID 1104, prefab `Unity/TanMuzzleDog.prefab` và bộ override 7 clip. Trong **Customize Face → Adjust**, căn đường giữa hai môi của ảnh trùng với khe trên mõm. Khi Bark hoặc Bite, phần ảnh bên dưới khe sẽ đi theo hàm dưới.

Dựng lại với Blender 5.2 bằng `--background --python scripts/build_tan_muzzle_dog.py`. Script dùng rig và các hàm dựng mesh của thư mục anh em `BlankFaceDog_20261107`.
