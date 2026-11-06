using DogSimulator.Editor;
using DogSimulator.Player;
using DogSimulator.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace DogSimulator.Tests
{
    public sealed class PreviewSceneTests
    {
        [Test]
        public void PreviewHasOneDriverAndValidSerializedReferences()
        {
            var scene = SceneManager.GetSceneByPath(LocalPreviewSetup.ScenePath);
            var wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(LocalPreviewSetup.ScenePath, OpenSceneMode.Additive);
            try
            {
                var drivers = 0;
                var events = 0;
                var cameras = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    drivers += root.GetComponentsInChildren<LocalMovementPreview>(true).Length;
                    events += root.GetComponentsInChildren<EventSystem>(true).Length;
                    cameras += root.GetComponentsInChildren<Camera>(true).Length;
                    Assert.That(root.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                    foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        Assert.That(behaviour, Is.Not.Null, "Missing scene script");
                        if (!(behaviour is DogMotor) && !(behaviour is DogInputReader)
                            && !(behaviour is LocalMovementPreview) && !(behaviour is DogCamera)
                            && !(behaviour is LocalPreviewUI)) continue;
                        var iterator = new SerializedObject(behaviour).GetIterator();
                        while (iterator.NextVisible(true))
                            if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                                Assert.That(iterator.objectReferenceValue, Is.Not.Null, behaviour.GetType().Name + "." + iterator.name);
                    }
                    foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                    {
                        Assert.That(text.font, Is.Not.Null);
                        Assert.That(text.richText, Is.False);
                    }
                }
                Assert.That(drivers, Is.EqualTo(1));
                Assert.That(events, Is.EqualTo(1));
                Assert.That(cameras, Is.EqualTo(1));
            }
            finally { if (!wasLoaded) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
