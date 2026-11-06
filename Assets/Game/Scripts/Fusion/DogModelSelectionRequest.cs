using System.Collections.Generic;
using Fusion;

namespace DogSimulator.Networking
{
    public sealed class DogModelSelectionRequest : NetworkBehaviour
    {
        private static readonly List<DogModelSelectionRequest> ActiveRequests = new();

        [Networked] public int RequestedModelId { get; private set; }
        [Networked] public int RequestId { get; private set; }

        public static IReadOnlyList<DogModelSelectionRequest> Active => ActiveRequests;

        public override void Spawned()
        {
            if (!ActiveRequests.Contains(this)) ActiveRequests.Add(this);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            ActiveRequests.Remove(this);
        }

        private void OnDestroy()
        {
            ActiveRequests.Remove(this);
        }

        public bool Submit(int modelId, int requestId)
        {
            if (Object == null || !Object.IsValid || !Object.HasStateAuthority || requestId <= 0) return false;
            RequestedModelId = modelId;
            RequestId = requestId;
            return true;
        }
    }
}
