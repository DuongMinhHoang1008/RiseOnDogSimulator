# DOG SIMULATOR — GAME DESIGN & IMPLEMENTATION BOUNDS

> **Phiên bản:** 1.0 — 04/11/2026  
> **Trạng thái:** Implementation specification cho Codex; không phải bằng chứng dự án đã chạy 20 CCU.  
> **Đối tượng:** 10–20 đồng nghiệp/bạn bè chơi chung qua trình duyệt PC.  
> **Nền tảng:** Unity WebGL phát hành trên itch.io.  
> **Mạng:** Photon Fusion 2 **Shared Mode**, Photon Cloud, phòng riêng bằng mã.  
> **Nguồn kiến trúc:** `NETWORK_ARCHITECTURE(1).md` do chủ dự án cung cấp (network baseline v1.0).  
> **Phạm vi tài liệu này:** Gameplay + network policy chi tiết + module contract + ràng buộc Codex + tiêu chí nghiệm thu.

---

## 0. HƯỚNG DẪN BẮT BUỘC CHO CODEX

### 0.1. Mục tiêu

Triển khai một game **Dog Simulator Multiplayer — Office Chaos** quy mô nhỏ: một bản đồ văn phòng/sân công ty, 10–20 mẫu chó đại diện cho các thành viên, cùng khám phá và nghịch đồ vật, chơi các hoạt động/nhiệm vụ phối hợp. **Cảm giác vui và sự ổn định mạng quan trọng hơn mô phỏng vật lý thực.**

### 0.2. Quy trình bắt buộc trước khi code

1. Đọc `AGENTS.md` (nếu có), toàn bộ các tài liệu kỹ thuật liên quan, assembly definitions, package manifest và scene/prefab liên quan trong repo.
2. Khảo sát **Unity version thực tế**, **Fusion version thực tế**, Input System, các framework/controller đã tồn tại, mô hình assets chó và các script networking sẵn có. Không giả định repo trống.
3. Lập một danh sách ngắn: **cái đã có / thiếu / xung đột / kế hoạch theo milestone**; chỉ sửa sau khi đã xác định tác động.
4. Tái sử dụng kiến trúc, coding style, pattern đã có khi phù hợp; tránh viết hệ thống thứ hai làm trùng trách nhiệm.
5. Triển khai **từng milestone độc lập có thể chạy thử**. Không tự thực hiện tất cả tính năng ở một lần thay đổi lớn.
6. Sau mỗi milestone: báo cáo file thay đổi, cách setup trong Editor, bước test thủ công, rủi ro, phần chưa làm. Nếu không có Unity Editor hoặc không build được, nói rõ **chưa xác minh chạy thực tế**.
7. Nếu quyết định trong tài liệu không tương thích phiên bản SDK đang có: kiểm tra API/official docs theo đúng phiên bản, ghi rõ khác biệt; chọn điều chỉnh **nhỏ nhất**, không tự chuyển topology.

### 0.3. Hard bounds — không được tự ý vượt qua

- **KHÔNG** thêm Chaos Meter, điểm hỗn loạn toàn phòng, event do tích điểm gây ra, hoặc hệ thống tương tự dưới tên khác.
- **KHÔNG** thêm open world, map thứ hai, scene streaming, xe cộ, economy, shop, leveling, IAP, quảng cáo, battle pass hay hệ thống unlock skin.
- **KHÔNG** triển khai dedicated server, Host Mode, P2P tự xây, backend đăng nhập, Firebase, database cloud hoặc persistent world trong MVP.
- **KHÔNG** tự cập nhật Unity, Photon, URP/HDRP, packages hoặc thay đổi pipeline render để làm cho code “dễ hơn”.
- **KHÔNG** đưa toàn bộ chó và đồ vật vào full Rigidbody / full ragdoll. Không làm vật lý dây thừng, vật thể phá hủy liên tục hay mô phỏng lực hai chiều nhiều client.
- **KHÔNG** spawn NetworkObject cho mảnh vụn, particle, âm thanh hoặc animation thuần thị giác.
- **KHÔNG** thêm lớp abstraction chung chung, service locator mới, event bus toàn cục hay DI framework khi chưa có nhu cầu thực sự.
- **KHÔNG** tạo 20 player network prefabs / 20 controllers riêng; chỉ **1 network player prefab** và nhiều visual prefabs.
- **KHÔNG** thay đổi bản `NETWORK_ARCHITECTURE(1).md` gốc nếu chưa được yêu cầu; ghi rõ chỗ nào tài liệu này bổ sung quyết định gameplay cho baseline.
- **KHÔNG** tự tạo asset production và tuyên bố là đã hoàn tất nếu repo không có asset; dùng placeholder có nhãn rõ và ghi ra danh sách asset cần cung cấp.
- **KHÔNG** tự ý chuyển mục P1/P2 thành bắt buộc ở P0.

### 0.4. Implementation rules

- Đặt tên dễ đọc, chức năng rõ ràng; ưu tiên composition và một trách nhiệm/mỗi component.
- Dữ liệu chỉnh trong Inspector phải là `[SerializeField]`/`ScriptableObject` có giới hạn và validation phù hợp; **không rải magic number** trong gameplay.
- `[Networked]` chỉ được ghi bởi State Authority của object. Không dùng RPC như kho lưu trạng thái.
- Không đọc hoặc mutate `[Networked]` trước khi `Spawned()` nếu API không cho phép.
- Trong **Fusion Shared Mode**, sử dụng `Object.HasStateAuthority` để quyết định chủ thể điều khiển/ghi state. **Không viết logic như Client–Server dựa vào `InputAuthority`, `OnInput` hoặc rollback**.
- Input nút bấm ngắn được capture/buffer ở `Update`, tiêu thụ trong `FixedUpdateNetwork` khi ảnh hưởng network state; camera/UI có thể cập nhật ở `LateUpdate`/`Update`.
- Bất kỳ request nào đến object của Master hay owner khác đều phải kiểm tra: nguồn gửi, player hợp lệ, khoảng cách, trạng thái mục tiêu, cooldown, request trùng.
- Luôn có cleanup khi player rời, refresh tab, mất kết nối, Master đổi, scene/runner shutdown.
- Mọi thao tác có kết quả lâu dài (đồ vật đổ, nhiệm vụ hoàn thành, cửa mở, đồ đang được mang) phải dựng lại được cho **late joiner** từ replicated state.
- Đừng tối ưu hóa micro trước khi đo; ưu tiên chi phí mạng, animation, draw calls và GC trong WebGL.

---

## 1. TẦM NHÌN, PILLARS VÀ SCOPE

### 1.1. Core fantasy

Người chơi vào một tầng văn phòng vui nhộn, trở thành con chó mang tên và model riêng của mình. Có thể tự do di chuyển, tương tác với bàn ghế, công tắc, đồ chơi, đồ ăn, rủ bạn bè phối hợp thực hiện những trò nghịch ngợm. Không thắng/thua cả phòng; mỗi lượt là một buổi chơi tự do.

### 1.2. Pillars

