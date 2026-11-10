using System;
using System.Threading;
using System.Threading.Tasks;
using DogSimulator.Player;
using DogSimulator.CustomDogFace;
using Fusion;
using Fusion.Photon.Realtime;
using UnityEngine;

namespace DogSimulator.Networking
{
    public enum SessionState { Disconnected, Connecting, Selecting, Connected, Disconnecting, Failed }

    public sealed partial class FusionSessionService : MonoBehaviour
    {
        private const float MinimumSpawnSeparation = 2.75f;
        private const float FirstSpawnRingRadius = 4f;
        private const float SpawnRingSpacing = 3.5f;
        [SerializeField] private NetworkConfig config;
        [SerializeField] private NetworkObject playerPrefab;
        [SerializeField] private CharacterController playerControllerPrefab;
        [SerializeField] private GameObject sandboxRegistryPrefab;
        [SerializeField] private DogModelCatalog modelCatalog;
        [SerializeField] private DogCamera followCamera;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private SimplePolyCityWorld city;
        [SerializeField] private FusionRunnerRoot gameRunnerPrefab;
        private NetworkRunner runner;
        private NetworkDogPlayer localPlayer;
        private SandboxInteractionRegistry sandbox;
        internal SandboxInteractionRegistry Sandbox => sandbox;
        private PlayerRef roomOwner;
        private CancellationTokenSource cancellation;
        private Task connecting;
        private Task leaving;
        private float deadline;
        private bool timedOut;
        private bool destroyed;
        private string pendingNickname;

        public SessionState State { get; private set; }
        public string Message { get; private set; } = "Create a Host room, join as Client, or play the same world offline.";
        public string RoomCode { get; private set; } = string.Empty;
        public string Region => config != null ? config.Region : string.Empty;
        public string GameVersion => config != null ? config.GameVersion : string.Empty;
        public int PlayerCount => runner != null && runner.IsRunning ? runner.SessionInfo.PlayerCount : 0;
        public int Capacity => config != null ? config.MaxPlayers : 0;
        public int PingMilliseconds => runner != null && runner.IsRunning
            ? Mathf.Max(0, Mathf.RoundToInt((float)(runner.GetPlayerRtt(runner.LocalPlayer) * 1000.0))) : 0;
        public int SimulationTickRate => runner != null && runner.IsRunning && runner.DeltaTime > 0
            ? Mathf.RoundToInt(1f / runner.DeltaTime) : 0;
        public bool IsConnected => State == SessionState.Selecting || State == SessionState.Connected;
        public bool IsSinglePlayer => runner != null && runner.GameMode == GameMode.Single;
        public bool HasAvatar => State == SessionState.Connected && localPlayer != null;
        public DogModelCatalog ModelCatalog => modelCatalog;
        public int SelectedModelId { get; private set; }
        public event Action Changed;
        public NetworkDogPlayer LocalPlayer => localPlayer;


        private void Awake() => SandboxInteractionRegistry.RegistrySpawned += OnSandboxSpawned;

        private void OnSandboxSpawned(SandboxInteractionRegistry registry)
        {
            if (registry.Runner == runner) sandbox = registry;
        }

        public Task CreateAsync(string nickname, int modelId, int worldSlot = 1) => Begin(nickname, modelId, null, true, false, worldSlot);
        public Task JoinAsync(string nickname, int modelId, string code) => Begin(nickname, modelId, code, false, false, 0);
        public Task StartSinglePlayerAsync(string nickname, int modelId, int worldSlot = 1) => Begin(nickname, modelId, null, false, true, worldSlot);
        public Task CreateAsync(string nickname) => CreateAsync(nickname, modelCatalog != null ? modelCatalog.FallbackModelId : 1);
        public Task JoinAsync(string nickname, string code) => JoinAsync(nickname, modelCatalog != null ? modelCatalog.FallbackModelId : 1, code);

        private Task Begin(string nickname, int modelId, string code, bool create, bool singlePlayer, int worldSlot)
        {
            if (destroyed || IsConnected || State == SessionState.Disconnecting
                || (connecting != null && !connecting.IsCompleted)
                || (leaving != null && !leaving.IsCompleted)) return Task.CompletedTask;
            connecting = ConnectAsync(nickname, modelId, code, create, singlePlayer, worldSlot);
            return connecting;
        }

