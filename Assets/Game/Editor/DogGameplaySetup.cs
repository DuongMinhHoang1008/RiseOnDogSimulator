using System;
using System.Linq;
using DogSimulator.Player;
using DogSimulator.Networking;
using Fusion.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DogSimulator.Editor
{
    public static class DogGameplaySetup
    {
        public const string AnimationConfigPath = "Assets/Game/Config/DogAnimationConfig.asset";
        public const string ControllerPath = "Assets/Game/Animation/DogGameplay.controller";
        public const string JumpPath = "Assets/Game/Animation/Jump_Gameplay.anim";
        public const string VisualPrefabPath = "Assets/Art/Models/Dogs/RealisticDogCustomFace_20261108/Unity/RealisticDogCustomFace.prefab";
        public const string VisualControllerPath = "Assets/Game/Animation/Catalog/RealisticDogCustomFace.overrideController";
        private const string ClipsPath = "Assets/Art/ReferenceDog/low_poly/Dog_Rigged.fbx";

        [MenuItem("Tools/Dog Simulator/Assign Rigged Dog to Gameplay")]
        public static void ApplyFromMenu() => Apply();

        public static string Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode before assigning gameplay assets.");
            EnsureFolder("Assets/Game/Animation");
            var prefabRoot = PrefabUtility.LoadPrefabContents(FusionPreviewSetup.PrefabPath);
            try
            {
                ConfigurePlayer(prefabRoot);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, FusionPreviewSetup.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefabRoot); }
            var scene = SceneManager.GetSceneByPath(LocalPreviewSetup.ScenePath);
            var wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(LocalPreviewSetup.ScenePath, OpenSceneMode.Additive);
            var wasDirty = scene.isDirty;
            try
            {
                var local = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LocalMovementPreview>(true)).Single();
                ConfigurePlayer(local.gameObject);
                local.name = "DogPlayer — LOCAL";
                foreach (var field in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TMP_InputField>(true))) field.richText = false;
                foreach (var text in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)))
                {
                    text.richText = false;
                    if (text.name == "Controls") text.text = DogAnimationDriver.Controls;
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!wasDirty) EditorSceneManager.SaveScene(scene);
            }
            finally { if (!wasLoaded) EditorSceneManager.CloseScene(scene, true); }
            AssetDatabase.SaveAssets();
            var network = AssetDatabase.LoadAssetAtPath<NetworkConfig>(FusionPreviewSetup.ConfigPath);
            if (network != null)
            {
                var serialized = new SerializedObject(network);
                var version = serialized.FindProperty("gameVersion");
                if (!version.stringValue.EndsWith("-riganim-v1", StringComparison.Ordinal))
                {
                    version.stringValue += "-riganim-v1";
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssets();
                }
            }
            NetworkProjectConfigUtilities.RebuildPrefabTable();
            return "Assigned rigged visual + gameplay animation to network prefab and local player. Local scene saved=" + !wasDirty + ". Existing dirty Fusion scene left unsaved.";
        }

        public static void ConfigurePlayer(GameObject root)
        {
            EnsureFolder("Assets/Game/Animation");
            var settings = AssetDatabase.LoadAssetAtPath<DogAnimationConfig>(AnimationConfigPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<DogAnimationConfig>();
                AssetDatabase.CreateAsset(settings, AnimationConfigPath);
            }
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) == null) CreateController(settings);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath);
            if (model == null) throw new InvalidOperationException("Missing supplied rigged dog: " + VisualPrefabPath);
            foreach (var child in root.transform.Cast<Transform>().ToArray())
                if (child.name.StartsWith("DogPlaceholder", StringComparison.Ordinal) || child.name == "DogVisual") Object.DestroyImmediate(child.gameObject);
            var visualRoot = new GameObject("DogVisual");visualRoot.transform.SetParent(root.transform, false);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, visualRoot.transform);
            var animator = instance.GetComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.updateWhenOffscreen = false;
            foreach (var node in visualRoot.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 2;
            var presenter = root.GetComponent<DogVisualPresenter>() ?? root.AddComponent<DogVisualPresenter>();
            var driver = root.GetComponent<DogAnimationDriver>() ?? root.AddComponent<DogAnimationDriver>();
            Reference(presenter, "animator", animator);Reference(presenter, "config", settings);
            Reference(presenter, "visualRoot", visualRoot.transform);
            Reference(presenter, "defaultVisual", instance);
            Reference(driver, "visual", presenter);Reference(driver, "config", settings);
            var capsule = root.GetComponent<CharacterController>();
            capsule.height = 1.1f;capsule.radius = .30f;capsule.center = new Vector3(0, .55f, 0);
            var mouth = root.transform.Find("MouthAnchor");
            if (mouth != null) mouth.localPosition = new Vector3(0, .80f, .60f);
            Reference(presenter, "mouthAnchor", mouth);
            var nickname = root.transform.Find("Nickname");
            if (nickname != null) nickname.localPosition = new Vector3(0, 1.40f, 0);
        }

        private static AnimatorController CreateController(DogAnimationConfig settings)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(ClipsPath).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToDictionary(c => c.name);
            var jump = AssetDatabase.LoadAssetAtPath<AnimationClip>(JumpPath);
            if (jump == null) { jump = new AnimationClip();AssetDatabase.CreateAsset(jump, JumpPath); }
            EditorUtility.CopySerialized(clips["Jump"], jump);jump.name = "Jump_Gameplay";jump.hideFlags = HideFlags.None;
            // DogMotor owns vertical movement. Keep skeletal tuck/landing, freeze only the root translation.
            foreach (var binding in AnimationUtility.GetCurveBindings(jump))
                if (binding.path.EndsWith("/Root", StringComparison.Ordinal) && binding.propertyName.StartsWith("m_LocalPosition.", StringComparison.Ordinal))
                {
                    var original = AnimationUtility.GetEditorCurve(jump, binding);
                    AnimationUtility.SetEditorCurve(jump, binding, AnimationCurve.Constant(0, jump.length, original.Evaluate(0)));
                }
            EditorUtility.SetDirty(jump);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("LocomotionRate", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            var machine = controller.layers[0].stateMachine;
            var names = new[] { "Idle", "Run", "Jump", "Bark", "Poop", "Pee", "Bite" };
            var states = names.ToDictionary(n => n, n => machine.AddState(n));
            foreach (var name in names)
            {
                states[name].motion = name == "Jump" ? jump : clips[name];
                states[name].writeDefaultValues = false;
            }
            states["Run"].speedParameter = "LocomotionRate";states["Run"].speedParameterActive = true;
            states["Jump"].speed = settings.JumpRate;
            machine.defaultState = states["Idle"];
            var run = states["Idle"].AddTransition(states["Run"]);
            run.hasExitTime = false;run.duration = settings.CrossFadeSeconds;run.AddCondition(AnimatorConditionMode.Greater, .03f, "Speed");
            var idle = states["Run"].AddTransition(states["Idle"]);
            idle.hasExitTime = false;idle.duration = settings.CrossFadeSeconds;idle.AddCondition(AnimatorConditionMode.Less, .03f, "Speed");
            foreach (var name in names.Skip(2))
            {
                var exit = states[name].AddTransition(states["Idle"]);exit.duration = settings.CrossFadeSeconds;
                exit.hasExitTime = name != "Jump";exit.exitTime = .98f;
                if (name == "Jump") exit.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void Reference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