1. **Be a Dog:** di chuyển, sủa, cắn, húc, ngậm/tha đồ; animation có biểu cảm.
2. **Dense Sandbox:** một map nhỏ nhưng có nhiều đồ vật phản ứng rõ ràng.
3. **Social Cooperation:** 2–5 người cùng giải trò nghịch; cả nhóm có thể chia nhau hoạt động; một sự kiện chụp ảnh có thể tập hợp 5–20 người.
4. **Fast to Join:** mở link → đặt tên/chọn chó → tạo hoặc nhập mã phòng → chơi.

### 1.3. Phân cấp phạm vi

| Nhóm | P0 — Playable MVP | P1 — Sau khi P0 ổn định | P2 — Chỉ nghiên cứu nếu còn thời gian |
|---|---|---|---|
| Người | Mục tiêu 10–20 (đo tải); 1 player prefab | Cải thiện UX/animation | Mobile browser nếu thực sự cần |
| Chó | Tối đa 20 visual model có sẵn, tên riêng | Thêm visual/emote không ảnh hưởng cân bằng | Cosmetic progression — hiện không chốt |
| Bản đồ | 1 map, 3 khu nối liền | Bí mật/chi tiết trang trí | Map bổ sung — **ngoài scope** |
| Actions | Đi/chạy/nhảy/sủa/cắn/húc; đá có thể reuse hit | Emote phong phú | Ragdoll — không làm mặc định |
| Interactables | Khoảng 15–20 **instance**, dùng 4–6 loại component | 5–10 instance mới nếu phù hợp | Rigidbody thực nghiệm riêng |
| Nhiệm vụ | 3 nhiệm vụ: Phá cuộc họp; Công tắc bí mật; Đội vận chuyển | 3 nhiệm vụ: Đại án xúc xích; Giành đồ chơi; Chụp ảnh tập thể | Không có quest chain, rewards vĩnh viễn |
| Network | Create/Join/Leave, authority, late join, master handoff | UX reconnect tốt hơn | Dedicated/Host Mode — ngoài scope |

> **Phân biệt:** “20 đồ vật” = 20 đối tượng đặt trong map; KHÔNG phải 20 cơ chế/phân hệ riêng. Con số là content budget mục tiêu, có thể điều chỉnh sau review scene.

### 1.4. Không có các hệ thống sau

Không có account/login bắt buộc, lưu cấp độ, shop, currency, reward/cosmetic unlock, NPC hành vi phức tạp, global event meter, combat damage/HP, voice chat, text chat, matchmaking công khai hay leaderboard vĩnh viễn.

---

## 2. PLAYER JOURNEY / ROOM / CHARACTER SELECTION

### 2.1. Luồng UX

```text
Launch WebGL
  -> Main Menu
  -> Enter nickname (2–16 ký tự, kiểm tra rỗng/ký tự điều khiển)
  -> Choose DogModelId (1..N, N <= 20)
  -> [Create Room | Join by Code]
  -> Connect to Photon Shared Session
  -> Reserve DogModelId / resolve conflict
  -> Spawn DogPlayer
  -> Free Roam / Activities
  -> Leave Room -> Main Menu
```

- **Guest-only**. Có thể lưu nickname và model ưa thích **cục bộ** trong PlayerPrefs; không hứa dữ liệu tồn tại khi đổi trình duyệt/thiết bị.
- Nickname không cần duy nhất; **DogModelId phải duy nhất trong phòng đang hoạt động**. Cho phép chọn lại nếu model đã được người khác dùng.
- UI chọn model hiển thị trước khi join, nhưng **chỉ xác nhận giữ chỗ sau khi tham gia session** để giải quyết race condition. Nếu bị trùng: thông báo, hiển thị lựa chọn khác, chưa spawn hoặc spawn vào staging camera cho tới khi chọn xong.
- Khi mất kết nối/rời phòng: release reservation sau khi Photon ghi nhận player rời; reconnect là phiên mới và phải xác minh lại model còn trống.
- Không hardcode danh tính thật của thành viên trong code; danh sách asset có thể được đội dự án đặt tên theo model, nhưng game chỉ đồng bộ ID và nickname.

### 2.2. Room

- `GameMode.Shared`, Photon Cloud; WebGL qua itch.io (HTTPS).
- **Private/invite-code room**. Code gồm 6–8 ký tự khó đoán tương đối, không phải mật khẩu bảo mật.
- Dùng **một Photon region thống nhất cho cả nhóm**; region phải nằm trong code/invite metadata hoặc hiển thị rõ trong UI. Ban đầu có thể cố định region phù hợp cả công ty.
- Tạo phòng: sinh mã ngẫu nhiên, thử tạo session với `SessionName` tương ứng; xử lý collision: không được âm thầm nhập nhầm phòng tồn tại và tuyên bố tạo thành công. Nếu trùng, sinh lại và retry có giới hạn.
- Vào phòng: `EnableClientSessionCreation = false` để mã sai **không tạo phòng mới** (mặc định Shared có thể create-or-join).
- Chặn quá capacity; hiển thị `Room not found`, `Full`, `Version mismatch`, `Network error`, `Disconnected`.
- Phiên làm việc sống khi còn người chơi trong phòng; không có thế giới chạy sau khi tất cả rời.
- Một client có một `NetworkRunner` active; runner đã shutdown/failed phải hủy và tạo mới khi retry.
- `MaxPlayers` mục tiêu 20, nhưng phải xác minh SDK/config/quota; có thể đặt giới hạn 4 hoặc 8 ở dev builds.
- Với mismatch asset/config: dùng `GameVersion`/version key theo build để ngăn người chơi vào cùng phòng có bảng model/quest IDs khác nhau.

### 2.3. Handoff và lỗi

- Người tạo phòng không phải game server bắt buộc.
- `SharedModeMasterClient` điều phối world objects/quests. Sau master disconnect, master mới phải tiếp tục từ replicated state, không từ local dictionary/coroutine riêng.
- Phòng có thể trễ chuyển Master khi tab background/hang; có timeout UI, không hứa instant failover.
- Client disconnect: despawn avatar theo lifecycle, giải phóng model, xóa khỏi pressure plates, team game, carried items, quest contribution tạm.
- Cấm tự spawn duplicate avatar sau reconnect; sử dụng liên kết `PlayerRef -> player NetworkObject` đúng lifecycle.

---

## 3. MAP & CONTENT BUDGET

### 3.1. Một scene gameplay, ba khu liên thông

| Zone | Vai trò | Đồ vật / hoạt động |
|---|---|---|
| **Office** | Khu gặp nhau chính | 3 ghế húc đổ, máy chiếu, chuông, cửa bí mật, 2–3 pressure plates, bàn, máy in phản ứng |
| **Pantry** | Nơi tìm/tha đồ | 3–4 hộp/vật phẩm có thể mang, tủ lạnh tương tác, thùng rác, bàn đồ ăn |
| **Courtyard** | Khu chơi tập thể | Vùng giao hàng, khu ảnh nhóm, bục/đường nhảy, vật trang trí; khu kéo co (P1) |

**Map bounds:**

