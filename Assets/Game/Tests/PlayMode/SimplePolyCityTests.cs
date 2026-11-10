using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using DogSimulator.Networking;
using DogSimulator.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
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

        [UnityTearDown]
        public IEnumerator CloseCityAfterFailure()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) yield break;
            var session = scene.GetRootGameObjects().Select(root => root.GetComponent<FusionSessionService>())
                .FirstOrDefault(service => service != null);
            if (session != null)
            {
                var leave = session.LeaveAsync();
                yield return Until(() => leave.IsCompleted, "Fusion Single did not close during cleanup.");
            }
            var unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null) yield return unload;
        }

        [UnityTest]
        public IEnumerator OfflineCitySpawnsItsWorldAndTaxiUsesNormalDrivingInput()
        {
            const string key = "DogSimulator." + SimplePolyCityWorld.SaveNamespace + ".WorldSlot.3";
            var original = PlayerPrefs.GetString(key);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            if (!SceneManager.GetSceneByPath(ScenePath).isLoaded)
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
                yield return Until(() => NetworkCarryable.Active.Count(prop => prop.Runner == dog.Runner && !IsJailBowl(prop))
                    == city.Props.Length + 2,
                    "City props did not spawn.");
                var vehicles = Object.FindObjectsByType<NetworkCityVehicle>(FindObjectsSortMode.None)
                    .Where(vehicle => vehicle.Runner == dog.Runner).ToArray();
                Assert.That(vehicles.Length, Is.EqualTo(city.Vehicles.Length));
                Assert.That(vehicles.Length, Is.EqualTo(104));
                Assert.That(vehicles.Count(vehicle => vehicle.IsTraffic), Is.EqualTo(6));
                Assert.That(vehicles.All(vehicle => vehicle.transform.Find("Driver Door") != null
                    && vehicle.GetComponent<Rigidbody>().mass >= 4500f), Is.True);
                Assert.That(city.Props.Count(prop => prop.prefab.name.Contains("Ball")), Is.GreaterThanOrEqualTo(4));
                Assert.That(city.Props.Select(prop => prop.id).Distinct().Count(), Is.EqualTo(city.Props.Length));
                yield return Until(() => controller.isGrounded, "Dog did not land at the city spawn.");

                var traffic = city.Vehicles.First(vehicle => vehicle.route.Length > 0
                    && vehicle.placement.prefab.name == "Vehicle_Car_color02" && vehicle.placement.position.z < 0);
                var moving = vehicles.Where(vehicle => vehicle.IsTraffic
                    && vehicle.name.StartsWith(traffic.placement.prefab.name, StringComparison.Ordinal))
                    .OrderBy(vehicle => Vector3.Distance(vehicle.transform.position, traffic.placement.position)).First();
                var trafficStart = vehicles.Where(vehicle => vehicle.IsTraffic)
                    .ToDictionary(vehicle => vehicle, vehicle => vehicle.transform.position);
                var taxi = vehicles.Where(vehicle => vehicle.name.StartsWith("DriveableTaxi", StringComparison.Ordinal))
                    .OrderBy(vehicle => Mathf.Abs(vehicle.transform.position.x - 46.4f)).First();
                var door = taxi.transform.Find("Driver Door");
                var initialTaxi = taxi.transform.position;
                yield return Until(() => trafficStart.All(entry => Vector3.Distance(entry.Key.transform.position, entry.Value) > 7f),
                    "One or more traffic cars did not follow their lanes.");
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
                yield return Until(() => taxi.DriverId == dog.Object.Id, () =>
                    $"E did not claim taxi. Dog {dog.transform.position}, door {door.position}, seated {dog.SeatedVehicleId}, down {dog.KnockdownActive}, speed {taxi.GetComponent<Rigidbody>().linearVelocity.magnitude:F2}, focus {Application.isFocused}.");
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
            if (!SceneManager.GetSceneByPath(ScenePath).isLoaded)
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
                yield return Until(() => NetworkCityPedestrian.Active.Count(npc => !IsPolice(npc))
                    == city.Pedestrians.Length + Course<(Vector3, Vector3)[]>("GuardPatrols").Length, "NPCs did not spawn.");
                Assert.That(city.Pedestrians.Length, Is.EqualTo(16));
                var npc = NetworkCityPedestrian.Active.First(npc => !IsPolice(npc));
                var origin = npc.transform.position;
                yield return Until(() => Vector3.Distance(origin, npc.transform.position) > .75f, "NPC did not patrol.");
                var ball = NetworkCarryable.Active.First(prop => prop.name.StartsWith("CarryableBall", StringComparison.Ordinal));

                var forward = npc.transform.forward;
                PlaceDog(dog, npc.transform.position + forward * 2.8f, -forward);
                ball.transform.position = dog.MouthAnchor.position + dog.transform.forward * .55f;
                ball.GetComponent<Rigidbody>().position = ball.transform.position;
                Physics.SyncTransforms();
                Assert.That(ball.TryPickup(dog), Is.True);
                Assert.That(ball.TryThrow(dog), Is.True);
                yield return Until(() => !npc.FlinchUntil.ExpiredOrNotRunning(npc.Runner), "Thrown ball did not reach NPC.");
                Assert.That(npc.AggroTarget.IsValid, Is.False);
                ball.ResetToSpawn();
                yield return Until(() => npc.FlinchUntil.ExpiredOrNotRunning(npc.Runner), "NPC did not recover from throw.");

                Assert.That(npc.GetComponent<NavMeshAgent>().Warp(new Vector3(212f, .08f, 105f)), Is.True);
                PlaceDog(dog, new Vector3(213.2f, .03f, 105f), Vector3.left);
                // Start the bite on a grounded simulation step regardless of editor pointer focus.
                var animation = dog.GetComponent<DogAnimationDriver>();
                var controller = dog.GetComponent<CharacterController>();
                yield return Until(() =>
                {
                    if (!controller.isGrounded || dog.KnockdownActive) return false;
                    animation.Simulate(Vector2.zero, false, false, DogAction.Bite, true, dog.transform.eulerAngles.y, .02f);
                    return animation.CurrentAction == DogAction.Bite;
                }, () => $"Dog did not start a grounded bite. Position {dog.transform.position}, grounded {controller.isGrounded}, action {animation.CurrentAction}, down {dog.KnockdownActive}.");
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

        [UnityTest]
        public IEnumerator VehicleHitLetsDogCarryAndThrowNpcBeforeRecovery()
        {
            const string key = "DogSimulator." + SimplePolyCityWorld.SaveNamespace + ".WorldSlot.3";
            var original = PlayerPrefs.GetString(key);
            if (!SceneManager.GetSceneByPath(ScenePath).isLoaded)
                yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var session = scene.GetRootGameObjects().Select(root => root.GetComponent<FusionSessionService>())
                .Single(service => service != null);
            AsyncOperation unload = null;
            try
            {
                PlayerPrefs.DeleteKey(key);
                var start = session.StartSinglePlayerAsync("NPC Carry Test", session.ModelCatalog.FallbackModelId, 3);
                yield return Until(() => start.IsCompleted, "Fusion Single did not start.");
                Assert.That(session.HasAvatar, Is.True, session.Message);
                var dog = session.LocalPlayer;
                var city = session.GetComponent<SimplePolyCityWorld>();
                yield return Until(() => NetworkCityPedestrian.Active.Count(npc => npc.Runner == dog.Runner && !IsPolice(npc))
                    == city.Pedestrians.Length + Course<(Vector3, Vector3)[]>("GuardPatrols").Length, "NPCs did not spawn.");
                var pedestrian = NetworkCityPedestrian.Active.First(npc => npc.Runner == dog.Runner && !IsPolice(npc));
                var agent = pedestrian.GetComponent<NavMeshAgent>();
                Assert.That(agent.enabled, Is.True);
                Assert.That(agent.Warp(new Vector3(100f, .03f, 60f)), Is.True);
                var npcBody = pedestrian.GetComponent<Rigidbody>();
                var car = Object.FindObjectsByType<NetworkCityVehicle>(FindObjectsSortMode.None)
                    .First(item => item.Runner == dog.Runner && !item.IsTraffic && item.name.StartsWith("DriveableTaxi"));
                var carBody = car.GetComponent<Rigidbody>();
                var point = pedestrian.transform.position;
                carBody.position = point + Vector3.right * 5.1f;
                carBody.rotation = Quaternion.LookRotation(Vector3.left);
                carBody.linearVelocity = Vector3.left * 14f;
                Physics.SyncTransforms();
                yield return Until(() => !agent.enabled && !npcBody.isKinematic, () =>
                    $"Vehicle contact did not knock the NPC down. Car {carBody.position}, NPC {npcBody.position}, speed {carBody.linearVelocity.magnitude:F2}.");
                yield return Until(() => npcBody.isKinematic, "NPC did not settle after impact.");
                Assert.That(agent.enabled, Is.False);

                point = pedestrian.transform.position;
                PlaceDog(dog, point + Vector3.back * 1.25f, Vector3.forward);
                SubmitPrimaryCarry(dog);
                yield return Until(() => pedestrian.CarrierId == dog.Object.Id, "Carry request did not claim the prone NPC.");
                Assert.That(agent.enabled, Is.False);
                Assert.That(npcBody.isKinematic, Is.True);
                SubmitPrimaryCarry(dog);
                yield return Until(() => !pedestrian.CarrierId.IsValid && !npcBody.isKinematic, "Throw request did not release the NPC.");
                yield return Until(() => agent.enabled && agent.isOnNavMesh, "NPC did not recover on the NavMesh.");

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

        [UnityTest]
        public IEnumerator GoldenBoneGateAndResetScopesInFusionSingle()
        {
            const string key = "DogSimulator." + SimplePolyCityWorld.SaveNamespace + ".WorldSlot.3";
            var original = PlayerPrefs.GetString(key);
            if (!SceneManager.GetSceneByPath(ScenePath).isLoaded)
                yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var session = scene.GetRootGameObjects().Select(root => root.GetComponent<FusionSessionService>())
                .Single(service => service != null);
            AsyncOperation unload = null;
            try
            {
                PlayerPrefs.DeleteKey(key);
                var start = session.StartSinglePlayerAsync("Mission Test", session.ModelCatalog.FallbackModelId, 3);
                yield return Until(() => start.IsCompleted, "Fusion Single did not start.");
                Assert.That(session.HasAvatar, Is.True, session.Message);
                var dog = session.LocalPlayer;
                var registry = Object.FindObjectsByType<SandboxInteractionRegistry>(FindObjectsSortMode.None)
                    .Single(item => item.Runner == dog.Runner);
                yield return Until(() => registry.GoldenBoneId.IsValid, "Golden Bone did not spawn.");
                var bone = NetworkCarryable.Active.Single(item => item.Runner == dog.Runner
                    && item.Object.Id == registry.GoldenBoneId);
                var crateSpawn = Course<Vector3>("CrateSpawn");
                var plate = Course<Vector3>("Switch");
                var gate = Course<Vector3>("GateClosed");
                var delivery = Course<Bounds>("Delivery");
                var boneApproach = bone.ResetPosition + new Vector3(-1.45f, -.98f, 0);
                Assert.That(Vector3.Distance(dog.transform.position, bone.ResetPosition), Is.GreaterThan(90f));
                Assert.That(Vector3.Distance(bone.ResetPosition, delivery.center), Is.GreaterThan(70f));
                var crate = NetworkCarryable.Active.Single(item => item.Runner == dog.Runner
                    && item.name.StartsWith("CarryableCrate", StringComparison.Ordinal)
                    && Vector3.Distance(item.ResetPosition, crateSpawn) < .01f);
                var otherProp = NetworkCarryable.Active.First(item => item.Runner == dog.Runner && item != bone
                    && item != crate && !IsJailBowl(item));
                var boneId = bone.Object.Id;

                crate.GetComponent<Rigidbody>().position = plate + Vector3.up * .33f;
                yield return Until(() => registry.GateOpen && registry.PlatePressed, "Crate did not open the gate.");
                PlaceDog(dog, gate - Vector3.up * .9f, Vector3.right);
                crate.ResetToSpawn();
                yield return Until(() => !registry.PlatePressed, "Pressure plate did not release.");
                Assert.That((bool)registry.GateOpen, Is.True, "Gate closed on the dog.");
                PlaceDog(dog, boneApproach, Vector3.right);
                yield return Until(() => !registry.GateOpen, "Clear gate did not close.");

                Assert.That(bone.TryPickup(dog), Is.True);
                crate.GetComponent<Rigidbody>().position = dog.MouthAnchor.position + dog.transform.forward * .35f;
                Physics.SyncTransforms();
                Assert.That(crate.TryPickup(dog), Is.False, "A second item occupied the same mouth.");
                crate.ResetToSpawn();

                var padCenter = Course<Vector3>("PadCenter") + Vector3.up * .91f;
                Assert.That(Physics.Raycast(padCenter, Vector3.down, out var padHit, 1f), Is.True);
                Assert.That(padHit.collider.GetComponent<BouncePad>(), Is.Not.Null);
                PlaceDog(dog, padCenter - Vector3.up * .2f, Vector3.left);
                var motor = dog.GetComponent<DogMotor>();
                yield return Until(() => dog.transform.position.y > padCenter.y + .35f,
                    "Dog did not gain height from the bounce pad.");
                for (var step = 0; step < 65; step++)
                    motor.Simulate(Vector2.up, true, false, 270f, .02f);
                Assert.That(dog.transform.position.x, Is.LessThan(gate.x - .4f), "Dog did not clear the low fence.");
                yield return Until(() => motor.IsGrounded, "Dog did not land.");
                Assert.That(bone.IsCarried, Is.True, "Dog dropped the bone during the pad jump.");

                yield return Until(() => registry.GoldenBoneAlert && NetworkCityPedestrian.Active.Any(npc =>
                    npc.Runner == dog.Runner && npc.AggroTarget == dog.Object.Id), "Guards did not pursue the dog after theft.");
                var westPatrol = Course<(Vector3 start, Vector3 end)[]>("GuardPatrols")[2];
                PlaceDog(dog, westPatrol.start + Vector3.right * 1.8f, Vector3.back);
                Assert.That(bone.TryDrop(dog), Is.True);
                yield return Until(() => NetworkCityPedestrian.Active.Any(npc => npc.Runner == dog.Runner
                    && bone.CarrierId == npc.Object.Id), "No guard recovered the dropped bone.");
                yield return Until(() => !bone.IsCarried && !registry.GoldenBoneAlert
                    && Vector3.Distance(bone.transform.position, bone.ResetPosition) < .5f,
                    () => $"Guard did not return the bone. Bone {bone.transform.position}, carrier {bone.CarrierId}, alert {registry.GoldenBoneAlert}.", 18f);
                Assert.That((bool)registry.GoldenBoneComplete, Is.False);

                PlaceDog(dog, boneApproach, Vector3.right);
                yield return Until(() => bone.TryPickup(dog), "Dog could not take the returned bone again.");
                PlaceDog(dog, delivery.center - Vector3.up * 1.2f + Vector3.back * .85f, Vector3.forward);
                yield return Until(() => registry.GoldenBoneComplete, () =>
                    $"Carried bone did not score. Dog {dog.transform.position}, bone {bone.transform.position}, carried {bone.IsCarried}.");
                var otherRevision = otherProp.Revision;
                SubmitReset(dog, SandboxInteractionKind.RespawnBone);
                yield return Until(() => !bone.IsCarried, "F6 did not return the bone.");
                Assert.That((bool)registry.GoldenBoneComplete, Is.True);
                Assert.That(bone.Object.Id, Is.EqualTo(boneId));
                Assert.That(otherProp.Revision, Is.EqualTo(otherRevision));

                SubmitReset(dog, SandboxInteractionKind.ResetMission);
                yield return Until(() => !registry.GoldenBoneComplete, "F7 did not restart the mission.");
                Assert.That(otherProp.Revision, Is.EqualTo(otherRevision));

                var body = bone.GetComponent<Rigidbody>();
                body.position = delivery.center - Vector3.right * 2f;
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That((bool)registry.GoldenBoneComplete, Is.False);
                body.position = delivery.center + Vector3.right * 2f;
                yield return Until(() => registry.GoldenBoneComplete, "A bone crossing the zone between samples did not score.");

                SubmitReset(dog, SandboxInteractionKind.ResetYard);
                yield return Until(() => !registry.GoldenBoneComplete && otherProp.Revision != otherRevision,
                    "Sandbox reset did not restart the mission and reset outside props.");
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

        [UnityTest]
        public IEnumerator GoldenBoneResetCommandsKeepTheirSeparateScopes()
        {
            const int slot = 3;
            const string key = "DogSimulator." + SimplePolyCityWorld.SaveNamespace + ".WorldSlot.3";
            var hadSave = PlayerPrefs.HasKey(key);
            var original = PlayerPrefs.GetString(key);
            if (!SceneManager.GetSceneByPath(ScenePath).isLoaded)
                yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var session = scene.GetRootGameObjects().Select(root => root.GetComponent<FusionSessionService>())
                .Single(service => service != null);
            try
            {
                PlayerPrefs.DeleteKey(key);
                var start = session.StartSinglePlayerAsync("Reset Test", session.ModelCatalog.FallbackModelId, slot);
                yield return Until(() => start.IsCompleted, "Fusion Single did not start.");
                Assert.That(session.HasAvatar, Is.True, session.Message);
                var dog = session.LocalPlayer;
                var registry = Object.FindObjectsByType<SandboxInteractionRegistry>(FindObjectsSortMode.None)
                    .Single(item => item.Runner == dog.Runner);
                yield return Until(() => registry.GoldenBoneId.IsValid, "Golden Bone did not spawn.");
                var props = NetworkCarryable.Active.Where(item => item.Runner == dog.Runner).ToArray();
                var bone = props.Single(item => item.Object.Id == registry.GoldenBoneId);
                var crateSpawn = Course<Vector3>("CrateSpawn");
                var crate = props.Single(item => Vector3.Distance(item.ResetPosition, crateSpawn) < .01f);
                var ball = props.First(item => item.name.StartsWith("CarryableBall", StringComparison.Ordinal));
                var boneRevision = bone.Revision;
                var crateRevision = crate.Revision;
                var ballRevision = ball.Revision;

                SubmitReset(dog, SandboxInteractionKind.RespawnBone);
                yield return Until(() => bone.Revision > boneRevision, "Bone respawn did not reset the bone.");
                Assert.That(crate.Revision, Is.EqualTo(crateRevision));
                Assert.That(ball.Revision, Is.EqualTo(ballRevision));

                boneRevision = bone.Revision;
                SubmitReset(dog, SandboxInteractionKind.ResetMission);
                yield return Until(() => crate.Revision > crateRevision, "Mission reset did not reset the crate.");
                Assert.That(bone.Revision, Is.GreaterThan(boneRevision));
                Assert.That(ball.Revision, Is.EqualTo(ballRevision));

                SubmitReset(dog, SandboxInteractionKind.ResetYard);
                yield return Until(() => ball.Revision > ballRevision, "Yard reset did not reset the city ball.");
            }
            finally
            {
                _ = session.LeaveAsync();
                if (hadSave) PlayerPrefs.SetString(key, original);
                else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        private static T Course<T>(string field) => (T)typeof(SimplePolyCityWorld).Assembly
            .GetType("DogSimulator.Networking.GoldenBoneCourse", true)
            .GetField(field, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        private static bool IsJailBowl(NetworkCarryable item) => Flag(item, "IsJailBowl");

        private static bool IsPolice(NetworkCityPedestrian npc) => Flag(npc, "IsPolice");

        private static bool Flag(object instance, string property) => (bool)(Fusion.NetworkBool)instance.GetType()
            .GetProperty(property, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);

        [UnityTest]
        public IEnumerator WantedDogCanOnlyEscapeByCrossingOpenedJailGate()
        {
            const string key = "DogSimulator." + SimplePolyCityWorld.SaveNamespace + ".WorldSlot.3";
            var hadSave = PlayerPrefs.HasKey(key);
            var original = PlayerPrefs.GetString(key);
            if (!SceneManager.GetSceneByPath(ScenePath).isLoaded)
                yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Additive);
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var session = scene.GetRootGameObjects().Select(root => root.GetComponent<FusionSessionService>())
                .Single(service => service != null);
            try
            {
                PlayerPrefs.DeleteKey(key);
                yield return ExerciseJail(session);
            }
            finally
            {
                _ = session.LeaveAsync();
                if (hadSave) PlayerPrefs.SetString(key, original);
                else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        private static IEnumerator ExerciseJail(FusionSessionService session)
        {
            var start = session.StartSinglePlayerAsync("Jail Test", session.ModelCatalog.FallbackModelId, 3);
            yield return Until(() => start.IsCompleted, "Fusion Single did not start.");
            Assert.That(session.HasAvatar, Is.True, session.Message);
            var dog = session.LocalPlayer;
            yield return Until(() => NetworkCityPedestrian.Active.Count(npc => npc.Runner == dog.Runner && IsPolice(npc)) == 2,
                "Expected two dedicated officers and a valid jail site.");
            var registry = Object.FindObjectsByType<SandboxInteractionRegistry>(FindObjectsSortMode.None)
                .Single(item => item.Runner == dog.Runner);
            var bowl = NetworkCarryable.Active.Single(item => item.Runner == dog.Runner && IsJailBowl(item));
            var officer = NetworkCityPedestrian.Active.First(npc => npc.Runner == dog.Runner && IsPolice(npc));
            var gate = typeof(SandboxInteractionRegistry).GetProperty("JailGate", BindingFlags.Instance | BindingFlags.NonPublic);
            var origin = (Vector3)typeof(SandboxInteractionRegistry)
                .GetProperty("JailPosition", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(registry);
            var record = typeof(NetworkDogPlayer).GetMethod("RecordIncident", BindingFlags.Instance | BindingFlags.NonPublic);
            var bite = Enum.Parse(record.GetParameters()[1].ParameterType, "Bite");
            PlaceDog(dog, officer.transform.position + Vector3.forward * 1.25f, Vector3.back);
            yield return null;
            foreach (var npc in NetworkCityPedestrian.Active.Where(npc => npc.Runner == dog.Runner && !IsPolice(npc)).Take(4))
            {
                record.Invoke(dog, new[] { npc, bite });
                record.Invoke(dog, new[] { npc, bite });
            }
            Assert.That(dog.WantedHeat, Is.EqualTo(48f), "Repeated hits within the pair cooldown must count once.");
            Assert.That(dog.WantedLevel, Is.EqualTo(2));
            yield return Until(() => dog.InJail, "Police did not knock down and arrest a wanted dog.");
            var heldHeat = dog.WantedHeat;
            dog.GetComponent<DogMotor>().Respawn();
            SubmitReset(dog, SandboxInteractionKind.ResetMission);
            yield return Until(() => Vector3.Distance(origin, dog.transform.position) < 6f,
                "Avatar respawn bypassed jail.");
            Assert.That((bool)dog.InJail, Is.True);
            Assert.That((byte)gate.GetValue(registry), Is.Zero);
            Assert.That(dog.WantedHeat, Is.EqualTo(heldHeat));

            var prefab = (Fusion.NetworkObject)typeof(FusionSessionService)
                .GetField("playerPrefab", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session);
            var rescuer = dog.Runner.Spawn(prefab, origin + new Vector3(2.35f, .1f, 8f),
                Quaternion.Euler(0, 180, 0)).GetComponent<NetworkDogPlayer>();
            // Fusion Single gives both avatars the same InputAuthority. Exercise the host's
            // validation directly, independently of the multiplayer request sequence.
            var useJail = typeof(SandboxInteractionRegistry).GetMethod("UseJail", BindingFlags.Instance | BindingFlags.NonPublic);
            var rescueArgs = new object[] { rescuer };
            useJail.Invoke(registry, rescueArgs);
            Assert.That((byte)gate.GetValue(registry), Is.Zero, "The rescue lever accepted an out-of-range request.");
            PlaceDog(rescuer, origin + new Vector3(2.35f, 0, 5.4f), Vector3.back);
            var canRescue = typeof(SandboxInteractionRegistry).GetMethod("CanUseJailLever",
                BindingFlags.Instance | BindingFlags.NonPublic);
            yield return Until(() => (bool)canRescue.Invoke(registry, rescueArgs),
                () => $"The outside lever is unreachable. Rescuer offset {rescuer.transform.position - origin}, "
                    + $"mouth offset {rescuer.MouthAnchor.position - origin}, jailed {dog.InJail}.", 3f);
            useJail.Invoke(registry, rescueArgs);
            Assert.That((byte)gate.GetValue(registry), Is.EqualTo(2), "The outside lever did not open the jail.");
            Assert.That((bool)dog.InJail, Is.True, "The rescue lever must not release a dog still in the cell.");
            Assert.That(rescuer.WantedHeat, Is.Zero);
            dog.Runner.Despawn(rescuer.Object);
            yield return Until(() => (byte)gate.GetValue(registry) == 0, "The rescue gate did not close after its open window.");

            Assert.That((bool)dog.InJail, Is.True, "Closing the gate must not release a prisoner.");
            Assert.That(dog.WantedHeat, Is.EqualTo(heldHeat));
            bowl.GetComponent<Rigidbody>().position = origin + Vector3.down * 2f;
            yield return Until(() => Vector3.Distance(bowl.transform.position, bowl.ResetPosition) < .75f,
                "The puzzle prop did not recover after leaving the cell.");
            Assert.That((byte)gate.GetValue(registry), Is.Zero, "Recovering the bowl must not open the gate.");

            PlaceDog(dog, bowl.transform.position + new Vector3(0, -.16f, 1.4f), Vector3.back);
            Assert.That(bowl.TryPickup(dog), Is.True);
            PlaceDog(dog, origin + new Vector3(3f, 0, .1f), Vector3.back);
            Assert.That(bowl.TryDrop(dog), Is.True);
            bowl.GetComponent<Rigidbody>().position = origin + new Vector3(3f, .45f, -1.5f);
            yield return Until(() => (byte)gate.GetValue(registry) == 1, "Placing the bowl did not open the jail.");
            Assert.That((bool)dog.InJail, Is.True, "Opening the gate must not release anyone.");
            var obstruction = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                obstruction.transform.position = origin + new Vector3(.9f, .7f, 4f);
                obstruction.AddComponent<Rigidbody>().isKinematic = true;
                Physics.SyncTransforms();
                yield return Until(() => (byte)gate.GetValue(registry) == 3,
                    () => $"The occupied doorway did not hold the gate open. Gate {gate.GetValue(registry)}.", 12f);
                PlaceDog(dog, origin + new Vector3(0, 0, 3.45f), Vector3.forward);
                yield return new WaitForSecondsRealtime(.12f);
                PlaceDog(dog, origin + new Vector3(0, 0, 5.55f), Vector3.forward);
                yield return Until(() => !dog.InJail, "Crossing the open gate did not release the dog.");
                Assert.That(dog.WantedHeat, Is.Zero);
                PlaceDog(dog, session.GetComponent<SimplePolyCityWorld>().SafeSpawns[0], Vector3.forward);
            }
            finally
            {
                Object.Destroy(obstruction);
            }
            yield return Until(() => (byte)gate.GetValue(registry) == 0,
                () => $"The gate did not close after the exit cleared. Gate {gate.GetValue(registry)}, "
                    + $"dog offset {dog.transform.position - origin}.");
        }

        // Exercise the host request path independently of focus on the Editor's Game View.
        private static void SubmitPrimaryCarry(NetworkDogPlayer dog) => typeof(NetworkDogPlayer)
            .GetMethod("SubmitPrimaryCarryInteraction", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(dog, null);

        private static void SubmitReset(NetworkDogPlayer dog, SandboxInteractionKind kind) => typeof(NetworkDogPlayer)
            .GetMethod("SubmitInteraction", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(dog, new object[] { Fusion.NetworkId.None, kind });

        private static void PlaceDog(NetworkDogPlayer dog, Vector3 point, Vector3 forward)
        {
            var controller = dog.GetComponent<CharacterController>();
            controller.enabled = false;
            dog.transform.SetPositionAndRotation(point + Vector3.up * .05f, Quaternion.LookRotation(forward));
            dog.GetComponent<DogMotor>().ClearMotion();
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private static GameObject Block(Transform exit)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.transform.position = exit.position + Vector3.up * .6f;
            return block;
        }

        private static IEnumerator Until(Func<bool> ready, string failure) => Until(ready, () => failure);

        private static IEnumerator Until(Func<bool> ready, Func<string> failure, float timeout = 9f)
        {
            var deadline = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (ready()) yield break;
                yield return null;
            }
            Assert.Fail(failure());
        }
    }
}