        private async Task ConnectAsync(string nickname, int modelId, string code, bool create, bool singlePlayer, int worldSlot)
        {
            if (!GuestProfile.TryNormalizeName(nickname, out var name, out var error))
            { SetState(SessionState.Failed, error); return; }
            if (!singlePlayer && !create && !RoomCodeService.TryNormalize(code, out code))
            { SetState(SessionState.Failed, "Use a valid 6–8 character room code."); return; }
            if ((singlePlayer || create) && !WorldSaveSlots.IsValidSlot(worldSlot))
            { SetState(SessionState.Failed, "Choose world slot 1, 2 or 3."); return; }
            if (config == null || !config.IsValid || playerPrefab == null
                || gameRunnerPrefab == null || playerControllerPrefab == null || sandboxRegistryPrefab == null
                || modelCatalog == null || !modelCatalog.Validate(out _) || !modelCatalog.IsValidModelId(modelId)
                || followCamera == null || spawnPoint == null)
            { SetState(SessionState.Failed, "Network scene setup is incomplete."); return; }
            FusionAppSettings settings = null;
            if (!singlePlayer && !TryGetPhotonSettings(out settings))
            { SetState(SessionState.Failed, "Set your Fusion 2 App ID in PhotonAppSettings / Fusion Hub first."); return; }

            cancellation = new CancellationTokenSource();
            timedOut = false;
            deadline = float.PositiveInfinity;
            var openingWorld = city != null ? "Opening the city…" : "Opening the yard…";
            SetState(SessionState.Connecting, singlePlayer ? openingWorld : create ? "Creating room…" : "Joining room…");
            try
            {
                await StopBrowsing();
                for (var attempt = 0; attempt < (create ? config.CreateAttempts : 1); attempt++)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    var candidate = create ? RoomCodeService.Generate() : code;
                    var creationId = Guid.NewGuid().ToString("N");
                    var args = singlePlayer
                        ? new StartGameArgs { GameMode = GameMode.Single, StartGameCancellationToken = cancellation.Token }
                        : FusionSessionOptions.Build(config, settings, candidate, create, creationId, cancellation.Token);
                    pendingNickname = name;
                    var runtime = CreateRunner();
                    runner.ProvideInput = !singlePlayer;
                    args.SceneManager = runtime.SceneManager;
                    args.ObjectProvider = runtime.ObjectProvider;
                    // This slice keeps the already loaded static arena on each peer. No
                    // network scene objects or scene streaming; use one client per process.
                    deadline = Time.realtimeSinceStartup + config.ConnectionTimeoutSeconds;
                    var result = await runner.StartGame(args);
                    cancellation.Token.ThrowIfCancellationRequested();
                    if (!result.Ok)
                    {
                        var reason = result.ShutdownReason;
                        await DisposeRunnerAsync();
                        if (create && reason == ShutdownReason.GameIdAlreadyExists) continue;
                        SetState(SessionState.Failed, FusionSessionOptions.Describe(reason));
                        return;
                    }
                    if (singlePlayer)
                    {
                        sandbox = await FindOrCreateSandboxRegistryAsync(cancellation.Token, worldSlot, true);
                        SpawnLocalAvatar(modelId);
                        return;
                    }
                    if (create && !FusionSessionOptions.MatchesCreation(runner.SessionInfo.Properties, creationId))
                    {
                        await DisposeRunnerAsync();
                        continue;
                    }
                    if (!runner.SessionInfo.Properties.TryGetValue(FusionSessionOptions.VersionKey, out var version)
                        || !version.IsString || (string)version != FusionSessionOptions.ProtocolVersion(config))
                    {
                        await DisposeRunnerAsync();
                        SetState(SessionState.Failed, "Version mismatch. Use the same game build.");
                        return;
                    }

                    RoomCode = candidate;
                    SetState(SessionState.Connecting, "Connected. Waiting for the host to spawn your dog…");
                    sandbox = await FindOrCreateSandboxRegistryAsync(cancellation.Token, worldSlot, create);
                    sandbox.BindCity(city);
                    roomOwner = create
                        ? runner.LocalPlayer
                        : await WaitForRoomOwnerAsync(sandbox, cancellation.Token);
                    if (!create && sandbox.RoomClosing)
                    {
                        await DisposeRunnerAsync();
                        SetState(SessionState.Failed, OwnerLeftMessage);
                        return;
                    }
                    await WaitForLocalAvatarAsync(modelId, cancellation.Token);
                    if (create) SetRoomAvailability(true);
                    return;
                }
                SetState(SessionState.Failed, "Room codes collided. Please create again.");
            }
            catch (OperationCanceledException)
            {
                await DisposeRunnerAsync();
                SetState(timedOut ? SessionState.Failed : SessionState.Disconnected,
                    timedOut ? "Connection timed out. Please retry." : "Connection cancelled.");
            }
            catch (Exception exception)
            {
                await DisposeRunnerAsync();
                // Never print the App ID, auth payload or raw transport exception.
                SetState(SessionState.Failed, $"Network setup failed ({exception.GetType().Name}). Please retry.");
            }
            finally
            {
                cancellation?.Dispose();
                cancellation = null;
            }
        }

