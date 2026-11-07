using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DogSimulator.CustomDogFace;
using DogSimulator.Player;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace DogSimulator.Networking
{
    [DisallowMultipleComponent]
    public sealed class CustomDogFaceNetworkService : MonoBehaviour, INetworkRunnerCallbacks
    {
        private enum MessageType : byte { Fetch = 1, Data = 2, Applied = 3 }
        private const int Magic = 0x44464143; // DFAC
        private const int StreamNamespace = 0x4446;
        private const float RetrySeconds = 12f;
        private const int MaximumImmediateRetries = 3;

        private sealed class Pending
        {
            public int revision;
            public int attempts;
            public float nextAttempt;
        }

        private sealed class Seen
        {
            public int revision;
            public readonly HashSet<PlayerRef> applied = new();
            public readonly HashSet<PlayerRef> offered = new();
        }

        private NetworkRunner runner;
        private int streamSequence;
        private readonly Dictionary<NetworkId, Pending> pending = new();
        private readonly Dictionary<NetworkId, int> renderedRevision = new();
        private readonly Dictionary<string, byte[]> cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<NetworkId, Seen> acknowledgements = new();

        public void Initialize(NetworkRunner value)
        {
            runner = value;
            runner.AddCallbacks(this);
        }

        public bool TryGetAppliedProgress(NetworkId avatar, out int applied, out int expected)
        {
            applied = acknowledgements.TryGetValue(avatar, out var seen) ? seen.applied.Count : 0;
            expected = 0;
            if (runner == null || !runner.IsRunning) return false;
            foreach (var player in runner.CommittedPlayers)
                if (player != runner.LocalPlayer) expected++;
            return true;
        }

        private void Update()
        {
            if (runner == null || !runner.IsRunning) return;
            foreach (var player in NetworkDogPlayer.Active)
            {
                if (player == null || player.Runner != runner || player.Object == null || !player.Object.IsValid
                    || player.FaceRevision <= 0) continue;
                ProcessManifest(player);
            }
        }

        private void ProcessManifest(NetworkDogPlayer player)
        {
            var id = player.Object.Id;
            if (player.Object.HasStateAuthority && player.OwnerFaceJpeg != null && player.FaceKind == DogFaceKind.CustomJpeg)
            {
                var seen = GetSeen(id, player.FaceRevision);
                foreach (var peer in runner.CommittedPlayers)
                    if (peer != runner.LocalPlayer && !seen.applied.Contains(peer) && seen.offered.Add(peer))
                        player.RPC_ReceiveFace(peer, player.FaceRevision, player.OwnerFaceJpeg);
            }
            if (renderedRevision.TryGetValue(id, out var rendered) && rendered == player.FaceRevision) return;
            if (player.FaceKind == DogFaceKind.Default)
            {
                player.FaceAppearance?.ApplyDefault(player.FaceModelId, player.FaceLayoutVersion);
                renderedRevision[id] = player.FaceRevision;
                pending.Remove(id);
                SendApplied(player);
                return;
            }
            if (!ManifestIsPlausible(player)) return;
            var hash = player.FaceSha256.ToString();
            if (cache.TryGetValue(hash, out var jpeg))
            {
                Apply(player, jpeg);
                return;
            }
            if (player.Object.HasStateAuthority && player.OwnerFaceJpeg != null)
            {
                cache[hash] = player.OwnerFaceJpeg;
                Apply(player, player.OwnerFaceJpeg);
                return;
            }
            if (!pending.TryGetValue(id, out var request) || request.revision != player.FaceRevision)
            {
                request = new Pending { revision = player.FaceRevision };
                pending[id] = request;
            }
            if (Time.unscaledTime < request.nextAttempt) return;
            if (request.attempts >= MaximumImmediateRetries) request.nextAttempt = Time.unscaledTime + 30f;
            else request.nextAttempt = Time.unscaledTime + RetrySeconds;
            request.attempts++;
            player.RPC_RequestFace(player.FaceRevision);
        }

        internal void ReceiveFace(NetworkDogPlayer player, int revision, byte[] jpeg)
        {
            if (revision != player.FaceRevision || !ManifestIsPlausible(player)
                || !DogFaceImageValidator.IsValidFaceJpeg(jpeg, out _) || jpeg.Length != player.FaceByteLength) return;
            var hash = player.FaceSha256.ToString();
            if (!string.Equals(DogFaceImageValidator.Sha256Hex(jpeg), hash, StringComparison.OrdinalIgnoreCase)) return;
            cache[hash] = jpeg;
            if (!renderedRevision.TryGetValue(player.Object.Id, out var applied) || applied != revision) Apply(player, jpeg);
        }

        private bool ManifestIsPlausible(NetworkDogPlayer player)
            => player.FaceKind == DogFaceKind.CustomJpeg
                && player.FaceByteLength is > 0 and <= CustomDogFaceConstants.MaximumJpegBytes
                && player.FaceSha256.ToString().Length == 64
                && player.FaceModelId == player.DogModelId
                && player.FaceLayoutVersion == CustomDogFaceConstants.LayoutVersion;

        private bool Apply(NetworkDogPlayer player, byte[] jpeg)
        {
            if (jpeg.Length != player.FaceByteLength || player.FaceAppearance == null
                || player.GetComponent<DogVisualPresenter>()?.RenderedModelId != player.FaceModelId
                || !player.FaceAppearance.ApplyCustom(player.FaceModelId, player.FaceLayoutVersion, jpeg)) return false;
            renderedRevision[player.Object.Id] = player.FaceRevision;
            pending.Remove(player.Object.Id);
            SendApplied(player);
            return true;
        }

        private void SendApplied(NetworkDogPlayer player)
        {
            if (player.Object.HasStateAuthority) return;
            Send(player.Object.StateAuthority, MessageType.Applied, player, 0, null);
        }

        private void Send(PlayerRef target, MessageType type, NetworkDogPlayer player, int requestId, byte[] payload)
        {
            if (runner == null || !runner.IsRunning || !runner.IsPlayerCommitted(target)) return;
            using var stream = new MemoryStream((payload?.Length ?? 0) + 128);
            using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
            writer.Write(Magic);
            writer.Write((byte)CustomDogFaceConstants.ProtocolVersion);
            writer.Write((byte)type);
            writer.Write(player.Object.Id.Raw);
            writer.Write(player.FaceRevision);
            writer.Write(requestId);
            writer.Write(player.FaceModelId);
            writer.Write(player.FaceLayoutVersion);
            writer.Write(player.FaceByteLength);
            var hash = player.FaceSha256.ToString();
            writer.Write((byte)hash.Length);
            writer.Write(Encoding.ASCII.GetBytes(hash));
            if (payload != null) writer.Write(payload);
            var bytes = stream.ToArray();
            var key = ReliableKey.FromInts(StreamNamespace, unchecked((int)player.Object.Id.Raw),
                runner.LocalPlayer.RawEncoded, unchecked(++streamSequence));
            runner.SendReliableDataToPlayer(target, key, bytes);
        }

        public void OnReliableDataReceived(NetworkRunner sourceRunner, PlayerRef source, ReliableKey key, ReadOnlySpan<byte> data)
        {
            if (sourceRunner != runner || !runner.IsRunning || data.Length < 30 || data.Length > CustomDogFaceConstants.MaximumJpegBytes + 160) return;
            try
            {
                using var stream = new MemoryStream(data.ToArray(), false);
                using var reader = new BinaryReader(stream, Encoding.ASCII, true);
                if (reader.ReadInt32() != Magic || reader.ReadByte() != CustomDogFaceConstants.ProtocolVersion) return;
                var type = (MessageType)reader.ReadByte();
                var id = new NetworkId { Raw = reader.ReadUInt32() };
                var revision = reader.ReadInt32();
                var requestId = reader.ReadInt32();
                var modelId = reader.ReadInt32();
                var layout = reader.ReadInt32();
                var byteLength = reader.ReadInt32();
                var hashLength = reader.ReadByte();
                if (hashLength > 64 || stream.Position + hashLength > stream.Length) return;
                var hash = Encoding.ASCII.GetString(reader.ReadBytes(hashLength));
                if (!runner.TryFindObject(id, out var obj)) return;
                var player = obj.GetComponent<NetworkDogPlayer>();
                if (player == null || player.FaceRevision != revision || player.FaceModelId != modelId
                    || player.FaceLayoutVersion != layout || player.FaceByteLength != byteLength
                    || !string.Equals(player.FaceSha256.ToString(), hash, StringComparison.OrdinalIgnoreCase)) return;
                switch (type)
                {
                    case MessageType.Fetch:
                        if (!obj.HasStateAuthority || source == runner.LocalPlayer || !runner.IsPlayerCommitted(source)
                            || player.FaceKind != DogFaceKind.CustomJpeg || player.OwnerFaceJpeg == null) return;
                        Send(source, MessageType.Data, player, requestId, player.OwnerFaceJpeg);
                        break;
                    case MessageType.Data:
                        if (source != obj.StateAuthority || obj.HasStateAuthority || !ManifestIsPlausible(player)
                            || stream.Length - stream.Position != byteLength) return;
                        if (renderedRevision.TryGetValue(id, out var appliedRevision) && appliedRevision == revision) return;
                        var jpeg = reader.ReadBytes((int)(stream.Length - stream.Position));
                        if (!DogFaceImageValidator.IsValidFaceJpeg(jpeg, out _)
                            || !string.Equals(DogFaceImageValidator.Sha256Hex(jpeg), hash, StringComparison.OrdinalIgnoreCase)) return;
                        cache[hash] = jpeg;
                        Apply(player, jpeg);
                        break;
                    case MessageType.Applied:
                        if (!obj.HasStateAuthority || source == runner.LocalPlayer || !runner.IsPlayerCommitted(source)) return;
                        RecordApplied(id, revision, source);
                        break;
                }
            }
            catch (Exception) { /* malformed application payloads are ignored */ }
        }

        private void RecordApplied(NetworkId id, int revision, PlayerRef player)
            => GetSeen(id, revision).applied.Add(player);

        private Seen GetSeen(NetworkId id, int revision)
        {
            if (!acknowledgements.TryGetValue(id, out var seen) || seen.revision != revision)
            { seen = new Seen { revision = revision }; acknowledgements[id] = seen; }
            return seen;
        }

        public void OnPlayerLeft(NetworkRunner r, PlayerRef player)
        {
            foreach (var seen in acknowledgements.Values)
            {
                seen.applied.Remove(player);
                seen.offered.Remove(player);
            }
        }

        public void OnShutdown(NetworkRunner r, ShutdownReason reason)
        {
            pending.Clear(); renderedRevision.Clear(); cache.Clear(); acknowledgements.Clear();
        }

        private void OnDestroy() { if (runner != null) runner.RemoveCallbacks(this); }
        public void OnReliableDataProgress(NetworkRunner r, PlayerRef p, ReliableKey k, float progress) { }
        public void OnObjectExitAOI(NetworkRunner r, NetworkObject o, PlayerRef p) { }
        public void OnObjectEnterAOI(NetworkRunner r, NetworkObject o, PlayerRef p) { }
        public void OnPlayerJoined(NetworkRunner r, PlayerRef p) { }
        public void OnInput(NetworkRunner r, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner r, PlayerRef p, NetworkInput input) { }
        public void OnConnectedToServer(NetworkRunner r) { }
        public void OnDisconnectedFromServer(NetworkRunner r, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner r, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner r, NetAddress address, NetConnectFailedReason reason) { }
        public void OnSessionListUpdated(NetworkRunner r, List<SessionInfo> sessions) { }
        public void OnCustomAuthenticationResponse(NetworkRunner r, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner r, HostMigrationToken token) { }
        public void OnSceneLoadDone(NetworkRunner r) { }
        public void OnSceneLoadStart(NetworkRunner r) { }
    }
}
