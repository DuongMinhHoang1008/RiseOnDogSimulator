using DogSimulator.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using static DogSimulator.Networking.DogMenuSkin;

namespace DogSimulator.Networking
{
    internal sealed partial class DogFrontEnd
    {
        private static readonly Vector2 TopLeft = new(0, 1);
        private static readonly Vector2 TopCenter = new(.5f, 1);
        private static readonly Vector2 BottomCenter = new(.5f, 0);
        private static readonly Vector2 BottomRight = new(1, 0);
        private static KeyControl SettingsKey => Keyboard.current?.escapeKey;
        private NetworkDogPlayer hudPlayer;
        [SerializeField] private TMP_Text interactionHint;
        [SerializeField] private TMP_Text actionHints;
        [SerializeField] private TMP_Text cooldownHints;
        private string primaryKey;
        private string dropKey;
        private string jumpKey;
        private string poopKey;
        private string peeKey;
        private string lastInteraction;
        private string lastInteractionKey;
        private string lastSecondaryInteraction;
        [SerializeField] private TMP_Text missionHint;
        private (string Label, int Distance, int CompassIndex) lastMissionHint;
        [SerializeField] private TMP_Text missionControls;
        private static readonly string[] Compass = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        [SerializeField] private TMP_Text roomStatus;
        [SerializeField] private TMP_Text wantedHint;
        [SerializeField] private TMP_Text jailHint;
        private byte shownWantedLevel;
        private int shownWantedHeat = -1;
        private float wantedPulseUntil;
        private static readonly string[] WantedStars = { "· · ·", "★ · ·", "★ ★ ·", "★ ★ ★" };
        private static readonly string[] WantedStarsFallback = { "· · ·", "* · ·", "* * ·", "* * *" };
        private int lastPoopSeconds = -1;
        private int lastPeeSeconds = -1;
        private float nextRoomStatusUpdate;

        [SerializeField] private TMP_Text settingsHint;

#if UNITY_EDITOR
        private void BuildGameplayHud(RectTransform root)
        {
            root.SetSiblingIndex(stage.GetSiblingIndex()); // Settings must draw over the HUD.
            var topRight = Vector2.one;
            settingsHint = HudChip(root, "Settings Shortcut", TopLeft, new Vector2(HudMargin, -HudMargin), 148);
            roomStatus = HudChip(root, "Room Status", TopCenter, new Vector2(0, -HudMargin), 340);
            wantedHint = HudChip(root, "Annoying Gauge", topRight, new Vector2(-HudMargin, -HudMargin), 238);
            jailHint = HudChip(root, "Jail Instructions", TopCenter, new Vector2(0, -HudMargin - 3 * (HudRow + HudGap)), 680);
            missionHint = HudChip(root, "Golden Bone Objective", TopCenter, new Vector2(0, -HudMargin - HudRow - HudGap), 490);
            missionControls = HudChip(root, "Golden Bone Controls", TopCenter, new Vector2(0, -HudMargin - 2 * (HudRow + HudGap)), 630);
            interactionHint = HudChip(root, "Interaction", BottomCenter, new Vector2(0, HudMargin), 300);
            actionHints = HudChip(root, "Dog Actions", BottomRight, new Vector2(-HudMargin, HudMargin + HudRow + HudGap), 224);
            cooldownHints = HudChip(root, "Dog Cooldowns", BottomRight, new Vector2(-HudMargin, HudMargin), 224);

            foreach (var hint in new[] { roomStatus, wantedHint, jailHint, missionHint, missionControls, interactionHint, actionHints, cooldownHints })
                hint.transform.parent.gameObject.SetActive(false);
        }

        private TMP_Text HudChip(Transform parent, string name, Vector2 anchor, Vector2 offset, float width)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.sizeDelta = new Vector2(width, HudRow);
            rect.anchoredPosition = offset;
            skin.Surface(rect, skin.HudPill, Color.white).pixelsPerUnitMultiplier = 2;
            return skin.Label(At(rect, "Label", 0, 0, 1600, 900), "", HudType, HudText);
        }

#endif

