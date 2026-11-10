using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DogSimulator.CustomDogFace;
using DogSimulator.Player;
using DogSimulator.UI;
using TMPro;
using UnityEngine;

namespace DogSimulator.Networking
{
    [DefaultExecutionOrder(100)]
    internal sealed partial class DogFrontEnd : MonoBehaviour
    {
        private const string ModelKey = "DogSimulator.DogModelId";
        private const string NicknameKey = "DogSimulator.Nickname";
        private enum Page { Home, Prepare, Hub, Face, Gameplay }
        private enum Launch { SinglePlayer, Create, Join }
        private readonly Dictionary<Page, CanvasGroup> pages = new();
        [SerializeField] private FusionSessionService session;
        [SerializeField] private DogSelectionPreview preview;
        [SerializeField] private FpsHud fps;
        [SerializeField] private DogMenuSkin skin;
        private CanvasGroup activePage;
        [SerializeField] private CanvasGroup settings;
        [SerializeField] private GameObject yard;
        [SerializeField] private CanvasGroup[] authoredPages;
#if UNITY_EDITOR
        private RectTransform stage;
#endif
        [SerializeField] private TMP_InputField nickname;
        [SerializeField] private TMP_InputField roomCode;
        [SerializeField] private TMP_Text prepareMessage;
        [SerializeField] private TMP_Text hubMessage;
        [SerializeField] private UnityEngine.UI.Button continueButton;
        [SerializeField] private UnityEngine.UI.Button cancelConnectionButton;
        [SerializeField] private UnityEngine.UI.Image homeDog;
        [SerializeField] private UnityEngine.UI.RawImage selectedDog;
        [SerializeField] private UnityEngine.UI.RawImage faceDog;
        [SerializeField] private UnityEngine.UI.RawImage faceCanvas;
        [SerializeField] private DogFaceEditorController faceEditor;
        private Page page;
        private Launch launch;
        private string pendingCode;
        private int selectedIndex;
        private int worldSlot = 1;
        private bool playing;
        private bool starting;
        private bool leaving;

        public void Bind(FusionSessionService service, DogSelectionPreview selection, FpsHud fpsHud)
        {
            session = service;
            preview = selection;
            fps = fpsHud;
        }

        private void Start()
        {
            if (session == null || preview == null || authoredPages == null || authoredPages.Length != 5)
            {
                Debug.LogError("Dog menu layout is missing. Run the Editor-first migration.", this);
                enabled = false;
                return;
            }
            for (var i = 0; i < authoredPages.Length; i++) pages.Add((Page)i, authoredPages[i]);
            nickname.SetTextWithoutNotify(PlayerPrefs.GetString(NicknameKey, "Guest"));
            settingsHint.text = WithKey(SettingsKey?.shortDisplayName ?? SettingsKey?.displayName, "Settings");
            BindSettings();
            BindFaceTools();
            foreach (var group in pages.Values) group.gameObject.SetActive(false);
            var saved = PlayerPrefs.GetInt(ModelKey, session.ModelCatalog.FallbackModelId);
            for (var i = 0; i < session.ModelCatalog.Entries.Count; i++)
                if (session.ModelCatalog.Entries[i].ModelId == saved) selectedIndex = i;
            session.Changed += OnSessionChanged;
            session.RoomsChanged += OnRoomListChanged;
            Show(Page.Home);
        }

        private DogModelCatalog.Entry Selected => session.ModelCatalog.Entries[selectedIndex];

        private void Show(Page next)
        {
            if (page == Page.Hub && next != Page.Hub) _ = session.StopBrowsing();
            page = next;
            activePage?.gameObject.SetActive(false);
            activePage = pages[next];
            activePage.alpha = 0;
            activePage.interactable = activePage.blocksRaycasts = true;
            activePage.gameObject.SetActive(true);
            yard.SetActive(next != Page.Gameplay);
            preview.SetVisible(false);
            switch (next)
            {
                case Page.Home:
                    preview.PresentHomeDog(homeDog);
                    break;
                case Page.Prepare:
                    ShowSelectedDog();
                    break;
                case Page.Hub:
                    session.BrowseRooms();
                    break;
                case Page.Face:
                    preview.PresentIn(faceDog);
                    preview.ShowModel(Selected.ModelId);
                    preview.FocusFace();
                    SetFaceView(true);
                    faceEditor.Open(session.IsConnected && !session.IsSinglePlayer);
                    break;
                case Page.Gameplay:
                    roomStatus.transform.parent.gameObject.SetActive(!session.IsSinglePlayer);
                    nextRoomStatusUpdate = 0;
                    break;
            }
        }

        private void Prepare(Launch mode, string code = null)
        {
            launch = mode;
            pendingCode = code;
            prepareMessage.text = "";
            Show(Page.Prepare);
        }

        private void BrowseDog(int offset)
        {
            if (starting || leaving) return;
            var count = session.ModelCatalog.Entries.Count;
            ChooseDog((selectedIndex + offset + count) % count);
        }

        private void ChooseDog(int index)
        {
            if (starting || leaving) return;
            selectedIndex = index;
            ShowSelectedDog();
        }

        private void ShowSelectedDog()
        {
            preview.PresentIn(selectedDog);
            preview.ShowModel(Selected.ModelId);
            if (Selected.SupportsCustomFace && DogFaceStorage.TryLoad(out var saved))
                preview.ApplyFaceJpeg(saved);
            else preview.ResetFaceToDefault();
            UpdateDogStrip();
            RefreshPrepare();
        }

