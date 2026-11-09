using System;
using System.Collections.Generic;
using System.Threading;
using Fusion;
using Fusion.Photon.Realtime;

namespace DogSimulator.Networking
{
    // Matchmaking policy for the installed Fusion 2.1.4 SDK; no transport abstraction.
    public static class FusionSessionOptions
    {
        public const string CreationKey = "creation";
        public const string VersionKey = "build";
        public const string WorldBuild = "office-world-slots-v2";
        public const string HostProtocolPrefix = "pc-host-v5-";

        public static string ProtocolVersion(NetworkConfig config)
            => HostProtocolPrefix + config.GameVersion;

        public static StartGameArgs Build(NetworkConfig config, FusionAppSettings settings,
            string code, bool create, string creationId, CancellationToken cancellation)
        {
            if (config == null || !config.IsValid) throw new ArgumentException("Invalid network config.");
            if (settings == null || !Guid.TryParse(settings.AppIdFusion, out var appId) || appId == Guid.Empty)
                throw new ArgumentException("Set a Fusion 2 App ID in PhotonAppSettings first.");
            if (!RoomCodeService.TryNormalize(code, out var normalized)) throw new ArgumentException("Invalid room code.");
            if (create && string.IsNullOrEmpty(creationId)) throw new ArgumentException("Missing creation ID.");

            return new StartGameArgs
            {
                GameMode = create ? GameMode.Host : GameMode.Client,
                SessionName = normalized,
                EnableClientSessionCreation = false,
                IsVisible = false,
                IsOpen = false,
                PlayerCount = config.MaxPlayers,
                CustomPhotonAppSettings = CopySettings(config, settings),
                UseCachedRegions = false,
                StartGameCancellationToken = cancellation,
                SessionProperties = create ? new Dictionary<string, SessionProperty>
                {
                    [CreationKey] = creationId,
                    [VersionKey] = ProtocolVersion(config)
                } : null
            };
        }

        internal static FusionAppSettings CopySettings(NetworkConfig config, FusionAppSettings settings)
        {
            var copy = settings.GetCopy();
            copy.FixedRegion = config.Region;
            copy.AppVersion = ProtocolVersion(config);
            copy.emptyRoomTtl = 0;
            return copy;
        }

        // Host creation writes a nonce so a room-code collision is never mistaken for success.
        public static bool MatchesCreation(IReadOnlyDictionary<string, SessionProperty> properties, string creationId)
            => properties != null && properties.TryGetValue(CreationKey, out var value)
                && value.IsString && (string)value == creationId;

        public static string Describe(ShutdownReason reason)
        {
            switch (reason)
            {
                case ShutdownReason.GameNotFound: return "Room not found. Check the code, region and build version.";
                case ShutdownReason.GameIsFull: return "Room is full.";
                case ShutdownReason.GameClosed: return "Room is closed.";
                case ShutdownReason.GameIdAlreadyExists: return "Room code collision. Please retry.";
                case ShutdownReason.InvalidAuthentication: return "Photon rejected the App ID or authentication settings.";
                case ShutdownReason.MaxCcuReached: return "Photon application capacity has been reached.";
                case ShutdownReason.InvalidRegion: return "Photon region is unavailable.";
                case ShutdownReason.IncompatibleConfiguration: return "Incompatible network configuration.";
                case ShutdownReason.PhotonCloudTimeout:
                case ShutdownReason.ConnectionTimeout:
                case ShutdownReason.OperationTimeout: return "Connection timed out. Please retry.";
                default: return $"Disconnected / network error ({reason}).";
            }
        }
    }
}
