# Giao diện Dog Simulator và map dùng chung

Yêu cầu bổ sung từ ảnh mẫu ngày 07/11/2026.

| Màn hình | Yêu cầu |
| --- | --- |
| HomeScene | Logo và nút Singleplayer, Multiplayer, Options, Exit bên trái. Chó 3D lơ lửng bên phải. Mỗi lần vào Home đổi sang một mẫu chó khác nếu catalog có nhiều mẫu. |
| PrepareScene | Tab Body và Face, chó xem trước ở giữa, mũi tên và dải chọn model. Chó để gắn mặt cần có vùng mặt trống. Dùng các model hiện có trong giai đoạn tạm. Chọn một trong ba world save khi chơi Singleplayer hoặc tạo phòng. |
| FaceCustomize | Khung xem trước, tải ảnh PNG/JPEG, chỉnh kích thước, vị trí X/Y và góc xoay. Có vẽ tự do, đổi màu/kích thước cọ, tẩy và hoàn tác. Bỏ stickers. |
| MultiplayerHub | Thẻ kem, nút tạo phòng, nhập mã và bảng phòng có phân trang. Bảng hiện các phòng đang mở và số người hiện tại trên sức chứa, theo region và phiên bản game. Chủ rời thì phòng đóng và người chơi còn lại trở về Hub. |
| Settings | Popup phủ trên màn hình hiện tại, có General, Graphics, Audio, Controls. Copy Room Code xuất hiện trong phòng Multiplayer. |
| HUD trong game | Esc mở Settings ở góc trên trái. Góc dưới phải hiện F Bark, RMB Bite, G Poop và H Pee bằng nhãn phím. Khi tới gần đồ có thể nhặt, hiện LMB Pick Up cạnh đồ. Khi đang cầm đồ, chuyển sang E Drop và LMB Throw ở dưới giữa màn hình. |

## Phong cách

- Màu kem `#FFF0D4`, vàng mật ong `#FFBA38`, nâu cacao `#452617`, xanh lá và xanh trời từ phông sân vườn.
- Nút bo tròn, viền nâu, điểm sáng và bóng đổ nhẹ. Nút co nhẹ lúc nhấn và trở về kích thước thường khi thả.
- Font tròn Sniglet, icon dấu chân và xương, chó lơ lửng và chuyển màn hình fade ngắn.
- Phông sân vườn tạm nằm ở `Assets/Game/Resources/DogMenu/Yard.png`. Font và giấy phép OFL nằm cùng thư mục. Ảnh chân dung lấy từ các bản xem trước của model có sẵn.

## Luồng chạy

- Mở `Assets/Game/Scenes/M1_FusionPreview.unity` và nhấn Play. Home, Prepare, Face và Hub là các màn hình uGUI trong scene này.
- Singleplayer khởi tạo Fusion `GameMode.Single` offline. Multiplayer giữ `GameMode.Shared` và cấu hình Photon của dự án. Hai chế độ dùng chung map sandbox, DogPlayer và đồ vật tương tác.
- Catalog tạm có Reference Dog, Photo Dog, Shiba Face và Blank Face Dog. Blank Face Dog có bề mặt mặt riêng và được chọn khi vào chỉnh mặt từ model chưa hỗ trợ. Các model tạm khác dùng texture có sẵn.
- FaceCustomize lưu mặt đã áp dụng trên thiết bị. Trong WebGL có thể kéo thả ảnh vào game khi màn hình chỉnh mặt mở. Trong Editor dùng Choose Image.
- Hub đọc danh sách và số người trực tiếp từ Photon. Người chơi có thể vào từ danh sách hoặc bằng mã phòng. Thông tin ping hiển thị trong phiên Multiplayer.
- Ba ô World 1–3 lưu vị trí và góc xoay của các đồ vật sandbox trên thiết bị. Cùng một ô mở được ở Singleplayer và khi tạo phòng Multiplayer. Phiên Multiplayer đọc và ghi bản lưu của chủ phòng. Đồ đang cầm được lưu theo vị trí trước lúc nhặt. Game lưu định kỳ và khi rời phiên.
- G bắt đầu Poop với hồi chiêu 20 giây. Cuối động tác tạo `DogPoop.obj` dưới dạng đồ vật có thể nhặt, thả và ném. Poop tự biến mất sau 45 giây nằm tự do. Thời gian còn lại được giữ khi có người cầm.
- H bắt đầu Pee với hồi chiêu 10 giây. Tia hạt theo động tác Pee và xuất hiện cho người chơi trong phòng. HUD hiển thị số giây hồi chiêu còn lại cho cả hai hành động.
- Esc mở Settings khi chơi. Apply lưu thiết lập trên thiết bị. Trình duyệt quyết định nhịp làm mới thực tế của WebGL. English là ngôn ngữ UI hiện tại.

