using System;
using System.Linq;
using DogSimulator.Networking;
using DogSimulator.Player;
using Fusion;
using Fusion.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace DogSimulator.Editor
{
    public static class M3SandboxSetup
    {
        public const string RegistryPath = "Assets/Game/Prefabs/SandboxInteractionRegistry.prefab";
        public const string BonePath = "Assets/Game/Prefabs/Carryables/CarryableBone.prefab";
        public const string BallPath = "Assets/Game/Prefabs/Carryables/CarryableBall.prefab";
        public const string CratePath = "Assets/Game/Prefabs/Carryables/CarryableCrate.prefab";
        public const string ProjectilePath = "Assets/Game/Prefabs/KnockbackBall.prefab";

        [InitializeOnLoadMethod]
        private static void QueueLegacyGunCleanup()
        {
            EditorApplication.delayCall -= CleanupLegacyGun;
            EditorApplication.delayCall += CleanupLegacyGun;
        }

        private static void CleanupLegacyGun()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
                || EditorApplication.isUpdating) return;
            var changed = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath) != null
                && AssetDatabase.DeleteAsset(ProjectilePath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FusionPreviewSetup.PrefabPath);
            if (prefab != null)
            {
                var root = PrefabUtility.LoadPrefabContents(FusionPreviewSetup.PrefabPath);
                try
                {
                    var gun = root.transform.Find("KnockbackGun");
                    if (gun != null)
                    {
                        Object.DestroyImmediate(gun.gameObject);
                        PrefabUtility.SaveAsPrefabAsset(root, FusionPreviewSetup.PrefabPath);
                        changed = true;
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var config = AssetDatabase.LoadAssetAtPath<NetworkConfig>(FusionPreviewSetup.ConfigPath);
            if (config != null)
            {
                var serialized = new SerializedObject(config);
                var version = serialized.FindProperty("gameVersion");
                if (version != null && version.stringValue != "office-m3-knockback-v2")
                {
                    version.stringValue = "office-m3-knockback-v2";
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
            }
            if (!changed) return;
            AssetDatabase.SaveAssets();
            NetworkProjectConfigUtilities.RebuildPrefabTable();
            Debug.Log("Removed legacy knockback gun/projectile assets and upgraded throw controls.");
        }

        [MenuItem("Tools/Dog Simulator/Apply M3-A + M3-B Sandbox")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before applying the sandbox upgrade.");
            AssetDatabase.StartAssetEditing();
            try
            {
                EnsureFolder("Assets/Game/Prefabs/Carryables");
                var bone = CreateCarryable(BonePath, "Bone", PrimitiveType.Capsule, new Vector3(.22f, .55f, .22f),
                    new Color(.92f, .82f, .58f), false, new Vector3(90, 0, 0));
                var ball = CreateCarryable(BallPath, "Ball", PrimitiveType.Sphere, Vector3.one * .55f,
                    new Color(.95f, .22f, .15f), true, Vector3.zero);
                var crate = CreateCarryable(CratePath, "Crate", PrimitiveType.Cube, new Vector3(1.45f, 1.2f, 1.45f),
                    new Color(.58f, .32f, .13f), false, Vector3.zero);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePath) != null) AssetDatabase.DeleteAsset(ProjectilePath);
                var registry = CreateRegistry(new[] { bone, ball, crate, crate, crate });
                UpgradePlayerPrefab();
                UpgradeScene(registry);
                UpgradeVersion();
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.SaveAssets();
            NetworkProjectConfigUtilities.RebuildPrefabTable();
            Debug.Log("M3-A carryable yard and M3-B dog stacking setup applied.");
        }

        private static GameObject CreateCarryable(string path, string name, PrimitiveType primitive, Vector3 scale,
            Color color, bool rolls, Vector3 euler)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) AssetDatabase.DeleteAsset(path);
            var materialPath = path.Replace(".prefab", ".mat");
            if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) != null) AssetDatabase.DeleteAsset(materialPath);
            var root = GameObject.CreatePrimitive(primitive);
            root.name = name;
            root.transform.localScale = scale;
            root.transform.rotation = Quaternion.Euler(euler);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                name = name + " Material",
                color = color
            };
            AssetDatabase.CreateAsset(material, materialPath);
            root.GetComponent<Renderer>().sharedMaterial = material;
            var body = root.AddComponent<Rigidbody>();
            body.mass = rolls ? 1 : 3;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var networkObject = root.AddComponent<NetworkObject>();
            networkObject.Flags |= NetworkObjectFlags.MasterClientObject;
            networkObject.Flags &= ~(NetworkObjectFlags.DestroyWhenStateAuthorityLeaves | NetworkObjectFlags.AllowStateAuthorityOverride);
            root.AddComponent<NetworkTransform>();
            var carryable = root.AddComponent<NetworkCarryable>();
            var serialized = new SerializedObject(carryable);
            serialized.FindProperty("rollsWhenFree").boolValue = rolls;
            serialized.FindProperty("carriedLocalPosition").vector3Value = name == "Crate"
                ? new Vector3(0, -.25f, .8f) : new Vector3(0, 0, .22f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            MarkNetworkPrefab(prefab);
            return prefab;
        }

        private static GameObject CreateRegistry(GameObject[] prefabs)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RegistryPath) != null) AssetDatabase.DeleteAsset(RegistryPath);
            var root = new GameObject("Sandbox Interaction Registry");
            var networkObject = root.AddComponent<NetworkObject>();
            networkObject.Flags |= NetworkObjectFlags.MasterClientObject;
            networkObject.Flags &= ~(NetworkObjectFlags.DestroyWhenStateAuthorityLeaves | NetworkObjectFlags.AllowStateAuthorityOverride);
            var registry = root.AddComponent<SandboxInteractionRegistry>();
            var serialized = new SerializedObject(registry);
            var prefabArray = serialized.FindProperty("carryablePrefabs");
            var positions = serialized.FindProperty("spawnPositions");
            var spawnPoints = new[]
            {
                new Vector3(-4, .45f, 3), new Vector3(0, .6f, 2),
                new Vector3(4, .65f, 3), new Vector3(6, .65f, 1), new Vector3(5, .65f, -2)
            };
            prefabArray.arraySize = positions.arraySize = prefabs.Length;
            for (var i = 0; i < prefabs.Length; i++)
            {
                prefabArray.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
                positions.GetArrayElementAtIndex(i).vector3Value = spawnPoints[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, RegistryPath);
            Object.DestroyImmediate(root);
            MarkNetworkPrefab(prefab);
            return prefab;
        }

        private static void UpgradePlayerPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(FusionPreviewSetup.PrefabPath);
            try
            {
                var existing = root.transform.Find("BackSupport");
                var supportRoot = existing != null ? existing.gameObject : new GameObject("BackSupport");
                supportRoot.transform.SetParent(root.transform, false);
                supportRoot.transform.localPosition = new Vector3(0, .94f, -.08f);
                var collider = supportRoot.GetComponent<BoxCollider>();
                if (collider == null) collider = supportRoot.AddComponent<BoxCollider>();
                collider.isTrigger = false;
                collider.center = Vector3.zero;
                collider.size = new Vector3(.9f, .12f, 1.15f);
                var support = supportRoot.GetComponent<DogBackSupport>();
                if (support == null) support = supportRoot.AddComponent<DogBackSupport>();
                var serialized = new SerializedObject(support);
                serialized.FindProperty("ownerRoot").objectReferenceValue = root.transform;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var bodyHitbox = root.transform.Find("BodyHitbox");
                if (bodyHitbox == null)
                {
                    bodyHitbox = new GameObject("BodyHitbox").transform;
                    bodyHitbox.SetParent(root.transform, false);
                }
                var capsule = bodyHitbox.GetComponent<CapsuleCollider>();
                if (capsule == null) capsule = bodyHitbox.gameObject.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0, .55f, 0);
                capsule.height = 1.05f;
                capsule.radius = .32f;
                var gun = root.transform.Find("KnockbackGun");
                if (gun != null) Object.DestroyImmediate(gun.gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, FusionPreviewSetup.PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void UpgradeScene(GameObject registryPrefab)
        {
            var scene = EditorSceneManager.OpenScene(FusionPreviewSetup.ScenePath);
            var session = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<FusionSessionService>(true)).Single();
            Reference(session, "sandboxRegistryPrefab", registryPrefab);
            foreach (var oldName in new[] { "TestArenaFloor", "NorthWall", "SouthWall", "EastWall", "WestWall" })
            {
                var old = FindTransform(scene, oldName);
                if (old != null) old.gameObject.SetActive(false);
            }
            var oldRoot = FindTransform(scene, "M3 Sandbox Yard");
            if (oldRoot != null) Object.DestroyImmediate(oldRoot.gameObject);
            var yard = new GameObject("M3 Sandbox Yard").transform;
            CreateBlock("Yard Ground", yard, new Vector3(0, -.5f, 0), new Vector3(50, 1, 50), new Color(.22f, .46f, .19f));
            CreateBlock("North Fence", yard, new Vector3(0, 1.5f, 25), new Vector3(50, 3, .5f), new Color(.32f, .25f, .18f));
            CreateBlock("South Fence", yard, new Vector3(0, 1.5f, -25), new Vector3(50, 3, .5f), new Color(.32f, .25f, .18f));
            CreateBlock("East Fence", yard, new Vector3(25, 1.5f, 0), new Vector3(.5f, 3, 50), new Color(.32f, .25f, .18f));
            CreateBlock("West Fence", yard, new Vector3(-25, 1.5f, 0), new Vector3(.5f, 3, 50), new Color(.32f, .25f, .18f));
            CreateBlock("Low Platform", yard, new Vector3(-6, .6f, -5), new Vector3(5, 1.2f, 5), new Color(.48f, .48f, .5f));
            CreateBlock("Coop Ledge", yard, new Vector3(-10, 2.5f, 7), new Vector3(6, 1, 5), new Color(.62f, .55f, .38f));
            CreateBlock("Ramp", yard, new Vector3(7, .65f, -7), new Vector3(7, .5f, 4), new Color(.5f, .5f, .52f), new Vector3(0, 0, 12));
            var bouncePad = CreateBlock("Bounce Pad", yard, new Vector3(8, .2f, 4),
                new Vector3(3, .4f, 3), new Color(.08f, .75f, .9f));
            bouncePad.AddComponent<BouncePad>();
            CreateBlock("Ball Goal Left", yard, new Vector3(11, .75f, 9), new Vector3(.4f, 1.5f, 5), Color.white);
            CreateBlock("Ball Goal Right", yard, new Vector3(17, .75f, 9), new Vector3(.4f, 1.5f, 5), Color.white);
            var stage = FindText(scene, "Stage");
            if (stage != null) stage.text = "M3 SANDBOX / GRAB + OBJECTS + DOG STACKING";
            var status = FindText(scene, "Status");
            if (status != null) status.text = "E grab/drop gently | Left Click throw held item | R reset | bounce pad";
            var message = FindText(scene, "Message");
            if (message != null) message.text = "Small multiplayer yard: objects inherit release momentum; cyan pad launches dogs.";
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void UpgradeVersion()
        {
            var config = AssetDatabase.LoadAssetAtPath<NetworkConfig>(FusionPreviewSetup.ConfigPath);
            var serialized = new SerializedObject(config);
            serialized.FindProperty("gameVersion").stringValue = "office-m3-knockback-v2";
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateBlock(string name, Transform parent, Vector3 position, Vector3 scale, Color color,
            Vector3? euler = null)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent);
            block.transform.position = position;
            block.transform.localScale = scale;
            if (euler.HasValue) block.transform.rotation = Quaternion.Euler(euler.Value);
            block.GetComponent<Renderer>().sharedMaterial = GetBlockMaterial(color);
            block.isStatic = true;
            return block;
        }

        private static Material GetBlockMaterial(Color color)
        {
            var key = ColorUtility.ToHtmlStringRGB(color);
            var path = $"Assets/Game/Materials/M3_Block_{key}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                name = "M3 Block " + key,
                color = color
            };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Transform FindTransform(Scene scene, string name)
            => scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Transform>(true)).FirstOrDefault(x => x.name == name);

        private static TMP_Text FindText(Scene scene, string name)
            => scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<TMP_Text>(true)).FirstOrDefault(x => x.name == name);

        private static void Reference(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException($"Missing {target.GetType().Name}.{field}");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void MarkNetworkPrefab(GameObject prefab)
        {
            var labels = AssetDatabase.GetLabels(prefab).Concat(new[] { "FusionPrefab" }).Distinct().ToArray();
            AssetDatabase.SetLabels(prefab, labels);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
