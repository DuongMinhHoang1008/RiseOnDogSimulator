# Kế hoạch triển khai mặt chó tùy chỉnh trong multiplayer

**Ngày:** 07/11/2026, Asia/Saigon  
**Trạng thái:** Đã triển khai lát cắt trong project; chờ nghiệm thu Web/multiplayer thật  
**Nền:** Unity `6000.3.19f1`, URP, uGUI, Photon Fusion `2.1.4 Stable 2500` Shared Mode, WebGL trên itch.io

## 1. Phạm vi bàn giao

Người chơi chọn ảnh PNG/JPEG từ thiết bị, kéo, thu phóng và xoay ảnh trong khung mặt. Người chơi có thể vẽ trên nền trống hoặc vẽ đè lên ảnh, xem trước trên chó 3D rồi xác nhận. **Ảnh và nét vẽ đã xác nhận phải hiện trên đúng avatar ở tất cả client trong phòng, kể cả người vào muộn.** Người chơi có thể khôi phục mặt mặc định và thay đổi đó cũng phải đến mọi client.

Tài liệu ảnh đính kèm cung cấp thiết kế chỉnh ảnh thủ công, preview, texture `512 × 512` và lưu cục bộ. Phạm vi dự án bổ sung vẽ cơ bản và nâng đồng bộ cả phòng thành điều kiện bàn giao. Bản đầu tập trung vào ảnh tĩnh, một mặt đã lưu trên mỗi trình duyệt và chọn ảnh trên WebGL desktop. Nhận diện mặt, tách nền, bộ chọn thư viện native và lưu bản gốc để chỉnh sửa lại nằm ngoài lát cắt này.

