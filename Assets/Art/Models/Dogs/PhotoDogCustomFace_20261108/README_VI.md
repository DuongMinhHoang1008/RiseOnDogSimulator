# Photo Dog Custom Face v1

Biến thể từ [FBX Photo Dog gốc](../PhotoDog_20261105/model/Dog_Rigged.fbx). `FaceSurface` gồm 802 tam giác trên bề mặt cong của đầu. Các tam giác đi qua viền được cắt theo ellipse ngay trên bề mặt gốc. Mesh thân và mesh mặt dùng chung vị trí, pháp tuyến và trọng số tại 118 đỉnh viền. Mặt mặc định dùng PNG 512 × 512 một màu.

Thân, tai, chân và đuôi đã được dựng lại theo phong cách Soft Low-Poly. Các dải vát nhỏ dùng normal có trọng số. Vùng mặt và khoang miệng giữ hình học gốc để bảo toàn ảnh đã lưu và chuyển động hàm.

UV0 chiếu theo X/Z trong Blender, với U tăng từ trái sang phải khi nhìn chính diện và V tăng từ dưới lên. Ảnh đi theo chuyển động của hàm trong Bark và Bite. Độ nổi của mũi và môi vẫn đến từ hình học gốc. Mình lùi riêng phần dưới của khoang miệng tối đa 2,6 cm để xử lý vệt xuyên qua cằm trong FBX nguồn.

## Tệp bàn giao

- [BLEND chỉnh sửa được, có texture đóng gói](model/PhotoDogCustomFace_Rigged.blend)
- [FBX skinned](model/PhotoDogCustomFace_Rigged.fbx) và [GLB skinned](model/PhotoDogCustomFace_Rigged.glb)
- [Màu mặt mặc định](model/BlankFace_Default.png), [base color](model/Dog_BaseColor.png) và [normal map](model/Dog_Normal.png)
- [Prefab Unity](Unity/PhotoDogCustomFace.prefab) và [Animator Override Controller](Unity/PhotoDogCustomFace.overrideController)

Prefab dùng rig Generic và gắn `DogFaceSurface` layout 1 vào skinned renderer của mặt. `DogFaceSurface.TryApplyJpeg` nhận JPEG 512 × 512 tối đa 128 KiB. Mục **Photo Dog** trong [catalog](../../../../Game/Config/DogModelCatalog.asset) dùng prefab mới và giữ ID `1001`. Chọn **Photo Dog** rồi mở **Customize Face** để áp ảnh.

## Kiểm định

| Định dạng | Đỉnh | Tam giác | Kết quả |
| --- | ---: | ---: | --- |
| FBX gốc | 9.100 | 18.048 | Đối chiếu |
| BLEND | 2.775 | 5.029 | [PASS](validation/blend.json) |
| FBX nhập lại bằng Blender | 2.775 | 5.029 | [PASS](validation/fbx.json) |
| GLB nhập lại bằng Blender | 4.768 | 5.029 | [PASS](validation/glb.json) |

GLB tách thêm đỉnh ở đường nối UV, pháp tuyến hoặc vật liệu. Blender 5.2.3 xác nhận UV0 phủ đủ 0–1, tối đa 4 ảnh hưởng xương mỗi đỉnh và độ lệch tại đường nối dưới 0,000001 đơn vị scene ở các frame đã lấy mẫu trong cả ba định dạng.

Rig có 27 xương. Bản BLEND giữ 7 clip gốc ở 30 fps gồm Idle (1–61), Run (1–25), Jump (1–43), Bark (1–37), Poop (1–109), Pee (1–97) và Bite (1–28).

[Báo cáo Unity gốc](validation/unity.json) ghi lại lần thử JPEG và reset mặt trong Singleplayer trước đợt đổi phong cách.

- [Chính diện](previews/front.png), [nhìn ngang](previews/side.png), [góc 3/4](previews/hero.png) và [cận cảnh](previews/face_angle.png)
- [Ảnh nhìn ngang FBX gốc](previews/source_side.png), [JPEG thử chiều UV](previews/face_uv_probe.png) và [ảnh thử trong Customize Face](previews/unity_face_probe.png)
- Các khung hình mẫu của [Run](previews/Run_motion_review.png), [Jump](previews/Jump_motion_review.png), [Bark](previews/Bark_motion_review.png) và [Bite](previews/Bite_motion_review.png)
- [Thông số model](asset_report.json)
