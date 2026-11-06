using DogSimulator.Player;
using DogSimulator.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DogSimulator.Networking
{
    public sealed class FusionPreviewUI : MonoBehaviour
    {
        [SerializeField] private FusionSessionService session;
        [SerializeField] private GameObject menu;
        [SerializeField] private GameObject hud;
        [SerializeField] private TMP_InputField nickname;
        [SerializeField] private TMP_InputField roomCode;
        [SerializeField] private TMP_Dropdown modelDropdown;
        [SerializeField] private DogSelectionPreview selectionPreview;
        [SerializeField] private TMP_Text message;
        [SerializeField] private TMP_Text playerLabel;
        [SerializeField] private UnityEngine.UI.Button createButton;
        [SerializeField] private UnityEngine.UI.Button joinButton;
        [SerializeField] private UnityEngine.UI.Button resumeButton;
        [SerializeField] private UnityEngine.UI.Button leaveButton;
        [SerializeField] private UnityEngine.UI.Button copyButton;
        private bool playing;
        private float nextHudUpdate;
        private bool suppressModelChange;
        private const string ModelPreferenceKey = "DogSimulator.DogModelId";

        private void Awake()
        {
            nickname.richText = roomCode.richText = false;
            foreach (var text in GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "Controls") text.text = DogAnimationDriver.Controls;
                if (text.name == "NetworkSettings") text.text = $"Region: {session.Region} | Capacity: {session.Capacity} | {session.GameVersion}";
            }
            nickname.text = PlayerPrefs.GetString("DogSimulator.Nickname", "Guest");
            PopulateModels();
            selectionPreview?.ShowModel(SelectedModelId());
            createButton.onClick.AddListener(Create);
            joinButton.onClick.AddListener(Join);
            resumeButton.onClick.AddListener(Resume);
            leaveButton.onClick.AddListener(Leave);
            copyButton.onClick.AddListener(CopyCode);
            modelDropdown.onValueChanged.AddListener(OnModelChanged);
            session.Changed += OnSessionChanged;
            Pause();
            OnSessionChanged();
        }

        private async void Create() { RememberName(); await session.CreateAsync(nickname.text, SelectedModelId()); }
        private async void Join() { RememberName(); await session.JoinAsync(nickname.text, SelectedModelId(), roomCode.text); }
        private async void Leave() { Pause(); await session.LeaveAsync(); }

        private void RememberName()
        {
            if (!GuestProfile.TryNormalizeName(nickname.text, out var name, out _)) return;
            PlayerPrefs.SetString("DogSimulator.Nickname", name);
            PlayerPrefs.Save();
        }

        private void PopulateModels()
        {
            modelDropdown.ClearOptions();
            var names = new System.Collections.Generic.List<string>();
            var catalog = session.ModelCatalog;
            if (catalog == null) return;
            foreach (var entry in catalog.Entries) names.Add(entry.DisplayName);
            modelDropdown.AddOptions(names);
            var saved = PlayerPrefs.GetInt(ModelPreferenceKey, catalog.FallbackModelId);
            var index = 0;
            for (var i = 0; i < catalog.Entries.Count; i++)
                if (catalog.Entries[i].ModelId == saved) { index = i; break; }
            suppressModelChange = true;
            modelDropdown.SetValueWithoutNotify(index);
            suppressModelChange = false;
        }

        private int SelectedModelId()
        {
            var catalog = session.ModelCatalog;
            return catalog != null && modelDropdown.value >= 0 && modelDropdown.value < catalog.Entries.Count
                ? catalog.Entries[modelDropdown.value].ModelId : catalog?.FallbackModelId ?? 1;
        }

        private async void OnModelChanged(int _)
        {
            if (suppressModelChange) return;
            var modelId = SelectedModelId();
            PlayerPrefs.SetInt(ModelPreferenceKey, modelId);
            PlayerPrefs.Save();
            selectionPreview?.ShowModel(modelId);
            if (!session.IsConnected) return;
            var result = await session.SelectModelAsync(modelId);
            if (result == ModelSelectionResult.Approved) return;
            var catalog = session.ModelCatalog;
            if (catalog == null) return;
            for (var i = 0; i < catalog.Entries.Count; i++)
            {
                if (catalog.Entries[i].ModelId != session.SelectedModelId) continue;
                modelDropdown.SetValueWithoutNotify(i);
                selectionPreview?.ShowModel(session.SelectedModelId);
                break;
            }
        }

        private void OnSessionChanged()
        {
            message.text = session.Message;
            var idle = session.State == SessionState.Disconnected || session.State == SessionState.Failed;
            createButton.interactable = joinButton.interactable = nickname.interactable = roomCode.interactable = idle;
            resumeButton.interactable = session.HasAvatar;
            copyButton.interactable = session.IsConnected;
            leaveButton.interactable = session.IsConnected || session.State == SessionState.Connecting;
            modelDropdown.interactable = idle || session.IsConnected;
            if (session.IsConnected) roomCode.text = session.RoomCode;
            selectionPreview?.SetVisible(true);
            if (session.HasAvatar) Resume();
            else Pause();
        }

        private void Resume()
        {
            if (!session.HasAvatar) return;
            playing = true;
            session.SetControlsEnabled(true);
            menu.SetActive(false);
            hud.SetActive(true);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Pause()
        {
            playing = false;
            session.SetControlsEnabled(false);
            menu.SetActive(true);
            selectionPreview?.SetVisible(true);
            hud.SetActive(false);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void CopyCode()
        {
            if (!session.IsConnected) return;
            var copied = WebClipboard.Copy(session.RoomCode);
            message.text = copied
                ? $"Copied room code: {session.RoomCode} | Region: {session.Region} | {session.GameVersion}"
                : $"Could not copy automatically. Room code: {session.RoomCode}";
        }

        private void Update()
        {
            if (playing && (Keyboard.current?.escapeKey.wasPressedThisFrame == true
                || Cursor.lockState != CursorLockMode.Locked)) Pause();
            if (!session.HasAvatar || Time.unscaledTime < nextHudUpdate) return;
            nextHudUpdate = Time.unscaledTime + 0.25f;
            playerLabel.text = $"Room {session.RoomCode} | {session.Region} | {session.PlayerCount}/{session.Capacity}"
                + $" | Ping {session.PingMilliseconds} ms | Net {session.SimulationTickRate} Hz\n"
                + "DOG STACK SYNC / RELATIVE SUPPORT ANCHOR";
        }

        private void OnApplicationFocus(bool focused) { if (!focused && playing) Pause(); }

        private void OnDestroy()
        {
            if (session != null) { session.Changed -= OnSessionChanged; session.SetControlsEnabled(false); }
            createButton.onClick.RemoveListener(Create);
            joinButton.onClick.RemoveListener(Join);
            resumeButton.onClick.RemoveListener(Resume);
            leaveButton.onClick.RemoveListener(Leave);
            copyButton.onClick.RemoveListener(CopyCode);
            if (modelDropdown != null) modelDropdown.onValueChanged.RemoveListener(OnModelChanged);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
