using System.Collections.Generic;
using DogSimulator.CustomDogFace;
using DogSimulator.Player;
using Fusion;
using TMPro;
using UnityEngine;

namespace DogSimulator.Networking
{
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform))]
    [RequireComponent(typeof(DogMotor), typeof(DogInputReader), typeof(DogAnimationDriver))]
    public sealed partial class NetworkDogPlayer : NetworkBehaviour
    {
        private static readonly List<NetworkDogPlayer> ActivePlayers = new();
        private static readonly int PeeState = Animator.StringToHash(nameof(DogAction.Pee));
        // The shared rig holds the raised leg from 20% to 76% of the clip.
        private const float PeeEmissionStart = .22f;
        private const float PeeEmissionEnd = .64f;
        private const float PeeLegLowerStart = .76f;
        private const float MinimumPlayerSeparation = .8f;
        private const float MaximumSeparationHeight = .65f;
        private static readonly Vector3 PeeOutletOffset = new(-.15f, -.057f, .129f);
        private static readonly Quaternion PeeAim = Quaternion.LookRotation(new Vector3(-1f, -.23f, .24f));
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private DogModelCatalog modelCatalog;
        [SerializeField] private Material peeMaterial;
        [SerializeField, Min(.5f)] private float carryRange = 2.2f;
        [SerializeField, Min(.1f)] private float minimumKnockdownSeconds = .8f;
        [SerializeField, Min(0)] private float knockdownSettleSpeed = .45f;
        [Networked] public NetworkString<_32> Nickname { get; private set; }
        // Model 1 is the supplied rigged visual. Selection/reservations remain separate work.
        [Networked] public int DogModelId { get; private set; }
        [Networked] public int FaceRevision { get; private set; }
        [Networked] public int FaceModelId { get; private set; }
        [Networked] public int FaceLayoutVersion { get; private set; }
        [Networked] public DogFaceKind FaceKind { get; private set; }
        [Networked] public int FaceByteLength { get; private set; }
        [Networked] public NetworkString<_64> FaceSha256 { get; private set; }
        [Networked] public float AnimationSpeed { get; private set; }
        [Networked] public NetworkBool AnimationGrounded { get; private set; }
        [Networked] public DogAction AnimationAction { get; private set; }
        [Networked] public int AnimationSequence { get; private set; }
        [Networked] public TickTimer AnimationUntil { get; private set; }
        [Networked] public NetworkId InteractionTarget { get; private set; }
        [Networked] public SandboxInteractionKind InteractionKind { get; private set; }
        [Networked] public int InteractionRequestId { get; private set; }
        [Networked] public Vector3 MovementVelocity { get; private set; }
        [Networked] public NetworkId StackSupportId { get; private set; }
        [Networked] public Vector3 StackLocalPosition { get; private set; }
        [Networked] public Quaternion StackLocalRotation { get; private set; }
        [Networked] public TickTimer KnockdownUntil { get; private set; }
        [Networked] public NetworkBool KnockdownActive { get; private set; }
        [Networked] public Vector3 KnockdownDirection { get; private set; }
        [Networked] public NetworkId ImpactTargetId { get; private set; }
        [Networked] public Vector3 ImpactImpulse { get; private set; }
        [Networked] public int ImpactRequestId { get; private set; }
        [Networked] public NetworkId SeatedVehicleId { get; private set; }
        [Networked] private byte VehicleExitHint { get; set; }
        [Networked] private TickTimer VehicleExitHintUntil { get; set; }
        [Networked] private TickTimer PoopCooldownUntil { get; set; }
        [Networked] private TickTimer PeeCooldownUntil { get; set; }
        [SerializeField] private DogMotor motor;
        [SerializeField] private NetworkTransform transformSync;
        [SerializeField] private DogBackSupport backSupport;
        internal CharacterController Controller => motor.Controller;
        internal DogInputReader InputReader => input;
        internal DogVisualPresenter Visual => animations.Visual;
        [SerializeField] private DogInputReader input;
        private DogCamera followCamera;
        private string renderedName;
        [SerializeField] private DogAnimationDriver animations;
        private int renderedModelId = int.MinValue;
        [SerializeField] private Transform mouthAnchor;
        private float nextBodyImpactTime;
        private readonly Dictionary<NetworkId, int> processedImpacts = new();
        [SerializeField] private DogFaceAppearance faceAppearance;
        private byte[] ownerFaceJpeg;
        [SerializeField] private ParticleSystem peeStream;
        private Transform peePelvis;
        [SerializeField] private Collider[] seatColliders;
        private bool presentedInSeat;
        private int processedBite = int.MinValue;
        private int processedBark = int.MinValue;
        [Networked] public NetworkId CarrierId { get; private set; }
        [Networked] private NetworkBool SettledDown { get; set; }
        [Networked] private TickTimer CarryImmuneUntil { get; set; }
        private float stableDownSeconds;
        private Pose lastPronePose;
        internal bool CanBeCarried => KnockdownActive && SettledDown && !CarrierId.IsValid
            && CarryImmuneUntil.ExpiredOrNotRunning(Runner) && FindHeldTarget() == null;
        internal Vector3 CarryPoint => transform.position + Vector3.up * .25f;

        public static IReadOnlyList<NetworkDogPlayer> Active => ActivePlayers;
        public Transform MouthAnchor => mouthAnchor != null ? mouthAnchor : transform;
        public float CarryRange => carryRange;
        internal int RespawnSerial => motor.RespawnSerial;
        internal float PoopCooldown => Object != null && Object.IsValid ? PoopCooldownUntil.RemainingTime(Runner) ?? 0 : 0;
        internal float PeeCooldown => Object != null && Object.IsValid ? PeeCooldownUntil.RemainingTime(Runner) ?? 0 : 0;
        public DogFaceAppearance FaceAppearance => faceAppearance;
        public byte[] OwnerFaceJpeg => Object != null && Object.HasStateAuthority ? ownerFaceJpeg : null;

        internal DogNetworkInput ConsumeNetworkInput()
        {
            if (input == null || followCamera == null || !input.GameplayEnabled || !Application.isFocused)
                return default;
            var buttons = DogInputButtons.None;
            if (input.ConsumeJump()) buttons |= DogInputButtons.Jump;
            if (input.ConsumePrimaryCarry()) buttons |= DogInputButtons.PrimaryCarry;
            if (input.ConsumeDropCarry()) buttons |= DogInputButtons.DropCarry;
            if (input.ConsumeResetYard()) buttons |= DogInputButtons.ResetYard;
            if (input.ConsumeRespawnBone()) buttons |= DogInputButtons.RespawnBone;
            if (input.ConsumeResetMission()) buttons |= DogInputButtons.ResetMission;
            return new DogNetworkInput
            {
                Move = input.Move,
                Yaw = followCamera.Yaw,
                Sprint = input.Sprint ? (byte)1 : (byte)0,
                Action = input.ConsumeAction(),
                Buttons = buttons
            };
        }

        internal static event System.Action<NetworkDogPlayer, int, byte[]> FaceReceived;

        // Any committed peer may need to fetch this avatar's face. In Host Mode the
        // avatar owner is InputAuthority rather than a Proxy, so Proxies alone rejects
        // the owner's late manifest fetch before it can reach StateAuthority.
        [Rpc(RpcSources.All, RpcTargets.StateAuthority, TickAligned = false)]
        internal void RPC_RequestFace(int revision, RpcInfo info = default)
        {
            if (revision == FaceRevision && ownerFaceJpeg != null && info.Source.IsRealPlayer
                && Runner.IsPlayerCommitted(info.Source)) RPC_ReceiveFace(info.Source, revision, ownerFaceJpeg);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies, Channel = RpcChannel.ReliableLargeData, TickAligned = false)]
        internal void RPC_ReceiveFace([RpcTarget] PlayerRef target, int revision, byte[] jpeg)
        {
            FaceReceived?.Invoke(this, revision, jpeg);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, TickAligned = false)]
        private void RPC_ConfigurePlayer(NetworkString<_32> name, int modelId, RpcInfo info = default)
        {
            if (!Object.HasStateAuthority || info.Source != Object.InputAuthority) return;
            ApplyProfile(name.ToString(), modelId);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, TickAligned = false)]
        private void RPC_RequestModel(int modelId, RpcInfo info = default)
        {
            if (Object.HasStateAuthority && info.Source == Object.InputAuthority) SetModel(modelId);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority,
            Channel = RpcChannel.ReliableLargeData, TickAligned = false)]
        private void RPC_PublishCustomFace(byte[] jpeg, RpcInfo info = default)
        {
            if (Object.HasStateAuthority && info.Source == Object.InputAuthority) PublishCustomFace(jpeg);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, TickAligned = false)]
        private void RPC_PublishDefaultFace(RpcInfo info = default)
        {
            if (Object.HasStateAuthority && info.Source == Object.InputAuthority) PublishDefaultFace();
        }

        public bool IntersectsBodySweep(Vector3 start, Vector3 end, float sweepRadius)
        {
            var controller = Controller;
            if (controller == null || CarrierId.IsValid) return false;
            var scale = transform.lossyScale;
            var bodyRadius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            var halfSegment = Mathf.Max(0, controller.height * .5f - controller.radius) * Mathf.Abs(scale.y);
            var center = transform.TransformPoint(controller.center);
            var axis = transform.up * halfSegment;
            var combinedRadius = sweepRadius + bodyRadius;
            return SegmentDistanceSquared(start, end, center - axis, center + axis)
                <= combinedRadius * combinedRadius;
        }

        private static float SegmentDistanceSquared(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            var d1 = q1 - p1;
            var d2 = q2 - p2;
            var r = p1 - p2;
            var a = Vector3.Dot(d1, d1);
            var e = Vector3.Dot(d2, d2);
            var f = Vector3.Dot(d2, r);
            float s, t;
            if (a <= Mathf.Epsilon && e <= Mathf.Epsilon) return r.sqrMagnitude;
            if (a <= Mathf.Epsilon)
            {
                s = 0;
                t = Mathf.Clamp01(f / e);
            }
            else
            {
                var c = Vector3.Dot(d1, r);
                if (e <= Mathf.Epsilon)
                {
                    t = 0;
                    s = Mathf.Clamp01(-c / a);
                }
                else
                {
                    var b = Vector3.Dot(d1, d2);
                    var denominator = a * e - b * b;
                    s = denominator > Mathf.Epsilon ? Mathf.Clamp01((b * f - c * e) / denominator) : 0;
                    t = (b * s + f) / e;
                    if (t < 0) { t = 0; s = Mathf.Clamp01(-c / a); }
                    else if (t > 1) { t = 1; s = Mathf.Clamp01((b - c) / a); }
                }
            }
            var closest1 = p1 + d1 * s;
            var closest2 = p2 + d2 * t;
            return (closest1 - closest2).sqrMagnitude;
        }

        public override void Spawned()
        {
            motor.ControllerImpact += OnControllerImpact;
            motor.BodyPush += OnBodyPush;
            motor.HardLanding += OnHardLanding;
            if (!ActivePlayers.Contains(this)) ActivePlayers.Add(this);
            input.SetGameplayEnabled(false);
            input.enabled = Object.HasInputAuthority || Runner.GameMode == GameMode.Single;
            var controller = Controller;
            controller.enabled = false;
            if (Object.HasStateAuthority)
            {
                // Awake can see the prefab pose before Fusion applies the spawn transform.
                motor.CaptureRespawnPose();
                controller.enabled = true;
            }
            else if (Object.HasInputAuthority)
            {
                // The motor and Unity collision state cannot be restored by Fusion rollback.
                // Keep collecting this player's input and interpolate the pose from the host.
                Runner.SetIsSimulated(Object, false);
            }
        }

        public void ConfigureLocalOwner(string name, int modelId, DogCamera camera)
        {
            if (!GuestProfile.TryNormalizeName(name, out var normalized, out var error))
                throw new System.ArgumentException(error);
            if (!Object.HasInputAuthority && Runner.GameMode != GameMode.Single) return;
            followCamera = camera;
            camera.Bind(transform, input);
            if (Object.HasStateAuthority) ApplyProfile(normalized, modelId);
            else RPC_ConfigurePlayer(normalized, modelId);
        }

        public void InitializeOwner(string name, int modelId, DogCamera camera)
            => ConfigureLocalOwner(name, modelId, camera);

        public void InitializeOwner(string name, DogCamera camera)
            => ConfigureLocalOwner(name, modelCatalog != null ? modelCatalog.FallbackModelId : 1, camera);

        private void ApplyProfile(string name, int modelId)
        {
            if (!Object.HasStateAuthority || !GuestProfile.TryNormalizeName(name, out var normalized, out _)
                || modelCatalog == null || !modelCatalog.IsValidModelId(modelId)) return;
            Nickname = normalized;
            SetModel(modelId);
        }

        public bool RequestModel(int modelId)
        {
            if (modelCatalog == null || !modelCatalog.IsValidModelId(modelId)
                || (!Object.HasInputAuthority && Runner.GameMode != GameMode.Single)) return false;
            if (Object.HasStateAuthority) SetModel(modelId);
            else RPC_RequestModel(modelId);
            return true;
        }

        public void SetModel(int modelId)
        {
            if (!Object.HasStateAuthority || modelCatalog == null || !modelCatalog.IsValidModelId(modelId)) return;
            DogModelId = modelId;
            if (FaceRevision > 0) PublishCurrentFaceForModel();
        }

        public bool PublishCustomFace(byte[] jpeg)
        {
            if (!Object.HasStateAuthority)
            {
                if (!Object.HasInputAuthority || !DogFaceImageValidator.IsValidFaceJpeg(jpeg, out _)) return false;
                RPC_PublishCustomFace(jpeg);
                return true;
            }
            if (modelCatalog == null
                || !modelCatalog.TryGet(DogModelId, out var entry) || !entry.SupportsCustomFace
                || !DogFaceImageValidator.IsValidFaceJpeg(jpeg, out _)) return false;
            ownerFaceJpeg = (byte[])jpeg.Clone();
            FaceRevision = NextFaceRevision();
            FaceModelId = DogModelId;
            FaceLayoutVersion = entry.FaceLayoutVersion;
            FaceKind = DogFaceKind.CustomJpeg;
            FaceByteLength = ownerFaceJpeg.Length;
            FaceSha256 = DogFaceImageValidator.Sha256Hex(ownerFaceJpeg);
            faceAppearance?.ApplyCustom(FaceModelId, FaceLayoutVersion, ownerFaceJpeg);
            return true;
        }

        public void PublishDefaultFace()
        {
            if (!Object.HasStateAuthority)
            {
                if (Object.HasInputAuthority) RPC_PublishDefaultFace();
                return;
            }
            ownerFaceJpeg = null;
            FaceRevision = NextFaceRevision();
            FaceModelId = DogModelId;
            FaceLayoutVersion = modelCatalog != null && modelCatalog.TryGet(DogModelId, out var entry)
                ? entry.FaceLayoutVersion : 0;
            FaceKind = DogFaceKind.Default;
            FaceByteLength = 0;
            FaceSha256 = default;
            faceAppearance?.ApplyDefault(FaceModelId, FaceLayoutVersion);
        }

        public void PublishCurrentFaceForModel()
        {
            if (!Object.HasStateAuthority || modelCatalog == null
                || !modelCatalog.TryGet(DogModelId, out var entry) || !entry.SupportsCustomFace)
            { PublishDefaultFace(); return; }
            if (ownerFaceJpeg != null) PublishCustomFace(ownerFaceJpeg);
            else PublishDefaultFace();
        }

        private int NextFaceRevision()
        {
            var next = unchecked(FaceRevision + 1);
            return next <= 0 ? 1 : next;
        }

        public void SetControlsEnabled(bool value)
        {
            if (input != null && Object != null
                && (Object.HasInputAuthority || Runner.GameMode == GameMode.Single))
                input.SetGameplayEnabled(value);
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;
            DogNetworkInput networkInput;
            var hasInput = true;
            if (Runner.GameMode == GameMode.Single)
                networkInput = ConsumeNetworkInput();
            else hasInput = GetInput(out networkInput);
            var reset = networkInput.IsPressed(DogInputButtons.ResetYard) ? SandboxInteractionKind.ResetYard
                : networkInput.IsPressed(DogInputButtons.ResetMission) ? SandboxInteractionKind.ResetMission
                : networkInput.IsPressed(DogInputButtons.RespawnBone) ? SandboxInteractionKind.RespawnBone
                : SandboxInteractionKind.None;
            if (reset != SandboxInteractionKind.None) SubmitInteraction(NetworkId.None, reset);
            if (CarrierId.IsValid)
            {
                if (Runner.TryFindObject(CarrierId, out var carrier) && carrier.TryGetComponent<NetworkDogPlayer>(out var dog))
                {
                    if (networkInput.IsPressed(DogInputButtons.Jump)) ReleaseFromCarrier(dog, true);
                    else FollowCarrier(dog);
                }
                else ReleaseFromCarrier(null, true);
                return;
            }
            if (SeatedVehicleId.IsValid)
            {
                var vehicle = GetSeatedVehicle();
                if (vehicle == null) RecoverFromVehicle();
                else
                {
                    animations.Simulate(Vector2.zero, false, false, DogAction.None, false, 0, Runner.DeltaTime);
                    transform.SetPositionAndRotation(vehicle.Seat.position, vehicle.Seat.rotation);
                    vehicle.Drive(this, networkInput.Move, hasInput, networkInput.IsPressed(DogInputButtons.DropCarry));
                    return;
                }
            }
            ProcessIncomingImpacts();
            var knockedDown = UpdateKnockdownState();
            var previousPoopCooldown = animations.PoopCooldown;
            var previousPeeCooldown = animations.PeeCooldown;
            animations.Simulate(networkInput.Move, networkInput.Sprint != 0,
                networkInput.IsPressed(DogInputButtons.Jump), networkInput.Action,
                true, networkInput.Yaw, Runner.DeltaTime, knockedDown);
            DispatchPedestrianReactions();
            if (animations.PoopCooldown > previousPoopCooldown)
                PoopCooldownUntil = TickTimer.CreateFromSeconds(Runner, animations.PoopCooldown);
            if (animations.PeeCooldown > previousPeeCooldown)
                PeeCooldownUntil = TickTimer.CreateFromSeconds(Runner, animations.PeeCooldown);
            ResolvePlayerOverlap();
            MovementVelocity = motor.WorldVelocity;
            CaptureStackState();
            if (!KnockdownActive && reset == SandboxInteractionKind.None)
            {
                if (networkInput.IsPressed(DogInputButtons.DropCarry)) SubmitDropInteraction();
                else if (networkInput.IsPressed(DogInputButtons.PrimaryCarry)) SubmitPrimaryCarryInteraction();
            }
            if (SeatedVehicleId.IsValid) return;
            AnimationSpeed = Mathf.Round(animations.NormalizedSpeed * 100) * .01f;
            AnimationGrounded = motor.IsGrounded;
            if (AnimationSequence != animations.Sequence || AnimationAction != animations.CurrentAction)
            {
                AnimationSequence = animations.Sequence;
                AnimationAction = animations.CurrentAction;
                AnimationUntil = AnimationAction == DogAction.None ? TickTimer.None
                    : TickTimer.CreateFromSeconds(Runner, animations.RemainingSeconds);
            }
        }

        private void DispatchPedestrianReactions()
        {
            if (animations.CurrentAction == DogAction.Bite && animations.RemainingSeconds <= .72f
                && processedBite != animations.Sequence)
            {
                processedBite = animations.Sequence;
                NetworkCityPedestrian.TryBite(this);
            }
            if (animations.CurrentAction == DogAction.Bark && processedBark != animations.Sequence)
            {
                processedBark = animations.Sequence;
                foreach (var npc in NetworkCityPedestrian.Active)
                    if (npc.Runner == Runner) npc.HearBark(this);
            }
        }

        private void ResolvePlayerOverlap()
        {
            // The dog controllers ignore each other so dogs can jump onto each other's backs.
            // Keep their bodies separated horizontally unless one is standing on a support.
            if (motor.CurrentSupportOwner != null) return;
            var controller = Controller;
            if (controller == null || !controller.enabled) return;
            foreach (var other in ActivePlayers)
            {
                if (other == null || other == this || other.Runner != Runner || other.Object == null
                    || !other.Object.IsValid || other.SeatedVehicleId.IsValid || other.CarrierId.IsValid
                    || other.motor != null && other.motor.CurrentSupportOwner == transform)
                    continue;
                var correction = CalculatePlayerSeparation(transform.position, other.transform.position,
                    Object.InputAuthority.AsIndex, other.Object.InputAuthority.AsIndex);
                if (correction.sqrMagnitude > 0) controller.Move(correction);
            }
        }

        internal static Vector3 CalculatePlayerSeparation(Vector3 position, Vector3 otherPosition,
            int playerIndex, int otherPlayerIndex)
        {
            var delta = position - otherPosition;
            if (Mathf.Abs(delta.y) >= MaximumSeparationHeight) return Vector3.zero;
            delta.y = 0;
            var distance = delta.magnitude;
            if (distance >= MinimumPlayerSeparation) return Vector3.zero;
            var direction = distance > .0001f
                ? delta / distance
                : (playerIndex <= otherPlayerIndex ? Vector3.left : Vector3.right);
            return direction * ((MinimumPlayerSeparation - distance) * .5f);
        }

        private void SubmitPrimaryCarryInteraction()
        {
            var target = GetCarryTarget(out var carrying);
            if (target != null && !(carrying && target is NetworkDogPlayer))
                SubmitInteraction(target.Object.Id, carrying ? SandboxInteractionKind.Throw : SandboxInteractionKind.Pickup);
        }

        private void SubmitDropInteraction()
        {
            var target = GetCarryTarget(out var carrying);
            if (carrying) SubmitInteraction(target.Object.Id, SandboxInteractionKind.Drop);
            else if (InJail || CitySandbox?.CanUseJailLever(this) == true)
                SubmitInteraction(NetworkId.None, SandboxInteractionKind.UseJail);
            else NetworkCityVehicle.FindAvailable(this)?.TryEnter(this);
        }

        internal NetworkBehaviour GetCarryTarget(out bool carrying)
        {
            carrying = false;
            if (Object == null || !Object.IsValid || SeatedVehicleId.IsValid || KnockdownActive || CarrierId.IsValid) return null;
            var held = FindHeldTarget();
            if (held != null)
            {
                carrying = true;
                return held;
            }
            NetworkBehaviour nearest = null;
            var best = carryRange * carryRange;
            foreach (var prop in NetworkCarryable.Active)
                if (IsSameSession(prop) && !prop.IsCarried && (!prop.IsJailBowl || InJail))
                    Consider(prop, prop.transform.position);
            foreach (var npc in NetworkCityPedestrian.Active)
                if (IsSameSession(npc) && npc.CanBeCarried) Consider(npc, npc.CarryPoint);
            foreach (var dog in ActivePlayers)
                if (dog != this && IsSameSession(dog) && !dog.InJail && !InJail && dog.CanBeCarried)
                    Consider(dog, dog.CarryPoint);
            return nearest;

            void Consider(NetworkBehaviour candidate, Vector3 point)
            {
                var distance = (point - MouthAnchor.position).sqrMagnitude;
                if (distance >= best || !CanReachCarryTarget(candidate, point)) return;
                best = distance;
                nearest = candidate;
            }
        }

        private bool IsSameSession(NetworkBehaviour target) =>
            target != null && target.Object != null && target.Object.IsValid && target.Runner == Runner;

        internal NetworkBehaviour FindHeldTarget()
        {
            var id = Object.Id;
            foreach (var prop in NetworkCarryable.Active)
                if (IsSameSession(prop) && prop.CarrierId == id) return prop;
            foreach (var npc in NetworkCityPedestrian.Active)
                if (IsSameSession(npc) && npc.CarrierId == id) return npc;
            foreach (var dog in ActivePlayers)
                if (IsSameSession(dog) && dog.CarrierId == id) return dog;
            return null;
        }

        internal bool CanPickUp(NetworkBehaviour target, Vector3 point) => Object.HasStateAuthority
            && target != this && IsSameSession(target) && !SeatedVehicleId.IsValid && !KnockdownActive
            && !CarrierId.IsValid && FindHeldTarget() == null && CanReachCarryTarget(target, point);

        private bool CanReachCarryTarget(NetworkBehaviour target, Vector3 point)
        {
            var delta = point - MouthAnchor.position;
            var bearing = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
            if (delta.sqrMagnitude > carryRange * carryRange || Vector3.Dot(bearing.normalized, transform.forward) < .15f)
                return false;
            foreach (var hit in Physics.RaycastAll(MouthAnchor.position, delta.normalized, delta.magnitude,
                Physics.DefaultRaycastLayers | (1 << 2), QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(target.transform)) return false;
            return true;
        }

        internal bool TryFindBodyDrop(NetworkBehaviour target, Vector3 center, Vector3 half, Quaternion rotation, out Vector3 point)
        {
            for (var i = 0; i < 8; i++)
            {
                var direction = Quaternion.Euler(0, i * 45, 0) * transform.forward;
                var probe = transform.position + direction * (1.1f + half.z) + Vector3.up * .8f;
                if (!Physics.Raycast(probe, Vector3.down, out var ground, 1.8f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || ground.normal.y < .65f) continue;
                point = ground.point + Vector3.up * .035f;
                if (Physics.Linecast(transform.position + Vector3.up * .55f, point + Vector3.up * .55f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (CanPlaceBody(target.transform, point, center, half, rotation)) return true;
            }
            point = default;
            return false;
        }

        private static bool CanPlaceBody(Transform target, Vector3 point, Vector3 center, Vector3 half, Quaternion rotation)
        {
            foreach (var collider in Physics.OverlapBox(point + rotation * center, half * .97f, rotation,
                Physics.DefaultRaycastLayers | (1 << 2), QueryTriggerInteraction.Ignore))
                if (!collider.transform.IsChildOf(target)) return false;
            return true;
        }

        internal bool TryBePickedUp(NetworkDogPlayer dog)
        {
            if (InJail || dog.InJail || !CanBeCarried || !dog.CanPickUp(this, CarryPoint)) return false;
            lastPronePose = new Pose(transform.position, transform.rotation);
            CarrierId = dog.Object.Id;
            motor.ClearMotion();
            CaptureStackState();
            MovementVelocity = Vector3.zero;
            foreach (var collider in seatColliders) collider.enabled = false;
            FollowCarrier(dog);
            return true;
        }

        internal void ReleaseFromCarrier(NetworkDogPlayer dog, bool escape)
        {
            if (!Object.HasStateAuthority || !CarrierId.IsValid || (dog != null && CarrierId != dog.Object.Id)) return;
            var pose = lastPronePose;
            var controller = Controller;
            var half = new Vector3(controller.radius, controller.height * .5f, controller.radius);
            if (dog != null && dog.TryFindBodyDrop(this, controller.center, half, dog.transform.rotation, out var point))
                pose = new Pose(point, dog.transform.rotation);
            else if (!CanPlaceBody(transform, pose.position, controller.center, half, pose.rotation))
            {
                motor.Respawn();
                pose = new Pose(transform.position, transform.rotation);
            }
            CarrierId = NetworkId.None;
            transformSync.Teleport(pose.position, pose.rotation);
            motor.ClearMotion();
            foreach (var collider in seatColliders) collider.enabled = true;
            if (escape)
            {
                KnockdownActive = SettledDown = false;
                CarryImmuneUntil = TickTimer.CreateFromSeconds(Runner, 2f);
            }
            else
            {
                SettledDown = false;
                stableDownSeconds = 0;
            }
        }

        private void FollowCarrier(NetworkDogPlayer dog) => transform.SetPositionAndRotation(
            dog.MouthAnchor.position + dog.transform.forward * .32f - Vector3.up * .24f, dog.transform.rotation);

        internal void ReleaseHeldTarget()
        {
            switch (FindHeldTarget())
            {
                case NetworkCarryable prop: prop.ForceDrop(this); break;
                case NetworkCityPedestrian npc: npc.Release(this, false); break;
                case NetworkDogPlayer dog: dog.ReleaseFromCarrier(this, false); break;
            }
        }

        private void SubmitInteraction(NetworkId target, SandboxInteractionKind kind)
        {
            InteractionTarget = target;
            InteractionKind = kind;
            InteractionRequestId = unchecked(InteractionRequestId + 1);
            if (InteractionRequestId <= 0) InteractionRequestId = 1;
        }

        public Vector3 FindDropPosition(NetworkCarryable carryable)
        {
            return MouthAnchor.position + transform.forward * .75f + Vector3.up * .2f;
        }

        public const string NamesPreferenceKey = "DogSimulator.ShowPlayerNames";
        private const float NameplateFadeStart = 18f;
        private const float NameplateFadeEnd = 24f;
        public static bool ShowNameplates { get; set; } = true;

        public override void Render()
        {
            ApplyStackPresentation();
            if (renderedModelId != DogModelId)
            {
                renderedModelId = DogModelId;
                animations.ApplyModel(modelCatalog, DogModelId);
                peePelvis = animations.Visual?.PeePelvis;
            }
            animations.PresentSnapshot(AnimationSpeed, AnimationGrounded, AnimationAction,
                AnimationSequence, Mathf.Max(0, AnimationUntil.RemainingTime(Runner) ?? 0));
            animations.PresentKnockdown(IsKnockedDown(), KnockdownDirection);
            if (nameLabel == null) return;
            var camera = ShowNameplates ? Camera.main : null;
            var distance = camera != null ? Vector3.Distance(camera.transform.position, nameLabel.transform.position) : 0f;
            nameLabel.enabled = camera != null && distance < NameplateFadeEnd;
            if (!nameLabel.enabled) return;
            var name = Nickname.ToString();
            if (renderedName != name) { nameLabel.text = name; renderedName = name; }
            nameLabel.alpha = 1f - Mathf.InverseLerp(NameplateFadeStart, NameplateFadeEnd, distance);
            nameLabel.transform.rotation = camera.transform.rotation;
        }

        private void LateUpdate()
        {
            if (Object != null && Object.IsValid)
            {
                var vehicle = GetSeatedVehicle();
                if (!Object.HasStateAuthority)
                {
                    if (presentedInSeat != (SeatedVehicleId.IsValid || CarrierId.IsValid))
                    {
                        presentedInSeat = SeatedVehicleId.IsValid || CarrierId.IsValid;
                        foreach (var collider in seatColliders)
                            if (collider is not CharacterController) collider.enabled = !presentedInSeat;
                    }
                    if (vehicle != null) transform.SetPositionAndRotation(vehicle.Seat.position, vehicle.Seat.rotation);
                    else if (CarrierId.IsValid && Runner.TryFindObject(CarrierId, out var carrier)
                        && carrier.TryGetComponent<NetworkDogPlayer>(out var dog)) FollowCarrier(dog);
                }
                followCamera?.Follow(vehicle != null ? vehicle.CameraTarget : transform);
            }
            // Sample after Animator evaluation so the stream follows the pose displayed on this peer.
            var animator = animations?.Visual?.Animator;
            var phase = -1f;
            if (Object != null && Object.IsValid && AnimationAction == DogAction.Pee
                && peePelvis != null && animator != null && animator.isActiveAndEnabled && !animator.IsInTransition(0))
            {
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (state.shortNameHash == PeeState) phase = state.normalizedTime;
            }

            if (phase < PeeEmissionStart || phase >= PeeLegLowerStart)
            {
                if (peeStream != null && (!peeStream.isStopped || peeStream.particleCount > 0))
                    peeStream.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                return;
            }
            if (phase >= PeeEmissionEnd)
            {
                if (peeStream != null && peeStream.isEmitting)
                    peeStream.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                return;
            }

            if (peeMaterial == null) return;
            if (peeStream == null) return;
            var outlet = peePelvis.position + animator.transform.TransformVector(PeeOutletOffset);
            peeStream.transform.SetPositionAndRotation(outlet, animator.transform.rotation * PeeAim);
            if (!peeStream.isEmitting) peeStream.Play();
        }

        private void CaptureStackState()
        {
            if (motor.TryGetSupportAnchor(out var supportOwner, out var localPosition,
                    out var localRotation)
                && supportOwner.TryGetComponent<NetworkDogPlayer>(out var supportPlayer)
                && supportPlayer.Object != null && supportPlayer.Object.IsValid)
            {
                StackSupportId = supportPlayer.Object.Id;
                StackLocalPosition = localPosition;
                StackLocalRotation = localRotation;
                return;
            }

            StackSupportId = NetworkId.None;
            StackLocalPosition = Vector3.zero;
            StackLocalRotation = Quaternion.identity;
        }

        private void ApplyStackPresentation()
        {
            if (Object == null || Object.HasStateAuthority || !StackSupportId.IsValid
                || !Runner.TryFindObject(StackSupportId, out var supportObject)) return;
            var support = supportObject.TryGetComponent<NetworkDogPlayer>(out var player) ? player.backSupport : null;
            if (support == null) return;
            var supportTransform = support.transform;
            transform.SetPositionAndRotation(
                supportTransform.TransformPoint(StackLocalPosition),
                supportTransform.rotation * StackLocalRotation);
        }

        private bool IsKnockedDown() => KnockdownActive;

        private bool UpdateKnockdownState()
        {
            if (!KnockdownActive) return false;

            var velocity = motor.WorldVelocity;
            var horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;
            var stillHasInertia = !motor.IsGrounded
                || horizontalSpeed > knockdownSettleSpeed
                || Mathf.Abs(velocity.y) > knockdownSettleSpeed;
            if (stillHasInertia)
            {
                SettledDown = false;
                stableDownSeconds = 0;
                return true;
            }
            stableDownSeconds += Runner.DeltaTime;
            if (stableDownSeconds < .2f) return true;
            if (!SettledDown)
            {
                SettledDown = true;
                KnockdownUntil = TickTimer.CreateFromSeconds(Runner, Mathf.Max(1.35f, minimumKnockdownSeconds));
            }
            if (!KnockdownUntil.Expired(Runner)) return true;
            KnockdownActive = SettledDown = false;
            return false;
        }

        private void OnControllerImpact(Collider other, Vector3 velocity)
        {
            if (CitySandbox != null && !InJail && velocity.sqrMagnitude >= 36f
                && other.GetComponentInParent<NetworkCityPedestrian>() is { } npc && npc.Runner == Runner
                && npc.IsStanding)
                npc.QueueHit(velocity, velocity.magnitude, Object.Id, AnnoyingAction.Ram);
            if (Time.unscaledTime < nextBodyImpactTime) return;
            var target = other.GetComponentInParent<NetworkDogPlayer>();
            if (target == null || target == this || target.Runner != Runner || target.CarrierId.IsValid) return;
            var speed = velocity.magnitude;
            if (speed < 1.5f) return;
            var away = Vector3.ProjectOnPlane(target.transform.position - transform.position, Vector3.up).normalized;
            if (away.sqrMagnitude < .01f) away = transform.forward;
            ImpactTargetId = target.Object.Id;
            ImpactImpulse = away * Mathf.Min(12f, speed * .7f)
                + Vector3.up * Mathf.Clamp(speed * .28f, .6f, 6f);
            ImpactRequestId = unchecked(ImpactRequestId + 1);
            if (ImpactRequestId <= 0) ImpactRequestId = 1;
            nextBodyImpactTime = Time.unscaledTime + .18f;
        }

        private void ProcessIncomingImpacts()
        {
            foreach (var source in ActivePlayers)
            {
                if (source == null || source.Runner != Runner || source.Object == null
                    || source.ImpactTargetId != Object.Id || source.ImpactRequestId <= 0) continue;
                if (processedImpacts.TryGetValue(source.Object.Id, out var processed)
                    && processed >= source.ImpactRequestId) continue;
                processedImpacts[source.Object.Id] = source.ImpactRequestId;
                ApplyImpact(source.ImpactImpulse, false);
            }
            foreach (var source in NetworkCarryable.Active)
            {
                if (source == null || source.Runner != Runner || source.Object == null
                    || source.ImpactTargetId != Object.Id || source.ImpactRequestId <= 0) continue;
                if (processedImpacts.TryGetValue(source.Object.Id, out var processed)
                    && processed >= source.ImpactRequestId) continue;
                processedImpacts[source.Object.Id] = source.ImpactRequestId;
                ApplyImpact(source.ImpactImpulse, source.ImpactForcesKnockdown);
            }
        }

        private void ApplyImpact(Vector3 impulse, bool forceKnockdown)
        {
            impulse = Vector3.ClampMagnitude(impulse, 18f);
            motor.ApplyKnockback(impulse);
            if (!forceKnockdown && impulse.magnitude < 7.5f) return;
            BeginKnockdown(impulse.normalized);
        }

        private void OnBodyPush(Rigidbody body, Vector3 impulse)
        {
            if (Object == null || !Object.HasStateAuthority || body.isKinematic) return;
            if (body.TryGetComponent<NetworkCityVehicle>(out var vehicle)) vehicle.ResistDogPush(impulse);
            else if (body.TryGetComponent<NetworkCarryable>(out var prop) && !prop.IsCarried)
                body.AddForce(impulse, ForceMode.Impulse);
        }

        private NetworkCityVehicle GetSeatedVehicle()
        {
            return SeatedVehicleId.IsValid && Runner.TryFindObject(SeatedVehicleId, out var vehicle)
                ? vehicle.GetComponent<NetworkCityVehicle>() : null;
        }

        internal string VehiclePrompt()
        {
            if (!SeatedVehicleId.IsValid)
                return NetworkCityVehicle.FindAvailable(this) != null ? "Enter vehicle" : null;
            if (!VehicleExitHintUntil.ExpiredOrNotRunning(Runner))
                return VehicleExitHint == 1 ? "Slow down to exit" : "Both exits blocked";
            return "Exit vehicle";
        }

        internal void SetVehicleExitHint(byte reason)
        {
            VehicleExitHint = reason;
            VehicleExitHintUntil = TickTimer.CreateFromSeconds(Runner, 2f);
        }

        internal bool TrySit(NetworkCityVehicle vehicle)
        {
            if (!Object.HasStateAuthority || InJail || SeatedVehicleId.IsValid || KnockdownActive || CarrierId.IsValid || FindHeldTarget() != null) return false;
            SeatedVehicleId = vehicle.Object.Id;
            motor.ClearMotion();
            CaptureStackState();
            foreach (var collider in seatColliders) collider.enabled = false;
            animations.ClearAction();
            AnimationSequence = animations.Sequence;
            AnimationAction = DogAction.None;
            AnimationUntil = TickTimer.None;
            AnimationGrounded = true;
            AnimationSpeed = 0;
            MovementVelocity = Vector3.zero;
            transform.SetPositionAndRotation(vehicle.Seat.position, vehicle.Seat.rotation);
            return true;
        }

        internal void LeaveVehicle(Vector3 position, Quaternion rotation)
        {
            transformSync.Teleport(position, rotation);
            RestoreWalking();
        }

        internal void RecoverFromVehicle()
        {
            motor.Respawn();
            transformSync.Teleport(transform.position, transform.rotation);
            RestoreWalking();
        }

        private void RestoreWalking()
        {
            SeatedVehicleId = NetworkId.None;
            VehicleExitHintUntil = TickTimer.None;
            motor.ClearMotion();
            foreach (var collider in seatColliders) collider.enabled = true;
        }

        internal void ApplyWorldImpact(Vector3 impulse, bool forceKnockdown)
        {
            if (Object.HasStateAuthority && !SeatedVehicleId.IsValid && !CarrierId.IsValid) ApplyImpact(impulse, forceKnockdown);
        }

        private void OnHardLanding(float impactSpeed)
        {
            if (impactSpeed < 9f) return;
            if (followCamera != null) followCamera.ShakeOnLanding(impactSpeed);
            else if (Object.InputAuthority.IsRealPlayer && !Object.HasInputAuthority)
                RPC_ShakeOnLanding(impactSpeed);
            BeginKnockdown(transform.right);
        }

        private void BeginKnockdown(Vector3 direction)
        {
            ReleaseHeldTarget();
            KnockdownDirection = direction;
            KnockdownActive = true;
            SettledDown = false;
            stableDownSeconds = 0;
            KnockdownUntil = TickTimer.None;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority,
            Channel = RpcChannel.Unreliable, TickAligned = false)]
        private void RPC_ShakeOnLanding(float impactSpeed) => followCamera?.ShakeOnLanding(impactSpeed);

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            ActivePlayers.Remove(this);
            if (motor != null) motor.ControllerImpact -= OnControllerImpact;
            if (motor != null) motor.BodyPush -= OnBodyPush;
            if (motor != null) motor.HardLanding -= OnHardLanding;
            if (input != null) input.SetGameplayEnabled(false);
            if (animations != null) animations.ResetPresentation();
            if (followCamera != null) followCamera.Unbind();
        }

        private void OnDestroy() => ActivePlayers.Remove(this);
    }
}