- 1 scene gameplay chính. Có thể dùng scene menu riêng nếu repo đang dùng; không có runtime scene streaming.
- Ưu tiên tầm nhìn thoáng; các điểm tương tác ở gần nhau, dễ gặp người khác.
- Khoảng 15–20 object **có logic**, không bao gồm props tĩnh trang trí. Mỗi prefab reusable; giữ world-state nhỏ.
- Chỉ 1–2 NPC đơn giản ở P1; ở P0 không cần NPC.
- Không dùng vật phá hủy bằng mesh fracture; ghế đổ là một animation/state hoặc preset transform.
- Không yêu cầu 20 model đang hoạt động có collider/rigidbody khác nhau theo giống chó.

### 3.2. Catalog P0 tối thiểu (instance/logic)

| Loại prefab | Số instance gợi ý | Hành vi | Network state |
|---|---:|---|---|
| `KnockableProp` | 3 ghế + 1 thùng | `Upright -> Knocked -> Reset` | `State`, `ResetTick` |
| `ToggleProp` | Máy chiếu, chuông, tủ lạnh, công tắc | `Off/On` hoặc event+timeout | `State`, `ResetTick` khi có |
| `CarryableProp` | 4 hộp/vật phẩm | `AtRest / Carried / Delivered` | `State`, `Carrier`, `RestPosition`/`DropPose` |
| `PressurePlate` | 3 | `Occupied` tính từ player positions | State gộp ở puzzle controller |
| `SecretDoor` | 1 | `Closed/Open` có timer | `IsOpen`, `CloseTick` |
| `DeliveryZone` | 1 | Check carryable IDs | Quest state, không cần riêng NetworkObject nếu là trigger thuần |
| `PhotoZone` | 1 (P1) | Đếm avatar trong vùng | Event/quest state |

Số lượng chỉ là kế hoạch phân bố; không nhân bản NetworkObject nếu một quest controller đã là nguồn state duy nhất. Static decoration không cần network.

---

## 4. PLAYER CONTROLLER / MODEL SYSTEM

### 4.1. Kiến trúc prefab

```text
DogPlayer [NetworkObject, NetworkTransform, CharacterController]
├── DogNetworkController (NetworkBehaviour: movement / action state)
├── DogInteractionController (target query / request dispatch)
├── DogIdentity (NetworkBehaviour: PlayerName / DogModelId)
├── VisualRoot
│   └── DogVisualPrefab (runtime visual by DogModelId; NO NetworkObject)
├── CameraTarget
├── MouthAnchor
└── HitOrigin / Hitbox (logic only)
```

- **1 network prefab cho tất cả player**; 10–20 **visual prefabs** gắn qua `DogModelCatalog` (ScriptableObject ID → prefab/Animator mapping).
- `DogModelId` là ID ổn định, không dùng index ngẫu nhiên từ Inspector giữa các build.
- Tùy asset: dùng skeleton/animation controller chung nếu tương thích; nếu không, mỗi visual có `DogAnimationAdapter` ánh xạ đến cùng **logical state**, KHÔNG tạo controller gameplay riêng.
- Game logic dùng collider/hit range **chuẩn hóa**, không phụ thuộc chiều dài chân/model. Visual scale, pivot, ground offset và mouth anchor chỉnh riêng ở metadata của từng model.
- Thiếu model asset: fallback placeholder, log warning ID; không crash cả phòng.
- Giới hạn 20 model ở build, nhưng chỉ instantiate model của người chơi thực đang có mặt. Không tải mesh runtime qua Photon.

### 4.2. Movement

- Cơ chế **CharacterController hoặc kinematic motor đã có trong repo**; không full Rigidbody và không full ragdoll.
- Đi/chạy/nhảy, xoay theo hướng di chuyển, camera third-person theo local owner.
- Mỗi player tự làm State Authority cho avatar. `FixedUpdateNetwork` tính chuyển động; remote render bằng state interpolation/NetworkTransform.
- Không bật player-player collision đẩy nhau bằng lực vật lý. Dùng overlap/hit query riêng để tương tác, tránh người chơi kẹt trong đám đông.
- Nếu sử dụng knockback: di chuyển cưỡng bức ngắn có kiểm soát qua motor, không `AddForce()` trên Rigidbody.

**Giá trị tuning khởi tạo (không phải yêu cầu cứng):** đi 4–5 m/s; chạy 6–7 m/s; nhảy cao ~1–1.5 m; tương tác trong 2 m; húc/cắn trong 1.5–2 m; cooldown 0.6–1 s; knockback 0.25–0.4 s. Đặt trong `DogMovementConfig`/`DogActionConfig`.

### 4.3. Input PC WebGL

| Input mặc định | Hành động |
|---|---|
| WASD | Di chuyển |
| Mouse | Xoay camera |
| Shift | Chạy |
| Space | Nhảy |
| Left Mouse | Húc/đá (một hit type, có thể chọn animation) |
| Right Mouse | Cắn (hit reaction hoặc ngậm nếu vật tương thích) |
| E | Interact / Pickup / Drop |
| F | Sủa |
| Tab | Bảng nhiệm vụ ngắn / hướng dẫn |
| Esc | Menu/Leave, xử lý pointer lock |

Input mapping là đề xuất; Codex phải giữ input solution đang có trong repo, không cài Input System mới một cách máy móc.

### 4.4. Network state avatar

| State đề xuất | Nguồn ghi | Ghi chú |
|---|---|---|
| `PlayerName` | Avatar owner | NetworkString giới hạn độ dài |
| `DogModelId` | Avatar owner **sau khi được Master chấp thuận** | Không chấp nhận ID đã được giữ |
| Position/Rotation | Avatar owner | NetworkTransform |
| `LocomotionState` | Avatar owner | Idle/Walk/Run/Jump/Knockback |
| `ActionState`, `ActionSequence` | Avatar owner | Dùng để render animation, tránh lặp action cũ |
| `CarriedObjectId` (nếu cần) | Theo quyết định authority của carryable | Không tạo hai nguồn sự thật xung đột |

**Quy tắc lựa chọn model:** Master giữ registry các ID đang sử dụng và validate các request. Kết quả chấp thuận phải được áp dụng trước khi công bố DogModelId chính thức. Registry phải rebuild từ avatar networked states hoặc từ state Master-owned khi Master thay đổi.

---

## 5. INTERACTION FRAMEWORK (ƯU TIÊN TÁI SỬ DỤNG)

### 5.1. Bộ hành động logic

| Logic | Player action | Kết quả | Physics |
|---|---|---|---|
| **Hit** | Húc, đá, cắn vào target | Target nhận hit type + reaction | Query + animation/scripted knockback |
| **Interact** | E gần mục tiêu | Toggle/activate | Không cần Rigidbody |
| **Carry** | Ngậm/nhặt; mang; thả | Attach vào MouthAnchor, drop pose | Kinematic state, không cần joint vật lý |
| **Hold/Push** | Giữ E cạnh đối tượng | Progress/animation/preset move | Scripted, không continuous physics |
| **GroupTrigger** | Nhiều player cùng đứng/giữ | Cửa mở, team progress, group photo | Master kiểm tra networked state |

