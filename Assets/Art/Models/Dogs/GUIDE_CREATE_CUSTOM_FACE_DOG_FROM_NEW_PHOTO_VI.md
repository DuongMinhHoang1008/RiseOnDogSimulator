# Tạo model chó Custom Face v1 từ ảnh mới

Tài liệu này dùng khi đầu vào là ảnh mới và cần dựng một model chó có animation, đồng thời có vùng mặt tích hợp để áp mặt người chơi ở runtime.

## Đầu vào nên có

Tối thiểu:

- Một ảnh toàn thân rõ silhouette.
- Một ảnh/crop chính diện vùng đầu nếu ảnh toàn thân quá nhỏ.

Tốt nhất:

- Chính diện, góc 3/4 và nhìn ngang.
- Ảnh đủ sáng, không motion blur, tai/đuôi/chân không bị che.
- Mô tả giống chó, chiều cao mong muốn, phong cách và ngân sách vertex.

Một ảnh không phải phép quét 3D. Các phần khuất phải được ước lượng thận trọng và ghi rõ trong README.

## Tham số project

```text
REFERENCE_IMAGES  = danh sách ảnh/crop nguồn
MODEL_NAME        = tên kỹ thuật không dấu
DISPLAY_NAME      = tên hiển thị
OUTPUT_ROOT       = Assets/Art/Models/Dogs/<MODEL_NAME>_<YYYYMMDD>
MODEL_ID          = ID catalog duy nhất
TARGET_VERTICES   = 5.000–10.000 để bắt đầu, điều chỉnh theo hình dáng
FORWARD_AXIS      = -Y trong Blender nếu không có yêu cầu khác
UP_AXIS           = +Z trong Blender
```

## Giai đoạn 1: dựng hình theo ảnh

### Silhouette trước, chi tiết sau

Khớp các đặc điểm sau trước khi rig:

- Tỷ lệ thân/cổ/đầu.
- Vai, hông, chiều dài chân và vị trí bàn chân.
- Dáng tai, điểm gắn và độ cong của đuôi.
- Độ sâu ngực, bụng và lưng.
- Dáng mõm nếu là chó bình thường.

Nếu ảnh là chó có mặt người hoặc người dùng yêu cầu mặt người, giữ chủ ý hybrid. Nếu ảnh là chó bình thường, không tự thêm mặt người.

Mesh phải là geometry 3D chỉnh sửa được. Có thể dùng primitives + voxel remesh để blockout, nhưng phải sửa các lỗi thường gặp như vai phồng, chân rời, hai khối má tách biệt hoặc đuôi xuyên thân.

### Thiết kế đầu cho Custom Face ngay từ đầu

Không dựng một đầu hoàn chỉnh rồi đặt plane lên trước. Thay vào đó:

1. Dựng đầu chó với một vùng facial patch có topology riêng nhưng liên tục với trán/má/cằm.
2. Vùng này có relief nông, đủ để mặt nhìn tự nhiên ở góc 3/4.
3. Biên vùng mặt là một loop hoặc dải polygon kín, không cắt vào chân tai/cổ.
4. Tách vùng đó thành object `FaceSurface` bằng đúng vertex positions của đầu.
5. Xóa polygon tương ứng khỏi mesh thân để hai mesh ghép thành một bề mặt duy nhất.
6. Không extrude, không offset, không tạo thickness hoặc side wall.

Mặt người chơi sẽ được chiếu lên vùng này. Vì vậy không nên có mõm chó dài nằm dưới ảnh. Nếu cần mũi/môi relief, giữ độ nhô nhỏ và kiểm tra với một texture mặt thử ở chính diện lẫn 3/4.

## Giai đoạn 2: texture

- Dùng base-color atlas và normal map nhẹ cho lông; tránh dựng từng sợi lông bằng geometry.
- Texture vùng khuất phải được suy luận hoặc tạo bổ sung; không tuyên bố giống ảnh ở những phần ảnh không cho thấy.
- Không lấy pixel nền đen/đỏ hoặc RGB trong vùng alpha trong suốt vào texture.
- Base color dùng sRGB, normal map dùng Non-Color.
- Giữ padding UV đủ cho mipmap.

`FaceSurface` có UV0 riêng phủ chính xác `0..1`:

```text
U: trái -> phải
V: dưới -> trên
Texture runtime: JPEG 512 × 512
```

Tạo `BlankFace_Default.png` 512 × 512, màu trung tính đồng nhất. Ảnh mặc định không chứa đặc điểm nhận dạng khuôn mặt.

## Giai đoạn 3: rig và skin

Rig bốn chân nên có khoảng 25–35 xương:

- Root, pelvis, spine, chest.
- Neck, Head, Jaw.
- Chuỗi riêng cho bốn chân với khớp hock đúng cho chân sau.
- Nhiều bone đuôi; ear bones là tùy chọn.

Yêu cầu skin:

