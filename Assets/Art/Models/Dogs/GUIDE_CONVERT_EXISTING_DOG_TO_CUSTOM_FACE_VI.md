# Chuyển model chó có sẵn sang Custom Face v1

Tài liệu này dùng khi đã có một model chó trong project và cần tạo biến thể nhận mặt người chơi giống `BlankFaceDog_20261107`.

## Mục tiêu bắt buộc

- Thân, đầu, tai và silhouette của model gốc được giữ nguyên tối đa.
- `FaceSurface` là một phần của bề mặt đầu, không phải tấm bảng hoặc khối đặt trước mặt.
- `FaceSurface` không có độ dày và không có mặt bên.
- Mép vùng mặt dùng đúng vị trí vertex của lỗ đã tách khỏi đầu. Nhìn ngang không xuất hiện silhouette mới.
- UV0 của `FaceSurface` phủ chính xác `0..1` để nhận JPEG 512 × 512.
- Model dùng rig Generic và tương thích hệ thống `DogFaceSurface` hiện có.

## Tham số cần chốt trước khi sửa

Điền các giá trị này cho từng model:

```text
SOURCE_MODEL      = đường dẫn .blend/.fbx/.glb gốc
OUTPUT_ROOT       = Assets/Art/Models/Dogs/<TênModel>_<YYYYMMDD>
DISPLAY_NAME      = tên hiển thị trong catalog
MODEL_ID          = ID duy nhất, không trùng catalog hiện tại
ARMATURE_NAME     = tên armature của model
FACE_CENTER       = tâm vùng mặt trong local space
FACE_HALF_WIDTH   = nửa chiều rộng vùng ảnh
FACE_HALF_HEIGHT  = nửa chiều cao vùng ảnh
FORWARD_AXIS      = hướng nhìn trước của model
```

Không tái sử dụng `1101` nếu vẫn giữ `Blank Face Dog`. `CustomDogFaceConstants.CustomFaceModelId` hiện là ID của model custom-face mặc định, không phải dải ID dành cho mọi biến thể.

## Quy trình Blender

### 1. Kiểm tra model nguồn

- Mở model và xác nhận đơn vị, origin, trục forward/up, armature, material và texture.
- Render chính diện, góc 3/4 và nhìn ngang trước khi sửa để có ảnh đối chiếu.
- Ghi lại vertex/triangle, số xương, tên action và frame range.
- Nếu chỉ có FBX/GLB, import vào file tạm; không lưu đè model nguồn.

### 2. Chọn vùng mặt

Chọn các polygon nằm trong một ellipse ở mặt trước đầu. Điều kiện chọn phải dựa trên local coordinate và hướng forward của model, không sao chép tọa độ từ model khác.

Vùng chọn nên:

- Bao phủ trán dưới, mắt, mũi, miệng và cằm cần nhận ảnh.
- Không cắt chân tai, cổ hoặc má ngoài.
- Có biên đủ dày polygon để tạo đường viền tương đối mượt.
- Giữ relief nông của mặt nếu model đã có hình dáng phù hợp.

### 3. Tách polygon, không tạo plate

Phương pháp chuẩn:

1. Sao chép chính các vertex và polygon đã chọn sang mesh mới tên `FaceSurface`.
2. Xóa đúng các polygon đó khỏi mesh thân.
3. Giữ nguyên tọa độ vertex ở mép trên cả hai mesh.
4. Không extrude, không Solidify, không tạo side wall và không offset ra trước.
5. Bật smooth shading phù hợp với đầu gốc.

Kết quả phải là một bề mặt một phía lấp đúng lỗ trên đầu. Nếu nhìn ngang thấy một đường hoặc khối nhô ra ngoài silhouette đầu, geometry chưa đạt.

Không dùng các cách sau:

- Rounded rectangle đặt trước mặt.
- Plane phẳng phủ lên mõm.
- Duplicate overlay nằm trên mesh đầu gây z-fighting.
- Một khối có bevel/độ dày để chứa texture.

### 4. UV và material

- Tạo UV layer tên `FaceUV` hoặc UV0 tương đương.
- Chuẩn hóa UV theo bounds thật của các vertex mặt:

```text
U = (x - minX) / (maxX - minX)
V = (z - minZ) / (maxZ - minZ)
```

Nếu model dùng trục khác, đổi cặp trục chiếu cho đúng mặt trước.

- Kiểm tra min UV gần `(0,0)` và max UV gần `(1,1)` với sai số tối đa `0.001`.
- Material đầu tiên của renderer mặt phải có `mainTexture`.
- Texture mặc định là PNG 512 × 512, màu trung tính đồng nhất, không có mắt/mũi/miệng.
- Base color dùng sRGB; normal map dùng Non-Color.

### 5. Skin và animation

