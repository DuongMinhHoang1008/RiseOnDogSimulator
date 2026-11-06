# M1 Fusion preview — 04/11/2026

**Cập nhật nghiệm thu 05/11/2026:** người dùng đã test Fusion và build Web, xác nhận phần chức năng hoạt động ổn, còn hơi giật. Xem [biên bản nghiệm thu](M1_ACCEPTANCE_2026-11-05.md) và [kế hoạch M1.1/M2](NEXT_STEPS_M1_1_M2.md). Các kết quả “App ID trống / chưa có WebGL module / NOT RUN” bên dưới là snapshot lúc bàn giao SDK ngày 04/11, không phải trạng thái setup hiện tại. Model gameplay nay đã rig và nối animation; xem [tích hợp model hiện tại](RIGGED_DOG_GAMEPLAY.md).

Tài liệu gốc ghi phần triển khai đầu tiên của M1 mạng và kết quả trước nghiệm thu. SDK: **Fusion 2.1.4 Stable Build 2500**, Unity **6000.3.19f1**, URP và Input System hiện có được giữ lại.

## SDK và cấu hình

- Đã import file do người dùng cung cấp: `C:\Users\admin\Downloads\Photon-Fusion-2.1.4-Stable-2500.unitypackage` bằng Editor đang mở.
- SHA-256 của gói: `FA390A5B1E976D926906BFF4C03077DD1FDBB9BA69D6FE9D8333A2B2211174EB`.
- `Assets/Photon/Fusion/build_info.txt`: `2.1.4 Stable 2500`, build date `2026-10-26`. SDK nằm trong `Assets/Photon/`; không thay phiên bản Unity/URP/Input System hay thêm transport khác.
- SDK tạo `PhotonAppSettings.asset` và `NetworkProjectConfig.fusion` trong `Assets/Photon/Fusion/Resources/`. App ID còn trống khi kiểm tra. Asset serialization đã là Force Text.
- Installer SDK tự thêm scripting symbols Fusion và bật `runInBackground`. Nó cũng yêu cầu Mono.Cecil 1.10.2; đã dùng Unity Package Manager API để giữ **1.11.6 đang được resolve sẵn** và đồng bộ manifest/lock ở phiên bản này. Đây là dependency trực tiếp bổ sung, không nâng/hạ bản Mono.Cecil thực tế; script cài tạm đã được gỡ. Bằng chứng: `Logs/Fusion-UPM-setup.log`.
- `DogSimulator.Fusion` được đăng ký vào `AssembliesToWeave` bằng API Editor của SDK. Prefab table cũng được SDK rebuild; không viết scene/prefab YAML bằng tay.
- `FusionNetworkConfig.asset`: region `asia`, version `office-m1-fusion214-v1`, dev capacity 8 **chưa đo tải**, 3 create attempts và timeout 20 giây mỗi lần thử.
- Dùng **Single Peer mode**, một client mỗi process. Scene giữ arena tĩnh đã mở trên từng client; chưa có network scene objects hay tải scene qua mạng. Build thử phải chứa/mở đúng `M1_FusionPreview`; công cụ không đổi danh sách release build scenes.

## Mở và chạy

