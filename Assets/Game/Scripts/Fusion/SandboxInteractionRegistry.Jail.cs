using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

namespace DogSimulator.Networking
{
    public sealed partial class SandboxInteractionRegistry
    {
        [Networked] private NetworkBool JailReady { get; set; }
        [Networked] private NetworkBool JailOccupied { get; set; }
        [Networked] private Vector3 JailPosition { get; set; }
        [Networked] internal byte JailGate { get; private set; }
        [Networked] private TickTimer JailGateUntil { get; set; }
        [SerializeField] private bool logWantedEvents;
        internal bool LogWantedEvents => logWantedEvents;
        private const byte OpenByPuzzle = 1, OpenByRescue = 2, ClosingBlocked = 3;
        private DogJail jail;
        private NetworkCarryable jailBowl;
        private int bowlRelease;
        private int bowlReset;
        private bool bowlArmed;
        private readonly Dictionary<NetworkId, Vector3> prisoners = new();
        private readonly List<NetworkCityPedestrian> police = new(AnnoyingRules.PoliceCount);
        private TickTimer nextDispatch;
        private readonly List<NetworkId> departedPrisoners = new();

        private void SpawnJail()
        {
            if (city.MissionCratePrefab == null || city.Pedestrians.Length == 0) return;
            if (!DogJail.TryFindSite(city, out var position))
            {
                Debug.LogError("Dog Jail could not find a clear City site. Check the plaza, NavMesh and vehicle routes.", city);
                return;
            }
            JailPosition = position;
            JailReady = true;
            PresentJail();
            var bowl = Runner.Spawn(city.MissionCratePrefab, jail.BowlSpawn, Quaternion.identity,
                onBeforeSpawned: (_, obj) => obj.GetComponent<NetworkCarryable>().IsJailBowl = true);
            if (bowl == null || !bowl.TryGetComponent(out jailBowl))
            {
                Debug.LogError("Dog Jail could not spawn its carryable puzzle prop.", city);
                return;
            }
            jailBowl.InitializeResetPose(jail.BowlSpawn, Quaternion.identity);
            for (var i = 0; i < AnnoyingRules.PoliceCount; i++)
            {
                var entry = city.Pedestrians[i % city.Pedestrians.Length];
                var home = i == 0 ? position + new Vector3(-3f, 0, 7f) : new Vector3(94f, .08f, 134f);
                if (NavMesh.SamplePosition(home, out var nav, 2.5f, NavMesh.AllAreas)) entry.start = nav.position;
                var officer = Runner.Spawn(entry.prefab, entry.start, Quaternion.identity,
                    onBeforeSpawned: (_, obj) => obj.GetComponent<NetworkCityPedestrian>().InitializePolice(entry));
                if (officer != null) police.Add(officer.GetComponent<NetworkCityPedestrian>());
            }
        }

        private void PresentJail()
        {
            if (!JailReady || city == null) return;
            if (jail == null)
            {
                jail = city.Jail;
                jail.Place(JailPosition);
            }
            jail.Present(JailGate != 0);
        }

        internal bool CanUseJailLever(NetworkDogPlayer dog) => JailOccupied && jail != null && jail.CanReachLever(dog);
        internal bool RescueOpenedGate => JailGate == OpenByRescue;

        private void UseJail(NetworkDogPlayer dog)
        {
            if (jailBowl == null || dog.KnockdownActive || dog.CarrierId.IsValid || dog.SeatedVehicleId.IsValid) return;
            if (dog.InJail)
            {
                if (jail.Contains(dog.transform.position) && !jailBowl.IsCarried) jailBowl.ResetToSpawn();
                return;
            }
            if (CanUseJailLever(dog)) OpenJail(OpenByRescue);
        }

        private void OpenJail(byte reason)
        {
            if (JailGate is OpenByPuzzle or OpenByRescue) return;
            JailGate = reason;
            JailGateUntil = TickTimer.CreateFromSeconds(Runner, AnnoyingRules.GateSeconds);
            PresentJail();
            if (LogWantedEvents) Debug.Log($"Jail gate opened with reason {reason} at tick {Runner.Tick}.", this);
        }

        private void UpdateJail()
        {
            if (jailBowl == null) return;
            foreach (var prisoner in prisoners)
                if (!Runner.TryFindObject(prisoner.Key, out _)) departedPrisoners.Add(prisoner.Key);
            foreach (var departed in departedPrisoners) prisoners.Remove(departed);
            departedPrisoners.Clear();
            JailOccupied = prisoners.Count != 0;
            var position = jailBowl.transform.position;
            if (!jail.Contains(position) || (jailBowl.IsCarried &&
                (!Runner.TryFindObject(jailBowl.CarrierId, out var carrier) || !jail.Contains(carrier.transform.position))))
                jailBowl.ResetToSpawn();

            if (bowlReset != jailBowl.ResetSerial)
            {
                bowlReset = jailBowl.ResetSerial;
                bowlRelease = jailBowl.ReleaseSerial;
                bowlArmed = false;
            }
            if (bowlRelease != jailBowl.ReleaseSerial)
            {
                bowlRelease = jailBowl.ReleaseSerial;
                bowlArmed = prisoners.ContainsKey(jailBowl.LastReleaseActor);
            }
            if (bowlArmed && !jailBowl.IsCarried && jailBowl.IsResting && jail.BowlOnPlate(position))
            {
                bowlArmed = false;
                OpenJail(OpenByPuzzle);
            }
            if (JailGate == 0 || !JailGateUntil.Expired(Runner)) return;
            JailGate = IsDoorwayOccupied() ? ClosingBlocked : (byte)0;
            PresentJail();
        }

