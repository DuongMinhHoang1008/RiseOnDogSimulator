using DogSimulator.CustomDogFace;
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
        [SerializeField] private UnityEngine.UI.Button editFaceButton;
        [SerializeField] private DogFaceEditorController faceEditor;
        [SerializeField] private TMP_Text controls;
        [SerializeField] private TMP_Text networkSettings;
        [SerializeField] private DogFrontEnd frontEnd;
        [SerializeField] private CanvasGroup legacyMenu;
        private bool playing;
        private float nextHudUpdate;
        private bool suppressModelChange;
        private bool useFrontEnd;
        private const string ModelPreferenceKey = "DogSimulator.DogModelId";

        private void Awake()
        {
            // Older generated preview scenes still use the diagnostic UI.
            if (frontEnd != null)
            {
                // The selection camera driver lives under this panel and remains active.
                legacyMenu.alpha = 0;
                legacyMenu.interactable = legacyMenu.blocksRaycasts = false;
                hud.SetActive(false);
                useFrontEnd = true;
                return;
            }
            nickname.richText = roomCode.richText = false;
            if (controls != null) controls.text = DogAnimationDriver.Controls;
            if (networkSettings != null)
                networkSettings.text = $"Region: {session.Region} | Capacity: {session.Capacity} | {session.GameVersion}";
            nickname.text = PlayerPrefs.GetString("DogSimulator.Nickname", "Guest");
            PopulateModels();
            selectionPreview?.ShowModel(SelectedModelId());
            createButton.onClick.AddListener(Create);
            joinButton.onClick.AddListener(Join);
            resumeButton.onClick.AddListener(Resume);
            leaveButton.onClick.AddListener(Leave);
            copyButton.onClick.AddListener(CopyCode);
            modelDropdown.onValueChanged.AddListener(OnModelChanged);
            if (editFaceButton != null) editFaceButton.onClick.AddListener(OpenFaceEditor);
            if (faceEditor != null)
            {
                faceEditor.Applied += OnFaceApplied;
                faceEditor.DefaultRequested += OnDefaultFace;
                faceEditor.Closed += OnFaceEditorClosed;
            }
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
            RefreshFaceButton();
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
            RefreshFaceButton();
            if (session.IsConnected) roomCode.text = session.RoomCode;
            selectionPreview?.SetVisible(true);
            if (session.HasAvatar && (faceEditor == null || !faceEditor.IsOpen)) Resume();
            else Pause();
        }

        private void RefreshFaceButton()
        {
            if (editFaceButton == null) return;
            editFaceButton.interactable = session.ModelCatalog != null
                && session.ModelCatalog.TryGet(SelectedModelId(), out var entry) && entry.SupportsCustomFace;
        }

        private void OpenFaceEditor()
        {
            if (faceEditor == null || session.ModelCatalog == null
                || !session.ModelCatalog.TryGet(SelectedModelId(), out var entry) || !entry.SupportsCustomFace) return;
            Pause();
            faceEditor.Open(session.IsConnected);
        }

        private void OnFaceApplied(byte[] jpeg)
        {
            selectionPreview?.ApplyFaceJpeg(jpeg);
            if (session.LocalPlayer != null) session.LocalPlayer.PublishCustomFace(jpeg);
        }

        private void OnDefaultFace()
        {
            selectionPreview?.ResetFaceToDefault();
            session.LocalPlayer?.PublishDefaultFace();
        }

        private void OnFaceEditorClosed()
        {
            if (session.HasAvatar) Resume();
            else Pause();
        }

        private void Resume()
        {
            if (!session.HasAvatar || (faceEditor != null && faceEditor.IsOpen)) return;
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
            if (useFrontEnd) return;
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
            if (useFrontEnd) return;
            if (session != null) { session.Changed -= OnSessionChanged; session.SetControlsEnabled(false); }
            createButton.onClick.RemoveListener(Create);
            joinButton.onClick.RemoveListener(Join);
            resumeButton.onClick.RemoveListener(Resume);
            leaveButton.onClick.RemoveListener(Leave);
            copyButton.onClick.RemoveListener(CopyCode);
            if (modelDropdown != null) modelDropdown.onValueChanged.RemoveListener(OnModelChanged);
            if (editFaceButton != null) editFaceButton.onClick.RemoveListener(OpenFaceEditor);
            if (faceEditor != null)
            {
                faceEditor.Applied -= OnFaceApplied;
                faceEditor.DefaultRequested -= OnDefaultFace;
                faceEditor.Closed -= OnFaceEditorClosed;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