**Sủa:** action có cooldown và animation/âm thanh. Có thể gửi một event tương tác nếu trong phạm vi `BarkReactiveProp`; ở P0 không bắt buộc NPC phản ứng. Không gửi audio data qua mạng.

### 5.2. Contract tối thiểu cho world interactables

Mọi interactable có:

- `InteractableId`: ID ổn định trong scene; validate không trùng.
- `SupportedActions`: tập hành động chấp nhận.
- `InteractionRange`, `Cooldown`, `ResetPolicy` trong config.
- State enum nhỏ, rõ nghĩa; state gốc/active/reset có thể dựng lại từ `[Networked]`.
- `TryApplyAction(PlayerRef source, ActionType action, ...)` **chỉ được gọi tại authority**. Tên method minh họa, không bắt buộc implement interface chung nếu không hữu ích.
- Render animation/VFX/audio theo state; **render không thay đổi authoritative state**.

Không bắt mọi prefab phải kế thừa một base class khổng lồ. Dùng component nhỏ như `KnockableProp`, `ToggleProp`, `CarryableProp`, `PressurePlatePuzzle`.

### 5.3. Luồng chuẩn request-to-authority

```text
LOCAL INPUT
  -> Local dog animation / feedback (nếu an toàn)
  -> Local hit / interact query
  -> RPC request to target State Authority
  -> Authority validate (source, target, range, cooldown, current state)
  -> Authority mutate [Networked] state
  -> Other peers observe state + render result
  -> Late join receives current state (không cần replay RPC)
```

- `RpcInfo.Source` là danh tính sender khi xử lý request; **không tin** `PlayerRef` tự khai trong payload.
- Không tin hoàn toàn client hit result; authority tự tra cứu vị trí attacker/target từ network state và cho phép tolerance hợp lý do độ trễ.
- Request duplicate/retry không được tạo hai lần cùng một side effect. Dùng cooldown + request sequence/last processed marker nếu cần.
- Nếu authority đang chuyển Master: request có thể fail; UI/action phải có timeout và được retry thủ công, không tự spam.
- RPC payload nhỏ: `TargetId`/`ActionType`/`ActionSequence`/tham số cần thiết; không serialise GameObject/Collider, mesh hay transform hierarchy.

### 5.4. Cắn/húc chó khác (dog-to-dog)

- Chó tấn công sở hữu `ActionState`, tự phát animation ngay khi bấm.
- RPC nhắm tới **State Authority của avatar mục tiêu**. Target owner xác thực attacker đang ở khoảng cách hợp lý, attack không quá cooldown, mục tiêu đang hợp lệ.
- Khi thành công, target owner áp dụng `KnockbackState` bằng motor với hướng và thời lượng **giới hạn cứng**; target transform sync lại cho người khác.
- Không dùng rigidbody physics collision gây lực qua mạng; không dùng HP, stun lock dài, ragdoll mặc định.
- Có immunity window ngắn sau khi bị húc để tránh 10 người giữ một người không di chuyển được; gợi ý 0.4–0.8 s, tuning sau test.
- Nếu target owner cố tình giả mạo/bỏ qua hit, đây là giới hạn Shared Mode chấp nhận được với game công ty, không tìm cách giải bằng server mới.

### 5.5. Đồ vật có thể húc đổ (Knockable)

- `Upright -> Knocked -> Cooldown -> Upright`.
- Húc/chạm đúng điều kiện làm object chuyển state; mỗi client chạy cùng animation hoặc tween preset. Không simulate ghế bật tung bằng Rigidbody.
- Sau 15–30 s tự reset; reset do authority điều khiển dựa trên `TickTimer`/tick-derived time, không dựa vào coroutine cục bộ trên Master.
- Event `Knocked` được quest system nhận **một lần** trong mỗi chu kỳ.

### 5.6. Ngậm / tha / thả đồ (Carryable)

- State `AtRest`, `Carried`, `Delivered`.
- Khi pickup: authority kiểm tra **chưa có carrier**, sender hợp lệ và trong range → set `CarrierPlayerRef`/`CarrierObjectRef` + state.
- **Không chuyển State Authority của carryable sang người mang trong P0**. Master/world authority giữ ownership; render mỗi client tự attach visual vào `MouthAnchor` của avatar tương ứng. Đây là một nguồn state, không stream transform liên tục.
- Khi drop: người mang gửi request; authority kiểm tra đúng carrier; lưu **một drop pose** đã clamp về nền/điểm hợp lệ, không thả vật thành Rigidbody động.
- Khi bước vào delivery zone: authority chuyển `Delivered`, snap vào slot giao hàng; phát event tăng tiến độ quest **một lần**.
- Nếu carrier rời game: authority release và respawn tại vị trí gốc hoặc drop location an toàn; mặc định về spawn gốc.
- Không cho hai người cùng ngậm một item trong P0. Tương tác kéo co là minigame state-based riêng ở P1.

### 5.7. Công tắc / nhóm / vùng

- `Toggle`: request-to-master -> state mới -> visual trên tất cả client.
- `PressurePlate`: **Master tính người đứng trong vùng từ vị trí networked của avatar** (không tin hoàn toàn các OnTrigger RPC từ proxy).
- `GroupTrigger`: yêu cầu distinct `PlayerRef`; đếm tối đa 1 lần mỗi player; khi tất cả điều kiện cùng đúng liên tục trong thời gian cấu hình thì kích hoạt.
- Dùng tick time cho hold timer, không per-frame reliable RPC.

---

## 6. QUEST / ACTIVITY SYSTEM

### 6.1. Nguyên tắc

- **6 hoạt động độc lập**, không có quest chain hoặc reward vĩnh viễn. Người chơi tự do nhận/khởi động lại hoạt động.
- Hệ thống đơn giản: có `QuestId`, `State`, `Progress`, `Participants` nếu cần, `StartedTick`/`EndTick`/`ResetTick` và event completion.
- Nhiệm vụ là **shared room state**; có thể nhiều nhiệm vụ chạy song song mà không tranh nhau một item thiết yếu. Nếu chung item phải có policy độc quyền hoặc reset rõ ràng.
- Không đặt cờ hoàn thành dựa trên animation local. Chỉ world/quest authority ghi state.
- Có quest board đơn giản: tên, mô tả 1–2 dòng, số mục tiêu còn lại, trạng thái. Không tracking data cá nhân vĩnh viễn.
- Mục tiêu hoàn thành dẫn đến animation, confetti **local**, thông báo chung, và reset sau cooldown; không có tiền/xếp hạng toàn phòng.
- Bắt buộc có hành vi khi player rời, restart và late join.

### 6.2. State machine dùng chung

```text
Idle -> Active -> Completed -> Cooldown -> Idle
            \-> Failed/TimedOut -> Cooldown -> Idle
```

- `Idle`: có thể bắt đầu bằng interaction với quest board/trigger; các mini game dạng vùng có thể tự Active khi đủ người.
- `Active`: đếm tiến trình + timeout nếu cần; không tính lặp event.
- `Completed`: hiện result 5–10 s; khóa input hoàn thành trùng.
- `Cooldown`: trả props liên quan về baseline an toàn; quay về Idle.
- Nếu Master đổi giữa chừng, timer và state phải tiếp tục từ tick/network state; không tự bắt đầu hai lần.

