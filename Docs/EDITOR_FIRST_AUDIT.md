# Editor-first Unity audit

The migration owns authored scenes and prefabs under Assets/Game, Assets/Art and Assets/Scenes. The source validator checks project-owned scripts included in player compilation. Photon Fusion, package code, the SimplePoly City source pack and TextMesh Pro remain outside the migration. Test fixtures and Editor authoring tools are separate from gameplay.

## Static composition moved to the Editor

| File or class | Prior method and operation | Authored dependency or composition |
| --- | --- | --- |
| DogMotor, DogAnimationDriver, LocalMovementPreview, DogBackSupport | Awake and support handling used same-object, child and owner lookups | CharacterController, collider collection, motor, input, animation driver and support owner |
| DogFaceAppearance, DogFaceEditorController | Awake repaired the presenter reference and picker actions reached a scene singleton | Presenter on the avatar and picker reference on each face editor |
| DogVisualPresenter, DogModelCatalog, DogSelectionPreview | ApplyModel, InstallModelVisual, InstantiateVisual, ShowModel and FrameModel searched model internals and added Animator if missing | Catalog entries point to DogModelVisual on each model prefab. That component holds its Animator, optional face and pelvis, renderers, physics and demo behaviours. The fallback visual uses the avatar's authored references. |
| ReferenceDogAnimationPreview | OnEnable searched for Animator when the reference was empty | Preview rig uses its authored Animator. |
| NetworkDogPlayer and NetworkDogPlayer.Wanted | Spawned, seat and movement methods resolved input, motor, controller, animation, support, mouth, colliders and NetworkTransform. LateUpdate constructed a ParticleSystem and searched the selected rig's pelvis. | DogPlayer prefab owns the references and the stopped Pee Stream. The active model provides its pelvis. |
| NetworkCityVehicle | Spawned and pose updates looked up Rigidbody, BoxCollider, NavMeshObstacle and NetworkTransform | Vehicle prefabs and variants hold the component references. |
| NetworkCityPedestrian and NetworkCityPedestrian.Police | Spawned composed a pose hierarchy, Rigidbody and prone collider. PresentPolice and PlaySiren added a status marker and AudioSource. | CityPedestrian prefab owns the hierarchy, physics, agent, sync, marker and source. Police activation stays tied to replicated state. |
| NetworkCarryable | Awake, PresentGoldenBone, PresentJailBowl and reset resolved or constructed colliders, renderers and sync | All carryable prefabs hold their physics and presentation references. CarryableCrate owns a disabled bowl visual. The jail state activates it and changes the crate collider size. |
| SimplePolyCityWorld, GoldenBoneCourse, DogJail, BouncePad and SandboxInteractionRegistry.Jail | Awake constructed the course. PresentJail built its shell, colliders, NavMeshObstacle and signs. Course presentation looked up gate components. | City scene contains both layouts and their assets, including the bounce pad's serialized landing setting. The same authority-side site selection positions and activates the authored jail. Despawn disables it for the next session. |
| FusionSessionService and FusionSessionService.Rooms | CreateRunner, ConnectAsync and ChangeBrowsingAsync created runner roots, events, scene manager and face service | Authored GameRunner and LobbyRunner prefabs. Scene sessions hold prefab references and the player controller template. |
| FusionSessionService, SandboxInteractionRegistry, NetworkDogPlayer and CustomDogFaceNetworkService | Registry discovery scanned the scene and a face RPC looked up the service on its runner | Spawn notification and face delivery are explicit events filtered by runner, with subscriptions tied to session/service lifetime. |
| FusionPreviewUI, LocalPreviewUI, DogFrontEnd and its Layout, Gameplay and HubSettings partials, DogMenuSkin, BrushColorPicker | Awake and Start created the menu component, pages, HUD, face tools and controls. The carousel constructed a portrait on first use. | Two Fusion scenes contain the complete menu, fixed controls and persistent button bindings. Runtime changes page state, room rows, portraits and brush textures through authored references. Preview labels use direct references. |

Collision and raycast recipients, carried objects, vehicles, host-spawned and replicated avatars, and objects resolved by NetworkId are dynamic references. Their component checks identify the object received at runtime. Spawn callbacks retain their existing access to the new instance before Fusion initializes it. The billboard camera follows Unity's active MainCamera selection. Input maps, keyboard devices and texture content follow their existing lifecycles.

## Runtime creation allowlist

