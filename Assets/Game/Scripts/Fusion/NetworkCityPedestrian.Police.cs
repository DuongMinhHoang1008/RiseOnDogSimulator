using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

namespace DogSimulator.Networking
{
    public sealed partial class NetworkCityPedestrian
    {
        [Networked] internal NetworkBool IsPolice { get; private set; }
        [Networked] internal NetworkId PoliceTarget { get; private set; }
        [Networked] private byte PoliceMode { get; set; }
        private const byte Pursuing = 1, Searching = 2, Arresting = 3, Returning = 4;
        private const float ResponseRange = 125f;
        private const float SightRange = 28f;
        private const float ArrestReach = 1.75f;
        private NavMeshPath policePath;
        private Vector3 lastSeenPosition;
        private TickTimer searchUntil;
        private TickTimer assignmentUntil;
        private TickTimer availableAgain;
        private TickTimer arrestUntil;
        private TickTimer nextPoliceCheck;
        private bool seesTarget;
        [SerializeField] private TMP_Text policeMarker;
        private byte renderedPoliceMode = byte.MaxValue;
        [SerializeField] private AudioSource policeAudio;
        private AudioClip siren;

        internal void InitializePolice(SimplePolyCityWorld.PedestrianPlacement placement)
        {
            Initialize(placement);
            IsPolice = true;
        }

        private bool CanWalk => IsStanding && agent != null && agent.enabled && agent.isOnNavMesh;
        internal bool CanTakePoliceOrder => CanWalk && !PoliceTarget.IsValid
            && availableAgain.ExpiredOrNotRunning(Runner);

        internal bool CanRespondTo(NetworkDogPlayer dog)
            => (dog.LastIncidentPosition - transform.position).sqrMagnitude <= ResponseRange * ResponseRange
                && FindPolicePath(dog.LastIncidentPosition);

        internal void AssignPolice(NetworkDogPlayer dog)
        {
            PoliceTarget = dog.Object.Id;
            PoliceMode = Searching;
            lastSeenPosition = dog.LastIncidentPosition;
            searchUntil = TickTimer.None;
            assignmentUntil = TickTimer.CreateFromSeconds(Runner, AnnoyingRules.AssignmentSeconds);
            nextPoliceCheck = TickTimer.None;
        }

        internal void UpdatePolice(SandboxInteractionRegistry sandbox)
        {
            NetworkDogPlayer dog = null;
            if (PoliceTarget.IsValid && Runner.TryFindObject(PoliceTarget, out var target))
                target.TryGetComponent(out dog);

            if (!CanWalk || dog == null || !dog.IsWanted)
            {
                if (PoliceTarget.IsValid) FinishAssignment(dog);
                if (CanWalk) ReturnToPost();
                return;
            }

            if (!FlinchUntil.ExpiredOrNotRunning(Runner))
            {
                CancelArrest(dog);
                agent.isStopped = true;
                return;
            }

            var offset = dog.transform.position - transform.position;
            var inReach = offset.sqrMagnitude <= ArrestReach * ArrestReach
                && HasLineOfSight(Chest, dog.transform.position + Vector3.up * .55f, dog.transform);
            if (inReach && dog.IsArrestable && dog.ClaimArrest(Object.Id))
            {
                agent.isStopped = true;
                Face(Vector3.ProjectOnPlane(offset, Vector3.up));
                if (PoliceMode != Arresting)
                {
                    PoliceMode = Arresting;
                    arrestUntil = TickTimer.CreateFromSeconds(Runner, AnnoyingRules.ArrestSeconds);
                }
                if (arrestUntil.Expired(Runner) && sandbox.TryArrest(this, dog)) FinishAssignment(dog);
                return;
            }
            CancelArrest(dog);

            if (!nextPoliceCheck.ExpiredOrNotRunning(Runner)) return;
            nextPoliceCheck = TickTimer.CreateFromSeconds(Runner, .25f);
            if (assignmentUntil.Expired(Runner) && sandbox.HasUnservedWantedDog(dog))
            {
                dog.DispatchRotationUntil = TickTimer.CreateFromSeconds(Runner, AnnoyingRules.DispatchRotationSeconds);
                FinishAssignment(dog);
                return;
            }
            var vehicle = dog.PoliceTargetVehicle;
            var approach = vehicle != null ? vehicle.DriverDoor.position : dog.transform.position;
            var visible = (approach - transform.position).sqrMagnitude <= SightRange * SightRange
                && HasLineOfSight(Chest, vehicle != null ? approach : approach + Vector3.up * .55f,
                    vehicle != null ? vehicle.transform : dog.transform);
            if (visible)
            {
                seesTarget = true;
                PoliceMode = Pursuing;
                lastSeenPosition = approach;
                searchUntil = TickTimer.CreateFromSeconds(Runner, AnnoyingRules.SearchSeconds);
                dog.ObservePursuit();
                if (vehicle != null && (approach - transform.position).sqrMagnitude <= ArrestReach * ArrestReach
                    && vehicle.TryEndDriveForPolice(dog))
                    agent.isStopped = true;
                else if (inReach && !dog.KnockdownActive && !dog.CarrierId.IsValid && !dog.SeatedVehicleId.IsValid)
                    AttemptPoliceTrip(dog, offset);
                else MovePolice(lastSeenPosition, 5.2f);
                return;
            }

            PoliceMode = Searching;
            if (!MovePolice(lastSeenPosition, 4.6f) || (transform.position - lastSeenPosition).sqrMagnitude < 2.25f)
            {
                if (!searchUntil.IsRunning)
                    searchUntil = TickTimer.CreateFromSeconds(Runner, AnnoyingRules.SearchSeconds);
            }
            if (searchUntil.Expired(Runner) || (!seesTarget && assignmentUntil.Expired(Runner)))
                FinishAssignment(dog);
        }

