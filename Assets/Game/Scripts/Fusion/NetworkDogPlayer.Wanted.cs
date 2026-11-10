using System.Collections.Generic;
using DogSimulator.Player;
using Fusion;
using UnityEngine;

namespace DogSimulator.Networking
{
    public sealed partial class NetworkDogPlayer
    {
        [Networked] public float WantedHeat { get; private set; }
        [Networked] public byte WantedLevel { get; private set; }
        [Networked] public NetworkBool InJail { get; private set; }
        [Networked] public NetworkId ArrestingPolice { get; private set; }
        [Networked] private TickTimer ArrestGrace { get; set; }

        private readonly Dictionary<(NetworkId, AnnoyingAction), TickTimer> heatCooldowns = new();
        private readonly Dictionary<NetworkId, (TickTimer expires, int count)> barkStreaks = new();
        private TickTimer heatDelay;
        private TickTimer nextDecay;
        private TickTimer barkAwardCooldown;
        private int jailRespawnSerial;
        internal SandboxInteractionRegistry CitySandbox { get; set; }
        internal Vector3 LastIncidentPosition { get; private set; }
        internal TickTimer DispatchRotationUntil { get; set; }
        internal NetworkCityVehicle PoliceTargetVehicle => GetSeatedVehicle();
        internal bool IsWanted => !InJail && WantedLevel >= 2 && ArrestGrace.ExpiredOrNotRunning(Runner);
        internal bool IsArrestable => IsWanted && KnockdownActive && SettledDown && motor.IsGrounded
            && motor.WorldVelocity.sqrMagnitude <= knockdownSettleSpeed * knockdownSettleSpeed
            && !CarrierId.IsValid && !SeatedVehicleId.IsValid;

        internal void RecordBark(NetworkCityPedestrian witness)
        {
            if (CitySandbox == null || InJail) return;
            var id = witness.Object.Id;
            barkStreaks.TryGetValue(id, out var streak);
            var count = streak.expires.ExpiredOrNotRunning(Runner) ? 1 : streak.count + 1;
            barkStreaks[id] = (TickTimer.CreateFromSeconds(Runner, AnnoyingRules.BarkWindow),
                count >= AnnoyingRules.BarksToAnnoy ? 0 : count);
            if (count < AnnoyingRules.BarksToAnnoy || !barkAwardCooldown.ExpiredOrNotRunning(Runner)) return;
            barkAwardCooldown = TickTimer.CreateFromSeconds(Runner, AnnoyingRules.PairCooldown);
            RecordIncident(witness, AnnoyingAction.Bark);
        }

        internal void RecordIncident(NetworkCityPedestrian witness, AnnoyingAction action)
        {
            if (!Object.HasStateAuthority || CitySandbox == null || InJail) return;
            var key = (witness.Object.Id, action);
            if (heatCooldowns.TryGetValue(key, out var cooldown) && !cooldown.ExpiredOrNotRunning(Runner)) return;
            heatCooldowns[key] = TickTimer.CreateFromSeconds(Runner, AnnoyingRules.PairCooldown);
            WantedHeat = Mathf.Min(100, WantedHeat + AnnoyingRules.Points(action));
            WantedLevel = AnnoyingRules.Level(WantedHeat, WantedLevel);
            LastIncidentPosition = transform.position;
            ObservePursuit();
            if (CitySandbox.LogWantedEvents)
                Debug.Log($"Wanted {Object.Id}: {action} at NPC {witness.Object.Id}, heat {WantedHeat}, tick {Runner.Tick}.", this);
        }

        internal void ObservePursuit()
        {
            heatDelay = TickTimer.CreateFromSeconds(Runner, AnnoyingRules.HeatDelay);
        }

        internal void DecayHeat()
        {
            if (InJail || WantedHeat <= 0 || !heatDelay.ExpiredOrNotRunning(Runner)
                || !nextDecay.ExpiredOrNotRunning(Runner)) return;
            nextDecay = TickTimer.CreateFromSeconds(Runner, 1f);
            WantedHeat = Mathf.Max(0, WantedHeat - AnnoyingRules.DecayPerSecond);
            WantedLevel = AnnoyingRules.Level(WantedHeat, WantedLevel);
        }

        internal bool ClaimArrest(NetworkId police)
        {
            if (!IsArrestable || (ArrestingPolice.IsValid && ArrestingPolice != police)) return false;
            ArrestingPolice = police;
            return true;
        }

        internal void CancelArrest(NetworkId police)
        {
            if (ArrestingPolice == police) ArrestingPolice = NetworkId.None;
        }

        internal bool EnterJail(NetworkId police, Vector3 position)
        {
            if (!Object.HasStateAuthority || !IsArrestable || ArrestingPolice != police) return false;
            if (!TryReleaseForPolice()) return false;
            ArrestingPolice = NetworkId.None;
            InJail = true;
            ReturnToCell(position);
            return true;
        }

        internal void ApplyPoliceTrip(Vector3 impulse)
        {
            if (IsWanted && TryReleaseForPolice()) ApplyWorldImpact(impulse, true);
        }

        private bool TryReleaseForPolice()
        {
            var held = FindHeldTarget();
            if (held == null) return true;
            // Preserve the mission item and its alert if all safe drop poses are blocked.
            if (held is NetworkCarryable prop && CitySandbox != null && prop.Object.Id == CitySandbox.GoldenBoneId)
                return prop.TryDrop(this);
            ReleaseHeldTarget();
            return FindHeldTarget() == null;
        }

        internal void ReturnToCell(Vector3 position)
        {
            KnockdownActive = SettledDown = false;
            KnockdownUntil = TickTimer.None;
            stableDownSeconds = 0;
            animations.ClearAction();
            AnimationAction = DogAction.None;
            AnimationSequence = animations.Sequence;
            AnimationUntil = TickTimer.None;
            AnimationSpeed = 0;
            motor.ClearMotion();
            MovementVelocity = Vector3.zero;
            CaptureStackState();
            transformSync.Teleport(position, Quaternion.identity);
            jailRespawnSerial = RespawnSerial;
        }

        internal bool RespawnedInJail => jailRespawnSerial != RespawnSerial;

        internal void LeaveJail()
        {
            InJail = false;
            WantedHeat = WantedLevel = 0;
            heatCooldowns.Clear();
            barkStreaks.Clear();
            barkAwardCooldown = TickTimer.None;
            ArrestGrace = TickTimer.CreateFromSeconds(Runner, AnnoyingRules.EscapeGrace);
        }
    }
}
