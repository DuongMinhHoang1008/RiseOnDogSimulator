# Implementation status — 2026-11-06

## Current acceptance and next work

**Update 06/11/2026:** [M1.1–M2 acceptance review](M1_1_M2_ACCEPTANCE_2026-11-06.md) records delivered work separately from unverified fixes. The final two-model animation patch still needs runtime confirmation; the failed PlayMode timing assertion is not waived. The historical M2 XML files referenced in the implementation note are no longer present in `Temp/` at this review. No new tests or builds were run for this documentation update.

The user now prioritizes gameplay: carrying props of any size, balls/bones, dog stacking, a compact expanded map, retaliating NPCs, downed ragdolls and eventually drivable vehicles. [The new gameplay proposal](NEXT_STEPS_GAMEPLAY_M3.md) evaluates all seven ideas and proposes G0 → M3-A carry/props/blockout → M3-B stacking → M3-C NPC → M3-D ragdoll → M4-A art/content → M4-B vehicle experiment. This is a design proposal, not an implementation claim; explicit scope differences from the original hard bounds are listed there. Configured capacity is 8, but the current exclusive three-model roster permits only three distinct spawned dog identities.

**M1 functional baseline accepted by the user on 2026-11-05**, after testing Fusion and a Web build. The user reports that the rest works well, with some stutter still present, and proposes an FPS display toggle in settings. This is user-reported runtime acceptance; no new Unity tests or Web build were run for this documentation update. See [M1 acceptance](M1_ACCEPTANCE_2026-11-05.md).

M1.1 instrumentation and the M2 catalog/reservation implementation are now present in the project. The selectable roster is explicitly three models: the Reference Dog fallback plus the supplied Photo Dog and Shiba Face assets. The responsive menu keeps compact controls/settings on the left and a catalog-driven 3D preview on the right, both before joining and while paused. The Fusion request path no longer uses the IL-weaved RPC that raised `MethodAccessException`; a real Photon create-room attempt and the actual UI create flow both succeeded in the Editor. A peer whose initial model loses a reservation race remains in-room and can choose again. Verification details, including **35/35 EditMode PASS**, the **17/17 PlayMode baseline**, and one later timing-only PlayMode retry failure, are recorded in [the implementation note](M1_1_M2_IMPLEMENTATION_2026-11-05.md); real multi-client browser acceptance and measured stutter data remain required before the milestone is accepted. See [next steps](NEXT_STEPS_M1_1_M2.md).

Existing rig-integration reports record **32/32 EditMode + 16/16 PlayMode PASS**; `PrivateRoom-PlayMode.xml` records **10/10 PASS**, including runtime runner/shutdown/leave/retry checks. Suites overlap and are not summed. Cloud/browser acceptance comes from the user's report, not these tests. Detailed lifecycle/concurrency/late-join/Master-handoff results, test client count, machine/browser/build identifiers and performance measurements have not been recorded individually. No capacity/FPS claim is made; M5 load-testing acceptance is still pending.

## M0 — Repository audit snapshot (2026-11-04)

- Project: Unity **6000.3.19f1** (revision `7689f4515d75`). Matching Editor exists at `D:/Unity Hub/Editors/6000.3.19f1/Editor/Unity.exe`.
- Rendering: URP **17.3.0**; keep the current rendering assets and pipeline.
- Input: Input System **1.19.0**, active input handler = Input System. Reuse `Assets/InputSystem_Actions.inputactions` (Player Move/Look/Sprint/Jump); no replacement input package.
- Other direct packages: AI Navigation 2.0.13, Collab Proxy 2.12.4, Rider 3.0.40, Visual Studio 2.0.26, Multiplayer Center 1.0.1, Test Framework 1.6.0, Timeline 1.8.12, uGUI 2.0.0, Visual Scripting 1.9.11; built-in modules remain unchanged. Full dependency graph: `Packages/packages-lock.json`.
- Existing content: `Assets/Scenes/SampleScene.unity` (camera, directional light, volume), template Readme scripts and URP settings. No game scripts, game assembly definitions, dog models, animation controllers, player prefabs, or networking assets found.
- No applicable `AGENTS.md` found in the project or its inspected parent directories.
- Photon Fusion at M0: not installed. Subsequently imported the user-supplied **Fusion 2.1.4 Stable Build 2500** package on 2026-11-04. App ID was unconfigured at that handoff; the user has since tested Fusion/Web successfully.
- WebGL module: missing at M0; now present in the matching Editor's `PlaybackEngines/WebGLSupport` directory when checked on 2026-11-05. The user reports a completed Web build/test; no build hash/URL or measured load-test report was provided here.
- Git at M0: initial project content was untracked. Preserve existing staged/unstaged work; do not reset, overwrite SampleScene, or commit unrelated files.

