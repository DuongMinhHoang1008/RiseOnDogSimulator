using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DogSimulator.CustomDogFace;
using DogSimulator.Networking;
using DogSimulator.Player;
using DogSimulator.UI;
using Fusion;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DogSimulator.Editor
{
    public static partial class EditorFirstMigration
    {
        // Each entry grants only the listed method and number of calls. See Docs/EDITOR_FIRST_AUDIT.md.
        private static readonly Dictionary<string, int> LifecycleCalls = new()
        {
            ["DogInputReader.cs:Awake:Instantiate"] = 1,
            ["DogVisualPresenter.cs:InstantiateVisual:Instantiate"] = 1,
            ["FusionSessionService.cs:CreateRunner:Instantiate"] = 1,
            ["FusionSessionService.Rooms.cs:ChangeBrowsingAsync:Instantiate"] = 1,
            ["FusionSessionService.cs:FindOrCreateSandboxRegistryAsync:Spawn"] = 1,
            ["FusionSessionService.cs:SpawnLocalAvatar:Spawn"] = 1,
            ["FusionSessionService.cs:OnPlayerJoined:Spawn"] = 1,
            ["SandboxInteractionRegistry.cs:SpawnContent:Spawn"] = 1,
            ["SandboxInteractionRegistry.cs:SpawnCity:Spawn"] = 3,
            ["SandboxInteractionRegistry.cs:SpawnMission:Spawn"] = 2,
            ["SandboxInteractionRegistry.cs:SpawnPoopIfDue:Spawn"] = 1,
            ["SandboxInteractionRegistry.Jail.cs:SpawnJail:Spawn"] = 2,
        };

        private static readonly Regex Creation = new(
            @"\b(?:(?<operation>new)\s+(?:UnityEngine\.)?GameObject\b|(?<operation>AddComponent|CreatePrimitive|Instantiate(?:Async)?|CreateInstance|Spawn(?:Async)?)\s*(?=<|\())");
        private static readonly Regex StaticLookup = new(
            @"\b(?:Find(?:Any|First)ObjectByType|FindObjects?(?:Of|By)Type)\s*[<(]|\b(?:GameObject|transform)\.Find\s*\(|(?<![\w.])(?:(?:this|gameObject|transform|Runner)\.)?(?:TryGetComponent|GetComponents?(?:InChildren|InParent)?)\s*[<(]");
        private static readonly Regex MethodDeclaration = new(
            @"^\s*(?:public|internal|protected|private)\s+(?:(?:static|async|override|virtual|sealed)\s+)*[\w<>,.? ]+\s+(\w+)\s*\(");

        [MenuItem("Tools/Dog Simulator/Validate Editor-Authored Dependencies")]
        public static void Validate()
        {
            var failures = new List<string>();
            CheckPlayerSource(failures);
            foreach (var path in PrefabsInDependencyOrder())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                CheckHierarchy(prefab, path, failures);
            }
            foreach (var path in Paths("t:Scene"))
            {
                var scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    foreach (var root in scene.GetRootGameObjects()) CheckHierarchy(root, path, failures);
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
            }
            foreach (var path in Paths("t:DogModelCatalog"))
            {
                var catalog = AssetDatabase.LoadAssetAtPath<DogModelCatalog>(path);
                foreach (var entry in catalog.Entries)
                    if (entry.VisualPrefab != null && (entry.Visual == null || entry.Visual.gameObject != entry.VisualPrefab))
                        failures.Add($"{path}: model {entry.ModelId} is missing its authored visual binding.");
            }
            if (failures.Count > 0)
                throw new InvalidOperationException($"Editor-first validation found {failures.Count} issue(s).\n{string.Join("\n", failures)}");
            Debug.Log("Editor-first validation passed for owned scenes, prefabs, catalogs and runtime call sites.");
            Debug.LogWarning("DogVisualPresenter.InstantiateVisual is a documented runtime model factory pending explicit project approval.");
        }

        private static void CheckPlayerSource(List<string> failures)
        {
            var remaining = new Dictionary<string, int>(LifecycleCalls);
            var files = CompilationPipeline.GetAssemblies(AssembliesType.Player)
                .SelectMany(assembly => assembly.sourceFiles)
                .Select(path => path.Replace('\\', '/'))
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal)
                    && !path.StartsWith("Assets/Photon/", StringComparison.Ordinal)
                    && !path.StartsWith("Assets/SimplePoly City - Low Poly Assets/", StringComparison.Ordinal)
                    && !path.StartsWith("Assets/TextMesh Pro/", StringComparison.Ordinal)
                    && !path.StartsWith("Assets/Game/Tests/", StringComparison.Ordinal))
                .Distinct();
            foreach (var path in files)
            {
                var blocks = new Stack<bool>();
                var editorOnly = false;
                var method = "";
                var lineNumber = 0;
                foreach (var rawLine in File.ReadLines(path))
                {
                    lineNumber++;
                    var line = rawLine.Trim();
                    if (line.StartsWith("#if ", StringComparison.Ordinal))
                    {
                        var editorGuard = line == "#if UNITY_EDITOR" || line.StartsWith("#if UNITY_EDITOR && ", StringComparison.Ordinal);
                        blocks.Push(editorOnly);
                        editorOnly |= editorGuard;
                        continue;
                    }
                    if (line == "#else" || line.StartsWith("#elif ", StringComparison.Ordinal))
                    {
                        editorOnly = blocks.Peek() || line == "#elif UNITY_EDITOR";
                        continue;
                    }
                    if (line == "#endif")
                    {
                        editorOnly = blocks.Pop();
                        continue;
                    }
                    if (editorOnly || line.StartsWith("//", StringComparison.Ordinal)) continue;
                    var declaration = MethodDeclaration.Match(line);
                    if (declaration.Success) method = declaration.Groups[1].Value;
                    foreach (Match match in Creation.Matches(line))
                    {
                        // A call from an expression-bodied method still counts.
                        var key = $"{Path.GetFileName(path)}:{method}:{match.Groups["operation"].Value}";
                        if (remaining.TryGetValue(key, out var count) && count > 0) remaining[key] = count - 1;
                        else failures.Add($"{path}:{lineNumber}: runtime creation has no matching lifecycle exception: {match.Value}");
                    }
                    if (StaticLookup.IsMatch(line))
                        failures.Add($"{path}:{lineNumber}: static component or scene lookup: {line}");
                }
            }
            foreach (var entry in remaining)
                if (entry.Value != 0) failures.Add($"Stale creation exception: {entry.Key} ({entry.Value} unused).");
        }

        private static void CheckHierarchy(GameObject root, string path, List<string> failures)
        {
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
            {
                var missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject);
                if (missing > 0) failures.Add($"{path} :: {HierarchyName(node)} has {missing} missing script(s).");
            }
            var owned = OwnedBehaviours(root).ToArray();
            foreach (var duplicate in owned.GroupBy(component => (component.gameObject, component.GetType())).Where(group => group.Count() > 1))
                failures.Add($"{path} :: {HierarchyName(duplicate.Key.gameObject.transform)} has duplicate {duplicate.Key.Item2.Name}.");
            foreach (var component in owned)
            {
                var context = $"{path} :: {HierarchyName(component.transform)} ({component.GetType().Name})";
                var data = new SerializedObject(component);
                var iterator = data.GetIterator();
                while (iterator.Next(true))
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference && iterator.objectReferenceValue == null
                        && iterator.objectReferenceInstanceIDValue != 0)
                        failures.Add($"{context}.{iterator.propertyPath} points to a missing object.");
                CheckRequiredFields(component, data, context, failures);
                switch (component)
                {
                    case NetworkDogPlayer when data.FindProperty("peeMaterial").objectReferenceValue != null:
                        CheckUniqueChildren(component.transform, context, failures, "Pee Stream");
                        break;
                    case NetworkCityPedestrian:
                        CheckUniqueChildren(component.transform, context, failures, "Pedestrian Pose", "Police Status");
                        break;
                    case NetworkCarryable when data.FindProperty("bowlVisual").objectReferenceValue != null:
                        CheckUniqueChildren(component.transform, context, failures, "Jail Bowl Visual");
                        break;
                    case SimplePolyCityWorld when data.FindProperty("missionCratePrefab").objectReferenceValue != null:
                        CheckUniqueChildren(component.transform, context, failures, "Golden Bone Courtyard", "Dog Jail");
                        break;
                }
            }
            foreach (var front in root.GetComponentsInChildren<DogFrontEnd>(true))
            {
                var pages = new SerializedObject(front).FindProperty("authoredPages");
                if (pages.arraySize != 5) failures.Add($"{path} :: {HierarchyName(front.transform)} needs five authored pages.");
                var layouts = front.transform.Cast<Transform>().Where(child => child.name == "Dog Simulator Menu").ToArray();
                if (layouts.Length != 1)
                {
                    failures.Add($"{path} :: {HierarchyName(front.transform)} needs exactly one authored menu.");
                    continue;
                }
                foreach (var button in layouts[0].GetComponentsInChildren<DogMenuButton>(true))
                {
                    var target = button.onClick.GetPersistentTarget(0);
                    var method = button.onClick.GetPersistentMethodName(0);
                    if (button.onClick.GetPersistentEventCount() != 1 || target == null || string.IsNullOrEmpty(method)
                        || (UnityEventBase.GetValidMethodInfo(target, method, Type.EmptyTypes) == null
                            && UnityEventBase.GetValidMethodInfo(target, method, new[] { typeof(int) }) == null))
                        failures.Add($"{path} :: {HierarchyName(button.transform)} is missing its authored click handler.");
                }
            }
        }

        private static void CheckRequiredFields(MonoBehaviour component, SerializedObject data, string context, List<string> failures)
        {
            string fields = component switch
            {
                DogMotor => "controller ownColliders",
                DogBackSupport => "ownerRoot ownerMotor",
                DogAnimationDriver => "motor visual",
                LocalMovementPreview => "input animations followCamera",
                DogFaceAppearance => "presenter",
                DogVisualPresenter => "animator visualRoot defaultVisual",
                DogModelVisual => "animator renderers colliders bodies demoBehaviours",
                DogSelectionPreview => "catalog viewport previewCamera previewAnchor",
                NetworkDogPlayer => "motor input animations transformSync seatColliders",
                NetworkCityVehicle => "body hull transformSync",
                NetworkCityPedestrian => "body standing lying agent visual transformSync policeMarker policeAudio",
                NetworkCarryable => "body transformSync colliders goldenRenderers",
                FusionSessionService => "playerControllerPrefab gameRunnerPrefab lobbyRunnerPrefab",
                FusionRunnerRoot runner when runner.name == "GameRunner" => "runner events sceneManager objectProvider faceService",
                FusionRunnerRoot => "runner events",
                GoldenBoneCourse => "gate gateCollider gateObstacle plateRenderer plateRest platePressed",
                DogJail => "gate lever",
                DogMenuSliderValue => "slider label",
                BrushColorPicker => "wheelImage squareImage hueMarkerImage squareMarkerImage hueMarker squareMarker",
                DogFrontEnd => "session preview settings yard authoredPages nickname roomCode prepareMessage hubMessage continueButton "
                    + "cancelConnectionButton homeDog selectedDog faceDog faceCanvas faceEditor dogName continueLabel faceViewLabel "
                    + "faceHelp prepareStatusStrip prepareBack faceButton faceTab previousDog nextDog dogTiles worldSlots worldButtons "
                    + "worldLabels dogTileLabels dogPortraits exitNote exitPaper faceTabs faceTabButtons brushButton eraseButton "
                    + "imageHint brushPicker brushColorPreview red green blue roomRows roomNames roomPlayers roomJoins roomPager "
                    + "emptyRooms previousRoomPage nextRoomPage settingsCopy settingsLeave settingsResume settingsClose settingsApply "
                    + "settingsMessage qualityValue volumeValue volume settingsTabs settingsTabButtons settingsToggles settingButtons "
                    + "interactionHint actionHints cooldownHints missionHint missionControls roomStatus wantedHint jailHint settingsHint",
                _ => ""
            };
            if (component is SimplePolyCityWorld && data.FindProperty("missionCratePrefab").objectReferenceValue != null)
                fields = "course jail";
            if (component is NetworkDogPlayer && data.FindProperty("peeMaterial").objectReferenceValue != null)
                fields += " peeStream";
            if (component is NetworkCarryable && component.name == "CarryableCrate")
                fields += " bowlVisual crateRenderer crateCollider";
            if (component is FusionPreviewUI && data.FindProperty("frontEnd").objectReferenceValue != null)
                fields = "legacyMenu";
            if (component is DogFaceEditorController && data.FindProperty("livePreview").boolValue)
                fields = "modal canvas preview imagePicker status zoomSlider rotationSlider positionX positionY brushSizeSlider";
            foreach (var field in fields.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var property = data.FindProperty(field);
                if (property == null)
                {
                    failures.Add($"{context}.{field} is missing from serialization.");
                    continue;
                }
                if (!property.isArray)
                {
                    if (property.objectReferenceValue == null) failures.Add($"{context}.{field} is unassigned.");
                    continue;
                }
                for (var i = 0; i < property.arraySize; i++)
                    if (property.GetArrayElementAtIndex(i).objectReferenceValue == null)
                        failures.Add($"{context}.{field}[{i}] is unassigned.");
            }
        }

        private static void CheckUniqueChildren(Transform parent, string context, List<string> failures, params string[] names)
        {
            foreach (var name in names)
                if (parent.Cast<Transform>().Count(child => child.name == name) != 1)
                    failures.Add($"{context} needs exactly one authored '{name}' child.");
        }

        private static string HierarchyName(Transform node)
            => node.parent != null ? $"{HierarchyName(node.parent)}/{node.name}" : node.name;
    }
}
