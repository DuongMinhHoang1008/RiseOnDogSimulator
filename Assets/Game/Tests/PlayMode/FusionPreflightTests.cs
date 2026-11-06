using System.Collections;
using System.Reflection;
using DogSimulator.Networking;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEngine;
using UnityEngine.TestTools;

namespace DogSimulator.Tests
{
    public sealed class FusionPreflightTests
    {
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
