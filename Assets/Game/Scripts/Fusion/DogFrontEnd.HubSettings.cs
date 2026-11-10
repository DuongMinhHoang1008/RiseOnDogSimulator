using System;
using DogSimulator.Player;
using DogSimulator.UI;
using TMPro;
using UnityEngine;
using static DogSimulator.Networking.DogMenuSkin;

namespace DogSimulator.Networking
{
    internal sealed partial class DogFrontEnd
    {
        private const int RoomsPerPage = 5;
        private const string VSyncKey = "DogSimulator.VSync";
        private const string VolumeKey = "DogSimulator.MasterVolume";
        private const string QualityKey = "DogSimulator.GraphicsQuality";
        private int roomPage;
        [SerializeField] private RectTransform[] roomRows;
        [SerializeField] private TMP_Text[] roomNames;
        [SerializeField] private TMP_Text[] roomPlayers;
        [SerializeField] private UnityEngine.UI.Button[] roomJoins;
        [SerializeField] private TMP_Text roomPager;
        [SerializeField] private TMP_Text emptyRooms;
        [SerializeField] private UnityEngine.UI.Button previousRoomPage;
        [SerializeField] private UnityEngine.UI.Button nextRoomPage;
        [SerializeField] private UnityEngine.UI.Button settingsCopy;
        [SerializeField] private UnityEngine.UI.Button settingsLeave;
        [SerializeField] private UnityEngine.UI.Button settingsResume;
        [SerializeField] private UnityEngine.UI.Button settingsClose;
        [SerializeField] private UnityEngine.UI.Button settingsApply;
        [SerializeField] private TMP_Text settingsMessage;
        [SerializeField] private TMP_Text qualityValue;
        [SerializeField] private TMP_Text volumeValue;
        [SerializeField] private UnityEngine.UI.Slider volume;
        [SerializeField] private RectTransform[] settingsTabs;
        [SerializeField] private UnityEngine.UI.Button[] settingsTabButtons;
        [SerializeField] private TMP_Text[] settingsToggles = new TMP_Text[4];
        [SerializeField] private UnityEngine.UI.Button[] settingButtons = new UnityEngine.UI.Button[4];
        private readonly bool[] draftToggles = new bool[4];
        private int draftQuality;
        private int defaultQuality;

#if UNITY_EDITOR
        private void BuildHub(RectTransform root)
        {
            skin.Surface(At(root, "Hub Card", 76, 121, 1448, 671), skin.Paper, Color.white);
            Header(root, "Multiplayer Hub", 500, 76, 600, 88);
            skin.Button(At(root, "Back", 99, 809, 170, 62), "‹ Back", ShowHome, false, 27);
            skin.Button(At(root, "Refresh Rooms", 1327, 151, 152, 59), "Refresh", session.BrowseRooms, false, 25);
            skin.Button(At(root, "Create Room", 116, 240, 377, 81), "+  Create Room", PrepareCreate, true, 35, skin.Paw);
            skin.Label(At(root, "Join By Code Heading", 117, 363, 373, 45), "— Join by Code —", 30, Brown);
            roomCode = skin.Input(At(root, "Room Code", 115, 420, 380, 65), "Enter room code…", "", 8);
            skin.Button(At(root, "Join Room", 115, 508, 380, 76), "Join Room  ›", JoinEnteredRoom, true, 35);
            skin.Label(At(root, "Region", 113, 626, 380, 48), $"Region  •  {session.Region.ToUpperInvariant()}", 25, Muted);
            var paw = skin.Surface(At(root, "Hub Paw", 143, 675, 85, 85), skin.Paw, new Color(.74f, .49f, .30f, .38f));
            paw.rectTransform.localRotation = Quaternion.Euler(0, 0, -17);
            skin.Surface(At(root, "Hub Divider", 524, 219, 2, 498), null, new Color(.79f, .65f, .50f, .58f));

            skin.Surface(At(root, "Room List Header", 557, 229, 925, 58), skin.Paper, Cocoa);
            skin.Label(At(root, "Room Header", 588, 229, 396, 58), "Open Rooms", 28, Color.white, TextAlignmentOptions.Left);
            skin.Label(At(root, "Region Header", 1010, 229, 170, 58), "Region", 26, Color.white);
            skin.Label(At(root, "Players Header", 1191, 229, 115, 58), "Players", 26, Color.white);

            roomRows = new RectTransform[RoomsPerPage];
            roomNames = new TMP_Text[RoomsPerPage];
            roomPlayers = new TMP_Text[RoomsPerPage];
            roomJoins = new UnityEngine.UI.Button[RoomsPerPage];
            for (var i = 0; i < RoomsPerPage; i++)
            {
                var slot = i;
                var row = At(root, $"Room {i + 1}", 557, 299 + i * 70, 925, 60);
                roomRows[i] = row;
                skin.Surface(row, skin.Paper, Color.white);
                skin.Surface(At(row, "Room Icon", 26, 105, 83, 670), i % 2 == 0 ? skin.Paw : skin.Bone, Cocoa).preserveAspect = true;
                roomNames[i] = skin.Label(At(row, "Room Name", 130, 0, 580, 900), "", 28, Brown, TextAlignmentOptions.Left);
                skin.Label(At(row, "Region", 777, 0, 306, 900), session.Region.ToUpperInvariant(), 24, Brown);
                roomPlayers[i] = skin.Label(At(row, "Players", 1110, 0, 163, 900), "", 24, Brown);
                roomJoins[i] = skin.Button(At(row, "Join", 1335, 106, 246, 675), "Join", JoinListedRoom, slot, true, 24);
            }
            emptyRooms = skin.Label(At(root, "Empty Rooms", 570, 329, 885, 140),
                "No open rooms in this region.", 29, Muted);
            previousRoomPage = skin.Button(At(root, "Previous Rooms", 855, 673, 64, 58), "‹", TurnRoomPage, -1, false, 37);
            roomPager = skin.Label(At(root, "Page Count", 922, 673, 180, 58), "", 27, Brown);
            nextRoomPage = skin.Button(At(root, "Next Rooms", 1106, 673, 64, 58), "›", TurnRoomPage, 1, false, 37);
            hubMessage = skin.Label(At(root, "Hub Status", 554, 738, 921, 46), "", 23, Brown);
        }

#endif

