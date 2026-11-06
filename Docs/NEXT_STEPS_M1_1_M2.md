# Kế hoạch tiếp theo — M1.1 và M2

> **Lịch sử kế hoạch 05/11/2026:** nhiều phần dưới đây đã được triển khai. Trạng thái mới xem [rà soát nghiệm thu 06/11](M1_1_M2_ACCEPTANCE_2026-11-06.md); hướng gameplay tiếp theo xem [thiết kế M3 và roadmap mới](NEXT_STEPS_GAMEPLAY_M3.md). Các câu “chưa triển khai” và giới hạn milestone bên dưới mô tả thời điểm lập kế hoạch gốc.

**Cập nhật:** 05/11/2026.  
**Đầu vào:** [nghiệm thu M1](M1_ACCEPTANCE_2026-11-05.md): Fusion và bản Web đã được người dùng thử, chức năng hoạt động ổn, còn hơi giật. Tài liệu này chuẩn bị công việc; chưa triển khai các hạng mục bên dưới.

## 1. M1.1 — Hiển thị FPS và đo hiện tượng giật

Đây là thay đổi nhỏ thực hiện trước phần catalog/reservation, để có số đo cho bản hiện tại trước khi tăng lượng visual và logic mạng.

### Phạm vi triển khai

- Thêm nhóm **Cài đặt** trong menu uGUI/TMP hiện có, với toggle **Hiển thị FPS**, mặc định tắt. Cho phép chỉnh cả trước và sau khi vào phòng.
- Lưu lựa chọn qua PlayerPrefs như các preference cục bộ hiện có; không đồng bộ toggle hoặc FPS qua Fusion.
- HUD FPS là local-only, đặt ở góc dễ đọc, không chặn raycast/click. Khi tắt thì ẩn hoàn toàn, không tạo runner/object mạng hoặc thay đổi nickname/model/session.
- Dùng số khung hình và thời gian unscaled trong cửa sổ lấy mẫu, cập nhật text khoảng 0,5 giây/lần. Có thể kèm frame time trung bình để hỗ trợ ghi số đo. Không ghi log mỗi frame hay tạo chuỗi mới mỗi frame cho HUD.
- Giữ pause/resume, pointer lock, input buffer và leave/cancel hiện có. Không tự đổi render pipeline, tick rate, chất lượng model, VSync hoặc target frame rate để che hiện tượng giật.

### Điểm sửa dự kiến

| Vị trí | Công việc |
| --- | --- |
| `Assets/Game/Scripts/UI/` | Component HUD FPS và preference cục bộ, trách nhiệm tách khỏi session/motor |
| `Assets/Game/Scripts/Fusion/FusionPreviewUI.cs` | Gắn UI cài đặt vào menu hiện có, giữ nguyên luồng phòng |
| `Assets/Game/Editor/FusionPreviewSetup.cs` | Bổ sung tham chiếu/toggle bằng Unity API; cập nhật scene hiện có theo cách idempotent, không ghi đè chỉnh sửa của người dùng |
| `Assets/Game/Scenes/M1_FusionPreview.unity` | Cài đặt và HUD được serialize trong Editor |
| `Docs/` | Ghi nhật ký trước/sau và môi trường thử bản Web |

Local preview có thể dùng cùng component HUD nếu cần so sánh; không tạo một bộ đếm FPS thứ hai.

### Đo baseline và tìm nguyên nhân

1. Ghi build/version, URL hoặc cách host, máy/GPU, browser/version, độ phân giải, số client, trạng thái focus và thời lượng thử.
2. So sánh local preview và Shared với cùng visual/độ phân giải: đứng yên, đi/chạy, xoay camera, nhảy và action. Ghi riêng cảm giác giật của avatar local/camera và avatar remote.
3. Đo FPS/frame time trước khi sửa; nếu có profiler hoặc thống kê Fusion phù hợp SDK thì bổ sung CPU/GPU, GC và RTT. Không suy ra nguyên nhân chỉ từ cảm giác hoặc chỉ từ FPS trung bình.
4. Kiểm tra chi phí rendering/skin/animation, UI/GC, camera/motor hoặc nhịp render network dựa trên dữ liệu. Chưa mặc định model mượt hay Photon là nguyên nhân.
5. Chọn thay đổi nhỏ có bằng chứng, đo lại cùng kịch bản và ghi cả kết quả chưa cải thiện. Tải 2/4/8/12/20 client là các lượt đo riêng; chỉ công bố mức đã thử.

### Tiêu chí nghiệm thu M1.1

- Toggle bật/tắt và preference hoạt động trong browser; HUD đọc được ở các độ phân giải desktop đã thử, không che nút/hint và không chặn input.
- Thử bật/tắt trước join, trong phòng, sau leave và sau reload; session/authority/camera vẫn hoạt động như baseline.
- FPS phản hồi thay đổi tải trong phiên đang chạy, không chỉ là giá trị cố định trên ảnh chụp. Không cam kết xử lý giật chỉ vì HUD đã hiện.
- Có nhật ký đo đủ môi trường và kịch bản; phần giật được ghi là đã sửa, cải thiện một phần hoặc còn tồn tại theo kết quả thực.
- Chạy các test phù hợp nếu sửa luồng UI/input/session; kiểm tra build Web thực. Không cần viết test chỉ phản chiếu dòng gán text hoặc màu của toggle.

