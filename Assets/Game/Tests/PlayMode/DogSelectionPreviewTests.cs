#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using DogSimulator.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
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
