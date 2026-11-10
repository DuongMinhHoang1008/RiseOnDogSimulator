using System;
using DogSimulator.CustomDogFace;
using DogSimulator.Player;
using TMPro;
using UnityEngine;
using static DogSimulator.Networking.DogMenuSkin;

namespace DogSimulator.Networking
{
    internal sealed partial class DogFrontEnd
    {
        [SerializeField] private TMP_Text dogName;
        [SerializeField] private TMP_Text continueLabel;
        [SerializeField] private TMP_Text faceViewLabel;
        [SerializeField] private TMP_Text faceHelp;
        [SerializeField] private GameObject prepareStatusStrip;
        [SerializeField] private UnityEngine.UI.Button prepareBack;
        [SerializeField] private UnityEngine.UI.Button faceButton;
        [SerializeField] private UnityEngine.UI.Button faceTab;
        [SerializeField] private UnityEngine.UI.Button previousDog;
        [SerializeField] private UnityEngine.UI.Button nextDog;
        [SerializeField] private UnityEngine.UI.Button[] dogTiles;
        [SerializeField] private RectTransform worldSlots;
        [SerializeField] private UnityEngine.UI.Button[] worldButtons;

        [SerializeField] private TMP_Text[] worldLabels;
        [SerializeField] private TMP_Text[] dogTileLabels;
        [SerializeField] private UnityEngine.UI.Image[] dogPortraits;
        [SerializeField] private GameObject exitNote;
        [SerializeField] private GameObject exitPaper;
        [SerializeField] private RectTransform[] faceTabs;
        [SerializeField] private UnityEngine.UI.Button[] faceTabButtons;
        [SerializeField] private UnityEngine.UI.Button brushButton;
        [SerializeField] private UnityEngine.UI.Button eraseButton;
        [SerializeField] private TMP_Text imageHint;
        [SerializeField] private BrushColorPicker brushPicker;
        [SerializeField] private UnityEngine.UI.Image brushColorPreview;
        [SerializeField] private UnityEngine.UI.Slider red;
        [SerializeField] private UnityEngine.UI.Slider green;
        [SerializeField] private UnityEngine.UI.Slider blue;
        private bool brushPickerReady;

#if UNITY_EDITOR
        internal void BakeLayout(Func<Sprite, Sprite> saveSprite, Func<TMP_FontAsset> loadFont)
        {
            skin = new DogMenuSkin(saveSprite, loadFont);
            BuildLayout();
            authoredPages = new CanvasGroup[pages.Count];
            foreach (var pair in pages)
            {
                authoredPages[(int)pair.Key] = pair.Value;
                pair.Value.gameObject.SetActive(false);
            }
            pages.Clear();
        }

        private void BuildLayout()
        {
            // Keep the fitted menu at its reference size in canvas units so text and controls scale together.
            if (TryGetComponent<UnityEngine.UI.CanvasScaler>(out var scaler))
                scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;

            var root = At(transform, "Dog Simulator Menu", 0, 0, 1600, 900);
            var background = At(root, "Illustrated Yard", 0, 0, 1600, 900);
            var backgroundImage = background.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            backgroundImage.texture = Resources.Load<Texture2D>("DogMenu/Yard");
            backgroundImage.raycastTarget = false;
            if (backgroundImage.texture != null)
            {
                var cover = background.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
                cover.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent;
                cover.aspectRatio = (float)backgroundImage.texture.width / backgroundImage.texture.height;
            }
            yard = background.gameObject;

            stage = FitMenuFrame(root, "Menu Frame");
            BuildHome(AddPage(Page.Home, "HomeScene"));
            BuildPrepare(AddPage(Page.Prepare, "PrepareScene"));
            BuildHub(AddPage(Page.Hub, "MultiplayerHub"));
            BuildFace(AddPage(Page.Face, "FaceCustomize"));
            BuildGameplayHud(AddPage(Page.Gameplay, "Gameplay HUD", root));
            BuildSettings(root);
        }

        private static RectTransform FitMenuFrame(Transform parent, string name)
        {
            var frame = At(parent, name, 0, 0, 1600, 900);
            var fitter = frame.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            fitter.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 16f / 9;
            return frame;
        }

        private RectTransform AddPage(Page key, string name, RectTransform parent = null)
        {
            var rect = At(parent != null ? parent : stage, name, 0, 0, 1600, 900);
            pages.Add(key, rect.gameObject.AddComponent<CanvasGroup>());
            return rect;
        }

