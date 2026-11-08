using System.Collections;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using UndeadOvertime.Core;
using Unity.Netcode;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace UndeadOvertime.LanPrototype.Tests
{
    public sealed class LanRangeTests
    {
        private GameObject root, cameraObject;
        private LanRange range;
        private ushort port;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            root = new GameObject("LAN test owner"); cameraObject = new GameObject("LAN test camera");
            range = root.AddComponent<LanRange>(); range.Build(cameraObject.AddComponent<Camera>());
            // Select an ephemeral loopback port. Binding may still race another process; report such a failure.
            using (var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0))) port = (ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port;
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (range != null) range.StopSession();
            for (int i = 0; i < 60 && range != null && range.IsBusy; i++) yield return null;
            Object.Destroy(root); Object.Destroy(cameraObject); yield return null; yield return null;
        }
        private IEnumerator Host()
        {
            Assert.That(range.StartHosting("127.0.0.1", port), Is.True);
            for (int i = 0; i < 60 && !range.IsConnected; i++) yield return null;
            Assert.That(range.IsHosting, Is.True); Assert.That(range.IsConnected, Is.True);
            Assert.That(range.MemberCount, Is.EqualTo(1));
        }
        [Test]
        public void BuildDoesNotStartNetworkingAndIsIdempotent()
        {
            int children = root.GetComponentsInChildren<Transform>(true).Length;
            range.Build(range.WorldCamera); Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(children));
            Assert.That(range.IsBusy, Is.False); Assert.That(NetworkManager.Singleton, Is.Null);
            Assert.That(range.StartHosting("0.0.0.0", port), Is.False);
        }
        [Test]
        public void NamedMessageReaderConsumesOnlyRemainingPayloadAfterNgoPrefix()
        {
            byte[] command = LanRangeProtocol.Encode(new RangeCommand(RangeAction.Fire, 1, 1, 1, new AimVector(0, 0, 1)));
            using (var writer = new FastBufferWriter(command.Length + 8, Allocator.Temp))
            {
                writer.WriteValueSafe(123UL); writer.WriteBytesSafe(command);
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.Seek(8);
                    Assert.That(LanRange.TryReadPayload(reader, command.Length, command.Length, out byte[] bytes), Is.True);
                    Assert.That(bytes, Is.EqualTo(command)); Assert.That(LanRangeProtocol.TryCommand(bytes, out _), Is.True);
                }
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                { Assert.That(LanRange.TryReadPayload(reader, command.Length, command.Length, out _), Is.False); }
            }
        }
        [UnityTest]
        public IEnumerator HostRoundFireReloadAndResetUseAuthority()
        {
            yield return Host(); Assert.That(range.Phase, Is.EqualTo(SessionPhase.Lobby));
            Assert.That(range.SendAction(RangeAction.Fire, Vector3.forward), Is.False);
            Assert.That(range.BeginRound(), Is.True); Assert.That(range.BeginRound(), Is.False);
            Vector3 aim = (new Vector3(0, 1.6f, 10) - LanRange.Station(0)).normalized;
            for (int i = 0; i < 4; i++) { range.SendAction(RangeAction.Fire, aim); yield return new WaitForSecondsRealtime(.21f); }
            Assert.That(range.CurrentSnapshot.TargetHealth(1), Is.Zero);
            Assert.That(range.CurrentSnapshot.Member(0).Ammo, Is.EqualTo(8));
            range.SendAction(RangeAction.Reload, aim); yield return new WaitForSecondsRealtime(1.7f);
            Assert.That(range.CurrentSnapshot.Member(0).Ammo, Is.EqualTo(12));
            Assert.That(range.ReturnToLobby(), Is.True); Assert.That(range.ReturnToLobby(), Is.False);
            Assert.That(range.BeginRound(), Is.True); Assert.That(range.CurrentSnapshot.RoundId, Is.EqualTo(2));
            Assert.That(range.CurrentSnapshot.TargetHealth(1), Is.EqualTo(100));
        }
        [UnityTest]
        public IEnumerator HostRaycastCannotShootThroughAnObstruction()
        {
            yield return Host(); range.BeginRound();
            GameObject wall = new GameObject("Host occlusion test"); wall.transform.SetParent(root.transform);
            wall.transform.position = new Vector3(-4.5f, 1.6f, 1); wall.AddComponent<BoxCollider>().size = new Vector3(4, 4, 1);
            Vector3 aim = (new Vector3(0, 1.6f, 10) - LanRange.Station(0)).normalized;
            range.SendAction(RangeAction.Fire, aim); yield return new WaitForSecondsRealtime(.15f);
            Assert.That(range.CurrentSnapshot.TargetHealth(1), Is.EqualTo(100));
            Assert.That(range.CurrentSnapshot.Member(0).Ammo, Is.EqualTo(11));
        }
        [UnityTest]
        public IEnumerator StopRestartDoesNotLeaveOldMembershipOrRound()
        {
            yield return Host(); range.BeginRound(); long oldSession = range.CurrentSnapshot.SessionId;
            Assert.That(range.StartHosting("127.0.0.1", port), Is.False);
            range.StopSession();
            for (int i = 0; i < 120 && range.IsBusy; i++) yield return null;
            Assert.That(range.IsBusy, Is.False); Assert.That(range.MemberCount, Is.Zero);
            yield return Host(); Assert.That(range.CurrentSnapshot.SessionId, Is.GreaterThan(oldSession));
            Assert.That(range.CurrentSnapshot.RoundId, Is.Zero); Assert.That(range.Phase, Is.EqualTo(SessionPhase.Lobby));
        }
    }
}
