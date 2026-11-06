using System.IO;
using DogSimulator.Networking;
using DogSimulator.Player;
using DogSimulator.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace DogSimulator.Editor
{
    public static class LocalPreviewSetup
    {
        public const string ScenePath = "Assets/Game/Scenes/M1_LocalPreview.unity";
        private static readonly Color Ink = new Color(0.06f, 0.10f, 0.15f);
        private static readonly Color Light = new Color(0.92f, 0.95f, 0.97f);
        private static bool exitAfterSetup;
        private static double setupStarted;

        // ImportPackage finishes on a later Editor update. Do not launch this with -quit.
        public static void CreateBatch()
        {
            exitAfterSetup = true;
            setupStarted = EditorApplication.timeSinceStartup;
            EditorApplication.update += CheckTimeout;
            ContinueSetup();
        }

        private static void CheckTimeout()
        {
            if (EditorApplication.timeSinceStartup - setupStarted < 120) return;
            Debug.LogError("Timed out waiting for preview setup/TMP resource import.");
            EditorApplication.Exit(1);
        }

        private static void ContinueSetup()
        {
            try { Create(); }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                if (exitAfterSetup) EditorApplication.Exit(1);
            }
        }

        private static void OnResourcesImported(string packageName)
        {
            AssetDatabase.importPackageCompleted -= OnResourcesImported;
            EditorApplication.delayCall += ContinueSetup;
        }

        [MenuItem("Tools/Dog Simulator/Create or Open M1 Local Preview")]
        public static void Create()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
                Debug.Log("Opened existing M1 local preview without rebuilding its content.");
                if (exitAfterSetup) EditorApplication.Exit(0);
                return;
            }
            if (!File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
            {
                AssetDatabase.importPackageCompleted -= OnResourcesImported;
                AssetDatabase.importPackageCompleted += OnResourcesImported;
                TMP_PackageResourceImporter.ImportResources(true, false, false);
                Debug.Log("Importing bundled TMP essentials; preview setup resumes when import completes.");
                return;
            }
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (font == null) throw new System.InvalidOperationException("Import TMP Essential Resources before creating the preview.");

            Directory.CreateDirectory("Assets/Game/Scenes");
            Directory.CreateDirectory("Assets/Game/Config");
            Directory.CreateDirectory("Assets/Game/Materials");
            Directory.CreateDirectory("Assets/Game/Prefabs/Dogs");
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var movement = LoadOrCreate<DogMovementConfig>("Assets/Game/Config/DogMovementConfig.asset");
            var network = LoadOrCreate<NetworkConfig>("Assets/Game/Config/NetworkConfig.asset");
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            if (actions == null) throw new System.InvalidOperationException("Existing Input System action asset is missing.");
            RenderSettings.ambientLight = new Color(0.68f, 0.75f, 0.8f);
            var floorMaterial = Material("Floor", new Color(0.30f, 0.45f, 0.43f));
            var wallMaterial = Material("Walls", new Color(0.69f, 0.75f, 0.77f));
            Box("TestArenaFloor", null, new Vector3(0, -0.25f, 0), new Vector3(30, 0.5f, 24), floorMaterial);
            Box("NorthWall", null, new Vector3(0, 1, 12), new Vector3(30, 2, 0.4f), wallMaterial);
            Box("SouthWall", null, new Vector3(0, 1, -12), new Vector3(30, 2, 0.4f), wallMaterial);
            Box("EastWall", null, new Vector3(15, 1, 0), new Vector3(0.4f, 2, 24), wallMaterial);
            Box("WestWall", null, new Vector3(-15, 1, 0), new Vector3(0.4f, 2, 24), wallMaterial);
            Box("JumpTestPlatform", null, new Vector3(3, 0.3f, 3), new Vector3(3, 0.6f, 2), wallMaterial);
            Box("CollisionTestBlock", null, new Vector3(-3, 0.8f, 3), new Vector3(2, 1.6f, 2), wallMaterial);

            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);

            var dog = new GameObject("DogPlayer — LOCAL");
            dog.layer = 2; // Ignore Raycast: camera queries exclude the local avatar.
            dog.transform.position = new Vector3(0, 0.1f, -2);
            var controller = dog.AddComponent<CharacterController>();
            controller.height = 1;
            controller.radius = 0.35f;
            controller.center = new Vector3(0, 0.55f, 0);
            controller.stepOffset = 0.25f;
            controller.skinWidth = 0.04f;
            controller.excludeLayers = 1 << 2;
            var motor = dog.AddComponent<DogMotor>();
            var input = dog.AddComponent<DogInputReader>();
            var driver = dog.AddComponent<LocalMovementPreview>();
            Reference(motor, "config", movement);
            Reference(input, "config", movement);
            Reference(input, "actions", actions);

            DogGameplaySetup.ConfigurePlayer(dog);
            var mouth = new GameObject("MouthAnchor").transform;
            mouth.SetParent(dog.transform, false);
            mouth.localPosition = new Vector3(0, 0.80f, 0.60f);

            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.47f, 0.63f, 0.73f);
            camera.gameObject.AddComponent<AudioListener>();
            var follow = camera.gameObject.AddComponent<DogCamera>();
            Reference(follow, "target", dog.transform);
            Reference(follow, "input", input);
            Reference(follow, "config", movement);
            Reference(driver, "followCamera", follow);

            CreateUI(font, network, input, motor);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("M1 LOCAL preview generated. Fusion SDK/App ID absent: no network session or multiplayer acceptance claimed.");
            if (exitAfterSetup) EditorApplication.Exit(0);
        }

        private static void CreateUI(TMP_FontAsset font, NetworkConfig network, DogInputReader input, DogMotor motor)
        {
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var canvas = new GameObject("Preview UI", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var menu = Rect("Menu", canvas.transform);
            menu.anchorMin = new Vector2(0.18f, 0.04f);
            menu.anchorMax = new Vector2(0.82f, 0.96f);
            menu.offsetMin = menu.offsetMax = Vector2.zero;
            menu.gameObject.AddComponent<UnityEngine.UI.Image>().color = Ink;
            var layout = menu.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 24, 24);
            layout.spacing = 10;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            Text("Title", menu, font, "OFFICE DOGS", 48, 66);
            Text("Stage", menu, font, "M1 DEVELOPMENT PREVIEW  /  OFFLINE", 24, 34);
            Text("Status", menu, font, "Multiplayer needs Photon Fusion 2 SDK + Fusion App ID.", 24, 40);
            Text("NetworkSettings", menu, font, $"Planned region: {network.Region}   •   Dev capacity: {network.MaxPlayers} (unverified)   •   {network.GameVersion}", 20, 34);
            Text("NameLabel", menu, font, "YOUR NICKNAME  /  2–16 CHARACTERS", 20, 30);
            var name = Field("Nickname", menu, font, "Guest name", 16);
            var play = Button("PlayPreview", menu, font, "Play local preview", new Color(0.16f, 0.52f, 0.39f));
            Text("RoomLabel", menu, font, "ROOM CODE  /  FORMAT CHECK ONLY", 20, 30);
            var code = Field("RoomCode", menu, font, "6–8 characters", 8);
            var check = Button("CheckCode", menu, font, "Check code format", new Color(0.22f, 0.30f, 0.39f));
            var create = Button("CreateRoom", menu, font, "Create Room — Photon setup required", new Color(0.22f, 0.27f, 0.32f));
            create.interactable = false;
            var join = Button("JoinRoom", menu, font, "Join Room — Photon setup required", new Color(0.22f, 0.27f, 0.32f));
            join.interactable = false;
            var back = Button("Reset", menu, font, "Reset local position", new Color(0.22f, 0.30f, 0.39f));
            var message = Text("Message", menu, font, "Rigged dog in a test arena. Actions are animations only; no network connection is made.", 22, 56);
            message.color = new Color(1, 0.81f, 0.44f);

            var hud = Rect("HUD", canvas.transform);
            Stretch(hud);
            var label = Text("Player", hud, font, "OFFLINE", 24, 48);
            Anchor(label.rectTransform, new Vector2(0.02f, 0.88f), new Vector2(0.85f, 0.97f));
            var hint = Text("Controls", hud, font, DogAnimationDriver.Controls, 25, 100);
            Anchor(hint.rectTransform, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.14f));
            label.color = hint.color = Color.white;
            hud.gameObject.SetActive(false);

            var ui = canvas.AddComponent<LocalPreviewUI>();
            Reference(ui, "menu", menu.gameObject);
            Reference(ui, "hud", hud.gameObject);
            Reference(ui, "nickname", name);
            Reference(ui, "roomCode", code);
            Reference(ui, "message", message);
            Reference(ui, "playerLabel", label);
            Reference(ui, "playButton", play);
            Reference(ui, "checkCodeButton", check);
            Reference(ui, "backButton", back);
            Reference(ui, "input", input);
            Reference(ui, "motor", motor);
        }

        private static TMP_InputField Field(string name, Transform parent, TMP_FontAsset font, string hint, int limit)
        {
            var rect = Rect(name, parent);
            rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 54;
            var background = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            background.color = new Color(0.16f, 0.22f, 0.29f);
            var viewport = Rect("Viewport", rect);
            Stretch(viewport);
            viewport.offsetMin = new Vector2(14, 5);
            viewport.offsetMax = new Vector2(-14, -5);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var text = Text("Text", viewport, font, "", 26, 44);
            Stretch(text.rectTransform);
            var placeholder = Text("Placeholder", viewport, font, hint, 26, 44);
            Stretch(placeholder.rectTransform);
            placeholder.color = new Color(0.65f, 0.72f, 0.78f);
            var field = rect.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = viewport;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.targetGraphic = background;
            field.characterLimit = limit;
            field.richText = false;
            field.lineType = TMP_InputField.LineType.SingleLine;
            return field;
        }

        private static UnityEngine.UI.Button Button(string name, Transform parent, TMP_FontAsset font, string caption, Color color)
        {
            var rect = Rect(name, parent);
            rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 54;
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            var label = Text("Label", rect, font, caption, 26, 54);
            Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static TextMeshProUGUI Text(string name, Transform parent, TMP_FontAsset font, string caption, int size, int height)
        {
            var rect = Rect(name, parent);
            rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = caption;
            text.fontSize = size;
            text.color = Light;
            text.richText = false; // Guest names are plain text.
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect) => Anchor(rect, Vector2.zero, Vector2.one);
        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collision = true)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = scale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) Object.DestroyImmediate(box.GetComponent<Collider>());
            return box;
        }

        private static Material Material(string name, Color color)
        {
            var path = $"Assets/Game/Materials/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new System.InvalidOperationException("Existing URP Lit shader not found.");
            var material = new Material(shader) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void Reference(Object target, string field, Object value)
        {
            if (value == null) throw new System.ArgumentNullException(field, "Cannot serialize a missing preview reference.");
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null) throw new System.ArgumentException($"Unknown serialized field {field}");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
