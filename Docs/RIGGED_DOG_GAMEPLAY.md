# Rigged dog — tích hợp vào game

Player local và prefab mạng `Assets/Game/Prefabs/DogPlayer.prefab` dùng bản mượt từ `Assets/Art/ReferenceDog/realistic_low_vertex/Dog_Rigged.fbx`.
Bản low-poly và scene trình diễn animation vẫn được giữ trong thư mục art. Chưa thêm menu chọn model/giữ chỗ model.

## Chơi

- Local: mở `Assets/Game/Scenes/M1_LocalPreview.unity`, Play, nhập tên và chọn **Play local preview**.
- Multiplayer: mở `Assets/Game/Scenes/M1_FusionPreview.unity`, Play và tạo/tham gia phòng như trước.
- WASD di chuyển, Shift chạy, Space nhảy, chuột xoay camera, Esc menu.
- **F sủa, chuột phải cắn, G ỉa, H đái**.
- Sủa/cắn dừng di chuyển trong thời gian clip. Di chuyển hoặc nhảy sẽ hủy tư thế ỉa/đái; mở menu xóa input/hành động đang chờ.

Các hành động ở bước này là animation. Chưa thêm âm thanh, nước/phân, hitbox hoặc sát thương.

## Cách nối

- `DogMotor`: vẫn là nơi duy nhất điều khiển CharacterController, xuất tốc độ thực và báo nhảy đã được chấp nhận.
- `DogInputReader`: tái sử dụng Input System hiện có, thêm bốn action vào bản clone runtime của map Player và buffer nút trong Update.
- `DogAnimationDriver`: chỉ chạy một lần từ LocalMovementPreview hoặc FixedUpdateNetwork của State Authority; xử lý thời gian action và khóa/hủy di chuyển.
- `DogVisualPresenter`: chỉ render Animator, không đọc input, không chạy physics hoặc ghi network state.
- `DogGameplay.controller`: Idle/Run theo tốc độ, Jump theo nhảy thật, cùng Bark/Poop/Pee/Bite.
- `Jump_Gameplay.anim`: bản sao clip nhảy, giữ chuyển động chân/thân nhưng cố định vị trí Root. Độ cao nhảy do DogMotor quản lý, tránh cộng thêm độ cao từ clip demo.
- Config thời lượng/rate nằm trong `Assets/Game/Config/DogAnimationConfig.asset`.

NetworkDogPlayer giữ một prefab mạng, vẫn dùng Fusion Shared/State Authority. Owner ghi tốc độ, grounded, mã action, sequence và TickTimer.
Proxy chỉ đọc state rồi render; snapshot của một action đang diễn ra bắt đầu ở thời điểm tương ứng thay vì phát lại từ đầu.
GameVersion được thêm hậu tố `-riganim-v1` vì layout network state thay đổi; các client phải dùng cùng bản game.

Menu cập nhật asset: **Tools > Dog Simulator > Assign Rigged Dog to Gameplay**.
Thao tác này không tự lưu scene Fusion đang có thay đổi chưa lưu.

## Kiểm tra

Các test mới kiểm tra asset đã gắn, clip nhảy không nhân đôi độ cao, input/hành động, hủy ỉa/đái, air-jump, snapshot giữa clip và state Fusion ở chế độ Single không kết nối Cloud.
Kết quả cuối được ghi trong `TestResults/rig_gameplay_editmode.json` và `TestResults/rig_gameplay_playmode.json`.
Ngày 05/11/2026: **32/32 EditMode và 16/16 PlayMode pass** trong Unity 6000.3.19f1, gồm smoke test Fusion Single ghi state owner thật và kiểm tra snapshot giữa action.
Single-mode smoke test không thay thế nghiệm thu hai client Shared qua Photon Cloud/WebGL.

**Cập nhật 05/11/2026:** người dùng sau đó xác nhận đã test Fusion và bản Web, chức năng hoạt động ổn, còn hơi giật. Kết luận và giới hạn ghi trong [biên bản nghiệm thu M1](M1_ACCEPTANCE_2026-11-05.md); kế tiếp là [M1.1 FPS/đo hiệu năng và M2 catalog/reservation](NEXT_STEPS_M1_1_M2.md). Đây là xác nhận runtime của người dùng, không thay đổi ý nghĩa của các test Single-mode ở trên.
