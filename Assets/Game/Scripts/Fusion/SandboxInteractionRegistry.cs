using Fusion;
using UnityEngine;

namespace DogSimulator.Networking
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class SandboxInteractionRegistry : NetworkBehaviour, IPlayerLeft
    {
        [SerializeField] private GameObject[] carryablePrefabs;
        [SerializeField] private Vector3[] spawnPositions;
        [Networked, Capacity(20)] private NetworkDictionary<PlayerRef, int> ProcessedRequests => default;
        [Networked] private NetworkBool ContentSpawned { get; set; }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;
            if (!ContentSpawned) SpawnContent();
            var players = NetworkDogPlayer.Active;
            for (var i = 0; i < players.Count; i++) ProcessRequest(players[i]);
        }

        private void SpawnContent()
        {
            ContentSpawned = true;
            var count = Mathf.Min(carryablePrefabs?.Length ?? 0, spawnPositions?.Length ?? 0);
            for (var i = 0; i < count; i++)
            {
                if (carryablePrefabs[i] == null) continue;
                var spawned = Runner.Spawn(carryablePrefabs[i], spawnPositions[i], Quaternion.identity);
                spawned?.GetComponent<NetworkCarryable>()?.InitializeResetPose(spawnPositions[i], Quaternion.identity);
            }
        }

        private void ProcessRequest(NetworkDogPlayer player)
        {
            if (player == null || player.Runner != Runner || player.Object == null || !player.Object.IsValid
                || player.InteractionRequestId <= 0) return;
            var source = player.Object.StateAuthority;
            if (ProcessedRequests.TryGet(source, out var processed) && processed >= player.InteractionRequestId) return;
            ProcessedRequests.Set(source, player.InteractionRequestId);
            if (player.InteractionKind == SandboxInteractionKind.ResetYard)
            {
                for (var i = 0; i < NetworkCarryable.Active.Count; i++)
                {
                    var item = NetworkCarryable.Active[i];
                    if (item != null && item.Runner == Runner && item.Object.HasStateAuthority) item.ResetToSpawn();
                }
                return;
            }
            if (!player.InteractionTarget.IsValid || !Runner.TryFindObject(player.InteractionTarget, out var target)
                || !target.TryGetComponent<NetworkCarryable>(out var carryable) || !carryable.Object.HasStateAuthority) return;
            if (player.InteractionKind == SandboxInteractionKind.Drop) carryable.TryDrop(player);
            else if (player.InteractionKind == SandboxInteractionKind.Throw) carryable.TryThrow(player);
            else if (player.InteractionKind == SandboxInteractionKind.Pickup
                && Vector3.Distance(player.MouthAnchor.position, carryable.transform.position) <= player.CarryRange)
                carryable.TryPickup(player);
        }

        public void PlayerLeft(PlayerRef player)
        {
            if (Object.HasStateAuthority) ProcessedRequests.Remove(player);
        }
    }
}
