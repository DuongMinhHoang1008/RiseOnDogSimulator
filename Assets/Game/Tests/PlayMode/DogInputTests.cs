#if UNITY_EDITOR
using System.Collections;
using DogSimulator.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace DogSimulator.Tests
{
    public sealed class DogInputTests
    {
        private GameObject dog;
        private DogInputReader reader;
        private DogMovementConfig config;
        private Keyboard keyboard;
        private Mouse mouse;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorBehavior;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;

        [UnitySetUp]
        public IEnumerator CreateDog()
        {
            previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>("PreviewTestKeyboard");
            mouse = InputSystem.AddDevice<Mouse>("PreviewTestMouse");
            dog = new GameObject("Input test dog");
            dog.SetActive(false);
            reader = dog.AddComponent<DogInputReader>();
            config = ScriptableObject.CreateInstance<DogMovementConfig>();
            var serialized = new SerializedObject(reader);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.FindProperty("actions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/InputSystem_Actions.inputactions");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            dog.SetActive(true);
            yield return null;
            reader.SetGameplayEnabled(true);
            Assert.That(reader.GameplayEnabled, Is.True);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Object.Destroy(dog);
            Object.Destroy(config);
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            yield return null;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorBehavior;
            InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
        }

        [UnityTest]
        public IEnumerator ExistingPcBindingsWorkAndJumpIsConsumedOnlyOnce()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift, Key.Space));
            // Let the normal dynamic input update and MonoBehaviour.Update consume the event.
            yield return null;
            yield return null;
            Assert.That(keyboard.wKey.isPressed, Is.True);
            Assert.That(reader.Move.y, Is.EqualTo(1));
            Assert.That(reader.Sprint, Is.True);
            Assert.That(reader.ConsumeJump(), Is.True);
            Assert.That(reader.ConsumeJump(), Is.False);
        }

        [UnityTest]
        public IEnumerator ExpiredJumpDoesNotFireAfterSimulationPause()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return null;
            yield return null;
            Assert.That(reader.ConsumeJump(), Is.True, "Verify capture before testing expiration.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(config.JumpBufferSeconds + 0.05f);
            Assert.That(reader.ConsumeJump(), Is.False);
        }

        [UnityTest]
        public IEnumerator DisablingGameplayDiscardsMovementAndBufferedButtons()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Space));
            yield return null;
            yield return null;
            Assert.That(reader.Move.y, Is.EqualTo(1), "Verify capture before testing cleanup.");
            reader.SetGameplayEnabled(false);
            Assert.That(reader.Move, Is.EqualTo(Vector2.zero));
            Assert.That(reader.Sprint, Is.False);
            Assert.That(reader.ConsumeJump(), Is.False);
        }

        [UnityTest]
        public IEnumerator SimultaneousButtonsKeepIndependentCommandsAndExistingActionPriority()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F, Key.G, Key.H, Key.F6, Key.F7));
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
            yield return null;
            yield return null;

            Assert.That(reader.ConsumeAction(), Is.EqualTo(DogAction.Bite));
            Assert.That(reader.ConsumeAction(), Is.EqualTo(DogAction.None));
            Assert.That(reader.ConsumeRespawnBone(), Is.True);
            Assert.That(reader.ConsumeRespawnBone(), Is.False);
            Assert.That(reader.ConsumeResetMission(), Is.True);
            Assert.That(reader.ConsumeResetMission(), Is.False);
        }

        [UnityTest]
        public IEnumerator CarryAndResetInputsAreBufferedAndConsumedOnce()
        {
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return null;
            yield return null;
            Assert.That(reader.ConsumePrimaryCarry(), Is.True);
            Assert.That(reader.ConsumePrimaryCarry(), Is.False);
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null;

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return null;
            yield return null;
            Assert.That(reader.ConsumeDropCarry(), Is.True);
            Assert.That(reader.ConsumeDropCarry(), Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return null;
            yield return null;
            Assert.That(reader.ConsumeResetYard(), Is.True);
            Assert.That(reader.ConsumeResetYard(), Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F6));
            yield return null;
            yield return null;
            Assert.That(reader.ConsumeRespawnBone(), Is.True);
            Assert.That(reader.ConsumeRespawnBone(), Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F7));
            yield return null;
            yield return null;
            Assert.That(reader.ConsumeResetMission(), Is.True);
            Assert.That(reader.ConsumeResetMission(), Is.False);
        }
    }
}
#endif
