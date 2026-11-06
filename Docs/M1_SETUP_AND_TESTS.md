# M1 partial slice — local movement preview

**Current update 2026-11-05:** the user has tested Fusion and built Web, accepting the M1 functional baseline with stutter carried forward. See [M1 acceptance](M1_ACCEPTANCE_2026-11-05.md) and [M1.1/M2 next steps](NEXT_STEPS_M1_1_M2.md). The setup blockers and test limitations below describe the earlier offline/SDK handoffs.

**Update 2026-11-04:** the user-supplied Fusion 2.1.4 SDK is now installed and a separate [Fusion preview](M1_FUSION_SETUP_AND_TESTS.md) has been added. This document describes the preserved offline scene and its earlier verification. The SDK installation instructions below are retained as setup history; the remaining external requirement is the Fusion App ID.

This is an **offline development preview**, not the multiplayer MVP. No runner, network prefab, replicated state, room, RPC, model reservation or fake network transport is created. The local driver is a small test harness for the motor/input/camera that the future Fusion adapter can reuse.

## Open and run

1. Open this project with **Unity 6000.3.19f1**. Do not open it with the separately installed Unity 2022 Editor.
2. Run **Tools → Dog Simulator → Create or Open M1 Local Preview**. The setup uses the current URP assets and existing Input System actions; it imports TMP Essential Resources from the already-installed uGUI package non-interactively if absent. No package versions are changed.
3. Open `Assets/Game/Scenes/M1_LocalPreview.unity` and press Play. Enter a nickname, then **Play local preview**.
4. WASD moves relative to camera yaw; Shift runs; Space jumps; mouse rotates the camera. Esc, lost focus or lost pointer lock returns to the menu and clears buffered input. **Reset local position** restores the spawn pose.
5. Use **Check code format** to validate a 6–8 character invite code. This checks syntax only; it does not look up or create a room. Create/Join remain visibly unavailable pending the real SDK integration.

The setup creates the scene, four simple URP materials, movement/network config assets and one **visual-only** dog placeholder prefab. Existing generated assets are reused. Re-running the menu command opens an existing preview without overwriting edits. `SampleScene` and the current build scene list are preserved. The preview is not added to release build settings.

The dog visual is made from cubes and has no animation, Rigidbody, or NetworkObject. The arena is a movement test area, not the Office/Pantry/Courtyard content milestone. Supplied production art, rigs, animation/audio and final map work remain outstanding.

## Components and configuration

- `DogInputReader`: clones `InputSystem_Actions`, enables only the PC bindings of the Player map while playing, captures in Update, buffers and consumes jump once, discards input on disable/focus loss.
- `DogMotor`: single CharacterController motor; no autonomous update loop. `Simulate` is called by the local harness now and only by State Authority in a future Fusion driver. Walking/running, normalized diagonal input, grounded jump, ceiling response, bounded fall speed and fall recovery.
- `DogCamera`: third-person yaw/pitch and sphere-cast obstruction avoidance. Query mask excludes layer 2, which is used by the normalized preview avatar collider. That collider excludes the same layer, anticipating avatars that overlap without pushing one another.
- `LocalMovementPreview`: explicit offline FixedUpdate driver; **replace this component** with the Fusion driver when constructing the single network player prefab. Do not run both drivers on a network avatar.
- `LocalPreviewUI`: uGUI/TMP menu and HUD, local nickname preference, no interpretation of nickname rich text. No Photon App ID is stored here.
- `DogMovementConfig`: movement, gravity, input buffer, camera tuning and fall bound in the Inspector.
- `NetworkConfig`: planned consistent region (`asia`), version namespace, unverified dev capacity 8, bounded create retries and timeout. These are settings prepared for the real adapter, not evidence they are enforced by a network service yet.

## Verification commands

From PowerShell in the project root, with no other Editor using this project:

```powershell
Start-Process -FilePath 'D:\Unity Hub\Editors\6000.3.19f1\Editor\Unity.exe' -WindowStyle Hidden -Wait -ArgumentList @('-batchmode', '-nographics', '-projectPath', 'D:\IT\Game\RiseOnDogSimulator', '-logFile', 'D:\IT\Game\RiseOnDogSimulator\Logs\M1-setup.log', '-executeMethod', 'DogSimulator.Editor.LocalPreviewSetup.CreateBatch')
unity test . --editor-path 'D:\Unity Hub\Editors\6000.3.19f1\Editor\Unity.exe' --mode EditMode --filter DogSimulator.Tests --output TestResults\M1-EditMode.xml --timeout 240 -- -nographics
unity test . --editor-path 'D:\Unity Hub\Editors\6000.3.19f1\Editor\Unity.exe' --mode PlayMode --filter DogSimulator.Tests --output TestResults\M1-PlayMode.xml --timeout 240 -- -nographics
```

The first command compiles scripts and serializes the scene using Unity APIs. It deliberately omits `-quit`: the batch entry point waits for the bundled TMP import and exits itself, with a 120-second setup timeout. Unity CLI's `run` command adds `-quit`, so use the direct installed Editor command for first-time setup. Later runs can use `unity run` with `LocalPreviewSetup.Create` when TMP is already imported.

Tests cover guest payload validation (including Unicode/control characters), room code grammar, scene references, one local driver/camera/EventSystem, no Rigidbody, normalized movement speed, camera-relative direction, grounded/airborne jumps, fall recovery, actual PC input bindings, one-shot/expired/cleared jump buffering and menu validation/start/reset. Test outcomes are recorded below only after execution.

Manual checks still needed: UI at different browser aspect ratios, pointer lock/focus changes, mouse camera, walls/platform collisions and menu/reset. No manual checklist item is automatically marked passed by compilation.

