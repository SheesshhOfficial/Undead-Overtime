using NUnit.Framework;

namespace UndeadOvertime.Core.Tests
{
    public sealed class LocalDamageFeedbackTests
    {
        private const ulong LocalPeer = 0;
        private LocalDamageFeedback feedback;
        [SetUp] public void Setup() { feedback = new LocalDamageFeedback(); }
        private static RangeMember Member(byte health, ulong peer = LocalPeer, long connection = 11, byte slot = 0) =>
            new RangeMember(peer, connection, slot, 12, false, HostMoveGate.Spawn(slot), health);
        private static RangeSnapshot Snapshot(byte health, long session = 1, long round = 1, long connection = 11,
            SessionPhase phase = SessionPhase.Playing) =>
            Snapshot(new[] { Member(health, connection: connection) }, session, round, phase);
        private static RangeSnapshot Snapshot(RangeMember[] members, long session = 1, long round = 1,
            SessionPhase phase = SessionPhase.Playing) =>
            new RangeSnapshot(session, round, phase, members, new[] { 100, 100, 100 });
        private void Damage()
        {
            Assert.That(feedback.Observe(Snapshot(100), LocalPeer, 0), Is.True);
            Assert.That(feedback.Observe(Snapshot(75), LocalPeer, 1), Is.True);
        }
        private void Quiet(double now, bool downed = false)
        {
            LocalDamageFeedbackState state = feedback.Read(now);
            Assert.That(state.DamagePulse, Is.Zero); Assert.That(state.LastDamage, Is.Zero);
            Assert.That(state.IsDowned, Is.EqualTo(downed));
        }

        [Test]
        public void FirstHealthyInjuredOrDownedSnapshotNeverInventsADamageEvent()
        {
            foreach (byte health in new byte[] { 100, 75, 0 })
            {
                feedback.Reset(); Assert.That(feedback.Observe(Snapshot(health), LocalPeer, 0), Is.True);
                Quiet(0, health == 0);
            }
        }

        [Test]
        public void HealthDropShowsObservedLossWithNoSourceOrDirection()
        {
            Damage(); LocalDamageFeedbackState state = feedback.Read(1);
            Assert.That(state.DamagePulse, Is.EqualTo(1)); Assert.That(state.LastDamage, Is.EqualTo(25));
            Assert.That(state.IsDowned, Is.False);
        }

        [Test]
        public void ReadsFadeWithoutExtendingLifetimeAndExpireAtTheBoundary()
        {
            foreach (double start in new[] { 0d, 1d, 1234.5d })
            {
                feedback.Reset(); feedback.Observe(Snapshot(100), LocalPeer, start); feedback.Observe(Snapshot(75), LocalPeer, start);
                double half = start + LocalDamageFeedback.DamagePulseSeconds / 2;
                Assert.That(feedback.Read(half).DamagePulse, Is.EqualTo(.5).Within(1e-9));
                Assert.That(feedback.Read(half).DamagePulse, Is.EqualTo(.5).Within(1e-9));
                Quiet(start + LocalDamageFeedback.DamagePulseSeconds); Quiet(start + LocalDamageFeedback.DamagePulseSeconds + 10);
            }
        }

        [Test]
        public void DuplicateHealthSnapshotsDoNotRestartThePulse()
        {
            Damage(); Assert.That(feedback.Observe(Snapshot(75), LocalPeer, 1.4), Is.True);
            Assert.That(feedback.Read(1.4).DamagePulse, Is.LessThan(.4)); Quiet(1.7);
            feedback.Observe(Snapshot(75), LocalPeer, 2); Quiet(2);
        }

        [Test]
        public void ANewLossReplacesLastDamageAndRestartsOnlyItsOwnPulse()
        {
            Damage(); feedback.Observe(Snapshot(65), LocalPeer, 1.4);
            LocalDamageFeedbackState state = feedback.Read(1.4);
            Assert.That(state.DamagePulse, Is.EqualTo(1)); Assert.That(state.LastDamage, Is.EqualTo(10));
            Quiet(2.1);
        }

        [Test]
        public void HealthIncreaseClearsInjuryInsteadOfClaimingAnotherHit()
        {
            Damage(); feedback.Observe(Snapshot(90), LocalPeer, 1.1); Quiet(1.1);
            feedback.Observe(Snapshot(80), LocalPeer, 1.2);
            Assert.That(feedback.Read(1.2).LastDamage, Is.EqualTo(10));
        }

