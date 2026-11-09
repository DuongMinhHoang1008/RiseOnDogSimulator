using System.Collections.Generic;
using DogSimulator.Player;
using Fusion;
using UnityEngine;

namespace DogSimulator.Networking
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class SandboxInteractionRegistry : NetworkBehaviour, IPlayerLeft
    {
        [SerializeField] private GameObject[] carryablePrefabs;
        [SerializeField] private Vector3[] spawnPositions;
        [SerializeField] private NetworkObject poopPrefab;
        [Networked, Capacity(20)] private NetworkDictionary<PlayerRef, int> ProcessedRequests => default;
        [Networked] private NetworkBool ContentSpawned { get; set; }
        [Networked] public PlayerRef RoomOwner { get; private set; }
        [Networked] public NetworkBool RoomClosing { get; private set; }
        private readonly Dictionary<PlayerRef, (int sequence, TickTimer cooldown)> poopEffects = new();
        private int worldSlot;
        private NetworkCarryable[] persistentProps;
        private WorldSaveSlots.PropPose[] savedPoses;
        private WorldSaveSlots.CityProp[] cityPoses;
        private SimplePolyCityWorld city;
        private float nextSave;

        internal void InitializeWorld(int slot, PlayerRef owner, SimplePolyCityWorld cityWorld = null)
        {
            RoomOwner = owner;
            worldSlot = slot;
            city = cityWorld;
        }

        internal void BeginClosing()
        {
            if (Object != null && Object.IsValid && Object.HasStateAuthority) RoomClosing = true;
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority || RoomClosing) return;
            if (!ContentSpawned) SpawnContent();
            var players = NetworkDogPlayer.Active;
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null || player.Runner != Runner || player.Object == null || !player.Object.IsValid) continue;
                ProcessRequest(player);
                SpawnPoopIfDue(player);
            }
            if (Time.unscaledTime < nextSave) return;
            nextSave = Time.unscaledTime + 5f;
            SaveWorld();
        }

        private void SpawnContent()
        {
            ContentSpawned = true;
            if (city != null)
            {
                SpawnCity();
                return;
            }
            var count = Mathf.Min(carryablePrefabs?.Length ?? 0, spawnPositions?.Length ?? 0);
            var loaded = WorldSaveSlots.Load(worldSlot);
            persistentProps = new NetworkCarryable[count];
            savedPoses = new WorldSaveSlots.PropPose[count];
            for (var i = 0; i < count; i++)
            {
                var pose = loaded != null && i < loaded.Length && loaded[i].IsValid
                    ? loaded[i] : new WorldSaveSlots.PropPose { position = spawnPositions[i], rotation = Quaternion.identity };
                savedPoses[i] = pose;
                if (carryablePrefabs[i] == null) continue;
                var spawned = Runner.Spawn(carryablePrefabs[i], pose.position, pose.rotation);
                var prop = spawned?.GetComponent<NetworkCarryable>();
                persistentProps[i] = prop;
                prop?.InitializeResetPose(spawnPositions[i], Quaternion.identity, false);
            }
            WorldSaveSlots.Save(worldSlot, savedPoses);
        }

        private void SpawnCity()
        {
            var loaded = new Dictionary<string, WorldSaveSlots.PropPose>();
            foreach (var entry in WorldSaveSlots.LoadCity(worldSlot) ?? System.Array.Empty<WorldSaveSlots.CityProp>())
                if (!string.IsNullOrEmpty(entry.id) && entry.pose.IsValid) loaded[entry.id] = entry.pose;

            persistentProps = new NetworkCarryable[city.Props.Length];
            cityPoses = new WorldSaveSlots.CityProp[city.Props.Length];
            for (var i = 0; i < city.Props.Length; i++)
            {
                var placement = city.Props[i];
                if (!loaded.TryGetValue(placement.id, out var pose))
                    pose = new WorldSaveSlots.PropPose { position = placement.position, rotation = placement.rotation };
                cityPoses[i] = new WorldSaveSlots.CityProp { id = placement.id, pose = pose };
                var prop = Runner.Spawn(placement.prefab, pose.position, pose.rotation)?.GetComponent<NetworkCarryable>();
                persistentProps[i] = prop;
                prop?.InitializeResetPose(placement.position, placement.rotation, false);
            }

            foreach (var entry in city.Vehicles)
            {
                var placement = entry.placement;
                Runner.Spawn(placement.prefab, placement.position, placement.rotation,
                    onBeforeSpawned: (_, obj) => obj.GetComponent<NetworkCityVehicle>().Initialize(entry));
            }
            foreach (var entry in city.Pedestrians)
            {
                var rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(entry.end - entry.start, Vector3.up));
                Runner.Spawn(entry.prefab, entry.start, rotation,
                    onBeforeSpawned: (_, obj) => obj.GetComponent<NetworkCityPedestrian>().Initialize(entry));
            }
        }

        internal void SaveWorld()
        {
            if (persistentProps == null || !WorldSaveSlots.IsValidSlot(worldSlot)) return;
            for (var i = 0; i < persistentProps.Length; i++)
            {
                var prop = persistentProps[i];
                if (prop == null || prop.Object == null || !prop.Object.IsValid) continue;
                var worldPose = prop.WorldSavePose;
                var pose = new WorldSaveSlots.PropPose { position = worldPose.position, rotation = worldPose.rotation };
                if (!pose.IsValid) continue;
                if (cityPoses != null) cityPoses[i].pose = pose;
                else savedPoses[i] = pose;
            }
            if (cityPoses != null) WorldSaveSlots.SaveCity(worldSlot, cityPoses);
            else WorldSaveSlots.Save(worldSlot, savedPoses);
        }

        private void SpawnPoopIfDue(NetworkDogPlayer player)
        {
            if (poopPrefab == null || player.SeatedVehicleId.IsValid || player.AnimationAction != DogAction.Poop
                || (player.AnimationUntil.RemainingTime(Runner) ?? float.MaxValue) > .8f) return;
            var source = player.Object.InputAuthority;
            if (poopEffects.TryGetValue(source, out var previous)
                && (previous.sequence == player.AnimationSequence || !previous.cooldown.ExpiredOrNotRunning(Runner))) return;
            poopEffects[source] = (player.AnimationSequence, TickTimer.CreateFromSeconds(Runner, DogAnimationDriver.PoopCooldownSeconds));
            var position = player.transform.position - player.transform.forward * .62f + Vector3.up * .4f;
            if (Physics.Raycast(position, Vector3.down, out var ground, 1.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                position = ground.point + Vector3.up * .15f;
            var poop = Runner.Spawn(poopPrefab, position, player.transform.rotation);
            poop?.GetComponent<NetworkCarryable>()?.InitializeResetPose(position, player.transform.rotation);
        }

        private void ProcessRequest(NetworkDogPlayer player)
        {
            if (player.InteractionRequestId <= 0) return;
            var source = player.Object.InputAuthority;
            if (ProcessedRequests.TryGet(source, out var processed) && processed >= player.InteractionRequestId) return;
            ProcessedRequests.Set(source, player.InteractionRequestId);
            if (player.InteractionKind == SandboxInteractionKind.ResetYard)
            {
                if (source != RoomOwner && Runner.GameMode != GameMode.Single) return;
                foreach (var vehicle in NetworkCityVehicle.Active)
                    if (vehicle.Runner == Runner && vehicle.Object.HasStateAuthority) vehicle.ResetVehicle();
                foreach (var pedestrian in NetworkCityPedestrian.Active)
                    if (pedestrian.Runner == Runner && pedestrian.Object.HasStateAuthority) pedestrian.ResetPatrol();
                for (var i = NetworkCarryable.Active.Count - 1; i >= 0; i--)
                {
                    var item = NetworkCarryable.Active[i];
                    if (item == null || item.Runner != Runner || !item.Object.HasStateAuthority) continue;
                    if (item.IsTemporary)
                    {
                        if (!item.IsCarried) Runner.Despawn(item.Object);
                    }
                    else item.ResetToSpawn();
                }
                SaveWorld();
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
            if (!Object.HasStateAuthority) return;
            ProcessedRequests.Remove(player);
            poopEffects.Remove(player);
        }
    }
}
