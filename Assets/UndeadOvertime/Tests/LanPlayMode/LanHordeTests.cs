using System.Collections;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using UndeadOvertime.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace UndeadOvertime.LanPrototype.Tests
{
    public sealed class LanHordeTests
    {
        private GameObject root, cameraObject;
        private LanRange range;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            root = new GameObject("Horde LAN test owner"); cameraObject = new GameObject("Horde LAN test camera");
            range = root.AddComponent<LanRange>(); range.Build(cameraObject.AddComponent<Camera>(), false);
            ushort port;
            using (var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0))) port = (ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port;
            Assert.That(range.StartWaves(), Is.False); Assert.That(range.StartHosting("127.0.0.1", port), Is.True);
            for (int i = 0; i < 60 && !range.IsConnected; i++) yield return null;
            Assert.That(range.BeginRound(), Is.True);
        }
        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (range != null) range.StopSession();
            for (int i = 0; i < 120 && range != null && range.IsBusy; i++) yield return null;
            Object.Destroy(root); Object.Destroy(cameraObject); yield return null; yield return null;
        }
        private Vector3 AimAtLiveEnemy()
        {
            range.TryGetLocalMember(out RangeMember local); double nearest = double.MaxValue; AimVector aim = default;
            for (int i = 0; i < range.CurrentSnapshot.EnemyCount; i++)
            {
                EnemySnapshot enemy = range.CurrentSnapshot.Enemy(i); AimVector delta = enemy.Position - local.Position;
                if (enemy.Health > 0 && delta.LengthSquared < nearest) { nearest = delta.LengthSquared; aim = delta; }
            }
            return new Vector3((float)aim.X, (float)aim.Y, (float)aim.Z).normalized;
        }
        [UnityTest]
        public IEnumerator HostClearsThreePhysicalWavesWithReloadsAndRetainedAmmo()
        {
            Assert.That(range.StartWaves(), Is.True); Assert.That(range.StartWaves(), Is.False);
            Assert.That(range.CurrentSnapshot.EnemyCount, Is.EqualTo(2));
            AimVector first = range.CurrentSnapshot.Enemy(0).Position, second = range.CurrentSnapshot.Enemy(1).Position;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That((range.CurrentSnapshot.Enemy(0).Position - first).LengthSquared, Is.GreaterThan(.001));
            Assert.That((range.CurrentSnapshot.Enemy(1).Position - second).LengthSquared, Is.GreaterThan(.001));
            int seen = 0, breaks = 0, lastWave = 1, priorAmmo = -1; bool inBreak = false;
            double deadline = Time.realtimeSinceStartupAsDouble + 45;
            while (range.CurrentSnapshot.Horde.Phase != HordePhase.Completed && Time.realtimeSinceStartupAsDouble < deadline)
            {
                RangeSnapshot state = range.CurrentSnapshot; range.TryGetLocalMember(out RangeMember player);
                Assert.That(state.Encounter.Phase, Is.EqualTo(EncounterPhase.Active)); seen |= 1 << state.Horde.Wave;
                if (state.Horde.Phase == HordePhase.Intermission)
                {
                    if (!inBreak) { breaks++; priorAmmo = player.Ammo; inBreak = true; }
                    yield return new WaitForSecondsRealtime(.05f); continue;
                }
                if (state.Horde.Wave != lastWave)
                {
                    Assert.That(player.Ammo, Is.EqualTo(priorAmmo), "Wave transition must not refill the magazine.");
                    lastWave = state.Horde.Wave; inBreak = false;
                }
                if (player.Reloading) { yield return new WaitForSecondsRealtime(.05f); continue; }
                if (player.Ammo == 0) range.SendAction(RangeAction.Reload, Vector3.up);
                else range.SendAction(RangeAction.Fire, AimAtLiveEnemy());
                yield return new WaitForSecondsRealtime(.22f);
            }
            Assert.That(range.CurrentSnapshot.Horde.Phase, Is.EqualTo(HordePhase.Completed));
            Assert.That(seen, Is.EqualTo(14)); Assert.That(breaks, Is.EqualTo(2));
            Assert.That(range.CurrentSnapshot.Encounter.Phase, Is.EqualTo(EncounterPhase.Cleared));
            for (int i = 0; i < 4; i++) Assert.That(range.CurrentSnapshot.Enemy(i).Health, Is.Zero);
        }
        [UnityTest]
        public IEnumerator WavePlacementAvoidsBlockedAnchorAndTeammateSpawns()
        {
            var wall = new GameObject("Blocked horde anchor"); wall.transform.SetParent(root.transform);
            AimVector anchor = HostInfectedEncounter.WaveSpawn(0);
            wall.transform.position = new Vector3((float)anchor.X, 1.5f, (float)anchor.Z);
            wall.AddComponent<BoxCollider>().size = new Vector3(2, 3, 2); Physics.SyncTransforms();
            Assert.That(range.StartWaves(), Is.True);
            RangeSnapshot state = range.CurrentSnapshot;
            Assert.That((state.Enemy(0).Position - anchor).LengthSquared, Is.GreaterThan(4));
            for (int i = 0; i < state.EnemyCount; i++)
                Assert.That((state.Enemy(i).Position - state.Member(0).Position).LengthSquared, Is.GreaterThanOrEqualTo(6.25));
            Assert.That((state.Enemy(0).Position - state.Enemy(1).Position).LengthSquared, Is.GreaterThanOrEqualTo(1));
            yield return null;
        }
        [UnityTest]
        public IEnumerator NoFreeSpawnFailsClosedWithoutMovingPlayersOrEnablingEnemies()
        {
            var wall = new GameObject("No free wave spawn"); wall.transform.SetParent(root.transform);
            wall.transform.position = new Vector3(0, 1.5f, 0); wall.AddComponent<BoxCollider>().size = new Vector3(32, 3, 32);
            Physics.SyncTransforms(); AimVector before = range.CurrentSnapshot.Member(0).Position;
            Assert.That(range.StartWaves(), Is.False);
            Assert.That(range.CurrentSnapshot.Horde.Phase, Is.EqualTo(HordePhase.Failed));
            Assert.That(range.CurrentSnapshot.Member(0).Health, Is.EqualTo(100));
            Assert.That((range.CurrentSnapshot.Member(0).Position - before).LengthSquared, Is.Zero);
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name.StartsWith("Host-owned infected")) Assert.That(child.gameObject.activeSelf, Is.False);
            yield return null;
        }
        [UnityTest]
        public IEnumerator LobbyRestartClearsWaveIdentityAndRestoresFreshRound()
        {
            Assert.That(range.StartWaves(), Is.True); byte id = range.CurrentSnapshot.Enemy(0).Id;
            range.SendAction(RangeAction.Fire, AimAtLiveEnemy()); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(range.ReturnToLobby(), Is.True); Assert.That(range.BeginRound(), Is.True);
            Assert.That(range.CurrentSnapshot.Horde.Phase, Is.EqualTo(HordePhase.Disabled)); Assert.That(range.CurrentSnapshot.EnemyCount, Is.Zero);
            Assert.That(range.CurrentSnapshot.Member(0).Ammo, Is.EqualTo(12)); Assert.That(range.CurrentSnapshot.Member(0).Health, Is.EqualTo(100));
            Assert.That(range.StartWaves(), Is.True); Assert.That(range.CurrentSnapshot.RoundId, Is.EqualTo(2));
            Assert.That(range.CurrentSnapshot.Enemy(0).Id, Is.EqualTo(id), "IDs are scoped by the new round identity.");
            Assert.That(range.CurrentSnapshot.Enemy(0).Health, Is.EqualTo(100));
        }
    }
}
