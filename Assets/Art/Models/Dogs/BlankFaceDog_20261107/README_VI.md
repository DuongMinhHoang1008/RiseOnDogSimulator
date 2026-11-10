# Blank Face Dog

Model chó hình khối dùng cho hệ thống mặt người chơi. Thân và chân giữ các mảng phẳng lớn, với bevel nhỏ ở cạnh và normal có trọng số trên dải vát. Đuôi giữ dáng cong, dùng thêm các vòng chuyển ở chỗ gập và pha trọng số xương khi vẫy. `FaceSurface` gồm mặt trước và các mặt vát của đầu, dùng chung đường viền tám đỉnh với phần đầu còn lại và có UV riêng để nhận JPEG 512 × 512 ở runtime.

## Asset chính

- `model/BlankFaceDog_Rigged.blend`: source Blender, texture đã pack, rig và 7 Action.
- `model/BlankFaceDog_Rigged.fbx`: model Unity sử dụng.
- `model/BlankFaceDog_Rigged.glb`: bản GLB có skin và animation.
- `Unity/BlankFaceDog.prefab`: prefab đã gắn `DogFaceSurface`.
- `previews/`: render chính diện, góc nghiêng và review toàn chu kỳ Run/Jump.
- `validation/`: báo cáo validator cho BLEND, FBX và GLB.

## Hợp đồng custom face

- Tên object: `FaceSurface`.
- Material slot đầu: `FaceSurface_Blank`; texture mặc định là `BlankFace_Default.png`.
- UV0 của vùng mặt phủ đủ hình chữ nhật 0–1, trái sang phải và dưới lên trên.
- Mép `FaceSurface` dùng chung vị trí vertex với vùng đầu đã tách, nên silhouette chính diện/nghiêng liên tục và không có khối chắn phía trước.
- `faceLayoutVersion = 1`, khớp `CustomDogFaceConstants.LayoutVersion`.
- Prefab có `DogFaceSurface.targetRenderer` trỏ thẳng tới renderer của `FaceSurface`.
- Catalog ID: `1101`, tên hiển thị `Blank Face Dog`, `supportsCustomFace = true`. ID được giữ ổn định để tương thích dữ liệu và phòng đã có.

## Rig và animation

Model giữ 27 xương và các clip `Idle`, `Run`, `Jump`, `Bark`, `Poop`, `Pee`, `Bite`. Vùng mặt bám 100% vào xương `Head`. Ảnh người chơi đi theo khối đầu khi chạy, nhảy, sủa và cắn.

## Thống kê và kiểm tra

- 696 đỉnh hình học và 1.296 tam giác trong source. GLB có 2.448 đỉnh sau khi tách cạnh theo normal và UV.
- Các đoạn thân, chân và mặt giữ một influence mỗi đỉnh. Vùng chuyển của đuôi có tối đa ba influence. Validator pass cho BLEND, FBX và GLB.

Để dựng lại model và render, chạy `scripts/build_blank_face_dog.py` bằng Blender ở chế độ background. Bản hình khối giữ prefab, catalog và controller hiện có. Sau khi Unity import FBX, chạy `CustomDogFaceTests` để kiểm tra tham chiếu và texture mặt.

## Tài liệu tái sử dụng

- Chuyển model chó có sẵn: `../GUIDE_CONVERT_EXISTING_DOG_TO_CUSTOM_FACE_VI.md`.
- Dựng model cùng chuẩn từ ảnh mới: `../GUIDE_CREATE_CUSTOM_FACE_DOG_FROM_NEW_PHOTO_VI.md`.
