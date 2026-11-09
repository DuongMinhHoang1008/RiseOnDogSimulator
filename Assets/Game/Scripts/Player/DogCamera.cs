using UnityEngine;

namespace DogSimulator.Player
{
    public sealed class DogCamera : MonoBehaviour
    {
        public const string ShakePreferenceKey = "DogSimulator.CameraShake";
        public static bool ShakeEnabled { get; set; } = true;
        [SerializeField] private Transform target;
        [SerializeField] private DogInputReader input;
        [SerializeField] private DogMovementConfig config;
        [SerializeField] private LayerMask obstructionMask = ~(1 << 2);
        private float pitch = 20;
        private float shake;
        public float Yaw { get; private set; }

        public void Bind(Transform avatar, DogInputReader reader)
        {
            target = avatar;
            input = reader;
            Yaw = avatar != null ? avatar.eulerAngles.y : 0;
            pitch = 20;
            shake = 0;
        }

        public void Unbind()
        {
            target = null;
            input = null;
            shake = 0;
        }

        public void Follow(Transform nextTarget) => target = nextTarget;

        public void ShakeOnLanding(float speed)
        {
            if (ShakeEnabled) shake = Mathf.InverseLerp(8f, 25f, speed) * .12f;
        }

        private void LateUpdate()
        {
            if (target == null || input == null || config == null) return;
            if (input.GameplayEnabled && Cursor.lockState == CursorLockMode.Locked)
            {
                Yaw = Mathf.Repeat(Yaw + input.Look.x * config.MouseSensitivity, 360);
                pitch = Mathf.Clamp(pitch - input.Look.y * config.MouseSensitivity,
                    config.CameraMinPitch, config.CameraMaxPitch);
            }

            var pivot = target.position + Vector3.up * config.CameraTargetHeight;
            var rotation = Quaternion.Euler(pitch, Yaw, 0);
            var backward = rotation * Vector3.back;
            var distance = config.CameraDistance;
            if (Physics.SphereCast(pivot, config.CameraCollisionRadius, backward,
                out var hit, distance, obstructionMask, QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(0, hit.distance - config.CameraCollisionRadius);
            var shakeOffset = ShakeEnabled ? Vector3.up * (Mathf.Sin(Time.unscaledTime * 63) * shake) : Vector3.zero;
            shake = Mathf.MoveTowards(shake, 0, Time.unscaledDeltaTime * .35f);
            transform.SetPositionAndRotation(pivot + backward * distance + shakeOffset, rotation);
        }
    }
}
