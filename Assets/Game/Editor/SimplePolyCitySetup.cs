using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DogSimulator.Networking;
using DogSimulator.Player;
using Fusion;
using Fusion.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DogSimulator.Editor
{
    public static partial class SimplePolyCitySetup
    {
        public const string ScenePath = "Assets/Game/Scenes/SimplePolyCity_Gameplay.unity";
        private const string Pack = "Assets/SimplePoly City - Low Poly Assets";
        private const string DemoPath = Pack + "/Demo/SimplePoly City - Low Poly Assets_Demo Scene.unity";
        private const string Taxi = Pack + "/Prefab/Vehicles/Vehicle with Separated Wheels/Vehicle_Taxi_separate.prefab";
        private const string PrefabFolder = "Assets/Game/Prefabs/SimplePolyCity";
        private const string ConfigPath = "Assets/Game/Config/SimplePolyCityNetworkConfig.asset";

        [MenuItem("Tools/Dog Simulator/Open SimplePoly City Gameplay")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before generating the city.");
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes before generating the city.");

            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/Game/Prefabs", "SimplePolyCity");
            var material = CreateVehicleMaterial();
            var scene = EditorSceneManager.OpenScene(DemoPath);
            var originals = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(t => PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject))
                .Select(t => (root: t.gameObject, path: PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)))
                .ToArray();

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var demoCamera in root.GetComponentsInChildren<Camera>(true))
                {
                    demoCamera.enabled = false;
                    if (demoCamera.CompareTag("MainCamera")) demoCamera.tag = "Untagged";
                }
                foreach (var listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
                foreach (var terrain in root.GetComponentsInChildren<Terrain>(true))
                {
                    var collider = terrain.GetComponent<TerrainCollider>() ?? terrain.gameObject.AddComponent<TerrainCollider>();
                    collider.terrainData = terrain.terrainData;
                }
            }

            var route = CreateRoute(false);
            var inbound = CreateRoute(true);
            var props = new List<SimplePolyCityWorld.Placement>();
            var vehicles = new List<SimplePolyCityWorld.VehiclePlacement>();
            var garageMoves = new List<(int index, string name, Bounds hull)>();
            var prefabs = new Dictionary<string, NetworkObject>();
            var trafficStarts = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["Vehicle_Car_color02 (3)"] = 0,
                ["Vehicle_Car_color03"] = route.Length / 2,
                ["Vehicle_Pick up Truck_color03"] = route.Length * 3 / 4,
                ["Vehicle_Car_color02 (2)"] = inbound.Length,
                ["Vehicle_Car_color01 (2)"] = inbound.Length + inbound.Length / 2,
                ["Vehicle_Car_color01 (1)"] = inbound.Length + inbound.Length * 3 / 4
            };
            foreach (var (root, path) in originals.OrderBy(entry => StableId(entry.root), StringComparer.Ordinal))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                var vehicle = path.Contains("/Vehicles/");
                var mass = LooseMass(name);
                if (!vehicle && mass <= 0)
                {
                    AddStaticColliders(root, name, path);
                    continue;
                }

                var separatedTaxi = root.name is "Vehicle_Taxi (9)" or "Vehicle_Taxi (8)";
                var key = separatedTaxi ? "DriveableTaxi" : name;
                if (!prefabs.TryGetValue(key, out var prefab))
                {
                    prefab = CreatePrefab(key, separatedTaxi ? Taxi : path, material, vehicle, mass);
                    prefabs.Add(key, prefab);
                }
                var placement = new SimplePolyCityWorld.Placement
                {
                    id = StableId(root),
                    prefab = prefab,
                    position = root.transform.position,
                    rotation = root.transform.rotation
                };
                if (Vector3.Distance(root.transform.lossyScale, Vector3.one) > .005f)
                {
                    key += "_" + placement.id.Replace('-', '_');
                    placement.prefab = CreatePrefab(key, path, material, vehicle, mass, root.transform.lossyScale);
                }
                if (!vehicle) props.Add(placement);
                else
                {
                    var box = placement.prefab.GetComponent<BoxCollider>();
                    var hull = new Bounds(box.center, box.size);
                    var blocksLane = BlocksRoute(placement, hull, route) || BlocksRoute(placement, hull, inbound);
                    if (trafficStarts.TryGetValue(root.name, out var start))
                    {
                        var lane = start >= route.Length ? inbound : route;
                        var loop = Enumerable.Range(0, lane.Length).Select(i => lane[(start + i) % lane.Length]).ToArray();
                        placement.position = loop[0].position;
                        placement.rotation = Quaternion.LookRotation(loop[1].position - loop[0].position);
                        vehicles.Add(new SimplePolyCityWorld.VehiclePlacement { placement = placement, route = loop });
                        Debug.Log($"City traffic {root.name} at {placement.position}");
                    }
                    else
                    {
                        if (blocksLane) garageMoves.Add((vehicles.Count, root.name, hull));
                        vehicles.Add(new SimplePolyCityWorld.VehiclePlacement { placement = placement });
                    }
                }
                root.SetActive(false);
            }

            Physics.SyncTransforms();
            MoveLaneBlockers(vehicles, garageMoves);
            var preview = EditorSceneManager.OpenScene(FusionPreviewSetup.ScenePath, OpenSceneMode.Additive);
            try
            {
                var runtime = preview.GetRootGameObjects().Where(root => root.name is "Main Camera" or "EventSystem"
                    or "Preview UI" or "LocalSpawnPoint" or "Fusion Session" or "Dog Preview Rig" or "Custom Dog Face Web Bridge").ToArray();
                if (runtime.Length != 7) throw new InvalidOperationException("Fusion Preview gameplay roots changed.");
                foreach (var root in runtime) SceneManager.MoveGameObjectToScene(root, scene);
                SceneManager.SetActiveScene(scene);
            }
            finally { EditorSceneManager.CloseScene(preview, true); }

            var session = scene.GetRootGameObjects().Single(root => root.name == "Fusion Session")
                .GetComponent<FusionSessionService>();
            var serialized = new SerializedObject(session);
            var sourceConfig = (NetworkConfig)serialized.FindProperty("config").objectReferenceValue;
            var cityConfig = AssetDatabase.LoadAssetAtPath<NetworkConfig>(ConfigPath);
            if (cityConfig == null)
            {
                cityConfig = Object.Instantiate(sourceConfig);
                AssetDatabase.CreateAsset(cityConfig, ConfigPath);
            }
            else EditorUtility.CopySerialized(sourceConfig, cityConfig);
            var config = new SerializedObject(cityConfig);
            config.FindProperty("gameVersion").stringValue = SimplePolyCityWorld.MapVersion;
            config.ApplyModifiedPropertiesWithoutUndo();

            var city = session.gameObject.AddComponent<SimplePolyCityWorld>();
            var spawns = SafeSpawns(session, vehicles).ToList();
            AddPlayProps(props, spawns, vehicles, cityConfig.MaxPlayers);
            if (spawns.Count < cityConfig.MaxPlayers)
                throw new InvalidOperationException($"Safe plaza has only {spawns.Count} valid dog spawns.");
            var spawn = (Transform)serialized.FindProperty("spawnPoint").objectReferenceValue;
            spawn.SetPositionAndRotation(spawns[0], Quaternion.Euler(0, 90, 0));
            var camera = (DogCamera)serialized.FindProperty("followCamera").objectReferenceValue;
            camera.transform.SetPositionAndRotation(spawn.position + new Vector3(-6, 5, -8), Quaternion.Euler(22, 34, 0));
            serialized.FindProperty("config").objectReferenceValue = cityConfig;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            WriteWorld(city, props, vehicles, spawns.ToArray());
            var navigation = BakePedestrianNavMesh();
            try { WritePedestrians(city, vehicles); }
            finally { navigation.RemoveData(); }
            SavePedestrianNavMesh(navigation);
            var mission = new SerializedObject(city);
            mission.FindProperty("goldenBonePrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<NetworkObject>("Assets/Game/Prefabs/Carryables/CarryableBone.prefab");
            mission.FindProperty("missionCratePrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<NetworkObject>("Assets/Game/Prefabs/Carryables/CarryableCrate.prefab");
            mission.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Could not save the generated gameplay scene.");
            if (!EditorBuildSettings.scenes.Any(entry => entry.path == ScenePath))
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Append(new EditorBuildSettingsScene(ScenePath, true)).ToArray();
            AssetDatabase.SaveAssets();
            NetworkProjectConfigUtilities.RebuildPrefabTable();
            Debug.Log($"City generated with {originals.Length} demo instances, {props.Count} loose props, {vehicles.Count} vehicles and {spawns.Count} safe spawns.");
        }

        private static void AddStaticColliders(GameObject root, string name, string path)
        {
            if (name.StartsWith("Road Split Line", StringComparison.Ordinal)) return;
            if (name.Contains("Tree"))
            {
                var trunk = root.AddComponent<CapsuleCollider>();
                trunk.center = new Vector3(0, 1.6f, 0);
                trunk.height = 3.2f;
                trunk.radius = .32f;
                return;
            }

            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null) continue;
                if (path.Contains("/Roads/") && filter.name.Contains("Line", StringComparison.OrdinalIgnoreCase)) continue;
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
            }
        }

        private static float LooseMass(string name) => name switch
        {
            "Props_Traffic cone" => .8f,
            "Props_Coffee shop chair" => 2.5f,
            "Props_Dustbin" => 4f,
            "Props_Traffic Control Barrier Fence" => 5f,
            "Props_Bench_1" or "Props_Bench_2" => 12f,
            "Natures_Pot Bush_small" => 3f,
            "Natures_Pot Bush_big" => 6f,
            _ => 0f
        };

        private static string StableId(GameObject root)
        {
            var id = GlobalObjectId.GetGlobalObjectIdSlow(root);
            return $"{id.targetObjectId}-{id.targetPrefabId}";
        }

        private static PhysicsMaterial CreateVehicleMaterial()
        {
            var path = PrefabFolder + "/VehicleGrip.physicMaterial";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (material != null) return material;
            material = new PhysicsMaterial("VehicleGrip")
            {
                dynamicFriction = .16f, staticFriction = .16f, frictionCombine = PhysicsMaterialCombine.Minimum
            };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static NetworkObject CreatePrefab(string name, string path, PhysicsMaterial material,
            bool vehicle, float mass, Vector3? scale = null)
        {
            var root = new GameObject(name);
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), root.transform);
                visual.name = "Visual";
                visual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                visual.transform.localScale = scale ?? Vector3.one;
                var bounds = default(Bounds);
                var first = true;
                foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>())
                {
                    var world = renderer.bounds;
                    if (first) { bounds = world; first = false; }
                    else bounds.Encapsulate(world);
                }
                if (first) throw new InvalidOperationException($"No renderer in {path}");
                var box = root.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = bounds.size * .96f;
                var body = root.AddComponent<Rigidbody>();
                body.mass = vehicle ? 1.5f * Mathf.Clamp(bounds.size.z * 750f, 3000f, 9500f) : mass;
                root.AddComponent<NetworkObject>();
                root.AddComponent<NetworkTransform>();
                if (!vehicle)
                {
                    var carryable = new SerializedObject(root.AddComponent<NetworkCarryable>());
                    carryable.FindProperty("dropClearance").objectReferenceValue = box;
                    carryable.FindProperty("carriedLocalPosition").vector3Value
                        = new Vector3(0, -.2f - bounds.center.y, .22f + bounds.extents.z);
                    carryable.ApplyModifiedPropertiesWithoutUndo();
                }
                else
                {
                    box.sharedMaterial = material;
                    box.size -= new Vector3(0, .30f, 0);
                    box.center += Vector3.up * .15f;
                    body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                    body.linearDamping = .06f;
                    body.sleepThreshold = .08f;
                    body.isKinematic = true;
                    const float wheelRadius = .38f;
                    foreach (var x in new[] { -.72f, .72f })
                    foreach (var z in new[] { -.68f, .68f })
                    {
                        var wheel = root.AddComponent<SphereCollider>();
                        wheel.radius = wheelRadius;
                        wheel.center = new Vector3(bounds.center.x + bounds.extents.x * x, bounds.min.y + wheelRadius,
                            bounds.center.z + bounds.extents.z * z);
                        wheel.sharedMaterial = material;
                    }
                    var component = new SerializedObject(root.AddComponent<NetworkCityVehicle>());
                    var cabinZ = bounds.size.z > 7.5f ? bounds.max.z - 1.45f : .35f;
                    Anchor("seat", "Seat", new Vector3(bounds.center.x - .5f, bounds.min.y + bounds.size.y * .43f, cabinZ));
                    Anchor("cameraTarget", "Vehicle Camera Target", new Vector3(bounds.center.x, bounds.max.y - .6f, bounds.center.z));
                    Anchor("door", "Driver Door", new Vector3(bounds.min.x - .22f, .55f, cabinZ));
                    Anchor("leftExit", "Left Exit", new Vector3(bounds.min.x - .95f, .2f, cabinZ));
                    Anchor("rightExit", "Right Exit", new Vector3(bounds.max.x + .95f, .2f, cabinZ));
                    component.ApplyModifiedPropertiesWithoutUndo();
                    var obstacle = root.AddComponent<NavMeshObstacle>();
                    obstacle.shape = NavMeshObstacleShape.Box;
                    obstacle.center = box.center;
                    obstacle.size = box.size;
                    obstacle.enabled = false;

                    void Anchor(string field, string label, Vector3 position)
                    {
                        var point = new GameObject(label).transform;
                        point.SetParent(root.transform, false);
                        point.localPosition = position;
                        component.FindProperty(field).objectReferenceValue = point;
                    }
                }
                return PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/{name}.prefab").GetComponent<NetworkObject>();
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static SimplePolyCityWorld.RoutePoint[] CreateRoute(bool inbound)
        {
            var min = inbound ? 3.3f : -3.3f;
            var max = 60f - min;
            var corners = new[] { new Vector3(max, 0, min), new Vector3(max, 0, max),
                new Vector3(min, 0, max), new Vector3(min, 0, min) };
            if (inbound) Array.Reverse(corners);
            var points = new List<SimplePolyCityWorld.RoutePoint>();
            for (var i = 0; i < corners.Length; i++)
            {
                var corner = corners[i];
                var last = corners[(i + 3) % 4];
                var next = corners[(i + 1) % 4];
                var before = corner + (last - corner).normalized * 7f;
                var after = corner + (next - corner).normalized * 7f;
                points.Add(new SimplePolyCityWorld.RoutePoint { position = (last + corner) * .5f, speed = 12f });
                points.Add(new SimplePolyCityWorld.RoutePoint { position = before, speed = 5f });
                for (var step = 1; step <= 3; step++)
                {
                    var t = step / 3f;
                    var point = Vector3.Lerp(Vector3.Lerp(before, corner, t), Vector3.Lerp(corner, after, t), t);
                    points.Add(new SimplePolyCityWorld.RoutePoint { position = point, speed = 5f });
                }
            }
            return points.ToArray();
        }

        private static bool BlocksRoute(SimplePolyCityWorld.Placement pose, Bounds hull, SimplePolyCityWorld.RoutePoint[] route)
        {
            var inverse = Quaternion.Inverse(pose.rotation);
            var protectedHull = hull;
            protectedHull.Expand(new Vector3(3.6f, 4f, 3.6f));
            for (var i = 0; i < route.Length; i++)
            {
                var start = route[i].position;
                var end = route[(i + 1) % route.Length].position;
                var samples = Mathf.CeilToInt(Vector3.Distance(start, end));
                for (var sample = 0; sample <= samples; sample++)
                    if (protectedHull.Contains(inverse * (Vector3.Lerp(start, end, sample / (float)samples) - pose.position)))
                        return true;
            }
            return false;
        }

        private static void MoveLaneBlockers(List<SimplePolyCityWorld.VehiclePlacement> vehicles,
            List<(int index, string name, Bounds hull)> moves)
        {
            var blocked = new HashSet<int>(moves.Select(move => move.index));
            var occupied = vehicles.Where((_, index) => !blocked.Contains(index))
                .Select(v => v.placement.position).ToList();
            foreach (var (index, name, hull) in moves)
            {
                var entry = vehicles[index];
                var original = entry.placement.position;
                var found = false;
                // Reuse the concrete forecourt beside the west approach to the z=120 junction.
                for (var x = 13f; x <= 45f && !found; x += 4f)
                for (var z = 84f; z <= 108f && !found; z += 4f)
                {
                    if (!Physics.Raycast(new Vector3(x, 1.2f, z), Vector3.down, out var ground, 1.5f)
                        || ground.normal.y < .9f) continue;
                    var position = ground.point + Vector3.up * .03f;
                    if (occupied.Any(other => Vector3.Distance(other, position) < (hull.size.z > 6f ? 8f : 6f))) continue;
                    var rotation = Quaternion.Euler(0, 90, 0);
                    if (Physics.CheckBox(position + rotation * hull.center, hull.extents + Vector3.one * .14f, rotation,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    entry.placement.position = position;
                    entry.placement.rotation = rotation;
                    vehicles[index] = entry;
                    occupied.Add(position);
                    found = true;
                    Debug.Log($"City parking {name}: {original} -> {position}");
                }
                if (!found) throw new InvalidOperationException($"Could not move {name} out of the test route.");
            }
        }

        private static IEnumerable<Vector3> SafeSpawns(FusionSessionService session, List<SimplePolyCityWorld.VehiclePlacement> vehicles)
        {
            var prefab = new SerializedObject(session).FindProperty("playerPrefab").objectReferenceValue as NetworkObject;
            var controller = prefab.GetComponent<CharacterController>();
            var axis = Vector3.up * (controller.height * .5f - controller.radius);
            // The paved parking courtyard at (212.5, 85–100) has a taxi and several exits to the sidewalk.
            var candidates = new List<Vector3>();
            for (var z = 79f; z <= 107f; z += 3f)
            for (var x = 200f; x <= 228f; x += 3f)
            {
                if (!Physics.Raycast(new Vector3(x, 1.25f, z), Vector3.down, out var ground, 1.5f)
                    || ground.normal.y < .9f) continue;
                var position = ground.point + Vector3.up * .06f;
                var center = position + controller.center;
                if (Physics.CheckCapsule(center - axis, center + axis, controller.radius + .04f)) continue;
                if (vehicles.Any(v => Vector3.Distance(v.placement.position, position) < 4.5f)) continue;
                candidates.Add(position);
            }
            return candidates.OrderBy(position => (position - new Vector3(209, 0, 101)).sqrMagnitude);
        }

        private static void WriteWorld(SimplePolyCityWorld world, List<SimplePolyCityWorld.Placement> props,
            List<SimplePolyCityWorld.VehiclePlacement> vehicles, Vector3[] spawns)
        {
            var serialized = new SerializedObject(world);
            var propArray = serialized.FindProperty("props");
            propArray.arraySize = props.Count;
            for (var i = 0; i < props.Count; i++) WritePlacement(propArray.GetArrayElementAtIndex(i), props[i]);
            var vehicleArray = serialized.FindProperty("vehicles");
            vehicleArray.arraySize = vehicles.Count;
            for (var i = 0; i < vehicles.Count; i++)
            {
                var element = vehicleArray.GetArrayElementAtIndex(i);
                WritePlacement(element.FindPropertyRelative("placement"), vehicles[i].placement);
                var route = vehicles[i].route ?? Array.Empty<SimplePolyCityWorld.RoutePoint>();
                var routeArray = element.FindPropertyRelative("route");
                routeArray.arraySize = route.Length;
                for (var j = 0; j < route.Length; j++)
                {
                    var point = routeArray.GetArrayElementAtIndex(j);
                    point.FindPropertyRelative("position").vector3Value = route[j].position;
                    point.FindPropertyRelative("speed").floatValue = route[j].speed;
                }
            }
            var spawnArray = serialized.FindProperty("safeSpawns");
            spawnArray.arraySize = spawns.Length;
            for (var i = 0; i < spawns.Length; i++) spawnArray.GetArrayElementAtIndex(i).vector3Value = spawns[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WritePlacement(SerializedProperty serialized, SimplePolyCityWorld.Placement placement)
        {
            serialized.FindPropertyRelative("id").stringValue = placement.id;
            serialized.FindPropertyRelative("prefab").objectReferenceValue = placement.prefab;
            serialized.FindPropertyRelative("position").vector3Value = placement.position;
            serialized.FindPropertyRelative("rotation").quaternionValue = placement.rotation;
        }
    }
}
