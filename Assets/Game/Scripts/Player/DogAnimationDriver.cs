using UnityEngine;

namespace DogSimulator.Player
{
    [RequireComponent(typeof(DogMotor), typeof(DogInputReader))]
    public sealed class DogAnimationDriver : MonoBehaviour
    {
        public const string Controls = "WASD Move | Shift Run | Space Jump | Mouse Camera | Esc Menu\nLeft Click Pick Up/Throw | E Drop | R Reset Yard | F Bark | Right Click Bite | G Poop | H Pee";
        [SerializeField] private DogAnimationConfig config;
        [SerializeField] private DogVisualPresenter visual;
        private DogMotor motor;
        public DogAction CurrentAction { get; private set; }
        public int Sequence { get; private set; }
        public float RemainingSeconds { get; private set; }
        public float NormalizedSpeed { get; private set; }
        public DogVisualPresenter Visual => visual;

        private void Awake() => motor = GetComponent<DogMotor>();

        public void ApplyModel(DogModelCatalog catalog, int modelId) => visual?.ApplyModel(catalog, modelId);

        // Called exactly once by the local driver or the Fusion State Authority, never by a proxy.
        public void Simulate(DogInputReader input, float yaw, float deltaTime, bool knockedDown = false)
        {
            if (motor == null || input == null || config == null || deltaTime <= 0) return;
            RemainingSeconds = Mathf.Max(0, RemainingSeconds - deltaTime);
            if (CurrentAction != DogAction.None && CurrentAction != DogAction.Jump && RemainingSeconds <= 0) ClearAction();
            if (!input.GameplayEnabled) ClearAction();
            var jump = input.ConsumeJump();
            var requested = input.ConsumeAction();
            if (knockedDown)
            {
                ClearAction();
                motor.Simulate(Vector2.zero, false, false, yaw, deltaTime);
                NormalizedSpeed = 0;
                return;
            }
            var moving = input.Move.sqrMagnitude > .01f;
            if ((jump && motor.IsGrounded) || (moving && (CurrentAction == DogAction.Poop || CurrentAction == DogAction.Pee))) ClearAction();
            if (requested != DogAction.None && CurrentAction == DogAction.None && motor.IsGrounded
                && (!moving || (requested != DogAction.Poop && requested != DogAction.Pee))) Begin(requested);
            var locked = CurrentAction != DogAction.None && CurrentAction != DogAction.Jump;
            motor.Simulate(locked ? Vector2.zero : input.Move, input.Sprint, jump, yaw, deltaTime);
            if (motor.JumpedThisStep) Begin(DogAction.Jump);
            else if (CurrentAction == DogAction.Jump && motor.IsGrounded) ClearAction();
            NormalizedSpeed = input.GameplayEnabled ? motor.NormalizedSpeed : 0;
        }

        private void Begin(DogAction action)
        {
            CurrentAction = action;RemainingSeconds = config.Duration(action);
            unchecked { Sequence++; }
        }

        public void ClearAction()
        {
            if (CurrentAction != DogAction.None) unchecked { Sequence++; }
            CurrentAction = DogAction.None;RemainingSeconds = 0;
        }

        public void PresentLocal()
        {
            if (visual != null && motor != null)
                visual.Present(NormalizedSpeed, motor.IsGrounded, CurrentAction, Sequence, RemainingSeconds);
        }

        public void PresentSnapshot(float speed, bool grounded, DogAction action, int sequence, float remaining)
        {
            if (visual != null) visual.Present(speed, grounded, action, sequence, remaining);
        }

        public void ResetPresentation()
        {
            ClearAction();NormalizedSpeed = 0;PresentLocal();
        }

        public void PresentKnockdown(bool knockedDown, Vector3 direction)
            => visual?.PresentKnockdown(knockedDown, direction);
    }
}