        private FusionRunnerRoot CreateRunner()
        {
            var runtime = Instantiate(gameRunnerPrefab);
            runner = runtime.Runner;
            NetworkRunner.CloudConnectionLostCurrentMode = NetworkRunner.CloudConnectionLostMode.Disabled;
            runner.ProvideInput = true;
            var events = runtime.Events;
            events.OnShutdown ??= new NetworkEvents.ShutdownEvent();
            events.OnShutdown.AddListener(OnShutdown);
            events.PlayerJoined ??= new NetworkEvents.PlayerEvent();
            events.PlayerJoined.AddListener(OnPlayerJoined);
            events.PlayerLeft ??= new NetworkEvents.PlayerEvent();
            events.PlayerLeft.AddListener(OnPlayerLeft);
            events.OnInput ??= new NetworkEvents.InputEvent();
            events.OnInput.AddListener(OnInput);
            runner.AddCallbacks(events);
            runtime.FaceService.Initialize(runner);
            return runtime;
        }

        private async Task<SandboxInteractionRegistry> FindOrCreateSandboxRegistryAsync(CancellationToken token,
            int worldSlot, bool ownsWorld)
        {
            if (sandboxRegistryPrefab == null) throw new InvalidOperationException("Sandbox registry prefab is missing.");
            var expires = Time.realtimeSinceStartup + config.ConnectionTimeoutSeconds;
            while (runner != null && runner.IsRunning && Time.realtimeSinceStartup < expires)
            {
                token.ThrowIfCancellationRequested();
                if (sandbox != null && sandbox.Object != null && sandbox.Object.IsValid && sandbox.Runner == runner)
                    return sandbox;
                if (ownsWorld)
                {
                    var owner = IsSinglePlayer ? PlayerRef.None : runner.LocalPlayer;
                    var spawned = runner.Spawn(sandboxRegistryPrefab, onBeforeSpawned: (_, obj) =>
                        obj.GetComponent<SandboxInteractionRegistry>().InitializeWorld(worldSlot, owner, city));
                    if (spawned != null) return spawned.GetComponent<SandboxInteractionRegistry>();
                }
                await Task.Yield();
            }
            throw new TimeoutException("Sandbox interaction registry was not available.");
        }

        private async Task<PlayerRef> WaitForRoomOwnerAsync(SandboxInteractionRegistry registry,
            CancellationToken token)
        {
            var expires = Time.realtimeSinceStartup + config.ConnectionTimeoutSeconds;
            while (runner != null && runner.IsRunning && registry != null
                && registry.Object != null && registry.Object.IsValid
                && Time.realtimeSinceStartup < expires)
            {
                token.ThrowIfCancellationRequested();
                var owner = registry.RoomOwner;
                if (IsRoomOwnerReady(owner, runner.CommittedPlayers)) return owner;
                await Task.Yield();
            }
            throw new TimeoutException("The room owner metadata was not replicated in time.");
        }

