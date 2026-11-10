using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

namespace DogSimulator.Networking
{
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform), typeof(NavMeshAgent))]
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed partial class NetworkCityPedestrian : NetworkBehaviour
    {
        private static readonly List<NetworkCityPedestrian> Pedestrians = new();
        private const float BiteReach = 1.7f;
        private const float AttackReach = 1.65f;
        private const float ChaseLeash = 18f;
        private const float Windup = .28f;
        private const float AttackDuration = .68f;
        private const int SightLayers = Physics.DefaultRaycastLayers | (1 << 2);

        [SerializeField] private Transform torso;
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform rightArm;
        [SerializeField] private Transform leftLeg;
        [SerializeField] private Transform rightLeg;
        [Networked] public NetworkId AggroTarget { get; private set; }
        [Networked] public TickTimer FlinchUntil { get; private set; }
        [Networked] public TickTimer AttackUntil { get; private set; }
        [Networked] private float WalkSpeed { get; set; }
        [Networked] public NetworkId CarrierId { get; private set; }
        [Networked] private byte FallState { get; set; }
        [Networked] private TickTimer RiseUntil { get; set; }

        private const byte Falling = 1, Prone = 2, Rising = 3;
        [SerializeField] private Rigidbody body;
        [SerializeField] private CapsuleCollider standing;
        [SerializeField] private BoxCollider lying;
        [SerializeField] private Transform visual;
        [SerializeField] private NetworkTransform transformSync;
        private Vector3 lastSafePosition;
        private float settledFor;
        private TickTimer recoveryAt;
        private Vector3 pendingImpact;
        private float pendingSeverity;
        private NetworkId pendingActor;
        private AnnoyingAction pendingAction;
        private SandboxInteractionRegistry guardMission;
        private bool followingBoneOrder;
        private NetworkCarryable guardedBone;
        private TickTimer barkCooldown;
        private TickTimer investigateUntil;
        private Vector3 investigateDirection;

        internal Pose BoneCarryPose => new(transform.TransformPoint(new Vector3(.29f, 1.03f, .38f)),
            transform.rotation * Quaternion.Euler(0, 20, 90));

        internal void AssignGuard(SandboxInteractionRegistry mission, NetworkCarryable bone)
        {
            guardMission = mission;
            guardedBone = bone;
        }

        internal void HearBark(NetworkDogPlayer dog)
        {
            var nearby = (dog.transform.position - transform.position).sqrMagnitude;
            if (IsStanding && nearby <= 16f && HasLineOfSight(Chest, dog.MouthAnchor.position, dog.transform))
            {
                dog.RecordBark(this);
                if (guardMission == null && !IsPolice && dog.WantedLevel >= 1 && !AggroTarget.IsValid
                    && barkCooldown.ExpiredOrNotRunning(Runner))
                {
                    investigateDirection = Vector3.ProjectOnPlane(dog.transform.position - transform.position, Vector3.up);
                    investigateUntil = TickTimer.CreateFromSeconds(Runner, 1.25f);
                    barkCooldown = TickTimer.CreateFromSeconds(Runner, 4f);
                }
            }
            if (guardMission == null || !IsStanding || AggroTarget.IsValid || !barkCooldown.ExpiredOrNotRunning(Runner))
                return;
            var offset = dog.transform.position - transform.position;
            if (offset.sqrMagnitude > 81f) return;
            investigateDirection = Vector3.ProjectOnPlane(offset, Vector3.up);
            investigateUntil = TickTimer.CreateFromSeconds(Runner, 1.25f);
            barkCooldown = TickTimer.CreateFromSeconds(Runner, 4f);
        }

        internal bool IsStanding => FallState == 0;
        internal bool CanBeCarried => FallState == Prone && !CarrierId.IsValid;
        internal Vector3 CarryPoint => transform.position + Vector3.up * .3f;
        internal Vector3 Velocity => agent != null && agent.enabled && agent.isOnNavMesh ? agent.velocity : Vector3.zero;

        private SimplePolyCityWorld.PedestrianPlacement patrol;
        [SerializeField] private NavMeshAgent agent;
        private TickTimer aggroUntil;
        private TickTimer swingAt;
        private TickTimer nextAttack;
        private TickTimer nextPath;
        private TickTimer waitAtEnd;
        private bool towardsEnd = true;
        private float walkPhase;

        public static IReadOnlyList<NetworkCityPedestrian> Active => Pedestrians;
        private Vector3 Chest => transform.position + Vector3.up * 1.05f;

        internal void Initialize(SimplePolyCityWorld.PedestrianPlacement placement) => patrol = placement;

        public override void Spawned()
        {
            Pedestrians.Add(this);
            policeMarker.gameObject.SetActive(IsPolice);
            lastSafePosition = patrol.start;
            if (IsPolice && Object.HasStateAuthority) policePath = new NavMeshPath();
            if (!Object.HasStateAuthority || !NavMesh.SamplePosition(patrol.start, out var start, .55f, NavMesh.AllAreas))
                return;
            transform.position = start.position;
            agent.enabled = true;
        }

        internal static void TryBite(NetworkDogPlayer dog)
        {
            NetworkCityPedestrian nearest = null;
            var best = BiteReach * BiteReach;
            var mouth = dog.MouthAnchor.position;
            foreach (var npc in Pedestrians)
            {
                if (npc.Runner != dog.Runner || !npc.Object.HasStateAuthority || !npc.IsStanding) continue;
                var toNpc = npc.Chest - mouth;
                var bearing = Vector3.ProjectOnPlane(npc.transform.position - dog.transform.position, Vector3.up);
                if (toNpc.sqrMagnitude >= best
                    || Vector3.Dot(dog.transform.forward, bearing.normalized) < .4f)
                    continue;
                if (!HasLineOfSight(mouth, npc.Chest, npc.transform)) continue;
                best = toNpc.sqrMagnitude;
                nearest = npc;
            }
            if (nearest == null) return;
            dog.RecordIncident(nearest, nearest.IsPolice && nearest.PoliceTarget == dog.Object.Id
                ? AnnoyingAction.AttackPolice : AnnoyingAction.Bite);
            nearest.Flinch(.3f);
            if (nearest.IsPolice) return;
            nearest.AggroTarget = dog.Object.Id;
            nearest.aggroUntil = TickTimer.CreateFromSeconds(nearest.Runner, 10f);
            nearest.nextPath = TickTimer.None;
        }

        internal void QueueHit(Vector3 velocity, float severity, NetworkId actor = default, AnnoyingAction action = default)
        {
            if (Object == null || !Object.IsValid || !Object.HasStateAuthority || severity <= pendingSeverity) return;
            pendingSeverity = severity;
            pendingImpact = velocity;
            pendingActor = actor;
            pendingAction = action;
        }

        internal bool TryPickup(NetworkDogPlayer dog)
        {
            if (!Object.HasStateAuthority || !CanBeCarried || !dog.CanPickUp(this, CarryPoint)) return false;
            CarrierId = dog.Object.Id;
            ConfigureBody();
            FollowCarrier(dog);
            return true;
        }

        internal bool Release(NetworkDogPlayer dog, bool thrown)
        {
            if (!Object.HasStateAuthority || CarrierId != dog.Object.Id) return false;
            var rotation = dog.transform.rotation;
            if (!dog.TryFindBodyDrop(this, lying.center, lying.size * .5f, rotation, out var point))
            {
                if (thrown) return false;
                if (!TryRecoveryPoint(lastSafePosition, 1.5f, out point)
                    && !TryRecoveryPoint(patrol.start, 2.5f, out point))
                {
                    ResetPatrol();
                    return true;
                }
            }
            CarrierId = NetworkId.None;
            transformSync.Teleport(point, rotation);
            body.position = point;
            body.rotation = rotation;
            StartFall(thrown ? Vector3.ClampMagnitude(dog.MovementVelocity + dog.transform.forward * 7f
                + Vector3.up * 2.5f, 12f) : Vector3.zero);
            return true;
        }

        private void FollowCarrier(NetworkDogPlayer dog)
        {
            var rotation = dog.transform.rotation;
            transform.SetPositionAndRotation(dog.MouthAnchor.position + dog.transform.forward * .3f
                - Vector3.up * .28f, rotation);
        }

        private void ConfigureBody()
        {
            standing.enabled = FallState is 0 or Rising;
            lying.enabled = !standing.enabled && !CarrierId.IsValid;
            var dynamic = Object.HasStateAuthority && FallState == Falling && !CarrierId.IsValid;
            if (!dynamic) body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.isKinematic = !dynamic;
            if (dynamic) body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        private void StartFall(Vector3 velocity)
        {
            guardedBone?.DropRecoveredBone(this);
            followingBoneOrder = false;
            agent.enabled = false;
            AggroTarget = NetworkId.None;
            AttackUntil = swingAt = FlinchUntil = TickTimer.None;
            WalkSpeed = 0;
            FallState = Falling;
            settledFor = 0;
            ConfigureBody();
            body.linearVelocity = velocity;
        }

        private void UpdateFall()
        {
            if (CarrierId.IsValid)
            {
                if (Runner.TryFindObject(CarrierId, out var carrier) && carrier.TryGetComponent<NetworkDogPlayer>(out var dog))
                    FollowCarrier(dog);
                else ResetPatrol();
                return;
            }
            if (FallState == Rising)
            {
                if (RiseUntil.Expired(Runner)) FallState = 0;
                return;
            }
            if (transform.position.y < -8f)
            {
                ResetPatrol();
                return;
            }
            if (FallState == Falling)
            {
                var grounded = Physics.Raycast(body.position + Vector3.up * .22f, Vector3.down, .33f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                settledFor = grounded && body.linearVelocity.sqrMagnitude < .2f ? settledFor + Runner.DeltaTime : 0;
                if (settledFor < .2f) return;
                body.linearVelocity = Vector3.zero;
                FallState = Prone;
                recoveryAt = TickTimer.CreateFromSeconds(Runner, 6f);
                ConfigureBody();
                return;
            }
            if (!recoveryAt.Expired(Runner)) return;
            if (TryRecoveryPoint(transform.position, 2.5f, out var point)
                || TryRecoveryPoint(lastSafePosition, 1.5f, out point)
                || TryRecoveryPoint(patrol.start, 3f, out point))
                RecoverAt(point, true);
            else recoveryAt = TickTimer.CreateFromSeconds(Runner, .5f);
        }

        private bool TryRecoveryPoint(Vector3 origin, float range, out Vector3 point)
        {
            point = default;
            for (var i = 0; i <= 8; i++)
            {
                var angle = i * Mathf.PI / 4f;
                var sample = i == 0 ? origin : origin + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * range * .65f;
                if (!NavMesh.SamplePosition(sample, out var nav, range, agent.areaMask)) continue;
                if (!Physics.Raycast(nav.position + Vector3.up * .5f, Vector3.down, .85f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                var blocked = false;
                foreach (var collider in Physics.OverlapCapsule(nav.position + Vector3.up * .36f,
                    nav.position + Vector3.up * 1.5f, .31f, SightLayers, QueryTriggerInteraction.Ignore))
                {
                    if (collider.transform.IsChildOf(transform)) continue;
                    blocked = true;
                    break;
                }
                if (blocked) continue;
                point = nav.position;
                return true;
            }
            return false;
        }

        private void RecoverAt(Vector3 point, bool animate)
        {
            var forward = Vector3.ProjectOnPlane(patrol.end - patrol.start, Vector3.up);
            var rotation = Quaternion.LookRotation(forward);
            agent.enabled = false;
            if (!body.isKinematic) body.linearVelocity = body.angularVelocity = Vector3.zero;
            CarrierId = NetworkId.None;
            FallState = animate ? Rising : (byte)0;
            RiseUntil = TickTimer.CreateFromSeconds(Runner, .5f);
            ConfigureBody();
            body.position = point;
            body.rotation = rotation;
            transformSync.Teleport(point, rotation);
            agent.enabled = true;
            if (agent.isOnNavMesh) agent.ResetPath();
            nextPath = TickTimer.None;
        }

        private void Flinch(float duration)
        {
            FlinchUntil = TickTimer.CreateFromSeconds(Runner, duration);
            AttackUntil = swingAt = TickTimer.None;
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;
            if (pendingSeverity > 0)
            {
                if (IsStanding && (pendingSeverity >= 6f || pendingAction == AnnoyingAction.ThrownProp)
                    && pendingActor.IsValid && Runner.TryFindObject(pendingActor, out var source)
                    && source.TryGetComponent<NetworkDogPlayer>(out var actor))
                    actor.RecordIncident(this, pendingAction);
                if (IsStanding && pendingSeverity >= 6f)
                {
                    lastSafePosition = transform.position;
                    StartFall(Vector3.ClampMagnitude(pendingImpact, 12f) + Vector3.up * 1.7f);
                }
                else if (IsStanding) Flinch(.46f);
                pendingSeverity = 0;
            }
            if (!IsStanding)
            {
                UpdateFall();
                return;
            }
            if (!agent.enabled || !agent.isOnNavMesh) return;
            WalkSpeed = agent.velocity.magnitude;
            if (IsPolice) return;
            if (!FlinchUntil.ExpiredOrNotRunning(Runner))
            {
                agent.isStopped = true;
                return;
            }
            if (UpdateBoneOrder()) return;
            if (followingBoneOrder)
            {
                followingBoneOrder = false;
                ClearTarget();
            }
            var target = FindTarget();
            if (target != null) Chase(target);
            else if (!investigateUntil.ExpiredOrNotRunning(Runner))
            {
                agent.isStopped = true;
                Face(investigateDirection);
            }
            else Patrol();
        }

        private bool UpdateBoneOrder()
        {
            if (guardMission == null || !guardMission.CanDispatchGuards || guardedBone == null
                || !investigateUntil.ExpiredOrNotRunning(Runner)) return false;
            var carrierId = guardedBone.CarrierId;
            if (carrierId == Object.Id)
            {
                FollowBoneOrder(NetworkId.None);
                var home = GoldenBoneCourse.ReturnPoint;
                if ((transform.position - home).sqrMagnitude < 1f)
                    guardedBone.ResetToSpawn();
                else GoToBonePosition(home, 3.8f);
                return true;
            }

            if (carrierId.IsValid)
            {
                if (!Runner.TryFindObject(carrierId, out var carrier) || !carrier.TryGetComponent<NetworkDogPlayer>(out var dog))
                    return false;
                FollowBoneOrder(carrierId);
                Chase(dog);
                return true;
            }

            var position = guardedBone.transform.position;
            if (GoldenBoneCourse.Delivery.Contains(position)) return false;
            FollowBoneOrder(NetworkId.None);
            if ((position - Chest).sqrMagnitude < 2.25f && HasLineOfSight(Chest, position, guardedBone.transform)
                && guardedBone.TryRecoverBy(this))
            {
                nextPath = TickTimer.None;
                return true;
            }
            GoToBonePosition(position, 4.6f);
            return true;
        }

        private void FollowBoneOrder(NetworkId target)
        {
            if (followingBoneOrder && target == AggroTarget) return;
            ClearTarget();
            followingBoneOrder = true;
            AggroTarget = target;
        }

        private void GoToBonePosition(Vector3 destination, float speed)
        {
            agent.isStopped = false;
            agent.speed = speed;
            if (!nextPath.ExpiredOrNotRunning(Runner)) return;
            nextPath = TickTimer.CreateFromSeconds(Runner, .35f);
            if (NavMesh.SamplePosition(destination, out var point, 2f, agent.areaMask))
                agent.SetDestination(point.position);
            else agent.ResetPath();
        }

        private NetworkDogPlayer FindTarget()
        {
            if (!AggroTarget.IsValid) return null;
            if (!aggroUntil.ExpiredOrNotRunning(Runner)
                && Runner.TryFindObject(AggroTarget, out var obj)
                && obj.TryGetComponent<NetworkDogPlayer>(out var dog)
                && !dog.InJail && !dog.SeatedVehicleId.IsValid && !dog.CarrierId.IsValid
                && (dog.transform.position - patrol.start).sqrMagnitude < ChaseLeash * ChaseLeash)
                return dog;

            ClearTarget();
            return null;
        }

        private void ClearTarget()
        {
            AggroTarget = NetworkId.None;
            AttackUntil = swingAt = nextPath = TickTimer.None;
        }

        private void Chase(NetworkDogPlayer dog)
        {
            var aim = dog.transform.position + Vector3.up * .55f;
            var offset = aim - transform.position;
            var horizontal = Vector3.ProjectOnPlane(offset, Vector3.up);
            if (!followingBoneOrder && HasLineOfSight(Chest, aim, dog.transform)) dog.ObservePursuit();
            var inRange = horizontal.sqrMagnitude <= AttackReach * AttackReach
                && Mathf.Abs(aim.y - Chest.y) < 1.15f && HasLineOfSight(Chest, aim, dog.transform);
            if (!AttackUntil.ExpiredOrNotRunning(Runner))
            {
                agent.isStopped = true;
                Face(horizontal);
                if (swingAt.Expired(Runner))
                {
                    swingAt = TickTimer.None;
                    if (inRange)
                        dog.ApplyWorldImpact(horizontal.normalized * 8f + Vector3.up * 1.6f, true);
                }
                return;
            }

            if (inRange)
            {
                agent.isStopped = true;
                Face(horizontal);
                if (nextAttack.ExpiredOrNotRunning(Runner) && !dog.KnockdownActive)
                {
                    AttackUntil = TickTimer.CreateFromSeconds(Runner, AttackDuration);
                    swingAt = TickTimer.CreateFromSeconds(Runner, Windup);
                    nextAttack = TickTimer.CreateFromSeconds(Runner, 1.4f);
                }
                return;
            }

            agent.isStopped = false;
            agent.speed = followingBoneOrder ? 4.6f : 3.6f;
            if (nextPath.ExpiredOrNotRunning(Runner))
            {
                nextPath = TickTimer.CreateFromSeconds(Runner, .18f);
                if (NavMesh.SamplePosition(dog.transform.position, out var point, 1.7f, agent.areaMask))
                    agent.SetDestination(point.position);
            }
        }

        private void Patrol()
        {
            agent.speed = 1.7f;
            if (!waitAtEnd.ExpiredOrNotRunning(Runner))
            {
                agent.isStopped = true;
                return;
            }
            agent.isStopped = false;
            var destination = towardsEnd ? patrol.end : patrol.start;
            if ((transform.position - destination).sqrMagnitude < .22f)
            {
                towardsEnd = !towardsEnd;
                waitAtEnd = TickTimer.CreateFromSeconds(Runner, .7f);
                agent.isStopped = true;
                return;
            }
            if (nextPath.ExpiredOrNotRunning(Runner))
            {
                nextPath = TickTimer.CreateFromSeconds(Runner, .75f);
                agent.SetDestination(destination);
            }
        }

        private void Face(Vector3 direction)
        {
            if (direction.sqrMagnitude > .001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction), Runner.DeltaTime * 650f);
        }

        private static bool HasLineOfSight(Vector3 origin, Vector3 target, Transform expected)
        {
            return !Physics.Linecast(origin, target, out var hit, SightLayers, QueryTriggerInteraction.Ignore)
                || hit.transform.IsChildOf(expected);
        }

        internal void ResetPatrol()
        {
            guardedBone?.DropRecoveredBone(this);
            followingBoneOrder = false;
            AggroTarget = NetworkId.None;
            FlinchUntil = AttackUntil = swingAt = nextAttack = nextPath = waitAtEnd = TickTimer.None;
            towardsEnd = true;
            pendingSeverity = 0;
            investigateUntil = barkCooldown = TickTimer.None;
            RecoverAt(TryRecoveryPoint(patrol.start, 2f, out var point) ? point : patrol.start, false);
        }

        public override void Render()
        {
            PresentPolice();
            if (!Object.HasStateAuthority)
            {
                ConfigureBody();
                if (CarrierId.IsValid && Runner.TryFindObject(CarrierId, out var carrier)
                    && carrier.TryGetComponent<NetworkDogPlayer>(out var dog)) FollowCarrier(dog);
            }
            var tilt = FallState == Rising ? Mathf.Clamp01((RiseUntil.RemainingTime(Runner) ?? 0) / .5f)
                : IsStanding ? 0f : 1f;
            visual.localRotation = Quaternion.Euler(90f * tilt, 0, 0);
            visual.localPosition = new Vector3(0, .3f, -.85f) * tilt;
            walkPhase += Time.deltaTime * WalkSpeed * 5f;
            var step = Mathf.Sin(walkPhase) * Mathf.Min(WalkSpeed * 18f, 43f);
            var attack = AttackUntil.RemainingTime(Runner) ?? 0f;
            var flinch = FlinchUntil.RemainingTime(Runner) ?? 0f;
            var swing = attack > 0 ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(attack / AttackDuration)) * 105f : 0;
            torso.localRotation = Quaternion.Euler(flinch > 0 ? -Mathf.Min(flinch * 65f, 24f) : swing * .15f, 0, 0);
            leftArm.localRotation = Quaternion.Euler(-step, 0, 6);
            rightArm.localRotation = Quaternion.Euler(step - swing, 0, -6);
            leftLeg.localRotation = Quaternion.Euler(step, 0, 0);
            rightLeg.localRotation = Quaternion.Euler(-step, 0, 0);
        }

        public override void Despawned(NetworkRunner runner, bool hasState) => Pedestrians.Remove(this);
        private void OnDestroy()
        {
            Pedestrians.Remove(this);
            if (siren != null) Destroy(siren);
        }
    }
}