## 2. M2 — Catalog visual và giữ chỗ model

### Những gì đã có / còn thiếu

| Đã có trong repo | Còn thiếu cho M2 |
| --- | --- |
| Một prefab mạng, motor/collider chuẩn hóa, `DogModelId` được replicate | Catalog ID ổn định và lựa chọn model |
| Model ReferenceDog có hai biến thể low-poly/mượt và cùng rig; gameplay đang dùng bản mượt | Danh sách các mẫu chó thực cần đại diện cho người chơi |
| `DogVisualPresenter`, `DogAnimationDriver`, controller và metadata animation | Mapping visual/animation/offset theo model, fallback khi thiếu asset |
| Shared session, owner state, nickname và cleanup runner | Master-owned replicated reservation, tranh chấp chọn model, late join và handoff |

Hai biến thể low-poly/mượt là hai mức thể hiện của cùng ReferenceDog, không mặc định là hai danh tính chó khác nhau. Tách lựa chọn chất lượng hình ảnh khỏi `DogModelId`; không coi hai mesh đó là đã có đủ danh sách 10–20 chó. Khi chưa đủ asset, dùng fixture/placeholder có nhãn cho kiểm thử và ghi rõ giới hạn roster có thể chọn.

### Các phần triển khai nhỏ

| Phần | Đầu ra | Kiểm tra chính |
| --- | --- | --- |
| M2-A: catalog và visual | `DogModelCatalog` với ModelId ổn định, visual prefab, display name, metadata scale/ground/mouth và mapping animation; adapter tái sử dụng presenter hiện có | ID trùng bị báo; thiếu prefab/ID dùng fallback; đổi visual không thay motor/collider hay thêm NetworkObject vào visual |
| M2-B: chọn model và giữ chỗ | Picker trước join lưu ý định; sau join request Master; chưa spawn avatar chính thức khi chưa được chấp thuận; registry replicated nhỏ theo PlayerRef/ModelId | Hai người chọn cùng ID chỉ một người được duyệt; người còn lại nhận lựa chọn khác; hết model có thông báo, không tự nhân đôi ID |
| M2-C: vòng đời và regression | Release sau Photon xác nhận player rời; idempotent request/retry; Master mới tiếp tục từ replicated registry; late join đọc current state | Leave/refresh/disconnect giải phóng đúng; Master rời không mất/nhân đôi reservation; không spawn avatar thừa |

Các file mới dự kiến là `DogModelCatalog`, phần tạo visual từ catalog và `DogModelSelectionRegistry`; chỉ tạo khi triển khai từng phần. Điểm tích hợp chính là `FusionSessionService` (hiện spawn ngay sau join), `NetworkDogPlayer` (hiện gán ID 1), `FusionPreviewUI` và `FusionPreviewSetup`. Không thêm service locator/event bus/framework tổng quát.

### Quy tắc dữ liệu và authority

- Chỉ một prefab `DogPlayer`; gameplay collider/motor không phụ thuộc kích thước visual. Visual và hiệu ứng cục bộ không có NetworkObject riêng.
- ModelId không dùng index Inspector; mọi build tương thích dùng cùng catalog. Bump GameVersion khi đổi layout network hoặc bảng ID không tương thích.
- Master là nguồn chấp thuận reservation. Request kiểm tra nguồn gửi/PlayerRef còn trong session, ID tồn tại, chưa có người giữ, request trùng và trạng thái selection hiện tại.
- Avatar owner chỉ công bố ModelId sau chấp thuận; UI không tự ghi network state. Registry replicated là nguồn giữ chỗ qua late join/handoff, không dựa vào dictionary/coroutine cục bộ chưa replicate.
- Giữ quy tắc runner mới khi retry và một avatar/player. Nếu join thành công nhưng chọn model thất bại, ở màn chờ/chọn lại; không giữ avatar với ID trùng.
- Tái sử dụng animation/snapshot đang có. Các action animation hiện tại không được tự tính là đã xong hit/bark interaction của M3.

### Gate hoàn thành M2

1. Hai client thấy cùng nickname và ModelId/visual; đồng thời chọn cùng ID không tạo duplicate reservation.
2. Late join thấy trạng thái hiện tại; missing asset fallback không crash; collider và mouth anchor được chuẩn hóa.
3. Leave, refresh, mất kết nối và Master handoff không làm kẹt model hoặc sinh avatar thừa.
4. Ghi rõ số model thực có thể chọn và các placeholder dùng cho test; không tuyên bố có 20 model khi asset chưa đủ.
5. Đo lại kịch bản M1.1 với visual thực đang dùng; ghi tác động hiệu năng trước khi chuyển M3.

## 3. Giữ phạm vi milestone

M3: interaction hit/bark, knockable/toggle/carry và validation. M4: map/ba quest P0. M5: phát hành Web và load test có số đo. Việc người dùng đã build/test Web ở M1 là bằng chứng cho bản thử hiện tại, không thay thế gate tải/capacity của M5. Không đổi topology, tạo thêm map hoặc tự đưa nội dung P1/P2 vào bước kế tiếp.
