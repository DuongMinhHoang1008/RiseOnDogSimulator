using System.Linq;
using DogSimulator.Networking;
using Fusion;
using Fusion.Editor;
using UnityEditor;
using UnityEngine;

namespace DogSimulator.Editor
{
    public static class DogWasteSetup
    {
        private const string ModelPath = "Assets/Art/Models/DogPoop.obj";
        private const string PoopPath = "Assets/Game/Prefabs/Carryables/CarryablePoop.prefab";
        private const string PeeMaterialPath = "Assets/Game/Materials/DogPee.mat";

        [MenuItem("Tools/Dog Simulator/Apply Poop and Pee Effects")]
        public static void Apply()
        {
            var poop = GetOrCreatePoopPrefab();
            var pee = GetOrCreatePeeMaterial();
            SetPrefabReference<SandboxInteractionRegistry>(M3SandboxSetup.RegistryPath, "poopPrefab", poop);
            SetPrefabReference<NetworkDogPlayer>(FusionPreviewSetup.PrefabPath, "peeMaterial", pee);
            AssetDatabase.SaveAssets();
            NetworkProjectConfigUtilities.RebuildPrefabTable();
        }

        internal static NetworkObject GetOrCreatePoopPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PoopPath);
            if (prefab != null) return prefab.GetComponent<NetworkObject>();
            var mesh = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Mesh>().First();
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Models/DogPoop.mat");
            var root = new GameObject("CarryablePoop");
            try
            {
                root.transform.localScale = Vector3.one * 2.4f;
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.AddComponent<MeshRenderer>().sharedMaterial = material;
                var collider = root.AddComponent<BoxCollider>();
                collider.center = mesh.bounds.center;
                collider.size = mesh.bounds.size;
                var body = root.AddComponent<Rigidbody>();
                body.mass = .18f;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                var network = root.AddComponent<NetworkObject>();
                network.Flags |= NetworkObjectFlags.MasterClientObject;
                network.Flags &= ~(NetworkObjectFlags.DestroyWhenStateAuthorityLeaves | NetworkObjectFlags.AllowStateAuthorityOverride);
                root.AddComponent<NetworkTransform>();
                var carryable = new SerializedObject(root.AddComponent<NetworkCarryable>());
                carryable.FindProperty("lifetimeSeconds").floatValue = 45f;
                carryable.ApplyModifiedPropertiesWithoutUndo();
                prefab = PrefabUtility.SaveAsPrefabAsset(root, PoopPath);
                AssetDatabase.SetLabels(prefab, new[] { "FusionPrefab" });
                return prefab.GetComponent<NetworkObject>();
            }
            finally { Object.DestroyImmediate(root); }
        }

        internal static Material GetOrCreatePeeMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(PeeMaterialPath);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"))
            {
                name = "Dog Pee",
                color = new Color(1f, .76f, .12f)
            };
            AssetDatabase.CreateAsset(material, PeeMaterialPath);
            return material;
        }

        private static void SetPrefabReference<T>(string path, string field, Object value) where T : Component
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var serialized = new SerializedObject(root.GetComponent<T>());
                serialized.FindProperty(field).objectReferenceValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