## Compatibility and blockers

The SDK and Shared session/avatar implementation are installed. The earlier SDK/App ID/Web setup blocker is superseded by the user's Fusion/Web test report on **2026-11-05**. FPS instrumentation and M2 selection/reservations are implemented; open verification includes stutter measurements, the final animation patch and multi-client lifecycle cases. [M1 Fusion setup](M1_FUSION_SETUP_AND_TESTS.md) retains the earlier verification snapshot; [M1 acceptance](M1_ACCEPTANCE_2026-11-05.md) records that decision and its scope.

The selected path remains **Fusion 2 Shared Mode**, owner-authoritative avatars and Master-authoritative world/quest state. No Host/Dedicated alternative or simulated networking is introduced.

The initial unauthenticated SDK download attempt returned a sign-in/403 page. The user later supplied `Photon-Fusion-2.1.4-Stable-2500.unitypackage`, which was imported through the running Unity Editor. Its installed `Assets/Photon/Fusion/build_info.txt` confirms the version. The adapter uses that SDK's `FusionAppSettings.GetCopy`, `StartGameArgs`, `NetworkEvents`, `NetworkTransform` and network-property weaving APIs. No substitute Fusion types or simulated transport were introduced.

- [Official SDK download](https://doc.photonengine.com/fusion/v2/getting-started/sdk-download)
- [Create a Fusion App ID](https://doc.photonengine.com/fusion/v2/getting-started/appid-instructions)
- [Shared Mode matchmaking](https://doc.photonengine.com/fusion/v2/manual/connection-and-matchmaking/matchmaking)

Tooling history from the 2026-11-04 SDK slice: CLI 1.0.0-beta.13 reported a beta.16 update; update check failed DNS resolution and no CLI/Editor upgrade was performed in that slice. No Pipeline-enabled Editor was used then. Native app control imported the SDK in the running Editor. Fusion's installer added scripting symbols, enabled run-in-background and requested a direct Mono.Cecil dependency; Unity Package Manager API retained the already-resolved **1.11.6**, with matching manifest/lock, instead of the SDK's 1.10.2 request. See `Logs/Fusion-UPM-setup.log`. This historical tooling snapshot is not a fresh inventory of later art/animation work.

## Milestone plan and boundaries

The table below retains the original milestone plan. For the proposed gameplay-focused sequence dated 06/11, use [NEXT_STEPS_GAMEPLAY_M3.md](NEXT_STEPS_GAMEPLAY_M3.md); NPC HP/ragdoll/vehicle experiments explicitly extend the old content bounds and are not silently included in the original M3 gate.

| Milestone | Small reviewable change | Acceptance gate |
| --- | --- | --- |
| M0 | This audit, unmodified reference copies, setup/test notes | Versions, missing dependencies and implementation path identified |
| M1, accepted functional baseline | Shared session lifecycle and avatar; local preview preserved; rigged model/animation integrated | User accepted Fusion/Web functionality on 2026-11-05, with stutter carried forward; detailed regression results remain to be logged |
| M1.1, next | Local FPS display toggle in settings; baseline measurements and targeted stutter investigation | Toggle persistence/UI/input checks in Web; reproducible measurements and before/after findings |
| M2 | Stable-ID visual catalog, real supplied dog assets/fallback, Master-owned replicated reservations and handoff | Concurrent model selection and late join; missing visual safe |
| M3 | Hit/bark plus knockable/toggle/carry vertical slice; validated RPC requests | Contention, idempotency, late join, Master handoff, disconnect cleanup |
| M4 | One Office/Pantry/Courtyard map and three P0 quests; replicated timers/checklists/reset | Cooperative completion/reset and safe handoff/late join |
| M5 | WebGL module/build path now available; release packaging, browser compatibility and measured load testing remain | Measured 2/4/8/12/20 client results; publish only a proven capacity |

M2–M5 are not bundled into the first code change. P1 content stays deferred until M5. No production dog art is generated or claimed.

## Delivered files and current verification

### Fusion slice, SDK handoff snapshot (2026-11-04)

- `Assets/Photon/`: actual user-supplied Fusion 2.1.4 SDK, global SDK settings/config and prefab table. App ID was empty at this handoff. Package SHA-256 and setup steps are recorded in `Docs/M1_FUSION_SETUP_AND_TESTS.md`.
- `Assets/Game/Scripts/Fusion/`: separate Fusion runtime assembly, Shared session options/lifecycle, State Authority avatar driver and uGUI flow. Reuses the existing motor/input; `DogCamera` now supports binding/unbinding the current local network avatar.
- `Assets/Game/Editor/FusionPreviewSetup.cs`, `Scenes/M1_FusionPreview.unity`, `Prefabs/DogPlayer.prefab`, `Config/FusionNetworkConfig.asset`: serialized by the actual Editor, registered with the SDK prefab table/weaver. No offline driver on the network prefab; no avatar exists in the scene before connection.
- New SDK-policy/prefab/preflight tests plus all existing tests: **30/30 EditMode + 9/9 PlayMode PASS**, no failures/skips, on Unity 6000.3.19f1. Evidence: `TestResults/Fusion-EditMode.xml`, `TestResults/Fusion-PlayMode.xml` and matching logs. The sandboxed test launch was stopped after licensing initialization stalled; final local-user runs exited 0.
- At that handoff, the Fusion menu was inspected in Full HD Play mode and Create with an empty App ID produced the expected setup message. Cloud/browser checks had not run and M1 was incomplete then. Later rig/animation work replaced the shared placeholder visual; the user's 2026-11-05 functional acceptance is recorded above. Model selection and unique reservations remain M2 work; detailed network regression cases are not individually marked passed.

### Preserved local preview, earlier slice

- `Assets/Game/Scripts/Networking/`: invite-code generation/normalization and serialized network settings for the future adapter. No session implementation without the SDK.
- `Assets/Game/Scripts/Player/`: guest-name validation, movement config, buffered existing Input System actions, CharacterController motor, third-person camera and explicit offline driver.
- `Assets/Game/Scripts/UI/LocalPreviewUI.cs`: local-preview menu/HUD; Create/Join unavailable pending Photon setup.
- `Assets/Game/Editor/LocalPreviewSetup.cs`: Unity API-based setup, asynchronous bundled TMP resource import, no manual scene YAML authoring, reuse existing generated content.
- `Assets/Game/Scenes/M1_LocalPreview.unity`, config/material assets and `Prefabs/Dogs/DogPlaceholder.prefab`: generated local test scene and a visual-only cube dog. No production model or network prefab claimed.
- `Assets/Game/Tests/`: EditMode and PlayMode tests; separate runtime/editor/test assemblies.
- `Assets/TextMesh Pro/`: Essential Resources imported from the already-installed uGUI package, not a package upgrade.
- `README.md`, this status and `Docs/M1_SETUP_AND_TESTS.md`: startup, blockers, file responsibilities, test instructions and staged plan.

Setup and compilation succeeded using the actual Unity 6000.3.19f1 Editor. Sandboxed execution could not initialize licensing; the authorized local-user run succeeded. The remaining LicensingClient validation warning did not prevent compilation or test execution. Official source copies were checked with SHA-256 and match both supplied files. See `Docs/M1_SETUP_AND_TESTS.md` for the executed test results and unverified manual/network/browser checks.

## Source documents

`Docs/Specifications/DOG_SIMULATOR_DESIGN_AND_CODEX_RULES.md` and `Docs/Specifications/NETWORK_ARCHITECTURE.md` are unmodified copies of the supplied files. The gameplay specification supplies authority, carry, quest, and model uniqueness decisions beyond the network-only baseline. The referenced filename `NETWORK_ARCHITECTURE(1).md` was not provided; the supplied `NETWORK_ARCHITECTURE.md` is used without renaming or modifying its content.
