using System.Collections.Generic;
using DogSimulator.Player;
using Fusion;
using TMPro;
using UnityEngine;

namespace DogSimulator.Networking
{
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform))]
    [RequireComponent(typeof(DogMotor), typeof(DogInputReader), typeof(DogAnimationDriver))]
    public sealed class NetworkDogPlayer : NetworkBehaviour
    {
        private static readonly List<NetworkDogPlayer> ActivePlayers = new();
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private DogModelCatalog modelCatalog;
        [SerializeField, Min(.5f)] private float carryRange = 2.2f;
        [SerializeField, Min(.1f)] private float minimumKnockdownSeconds = .8f;
        [SerializeField, Min(0)] private float knockdownSettleSpeed = .45f;
        [Networked] public NetworkString<_32> Nickname { get; private set; }
        // Model 1 is the supplied rigged visual. Selection/reservations remain separate work.
        [Networked] public int DogModelId { get; private set; }
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
        private DogMotor motor;
        private DogInputReader input;
        private DogCamera followCamera;
        private string renderedName;
        private DogAnimationDriver animations;
        private int renderedModelId = int.MinValue;
        private Transform mouthAnchor;
        private float nextBodyImpactTime;
        private readonly Dictionary<NetworkId, int> processedImpacts = new();

        public static IReadOnlyList<NetworkDogPlayer> Active => ActivePlayers;
        public Transform MouthAnchor => mouthAnchor != null ? mouthAnchor : transform;
        public float CarryRange => carryRange;

        public bool IntersectsBodySweep(Vector3 start, Vector3 end, float sweepRadius)
        {
            var controller = GetComponent<CharacterController>();
            if (controller == null) return false;
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
            motor = GetComponent<DogMotor>();
            input = GetComponent<DogInputReader>();
            animations = GetComponent<DogAnimationDriver>();
            motor.ControllerImpact += OnControllerImpact;
            motor.HardLanding += OnHardLanding;
            mouthAnchor = transform.Find("MouthAnchor");
            if (!ActivePlayers.Contains(this)) ActivePlayers.Add(this);
            input.SetGameplayEnabled(false);
            input.enabled = Object.HasStateAuthority;
            GetComponent<CharacterController>().enabled = Object.HasStateAuthority;
        }

        public void InitializeOwner(string name, int modelId, DogCamera camera)
        {
            if (!Object.HasStateAuthority) return;
            if (!GuestProfile.TryNormalizeName(name, out var normalized, out var error))
                throw new System.ArgumentException(error);
            Nickname = normalized;
            SetModel(modelId);
            followCamera = camera;
            camera.Bind(transform, input);
        }

        public void InitializeOwner(string name, DogCamera camera)
            => InitializeOwner(name, modelCatalog != null ? modelCatalog.FallbackModelId : 1, camera);

        public void SetModel(int modelId)
        {
            if (!Object.HasStateAuthority || modelCatalog == null || !modelCatalog.IsValidModelId(modelId)) return;
            DogModelId = modelId;
        }

        public void SetControlsEnabled(bool value)
        {
            if (input != null && Object != null && Object.HasStateAuthority)
                input.SetGameplayEnabled(value);
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority || input == null || followCamera == null) return;
            ProcessIncomingImpacts();
            var knockedDown = UpdateKnockdownState();
            animations.Simulate(input, followCamera.Yaw, Runner.DeltaTime, knockedDown);
            MovementVelocity = motor.WorldVelocity;
            CaptureStackState();
            if (input.ConsumePrimaryCarry()) SubmitPrimaryCarryInteraction();
            if (input.ConsumeDropCarry()) SubmitDropInteraction();
            if (input.ConsumeResetYard()) SubmitInteraction(NetworkId.None, SandboxInteractionKind.ResetYard);
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

        private void SubmitPrimaryCarryInteraction()
        {
            for (var i = 0; i < NetworkCarryable.Active.Count; i++)
            {
                var carried = NetworkCarryable.Active[i];
                if (carried != null && carried.Runner == Runner && carried.CarrierId == Object.Id)
                { SubmitInteraction(carried.Object.Id, SandboxInteractionKind.Throw); return; }
            }
            NetworkCarryable nearest = null;
            var best = carryRange * carryRange;
            for (var i = 0; i < NetworkCarryable.Active.Count; i++)
            {
                var candidate = NetworkCarryable.Active[i];
                if (candidate == null || candidate.Runner != Runner || candidate.IsCarried) continue;
                var distance = (candidate.transform.position - MouthAnchor.position).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                nearest = candidate;
            }
            if (nearest != null) SubmitInteraction(nearest.Object.Id, SandboxInteractionKind.Pickup);
        }

        private void SubmitDropInteraction()
        {
            for (var i = 0; i < NetworkCarryable.Active.Count; i++)
            {
                var carried = NetworkCarryable.Active[i];
                if (carried != null && carried.Runner == Runner && carried.CarrierId == Object.Id)
                { SubmitInteraction(carried.Object.Id, SandboxInteractionKind.Drop); return; }
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

        public override void Render()
        {
            ApplyStackPresentation();
            if (renderedModelId != DogModelId)
            {
                renderedModelId = DogModelId;
                animations.ApplyModel(modelCatalog, DogModelId);
            }
            animations.PresentSnapshot(AnimationSpeed, AnimationGrounded, AnimationAction,
                AnimationSequence, Mathf.Max(0, AnimationUntil.RemainingTime(Runner) ?? 0));
            animations.PresentKnockdown(IsKnockedDown(), KnockdownDirection);
            if (nameLabel == null) return;
            var name = Nickname.ToString();
            if (renderedName != name) { nameLabel.text = name; renderedName = name; }
            var camera = Camera.main;
            if (camera != null) nameLabel.transform.rotation = camera.transform.rotation;
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
            var support = supportObject.GetComponentInChildren<DogBackSupport>(true);
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
            if ((KnockdownUntil.RemainingTime(Runner) ?? 0) > 0) return true;

            var velocity = motor.WorldVelocity;
            var horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;
            var stillHasInertia = !motor.IsGrounded
                || horizontalSpeed > knockdownSettleSpeed
                || Mathf.Abs(velocity.y) > knockdownSettleSpeed;
            if (stillHasInertia)
            {
                // Keep a short rolling window alive until the knockback has actually settled.
                KnockdownUntil = TickTimer.CreateFromSeconds(Runner, .15f);
                return true;
            }

            KnockdownActive = false;
            return false;
        }

        private void OnControllerImpact(Collider other, Vector3 velocity)
        {
            if (Time.unscaledTime < nextBodyImpactTime) return;
            var target = other.GetComponentInParent<NetworkDogPlayer>();
            if (target == null || target == this || target.Runner != Runner) return;
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
            KnockdownDirection = impulse.normalized;
            KnockdownActive = true;
            KnockdownUntil = TickTimer.CreateFromSeconds(Runner, minimumKnockdownSeconds);
        }

        private void OnHardLanding(float impactSpeed)
        {
            if (impactSpeed < 9f) return;
            KnockdownDirection = transform.right;
            KnockdownActive = true;
            KnockdownUntil = TickTimer.CreateFromSeconds(Runner, minimumKnockdownSeconds);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            ActivePlayers.Remove(this);
            if (motor != null) motor.ControllerImpact -= OnControllerImpact;
            if (motor != null) motor.HardLanding -= OnHardLanding;
            if (input != null) input.SetGameplayEnabled(false);
            if (animations != null) animations.ResetPresentation();
            if (followCamera != null) followCamera.Unbind();
        }

        private void OnDestroy() => ActivePlayers.Remove(this);
    }
}
