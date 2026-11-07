using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DogSimulator.Editor;
using DogSimulator.Networking;
using DogSimulator.Player;
using Fusion;
using Fusion.Photon.Realtime;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DogSimulator.Tests
{
    public sealed class FusionSetupTests
    {
        private NetworkConfig config;
        [SetUp] public void SetUp() => config = ScriptableObject.CreateInstance<NetworkConfig>();
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(config);

        [TestCase(false)]
        [TestCase(true)]
        public void MatchmakingStartsUnlistedAndExplicitAboutCreation(bool create)
        {
            var source = new FusionAppSettings { AppIdFusion = Guid.NewGuid().ToString(), FixedRegion = "eu", AppVersion = "other" };
            using (var cancellation = new CancellationTokenSource())
            {
                var args = FusionSessionOptions.Build(config, source, " abcd23 ", create, "nonce", cancellation.Token);
                Assert.That(args.GameMode, Is.EqualTo(GameMode.Shared));
                Assert.That(args.SessionName, Is.EqualTo("ABCD23"));
                Assert.That(args.EnableClientSessionCreation, Is.EqualTo(create));
                Assert.That(args.IsVisible, Is.False);
                Assert.That(args.IsOpen, Is.EqualTo(!create));
                Assert.That(args.PlayerCount, Is.EqualTo(8));
                Assert.That(args.CustomPhotonAppSettings.FixedRegion, Is.EqualTo(config.Region));
                Assert.That(args.CustomPhotonAppSettings.AppVersion, Is.EqualTo(config.GameVersion));
                Assert.That(args.CustomPhotonAppSettings, Is.Not.SameAs(source));
                Assert.That(source.FixedRegion, Is.EqualTo("eu"));
                Assert.That(source.AppVersion, Is.EqualTo("other"));
                Assert.That(args.StartGameCancellationToken, Is.EqualTo(cancellation.Token));
                if (create) Assert.That(FusionSessionOptions.MatchesCreation(args.SessionProperties, "nonce"), Is.True);
                else Assert.That(args.SessionProperties, Is.Null, "A named join must not write the creation marker.");
            }
        }

        [Test]
        public void CreationCollisionMustNotBeAcceptedAsOurRoom()
        {
            var properties = new Dictionary<string, SessionProperty> { [FusionSessionOptions.CreationKey] = "another creator" };
            Assert.That(FusionSessionOptions.MatchesCreation(properties, "our attempt"), Is.False);
            Assert.That(FusionSessionOptions.MatchesCreation(new Dictionary<string, SessionProperty>(), "our attempt"), Is.False);
            properties[FusionSessionOptions.CreationKey] = 123;
            Assert.That(FusionSessionOptions.MatchesCreation(properties, "our attempt"), Is.False);
        }

        [Test]
        public void ConcurrentPlayersReceiveSeparatedSpawnOffsets()
        {
            var offsets = Enumerable.Range(0, 8).Select(FusionSessionService.CalculateSpawnOffset).ToArray();
            for (var i = 0; i < offsets.Length; i++)
                for (var j = i + 1; j < offsets.Length; j++)
                    Assert.That(Vector3.Distance(offsets[i], offsets[j]), Is.GreaterThan(1.1f));
        }

        [Test]
        public void FusionPlayerIdsReceiveUniqueSpawnSlots()
        {
            var slots = Enumerable.Range(0, 8)
                .Select(index => FusionSessionService.CalculateSpawnSlot(PlayerRef.FromIndex(index)))
                .ToArray();

            CollectionAssert.AreEqual(Enumerable.Range(0, 8), slots);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("invalid")]
        [TestCase("00000000-0000-0000-0000-000000000000")]
        public void MissingOrInvalidAppIdIsRejectedBeforeConnecting(string id)
            => Assert.Throws<ArgumentException>(() => FusionSessionOptions.Build(config,
                new FusionAppSettings { AppIdFusion = id }, "ABCD23", false, null, default));

        [Test]
        public void GeneratedNetworkPrefabAndSceneRespectTheSingleDriverBoundary()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FusionPreviewSetup.PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponents<NetworkObject>().Length, Is.EqualTo(1));
            Assert.That(prefab.GetComponents<NetworkDogPlayer>().Length, Is.EqualTo(1));
            Assert.That(prefab.GetComponent<NetworkTransform>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<LocalMovementPreview>(), Is.Null);
            Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(prefab.GetComponent<NetworkObject>().Flags.HasFlag(NetworkObjectFlags.DestroyWhenStateAuthorityLeaves), Is.True);
            Assert.That(prefab.GetComponent<NetworkObject>().Flags.HasFlag(NetworkObjectFlags.AllowStateAuthorityOverride), Is.False);
            Assert.That(NetworkProjectConfig.Global.AssembliesToWeave, Does.Contain("DogSimulator.Fusion"));
            Assert.That(AssetDatabase.GetLabels(prefab), Does.Contain("FusionPrefab"));
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(FusionPreviewSetup.ScenePath);
            var wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(FusionPreviewSetup.ScenePath, OpenSceneMode.Additive);
            try
            {
                var behaviours = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                Assert.That(behaviours.Count(b => b is FusionSessionService), Is.EqualTo(1));
                Assert.That(behaviours.Count(b => b is FusionPreviewUI), Is.EqualTo(1));
                Assert.That(behaviours.Any(b => b is LocalMovementPreview || b is NetworkDogPlayer), Is.False,
                    "The network scene must wait for a successful connection before spawning an avatar.");
                foreach (var behaviour in behaviours.Where(b => b is FusionSessionService || b is FusionPreviewUI))
                {
                    var iterator = new SerializedObject(behaviour).GetIterator();
                    while (iterator.NextVisible(true))
                        if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                            Assert.That(iterator.objectReferenceValue, Is.Not.Null, behaviour.GetType().Name + "." + iterator.name);
                }
            }
            finally { if (!wasLoaded) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
