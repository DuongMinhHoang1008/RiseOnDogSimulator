# Biên bản rà soát nghiệm thu M1.1–M2 và chuyển hướng gameplay

**Ngày:** 06/11/2026, Asia/Saigon.  
**Kết luận:** ghi nhận phần triển khai đã có, cho phép chuẩn bị thiết kế gameplay tiếp theo; **chưa nghiệm thu hoàn tất M1.1–M2**. Người dùng yêu cầu tập trung gameplay, chưa xác nhận lại hai model sau bản vá animation. Biên bản này không thay lời xác nhận nghiệm thu của người dùng.

## 1. Phạm vi và căn cứ

- [M1 đã được nghiệm thu chức năng](M1_ACCEPTANCE_2026-11-05.md) theo phản hồi người dùng ngày 05/11; hiện tượng giật được chuyển tiếp.
- [Nhật ký triển khai M1.1–M2](M1_1_M2_IMPLEMENTATION_2026-11-05.md) và các thay đổi trong repo: FPS HUD, catalog ba model, preview, menu hai cột, reservation và sửa lỗi tạo phòng.
- Phản hồi tiếp theo của người dùng: hai model mới chưa có animation trong game. Sau đó đã sửa import loop và khởi tạo Animator, nhưng chưa có xác nhận runtime mới.
- Lượt lập biên bản này đọc lại source/config và tài liệu, **không chạy Unity, không build Web, không chạy test mới**. Các XML `Temp/M2EditModeResults.xml` và `Temp/M2PlayModeResults.xml` không còn hiện diện khi rà soát; số test bên dưới là lịch sử từ nhật ký/chat, chưa có bản sao bền vững cho lượt đó.

## 2. Trạng thái từng phần

| Hạng mục | Bằng chứng / trạng thái | Quyết định |
| --- | --- | --- |
| FPS HUD, toggle và lưu preference | Có implementation local-only; chưa có nhật ký đo browser trước/sau | Đã triển khai, chờ nghiệm thu Web và hiệu năng |
| Catalog | Có ID `1` Reference Dog, `1001` Photo Dog, `1002` Shiba Face | Ghi nhận roster thực là **3 model**, không phải 10–20 |
| Menu trước phòng | Đã đổi sang controls/settings bên trái, preview bên phải; lượt trước quan sát Full HD trong Editor | Đã kiểm tra Editor; chờ người dùng xác nhận ở bản hiện tại |
| Menu pause | Dùng chung layout; source bật preview khi pause | Đã triển khai; chưa ghi PASS thao tác Escape/runtime sau bản vá cuối |
| Tạo phòng | Lượt trước xác định `MethodAccessException` ở RPC generated; thay request bằng replicated state; tạo phòng Photon thật và nút Create vào gameplay thành công | Ghi nhận smoke test Editor một client; chưa suy rộng thành regression nhiều client |
| Reservation model | Registry Master-owned và mỗi peer có request object | Có implementation; tranh chấp, late join, disconnect và Master handoff còn chờ thử thực |
| Animation Photo Dog/Shiba Face | Có 7 clip/controller mỗi model. Bản vá sửa tên `Dog_Rig|Idle/Run`, bật loop, rebind Animator và culling | **Chưa xác nhận hết lỗi**; loop sai giải thích đứng ở pose cuối, chưa chứng minh là nguyên nhân duy nhất của mọi action không chạy |
| Hiệu năng / độ mượt | Có công cụ FPS; chưa có baseline máy/browser/build/client rõ ràng | Chưa đạt gate đo; chưa tuyên bố sửa giật |

Baseline config hiện tại: Unity **6000.3.19f1**, Fusion **2.1.4**, Shared Mode, region `asia`, version `office-m2-catalog-reservations-v2`, capacity cấu hình **8**. Ba model độc quyền đồng nghĩa tối đa **3 avatar có model khác nhau**; không được diễn giải capacity 8 là 8 người đều có thể spawn. Chưa thay quy tắc độc quyền model trong biên bản này.

## 3. Test lịch sử và giới hạn

| Lượt được ghi trong nhật ký | Kết quả | Diễn giải |
| --- | --- | --- |
| EditMode M2 trước bản vá animation cuối | 35/35 pass | Không bao phủ mọi thay đổi sau lượt chạy |
| PlayMode baseline M2 | 17/17 pass | Mốc lịch sử, không phải trạng thái hiện tại |
| PlayMode sau sửa UI/network | 16/17 pass | `FusionAuthorityWritesMovementAndActionStateWithoutCloud` thất bại assertion animation timing: 0,42 thay vì >0,8 |
| Test preview trong lượt trên | Pass | Chỉ xác nhận RenderTexture và đổi instance model; **không đo chuyển động xương** |
| Sau bản vá loop/rebind cuối | Chưa có kết quả mới | Không đánh dấu xanh dựa trên test cũ |

Không cộng các suite thành tổng test độc lập. Lỗi timing cần được tái hiện và phân loại; chưa có căn cứ kết luận chỉ do môi trường hoặc có thể bỏ qua.

## 4. Checklist đóng phần nền trước khi tích hợp gameplay mới

- [ ] Với cả 3 model: đứng yên ít nhất 10 giây, đi/chạy ít nhất 10 giây, nhảy và thử F/chuột phải/G/H; thấy xương chuyển động, animation loop đúng và quay về locomotion.
- [ ] Hai client thấy đúng model/action của nhau; đổi model khi pause không kẹt pose. Preview không chiếm input và không che picker ở 1280×720, 1920×1080.
- [ ] Create/join/leave/retry chạy trên cùng build Web; menu pause/resume và pointer lock ổn.
- [ ] Hai client tranh cùng model: đúng một reservation; bên thua chọn lại. Client thứ ba join muộn; Master rời; model được giải phóng khi Photon xác nhận departure.
- [ ] Rerun test liên quan sau bản vá cuối, lưu XML/log vào `TestResults/` cùng build hoặc commit, thời gian và môi trường. Xử lý test timing thất bại thay vì chỉ chạy đến khi xanh.
- [ ] Ghi FPS/frame time và cảm giác local/remote với 1 và 2 client. Chưa yêu cầu tải 20 người để thử prototype gameplay.

Ưu tiên đóng lỗi animation và room/input trước khi tích hợp cơ chế mới. Có thể làm blockout map và prototype tương tác song song về mặt tiến độ; chưa coi đây là release đạt mọi gate M2/M5.

## 5. Hướng tiếp theo

Người dùng đề xuất chuyển trọng tâm sang đứng trên nhau, mở map, bóng/xương, NPC phản công, hết HP thành ragdoll, ngoạm đồ không giới hạn kích thước và xe lái. Đánh giá, thiết kế và thứ tự triển khai ở [NEXT_STEPS_GAMEPLAY_M3.md](NEXT_STEPS_GAMEPLAY_M3.md).

Tài liệu thiết kế mới là đề xuất cho đợt tiếp theo, chưa phải bằng chứng tính năng đã được làm hoặc yêu cầu triển khai tất cả trong một lần.