        private void OpenFace()
        {
            if (starting || leaving) return;
            if (!Selected.SupportsCustomFace)
            {
                var entries = session.ModelCatalog.Entries;
                for (var i = 0; i < entries.Count; i++)
                {
                    if (!entries[i].SupportsCustomFace) continue;
                    selectedIndex = i;
                    break;
                }
            }
            if (Selected.SupportsCustomFace) Show(Page.Face);
            else prepareMessage.text = "There is no customizable dog in this catalog.";
        }

        private void SetFaceView(bool showModel)
        {
            preview.SetVisible(showModel);
            faceCanvas.enabled = !showModel;
            faceViewLabel.text = showModel ? "Edit Canvas" : "3D Preview";
        }

        private async void Continue()
        {
            if (starting || leaving) return;
            if (!GuestProfile.TryNormalizeName(nickname.text, out var name, out var error))
            {
                prepareMessage.text = error;
                return;
            }
            PlayerPrefs.SetString(NicknameKey, name);
            PlayerPrefs.SetInt(ModelKey, Selected.ModelId);
            PlayerPrefs.Save();
            starting = true;
            RefreshPrepare();
            try
            {
                if (session.State == SessionState.Selecting) await session.SelectModelAsync(Selected.ModelId);
                else await StartGameAsync(name);
            }
            finally
            {
                if (this != null)
                {
                    starting = false;
                    RefreshPrepare();
                }
            }
        }

        private Task StartGameAsync(string name)
        {
            switch (launch)
            {
                case Launch.SinglePlayer: return session.StartSinglePlayerAsync(name, Selected.ModelId, worldSlot);
                case Launch.Create: return session.CreateAsync(name, Selected.ModelId, worldSlot);
                default: return session.JoinAsync(name, Selected.ModelId, pendingCode);
            }
        }

        private void OnSessionChanged()
        {
            if (session.HasAvatar && !playing && !leaving && page == Page.Prepare)
            {
                ResumeGame();
            }
            else if (!leaving && (session.State == SessionState.Disconnected || session.State == SessionState.Failed)
                && (page == Page.Gameplay || session.Message == FusionSessionService.OwnerLeftMessage))
            {
                playing = false;
                settings.gameObject.SetActive(false);
                if (page == Page.Face && faceEditor.IsOpen) faceEditor.Close();
                UnlockCursor();
                Show(launch == Launch.SinglePlayer ? Page.Home : Page.Hub);
                hubMessage.text = session.Message;
            }
            if (page == Page.Prepare || page == Page.Face) prepareMessage.text = session.Message;
            RefreshPrepare();
        }

        private void RefreshPrepare()
        {
            var busy = starting || leaving;
            continueButton.interactable = !busy;
            nickname.interactable = !busy && !session.IsConnected;
            prepareBack.interactable = !busy;
            faceButton.interactable = faceTab.interactable = !busy;
            previousDog.interactable = nextDog.interactable = !busy;
            foreach (var tile in dogTiles) tile.interactable = !busy;
            RefreshWorldSlots(!busy);
            cancelConnectionButton.gameObject.SetActive(session.State is SessionState.Connecting or SessionState.Selecting);
            cancelConnectionButton.interactable = !leaving;
            continueLabel.text = session.State == SessionState.Selecting ? "Use This Dog"
                : launch == Launch.SinglePlayer ? "Play" : launch == Launch.Create ? "Create Room" : "Join Room";
        }

        private void ResumeGame()
        {
            settings.gameObject.SetActive(false);
            if (page != Page.Gameplay) Show(Page.Gameplay);
            playing = true;
            session.SetControlsEnabled(true);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void PauseGame()
        {
            playing = false;
            session.SetControlsEnabled(false);
            UnlockCursor();
            OpenSettings();
        }

        private static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private async void LeaveGame()
        {
            if (leaving) return;
            leaving = true;
            playing = false;
            UnlockCursor();
            RefreshPrepare();
            settingsLeave.interactable = settingsResume.interactable = settingsClose.interactable = settingsApply.interactable = false;
            await session.LeaveAsync();
            if (this == null) return;
            leaving = false;
            settingsLeave.interactable = settingsResume.interactable = settingsClose.interactable = settingsApply.interactable = true;
            settings.gameObject.SetActive(false);
            Show(launch == Launch.SinglePlayer ? Page.Home : Page.Hub);
            hubMessage.text = "";
        }

        private void Update()
        {
            if (activePage == null) return;
            var delta = Time.unscaledDeltaTime * 6.5f;
            activePage.alpha = Mathf.MoveTowards(activePage.alpha, 1, delta);
            if (page == Page.Prepare) prepareStatusStrip.SetActive(!string.IsNullOrEmpty(prepareMessage.text));
            if (settings.gameObject.activeSelf) settings.alpha = Mathf.MoveTowards(settings.alpha, 1, delta);
            var escape = SettingsKey?.wasPressedThisFrame == true;
            if (playing && (escape || Cursor.lockState != CursorLockMode.Locked)) PauseGame();
            else if (escape && settings.gameObject.activeSelf && !leaving) CloseSettings();
            else if (escape && page == Page.Face) faceEditor.Close();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && playing) PauseGame();
        }

        private void OnDestroy()
        {
            if (session != null)
            {
                session.Changed -= OnSessionChanged;
                session.RoomsChanged -= OnRoomListChanged;
                session.SetControlsEnabled(false);
            }
            UnlockCursor();
        }
    }
}
