using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fusion;
using Fusion.Photon.Realtime;
using UnityEngine;

namespace DogSimulator.Networking
{
    public sealed partial class FusionSessionService
    {
        internal readonly struct RoomListing
        {
            public readonly string Code;
            public readonly int Players;
            public readonly int Capacity;

            public RoomListing(string code, int players, int capacity)
            {
                Code = code;
                Players = players;
                Capacity = capacity;
            }
        }

        internal const string OwnerLeftMessage = "The room closed because its owner left.";
        private readonly List<RoomListing> rooms = new();
        [SerializeField] private FusionRunnerRoot lobbyRunnerPrefab;
        private NetworkRunner lobbyRunner;
        private Task lobbyOperation = Task.CompletedTask;
        private CancellationTokenSource lobbyCancellation;
        private int lobbyGeneration;
        private int activeLobbyGeneration;
        private bool browsing;
        private float lobbyDeadline;

        internal IReadOnlyList<RoomListing> Rooms => rooms;
        internal string RoomListMessage { get; private set; } = "";
        internal event Action RoomsChanged;

        internal void BrowseRooms()
        {
            // Photon keeps an active lobby subscription up to date, including on Refresh.
            if (browsing && (!lobbyOperation.IsCompleted || (lobbyRunner != null && lobbyRunner.LobbyInfo.IsValid))) return;
            rooms.Clear();
            UpdateRoomList("Finding rooms…");
            _ = ChangeBrowsing(true);
        }
        internal Task StopBrowsing() => ChangeBrowsing(false);

        private Task ChangeBrowsing(bool enable)
        {
            if (!enable && !browsing) return lobbyOperation;
            browsing = enable;
            var generation = ++lobbyGeneration;
            lobbyOperation = ChangeBrowsingAsync(lobbyOperation, generation, enable);
            return lobbyOperation;
        }

        private async Task ChangeBrowsingAsync(Task previous, int generation, bool enable)
        {
            await previous;
            var old = lobbyRunner;
            lobbyRunner = null;
            if (old != null) await ShutdownRunnerAsync(old);
            if (destroyed || generation != lobbyGeneration || !enable) return;

            if (lobbyRunnerPrefab == null || config == null || !config.IsValid || !TryGetPhotonSettings(out var settings))
            {
                UpdateRoomList("Room list unavailable. Check the Photon settings.");
                return;
            }
            using var token = new CancellationTokenSource();
            lobbyCancellation = token;
            lobbyDeadline = Time.realtimeSinceStartup + config.ConnectionTimeoutSeconds;
            try
            {
                var runtime = Instantiate(lobbyRunnerPrefab);
                lobbyRunner = runtime.Runner;
                activeLobbyGeneration = generation;
                var events = runtime.Events;
                events.OnSessionListUpdate ??= new NetworkEvents.SessionListUpdateEvent();
                events.OnSessionListUpdate.AddListener(OnRoomListUpdated);
                events.OnShutdown ??= new NetworkEvents.ShutdownEvent();
                events.OnShutdown.AddListener(OnLobbyShutdown);
                lobbyRunner.AddCallbacks(events);
                var result = await lobbyRunner.JoinSessionLobby(SessionLobby.ClientServer,
                    customAppSettings: FusionSessionOptions.CopySettings(config, settings), cancellationToken: token.Token);
                if (generation != lobbyGeneration || destroyed) return;
                UpdateRoomList(result.Ok ? "" : token.IsCancellationRequested
                    ? "Room list timed out. Try Refresh." : FusionSessionOptions.Describe(result.ShutdownReason));
            }
            catch (OperationCanceledException)
            {
                if (!destroyed && generation == lobbyGeneration) UpdateRoomList("Room list timed out. Try Refresh.");
            }
            catch (Exception)
            {
                if (!destroyed && generation == lobbyGeneration) UpdateRoomList("Room list unavailable. Try Refresh.");
            }
            finally { lobbyCancellation = null; }
        }

        private void OnRoomListUpdated(NetworkRunner source, List<SessionInfo> sessions)
        {
            if (source != lobbyRunner || activeLobbyGeneration != lobbyGeneration || !browsing || destroyed) return;
            rooms.Clear();
            foreach (var entry in sessions)
            {
                if (!entry.IsOpen || !entry.IsVisible || entry.PlayerCount <= 0
                    || !RoomCodeService.TryNormalize(entry.Name, out var code)
                    || !entry.Properties.TryGetValue(FusionSessionOptions.VersionKey, out var build)
                    || !build.IsString || (string)build != FusionSessionOptions.ProtocolVersion(config)) continue;
                rooms.Add(new RoomListing(code, entry.PlayerCount, entry.MaxPlayers));
            }
            rooms.Sort((left, right) => string.CompareOrdinal(left.Code, right.Code));
            UpdateRoomList("");
        }

        private void OnLobbyShutdown(NetworkRunner source, ShutdownReason reason)
        {
            if (source != lobbyRunner || activeLobbyGeneration != lobbyGeneration || !browsing || destroyed) return;
            rooms.Clear();
            UpdateRoomList(FusionSessionOptions.Describe(reason));
        }

        private void UpdateRoomList(string message)
        {
            RoomListMessage = message;
            RoomsChanged?.Invoke();
        }

        private static bool TryGetPhotonSettings(out FusionAppSettings settings)
        {
            settings = PhotonAppSettings.TryGetGlobal(out var asset) ? asset.AppSettings : null;
            return settings != null && Guid.TryParse(settings.AppIdFusion, out var id) && id != Guid.Empty;
        }

        private static async Task ShutdownRunnerAsync(NetworkRunner old)
        {
            try { await old.Shutdown(false); }
            catch (Exception) { Debug.LogWarning("Fusion runner shutdown reported an error; disposing the runner instance."); }
            finally { if (old != null) Destroy(old.gameObject); }
        }
    }
}
