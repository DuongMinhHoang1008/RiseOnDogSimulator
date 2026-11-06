using System;
using System.Threading;
using System.Threading.Tasks;
using DogSimulator.Player;
using Fusion;
using Fusion.Photon.Realtime;
using UnityEngine;

namespace DogSimulator.Networking
{
    public enum SessionState { Disconnected, Connecting, Selecting, Connected, Disconnecting, Failed }

    public sealed class FusionSessionService : MonoBehaviour
    {
        [SerializeField] private NetworkConfig config;
        [SerializeField] private NetworkObject playerPrefab;
        [SerializeField] private GameObject registryPrefab;
        [SerializeField] private GameObject selectionRequestPrefab;
        [SerializeField] private GameObject sandboxRegistryPrefab;
        [SerializeField] private DogModelCatalog modelCatalog;
        [SerializeField] private DogCamera followCamera;
        [SerializeField] private Transform spawnPoint;
        private NetworkRunner runner;
        private NetworkDogPlayer localPlayer;
        private DogModelSelectionRegistry registry;
        private DogModelSelectionRequest selectionRequest;
        private CancellationTokenSource cancellation;
        private Task connecting;
        private Task leaving;
        private float deadline;
        private bool timedOut;
        private bool destroyed;
        private int selectionRequestId;
        private string pendingNickname;

        public SessionState State { get; private set; }
        public string Message { get; private set; } = "Create or join a private Shared room. Rigged dog with movement/action animations.";
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
        public bool HasAvatar => State == SessionState.Connected && localPlayer != null;
        public DogModelCatalog ModelCatalog => modelCatalog;
        public int SelectedModelId { get; private set; }
        public event Action Changed;

        public Task CreateAsync(string nickname, int modelId) => Begin(nickname, modelId, null, true);
        public Task JoinAsync(string nickname, int modelId, string code) => Begin(nickname, modelId, code, false);
        public Task CreateAsync(string nickname) => CreateAsync(nickname, modelCatalog != null ? modelCatalog.FallbackModelId : 1);
        public Task JoinAsync(string nickname, string code) => JoinAsync(nickname, modelCatalog != null ? modelCatalog.FallbackModelId : 1, code);

        private Task Begin(string nickname, int modelId, string code, bool create)
        {
            if (destroyed || IsConnected || State == SessionState.Disconnecting
                || (connecting != null && !connecting.IsCompleted)
                || (leaving != null && !leaving.IsCompleted)) return Task.CompletedTask;
            connecting = ConnectAsync(nickname, modelId, code, create);
            return connecting;
        }

