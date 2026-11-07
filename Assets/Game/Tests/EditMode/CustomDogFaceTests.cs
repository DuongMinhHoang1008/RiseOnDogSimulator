using System.IO;
using System.Linq;
using DogSimulator.CustomDogFace;
using DogSimulator.Editor;
using DogSimulator.Networking;
using DogSimulator.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DogSimulator.Tests.EditMode
{
    public sealed class CustomDogFaceTests
    {
        [Test]
        public void JpegHeaderParserReadsDimensionsAndRejectsTruncation()
        {
            var header = new byte[]
            {
                0xff, 0xd8, 0xff, 0xe0, 0, 4, 0, 0,
                0xff, 0xc0, 0, 17, 8, 2, 0, 2, 0, 3, 1, 1, 0, 2, 1, 0, 3, 1, 0
            };
            Assert.That(DogFaceImageValidator.TryReadJpegSize(header, out var width, out var height), Is.True);
            Assert.That(width, Is.EqualTo(512));
            Assert.That(height, Is.EqualTo(512));
            Assert.That(DogFaceImageValidator.TryReadJpegSize(header.Take(10).ToArray(), out _, out _), Is.False);
        }

        [Test]
        public void ValidatorRejectsOversizeAndWrongDimensions()
        {
            var oversize = new byte[CustomDogFaceConstants.MaximumJpegBytes + 1];
            oversize[0] = 0xff; oversize[1] = 0xd8;
            Assert.That(DogFaceImageValidator.IsValidFaceJpeg(oversize, out var error), Is.False);
            Assert.That(error, Does.Contain("128 KiB"));
        }

        [Test]
        public void CatalogUsesBlankFaceArtModelWithRuntimeSurface()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DogModelCatalog>("Assets/Game/Config/DogModelCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(BlankFaceDogSetup.ModelId, Is.EqualTo(CustomDogFaceConstants.CustomFaceModelId));
            Assert.That(catalog.TryGet(CustomDogFaceConstants.CustomFaceModelId, out var entry), Is.True);
            Assert.That(entry.DisplayName, Is.EqualTo("Blank Face Dog"));
            Assert.That(entry.SupportsCustomFace, Is.True);
            Assert.That(entry.FaceLayoutVersion, Is.EqualTo(CustomDogFaceConstants.LayoutVersion));
            Assert.That(AssetDatabase.GetAssetPath(entry.VisualPrefab),
                Is.EqualTo("Assets/Art/Models/Dogs/BlankFaceDog_20261107/Unity/BlankFaceDog.prefab"));
            var surface = entry.VisualPrefab.GetComponentInChildren<DogFaceSurface>(true);
            Assert.That(surface, Is.Not.Null);
            Assert.That(surface.TargetRenderer, Is.Not.Null);
        }

        [Test]
        public void DefaultFaceTextureIsAFeaturelessSolidColor()
        {
            var png = File.ReadAllBytes("Assets/Art/Models/Dogs/BlankFaceDog_20261107/model/BlankFace_Default.png");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(ImageConversion.LoadImage(texture, png, false), Is.True);
                Assert.That(texture.width, Is.EqualTo(CustomDogFaceConstants.TextureSize));
                Assert.That(texture.height, Is.EqualTo(CustomDogFaceConstants.TextureSize));
                var pixels = texture.GetPixels32();
                Assert.That(pixels.All(pixel => pixel.Equals(pixels[0])), Is.True,
                    "The blank test face must not contain eyes, a nose, a mouth, or any other markings.");
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void PlayerPrefabAndSceneContainFaceRuntimeBindings()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/DogPlayer.prefab");
            Assert.That(prefab.GetComponent<DogFaceAppearance>(), Is.Not.Null);
            var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/M1_FusionPreview.unity", OpenSceneMode.Single);
            var ui = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<FusionPreviewUI>(true)).Single();
            var uiObject = new SerializedObject(ui);
            Assert.That(uiObject.FindProperty("editFaceButton").objectReferenceValue, Is.Not.Null);
            Assert.That(uiObject.FindProperty("faceEditor").objectReferenceValue, Is.Not.Null);
            Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DogFaceWebBridge>(true)).Count(), Is.EqualTo(1));
        }
    }
}
