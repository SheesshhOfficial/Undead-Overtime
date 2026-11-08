using System;
using NUnit.Framework;

namespace UndeadOvertime.Core.Tests
{
    public sealed class CoopMissionProgressionTests
    {
        private LanSession session;
        private PlayerTicket host, client;
        private CoopMissionProgression mission;
        private double now;
        [SetUp]
        public void Setup()
        {
            session = new LanSession(5); host = session.StartHost(0);
            session.TryJoin(7, 5, out client); session.TryBeginRound(host);
            mission = new CoopMissionProgression(session, 0); now = 0;
        }
        private bool Observe(bool power = false, bool exit = false, HordePhase horde = HordePhase.Disabled, int living = 2)
        {
            bool accepted = mission.AdvanceFromHost(now, new CoopMissionObservation(living, power, exit, horde));
            now += .05; return accepted;
        }
        private void Power()
        {
            for (int i = 0; i <= 60; i++) Assert.That(Observe(power: true), Is.True);
            Assert.That(mission.Phase, Is.EqualTo(CoopMissionPhase.DefendDepot));
        }
        private void ExitReady()
        {
            Power(); Observe(horde: HordePhase.Fighting); Observe(horde: HordePhase.Completed);
            Assert.That(mission.Phase, Is.EqualTo(CoopMissionPhase.ReachExtraction));
        }
        [Test]
        public void FullObjectiveOrderRequiresPowerWavesAndSeparateExtractionHold()
        {
            Assert.That(mission.Snapshot.DepotGateUnlocked, Is.False);
            Assert.That(Observe(exit: true, horde: HordePhase.Completed), Is.False);
            Power(); Assert.That(mission.Snapshot.DepotGateUnlocked, Is.True);
            Observe(exit: true, horde: HordePhase.Intermission);
            Assert.That(mission.Phase, Is.EqualTo(CoopMissionPhase.DefendDepot));
            Observe(exit: true, horde: HordePhase.Completed);
            Assert.That(mission.Phase, Is.EqualTo(CoopMissionPhase.ReachExtraction));
            Assert.That(mission.Snapshot.HoldSeconds, Is.Zero);
            for (int i = 0; i < 40; i++) Observe(exit: true, horde: HordePhase.Completed);
            Assert.That(mission.Phase, Is.EqualTo(CoopMissionPhase.ReachExtraction));
            Observe(exit: true, horde: HordePhase.Completed);
            Assert.That(mission.Phase, Is.EqualTo(CoopMissionPhase.Completed));
        }
        [Test]
        public void InitialObservationCannotReceiveRetroactiveInteractionCredit()
        {
            now = 10; Observe(power: true);
            Assert.That(mission.Snapshot.HoldSeconds, Is.Zero);
            Observe(power: true); Assert.That(mission.Snapshot.HoldSeconds, Is.EqualTo(.05).Within(1e-8));
        }
        [Test]
        public void RepeatedTickAndExtraTeammatesCannotMultiplyHoldSpeed()
        {
            var ten = new LanSession(5); var owner = ten.StartHost(0);
            for (ulong i = 1; i < 10; i++) ten.TryJoin(i, 5, out _);
            ten.TryBeginRound(owner); var group = new CoopMissionProgression(ten, 0);
            for (int i = 0; i < 60; i++)
            {
                double time = i * .05;
                Assert.That(group.AdvanceFromHost(time, new CoopMissionObservation(10, true, false, HordePhase.Disabled)), Is.True);
                for (int actor = 0; actor < 10; actor++)
                    Assert.That(group.AdvanceFromHost(time, new CoopMissionObservation(10, true, false, HordePhase.Disabled)), Is.False);
            }
            Assert.That(group.Phase, Is.EqualTo(CoopMissionPhase.RestorePower));
            Assert.That(group.AdvanceFromHost(3, new CoopMissionObservation(10, true, false, HordePhase.Disabled)), Is.True);
            Assert.That(group.Phase, Is.EqualTo(CoopMissionPhase.DefendDepot));
        }
        [Test]
        public void ReleasingPowerCancelsContinuousHoldWithoutUnlocking()
        {
            for (int i = 0; i < 40; i++) Observe(power: true);
            Assert.That(mission.Snapshot.HoldSeconds, Is.GreaterThan(1));
            Observe(); Assert.That(mission.Snapshot.HoldSeconds, Is.Zero);
            for (int i = 0; i < 40; i++) Observe(power: true);
            Assert.That(mission.Phase, Is.EqualTo(CoopMissionPhase.RestorePower));
            Assert.That(mission.Snapshot.DepotGateUnlocked, Is.False);
        }
        [Test]
        public void HostPauseBreaksContinuityInsteadOfCompletingHold()
        {
            for (int i = 0; i < 40; i++) Observe(power: true);
            now += 10; Observe(power: true);
            Assert.That(mission.Snapshot.HoldSeconds, Is.Zero);
            Assert.That(mission.Phase, Is.EqualTo(CoopMissionPhase.RestorePower));
        }
        [Test]
        public void SlowHostTickHasBoundedCredit()
        {
            Observe(power: true); now = .2; Observe(power: true);
            Assert.That(mission.Snapshot.HoldSeconds, Is.EqualTo(CoopMissionProgression.MaximumStep));
        }
        [TestCase(-1)] [TestCase(3)] [TestCase(11)]
        public void InvalidLifeCountCancelsProgress(int living)
        {
            Observe(power: true); Observe(power: true);
            Assert.That(Observe(power: true, living: living), Is.False);
            Assert.That(mission.Snapshot.HoldSeconds, Is.Zero);
            Assert.That(mission.Phase, Is.EqualTo(CoopMissionPhase.RestorePower));
        }
        [Test]
        public void InvalidHordePhaseDoesNotUnlockOrComplete()
        {
            Assert.That(Observe(power: true, horde: (HordePhase)255), Is.False);
            Assert.That(mission.Snapshot.DepotGateUnlocked, Is.False);
        }
        [Test]
        public void BackwardsAndNonFiniteTimeCannotAdvanceState()
        {
            Observe(power: true); Observe(power: true); double progress = mission.Snapshot.HoldSeconds;
            foreach (double time in new[] { 0, -.1, double.NaN, double.PositiveInfinity, HostFireGate.MaximumHostTime + 1 })
                Assert.That(mission.AdvanceFromHost(time, new CoopMissionObservation(2, true, true, HordePhase.Disabled)), Is.False);
            Assert.That(mission.Snapshot.HoldSeconds, Is.EqualTo(progress));
        }
        [Test]
        public void DownedConnectedPlayerBlocksExtractionUntilRevived()
        {
            ExitReady();
            for (int i = 0; i <= 50; i++) Observe(exit: true, horde: HordePhase.Completed, living: 1);
            Assert.That(mission.Phase, Is.EqualTo(CoopMissionPhase.ReachExtraction));
            Assert.That(mission.Snapshot.HoldSeconds, Is.Zero);
            for (int i = 0; i <= 40; i++) Observe(exit: true, horde: HordePhase.Completed);
            Assert.That(mission.Phase, Is.EqualTo(CoopMissionPhase.Completed));
        }
        [Test]
        public void TeammateLeavingExitResetsTheSharedCountdown()
        {
            ExitReady(); for (int i = 0; i < 30; i++) Observe(exit: true, horde: HordePhase.Completed);
            Observe(exit: false, horde: HordePhase.Completed);
            Assert.That(mission.Snapshot.HoldSeconds, Is.Zero);
            for (int i = 0; i < 30; i++) Observe(exit: true, horde: HordePhase.Completed);
            Assert.That(mission.Phase, Is.EqualTo(CoopMissionPhase.ReachExtraction));
        }
        [Test]
        public void DisconnectedMemberDoesNotPermanentlyBlockRemainingTeam()
        {
            ExitReady(); Assert.That(session.Disconnect(client), Is.True);
            for (int i = 0; i <= 40; i++) Observe(exit: true, horde: HordePhase.Completed, living: 1);
            Assert.That(mission.Phase, Is.EqualTo(CoopMissionPhase.Completed));
        }
        [Test]
        public void GateRemainsUnlockedThroughBacktrackingAndFailure()
        {
            Power(); Assert.That(mission.Snapshot.DepotGateUnlocked, Is.True);
            Observe(horde: HordePhase.Intermission); Assert.That(mission.Snapshot.DepotGateUnlocked, Is.True);
            Assert.That(mission.FailFromHost(), Is.True); Assert.That(mission.Snapshot.DepotGateUnlocked, Is.True);
            Assert.That(mission.Phase, Is.EqualTo(CoopMissionPhase.Failed)); Assert.That(mission.FailFromHost(), Is.False);
        }
        [TestCase(false)] [TestCase(true)]
        public void TeamWipeOrHordeFailureIsTerminal(bool hordeFailure)
        {
            Observe(horde: hordeFailure ? HordePhase.Failed : HordePhase.Disabled, living: hordeFailure ? 2 : 0);
            Assert.That(mission.Phase, Is.EqualTo(CoopMissionPhase.Failed));
            Assert.That(Observe(power: true), Is.False); Assert.That(mission.FailFromHost(), Is.False);
        }
        [Test]
        public void CompletedMissionCannotBeLostOrAwardedTwice()
        {
            ExitReady(); for (int i = 0; i <= 40; i++) Observe(exit: true, horde: HordePhase.Completed);
            Assert.That(mission.Phase, Is.EqualTo(CoopMissionPhase.Completed));
            Assert.That(mission.FailFromHost(), Is.False); Assert.That(Observe(horde: HordePhase.Failed, living: 0), Is.False);
            Assert.That(mission.Phase, Is.EqualTo(CoopMissionPhase.Completed));
        }
        [Test]
        public void OldRoundAndStoppedSessionCannotChangeMission()
        {
            Power(); session.TryReturnToLobby(host); session.TryBeginRound(host);
            Assert.That(mission.Snapshot.IsCurrent, Is.False);
            Assert.That(Observe(horde: HordePhase.Completed), Is.False); Assert.That(mission.FailFromHost(), Is.False);
            var fresh = new CoopMissionProgression(session, now);
            Assert.That(fresh.Phase, Is.EqualTo(CoopMissionPhase.RestorePower)); Assert.That(fresh.Snapshot.DepotGateUnlocked, Is.False);
            session.Disconnect(host); Assert.That(fresh.IsCurrent, Is.False);
            Assert.That(fresh.AdvanceFromHost(now, new CoopMissionObservation(0, false, false, HordePhase.Failed)), Is.False);
        }
        [Test]
        public void ExtractionRejectsRegressedWaveState()
        {
            ExitReady(); Observe(exit: true, horde: HordePhase.Completed); Observe(exit: true, horde: HordePhase.Completed);
            Assert.That(Observe(exit: true, horde: HordePhase.Fighting), Is.False);
            Assert.That(mission.Snapshot.HoldSeconds, Is.Zero); Assert.That(mission.Phase, Is.EqualTo(CoopMissionPhase.ReachExtraction));
        }
        [Test]
        public void ConstructionRequiresCurrentRoundAndFiniteHostClock()
        {
            Assert.Throws<ArgumentNullException>(() => new CoopMissionProgression(null, 0));
            Assert.Throws<InvalidOperationException>(() => new CoopMissionProgression(new LanSession(5), 0));
            foreach (double time in new[] { -1d, double.NaN, double.PositiveInfinity, HostFireGate.MaximumHostTime + 1 })
                Assert.Throws<ArgumentOutOfRangeException>(() => new CoopMissionProgression(session, time));
        }
    }
}
