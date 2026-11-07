# Biên bản nghiệm thu M3-A/M3-B và chuẩn bị M3-C

**Ngày:** 07/11/2026, Asia/Saigon
**Build/version logic:** `office-m3-knockback-v2`
**Kết luận:** nghiệm thu phạm vi **ngoạm + đồ vật + sân nhỏ + chồng chó** cùng các bản vá gameplay phát sinh. Chuyển bước tiếp theo sang **M3-C — NPC phản công**. NPC, ragdoll mang được và xe chưa được tính là đã triển khai.

## 1. Phạm vi đã nghiệm thu

### M3-A — Sân thử và đồ vật

- Có sân blockout nhỏ, đồ carryable gồm bóng, xương và thùng, khu thử co-op, mục tiêu bóng, nút reset sân và đệm nhún.
- Một vật chỉ có một carrier; world/Master authority quyết định state và phát pose qua Fusion. Request dùng replicated state + sequence, không quay lại RPC từng gây `MethodAccessException`.
- Chuột trái khi tay không nhặt vật gần miệng; chuột trái khi đang cầm ném vật; `E` thả nhẹ; `R` reset sân.
- Thả thường không cộng lực. Ném kế thừa quán tính ba chiều của chó/miệng, cộng lực tới trước và hướng lên.
- Vật ném trúng chó gây knockback và ngã; vật chỉ đang lăn không bị coi như một cú ném mới. Phép quét va chạm dùng capsule thân chó, không chỉ so với điểm gốc ở chân.
- Rigidbody tự do có linear/angular damping và sleep threshold: bóng còn lăn một đoạn, thùng/xương dừng nhanh hơn và cuối cùng ngủ hẳn.
- Đã bỏ prototype súng/bóng đạn. Cơ chế gây ngã chính là ném đồ, va chạm tốc độ cao và tiếp đất mạnh.

### M3-B — Chồng chó và va chạm người chơi

- Spawn nhiều người cùng lúc được tách vị trí; CharacterController không còn kẹt vì chồng đúng một điểm.
- Người chơi được chọn trùng model chó; roster không còn là giới hạn số người vì reservation độc quyền.
- Chó có thể đẩy nhau. Va chạm đủ mạnh hoặc rơi từ cao gây knockback/ngã, không gây mất máu.
- Chó đứng trên chó khác dùng quan hệ support + tọa độ local tương đối; không đổi parent NetworkObject. Proxy dựng pose trên từ pose support đang hiển thị để giảm độ trễ mạng.
- Có chống chu trình support và giới hạn tầng. Khi nhảy, rời mặt lưng, support mất hoặc respawn thì quan hệ support được tháo an toàn.
- HUD hiển thị ping và simulation tick rate để quan sát chất lượng mạng.

### Trạng thái ngã

- Hình ảnh ngã nghiêng dần trong khoảng 0,35 giây thay vì xoay tức thì.
- Nằm tối thiểu 0,8 giây. Sau đó chỉ đứng lên khi đã chạm đất và vận tốc ngang/dọc xuống dưới ngưỡng ổn định; nếu còn bay hoặc trượt thì tiếp tục nằm.
- Khi đủ điều kiện, đứng lên ngay. Va chạm mới trong lúc đang nằm gia hạn trạng thái.
- Đây là visual knockdown trên CharacterController, chưa phải ragdoll nhiều khớp.

### WebGL/itch.io

- Nút copy mã phòng có bridge WebGL riêng: ưu tiên copy đồng bộ trong click bằng `document.execCommand('copy')`, fallback sang `navigator.clipboard.writeText()` trên secure context.
- Editor/desktop giữ `GUIUtility.systemCopyBuffer`.
- Bản vá này cần được xác nhận lại trên build WebGL mới tải lên itch.io; bản build/cache cũ không chứa `.jslib` mới.

## 2. Điều khiển hiện tại

| Input | Hành động |
| --- | --- |
| WASD | Di chuyển |
| Shift | Chạy |
| Space | Nhảy |
| Chuột trái | Nhặt vật / ném vật đang cầm |
| E | Thả nhẹ vật đang cầm |
| R | Reset đồ vật trong sân |
| F | Sủa |
| Chuột phải | Cắn |
| G / H | Poop / pee |
| Esc | Mở menu pause |

## 3. Bằng chứng và giới hạn nghiệm thu

- Source hiện compile qua các lượt sửa cuối; không còn compiler error được quan sát trong Unity Editor log.
- Các suite lưu bền vững trong `TestResults/` là baseline cũ: `Fusion-EditMode.xml` 30/30, `Fusion-PlayMode.xml` 9/9 và `PrivateRoom-PlayMode.xml` 10/10. Không dùng các số này để tuyên bố tự động test những thay đổi M3 mới.
- Test input PlayMode đã được cập nhật cho `PrimaryCarry` và `DropCarry`, nhưng chưa có XML suite mới lưu vào `TestResults/` sau toàn bộ bản vá M3.
- Nghiệm thu gameplay dựa trên chuỗi playtest/phản hồi và các bản sửa trực tiếp ngày 06–07/11. Chưa có benchmark bandwidth, FPS Web hoặc load test 4/8 người mới.
- Vì có thay đổi layout `[Networked]`, mọi client phải dùng cùng build và kết nối lại; không hot-mix build cũ/mới trong một phòng.