### 6.3. Q01 — Phá cuộc họp (**P0**)

**Địa điểm:** Office.  
**Mục tiêu:** Trong vòng **60 s**, húc đổ **3 ghế**, bật máy chiếu và bấm chuông phòng họp.  
**Số người:** 1–5, khuyến khích hợp tác nhưng không bắt đủ người; bất cứ ai đều có thể tham gia.  
**Props:** 3 `KnockableProp`, 1 `ToggleProp` máy chiếu, 1 `Interact` chuông.  
**Network truth:** bitset/checklist `Chair1..3`, `ProjectorActivated`, `BellActivated`, `QuestState`, timeout.  
**Hoàn thành:** đủ 5 điều kiện trong time window → bảng “Meeting Ruined!”, hiệu ứng vui + tiếng còi local; cooldown ~20 s rồi reset.  
**Failure:** hết 60 s -> hiện thông báo, reset tất cả props của quest và có thể thử lại.

**Edge cases:**
- Ghế đã đổ trước khi nhiệm vụ Active không tự tính trừ khi quy tắc quest chủ động snapshot/reset lúc start; **mặc định reset ghế khi start**.
- Event húc ghế lặp không cộng hai lần.
- Nếu người thực hiện rời phòng, tiến trình shared vẫn giữ cho tới timeout.

### 6.4. Q02 — Công tắc bí mật (**P0**)

**Địa điểm:** Office.  
**Mục tiêu:** **3 player khác nhau** đứng trên **3 pressure plates** đồng thời ít nhất **2 giây** để mở cửa bí mật.  
**Số người:** 3–20 có thể tham gia; ba người đủ kích hoạt.  
**Network truth:** plate occupancy được Master suy ra từ player net positions; `HoldProgress`/`DoorOpen`/`CloseTick`.  
**Hoàn thành:** cửa mở **15–20 s**, ánh sáng và hiệu ứng mở; sau đó cửa tự đóng **nếu an toàn**. Không cần loot/điểm thưởng.  
**Reset:** khi đóng, quay `Idle`; cooldown ngắn chống spam.

**Edge cases:**
- Một player không thể đứng đồng thời trên hai plate để được tính hai người.
- Một player thoát/mất kết nối → plate bỏ chiếm; hold progress reset hoặc decay, không hoàn thành từ stale data.
- Khi cửa đóng, không kẹt hoặc teleport người chơi đang ở trong doorway; dùng trigger an toàn, trì hoãn đóng khi bị chắn.

### 6.5. Q03 — Đội vận chuyển bốn chân (**P0**)

**Địa điểm:** Pantry → Courtyard.  
**Mục tiêu:** Mang **4 món đồ** tới **4 slot giao hàng** (hoặc 1 vùng giao hàng với ID phân biệt).  
**Số người:** 1–6 người hiệu quả, không ngăn nhiều người khác chơi nhiệm vụ khác.  
**Cơ chế:** `Carry` + `Drop` + `DeliveryZone`.  
**Network truth:** mỗi item có unique ItemId, State, Carrier, slot; quest có delivered bitmask.  
**Hoàn thành:** 4/4 delivered; hiển thị bảng khen/animation; sau 20–30 s reset về vị trí đầu.

**Edge cases:**
- Cùng món không thể được giao hai lần.
- Carrier disconnect → trả đồ về spawn an toàn.
- Người khác có thể nhặt vật đang nằm trên sàn, **không thể giật đồ đang được mang**.
- Khi quest reset, player đang mang vật thuộc quest phải bị hủy trạng thái mang một cách đồng bộ.

### 6.6. Q04 — Đại án xúc xích (**P1**)

**Địa điểm:** Pantry.  
**Mục tiêu:** Đánh lạc hướng NPC bằng sủa/nhấn thiết bị, ngậm **3 xúc xích** đem về ổ chó.  
**Số người:** 1–4.  
**NPC:** duy nhất một NPC đơn giản với `Idle / Distracted / Alert`; không pathfinding phức tạp. Có thể thay bằng camera an ninh quay theo preset nếu không có NPC asset.  
**Network truth:** NPC state, 3 carryable states, objective checklist.  
**Reset:** NPC và item về trạng thái mặc định khi hoàn tất/timeout.  
**Bound:** Không làm stealth AI, chase AI, navigation mesh runtime phức tạp hoặc detection based on full physics.

### 6.7. Q05 — Đại chiến giành đồ chơi (**P1**)

**Địa điểm:** Courtyard.  
**Mục tiêu:** Hai đội đứng hai bên món đồ chơi và **giữ nút** để kéo thanh tiến độ về phía mình.  
**Số người:** 2–8, mỗi đội tối thiểu 1; tối đa 4/đội; người còn lại quan sát.  
**Mechanic:** `Hold/Push` + team registration; progress thay đổi có giới hạn theo số player active; visual kéo đồ bằng tween.  
**Network truth:** roster hai đội, `ActiveHold` per participant, signed progress, game state, timer.  
**Bound:** Không dùng rope/joint physics, không gửi RPC mỗi frame. Gửi **start/stop hold** và heartbeat có giới hạn nếu cần; Master tự tích phân progress theo tick.

**Edge cases:** client disconnect, switch team khi đang hold, mất focus/tab background, cả hai đội không bấm → ngừng progress; invalid join bị từ chối; clear state khi kết thúc.

### 6.8. Q06 — Chụp ảnh cả bầy (**P1**)

**Địa điểm:** Courtyard.  
**Mục tiêu:** Có **tối thiểu 5 player** đứng trong khung ảnh cùng lúc khoảng **2 giây**.  
**Số người:** 5–20.  
**Network truth:** số player hợp lệ, trạng thái countdown, completion sequence.  
**Presentation:** camera flash, khung ảnh và animation/emote tùy chọn.  
**Bound:** Không xây cloud image storage, gallery hoặc tính năng server lưu ảnh. Screenshot về máy chỉ là tính năng phụ, phải chứng minh hoạt động trong WebGL/browser trước khi chốt.

### 6.9. Rule chung chống tranh chấp

- Các quest dùng các **ID và props riêng**, trừ lúc intentionally shared. Nếu cùng một vật được sử dụng ở hai quest, phải định nghĩa owner/use lock trước khi code.
- Nhiệm vụ không phụ thuộc cả 20 người phối hợp; riêng group photo yêu cầu 5+.
- Hoàn thành nhiệm vụ không tạo reward vĩnh viễn. Không lưu quest progress sau khi tất cả người chơi rời phòng.
- Quest UI lấy từ networked state; client late join nhìn được trạng thái hiện tại, không phải start lại animation/event cũ.

---

## 7. NETWORK TOPOLOGY, AUTHORITY, REPLICATION

### 7.1. Topology bất biến trong P0/P1

```text
itch.io (hosts WebGL static files)
        |
   Browser A ... Browser N
        |        |
        +-- Photon Cloud --+
                  |
         Fusion Shared Session
        /                 \
  Client-owned Dogs    Master-owned World / Quests
```

