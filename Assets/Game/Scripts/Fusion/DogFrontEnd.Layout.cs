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
        private TMP_Text dogName;
        private TMP_Text continueLabel;
        private TMP_Text faceViewLabel;
        private TMP_Text faceHelp;
        private GameObject prepareStatusStrip;
        private UnityEngine.UI.Button prepareBack;
        private UnityEngine.UI.Button faceButton;
        private UnityEngine.UI.Button faceTab;
        private UnityEngine.UI.Button previousDog;
        private UnityEngine.UI.Button nextDog;
        private UnityEngine.UI.Button[] dogTiles;
        private RectTransform worldSlots;
        private UnityEngine.UI.Button[] worldButtons;

        private void BuildLayout()
        {
            var root = At(transform, "Dog Simulator Menu", 0, 0, 1600, 900);
            var background = At(root, "Illustrated Yard", 0, 0, 1600, 900);
            var backgroundImage = background.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            backgroundImage.texture = Resources.Load<Texture2D>("DogMenu/Yard");
            backgroundImage.raycastTarget = false;
            yard = background.gameObject;

            stage = At(root, "Landscape Safe Area", 0, 0, 1600, 900);
            var fitter = stage.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            fitter.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 16f / 9;
            BuildHome(AddPage(Page.Home, "HomeScene"));
            BuildPrepare(AddPage(Page.Prepare, "PrepareScene"));
            BuildHub(AddPage(Page.Hub, "MultiplayerHub"));
            BuildFace(AddPage(Page.Face, "FaceCustomize"));
            BuildGameplayHud(AddPage(Page.Gameplay, "Gameplay HUD", root));
            BuildSettings();
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
            homeDog = ModelViewport(root, "Floating Mascot", 684, 69, 790, 790);
            skin.Surface(At(root, "Decorative Bone", 1281, 639, 120, 90), skin.Bone, Cream)
                .rectTransform.localRotation = Quaternion.Euler(0, 0, 24);
            skin.Surface(At(root, "Decorative Paw", 795, 179, 87, 87), skin.Paw, Cream)
                .rectTransform.localRotation = Quaternion.Euler(0, 0, -20);
            skin.Button(At(root, "Singleplayer", 114, 365, 530, 92), "Singleplayer", () => Prepare(Launch.SinglePlayer), true, 42, skin.Paw);
            skin.Button(At(root, "Multiplayer", 114, 472, 530, 85), "Multiplayer", () => Show(Page.Hub), false, 41, skin.Bone);
            skin.Button(At(root, "Options", 114, 571, 500, 81), "Options", OpenSettings, false, 39, skin.Paw);
            skin.Button(At(root, "Exit", 114, 666, 475, 81), "Exit", Exit, false, 38, skin.Bone);
            var webExit = skin.Label(At(root, "Exit Note", 110, 755, 465, 48), "Close this browser tab to exit.", 23, Brown);
            skin.Surface(At(root, "Exit Note Paper", 114, 755, 457, 48), skin.Paper, Color.white)
                .transform.SetSiblingIndex(webExit.transform.GetSiblingIndex());
            webExit.transform.SetAsLastSibling();
            webExit.gameObject.SetActive(false);
            var exitPaper = root.Find("Exit Note Paper").gameObject;
            exitPaper.SetActive(false);

            void Exit()
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                webExit.gameObject.SetActive(true);
                exitPaper.SetActive(true);
#elif UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }

        private void BuildPrepare(RectTransform root)
        {
            skin.Surface(At(root, "Body and Face Tabs", 515, 33, 570, 80), skin.Paper, Color.white);
            skin.Surface(At(root, "Body Tab", 522, 39, 277, 66), skin.Primary, Color.white);
            skin.Label(At(root, "Body Tab Label", 522, 39, 277, 66), "Body", 32, Brown);
            faceTab = skin.Button(At(root, "Face", 802, 39, 273, 66), "Face", OpenFace, false, 32, skin.Paw);
            selectedDog = ModelViewport(root, "Selected Dog", 527, 91, 546, 546);
            previousDog = skin.Button(At(root, "Previous Dog", 286, 325, 78, 76), "‹", () => BrowseDog(-1), false, 52);
            nextDog = skin.Button(At(root, "Next Dog", 1236, 325, 78, 76), "›", () => BrowseDog(1), false, 52);
            BuildWorldSlots(root);
            skin.Surface(At(root, "Dog Name Plaque", 554, 548, 492, 69), skin.Paper, Color.white);
            skin.Surface(At(root, "Name Paw Left", 579, 563, 40, 39), skin.Paw, Brown);
            skin.Surface(At(root, "Name Paw Right", 981, 563, 40, 39), skin.Paw, Brown);
            dogName = skin.Label(At(root, "Dog Name", 623, 548, 354, 69), "", 35, Brown);

            skin.Surface(At(root, "Model Carousel", 295, 630, 1010, 117), skin.Paper, Color.white);
            var count = Mathf.Min(6, session.ModelCatalog.Entries.Count);
            dogTiles = new UnityEngine.UI.Button[count];
            for (var tile = 0; tile < count; tile++)
            {
                var slot = tile;
                dogTiles[tile] = skin.Button(At(root, $"Model Tile {tile + 1}", 322 + (956f / count) * tile, 645,
                    944f / count, 86), "", () => ChooseDog(FirstVisibleDog() + slot), false, 23);
            }

            skin.Surface(At(root, "Prepare Footer", 63, 767, 1474, 83), skin.Paper, Color.white);
            prepareBack = skin.Button(At(root, "Back", 76, 781, 142, 53), "‹ Back", BackFromPrepare, false, 26);
            nickname = skin.Input(At(root, "Nickname", 231, 780, 300, 55), "Nickname",
                PlayerPrefs.GetString(NicknameKey, "Guest"), 16);
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
            for (var i = 0; i < worldButtons.Length; i++)
            {
                var slot = i + 1;
                worldButtons[i] = skin.Button(At(worldSlots, $"World {slot}", 76, 209 + i * 212, 1448, 177), "", () =>
                {
                    worldSlot = slot;
                    RefreshWorldSlots(true);
                }, false, 22);
            }
        }

        private void RefreshWorldSlots(bool interactable)
        {
            worldSlots.gameObject.SetActive(launch != Launch.Join);
            if (!worldSlots.gameObject.activeInHierarchy) return;
            for (var i = 0; i < worldButtons.Length; i++)
            {
                var slot = i + 1;
                var button = worldButtons[i];
                var status = WorldSaveSlots.Load(slot) != null ? "Saved" : "New";
                button.GetComponentInChildren<TMP_Text>().text = $"World {slot}  •  {status}";
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
                var label = tile.GetComponentInChildren<TMP_Text>();
                label.text = entry.DisplayName;
                var portrait = tile.transform.Find("Dog Portrait") as RectTransform;
                if (portrait == null)
                {
                    portrait = At(tile.transform, "Dog Portrait", 525, 65, 550, 480);
                    skin.Surface(portrait, skin.Paw, Cocoa).preserveAspect = true;
                    label.rectTransform.anchorMax = new Vector2(.975f, .47f);
                }
                var icon = portrait.GetComponent<UnityEngine.UI.Image>();
                var texture = Resources.Load<Sprite>($"DogMenu/Dog-{entry.ModelId}");
                icon.sprite = texture != null ? texture : skin.Paw;
                icon.color = texture != null ? Color.white : Cocoa;
            }
        }

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
                () => SetFaceView(faceCanvas.enabled), false, 27).GetComponentInChildren<TMP_Text>();

            skin.Surface(At(root, "Face Tools Panel", 827, 145, 698, 716), skin.Paper, Color.white);
            var photo = At(root, "Photo Tools", 0, 0, 1600, 900);
            var draw = At(root, "Drawing Tools", 0, 0, 1600, 900);
            var adjust = At(root, "Adjustment Tools", 0, 0, 1600, 900);
            var tabs = new[] { photo, draw, adjust };
            var names = new[] { "Photo", "Draw", "Adjust" };
            var buttons = new UnityEngine.UI.Button[names.Length];

            skin.Surface(At(photo, "Image Drop Area", 860, 181, 632, 160), skin.Paper, Cream);
            skin.Button(At(photo, "Choose Image", 965, 196, 423, 72), "Choose Image", faceEditor.ChooseImage, true, 33, skin.Paw);
