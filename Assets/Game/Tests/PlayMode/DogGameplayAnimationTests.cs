#if UNITY_EDITOR
using System.Collections;
using DogSimulator.Player;
using DogSimulator.Networking;
using Fusion;
using Assert = NUnit.Framework.Assert;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace DogSimulator.Tests
{
    public sealed class DogGameplayAnimationTests
    {
        private GameObject dog, floor;
        private DogMotor motor;
        private DogAnimationDriver driver;
        private DogInputReader input;
        private Keyboard keyboard;
        private Mouse mouse;
        private NetworkRunner trialRunner;
        private GameObject trialCamera;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorBehavior;
        private InputSettings.BackgroundBehavior previousBackgroundBehavior;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>("GameplayAnimationTestKeyboard");
            mouse = InputSystem.AddDevice<Mouse>("GameplayAnimationTestMouse");
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(400, -.25f, 400);floor.transform.localScale = new Vector3(30, .5f, 30);
            dog = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/DogPlayer.prefab"));
            dog.GetComponent<CharacterController>().enabled = false;
            dog.transform.position = new Vector3(400, .1f, 400);
            dog.GetComponent<CharacterController>().enabled = true;
            motor = dog.GetComponent<DogMotor>();driver = dog.GetComponent<DogAnimationDriver>();input = dog.GetComponent<DogInputReader>();
            driver.Visual.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Physics.SyncTransforms();yield return null;
            input.SetGameplayEnabled(true);
            for (var i=0;i<20;i++) driver.Simulate(input,0,.02f);
            Assert.That(motor.IsGrounded, Is.True);
        }

        private IEnumerator Keys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));yield return null;yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (trialRunner != null)
            {
                var shutdown = trialRunner.Shutdown();var timeout = Time.realtimeSinceStartup + 15;
                while (!shutdown.IsCompleted && Time.realtimeSinceStartup < timeout) yield return null;
                Object.Destroy(trialRunner.gameObject);
            }
            if (trialCamera != null) Object.Destroy(trialCamera);
            Object.Destroy(dog);Object.Destroy(floor);InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);yield return null;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorBehavior;
            InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
        }

        [UnityTest]
        public IEnumerator MovementDrivesRunAndBarkStopsUntilItsAnimationEnds()
        {
            yield return Keys(Key.W, Key.LeftShift);
            var start = dog.transform.position;
            for (var i=0;i<10;i++) driver.Simulate(input,0,.02f);
            Assert.That(driver.NormalizedSpeed, Is.GreaterThan(.9f));
            Assert.That(Vector3.Distance(start,dog.transform.position), Is.GreaterThan(1));
            driver.PresentLocal();driver.Visual.Animator.Update(.2f);
            Assert.That(driver.Visual.Animator.GetCurrentAnimatorStateInfo(0).IsName("Run"), Is.True);
            yield return Keys(Key.F);
            driver.Simulate(input,0,.02f);driver.PresentLocal();
            Assert.That(driver.CurrentAction, Is.EqualTo(DogAction.Bark));
            yield return Keys(Key.W);
            start = dog.transform.position;
            for (var i=0;i<10;i++) driver.Simulate(input,0,.02f);
            Assert.That(Vector3.Distance(start,dog.transform.position), Is.LessThan(.01f));
            for (var i=0;i<60;i++) driver.Simulate(input,0,.02f);
            Assert.That(driver.CurrentAction, Is.EqualTo(DogAction.None));
            Assert.That(driver.NormalizedSpeed, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator ToiletingCanBeCancelledAndMenuDiscardsBufferedActions()
        {
            yield return Keys(Key.H);
            driver.Simulate(input,0,.02f);Assert.That(driver.CurrentAction, Is.EqualTo(DogAction.Pee));
            yield return Keys(Key.W);
            driver.Simulate(input,0,.02f);Assert.That(driver.CurrentAction, Is.EqualTo(DogAction.None));
            yield return Keys(Key.G);
            driver.Simulate(input,0,.02f);Assert.That(driver.CurrentAction, Is.EqualTo(DogAction.Poop));
            input.SetGameplayEnabled(false);driver.Simulate(input,0,.02f);
            Assert.That(driver.CurrentAction, Is.EqualTo(DogAction.None));
            Assert.That(input.ConsumeAction(), Is.EqualTo(DogAction.None));
        }

        [UnityTest]
        public IEnumerator AcceptedJumpAnimatesAndAnAirJumpDoesNotResetIt()
        {
            yield return Keys(Key.Space);
            driver.Simulate(input,0,.02f);
            Assert.That(motor.JumpedThisStep, Is.True);
            Assert.That(driver.CurrentAction, Is.EqualTo(DogAction.Jump));
            var sequence = driver.Sequence;
            yield return Keys();yield return Keys(Key.Space);
            driver.Simulate(input,0,.02f);
            Assert.That(motor.JumpedThisStep, Is.False);
            Assert.That(driver.CurrentAction, Is.EqualTo(DogAction.Jump));
            Assert.That(driver.Sequence, Is.EqualTo(sequence));
            for (var i=0;i<80;i++) driver.Simulate(input,0,.02f);
            Assert.That(motor.IsGrounded, Is.True);
            Assert.That(driver.CurrentAction, Is.EqualTo(DogAction.None));
        }

        [UnityTest]
        public IEnumerator RemoteSnapshotStartsAnOngoingActionPartwayThrough()
        {
            driver.PresentSnapshot(0,true,DogAction.Pee,123,1.6f);
            driver.Visual.Animator.Update(.1f);
            var state = driver.Visual.Animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(driver.Visual.RenderedAction, Is.EqualTo(DogAction.Pee));
            Assert.That(state.IsName("Pee"), Is.True);
            Assert.That(state.normalizedTime, Is.InRange(.45f,.75f));
            driver.PresentSnapshot(0,true,DogAction.None,124,0);driver.Visual.Animator.Update(.2f);
            Assert.That(driver.Visual.Animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RightClickBuffersBiteOnceAndMenuDiscardsIt()
        {
            InputSystem.QueueStateEvent(mouse,new MouseState { buttons = 2 });yield return null;yield return null;
            Assert.That(input.ConsumeAction(), Is.EqualTo(DogAction.Bite));
            Assert.That(input.ConsumeAction(), Is.EqualTo(DogAction.None));
            input.SetGameplayEnabled(false);
            InputSystem.QueueStateEvent(mouse,new MouseState());yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState { buttons = 2 });yield return null;yield return null;
            Assert.That(input.ConsumeAction(), Is.EqualTo(DogAction.None));
        }

        [UnityTest]
        public IEnumerator FusionAuthorityWritesMovementAndActionStateWithoutCloud()
        {
            input.SetGameplayEnabled(false);
            trialRunner = new GameObject("Single runner animation test").AddComponent<NetworkRunner>();
            var manager = trialRunner.gameObject.AddComponent<NetworkSceneManagerDefault>();
            var start = trialRunner.StartGame(new StartGameArgs { GameMode = GameMode.Single, SceneManager = manager, PlayerCount = 1 });
            var deadline = Time.realtimeSinceStartup + 20;
            while (!start.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(start.IsCompleted, Is.True);Assert.That(start.Result.Ok, Is.True);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/DogPlayer.prefab").GetComponent<NetworkObject>();
            var avatar = trialRunner.Spawn(prefab,new Vector3(402,.1f,400),Quaternion.identity);
            var player = avatar.GetComponent<NetworkDogPlayer>();
            trialCamera = new GameObject("Single runner follow camera");
            player.InitializeOwner("RigTest",trialCamera.AddComponent<DogCamera>());player.SetControlsEnabled(true);
            Assert.That(avatar.HasStateAuthority, Is.True);
            yield return Keys(Key.W,Key.LeftShift);
            yield return new WaitForSeconds(.15f);
            Assert.That(player.AnimationSpeed, Is.GreaterThan(.8f));
            yield return Keys(Key.F);
            deadline = Time.realtimeSinceStartup + 1;
            while (player.AnimationAction != DogAction.Bark && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(player.AnimationAction, Is.EqualTo(DogAction.Bark));
            Assert.That(player.AnimationSequence, Is.GreaterThan(0));
            Assert.That(player.AnimationUntil.IsRunning, Is.True);
            Assert.That(avatar.GetComponent<DogVisualPresenter>().RenderedAction, Is.EqualTo(DogAction.Bark));
        }
    }
}
#endif
