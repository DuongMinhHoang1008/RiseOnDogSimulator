using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace DogSimulator.Networking
{
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform), typeof(Rigidbody))]
    public sealed class NetworkCarryable : NetworkBehaviour
    {
        private static readonly List<NetworkCarryable> ActiveItems = new();
        [SerializeField] private Vector3 carriedLocalPosition = new(0, 0, .18f);
        [SerializeField] private Vector3 carriedLocalEuler;
        [SerializeField] private bool rollsWhenFree;
        [SerializeField, Min(0)] private float rollingLinearDamping = .55f;
        [SerializeField, Min(0)] private float rollingAngularDamping = 1.8f;
        [SerializeField, Min(0)] private float nonRollingLinearDamping = 1.1f;
        [SerializeField, Min(0)] private float nonRollingAngularDamping = 2.5f;
        [SerializeField, Min(0)] private float lifetimeSeconds;
        [Networked] public NetworkId CarrierId { get; private set; }
        [Networked] public Vector3 ResetPosition { get; private set; }
        [Networked] public Quaternion ResetRotation { get; private set; }
        [Networked] public int Revision { get; private set; }
        [Networked] public NetworkId ImpactTargetId { get; private set; }
        [Networked] public Vector3 ImpactImpulse { get; private set; }
        [Networked] public int ImpactRequestId { get; private set; }
        [Networked] public NetworkBool WasThrown { get; private set; }
        [Networked] public NetworkBool ImpactForcesKnockdown { get; private set; }
        [Networked] private TickTimer DespawnAt { get; set; }
        [Networked] private float PausedLifetime { get; set; }
        private Rigidbody body;
        private Collider[] colliders;
        private Vector3 previousMouthPosition;
        private Vector3 carriedWorldVelocity;
        private bool hasMouthSample;
        private Vector3 previousFreePosition;
        private readonly Dictionary<NetworkId, float> impactCooldowns = new();
        private NetworkId recentCarrierId;
        private float recentCarrierGraceUntil;
        private Pose lastFreePose;

        public static IReadOnlyList<NetworkCarryable> Active => ActiveItems;
        public bool IsCarried => CarrierId.IsValid;
        public bool IsTemporary => lifetimeSeconds > 0;
        internal Pose WorldSavePose => IsCarried ? lastFreePose : new Pose(transform.position, transform.rotation);

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>(true);
        }

        public override void Spawned()
        {
            if (!ActiveItems.Contains(this)) ActiveItems.Add(this);
            if (Object.HasStateAuthority && IsTemporary) DespawnAt = TickTimer.CreateFromSeconds(Runner, lifetimeSeconds);
            ConfigurePhysics();
            previousFreePosition = transform.position;
        }

        public void InitializeResetPose(Vector3 position, Quaternion rotation, bool moveToResetPose = true)
        {
            if (!Object.HasStateAuthority) return;
            ResetPosition = position;
            ResetRotation = rotation;
            if (moveToResetPose) transform.SetPositionAndRotation(position, rotation);
        }

        public bool TryPickup(NetworkDogPlayer carrier)
        {
            if (!Object.HasStateAuthority || IsCarried || carrier == null || carrier.Object == null) return false;
            if (IsTemporary)
            {
                if (DespawnAt.Expired(Runner)) return false;
                PausedLifetime = DespawnAt.RemainingTime(Runner) ?? lifetimeSeconds;
                DespawnAt = TickTimer.None;
            }
            lastFreePose = new Pose(transform.position, transform.rotation);
            CarrierId = carrier.Object.Id;
            WasThrown = false;
            previousMouthPosition = carrier.MouthAnchor.position;
            carriedWorldVelocity = carrier.MovementVelocity;
            hasMouthSample = true;
            Revision++;
            ConfigurePhysics();
            return true;
        }

        public bool TryDrop(NetworkDogPlayer carrier)
        {
            if (!Object.HasStateAuthority || carrier == null || CarrierId != carrier.Object.Id) return false;
            return Release(carrier, Vector3.zero, false);
        }

        public bool TryThrow(NetworkDogPlayer carrier)
        {
            if (!Object.HasStateAuthority || carrier == null || CarrierId != carrier.Object.Id) return false;
            var inheritedVelocity = carriedWorldVelocity;
            if (Mathf.Abs(carrier.MovementVelocity.y) > Mathf.Abs(inheritedVelocity.y))
                inheritedVelocity.y = carrier.MovementVelocity.y;
            var throwVelocity = inheritedVelocity + carrier.transform.forward * 6f + Vector3.up * 2.4f;
            return Release(carrier, throwVelocity, true);
        }

        private bool Release(NetworkDogPlayer carrier, Vector3 releaseVelocity, bool thrown)
        {
            var position = carrier.FindDropPosition(this);
            recentCarrierId = carrier.Object.Id;
            recentCarrierGraceUntil = Time.unscaledTime + .45f;
            CarrierId = NetworkId.None;
            ResumeLifetime();
            WasThrown = thrown;
            transform.SetPositionAndRotation(position, transform.rotation);
            Revision++;
            ConfigurePhysics();
            body.WakeUp();
            body.linearVelocity = releaseVelocity;
            body.angularVelocity = Vector3.zero;
            hasMouthSample = false;
            return true;
        }

        public void ResetToSpawn()
        {
            if (!Object.HasStateAuthority) return;
            CarrierId = NetworkId.None;
            ResumeLifetime();
            WasThrown = false;
            hasMouthSample = false;
            transform.SetPositionAndRotation(ResetPosition, ResetRotation);
            Revision++;
            ConfigurePhysics();
            body.linearVelocity = body.angularVelocity = Vector3.zero;
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;
            if (IsTemporary && !IsCarried && DespawnAt.Expired(Runner))
            {
                Runner.Despawn(Object);
                return;
            }
            if (IsCarried)
            {
                if (!Runner.TryFindObject(CarrierId, out var carrierObject)
                    || !carrierObject.TryGetComponent<NetworkDogPlayer>(out var carrier))
                { ResetToSpawn(); return; }
                var mouth = carrier.MouthAnchor;
                if (mouth == null) { TryDrop(carrier); return; }
                var carriedPosition = mouth.TransformPoint(carriedLocalPosition);
                if (hasMouthSample && Runner.DeltaTime > 0)
                    carriedWorldVelocity = (carriedPosition - previousMouthPosition) / Runner.DeltaTime;
                else carriedWorldVelocity = carrier.MovementVelocity;
                previousMouthPosition = carriedPosition;
                hasMouthSample = true;
                transform.SetPositionAndRotation(carriedPosition,
                    mouth.rotation * Quaternion.Euler(carriedLocalEuler));
            }
            else DetectPlayerImpacts();
            if (transform.position.y < -8) ResetToSpawn();
        }

        private void ResumeLifetime()
        {
            if (!IsTemporary || DespawnAt.IsRunning) return;
            DespawnAt = TickTimer.CreateFromSeconds(Runner, Mathf.Max(Runner.DeltaTime, PausedLifetime));
            PausedLifetime = 0;
        }

        private void DetectPlayerImpacts()
        {
            var velocity = body.linearVelocity;
            var speed = velocity.magnitude;
            var start = previousFreePosition;
            var end = transform.position;
            previousFreePosition = end;
            if (speed < 1f) { WasThrown = false; return; }
            if (speed < 3.5f) return;
            var radius = Mathf.Max(.35f, colliders[0].bounds.extents.magnitude);
            foreach (var player in NetworkDogPlayer.Active)
            {
                if (player == null || player.Runner != Runner || player.Object == null) continue;
                if (player.Object.Id == recentCarrierId && Time.unscaledTime < recentCarrierGraceUntil) continue;
                if (impactCooldowns.TryGetValue(player.Object.Id, out var until) && Time.unscaledTime < until) continue;
                if (!player.IntersectsBodySweep(start, end, radius)) continue;
                var massScale = Mathf.Clamp(body.mass * .45f, .45f, 2.4f);
                ImpactTargetId = player.Object.Id;
                ImpactImpulse = Vector3.ClampMagnitude(velocity * massScale + Vector3.up * speed * .12f, 18f);
                ImpactForcesKnockdown = WasThrown;
                ImpactRequestId = unchecked(ImpactRequestId + 1);
                if (ImpactRequestId <= 0) ImpactRequestId = 1;
                impactCooldowns[player.Object.Id] = Time.unscaledTime + .35f;
                break;
            }
        }

        public override void Render()
        {
            ConfigurePhysics();
            if (!IsCarried || !Runner.TryFindObject(CarrierId, out var carrierObject)
                || !carrierObject.TryGetComponent<NetworkDogPlayer>(out var carrier)) return;
            var mouth = carrier.MouthAnchor;
            if (mouth == null) return;

            // Present carried items from the carrier pose visible on this peer. Otherwise the
            // carrier and item are interpolated as two independent network timelines, and the
            // master-authoritative item visibly trails the mouth by an extra network hop.
            transform.SetPositionAndRotation(
                mouth.TransformPoint(carriedLocalPosition),
                mouth.rotation * Quaternion.Euler(carriedLocalEuler));
        }

        private void ConfigurePhysics()
        {
            if (body == null) return;
            body.isKinematic = IsCarried || Object == null || !Object.IsValid || !Object.HasStateAuthority;
            body.useGravity = !body.isKinematic;
            body.linearDamping = rollsWhenFree ? rollingLinearDamping : nonRollingLinearDamping;
            body.angularDamping = rollsWhenFree ? rollingAngularDamping : nonRollingAngularDamping;
            body.sleepThreshold = .08f;
            foreach (var itemCollider in colliders) itemCollider.enabled = !IsCarried;
        }

        public override void Despawned(NetworkRunner runner, bool hasState) => ActiveItems.Remove(this);
        private void OnDestroy() => ActiveItems.Remove(this);
    }
}