        private void BuildHome(RectTransform root)
        {
            var mark = At(root, "Dog Simulator Logo", 96, 91, 584, 252);
            skin.Surface(At(mark, "Logo Plaque", 30, 70, 1535, 705), skin.Paper, Color.white);
            skin.Surface(At(mark, "Paw", 95, 185, 244, 435), skin.Paw, Brown).preserveAspect = true;
            var title = skin.Label(At(mark, "Dog", 315, 55, 1160, 695), "DOG", 139, Brown);
            title.fontStyle = FontStyles.Bold;
            skin.Surface(At(mark, "Simulator Plaque", 180, 610, 1315, 272), skin.Primary, Color.white);
            skin.Label(At(mark, "Simulator", 192, 610, 1290, 272), "SIMULATOR", 58, Brown).fontStyle = FontStyles.Bold;
            mark.localRotation = Quaternion.Euler(0, 0, 3);
            homeDog = skin.Surface(At(root, "Floating Mascot", 684, 69, 790, 790), null, Color.white);
            homeDog.preserveAspect = true;
            skin.Surface(At(root, "Decorative Bone", 1281, 639, 120, 90), skin.Bone, Cream)
                .rectTransform.localRotation = Quaternion.Euler(0, 0, 24);
            skin.Surface(At(root, "Decorative Paw", 795, 179, 87, 87), skin.Paw, Cream)
                .rectTransform.localRotation = Quaternion.Euler(0, 0, -20);
            skin.Button(At(root, "Singleplayer", 114, 365, 530, 92), "Singleplayer", PrepareSinglePlayer, true, 42, skin.Paw);
            skin.Button(At(root, "Multiplayer", 114, 472, 530, 85), "Multiplayer", ShowHub, false, 41, skin.Bone);
            skin.Button(At(root, "Options", 114, 571, 500, 81), "Options", OpenSettings, false, 39, skin.Paw);
            var webExit = skin.Label(At(root, "Exit Note", 110, 755, 465, 48), "Close this browser tab to exit.", 23, Brown);
            exitNote = webExit.gameObject;
            exitPaper = skin.Surface(At(root, "Exit Note Paper", 114, 755, 457, 48), skin.Paper, Color.white).gameObject;
            exitPaper.transform.SetSiblingIndex(webExit.transform.GetSiblingIndex());
            webExit.transform.SetAsLastSibling();
            exitNote.SetActive(false);
            exitPaper.SetActive(false);
            skin.Button(At(root, "Exit", 114, 666, 475, 81), "Exit", Exit, false, 38, skin.Bone);
        }

        private void BuildPrepare(RectTransform root)
        {
            skin.Surface(At(root, "Body and Face Tabs", 515, 33, 570, 80), skin.Paper, Color.white);
            skin.Surface(At(root, "Body Tab", 522, 39, 277, 66), skin.Primary, Color.white);
            skin.Label(At(root, "Body Tab Label", 522, 39, 277, 66), "Body", 32, Brown);
            faceTab = skin.Button(At(root, "Face", 802, 39, 273, 66), "Face", OpenFace, false, 32, skin.Paw);
            selectedDog = ModelViewport(root, "Selected Dog", 527, 91, 546, 546);
            previousDog = skin.Button(At(root, "Previous Dog", 286, 325, 78, 76), "‹", BrowseDog, -1, false, 52);
            nextDog = skin.Button(At(root, "Next Dog", 1236, 325, 78, 76), "›", BrowseDog, 1, false, 52);
            BuildWorldSlots(root);
            skin.Surface(At(root, "Dog Name Plaque", 554, 548, 492, 69), skin.Paper, Color.white);
            skin.Surface(At(root, "Name Paw Left", 579, 563, 40, 39), skin.Paw, Brown);
            skin.Surface(At(root, "Name Paw Right", 981, 563, 40, 39), skin.Paw, Brown);
            dogName = skin.Label(At(root, "Dog Name", 623, 548, 354, 69), "", 35, Brown);

            skin.Surface(At(root, "Model Carousel", 295, 630, 1010, 117), skin.Paper, Color.white);
            var count = Mathf.Min(6, session.ModelCatalog.Entries.Count);
            dogTiles = new UnityEngine.UI.Button[count];
            dogTileLabels = new TMP_Text[count];
            dogPortraits = new UnityEngine.UI.Image[count];
            for (var tile = 0; tile < count; tile++)
            {
                var slot = tile;
                dogTiles[tile] = skin.Button(At(root, $"Model Tile {tile + 1}", 322 + (956f / count) * tile, 645,
                    944f / count, 86), "", ChooseVisibleDog, slot, false, 23);
                dogTileLabels[tile] = dogTiles[tile].GetComponentInChildren<TMP_Text>(true);
                dogTileLabels[tile].rectTransform.anchorMax = new Vector2(.975f, .47f);
                dogPortraits[tile] = skin.Surface(At(dogTiles[tile].transform, "Dog Portrait", 525, 65, 550, 480), skin.Paw, Cocoa);
                dogPortraits[tile].preserveAspect = true;
            }

            skin.Surface(At(root, "Prepare Footer", 63, 767, 1474, 83), skin.Paper, Color.white);
            prepareBack = skin.Button(At(root, "Back", 76, 781, 142, 53), "‹ Back", BackFromPrepare, false, 26);
            nickname = skin.Input(At(root, "Nickname", 231, 780, 300, 55), "Nickname",
                "Guest", 16);
            cancelConnectionButton = skin.Button(At(root, "Cancel Connection", 547, 781, 168, 53), "Cancel", LeaveGame, false, 26);
            faceButton = skin.Button(At(root, "Customize Face", 913, 781, 309, 53), "Customize Face", OpenFace, false, 26, skin.Paw);
            continueButton = skin.Button(At(root, "Continue", 1235, 781, 289, 53), "Play", Continue, true, 27);
            continueLabel = continueButton.GetComponentInChildren<TMP_Text>();
            prepareStatusStrip = skin.Surface(At(root, "Status Strip", 392, 856, 816, 35), skin.Paper, Color.white).gameObject;
            prepareMessage = skin.Label(At(root, "Prepare Status", 406, 855, 788, 38), "", 21, Brown);
            prepareStatusStrip.SetActive(false);
        }

