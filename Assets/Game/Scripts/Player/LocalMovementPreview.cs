using UnityEngine;

namespace DogSimulator.Player
{
    // Offline counterpart to the Fusion State Authority driver.
    [RequireComponent(typeof(DogMotor), typeof(DogInputReader), typeof(DogAnimationDriver))]
    public sealed class LocalMovementPreview : MonoBehaviour
    {
        [SerializeField] private DogCamera followCamera;
        [SerializeField] private DogInputReader input;
        [SerializeField] private DogAnimationDriver animations;

        private void FixedUpdate()
        {
            if (!input.GameplayEnabled || followCamera == null)
            {
                animations.ResetPresentation();
                return;
            }

            animations.Simulate(input, followCamera.Yaw, Time.fixedDeltaTime);
        }

        private void LateUpdate() => animations.PresentLocal();
    }
}
