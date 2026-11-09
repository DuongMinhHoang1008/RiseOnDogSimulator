using System.Collections.Generic;
using System.Linq;
using DogSimulator.Editor;
using DogSimulator.CustomDogFace;
using DogSimulator.Networking;
using DogSimulator.Player;
using DogSimulator.UI;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DogSimulator.Tests
{
    public sealed class M2CatalogReservationTests
    {
        [Test]
        public void CatalogHasStableUniqueIdsAndRealSuppliedModels()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DogModelCatalog>(M2PreviewUpgrade.CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Validate(out var error), Is.True, error);
            Assert.That(catalog.Entries.Select(entry => entry.ModelId),
                Is.EquivalentTo(new[] { 1, 1001, 1002, CustomDogFaceConstants.CustomFaceModelId }));
            Assert.That(catalog.Entries.Select(entry => entry.ModelId).Distinct().Count(), Is.EqualTo(catalog.Entries.Count));
            var photoDog = catalog.Entries.Single(entry => entry.ModelId == 1001);
            Assert.That(photoDog.VisualPrefab.name, Is.EqualTo("PhotoDogCustomFace"));
            Assert.That(photoDog.SupportsCustomFace, Is.True);
            Assert.That(photoDog.FaceLayoutVersion, Is.EqualTo(CustomDogFaceConstants.LayoutVersion));
            Assert.That(photoDog.AnimationController, Is.EqualTo(photoDog.VisualPrefab.GetComponent<Animator>().runtimeAnimatorController));
            var photoFace = photoDog.VisualPrefab.GetComponentInChildren<DogFaceSurface>();
            Assert.That(photoFace, Is.Not.Null);
            Assert.That(photoFace.TargetRenderer, Is.InstanceOf<SkinnedMeshRenderer>());
            Assert.That(catalog.Entries.Single(entry => entry.ModelId == 1002).VisualPrefab.name, Is.EqualTo("Dog_Rigged"));
            Assert.That(catalog.Entries.Single(entry => entry.ModelId == CustomDogFaceConstants.CustomFaceModelId)
                .VisualPrefab.name, Is.EqualTo("BlankFaceDog"));
            Assert.That(catalog.Resolve(987654).ModelId, Is.EqualTo(catalog.FallbackModelId));
        }

        [Test]
        public void ReservationRulesAreUniqueIdempotentAndReleaseable()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DogModelCatalog>(M2PreviewUpgrade.CatalogPath);
            var first = PlayerRef.FromIndex(0);
            var second = PlayerRef.FromIndex(1);
            var reservations = new Dictionary<PlayerRef, int>();
            Assert.That(DogModelReservationRules.TryReserve(reservations, first, 1001, catalog), Is.EqualTo(ModelSelectionResult.Approved));
            Assert.That(DogModelReservationRules.TryReserve(reservations, first, 1001, catalog), Is.EqualTo(ModelSelectionResult.Approved));
            Assert.That(DogModelReservationRules.TryReserve(reservations, second, 1001, catalog), Is.EqualTo(ModelSelectionResult.Approved));
            Assert.That(reservations[first], Is.EqualTo(1001));
            Assert.That(reservations[second], Is.EqualTo(1001));
            Assert.That(DogModelReservationRules.TryReserve(reservations, second, 9999, catalog), Is.EqualTo(ModelSelectionResult.InvalidModel));
            reservations.Remove(first);
            Assert.That(DogModelReservationRules.TryReserve(reservations, second, 1001, catalog), Is.EqualTo(ModelSelectionResult.Approved));
        }

        [Test]
        public void RegistryAndSceneContainMasterHandoffPickerPreviewAndLocalFpsHud()
        {
            var registry = AssetDatabase.LoadAssetAtPath<GameObject>(M2PreviewUpgrade.RegistryPrefabPath);
            Assert.That(registry, Is.Not.Null);
            var networkObject = registry.GetComponent<NetworkObject>();
            Assert.That(networkObject.Flags.HasFlag(NetworkObjectFlags.MasterClientObject), Is.True);
            Assert.That(networkObject.Flags.HasFlag(NetworkObjectFlags.DestroyWhenStateAuthorityLeaves), Is.False);
            Assert.That(registry.GetComponent<DogModelSelectionRegistry>(), Is.Not.Null);
            var request = AssetDatabase.LoadAssetAtPath<GameObject>(M2PreviewUpgrade.RequestPrefabPath);
            Assert.That(request, Is.Not.Null);
            Assert.That(request.GetComponent<DogModelSelectionRequest>(), Is.Not.Null);
            Assert.That(request.GetComponent<NetworkObject>().Flags
                .HasFlag(NetworkObjectFlags.DestroyWhenStateAuthorityLeaves), Is.True);

            var scene = EditorSceneManager.OpenScene(FusionPreviewSetup.ScenePath, OpenSceneMode.Additive);
            try
            {
                var behaviours = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                Assert.That(behaviours.Count(item => item is FpsHud), Is.EqualTo(1));
                Assert.That(behaviours.Count(item => item is DogSelectionPreview), Is.EqualTo(1));
                Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TMP_Dropdown>(true)).Count(), Is.EqualTo(1));
                var menu = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<RectTransform>(true))
                    .Single(item => item.name == "Menu");
                Assert.That(menu.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>(), Is.Not.Null);
                Assert.That(menu.GetComponent<UnityEngine.UI.VerticalLayoutGroup>(), Is.Null);
                Assert.That(menu.Find("LeftColumn"), Is.Not.Null);
                Assert.That(menu.Find("Dog Preview"), Is.Not.Null);
                Assert.That(menu.Find("LeftColumn/Settings"), Is.Not.Null);
                var fps = behaviours.OfType<FpsHud>().Single();
                var iterator = new SerializedObject(fps).GetIterator();
                while (iterator.NextVisible(true))
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                        Assert.That(iterator.objectReferenceValue, Is.Not.Null, iterator.name);
                var preview = behaviours.OfType<DogSelectionPreview>().Single();
                iterator = new SerializedObject(preview).GetIterator();
                while (iterator.NextVisible(true))
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                        Assert.That(iterator.objectReferenceValue, Is.Not.Null, iterator.name);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
