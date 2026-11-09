#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using DogSimulator.CustomDogFace;
using DogSimulator.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DogSimulator.Tests
{
    public sealed class DogSelectionPreviewTests
    {
        [UnityTest]
        public IEnumerator PreviewSceneRendersAndSwitchesBothNewDogModels()
        {
            var operation = EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Game/Scenes/M1_FusionPreview.unity", new LoadSceneParameters(LoadSceneMode.Additive));
            while (!operation.isDone) yield return null;
            var scene = SceneManager.GetSceneByPath("Assets/Game/Scenes/M1_FusionPreview.unity");
            var preview = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<DogSelectionPreview>(true)).Single();
            Assert.That(preview.RenderTextureReady, Is.True);
            preview.ShowModel(1001);
            yield return null;
            Assert.That(preview.DisplayedModelId, Is.EqualTo(1001));
            Assert.That(preview.HasRenderedModel, Is.True);
            Assert.That(preview.FaceSurface, Is.Not.Null);
            var faceRenderer = preview.FaceSurface.TargetRenderer;
            var defaultFace = faceRenderer.sharedMaterial.mainTexture;
            var size = CustomDogFaceConstants.TextureSize;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            try
            {
                texture.SetPixels32(Enumerable.Repeat(new Color32(32, 128, 224, 255), size * size).ToArray());
                Assert.That(preview.ApplyFaceJpeg(texture.EncodeToJPG(80)), Is.True);
                Assert.That(faceRenderer.sharedMaterial.mainTexture, Is.Not.SameAs(defaultFace));
                preview.ResetFaceToDefault();
                Assert.That(faceRenderer.sharedMaterial.mainTexture, Is.SameAs(defaultFace));
            }
            finally { Object.Destroy(texture); }
            preview.ShowModel(1002);
            yield return null;
            Assert.That(preview.DisplayedModelId, Is.EqualTo(1002));
            Assert.That(preview.HasRenderedModel, Is.True);
            operation = SceneManager.UnloadSceneAsync(scene);
            while (!operation.isDone) yield return null;
        }
    }
}
#endif