        private void BuildWorldSlots(RectTransform root)
        {
            worldSlots = At(root, "World Saves", 72, 168, 210, 265);
            skin.Surface(worldSlots, skin.Paper, Color.white);
            skin.Label(At(worldSlots, "World Saves Title", 0, 30, 1600, 154), "World Saves", 28, Brown);
            worldButtons = new UnityEngine.UI.Button[WorldSaveSlots.Count];
            worldLabels = new TMP_Text[worldButtons.Length];
            for (var i = 0; i < worldButtons.Length; i++)
            {
                var slot = i + 1;
                worldButtons[i] = skin.Button(At(worldSlots, $"World {slot}", 76, 209 + i * 212, 1448, 177), "", SelectWorldSlot, slot, false, 22);
                worldLabels[i] = worldButtons[i].GetComponentInChildren<TMP_Text>(true);
            }
        }

#endif

        private void RefreshWorldSlots(bool interactable)
        {
            worldSlots.gameObject.SetActive(launch != Launch.Join);
            if (!worldSlots.gameObject.activeInHierarchy) return;
            for (var i = 0; i < worldButtons.Length; i++)
            {
                var slot = i + 1;
                var button = worldButtons[i];
                var status = WorldSaveSlots.Load(slot) != null ? "Saved" : "New";
                worldLabels[i].text = $"World {slot}  •  {status}";
                button.image.sprite = slot == worldSlot ? skin.Primary : skin.Paper;
                button.interactable = interactable;
            }
        }

        private void BackFromPrepare()
        {
            if (session.IsConnected) LeaveGame();
            else Show(launch == Launch.SinglePlayer ? Page.Home : Page.Hub);
        }

        private int FirstVisibleDog() => selectedIndex / dogTiles.Length * dogTiles.Length;

