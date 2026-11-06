# M1.1 + M2 implementation — 2026-11-05

## Delivered

- Local-only FPS HUD with a persistent **Show FPS** setting. It samples unscaled time and frame count, updates every 0.5 seconds, reports average FPS/frame time, creates no network state and has raycasts disabled.
- Stable dog catalog with three selectable identities: `1` Reference Dog fallback, `1001` Photo Dog and `1002` Shiba Face. The two newly supplied FBX assets use their own seven animation clips through generated Animator Override Controllers.
- Pre-join and in-room model picker stored in `PlayerPrefs`. A missing/unknown model resolves visually to ID `1`; catalog validation rejects duplicate/non-positive IDs.
- Pre-join 3D dog preview driven by the same catalog/dropdown. The menu is now a responsive two-column layout: compact controls/settings on the left and the preview on the right. The same preview remains visible when the in-game pause menu opens. A dedicated camera renders the selected visual to a local 512×512 RenderTexture, auto-frames renderer bounds, plays Idle and slowly rotates the model. Preview objects use an isolated layer and have colliders/rigidbodies disabled; they never create a Fusion object.
- A small replicated reservation registry. It is a Fusion `MasterClientObject`, so State Authority follows Master handoff. Each peer owns one replicated selection-request object; the Master validates its current request ID/model and writes the durable result/reservation state. This avoids the Fusion 2.1.4 IL-weaved RPC `MethodAccessException` encountered by the previous request path. Requests remain source-bound and idempotent, reject missing or occupied IDs, and reservations are removed only from Fusion's confirmed `IPlayerLeft` callback.
- The player avatar is spawned only after reservation approval. If a join succeeds but that ModelId has just been claimed, the peer remains in the room without an avatar and can select another dog; it is not disconnected. Visual instances remain local children of the one network player prefab and do not alter the gameplay `CharacterController`/motor.
- Network build compatibility version bumped to `office-m2-catalog-reservations-v2`.
- A real Photon create-room attempt from the Editor reached `Connected`, and the actual **Create private room** UI path entered gameplay with the registry, local request object and dog player spawned.

## Automated verification

- Unity 6000.3.19f1 EditMode: **35/35 passed**, 0 failed, 0 skipped.
- Unity 6000.3.19f1 PlayMode baseline: **17/17 passed**, 0 failed, 0 skipped. A later verification run after the UI/network fix produced **16/17** because the existing `FusionAuthorityWritesMovementAndActionStateWithoutCloud` animation timing assertion sampled `0.42` instead of `> 0.8`; the new dog-preview test passed in that run. This timing-only case still needs a clean rerun.
- The added preview PlayMode test loads `M1_FusionPreview`, verifies its RenderTexture is created, and switches successfully to model IDs `1001` and `1002`.
- Result XML: `Temp/M2EditModeResults.xml` and `Temp/M2PlayModeResults.xml` for this local run. These are local test artifacts, not release evidence.

## Performance measurement record

The FPS instrumentation is implemented, but this change does **not** claim that the previously reported stutter is fixed. A reproducible Web measurement still requires the tester's actual browser/machine and at least two processes for remote-avatar observations.

Fill one row per scenario/build; do not merge client counts:

| Build/URL | Machine/GPU | Browser/version | Resolution | Clients | Focus | Scenario | FPS / ms | Local camera/avatar | Remote avatar | RTT/GC/CPU/GPU notes |
| --- | --- | --- | --- | ---: | --- | --- | --- | --- | --- | --- |
| pending | pending | pending | pending | 1 | pending | idle / move / rotate / jump / action | pending | pending | n/a | pending |
| pending | pending | pending | pending | 2 | pending | idle / move / rotate / jump / action | pending | pending | pending | pending |

Repeat separately for 4/8/12/20 clients only when those loads are actually run. Compare local preview and Shared at the same resolution/visual. Record regressions and unchanged results as well as improvements.

## Manual network/browser acceptance still required

- Toggle before join, while paused in-room, after leave and after browser reload; verify no click/raycast/input/pointer-lock regression.
- Two clients choose the same ID concurrently: exactly one is approved; the other remains unspawned during initial selection or keeps its previous model during an in-room change.
- Late join sees nickname, reserved ModelId and matching visual; refresh/disconnect releases the reservation after Photon confirms departure.
- Master leaves while other peers remain: the registry persists, no reservation duplicates, and the new Master accepts subsequent requests.
- Verify Photo Dog and Shiba Face Idle/Run/Jump/Bark/Poop/Pee/Bite in a real Web build and re-run the performance table with the selected visual.