        internal static bool IsRoomOwnerReady(PlayerRef owner, System.Collections.Generic.IEnumerable<PlayerRef> committedPlayers)
        {
            if (!owner.IsRealPlayer || committedPlayers == null) return false;
            foreach (var player in committedPlayers)
                if (player == owner) return true;
            return false;
        }

        public async Task<ModelSelectionResult> SelectModelAsync(int modelId)
        {
            if (!IsConnected || runner == null || localPlayer == null || !modelCatalog.IsValidModelId(modelId))
                return ModelSelectionResult.PlayerUnavailable;
            Message = "Requesting dog model…";
            Changed?.Invoke();
            var result = localPlayer.RequestModel(modelId)
                ? ModelSelectionResult.Approved : ModelSelectionResult.InvalidModel;
            await Task.Yield();
            if (result == ModelSelectionResult.Approved)
            {
                SelectedModelId = modelId;
                Message = $"Dog changed to {modelCatalog.Resolve(modelId).DisplayName}.";
            }
            else Message = DogModelSelectionRegistry.Describe(result);
            Changed?.Invoke();
            return result;
        }

        private void SpawnLocalAvatar(int modelId)
        {
            var slot = CalculateSpawnSlot(runner.LocalPlayer);
            var spawnPosition = FindAvailableSpawnPosition(slot);
            var avatar = runner.Spawn(playerPrefab, spawnPosition, spawnPoint.rotation, runner.LocalPlayer);
            if (avatar == null) throw new InvalidOperationException("Player prefab could not be spawned.");
            localPlayer = avatar.GetComponent<NetworkDogPlayer>();
            localPlayer.ConfigureLocalOwner(pendingNickname, modelId, followCamera);
            if (modelCatalog.TryGet(modelId, out var entry) && entry.SupportsCustomFace
                && DogFaceStorage.TryLoad(out var savedFace)) localPlayer.PublishCustomFace(savedFace);
            else localPlayer.PublishDefaultFace();
            runner.SetPlayerObject(runner.LocalPlayer, avatar);
            SelectedModelId = modelId;
            SetState(SessionState.Connected,
                IsSinglePlayer ? $"Playing offline as {modelCatalog.Resolve(modelId).DisplayName}."
                    : $"Connected to {RoomCode} ({config.Region}) with {modelCatalog.Resolve(modelId).DisplayName}.");
        }

        private async Task WaitForLocalAvatarAsync(int modelId, CancellationToken token)
        {
            var expires = Time.realtimeSinceStartup + config.ConnectionTimeoutSeconds;
            while (runner != null && runner.IsRunning && Time.realtimeSinceStartup < expires)
            {
                token.ThrowIfCancellationRequested();
                var avatar = runner.GetPlayerObject(runner.LocalPlayer);
                if (avatar != null && avatar.IsValid)
                {
                    localPlayer = avatar.GetComponent<NetworkDogPlayer>();
                    if (localPlayer == null) throw new InvalidOperationException("Player prefab has no NetworkDogPlayer.");
                    localPlayer.ConfigureLocalOwner(pendingNickname, modelId, followCamera);
                    if (modelCatalog.TryGet(modelId, out var entry) && entry.SupportsCustomFace
                        && DogFaceStorage.TryLoad(out var savedFace)) localPlayer.PublishCustomFace(savedFace);
                    else localPlayer.PublishDefaultFace();
                    SelectedModelId = modelId;
                    SetState(SessionState.Connected,
                        $"Connected to {RoomCode} ({config.Region}) with {modelCatalog.Resolve(modelId).DisplayName}.");
                    return;
                }
                await Task.Yield();
            }
            throw new TimeoutException("The host did not spawn the local avatar.");
        }

        private void OnPlayerJoined(NetworkRunner source, PlayerRef player)
        {
            if (source != runner || !source.IsServer || source.GameMode == GameMode.Single) return;
            var existing = FindAvatarForPlayer(source, player);
            if (existing != null)
            {
                if (source.GetPlayerObject(player) == null) source.SetPlayerObject(player, existing);
                return;
            }
            var slot = CalculateSpawnSlot(player);
            var position = FindAvailableSpawnPosition(slot);
            var rotation = city != null ? spawnPoint.rotation
                : Quaternion.LookRotation(spawnPoint.position - position, spawnPoint.up);
            var avatar = source.Spawn(playerPrefab, position, rotation, player);
            if (avatar != null) source.SetPlayerObject(player, avatar);
        }

