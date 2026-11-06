#if UNITY_EDITOR
using System.Collections;
using DogSimulator.Player;
using DogSimulator.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DogSimulator.Tests
{
    public sealed class PreviewFlowTests
    {
        private Scene previewScene;

        [UnityTest]
        public IEnumerator MenuValidatesNameStartsLocalDriverAndResetsWithoutNetwork()
        {
            previewScene = EditorSceneManager.LoadSceneInPlayMode("Assets/Game/Scenes/M1_LocalPreview.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            {
                yield return null;
                var frame = Time.frameCount;
                yield return null;
                Assert.That(Time.frameCount, Is.GreaterThan(frame), "Player loop must be advancing.");
                LocalPreviewUI ui = null;
                foreach (var root in previewScene.GetRootGameObjects())
                    if (root.TryGetComponent<LocalPreviewUI>(out var found)) ui = found;
                Assert.That(ui, Is.Not.Null);
                var fields = new SerializedObject(ui);
                var reader = (DogInputReader)fields.FindProperty("input").objectReferenceValue;
                var motor = (DogMotor)fields.FindProperty("motor").objectReferenceValue;
                var nickname = (TMP_InputField)fields.FindProperty("nickname").objectReferenceValue;
                var message = (TMP_Text)fields.FindProperty("message").objectReferenceValue;
                var label = (TMP_Text)fields.FindProperty("playerLabel").objectReferenceValue;
                var play = (UnityEngine.UI.Button)fields.FindProperty("playButton").objectReferenceValue;
                var reset = (UnityEngine.UI.Button)fields.FindProperty("backButton").objectReferenceValue;
                var menu = (GameObject)fields.FindProperty("menu").objectReferenceValue;
                Assert.That(reader.GameplayEnabled, Is.False);
                nickname.text = "A";
                play.onClick.Invoke();
                Assert.That(reader.GameplayEnabled, Is.False);
                Assert.That(message.text, Does.Contain("2–16"));
                var spawn = motor.transform.position;
                nickname.text = "<b>Dog</b>";
                play.onClick.Invoke();
                Assert.That(reader.GameplayEnabled, Is.True);
                Assert.That(menu.activeSelf, Is.False);
                Assert.That(label.richText, Is.False);
                Assert.That(label.text, Does.Contain("<b>Dog</b>"));
                Assert.That(label.text, Does.Contain("OFFLINE"));
                reset.onClick.Invoke();
                Assert.That(reader.GameplayEnabled, Is.False);
                Assert.That(menu.activeSelf, Is.True);
                Assert.That(motor.transform.position, Is.EqualTo(spawn));
                Assert.That(reader.ConsumeJump(), Is.False);
            }
        }

        private bool hadNickname;
        private string previousNickname;

        [SetUp]
        public void SavePreferences()
        {
            hadNickname = PlayerPrefs.HasKey("DogSimulator.Nickname");
            previousNickname = PlayerPrefs.GetString("DogSimulator.Nickname");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (previewScene.IsValid() && previewScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(previewScene);
            if (hadNickname) PlayerPrefs.SetString("DogSimulator.Nickname", previousNickname);
            else PlayerPrefs.DeleteKey("DogSimulator.Nickname");
            PlayerPrefs.Save();
        }
    }
}
#endif