- Không dedicated game server, không bắt người tạo phòng làm server.
- Photon Cloud lo kết nối/session và chuyển replicated state; **không mô phỏng physics/gameplay thay cho client**.
- Mỗi `NetworkObject` có **một State Authority** tại một thời điểm.

### 7.2. Bảng authority chuẩn

| Object | State Authority | Được viết gì | Ai request? |
|---|---|---|---|
| `DogPlayer` | Client điều khiển nó | Transform, movement/action state, nickname/model sau chấp thuận | Owner; targeted hit RPC từ peer khác |
| `WorldInteractable` scene objects | **SharedModeMasterClient** qua `Is Master Client` setting nếu phù hợp | Knocked/toggle/carry state, reset tick | Mọi player được phép request, Master validate |
| `QuestController` | **SharedModeMasterClient** | Quest state/progress/timer | Player request hoặc trigger từ world logic |
| `DogModelSelectionRegistry` | **SharedModeMasterClient** | Reservations/approved IDs | Người muốn chọn model |
| Local effects | Không có NetworkObject | VFX/audio/camera/UI | Local renderer |

**Master transfer:** dùng Shared Mode `Is Master Client` setting cho Master-owned scene objects theo Fusion SDK đang dùng; khi Master đổi, authority chuyển theo engine rules. Phải kiểm chứng trong build thực. **Không** ghi ghost state trong MonoBehaviour cục bộ rồi giả định nó tồn tại qua đổi Master.

### 7.3. RPC / Networked state

| Tín hiệu | Cơ chế | Quy tắc |
|---|---|---|
| `RequestInteract`, `RequestPickup`, `RequestHit` | RPC tới **StateAuthority** | Reliable nếu ảnh hưởng gameplay; validate nguồn |
| `PropState`, `Carrier`, `QuestState`, `QuestProgress` | `[Networked]` | Late join và Master swap phải đọc được |
| Player Position/Rotation | `NetworkTransform` | Không gửi RPC transform mỗi tick |
| `ActionState`/`ActionSequence` | `[Networked]` hoặc event cần thiết | Dùng một nguồn animation truth; tránh spam |
| Audio, debris, flash, dust | Local visual/audio | Không network object cho hiệu ứng |

Không dùng `InputAuthority` làm căn cứ kiểm soát trong Shared Mode, không copy ví dụ `OnInput` từ Host/Server rồi cho rằng hợp lệ. Các RPC từ non-owner có `RpcSources.All` chỉ được mở ở những method có xác thực đầy đủ.

### 7.4. Network life cycle

- World scene objects được baked đúng scene + network scene manager tương thích Fusion; ưu tiên preplaced thay vì spawn 20 props lúc runtime.
- Avatar spawn một lần / player, set mapping `PlayerRef` nếu tương thích flow hiện có.
- Spawn local visual từ catalog trên mọi peer sau khi state identity hợp lệ.
- On player leaving: despawn avatar; clear reservation, occupancy, hold, carried ownership.
- Late join: nhận world state, quest state, avatar identity, carry attach và door state **không cần RPC lịch sử**.
- Khi last player rời: session/world không persist.

### 7.5. Ràng buộc WebGL

- WebGL dùng transport SDK hỗ trợ, không dùng native UDP socket do tự code.
- Test Chromium browser, tab bị background/throttled, refresh, network loss.
- Không đặt credential bí mật trong WebGL frontend; Photon App ID không thay thế backend secret.
- Tất cả phiên bản build phải có cùng catalog IDs / scene object IDs.
- Không lưu private data nhạy cảm trong replicated state (nickname là dữ liệu công khai trong phòng).

---

## 8. MODULES / DATA CONTRACTS

### 8.1. Cấu trúc đề xuất — chỉ tạo module cần dùng

```text
Assets/Game/
├── Scripts/
│   ├── Networking/
│   │   ├── NetworkBootstrap.cs
│   │   ├── SessionService.cs
│   │   └── RoomCodeService.cs
│   ├── Player/
│   │   ├── DogNetworkController.cs
│   │   ├── DogIdentity.cs
│   │   ├── DogVisualController.cs
│   │   └── DogInteractionController.cs
│   ├── World/
│   │   ├── KnockableProp.cs
│   │   ├── ToggleProp.cs
│   │   ├── CarryableProp.cs
│   │   ├── PressurePlatePuzzle.cs
│   │   └── WorldStateCoordinator.cs
│   ├── Quests/
│   │   ├── QuestController.cs
│   │   └── QuestPresentation.cs
│   └── UI/
│       ├── MainMenuUI.cs
│       ├── RoomUI.cs
│       └── QuestBoardUI.cs
├── Config/
│   ├── DogModelCatalog.asset
│   ├── DogMovementConfig.asset
│   └── InteractionConfig.asset
├── Prefabs/
│   ├── Network/
│   ├── Dogs/
│   └── World/
└── Scenes/
    ├── Menu.unity
    └── OfficeWorld.unity
```

**Đây là hướng tổ chức, không phải yêu cầu bắt buộc tái cấu trúc repo đang có.** Tên lớp có thể thay cho khớp conventions hiện tại. Không tạo các file rỗng chỉ để giống sơ đồ.

### 8.2. Contract dữ liệu tối thiểu

```text
DogModelDefinition:
  ModelId: stable byte/int
  DisplayName: string
  VisualPrefab: GameObject
  AnimatorProfile: reference/adapter
  VisualScale / GroundOffset / MouthAnchorOffset

WorldInteractableDefinition:
  InteractableId: stable int
  SupportedActions: flags/enum
  Range: float
  Cooldown: duration
  ResetPolicy: None | Timed | QuestControlled

CarryableState:
  ItemId
  Status: AtRest | Carried | Delivered
  CarrierPlayerRef: valid only if Carried
  RestOrDropPose
  QuestOwnerId: optional

QuestState:
  QuestId
  Status: Idle | Active | Completed | Cooldown | Failed
  ObjectiveFlags / Progress
  StartTick / Deadline / ResetTick
  SequenceCounter (để UI không phát completion VFX trùng)
```

**Quy tắc dữ liệu:** ID có bảng mapping ổn định, được validate duplicate trong Editor/test; không dựa vào tên GameObject ở runtime; không đồng bộ string JSON lớn khi enum/ID đủ dùng. Trong code thật chọn kiểu Fusion-supported (`NetworkBool`, `NetworkString`, `PlayerRef`, `NetworkId`, `TickTimer`...) theo đúng SDK installed.

### 8.3. Chống coupling

- Quest logic không trực tiếp điều khiển Animator; quest phát event/đổi state → presentation xử lý.
- Interactables không trực tiếp thao tác UI.
- Dog controller không chứa logic riêng cho từng quest.
- UI không trực tiếp gán `[Networked]` state; đi qua local owner hoặc request tới authority.
- RPC routing không được nhúng trong 20 visual prefabs.
- Không cần một `INetworkService` siêu tổng quát chỉ để bọc vài API Fusion; wrapper hẹp cho Session/Room là đủ.

---

