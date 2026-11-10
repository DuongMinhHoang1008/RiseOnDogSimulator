# Shiba mặt trống — model rigged

Model dùng trục `-Y` forward và `+Z` up trong Blender. Bản Soft Low-Poly giữ thân và chân dạng khối, tai vát cạnh và đuôi cuộn. Đầu được dựng lại theo kiểu Blank Face Dog. Vùng `FaceSurface` trống gồm mặt trước và các mặt vát của đầu, ghép với phần đầu còn lại tại tám đỉnh viền.

## File chính

- `model/Dog_Rigged.blend`: file Blender có texture đã pack, rig và mọi Action.
- `model/Dog_Rigged.fbx`: mesh, skin, texture nhúng và 7 clip đã bake.
- `model/Dog_Rigged.glb`: mesh, skin, texture nhúng và 7 clip đã bake.
- `model/BlankFace_Default.png`: texture 512 × 512 một màu cho `FaceSurface`. UV0 phủ 0–1, U tăng từ trái sang phải và V tăng từ dưới lên khi nhìn chính diện.
- `asset_report.json`: thống kê hình học, rig và clip.
- `validation/*.json`: kết quả kiểm tra native và round-trip export.
- `previews/Run_contact_sheet.png`, `previews/Jump_contact_sheet.png`: khung hình kiểm tra bản hiện tại.

## Thông số

- 696 đỉnh hình học, 1.296 tam giác trong BLEND và FBX nhập lại bằng Blender.
- GLB nhập lại có 2.432 đỉnh do tách ở UV và normal. Số tam giác giữ nguyên.
- 27 xương, tối đa 4 influence/vertex; không có vertex mất weight.
- Kích thước mesh ở tư thế nghỉ xấp xỉ 0,459 × 1,287 × 1,173 m.
- 7 clip ở 30 fps: `Idle`, `Run`, `Jump`, `Bark`, `Poop`, `Pee`, `Bite`.
- `Idle` và `Run` loop kín; mọi chuyển động đã bake, không phụ thuộc constraint lúc runtime.

## Nghiệm thu

Validator pass cho BLEND, FBX và GLB, bao gồm kiểm tra UV, ảnh trống và đường ghép trong cả bảy clip. Mặt bám xương `Head` và di chuyển cùng khối đầu khi chạy, nhảy, sủa và cắn.

## Giới hạn

Model đã có hình học và texture mặt theo Custom Face v1. Để thay ảnh trong game, Shiba cần prefab có component `DogFaceSurface` và một mục trong catalog. `SurfaceReview.blend`, atlas cũ và các GIF cũ là tư liệu của bản chân dung trước đây.