        private void ShowHome() => Show(Page.Home);
        private void PrepareCreate() => Prepare(Launch.Create);
        private void JoinEnteredRoom() => JoinFromHub(roomCode.text);

        private void ToggleSetting(int index)
        {
            draftToggles[index] = !draftToggles[index];
            RefreshSettingToggle(index);
        }

        private void BindSettings()
        {
            roomCode.onSubmit.AddListener(JoinFromHub);
            volume.onValueChanged.AddListener(value => volumeValue.text = $"{value:P0}");
            defaultQuality = QualitySettings.GetQualityLevel();
            ReadSettings();
            ApplySettings(false);
        }

        private void JoinFromHub(string code)
        {
            if (!RoomCodeService.TryNormalize(code, out var normalized))
            {
                hubMessage.text = "Enter a valid 6–8 character room code.";
                return;
            }
            roomCode.SetTextWithoutNotify(normalized);
            hubMessage.text = "";
            Prepare(Launch.Join, normalized);
        }

        private void JoinListedRoom(int slot)
        {
            var index = roomPage * RoomsPerPage + slot;
            if (index < session.Rooms.Count) JoinFromHub(session.Rooms[index].Code);
        }

        private void OnRoomListChanged()
        {
            if (page != Page.Hub) return;
            emptyRooms.text = string.IsNullOrEmpty(session.RoomListMessage)
                ? "No open rooms in this region." : session.RoomListMessage;
            TurnRoomPage(0);
        }

        private void TurnRoomPage(int delta)
        {
            var rooms = session.Rooms;
            var count = Mathf.Max(1, Mathf.CeilToInt(rooms.Count / (float)RoomsPerPage));
            roomPage = Mathf.Clamp(roomPage + delta, 0, count - 1);
            emptyRooms.gameObject.SetActive(rooms.Count == 0);
            previousRoomPage.interactable = roomPage > 0;
            nextRoomPage.interactable = roomPage + 1 < count;
            roomPager.text = $"{roomPage + 1} / {count}";
            for (var i = 0; i < roomRows.Length; i++)
            {
                var index = roomPage * RoomsPerPage + i;
                roomRows[i].gameObject.SetActive(index < rooms.Count);
                if (index >= rooms.Count) continue;
                var room = rooms[index];
                roomNames[i].text = $"Room {room.Code}";
                roomPlayers[i].text = $"{room.Players} / {room.Capacity}";
                roomJoins[i].interactable = room.Players < room.Capacity;
            }
        }

#if UNITY_EDITOR
        private void Header(Transform root, string title, float x, float y, float width, float height)
        {
            var plaque = At(root, title + " Plaque", x, y, width, height);
            skin.Surface(plaque, skin.Paper, Color.white);
            skin.Surface(At(plaque, "Bone", 51, 135, 226, 630), skin.Bone, Cocoa).preserveAspect = true;
            skin.Label(At(plaque, "Title", 255, 0, 1305, 900), title, 47, Brown).fontStyle = FontStyles.Bold;
        }

