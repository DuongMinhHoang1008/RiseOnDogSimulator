using System.Collections.Generic;
using DogSimulator.Player;
using Fusion;

namespace DogSimulator.Networking
{
    public enum ModelSelectionResult : byte
    {
        None,
        Approved,
        InvalidModel,
        AlreadyReserved,
        PlayerUnavailable
    }

    public static class DogModelReservationRules
    {
        public static ModelSelectionResult TryReserve(IDictionary<PlayerRef, int> reservations,
            PlayerRef player, int modelId, DogModelCatalog catalog)
        {
            if (!player.IsRealPlayer) return ModelSelectionResult.PlayerUnavailable;
            if (catalog == null || !catalog.IsValidModelId(modelId)) return ModelSelectionResult.InvalidModel;
            reservations[player] = modelId;
            return ModelSelectionResult.Approved;
        }
    }
}
