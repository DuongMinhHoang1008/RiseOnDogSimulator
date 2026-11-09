using System;
using System.Collections;
using System.Linq;
using DogSimulator.Networking;
using DogSimulator.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DogSimulator.Tests
{
    public sealed class SimplePolyCityTests
    {
        private const string ScenePath = "Assets/Game/Scenes/SimplePolyCity_Gameplay.unity";

        [UnityTest]
        public IEnumerator OfflineCitySpawnsItsWorldAndTaxiUsesNormalDrivingInput()
        {
            const string key = "DogSimulator." + SimplePolyCityWorld.SaveNamespace + ".WorldSlot.3";
            var original = PlayerPrefs.GetString(key);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var session = scene.GetRootGameObjects().Select(root => root.GetComponent<FusionSessionService>())
                .Single(service => service != null);
            var city = session.GetComponent<SimplePolyCityWorld>();
            AsyncOperation unload = null;
            GameObject leftBlock = null, rightBlock = null;
            try
            {
                PlayerPrefs.DeleteKey(key);
                var start = session.StartSinglePlayerAsync("City Test", session.ModelCatalog.FallbackModelId, 3);
                yield return Until(() => start.IsCompleted, "Fusion Single did not start.");
                Assert.That(session.HasAvatar, Is.True, session.Message);
                var dog = session.LocalPlayer;
                var controller = dog.GetComponent<CharacterController>();
                yield return Until(() => NetworkCarryable.Active.Count(prop => prop.Runner == dog.Runner) == city.Props.Length,
                    "City props did not spawn.");
                var vehicles = Object.FindObjectsByType<NetworkCityVehicle>(FindObjectsSortMode.None)
                    .Where(vehicle => vehicle.Runner == dog.Runner).ToArray();
                Assert.That(vehicles.Length, Is.EqualTo(city.Vehicles.Length));
                Assert.That(vehicles.Length, Is.EqualTo(104));
                Assert.That(vehicles.Count(vehicle => vehicle.IsTraffic), Is.EqualTo(3));
                Assert.That(vehicles.All(vehicle => vehicle.transform.Find("Driver Door") != null
                    && vehicle.GetComponent<Rigidbody>().mass >= 3000f), Is.True);
                Assert.That(city.Props.Count(prop => prop.prefab.name.Contains("Ball")), Is.GreaterThanOrEqualTo(4));
                Assert.That(city.Props.Select(prop => prop.id).Distinct().Count(), Is.EqualTo(city.Props.Length));
                yield return Until(() => controller.isGrounded, "Dog did not land at the city spawn.");

                var traffic = city.Vehicles.Single(vehicle => vehicle.route.Length > 0
                    && vehicle.placement.prefab.name == "Vehicle_Car_color02");
                var moving = vehicles.Single(vehicle => vehicle.IsTraffic
                    && vehicle.name.StartsWith(traffic.placement.prefab.name, StringComparison.Ordinal));
                var initial = moving.transform.position;
                var taxi = vehicles.Where(vehicle => vehicle.name.StartsWith("DriveableTaxi", StringComparison.Ordinal))
                    .OrderBy(vehicle => Mathf.Abs(vehicle.transform.position.x - 46.4f)).First();
                var door = taxi.transform.Find("Driver Door");
                var initialTaxi = taxi.transform.position;
                yield return Until(() => Vector3.Distance(moving.transform.position, initial) > 7f, "Traffic did not follow its lane.");
                Assert.That(Vector3.Distance(taxi.transform.position, initialTaxi), Is.LessThan(.1f));

                var crossing = moving.transform.position + moving.transform.forward * 4.4f;
                PlaceDog(dog, crossing, -moving.transform.forward);
                moving.GetComponent<Rigidbody>().linearVelocity = moving.transform.forward * 15f;
                yield return Until(() => dog.KnockdownActive, "A high-speed hit did not knock the dog down.");
                yield return Until(() => !dog.KnockdownActive, "The dog did not recover after the hit.");

                controller.enabled = false;
                var entrance = door.position + door.right * -0.4f;
                entrance.y = taxi.transform.position.y + .05f;
                dog.transform.SetPositionAndRotation(entrance, taxi.transform.rotation);
                controller.enabled = true;
                dog.SetControlsEnabled(true);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return Until(() => taxi.DriverId == dog.Object.Id, "E did not claim the taxi seat.");
                Assert.That(dog.SeatedVehicleId, Is.EqualTo(taxi.Object.Id));
                Assert.That(controller.enabled, Is.False);
                Assert.That(taxi.GetComponent<NetworkCarryable>(), Is.Null);

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return Until(() => Vector3.Distance(taxi.transform.position, initialTaxi) > 3f, "W did not drive the taxi.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                var body = taxi.GetComponent<Rigidbody>();
                yield return Until(() => body.linearVelocity.magnitude < .3f, "Taxi did not brake.");
                leftBlock = Block(taxi.transform.Find("Left Exit"));
                rightBlock = Block(taxi.transform.Find("Right Exit"));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(dog.SeatedVehicleId, Is.EqualTo(taxi.Object.Id));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Object.Destroy(rightBlock);
                yield return null;
                yield return new WaitForFixedUpdate();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return Until(() => !dog.SeatedVehicleId.IsValid, "E did not use the clear exit.");
                Assert.That(controller.enabled, Is.True);
                Assert.That(taxi.DriverId.IsValid, Is.False);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                var leave = session.LeaveAsync();
                yield return Until(() => leave.IsCompleted, "Fusion Single did not close.");
                Assert.That(session.HasAvatar, Is.False);
            }
            finally
            {
                _ = session.LeaveAsync();
                Object.Destroy(leftBlock);
                Object.Destroy(rightBlock);
                InputSystem.RemoveDevice(keyboard);
                if (original.Length == 0) PlayerPrefs.DeleteKey(key);
                else PlayerPrefs.SetString(key, original);
                PlayerPrefs.Save();
                unload = SceneManager.UnloadSceneAsync(scene);
            }
            yield return unload;
        }

        [UnityTest]
        public IEnumerator PedestrianPatrolsFlinchesAtThrowAndRetaliatesAfterBite()
        {
            const string key = "DogSimulator." + SimplePolyCityWorld.SaveNamespace + ".WorldSlot.3";
            var original = PlayerPrefs.GetString(key);
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var session = scene.GetRootGameObjects().Select(root => root.GetComponent<FusionSessionService>())
                .Single(service => service != null);
            AsyncOperation unload = null;
            try
            {
                PlayerPrefs.DeleteKey(key);
                var start = session.StartSinglePlayerAsync("Pedestrian Test", session.ModelCatalog.FallbackModelId, 3);
                yield return Until(() => start.IsCompleted, "Fusion Single did not start.");
                Assert.That(session.HasAvatar, Is.True, session.Message);
                var dog = session.LocalPlayer;
                var city = session.GetComponent<SimplePolyCityWorld>();
                yield return Until(() => NetworkCityPedestrian.Active.Count == city.Pedestrians.Length, "NPCs did not spawn.");
                Assert.That(city.Pedestrians.Length, Is.EqualTo(5));
                var npc = NetworkCityPedestrian.Active[0];
                var origin = npc.transform.position;
                yield return Until(() => Vector3.Distance(origin, npc.transform.position) > .75f, "NPC did not patrol.");
                var ball = NetworkCarryable.Active.First(prop => prop.name.StartsWith("CarryableBall", StringComparison.Ordinal));

                var forward = npc.transform.forward;
                PlaceDog(dog, npc.transform.position + forward * 2.8f, -forward);
                Assert.That(ball.TryPickup(dog), Is.True);
                Assert.That(ball.TryThrow(dog), Is.True);
                yield return Until(() => !npc.FlinchUntil.ExpiredOrNotRunning(npc.Runner), "Thrown ball did not reach NPC.");
                Assert.That(npc.AggroTarget.IsValid, Is.False);
                ball.ResetToSpawn();
                yield return Until(() => npc.FlinchUntil.ExpiredOrNotRunning(npc.Runner), "NPC did not recover from throw.");

                forward = npc.transform.forward;
                PlaceDog(dog, npc.transform.position + forward * 1.5f, -forward);
                yield return Until(() => dog.GetComponent<CharacterController>().isGrounded, "Dog is not grounded.");
                // The input suite checks the right-click binding. Start the action here so
                // this world simulation test is independent of Editor pointer focus.
                var animation = dog.GetComponent<DogAnimationDriver>();
                animation.Simulate(Vector2.zero, false, false, DogAction.Bite, true, dog.transform.eulerAngles.y, .02f);
                Assert.That(animation.CurrentAction, Is.EqualTo(DogAction.Bite));
                yield return Until(() => npc.AggroTarget == dog.Object.Id, "Bite did not aggro NPC.");
                yield return Until(() => dog.KnockdownActive, "NPC did not retaliate.");

                var leave = session.LeaveAsync();
                yield return Until(() => leave.IsCompleted, "Fusion Single did not close.");
            }
            finally
            {
                _ = session.LeaveAsync();
                if (original.Length == 0) PlayerPrefs.DeleteKey(key);
                else PlayerPrefs.SetString(key, original);
                PlayerPrefs.Save();
                unload = SceneManager.UnloadSceneAsync(scene);
            }
            yield return unload;
        }

        private static void PlaceDog(NetworkDogPlayer dog, Vector3 point, Vector3 forward)
        {
            var controller = dog.GetComponent<CharacterController>();
            controller.enabled = false;
            dog.transform.SetPositionAndRotation(point + Vector3.up * .05f, Quaternion.LookRotation(forward));
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private static GameObject Block(Transform exit)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.transform.position = exit.position + Vector3.up * .6f;
            return block;
        }

        private static IEnumerator Until(Func<bool> ready, string failure)
        {
            var deadline = Time.realtimeSinceStartup + 9f;
            while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(ready(), Is.True, failure);
        }
    }
}