        private async Task ConnectAsync(string nickname, int modelId, string code, bool create)
        {
            if (!GuestProfile.TryNormalizeName(nickname, out var name, out var error))
            { SetState(SessionState.Failed, error); return; }
            pendingNickname = name;
            if (!create && !RoomCodeService.TryNormalize(code, out code))
            { SetState(SessionState.Failed, "Use a valid 6–8 character room code."); return; }
            if (config == null || !config.IsValid || playerPrefab == null || registryPrefab == null || selectionRequestPrefab == null
                || sandboxRegistryPrefab == null
                || modelCatalog == null || !modelCatalog.Validate(out _) || !modelCatalog.IsValidModelId(modelId)
                || followCamera == null || spawnPoint == null)
            { SetState(SessionState.Failed, "Network scene setup is incomplete."); return; }
            if (!PhotonAppSettings.TryGetGlobal(out var settings)
                || !Guid.TryParse(settings.AppSettings.AppIdFusion, out var id) || id == Guid.Empty)
            { SetState(SessionState.Failed, "Set your Fusion 2 App ID in PhotonAppSettings / Fusion Hub first."); return; }

            cancellation = new CancellationTokenSource();
            timedOut = false;
            SetState(SessionState.Connecting, create ? "Creating private room…" : "Joining room…");
            try
            {
                for (var attempt = 0; attempt < (create ? config.CreateAttempts : 1); attempt++)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    var candidate = create ? RoomCodeService.Generate() : code;
                    var creationId = Guid.NewGuid().ToString("N");
                    var args = FusionSessionOptions.Build(config, settings.AppSettings, candidate, create, creationId, cancellation.Token);
                    CreateRunner();
                    args.SceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
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
                    if (create && !FusionSessionOptions.MatchesCreation(runner.SessionInfo.Properties, creationId))
                    {
                        await DisposeRunnerAsync();
                        continue;
                    }
                    if (!runner.SessionInfo.Properties.TryGetValue(FusionSessionOptions.VersionKey, out var version)
                        || !version.IsString || (string)version != config.GameVersion)
                    {
                        await DisposeRunnerAsync();
                        SetState(SessionState.Failed, "Version mismatch. Use the same game build.");
                        return;
                    }

                    RoomCode = candidate;
                    SetState(SessionState.Connecting, "Connected. Waiting for the Master to approve your dog model…");
                    registry = await FindOrCreateRegistryAsync(cancellation.Token);
                    await FindOrCreateSandboxRegistryAsync(cancellation.Token);
                    var requestObject = runner.Spawn(selectionRequestPrefab);
                    if (requestObject == null) throw new InvalidOperationException("Model selection request could not be spawned.");
                    selectionRequest = requestObject.GetComponent<DogModelSelectionRequest>();
                    var selection = await ReserveAsync(modelId, cancellation.Token);
                    if (selection != ModelSelectionResult.Approved)
                    {
                        SetState(SessionState.Selecting,
                            $"{DogModelSelectionRegistry.Describe(selection)} Choose another dog to continue in room {RoomCode}.");
                        return;
                    }
                    SpawnLocalAvatar(modelId);
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

        private void CreateRunner()
        {
            var root = new GameObject("Fusion Runner — Shared");
            runner = root.AddComponent<NetworkRunner>();
            runner.ProvideInput = false; // Shared avatars capture their own owner input.
            var events = root.AddComponent<NetworkEvents>();
            // Runtime-added components do not get serialized UnityEvent instances.
            events.OnShutdown = new NetworkEvents.ShutdownEvent();
            events.OnShutdown.AddListener(OnShutdown);
            runner.AddCallbacks(events);
        }

        private async Task<DogModelSelectionRegistry> FindOrCreateRegistryAsync(CancellationToken token)
        {
            var expires = Time.realtimeSinceStartup + config.ConnectionTimeoutSeconds;
            while (runner != null && runner.IsRunning && Time.realtimeSinceStartup < expires)
            {
                token.ThrowIfCancellationRequested();
                var candidates = FindObjectsByType<DogModelSelectionRegistry>(FindObjectsSortMode.None);
                foreach (var candidate in candidates)
                    if (candidate != null && candidate.Object != null && candidate.Object.IsValid && candidate.Runner == runner)
                        return candidate;
                if (runner.IsSharedModeMasterClient)
                {
                    var spawned = runner.Spawn(registryPrefab);
                    if (spawned != null) return spawned.GetComponent<DogModelSelectionRegistry>();
                }
                await Task.Yield();
            }
            throw new TimeoutException("The model reservation registry was not available.");
        }

        private async Task<SandboxInteractionRegistry> FindOrCreateSandboxRegistryAsync(CancellationToken token)
        {
            if (sandboxRegistryPrefab == null) throw new InvalidOperationException("Sandbox registry prefab is missing.");
            var expires = Time.realtimeSinceStartup + config.ConnectionTimeoutSeconds;
            while (runner != null && runner.IsRunning && Time.realtimeSinceStartup < expires)
            {
                token.ThrowIfCancellationRequested();
                var candidates = FindObjectsByType<SandboxInteractionRegistry>(FindObjectsSortMode.None);
                foreach (var candidate in candidates)
                    if (candidate != null && candidate.Object != null && candidate.Object.IsValid && candidate.Runner == runner)
                        return candidate;
                if (runner.IsSharedModeMasterClient)
                {
                    var spawned = runner.Spawn(sandboxRegistryPrefab);
                    if (spawned != null) return spawned.GetComponent<SandboxInteractionRegistry>();
                }
                await Task.Yield();
            }
            throw new TimeoutException("Sandbox interaction registry was not available.");
        }

        private async Task<ModelSelectionResult> ReserveAsync(int modelId, CancellationToken token)
        {
            if (registry == null || selectionRequest == null) return ModelSelectionResult.PlayerUnavailable;
            var requestId = unchecked(++selectionRequestId);
            if (!selectionRequest.Submit(modelId, requestId)) return ModelSelectionResult.PlayerUnavailable;
            var expires = Time.realtimeSinceStartup + config.ConnectionTimeoutSeconds;
            while (runner != null && runner.IsRunning && Time.realtimeSinceStartup < expires)
            {
                token.ThrowIfCancellationRequested();
                if (registry.TryGetReservation(runner.LocalPlayer, out var reserved) && reserved == modelId)
                    return ModelSelectionResult.Approved;
                if (registry.TryGetResult(runner.LocalPlayer, requestId, out var result)
                    && result != ModelSelectionResult.None && result != ModelSelectionResult.Approved)
                    return result;
                await Task.Yield();
            }
            return ModelSelectionResult.PlayerUnavailable;
        }

        public async Task<ModelSelectionResult> SelectModelAsync(int modelId)
        {
            if (!IsConnected || runner == null || registry == null)
                return ModelSelectionResult.PlayerUnavailable;
            Message = "Requesting dog model…";
            Changed?.Invoke();
            var result = await ReserveAsync(modelId, CancellationToken.None);
            if (result == ModelSelectionResult.Approved)
            {
                SelectedModelId = modelId;
                if (localPlayer == null) SpawnLocalAvatar(modelId);
                else
                {
                    localPlayer.SetModel(modelId);
                    Message = $"Dog changed to {modelCatalog.Resolve(modelId).DisplayName}.";
                }
            }
            else Message = DogModelSelectionRegistry.Describe(result);
            Changed?.Invoke();
            return result;
        }

        private void SpawnLocalAvatar(int modelId)
        {
            var slot = CalculateSpawnSlot(runner.LocalPlayer);
            var spawnPosition = FindAvailableSpawnPosition(slot);
            var avatar = runner.Spawn(playerPrefab, spawnPosition, spawnPoint.rotation);
            if (avatar == null) throw new InvalidOperationException("Player prefab could not be spawned.");
            localPlayer = avatar.GetComponent<NetworkDogPlayer>();
            localPlayer.InitializeOwner(pendingNickname, modelId, followCamera);
            runner.SetPlayerObject(runner.LocalPlayer, avatar);
            SelectedModelId = modelId;
            SetState(SessionState.Connected,
                $"Connected to {RoomCode} ({config.Region}) with {modelCatalog.Resolve(modelId).DisplayName}.");
        }

        private Vector3 FindAvailableSpawnPosition(int initialSlot)
        {
            const int maxAttempts = 32;
            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var candidate = spawnPoint.position
                    + spawnPoint.rotation * CalculateSpawnOffset(initialSlot + attempt);
                if (!IsPlayerSpawnOccupied(candidate)) return candidate;
            }

            return spawnPoint.position
                + spawnPoint.rotation * CalculateSpawnOffset(initialSlot + maxAttempts);
        }

        private static bool IsPlayerSpawnOccupied(Vector3 position)
        {
            // Match the player CharacterController closely without treating the floor as occupied.
            var hits = Physics.OverlapCapsule(
                position + Vector3.up * .32f,
                position + Vector3.up * .78f,
                .32f,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);

            foreach (var hit in hits)
                if (hit.GetComponentInParent<NetworkDogPlayer>() != null)
                    return true;

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
            var radius = 2f + ring * 2f;
            var angle = (slot % 8) * Mathf.PI * .25f + ring * Mathf.PI * .125f;
            return new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
        }

        public Task LeaveAsync()
        {
            if (leaving != null && !leaving.IsCompleted) return leaving;
            leaving = LeaveCoreAsync();
            return leaving;
        }

        private async Task LeaveCoreAsync()
        {
            cancellation?.Cancel();
            if (connecting != null && !connecting.IsCompleted) await connecting;
            SetState(SessionState.Disconnecting, "Leaving room…");
            await DisposeRunnerAsync();
            SetState(SessionState.Disconnected, "Left room. You can create or join again.");
        }

        public void SetControlsEnabled(bool value) => localPlayer?.SetControlsEnabled(IsConnected && value);

        private async Task DisposeRunnerAsync()
        {
            SetControlsEnabled(false);
            localPlayer = null;
            registry = null;
            selectionRequest = null;
            SelectedModelId = 0;
            pendingNickname = string.Empty;
            if (followCamera != null) followCamera.Unbind();
            RoomCode = string.Empty;
            var old = runner;
            runner = null; // Ignore callbacks from this attempt before awaiting shutdown.
            if (old == null) return;
            try { await old.Shutdown(false); }
            catch (Exception) { Debug.LogWarning("Fusion runner shutdown reported an error; disposing the runner instance."); }
            finally { if (old != null) Destroy(old.gameObject); }
        }

        private void OnShutdown(NetworkRunner source, ShutdownReason reason)
        {
            if (destroyed || source != runner || !IsConnected) return;
            SetControlsEnabled(false);
            localPlayer = null;
            registry = null;
            selectionRequest = null;
            SelectedModelId = 0;
            followCamera.Unbind();
            runner = null;
            RoomCode = string.Empty;
            Destroy(source.gameObject);
            SetState(SessionState.Failed, FusionSessionOptions.Describe(reason));
        }

        private void Update()
        {
            if (State == SessionState.Connecting && cancellation != null
                && !cancellation.IsCancellationRequested && Time.realtimeSinceStartup >= deadline)
            { timedOut = true; cancellation.Cancel(); }
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
            destroyed = true;
            cancellation?.Cancel();
            SetControlsEnabled(false);
            if (runner != null) { var old = runner; runner = null; _ = old.Shutdown(); }
        }
    }
}
