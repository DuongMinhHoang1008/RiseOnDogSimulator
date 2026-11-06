using UnityEngine;

namespace DogSimulator.Player
{
    // Offline counterpart to the Fusion State Authority driver.
    [RequireComponent(typeof(DogMotor), typeof(DogInputReader), typeof(DogAnimationDriver))]
    public sealed class LocalMovementPreview : MonoBehaviour
    {
        [SerializeField] private DogCamera followCamera;
        private DogMotor motor;
        private DogInputReader input;
        private DogAnimationDriver animations;

        private void Awake()
        {
            motor = GetComponent<DogMotor>();
            input = GetComponent<DogInputReader>();
            animations = GetComponent<DogAnimationDriver>();
        }

        private void FixedUpdate()
        {
            if (!input.GameplayEnabled || followCamera == null) { animations.ResetPresentation(); return; }
            animations.Simulate(input, followCamera.Yaw, Time.fixedDeltaTime);
        }

        private void LateUpdate() => animations.PresentLocal();
    }
}
