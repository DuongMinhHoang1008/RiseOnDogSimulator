using System.Collections.Generic;
using DogSimulator.Player;
using Fusion;
using UnityEngine;

namespace DogSimulator.Networking
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed partial class SandboxInteractionRegistry : NetworkBehaviour, IPlayerLeft
    {
        [SerializeField] private GameObject[] carryablePrefabs;
        [SerializeField] private Vector3[] spawnPositions;
        [SerializeField] private NetworkObject poopPrefab;
        [Networked, Capacity(20)] private NetworkDictionary<PlayerRef, int> ProcessedRequests => default;
        [Networked] private NetworkBool ContentSpawned { get; set; }
        [Networked] public PlayerRef RoomOwner { get; private set; }
        [Networked] public NetworkBool RoomClosing { get; private set; }
        [Networked] public NetworkId GoldenBoneId { get; private set; }
        [Networked] public NetworkBool GoldenBoneComplete { get; private set; }
        [Networked] public NetworkBool GoldenBoneAlert { get; private set; }
        [Networked] public NetworkBool GateOpen { get; private set; }
        [Networked] public NetworkBool PlatePressed { get; private set; }
        private NetworkCarryable missionBone;
        private NetworkCarryable missionCrate;
        private readonly List<NetworkCityPedestrian> missionGuards = new();
        private Vector3 previousBonePosition;
        private int observedBoneReset;
        private int observedBoneRelease;
        private int observedCarrierRespawn;
        private NetworkId observedCarrier;
        private bool resetMissionThisTick;
        private bool presentedGate;
        private bool presentedPlate;

        internal bool CanDispatchGuards => GoldenBoneAlert && missionBone != null && observedBoneReset == missionBone.ResetSerial;

        internal void BindCity(SimplePolyCityWorld world) => city = world;
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
            resetMissionThisTick = false;
            var players = NetworkDogPlayer.Active;
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null || player.Runner != Runner || player.Object == null || !player.Object.IsValid) continue;
                if (jailBowl != null) player.CitySandbox = this;
                UpdatePrisoner(player);
                ProcessRequest(player);
                SpawnPoopIfDue(player);
            }
            UpdateJail();
            DispatchPolice();
            foreach (var player in players)
                if (player.Runner == Runner && player.Object.IsValid) player.DecayHeat();
            UpdateMission();
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
            SpawnJail();
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
            if (city.Course != null) SpawnMission();
        }

        private void SpawnMission()
        {
            missionBone = SpawnProp(city.GoldenBonePrefab, GoldenBoneCourse.Pedestal);
            missionCrate = SpawnProp(city.MissionCratePrefab, GoldenBoneCourse.CrateSpawn);
            GoldenBoneId = missionBone.Object.Id;
            previousBonePosition = GoldenBoneCourse.Pedestal;
            observedBoneReset = missionBone.ResetSerial;
            foreach (var route in GoldenBoneCourse.GuardPatrols)
            {
                var guard = new SimplePolyCityWorld.PedestrianPlacement
                {
                    prefab = city.Pedestrians[0].prefab,
                    start = route.start,
                    end = route.end
                };
                var facing = Quaternion.LookRotation(route.end - route.start);
                var npc = Runner.Spawn(guard.prefab, guard.start, facing,
                    onBeforeSpawned: (_, obj) => obj.GetComponent<NetworkCityPedestrian>().Initialize(guard))
                    .GetComponent<NetworkCityPedestrian>();
                npc.AssignGuard(this, missionBone);
                missionGuards.Add(npc);
            }
            city.Course.Present(false, false);

            NetworkCarryable SpawnProp(NetworkObject prefab, Vector3 position)
            {
                var prop = Runner.Spawn(prefab, position, Quaternion.identity).GetComponent<NetworkCarryable>();
                prop.InitializeResetPose(position, Quaternion.identity);
                return prop;
            }
        }

        private void UpdateMission()
        {
            if (missionBone == null) return;
            PlatePressed = !resetMissionThisTick && city.Course.IsPlatePressed();
            if (PlatePressed || GuardNeedsGate()) GateOpen = true;
            else if (GateOpen && city.Course.GatePathClear()) GateOpen = false;
            PresentCourse();

            var position = missionBone.GetMissionPosition(out var carrierRespawn);
            var teleported = observedBoneReset != missionBone.ResetSerial;
            var returning = missionBone.IsPedestrianCarried;
            if (teleported) GoldenBoneAlert = false;
            if (!GoldenBoneComplete && missionBone.IsCarried && !returning) GoldenBoneAlert = true;
            if (missionBone.CarrierId.IsValid &&
                (observedCarrier != missionBone.CarrierId || observedCarrierRespawn != carrierRespawn))
                previousBonePosition = position;
            observedCarrier = missionBone.CarrierId;
            observedCarrierRespawn = carrierRespawn;
            observedBoneReset = missionBone.ResetSerial;
            if (observedBoneRelease != missionBone.ReleaseSerial)
            {
                observedBoneRelease = missionBone.ReleaseSerial;
                previousBonePosition = missionBone.LastReleasePosition;
            }
            if (!teleported && !resetMissionThisTick && !returning && !GoldenBoneComplete
                && ReachesDelivery(previousBonePosition, position, GoldenBoneCourse.Delivery))
            {
                GoldenBoneComplete = true;
                GoldenBoneAlert = false;
            }
            previousBonePosition = position;
        }

        private bool GuardNeedsGate()
        {
            if (!CanDispatchGuards || !missionBone.IsPedestrianCarried
                || !Runner.TryFindObject(missionBone.CarrierId, out var guard)) return false;
            return (guard.transform.position - GoldenBoneCourse.GateClosed).sqrMagnitude < 36f;
        }

        internal static bool ReachesDelivery(Vector3 from, Vector3 to, Bounds zone)
        {
            if (zone.Contains(from) || zone.Contains(to)) return true;
            var displacement = to - from;
            return displacement.sqrMagnitude > .000001f
                && zone.IntersectRay(new Ray(from, displacement.normalized), out var distance)
                && distance <= displacement.magnitude;
        }

        private void ResetMission()
        {
            if (missionBone == null) return;
            resetMissionThisTick = true;
            missionBone.ResetToSpawn();
            missionCrate.ResetToSpawn();
            foreach (var guard in missionGuards) guard.ResetPatrol();
            GoldenBoneComplete = false;
            GoldenBoneAlert = false;
        }

        private void PresentCourse()
        {
            if (city?.Course == null || (presentedGate == GateOpen && presentedPlate == PlatePressed)) return;
            city.Course.Present(PlatePressed, GateOpen);
            presentedGate = GateOpen;
            presentedPlate = PlatePressed;
        }

        public override void Render()
        {
            PresentJail();
            PresentCourse();
            if (!GoldenBoneId.IsValid || !Runner.TryFindObject(GoldenBoneId, out var bone)) return;
            if (bone.TryGetComponent<NetworkCarryable>(out var carryable)) carryable.PresentGoldenBone();
        }

        internal static event System.Action<SandboxInteractionRegistry> RegistrySpawned;

        public override void Spawned() => RegistrySpawned?.Invoke(this);

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            city?.Course?.Present(false, false);
            jail?.Dispose();
            jail = null;
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
            var kind = player.InteractionKind;
            if (kind == SandboxInteractionKind.UseJail)
            {
                UseJail(player);
                return;
            }
            if (kind is SandboxInteractionKind.ResetYard or SandboxInteractionKind.ResetMission or SandboxInteractionKind.RespawnBone)
            {
                ProcessResetRequest(source, kind);
                return;
            }
            if (!player.InteractionTarget.IsValid || !Runner.TryFindObject(player.InteractionTarget, out var target)
                || !target.HasStateAuthority || player.KnockdownActive || player.SeatedVehicleId.IsValid) return;
            if (resetMissionThisTick && (target == missionBone?.Object || target == missionCrate?.Object
                || (target.TryGetComponent<NetworkCityPedestrian>(out var guard) && missionGuards.Contains(guard)))) return;
            if (target.TryGetComponent<NetworkCarryable>(out var carryable))
            {
                if (kind == SandboxInteractionKind.Drop) carryable.TryDrop(player);
                else if (kind == SandboxInteractionKind.Throw) carryable.TryThrow(player);
                else if (kind == SandboxInteractionKind.Pickup) carryable.TryPickup(player);
            }
            else if (target.TryGetComponent<NetworkCityPedestrian>(out var npc))
            {
                if (kind == SandboxInteractionKind.Pickup) npc.TryPickup(player);
                else if (kind == SandboxInteractionKind.Drop || kind == SandboxInteractionKind.Throw)
                    npc.Release(player, kind == SandboxInteractionKind.Throw);
            }
            else if (target.TryGetComponent<NetworkDogPlayer>(out var dog))
            {
                if (kind == SandboxInteractionKind.Pickup) dog.TryBePickedUp(player);
                else if (kind == SandboxInteractionKind.Drop) dog.ReleaseFromCarrier(player, false);
            }
        }

        private void ProcessResetRequest(PlayerRef source, SandboxInteractionKind kind)
        {
            if (source != RoomOwner && Runner.GameMode != GameMode.Single) return;
            if (kind == SandboxInteractionKind.RespawnBone)
            {
                missionBone?.ResetToSpawn();
                return;
            }

            ResetMission();
            if (kind == SandboxInteractionKind.ResetMission) return;

            jailBowl?.ResetToSpawn();
            foreach (var dog in NetworkDogPlayer.Active)
                if (dog.Runner == Runner && dog.Object.HasStateAuthority) dog.ReleaseHeldTarget();
            foreach (var vehicle in NetworkCityVehicle.Active)
                if (vehicle.Runner == Runner && vehicle.Object.HasStateAuthority) vehicle.ResetVehicle();
            foreach (var pedestrian in NetworkCityPedestrian.Active)
                if (!pedestrian.IsPolice && !missionGuards.Contains(pedestrian)
                    && pedestrian.Runner == Runner && pedestrian.Object.HasStateAuthority)
                    pedestrian.ResetPatrol();
            for (var i = NetworkCarryable.Active.Count - 1; i >= 0; i--)
            {
                var item = NetworkCarryable.Active[i];
                if (item == null || item == jailBowl || item == missionBone || item == missionCrate
                    || item.Runner != Runner || !item.Object.HasStateAuthority) continue;
                if (item.IsTemporary)
                {
                    if (!item.IsCarried) Runner.Despawn(item.Object);
                }
                else item.ResetToSpawn();
            }
            SaveWorld();
        }

        public void PlayerLeft(PlayerRef player)
        {
            if (!Object.HasStateAuthority) return;
            ProcessedRequests.Remove(player);
            poopEffects.Remove(player);
        }
    }
}