        private void LateUpdate()
        {
            if (activePage == null || page != Page.Gameplay) return;

            var dog = session.HasAvatar ? session.LocalPlayer : null;
            if (dog != hudPlayer)
            {
                hudPlayer = dog;
                RefreshHudBindings(dog != null ? dog.InputReader : null);
            }

            var sandbox = session.Sandbox;
            var ready = playing && dog != null;
            UpdateWantedHud(ready ? dog : null, sandbox);
            SetChipVisible(actionHints, ready);
            SetChipVisible(cooldownHints, ready);
            if (ready) UpdateCooldownHints(dog);
            var missionActive = ready && sandbox != null && sandbox.Object != null && sandbox.Object.IsValid
                && sandbox.GoldenBoneId.IsValid;
            SetChipVisible(missionHint, missionActive);
            SetChipVisible(missionControls, missionActive
                && (session.IsSinglePlayer || sandbox.RoomOwner == dog.Object.InputAuthority));
            if (missionActive) UpdateMissionHint(sandbox, dog);
            UpdateInteraction(ready ? dog : null, sandbox, missionActive);

            var showRoom = ready && !session.IsSinglePlayer;
            SetChipVisible(roomStatus, showRoom);
            if (!showRoom || Time.unscaledTime < nextRoomStatusUpdate) return;
            nextRoomStatusUpdate = Time.unscaledTime + .25f;
            roomStatus.text = $"{session.RoomCode}   ·   {session.PlayerCount}/{session.Capacity} Dogs   ·   {session.PingMilliseconds} ms";
        }

        private void RefreshHudBindings(DogInputReader reader)
        {
            string Binding(string action) => reader != null ? reader.GetBindingLabel(action) : "";
            primaryKey = Binding("PrimaryCarry");
            dropKey = Binding("DropCarry");
            jumpKey = Binding("Jump");
            poopKey = Binding("Poop");
            peeKey = Binding("Pee");
            actionHints.text = WithKey(Binding("Bark"), "Bark") + "     " + WithKey(Binding("Bite"), "Bite");
            missionControls.text = WithKey(Binding("RespawnBone"), "Respawn Bone") + "     "
                + WithKey(Binding("ResetMission"), "Reset Mission") + "     " + WithKey(Binding("ResetYard"), "Reset Sandbox");
            lastPoopSeconds = lastPeeSeconds = -1;
            lastInteraction = null;
            lastInteractionKey = null;
        }

        private void UpdateInteraction(NetworkDogPlayer dog, SandboxInteractionRegistry sandbox, bool missionActive)
        {
            if (dog == null)
            {
                SetChipVisible(interactionHint, false);
                return;
            }

            var target = dog.GetCarryTarget(out var carrying);
            if (dog.CarrierId.IsValid) ShowInteraction("Wriggle free", jumpKey);
            else if (carrying)
            {
                if (target is NetworkDogPlayer) ShowInteraction("Drop", dropKey);
                else ShowInteraction("Drop", dropKey, "Throw", primaryKey);
            }
            else if (target != null)
            {
                var action = target switch
                {
                    NetworkDogPlayer => "Carry dog",
                    NetworkCityPedestrian => "Carry NPC",
                    NetworkCarryable prop when prop.IsJailBowl => "Pick up bowl",
                    _ when missionActive && target.Object.Id == sandbox.GoldenBoneId => "Take Golden Bone",
                    _ => "Pick up"
                };
                ShowInteraction(action, primaryKey);
            }
            else if (!dog.InJail && (sandbox == null || !sandbox.CanUseJailLever(dog)) && dog.VehiclePrompt() is { } prompt)
                ShowInteraction(prompt, prompt is "Enter vehicle" or "Exit vehicle" ? dropKey : null);
            else SetChipVisible(interactionHint, false);
        }

        private void ShowInteraction(string action, string key, string secondary = null, string secondaryKey = null)
        {
            SetChipVisible(interactionHint, true);
            if (lastInteraction == action && lastInteractionKey == key && lastSecondaryInteraction == secondary) return;
            lastInteraction = action;
            lastInteractionKey = key;
            lastSecondaryInteraction = secondary;
            interactionHint.text = WithKey(key, action) + (secondary == null ? "" : "     " + WithKey(secondaryKey, secondary));
        }

        private static string WithKey(string key, string action)
            => string.IsNullOrEmpty(key) ? action : $"{key} · {action}";

