using System;
using NUnit.Framework;

namespace UndeadOvertime.Core.Tests
{
    public sealed class HostHordeTests
    {
        private LanSession session;
        private HostInfectedEncounter encounter;
        private PlayerTicket host, client;
        [SetUp]
        public void Setup()
        {
            session = new LanSession(5); host = session.StartHost(0); session.TryJoin(7, 5, out client); session.TryBeginRound(host);
            encounter = new HostInfectedEncounter(session, 0);
            encounter.SetPlayerPositionFromHost(host, HostMoveGate.Spawn(0));
            encounter.SetPlayerPositionFromHost(client, HostMoveGate.Spawn(1));
        }
        private void Clear(double now, PlayerTicket shooter)
        {
            for (int i = 0; i < encounter.EnemyCount; i++)
                if (encounter.Enemy(i).Health > 0) Assert.That(encounter.DamageEnemyFromHost(shooter, encounter.Enemy(i).Id, 100, now), Is.True);
        }
        private void ReachThirdWave()
        {
            encounter.StartWavesFromHost(0); Clear(0, host); encounter.AdvanceWavesFromHost(5);
            Clear(5, host); encounter.AdvanceWavesFromHost(10);
        }
        [Test]
        public void SingleAndWaveModesAreMutuallyExclusive()
        {
            Assert.That(encounter.StartWavesFromHost(0), Is.True);
            Assert.That(encounter.StartWavesFromHost(0), Is.False); Assert.That(encounter.StartFromHost(0), Is.False);
            Assert.That(encounter.EnemyCount, Is.EqualTo(2)); Assert.That(encounter.HordeAt(0).Phase, Is.EqualTo(HordePhase.Fighting));
        }
        [Test]
        public void ThreeBoundedWavesRequireAllDeathsAndFullIntermissions()
        {
            encounter.StartWavesFromHost(0); byte first = encounter.Enemy(0).Id;
            encounter.DamageEnemyFromHost(host, first, 100, 0);
            Assert.That(encounter.HordeAt(0).Phase, Is.EqualTo(HordePhase.Fighting));
            Assert.That(encounter.DamageEnemyFromHost(host, first, 100, 0), Is.False);
            Clear(0, host); Assert.That(encounter.HordeAt(0).IntermissionSeconds, Is.EqualTo(5));
            Assert.That(encounter.AdvanceWavesFromHost(4.999), Is.False);
            Assert.That(encounter.AdvanceWavesFromHost(5), Is.True); Assert.That(encounter.EnemyCount, Is.EqualTo(3));
            Clear(5, host); Assert.That(encounter.AdvanceWavesFromHost(10), Is.True); Assert.That(encounter.EnemyCount, Is.EqualTo(4));
            Clear(10, host); Assert.That(encounter.Phase, Is.EqualTo(EncounterPhase.Cleared));
            Assert.That(encounter.HordeAt(10).Phase, Is.EqualTo(HordePhase.Completed));
            Assert.That(encounter.HordeAt(10).Wave, Is.EqualTo(3)); Assert.That(encounter.AdvanceWavesFromHost(100), Is.False);
        }
        [Test]
        public void OldWaveEnemyIdentityCannotAffectItsReplacement()
        {
            encounter.StartWavesFromHost(0); byte stale = encounter.Enemy(0).Id; Clear(0, host); encounter.AdvanceWavesFromHost(5);
            Assert.That(encounter.Enemy(0).Id, Is.Not.EqualTo(stale));
            Assert.That(encounter.DamageEnemyFromHost(host, stale, 100, 5), Is.False);
            Assert.That(encounter.SetEnemyPositionFromHost(stale, HostMoveGate.Spawn(0)), Is.False);
            Assert.That(encounter.MeleeFromHost(stale, host, 5, true), Is.False);
            Assert.That(encounter.ProposeStepFromHost(stale, 5, out _, out _), Is.False);
            Assert.That(encounter.Enemy(0).Health, Is.EqualTo(100));
        }
        [Test]
        public void FourEnemiesEachReceiveTheSameBoundedHostTick()
        {
            ReachThirdWave();
            for (int i = 0; i < 4; i++)
            {
                byte id = encounter.Enemy(i).Id;
                Assert.That(encounter.ProposeStepFromHost(id, 10.05, out AimVector move, out _), Is.True);
                Assert.That(Math.Sqrt(move.LengthSquared), Is.EqualTo(.08).Within(.000001));
                Assert.That(encounter.ProposeStepFromHost(id, 10.05, out AimVector again, out _), Is.True);
                Assert.That(again.LengthSquared, Is.Zero);
            }
        }
        [Test]
        public void PackMembersHaveIndependentMeleeCooldowns()
        {
            encounter.StartWavesFromHost(0);
            for (int i = 0; i < 2; i++)
            {
                byte id = encounter.Enemy(i).Id; encounter.SetEnemyPositionFromHost(id, HostMoveGate.Spawn(0));
                Assert.That(encounter.MeleeFromHost(id, host, 0, true), Is.True);
                Assert.That(encounter.MeleeFromHost(id, host, 0, true), Is.False);
            }
            Assert.That(encounter.PlayerHealth(host), Is.EqualTo(80));
            for (int i = 0; i < 2; i++) Assert.That(encounter.MeleeFromHost(encounter.Enemy(i).Id, host, 1, true), Is.True);
            Assert.That(encounter.PlayerHealth(host), Is.EqualTo(60));
        }
        [Test]
        public void NextWaveDoesNotRefillHealthOrWeaponAmmo()
        {
            var weapon = new HostFireGate(session, 0); var position = HostMoveGate.Spawn(0); var forward = new AimVector(0, 0, 1);
            weapon.SetPoseFromHost(host, position, forward);
            Assert.That(weapon.TryAuthorize(host, new FireRequest(1, 1, 1, position, forward), 0, out _), Is.EqualTo(FireResult.Accepted));
            encounter.StartWavesFromHost(0); byte id = encounter.Enemy(0).Id; encounter.SetEnemyPositionFromHost(id, position);
            encounter.MeleeFromHost(id, host, 0, true); Clear(0, host); encounter.AdvanceWavesFromHost(5);
            Assert.That(encounter.PlayerHealth(host), Is.EqualTo(90)); Assert.That(weapon.Ammo(host), Is.EqualTo(11));
            Assert.That(encounter.HordeAt(5).Wave, Is.EqualTo(2));
        }
        [Test]
        public void TeammateCanBeRevivedDuringIntermissionAndKeepsFortyHealth()
        {
            encounter.SetPlayerPositionFromHost(host, new AimVector(.5, 1.6, 5));
            encounter.SetPlayerPositionFromHost(client, new AimVector(0, 1.6, 5)); encounter.StartWavesFromHost(0);
            byte id = encounter.Enemy(0).Id; encounter.SetEnemyPositionFromHost(id, new AimVector(0, 1.6, 6));
            for (int i = 0; i < 10; i++) encounter.MeleeFromHost(id, client, i, true);
            Clear(9, host); Assert.That(encounter.PlayerHealth(client), Is.Zero);
            for (int i = 0; i <= 31; i++)
            {
                double now = 9.1 + i / 10d; encounter.HoldReviveFromHost(host, true, now);
                encounter.AdvanceReviveFromHost(host, client, now, true);
            }
            Assert.That(encounter.PlayerHealth(client), Is.EqualTo(40));
            encounter.AdvanceWavesFromHost(14); Assert.That(encounter.PlayerHealth(client), Is.EqualTo(40));
            Assert.That(encounter.HordeAt(14).Wave, Is.EqualTo(2));
        }
        [Test]
        public void TeamWipeStopsEveryEnemyAndPreventsMoreWaves()
        {
            encounter.SetPlayerPositionFromHost(host, new AimVector(.5, 1.6, 5));
            encounter.SetPlayerPositionFromHost(client, new AimVector(0, 1.6, 5)); encounter.StartWavesFromHost(0);
            byte id = encounter.Enemy(0).Id; encounter.SetEnemyPositionFromHost(id, new AimVector(0, 1.6, 6));
            for (int i = 0; i < 10; i++) encounter.MeleeFromHost(id, client, i, true);
            for (int i = 10; i < 20; i++) encounter.MeleeFromHost(id, host, i, true);
            Assert.That(encounter.Phase, Is.EqualTo(EncounterPhase.Failed)); Assert.That(encounter.HordeAt(20).Phase, Is.EqualTo(HordePhase.Failed));
            Assert.That(encounter.AdvanceWavesFromHost(30), Is.False);
            Assert.That(encounter.ProposeStepFromHost(encounter.Enemy(1).Id, 30, out _, out _), Is.False);
        }
        [Test]
        public void LastLivingDisconnectDuringBreakFailsBeforeSpawning()
        {
            encounter.SetPlayerPositionFromHost(host, new AimVector(0, 1.6, 5)); encounter.StartWavesFromHost(0);
            byte id = encounter.Enemy(0).Id; encounter.SetEnemyPositionFromHost(id, new AimVector(0, 1.6, 6));
            for (int i = 0; i < 10; i++) encounter.MeleeFromHost(id, host, i, true);
            Clear(9, client); session.Disconnect(client);
            Assert.That(encounter.AdvanceWavesFromHost(14), Is.False);
            Assert.That(encounter.HordeAt(14).Phase, Is.EqualTo(HordePhase.Failed)); Assert.That(encounter.HordeAt(14).Wave, Is.EqualTo(1));
        }
        [Test]
        public void InvalidTimeAndOldRoundCannotAdvanceOrDamageWaves()
        {
            Assert.That(encounter.StartWavesFromHost(double.NaN), Is.False); encounter.StartWavesFromHost(0);
            Assert.That(encounter.AdvanceWavesFromHost(-1), Is.False);
            Assert.That(encounter.DamageEnemyFromHost(host, 255, 100, 1), Is.False);
            session.TryReturnToLobby(host); session.TryBeginRound(host);
            Assert.That(encounter.AdvanceWavesFromHost(10), Is.False);
            Assert.That(encounter.DamageEnemyFromHost(host, 4, 100, 10), Is.False);
            var fresh = new HostInfectedEncounter(session, 10); Assert.That(fresh.Phase, Is.EqualTo(EncounterPhase.Dormant));
            Assert.That(fresh.PlayerHealth(host), Is.EqualTo(100));
        }
        [Test]
        public void SpawnSlotsAreDistinctBoundedAndAbortIsHostOnlyState()
        {
            for (int i = 0; i < 4; i++)
            {
                Assert.That(HostMoveGate.ValidPosition(HostInfectedEncounter.WaveSpawn(i)), Is.True);
                for (int j = 0; j < i; j++) Assert.That((HostInfectedEncounter.WaveSpawn(i) - HostInfectedEncounter.WaveSpawn(j)).LengthSquared, Is.GreaterThan(1));
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => HostInfectedEncounter.WaveSpawn(4));
            Assert.That(encounter.AbortFromHost(), Is.False); encounter.StartWavesFromHost(0);
            Assert.That(encounter.AbortFromHost(), Is.True); Assert.That(encounter.HordeAt(0).Phase, Is.EqualTo(HordePhase.Failed));
        }
        private static RangeSnapshot Wire(EnemySnapshot[] enemies, HordeSnapshot horde, EncounterPhase phase = EncounterPhase.Active)
        {
            var members = new RangeMember[10]; for (byte i = 0; i < 10; i++) members[i] = new RangeMember(i, i + 1, i, 12, false);
            return new RangeSnapshot(1, 1, SessionPhase.Playing, members, new[] { 100, 100, 100 }, phase, enemies, horde);
        }
        private static EnemySnapshot[] FullWave()
        {
            var enemies = new EnemySnapshot[4];
            for (byte i = 0; i < 4; i++) enemies[i] = new EnemySnapshot((byte)(12 + i), 100, HostInfectedEncounter.WaveSpawn(i));
            return enemies;
        }
        [Test]
        public void MaximumHordeSnapshotCopiesAndRoundTripsEveryBoundedEnemy()
        {
            var enemies = FullWave(); var state = Wire(enemies, new HordeSnapshot(HordePhase.Fighting, 3, 3)); enemies[0] = default;
            byte[] bytes = LanRangeProtocol.Encode(state); Assert.That(bytes.Length, Is.EqualTo(442));
            Assert.That(LanRangeProtocol.TrySnapshot(bytes, out RangeSnapshot decoded), Is.True);
            Assert.That(decoded.EnemyCount, Is.EqualTo(4)); Assert.That(decoded.Enemy(0).Id, Is.EqualTo(12));
            Assert.That(decoded.Enemy(3).Health, Is.EqualTo(100)); Assert.That(decoded.Horde.Wave, Is.EqualTo(3));
        }
        [Test]
        public void TruncatedOversizedOrForgedEnemyCountPacketsAreRejectedBeforeAllocation()
        {
            byte[] bytes = LanRangeProtocol.Encode(Wire(FullWave(), new HordeSnapshot(HordePhase.Fighting, 3, 3)));
            for (int length = 0; length < bytes.Length; length++)
            { byte[] cut = new byte[length]; Array.Copy(bytes, cut, length); Assert.That(LanRangeProtocol.TrySnapshot(cut, out _), Is.False); }
            byte[] extra = new byte[443]; Array.Copy(bytes, extra, bytes.Length); Assert.That(LanRangeProtocol.TrySnapshot(extra, out _), Is.False);
            bytes[45] = 255; Assert.That(LanRangeProtocol.TrySnapshot(bytes, out _), Is.False);
        }
        [Test]
        public void DuplicateStaleEnemyIdsAndNonFiniteCountdownAreRejected()
        {
            var enemies = FullWave(); enemies[1] = enemies[0];
            Assert.Throws<ArgumentException>(() => LanRangeProtocol.Encode(Wire(enemies, new HordeSnapshot(HordePhase.Fighting, 3, 3))));
            enemies = FullWave(); enemies[0] = new EnemySnapshot(4, 100, HostInfectedEncounter.Spawn);
            Assert.Throws<ArgumentException>(() => LanRangeProtocol.Encode(Wire(enemies, new HordeSnapshot(HordePhase.Fighting, 3, 3))));
            Assert.Throws<ArgumentException>(() => LanRangeProtocol.Encode(Wire(FullWave(), new HordeSnapshot(HordePhase.Fighting, 3, 3, double.NaN))));
            byte[] old = LanRangeProtocol.Handshake; old[3] = 4; Assert.That(LanRangeProtocol.AcceptHandshake(old), Is.False);
        }
        [Test]
        public void HordePhaseMustMatchEnemyHealthAndWaveProgress()
        {
            Assert.Throws<ArgumentException>(() => LanRangeProtocol.Encode(Wire(FullWave(), new HordeSnapshot(HordePhase.Completed, 3, 3), EncounterPhase.Cleared)));
            var dead = new[] { new EnemySnapshot(4, 0, HostInfectedEncounter.WaveSpawn(0)), new EnemySnapshot(5, 0, HostInfectedEncounter.WaveSpawn(1)) };
            Assert.Throws<ArgumentException>(() => LanRangeProtocol.Encode(Wire(dead, new HordeSnapshot(HordePhase.Fighting, 1, 3))));
            byte[] bytes = LanRangeProtocol.Encode(Wire(dead, new HordeSnapshot(HordePhase.Intermission, 1, 3, 4.5)));
            Assert.That(LanRangeProtocol.TrySnapshot(bytes, out RangeSnapshot parsed), Is.True); Assert.That(parsed.Horde.IntermissionSeconds, Is.EqualTo(4.5));
            Assert.Throws<ArgumentException>(() => LanRangeProtocol.Encode(Wire(dead, new HordeSnapshot(HordePhase.Intermission, 1, 3, 6))));
        }
    }
}
