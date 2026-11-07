using System;
using System.Collections.Generic;
using System.Linq;
using DogSimulator.CustomDogFace;
using DogSimulator.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DogSimulator.Editor
{
    public static class BlankFaceDogSetup
    {
        public const int ModelId = CustomDogFaceConstants.CustomFaceModelId;
        private const int LegacyModelId = 1102;
        private const string Root = "Assets/Art/Models/Dogs/BlankFaceDog_20261107";
        private const string ModelPath = Root + "/model/BlankFaceDog_Rigged.fbx";
        private const string PrefabPath = Root + "/Unity/BlankFaceDog.prefab";
        private const string ControllerPath = "Assets/Game/Animation/Catalog/BlankFaceDog.overrideController";
        private const string BaseControllerPath = "Assets/Game/Animation/DogGameplay.controller";

        [MenuItem("Tools/Dog Simulator/Custom Face/Register Blank Face Art Model")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before registering Blank Face Dog.");
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            ConfigureImporter();
            var controller = CreateController();
            var prefab = CreatePrefab();
            RegisterCatalog(prefab, controller);
            VerifyPrefabAndCatalog(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Blank Face Dog art model registered with custom-face layout v1.");
        }

        private static void ConfigureImporter()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter
                ?? throw new InvalidOperationException($"Missing ModelImporter: {ModelPath}");
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.loopTime = Matches(clip.name, "Idle") || Matches(clip.name, "Run");
                clip.loopPose = clip.loopTime;
                clip.keepOriginalPositionY = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        private static RuntimeAnimatorController CreateController()
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
            foreach (var required in new[] { "Idle", "Run", "Jump", "Bark", "Poop", "Pee", "Bite" })
                if (!clips.Any(clip => Matches(clip.name, required)))
                    throw new InvalidOperationException($"Blank Face Dog is missing clip '{required}'.");
            var baseController = AssetDatabase.LoadAssetAtPath<AnimatorController>(BaseControllerPath)
                ?? throw new InvalidOperationException($"Missing base controller: {BaseControllerPath}");
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ControllerPath);
            if (controller == null)
            {
                controller = new AnimatorOverrideController(baseController) { name = "BlankFaceDog" };
                AssetDatabase.CreateAsset(controller, ControllerPath);
            }
            else controller.runtimeAnimatorController = baseController;
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(overrides);
            for (var index = 0; index < overrides.Count; index++)
            {
                var replacement = clips.FirstOrDefault(clip => Matches(clip.name, overrides[index].Key.name));
                if (replacement != null)
                    overrides[index] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[index].Key, replacement);
            }
            controller.ApplyOverrides(overrides);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static GameObject CreatePrefab()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath)
                ?? throw new InvalidOperationException($"Could not load model: {ModelPath}");
            var root = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                root.name = "BlankFaceDog";
                var faceTransform = root.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(item => string.Equals(item.name, "FaceSurface", StringComparison.Ordinal));
                if (faceTransform == null)
                    throw new InvalidOperationException("Imported model does not contain FaceSurface.");
                var renderer = faceTransform.GetComponent<Renderer>()
                    ?? throw new InvalidOperationException("FaceSurface has no Renderer.");
                var surface = faceTransform.GetComponent<DogFaceSurface>() ?? faceTransform.gameObject.AddComponent<DogFaceSurface>();
                var serialized = new SerializedObject(surface);
                serialized.FindProperty("targetRenderer").objectReferenceValue = renderer;
                serialized.FindProperty("layoutVersion").intValue = CustomDogFaceConstants.LayoutVersion;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }

        private static void RegisterCatalog(GameObject prefab, RuntimeAnimatorController controller)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DogModelCatalog>(M2PreviewUpgrade.CatalogPath)
                ?? throw new InvalidOperationException("DogModelCatalog is missing.");
            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("entries");
            for (var i = entries.arraySize - 1; i >= 0; i--)
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("modelId").intValue == LegacyModelId)
                    entries.DeleteArrayElementAtIndex(i);
            var index = -1;
            for (var i = 0; i < entries.arraySize; i++)
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("modelId").intValue == ModelId) index = i;
            if (index < 0) { index = entries.arraySize; entries.InsertArrayElementAtIndex(index); }
            var entry = entries.GetArrayElementAtIndex(index);
            var source = catalog.Entries.First(item => item.ModelId == 1001);
            entry.FindPropertyRelative("modelId").intValue = ModelId;
            entry.FindPropertyRelative("displayName").stringValue = "Blank Face Dog";
            entry.FindPropertyRelative("visualPrefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("animationController").objectReferenceValue = controller;
            entry.FindPropertyRelative("localPosition").vector3Value = source.LocalPosition;
            entry.FindPropertyRelative("localEulerAngles").vector3Value = source.LocalRotation.eulerAngles;
            entry.FindPropertyRelative("localScale").vector3Value = source.LocalScale;
            entry.FindPropertyRelative("mouthAnchor").vector3Value = source.MouthAnchor;
            entry.FindPropertyRelative("supportsCustomFace").boolValue = true;
            entry.FindPropertyRelative("faceLayoutVersion").intValue = CustomDogFaceConstants.LayoutVersion;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            if (!catalog.Validate(out var error)) throw new InvalidOperationException(error);
        }

        private static void VerifyPrefabAndCatalog(GameObject prefab)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DogModelCatalog>(M2PreviewUpgrade.CatalogPath);
            if (!catalog.TryGet(ModelId, out var entry) || !entry.SupportsCustomFace
                || entry.FaceLayoutVersion != CustomDogFaceConstants.LayoutVersion || entry.VisualPrefab != prefab)
                throw new InvalidOperationException("Blank Face Dog catalog entry is incomplete.");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                var surface = instance.GetComponentInChildren<DogFaceSurface>(true)
                    ?? throw new InvalidOperationException("Blank Face Dog prefab has no DogFaceSurface.");
                if (surface.TargetRenderer == null || surface.LayoutVersion != CustomDogFaceConstants.LayoutVersion)
                    throw new InvalidOperationException("DogFaceSurface renderer/layout binding is invalid.");
                var mesh = surface.TargetRenderer is SkinnedMeshRenderer skinned
                    ? skinned.sharedMesh : surface.TargetRenderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null || mesh.uv.Length == 0)
                    throw new InvalidOperationException("FaceSurface has no UV mesh.");
                var min = mesh.uv.Aggregate(Vector2.one, Vector2.Min);
                var max = mesh.uv.Aggregate(Vector2.zero, Vector2.Max);
                if (min.x > .001f || min.y > .001f || max.x < .999f || max.y < .999f)
                    throw new InvalidOperationException($"FaceSurface UV must cover 0..1, got {min}..{max}.");
                var probe = new Texture2D(CustomDogFaceConstants.TextureSize, CustomDogFaceConstants.TextureSize,
                    TextureFormat.RGB24, false) { name = "Blank Face Runtime Probe" };
                surface.Apply(probe, true);
                if (surface.TargetRenderer.sharedMaterial.mainTexture != probe)
                    throw new InvalidOperationException("DogFaceSurface did not apply the runtime texture.");
            }
            finally { Object.DestroyImmediate(instance); }
        }

        private static bool Matches(string actual, string expected)
        {
            actual = actual.Replace("_Gameplay", string.Empty);
            expected = expected.Replace("_Gameplay", string.Empty);
            return actual.Equals(expected, StringComparison.OrdinalIgnoreCase)
                || actual.EndsWith("|" + expected, StringComparison.OrdinalIgnoreCase)
                || actual.EndsWith("_" + expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
