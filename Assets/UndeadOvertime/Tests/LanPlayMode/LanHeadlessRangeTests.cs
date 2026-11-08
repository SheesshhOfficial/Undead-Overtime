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
    }
}
