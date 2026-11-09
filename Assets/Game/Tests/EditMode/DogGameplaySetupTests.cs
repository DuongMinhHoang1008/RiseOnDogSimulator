using System.Collections.Generic;
using System.Linq;
using DogSimulator.CustomDogFace;
using DogSimulator.Editor;
using DogSimulator.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DogSimulator.Tests
{
    public sealed class DogGameplaySetupTests
    {
        [Test]
        public void PlayerUsesRiggedVisualAndGameplayControllerWithoutDemoAutoplay()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FusionPreviewSetup.PrefabPath);
            var presenter = prefab.GetComponent<DogVisualPresenter>();
            Assert.That(presenter, Is.Not.Null);
            Assert.That(prefab.GetComponent<DogAnimationDriver>(), Is.Not.Null);
            Assert.That(prefab.transform.Find("DogVisual"), Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, Is.EqualTo(2));
            Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true).Any(c => c != null && c.GetType().Name == "ReferenceDogAnimationPreview"), Is.False);
            var animator = presenter.Animator;
            Assert.That(animator.avatar.isValid, Is.True);
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController), Is.EqualTo(DogGameplaySetup.VisualControllerPath));

            var catalog = AssetDatabase.LoadAssetAtPath<DogModelCatalog>(M2PreviewUpgrade.CatalogPath);
            var entry = catalog.Resolve(catalog.FallbackModelId);
            Assert.That(entry.ModelId, Is.EqualTo(1));
            Assert.That(entry.AnimationController, Is.SameAs(animator.runtimeAnimatorController));
            Assert.That(AssetDatabase.GetAssetPath(entry.VisualPrefab), Is.EqualTo(DogGameplaySetup.VisualPrefabPath));
            Assert.That(entry.SupportsCustomFace, Is.True);
            Assert.That(entry.FaceLayoutVersion, Is.EqualTo(CustomDogFaceConstants.LayoutVersion));

            var surface = prefab.GetComponentInChildren<DogFaceSurface>(true);
            Assert.That(surface, Is.Not.Null);
            Assert.That(surface.name, Is.EqualTo("FaceSurface"));
            Assert.That(surface.TargetRenderer, Is.SameAs(surface.GetComponent<SkinnedMeshRenderer>()));
            var texture = surface.TargetRenderer.sharedMaterial.mainTexture;
            Assert.That(texture, Is.Not.Null);
            Assert.That(texture.width, Is.EqualTo(CustomDogFaceConstants.TextureSize));
            Assert.That(texture.height, Is.EqualTo(CustomDogFaceConstants.TextureSize));
        }

        [Test]
        public void GameplayJumpKeepsPoseButRemovesTheSecondVerticalJump()
        {
            AssertJumpHasFixedRoot(AssetDatabase.LoadAssetAtPath<AnimationClip>(DogGameplaySetup.JumpPath));
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(DogGameplaySetup.VisualControllerPath);
            var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            controller.GetOverrides(overrides);
            Assert.That(overrides.Count, Is.EqualTo(7));
            Assert.That(overrides.All(pair => pair.Value != null), Is.True);
            AssertJumpHasFixedRoot(overrides.Single(pair => pair.Key.name == "Jump_Gameplay").Value);

            var source = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/ReferenceDog/low_poly/Dog_Rigged.fbx").OfType<AnimationClip>().Single(c => c.name == "Jump");
            Assert.That(AnimationUtility.GetCurveBindings(source).Where(b => b.path.EndsWith("/Root") && b.propertyName == "m_LocalPosition.z")
                .Any(b => AnimationUtility.GetEditorCurve(source, b).keys.Max(k => k.value) - AnimationUtility.GetEditorCurve(source, b).keys.Min(k => k.value) > .001f), Is.True,
                "Do not change the original animation/showcase asset.");
        }

        private static void AssertJumpHasFixedRoot(AnimationClip jump)
        {
            Assert.That(jump, Is.Not.Null);
            var rootPositions = AnimationUtility.GetCurveBindings(jump)
                .Where(b => b.path.EndsWith("/Root") && b.propertyName.StartsWith("m_LocalPosition.")).ToArray();
            Assert.That(rootPositions.Length, Is.EqualTo(3));
            foreach (var binding in rootPositions)
            {
                var values = AnimationUtility.GetEditorCurve(jump, binding).keys.Select(k => k.value).ToArray();
                Assert.That(values.Max() - values.Min(), Is.LessThan(.000001f));
            }
            Assert.That(AnimationUtility.GetCurveBindings(jump).Length, Is.GreaterThan(100), "Leg tuck and landing curves must remain.");
        }
    }
}