        private void UpdateDogStrip()
        {
            dogName.text = Selected.DisplayName;
            var first = FirstVisibleDog();
            for (var i = 0; i < dogTiles.Length; i++)
            {
                var index = first + i;
                var tile = dogTiles[i];
                tile.gameObject.SetActive(index < session.ModelCatalog.Entries.Count);
                if (!tile.gameObject.activeSelf) continue;
                var entry = session.ModelCatalog.Entries[index];
                tile.image.sprite = index == selectedIndex ? skin.Primary : skin.Paper;
                dogTileLabels[i].text = entry.DisplayName;
                var icon = dogPortraits[i];
                var texture = Resources.Load<Sprite>($"DogMenu/Dog-{entry.ModelId}");
                icon.sprite = texture != null ? texture : skin.Paw;
                icon.color = texture != null ? Color.white : Cocoa;
            }
        }

#if UNITY_EDITOR
        private void BuildFace(RectTransform root)
        {
            skin.Surface(At(root, "Portrait Frame", 70, 97, 733, 682), skin.Paper, Color.white);
            skin.Surface(At(root, "Portrait Backdrop", 90, 115, 693, 646), skin.Paper, new Color(.63f, .81f, .92f));
            faceDog = ModelViewport(root, "Live Face Preview", 123, 132, 626, 626);
            faceCanvas = ModelViewport(root, "Drawing Surface", 131, 135, 610, 610);
            faceCanvas.raycastTarget = true;
            faceEditor = faceCanvas.gameObject.AddComponent<DogFaceEditorController>();
            skin.Button(At(root, "Face Back", 82, 803, 179, 60), "‹ Back", faceEditor.Close, false, 28);
            faceViewLabel = skin.Button(At(root, "Toggle Face Preview", 537, 803, 262, 60), "Edit Canvas",
                ToggleFaceView, false, 27).GetComponentInChildren<TMP_Text>();

            skin.Surface(At(root, "Face Tools Panel", 827, 145, 698, 716), skin.Paper, Color.white);
            var photo = At(root, "Photo Tools", 0, 0, 1600, 900);
            var draw = At(root, "Drawing Tools", 0, 0, 1600, 900);
            var adjust = At(root, "Adjustment Tools", 0, 0, 1600, 900);
            faceTabs = new[] { photo, draw, adjust };
            var names = new[] { "Photo", "Draw", "Adjust" };
            faceTabButtons = new UnityEngine.UI.Button[names.Length];

            skin.Surface(At(photo, "Image Drop Area", 860, 181, 632, 160), skin.Paper, Cream);
            skin.Button(At(photo, "Choose Image", 965, 196, 423, 72), "Choose Image", faceEditor.ChooseImage, true, 33, skin.Paw);
            imageHint = skin.Label(At(photo, "Image Hint", 885, 278, 580, 42), "PNG or JPEG • Preview your new face live", 25, Muted);
            skin.Label(At(photo, "Photo Tips", 869, 389, 604, 100), "Use Adjust to position your photo.", 28, Brown);
            skin.Label(At(photo, "Drawing Tips", 869, 473, 604, 72), "Use Draw to paint directly on the face.", 26, Brown);

            var size = LabeledSlider(adjust, "Size", 195, .1f, 8, 1, "P0");
            var x = LabeledSlider(adjust, "Position X", 297, -1, 1, 0, "+0.00;-0.00;0");
            var y = LabeledSlider(adjust, "Position Y", 399, -1, 1, 0, "+0.00;-0.00;0");
            var angle = LabeledSlider(adjust, "Rotation", 501, -180, 180, 0, "0°");
            skin.Button(At(adjust, "Reset Alignment", 864, 607, 283, 54), "Reset Alignment", faceEditor.ResetAlignment, false, 27);

            var brush = LabeledSlider(draw, "Brush Size", 213, .003f, .12f, .025f, "P1");
            BuildBrushColor(draw);
            brushButton = skin.Button(At(draw, "Brush", 1160, 476, 154, 52), "Brush", UseBrush, true, 29);
            eraseButton = skin.Button(At(draw, "Erase", 1330, 476, 154, 52), "Erase", UseEraser, false, 29);
            skin.Button(At(draw, "Undo", 1160, 542, 112, 52), "Undo", faceEditor.Undo, false, 29);
            skin.Button(At(draw, "Clear Drawing", 1286, 542, 198, 52), "Clear Drawing", faceEditor.ClearDrawing, false, 27);
            skin.Label(At(draw, "Draw Hint", 863, 613, 615, 43), "Drag on the canvas to draw.", 26, Muted);

            for (var i = 0; i < names.Length; i++)
            {
                var index = i;
                faceTabButtons[i] = skin.Button(At(root, names[i], 832 + i * 230, 76, 218, 70), names[i], SelectFaceTab, index, i == 0, 32);
            }

            faceHelp = skin.Label(At(root, "Face Status", 850, 687, 654, 58), "", 22, Brown);
            skin.Button(At(root, "Use Default Face", 863, 771, 318, 61), "Use Default", faceEditor.UseDefault, false, 28);
            skin.Button(At(root, "Apply Face", 1200, 771, 285, 61), "Apply", faceEditor.Apply, true, 30);
            faceEditor.ConfigureMenu(root.gameObject, faceCanvas, preview, faceHelp, size, x, y, angle, brush);
        }