## Unblock real multiplayer

### Bước tiếp theo — cài Photon (cập nhật 04/11/2026)

Đã kiểm tra lại: dự án chưa có Fusion SDK; người dùng xác nhận chưa có SDK và App ID. M1 multiplayer vẫn chờ hai đầu vào này. Thực hiện trong đúng project `D:\IT\Game\RiseOnDogSimulator`:

1. Đăng nhập hoặc đăng ký tài khoản tại [Photon Dashboard](https://dashboard.photonengine.com/).
2. Mở [SDK & Download chính thức](https://doc.photonengine.com/fusion/v2/getting-started/sdk-download), tải **Fusion SDK 2.1.4 Stable Build 2500** — bản stable được niêm yết khi kiểm tra ngày 04/11/2026. Trang liệt kê hỗ trợ Unity 6.3 và WebGL. Đây là phiên bản đề xuất để cài mới, **chưa phải SDK đã cài/kiểm chứng trong repo**.
3. Trong Unity **6000.3.19f1**, dùng **Assets → Import Package → Custom Package…**, chọn file `.unitypackage` vừa tải và import SDK. Chờ Unity hoàn tất import/compile; ghi lại phiên bản thực tế và xử lý lỗi Console trước khi triển khai adapter.
4. Trong Dashboard, vào **YOUR → APPS → Development → CREATE A NEW APP**. Chọn SDK **Fusion**, phiên bản **Fusion 2**, đặt tên `Office Dogs`, rồi tạo ứng dụng. Xem [hướng dẫn tạo App ID](https://doc.photonengine.com/fusion/v2/getting-started/appid-instructions).
5. Sao chép App ID vào **Fusion Hub**, hoặc chọn asset `PhotonAppSettings` và điền **AppSettings → App Id Fusion**. Không cần gửi mật khẩu/tài khoản Photon vào chat.
6. Kiểm tra **Project Settings → Editor → Asset Serialization → Mode = Force Text**, theo yêu cầu SDK. Giữ Unity, URP và Input System hiện có.
7. Báo đã import và cấu hình xong; nếu chỉ mới tải SDK, cung cấp đường dẫn file trên máy để tiếp tục import. Bước triển khai tiếp theo là đọc API SDK thực tế, rồi làm Create/Join/Leave Shared Mode và một network player prefab dùng lại motor/input/camera hiện có.

Chỉ cài SDK/App ID chưa làm các nút Create/Join hoạt động: scene hiện tại vẫn là local preview cho đến khi adapter và network prefab được triển khai. WebGL Build Support vẫn thiếu nhưng không chặn việc compile/tích hợp SDK trong Editor; cài module đúng phiên bản ở M5 trước khi kiểm thử browser.

### Integration gate after installation

1. Sign in to Photon and obtain the [Fusion 2 SDK](https://doc.photonengine.com/fusion/v2/getting-started/sdk-download). Import the actual `.unitypackage` into this project; record its installed version before writing the adapter. The official unauthenticated package link returned 403/sign-in during this task.
2. Create a Fusion application in the [Photon dashboard](https://dashboard.photonengine.com/) and set its App ID in the SDK settings. No account or App ID was created by this task.
3. Implement and compile against that installed API: Shared-only runner/session lifecycle, collision-safe create, strict `EnableClientSessionCreation = false` joins, consistent region/version isolation, one normalized player prefab with `NetworkTransform`, State Authority movement/identity and cleanup. The document's model uniqueness requirement must be addressed before claiming the M1 journey complete, coordinating the minimal M2 reservation work as needed.
4. Run two clients on different networks, test wrong/full room, simultaneous create/model selection, leave, refresh, disconnect and retry. M1 stays incomplete until these tests pass.
5. Add **WebGL Build Support** for the same Editor via Unity Hub before M5. It is currently missing; no browser build/load-test result is asserted.

## Execution results

Executed on **2026-11-04**, using the installed **Unity 6000.3.19f1** Editor:

| Check | Result | Evidence |
| --- | --- | --- |
| Compile and generate preview with actual Unity serialization | PASS | `Logs/M1-setup.log`, generated scene/config/material/prefab assets |
| EditMode suite | **22/22 PASS**, 0 failures | `TestResults/M1-EditMode.xml` |
| PlayMode suite, final complete run | **8/8 PASS**, 0 failures | `TestResults/M1-PlayMode.xml` |
| Input tests, focused diagnostic run | **3/3 PASS** (included in the final 8 above) | `TestResults/M1-Input.xml` |
| Supplied document copies | SHA-256 matches both source files | Local file hash comparison |

The PlayMode run advances real Unity frames and tests motor physics, keyboard bindings, buffering and actual scene menu callbacks. The headless input tests temporarily override focus routing and restore both settings after each test; project input settings are unchanged. Initial failures identified missing serialized config references during scene creation; the setup now loads config after creating the scene and refuses null references. The final saved scene passed the reference tests. An invalid Unicode test constructs unpaired surrogates at runtime to avoid NUnit argument encoding replacing them.

These are **headless Editor results**, not a visual review or browser test. Pointer lock, mouse feel, obstruction appearance and responsive UI still need an interactive visual pass. Multiplayer/late join/Master handoff, two-network connectivity, WebGL build/itch.io and 2–20 client load tests are **NOT RUN**. M1 multiplayer is **incomplete** pending the real Fusion SDK/App ID and its acceptance tests. No capacity/FPS claim is made.

The first sandboxed Editor attempt failed licensing initialization; the authorized local-user Editor run succeeded. Logs retain a LicensingClient validation warning, while the final test processes exit 0. No licensing configuration, Unity version, package version or render pipeline was changed.