        private bool IsDoorwayOccupied()
        {
            foreach (var dog in NetworkDogPlayer.Active)
                if (dog.Runner == Runner && jail.DogBlocksGate(dog.transform.position)) return true;
            return jail.GateBlocked();
        }

        private void UpdatePrisoner(NetworkDogPlayer dog)
        {
            if (!dog.InJail || jail == null) return;
            var id = dog.Object.Id;
            var current = dog.transform.position;
            if (!dog.RespawnedInJail && jail.Contains(current))
            {
                prisoners[id] = current;
                return;
            }
            if (!dog.RespawnedInJail && JailGate != 0 && prisoners.TryGetValue(id, out var previous)
                && jail.CrossedExit(previous, current))
            {
                if (jailBowl != null && jailBowl.CarrierId == id) jailBowl.ResetToSpawn();
                prisoners.Remove(id);
                dog.LeaveJail();
                if (LogWantedEvents) Debug.Log($"Dog {id} crossed the jail gate at tick {Runner.Tick}.", this);
                return;
            }
            dog.ReleaseHeldTarget();
            var cell = FindCell(dog);
            dog.ReturnToCell(cell);
            prisoners[id] = cell;
        }

        private Vector3 FindCell(NetworkDogPlayer dog)
        {
            var start = FusionSessionService.CalculateSpawnSlot(dog.Object.InputAuthority);
            for (var i = 0; i < 21; i++)
            {
                var point = jail.CellSpawn((start + i) % 21);
                var occupied = false;
                foreach (var occupant in prisoners)
                {
                    if (occupant.Key == dog.Object.Id || (occupant.Value - point).sqrMagnitude >= 1f) continue;
                    occupied = true;
                    break;
                }
                if (!occupied) return point;
            }
            return jail.CellSpawn(start % 21);
        }

        internal bool TryArrest(NetworkCityPedestrian officer, NetworkDogPlayer dog)
        {
            if (jailBowl == null || officer.PoliceTarget != dog.Object.Id) return false;
            var cell = FindCell(dog);
            if (!dog.EnterJail(officer.Object.Id, cell)) return false;
            prisoners[dog.Object.Id] = cell;
            JailOccupied = true;
            if (LogWantedEvents)
                Debug.Log($"Police {officer.Object.Id} arrested dog {dog.Object.Id} at tick {Runner.Tick}.", this);
            return true;
        }

        private void DispatchPolice()
        {
            if (nextDispatch.ExpiredOrNotRunning(Runner))
            {
                nextDispatch = TickTimer.CreateFromSeconds(Runner, .45f);
                foreach (var officer in police)
                {
                    if (officer == null || !officer.CanTakePoliceOrder) continue;
                    NetworkDogPlayer choice = null;
                    var bestScore = float.MinValue;
                    foreach (var dog in NetworkDogPlayer.Active)
                    {
                        if (dog.Runner != Runner || !dog.IsWanted) continue;
                        var assigned = 0;
                        foreach (var other in police)
                            if (other != null && other.PoliceTarget == dog.Object.Id) assigned++;
                        if (assigned >= (dog.WantedLevel == 3 ? 2 : 1)) continue;
                        var score = dog.WantedHeat - (officer.transform.position - dog.LastIncidentPosition).magnitude * .3f;
                        if (assigned > 0) score -= 150f;
                        if (!dog.DispatchRotationUntil.ExpiredOrNotRunning(Runner)) score -= 150f;
                        if (score <= bestScore || !officer.CanRespondTo(dog)) continue;
                        bestScore = score;
                        choice = dog;
                    }
                    if (choice == null) continue;
                    officer.AssignPolice(choice);
                    if (LogWantedEvents)
                        Debug.Log($"Police {officer.Object.Id} assigned dog {choice.Object.Id} at tick {Runner.Tick}.", this);
                }
            }
            foreach (var officer in police)
                if (officer != null) officer.UpdatePolice(this);
        }

        internal bool HasUnservedWantedDog(NetworkDogPlayer current)
        {
            foreach (var dog in NetworkDogPlayer.Active)
            {
                if (dog == current || dog.Runner != Runner || !dog.IsWanted) continue;
                var assigned = false;
                foreach (var officer in police)
                    if (officer != null && officer.PoliceTarget == dog.Object.Id) assigned = true;
                if (!assigned) return true;
            }
            return false;
        }
    }
}
