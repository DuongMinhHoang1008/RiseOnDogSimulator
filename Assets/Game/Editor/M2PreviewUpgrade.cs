using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DogSimulator.CustomDogFace;
using DogSimulator.Networking;
using DogSimulator.Player;
using DogSimulator.UI;
using Fusion;
using Fusion.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DogSimulator.Editor
{
    public static class M2PreviewUpgrade
    {
        public const string CatalogPath = "Assets/Game/Config/DogModelCatalog.asset";
        public const string RegistryPrefabPath = "Assets/Game/Prefabs/DogModelSelectionRegistry.prefab";
        public const string RequestPrefabPath = "Assets/Game/Prefabs/DogModelSelectionRequest.prefab";
        private const string BaseControllerPath = "Assets/Game/Animation/DogGameplay.controller";
        private const string PhotoDogUnityFolder = "Assets/Art/Models/Dogs/PhotoDogCustomFace_20261108/Unity";
        private const string PhotoDogPrefabPath = PhotoDogUnityFolder + "/PhotoDogCustomFace.prefab";
        private const string PhotoDogControllerPath = PhotoDogUnityFolder + "/PhotoDogCustomFace.overrideController";
        private const string ShibaModelPath = "Assets/Art/Models/Dogs/ShibaFace_20261105/model/Dog_Rigged.fbx";

        [MenuItem("Tools/Dog Simulator/Upgrade Preview to M1.1 + M2")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before upgrading the preview.");
            if (!File.Exists(FusionPreviewSetup.ScenePath) || !File.Exists(FusionPreviewSetup.PrefabPath))
                throw new InvalidOperationException("Create the M1 Fusion preview first.");

            var shibaController = ConfigureModel(ShibaModelPath, "ShibaFace");
            var catalog = CreateOrUpdateCatalog(shibaController);
            var registryPrefab = CreateOrUpdateRegistry(catalog);
            var requestPrefab = CreateOrUpdateRequest();
            UpgradePlayerPrefab(catalog);
            UpgradeNetworkConfig();
            UpgradeScene(catalog, registryPrefab, requestPrefab);
            AssetDatabase.SaveAssets();
            NetworkProjectConfigUtilities.RebuildPrefabTable();
            Debug.Log("M1.1 FPS settings and M2 model catalog/reservation flow upgraded successfully.");
        }

        private static RuntimeAnimatorController ConfigureModel(string path, string assetName)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                throw new InvalidOperationException($"Missing model importer: {path}");
            var clips = importer.defaultClipAnimations;
            if (clips.Length > 0)
            {
                foreach (var clip in clips)
                {
                    clip.loopTime = ClipMatches(clip.name, "Idle") || ClipMatches(clip.name, "Run");
                    clip.loopPose = clip.loopTime;
                }
                importer.clipAnimations = clips;
                importer.SaveAndReimport();
            }
            var modelClips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
            var required = new[] { "Idle", "Run", "Jump", "Bark", "Poop", "Pee", "Bite" };
            foreach (var name in required)
                if (!modelClips.Any(clip => ClipMatches(clip.name, name)))
                    throw new InvalidOperationException($"{assetName} is missing animation clip '{name}'.");

            const string folder = "Assets/Game/Animation/Catalog";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Game/Animation", "Catalog");
            var overridePath = $"{folder}/{assetName}.overrideController";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overridePath);
            var baseController = AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseControllerPath);
            if (controller == null)
            {
                controller = new AnimatorOverrideController(baseController) { name = assetName };
                AssetDatabase.CreateAsset(controller, overridePath);
            }
            else controller.runtimeAnimatorController = baseController;
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(overrides);
            for (var i = 0; i < overrides.Count; i++)
            {
                var replacement = modelClips.FirstOrDefault(clip => ClipMatches(clip.name, overrides[i].Key.name));
                if (replacement != null) overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, replacement);
            }
            controller.ApplyOverrides(overrides);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static bool ClipMatches(string actual, string expected)
        {
            actual = actual.Replace("_Gameplay", string.Empty);
            expected = expected.Replace("_Gameplay", string.Empty);
            return actual.Equals(expected, StringComparison.OrdinalIgnoreCase)
                || actual.EndsWith("|" + expected, StringComparison.OrdinalIgnoreCase)
                || actual.EndsWith("_" + expected, StringComparison.OrdinalIgnoreCase);
        }

        private static DogModelCatalog CreateOrUpdateCatalog(RuntimeAnimatorController shiba)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DogModelCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<DogModelCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var serialized = new SerializedObject(catalog);
            serialized.FindProperty("fallbackModelId").intValue = 1;
            var entries = serialized.FindProperty("entries");
            SetEntry(entries, 1, "Reference Dog (fallback)",
                AssetDatabase.LoadAssetAtPath<GameObject>(DogGameplaySetup.VisualPrefabPath),
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(DogGameplaySetup.VisualControllerPath),
                Vector3.zero, Vector3.one, CustomDogFaceConstants.LayoutVersion);
            SetEntry(entries, 1001, "Photo Dog",
                AssetDatabase.LoadAssetAtPath<GameObject>(PhotoDogPrefabPath),
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PhotoDogControllerPath),
                Vector3.zero, Vector3.one, CustomDogFaceConstants.LayoutVersion);
            SetEntry(entries, 1002, "Shiba Face",
                AssetDatabase.LoadAssetAtPath<GameObject>(ShibaModelPath), shiba, Vector3.zero, Vector3.one);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (!catalog.Validate(out var error)) throw new InvalidOperationException(error);
            return catalog;
        }

        private static void SetEntry(SerializedProperty entries, int id, string name, GameObject prefab,
            RuntimeAnimatorController controller, Vector3 position, Vector3 scale, int faceLayoutVersion = 0)
        {
            if (prefab == null || controller == null) throw new InvalidOperationException($"Catalog asset missing for {name}.");
            var index = 0;
            while (index < entries.arraySize
                && entries.GetArrayElementAtIndex(index).FindPropertyRelative("modelId").intValue != id)
                index++;
            if (index == entries.arraySize) entries.InsertArrayElementAtIndex(index);
            var entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("modelId").intValue = id;
            entry.FindPropertyRelative("displayName").stringValue = name;
            entry.FindPropertyRelative("visualPrefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("animationController").objectReferenceValue = controller;
            entry.FindPropertyRelative("localPosition").vector3Value = position;
            entry.FindPropertyRelative("localEulerAngles").vector3Value = Vector3.zero;
            entry.FindPropertyRelative("localScale").vector3Value = scale;
            entry.FindPropertyRelative("mouthAnchor").vector3Value = new Vector3(0, .8f, .6f);
            entry.FindPropertyRelative("supportsCustomFace").boolValue = faceLayoutVersion > 0;
            entry.FindPropertyRelative("faceLayoutVersion").intValue = faceLayoutVersion;
        }

        private static GameObject CreateOrUpdateRegistry(DogModelCatalog catalog)
        {
            var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RegistryPrefabPath);
            if (existingPrefab != null)
            {
                var contents = PrefabUtility.LoadPrefabContents(RegistryPrefabPath);
                try
                {
                    var existingNetworkObject = contents.GetComponent<NetworkObject>();
                    existingNetworkObject.Flags |= NetworkObjectFlags.MasterClientObject;
                    existingNetworkObject.Flags &= ~(NetworkObjectFlags.DestroyWhenStateAuthorityLeaves | NetworkObjectFlags.AllowStateAuthorityOverride);
                    Reference(contents.GetComponent<DogModelSelectionRegistry>(), "catalog", catalog);
                    PrefabUtility.SaveAsPrefabAsset(contents, RegistryPrefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                return existingPrefab;
            }
            var root = new GameObject("Dog Model Selection Registry");
            var networkObject = root.AddComponent<NetworkObject>();
            networkObject.Flags |= NetworkObjectFlags.MasterClientObject;
            networkObject.Flags &= ~(NetworkObjectFlags.DestroyWhenStateAuthorityLeaves | NetworkObjectFlags.AllowStateAuthorityOverride);
            var registry = root.AddComponent<DogModelSelectionRegistry>();
            Reference(registry, "catalog", catalog);
            PrefabUtility.SaveAsPrefabAsset(root, RegistryPrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.ImportAsset(RegistryPrefabPath, ImportAssetOptions.ForceUpdate);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RegistryPrefabPath);
            return prefab;
        }

        private static GameObject CreateOrUpdateRequest()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(RequestPrefabPath);
            if (existing != null) return existing;
            var root = new GameObject("Dog Model Selection Request");
            var networkObject = root.AddComponent<NetworkObject>();
            networkObject.Flags |= NetworkObjectFlags.DestroyWhenStateAuthorityLeaves;
            networkObject.Flags &= ~(NetworkObjectFlags.MasterClientObject | NetworkObjectFlags.AllowStateAuthorityOverride);
            root.AddComponent<DogModelSelectionRequest>();
            PrefabUtility.SaveAsPrefabAsset(root, RequestPrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.ImportAsset(RequestPrefabPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<GameObject>(RequestPrefabPath);
        }

        private static void UpgradePlayerPrefab(DogModelCatalog catalog)
        {
            var root = PrefabUtility.LoadPrefabContents(FusionPreviewSetup.PrefabPath);
            try
            {
                var player = root.GetComponent<NetworkDogPlayer>();
                var presenter = root.GetComponent<DogVisualPresenter>();
                var visualRoot = root.transform.Find("DogVisual");
                var mouth = root.transform.Find("MouthAnchor");
                if (player == null || presenter == null || visualRoot == null || mouth == null)
                    throw new InvalidOperationException("DogPlayer prefab hierarchy is incomplete.");
                Reference(player, "modelCatalog", catalog);
                Reference(presenter, "visualRoot", visualRoot);
                Reference(presenter, "defaultVisual", visualRoot.childCount > 0 ? visualRoot.GetChild(0).gameObject : visualRoot.gameObject);
                Reference(presenter, "mouthAnchor", mouth);
                PrefabUtility.SaveAsPrefabAsset(root, FusionPreviewSetup.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void UpgradeNetworkConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<NetworkConfig>(FusionPreviewSetup.ConfigPath);
            var serialized = new SerializedObject(config);
            serialized.FindProperty("gameVersion").stringValue = "office-m2-catalog-reservations-v2";
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void UpgradeScene(DogModelCatalog catalog, GameObject registryPrefab, GameObject requestPrefab)
        {
            var scene = EditorSceneManager.OpenScene(FusionPreviewSetup.ScenePath);
            var session = Find<FusionSessionService>(scene, "Fusion Session");
            Reference(session, "registryPrefab", registryPrefab);
            Reference(session, "selectionRequestPrefab", requestPrefab);
            Reference(session, "modelCatalog", catalog);
            var ui = Find<FusionPreviewUI>(scene, "Preview UI");
            var menu = Find<RectTransform>(scene, "Menu");
            var font = Find<TMP_Text>(scene, "Stage").font;
            var settings = FindChild(menu, "Settings") ?? CreateSettings(menu, font);
            var dropdown = settings.GetComponentInChildren<TMP_Dropdown>(true);
            Reference(ui, "modelDropdown", dropdown);
            var preview = EnsureSelectionPreview(scene, menu, font, catalog);
            Reference(ui, "selectionPreview", preview);
            EnsureFpsHud(scene, menu, settings, font);
            ReflowMenu(menu, settings, preview.GetComponent<RectTransform>());
            Find<TMP_Text>(scene, "Stage").text = "M2 NETWORK PREVIEW / UNIQUE DOG RESERVATIONS";
            Find<TMP_Text>(scene, "Status").text = "Photon Fusion 2.1.4 / Shared Mode / private room codes";
            Find<TMP_Text>(scene, "Message").text = "Choose a dog before joining. The Master reserves each ModelId uniquely.";
            Find<TMP_Text>(scene, "NetworkSettings").text = $"Region: {session.Region} | Capacity: {session.Capacity} | {session.GameVersion}";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ReflowMenu(RectTransform menu, RectTransform settings, RectTransform preview)
        {
            menu.anchorMin = new Vector2(.055f, .05f);
            menu.anchorMax = new Vector2(.945f, .95f);
            menu.offsetMin = menu.offsetMax = Vector2.zero;
            var oldLayout = menu.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            if (oldLayout != null) Object.DestroyImmediate(oldLayout);
            var columns = menu.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>()
                ?? menu.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            columns.padding = new RectOffset(28, 28, 24, 24);
            columns.spacing = 26;
            columns.childAlignment = TextAnchor.UpperLeft;
            columns.childControlWidth = true;
            columns.childControlHeight = true;
            columns.childForceExpandWidth = true;
            columns.childForceExpandHeight = true;

            var left = FindChild(menu, "LeftColumn");
            if (left == null)
            {
                left = new GameObject("LeftColumn", typeof(RectTransform), typeof(UnityEngine.UI.LayoutElement),
                    typeof(UnityEngine.UI.VerticalLayoutGroup)).GetComponent<RectTransform>();
                left.SetParent(menu, false);
            }
            left.SetSiblingIndex(0);
            var leftElement = left.GetComponent<UnityEngine.UI.LayoutElement>();
            leftElement.flexibleWidth = 1.08f;
            leftElement.flexibleHeight = 1;
            var leftLayout = left.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
            leftLayout.padding = new RectOffset(0, 0, 0, 0);
            leftLayout.spacing = 7;
            leftLayout.childAlignment = TextAnchor.UpperLeft;
            leftLayout.childControlWidth = true;
            leftLayout.childControlHeight = true;
            leftLayout.childForceExpandWidth = true;
            leftLayout.childForceExpandHeight = false;

            var order = new[]
            {
                "Title", "Stage", "Status", "NetworkSettings", "NameLabel", "Nickname", "PlayPreview",
                "RoomLabel", "RoomCode", "CheckCode", "CreateRoom", "JoinRoom", "Reset", "Message"
            };
            foreach (var name in order)
            {
                var child = FindChild(menu, name);
                if (child != null) child.SetParent(left, false);
            }
            settings.SetParent(left, false);
            settings.SetAsLastSibling();
            SetPreferredHeight(FindChild(left, "Title"), 54);
            SetPreferredHeight(FindChild(left, "Stage"), 28);
            SetPreferredHeight(FindChild(left, "Status"), 30);
            SetPreferredHeight(FindChild(left, "NetworkSettings"), 28);
            SetPreferredHeight(FindChild(left, "NameLabel"), 25);
            SetPreferredHeight(FindChild(left, "Nickname"), 46);
            SetPreferredHeight(FindChild(left, "PlayPreview"), 46);
            SetPreferredHeight(FindChild(left, "RoomLabel"), 25);
            SetPreferredHeight(FindChild(left, "RoomCode"), 46);
            SetPreferredHeight(FindChild(left, "CheckCode"), 46);
            SetPreferredHeight(FindChild(left, "CreateRoom"), 46);
            SetPreferredHeight(FindChild(left, "JoinRoom"), 46);
            SetPreferredHeight(FindChild(left, "Reset"), 46);
            SetPreferredHeight(FindChild(left, "Message"), 60);
            SetPreferredHeight(settings, 144);
            settings.anchorMin = settings.anchorMax = settings.pivot = new Vector2(.5f, .5f);

            var dropdown = settings.GetComponentInChildren<TMP_Dropdown>(true).GetComponent<RectTransform>();
            dropdown.anchorMin = dropdown.anchorMax = new Vector2(0, 1);
            dropdown.pivot = new Vector2(0, 1);
            dropdown.anchoredPosition = new Vector2(140, -54);
            dropdown.sizeDelta = new Vector2(300, 34);

            preview.SetParent(menu, false);
            preview.SetSiblingIndex(1);
            var previewElement = preview.GetComponent<UnityEngine.UI.LayoutElement>()
                ?? preview.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            previewElement.flexibleWidth = .92f;
            previewElement.flexibleHeight = 1;
            var viewport = preview.GetComponentInChildren<UnityEngine.UI.RawImage>(true).rectTransform;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(14, 14);
            viewport.offsetMax = new Vector2(-14, -52);
        }

        private static void SetPreferredHeight(RectTransform rect, float height)
        {
            if (rect == null) return;
            var element = rect.GetComponent<UnityEngine.UI.LayoutElement>()
                ?? rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            element.preferredHeight = height;
        }

        private static DogSelectionPreview EnsureSelectionPreview(Scene scene, RectTransform menu,
            TMP_FontAsset font, DogModelCatalog catalog)
        {
            const int previewLayer = 31;
            var existing = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<DogSelectionPreview>(true)).FirstOrDefault();
            if (existing != null)
            {
                Reference(existing, "catalog", catalog);
                return existing;
            }

            var panel = new GameObject("Dog Preview", typeof(RectTransform), typeof(Image),
                typeof(DogSelectionPreview)).GetComponent<RectTransform>();
            panel.SetParent(menu, false);
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one;
            panel.anchoredPosition = new Vector2(-24, -104);
            panel.sizeDelta = new Vector2(390, 370);
            panel.GetComponent<Image>().color = new Color(.025f, .035f, .055f, .96f);
            CreateText(panel, "PreviewTitle", "DOG PREVIEW", font, 22, new Vector2(14, -10), new Vector2(360, 30));

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RawImage)).GetComponent<RectTransform>();
            viewport.SetParent(panel, false);
            viewport.anchorMin = viewport.anchorMax = new Vector2(0, 1);
            viewport.pivot = new Vector2(0, 1);
            viewport.anchoredPosition = new Vector2(12, -48);
            viewport.sizeDelta = new Vector2(366, 310);
            var rawImage = viewport.GetComponent<RawImage>();
            rawImage.color = Color.white;
            rawImage.raycastTarget = false;

            var rig = new GameObject("Dog Preview Rig");
            rig.transform.position = new Vector3(10000, 10000, 10000);
            var anchor = new GameObject("Preview Anchor").transform;
            anchor.SetParent(rig.transform, false);
            var camera = new GameObject("Preview Camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(rig.transform, false);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.055f, .075f, .11f, 1);
            camera.cullingMask = 1 << previewLayer;
            camera.fieldOfView = 32f;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            var light = new GameObject("Preview Key Light", typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(rig.transform, false);
            light.transform.localRotation = Quaternion.Euler(35, -35, 0);
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            light.color = new Color(1f, .91f, .8f);
            light.cullingMask = 1 << previewLayer;
            var fill = new GameObject("Preview Fill Light", typeof(Light)).GetComponent<Light>();
            fill.transform.SetParent(rig.transform, false);
            fill.transform.localRotation = Quaternion.Euler(15, 145, 0);
            fill.type = LightType.Directional;
            fill.intensity = .75f;
            fill.color = new Color(.55f, .72f, 1f);
            fill.cullingMask = 1 << previewLayer;

            var mainCamera = Find<DogCamera>(scene, "Main Camera").GetComponent<Camera>();
            mainCamera.cullingMask &= ~(1 << previewLayer);
            var preview = panel.GetComponent<DogSelectionPreview>();
            Reference(preview, "catalog", catalog);
            Reference(preview, "viewport", rawImage);
            Reference(preview, "previewCamera", camera);
            Reference(preview, "previewAnchor", anchor);
            return preview;
        }

        private static RectTransform CreateSettings(RectTransform menu, TMP_FontAsset font)
        {
            var root = new GameObject("Settings", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            root.SetParent(menu, false);
            root.anchorMin = new Vector2(1, 0);
            root.anchorMax = new Vector2(1, 0);
            root.pivot = new Vector2(1, 0);
            root.anchoredPosition = new Vector2(-24, 24);
            root.sizeDelta = new Vector2(390, 170);
            root.GetComponent<Image>().color = new Color(.04f, .055f, .08f, .92f);
            CreateText(root, "SettingsTitle", "SETTINGS", font, 24, new Vector2(18, -14), new Vector2(354, 30));
            CreateText(root, "ModelLabel", "DOG MODEL", font, 17, new Vector2(18, -53), new Vector2(120, 30));
            var dropdownGo = TMP_DefaultControls.CreateDropdown(default);
            dropdownGo.name = "DogModelPicker";
            dropdownGo.transform.SetParent(root, false);
            var dropdownRect = (RectTransform)dropdownGo.transform;
            dropdownRect.anchorMin = dropdownRect.anchorMax = new Vector2(0, 1);
            dropdownRect.pivot = new Vector2(0, 1);
            dropdownRect.anchoredPosition = new Vector2(140, -54);
            dropdownRect.sizeDelta = new Vector2(230, 32);
            foreach (var text in dropdownGo.GetComponentsInChildren<TMP_Text>(true))
            {
                text.font = font;
                text.fontSize = 16;
                text.color = new Color(.08f, .1f, .14f);
            }
            var toggleRoot = new GameObject("ShowFpsToggle", typeof(RectTransform), typeof(Toggle)).GetComponent<RectTransform>();
            toggleRoot.SetParent(root, false);
            toggleRoot.anchorMin = toggleRoot.anchorMax = new Vector2(0, 1);
            toggleRoot.pivot = new Vector2(0, 1);
            toggleRoot.anchoredPosition = new Vector2(18, -105);
            toggleRoot.sizeDelta = new Vector2(330, 34);
            var box = new GameObject("Background", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            box.SetParent(toggleRoot, false);
            box.anchorMin = box.anchorMax = new Vector2(0, .5f);
            box.sizeDelta = new Vector2(28, 28);
            box.anchoredPosition = new Vector2(14, 0);
            var boxImage = box.GetComponent<Image>();
            boxImage.color = new Color(.25f, .3f, .4f, 1);
            var check = new GameObject("Checkmark", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            check.SetParent(box, false);
            check.anchorMin = new Vector2(.2f, .2f);
            check.anchorMax = new Vector2(.8f, .8f);
            check.offsetMin = check.offsetMax = Vector2.zero;
            check.GetComponent<Image>().color = new Color(.35f, .9f, .55f, 1);
            var toggle = toggleRoot.GetComponent<Toggle>();
            toggle.targetGraphic = boxImage;
            toggle.graphic = check.GetComponent<Image>();
            CreateText(toggleRoot, "Label", "Show FPS", font, 18, new Vector2(44, -1), new Vector2(260, 32));
            return root;
        }

        private static void EnsureFpsHud(Scene scene, RectTransform menu, RectTransform settings, TMP_FontAsset font)
        {
            var existing = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FpsHud>(true)).FirstOrDefault();
            if (existing != null)
            {
                var serialized = new SerializedObject(existing);
                if (serialized.FindProperty("display").objectReferenceValue is GameObject existingDisplay)
                    existingDisplay.SetActive(false);
                return;
            }
            var canvas = menu.GetComponentInParent<Canvas>().transform;
            var hudRoot = new GameObject("FPS HUD", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            hudRoot.SetParent(canvas, false);
            hudRoot.anchorMin = hudRoot.anchorMax = hudRoot.pivot = Vector2.one;
            hudRoot.anchoredPosition = new Vector2(-18, -18);
            hudRoot.sizeDelta = new Vector2(190, 44);
            hudRoot.GetComponent<Image>().color = new Color(0, 0, 0, .62f);
            hudRoot.GetComponent<Image>().raycastTarget = false;
            var text = CreateText(hudRoot, "FPS", "-- FPS  |  --.- ms", font, 19, new Vector2(10, -6), new Vector2(170, 32));
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            var controller = canvas.gameObject.AddComponent<FpsHud>();
            Reference(controller, "toggle", settings.GetComponentInChildren<Toggle>(true));
            Reference(controller, "display", hudRoot.gameObject);
            Reference(controller, "label", text);
            hudRoot.gameObject.SetActive(false);
        }

        private static TMP_Text CreateText(RectTransform parent, string name, string value, TMP_FontAsset font,
            float size, Vector2 position, Vector2 dimensions)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false);
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            text.font = font;
            text.fontSize = size;
            text.color = Color.white;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform FindChild(RectTransform parent, string name)
            => parent.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(item => item.name == name);

        private static T Find<T>(Scene scene, string name) where T : Component
            => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true))
                .Single(component => component.name == name);

        private static void Reference(Object target, string field, Object value)
        {
            if (target == null || value == null) throw new ArgumentNullException(field);
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException($"Missing serialized field {field} on {target.name}.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
