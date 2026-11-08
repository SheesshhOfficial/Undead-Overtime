using System;
using NUnit.Framework;

namespace UndeadOvertime.Core.Tests
{
    public sealed class HostReviveTests
    {
        private LanSession session;
        private HostInfectedEncounter encounter;
        private PlayerTicket host, client;
        [SetUp]
        public void Setup()
        {
            session = new LanSession(4); host = session.StartHost(0); session.TryJoin(7, 4, out client); session.TryBeginRound(host);
            encounter = new HostInfectedEncounter(session, 0);
            encounter.SetPlayerPositionFromHost(host, new AimVector(.5, 1.6, 5));
            encounter.SetPlayerPositionFromHost(client, new AimVector(0, 1.6, 5));
            encounter.StartFromHost(0);
            for (int i = 0; i < 10; i++) encounter.MeleeFromHost(client, i, true);
            Assert.That(encounter.PlayerHealth(client), Is.Zero);
        }
        private bool Tick(double time, bool clear = true)
        {
            Assert.That(encounter.HoldReviveFromHost(host, true, time), Is.True);
            return encounter.AdvanceReviveFromHost(host, client, time, clear);
        }
        private void HoldBetween(double start, double end, bool clear = true)
        {
            int ticks = (int)Math.Round((end - start) * 10);
            for (int i = 0; i <= ticks; i++) Tick(start + i / 10d, clear);
        }
        [Test]
        public void ContinuousThreeSecondHoldRevivesToFortyHealth()
        {
            HoldBetween(10, 12.9); Assert.That(encounter.PlayerHealth(client), Is.Zero);
            Assert.That(encounter.ReviveStatusFromHost(host, 12.9, out PlayerTicket target, out byte progress), Is.True);
            Assert.That(target, Is.EqualTo(client)); Assert.That(progress, Is.InRange(90, 99));
            Assert.That(Tick(13), Is.True); Assert.That(encounter.PlayerHealth(client), Is.EqualTo(40));
            Assert.That(encounter.ReviveStatusFromHost(host, 13, out _, out _), Is.False);
        }
        [Test]
        public void OneHoldPacketCannotCompleteAReviveAfterSilence()
        {
            Tick(10);
            Assert.That(encounter.AdvanceReviveFromHost(host, client, 13, true), Is.False);
            Assert.That(encounter.PlayerHealth(client), Is.Zero);
        }
        [Test]
        public void ExplicitReleaseResetsAllAccumulatedProgress()
        {
            HoldBetween(10, 12); encounter.HoldReviveFromHost(host, false, 12.01);
            HoldBetween(12.1, 15); Assert.That(encounter.PlayerHealth(client), Is.Zero);
            Assert.That(Tick(15.2), Is.True);
        }
        [Test]
        public void OccludedReviveCannotAccumulateAndMustRestartWhenClear()
        {
            HoldBetween(10, 12); Tick(12.1, false);
            HoldBetween(12.2, 15.1); Assert.That(encounter.PlayerHealth(client), Is.Zero);
            Assert.That(Tick(15.3), Is.True);
        }
        [Test]
        public void LeavingRangeCancelsProgressEvenIfHoldPacketsContinue()
        {
            HoldBetween(10, 12); encounter.SetPlayerPositionFromHost(host, new AimVector(4, 1.6, 5));
            Assert.That(Tick(12.1), Is.False); Assert.That(encounter.ReviveStatusFromHost(host, 12.1, out _, out _), Is.False);
            encounter.SetPlayerPositionFromHost(host, new AimVector(.5, 1.6, 5));
            HoldBetween(12.2, 15.1); Assert.That(encounter.PlayerHealth(client), Is.Zero); Assert.That(Tick(15.3), Is.True);
        }
        [Test]
        public void TakingDamageInterruptsTheRescuersChannel()
        {
            HoldBetween(10, 12); Assert.That(encounter.MeleeFromHost(host, 12.01, true), Is.True);
            Assert.That(encounter.ReviveStatusFromHost(host, 12.01, out _, out _), Is.False);
            HoldBetween(12.1, 15); Assert.That(encounter.PlayerHealth(client), Is.Zero); Assert.That(Tick(15.2), Is.True);
        }
        [Test]
        public void DownedPlayersCannotReviveAndAllDownIsFinalForThatRound()
        {
            Assert.That(encounter.HoldReviveFromHost(client, true, 10), Is.False);
            for (int i = 10; i < 20; i++) encounter.MeleeFromHost(host, i, true);
            Assert.That(encounter.Phase, Is.EqualTo(EncounterPhase.Failed));
            Assert.That(encounter.HoldReviveFromHost(host, true, 20), Is.False);
        }
        [Test]
        public void ClearingEnemyDoesNotPreventRescuingATeammate()
        {
            encounter.DamageEnemyFromHost(host, 100); Assert.That(encounter.Phase, Is.EqualTo(EncounterPhase.Cleared));
            HoldBetween(10, 13); Assert.That(encounter.PlayerHealth(client), Is.EqualTo(40));
            Assert.That(encounter.ProposeReviveFromHost(host, 13, out _), Is.False); // Alive players are not healing targets.
        }
        [Test]
        public void DisconnectAndNewRoundInvalidateHeldRescue()
        {
            HoldBetween(10, 11); session.Disconnect(client);
            Assert.That(Tick(11.1), Is.False);
            session.TryReturnToLobby(host); session.TryJoin(7, 4, out PlayerTicket replacement); session.TryBeginRound(host);
            Assert.That(encounter.HoldReviveFromHost(host, true, 12), Is.False);
            Assert.That(encounter.AdvanceReviveFromHost(host, replacement, 12, true), Is.False);
        }
        [Test]
        public void TwoRescuersCannotReviveTheSameTargetTwice()
        {
            session.TryReturnToLobby(host); session.TryJoin(8, 4, out PlayerTicket second); session.TryBeginRound(host);
            encounter = new HostInfectedEncounter(session, 0);
            encounter.SetPlayerPositionFromHost(host, new AimVector(.5, 1.6, 5));
            encounter.SetPlayerPositionFromHost(second, new AimVector(-.5, 1.6, 5));
            encounter.SetPlayerPositionFromHost(client, new AimVector(0, 1.6, 5)); encounter.StartFromHost(0);
            for (int i = 0; i < 10; i++) encounter.MeleeFromHost(client, i, true);
            bool secondCompleted = false;
            for (int i = 0; i <= 30; i++)
            {
                double now = 10 + i / 10d;
                encounter.HoldReviveFromHost(host, true, now); encounter.HoldReviveFromHost(second, true, now);
                encounter.AdvanceReviveFromHost(host, client, now, true);
                secondCompleted |= encounter.AdvanceReviveFromHost(second, client, now, true);
            }
            Assert.That(encounter.PlayerHealth(client), Is.EqualTo(40)); Assert.That(secondCompleted, Is.False);
            Assert.That(encounter.ReviveStatusFromHost(second, 13, out _, out _), Is.False);
        }
        [Test]
        public void InvalidClockAndWrongTicketCannotHoldOrCompleteRevive()
        {
            Tick(10);
            Assert.That(encounter.HoldReviveFromHost(host, true, 9.9), Is.False);
            Assert.That(encounter.HoldReviveFromHost(host, true, double.NaN), Is.False);
            Assert.That(encounter.AdvanceReviveFromHost(default, client, 11, true), Is.False);
            Assert.That(encounter.AdvanceReviveFromHost(host, host, 11, true), Is.False);
        }
        [Test]
        public void ReviveWireAllowsOnlyHoldOrReleaseWithoutClientChosenTarget()
        {
            foreach (var intent in new[] { new AimVector(0, 0, 1), default(AimVector) })
            {
                byte[] bytes = LanRangeProtocol.Encode(new RangeCommand(RangeAction.Revive, 1, 1, 2, intent));
                Assert.That(bytes.Length, Is.EqualTo(44)); Assert.That(LanRangeProtocol.TryCommand(bytes, out RangeCommand command), Is.True);
                Assert.That(command.Action, Is.EqualTo(RangeAction.Revive)); Assert.That(command.Aim.Z, Is.EqualTo(intent.Z));
            }
            Assert.Throws<ArgumentException>(() => LanRangeProtocol.Encode(new RangeCommand(RangeAction.Revive, 1, 1, 1, new AimVector(0, 1, 0))));
        }
        [Test]
        public void ProgressSnapshotRequiresALivingRescuerAndExistingDownedTarget()
        {
            var rows = new[] { new RangeMember(0, 1, 0, 12, false, HostMoveGate.Spawn(0), 90, 1, 50),
                new RangeMember(7, 2, 1, 12, false, HostMoveGate.Spawn(1), 0) };
            var snapshot = new RangeSnapshot(1, 1, SessionPhase.Playing, rows, new[] { 100, 100, 100 },
                new InfectedSnapshot(EncounterPhase.Cleared, 0, HostInfectedEncounter.Spawn));
            Assert.That(LanRangeProtocol.TrySnapshot(LanRangeProtocol.Encode(snapshot), out RangeSnapshot decoded), Is.True);
            Assert.That(decoded.Member(0).ReviveTargetSlot, Is.EqualTo(1)); Assert.That(decoded.Member(0).ReviveProgress, Is.EqualTo(50));
            rows[1] = new RangeMember(7, 2, 1, 12, false); // Target is alive, so this progress is inconsistent.
            Assert.Throws<ArgumentException>(() => LanRangeProtocol.Encode(new RangeSnapshot(1, 1, SessionPhase.Playing, rows, new[] { 100, 100, 100 },
                new InfectedSnapshot(EncounterPhase.Cleared, 0, HostInfectedEncounter.Spawn))));
        }
    }
}