        private void AttemptPoliceTrip(NetworkDogPlayer dog, Vector3 offset)
        {
            agent.isStopped = true;
            var direction = Vector3.ProjectOnPlane(offset, Vector3.up).normalized;
            Face(direction);
            if (swingAt.Expired(Runner))
            {
                swingAt = TickTimer.None;
                dog.ApplyPoliceTrip(direction * 2.4f + Vector3.up * .35f);
            }
            if (!nextAttack.ExpiredOrNotRunning(Runner)) return;
            AttackUntil = TickTimer.CreateFromSeconds(Runner, AttackDuration);
            swingAt = TickTimer.CreateFromSeconds(Runner, Windup);
            nextAttack = TickTimer.CreateFromSeconds(Runner, 1.6f);
        }

        private void CancelArrest(NetworkDogPlayer dog)
        {
            dog?.CancelArrest(Object.Id);
            if (PoliceMode == Arresting) PoliceMode = Pursuing;
        }

        private void FinishAssignment(NetworkDogPlayer dog)
        {
            CancelArrest(dog);
            PoliceTarget = NetworkId.None;
            PoliceMode = Returning;
            seesTarget = false;
            AttackUntil = swingAt = TickTimer.None;
            availableAgain = TickTimer.CreateFromSeconds(Runner, 3f);
        }

        private void ReturnToPost()
        {
            if (!nextPoliceCheck.ExpiredOrNotRunning(Runner)) return;
            nextPoliceCheck = TickTimer.CreateFromSeconds(Runner, .5f);
            if ((transform.position - patrol.start).sqrMagnitude < 1.4f)
            {
                agent.isStopped = true;
                PoliceMode = 0;
            }
            else MovePolice(patrol.start, 3.1f);
        }

        private bool FindPolicePath(Vector3 destination)
            => NavMesh.SamplePosition(destination, out var point, 2f, agent.areaMask)
                && agent.CalculatePath(point.position, policePath) && policePath.status == NavMeshPathStatus.PathComplete;

        private bool MovePolice(Vector3 destination, float speed)
        {
            if (!FindPolicePath(destination))
            {
                agent.isStopped = true;
                return false;
            }
            agent.isStopped = false;
            agent.speed = speed;
            return agent.SetPath(policePath);
        }

        private void PresentPolice()
        {
            if (!IsPolice) return;
            if (Camera.main is Camera camera) policeMarker.transform.rotation = camera.transform.rotation;
            if (renderedPoliceMode == PoliceMode) return;
            if (PoliceMode == Pursuing && renderedPoliceMode != Arresting) PlaySiren();
            renderedPoliceMode = PoliceMode;
            policeMarker.text = PoliceMode switch { Pursuing => "!", Searching => "?", Arresting => "!", _ => "POLICE" };
            policeMarker.color = PoliceMode == Pursuing || PoliceMode == Arresting
                ? new Color(1f, .36f, .16f) : new Color(.35f, .75f, 1f);
        }

        private void PlaySiren()
        {
            if (siren == null)
            {
                const int rate = 16000;
                var samples = new float[rate / 2];
                var phase = 0f;
                for (var i = 0; i < samples.Length; i++)
                {
                    var t = (float)i / samples.Length;
                    phase += (530f + 360f * Mathf.Sin(Mathf.PI * t)) * (2f * Mathf.PI / rate);
                    samples[i] = .2f * Mathf.Sin(phase) * Mathf.Sin(Mathf.PI * t);
                }
                siren = AudioClip.Create("Police call", samples.Length, 1, rate, false);
                siren.SetData(samples, 0);
            }
            if (!policeAudio.isPlaying) policeAudio.PlayOneShot(siren);
        }
    }
}
