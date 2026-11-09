using TMPro;
using UnityEngine;
using static DogSimulator.Networking.DogMenuSkin;

namespace DogSimulator.Networking
{
    internal sealed partial class DogFrontEnd
    {
        private static readonly Vector2 TopLeft = new(0, 1);
        private static readonly Vector2 TopCenter = new(.5f, 1);
        private static readonly Vector2 BottomCenter = new(.5f, 0);
        private static readonly Vector2 BottomRight = new(1, 0);
        private RectTransform gameplayHud;
        private RectTransform pickupHint;
        private GameObject carryHints;
        private TMP_Text vehicleHint;
        private GameObject vehicleHints;
        private TMP_Text roomStatus;
        private TMP_Text poopHint;
        private TMP_Text peeHint;
        private int lastPoopSeconds = -1;
        private int lastPeeSeconds = -1;
        private Camera gameplayCamera;
        private float nextRoomStatusUpdate;

        private void BuildGameplayHud(RectTransform root)
        {
            gameplayHud = root;
            root.SetSiblingIndex(stage.GetSiblingIndex()); // Settings must draw over the HUD.
            BuildKeyHint(HudAnchor(root, "Settings Shortcut", TopLeft, TopLeft, new Vector2(30, -28), 192, 46), "Esc", "Settings");

            var room = HudAnchor(root, "Room Status", TopCenter, TopCenter, new Vector2(0, -28), 390, 40);
            skin.Surface(room, skin.HudPill, Color.white);
            roomStatus = skin.Label(At(room, "Room Info", 0, 0, 1600, 900), "", 22, Cream);

            var actionList = HudAnchor(root, "Dog Actions", BottomRight, BottomRight, new Vector2(-30, 80), 174, 180);
            AddActionRow("F", "Bark", 0);
            AddActionRow("RMB", "Bite", 47);
            poopHint = AddActionRow("G", "Poop", 94);
            peeHint = AddActionRow("H", "Pee", 141);

            pickupHint = HudAnchor(root, "Pick Up Hint", Vector2.one * .5f, TopCenter, Vector2.zero, 178, 44);
            BuildKeyHint(pickupHint, "LMB", "Pick Up");
            pickupHint.gameObject.SetActive(false);

            var held = HudAnchor(root, "Held Item Hints", BottomCenter, BottomCenter, new Vector2(0, 84), 324, 44);
            BuildKeyHint(HudAnchor(held, "Drop Hint", TopLeft, TopLeft, Vector2.zero, 132, 44), "E", "Drop");
            BuildKeyHint(HudAnchor(held, "Throw Hint", TopLeft, TopLeft, new Vector2(144, 0), 180, 44), "LMB", "Throw");
            carryHints = held.gameObject;
            carryHints.SetActive(false);
            var vehicle = HudAnchor(root, "Vehicle Hint", BottomCenter, BottomCenter, new Vector2(0, 84), 252, 44);
            vehicleHint = BuildKeyHint(vehicle, "E", "Enter vehicle");
            vehicleHints = vehicle.gameObject;
            vehicleHints.SetActive(false);

            TMP_Text AddActionRow(string key, string action, float y)
                => BuildKeyHint(HudAnchor(actionList, action + " Hint", TopLeft, TopLeft, new Vector2(0, -y), 174, 39), key, action);
        }

        private TMP_Text BuildKeyHint(RectTransform rect, string key, string action)
        {
            skin.Surface(rect, skin.HudPill, Color.white);
            var keyWidth = key.Length == 1 ? 380 : 535;
            var cap = At(rect, "Keycap", 66, 112, keyWidth, 676);
            skin.Surface(cap, skin.Paper, Color.white).pixelsPerUnitMultiplier = 1.5f;
            var keyLabel = skin.Label(At(cap, "Key", 0, 0, 1600, 900), key, 22, Brown);
            keyLabel.margin = Vector4.zero;
            keyLabel.fontStyle = FontStyles.Bold;
            var actionLabel = skin.Label(At(rect, "Action", keyWidth + 145, 0, 1385 - keyWidth, 900), action, 23, Cream, TextAlignmentOptions.Left);
            actionLabel.margin = Vector4.zero;
            return actionLabel;
        }

        private static RectTransform HudAnchor(Transform parent, string name, Vector2 anchor, Vector2 pivot,
            Vector2 position, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = position;
            return rect;
        }

        private void LateUpdate()
        {
            if (page != Page.Gameplay) return;
            if (session.HasAvatar)
            {
                UpdateCooldownHint(poopHint, "Poop", session.LocalPlayer.PoopCooldown, ref lastPoopSeconds);
                UpdateCooldownHint(peeHint, "Pee", session.LocalPlayer.PeeCooldown, ref lastPeeSeconds);
            }
            var carrying = false;
            var target = playing && session.HasAvatar ? session.LocalPlayer.GetCarryTarget(out carrying) : null;
            carryHints.SetActive(carrying);
            pickupHint.gameObject.SetActive(!carrying && target != null && PositionPickupHint(target));
            var vehiclePrompt = playing && session.HasAvatar && !carrying ? session.LocalPlayer.VehiclePrompt() : null;
            vehicleHints.SetActive(vehiclePrompt != null);
            if (vehiclePrompt != null && vehicleHint.text != vehiclePrompt) vehicleHint.text = vehiclePrompt;

            if (session.IsSinglePlayer || !session.HasAvatar || Time.unscaledTime < nextRoomStatusUpdate) return;
            nextRoomStatusUpdate = Time.unscaledTime + .25f;
            roomStatus.text = $"{session.RoomCode}   •   {session.PlayerCount}/{session.Capacity} Dogs   •   {session.PingMilliseconds} ms";
        }

        private static void UpdateCooldownHint(TMP_Text label, string action, float remaining, ref int lastSeconds)
        {
            var seconds = Mathf.CeilToInt(remaining);
            if (seconds == lastSeconds) return;
            lastSeconds = seconds;
            label.text = seconds > 0 ? $"{action}  {seconds}s" : action;
        }

        private bool PositionPickupHint(NetworkCarryable target)
        {
            if (gameplayCamera == null) gameplayCamera = Camera.main;
            if (gameplayCamera == null) return false;

            var point = target.transform.position;
            if (target.TryGetComponent<Collider>(out var collider))
            {
                var bounds = collider.bounds;
                var up = gameplayCamera.transform.up;
                var radius = Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z)));
                point = bounds.center - up * radius;
            }
            var screen = gameplayCamera.WorldToScreenPoint(point);
            if (screen.z <= 0 || !gameplayCamera.pixelRect.Contains(screen)) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(gameplayHud, screen, null, out var local)) return false;

            const float padding = 12;
            var limits = gameplayHud.rect;
            var halfWidth = pickupHint.rect.width * .5f;
            local.x = Mathf.Clamp(local.x, limits.xMin + halfWidth + padding, limits.xMax - halfWidth - padding);
            local.y = Mathf.Max(local.y - padding, limits.yMin + pickupHint.rect.height + padding);
            pickupHint.anchoredPosition = local;
            return true;
        }
    }
}