## 9. UX / PRESENTATION

### 9.1. UI tối thiểu

- Main Menu: nickname, model picker, Create Room, Join Room.
- In Room: room code + copy, số người / capacity, tên các thành viên, Leave.
- In Game HUD: hint khi tới gần object (`E Interact`, `Right Click Bite`, ...), quest board dạng compact, completion banner.
- Error UI: mã phòng sai, phòng đầy, model trùng, mất kết nối, version mismatch.
- Camera: third-person, tránh chui qua tường và tránh motion sickness do knockback.

### 9.2. Đồng bộ presentation

- Remote player có movement interpolation và animation dựa trên state; **không phát lại mọi frame hành động RPC**.
- Các hành động một lần sử dụng state transition/sequence để tránh lặp VFX sau frame update.
- Audio/particles mô phỏng local; nếu cần người khác nghe tiếng sủa thì gửi action code/timestamp, không truyền âm thanh.
- Có tùy chọn giảm hiệu ứng và giảm chất lượng rendering cho browser yếu.

---

## 10. PERFORMANCE / TECHNICAL BOUNDS

### 10.1. Budget thiết kế ban đầu (phải benchmark)

| Hạng mục | Mục tiêu ban đầu | Lưu ý |
|---|---|---|
| Người/phòng | 10–20 | **Chưa bảo đảm**, cần kiểm thử 2→4→8→12→20 |
| Network player prefab | 1 loại | Visuals theo ModelId |
| Visual dog models | Tối đa 20 | Chỉ instantiate những ai đang online |
| World interactable instances | 15–20 P0 | Ưu tiên scene-placed |
| Networked dynamic Rigidbody | **0** trong P0/P1 | Dùng scripted events / preset animation |
| NPC | 0 P0, tối đa 1–2 P1 | State-based, không AI phức tạp |
| Quest | 3 P0 + 3 P1 | Không thêm tự động |
| Fusion simulation tick | Giá trị thử nghiệm 30 Hz | Đo thực tế, không hardcode khắp nơi |
| Target FPS | >=30 trên thiết bị PC thử nghiệm đã xác định | Không tuyên bố đạt nếu chưa đo |
| Network message | Không per-frame RPC cho hold/movement/effects | Kiểm tra traffic thực tế |

**Không đặt giới hạn bandwidth byte/s, CPU ms, tris/draw-call cụ thể khi chưa có máy mục tiêu và profiler.** Codex phải đo, ghi số liệu và nêu cách tái hiện.

### 10.2. Hiệu năng WebGL

- Dùng object pooling cho VFX nếu phát thường xuyên; không pooling mọi thứ vô cớ.
- Giảm Animator complexity, texture/material variants; batch/instancing khi phù hợp asset.
- Tối ưu tổng bộ nhớ và load time vì build WebGL phải được tải xuống qua browser.
- Không dùng threads/natives APIs không hỗ trợ WebGL.
- Tránh allocations trong `FixedUpdateNetwork`, đặc biệt là `FindObjectsOfType` hoặc LINQ trên toàn player mỗi tick.
- `GroupTrigger` có thể query snapshot 20 player tối đa, không phải high-frequency physics simulation.

---

## 11. TRIỂN KHAI THEO MILESTONE (DÀNH CHO CODEX)

### M0 — Repo audit & skeleton

**Đầu ra:** liệt kê Unity/Fusion versions, scenes, dog assets/controller hiện có, dependencies, chỗ sửa; cập nhật kế hoạch.  
**Chưa làm:** không tự nâng version, không code gameplay lớn.

**Done khi:** xác nhận một architecture path không xung đột code repo, biết cách test Editor/WebGL.

### M1 — Room & multiplayer movement

- Create/Join/Leave bằng room code và Shared Mode, strict join không auto-create.
- Một prefab `DogPlayer` spawn owner đúng, có Name/ModelId, movement và camera.
- Test 2 browsers khác mạng; reconnect/refresh/leave.

**Done khi:** hai client thấy nhau, mỗi client chỉ điều khiển avatar của mình, không duplicate, room code sai không tự tạo phòng.

### M2 — Model catalog & 10–20 dog visuals

- `DogModelCatalog`, runtime visual instantiation, normalization collider/mouth anchor.
- Model selection/reservation tránh trùng; fallback missing assets.
- Không đòi dựng 20 model trong code; dùng assets sẵn, placeholder rõ nếu thiếu.

**Done khi:** mỗi người thấy cùng DogModelId / nickname từ mọi browser, không chọn trùng model lúc đồng thời, một model thiếu không crash.

### M3 — Interaction vertical slice

- Hit/húc chó khác + short knockback; Bark.
- `KnockableProp`, `ToggleProp`, `CarryableProp` và request/validation/replication.
- Thử riêng các case: cùng hit object, cùng nhặt item, drop rồi reconnect, Master swap.

**Done khi:** hai client thấy cùng trạng thái ghế, máy chiếu, carried item; item không có 2 carrier; late join dựng đúng.

### M4 — Ba nhiệm vụ P0 & map

- Office, Pantry, Courtyard trong 1 map.
- Q01 Meeting; Q02 Secret Switch; Q03 Delivery; UI ngắn gọn.
- Reset/timeouts/network state và late join/master migration.

**Done khi:** 3 quest hoàn thành/reset được trong session nhiều người, người mới vào thấy đúng current progress, Master rời không phá trạng thái.

### M5 — WebGL build & load test

- Build WebGL trên itch.io, kiểm tra HTTPS/iframe, fullscreen, pointer lock, startup, network.
- Test 2→4→8→12→20 người trên máy/trình duyệt thực; đo FPS, memory, RTT, bandwidth nếu SDK hỗ trợ.
- Fix trước các lỗi ảnh hưởng nhiều người, không mở thêm content để che lỗi performance.

**Done khi:** có nhật ký test/báo cáo thực tế và giới hạn capacity đã chứng minh. Nếu chỉ chạy ổn 8 người thì ghi capacity tạm **8**, không tuyên bố hoàn thành 20.

### M6 — Nội dung P1 (chỉ sau khi M5 có kết quả)

Q04 Sausage, Q05 Tug of War state-based, Q06 Group Photo. Ưu tiên trò chơi được nhóm bạn yêu thích từ playtest; không bắt buộc phải làm cả ba nếu mục tiêu đã đạt.

---

## 12. ACCEPTANCE / FAILURE TEST MATRIX