        private void BuildBrushColor(Transform draw)
        {
            skin.Label(At(draw, "Brush Color Label", 1160, 314, 182, 45), "Brush Color", 27, Brown, TextAlignmentOptions.Left);
            var preview = At(draw, "Current Brush Color", 1357, 314, 127, 45);
            skin.Surface(preview, skin.Paper, Color.white);
            brushColorPreview = skin.Surface(At(preview, "Color", 87, 130, 1426, 640), null, Color.white);

            brushPicker = At(draw, "Brush Color Picker", 862, 313, 282, 282).gameObject.AddComponent<BrushColorPicker>();
            red = Channel("R", 370);
            green = Channel("G", 400);
            blue = Channel("B", 430);
            brushPicker.BakeImages();

            UnityEngine.UI.Slider Channel(string name, float y)
            {
                skin.Label(At(draw, name + " Label", 1160, y, 24, 22), name, 20, Brown);
                var slider = skin.Slider(At(draw, name + " Channel", 1192, y, 292, 22), 0, 255, 0);
                slider.wholeNumbers = true;
                return slider;
            }

        }

        private UnityEngine.UI.Slider LabeledSlider(Transform root, string name, float y, float min, float max, float value,
            string format)
        {
            skin.Label(At(root, name + " Label", 860, y, 380, 43), name, 27, Brown, TextAlignmentOptions.Left);
            var slider = skin.Slider(At(root, name, 866, y + 45, 613, 26), min, max, value);
            var label = skin.Label(At(root, name + " Value", 1240, y, 248, 43), value.ToString(format), 24, Brown, TextAlignmentOptions.Right);
            label.gameObject.AddComponent<DogMenuSliderValue>().Bind(slider, label, format);
            return slider;
        }

        private static UnityEngine.UI.RawImage ModelViewport(Transform root, string name, float x, float y, float width, float height)
        {
            var image = At(root, name, x, y, width, height).gameObject.AddComponent<UnityEngine.UI.RawImage>();
            image.raycastTarget = false;
            return image;
        }
#endif

        private void PrepareSinglePlayer() => Prepare(Launch.SinglePlayer);
        private void ShowHub() => Show(Page.Hub);
        private void ChooseVisibleDog(int slot) => ChooseDog(FirstVisibleDog() + slot);
        private void ToggleFaceView() => SetFaceView(faceCanvas.enabled);

        private void SelectWorldSlot(int slot)
        {
            worldSlot = slot;
            RefreshWorldSlots(true);
        }

        private void Exit()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            exitNote.SetActive(true);
            exitPaper.SetActive(true);
#elif UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void BindFaceTools()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            imageHint.text = "or drop a PNG / JPEG onto the game";
#endif
            faceEditor.Closed += () => Show(Page.Prepare);
            faceEditor.Applied += jpeg => session.LocalPlayer?.PublishCustomFace(jpeg);
            faceEditor.DefaultRequested += () => session.LocalPlayer?.PublishDefaultFace();
            faceEditor.Opened += () =>
            {
                SelectFaceTab(0);
                faceHelp.text = "Photo, freehand drawing, and alignment";
            };
        }

        private void BindBrushPicker()
        {
            if (brushPickerReady) return;
            brushPickerReady = true;
            brushPicker.Initialize(new Color32(75, 43, 29, 255), color =>
            {
                faceEditor.SetBrushColor(color);
                brushColorPreview.color = color;
                Color32 channels = color;
                red.SetValueWithoutNotify(channels.r);
                green.SetValueWithoutNotify(channels.g);
                blue.SetValueWithoutNotify(channels.b);
            });
            red.onValueChanged.AddListener(UpdateBrushChannels);
            green.onValueChanged.AddListener(UpdateBrushChannels);
            blue.onValueChanged.AddListener(UpdateBrushChannels);
        }

        private void UpdateBrushChannels(float _)
            => brushPicker.SetColor(new Color32((byte)red.value, (byte)green.value, (byte)blue.value, 255));

        private void SelectFaceTab(int index)
        {
            for (var i = 0; i < faceTabs.Length; i++)
            {
                faceTabs[i].gameObject.SetActive(i == index);
                faceTabButtons[i].image.sprite = i == index ? skin.Primary : skin.Paper;
            }
            if (index == 1)
            {
                BindBrushPicker();
                UseBrush();
            }
            else
            {
                faceEditor.SetAlignMode();
                SetFaceView(index == 0);
            }
        }

        private void UseBrush()
        {
            faceEditor.SetDrawMode();
            HighlightDrawingTool(false);
        }

        private void UseEraser()
        {
            faceEditor.SetEraseMode();
            HighlightDrawingTool(true);
        }

        private void HighlightDrawingTool(bool erase)
        {
            brushButton.image.sprite = erase ? skin.Paper : skin.Primary;
            eraseButton.image.sprite = erase ? skin.Primary : skin.Paper;
            SetFaceView(false);
        }
    }
}
