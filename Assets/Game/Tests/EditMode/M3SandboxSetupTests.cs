using System.Linq;
using DogSimulator.Editor;
using DogSimulator.Networking;
using DogSimulator.Player;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEditor;
using UnityEngine;

namespace DogSimulator.Tests
{
    public sealed class M3SandboxSetupTests
    {
        [Test]
        public void CarryablesAndRegistryAreNetworkReady()
        {
            var paths = new[] { M3SandboxSetup.BonePath, M3SandboxSetup.BallPath, M3SandboxSetup.CratePath };
            foreach (var path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                Assert.That(prefab.GetComponent<NetworkObject>(), Is.Not.Null, path);
                Assert.That(prefab.GetComponent<NetworkTransform>(), Is.Not.Null, path);
                Assert.That(prefab.GetComponent<NetworkCarryable>(), Is.Not.Null, path);
                Assert.That(prefab.GetComponent<Rigidbody>(), Is.Not.Null, path);
                Assert.That(AssetDatabase.GetLabels(prefab), Does.Contain("FusionPrefab"), path);
            }
            var registry = AssetDatabase.LoadAssetAtPath<GameObject>(M3SandboxSetup.RegistryPath);
            Assert.That(registry, Is.Not.Null);
            Assert.That(registry.GetComponent<SandboxInteractionRegistry>(), Is.Not.Null);
        }

        [Test]
        public void PlayerPrefabHasASeparateBackSupport()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FusionPreviewSetup.PrefabPath);
            var supports = prefab.GetComponentsInChildren<DogBackSupport>(true);
            Assert.That(supports.Length, Is.EqualTo(1));
            Assert.That(supports.Single().GetComponent<BoxCollider>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<CharacterController>(), Is.Not.Null);
        }
    }
}