## 4. Checklist đóng build M3-A/M3-B

- [ ] Build WebGL release mới, tải đầy đủ lên itch.io và xác nhận nút Copy hoạt động trên Chrome/Edge trong iframe.
- [ ] Hai client cùng build: nhặt tranh một vật chỉ có một người thắng; ném trúng remote player làm remote ngã.
- [ ] Hai client chồng nhau khi người dưới đi, xoay, nhảy và rời phòng; người trên không tụt/kéo lặp hoặc bay khỏi sân.
- [ ] Spawn đồng thời 2–3 client không kẹt; cùng chọn một model vẫn spawn được.
- [ ] Bóng lăn rồi dừng; thả bằng `E` không ném; chuột trái lần hai có lực ném và quán tính dọc.
- [ ] Ngã nghiêng dần, tiếp tục nằm khi còn trượt/bay và chỉ đứng lên khi ổn định.
- [ ] Lưu XML EditMode/PlayMode mới và ghi browser, build ID, số client, ping/FPS vào `TestResults/` hoặc báo cáo test.

Các mục trên là regression/release evidence còn cần lưu, không mở lại phạm vi thiết kế M3-A/M3-B trừ khi phát hiện lỗi chặn.

## 5. Bước tiếp theo — M3-C NPC phản công

### Mục tiêu lát cắt

Một NPC duy nhất đi tuần trong sân, phản ứng khi bị chó cắn, đuổi và đấm có báo hiệu. NPC có 3 HP; hết HP chuyển sang trạng thái `Downed` tạm thời nhưng **chưa bật ragdoll mang được** trong M3-C.

### State machine đề xuất

`Patrol → Alert → Chase → Windup → Punch → Recovery → Patrol`
`HP = 0 → Downed`

- Master/world authority chạy NavMesh, chọn mục tiêu, HP, phase và timer.
- Owner của chó chỉ gửi bite request có sequence. Master xác thực phase, khoảng cách, cooldown và mục tiêu trước khi trừ HP.
- Punch tạo knockback/knockdown qua request/state hiện có của avatar; không dùng `AddForce` lên CharacterController và không trừ HP của chó.
- Không dùng RPC gameplay mới cho lát cắt đầu. Dùng networked request/sequence đã tránh được lỗi Fusion `CheckInvokeRpc`.
- Proxy chỉ render Animator/FX; không tự chạy AI hoặc tự áp damage.

### Thông số bắt đầu playtest

| Thông số | Giá trị đầu |
| --- | ---: |
| NPC | 1 |
| HP | 3 |
| Bite cooldown | 0,7 giây |
| Punch windup | 0,5 giây |
| Punch recovery | 1 giây |
| Mất mục tiêu | 5 giây không tiếp cận được |
| Knockback chó | Dùng hệ thống knockback hiện tại, không damage |

### Thứ tự triển khai

1. Blockout NPC prefab, capsule/agent, waypoint và NavMesh sân.
2. Master-authoritative patrol/chase với root pose replicated.
3. Bite request + HP + hit reaction, chống request lặp.
4. Windup/punch/recovery và knockback chó.
5. Downed placeholder, reset sân, disconnect/Master handoff và late join.
6. Hai-client Web playtest, lưu test evidence rồi mới mở M3-D ragdoll.

### Gate M3-C

- Hai client thấy cùng HP/state/target; một bite chỉ trừ một lần.
- NPC không đổi mục tiêu mỗi frame, không đấm xuyên sân và không stun-lock chó.
- Windup đủ rõ để né; punch chỉ áp dụng trong đúng cửa sổ/range.
- Late join thấy đúng HP/state hiện tại; Master rời không nhân đôi NPC hoặc reset HP sai.
- Reset sân đưa NPC về waypoint/HP/state chuẩn.
- Web build giữ FPS/ping trong budget playtest đã ghi; không suy rộng thành capacity 8 người nếu chỉ thử 2.

## 6. Ngoài phạm vi bước kế tiếp

- Ragdoll nhiều khớp và ngoạm NPC gục: M3-D sau khi M3-C đạt gate.
- Art pass/số NPC lớn hơn một: M4-A.
- Xe lái: M4-B experimental.
- HP/death của chó, súng, projectile gây damage và combat competitive: không nằm trong phạm vi hiện tại.

Roadmap chi tiết được cập nhật tại [NEXT_STEPS_GAMEPLAY_M3.md](NEXT_STEPS_GAMEPLAY_M3.md).