- Nếu mặt chỉ cần cứng theo đầu, gán toàn bộ `FaceSurface` vào bone `Head` với weight 1.
- Nếu cần biến dạng hàm, bảo toàn weight gốc nhưng giới hạn tối đa 4 influence/vertex và kiểm tra khe hở khi Bark/Bite.
- Không để vertex mất weight.
- Giữ các action yêu cầu: `Idle`, `Run`, `Jump`, `Bark`, `Poop`, `Pee`, `Bite`.
- Review cả chu kỳ Run/Jump, không chỉ kiểm tra action name.

## Cấu trúc đầu ra

```text
OUTPUT_ROOT/
  model/
    <TênModel>_Rigged.blend
    <TênModel>_Rigged.fbx
    <TênModel>_Rigged.glb
    BlankFace_Default.png
    Dog_BaseColor.png
    Dog_Normal.png
  previews/
    front.png
    side.png
    hero.png
    face.png
    face_angle.png
    Run_motion_review.png
    Jump_motion_review.png
  validation/
    blend.json
    fbx.json
    glb.json
  scripts/
  Unity/
    <TênModel>.prefab
  asset_report.json
  README_VI.md
```

File `.blend` phải pack texture. Trước khi export, relink mọi image path sang file đang tồn tại trong `OUTPUT_ROOT/model` rồi pack lại.

## Tích hợp Unity

Lấy `Assets/Game/Editor/BlankFaceDogSetup.cs` làm mẫu nhưng tạo setup riêng cho model mới.

Yêu cầu:

- Importer dùng `ModelImporterAnimationType.Generic`.
- Object trong FBX phải tên chính xác `FaceSurface`.
- Prefab gắn `DogFaceSurface` lên object mặt.
- `targetRenderer` trỏ tới renderer của `FaceSurface`.
- `layoutVersion = CustomDogFaceConstants.LayoutVersion`.
- Catalog entry có `supportsCustomFace = true` và `faceLayoutVersion = 1`.
- Dùng `MODEL_ID` mới, không xóa entry custom-face đang tồn tại.
- Tạo Animator Override Controller dựa trên `DogGameplay.controller` và map đủ 7 clip.

Runtime contract hiện tại:

```text
TextureSize       = 512
MaximumJpegBytes  = 128 KiB
LayoutVersion     = 1
```

## Validation bắt buộc

Chạy validator trên cả ba định dạng:

```powershell
& 'D:\Blender 5.2\blender.exe' --background --factory-startup --python-exit-code 1 `
  --python 'C:\Users\admin\.codex\skills\blender-dog-from-photo\scripts\validate_dog_asset.py' -- `
  --input '<MODEL.blend>' --report '<validation/blend.json>' --max-influences 4
```

Lặp lại cho FBX và GLB. Sau khi import Unity, chạy:

```powershell
unity command test run --caller plugin --skill unity-cli --mode editor --filter CustomDogFaceTests --timeout 120 --project-path '<PROJECT>' --format json
unity command test run --caller plugin --skill unity-cli --mode editor --filter M2CatalogReservationTests --timeout 120 --project-path '<PROJECT>' --format json
```

## Checklist nghiệm thu

- [ ] Front render: vùng mặt nằm giữa đầu, không giống sticker hoặc bảng.
- [ ] Side render: không có silhouette mới, side wall hoặc khoảng hở.
- [ ] Face angle: mép mặt nối liên tục với trán, má và cằm.
- [ ] UV0 đạt đủ 0–1.
- [ ] JPEG probe 512 × 512 được áp đúng renderer.
- [ ] BLEND, FBX, GLB đều PASS validator, không warning chưa giải thích.
- [ ] 7 action tồn tại và chuyển động thực sự làm biến dạng model.
- [ ] Run/Jump contact sheet không có mặt tách khỏi đầu.
- [ ] Unity Console có 0 error và 0 warning sau setup/test.
- [ ] Catalog ID duy nhất và entry cũ vẫn còn nguyên.

## Prompt tái sử dụng

```text
Hãy chuyển model <SOURCE_MODEL> thành một biến thể Custom Face v1 trong <OUTPUT_ROOT>.
Tách chính polygon mặt của đầu thành FaceSurface, giữ nguyên tọa độ và độ cong, không tạo
plane/plate/độ dày/side wall. UV0 phải phủ đúng 0..1 cho JPEG 512x512. Giữ rig và các clip
Idle, Run, Jump, Bark, Poop, Pee, Bite; xuất BLEND/FBX/GLB, render front/side/face_angle và
Run/Jump review. Tạo prefab Unity, DogFaceSurface binding và catalog entry với MODEL_ID duy nhất.
Chạy validator trên cả ba định dạng và hai bộ EditMode test trước khi bàn giao.
```
