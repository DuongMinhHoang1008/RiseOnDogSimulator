using DogSimulator.Player;
using Fusion;
using UnityEngine;

namespace DogSimulator.Networking
{
    public struct ModelSelectionReply : INetworkStruct
    {
        public int RequestId;
        public byte Result;
    }

    [RequireComponent(typeof(NetworkObject))]
    public sealed class DogModelSelectionRegistry : NetworkBehaviour, IPlayerLeft
    {
        [SerializeField] private DogModelCatalog catalog;
        [Networked, Capacity(20)] public NetworkDictionary<PlayerRef, int> Reservations => default;
        [Networked, Capacity(20)] private NetworkDictionary<PlayerRef, ModelSelectionReply> Results => default;

        public DogModelCatalog Catalog => catalog;

        public bool TryGetReservation(PlayerRef player, out int modelId) => Reservations.TryGet(player, out modelId);

        public bool TryGetResult(PlayerRef player, int requestId, out ModelSelectionResult result)
        {
            if (Results.TryGet(player, out var reply) && reply.RequestId == requestId)
            {
                result = (ModelSelectionResult)reply.Result;
                return true;
            }
            result = ModelSelectionResult.None;
            return false;
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;
            var requests = DogModelSelectionRequest.Active;
            for (var i = 0; i < requests.Count; i++)
            {
                var request = requests[i];
                if (request == null || request.Runner != Runner || request.Object == null || !request.Object.IsValid
                    || request.RequestId <= 0 || !request.Object.InputAuthority.IsRealPlayer) continue;
                var source = request.Object.InputAuthority;
                if (Results.TryGet(source, out var previous) && previous.RequestId == request.RequestId) continue;
                var result = ValidateAndReserve(source, request.RequestedModelId);
                Results.Set(source, new ModelSelectionReply
                {
                    RequestId = request.RequestId,
                    Result = (byte)result
                });
            }
        }

        private ModelSelectionResult ValidateAndReserve(PlayerRef player, int modelId)
        {
            if (!DogModelReservationRules.IsValidModel(catalog, modelId)) return ModelSelectionResult.InvalidModel;
            var active = false;
            foreach (var candidate in Runner.ActivePlayers)
            {
                if (candidate != player) continue;
                active = true;
                break;
            }
            if (!active) return ModelSelectionResult.PlayerUnavailable;
            Reservations.Set(player, modelId);
            return ModelSelectionResult.Approved;
        }

        public void PlayerLeft(PlayerRef player)
        {
            if (!Object.HasStateAuthority) return;
            Reservations.Remove(player);
            Results.Remove(player);
        }

        public static string Describe(ModelSelectionResult result)
        {
            switch (result)
            {
                case ModelSelectionResult.InvalidModel: return "This dog model is not available in this build.";
                case ModelSelectionResult.AlreadyReserved: return "That dog model can now be shared. Please retry.";
                case ModelSelectionResult.PlayerUnavailable: return "Player is no longer available. Please retry.";
                default: return "Model selection is still pending.";
            }
        }
    }
}
