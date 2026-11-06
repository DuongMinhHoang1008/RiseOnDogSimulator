using UnityEngine;
using UnityEngine.InputSystem;

namespace DogSimulator.Player
{
    public sealed class DogInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private DogMovementConfig config;
        private InputActionAsset runtimeActions;
        private InputAction move;
        private InputAction look;
        private InputAction sprint;
        private InputAction jump;
        private InputAction bark, bite, poop, pee, primaryCarry, dropCarry, resetYard;
        private DogAction pendingAction;
        private float actionExpires = float.NegativeInfinity;
        private float jumpExpires = float.NegativeInfinity;
        private float primaryCarryExpires = float.NegativeInfinity;
        private float dropCarryExpires = float.NegativeInfinity;
        private float resetExpires = float.NegativeInfinity;

        public bool GameplayEnabled { get; private set; }
        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool Sprint { get; private set; }

        private void Awake()
        {
            if (actions == null || config == null)
            {
                Debug.LogError("DogInputReader requires the existing input asset and movement config.", this);
                enabled = false;
                return;
            }
            runtimeActions = Instantiate(actions);
            runtimeActions.bindingMask = InputBinding.MaskByGroup("Keyboard&Mouse");
            move = runtimeActions.FindAction("Player/Move", true);
            look = runtimeActions.FindAction("Player/Look", true);
            sprint = runtimeActions.FindAction("Player/Sprint", true);
            jump = runtimeActions.FindAction("Player/Jump", true);
            var map = runtimeActions.FindActionMap("Player", true);
            bark = Action(map, "Bark", "<Keyboard>/f");
            bite = Action(map, "Bite", "<Mouse>/rightButton");
            poop = Action(map, "Poop", "<Keyboard>/g");
            pee = Action(map, "Pee", "<Keyboard>/h");
            // Use dedicated runtime actions so legacy Interact/Throw bindings in the
            // input asset cannot make one press dispatch two carry commands.
            primaryCarry = Action(map, "PrimaryCarry", "<Mouse>/leftButton");
            dropCarry = Action(map, "DropCarry", "<Keyboard>/e");
            resetYard = Action(map, "ResetYard", "<Keyboard>/r");
        }

        private static InputAction Action(InputActionMap map, string name, string binding)
        {
            var existing = map.FindAction(name);
            if (existing != null) return existing;
            var action = map.AddAction(name, InputActionType.Button);
            action.AddBinding(binding, groups: "Keyboard&Mouse");
            return action;
        }

        public void SetGameplayEnabled(bool value)
        {
            GameplayEnabled = value && enabled && runtimeActions != null;
            Move = Look = Vector2.zero;
            Sprint = false;
            jumpExpires = float.NegativeInfinity;
            pendingAction = DogAction.None;
            actionExpires = float.NegativeInfinity;
            primaryCarryExpires = dropCarryExpires = resetExpires = float.NegativeInfinity;
            if (runtimeActions == null) return;
            if (GameplayEnabled) runtimeActions.FindActionMap("Player").Enable();
            else runtimeActions.Disable();
        }

        private void Update()
        {
            if (!GameplayEnabled) return;
            Move = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1);
            Look = look.ReadValue<Vector2>();
            Sprint = sprint.IsPressed();
            if (jump.WasPressedThisFrame())
                jumpExpires = Time.unscaledTime + config.JumpBufferSeconds;
            if (bark.WasPressedThisFrame()) QueueAction(DogAction.Bark);
            if (poop.WasPressedThisFrame()) QueueAction(DogAction.Poop);
            if (pee.WasPressedThisFrame()) QueueAction(DogAction.Pee);
            if (bite.WasPressedThisFrame()) QueueAction(DogAction.Bite);
            if (primaryCarry.WasPressedThisFrame())
                primaryCarryExpires = Time.unscaledTime + config.ActionBufferSeconds;
            if (dropCarry.WasPressedThisFrame())
                dropCarryExpires = Time.unscaledTime + config.ActionBufferSeconds;
            if (resetYard.WasPressedThisFrame()) resetExpires = Time.unscaledTime + config.ActionBufferSeconds;
        }

        private void QueueAction(DogAction action)
        {
            pendingAction = action;
            actionExpires = Time.unscaledTime + config.ActionBufferSeconds;
        }

        public DogAction ConsumeAction()
        {
            var action = GameplayEnabled && Time.unscaledTime <= actionExpires ? pendingAction : DogAction.None;
            pendingAction = DogAction.None;actionExpires = float.NegativeInfinity;
            return action;
        }

        // The network driver will call this once per FixedUpdateNetwork; preview uses FixedUpdate.
        public bool ConsumeJump()
        {
            var buffered = GameplayEnabled && Time.unscaledTime <= jumpExpires;
            jumpExpires = float.NegativeInfinity;
            return buffered;
        }

        public bool ConsumePrimaryCarry() => ConsumeBuffered(ref primaryCarryExpires);
        public bool ConsumeDropCarry() => ConsumeBuffered(ref dropCarryExpires);
        public bool ConsumeResetYard() => ConsumeBuffered(ref resetExpires);

        private bool ConsumeBuffered(ref float expires)
        {
            var buffered = GameplayEnabled && Time.unscaledTime <= expires;
            expires = float.NegativeInfinity;
            return buffered;
        }

        private void OnDisable() => SetGameplayEnabled(false);
        private void OnDestroy()
        {
            if (runtimeActions != null) Destroy(runtimeActions);
        }
    }
}