        [Test]
        public void DowningClearsThePulseAndReportsTheCurrentDownedState()
        {
            Damage(); feedback.Observe(Snapshot(0), LocalPeer, 1.1); Quiet(1.1, true);
            feedback.Observe(Snapshot(0), LocalPeer, 1.2); Quiet(1.2, true);
        }

        [Test]
        public void RevivalIsQuietAndSubsequentDamageUsesRestoredHealth()
        {
            feedback.Observe(Snapshot(0), LocalPeer, 0); feedback.Observe(Snapshot(40), LocalPeer, 1); Quiet(1);
            feedback.Observe(Snapshot(15), LocalPeer, 1.1);
            Assert.That(feedback.Read(1.1).LastDamage, Is.EqualTo(25));
        }

        [Test]
        public void ANewSessionEstablishesABaselineInsteadOfComparingOldHealth()
        {
            Damage(); feedback.Observe(Snapshot(20, session: 2), LocalPeer, 1.1); Quiet(1.1);
            feedback.Observe(Snapshot(10, session: 2), LocalPeer, 1.2);
            Assert.That(feedback.Read(1.2).LastDamage, Is.EqualTo(10));
        }

        [Test]
        public void ANewRoundClearsPulseEvenIfTheSameConnectionReturnsInjured()
        {
            Damage(); feedback.Observe(Snapshot(20, round: 2), LocalPeer, 1.1); Quiet(1.1);
        }

        [Test]
        public void ReusedPeerAndSlotWithANewConnectionNeverCarryOldDamage()
        {
            Damage(); feedback.Observe(Snapshot(20, connection: 12), LocalPeer, 1.1); Quiet(1.1);
        }

        [Test]
        public void ChangingTheSelectedLocalPeerStartsANewBaseline()
        {
            Damage(); feedback.Observe(Snapshot(new[] { Member(10, peer: 7) }), 7, 1.1); Quiet(1.1);
        }

        [Test]
        public void TeammateHealthLossNeverAppearsAsLocalDamage()
        {
            feedback.Observe(Snapshot(new[] { Member(100), Member(100, 7, 12, 1) }), LocalPeer, 0);
            feedback.Observe(Snapshot(new[] { Member(0, 7, 12, 1), Member(100) }), LocalPeer, 1); Quiet(1);
        }

        [Test]
        public void MissingLocalMemberClearsAndReappearanceOnlyRebaselines()
        {
            Damage(); Assert.That(feedback.Observe(Snapshot(new[] { Member(10, 7, 12, 1) }), LocalPeer, 1.1), Is.False);
            Quiet(1.1); feedback.Observe(Snapshot(10), LocalPeer, 1.2); Quiet(1.2);
        }

        [Test]
        public void NullSnapshotClearsDamageAndDownedState()
        {
            Damage(); Assert.That(feedback.Observe(null, LocalPeer, 1.1), Is.False); Quiet(1.1);
            feedback.Observe(Snapshot(0), LocalPeer, 1.2);
            Assert.That(feedback.Observe(null, LocalPeer, 1.3), Is.False); Quiet(1.3);
        }

        [Test]
        public void LobbyStoppedAndUnsupportedPhasesClearThePlayingBaseline()
        {
            foreach (SessionPhase phase in new[] { SessionPhase.Lobby, SessionPhase.Stopped, (SessionPhase)255 })
            {
                feedback.Reset(); Damage();
                Assert.That(feedback.Observe(Snapshot(75, phase: phase), LocalPeer, 1.1), Is.False); Quiet(1.1);
                feedback.Observe(Snapshot(10), LocalPeer, 1.2); Quiet(1.2);
            }
        }

        [Test]
        public void InvalidLocalHealthConnectionOrDuplicateLocalIdentityFailsClosed()
        {
            foreach (RangeMember[] members in new[] {
                new[] { Member(101) }, new[] { Member(10, connection: 0) }, new[] { Member(10, connection: -1) },
                new[] { Member(10), Member(20, connection: 12, slot: 1) },
                new[] { new RangeMember(LocalPeer, 11, 10, 12, false, HostMoveGate.Spawn(0), 10) } })
            {
                feedback.Reset(); Damage();
                Assert.That(feedback.Observe(Snapshot(members), LocalPeer, 1.1), Is.False); Quiet(1.1);
                feedback.Observe(Snapshot(10), LocalPeer, 1.2); Quiet(1.2);
            }
        }

