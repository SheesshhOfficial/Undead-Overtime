using System;
using NUnit.Framework;

namespace UndeadOvertime.Core.Tests
{
    public sealed class HostInfectedEncounterTests
    {
        private LanSession session;
        private PlayerTicket host, client;
        private HostInfectedEncounter encounter;
        [SetUp]
        public void Setup()
        {
            session = new LanSession(3); host = session.StartHost(0); session.TryJoin(4, 3, out client); session.TryBeginRound(host);
            encounter = new HostInfectedEncounter(session, 0);
            encounter.SetPlayerPositionFromHost(host, HostMoveGate.Spawn(0));
            encounter.SetPlayerPositionFromHost(client, HostMoveGate.Spawn(1));
        }
        private void Contact(PlayerTicket ticket)
        { encounter.SetPlayerPositionFromHost(ticket, new AimVector(0, 1.6, 5)); }
        [Test]
        public void EncounterIsOptInAndCannotRestartInTheSameRound()
        {
            Assert.That(encounter.Snapshot.Phase, Is.EqualTo(EncounterPhase.Dormant)); Assert.That(encounter.Snapshot.Health, Is.Zero);
            Assert.That(encounter.ProposeStepFromHost(0, out _, out _), Is.False);
            Assert.That(encounter.StartFromHost(0), Is.True); Assert.That(encounter.StartFromHost(0), Is.False);
            Assert.That(encounter.PlayerHealth(host), Is.EqualTo(100)); Assert.That(encounter.Snapshot.Health, Is.EqualTo(100));
        }
        [Test]
        public void PursuitSelectsNearestAliveCurrentPlayerWithBoundedStep()
        {
            encounter.StartFromHost(0);
            Assert.That(encounter.ProposeStepFromHost(.2, out AimVector delta, out PlayerTicket target), Is.True);
            Assert.That(target, Is.EqualTo(client)); Assert.That(delta.Y, Is.Zero);
            Assert.That(delta.LengthSquared, Is.EqualTo(.0064).Within(.000001)); // 1.6 * .05 metres.
            Assert.That(encounter.ProposeStepFromHost(.1, out _, out _), Is.False);
            Assert.That(encounter.ProposeStepFromHost(double.NaN, out _, out _), Is.False);
            Assert.That(encounter.SetEnemyPositionFromHost(new AimVector(99, 1.6, 0)), Is.False);
        }
        [Test]
        public void MeleeRequiresRangeLineOfSightAndCooldown()
        {
            encounter.StartFromHost(0);
            Assert.That(encounter.MeleeFromHost(client, 0, true), Is.False);
            Contact(client); Assert.That(encounter.MeleeFromHost(client, 0, false), Is.False);
            Assert.That(encounter.MeleeFromHost(client, 0, true), Is.True);
            Assert.That(encounter.PlayerHealth(client), Is.EqualTo(90));
            Assert.That(encounter.MeleeFromHost(client, .99, true), Is.False);
            Assert.That(encounter.MeleeFromHost(client, 1, true), Is.True);
            Assert.That(encounter.PlayerHealth(client), Is.EqualTo(80));
        }
        [Test]
        public void FourAuthorizedDamageApplicationsClearEnemyAndStopFurtherDamage()
        {
            encounter.StartFromHost(0);
            Assert.That(encounter.DamageEnemyFromHost(client, -1), Is.False);
            Assert.That(encounter.DamageEnemyFromHost(client, 101), Is.False);
            for (int i = 0; i < 4; i++) Assert.That(encounter.DamageEnemyFromHost(client, 25), Is.True);
            Assert.That(encounter.Snapshot.Health, Is.Zero); Assert.That(encounter.Phase, Is.EqualTo(EncounterPhase.Cleared));
            Contact(client); Assert.That(encounter.MeleeFromHost(client, 1, true), Is.False);
            Assert.That(encounter.DamageEnemyFromHost(client, 25), Is.False);
        }
        [Test]
        public void DownedPlayerCannotShootAndPursuitRetargetsSurvivor()
        {
            Contact(client); encounter.StartFromHost(0);
            for (int i = 0; i < 10; i++) Assert.That(encounter.MeleeFromHost(client, i, true), Is.True);
            Assert.That(encounter.PlayerHealth(client), Is.Zero); Assert.That(encounter.Phase, Is.EqualTo(EncounterPhase.Active));
            Assert.That(encounter.DamageEnemyFromHost(client, 25), Is.False);
            Assert.That(encounter.ProposeStepFromHost(10, out _, out PlayerTicket next), Is.True);
            Assert.That(next, Is.EqualTo(host));
        }
        [Test]
        public void AllPlayersDownEndsEncounterAndNewRoundRequiresNewState()
        {
            Contact(host); Contact(client); encounter.StartFromHost(0);
            for (int i = 0; i < 10; i++) encounter.MeleeFromHost(client, i, true);
            for (int i = 10; i < 20; i++) encounter.MeleeFromHost(host, i, true);
            Assert.That(encounter.Phase, Is.EqualTo(EncounterPhase.Failed));
            Assert.That(encounter.ProposeStepFromHost(20, out _, out _), Is.False);
            session.TryReturnToLobby(host); session.TryBeginRound(host);
            Assert.That(encounter.DamageEnemyFromHost(host, 25), Is.False);
            var reset = new HostInfectedEncounter(session, 20);
            Assert.That(reset.PlayerHealth(host), Is.EqualTo(100)); Assert.That(reset.Phase, Is.EqualTo(EncounterPhase.Dormant));
        }
        [Test]
        public void DisconnectedPlayerCannotBeDamagedOrRemainPursuitTarget()
        {
            encounter.StartFromHost(0); session.Disconnect(client);
            Assert.That(encounter.DamageEnemyFromHost(client, 25), Is.False);
            Assert.That(encounter.MeleeFromHost(client, 1, true), Is.False);
            Assert.That(encounter.ProposeStepFromHost(1, out _, out PlayerTicket target), Is.True);
            Assert.That(target, Is.EqualTo(host));
        }
        [Test]
        public void HostDownedMovementFlagCancelsPendingAndHeldIntent()
        {
            var movement = new HostMoveGate(session, 0);
            Assert.That(movement.TrySetIntent(client, 1, 1, 1, new AimVector(0, 0, 1), 0), Is.True);
            Assert.That(movement.SetCanMoveFromHost(client, false), Is.True);
            Assert.That(movement.TrySetIntent(client, 1, 1, 2, new AimVector(0, 0, 1), .01), Is.False);
            Assert.That(movement.TryStepFromHost(.02, out MoveStep[] rows), Is.True); Assert.That(rows, Is.Empty);
        }
        [Test]
        public void EncounterAndPlayerHealthRoundTripInBoundedWireSnapshot()
        {
            var enemy = new InfectedSnapshot(EncounterPhase.Active, 75, HostInfectedEncounter.Spawn);
            var state = new RangeSnapshot(1, 1, SessionPhase.Playing,
                new[] { new RangeMember(0, 1, 0, 11, false, HostMoveGate.Spawn(0), 90) }, new[] { 100, 100, 100 }, enemy);
            byte[] bytes = LanRangeProtocol.Encode(state);
            Assert.That(LanRangeProtocol.TrySnapshot(bytes, out RangeSnapshot parsed), Is.True);
            Assert.That(parsed.Member(0).Health, Is.EqualTo(90)); Assert.That(parsed.Encounter.Health, Is.EqualTo(75));
            Assert.That(parsed.Encounter.Phase, Is.EqualTo(EncounterPhase.Active));
            bytes[38] = 255; Assert.That(LanRangeProtocol.TrySnapshot(bytes, out _), Is.False);
        }
        [Test]
        public void InvalidEncounterPhaseHealthPositionAndOldHandshakeAreRejected()
        {
            foreach (var invalid in new[] {
                new InfectedSnapshot((EncounterPhase)255, 100, HostInfectedEncounter.Spawn),
                new InfectedSnapshot(EncounterPhase.Active, 0, HostInfectedEncounter.Spawn),
                new InfectedSnapshot(EncounterPhase.Failed, 0, HostInfectedEncounter.Spawn),
                new InfectedSnapshot(EncounterPhase.Dormant, 1, HostInfectedEncounter.Spawn),
                new InfectedSnapshot(EncounterPhase.Active, 100, new AimVector(double.NaN, 1.6, 0)) })
                Assert.Throws<ArgumentException>(() => LanRangeProtocol.Encode(new RangeSnapshot(1, 1, SessionPhase.Playing,
                    new[] { new RangeMember(0, 1, 0, 12, false) }, new[] { 100, 100, 100 }, invalid)));
            byte[] old = LanRangeProtocol.Handshake; old[3] = 2;
            Assert.That(LanRangeProtocol.AcceptHandshake(old), Is.False);
        }
    }
}