        private static void SetChipVisible(TMP_Text label, bool visible)
            => label.transform.parent.gameObject.SetActive(visible);

        private void UpdateWantedHud(NetworkDogPlayer dog, SandboxInteractionRegistry sandbox)
        {
            var visible = dog != null && dog.WantedHeat > 0;
            wantedHint.transform.parent.gameObject.SetActive(visible);
            if (visible)
            {
                var level = dog.WantedLevel;
                if (level > shownWantedLevel) wantedPulseUntil = Time.unscaledTime + .65f;
                var heat = Mathf.CeilToInt(dog.WantedHeat);
                if (shownWantedLevel != level || shownWantedHeat != heat)
                {
                    var stars = wantedHint.font.HasCharacter('★', true) ? WantedStars : WantedStarsFallback;
                    wantedHint.text = $"ANNOYING   {stars[level]}   {heat}%";
                    shownWantedLevel = level;
                    shownWantedHeat = heat;
                }
                wantedHint.color = Time.unscaledTime < wantedPulseUntil ? HudAlertText : HudText;
            }
            else
            {
                shownWantedLevel = 0;
                shownWantedHeat = -1;
            }

            var ready = dog != null && sandbox != null && sandbox.Object != null && sandbox.Object.IsValid;
            var text = dog != null && dog.InJail
                ? (ready && sandbox.JailGate != 0
                    ? (sandbox.RescueOpenedGate ? "A teammate opened the gate! Run out." : "Gate open! Run out.")
                    : "CAUGHT  ·  Drop the bowl on the yellow plate.  " + WithKey(dropKey, "Reset bowl with empty mouth"))
                : ready && sandbox.CanUseJailLever(dog) ? WithKey(dropKey, "Open jail gate for teammates")
                : dog != null && dog.ArrestingPolice.IsValid ? "Police are catching you!" : "";
            jailHint.transform.parent.gameObject.SetActive(text.Length != 0);
            if (jailHint.text != text) jailHint.text = text;
        }

        private void UpdateMissionHint(SandboxInteractionRegistry sandbox, NetworkDogPlayer dog)
        {
            if (sandbox.GoldenBoneComplete)
            {
                const string delivered = "Golden Bone delivered!";
                if (missionHint.text != delivered) missionHint.text = delivered;
                lastMissionHint = default;
                return;
            }
            var label = "Golden Bone vault";
            var destination = GoldenBoneCourse.Pedestal;
            if (dog.Runner.TryFindObject(sandbox.GoldenBoneId, out var obj)
                && obj.TryGetComponent<NetworkCarryable>(out var bone))
            {
                destination = bone.transform.position;
                if (bone.CarrierId == dog.Object.Id)
                {
                    label = "Guards alerted  •  Blue kennel";
                    destination = GoldenBoneCourse.Delivery.center;
                }
                else if (bone.IsPedestrianCarried) label = "Guards returning the bone";
                else if (sandbox.GoldenBoneAlert) label = "Recover the Golden Bone";
            }
            var offset = Vector3.ProjectOnPlane(destination - dog.transform.position, Vector3.up);
            var sector = Mathf.RoundToInt(Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg / 45f);
            var hint = (Label: label, Distance: Mathf.RoundToInt(offset.magnitude), CompassIndex: (sector + 8) % 8);
            if (lastMissionHint == hint) return;
            lastMissionHint = hint;
            missionHint.text = $"{hint.Label}  •  {hint.Distance} m {Compass[hint.CompassIndex]}";
        }

        private void UpdateCooldownHints(NetworkDogPlayer dog)
        {
            var poopSeconds = Mathf.CeilToInt(dog.PoopCooldown);
            var peeSeconds = Mathf.CeilToInt(dog.PeeCooldown);
            if (poopSeconds == lastPoopSeconds && peeSeconds == lastPeeSeconds) return;
            lastPoopSeconds = poopSeconds;
            lastPeeSeconds = peeSeconds;
            cooldownHints.text = WithKey(poopKey, poopSeconds > 0 ? $"Poop {poopSeconds}s" : "Poop") + "     "
                + WithKey(peeKey, peeSeconds > 0 ? $"Pee {peeSeconds}s" : "Pee");
        }
    }
}