        private void BuildSettings(RectTransform parent)
        {
            var popup = At(parent, "Settings Popup", 0, 0, 1600, 900);
            settings = popup.gameObject.AddComponent<CanvasGroup>();
            skin.Surface(At(popup, "Backdrop", 0, 0, 1600, 900), null, new Color(.14f, .10f, .09f, .72f), true);
            var root = FitMenuFrame(popup, "Settings Frame");
            skin.Surface(At(root, "Settings Card", 244, 130, 1112, 641), skin.Paper, Color.white);
            Header(root, "Settings", 552, 83, 496, 90);
            settingsClose = skin.Button(At(root, "Close Settings", 1290, 110, 73, 73), "×", CloseSettings, true, 45);
            settingsClose.image.color = new Color(1f, .58f, .46f);
            skin.Surface(At(root, "Settings Divider", 578, 214, 2, 402), null, new Color(.77f, .62f, .49f, .55f));
            skin.Surface(At(root, "Settings Paw", 303, 589, 92, 92), skin.Paw, new Color(.75f, .49f, .29f, .40f));

            var categories = new[] { "General", "Graphics", "Audio", "Controls" };
            settingsTabs = new RectTransform[categories.Length];
            settingsTabButtons = new UnityEngine.UI.Button[categories.Length];
            for (var i = 0; i < categories.Length; i++)
            {
                var index = i;
                settingsTabButtons[i] = skin.Button(At(root, categories[i], 273, 222 + i * 81, 270, 65),
                    categories[i], SelectSettingsTab, index, i == 0, 30);
                settingsTabs[i] = At(root, categories[i] + " Settings", 0, 0, 1600, 900);
            }

            var general = settingsTabs[0];
            SettingsLabel(general, "Language", 217);
            skin.Surface(At(general, "Language Value", 1079, 221, 216, 49), skin.Paper, Color.white);
            skin.Label(At(general, "English", 1086, 221, 202, 49), "English", 26, Brown);
            AddSettingToggle(general, "Show FPS", 289, 0);
            AddSettingToggle(general, "VSync", 359, 1);
            AddSettingToggle(general, "Camera Shake", 429, 2);
            AddSettingToggle(general, "Show Player Name", 499, 3);

            var graphics = settingsTabs[1];
            SettingsLabel(graphics, "Quality Preset", 231);
            skin.Button(At(graphics, "Previous Quality", 1000, 233, 61, 50), "‹", ChangeQuality, -1, false, 32);
            qualityValue = skin.Label(At(graphics, "Quality Value", 1068, 233, 154, 50), "", 25, Brown);
            skin.Button(At(graphics, "Next Quality", 1230, 233, 61, 50), "›", ChangeQuality, 1, false, 32);

            var audio = settingsTabs[2];
            SettingsLabel(audio, "Master Volume", 227);
            volume = skin.Slider(At(audio, "Master Volume Slider", 620, 306, 663, 29), 0, 1, 1);
            volumeValue = skin.Label(At(audio, "Master Volume Value", 1111, 227, 181, 57), "", 27, Brown, TextAlignmentOptions.Right);

            var controls = settingsTabs[3];
            var instructions = skin.Label(At(controls, "Gameplay Controls", 615, 227, 682, 369),
                DogAnimationDriver.Controls + "\nEsc  •  Open Settings", 28, Brown, TextAlignmentOptions.TopLeft);
            instructions.textWrappingMode = TextWrappingModes.Normal;

            settingsCopy = skin.Button(At(root, "Copy Room Code", 616, 561, 318, 52), "Copy Room Code", CopyRoomCode, false, 26);
            settingsMessage = skin.Label(At(root, "Settings Status", 951, 562, 344, 51), "", 22, Brown);
            skin.Button(At(root, "Reset Defaults", 274, 696, 274, 55), "Reset to Default", ResetSettings, false, 26);
            settingsLeave = skin.Button(At(root, "Leave Game", 588, 696, 207, 55), "Leave Game", LeaveGame, false, 27);
            settingsResume = skin.Button(At(root, "Resume", 816, 696, 187, 55), "Resume", CloseSettings, false, 27);
            settingsApply = skin.Button(At(root, "Apply Settings", 1028, 688, 275, 66), "Apply", ApplyAndClose, true, 32);
            settings.gameObject.SetActive(false);
        }

