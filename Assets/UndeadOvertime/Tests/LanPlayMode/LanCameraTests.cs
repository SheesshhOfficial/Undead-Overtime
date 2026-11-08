using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using UndeadOvertime.Core;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace UndeadOvertime.LanPrototype.Tests
{
    public sealed class LanCameraTests
    {
        private GameObject root, cameraObject;
        private LanRange range;
        [UnitySetUp]
        public IEnumerator Setup() { root = new GameObject("LAN camera test root"); yield return null; }
        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (range != null) range.StopSession();
            for (int i = 0; i < 120 && range != null && range.IsBusy; i++) yield return null;
            Object.Destroy(root); if (cameraObject != null) Object.Destroy(cameraObject);
            yield return null; yield return null;
        }
        private BoxCollider Box(string name, Vector3 position, Vector3 size)
        {
            var obj = new GameObject(name); obj.transform.SetParent(root.transform); obj.transform.position = position;
            var collider = obj.AddComponent<BoxCollider>(); collider.size = size; Physics.SyncTransforms(); return collider;
        }
        private SphereCollider Target(Vector3 position)
        {
            var obj = new GameObject("Camera reticle target"); obj.transform.SetParent(root.transform); obj.transform.position = position;
            var collider = obj.AddComponent<SphereCollider>(); collider.radius = .5f; Physics.SyncTransforms(); return collider;
        }
        [Test]
        public void FirstPersonStaysAtEyeAndThirdPersonUsesBoundedShoulderOffset()
        {
            Vector3 eye = new Vector3(0, 1.6f, 0);
            Assert.That(LanCameraRig.ResolvePosition(eye, Quaternion.identity, PlayerViewMode.FirstPerson), Is.EqualTo(eye));
            Assert.That(LanCameraRig.ResolvePosition(eye, Quaternion.identity, PlayerViewMode.ThirdPerson), Is.EqualTo(eye + LanCameraRig.ShoulderOffset));
            Assert.Throws<ArgumentException>(() => LanCameraRig.ResolvePosition(eye, Quaternion.identity, (PlayerViewMode)255));
            Assert.Throws<ArgumentException>(() => LanCameraRig.ResolvePosition(new Vector3(float.NaN, 0, 0), Quaternion.identity, PlayerViewMode.ThirdPerson));
            Assert.Throws<ArgumentException>(() => LanCameraRig.ResolvePosition(eye, default, PlayerViewMode.ThirdPerson));
            Assert.That(LanCameraRig.ShowLocalBody(PlayerViewMode.ThirdPerson, eye, eye + LanCameraRig.ShoulderOffset), Is.True);
            Assert.That(LanCameraRig.ShowLocalBody(PlayerViewMode.ThirdPerson, eye, eye + Vector3.back * .7f), Is.False);
            Assert.That(LanCameraRig.ShowLocalBody(PlayerViewMode.FirstPerson, eye, eye + LanCameraRig.ShoulderOffset), Is.False);
        }
        [Test]
        public void ThirdPersonSphereStopsBeforeWallInsteadOfClippingThroughIt()
        {
            Box("Camera wall", new Vector3(0, 2, -1.5f), new Vector3(10, 6, .2f));
            Vector3 position = LanCameraRig.ResolvePosition(new Vector3(0, 1.6f, 0), Quaternion.identity, PlayerViewMode.ThirdPerson);
            Assert.That(position.z, Is.GreaterThan(-1.25f)); Assert.That(position.z, Is.LessThan(0));
            Assert.That(Physics.CheckSphere(position, LanCameraRig.CameraRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.False);
        }
        [Test]
        public void ShoulderShotDirectionConvergesOnTheVisibleReticleTarget()
        {
            Vector3 eye = new Vector3(0, 1.6f, 0), camera = eye + LanCameraRig.ShoulderOffset;
            SphereCollider target = Target(camera + Vector3.forward * 8);
            Vector3 aim = LanCameraRig.ResolveShotDirection(eye, camera, Quaternion.identity);
            Assert.That(aim.magnitude, Is.EqualTo(1).Within(.0001)); Assert.That(Vector3.Angle(aim, Vector3.forward), Is.GreaterThan(.1));
            Assert.That(Physics.Raycast(eye, aim, out RaycastHit hit, 100), Is.True); Assert.That(hit.collider, Is.EqualTo(target));
        }
        [Test]
        public void CameraVisibilityDoesNotBypassCoverAtTheAuthoritativeMuzzle()
        {
            Vector3 eye = new Vector3(0, 1.6f, 0), camera = eye + LanCameraRig.ShoulderOffset;
            SphereCollider target = Target(camera + Vector3.forward * 8);
            BoxCollider cover = Box("Muzzle cover", new Vector3(.1f, 1.6f, 1), new Vector3(.4f, .4f, .2f));
            Assert.That(Physics.Raycast(camera, Vector3.forward, out RaycastHit cameraHit, 100), Is.True);
            Assert.That(cameraHit.collider, Is.EqualTo(target));
            Vector3 aim = LanCameraRig.ResolveShotDirection(eye, camera, Quaternion.identity);
            Assert.That(Physics.Raycast(eye, aim, out RaycastHit muzzleHit, 100), Is.True);
            Assert.That(muzzleHit.collider, Is.EqualTo(cover));
        }
        [Test]
        public void ObstructionBehindMuzzleNeverProducesABackwardShot()
        {
            Vector3 eye = new Vector3(0, 1.6f, 0), camera = eye + Vector3.back * 3;
            Box("Behind muzzle obstacle", new Vector3(0, 1.6f, -1), Vector3.one);
            Vector3 aim = LanCameraRig.ResolveShotDirection(eye, camera, Quaternion.identity);
            Assert.That(aim.z, Is.EqualTo(1).Within(.0001));
            Assert.Throws<ArgumentException>(() => LanCameraRig.ResolveShotDirection(eye, new Vector3(0, float.NaN, 0), Quaternion.identity));
        }
        [UnityTest]
        public IEnumerator ViewChangeNeverReplaysAnOldEnemySnapshotIntoHostPhysics()
        {
            cameraObject = new GameObject("Camera authority regression camera"); range = root.AddComponent<LanRange>();
            range.Build(cameraObject.AddComponent<Camera>(), false);
            ushort port;
            using (var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0))) port = (ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port;
            Assert.That(range.StartHosting("127.0.0.1", port), Is.True);
            for (int i = 0; i < 60 && !range.IsConnected; i++) yield return null;
            Assert.That(range.BeginRound(), Is.True); Assert.That(range.StartEncounter(), Is.True);
            Transform enemy = null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) if (child.name == "Host-owned infected 0") enemy = child;
            Assert.That(enemy, Is.Not.Null);
            bool observedNewerPhysics = false;
            for (int i = 0; i < 20; i++)
            {
                yield return new WaitForFixedUpdate();
                AimVector old = range.CurrentSnapshot.Enemy(0).Position;
                Vector3 publishedBody = new Vector3((float)old.X, (float)old.Y - .7f, (float)old.Z);
                if ((enemy.position - publishedBody).sqrMagnitude > .0000001f) { observedNewerPhysics = true; break; }
            }
            Assert.That(observedNewerPhysics, Is.True, "Regression must exercise physics newer than the last published snapshot.");
            Vector3 before = enemy.position; bool wasActive = enemy.gameObject.activeSelf;
            range.SetViewMode(PlayerViewMode.ThirdPerson); range.SetViewMode(PlayerViewMode.FirstPerson);
            Assert.That(enemy.position, Is.EqualTo(before)); Assert.That(enemy.gameObject.activeSelf, Is.EqualTo(wasActive));
        }
        [UnityTest]
        public IEnumerator SwitchingViewNeverChangesRoundStateOrCapturesControls()
        {
            cameraObject = new GameObject("Camera switch test camera"); range = root.AddComponent<LanRange>();
            Assert.That(range.SetViewMode(PlayerViewMode.ThirdPerson), Is.False);
            range.Build(cameraObject.AddComponent<Camera>());
            ushort port;
            using (var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0))) port = (ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port;
            Assert.That(range.StartHosting("127.0.0.1", port), Is.True);
            for (int i = 0; i < 60 && !range.IsConnected; i++) yield return null;
            Assert.That(range.BeginRound(), Is.True); long round = range.CurrentSnapshot.RoundId;
            Assert.That(range.TryGetLocalMember(out RangeMember before), Is.True);
            Assert.That(range.SetViewMode(PlayerViewMode.ThirdPerson), Is.True); yield return null;
            Assert.That(range.ViewMode, Is.EqualTo(PlayerViewMode.ThirdPerson)); Assert.That(range.ControlsCaptured, Is.False);
            if (range.PresentationEnabled) Assert.That((range.WorldCamera.transform.position - LanRange.Station(0)).magnitude, Is.GreaterThan(2.9));
            Assert.That(range.SetViewMode(PlayerViewMode.FirstPerson), Is.True); yield return null;
            Assert.That(range.WorldCamera.transform.position, Is.EqualTo(LanRange.Station(0)));
            Assert.That(range.CurrentSnapshot.RoundId, Is.EqualTo(round));
            Assert.That(range.TryGetLocalMember(out RangeMember after), Is.True);
            Assert.That(after.Health, Is.EqualTo(before.Health)); Assert.That(after.Ammo, Is.EqualTo(before.Ammo));
            Assert.That((after.Position - before.Position).LengthSquared, Is.Zero); Assert.That(range.ControlsCaptured, Is.False);
            Assert.That(range.SetViewMode((PlayerViewMode)255), Is.False);
            Assert.That(range.SetMouseSensitivity(.03f), Is.True); Assert.That(range.SetMouseSensitivity(.35f), Is.True);
            Assert.That(range.SetMouseSensitivity(float.NaN), Is.False); Assert.That(range.SetMouseSensitivity(.36f), Is.False);
        }
    }
}