| Test ID | Tình huống | Kỳ vọng |
|---|---|---|
| NET-01 | 2 browser, 2 mạng khác nhau | Join cùng room, thấy nhau |
| NET-02 | Join mã không tồn tại | Báo lỗi, **không tạo session** |
| NET-03 | Full room | Không over-capacity hoặc spawn avatar vượt limit |
| NET-04 | Master rời giữa nhiệm vụ | Master mới tiếp tục progress/timer; không duplicate completion |
| NET-05 | Late join khi ghế đổ/cửa mở/item đang carried | Render đúng từ state hiện tại |
| NET-06 | Refresh tab khi đang mang item | Item được release/reset, không kẹt ghost carrier |
| NET-07 | Hai client cùng pickup một item | Đúng **một** carrier |
| NET-08 | Hai người cùng chọn model | Chỉ một người được xác nhận ID đó |
| NET-09 | Spam E/Hit/F | Không lặp side effect, không crash/disconnect |
| NET-10 | Client vào background 30–60 s | Có disconnect/recovery behavior rõ ràng; không orphan state |
| MOVE-01 | 10+ chó tụ tập, chạy/húc | Không physics explosion/stun lock; không mắc kẹt lâu |
| MOVE-02 | 2 người cùng húc 1 người | Knockback bounded, xử lý request hợp lệ |
| PROP-01 | Ghế Knocked + timed reset | Đồng bộ, không cộng event nhiều lần |
| PROP-02 | Thả/đặt và giao hàng | Trạng thái giống nhau trên các client |
| Q-01 | Meeting: đủ 5 mục tiêu | Complete đúng 1 lần, reset đúng |
| Q-02 | 3 người đứng 3 plate, một người rời | Hold reset; không mở cửa nếu không đủ 3 |
| Q-03 | Delivery: 4 món, carrier rời | Không tiến độ ảo; có đường hoàn thành |
| Q-04 | Hai quest chạy đồng thời | Không chia sẻ state sai hoặc deadlock |
| WEB-01 | Build trên itch.io | Chạy từ `index.html`, WebGL + Photon kết nối |
| PERF-01 | 2/4/8/12/20 người | Báo cáo FPS, memory, RTT và trạng thái hoàn thành |

**Tiêu chí phát hành nội bộ:** P0 đạt tất cả test quan trọng tương ứng số người thực nghiệm. Mục tiêu 20 người là tiêu chí nghiên cứu hiệu năng, không được tự ghi “pass” nếu chưa có kết quả.

---

## 13. CHECKLIST REVIEW MỖI PR / CODEX TASK

- [ ] Không vượt scope/milestone; không có Chaos Meter hoặc hệ thống điểm hỗn loạn thay thế.
- [ ] Chỉ 1 DogPlayer network prefab; các dog models là visual prefabs.
- [ ] Quy tắc State Authority đúng cho dog/world/quest; không nhầm Shared với Host Mode.
- [ ] RPC có nguồn gửi hợp lệ, validate range/state/cooldown và idempotency khi cần.
- [ ] Late join hiển thị current state mà không phụ thuộc RPC quá khứ.
- [ ] Master migration và disconnect đã có đường cleanup rõ ràng.
- [ ] Không thêm full Rigidbody, NetworkObject cho debris hoặc per-frame reliable RPC.
- [ ] Scriptable config/serialized references; không hardcode asset ID lung tung.
- [ ] Không phá API, assets, scenes hoặc conventions sẵn có trong repo.
- [ ] Nêu test đã chạy, kết quả, và test **chưa thể chạy**.
- [ ] Mọi thay đổi scene/prefab có hướng dẫn setup để người dùng kiểm tra.

---

## 14. OPEN DECISIONS / ASSUMPTIONS (KHÔNG TỰ Ý THỔI PHỒNG)

1. **Unity/Fusion installed versions:** kiểm tra repo trước khi implement; không giả định sẵn Fusion 2.1 hoặc API của ví dụ online.
2. **Danh sách model chó:** cần assets thực, rig/animation mapping. Codex không thể tạo 20 model production chỉ từ architecture.
3. **Đặc điểm máy đích:** công ty có cấu hình browser/PC cụ thể chưa xác định; FPS/20 players cần load test.
4. **Phong cách nghệ thuật/map layout cuối:** người làm game sẽ quyết định khi có assets; placeholder đủ để kiểm thử.
5. **Model uniqueness trong session:** cần registry chuyển Master an toàn; nếu phức tạp, làm quy trình request/approve tuần tự và integration test trước khi polish UI.
6. **NPC, kéo co, ảnh nhóm:** P1, không chặn M1–M5.
7. **Mã phòng và Photon region:** phải thống nhất region; không coi code là cơ chế xác thực bảo mật.
8. **Bảo mật:** client Shared Mode có thể bị sửa; game nội bộ chấp nhận không có anti-cheat server. Không lưu dữ liệu nhạy cảm hay thưởng có giá trị thật.

---

## 15. TÀI LIỆU ĐỐI CHIẾU / SOURCE OF TRUTH

- **Baseline do chủ dự án cung cấp:** `NETWORK_ARCHITECTURE(1).md` (Fusion 2 Shared, itch.io, private room, 10–20 người là mục tiêu).
- [Fusion 2 — Shared Mode Master Client](https://doc.photonengine.com/fusion/v2/manual/shared-mode-master-client): scene objects, `Is Master Client` và chuyển Master.
- [Fusion 2 — Shared Mode Input](https://doc.photonengine.com/fusion/v2/manual/input/shared-mode-input): authority và `FixedUpdateNetwork`/buffer input.
- [Fusion 2 — Matchmaking](https://doc.photonengine.com/fusion/v2/manual/connection-and-matchmaking/matchmaking): `EnableClientSessionCreation`, session rules.
- [Fusion 2 — RPCs](https://doc.photonengine.com/fusion/v2/manual/data-transfer/rpcs): sự kiện, RPC target và late join.
- [Fusion 2 — Networked Properties](https://doc.photonengine.com/fusion/v2/manual/network-behaviour): state replication.
- [Fusion 2 — PlayerRef](https://doc.photonengine.com/fusion/v2/manual/playerref): player object mapping.
- [itch.io — HTML5 games](https://itch.io/docs/creators/html5): ZIP có `index.html`, iframe và giới hạn upload.

**Priority khi xảy ra mâu thuẫn:** (1) hard bounds trong tài liệu này; (2) API thực tế của installed SDK + official docs khớp version; (3) kiến trúc/code đã có trong repo; (4) lựa chọn triển khai tối thiểu và giải thích rõ. Nếu gặp mâu thuẫn không thể xử lý mà vẫn giữ bounds, **dừng phần bị ảnh hưởng, báo blocker và đề xuất lựa chọn**, không tự mở scope.

---

## 16. PROMPT NGẮN ĐỂ BẮT ĐẦU VỚI CODEX

> Hãy đọc toàn bộ `DOG_SIMULATOR_DESIGN_AND_CODEX_RULES.md`, `NETWORK_ARCHITECTURE(1).md` (nếu có trong repo), `AGENTS.md` và cấu trúc Unity project hiện tại. Trước tiên chỉ thực hiện **M0 — Repo audit**, liệt kê dependencies, những gì repo đã có, rủi ro/tương thích Photon Fusion Shared Mode và kế hoạch triển khai M1–M5 theo từng PR nhỏ. Tuân thủ tất cả **Hard Bounds**; tuyệt đối không thêm Chaos Meter, không dùng full Rigidbody cho sandbox và không chuyển Host/Dedicated Mode. Đừng tự tạo 20 model production khi chưa có assets. Sau khi có kế hoạch, bắt đầu **M1** trong một thay đổi độc lập có thể kiểm thử. Báo cáo rõ mọi test đã/chưa thể chạy.

