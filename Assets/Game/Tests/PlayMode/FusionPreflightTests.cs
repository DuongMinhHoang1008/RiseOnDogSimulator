using System.Collections;
using System.Linq;
using System.Reflection;
using DogSimulator.Networking;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DogSimulator.Tests
{
    public sealed class FusionPreflightTests
    {
        [UnityTest]
        public IEnumerator SinglePlayerReopensTheSavedSandbox()
        {
            const string path = "Assets/Game/Scenes/M1_FusionPreview.unity";
            const int worldSlot = 3;
            const string saveKey = "DogSimulator.WorldSlot.3";
            var originalSave = PlayerPrefs.GetString(saveKey);
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            var scene = SceneManager.GetSceneByPath(path);
            var session = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<FusionSessionService>()).Single();
            AsyncOperation unload = null;
            try
            {
                PlayerPrefs.DeleteKey(saveKey);
                var start = session.StartSinglePlayerAsync("Offline Dog", session.ModelCatalog.FallbackModelId, worldSlot);
                yield return new WaitUntil(() => start.IsCompleted);
                Assert.That(start.IsFaulted, Is.False);
                Assert.That(session.HasAvatar, Is.True, session.Message);
                Assert.That(session.IsSinglePlayer, Is.True);
                Assert.That(session.RoomCode, Is.Empty);
                yield return new WaitUntil(() => FindBall(session) != null);
                var ball = FindBall(session);
                var moved = new Vector3(-7, .28f, -6);
                ball.transform.position = moved;
                ball.GetComponent<Rigidbody>().position = moved;

                var leave = session.LeaveAsync();
                yield return new WaitUntil(() => leave.IsCompleted);
                Assert.That(leave.IsFaulted, Is.False);
                Assert.That(session.State, Is.EqualTo(SessionState.Disconnected));
                Assert.That(session.HasAvatar, Is.False);

                var reopen = session.StartSinglePlayerAsync("Offline Dog", session.ModelCatalog.FallbackModelId, worldSlot);
                yield return new WaitUntil(() => reopen.IsCompleted);
                Assert.That(session.HasAvatar, Is.True, session.Message);
                yield return new WaitUntil(() => FindBall(session) != null);
                Assert.That(FindBall(session).transform.position.x, Is.EqualTo(moved.x).Within(.1f));
                Assert.That(FindBall(session).transform.position.z, Is.EqualTo(moved.z).Within(.1f));
                var leaveReopened = session.LeaveAsync();
                yield return new WaitUntil(() => leaveReopened.IsCompleted);
            }
            finally
            {
                if (originalSave.Length == 0) PlayerPrefs.DeleteKey(saveKey);
                else PlayerPrefs.SetString(saveKey, originalSave);
                PlayerPrefs.Save();
                unload = SceneManager.UnloadSceneAsync(scene);
            }
            yield return unload;
        }

        private static NetworkCarryable FindBall(FusionSessionService session)
            => NetworkCarryable.Active.FirstOrDefault(carryable => carryable.Runner == session.LocalPlayer.Runner
                && carryable.name.Contains("Ball"));

        [UnityTest]
        public IEnumerator RuntimeRunnerRegistersShutdownAndCanLeaveAndRetry()
        {
            var root = new GameObject("Runtime runner test");
            var session = root.AddComponent<FusionSessionService>();
            var createRunner = typeof(FusionSessionService).GetMethod("CreateRunner",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var runnerField = typeof(FusionSessionService).GetField("runner",
                BindingFlags.Instance | BindingFlags.NonPublic);
            NetworkRunner runner = null;
            try
            {
                for (var attempt = 0; attempt < 2; attempt++)
                {
                    Assert.DoesNotThrow(() => createRunner.Invoke(session, null));
                    runner = (NetworkRunner)runnerField.GetValue(session);
                    var events = runner.GetComponent<NetworkEvents>();
                    Assert.That(events.OnShutdown, Is.Not.Null);
                    Assert.That(events.OnInput, Is.Not.Null);
                    Assert.That(events.PlayerJoined, Is.Not.Null);
                    Assert.That(events.PlayerLeft, Is.Not.Null);
                    var notified = false;
                    events.OnShutdown.AddListener((source, reason) =>
                        notified = source == runner && reason == ShutdownReason.Ok);
                    ((INetworkRunnerCallbacks)events).OnShutdown(runner, ShutdownReason.Ok);
                    Assert.That(notified, Is.True);

                    var leave = session.LeaveAsync();
                    yield return new WaitUntil(() => leave.IsCompleted);
                    Assert.That(leave.IsFaulted, Is.False);
                    Assert.That(session.State, Is.EqualTo(SessionState.Disconnected));
                    yield return null; // Allow the disposed runner's GameObject to be destroyed.
                    Assert.That(runner == null, Is.True);
                }
            }
            finally
            {
                Object.Destroy(root);
                if (runner != null) Object.Destroy(runner.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator InvalidRequestsCreateNoRunnerAndCanLeaveAndRetry()
        {
            var root = new GameObject("Session preflight test");
            var session = root.AddComponent<FusionSessionService>();
            var initialCount = Object.FindObjectsByType<NetworkRunner>(FindObjectsSortMode.None).Length;
            try
            {
                var invalidName = session.CreateAsync("A");
                yield return new WaitUntil(() => invalidName.IsCompleted);
                Assert.That(invalidName.IsFaulted, Is.False);
                Assert.That(session.State, Is.EqualTo(SessionState.Failed));
                var invalidCode = session.JoinAsync("Guest", "bad!");
                yield return new WaitUntil(() => invalidCode.IsCompleted);
                Assert.That(session.Message, Does.Contain("room code"));
                var leave = session.LeaveAsync();
                yield return new WaitUntil(() => leave.IsCompleted);
                Assert.That(session.State, Is.EqualTo(SessionState.Disconnected));
                Assert.That(session.RoomCode, Is.Empty);
                var missingSceneConfig = session.CreateAsync("Guest");
                yield return new WaitUntil(() => missingSceneConfig.IsCompleted);
                Assert.That(session.Message, Does.Contain("setup is incomplete"));
                Assert.That(Object.FindObjectsByType<NetworkRunner>(FindObjectsSortMode.None).Length, Is.EqualTo(initialCount));
            }
            finally { Object.Destroy(root); }
        }
    }
}
