using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.AI;

namespace DogSimulator.Networking
{
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform), typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class NetworkCityVehicle : NetworkBehaviour
    {
        private const float DoorReach = 1.85f;
        private const float ExitSpeed = 1.2f;
        // DogPlayer's CharacterController is on Ignore Raycast for the dog camera.
        private const int VehicleQueryLayers = Physics.DefaultRaycastLayers | (1 << 2);
        private static readonly List<NetworkCityVehicle> Vehicles = new();

        [SerializeField] private Transform seat;
        [SerializeField] private Transform cameraTarget;
        [SerializeField] private Transform door;
        [SerializeField] private Transform leftExit;
        [SerializeField] private Transform rightExit;
        [Networked] public NetworkId DriverId { get; private set; }
        [Networked] public NetworkBool IsTraffic { get; private set; }
        [Networked] private float Speed { get; set; }

        private readonly RaycastHit[] casts = new RaycastHit[24];
        private readonly Collider[] overlaps = new Collider[24];
        private readonly Dictionary<NetworkDogPlayer, Vector3> pendingImpacts = new();
        private readonly Dictionary<NetworkId, float> hitCooldowns = new();
        private Rigidbody body;
        private BoxCollider hull;
        private SimplePolyCityWorld.VehiclePlacement layout;
        private Vector3 previousCenter;
        private Quaternion previousRotation;
        private Vector3 stepVelocity;
        private Vector2 controls;
        private TickTimer controlsExpire;
        private int waypoint;
        private float stuckTime;
        private float timeWithoutWaypoint;
        private bool resetPending;

        internal static IReadOnlyList<NetworkCityVehicle> Active => Vehicles;
        internal Transform Seat => seat;
        internal Transform CameraTarget => cameraTarget;
        private bool IsHost => Object != null && Object.IsValid && Object.HasStateAuthority;

        internal void Initialize(SimplePolyCityWorld.VehiclePlacement placement) => layout = placement;

        public override void Spawned()
        {
            body = GetComponent<Rigidbody>();
            hull = GetComponent<BoxCollider>();
            if (IsHost) IsTraffic = layout.route != null && layout.route.Length > 1;
            body.isKinematic = !IsHost;
            body.collisionDetectionMode = IsHost ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.Discrete;
            if (IsHost && TryGetComponent<NavMeshObstacle>(out var obstacle)) obstacle.enabled = true;
            previousCenter = transform.TransformPoint(hull.center);
            previousRotation = body.rotation;
            waypoint = 1;
            Vehicles.Add(this);
        }

        internal static NetworkCityVehicle FindAvailable(NetworkDogPlayer player)
        {
            NetworkCityVehicle nearest = null;
            var distance = DoorReach * DoorReach;
            foreach (var candidate in Vehicles)
            {
                if (candidate.Runner != player.Runner || !candidate.CanEnter(player)) continue;
                var sqr = (candidate.door.position - player.transform.position).sqrMagnitude;
                if (sqr >= distance) continue;
                nearest = candidate;
                distance = sqr;
            }
            return nearest;
        }

        private bool CanEnter(NetworkDogPlayer player)
        {
            return Object != null && Object.IsValid && !IsTraffic && !DriverId.IsValid && CurrentSpeed <= ExitSpeed
                && !player.SeatedVehicleId.IsValid && !player.KnockdownActive
                && (door.position - player.transform.position).sqrMagnitude < DoorReach * DoorReach;
        }

        private float CurrentSpeed => IsHost ? body.linearVelocity.magnitude : Speed;

        internal void ResistDogPush(Vector3 impulse)
        {
            // Parked brakes hold against a dog's sustained push. Vehicles still respond
            // to PhysX contacts from other vehicles and heavy objects.
            if (IsTraffic || DriverId.IsValid) body.AddForce(impulse, ForceMode.Impulse);
        }

        internal bool TryEnter(NetworkDogPlayer player)
        {
            if (!IsHost || player.Runner != Runner || !CanEnter(player)) return false;
            player.GetCarryTarget(out var carrying);
            if (carrying || !player.TrySit(this)) return false;
            DriverId = player.Object.Id;
            controls = Vector2.zero;
            return true;
        }

        internal void Drive(NetworkDogPlayer player, Vector2 move, bool hasInput, bool requestExit)
        {
            if (!IsHost || DriverId != player.Object.Id) return;
            if (!hasInput)
            {
                if (controlsExpire.ExpiredOrNotRunning(Runner)) controls = Vector2.zero;
                return;
            }
            controls = float.IsFinite(move.x) && float.IsFinite(move.y) ? Vector2.ClampMagnitude(move, 1) : Vector2.zero;
            // A missing input tick should not alternate braking with the held throttle.
            controlsExpire = TickTimer.CreateFromSeconds(Runner, .15f);
            if (!requestExit) return;
            if (CurrentSpeed > ExitSpeed) { player.SetVehicleExitHint(1); return; }
            if (!TryExitAt(player, leftExit, out var position) && !TryExitAt(player, rightExit, out position))
            { player.SetVehicleExitHint(2); return; }
            ClearDriver();
            player.LeaveVehicle(position, transform.rotation);
        }

        private bool TryExitAt(NetworkDogPlayer player, Transform anchor, out Vector3 position)
        {
            position = default;
            if (!Physics.Raycast(anchor.position + Vector3.up * 1.3f, Vector3.down, out var ground, 2.2f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                || ground.rigidbody != null || ground.normal.y < .7f) return false;

            var controller = player.GetComponent<CharacterController>();
            if (ground.point.y > body.position.y + controller.stepOffset + .06f) return false;
            position = ground.point + Vector3.up * .06f;
            var center = position + transform.rotation * controller.center;
            var axis = Vector3.up * Mathf.Max(0, controller.height * .5f - controller.radius);
            var count = Physics.OverlapCapsuleNonAlloc(center - axis, center + axis, controller.radius,
                overlaps, VehicleQueryLayers, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (var i = 0; i < count; i++)
                if (!overlaps[i].transform.IsChildOf(player.transform)) return false;
            return true;
        }

        internal void ReleaseDepartingPlayer(PlayerRef player)
        {
            if (!IsHost || !DriverId.IsValid) return;
            if (!Runner.TryFindObject(DriverId, out var driver) || driver.InputAuthority == player) ClearDriver();
        }

        private void ClearDriver()
        {
            DriverId = NetworkId.None;
            controls = Vector2.zero;
            controlsExpire = TickTimer.None;
        }

        internal void ResetVehicle()
        {
            if (!IsHost) return;
            if (DriverId.IsValid && Runner.TryFindObject(DriverId, out var driver)
                && driver.TryGetComponent<NetworkDogPlayer>(out var player))
                player.RecoverFromVehicle();
            ClearDriver();
            body.position = layout.placement.position;
            body.rotation = layout.placement.rotation;
            transform.SetPositionAndRotation(body.position, body.rotation);
            var transformSync = GetComponent<NetworkTransform>();
            transformSync.Teleport(body.position, body.rotation);
            body.linearVelocity = body.angularVelocity = Vector3.zero;
            stepVelocity = Vector3.zero;
            previousCenter = body.position + body.rotation * hull.center;
            previousRotation = body.rotation;
            waypoint = 1;
            Speed = 0;
            stuckTime = 0;
            timeWithoutWaypoint = 0;
            resetPending = false;
            pendingImpacts.Clear();
        }

        public override void FixedUpdateNetwork()
        {
            if (!IsHost) return;
            Speed = body.linearVelocity.magnitude;
            if (DriverId.IsValid && !Runner.TryFindObject(DriverId, out _)) ClearDriver();

            if (resetPending && body.position.y < -8 && DriverId.IsValid)
            {
                if (Runner.TryFindObject(DriverId, out var driver) && driver.TryGetComponent<NetworkDogPlayer>(out var player))
                    player.RecoverFromVehicle();
                ClearDriver();
            }
            if (resetPending && SpawnIsClear())
            {
                ResetVehicle();
                return;
            }

            foreach (var pair in pendingImpacts)
            {
                var player = pair.Key;
                if (player == null || player.Object == null || !player.Object.IsValid || player.SeatedVehicleId.IsValid)
                    continue;
                if (hitCooldowns.TryGetValue(player.Object.Id, out var until) && until > Time.fixedTime) continue;
                var speed = pair.Value.magnitude;
                player.ApplyWorldImpact(pair.Value.normalized * Mathf.Clamp(speed * 1.1f, 3f, 16f)
                    + Vector3.up * Mathf.Min(3f, speed * .21f), speed >= 7.5f);
                hitCooldowns[player.Object.Id] = Time.fixedTime + .65f;
            }
            pendingImpacts.Clear();
        }

        // Forces and hull samples run once per PhysX step, even if Fusion advances several ticks.
        private void FixedUpdate()
        {
            if (!IsHost || body == null) return;
            var center = body.position + body.rotation * hull.center;
            SweepActualMotion(center);
            previousCenter = center;
            previousRotation = body.rotation;
            if (body.position.y < -8)
            {
                resetPending = true;
                return;
            }

            var forward = body.rotation * Vector3.forward;
            var horizontal = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            var signedSpeed = Vector3.Dot(horizontal, forward);
            var targetSpeed = controls.y >= 0 ? controls.y * 15f : controls.y * 5f;
            var turn = controls.x * Mathf.Clamp(signedSpeed / 3f, -1, 1);
            if (IsTraffic)
                TrafficControls(center, forward, horizontal.magnitude, out targetSpeed, out turn);
            else
            {
                if (!DriverId.IsValid && body.IsSleeping())
                {
                    stepVelocity = Vector3.zero;
                    return;
                }
                stuckTime = Mathf.Abs(controls.y) > .75f && horizontal.magnitude < .3f
                    ? stuckTime + Time.fixedDeltaTime : 0;
                if (stuckTime > 12f) resetPending = true;
            }

            var dt = Time.fixedDeltaTime;
            var change = Mathf.Clamp(targetSpeed - signedSpeed, -16f * dt, 7f * dt);
            var lateral = horizontal - forward * signedSpeed;
            var correction = Vector3.ClampMagnitude(lateral, 9f * dt);
            if (Mathf.Abs(change) > .002f || correction.sqrMagnitude > .000004f)
                body.AddForce(forward * change - correction, ForceMode.VelocityChange);
            if (Mathf.Abs(turn) > .005f || Mathf.Abs(body.angularVelocity.y) > .03f)
                body.angularVelocity = Vector3.up * (turn * 1.35f);
            stepVelocity = horizontal + forward * change - correction;
        }

        private void TrafficControls(Vector3 center, Vector3 forward, float speed, out float targetSpeed, out float turn)
        {
            var route = layout.route;
            var toPoint = Vector3.ProjectOnPlane(route[waypoint].position - body.position, Vector3.up);
            var previous = route[(waypoint + route.Length - 1) % route.Length].position;
            var segment = route[waypoint].position - previous;
            timeWithoutWaypoint += Time.fixedDeltaTime;
            if (toPoint.sqrMagnitude < 4f || Vector3.Dot(toPoint, segment) < 0f && toPoint.sqrMagnitude < 36f)
            {
                waypoint = (waypoint + 1) % route.Length;
                toPoint = Vector3.ProjectOnPlane(route[waypoint].position - body.position, Vector3.up);
                timeWithoutWaypoint = 0;
            }

            var angle = Vector3.SignedAngle(forward, toPoint, Vector3.up);
            turn = Mathf.Clamp(angle / 32f, -1, 1);
            targetSpeed = route[waypoint].speed * Mathf.Lerp(1f, .24f, Mathf.InverseLerp(15f, 95f, Mathf.Abs(angle)));
            var stoppingRange = Mathf.Max(2.4f, speed * speed / 18f + 1.5f);
            var count = Physics.BoxCastNonAlloc(center, CastHalfExtents, forward, casts, body.rotation,
                stoppingRange, VehicleQueryLayers, QueryTriggerInteraction.Ignore);
            var hardDistance = count == casts.Length ? 0f : float.PositiveInfinity;
            var dogDistance = float.PositiveInfinity;
            for (var i = 0; i < count; i++)
            {
                var hit = casts[i];
                if (hit.rigidbody == body || hit.normal.y > .55f) continue;
                var dog = hit.collider.GetComponentInParent<NetworkDogPlayer>();
                if (dog != null)
                {
                    if (IsWalkingDog(dog)) dogDistance = Mathf.Min(dogDistance, hit.distance);
                    continue;
                }
                if (hit.rigidbody != null && hit.rigidbody.mass < 25f) continue;
                hardDistance = Mathf.Min(hardDistance, hit.distance);
            }
            targetSpeed = Mathf.Min(targetSpeed, Mathf.Sqrt(16f * Mathf.Max(0, hardDistance - 1f)));
            if (dogDistance < hardDistance && dogDistance < 2.7f) targetSpeed *= .87f;
            stuckTime = speed < .4f ? stuckTime + Time.fixedDeltaTime : 0;
            if (stuckTime > 12f || timeWithoutWaypoint > 22f) resetPending = true;
        }

        private Vector3 CastHalfExtents => hull.size * .5f - Vector3.up * .035f;

        private void SweepActualMotion(Vector3 center)
        {
            var movement = center - previousCenter;
            var distance = movement.magnitude;
            if (distance < .005f || distance > 2f || stepVelocity.sqrMagnitude < 4f) return;
            // CCD can stop the hull at the contact offsets before the surfaces overlap.
            const float contactMargin = .045f;
            var count = Physics.BoxCastNonAlloc(previousCenter, CastHalfExtents, movement / distance,
                casts, previousRotation, distance + contactMargin, VehicleQueryLayers, QueryTriggerInteraction.Ignore);
            if (count == casts.Length) return;
            var obstruction = float.PositiveInfinity;
            for (var i = 0; i < count; i++)
            {
                var hit = casts[i];
                if (hit.rigidbody != body && hit.normal.y < .55f
                    && hit.collider.GetComponentInParent<NetworkDogPlayer>() == null)
                    obstruction = Mathf.Min(obstruction, hit.distance);
            }
            for (var i = 0; i < count; i++)
            {
                var hit = casts[i];
                var player = hit.collider.GetComponentInParent<NetworkDogPlayer>();
                if (hit.distance <= obstruction && player != null)
                    QueueImpact(player, stepVelocity);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsHost) return;
            var dog = collision.collider.GetComponentInParent<NetworkDogPlayer>();
            if (dog != null) QueueImpact(dog, stepVelocity);
        }

        private bool IsWalkingDog(NetworkDogPlayer player)
            => player.Object != null && player.Object.IsValid && player.Runner == Runner
                && player.Object.Id != DriverId && !player.SeatedVehicleId.IsValid;

        private void QueueImpact(NetworkDogPlayer player, Vector3 velocity)
        {
            if (!IsWalkingDog(player) || velocity.sqrMagnitude < 4f) return;
            var relative = velocity - Vector3.ProjectOnPlane(player.MovementVelocity, Vector3.up);
            var approach = Vector3.Dot(relative, velocity.normalized);
            if (approach < 2.4f) return;
            pendingImpacts[player] = velocity.normalized * approach;
        }

        private bool SpawnIsClear()
        {
            var rotation = layout.placement.rotation;
            var center = layout.placement.position + rotation * hull.center;
            var count = Physics.OverlapBoxNonAlloc(center, CastHalfExtents, overlaps, rotation,
                VehicleQueryLayers, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (var i = 0; i < count; i++)
                if (overlaps[i].attachedRigidbody != body) return false;
            return true;
        }

        public override void Despawned(NetworkRunner runner, bool hasState) => Vehicles.Remove(this);
        private void OnDestroy() => Vehicles.Remove(this);
    }
}