1. Trong [Photon Dashboard](https://dashboard.photonengine.com/), tạo ứng dụng dùng SDK **Fusion**, phiên bản **Fusion 2**. Điền App ID vào **Tools → Fusion → Fusion Hub**, hoặc chọn `PhotonAppSettings` và điền `AppSettings → App Id Fusion`. Xem [hướng dẫn chính thức](https://doc.photonengine.com/fusion/v2/getting-started/appid-instructions).
2. Mở **Tools → Dog Simulator → Create or Open M1 Fusion Preview**. Lần đầu công cụ sao chép arena/UI từ local preview, thay driver trong bản sao, tạo một prefab `Assets/Game/Prefabs/DogPlayer.prefab`, rồi lưu scene `Assets/Game/Scenes/M1_FusionPreview.unity`. Lần sau mở scene đã có; không ghi đè chỉnh sửa. Local preview gốc được giữ lại.
3. Play → nhập nickname → **Create private room**. Chỉ hiển thị mã thành công sau khi xác minh phòng, phiên bản và spawn local avatar.
4. Client khác chạy cùng scene/build, App ID, region và version; nhập mã rồi **Join room by code**.
5. WASD, Shift, Space, chuột như local preview. Esc/mất focus/mất pointer lock tạm ngừng input và mở menu, vẫn giữ kết nối. **Resume game** tiếp tục; **Copy room code** sao chép mã; **Leave room / Cancel connection** thoát hoặc hủy kết nối đang chờ.
6. Thiếu App ID, nickname hoặc mã sai được báo trước khi tạo runner. Cài SDK/App ID không tự bật nút mạng trong scene local preview; hãy dùng scene Fusion riêng.

## Trách nhiệm các file

| File | Hành vi |
| --- | --- |
| `Scripts/Fusion/FusionSessionOptions.cs` | Shared-only; private session; clone SDK app settings để cố định region/version; strict join; metadata đánh dấu lần tạo; thông báo lỗi |
| `Scripts/Fusion/FusionSessionService.cs` | Một thao tác kết nối tại một thời điểm; runner mới mỗi attempt; cancellation/timeout trong Unity Update; kiểm tra kết quả trước spawn; mapping player/avatar; leave/shutdown cleanup |
| `Scripts/Fusion/NetworkDogPlayer.cs` | Chỉ State Authority đọc input và gọi motor trong `FixedUpdateNetwork`; remote controller/input bị tắt; `NetworkTransform`; nickname và model ID được ghi sau Spawned; label plain text |
| `Scripts/Fusion/FusionPreviewUI.cs` | Nickname, Create/Join, trạng thái và số người/mã phòng; pause/resume/copy/leave; không tự ghi network state |
| `Editor/FusionPreviewSetup.cs` | Tạo/cấu hình bằng Unity và Fusion API; weave assembly; tạo prefab/scene; reuse content đã có |
| `Scripts/Player/DogCamera.cs` | Thêm Bind/Unbind để camera scene bám avatar local mới; driver offline giữ nguyên |
| `Tests/*/Fusion*Tests.cs` | Kiểm tra policy bằng kiểu SDK thật, scene/prefab references và chặn request lỗi trước tạo runner; không giả lập kết nối Photon |

Shared `StartGame` có thể join một room trùng tên. Mỗi create attempt gửi nonce mới trong initial session properties; nếu kết quả mang nonce khác thì leave, hủy runner và thử mã khác. Không spawn avatar hoặc báo create thành công trong phòng trùng mã. Một kết nối ngắn vào phòng trùng mã có thể xuất hiện trước bước kiểm tra này; cần kiểm thử contention thực tế. Join đặt `EnableClientSessionCreation = false` và không ghi nonce.

Nickname/model ID là replicated state; các peer có thể dựng label sau late join. Avatar đặt `DestroyWhenStateAuthorityLeaves`, tắt authority override và không chạy offline driver. Hiệu lực cleanup khi refresh/disconnect và chuyển Master vẫn cần được kiểm chứng trên Photon thật.

## Kiểm thử tự động

Với Editor đã đóng, chạy tuần tự từ root project:

```powershell
unity test . --editor-path 'D:\Unity Hub\Editors\6000.3.19f1\Editor\Unity.exe' --mode EditMode --filter DogSimulator.Tests --output TestResults\Fusion-EditMode.xml --timeout 240 -- -nographics -logFile 'D:\IT\Game\RiseOnDogSimulator\Logs\Fusion-EditMode.log'
unity test . --editor-path 'D:\Unity Hub\Editors\6000.3.19f1\Editor\Unity.exe' --mode PlayMode --filter DogSimulator.Tests --output TestResults\Fusion-PlayMode.xml --timeout 240 -- -nographics -logFile 'D:\IT\Game\RiseOnDogSimulator\Logs\Fusion-PlayMode.log'
```

Đã chạy ngày **04/11/2026** bằng Unity 6000.3.19f1 thực tế:

| Kiểm tra | Kết quả | Bằng chứng |
| --- | --- | --- |
| Import SDK, biên dịch, sinh scene/prefab bằng Unity | PASS | `Assets/Photon/Fusion/build_info.txt`, assets đã sinh, `Logs/Fusion-SDK-setup.log` |
| EditMode, toàn bộ suite hiện tại | **30/30 PASS**, 0 fail/skip | `TestResults/Fusion-EditMode.xml`, `Logs/Fusion-EditMode.log` |
| PlayMode, toàn bộ suite hiện tại | **9/9 PASS**, 0 fail/skip | `TestResults/Fusion-PlayMode.xml`, `Logs/Fusion-PlayMode.log` |
| Menu Full HD trong Editor; Create khi App ID trống | **PASS thủ công** | Menu hiển thị đủ trường/nút; bấm Create trả thông báo cấu hình App ID ngay tại menu; đã thoát Play mode sau kiểm tra; log phiên Editor: `Logs/Fusion-Manual-menu.log` |
| Photon Cloud / hai client / browser / load test | **NOT RUN** | App ID còn trống, WebGL module thiếu |

`Logs/Fusion-SDK-setup.log` lưu cả lỗi compile trung gian (test assembly cần tham chiếu SDK DLL tường minh; NUnit Assert cần alias). Các lỗi này đã được sửa; hai test process cuối exit 0. Log setup cũng có cảnh báo khởi tạo của Editor/SDK; kết quả test cuối là bằng chứng cho code hiện tại. Đã kiểm tra menu của scene Fusion trong Play mode ở Full HD và thấy phản hồi thiếu App ID sau khi bấm Create; chưa xác minh movement/label qua hai client thực.

Lần test sandbox đầu tiên kẹt ở licensing trước khi chạy test. Chạy bằng local Windows user để dùng license đã có; không đổi cấu hình license.

Phiên kiểm tra menu có một thông báo Curl certificate từ Editor trong log. Vì App ID trống và request bị chặn tại menu, đây chưa phải kết quả kiểm thử kết nối Photon; kiểm tra lại kết nối Cloud khi đã cấu hình App ID.

## Checklist chưa nghiệm thu

- Hai browser trên hai mạng nhìn thấy nickname/movement của nhau; mỗi client chỉ điều khiển avatar của mình.
- Mã không tồn tại không tạo room; room đầy/đóng, App ID sai, region/version khác trả lỗi phù hợp. Room ở namespace version khác có thể chỉ trả “not found”, không thể phân biệt chắc chắn với mã sai.
- Create trùng mã đồng thời: mã đánh dấu ngăn báo nhầm thành công; mọi runner thất bại phải được dọn trước retry.
- Cancel khi đang kết nối, timeout, leave/retry nhiều lần, mất mạng, refresh tab, đóng tab và Master rời không sinh avatar thừa.
- Mất focus/pointer lock, camera collision và UI ở nhiều aspect ratio.
- Model catalog/selection, replicated reservations và handoff ở M2: **chưa làm**. Mọi người tạm dùng model ID 1 placeholder, chưa đáp ứng yêu cầu model duy nhất.
- Interaction/quest/world-state thuộc M3–M4: chưa làm. WebGL module/build và browser/load tests thuộc các gate tiếp theo: chưa chạy. Không có cam kết capacity/FPS.
