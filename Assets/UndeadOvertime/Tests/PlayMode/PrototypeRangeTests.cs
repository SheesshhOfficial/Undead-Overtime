using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UndeadOvertime.Prototype.Tests
{
    public sealed class PrototypeRangeTests
    {
        private GameObject root;
        private GameObject cameraObject;
        private PrototypeRange range;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            root = new GameObject("Test range owner");
            cameraObject = new GameObject("Test camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            range = root.AddComponent<PrototypeRange>();
            range.Build(camera);
            range.Player.SetInputEnabled(false);
            Physics.SyncTransforms();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(root);
            Object.Destroy(cameraObject);
            yield return null;
        }

        [Test]
        public void BuildIsIdempotentAndCreatesThreeTargets()
        {
            range.Build(range.WorldCamera);
            Assert.That(range.Targets.Count, Is.EqualTo(3));
            Assert.That(root.GetComponentsInChildren<PrototypePlayer>(true).Length, Is.EqualTo(1));
            Assert.That(root.GetComponentsInChildren<PrototypeTarget>(true).Length, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator PlayerSettlesOnFloorWithoutFallingThrough()
        {
            yield return new WaitForSeconds(0.4f);
            Assert.That(range.Player.transform.position.y, Is.InRange(-0.06f, 0.12f));
            Assert.That(range.Player.GetComponent<CharacterController>().isGrounded, Is.True);
        }

        [UnityTest]
        public IEnumerator RealHitscanKillsTargetInFourShotsAndHonorsCooldown()
        {
            Assert.That(range.Player.TryFire(), Is.True);
            Assert.That(range.Player.TryFire(), Is.False);
            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForSeconds(0.20f);
                Assert.That(range.Player.TryFire(), Is.True);
            }
            Assert.That(range.Player.ShotsFired, Is.EqualTo(4));
            Assert.That(range.Player.Hits, Is.EqualTo(4));
            Assert.That(range.Player.Kills, Is.EqualTo(1));
            Assert.That(range.Targets[1].IsAlive, Is.False);
            Assert.That(range.Targets[1].GetComponent<Collider>().enabled, Is.False);
        }

        [Test]
        public void SolidCoverBlocksDamage()
        {
            GameObject obstruction = new GameObject("Shot blocking test wall");
            obstruction.transform.SetParent(root.transform);
            obstruction.transform.position = new Vector3(0f, 1.5f, -7f);
            obstruction.AddComponent<BoxCollider>().size = new Vector3(4f, 3f, 0.5f);
            Physics.SyncTransforms();
            Assert.That(range.Player.TryFire(), Is.True);
            Assert.That(range.Player.Hits, Is.Zero);
            Assert.That(range.Targets[1].CurrentHealth, Is.EqualTo(100));
        }

        [Test]
        public void ThirdPersonCameraStaysInsideBackWallAndCanReturnToFirstPerson()
        {
            CharacterController controller = range.Player.GetComponent<CharacterController>();
            controller.enabled = false;
            range.Player.transform.position = new Vector3(0f, 0.1f, -15f);
            controller.enabled = true;
            Physics.SyncTransforms();
            range.Player.SetThirdPerson(true);
            Assert.That(range.Player.IsThirdPerson, Is.True);
            Assert.That(range.WorldCamera.transform.position.z, Is.GreaterThan(-15.75f));
            range.Player.SetThirdPerson(false);
            Assert.That(range.WorldCamera.transform.position.z, Is.EqualTo(-15f).Within(0.001f));
            Assert.That(range.WorldCamera.transform.position.y, Is.EqualTo(1.72f).Within(0.001f));
        }

        [Test]
        public void ResetRestoresTargetsCountersSpawnAndFirstPerson()
        {
            range.Targets[0].TakeDamage(100);
            range.Player.TryFire();
            range.Player.SetThirdPerson(true);
            range.ResetRange();
            Assert.That(range.Targets[0].CurrentHealth, Is.EqualTo(100));
            Assert.That(range.Targets[0].GetComponent<Collider>().enabled, Is.True);
            Assert.That(range.Targets[0].GetComponent<Renderer>().enabled, Is.True);
            Assert.That(range.Player.ShotsFired, Is.Zero);
            Assert.That(range.Player.Hits, Is.Zero);
            Assert.That(range.Player.Kills, Is.Zero);
            Assert.That(range.Player.IsThirdPerson, Is.False);
            Assert.That(range.Player.transform.position, Is.EqualTo(new Vector3(0f, 0.1f, -9f)));
        }
    }
}