| Call sites | Owner, timing and cleanup | Reason and resource tradeoff |
| --- | --- | --- |
| FusionSessionService.CreateRunner and FusionSessionService.Rooms.ChangeBrowsingAsync | Session service creates one game runner per connection attempt and one lobby runner per browse lifetime. It shuts down the preceding runner during a retry or transition. | Fusion runners have a single startup/shutdown lifecycle. Prefabs keep composition in the Editor. There is no reusable runner pool. |
| FusionSessionService.FindOrCreateSandboxRegistryAsync, SpawnLocalAvatar and OnPlayerJoined | Fusion instantiates the sandbox registry and avatars after session startup or a player join. Runner authority, player ownership and shutdown control their lifetime. | Network identities and participants are available only at runtime. Uses Fusion's default prefab provider with a serialized provider component. |
| SandboxInteractionRegistry.SpawnContent, SpawnCity, SpawnMission, SpawnPoopIfDue and SandboxInteractionRegistry.Jail.SpawnJail | State authority creates sandbox props, vehicles, pedestrians, mission actors, jail bowl, police and temporary waste. Fusion owns instances. The registry uses existing reset flows and despawns temporary waste. | World saves, session participants, dynamic incidents and replicated identity determine the active instances. Existing prefab lifecycle and reuse are preserved. |
| DogVisualPresenter.InstantiateVisual, reached by InstallModelVisual and DogSelectionPreview.ShowModel | Presenter and preview each own one selected visual. They replace it when the catalog selection changes. Parent destruction cleans up the current visual. | Model identity is selected at runtime and replicated per avatar. One rig per active selection avoids retaining every catalog rig per avatar. This is a documented exception candidate for explicit project approval. |
| DogInputReader.Awake | Each reader clones its InputActionAsset and destroys it with the reader. | This clones action state, a ScriptableObject. It creates no GameObject or Component. |

Face JPEG decoding, brush textures, rendering resources, per-dog face materials and the generated siren clip allocate Unity data objects. Managed collections, structs and the NavMesh path buffer are outside GameObject/Component construction.

## Editor migration and validation

Use **Tools → Dog Simulator → Migrate Editor-Authored Dependencies** after regenerating a scene, changing the catalog prefabs or adding an affected prefab variant. The migrator visits base and nested prefab dependencies first, loads prefab contents in isolation, and resolves scene references in the scene that owns them. A field with multiple semantic candidates stops migration with context. Save dirty scenes before running it.

Use **Tools → Dog Simulator → Validate Editor-Authored Dependencies** to check project scene and prefab hierarchies, the authored fields, model bindings, menu callbacks and known creation sites. Call-site checks use a source scan and counted exceptions. They cannot prove arbitrary indirect calls, conditional compilation expressions or external package behavior. Physics queries and typed lookups on runtime recipients still require architectural review. Live host/client and browser behavior require platform verification.


## Migrated assets and verification

- Scenes — M1_LocalPreview, M1_FusionPreview and SimplePolyCity_Gameplay.
- Prefabs — DogPlayer, CarryableBall, CarryableBone, CarryableCrate, CarryablePoop, CityPedestrian, DriveableTaxi, the 21 Vehicle_* prefabs, Natures_Pot Bush_big, Natures_Pot Bush_small, Props_Bench_1, Props_Coffee shop chair, Props_Dustbin, Props_Traffic Control Barrier Fence and Props_Traffic cone. The catalog's TanMuzzleDog and ChihuahuaF0194 prefabs were wired separately. GameRunner and LobbyRunner were created as prefab assets.
- Shared assets — DogModelCatalog, six course materials, three jail materials, and five menu sprites plus the Sniglet font asset.
- Tooling — EditorFirstMigration.cs and its Menu, Models, Validation and World partials. EditorAccess.cs in each runtime assembly exposes internal authoring contracts to DogSimulator.Editor. DogModelVisual, FusionRunnerRoot, DogMenuButton and DogMenuSliderValue are separate components.
- Test fixtures — DogMotorTests now supplies the controller before activation, FusionSetupTests treats the city reference as optional in the yard, and FusionPreflightTests uses the authored runner and exercises the persistent menu events.

Unity 6000.3.19f1 compiled the project and saved the migration. A repeat run saved zero scenes or prefabs. The validator checked the saved assets and the source call sites.

EditMode passed 45 of 49 tests. The four failures assert older catalog entries and fallback IDs. The catalog already contained 1104 and 1105 with 1104 as fallback when the migration began. The migration added visual bindings to those entries.

PlayMode with Direct3D 11 passed 22 of 27 tests, including the authored menu, runner retry and cleanup, sandbox save/reopen, Golden Bone reset scopes and the jail escape flow. The remaining failures were:

| Test | Observed failure |
| --- | --- |
| FusionAuthorityWritesMovementAndActionStateWithoutCloud | Animation speed stayed at zero. The batch run did not establish whether foreground input is responsible. |
| GoldenBoneGateAndResetScopesInFusionSingle | The gate closed, then the first bone pickup was rejected. |
| OfflineCitySpawnsItsWorldAndTaxiUsesNormalDrivingInput | The high-speed hit did not knock the dog down. |
| PedestrianPatrolsFlinchesAtThrowAndRetaliatesAfterBite | The bite did not set the NPC's aggro target. |
| VehicleHitLetsDogCarryAndThrowNpcBeforeRecovery | The carry request did not claim the prone NPC. |

A matched pre-migration PlayMode run was unavailable. Gameplay equivalence for the five failed cases remains unverified. There was no live Host/Client, player build or WebGL browser run. The project has no owned runtime GameObject pool to exercise.
