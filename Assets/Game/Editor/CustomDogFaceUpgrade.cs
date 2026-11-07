using System;
using System.Linq;
using DogSimulator.CustomDogFace;
using DogSimulator.Networking;
using DogSimulator.Player;
using DogSimulator.UI;
using Fusion.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DogSimulator.Editor
{
    public static class CustomDogFaceUpgrade
    {
        [MenuItem("Tools/Dog Simulator/Custom Face/Apply Custom Face with BlankFaceDog")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before applying the custom-face upgrade.");
            BlankFaceDogSetup.Apply();
            UpgradePlayerPrefab();
            UpgradeNetworkVersion();
            UpgradeScene();
            AssetDatabase.SaveAssets();
            NetworkProjectConfigUtilities.RebuildPrefabTable();
            AssetDatabase.Refresh();
            Debug.Log("Custom-face systems upgraded to use the BlankFaceDog art model.");
        }

        private static void UpgradePlayerPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(FusionPreviewSetup.PrefabPath);
            try
            {
                var presenter = root.GetComponent<DogVisualPresenter>();
                var appearance = root.GetComponent<DogFaceAppearance>() ?? root.AddComponent<DogFaceAppearance>();
                Reference(appearance, "presenter", presenter);
                PrefabUtility.SaveAsPrefabAsset(root, FusionPreviewSetup.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void UpgradeNetworkVersion()
        {
            var config = AssetDatabase.LoadAssetAtPath<NetworkConfig>(FusionPreviewSetup.ConfigPath);
            var serialized = new SerializedObject(config);
            serialized.FindProperty("gameVersion").stringValue = FusionSessionOptions.WorldBuild;
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(config);
        }

        private static void UpgradeScene()
        {
            var scene = EditorSceneManager.OpenScene(FusionPreviewSetup.ScenePath);
            var ui = Find<FusionPreviewUI>(scene);
            var preview = Find<DogSelectionPreview>(scene);
            var canvas = ui.GetComponentInParent<Canvas>().transform as RectTransform;
            var bridge = Object.FindFirstObjectByType<DogFaceWebBridge>();
            if (bridge == null) bridge = new GameObject("Custom Dog Face Web Bridge", typeof(DogFaceWebBridge)).GetComponent<DogFaceWebBridge>();
            var modal = canvas.Find("Custom Face Modal") as RectTransform;
            if (modal != null) Object.DestroyImmediate(modal.gameObject);
            modal = Rect("Custom Face Modal", canvas, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            modal.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .92f);
            var editor = modal.gameObject.AddComponent<DogFaceEditorController>();
            var panel = Rect("Panel", modal, new Vector2(.08f, .08f), new Vector2(.92f, .92f), Vector2.zero, Vector2.zero);
            panel.gameObject.AddComponent<Image>().color = new Color(.035f, .05f, .075f, 1);
            Text(panel, "Title", "CUSTOM DOG FACE", 28, new Vector2(.03f, .92f), new Vector2(.97f, .99f));
            var image = Rect("Editor Canvas", panel, new Vector2(.04f, .2f), new Vector2(.58f, .88f), Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>();
            image.color = Color.white;
            var status = Text(panel, "Status", "Choose PNG/JPEG, align it, or draw. The final JPEG is shared with room members.", 15,
                new Vector2(.04f, .04f), new Vector2(.96f, .11f));
            var zoom = Slider(panel, "Zoom", new Vector2(.63f, .74f), new Vector2(.94f, .79f), .1f, 8f, 1f);
            var rotation = Slider(panel, "Rotation", new Vector2(.63f, .65f), new Vector2(.94f, .7f), -180, 180, 0);
            var brush = Slider(panel, "Brush Size", new Vector2(.63f, .56f), new Vector2(.94f, .61f), .003f, .2f, .025f);
            var color = TMP_DefaultControls.CreateInputField(default).GetComponent<TMP_InputField>();
            color.name = "Brush Color"; color.transform.SetParent(panel, false); color.text = "#ffffff";
            SetRect((RectTransform)color.transform, new Vector2(.63f, .49f), new Vector2(.94f, .54f));
            var choose = Button(panel, "Choose Image", "CHỌN ẢNH", new Vector2(.63f, .84f), new Vector2(.78f, .9f));
            var align = Button(panel, "Align", "CĂN ẢNH", new Vector2(.8f, .84f), new Vector2(.94f, .9f));
            var draw = Button(panel, "Draw", "VẼ", new Vector2(.63f, .4f), new Vector2(.72f, .46f));
            var erase = Button(panel, "Erase", "TẨY", new Vector2(.73f, .4f), new Vector2(.82f, .46f));
            var undo = Button(panel, "Undo", "HOÀN TÁC", new Vector2(.83f, .4f), new Vector2(.94f, .46f));
            var clear = Button(panel, "Clear", "XÓA NÉT", new Vector2(.63f, .32f), new Vector2(.78f, .38f));
            var reset = Button(panel, "Reset Align", "ĐẶT LẠI", new Vector2(.8f, .32f), new Vector2(.94f, .38f));
            var defaults = Button(panel, "Use Default", "DÙNG MẶT MẶC ĐỊNH", new Vector2(.63f, .22f), new Vector2(.94f, .29f));
            var cancel = Button(panel, "Cancel", "HỦY", new Vector2(.63f, .12f), new Vector2(.76f, .19f));
            var apply = Button(panel, "Apply", "LƯU VÀ CHIA SẺ KHI VÀO PHÒNG", new Vector2(.78f, .12f), new Vector2(.94f, .19f));
            UnityEventTools.AddPersistentListener(choose.onClick, editor.ChooseImage);
            UnityEventTools.AddPersistentListener(align.onClick, editor.SetAlignMode);
            UnityEventTools.AddPersistentListener(draw.onClick, editor.SetDrawMode);
            UnityEventTools.AddPersistentListener(erase.onClick, editor.SetEraseMode);
            UnityEventTools.AddPersistentListener(undo.onClick, editor.Undo);
            UnityEventTools.AddPersistentListener(clear.onClick, editor.ClearDrawing);
            UnityEventTools.AddPersistentListener(reset.onClick, editor.ResetAlignment);
            UnityEventTools.AddPersistentListener(defaults.onClick, editor.UseDefault);
            UnityEventTools.AddPersistentListener(cancel.onClick, editor.Close);
            UnityEventTools.AddPersistentListener(apply.onClick, editor.Apply);
            Reference(editor, "modal", modal.gameObject); Reference(editor, "canvas", image);
            Reference(editor, "zoomSlider", zoom); Reference(editor, "rotationSlider", rotation);
            Reference(editor, "brushSizeSlider", brush); Reference(editor, "colorHex", color);
            Reference(editor, "status", status); Reference(editor, "applyLabel", apply.GetComponentInChildren<TMP_Text>());
            Reference(editor, "preview", preview);
            var settings = ui.GetComponentsInChildren<Transform>(true).First(item => item.name == "Settings") as RectTransform;
            var editButton = settings.GetComponentsInChildren<Button>(true).FirstOrDefault(item => item.name == "Edit Custom Face");
            if (editButton == null) editButton = Button(settings, "Edit Custom Face", "CHỈNH MẶT", new Vector2(.57f, .08f), new Vector2(.96f, .32f));
            Reference(ui, "editFaceButton", editButton); Reference(ui, "faceEditor", editor);
            modal.gameObject.SetActive(false);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = offsetMin; rect.offsetMax = offsetMax; return rect;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }

        private static TMP_Text Text(Transform parent, string name, string value, float size, Vector2 min, Vector2 max)
        {
            var text = Rect(name, parent, min, max, Vector2.zero, Vector2.zero).gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value; text.fontSize = size; text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.Normal; return text;
        }

        private static Button Button(Transform parent, string name, string label, Vector2 min, Vector2 max)
        {
            var button = Rect(name, parent, min, max, Vector2.zero, Vector2.zero).gameObject.AddComponent<Button>();
            var image = button.gameObject.AddComponent<Image>(); image.color = new Color(.12f, .31f, .52f, 1); button.targetGraphic = image;
            var text = Text(button.transform, "Label", label, 14, Vector2.zero, Vector2.one); text.alignment = TextAlignmentOptions.Center; return button;
        }

        private static Slider Slider(Transform parent, string name, Vector2 min, Vector2 max, float low, float high, float value)
        {
            var root = Rect(name, parent, min, max, Vector2.zero, Vector2.zero); var slider = root.gameObject.AddComponent<Slider>();
            var background = Rect("Background", root, new Vector2(0, .35f), new Vector2(1, .65f), Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            background.color = new Color(.15f, .18f, .24f); var fill = Rect("Fill", root, new Vector2(0, .35f), new Vector2(1, .65f), Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            fill.color = new Color(.2f, .65f, .95f); var handle = Rect("Handle", root, new Vector2(0, .15f), new Vector2(.04f, .85f), Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            handle.color = Color.white; slider.fillRect = fill.rectTransform; slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            slider.minValue = low; slider.maxValue = high; slider.value = value; return slider;
        }

        private static T Find<T>(Scene scene) where T : Component
            => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).First();

        private static void Reference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target); var property = serialized.FindProperty(field)
                ?? throw new InvalidOperationException($"Missing field {field} on {target.name}");
            property.objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);
        }
    }
}
