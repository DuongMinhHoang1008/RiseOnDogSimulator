using System;
using System.Collections.Generic;
using UnityEngine;

namespace DogSimulator.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class DogMotor : MonoBehaviour
    {
        [SerializeField] private DogMovementConfig config;
        [SerializeField] private CharacterController controller;
        [SerializeField] private Collider[] ownColliders = Array.Empty<Collider>();
        public CharacterController Controller => controller;
        private float verticalSpeed;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private DogBackSupport currentSupport;
        private Vector3 supportLocalPosition;
        private Quaternion supportLocalRotation;
        private Vector3 externalVelocity;
        private Vector3 simulatedVelocity;
        private bool bouncedThisStep;
        private float cushionedLandingSpeed;
        private readonly Dictionary<Rigidbody, Vector3> pushContacts = new();
        private Vector3 pushDirection;
        private bool collectPushContacts;
        [SerializeField, Range(2, 4)] private int maxStackDepth = 3;

        public int RespawnSerial { get; private set; }
        public float VerticalSpeed => verticalSpeed;
        public bool JumpedThisStep { get; private set; }
        public float NormalizedSpeed { get; private set; }
        public bool IsGrounded => controller != null && controller.isGrounded;
        public Transform CurrentSupportOwner => currentSupport != null ? currentSupport.OwnerRoot : null;
        public int SupportDepth { get; private set; }
        public Vector3 WorldVelocity { get; private set; }
        public event Action<Collider, Vector3> ControllerImpact;
        public event Action<Rigidbody, Vector3> BodyPush;
        public event Action<float> HardLanding;

        public bool TryGetSupportAnchor(out Transform owner, out Vector3 localPosition,
            out Quaternion localRotation)
        {
            owner = currentSupport != null ? currentSupport.OwnerRoot : null;
            localPosition = supportLocalPosition;
            localRotation = supportLocalRotation;
            return owner != null;
        }

        private void Awake()
        {
            CaptureRespawnPose();
            foreach (var ownCollider in ownColliders)
                if (ownCollider != controller) Physics.IgnoreCollision(controller, ownCollider, true);
        }

        public void CaptureRespawnPose()
        {
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        // No Update/FixedUpdate here. Only the authoritative driver advances this motor.
        public void Simulate(Vector2 input, bool running, bool jump, float yaw, float deltaTime)
        {
            JumpedThisStep = false;
            bouncedThisStep = false;
            NormalizedSpeed = 0;
            if (config == null || controller == null || !controller.enabled || deltaTime <= 0) return;
            var startPosition = transform.position;
            ApplySupportAnchor();
            var direction = Quaternion.Euler(0, yaw, 0)
                * new Vector3(Mathf.Clamp(input.x, -1, 1), 0, Mathf.Clamp(input.y, -1, 1));
            direction = Vector3.ClampMagnitude(direction, 1);
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(direction), config.TurnSpeed * deltaTime);

            var isAttached = currentSupport != null;
            var isSupported = controller.isGrounded || isAttached;
            if (isAttached) verticalSpeed = 0;
            else if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            if (jump && isSupported)
            {
                ClearSupport();
                verticalSpeed = Mathf.Sqrt(2 * config.Gravity * config.JumpHeight);
                JumpedThisStep = true;
            }
            if (currentSupport == null)
                verticalSpeed = Mathf.Max(verticalSpeed - config.Gravity * deltaTime, -config.TerminalSpeed);
            var speed = running ? config.RunSpeed : config.WalkSpeed;
            simulatedVelocity = direction * speed + Vector3.up * verticalSpeed + externalVelocity;
            var landingSpeed = Mathf.Max(0, -verticalSpeed);
            var wasGrounded = controller.isGrounded;
            pushContacts.Clear();
            pushDirection = direction * speed;
            collectPushContacts = BodyPush != null;
            var collisions = controller.Move(simulatedVelocity * deltaTime);
            collectPushContacts = false;
            foreach (var contact in pushContacts)
                BodyPush?.Invoke(contact.Key, contact.Value * Mathf.Min(deltaTime * 24f, .8f));
            externalVelocity = Vector3.MoveTowards(externalVelocity, Vector3.zero, 7f * deltaTime);
            var travelled = transform.position - startPosition;
            WorldVelocity = travelled / deltaTime;
            NormalizedSpeed = Mathf.Clamp01(new Vector2(travelled.x, travelled.z).magnitude / deltaTime / config.RunSpeed);
            if ((collisions & CollisionFlags.Above) != 0 && verticalSpeed > 0) verticalSpeed = 0;
            if (!wasGrounded && !bouncedThisStep && (collisions & CollisionFlags.Below) != 0)
            {
                if (landingSpeed >= Mathf.Max(9f, cushionedLandingSpeed))
                    HardLanding?.Invoke(landingSpeed);
                cushionedLandingSpeed = 0;
            }
            RefreshSupport();
            UpdateSupportAnchorFromCurrentPose();
            if (transform.position.y < config.RespawnBelowY) Respawn();
        }

        private void RefreshSupport()
        {
            var origin = transform.position + Vector3.up * 0.22f;
            var radius = Mathf.Max(0.08f, controller.radius * 0.65f);
            DogBackSupport support = null;
            var nearestDistance = float.PositiveInfinity;
            var hits = Physics.SphereCastAll(origin, radius, Vector3.down, 0.42f,
                ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.distance >= nearestDistance
                    || !hit.collider.TryGetComponent<DogBackSupport>(out var candidate)
                    || !CanUseSupport(candidate)) continue;
                support = candidate;
                nearestDistance = hit.distance;
            }

            if (support != null)
            {
                if (currentSupport != support)
                {
                    ClearSupport();
                    currentSupport = support;
                    CaptureSupportAnchor();
                    SetSupportCollisionsIgnored(support, true);
                }
                SupportDepth = (support.OwnerMotor?.SupportDepth ?? 0) + 1;
                return;
            }
            ClearSupport();
        }

        private void ApplySupportAnchor()
        {
            if (currentSupport == null) return;
            var supportTransform = currentSupport.transform;
            var targetPosition = supportTransform.TransformPoint(supportLocalPosition);
            var targetRotation = supportTransform.rotation * supportLocalRotation;
            var positionDelta = targetPosition - transform.position;

            // Rebuild the exact world pose from the support-local anchor. The avatar remains a
            // root NetworkTransform, while its own movement updates only the relative offset.
            if (positionDelta.sqrMagnitude > 0.0000001f) controller.Move(positionDelta);
            transform.rotation = targetRotation;
        }

        private void CaptureSupportAnchor()
        {
            if (currentSupport == null) return;
            var supportTransform = currentSupport.transform;
            supportLocalPosition = supportTransform.InverseTransformPoint(transform.position);
            supportLocalRotation = Quaternion.Inverse(supportTransform.rotation) * transform.rotation;
        }

        private void UpdateSupportAnchorFromCurrentPose()
        {
            if (currentSupport == null) return;
            var supportTransform = currentSupport.transform;
            var movedLocalPosition = supportTransform.InverseTransformPoint(transform.position);
            supportLocalPosition.x = movedLocalPosition.x;
            supportLocalPosition.z = movedLocalPosition.z;
            supportLocalRotation = Quaternion.Inverse(supportTransform.rotation) * transform.rotation;
        }

        private bool CanUseSupport(DogBackSupport support)
        {
            if (support == null || support.OwnerRoot == null || support.OwnerRoot == transform) return false;
            var ownerMotor = support.OwnerMotor;
            if (ownerMotor != null && ownerMotor.SupportDepth >= maxStackDepth - 1) return false;
            for (var cursor = ownerMotor; cursor != null;)
            {
                if (cursor == this) return false;
                cursor = cursor.currentSupport?.OwnerMotor;
            }
            return true;
        }

        private void ClearSupport()
        {
            if (currentSupport != null) SetSupportCollisionsIgnored(currentSupport, false);
            currentSupport = null;
            supportLocalPosition = Vector3.zero;
            supportLocalRotation = Quaternion.identity;
            SupportDepth = 0;
        }

        private void SetSupportCollisionsIgnored(DogBackSupport support, bool ignored)
        {
            if (support == null || support.OwnerMotor == null) return;
            foreach (var supportCollider in support.OwnerMotor.ownColliders)
            {
                if (supportCollider == null || supportCollider == controller
                    || supportCollider.transform.IsChildOf(transform)) continue;
                Physics.IgnoreCollision(controller, supportCollider, ignored);
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit == null) return;
            if (hit.normal.y >= .5f && verticalSpeed <= 0
                && hit.collider.TryGetComponent<BouncePad>(out var pad))
            {
                ClearSupport();
                verticalSpeed = pad.BounceSpeed;
                cushionedLandingSpeed = pad.CushionLanding ? pad.BounceSpeed + 1f : 0;
                JumpedThisStep = true;
                bouncedThisStep = true;
                return;
            }
            if (hit.normal.y < .65f && simulatedVelocity.sqrMagnitude >= 2.25f)
                ControllerImpact?.Invoke(hit.collider, simulatedVelocity);
            if (!collectPushContacts || Mathf.Abs(hit.normal.y) >= .55f || hit.rigidbody == null
                || hit.rigidbody.isKinematic) return;
            var inward = -Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
            var speed = Vector3.Dot(pushDirection, inward);
            if (speed <= .1f) return;
            var impulse = inward * Mathf.Min(speed, 5f);
            if (!pushContacts.TryGetValue(hit.rigidbody, out var previous) || previous.sqrMagnitude < impulse.sqrMagnitude)
                pushContacts[hit.rigidbody] = impulse;
        }

        public void ClearMotion()
        {
            ClearSupport();
            verticalSpeed = 0;
            NormalizedSpeed = 0;
            JumpedThisStep = false;
            WorldVelocity = Vector3.zero;
            cushionedLandingSpeed = 0;
            externalVelocity = simulatedVelocity = Vector3.zero;
        }

        public void ApplyKnockback(Vector3 impulse)
        {
            ClearSupport();
            cushionedLandingSpeed = 0;
            externalVelocity += new Vector3(impulse.x, 0, impulse.z);
            externalVelocity = Vector3.ClampMagnitude(externalVelocity, 15f);
            verticalSpeed = Mathf.Max(verticalSpeed, Mathf.Clamp(impulse.y, 0, 10f));
        }

        public void Respawn()
        {
            if (controller == null) return;
            RespawnSerial++;
            var wasEnabled = controller.enabled;
            controller.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            ClearMotion();
            controller.enabled = wasEnabled;
        }
    }
}