#if UNITY_WEBGL && !UNITY_EDITOR
            const string imageHint = "or drop a PNG / JPEG onto the game";
#else
            const string imageHint = "PNG or JPEG • Preview your new face live";
#endif
            skin.Label(At(photo, "Image Hint", 885, 278, 580, 42), imageHint, 25, Muted);
            skin.Label(At(photo, "Photo Tips", 869, 389, 604, 100), "Use Adjust to position your photo.", 28, Brown);
            skin.Label(At(photo, "Drawing Tips", 869, 473, 604, 72), "Use Draw to paint directly on the face.", 26, Brown);

            var size = LabeledSlider(adjust, "Size", 195, .1f, 8, 1, value => $"{value:P0}");
            var x = LabeledSlider(adjust, "Position X", 297, -1, 1, 0, value => $"{value:+0.00;-0.00;0}");
            var y = LabeledSlider(adjust, "Position Y", 399, -1, 1, 0, value => $"{value:+0.00;-0.00;0}");
            var angle = LabeledSlider(adjust, "Rotation", 501, -180, 180, 0, value => $"{value:0}°");
            skin.Button(At(adjust, "Reset Alignment", 864, 607, 283, 54), "Reset Alignment", faceEditor.ResetAlignment, false, 27);

            var brush = LabeledSlider(draw, "Brush Size", 213, .003f, .12f, .025f, value => $"{value:P1}");
            skin.Label(At(draw, "Brush Color Label", 866, 322, 260, 49), "Brush Color", 27, Brown, TextAlignmentOptions.Left);
            var color = skin.Input(At(draw, "Brush Color", 1183, 322, 300, 49), "#RRGGBB", "#4b2b1d", 7);
            var brushButton = skin.Button(At(draw, "Brush", 862, 408, 195, 64), "Brush", faceEditor.SetDrawMode, true, 29);
            var eraseButton = skin.Button(At(draw, "Erase", 1075, 408, 195, 64), "Erase", faceEditor.SetEraseMode, false, 29);
            brushButton.onClick.AddListener(() => HighlightDrawingTool(false));
            eraseButton.onClick.AddListener(() => HighlightDrawingTool(true));
            skin.Button(At(draw, "Undo", 1288, 408, 195, 64), "Undo", faceEditor.Undo, false, 29);
            skin.Button(At(draw, "Clear Drawing", 862, 506, 280, 62), "Clear Drawing", faceEditor.ClearDrawing, false, 27);
            skin.Label(At(draw, "Draw Hint", 863, 601, 615, 54), "Drag on the canvas to draw.", 26, Muted);

            for (var i = 0; i < names.Length; i++)
            {
                var index = i;
                buttons[i] = skin.Button(At(root, names[i], 832 + i * 230, 76, 218, 70), names[i], () => SelectFaceTab(index), i == 0, 32);
            }

            faceHelp = skin.Label(At(root, "Face Status", 850, 687, 654, 58), "", 22, Brown);
            skin.Button(At(root, "Use Default Face", 863, 771, 318, 61), "Use Default", faceEditor.UseDefault, false, 28);
            skin.Button(At(root, "Apply Face", 1200, 771, 285, 61), "Apply", faceEditor.Apply, true, 30);
            faceEditor.ConfigureMenu(root.gameObject, faceCanvas, preview, faceHelp, size, x, y, angle, brush, color);
            faceEditor.Closed += () => Show(Page.Prepare);
            faceEditor.Applied += jpeg => session.LocalPlayer?.PublishCustomFace(jpeg);
            faceEditor.DefaultRequested += () => session.LocalPlayer?.PublishDefaultFace();
            faceEditor.Opened += () =>
            {
                SelectFaceTab(0);
                faceHelp.text = "Photo, freehand drawing, and alignment";
            };

            void SelectFaceTab(int index)
            {
                for (var i = 0; i < tabs.Length; i++)
                {
                    tabs[i].gameObject.SetActive(i == index);
                    buttons[i].image.sprite = i == index ? skin.Primary : skin.Paper;
                }
                if (index == 1)
                {
                    faceEditor.SetDrawMode();
                    HighlightDrawingTool(false);
                }
                else
                {
                    faceEditor.SetAlignMode();
                    SetFaceView(index == 0);
                }
            }

            void HighlightDrawingTool(bool erase)
            {
                brushButton.image.sprite = erase ? skin.Paper : skin.Primary;
                eraseButton.image.sprite = erase ? skin.Primary : skin.Paper;
                SetFaceView(false);
            }
        }

        private UnityEngine.UI.Slider LabeledSlider(Transform root, string name, float y, float min, float max, float value,
            Func<float, string> format)
        {
            skin.Label(At(root, name + " Label", 860, y, 380, 43), name, 27, Brown, TextAlignmentOptions.Left);
            var slider = skin.Slider(At(root, name, 866, y + 45, 613, 26), min, max, value);
            var label = skin.Label(At(root, name + " Value", 1240, y, 248, 43), format(value), 24, Brown, TextAlignmentOptions.Right);
            label.gameObject.AddComponent<DogMenuSliderValue>().Bind(slider, label, format);
            return slider;
        }

        private static UnityEngine.UI.RawImage ModelViewport(Transform root, string name, float x, float y, float width, float height)
        {
            var image = At(root, name, x, y, width, height).gameObject.AddComponent<UnityEngine.UI.RawImage>();
            image.raycastTarget = false;
            return image;
        }
    }

    internal sealed class DogMenuSliderValue : MonoBehaviour
    {
        private UnityEngine.UI.Slider slider;
        private TMP_Text label;
        private Func<float, string> format;
        private float last;

        public void Bind(UnityEngine.UI.Slider source, TMP_Text target, Func<float, string> formatValue)
        {
            slider = source;
            label = target;
            format = formatValue;
            last = source.value;
        }

        private void LateUpdate()
        {
            if (slider == null || Mathf.Approximately(last, slider.value)) return;
            last = slider.value;
            label.text = format(last);
        }
    }
}
