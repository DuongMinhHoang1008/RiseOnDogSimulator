using UnityEngine;

namespace DogSimulator.Player
{
    public sealed class DogCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private DogInputReader input;
        [SerializeField] private DogMovementConfig config;
        [SerializeField] private LayerMask obstructionMask = ~(1 << 2);
        private float pitch = 20;
        public float Yaw { get; private set; }

        public void Bind(Transform avatar, DogInputReader reader)
        {
            target = avatar;
            input = reader;
            Yaw = avatar != null ? avatar.eulerAngles.y : 0;
            pitch = 20;
        }

        public void Unbind()
        {
            target = null;
            input = null;
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
            transform.SetPositionAndRotation(pivot + backward * distance, rotation);
        }
    }
}