- Khớp xương đặt theo mesh mới, không sao chép tọa độ từ con chó khác.
- Weight chuẩn hóa, tối đa 4 deform influence/vertex.
- Không có vertex mất weight.
- `FaceSurface` mặc định weight 1.0 vào `Head` để ảnh người chơi không bị méo.
- Nếu cho phép Jaw biến dạng vùng mặt, phải kiểm tra ảnh 2D không bị kéo giãn bất thường.

## Giai đoạn 4: animation

Nếu không có yêu cầu khác, tạo đủ:

| Clip | Yêu cầu |
| --- | --- |
| Idle | Loop, thở và đuôi nhẹ |
| Run | Loop hoàn chỉnh, phối hợp bốn chân |
| Jump | Lấy đà, bật, airborne, tiếp đất |
| Bark | Chuyển động đầu/cổ và hàm |
| Poop | Hạ thân bằng khớp chân sau |
| Pee | Chuyển trọng tâm, nhấc một chân sau |
| Bite | Chuẩn bị, há hàm, cắn chụp, phục hồi |

Mặc định 30 fps. Bake mọi transform cần thiết trước export. Run và Jump phải được review bằng nhiều frame hoặc video, không chỉ kiểm tra tên action.

## Giai đoạn 5: output và Unity

Dùng cấu trúc:

```text
OUTPUT_ROOT/
  source/
  model/
  previews/
  validation/
  scripts/
  Unity/
  asset_report.json
  README_VI.md
```

Xuất:

- `.blend` có texture đã pack.
- `.fbx` có skin và 7 clip cho Unity.
- `.glb` có skin, material và animation để round-trip check.
- PNG base color, normal và blank face riêng.

Unity:

- Import rig dạng Generic.
- Tạo prefab có component `DogFaceSurface` trên `FaceSurface`.
- Bind `targetRenderer`, đặt layout version 1.
- Tạo Animator Override Controller từ `DogGameplay.controller`.
- Đăng ký catalog bằng `MODEL_ID` duy nhất với `supportsCustomFace = true`.
- Không thay motor, input, network authority hoặc jump policy hiện có.

## Validation và review

Chạy `validate_dog_asset.py` trên BLEND, FBX và GLB với `--python-exit-code 1` và `--max-influences 4`.

Ngoài validator, bắt buộc xem:

- Front, side, hero, face close-up và face 3/4.
- Back/underside nếu texture được suy luận từ một ảnh.
- Toàn chu kỳ Run và Jump.
- Bark/Bite ở pose hàm đóng và mở.
- `FaceSurface` khi gắn một ảnh thử 512 × 512.

Validator không đánh giá độ giống ảnh, gait hợp lý, foot contact hoặc độ tự nhiên của mặt. Những mục này phải nghiệm thu bằng render.

## Tiêu chí bàn giao

- [ ] Silhouette giống ảnh tham chiếu trong giới hạn góc nhìn có sẵn.
- [ ] `FaceSurface` là một phần của đầu, không có plate/side wall/độ dày.
- [ ] Nhìn ngang không có vật thể nhô ra riêng khỏi đầu.
- [ ] UV0 đạt đúng 0–1 và runtime JPEG probe thành công.
- [ ] Vertex/triangle, bone count và clip ranges được ghi trong `asset_report.json`.
- [ ] BLEND, FBX, GLB đều PASS validator.
- [ ] Unity prefab, Animator Override và catalog entry hợp lệ.
- [ ] `CustomDogFaceTests` và `M2CatalogReservationTests` PASS.
- [ ] Unity Console sạch sau import/setup/test.
- [ ] README ghi rõ phần hình học/texture nào phải suy luận từ ảnh.

## Prompt tái sử dụng

```text
Từ các ảnh <REFERENCE_IMAGES>, hãy dựng một model chó mới tên <MODEL_NAME> trong <OUTPUT_ROOT>.
Giữ đúng giống chó, tỷ lệ, màu lông, tai và đuôi theo ảnh; ước lượng thận trọng phần bị khuất.
Thiết kế FaceSurface ngay trong topology đầu: tách đúng polygon cong của mặt, chung vị trí mép với
mesh thân, không plane/plate/offset/độ dày/side wall. UV0 phải phủ 0..1 cho JPEG 512x512.
Tạo rig bốn chân, tối đa 4 influence/vertex và đủ Idle, Run, Jump, Bark, Poop, Pee, Bite.
Xuất BLEND/FBX/GLB, texture PNG, preview front/side/face_angle và motion review. Tích hợp prefab
Unity với DogFaceSurface, Animator Override, catalog MODEL_ID duy nhất. Chạy validator trên cả ba
định dạng, EditMode tests và kiểm tra Unity Console trước khi bàn giao.
```

## Mẫu tham khảo trong project

- `Assets/Art/Models/Dogs/BlankFaceDog_20261107/scripts/build_blank_face_dog.py`
- `Assets/Art/Models/Dogs/BlankFaceDog_20261107/README_VI.md`
- `Assets/Game/Scripts/CustomDogFace/DogFaceSurface.cs`
- `Assets/Game/Editor/BlankFaceDogSetup.cs`
- `Assets/Game/Tests/EditMode/CustomDogFaceTests.cs`