        private static NetworkObject FindAvatarForPlayer(NetworkRunner source, PlayerRef player)
        {
            var registered = source.GetPlayerObject(player);
            if (registered != null && registered.IsValid) return registered;
            foreach (var candidate in NetworkDogPlayer.Active)
                if (candidate != null && candidate.Runner == source && candidate.Object != null
                    && candidate.Object.IsValid && candidate.Object.InputAuthority == player)
                    return candidate.Object;
            return null;
        }

        private void OnPlayerLeft(NetworkRunner source, PlayerRef player)
        {
            if (source != runner) return;
            if (player == roomOwner && player != source.LocalPlayer)
            {
                SetRoomAvailability(false);
                _ = LeaveAsync(OwnerLeftMessage);
                return;
            }
            if (!source.IsServer) return;
            NetworkCarryable.ReleaseCarriedBy(source, player);
            foreach (var vehicle in NetworkCityVehicle.Active)
                if (vehicle.Runner == source) vehicle.ReleaseDepartingPlayer(player);
            // Remove every avatar carrying this authority. Normally there is exactly one;
            // scanning also cleans up a stale duplicate created by an interrupted join.
            for (var index = NetworkDogPlayer.Active.Count - 1; index >= 0; index--)
            {
                var candidate = NetworkDogPlayer.Active[index];
                if (candidate != null && candidate.Runner == source && candidate.Object != null
                    && candidate.Object.IsValid && candidate.Object.InputAuthority == player)
                {
                    candidate.ReleaseHeldTarget();
                    candidate.ReleaseFromCarrier(null, false);
                    source.Despawn(candidate.Object);
                }
            }
        }

        private void OnInput(NetworkRunner source, NetworkInput networkInput)
        {
            if (source != runner) return;
            networkInput.Set(localPlayer != null ? localPlayer.ConsumeNetworkInput() : default(DogNetworkInput));
        }

