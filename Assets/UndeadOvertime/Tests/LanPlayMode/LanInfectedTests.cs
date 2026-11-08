using System.Collections;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using UndeadOvertime.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace UndeadOvertime.LanPrototype.Tests
{
    public sealed class LanInfectedTests
    {
        private GameObject root, cameraObject;
        private LanRange range;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            root = new GameObject("Infected LAN test owner"); cameraObject = new GameObject("Infected LAN test camera");
            range = root.AddComponent<LanRange>(); range.Build(cameraObject.AddComponent<Camera>(), false);
            ushort port;
            using (var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
                port = (ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port;
            Assert.That(range.StartEncounter(), Is.False);
            Assert.That(range.StartHosting("127.0.0.1", port), Is.True);
            for (int i = 0; i < 60 && !range.IsConnected; i++) yield return null;
            Assert.That(range.BeginRound(), Is.True);
            Assert.That(range.CurrentSnapshot.Encounter.Phase, Is.EqualTo(EncounterPhase.Dormant));
        }
        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (range != null) range.StopSession();
            for (int i = 0; i < 120 && range != null && range.IsBusy; i++) yield return null;
            Object.Destroy(root); Object.Destroy(cameraObject); yield return null; yield return null;
        }
        private Vector3 AimAtEnemy()
        {
            Assert.That(range.TryGetLocalMember(out RangeMember player), Is.True);
            AimVector delta = range.CurrentSnapshot.Encounter.Position - player.Position;
            return new Vector3((float)delta.X, (float)delta.Y, (float)delta.Z).normalized;
        }
        [UnityTest]
        public IEnumerator HostEnemyPursuesInjuresAndCanBeKilledThenRoundResets()
        {
            Assert.That(range.StartEncounter(), Is.True); Assert.That(range.StartEncounter(), Is.False);
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (range.CurrentSnapshot.Member(0).Health == 100 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(range.CurrentSnapshot.Member(0).Health, Is.InRange(1, 90));
            Assert.That((range.CurrentSnapshot.Encounter.Position - HostInfectedEncounter.Spawn).LengthSquared, Is.GreaterThan(1));
            for (int i = 0; i < 4; i++)
            {
                Assert.That(range.SendAction(RangeAction.Fire, AimAtEnemy()), Is.True);
                yield return new WaitForSecondsRealtime(.22f);
            }
            Assert.That(range.CurrentSnapshot.Encounter.Health, Is.Zero);
            Assert.That(range.CurrentSnapshot.Encounter.Phase, Is.EqualTo(EncounterPhase.Cleared));
            Assert.That(range.CurrentSnapshot.Member(0).Ammo, Is.EqualTo(8));
            int hp = range.CurrentSnapshot.Member(0).Health;
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.That(range.CurrentSnapshot.Member(0).Health, Is.EqualTo(hp));
            Assert.That(range.ReturnToLobby(), Is.True); Assert.That(range.BeginRound(), Is.True);
            Assert.That(range.CurrentSnapshot.Encounter.Phase, Is.EqualTo(EncounterPhase.Dormant));
            Assert.That(range.CurrentSnapshot.Member(0).Health, Is.EqualTo(100));
        }
        [UnityTest]
        public IEnumerator DownedHostCannotMoveOrFireAndCanResetTheRound()
        {
            Assert.That(range.StartEncounter(), Is.True);
            double deadline = Time.realtimeSinceStartupAsDouble + 35;
            while (range.CurrentSnapshot.Member(0).Health > 0 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(range.CurrentSnapshot.Member(0).Health, Is.Zero);
            Assert.That(range.CurrentSnapshot.Encounter.Phase, Is.EqualTo(EncounterPhase.Failed));
            AimVector before = range.CurrentSnapshot.Member(0).Position;
            range.SendAction(RangeAction.Move, Vector3.forward); range.SendAction(RangeAction.Fire, AimAtEnemy());
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(range.CurrentSnapshot.Member(0).Ammo, Is.EqualTo(12));
            Assert.That((range.CurrentSnapshot.Member(0).Position - before).LengthSquared, Is.LessThan(.0001));
            Assert.That(range.CurrentSnapshot.Encounter.Health, Is.EqualTo(100));
            Assert.That(range.ReturnToLobby(), Is.True); Assert.That(range.BeginRound(), Is.True);
            Assert.That(range.CurrentSnapshot.Member(0).Health, Is.EqualTo(100));
            Assert.That(range.StartEncounter(), Is.True);
        }
        [UnityTest]
        public IEnumerator WorldWallBlocksInfectedPursuitAndPlayerShots()
        {
            GameObject wall = new GameObject("Encounter occlusion wall"); wall.transform.SetParent(root.transform);
            wall.transform.position = new Vector3(0, 1.5f, 0); wall.AddComponent<BoxCollider>().size = new Vector3(32, 3, .5f);
            Physics.SyncTransforms(); Assert.That(range.StartEncounter(), Is.True);
            yield return new WaitForSecondsRealtime(5.5f);
            Assert.That(range.CurrentSnapshot.Encounter.Position.Z, Is.GreaterThan(.55));
            Assert.That(range.CurrentSnapshot.Member(0).Health, Is.EqualTo(100));
            Assert.That(range.SendAction(RangeAction.Fire, AimAtEnemy()), Is.True);
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(range.CurrentSnapshot.Encounter.Health, Is.EqualTo(100));
            Assert.That(range.CurrentSnapshot.Member(0).Ammo, Is.EqualTo(11));
        }
        [UnityTest]
        public IEnumerator ThinWallPreventsMeleeEvenInsideAttackRange()
        {
            Vector3 goal = new Vector3(0, 1.6f, 5);
            double deadline = Time.realtimeSinceStartupAsDouble + 15;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                Assert.That(range.TryGetLocalMember(out RangeMember local), Is.True);
                Vector3 position = new Vector3((float)local.Position.X, 1.6f, (float)local.Position.Z);
                Vector3 toward = goal - position;
                if (toward.sqrMagnitude < .0144f) break;
                range.SendAction(RangeAction.Move, toward.normalized * .5f);
                yield return new WaitForSecondsRealtime(.05f);
            }
            range.SendAction(RangeAction.Move, Vector3.zero); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(range.TryGetLocalMember(out RangeMember stopped), Is.True);
            Assert.That((stopped.Position - HostInfectedEncounter.Spawn).LengthSquared, Is.LessThan(1.3 * 1.3));
            Assert.That(stopped.Position.Z, Is.LessThan(5.2));
            GameObject wall = new GameObject("Close melee line-of-sight wall"); wall.transform.SetParent(root.transform);
            wall.transform.position = new Vector3(0, 1.5f, 5.6f); wall.AddComponent<BoxCollider>().size = new Vector3(3, 3, .05f);
            Physics.SyncTransforms(); Assert.That(range.StartEncounter(), Is.True);
            yield return new WaitForSecondsRealtime(2.2f);
            Assert.That((range.CurrentSnapshot.Member(0).Position - range.CurrentSnapshot.Encounter.Position).LengthSquared,
                Is.LessThan(1.3 * 1.3));
            Assert.That(range.CurrentSnapshot.Member(0).Health, Is.EqualTo(100));
        }
        [UnityTest]
        public IEnumerator ImmediateReloadAfterShotIsAcceptedAndRetriesDoNotRestartIt()
        {
            Vector3 aim = (new Vector3(0, 1.6f, 10) - LanRange.Station(0)).normalized;
            Assert.That(range.SendAction(RangeAction.Fire, aim), Is.True);
            Assert.That(range.SendAction(RangeAction.Reload, aim), Is.True); // No frame/delay between distinct inputs.
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(range.CurrentSnapshot.Member(0).Reloading, Is.True);
            Assert.That(range.CurrentSnapshot.Member(0).Ammo, Is.EqualTo(11));
            Assert.That(range.CurrentSnapshot.TargetHealth(1), Is.EqualTo(75));
            double until = Time.realtimeSinceStartupAsDouble + 1.7;
            while (Time.realtimeSinceStartupAsDouble < until)
            {
                range.SendAction(RangeAction.Reload, aim);
                yield return new WaitForSecondsRealtime(.05f);
            }
            Assert.That(range.CurrentSnapshot.Member(0).Reloading, Is.False);
            Assert.That(range.CurrentSnapshot.Member(0).Ammo, Is.EqualTo(12));
            Assert.That(range.CurrentSnapshot.TargetHealth(1), Is.EqualTo(75));
        }
        [UnityTest]
        public IEnumerator ReviveWithoutADownedTeammateCannotHealOrBlockWeaponInput()
        {
            Assert.That(range.SendAction(RangeAction.Revive, Vector3.forward), Is.True);
            Vector3 aim = (new Vector3(0, 1.6f, 10) - LanRange.Station(0)).normalized;
            Assert.That(range.SendAction(RangeAction.Fire, aim), Is.True);
            range.SendAction(RangeAction.Revive, Vector3.zero);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(range.CurrentSnapshot.TargetHealth(1), Is.EqualTo(75));
            Assert.That(range.CurrentSnapshot.Member(0).Ammo, Is.EqualTo(11));
            Assert.That(range.CurrentSnapshot.Member(0).Health, Is.EqualTo(100));
            Assert.That(range.CurrentSnapshot.Member(0).ReviveTargetSlot, Is.EqualTo(255));
            Assert.That(range.CurrentSnapshot.Member(0).ReviveProgress, Is.Zero);
        }
    }
}