Đây là hạng mục riêng của roadmap. [M3-C NPC](M3_AB_ACCEPTANCE_2026-11-07.md#5-bước-tiếp-theo--m3-c-npc-phản-công) tiếp tục theo gate gameplay đã ghi. Dùng [biên bản 07/11](M3_AB_ACCEPTANCE_2026-11-07.md) làm chuẩn regression cho ngoạm, thả, ném, chồng chó và knockdown.

## 2. Model mặt phẳng và tích hợp menu

- Tạo một biến thể có ID riêng trong `DogModelCatalog` từ [Photo Dog](../Assets/Art/Models/Dogs/PhotoDog_20261105/README_VI.md). Giữ thân, tai và vỏ đầu phía sau để còn dáng chó khi nhìn nghiêng. Cắt vùng mắt, mũi và miệng nổi, dọn lưỡi/răng hoặc phần hàm lộ ra trước mặt. Gắn `FaceSurface` vào xương `Head` và giữ tên xương cùng các clip đã bake. Giữ các lựa chọn chó hiện tại trong catalog. Mở trình chỉnh mặt trên biến thể hỗ trợ tính năng và cho người chơi chọn biến thể đó ngay từ menu.
- `FaceSurface` là mesh phẳng có viền bo, một renderer và UV `0–1` lấy mẫu từ khung vuông. Chốt một `FaceLayoutVersion` cùng viền và hướng UV chuẩn cho editor, preview và avatar. Đặt mặt sát vỏ đầu và kiểm tra hở viền, xuyên mesh, mặt bị lật và z-fighting ở góc nghiêng. Bắt đầu với material URP Unlit và một mặt mặc định dễ nhận ra. Mỗi renderer nhận texture riêng của nó. Material asset dùng chung giữ vai trò cấu hình gốc.
- Bản sửa art giữ `MouthAnchor`, `BackSupport` và `CharacterController` trên prefab gameplay. `FaceSurface` đi theo xương đầu và `DogVisual` khi ngã. Thử riêng `Bark` và `Bite` vì tấm mặt không còn biểu cảm mở hàm. Nếu cú chồm và âm thanh hiện có chưa đủ rõ, chỉnh tín hiệu visual/âm thanh trong clip và giữ timing gameplay.
- Thêm **Chỉnh mặt** cạnh bộ chọn chó trong [FusionPreviewUI](../Assets/Game/Scripts/Fusion/FusionPreviewUI.cs), trước khi vào phòng và trong pause. Tái dùng camera preview của [DogSelectionPreview](../Assets/Game/Scripts/UI/DogSelectionPreview.cs). Khi căn ảnh, mặc định nhìn thẳng và cho xoay để kiểm tra viền mặt. Modal phải giữ controls gameplay tắt cả khi picker lấy focus hoặc `OnSessionChanged()` chạy. Chỉ bật lại khi người chơi tiếp tục chơi và còn avatar. Preview vẫn là một visual cục bộ.
- Dùng chung component render mặt giữa [DogVisualPresenter](../Assets/Game/Scripts/Player/DogVisualPresenter.cs) và preview. `ApplyModel()` có thể hủy và tạo lại visual. Sau khi visual mới sẵn sàng, bind lại texture đã xác nhận nếu model và layout phù hợp. Hai avatar dùng cùng model vẫn có texture riêng theo từng avatar.

## 3. Chỉnh ảnh, vẽ và lưu

| Phần | Hành vi triển khai |
| --- | --- |
| Chọn ảnh | Adapter WebGL dùng `<input type="file">` qua `.jslib`, mở từ thao tác trực tiếp của người dùng. Nếu đường gọi từ uGUI không giữ được user activation trong iframe, dùng nút HTML kích hoạt bộ chọn trực tiếp. Hủy chọn giữ bản nháp hiện tại. Bỏ callback của picker cũ sau khi đổi tệp hoặc đóng modal. |
| Kiểm tra ảnh | Giới hạn đề xuất `16 MiB`, `32 megapixel` và tối đa `8.192 px` mỗi cạnh trước decode. Kiểm tra signature và kích thước từ header, chuẩn hóa EXIF, downscale ảnh nhập về cạnh dài tối đa `2.048 px` trước khi đưa sang editor. Ảnh sai định dạng, hỏng hoặc vượt giới hạn có thông báo và giữ bản nháp đang mở. Giới hạn này cần thử bằng ảnh thật trên Web. |
| Căn ảnh | Kéo, zoom bằng slider/cuộn chuột, xoay bằng slider. Dùng một bộ offset, scale, rotation trong tọa độ khung mặt. Reset căn giữa và phủ đầy khung theo tỉ lệ ảnh. PNG trong suốt và vùng lộ khi xoay dùng nền mặc định. |
| Vẽ | Tách chế độ **Căn ảnh** và **Vẽ**. Cọ, màu, cỡ nét, tẩy, hoàn tác tối đa 20 nét và xóa lớp vẽ. Nét bám tọa độ UV của mặt. Tẩy chỉ xóa nét, căn lại ảnh chỉ đổi lớp ảnh. Dùng cùng viền mặt cho con trỏ, preview và kết quả. |
| Xem trước | Dùng một phép ghép cho nền, ảnh đã biến đổi và lớp vẽ. Render khi dữ liệu đổi vào buffer tái sử dụng. `RectMask2D` chỉ dùng để cắt giao diện. Chỉ đọc pixel và encode khi bấm Áp dụng. |
| Xác nhận | Ghép nền, ảnh và nét thành JPEG sRGB đục `512 × 512`. Viền mặt thuộc `FaceSurface`. Thử các mức chất lượng JPEG đã định trước để đạt giới hạn mạng `128 KiB` mà giữ nguyên độ phân giải. Nếu encode/kiểm tra thất bại, giữ bản nháp và mặt đã áp dụng trước đó. Owner và remote cùng render từ chính bytes JPEG đã xác nhận. |
| Hủy và khôi phục | Hủy bỏ bản nháp, giữ mặt đã xác nhận. **Dùng mặt mặc định** là thao tác xác nhận riêng, xóa mặt đã lưu và phát một revision mặc định mới nếu đang trong phòng. |
| Lưu và tải | Web lưu một record có phiên bản schema, layout, SHA-256 và JPEG cuối trong IndexedDB. Dùng giao dịch ghi/xóa, đọc lại và kiểm tra record khi mở game. Editor dùng adapter local phục vụ dev/test. Khi lưu thất bại, mặt đã áp dụng vẫn dùng được trong phiên hiện tại, UI báo rõ chưa lưu được cho lần sau và cho thử lại. Ảnh gốc, đường dẫn tệp và dữ liệu lớp vẽ chỉ thuộc phiên chỉnh sửa. |

Trước khi vào phòng, nút xác nhận ghi rõ **Lưu và chia sẻ khi vào phòng**. Trong phòng, ghi **Áp dụng cho cả phòng**. Giải thích ngay trong modal rằng ảnh cuối sẽ được gửi tới những người chơi trong phòng. Khi mở lại editor, có thể dùng kết quả đã lưu làm nền để vẽ tiếp. Muốn lấy lại phần ảnh đã crop cần chọn lại ảnh nguồn.

[JavaScript plugin trong Unity Web](https://docs.unity3d.com/6000.3/Documentation/Manual/web-interacting-browser-js.html) · [User activation của file picker](https://html.spec.whatwg.org/multipage/input.html#dom-input-showpicker) · [Giải mã theo chiều EXIF](https://developer.mozilla.org/en-US/docs/Web/API/Window/createImageBitmap) · [IndexedDB](https://w3c.github.io/IndexedDB/)

## 4. Hợp đồng đồng bộ Fusion Shared Mode

### State và authority

Mỗi `DogPlayer` có một manifest `[Networked]` nhỏ, đặt trên `NetworkDogPlayer` hoặc `NetworkBehaviour` riêng cùng `NetworkObject`. Owner ghi manifest khi có State Authority, sau khi đã tạo và giữ được payload hợp lệ trong bộ nhớ. Dùng một struct để commit manifest cùng revision. Dữ liệu gameplay và authority của Master tiếp tục theo [kiến trúc mạng](Specifications/NETWORK_ARCHITECTURE.md).

| Trường | Ý nghĩa |
| --- | --- |
| `Revision` | `0` là chưa công bố. Tăng sau mỗi thay đổi đã xác nhận hoặc thay đổi khả năng hiển thị khi đổi model. Phạm vi là vòng đời một avatar trong một runner. |
| `ModelId`, `LayoutVersion` | Client chỉ áp dụng khi `DogModelId` và layout renderer khớp. Chờ dữ liệu model/visual nếu chúng đến sau manifest. |
| `Kind` | `Default` hoặc `CustomJpeg`. `Default` có size và hash rỗng, được áp dụng và ACK như một revision. |
| `ByteLength`, `Sha256` | Kích thước và hash đầy đủ của payload custom. Phiên bản giao thức quy định duy nhất JPEG `512 × 512`. |

Định danh đích là `NetworkId` của avatar trong runner hiện tại, kèm `StateAuthority`, `Revision` và hash. Nickname, ID model và room code không định danh một mặt. Dùng interest của avatar đủ để mọi client trong phòng quan sát manifest. Nếu sau này bật lọc theo vùng, phải giữ khả năng quan sát manifest toàn phòng.

### Truyền payload và xác nhận hiển thị

Sử dụng `NetworkRunner.SendReliableDataToPlayer` và callback `OnReliableDataReceived(..., ReadOnlySpan<byte> data)` của Fusion 2.1. Fusion chia và ghép fragment. Dữ liệu ảnh đi theo yêu cầu, tách khỏi snapshot gameplay. Các message mang phiên bản giao thức, `NetworkId`, revision, hash và ID request khi cần tương quan. `ReliableKey` dùng namespace của tính năng và ID stream riêng cho từng lần gửi. [Photon Fusion Data Streaming](https://doc.photonengine.com/fusion/v2/manual/data-transfer/data-streaming) · [Chữ ký SDK đã cài](../Assets/Photon/Fusion/Assemblies/Fusion.Runtime.xml)

1. **Publish.** Owner cập nhật manifest khi bấm áp dụng hoặc khi spawn với mặt đã lưu và model tương thích. Nếu đang đổi model, chờ model mới được chọn thành công. Khi đổi sang model cũ, công bố mặt mặc định cho model đó. Khi quay lại biến thể hỗ trợ, công bố lại mặt đã xác nhận bằng revision mới.
2. **Fetch.** Khi thấy avatar spawn hoặc manifest đổi, client áp dụng `Default`, dùng texture cùng hash còn trong cache, hoặc gửi `FetchFace` đến `Object.StateAuthority`. Client join muộn chạy cùng luồng này từ snapshot. Owner dùng `Runner.IsPlayerCommitted()` để chỉ trả cho thành viên còn trong phòng đang hỏi đúng avatar, revision và hash hiện hành. Mỗi peer có tối đa một yêu cầu đang chờ cho revision này. Retry thay ID request đang chờ và phản hồi vẫn phải khớp ID mới nhất.
3. **Receive.** Owner gửi `FaceData` chứa đúng JPEG đã lưu trong bộ nhớ. Client kiểm tra runner, `PlayerRef` nguồn do Fusion cung cấp, authority hiện tại, request đang chờ, model/layout, revision, kích thước và SHA-256. Kiểm tra header JPEG đúng `512 × 512` trước decode và kiểm tra lại sau decode. Chỉ áp dụng sau khi tất cả điều kiện còn đúng và renderer đã sẵn sàng. Bỏ reply cũ, ngoài phòng, sai người, sai revision hoặc sai hash. Trong lúc chờ, dùng mặt trước đó nếu cùng model/layout, hoặc mặt mặc định.
4. **Applied.** Client gửi `FaceApplied` sau khi bind texture lên renderer của avatar, kể cả khi lấy từ cache hoặc chuyển về mặc định. Owner chỉ đếm ACK đúng revision từ peer đang trong phòng, mỗi peer tính một lần. Dùng `Runner.CommittedPlayers` để cập nhật mẫu số, loại chính owner. Người vào mới thêm vào danh sách chờ và người rời được bỏ khỏi danh sách. UI tách **đã lưu trên thiết bị** khỏi **đã hiển thị `n/N` người trong phòng**. Nếu chậm hoặc có lỗi, hiện trạng thái chờ/thử lại. Callback tiến độ của stream không đại diện cho việc mặt đã hiển thị.

### Giới hạn, retry và vòng đời

- Ngân sách khởi điểm gồm `128 KiB` JPEG mỗi mặt, tối đa hai lượt gửi ảnh đang chờ ACK mỗi owner, hai lượt fetch đang chờ mỗi client, cooldown áp dụng 5 giây và giới hạn decode một ảnh mỗi frame. Chia hàng đợi công bằng giữa peer. Giới hạn số mục theo thành viên trong phòng, trả lượt gửi khi ACK hoặc timeout, bỏ lượt cũ khi revision đổi. Một phòng cấu hình tám người có tối đa bảy bản gửi cho một lần đổi mặt, tương đương `896 KiB` payload ảnh ở trần, chưa tính overhead và retry. Đây là ngân sách thử, cần đo trên build thật.
- Đặt timeout thử nghiệm 12 giây cho một lượt. Client nhận retry lỗi truyền tạm thời với backoff và jitter, tối đa ba lượt liên tiếp cho mỗi cặp avatar/revision rồi thử nền thưa khi peer/runner còn hợp lệ. Nếu owner vẫn thiếu ACK, nút **Thử đồng bộ lại** công bố revision mới của cùng nội dung với cooldown, để peer đang có ảnh có thể ACK lại từ cache. Payload sai định dạng, sai kích thước hoặc sai hash bị loại trước khi áp dụng, và không được retry liên tục cho cùng revision. Bỏ hàng đợi, ACK và reply cũ khi đổi revision, đổi authority, despawn hoặc rời room.
- Giữ bytes ảnh xác nhận của owner đủ lâu để phục vụ join muộn trong suốt vòng đời avatar. Cache texture remote theo layout và hash trong runner, có ref count từ renderer. Nhả texture khi hết tham chiếu, và dọn hết khi runner shutdown. Hủy buffer và callback của runner cũ trước khi nhận dữ liệu phiên mới. Owner rejoin tạo avatar mới, kiểm tra lại bản lưu và công bố lại. Khi Master rời, các owner còn lại tiếp tục phục vụ mặt của mình. Avatar đã despawn thì mặt cũng biến mất.
- SHA-256 kiểm tra tính toàn vẹn của bytes, còn authority và sender do Fusion dùng để xác định avatar được cập nhật. Các giới hạn tại callback bảo vệ tầng ứng dụng. Chỉ nhận phản hồi đã được yêu cầu, hạn chế fetch/response theo peer và bỏ qua tin không đúng giao thức. Client chỉ gửi JPEG cuối cho thành viên phòng hiện tại. Cache của người nhận tồn tại trong bộ nhớ và log chỉ ghi ID kỹ thuật, dung lượng, thời gian và mã lỗi.

Nếu spike cho thấy kích thước stream hoặc tải đồng thời vượt ngân sách Web, đo lại với mức JPEG thấp hơn và cửa sổ gửi hẹp hơn. Nếu vẫn không đạt, thay cách chuyển payload và chạy lại gate cả phòng. Bản chỉ hiển thị cục bộ là lát cắt dev.

## 5. Lát cắt triển khai

| Đợt | Công việc | Điều kiện đi tiếp |
| --- | --- | --- |
| 1. Spike Web và Fusion | Truyền một JPEG mẫu giữa hai Web client bằng SDK hiện tại. Test ảnh sát trần, late join và sender identity trong iframe itch.io. Thử bộ chọn ảnh và ghi/đọc IndexedDB khi reload. | Xác nhận transport, đường mở picker và lưu trữ hoạt động trong môi trường đích. Ghi kích thước, thời gian và lỗi nếu có. |
| 2. Art và editor | Variant mặt phẳng, preview chung, căn ảnh, lớp vẽ, xuất JPEG, lưu và khôi phục. | Hủy giữ mặt cũ. Mặt hiển thị đúng hướng. Preview và avatar dùng cùng layout. Không có input gameplay lọt qua modal. |
| 3. Đồng bộ avatar | Manifest, fetch/data/ACK, cache, retry, late join, publish khi spawn và xóa mặt. Tích hợp theo vòng đời runner ở `FusionSessionService` và cập nhật prefab/bake Fusion. Tăng `GameVersion` qua cấu hình hiện có để phòng chỉ ghép client cùng giao thức. | Hai client Web xem được hai mặt khác nhau trên cùng model. Client thứ ba vào muộn nhận đúng revision. Master rời không làm mất mặt của owner còn lại. |
| 4. Regression và nghiệm thu | Kiểm tra đủ ma trận bên dưới trên build Web tải lên itch.io, bao gồm phòng đủ tám client theo cấu hình hiện tại. | Có kết quả và bằng chứng cho từng gate. Trạng thái triển khai cập nhật sau khi có kết quả. |

Tách picker/storage Web khỏi editor, pipeline ghép ảnh khỏi presenter, và transport/cache khỏi `NetworkDogPlayer`. Cảnh vào game vẫn dùng `Assets/Game/Scenes/M1_FusionPreview.unity`. Dùng thư mục tính năng trong `Assets/Game/Scripts` và một `.jslib` riêng cạnh bridge clipboard. Mỗi phần có một chủ sở hữu rõ cho `Texture2D`, `RenderTexture`, material instance và buffer mạng. Giải phóng tài nguyên khi thay ảnh, đóng editor, despawn hoặc rời phòng.

## 6. Ma trận nghiệm thu

| Ca | Kết quả cần đạt |
| --- | --- |
| Hai client cùng model, hai mặt khác nhau | Mỗi client thấy cả hai mặt đúng chủ, gồm cả nét vẽ. Màu/hướng nhìn khớp JPEG đã xác nhận. Một lượt cập nhật được ACK sau khi hiển thị. |
| Client thứ ba vào muộn | Nhận đủ mặt hiện hành từ các owner, kể cả mặt xác nhận từ trước khi tạo phòng. Đổi mặt trong lúc client mới tải ảnh vẫn kết thúc ở revision mới nhất. |
| Đổi model, reset mặt và cập nhật liên tiếp | Không gắn nhầm mặt vào model cũ, avatar khác hoặc revision trước. Reset hiển thị mặt mặc định trên tất cả client. |
| Owner rời/rejoin, Master rời | Proxy và cache mặt của avatar đã despawn được dọn. Owner rejoin công bố lại từ bản lưu hợp lệ. Master rời giữa lượt truyền không làm hỏng đồng bộ giữa các owner còn lại. |
| Mạng gián đoạn và dữ liệu sai | Timeout báo chờ, retry có giới hạn và kết thúc đúng revision khi kết nối phục hồi. Payload quá cỡ, sai hash, hỏng JPEG, trùng hoặc sai sender bị loại. Callback đến từ runner đã đóng không thay đổi avatar mới. |
| Web picker, vẽ và lưu | Chrome/Edge desktop trong iframe itch.io. Thử ảnh dọc/ngang, EXIF xoay/lật, PNG alpha, tệp lỗi/quá giới hạn, hủy picker, tẩy, hoàn tác, hủy chỉnh, reload và trình duyệt chặn lưu. |
| Gameplay | Không kích hoạt nhặt/ném/cắn khi chỉnh mặt. Mặt đi đúng với đầu lúc chạy, nhảy, cắn, ngoạm đồ, chồng chó và knockdown. Tệp lỗi hoặc thiếu storage không cản vào phòng. |
| Tám client | Mỗi client có mặt riêng, xác nhận gần đồng thời. Mỗi client thấy bảy mặt remote đúng chủ. Lặp ca join muộn khi phòng có bảy người và thử ba đợt đổi mặt liên tiếp, quan sát hàng đợi, FPS, ping, memory và disconnect. |

Mục tiêu thử ban đầu trong cùng region với điều kiện mạng được ghi là mọi peer ACK trong 5 giây ở ca 2–3 client và 20 giây ở ca tám client, kể từ khi client nhận manifest. Ghi cả thời gian từ Áp dụng đến ACK cuối và từ join đến khi hiện đủ mặt. So với baseline M3 cùng môi trường, ghi frame time/FPS, spike, ping và dung lượng truyền. Lưu build ID, browser, số client, kết quả EditMode/PlayMode và bằng chứng Web. Gate release chỉ đạt khi đồng bộ đủ cả phòng ở cấu hình phát hành và không phát sinh regression gameplay chặn chơi.

Ưu tiên test EditMode cho kiểm tra header/giới hạn, map UV và nhận reply cũ/sai nguồn. Dùng PlayMode để kiểm tra đóng modal, thay visual và bind lại mặt. Các ca picker trong iframe, mạng thật, join muộn và tải tám client cần Web build vì kết quả trong Editor không đại diện cho các đường chạy đó.

## 7. Kết quả triển khai trong project

- Đã chuyển model custom-face `modelId 1101` sang `BlankFaceDog.prefab`: dùng trực tiếp `FaceSurface` cong, UV `0–1`, texture blank, rig và animation của asset mới; không còn sinh plane/cắt mesh Photo Dog ở bước setup.
- Đã thêm modal chỉnh mặt dùng chung cho preview/gameplay: chọn ảnh, kéo, zoom, xoay, vẽ, tẩy riêng lớp nét, undo, xóa nét, reset, preview, áp dụng và khôi phục mặc định.
- Đã thêm bridge WebGL cho file picker/EXIF/downscale và lưu IndexedDB, cùng bản lưu `PlayerPrefs` có manifest, JPEG và SHA-256.
- Đã thêm manifest/revision trên `NetworkDogPlayer` và transport Fusion reliable có fetch/data/ACK, giới hạn payload, xác thực sender/hash/model/layout, cache và retry.
- Đã tăng `GameVersion` thành `office-custom-face-v1`, cập nhật prefab/scene và thêm kiểm thử EditMode cho validator/catalog/prefab/binding.
- Kết quả local sau triển khai: EditMode `44/44` đạt; PlayMode `18/18` đạt; modal và mặt preview đã kiểm tra thủ công trong Editor.

Các gate Web chưa được tuyên bố đạt: picker trong iframe itch.io, đọc/lưu sau reload trên browser thật, hai/ba client với late join, chuyển host, payload sát trần và phòng tám client. Cần chạy đúng ma trận mục 6 trên Web build trước khi đổi trạng thái thành nghiệm thu phát hành.

## Tương thích runtime Fusion 2.1.4

Các DLL Fusion Runtime trong repo cấp `InternalsVisibleTo("DogSimulator.Fusion")`. Weaver 2.1.4 sinh lời gọi tới `NetworkBehaviourUtils.CheckInvokeRpc`, một hàm `internal`. Quyền này cần có trong DLL đang dùng và cả biến thể Debug/Release đi kèm. Kiểm tra lại khi thay SDK.
