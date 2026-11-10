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
        [SerializeField] private BoxCollider dropClearance;
        [Networked] public NetworkId CarrierId { get; private set; }
        [Networked] public Vector3 ResetPosition { get; private set; }
        [Networked] public Quaternion ResetRotation { get; private set; }
        [Networked] public int Revision { get; private set; }
        [Networked] public NetworkId ImpactTargetId { get; private set; }
        [Networked] public Vector3 ImpactImpulse { get; private set; }
        [Networked] public int ImpactRequestId { get; private set; }
        [Networked] public NetworkBool WasThrown { get; private set; }
        [Networked] internal NetworkBool IsJailBowl { get; set; }
        [Networked] public NetworkBool ImpactForcesKnockdown { get; private set; }
        [Networked] private TickTimer DespawnAt { get; set; }
        [Networked] private float PausedLifetime { get; set; }
        [SerializeField] private Rigidbody body;
        [SerializeField] private NetworkTransform transformSync;
        [SerializeField] private MeshRenderer[] goldenRenderers;
        internal float Mass => body.mass;
        [SerializeField] private Collider[] colliders;
        private Vector3 previousMouthPosition;
        private Vector3 carriedWorldVelocity;
        private bool hasMouthSample;
        private Vector3 previousFreePosition;
        private readonly Dictionary<NetworkId, float> impactCooldowns = new();
        private NetworkId recentCarrierId;
        private float recentCarrierGraceUntil;
        private Pose lastFreePose;
        private readonly Collider[] dropOverlaps = new Collider[16];
        private NetworkCityPedestrian pedestrianHit;
        private Vector3 pedestrianImpact;
        private float pedestrianSeverity;
        private readonly HashSet<NetworkId> witnessedThrow = new();
        internal NetworkId LastReleaseActor { get; private set; }
        internal bool IsResting => body.linearVelocity.sqrMagnitude < .45f * .45f;
        [SerializeField] private GameObject bowlVisual;
        [SerializeField] private Renderer crateRenderer;
        [SerializeField] private BoxCollider crateCollider;
        internal int ResetSerial { get; private set; }
        internal int ReleaseSerial { get; private set; }
        internal Vector3 LastReleasePosition { get; private set; }
        private bool goldenPresented;
        internal bool IsPedestrianCarried => IsCarried && Runner.TryFindObject(CarrierId, out var carrier)
            && carrier.GetComponent<NetworkCityPedestrian>() != null;

        internal bool CanBeRecovered => !IsCarried && body.linearVelocity.sqrMagnitude < 1f
            && Physics.Raycast(body.position + Vector3.up * .12f, Vector3.down, .65f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        internal Vector3 GetMissionPosition(out int carrierRespawn)
        {
            carrierRespawn = 0;
            if (!IsCarried || !Runner.TryFindObject(CarrierId, out var carrier)) return body.position;
            if (carrier.TryGetComponent<NetworkDogPlayer>(out var dog))
            {
                carrierRespawn = dog.RespawnSerial;
                return dog.MouthAnchor.TransformPoint(carriedLocalPosition);
            }
            return carrier.TryGetComponent<NetworkCityPedestrian>(out var npc) ? npc.BoneCarryPose.position : body.position;
        }

        internal void PresentGoldenBone()
        {
            if (goldenPresented) return;
            goldenPresented = true;
            var color = new Color(1f, .62f, .035f);
            var block = new MaterialPropertyBlock();
            foreach (var mesh in goldenRenderers)
            {
                mesh.GetPropertyBlock(block);
                block.SetColor("_BaseColor", color);
                block.SetColor("_Color", color);
                mesh.SetPropertyBlock(block);
            }
        }

        public static IReadOnlyList<NetworkCarryable> Active => ActiveItems;
        public bool IsCarried => CarrierId.IsValid;
        public bool IsTemporary => lifetimeSeconds > 0;
        internal Pose WorldSavePose => IsCarried ? lastFreePose : new Pose(transform.position, transform.rotation);

        public static void ReleaseCarriedBy(NetworkRunner runner, PlayerRef player)
        {
            for (var i = ActiveItems.Count - 1; i >= 0; i--)
            {
                var item = ActiveItems[i];
                if (item == null || item.Runner != runner || !item.Object.HasStateAuthority || !item.IsCarried
                    || !runner.TryFindObject(item.CarrierId, out var carrierObject)
                    || carrierObject.InputAuthority != player) continue;
                if (carrierObject.TryGetComponent<NetworkDogPlayer>(out var carrier))
                    item.ForceDrop(carrier);
            }
        }

        public override void Spawned()
        {
            if (!ActiveItems.Contains(this)) ActiveItems.Add(this);
            if (IsJailBowl) PresentJailBowl();
            if (Object.HasStateAuthority && IsTemporary) DespawnAt = TickTimer.CreateFromSeconds(Runner, lifetimeSeconds);
            ConfigurePhysics();
            previousFreePosition = transform.position;
        }

        private void PresentJailBowl()
        {
            transform.localScale = Vector3.one;
            crateRenderer.enabled = false;
            crateCollider.size = new Vector3(.72f, .26f, .72f);
            bowlVisual.SetActive(true);
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
            if (!Object.HasStateAuthority || IsCarried || carrier == null || (IsJailBowl && !carrier.InJail)
                || !carrier.CanPickUp(this, transform.position)) return false;
            if (IsTemporary)
            {
                if (DespawnAt.Expired(Runner)) return false;
                PausedLifetime = DespawnAt.RemainingTime(Runner) ?? lifetimeSeconds;
                DespawnAt = TickTimer.None;
            }
            lastFreePose = new Pose(transform.position, transform.rotation);
            CarrierId = carrier.Object.Id;
            WasThrown = false;
            pedestrianHit = null;
            previousMouthPosition = carrier.MouthAnchor.position;
            carriedWorldVelocity = carrier.MovementVelocity;
            hasMouthSample = true;
            Revision++;
            ConfigurePhysics();
            return true;
        }

        internal bool TryRecoverBy(NetworkCityPedestrian npc)
        {
            if (!Object.HasStateAuthority || !npc.IsStanding || npc.Runner != Runner || !CanBeRecovered) return false;
            CarrierId = npc.Object.Id;
            WasThrown = false;
            pedestrianHit = null;
            hasMouthSample = false;
            Revision++;
            ConfigurePhysics();
            var pose = npc.BoneCarryPose;
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            return true;
        }

        internal void DropRecoveredBone(NetworkCityPedestrian npc)
        {
            if (!Object.HasStateAuthority || CarrierId != npc.Object.Id) return;
            var pose = npc.BoneCarryPose;
            ReleaseAt(pose.position, pose.rotation, Vector3.zero, false);
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
            var rotation = transform.rotation;
            if (dropClearance != null)
            {
                if (!TryDropPose(carrier, position, out position))
                {
                    if (thrown) return false;
                    if (!IsDropPoseClear(lastFreePose.position, lastFreePose.rotation)) return false;
                    position = lastFreePose.position;
                    rotation = lastFreePose.rotation;
                }
            }
            recentCarrierId = carrier.Object.Id;
            LastReleaseActor = recentCarrierId;
            witnessedThrow.Clear();
            recentCarrierGraceUntil = Time.unscaledTime + .45f;
            ReleaseAt(position, rotation, releaseVelocity, thrown);
            return true;
        }

        private void ReleaseAt(Vector3 position, Quaternion rotation, Vector3 releaseVelocity, bool thrown)
        {
            CarrierId = NetworkId.None;
            ResumeLifetime();
            WasThrown = thrown;
            transform.SetPositionAndRotation(position, rotation);
            body.position = position;
            body.rotation = rotation;
            previousFreePosition = position;
            LastReleasePosition = position;
            ReleaseSerial++;
            Revision++;
            ConfigurePhysics();
            body.WakeUp();
            body.linearVelocity = releaseVelocity;
            body.angularVelocity = Vector3.zero;
            hasMouthSample = false;
        }

        private bool TryDropPose(NetworkDogPlayer carrier, Vector3 preferred, out Vector3 position)
        {
            position = preferred;
            if (IsDropPoseClear(position, transform.rotation)) return true;
            var distance = dropClearance.size.magnitude * .5f + .55f;
            var side = carrier.transform.right * distance;
            position = carrier.transform.position + side + Vector3.up * .65f;
            if (IsDropPoseClear(position, transform.rotation)) return true;
            position -= 2 * side;
            return IsDropPoseClear(position, transform.rotation);
        }

        private bool IsDropPoseClear(Vector3 position, Quaternion rotation)
        {
            var center = position + rotation * dropClearance.center;
            var half = dropClearance.size * .49f;
            var count = Physics.OverlapBoxNonAlloc(center, half, dropOverlaps, rotation,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            return count == 0;
        }

        internal void ForceDrop(NetworkDogPlayer carrier)
        {
            if (!TryDrop(carrier)) ResetToSpawn();
        }

        public void ResetToSpawn()
        {
            if (!Object.HasStateAuthority) return;
            CarrierId = NetworkId.None;
            ResumeLifetime();
            ResetSerial++;
            WasThrown = false;
            pedestrianHit = null;
            hasMouthSample = false;
            transform.SetPositionAndRotation(ResetPosition, ResetRotation);
            body.position = ResetPosition;
            body.rotation = ResetRotation;
            previousFreePosition = ResetPosition;
            transformSync.Teleport(ResetPosition, ResetRotation);
            Revision++;
            ConfigurePhysics();
            body.linearVelocity = body.angularVelocity = Vector3.zero;
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;
            if (pedestrianHit != null)
            {
                var actor = witnessedThrow.Add(pedestrianHit.Object.Id) ? LastReleaseActor : NetworkId.None;
                pedestrianHit.QueueHit(pedestrianImpact, pedestrianSeverity, actor, AnnoyingAction.ThrownProp);
                pedestrianHit = null;
            }
            if (IsTemporary && !IsCarried && DespawnAt.Expired(Runner))
            {
                Runner.Despawn(Object);
                return;
            }
            if (IsCarried)
            {
                if (!Runner.TryFindObject(CarrierId, out var carrierObject))
                { ResetToSpawn(); return; }
                if (carrierObject.TryGetComponent<NetworkCityPedestrian>(out var npc))
                {
                    var pose = npc.BoneCarryPose;
                    transform.SetPositionAndRotation(pose.position, pose.rotation);
                    return;
                }
                if (!carrierObject.TryGetComponent<NetworkDogPlayer>(out var carrier))
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

        private void OnCollisionEnter(Collision collision)
        {
            if (Object == null || !Object.IsValid || !Object.HasStateAuthority || !WasThrown || IsCarried
                || collision.relativeVelocity.sqrMagnitude < 4f) return;
            var npc = collision.collider.GetComponentInParent<NetworkCityPedestrian>();
            if (npc == null || npc.Runner != Runner) return;
            var speed = collision.relativeVelocity.magnitude;
            var direction = Vector3.ProjectOnPlane(npc.transform.position - transform.position, Vector3.up).normalized;
            pedestrianHit = npc;
            pedestrianImpact = direction * speed;
            pedestrianSeverity = body.mass >= 4f ? speed : Mathf.Min(speed, 4f);
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
                if (player == null || player.Runner != Runner || player.Object == null || !player.Object.IsValid
                    || player.SeatedVehicleId.IsValid) continue;
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
            if (!IsCarried || !Runner.TryFindObject(CarrierId, out var carrierObject)) return;
            if (carrierObject.TryGetComponent<NetworkCityPedestrian>(out var npc))
            {
                var pose = npc.BoneCarryPose;
                transform.SetPositionAndRotation(pose.position, pose.rotation);
                return;
            }
            if (!carrierObject.TryGetComponent<NetworkDogPlayer>(out var carrier)) return;
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
        private void OnDestroy()
        {
            ActiveItems.Remove(this);
        }
    }
}
