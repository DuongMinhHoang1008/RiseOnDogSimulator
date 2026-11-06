using UnityEngine;

namespace DogSimulator.Player
{
    [CreateAssetMenu(menuName = "Dog Simulator/Movement Config")]
    public sealed class DogMovementConfig : ScriptableObject
    {
        [SerializeField, Range(1, 8)] private float walkSpeed = 4.5f;
        [SerializeField, Range(1, 10)] private float runSpeed = 6.5f;
        [SerializeField, Range(0.1f, 3)] private float jumpHeight = 1.2f;
        [SerializeField, Range(5, 40)] private float gravity = 20;
        [SerializeField, Range(10, 60)] private float terminalSpeed = 35;
        [SerializeField, Range(90, 1080)] private float turnSpeed = 540;
        [SerializeField, Range(0.05f, 0.3f)] private float jumpBufferSeconds = 0.15f;
        [SerializeField, Range(0.05f, 0.3f)] private float actionBufferSeconds = 0.15f;
        [SerializeField, Range(-50, -2)] private float respawnBelowY = -8;
        [SerializeField, Range(0.02f, 0.5f)] private float mouseSensitivity = 0.12f;
        [SerializeField, Range(2, 8)] private float cameraDistance = 4.5f;
        [SerializeField, Range(0.5f, 3)] private float cameraTargetHeight = 1;
        [SerializeField, Range(0.1f, 0.5f)] private float cameraCollisionRadius = 0.2f;
        [SerializeField, Range(-80, 0)] private float cameraMinPitch = -20;
        [SerializeField, Range(5, 80)] private float cameraMaxPitch = 65;

        public float WalkSpeed => walkSpeed;
        public float RunSpeed => runSpeed;
        public float JumpHeight => jumpHeight;
        public float Gravity => gravity;
        public float TerminalSpeed => terminalSpeed;
        public float TurnSpeed => turnSpeed;
        public float JumpBufferSeconds => jumpBufferSeconds;
        public float ActionBufferSeconds => actionBufferSeconds;
        public float RespawnBelowY => respawnBelowY;
        public float MouseSensitivity => mouseSensitivity;
        public float CameraDistance => cameraDistance;
        public float CameraTargetHeight => cameraTargetHeight;
        public float CameraCollisionRadius => cameraCollisionRadius;
        public float CameraMinPitch => cameraMinPitch;
        public float CameraMaxPitch => cameraMaxPitch;

        private void OnValidate()
        {
            walkSpeed = Mathf.Clamp(walkSpeed, 1, 8);
            runSpeed = Mathf.Clamp(runSpeed, walkSpeed, 10);
            jumpHeight = Mathf.Clamp(jumpHeight, 0.1f, 3);
            gravity = Mathf.Clamp(gravity, 5, 40);
            terminalSpeed = Mathf.Clamp(terminalSpeed, 10, 60);
            turnSpeed = Mathf.Clamp(turnSpeed, 90, 1080);
            jumpBufferSeconds = Mathf.Clamp(jumpBufferSeconds, 0.05f, 0.3f);
            actionBufferSeconds = Mathf.Clamp(actionBufferSeconds, 0.05f, 0.3f);
            respawnBelowY = Mathf.Clamp(respawnBelowY, -50, -2);
            mouseSensitivity = Mathf.Clamp(mouseSensitivity, 0.02f, 0.5f);
            cameraDistance = Mathf.Clamp(cameraDistance, 2, 8);
            cameraTargetHeight = Mathf.Clamp(cameraTargetHeight, 0.5f, 3);
            cameraCollisionRadius = Mathf.Clamp(cameraCollisionRadius, 0.1f, 0.5f);
            cameraMinPitch = Mathf.Clamp(cameraMinPitch, -80, 0);
            cameraMaxPitch = Mathf.Clamp(cameraMaxPitch, 5, 80);
        }
    }
}