## Kiểm tra

1. Vào Home, mở Multiplayer rồi quay lại. Kiểm tra chó đổi mẫu và tiếp tục lơ lửng.
2. Vào Singleplayer, đổi model, nhập nickname và Play. Kiểm tra nhân vật, map và đồ vật sandbox. Mở Settings bằng Esc và kiểm tra nút copy mã phòng ẩn.
3. Trong Prepare, mở Customize Face. Thử chọn ảnh, kéo hoặc chỉnh thanh trượt, vẽ, tẩy, hoàn tác, bật 3D Preview và Apply. Vào game với Blank Face Dog để xem kết quả.
4. Với Photon App ID hợp lệ, mở hai client. Tạo phòng ở client thứ nhất rồi xem số người trong bảng Open Rooms của client thứ hai. Vào bằng mã hoặc dòng phòng. Cho chủ rời để kiểm tra client còn lại về Hub và dòng phòng biến mất.
5. Tới gần đồ trong sandbox và kiểm tra LMB Pick Up xuất hiện cạnh đồ. Nhặt đồ để thấy E Drop và LMB Throw. Thử thả rồi nhặt lại và ném, sau đó đi ra xa để kiểm tra các gợi ý tự ẩn.
6. Dời một đồ vật trong World 1, rời game, rồi tạo phòng từ World 1. Kiểm tra vật ở vị trí đã lưu và World 2 có trạng thái riêng.
7. Thử G và H khi đứng yên. Kiểm tra tia Pee, thời gian hồi chiêu và poop chỉ hết hạn khi nằm tự do.

Kiểm tra ngày 08/11/2026 trên Unity 6000.3.19f1 đạt 44 bài EditMode và 19 bài PlayMode. Hai tiến trình Unity qua Photon xác nhận số người cập nhật từ 1/8 thành 2/8, khách rời phiên khi chủ đóng phòng và dòng phòng biến mất khỏi Hub. Vị trí đồ vật từ Singleplayer được khôi phục trong phòng Multiplayer. Chạy thử offline xác nhận Poop, Pee, hồi chiêu và việc dừng đếm hạn của poop khi đang cầm. Các phép thử này chạy trong Unity Editor.

## Phông tạm

Tạo bằng công cụ ImageGen tích hợp với prompt dưới đây.

> Use case illustration-story. Generate one 16:9 landscape full-bleed background plate for a cozy cartoon Dog Simulator game menu. Use the user's dog menu reference as art direction only: bright blue sky and fluffy clouds, sunlit green backyard lawn, low wooden fence in midground, branches with rounded green leaves framing the top left and top right, a cozy cream house just on the far right edge, small red-roof doghouse at the lower right, small red ball and cream dog bone in the lower grass near the corners. Keep the central and left areas open behind UI, with calm sky/grass values. Painterly polished casual game environmental illustration, soft warm afternoon, clean broad shapes, saturated but soft greens and blues. Environment only. No dogs, no humans, no letters, no logos, no UI, no borders, no collage.
