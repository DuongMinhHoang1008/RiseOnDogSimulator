using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
        public void ReconfiguringFaceEditorDetachesPreviousOffsetSliders()
        {
            var root = new GameObject("Face editor test");
            try
            {
                var editor = root.AddComponent<DogFaceEditorController>();
                var oldX = AddSlider("Old X");
                var oldY = AddSlider("Old Y");
                var newX = AddSlider("New X");
                var newY = AddSlider("New Y");
                var offset = typeof(DogFaceEditorController).GetField("offset", BindingFlags.Instance | BindingFlags.NonPublic);
                editor.ConfigureMenu(root, null, null, null, null, oldX, oldY, null, null);
                oldX.onValueChanged.Invoke(.25f);
                oldY.onValueChanged.Invoke(.5f);
                Assert.That(offset.GetValue(editor), Is.EqualTo(new Vector2(.25f, .5f)));

                editor.ConfigureMenu(root, null, null, null, null, newX, newY, null, null);
                editor.ConfigureMenu(root, null, null, null, null, newX, newY, null, null);
                newX.onValueChanged.Invoke(.75f);
                newY.onValueChanged.Invoke(.25f);
                oldX.onValueChanged.Invoke(1f);
                oldY.onValueChanged.Invoke(1f);
                Assert.That(offset.GetValue(editor), Is.EqualTo(new Vector2(.75f, .25f)));
            }
            finally { Object.DestroyImmediate(root); }

            UnityEngine.UI.Slider AddSlider(string name)
            {
                var child = new GameObject(name, typeof(RectTransform));
                child.transform.SetParent(root.transform);
                return child.AddComponent<UnityEngine.UI.Slider>();
            }
        }

        [Test]
        public void FaceMessageHeaderRejectsTruncationAndLeavesPayloadUnread()
        {
            const string hash = "abcd";
            using var packet = new MemoryStream();
            using var writer = new BinaryWriter(packet);
            writer.Write(0x44464143);
            writer.Write((byte)CustomDogFaceConstants.ProtocolVersion);
            writer.Write((byte)2);
            writer.Write(123u);
            writer.Write(4);
            writer.Write(17);
            writer.Write(CustomDogFaceConstants.CustomFaceModelId);
            writer.Write(CustomDogFaceConstants.LayoutVersion);
            writer.Write(1);
            writer.Write((byte)hash.Length);
            writer.Write(Encoding.ASCII.GetBytes(hash));
            writer.Write((byte)0xff);
            var bytes = packet.ToArray();

            var parser = typeof(CustomDogFaceNetworkService)
                .GetMethod("TryReadHeader", BindingFlags.Static | BindingFlags.NonPublic);
            using var shortHeader = new BinaryReader(new MemoryStream(bytes, 0, 30));
            Assert.That(parser.Invoke(null, new object[] { shortHeader, null }), Is.False);
            using var shortHash = new BinaryReader(new MemoryStream(bytes, 0, bytes.Length - 2));
            Assert.That(parser.Invoke(null, new object[] { shortHash, null }), Is.False);

            using var reader = new BinaryReader(new MemoryStream(bytes));
            var arguments = new object[] { reader, null };
            Assert.That(parser.Invoke(null, arguments), Is.True);
            var header = arguments[1];
            Assert.That(header.GetType().GetField("Hash").GetValue(header), Is.EqualTo(hash));
            Assert.That(header.GetType().GetField("RequestId").GetValue(header), Is.EqualTo(17));
            Assert.That(reader.ReadByte(), Is.EqualTo(0xff));
        }

        [Test]
        public void FaceStorageRejectsMalformedRecordsAndReloadsSavedJpeg()
        {
            const string key = CustomDogFaceConstants.StorageKey;
            var hadRecord = PlayerPrefs.HasKey(key);
            var original = PlayerPrefs.GetString(key);
            const int size = CustomDogFaceConstants.TextureSize;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            try
            {
                PlayerPrefs.SetString(key, "{ invalid");
                Assert.That(DogFaceStorage.TryLoad(out _), Is.False);
                var jpeg = ImageConversion.EncodeToJPG(texture);
                Assert.That(DogFaceStorage.Save(jpeg), Is.True);
                Assert.That(DogFaceStorage.TryLoad(out var saved), Is.True);
                Assert.That(saved, Is.EqualTo(jpeg));
            }
            finally
            {
                if (hadRecord) PlayerPrefs.SetString(key, original);
                else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
                Object.DestroyImmediate(texture);
            }
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
            Assert.That(surface.TargetRenderer, Is.InstanceOf<SkinnedMeshRenderer>());
            var faceRenderer = (SkinnedMeshRenderer)surface.TargetRenderer;
            Assert.That(faceRenderer.sharedMesh, Is.Not.Null);
            var texture = faceRenderer.sharedMaterial.mainTexture;
            Assert.That(texture, Is.Not.Null);
            Assert.That(texture.width, Is.EqualTo(CustomDogFaceConstants.TextureSize));
            Assert.That(texture.height, Is.EqualTo(CustomDogFaceConstants.TextureSize));
            Assert.That(entry.AnimationController.animationClips.Select(AssetDatabase.GetAssetPath),
                Has.All.EqualTo("Assets/Art/Models/Dogs/BlankFaceDog_20261107/model/BlankFaceDog_Rigged.fbx"));
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
