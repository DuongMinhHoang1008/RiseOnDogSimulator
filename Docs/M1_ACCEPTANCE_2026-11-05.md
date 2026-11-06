# Biên bản nghiệm thu M1 — Fusion và bản Web

**Ngày ghi nhận:** 05/11/2026 (Asia/Saigon).  
**Kết luận:** nghiệm thu chức năng M1 theo xác nhận của người dùng, chấp nhận tồn đọng hiện tượng hơi giật để xử lý ở bước kế tiếp. Được chuyển sang chuẩn bị M2; chưa kết luận đạt mục tiêu hiệu năng hoặc tải 20 người.

## 1. Xác nhận của người dùng

> Đã test Fusion và build Web, đang hơi giật, đề xuất toggle FPS trong cài đặt, còn lại đã hoạt động ổn, viết ra md nghiệm thu và chuẩn bị bước tiếp theo

Đây là kết quả thử thực tế do người dùng báo trong chat ngày 05/11/2026. Lượt cập nhật tài liệu này không chạy lại Unity, build Web hoặc thử kết nối Cloud.

| Hạng mục | Kết quả ghi nhận | Nguồn |
| --- | --- | --- |
| Luồng Fusion và phần chức năng đã thử | Hoạt động ổn theo xác nhận người dùng | Phản hồi trên |
| Build Web | Đã build theo xác nhận người dùng | Phản hồi trên; chưa lưu URL hoặc hash bản build trong biên bản |
| Độ mượt | Còn hơi giật | Người dùng quan sát; chưa có số đo FPS/frame time/RTT hoặc bước tái hiện riêng |
| Hiển thị FPS trong cài đặt | Đề xuất đã ghi vào backlog ưu tiên kế tiếp | Chưa triển khai toggle hoặc HUD FPS |

Số client, việc dùng hai mạng khác nhau, cấu hình máy, trình duyệt, môi trường xuất hiện giật và thời lượng phiên chưa được nêu riêng. Các ca mã sai/đầy phòng, create collision, refresh/retry, late join và đổi Master chưa có kết quả chi tiết trong phản hồi này; giữ chúng trong checklist regression. Không tự gán PASS cho từng ca, browser compatibility hoặc coi đây là nghiệm thu M5.

## 2. Baseline được giữ lại

- Unity **6000.3.19f1**, URP **17.3.0**, Input System **1.19.0**, Fusion **2.1.4 Stable Build 2500**, topology **Shared Mode**.
- Scene thử mạng: `Assets/Game/Scenes/M1_FusionPreview.unity`; một prefab mạng `Assets/Game/Prefabs/DogPlayer.prefab`; motor CharacterController và input hiện có được tái sử dụng.
- Config trong repo lúc ghi biên bản: region `asia`, version `office-m1-fusion214-v1-riganim-v1`, capacity cấu hình 8. **Capacity cấu hình không phải capacity đã benchmark.** Không thay config trong lượt tài liệu này.
- Đã có model rigged, presenter/controller và animation đi/chạy/nhảy/sủa/cắn/ỉa/đái. Các action hiện là animation, chưa tương đương hitbox/sát thương/âm thanh hay tương tác của M3.
- WebGL Build Support đã hiện diện trong Editor khớp phiên bản khi kiểm tra thư mục trên máy. Thông tin “thiếu module/chưa test Web” ở audit 04/11 là lịch sử.

## 3. Bằng chứng tự động sẵn có

Các báo cáo dưới đây đã tồn tại và được đọc lại, không phải lượt test mới của biên bản:

| Báo cáo | Kết quả | Giới hạn |
| --- | --- | --- |
| `TestResults/Fusion-EditMode.xml` | 30/30 pass | Baseline tích hợp SDK ngày 04/11 |
| `TestResults/Fusion-PlayMode.xml` | 9/9 pass | Baseline motor/input/UI/preflight ngày 04/11 |
| `TestResults/PrivateRoom-PlayMode.xml` | 10/10 pass | Có kiểm tra tạo runner runtime, callback shutdown, leave/retry; không thay thế Cloud nhiều client |
| `TestResults/rig_gameplay_editmode.json` | 32/32 pass | Asset/animation và các kiểm tra EditMode trong báo cáo tích hợp rig |
| `TestResults/rig_gameplay_playmode.json` | 16/16 pass | Input/action/snapshot, có smoke test Fusion Single; không phải kết quả Shared Cloud |

Các suite có test trùng nhau, **không cộng các con số thành tổng test độc lập**. Kết quả Shared/Web thực tế được ghi riêng ở mục 1 theo xác nhận người dùng.

## 4. Tồn đọng và thứ tự xử lý

| ID | Việc tiếp theo | Tiêu chí đóng |
| --- | --- | --- |
| M1-FPS-01 | Thêm toggle **Hiển thị FPS** vào cài đặt, lưu lựa chọn cục bộ | Bật/tắt đúng trong bản Web; nhớ lựa chọn sau reload trên cùng browser; không ảnh hưởng input hoặc session |
| M1-PERF-01 | Đo và tái hiện hiện tượng giật | Có baseline máy/browser/build/số client, FPS và frame time; phân biệt giật khung hình với chuyển động remote; sửa theo nguyên nhân có bằng chứng rồi đo lại |
| M1-REG-01 | Ghi kết quả regression lifecycle mạng khi thử bản kế tiếp | Nhật ký riêng cho strict join, full room, collision, leave/refresh/retry, late join và Master rời |

Toggle được hiểu là **bật/tắt bộ đếm FPS**, chưa phải tùy chọn giới hạn FPS. Bộ đếm là công cụ quan sát, không được ghi là đã sửa giật khi chưa đo và kiểm chứng lại. Mục tiêu >=30 FPS và tải 10–20 người vẫn cần benchmark trên môi trường được ghi rõ.

## 5. Bước tiếp theo

Thực hiện **M1.1 — Hiển thị FPS và đo hiệu năng**, sau đó triển khai **M2 — catalog model và reservation** theo [kế hoạch tiếp theo](NEXT_STEPS_M1_1_M2.md). Việc nghiệm thu này không tự mở rộng phạm vi sang M3–M5 hoặc nội dung P1/P2.