        private Vector3 FindAvailableSpawnPosition(int initialSlot)
        {
            const int maxAttempts = 32;
            var controller = playerControllerPrefab;
            var center = spawnPoint.rotation * controller.center;
            var halfAxis = spawnPoint.up * Mathf.Max(0, controller.height * .5f - controller.radius);
            if (city != null)
            {
                var positions = city.SafeSpawns;
                for (var attempt = 0; attempt < positions.Length; attempt++)
                {
                    var position = positions[(initialSlot + attempt) % positions.Length];
                    if (IsPlayerSpawnOccupied(position)) continue;
                    if (!Physics.CheckCapsule(position + center - halfAxis, position + center + halfAxis,
                            controller.radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        return position;
                }
                return spawnPoint.position;
            }
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var candidate = spawnPoint.position
                    + spawnPoint.rotation * CalculateSpawnOffset(initialSlot + attempt);
                if (IsPlayerSpawnOccupied(candidate)) continue;
                var capsuleCenter = candidate + center;
                if (!Physics.CheckCapsule(capsuleCenter - halfAxis, capsuleCenter + halfAxis,
                        controller.radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    return candidate;
            }

            return spawnPoint.position
                + spawnPoint.rotation * CalculateSpawnOffset(initialSlot + maxAttempts);
        }

        private bool IsPlayerSpawnOccupied(Vector3 position)
        {
            // The controller capsule is much narrower than the dog visual. Reserve enough
            // horizontal room for the complete model so adjacent joins never look stacked.
            var minimumSqr = MinimumSpawnSeparation * MinimumSpawnSeparation;
            foreach (var player in NetworkDogPlayer.Active)
            {
                if (player == null || player.Runner != runner || player.Object == null || !player.Object.IsValid)
                    continue;
                var delta = player.transform.position - position;
                if (Mathf.Abs(delta.y) < 2f && new Vector2(delta.x, delta.z).sqrMagnitude < minimumSqr)
                    return true;
            }

            return false;
        }

        public static int CalculateSpawnSlot(PlayerRef player)
        {
            return Mathf.Max(0, player.AsIndex);
        }

        public static Vector3 CalculateSpawnOffset(int slot)
        {
            slot = Mathf.Max(0, slot);
            var ring = slot / 8;
            var radius = FirstSpawnRingRadius + ring * SpawnRingSpacing;
            var angle = (slot % 8) * Mathf.PI * .25f + ring * Mathf.PI * .125f;
            return new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
        }

        public Task LeaveAsync() => LeaveAsync(null);

        private Task LeaveAsync(string message)
        {
            if (leaving != null && !leaving.IsCompleted) return leaving;
            leaving = LeaveCoreAsync(message);
            return leaving;
        }

        private async Task LeaveCoreAsync(string message)
        {
            cancellation?.Cancel();
            if (connecting != null && !connecting.IsCompleted) await connecting;
            SetState(SessionState.Disconnecting, "Leaving room…");
            await DisposeRunnerAsync();
            SetState(SessionState.Disconnected, message ?? "Left room. You can create or join again.");
        }

        public void SetControlsEnabled(bool value) => localPlayer?.SetControlsEnabled(IsConnected && value);

        private async Task DisposeRunnerAsync()
        {
            SaveAndCloseOwnedWorld();
            ClearSessionBindings();
            pendingNickname = string.Empty;
            var old = runner;
            runner = null; // Ignore callbacks from this attempt before awaiting shutdown.
            if (old == null) return;
            await ShutdownRunnerAsync(old);
        }

        private void OnShutdown(NetworkRunner source, ShutdownReason reason)
        {
            if (destroyed || source != runner || !IsConnected) return;
            sandbox?.SaveWorld();
            ClearSessionBindings();
            runner = null;
            Destroy(source.gameObject);
            SetState(SessionState.Failed, FusionSessionOptions.Describe(reason));
        }

        private void ClearSessionBindings()
        {
            SetControlsEnabled(false);
            localPlayer = null;
            sandbox = null;
            roomOwner = PlayerRef.None;
            SelectedModelId = 0;
            if (followCamera != null) followCamera.Unbind();
            RoomCode = string.Empty;
        }

        private void Update()
        {
            if (State == SessionState.Connecting && cancellation != null
                && !cancellation.IsCancellationRequested && Time.realtimeSinceStartup >= deadline)
            { timedOut = true; cancellation.Cancel(); }
            if (lobbyCancellation != null && !lobbyCancellation.IsCancellationRequested
                && Time.realtimeSinceStartup >= lobbyDeadline) lobbyCancellation.Cancel();

            if (runner == null || !runner.IsRunning || !roomOwner.IsRealPlayer || roomOwner == runner.LocalPlayer
                || (leaving != null && !leaving.IsCompleted)) return;
            if (sandbox != null && sandbox.Object != null && sandbox.Object.IsValid && sandbox.RoomClosing)
            {
                SetRoomAvailability(false);
                _ = LeaveAsync(OwnerLeftMessage);
            }
        }

        private void SetRoomAvailability(bool open)
        {
            if (runner == null || !runner.IsRunning || !runner.IsServer) return;
            runner.SessionInfo.IsOpen = open;
            runner.SessionInfo.IsVisible = open;
        }

        private void SaveAndCloseOwnedWorld()
        {
            sandbox?.SaveWorld();
            if (runner == null || !roomOwner.IsRealPlayer || roomOwner != runner.LocalPlayer) return;
            SetRoomAvailability(false);
            sandbox?.BeginClosing();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) sandbox?.SaveWorld();
        }

        private void SetState(SessionState state, string message)
        {
            if (destroyed) return;
            State = state;
            Message = message;
            Changed?.Invoke();
        }

        private void OnDestroy()
        {
            SandboxInteractionRegistry.RegistrySpawned -= OnSandboxSpawned;
            SaveAndCloseOwnedWorld();
            destroyed = true;
            cancellation?.Cancel();
            _ = StopBrowsing();
            SetControlsEnabled(false);
            if (runner != null) { var old = runner; runner = null; _ = old.Shutdown(); }
        }
    }
}
