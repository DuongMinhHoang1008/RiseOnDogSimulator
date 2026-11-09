# Dog Simulator — Office Chaos

Unity **6000.3.19f1**, URP **17.3.0**, Input System **1.19.0**. Đích multiplayer: **Photon Fusion 2 Shared Mode**, WebGL, phòng riêng qua mã.

Hiện tại (06/11/2026): **M1 đã được nghiệm thu chức năng theo xác nhận người dùng**; FPS HUD, catalog 3 model, reservation và preview/menu hai cột đã có implementation. Bản vá animation hai model mới còn chờ xác minh runtime; chưa kết luận hết giật hoặc đạt capacity. Xem [rà soát nghiệm thu M1.1–M2](Docs/M1_1_M2_ACCEPTANCE_2026-11-06.md). Hướng tiếp theo ưu tiên **ngoạm đồ, bóng/xương và sân thử**, rồi chồng chó, NPC phản công, ragdoll và prototype xe theo [thiết kế gameplay mới](Docs/NEXT_STEPS_GAMEPLAY_M3.md).

## Chạy phần mạng đang phát triển

1. Mở bằng Unity 6000.3.19f1; điền Fusion 2 App ID vào **Tools → Fusion → Fusion Hub** hoặc asset `PhotonAppSettings`.
2. Chọn **Tools → Dog Simulator → Create or Open M1 Fusion Preview** để mở `Assets/Game/Scenes/M1_FusionPreview.unity`.
3. Play → nhập nickname → **Create private room** hoặc nhập mã và **Join room by code**. Dùng một client mỗi process; các client phải cùng App ID, region `asia` và game build.
4. Esc mở menu; **Resume game**, **Copy room code**, **Leave room / Cancel connection**. Chưa cấu hình App ID thì thao tác sẽ báo lỗi tại chỗ, không tạo runner.

Chi tiết setup, giới hạn và kiểm thử: [M1 Fusion setup](Docs/M1_FUSION_SETUP_AND_TESTS.md). Catalog hiện gồm Reference Dog, Photo Dog và Shiba Face, có picker và reservation model độc quyền. Hai model mới có bản vá loop/rebind còn chờ xác nhận chạy lại. Xem [nhật ký M2](Docs/M1_1_M2_IMPLEMENTATION_2026-11-05.md) và [hướng dẫn tích hợp animation](Docs/RIGGED_DOG_GAMEPLAY.md).

## Chạy thử phần hiện có

1. Mở bằng Unity 6000.3.19f1.
2. Mở scene `Assets/Game/Scenes/M1_LocalPreview.unity` hoặc dùng menu **Tools → Dog Simulator → Create or Open M1 Local Preview**.
3. Play → nhập nickname → **Play local preview**.
4. WASD di chuyển, Shift chạy, Space nhảy, chuột xoay camera, Esc về menu. **F sủa, chuột phải cắn, G ỉa, H đái**. **Reset local position** đưa chó về điểm xuất phát.

Model đã rig được gắn vào player local và prefab mạng; arena vẫn là **placeholder để test**. Các hành động mới là animation, chưa có âm thanh, hitbox/sát thương hay hiệu ứng nước/phân. **Check code format** chỉ kiểm tra cú pháp mã, không kết nối mạng.

Kiểm thử sau tích hợp SDK ngày 04/11/2026: **30/30 EditMode + 9/9 PlayMode pass** trên Unity thực, chạy headless. Đây là báo cáo baseline trước lượt nghiệm thu Fusion/Web của người dùng.

Tích hợp model/animation ngày 05/11/2026: **32/32 EditMode + 16/16 PlayMode pass** trong Unity 6000.3.19f1. Đã kiểm tra input, hành động, nhảy và state owner Fusion Single. Kết quả tự động này tách biệt với xác nhận thử Shared/Web thực tế; các ca mạng chi tiết và tải nhiều người cần nhật ký riêng.

- [Audit, phạm vi và kế hoạch milestone](Docs/IMPLEMENTATION_STATUS.md)
- [Nghiệm thu M1 ngày 05/11/2026](Docs/M1_ACCEPTANCE_2026-11-05.md)
- [Rà soát nghiệm thu M1.1–M2 ngày 06/11/2026](Docs/M1_1_M2_ACCEPTANCE_2026-11-06.md)
- [Thiết kế gameplay tiếp theo và đánh giá đề xuất](Docs/NEXT_STEPS_GAMEPLAY_M3.md)
- [Đề xuất kiến trúc Windows PC và lộ trình chuyển](Docs/Specifications/PC_ARCHITECTURE.md)
- [Chuẩn bị M1.1 và M2](Docs/NEXT_STEPS_M1_1_M2.md)
- [Setup và kiểm thử local preview](Docs/M1_SETUP_AND_TESTS.md)
- [Setup và kiểm thử Fusion preview](Docs/M1_FUSION_SETUP_AND_TESTS.md)
- [Thiết kế gameplay được cung cấp](Docs/Specifications/DOG_SIMULATOR_DESIGN_AND_CODEX_RULES.md)
- [Baseline mạng được cung cấp](Docs/Specifications/NETWORK_ARCHITECTURE.md)

**Bước tiếp theo đề xuất:** xác minh animation và regression nền còn mở, rồi làm M3-A: ngoạm/thả, xương/bóng/thùng và một sân blockout nhỏ. Tài liệu mới chưa triển khai gameplay, mua asset hoặc thay đổi runtime; ragdoll và xe có đợt thử riêng.
