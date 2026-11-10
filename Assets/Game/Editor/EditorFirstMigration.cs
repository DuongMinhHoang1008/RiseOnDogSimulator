using System;
using System.Collections.Generic;
using System.Linq;
using DogSimulator.CustomDogFace;
using DogSimulator.Networking;
using DogSimulator.Player;
using DogSimulator.UI;
using Fusion;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace DogSimulator.Editor
{
    public static partial class EditorFirstMigration
    {
        private const string CratePath = "Assets/Game/Prefabs/Carryables/CarryableCrate.prefab";
        private static readonly string[] AssetRoots = { "Assets/Game", "Assets/Art", "Assets/Scenes" };

        [MenuItem("Tools/Dog Simulator/Migrate Editor-Authored Dependencies")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Exit Play Mode and Prefab Mode before migrating.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            EnsureRunnerPrefab("GameRunner", true);
            EnsureRunnerPrefab("LobbyRunner", false);
            var changed = new List<string>();
            try
            {
                WireCatalogs();
                foreach (var path in PrefabsInDependencyOrder())
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (!OwnedBehaviours(prefab).Any()) continue;
                    var root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        var dirty = Wire(root, path);
                        if (!dirty) continue;
                        PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);
                        if (!saved) throw new InvalidOperationException($"Could not save {path}");
                        changed.Add(path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }

                foreach (var path in Paths("t:Scene"))
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var dirty = false;
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        dirty |= Wire(root, path);
                        foreach (var session in root.GetComponentsInChildren<FusionSessionService>(true))
                        {
                            dirty |= Set(session, "gameRunnerPrefab", LoadRunnerPrefab("GameRunner"));
                            dirty |= Set(session, "lobbyRunnerPrefab", LoadRunnerPrefab("LobbyRunner"));
                        }
                    }
                    var roots = scene.GetRootGameObjects();
                    var picker = Unique(roots.SelectMany(root => root.GetComponentsInChildren<DogFaceWebBridge>(true)),
                        roots.FirstOrDefault(), "scene face image picker", true);
                    if (picker != null)
                        foreach (var editor in roots.SelectMany(root => root.GetComponentsInChildren<DogFaceEditorController>(true)))
                            dirty |= Set(editor, "imagePicker", picker);
                    if (!dirty) continue;
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException($"Could not save {path}");
                    changed.Add(path);
                }
            }
            finally
            {
                if (setup.Length != 0 && setup.All(item => !string.IsNullOrEmpty(item.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                AssetDatabase.SaveAssets();
            }
            Debug.Log($"Editor-first migration saved {changed.Count} prefab/scene assets.\n{string.Join("\n", changed)}");
        }

        private static IEnumerable<string> Paths(string filter)
            => AssetDatabase.FindAssets(filter, AssetRoots).Select(AssetDatabase.GUIDToAssetPath).Distinct();

        private static IEnumerable<string> PrefabsInDependencyOrder()
        {
            var paths = Paths("t:Prefab").Where(path => path.EndsWith(".prefab", StringComparison.Ordinal)).ToArray();
            var pending = new HashSet<string>(paths);
            var ordered = new List<string>();
            foreach (var path in paths.OrderBy(path => path, StringComparer.Ordinal)) Visit(path);
            return ordered;

            void Visit(string path)
            {
                if (!pending.Remove(path)) return;
                foreach (var dependency in AssetDatabase.GetDependencies(path, false)) Visit(dependency);
                ordered.Add(path);
            }
        }

        private static IEnumerable<MonoBehaviour> OwnedBehaviours(GameObject root)
            => root.GetComponentsInChildren<MonoBehaviour>(true).Where(component =>
                component != null && (component.GetType().Namespace?.StartsWith("DogSimulator.", StringComparison.Ordinal) == true
                    || component.GetType().FullName == "ReferenceDog.Animation.ReferenceDogAnimationPreview"));

        private static bool Wire(GameObject root, string path)
        {
            var changed = false;
            foreach (var component in OwnedBehaviours(root).ToArray())
            {
                var obj = component.gameObject;
                switch (component)
                {
                    case DogMotor motor:
                        changed |= Set(motor, "controller", One<CharacterController>(obj));
                        changed |= SetArray(motor, "ownColliders", obj.GetComponentsInChildren<Collider>(true));
                        break;
                    case DogBackSupport support:
                        var owner = Read<Transform>(support, "ownerRoot") ?? support.transform.root;
                        changed |= Set(support, "ownerRoot", owner);
                        changed |= Set(support, "ownerMotor", One<DogMotor>(owner.gameObject));
                        break;
                    case DogVisualPresenter visual:
                        changed |= WireDefaultVisual(visual);
                        break;
                    case DogAnimationDriver driver:
                        changed |= Set(driver, "motor", One<DogMotor>(obj));
                        break;
                    case LocalMovementPreview preview:
                        changed |= Set(preview, "input", One<DogInputReader>(obj));
                        changed |= Set(preview, "animations", One<DogAnimationDriver>(obj));
                        break;
                    case DogFaceAppearance face:
                        changed |= Set(face, "presenter", One<DogVisualPresenter>(obj));
                        break;
                    case NetworkDogPlayer dog:
                        changed |= WireDog(dog);
                        break;
                    case NetworkCityVehicle vehicle:
                        changed |= Set(vehicle, "body", One<Rigidbody>(obj));
                        changed |= Set(vehicle, "hull", One<BoxCollider>(obj));
                        changed |= Set(vehicle, "transformSync", One<NetworkTransform>(obj));
                        changed |= Set(vehicle, "obstacle", One<NavMeshObstacle>(obj, optional: true));
                        break;
                    case NetworkCityPedestrian npc:
                        changed |= WirePedestrian(npc);
                        break;
                    case NetworkCarryable carryable:
                        changed |= Set(carryable, "body", One<Rigidbody>(obj));
                        changed |= Set(carryable, "transformSync", One<NetworkTransform>(obj));
                        changed |= SetArray(carryable, "colliders", obj.GetComponentsInChildren<Collider>(true));
                        changed |= SetArray(carryable, "goldenRenderers", obj.GetComponentsInChildren<MeshRenderer>());
                        if (path == CratePath && obj == root) changed |= WireBowl(carryable);
                        break;
                    case LocalPreviewUI localUI:
                        changed |= Set(localUI, "controls", NamedText(obj, "Controls"));
                        break;
                    case FusionPreviewUI fusionUI:
                        changed |= WireFusionUI(fusionUI);
                        break;
                    case FusionSessionService session:
                        changed |= Set(session, "city", One<SimplePolyCityWorld>(obj, optional: true));
                        var prefab = Read<NetworkObject>(session, "playerPrefab");
                        if (prefab != null)
                            changed |= Set(session, "playerControllerPrefab", One<CharacterController>(prefab.gameObject));
                        break;
                    case SimplePolyCityWorld world when Read<NetworkObject>(world, "missionCratePrefab") != null:
                        if (Read<GoldenBoneCourse>(world, "course") == null)
                        {
                            BuildCourse(world);
                            changed = true;
                        }
                        if (Read<DogJail>(world, "jail") == null)
                        {
                            BuildJail(world);
                            changed = true;
                        }
                        break;
                }
            }
            return changed;
        }

        private static bool WireDog(NetworkDogPlayer dog)
        {
            var obj = dog.gameObject;
            var changed = Set(dog, "motor", One<DogMotor>(obj));
            changed |= Set(dog, "input", One<DogInputReader>(obj));
            changed |= Set(dog, "animations", One<DogAnimationDriver>(obj));
            changed |= Set(dog, "faceAppearance", One<DogFaceAppearance>(obj, optional: true));
            changed |= Set(dog, "mouthAnchor", Child(obj.transform, "MouthAnchor"));
            changed |= Set(dog, "transformSync", One<NetworkTransform>(obj));
            changed |= Set(dog, "backSupport", Unique(obj.GetComponentsInChildren<DogBackSupport>(true), obj, "back support", true));
            changed |= SetArray(dog, "seatColliders", obj.GetComponentsInChildren<Collider>(true));
            var material = Read<Material>(dog, "peeMaterial");
            if (material == null || Read<ParticleSystem>(dog, "peeStream") != null) return changed;

            var effect = Child(obj.transform, "Pee Stream") ?? NewChild(obj.transform, "Pee Stream");
            var stream = One<ParticleSystem>(effect.gameObject, optional: true) ?? effect.gameObject.AddComponent<ParticleSystem>();
            var main = stream.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = .38f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 1.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(.025f, .043f);
            main.gravityModifier = .65f;
            main.maxParticles = 48;
            var emission = stream.emission;
            emission.rateOverTime = 75;
            var shape = stream.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 4;
            shape.radius = .009f;
            One<ParticleSystemRenderer>(effect.gameObject).sharedMaterial = material;
            return Set(dog, "peeStream", stream) | changed;
        }

        private static bool WirePedestrian(NetworkCityPedestrian npc)
        {
            var obj = npc.gameObject;
            var changed = Set(npc, "agent", One<NavMeshAgent>(obj));
            changed |= Set(npc, "standing", One<CapsuleCollider>(obj));
            changed |= Set(npc, "transformSync", One<NetworkTransform>(obj));

            var body = One<Rigidbody>(obj, optional: true);
            if (body == null)
            {
                body = obj.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.mass = 75f;
                body.linearDamping = 1.5f;
                body.constraints = RigidbodyConstraints.FreezeRotation;
            }
            changed |= Set(npc, "body", body);

            var lying = One<BoxCollider>(obj, optional: true);
            if (lying == null)
            {
                lying = obj.AddComponent<BoxCollider>();
                lying.center = new Vector3(0, .28f, 0);
                lying.size = new Vector3(.62f, .54f, 1.8f);
                lying.enabled = false;
            }
            changed |= Set(npc, "lying", lying);

            var pose = Child(obj.transform, "Pedestrian Pose");
            if (pose == null)
            {
                var children = obj.transform.Cast<Transform>().Where(child => child.name != "Police Status").ToArray();
                pose = NewChild(obj.transform, "Pedestrian Pose");
                foreach (var child in children) child.SetParent(pose, false);
            }
            changed |= Set(npc, "visual", pose);

            var markerRoot = Child(obj.transform, "Police Status") ?? NewChild(obj.transform, "Police Status");
            var marker = One<TextMeshPro>(markerRoot.gameObject, optional: true);
            if (marker == null)
            {
                marker = markerRoot.gameObject.AddComponent<TextMeshPro>();
                marker.transform.localPosition = Vector3.up * 2.2f;
                marker.alignment = TextAlignmentOptions.Center;
                marker.fontSize = 3f;
                marker.rectTransform.sizeDelta = new Vector2(2, .6f);
                marker.gameObject.SetActive(false);
            }
            changed |= Set(npc, "policeMarker", marker);

            var audio = One<AudioSource>(obj, optional: true);
            if (audio == null)
            {
                audio = obj.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                audio.spatialBlend = 1f;
                audio.maxDistance = 32f;
            }
            return changed | Set(npc, "policeAudio", audio);
        }

        private static bool WireBowl(NetworkCarryable crate)
        {
            if (Read<GameObject>(crate, "bowlVisual") != null) return false;
            var root = NewChild(crate.transform, "Jail Bowl Visual").gameObject;
            var material = MaterialAsset("Jail_Bowl", new Color(.88f, .28f, .12f));
            Piece("Bowl Base", PrimitiveType.Cylinder, new Vector3(0, -.1f, 0), new Vector3(.67f, .03f, .67f));
            for (var i = 0; i < 12; i++)
            {
                var angle = i * Mathf.PI / 6f;
                var rim = Piece($"Bowl Rim {i + 1}", PrimitiveType.Cube,
                    new Vector3(Mathf.Sin(angle) * .32f, .01f, Mathf.Cos(angle) * .32f),
                    new Vector3(.18f, .21f, .065f));
                rim.localRotation = Quaternion.Euler(0, i * 30f, 0);
            }
            root.SetActive(false);
            Set(crate, "bowlVisual", root);
            Set(crate, "crateRenderer", One<Renderer>(crate.gameObject));
            Set(crate, "crateCollider", One<BoxCollider>(crate.gameObject));
            return true;

            Transform Piece(string name, PrimitiveType type, Vector3 position, Vector3 scale)
            {
                var piece = GameObject.CreatePrimitive(type);
                piece.name = name;
                Object.DestroyImmediate(One<Collider>(piece));
                One<Renderer>(piece).sharedMaterial = material;
                piece.transform.SetParent(root.transform, false);
                piece.transform.localPosition = position;
                piece.transform.localScale = scale;
                return piece.transform;
            }
        }

        private static bool WireFusionUI(FusionPreviewUI ui)
        {
            var session = Read<FusionSessionService>(ui, "session");
            var preview = Read<DogSelectionPreview>(ui, "selectionPreview");
            var obj = ui.gameObject;
            var changed = Set(ui, "controls", NamedText(obj, "Controls"));
            changed |= Set(ui, "networkSettings", NamedText(obj, "NetworkSettings"));
            if (session == null || session.ModelCatalog == null || preview == null
                || Read<DogFaceEditorController>(ui, "faceEditor") == null) return changed;
            var menu = Read<GameObject>(ui, "menu");
            var group = One<CanvasGroup>(menu, optional: true) ?? menu.AddComponent<CanvasGroup>();
            var front = One<DogFrontEnd>(obj, optional: true) ?? obj.AddComponent<DogFrontEnd>();
            changed |= Set(ui, "frontEnd", front);
            changed |= Set(ui, "legacyMenu", group);
            changed |= Set(front, "session", session);
            changed |= Set(front, "preview", preview);
            changed |= Set(front, "fps", One<FpsHud>(obj, optional: true));
            if (new SerializedObject(front).FindProperty("authoredPages").arraySize == 0)
            {
                EnsureMenuAssetFolder();
                front.BakeLayout(SaveMenuSprite, MenuFont);
                EditorUtility.SetDirty(front);
                changed = true;
            }
            return changed;
        }

        private static FusionRunnerRoot LoadRunnerPrefab(string name)
            => One<FusionRunnerRoot>(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Game/Prefabs/{name}.prefab"));

        private static void EnsureRunnerPrefab(string name, bool game)
        {
            var path = $"Assets/Game/Prefabs/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return;
            var root = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.None);
            try
            {
                var wiring = root.AddComponent<FusionRunnerRoot>();
                Set(wiring, "runner", root.AddComponent<NetworkRunner>());
                Set(wiring, "events", root.AddComponent<NetworkEvents>());
                if (game)
                {
                    Set(wiring, "sceneManager", root.AddComponent<NetworkSceneManagerDefault>());
                    Set(wiring, "objectProvider", root.AddComponent<NetworkObjectProviderDefault>());
                    Set(wiring, "faceService", root.AddComponent<CustomDogFaceNetworkService>());
                }
                PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);
                if (!saved) throw new InvalidOperationException($"Could not save {path}");
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Transform NewChild(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static Transform Child(Transform parent, string name)
            => Unique(parent.Cast<Transform>().Where(child => child.name == name), parent, name, true);

        private static TMP_Text NamedText(GameObject root, string name)
            => Unique(root.GetComponentsInChildren<TMP_Text>(true).Where(label => label.name == name), root, name, true);

        private static T One<T>(GameObject obj, bool optional = false) where T : Component
            => Unique(obj.GetComponents<T>(), obj, typeof(T).Name, optional);

        private static T Unique<T>(IEnumerable<T> candidates, Object context, string description, bool optional) where T : Object
        {
            var matches = candidates.Take(2).ToArray();
            if (matches.Length == 1) return matches[0];
            if (optional && matches.Length == 0) return null;
            throw new InvalidOperationException($"{context.name}: expected one {description}, found {(matches.Length == 0 ? "none" : "multiple")}.");
        }

        private static T Read<T>(Object target, string field) where T : Object
            => new SerializedObject(target).FindProperty(field).objectReferenceValue as T;

        private static bool Set(Object target, string field, Object value)
        {
            var data = new SerializedObject(target);
            var property = data.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name}.{field}");
            if (property.objectReferenceValue == value) return false;
            property.objectReferenceValue = value;
            return data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool SetArray<T>(Object target, string field, T[] values) where T : Object
        {
            var data = new SerializedObject(target);
            var property = data.FindProperty(field);
            if (property.arraySize != values.Length) property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
            {
                var item = property.GetArrayElementAtIndex(i);
                if (item.objectReferenceValue != values[i]) item.objectReferenceValue = values[i];
            }
            return data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
