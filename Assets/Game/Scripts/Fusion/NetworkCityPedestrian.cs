using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

namespace DogSimulator.Networking
{
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform), typeof(NavMeshAgent))]
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class NetworkCityPedestrian : NetworkBehaviour
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

        private SimplePolyCityWorld.PedestrianPlacement patrol;
        private NavMeshAgent agent;
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
            agent = GetComponent<NavMeshAgent>();
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
                if (npc.Runner != dog.Runner || !npc.Object.HasStateAuthority) continue;
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
            nearest.AggroTarget = dog.Object.Id;
            nearest.aggroUntil = TickTimer.CreateFromSeconds(nearest.Runner, 10f);
            nearest.nextPath = TickTimer.None;
            nearest.Flinch(.3f);
        }

        internal void HitByThrownProp()
        {
            if (Object != null && Object.IsValid && Object.HasStateAuthority) Flinch(.46f);
        }

        private void Flinch(float duration)
        {
            FlinchUntil = TickTimer.CreateFromSeconds(Runner, duration);
            AttackUntil = swingAt = TickTimer.None;
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority || !agent.enabled || !agent.isOnNavMesh) return;
            WalkSpeed = agent.velocity.magnitude;
            var target = FindTarget();
            if (!FlinchUntil.ExpiredOrNotRunning(Runner))
            {
                agent.isStopped = true;
                return;
            }
            if (target != null) Chase(target);
            else Patrol();
        }

        private NetworkDogPlayer FindTarget()
        {
            if (!AggroTarget.IsValid) return null;
            if (!aggroUntil.ExpiredOrNotRunning(Runner)
                && Runner.TryFindObject(AggroTarget, out var obj)
                && obj.TryGetComponent<NetworkDogPlayer>(out var dog)
                && !dog.SeatedVehicleId.IsValid
                && (dog.transform.position - patrol.start).sqrMagnitude < ChaseLeash * ChaseLeash)
                return dog;

            AggroTarget = NetworkId.None;
            AttackUntil = swingAt = nextPath = TickTimer.None;
            return null;
        }

        private void Chase(NetworkDogPlayer dog)
        {
            var aim = dog.transform.position + Vector3.up * .55f;
            var offset = aim - transform.position;
            var horizontal = Vector3.ProjectOnPlane(offset, Vector3.up);
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
            agent.speed = 3.6f;
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
            AggroTarget = NetworkId.None;
            FlinchUntil = AttackUntil = swingAt = nextAttack = nextPath = waitAtEnd = TickTimer.None;
            towardsEnd = true;
            if (!agent.enabled || !agent.Warp(patrol.start)) return;
            agent.ResetPath();
            GetComponent<NetworkTransform>().Teleport(patrol.start,
                Quaternion.LookRotation(Vector3.ProjectOnPlane(patrol.end - patrol.start, Vector3.up)));
        }

        public override void Render()
        {
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
        private void OnDestroy() => Pedestrians.Remove(this);
    }
}