        private void AddSettingToggle(Transform root, string name, float y, int index)
        {
            SettingsLabel(root, name, y);
            settingButtons[index] = skin.Button(At(root, name, 1145, y + 6, 150, 44), "", ToggleSetting, index, true, 25);
            settingsToggles[index] = settingButtons[index].GetComponentInChildren<TMP_Text>();
        }

        private void SettingsLabel(Transform root, string name, float y)
            => skin.Label(At(root, name + " Label", 614, y, 448, 57), name, 29, Brown, TextAlignmentOptions.Left);

#endif

        private void RefreshSettingToggle(int index)
        {
            var text = settingsToggles[index];
            text.text = draftToggles[index] ? "ON" : "OFF";
            settingButtons[index].image.sprite = draftToggles[index] ? skin.Primary : skin.Paper;
        }

        private void SelectSettingsTab(int index)
        {
            for (var i = 0; i < settingsTabs.Length; i++)
            {
                settingsTabs[i].gameObject.SetActive(i == index);
                settingsTabButtons[i].image.sprite = i == index ? skin.Primary : skin.Paper;
            }
            settingsCopy.gameObject.SetActive(index == 0 && session.IsConnected && !session.IsSinglePlayer);
            settingsMessage.text = "";
        }

        private void OpenSettings()
        {
            ReadSettings();
            SelectSettingsTab(0);
            settingsLeave.gameObject.SetActive(session.HasAvatar);
            settingsResume.gameObject.SetActive(session.HasAvatar);
            settings.alpha = 0;
            settings.gameObject.SetActive(true);
            activePage.interactable = false;
            activePage.blocksRaycasts = false;
        }

        private void CloseSettings()
        {
            settings.gameObject.SetActive(false);
            activePage.interactable = activePage.blocksRaycasts = true;
            if (session.HasAvatar) ResumeGame();
        }

        private void CopyRoomCode()
        {
            if (!session.IsConnected || session.IsSinglePlayer) return;
            settingsMessage.text = WebClipboard.Copy(session.RoomCode)
                ? $"Copied {session.RoomCode}" : $"Room code: {session.RoomCode}";
        }

        private void ReadSettings()
        {
            draftToggles[0] = PlayerPrefs.GetInt(FpsHud.PreferenceKey, 0) != 0;
            draftToggles[1] = PlayerPrefs.GetInt(VSyncKey, 1) != 0;
            draftToggles[2] = PlayerPrefs.GetInt(DogCamera.ShakePreferenceKey, 1) != 0;
            draftToggles[3] = PlayerPrefs.GetInt(NetworkDogPlayer.NamesPreferenceKey, 1) != 0;
            draftQuality = Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, defaultQuality), 0, QualitySettings.names.Length - 1);
            volume.value = PlayerPrefs.GetFloat(VolumeKey, 1);
            RefreshSettingsValues();
        }

        private void ResetSettings()
        {
            draftToggles[0] = false;
            draftToggles[1] = draftToggles[2] = draftToggles[3] = true;
            draftQuality = defaultQuality;
            volume.value = 1;
            RefreshSettingsValues();
        }

        private void ChangeQuality(int delta)
        {
            draftQuality = (draftQuality + delta + QualitySettings.names.Length) % QualitySettings.names.Length;
            qualityValue.text = QualitySettings.names[draftQuality];
        }

        private void RefreshSettingsValues()
        {
            for (var i = 0; i < draftToggles.Length; i++) RefreshSettingToggle(i);
            qualityValue.text = QualitySettings.names[draftQuality];
            volumeValue.text = $"{volume.value:P0}";
        }

        private void ApplyAndClose()
        {
            ApplySettings(true);
            CloseSettings();
        }

        private void ApplySettings(bool persist)
        {
            fps?.SetVisible(draftToggles[0]);
            if (QualitySettings.GetQualityLevel() != draftQuality) QualitySettings.SetQualityLevel(draftQuality);
            QualitySettings.vSyncCount = draftToggles[1] ? 1 : 0;
            DogCamera.ShakeEnabled = draftToggles[2];
            NetworkDogPlayer.ShowNameplates = draftToggles[3];
            AudioListener.volume = volume.value;
            if (!persist) return;
            PlayerPrefs.SetInt(VSyncKey, draftToggles[1] ? 1 : 0);
            PlayerPrefs.SetInt(DogCamera.ShakePreferenceKey, draftToggles[2] ? 1 : 0);
            PlayerPrefs.SetInt(NetworkDogPlayer.NamesPreferenceKey, draftToggles[3] ? 1 : 0);
            PlayerPrefs.SetInt(QualityKey, draftQuality);
            PlayerPrefs.SetFloat(VolumeKey, volume.value);
            PlayerPrefs.Save();
        }
    }
}
