using UnityEngine;
using UnityEngine.InputSystem;

namespace DogSimulator.Player
{
    public sealed class DogInputReader : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private DogMovementConfig config;
        private InputActionAsset runtimeActions;
        private InputActionMap playerActions;
        private InputAction move;
        private InputAction look;
        private InputAction sprint;
        private InputAction jump;
        private InputAction bark;
        private InputAction bite;
        private InputAction poop;
        private InputAction pee;
        private InputAction primaryCarry;
        private InputAction dropCarry;
        private InputAction resetYard;
        private InputAction respawnBone;
        private InputAction resetMission;
        private DogAction pendingAction;
        private float actionExpires = float.NegativeInfinity;
        private float jumpExpires = float.NegativeInfinity;
        private float primaryCarryExpires = float.NegativeInfinity;
        private float dropCarryExpires = float.NegativeInfinity;
        private float resetYardExpires = float.NegativeInfinity;
        private float respawnBoneExpires = float.NegativeInfinity;
        private float resetMissionExpires = float.NegativeInfinity;

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
            playerActions = runtimeActions.FindActionMap("Player", true);
            move = playerActions.FindAction("Move", true);
            look = playerActions.FindAction("Look", true);
            sprint = playerActions.FindAction("Sprint", true);
            jump = playerActions.FindAction("Jump", true);
            bark = FindOrAddButton("Bark", "<Keyboard>/f");
            bite = FindOrAddButton("Bite", "<Mouse>/rightButton");
            poop = FindOrAddButton("Poop", "<Keyboard>/g");
            pee = FindOrAddButton("Pee", "<Keyboard>/h");
            // Use dedicated runtime actions so legacy Interact/Throw bindings in the
            // input asset cannot make one press dispatch two carry commands.
            primaryCarry = FindOrAddButton("PrimaryCarry", "<Mouse>/leftButton");
            dropCarry = FindOrAddButton("DropCarry", "<Keyboard>/e");
            resetYard = FindOrAddButton("ResetYard", "<Keyboard>/r");
            respawnBone = FindOrAddButton("RespawnBone", "<Keyboard>/f6");
            resetMission = FindOrAddButton("ResetMission", "<Keyboard>/f7");
        }

        private InputAction FindOrAddButton(string name, string binding)
        {
            var existing = playerActions.FindAction(name);
            if (existing != null) return existing;
            var action = playerActions.AddAction(name, InputActionType.Button);
            action.AddBinding(binding, groups: "Keyboard&Mouse");
            return action;
        }

        public string GetBindingLabel(string actionName)
            => playerActions?.FindAction(actionName)?.GetBindingDisplayString(group: "Keyboard&Mouse") ?? "";

        public void SetGameplayEnabled(bool value)
        {
            GameplayEnabled = value && enabled && runtimeActions != null;
            Move = Look = Vector2.zero;
            Sprint = false;
            jumpExpires = float.NegativeInfinity;
            pendingAction = DogAction.None;
            actionExpires = float.NegativeInfinity;
            primaryCarryExpires = float.NegativeInfinity;
            dropCarryExpires = float.NegativeInfinity;
            resetYardExpires = float.NegativeInfinity;
            respawnBoneExpires = float.NegativeInfinity;
            resetMissionExpires = float.NegativeInfinity;
            if (runtimeActions == null) return;
            if (GameplayEnabled) playerActions.Enable();
            else runtimeActions.Disable();
        }

        private void Update()
        {
            if (!GameplayEnabled) return;
            Move = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1);
            Look = look.ReadValue<Vector2>();
            Sprint = sprint.IsPressed();
            var now = Time.unscaledTime;
            BufferPress(jump, now + config.JumpBufferSeconds, ref jumpExpires);
            var commandExpires = now + config.ActionBufferSeconds;
            // Later actions take priority when several are pressed in the same frame.
            if (bark.WasPressedThisFrame()) QueueAction(DogAction.Bark, commandExpires);
            if (poop.WasPressedThisFrame()) QueueAction(DogAction.Poop, commandExpires);
            if (pee.WasPressedThisFrame()) QueueAction(DogAction.Pee, commandExpires);
            if (bite.WasPressedThisFrame()) QueueAction(DogAction.Bite, commandExpires);
            BufferPress(primaryCarry, commandExpires, ref primaryCarryExpires);
            BufferPress(dropCarry, commandExpires, ref dropCarryExpires);
            BufferPress(resetYard, commandExpires, ref resetYardExpires);
            BufferPress(respawnBone, commandExpires, ref respawnBoneExpires);
            BufferPress(resetMission, commandExpires, ref resetMissionExpires);
        }

        private static void BufferPress(InputAction button, float deadline, ref float expires)
        {
            if (button.WasPressedThisFrame()) expires = deadline;
        }

        private void QueueAction(DogAction action, float deadline)
        {
            pendingAction = action;
            actionExpires = deadline;
        }

        public DogAction ConsumeAction()
        {
            var action = ConsumeBuffered(ref actionExpires) ? pendingAction : DogAction.None;
            pendingAction = DogAction.None;
            return action;
        }

        // The network driver will call this once per FixedUpdateNetwork; preview uses FixedUpdate.
        public bool ConsumeJump() => ConsumeBuffered(ref jumpExpires);

        public bool ConsumePrimaryCarry() => ConsumeBuffered(ref primaryCarryExpires);
        public bool ConsumeDropCarry() => ConsumeBuffered(ref dropCarryExpires);
        public bool ConsumeResetYard() => ConsumeBuffered(ref resetYardExpires);
        public bool ConsumeRespawnBone() => ConsumeBuffered(ref respawnBoneExpires);
        public bool ConsumeResetMission() => ConsumeBuffered(ref resetMissionExpires);

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
