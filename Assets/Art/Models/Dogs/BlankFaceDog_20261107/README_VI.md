# Blank Face Dog

Model chó dành cho hệ thống mặt người chơi. `FaceSurface` được tách trực tiếp từ các polygon cong của đầu chó gốc và lấp đúng lỗ trên mesh thân. Bề mặt không có độ dày, không offset ra phía trước và có UV riêng để nhận JPEG 512 × 512 ở runtime.

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
- Catalog ID: `1101`, tên hiển thị `Blank Face Dog`, `supportsCustomFace = true`. Đây là model custom-face duy nhất; ID được giữ ổn định để tương thích dữ liệu và phòng đã có.

## Rig và animation

Model có 27 xương và các clip `Idle`, `Run`, `Jump`, `Bark`, `Poop`, `Pee`, `Bite`. Vùng mặt bám 100% vào xương `Head`, nên ổn định khi chạy và nhảy. Ảnh 2D vẫn cứng theo đầu: Bark/Bite chuyển động đầu và xương hàm nhưng không làm biến dạng miệng trong ảnh người chơi.

## Thống kê và kiểm tra

- 6.359 vertex hình học, 12.526 triangle trong source trước khi exporter tách UV/normal.
- Tối đa 4 influence/vertex; không có vertex mất weight.
- Validator PASS cho `.blend`, `.fbx`, `.glb`, không warning.
- EditMode: `CustomDogFaceTests` 6/6 PASS; `M2CatalogReservationTests` 3/3 PASS.
- Unity Console sạch sau khi tạo prefab và chạy runtime texture probe.

Để dựng lại toàn bộ tích hợp sau khi thay FBX, chạy menu `Tools > Dog Simulator > Custom Face > Apply Custom Face with BlankFaceDog`. Chỉ đăng ký lại riêng model bằng menu `Register Blank Face Art Model`.

## Tài liệu tái sử dụng

- Chuyển model chó có sẵn: `../GUIDE_CONVERT_EXISTING_DOG_TO_CUSTOM_FACE_VI.md`.
- Dựng model cùng chuẩn từ ảnh mới: `../GUIDE_CREATE_CUSTOM_FACE_DOG_FROM_NEW_PHOTO_VI.md`.
