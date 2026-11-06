using DogSimulator.Networking;
using DogSimulator.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DogSimulator.UI
{
    public sealed class LocalPreviewUI : MonoBehaviour
    {
        [SerializeField] private GameObject menu;
        [SerializeField] private GameObject hud;
        [SerializeField] private TMP_InputField nickname;
        [SerializeField] private TMP_InputField roomCode;
        [SerializeField] private TMP_Text message;
        [SerializeField] private TMP_Text playerLabel;
        [SerializeField] private UnityEngine.UI.Button playButton;
        [SerializeField] private UnityEngine.UI.Button checkCodeButton;
        [SerializeField] private UnityEngine.UI.Button backButton;
        [SerializeField] private DogInputReader input;
        [SerializeField] private DogMotor motor;
        private bool playing;

        private void Awake()
        {
            nickname.richText = roomCode.richText = false;
            foreach (var text in GetComponentsInChildren<TMP_Text>(true))
                if (text.name == "Controls") text.text = DogAnimationDriver.Controls;
            nickname.text = PlayerPrefs.GetString("DogSimulator.Nickname", "Guest");
            message.text = "Rigged dog ready. Local movement and action animations; no network connection.";
            playButton.onClick.AddListener(Play);
            checkCodeButton.onClick.AddListener(CheckCode);
            backButton.onClick.AddListener(ReturnToMenu);
            SetPlaying(false);
        }

        private void Play()
        {
            if (!GuestProfile.TryNormalizeName(nickname.text, out var name, out var error))
            {
                message.text = error;
                return;
            }
            PlayerPrefs.SetString("DogSimulator.Nickname", name);
            PlayerPrefs.Save();
            playerLabel.text = $"{name}  |  Model 1: rigged dog  |  OFFLINE";
            SetPlaying(true);
        }

        private void CheckCode()
        {
            if (!RoomCodeService.TryNormalize(roomCode.text, out var normalized))
            {
                message.text = "Use 6–8 letters/digits; omit I, O, 0 and 1.";
                return;
            }
            roomCode.text = normalized;
            message.text = "Code format is valid. Joining needs Photon Fusion SDK + App ID.";
        }

        private void Update()
        {
            if (playing && (Keyboard.current?.escapeKey.wasPressedThisFrame == true
                || Cursor.lockState != CursorLockMode.Locked)) SetPlaying(false);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && playing) SetPlaying(false);
        }

        private void ReturnToMenu()
        {
            SetPlaying(false);
            motor.Respawn();
            message.text = "Local preview reset. Choose Play local preview to continue.";
        }

        private void SetPlaying(bool value)
        {
            playing = value;
            input.SetGameplayEnabled(value);
            menu.SetActive(!value);
            hud.SetActive(value);
            Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !value;
        }

        private void OnDestroy()
        {
            if (playButton != null) playButton.onClick.RemoveListener(Play);
            if (checkCodeButton != null) checkCodeButton.onClick.RemoveListener(CheckCode);
            if (backButton != null) backButton.onClick.RemoveListener(ReturnToMenu);
            if (input != null) input.SetGameplayEnabled(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
