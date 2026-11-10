using System;
using System.Collections.Generic;
using System.Linq;
using DogSimulator.Networking;
using Fusion;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace DogSimulator.Editor
{
    public static partial class SimplePolyCitySetup
    {
        private const string NavMeshPath = "Assets/Game/Config/SimplePolyCityNavMesh.asset";

        private static NavMeshSurface BakePedestrianNavMesh()
        {
            var surface = new GameObject("City Pedestrian Navigation").AddComponent<NavMeshSurface>();
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.collectObjects = CollectObjects.All;
            surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new InvalidOperationException("Could not bake the city NavMesh.");
            return surface;
        }

        private static void SavePedestrianNavMesh(NavMeshSurface surface)
        {
            var data = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshPath);
            if (data == null)
                AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
            else
            {
                EditorUtility.CopySerialized(surface.navMeshData, data);
                Object.DestroyImmediate(surface.navMeshData);
                surface.navMeshData = data;
                EditorUtility.SetDirty(data);
            }
            surface.AddData();
        }

        private static void AddPlayProps(List<SimplePolyCityWorld.Placement> props, List<Vector3> spawns,
            List<SimplePolyCityWorld.VehiclePlacement> vehicles, int maxPlayers)
        {
            var items = new[] { "Ball", "Bone", "Crate" };
            // Keep the original placement IDs so existing city slots restore by ID.
            foreach (var name in items)
            {
                if (spawns.Count <= maxPlayers) throw new InvalidOperationException("Missing play-prop positions.");
                var index = Mathf.Min(spawns.Count - 1, maxPlayers + 1);
                Add(name, "legacy-" + name.ToLowerInvariant(), spawns[index]);
                spawns.RemoveAt(index);
            }

            var stations = new[] { new Vector3(208, 0, 102), new Vector3(37, 0, 101), new Vector3(210, 0, 32) };
            for (var station = 0; station < stations.Length; station++)
            {
                var spots = FindOpenGround(stations[station], vehicles)
                    .Where(position => props.All(prop => (prop.position - position).sqrMagnitude > 2.2f))
                    .Take(items.Length).ToArray();
                if (spots.Length != items.Length) throw new InvalidOperationException($"Missing city play area {station}.");
                for (var i = 0; i < items.Length; i++)
                    Add(items[i], $"play-{station}-{items[i].ToLowerInvariant()}", spots[i]);
            }

            spawns.RemoveAll(position => props.Any(prop => (prop.position - position).sqrMagnitude < 1.2f));

            void Add(string name, string id, Vector3 position)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<NetworkObject>($"Assets/Game/Prefabs/Carryables/Carryable{name}.prefab");
                if (prefab == null) throw new InvalidOperationException($"Missing carryable {name}.");
                props.Add(new SimplePolyCityWorld.Placement
                {
                    id = id, prefab = prefab, position = position + Vector3.up * .5f, rotation = Quaternion.identity
                });
            }
        }

        private static IEnumerable<Vector3> FindOpenGround(Vector3 center, List<SimplePolyCityWorld.VehiclePlacement> vehicles)
        {
            var candidates = new List<Vector3>();
            for (var x = -8f; x <= 8f; x += 2f)
            for (var z = -8f; z <= 8f; z += 2f)
            {
                var sample = center + new Vector3(x, 1.3f, z);
                if (!Physics.Raycast(sample, Vector3.down, out var ground, 1.5f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || ground.normal.y < .9f)
                    continue;
                var position = ground.point + Vector3.up * .06f;
                if (Physics.CheckCapsule(position + Vector3.up * .42f, position + Vector3.up * 1.37f, .4f)) continue;
                if (vehicles.Any(vehicle => InsideVehicleFootprint(position, vehicle.placement))) continue;
                candidates.Add(position);
            }
            return candidates.OrderBy(position => (position - center).sqrMagnitude);
        }

        private static bool InsideVehicleFootprint(Vector3 position, SimplePolyCityWorld.Placement vehicle)
        {
            var hull = vehicle.prefab.GetComponent<BoxCollider>();
            var offset = Quaternion.Inverse(vehicle.rotation) * (position - vehicle.position) - hull.center;
            return Mathf.Abs(offset.x) <= hull.size.x * .5f + .65f
                && Mathf.Abs(offset.z) <= hull.size.z * .5f + .65f;
        }

        private static void WritePedestrians(SimplePolyCityWorld world, List<SimplePolyCityWorld.VehiclePlacement> vehicles)
        {
            var locations = new[]
            {
                new Vector3(212, 0, 107), new Vector3(41, 0, 96), new Vector3(132, 0, 132),
                new Vector3(208, 0, 33), new Vector3(22, 0, 53)
            };
            var prefab = CreatePedestrianPrefab();
            var data = new SerializedObject(world);
            var array = data.FindProperty("pedestrians");
            array.arraySize = locations.Length;
            for (var i = 0; i < locations.Length; i++)
            {
                WritePatrol(array.GetArrayElementAtIndex(i), prefab, FindPatrol(locations[i], vehicles));
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            AppendNeighborhoodPatrols(world);
        }

        private static void AppendNeighborhoodPatrols(SimplePolyCityWorld world)
        {
            var locations = new[]
            {
                new Vector3(88, 0, 137), new Vector3(107, 0, 146), new Vector3(141, 0, 135),
                new Vector3(167, 0, 144), new Vector3(190, 0, 122), new Vector3(113, 0, 167),
                new Vector3(156, 0, 171), new Vector3(193, 0, 167), new Vector3(75, 0, 93),
                new Vector3(153, 0, 57), new Vector3(74, 0, 185)
            };
            var vehicles = world.Vehicles.ToList();
            var prefab = world.Pedestrians[0].prefab;
            var routes = locations.Select(location => FindPatrol(location, vehicles)).ToArray();
            var data = new SerializedObject(world);
            var array = data.FindProperty("pedestrians");
            var first = array.arraySize;
            array.arraySize += routes.Length;
            for (var i = 0; i < routes.Length; i++)
                WritePatrol(array.GetArrayElementAtIndex(first + i), prefab, routes[i]);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WritePatrol(SerializedProperty entry, NetworkObject prefab, (Vector3 start, Vector3 end) route)
        {
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("start").vector3Value = route.start;
            entry.FindPropertyRelative("end").vector3Value = route.end;
        }

        private static (Vector3 start, Vector3 end) FindPatrol(Vector3 center,
            List<SimplePolyCityWorld.VehiclePlacement> vehicles)
        {
            var directions = new[] { Vector3.right, Vector3.forward, Vector3.left, Vector3.back };
            foreach (var candidate in FindOpenGround(center, vehicles))
            {
                if (!NavMesh.SamplePosition(candidate, out var start, .55f, NavMesh.AllAreas)) continue;
                foreach (var direction in directions)
                {
                    if (!NavMesh.SamplePosition(start.position + direction * 6f, out var end, .55f, NavMesh.AllAreas)
                        || NavMesh.Raycast(start.position, end.position, out _, NavMesh.AllAreas))
                        continue;
                    var path = Enumerable.Range(0, 9).Select(sample => Vector3.Lerp(start.position, end.position, sample / 8f));
                    if (path.Any(point => vehicles.Any(vehicle => InsideVehicleFootprint(point, vehicle.placement)))) continue;
                    return (start.position, end.position);
                }
            }
            throw new InvalidOperationException($"Could not place a pedestrian route near {center}.");
        }

        private static NetworkObject CreatePedestrianPrefab()
        {
            var root = new GameObject("CityPedestrian");
            try
            {
                var body = root.AddComponent<CapsuleCollider>();
                body.center = Vector3.up * .91f;
                body.height = 1.82f;
                body.radius = .32f;
                var agent = root.AddComponent<NavMeshAgent>();
                agent.height = body.height;
                agent.radius = body.radius;
                agent.speed = 1.7f;
                agent.stoppingDistance = .2f;
                agent.angularSpeed = 540;
                agent.enabled = false;
                root.AddComponent<NetworkObject>();
                root.AddComponent<NetworkTransform>();
                var npc = new SerializedObject(root.AddComponent<NetworkCityPedestrian>());

                var skin = PedestrianMaterial("Skin", new Color(.82f, .55f, .37f));
                var coat = PedestrianMaterial("Jacket", new Color(.12f, .45f, .59f));
                var trim = PedestrianMaterial("Trim", new Color(.95f, .59f, .17f));
                var pants = PedestrianMaterial("Trousers", new Color(.21f, .24f, .32f));
                var hair = PedestrianMaterial("Hair", new Color(.17f, .105f, .075f));

                var torso = Pivot(root.transform, "Torso", new Vector3(0, .87f, 0));
                Part(torso, "Jacket", PrimitiveType.Cube, new Vector3(0, .27f, 0), new Vector3(.49f, .55f, .3f), coat);
                Part(torso, "Collar", PrimitiveType.Cube, new Vector3(0, .52f, .16f), new Vector3(.18f, .095f, .035f), trim);
                Part(torso, "Head", PrimitiveType.Cube, new Vector3(0, .71f, .015f), new Vector3(.31f, .32f, .28f), skin);
                Part(torso, "Hair", PrimitiveType.Cube, new Vector3(0, .86f, -.005f), new Vector3(.33f, .1f, .3f), hair);
                foreach (var side in new[] { -1, 1 })
                {
                    Part(torso, "Eye", PrimitiveType.Cube, new Vector3(side * .07f, .735f, .159f), new Vector3(.036f, .04f, .02f), hair);
                    var left = side < 0;
                    var arm = Pivot(torso, left ? "Left Arm" : "Right Arm", new Vector3(side * .27f, .48f, 0));
                    Part(arm, "Sleeve", PrimitiveType.Cube, new Vector3(0, -.18f, 0), new Vector3(.14f, .42f, .16f), coat);
                    Part(arm, "Hand", PrimitiveType.Cube, new Vector3(0, -.42f, 0), new Vector3(.13f, .14f, .14f), skin);
                    var leg = Pivot(root.transform, left ? "Left Leg" : "Right Leg", new Vector3(side * .125f, .92f, 0));
                    Part(leg, "Trouser", PrimitiveType.Cube, new Vector3(0, -.39f, 0), new Vector3(.19f, .77f, .22f), pants);
                    Part(leg, "Shoe", PrimitiveType.Cube, new Vector3(0, -.82f, .05f), new Vector3(.2f, .17f, .32f), hair);
                    npc.FindProperty(left ? "leftArm" : "rightArm").objectReferenceValue = arm;
                    npc.FindProperty(left ? "leftLeg" : "rightLeg").objectReferenceValue = leg;
                }
                npc.FindProperty("torso").objectReferenceValue = torso;
                npc.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/CityPedestrian.prefab").GetComponent<NetworkObject>();
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Transform Pivot(Transform parent, string name, Vector3 position)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = position;
            return pivot;
        }

        private static void Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static Material PedestrianMaterial(string name, Color color)
        {
            var path = $"{PrefabFolder}/Pedestrian{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = color;
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }
    }
}
