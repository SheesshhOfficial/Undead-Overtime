using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using UndeadOvertime.Core;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
namespace UndeadOvertime.LanPrototype.Tests
{
    public sealed class LanMissionTests
    {
        private GameObject root, cameraObject;
        private LanRange range;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            root = new GameObject("Mission integration owner"); cameraObject = new GameObject("Mission integration camera");
            range = root.AddComponent<LanRange>(); range.Build(cameraObject.AddComponent<Camera>(), false); yield return null;
        }
        [UnityTearDown]
        public IEnumerator Teardown()
        {
            range.StopSession(); for (int i = 0; i < 120 && range.IsBusy; i++) yield return null;
            Object.Destroy(root); Object.Destroy(cameraObject); yield return null; yield return null;
        }
        private IEnumerator Host(bool mission = true)
        {
            Assert.That(range.SetMissionMode(mission), Is.True); ushort port;
            using (var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0))) port = (ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port;
            Assert.That(range.StartHosting("127.0.0.1", port), Is.True);
            for (int i = 0; i < 120 && !range.IsConnected; i++) yield return null;
            Assert.That(range.IsConnected, Is.True);
        }
        private IEnumerator WalkTo(Vector3 destination)
        {
            double end = Time.realtimeSinceStartupAsDouble + 15;
            while (Time.realtimeSinceStartupAsDouble < end)
            {
                Assert.That(range.TryGetLocalMember(out RangeMember player), Is.True);
                Vector3 at = new Vector3((float)player.Position.X, (float)player.Position.Y, (float)player.Position.Z);
                Vector3 delta = destination - at; delta.y = 0;
                if (delta.magnitude < .22f) { range.SendAction(RangeAction.Move, Vector3.zero); yield return new WaitForSecondsRealtime(.12f); yield break; }
                Assert.That(player.Health, Is.GreaterThan(0)); range.SendAction(RangeAction.Move, delta.normalized);
                yield return new WaitForSecondsRealtime(.05f);
            }
            Assert.Fail("Host movement did not reach its bounded mission waypoint.");
        }
        private IEnumerator HoldPower(float seconds, bool expectPlacementFailure = false)
        {
            double end = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < end && range.CurrentMission.Mission.Phase == CoopMissionPhase.RestorePower)
            {
                range.SendAction(RangeAction.Move, Vector3.zero); range.SendAction(RangeAction.Revive, Vector3.zero);
                bool submitted = range.SendPowerIntent(true);
                if (!submitted && expectPlacementFailure)
                {
                    // The host rejects terminal actions immediately, before its next 10 Hz public frame.
                    // Only this deliberate placement-failure case may observe that short transition gap.
                    long sessionId = range.CurrentMission.Range.SessionId, roundId = range.CurrentMission.Range.RoundId;
                    ulong staleSequence = range.CurrentMission.Sequence;
                    double publishDeadline = Time.realtimeSinceStartupAsDouble + .3;
                    while (range.CurrentMission != null && range.CurrentMission.Mission.Phase == CoopMissionPhase.RestorePower &&
                        Time.realtimeSinceStartupAsDouble < publishDeadline)
                    {
                        Assert.That(range.IsConnected, Is.True);
                        Assert.That(range.Phase, Is.EqualTo(SessionPhase.Playing));
                        Assert.That(range.CurrentMission.Range.SessionId, Is.EqualTo(sessionId));
                        Assert.That(range.CurrentMission.Range.RoundId, Is.EqualTo(roundId));
                        yield return null;
                    }
                    Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThanOrEqualTo(publishDeadline),
                        "A delayed coroutine resume must not turn an expired publication deadline into success.");
                    Assert.That(range.IsConnected, Is.True, "A disconnect must not count as the expected placement failure.");
                    Assert.That(range.Phase, Is.EqualTo(SessionPhase.Playing));
                    Assert.That(range.CurrentMission, Is.Not.Null);
                    Assert.That(range.CurrentMission.Range.SessionId, Is.EqualTo(sessionId));
                    Assert.That(range.CurrentMission.Range.RoundId, Is.EqualTo(roundId));
                    Assert.That(range.CurrentMission.Sequence, Is.GreaterThan(staleSequence));
                    Assert.That(range.CurrentMission.Mission.Phase, Is.EqualTo(CoopMissionPhase.Failed),
                        "Rejected power must be followed by the same round's published failure within 300 ms.");
                    yield break;
                }
                Assert.That(submitted, Is.True); yield return new WaitForSecondsRealtime(.05f);
            }
        }
        [Test]
        public void MissionIsOptInAndDoesNotCreateGeometryOrNetworkingOnBuild()
        {
            Assert.That(range.MissionMode, Is.False); Assert.That(range.CurrentMission, Is.Null);
            Assert.That(NetworkManager.Singleton, Is.Null); Assert.That(range.SendPowerIntent(true), Is.False);
            Assert.That(range.SetMissionMode(true), Is.True); Assert.That(range.IsBusy, Is.False);
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name.StartsWith("Shared target ")) Assert.That(child.gameObject.activeSelf, Is.False);
            Assert.That(range.SetMissionMode(false), Is.True);
        }
        [Test]
        public void MaximumMissionPayloadAcceptsConsumedPrefixButLegacyBoundStillRejectsIt()
        {
            byte[] packet = new byte[CoopMissionProtocol.MaximumFrameBytes];
            using (var writer = new FastBufferWriter(packet.Length + 8, Allocator.Temp))
            {
                writer.WriteValueSafe(123UL); writer.WriteBytesSafe(packet);
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.Seek(8); Assert.That(LanRange.TryReadPayload(reader, 1, CoopMissionProtocol.MaximumFrameBytes, out byte[] bytes), Is.True);
                    Assert.That(bytes.Length, Is.EqualTo(465));
                }
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                { reader.Seek(8); Assert.That(LanRange.TryReadPayload(reader, 1, LanRangeProtocol.MaximumBytes, out _), Is.False); }
            }
        }
        [UnityTest]
        public IEnumerator ReusedManagerSwitchesProtocolOnlyWhenStopped()
        {
            yield return Host(false); Assert.That(NetworkManager.Singleton.NetworkConfig.ProtocolVersion, Is.EqualTo(5));
            Assert.That(range.SetMissionMode(true), Is.False); range.StopSession();
            for (int i = 0; i < 120 && range.IsBusy; i++) yield return null;
            yield return Host(); Assert.That(NetworkManager.Singleton.NetworkConfig.ProtocolVersion, Is.EqualTo(101));
            Assert.That(CoopMissionProtocol.AcceptHandshake(NetworkManager.Singleton.NetworkConfig.ConnectionData), Is.True);
            range.StopSession(); for (int i = 0; i < 120 && range.IsBusy; i++) yield return null;
            yield return Host(false); Assert.That(NetworkManager.Singleton.NetworkConfig.ProtocolVersion, Is.EqualTo(5));
            Assert.That(LanRangeProtocol.AcceptHandshake(NetworkManager.Singleton.NetworkConfig.ConnectionData), Is.True);
        }
        [UnityTest]
        public IEnumerator MissionRoundHasGateAndRejectsManualEncounterModes()
        {
            yield return Host(); Assert.That(range.BeginRound(), Is.True);
            Assert.That(range.CurrentMission.Mission.Phase, Is.EqualTo(CoopMissionPhase.RestorePower)); Assert.That(range.MissionGateOpen, Is.False);
            Assert.That(range.StartEncounter(), Is.False); Assert.That(range.StartWaves(), Is.False); Assert.That(range.SetMissionMode(false), Is.False);
            Vector3 blocked = LanRange.ResolvePlanarMovement(new Vector3(0, 1.6f, -2.6f), Vector3.forward * .2f);
            Assert.That(blocked.z, Is.LessThan(-2.5f));
            Assert.That(range.ReturnToLobby(), Is.True); Assert.That(range.CurrentMission, Is.Null);
            Assert.That(LanRange.ResolvePlanarMovement(new Vector3(0, 1.6f, -2.6f), Vector3.forward * .2f).z, Is.GreaterThan(-2.5f));
            Assert.That(range.BeginRound(), Is.True); Assert.That(range.CurrentMission.Range.RoundId, Is.EqualTo(2)); Assert.That(range.MissionGateOpen, Is.False);
        }
        [UnityTest]
        public IEnumerator FreshStationaryPowerSurvivesNeutralPacketsButReleaseResetsProgress()
        {
            yield return Host(); range.BeginRound(); yield return HoldPower(.3f);
            Assert.That(range.CurrentMission.Mission.HoldSeconds, Is.Zero, "Spawn is outside the console interaction radius.");
            yield return WalkTo(new Vector3(-9, 1.6f, -6.7f));
            Assert.That((range.CurrentSnapshot.Member(0).Position - DepotMissionPresentation.PowerControl).LengthSquared, Is.LessThan(4)); yield return HoldPower(1.1f);
            Assert.That(range.CurrentMission.Mission.HoldSeconds, Is.GreaterThan(.5)); range.SendPowerIntent(false);
            yield return new WaitForSecondsRealtime(.15f); Assert.That(range.CurrentMission.Mission.HoldSeconds, Is.Zero);
            yield return HoldPower(1.1f); Assert.That(range.CurrentMission.Mission.HoldSeconds, Is.GreaterThan(.5));
            range.SendAction(RangeAction.Fire, Vector3.up); yield return new WaitForSecondsRealtime(.15f);
            Assert.That(range.CurrentMission.Mission.HoldSeconds, Is.Zero, "A shot interrupts the interaction.");
            yield return HoldPower(3.6f); Assert.That(range.CurrentMission.Mission.Phase, Is.EqualTo(CoopMissionPhase.DefendDepot));
            Assert.That(range.MissionGateOpen, Is.True); Assert.That(range.CurrentSnapshot.EnemyCount, Is.EqualTo(2));
        }
        [UnityTest]
        public IEnumerator FullyBlockedWavePlacementFailsMissionClosed()
        {
            yield return Host(); range.BeginRound(); yield return WalkTo(new Vector3(-9, 1.6f, -6.7f));
            Assert.That((range.CurrentSnapshot.Member(0).Position - DepotMissionPresentation.PowerControl).LengthSquared, Is.LessThan(4));
            for (int i = 0; i < 20; i++)
            {
                Vector3 position;
                if (i < 4) { AimVector anchor = HostInfectedEncounter.WaveSpawn(i); position = new Vector3((float)anchor.X, 1.3f, (float)anchor.Z); }
                else position = new Vector3(-12 + ((i - 4) % 4) * 8, 1.3f, -12 + ((i - 4) / 4) * 8);
                var block = new GameObject("Blocked mission spawn " + i); block.transform.SetParent(root.transform);
                block.transform.position = position; block.AddComponent<BoxCollider>().size = new Vector3(1, 2, 1);
            }
            Physics.SyncTransforms(); yield return HoldPower(3.6f, expectPlacementFailure: true); yield return new WaitForSecondsRealtime(.15f);
            Assert.That(range.CurrentMission.Mission.Phase, Is.EqualTo(CoopMissionPhase.Failed));
            Assert.That(range.CurrentSnapshot.Horde.Phase, Is.EqualTo(HordePhase.Failed));
            Assert.That(range.SendPowerIntent(true), Is.False); Assert.That(range.SendPowerIntent(false), Is.False);
            Assert.That(range.SendAction(RangeAction.Fire, Vector3.up), Is.False);
            Assert.That(range.SendAction(RangeAction.Reload, Vector3.up), Is.False);
            Assert.That(range.SendAction(RangeAction.Move, Vector3.forward), Is.False);
            Assert.That(range.ReturnToLobby(), Is.True); Assert.That(range.CurrentMission, Is.Null);
        }
        [TestCase(true)] [TestCase(false)]
        public void GateWaypointRoutesPursuitThroughActualOpenPartitionBothWays(bool northToSouth)
        {
            var layout = new DepotMissionPresentation(root.transform, 5, 1, false);
            try
            {
                layout.TryApply(new CoopMissionSnapshot(5, 1, CoopMissionPhase.DefendDepot, true, true, 0));
                Vector3 start = northToSouth ? new Vector3(-12, 1.6f, 8) : new Vector3(-9, 1.6f, -8);
                Vector3 target = northToSouth ? new Vector3(-9, 1.6f, -8) : new Vector3(-12, 1.6f, 8);
                for (int i = 0; i < 1200 && (start - target).magnitude > .3f; i++)
                {
                    Vector3 direct = Vector3.ClampMagnitude(target - start, .032f);
                    start = LanRange.ResolvePlanarMovement(start, LanRange.MissionChaseStep(start, target, direct));
                }
                Assert.That((start - target).magnitude, Is.LessThan(.35f));
            }
            finally { layout.Dispose(); }
        }
        [UnityTest]
        public IEnumerator FullSoloMissionRunsPowerWavesExtractionAndStopsTerminalActions()
        {
            yield return Host(); range.BeginRound(); yield return WalkTo(new Vector3(-9, 1.6f, -6.7f));
            Assert.That((range.CurrentSnapshot.Member(0).Position - DepotMissionPresentation.PowerControl).LengthSquared, Is.LessThan(4)); yield return HoldPower(3.6f);
            yield return WalkTo(new Vector3(0, 1.6f, -4));
            double end = Time.realtimeSinceStartupAsDouble + 50;
            while (range.CurrentMission.Mission.Phase == CoopMissionPhase.DefendDepot && Time.realtimeSinceStartupAsDouble < end)
            {
                range.TryGetLocalMember(out RangeMember player); Assert.That(player.Health, Is.GreaterThan(0));
                if (!player.Reloading && range.CurrentSnapshot.Horde.Phase == HordePhase.Fighting)
                {
                    AimVector delta = default; double nearest = double.MaxValue;
                    for (int i = 0; i < range.CurrentSnapshot.EnemyCount; i++)
                    {
                        EnemySnapshot enemy = range.CurrentSnapshot.Enemy(i); AimVector d = enemy.Position - player.Position;
                        if (enemy.Health > 0 && d.LengthSquared < nearest) { nearest = d.LengthSquared; delta = d; }
                    }
                    if (player.Ammo == 0) range.SendAction(RangeAction.Reload, Vector3.up);
                    else if (nearest < double.MaxValue) range.SendAction(RangeAction.Fire, new Vector3((float)delta.X, (float)delta.Y, (float)delta.Z).normalized);
                }
                yield return new WaitForSecondsRealtime(.22f);
            }
            Assert.That(range.CurrentMission.Mission.Phase, Is.EqualTo(CoopMissionPhase.ReachExtraction));
            yield return WalkTo(new Vector3(0, 1.6f, -4)); yield return WalkTo(new Vector3(0, 1.6f, 12));
            yield return new WaitForSecondsRealtime(2.4f); Assert.That(range.CurrentMission.Mission.Phase, Is.EqualTo(CoopMissionPhase.Completed));
            Assert.That(range.SendAction(RangeAction.Fire, Vector3.up), Is.False); Assert.That(range.SendAction(RangeAction.Move, Vector3.right), Is.False);
            Assert.That(range.SendPowerIntent(true), Is.False); Assert.That(range.ReturnToLobby(), Is.True); Assert.That(range.CurrentMission, Is.Null);
        }
    }
}
