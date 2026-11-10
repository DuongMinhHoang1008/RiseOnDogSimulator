using System.Collections;
using DogSimulator.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DogSimulator.Tests
{
    public sealed class DogMotorTests
    {
        private GameObject dog;
        private GameObject floor;
        private DogMotor motor;
        private DogMovementConfig config;
        private static readonly Vector3 Spawn = new Vector3(200, 0.1f, 200);

        [UnitySetUp]
        public IEnumerator Setup()
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = Spawn + new Vector3(0, -0.35f, 0);
            floor.transform.localScale = new Vector3(100, 0.5f, 100);
            dog = new GameObject("Motor test dog");
            dog.SetActive(false);
            dog.transform.position = Spawn;
            var controller = dog.AddComponent<CharacterController>();
            controller.height = 1;
            controller.radius = 0.35f;
            controller.center = new Vector3(0, 0.55f, 0);
            motor = dog.AddComponent<DogMotor>();
            config = ScriptableObject.CreateInstance<DogMovementConfig>();
#if UNITY_EDITOR
            var serialized = new UnityEditor.SerializedObject(motor);
            serialized.FindProperty("config").objectReferenceValue = config;
            serialized.FindProperty("controller").objectReferenceValue = controller;
            serialized.ApplyModifiedPropertiesWithoutUndo();
#endif
            dog.SetActive(true);
            Physics.SyncTransforms();
            yield return null;
            Step(Vector2.zero, false, false, 20);
            Assert.That(motor.IsGrounded, Is.True);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Object.Destroy(dog);
            Object.Destroy(floor);
            Object.Destroy(config);
            yield return null;
        }

        private void Step(Vector2 direction, bool run, bool jump, int ticks, float yaw = 0)
        {
            for (var i = 0; i < ticks; i++)
                motor.Simulate(direction, run, jump && i == 0, yaw, 0.02f);
        }

        [Test]
        public void DiagonalMovementIsNormalizedAndRunningIsFaster()
        {
            var start = dog.transform.position;
            Step(Vector2.up, false, false, 50);
            var walkDistance = Vector3.Distance(start, dog.transform.position);
            Assert.That(walkDistance, Is.EqualTo(config.WalkSpeed).Within(0.05f));
            motor.Respawn();
            Step(Vector2.one, false, false, 50);
            Assert.That(Vector3.Distance(start, dog.transform.position), Is.EqualTo(walkDistance).Within(0.05f));
            motor.Respawn();
            Step(Vector2.up, true, false, 50);
            Assert.That(Vector3.Distance(start, dog.transform.position), Is.EqualTo(config.RunSpeed).Within(0.05f));
        }

        [Test]
        public void CameraYawRotatesMovementDirection()
        {
            var start = dog.transform.position;
            Step(Vector2.up, false, false, 50, 90);
            Assert.That(dog.transform.position.x - start.x, Is.EqualTo(config.WalkSpeed).Within(0.05f));
            Assert.That(dog.transform.position.z, Is.EqualTo(start.z).Within(0.05f));
        }

        [Test]
        public void JumpHasBoundedApexRejectsAirJumpAndLands()
        {
            var startY = dog.transform.position.y;
            Step(Vector2.zero, false, true, 1);
            Assert.That(motor.VerticalSpeed, Is.GreaterThan(0));
            var peak = startY;
            for (var i = 0; i < 100; i++)
            {
                var velocity = motor.VerticalSpeed;
                Step(Vector2.zero, false, i == 5, 1);
                if (i == 5) Assert.That(motor.VerticalSpeed, Is.LessThan(velocity));
                peak = Mathf.Max(peak, dog.transform.position.y);
            }
            Assert.That(peak - startY, Is.InRange(config.JumpHeight - 0.2f, config.JumpHeight + 0.1f));
            Assert.That(motor.IsGrounded, Is.True);
        }

        [Test]
        public void FallingOutOfArenaRestoresSpawnAndClearsVelocity()
        {
            var controller = dog.GetComponent<CharacterController>();
            controller.enabled = false;
            dog.transform.position = new Vector3(200, config.RespawnBelowY - 1, 200);
            controller.enabled = true;
            Step(Vector2.zero, false, false, 1);
            Assert.That(dog.transform.position, Is.EqualTo(Spawn));
            Assert.That(motor.VerticalSpeed, Is.Zero);
        }
    }
}
