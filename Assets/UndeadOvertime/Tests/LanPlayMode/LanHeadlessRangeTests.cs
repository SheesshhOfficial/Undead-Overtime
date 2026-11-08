using System.Collections;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using UndeadOvertime.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace UndeadOvertime.LanPrototype.Tests
{
    public sealed class LanHeadlessRangeTests
    {
        private GameObject root, cameraObject;
        private LanRange range;
        private ushort port;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            root = new GameObject("Headless LAN test owner"); cameraObject = new GameObject("Headless LAN test camera");
            range = root.AddComponent<LanRange>(); range.Build(cameraObject.AddComponent<Camera>(), false);
            using (var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
                port = (ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port;
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (range != null) range.StopSession();
            for (int i = 0; i < 60 && range != null && range.IsBusy; i++) yield return null;
            Object.Destroy(root); Object.Destroy(cameraObject); yield return null; yield return null;
        }
        [Test]
        public void ColliderOnlyBuildNeedsNoRendererAndPreservesRaycastGeometry()
        {
            Assert.That(range.IsBuilt, Is.True); Assert.That(range.PresentationEnabled, Is.False);
            Assert.That(range.WorldCamera.enabled, Is.False);
            Assert.That(root.GetComponentsInChildren<Renderer>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<MeshFilter>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<BoxCollider>(true).Length, Is.EqualTo(5));
            Assert.That(root.GetComponentsInChildren<SphereCollider>(true).Length, Is.EqualTo(3));
            var avatars = root.GetComponentsInChildren<CapsuleCollider>(true);
            Assert.That(avatars.Length, Is.EqualTo(10));
            foreach (var avatar in avatars) Assert.That(avatar.enabled, Is.False);
            Assert.That(range.IsBusy, Is.False); Assert.That(NetworkManager.Singleton, Is.Null);
            range.Build(range.WorldCamera); // Idempotent: cannot re-enable graphics after construction.
            Assert.That(range.PresentationEnabled, Is.False);
            Assert.That(root.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(18));
            Physics.SyncTransforms();
            Vector3 origin = LanRange.Station(0), aim = (new Vector3(0, 1.6f, 10) - origin).normalized;
            Assert.That(Physics.Raycast(origin, aim, out RaycastHit hit, 100), Is.True);
            Assert.That(hit.collider.name, Is.EqualTo("Shared target 2"));
            Assert.That(hit.collider, Is.TypeOf<SphereCollider>());
            Assert.That((hit.collider.bounds.size - Vector3.one * 1.5f).sqrMagnitude, Is.LessThan(.000001f));
        }
        [UnityTest]
        public IEnumerator ColliderOnlyHostUsesDamageReloadAndRoundReset()
        {
            Assert.That(range.StartHosting("127.0.0.1", port), Is.True);
            for (int i = 0; i < 60 && !range.IsConnected; i++) yield return null;
            Assert.That(range.IsHosting && range.IsConnected, Is.True);
            Assert.That(range.BeginRound(), Is.True);
            Vector3 aim = (new Vector3(0, 1.6f, 10) - LanRange.Station(0)).normalized;
            for (int i = 0; i < 4; i++)
            {
                Assert.That(range.SendAction(RangeAction.Fire, aim), Is.True);
                yield return new WaitForSecondsRealtime(.21f);
            }
            Assert.That(range.CurrentSnapshot.TargetHealth(1), Is.Zero);
            Assert.That(range.CurrentSnapshot.Member(0).Ammo, Is.EqualTo(8));
            Assert.That(range.SendAction(RangeAction.Reload, aim), Is.True);
            yield return new WaitForSecondsRealtime(1.7f);
            Assert.That(range.CurrentSnapshot.Member(0).Ammo, Is.EqualTo(12));
            Assert.That(range.ReturnToLobby(), Is.True); Assert.That(range.BeginRound(), Is.True);
            Assert.That(range.CurrentSnapshot.RoundId, Is.EqualTo(2));
            Assert.That(range.CurrentSnapshot.TargetHealth(1), Is.EqualTo(100));
            Assert.That(range.WorldCamera.transform.position, Is.EqualTo(LanRange.Station(0)));
            Assert.That(root.GetComponentsInChildren<Renderer>(true), Is.Empty);
            range.StopSession();
            for (int i = 0; i < 120 && range.IsBusy; i++) yield return null;
            Assert.That(range.IsBusy, Is.False); Assert.That(range.MemberCount, Is.Zero);
            Assert.That(range.Phase, Is.EqualTo(SessionPhase.Stopped));
        }
        [UnityTest]
        public IEnumerator HostWalkingStopsInsidePacingWindowExpiresAndUsesMovedShotOrigin()
        {
            Assert.That(range.StartHosting("127.0.0.1", port), Is.True);
            for (int i = 0; i < 60 && !range.IsConnected; i++) yield return null;
            Assert.That(range.BeginRound(), Is.True);
            Assert.That(range.SendAction(RangeAction.Move, Vector3.forward), Is.True);
            Assert.That(range.SendAction(RangeAction.Move, Vector3.zero), Is.True); // Same frame, inside 20 ms pacing window.
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(range.TryGetLocalMember(out RangeMember initiallyStopped), Is.True);
            Assert.That((initiallyStopped.Position - HostMoveGate.Spawn(0)).LengthSquared, Is.LessThan(.0001));
            double until = Time.realtimeSinceStartupAsDouble + .65;
            while (Time.realtimeSinceStartupAsDouble < until)
            {
                Assert.That(range.SendAction(RangeAction.Move, Vector3.forward), Is.True);
                yield return new WaitForSecondsRealtime(.05f);
            }
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(range.TryGetLocalMember(out RangeMember moved), Is.True);
            Assert.That(moved.Position.Z - LanRange.Station(0).z, Is.InRange(1d, 4d));
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(range.TryGetLocalMember(out RangeMember stopped), Is.True);
            Assert.That((stopped.Position - moved.Position).LengthSquared, Is.LessThan(.0001));
            Vector3 origin = new Vector3((float)stopped.Position.X, (float)stopped.Position.Y, (float)stopped.Position.Z);
            Assert.That(range.WorldCamera.transform.position, Is.EqualTo(origin));
            Assert.That(range.SendAction(RangeAction.Fire, (new Vector3(0, 1.6f, 10) - origin).normalized), Is.True);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(range.CurrentSnapshot.TargetHealth(1), Is.EqualTo(75));
            Assert.That(range.ReturnToLobby(), Is.True); Assert.That(range.BeginRound(), Is.True);
            Assert.That(range.TryGetLocalMember(out RangeMember reset), Is.True);
            Assert.That((reset.Position - HostMoveGate.Spawn(0)).LengthSquared, Is.LessThan(.0001));
        }
        [Test]
        public void WalkingCapsuleCannotCrossWallsAndSlidesAlongAnObstacle()
        {
            GameObject wall = new GameObject("Walking test obstacle"); wall.transform.SetParent(root.transform);
            wall.transform.position = new Vector3(0, 1, -4); wall.AddComponent<BoxCollider>().size = new Vector3(5, 2, .5f);
            Physics.SyncTransforms();
            Vector3 position = new Vector3(0, 1.6f, -8);
            for (int i = 0; i < 50; i++) position = LanRange.ResolvePlanarMovement(position, Vector3.forward * .2f);
            Assert.That(position.z, Is.InRange(-4.8f, -4.55f));
            for (int i = 0; i < 5; i++) position = LanRange.ResolvePlanarMovement(position, new Vector3(.1f, 0, .1f));
            Assert.That(position.x, Is.GreaterThan(.3f)); Assert.That(position.z, Is.LessThan(-4.55f));
            position = new Vector3(0, 1.6f, -8);
            for (int i = 0; i < 200; i++) position = LanRange.ResolvePlanarMovement(position, Vector3.right * .2f);
            Assert.That(position.x, Is.InRange(15f, 15.301f)); Assert.That(position.y, Is.EqualTo(1.6f));
            Assert.That(LanRange.ResolvePlanarMovement(position, Vector3.right * 50), Is.EqualTo(position));
        }
    }
}