        [Test]
        public void InvalidSessionRoundAndMemberCountsFailClosed()
        {
            foreach (RangeSnapshot snapshot in new[] {
                Snapshot(10, session: 0), Snapshot(10, session: -1), Snapshot(10, round: 0), Snapshot(10, round: -1),
                Snapshot(new RangeMember[0]), Snapshot(new RangeMember[LanSession.MaximumPlayers + 1]) })
            {
                feedback.Reset(); Damage(); Assert.That(feedback.Observe(snapshot, LocalPeer, 1.1), Is.False); Quiet(1.1);
            }
        }

        [Test]
        public void NonfiniteNegativeOrExcessiveTimeClearsWithoutPoisoningTheClock()
        {
            foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1d, HostFireGate.MaximumHostTime + 1 })
            {
                feedback.Reset(); Damage(); Assert.That(feedback.Observe(Snapshot(10), LocalPeer, invalid), Is.False); Quiet(1.1);
                feedback.Observe(Snapshot(5), LocalPeer, 1.2); Quiet(1.2);
                feedback.Reset(); Damage();
                LocalDamageFeedbackState rejected = feedback.Read(invalid);
                Assert.That(rejected.DamagePulse, Is.Zero); Assert.That(rejected.LastDamage, Is.Zero); Assert.That(rejected.IsDowned, Is.False);
                feedback.Observe(Snapshot(5), LocalPeer, 1.2); Quiet(1.2);
            }
        }

        [Test]
        public void BackwardObservationClearsButCannotLowerTheClockBoundary()
        {
            Damage(); Assert.That(feedback.Observe(Snapshot(50), LocalPeer, .9), Is.False); Quiet(1);
            Assert.That(feedback.Observe(Snapshot(25), LocalPeer, .95), Is.False);
            feedback.Observe(Snapshot(10), LocalPeer, 1.1); Quiet(1.1);
        }

        [Test]
        public void BackwardReadClearsTheBaselineInsteadOfRestoringOldFeedback()
        {
            Damage(); Quiet(.9); Assert.That(feedback.Observe(Snapshot(20), LocalPeer, .95), Is.False);
            feedback.Observe(Snapshot(10), LocalPeer, 1.1); Quiet(1.1);
        }

        [Test]
        public void EqualTimestampsPermitActualNewObservationsWithoutDuplicateRefresh()
        {
            feedback.Observe(Snapshot(100), LocalPeer, 0); feedback.Observe(Snapshot(80), LocalPeer, 0);
            feedback.Observe(Snapshot(70), LocalPeer, 0); Assert.That(feedback.Read(0).LastDamage, Is.EqualTo(10));
            feedback.Observe(Snapshot(70), LocalPeer, 0); Assert.That(feedback.Read(0).DamagePulse, Is.EqualTo(1));
            Quiet(LocalDamageFeedback.DamagePulseSeconds);
        }

        [Test]
        public void ExplicitResetClearsAllStateAndAllowsANewClockDomain()
        {
            Damage(); feedback.Reset(); Quiet(0);
            feedback.Observe(Snapshot(20), LocalPeer, 0); Quiet(0);
            feedback.Observe(Snapshot(10), LocalPeer, 0); Assert.That(feedback.Read(0).LastDamage, Is.EqualTo(10));
            feedback.Reset(); Quiet(0);
        }

        [Test]
        public void FeedbackNeverMutatesSnapshotBytesOrAuthority()
        {
            RangeSnapshot first = Snapshot(100), second = Snapshot(75);
            byte[] firstBytes = LanRangeProtocol.Encode(first), secondBytes = LanRangeProtocol.Encode(second);
            feedback.Observe(first, LocalPeer, 0); feedback.Observe(second, LocalPeer, 1);
            feedback.Read(1.2); feedback.Reset();
            Assert.That(LanRangeProtocol.Encode(first), Is.EqualTo(firstBytes));
            Assert.That(LanRangeProtocol.Encode(second), Is.EqualTo(secondBytes));
        }
    }
}
